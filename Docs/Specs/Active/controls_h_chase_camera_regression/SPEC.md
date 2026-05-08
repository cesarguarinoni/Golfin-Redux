# SPEC — `controls_h_chase_camera_regression`

> **Authoritative spec for this task.** Implementer reads this and ONLY this for the work definition. STATUS.md tracks pipeline state. Reports/reviews go in their own files.

## Status

See `STATUS.md`. Architect-locked at SPEC_READY 2026-05-07 17:20 JST.

## Goal

Restore chase-camera visual tracking on the touch/flick shot path. Currently the camera stays static during flight (Driver, Iron, OB), then SmoothDamps to a downrange position at landing on long shots only, and never moves at all on short shots. Root cause is an order-of-operations bug introduced in `controls_g_smoke_followup` (commit `03e6a31e`) where `_ballSM.OnTrajectoryComputed(...)` synchronously fires `Aiming→Flying` BEFORE `_lastShotOrigin/Dir` are cached and BEFORE `BallAnimator.Play()` spawns the new ball — leaving the Director with stale origin/launchDir AND a destroyed target Transform.

This task: reorder the call sequence in `HandleShotResolved` (Option A from NOTES.md), kill the legacy direct chase-camera calls in `FireInternal` (Option D), add an integration test that catches this regression class, and codify the methodology gap that let it ship as a Pipeline Lesson O.

## Reference

- **Architect prep NOTES:** `Docs/Specs/Active/controls_h_chase_camera_regression/NOTES.md` — Code's full diagnosis with line-by-line evidence trace, repair options A/B/C/D, and the four open questions (all four ruled by Architect 2026-05-07 17:15 JST).
- **§2b SPEC (origin of regression):** `Docs/Specs/Completed/loop_v1_2b_camera_transitions/SPEC.md` — the original camera-lifecycle design.
- **`controls_g_smoke_followup` SPEC:** `Docs/Specs/Active/controls_g_smoke_followup/SPEC.md` — the spec that introduced the regression. Its smoke captures used `OnModeChanged` as visual evidence; that's the methodology gap Lesson O codifies.
- **Methodology lessons referenced:**
  - controls_g lesson "Smoke-Runner Timed Waits Are Fragile" in `tasks/lessons.md` — predecessor lesson on capture-trigger reliability
  - §2a Lessons M+N in `Docs/Diagnostics/PIPELINE_LESSONS.md` — file-on-disk evidence protocol

## Background — what exists today

Verified by Architect code walk 2026-05-07 17:00 JST (independent of Code's NOTES.md).

### The buggy sequence in `HandleShotResolved` (`PhysicsLabController.cs:683-759`)

```
689:    _previousTrajectory = trajectory;            // ✅ fresh
        ...
692:    _ballSM?.OnTrajectoryComputed(...);          // 🔥 fires Aiming→Flying synchronously
        //   ↳ BallStateMachine.cs:222-229 (verified): State = Flying;
        //                                              OnStateChanged?.Invoke(first);
        //   ↳ Director.HandleStateChanged (verified at LoopCameraDirector.cs:179-234):
        //        change.Next == Flying && change.Previous == Aiming →
        //        ArmChaseForShot(ctrl.LastShotOrigin, ctrl.LastShotLaunchDir, ctrl.CurrentBall)
        //        - SetTarget(OLD ball — about to be destroyed)
        //        - ResetToOrigin(STALE origin, STALE launchDir)
        //        - ApplyMode(Chase)
        ...
695:    ballAnimator.Play(trajectory);               // 🔥 DESTROYS old ball, spawns new one
        //   _target now points to a destroyed Transform → equates to null in Unity
        ...
720:    Vector3 origin    = new Vector3(s0.x.ToFloat(), s0.y.ToFloat(), s0.z.ToFloat());
721:    Vector3 launchDir = new Vector3(...).normalized;
722:    _orbitCenter = origin;
723:    _lastShotOrigin    = origin;                 // ❌ updated AFTER it was needed
724:    _lastShotLaunchDir = launchDir;
```

### Visual symptom mapping (from Cesar's manual play 2026-05-07)

- **Driver / Iron flight:** `_target` null → `ChaseCamera.LateUpdate` line 84 early-return → camera doesn't move during flight.
- **"Quick motion at landing" on Driver:** `LoopCameraDirector.TickCinematicCut` runs in `Update()` independently. By the next tick the cache IS fresh (set at line 723-724); SM is still `Flying`; at ~65% of carry the cut fires. The Downrange mode in `ChaseCamera.LateUpdate` does NOT need a target — it SmoothDamps to a static `_downrangePos`. That SmoothDamp from "wherever the camera was idle" to the downrange point is the "quick motion."
- **Iron never moves:** carry < 30m (`minCarryForCinematicMeters = 30f` at LoopCameraDirector.cs:42), so `TickCinematicCut` returns early at line 158. Mode stays Chase whole shot. Target stays null. **Nothing ever updates camera.**
- **"Stays there during roll":** falling-edge drain fires Flying→Rolling and Rolling→AtRest in one frame; Director re-applies Chase mode but `ArmChaseForShot` only fires on Aiming→Flying. `_target` stays null. Camera frozen at downrange position.
- **OB looks identical:** mode goes Chase → Downrange (cinematic cut) → OBFreeze. Cinematic cut fires the SmoothDamp; OBFreeze is a static pivot. Same visual: idle → snap-to-position-at-landing → frozen.

### Why smoke captures missed it

`SmokeTestRunner2b.cs` used `CaptureCore.SnapWhenModeReached`, which subscribes to `LoopCameraDirector.OnModeChanged`. That event fires whenever `ApplyMode` is called regardless of whether the camera visually responds. Mode history `[Chase, Downrange, ...]` proved the dispatch table fired. It said NOTHING about whether `_target` was a valid Transform during Chase. The reviewer's three flagged visual concerns in `controls_g_smoke_followup` (faint Downrange ball-in-flight, putter showing predictor not roll, OBFreeze frame missing water) were all **downstream symptoms of this same root cause** — accepted at the time because the runtime mode-history evidence was treated as dispositive. It wasn't.

This is the methodology gap Lesson O codifies.

### Why the smoke runners didn't crash

`SmokeTestRunner2b` used `FireInternal(preset)` (the preset path, line 822), NOT `HandleShotResolved`. `FireInternal` still has direct `chaseCamera.SetTarget` + `ResetToOrigin` calls (lines 837-841) that the §2b refactor missed. Those calls happen AFTER `ballAnimator.Play()` (line 836) — correct ordering by accident. So smoke ran clean while manual play was broken. Fixing this drift is Option D in scope.

## Locked decisions (carry forward from architect ruling on NOTES.md)

- **L1 — Option A + D combined.** Reorder `HandleShotResolved` (cache + spawn BEFORE OnTrajectoryComputed), AND kill `FireInternal`'s legacy direct chase-camera calls. Single SPEC ships both.
- **L2 — Lesson O written + SPEC template updated.** New lesson in `Docs/Diagnostics/PIPELINE_LESSONS.md`: *"`OnModeChanged` is dispatch evidence, not visual evidence. Spec verification protocols must distinguish these."* SPEC template at `Docs/Specs/Active/_TEMPLATE/SPEC.md` § Smoke evidence gets a new sub-section: "When the spec involves visual fidelity (camera tracking, animation timing, ball/ribbon rendering, mode transitions), runtime event-dispatch captures (e.g., `OnModeChanged`, `OnStateChanged`) are NECESSARY but NOT SUFFICIENT. Visual fidelity requires either (a) human-in-the-loop play-and-confirm in the IMPLEMENTER_REPORT, or (b) reading the actual camera/object Transform position over multiple frames and asserting it tracks the expected reference."
- **L3 — New integration test added.** `Director_HandleShotResolvedFlow_TargetIsValidAfterPlay` exercises the real `HandleShotResolved` → `OnTrajectoryComputed` → `ballAnimator.Play()` sequence and asserts `setter.GetTarget() == ballAnimator.CurrentBall` AND `setter.GetTarget() != null` AFTER the call returns. This catches the regression class for all future shot-fire reorderings.
- **L4 — Keep the SmokeTestRunner .cs files** as auditable evidence. Move them to `Docs/Specs/Completed/loop_v1_2b_camera_transitions/screenshots/SmokeTestRunner2a.cs` + `SmokeTestRunner2b.cs` — NOT in `Assets/` (so they don't compile / clutter editor menus). They're reference artifacts, not active test infrastructure.
- **L5 — Update the `BallStateMachine.cs:64-66` comment.** Currently says "BEFORE BallAnimator.Play() is invoked"; with Option A this is now AFTER Play(). Replace with: *"Called by PhysicsLabController immediately after BallSimulation.Simulate() returns AND after BallAnimator.Play() has spawned the new ball Transform. Pre-computes the canonical transition list from the trajectory + cup scan and stores it for live polling. Caller MUST invoke this synchronously while the animator is still playing the new shot, so the falling-edge detection in Tick() correctly fires when the animation later completes. In Headless mode, drains all transitions synchronously before returning."*

## Architecture context

- **No new asmdef.** All work in `Golfin.Physics.Viewer` (PhysicsLabController + LoopCameraDirector test additions), `Golfin.Gameplay.Loop` (comment update only).
- **No changes to** `BallStateMachine.cs` source logic. ONLY the docstring comment at lines 64-66 changes per L5. Everything else in BallStateMachine stays bit-exact.
- **No changes to** `BallSimulation.cs`, `Trajectory.cs`, `AeroModel.cs`, any aero CSV, `BallAnimator.cs` (no new event per Option C rejection), `ChaseCamera.cs`, `LoopCameraDirector.cs` source logic.
- **No new tests outside** `Golfin.Physics.Tests` asmdef.

## Implementation

### A — Reorder `HandleShotResolved` (`PhysicsLabController.cs:683-759`)

Current sequence (broken):

```csharp
void HandleShotResolved(ShotInput input, BallPhysicsModifiers ballMods)
{
    fp3 ballOrigin = GetCurrentOrigin(fallbackToInput: input.origin);
    var correctedInput = new ShotInput(ballOrigin, input.velocity, input.maxDuration, input.Spin, input.seed);

    var trajectory = RunSimFromController(correctedInput, ballMods);
    _previousTrajectory = trajectory;

    // ❌ §2a: feed the SM before playback starts.
    _ballSM?.OnTrajectoryComputed(correctedInput.origin, trajectory, AeroCfg.BallRadius);

    trajectoryRenderer.Draw(trajectory);
    ballAnimator.Play(trajectory);

    // ... HUD setup ...

    var s0 = trajectory.samples != null && trajectory.samples.Count > 0
        ? trajectory.samples[0].position : correctedInput.origin;
    Vector3 origin    = new Vector3(s0.x.ToFloat(), s0.y.ToFloat(), s0.z.ToFloat());
    Vector3 launchDir = new Vector3(correctedInput.velocity.x.ToFloat(), 0f,
                                     correctedInput.velocity.z.ToFloat()).normalized;
    if (launchDir == Vector3.zero) launchDir = Vector3.right;

    _orbitCenter = origin;

    // ❌ §2b: cache for LoopCameraDirector. (Set AFTER they were needed.)
    _lastShotOrigin    = origin;
    _lastShotLaunchDir = launchDir;

    // ... readout build + LogReadout ...
}
```

Required sequence (fixed):

```csharp
void HandleShotResolved(ShotInput input, BallPhysicsModifiers ballMods)
{
    fp3 ballOrigin = GetCurrentOrigin(fallbackToInput: input.origin);
    var correctedInput = new ShotInput(ballOrigin, input.velocity, input.maxDuration, input.Spin, input.seed);

    var trajectory = RunSimFromController(correctedInput, ballMods);
    _previousTrajectory = trajectory;

    // ── §controls_h: cache origin + launchDir BEFORE SM transition fires ────
    var s0 = trajectory.samples != null && trajectory.samples.Count > 0
        ? trajectory.samples[0].position : correctedInput.origin;
    Vector3 origin    = new Vector3(s0.x.ToFloat(), s0.y.ToFloat(), s0.z.ToFloat());
    Vector3 launchDir = new Vector3(correctedInput.velocity.x.ToFloat(), 0f,
                                     correctedInput.velocity.z.ToFloat()).normalized;
    if (launchDir == Vector3.zero) launchDir = Vector3.right;

    _orbitCenter      = origin;
    _lastShotOrigin   = origin;
    _lastShotLaunchDir = launchDir;

    // ── §controls_h: spawn the new ball BEFORE SM transition fires ──────────
    // BallAnimator.Play() destroys the previous ball Transform and creates a new one.
    // The Director's ArmChaseForShot reads CurrentBall during the synchronous SM
    // transition — it MUST see the post-Play() Transform, not the pre-Play() one
    // that's about to be destroyed.
    trajectoryRenderer.Draw(trajectory);
    ballAnimator.Play(trajectory);

    // ── §2a: now feed the SM. Director sees fresh cache + fresh ball. ───────
    _ballSM?.OnTrajectoryComputed(correctedInput.origin, trajectory, AeroCfg.BallRadius);

    // ── HUD setup (after Play() so CurrentBall is the fresh Transform) ──────
    if (_shotConeView != null && ballAnimator?.CurrentBall != null)
        _shotConeView.SetBallTransform(ballAnimator.CurrentBall);
    if (_puttPathPredictor != null && ballAnimator?.CurrentBall != null)
    {
        _puttPathPredictor.SetBallTransform(ballAnimator.CurrentBall);
        _puttPathPredictor.SetCamera(chaseCamera != null ? chaseCamera.GetComponent<Camera>() : null);
    }

    if (ballAnimator?.CurrentBall != null)
    {
        var holeWidgetShot = FindObjectOfType<Golfin.Gameplay.UI.ShotUI.HoleIndicatorWidget>();
        if (holeWidgetShot != null) holeWidgetShot.SetBallTransform(ballAnimator.CurrentBall);
    }

    // ── readout build (unchanged) ──────────────────────────────────────────
    // ... existing code from current line 727 onward (carryM, finalSurface,
    //     totalM, peakY, bounceCount, readout = new ShotReadout, OnShotFired,
    //     LogReadout) — verbatim, unchanged ...
}
```

**The reorder is purely structural — no logic changes in any line that was already there.**

### B — Kill `FireInternal`'s legacy direct chase-camera calls (`PhysicsLabController.cs:822-846`)

Current `FireInternal` (broken — bypasses Director):

```csharp
void FireInternal(ShotPreset preset)
{
    var trajectory = RunSimForCamera(preset);
    _previousTrajectory = trajectory;

    trajectoryRenderer.Draw(trajectory);
    ballAnimator.Play(trajectory);

    var s0 = trajectory.samples != null && trajectory.samples.Count > 0
        ? trajectory.samples[0].position : preset.Origin;
    Vector3 origin    = new Vector3(s0.x.ToFloat(), s0.y.ToFloat(), s0.z.ToFloat());
    Vector3 launchDir = new Vector3(Mathf.Cos(_cameraYaw), 0f, Mathf.Sin(_cameraYaw));

    _orbitCenter = origin;

    if (chaseCamera != null)                       // ❌ Director-bypass smell
    {
        chaseCamera.SetTarget(ballAnimator.CurrentBall);
        chaseCamera.ResetToOrigin(origin, launchDir);
    }

    var readout = BuildReadout(preset, trajectory);
    OnShotFired?.Invoke(readout);
    LogReadout(readout);
}
```

Required `FireInternal` (fixed — routes through Director like the touch path):

```csharp
void FireInternal(ShotPreset preset)
{
    var trajectory = RunSimForCamera(preset);
    _previousTrajectory = trajectory;

    var s0 = trajectory.samples != null && trajectory.samples.Count > 0
        ? trajectory.samples[0].position : preset.Origin;
    Vector3 origin    = new Vector3(s0.x.ToFloat(), s0.y.ToFloat(), s0.z.ToFloat());
    Vector3 launchDir = new Vector3(Mathf.Cos(_cameraYaw), 0f, Mathf.Sin(_cameraYaw));

    // ── §controls_h: same ordering contract as HandleShotResolved ──────────
    _orbitCenter      = origin;
    _lastShotOrigin   = origin;
    _lastShotLaunchDir = launchDir;

    trajectoryRenderer.Draw(trajectory);
    ballAnimator.Play(trajectory);

    // Build a ShotInput from the preset's velocity + spin so the SM sees a
    // proper Aiming→Flying transition. RunSimForCamera already simulated the
    // trajectory; we just need the SM to know about it.
    fp3 origin_fp = new fp3(fp.FromFloat(origin.x), fp.FromFloat(origin.y), fp.FromFloat(origin.z));
    _ballSM?.OnTrajectoryComputed(origin_fp, trajectory, AeroCfg.BallRadius);

    var readout = BuildReadout(preset, trajectory);
    OnShotFired?.Invoke(readout);
    LogReadout(readout);
}
```

**Removed:** the `chaseCamera.SetTarget` + `ResetToOrigin` block. Director handles both via SM transition.

**Added:** `_lastShotOrigin`/`_lastShotLaunchDir` cache (same as HandleShotResolved), and `_ballSM.OnTrajectoryComputed` call so SM sees the preset shot.

**Side-benefit:** preset-fire path now uses the SM lifecycle. §2c's `HoleSessionDriver` will record preset shots in ShotHistory automatically. §2a's TURN counter will tick. Everything that the touch path drives, the preset path drives too. No more two-paths-drift.

**Risk:** if any preset path test asserts that the SM was NOT involved, it'll break. Architect-spot-check: no such test exists in `Golfin.Physics.Tests`. `FireInternal` is only invoked by the lab UI's preset buttons + the SmokeTestRunners, neither of which is in the test gate.

### C — Update `BallStateMachine.cs:62-66` comment per L5

Current:

```csharp
/// <summary>
/// Called by PhysicsLabController immediately after BallSimulation.Simulate() returns,
/// BEFORE BallAnimator.Play() is invoked. Pre-computes the canonical transition list
/// from the trajectory + cup scan and stores it for live polling.
/// In Headless mode, drains all transitions synchronously before returning.
/// </summary>
```

Required:

```csharp
/// <summary>
/// Called by PhysicsLabController immediately after BallSimulation.Simulate() returns
/// AND after BallAnimator.Play() has spawned the new ball Transform. Pre-computes the
/// canonical transition list from the trajectory + cup scan and stores it for live polling.
/// Caller MUST invoke this synchronously while the animator is still playing the new shot,
/// so the falling-edge detection in Tick() correctly fires when the animation later completes.
/// In Headless mode, drains all transitions synchronously before returning.
/// </summary>
```

This is the ONLY allowed edit to `BallStateMachine.cs`.

### D — New EditMode integration test

**Location:** `Assets/Scripts/Physics/Tests/PhysicsLabControllerHandleShotResolvedTests.cs` (new file). Asmdef: `Golfin.Physics.Tests`.

**Test design:** the existing 9 LoopCameraDirector tests use `RecordingModeSetter` + `StubControllerAccessor` and never touch the real `PhysicsLabController` flow. This new test instantiates a real (or near-real) PhysicsLabController-equivalent, drives `HandleShotResolved`, and asserts that AFTER the call returns:
- `setter.GetTarget() != null`
- `setter.GetTarget() == ballAnimator.CurrentBall` (the post-Play Transform)

**Test seam approach:** since `HandleShotResolved` is private, expose an `internal void HandleShotResolvedForTests(ShotInput input, BallPhysicsModifiers ballMods)` thin wrapper that just calls `HandleShotResolved`, with `[InternalsVisibleTo("Golfin.Physics.Tests")]` on the asmdef (already wired per existing precedent). Architect-verified: the existing `internal` accessors `BallSM`, `LastTrajectory`, `LastShotOrigin`, etc. are already exposed via `internal` in this asmdef, so the InternalsVisibleTo wiring is already in place.

**Required test:**

```csharp
[Test]
public void Director_HandleShotResolvedFlow_TargetIsValidAfterPlay()
{
    // Arrange: scene with PhysicsLabController + ballAnimator + LoopCameraDirector
    // wired via RecordingModeSetter (no Camera GO needed).
    var (controller, ballAnimator, director, setter) = TestScaffold.CreatePhysicsLabSetup();
    
    // Build a minimal valid ShotInput for a 50-yard driver shot.
    var input = TestScaffold.MakeDriverShotInput();
    var ballMods = BallPhysicsModifiers.Neutral;
    
    // Act: drive the real touch-path flow.
    controller.HandleShotResolvedForTests(input, ballMods);
    
    // Assert: AFTER the call returns, the Director should have armed the camera
    // with the post-Play() ball Transform.
    Assert.That(setter.GetTarget(), Is.Not.Null,
        "Chase target should be non-null after HandleShotResolved — was the SM transition fired before BallAnimator.Play()?");
    Assert.That(setter.GetTarget(), Is.EqualTo(ballAnimator.CurrentBall),
        "Chase target should be the post-Play() ball Transform, not a stale or destroyed one.");
    Assert.That(setter.CurrentMode, Is.EqualTo(ChaseCamera.Mode.Chase),
        "Mode should be Chase after Aiming→Flying transition (not Downrange — cinematic cut hasn't ticked yet).");
}
```

**Test scaffold helpers** (`TestScaffold.CreatePhysicsLabSetup` etc.) — implementer designs to match existing test infrastructure precedent. If too heavy to mock, Architect lean is to use a thin testable subclass `TestablePhysicsLabController` that exposes `HandleShotResolved` as protected and uses a no-op BallAnimator stub that still creates a Transform. The exact harness shape is implementer judgment per L3 escalation if it gets thorny.

### E — Lesson O in `Docs/Diagnostics/PIPELINE_LESSONS.md`

Append at the end of the file (verify location with `grep -n "## " Docs/Diagnostics/PIPELINE_LESSONS.md` — Lesson O follows whatever the current last lesson is):

```markdown
## Lesson O — `OnModeChanged` is dispatch evidence, not visual evidence (controls_h, 2026-05-07)

**Symptom:** `controls_g_smoke_followup` shipped 3 captures verified via `LoopCameraDirector.OnModeChanged` mode history (`[Chase, Downrange, ...]` etc.). All 3 captures passed reviewer + architect. Cesar then loaded the lab and discovered the chase camera doesn't visually track the ball during flight at all — `_target` was null for the entire shot due to an order-of-operations bug introduced in the same PR.

**Methodology gap:** `OnModeChanged` fires whenever `ApplyMode` is called regardless of whether the camera Transform actually moves to track the ball. So mode history `[Chase, Downrange]` proved the dispatch table fired in the correct sequence — but said NOTHING about whether `_target` was a valid Transform during Chase, or whether the camera position equation `pos = ball - launchDir·5m + up·2.5m` actually evaluated against a real ball reference. The reviewer flagged three "visual concerns" (faint ball-in-flight, putter showing predictor, OBFreeze missing water) that were ALL downstream symptoms of the root cause; we accepted them on the runtime mode-history evidence.

**Rule:** When a spec involves visual fidelity (camera tracking, animation timing, ball/ribbon rendering, mode transitions, SmoothDamp targets, anything where the player-perceived behavior is the deliverable), runtime event-dispatch captures are NECESSARY but NOT SUFFICIENT. They prove dispatch fired. They do NOT prove the visual responded.

**Visual fidelity requires one of:**
1. **Human-in-the-loop play-and-confirm.** The implementer (or Cesar) loads the scene, drives the relevant flow manually, and writes a short content-sanity description in the IMPLEMENTER_REPORT: "I drove the touch-path driver shot 5 times. Each time the camera tracked the ball through Chase mode for ~2 seconds before the cinematic cut to Downrange at 65% carry. Roll camera tracked the ball back to rest." That description is auditable evidence.
2. **Position-trace assertion.** Read the actual camera/object Transform position over multiple frames during the flow, assert it tracks the expected reference (e.g., `Mathf.Abs(cam.position.x - (ball.position.x - launchDir.x * 5)) < 0.5f` over 30 frames). Coded into an EditMode or PlayMode test.

**Forbidden as sole evidence for visual fidelity:**
- Mode-history list (proves dispatch only)
- Single-frame screenshot at a specific state (might be coincident with bug; tells you nothing about transition)
- "Test gate green" (unit tests cover dispatch, not visual)

**Spec template implication:** the SPEC.md template (`Docs/Specs/Active/_TEMPLATE/SPEC.md`) gets a new sub-section under § Smoke evidence: *"When the spec involves visual fidelity, runtime event-dispatch captures are necessary but not sufficient. Visual fidelity requires either (a) human-in-the-loop play-and-confirm in IMPLEMENTER_REPORT, or (b) position-trace assertions over multiple frames."*

**Pattern recognition for future specs:** any spec that says "verify mode X fires" is a dispatch verification. Any spec that says "verify the camera tracks the ball" is a visual verification. The two are NOT the same. SPEC reviews must distinguish.
```

### F — SPEC template update

**File:** `Docs/Specs/Active/_TEMPLATE/SPEC.md`. Architect-verified file exists per earlier session.

Find the existing `## Smoke evidence` section. ADD a new sub-section underneath:

```markdown
### Visual-fidelity verification (Lesson O)

When the spec involves visual fidelity — camera tracking, animation timing, ball/ribbon rendering, mode transitions, SmoothDamp targets, or any deliverable where player-perceived behavior is the success criterion — runtime event-dispatch captures (e.g., `OnModeChanged`, `OnStateChanged`, `OnShotComplete`) are NECESSARY but NOT SUFFICIENT.

Visual fidelity REQUIRES one of:
- **Human-in-the-loop play-and-confirm.** Implementer loads the scene, drives the flow manually, and writes a content-sanity description in IMPLEMENTER_REPORT.md describing what the camera/animation/ball visually did. Auditable by Cesar and reviewer.
- **Position-trace assertion.** EditMode or PlayMode test reads actual Transform positions over multiple frames and asserts tracking against the expected reference.

Mode-history captures + screenshot files alone are dispatch evidence, not visual evidence. See `Docs/Diagnostics/PIPELINE_LESSONS.md` Lesson O for the full failure analysis.
```

### G — Move SmokeTestRunner files per L4

Move:
- `Assets/Scripts/Physics/Viewer/SmokeTestRunner2a.cs` → `Docs/Specs/Completed/loop_v1_2a_ball_state_machine/SmokeTestRunner2a.cs`
- `Assets/Scripts/Physics/Viewer/SmokeTestRunner2b.cs` → `Docs/Specs/Completed/loop_v1_2b_camera_transitions/SmokeTestRunner2b.cs`

Verify they DON'T compile in the new location (they're under `Docs/`, outside `Assets/`). They're reference artifacts only.

Delete the corresponding `.cs.meta` files from `Assets/`.

If the move triggers any reference resolution issue in `LabScaffold.unity` (e.g., a stale GameObject component pointer), use Unity Editor MCP to clean up. Per L4 hard-rule list: NO raw YAML edits.

## Tests

**Test gate target:** **248 → 249/249 PASS, 0 IGNORED.** 1 new integration test added. All 248 existing tests must still PASS.

**Risk:** if Option B-style implementation drift creeps in (modifying `BallStateMachine.cs` source logic instead of just the comment), the 16 existing SM tests will likely break. Hard rule: ONLY the comment change is allowed in `BallStateMachine.cs`.

## Smoke evidence

**Per Lesson O written in this very SPEC: visual-fidelity verification REQUIRED.**

Implementer must do BOTH:

### Mandatory: Human-in-the-loop play-and-confirm (IMPLEMENTER_REPORT.md content-sanity descriptions)

Drive the lab manually, do all 5 of these test cases, and write a 2-3 sentence content-sanity description for each in IMPLEMENTER_REPORT.md § "Visual Verification":

1. **Driver, full power, on Hole 1 fairway-aimed.** Expected: camera tracks ball smoothly through Chase mode for ~1-2 seconds; cinematic cut to Downrange triggers at landing area; camera holds Downrange while ball rolls; settles in Chase looking at rest position.
2. **Iron, half power, short carry (<30m).** Expected: camera tracks ball smoothly through Chase mode through entire flight + roll. NO Downrange cut (under 30m gate). Camera follows ball all the way to rest.
3. **Driver, full power, aimed off-tee at OB (Hole 6 lake).** Expected: camera tracks ball during flight through Chase; cinematic cut to Downrange triggers; on water-hit, OBFreeze locks camera. Ball flies away from camera into hazard.
4. **Putter, on green.** Expected: camera stays in GroundLevel framing through entire putt. NO Downrange cut. NO mode change to Chase (per Q1'c).
5. **Two consecutive driver shots from same tee.** Expected: shot 1 tracks correctly; on rest, camera settles. Re-arm, fire shot 2. Shot 2 ALSO tracks correctly (no stale-Transform regression on second shot).

**Each description must explicitly state:** "Camera moved through Chase mode tracking the ball" (or didn't, with explanation) — so the reviewer can audit whether the test was actually performed and whether it actually worked.

### Recommended: Position-trace assertion (test added in § Tests)

The new `Director_HandleShotResolvedFlow_TargetIsValidAfterPlay` test catches the regression class automatically going forward.

### File-on-disk artifacts

3 captures under `Docs/Specs/Active/controls_h_chase_camera_regression/screenshots/` taken DURING the human-in-the-loop verification:

- `controls_h_driver_chase_midflight.png` — Chase mode mid-flight on driver, ball visible mid-screen, camera positioned behind it.
- `controls_h_iron_chase_landing.png` — Chase mode at iron landing, ball + camera in frame showing tracking continuity.
- `controls_h_two_consecutive_shots_log.txt` — text log of GameObject.GetInstanceID() of `ballAnimator.CurrentBall` BEFORE and AFTER each shot's HandleShotResolved, plus `setter.GetTarget()?.GetInstanceID()` AFTER. Proves target Transform is valid (non-null, same instance as CurrentBall) on both shots.

These captures DO NOT replace the human-in-the-loop descriptions per Lesson O — they supplement.

## Definition of Done

- `HandleShotResolved` reordered per § A: cache + Play() BEFORE OnTrajectoryComputed.
- `FireInternal` updated per § B: legacy direct chase-camera calls removed; routes through SM/Director like the touch path.
- `BallStateMachine.cs:62-66` docstring updated per § C / L5.
- New integration test `Director_HandleShotResolvedFlow_TargetIsValidAfterPlay` lands and PASSES.
- Lesson O added to `Docs/Diagnostics/PIPELINE_LESSONS.md`.
- SPEC template updated at `Docs/Specs/Active/_TEMPLATE/SPEC.md` § Smoke evidence with the visual-fidelity sub-section.
- SmokeTestRunner files moved out of `Assets/` to `Docs/Specs/Completed/loop_v1_2{a,b}_*/`.
- 5 manual content-sanity descriptions in IMPLEMENTER_REPORT.md § Visual Verification.
- 3 supporting files (2 screenshots + 1 instance-ID log) under `Docs/Specs/Active/controls_h_chase_camera_regression/screenshots/`.
- Test gate: **249/249 PASS, 0 IGNORED.**
- Cesar manually verifies the 5 cases pass.

## Mid-task escalation paths

- **`IMPLEMENTER_BLOCKED`** if:
  - The new integration test cannot be cleanly written (PhysicsLabController is too heavy to instantiate in EditMode without a scene). Architect resolves with one of: PlayMode test instead of EditMode, testable subclass with stubbed BallAnimator, or accept that the test is integration-flavored and lives in a future PlayMode test asmdef.
  - Reordering `HandleShotResolved` breaks any of the 248 existing tests unexpectedly. Most likely culprit: an existing test asserts on `_lastShotOrigin` BEFORE the SM transition fires (would catch the cache being too late, but the inverse direction). Architect investigates whether the test's assertion timing was wrong.
  - `FireInternal` SM integration breaks something the SmokeTestRunners (now archived) were silently relying on. Since they're archived, runtime impact is zero — but if the SM transition during preset fires causes a side-effect that breaks lab UI behavior, architect investigates.
- **`IMPLEMENTER_PARTIAL`** acceptable if:
  - A + B + C + manual verification all land clean, but the new EditMode test (D) hits intractable scaffolding friction. Acceptable to ship without the test; architect logs a follow-up flag for "controls_h_visual_regression_test" as P2 future work. Lesson O still lands; SPEC template still updates.
  - The 3 file-on-disk captures land but the instance-ID log is hard to produce cleanly. Acceptable to ship with just 2 screenshots + a written description of the two-consecutive-shots verification.

## Out of scope

- **Option B (defer SM fire to next Tick).** Architecturally cleaner but rejected per L1: blast radius into SM core + 16 EditMode tests. Filed as future-task candidate if A+D drift recurs.
- **Option C (BallAnimator.OnBallSpawned event).** Robust safety net but doesn't address stale `LastShotOrigin/Dir`. Rejected per L1.
- **Position-trace EditMode test for cinematic-cut timing.** Stretch goal. The integration test catches the immediate regression class; tightening cinematic timing assertions can come in a future spec.
- **Re-doing the controls_g_smoke_followup captures** under the new methodology. They're closed; they served their narrow dispatch-verification purpose. Re-running them without Lesson O would be theatre. Lesson O catches the next one.
- **Camera framing redesigns** (OBFreeze yaw orientation, Downrange height tuning, Chase distance/height). Cesar's manual tuning session was about to start when the regression surfaced. Tuning resumes after this fix lands; framing decisions are out of scope here.
- **§2c blocking decision.** §2c subscribes to `OnShotComplete` (coarse, terminal-only) and reads `LastTrajectory`/`LastShotOrigin` — both fields are stable across this fix (LastTrajectory is set at line 689, LastShotOrigin is set in the new ordering BEFORE the SM transition, both still valid at OnShotComplete time which is much later). §2c is NOT impacted by this fix; can run in parallel or after.

## Hard rules for implementer

1. **Do NOT modify** `BallStateMachine.cs` source logic. ONLY the docstring at lines 62-66 changes.
2. **Do NOT modify** `BallSimulation.cs`, `Trajectory.cs`, `AeroModel.cs`, `AeroConfig.cs`, any aero CSV, `BallAnimator.cs`. `ChaseCamera.cs` and `LoopCameraDirector.cs` ARE editable as of iteration 5 (see § Iteration 5 amendments) — only the listed aero/animation files remain locked.
3. **Do NOT add** new events to `BallAnimator` (Option C rejected).
4. **Do NOT defer** the SM synchronous fire (Option B rejected).
5. **Do NOT modify** `LabScaffold.unity` via raw YAML. If the SmokeTestRunner removal triggers any scene reference issue, use Unity Editor MCP. Per controls_g deviation #3 lesson.
6. **Do NOT skip the 5 manual content-sanity descriptions.** Per Lesson O — written by THIS spec, applied to THIS spec — runtime event-dispatch captures are not sufficient. Implementer must drive the lab manually for all 5 cases and write the descriptions.
7. **Do NOT use `OnModeChanged`-only captures as visual verification.** That's the failure mode this spec exists to fix. Mode history captures are NOT acceptable evidence for the camera-tracking checklist items.
8. **Bit-exact 248-test PASS gate must hold.** Adding 1 test → 249/249. If any of the 248 starts failing, escalate `IMPLEMENTER_BLOCKED` immediately — do NOT "fix" by editing existing tests.

---

## Iteration 5 amendments (Cesar 2026-05-08)

After iter-4 the self-reviewer escalated two questions to Cesar:

- **Q1** — should the camera chase the ball during the visual roll-out (Rolling state), or should the Downrange cinematic stay parked through the roll?
- **Q2** — how should visual evidence be produced given the off-screen-RT capture path keeps producing temporally misaligned frames?

Cesar's rulings (chat, 2026-05-08):

- **Q1 → Option B.** Camera must chase through the visual roll-out. The Downrange cinematic must release at touchdown (ball reaches terrain) and Rolling stays in `Chase` mode. The current behaviour ("snaps to ground violently and stays parked while the ball rolls away") is unacceptable.
- **Q2 → Cesar verifies visuals manually in chat.** Implementer no longer needs to produce mid-Rolling, Downrange, or any chase-camera screenshot. The instance-ID log + the EditMode test gate + Cesar's manual confirmation in chat are the evidence path.

Plus one new regression Cesar surfaced in the same chat round:

- **Aiming for the second shot is broken — sideways camera pan specifically does not respond.** R4 in the iter-3 rejection covered first-shot pan dead. Iter-3's `Start()` priming fixed first-shot pan but introduced (or unmasked) a second-shot pan failure.

### R3-revised — Downrange releases at touchdown; Rolling stays in Chase

**Replaces** the iter-3 R3 narrow fix (AtRest→Chase ModeMap entry).

In `LoopCameraDirector.cs`:

- `TickCinematicCut` may still fire Downrange during Flying for shots that meet the threshold. That's fine — the cinematic still plays through the air phase.
- Add a release condition: when `BallStateMachine` transitions Flying→Rolling (i.e. the ball touches down), the Director must immediately switch the active mode away from `Downrange` and back into `Chase`, with the same `_target` (the live ball Transform).
- Rolling must stay in `Chase` until the ball reaches AtRest. AtRest can stay in Chase (carry-over from iter-3's ModeMap entry), or transition out — Cesar's ruling does not require a specific AtRest behaviour, only that the roll-out itself is chased.
- The "snaps to ground violently" symptom Cesar described is the Downrange→Chase transition the existing camera-mode dispatch isn't smoothing. The implementer must either smooth this transition (lerp `_followDistance`/`_followHeight` over a short blend window) or position the camera at a sensible Chase pose at touchdown such that the visible jump is minimal.

Implementation hint: the Flying→Rolling transition fires on the falling edge of `BallAnimator.IsPlaying` per the SM. `LoopCameraDirector` already subscribes to `OnStateChanged` (or can subscribe). Hook the release there.

Acceptance per § R3-revised:

- Driver full-power shot: camera Chase during flight → Downrange cinematic plays through air → at touchdown, mode releases to Chase and the camera follows the ball through Rolling until AtRest, with no violent snap.
- Iron short shot (under cinematic threshold): mode stays in Chase end-to-end (no Downrange, no transition needed). Already worked in iter-3, must continue to work.
- Putt: mode stays in GroundLevel end-to-end. No Downrange, no Chase. Already worked.

### R5 — Second-shot aiming sideways pan is broken

New in iter-5. After firing a first shot and re-arming for a second shot from the rest position (or from a re-aim), pressing left/right pan input does nothing.

**Likely culprits to investigate:**

- The first-shot pan input handler is wired in `Start()` (R4 fix from iter-3). On Aiming entry for shot 2, something tears down or replaces the input subscription. Trace `EnableInput` / `DisableInput` / equivalent calls in `PhysicsLabController` around `OnShotComplete` and the next Aiming entry.
- The `CameraHeadingRadians` field is reset on Aiming entry but the ChaseCamera or pan controller is reading from a different cached value that wasn't reset.
- `HandleCameraOrbit` (the orbit-input handler edited in iter-3 — "removed double-clear") may have lost a re-arm path.
- The pan input action is subscribed via `Input System` and the action map is being disabled and not re-enabled on Aiming re-entry.

Acceptance per § R5:

- Fire shot 1 from tee. Pan left/right during Aiming — works (R4 carry-over).
- Wait for ball to settle. Shot 1 resolves; lab returns to Aiming for shot 2.
- Pan left/right during Aiming for shot 2 — must work identically to shot 1.
- Repeat for shot 3, 4 — must work indefinitely.

### R6 — Eliminate the violent ground snap on Downrange→Chase transition

Subsumed under R3-revised but worth calling out explicitly: the implementer must verify visually (via Cesar) that the touchdown transition does not cause a visible camera jolt. Any snap of more than ~0.5s of camera-position discontinuity counts as "violent" for purposes of this acceptance.

### Smoke evidence — iter-5 update

The original § Smoke evidence section (file-on-disk artifacts requirement) is **superseded for iter-5+** by Cesar's Q2 ruling.

Implementer must:

1. Run the full EditMode test gate via `mcp__ai-game-developer__tests-run` and record PASS count + failure count + skip count in IMPLEMENTER_REPORT § Tests. Continue retrying transient MCP transport drops every 30–60s — do NOT pre-declare blocked.
2. Drive the lab manually in play mode and verify in chat with Cesar:
   - R3-revised: driver full-power shot — confirm Chase→Downrange→Chase-during-roll behaviour with no violent snap.
   - R5: second-shot sideways pan works.
3. The `controls_h_two_consecutive_shots_log.txt` instance-ID log from iter-2 still stands; do not re-produce.
4. **No new screenshot files required.** No mid-Rolling, no Downrange, no chase-camera PNGs. Cesar verifies visuals in chat.

### Definition of Done — iter-5 update

Items dropped from original DoD:

- ~~3 file-on-disk captures~~ (Q2 ruling).
- ~~Mid-Rolling chase screenshot~~ (Q2 ruling).
- ~~Downrange cinematic screenshot~~ (Q2 ruling).
- ~~5 manual content-sanity descriptions in IMPLEMENTER_REPORT~~ — replaced with R3-revised + R5 acceptance descriptions only.

Items added:

- R3-revised landed and verified by Cesar in chat: Downrange releases at touchdown, Rolling stays in Chase, no violent snap.
- R5 landed and verified by Cesar in chat: second-shot sideways pan works.
- New tests covering R3-revised (Downrange→Chase release on Flying→Rolling) and R5 (pan input wired on every Aiming entry, not just first). At least 2 new tests; total expected suite count adjusts accordingly.
- Hard rule 2 (don't modify `LoopCameraDirector.cs`) is loosened — implementer MUST modify `LoopCameraDirector.cs` for R3-revised. Document the change in IMPLEMENTER_REPORT § Spec Deviations as expected per this amendment, not flagged as a deviation.

### Hard rules — iter-5 amendments

- Hard rule 2 amended above to permit `LoopCameraDirector.cs` and `ChaseCamera.cs` edits.
- Hard rule 6 (5 manual content-sanity descriptions) — superseded by Q2 ruling. Implementer writes only R3-revised + R5 descriptions in IMPLEMENTER_REPORT § Visual Verification.
- Hard rule 7 (don't use OnModeChanged-only captures) — still in force as a methodology lesson, but the consequence (file-on-disk PNG captures) is no longer required given Q2.
- All other hard rules (1, 3, 4, 5, 8) unchanged.
