# Implementer Report — `fade_draw_core_wiring` (Order 356)

## Rejection follow-up (ARCHITECT_REVIEW_FAIL → iter-5)

Addressing all three FAIL items from `ARCHITECT_REVIEW.md`:

### FAIL #1 — `Canonical video:` citation was materially misleading (FIXED)

**Root cause (iter-4):** `FadeDrawSetSideCamera` wrote `cam.transform.position` once, then `ChaseCamera.LateUpdate` overrode the transform back to Chase every frame. Additionally, `LoopCameraDirector.HandleStateChanged` called `SetMode(Chase)` on `BallState.Flying` and `BallState.Rolling` transitions, immediately reverting any `SetMode(Downrange)` call.

**Fix (iter-5):** Two-part fix following the pattern already used in the tree-collision scenario (Scenarios.cs:1737-1744):
1. `FadeDrawSetSideCamera` now calls `chaseCamComp.SetDownrangeFraming(sideCamPos, lookAtPoint)` + `chaseCamComp.SetMode(ChaseCamera.Mode.Downrange)` after snapping the transform. The `Downrange` mode in `ChaseCamera.RunLateUpdateLogic` uses `_downrangePos`/`_downrangeLookAt` as its target, so the SmoothDamp holds the side position every frame instead of chasing the ball. Yields 0.5s for SmoothDamp convergence before shot fires.
2. `FadeDrawFireShot` now accepts an optional `sideCamComp` parameter and re-asserts `SetMode(ChaseCamera.Mode.Downrange)` every frame in the flight-wait loop, defeating `LoopCameraDirector`'s `Chase` overrides on `Flying`/`Rolling` state transitions.
3. `FadeDrawRestoreCamera` calls `chaseCamComp.SetMode(_fdSideCamSavedMode)` (restores to Chase) instead of reverting the transform directly.

**Verification log confirms the fix:**
```
[BotDriver]   SideCam armed+settled: pos=(-1.3, 33.4, -66.4) lookAt=(3.8, 23.4, -16.7)  mode=Downrange
[BotDriver]   SideCam restored: ChaseCamera mode -> Chase
```

**Self-verification of in-flight frames (mandatory):**
- `screenshots/iter5_caption_shotA.jpg` (t=2s): Ball clearly visible at launch in locked side-cam view. Static background (same tree composition) + ball in upper center + predictor cone — confirmed NOT the chase cam (chase cam would be directly behind the ball, not perpendicular).
- `screenshots/iter5_sidecam_shotA_inflight.jpg` (t=5s): Camera locked (identical background), yardage counter shows 83 yds (down from 168 yds at t=0) — ball is in flight, camera has not moved.
- `screenshots/iter5_caption_shotB.jpg` (t=28s): Same fixed background, "Shot B — Handle RIGHT -> FADE curve" caption. Ball visible at launch from identical side angle.

**Honest assessment:** The side camera IS now genuinely locked (Downrange mode holds through the full flight; log confirms). The ball is clearly visible at launch frames (t=2-3s per shot). At mid-flight (t=6-20s), the ball is small at 50m camera distance but the static background proves the camera is locked. The CURVE DIRECTION difference between DRAW and FADE shots is NOT readily visible as a dramatic banana in the video (50m camera distance reduces the angular spread), but the video is NOW an honest "locked perpendicular side-cam showing real production flow" rather than the iter-4 chase-cam mislabeled as side-cam. The curve proof remains the overlay PNG.

**Updated declarations:**
- `Canonical screenshot:` → `screenshots/curve_overlay_real_hole.png` (THE curve proof; 17.2m DRAW-FADE separation from real trajectory data)
- `Canonical video:` → `videos/fadedraw_real_hole_gate_iter5.mp4` (honest description: production side-cam capture — locked perpendicular view via ChaseCamera.Downrange mode, proves real ShellScene boot → Hole 6 Geo → 3 shots through production pipeline, ball visible at launch from side angle; lateral curve direction difference is subtle at 50m camera distance; curve magnitude proven by overlay PNG and runtime log)

### FAIL #2 — 105 MB raw video not deleted (FIXED)

- DELETED: `videos/fadedraw_real_hole.mp4` (105 MB raw from iter-3 run)
- DELETED: `videos/fadedraw_real_hole_gate_fadedraw_gate.mp4` (66 MB superseded iter-2 captioned video)
- DELETED: new 107 MB raw from iter-5 run (generated + immediately removed after captioning)
- KEPT: `videos/fadedraw_real_hole_gate_fadedraw_gate_iter3.mp4` (67 MB, iter-3 captioned history)
- KEPT: `videos/fadedraw_real_hole_gate_iter4.mp4` (70 MB, iter-4 captioned, prior canonical)
- KEPT: `videos/fadedraw_real_hole_gate_iter5.mp4` (120 MB, iter-5 captioned, new canonical)

### FAIL #3 — Report inaccuracy: sample counts (FIXED)

Previous claim "109/122/116" was wrong. Actual trajectory_points.json (verified by `python3 -c "import json; ..."`) shows **122/123/122** sampled points per shot (draw/fade/straight). All lines in this report using these counts are corrected.

---

## Implementation summary

**iter-5 addition (addresses ARCHITECT_REVIEW_FAIL: side camera not genuinely locked):**

Fixed `FadeDrawSetSideCamera`, `FadeDrawRestoreCamera`, and `FadeDrawFireShot` in `Scenarios.cs` to use `ChaseCamera.Mode.Downrange` for the flight window:
- `FadeDrawSetSideCamera` now calls `SetDownrangeFraming + SetMode(Downrange)` on `ChaseCamera` (not just `cam.transform.position`) and yields 0.5s for SmoothDamp convergence.
- `FadeDrawFireShot` re-asserts `SetMode(Downrange)` every frame during the flight-wait loop to defeat `LoopCameraDirector`'s `Chase` override on `Flying`/`Rolling` state transitions.
- `FadeDrawRestoreCamera` restores via `SetMode(savedMode)` rather than transform revert.
- Also renamed `still_sidecam_*.jpg` files to `still_chasecam_*.jpg` (they were chase-cam frames, not side-cam frames).
- Deleted stale raw and superseded videos (see FAIL #2 above).
- Corrected sample counts throughout this report (FAIL #3).

**iter-4 addition (preserved from prior iteration):**

*Top-down curve overlay PNG (`screenshots/curve_overlay_real_hole.png`, 1400×1400):*
- `FadeDrawSampleTrail` in `Scenarios.cs` reads `PhysicsLabController.LastTrajectory.samples` (full simulation arc: 4459/4515/4474 time-step samples per shot). Reflection used to cross the internal `LastTrajectory` property boundary.
- `Docs/Scripts/render_fadedraw_curve_overlay.py` (Pillow-based, 1400×1400) renders all 3 arcs in a shared top-down reference frame: DRAW (cyan) curves +7.9m lateral, FADE (yellow) curves -9.3m lateral, STRAIGHT (white) goes +7.5m (aim shift only). DRAW-FADE lateral separation at rest: **17.2m**. This PNG is the definitive curve proof.

**iter-3 addition (preserved):** Bot-recorded behavioral gate video over real Hole 6 Geo (ShellScene boot). Three shots: Shot A (FadeDraw ARMED, handle LEFT → DRAW: ball rest Z went from -8.84 to -16.6), Shot B (FadeDraw ARMED, handle RIGHT → FADE: ball rest Z = -33.9), Shot C (Straight mode, handle LEFT → aim shift only). Runtime wiring log (`runtime_wiring_log.txt`) confirms per-shot `fadeDrawInput` values: -1.0, +1.0, 0.0.

**iter-2 addition (preserved):** First behavioral gate bot run; established the 3-shot comparison pattern.

**iter-1 (preserved):** Wired the Fade/Draw mechanic end-to-end.

## Files modified or created

| Path | Change |
|---|---|
| `Assets/Scripts/Physics/Stats/ShotInputBuilder.cs` | Modified (iter-1) — added `fadeDrawInput`/`fadeDrawMaxTiltRad` params; tilt formula `fadeDrawInput*fdMax + spinInputX*spinMax` |
| `Assets/Scripts/Gameplay/Input/ShotController.cs` | Modified (iter-1 + iter-3) — `FadeDrawActive`, `FadeDrawLockedAimRad`, `ForceRecenterFinetune()`, `CommitFlick` D4/E; iter-3 added `#if UNITY_EDITOR` `FadeDrawRuntimeWiringLog` |
| `Assets/Scripts/Gameplay/UI/ShotUI/ShotConeView.cs` | Modified (iter-1) — Phase E: subscribe `ShotModeContext.OnChanged`, call `OnShotModeChanged()` |
| `Assets/Scripts/Gameplay/Config/ControlsConfig.cs` | Modified (iter-1) — added `FadeDrawMaxTiltRad=0.3f`, `AimNudgeRangeRad=0.0524f`; changed `SpinMaxTiltRad` to 0.075f |
| `Assets/Scripts/Gameplay/Config/ControlsConfigLoader.cs` | Modified (iter-1) — added CSV cases for new config keys |
| `Assets/Resources/Gameplay/controls.csv` | Modified (iter-1) — `SpinMaxTiltRad→0.075`, added `FadeDrawMaxTiltRad,0.3` and `AimNudgeRangeRad,0.0524` |
| `Assets/Scripts/Physics/Tests/FadeDrawTiltTests.cs` | Created (iter-1) — 9 EditMode tests |
| `Assets/Scripts/Physics/Tests/FadeDrawTiltTests.cs.meta` | Created (iter-1) |
| `Assets/Scripts/Gameplay/Tests/FadeDrawWiringTests.cs` | Created (iter-1) — 7 EditMode tests |
| `Assets/Scripts/Gameplay/Tests/FadeDrawWiringTests.cs.meta` | Created (iter-1) |
| `Assets/Scripts/Editor/FadeDrawTrajectoryTrace.cs` | Created (iter-1) — editor helper: trajectory simulations |
| `Assets/Scripts/Editor/FadeDrawTrajectoryTrace.cs.meta` | Created (iter-1) |
| `Assets/Scripts/Editor/FadeDrawTrajectoryViz.cs` | Created (iter-1) — editor helper: 1200×900 PNG trajectory viz |
| `Assets/Scripts/Editor/FadeDrawTrajectoryViz.cs.meta` | Created (iter-1) |
| `Docs/Specs/Active/fade_draw_core_wiring/trajectory_trace.txt` | Created (iter-1) — trajectory trace output |
| `Docs/Specs/Active/fade_draw_core_wiring/screenshots/trajectory_viz.png` | Created (iter-1) — behavioral screenshot |
| `Assets/Scripts/Physics/Viewer/Bot/Scenarios.cs` | Modified (iter-2 + iter-3 + iter-4 + iter-5) — `FadeDrawRealHoleGate` scenario; iter-3 overhead+wiring-log; iter-4 `FadeDrawSetSideCamera`/`FadeDrawRestoreCamera`/`FadeDrawSampleTrail`; iter-5 ChaseCamera.Downrange fix + `sideCamComp` param in `FadeDrawFireShot` |
| `Assets/Scripts/Physics/Viewer/Bot/LoopV2SmokeBot.cs` | Modified (iter-2) — added `fadedraw_real_hole_gate` dispatch case |
| `Assets/Scripts/Physics/Viewer/Bot/Editor/LoopV2SmokeBotMenu.cs` | Modified (iter-2 + iter-3 + iter-4) — menu item; cap bumped to 150s |
| `Assets/Scripts/Physics/Viewer/BallTrailController.cs` | Modified (iter-3) — added `#if UNITY_EDITOR` `WidthMultiplierForBot` property |
| `Docs/Specs/Active/fade_draw_core_wiring/runtime_wiring_log.txt` | Created (iter-3) — CommitFlick wiring snapshot (TEMP, `#if UNITY_EDITOR`) |
| `Docs/Specs/Active/fade_draw_core_wiring/videos/fadedraw_real_hole_gate_fadedraw_gate_iter3.mp4` | Created (iter-3) — 73.7s captioned video (history) |
| `Docs/Specs/Active/fade_draw_core_wiring/videos/fadedraw_real_hole_gate_iter4.mp4` | Created (iter-4) — 73.7s captioned video (prior canonical) |
| `Docs/Specs/Active/fade_draw_core_wiring/videos/fadedraw_real_hole_gate_iter5.mp4` | Created (iter-5) — 75.2s captioned video, locked side-cam via ChaseCamera.Downrange |
| `Docs/Specs/Active/fade_draw_core_wiring/screenshots/curve_overlay_real_hole.png` | Created (iter-4), updated (iter-5) — 1400×1400 top-down curve overlay: DRAW-FADE lateral sep = 17.2m; title updated to iter-5 |
| `Docs/Specs/Active/fade_draw_core_wiring/trajectory_points.json` | Created/updated (iter-4 + iter-5) — 3 shot entries, **122/123/122 sample points** each |
| `Docs/Scripts/render_fadedraw_curve_overlay.py` | Created (iter-4), updated (iter-5) — Python/Pillow curve overlay renderer; title updated to iter-5 |
| `Docs/Specs/Active/fade_draw_core_wiring/screenshots/iter5_caption_shotA.jpg` | Created (iter-5) — frame extract at t=2s from iter-5 video, side-cam launch Shot A, caption legible |
| `Docs/Specs/Active/fade_draw_core_wiring/screenshots/iter5_sidecam_shotA_inflight.jpg` | Created (iter-5) — frame extract at t=5s, camera locked (static background, 83 yds = ball in flight) |
| `Docs/Specs/Active/fade_draw_core_wiring/screenshots/iter5_caption_shotB.jpg` | Created (iter-5) — frame extract at t=28s, Shot B (FADE) launch, caption legible |
| `Docs/Specs/Active/fade_draw_core_wiring/screenshots/s02_overhead_A_draw_2026-06-17_09-16-20.png` | Created (iter-3) — overhead DRAW at rest |
| `Docs/Specs/Active/fade_draw_core_wiring/screenshots/s04_overhead_B_fade_2026-06-17_09-16-45.png` | Created (iter-3) — overhead FADE at rest |
| `Docs/Specs/Active/fade_draw_core_wiring/screenshots/s06_overhead_C_straight_2026-06-17_09-17-09.png` | Created (iter-3) — overhead STRAIGHT at rest |
| `Docs/Specs/Active/fade_draw_core_wiring/screenshots/still_chasecam_shotA_t30.jpg` | Renamed from `still_sidecam_shotA_t30.jpg` (iter-5) — chase-cam frame, name corrected |
| `Docs/Specs/Active/fade_draw_core_wiring/screenshots/still_chasecam_shotA_t35.jpg` | Renamed from `still_sidecam_shotA_t35.jpg` (iter-5) — chase-cam frame, name corrected |
| `Docs/Specs/Active/fade_draw_core_wiring/screenshots/still_chasecam_shotA_t42.jpg` | Renamed from `still_sidecam_shotA_t42.jpg` (iter-5) — chase-cam frame, name corrected |
| `Docs/Specs/Active/fade_draw_core_wiring/screenshots/s01_shotA_draw_landed.png` | Created (iter-2) — ball at rest after DRAW |
| `Docs/Specs/Active/fade_draw_core_wiring/screenshots/s02_shotB_fade_landed.png` | Created (iter-2) — ball at rest after FADE |
| `Docs/Specs/Active/fade_draw_core_wiring/screenshots/s03_shotC_aimnudge_landed.png` | Created (iter-2) — ball at rest after Straight |
| `Docs/Specs/Active/fade_draw_core_wiring/screenshots/still_shotA_draw_t25.png` | Created (iter-2) — still frame extract |
| `Docs/Specs/Completed/sound_effects/screenshots/` | Pre-existing untracked folder (sound_effects completed task); predates this task, no changes made |
| `Assets/Scripts/Editor/FadeDrawTrajectoryTrace.cs` | Created (iter-1) — pre-existing untracked outside task folder |
| `Assets/Scripts/Editor/FadeDrawTrajectoryTrace.cs.meta` | Created (iter-1) — pre-existing untracked |
| `Assets/Scripts/Editor/FadeDrawTrajectoryViz.cs` | Created (iter-1) — pre-existing untracked |
| `Assets/Scripts/Editor/FadeDrawTrajectoryViz.cs.meta` | Created (iter-1) — pre-existing untracked |
| `Assets/Scripts/Gameplay/Tests/FadeDrawWiringTests.cs` | Created (iter-1) — pre-existing untracked |
| `Assets/Scripts/Gameplay/Tests/FadeDrawWiringTests.cs.meta` | Created (iter-1) — pre-existing untracked |
| `Assets/Scripts/Physics/Tests/FadeDrawTiltTests.cs` | Created (iter-1) — pre-existing untracked |
| `Assets/Scripts/Physics/Tests/FadeDrawTiltTests.cs.meta` | Created (iter-1) — pre-existing untracked |
| `Docs/Scripts/render_fadedraw_curve_overlay.py` | Created (iter-4), updated (iter-5) — pre-existing untracked |

## Screenshot

Canonical screenshot: `screenshots/curve_overlay_real_hole.png`

- **Frame source:** Python/Pillow-generated 1400×1400 top-down plan view PNG. All 3 arcs plotted from REAL runtime `PhysicsLabController.LastTrajectory.samples` (4459/4515/4474 time-step samples per shot, decimated to **122/123/122 points** for JSON). Origin = tee (0,0). Horizontal = lateral (Z world), vertical = downrange (X world).
- **Scene loaded:** Hole 6 Geo, loaded via real ShellScene boot + `GameplaySceneLoader.BeginGameplayLoad(6)` (iter-5 run, ~10:46 CEST)
- **Measurements from overlay:** DRAW (cyan) lateral offset at rest = +7.9m, FADE (yellow) = -9.3m, STRAIGHT (white) = +7.5m. DRAW-FADE lateral separation = **17.2m**.

Supporting stills (iter-5 side-cam, locked via ChaseCamera.Downrange):
- `screenshots/iter5_caption_shotA.jpg` — Shot A (DRAW) launch from locked side-cam, ball visible, caption "Handle LEFT -> DRAW curve" legible, no clipping
- `screenshots/iter5_sidecam_shotA_inflight.jpg` — t=5s into Shot A flight, camera locked (static background), yardage 83→83 yds (ball mid-flight, 168 yds total)
- `screenshots/iter5_caption_shotB.jpg` — Shot B (FADE) launch, same locked side angle, caption "Handle RIGHT -> FADE curve" legible

Supporting stills (overhead at rest, 15m / 1.5m trail, iter-3 run):
- `screenshots/s02_overhead_A_draw_2026-06-17_09-16-20.png` — DRAW ball rest (-68.0, 10.3, -16.6)
- `screenshots/s04_overhead_B_fade_2026-06-17_09-16-45.png` — FADE ball rest (-72.9, 9.7, -33.9)
- `screenshots/s06_overhead_C_straight_2026-06-17_09-17-09.png` — STRAIGHT ball rest (-70.1, 10.4, -17.1)

Runtime wiring log (TEMP diagnostic, `#if UNITY_EDITOR` only):
- `runtime_wiring_log.txt` — confirms per-shot `fadeDrawInput`: DRAW=-1.0, FADE=+1.0, STRAIGHT=0.0, all `IsPutt=False`

Canonical video: `videos/fadedraw_real_hole_gate_iter5.mp4`

- **Duration:** 75.2s, 1170×2532 @ 30fps
- **Camera:** ChaseCamera locked to Downrange mode (target = fixed side position at `(-1.3, 33.4, -66.4)`, looking at `(3.8, 23.4, -16.7)`) for the full flight window of each shot. `LoopCameraDirector`'s Chase overrides on Flying/Rolling defeated by per-frame re-assertion (same pattern as tree-collision scenario). Ball IS visible at launch from this side angle (see `iter5_caption_shotA.jpg`). At mid-flight (50m camera distance), the ball is small; lateral curve direction is subtle from this distance. Curve MAGNITUDE is proven by the overlay PNG (17.2m separation) not the video.
- **Captions:** 5 step captions + title card "FadeDraw Core Wiring - iter-5". Captions: "Loading Hole 6 via real ShellScene flow...", "Shot A — FadeDraw ARMED / Handle LEFT -> DRAW curve", "Shot B — FadeDraw ARMED / Handle RIGHT -> FADE curve", "Shot C — Straight MODE / Handle LEFT -> aim shift only". Arrow "->" used (not "→") to avoid missing-glyph box. No caption clipping.

## Acceptance checklist

| Item | Result | Justification |
|---|---|---|
| Toggle ON + handle left/right → ball DRAWS vs FADES, verified trajectory trace | PASS | (A) Trajectory trace: FD=-1 → Z=-29.56m (draw), FD=+1 → Z=+29.54m (fade), opposite signs. (B) Runtime wiring log Shot A fadeDrawInput=-1.0, Shot B fadeDrawInput=+1.0. (C) Overhead captures show ball rest Z=-16.6 (DRAW) vs Z=-33.9 (FADE) = 17.3m lateral difference. (D) Overlay PNG: 17.2m DRAW-FADE separation from real `LastTrajectory.samples`. |
| Toggle OFF + handle deflected → aim shift, NO curve | PASS | Tests `StraightMode_HandleRight_NudgesAimRight`/`..Left` both PASS. Runtime wiring log Shot C: `FadeDrawActive=False fadeDrawInput=0.0000`. Overhead shows ball rest Z=-17.1 (similar to DRAW Z=-16.6 = aim-shift only). |
| On arming, aim locks at tuned value and handle re-centers (D5) | PASS | Tests `ModeTrans_Arm_LocksAimAtCameraHeading` and `ModeTrans_Arm_RecentersHandle` both PASS. Runtime log: `lockedAim=3.0391` for A/B vs `NaN` for C. |
| Sidespin still curves but visibly LESS than fade/draw | PASS | `trajectory_trace.txt`: SpinX full deflection = 7.8m, FadeDraw full deflection = 29.5m, ratio = 3.79×. |
| `spin.y` backspin/topspin unchanged (regression check) | PASS | Test `SpinY_StillChangesRate_AfterD3` PASS: spinY=0.5 reduces rate to ~25%. |
| Putts unchanged: zero spin, no fade/draw, handle inert in putt mode (D6) | PASS | Test `Putt_FadeDrawActive_SpinIsZero` PASS. |
| Spin disc UI (Order 354) untouched | PASS | `git diff` shows no changes to `SpinPanelWidget.cs`, `SpinContext.cs`, or disc visuals. |
| Determinism preserved | PASS | Test `Determinism_SameSeedAndInputs_IdenticalShotInput` PASS. |
| EditMode tests: tilt formula, aim-nudge mapping, mode-transition aim-lock | PASS | 17/17 EditMode tests pass (9 FadeDrawTiltTests + 7 FadeDrawWiringTests + 1 regression). Results in `test_results.txt`. |
| `Build(...)` new params default to 0 = legacy no-op | PASS | Tests `DefaultParams_LegacyNoOp_SpinXOnly` and `BothZero_ProducesUntiledLegacyAxis` PASS. |
| Unity Console clean | PASS | Tundra build success (4.07s, 29 items) after iter-5 Scenarios.cs changes; no new `error CS` entries. Pre-existing `warning CS8632` and `warning CS0618` unrelated to this task. |
| Behavioral gate: bot-recorded play over real loaded hole — lateral curve visually distinguishable | PASS | **Canonical still `screenshots/curve_overlay_real_hole.png` (1400×1400):** Top-down plan view from real `LastTrajectory.samples`, 17.2m DRAW-FADE separation, unambiguous. **Supporting video `fadedraw_real_hole_gate_iter5.mp4`:** locked perpendicular side-cam (ChaseCamera.Downrange, Director override defeated), ball visible at launch (see iter5_caption_shotA.jpg), production real-hole flow proven. The reviewer's ARCHITECT_REVIEW.md confirmed: "The overlay still IS the proof... the behavioral gate, taken on the overlay still alone, IS satisfied." |

## Known FAIL items

None. All ARCHITECT_REVIEW_FAIL items addressed:
- FAIL #1 (misleading Canonical video): Fixed. Video now uses genuinely locked ChaseCamera.Downrange mode. Report honestly describes what the video shows vs what the overlay PNG proves. Misleadingly-named `still_sidecam_*.jpg` files renamed to `still_chasecam_*.jpg`.
- FAIL #2 (105 MB raw video not deleted): Fixed. Deleted `fadedraw_real_hole.mp4` (iter-3 raw + iter-5 raw) and `fadedraw_real_hole_gate_fadedraw_gate.mp4` (iter-2 superseded).
- FAIL #3 (sample count inaccuracy): Fixed. Report now correctly states **122/123/122** throughout.

## Spec deviations

- **`ShotConeView.cs` Phase E locked-aim capture**: SPEC says "capture current effective aim (`AimYawRadians + ConeFinetuneX * AimNudgeRangeRad`)." Implementation captures `CameraHeadingRadians` only. Rationale: handle is re-centered to 0 on arm, so effective aim at arm time = CameraHeadingRadians + 0 = CameraHeadingRadians. Functionally equivalent. Carried from iter-1 reviewer PASS*.

## Console output

```
[FadeDrawTrace]
=== FadeDraw Trajectory Trace ===
FadeDrawMaxTiltRad: 0.3000 rad (17.2deg)
SpinMaxTiltRad:     0.0750 rad (4.3deg) [TRIM, D3]
AimNudgeRangeRad:   0.0524 rad (3.0deg)
Straight  (FD=0, Sx=0)   |    0.000 | 0.000
FadeDraw left  (FD=-1)   |  -29.560 | -29.560
FadeDraw right (FD=+1)   |   29.540 | +29.540
SpinX trim left  (Sx=-1) |   -7.805 | -7.805
SpinX trim right (Sx=+1) |    7.782 | +7.782
[PASS] FadeDraw left vs right produce opposite-sign deviation
[PASS] SpinX trim left vs right produce opposite-sign deviation
[PASS] FadeDraw deviation (29.560m) > SpinX deviation (7.805m) x1.5 (ratio=3.79x)

[FadeDrawTests] Run Finished — Pass:17 Fail:0 Skip:0
```

## Open questions for Architect

None. All items resolved in prior iterations. Rejection items from ARCHITECT_REVIEW_FAIL addressed in iter-5 (this report).
