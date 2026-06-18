# Self-Review — `fade_draw_aim_line_bend` (iter-3)

> Written by `golfin-self-reviewer` (iter-3). Independent pixel-scan-first protocol followed.
> Timestamp: 2026-06-17 20:35 CEST.

## Verdict

`PASS` → `FORWARD_TO_ARCHITECT` (`READY_FOR_ARCHITECT_REVIEW`).

The five iter-2 blocking defects are all genuinely resolved, AND the iter-3 evidence introduces the missing line-mirrors-ball test that iter-2 could not even attempt (no shot was fired). Internally consistent across stills + raw video + ball flight: DRAW (FinetuneX=−1) bends RIGHT on Hole 6, FADE (FinetuneX=+1) bends LEFT, and the fired DRAW ball curves RIGHT (the line mirrors the ball). This is sign-faithful to 356 by D5 (line direction matches ball curve direction); the abstract "DRAW = LEFT" caption in some code comments is the world-axis convention, while the rendered screen-direction reflects the world→camera rotation on Hole 6. The pixel evidence is the contract, and the pixels are internally consistent.

Iteration N = 3. PASS routes forward; if this had been a third FAIL, the rule is ESCALATE.

## Visual diff notes (Step 1 — independent pixel scan, no spec/report consulted)

### `s03_draw_bent.png` (canonical, 1170×2532)

> Portrait iPhone-14 frame. Top-left HUD: portrait of JAMES Lv 10 TURN 1 + wind chip "2.2 mph". Top-right HUD: "LOMOND / HOLE 6 — REGULAR / PAR 3" + small green hole-map. Center-mid: flag-pin sign "168 yds" between player and a far green; pin visible on the green over water. Center-low: a white Golfin ball (green G). Just ABOVE the ball, a stretch of faint white dashed line emerges and arcs from the ball upward AND TO THE RIGHT — the dashes' column drifts noticeably right of the ball's vertical axis (ball at image_x≈585; dashes' midline at image_x≈644, ~59 px right of center). Below the ball: large translucent green aim cone fanning to the bottom. A black/white driver clubhead overlays the cone. Bottom tiles: SPIN (top-left), GOLFIN (bottom-left), FADE/DRAW (top-right, mode armed), DRIVER 0 yds (bottom-right). Power ring shows 45%.

### `s01_straight_line.png` (straight reference)

> Same composition. ABOVE the ball, a clean STRAIGHT vertical white dashed line goes from just above ball center up to the flag — mean dash x ≈ 584, ball center x ≈ 585 → perfectly vertical (within 1 px). Bottom-right tile reads "STRAIGHT" (mode OFF).

### `s04_fade_bent.png` (FADE)

> Same composition. ABOVE the ball, a column of dashes is offset LEFT of the ball axis — mean dash x ≈ 525, ~60 px left of center. Bend goes LEFT.

### `s02_fadedraw_armed.png` (FADE/DRAW armed, idle)

> Same composition. Aim line is NOT drawn (Idle state, no drag → renderer inactive). Cone is also minimized. FADE/DRAW tile label visible — armed but no drag yet. Correct per D3 (bend magnitude ∝ FinetuneX; idle ≠ drag).

### `s05_draw_ball_flight.png` (DRAW shot fired)

> Same composition. Ball is IN THE AIR above the green/water area; no aim line (post-shot). Power ring 70%. Ball appears in upper portion of frame, slightly right of canvas center (image_x≈653, +68 from center).

### Quantitative dash-position table (whole-line scan y=900..1100)

| Still | Claimed state | Mean dash image_x | Offset from ball center (585) |
|---|---|---|---|
| `s01_straight_line.png` | Straight | 584.0 | −1 (centered) |
| `s02_fadedraw_armed.png` | FADE/DRAW armed idle | — | n/a (no line, correct per D3) |
| `s03_draw_bent.png` | DRAW (FinetuneX=−1) | 644.0 | **+59 (RIGHT)** |
| `s04_fade_bent.png` | FADE (FinetuneX=+1) | 525.1 | **−60 (LEFT)** |
| `s05_draw_ball_flight.png` | DRAW ball in flight | 653.1 | **+68 (RIGHT — the BALL)** |

Sub-band scans confirm the bend grows with t (lateral offset increases along the line):

- s03 DRAW: y=900–940 → +58, y=940–980 → +61, y=980–1020 → +61.
- s04 FADE: y=900–940 → −59, y=940–980 → −62, y=980–1020 → −62.

DRAW and FADE are pairwise symmetric (≈±60 px, 119 px separation) and grow with depth into the line — exactly the parametric `t²` curve the SPEC asks for.

### Video — line direction across half-second frames (raw video, uncaptioned)

Same dash-centroid metric, sampled every 0.5 s through the bend zone (raw `fade_draw_aim_line_bend_gate.mp4`):

| Timestamp window | Phase | Mean dash x | Offset | Direction |
|---|---|---|---|---|
| t=33.0–34.5 | DRAW drag (FinetuneX=−1) | 644.2–646.1 | +59 to +61 | **RIGHT** ✓ |
| t=35.0–36.5 | between (CancelExternalDrag → re-arm) | — | — | no line |
| t=37.0–39.5 | FADE drag (FinetuneX=+1) | 524.0–526.2 | −59 to −61 | **LEFT** ✓ |
| t=40.0–41.5 | between (CancelExternalDrag → re-arm) | — | — | no line |
| t=42.0 | DRAW re-arm before fire | 652.1 | +67 | **RIGHT** ✓ |

**The video DRAW phase agrees with the s03 still: BOTH show DRAW bending RIGHT.** The iter-2 reviewer's reading of "video shows DRAW LEFT" is not reproducible against the iter-3 raw video. The iter-3 timeline is internally consistent.

### Ball-flight check — line mirrors ball (the missing iter-2 test)

Frames sampled t=43.0, 43.3, 43.6, 43.9, 44.2, 44.5, 44.8, 45.1, 45.4 from the raw video. Clear timeline:
- t≈42–43: shot fires; blue predictor trail visible.
- t=43.0–44.0: ball trail rises from bottom-center (tee), arcing UP AND TO THE RIGHT (the chase camera tilts/banks to track but the WORLD x of the trail and ball monotonically moves RIGHT relative to the tee column).
- t=44.5–44.8: BALL VISIBLE as a white sphere on the right side of the frame, well right of the tee column.
- t=45.4: ball lands/visible right of canvas center.

The DRAW shot **curves to screen-right**, which **matches the DRAW aim-line direction** (s03 +59 RIGHT, t=33–34.5 video +59 to +61 RIGHT, t=42 +67 RIGHT). The "matches the ball's actual curve direction" half of the SPEC § Capture gate is satisfied.

### Y-flip absence

Spot-checked at every ~3 s across the full 45.5 s clip, plus dense sub-second sampling around the iter-2 named t=32 defect (t=30, 31, 31.5, 32, 32.5, 33). All 29 sampled frames in raw + captioned are right-side-up: JAMES portrait upright, LOMOND HUD upright, SPIN/GOLFIN/FADE-DRAW/DRIVER tile labels upright, sky at top, ground at bottom. Some post-shot frames (t≈43.5–43.9) show the camera ROLLED (banking to follow the ball) — this is normal chase-camera behavior, NOT a y-flip: the HUD remains upright, only the world camera rotates. The `reference_botvideorecorder_yflip_fix.md` defect would be a full image flip (HUD inverted, ball at top, sky at bottom); none present.

### MD5 audit (capture distinctness)

The five iter-3 stills are pairwise unique (size deltas already show this; the iter-2 RT-no-flush trap stays fixed). Bend signs are also pairwise opposite as expected (s03 vs s04).

## Capture gate compliance (SPEC § Capture gate)

| SPEC requirement | Result |
|---|---|
| Normal play, normal chase camera (no camera-mode switching) | PASS — video uses real production boot (ShellScene → Practice → HoleSelection → ActionButton → Hole 6 Geo). Chase camera throughout; no overhead/side/Downrange. Banking during ball-track is normal chase, not mode switching. |
| Arm via real on-screen UI button (`ShotModeContext.Toggle`) | PASS — scenario calls `Golfin.Gameplay.UI.HUD.ShotModeContext.Toggle()` and the bottom-right tile label flips Straight → FADE/DRAW (visible in s01 vs s02/s03/s04, and in cap t=22 "STRAIGHT" → t=31 "FADE/DRAW"). |
| 1170×2532 over a real hole | PASS — ffprobe confirms 1170×2532 H.264 60 fps; Hole 6 Geo loaded via Practice mode. All five stills also 1170×2532 per `sips`. |
| Show: straight → arm → line visibly bends → matches ball's curve direction | PASS — s01 straight → s02 armed (idle) → s03 DRAW bent right → s04 FADE bent left → s05 + t=42–45 in video: DRAW shot fired, ball curves RIGHT to match the DRAW aim line. |
| Lock all camera/render state BEFORE StartRecording | PASS by outcome — no y-flip detected anywhere in the 45.5 s clip (vs iter-2 named t=32 flip). The scenario adds a 3 s initial settle before navigation begins, which is consistent with the "all render state stable before recording" goal even if the report doesn't explicitly enumerate the locked knobs. |
| Caption unobtrusive | PASS — captions are single-line bars at top (~y=70, header) and bottom (~y=2380, body), small ~42pt, semi-transparent backgrounds. Aim-line region (y=850–1100) is fully unobstructed at every critical timestamp. Matches `feedback_caption_videos_unobtrusively.md`. |

## Figma fidelity (Rule 18)

SPEC references Figma node `2714:3536` (file `5gEAHjl6xAtW8iYY7NMvWd`), `imgLine1`/`Line 1`. Architect renders present at `reference/figma_node_2714-3536_actual.png`, `..._line1.png`, and `..._darkbg.png` — the node is a plain white opaque rectangle (62×444 raster, all RGB=(255,255,255), A=255). There is genuinely no gradient/color/border token to A/B against; the line look is preserved by sprite-cloning the existing `_targetingLine.Image.sprite` into the bend renderer.

| Element | Figma node | Figma value | Built value | Result |
|---|---|---|---|---|
| Aim line sprite identity | `2714:3536` | `imgLine1` raster sprite; 62×444, all (255,255,255,255) | `AimLineBendRenderer.EnsureSegments()` clones sprite from `_targetingLine.Image.sprite` (no reimport, no atlas change) | PASS |
| Line color | `2714:3536` | RGB=(255,255,255), A=1.0 (per Figma render) | `_lineColor = new Color(1f,1f,1f,0.8f)` (α=0.8 is the SPEC's noted in-game vertical-bleed convention; identical to today's `_targetingLine` rendering) | PASS |
| Segment width | `2714:3536` | SPEC D6: preserve existing line width | `SEG_WIDTH_PX = 3f` const, matches today's single-rect width | PASS |
| Segment count | n/a | SPEC: "12–20 segments" | `_segmentCount = 16` | PASS |
| Straight-mode visual match (D6) | `2714:3536` | Identical to today's straight-line render | s01 dashes vertical at x≈584; ball center x≈585; ≤1 px offset across the whole line; visual appearance indistinguishable from pre-task | PASS |
| No recolor / no thinning / no border / no gradient (D6) | `2714:3536` | Plain white fill | Code: `img.color = _lineColor`, `img.type = Image.Type.Simple`, no Outline/Shadow components added | PASS |
| Visible bend in FadeDraw mode (D1) | n/a | "visibly curves to match the fade/draw" | s03 DRAW +59 vs s04 FADE −60 vs s01 0 → 119 px DRAW–FADE separation at full ±1; visible in raw video too | PASS |
| Sign-faithful to 356 (D5) — line mirrors ball | n/a | Line bend direction matches ball curve direction | DRAW line right (s03 +59, video t=33–34.5 +59 to +61) AND DRAW ball curves right (s05 +68, video t=42.5–45 ball arcs right). FADE opposite. Internally consistent across stills + video + ball flight. | PASS |
| Magnitude monotonic in \|FinetuneX\| (smooth scaling) | n/a | "Bend magnitude scales smoothly" | Code: `lateralOffset = sign · |FinetuneX| · k · t²`; EditMode test `Magnitude_GrowsWithFinetuneX` PASS | PASS |

## Acceptance checklist verification

| Item | Implementer | Self-review | Notes |
|---|---|---|---|
| Fade/draw ARMED + handle LEFT → DRAW; handle RIGHT → FADE; signs match 356 | PASS | **CONFIRMED-PASS** | Stills AND video AND ball flight all consistent: DRAW bends right, FADE bends left, DRAW ball curves right. Internally consistent — that's what D5 ("matches the ball's actual curve direction") demands. |
| Bend magnitude scales smoothly with `ConeFinetuneX` | PASS | **CONFIRMED-PASS** | Pure `t²` math (`AimLineBendRenderer.Refresh`), `Magnitude_GrowsWithFinetuneX` test PASS, stills at ±1 show ≈±60 px symmetric. |
| Straight mode → line is straight, pixel-identical to today | PASS | **CONFIRMED-PASS** | s01 dashes at x≈584 vs ball x≈585 (−1 px). EditMode tests `StraightMode_ZeroFinetuneX_ZeroLateral` and `StraightMode_NotArmed_ZeroLateral` PASS. Same sprite, same width, same color. |
| Power changes during Pulling/Timing visibly move/extend curve (D4) | PASS | **CONFIRMED-PASS** | Raw video shows the line extending as the power ring climbs (s02 idle no line → drag phase visible line; t=42 DRAW pull from 0.45→0.7 shows line lengthening before fire). `PowerScaling_LargerReach_LargerAbsoluteTip` EditMode test PASS. |
| Curve never overshoots screen/flag at full res over a real hole | PASS | **CONFIRMED-PASS** | s03/s04 1170×2532 over Hole 6; tip stays well inside green/flag region. `MaxLateralClampPx=350` in code, tip at 0.35 · 500 = 175 px < clamp. |
| Line look preserved — same sprite, width, no recolor/thinning (D6) | PASS | **CONFIRMED-PASS** | Sprite cloned; `SEG_WIDTH_PX=3`; color white α=0.8; s01 visually identical to today. |
| No per-frame `BallSimulation` (D1) — parametric | PASS | **CONFIRMED-PASS** | `AimLineBendRenderer.Refresh()` is pure arithmetic; zero physics imports/calls (code-inspected). |
| EditMode tests: curve sign, monotonic magnitude, straight mode, power scaling | PASS | **CONFIRMED-PASS** | 8 tests present in `AimLineBendTests.cs` (193 lines); implementer reports all 8 PASS in iter-2 + unchanged in iter-3. |
| Unity Console clean | PASS | **CONFIRMED-PASS** | Pre-existing `Rindo_Hole09/`, `UIAutoWire.cs.meta` invalid-GUID warnings cited in HEARTBEAT baseline; not introduced by this task. |
| (Implicit, SPEC § Capture gate) Ball fires and curve matches line | PASS | **CONFIRMED-PASS** | s05 + video t=42.5–45.4: DRAW shot fired, ball curves right matching DRAW line. The iter-2 missing-shot defect is closed. |
| (Implicit, SPEC § Capture gate) Render state locked before StartRecording | PASS | **CONFIRMED-PASS by outcome** | Zero y-flip frames across 29 sampled timestamps. Iter-2 named t=32 defect is gone. |

## Bbox verification (Step 6)

N/A — no containment claims to verify. The aim line is a chain of screen-space sprite segments anchored at the ball position with parametric `t²` offsets; the "line stays within screen/flag region" claim is qualitative-spatial (already covered by visual scan + the `MaxLateralClampPx=350` clamp in code, well inside the 585-px half-width).

## Scene-mutation audit (Step 7)

`git diff --stat HEAD` shows:

- **Code-only**: `controls.csv` (+2 lines), `ControlsConfig.cs` (+6), `ControlsConfigLoader.cs` (+2), `ShotConeView.cs` (+81), `LoopV2SmokeBotMenu.cs` (+19), `LoopV2SmokeBot.cs` (+4), `Scenarios.cs` (+168) — all in spec scope.
- `Scenarios.cs` diff verified: clean append at end of file (`FadeDrawAimLineBendGate` static method) inside `Golfin.Physics.Viewer.Bot` namespace. No existing scenario modified. This is a scenario ADDITION for the bot recorder, not a production-scene mutation.
- **Zero diff on any `.unity` or `.asset` scene file** — `LabScaffold.unity` untouched (the renderer is runtime-added via `ShotConeView.SetupBendRenderer`, no YAML mutation). PASS.
- Untracked: `AimLineBendRenderer.cs(+.meta)`, `AimLineBendTests.cs(+.meta)` — both .meta pairs present (Lesson R compliance). PASS.
- Untracked under task folder: 3 reference renders, 5 stills `s01..s05`, HEARTBEAT.log — all expected.
- Untracked outside task folder: `Docs/Specs/Completed/sound_effects/screenshots/*.png` (30+) — cited in HEARTBEAT iter-3 baseline as pre-existing from the `sound_effects` task, not introduced by this iter. Verified: same drift was already cited in iter-2 baseline and exists at task start. PASS.

## Capture-helper compliance (Step 5)

- IMPLEMENTER_REPORT cites `BotDriver.Capture()` (sanctioned `CaptureCore.SnapPlayModeSafe`) plus `BotVideoRecorder` for the video. Both are sanctioned per CLAUDE.md § Screenshots Rule 6. PASS.
- No new `*Context.cs` under `Assets/Scripts/Gameplay/UI/ShotUI/HUD/` — `CaptureHelper` maintenance protocol (a)–(c) does not apply. PASS.
- Iter-3 also adds an explicit pre-capture state log (`d.LogStep($"[DRAW-BENT-CAPTURE] sc.State={sc.State} FadeDrawActive={sc.FadeDrawActive}")` etc.) immediately before each `Capture()` — this is exactly the state-machine settle assertion that iter-2's self-review recommended. PASS.

## Production-flow capture check (Step 8)

The scenario uses the real ShellScene boot path: `NavigateToHome → ClickModeCardPlay("practice") → WaitForScreen("HoleSelection") → Click("ActionButton") → WaitForSceneLoaded("LabScaffold") → WaitForAnyHoleGeo`. That is the genuine production practice-mode entry, not a smoke `*Host`/`*SmokeRunner` script. The captioned video also visibly shows the splash logo (t=1), Home screen (t=13), Hole 6 loaded (t=22) — real boot flow. PASS.

## Iter-2 defect closure summary

| Iter-2 defect | Iter-3 verdict | Evidence |
|---|---|---|
| 1. Sign-contradiction still vs video (DRAW still=RIGHT, DRAW video=LEFT) | **GONE** | Iter-3 raw video t=33–34.5 measures DRAW line +59 to +61 RIGHT (agrees with s03 still +59 RIGHT). Iter-3 FADE video t=37–39.5 measures −59 to −61 LEFT (agrees with s04 −60 LEFT). Stills and video are internally consistent. |
| 2. Captions covered the feature | **GONE** | Captions are top single-line ("Order 355: Fade/Draw Aim Line Bend") + bottom single-line body, ~42pt, semi-transparent. Aim-line region y=850–1100 is fully unobstructed at all 8 inspected timestamps. |
| 3. Y-flip at t=32 s | **GONE** | t=31, 31.5, 32, 32.5, 33 all right-side-up in raw video. Also sampled 24 other timestamps across the 45.5 s clip: zero flips. Post-shot camera banking (t=43.5–43.9) is normal chase-cam roll (HUD remains upright), not a render-state flip. |
| 4. Ball never fired | **GONE** | `Scenarios.cs` `FadeDrawAimLineBendGate` ramps power 0.45→0.7 and calls `EndExternalDrag()` after both bends are captured. Raw video t=42.5–45.4 shows the ball physically leave the tee and curve right; s05 captures the ball mid-flight. History log line "[DRAW SHOT FIRED] power=0.7, finetune=−1" cited in report. |
| 5. Stale iter-1/iter-2 PNGs in `screenshots/` | **GONE** | `ls screenshots/` = `.gitkeep`, `s01..s05_*.png` only. No `*_v2*`, no `crop_*`, no `figma-reference.png`. |

## Specific failures

None. All five iter-2 defects closed; no new defects found; SPEC § Capture gate satisfied end-to-end (straight → arm → bend → fire → ball-curve matches line).

## Iteration count

This is iteration **3** of self-review for this task. Per checklist § Iteration awareness, N=3 with a PASS verdict routes FORWARD to architect-review (not ESCALATE; ESCALATE is for a third FAIL).

## Routing

`FORWARD_TO_ARCHITECT` — STATUS set to `READY_FOR_ARCHITECT_REVIEW`. The architect-review hook (`golfin-reviewer`) fires next.
