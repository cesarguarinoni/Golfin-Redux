# Queued NOTES — `putter_aim_blue_line`

**Filed:** 2026-05-25
**Fires after:** any time (zero file overlap with `live_stat_provider_wiring` or `spin_and_shot_shape_wiring`)
**Priority:** P1 (visual polish for the now-shipped green grid; not gameplay-blocking)
**Estimate:** S (2–4 hr Code time + pipeline)
**Reference:** Cesar attached a Winning Putt screenshot 2026-05-25 showing a thin straight green/cyan aim-line running from the ball straight along the player's heading, OVERLAID on the green grid. The line is narrow (~10cm world width), uniformly bright, runs the full available distance to the cup or beyond.

## Problem

The new green-reading grid (warped wireframe, shipped in `puttpath_predictor_perf_and_design` 2026-05-25) shows slope topology beautifully — but the player has no straight visual cue for *their current aim direction*. Where exactly is the ball going to start rolling? Existing `PutterTrack` may serve part of this purpose but needs a check:

- It might already render a thin aim-line that's just visually lost under the new grid (z-fight, color contrast, width too small).
- Or it might render a different visual entirely (a cone or stretched track shape), not the clean straight line in the reference.

**Pre-flight required:** read `Assets/Scripts/Gameplay/UI/ShotUI/PutterTrack.cs` (or wherever the `_putterTrack` SerializeField on `ShotConeView` points) + take a fresh putter-aim screenshot. Determine which of these is true:

- **Scenario A:** PutterTrack already renders a straight thin line, just visually drowned by the new grid → fix is rendering-tuning (color/width/Z-offset on top of grid).
- **Scenario B:** PutterTrack renders something different (cone, fan, track) → either add a new aim-line component alongside, or replace PutterTrack's geometry with the straight line.
- **Scenario C:** PutterTrack is absent or not active during the new grid's lifetime → wire a new aim-line component into the putter aim state.

## Goal

While the player is in putter aim mode, render a thin straight aim-line from the ball position along the current aim heading. Line:

- World-space, stays anchored to ball + camera-aim heading.
- Thin (~5–10cm world width — narrow enough to feel like a precision marker, wide enough to read from chase-cam distance).
- Bright cyan/light-blue color matching the Winning Putt reference (HEX TBD — eyeball from the reference and put in SPEC).
- Renders ON TOP of the green grid (z-write disabled or higher render queue).
- Length: extends from ball forward to either (a) cup line-of-sight, or (b) a fixed reasonable distance like 15m. Architect lean: fixed 15m, simpler; cup-aware is future polish.
- Hides on shot start / non-putter mode (follows the same lifecycle as `PutterTrack` per Lesson Q work in `putter_cone_per_shot_lifecycle`).

## Architecture sketch (refine in SPEC after pre-flight)

If **Scenario A** (PutterTrack exists, just needs polish):
- Material/shader tweak on PutterTrack: bump color saturation + brightness, add `_RenderQueue` bump so it sorts above the green grid.
- Adjust line width via PutterTrack's existing knob.

If **Scenario B** (PutterTrack is different geometry):
- New child GO `PutterAimLine` under `PutterTrack`'s parent.
- `LineRenderer` component or simple stretched-quad mesh.
- Wire its lifecycle to follow `PutterTrack._enabled` (or whatever the existing visibility gate is).

If **Scenario C** (absent):
- New MonoBehaviour `PutterAimLineWidget` in `Golfin.Gameplay.UI.ShotUI`.
- Subscribes to `ShotController.OnStateChanged` like `PutterTrack` does; activates on putter Aiming, hides otherwise.
- LineRenderer in front of ball, world-space, anchored to ball position + ShotController's live aim heading.

## Open Q's

- Q1: Scenario A/B/C — locked by pre-flight read.
- Q2: Line width (world meters). Lean: 0.08m (8cm).
- Q3: Line length. Lean: fixed 15m. Future polish: extend to cup if line-of-sight, otherwise 15m.
- Q4: Color. Lean: `#7AE9FF` (light cyan, eyeballed from reference). Lock with Cesar after first capture.
- Q5: Z-offset above grid (must clear `_surfaceYOffset = 0.02m` from the grid). Lean: 0.04m (4cm) above terrain mesh so it sits 2cm above the grid mesh.

## Visual gate

Manual play screenshot on production Hole 1 putter-aim:
- Blue line clearly visible on top of the green grid, no z-fight.
- Line stays anchored to ball and aim heading as the player rotates camera.
- Line hides when the ball is hit / leaves putter mode.

Plus the bot-recorded video `PutterAimWarpedGridOnTestGreen` extended to demonstrate the line.

## Out of scope

- Aim line for iron / driver shots (separate cone visualization handles those already).
- Distance markers along the line (Winning Putt has tick marks every meter; future polish).
- Putt-strength indicator integrated into the line (orthogonal feature).
- Curve prediction on the line (this is the "Sim positioning" anti-feature per the `puttpath_predictor` L1 design lock — grid + line is the full feedback set, no live curve).

## Pipeline

TIER 2 (TellCode) likely sufficient — single component, visual fidelity gated by Cesar's eyeball + bot video. TIER 3 acceptable if pre-flight reveals Scenario C (new component creation justifies the chain).

## Sequencing

Zero file overlap with the other two specs. Can fire in any order — likely lands fastest because the scope is the smallest of the three.
