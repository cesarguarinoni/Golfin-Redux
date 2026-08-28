# ARCHITECT DECISION — `content_art_urls` §2 (answers SITREP 2026-08-28)

Architect (Cowork), 2026-08-28. Root cause verified in the code before deciding, not from the
sitrep: `TournamentArtService.TryGet` (:119) reads the in-memory `_sprites` dict only; the disk
read (`File.Exists`/`ReadAllBytes`, :203–205) lives inside the async `LoadRoutine`; `Prefetch`
passes no callback. The ladder's rung 1 can never hit at `Awake`. Confirmed.

## 1. Decision: OPTION A — synchronous disk read in `Cached()`, capped and measured

A restores the behaviour the spec actually promised ("first launch withholds, second launch
shows"), stays inside `CatalogArtCache` / `TournamentArtService`, and keeps §2's
zero-consumer-edits constraint intact. B is the better product but is a behavioural spec
revision (mid-session appearance vs I5's launch-boundary rule, `GeneralShopCatalog`'s one-shot
load, `CharacterManager` seeding) — file it separately if the one-relaunch delay ever bites.

Constraints on A, all binding:

1. **One new synchronous entry point** on `TournamentArtService`, e.g.
   `TryGetOrLoadCached(url, out sprite)`: dict hit → done; else derive the SAME cache path the
   routine uses, and on `File.Exists` decode via the EXISTING `Decode(url, bytes)` (:332) so the
   bytes→sprite path stays single. Atomic `File.Replace` writes mean a file is whole or absent —
   no torn-read handling needed. Only `CatalogArtCache.Cached` calls it; banners and tournament
   art keep their async behaviour untouched.
2. **Cap: at most 24 synchronous decodes per session** (`const`, named). Beyond it, log ONE loud
   warning naming the count and the first over-cap row; the remainder stay on the async prefetch
   and are withheld this launch, exactly as a cold cache is. First-come in loader order —
   deterministic. 24 is a starting number, not a law: it bounds worst-case decode work AND the
   uncompressed-RGBA memory (~1.9 MB per 537×900 full-body ⇒ ~45 MB absolute worst case, far
   less for thumbnails); revisit it against the measured numbers, and remember the set is
   self-draining by design — `content_art_bundling` exists to empty it every release.
3. **SPEC §7's boot-delta finally gets its number.** `CatalogArtCache` accumulates a Stopwatch
   across sync decodes and logs once after the loaders finish: files, ms, decoded MB. The
   acceptance re-run reports it. This number is now what stands between the feature and the
   boot path; it does not get to stay unmeasured a second time.
4. **The test goes through the DISK.** New EditMode test: write a real PNG into the cache dir,
   `_sprites` empty, `Cached(url)` returns a sprite. Then tripwire-demonstrate it (break the
   disk read, show the sweep go red, revert) — per the new PIPELINE_HARDENING rules 20/21.
5. **Re-run the E2E on the kept fixture** (`characters-char_olivia-portraitUrl-…png` stays, as
   the sitrep recommends): launch 1 withheld + downloaded, launch 2 Olivia's card renders
   Camila's face, `renderable = true`, `GetAvailableCharacters` = 12. Log the sprite identity.

`STATUS.md` → `ARCHITECT_REVIEW_FAIL` (set by the Architect with this file; the earlier PASS is
withdrawn — the red-team verdict was right to stand until this decision existed).

## 2. `content_two_way` — advance to DONE (recommended; Cesar's word is the gate)

Its evidence is of the kind that failed here five times — except it isn't: the §9 items that
matter were run against the product (play-mode measurements: 799/150/0 club placeholders,
roster skip observed) and against prod (the HOME_CURRENCY_LABEL round-trip, both legs published
by Cesar, export byte-identical). The Architect review also read the code, not the report. No
red-team re-run needed. Recommend DONE on Cesar's word.

## 3. Pipeline rules — adopted

`PIPELINE_HARDENING.md` gains §20 (tripwire demonstration for regression-guard tests) and §21
(one named E2E against the live dependency before ARCHITECT_REVIEW_PASS; deferral only with a
verbatim quoted spec line that is verified to exist). The iter-1 "SPEC §0" fabrication is the
motivating incident for §21's quote rule.

## 4. Knock-on

`content_art_bundling` premise holds unchanged under A. No spec edit needed there.
