DONE

Approved by Cesar 2026-09-01. Folder moved Active -> Completed.

## What shipped

Balls went from 2 rows to 20, end to end: `Balls.csv` + `rarity` column, 18 `BALL_INFO_*` strings
EN+JA, `BallDataRuntime.rarity` via the existing `ClubCsvParser.ParseRarity`, `BallWindCutPerPoint`
0.01 -> 0.02 with `Physics/stats.csv` and the never-called `LoadStatCoefficients()` retired, a
dedicated `/balls` admin panel with brand+rarity facets, and the importer round trip
(balls v6 -> v7 -> v8, texts v20 -> v21, `--check` clean).

Three defects were found and fixed that the spec did not anticipate:

1. **The 18 fulls + 2 thumbnails had imported as DEFAULT textures, not Sprites.**
   `Resources.Load<Sprite>` returns null for those, so all 20 new balls would have shipped
   withheld as non-renderable. Import settings copied off `Golfin.png`.
2. **SPEC §7 was a real defect, but not the predicted one.** Layout does not move (measured
   identical with a 200px and a 1000px sprite); the 1000x1000 thumbnails were a 5.95x downscale
   with no mip chain, aliasing on the Balls card, the shot UI centre ball AND the ball button.
   Fixed per §7 with 200x200 LANCZOS copies; worst-case downscale now 1.19x. 68 MB -> 2 MB of VRAM.
   The 19 `S_Controls_Ball_*` files this orphaned were then deleted from `Resources/` (they were
   byte-identical duplicates of `Art/Original UI/Ball Sprites/`).
3. **A blank `rarity` passed validation.** SPEC §5 assumed adding `rarity` to REQUIRED was enough;
   it was not (REQUIRED only checks the key exists, and the rarity rule exempted blanks). Closed —
   and the first cut of that fix was itself wrong (it named `shop_catalog` directly and broke
   `mission_loadouts`), corrected later the same day to key off REQUIRED.

## NOT verified — stated plainly rather than left implied

Two SPEC §10 items were never run, because this Editor was shared with two other live sessions all
day and never gave an uncontended window:

  1. Play-mode Balls carousel + detail panel EN/JA. NOTE: "carousel shows 20 entries" is not
     reachable as specified anyway — `BallCarouselController` builds from `GetAllOwnedBallIds()`,
     so it is an inventory view, not a catalog view; it needs the balls GRANTED first.
  2. A `Golfin.Gameplay.Tests` assembly run. The only change there is a DELETED test method
     (`T8_NeutralizationParity_StatsCSV_FloorFractionIs1`, which read the deleted CSV); the file
     compiles and no other test in the assembly was touched.

What WAS verified: 459 pass / 0 fail / 3 pre-existing skips across `Golfin.Physics.Tests` +
`Golfin.Inventory.Tests`; 20/20 balls resolve both sprites; the wind perceptibility table;
246 admin tests + clean typecheck; importer plan/apply/publish/`--check`.

## Spun out of this task

- `Docs/Specs/Quick/broken_sprite_refs.md` — 86 broken `Image.sprite` refs, fixed (0 remain).
- `Docs/Specs/Quick/club_full_art_repoint.md` — 205 club `portraitFull` values repointed.
- `Docs/Specs/Queued/publish_blocked_catalogs/ARCHITECT_BRIEF.md` — `mission_loadouts` and
  `gacha_pools` cannot be published; diagnosed, handed to the Architect. Cesar: being taken care
  of in another session (that session's `lib/loadoutTokens.ts` work was already in the tree at
  close-out and is deliberately untouched here).
