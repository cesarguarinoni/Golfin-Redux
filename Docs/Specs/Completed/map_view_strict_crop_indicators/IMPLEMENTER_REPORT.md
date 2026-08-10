# IMPLEMENTER REPORT — map_view_strict_crop_indicators (Order 355)

**Iteration shape:** `map_view:strict_crop_and_indicators`
**Date:** 2026-08-10
**Baseline:** HEAD `dda1416e9`
**Canonical screenshot:** `screenshots/hole1_tee_open_strict_crop.png` (1170×2532)
**Canonical video:** `videos/map_view_strict_crop_indicators.mp4` (1170×2532, 39.3 s, 20.7 MB)

---

## 1. Files modified or created

| File | Lines | Summary |
|---|---|---|
| `Assets/Scripts/Gameplay/UI/ShotUI/MapViewController.cs` | +732 / −41 | Phase A framing (ball + landing, not ball + flag) under a new strict-containment invariant; Phase B footprint-clamped pan + dynamic pinch gate + editor tripwire; Phase C shared edge-clamped indicator for flag AND ball. |
| `Assets/Scripts/Gameplay/Tests/MapViewAimingTests.cs` | +453 / −0 | 16 new EditMode tests ("Test 8") driving the production statics: footprint geometry, per-axis footprint clamp, end-to-end containment on Holes 1 and 5 real OB rects, indicator dock/float/mirror/continuity/UI-avoidance. |
| `Assets/Scripts/UI/Editor/MapViewStrictCropDemoRecorder.cs` | new, editor-only | Video deliverable. Modelled on the existing `*DemoRecorder` family (`GameplayLocalizationDemoRecorder`), menu `GOLFIN > MapView > Record Strict Crop Demo Video`. **Beyond the SPEC's "single file" scope** — flagged deliberately: the standing rule is that a real-play video is the sign-off artifact, and the sanctioned way to produce one is to reuse this family, never to hand-stitch stills. |

Nothing else was authored. `MapViewCaptureDriver` (both files) has **zero** diff and compiles unmodified.
`Assets/Scripts/Physics/` has zero edits from this task — the four dirty paths there
(`BotDriver.cs`, `Scenarios.cs`, `ChaseCamera.cs`, `PhysicsLabController.cs`) are **pre-existing**,
present in the session-start `git status` snapshot before any work began, alongside
`LabScaffold.unity`, `BallConeAlphaMirror.cs`, `CentralBallWidget.cs`, the three `UI/` controllers,
`Docs/AI_CONTEXT.md`, `Docs/TellCode.md`, `Docs/Specs/Completed/shot_ui_translucency_glow/ARCHITECT_REVIEW.md`,
`tasks/loop_v2_smoke_bot/…`, and the untracked `Assets/Scripts/Physics/Tests/AimCameraFramingTests.cs`,
`Docs/Specs/Active/aim_camera_ball_centering/`, `Docs/Specs/Active/power_gauge_target_marker/`.

**One drift I caused and restored:** Unity re-serialised `m_CustomRenderQueue: 3100 → 3000` in
`M_SplashDroplet.mat`, `M_SplashFoam.mat`, `M_SplashRing.mat` during an asset refresh. These are on
the standing-ban list (PIPELINE_HARDENING rule 7), so all three were surgically reverted to their
HEAD values; `git status` on `Assets/Resources/FX/` is now empty.

---

## 2. What was built

### Phase A — framing (§3)
`BuildShotRegion` no longer includes the flag. The fit set is the **ball + the landing disc's four
extreme points** along the current aim, plus the `_minFramedSpanM` floor. That is what makes a strict
crop achievable at all: a whole-hole frame cannot be contained inside a 261 m-deep OB rectangle.

`FrameShowRegion` and the `AnchorBallToBottom` fallback both end in `ContainCameraFootprint`, so **no
path can violate the invariant** — mirroring 354's "no path may reveal the world" rule.

`ContainFootprint` (public static, camera passed in, same pattern as `SolveShowRegionPose` so tests
drive production code) is three steps:
1. shrink distance about the focus until the footprint fits the rect **by size** (converges because
   size depends on distance alone);
2. re-seat the ball vertically at `kShotBottomFrac` **and laterally** into a legal window;
3. translate by the containment correction — a ground translation cannot change footprint size, so
   this always succeeds, which is precisely why **containment wins over seats**.

### Phase B — pan / pinch (§4)
`PanCamera` clamps the **footprint**, per axis (slide-along-edge on diagonals), not the focus point.
`ClampPointToRect` is kept as the fallback when the footprint is unresolvable. Pinch zoom-**out** is
gated by `FootprintFitsAtFov` at the candidate FOV (zoom-in is always legal); the static
`_zoomOutCapFov` remains as the fast pre-check. `Update()` carries a `#if UNITY_EDITOR` assertion
that logs corners + rect on any violation, rate-limited to 1/s.

### Phase C — indicators (§5)
One `SolveIndicatorPlacement` (pure static) drives **both** the flag and the ball, so docking is
continuous by construction. Docked → icon on the target, no arrow. Floating → icon on the inset rect
along screen-centre→target, arrow rotated to point out at it. Behind-camera targets are mirrored
through screen centre. `_ballIconRT` / `_ballArrowRT` are new on the existing indicator canvas; the
ball's icon hides when docked because the world-space marker is its on-screen representation.
Arrow and ball sprites are **procedural** (generated `Texture2D`, no art import) with two serialized
slots (`_ballIndicatorSprite`, `_indicatorArrowSprite`) for Robin's styled versions.
`_indicatorEdgeInsetPx` is serialized and defaults to 70.

Controls are untouched: one finger aims, two fingers pan/pinch.

---

## 3. Three defects found in play mode and fixed

These were **not** visible in EditMode; each was caught by driving the real entry path and reading
the whole frame.

**(a) `Screen.width` lies in Editor play mode.** It reported the Game View *window* (2070×1772) while
the render surface was 1170×2532, so the indicator inset rect was solved in a different space than
`WorldToScreenPoint`'s output. Measured symptom: the flag never docked — panning it fully into frame
(`flagVP = (0.201, 0.845)`, comfortably inside) left it pinned to a phantom edge at y = 1702 with the
arrow still on. Fixed by using `_mapCam.pixelWidth/pixelHeight`, which *is* the projection surface and
matches both `WorldToScreenPoint` and the overlay canvas on device and in the Editor.

**(b) Hole 5: the containment zoom threw the ball off the side.** Hole 5 runs 41.5° off the snapped
playfield axis, so a 228 m driver puts the landing far laterally. The footprint came out 468 m deep
against a 337 m rect, six shrink steps fixed the depth, the vertical re-seat put the ball back on the
bottom margin — and the ball was at **viewport x = 1.196**, off the right edge, while the *landing*
stayed on screen. Exactly backwards: §3 says the landing may be sacrificed, never the ball. Added
`SeatBallLaterally`, and a regression test on Hole 5's real OB rect over 9 (carry × lie) combinations.

**(c) The ball seated under the SHOOT button.** The seat row (`kShotBottomFrac` → 203 px) is the row
SHOOT occupies. Two fixes: the lateral seat's window now stops short of the button (and clears the
indicator inset by `kIndicatorDockClearancePx`, because the bisection converges *exactly* — the ball
landed on screen x = 1100.0 against a dock boundary of 1100.0 and a float coin-flip decided whether
its own indicator fired); and where containment makes the move impossible (Hole 5 pins the
footprint's left edge to the OB boundary) a target under the button now counts as **not docked**, so
the indicator floats just clear of the button with the arrow pointing back down at the ball.

---

## 4. Acceptance — EditMode (spec §7)

`tests-run` EditMode, assembly `Golfin.Gameplay.Tests`:

**1078 total · 1075 passed · 0 failed · 3 skipped** (the 3 skips are pre-existing
`HoleCompleteDriverTests` Stage-C1 skips, unrelated). The whole EditMode suite ran, not just this
class — no regressions anywhere.

New tests, all driving the production statics (no local re-implementation):

| Test | Covers |
|---|---|
| `Footprint_AllFourCornersHitTheGround_AtHeroTilt` | 4 plane hits at fov 30/45/75/90 — if this fails the horizon is in frame and the crop is unenforceable |
| `Footprint_ShrinksMonotonically_AsTheCameraComesIn` | the monotonicity the containment pass relies on to terminate |
| `Footprint_GrowsWithFov_WhichIsWhyZoomOutIsGated` | why the pinch gate exists |
| `FootprintClamp_LeavesALegalMoveUntouched` | no rubber-banding away from the edges |
| `FootprintClamp_StopsTheFootprintAtTheRectEdge_NotTheFocusPoint` | **the 355 change** vs 354 |
| `FootprintClamp_SlidesAlongTheEdgeOnADiagonalPan` | per-axis solve = slide, not dead stop |
| `FootprintClamp_CorrectsAnAlreadyViolatingFootprint_WithAZeroMove` | the move=0 correction the framing pass reuses |
| `FootprintClamp_CentresAnOversizedFootprint_…` | symmetric leak in the infeasible case |
| `StrictCrop_OpenFramingContainsTheFootprint_OnRealHoleGeometry` | Hole 1 real rect, 3 carries × 4 lies (tee → t=0.95 green-side) |
| `StrictCrop_ContainmentWinsOverTheBallSeat_AndTheBallStaysOnScreen` | ball hard against the near rect edge |
| `StrictCrop_BallStaysOnScreen_WhenTheContainmentZoomThrowsItSideways` | **regression for defect (b)**, Hole 5 real rect, 3 carries × 3 lies |
| `Indicator_DocksWhenTheTargetIsOnScreen` | docked = icon on target, no arrow |
| `Indicator_FloatsOnTheInsetRect_WhenTheTargetIsOffScreen` | edge intersection + arrow bearing |
| `Indicator_StaysInsideTheInsetRect_ForEveryOffScreenDirection` | 24 bearings, position + arrow angle |
| `Indicator_MirrorsTargetsBehindTheCamera` | behind-camera mirror |
| `Indicator_IsContinuous_SoItWalksTheEdgeInsteadOfJumping` | no jump across the dock boundary |
| `Indicator_NeverFloatsUnderTheShootButton` | UI avoidance |
| `Indicator_DockedTargetIsLeftOnTheWorldPoint_WhenClearOfUi` | a visible docked icon is never displaced |
| `Indicator_FloatsClearOfUi_WhenTheTargetIsHiddenUnderTheShootButton` | **regression for defect (c)** |

---

## 5. Acceptance — Editor manual matrix (spec §7), real entry path

Every run boots ShellScene → **StartButton** → **PlayButton** → real
`HoleCardController.actionButton` → hole load → real **HoleMap** button `onClick`
(PIPELINE_HARDENING rule 2 — no synthetic entry). Captures via
`GOLFIN/Screenshot/Capture Game View`, all **1170×2532** (verified with PIL).

### (a) Open → only playable area, ball seated low

| Hole | Footprint (m) | OB rect (m) | Inside? | ball VP | flag VP | Flag indicator | Ball indicator |
|---|---|---|---|---|---|---|---|
| 1 (long, 13.4° off axis) | 297 × 148 | 576 × 261 | **YES** | (0.796, 0.080) | (0.067, 1.365) | floating, top edge + arrow | hidden (docked) |
| 5 (41.5° off axis, driver) | 161 × 323 | 317 × 337 | **YES** | (0.840, 0.080) | (−0.729, 0.900) | floating, left edge + arrow | floating clear of SHOOT, arrow down |
| 6 (short) | 197 × 98 | 229 × 101 | **YES** | (0.194, 0.080) | (0.430, 0.834) | **docked over the hole**, no arrow | hidden (docked) |

Hole 6 is the "if it fits" case Cesar described: the flag docks on the green with no arrow.
Screenshots: `hole1_tee_open_strict_crop.png`, `hole5_tee_open_flag_floating_ball_lifted_clear.png`,
`hole6_tee_open_flag_docked.png`.

### (b) Pan to all four edges + diagonal — invariant intact
Hole 1, `PanCamera` driven live: **750 steps across 5 directions (U/D/L/R/diagonal), 0 invariant
violations.** Each stop lands exactly on the rect boundary:

```
UP    stop footprint=[  -8.3,-72.6]..[288.9, 75.9]
DOWN  stop footprint=[-288.8,-72.6]..[  8.3, 75.9]
LEFT  stop footprint=[ -59.8,-17.1]..[237.3,131.3]
RIGHT stop footprint=[ -59.8,-131.3]..[237.3, 17.1]
DIAG  stop footprint=[  -8.3,-17.1]..[288.9,131.3]   ← both axes at their limit = slide-along-edge
rect  =              [-288.1,-130.6]..[288.1,130.6]
```

A separate 141-step diagonal pan also returned **0/141** violations.
`hole1_pan_flag_docked_ball_floating.png` is the panned-to-the-green frame: the ball indicator floats
at the bottom pointing back at the off-screen ball, clear of SHOOT.

### (c) Pinch — zoom-out refuses at containment
At the open pose (Hole 1 centred): `30→fits 40→fits 45→fits 46→fits 50→fits 60→fits 90→REFUSE`,
with `_zoomOutCapFov = 45` already refusing anything above 45 as the fast pre-check; FOV restored to
45.0 after every probe. Panned to the edge, the dynamic gate binds *before* the static cap would:
`50/55/60/70/90 → all REFUSED` (at fov 50 the footprint is 337×171 m, which fits the 576×261 m rect
**by size** but not at that position — the gate is doing real work beyond a size check).

### (d) Pan toward the hole → indicator walks the edge and docks
Hole 1, 141-step diagonal pan: **docked at step 34**, the frame the flag entered the inset rect
(`flagVP` crossed from 0.954 → inside). Max icon movement between adjacent steps **38.3 px** for a
~57 px pan step — proportional, no jump at the dock boundary. Icon position after docking
(457.04, 2148.91) equals `WorldToScreenPoint(flag + 2m)` = (457, 2149) exactly.

### (e) Aim ±90°, SHOOT closes + write-back
Real SHOOT `onClick`: `IsOpen=False`, `aimBefore = −2.9073` → `ShotController.CameraHeadingRadians =
−2.9073` (match). World fully restored — `MapView_RuntimeRoot alive=False`, `mapCams=0`,
`indicatorCanvases=0`; sky, trees, HUD, top bar, mini-map, SPIN/GOLFIN/STRAIGHT/DRIVER chrome and the
aim cone are all back (`hole1_map_closed_world_restored.png`).

### (g) Video — `videos/map_view_strict_crop_indicators.mp4`
1170×2532, 39.3 s, 30 fps, GameView source (a camera source drops the ScreenSpaceOverlay indicators
under URP). Recorded through the same real entry path as the matrix above; **every camera move in
the clip goes through the production `MapViewController.PanCamera`** and every interaction through a
real widget's `onClick` — the recorder re-implements no framing, clamping or indicator math.

Beats: gameplay HUD at the tee → tap the real HoleMap button → map opens (strict crop, ball seated
low, flag off-screen with its floating indicator + arrow) → pan toward the hole (indicator walks the
edge, then **docks on the hole**; the ball leaves frame and its own indicator appears pointing back,
clear of SHOOT) → keep panning (camera stops dead at the boundary) → pan back → SHOOT → world
restored.

**Pinch is deliberately not in the clip.** The Editor has no `Touchscreen`, so the two-finger branch
of `HandleTouchInput` cannot be driven honestly; staging it would have meant the recorder duplicating
the gate's own logic. It is covered by the EditMode tests and the live numbers in §5(c) instead.

Orientation verified by decoding **consecutive** frames (n = 330, 331) plus a spread across the clip
— not `ffmpeg -ss` keyframe sampling, which misses flips (PIPELINE_HARDENING rule 4). Captions were
frame-checked after encoding: the first pass overflowed 1170 px (`build_bot_video.py` does not wrap),
so every caption was re-wrapped with explicit line breaks and re-encoded from the same raw.

### (f) Editor invariant assertion silent, console clean
`grep -c "INVARIANT VIOLATION" ~/Library/Logs/Unity/Editor.log` → **0** across the entire session
(3 holes, many opens, ~900 live pan steps). `MapViewController` exceptions → **0**. No new warnings.

---

## 6. Needs manual / on-device verification

1. **The pinch gesture itself.** The FOV gate is unit-tested and was probed live through
   `FootprintFitsAtFov`, but the Editor has no `Touchscreen`, so the two-finger branch of
   `HandleTouchInput` that *calls* it was never exercised. Expect: zoom-in works, zoom-out stops.
2. **The two-finger pan gesture itself.** `PanCamera` was driven ~900 times live with zero
   violations, but the `HandleTouchInput` → `PanCamera` plumbing was not. Same reason.
3. **One-finger aim drag on device.** `TrySetAimFromScreenPoint` is untouched by 355, but the
   indicator now re-solves every frame against the camera pose, so a quick look while dragging is
   worth it.
4. **Green-side lies, visually.** Proven numerically through the production solver on Hole 1
   (12 carry × lie combinations, t up to 0.95) and Hole 5 (9), but reaching a green-side lie visually
   needs a real played shot; all three visual captures are tee lies.

## 7. Observed, out of scope, flagged

- **Pre-existing:** the §11 invariant-dump coroutine (`DoFrameReadbackAndDump("aimed")`, Order 352)
  calls `SetAimYawDirectly` twice, and that calls `PositionMapCamera()` — so a pan made within the
  first ~1 s of opening the map is silently re-framed away. Verified in the source, not introduced
  here, and left alone.
- **Pre-existing:** water renders near-black in map view on Holes 5 and 6 (see memory
  `bug_water_color_physicslab` / `project_water_test_hole_6`). It is inside the OB rect, so the
  invariant is satisfied — §2 explicitly accepts that some OOB fringe inside the rect will show.
- On Hole 5 with a driver the landing goes off-screen (`landingVP.x = −0.178`) because the shot is
  468 m deep against a 337 m rect. That is the §1.2 consequence Cesar accepted; the guide line clips
  at the screen edge and the two indicators carry orientation.

## 8. Out of scope / not regressed (§8)

P-006, P-007, P-009 untouched. No fairway-mask-tight cropping. No scene edits. No `zones.json` edits.
No `MapViewCaptureDriver` edits. No RT / RawImage / `uvRect` reintroduction — the banned list at the
top of `MapViewController.cs` is intact and unmodified. Everything on the §6 salvage list
(`SnapAxisToPlayfield`, `HideEnvironmentForMap`/`RestoreEnvironmentAfterMap`, hole-fitted far clip,
`TryGetObRect` cache, `TrySetAimFromScreenPoint` + `kHorizDragSensitivity`, landing zone / guide line
/ rings, SHOOT repurpose, aim write-back, invariant JSON dumps) is kept and extended, not rewritten.
