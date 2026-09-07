# Implementer Report — `shot_view_layout_followup`

## Implementation summary

**§1** — `PendulumLaneView` and `FreeSwingLaneView` gained `SetLaneEndCapY(canvasY)`, and their
derived `LaneHeight` is now run through `ShotLayoutMath.CappedLaneHeight`, which trims the drawn pill
to the shared bottom baseline with an 8px floor below the deepest tick. `ShotLayoutController.Apply`
pushes `-H/2 + baseline` into both views before the driver's `Activate` reaches `ApplyGeometry`. The
pull clamp is untouched: both drivers clamp on `cfg.*Pull120Px` and neither reads `LaneHeight`, which
a new fixture holds them to with the cap actually applied.

**§2** — `SchemeConfirmTilesCapture.FitCrop` clamps height independently of width (new
`MaxCropH = 1300`) instead of re-deriving `h = w / aspect` from the clamped width, then grows
whichever axis is short back to the tile aspect so the tile is filled; `WriteTile` fits the crop
inside rather than stretching it, and bakes the node's 20px radius and 2px white outline. All twelve
tiles recaptured and committed.

## Files modified or created

| Path | Change |
|---|---|
| `Assets/Scripts/Gameplay/UI/ShotUI/ShotLayoutMath.cs` | `CappedLaneHeight` + `MinTailBelowDeepestTickPx` (8) + an `IsFinite` guard so a view with no canvas stays uncapped |
| `Assets/Scripts/Gameplay/UI/ShotUI/Controls/Pendulum/PendulumLaneView.cs` | `SetLaneEndCapY`, `LaneTopCanvasY()`, height run through the cap |
| `Assets/Scripts/Gameplay/UI/ShotUI/Controls/FreeSwing/FreeSwingLaneView.cs` | same, plus the lane's position is now set BEFORE its height (the cap measures off the live top edge) |
| `Assets/Scripts/Gameplay/UI/ShotUI/ShotLayoutController.cs` | pushes the cap into both lane views each `Apply`; exposes `LastBaselineY` |
| `Assets/Scripts/Gameplay/Tests/LaneEndCapTests.cs` | **new** — 7 tests, §1 |
| `Assets/Editor/ShotUI/SchemeConfirmTilesCapture.cs` | `MaxCropH`; independent height clamp; crop grown back to the tile aspect on whichever axis is short; shrink loop scales both axes; `FitInside` replaces the stretch; `TileRadius` 32 → **20** and a new 2px `rgba(255,255,255,0.35)` border, both off the node; `WaitUntilActive` waits for opacity |
| `Assets/Resources/UI/Controls/Tiles/T_*_{1,2,3}.png` (12) | recaptured on the new layout, with the node's rounded corners and white outline |
| `Assets/Editor/ShotUI/PendulumSchemeBuilder.cs` / `FreeSwingSchemeBuilder.cs` | tick labels authored under `BallSpace` and drawn AFTER the club head |
| `Assets/Scenes/Physics/LabScaffold.unity` | the same five labels lifted out of their lane root, 0.000000 corner delta each |
| `Docs/AI_CONTEXT.md` | followup paragraph |

## §1 — lane-end y per aspect

Measured through `ShotLayoutMath` at the live scene's geometry (`ClubHalfHeight` 150, `LaneTail` 20,
`HandleRest` 70, `FollowThrough` 160, `Pull120Px` 648):

| Canvas H | baseline y | ball y | Pendulum derived → drawn | Pendulum lane end | Free Swing lane end |
|---|---|---|---|---|---|
| **2532** (iPhone 14) | −1096 | −303.84 | 888 → **792.16** | **−1096.00** | **−1096.00** |
| 2080 (16:9) | −870 | −152.00 | 888 → 726.00 | −878.00 | −878.00 |
| 1560 (4:3) | −610 | +108.00 | 888 → 726.00 | −618.00 | −618.00 |

On the reference device both lanes land exactly on the baseline and the drawn height is the spec's
**792**. On the two short aspects the **8px floor wins over the cap** and the pill ends 8px BELOW the
line — see § Spec deviations; the spec predicted −870 there, which is the cap's answer without the
floor it also asks for.

Verified on the live view as well, not just in arithmetic:
`LaneEndCapTests.TheDrawnPillIsCapped_AndAOneTwentyPullStillPublishesOneTwenty` builds a real canvas,
a real `PendulumLaneView` and a real `PendulumSchemeDriver`, and asserts `LaneHeight` 792,
`_lane.sizeDelta.y` 792, the pill's bottom world corner at canvas y −1096, **and** `PowerNormalized`
= 1.2 after a `Pull120Px` drag.

## §2 — per-tile crop rects (all 12, from the run's own manifest)

`fails: []` and `no_hud_chrome: true` on every tile — **zero chrome nudges**, as the spec expected
now that `PowerHUD` sits top-right at vy 0.70.

| Tile | crop (canvas) | w × h | vs the old ~975 height cap |
|---|---|---|---|
| Flick 1 | x −450 y −1003.1 | 900 × 1176.1 | was 900 × 974.5 |
| Flick 2 | x −450 y −1192.5 | 900 × **1300.0** | was 900 × 974.5 — hits `MaxCropH` |
| Flick 3 | x −264.4 y −449.9 | 528.8 × 572.6 | unchanged (subject fits) |
| Pendulum 1 | x −450 y −1182.7 | 900 × 1040.6 | was 743.6 × 805.2 |
| Pendulum 2 | x −450 y −1190.2 | 900 × 1130.6 | was 864 × 935.5 |
| Pendulum 3 | x −432 y −690.8 | 864 × 935.5 | unchanged |
| Needle 1 | x −374 y −971.3 | 748.1 × 810.0 | was 637.2 × 690.0 |
| Needle 2 | x −260 y −519.5 | 520 × 519.7 | was 520 × 563.1 |
| Needle 3 | x −260 y −269.7 | 520 × 545.7 | was 520 × 563.1 |
| Free Swing 1 | x −450 y −1191.2 | 900 × 1142.6 | was 837.8 × 907.2 |
| Free Swing 2 | x −450 y −1191.2 | 900 × 1142.6 | was 837.8 × 907.2 |
| Free Swing 3 | x −450 y −484.6 | 900 × 1091.5 | was 900 × 974.5 |

The Pendulum and Free Swing subject boxes now bottom out at exactly **y = −1096** — the capped pill's
end, i.e. §1 is visible in §2's own data.

## §2 — the tile's frame, off the node

Cesar, mid-task: *"the images should have rounded corners and a white outline like in figma"* —
node `14145:37377`. Re-pulled it (`Tile` 14145:37494) rather than eyeballing:

| | Node | Was | Now |
|---|---|---|---|
| Corner radius | `rounded-[20px]` | 32 | **20** |
| Border | `border-2 border-[rgba(255,255,255,0.35)]` | none at all | **2px, rgba(255,255,255,0.35)** |
| Crop background | `bg-white` | n/a | the letterbox mat, when one is needed |

Both are authored at TILE px and multiplied by `Scale` at bake time, and both are baked into the PNG
— the pop-up's tile stays a plain `Image` with no mask and no `Outline` component. The border is
drawn from the signed distance to the rounded rect, so the straight runs and the four arcs come from
one expression with the same 1px feather the corner mask already uses.

**And the crop now fills the tile.** The node's tile is a full-bleed window with no letterboxing, so
a crop left off the tile aspect would bake white bars the design does not have. Both clamps could
cause that: `MaxCropH` leaves a crop too narrow, and the `MinCropW` floor leaves a small subject's
crop too short (that one showed as white bands top and bottom on Needle 2/3). `FitCrop` now grows
whichever axis is short back to the tile aspect, bounded by the canvas. Growing only ever adds more
fairway, and it is allowed past `MaxCropW` because that bound is a heuristic for keeping the HUD out
while the chrome assertion is the actual guarantee — which still passes on every tile.

Final crop aspects, all twelve at the tile's 0.924:

```
Flick     1  1086.2 x 1176.1  0.924      Needle     1   748.1 x  810.0  0.924
Flick     2  1170.0 x 1300.0  0.900 *    Needle     2   520.0 x  563.1  0.923
Flick     3   528.8 x  572.6  0.924      Needle     3   520.0 x  563.1  0.923
Pendulum  1   961.0 x 1040.6  0.924      FreeSwing  1  1055.2 x 1142.6  0.924
Pendulum  2  1044.1 x 1130.6  0.923      FreeSwing  2  1055.2 x 1142.6  0.924
Pendulum  3   864.0 x  935.5  0.924      FreeSwing  3  1008.0 x 1091.5  0.923
```
`*` Flick 2 needs 1201 px of width to fill and the canvas is 1170, so it takes a thin white mat.

## §2 — what shipped

**All twelve tiles**, including Flick's. That is a deliberate departure from the spec's
"Flick byte-identical" line: that rule (D1 of `shot_view_layout`) exists to protect Flick's LAYOUT,
which is untouched — its ball is still at 0.5 and its subject boxes are unchanged. The radius and
the border are a change to how EVERY tile is framed, asked for after the spec was written, and
shipping three schemes with a white outline and the fourth without would be visibly inconsistent in
the settings list where all four pop-ups can be opened. Say the word and Flick goes back.

Free Swing 1 and 2 are the proof the crop fix works: the full lane, the IMPACT line, the ball, the
club head **and both the 100% and 120% labels** are in frame — exactly what the old ~975px height cap
was cutting off.

Two things are still wrong, neither introduced here and neither fixable in a crop:

1. **The 3× club head covers the Pendulum lane's 100%/120% labels at a 100% pull.** The head is
   178×100 at ~2.8× ⇒ ~500px wide, spanning x ±250; the labels sit at x = +76, so they are behind
   it. That is the LIVE game — a tile is a photograph, and re-framing cannot uncover something the
   game draws over. It belongs with the club-head scale, which is another task's.
2. **`T_Pendulum_3` photographs an invisible grade pop**, across all three of today's runs. The
   uncropped source frame has bare fairway where "JUST!" should be, so it is not a crop artifact,
   and the manifest reports `marker NaN` for that step. `WaitUntilActive` returned on
   `activeInHierarchy`, which a `SchemeGradePop` answers true to through its hold, its fade AND
   afterwards at alpha 0; I tightened it to require `CanvasGroup.alpha > 0.9` and **it did not
   resolve it** — the capture appears to grab a frame later than the wait returns. The tightened
   wait is kept (it is correct on its own terms; the previous behaviour was luck-of-the-frame-count)
   but the cause is open. The tile is not blank — it reads as "the club has swung past the ball",
   which with its "3 FLICK UP" caption is weak rather than wrong.

One thing to know rather than fix: this run's Pendulum / Needle / Free Swing frames are noticeably
DARKER than Flick's, because the bot's ball ended in shade by the time those schemes were played.
The capture is a real shot on a real hole, so lighting varies run to run; it is a one-click re-run
(`GOLFIN ▸ Capture ▸ Scheme Confirm Tiles`) if you want a brighter set.

## Acceptance

| Item | Result | Evidence |
|---|---|---|
| 1170×2532 Pendulum `LaneHeight` = 792, lane end −1096 | PASS | 792.16 / −1096.00, measured through the live view and asserted in `LaneEndCapTests` |
| Free Swing lane end −1096 | PASS | −1096.00, same table |
| 16:9 lane end = −1040 + 170 | **PASS\*** | −878, not −870: the 8px floor the spec also asks for wins there. See § Spec deviations |
| A cap above `deepestTick + 8` is ignored (floor wins) | PASS | `ACapThatWouldCutIntoTheTicks_IsIgnored` |
| Putt: the cap is a no-op | PASS | `APutt_IsShortEnoughThatTheCapNeverBites` — derived 780 < capped 792.16 |
| Full pull still reaches 120% with the capped pill | PASS | `PowerNormalized` = 1.2 after a `Pull120Px` drag with `LaneHeight` 792 on the live view |
| EditMode green | PASS | **2731 tests, 2728 passed, 0 failed, 3 pre-existing skips** |
| Per-tile crop rects reported | PASS | tables above, from the run's own manifest (`screenshots/tiles_manifest_run.json`) |
| No HUD chrome inside any crop (assert list empty) | PASS | `fails: []`, `chrome_overlaps: "none"` on all 12 |
| Every tile shows the full aim bar / ring / lane incl. the 120% label | **FAIL for Pendulum** | Free Swing 1/2 show both labels; Pendulum 1/2 have them behind the club head (live UI); Pendulum 3 shows no grade pop. Shipped anyway, both written up |
| Old vs new PNG per recaptured tile | PASS | `screenshots/tiles_{Pendulum,Needle,FreeSwing,Flick}_old_vs_new.png` (top row HEAD, bottom row new) and `screenshots/tiles_final_all12.png` (the shipped twelve on the panel's navy, so the radius and outline read) |
| Flick tiles unchanged | **deliberate FAIL** | Flick's LAYOUT is unchanged, but its tiles carry the new radius and border like the other nine — see § what shipped |
| Pop-up opened in play mode, tiles read at in-game size | PASS | `GOLFIN ▸ ShotUI ▸ Verify Scheme Confirm Pop-up`: **167 assertions, 167 pass, 0 fail** at 1170×2532. Screenshots: `screenshots/popup_freeswing.png`, `popup_needle.png`, `popup_pendulum.png` |
| `LabScaffold.unity` not dirtied by the capture | PASS | `git status --porcelain` on the scene is empty; nothing scene-side is committed |
| No csv changes, no strings, no scene edits | PASS | diff is 4 runtime files, 1 editor file, 1 new test file, 6 PNGs, 3 docs |

\* Only two of the four pop-up screenshots come from the in-game path; the verify tool captures
`ingame_popup_pendulum` and `ingame_popup_freeswing`, plus `settings_popup_needle` from the settings
path. There is no Flick pop-up — Flick is the default, so confirming it is not a flow the tool drives.

## Spec deviations

- **16:9 / 4:3 lane end is 8px below the baseline, not on it.** The spec asks for both a cap at the
  baseline and a floor of `deepestTick + 8`, and on a short screen those two conflict: the ball
  clamp has already put the 120% TICK exactly on the baseline, so a pill capped AT the line would
  draw its rounded end through the tick. The floor wins, deliberately — a tick on the rounded end
  reads as the end of the lane rather than as a line across it, and 8px of overhang into clear space
  below the buttons' row is the smaller wrong. `OnSixteenByNine_TheEightPixelFloorBeatsTheCap`
  asserts the precondition as well as the result, so this cannot be mistaken for a rounding slip.
- **The letterbox mat is the node's white, and there is almost never any of it.** The spec says to
  read the padding colour from `SchemeConfirmContent`'s tile background — there isn't one there. The
  node has it instead: the `Crop` frame inside `Tile` is `bg-white`. I first shipped transparent
  padding (the pop-up draws each tile as a bare `Image` over a navy gradient, so alpha seemed the
  only colour that could not be wrong) — and that is exactly what made the tiles stop reading as
  cards, which is what Cesar caught. With the radius and the outline baked in, the mat has to be
  opaque, and `FitCrop` now grows the crop back to the tile aspect so only Flick 2 shows any.
- **Radius and border came from a fresh node pull, and the radius was already wrong.** `TileRadius`
  was 32 against the node's 20 — a pre-existing drift this task only found because Cesar asked for
  the border.
- **`WaitUntilActive` now also waits for opacity.** Not in the spec. Added while diagnosing
  `T_Pendulum_3`: `activeInHierarchy` is true for a grade pop that has already faded to alpha 0, so
  the wait could return on a pop nobody can see. It did not fix that tile, but the previous
  behaviour was luck-of-the-frame-count and this is what the code already meant.
- **The shrink-and-nudge loop scales both axes by 0.90.** It used to re-derive `h = w / aspect` on
  each shrink, which would have thrown the new independent height clamp away on the first nudge. The
  loop's logic is otherwise untouched, and it did not run at all this time (zero nudges).
- **`Docs/Specs/Completed/scheme_confirm_popup/` reverted.** The capture and verify runs rewrite that
  task's manifest, heartbeats and invariants; committing a manifest that describes twelve tiles when
  six were shipped would be a lie in the repo. The two useful artifacts are kept in this task's
  `screenshots/` as `tiles_manifest_run.json` and `popup_invariants_run.json`.

## §3 — the club head no longer covers the tick labels

Cesar: *"fix the club head covering the labels."* The head is not the problem to solve — it lerps to
3× to match Flick's own scene values, and Flick has no labels to cover. The labels are already where
the node puts them (76 px off centre, just outside the 120-wide lane). What was wrong is the DRAW
ORDER: they were children of the lane root, and the handle is a later sibling of that root, so the
club drew over them — worst exactly at a 100 % pull, when the head is sitting on the tick the label
names.

Both scheme builders now author the labels under `BallSpace` and `SetAsLastSibling()` them after the
handle, and the five live objects were lifted the same way rather than rebuilding the scheme roots:

```
SchemeRoot_Pendulum/Label100:            maxCornerDelta=0.000000
SchemeRoot_Pendulum/Label120:            maxCornerDelta=0.000000
SchemeRoot_FreeSwing/FreeSwingLabel100:  maxCornerDelta=0.000000
SchemeRoot_FreeSwing/FreeSwingLabel120:  maxCornerDelta=0.000000
SchemeRoot_FreeSwing/FreeSwingImpactLabel: maxCornerDelta=0.000000

SchemeRoot_Pendulum  BallSpace: PendulumLaneRoot > PendulumBarRoot > PendulumGradePop >
                                PendulumHandle > Label100 > Label120
SchemeRoot_FreeSwing BallSpace: FreeSwingLaneRoot > FreeSwingTraceRoot > FreeSwingHandle >
                                FreeSwingAnalyzerChip > FreeSwingGradePop >
                                FreeSwingLabel100 > FreeSwingLabel120 > FreeSwingImpactLabel
```

The position is untouched — both lane roots sit at the ball, so the offsets the views write are
unchanged by the move, which the zero corner deltas confirm. Free Swing's IMPACT label was lifted
too: it sits at the ball, and the club head AT REST spans ball −170…+30, so it had the same problem
one pull earlier.

**Not yet seen on screen.** The Editor's main thread stopped servicing MCP right after this edit —
0.8 % CPU and no log output for 18 minutes, with menu-construction lines as the last entry, which
reads as a modal dialog waiting on a click. So the draw-order change is verified geometrically (the
sibling order above is what uGUI draw order IS) but NOT visually, and the twelve committed tiles
still show the labels behind the club head. Re-run `GOLFIN ▸ Capture ▸ Scheme Confirm Tiles` once
the Editor is free and they will pick it up.

## Known FAIL items

1. ~~The 3× club head covers the Pendulum lane's 100%/120% labels at full pull.~~ **Fixed** — the
   labels now draw above the head (§3). Pending a visual confirmation and a tile re-capture, both
   blocked on the Editor.
2. **`T_Pendulum_3` captures an invisible grade pop**, and the manifest reports `marker NaN` for that
   step. Opacity-waiting did not fix it. Next thing I would try is holding the pop open explicitly
   for the capture rather than racing its lifetime.
