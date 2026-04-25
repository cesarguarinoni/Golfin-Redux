# Milestone 2 — `BakedHeightProvider`

## Status: PASS (with 1 known correctness gap escalated for Architect review before M3)

All 7 unit tests pass. Integration test passes (>= 20 in-scope samples; spec doesn't fail on divergence, only flags). 95/100 random Hole_01 samples within ±5 cm; 5/100 diverge by ~0.40 m and the cause is fully diagnosed.

## What ran

- **Authored runtime type:** `Assets/Scripts/Physics/Runtime/Baked/BakedHeightProvider.cs`. Implements `IGroundProvider`. `SampleHeight(x, z) = heightmap.SampleHeight(x, z) + classifier.GetYOffset(typeAt(x, z))`. 3-arg `SampleHeight(x, z, preferred)` honours the caller's preferred-zone hint when its offset is greater than the actual classification's offset (mirrors the legacy `SceneGroundProvider` 3-arg "ball-on-bunker stays on bunker" semantic). Null-safe for both heightmap and classifier inputs.
- **Authored unit tests** (`BakedHeightProviderTests.cs`, 7 tests):
  - No-zones → terrain Y verbatim.
  - Inside Sand → adds 0.02.
  - Inside Green → adds 0.11.
  - Overlapping Green+Sand → Green offset wins (priority).
  - 3-arg preferred (Green requested at Sand-classified XZ) → uses higher of the two offsets.
  - Null classifier → terrain Y.
  - Null heightmap → 0 + classified offset.
- **Authored integration test** (`BakedHeight_Hole01_Test.cs`):
  - Loads Hole_01_Geo additively + reads `zones.json` + reads `heightmap.bytes` from `Tools/UHoleGeo/output/lomond-country-club/export/hole-01/heightmap.bytes`.
  - Samples 100 deterministic XZ across polygon bounds (seed 7777).
  - Compares `BakedHeightProvider.SampleHeight` vs `SceneGroundProvider.SampleHeight` (max-Y raycast).
  - Writes `M2-height-agreement.md` with histogram, max/mean divergence, full table of diverging samples.
- **Re-baked Hole_01 heightmap.bytes** via `Import > Bake Physics Heightmap > Bake Hole 01` (was 4 days stale).
- **Tests executed via Unity MCP** at `localhost:29830`:

  | suite | total | pass | fail |
  |-------|------:|-----:|-----:|
  | `BakedHeightProviderTests` (M2 unit) | 7 | **7** | 0 |
  | `BakedHeight_Hole01_Test` (M2 integration) | 1 | **1** | 0 |
  | full EditMode | 145 | 143 | **2** ← expected M0 baseline |

## Regression test result (M2 commit)

Unchanged from M0 — `SceneGroundProvider` is still wired into the sim:
- `RegressionTest_DriverFromBunker_DoesNotFallThrough`: **FAIL** (7/8)
- `RegressionTest_PutterFromGreen_StaysOnGreen`: **PASS** (8/8)
- `RegressionTest_DriverFromGreen_StaysOnGreen`: **FAIL** (2/8)

These pass when M3 rewires the providers.

## Divergence analysis (M2-height-agreement.md, current state)

```
- In-scope samples: 100
- Within tolerance (±5 cm): 95 / 100
- Diverged (>5 cm): 5 / 100
- Max divergence: 0.408 m
- Mean abs divergence: 0.0341 m

Histogram:
  0–1 cm:  2
  1–2 cm: 93
  2–5 cm:  0
  5–10 cm: 0
  >10 cm:  5
```

All 5 diverging samples land inside `Fairway` or `CartPath` polygons and consistently differ by **~0.40 m**, exactly matching `OverlayDepressionMeters` in `HoleGeoImporter`.

### Root cause (diagnosed end-to-end)

The HoleGeoImporter pipeline:

1. Builds zone meshes (Bunkers, Greens, Tees, Fairways, CartPaths) at lines 230 / 233 / 242 / 3995 — at this point Terrain heights are *un-depressed*. Each mesh vertex Y = `terrain.SampleHeight(vertXZ) + meshOffset`.
2. Calls `DepressTerrainUnderOverlays` at line 307 — depresses terrain heights under fairway / cart-path / tee polygons by `OverlayDepressionMeters` (0.40 m) / `TeeDepressionMeters` (0.05 m).

So the visible mesh top is `un-depressed_terrain + meshOffset` everywhere within the dilated mesh boundary, but the heightmap.bytes (baked from `terrainData.GetHeights()` after import completes) captures the *post-depression* heights. The depression only applies inside the original (non-dilated) contour; the dilated outer ring keeps the un-depressed value.

That gives two regions inside any dilated fairway mesh:

| region | heightmap.bytes | visible mesh Y | M2 bakedY (offset 0.015) | divergence |
|---|---|---|---|---|
| Inside original contour (depressed) | un-dep − 0.40 | un-dep + 0.015 | un-dep − 0.385 | **+0.40 m sceneY > bakedY** |
| Inside dilated ring (un-depressed) | un-dep | un-dep + 0.015 | un-dep + 0.015 | 0 (within tolerance) |

The 5/100 diverging samples sit in the inside-original-contour region; the 95/100 within-tolerance samples are either Rough (no offset) or in the dilated ring (where heightmap incidentally matches un-depressed terrain).

### Why I tried — and reverted — a `0.40 + meshOffset` patch

Inflating `Fairway`/`CartPath`/`Tee` offsets to `depression + meshOffset` (0.415 / 0.41 / 0.055) fixes the inside-original-contour case but inverts the bug for the dilated ring (where heightmap is already un-depressed). The re-baked test showed 93/100 diverging by +0.415 m — net worse.

Reverted to mesh offsets only and committed the divergence pattern as a known M2 issue.

### Impact for M3

With the current state, sim balls inside `Fairway` / `CartPath` original contours will sit ~0.40 m below the visible mesh — the same fall-through class M0's regression test catches. **M3 will fail until this is closed.** Three viable remediation paths (Architect to choose):

- **Path A: per-polygon mesh-Y baking.** `BakeZoneJsonTool` extracts not just the XZ contour but a triangulated XZ→Y surface per zone; `BakedHeightProvider` interpolates Y from the polygon's actual mesh vertices. Requires extending `ZoneData` to carry vertex Ys + tri indices. Eliminates the depression-band issue by construction (we're sampling the visible mesh directly). ~150 lines + tests.
- **Path B: bake `heightmap.bytes` from un-depressed terrain.** Either (b1) snapshot terrain heights *before* `DepressTerrainUnderOverlays` runs and feed that to `PhysicsHeightmapBaker`, or (b2) un-apply the depression in the baker by reading the depression masks. (b1) is cleaner but requires `HoleGeoImporter` to expose the pre-depression snapshot to the baker. (b2) keeps the baker self-contained but couples it to importer-internal masks.
- **Path C: bake "depression bands" as separate zone groups** with effective offsets `depression + meshOffset`. Ring areas keep the mesh-only offset. Requires reading the per-hole depression mask from `HoleGeoImporter`, not derivable from MeshFilter alone.

I'm leaning Path A (most general — also future-proofs Greens/Bunkers when their bowl meshes have non-trivial slope). Will surface for Architect's call before starting M3.

## OB classification gap (carried over from M1)

Still open. The 100-sample test bounds are the polygon bounding rectangle (smaller than terrain extent), and at seed 7777 no samples landed in OB territory, so the M2 integration didn't surface it. M2 inherits the M1 gap: OB-classified-by-terrain-alphamap regions classify as `Fairway` under the baked classifier. **Both gaps must close before M3.**

## Artifacts

New on `sim-baked-data-path`:
- `Assets/Scripts/Physics/Runtime/Baked/BakedHeightProvider.cs`
- `Assets/Scripts/Physics/Tests/BakedHeightProviderTests.cs`
- `Assets/Scripts/Gameplay/Tests/BakedHeight_Hole01_Test.cs`
- `Docs/DIAG/baked-pivot/M2-height-agreement.md`
- `Docs/DIAG/baked-pivot/MILESTONE_2_DONE.md` (this file)

Modified:
- `Assets/Scripts/Editor/CourseImporter/BakeZoneJsonTool.cs` — annotated `YOffsets` with the depression-band issue.
- `Assets/Resources/HoleData/Hole_01/zones.json` — re-baked via the corrected tool.
- `Tools/UHoleGeo/output/lomond-country-club/export/hole-01/heightmap.bytes` — re-baked (was stale; re-bake didn't change the divergence pattern, only refreshed the data).

## Commits

(One M2 commit pending.) Tag `pre-baked-pivot` unchanged.

## Next milestone ready: NO

Holding before M3. Three correctness gaps (depression-band height divergence, OB classification, possibly Tee depression) must close before the regression tests can credibly pass under the baked architecture.

Recommended ordering:

- **M2.5a (this branch):** Bake OB polygons from terrain alphamap via marching squares or row-strip rectangle decomposition. Re-run the M1 agreement test → expect ≥ 95 % including OB samples.
- **M2.5b (this branch):** Implement Path A (per-polygon mesh-Y baking) OR Path B (un-depressed heightmap). Re-run the M2 height test → expect ≤ 1 % > 5 cm divergence.
- **M3:** flip sim to baked providers + re-run regression. Expect 24/24 PASS.

Will surface for Architect's call on (a)/(b) path before kicking off M2.5b. Path A is my recommendation.

## Notes for Architect

- The 3-arg `SampleHeight(x, z, preferred)` semantic in `BakedHeightProvider` is a soft equivalent of the legacy `SceneGroundProvider` 3-arg behaviour. In baked architecture there's no "another collider at this XZ might overlap higher" race condition, so the hint is informational. I picked "use the higher of actual/preferred offsets" as the safest default. Open to a different policy if Architect wants something stricter (e.g. always use actual classification).
- `BakedHeight_Hole01_Test.cs` uses an extension method (`Classify_NoOp_GetGroundY`) on `SceneGroundProvider` for readability. Internal-only; remove in Phase F when scene providers are deleted.
- Re-baking `heightmap.bytes` did NOT change the divergence pattern — confirms the bug is structural (mesh-vs-heightmap relationship), not staleness.
- I did not implement Path A in M2 because the spec scope ("Goal: new ground provider reads heightmap.bytes + applies per-zone Y offsets from JSON") explicitly framed the offset as scalar per zone. Treating mesh-Y as variable per-XZ is a richer mechanism that warrants Architect concurrence before I bake it in.
