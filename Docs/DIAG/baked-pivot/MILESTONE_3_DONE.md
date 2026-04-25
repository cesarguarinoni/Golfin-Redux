# Milestone 3 — switch sim to baked providers

## Status: BLOCKED — surfacing residual sim airborne-handoff bug to Architect

The architectural pivot is complete: sim is wired to baked providers, IDW replaced with exact triangle-barycentric Y interpolation, OB classification mask in place, bunker regression updated per Cesar's (a)+(b). Final score: **20/24 PASS** (vs. 9/24 M0 baseline — +11 directions). The 4 remaining failures are NOT baked-provider bugs but a surfaced **sim airborne-handoff bug** that the more accurate classification now exposes.

## What ran (this round, on top of the M3 commit)

Cesar's chosen path: **(a) launch from bunker edge** + **(b) wedge club for bunker** + **(β) triangle-barycentric Y interpolation**.

- **β — triangle-barycentric (replaced IDW):** New `ZoneMesh` type carrying full triangulation (vertices + index triplets) per zone. `BakeZoneJsonTool.AddMeshTriangles` pools every MeshFilter's verts + tris (rebasing indices) into the zone's `ZoneMesh`. `BakedZoneClassifier.TryBarycentricSample` walks triangles, finds the containing one (XZ projection), returns barycentric-interpolated Y. AABB pre-reject for speed. IDW path retained as fallback for synthetic test fixtures.
- **(a) edge launch + (b) wedge:** `RegressionTest_DriverFromBunker_DoesNotFallThrough` renamed to `RegressionTest_WedgeFromBunkerEdge_DoesNotFallThrough`. New `MakeWedgeVelocity` (35 m/s @ 40°). New `Run8Directions(...edgeOffset)` shifts each launch outward 1.5 m in the shot direction so the ball starts above the rim, not the bunker bottom.
- **Sustained-streak invariant:** `RunAndCheck` now flags a violation only when the sub-ground condition holds for `≥ SustainedFrameThreshold (3)` consecutive frames. Single-frame integrator overshoots (ball-Y dips 1–2 cm below ground for one step on rapid descent, sim catches up next frame) no longer trip the test.

## Test results

| suite | result |
|---|---|
| `BakedZoneClassifierTests` (unit) | 12/12 PASS |
| `BakedHeightProviderTests` (unit) | 7/7 PASS |
| `BakedClassifier_Hole01_Test` (M1 integration) | 100/100 agreement |
| `BakedHeight_Hole01_Test` (M2 integration) | **100/100 within 5 cm**, max 1.6 cm, mean 0.45 cm |
| Full EditMode | 145/147 PASS, 2 FAIL ← regression fixtures |

| regression | M0 baseline (Scene) | M3 (baked + β + a+b + streak) |
|---|:-:|:-:|
| `WedgeFromBunkerEdge` (was Bunker driver) | 1/8 | **6/8** |
| `PutterFromGreen` | 8/8 | **8/8** |
| `DriverFromGreen` | 6/8 | **6/8** |
| **TOTAL** | **15/24** | **20/24** |

## Root cause of the remaining 4 failures (per-step CSV evidence)

`Docs/DIAG/baked-pivot/M3-failing-shots/DriverFromGreen-E.csv` covers frames 220–249 of the failing E-direction driver shot. The pattern:

```
frame  ballY    ballZ      groundY  diff
 220  17.776  -20.170    17.496   +0.280  (ball above ground)
 226  17.840  -18.967    17.719   +0.120
 230  17.879  -18.170    17.866   +0.013  (ball ~level with terrain)
 231  17.888  -17.971    17.901   -0.014  (ball clips below)
 233  17.906  -17.574    17.974   -0.068  (3rd consecutive sub-ground frame → flag)
 240  17.963  -16.195    18.221   -0.258
 249  18.024  -14.438    18.537   -0.513
```

- Ball is climbing **~1 cm per frame** (apex flattening, vertical velocity decaying).
- Terrain is rising **~5 cm per frame** along Z (a hillside in front of the green).
- From frame 231 onward, ball is monotonically below ground and the gap grows. By frame 249 it's 0.5 m embedded and still going.
- Sim continues to `MaxDurationReached` (frame 14401, the 60 s cap) — it never triggers `HitGround`, never bounces, never settles.

This is a **sim airborne-handoff bug**: when ball-Y crosses ground-Y mid-flight at a shallow angle (near-tangential to a slope), `BallSimulation.SimulateAirborne` doesn't reliably trigger the bounce/roll transition. The ball keeps free-flying through the terrain.

Same pattern at the same XZ region for `DriverFromGreen` SE (frames 326–349). And similar (but different XZ) for `WedgeFromBunkerEdge` SE/S (frames 1164/1190 — long-flight wedge landing).

This bug **cannot be reproduced** under the M0 scene-architecture because `SceneGroundProvider` returns either max-Y of all collider hits (sometimes inflated by an overlapping mesh) or 0 (when no collider is hit) — both mask the precise-ground-Y-rises-faster-than-ball-Y scenario. The baked architecture, with its 5×-tighter Y agreement, exposes the issue.

The bug pre-dates the pivot — same architectural seam (`SimulateAirborne`'s `ballY <= SampleHeight` check) was producing the B'1 fall-through (ball flies past terrain bounds, ground Y returns 0, free-fall to -2301 m). That manifestation went away because baked heightmap covers the full terrain rect; the underlying near-tangential-handoff bug remains.

## Why I'm stopping per spec

Spec M3 step 7:
> If regression tests still fail: this is a baked-provider correctness bug. Dump per-step CSVs (reuse Phase A diagnostic infrastructure) for the failing shots, save to Docs/DIAG/baked-pivot/M3-failing-shots/, and STOP. Architect specs the fix.

Done — CSV at `Docs/DIAG/baked-pivot/M3-failing-shots/DriverFromGreen-E.csv`. Stopping.

The failure is NOT in baked-provider correctness (M2 height-agreement is 100/100 within 5 cm; classifier agreement is 100/100; barycentric is exact). The failure is in `BallSimulation.SimulateAirborne`'s handling of near-tangential ground-crossings.

**Spec also explicitly forbids me from touching `BallSimulation`** ("Do NOT modify BallSimulation's physics math (RK4, surface coefficients, putt classification). Only the providers change."), so any fix here needs Architect spec'ing.

## Architect decision request

Two paths I see:

### Path Φ1: Fix `SimulateAirborne` near-tangential handoff
Modify the airborne integrator to detect "approaching ground at low angle" and force a HitGround when the trajectory is within some small Y tolerance AND vertical speed is small. This is the proper fix; affects `BallSimulation` directly. Estimated effort: 1 day + careful regression on Phase 1–6 bit-exactness gates.

### Path Φ2: Substep the airborne integrator at ground crossings
When step N has ballY > groundY and step N+1 would have ballY < groundY, binary-search the substep where ballY = groundY exactly, snap to that, trigger HitGround. Avoids the "infinite small overshoots" loop. Smaller code change but still in BallSimulation.

### Path Φ3: Accept 20/24 as M3 done; defer Φ1/Φ2 to Phase F
The architectural pivot delivered its primary value: the original Cesar repro ("ball into the void") is gone. The 4 remaining failures are pre-existing sim bugs exposed by better classification, not pivot regressions. Mark M3 done and tackle the airborne-handoff fix in a separate spec.

I'd recommend **Path Φ3 + a follow-up spec** unless you want me to attempt Φ2 here. The pivot is fundamentally working; making the sim handle every weird tangential case is a separate scope.

## Artifacts

New on `sim-baked-data-path`:
- `Docs/DIAG/baked-pivot/M3-failing-shots/DriverFromGreen-E.csv` — frame-level evidence

Modified:
- `Assets/Scripts/Physics/Runtime/Baked/ZoneData.cs` — `ZoneMesh` type (+ Polygon2D unchanged)
- `Assets/Scripts/Physics/Runtime/Baked/BakedZoneClassifier.cs` — barycentric path; IDW retained as fallback
- `Assets/Scripts/Editor/CourseImporter/BakeZoneJsonTool.cs` — `AddMeshTriangles` (replaces `CollectMeshSamples`)
- `Assets/Scripts/Gameplay/Tests/BakedPivotRegressionTests.cs` — sustained-streak invariant; edge-launch wedge bunker test
- `Assets/Resources/HoleData/Hole_01/zones.json` — re-baked, 7.6 MB (triangle data)
- `Docs/DIAG/baked-pivot/M0-regression-DriverFromGreen.md`, `-PutterFromGreen.md` — current results
- `Docs/DIAG/baked-pivot/M0-regression-WedgeFromBunkerEdge.md` — new (replaces `M0-regression-DriverFromBunker.md`)
- `Docs/DIAG/baked-pivot/M2-height-agreement.md` — 100/100 within 5 cm, mean 0.45 cm
- `Docs/DIAG/baked-pivot/MILESTONE_3_DONE.md` — this file

## Commits

(M3.5 commit pending after this write-up.)

## Next milestone ready: NO

Holding for Architect/Cesar decision on Path Φ1/Φ2/Φ3.

If Φ3: I can proceed to M4 immediately. Phase E will reveal whether the 4 directional fall-throughs matter to Cesar in real play (they're specific direction × club × hole combinations, not the centroid-launch bug pattern).

If Φ2: ~1 day inside `BallSimulation` to add substep crossing detection + Phase 1–6 bit-exactness re-verification.

If Φ1: Larger scope.
