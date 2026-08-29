# Design note — flick-vector aiming ("scheme C")

**Status:** parked idea, NOT a spec. Cesar (2026-08-28): "leaning C for the alternative, but let's try to fix what we have." Revisit after `shot_aim_parity` + `shot_timing_power` have been felt on device.

## The idea
Aim = the direction of the up-flick through the ball, not the handle's lateral position. The cone becomes a **tolerance window**: a flick inside it aims where it points; outside it counts as a mishit. This is the Confluence 2024/9/17 reading (Neko Golf reference): "aim is defined by the flick path crossing the ball".

## What already exists that it would reuse
- `ShotController.PushTouchSample` ring buffer (6 samples, unscaled time) — the flick vector is `samples[newest] − samples[oldest-in-window]`, the same pair `EvaluateFlickGate` already measures for speed. Angle = `atan2(dx, dy)` of that vector, screen space.
- `ControlsConfig.FlickAngleDeviationMaxDeg` (30°) is in `controls.csv` today and unused — it is exactly the "outside the cone" threshold this scheme needs.
- `HalfConeAngleRad()` maps the screen-space flick angle to world yaw: `yaw = CameraHeading + clamp(flickAngle / FlickAngleDeviationMaxDeg, −1, 1) * halfCone`.
- The handle keeps setting power (vertical pull); its lateral position becomes fade/draw only — i.e. the Straight/FadeDraw toggle disappears (this is scheme B folded in).

## Why it is not the first move
- Least forgiving on a phone: a thumb flick has ~±8° of natural angular noise; with a 5° half-cone at low Accuracy the noise is bigger than the aim range. Needs a filter (e.g. aim = flick angle × `FlickAimGain` 0.5) and the tolerance shape to be tuned on device, which is feel work, not code work.
- It changes what the targeting line can show before the flick (nothing — the aim does not exist yet), so the line becomes a post-hoc trail or the camera heading only. That is a UX decision Cesar has to look at, not a diff.
- `shot_aim_parity` fixes the current scheme with a small diff and a parity test; if that feels right, C is unnecessary.

## If it goes ahead — the shape of the spec
1. `ShotController.ComputeFlickYaw()` from the sample window; fall back to the latched handle aim when the window has < 2 samples (programmatic drivers unchanged).
2. Config: `FlickAimGain` (new), reuse `FlickAngleDeviationMaxDeg`; mishit outside the window = `DegradationYawDegPerPass × N` or a power cut — decide with Cesar.
3. Line: show camera heading + handle fade/draw bend during aim; after commit, rotate to the resolved yaw for the `Flicking` frame(s).
4. Tests: yaw from a synthetic sample pair; gain; clamp; sampleless parity.
