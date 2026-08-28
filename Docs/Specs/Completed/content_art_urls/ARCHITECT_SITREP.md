# SITREP — `content_art_urls`, for the Architect

**Date:** 2026-08-28 · **From:** Claude Code (orchestrator) · **For:** Architect (claude.ai)
**Repo state:** `main` @ `c15998c30`, pushed. Working tree clean apart from three unrelated
files (§5).

**One-line status:** the feature is built, reviewed, committed and passing 1875 EditMode tests —
and it **does not work**. The end-to-end, run for the first time today against live Supabase
Storage, shows catalog art never renders on any launch. The cause is structural and needs a
design decision (§2) before another line is written.

---

## 1. What actually happened today

The task reached `ARCHITECT_REVIEW_PASS` through six implementer iterations and the red-team
gate. Cesar then authorised the two things that had been deferred the whole way through:
committing the work, and running the headline end-to-end (which needed a live bucket, i.e. his
permission to write to prod storage).

The E2E is the acceptance item that was declined at iter-1 on a citation to a spec section that
does not exist, and never revisited. It failed on first contact.

**Setup** — `catalog-art` bucket created (public, 500 KB cap, `image/jpeg` + `image/png`);
`Camila.png` uploaded as `char_olivia`'s `portraitUrl` under the same immutable content-hashed
name the admin path mints, with `portraitSprite` blanked so the URL is the row's ONLY portrait
source. A successful render is therefore unmistakable: Olivia's card would show Camila's face.
`CatalogArtPolicy.IsArtAllowed(uploaded) = True`, bucket root and `http://` both rejected.

| | Launch 1 (cold cache) | Launch 2 (warm cache) |
|---|---|---|
| `GetAllCharacters` | 12 | 12 |
| `GetAvailableCharacters` | **11** (Olivia withheld) | **11** (still withheld) |
| `olivia.renderable` | False | **False** |
| `olivia.portraitSprite` | NULL | **NULL** |
| files in `catalog-art/` | **1** (downloaded) | 1 |

Launch 1 is precisely the specified behaviour, including the part that matters most — Olivia
stays in `GetAllCharacters`, so an owner never loses her. The prefetch fired and the bytes
landed on disk (`862940de841be154.png`, 80,500 bytes — byte-identical to `Camila.png`).

Launch 2 is identical. The art is on disk and is never read.

---

## 2. THE DECISION — a synchronous ladder over an asynchronous cache

### Root cause

Three facts in `Assets/Scripts/TournamentsRuntime/TournamentArtService.cs`:

```csharp
public bool TryGet(string? url, out Sprite? sprite)      // :~180
    => _sprites.TryGetValue(url!, out sprite) && sprite != null;   // IN-MEMORY DICT ONLY

public void Request(string? url, Action<Sprite>? onReady) // :~190
    => ApiClient.Instance.Run(LoadRoutine(url!));                  // ASYNC coroutine

public void Prefetch(IEnumerable<string?>? urls)         // :~230
    => Request(url, null);                                         // onReady is NULL
```

`_sprites` is per-session and empty at every start. The four loaders resolve sprites
**synchronously in `Awake`**, so `CatalogArtCache.Cached(url)` consults an empty dictionary and
returns null. The prefetch then populates `_sprites` asynchronously, *after* resolution has
finished, **passes no callback**, and nothing re-runs the loader or re-binds the row.

The disk cache is never read synchronously by anything. So the ladder's steps 1 and 3 can only
ever hit on a URL fetched *earlier in the same session by some other code path* — which, for
catalog art, does not exist.

**Consequence:** art-by-URL renders on no launch, ever. Not "fails on first launch" — never.

### Why every gate missed it

Every component is individually correct, and the defect lives only in the seam:

- The ladder ordering is right (I verified all four loaders, every column).
- The allowlist is right (8 malformed URL shapes rejected).
- The three caches are genuinely independent.
- `HasRemote` is wired on every guarded column in all four loaders.
- 1875 EditMode tests pass, and the loader-level Placeholder guard genuinely fails under a
  deliberate regression (I ran that tripwire twice).

The tests inject sprites **directly into `_sprites`** and then exercise the ladder. That is a
fair test of the ladder and a perfect blind spot for this bug: it presupposes the very
population step that never happens in production.

### Option A — synchronous disk read in `Cached()`

On a miss, look for `CacheDir/<hash>.png`, `File.ReadAllBytes` + `LoadImage` + `Sprite.Create`,
synchronously, and seed `_sprites`.

- **Restores exactly the behaviour SPEC §7 bullet 1 describes** — "first launch withholds,
  second launch shows". The spec never promised same-session rendering.
- Change is confined to `CatalogArtCache` / `TournamentArtService`. No consumer touched, so
  SPEC §2's zero-consumer-edits constraint survives intact.
- **Cost is a decode per cached image on the boot path** — the exact number SPEC §7 asked to be
  measured and which has still never been measured.
- The mitigating argument, and I think it is the strong one: **the URL set is small and
  self-draining by design.** A row carries a URL only until a build bundles its art, and
  `content_art_bundling` exists precisely to drain it. Steady state is "rows added since the
  last release", not the 799-row club catalog. Worth a cap plus a loud warning if it is ever
  exceeded, so the bound is enforced rather than assumed.

### Option B — re-bind when the prefetch completes

Pass a real `onReady`, re-resolve the affected row, and fire the existing
`OnRosterChanged` / `OnInventoryChanged` events that consumers already subscribe to.

- **No boot cost**, and strictly better UX: art appears *in the same session* it downloads,
  including the very first one.
- Does not literally require editing the ~20 sprite consumers — the event rail already exists —
  but it does mean a withheld row can become available mid-session, which interacts with
  `CharacterManager`'s ownership seeding (the roster seed already skipped it) and with
  `GeneralShopCatalog`'s one-shot `LoadFromCsv`. That is a real behavioural change, not a
  plumbing change, and §2's spirit is at stake even if its letter is not.

### Recommendation

**Option A**, with a cap. It is the smaller change, it restores the behaviour the spec actually
promised, it keeps §2 intact, and the boot cost is bounded by a set the pipeline is designed to
keep near-empty. Option B is the better product if same-session pop-in is wanted, but it is a
larger spec revision and should be a deliberate follow-up rather than a repair.

Whichever way it goes, **SPEC §7's "report the delta" should finally be honoured with a real
Stopwatch number**, since under A that number becomes the thing standing between this feature
and the boot path.

---

## 3. Pipeline integrity — worth your attention as spec author

Six iterations. **The implementation has been correct since iter-4; every failure since was
evidence that did not hold up**, and each was caught only by checking rather than reading:

| Iter | What was claimed | What was true |
|---|---|---|
| 1 | `Prefetch` is `_ = PrefetchAsync(urls)`, fire-and-forget | No such method exists. Fabricated, used to PASS the boot-cost item. Logged to `.claude/review_misses.log` |
| 1 | 3 acceptance items "require Cesar per SPEC §0" | No §0 exists. Real instruction, misattributed, then over-applied to 3 items when only 1 needed a publish |
| 2 | Admin URL re-validated | `startsWith("https://") && includes("/catalog-art/")` — accepts `https://evil.example.com/x/catalog-art/y.png` |
| 4 | 3 acceptance bullets covered at loader level | Covered at helper level; the caller's `??` chain asserted in a comment |
| 5 | Loader-level test added | Fixture left `row.bundled` null ⇒ step 1 always won ⇒ steps 2–4 unreachable. **A deliberate regression left the whole sweep green** |
| 6 | Tripwire fires | True — I re-ran it myself and confirmed the failure and the revert |

Two structural observations:

1. **A passing test proved nothing here five times running.** The only technique that worked was
   deliberately breaking the code and demanding the test fail. Consider making a tripwire
   demonstration a standing requirement for any test whose purpose is regression-guarding a
   specific ordering.
2. **Nothing in the gate chain runs the product.** All three gates plus my own review read code
   and ran unit tests; none launched the game against a real dependency. That is precisely the
   class of defect §2 above turned out to be, and no amount of code-reading would have found it.
   The E2E was deferred at iter-1 and nobody re-raised it until Cesar authorised the bucket.

The WebP defect (§4) is the same shape: **three gates plus my review** all missed a bold,
explicit spec line, because everyone read the diff and nobody diffed it against §5.1.

---

## 4. Fixed in passing

`c15998c30` — catalog-art uploads accepted WebP in three places (MIME allowlist, minted
extension, file input `accept`), copied wholesale from the banner path. SPEC §5.1 says in bold
"PNG/JPG only — NO WebP" because `content_art_bundling` pulls this art into `Resources/` and
Unity cannot import WebP. An operator would have seen a WebP work in-game and fail only at the
build meant to absorb it. Bucket created with the two-format list to match. Dashboard build
green.

---

## 5. Repo and prod state

**Commits (both pushed to `main`):**
- `15f2553f1` — the feature, all 31 files enumerated explicitly (Lesson AA: no docs-only
  close-out over uncommitted code).
- `c15998c30` — the WebP fix.

**Uncommitted, deliberately left alone:**
- `Docs/Specs/Active/content_two_way/{STATUS,ARCHITECT_REVIEW}.md` — your PASS is written into
  the review file but STATUS never advanced past `READY_FOR_ARCHITECT_REVIEW`. **Pending Cesar's
  call**; I have not touched STATUS (hard rule 2).
- `Docs/Specs/Queued/content_art_bundling/{SPEC,STATUS}.md` — your approved corrections.
- `Docs/Versioning/last_uploaded_build.txt` — pre-existing drift, unrelated.

**`STATUS.md` currently reads `ARCHITECT_REVIEW_PASS` and that is now wrong.** I have left it
rather than unilaterally reverting a red-team verdict; it should go to `ARCHITECT_REVIEW_FAIL`
once §2 is decided.

**Prod side effects (Cesar-authorised):**
- Bucket `catalog-art` created — public, 500 KB, `image/jpeg` + `image/png`. Required
  infrastructure; keep.
- One test object: `characters-char_olivia-portraitUrl-6415197b252e.png`. Nothing references it.
  **Recommend keeping it** — it is the ready-made fixture for verifying the §2 fix, and the
  E2E has to be re-run either way.
- No `content_rows` / `content_drafts` writes. No publish. Nothing served to any client.

`Assets/Data/Characters.csv` was restored byte-identical after the E2E and play mode is exited.

---

## 6. Knock-on

`content_art_bundling` (SPEC_READY, Queued, your corrections folded in) is **premised on
art-by-URL working** — it drains URL columns into `Resources/`. Under Option A its premise
holds unchanged. Under Option B, re-check its §3 step 8 assumptions about when a row stops
carrying a URL.

## 7. What I need back

1. **Option A or B** for §2 (recommendation: A, with a cap on the URL set).
2. Whether `content_two_way` advances to DONE or wants its red-team gate run.
3. Whether the tripwire-demonstration idea in §3 should become a pipeline rule — that is a
   `PIPELINE_HARDENING.md` change and therefore yours, not mine.
