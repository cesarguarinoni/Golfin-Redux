# Architect Review — `controls_h_chase_camera_regression` (iteration 5)

**Reviewer:** golfin-reviewer
**Timestamp:** 2026-05-08 10:30 JST
**Verdict:** `ARCHITECT_REVIEW_PASS`
**Iteration under review:** 5 (post-CESAR_REJECTED iter-4 → iter-5 amendments)

> The iter-3 FAIL verdict that lived in this file is superseded. Iter-4 closed the gate items (test-runner, screenshots) and iter-5 addresses Cesar's Q1=Option B and Q2=manual-verify rulings plus the new R5 second-shot pan regression.

---

## Summary

Iter-5 is a clean architectural pass. R3-revised (Downrange releases at touchdown so Chase tracks the visual roll-out) and R5 (per-frame `UpdateOrbitDirection` so pan input survives multiple shots) are both structurally correct on direct code read. The 110 + 2 = 112 EditMode test gate is green. The smoothness of the Downrange→Chase release is conditional on Cesar's chat-visual confirmation per Q2 ruling — that's the explicit path the spec amendment authorized, not a deferral.

I concur with the self-reviewer's two architect-attention items:

1. **Test 14 is theatre** — confirmed. It verifies the API exists and doesn't throw across three calls, but never asserts `_launchDir` actually changed, never asserts `_velocity` was preserved, never simulates two-shot sequencing. It is a smoke-test for the new API, not a regression-catching assertion. **Not a blocker** because the structural fix in production code is sound and Cesar's manual chat-side verification is the Q2 path. Logged as P2 follow-up.

2. **No explicit `_followDistance`/`_followHeight` lerp** on the Downrange→Chase release — confirmed. The release relies entirely on `ChaseCamera.LateUpdate`'s SmoothDamp glide (`smoothTime = 0.08f`) for visual smoothness. The geometric distance between `_downrangePos` (~12 m past landing, 4 m up) and the Chase target pose (~3 m behind ball, 1.8 m up) is roughly 9 m horizontal + 2.2 m vertical, smoothed over ~160 ms = ~10 frames at 60fps. That is structurally a noticeable but non-jarring slide. **Acceptable for iter-5** because Cesar's chat-visual is the Q2 path. If Cesar reports residual jolt, a follow-up spec adds an explicit blend window — that is a one-paragraph addition, not a redesign.

3. **`Iter5TestRunner.cs`** under `Assets/Scripts/Physics/Tests/Editor/` follows the precedent of `Iter4TestRunner.cs` and `Iter4ShotCapture.cs` already in that folder. Flagged for archive-time housekeeping only, not fail-grade. The implementer should move it (and the iter-4 runners if they're not already moved) to `Docs/Specs/Completed/controls_h_chase_camera_regression/` when this task is closed.

---

## R3-revised — Downrange releases at touchdown (PASS)

`LoopCameraDirector.cs:166-182` — verified by direct read:

```csharp
if (setter.CurrentMode == ChaseCamera.Mode.Downrange)
{
    if (predictedCarry > 0f && currentProgress >= predictedCarry)
    {
        setter.SetTarget(ctrl.CurrentBall);
        ApplyMode(ChaseCamera.Mode.Chase);
    }
    return;
}
```

Structural analysis:

- **Outer gate.** `setter.CurrentMode == Downrange` ensures the release path only runs once Downrange has actually been entered. For shots under `minCarryForCinematicMeters = 30f` the cinematic cut never fires, mode stays Chase end-to-end via the ModeMap entries `Flying→Chase` and `Rolling→Chase`, and this branch is dead. **No spurious release on short shots.**
- **Inner condition.** `predictedCarry > 0f && currentProgress >= predictedCarry`. `predictedCarry` is computed from the trajectory's first non-IsStop terrain hit (or fallback to final XZ distance). `currentProgress` is the XZ projection of `(ball.position - origin)` onto `launchDir`, monotonic-non-decreasing in flight. The condition fires the same frame ball XZ progress reaches the carry distance — i.e., at visual touchdown, exactly what the spec asks for.
- **Live target restoration.** `setter.SetTarget(ctrl.CurrentBall)` re-arms with the live (post-Play) ball Transform, not a stale reference. Iter-2's reorder fix in `HandleShotResolved` ensures `CurrentBall` points to the correct Transform throughout Flying.
- **Mode dispatch.** `ApplyMode(Chase)` routes through the ModeChanged event so observers are notified.
- **Subsumes "Rolling stays in Chase".** Once Mode is Chase, the SM's later Flying→Rolling transition fires `ModeMap[Rolling] = Chase` (line 111) which is a no-op against the already-Chase mode. Carry-over is correct.

**Catches the regression class:** Test 13 (`Director_DownrangeReleased_WhenBallPassesTouchdown`) at `LoopCameraDirectorTests.cs:554-616`. Phase 1 positions ball at 70 m of 100 m carry, asserts Downrange fires. Phase 2 advances ball to 105 m (past carry), clears recorded calls, asserts `setter.SetModeCalls.Contains(Chase)`, `setter.SetTargetCalls.Count > 0`, and last `SetTarget` is the live ball Transform. **This is a real regression-catching test, not theatre.** It will fail if anyone removes or reorders the release block.

**Smoothness — Cesar visual judgment.** Per Q2 ruling, the SmoothDamp-only glide is acceptable evidence path; the architect does not block on lack of explicit lerp. If Cesar reports the touchdown still snaps violently, follow-up spec adds an explicit `_followDistance`/`_followHeight` blend over ~0.5 s (or repositions Chase pose at touchdown to minimize the geometric jump).

**Spec deviation acknowledgement.** The implementer used `TickCinematicCut` polling rather than the spec's `OnStateChanged` Flying→Rolling hint. Their rationale (documented in IMPLEMENTER_REPORT § Spec Deviations) is correct: the SM Flying→Rolling transition fires on the falling edge of `BallAnimator.IsPlaying` — i.e., AFTER the visual roll completes — so subscribing to it would release Downrange at the END of the roll, not at touchdown. The spec called the hint an "Implementation hint", not a mandate; the deviation is sound and Test 13 validates the polling approach produces correct behavior.

---

## R5 — Second-shot sideways pan (PASS)

End-to-end flow verified by direct read of `ChaseCamera.cs:81-85`, `PhysicsLabController.cs:280-287` (Update), `:496` (SetupAtTee priming), and `:581-651` (HandleCameraOrbit).

**Root cause analysis (confirmed):** pre-fix, `ApplyCameraYaw(cam)` writes `cam.transform.rotation/position` in `Update()`, but `ChaseCamera.LateUpdate` overrides those writes whenever `_target != null`. After shot 1 settles, the ball-at-rest is a valid `_target` (per iter-3 R3 ModeMap fix that intentionally preserves target on AtRest), so `LateUpdate` clobbers every yaw drag mid-frame and the camera never visibly orbits. First-shot was unaffected because the ball Transform during shot-1 Aiming was the freshly-spawned-but-at-rest tee ball — which iter-3's `Start()` priming gated on a `_target == null` early return that no longer holds in the second-shot scenario.

**Fix structure verified:**

1. **`ChaseCamera.UpdateOrbitDirection(Vector3 launchDir)`** at lines 81-85 — updates `_launchDir` only, does not reset `_velocity`. This means LateUpdate's next iteration re-computes `desiredPos = focus - _launchDir * _followDistance + up * (_followHeight + offset)` against the new direction, and SmoothDamp glides from current camera pos toward the new desired pos without a velocity reset.

2. **`PhysicsLabController.HandleCameraOrbit` lines 637-651** — when in Chase mode, the orbit handler now calls `chaseCamera.UpdateOrbitDirection(orbitLookDir)` *instead of* `ApplyCameraYaw(cam)`. For non-Chase modes (which orbit shouldn't engage in anyway, gated at line 593), the fallback uses the legacy `ApplyCameraYaw(cam)` path. Production-code orbit drag now writes through ChaseCamera's input field (`_launchDir`) rather than fighting LateUpdate.

3. **`PhysicsLabController.SetupAtTee` line 496** — calls `chaseCamera?.ResetToOrigin(teePos, lookDir)` after computing `lookDir = GetDefaultLookDirection()`. This primes `_launchDir` on every tee setup (initial scene load, ResetToTee, hole-load). Without this, mid-session tee resets could leave `_launchDir` stale from the previous shot's orbit drag.

**Subscription persistence across shots verified:**

- `Update()` at lines 280-287 calls `HandleCameraOrbit()` every frame unconditionally.
- `HandleShotComplete` at line 825 does NOT remove, replace, or disable the orbit handler. It re-arms `_shotController.CompleteShot()` and `_ballSM.ReArm()` only.
- `HandleCameraOrbit` early-returns on `chaseCamera.CurrentMode != Chase` (line 593). After AtRest, mode is Chase (per ModeMap entry `AtRest → Chase` from iter-3); after Aiming re-arm, `ModeMap[Aiming] = null` leaves Chase intact. So the gate at 593 lets pan input through on shot 2 and indefinitely after.
- Mouse input (`Mouse.current` line 612, `mouse.delta.x.ReadValue()` line 630) is read every frame from the same Input System device. No subscription, no action map, no input-disable path that could break mid-session.

**This solves "first shot pan works, second shot doesn't" specifically.** The fix's correctness is independent of shot count: `HandleCameraOrbit` runs per-frame from `Update`, the orbit drag math is the same on every frame, and `UpdateOrbitDirection` writes to ChaseCamera through a path LateUpdate respects. There is no codepath that disables or unwires this on shot completion.

**Test 14 caveat (concur with self-reviewer).** The test name promises three things — `_launchDir` changes, `_velocity` is NOT reset, the API works on every shot. The actual assertions only verify (a) the method compiles and (b) doesn't throw across three calls; the final `Assert.Pass(...)` is the only hard assertion. There is no `_launchDir` value check (it's private), no `_velocity` preservation check, no two-shot simulation. **It is a smoke test, not a regression-catching assertion.** That's noted as a follow-up improvement, not a blocker, because:

- The structural production fix above is reasoned-correct and Cesar's manual chat-side verification is the Q2 path.
- The pre-existing Test 6 (`Director_OnAtRest_ChaseModePersists_TargetNotClearedByTerminalHandler`) and Test 11 (`Director_ChaseModePersistsThroughFlying_Rolling_AtRest`) lock down the prerequisite — that mode stays Chase across the AtRest→Aiming transition, which is what gates the orbit handler at line 593.
- A proper position-trace test (simulate two shots, drive `HandleCameraOrbit` with synthetic mouse input, assert camera Transform actually moves on both) is a worthwhile P2 follow-up but doesn't change the iter-5 verdict.

**Recommended follow-up (P2):** upgrade Test 14 to a real position-trace assertion. Use a fake `IMouseProvider` seam or expose `_launchDir` to internal-test access, simulate two `HandleCameraOrbit` invocations after two `HandleShotResolved` calls, and assert `_launchDir` actually rotated on both. Not blocking iter-5.

---

## R6 — No violent ground snap (PASS conditional on Cesar)

Subsumed under R3-revised. The release sets target + mode and lets ChaseCamera's SmoothDamp glide handle the position transition. No hard `transform.position = ...` assignment on the release. Cesar verifies in chat per Q2 ruling — that is explicitly the authorized evidence path for iter-5. If the result is "still violent" in chat, follow-up spec adds an explicit lerp window. The architect does not preemptively block on the absence of explicit lerp because:

1. Q2 is explicit: Cesar verifies visuals manually.
2. SmoothDamp's `smoothTime = 0.08f` over a ~9 m horizontal + 2.2 m vertical delta produces a ~160 ms glide — that's structurally not a hard snap.
3. If chat-side verification reports residual jolt, the fix is a well-bounded follow-up (one paragraph in a new spec), not a fundamental rework.

---

## Test gate

**Result:** `iter5_test_results.txt` line 118-123 — **TOTAL=112 PASSED=112 FAILED=0 SKIPPED=0, GATE: PASS.**

**Count add-up:** iter-4 baseline = 110 leaf tests. Iter-5 adds Test 13 + Test 14 = 2 new tests. 110 + 2 = 112. **Add-up correct.**

The runner method (`Iter5TestRunner.cs` invoked via MCP `script-execute`) is a deterministic out-of-band path that bypasses the MCP `tests-run` namespace which had been intermittent in iter-3. This is the same precedent set in iter-4 and is acceptable as long as the result file is reproducible and lists every test by name with PASS/FAIL.

---

## Iter5TestRunner.cs — archive-time housekeeping (FLAG only)

`Assets/Scripts/Physics/Tests/Editor/Iter5TestRunner.cs` — Editor-only `[MenuItem]` test runner, follows the precedent of `Iter4TestRunner.cs` and `Iter4ShotCapture.cs` already in that folder. Per the L4 ruling for SmokeTestRunner files, all per-iteration runners under `Assets/Scripts/Physics/Tests/Editor/` should be moved to `Docs/Specs/Completed/controls_h_chase_camera_regression/` when this task is archived. The iter-4 runners should follow at the same time. **Not a fail-grade item per the spec's iter-5 amendments and the self-reviewer's correct read.**

---

## Capture-helper compliance (PASS)

No new screenshots produced this iteration (Q2 ruling). No new `*Context.cs` files added under `Assets/Scripts/Gameplay/UI/ShotUI/HUD/`. CaptureHelper extension protocol is N/A. PASS.

---

## Cross-cutting checks

- **Asmdef boundaries.** All work is in `Golfin.Physics.Viewer` (LoopCameraDirector + ChaseCamera + PhysicsLabController) and `Golfin.Physics.Tests` (LoopCameraDirectorTests, Iter5TestRunner). No new asmdef. No cross-namespace pollution. PASS.
- **No re-implementation of existing utilities.** `UpdateOrbitDirection` is a new method, not a re-implementation — `ResetToOrigin` exists for the full reset case (with `_velocity` reset), `UpdateOrbitDirection` is the partial-reset case for orbit drag. The two are intentionally distinct. PASS.
- **No raw scene/asset YAML edits.** Changes are all `.cs` files. PASS.
- **Spec-locked files unchanged.** `BallSimulation.cs`, `Trajectory.cs`, `AeroModel.cs`, aero CSVs, `BallAnimator.cs` — none touched. `BallStateMachine.cs` not modified in iter-5 (was modified per L5 docstring update in iter-2, that change persists). PASS.
- **No latent null-ref or asset-load-order issues found.** `ctrl.CurrentBall` null-check inside Director is consistent with existing usage. `chaseCamera?.UpdateOrbitDirection(...)` null-check guard is present at line 643.

---

## Decision

Per the prompt's decision tree:

- R3-revised + R5 code fixes are correct → ✓
- Test 13 catches the R3 regression class → ✓ (Phase 1 + Phase 2 with concrete `setter.SetModeCalls` / `setter.SetTargetCalls` assertions)
- Test 14 is theatre but doesn't undermine the fix → noted as P2 follow-up, not blocking
- R3-revised has no explicit lerp but Cesar's chat-visual is the Q2 path → acceptable

**Verdict: `ARCHITECT_REVIEW_PASS`.**

Cesar to verify the two visual acceptance items in chat:

1. **R3-revised:** drive a driver full-power shot. Confirm the camera goes Chase → Downrange cinematic during flight → at touchdown, mode releases to Chase and follows the ball through the visual roll-out until AtRest, with no perceivably violent snap on the Downrange→Chase transition.
2. **R5:** fire shot 1 from tee, pan left/right (works). Wait for ball to settle. Pan left/right again from the new aiming pose — must work identically.

If either fails Cesar's chat-visual, route back via `CESAR_REJECTED` and the implementer adds an explicit `_followDistance`/`_followHeight` blend window for R3, or whatever specific gap R5 reveals.

---

## P2 follow-ups (not blocking iter-5)

1. Upgrade Test 14 from a smoke-test (`Assert.Pass`) to a position-trace assertion that simulates two shots + two synthetic pan inputs and asserts `_launchDir` actually rotated on both. Optional internal-test seam exposing `_launchDir` would help.
2. If Cesar reports residual snap on R3 release: add an explicit `_followDistance`/`_followHeight` lerp over ~0.5 s (or reposition Chase pose at touchdown so geometric jump is minimal).
3. At archive time, move `Iter5TestRunner.cs` (and the iter-4 runners if not already moved) from `Assets/Scripts/Physics/Tests/Editor/` to `Docs/Specs/Completed/controls_h_chase_camera_regression/` per the L4 SmokeTestRunner precedent.

---

## Files relevant to this review

- `/Users/cesar/Documents/GolfinRedux/Assets/Scripts/Physics/Viewer/LoopCameraDirector.cs:166-182` — R3-revised touchdown-release block.
- `/Users/cesar/Documents/GolfinRedux/Assets/Scripts/Physics/Viewer/ChaseCamera.cs:81-85` — new `UpdateOrbitDirection` method.
- `/Users/cesar/Documents/GolfinRedux/Assets/Scripts/Physics/Viewer/ChaseCamera.cs:101-162` — LateUpdate confirms `_launchDir` is read on every frame for Chase mode.
- `/Users/cesar/Documents/GolfinRedux/Assets/Scripts/Physics/Viewer/PhysicsLabController.cs:496` — SetupAtTee primes `_launchDir` via `chaseCamera.ResetToOrigin`.
- `/Users/cesar/Documents/GolfinRedux/Assets/Scripts/Physics/Viewer/PhysicsLabController.cs:580-652` — full HandleCameraOrbit; per-frame call from Update, no teardown on shot complete.
- `/Users/cesar/Documents/GolfinRedux/Assets/Scripts/Physics/Viewer/PhysicsLabController.cs:825-840` — HandleShotComplete; confirms no input-handler teardown.
- `/Users/cesar/Documents/GolfinRedux/Assets/Scripts/Physics/Tests/LoopCameraDirectorTests.cs:554-616` — Test 13 (regression-catching).
- `/Users/cesar/Documents/GolfinRedux/Assets/Scripts/Physics/Tests/LoopCameraDirectorTests.cs:620-657` — Test 14 (theatre; flagged as P2 follow-up).
- `/Users/cesar/Documents/GolfinRedux/Assets/Scripts/Physics/Tests/Editor/Iter5TestRunner.cs` — flagged for archive housekeeping.
- `/Users/cesar/Documents/GolfinRedux/Docs/Specs/Active/controls_h_chase_camera_regression/iter5_test_results.txt` — 112/112 PASS, 0 failures.
- `/Users/cesar/Documents/GolfinRedux/Docs/Specs/Active/controls_h_chase_camera_regression/SELF_REVIEW.md` — iter-5 self-review (FORWARD verdict, two architect-attention notes — both concurred with above).
