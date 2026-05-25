# Queued NOTES — `spin_and_shot_shape_wiring`

**Filed:** 2026-05-25
**Fires after:** `live_stat_provider_wiring`
**Priority:** P0 (gameplay foundation, blocks meaningful character/club differentiation in flight)
**Estimate:** S–M (4–8 hr Code time + pipeline)

## Problem

UI exists for spin input (`Gameplay/UI/ShotUI/SpinPanelWidget.cs` — 5 positions: center / top / bottom / left / right, writes to `SpinContext.Spin` static `Vector2`). But the spin physics path never reads it:

- `Physics/Stats/ShotInputBuilder.cs:86` hardcodes the spin axis to the right-vector (`new fp3(-sinYaw, fp.Zero, cosYaw)`) and derives magnitude solely from `bundle.Club.Value.BaseBackspinRpm × resolved.SpinMagnitudeMultiplier`.
- Putts always get `SpinState.None`.
- `SpinContext` is read only by `SpinPanelWidget` itself (to snap its dot) and `Editor/CaptureHelper.cs` (reset for screenshots).

Fade/draw is the same gap: there is no shot-shape control or sidespin generator anywhere. But the aero model (`AeroModel.cs`) already consumes a full 3D `SpinState` and computes Magnus on the actual spin vector — so tilting the spin axis off the pure right-vector will produce lateral lift and curve the ball in flight. **Spin wiring and fade/draw are the same fix.**

## Goal

Make the `SpinContext.Spin` Vector2 input affect the actual physics shot. Concretely:

- `SpinContext.Spin.y > 0` (top of ball) → reduce backspin magnitude or sign-flip to topspin (forward roll); ball runs further on the ground and dives faster in flight.
- `SpinContext.Spin.y < 0` (bottom of ball) → increase backspin magnitude; ball rises higher, stops faster on landing.
- `SpinContext.Spin.x != 0` (left/right of ball) → tilt the spin axis around the velocity vector to introduce sidespin → ball curves left (draw, for a right-handed swing) or right (fade) in flight via Magnus.

Result: the spin UI becomes meaningful for the first time, AND fade/draw is implemented as a side-effect because side-spin curves the ball.

## Architecture sketch (refine in SPEC)

**Single edit point: `ShotInputBuilder.Build`.**

Add an optional parameter `Vector2 spinInput` (or extend `StatBundle` with a per-shot spin field — TBD in spec). In the swing branch (`!bundle.IsPutt`), modify the spin axis + magnitude:

```csharp
// Existing:
var spinAxis = new fp3(-sinYaw, fp.Zero, cosYaw);  // pure right-vector backspin
fp spinMag = baseRpm * fpMath.TwoPi / fp.FromInt(60) * resolved.SpinMagnitudeMultiplier;

// New:
fp spinY = fp.FromFloat(spinInput.y);  // -1 = back of ball (more backspin), +1 = top of ball (forward spin)
fp spinX = fp.FromFloat(spinInput.x);  // -1 = left of ball (draw axis), +1 = right of ball (fade axis)

// Magnitude: y < 0 boosts backspin, y > 0 reduces / inverts.
fp magScale = fp.One - spinY * fp.FromFloat(0.5f);  // tuning constant, locks in SPEC
fp finalMag = spinMag * magScale;

// Axis tilt: rotate around velocity vector by spinX × maxTiltAngle.
fp tiltAngle = spinX * fp.FromFloat(0.3f);  // ~17° max tilt at full sidespin; locks in SPEC
spinAxis = RotateAroundVelocity(spinAxis, velocityNormalized, tiltAngle);

spin = new SpinState(spinAxis, finalMag);
```

**Plumbing: `ShotController.CommitFlick` → `ShotInputBuilder.Build`.** Read `SpinContext.Spin` at `CommitFlick` entry, pass through to Build.

**Putts:** keep `SpinState.None` for v1 (Phase 5 design lock). Adding spin to putts is a separate ticket.

## Open Q's (lock in SPEC)

- Q1: Tuning constants — `magScale` slope (how much spin.y modulates magnitude), `tiltAngle` max (how much spin.x tilts the axis). Need playtest to dial. Architect lean: start `0.5f / 0.3rad` (above) and iterate.
- Q2: Sign convention — does spin.y > 0 mean "press top of ball" (topspin, ball runs) or "ball nose goes up" (more backspin, ball climbs)? Need to match the SpinPanel UI's visual representation.
- Q3: Symmetric tilt or asymmetric (allow draw and fade equally)? Architect lean: symmetric. Realistic golf has slight asymmetry (right-handers naturally fade) but that's a future tuning knob.
- Q4: Where do tuning constants live? `ControlsConfig.Default` extension vs a new CSV row vs hardcoded in Build. Lean: `ControlsConfig` so it's live-tunable.
- Q5: Does the existing SpinPanel UI need to change? 5 discrete positions (center / N / S / E / W) may feel coarse for fade/draw — the player wants continuous control. Lean: ship spin wiring with the current 5-position UI; revisit UI as a follow-up if Cesar wants finer control.

## Visual gate

Manual play in production gameplay:
- Fire a driver shot with `SpinContext.Spin = (0, 0)` — baseline straight shot, no curve.
- Fire the same driver shot with `SpinContext.Spin = (-1, 0)` (left of ball, draw) — ball curves left.
- Fire the same with `(+1, 0)` (right of ball, fade) — ball curves right.
- Fire with `(0, +1)` (top of ball) — ball rolls further on landing, lower trajectory.
- Fire with `(0, -1)` (bottom of ball) — ball climbs higher, stops faster on landing.

All five shots must visibly differ. One-line description of each in the implementer report.

## Out of scope

- Putt spin (Phase 5 design lock; revisit only if Cesar wants short-game spin).
- Continuous spin UI (current 5-position widget stays for v1).
- Per-character spin modifiers (`SpinMagnitudeMultiplier` already does this via character stats; this SPEC just adds the user-input dimension).
- Replay determinism extensions — the existing seed already covers per-shot variance; spin.xy adds 2 floats per shot to the replay record, trivial.

## Pipeline

TIER 2 or TIER 3 — caller's call. The work is structurally small (one Build edit + one CommitFlick edit + one SpinState math helper + tuning) but the visual gate is a 5-shot manual verification that warrants the chain. Lean TIER 3 to keep the visual-gate discipline.

## Sequencing

Fires after `live_stat_provider_wiring` (character/club stats need to be live before spin's "per-club spin multiplier" path means anything in production). Touches zero files in `live_stat_provider_wiring` scope. Could run before `putter_aim_blue_line` (rendering-only, no overlap).
