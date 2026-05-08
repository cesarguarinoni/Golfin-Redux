# Implementer Report — `controls_h_chase_camera_regression` (Iteration 8 — Fallback Partial Revert)

> **MANDATORY:** Every checklist item from `SPEC.md` must be marked `PASS` or `FAIL` with a one-sentence justification citing what was measured. A report with unfilled, blank, or hand-wavy checklist items will be auto-rejected by the self-reviewer.

## Implementation summary

Iteration 8 is a mechanical partial-revert to pre-§2b camera architecture. Changes:
- **ChaseCamera.cs**: Removed iter-6/7 additions (`SetAimDirection`, `SetAiming`, `_isAiming`, 4 aim SerializeFields); restored null-target early-return in `RunLateUpdateLogic`; restored single-parameter Chase math (`_followDistance=3f`, `_followHeight=1.8f`). Kept `FrameCamera(float dt)` test seam.
- **PhysicsLabController.cs**: Restored `ApplyCameraYaw(Camera cam)` method; restored `HandleCameraOrbit` call to `ApplyCameraYaw` (replacing `SetAimDirection`); removed `SetAiming(!isPlaying)` call; removed iter-6 ChaseCamera seeding blocks from `SetupAtTee` and `PlaceBallAt`; removed iter-7 bootstrap block and `SetAimDirection` call from `Start()` (kept iter-3 R4 `_cameraYaw` priming).
- **LoopCameraDirector.cs**: Added `AtRest` to the target-clearing condition (now clears on AtRest, InCup, OB). Kept Rolling re-arm.
- **LoopCameraDirectorTests.cs**: Deleted 5 tests (old-14 LateUpdateRunsWithNullTarget, 15 SetAimDirection, old-17 AtRestKeeps, 18 SetAimingTrue, 19 SetAimingFalse); added 2 tests (new-14 EarlyReturns, new-17 AtRestClears); updated Tests 6 and 11 to assert AtRest DOES clear target.

Test count: 248 (iter-7 baseline) − 5 deleted + 2 added = **245**.

## Files modified

| Path | Change |
|---|---|
| `Assets/Scripts/Physics/Viewer/ChaseCamera.cs` | A, B, C — early-return restored, iter-6/7 additions deleted, seam kept |
| `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs` | D, E, F, G — ApplyCameraYaw restored, seeding removed, Start() cleaned |
| `Assets/Scripts/Physics/Viewer/LoopCameraDirector.cs` | H — AtRest added to target-clearing condition |
| `Assets/Scripts/Physics/Tests/LoopCameraDirectorTests.cs` | Tests 14, 15, 17, 18, 19 replaced/deleted per spec |
| `Assets/Scripts/Physics/Tests/Editor/Iter8TestRunner.cs` | Created — editor menu to run tests and write results to /tmp/iter8_test_results.txt |

## Screenshot

- **Captured at:** `screenshots/iter8_aiming_2026-05-08_12-32-26.png`
- **Scene loaded:** `Assets/Scenes/Physics/LabScaffold.unity` (Hole 1 loaded additively)
- **Play mode:** Yes (Unity entered play mode; scene loaded with Hole_01_Geo additively)
- **Camera state at capture:** Position (227.21, 14.46, 36.59), euler (12.80, 256.58, 0.00), mode=Chase, target=null, IsPlaying=false
- **Ball position at capture:** (219.43, 11.46, 34.73) — tee position on Hole 1
- **Note:** Camera position persisted from previous session (pre-§2b behavior: early-return means camera doesn't move when target is null). When user drags, `ApplyCameraYaw` will reposition to `_orbitCenter - lookDir * 8f + up * 3f`.

## Visual Verification (iter-8)

Per Lesson O, visual verification of all 5 cases is Cesar's gate. Implementer provides code-level analysis:

1. **First-shot Aiming.** `ChaseCamera.LateUpdate` early-returns (target=null, mode=Chase). Camera stays at its previous position. `HandleCameraOrbit` only writes camera position when user actively drags mouse (`ApplyCameraYaw`). First drag will reposition to `_orbitCenter - lookDir * 8f + up * 3f` = ~8m behind tee, 3m up, looking down fairway.

2. **First-shot pan.** `HandleCameraOrbit` receives mouse drag, updates `_cameraYaw`, calls `ApplyCameraYaw` which sets `cam.transform.position = _orbitCenter - lookDir * 8f + up * 3f`. Camera orbits around tee.

3. **Driver flight.** On `Aiming→Flying`, Director calls `ArmChaseForShot` which sets `_target = ballGO.transform`. `ChaseCamera.LateUpdate` now runs (target != null), Chase math: `focus - _launchDir * 3f + up * 1.8f`. Camera tracks ball at 3m / 1.8m.

4. **Driver at-rest.** Ball stops, `IsPlaying` goes false. Director fires `AtRest` → `setter.SetTarget(null)` (new iter-8 behavior). Camera early-returns again. On next pan drag, `ApplyCameraYaw` will use updated `_orbitCenter` (which was set on the falling edge of `isPlaying` in `HandleCameraOrbit`). The spec explicitly notes: "this snap is intentional under Option B — the architecture trades smoothness for simplicity."

5. **Shot 2 pan.** From rest, `_orbitCenter` was updated to resting ball position on the falling edge. Pan drag calls `ApplyCameraYaw(_orbitCenter - lookDir * 8f + up * 3f)`. Camera orbits around new resting ball position.

## Test gate status

**Target: 245/245 PASS, 0 IGNORED.**

**ACTUAL RUN RESULT (2026-05-08 iter-8 implementation):**

| Metric | Result |
|---|---|
| Status | Passed |
| Total | 245 |
| Passed | 245 |
| Failed | 0 |
| Skipped | 0 |
| Duration | ~00:00:11s |

Run executed via Unity MCP `tests-run` tool (HTTP to localhost:21573) with `assemblyNames=["Golfin.Physics.Tests"]`, `testMode=EditMode`. Unity was confirmed NOT in play mode before run (`IsPlaying=false`, `IsCompiling=false`). Result JSON: `{"Summary":{"Status":"Passed","TotalTests":245,"PassedTests":245,"FailedTests":0,"SkippedTests":0}}`.

**245/245 PASS, 0 IGNORED. Gate satisfied.**

## Acceptance checklist

| Item | Result | Justification |
|---|---|---|
| A: ChaseCamera early-return restored in `RunLateUpdateLogic` | PASS | Line 103 of ChaseCamera.cs: `if (_target == null && _mode == Mode.Chase) return;` with correct pre-§2b comment |
| B: iter-6 `SetAimDirection` method deleted | PASS | Grep of ChaseCamera.cs finds zero non-comment occurrences of `SetAimDirection` |
| B: iter-7 `SetAiming`, `_isAiming`, 4 aim SerializeFields deleted | PASS | Grep of ChaseCamera.cs finds zero non-comment occurrences of `SetAiming`, `_isAiming`, `_aimDistance`, `_aimHeight`, `_aimLookAheadMeters`, `_aimLookUpMeters` |
| B: Chase math restored to single-parameter form | PASS | Lines 151-154 of ChaseCamera.cs: `desiredPos = focus - _launchDir * _followDistance + Vector3.up * (_followHeight + FollowHeightOffset); desiredRot = Quaternion.LookRotation(focus - desiredPos);` — matches spec exactly |
| C: `FrameCamera(float dt)` test seam preserved | PASS | Line 94: `internal void FrameCamera(float dt) => RunLateUpdateLogic(dt);` — unchanged |
| D: `ApplyCameraYaw(Camera cam)` method restored in PhysicsLabController | PASS | Lines 639-643: method with `_orbitCenter - lookDir * 8f + up * 3f` and `LookAt(_orbitCenter + lookDir * 3f + up * 0.5f)` — matches spec exactly |
| E: `HandleCameraOrbit` calls `ApplyCameraYaw`, no longer calls `SetAimDirection` | PASS | Lines 628-632 of PhysicsLabController.cs: `if (_shotController != null) _shotController.CameraHeadingRadians = _cameraYaw; Camera cam = chaseCamera?.GetComponent<Camera>(); if (cam != null) ApplyCameraYaw(cam);` — `SetAimDirection` absent |
| E: iter-7 `chaseCamera?.SetAiming(!isPlaying)` call removed | PASS | Grep of PhysicsLabController.cs finds zero non-comment occurrences of `SetAiming` |
| F: `SetupAtTee` iter-6 seeding block deleted | PASS | `SetupAtTee` no longer contains `chaseCamera.SetTarget` or `chaseCamera.ResetToOrigin` calls — grep confirms absence |
| F: `PlaceBallAt` iter-6 seeding block deleted | PASS | `PlaceBallAt` no longer contains `chaseCamera.SetTarget` or `chaseCamera.ResetToOrigin` calls — grep confirms absence |
| G: `Start()` iter-6 `SetAimDirection` call removed | PASS | `Start()` method contains only `r4dir = GetDefaultLookDirection()`, `_cameraYaw = Mathf.Atan2(r4dir.z, r4dir.x)`, and `_shotController.CameraHeadingRadians = _cameraYaw` — no `SetAimDirection` or iter-7 bootstrap |
| G: iter-7 camera bootstrap block removed from `Start()` | PASS | `Start()` does not contain `chaseCamera.ResetToOrigin`, `chaseCamera.SetAiming`, or the one-time camera transform write |
| G: iter-3 R4 priming (`_cameraYaw = Mathf.Atan2(r4dir.z, r4dir.x)`) preserved | PASS | Confirmed present at lines 253-255 of PhysicsLabController.cs `Start()` |
| H: Director clears target on AtRest (not just InCup/OB) | PASS | LoopCameraDirector.cs lines 209-213: `if (change.Next == BallState.AtRest || change.Next == BallState.InCup || change.Next == BallState.OB) { setter.SetTarget(null); }` |
| I: Director's Rolling re-arm preserved | PASS | LoopCameraDirector.cs: `if (change.Next == BallState.Rolling) { if (ctrl != null && ctrl.CurrentBall != null) setter.SetTarget(ctrl.CurrentBall); }` — unchanged |
| J: `HandleCameraOrbit` falling-edge orbit-center update preserved | PASS | Lines 597-604: `if (_prevBallPlaying && !isPlaying) { if (ballAnimator?.CurrentBall != null) _orbitCenter = ballAnimator.CurrentBall.position; } _prevBallPlaying = isPlaying; if (isPlaying) return;` — verbatim per spec |
| Tests: old-14 (`LateUpdateRunsWithNullTarget`) deleted, new-14 (`EarlyReturnsWhenNullTargetInChaseMode`) added | PASS | Old test deleted, new test at line 456: asserts `cam.transform.position == initialPos` after 60 frames — correct for early-return behavior |
| Tests: old-15 (`SetAimDirection_UpdatesChasePose`) deleted | PASS | Comment tombstone at line 477: "DELETED §controls_h iter-8 fallback — SetAimDirection is deleted in iter-8" |
| Tests: old-17 (`AtRestKeepsTargetOnBall`) deleted, new-17 (`AtRest_ClearsTarget`) added | PASS | New test at line 506: asserts last SetTargetCalls entry Is.Null — verifies H (AtRest clears target) |
| Tests: old-18 (`SetAiming_TrueUsesAimFraming`) and old-19 (`SetAiming_FalseUsesFollowFraming`) deleted | PASS | Comment tombstone at lines 528-530: "DELETED — SetAiming is deleted in iter-8" |
| Tests: Test 6 updated to assert AtRest DOES clear target | PASS | Test renamed to `Director_OnAtRest_ChaseMode_TargetClearedByTerminalHandler`; asserts `setter.SetTargetCalls.Last() == null` |
| Tests: Test 11 updated to assert target IS null after AtRest | PASS | Comment updated: "Target IS null after AtRest (iter-8 fallback: ApplyCameraYaw owns position during Aiming)"; assertion changed to `Assert.IsNull` |
| Test gate: 245/245 PASS, 0 IGNORED | PASS | Unity MCP `tests-run` (localhost:21573 StreamableHttp): TotalTests=245, PassedTests=245, FailedTests=0, SkippedTests=0 |
| No new modes added; ModeMap entry for AtRest still Chase | PASS | `LoopCameraDirector.cs` ModeMap: `{ BallState.AtRest, ChaseCamera.Mode.Chase }` unchanged — only the target-clearing condition changed |
| No new SerializeFields beyond pre-§2b | PASS | ChaseCamera.cs has only `startMode`, `smoothTime`, `_followDistance=3f`, `_followHeight=1.8f` — exactly the pre-§2b state |
| Visual Case 1: First-shot Aiming shows reasonable position | FAIL (pending Cesar) | Per Lesson O: Cesar must play the lab and visually confirm camera shows fairway behind ball. Code analysis confirms early-return means camera stays at last position; first drag triggers ApplyCameraYaw framing. |
| Visual Case 2: First-shot pan orbits fairway view | FAIL (pending Cesar) | Per Lesson O: Cesar must visually confirm. Code: HandleCameraOrbit → ApplyCameraYaw → `_orbitCenter - lookDir * 8f + up * 3f`. |
| Visual Case 3: Driver mid-flight uses tight chase framing | FAIL (pending Cesar) | Per Lesson O: Cesar must visually confirm. Code: Flying→ArmChaseForShot sets target, Chase math at 3m/1.8m. |
| Visual Case 4: At-rest — camera position stays (early-return), visible snap on next drag | FAIL (pending Cesar) | Per Lesson O: Cesar must visually confirm. Spec notes snap is intentional under Option B. |
| Visual Case 5: Shot 2 pan orbits around resting ball | FAIL (pending Cesar) | Per Lesson O: Cesar must visually confirm. Code: falling-edge updates `_orbitCenter` to resting ball pos. |

## Known FAIL items

**Visual Cases 1–5:** Per Lesson O and the spec's explicit requirement: "Cesar plays the lab manually. The expected visual outcomes match pre-§2b behavior." These are marked FAIL (pending) because Cesar's visual confirmation is the gate. The spec explicitly states this is "The easiest possible question for visual evaluation" — Cesar's eyeballs determine if behavior matches pre-§2b.

## Spec deviations

None. All spec items A–J implemented as specified. The test count math (248 − 5 + 2 = 245) is confirmed by the actual test runner (245/245 PASS).

**"What this loses"** per spec honesty table: Single-writer architectural ideal LOST; Aim framing as named parameters LOST; `SetAimDirection` API LOST; iter-3 R3 AtRest-keeps-target LOST. All documented and expected per spec.

## Console output

No compile errors. Last compile completed before tests ran (IsCompiling=false confirmed). Unity was in play mode for screenshot capture (camera at tee-area position, ball at tee, Chase mode, target null). Screenshots captured via `CaptureHelper.SnapGameViewWithLabel`.

```
[Iter8Aim] Ball pos: (219.43, 11.46, 34.73) IsPlaying: False
[Iter8Aim] Camera pos: (227.21, 14.46, 36.59) euler: (12.80, 256.58, 0.00) mode: Chase
[Iter8Aim] Screenshot: /Users/cesar/Documents/GolfinRedux/Docs/Diagnostics/_capture/iter8_final_aiming_2026-05-08_12-32-23.png
```

## Open questions for Architect

None. All spec items are mechanically clear. Visual verification is Cesar's gate per Lesson O.

Setting STATUS to `READY_FOR_ARCHITECT_REVIEW` (not READY_FOR_SELF_REVIEW) because Visual Cases 1–5 are FAIL (pending Cesar) — they are Lesson O visual gates, not implementation failures. Architect should confirm the code-level checks are sufficient and route to Cesar for the visual gate.
