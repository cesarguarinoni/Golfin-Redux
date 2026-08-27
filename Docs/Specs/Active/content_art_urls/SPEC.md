# SPEC — `content_art_urls`

> **Authoritative spec for this task.** Implementer reads this and ONLY this for the work
> definition. STATUS.md tracks pipeline state. Reports/reviews go in their own files.
>
> Filed 2026-08-27. Follows `content_two_way` (§4 is the foundation this builds on) and
> `game_banners` (whose upload + allowlist + cache path is reused, not re-invented).
> Plan: `Docs/CONTENT_PIPELINE_PLAN.md` §10.2, which already concluded that
> `TournamentArtService` — not Addressables — is the right vehicle and that a second
> instance is "close to free".
>
> **No Figma.** This task has no new player-facing layout: every sprite lands in an `Image`
> that already exists and is already positioned. The only new control is one upload button in
> the admin, modelled on the banner editor's.

## Status

See `STATUS.md`. `SPEC_READY`.

## Goal

Make a character / club / item / ball created in the admin **render on an already-installed
build, with no store release**.

`content_two_way` §4 made the in-between state SAFE — a row whose art this build does not
bundle is withheld from every visible list instead of drawing a blank. It did not make it
SHIP. Today the sequence is still: create the row in the admin → drop PNGs into
`Resources/` → build → submit → wait for review. This task removes the last three steps for
2D art by letting a row carry its art as a URL, uploaded in the admin alongside the row and
downloaded by the client into the disk cache the banner feature already runs.

**The invariant does not move.** A client that cannot render a row still never shows it
(clubs still show `Placeholder`). What changes is only how many rows a given build *can*
render, and the answer stops depending on when that build was cut.

---

## 1. What is true today (verified 2026-08-27)

| Piece | State |
|---|---|
| `TournamentArtService` (`Assets/Scripts/TournamentsRuntime/TournamentArtService.cs`) | Already parameterised: `private TournamentArtService(tag, cacheDirName, isAllowed)`. Two instances exist — `Instance` (tournament art) and `Banners` (`game_banners`). 50 MB LRU per cache dir, 1 MB per-image download ceiling refused on `Content-Length` **before** buffering, `redirectLimit = 0`, atomic cache writes, in-flight coalescing, per-session failure memory. |
| `BannerPolicy` (`Assets/Scripts/BannersRuntime/BannerPolicy.cs`) | The security surface: one `AllowedArtPrefix` const (scheme + host + path), checked via the shared `TournamentArtPolicy.IsAllowedUnder(url, root)` which refuses non-https, wrong host, userinfo, non-default port, the bucket root itself, and `..` / `%2e` surviving normalization. |
| Admin upload (`Tools/admin-dashboard/lib/bannerMutations.ts` `uploadBannerArt`) | Validates MIME + size, content-hashes the bytes, uploads to a public Supabase bucket under `{placement}-{locale}-{hash}.{ext}`, creates the bucket on first use, re-validates the returned URL against the client's own allowlist, writes an audit row. **Immutable naming: the URL IS the cache key.** |
| `renderable` (`content_two_way` §4) | `CharacterDataRuntime` / `ItemDataRuntime` / `BallDataRuntime`; set at load from the PRIMARY bundled sprite; `GetAvailable…()` = `isActive && renderable`; `GetAll…()` untouched. |
| `ContentSpriteGuard` | Vetoes an OVERLAY row whose changed sprite NAMES do not resolve — appended row dropped, patched row reverted to bundled. |
| Clubs | `Placeholder` policy, decision of record. `ClubDatabaseCSV.LoadSprite` never returns null. |
| Sprite consumers | ~20 binding sites (`CharacterThumbnailCard`, `CharacterDetailPanel`, `GeneralShopCard`, `BagClubCard`, `BallThumbnailCard`, `ItemThumbnailCard`, `RankingsCardWidget`, `Top3CardWidget`, `MatchmakingModalController`, …). **All of them read a `Sprite` field off the runtime row.** None of them knows a network exists. |
| CSVs | No URL columns. `export_content.py` WARNS and does not export a `content_rows` column the repo header lacks — so the header has to gain the columns first (I4, additive-only). |

---

## 2. The shape — resolve at the LOADER, not at twenty call sites

The obvious implementation is to make every card request its art asynchronously. **Do not do
that.** It is ~20 edits to screens that currently cannot fail, it introduces a
half-drawn-card state at each one, and it is precisely the class of change that produced the
blank-card bug `shop_stocking` §6 exists to prevent.

Instead: the loaders already turn a NAME into a `Sprite` and hand every consumer the same
runtime object. Add one more source in front of the name, in that same place.

**Resolution order, per sprite column:**

```
1. remote URL, ALREADY IN THE DISK CACHE   →  use it
2. bundled sprite by name                  →  use it            (today's behaviour)
3. clubs only: Placeholder                 →  use it            (decision of record)
4. otherwise                               →  null ⇒ renderable=false ⇒ withheld (§4)
```

Every consumer is untouched. A row is renderable exactly when step 1, 2 or 3 produced
something.

### 2.1 Cache-backed, deliberately — and what that costs

Step 1 says **already in the cache**, not "download it now". A row whose art has never been
fetched is therefore withheld on the launch that first sees it, and renderable on the next
one, because the boot prefetch (§4) will have pulled it meanwhile.

That one-relaunch delay is a deliberate trade and it is the whole reason this design is
safe: the §4 invariant is *never show a row this build cannot draw*, not *show it as soon as
possible*. Admitting a row on the strength of a URL that has not downloaded yet would put a
card on screen and then discover it cannot be filled — which is the blank card again, just
with extra steps.

**A relaunch is not a store release.** The goal in the § Goal section is met.

Live in-session arrival (patch the sprite in place, raise an event, let an open screen
re-bind) is **out of scope** — see §8. It is a strict improvement on top of this and it is
not needed for the goal.

---

## 3. Data — four new optional columns, additive

Per catalog, mirroring the existing sprite-name columns exactly:

| Catalog | Existing name columns | New URL columns |
|---|---|---|
| `characters` | `portraitSprite`, `portraitFull` | `portraitUrl`, `fullUrl` |
| `clubs` | `portraitSprite`, `portraitFull`, `controlSprite` | `portraitUrl`, `fullUrl`, `controlUrl` |
| `items` | `thumbnailSprite`, `fullSprite` | `thumbnailUrl`, `fullUrl` |
| `balls` | `thumbnailSprite`, `fullSprite` | `thumbnailUrl`, `fullUrl` |

Rules:

1. **Add the column to the repo CSV HEADER first** (I4: additive-only, client parses by
   name). `export_content.py` warns and DROPS a `content_rows` column the header lacks, so
   skipping this means the URL never round-trips and the failure is a warning nobody reads.
   Empty for every existing row; the exporter's byte-for-byte rule keeps the diff to the
   header line plus one trailing comma per row.
2. **Empty means "no remote art"**, not "broken". Step 2 of the ladder handles it.
3. `min_build` semantics are UNCHANGED and, for once, self-correcting: a build older than
   this task ignores the unknown column, finds no bundled sprite, and withholds the row via
   §4. **Nothing has to be done to make old builds safe** — they already are, and this is
   worth an explicit test (§7).

---

## 4. Client

### 4.0 Assembly boundaries — settle this BEFORE writing a line

Verified 2026-08-27, because the obvious placement does not compile:

| Type | Assembly |
|---|---|
| `TournamentArtService`, `TournamentArtPolicy`, `BannerPolicy` | **Assembly-CSharp** (`Assets/Scripts/TournamentsRuntime/` and `BannersRuntime/` have no runtime asmdef — only a `*.Tests` one) |
| `CharacterDatabaseCSV`, `ItemDatabaseCSV`, `BallDatabaseCSV`, `ClubDatabaseCSV` | **Assembly-CSharp** |
| `ContentSpriteGuard`, `ContentCatalogStore`, `ContentFields` | **`Golfin.Content`** (asmdef; references `Golfin.Net`, `Golfin.Localization`, `Golfin.Save`) |

Two consequences, and they shape §4.1–§4.4:

1. **`Golfin.Content` cannot see Assembly-CSharp.** An asmdef assembly cannot reference
   Assembly-CSharp at all — the dependency only runs the other way (Assembly-CSharp
   auto-references `Golfin.Content`, which is `autoReferenced: true`). So
   `ContentSpriteGuard` **cannot** call `TournamentArtService`, and any design that has it do
   so is dead on arrival.
2. **`TournamentArtPolicy.IsAllowedUnder` is `internal`** — internal to Assembly-CSharp. Only
   a type in Assembly-CSharp can reuse it, which is exactly why `BannerPolicy` lives there and
   says so in its header.

**Therefore: the new policy and the cache lookup live in Assembly-CSharp, beside
`BannerPolicy`. The loaders are already there, so they can call both directly.**
`ContentSpriteGuard` learns nothing new about networking — see §4.4 for how it is told what it
needs instead. **Do not add an asmdef reference to make a tidier arrangement compile;** if this
seems to require one, stop and surface it.

### 4.1 `CatalogArtPolicy` — new, mirrors `BannerPolicy`

New file `Assets/Scripts/ContentRuntime/CatalogArtPolicy.cs` — **but in namespace
`Golfin.Content` while compiling into Assembly-CSharp**, which the folder does NOT give you:
`Assets/Scripts/ContentRuntime/` is inside the `Golfin.Content` asmdef's folder, so a file put
there joins that assembly and will not compile against `TournamentArtPolicy`.

Put it at `Assets/Scripts/BannersRuntime/`-level instead — e.g.
`Assets/Scripts/CatalogArt/CatalogArtPolicy.cs` — and mirror `BannerPolicy`'s header comment
explaining the placement, because this is the second time the reason has had to be rediscovered.

- `AllowedArtPrefix` = the public URL root of a NEW `catalog-art` bucket. One const, scheme +
  host + path, exactly like `BannerPolicy.AllowedArtPrefix`.
- `CacheDirName` = `"catalog-art"` — **a third directory**, so its 50 MB LRU cannot evict
  tournament or banner art and vice versa.
- `IsArtAllowed(url)` delegates to `TournamentArtPolicy.IsAllowedUnder(url, root)`. **Do not
  copy that check.** Read its doc comment for why a raw `StartsWith` is exploitable.

⚠️ These columns are free text on a row the client fetches UNATTENDED at boot. The allowlist
is the control, not a usability guard — the dashboard's equivalent check is the usability
guard. State that in the file header the way `BannerPolicy` does.

### 4.2 A third `TournamentArtService` instance

Added to `TournamentArtService` itself, beside `Instance` and `Banners` — the same placement,
for the same reason the `Banners` property is there:

```csharp
public static TournamentArtService CatalogArt { get; } = new TournamentArtService(
    "[CatalogArt]", CatalogArtPolicy.CacheDirName, CatalogArtPolicy.IsArtAllowed);
```

One property. Do not fork the class — the download path carries load-bearing security
behaviour (`Content-Length` refusal before buffering, `redirectLimit = 0`, atomic writes) and
a fork drifts. Both the service and the policy are in Assembly-CSharp (§4.0), so this compiles
with no asmdef change.

### 4.3 Loader changes — one helper, four call sites

Add the cache lookup beside the policy, in Assembly-CSharp (§4.0 — **not** on
`ContentSpriteGuard`, which cannot reach the service):

```csharp
// Assets/Scripts/CatalogArt/CatalogArt.cs
/// Remote sprite for this URL if it is ALREADY decoded/cached, else null. Never downloads.
public static Sprite? Cached(string? url);
```

Then in each of `CharacterDatabaseCSV.ParseCharacterFromCSV`, `ItemDatabaseCSV.ParseRow`,
`BallDatabaseCSV.ParseRow`, `ClubDatabaseCSV.ToRuntime` — all four already in Assembly-CSharp
— resolve each sprite as `CatalogArt.Cached(url) ?? <today's bundled lookup>`. Keep the URL
string on the runtime object next to the name (`portraitUrlValue` etc.); the prefetch needs it.

`renderable` needs NO new rule: it already asks "is the primary sprite non-null", and the
ladder now has one more way to say yes.

### 4.4 `ContentSpriteGuard` overlay veto — a URL satisfies it

Today an appended overlay row whose sprite NAMES do not resolve is dropped. That is now
wrong: an admin-created row is *expected* to have no bundled name and a URL instead. A
changed/appended sprite reference is satisfied when **either** the name resolves **or** the
row carries an allowlisted URL for that column. A row with neither is still dropped.

**How, given §4.0.** `ContentSpriteGuard` is in `Golfin.Content` and cannot evaluate the
allowlist itself. Do not move it and do not add a reference. Instead extend the `SpriteRef`
struct it already takes with one field the CALLER fills:

```csharp
public readonly bool HasRemote;   // caller: CatalogArtPolicy.IsArtAllowed(row's url for THIS column)
```

The loaders are in Assembly-CSharp, so they can evaluate the policy; the guard just honours the
flag (`if (r.HasRemote) continue;` before the name check) and learns nothing about networks.
This keeps the guard a pure decision function, which is what makes it unit-testable today.

### 4.5 Boot prefetch + sweep

After the catalogs load, hand every non-empty, allowlisted URL to
`TournamentArtService.CatalogArt.Prefetch(IEnumerable<string?>)` — the overload already
exists. One call, fire-and-forget, no await on the boot path.

Sweep: `SweepCacheAsync` takes `IReadOnlyDictionary<string, DateTime> urlEndUtc` and drops
art whose entry ended more than `EndedRetention` ago. Catalog rows have no end date, so pass
an EMPTY dictionary — the 50 MB LRU is then the only bound, which is the correct behaviour
for art that is live until deactivated. **Say this in a comment**; an empty dictionary looks
like a bug otherwise.

---

## 5. Admin

### 5.1 Upload

Reuse `uploadBannerArt`'s shape in a new `lib/contentArtMutations.ts`:
`uploadCatalogArt(adminEmail, catalog, rowId, column, file)`.

- Same validation: MIME in {jpg, png, webp}, `maxBytes`, non-empty.
- Same immutable naming: `{catalog}-{rowId}-{column}-{sha256[:12]}.{ext}` — **the URL is the
  cache key**, so replacing art mints a new URL and the client needs no invalidation story.
- Same bucket-create-on-first-use, same re-validation of the returned public URL against the
  client's allowlist, same audit row (`content_art_upload`).
- New bucket `catalog-art`, public, `fileSizeLimit` = the cap.
- Route: `app/api/content/art/route.ts`, modelled on `app/api/banners/art/`.

**Size cap: 500 KB**, same as banners, and for the same reason — every mobile player
downloads it. Note in the hint that character full-body art is 537×900 today and a 500 KB
WebP at that size is comfortable. The client's own 1 MB ceiling is the backstop, not the
budget.

### 5.2 Editor control

In `RowEditor`, for the columns in §3, render an upload button beside the existing text
field (the field stays editable — pasting a URL must remain possible). On success, `set()`
the column to the returned URL. Reuse the banner editor's control.

Update the `content_two_way` §6 hint text: a sprite NAME still means "must be bundled in the
build"; a URL now means "ships to any build that has this feature". Both EN and JA.

---

## 6. What this does NOT change

- **`renderable` and the withholding rail.** `content_two_way` §4 stands exactly as written;
  this only adds a source to the ladder in front of it.
- **Clubs keep `Placeholder`.** Decision of record. Order is URL → bundled → `Placeholder`.
- **`min_build`.** No new semantics (§3 rule 3).
- **Publish.** Art upload writes Storage, not `content_rows`; the URL reaches players only
  when the row is published, like every other column.
- **The bundled floor.** Bundled sprites stay. This is a supplement, never a replacement —
  an offline first launch must still render the roster it shipped with.
- **Any of the ~20 sprite consumers.** If a diff touches one, the design in §2 was abandoned;
  say so and escalate rather than doing it quietly.

## 7. Acceptance

- [ ] **The headline.** Create a character in the admin with NO bundled sprite name, upload a
      portrait, publish. On a build that already exists (do NOT rebuild): first launch withholds
      it (§2.1) and the prefetch pulls the art; **second launch shows it in the Roster.** No
      store release, no new build. *(Editor is sufficient — Cesar's standing rule.)*
- [ ] The same character on a build predating this task: withheld, silently, no error, no blank
      card. (Run the previous archive, or a build with the URL columns stripped from the CSV.)
- [ ] A row with a bundled name AND a URL uses the URL; delete the URL, republish → it falls back
      to the bundled sprite with no relaunch beyond the usual one.
- [ ] A row with neither is withheld, and the §4 summary warning names it.
- [ ] Clubs: a club with no bundled art and no URL still renders `Placeholder` in the bag.
- [ ] **Security.** A URL outside the `catalog-art` bucket is REFUSED and logged: try
      `http://` (wrong scheme), another host, another bucket, `…/catalog-art/../game-banners/x`,
      and the bucket root itself. EditMode tests over `CatalogArtPolicy.IsArtAllowed`, mirroring
      the existing `BannerPolicy` tests — find them and follow their shape.
- [ ] The three caches are independent: `catalog-art` filling to 50 MB evicts nothing from
      `tournament-art` or `game-banners`. Assert on `CacheDir` being three distinct paths plus a
      sweep test.
- [ ] An oversized image (> 1 MB) is refused on `Content-Length` **before** it is buffered.
- [ ] `export_content.py` round-trips the new columns: add one, publish, export, `--check`
      clean, and `python3 -m unittest discover Tools/content/tests` still green.
- [ ] Full unfiltered EditMode sweep green; dashboard `npm run build` green.
- [ ] Boot cost measured: the prefetch must not extend the boot path. Report the delta.

## 8. Out of scope

- **Live in-session arrival** (patch the sprite in place + an `OnCatalogArtArrived` event so an
  open Roster re-binds without a relaunch). A strict improvement on §2.1, not needed for the
  goal, and it reintroduces the mid-bind states §2 exists to avoid. File it separately if the
  one-relaunch delay ever actually bites.
- **3D / hole content.** That is `CONTENT_PIPELINE_PLAN.md` §10.3 and its trigger is the second
  course, not this.
- **Addressables.** §10.1 and §10.2 both say no, for different reasons. Do not add the package.
- **Retiring bundled art.** Tempting (§10.2 measures `Assets/Resources/Clubs` at 122 MB of
  source PNG, ~50 MB in-build, trending to ~150 MB) but it is a separate decision with an
  offline-first-launch consequence, and it must not ride along with the mechanism that makes it
  possible.
- **Localised art per row.** Banners have it; catalogs have no such requirement today.
