# SPEC — Bulletproof terrain: ball must never fall through any surface

**Date:** 2026-04-25
**Status:** Active — handoff to Claude Code
**Pointer in:** `Docs/TellCode.md`

## Background

Ball falls off the green on some (not all) shots — both putts starting from the green and approach shots landing on it. Ball falls off bunkers when starting from a bunker. Root cause is almost certainly that `SceneGroundProvider.SampleHeight` uses `RaycastAll` + max-Y (the F-Hotfix placement fix), but this policy is wrong for **sim-time** ground lookups inside `BallSimulation.RunRollPhase` / `RunPuttPhase`. When a bunker ball asks for ground Y, max-Y can return the surrounding terrain collider instead of the bunker floor; when a green ball asks, max-Y can return the Unity terrain component instead of the green mesh — in both cases the ball gets snapped UP onto a surface that isn't there on the next step, then falls.

Fix: make sim-time ground sampling **surface-classification-aware**. If sim has classified the ball on Surface X, ground Y must come from a collider whose `Course.SurfaceMarker.surfaceType == X` when one exists. Airborne path stays on max-Y (overhead hit-detection needs it).

Cesar is away for ~24 hours. Code has autonomous authority to try up to **5 different fix approaches** and must run a **full stress-test pass** (~3,500 shots across all 18 holes, all surface types) before reporting done.

## Decisions (authoritative — do not re-debate)

1. **Classification wins.** Sim-time ground Y comes from the collider matching the classified surface, not max-Y. Ball on green → green Y. Ball in bunker → bunker Y. Never overruled by a higher collider.
2. **Green→fringe ~2cm step accepted** as realistic (real greens are cut shorter than collar). No smoothing.
3. **Airborne integrator stays on max-Y** — overhead hit-detection needs it.
4. **Budget: 5 fix attempts + full stress test.** Restore point mandatory before touching anything.

---

## Phase 0 — RESTORE POINT (MANDATORY FIRST STEP)

Before touching any file:

1. `git status` → confirm clean working tree. If dirty, `git stash push -u -m "pre-terrain-fix-stash"` and record the stash ref.
2. `git tag terrain-fallthrough-pre-fix` on current HEAD. Push tag: `git push origin terrain-fallthrough-pre-fix`.
3. Create `Docs/BACKUPS/terrain-fallthrough-YYYYMMDD/` folder. Copy into it the pre-modification versions of every file Code will touch. Copy BEFORE editing, every time. Minimum list:
   - `Assets/Scripts/Physics/Core/IGroundProvider.cs`
   - `Assets/Scripts/Physics/Core/BallSimulation.cs`
   - `Assets/Scripts/Physics/Runtime/SceneGroundProvider.cs`
4. After each fix attempt, commit with message `terrain-fix-attempt-N: <one-line summary>` so every attempt is individually revertable via `git reset --hard <hash>`. Do NOT squash — keep every attempt as its own commit until the final summary commit.
5. Done report MUST include: tag name, stash ref (if used), backup folder path, full list of commit hashes made during the session.

**Rollback path for Cesar:** `git reset --hard terrain-fallthrough-pre-fix` restores everything to pre-session state.

---

## Phase 1 — Baseline capture (BEFORE any fix)

Run the new B-group tests (7–11, specified below in Phase 4) against the **unmodified** code. Record pass/fail rate per test. For each failed shot, dump trajectory JSON (`time, pos, vel, surface, groundY, ballY`) to `Docs/LESSONS_TERRAIN_FALLTHROUGH_BASELINE.md`. This gives the "fixed X of Y known failures" metric.

Commit baseline artifacts before Phase 2.

---

## Phase 2 — Fix attempts (budget: 5)

**Attempt 1 — Surface-aware ground provider (primary approach).**

Extend `IGroundProvider`:
```csharp
fp SampleHeight(fp worldX, fp worldZ);
// New default-interface method, falls back to 2-arg for providers that don't override:
fp SampleHeight(fp worldX, fp worldZ, SurfaceType preferred) => SampleHeight(worldX, worldZ);
```

`SceneGroundProvider` overrides the 3-arg version using the same partition pattern F-Hotfix.B's `SurfaceSnap` uses conceptually, but with the **physics-side** SurfaceMarker — not the Course one:
- Use `Golfin.Physics.Runtime.SurfaceMarker` (declared in `SceneSurfaceProvider.cs`), with field `Type` of enum `Golfin.Physics.SurfaceType`. This is the same marker `SceneSurfaceProvider.Classify` already reads.
- Do NOT reference `Golfin.Course.SurfaceMarker` — `Golfin.Course` lives in Assembly-CSharp which `Golfin.Physics.Runtime` cannot reference. (See comment block atop `SceneSurfaceProvider.cs`.)
- `RaycastAll` from Y=500 downward.
- Partition hits by whether `collider.GetComponentInParent<SurfaceMarker>()?.Type == preferred`.
- If any preferred hits exist: return the highest Y among those.
- Else: fall back to max-Y of all hits (current behavior).

Assumption this relies on: zone mesh GOs that need explicit classification already carry a `Golfin.Physics.Runtime.SurfaceMarker` (attached during Phase 4 integration). If a green/bunker mesh lacks one, the preferred-type partition will miss it and fall back to max-Y — same as today's broken behavior for that mesh. Not a blocker for the fix; flag any such misses in the done report and Architect will route to course importer work separately.

`BallSimulation` changes:
- `RunRollPhase` step loop: after `surface = surfaces.Classify(pos.x, pos.z)`, replace the ground-sample call with `ground.SampleHeight(posNext.x, posNext.z, surface)`.
- `RunPuttPhase` same swap.
- Phase 6 `Simulate` putt-start block: use `ground.SampleHeight(x, z, originSurface)` where `originSurface = surfaces.Classify(input.origin.x, input.origin.z)`.
- `SimulateAirborne`: **DO NOT CHANGE**. Airborne wants max-Y for overhead hit-detection.

**Attempt 2 (if A1 doesn't fix all):** Add a step-size lookahead guard. Before advancing `pos` in roll/putt, also sample ground at `posNext`. If Y delta vs current pos exceeds threshold (e.g. 0.3m in one 1/240s step — a cliff the ball shouldn't step off in one tick), clamp the horizontal step to where Y-delta is within tolerance.

**Attempt 3 (if still failing):** Post-step clamp. After `posNext` is computed, if `posNext.y < groundY_of_classified_surface - 0.02`, force `posNext.y = groundY + ballRadius` AND reproject velocity onto the tangent plane. Cheap catch-all.

**Attempt 4 (if bunker-specific remains):** Inspect bunker MeshCollider config (convex flag, cooking options). If convex=true is found, the collider loses the concave depression shape — that's a likely cause. Fix is at importer level which is DO-NOT-TOUCH scope; Code stops and surfaces to Architect with the finding.

**Attempt 5 (if green-specific remains):** Collider audit — use Unity MCP to raycast at 50 random XZ points on each green of Hole 1, log every hit (collider name, SurfaceMarker type, hit Y). Save to `Docs/LESSONS_GREEN_COLLIDER_AUDIT.md`. If the Unity terrain collider is higher than the green mesh at any XZ, that's the smoking gun — heightmap depression under green is insufficient. Out-of-scope fix; Code stops and surfaces.

After each attempt: run Phase 3 tests + repeat Phase 1 baseline tests. Commit. Only move to next attempt if failures remain.

---

## Phase 3 — Unit tests (A group, EditMode)

New file: `Assets/Scripts/Physics/Tests/GroundProviderSurfacePreferenceTests.cs`

1. `SampleHeight_PreferredGreen_OverFringe` — two overlapping colliders at same XZ: green Y=10.15 (SurfaceMarker=Green), fringe-Fairway Y=10.18. `SampleHeight(x, z, Green)` → 10.15.
2. `SampleHeight_PreferredBunker_UnderTerrain` — bunker Y=8.7 (SurfaceMarker=Bunker), terrain Y=10.0 spanning same XZ. `SampleHeight(x, z, Bunker)` → 8.7.
3. `SampleHeight_PreferredNotFound_FallsBackToMaxY` — only terrain Y=10 present. `SampleHeight(x, z, Green)` → 10.0.
4. `SampleHeight_TwoArg_Unchanged` — regression: 2-arg call returns max-Y exactly as before. Protects airborne path.
5. `SampleHeight_EmptyScene_ReturnsZero` — no hits → `fp.Zero`.
6. `SampleHeight_MultipleGreens_ReturnsHighestOfPreferred` — three green colliders at Y=10.10/10.15/10.12 same XZ. Returns 10.15.

---

## Phase 4 — Integration tests (B group, PlayMode, Hole 1)

New file: `Assets/Scripts/Gameplay/Tests/TerrainFallthroughIntegrationTests.cs` (PlayMode, `[UnityTest]`, additive-load `Hole_01_Geo`).

Each test records every sim frame's `(pos.y, groundY_under_ball)`. On failure, dumps trajectory JSON + takes a screenshot via Unity MCP `screenshot-game-view`.

7. `Green_100Putts_AllStayAboveGreenY` — ball at Green_1 centroid, 100 putts, random yaw 0..2π, power 30–95%. Invariant: `ball.Y >= ground.SampleHeight(ball.x, ball.z, Green) - 0.005` every frame while surface classifies as Green.
8. `Green_50Approach_AllStayAboveGreenY` — ball at fairway 80yd from pin, 50 wedge shots varying aim. From first green-surface-frame until rest, same invariant.
9. `Bunker_50Shots_BallStaysInOrExitsCleanly` — ball in Bunker_1, 50 wedge shots. Invariant: `ball.Y >= ground.SampleHeight(ball.x, ball.z, <classified surface>) - 0.005` every frame AND final rest Y > 0.
10. `Fairway_50Shots_NoSubsurface` — 50 shots from fairway. Same invariant.
11. `AllHoles_Smoke_3ShotsPerHole` — iterate Hole 1–18, 3 shots each from tee. Assert no frame has `ball.Y < ground.SampleHeight(ball.x, ball.z, surface) - 0.010`.

---

## Phase 5 — STRESS TEST (after all fixes pass — mandatory, not optional)

Code runs this autonomously after Phase 4 is green. Total volume ~3,500 shots across all 18 holes and all surface types.

12. **`Green_1000Putts_RandomParams`** — 1000 putts, random start positions across ALL green XZ on ALL 18 holes, random yaw/power. Zero fall-through frames across all trajectories.
13. **`Bunker_500Shots_AllBunkersAllHoles`** — iterate every bunker on every hole (skip holes with 0 bunkers). 5 shots each varying club+power+aim. Zero fall-through.
14. **`Green_500ApproachLandings`** — 500 approach shots across all 18 holes, various clubs, all landing on green. Zero fall-through on landing + subsequent roll-out.
15. **`Fairway_1000Shots_AllFairwaysAllHoles`** — iterate fairways on all 18 holes, ~55 shots per hole (varying club + power + aim). Zero fall-through across airborne→bounce→roll transitions.
16. **`Rough_500Shots_AllRoughsAllHoles`** — for each hole, locate Rough surface markers and fire ~28 shots per hole. Zero fall-through.

**Rough edge case:** Rough may not exist as discrete GOs (often just splatmap base, not a mesh overlay). If Code can't find any `SurfaceMarker.surfaceType == Rough` GOs in a hole scene, fall back to "place ball at XZ where `surfaces.Classify()` returns Rough" by scanning a grid over the hole bounds. Document what Code ended up doing in the done report.

**Aggregate reporting** — output this summary table at the end:

```
Surface      | Shots | Frames checked | Fall-throughs | Min Y-gap (m) | Max Y-gap (m)
-------------+-------+----------------+---------------+---------------+---------------
Green (putt) | 1000  | ~X             | 0             | +0.xxx        | +X.xxx
Green (land) |  500  | ~X             | 0             | ...           | ...
Bunker       |  500  | ~X             | 0             | ...           | ...
Fairway      | 1000  | ~X             | 0             | ...           | ...
Rough        |  500  | ~X             | 0             | ...           | ...
```

**Min Y-gap** tells us the closest any trajectory came to a fall-through on the fixed code. If any surface shows min < 0.001m, tolerance margin is thin — flag it in the done report so Cesar can investigate.

---

## Phase 6 — Runtime debug assertion (ship ON in editor)

Add to `RunRollPhase` and `RunPuttPhase`, once per step after `pos` is finalized:

```csharp
#if UNITY_EDITOR
    float gY = ground.SampleHeight(pos.x, pos.z, surface).ToFloat();
    if (pos.y.ToFloat() < gY - 0.02f)
    {
        UnityEngine.Debug.LogError(
            $"[Terrain] Ball below surface! surface={surface} " +
            $"ballY={pos.y.ToFloat():F3} groundY={gY:F3} " +
            $"xz=({pos.x.ToFloat():F2},{pos.z.ToFloat():F2})");
    }
#endif
```

Zero runtime cost in shipping builds. Turns any future regression into a visible error.

**Tolerance tuning:** `0.02f` is the initial threshold. After stress test, if min Y-gap observed is e.g. `0.008m`, tighten threshold to `0.004f`. If min is `0.05m`, loosen to `0.03f`. Set based on data, not guessing.

---

## Files Code will touch (expected)

- `Assets/Scripts/Physics/Core/IGroundProvider.cs` — add 3-arg overload with default impl
- `Assets/Scripts/Physics/Runtime/SceneGroundProvider.cs` — implement 3-arg override
- `Assets/Scripts/Physics/Core/BallSimulation.cs` — swap 3 call sites (roll, putt, Phase 6 putt-start); add debug assertion
- `Assets/Scripts/Physics/Tests/GroundProviderSurfacePreferenceTests.cs` — new (A group, 6 tests)
- `Assets/Scripts/Gameplay/Tests/TerrainFallthroughIntegrationTests.cs` — new (B group + stress, tests 7–16)
- `Docs/BACKUPS/terrain-fallthrough-YYYYMMDD/` — pre-edit file copies
- `Docs/LESSONS_TERRAIN_FALLTHROUGH_BASELINE.md` — baseline capture + stress results
- Maybe `Docs/LESSONS_GREEN_COLLIDER_AUDIT.md` (only if Attempt 5 triggers)

## Done report requirements

- Restore point: git tag name, stash ref (if used), backup folder path, ALL commit hashes made.
- Baseline failure rates from Phase 1.
- Which attempt(s) produced the fix (e.g. "Attempt 1 resolved all Phase 1 failures; Attempts 2–5 not needed").
- Phase 3 + 4 results: must be 100% pass on tests 1–11.
- Phase 5 stress summary table (the one above), filled in with real numbers.
- Min Y-gap value and whether debug assertion threshold was tuned from 0.02f.
- Any surfaced blockers from attempts 4–5 needing Architect input.
- Files modified with line-count diff.

## Iteration budget

- Attempts 1–5 as scoped above. After attempt 5 or any out-of-scope blocker: STOP, write findings to `Docs/LESSONS_TERRAIN_FALLTHROUGH_BASELINE.md`, commit, surface in done report.
- Phase 3 tests: 2 iterations on test authoring.
- Phase 4 tests: 2 iterations (mocks + scene setup may need helpers).
- Phase 5 stress: runs to completion; if it exceeds 3 hours wall time, gate the slowest test and continue.

## DO NOT

- Do NOT modify `HoleGeoImporter.cs` or any other course importer.
- Do NOT modify `BallSimulation.SimulateAirborne` — max-Y is correct there.
- Do NOT re-bake heightmaps.
- Do NOT skip Phase 0 restore point.
- Do NOT commit without the `terrain-fallthrough-pre-fix` tag in place first.
- Do NOT touch `SurfaceMarker`, `ISurfaceProvider`, or classification logic — only the ground-Y lookup.
- Do NOT squash attempts into one commit — keep every attempt revertable.
- Do NOT proceed past Phase 4 to Phase 5 stress test unless all tests 1–11 are green.
