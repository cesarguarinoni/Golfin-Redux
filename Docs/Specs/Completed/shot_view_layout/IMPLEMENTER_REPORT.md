# Implementer Report — `shot_view_layout`

## Implementation summary

The shot view is framed by where the 2D ball widget sits: the aim camera pins the 3D ball to
`CentralBall` (`PhysicsLabController.GetAimBallViewportY` → `SolveAimCameraPose`), so moving the
widget IS the camera change and nothing under `Assets/Scripts/Physics/` was touched. A new
`ShotLayoutController` (+ pure `ShotLayoutMath`) runs from `ShotSchemeHost.Apply`, immediately
before `driver.Activate()`, reusing the host's existing never-mid-swing deferral rather than adding
a second Idle gate. It places the per-scheme ball anchor, all four `BallSpace` rects, the
action-button cluster, both selector overlays and the power gauge.

Each `SchemeRoot_*` gained one full-stretch `BallSpace` between it and its ball-relative children,
so moving a scheme is a single write instead of re-deriving every offset in four builders. The
Pendulum and Free Swing pulls grew 380/456 → 540/648 (exact 1.2× kept); Needle deliberately kept
380/456 (D5).

## Files modified or created

| Path | Change |
|---|---|
| `Assets/Scripts/Gameplay/Config/ControlsConfig.cs` | modified — 6 new layout fields + `Default` seeds; Pendulum/FreeSwing `Pull100Px`/`Pull120Px` 380/456 → 540/648 |
| `Assets/Scripts/Gameplay/Config/ControlsConfigLoader.cs` | modified — 6 new switch cases |
| `Assets/Resources/Gameplay/controls.csv` | modified — 6 new rows, 4 retuned rows, confirm-tile header note extended |
| `Assets/Scripts/Gameplay/UI/ShotUI/ShotLayoutMath.cs` | **created** — pure framing arithmetic (anchor, baseline, D6 clamp, lane end, gauge y) |
| `Assets/Scripts/Gameplay/UI/ShotUI/ShotLayoutController.cs` | **created** — the MonoBehaviour on `ShotUI_Canvas` that applies it |
| `Assets/Scripts/Gameplay/UI/ShotUI/Controls/ShotSchemeHost.cs` | modified — one call to the layout controller inside `Apply`, before `Activate` |
| `Assets/Scripts/Gameplay/UI/ShotUI/SelectorOverlayWidget.cs` | modified — `SetOpenBaselineY(float)` setter |
| `Assets/Scripts/Gameplay/UI/ShotUI/Controls/Pendulum/PendulumLaneView.cs` | modified — expose `ClubHalfHeight` / `LaneTailPx` for the D6 clamp |
| `Assets/Scripts/Gameplay/UI/ShotUI/Controls/FreeSwing/FreeSwingLaneView.cs` | modified — same two properties |
| `Assets/Editor/ShotUI/ShotBallSpace.cs` | **created** — the one place that creates/configures a `BallSpace`; used by the three scheme builders and the scene migration |
| `Assets/Editor/ShotUI/PendulumSchemeBuilder.cs` | modified — builds under `BallSpace`; `_schemeRoot` still wired to the ROOT |
| `Assets/Editor/ShotUI/NeedleSchemeBuilder.cs` | modified — same |
| `Assets/Editor/ShotUI/FreeSwingSchemeBuilder.cs` | modified — same |
| `Assets/Scripts/Editor/CanvasScalerMigration/ActionButtonsBuilder.cs` | modified — CONFIG SNAPSHOT note + `WireShotLayoutController` (wires 8 refs + 4 `BallSpace`s, creating any that are missing) |
| `Assets/Scenes/Physics/LabScaffold.unity` | modified — 4 `BallSpace` rects + re-parenting, `PowerHUD` re-anchored top-right, `ShotLayoutController` added and wired, cluster/selectors/spin panel rebuilt by the 8.5 re-run |
| `Assets/Scripts/Gameplay/Tests/ShotLayoutMathTests.cs` | **created** — 9 tests, SPEC §3.6 |
| `Assets/Scripts/Gameplay/Tests/FreeSwingMathTests.cs` | modified — the day-one "equal to Needle" claim replaced by the deliberate D5 divergence |
| `Docs/AI_CONTEXT.md` | modified — layout paragraph |
| `Docs/CONTROL_SCHEMES_PLAN.md` | modified — one-line "§8 geometry is stale for Pendulum/Free Swing" note |

## Screenshot

- **Canonical screenshot:** `screenshots/ab_flick_vs_pendulum.png` (2380 × 2532) — Flick at viewport
  0.500 beside Pendulum at **0.3800**, both on Lomond hole 2, each annotated with the measured ball
  row (red), the shared bottom baseline (yellow) and the measured horizon (cyan). The red box is
  `CentralBall`'s own 150×150 rect drawn at the y this task computed; in both frames it lands on the
  ball, which is the 1:1 check that the widget and the 3D ball agree.
- Also: `screenshots/pendulum_038_hole2.png`, `screenshots/flick_050_hole2.png` (raw 1170 × 2532),
  `screenshots/pendulum_hole2_1170x2532.png` (the earlier 0.418 framing, kept for the A/B in
  § What the clamp guards), `screenshots/confirm_tiles_recapture_clipped.png` and
  `screenshots/confirm_tile_pendulum2_before_after.png` (the reverted tile re-capture).
- **Scene loaded:** `Assets/Scenes/Physics/LabScaffold.unity` + `Hole_02_Geo` additive
- **Play mode:** Yes. **Hole loaded:** Lomond hole 2 (Cesar's own reference hole for this task).
- Capture path: `GOLFIN ▸ Screenshot ▸ Capture Game View` at 1170 × 2532.
  `CaptureCore.SnapPlayModeSafe` was tried first and returned the 1740 × 1598 **editor window**
  rather than the game RT — worth knowing. The menu capture also writes the last RENDERED frame, so
  a capture issued in the same call as a scheme switch returns the pre-switch image: it caught me
  twice, both times found by pixel-diffing the pair rather than trusting the filenames. Switch,
  then capture in a LATER call.

## Measured numbers (canvas px, origin = canvas centre, 1170 × 2532)

Everything below was read off live rects in play mode, not computed twice.

| Thing | Measured | Spec target | Note |
|---|---|---|---|
| Baseline | 170 px → canvas y **−1096** | 170 / −1096 | `safeAreaBottom` is 0 in the Editor, so the authored value wins |
| `GolfinButton` / `DriverButton` bottom | **−1096** | −1096 | on the baseline |
| `SpinButton` / `FadeDrawButton` bottom | **−832** (434 above canvas bottom) | 434 | row gap 264 kept |
| Selector overlays open y | **170** (both) | 170 | see § Spec deviations — they were 28 and 96, not 96 and 96 |
| `PowerHUD` anchoredPosition | **(−48, −659.6)**, anchors (1,1) | (−48, −660), (1,1) | centre at canvas y 506.4 = viewport **0.7000** |
| Flick ball y | **0** (vy 0.500) | 0 | D1 |
| Needle ball y | **−303.84** (vy **0.3800**) | −304 | no lane ⇒ no clamp ⇒ lands on the Figma anchor |
| Pendulum ball y | **−303.84** (vy **0.3800**) | −304 (0.38) | anchor wins; the clamp has 74 px of slack |
| Free Swing ball y | **−303.84** (vy 0.3800) | −304 | same |
| Pendulum/Free Swing 120 % handle | **−1021.84** | −1022 ± 2 | 74 px above the baseline, 244 px above the screen edge — D3's own number |
| Pendulum/Free Swing 100 % tick | **−913.84** | — | ball − (70 + 540) |
| Lane end (both) | **−1191.84** | −1092 ± 2 | at the SPEC's `ClubHalfHeight` 50 it is −1091.84 = the spec's number exactly; the live 150 hangs the tail a further 100 px, to 74 px above the screen edge |
| Pendulum `LaneHeight` | 888 = 718 + 150 + 20 | 788 | `ClubHalfHeight` is 150, not the spec's 50 |
| Free Swing `LaneHeight` | 1048 = 160 + 718 + 150 + 20 | — | impact line still ball + 160 |
| Needle rings r80/r100/r120 | **375.5 / 452 / 527.5** | 374 / 450 / 526 | unchanged (D5); measured off the drawn graphic, which includes its stroke. r120 < 585 ⇒ fits the canvas width |
| Aim camera pitch, Lomond hole 2 | Flick **12.500°** → Pendulum **4.612°** | — | same camera position (122.05, 15.40, −134.25); the ball drop pitches the camera up 7.89° |
| Horizon, Lomond hole 2 | Flick **25.5 %** → Pendulum **37.8 %** from the top | ~38 % | measured as the sky/turf boundary in a 40–300 px left band of each 1170×2532 frame, not eyeballed |

## What the clamp guards, and why it changed

**`ClubHalfHeight` went 50 → 150 earlier today** (the control-scheme polish pass: the club head now
lerps to 3× scale, so the lane has to contain a 300 px-tall head). SPEC §D6's formula clamps on the
**lane's rounded end**, so at 150 the lane read as 100 px deeper and the ball was pushed up to
viewport 0.418 — 96 px above the Figma anchor, and a measured 34.0 % horizon against the ~38 %
target.

SPEC §D3 already says which end matters: *"The 120 % handle position is the thing that must clear
the home-gesture zone (the flick starts there), not the lane's rounded end."* D6's formula was
written before that was settled. The clamp now guards the **handle centre** — where the finger is —
rather than the pill's tail:

```
BallYForLane = canvasBottom + baseline + handleRestBelowBall + pull120Px
```

`ClubHalfHeight` and `LaneTailPx` describe only how far the drawn pill extends *past* the finger, so
the club head growing below it can no longer cost framing. Cesar approved this on 2026-09-07
("do option 4").

What it bought, all measured on Lomond hole 2 at 1170 × 2532:

| | before (lane-end clamp) | after (handle clamp) |
|---|---|---|
| Ball | −208, viewport 0.418 | **−303.84, viewport 0.3800** |
| 120 % handle | −926 | **−1021.84** — D3's own predicted number |
| Handle above screen edge | 340 px | **244 px** — again exactly D3's figure |
| Handle above the baseline | 170 px | 74 px |
| Aim camera pitch | 7.082° | **4.612°** (Flick is 12.500°) |
| Horizon | 34.0 % from the top | **37.8 %** (target ~38 %) |

The cost is the lane's tail: at the live `ClubHalfHeight` of 150 it now hangs 96 px below the
baseline, ending at −1191.84 — still 74 px above the screen edge. It is 120 px wide down the centre
of the canvas and the action buttons live at x ±382…527, so it reaches neither. (At the
`ClubHalfHeight` of 50 the spec was written against, the tail lands at −1091.84, i.e. the spec's own
−1092.)

`ShotLayoutMathTests.TheClampIgnoresTheClubHeadSize_SoScalingTheHeadCannotCostFraming` pins the
whole argument in one test, so a future club-head resize cannot quietly take the framing back.

## Flick parity (control_scheme_seam / D1)

World corners in canvas-local space, before and after the `BallSpace` migration. Identical to the
printed precision on every rect the spec names, and the migration itself reported
`maxCornerDelta = 0.000000` for all 17 re-parented children across the four roots.

| Rect | Before | After |
|---|---|---|
| `ConeRoot` | BL(−585, −1266) TR(585, 1266) | BL(−585, −1266) TR(585, 1266) |
| `ConeMesh` | (0, −1160) zero-size | (0, −1160) zero-size |
| `TimingSlab` | BL(−200, −1160) TR(200, −151) | BL(−200, −1160) TR(200, −151) |
| `ClubHandle` | BL(−89, −1160) TR(89, −1060) | BL(−89, −1160) TR(89, −1060) |
| `TargetingLine` | BL(−1.5, 0) TR(1.5, 200) | BL(−1.5, 0) TR(1.5, 200) |
| `PutterTrack` | BL(−70, −1187) TR(70, −187) | BL(−70, −1187) TR(70, −187) |

Flick's parity test files are untouched:

```
$ git diff --stat HEAD -- Assets/Scripts/Gameplay/Tests/ShotAimParityTests.cs \
      Assets/Scripts/Gameplay/Tests/ShotTimingPowerTests.cs \
      Assets/Scripts/Gameplay/Tests/ShotControllerFlickGateTests.cs \
      Assets/Scripts/Gameplay/Tests/MapViewAimingTests.cs
(no output — none of the four is modified)
```

`AimCameraFramingTests.cs` lives under `Assets/Scripts/Physics/Tests/` rather than the Gameplay
test folder; it is likewise unmodified and green in the full run below.

## Acceptance checklist (SPEC §4)

| Item | Result | Justification |
|---|---|---|
| Game View 1170×2532, Pendulum, Idle: `CentralBall.anchoredPosition.y = −304`; 3D ball centred on the widget | PASS | y = **−303.84** = viewport **0.3800** exactly. Widget/ball agreement verified in `screenshots/ab_flick_vs_pendulum.png`: the widget's own 150×150 rect, drawn at the computed y, lands on the ball in both schemes, and `GetAimBallViewportY()` returns 0.38 — the camera reading the widget back |
| Horizon visible; aim-camera pitch before/after reported | PASS | Lomond hole 2, same camera position (122.05, 15.40, −134.25): Flick **12.500°** → Pendulum **4.612°**. Horizon measured as the sky/turf boundary in a 40–300 px left band: **25.5 % → 37.8 %** from the top, against the Figma ~38 % |
| 120 % tick −1022 ± 2; lane end −1092 ± 2; `GolfinButton`/`DriverButton` bottom −1096; `SpinButton`/`FadeDrawButton` 434; overlays open at 170 | PASS | 120 % handle **−1021.84** ✓, buttons **−1096** ✓, top row **434** ✓, overlays **170** ✓. Lane end is **−1191.84** rather than −1092 — that difference is entirely the live `ClubHalfHeight` of 150 against the spec's 50; at 50 the same ball position puts it at −1091.84. See § What the clamp guards |
| Free Swing: same lane numbers; impact line 160 above the ball | PASS | ball −303.84 and 120 % handle −1021.84, identical to Pendulum (`FreeSwing_ResolvesToTheSameBallAndLaneEndAsThePendulum` asserts it). `LaneHeight` 1048 = 160 + 718 + 150 + 20, so the impact line is still at ball + 160 by derivation |
| Needle: rings 374/450/526 unchanged; 120 % ring inside the canvas width; tap catcher still covers the arc | PASS | r80/r100/r120 measured **375.5 / 452 / 527.5** off the drawn graphics (the +1.5 is the stroke); `NeedlePull*` untouched. r120 527.5 < 585 half-width. Tap catcher spans x ±537, y −738.8…657.2 around a ball at −303.8, the same ±(ball) coverage it had |
| Flick: `ConeMesh`, `ClubHandle`, `TimingSlab`, `PutterTrack` world corners identical; parity test files untouched | PASS | Table above; `maxCornerDelta = 0.000000` on all 17 re-parented rects; `git diff --stat` on the four parity test files is empty |
| Switching scheme in the gear modal at Idle re-frames with no pop; mid-swing defers | PASS | Driven through `ControlSchemeService.Set` — the same authority the gear modal writes — with `ShotController.State = Idle`; ball, all four `BallSpace`s and the camera all moved within the one call. The mid-swing path is untouched: the layout call sits INSIDE `ShotSchemeHost.Apply`, which is only reached after the host's existing Idle check |
| `PowerHUD` top-right, centre at 30 % from the top, no overlap with the hole card or the aim bar | PASS | anchors (1,1), ap (−48, −659.6), centre canvas y 506.4 = viewport 0.7000. Right edge x = 537 = 585 − 48, margin unchanged. `HoleCard` (anchors and pivot (1,1), ap (−48, −158), 478×180) spans canvas y 928…1108, so its bottom edge clears the gauge's top edge (606.4) by 321.6 px; the Pendulum aim bar sits at −80, 686 px below the gauge's centre |
| Device (Cesar, manual): 120 % pull does not trigger the home gesture; buttons reachable; framing matches Figma by eye | **NEEDS CESAR** | Not verifiable from the Editor. The 120 % handle rests at canvas y −1021.84 = **244 px above the screen edge (≈ 81 pt)**, which is the clearance D3 specified. Worth a look at the lane's tail too: it now ends 74 px above the edge, down the middle of the screen |
| Editor Game View 16:9: lane end on the baseline, ball auto-raised, no button overlap | PASS | Pinned by `ShotLayoutMathTests.OnSixteenByNine_TheBallIsRaisedSoTheFlickLandsExactlyOnTheBaseline`: at canvas height 2080 the clamp beats the anchor (ball −152 against an anchor of −249.6) and the 120 % handle lands on −870 = −1040 + 170 exactly. 4:3 (1560) is covered too: ball +108, handle on −610 |
| `ShotLayoutMathTests` green; full EditMode run count quoted; no new test file other than that one | PASS | **2724 tests, 2721 passed, 0 failed, 3 skipped** (the 3 skips are pre-existing `HoleCompleteDriverTests` Stage-C1 skips). One new test file, 10 tests |
| Confirm tiles re-captured for Pendulum / Needle / Free Swing | **FAIL — reverted deliberately** | `GOLFIN ▸ Capture ▸ Scheme Confirm Tiles` was run and produced all 12 tiles. The new Pendulum 1/2 crops cut the timing marker bar off the top and clip the 100 %/120 % labels; Needle 1 loses the top of its ring. The auto-crop is width-driven and clamped at `MaxCropW = 900` (a guard that keeps the HUD columns out of the tile), and the longer lane plus the 3× club head no longer fit inside it. I reverted all 12 rather than ship tiles that explain the control worse than the ones on disk. Evidence: `screenshots/confirm_tiles_recapture_clipped.png`, `screenshots/confirm_tile_pendulum2_before_after.png`. Flick tiles were byte-restored regardless, per D1 |
| Console has no errors related to this task; all new `[SerializeField]`s wired; builder 8.5 re-run once | PASS | 8.5 re-run once, in the session that produced the committed scene. All 8 object refs + 4 `BallSpace` entries read back non-null from the live `SerializedObject`. Compilation is clean — warnings only, all pre-existing (obsolete `FindObjectsOfType`, nullable analysis) |
| Spec deviations flagged | PASS | Below |

## Known FAIL items

1. **Confirm tiles not re-captured** (reverted — reasoning above). Unblocking it is a decision about
   what the tile should frame now that the lane is 888 px tall: raise `MaxCropW` past 900 and accept
   HUD columns in the tile, crop on the lane alone, or shrink the subject. Yours to pick.
2. ~~Ball at 0.418 rather than 0.38~~ — **resolved 2026-09-07**, Cesar picked option 4: the clamp
   now guards the 120 % handle rather than the lane's end, per D3. Ball is back on viewport 0.3800
   and the horizon measures 37.8 %.

## Spec deviations

- **§3.2 authored `BallSpace` offset.** The spec says both "preserving their current
  `anchoredPosition` so nothing moves until `ShotLayoutController` sets `BallSpace.y`" and
  "`anchoredPosition` = the scheme's ball anchor". I took the first: all four are authored at **0**,
  so a scene without the controller is byte-identical to today for every scheme, and the controller
  is the only thing that applies the new framing. Flick's guarantee then holds at the YAML level and
  not just at runtime.
- **§3.2 Flick's `ConeRoot`.** Took the stretch option: every `BallSpace` is full-stretch
  (0,0)–(1,1) with `anchoredPosition` as a pure offset, uniformly across the four roots. That keeps
  `ConeRoot` (itself full-stretch) and `PutterTrack` (anchored to the canvas TOP) both unchanged;
  a zero-sized container would have collapsed the first and re-based the second.
- **§3.4 scheme builders.** The spec asked whether the scheme roots are hand-authored — they are;
  the three scheme builders only find them. But those builders own and wipe every CHILD of the root,
  so a re-run would have destroyed the `BallSpace` and dangled the controller's reference. Each now
  builds under `ShotBallSpace.EnsureAndClear(schemeRoot)`, which empties the existing rect instead of
  replacing it. `_schemeRoot` stays wired to the ROOT: the drivers use it only to convert a pointer
  into a stable local space for deltas, and a rect that moves under the finger would not be that.
- **§3.6 test literals.** There are none. `PendulumMathTests`, `FreeSwingMathTests`,
  `PendulumSchemeDriverTests` and `FreeSwingSchemeDriverTests` read every threshold off
  `ControlsConfig.Default` (their own header calls literals out as forbidden), so retuning `Default`
  moved them for free. The one real edit: `FreeSwingMathTests.Power_SeededEqualToPendulumAndNeedle`
  asserted `NeedlePull120Px == FreeSwingPull120Px`, which D5 deliberately breaks. Its own comment
  says changing it is the correct response to a deliberate divergence; it now asserts the surviving
  Pendulum↔FreeSwing equality and asserts the Needle divergence explicitly, with the D5 reason.
- **§2 selector overlays.** The spec has them both at 96 today. They are not: the builder authors
  the club overlay at **28** and the ball overlay at **96**. D2 says both sit on the baseline, so
  both are now set to 170 — which also collapses a 68 px asymmetry nobody documented. If that offset
  was deliberate, say so and it becomes a per-overlay offset instead of a shared y.
- **§3.6 / D6 — the clamp guards the handle, not the lane end.** SPEC D6's formula clamps on the
  lane's rounded end; SPEC D3 says the 120 % HANDLE is the thing that must clear the home-gesture
  zone. Those disagree by the club's lower half plus the pill's tail, which on the current 3× club
  head is 170 px — enough to cost 96 px of ball position and ~4 points of horizon.
  `ShotLayoutMath.BallYForLane` now takes D3's reading; Cesar approved it explicitly. The §3.6 test
  expectations moved with it: the 16:9 and 4:3 cases assert the HANDLE on the baseline rather than
  the lane end, and one test was added.
- **§3.3 camera re-pose.** `ApplyCameraYaw` re-reads the widget every call but nothing calls it while
  the player is standing at address (`HandleCameraOrbit` only reaches it on a drag), so a scheme
  switch would have moved the 2D ball and left the 3D one behind. `MapViewController.WriteBackAimToPhysicsLab`
  hit the identical problem at iter-35 and solved it by invoking `ApplyCameraYaw` through reflection
  to stay out of `Assets/Scripts/Physics/`. `ShotLayoutController.NudgeAimCamera` is that idiom
  verbatim, and the measured 12.500° → 7.082° is it working.
- **§3.2 `PhysicsLab_Hole1.unity`.** Not edited, and the Architect's "believed legacy" checks out:
  it is absent from `EditorBuildSettings.asset` (zero matches), was last committed 2026-06-12
  (`2fb4c2b71`, three months ago), and the only live references to it are editor tooling
  (`PhysicsLabZoneMeshBaker`, `CanvasScalerMigrationTool`) plus a comment in `PhysicsLabController`
  that literally reads "Legacy Hole1 hardcoded direction". It does carry its own `ShotUI_Canvas`
  fragment, so if it is ever revived it will need the same `BallSpace` migration —
  `ShotBallSpace.Ensure` plus a re-run of `ActionButtonsBuilder` is the whole of it.

## Editor state

Play mode exited; `LabScaffold.unity` open and not dirty; `Hole_02_Geo` closed; no scratch objects
left in the scene. The runtime-spawned `[ObGroundSkirt]` GameObject and its `ObGroundSkirt`
component were stripped before each save — the first save baked them in, which is visible as a
535-line drop between the first scene diff and the committed one.
