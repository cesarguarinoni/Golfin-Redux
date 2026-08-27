# SPEC — `hole_heightmap_density` (Queued — runs alongside `quality_tiers`; Cesar 2026-08-27)

> Importer fix, applied to every device — inside the "identical terrain on every tier" rule because every tier gets the same new terrain.

## Goal

Hole 06 is the worst pose in the game (3.88 M tris / 4,006 batches at the tee, collapses hardest under heat: 60 → 40.7 fps) because `HoleGeoImporter` hard-codes a **2049² heightmap for every hole regardless of terrain size** (`Assets/Scripts/Editor/CourseImporter/HoleGeoImporter.cs:550`, `int actualRes = 2049;`). On H06's 228.9 × 100.6 m terrain that is 0.112 × 0.049 m per sample (182 samples/m² vs H08's 26 — report §10.5), and `heightmapPixelError 5` is screen-space so the density survives into the mesh. Make the importer pick the heightmap resolution from the terrain size, re-import H06 first, measure, then decide on the other holes.

## Implementation

1. `HoleGeoImporter`: replace the constant with `PickHeightmapResolution(terrainX, terrainZ)` → smallest power-of-two+1 in {513, 1025, 2049} that gives **≤ 0.25 m per sample on the longer axis** (H06 → 1025 = 0.22 m; H08 463 m → 2049 = 0.23 m; H01 576 m → 2049 = 0.28 m ✔ stays). The `rawRes != actualRes` upsample branch (`:589`) already handles source ≠ target; the downsample direction needs the same care (average, don't decimate). Log the chosen res per hole.
2. The **sim heightmap** (`Resources/HoleData/<course>/Hole_NN/heightmap.bytes`, GHM1, `res` in header) is baked from the same data: keep the sim at whatever the importer now produces and confirm `HeightmapLoader` reads the header `res` (it does — `HeightmapLoader.cs:14-53`) so nothing assumes 2049. Green.json / zones.json / tree_obstacles unaffected by resolution, but **re-bake everything for H06 in one pass** so `bake_hash` is coherent.
3. Re-import **Hole 06 only**. Tree placement: `TreePlacer` is seeded — confirm the seed path so the 434 Fir instances land in the same places (the fairness rule for content: the new H06 must have the same trees). If placement drifts, that is a finding, not a fix — report and stop.
4. Measure: H06 tee, pinned sky/yaw, 3 runs, before/after tris + batches + fps + 5-min endurance at High (the tier spec's endurance table is the comparison).
5. Visual: H06 tee and green screenshots before/after — Cesar judges the ground silhouette (bunker lips, green contour). Ball roll on the H06 green: 3 reference putts from the physics lab preset must land within the existing putt tolerance (the sim reads the new heightmap — this is the one place "identical terrain" is deliberately changed for everyone).

## Out of scope

Other holes (decide after H06 numbers), pixel error, basemap, tier work.
