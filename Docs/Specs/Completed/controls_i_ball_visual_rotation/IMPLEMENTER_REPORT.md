# Implementer Report — `controls_i_ball_visual_rotation`

> **MANDATORY:** Every checklist item from `SPEC.md` must be marked `PASS` or `FAIL` with a one-sentence justification citing what was measured. A report with unfilled, blank, or hand-wavy checklist items will be auto-rejected by the self-reviewer.

## Implementation summary

Added frame-to-frame position-delta–derived rotation to `BallAnimator.Update()` per Option A from the spec. Two new private fields (`_previousPos` and `BallRadiusMeters` const) were added. `SpawnInstance` now resets orientation to identity and seeds `_previousPos` after the position write. `SnapToEnd` now re-seeds `_previousPos`. Internal test-seam methods `DriveUpdateForTests`, `SpawnAtForTests`, and `InstanceForTests` were added. New `BallAnimatorTests.cs` created with two EditMode tests. A `BallAnimatorTestAutoRunner.cs` helper was added in the Editor subfolder to auto-run the tests on next script reload (fires on Unity exit from play mode).

## Files modified or created

| Path | Change |
|---|---|
| `Assets/Scripts/Physics/Viewer/BallAnimator.cs` | Modified — added `_previousPos` field, `BallRadiusMeters` const, rotation logic in `Update()`, identity-reset + seed in `SpawnInstance`, seed in `SnapToEnd`, and 3 internal test-seam members |
| `Assets/Scripts/Physics/Tests/BallAnimatorTests.cs` | Created — 2 EditMode tests: `Update_AppliesRotation_WhenBallTranslatesHorizontally` and `Update_DoesNotRotate_WhenBallStationary` |
| `Assets/Scripts/Physics/Tests/Editor/BallAnimatorTestAutoRunner.cs` | Created — temporary `[InitializeOnLoad]` helper that auto-runs BallAnimatorTests once on next script reload (exits safely if in play mode; writes results to `test_results.txt` in task folder) |

## Screenshot

- **Captured at:** BLOCKED — see Known FAIL items below
- **Scene loaded:** N/A (blocked)
- **Play mode:** N/A (blocked)
- **Hole loaded:** N/A (blocked)

## Acceptance checklist (copy from SPEC.md, fill every line)

| Item | Result | Justification |
|---|---|---|
| `BallAnimator.Update()` writes `transform.rotation` (via `transform.Rotate`) every frame the ball is animating and moves >0.1mm horizontally | PASS | Code review: `_instance.transform.Rotate(axis, angleDegrees, Space.World)` is called inside the `if (deltaMag > 0.0001f)` and `if (axisMag > 0.0001f)` guards in `Update()`, which fires on every frame while `_playing == true` |
| `BallAnimator.SpawnInstance` resets `_instance.transform.rotation = Quaternion.identity` and seeds `_previousPos` AFTER the position write | PASS | Code review: lines `_instance.transform.rotation = Quaternion.identity;` and `_previousPos = _instance.transform.position;` are both placed after `_instance.transform.position = ToVec3(startPos);` at end of `SpawnInstance()` |
| `BallAnimator.SnapToEnd` re-seeds `_previousPos` after the final position write (defensive) | PASS | Code review: `_previousPos = _instance.transform.position;` is inside the `if (_instance != null)` guard in `SnapToEnd()`, after the position write, matching spec requirement exactly |
| New `BallAnimatorTests.cs` file in `Assets/Scripts/Physics/Tests/` with both tests passing in Unity Test Runner (EditMode) | FAIL | Tests cannot be executed: Unity is locked in Play Mode (lockfile at `Temp/UnityLockfile`, last modified 2026-05-12 07:49 JST); batch mode exits with return code 1 due to project lock. See Known FAIL items. |
| Full EditMode test gate run: count = previous count + 2, ALL PASS, 0 IGNORED | FAIL | Same blocker as above — cannot run test suite. Pre-existing count was 21 test files; +2 tests expected from `BallAnimatorTests.cs`. |
| No new GC allocations in `Update()` hot path (verify by code review — no `new Vector3()`, no `new Quaternion()`, no string concat, no `Debug.Log`) | PASS | Code review: `Update()` rotation block uses only `_instance.transform.position` (struct read), `Vector3.Cross` (static method returning value type), scalar arithmetic, and `_instance.transform.Rotate` — no heap allocations, no string operations, no `Debug.Log` |
| Unity Console has no errors related to this task on play-mode entry or during a smoke shot | FAIL | Cannot verify: Unity is in Play Mode with old compiled code (BallAnimator.cs was modified but not yet recompiled because Unity is in play mode). The log shows no compile errors for BallAnimator.cs, but new rotation code hasn't been compiled yet. |
| **Visual-fidelity verification (Lesson O)** — ball visibly tumbles/rolls during flight; GOLFIN logo rotates; putter shot rolls correctly | FAIL | Cannot perform: Unity is in Play Mode with old code (pre-rotation changes not compiled). Cannot exit play mode, enter physics lab, and fire shots without computer-use or Unity MCP tools (neither available in this agent's tool schema). |
| **Visual evidence:** at least one screenshot showing ball mid-flight with logo at non-identity orientation | FAIL | Blocked by same cause as visual-fidelity item above — no screenshot capture tools available. `BallAnimatorTestAutoRunner.cs` has been staged to auto-run tests once Unity exits play mode and recompiles. |
| All `[SerializeField]` references wired in the Inspector (no scene/prefab changes expected) | PASS | Code review: no new `[SerializeField]` fields were added. The spec states "No scene/prefab changes expected" — confirmed: `BallAnimator` already has `ballPrefab` wired in the scene; no new serialized fields introduced. |
| Spec deviations flagged | PASS | None: implementation exactly follows Edit 1, Edit 2, and Edit 3 from the spec. |

## Known FAIL items

### FAIL 1: Test execution blocked — Unity locked in Play Mode

**What's blocking:** Unity is currently in Play Mode with the Physics Lab running (confirmed via `Editor.log` — last "Entering Playmode" at log line 33111, no matching "Exiting Playmode"). The project lockfile exists at `Temp/UnityLockfile` (modified 2026-05-12 07:49 JST). Batch mode `-runTests` exit code 1 immediately due to project lock.

**What would unblock:** Cesar exits Play Mode in the Unity Editor. When Unity exits play mode, it will:
1. Detect the new `BallAnimatorTests.cs` and `BallAnimatorTestAutoRunner.cs` files
2. Compile all assemblies (including `Golfin.Physics.Tests`)
3. Fire `BallAnimatorTestAutoRunner` (`[InitializeOnLoad]`), which auto-runs the two tests and writes results to `Docs/Specs/Active/controls_i_ball_visual_rotation/test_results.txt`
4. Debug.Log output will show `[BallAnimatorTestAutoRunner] DONE — Passed=2, Failed=0` if tests pass

**Expected test behavior (code review):**
- `Update_AppliesRotation_WhenBallTranslatesHorizontally`: spawns ball at origin, moves to (0,0,1), calls `DriveUpdateForTests()`. Expects non-zero rotation about ±X axis. `Vector3.Cross((0,0,1).normalized, (0,1,0))` = `(-1,0,0)`, non-zero, so rotation WILL be applied. SHOULD PASS.
- `Update_DoesNotRotate_WhenBallStationary`: 60 frames at same position, `delta.magnitude = 0 < 0.0001f` guard triggers, no rotation applied. SHOULD PASS.

### FAIL 2: Visual verification and screenshot blocked — no GUI access

**What's blocking:** Agent has no computer-use tools and no Unity MCP tools in its tool schema. Cannot enter/exit play mode, cannot fire a shot, cannot take a screenshot.

**What would unblock:** After Cesar exits play mode and Unity recompiles (which triggers the test auto-run per FAIL 1), Cesar should:
1. Open `LabScaffold.unity` (or keep the currently-loaded physics lab)
2. Enter Play Mode
3. Fire a preset Driver shot
4. Observe that the GOLFIN logo on the ball rotates (visibly tumbles) during flight
5. Fire a Putter shot and confirm ball rolls along the green
6. Capture a screenshot via `GOLFIN > Capture > ...` or `CaptureHelper.SnapAtEndOfFrameAndPause`
7. Copy result to `screenshots/` in the task folder
8. Confirm visual description matches: "Ball visibly tumbles/rolls during flight; GOLFIN logo rotates around the ball rather than staying world-locked; on green roll-out, ball rolls in direction of motion"

The code is correct — `_instance.transform.Rotate(axis, angleDegrees, Space.World)` is called on the spawned ball instance, not on the BallAnimator's own transform, so the rotation will affect the visible ball.

## Spec deviations

None. Implementation exactly follows Edit 1 (BallAnimator.cs modifications), Edit 2 (internal test seams), and Edit 3 (BallAnimatorTests.cs) from the spec. An additional helper file `BallAnimatorTestAutoRunner.cs` was added in the existing `Tests/Editor/` folder (following the pattern of Iter2c/Iter4/Iter5/Iter6/Iter8TestRunner files) to facilitate test execution when Unity exits play mode.

## Console output

Unity Editor.log shows no compile errors for `BallAnimator.cs` or related files. The new files (`BallAnimatorTests.cs`, `BallAnimatorTestAutoRunner.cs`) have NOT been compiled yet (no `.meta` files generated, no compilation log entries) because Unity is in Play Mode.

Relevant log snippet (last activity):
```
[BallAnimator] Ball prefab components: Transform, MeshFilter, MeshRenderer
Asset Pipeline Refresh (id=1a467e37ebc80714ab0af7b4ec4be76c): Total: 0.128 seconds - Initiated by RefreshV2(AllowForceSynchronousImport)
```
No `error CS` entries found in Editor.log tail-1000 search.

## Open questions for Architect

- **Blocker: This agent (implementer subagent) does not have Unity MCP tools or computer-use tools available in its tool schema.** The `mcp__ai-game-developer__*` tools referenced in CLAUDE.md and the role's hard rules are not present in the tool grants for this session. This blocks test execution and screenshot capture. Cesar must manually: (1) exit Play Mode to trigger compilation and test auto-run, (2) verify the `test_results.txt` file shows `Passed=2, Failed=0`, (3) enter Play Mode and fire a Driver shot to confirm visual ball rotation, (4) capture a screenshot. After Cesar confirms these pass, this task can proceed through the review chain.
