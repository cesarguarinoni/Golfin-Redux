# SPEC — `fade_draw_aim_line_bend`

> Authoritative spec. Notion Order **355** (P2, Gameplay Polish, Queued).
> UNBLOCKED by Order 356 (`fade_draw_core_wiring`, DONE) — the ball now actually curves from the club handle, so there is a real curve for the line to mirror.
> This is the order that lets Cesar human-confirm the fade/draw feel ("easier to read with the bent line").

## Status
See `STATUS.md`. Currently `SPEC_READY`.

## Goal
Bend the in-game aim direction LINE so it visibly curves to match the fade/draw the ball will take. Today the line is straight; after 356 the ball curves but the player has no on-screen read of where a shaped shot goes. This adds that read — an APPROXIMATE, sign-faithful curved guide. Not exact (a guide must not be exact, and exact per-frame sim was rejected on the putter); the curvature is a cheap parametric screen-space curve calibrated against 356's measured trajectory.

## Decisions locked with Cesar (do NOT relitigate)
- **D1 — APPROXIMATION, not a sim.** Cheap parametric screen-space curve; no per-frame `BallSimulation`. Must be sign- and trend-faithful to 356, not numerically exact.
- **D2 — bend the LINE, not the cone.** The cone is the club aiming + power zone — out of scope. The element is `_targetingLine`.
- **D3 — trigger = fade/draw armed.** Line bends only when `ShotMode == FadeDraw`; bend magnitude ∝ `ConeFinetuneX`. In Straight mode the line stays straight (rotates with aim only, as today).
- **D4 — default reach at rest; power moves it.** At Idle/Aiming, draw at a default reach; during Pulling/Timing, re-evaluate the curve with live power each frame so it extends/retracts and bends. Still no sim.
- **D5 — sign-faithful to 356.** Handle LEFT = DRAW = curves one way; handle RIGHT = FADE = the other. Match the signs from 356's runtime (`fadeDrawInput −1.0 = draw`, `+1.0 = fade`).
- **D6 — preserve the line's look.** `_targetingLine` is the `imgLine1` sprite (Figma `2714:3536`). The bent line reuses/segments that same sprite; do NOT restyle, recolor, or thin it (no look-regression — the Order-354 iter-5 lesson).

## Reference
- **Element:** `_targetingLine` (Figma `Line 1`, node `2714:3536`, file `5gEAHjl6xAtW8iYY7NMvWd`) — a `435×48` line that is the `imgLine1` raster sprite (no gradient tokens; ~14.6% vertical bleed). In code, updated by `ShotConeView.UpdateTargetingLine` (rotation-only today, anchored at canvas center = the ball/aim origin).
- **Calibration input (from Order 356, kept for this task):**
  - `Docs/Specs/Completed/fade_draw_core_wiring/trajectory_points.json` — real-runtime arcs over Hole 6.
  - Full handle (`ConeFinetuneX = ±1`, `FadeDrawMaxTiltRad = 0.3 ≈ 17°`): **DRAW +7.9m / FADE −9.3m lateral, 17.2m DRAW–FADE separation.** Note the slight asymmetry — symmetrizing is acceptable for a guide; do not invert it.
  - Model: `tilt = ConeFinetuneX × 0.3` (+ `spin.x × 0.075` trim). Lateral curve scales ~linearly with `ConeFinetuneX`, so intermediate values interpolate from the full-deflection arc.
- **Before any Figma work:** read `Docs/Reference/Figma_Lessons.md` (sandbox has no network for image fetch — delegate sprite pulls to Code or reuse the in-project line sprite).

## Verified current state (cite before changing)
- `ShotConeView.UpdateTargetingLine` projects ball→screen and target→screen, computes an angle, and ROTATES `_targetingLine` (a single rect). No bend capability.
- After 356: `ConeFinetuneX` and `ShotMode` reach the build path; `ShotController` exposes the armed state + handle value. The line update can read the same source the curve uses, so UI and ball share one input.

## Implementation
### Phase A — make the line bendable
- Replace the single-rect `_targetingLine` with a **segmented poly-line** (N segments, e.g. 12–20) that reuses the `imgLine1` sprite (tiled/segmented) or a sprite-textured `UILineRenderer`-style mesh preserving the sprite's width/look (D6). Straight case = the segments form a straight line identical to today's look.

### Phase B — the parametric curve (D1, calibrated)
- When `ShotMode == FadeDraw`: build the poly-line as a curve whose lateral excursion grows along its length and scales with `ConeFinetuneX`. Fit the shape to 356's arc (quadratic-in-distance lateral offset reproduces a Magnus-style curve well): `lateralOffset(t) = curveSign · |ConeFinetuneX| · k · t²` along the line param `t∈[0,1]`, with `k` chosen so full deflection at default reach matches the screen projection of ~8–9m lateral from `trajectory_points.json`.
- `curveSign` from `ConeFinetuneX` sign, matching D5 (draw vs fade). Optionally fold in `spin.x × 0.075` trim for extra fidelity — minor (¼ weight); fadeDraw-only is acceptable for v1.
- Straight mode → zero lateral offset (straight line, rotation only).

### Phase C — power response (D4)
- Line reach (length) and therefore the absolute curve at the tip scale with shot power. At Idle/Aiming use a default reach (config knob, e.g. matches a mid/default power). During Pulling/Timing, recompute reach + curve from live `PowerNormalized` each frame (cheap parametric eval).

### Phase D — bounds (absolute, full-res)
- Clamp the curve so the bent tip never overshoots the screen/flag region at full resolution (the Order-354 iter-3 lesson: check absolute bounds against the real anchor, not just relative scaling). Verify at 1170×2532 over a real hole.

## Config additions (`ControlsConfig.Default` + `controls.csv`)
- `AimLineDefaultReachPx` (or reuse existing line length) — default line reach at rest.
- `AimLineCurveScale` (`k`) — screen-space curvature gain calibrated to 356's lateral metres.

## Acceptance checklist (Implementer fills `IMPLEMENTER_REPORT.md`)
- [ ] Fade/draw ARMED + handle LEFT → line curves the DRAW direction; handle RIGHT → FADE direction; **signs match 356** (`fadeDrawInput −1 = draw`).
- [ ] Bend magnitude scales smoothly with `ConeFinetuneX` (no snapping).
- [ ] Straight mode → line is straight (rotation only), pixel-identical look to today.
- [ ] Power changes during Pulling/Timing visibly move/extend the curve (D4).
- [ ] Curve never overshoots screen/flag at full res over a real hole (D5/Phase D).
- [ ] Line look preserved — same sprite, width, no recolor/thinning (D6); diff the straight-mode render against current.
- [ ] No per-frame `BallSimulation` (D1) — confirm the curve is parametric.
- [ ] EditMode tests: curve sign vs `ConeFinetuneX`, magnitude monotonic in `|ConeFinetuneX|`, straight when not armed, power scaling.
- [ ] Unity Console clean.

## Capture gate — READ THIS (Order 356 was rejected purely on capture)
The 356 video was bounced for fighting the camera. This order's proof is the **UI line**, which is always visible in the normal chase cam — so there is NO reason to touch the camera. Mandatory:
- **Normal play, normal chase camera. No camera-mode switching, no overhead/side/Downrange.** (Also avoids the y-flip.)
- **Arm fade/draw through the REAL UI toggle** (`ShotModeContext.Toggle` via the on-screen button), so the captured ShotMode button reads "FADE/DRAW" — do NOT set `FadeDrawActive` directly.
- **Sensible shot** on a real loaded hole (1170×2532); ball stays in play. Fix the shot, not the camera.
- Show, in normal play: straight line (mode off) → arm via the button → line visibly bends with the handle → matches the ball's actual curve direction.
- Lock all camera/render state before `StartRecording`.

## Files this task touches
- `Assets/Scripts/Gameplay/UI/ShotUI/ShotConeView.cs` — `UpdateTargetingLine`: poly-line build, parametric curve, power + armed-state reads.
- Possibly a new small `UILineRenderer`-style helper for the segmented sprite line (Gameplay.UI asmdef).
- `Assets/Scripts/Gameplay/Config/ControlsConfig.cs` + `controls.csv` — `AimLineCurveScale`, `AimLineDefaultReachPx`.
- `Assets/Scenes/Physics/LabScaffold.unity` — `_targetingLine` swapped to the segmented renderer (preserve sprite).
- New EditMode test file.

## Tier
**FULL PIPELINE (Tier 3)** — visual fidelity + runtime spatial math (screen-space curve, world→screen projection of reach, power-driven re-eval).

## Out of scope
- The cone (`ConeMeshGraphic`) — it is the club/power zone, untouched (D2).
- Any physics / 356 wiring change — the curve model is fixed; this only visualizes it.
- The spin disc UI (Order 354) / backspin / putts.
- `map_view_aiming` (Order 352).
