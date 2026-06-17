# SPEC — `fade_draw_core_wiring`

> Authoritative spec. Notion Order **356** (P2, Gameplay Polish, Queued).
> PREREQUISITE for Order 355 (`fade_draw_aim_line_bend`) — that line viz cannot start until this lands, because there is no fade/draw ball curve to draw until this wires one.
> Finishes the deferred "Part D" finetune wiring (origin in `Docs/Archive/` history; no live order owned it).

## Status
See `STATUS.md`. Currently `SPEC_READY`.

## Goal
Make the fade/draw mechanic actually affect the ball. Today the club handle and the Fade/Draw toggle are scaffolding: `ConeFinetuneX` moves a sprite but never reaches the builder, and `ShotMode` is read only by UI. After this task: arming the Fade/Draw toggle and positioning the club in the cone curves the ball; with the toggle off, the same handle nudges aim; sidespin is demoted to trim so fade/draw owns the left-right curve. NO new visual surface — this is input->physics wiring.

## Verified current state (cite before changing)
- **The only wired left/right curve is `spin.x`.** `ShotInputBuilder.Build` (`Assets/Scripts/Physics/Stats/ShotInputBuilder.cs:104-110`) computes orbital tilt = `spinInputX * spinMaxTiltRad`. Driven solely by the spin disc.
- **`ConeFinetuneX` is cosmetic.** Computed in `ShotController.ComputeFinetune()` (`ShotController.cs:365`, `dx/150` clamped +/-1, comment "refined in Part D"), stored in `ShotInputState.ConeFinetuneX`, read ONLY by `ShotConeView.UpdateClubHandle` (`ShotConeView.cs:250`, positions `_clubHandle`). Never reaches the builder or controller shot math.
- **`ShotMode` is inert in physics.** `ShotModeContext.Mode` (`HUD/ShotModeContext.cs`, enum `{Straight, FadeDraw}`, static) is read only by UI widgets. Build path never reads it.
- **Cone width IS wired to club accuracy (do NOT touch).** `ShotController.HalfConeAngleRad()` = `lerp(ConeHalfAngleAtAcc0Deg=5, ConeHalfAngleAtAcc100Deg=20, Accuracy/120)` (`controls.csv`). Higher accuracy = wider cone. `UpdateClubHandle` already bounds handle travel by `maxX = halfBase * widthFraction`, so the accuracy-sized cone is the natural bound on fade/draw range — for free.
- **Build site:** `ShotController.CommitFlick` (`ShotController.cs:289-306`) reads `PendingSpinInput`, builds `spinInputX/Y`, calls `ShotInputBuilder.Build(...)`. This is the single integration point.

## Decisions locked with Cesar (do NOT relitigate)
- **D1 — fade/draw is driven by the club handle (`ConeFinetuneX`), separate from the spin disc.**
- **D2 — accuracy = wider cone = more shaping room** (current `5deg->20deg` lerp confirmed; do not flip).
- **D3 — sidespin demoted to TRIM.** Reduce `spin.x`'s tilt contribution so fade/draw owns the curve. `spin.y`/backspin unchanged. **Spin disc UI unchanged (Order 354 stays intact).**
- **D4 — dual-purpose handle, mode-switched.** Toggle OFF = aim nudge; toggle ON = fade/draw curve.
- **D5 — on arming the toggle: lock aim at the tuned value, re-center the handle to 0, then handle offset drives the curve** (clean path, avoids double-counting the offset as both aim and curve).
- **D6 — putts unchanged** (zero spin, no fade/draw).

## Implementation

### Phase A — route the mode + handle into the build path
- In `CommitFlick`, read `ShotModeContext.Mode` and the current `ConeFinetuneX` (same source `ComputeFinetune` feeds the state).
- Add `fadeDrawInput` + `fadeDrawMaxTiltRad` parameters to `ShotInputBuilder.Build(...)` (mirror the existing `spinInputX`/`spinMaxTiltRad` pattern; default 0 = legacy no-op so existing tests/callers are unaffected).

### Phase B — fade/draw curve (toggle ON)
- When `Mode == FadeDraw` and not a putt: `fadeDrawInput = ConeFinetuneX` (the re-centered offset, D5); else `fadeDrawInput = 0`.
- In the builder, the orbital tilt becomes:
  `tiltAngle = fadeDrawInput * fadeDrawMaxTiltRad + spinInputX * spinMaxTiltRad`
  with `spinMaxTiltRad` reduced to a TRIM magnitude (D3) and `fadeDrawMaxTiltRad` the dominant term. Keep all math in `fp` fixed-point; reuse the existing `fpMath.Rotate(startAxis, velocityDir, tiltAngle)` path (`ShotInputBuilder.cs:110`) — do not add a second rotation.
- Sign convention must match the existing spin tilt so a given handle direction and the equivalent sidespin curve the same way.

### Phase C — demote sidespin to trim (D3)
- Lower `SpinMaxTiltRad` in `ControlsConfig.Default` + `controls.csv` to a trim value (propose ~1/4 of its current value; tunable). Backspin/topspin (`spin.y` -> `magScale`) untouched.
- This is the entirety of Cesar's "make sidespin less relevant in the formula" note. Do NOT change the spin disc UI or `SpinContext`.

### Phase D — aim nudge (toggle OFF)
- When `Mode == Straight` and not a putt: `ConeFinetuneX` nudges launch yaw. Effective launch yaw = `AimYawRadians + ConeFinetuneX * AimNudgeRangeRad` (new config/CSV `AimNudgeRangeRad`, propose ~3deg full deflection; tunable). Apply where velocity is built from `AimYawRadians` in `CommitFlick`.
- No fade/draw tilt in Straight mode (`fadeDrawInput = 0`).

### Phase E — mode-transition behavior (D5)
- On `Toggle()` Straight->FadeDraw (arming): capture the current effective aim (`AimYawRadians + ConeFinetuneX * AimNudgeRangeRad`) as the LOCKED launch yaw for the shot; re-center the handle (`ConeFinetuneX -> 0`, `ShotConeView` resets `_clubHandle` to xOffset 0). Subsequent handle movement is curve, not aim.
- On FadeDraw->Straight (disarming): restore the handle as an aim-nudge control from center.
- Keep this state on `ShotController` (single source of truth); `ShotConeView` reflects it.

## Config additions/changes (`ControlsConfig.Default` + `Assets/Resources/Gameplay/controls.csv`)
- NEW `FadeDrawMaxTiltRad` — max curve at full handle deflection (propose = current `SpinMaxTiltRad` value, so fade/draw inherits the curve range spin.x used to have).
- NEW `AimNudgeRangeRad` — aim yaw nudge at full handle deflection (Straight mode), propose ~3deg.
- CHANGE `SpinMaxTiltRad` -> trim value (propose ~1/4 prior), D3.

## Acceptance checklist (Implementer fills `IMPLEMENTER_REPORT.md`)
- [ ] Toggle ON + handle deflected left vs right -> ball visibly DRAWS vs FADES (curved flight), verified by trajectory trace/capture over a real loaded hole.
- [ ] Toggle OFF + handle deflected -> ball LANDS left/right (aim shift) with NO curve.
- [ ] On arming, aim locks at the tuned value and the handle re-centers (D5) — verified, not just asserted.
- [ ] Sidespin still curves but visibly LESS than before (trim); a fade/draw handle deflection produces a larger curve than the same-magnitude spin.x.
- [ ] `spin.y` backspin/topspin unchanged (regression check vs 414 behavior).
- [ ] Putts unchanged: zero spin, no fade/draw, handle inert in putt mode (D6).
- [ ] Spin disc UI (Order 354) untouched: `git diff` shows no change to `SpinPanelWidget`/`SpinContext`/disc visuals.
- [ ] Determinism preserved: same seed + same inputs -> identical trajectory (fixed-point; replay/seed test).
- [ ] EditMode tests: tilt formula (fadeDraw dominant + spin trim, signs), aim-nudge mapping, mode-transition aim-lock.
- [ ] `Build(...)` new params default to 0 = legacy no-op (existing `ShotInputBuilderTests` still pass unmodified).
- [ ] Unity Console clean.

## Files this task touches
- `Assets/Scripts/Physics/Stats/ShotInputBuilder.cs` — new `fadeDrawInput`/`fadeDrawMaxTiltRad` params; tilt formula (Phase B/C).
- `Assets/Scripts/Gameplay/Input/ShotController.cs` — read `ShotModeContext.Mode` + `ConeFinetuneX` at `CommitFlick`; aim nudge (D); mode-transition aim-lock + re-center state (E); pass new params to `Build`.
- `Assets/Scripts/Gameplay/UI/ShotUI/ShotConeView.cs` — re-center `_clubHandle` on arm; reflect disarm (E). UI-reflect only.
- `Assets/Scripts/Gameplay/Config/ControlsConfig.cs` + `Assets/Resources/Gameplay/controls.csv` — `FadeDrawMaxTiltRad`, `AimNudgeRangeRad`, trim `SpinMaxTiltRad`.
- New/updated EditMode tests under `Assets/Scripts/Physics/Tests/` (+ any gameplay input test).

## Behavioral gate (replaces visual-fidelity gate; Lesson O analogue)
This task has no new visual surface, but it changes ball behavior, so dispatch logs alone are insufficient:
- **Human-in-the-loop play-and-confirm** over a REAL loaded hole (never LabScaffold): arm fade/draw, deflect the handle each way, confirm draw vs fade; toggle off, confirm aim-shift-no-curve. Record what was seen in `IMPLEMENTER_REPORT.md`.
- **Trajectory trace** (lab `TrajectoryRenderer` or sim sampling) showing lateral deflection sign+magnitude for: fadeDraw left, fadeDraw right, spin.x trim, straight. This trace is ALSO the calibration data Order 355 consumes for the aim-line bend — capture and keep it.
- **EditMode determinism + formula tests** as above.

## Out of scope (do NOT do these)
- The aim-LINE bend visualization — Order 355, after this.
- Any change to the spin disc UI, `SpinContext`, or backspin/topspin (`spin.y`) behavior.
- Cone width / accuracy mapping — already wired, leave it (D2).
- Putt behavior (D6).
