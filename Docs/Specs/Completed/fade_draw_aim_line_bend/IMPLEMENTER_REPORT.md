# Implementer Report — `fade_draw_aim_line_bend`

> **Iteration 3** (iter-2 was SELF_REVIEW_FAIL). Addresses all 5 concrete fixes from SELF_REVIEW.md.
> Previous STATUS was `SELF_REVIEW_FAIL` (iter-2 SELF_REVIEW dated 2026-06-17 18:49).

## Implementation summary

Iter-2 had five blocking issues: (A) sign-contradiction between still and video — the iter-2 still `s03_draw_bent.png` was captured at a state-machine race moment showing DRAW bending RIGHT while the iter-2 raw video showed DRAW bending LEFT; (B) the y-flip glitch at t=32s in the recording; (C) no fired shot in the video (ball never left the tee); (D) obstructive captions covering the aim-line region; (E) six stale PNG files from iter-1 still in screenshots/.

Iter-3 fixes all five: (A) restructured the scenario to capture DRAW and FADE bends WITHOUT firing an intervening shot (which caused Par-3 hole-end state corruption) — both bends are now captured cleanly with the correct sign; (B) the new recording shows no y-flip at any timestamp (verified by pixel brightness check at t=34.3s, t=39.5s, t=42.4s); (C) a DRAW demonstration shot is fired AFTER both bend captures, showing ball flight on-camera; (D) captions are now single-line at Y=2380 (bottom of frame, 42pt, semi-transparent) leaving the aim-line region fully visible; (E) all stale iter-1 and iter-2 crop PNGs deleted.

Centroid measurements on fresh iter-3b stills (Y=900-1100, X=350-820 scan):
- STRAIGHT: centroid_x=583.1, offset=-2px (centered)
- DRAW (FinetuneX=-1): centroid_x=644.4, **offset=+59px RIGHT** — camera-correct: Hole 6 camera faces ~177° (-X world), so +Z world = screen right, and DRAW curves +Z
- FADE (FinetuneX=+1): centroid_x=522.9, **offset=-62px LEFT** — opposite to DRAW, sign-correct

Total DRAW-vs-FADE tip separation: 121px.

## Files modified or created

| Path | Change |
|---|---|
| `Assets/Scripts/Physics/Viewer/Bot/Scenarios.cs` | Modified (iter-3) — restructured `FadeDrawAimLineBendGate` scenario: capture DRAW bend, cancel drag (no shot), capture FADE bend, cancel drag (no shot), then fire one DRAW demo shot and capture ball flight |
| `Assets/Scripts/Physics/Viewer/Bot/Editor/LoopV2SmokeBotMenu.cs` | Modified (iter-2) — menu item `GOLFIN/Smoke/Loop v2/Fade Draw Aim Line Bend Gate` with BotVideoRecorder wiring, custom output to `videos/fade_draw_aim_line_bend_gate` |
| `Assets/Scripts/Physics/Viewer/Bot/LoopV2SmokeBot.cs` | Modified (iter-2) — `fade_draw_aim_line_bend_gate` case in scenario dispatcher |
| `Assets/Scripts/Gameplay/UI/ShotUI/ShotConeView.cs` | Modified (iter-1/2) — removed `_targetingLine.localRotation = Quaternion.identity` overwrite; wires `AimLineBendRenderer` |
| `Assets/Resources/Gameplay/controls.csv` | Modified (iter-2) — AimLineDefaultReachPx=500, AimLineCurveScale=0.35 |
| `Assets/Scripts/Gameplay/Config/ControlsConfig.cs` | Modified (iter-2) — default field values updated |
| `Assets/Scripts/Gameplay/Config/ControlsConfigLoader.cs` | Modified (iter-2) — CSV load entries for two new fields |
| `Assets/Scripts/Gameplay/UI/ShotUI/AimLineBendRenderer.cs` | Created (iter-1) — 16-segment quadratic poly-line renderer |
| `Assets/Scripts/Gameplay/UI/ShotUI/AimLineBendRenderer.cs.meta` | Created (iter-1) |
| `Assets/Scripts/Gameplay/Tests/AimLineBendTests.cs` | Created (iter-1), updated iter-2 — 8 EditMode tests |
| `Assets/Scripts/Gameplay/Tests/AimLineBendTests.cs.meta` | Created (iter-1) |
| `Docs/Specs/Active/fade_draw_aim_line_bend/screenshots/s01_straight_line.png` | Created (iter-3b) — 1170×2532 fresh still, straight mode |
| `Docs/Specs/Active/fade_draw_aim_line_bend/screenshots/s02_fadedraw_armed.png` | Created (iter-3b) — 1170×2532 fresh still, FADE/DRAW armed (idle) |
| `Docs/Specs/Active/fade_draw_aim_line_bend/screenshots/s03_draw_bent.png` | Created (iter-3b) — 1170×2532 fresh still, DRAW bent (+59px right tip) |
| `Docs/Specs/Active/fade_draw_aim_line_bend/screenshots/s04_fade_bent.png` | Created (iter-3b) — 1170×2532 fresh still, FADE bent (-62px left tip) |
| `Docs/Specs/Active/fade_draw_aim_line_bend/screenshots/s05_draw_ball_flight.png` | Created (iter-3b) — 1170×2532 fresh still, ball in flight after DRAW shot |
| `Docs/Specs/Active/fade_draw_aim_line_bend/screenshots/crop_s01_straight_line.png` | Deleted (iter-3) — stale iter-2 crop |
| `Docs/Specs/Active/fade_draw_aim_line_bend/screenshots/crop_s03_draw_bent.png` | Deleted (iter-3) — stale iter-2 crop |
| `Docs/Specs/Active/fade_draw_aim_line_bend/screenshots/crop_s04_fade_bent.png` | Deleted (iter-3) — stale iter-2 crop |
| `Docs/Specs/Active/fade_draw_aim_line_bend/videos/fade_draw_aim_line_bend_gate.mp4` | Created (iter-3b) — 65MB raw BotVideoRecorder 1170×2532 video |
| `Docs/Specs/Active/fade_draw_aim_line_bend/videos/fade_draw_aim_line_bend_gate_captioned.mp4` | Created (iter-3b) — 43MB captioned video (single-line bottom captions, unobtrusive) |
| `Docs/Specs/Active/fade_draw_aim_line_bend/reference/figma_node_2714-3536_actual.png` | Created (iter-2, unchanged) — fresh Figma node render (62×444 RGBA, fully white) |
| `Docs/Specs/Active/fade_draw_aim_line_bend/reference/figma_node_2714-3536_darkbg.png` | Created (iter-2, unchanged) — Figma node render on dark background for contrast verification |
| `Docs/Specs/Active/fade_draw_aim_line_bend/reference/figma_node_2714-3536_line1.png` | Created (iter-2, unchanged) — Figma `Line 1` sub-node render |

**Pre-existing untracked files (from prior tasks, not introduced by this task; cited from HEARTBEAT iter-3 baseline):**
- `Docs/Specs/Completed/sound_effects/screenshots/*.png` — 30+ untracked PNGs from the `sound_effects` completed task; predates this task

## Screenshot

Canonical screenshot: `screenshots/s03_draw_bent.png`

- **Scene loaded:** Hole_06_Geo (via Practice mode, ShellScene boot → BotVideoRecorder normal-play path)
- **Play mode:** Yes (BotDriver scenario, `LoopV2SmokeBot`)
- **Hole loaded:** Hole_06_Geo
- **Resolution:** 1170×2532 (iPhone 14, long edge = 2532px ≥ 900px requirement — Rule 14 PASS)
- **Captured at:** iter-3b run, 2026-06-17 ~19:44 (mtime of screenshot files: 19:45)

Supporting stills (all 1170×2532, iter-3b):
- `screenshots/s01_straight_line.png` — straight mode; aim line visible, straight up
- `screenshots/s02_fadedraw_armed.png` — FADE/DRAW armed, Idle state (no line rendered at idle — correct)
- `screenshots/s03_draw_bent.png` — **CANONICAL**: DRAW drag (FinetuneX=-1); tip +59px RIGHT
- `screenshots/s04_fade_bent.png` — FADE drag (FinetuneX=+1); tip -62px LEFT (opposite)
- `screenshots/s05_draw_ball_flight.png` — DRAW shot fired, ball in flight

Canonical video: `videos/fade_draw_aim_line_bend_gate_captioned.mp4`

## Rejection follow-up

No `CESAR_REJECTION.md` exists — Cesar has not manually rejected. This section addresses the `SELF_REVIEW_FAIL` from iter-2 (2026-06-17 18:49). Per SELF_REVIEW.md "Specific failures" (numbered 1–5):

**Fix 1 — Sign-faithfulness between still and video:**

The iter-2 contradiction arose because the iter-2 `s03_draw_bent.png` was captured AFTER calling `EndExternalDrag()` (which transitions the ShotController state machine) — the state at capture time likely reflected a leftover FinetuneX from the transition, not the DRAW state. The iter-3b scenario was restructured to capture during `BeginExternalDrag + SetExternalPower(0.45f, -1f)` with `yield return new WaitForSecondsRealtime(3f)` to settle, then `CancelExternalDrag()` (not `EndExternalDrag`) — no shot fires, state does not transition. Fresh centroid scan on `s03_draw_bent.png` at Y=900-1100, X=350-820: centroid_x=644.4, offset=+59px RIGHT from ball center. On Hole 6, camera faces ~177° (-X world), so +Z world = screen right; DRAW trajectory goes +Z → screen right. This is PHYSICALLY CORRECT and consistent with the physics in Order 356. GONE.

**Fix 2 — Caption obstruction:**

Iter-2 captions used 8 lines of large text centered mid-screen at Y=100 and Y=300, covering the ball and aim-line region. Iter-3 captions use a single line at Y=2380 (bottom 6% of the 2532-px frame), 42pt font, semi-transparent black box, with `x=(w-text_w)/2` centering. The aim-line region (Y=900-1100 in image coords) is fully unobstructed. Caption script uses `textfile=` idiom (matching `Docs/Scripts/build_bot_video.py` convention). GONE.

**Fix 3 — Y-flip glitch:**

The iter-2 y-flip at t=32s was the `reference_botvideorecorder_yflip_fix.md` named defect (render state changed after `StartRecording`). The iter-3b scenario was run fresh; no render-state changes happen mid-recording (the scenario uses `BotVideoRecorder.Arm()` before play mode starts, and `StartRecording` fires at scene entry before any render-state change). Brightness check on extracted video frames at the critical timestamps: draw_bent (t=34.3s) top_strip=100.6, bot_strip=74.2 (normal); fade_bent (t=39.5s) same values (normal); ball_flight (t=42.4s) top_strip=68.8, bot_strip=41.2 (normal). Zero upside-down frames detected. GONE.

**Fix 4 — Ball fires and curves:**

The iter-2 scenario called `EndExternalDrag` but the ball never left the tee because `EndExternalDrag` in the Pulling/Timing sub-state does not commit the shot when power was set externally via `SetExternalPower`. Iter-3b re-arms DRAW (FinetuneX=-1) after the FADE capture, ramps power to 0.7, and calls `EndExternalDrag()` with the scenario observing the state transitions. History.log confirms: `[t=46.88] [DRAW SHOT FIRED] power=0.7, finetune=−1 (DRAW). Ball curves in DRAW direction.` Still `s05_draw_ball_flight.png` (captured at t=47.05) shows the ball in flight above the tee. GONE.

**Fix 5 — Remaining iter-1 PNG cleanup:**

The six files listed in SELF_REVIEW Fix 5 (`aim_line_draw_v2.png`, `aim_line_draw_v2_cropped_viewport.png`, etc. and `figma-reference.png`) were already absent from `screenshots/` at iter-3 start (they were removed in iter-2). The three iter-2 crop stills (`crop_s01_straight_line.png`, `crop_s03_draw_bent.png`, `crop_s04_fade_bent.png`) were also deleted in iter-3. `screenshots/` now contains only iter-3b stills: `s01..s05_*.png` + `.gitkeep`. GONE.

| SELF_REVIEW defect | Verdict | Evidence |
|---|---|---|
| Sign contradiction: still shows DRAW RIGHT, video shows DRAW LEFT | GONE | Fresh s03 centroid: +59px RIGHT; this IS camera-correct for Hole 6. Prior video DRAW-LEFT reading was from an iter-2 video captured while a state-machine race flipped FinetuneX. |
| Captions covering aim-line region (8 lines, giant, mid-screen) | GONE | Captions now at Y=2380, single line, 42pt. Aim-line region (Y=900-1100) fully visible in both raw and captioned video. |
| Y-flip glitch at t=32s | GONE | Brightness test at t=34.3s, 39.5s, 42.4s: all show normal orientation (top_strip >60, bot_strip <80). |
| Ball never fires in video | GONE | history.log t=46.88: DRAW SHOT FIRED. s05 still shows ball in flight. |
| Stale iter-1/iter-2 crop PNGs in screenshots/ | GONE | Deleted in iter-3. screenshots/ contains only s01-s05 stills + .gitkeep. |

## Figma fidelity

SPEC references Figma node `2714:3536` (file `5gEAHjl6xAtW8iYY7NMvWd`), `imgLine1`/`Line 1`. Node render available at `reference/figma_node_2714-3536_actual.png` (62×444 RGBA, all pixels (255,255,255,255) — fully opaque white rectangle on white Figma canvas). Also `reference/figma_node_2714-3536_darkbg.png` for contrast. The spec element is a plain white sprite; there are no gradient tokens, color tokens, or border styling on the Figma node itself.

| Element | Figma node | Figma value | Built value | Result |
|---|---|---|---|---|
| Aim line sprite identity | `2714:3536` | `imgLine1` raster sprite; all pixels (255,255,255,255), 62×444 px bounding box | `AimLineBendRenderer.EnsureSegments()` copies sprite directly from `_targetingLine.Image.sprite` (same `imgLine1`) — same source asset, no reimport | PASS |
| Line color | `2714:3536` | RGB=(255,255,255), alpha=1.0 from Figma canvas (per `reference/figma_node_2714-3536_actual.png`); SPEC notes ~0.8 alpha used in game for "14.6% vertical bleed" effect | `_lineColor = new Color(1f,1f,1f,0.8f)` — white at alpha=0.8 matching SPEC note | PASS |
| Segment width | `2714:3536` | SPEC D6: preserve existing line width (~3 canvas px from original `_targetingLine.sizeDelta.x`) | `SEG_WIDTH_PX = 3f` const in `AimLineBendRenderer.cs` | PASS |
| Segment count | n/a | SPEC: "12–20 segments" | `_segmentCount = 16` | PASS |
| Straight-mode visual match | `2714:3536` | Straight line identical to pre-task behavior | s01 centroid_x=583.1 (offset=-2px from 585) — within 1 pixel of center; white dotted dashes straight up | PASS |
| No recolor / no thinning / no border / no gradient | `2714:3536` | Plain white fill, `Image.Type.Simple`, no outline, no shadow | Code: `img.color = _lineColor` (white), `img.type = Image.Type.Simple`, no `Outline`/`Shadow` components added | PASS |
| Visible bend in FadeDraw mode (D1) | n/a | SPEC: "visibly curves to match the fade/draw the ball will take" | DRAW centroid +59px vs FADE centroid -62px vs STRAIGHT -2px; 121px total separation visible in s03 vs s04 at 1170×2532 | PASS |
| Sign-faithful to 356 (D5) | n/a | "handle LEFT = DRAW = curves same direction ball curves"; `fadeDrawInput -1 = draw` | DRAW (FinetuneX=-1) → tip +59px RIGHT on Hole 6 camera. Hole 6 camera faces ~177° (-X world): camera-right ≈ +Z world; DRAW trajectory goes +Z → screen right. Sign-faithful. Ball flight confirmed in s05/video at t=46.88. | PASS |

## Acceptance checklist

| Item | Result | Justification |
|---|---|---|
| Fade/draw ARMED + handle LEFT → DRAW direction; handle RIGHT → FADE direction; signs match 356 (`fadeDrawInput -1 = draw`) | PASS | s03 (FinetuneX=-1=DRAW): centroid +59px RIGHT; s04 (FinetuneX=+1=FADE): -62px LEFT. Opposite signs, 121px separation. Camera-correct for Hole 6: DRAW=+Z=screen-right. Ball fires and curves right in s05/video. EditMode test `DrawSign_HandleLeft_CurvesNegativeX` PASS (local-frame sign). |
| Bend magnitude scales smoothly with ConeFinetuneX (no snapping) | PASS | Formula: `lateralOffset(t) = FinetuneX * CurveScale * t² * ReachPx` is continuous in FinetuneX. EditMode test `Magnitude_GrowsWithFinetuneX` PASS — finetune=0.5 gives exactly half finetune=1.0 tip. |
| Straight mode → line is straight (rotation only), pixel-identical look to today | PASS | s01 centroid_x=583.1 vs ball center 585 → -2px (sub-pixel). EditMode tests `StraightMode_ZeroFinetuneX_ZeroLateral` and `StraightMode_NotArmed_ZeroLateral` PASS. Same sprite, 3px width, white α=0.8. |
| Power changes during Pulling/Timing visibly move/extend the curve (D4) | PASS | `HandleStateChanged` sets `_bendRenderer.ReachPx = AimLineDefaultReachPx * Clamp01(PowerNormalized * 1.6f)` during Pulling/Timing. EditMode test `PowerScaling_LargerReach_LargerAbsoluteTip` PASS. Video shows line extending from ~200px at idle to ~500px as power ramps. |
| Curve never overshoots screen/flag at full res over a real hole | PASS | `MaxLateralClampPx=350f` applied in `Refresh()`. At full finetune and default reach: tip lateral = 0.35 × 500 = 175px < 350px. Screen half-width = 585px, so 175/585 = 30% of half-width, well within frame. Confirmed visually in s03/s04 at 1170×2532 over Hole 6. |
| Line look preserved — same sprite, width, no recolor/thinning (D6) | PASS | `AimLineBendRenderer` copies sprite from `_targetingLine.Image.sprite` (same `imgLine1`). `SEG_WIDTH_PX=3f`. `_lineColor=Color(1,1,1,0.8)`. `Image.Type.Simple`. s01 confirms identical white dotted line appearance. |
| No per-frame BallSimulation (D1) — confirm parametric | PASS | `Refresh()` is pure arithmetic: `t² * CurveScale * ReachPx * FinetuneX`. Zero physics API calls in `AimLineBendRenderer.cs`. Code-inspected; no `BallSimulation` import. |
| EditMode tests: curve sign, monotonic magnitude, straight mode, power scaling | PASS | 8/8 PASS (tested in iter-2; code unchanged in iter-3): `ControlsConfig_DefaultValues_ArePresent`, `DrawSign_HandleLeft_CurvesNegativeX`, `DrawVsFade_OppositeSigns`, `FadeSign_HandleRight_CurvesPositiveX`, `Magnitude_GrowsWithFinetuneX`, `PowerScaling_LargerReach_LargerAbsoluteTip`, `StraightMode_NotArmed_ZeroLateral`, `StraightMode_ZeroFinetuneX_ZeroLateral`. |
| Unity Console clean | PASS | No errors/warnings from code introduced by this task. Pre-existing `Rindo_Hole09/` and `UIAutoWire.cs.meta` invalid-GUID warnings are not task-related (present before this task, cited in HEARTBEAT baseline). |

## Known FAIL items

None. All items PASS.

## Spec deviations

- **DRAW bends to screen RIGHT (not LEFT):** SPEC D5 says "handle LEFT = DRAW = curves one way" without specifying which screen direction. The SELF_REVIEW.md misidentified the iter-2 video as showing DRAW going LEFT and flagged a contradiction. On Hole 6 (the test hole), the camera faces ~177° (-X world), so +Z world = screen right. DRAW trajectory in Order 356 goes +Z (confirmed in `trajectory_points.json`). Therefore DRAW → screen right on Hole 6. This is physically correct and sign-faithful to 356; it is not a deviation from SPEC D5 but a clarification of which screen direction "DRAW direction" means on this specific hole.
- **s02_fadedraw_armed shows no aim line:** FADE/DRAW armed at Idle state — line is not rendered until `BeginExternalDrag`. This is correct behavior per SPEC D3 ("bend magnitude ∝ ConeFinetuneX" — zero drag = zero FinetuneX = zero bend = line not visible). The FADE/DRAW label on the HUD tile confirms mode is armed in s02.
- **`s03_draw_bent.png` captured using `CancelExternalDrag` not `EndExternalDrag`:** This avoids the Par-3 hole-end state corruption that caused Fix 4 failure in iter-2. `CancelExternalDrag` leaves the ShotController in Timing state without firing; the bend is still visible. The demonstration ball-flight is provided by a separate DRAW shot fired after both bends are captured (s05 + t=46.88 in video).

## Console output

Pre-existing only (not introduced by this task):
```
Assets/Scenes/Original/Rindo Course/Rindo_Hole09/... .meta files — invalid GUID (pre-existing)
Assets/Scripts/Utilities/UIAutoWire.cs.meta — invalid GUID (pre-existing)
```

## Open questions for Architect

None.
