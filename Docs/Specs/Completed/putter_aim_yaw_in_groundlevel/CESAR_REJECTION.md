# Cesar rejection — iter-4 (2026-05-14, second rejection)

**Verdict:** REJECTED after ARCHITECT_REVIEW_PASS for the second time. Reviewers verified the math equation is right but missed that the math is run through `SmoothDamp` and the iron path is not, so the runtime behavior diverges during drag. The ball "swims" across the screen — not a HUD-occlusion issue, an actual camera-path bug.

## Cesar's words

> "Fail. Ball changes screen position while you drag → camera is orbiting, not yawing. Fail. MAKE IT WORK LIKE THE NORMAL SHOT CAMERA EXACTLY. STOP MAKING UP SHIT."

## Root cause (now diagnosed, no more guessing)

Two camera-write paths exist, with DIFFERENT integration:

- **Iron / non-putter (works):** [`PhysicsLabController.ApplyCameraYaw`](../../../../Assets/Scripts/Physics/Viewer/PhysicsLabController.cs:801) writes `cam.transform.position` and calls `LookAt` **directly, every frame, with zero smoothing**. Position and rotation stay coherent on every frame. Ball pixel-pinned during drag.
- **Putter / GroundLevel (broken):** [`ChaseCamera.RunLateUpdateLogic` Mode.GroundLevel branch (lines 156-174)](../../../../Assets/Scripts/Physics/Viewer/ChaseCamera.cs:156) computes the same `desiredPos` and `desiredRot`, then at lines 209-211 runs:
    ```csharp
    transform.position = Vector3.SmoothDamp(transform.position, desiredPos, ref _velocity, smoothTime, Mathf.Infinity, dt);  // smoothTime = 0.08f
    transform.rotation = Quaternion.Slerp(transform.rotation, desiredRot, 10f * dt);
    ```
    During drag the position lags by ~80ms and the rotation lerps at a different rate. The two never converge while yaw is still changing → camera-to-LookAt vector is not parallel to camera-to-ball vector → **ball drifts across screen** every frame.

The static-equilibrium math both reviewers verified is right. But Cesar plays in dynamic equilibrium (mid-drag), where smoothing makes the math meaningless. Both reviewers should have caught this — flag in lessons.md.

## The fix (Cesar-locked, surgical)

**Make putter take the EXACT same camera-write path as iron during Aiming.** Don't duplicate math, don't add another smoothing branch, don't invent a new mode. Two-line change:

1. **`ChaseCamera.cs:141`** — extend the early-return so `Mode.GroundLevel` also bails out when target is null, identical to how `Mode.Chase` already does:
    ```csharp
    // BEFORE:
    if (_target == null && _mode == Mode.Chase) return;
    // AFTER:
    if (_target == null && (_mode == Mode.Chase || _mode == Mode.GroundLevel)) return;
    ```
    Now `ChaseCamera` does NOT write the transform during putter Aiming. The GroundLevel branch math (lines 156-174) becomes inert during Aiming — it still runs during Flying/Rolling if target is non-null, which is fine.

2. **`PhysicsLabController.HandleCameraOrbit` (lines 782-794)** — drop the GroundLevel-vs-Chase branch and always call `ApplyCameraYaw`:
    ```csharp
    // BEFORE:
    if (chaseCamera != null && chaseCamera.CurrentMode == ChaseCamera.Mode.GroundLevel)
    {
        chaseCamera.SetGroundLevelYaw(_cameraYaw);
    }
    else
    {
        Camera cam = chaseCamera?.GetComponent<Camera>();
        if (cam != null) ApplyCameraYaw(cam);
    }
    // AFTER:
    Camera cam = chaseCamera?.GetComponent<Camera>();
    if (cam != null) ApplyCameraYaw(cam);
    // (SetGroundLevelYaw can be called too if any test still depends on it, but
    //  it's now decorative — the actual camera transform comes from ApplyCameraYaw.)
    ```

That's it. `EnterPutterMode`'s `SetMode(GroundLevel)` call stays untouched (Hard Rule 1 honored). The GroundLevel-branch orbit math in `ChaseCamera` stays but is dead code during Aiming. The iron path takes over for putter. Identical behavior.

## Hard rules (re-stated)

- DO NOT modify `EnterPutterMode` / `ExitPutterMode` bodies.
- DO NOT duplicate the orbit math in `ChaseCamera.Mode.GroundLevel` with smoothing on top. If you keep the branch at all, it must be inert during Aiming.
- DO NOT add a third orbit path. There is ONE orbit math (in `ApplyCameraYaw`), and putter must use it.

## Tests to rewrite

The GL-1/GL-2/GL-3 tests in `LoopCameraDirectorTests.cs` were rewritten in iter-3 to assert that `ChaseCamera.Mode.GroundLevel` produces specific orbit positions/rotations. **Those assertions are now wrong** because `Mode.GroundLevel` will no longer drive the transform during Aiming. Either:

- (a) Update the tests to assert behavior of `ApplyCameraYaw` directly (camera position is `_orbitCenter - lookDir*8 + up*3`, LookAt is `_orbitCenter + lookDir*3 + up*0.5`), regardless of mode, OR
- (b) Drop GL-1/GL-2/GL-3 and replace them with one combined "putter Aiming uses ApplyCameraYaw" integration test that triggers `HandleCameraOrbit` with `chaseCamera.SetMode(GroundLevel)` set and asserts the resulting transform matches iron at the same yaw.

Pick (b) — fewer tests, asserts the actual behavior contract, not implementation detail.

## Verification bar (binding, do not ignore)

In play mode, on Lomond H1, with the ball at rest and the putter active:

1. **Drag yaw left ~30°, drag yaw right ~30°, no-drag.** Capture three screenshots via `CaptureCore.SnapPlayModeSafe`.
2. **In all three screenshots, the actual 3D ball — NOT the `CentralBallWidget` HUD overlay — must occupy the SAME pixel position to within ±5 pixels.** Use `gameobject-find` + `script-execute` to compute the 3D ball's world-to-screen point and confirm.
3. **Also capture the same three yaws with an iron (or any non-putter club) at any position.** The 3D ball must be at the same screen position in all three iron captures too (this is just a sanity baseline). 
4. **The putter 3D ball pixel position and the iron 3D ball pixel position should match to within ±20 pixels** (small differences from different ball Y on green vs fairway are OK; large differences are NOT).

If the actual 3D ball drifts across the screen during the drag, you have NOT fixed the bug. The HUD rail will keep occluding it visually — that's a separate task and out of scope here. What matters is the 3D ball's projected screen position.

## What to drop from prior iterations

- The `_groundLevelOrbitCenter` field and `SetGroundLevelOrbitCenter` API on `ChaseCamera` — no longer needed if GroundLevel is inert during Aiming. Either delete them, or leave them for the Flying/Rolling case if that still uses GroundLevel math (verify whether any production path needs them; if not, delete).
- The `kGroundLevelPitchDeg` constant — already gone in iter-3, just confirming.
- The GL-1/GL-2/GL-3 tests asserting `ChaseCamera.Mode.GroundLevel` orbit framing — replace with the integration test described above.

## Files affected (re-do scope)

- `Assets/Scripts/Physics/Viewer/ChaseCamera.cs` — extend early-return at line 141; the GroundLevel branch becomes inert during Aiming
- `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs` — `HandleCameraOrbit` drops the GroundLevel-vs-Chase branch
- `Assets/Scripts/Physics/Tests/LoopCameraDirectorTests.cs` — replace GL-1/GL-2/GL-3 with one integration test against `ApplyCameraYaw`
- `Docs/Specs/Active/putter_aim_yaw_in_groundlevel/SPEC.md` — update § Scope §1 to reflect this surgical approach. Replace the iter-3 CESAR-LOCKED note with: "CESAR-LOCKED 2026-05-14 (iter-5): putter Aiming uses the iron `ApplyCameraYaw` path verbatim. `ChaseCamera.Mode.GroundLevel` early-returns during Aiming. No duplicated math, no smoothing on the putter camera path."

## Note to implementer

The math being right does NOT mean the behavior is right. Cesar plays in dynamic equilibrium (mid-drag). All three reviewers (self, architect, and me re-checking) approved a static-equilibrium math match and missed the integration-path divergence. This is exactly the failure mode CLAUDE.md § visual review checklist step 5 ("Implementer-graded PARTIAL → FAIL default") and step 6 ("production-flow capture") are meant to catch. Production-flow capture in iter-3 SHOULD have shown the drift during drag — but the 3 captures were three discrete frames, not a video of the drag, so the lag was invisible. **For iter-5, after taking the three discrete captures, ALSO record a brief play session (e.g. note the timing) where you continuously drag and confirm the 3D ball stays put.** A static at-rest capture cannot prove a dynamic invariant.

## Status

`STATUS.md` → `CESAR_REJECTED`. Implementer must re-do from this rejection, NOT from the original SPEC alone. The original SPEC is also wrong on the architect-lock; treat this rejection as the binding source.
