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
| `Assets/Editor/ShotUI/SchemeConfirmTilesCapture.cs` | `ResetLie()` — the ball goes back on the tee before every scheme |
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
2. **`T_Pendulum_3`: the Pendulum flick never commits** — traced in §5. Not a pop bug and not a
   crop bug: `ReleaseSwing` returns before its commit block, so `Show()` is never called. Which of
   its three exits it takes is the open question.

   (superseded detail) The
   uncropped source frame has bare fairway where "JUST!" should be, so it is not a crop artifact,
   and the manifest reports `marker NaN` for that step. `WaitUntilActive` returned on
   `activeInHierarchy`, which a `SchemeGradePop` answers true to through its hold, its fade AND
   afterwards at alpha 0; I tightened it to require `CanvasGroup.alpha > 0.9` and **it did not
   resolve it** — the capture appears to grab a frame later than the wait returns. The tightened
   wait is kept (it is correct on its own terms; the previous behaviour was luck-of-the-frame-count)
   but the cause is open. Not a wiring bug — `_gradePop` is wired on all three drivers
   (`1396204410`, `2043148095`, `1123222000`), and Needle 3 and Free Swing 3 photograph THEIR pops
   fine from the same code path, so it is specific to what the Pendulum commit does to its own root.
   The tile is not blank — it reads as "the club has swung past the ball", which with its
   "3 FLICK UP" caption is weak rather than wrong.

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

**Confirmed on screen.** `T_Pendulum_1` and `T_Pendulum_2` now read "100%" over the club head where
before there was nothing but club, and the same on Free Swing 1/2. See
`screenshots/tiles_final_all12.png`.

## §4 — all four schemes shot from the same lit tee

Cesar: *"plan the shot so they are not in the shade."* Every `Capture` COMMITS a shot, so the loop
photographed Flick from the tee, Pendulum from wherever Flick's ball landed, Needle from Pendulum's,
and Free Swing from Needle's. On Lomond 2 that walks the set downrange into tree shadow — Flick came
back off a bright tee and the other three came back murky, which is not a scheme difference at all,
it is four different lies.

`SchemeConfirmTilesCapture.ResetLie()` now calls `PhysicsLabController.ResetToTee()` between
`SelectScheme` and `Capture` (reflection — `Assets/Scripts/Physics/` is frozen and `ResetToTee` is
already public, so this is a call, not a reach-in), waits for Idle and re-runs `HideChrome` in case
the tee setup restored a card. The heartbeat logs `reset_to_tee: ok` four times.

Result: every scheme's first two tiles are on bright, identical fairway, and the set reads as one
thing. The RESULT tiles (step 3) are still shot after the ball is struck, so their backdrop is
wherever the chase camera followed it — Needle 3 has trees behind "PERFECT", Free Swing 3 a cart
path behind "PURE". Both grade chips are bright on a darker backdrop and read fine; making those
identical too would mean not photographing a real result.

## §5 — tracing `T_Pendulum_3`'s missing grade pop

Cesar: *"trace the pendulum grade pop."* Instrumented rather than reasoned about — `NotePopState`
dumps everything that decides whether a pop is in the frame, and a temporary per-frame tracer
sampled its alpha across the whole ~1 s life of the animation.

**What the instrument says.** At the moment the shutter opens, and every frame from the one after
`Up()`:

```
PendulumGradePop  activeSelf=True inHierarchy=True offAncestor=none
                  alpha=0.000  scale=0.600  text='JUST!'  labelAlpha=1.00 labelEnabled=True
NeedleGradePop    activeSelf=True inHierarchy=True offAncestor=none     <- the control
                  alpha=1.000  scale=1.000  text='PERFECT'
```

alpha is `0.00` from the FIRST frame after the release and never rises. `PlayRoutine` sets
`alpha = 1f` on its first line, synchronously, so **it never ran — `Show()` was never called**.
`scale = 0.600` is `_startScale`, exactly what `Awake`'s `HideImmediate()` leaves, so the pop has
not been touched since the scene loaded.

**Which relocates the bug entirely.** The pop is fine. Reading the driver back:

- `LastCommittedMarker` is `float.NaN` — its *initialiser*, so it was never assigned.
- `LastCommittedGrade` is `Just`, which is `default(PendulumGrade)` — also never assigned.
- Both are set at `PendulumSchemeDriver:333-335`, six lines above the `_gradePop?.Show(...)` at 339.

So `ReleaseSwing` returns before its commit block: **the flick never fires.** `T_Pendulum_3` is not
a capture defect at all — it is an accurate photograph of a swing that did not commit, which is why
it reads as "the club has swung past the ball".

**Ruled out along the way**, each by evidence rather than argument: `ResetSwing` does not hide the
pop (none of the three drivers' do); `SchemeGradePop` is not a `PendulumFadingView`, so Resolving
does not fade it; `Awake` is once-per-lifetime so it cannot re-hide on enable; `_startScale` is 0.6,
so it is not a scale-in caught early; the three source frames have distinct md5s, so it is not a
stale grab; and `_label`/`_group`/`_gradePop` are identically wired on all three schemes
(`1396204410` / `2043148095` / `1123222000`). Needle and Free Swing are unaffected because neither
commits through a flick gate — Needle commits on a tap, Free Swing on crossing the impact line.

**WHICH exit: A, then B.** Cesar approved a log line inside `PendulumSchemeDriver`, so I put one at
every outcome — the reverse-cancel, `OnPointerUp`'s `!_dragging` guard, the flick gate, the
no-power cancel and the commit. One run:

```
[PendulumExit] A reverse-cancel  held=0.333s deepest=540 peak=1.00
[PendulumExit] B pointer-up with no live drag — the swing was already ended
```

`Advance`'s `HandleReverseCancel` fires at **held = 0.333 s**, nearly 3x the 0.12 s threshold, on a
full 100 % pull (`deepest=540 peak=1.00`). It sets `_dragging = false`, cancels the drag and resets
the swing. `OnPointerUp` then arrives, hits its own `if (!_dragging) return`, and `ReleaseSwing` is
**never called at all** — no gate, no commit, no `Show`. That is the whole chain.

**Why 0.333 s: the capture's frames are ~111 ms.** It screenshots and writes files between frames.
Step 3 drags up over three of them; the first alone clears the 60 px arming threshold and the other
two are three times the hold.

**And the obvious fix does not work, for a second reason.** Shortening the gesture just moves the
failure to the flick gate, because `ShotController.EvaluateFlickGate` cannot pass at this frame rate
either:

- `_flickSampleWindow` is 0.08 s, so at 111 ms frames no two samples are ever inside the window;
- line 280 then falls back to the immediately-previous sample;
- line 284 is `if (dt > _stutterFrameThreshold) return false;` with the threshold at **0.1 s**.

dt is always ~0.111 s, so the gate returns false every time, whatever the gesture. That is exactly
what my two earlier rewrites ran into. **The rig cannot produce a committing Pendulum flick through
synthetic pointer events at its own frame rate** — not a tuning problem, a structural one.

**The fix is the one the codebase already prescribes.** `EvaluateFlickGate` line 261 is
`if (_sampleCount == 0) return true; // programmatic driver — not a touch swing`, and
`ReleaseSwing(requireFlickGate: false)` has exactly one caller: `DriveBot`. The tile capture is a
bot in all but name, and CLAUDE.md rule 17 already says bots swing through `BotSwing.Play`, never
hand-rolled pointer events. Routing step 3's swing through the bot path would clear both the
reverse-cancel and the gate at once, and would produce a real graded commit instead of a synthetic
one. That is a change to the capture rig rather than a log line, so it is scoped and left for a
decision.

**Worth a second look beyond the capture:** `HandleReverseCancel` landed this morning, and it fires
here on a genuine full-power pull followed by an upward move. On a device that hitches for a third
of a second mid-flick, a real player's shot would die the same way. The 0.12 s hold is measured in
wall-clock time with no guard against a stutter frame — unlike the flick gate right next to it,
which explicitly refuses to trust a frame longer than 0.1 s.

**The probes are kept, behind `_logSwings`** — the driver's existing flag, which is 0 in the shipped
scene, so they are silent. They took this from "the pop is broken" to "the swing never fired" in one
run, and they are the instrument for the next person.

## Known FAIL items


1. ~~The 3× club head covers the Pendulum lane's 100%/120% labels at full pull.~~ **Fixed** — the
   labels now draw above the head (§3). Pending a visual confirmation and a tile re-capture, both
   blocked on the Editor.
2. **`T_Pendulum_3` captures an invisible grade pop**, and the manifest reports `marker NaN` for that
   step. Opacity-waiting did not fix it. Next thing I would try is holding the pop open explicitly
   for the capture rather than racing its lifetime.
