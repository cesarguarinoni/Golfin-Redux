# QUICK — `hole1_cup_buried_under_green`

**Filed:** 2026-08-10 · **Found during:** `putter_aim_blue_line` video review (Cesar spotted it)
**Priority:** high — Hole 1 is the first hole every new player sees.

## Symptom

On Hole 1 the black cup disc never renders. The flagstick appears to be planted in unbroken
turf, with no hole at its base. Evidence:
`Docs/Specs/Completed/putter_aim_blue_line/screenshots/hole1_cup_MISSING_2x.png`
(2× crop, aim line AND green grid both off, so nothing is drawing over it) versus
`.../hole6_cup_visible_2x.png` (same crop on Hole 6, disc present).

## Root cause — measured, not guessed

`Assets/Scripts/Editor/CourseImporter/HoleGeoImporter.cs:2840-2847` places the cup as a flat
cylinder at `pinSeatY + 0.001` with `localScale.y = 0.001` — i.e. a 1 mm-thick disc whose top
sits ~1.5 mm above the `pinSeatY` datum.

Raycast down at each cup's own XZ, `greenSurfaceY − cupTopY`, all 18 holes:

| Hole | green − cup top | cup |
|---|---|---|
| **1** | **+23.6 mm** | **BURIED** |
| 2, 4, 5, 6, 8, 9, 10, 13, 14, 15, 16, 17, 18 | −1.3 to −1.6 mm | visible |
| 3 | −6.4 mm | visible |
| 11 | −3.1 mm | visible |

Hole 1's green mesh sits 23.6 mm **above** the `pinSeatY` datum at the pin XZ, so the disc is
inside the turf. Every other hole clears by only 1.3–6.4 mm — the margin is ~1 mm by design and
the greens bake evidently consumed it on Hole 1 alone. **The whole course is one bad bake away
from this**; Hole 1 is not a special case, it is the first casualty.

## Fix

In `HoleGeoImporter`, seat the cup on the **actual green mesh surface** at the pin XZ (the same
surface the player putts on) rather than on `pinSeatY`, and give it a margin that survives a
re-bake — order 10–20 mm, not 1 mm. Then **re-import Hole 1**.

Do NOT hand-edit `Assets/Golf/Courses/lomond-country-club/Generated/Hole_01_Geo.unity`: Generated
scenes are build artifacts and the edit is erased on the next import.

Consider a cheap importer-side assertion that logs a warning when any placed cup ends up below
its green surface — this class of bug is invisible until someone films it.

## Verification

1. Re-run the measurement across all 18 holes (raycast down at the cup XZ; every hole must report
   `greenSurfaceY − cupTopY < 0`).
2. Visual: `GOLFIN/Smoke/Loop v2/Putter Aim Blue Line — clip (Hole 1, cup is buried)` — that menu
   entry exists precisely to reproduce this. Rename/relabel it once the fix lands.
