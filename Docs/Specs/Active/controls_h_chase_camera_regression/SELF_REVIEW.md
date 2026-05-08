# Self Review — `controls_h_chase_camera_regression` (iteration 5)

> Iteration: 4 of N (self-review iter-4 — pipeline iter-5; supersedes iter-3 ESCALATE verdict above)
> Reviewer: golfin-self-reviewer
> Timestamp: 2026-05-08 14:35 JST

---

## Visual diff notes (Step 1)

Per Cesar's Q2 ruling in iter-5 amendments: no new screenshot files this iteration. Cesar verifies visuals manually in chat. No Step 1 visual description applies — the verification surface this iteration is code/test correctness, not pixel comparison. The carry-over `controls_h_two_consecutive_shots_log.txt` from iter-2 is the only visual-evidence artefact that remains in scope, and it is unchanged.

---

## Step 3 — Walk the prompt's six verification items

### 1. R3-revised — Downrange → Chase release on Flying→Rolling (touchdown)

**Read:** `Assets/Scripts/Physics/Viewer/LoopCameraDirector.cs:166-182`.

```csharp
if (setter.CurrentMode == ChaseCamera.Mode.Downrange)
{
    if (predictedCarry > 0f && currentProgress >= predictedCarry)
    {
        UnityEngine.Debug.Log($"[CameraDirector][R3-revised] Downrange released at touchdown: ...");
        setter.SetTarget(ctrl.CurrentBall);
        ApplyMode(ChaseCamera.Mode.Chase);
    }
    return; // already cut — either release happened above, or ball not yet at landing
}
```

Verifications:

- **Release timing.** Fires when ball XZ progress (projection of `ball.position - origin` onto `launchDir`) >= predictedCarry. `predictedCarry` is computed from the trajectory's first non-stop terrain hit (or fallback to final XZ distance). At touchdown, `currentProgress` reaches predictedCarry. Not too early (peak of arc has progress < carry-distance because the ball is still mid-air over a roughly parabolic XZ trajectory, but XZ-progress is monotonic during flight, so frac>=1 only at landing). Not too late (fires the same frame `progress >= predictedCarry`, well before the ball has rolled past the camera). **PASS.**
- **Live target restoration.** `setter.SetTarget(ctrl.CurrentBall)` reassigns the target to the current ball Transform, not the stale reference. `ctrl.CurrentBall` is the post-Play() ball (per the iter-2 reorder fix in `HandleShotResolved`). **PASS.**
- **No violent snap (smoothness).** No explicit blend of `_followDistance`/`_followHeight`. The release just sets target + mode; ChaseCamera.LateUpdate then SmoothDamps with `smoothTime=0.08f` from `_downrangePos` toward `focus - _launchDir·3 + up·1.8`. Because SmoothDamp uses the same `_velocity` reference across mode changes, the camera glides over ~0.16s rather than teleporting. The implementer's claim that this is sufficient to avoid a "violent snap" is structurally plausible but ultimately a visual judgment for Cesar (per Q2 ruling). **PASS conditional on Cesar's chat-visual confirmation.**
- **Robust to short shots.** Outer gate `setter.CurrentMode == ChaseCamera.Mode.Downrange` ensures the release path only executes when Downrange was actually active. For shots under the cinematic threshold (carry < 30m), Mode never enters Downrange (line 184 early-return), so this branch never fires. Mode stays Chase end-to-end via the ModeMap entries `Flying→Chase` and `Rolling→Chase`. **PASS.**

The implementer used XZ-progress polling rather than the spec's `OnStateChanged` hint. They documented the rationale in § Spec Deviations: the SM's Flying→Rolling transition fires on the falling edge of `BallAnimator.IsPlaying` (i.e., AFTER the animator's visual roll completes), so subscribing to that event would release Downrange at the END of the roll, not at touchdown. Polling during Flying at `progress >= carry` correctly fires at visual touchdown. The spec calls "hook the release there" an implementation hint, not a mandate. The deviation is sound and Test 13 validates the polling approach.

### 2. R5 — Pan works on every shot

**Read:**
- `Assets/Scripts/Physics/Viewer/ChaseCamera.cs:81-85` — new `UpdateOrbitDirection(Vector3 launchDir)` method updates `_launchDir` only (does NOT reset `_velocity`).
- `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs:643-651` — `HandleCameraOrbit()` calls `chaseCamera.UpdateOrbitDirection(orbitLookDir)` when in Chase mode.
- `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs:496` — `SetupAtTee()` calls `chaseCamera?.ResetToOrigin(teePos, lookDir)` to prime the launch direction on tee resets.

Verifications:

- **Subscription persists across shots.** `HandleCameraOrbit()` is invoked unconditionally every frame from `Update()` (line 286: `_ballSM?.Tick(isPlaying); HandleCameraOrbit();`). There is no path in `OnShotComplete` (`HandleShotComplete`, line 825) or `SetupAtTee` (line 456) that disables, removes, or rewires this subscription. The handler simply runs every frame and inside it gates on Chase-mode + UI-overlay-not-open + not-currently-playing. Shot 2 enters Aiming with mode=Chase (per ModeMap AtRest→Chase carry-over from iter-3 + post-touchdown Chase from R3-revised), and `HandleCameraOrbit` continues to read mouse input identically to shot 1. **PASS.**
- **`UpdateOrbitDirection` math is meaningful.** The handler computes `_cameraYaw += dx * _orbitSensitivity * Deg2Rad` from mouse drag, then `orbitLookDir = (cos(yaw), 0, sin(yaw))`. `UpdateOrbitDirection` writes `_launchDir = (orbitLookDir.x, 0, orbitLookDir.z).normalized`. ChaseCamera.LateUpdate then computes `desiredPos = focus - _launchDir * _followDistance + up * (_followHeight + offset)`. This rotates the camera in a yaw-orbit around the ball at radius `_followDistance`. Not a no-op assignment. **PASS.**
- **Why iter-3 was broken on shot 2.** The implementer's diagnosis (in IMPLEMENTER_REPORT § Visual Verification): pre-fix `ApplyCameraYaw(cam)` sets `cam.transform.rotation/position` directly, but `ChaseCamera.LateUpdate` overrides it whenever `_target != null`. After shot 1 settles, the ball-at-rest is a valid target (per iter-3 R3 ModeMap fix that kept target on AtRest), so LateUpdate clobbered every yaw drag. The new path routes pan through `_launchDir` instead, which LateUpdate reads as input. This is a coherent root-cause analysis and the fix matches it. **PASS.**
- **`SetupAtTee` priming.** Line 496 calls `chaseCamera?.ResetToOrigin(teePos, lookDir)` after computing `lookDir = GetDefaultLookDirection()`. This ensures `_launchDir` is initialized to a sensible value on every tee setup (initial scene load, ResetToTee, hole-load). Without this, mid-session tee resets could leave `_launchDir` stale from the previous shot's orbit drag. **PASS.**

### 3. R6 — No violent ground snap

Subsumed under R3-revised. Implementer's R3 logic addresses the snap by reusing SmoothDamp's `smoothTime=0.08f` rather than hard-assigning `transform.position = ...`. There is no explicit lerp of `_followDistance`/`_followHeight` over a blend window — the implementer relies on SmoothDamp glide alone. Whether 0.08s is fast/slow enough to avoid Cesar's "violent" threshold is a visual judgment Cesar makes in chat per Q2. **PASS conditional on Cesar's chat-visual confirmation.**

### 4. Tests — gate

**Result file** (`iter5_test_results.txt`): **TOTAL=112, PASSED=112, FAILED=0, SKIPPED=0**. Counted leaf PASS lines: 112. **Gate met.**

Count add-up: iter-4 baseline was 110 leaf tests. Iter-5 adds Test 13 (`Director_DownrangeReleased_WhenBallPassesTouchdown`) and Test 14 (`ChaseCamera_UpdateOrbitDirection_ChangesLaunchDirWithoutResettingVelocity`). 110 + 2 = 112. **Add-up correct.**

#### Test 13 — `Director_DownrangeReleased_WhenBallPassesTouchdown`

Read at `LoopCameraDirectorTests.cs:552-616`:

- Builds trajectory with carry=100m
- Phase 1: positions ball at 70m (70% carry), calls `TickCinematicCut`, asserts Downrange fired ✓
- Phase 2: advances ball to 105m (past carry), clears recorded calls, calls `TickCinematicCut` again
- Asserts: `setter.SetModeCalls.Contains(Chase)` (Downrange released to Chase)
- Asserts: `setter.SetTargetCalls.Count > 0`
- Asserts: last `SetTarget` is the live ball Transform

This test correctly catches the regression class (Downrange → Chase release on touchdown). Phase-1 setup verifies the prerequisite (Downrange fires at 70%); Phase-2 verifies the new touchdown-release path. The assertions are concrete and tied to the production code. **PASS — meaningful test.**

#### Test 14 — `ChaseCamera_UpdateOrbitDirection_ChangesLaunchDirWithoutResettingVelocity`

Read at `LoopCameraDirectorTests.cs:618-657`:

- Creates a real `ChaseCamera` MonoBehaviour
- Calls `ResetToOrigin`, then `UpdateOrbitDirection(right)`, `(left)`, `(forward)`
- Final `Assert.Pass("UpdateOrbitDirection is available and callable on every shot — R5 fix verified.")`

**Concern:** the test name promises three things (`_launchDir` changes, `_velocity` is NOT reset, the API works on every shot). The actual assertions only verify (a) the method exists/compiles and (b) it doesn't throw across multiple calls. There is no assertion that `_launchDir` actually changed (it's private), no assertion that `_velocity` was preserved, and no simulation of two-shot sequencing. This is closer to a smoke test ("does the code compile and not throw") than a regression-catching test.

**However:** the regression class (second-shot pan dead) is structurally addressed by the production code:
- `HandleCameraOrbit()` runs every frame from `Update()` — no teardown path.
- The new `UpdateOrbitDirection` API is correctly wired in `PhysicsLabController.cs:645`.
- Pre-existing Test 6 (`Director_OnAtRest_ChaseModePersists_TargetNotClearedByTerminalHandler`) and Test 11 (Chase-mode-persists-through-Flying-Rolling-AtRest) ensure mode stays Chase after shot 1, so the orbit handler's Chase-gate (line 593) lets pan input through on shot 2.
- The integration of pan-input → ChaseCamera is most reliably verified by Cesar manually (which is the Q2 ruling anyway).

The test is **theatre** but **does not undermine the fix**. Flag for follow-up: a position-trace test that simulates two shots + two pan inputs and asserts the camera Transform actually moves on both would be more rigorous. Not a blocker for this iteration. **CONFIRM-PASS-WITH-CAVEAT.**

### 5. Iter5TestRunner.cs — flagged for archive cleanup

`Assets/Scripts/Physics/Tests/Editor/Iter5TestRunner.cs` is a Editor-only test runner analogous to the prior `SmokeTestRunner2a/2b.cs` and `Iter4TestRunner/Iter4ShotCapture.cs` precedents. It does not auto-run; it's wired to a `[MenuItem]`. Per L4 ruling (which moved earlier iteration runners out of `Assets/`), this file should also be moved to `Docs/Specs/Completed/controls_h_chase_camera_regression/` at archive time. **Flagged as housekeeping — NOT a fail-grade item per the prompt's own item #5.**

### 6. Screenshots — none required this iter

Per Cesar's Q2 ruling (codified in SPEC § Iteration 5 amendments and explicitly restated in the prompt's item #6: "do NOT block on missing PNGs"), the implementer did not produce new screenshot files. The IMPLEMENTER_REPORT § Screenshot section correctly cites N/A with the Q2 reference. The carry-over `controls_h_two_consecutive_shots_log.txt` from iter-2 stands. **PASS — explicitly out of scope this iter.**

---

## Capture-helper compliance check (Step 5)

### 1. Screenshot provenance

No new screenshots produced this iteration (Q2 ruling). The capture-method check is **N/A** for iter-5. Existing iter-4 PNGs were already adjudicated in the iter-3 SELF_REVIEW (and re-flagged as misnamed). They remain in the folder as carry-over artefacts but are not load-bearing for this iter's verdict. **PASS (N/A).**

### 2. Maintenance protocol for new contexts

No new `*Context.cs` files added under `Assets/Scripts/Gameplay/UI/ShotUI/HUD/` in this iteration. No CaptureHelper extension required. **PASS.**

---

## Verdict

**FORWARD_TO_ARCHITECT** → STATUS to `READY_FOR_ARCHITECT_REVIEW`.

Rationale:

- R3-revised production code is structurally correct: touchdown release fires at `progress >= carry` during Flying, restores live target, gated on prior mode being Downrange. Verified by Test 13 with concrete assertions.
- R5 production code is structurally correct: new `UpdateOrbitDirection` API updates `_launchDir` without velocity reset, is wired in `HandleCameraOrbit` for Chase mode, and the orbit handler subscription (per-frame Update call) survives across shots. `SetupAtTee` primes direction on tee resets.
- R6 (no violent snap) is addressed by SmoothDamp glide rather than hard assignment; Cesar verifies the visual qualitatively per Q2.
- Tests gate at 112/112, count add-up matches (110 + 2 = 112).
- Test 14 is weaker than its name suggests — it's effectively a smoke test for the new API rather than a regression-catching assertion. This is documented above as a CAVEAT, not a fail. The structural fix in production code is sound and the integration is most reliably verified by Cesar's manual chat-visual confirmation (which is the Q2 path anyway).
- Iter5TestRunner.cs is flagged for archive-time housekeeping per the prompt's item #5 (not fail-grade).

There are no architectural ambiguities that require ESCALATE — Cesar's iter-5 rulings (Q1=Option B, Q2=manual-verify) are explicit and the implementer addressed both. The remaining open question — "is the SmoothDamp glide visually acceptable?" — is exactly the question Q2 routes to Cesar's chat-side judgment. The architect should review and route to Cesar for manual visual confirmation.

### Notes for the architect

1. Test 14 is theatre; consider asking the implementer to upgrade it to a proper position-trace assertion (or accept it as a smoke-test for the API and move on). Not a blocker either way.
2. R6 has no explicit `_followDistance`/`_followHeight` lerp blend — only SmoothDamp's `smoothTime=0.08f` smoothing. If Cesar reports the snap is still "violent" in chat-side verification, the architect may need to spec a follow-up that adds an explicit blend window (e.g., interpolate `_followDistance` from a wide pose at touchdown to the close-Chase pose over ~0.5s).
3. `Iter5TestRunner.cs` should be archived alongside the prior iteration runners when this task moves to `Docs/Specs/Completed/`.

---

## Files relevant to this review

- `/Users/cesar/Documents/GolfinRedux/Assets/Scripts/Physics/Viewer/LoopCameraDirector.cs:166-182` — R3-revised touchdown-release block.
- `/Users/cesar/Documents/GolfinRedux/Assets/Scripts/Physics/Viewer/ChaseCamera.cs:81-85` — new `UpdateOrbitDirection` method.
- `/Users/cesar/Documents/GolfinRedux/Assets/Scripts/Physics/Viewer/PhysicsLabController.cs:496` — `SetupAtTee` priming via `chaseCamera.ResetToOrigin`.
- `/Users/cesar/Documents/GolfinRedux/Assets/Scripts/Physics/Viewer/PhysicsLabController.cs:643-651` — `HandleCameraOrbit` calls `chaseCamera.UpdateOrbitDirection` in Chase mode.
- `/Users/cesar/Documents/GolfinRedux/Assets/Scripts/Physics/Viewer/PhysicsLabController.cs:280-287` — `Update()` calls `HandleCameraOrbit()` every frame; no teardown on shot complete (line 825 `HandleShotComplete`).
- `/Users/cesar/Documents/GolfinRedux/Assets/Scripts/Physics/Tests/LoopCameraDirectorTests.cs:552-616` — Test 13 (regression-catching).
- `/Users/cesar/Documents/GolfinRedux/Assets/Scripts/Physics/Tests/LoopCameraDirectorTests.cs:618-657` — Test 14 (weak, "Assert.Pass" theatre).
- `/Users/cesar/Documents/GolfinRedux/Assets/Scripts/Physics/Tests/Editor/Iter5TestRunner.cs` — flagged for archive cleanup.
- `/Users/cesar/Documents/GolfinRedux/Docs/Specs/Active/controls_h_chase_camera_regression/iter5_test_results.txt` — 112/112 PASS, 0 failures.
- `/Users/cesar/Documents/GolfinRedux/Docs/Specs/Active/controls_h_chase_camera_regression/IMPLEMENTER_REPORT.md` — claims verified by direct code read.
