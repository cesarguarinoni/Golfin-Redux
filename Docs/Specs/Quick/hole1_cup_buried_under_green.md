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

## Fix — DONE 2026-08-10

> **⚠️ The original instruction in this spec said "then re-import Hole 1". DO NOT DO THAT.**
> Cesar caught it: `HoleGeoImporter` builds each hole with
> `EditorSceneManager.NewScene(EmptyScene, Single)` and overwrites `Hole_NN_Geo.unity` wholesale
> (`HoleGeoImporter.cs:286` / `:481`), so a re-import **destroys everything authored after the
> import** — above all the **trees**, which the importer deliberately does not place ("Trees are
> placed separately via Trees > Import Trees (Current Hole)", `HoleGeoImporter.cs:468`; Hole 1
> carries **1362** terrain tree instances). It also regenerates the baked sim data the multiplayer
> bots read (`zones.json`, `heightmap.bytes`, `tree_obstacles.csv`), reverting any later re-bake.
> A shipped hole must be repaired **surgically**, never re-imported.

Two halves, both landed:

1. **Existing shipped scenes — surgical, no re-import.** New editor tool
   `Assets/Scripts/Editor/CourseImporter/CupReseatTool.cs`:
   - `GOLFIN > Course > Cups > Measure Cup Seating` — read-only report, mutates nothing.
   - `GOLFIN > Course > Cups > Reseat Buried Cups` — raises only buried cups, saves only changed scenes.

   It measures each cup's top against the **green's MeshCollider** at the cup XZ and re-seats to
   `CupSurfaceClearanceM` above the *measured* surface. Safe because the cup disc is cosmetic
   only — `HoleGeoImporter` destroys its collider at creation, so it carries just Transform +
   MeshFilter + MeshRenderer and drives no physics (cup capture is `CupSpec` / `RealCupDetector`,
   keyed off the pin position).

   Applied to Hole 1: `-23.12 mm → +6.00 mm`. The saved-scene diff was **36 lines**: the cup's
   `m_LocalPosition.y` (10.178116 → 10.207232, XZ untouched) plus a default
   `UniversalAdditionalLightData` that URP auto-adds to the Directional Light on any save
   (`m_UsePipelineSettings: 1`, behaviourally identical to absent). Nothing else moved; trees,
   terrain, greens and baked data untouched.

2. **Future imports.** `HoleGeoImporter` now seats the cup on the **measured mesh surface** at the
   pin XZ (raycast against the green's `MeshCollider`, which exists by then — the mesh is built at
   `:2742`, the cup at `:2840`), falling back to the old analytic `pinSeatY` if the ray misses. It
   also logs the requested warning when the legacy datum *would* have buried the cup, naming the
   millimetres — so a future bad bake announces itself at import time instead of hiding until
   someone films it. Shared constant `HoleGeoImporter.CupSurfaceClearanceM = 6 mm`, referenced by
   `CupReseatTool`, so repaired and freshly-imported holes cannot drift apart.

**Why 6 mm and not the 10–20 mm this spec originally proposed:** the 17 holes that already render
correctly sit between **1.3 mm and 6.4 mm** proud. 6 mm is at the top of the range already
accepted visually, while being 6x the 1 mm margin the Hole 1 bake ate. The load-bearing change is
the **datum** (measured mesh, not analytic seat plane), not the size of the margin — with the
right datum there is no accumulating error for a large margin to absorb.

## Verification

1. **Geometric (the gate).** `Measure Cup Seating` on a fresh load from disk: Hole 1 reports
   `cupTop=10.20823 greenSurface=10.20223 clearance=+6.00 mm ok`. Trees confirmed intact in the
   same pass (`terrainData.treeInstanceCount = 1362`).
2. **Visual (real flow).** `GOLFIN/Smoke/Loop v2/Hole 1 Playthrough` (ShellScene → PLAY → Hole 1),
   green-side frame at 7 m. Before/after A/B at the same crop and camera framing: the flagstick
   went from tapering into unbroken turf to having a distinct dark disc at its base.
   Before: `Docs/Specs/Active/auto_club_selection/screenshots/green_turn6_putter_mode_button_gap.png`.
   After:  `tasks/loop_v2_smoke_bot/hole1_playthrough/screenshots/s08_stroke5_2026-08-10_15-40-31.png`.
3. **Still outstanding:** the other 17 holes have NOT been re-measured with this tool (they were
   measured once when this bug was filed, and all read visible). Run `Measure Cup Seating` with
   them open to confirm, and re-run it after any green re-bake.
4. The menu entry `GOLFIN/Smoke/Loop v2/Putter Aim Blue Line — clip (Hole 1, cup is buried)` still
   carries the stale "cup is buried" label — worth renaming now that it isn't.
