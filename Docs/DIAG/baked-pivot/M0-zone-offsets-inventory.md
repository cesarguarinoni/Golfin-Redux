# M0 — Zone Y-offset inventory (HoleGeoImporter)

**Scope:** the Y offsets `HoleGeoImporter` applies per zone, split into (a) terrain-level depressions already baked into `heightmap.bytes` and (b) overlay-mesh Y offsets that sit on top of the (depressed) terrain.

M2's `BakedHeightProvider` needs (b) — one scalar per zone type, applied to `heightmap.SampleHeight(x, z)`. (a) is encoded in the heightmap and requires no runtime math.

## (a) Terrain-level depressions — already in `heightmap.bytes`

`DepressTerrainUnderOverlays` writes these into `terrainData.heights` before `PhysicsHeightmapBaker` runs. Ball-sim never sees them directly.

| depression | value (m) | applied under | source |
|---|---:|---|---|
| `OverlayDepressionMeters` | 0.40 | greens + bunkers | `HoleGeoImporter.cs:37` |
| `DepressionInsetMeters` | 0.20 | the overlap margin on overlay meshes | `HoleGeoImporter.cs:38` |
| `TeeDepressionMeters` | 0.05 | tees (anti-Z-fight only) | `HoleGeoImporter.cs:41` |
| cart-path flat drop | `dropNormalized` (scene-dependent; comes from a meter-to-normalized conversion, typical ~0.02 m) | cart-path spline footprints | `HoleGeoImporter.cs:3534–3542` |
| shore depression | `ShoreDepthMeters = 0.40`, ramped outward by `ShoreRadius = 10` cells | water polygons | `HoleGeoImporter.cs:21, 3547+` |

These all influence the heightmap but M2 does NOT re-add them — `BakedHeightProvider.heightmap.SampleHeight(x,z)` already returns the post-depression terrain Y.

## (b) Overlay-mesh Y offsets — needed by M2 `BakedHeightProvider`

Each overlay mesh is built via a CDT triangulator that takes a `yOffset` (plus sometimes a per-vertex raise) above `terrainBaseY + terrain.SampleHeight(x,z)`. These are the runtime authoritative zone heights.

| zone (Physics.SurfaceType) | constant offset (m) | per-vertex raise | total at interior (m) | source line | builder |
|---|---:|---|---:|---:|---|
| `Green` | 0.03 | +0.08 (`GreenRaiseMeters`, interior only) | **0.11** | 2409 + 2542 | `CreateGreenMeshCDT` |
| `GreenCollar` | 0.00 | smoothstep 0.08 → 0 over `collarWidth = 0.6 m` | 0.08 at inner edge → 0 at outer edge | 2490 + 2546–2548 | `CreateGreenMeshCDT` |
| `Sand` (Bunker) | 0.02 | — | **0.02** | 4139 | `CreateFlatContourMesh` |
| `Fairway` | 0.015 | — | **0.015** | 4269 | `CreateFairwayMesh` |
| `Tee` | 0.005 | — | **0.005** | 4401, 4557 | `CreateTeeMeshWithInsetBorder`, `CreateTeeMeshFlat` |
| `Tee` (legacy bordered, rarely used) | 0.02 | — | 0.02 | 4641 | `CreateTeeMeshWithBorder` |
| `CartPath` (spline mesh) | 0.01 | — | **0.01** | 4914 | `CreateSplineCartPaths` |
| `CartPath` (spline end-patch) | 0.017 | — | 0.017 | 5302 | end-patch builder |
| `Water` (surface mesh) | absolute Y ≈ 0 | — | — | 2829+ | `CreateWaterMeshes`. Terminal in sim; Y irrelevant beyond classification. |
| `Rough` / `SemiRough` / `OB` / default | — | — | **0 (use heightmap directly)** | n/a | no overlay mesh; terrain is the surface |

**Physics surface markers** (`Golfin.Physics.SurfaceType`) are the runtime types the BallSimulation/SurfaceConfig already consume. Mapping from Course.SurfaceType → Physics.SurfaceType happens via `SurfaceMarkerMap.MapCourseToPhysics` (see `HoleGeoImporter.cs:4194`). The BakeZoneJsonTool should emit the Physics type directly.

## Proposed per-zone offset for `BakedHeightProvider` (M2)

Simple scalar-offset model (matches the spec's `yOffsetFromTerrain: 0.0` field). Collar's smoothstep is approximated by a single mid-value.

```csharp
// Offset added to heightmap.SampleHeight(x, z) for each zone.
Physics.SurfaceType.Green       => 0.11f;   // HoleGeoImporter.cs:2409, 2542 — interior raise
Physics.SurfaceType.GreenCollar => 0.04f;   // mean of smoothstep 0..0.08 over collarWidth
Physics.SurfaceType.Sand        => 0.02f;   // HoleGeoImporter.cs:4139
Physics.SurfaceType.Fairway     => 0.015f;  // HoleGeoImporter.cs:4269
Physics.SurfaceType.Tee         => 0.005f;  // HoleGeoImporter.cs:4401, 4557
Physics.SurfaceType.CartPath    => 0.01f;   // HoleGeoImporter.cs:4914
Physics.SurfaceType.Rough       => 0.00f;
Physics.SurfaceType.SemiRough   => 0.00f;
Physics.SurfaceType.OB          => 0.00f;
Physics.SurfaceType.Water       => 0.00f;   // surface-as-terminal; classifier triggers HitWater
```

## Priority ordering (for overlapping zones)

Per the active spec (step M1.1), priority for overlapping polygons:

```
Green > Sand > Water > GreenCollar > Tee > CartPath > Fairway > Rough (default)
```

Rationale: rewards the most-specific zone the player perceives visually. A ball on the green-bunker overlap region reads as Green (tallest Y-offset); a ball on cart-path-through-fairway reads as CartPath (specific zone trumps the broad Fairway).

## What the current architecture does wrong (reminder for M3 correctness bar)

Current `SceneGroundProvider.SampleHeight(x, z)` raycasts the live scene and returns max-Y. This picks up:
- Fringe mesh that overlaps green at some XZ → picks fringe, not green (the fringe collar is 3 cm HIGHER than the bare green at the boundary due to per-vertex raise ordering).
- Bunker-rim terrain as a neighbor → picks rim, reports ball-in-bunker is ~1.4 m "above ground," camera adjusts.
- **Missing collider coverage in some directions (B'1 finding):** `SampleHeight` returns 0 (world origin Y) when no hit at all → airborne integrator sees `ballY <= 0` never triggers → ball free-falls to Y=-2301.

M2's `BakedHeightProvider` eliminates all three: heightmap is authoritative, zone offset is a deterministic scalar, and "missing coverage" can't happen (heightmap covers the entire terrain rect).

## Notes for Architect

- **Collar smoothstep.** For M1's polygon classifier, treating GreenCollar as a constant 0.04m offset loses the visible ramp. Acceptable for sim (ball physics doesn't care about mm-scale ramp within the 60cm collar band), not acceptable for visual alignment (which already comes from the rendered mesh, not the baked data — so still fine).
- **Legacy bordered tee builder** (`CreateTeeMeshWithBorder`, 0.02 m). Only a few holes still use it. Most tees flow through the 0.005 m path. M1's BakeZoneJsonTool should use the MESH centroid Y relative to local terrain to determine the actual offset per-instance rather than hard-coding 0.005, so legacy variants read correctly. (Alternative: hardcode 0.005 and accept that legacy bordered tees sit 1.5 cm "under" the mesh — unlikely to affect physics materially.)
- **Cart-path spline end-patches** (0.017 m) sit between standard spline mesh segments (0.01 m) and tee surfaces (0.005 m). At a junction, ball transitions between them; the current `SceneGroundProvider` max-Y picks either; baked architecture picks the classified zone. Worth a regression on a spline-junction hole in M4.
- **All offsets are millimeter- to centimeter-scale.** Invariant tolerance of 0.05 m in the M0 regression test comfortably accommodates every zone above without false positives.
