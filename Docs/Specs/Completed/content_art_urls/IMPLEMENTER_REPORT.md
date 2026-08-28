# Implementer Report — `content_art_urls` (Option A seam fix)

Written by the orchestrating main thread, 2026-08-28, implementing `ARCHITECT_DECISION.md` §1.
Supersedes the iter-6 report for the §2 defect only; everything else in that report still stands
and was not re-litigated.

## Implementation summary

The feature was complete and wrong: the resolution ladder ran synchronously in each loader's
`Awake`, but the only thing it could consult was `TournamentArtService._sprites` — a per-session
dictionary that is empty at that moment — while the disk cache was read exclusively inside the
async `LoadRoutine`. Art downloaded on one launch was therefore never read on any later launch,
and catalog art rendered on no launch at all.

Fixed at the seam, per Option A: one new synchronous entry point that consults the on-disk cache
and decodes through the EXISTING bytes→sprite path, called only from `CatalogArtCache`. No
consumer was touched; banners and tournament art keep their fully async behaviour.

## Files modified or created

| Path | Change |
|---|---|
| `Assets/Scripts/TournamentsRuntime/TournamentArtService.cs` | New `TryGetOrLoadCached(url, out sprite, out bytesRead)`: dict hit → done, else the same cache path `LoadRoutine` derives, `File.Exists` → `File.ReadAllBytes` → the existing `Decode(url, bytes)`. Stamps last-access for the LRU, honours `_failed`, and reports bytes read so the caller can measure. |
| `Assets/Scripts/CatalogArt/CatalogArt.cs` | `Cached()` (both overloads) now route through a shared `Resolve()`: free uncapped dict hit first, then the disk under a 24-decode session cap with one loud over-cap warning naming the first offending row; accumulates a Stopwatch and logs the boot delta once, one frame after the first decode. |
| `Assets/Scripts/TournamentsRuntime/Tests/CatalogArtPolicyTests.cs` | New `CatalogArtDiskCacheTests` (2 tests) that write real PNG bytes into the real cache dir with `_sprites` empty; helper gains `CacheDir`, `CacheFileName`, `ResetCacheCounters`. |
| `Tools/admin-dashboard/lib/contentArtMutations.ts` | (earlier today) WebP removed from the MIME allowlist and the minted extension — SPEC §5.1. |
| `Tools/admin-dashboard/app/(panels)/_content/row-editor.tsx` | (earlier today) file input `accept` narrowed to JPG/PNG. |

## Screenshot

- **Canonical screenshot:** `screenshots/2026-08-28_url_art_renders.png` — 1170×2532, play mode,
  reached through the real entry path (`PlayButton.onClick.Invoke()` → title gate, then
  `NavCharactersButton.onClick.Invoke()`).
- **What it shows:** twelve cards, and **Olivia's card rendering Camila's portrait** — the
  dark-haired POWER-cap art, identical to the Camila card at position 6. Olivia's own
  `portraitSprite` column is blank in the fixture, so that art can only have come from the URL.

## The boot delta — SPEC §7, owed since iteration 1

```
[CatalogArt] Boot art decode: 1 file(s), 3.1 ms, 0.08 MB read from the on-disk cache
             (cap 24/session). This is the delta this feature adds to the synchronous boot path.
```

Read from `Editor.log:1207790` and cross-checked directly off the counters. 3.1 ms for one
170×343 PNG. The cap of 24 bounds the worst case; full-body art is larger per file, so the
honest extrapolation is "tens of ms, not hundreds", and the number should be re-measured if the
cap is ever raised.

## End-to-end, on the kept fixture

Fixture: `characters-char_olivia-portraitUrl-6415197b252e.png` in the live `catalog-art` bucket
(Camila's portrait, uploaded under the same immutable content-hashed name the admin path mints).
`char_olivia.portraitSprite` blanked so the URL is the row's ONLY portrait source.

| | Launch 1 (cache cleared) | Launch 2 (warm) |
|---|---|---|
| `GetAllCharacters` | 12 | 12 |
| `GetAvailableCharacters` | **11** | **12** |
| `oliviaAvailable` | False | **True** |
| `renderable` | False | **True** |
| `portraitSprite` | NULL | **170×343 texture** |
| cache files | 1 (downloaded) | 1 |

**Sprite identity, not inferred:** sampling 200 pixels of the rendered texture against both
candidate PNGs — `matchesCamila = 200/200`, `matchesOlivia = 32/200` (incidental background).
The rendered art is the uploaded file. `portraitSpriteName` was `''` throughout, so there was no
bundled art to fall back to in the first place.

`Assets/Data/Characters.csv` restored byte-identical afterwards (`git diff` empty).

## Tripwire demonstration — PIPELINE_HARDENING §20

The disk test was proven capable of failing before being trusted.

Regression applied to `TournamentArtService.TryGetOrLoadCached` (the pre-fix behaviour):

```diff
160a161,162
> 
> +            sprite = null; return false;   // TRIPWIRE: disk read removed (the pre-fix behaviour)
```

| Run | Result |
|---|---|
| Disk read removed | **1877 / 1872 / 2 FAILED** — `CatalogArtDiskCacheTests.Step1_…` and `Step3_…` |
| Reverted (byte-identical to backup) | **1877 / 1874 / 0 failed** |

## Acceptance checklist

| Item | Result | Justification |
|---|---|---|
| One synchronous entry point, reusing `Decode` | PASS | `TryGetOrLoadCached` calls the existing `Decode(url, bytes)`; no second bytes→sprite path exists. Only `CatalogArtCache.Resolve` calls it — `grep` shows no other call site. |
| Banners / tournament art untouched | PASS | `Instance` and `Banners` never call the new method; their `TryGet`/`Request` behaviour is unchanged. |
| Cap of 24, first-come, one loud warning | PASS | `MaxSyncDecodesPerSession = 24`; over-cap path logs once naming the count and the first over-cap row leaf, returns null so the row stays on the async prefetch. Dict hits are exempt so shared club art cannot burn the budget. |
| Stopwatch reported | PASS | Line quoted above, from `Editor.log`. |
| Disk-path test, `_sprites` empty | PASS | `CatalogArtDiskCacheTests` writes a real encoded PNG into `CacheDir` and asserts both step 1 and step 3 resolve. |
| Tripwire demonstrated | PASS | Table + diff above; observed red, then byte-identical revert. |
| E2E re-run on the kept fixture | PASS | Table above, with pixel-level sprite identity. |
| Full EditMode sweep | PASS | **1877 / 1874 passed / 0 failed / 3 pre-existing skips.** |
| `Tools/content` tests | PASS | 26 tests, OK. |
| Dashboard `npm run build` | PASS | Compiled successfully. |
| No consumer edits (SPEC §2) | PASS | Diff touches two runtime files, one test file, and two dashboard files. No sprite consumer in the diff. |

## Spec deviations

- **`ScheduleSummary` is guarded with `Application.isPlaying` and a try/catch.** Not in the
  decision. Reaching for the coroutine host outside play mode made the first EditMode disk test
  fail — diagnostics were able to break resolution, which is unacceptable regardless of the
  decision's wording. Counters still accumulate; only the deferred log is skipped.
- The decision's `TryGetOrLoadCached(url, out sprite)` gained a third `out int bytesRead`
  parameter, so the "decoded MB" half of §1.3 could be reported honestly rather than estimated.

## Open questions for the Architect

- **The cap's real ceiling is unmeasured.** 3.1 ms is one thumbnail; a 537×900 full-body decodes
  slower and costs ~1.9 MB resident. If the cap is ever raised, that number needs re-taking with
  full-body art, not extrapolating from this one.
