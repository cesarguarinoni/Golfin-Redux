# SPEC AMENDMENT — Iteration 6: Single-Writer Chase, Kill Downrange

> **This is an amendment to `controls_h_chase_camera_regression`.** Prior iterations 1–5 documented in original SPEC.md and SITREP_FOR_ARCHITECT.md. This amendment supersedes all unresolved Q1/Q2 design questions in the SITREP. Implementer reads THIS amendment for the iter-6 work definition.

**Architect-locked 2026-05-08.** Ships the minimum reliable fix. No options. No tuning knobs. No new modes.

## What's wrong (one paragraph)

Two writers fight for `cam.transform.position` every frame. `PhysicsLabController.ApplyCameraYaw` writes it from `Update()` via HandleCameraOrbit. `ChaseCamera.LateUpdate` writes it again with different math. LateUpdate runs after Update, so LateUpdate wins — except when ChaseCamera early-returns (first shot, no `_target` yet), in which case ApplyCameraYaw wins. That's why first-shot Aiming worked and shot-2+ Aiming didn't. The cinematic cut adds a third moving part (Downrange position) that fights the other two during transitions.

**Fix:** one writer. ChaseCamera owns position. Everything else writes only to ChaseCamera's inputs. Cinematic cut is removed entirely.

## What lands

### A. ChaseCamera changes (`Assets/Scripts/Physics/Viewer/ChaseCamera.cs`)

**A1. Remove the early-return at line 93.** Currently:
```csharp
if (_target == null && _mode == Mode.Chase) return;
```
Delete this line. ChaseCamera always runs LateUpdate's math, even with no target.

**A2. Add `SetAimDirection` public method:**
```csharp
/// <summary>
/// Updates the aim/launch direction used by Chase mode framing.
/// Called every frame by PhysicsLabController.HandleCameraOrbit while the player
/// is panning during Aiming. Also called by ResetToOrigin at shot fire time.
/// Pure setter — does not touch transform.position.
/// </summary>
public void SetAimDirection(Vector3 dir)
{
    var flat = new Vector3(dir.x, 0f, dir.z);
    if (flat.sqrMagnitude > 0.0001f)
        _launchDir = flat.normalized;
}
```

**A3. No other changes to ChaseCamera.** Existing Chase math at line 142–143 already does the right thing once A1 is in place — focus falls back to `_shotOrigin` when `_target == null`.

### B. PhysicsLabController changes (`Assets/Scripts/Physics/Viewer/PhysicsLabController.cs`)

**B1. Delete `ApplyCameraYaw` method entirely.** Find the method (currently around line 504). Remove it.

**B2. Rewrite `HandleCameraOrbit` to write only to ChaseCamera inputs.** The new body, in full:

```csharp
void HandleCameraOrbit()
{
    // Block orbit while any action-button selector overlay is open.
    if (Golfin.Gameplay.UI.ShotUI.OtherButtonsFader.AnyOverlayOpen)
    {
        _orbitDragActive = false;
        return;
    }

    if (_shotController != null && _shotController.IsExternalDragActive) return;

    // Orbit only makes sense in Chase mode; Overhead/Ground manage themselves.
    if (chaseCamera != null && chaseCamera.CurrentMode != ChaseCamera.Mode.Chase) return;

    // §controls_h iter-6: When ball animation finishes (falling edge of isPlaying),
    // update the orbit center to the resting ball position so subsequent panning
    // orbits around the new resting position. ChaseCamera owns the actual position
    // write — we only update _cameraYaw and seed ChaseCamera's aim direction.
    bool isPlaying = ballAnimator != null && ballAnimator.IsPlaying;
    if (_prevBallPlaying && !isPlaying)
    {
        if (ballAnimator?.CurrentBall != null)
            _orbitCenter = ballAnimator.CurrentBall.position;
    }
    _prevBallPlaying = isPlaying;
    if (isPlaying) return;

    var mouse = Mouse.current;
    if (mouse == null) return;

    bool pressing = mouse.leftButton.isPressed;
    if (pressing && !_orbitDragActive)
    {
        bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        if (overUI) return;
        _orbitDragActive = true;
    }
    if (!pressing)
    {
        _orbitDragActive = false;
        return;
    }

    float dx = mouse.delta.x.ReadValue();
    if (Mathf.Abs(dx) < 0.5f) return;

    _cameraYaw += dx * _orbitSensitivity * Mathf.Deg2Rad;

    // Compute the new aim direction from yaw (yaw=0 → +X forward).
    Vector3 lookDir = new Vector3(Mathf.Cos(_cameraYaw), 0f, Mathf.Sin(_cameraYaw));

    // Update ShotController so the aim cone follows the new yaw.
    if (_shotController != null)
        _shotController.CameraHeadingRadians = _cameraYaw;

    // §controls_h iter-6: ChaseCamera is the single writer of cam.transform.position.
    // We feed it the new aim direction; its LateUpdate handles framing relative to
    // the current target (or _shotOrigin fallback when no target).
    chaseCamera?.SetAimDirection(lookDir);
}
```

Note: the `if (chaseCamera?.SetTarget(null))` calls and direct camera writes that existed in the old falling-edge block are gone — Director owns target lifecycle, and ChaseCamera owns position.

**B3. Modify `SetupAtTee` to seed ChaseCamera state.** Find the existing `SetupAtTee()` method. After the line `if (ballAnimator != null) ballAnimator.PlaceAtRest(teePos);`, ADD:

```csharp
// §controls_h iter-6: seed ChaseCamera so first-shot Aiming framing is correct.
// Without this, ChaseCamera._shotOrigin = (0,0,0) and _launchDir = forward default,
// so the camera renders at world origin until the first shot fires.
if (chaseCamera != null)
{
    chaseCamera.SetTarget(ballAnimator?.CurrentBall);
    chaseCamera.ResetToOrigin(teePos, GetDefaultLookDirection());
}
```

This goes BEFORE the existing line that sets `_orbitCenter = teePos;`.

**B4. Modify `PlaceBallAt` to seed ChaseCamera state.** Find `PlaceBallAt(Vector3 worldPos, ...)`. After the line `if (ballAnimator != null) ballAnimator.PlaceAtRest(pos);`, ADD:

```csharp
// §controls_h iter-6: same as SetupAtTee — seed ChaseCamera with new resting position.
if (chaseCamera != null)
{
    chaseCamera.SetTarget(ballAnimator?.CurrentBall);
    chaseCamera.ResetToOrigin(pos, GetDefaultLookDirection());
}
```

### C. LoopCameraDirector changes (`Assets/Scripts/Physics/Viewer/LoopCameraDirector.cs`)

**C1. Stub out `TickCinematicCut`.** Replace the entire method body with:

```csharp
public void TickCinematicCut()
{
    // §controls_h iter-6: cinematic cut deleted. Chase runs the entire shot.
    // Method preserved as a no-op for tests that may call it; can be removed
    // entirely after the iter-6 test suite lands.
}
```

Delete the `_lastCinematicDiagTime` field — it's no longer used.

**C2. ModeMap stays as-is.** The current dispatch (Flying→Chase, Rolling→Chase, AtRest→Chase, InCup→CupZoom, OB→OBFreeze, Aiming→null) is correct under the new model.

**C3. The Director's `SetTarget(null)` on InCup/OB stays.** Those modes don't need a follow target. Chase/AtRest target stays on the ball (existing iter-3 behavior, preserved).

**C4. Delete the `R2-DIAG` log statements.** They reference dead code paths after C1.

### D. Cleanup

**D1. Remove unused fields.** `_lastCinematicDiagTime`, `cinematicCutAtCarryFraction`, `minCarryForCinematicMeters`, `downrangePastLandingMeters`, `downrangeHeightMeters` SerializeFields in LoopCameraDirector — keep them for now (deleting touches the public properties exposed for tests). They're harmless dead state. Mark as `[Obsolete]` in a follow-up if it bothers anyone.

**D2. iter-5 Test 13** (`Director_DownrangeReleased_WhenBallPassesTouchdown`) — DELETE. The release-at-touchdown logic is gone. Test is now testing nothing.

## Tests

**Required new tests** (4 tests, replacing iter-5 Test 13):

```csharp
[Test]
public void ChaseCamera_LateUpdateRunsWithNullTarget_UsesShotOriginAsFocus()
{
    // Verify A1: removing the early-return means Chase math runs even with null target.
    var go = new GameObject("ChaseCam");
    var cam = go.AddComponent<ChaseCamera>();
    cam.SetMode(ChaseCamera.Mode.Chase);
    cam.SetTarget(null);
    cam.ResetToOrigin(new Vector3(10f, 0f, 0f), Vector3.forward);
    
    // Position the camera somewhere arbitrary first.
    cam.transform.position = new Vector3(999f, 999f, 999f);
    
    // Drive a few LateUpdates (SmoothDamp converges).
    for (int i = 0; i < 60; i++) cam.SendMessage("LateUpdate");
    
    // After convergence, camera should be behind shot origin (10,0,0) by FollowDistance,
    // up by FollowHeight. Within tolerance for SmoothDamp residual.
    var expected = new Vector3(10f, 0f, 0f) - Vector3.forward * cam.FollowDistance + Vector3.up * cam.FollowHeight;
    Assert.That(Vector3.Distance(cam.transform.position, expected), Is.LessThan(0.5f),
        $"Expected camera near {expected}, got {cam.transform.position}");
    
    Object.DestroyImmediate(go);
}

[Test]
public void ChaseCamera_SetAimDirection_UpdatesChasePose()
{
    // Verify A2: SetAimDirection actually rotates the chase pose.
    var go = new GameObject("ChaseCam");
    var cam = go.AddComponent<ChaseCamera>();
    cam.SetMode(ChaseCamera.Mode.Chase);
    cam.SetTarget(null);
    cam.ResetToOrigin(Vector3.zero, Vector3.right);  // initial forward = +X
    cam.transform.position = Vector3.zero;
    for (int i = 0; i < 60; i++) cam.SendMessage("LateUpdate");
    var posWithRightAim = cam.transform.position;
    
    cam.SetAimDirection(Vector3.forward);  // change to +Z
    for (int i = 0; i < 60; i++) cam.SendMessage("LateUpdate");
    var posWithForwardAim = cam.transform.position;
    
    Assert.That(Vector3.Distance(posWithRightAim, posWithForwardAim), Is.GreaterThan(1f),
        "Camera position should move noticeably when aim direction changes 90°.");
    
    Object.DestroyImmediate(go);
}

[Test]
public void Director_NeverEntersDownrange_DuringFlying()
{
    // Verify C1: cinematic cut is gone. No matter how long a shot flies, Director
    // does not promote Chase to Downrange.
    var (director, modeSetter, controllerStub) = DirectorFactory.Create();
    
    // Drive Aiming→Flying.
    controllerStub.RaiseStateChange(BallState.Aiming, BallState.Flying);
    
    // Simulate a long shot: 500 frames of cinematic-cut tick.
    for (int i = 0; i < 500; i++) director.TickCinematicCut();
    
    // Mode must be Chase, never Downrange.
    Assert.That(modeSetter.CurrentMode, Is.EqualTo(ChaseCamera.Mode.Chase));
    Assert.That(modeSetter.ModeHistory, Has.None.EqualTo(ChaseCamera.Mode.Downrange));
}

[Test]
public void Director_AtRestKeepsTargetOnBall()
{
    // Verify iter-3 R3 preservation: AtRest does NOT clear target. Chase keeps tracking.
    var (director, modeSetter, controllerStub) = DirectorFactory.Create();
    var ballGO = new GameObject("Ball");
    controllerStub.SetCurrentBall(ballGO.transform);
    
    controllerStub.RaiseStateChange(BallState.Aiming, BallState.Flying);
    Assert.That(modeSetter.GetTarget(), Is.EqualTo(ballGO.transform), "Target armed at flight start.");
    
    controllerStub.RaiseStateChange(BallState.Flying, BallState.Rolling);
    controllerStub.RaiseStateChange(BallState.Rolling, BallState.AtRest);
    
    Assert.That(modeSetter.GetTarget(), Is.EqualTo(ballGO.transform),
        "Target should NOT be cleared on AtRest — Chase tracks the resting ball into Aiming.");
    
    Object.DestroyImmediate(ballGO);
}
```

**Test gate target:** **245 → 248/248 PASS, 0 IGNORED.** (245 baseline post-revert, +4 new tests, −1 deleted iter-5 Test 13 = 248 total.)

If iter-5 Test 13 was actually removed as part of the revert, the math is 245 + 4 = 249. Implementer confirms in IMPLEMENTER_REPORT which baseline applies.

## Manual verification (REQUIRED per Lesson O)

After code lands, before review, drive the lab manually for ALL 5 cases. Write a 1-2 sentence content-sanity description for each in IMPLEMENTER_REPORT.md § Visual Verification:

1. **First-shot Aiming pan.** Load Hole 1, do not fire, drag mouse left/right. Camera orbits around the tee. Aim cone follows.
2. **Driver shot, full power.** Fire driver. Camera tracks ball through entire flight. Smoothly tracks roll. Settles on resting ball.
3. **Iron shot, half power.** Fire iron. Camera tracks ball through short flight + roll. No violent transitions, no parking.
4. **Shot 2 Aiming pan.** After shot 1 settles, drag mouse left/right. Camera orbits around the resting ball at its new position. Aim cone follows.
5. **OB shot.** Fire driver into water on Hole 6. Camera tracks ball during flight, then OBFreeze locks at hazard pivot.

If ANY of these 5 fails, do NOT mark spec PASS. Either escalate `IMPLEMENTER_BLOCKED` or revert.

## Out of scope (explicit deferrals)

These are NOT shipped this iteration. Each is a separate future task:

- **Apex zoom-out** (Chase pose scales with ball altitude). Cesar requested it; deferred to make this fix bulletproof first. After this lands, Cesar plays it; if the camera feels flat at apex, file `controls_i_chase_apex_zoom` as a small follow-up.
- **Cinematic cut at landing.** Deleted entirely. If we ever want it back, it'll be a from-scratch design with proper transition blending — not a bolt-on to Chase.
- **Director cleanup of dead Downrange-related fields.** Cosmetic; leave for housekeeping pass.
- **Tighter chase tuning** (`_followDistance`, `_followHeight`, `smoothTime`). Already serialized; Cesar can tune in inspector without code changes.

## Hard rules

1. **Single writer.** After this fix, `cam.transform.position` is written ONLY in `ChaseCamera.LateUpdate`. Do not add any other writers. If a future feature needs to position the camera, it MUST go through ChaseCamera's input methods.
2. **Do NOT modify** `BallStateMachine.cs`, `BallSimulation.cs`, `Trajectory.cs`, `AeroModel.cs`, `BallAnimator.cs`, any aero CSV.
3. **Do NOT modify** any test currently in PASS state outside `LoopCameraDirectorTests.cs` (where the 4 new tests land + iter-5 Test 13 deletes).
4. **Do NOT add** new modes, new transitions, new SerializeFields, new public methods beyond `ChaseCamera.SetAimDirection`. Anything else is scope creep.
5. **Do NOT skip the 5 manual verification cases.** Per Lesson O — written by this very task family. Runtime event-dispatch evidence is not sufficient. Manual play and write descriptions.
6. **If a test breaks unexpectedly, escalate IMPLEMENTER_BLOCKED.** Do NOT "fix" failing tests by editing them. The previous iterations had this failure mode; do not repeat.

## Definition of Done

- A1, A2 in ChaseCamera. A3 verified (no other changes).
- B1, B2, B3, B4 in PhysicsLabController.
- C1, C2, C3, C4 in LoopCameraDirector.
- iter-5 Test 13 deleted. 4 new tests added. Gate at **248/248 or 249/249 PASS, 0 IGNORED** (baseline confirmed in IMPLEMENTER_REPORT).
- 5 manual content-sanity descriptions in IMPLEMENTER_REPORT § Visual Verification, all passing.
- 1 file artifact: `Docs/Specs/Active/controls_h_chase_camera_regression/screenshots/iter6_aiming_pan_shot2.png` showing post-shot-1 camera orbiting around the resting ball (proves Q2 fixed).
- All 5 manual cases pass per Cesar's verification.
- Cesar reviews and approves.

## Why this WILL work

The previous iterations failed because they tried to keep both writers and arbitrate between them. Iter-3 made AtRest keep the target (so LateUpdate runs in Aiming) but didn't remove ApplyCameraYaw — two writers still fighting. Iter-5 added `UpdateOrbitDirection` on ChaseCamera but didn't remove ApplyCameraYaw — same fight, more code. Each iteration made the conflict subtler instead of removing it.

This iteration removes one writer. There's no longer a conflict to arbitrate. ChaseCamera reads `_target`, `_shotOrigin`, `_launchDir`, computes a position, writes it. PhysicsLabController updates `_launchDir` via `SetAimDirection`. Director updates `_target` via `SetTarget`. SetupAtTee/PlaceBallAt seed both. There is exactly one place where `cam.transform.position = ...` happens, and exactly one path of inputs that drives it.

The Downrange deletion is independent of the single-writer fix, but compounds the simplification: zero mode transitions during flight means zero transition blending, which was where iter-5's "violent ground snap" lived.

If after this fix Cesar still sees broken camera behavior, the bug is somewhere we have not yet traced — not in the writer-conflict pattern. That's a much narrower search.
