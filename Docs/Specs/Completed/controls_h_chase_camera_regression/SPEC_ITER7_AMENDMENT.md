# SPEC AMENDMENT — Iteration 7: Restore Aim Framing

> **Amendment to `controls_h_chase_camera_regression`.** Iter-6 fixed the single-writer architecture but inadvertently lost the distinct "Aim framing" that pre-iter-6's `ApplyCameraYaw` provided. This amendment restores that framing as a parameter set on Chase mode, toggled automatically by ball-playing state. **Architect-locked 2026-05-08, post-iter-6 reject.**

## Why this is broken

Pre-iter-6, the camera had two implicit framing contracts:

| Phase | Source | Distance | Height | Look-at target |
|---|---|---|---|---|
| Ball at rest (Aiming) | `PhysicsLabController.ApplyCameraYaw` | **8m** | **3m** | `focus + launchDir·3m + up·0.5m` (forward of ball) |
| Ball in flight (Chase) | `ChaseCamera.LateUpdate` | 3m | 1.8m | `focus` (at ball) |

Iter-6 deleted `ApplyCameraYaw` and made ChaseCamera the single writer — correct architecturally — but used the Chase parameters (3m / 1.8m / look-at-ball) for BOTH cases. So during Aiming, the camera now sits very close to the ball, looking directly at it. The player sees a tight close-up of the ball, no fairway, no hole context. That's wrong for golf setup.

The implementer's "manual verification" reported camera at `(488.19, 1.82, 34.73)` with ball at `(491.19, 0.02, 34.73)` — that's mathematically correct for the new framing, AND visually wrong for Aiming. The verification was a Lesson O violation: they asserted coordinates instead of playing the game. The architect approved iter-6 on that report.

This amendment fixes the framing AND tightens the verification protocol.

## What lands

### A. ChaseCamera framing parameters

**File:** `Assets/Scripts/Physics/Viewer/ChaseCamera.cs`

Add four new SerializeFields under a new `[Header("Aim framing — §controls_h iter-7")]` block, immediately after the existing `_followDistance` / `_followHeight` block:

```csharp
[Header("Aim framing — §controls_h iter-7")]
[Tooltip("XZ distance behind ball during Aiming (ball at rest). Larger than follow distance to show fairway context.")]
[SerializeField] float _aimDistance = 8f;
[Tooltip("Height above ball during Aiming. Higher than follow height to give the player a setup view.")]
[SerializeField] float _aimHeight = 3f;
[Tooltip("How far past the ball the camera looks during Aiming. Frames the fairway ahead, not the ball itself.")]
[SerializeField] float _aimLookAheadMeters = 3f;
[Tooltip("Vertical offset on the look-target during Aiming (slightly above ground level).")]
[SerializeField] float _aimLookUpMeters = 0.5f;
```

Add a private state field:

```csharp
bool _isAiming = true;  // default to aim framing on scene load (no ball is playing yet)
```

Add a public setter:

```csharp
/// <summary>
/// Toggle between Aim framing (ball at rest, wider stance, look-ahead) and Follow framing
/// (ball in flight, tight chase, look-at-ball). Called every frame from PhysicsLabController
/// based on ball-is-playing state. Cheap to call repeatedly — only assigns a bool.
/// </summary>
public void SetAiming(bool aiming) => _isAiming = aiming;
```

### B. ChaseCamera Chase-mode math

**File:** `Assets/Scripts/Physics/Viewer/ChaseCamera.cs`

Replace the existing `default: // Chase` case in the switch inside `RunLateUpdateLogic`:

```csharp
default: // Chase — §controls_h iter-7: branch on _isAiming
{
    float dist   = _isAiming ? _aimDistance : _followDistance;
    float height = _isAiming ? _aimHeight   : _followHeight;
    
    desiredPos = focus - _launchDir * dist + Vector3.up * (height + FollowHeightOffset);
    
    Vector3 lookTarget = _isAiming
        ? focus + _launchDir * _aimLookAheadMeters + Vector3.up * _aimLookUpMeters
        : focus;
    desiredRot = Quaternion.LookRotation(lookTarget - desiredPos);
    break;
}
```

Note the `lookTarget` change: when aiming, the camera looks AT a point ahead of the ball (showing fairway), not at the ball itself. This matches pre-iter-6's `cam.transform.LookAt(_orbitCenter + lookDir * 3f + Vector3.up * 0.5f)` semantics.

### C. PhysicsLabController writes the aim flag every frame

**File:** `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs`

In `HandleCameraOrbit`, immediately after the line `_prevBallPlaying = isPlaying;` and before `if (isPlaying) return;`, ADD:

```csharp
// §controls_h iter-7: feed ChaseCamera the aim/follow framing flag every frame.
// Cheap (just a bool assignment); always correct because isPlaying tracks BallAnimator state.
chaseCamera?.SetAiming(!isPlaying);
```

That's the entire integration. No SM coupling, no Director changes, no new modes.

### D. Initial scene-load convergence guard

**File:** `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs`

The implementer report shows the camera converges from world origin during the first 2 frames before `ScanForLoadedHoleSceneAtStartup` calls `SetupAtTee`. That's a visible "camera flies in from world origin" glitch. Fix: in `Start()`, after the existing `chaseCamera?.SetAimDirection(r4dir);` line, ADD:

```csharp
// §controls_h iter-7: snap camera to its initial Chase pose immediately so the first
// rendered frame is already in the right place. Without this, ChaseCamera SmoothDamps
// from world origin during the 2-frame scan delay, producing a visible startup glitch.
if (chaseCamera != null)
{
    Vector3 initialFocus = _ballSpawnPoint != null ? _ballSpawnPoint.position : Vector3.zero;
    chaseCamera.ResetToOrigin(initialFocus, r4dir);
    chaseCamera.SetAiming(true);
    // Force the camera Transform to its desired Aim pose immediately (no SmoothDamp glide).
    var cam = chaseCamera.GetComponent<Camera>();
    if (cam != null)
    {
        Vector3 desired = initialFocus - r4dir * 8f + Vector3.up * 3f;
        cam.transform.position = desired;
        cam.transform.LookAt(initialFocus + r4dir * 3f + Vector3.up * 0.5f);
    }
}
```

This is a deliberate exception to the "single writer" rule — the ONE-TIME bootstrapping write at scene start. ChaseCamera takes over after this. Comment on the lines makes the exception explicit so future readers don't think the rule is broken.

## Tests

**Add to `Assets/Scripts/Physics/Tests/LoopCameraDirectorTests.cs`:**

```csharp
[Test]
public void ChaseCamera_SetAiming_TrueUsesAimFraming()
{
    var go = new GameObject("ChaseCam");
    var cam = go.AddComponent<ChaseCamera>();
    cam.SetMode(ChaseCamera.Mode.Chase);
    cam.SetTarget(null);
    cam.ResetToOrigin(Vector3.zero, Vector3.right);
    cam.SetAiming(true);
    cam.transform.position = Vector3.zero;
    
    for (int i = 0; i < 60; i++) cam.FrameCamera(1f / 60f);
    
    // Aim framing: 8m back, 3m up. Position should be near (-8, 3, 0).
    Assert.That(Vector3.Distance(cam.transform.position, new Vector3(-8f, 3f, 0f)), Is.LessThan(0.5f),
        $"Aim framing expected near (-8,3,0); got {cam.transform.position}");
    
    Object.DestroyImmediate(go);
}

[Test]
public void ChaseCamera_SetAiming_FalseUsesFollowFraming()
{
    var go = new GameObject("ChaseCam");
    var cam = go.AddComponent<ChaseCamera>();
    cam.SetMode(ChaseCamera.Mode.Chase);
    cam.SetTarget(null);
    cam.ResetToOrigin(Vector3.zero, Vector3.right);
    cam.SetAiming(false);
    cam.transform.position = Vector3.zero;
    
    for (int i = 0; i < 60; i++) cam.FrameCamera(1f / 60f);
    
    // Follow framing: 3m back, 1.8m up. Position should be near (-3, 1.8, 0).
    Assert.That(Vector3.Distance(cam.transform.position, new Vector3(-3f, 1.8f, 0f)), Is.LessThan(0.5f),
        $"Follow framing expected near (-3,1.8,0); got {cam.transform.position}");
    
    Object.DestroyImmediate(go);
}
```

**Update Test 14** (`ChaseCamera_LateUpdateRunsWithNullTarget_UsesShotOriginAsFocus`) — the assertion currently expects follow framing; under iter-7 default `_isAiming=true`, it should expect aim framing. Either:
- Change the test to assert aim framing, OR  
- Add `cam.SetAiming(false)` before the convergence loop.

Architect lean: change the test to call `cam.SetAiming(false)` before convergence (preserves the original test intent — "Chase math runs without target, computes a position relative to shot origin," doesn't matter which framing).

**Test gate target:** **246 → 248/248 PASS, 0 IGNORED.** 2 new tests, 0 deleted.

## Manual verification — TIGHTENED PROTOCOL

**This is non-negotiable per Lesson O. The implementer must NOT script-execute camera coordinates and call that "manual verification." Cesar must play the lab and visually confirm.**

For each case below, write a 1-2 sentence visual description in IMPLEMENTER_REPORT.md § "Visual Verification (iter-7)". The description must say what the player SEES, not what the camera coordinates are.

1. **First-shot Aiming on Hole 1 (no shots fired yet).** Load scene. Look at the screen. Camera should sit ~8m behind the tee, ~3m up, looking down the fairway toward the green. The ball should appear in the lower-center of the screen, the fairway should fill most of the view. NOT a close-up of just the ball. **If you see ball-only with no fairway, FAIL.**
2. **First-shot pan.** From state #1, drag the mouse horizontally. Camera orbits around the tee, fairway view rotates correspondingly. Aim cone follows.
3. **Driver shot mid-flight.** Fire driver. Camera should pull in tighter to follow the ball during flight (3m / 1.8m, looking at ball). Ball should be tracked through the air with the camera staying behind it.
4. **Driver shot at-rest.** Ball lands and rolls to stop. Camera should pull BACK to Aim framing (8m / 3m / look-ahead) within ~0.5 seconds, showing the new fairway view from the resting position.
5. **Shot 2 pan.** From #4, drag the mouse horizontally. Camera orbits around the resting ball, fairway view rotates. Same feel as #2.

**Cesar must approve all 5 visually before this spec is marked complete.** Implementer can pre-screen but final sign-off is Cesar's eyes on the actual game running.

## Definition of Done

- A: 4 new SerializeFields + `_isAiming` field + `SetAiming` method on ChaseCamera.
- B: Chase-mode math branches on `_isAiming`.
- C: HandleCameraOrbit calls `chaseCamera?.SetAiming(!isPlaying)` every frame.
- D: Start() snaps camera to initial Aim pose to eliminate scene-load glitch.
- 2 new tests added, Test 14 updated. Gate at **248/248 PASS, 0 IGNORED.**
- Cesar plays the lab, manually verifies all 5 cases visually, and approves.
- Implementer writes visual descriptions (not coordinate assertions) in IMPLEMENTER_REPORT § Visual Verification (iter-7).

## Hard rules

1. **No script-execute substitute for visual verification.** Per Lesson O. The implementer scripts coordinate-checking tests for the EditMode test gate. They do NOT script-check coordinates and call that "manual verification" of the 5 cases. Visual = eyeballs on screen, period.
2. **Single writer rule still applies, with one documented exception.** ChaseCamera owns `cam.transform.position` for all of runtime. The Start() bootstrapping write in §D is the ONLY exception, marked with a comment, executed exactly once per scene load.
3. **No new modes.** Aim framing is a parameter set within Chase mode, not a separate mode. Director's ModeMap is unchanged.
4. **Do NOT change** `BallStateMachine.cs`, `LoopCameraDirector.cs`, `BallAnimator.cs`, `BallSimulation.cs`, any aero CSV.
5. **Do NOT add additional knobs.** Only the four SerializeFields specified in §A. Tuning happens in inspector after this lands.

## What this won't fix

- **Apex zoom-out** during long flights (still deferred).
- **Cinematic cuts at landing** (still deferred — see `Docs/Game Design/CAMERA_SYSTEM_FUTURE_DESIGN.md`).
- **OBFreeze framing** with visible water (forward-flagged in TellCode, not this task).

If after iter-7 lands and Cesar plays it, the camera STILL feels wrong, the issue is somewhere else — not in framing parameters. Future iteration starts from a working baseline.

## Why this WILL work

Iter-6 broke shot 1 because it collapsed two distinct framing contracts into one. Iter-7 restores the distinction explicitly with named parameters. The toggle (`SetAiming(!isPlaying)`) runs every frame in `HandleCameraOrbit` which is already running every frame; the cost is one bool assignment.

The Lesson O failure that let iter-6 ship is now structurally addressed: the manual verification protocol explicitly forbids the coordinate-script substitute. Cesar's eyeballs are the final gate.

If THIS amendment ships and shot 1 still feels wrong, the bug is somewhere outside the camera framing logic — and that's a different problem to debug, with a much narrower search space.
