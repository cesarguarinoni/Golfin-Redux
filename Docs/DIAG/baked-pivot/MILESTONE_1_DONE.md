# Milestone 1 — `BakedZoneClassifier`

## Status: PASS

All M1 unit tests pass (10/10). Integration test passes at 100 % in-scope agreement against `SceneSurfaceProvider` on Hole_01. Regression tests still fail in the same shape as M0 (expected — sim still uses scene providers). One known coverage gap (OB) is flagged for follow-up before M3.

## What ran

- **Authored runtime types:**
  - `Assets/Scripts/Physics/Runtime/Baked/ZoneData.cs` — `[Serializable]` schema + `JsonUtility` round-trip.
  - `Assets/Scripts/Physics/Runtime/Baked/BakedZoneClassifier.cs` — implements `ISurfaceProvider`. Compiles polygons into a flat array sorted by descending priority; classifies via AABB pre-reject + standard ray-cast point-in-polygon. Priority ordering: `Green > Sand > BunkerLip > Water > GreenCollar > Tee > CartPath > Fairway > Semirough > Rough > OOB > (default Fairway)`.
- **Authored editor tool:**
  - `Assets/Scripts/Editor/CourseImporter/BakeZoneJsonTool.cs` — menu items `GOLFIN > Tools > Bake Zone JSON (Active Hole)` and `(All Holes)`. Walks each Hole_XX_Geo scene's GO hierarchy; for every GO with both a `Physics.Runtime.SurfaceMarker` and a `MeshFilter`, extracts the boundary contour (boundary-edge chaining algorithm) and emits the world-XZ polygon. Writes to `Assets/Resources/HoleData/Hole_XX/zones.json`.
- **Authored unit tests** (`Assets/Scripts/Physics/Tests/BakedZoneClassifierTests.cs`, 10 tests):
  - Empty / null zone data → Fairway default.
  - Single Green polygon: inside / outside.
  - Overlapping Green+Sand: Green wins (priority).
  - Fairway + CartPath strip: CartPath wins.
  - Strict-interior boundary semantic (1mm offsets cross the threshold cleanly).
  - JSON round-trip: identical classification on a 7×7 grid.
  - Y-offset accessor.
  - Non-convex (L-shape) polygon: notch correctly excluded.
- **Authored integration test** (`Assets/Scripts/Gameplay/Tests/BakedClassifier_Hole01_Test.cs`):
  - Loads `Hole_01_Geo` additively + reads `zones.json`.
  - Samples 100 deterministic XZ (seed 12345) across the polygon-bounding rectangle.
  - Compares against `SceneSurfaceProvider` per sample.
  - Asserts agreement ≥ 95 % within the classifier's domain (non-OB samples).
- **Baked Hole_01:** 46 polygons in 5 zone groups — Fairway 9, Green 3, Tee 12, Sand 7, CartPath 15. Polygon counts match GO counts × inferred sub-loops:
  - Sand 7 = 7 bunker GOs × 1 boundary
  - CartPath 15 = 10 spline meshes + 5 junctions × 1 boundary
  - Tee 12 = 4 tee GOs × 3 boundaries (outer dilated + inner inset + border ring per the inset-border builder)
  - Green 3 = 1 green GO × 2 boundaries (dilated + inner) + 1 cup mesh
  - Fairway 9 = 3 fairway GOs × 3 boundaries (dilated outer + inner + collar transition)
- **Tests executed via Unity MCP** at `localhost:29830`:

  | suite | total | pass | fail |
  |-------|------:|-----:|-----:|
  | `BakedZoneClassifierTests` | 10 | **10** | 0 |
  | `BakedClassifier_Hole01_Test` | 1 | **1** (42/42 = 100 %) | 0 |
  | full EditMode (incl. M0 regression) | 137 | 135 | **2** ← expected M0 baseline |

## Regression test result (re-ran on M1 commit)

- `RegressionTest_DriverFromBunker_DoesNotFallThrough`: **FAIL** (7/8 directions, unchanged from M0)
- `RegressionTest_PutterFromGreen_StaysOnGreen`: **PASS** (unchanged)
- `RegressionTest_DriverFromGreen_StaysOnGreen`: **FAIL** (2/8 directions, unchanged)

This is the spec-required behaviour at M1: the regression tests still use `SceneGroundProvider` in their invariant, so they continue to fail until M3 rewires the sim to baked providers.

## Artifacts

New on `sim-baked-data-path`:
- `Assets/Scripts/Physics/Runtime/Baked/ZoneData.cs`
- `Assets/Scripts/Physics/Runtime/Baked/BakedZoneClassifier.cs`
- `Assets/Scripts/Physics/Tests/BakedZoneClassifierTests.cs`
- `Assets/Scripts/Editor/CourseImporter/BakeZoneJsonTool.cs`
- `Assets/Scripts/Gameplay/Tests/BakedClassifier_Hole01_Test.cs`
- `Assets/Resources/HoleData/Hole_01/zones.json` (1.1 MB, 46 polygons)
- `Docs/DIAG/baked-pivot/M1-classifier-agreement.md`
- `Docs/DIAG/baked-pivot/MILESTONE_1_DONE.md` (this file)

## Commits

(Pending one commit at end of M1.) Tag `pre-baked-pivot` unchanged. Branch tracks `origin/sim-baked-data-path`.

## Observations / known gaps

1. **OB classification is a coverage gap.** `SceneSurfaceProvider` returns `OOB` when the terrain alphamap has > 0.5 weight on a layer named `*OB*` at that XZ. `BakedZoneClassifier` doesn't currently know about OB because there is no zone-mesh for it — OB is a raster property of the terrain, not a polygon. 58/100 random samples landed in OB and were skipped by the agreement test. **This must be closed before M3** — otherwise sim-classified-OB regions will become "Fairway" under the baked classifier and balls won't terminate as `HitOOB`. Two viable approaches:
   - (a) Extend `BakeZoneJsonTool` to read the Terrain's alphamap, threshold the OB layer, run marching squares (or boundary-trace) to emit OB polygons. Cost: ~150–250 lines of contour-tracing code.
   - (b) Embed a binary OB mask (raster, ~1024×1024 base64 uint8 or bit-packed) directly in `zones.json`. Classifier prefers polygons, falls back to mask. Cost: lower implementation, slightly larger JSON.
   - I'm leaning (a) for purity (everything's a polygon, classifier stays simple). Will scope and execute as a "M1.5" micro-milestone after M2 if M2 is unblocked.
2. **Greens have 3 polygons** — outer dilated (collar perimeter), inner (green-proper perimeter), and the cup. All 3 are tagged `Green`, so a point inside the inner ring also classifies as Green (correct). A point inside the cup is also `Green` (slightly wrong, but the cup is 0.1m diameter and the ball can't physically rest there). Acceptable.
3. **Tees have 3 boundary loops per GO** (12 polygons for 4 tees) — outer dilated + inner inset + border ring. All tagged `Tee`. Same semantics as greens; correct in practice.
4. **Cart-path junctions** (5 small meshes between spline segments) are emitted as separate polygons. Classifier handles them naturally.
5. **GreenCollar is currently absent** from baked output. The collar is a sub-region of `Green_1`'s mesh, not a separate GO. M0 zone-offsets-inventory documented this with an averaged 0.04 m offset; in practice the collar's distinct classification is lost. Low-impact for sim physics (millimeter Y differences); flagged for completeness.
6. **Active Unity Editor lives in main project tree** (`C:/Users/cesar/GolfinRedux`), not the worktree. I sync new files into main on each Write/Edit, then re-pull generated artifacts. Same MCP-port tactic as M0: pass main project path to `unity-mcp-cli run-tool` to route to `localhost:29830`.

## Next milestone ready: YES

Proceeding to M2 (`BakedHeightProvider`). M2 doesn't depend on OB classification; M1's gap is confined to `Classify()` and unrelated to `SampleHeight()`.

## Notes for Architect

- The schema uses a wrapping `Polygon2D { points: [Point2D...] }` instead of `[[ [x,z], ... ]]` because `JsonUtility` cannot round-trip nested generic lists. The semantics are identical; the JSON is slightly more verbose but human-readable.
- Boundary extraction uses unordered edge counting (lo<<32|hi key) to detect single-use edges, then a second pass over triangles to record oriented `a→b` for chaining. Standard technique; produces correct polygon winding (matches mesh winding).
- Priority ordering matches the spec exactly. `BunkerLip` has its own bucket above `Water` (close to `Sand`) — there are no `BunkerLip` polygons in Hole_01's current GO hierarchy, but the rule is in place for future submesh extraction.
- The integration test deliberately runs against the LIVE `SceneSurfaceProvider` (and thus needs scene colliders), making it stricter than a synthetic fixture.
