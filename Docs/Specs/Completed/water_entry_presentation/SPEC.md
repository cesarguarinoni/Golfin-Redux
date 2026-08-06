# SPEC — `water_entry_presentation`

## Goal

Make a water landing read as an event. Today the ball stops dead on the surface, the camera hard-freezes on the contact frame, and the splash — which does fire — is invisible.

## Diagnosis (measured 2026-08-06, Hole 6 water shot)

| Finding | Evidence |
|---|---|
| Camera is aimed correctly, not the "OB camera" | At freeze: camPos `(-13.01, 10.69, -8.54)`, impact at viewport `(0.518, 0.597)`, `onScreen=True`, dist 8.2 m, `mode=Chase` for 12 consecutive frames |
| Camera hard-freezes on the contact frame | `LoopCameraDirector` calls `SetTarget(null)` on `BallState.OB`; `ChaseCamera.RunLateUpdateLogic` early-returns on null target in Chase → transform never written again (K10 ruling 2026-08-05) |
| Ball never sinks | Consecutive frames 4.55 s → 5.22 s: ball motionless on the surface with the red OB line. World is not paused (water shader frame-diff steady at ~3.8) |
| Splash fires correctly | Spawns at `(-20.38, 7.27, -7.26)`, all 4 systems emitting, alive 0.8 s |
| Splash is invisible | All 3 splash materials at `renderQueue 3000` — **same queue as the water surface** (`URPWater/Standard`, 3000) — and the splash is coplanar with it (raycast: `Water_1` y = 7.27, splash y = 7.27). Water sorts over it. |
| The authored fix never took effect | HEAD authors `M_Splash{Foam,Ring,Droplet}.mat` at `m_CustomRenderQueue: 3100`; Unity resets them to 3000 on every load (the recurring `M_Splash*.mat` churn) |

## Scope decisions (Cesar, 2026-08-06)

- **Camera:** "The call I made yesterday was not for the water. Stop the camera on contact but don't freeze until after the splash plays." The K10 stop-chasing rule stands for non-water OB. On water, the camera stops *advancing* at contact (the existing chase clamp already does this) and stays **live** until the splash finishes, then freezes.
- **Ball:** sinks through the surface and disappears (~0.5 s), rather than resting on it.
- **Splash materials are off-limits** — `M_Splash*.mat` is under the standing ban, and the importer resets the queue anyway. Force the queue on the runtime **material instance** instead.

## Implementation

1. **`WaterSplashController.cs`** — on creating `_splashInstance`, set `renderQueue = 3100` on every `ParticleSystemRenderer`'s **material instance** (`r.material`, not `sharedMaterial`) so the splash draws after the water and cannot be undone by the importer.
2. **`WaterSplashController.cs`** — ball sink. `Configure(BallAnimator anim, …)` already receives the animator (currently discarded); use `anim.CurrentBall` to lerp the ball down by `_sinkDepth` over `_sinkDuration`, then deactivate it. `BallAnimator.PlaceAtRest` destroys and respawns the instance for the next shot, so deactivating is safe.
3. **`LoopCameraDirector.cs`** — on `BallState.OB`, branch on `OBReason`: Water defers `SetTarget(null)` by `_waterHoldSeconds`; every other reason clears immediately (K10 unchanged).
4. **`LoopCameraDirectorTests.cs`** — the existing water-OB test asserts the target is cleared immediately; update it to the new intent (live during the hold, cleared after).

## Acceptance

- [ ] Water landing: splash is clearly visible over the water surface
- [ ] Ball sinks and disappears instead of resting on the surface
- [ ] Camera stops advancing at contact but keeps rendering through the splash, then settles
- [ ] Non-water OB (boundary) behaviour unchanged — camera still freezes at contact
- [ ] `M_Splash*.mat` unmodified in `git diff`
- [ ] EditMode camera-director tests pass
- [ ] Console clean
