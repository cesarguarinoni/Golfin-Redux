# SPEC — `shot_view_layout`

> **Authoritative spec for this task.** Implementer reads this and ONLY this for the work definition. STATUS.md tracks pipeline state. Reports/reviews go in their own files.
> Written 2026-09-07 (Architect, Cowork). Project mirror: `claude/SHOT_VIEW_LAYOUT_SPEC.md`.

## Status

See `STATUS.md`. Starts at `SPEC_READY`.

## Goal

Bring the shot view back to the framing of the Figma design (`In-Game - Shot Tests`, node `14153:4602`): more sky and fairway, the ball **lower** on the screen (62 % from the top instead of dead centre), a **longer pull lane** for the Pendulum and Free Swing schemes, the round power gauge **up** in the HUD where it no longer sits on the aim bar, and every bottom element (the four action buttons, the selector overlays, the lane end) on **one shared bottom baseline** that clears the iOS/Android home-gesture zone.

Nothing about how a shot is computed changes. The camera already pins the 3D ball to the 2D `CentralBall` widget (`PhysicsLabController.GetAimBallViewportY()` → `SolveAimCameraPose`), so moving the widget IS the camera change — no camera code is touched.

Cesar's measurements that drove this (fractions of screen height, from the top; live = Pendulum scheme on Lomond hole 2):

| | Figma 14153:4602 | Live 2026-09-06 |
|---|---|---|
| Horizon | ~38 % | ~21 % |
| Ball centre | ~62 % | 50 % |
| Aim bar | ~59 % | ~45 % |
| 100 % line | ~91 % | ~68 % |
| 120 % line | ~93 % | ~71 % |
| Pull travel ball → 100 % | ~28 % | ~18 % |
| Round gauge | ~30 %, top-right, clear | ~50 %, on the aim-bar row |

## Reference

- **Figma frame:** Golfin Game Redux / `In-Game - Shot Tests` node `14153:4602`, file `5gEAHjl6xAtW8iYY7NMvWd` — render in `reference/figma_14153-4602_shot_view.png`.
- **Live screenshot** (Pendulum, what we are moving away from): `reference/live_pendulum_2026-09-06.png`.
- Figma content that is mockup, not target: the 2D golfer on the left, the driver-club handle, the dashed hit ring around the ball, the Fade/Draw button (Pendulum shows Straight/Fade-Draw per `CONTROL_SCHEMES_PLAN` §7.3). Only the **positions** listed in §2 are taken from the node.

## 1. Decisions (locked with Cesar 2026-09-06/07)

| # | Decision |
|---|---|
| D1 | Ball anchor is **per scheme**. Pendulum / Needle / Free Swing → viewport **y = 0.38** (62 % from the top). **Flick stays at 0.5** — its cone is scene-authored 1009 px tall with its base at ball −1160; lowering the ball would push the base off-screen or force a shorter cone, and `control_scheme_seam` guarantees Flick byte-identical. Cesar can move Flick later by changing one key (§3.1) — the cone then travels with the ball. |
| D2 | One **bottom baseline**, `BottomBaselinePx = 170` canvas px above the canvas bottom, raised to `safeAreaBottom + 60` when the device inset is larger. The four action buttons' bottoms, both selector overlays and the lane end all sit ON it. Today they sit at 96. |
| D3 | The **120 % handle position** is the thing that must clear the home-gesture zone (the flick starts there), not the lane's rounded end. With D2 it lands 244 canvas px (≈ 81 pt) above the screen edge on 1170×2532. |
| D4 | Pendulum and Free Swing pull lengths grow so the lane end meets the baseline: `Pull100Px 380 → 540`, `Pull120Px 456 → 648` (exact 1.2× spacing kept — tests assert it). The lane end lands 4 px above the baseline; D6 is a ≥ constraint, not equality. Bots read these through `PullPxForPower` and their sigma is on the normalised timing axis, so they need nothing. |
| D5 | **Needle keeps 380 / 456.** Its pull is a circle around the ball, not a lane; the 120 % ring is already r = 526 on a 585 half-width canvas, and at 648 it would clip left/right. Only its ball anchor moves. |
| D6 | On screens too short for the lane (16:9 phones, tablets) the ball anchor is **raised automatically** so the lane end never drops below the baseline: `ballY = max(anchorY, canvasBottom + baseline + 70 + Pull120 + 70)`. Constants stay device-independent (canvas px = width-normalised), framing degrades gracefully instead of the lane running under the buttons. |
| D7 | Round power gauge → anchored **top-right**, centre at viewport **y = 0.70** (30 % from the top), right margin unchanged (48). |
| D8 | No debug sliders. Tuning is by editing `Assets/Resources/Gameplay/controls.csv` and relaunching (Cesar: "suggest a value, let me tweak after"). |
| D9 | Sequencing: **do not run concurrently with `selector_carousel`** — both edit `ActionButtonsBuilder.cs` and `LabScaffold.unity`. Run this after it closes (or before it starts). |

## 2. Target layout (canvas px, 1170 × 2532 reference; canvas origin = centre; canvas bottom = −1266)

| Element | Today | Target | How |
|---|---|---|---|
| `CentralBall` y (Pendulum/Needle/FreeSwing) | 0 (vy 0.50) | **−304** (vy 0.38) | `ShotLayoutController` from `BallAnchorViewportY_<scheme>` |
| `CentralBall` y (Flick) | 0 | 0 | `BallAnchorViewportY_Flick = 0.5` |
| Handle rest (all schemes) | ball −70 | ball −70 (unchanged) | `_handleRestBelowBall`, untouched |
| Pendulum aim bar `PendulumBarRoot` | ball +128 | ball +128 → −176 (57 % from top) | moves with `BallSpace` |
| Pendulum / FreeSwing 100 % tick | rest −380 | rest **−540** | `PendulumPull100Px`, `FreeSwingPull100Px` |
| Pendulum / FreeSwing 120 % tick | rest −456 | rest **−648** → y −1022 | `PendulumPull120Px`, `FreeSwingPull120Px` |
| Lane end (`LaneHeight` = deepest + clubHalfHeight 50 + tail 20) | −596 | **−1092** (4 px above the −1096 baseline) | falls out of the constants |
| Action buttons bottom row (`GolfinButton`, `DriverButton`) y | 96 | **170** | builder + scene |
| Action buttons top row (`SpinButton`, `FadeDrawButton`) y | 360 | **434** | builder + scene (row gap 264 kept) |
| Selector overlays `_anchoredPositionForClub/Ball` y | 96 | **170** | builder |
| `PowerHUD` (200×200, pivot 1,1) | anchor (1,0.5), pos (−48, 100) | anchor **(1,1)**, pos (−48, −(0.30·H − 100)) = **(−48, −660)** | `PowerGaugeViewportY = 0.70` |
| Needle rings r80/r100/r120 | 374 / 450 / 526 | unchanged | D5 |
| Flick `ConeMesh` | ball −1160 | ball −1160 (ball unchanged → no change) | moves into Flick `BallSpace` |

Resulting pull travel ball → 100 % = 70 + 540 = 610 px = **24 %** of screen height (was 18 %, Figma 28 %). Horizon should land at roughly 35–38 %.

## 3. Implementation

### 3.1 Config — `ControlsConfig` + `controls.csv`

Add to `Assets/Scripts/Gameplay/Config/ControlsConfig.cs` (struct fields + `Default` seeds), `ControlsConfigLoader.cs` (switch cases) and `Assets/Resources/Gameplay/controls.csv` (rows, with `notes`):

```
BallAnchorViewportY_Flick,0.5,3D ball / CentralBall viewport Y (0 = bottom) while Flick is active. Camera pins to it (PhysicsLabController.GetAimBallViewportY). Kept at centre: the Flick cone is 1009 px tall below the ball.
BallAnchorViewportY_Pendulum,0.38,shot_view_layout (2026-09-07): 0.5 -> 0.38 = 62 % from the top, the Figma 14153:4602 ball position. Lower ball = camera pitches up = horizon back at ~38 %.
BallAnchorViewportY_Needle,0.38,same as Pendulum
BallAnchorViewportY_FreeSwing,0.38,same as Pendulum
BottomBaselinePx,170,canvas px above the canvas bottom shared by the action buttons' bottom edge, the selector overlays and the Pendulum/FreeSwing lane end. Raised at runtime to safeAreaBottom+60 when the device inset is larger. Was 96 (buttons only).
PowerGaugeViewportY,0.70,centre of the round power gauge (PowerHUD), 0 = bottom. 0.70 = 30 % from the top, top-right under the hole card (Figma 14153:4602).
```

Change existing rows (keep their notes, append the change note):

```
PendulumPull100Px,540,... shot_view_layout (2026-09-07): 380 -> 540 so the lane end meets BottomBaselinePx with the ball at 0.38
PendulumPull120Px,648,... 456 -> 648 (1.2x spacing kept)
FreeSwingPull100Px,540,... seeded equal to Pendulum, same reason
FreeSwingPull120Px,648,...
```

`Needle*` rows unchanged (D5). `controls.csv` is a `Resources` asset read by `ControlsConfigLoader`, **not** a content catalog — no importer run. Update the header comment line about the confirm tiles: this task is exactly the case it warns about (§3.7).

### 3.2 Scene — one `BallSpace` per scheme root (`Assets/Scenes/Physics/LabScaffold.unity`)

Every scheme's ball-relative UI is today anchored to the canvas **centre** with absolute offsets that assume the ball is at (0,0). Give each root a `BallSpace` RectTransform (anchor 0.5/0.5, pivot 0.5/0.5, size 0, anchoredPosition = the scheme's ball anchor) and re-parent the ball-relative children under it, **preserving their current anchoredPosition** so nothing moves until `ShotLayoutController` sets `BallSpace.y`:

| Root | Move under `BallSpace` | Leave where it is |
|---|---|---|
| `SchemeRoot_Flick` | `ConeRoot` (ConeMesh, TargetingLine, ClubHandle, TimingSlab), `PutterTrack` | — |
| `SchemeRoot_Pendulum` | `PendulumLaneRoot`, `PendulumBarRoot`, `PendulumHandle`, `PendulumGradePop` | — |
| `SchemeRoot_Needle` | `NeedleCircleRoot`, `NeedleArcRoot`, `NeedleHandle`, `TapHint`, `NeedleGradePop`, `NeedleTapCatcher` (so its coverage relative to the rings is unchanged) | — |
| `SchemeRoot_FreeSwing` | `FreeSwingLaneRoot`, `FreeSwingTraceRoot`, `FreeSwingHandle`, `FreeSwingAnalyzerChip`, `FreeSwingGradePop` | — |

`CentralBall` stays a direct child of `ShotUI_Canvas`; the controller drives its `anchoredPosition.y` directly (it is what the camera reads).

`ConeRoot` is a full-stretch rect: re-parenting it under a zero-size `BallSpace` collapses it. Either give Flick's `BallSpace` the same stretch anchors (0,0)–(1,1) with `anchoredPosition` used as the offset (a stretched rect offsets fine), or re-anchor `ConeRoot` to centre with size 1170×2532. Pick one, state it in the report. **Flick parity check:** with `BallAnchorViewportY_Flick = 0.5` every Flick rect's world position must be identical to before the change — dump `ConeMesh`, `ClubHandle`, `TimingSlab` world corners before/after and quote them.

Do the same in `Assets/Scenes/Physics/PhysicsLab_Hole1.unity` **only if** it is still a shipped/used scene — it has its own `CentralBall`/`ClubHandle`. NOTE: Architect believes it is legacy; confirm and say so in the report rather than editing it blind.

### 3.3 `ShotLayoutController` (new, `Assets/Scripts/Gameplay/UI/ShotUI/ShotLayoutController.cs`, namespace `Golfin.Gameplay.UI.ShotUI`)

One MonoBehaviour on `ShotUI_Canvas`. Serialized: `_canvasRect` (root canvas RectTransform), `_centralBall` (RectTransform), `_ballSpaces` (4 entries `{ControlScheme scheme; RectTransform rect;}`), `_actionButtonsCluster` (RectTransform), `_powerHud` (RectTransform), the two `SelectorOverlayWidget`s (or their rects), `_pendulumLane` / `_freeSwingLane` views (for `LaneHeight`). Reads `ControlsConfig` the same way `PendulumLaneView` does.

Pure math in `static class ShotLayoutMath` (same file or `ShotLayoutMath.cs`), unit-testable, no Unity scene types:

```csharp
// canvas y (origin centre) for a viewport fraction
public static float AnchorY(float viewportY, float canvasHeight) => (viewportY - 0.5f) * canvasHeight;

// baseline above the canvas bottom, safe-area aware. safeBottomCanvasPx = Screen.safeArea.y * canvasHeight / Screen.height
public static float Baseline(float baselinePx, float safeBottomCanvasPx) => Mathf.Max(baselinePx, safeBottomCanvasPx + 60f);

// D6: lowest ball y that keeps the lane end on the baseline. handleRest=70, clubHalfHeight=50, tail=20 → laneBelowBall = 70 + pull120 + 70
public static float BallYForLane(float canvasHeight, float baseline, float pull120Px, float handleRestBelowBall, float clubHalfHeight, float laneTailPx)
    => -canvasHeight * 0.5f + baseline + handleRestBelowBall + pull120Px + clubHalfHeight + laneTailPx;

public static float ResolveBallY(float anchorViewportY, float canvasHeight, float baseline, bool schemeHasLane, float pull120Px, ...)
    => schemeHasLane ? Mathf.Max(AnchorY(anchorViewportY, canvasHeight), BallYForLane(...)) : AnchorY(anchorViewportY, canvasHeight);
```

`Apply(ControlScheme scheme)`:
1. `H = _canvasRect.rect.height`; `baseline = Baseline(cfg.BottomBaselinePx, safeBottomCanvasPx)`.
2. `ballY = ResolveBallY(...)` — `schemeHasLane` is true for Pendulum and FreeSwing (use their `Pull120Px`), false for Flick and Needle.
3. `_centralBall.anchoredPosition.y = ballY`; every `BallSpace.anchoredPosition.y = ballY` (all four, so a scheme switch at Idle finds its root already in place).
4. `_actionButtonsCluster.anchoredPosition.y = baseline − 96f` (the cluster is full-stretch; the builder keeps authoring rows at 96/360 and the controller applies the delta — one source of truth for the number, no per-button edits). Selector overlays: set `_anchoredPositionForClub/Ball.y = baseline` through a small public setter on `SelectorOverlayWidget` (they open at that y).
5. `_powerHud`: anchors (1,1), `anchoredPosition = (−48, −((1 − cfg.PowerGaugeViewportY) * H − 100))` (pivot 1,1 → the rect's top is 100 above its centre).

When it runs: `OnEnable`, on `ControlSchemeService.OnSchemeChanged` **routed through `ShotSchemeHost.Apply`** (call the layout controller from `Apply(scheme)` right before `driver.Activate()`, so the scheme root is positioned before it reads the finger and the Idle-deferral logic is reused — do not add a second Idle gate), and on `OnRectTransformDimensionsChange` (rotation / resolution change). Never while `ShotController.State != Idle` except through the host's existing deferral.

Camera: `GetAimBallViewportY()` reads the live `CentralBall` rect each `ApplyCameraYaw`, so the pitch follows on the next aim-camera apply. Confirm in the Editor that the first frame after a scheme switch at Idle shows the new framing without a one-frame pop (if `ApplyCameraYaw` is not called on `OnSchemeChanged`, call `ReapplyActiveScheme()` → whichever path already re-applies the camera; NOTE and quote it).

### 3.4 Builder — `Assets/Scripts/Editor/CanvasScalerMigration/ActionButtonsBuilder.cs`

The cluster offset is applied at runtime (§3.3 step 4), so the builder's `96f`/`360f` rows stay. Add one comment block at the CONFIG SNAPSHOT pointing at `BottomBaselinePx` and `ShotLayoutController`, and make the builder wire the new `[SerializeField]`s on `ShotLayoutController` when it runs (8.5) so a rebuild cannot leave them null. Add `BallSpace` creation to the builder only if a builder already creates the scheme roots (check `SchemeRoot_*` authoring — if they are hand-authored in the scene, hand-author `BallSpace` too and say so).

### 3.5 Lane views

`PendulumLaneView` and `FreeSwingLaneView` already draw ticks and `LaneHeight` from cfg ("THE LANE IS THE CONFIG, DRAWN") — no change expected. Verify the drawn 120 % tick ends up at canvas y **−1022 ± 2** and the lane end at **−1092 ± 2** on a 1170×2532 Game View and write both numbers in the report. `NeedlePowerCircleView` unchanged.

### 3.6 Tests (EditMode)

New `Assets/Scripts/Gameplay/Tests/ShotLayoutMathTests.cs`:
- `AnchorY(0.38, 2532) == −304`, `AnchorY(0.5, 2532) == 0`.
- `Baseline(170, 102) == 170`; `Baseline(170, 150) == 210`.
- 1170×2532, Pendulum defaults: `ResolveBallY == −304` (anchor wins) and lane end (`ballY − 70 − 648 − 70 = −1092`) `>= −1266 + 170`.
- 16:9 (canvas H 2080): `ResolveBallY > AnchorY(0.38, 2080)` and lane end `== −1040 + 170` exactly (D6 clamps to the baseline).
- 4:3 (H 1560): same invariant holds; ball ends above centre (document the value, it is expected).
- Needle and Flick: `ResolveBallY == AnchorY` regardless of H.

Existing tests: `PendulumMathTests`, `FreeSwingMathTests`, `PendulumSchemeDriverTests`, `FreeSwingSchemeDriverTests` carry `380f`/`456f` literals — update **fixture values** to 540/648 where they mirror `Default`; any assertion that encodes the 1.2× tick spacing or `Power(p100) == 1` must still pass unchanged. `ShotAimParityTests`, `ShotTimingPowerTests`, `ShotControllerFlickGateTests`, `MapViewAimingTests`, `AimCameraFramingTests` must be green **unmodified** (Flick parity, D1). Full EditMode run count in the report.

### 3.7 Scheme confirm tiles (mandatory follow-through)

`controls.csv`'s header already says it: the scheme confirm pop-up's tiles are captured from the running game. This task changes lane length and ball position for three schemes → re-run the tile capture (`GOLFIN > Capture > …` — the menu `scheme_confirm_popup` added; name it in the report) for **Pendulum, Needle, Free Swing** and commit the new tiles. Flick tiles unchanged (D1).

### 3.8 Docs

- `Docs/AI_CONTEXT.md` — layout paragraph: ball anchor per scheme, baseline, gauge.
- `CONTROL_SCHEMES_PLAN.md` §8 geometry line ("pull lane 300 px = 100 %, 360 px = 120 %") is now stale for Pendulum/FreeSwing — add a one-line note pointing here, don't rewrite it.

## 4. Acceptance checklist (Implementer fills in `IMPLEMENTER_REPORT.md`)

Each item `PASS`/`FAIL` with the measured number.

- [ ] Game View 1170×2532, Pendulum selected, Idle: `CentralBall` anchoredPosition.y = −304; 3D ball renders centred on the widget (screenshot with the widget outline).
- [ ] Same view: horizon visible; pitch of the aim camera reported (before/after degrees from `SolveAimCameraPose`).
- [ ] Pendulum 120 % tick at y −1022 ± 2; lane end at −1092 ± 2; `GolfinButton`/`DriverButton` bottom edge at −1096 (baseline 170); `SpinButton`/`FadeDrawButton` at 434; selector overlays open at y 170.
- [ ] Free Swing: same lane numbers; impact line 160 above the ball as before.
- [ ] Needle: rings r80/r100/r120 = 374/450/526 unchanged; 120 % ring fully inside the canvas width; tap catcher still covers the arc.
- [ ] Flick: `ConeMesh`, `ClubHandle`, `TimingSlab`, `PutterTrack` world corners identical before/after (numbers quoted). Flick parity test files untouched (`git diff --stat` quoted).
- [ ] Switching scheme in the in-game gear modal at Idle re-frames the camera with no visible pop; switching mid-swing defers to Idle as before.
- [ ] `PowerHUD` top-right: centre at 30 % from the top (y −660 for the pivot), no overlap with the hole card or the aim bar in any of the four schemes.
- [ ] Device (Cesar, manual): 120 % pull on iPhone does not trigger the home gesture; buttons reachable; framing matches Figma 14153:4602 by eye.
- [ ] Editor Game View 16:9 preset: lane end sits on the baseline (ball auto-raised, D6), no overlap with buttons.
- [ ] `ShotLayoutMathTests` green; full EditMode run count quoted; no new test file other than the one above.
- [ ] Confirm tiles re-captured for Pendulum / Needle / Free Swing (paths listed).
- [ ] Unity Console has no errors related to this task; all new `[SerializeField]`s wired (builder 8.5 re-run once, said so).
- [ ] Spec deviations flagged at the bottom of the report with justification.

## 5. Out of scope (rows added to `Docs/GPS/GPS_BACKLOG.md` this session)

- Debug sliders for ball anchor / baseline / gauge (D8 — csv tuning instead).
- Flick at the shared 0.38 anchor with a shortened cone and 120 % overpower on the cone (Flick has no 120 % today; `ClubHandleDragger` clamps power at the cone base).
- Cone base vs baseline: with Flick at 0.5 the cone base (−1160) sits 64 px below the new button bottoms. Accepted (D1); realign only if Flick's anchor moves.
- Tablet framing: D6 keeps the lane playable on 4:3 but the ball sits above centre; a per-aspect anchor table is a later item.
- Figma scheme frames (`CONTROL_SCHEMES_PLAN` §8) redrawn at the new geometry.
- Putt camera (`_puttCamDistanceM/HeightM`) — same viewport pin, own distance; unchanged here.

## 6. Files / hierarchy this task touches

- `Assets/Scripts/Gameplay/Config/ControlsConfig.cs`, `ControlsConfigLoader.cs`, `Assets/Resources/Gameplay/controls.csv`
- NEW `Assets/Scripts/Gameplay/UI/ShotUI/ShotLayoutController.cs` (+ `ShotLayoutMath` static)
- `Assets/Scripts/Gameplay/UI/ShotUI/Controls/ShotSchemeHost.cs` (call layout before `Activate`)
- `Assets/Scripts/Gameplay/UI/ShotUI/SelectorOverlayWidget.cs` (baseline setter)
- `Assets/Scripts/Editor/CanvasScalerMigration/ActionButtonsBuilder.cs` (wiring + comment)
- `Assets/Scenes/Physics/LabScaffold.unity` (`BallSpace` × 4, re-parenting, `PowerHUD` anchors, controller component)
- NEW `Assets/Scripts/Gameplay/Tests/ShotLayoutMathTests.cs`; fixture updates in `PendulumMathTests`, `FreeSwingMathTests`, `PendulumSchemeDriverTests`, `FreeSwingSchemeDriverTests`
- Re-captured confirm tiles (Pendulum / Needle / Free Swing)
- `Docs/AI_CONTEXT.md`, `Docs/CONTROL_SCHEMES_PLAN.md` (one line)
