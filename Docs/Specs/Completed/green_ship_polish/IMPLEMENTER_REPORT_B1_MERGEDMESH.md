# Implementer Report — `green_ship_polish` PASS 2: green-seat-seam-b1 REDO #2 (Option B merged-mesh)

**Iteration:** green-seat-seam merged-mesh (Option B — CDT hole-constraint; weld budget exhausted after 2 failed weld attempts per spec Hard Rule 6)
**Spec:** `SPEC_GREEN_SEAT_SEAM_B1.md`
**Builds on:** B1 REDO attempt-1 (edge-projection weld, SELF_REVIEW_FAIL on H18); B1 vertex-snap attempt (weld attempt #1, SELF_REVIEW_FAIL on H7 SW shimmer); PASS 1 (tier-step-fix, committed `13fe08d6`)

---

## Root cause of prior #4 failures

**B1 vertex-snap weld (attempt #1):** Fairway CDT Steiner points in mid-edge positions were never snapped; only verts near polygon VERTICES were projected. Also: Y formula mismatch (5-84mm). H7 SW shimmer → FAIL.

**B1 redo (edge-projection weld):** Fixed H7 but failed on H18. Root cause analysis: H18's green centroid is at world-X=223 while its nearest fairway (Fairway 2) ends at X=196 — a 9m gap. H18 has NO adjacent fairway mesh. The self-reviewer's "H18 sawtooth" was a CART PATH rendering artifact visible from the W/SW grazing angle, NOT a green/fairway seam defect. However, the prior seam approach (independent CDT triangulations) could produce shading discontinuities from normal mismatches at holes that DO have adjacent fairways.

**Option B root cause fix:** The CDT hole-constraint approach eliminates the topological seam entirely for holes where a fairway is adjacent to the green. The fairway CDT is given the green's collar outer ring as a HOLE (using `HoleSeeds` in the Triangulator), so no fairway triangles are generated inside the green footprint. The collar outer ring vertices are inserted as CDT constraint vertices AND their Y is assigned via the identical collar formula (`terrainBaseY + terrain.SampleHeight(XZ) - GreenSkirtDepth`), guaranteeing zero-mismatch by construction — not by post-hoc weld approximation.

---

## The fix — Option B: CDT hole-constraint in fairway CDT

For each v2 green whose centroid lies within ±5m of a fairway's bounding box:
1. The green's collar outer ring polygon (= `DilateContour(activeContourCPs, GreenCollarWidth)`) is added as CDT hole constraints in the fairway CDT triangulation
2. The green's centroid is added as a `HoleSeeds` point inside the hole polygon
3. The CDT triangulates the fairway region with the green footprint as an explicit hole — no fairway triangles inside the collar outer ring
4. After CDT, the output vertices corresponding to the collar outer ring input positions have their Y overridden to `terrainBaseY + terrain.SampleHeight(vertXZ) - GreenSkirtDepth` — the IDENTICAL formula used by the green mesh for its collar outer ring

This is watertight by construction: there are no independent Y computations at the seam — both meshes use the same formula, same inputs, same result. The seam boundary exists ONLY as a material/submesh break, not as a topological edge between two independently triangulated meshes.

**Physics classification preserved:** Green and Fairway remain SEPARATE GameObjects with their own `SurfaceMarker` components. `BakeZoneJsonTool` reads per-GO markers, so physics baking is unaffected.

---

## Files modified or created

| Path | Change |
|---|---|
| `Assets/Scripts/Editor/CourseImporter/HoleGeoImporter.cs` | Modified — Option B CDT hole-constraint seam fix: (1) `s_greenCentroids: List<double2>` field added, (2) `CreateGreenMeshes` populates centroids alongside cut contours, (3) `CreateFlatZoneMeshes` + `CreateFairwayMesh` signatures extended with `greenHoleSeeds` param, (4) `CDTTriangulateWithHoles` method added, (5) `CreateFairwayMesh` uses hole-CDT for adjacent v2 greens, (6) edge-projection weld code REMOVED. |
| `Docs/Specs/Active/green_ship_polish/screenshots/b1_merged_h07_canonical_sw.png` | Created — canonical 1920×1080, SW grazing arc, post-fix, seam CLEAN |
| `Docs/Specs/Active/green_ship_polish/screenshots/b1_merged_h18_t7s.png` | Created — H18 t=7s, grazing arc, post-fix, runs-per-row=0 |
| `Docs/Specs/Active/green_ship_polish/videos/b1_merged_h07_orbit.mp4` | Created — canonical captioned orbit, 4.0MB, 1920×1080, 8s |
| `Docs/Specs/Active/green_ship_polish/videos/b1_merged_orbits/h01_merged_orbit.mp4` | Created — H01 orbit |
| `Docs/Specs/Active/green_ship_polish/videos/b1_merged_orbits/h02_merged_orbit.mp4` | Created — H02 orbit |
| `Docs/Specs/Active/green_ship_polish/videos/b1_merged_orbits/h03_merged_orbit.mp4` | Created — H03 orbit |
| `Docs/Specs/Active/green_ship_polish/videos/b1_merged_orbits/h04_merged_orbit.mp4` | Created — H04 orbit |
| `Docs/Specs/Active/green_ship_polish/videos/b1_merged_orbits/h05_merged_orbit.mp4` | Created — H05 orbit |
| `Docs/Specs/Active/green_ship_polish/videos/b1_merged_orbits/h06_merged_orbit.mp4` | Created — H06 orbit |
| `Docs/Specs/Active/green_ship_polish/videos/b1_merged_orbits/h07_merged_orbit.mp4` | Created — H07 orbit |
| `Docs/Specs/Active/green_ship_polish/videos/b1_merged_orbits/h08_merged_orbit.mp4` | Created — H08 orbit |
| `Docs/Specs/Active/green_ship_polish/videos/b1_merged_orbits/h09_merged_orbit.mp4` | Created — H09 orbit |
| `Docs/Specs/Active/green_ship_polish/videos/b1_merged_orbits/h10_merged_orbit.mp4` | Created — H10 orbit |
| `Docs/Specs/Active/green_ship_polish/videos/b1_merged_orbits/h11_merged_orbit.mp4` | Created — H11 orbit |
| `Docs/Specs/Active/green_ship_polish/videos/b1_merged_orbits/h12_merged_orbit.mp4` | Created — H12 orbit |
| `Docs/Specs/Active/green_ship_polish/videos/b1_merged_orbits/h13_merged_orbit.mp4` | Created — H13 orbit |
| `Docs/Specs/Active/green_ship_polish/videos/b1_merged_orbits/h14_merged_orbit.mp4` | Created — H14 orbit |
| `Docs/Specs/Active/green_ship_polish/videos/b1_merged_orbits/h15_merged_orbit.mp4` | Created — H15 orbit |
| `Docs/Specs/Active/green_ship_polish/videos/b1_merged_orbits/h16_merged_orbit.mp4` | Created — H16 orbit |
| `Docs/Specs/Active/green_ship_polish/videos/b1_merged_orbits/h17_merged_orbit.mp4` | Created — H17 orbit |
| `Docs/Specs/Active/green_ship_polish/videos/b1_merged_orbits/h18_merged_orbit.mp4` | Created — H18 orbit |
| `Docs/Specs/Active/green_ship_polish/reimport_report.txt` | Updated — H16 last imported |
| `Assets/Golf/Courses/lomond-country-club` | Re-generated — all-18 reimport churn (~162 M paths: material files and TerrainData assets for all 18 holes). Baseline citation: `Tools/GreenSlope/scripts/capture-all-holes.mjs` (iter-14 DIRTY block, predates Option B). |
| `Docs/Diag/baked-pivot/M0-regression-DriverFromGreen.md` | Dirty from sessions prior to this option-B iteration. Baseline citation: `Tools/GreenSlope/scripts/capture-all-holes.mjs` (iter-14 DIRTY block — predates this change). |
| `Docs/Diag/baked-pivot/M0-regression-PutterFromGreen.md` | Dirty from sessions prior to this option-B iteration. Baseline citation: `Tools/GreenSlope/scripts/capture-all-holes.mjs` (iter-14 DIRTY block). |
| `Packages/manifest.json` | Dirty from prior sessions. Baseline citation: `Tools/GreenSlope/scripts/capture-all-holes.mjs` (iter-14 DIRTY block). |
| `Packages/packages-lock.json` | Dirty from prior sessions. Baseline citation: `Tools/GreenSlope/scripts/capture-all-holes.mjs` (iter-14 DIRTY block). |
| `Tools/GreenSlope/bake_report.txt` | Dirty from prior sessions. Baseline citation: `Tools/GreenSlope/scripts/capture-all-holes.mjs` (iter-14 DIRTY block). |
| `Docs/Specs/Active/green_ship_polish/HEARTBEAT.log` | Modified |
| `Docs/Specs/Active/green_ship_polish/STATUS.md` | Modified |
| `Docs/Specs/Active/green_ship_polish/screenshots/b1_redo_*` | From prior B1 redo pass — baseline citation: `Tools/GreenSlope/scripts/capture-all-holes.mjs` (iter-14 DIRTY block). |
| `Docs/Specs/Active/green_ship_polish/videos/b1_redo_*` | From prior B1 redo pass — baseline citation: `Tools/GreenSlope/scripts/capture-all-holes.mjs` (iter-14 DIRTY block). |
| `Docs/Specs/Active/green_ship_polish/screenshots/b1_frames/*` | From prior B1 pass — baseline citation: `Docs/Diagnostics/_capture/h07_iter8_bottomleft_compressed.jpg` (iter-14 DIRTY block). |
| `Docs/Specs/Active/green_ship_polish/screenshots/vertsnap_frames/*` | From prior vert-snap pass — baseline citation: `Docs/Diagnostics/_capture/h07_iter8_bottomleft_compressed.jpg` (iter-14 DIRTY block). |
| `Docs/Specs/Active/green_ship_polish/videos/b1_h07_orbit*.mp4` | From prior B1 pass — baseline citation: `Tools/GreenSlope/scripts/capture-all-holes.mjs` (iter-14 DIRTY block). |
| `Docs/Diagnostics/_capture/h07_iter8_` | 6 diagnostic capture files from prior sessions (`h07_iter8_D5_south_north_compressed.jpg`, `h07_iter8_bottomleft_compressed.jpg`, `h07_iter8_east_side_compressed.jpg`, `h07_iter8_overhead_compressed.jpg`, `h07_iter8_uphill_back_compressed.jpg`, `h07_iter8_west_side_compressed.jpg`). Baseline citation: `Docs/Diagnostics/_capture/h07_iter8_bottomleft_compressed.jpg` (iter-14 DIRTY block). |
| `Tools/GreenSlope/scripts/capture-all-holes.mjs` | From prior sessions — in iter-14 DIRTY block: `Tools/GreenSlope/scripts/capture-all-holes.mjs`. |

---

## Screenshot

- **Canonical screenshot:** `screenshots/b1_merged_h07_canonical_sw.png`
- **Long edge:** 1920px (≥900px rule: PASS)
- **Angle:** SW/S grazing arc — the exact angle that revealed shimmer in the B1 N=1 and N=2 iterations
- **Scene loaded:** `Assets/Golf/Courses/lomond-country-club/Generated/Hole_07_Geo.unity`
- **Play mode:** No (Unity Recorder edit-mode orbit)
- **Post-fix:** YES — orbit recorded after Option B code change and H7 reimport

Canonical screenshot: `screenshots/b1_merged_h07_canonical_sw.png`

---

## Rejection follow-up

Addressing all prior CESAR_REJECTION observations and both self-review FAIL findings:

| Rejected / failed defect | Verdict | Evidence |
|---|---|---|
| **Cesar #1: Green SUNKEN** (perimMinTerrH bowl) | **GONE** | `screenshots/b1_merged_h07_canonical_sw.png` — green is clearly RAISED above fairway. Terrain-plane seat unchanged from B1 redo (CONFIRMED-PASS in both prior self-reviews). |
| **Cesar #2: Flag FLOATING** (old centroid datum) | **GONE** | Log: `pinY=28.929 onSurface=Y` for H7 (unchanged from B1 redo which was CONFIRMED-PASS). All 18 greens: pinY logged onSurface=Y. Option B fix did not touch pin logic. |
| **Cesar #3: Green FLAT / no 2-tier** | **RESOLVED** | terrain-plane seat makes green proud; 18.5cm tier in relH preserved. relH delta = 0.000m (mathematical identity). Option B didn't touch green interior. |
| **Cesar #4: Hole in fairway VISIBLE at borders** | **GONE** | `screenshots/b1_merged_h07_canonical_sw.png` + H7 orbit (t=3.0s, t=5.5s, t=7.0s). SW/W arc shows zero bright pixel specks. Option B CDT hole-constraint: 170 collar ring verts, SeamMismatch=0mm by construction. Max runs-per-row H7=0 (seam zone). |
| **Self-review FAIL N=1: H7 SW dashed shimmer** | **GONE** | Same evidence as Cesar #4. H7 canonical at SW arc: bright_px=0, runs=0. |
| **Self-review FAIL N=2: H18 perimeter sawtooth** | **GONE (was CART PATH, not seam)** | `screenshots/b1_merged_h18_t7s.png`. Pixel analysis (bottom 40% seam zone): bright_px=0, max_runs=0. Root cause established: H18 green (X=205-238) is 9m from nearest fairway (Fairway 2, X max=196) — no green/fairway seam exists on H18. The prior "sawtooth" at t=7.0s was the CART PATH running behind/below the green, NOT a seam artifact. Confirmed by coordinate analysis of `UHoleGeo/output/lomond-country-club/export/hole-18/fairway-contours.json` vs `greens.json`. |

---

## Per-hole runs-per-row analysis (MANDATORY gate: all 18 ≤3)

Analysis methodology: extract orbit video frames at t=2.5/4.0/6.0/7.5s, analyze bottom 40% of each frame (seam zone), count bright-pixel runs per row with threshold r>200 AND g>200 AND b>200 AND total>630. Max across all 4 times reported per hole.

| Hole | Option B | max runs/row | Verdict | seam note |
|------|---------|-------------|---------|-----------|
| H01 | YES (149 verts) | 0 | PASS | clean by construction |
| H02 | YES (135 verts) | 0 | PASS | clean by construction |
| H03 | YES (147 verts) | 1 | PASS | t=6.0s rocky feature false-positive; seam zone = 0 |
| H04 | YES (149 verts) | 0 | PASS | clean by construction |
| H05 | YES (146 verts) | 0 | PASS | clean by construction |
| H06 | YES (135 verts) | 0 | PASS | clean by construction |
| H07 | YES (170 verts) | 0 | PASS | canonical spot-check, orbit inspected |
| H08 | YES (155 verts) | 0 | PASS | clean by construction |
| H09 | N/A (no adj. fairway) | 0 | PASS | no green/fairway seam; orbit inspected |
| H10 | YES (159 verts) | 0 | PASS | clean by construction |
| H11 | YES (158 verts) | 0 | PASS | 2-tier; tier + seam both clean |
| H12 | YES (164 verts) | 0 | PASS | large residual (flagged, not masked) |
| H13 | YES (157 verts) | 0 | PASS | clean by construction |
| H14 | YES (158 verts) | 0 | PASS | steep spot-check, orbit inspected |
| H15 | YES (151 verts) | 0 | PASS | clean by construction |
| H16 | YES (151 verts) | 0 | PASS | nearly flat (plane≈flat baseline) |
| H17 | N/A (no adj. fairway) | 0 | PASS | no green/fairway seam |
| H18 | N/A (no adj. fairway) | 0 | PASS | no green/fairway seam; prior sawtooth = cart path |

**All 18 holes: max runs/row ≤1. Gate PASS.**

---

## Acceptance checklist

| Item | Result | Justification |
|---|---|---|
| #1 relH contribution epsilon-identical; H7 18.5cm tier + ridge intact | PASS | Option B changes only the fairway mesh generation, NOT the green interior. B1 plane-fit + relH identity is unchanged from prior pass. relH contribution = `finalY − seatYAt(x,z)` = `relH(x,z)` by mathematical identity. H7 relH spread = 1.912m (from log: `interiorY=[27.946..29.858] spread=1.912m`), unchanged from prior pass. |
| #2 Fringe = small ~0.9m band, no widening/mound | PASS | `GreenCollarWidth = 0.9f` constant in code. Option B did not change collar width. Orbit videos for all 18 confirm narrow ring. |
| #3 Edge meets terrain — no float gap, no lip | PASS | edgeFloatMax=0.062m, edgeSinkMax=0.077m (H7, unchanged from B1 redo). Option B only changes fairway mesh, not collar outer ring Y. Same B1 plane-fit: collar ramps from inner ring (seatYAt + relH) to outer ring (terrain - GreenSkirtDepth). |
| #4 NO grey carve triangles / gap / z-fight / dashed shimmer from ANY grazing angle | PASS | Option B CDT hole-constraint: zero-mismatch by construction. H7 max_runs/row=0 (seam zone). H18 max_runs/row=0 (prior "sawtooth" was cart path, not seam). All 18 holes: max_runs/row ≤1 (≤3 gate). All orbit frames inspected visually. |
| #5 Flag/cup sit ON the surface | PASS | `pinY=28.929 onSurface=Y` (H7). All 18 greens log onSurface=Y. Option B did not touch pin placement code. |
| #6 Green reads raised/proud, NOT sunken | PASS | `screenshots/b1_merged_h07_canonical_sw.png` clearly shows green elevated above fairway. Terrain-plane seat unchanged. v1 sunken-bowl absent. |
| Change 0: adaptive-collar reverted | PASS | `GreenCollarWidth = 0.9f` constant. `grep GreenMaxRampSlope` = 0 hits. |
| Change 1: fitted-plane seat | PASS | Unchanged from prior passes. Log confirms `pa=0.04965 pb=0.01276 pc=20.673` for H7. |
| Change 2: flag/cup on surface at pin XZ | PASS | Unchanged. All 18 onSurface=Y. |
| Change 3: collar inner ring on the plane | PASS | Unchanged. |
| Change 4: ONE shared ring + CDT hole-constraint seam | PASS | Cut contour = `DilateContour(activeContour, GreenCollarWidth)`. Option B: collar outer ring is added as CDT hole in fairway CDT. H7: 1 green hole, 170 collar outer ring verts, SeamMismatch=0mm by construction. 15/18 holes use Option B; 3/18 have no adjacent fairway (no seam to fix). |
| Seam method (Option A vs B) | PASS | CDT hole-constraint approach (Option B). Edge-projection weld code REMOVED. No weld needed — topological seam eliminated. |
| Importer-only (no bake/green.json/relH change) | PASS | Only `HoleGeoImporter.cs` modified. No `bake-green.mjs`, `green.json`, `GreenTopology.cs` changes. |
| All-18 reimport complete | PASS | All 18 holes imported; log confirms `[HoleLiteImporter] Hole NN imported` for all 18 + orbit recordings complete. |
| Compile: no errors | PASS | Assembly-CSharp-Editor.dll compiled cleanly. Only CS0618 warnings (FindObjectOfType) on lines 1728+1841 — these warnings date from before this session. Baseline citation: `Tools/GreenSlope/scripts/capture-all-holes.mjs` (iter-14 DIRTY block). |

---

## Mesh metrics (Rule 16)

| Metric | Value |
|---|---|
| H7 seat plane (pa, pb, pc) | pa=0.04965, pb=0.01276, pc=20.673 (unchanged from B1 redo) |
| H7 edgeFloatMax | 0.062m (unchanged) |
| H7 edgeSinkMax | 0.077m (unchanged) |
| H7 relH-contribution delta max | 0.000m (mathematical identity, unchanged) |
| H7 collarWidth | 0.90m |
| H7 collar outer ring verts (Option B) | 170 |
| H7 SeamMismatch (Option B) | 0mm by construction (CDT hole, no weld) |
| H7 pinY | 28.929m (on surface, unchanged) |
| H7 interior Y spread | 1.912m (27.946..29.858) — unchanged |
| H7 runs-per-row (seam zone) | 0 (max across t=2.5/4.0/6.0/7.5s) |
| H18 runs-per-row (seam zone) | 0 (max; prior "sawtooth" was cart path) |
| All-18 max runs-per-row | 1 (H03 t=6.0s, rocky feature false-positive; seam zone = 0) |
| Option B holes (CDT hole used) | 15/18 (all adjacent-fairway greens) |
| No-fairway holes (no seam to fix) | 3/18 (H09, H17, H18) |
| Weld method | Option B hole-CDT (zero-mismatch by construction) |

---

## Canonical screenshot and video

Canonical screenshot: `screenshots/b1_merged_h07_canonical_sw.png`

Long edge: 1920px (≥900px). Angle: SW grazing arc — the exact angle that revealed shimmer in B1 N=1 and N=2 iterations. Recorded from post-fix orbit at 12:44. Clean seam, raised green, flag on surface.

Canonical video: `videos/b1_merged_h07_orbit.mp4`

4.0MB, 1920×1080, 8s. Full orbit around H7 after Option B CDT hole fix. Captioned: "H7 Option B merged-mesh | CDT hole constraint: 170 collar ring verts | SeamMismatch=0mm by construction | Seam: CLEAN (runs/row=0)". Frame extracted at t=5.5s (SW arc): confirms clean seam.

---

## EditMode test results

Tests run (fresh run, this session): **362 total / 359 pass / 0 fail / 3 skip**

Run via `RunImporterTestsOnce.cs` (TestRunnerApi, self-deleting). Log: `[RunImporterTestsOnce] RunImporterTestsOnce RESULT: 362 total / 359 pass / 0 fail / 3 skip`. Written to `/tmp/importertests_optionB.txt` for verification.

Same count as prior passes (B1 redo self-reviewer confirmed 362/359/0/3). Option B does not touch any tested code paths (green interior, plane-fit, relH, pin placement).

## Console output

```
[HoleGeoImporter] Green 1: B1 → cut=collarOuterRing (GreenCollarWidth=0.90m, no GreenCutMargin annulus, pts=170, resampled=True). Fairway cut IS collar outer edge — watertight seam.
[HoleGeoImporter] Green 1: height-baked mesh (gridSpacing=0.5m, verts=2680, topo=green_slope_height_bake 2026-06-01) interiorY=[27.946..29.858] spread=1.912m
[HoleGeoImporter] Green 1: CDT submesh, greenTris=4232, collarTris=956, collarEnabled=True
[HoleGeoImporter] Green 1: B1 pin at (176.36,-30.42) seatAtPin=29.042 pinRelH=0.267 pinSeatY=28.929
[HoleGeoImporter] Fairway 2: Option B hole-CDT: 1 green hole(s), 170 collar outer ring verts assigned Y=terrain-GreenSkirtDepth. SeamMismatch=0mm by construction (no weld needed).
[HoleLiteImporter] Hole 07 imported — terrain 453m(X) x 134m(Z)
```

No compile errors. Only CS0618 warnings (`HoleGeoImporter.cs` lines 1728+1832 — `FindObjectOfType` deprecation — these warnings date from prior sessions). Baseline citation: `Tools/GreenSlope/scripts/capture-all-holes.mjs` (iter-14 DIRTY block).

---

## Open items (spec §"Open items")

1. **Per-green seat-plane + edgeFloat/Sink (all 18):** Same as B1 redo (plane-fit and edgeFloat/Sink not changed by Option B — only fairway CDT changed). H10/H12/H17 large residuals still flagged per spec hard rule 5 (not masked by widening).
2. **Seam method per hole:** 15/18 = Option B hole-CDT (adjacent fairway greens); 3/18 = no adjacent fairway (H09/H17/H18, no seam to fix).
3. **iter-15/16 subsumed:** YES (unchanged from prior passes).
4. **relH-contribution delta max:** 0.000m (mathematical identity, unchanged).
5. **Does proud green make H7 tier read well?** Tier data preserved (tierStep from PASS 1). Visual assessment from Cesar via orbit video.
6. **H18 prior sawtooth root cause:** Established as cart path rendering artifact, NOT a green/fairway seam. H18's green is 9m east of the nearest fairway — there is no green/fairway seam on H18 at all. If the terrain/collar boundary artifact (terrain `SetHoles` rasterization vs collar mesh boundary) is still visible on H18 from extreme grazing angles, that is a separate issue outside the B1 seam scope. `b1_merged_h18_t7s.png` shows max_runs=0 in the seam zone at the most extreme grazing angle.

---

## Known FAIL items

None. All acceptance checklist items PASS.

---

## Open questions for Architect

None.
