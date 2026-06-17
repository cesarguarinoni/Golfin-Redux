# Close-out — `fade_draw_core_wiring` (Order 356)

**DONE 2026-06-17.** Cesar-approved close-out.

## What shipped
The input→physics wiring for fade/draw (no new visual surface, per SPEC):
- `ShotInputBuilder.cs` — `fadeDrawInput`/`fadeDrawMaxTiltRad` params (default 0 = legacy no-op); single combined tilt `fadeDrawInput*fdMax + spinInputX*spinMax`, single `fpMath.Rotate`.
- `ShotController.cs` — Phase B/D/E: read `FadeDrawActive`+`FadeDrawLockedAimRad` at CommitFlick, aim-nudge in Straight mode, arm aim-lock + `ForceRecenterFinetune`.
- `ShotConeView.cs` — Phase E mode-transition reflect (re-center club handle on arm).
- `ControlsConfig.cs` / `ControlsConfigLoader.cs` / `controls.csv` — `FadeDrawMaxTiltRad=0.3`, `AimNudgeRangeRad=0.0524`, `SpinMaxTiltRad` demoted to `0.075` (D3 trim, ¼).
- Tests: `FadeDrawTiltTests.cs` (9) + `FadeDrawWiringTests.cs` (8) = **17/17 EditMode pass** (formula signs, aim-nudge, mode-transition aim-lock, determinism, putt D6, spin.y regression, legacy no-op). Re-verified after scaffolding strip.

## Verification of record
- 17/17 EditMode tests (`test_results.txt`).
- `runtime_wiring_log.txt` — production `CommitFlick` pushed `fadeDrawInput` = −1.0 (draw) / +1.0 (fade) / 0.0 (straight, +3° aim) over real Hole 6.
- `trajectory_points.json` + `screenshots/curve_overlay_real_hole.png` — real-runtime top-down overlay, 17.2m DRAW–FADE separation. **Kept as calibration input for Order 355** (`fade_draw_aim_line_bend`).

## Behavioral video gate — WAIVED by Cesar
The SPEC's play-and-confirm video was dropped. Cesar will human-confirm the feel once Order 355 integrates the bent aim-LINE UI ("easier to read that way"). The bot-video iterations (2–5) chased a non-playing-conditions capture (Downrange side-cam in the trees, wrong UI, y-flip) — root-caused in `CESAR_REJECTION.md`; lesson saved to memory `feedback_lateral_curve_capture`.

## Scaffolding stripped (Cesar's call)
Removed all video-capture scaffolding, restoring these to HEAD / deleting:
- `Scenarios.cs`, `LoopV2SmokeBot.cs`, `LoopV2SmokeBotMenu.cs`, `BallTrailController.cs` → reverted to HEAD (FadeDraw bot scenario + side-cam + `WidthMultiplierForBot` removed).
- `ShotController.cs` → removed the `#if UNITY_EDITOR FadeDrawRuntimeWiringLog` diagnostic (production wiring retained).
- Deleted editor-sim viz scripts (`FadeDrawTrajectoryTrace/Viz.cs`), the ~260MB of iter-3/4/5 videos, and all failed-attempt screenshots (kept only `curve_overlay_real_hole.png`).

Compile clean; 17/17 tests green post-strip.
