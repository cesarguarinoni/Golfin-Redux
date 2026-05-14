# putter_aim_yaw_in_groundlevel — Restore sideways aim in putter mode

> **STATUS:** Queued (drafted 2026-05-14 by architect chain, surfaced by Cesar Lesson O on `loop_v1_2f_putter_p2_in_context`). **Priority: HIGH — pick up immediately. §2f auto-toggle makes this bug front-and-center.**

## One-line

When in putter mode, mouse-drag must rotate aim heading sideways (and the camera should follow), like every other club. Today the camera and aim are pinned to the ball→pin line in `ChaseCamera.Mode.GroundLevel` and cannot rotate.

## Cesar's observation (Lesson O, 2026-05-14)

> "When in putter mode, I can't move the camera sideways, can only shoot forward."

This blocks the basic putter play loop: the player needs to read the green, walk the aim around the line they want to roll, and commit. Currently they can only shoot at whatever heading the ball happened to come to rest pointing at (= pin direction, because §2e's pin-aim rotation runs before auto-switch on non-flip cases; on auto-flip cases putter mode owns framing per §2f L4).

## Root cause

[ChaseCamera.cs:118-122](Assets/Scripts/Physics/Viewer/ChaseCamera.cs:118):

```csharp
case Mode.GroundLevel:
    desiredPos = _shotOrigin + Vector3.up * 1.6f;
    Vector3 lookAt = focus != _shotOrigin ? focus : _shotOrigin + _launchDir * 10f;
    desiredRot = Quaternion.LookRotation(lookAt - desiredPos);
    break;
```

Every `LateUpdate`, GroundLevel re-pins the camera to `_shotOrigin` and forces look direction at `focus` (the pin/target). Any `PhysicsLabController.ApplyCameraYaw` write from [`HandleCameraOrbit`](Assets/Scripts/Physics/Viewer/PhysicsLabController.cs:743) is overwritten on the next frame. The shot heading (`_shotController.CameraHeadingRadians`) is also effectively locked because the visual feedback loop is broken — and likely because GroundLevel's pin-to-target framing is what computes the actual launch direction too.

This was always the case in P1's manual putter selection. §2f's auto-switch just makes it impossible to avoid noticing.

## Scope

1. **Allow yaw rotation in `Mode.GroundLevel`** while preserving the ground-level eye-height framing (~1.6m up, looking out from ball origin, NOT orbiting around the ball).
   - Approach A: replace pin-locked `lookAt` with a yaw-driven `lookAt`. Camera stays at `_shotOrigin + 1.6m`, looks along `Vector3(cos(yaw), 0, sin(yaw))` at distance, with a small downward pitch.
   - Approach B: keep current GroundLevel behavior but add a separate "putter aim" sub-mode that allows yaw rotation; toggle on putter entry.
   - **ARCHITECT-LOCKED 2026-05-14 09:30 JST: Approach A.** Single coherent mode (camera-at-eye + yaw-driven look). Pitch: ~10° downward (configurable constant `kGroundLevelPitchDeg = 10f`). LookAt distance: 10m (same as current). Eye height: 1.6m (unchanged).
2. **Verify aim heading propagates to shot launch direction.** `_shotController.CameraHeadingRadians = _cameraYaw` already sets this in `HandleCameraOrbit`, but confirm the shot fire path uses this value in putter mode (it may be overridden by some pin-aim helper).
3. **Tests:** 2-3 EditMode tests covering GroundLevel + yaw rotation; assert camera `transform.position` stays near-pinned but `transform.rotation.eulerAngles.y` tracks `_cameraYaw`.
4. **Smoke evidence:** capture 3 frames: putter at rest, after dragging aim left ~30°, after dragging aim right ~30°. Three distinct camera headings, ball position fixed.

## Out of scope

- Overhead/top-down green camera. Cesar locked this as future polish in P1 SPEC line 499.
- Stimp readout, putt-line-prediction overlays. Independent polish.
- Anything to do with `Mode.Chase` (this fix is scoped to `Mode.GroundLevel`).
- Putter mode auto-toggle itself (§2f, already shipped).

## Hard rules

1. Do NOT touch `EnterPutterMode` / `ExitPutterMode` bodies. They're protected by §2f Hard Rule 1 and earlier P1 specs.
2. Do NOT modify `BallSimulation.cs`, `Trajectory.cs`, or any aero CSV. This is a camera + aim-input fix, not a physics change.
3. Do NOT introduce a separate aim-vs-camera-yaw split unless absolutely necessary. Today `_cameraYaw` drives both; keep that invariant.
4. Test gate must remain bit-exact pre-existing + N new tests.

## Definition of done

- In putter mode (auto-entered via §2f or manually selected), mouse-drag rotates the camera sideways and the resulting shot fires in that direction.
- Camera framing stays low (ground-level eye height), NOT orbital.
- Smoke evidence shows 3 distinct yaws with fixed ball position.
- 2-3 new EditMode tests PASS; baseline+N target met.
- Cesar Lesson O verification: putter aim rotates freely.

## Estimate

Half-day to 1 day. The fix itself is small (one switch case in `ChaseCamera`), but verifying the shot-aim path and writing tests for GroundLevel framing takes the bulk of the time.

## References

- [ChaseCamera.cs:111-122](Assets/Scripts/Physics/Viewer/ChaseCamera.cs:111) — `Mode.GroundLevel` case
- [PhysicsLabController.cs:715-749](Assets/Scripts/Physics/Viewer/PhysicsLabController.cs:715) — `HandleCameraOrbit` (the orbit input path; currently overridden by GroundLevel LateUpdate)
- `Docs/Specs/Completed/putter_p1_ui/SPEC.md` line 499 — P1 explicit carve-out: "Camera lock to green / overhead view is a follow-up task."
- `Docs/Specs/Completed/loop_v1_2f_putter_p2_in_context/` — Cesar's Lesson O surfaced this bug; §2f is what put it in front of the player automatically.
