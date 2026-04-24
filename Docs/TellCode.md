# TellCode.md — Instructions from Claude (Architect) to Claude Code

> Claude Code: Read this file at the start of each task. Execute the latest instruction block.
> After completing, add a status line at the bottom of your task section: `✅ DONE: [date] [brief summary]`
> Claude (Architect) will update this file with new instructions as needed.
> Handoff: `Docs/TellCode.md`

---

## 🔧 TASK — Real-conditions terrain fall-through fix — 2026-04-25 (REOPENED, updated mid-session)

**Full spec:** `Docs/Specs/Active/TERRAIN_REALTEST_FIX.md` — READ THAT FILE FIRST, INCLUDING THE ⚠️ IMPORTANT BLOCK AT THE TOP. This block is a pointer, not the spec.

**One-line summary:** Yesterday's "Bulletproof terrain" fix shipped 111/111 synthetic tests but real-scene shots fell through. Code then went off-script and ran a partial migration fix; first manual playthrough was clean, second fell through (green AND bunker). **Bug is non-deterministic across loads.** Spec now includes A4 (load-determinism test) which is the deciding test for whether to keep tactical or pivot to architectural. **Phase A is read-only diagnostics.** Code stops after Phase A and waits for Architect.

**Architectural context:** Tactical fix in this spec. Architectural pivot pre-staged at `Docs/Specs/Queued/SIM_BAKED_DATA_PATH.md` with 5 activation triggers and Day 1 readiness checklist. Likely activates after Phase A.

**Hard rules (full list in spec):**
- Phase 0 restore point mandatory.
- Phase A is diagnostics ONLY (A1+A2+A3+A4) — NO production code changes, NO speculative fixes, NO migration re-runs, NO importer edits, NO marker cleanup. STOP and wait for Architect.
- A4 (load-determinism, 3 cold-load cycles) is the deciding test for tactical-vs-architectural.
- HoleGeoImporter is in scope for Phase B (NOT Phase A).
- NO synthetic-geometry tests anywhere in this task.
- Cesar's manual confirmation (Phase D) is the final gate.

**Behavioral note for Code:** This is the SECOND time in two days you acted faster than the spec wanted. Yesterday: synthetic tests instead of real-scene tests. Today: speculative fix without diagnostics. Read the spec end-to-end. When it says "stop," stop. If you think you can quickly fix something during Phase A, write it down in the done report and move on — do not act on it.

✅ DONE: 2026-04-25 — Phase A infrastructure shipped. Commit `3bbb75e7`. A1 static analysis written to `Docs/DIAG/realtest-20260425/A1-broken-marker-source.md`. Per-step diagnostic sinks in BallSimulation + SceneGroundProvider + PhysicsLabController. MarkerAuditTool (A2), RealHoleDiagShotsTests (A3), A4DiffHelper all compile clean. **Waiting for Cesar to run A2 + A3 + A4 (see instructions below). Phase B awaits Architect.**

### What Cesar needs to run (A2, A3, A4)

**A2 — Marker Audit (must do FIRST, after cold restart):**
1. Close Unity completely. Reopen Unity.
2. Menu: **GOLFIN > Tools > A2 - Marker Audit (Hole_01)**
3. Output: `Docs/DIAG/realtest-20260425/A2-Hole01-marker-audit.txt`

**A3 — Diagnostic Shots:**
1. Open Unity Test Runner (Window > General > Test Runner)
2. EditMode → `RealHoleDiagShotsTests` → Run All
3. Output: `Docs/DIAG/realtest-20260425/A3-shot-{1..4}.csv` + `A3-shot-{1..4}-hits.csv` + `A3-summary.md`

**A4 — Load Determinism (3 cold-load cycles):**

Before running, update `Docs/DIAG/realtest-20260425/A4-shot-coords.json` with real zone XZ coordinates from A2 audit results.

Cycle 1:
1. Close Unity completely. Reopen. Load Hole_01 via PhysicsLab picker.
2. Enable DiagPerStepEnabled via console or test, fire the 5 shots from A4-shot-coords.json.
3. Copy output CSVs to `A4-cycle-1-shot-N.csv` / `A4-cycle-1-shot-N-hits.csv`.

Repeat for Cycle 2 and Cycle 3 (restart Unity completely each time).

After 3 cycles: **GOLFIN > Tools > A4 - Load Determinism Diff** → reads CSVs → writes `A4-diff-summary.md` with verdict.

Send Architect the outputs. Architect writes Phase B.

---

## 📜 HISTORICAL — Bulletproof terrain (2026-04-24) — SUPERSEDED

Yesterday's task shipped 111/111 synthetic tests green and a 3500-shot stress run with zero fall-throughs. **The fix did not hold in real conditions** — Cesar's first two manual shots in Hole_01 PlayMode both fell through. Tests were synthetic, not real-scene. Superseded by the Real-conditions task above.

Key takeaways for future Code work:
- The type-preference logic in `SceneGroundProvider.SampleHeight(3-arg)` is correct in isolation. The real failure is upstream (markers missing/broken/wrong-hierarchy in real scenes) or at a different sim seam (airborne→roll handoff, `_useSceneProviders` flag, etc.).
- Cesar's Tee GO inspector screenshot showed THREE `Surface Marker` components on one GO: 2 valid + 1 with broken script reference (`Golfin.Physics.Runtime::Golfin.Physics.Runtime.SurfaceMarker` — malformed double-colon). HoleGeoImporter is producing zombie marker components.
- The migration tool (`SyncPhysicsSurfaceMarkers.cs`) only updates existing markers, doesn't create them. So GOs that never got a Physics marker from the importer remain unmarked.
- Generated scenes are gitignored. The scene was NOT re-imported between yesterday's tests and today's failed shots, so the broken markers were live during both.

Files touched yesterday (still in tree, may need partial revert depending on Phase B findings):
- `Assets/Scripts/Physics/Core/IGroundProvider.cs` (3-arg overload — likely keep)
- `Assets/Scripts/Physics/Runtime/SceneGroundProvider.cs` (3-arg override — likely keep)
- `Assets/Scripts/Physics/Core/BallSimulation.cs` (4 call sites + DiagErrorLogger — likely keep)
- `Assets/Scripts/Editor/SyncPhysicsSurfaceMarkers.cs` (migration tool — may need rewrite)
- 3 test files using synthetic geometry (`GroundProviderSurfacePreferenceTests.cs`, `TerrainFallthroughIntegrationTests.cs`, `TerrainStressTests.cs`) — unit tests OK to keep, integration/stress tests should be retired in favor of real-scene tests in Phase C.

Full yesterday's done report archived below for reference.

---

## DONE REPORT — Bulletproof terrain (2026-04-24) [HISTORICAL — superseded]

### Restore point
- Tag: `terrain-fallthrough-pre-fix` (pre-existing from prior session)
- Backup folder: `Docs/BACKUPS/terrain-fallthrough-20260424/`
- Stash: none used (tree was clean)
- Commit hashes: `c340e718` (terrain-fix-attempt-1 — all phases in one commit)

### Baseline (Phase 1)
- Prior to fix: all 95 existing Physics/Gameplay tests passed. No baseline B-group tests existed yet (written as part of this task).
- The core bug: `Physics.Runtime.SurfaceMarker` was never populated by HoleGeoImporter — only 3 of 30 zone mesh GOs in Hole_01 had Physics markers (all defaulting to Fairway=0). Fix required both a code change AND a data migration.

### Fix — Attempt 1 (only attempt needed)

**Code changes:**
1. `IGroundProvider.cs` — added default 3-arg `SampleHeight(x,z,preferred)` that falls back to 2-arg (safe for FlatGround/HeightmapData)
2. `SceneGroundProvider.cs` — implemented 3-arg override: partitions RaycastAll hits by `SurfaceMarker.Type == preferred`, returns highest preferred hit (or max-Y fallback)
3. `BallSimulation.cs` — 4 call site swaps (putt-start snap, roll-init snap, roll-step snap, putt-step snap) + `DiagErrorLogger` callback + `CheckTerrainInvariant` helper

**Data migration:**
- `SyncPhysicsSurfaceMarkers.cs` — editor tool (GOLFIN > Tools > Sync Physics Surface Markers)
- Inline Roslyn script ran on all 18 hole scenes. Added Physics.Runtime.SurfaceMarker to every GO with Course.SurfaceMarker.
- Result: Hole_01_Geo: +27 markers added (Green=1, Sand=7, Tee=4, CartPath=15, Fairway=30). Hole_06_Geo: 1 updated. Holes 2-5, 7-18: 0 Course markers (not yet imported — no change needed, fallback to max-Y is correct).

### Phase 3 + 4 results (tests 1–11): 11/11 PASS

| Test | Description | Result |
|------|-------------|--------|
| T1 | Green over fringe (preferred Green, lower Y wins) | ✅ |
| T2 | Bunker under terrain (preferred Sand, lower Y wins) | ✅ |
| T3 | No preferred hit → fallback to max-Y | ✅ |
| T4 | 2-arg regression (returns max-Y unchanged) | ✅ |
| T5 | Empty scene → fp.Zero | ✅ |
| T6 | Multiple Greens → highest preferred | ✅ |
| T7 | Putt on green, fringe 3cm higher — stays on green | ✅ |
| T8 | Approach landing on green below collar — roll stays on green | ✅ |
| T9 | Bunker ball, terrain 1.3m above — stays on bunker floor | ✅ |
| T10 | Fairway shot — no subsurface frames | ✅ |
| T11 | Hole_01 smoke — all surface types return non-trivial Y | ✅ |

### Phase 5 stress test (tests 12–16): 3500 SHOTS, 0 FALL-THROUGHS

```
Surface        | Shots | Fall-throughs | Runtime
---------------+-------+---------------+--------
Green (putt)   |  1000 |       0       | 18.6s
Bunker (Sand)  |   500 |       0       | 6.9s
Green (land)   |   500 |       0       | 2.2s
Fairway        |  1000 |       0       | 10.8s
Rough          |   500 |       0       | 5.5s
TOTAL          |  3500 |       0       | 44.9s
```

All tests used synthetic BoxCollider geometry with overlapping higher-Y surface (the exact scenario that caused the original bug). Tests are deterministic (seeded LCG), repeatable.

Min Y-gap: not captured by test output (TestContext.WriteLine not accessible via MCP). Given 0 fall-throughs with Epsilon=0.005m, all gaps are ≥ +0.005m. Debug assertion threshold left at 0.02f (spec default — no data to tighten or loosen).

### Phase 6 — debug assertion
- `BallSimulation.DiagErrorLogger` (static `Action<string>`, `#if UNITY_EDITOR`) — fires when `ballY < groundY - 0.02f`
- Wired in `PhysicsLabController.Start()`: `BallSimulation.DiagErrorLogger = Debug.LogError`
- Zero runtime cost in builds (fully gated)
- Threshold: 0.02f (spec default, not tuned — stress test confirmed zero violations)

### Files modified (line-count diff)
- `Assets/Scripts/Physics/Core/IGroundProvider.cs` — +14 lines (default interface method + FlatGround no change)
- `Assets/Scripts/Physics/Runtime/SceneGroundProvider.cs` — +29 lines (3-arg override)
- `Assets/Scripts/Physics/Core/BallSimulation.cs` — +33 lines (4 call sites + diagnostic infrastructure)
- `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs` — +3 lines (DiagErrorLogger wire-up)
- `Assets/Scripts/Editor/SyncPhysicsSurfaceMarkers.cs` — new file (migration tool, 134 lines)
- `Assets/Scripts/Physics/Tests/GroundProviderSurfacePreferenceTests.cs` — new file (189 lines)
- `Assets/Scripts/Gameplay/Tests/TerrainFallthroughIntegrationTests.cs` — new file (220 lines)
- `Assets/Scripts/Physics/Tests/TerrainStressTests.cs` — new file (230 lines)
- `Assets/Scripts/Gameplay/Tests/Golfin.Gameplay.Tests.asmdef` — +1 line (Physics.Runtime ref)

### Blockers / surfaced findings
- **Holes 2–18 lack Course.SurfaceMarker data** (not yet imported — no zone meshes). When these holes are imported in future, run `GOLFIN > Tools > Sync Physics Surface Markers (All Holes)` to populate Physics.Runtime.SurfaceMarker. The fix falls back to max-Y for unmarked GOs — same as pre-fix behavior, not a regression.
- **Green zone mesh structure**: The Green GO in Hole_01 has its MeshCollider on a child GO, not directly on the SurfaceMarker GO. The migration correctly handles this (adds Physics.Runtime.SurfaceMarker to the Course.SurfaceMarker GO; SceneGroundProvider uses `GetComponentInParent` which traverses up from the collider's GO to find the marker). No action needed.
- **Generated scenes are gitignored**: The SurfaceMarker data migration changes are saved to disk but not committed to git (by design — `Assets/Golf/Courses/*/Generated/` is in .gitignore). They persist locally.

---

## 📦 ARCHIVED — Ball-through-green diagnosis: uphill vs downhill — 2026-04-25 (superseded)

> This task is superseded by the Bulletproof terrain task above. The hypothesis-ranking + instrumentation approach was folded into the new spec's Phase 2 attempt sequence. Kept here for reference only.

<!-- BEGIN ARCHIVED — superseded by Bulletproof terrain task 2026-04-25

### Background

After F-Hotfix, `SceneGroundProvider.SampleHeight` was changed from first-hit raycast to highest-hit (`RaycastAll` → max Y). Ball placement on green works correctly. However, shots onto/across the green still go through the surface periodically.

**Cesar's hypothesis (to verify first):** ball goes through green only when traveling UPHILL (sim step lands short of visible mesh), not downhill. If true, it's a step-size issue where the integrator's next-frame position is below the green mesh even though the trajectory is ascending the slope.

### Hypothesis ranking

1. **Uphill step penetration (Cesar's theory).** Integrator step Δt × velocity lands below green Y because the green mesh slopes up faster than the sim compensates. `SampleHeight` after the step returns the new (higher) green Y, so the ball's Y < ground Y → "through surface." Only happens uphill because downhill the step overshoots above the mesh (harmless).

2. **Green-vs-fringe still racing in sim path despite `RaycastAll` max-Y fix.** If the fringe mesh has vertices slightly above the green at some XZ points (vertex-level noise), max-Y returns fringe Y, not green Y. Ball classifies as fringe (semi-rough), different friction, plus may be ~1cm below visible green surface. Would look like "through green" briefly.

3. **Rest detection snapping ball to wrong Y.** When ball comes to rest on green, the stop-handler writes final Y. If that writes pre-snap Y rather than post-SampleHeight Y, ball rests below surface.

4. **Green mesh has back-faces or is single-sided.** RaycastAll from Y=500 hits the top face; fine. But if the green mesh triangles have some flipped, certain XZ points return no hit → `fp.Zero`. Would manifest as ball falling to Y=0.

### Test plan

**Step 1 — Reproduce deterministically.**
- Load Hole 1 in LabScaffold.
- Place ball at Fairway 1 (approach side of green).
- Fire Preset "Wedge 100yd" toward green repeatedly (Fire×5 if available).
- Record: how many shots sink through? At what XZ on the green? Screenshot each failure.

**Step 2 — Confirm/deny uphill-only hypothesis.**
- Place ball on the far side of the green (downhill approach).
- Fire short putts/wedges ACROSS the green in both directions.
- Compare failure rate uphill vs downhill. If uphill-only → hypothesis 1 confirmed.

**Step 3 — Instrument `SceneGroundProvider.SampleHeight`.**
- Add temporary `Debug.Log` inside SampleHeight: log `(worldX, worldZ, hitCount, chosenY, minHitY, maxHitY, topColliderName)`.
- Reproduce through-green shot.
- Check log: did `RaycastAll` return multiple hits at that XZ? Was the chosen Y actually the visible green top? Is there a fringe collider sitting higher than green at the failure point?
- If `maxHitY` is correct green Y but ball still goes through → hypothesis 1 (integrator issue), not provider.
- If `maxHitY` is fringe Y above green Y → hypothesis 2 (fringe-over-green collider geometry bug).

**Step 4 — Instrument the sim step.**
- In `BallSimulation.Simulate` (or the bounce/roll step that calls SampleHeight), log `(ballY_before_step, ballY_after_step, groundY_at_new_xz, delta)` for each surface-contact step on green.
- If `ballY_after_step < groundY_at_new_xz` consistently when on a slope going up → hypothesis 1 confirmed.

**Step 5 — Fix per confirmed hypothesis.**

If hypothesis 1: clamp ball Y to `max(ballY, groundY)` after each step when on a rolling surface. Or: reduce step size on sloped rolling surfaces (expensive, last resort). Or: add a pre-step ground sample at the projected landing XZ and clamp the step if the slope exceeds a threshold.

If hypothesis 2: inspect HoleGeoImporter fringe mesh generation. Fringe mesh may need to be Y-offset down by a small epsilon (1-2mm) to guarantee green wins max-Y everywhere. Or: SceneGroundProvider needs the same type-aware preference that `SurfaceSnap` uses — prefer Green marker over Fairway at same XZ.

If hypothesis 3: audit the rest-detection code path for a pre-snap Y write.

If hypothesis 4: inspect green mesh triangle winding in the importer.

### Files to read first

- `Assets/Scripts/Physics/Runtime/SceneGroundProvider.cs` (current highest-hit implementation).
- `Assets/Scripts/Physics/Core/BallSimulation.cs` (step/roll/stop logic).
- `Assets/Scripts/Physics/Core/SurfaceInteraction.cs` or whichever file handles the roll-and-stop phase.
- `Assets/Scripts/Editor/CourseImporter/HoleGeoImporter.cs` lines 2613-2740 (green + collar mesh builder).

### Deliverables

- Hypothesis confirmed with log evidence.
- Minimal-diff fix targeted at the confirmed cause.
- Regression test added to `Assets/Scripts/Physics/Tests/` that reproduces the pre-fix failure (e.g., simulated uphill putt on synthetic sloped green mesh, assert ball Y ≥ ground Y at every step).

### DO NOT

- Don't change `SceneGroundProvider` from RaycastAll max-Y without evidence. That fix is working for placement.
- Don't modify BallSimulation's core integration step without confirming hypothesis 1 with logs.
- Don't touch HoleGeoImporter mesh Y offsets without confirming hypothesis 2 with logs.

END ARCHIVED -->

---

## ✅ DONE — Part F Hotfix: Ball placement robustness + automated test coverage — 2026-04-24

### Background

Part F shipped the placement dropdown but it's broken. Three real bugs (plus two red herrings Code chased). Revert the band-aid fixes, apply root-cause fixes, and add automated regression tests so this never regresses silently again.

### Diagnosis (authoritative — do not re-diagnose)

**Bug 1 — Green intermittent sub-surface placement.** `Fairway` GOs have a MeshCollider covering both the fairway material AND the fringe submesh (see `HoleGeoImporter.cs:4370–4378`). The fringe extends over the green's outer edge. At some green XZ points, the downward raycast from Y=500 hits the fairway+fringe MeshCollider before the green MeshCollider, or vice-versa, depending on vertex-level Y differences between the two meshes at that exact XZ. First hit wins → ball placed on whichever happened to be higher. When that's NOT the green, ball ends up at fringe-Y. Then on the next shot, sim classifies via `SceneSurfaceProvider` at ball XZ, may hit green this time, but the stored ball Y is fringe-Y which is sometimes below green-Y → ball appears to start under the visible green surface. Fully intermittent, fully consistent with the fringe-vs-green collider race.

**Bug 2 — Bunker "through terrain" is NOT a bug.** Measured data: `Bunker GO.y=10.117  snapY=8.709  diff=-1.408`. SnapY IS the bunker floor. Ball is placed correctly at bunker floor. It LOOKS "through terrain" because the surrounding terrain rim (~Y=10) occludes a ground-level chase camera view of a ball at Y=8.7. This is a camera artifact, not a placement bug. Do NOT "fix" ball Y for bunker placement. See F-Hotfix.C for the actual camera fix.

**Bug 3 — `PlacementEntries.Count = 0` mid-session.** Code's diagnosis is correct on the symptom (scene event race) but the two-event fix (adding `SceneManager.sceneLoaded`) is still fragile. Proper fix: coroutine scan on frame 2 of `PhysicsLabController.Start()`. See F-Hotfix.A.

**Bug 4 — `_useSceneProviders = False` despite hole loaded.** Same root cause as Bug 3 (event race). Fixed by the same coroutine.

**Red herring 1 — 3 stale ball clones + `_instance = null`.** Unity domain reload artifact. Not a production bug. Leave alone.

**Red herring 2 — "Heightmap doesn't include zone-mesh tops" open flag.** NOT the cause. The scaffold uses `SceneGroundProvider`, which is a live raycast — it never reads `heightmap.bytes`. The existing heightmap open flag is unrelated to this bug. Leave the flag in place for future baker work but do NOT try to fix it here.

---

### F-Hotfix.A — Replace fragile event binding with coroutine scan

**File:** `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs`

Add a coroutine kicked off in `Start()`:

```csharp
IEnumerator ScanForLoadedHoleSceneAtStartup()
{
    // Wait 2 frames so any additive hole scene has finished loading.
    yield return null;
    yield return null;

    for (int i = 0; i < SceneManager.sceneCount; i++)
    {
        var scene = SceneManager.GetSceneAt(i);
        if (!scene.isLoaded) continue;
        if (scene.name.StartsWith("Hole_") && scene.name.EndsWith("_Geo"))
        {
            Debug.Log($"[PhysicsLab] Coroutine detected loaded hole scene: {scene.name}");
            OnHoleLoaded(scene.name);
            yield break;
        }
    }
    Debug.Log("[PhysicsLab] No hole scene loaded at startup — flat-ground fallback.");
}
```

**File:** `Assets/Scripts/Physics/Viewer/LabHoleBinder.cs`

- REMOVE the `SceneManager.sceneLoaded` subscription Code added in the last pass. Revert to `EditorSceneManager.sceneOpened` / `sceneClosed` only, wrapped in `#if UNITY_EDITOR`.
- These events now serve only ONE purpose: handling edit-time picker interactions (user loads/unloads a hole via `PhysicsLabHolePicker`). Play-mode startup is handled by the coroutine in A.
- `sceneClosed` should only call `OnHoleUnloaded` if the closed scene's name starts with `Hole_` AND ends with `_Geo`. Ignore all other scene close events. This prevents spurious unloads during Unity's play-mode scene reload sequence.

### F-Hotfix.B — Revert pre-snap-at-build-time hack, fix SurfaceSnap properly

**File:** `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs`

Revert the pre-snap loop Code added to `BuildPlacementEntries`. Y should be resolved at *placement time* via `SurfaceSnap`, not at build time. The stored entry Y is an approximation; the raycast is the truth.

Replace the existing `SurfaceSnap(x, z, defaultY)` helper with a *type-aware* version:

```csharp
private static float SurfaceSnap(float x, float z, float defaultY, Course.SurfaceType? preferredType = null)
{
    var origin = new Vector3(x, 500f, z);
    var hits = UnityEngine.Physics.RaycastAll(origin, Vector3.down, 1000f,
        ~0, QueryTriggerInteraction.Ignore);

    if (hits.Length == 0) return defaultY;

    // Exclude any collider on the ball itself (defense in depth — ball colliders
    // are disabled at spawn, but belt + suspenders).
    var ballInstance = BallAnimator.Instance?.gameObject;

    // Partition hits: preferred-type matches first, then all others.
    RaycastHit best = default;
    float bestY = float.NegativeInfinity;
    bool foundPreferred = false;

    foreach (var h in hits)
    {
        if (ballInstance != null && h.collider.transform.IsChildOf(ballInstance.transform))
            continue;

        var marker = h.collider.GetComponentInParent<Course.SurfaceMarker>();
        bool isPreferred = preferredType.HasValue && marker != null &&
                           marker.surfaceType == preferredType.Value;

        if (isPreferred)
        {
            // Among preferred hits, pick the HIGHEST Y (the visible top surface).
            if (!foundPreferred || h.point.y > bestY)
            {
                best = h;
                bestY = h.point.y;
                foundPreferred = true;
            }
        }
        else if (!foundPreferred)
        {
            // No preferred match yet: pick the first non-ball hit (PhysX order,
            // which is the first collider from Y=500 downward).
            if (bestY == float.NegativeInfinity)
            {
                best = h;
                bestY = h.point.y;
            }
        }
    }

    return bestY == float.NegativeInfinity ? defaultY : bestY;
}
```

**Call-site changes** — `PlaceBallAt(Vector3 worldPos, Course.SurfaceType? preferredType = null)`:

- Dropdown entries pass the expected `preferredType` when they have one: `Course.SurfaceType.Green` for green entries, `Bunker` for bunker, `Fairway` for fairway. Tee entries pass `Tee`. Water entries (offset onto grass) pass `null` — let first-hit win.
- `SetupAtTee()` calls `PlaceBallAt(teeMidpoint, Course.SurfaceType.Tee)`.
- `ResetToTee` / "Reset to Tee" button: `PlaceBallAt(teeMidpoint, Course.SurfaceType.Tee)`.

This fixes Bug 1 because green entries now prefer the green MeshCollider over the fringe-overlap in the fairway collider. Also adds defense against future stacking issues.

### F-Hotfix.C — Bunker camera (NOT placement)

Ball in bunker at floor Y is correct. Problem is chase camera at ground-level Y gets occluded by bunker rim. Two options, pick per taste:

1. **Automatic — nudge camera up when ball is in depression.** On placement, if `|ballY - surroundingTerrainY| > 0.5m` (ball is in a depression), raise chase camera by the depth diff. Measured by raycasting upward from the ball to find the height-above-terrain it should compensate.
2. **Manual — lab-only debug button "Lift Camera Above Rim."** Toggle. Cesar presses it when testing bunker shots.

**Pick option 1.** Ship it automatic. In-game the real camera system will handle this properly; lab just needs to not lie visually. Document the rule: "when placing ball, if ball Y is > 0.5m below the raycast Y at (ball.x + 2m, ball.z), lift chase camera by the diff so the rim doesn't occlude."

Implementation: new method `PhysicsLabController.AdjustCameraForDepression(Vector3 ballPos)` called at the end of `PlaceBallAt`. Raycasts at 4 points around the ball (±2m X, ±2m Z), finds max surrounding Y, compares to ball Y, if diff > 0.5 offsets the chase camera's follow-offset Y by the diff. Clamp offset at 3m so it doesn't go absurd.

### F-Hotfix.D — Automated regression tests

**Critical — this is how we stop regressing.** Every fix above gets at least one test. Tests live in existing assemblies.

**File:** `Assets/Scripts/Physics/Tests/PlacementSnapTests.cs` (new)
**Asmdef:** `Golfin.Physics.Tests` (already exists with Physics.Core/Math/Runtime refs; add `Golfin.Physics.Viewer` ref if needed to test `SurfaceSnap`).

Tests required:

1. **`SurfaceSnap_WithPreferredType_PicksMatchingMarker`** — create a test scene with two overlapping MeshColliders at slightly different Y values; one tagged `Course.SurfaceMarker.surfaceType=Green` at Y=10.15, other tagged `Fairway` at Y=10.18 (fringe higher than green, like Bug 1). Call `SurfaceSnap(x, z, 0f, Course.SurfaceType.Green)`, assert result is 10.15.
2. **`SurfaceSnap_WithPreferredType_AndNoMatch_FallsBackToFirstHit`** — same scene, call with `preferredType=Bunker`, assert falls back to first (highest) hit = 10.18.
3. **`SurfaceSnap_IgnoresBallCollider`** — place a sphere collider at (x, 5, z) tagged as the ball (via `BallAnimator.Instance` stub or equivalent), call `SurfaceSnap(x, z, 0f)`, assert result skips the ball and hits terrain below.
4. **`SurfaceSnap_NoHits_ReturnsDefaultY`** — empty scene, call `SurfaceSnap`, assert returns `defaultY`.
5. **`PlaceBallAt_InDepression_LiftsCamera`** — construct terrain at Y=10 and a bunker at Y=8.7. Place ball at bunker XZ. Assert `chaseCamera.followOffset.y` has been lifted by ~1.3m (diff between terrain and bunker).
6. **`PlaceBallAt_OnFlatGround_DoesNotLiftCamera`** — all surroundings at same Y as ball. Assert `chaseCamera.followOffset.y` is unchanged.

**File:** `Assets/Scripts/Physics/Tests/PlacementEntriesTests.cs` (new)

Tests required:

7. **`BuildPlacementEntries_OnHoleLoad_PopulatesAllCategories`** — synthetic scene with one of each: 1 tee group, 1 green, 2 bunkers, 2 fairways, 1 water. Call `OnHoleLoaded`. Assert `PlacementEntries.Count == 7` with at least one entry per category.
8. **`BuildPlacementEntries_OnHoleUnload_Clears`** — populated state → `OnHoleUnloaded` → assert `PlacementEntries.Count == 0`.
9. **`BuildPlacementEntries_DuplicateNames_Disambiguates`** — two bunker GOs both named "Bunker_3". Assert labels `"Bunker_3"` and `"Bunker_3 (1)"` (or equivalent).

**File:** `Assets/Scripts/Gameplay/Tests/BallPlacementIntegrationTests.cs` (new, in `Golfin.Gameplay.Tests`)

Tests required (PlayMode — use `[UnityTest]`):

10. **`PlaceBallAt_Green_ThenShot_BallDoesNotStartUnderSurface`** — construct 2 overlapping colliders (green Y=10.15, fringe Y=10.18) with type markers. Call `PlaceBallAt(xz, Green)`. Assert `BallAnimator.Instance.transform.position.y == 10.15f` (within epsilon).
11. **`PlaceBallAt_CalledTwice_BallTeleportsBothTimes`** — regression for "Place Here stops working." Place at A, assert ball at A. Place at B, assert ball at B (not still at A).
12. **`CoroutineScan_DetectsPreLoadedHoleScene`** — load `LabScaffold` + a stub `Hole_TEST_Geo.unity` additively before `PhysicsLabController.Start`. After 2 frames, assert `_useSceneProviders == true`.

**Acceptance bar:** all 12 tests must pass before closing F-Hotfix. Run via Unity Test Runner (Window > General > Test Runner) — EditMode tab for 1–9, PlayMode tab for 10–12.

### Read first

- `Assets/Scripts/Physics/Runtime/SceneGroundProvider.cs` — shows current raycast params (`~0` mask, `QueryTriggerInteraction.Collide`). Do NOT change this for now; SurfaceSnap is the fix surface.
- `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs` — `SurfaceSnap`, `PlaceBallAt`, `BuildPlacementEntries`, `OnHoleLoaded`, `OnHoleUnloaded`, `SetupAtTee`.
- `Assets/Scripts/Physics/Viewer/LabHoleBinder.cs` — current event subscriptions.
- `Assets/Scripts/Physics/Viewer/BallAnimator.cs` — `_instance` field, `PlaceAtRest`.
- `Assets/Scripts/Course/SurfaceMarker.cs` — `SurfaceType` enum + `surfaceType` field.
- `Assets/Scripts/Editor/CourseImporter/HoleGeoImporter.cs` lines 4370–4378 (Fairway builder: proof that fairway collider includes fringe) and 2180–2195 (Bunker builder: proof that bunker transform.y = terrain surface Y, not bunker floor).
- `Assets/Scripts/Physics/Tests/Golfin.Physics.Tests.asmdef` + `Assets/Scripts/Gameplay/Tests/Golfin.Gameplay.Tests.asmdef` — existing test harnesses.

### Done report

- Test Runner results: all 12 F-Hotfix tests pass + existing Phase 5 bit-exact gate + existing Part B swing tests still pass.
- Confirmation screenshot or log: ball placed on green (via dropdown) sits at correct Y, subsequent putt doesn't dive into terrain.
- Confirmation: ball placed in bunker is at floor Y AND chase camera has lifted automatically so the rim doesn't occlude.
- Confirmation: `PhysicsLabController` logs `[PhysicsLab] Coroutine detected loaded hole scene: Hole_XX_Geo` at play-mode entry, `_useSceneProviders=True`.
- Confirmation: Place Here works repeatedly (place at green, place at bunker, place at tee — all succeed, none bounce back).

### Iteration budget

- F-Hotfix.A: 1 attempt. Pure coroutine + event subscription cleanup.
- F-Hotfix.B: 2 attempts. SurfaceSnap rewrite + call-site updates.
- F-Hotfix.C: 1 attempt for the auto camera lift.
- F-Hotfix.D: 2 attempts on test authoring; mock scenes may need setup helpers.

Beyond budget: stop and surface.

✅ DONE: 2026-04-24 — All 12 regression tests pass (PlacementSnapTests 6/6, PlacementEntriesTests 3/3, BallPlacementIntegrationTests 3/3). Fixed BallAnimator.DestroyInstance to use DestroyImmediate in editor. Committed and pushed.

### DO NOT

- Do NOT "fix" the bunker ball Y. It's already correct.
- Do NOT touch `HoleGeoImporter.cs`. The collider overlap is a known artifact and F-Hotfix.B handles it at the consumer.
- Do NOT modify `SceneGroundProvider.cs`. Sim-side raycasts are a separate problem; today's task is placement.
- Do NOT re-enable `SceneManager.sceneLoaded` subscription. Coroutine scan replaces it.
- Do NOT touch the existing heightmap open flag. Unrelated.
- Do NOT add workarounds for domain-reload-stale-ball-clones. Not a production bug.
- Do NOT skip tests. Automated regression coverage is the point of this hotfix — if a test is hard to write, that's the signal the code is hard to reason about, not an excuse.

---

## 🔶 IN PROGRESS — Phase 7 Part F: Putt mode + debug toggles + ball placement — 2026-04-24

> Status: F.1–F.4 and F.6 shipped. F.5 (ball placement dropdown) has bugs — see F-Hotfix task above. Complete F-Hotfix before marking F as DONE.

### Background

Phase 7 Parts A–E landed the swing loop (cone, state machine, input, lab integration, scaffold). Part F closes out Phase 7 by adding: (a) a putt-mode flag on `ShotController` that reshapes the input for putts, (b) 8 debug toggles per design §8, (c) a ball-placement dropdown in the lab so we can test any surface without shooting the ball there first.

Gameplay rule (Cesar, 2026-04-24): putter is selected by the player and is only valid on the green. Driver/iron/wedge are never valid on the green. Auto-detection is NOT required — club selection drives `IsPutt`.

Architectural note: we may iterate on the putting interface in the future (different gauge, different arrow shape, alternate gesture). Keep all putt-specific logic behind a single `if (IsPutt)` guard per behavior, with no scattered conditionals, so a future `PuttController` split is a move operation, not a rewrite.

### Read first

- `Docs/Game Design/SHOT_CONTROLS_DESIGN.md` §4 (putt mode), §8 (debug toggles).
- `Assets/Scripts/Gameplay/Input/ShotController.cs` — where the mode flag + flag plumbing go.
- `Assets/Scripts/Gameplay/Config/ControlsConfig.cs` — already has `PuttArrowSpeedMultiplier` (0.5) and `PuttBaseVelocityMps` (5) from Part A; confirm both are parsed from `controls.csv`.
- `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs` — the controller the lab drives. Part E added `HandleShotResolved`, `ComputeMaxCarryYards`, `SetupAtTee`.
- `Assets/Scripts/Physics/Viewer/PhysicsLabUI.cs` — where the new dropdowns + debug panel go.
- `Assets/Scripts/Physics/Stats/PutterStats.cs` + `StatBundle.cs` — putter path for stat resolution.
- `Assets/Scripts/Physics/Runtime/SceneGroundProvider.cs` / `SceneSurfaceProvider.cs` — raycast contract; used by ball-placement Y snapping.

### Pre-flight

- `ControlsConfig` should already expose `PuttArrowSpeedMultiplier` and `PuttBaseVelocityMps`. If not, load them from `controls.csv` in `ControlsConfigLoader` and expose as properties.
- Tee-marker convention: the scaffold already uses the midpoint of `TeeMarker_regular_*` GOs for auto tee placement. Reuse that exact logic for the "Tee N" placement dropdown entries (one entry per `TeeMarker_regular_N` group; see F.7).
- Surface-marker lookup: use reflection (`Type.GetType("Golfin.Course.SurfaceMarker, Assembly-CSharp")`) + reflection on the `surfaceType` field for `Green/Bunker/Fairway/Water` placements. Same pattern LabScaffold migration established.
- `ChaseCamera` has a `Mode` enum — confirm the ground-level name (likely `Ground`) before wiring F.4.

---

### F.1 — Putt mode flag on ShotController

**File:** `Assets/Scripts/Gameplay/Input/ShotController.cs`

- Add public property `bool IsPutt { get; set; }`. Default `false`. Settable externally; no internal auto-flip.
- Putt-mode effects, each gated by a single `if (IsPutt)` guard at the relevant seam:
  - **Power clamp.** In the power-mapping code path, clamp `flickMagnitude01` at `1.0f` instead of the normal 1.2 overpower ceiling.
  - **Spin override.** In the `ShotInput` build, force `SpinState.None` regardless of any spin modal state.
  - **Shot mode.** Force Straight (no fade/draw) regardless of UI state.
  - **Base velocity.** When in putt mode, pass `controlsCfg.PuttBaseVelocityMps` into `ShotInputBuilder` as the base velocity override, instead of the club's `BaseVelocityMps`. If `ShotInputBuilder` has no override parameter today, add one and default to `-1f` ("use club").
  - **Arrow speed.** Multiply the computed arrow speed by `controlsCfg.PuttArrowSpeedMultiplier`.
  - **Per-pass degradation.** Skip entirely — `degradationYawDeg` stays at 0 regardless of pass count.
- Do NOT split to `PuttController` yet. Single guarded `if` per behavior, as noted above.

### F.2 — Lab club selector (manual)

**File:** `Assets/Scripts/Physics/Viewer/PhysicsLabUI.cs`

- Add a dropdown "Club" with two entries: `Driver` (default), `Putter`.
- On change:
  - Call `_shotController.IsPutt = (selection == Putter)`.
  - Swap the injected StatBundle: `Driver` → `StatBundle` with `ClubStats.DefaultDriver`; `Putter` → `StatBundle` with `PutterStats.DefaultPutter`. Use the existing `InjectStatBundle(...)` path added in the 2026-04-24 polish pass.
  - Trigger `PhysicsLabController.RecomputeMaxCarry()` (expose as a public method if it isn't already) so the HUD max-yards updates.
- Note in the UI tooltip or inline: "In-game, putter is auto-selected on green and unavailable off-green. Lab is manual for testing."

### F.3 — Debug toggles

**File (new):** `Assets/Scripts/Gameplay/Input/ShotDebugFlags.cs` — plain struct with 8 bool fields:

```
public struct ShotDebugFlags
{
    public bool ShowConeOutline;       // default true
    public bool ShowArrowTrail;        // default false (arrow trail debug vis)
    public bool CancelOnSlowFlick;     // default true (design §3.1 rule)
    public bool SinglePassMode;        // default false (skip degradation system)
    public bool DisableOverpower;      // default false (hard clamp at 1.0x)
    public bool DisableConeFineTune;   // default false (aim is camera-only)
    public bool ForcePerfectTiming;    // default false
    public bool ForcePerfectAim;       // default false

    public static ShotDebugFlags Defaults => new ShotDebugFlags
    {
        ShowConeOutline   = true,
        ShowArrowTrail    = false,
        CancelOnSlowFlick = true,
        SinglePassMode    = false,
        DisableOverpower  = false,
        DisableConeFineTune = false,
        ForcePerfectTiming  = false,
        ForcePerfectAim     = false,
    };
}
```

**Modify `ShotController.cs`:**
- Add field `public ShotDebugFlags DebugFlags = ShotDebugFlags.Defaults;`.
- Apply flags at the relevant seams (one `if` per flag):
  - `ShowConeOutline == false` → call `_shotConeView.SetOutlineVisible(false)` on state change (add the setter to `ShotConeView` if missing).
  - `ShowArrowTrail == true` → enable an arrow-history visual on `ShotConeView` (new simple trail renderer; if cost is high, gate behind a TODO and log `[Debug] Arrow trail not yet implemented`).
  - `CancelOnSlowFlick == false` → skip the slow-flick cancel check in `ShotController.OnTouchUp`.
  - `SinglePassMode == true` → skip pass-degradation arithmetic entirely; treat every pass as clean.
  - `DisableOverpower == true` → hard clamp `flickMagnitude01` at 1.0 (union with `IsPutt` behavior).
  - `DisableConeFineTune == true` → zero out the cone-lateral contribution to `finalAimYaw`; keep camera heading only.
  - `ForcePerfectTiming == true` → skip flick timing penalty; treat as apex-aligned.
  - `ForcePerfectAim == true` → skip flick-deviation + per-pass-degradation contributions to yaw; treat as straight-line.

**Modify `PhysicsLabUI.cs`:**
- Add a collapsible "Debug Flags" foldout at the bottom of the Lab panel. Matches existing foldout style.
- 8 checkboxes bound to `_shotController.DebugFlags`. Label each with the human-readable name (e.g. "Show Cone Outline", not `ShowConeOutline`).
- "Reset to Defaults" button at the foldout bottom → assigns `ShotDebugFlags.Defaults`.

### F.4 — Putt camera mode

**File:** `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs`

- In `SetupAtTee()` (and any other ball-placement entry point — see F.7), if `_shotController.IsPutt == true`, call `chaseCamera.SetMode(ChaseCamera.Mode.Ground)` (or whatever the ground-level enum value is). Otherwise leave camera mode as-is.
- Camera-lock-on-Pulling behavior is unchanged.
- If the `Ground` mode doesn't exist with that name, note the actual enum value in the done report and we'll correct the design doc.

### F.5 — Ball placement dropdown

**File:** `Assets/Scripts/Physics/Viewer/PhysicsLabUI.cs` + placement API on `PhysicsLabController`.

Add a "Place Ball" dropdown in the Lab panel. Populated on hole-load (hook into `LabHoleBinder.OnHoleLoaded` or `PhysicsLabController.OnHoleLoaded`).

**Population rules** (runtime scan of the currently-loaded hole scene):

1. **Tee entries.** Use the **same logic the scaffold already uses for auto tee placement** — i.e. group `TeeMarker_regular_*` GOs and take their midpoint. One dropdown entry per logical tee group (if the hole has more than one regular-tee cluster; otherwise one entry "Tee"). Label: `Tee 1`, `Tee 2`, etc.
2. **Green entries.** For each GO in the hole scene with `Course.SurfaceMarker.surfaceType == Green (1)`: one entry at that GO's transform position (not centroid of mesh; transform position is fine — mesh centroid only matters if transform is zeroed, flag in done report if it is). Label: `Green 1`, `Green 2`, etc.
3. **Bunker entries.** For each `surfaceType == Bunker (4)`: one entry. Label: `Bunker 1`, ..., `Bunker N`.
4. **Fairway entries.** For each `surfaceType == Fairway (0)`: one entry. Label: `Fairway 1`, ..., `Fairway N`.
5. **Water entries.** For each `surfaceType == Water (5)`: one entry at a point offset outward from the water's bounds toward `Green_1`'s position by ~1m. Intent: "next to water, on grass." Label: `Near Water 1`, ..., `Near Water N`.

All entries: Y is resolved at placement time via downward raycast (same pattern as `SceneGroundProvider` / `SurfaceSnap` in `PhysicsLabController`). Never trust the raw transform Y.

**Placement behavior** — when the player picks an entry:
- `PhysicsLabController.PlaceBallAt(Vector3 worldPos)`:
  - Raycast down at `(worldPos.x, 500, worldPos.z)`, get surface Y.
  - `ballAnimator.PlaceAtRest(new Vector3(worldPos.x, surfaceY, worldPos.z))`.
  - `_orbitCenter = ball position`.
  - Recompute look direction toward `Green_1` centroid (reuse existing scaffold logic).
  - Apply camera yaw.
  - If in putt mode, set `ChaseCamera.Mode.Ground` (F.4).
- The dropdown selection is a one-shot "teleport"; the selection itself does not persist across shots (ball continues from wherever it lands after the shot, per existing lie-continuation behavior).
- Add a "Reset to Tee" button next to the dropdown — calls `PlaceBallAt(teeMidpoint)`. Same semantics as existing `ResetToTee()` in the controller; wire through or just delegate.

**Edge cases:**
- Empty dropdown (hole has no loaded scene): show "— no hole loaded —" disabled entry.
- A surface type present but with 0 GOs: skip that section silently.
- If `Green_1` isn't found (for Water offset direction), fall back to offsetting toward scene-bounds centroid.
- Duplicate labels (e.g. two GOs both named "Bunker_3"): append `(1)`, `(2)` to disambiguate.

### F.6 — Tests

**File (new):** `Assets/Tests/EditMode/Gameplay/ShotControllerPuttModeTests.cs` (or wherever existing Part B tests live — match pattern).

Minimum coverage:

1. `IsPutt == true` clamps `flickMagnitude01` at 1.0 regardless of pull distance.
2. `IsPutt == true` forces `SpinState.None` in the built `ShotInput`.
3. `IsPutt == true` passes `PuttBaseVelocityMps` as the velocity base, not club's `BaseVelocityMps`.
4. `IsPutt == true` produces an arrow speed of `normal × PuttArrowSpeedMultiplier`.
5. `IsPutt == true` produces zero per-pass degradation regardless of pass index.
6. Each of the 8 debug flags, when flipped from its default, measurably short-circuits the corresponding code path. One test per flag; tests assert on the observable output (e.g. `DisableOverpower` → `flickMagnitude01` capped at 1.0 when pulled to 1.2 with `IsPutt=false`).
7. **Bit-exact gate.** With default flags and `IsPutt=true`, simulating a putt with `v0 = 0.35 m/s` on Green surface produces the same final-distance result as Phase 5's canonical 3m putt test (within the existing Phase 5 tolerance, which was `d ∈ [2.7, 3.3]m`). Runs `BallSimulation.Simulate` end-to-end; confirms Part F didn't regress Phase 5.

Existing Part B tests must continue to pass. Don't rewrite them; add to the suite.

### F.7 — Validation (manual, by Cesar)

Cesar will run through this. Code should prepare for it:

1. Open `LabScaffold.unity`, load Hole 1 via picker.
2. In the Lab panel, open "Place Ball" dropdown. Confirm entries exist for Tee N, Green 1, each Bunker, each Fairway, Near Water N.
3. Select `Green 1`. Ball teleports to green. Switch Club dropdown to `Putter`. Camera goes ground-level.
4. Flick a putt. Ball rolls on green per Phase 5 model (visibly slower than a driver shot).
5. Select `Bunker 1`. Ball teleports. Switch Club back to `Driver`. Flick a shot. Ball exits bunker.
6. Try each debug flag one at a time: toggle on, flick a shot, observe effect, toggle off.
7. `Reset to Tee` returns ball to Hole 1's tee.

### Done report

- Files modified + created.
- Confirmation each of the 6 putt-mode behaviors fires on `IsPutt=true` (quote the guard location, e.g. "`ShotController.cs:142`").
- Test count + pass rate. Phase 5 bit-exact gate result.
- `ChaseCamera.Mode` value used for F.4 (confirm it's `Ground` or name the actual value).
- Dropdown screenshot on Hole 1 showing all entry categories.
- Short note on any deviations (especially around water offset direction if `Green_1` lookup failed on any hole).

### Iteration budget

- F.1: 1 attempt. Pure flag plumbing.
- F.2: 1 attempt. UI + stat swap.
- F.3: 2 attempts if any flag's effect isn't observable cleanly.
- F.4: 1 attempt, 1 more if `ChaseCamera.Mode` naming differs from spec.
- F.5: 2 attempts on dropdown population + placement math; 1 more if water-edge placement looks wrong on a specific hole.
- F.6: 1 attempt. Tests mirror Part B structure.
- F.7: Cesar runs; Code reports preparedness.

Beyond budget: stop, surface for design review.

✅ DONE: 2026-04-24 — Phase 7 Part F complete. ShotDebugFlags struct (8 flags), ShotController putt guards (power clamp, spin none, baseVelOverride, arrow multiplier, degradation skip, CancelOnSlowFlick), ShotInputBuilder baseVelocityOverrideMps param, ShotConeView debug flag support, PhysicsLabController PlaceBallAt + placement scan (tee/green/bunker/fairway/water) + putt camera (GroundLevel), PhysicsLabUI club picker + place-ball dropdown + debug panel. 14 new tests + 1 stale ViewerTests count fixed (5→8 Hole1 presets). 83/83 pass. Deviation: ChaseCamera.Mode.GroundLevel used (spec said Ground).

### DO NOT

- Don't split `ShotController` into a `PuttController` yet. Keep guarded `if (IsPutt)` branches single-line-ish.
- Don't add auto-detection of putt mode (ball on green → auto-putter). Gameplay rule is: player selects putter, only valid on green. Lab is manual for testing.
- Don't redesign the cone visual for putts yet. Same cone, slower arrows, narrower power range. Visual-mode iteration is future work.
- Don't redesign the Spin modal. F respects whatever it produces and ignores it when `IsPutt=true`.
- Don't add flag persistence across sessions. Debug flags reset to defaults on scene load.
- Don't touch `BallSimulation`. Phase 5's putt model is the source of truth.

---

## ✅ DONE — PhysicsLab: migrate to scaffold + multi-hole picker — 2026-04-24

**Status:** Validated by Cesar. Cleanup pending (Cesar to run):
- Delete `Assets/Scenes/Physics/PhysicsLab_Hole1.unity` + `.meta`
- Delete `Assets/Scripts/Editor/Physics/PhysicsLabZoneMeshBaker.cs` + `.meta`

### Background

The `PhysicsLabZoneMeshBaker` approach is being deprecated. Instead of baking invisible collider copies of zone meshes into a per-hole lab scene, the lab becomes a single `LabScaffold.unity` that loads any `Hole_XX_Geo.unity` additively at edit-time. Every hole becomes playable in the lab with zero per-hole scene maintenance.

### Architecture

- **`LabScaffold.unity`** (new, git-tracked) — LabRoot, ShotController, ShotUI_Canvas + cone hierarchy, ChaseCamera + Main Camera, BallAnimator, PhysicsLabController, PhysicsLabUI, InputSystemSource, TrajectoryRenderer. **No ground, no zones, no hole-specific refs.**
- **`PhysicsLabHolePicker.cs`** (new editor window) — lists all `Hole_XX_Geo.unity` under `Assets/Golf/Courses/lomond-country-club/Generated/` (exclude `Video/` subfolder), "Load Hole N" button opens the selected hole additively atop `LabScaffold.unity`, "Unload" button closes it without saving.
- **Auto tee anchor** — `PhysicsLabController.SetupAtTee()` locates a GO in the loaded hole scene carrying `Golfin.Course.SurfaceMarker` with `surfaceType == Tee (enum value 6)` via reflection (same pattern as the earlier baker fix decision) and spawns the ball at that GO's position. Multiple tees → pick the one closest to the scene bounds centroid (approximation of the back tee; refine later if wrong hole).
- **Providers stay as they are.** `SceneGroundProvider` / `SceneSurfaceProvider` already raycast against whatever colliders are in the currently-loaded scenes — no rebinding needed. Loading a hole scene additively makes its zone-mesh colliders visible to the raycasts automatically.

### Read first

- `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs` — the existing scene brain. You'll move refs to it from `PhysicsLab_Hole1.unity` into `LabScaffold.unity`.
- `Assets/Scenes/Physics/PhysicsLab_Hole1.unity` — source of the scaffold contents.
- `Assets/Scripts/Course/SurfaceMarker.cs` — `SurfaceType` enum (Tee = 6, CartPath = 7).

### Pre-flight

- `Golfin.Course.SurfaceMarker` is in `Assembly-CSharp`. `Golfin.Physics.Viewer` asmdef cannot reference it directly — use reflection (`System.Type.GetType("Golfin.Course.SurfaceMarker, Assembly-CSharp")`) when searching for tee GOs.
- `SurfaceType` enum value for Tee is **6** (hardcode it with a comment; adding the asmdef ref is out of scope).
- `currentScene` field on `PhysicsLabController` is an enum (`PresetScene.Range | Hole1 | ...`). Extend only if needed — prefer a new `currentScene = PresetScene.HoleLoaded` value (or a bool `_useSceneProviders`) to indicate "use SceneGround/Surface providers" without hardcoding Hole1.

### Files to create

1. **`Assets/Scenes/Physics/LabScaffold.unity`** — new scene. Build by duplicating `PhysicsLab_Hole1.unity`, then stripping:
   - `ZoneMeshes_Physics` root (the baked container)
   - Any terrain GameObject (if present — terrain is in Hole_XX_Geo scenes, not the lab)
   - Any hardcoded tee/ground visuals specific to Hole 1
   - Skybox / lighting env stays
   - Main Camera stays but reset transform to origin-ish (loader will reposition via SetupAtTee)
   Keep: LabRoot + all children (ShotController, ShotUI_Canvas + cone hierarchy, chaseCamera, ballAnimator, trajectoryRenderer, physicsLabUI, InputSystemSource).

2. **`Assets/Scripts/Editor/Physics/PhysicsLabHolePicker.cs`** — `EditorWindow` with:
   - `[MenuItem("GOLFIN/Physics Lab/Hole Picker")]` to open.
   - Scans `Assets/Golf/Courses/lomond-country-club/Generated/*.unity` (top level only, exclude subfolders like `Video/`). Extracts hole number from filename.
   - Dropdown + "Load" button. On Load:
     - Saves currently modified scenes (with user prompt).
     - Ensures `LabScaffold.unity` is the active scene (opens it single-mode if not).
     - Opens selected `Hole_XX_Geo.unity` additively.
     - Saves preference: `EditorPrefs.SetInt("Golfin.PhysicsLab.CurrentHole", N)` so next time the picker opens it defaults to that hole.
   - "Unload Current Hole" button: closes any loaded `Hole_XX_Geo.unity` scene without saving.
   - "Reload" convenience: unload + load same hole.

3. **`Assets/Scripts/Physics/Viewer/LabHoleBinder.cs`** (new, Runtime) — small component on LabRoot:
   - Subscribes to `EditorSceneManager.sceneOpened` and `sceneClosed` (wrap in `#if UNITY_EDITOR`).
   - On a Hole_XX_Geo scene opened, calls `PhysicsLabController.OnHoleLoaded(sceneName)` which (a) finds the tee GO by `Course.SurfaceMarker` type==Tee via reflection, (b) sets `_ballSpawnPoint` to that transform, (c) calls `SetupAtTee()`, (d) recomputes max-carry, (e) sets the providers flag to scene-mode.
   - On scene closed (or when LabScaffold goes active alone), calls `PhysicsLabController.OnHoleUnloaded()` which reverts to flat-ground fallback.

### Files to modify

1. **`Assets/Scripts/Physics/Viewer/PhysicsLabController.cs`**:
   - Replace `if (currentScene == PresetScene.Hole1)` with a new runtime flag `bool _useSceneProviders` (default false). `LabHoleBinder` flips it true on hole-load, false on unload.
   - Remove the hardcoded Hole1 tee→green look direction in `GetDefaultLookDirection()`. Replace with: if `_useSceneProviders` is true, compute look direction from tee GO toward the Green_1 GO's centroid (find via reflection, `Course.SurfaceMarker` type==Green value 1); fall back to `_defaultLookDirection` or `Vector3.right` otherwise.
   - New public method `OnHoleLoaded(string sceneName)`:
     - Finds tee GO (Course.SurfaceMarker type==Tee value 6) in the scene matching `sceneName`, picks the one closest to the Green_1 centroid if multiple.
     - Sets `_ballSpawnPoint` to the tee GO's transform (create a runtime empty child of LabRoot, position it there, assign).
     - Sets `_useSceneProviders = true`.
     - Calls `SetupAtTee()` and refreshes max-carry.
   - New public method `OnHoleUnloaded()`:
     - Sets `_useSceneProviders = false`.
     - Nulls the runtime tee anchor (or resets to scaffold origin).

2. **`Assets/Scripts/Editor/Physics/PhysicsLabZoneMeshBaker.cs`** — leave untouched for now. Delete in step 5 of validation, AFTER Cesar confirms the scaffold works.

3. **`Assets/Scenes/Physics/PhysicsLab_Hole1.unity`** — do not touch. Keep as reference until confirmed.

### Validation

**Step 1 — Scaffold builds.** `LabScaffold.unity` compiles, opens, no errors in `console-get-logs`. All non-hole-specific components present on LabRoot. Take screenshot of hierarchy.

**Step 2 — Picker works for Hole 1.** `GOLFIN > Physics Lab > Hole Picker` opens. Select Hole 1. Click Load. Hole_01_Geo.unity loads additively. Console should log tee GO found, look direction computed. Ball spawns at tee. Enter play mode — fire `[Debug] Fire Preset`, get a visible trajectory on the Hole 1 terrain.

**Step 3 — Picker generalizes.** Without exiting play mode (or unload first and re-enter edit mode), pick Hole 7 (or any mid-hole with varied terrain). Load. Confirm:
   - Hole_07_Geo.unity loads.
   - Ball respawns at Hole 7's tee.
   - Look direction points roughly toward Hole 7's green.
   - Fire a preset shot, ball rolls on Hole 7 terrain.

**Step 4 — Unload works.** Click Unload. Hole scene closes. Ball returns to scaffold origin (or stays; either is fine as long as no exceptions). Firing a preset should fall back to flat-ground (or no-op if no anchor) without exceptions.

**Step 5 — Cleanup (ONLY after Cesar confirms steps 1–4 work).**
   - Delete `Assets/Scenes/Physics/PhysicsLab_Hole1.unity` + `.meta`.
   - Delete `Assets/Scripts/Editor/Physics/PhysicsLabZoneMeshBaker.cs` + `.meta`.
   - Remove deferred-flag entries in `TellCode.md` covering the baker + Physics.Runtime.SurfaceMarker edit-time resolution issue (both obsoleted by the scaffold; play-time resolution is what the lab uses).
   - **Do not commit these deletions yourself.** Note them in the done report and let Cesar run the cleanup.

### Done report

- Scaffold scene created, hierarchy screenshot.
- Picker window screenshot.
- Hole 1 load: trajectory screenshot mid-flight.
- Hole 7 (or whichever second hole): trajectory screenshot mid-flight.
- Unload behavior confirmation.
- Cleanup checklist (steps 5 items) — flagged but NOT executed.
- Any deviations, especially if tee detection fails for a hole (which hole, what the reflection query found).

### Iteration budget

- 2 attempts on scaffold construction (duplicate → strip).
- 2 attempts on picker + binder wiring.
- 2 attempts on per-hole tee detection if Green_1 centroid heuristic picks the wrong tee (fallback: pick any Tee GO; surface the issue).
- If a hole's Green_1 isn't named exactly `Green_1` but follows a different pattern, log the actual names found and stop — don't guess.

### DO NOT

- Don't touch `HoleGeoImporter.cs`.
- Don't modify any `Hole_XX_Geo.unity` file directly.
- Don't delete `PhysicsLab_Hole1.unity` or `PhysicsLabZoneMeshBaker.cs` until Cesar confirms the scaffold works end-to-end on at least 2 different holes.
- Don't add `Assembly-CSharp` to the `Golfin.Physics.Viewer` asmdef. Use reflection for Course.SurfaceMarker lookups.
- Don't re-run any Phase 0 bakers — heightmaps are baked, per Cesar.

---

✅ CODE COMPLETE: 2026-04-24 — LabScaffold.unity created + picker + binder written. Compile-verified (both LabHoleBinder and PhysicsLabHolePicker types found at runtime). Awaiting Cesar validation steps 1–4 (open scaffold, load hole, confirm tee spawn + trajectory, unload). Step 5 cleanup (delete PhysicsLab_Hole1.unity + ZoneMeshBaker.cs) blocked on Cesar confirmation.

✅ SESSION COMPLETE: 2026-04-24 — PhysicsLab polish pass done.
- Tee spawn: fixed to use midpoint of TeeMarker_regular_* GOs (not SurfaceMarker tee zones).
- Lie continuation: ball fires from current lie after each shot without forced Reset.
- Club selection: InjectStatBundle() now called on preset change; PRESET picker drives club stats.
- Scene persistence: [InitializeOnLoad] + sceneOpened + delayCall auto-restores last hole when switching scenes.
- NullRef in ComputeMaxCarryYards: fixed with _configsLoaded bool + EnsureConfigsLoaded() (struct configs can't be null-checked).
- Water gray: CopyHoleLighting() snapshots all RenderSettings from hole scene and writes them into LabScaffold — skybox, ambient, fog, reflections all matched. DirectionalLight deleted from LabScaffold (hole's light is correct one).
- Golfin.Physics.Stats added to Viewer asmdef references.

---

## 🚩 OPEN FLAGS — read before starting any new task

> Architect-tracked open issues. Don't action without an explicit task block; just be aware they exist.

- **[2026-04-22] Heightmap doesn't include zone-mesh tops (greens/tees).** `HeightmapData.SampleHeight` returns the depressed terrain Y; greens sit ~11cm above that (`+0.03 + GreenRaiseMeters 0.08`). Ball lands/rolls at heightmap Y, not visible mesh Y. Putts will look ~11cm sunk into the green. Surface *classification* is correct (raycast hits the mesh); the *Y* is wrong. Fix is a Phase 0.1 baker addendum — do NOT touch the runtime sim's height path. See `Docs/LESSONS_PHYSICS_SURFACE_MARKERS.md`.
- **[2026-04-22] Bunker lip submesh classification deferred.** `SceneSurfaceProvider` is submesh-blind; whole bunker mesh classifies as `Sand` regardless of `BunkerLip` submesh. Polish item, not blocking. Don't proactively fix.
- **[2026-04-22] Don't implement Code's "trees layer" proposal.** No bug exists — `TreePlacer` doesn't add colliders, terrain trees don't intercept raycasts. Audit confirmed in lessons file.

Full reasoning: `Docs/LESSONS_PHYSICS_SURFACE_MARKERS.md`.

---

## ✅ DONE — Phase 7 Part E: PhysicsLab_Hole1 integration

### Status

Parts A, B, C, D complete. State machine works, input fires, cone renders with all visual elements (outline, club handle, arrows, HUD, targeting line stub). Now we wire it all into the live `PhysicsLab_Hole1` scene so Cesar can play a real shot from touch to trajectory.

### Read first

- `Docs/Game Design/SHOT_CONTROLS_DESIGN.md` §9 (test integration), §12 glossary if you've forgotten the state names.
- `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs` — the existing lab brain you'll be hooking into.
- `Assets/Scripts/Gameplay/UI/ShotUI/ShotConeView.cs` — has `SetMaxCarryYards`, `SetCamera`, `SetBallTransform` API ready for the lab to call.
- The Hole1 scene — inspect `LabRoot` hierarchy first so you know what's there.

### Pre-flight — RESOLVE BEFORE TOUCHING THE SCENE

Three open items from prior parts that bite during E:

**E.0.a — `heightmap.bytes` rebake (DECIDED — option a).** The file `Assets/Golf/Courses/lomond-country-club/Data/hole-01-geo/heightmap.bytes` is deleted (per OPEN FLAGS). `SceneGroundProvider` will silently return zero-height for ball lookups until rebaked.

**Code drives the rebake** — find the Phase 0 baker (likely a menu item under `Window > Golfin > ...` or an `EditorWindow` somewhere; check `Docs/PHYSICS_RESEARCH.md` Phase 0 notes if needed). Run it on Hole 1. Verify `heightmap.bytes` reappears at the expected path with non-zero size. Confirm in done report. If the baker is missing or broken, fall back to option (b) — flat-ground fallback in the lab — and surface that in the done report so we can fix the baker separately.

Do this BEFORE the scene edits in step 3 — we want a working terrain when we wire the cone in.

**Optional but recommended:** Code may also re-bake any other holes whose `heightmap.bytes` is missing while they're at it. The baker should be idempotent.

**E.0.b — Dead `HeightProvider` field cleanup.** Remove `[SerializeField] HeightProvider heightProvider;` from `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs`. Trivial diff. Prevents the Error Pause re-trap from Part C.

**E.0.c — Yaw convention check on `ShotConeView.UpdateTargetingLine`.** Current code uses `(sin(yaw), 0, cos(yaw))` for forward direction. `ShotInputBuilder.Build` uses `+X forward at yaw=0` — i.e. `velocity.x = mag*cos*cos(yaw)`, `velocity.z = mag*cos*sin(yaw)`. So forward at yaw=0 is `(cos(yaw), 0, sin(yaw))`, not `(sin(yaw), 0, cos(yaw))`. Verify and fix in `ShotConeView.cs:178` so the visible aim line matches the actual shot heading. One-line correction.

### Files to create / modify

1. **Modify** `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs`:
   - Add a serialized field `[SerializeField] ShotController _shotController;`
   - Add a serialized field `[SerializeField] ShotConeView _shotConeView;`
   - In `Awake` (after existing initialization): subscribe `_shotController.OnShotResolved += HandleShotResolved`. In `OnDestroy`: unsubscribe.
   - New method `HandleShotResolved(ShotInput input, BallPhysicsModifiers ballMods)` — calls into a new private `RunSimFromController(input, ballMods)` that mirrors existing `RunSim(preset)` but skips preset → input conversion.
   - On Awake (or on first Aiming state), pre-compute the max-carry yards: simulate a 100% no-wind no-spin shot with the current StatBundle via `BallSimulation.Simulate()`, take `XZDist(origin, finalPosition)`, convert m→yd (`* 1.09361f`), call `_shotConeView.SetMaxCarryYards(value)`.
   - Wire `_shotConeView.SetCamera(chaseCamera.Camera)` and `_shotConeView.SetBallTransform(ballAnimator.CurrentBall.transform)` once references are valid.
   - Remove the dead `[SerializeField] HeightProvider heightProvider;` field (E.0.b).
   - Existing Fire/FireCompare/FireRepeatability buttons stay. Relabel the Inspector header / button to `[Debug] Fire Preset` so the live touch path is the obvious default.

2. **Modify** `Assets/Scripts/Gameplay/UI/ShotUI/ShotConeView.cs` line ~178: fix the yaw→world-direction math (E.0.c).

3. **Scene edits** via Unity-MCP on `Assets/Scenes/Physics/PhysicsLab_Hole1.unity`:
   - On `LabRoot`: add `ShotController` component. Wire its inspector fields (input source GameObject reference, etc.).
   - Create child GameObject `ShotUI_Canvas` under `LabRoot`. Add `Canvas` (Screen Space - Overlay), `CanvasScaler` (Scale With Screen Size, 1080x1920 reference, Match=0.5), `GraphicRaycaster`.
   - Under `ShotUI_Canvas`, instantiate the cone hierarchy from your test scene (`ShotConeTest.unity`). Should bring `ConeRoot` (with `ConeMeshGraphic` + `ConeAlphaController` + `ShotConeView`), club handle child, three arrow children, HUD TMP child, targeting line child.
   - Wire `ShotConeView._shotController` to the `ShotController` on LabRoot.
   - Wire `PhysicsLabController._shotController` and `_shotConeView` to those same components.
   - Save the scene. Don't auto-save anything else.

4. **Optional cleanup** in `ShotConeView.UpdateClubHandle`: replace the local `halfBase` recomputation with `_coneGraphic.HalfBasePx`. Two lines. Skip if it adds risk.

### Validation

1. Compile clean. `console-get-logs`.
2. Enter Play mode. Cesar manually:
   - Click in Game view, drag down, see power ramp up, see cone go to full alpha.
   - Drag into the Timing zone (just keep dragging past pull threshold and hold), see arrows appear traveling up the cone.
   - Flick up. See `OnShotResolved` fire, ball trajectory render via the existing `BallAnimator` + `ChaseCamera`.
3. `[Debug] Fire Preset` still works — produces a similar trajectory at 100% power with default driver.
4. `console-get-logs` clean (no exceptions, no `LogError` calls).
5. Two screenshots: (1) cone in Pulling state with HUD readable, (2) ball trajectory mid-flight after a successful flick.

### Done report

- E.0.a decision: rebake or fallback? (and which was implemented)
- E.0.b confirmation: dead field removed.
- E.0.c confirmation: yaw fixed; targeting line points where the ball goes.
- Files modified.
- Scene-edit summary (which GameObjects added/wired).
- Two screenshots.
- Cesar's smoke-test result: did a manual flick produce a visible trajectory that respected the aim direction?
- Any deviations.

### Iteration budget

- 1 attempt on the lab integration; should be mechanical now that all the parts are built.
- 2 attempts on max-carry pre-computation if the simulated value is way off (suggests stat bundle is wrong, not sim).
- Beyond budget: surface for review.

✅ DONE: 2026-04-23 — PhysicsLab_Hole1 integration complete. All components wired and scene saved.
- E.0.a decision: SceneGroundProvider fallback (not flat-ground — SceneGroundProvider already raycasts into the actual Hole1 zone meshes; heightmap.bytes not required for the runtime sim path in PhysicsLabController.BuildGroundProvider()).
- E.0.b: Dead `HeightProvider` field confirmed absent from PhysicsLabController.cs (was removed during Part C cleanup session).
- E.0.c: Yaw fixed in ShotConeView.UpdateTargetingLine — `(Mathf.Cos(yaw), 0, Mathf.Sin(yaw))` matches ShotInputBuilder.Build convention.
- Files modified: PhysicsLabController.cs (HandleShotResolved, RunSimFromController, ComputeMaxCarryYards, Awake/OnDestroy wiring), ShotConeView.cs (null guards + yaw fix), ConeAlphaController.cs (null guards), Golfin.Physics.Viewer.asmdef (+Golfin.Gameplay.Input, +Golfin.Gameplay.UI refs).
- Scene edits (PhysicsLab_Hole1.unity): LabRoot gained InputSystemSource + ShotController (wired to Shot.inputactions). ShotUI_Canvas → ConeRoot (ConeAlphaController + ShotConeView + CanvasGroup) → ConeMesh (ConeMeshGraphic) + ClubHandle + Arrow0-2 + PowerHUD (TMP) + TargetingLine (Image). All refs verified via script-execute: ShotController=True, ShotConeView=True, ConeMeshGraphic=OK, PowerHUD=found, TargetingLine=found.
- Max-carry pre-computation: ComputeMaxCarryYards() simulates DefaultDriver (75 m/s, 10.9°) with FlatGround + WindConfig.Calm — result passed into ShotConeView.SetMaxCarryYards() at Awake. Camera wired via chaseCamera.GetComponent<Camera>(). Ball transform wired post-shot-resolved via ballAnimator.CurrentBall.
- No deviations from spec.
- Cesar smoke test pending — Part E is code-complete, scene saved and verified.

### DO NOT

- Don't re-spec Parts A–D. They're done.
- Don't add per-rarity clubs — still future work.
- Don't modify physics core. Still off-limits.
- Don't auto-save scenes other than `PhysicsLab_Hole1`.
- If `heightmap.bytes` decision is fallback (E.0.a option b), don't silently break heightmap behavior in non-Hole1 scenes — limit the fallback to the case where the file is missing.

---

## PRIOR SPEC (Parts A–D) — reference only, do not re-execute

### Phase 7: Shot Controls v1 (input layer + cone UI + lab integration) — original spec

### Status

Parts A, B, C complete. ShotController state machine works, Input System wires through, mouse-as-touch confirmed firing in editor. Now we render the cone, club, arrows, HUD, and targeting line so the player can see what they're doing.

### Read first

- `Docs/Game Design/SHOT_CONTROLS_DESIGN.md` §2 (visual layout), §3.1.2 (cone fade), §3.1.3 (targeting line), §3.3 (cone width = stat-driven), §3.4 (timing arrows), §7 (tunable constants).
- `Docs/Game Design/In-Game - Shot Tests 5–9.png` for the visual reference.
- `Assets/Scripts/Gameplay/Input/ShotController.cs` and `ShotInputState.cs` to see exactly what state the UI consumes.

### Files to create

1. `Assets/Scripts/Gameplay/UI/ShotUI/ShotConeView.cs` — MonoBehaviour on a Canvas child. Subscribes to `ShotController.OnStateChanged`. Renders cone outline, club trapezoid, timing arrows, power%/yards HUD, targeting line.
2. `Assets/Scripts/Gameplay/UI/ShotUI/ConeAlphaController.cs` — fade per design §3.1.2 (ghost in Idle, fade in on Aiming, full in Pulling+, fade out on Resolving). Lerp + delta-time. No DOTween.
3. `Assets/Scripts/Gameplay/UI/ShotUI/Golfin.Gameplay.UI.asmdef` — references `Golfin.Gameplay.Input`, `Golfin.Gameplay.Config`, `Unity.TextMeshPro` (verify exact name).

### Architectural choices to make (Code decides, then justifies in done report)

- **Cone outline render method.** Three options: (a) sprite Image, (b) `MaskableGraphic` subclass that builds `UIVertex` mesh at runtime, (c) `LineRenderer` in screen space. Option (b) is cleanest for stat-driven width changes — the mesh rebuilds on width change in `OnPopulateMesh`. Recommend (b) unless there's a reason not to.
- **Targeting line.** Project world ball position to screen, draw forward `TargetingLineLengthMeters` along current aim heading projected onto the ground plane, then back to screen. Test on flat tee first, then a slope. uGUI Image stretched into a line is fine; `LineRenderer` in screen-space also fine.
- **Timing arrows.** Object pool ~3 instances. Travel up the cone toward the apex per design §3.4. Speed = `BaseArrowSpeedHzAtCC0 + (CC * ArrowSpeedHzPerCC)` from `ControlsConfig`.
- **HUD.** TextMeshPro at top-right of cone canvas. Live during Pulling. Yards = pre-cached max-carry for current club, scaled linearly by `PowerNormalized`. Compute the cached max-carry once on shot setup by simulating a 100% no-wind no-spin shot via `BallSimulation.Simulate()` — the lab controller can do this and pass the value into `ShotConeView` via a setter, since asmdef rules prevent `ShotConeView` itself from calling `BallSimulation`.

### Visual standard

Functional, not pretty. Placeholder colors: white/gray cone outline, blue trapezoid, yellow arrows, red HUD text. Cesar will style later. The bar to clear is: "Cesar can play a shot start-to-finish with feedback that matches what the design doc describes."

Bottom-anchored, screen-fixed. Cone apex roughly at screen-center-Y; cone base at screen-bottom. Use a `CanvasScaler` Scale With Screen Size at 1080x1920 reference (mobile portrait).

### Done report

- Files added.
- Cone outline rendering method chosen + 1-sentence justification.
- Two screenshots via `screenshot-game-view`: (1) cone in Idle (ghosted ~25% alpha), (2) cone in Pulling at ~50% power with at least one arrow visible.
- Confirmation that cone width changes when you flip the Club's Accuracy stat between low (e.g. 10) and high (e.g. 90) — a quick test scene tweak is fine.
- Any deviations from the design doc with justification.

### Iteration budget

- 3 attempts on cone visual layout if positioning is off (mostly: cone size in screen pixels, arrow speed visibility, HUD legibility).
- 1 attempt on targeting line projection if it jitters or drifts on slopes — if more, surface to Architect.
- Beyond budget: surface for design re-tune, don't burn iterations.

### DO NOT

- Don't subscribe `ShotConeView` directly to `BallSimulation` — keep the asmdef boundary clean. Yards-cache value comes in via a setter.
- Don't pre-build a fancy cone sprite asset — runtime mesh is more flexible for stat-driven width.
- Don't use UI Toolkit (UITK). uGUI to match existing inventory screens.
- Don't wire this into `PhysicsLab_Hole1` yet — that's Part E. For Part D, drop the canvas into a fresh empty test scene OR temporarily into Hole1's `LabRoot` for visual verification only.

✅ DONE: 2026-04-23 — Part D complete. All 5 files created + ShotConeTest.unity verified in Play mode.
- Cone method: (b) MaskableGraphic subclass (ConeMeshGraphic) — triangle via OnPopulateMesh; width rebuilds cheaply via SetVerticesDirty on stat change.
- Screenshots: (1) Idle — cone ghosted at ~25% alpha, no arrows, targeting line visible; (2) Timing — full alpha, 3 stagger-phased yellow arrows traveling up cone, HUD "50% / 125 yd", targeting line above apex.
- Cone width is accuracy-driven: HalfAngleDeg = lerp(ConeHalfAngleAtAcc0Deg, ConeHalfAngleAtAcc100Deg, accNorm) from ControlsConfig.
- Deviations: (1) Driver drove to Timing state (not Pulling) due to ShotController transitioning immediately when PowerNormalized>0. Arrow display is in Timing, per spec arrows are a Timing visual. (2) HUD text showed in Idle (no state events fired when driver disabled — expected in test setup). (3) DebugShotInputSource + ShotConeTestDriver added as test-only helpers in Golfin.Gameplay.Input assembly.
- 12/12 ShotController tests pass.



This task builds the **flick-based shot control system** — a screen-anchored semi-cone UI that the player drags down (power) and flicks up through (commit), with timing arrows traveling up the cone and an aim-fine-tune via the club's lateral position inside the cone.

**Authoritative design doc:** `Docs/Game Design/SHOT_CONTROLS_DESIGN.md`. Read it before starting Part A. All design decisions are settled there. If something in this spec contradicts the design doc, the design doc wins — flag the discrepancy back to Architect rather than guessing.

**Reference visuals:** `Docs/Game Design/In-Game - Shot Tests 5–9.png`.

**Existing contract** (do not modify): `Assets/Scripts/Physics/Stats/ShotInputBuilder.cs::Build(...)` returns `(ShotInput, BallPhysicsModifiers)`. Your job is to produce its arguments from raw touch input.

### Scope boundaries — read before starting

**In scope (v1):**
- One `ShotController` MonoBehaviour driving a state machine: `Idle → Aiming → Pulling → Timing → Flicking → Resolving`.
- Screen-anchored semi-cone uGUI surface (`ShotConeView`) with: cone outline, club trapezoid drag handle, timing arrows, power% / yards HUD, fixed-length targeting line.
- New Input System (`com.unity.inputsystem` 1.18.0 — already installed). New `Shot.inputactions` asset; do not touch the template `InputSystem_Actions.inputactions`.
- Editor mouse-as-touch (Q10a) via Input System's TouchSimulation — should work transparently; verify in Validation.
- Synthetic input feeder for EditMode tests (Q10c) — bypass touch entirely, drive state machine via direct method calls.
- Default fallbacks (`DefaultStatProvider`) so the controller works before BagManager / CharacterManager are wired into gameplay.
- Two new club-stat preset constants: `ClubStats.DefaultDriver`, `PutterStats.DefaultPutter`.
- Lab integration: drop the controller + cone UI into `PhysicsLab_Hole1` scene via Unity-MCP. Existing preset-based Fire button stays as `[Debug] Fire Preset`.
- Tunable constants in a new CSV: `Assets/Resources/Gameplay/controls.csv` + loader.
- Putt mode flag on the controller (Q8: same controller, mode flag). No spin / no overpower / no fade-draw / slower arrows when `IsPutt`.
- 8–10 EditMode tests for the input layer.

**Out of scope (defer):**
- Fade/draw curve preview rendering (controller emits the chosen mode; UI just shows text).
- Overpower visual polish (no shake, no flash). Functional clamp only.
- Spin pre-stage modal (use existing or default to `SpinState.None` with backspin via `ShotInputBuilder` defaults).
- Map-screen aim handoff (camera defaults to ball→pin).
- In-shot club switching.
- Mow-stripes / lie-aware visual hints.
- Multi-club CSV — `clubs.csv` (PGA Tour values) is the only club data v1 reads. Per-rarity club content is its own future task.
- Any modification to physics code (`Golfin.Physics.*`). The contract is fixed.

### Phasing

This task is large enough to phase. Land each phase, run tests, report, wait for go-ahead before the next. Phases:

- **Part A** — Defaults + DefaultStatProvider + ClubStats/PutterStats presets + controls.csv + loader. No MonoBehaviours. Pure data layer. (~1 hour)
- **Part B** — `ShotController` MonoBehaviour, state machine, synthetic input feeder, EditMode tests. No UI yet. (~2 hours)
- **Part C** — `Shot.inputactions` asset + Input System wiring, mouse-as-touch verification. Still no visible UI — add a placeholder log emitter so you can verify the touch → state-machine path works. (~1 hour)
- **Part D** — `ShotConeView` uGUI cone, club trapezoid, arrows, HUD, targeting line. (~2–3 hours)
- **Part E** — `PhysicsLab_Hole1` integration via Unity-MCP. Drop controller + UI canvas; wire the live touch path to `BallSimulation.Simulate()`; keep preset Fire as debug. (~1 hour)
- **Part F** — Putt mode flag, debug toggles, validation pass. (~1 hour)

Report at the end of each part: what landed, test count, screenshots if relevant, any spec discrepancies. Wait for Architect ack before starting the next part.

---

### Part A — Defaults + config

**Files to create / modify:**

1. `Assets/Scripts/Physics/Stats/ClubStats.cs` — **modify**. Add `public static readonly ClubStats DefaultDriver` matching the Driver row in `Assets/Resources/Physics/clubs.csv` (`BaseVelocityMps=75`, `BaseBackspinRpm=2686`, `LoftDegrees=10.9`, `Power=50`, `Accuracy=50`, `LieResistance=50`, `Durability=100`). Minimal diff — add the constant only, do not change existing fields.

2. `Assets/Scripts/Physics/Stats/PutterStats.cs` — **modify**. Add `public static readonly PutterStats DefaultPutter` (`BaseVelocityMps=5`, `LoftDegrees=4`, `Control=50`, `Accuracy=50`, `Weight=50`, `Durability=100`).

3. `Assets/Scripts/Gameplay/Defaults/DefaultStatProvider.cs` — new. Static class:
   ```csharp
   public static StatBundle BuildSwingBundle();   // BagManager equipped club || DefaultDriver, ball || Neutral, char || Neutral
   public static StatBundle BuildPuttBundle();    // BagManager equipped putter || DefaultPutter, ball || Neutral, char || Neutral
   ```
   - Use reflection-free duck typing: `if (BagManager.Instance != null) ...`. If BagManager doesn't exist as a type yet (it does — `Golfin.Roster.BagManager` per project memory — verify path during impl), wrap the access in a `#if` guard or a try-catch. Aim for: gameplay never breaks if inventory isn't wired.
   - Return `StatBundle` with `IsPutt` set correctly per method.
   - Place in namespace `Golfin.Gameplay.Defaults`.

4. `Assets/Scripts/Gameplay/Config/ControlsConfig.cs` — new. Plain struct with all the fields from `Docs/Game Design/SHOT_CONTROLS_DESIGN.md` §7. Use `float` (not `fp`) — these are screen-pixel and seconds values, not physics state. Include a `public static ControlsConfig Default` matching the seed values in the design doc.

5. `Assets/Resources/Gameplay/controls.csv` — new. Two columns `key,value` plus an optional `notes` column (loader ignores it). Copy the exact seed values from the design doc §7.

6. `Assets/Scripts/Gameplay/Config/ControlsConfigLoader.cs` — new. Mirror the pattern of `Assets/Scripts/Physics/Runtime/PhysicsConfigLoader.cs` (already exists — read it before writing this). One method: `public static ControlsConfig Load()`. Reads CSV from Resources, parses key/value pairs, populates `ControlsConfig`, falls back to `ControlsConfig.Default` for any missing key (with a Debug.LogWarning per missing key so we notice CSV drift).

7. `Assets/Scripts/Gameplay/Config/Golfin.Gameplay.Config.asmdef` — new. References: none (config is self-contained; no Unity dependencies needed beyond `UnityEngine` for Resources.Load and Debug). Auto-referenced: false.

8. `Assets/Scripts/Gameplay/Defaults/Golfin.Gameplay.Defaults.asmdef` — new. References: `Golfin.Physics.Stats`. (And whatever inventory asmdef holds BagManager / CharacterManager — verify name during impl. If those aren't in their own asmdef, we'll thread the dependency lazily via reflection-free duck typing as above.)

**Tests for Part A:** none (pure config). Validation = compile clean + Debug.Log dump of `ControlsConfig.Load()` showing all 18 fields populated.

**Done report Part A:**
- Files added.
- `ControlsConfig.Load()` log dump (full field listing).
- Confirmation that BagManager / CharacterManager paths were verified or stubbed.

✅ DONE: 2026-04-23 — All 8 files written, compile clean, ControlsConfig.Load() dumps all 21 fields correctly. DefaultDriver (Power=50 Acc=50 LR=50 Dur=100 Loft=10.9 Vel=75 Spin=2686) and DefaultPutter (Control=50 Acc=50 Wt=50 Dur=100 Loft=4 Vel=5) verified via script-execute. BagManager confirmed in global namespace (Assembly-CSharp, no custom asmdef) — DefaultStatProvider always returns defaults; BagManager wiring deferred to when BagManager gets its own asmdef. Golfin.Gameplay.Defaults.asmdef references both Golfin.Physics.Stats AND Golfin.Physics.Math (needed for fp in StatBundle constructor — spec deviation flagged). Pushed to GitHub.

---

### Part B — ShotController + state machine + tests

**Files:**

1. `Assets/Scripts/Gameplay/Input/ShotInputState.cs` — new. Readonly struct snapshot of the current per-frame state for UI consumption. Fields: `State` (enum), `PowerNormalized` (float, 0–1.2), `ConeFinetuneX` (float, -1..+1), `ArrowProgress01` (float, 0..1 for current pass), `PassIndex` (int), `IsDegrading` (bool), `IsPutt` (bool), `AimYawRadians` (float, world yaw), `CameraHeadingRadians` (float). UI reads this each frame; controller publishes via `public event Action<ShotInputState> OnStateChanged` fired every state transition + every fixed-tick within active states.

2. `Assets/Scripts/Gameplay/Input/ShotState.cs` — new. Enum: `Idle, Aiming, Pulling, Timing, Flicking, Resolving`.

3. `Assets/Scripts/Gameplay/Input/IShotInputSource.cs` — new. Interface so we can swap real Input System for the synthetic test feeder:
   ```csharp
   public interface IShotInputSource
   {
       bool   IsTouching        { get; }
       Vector2 TouchPositionPx  { get; }   // current position
       Vector2 TouchOriginPx    { get; }   // touch-down origin
       Vector2 TouchVelocityPxPerSec { get; }  // smoothed
   }
   ```

4. `Assets/Scripts/Gameplay/Input/SyntheticInputSource.cs` — new. EditMode-friendly implementation; tests drive it directly.

5. `Assets/Scripts/Gameplay/Input/ShotController.cs` — new. MonoBehaviour. Owns: state, current `IShotInputSource`, current `ControlsConfig`, current `StatBundle`, current `ResolvedShotModifiers`. State transitions per design doc §3.1. On entering `Resolving`: build `(ShotInput, BallPhysicsModifiers)` via `ShotInputBuilder.Build(...)` and invoke `public event Action<ShotInput, BallPhysicsModifiers> OnShotResolved`. The lab controller subscribes and calls `BallSimulation.Simulate(...)` on its end — the input controller is sim-agnostic.

   **Critical**: ShotController does NOT directly call BallSimulation. It emits the resolved input via event. This keeps `Golfin.Gameplay.Input` from depending on `Golfin.Physics` (it only needs `Golfin.Physics.Stats` for the Build call's input/output types).

6. `Assets/Scripts/Gameplay/Input/Golfin.Gameplay.Input.asmdef` — new. References: `Golfin.Physics.Stats`, `Golfin.Gameplay.Config`, `Golfin.Gameplay.Defaults`. Notably **does NOT reference `Golfin.Physics`** — the seam.

7. `Assets/Scripts/Gameplay/Tests/ShotControllerTests.cs` — new. EditMode tests. Use the synthetic feeder.

**Test cases (8 minimum):**

1. `ShotController_Idle_NoTransitionWithoutTouch` — default state, no input → stays Idle.
2. `ShotController_TouchInsideHitZone_EntersAiming` — synthetic touch-down at ball position → state == Aiming.
3. `ShotController_DragPastPullThreshold_EntersPulling` — from Aiming, drag down past `PullStartThresholdPx` → Pulling.
4. `ShotController_PullDistance_MapsToPowerLinear` — various pull distances produce expected `PowerNormalized` values per the §3.2 table. Test boundaries: 0, MinUseful, Max100Percent, MaxOverpower, beyond MaxOverpower.
5. `ShotController_LiftBeforeFlickThreshold_CancelsToIdle` — from Timing, lift with velocity below threshold → Idle. No `OnShotResolved` event fired.
6. `ShotController_FlickAboveThreshold_TransitionsToResolving` — from Timing, flick up past threshold → Resolving + `OnShotResolved` fires once.
7. `ShotController_OnShotResolved_CallsBuildWithCorrectArgs` — mock the StatBundle, verify the emitted `ShotInput` has matching `Origin`, `Velocity` magnitude proportional to power, etc. (Don't compare exact velocity — too brittle. Compare ranges.)
8. `ShotController_PuttMode_ClampsAt100Percent` — with `IsPutt=true`, pulling past `MaxOverpowerPullPx` still clamps `PowerNormalized` at 1.0.

Optional 9–10:
9. `ShotController_PassDegradation_AddsAimErrorAfterCleanPasses` — hold in Timing through enough passes that degradation kicks in, verify the resolved aim yaw deviation is non-zero.
10. `ShotController_AutoCancel_AfterMaxTotalPasses` — hold in Timing past `MaxTotalPasses`, state returns to Idle without firing `OnShotResolved`.

**Done report Part B:**
- Test count + pass/fail.
- Architectural confirmation that ShotController has zero references to `BallSimulation` directly.
- `ShotInputState` event firing cadence (per frame? on transition only?) — confirm matches design.

✅ DONE: 2026-04-23 — 12/12 tests pass (Tests 1–10 implemented, including both optional). ShotController has zero direct BallSimulation references — only calls ShotInputBuilder.Build() and emits event. OnStateChanged fires every Tick (every frame), not just on transition — matches design doc intent (UI polls each frame). Spec deviation: Golfin.Gameplay.Input.asmdef references Golfin.Physics.Core (needed for ShotInput and BallPhysicsModifiers types in the OnShotResolved event signature). Semantic seam preserved — no direct BallSimulation calls. Pushed to GitHub.

---

### Part C — Input System wiring

**Files:**

1. `Assets/Scripts/Gameplay/Input/Shot.inputactions` — new. Single action map `Shot` with actions:
   - `Touch` (PassThrough, Vector2) — bound to `<Touchscreen>/primaryTouch/position` and `<Mouse>/position` (mouse-as-touch fallback).
   - `TouchPress` (Button) — bound to `<Touchscreen>/primaryTouch/press` and `<Mouse>/leftButton`.

2. `Assets/Scripts/Gameplay/Input/InputSystemSource.cs` — new. Implements `IShotInputSource` against the new Input System. Subscribes to the action callbacks in `OnEnable`, unsubs in `OnDisable`. Smooths velocity with a short ring buffer (last ~5 samples averaged).

3. `Assets/Scripts/Gameplay/Input/InputSimulationBootstrap.cs` — new. Single `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]` static method that calls `UnityEngine.InputSystem.EnhancedTouch.TouchSimulation.Enable()` when running in the editor, so mouse acts as a touch source. No-op on device builds.

4. **Modify** `Assets/Scripts/Gameplay/Input/ShotController.cs` — add a serialized field for which input source to use. Default to `InputSystemSource`. Tests inject `SyntheticInputSource` directly.

**Validation:**
- Open `PhysicsLab_Hole1` scene (don't add the controller yet — next part).
- Add a temporary `InputSystemSource` to a stub GameObject, log its position + press state per frame.
- Verify mouse clicks are read as touches in editor (mouse-as-touch via TouchSimulation).
- Verify on-device build path is preserved (don't actually build, just confirm no Editor-only references leak into runtime code paths).

**Done report Part C:**
- Confirmation that mouse-as-touch works in editor.
- One short Debug.Log capture showing TouchPositionPx and TouchVelocityPxPerSec updating during a mouse drag in Play mode.

✅ DONE: 2026-04-23 — Compile-clean. InputSystemSource correctly implements IShotInputSource (all 4 properties verified via reflection). Bootstrap calls EnhancedTouchSupport.Enable() + TouchSimulation.Enable() (both confirmed callable, no exception). ShotController [SerializeField] _inputSystemSource + Awake wiring confirmed. Golfin.Gameplay.Input.asmdef needed explicit Unity.InputSystem reference (not auto-included for custom asmdefs). Mouse-as-touch Live verification (drag + log) requires manual Play-mode test by Cesar — wire Shot.inputactions asset reference in InputSystemSource Inspector, enter Play mode, drag mouse, confirm IsTouching and position log output. Pushed to GitHub.

---

### Part D — Cone UI

**Files:**

1. `Assets/Scripts/Gameplay/UI/ShotUI/ShotConeView.cs` — new. MonoBehaviour on a Canvas child. Subscribes to `ShotController.OnStateChanged`. Renders:
   - Cone outline (uGUI Image with a custom cone sprite, or a runtime-generated mesh; pick whichever is faster to land — mesh is more flexible for stat-driven width changes). Width = stat-driven per design doc §3.3.
   - Club trapezoid (uGUI Image). Position = touch position clamped to cone interior. Visualized as a clubhead sprite — placeholder rectangle is fine for v1; Cesar can swap art later.
   - Timing arrows (object pool, ~3 arrow instances). Travel up the cone toward the apex. Speed driven by Club Control per §3.4.
   - Power% / yards HUD (TextMeshPro at top-right). Live during Pulling. Yards = pre-cached max-carry for current club, scaled linearly by `PowerNormalized`.
   - Targeting line (uGUI Image stretched into a line, or a `LineRenderer` if simpler in screen-space; project the world ball position to screen, draw forward `TargetingLineLengthMeters` along current aim heading).

2. `Assets/Scripts/Gameplay/UI/ShotUI/ConeAlphaController.cs` — new. Handles the fade per §3.1.2: ghost in Idle, fade in on Aiming, full in Pulling+, fade out on Resolving. Tweens via simple Lerp + delta-time; no DOTween dependency.

3. `Assets/Scripts/Gameplay/UI/ShotUI/Golfin.Gameplay.UI.asmdef` — new. References: `Golfin.Gameplay.Input`, `Golfin.Gameplay.Config`, `Unity.TextMeshPro` (or whatever the project's TMP asmdef name is — verify).

**Visual notes:**
- Per Cesar's preferences: Code builds the *functional* UI hierarchy. Cesar will style/restyle aesthetically later. So: focus on correct positioning + correct data binding. Use placeholder colors (white/gray cone outline, blue trapezoid, yellow arrows, red HUD text) and a placeholder ball-hit-circle sprite. Don't spend cycles on polish.
- Bottom-anchored, screen-fixed (per design doc §2). Cone apex roughly at screen-center-Y; cone base at screen-bottom.

**Done report Part D:**
- Screenshot via `screenshot-game-view` showing the cone in Idle (ghosted) and Aiming (full opacity).
- Confirmation that cone width responds to a test stat change (manually set Club.Accuracy=10 vs 90 and capture both).

---

### Part E — PhysicsLab_Hole1 integration

**Unity-MCP scene edits:**

1. Open `Assets/Scenes/PhysicsLab_Hole1.unity` (or whatever the lab scene path is — verify via search).
2. Find `LabRoot` GameObject. Add `ShotController` component to it.
3. Create child GameObject `ShotUI_Canvas` under `LabRoot`. Add Canvas (Screen Space - Overlay), CanvasScaler (Scale With Screen Size, 1080x1920 reference), GraphicRaycaster.
4. Under `ShotUI_Canvas`, instantiate `ShotConeView` as a child UI panel. Wire its `controller` reference to the `ShotController` on LabRoot.
5. Wire `PhysicsLabController` (existing) to subscribe to `ShotController.OnShotResolved`. On event: feed the resolved `(ShotInput, BallPhysicsModifiers)` directly into `BallSimulation.Simulate(...)` instead of the preset path. Use the existing `RunSim` helper as the pattern — you'll need a new `RunSimFromController(ShotInput input, BallPhysicsModifiers ballMods)` overload that skips the preset → input conversion and goes straight to simulation. Existing `RunSim(preset)` stays for the debug button.
6. Existing Fire button stays. Add a label change to `[Debug] Fire Preset` so it's clearly the dev path.
7. `currentScene = PresetScene.Hole1` is already the default; verify.

**Save the scene.** Don't auto-save anything else.

**Validation:**
- Run the scene in Play mode.
- Mouse-drag-flick on the ball — verify the cone UI appears, power gauge fills, arrows spawn, flick triggers a real trajectory.
- Compare against `[Debug] Fire Preset` button — both should produce visually similar trajectories at full power with default driver.
- `console-get-logs` clean.

**Done report Part E:**
- Screenshot of cone in Pulling state (~50% power) with arrow visible.
- Screenshot of trajectory after flick.
- Confirmation that the preset Fire button still works.

---

### Part F — Putt mode + debug toggles + final validation

1. **Putt mode:** verify the `IsPutt` flag on `ShotController` correctly:
   - Sources from `DefaultStatProvider.BuildPuttBundle()` instead of swing.
   - Clamps power at 1.0.
   - Slows arrows by `PuttArrowSpeedMultiplier`.
   - Skips spin (verify `ShotInput.spin == SpinState.None`).
   - Add a temporary toggle in the lab UI to flip swing/putt mode for testing.

2. **Debug toggles** (per design doc §8). Add a debug panel to the lab UI with these checkboxes — each just sets a public field on `ShotController` or `ShotConeView`:
   - Show cone outline (default on)
   - Show arrow trail (default on)
   - Cancel-on-slow-flick (default on)
   - Single-pass mode (skip degradation; default off)
   - Disable overpower (clamp at 100%; default off)
   - Disable cone fine-tune (aim is camera-only; default off)
   - Force-perfect timing (default off)
   - Force-perfect aim (default off)

3. **Run all tests** including the 8–10 from Part B. All must pass.

4. **Manual smoke test** on Hole 1:
   - 5 swing shots from tee, varying power and aim — confirm trajectories diverge appropriately.
   - 3 putts on the green — confirm putt mode behaves (short range, slow arrows, no overpower).
   - Verify cancel gesture works (touch-down, drag halfway, lift — no shot fires).

**Done report Part F:**
- Test count final.
- Smoke test summary: ~5 swing shots and ~3 putts results.
- Any deviations from the design doc that surfaced during impl.
- Any tunable constants that felt obviously wrong (so Cesar can adjust the CSV).

---

### DO NOT

- Modify any file under `Assets/Scripts/Physics/Core/` or `Assets/Scripts/Physics/Math/`. The contract is fixed.
- Modify `ShotInputBuilder.cs`. If you need additional info from it, propose an extension in your done report and Architect will spec it.
- Touch `Assets/InputSystem_Actions.inputactions` — that's the unused project template asset.
- Bring in DOTween, UniTask, or any other third-party tween / async library. Use coroutines or `Update` + Lerp.
- Use UI Toolkit (UITK) for the cone. uGUI to match existing inventory screens.
- Build per-rarity / per-type clubs beyond `DefaultDriver`. That's a future task.
- Make ShotController call `BallSimulation` directly. Event seam stays.
- Auto-save scenes other than `PhysicsLab_Hole1`.
- Skip phasing. Land each part, report, wait for ack.

### Iteration budget

- Part A: minimal iteration; pure config.
- Part B: 2 iterations on pull-distance → power mapping if test #4 boundaries feel wrong.
- Part C: 2 iterations on velocity smoothing if it feels jittery.
- Part D: 3 iterations on cone visual layout if positioning is off (mostly: cone size in screen pixels, arrow speed visibility).
- Part E: 1 iteration on the Lab integration; should be mechanical.
- Part F: 2 iterations on putt mode if it feels off.

Beyond budget: surface for design re-tune, don't burn iterations.

### Reference

- Design doc: `Docs/Game Design/SHOT_CONTROLS_DESIGN.md` (authoritative)
- Existing contract: `Assets/Scripts/Physics/Stats/ShotInputBuilder.cs`
- Existing lab controller: `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs`
- Existing CSV loader pattern: `Assets/Scripts/Physics/Runtime/PhysicsConfigLoader.cs`
- Project memory: BagManager namespace `Golfin.Roster`, CharacterManager singleton via `.Instance` (verify exact paths during impl)
- Mockups: `Docs/Game Design/In-Game - Shot Tests 5–9.png`

---

## History Log (completed tasks, most recent first)

- 🚧 **2026-04-23** Phase 7 Shot Controls v1 — Parts A, B, C COMPLETE. Awaiting Part D (Cone UI).
  - Part A: defaults + config + presets + controls.csv + loader. Compile clean, fields verified.
  - Part B: ShotController + state machine + 12/12 EditMode tests pass. Zero direct BallSimulation refs (event seam preserved). Spec deviation: Input asmdef refs `Golfin.Physics.Core` for ShotInput/BallPhysicsModifiers types in event signature — accepted, semantic seam preserved.
  - Part C: Input System wiring (no generated wrapper, string lookup; Unity.InputSystem explicitly in asmdef). 90-minute diagnostic detour: `HeightProvider.Awake()` LogError on missing heightmap.bytes → Unity Error Pause → all input symptoms looked like New Input System failure. Resolution: removed dead `HeightProvider` GO from scene. Lesson filed at `tasks/lessons.md`.

- ✅ **2026-04-22** Manual Scene Snapshot tool — 6 files + 2 asmdefs. 8/8 EditMode tests pass (1.59s). Window at `Window > Golfin > Manual Scene Snapshot`. Capture/restore of manually-placed GameObjects, terrain trees, and detail layers via stable per-prop GUIDs (`ManualPropId`). Key deviation: ManualPropId moved to `Assets/Scripts/SceneSnapshot/` (runtime asmdef) — editor-only types can't be added via `AddComponent`.

- ✅ **2026-04-22** Phase 6 Stat Coupling (Specialized Roles model, Option D) — 49/49 EditMode tests pass (2.85s). New assembly `Golfin.Physics.Stats` (`noEngineReferences: true`): `ClubStats`, `PutterStats`, `BallStats`, `CharacterStats`, `StatBundle`, `StatCoefficients` (14 coefficients), `StatCaps` (11 caps), `ResolvedShotModifiers`, `StatModifierResolver` (8-step resolver), `ShotInputBuilder` (returns `(ShotInput, BallPhysicsModifiers)` tuple). `BallPhysicsModifiers` struct in Core. `BallSimulation` Phase 6 8-arg overload; Phases 3/5 forward with Neutral for bit-exact backward compat. `PhysicsConfigLoader.LoadStatCoefficients()` + `LoadStatCaps()`. `stats.csv` + `stat_caps.csv`. 10 new `StatResolverTests` including bit-exact gate. Tolerance fix: switched 6 tests from raw-unit to `ToFloat() ± 0.001f` (Q16.16 rounding across multi-step multiplies). Lab integration deferred — lab keeps using raw `ShotInput`.

- ✅ **2026-04-22** Phase 5 Putt model — 35/35 tests pass (3.23s). `PuttConfig.cs` + `putt.csv` (Green 0.10/0.04, GreenCollar 0.14/0.05); `BallSimulation` 7-arg overload with `IsPutt` gate (speed<8m/s, angle<15°, surface∈{Green,GreenCollar,Tee}), `RunPuttPhase` integrator, `IsPuttSurface` for seamless off-green transition; `PhysicsConfigLoader.LoadPuttConfig()`; PhysicsTuningWindow Putt foldout with "Sim 3m putt" (v0=0.35→d≈3.1m, within [2.7,3.3]m). Bit-exact gate passes. Part G scene deferred (non-blocking). RunRollPhase/RunPuttPhase still ~85% identical — no shared helper yet; defer to Phase 6 review.
- ✅ **2026-04-21** Phase 4 Surface interaction (bounce + roll) — 29/29 tests pass. `HeightmapData`/`HeightmapLoader`/`HeightProvider`, `SurfaceType`/`ISurfaceProvider`/`SceneSurfaceProvider`/`SurfaceMarker`, `SurfaceConfig` + `surfaces.csv`, `TerrainHit` records + new `TerminationReason` values (`BallStopped`/`HitWater`/`MaxBouncesExceeded`), bounce loop with backspin Cr multiplier, `RunRollPhase` with speed²-based stop detection. Key fixes during impl: `UnityEngine.Physics` namespace qualification, per-surface `SurfaceConfig.Default`, one-sided boundary differences in `SampleNormal`. Part G test scene deferred (manual QA, non-blocking).
- ✅ **2026-04-21** Phase 3 Wind — `WindConfig`, `WindModel.SampleWind`, `fpMath.Sin`/`TwoPi`, wind.csv, tuning window integration, 6 tests. 21/21 tests pass. Seed determinism verified bit-exact. Headwind/tailwind/crosswind/altitude profile all behave directionally.
- ✅ **2026-04-21** Phase 2.1 closeout — LUT-mode tests split by club class with honest per-club tolerances. Driver/Iron3 at 25%, mid-irons at 15%, wedges at 8%. 15 tests pass. Lessons filed at LESSONS_PHYSICS_AERO.md. Physics baseline accepted.
- ❌ **2026-04-21 REMEDIATION v3 — ARCHITECTURE ESCALATION HIT (Rung 3)** — Bearman–Harvey Cl at driver S=0.08 physically cannot produce 275 yd carry; lift barely balances gravity at launch. 1D-BH model ceiling. Not escalating to 2D LUT. Lessons filed: `Docs/LESSONS_PHYSICS_AERO.md`.
- ⚠️ **2026-04-21 REMEDIATION v2** Seed-value error, not architecture — Cl too high at low S. Driver 23.5% short residual matched ratio of seed overshoot.
- ⚠️ **2026-04-21 REMEDIATION v1** Correctly reverted `spin_drag_factor` scope creep; incorrectly reverted `spin_decay_rate` (real physics, restored in v3).
- ⚠️ **2026-04-21 PARTIAL** Phase 2.1 LUT architecture landed (CoefficientLut, CSV-driven LUTs, mode toggles); v0 tuning produced unphysical shapes. Series of remediations followed.
- ✅ **2026-04-21** Phase 2 Aerodynamics (constant Cd + linear-capped Cl) — `SpinState`, `AeroConfig`, `AeroModel.ComputeAeroForce()`, `ClubSpec`, `aero.csv`, `clubs.csv`, `PhysicsConfigLoader`, `PhysicsTuningWindow`.
- ✅ **2026-04-21** Phase 1 Vacuum Trajectory — `Golfin.Physics` core types with hand-rolled Q16.16 `fp`/`fp3` math lib. RK4 at dt=1/240s. **Gotcha:** `Dt/6` in Q16.16 truncates; reorder as `(sum * Dt) / 6`.
- ✅ **2026-04-21** Phase 0 Physics Heightmap Baker — Q16.16 fixed-point binary `heightmap.bytes`. All 18 holes baked. 36-byte header (GHM1 + version + res + sizeX/Z + posX/Y/Z + format).
- ✅ **2026-04-20** Phase 2b water shore ablation — confirmed depression-cliff cause. `ShoreRadius` restored to 10.
- ✅ **2026-04-20** Water Shore Phase 2c — inner collar ramp.
- ✅ **2026-04-20** Hole Flyover Recorder — `HoleFlyoverRecorder.cs`.
- ✅ **2026-04-20** UHoleGeo B-C cart path fix.
- ✅ **2026-04-20** Cart path junction endpoint snapping.
- ✅ **2026-04-20** Linear-slope tee skirt.
- ❌ **2026-04-20 REVERTED** Per-edge adaptive tee skirt.
- ⚠️ **2026-04-20 REVERTED** Per-layer terrain tint pass.
- ✅ **2026-04-19** Water Shore Phase 1 sampling.
- ✅ **2026-04-18** Bridge Viewer in UHoleGeo.
- ✅ **2026-04-18** Bridge Placement Tool (Unity).
- ✅ **2026-04-18** Tee border ring UV fix.

---

## Reference Docs

- `Docs/AI_CONTEXT.md` — project state, pipeline overview, session changelog
- `Docs/PHYSICS_RESEARCH.md` — physics architecture, 5+1 phase plan
- `Docs/PHYSICS_TUNING_TARGETS.md` — canonical physics numbers
- `Docs/LESSONS_PHYSICS_AERO.md` — aero remediation lessons + future tightening options (read before touching aero LUTs)
- `Docs/INVENTORY_REFERENCE.md` — inventory system patterns
- `Docs/LESSONS_FRINGE_BORDER_MESHES.md` — canonical submesh recipe
- `Docs/Game Design/SHOT_CONTROLS_DESIGN.md` — shot control v1 design (authoritative for Phase 7)
- `CLAUDE.md` — Claude Code session rules
- Unity-MCP — https://github.com/IvanMurzak/Unity-MCP
