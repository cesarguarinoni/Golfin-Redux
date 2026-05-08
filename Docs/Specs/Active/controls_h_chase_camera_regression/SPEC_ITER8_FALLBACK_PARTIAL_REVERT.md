# SPEC AMENDMENT — Iteration 8 (FALLBACK): Partial Revert to Pre-§2b Camera Pattern

> **Conditional fallback for `controls_h_chase_camera_regression`.** Fire ONLY if iter-7 fails Cesar's manual verification. If iter-7 ships clean, this spec is archived without execution.
> 
> **Architect-locked 2026-05-08, post-iter-6-reject. Pre-authorized fallback.**

## Context

Cesar reports that the chase camera was working as intended on all shots **before** the controls_h phase began. Pre-§2b state had a simple two-writer pattern that didn't conflict because each writer gated on a different condition. §2b/§2g/§controls_h iter-1→6 progressively entangled the camera lifecycle and produced regressions.

This spec reverts to pre-§2b camera behavior **while preserving the architectural work that's correct and worth keeping** (HandleShotResolved order fix, FireInternal SM routing, Lesson O methodology, SPEC template visual-fidelity sub-section, Director's CupZoom/OBFreeze dispatch). The result is a known-working camera with the foundation still in place for future fancy modes.

## Pre-flight check before firing

Verify iter-7 actually failed:
1. Cesar played the lab manually (not script-execute).
2. Cesar saw a visual issue in one or more of the 5 manual cases.
3. Cesar wrote a description of what they saw, not a coordinate dump.

If iter-7 visually passes Cesar's eyeballs, archive this spec. Don't revert away from a working state out of paranoia.

## What this spec does NOT touch

These iter-1→6 changes are KEPT. Do not revert them under any circumstance:

| File | Change | Why kept |
|---|---|---|
| `PhysicsLabController.HandleShotResolved` | Cache `_lastShotOrigin/Dir` + `ballAnimator.Play()` BEFORE `_ballSM.OnTrajectoryComputed` | Original controls_h Option A. Fixes the synchronous-fire-with-stale-cache bug. Correct under all camera architectures. |
| `PhysicsLabController.FireInternal` | Routes through SM (calls `OnTrajectoryComputed`) instead of direct `chaseCamera.SetTarget`/`ResetToOrigin` | Original controls_h Option D. Keeps preset path and touch path in sync. SM dispatch needed for Director. |
| `BallStateMachine.cs` lines 62-66 | Updated docstring "AND after BallAnimator.Play() has spawned the new ball Transform" | Reflects the fixed order. Don't revert the comment to a lie. |
| `Docs/Diagnostics/PIPELINE_LESSONS.md` | Lesson O (dispatch ≠ visual evidence) | Methodology lesson. Stays. |
| `Docs/Specs/Active/_TEMPLATE/SPEC.md` § Smoke evidence | Visual-fidelity sub-section | Methodology guard. Stays. |
| `LoopCameraDirector.cs` `TickCinematicCut` stubbed to no-op | Downrange cinematic deleted | Cesar said ditch Downrange. Stays deleted. |
| `LoopCameraDirector.cs` ModeMap dispatch (CupZoom on InCup, OBFreeze on OB) | Director still dispatches modes for terminal states | These work correctly. Don't touch. |
| `BallStateMachine.cs` source logic | Unchanged from §2a baseline | Don't revert SM. |
| `BallSimulation.cs`, `Trajectory.cs`, `AeroModel.cs`, aero CSV | Unchanged | Out of scope. |

## What this spec reverts

### A. ChaseCamera.cs — restore null-target early-return

Add back the early-return at the top of `RunLateUpdateLogic`, before the focus calculation:

```csharp
void RunLateUpdateLogic(float dt)
{
    // Pre-§2b behavior: when no target and in Chase mode, do nothing.
    // PhysicsLabController.ApplyCameraYaw owns the camera position during Aiming
    // (when Director has cleared the target on AtRest/InCup/OB).
    if (_target == null && _mode == Mode.Chase) return;

    // ... rest of method unchanged ...
}
```

### B. ChaseCamera.cs — remove iter-6/7 additions

If iter-6 landed: delete `SetAimDirection(Vector3)` method.

If iter-7 also landed: delete `_isAiming` field, `_aimDistance` / `_aimHeight` / `_aimLookAheadMeters` / `_aimLookUpMeters` SerializeFields, `SetAiming(bool)` method, and the `_isAiming` branch in Chase math (revert to single follow-only framing).

Final Chase math is the original single-parameter form:

```csharp
default: // Chase
    desiredPos = focus - _launchDir * _followDistance + Vector3.up * (_followHeight + FollowHeightOffset);
    desiredRot = Quaternion.LookRotation(focus - desiredPos);
    break;
```

`_followDistance = 3f` and `_followHeight = 1.8f` (iter-3 R1 values) **stay** — those are correct for ball-tracking during Flying.

### C. ChaseCamera.cs — keep test seam infrastructure

Keep the `RunLateUpdateLogic(float dt)` extraction and `internal FrameCamera(float dt)` test hook. They're harmless and tests need them.

### D. PhysicsLabController.cs — restore ApplyCameraYaw

Restore the deleted method (the body matches pre-iter-6):

```csharp
// Pre-§2b: ChaseCamera owns position only when _target != null (Flying/Rolling).
// During Aiming when Director has cleared _target, ApplyCameraYaw writes the camera
// transform directly. Two writers don't conflict because each gates on a different
// condition (target null vs ball not playing).
void ApplyCameraYaw(Camera cam)
{
    Vector3 lookDir = new Vector3(Mathf.Cos(_cameraYaw), 0f, Mathf.Sin(_cameraYaw));
    cam.transform.position = _orbitCenter - lookDir * 8f + Vector3.up * 3f;
    cam.transform.LookAt(_orbitCenter + lookDir * 3f + Vector3.up * 0.5f);
}
```

### E. PhysicsLabController.cs — restore HandleCameraOrbit's ApplyCameraYaw call

In `HandleCameraOrbit`, after `_cameraYaw += dx * _orbitSensitivity * Mathf.Deg2Rad;`, restore the camera write:

```csharp
_cameraYaw += dx * _orbitSensitivity * Mathf.Deg2Rad;

if (_shotController != null)
    _shotController.CameraHeadingRadians = _cameraYaw;

Camera cam = chaseCamera?.GetComponent<Camera>();
if (cam != null) ApplyCameraYaw(cam);
```

**Remove** the iter-6 line `chaseCamera?.SetAimDirection(lookDir);`.

### F. PhysicsLabController.cs — remove iter-6 SetupAtTee / PlaceBallAt seeding

In `SetupAtTee()`, DELETE the iter-6 block that calls `chaseCamera.SetTarget(...)` and `chaseCamera.ResetToOrigin(...)`. Restore the pre-iter-6 behavior where SetupAtTee only writes `_orbitCenter = teePos` and `_cameraYaw = ...` and `_shotController.CameraHeadingRadians = _cameraYaw`.

In `PlaceBallAt()`, DELETE the matching block. Same restoration.

### G. PhysicsLabController.cs — remove iter-6 Start() priming

In `Start()`, KEEP the iter-3 R4 priming (`_cameraYaw = Mathf.Atan2(...)` and `_shotController.CameraHeadingRadians = _cameraYaw`). REMOVE the iter-6 line `chaseCamera?.SetAimDirection(r4dir);`.

If iter-7 also added a bootstrapping camera-pose snap in Start(), DELETE that entire block.

### H. LoopCameraDirector.cs — revert iter-3 "AtRest keeps target"

In `HandleStateChanged`, change the terminal-state target-clearing condition to include AtRest:

```csharp
// Pre-iter-3 behavior: clear target on ALL terminal states. Aiming-camera owner
// (ApplyCameraYaw) takes over via ChaseCamera.LateUpdate's null-target early-return.
if (change.Next == BallState.AtRest
 || change.Next == BallState.InCup
 || change.Next == BallState.OB)
{
    setter.SetTarget(null);
}
```

Update the comment to remove the iter-3 R3 "do NOT clear on AtRest" rationale.

### I. LoopCameraDirector.cs — keep Rolling re-arm

The iter-3 R3 Rolling re-arm:

```csharp
if (change.Next == BallState.Rolling)
{
    if (ctrl != null && ctrl.CurrentBall != null)
        setter.SetTarget(ctrl.CurrentBall);
}
```

**Keep this.** Defensive. Doesn't conflict with the AtRest clear since Rolling fires before AtRest in the falling-edge drain.

### J. PhysicsLabController.cs — keep falling-edge orbit center update

The block in `HandleCameraOrbit`:

```csharp
bool isPlaying = ballAnimator != null && ballAnimator.IsPlaying;
if (_prevBallPlaying && !isPlaying)
{
    if (ballAnimator?.CurrentBall != null)
        _orbitCenter = ballAnimator.CurrentBall.position;
}
_prevBallPlaying = isPlaying;
if (isPlaying) return;
```

**Keep this verbatim.** Critical for shot-2 Aiming: orbit center moves to the new ball-rest position so ApplyCameraYaw orbits around the right point.

## Tests

**Delete (testing reverted behavior):**
- Test 14 `ChaseCamera_LateUpdateRunsWithNullTarget_UsesShotOriginAsFocus` — under Option B, LateUpdate DOES early-return on null target. Test assertion is now wrong-direction.
- Test 15 `ChaseCamera_SetAimDirection_UpdatesChasePose` — `SetAimDirection` is deleted.
- Test 17 `Director_AtRestKeepsTargetOnBall` — Director now CLEARS target on AtRest.
- iter-7's `ChaseCamera_SetAiming_TrueUsesAimFraming` and `ChaseCamera_SetAiming_FalseUsesFollowFraming` (if iter-7 landed) — `SetAiming` and Aim params are deleted.

**Add:**

```csharp
[Test]
public void ChaseCamera_LateUpdate_EarlyReturnsWhenNullTargetInChaseMode()
{
    // Verify A: with null target in Chase mode, LateUpdate does NOT modify transform.
    var go = new GameObject("ChaseCam");
    var cam = go.AddComponent<ChaseCamera>();
    cam.SetMode(ChaseCamera.Mode.Chase);
    cam.SetTarget(null);
    cam.ResetToOrigin(Vector3.zero, Vector3.right);
    
    Vector3 initialPos = new Vector3(123f, 456f, 789f);
    cam.transform.position = initialPos;
    
    for (int i = 0; i < 60; i++) cam.FrameCamera(1f / 60f);
    
    Assert.That(cam.transform.position, Is.EqualTo(initialPos),
        "LateUpdate should early-return on null target in Chase mode; transform should be unchanged.");
    
    Object.DestroyImmediate(go);
}

[Test]
public void Director_AtRest_ClearsTarget()
{
    // Verify H: Director clears _target on AtRest entry.
    var (director, modeSetter, controllerStub) = DirectorFactory.Create();
    var ballGO = new GameObject("Ball");
    controllerStub.SetCurrentBall(ballGO.transform);
    
    controllerStub.RaiseStateChange(BallState.Aiming, BallState.Flying);
    Assert.That(modeSetter.GetTarget(), Is.EqualTo(ballGO.transform), "Target armed at flight start.");
    
    controllerStub.RaiseStateChange(BallState.Flying, BallState.Rolling);
    controllerStub.RaiseStateChange(BallState.Rolling, BallState.AtRest);
    
    Assert.That(modeSetter.GetTarget(), Is.Null,
        "Target should be CLEARED on AtRest — ApplyCameraYaw owns Aiming-camera position.");
    
    Object.DestroyImmediate(ballGO);
}
```

**Test 16** `Director_NeverEntersDownrange_DuringFlying` stays unchanged — Downrange is still gone.

**Test gate target:** **244/244 PASS, 0 IGNORED.**

Math from iter-6 baseline (246): 246 − 3 (Tests 14, 15, 17) + 1 (early-return test) + 1 (AtRest clear test, replacing Test 17) = 245. Wait let me recount.

Actually: 246 − 3 deleted + 2 added = 245. Test 16 count includes the kept tests. Let me just say: **target gate = 245/245**, implementer confirms exact baseline in IMPLEMENTER_REPORT and adjusts.

If iter-7 also landed (248 baseline): 248 − 5 deleted + 2 added = 245. Same target.

## Manual verification

Same 5 cases as iter-7. Cesar plays the lab manually. The expected visual outcomes match pre-§2b behavior:

1. **First-shot Aiming.** Camera 8m behind tee, 3m up, looking down the fairway toward the green. Ball appears in lower-center. Fairway fills most of view.
2. **First-shot pan.** Camera orbits around the tee. Fairway view rotates.
3. **Driver flight.** Camera tightens to 3m / 1.8m, looking AT the ball. Tracks through the air.
4. **Driver at-rest.** Camera "snaps" to Aim framing (8m back, 3m up, look-ahead) once the ball stops. (Note: this snap is intentional under Option B — the architecture trades smoothness for simplicity. If the snap is visually objectionable, that's a future polish item, not a regression.)
5. **Shot 2 pan.** Camera orbits around the resting ball at the new position. Same feel as case 2 from the new origin.

Cesar's eyeballs are the final gate. Implementer writes visual descriptions, not coordinates.

## Definition of Done

- A: ChaseCamera early-return restored.
- B: iter-6 / iter-7 ChaseCamera additions deleted.
- C: Test seam infrastructure preserved.
- D: ApplyCameraYaw restored.
- E: HandleCameraOrbit calls ApplyCameraYaw, no longer calls SetAimDirection.
- F: SetupAtTee / PlaceBallAt iter-6 seeding blocks deleted.
- G: Start() iter-6 SetAimDirection call deleted; iter-3 R4 priming kept.
- H: Director clears target on AtRest.
- I: Director's Rolling re-arm preserved.
- J: HandleCameraOrbit's falling-edge orbit-center update preserved.
- 3-5 tests deleted, 2 tests added. Test gate at **245/245 PASS, 0 IGNORED** (or whatever baseline math produces — implementer confirms).
- Cesar manually verifies all 5 cases. Visual descriptions in IMPLEMENTER_REPORT § Visual Verification (iter-8).
- Cesar approves.

## What this loses (be honest)

| Capability | Status under Option B |
|---|---|
| Single-writer architectural ideal | LOST — back to two writers |
| Aim framing as explicit named parameters | LOST — back to magic numbers in ApplyCameraYaw (8f / 3f) |
| `chaseCamera.SetAimDirection` API for future use | LOST |
| iter-3 R3 `AtRest keeps target` behavior | LOST |
| Iter-6 / iter-7 architectural cleanups | LOST |
| HandleShotResolved order fix | KEPT |
| FireInternal SM routing | KEPT |
| BallStateMachine docstring update | KEPT |
| Director's CupZoom on InCup | KEPT |
| Director's OBFreeze on OB | KEPT |
| Pipeline Lesson O | KEPT |
| SPEC template visual-fidelity sub-section | KEPT |
| Cinematic cut deletion (ditched Downrange) | KEPT |
| §2c, §2d, §2e, §2f compatibility | KEPT (Director still dispatches modes) |

The architectural ideal of single-writer was correct in principle but proved fragile in practice across two iterations. Two-writers-don't-fight (when each gates on a different condition) is uglier on paper but worked for ~6 months of pre-§2b dev. Sometimes the boring solution is the right one.

## Hard rules

1. **Do NOT revert** any change in the "What this spec does NOT touch" table above. The HandleShotResolved order fix in particular is correct AND independent of camera architecture.
2. **Do NOT skip the manual verification.** Lesson O still applies. Cesar's eyeballs, not coordinate scripts.
3. **Do NOT add new modes** or new SerializeFields beyond what existed pre-§2b. This is a revert, not a redesign.
4. **Do NOT touch** `BallStateMachine.cs`, `BallSimulation.cs`, `Trajectory.cs`, `AeroModel.cs`, `BallAnimator.cs`, or any aero CSV.

## Why this WILL work (if iter-7 doesn't)

Pre-§2b's camera worked for ~6 months across hundreds of shots and many code revisions. The pattern is battle-tested. Option B restores that pattern verbatim while keeping the small set of demonstrably-correct architectural improvements that don't conflict with it.

The risk profile is low because we're returning to a known-good state, not designing a new one. Implementer's job is mechanical revert + a small set of test updates. Cesar's verification is "did it look like it used to look" — the easiest possible question for visual evaluation.

If Option B fails, something is genuinely wrong somewhere outside camera code. That would be a much narrower search than where we've been.
