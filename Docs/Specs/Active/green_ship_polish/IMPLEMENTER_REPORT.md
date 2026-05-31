# Implementer Report — `green_ship_polish` iter-13 (2-tier gate amendment)

**Iteration:** iter-13 2-tier-gate — the authoritative directive for this report.
**Supersedes:** prior iter-13 amendment (drop-scaled width) report.

---

## Implementation summary

Added the `TWO_TIER_HOLES = new Set([3, 7, 11, 18])` gate to `bake-green.mjs` and updated `verify-ridge.mjs` with the side-agnostic interior Δh scan. The core change:

```js
const TWO_TIER_HOLES = new Set([3, 7, 11, 18]);  // source: A4_ホール攻略冊子.pdf 「２段グリーン」 p4/p8/p12/p19
const applyRidgeBarrier = ridgePresent && TWO_TIER_HOLES.has(holeNum);
```

For non-tier holes with a traced ridge (H06, H13, H14), `applyRidgeBarrier=false`:
- `classifyRegions` returns all-region-0 (widened trigger condition from `!ridgePresent` to `!applyRidgeBarrier`)
- All authored arrows remapped to region 0 (so the full arrow set drives IDW interpolation across the whole green)
- `ridgeSeparated` always returns false (Poisson relaxes across the entire green)
- `smoothRidgeBand` is a no-op
- The traced dashed line is ignored for geometry; arrows carry the swale

For genuine two-tier holes (H03/H07/H11/H18), behaviour is **exactly unchanged** from the drop-scaled amendment — same two-region Poisson, same drop-scaled rampWidth formula, same smoothRidgeBand pass.

`verify-ridge.mjs` was updated to:
1. Add `TWO_TIER_HOLES` constant (kept in sync with bake)
2. Add `interiorCliffScan()` function — side-agnostic, whole-green, excludes edge band (1.0m)
3. Run the interior cliff scan on ALL 18 holes (not just ridge holes)
4. Only run ridge-band perp-slope + continuity checks on genuine tier holes

### Note on H06

H06 also had a traced ridge in its authoring JSON (ridgePresent=true) that was not caught in prior iterations because it was not a named 2-tier hole. The 2-tier gate correctly treats it as single-region. H06 bake: 0 interior cliffs, single region.

### Elevation change for orbit clips

`GreenOrbitElevationDeg` lowered from 38° to 18° in `HoleFlyoverRecorder.cs` for this iteration's orbit clips to ensure the grazing angle reveals interior surface topology. Per SPEC: "If the /green-orbit default elevation (38°) is too high to read the surface, lower GreenOrbitElevationDeg … for these clips and note it."

---

## SPEC report-back items

### 1. classifyRegions single-region path for non-tier holes

Confirmed: the widened condition `!applyRidgeBarrier` (vs previous `!ridgePresent`) correctly triggers the single-region path for H06, H13, and H14 — holes that have a traced ridge but are not in `TWO_TIER_HOLES`.

**H14 region count post-fix:** 1 (single region). Bake log: `ridgePresent=false, regionCount=1, grid=53x51`. The `ridgePresent=false` in the bake report reflects that `applyRidgeBarrier=false` was passed to the QA gate — exactly as intended.

**H13 region count post-fix:** 1 (single region). Bake log: `ridgePresent=false, regionCount=1, grid=49x62`.

### 2. Per-hole interior cliff count (all 18)

Side-agnostic interior Δh scan (>5cm, >1m from contour edge) on all 18 green.json files:

| Hole | Type | Region | Interior cliffs | maxΔh |
|------|------|--------|----------------|-------|
| H01  | single | 1 | 0 | 1.8cm |
| H02  | single | 1 | 0 | 1.8cm |
| H03  | 2-tier | 2 | 0 | 2.7cm |
| H04  | single | 1 | 0 | 1.7cm |
| H05  | single | 1 | 0 | 1.4cm |
| H06  | single (fall-line) | 1 | 0 | 1.1cm |
| H07  | 2-tier | 2 | 0 | 2.1cm |
| H08  | single | 1 | 0 | 1.7cm |
| H09  | single | 1 | 0 | 1.2cm |
| H10  | single | 1 | 0 | 2.1cm |
| H11  | 2-tier | 2 | 0 | 3.4cm |
| H12  | single | 1 | 0 | 1.1cm |
| H13  | single (fall-line) | 1 | 0 | 1.2cm |
| H14  | single (fall-line) | 1 | 0 | 1.2cm |
| H15  | single | 1 | 0 | 1.5cm |
| H16  | single | 1 | 0 | 2.1cm |
| H17  | single | 1 | 0 | 0.9cm |
| H18  | 2-tier | 2 | 0 | 2.0cm |

**Result: 18/18 PASS. Zero interior cliffs on every hole.**

### 3. Holes that kept the barrier vs lost it

**Barrier retained (TWO_TIER_HOLES):** H03, H07, H11, H18 — exactly 4 holes.
**Barrier removed (non-tier with ridgePresent=true):** H06, H13, H14 — these now get single-region treatment.
**Never had a barrier:** H01, H02, H04, H05, H08, H09, H10, H12, H15, H16, H17 — flat/single-region greens.

---

## Files modified or created

| Path | Change |
|------|--------|
| `Tools/GreenSlope/scripts/bake-green.mjs` | Modified — added `TWO_TIER_HOLES`, `applyRidgeBarrier` gate; widened `classifyRegions` trigger; updated all downstream calls; added arrow remapping for non-tier holes |
| `Tools/GreenSlope/scripts/verify-ridge.mjs` | Modified — added `TWO_TIER_HOLES`, `interiorCliffScan()`, updated `verifyHole()` to run interior scan on all holes and ridge-band checks only on tier holes |
| `Assets/Scripts/Editor/Recording/HoleFlyoverRecorder.cs` | Modified — `GreenOrbitElevationDeg` 38→18° for grazing-angle orbit clips |
| `Assets/Resources/HoleData/Hole_01/green.json` | Re-baked (single region, no change from prior bake, no barrier) |
| `Assets/Resources/HoleData/Hole_02/green.json` | Re-baked (single region) |
| `Assets/Resources/HoleData/Hole_03/green.json` | Re-baked (2-tier; rampWidth=5.61m; 0 interior cliffs) |
| `Assets/Resources/HoleData/Hole_04/green.json` | Re-baked (single region) |
| `Assets/Resources/HoleData/Hole_05/green.json` | Re-baked (single region) |
| `Assets/Resources/HoleData/Hole_06/green.json` | Re-baked (previously had barrier incorrectly; now single region — fall-line; 0 interior cliffs) |
| `Assets/Resources/HoleData/Hole_07/green.json` | Re-baked (2-tier; rampWidth=8.89m; 0 interior cliffs) |
| `Assets/Resources/HoleData/Hole_08/green.json` | Re-baked (single region) |
| `Assets/Resources/HoleData/Hole_09/green.json` | Re-baked (single region) |
| `Assets/Resources/HoleData/Hole_10/green.json` | Re-baked (single region) |
| `Assets/Resources/HoleData/Hole_11/green.json` | Re-baked (2-tier; rampWidth=9.63m; 0 interior cliffs) |
| `Assets/Resources/HoleData/Hole_12/green.json` | Re-baked (single region) |
| `Assets/Resources/HoleData/Hole_13/green.json` | Re-baked (was incorrectly 2-tier; now single region; 0 interior cliffs) |
| `Assets/Resources/HoleData/Hole_14/green.json` | Re-baked (was incorrectly 2-tier with 23cm cliff; now single region; 0 interior cliffs) |
| `Assets/Resources/HoleData/Hole_15/green.json` | Re-baked (single region) |
| `Assets/Resources/HoleData/Hole_16/green.json` | Re-baked (single region) |
| `Assets/Resources/HoleData/Hole_17/green.json` | Re-baked (single region) |
| `Assets/Resources/HoleData/Hole_18/green.json` | Re-baked (2-tier; rampWidth=9.63m; 0 interior cliffs) |
| `Tools/GreenSlope/bake_report.txt` | Regenerated by --all bake run |
| `Docs/Specs/Active/green_ship_polish/screenshots/h14_2tier_gate_grazing.png` | Created — 1920×1080 scene view, H14 after 2-tier gate fix |
| `Docs/Specs/Active/green_ship_polish/screenshots/h14_2tier_gate_grazing_frame2s.png` | Created — 1920×1080 frame extract from H14 orbit at 2s (canonical screenshot) |
| `Docs/Specs/Active/green_ship_polish/videos/green_orbit_h14_2tier_h14_2tier_gate_orbit.mp4` | Created — 3.9MB captioned 360° orbit H14 (18° elevation, 60fps, 7.8s). Caption: "H14 iter-13 2-tier gate — region=1 (single) / 0 interior cliffs — swale from arrows, no cliff" |
| `Docs/Specs/Active/green_ship_polish/videos/green_orbit_h07_2tier_h07_2tier_gate_orbit.mp4` | Created — 4.3MB captioned 360° orbit H07 (18° elevation, 60fps, 7.8s). Caption: "H07 iter-13 2-tier gate — region=2 (genuine tier) / ~8% ramp-width=8.89m — 0 interior cliffs" |
| `Assets/Golf/Courses/lomond-country-club/Data/hole-07-geo/*.mat` | Pre-existing dirty (from iter-13a) — see baseline block in HEARTBEAT.log |
| `Assets/Golf/Courses/lomond-country-club/Data/hole-14-geo/*.mat` | Modified by H14 reimport this iteration |
| `Assets/Plugins/NuGet/*.dll` | Pre-existing dirty — see baseline block |
| `Packages/manifest.json` | Pre-existing dirty — see baseline block |
| `Packages/packages-lock.json` | Pre-existing dirty — see baseline block |

**Pre-existing paths from baseline block (HEAD `ee4b426c`, NOT introduced by this iteration):**

```
 M .claude/hooks/__pycache__/enforce_implementer_done.cpython-313.pyc
 M Assets/Golf/Courses/lomond-country-club/Data/hole-07-geo/BunkerSand.mat
 M Assets/Golf/Courses/lomond-country-club/Data/hole-07-geo/GreenSurface.mat
 M Assets/Golf/Courses/lomond-country-club/Data/hole-07-geo/MAT_T_Fairway_Mix.mat
 M Assets/Golf/Courses/lomond-country-club/Data/hole-07-geo/MAT_T_RoadAsphalt_Albedo.mat
 M Assets/Golf/Courses/lomond-country-club/Data/hole-07-geo/MAT_T_Semirough_Albedo.mat
 M Assets/Golf/Courses/lomond-country-club/Data/hole-07-geo/MAT_T_Tee_Albedo.mat
 M Assets/Golf/Courses/lomond-country-club/Data/hole-07-geo/MAT_TeeBorder.mat
 M Assets/Golf/Courses/lomond-country-club/Data/hole-07-geo/TerrainData_Hole07Geo.asset
 M Assets/Golf/Courses/lomond-country-club/Data/hole-07-geo/TerrainLayer_T_OB_TintedRough.asset
 M Assets/Plugins/NuGet/.nuget-installed.json
 M Assets/Plugins/NuGet/McpPlugin.Common.dll
 M Assets/Plugins/NuGet/McpPlugin.dll
 M Assets/Plugins/NuGet/ReflectorNet.dll
 M Packages/manifest.json
 M Packages/packages-lock.json
 M Assets/Scripts/Editor/Recording/HoleFlyoverRecorder.cs (iter-13 amendment — ALSO modified this iter for elevation)
 M Tools/GreenSlope/scripts/bake-green.mjs (iter-13 amendment — now modified again this iter)
?? Tools/GreenSlope/scripts/verify-ridge.mjs (created iter-13a — now modified this iter)
?? Docs/Specs/Active/green_ship_polish/screenshots/h07_ridge_iter13_*.png (iter-13a)
?? Docs/Specs/Active/green_ship_polish/videos/h07_ridge_iter13_orbit.mp4 (iter-13a)
?? Docs/Specs/Active/green_ship_polish/videos/h07_ridge_iter13amend_orbit.mp4 (amendment)
?? Docs/Specs/Active/green_ship_polish/videos/h14_ridge_iter13amend_orbit.mp4 (amendment)
```

---

## Screenshot

Canonical screenshot: `screenshots/h14_2tier_gate_grazing_frame2s.png`

- **Long edge:** 1920px (satisfies Rule 14 ≥ 900px)
- **Angle:** 18° grazing (LOW — per SPEC requirement to show interior topology)
- **Content:** H14_Geo after 2-tier gate fix — single continuous surface, swale visible, NO cliff
- **Source:** frame extract at 2s from captioned orbit video

---

## Canonical video

Canonical video: `videos/green_orbit_h14_2tier_h14_2tier_gate_orbit.mp4`

H14 green 360° orbit after 2-tier gate fix. 18° grazing elevation, 60fps, 7.8s (469 frames).
Caption: "H14 iter-13 2-tier gate — region=1 (single) / 0 interior cliffs — swale from arrows, no cliff"

**Motion gate:** r_frame_rate=60/1 PASS, 90° pixel diff=13.8 > 12 PASS.

H07 orbit also delivered (tier hole, tier intact): `videos/green_orbit_h07_2tier_h07_2tier_gate_orbit.mp4`

**Motion gate:** H07 r_frame_rate=60/1 PASS, 90° pixel diff=24.2 > 12 PASS.

---

## Acceptance checklist

| Item | Result | Justification |
|------|--------|---------------|
| `TWO_TIER_HOLES = {3,7,11,18}` added with PDF citation comment | PASS | Verified in source: `const TWO_TIER_HOLES = new Set([3, 7, 11, 18]); // source: A4_ホール攻略冊子.pdf 「２段グリーン」 p4/p8/p12/p19` |
| `applyRidgeBarrier = ridgePresent && TWO_TIER_HOLES.has(holeNumber)` | PASS | Present in bakeHole(): `const applyRidgeBarrier = ridgePresent && TWO_TIER_HOLES.has(holeNum);` |
| Two-tier holes (3/7/11/18): behaviour UNCHANGED from drop-scaled amendment | PASS | classifyRegions path = 2-region, ridgeSeparated = barrier, smoothRidgeBand = drop-scaled ramp. Verified via bake log: H07 `ridgePresent=true, regionCount=2, rampWidth=8.89m` unchanged. |
| Non-tier holes with traced ridge (H13, H14, H06): single-region treatment | PASS | Bake logs: H14 `ridgePresent=false, regionCount=1`. H13 `ridgePresent=false, regionCount=1`. H06 `ridgePresent=false, regionCount=1`. Arrow remapping confirmed: H14 8 arrows → all region 0. |
| H14 region count = 1 post-fix | PASS | verify-ridge.mjs H14: `region count: 1 (single region, confirmed via applyRidgeBarrier=false)` |
| H13 region count = 1 post-fix | PASS | verify-ridge.mjs H13: `region count: 1 (single region, confirmed via applyRidgeBarrier=false)` |
| classifyRegions widened trigger (`!applyRidgeBarrier`) | PASS | Code change verified: `if (!applyRidgeBarrier \|\| !ridge \|\| ridge.length < 2) { regions.fill(0); return regions; }` |
| H14 interior cliff scan: 0 interior cliffs | PASS | verify-ridge.mjs H14: `interior cliffs (|Δh|>5cm): 0  maxΔh=1.2cm` |
| H14 in-engine: single continuous surface, swale present, NO cliff | PASS | H14_Geo reimported via Unity MCP (log: `[HoleLiteImporter] Hole 14 imported — terrain 311m(X) x 338m(Z)`). Orbit video `green_orbit_h14_2tier_h14_2tier_gate_orbit.mp4` shows smooth continuous surface at 18° grazing. Frame extract confirms no cliff. |
| Side-agnostic interior Δh scan on all 18 holes — 0 interior cliffs everywhere | PASS | verify-ridge.mjs --all: 18/18 PASS. Interior cliff gate: `18/18 PASS`. Detailed per-hole table in §2 above. |
| Holes 3/7/11/18: ridge-band perp slope max ≤ 12% + band continuity PASS | PASS | H03: 2.4%, H07: 2.1%, H11: 3.2%, H18: 3.0% — all far below 12%. Band continuity ✓ on all 4. |
| `--all` regenerates all 18 green.json files (including H06 previously uncaught) | PASS | All 18 holes PASS bake QA. H06 now correctly single-region (was previously borderline). |
| No changes to Poisson loop, buildSlopeGrid, dilateHeightMask, importer | PASS | Code inspection: only the guard conditions and the arrow remapping changed. Poisson iterations, source term computation, Gauss-Seidel loop unchanged. |
| Schema v2 byte layout intact | PASS | `greenJson.schemaVersion` still 2 on all output files. QA PASS on all 18 baked holes. |
| Canonical video (Rule 17): real orbit, ≥50KB, captioned | PASS | `videos/green_orbit_h14_2tier_h14_2tier_gate_orbit.mp4`: 3.9MB, 60fps, 469 frames, captioned via build_bot_video.py |
| Canonical screenshot (Rule 14): long edge ≥ 900px, LOW grazing angle | PASS | `screenshots/h14_2tier_gate_grazing_frame2s.png`: 1920×1080, 18° grazing angle |
| Motion gate — r_frame_rate ≥ 30/1 (not 1/2 slideshow) | PASS | H14: r_frame_rate=60/1. H07: r_frame_rate=60/1 |
| Motion gate — 90° pixel diff > 12 | PASS | H14: 13.8 > 12 PASS. H07: 24.2 > 12 PASS. |
| Caption renders without occluding green surface | PASS | Frame extract at 2s confirmed: caption is a semi-transparent bottom-center overlay, not over the green surface |
| GreenOrbitElevationDeg = 18° noted (was 38°) | PASS | `HoleFlyoverRecorder.cs` comment: "lowered to grazing for iter-13 2-tier-gate (was 38° — too high to resolve interior cliffs)" |

---

## Mesh metrics (Rule 16)

From `verify-ridge.mjs --all` on post-2-tier-gate baked `green.json` files:

```
H03 [2-tier]: ridge 16.8m, 65 cells, tierDrop=29.9cm, rampWidth=5.61m, perpSlopeMax=2.4%, continuity ✓, interiorCliffs=0
H07 [2-tier]: ridge 19.1m, 77 cells, tierDrop=47.4cm, rampWidth=8.89m, perpSlopeMax=2.1%, continuity ✓, interiorCliffs=0
H11 [2-tier]: ridge 17.6m, 69 cells, tierDrop=51.4cm, rampWidth=9.63m, perpSlopeMax=3.2%, continuity ✓, interiorCliffs=0
H18 [2-tier]: ridge 17.9m, 71 cells, tierDrop=51.4cm, rampWidth=9.63m, perpSlopeMax=3.0%, continuity ✓, interiorCliffs=0

H06 [single/fall-line]: 0 interior cliffs, maxΔh=1.1cm (was incorrectly getting barrier in prior iterations)
H13 [single/fall-line]: 0 interior cliffs, maxΔh=1.2cm (was incorrectly 2-tier)
H14 [single/fall-line]: 0 interior cliffs, maxΔh=1.2cm (was incorrectly 2-tier with 23cm interior cliff)

Interior cliff gate (all 18): 18/18 PASS
Ridge-band gate (tier holes): 4/4 PASS
```

No hole hits the `0.40 * greenPerpWidth` cap. No hole exceeds 12% perp slope.

---

## Console output (bake QA — key holes)

H14 bake:
```
INFO: 2-tier gate: hole 14 has a traced ridge but is NOT in TWO_TIER_HOLES {3,7,11,18} — treating as single region (fall-line, not a height step). All 8 arrows remapped to region 0.
INFO: ridge-band smoothing (iter-13 2-tier gate): ridge present but hole 14 not in TWO_TIER_HOLES — smoothing skipped (fall-line hole, single region)
INFO: arrows=8, ridgePresent=false, regionCount=1, grid=53x51
PASS: hole 14 bake complete
```

H07 bake:
```
INFO: ridge-band smoothing (iter-13 amendment): rampWidth=8.89m (drop-scaled, target slope 8%), bandCells=681, maxDeltaH=11.59cm
INFO: arrows=8, ridgePresent=true, regionCount=2, grid=54x61
PASS: hole 07 bake complete
```

H13 bake:
```
INFO: 2-tier gate: hole 13 has a traced ridge but is NOT in TWO_TIER_HOLES {3,7,11,18} — treating as single region (fall-line, not a height step). All 12 arrows remapped to region 0.
INFO: arrows=12, ridgePresent=false, regionCount=1, grid=49x62
PASS: hole 13 bake complete
```

---

## Open questions for Architect

None. All spec items implemented and verified.
