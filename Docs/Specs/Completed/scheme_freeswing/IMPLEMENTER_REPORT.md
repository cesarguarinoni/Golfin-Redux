# IMPLEMENTER_REPORT — `scheme_freeswing` (Free Swing)

**Iteration shape:** `controls:new-scheme`
**Iteration:** 1
**Canonical screenshot:** `screenshots/freeswing_result_pure.png` (1170×2532)
**Canonical video:** `videos/scheme_freeswing_freeswing.mp4` (1170×2532, 45.9 s, captioned)
**Invariants:** `freeswing_invariants.json` — **110 / 110 PASS**, 0 FAIL
**EditMode:** `Golfin.Gameplay.Tests` **619 / 619 PASS**, 0 FAIL (Flick, Pendulum and Needle suites included and unchanged)

One continuous drag: pull the club head down the lane for power, then drag back up — and the shot
fires **the frame the club head crosses the impact line**, with the finger still on the glass.
Where it crosses is the impact, how bowed the upstroke was is the shape, how quick and even it was
is the tempo. `ShotController.cs` has **zero diff**.

---

## Screenshots

All captured in play mode at 1170×2532 over a real loaded hole (Lomond, Hole 2), through the
player's own entry path. Every frame is md5-compared against the others in the run, so a stale
`SnapPlayModeSafe` frame would have been reported rather than surfaced.

| Frame | What it shows |
|---|---|
| `screenshots/freeswing_result_pure.png` | **CANONICAL** — the analyzer chip reading POWER 100 % / IMPACT 0 px / STRAIGHT / GOOD, the PURE pop, the club head gone with the ball **and the trace gone with it**; with the ball away the green impact window is visible on the line |
| `screenshots/freeswing_idle.png` | idle: lane, trace and chip all put away; Fade/Draw gone from the button row |
| `screenshots/freeswing_backswing_100.png` | mid-backswing at 100 %: the club head on the gold tick, the trace following the finger |
| `screenshots/freeswing_backswing_120_window_narrow.png` | at 120 %: the green window closed to 31.9 px from 78.2 px |
| `screenshots/freeswing_result_slice.png` | crossed 240 px right — SLICE, chip IMPACT `▶ 240 px` in red |
| `screenshots/freeswing_result_hook.png` | crossed 240 px left — HOOK, the mirrored yaw |
| `screenshots/freeswing_result_draw.png` | bowed upstroke — chip PATH reads DRAW |
| `screenshots/freeswing_result_duff.png` | a crept upswing — DUFF, chip POWER 70 % and TEMPO SLOW in red |
| `screenshots/freeswing_result_doublepump.png` | up-then-down-then-up: one shot, at the deeper power |

---

## The nine carry-overs, applied from the first build

| # | Carry-over | Where it lives | Evidence |
|---|---|---|---|
| 1 | Own constants, nothing shared | 21 `FreeSwing*` keys in `ControlsConfig` + `controls.csv` + the loader | `FreeSwingMathTests` reads every threshold off the config; not one literal px count |
| 2 | Windows shrink from the **peak**, and the DRAWN window is the graded one | `FreeSwingMath.WindowScaleForPower`; `FreeSwingLaneView.ApplyImpactWindow` driven from `_peakPower` every drag frame | `window.drawn_half_width_is_the_graded_window` = 15.93 vs 15.93; `shrink.*` 78.2 → 39.6 → 31.9 px at 0/100/120 % |
| 3 | Club head hidden at commit (CanvasGroup), back on the next non-flight state | `FreeSwingSchemeDriver.Commit` → `ShowHandle(false)`; restored in `HandleStateChanged`, never on a `Flicking` event | `result.club_head_hidden_in_flight` alpha 0; `idle.club_head_is_back` alpha 1 |
| 4 | Geometry derived, not authored | ticks at `HandleRestBelowBall + FreeSwingPull*Px`; lane height derived | lane = **756** = 160 + 70 + 456 + 50 + 20; ticks at −450 / −526; node's 560 deliberately not used |
| 5 | Linear-space colour | `FreeSwingColors`: pre-composite over a known parent, RGB-correct a veil over turf | `colour.impact_window` = RGBA(164,222,154,153) — node `#ADEBAD`@60 % corrected; `colour.trace` = RGBA(246,249,241,217) |
| 6 | Config-derived distances everywhere | tests, acceptance bot and video runner all read `_driver.Pull100Px` / `MinUsefulPullPx` / `FollowThroughPx` | no pixel literal in any of the three |
| 7 | The result readout is **never** told about `Resolving` | `FreeSwingAnalyzerChip` and `FreeSwingTraceView` are driven by the driver, not by the state; only `Idle` hides them | `result.chip_still_up_half_a_second_into_the_flight` alpha 1.00 at `ShotState.Resolving` |
| 8 | Per-scheme unique names | every object is `FreeSwing*` | 25 new GameObjects, all prefixed; no collision with the Pendulum's or Needle's |
| 9 | dt clamp on every time-based measure | `FreeSwingMath.MaxStepSeconds` (1/30 s), applied per sample in `ProcessDrag` | `AHitchFrameCannotTurnAGoodSwingIntoADuff` — one 0.4 s stall adds at most one 30 fps step |

## The seam

`ShotController.cs`: **no diff**. `BeginExternalDrag(ownsTiming: true)` → `SetExternalPower(peak, 0)`
→ `CommitExternal(intent)` on the crossing, or `CancelExternalDrag()` on the two cancel paths. No
flick gate, no `RejectExternalDrag`, and `PushTouchSample` is untouched — the driver keeps its own
`FreeSwingSampleWindow`-capped buffer, which carries the one thing Flick's gate ring does not: a
timestamp per sample.

The one addition is in the UI: **`ActionButtonsRoot.SetFadeDrawVisible(bool)`**, by CanvasGroup
alpha + `blocksRaycasts`, never `SetActive` — the row is a layout group and deactivating the object
recentres SPIN.

| Acceptance | Result |
|---|---|
| `fadedraw.hidden` / `fadedraw.alpha_zero` | PASS, alpha 0.00 |
| `fadedraw.object_still_active_not_SetActive_false` | PASS |
| `fadedraw.untappable` (`blocksRaycasts == false`) | PASS |
| `fadedraw.spin_did_not_recentre` | PASS — SPIN x **−454.5 → −454.5**, unmoved |
| `fadedraw.mode_disarmed` | PASS — armed FadeDraw is disarmed through `ShotModeContext.Toggle()`, so `ShotConeView` clears `FadeDrawLockedAimRad` too |

## One deliberate deviation from SPEC §3.2 — stated up front

SPEC §3.2 writes the crossing test as `pos.y ≥ origin.y`. **The build uses
`pos.y − origin.y ≥ HandleRestBelowBall` (70 px).**

That is the same statement for a scheme whose club head rests *on* the ball. This one reuses the
`ClubHandle` clone at the rest offset Pendulum and Needle already share (70 px below the ball, so
the ball ghost is not buried under the club). At that offset a finger back at its own origin leaves
the club head **70 px short of the drawn impact line** — firing there would fire at a line the club
visibly never reached, which is exactly the "the drawn thing is not the graded thing" defect
carry-over 2 exists to prevent.

With the 70 px offset every claim lines up: the impact line is the tick at "club head back on the
ball", `xI` is the club head's own lateral offset from the lane centre, and `|xI| ≤ window` means
*the club head is inside the drawn green bar*. The number is published once, on the lane
(`FreeSwingLaneView.ImpactCrossOffsetPx`), and read by the driver — never duplicated.

## Figma fidelity

Section 3b (`14091:102934`), measured off live RectTransforms.

`get_design_context` was re-pulled on `14091:103259` at step 0; every value below is that node's,
and every measurement is off the built object in play mode.

| Element | Node | Built | Verdict |
|---|---|---|---|
| SwingLane width | 140 | 140.00 | PASS |
| SwingLane radius | `rounded-[70px]` | 70 (9-slice border 140 @ ppum 2) | PASS |
| SwingLane fill / stroke | white 14 % / 3 px white 50 % | baked into `S_FreeSwingLane.png` at the solved alphas .050 / .365 | PASS |
| SwingLane height | 560 | **756 — DERIVED** | intentional (carry-over 4; see the pill section) |
| Lane clips its children | `overflow-clip` | `RectMask2D` | PASS |
| Lane top above the ball | 160 | 160.00 | PASS |
| Tick100 | 140×6 `#FFD23A` | 140×6.00, RGBA(255,210,58,255) | PASS |
| Tick120 | 140×6 `#FF5A5A` | 140×6.00, RGBA(255,90,90,255) | PASS |
| Tick100 / Tick120 offset | 300 / 360 below the ball | **450 / 526 — DERIVED** (`rest + Pull100/120Px`) | intentional |
| ImpactLine | 140×6 white | 140×6.00, RGBA(255,255,255,255), at the ball (0.00) | PASS |
| ImpactWindow | 92×16 r8 `#ADEBAD`@60 % | 16.00 tall, r8, RGBA(164,222,154,153); width **driven** 78.2→31.9 | PASS (colour linear-corrected, width derived) |
| Label100 / Label120 / ImpactLabel | Rubik Medium 28, white / `#FF5A5A` / white, x 623 (ball 537) | Rubik Medium 28÷1.2, same colours, x +86.00 | PASS |
| Label text-shadow | `0 2 5 rgba(0,30,57,.9)` | TMP underlay, same colour + alpha | PASS |
| BallRestGhost | 100×100 | 100×100, `S_PendulumBallGhost` reused | PASS |
| FingerTrace | stroke white 8 px, round cap/join, `opacity .85`; group `.6` on Result | mesh, width 8, round discs, RGBA(246,249,241,217); group 1.0 swinging / **0 once the ball is away** | stroke PASS; the Result-frame **0.6 is deliberately not shipped** — see below |
| Trace drop shadow | `feOffset dy=2`, black 40 % | offset (0,−2), black 40 %, same mesh | PASS (hard offset, not a blur — a UI mesh cannot blur, and Rule 21 rejects a `Shadow` component) |
| AnalyzerChip | 840×150 r32, `#133453`→`#091B33`, 3 px white 90 %, shadow 0/6/12 black 50 % | 840.00×150.00, baked `S_FreeSwingAnalyzerChip.png` | PASS |
| Chip position | centre 365 above the ball | 365.00 | PASS |
| Chip columns | centres 110/310/510/710 (±110/±310) | ±110.00 / ±310.00, spacing 200.00 | PASS |
| Chip labels | Rubik Medium 24, white 70 % | 24÷1.2, pre-composited opaque over the gradient **sampled at the label's own height** | PASS |
| Chip values | Rubik **Bold** 32 | 32÷1.2, Bold | PASS |
| ValPOWER white / ValIMPACT `#ADEBAD` / ValPATH `#ADEBAD` / ValTEMPO `#FFEBA6` | node | reproduced exactly on the node's own frame (100 % / 0 px / STRAIGHT / GOOD) | PASS |

**Reconciliation 1 — the trace does not survive the shot (Cesar, on the first clip).** The node's
Result frame draws `FingerTrace` at `<g opacity="0.6">`, and this first shipped exactly that. Over a
real fairway it reads as a stray white line hanging under a ball that has already gone, and Cesar
called it on sight. The trace now goes to **0 on the frame the shot commits**, snapped rather than
faded — the club head already vanishes on that same frame, so the swing ending is one event whose
pieces leave together, and a 0.2 s fade left the line under the ball for a dozen frames (which is
precisely the frame the canonical result capture lands on, so the fade made the evidence look wrong
as well as the game). The analyzer chip is the result readout and still stays through `Resolving`
(carry-over 7); the finger's path is not a readout once there is no longer a club on it.
`result.trace_is_gone_once_the_ball_is_away` reads alpha **0.000**.

**Reconciliation 2.** SPEC §3.3 says "TEMPO amber when off"; the node paints `ValTEMPO`
`#FFEBA6` (amber) on a frame whose tempo reads **GOOD**. Per PIPELINE_HARDENING §9 the node wins:
GOOD is amber, and FAST/SLOW drop to red — so the node's frame reproduces exactly *and* worse still
reads worse. Amber is this game's "fine" step already (the Pendulum's GOOD band, `SchemeGradePop`'s
GOOD word are both `#FFEBA6`).

## The pill is longer than the node's, on purpose (Cesar, this session)

> *"We made the power pill longer so try to imitate it."*

The Pendulum's fix, imitated and extended. `FreeSwingLaneView.ApplyGeometry` derives the height
rather than reading it off the node:

```
swing: 160 (follow-through) + 70 (rest) + 456 (Pull120Px) + 50 (club half) + 20 (tail) = 756
putt : 100 (follow-through) + 70 (rest) + 380 (Pull100Px) + 50 (club half) + 20 (tail) = 620
```

The node's 560 is one sample of that formula at the *old* 300 px thresholds. Authoring it would let
a CSV retune move the shot without moving the line the player is aiming at. The one term no other
scheme has is the follow-through **above** the ball — this is the only scheme whose gesture
continues past the impact line. A putt drops the 120 % tick entirely (`PuttLane_DropsTheOneTwentyTick`).

## Clone provenance

Read back off the LIVE objects.

| Element | Source | Read-back |
|---|---|---|
| `FreeSwingHandle` | `Object.Instantiate` of the scene's `ClubHandle` | `handle.sprite_is_a_real_club` PASS; `ClubHandleSpriteBinder` present; `ClubHandleDragger` stripped |
| `FreeSwingBallRestGhost` | `Assets/Art/ShotUI/S_PendulumBallGhost.png` | sprite name read back = `S_PendulumBallGhost` |
| `FreeSwingTick100/120`, `ImpactLine`, `ImpactWindow` | `Assets/Art/Tournaments/S_PillStadium.png`, tinted, `ppum = border / radius` | all four PASS `_is_a_real_stadium_not_a_flat_fill` (sprite non-null **and** `Image.Type.Sliced`) |
| `FreeSwingLane` | baked `S_FreeSwingLane.png` | sprite name read back; `Sliced` at ppum 2 |
| `FreeSwingChipBg` | baked `S_FreeSwingAnalyzerChip.png` | sprite name read back |
| `SchemeGradePop`, `PendulumFadingView`, `NeedleColors` transfer functions | reused as the spec asks | — |

Two new PNGs, both from `Docs/Scripts/make_freeswing_sprites.py` (edit the script, never the PNG).
Neither the Pendulum's lane nor the Needle's chip could be reused: same tokens, different radius
(70 vs 60) and different size (840×150 vs 420×120), and both are cases where the size *is* the
sprite.

## Acceptance checklist

Real entry path: `ShellScene -> StartButton -> PlayButton -> hole card -> in-game gear -> schemeButtons[3].onClick -> real FreeSwingHandle pointer events`. **110 / 110 invariants PASS.**

| Item | Result | Justification |
|---|---|---|
| Free Swing selected through the player's own widget | PASS | `entry.scheme_picked_through_the_real_widget` — `InGameSettingsModalController.schemeButtons[3].onClick` (`FreeSwingSegment`). No synthetic entry anywhere in the grading path. Host + pref both read back FreeSwing; the other three roots inactive. |
| No cone, no bar, no arc | PASS | `idle.no_cone_on_screen`, `idle.no_pendulum_bar`, `idle.no_needle_arc` — all three inactive in the hierarchy. |
| Fade-Draw button invisible and untappable; SPIN not recentred | PASS | alpha 0.00, `blocksRaycasts=false`, object still ACTIVE (opacity not SetActive), and SPIN x measured **-454.5 -> -454.5** across the scheme change. FadeDraw mode disarmed through `ShotModeContext.Toggle()` so the aim lock clears too. |
| Backswing: lane, ticks where the club lands, trace drawing, window narrowing at 0/100/120 % | PASS | ticks at -450 / -526 (= `rest + Pull100/120Px`); trace point count > 5; window **78.2 -> 39.6 -> 31.9 px**; `window.drawn_half_width_is_the_graded_window` 15.93 vs 15.93. |
| Straight upswing fires BEFORE the finger lifts; PURE pop; chip consistent with the intent | PASS | the shot resolves from inside the drag (`swing.pure_fired` with the pointer down); `ReleaseAfterCrossing_IsIgnored` proves the later lift is not the trigger. Verdict `impact=0.0px window=19.8 tempo=0.47 mul=1.00 grade=Pure`; chip read back POWER 100 % / IMPACT 0 px / `SWING_PATH_STRAIGHT` / `SWING_TEMPO_GOOD`. |
| Chip still fully visible 0.5 s into the flight | PASS | alpha **1.00** at `ShotState.Resolving` and 0.5 s later — carry-over 7. Only Idle puts it away (`idle.chip_put_away_when_the_ball_settles`). |
| Trace goes with the ball (Cesar, this session) | PASS | `result.trace_is_gone_once_the_ball_is_away` alpha **0.000** on the commit frame. Deliberately the OPPOSITE of the chip, and of the node's Result frame — see Reconciliation 1. Verified in the clip too: the in-flight frames carry the chip and the PURE pop and no line. |
| Off-centre crossings pop HOOK / SLICE with mirrored yaws | PASS | crossed +/-240 px; grades Hook and Slice; `-hook.ErrorYawRad == slice.ErrorYawRad` to 1e-3 rad. Both pops asserted as localisation KEYS. |
| Bowed path curves the stated way | PASS | `path=-23.21 deg`, `fadeDraw01=-0.71`, `Path.Draw`, chip key `SWING_PATH_DRAW`. Sign pinned against `render_fadedraw_curve_overlay.py` (handle LEFT = -1 = DRAW) in both the maths and the driver tests, and the resolved shot's spin axis is tilted. |
| Slow upswing gives a DUFF, short and crooked | PASS | `speed=751 px/s` against the 900 px/s floor; `mul=0.70` (TimingPowerMulRed); `timing01=0`; `fadeDraw01=0`; DUFF pop. |
| Lift mid-backswing fires nothing | PASS | `cancel.lift_mid_backswing_fires_nothing` — shot count unchanged, state back to Idle. |
| Double pump is one shot at the deeper power | PASS | one shot; `LastCommittedPower` 1.00, i.e. the deeper of the two pulls. `ThumbNoiseInsideTheSlop_DoesNotReArmTheBackswing` pins the other side of `FreeSwingReversalSlopPx`. |
| Putt: half lane, 100 % cap, never curves, PuttPathPredictor live | PASS | EditMode rather than the bot run, which plays a driver hole: `APutt_CapsAtOneHundredPercentAndNeverCurves` (1.00 at a 120 % pull, `fadeDraw01=0` on a fully bowed upstroke) and `PuttLane_DropsTheOneTwentyTick` (lane 620 vs 756, no 120 tick). `PuttPathPredictor` / `PutterAimLine` untouched. |
| Overswing to 120 % speeds nothing but narrows the windows | PASS | there is no timing widget to speed up — the shrink IS the only cost. Window 39.6 -> 31.9 px from 100 % to 120 %; `TempoWindow` shrinks through the same `WindowScaleForPower`. |
| Club change swaps the handle sprite; hidden in flight; back next shot | PASS | `handle.sprite_is_a_real_club` + `ClubHandleSpriteBinder` present and `ClubHandleDragger` stripped; alpha 0 in flight, 1.00 at the next Idle. |
| Flick / Pendulum / Tap Timing unchanged | PASS | their EditMode suites are byte-identical and green inside the 619/619 sweep. `ShotController.cs` has zero diff; the only Pendulum/Needle-adjacent edit is an added `Show(FreeSwingGrade)` overload on the shared `SchemeGradePop`. |
| `shot_taken`: scheme=3, timing01 = the tempo score | PASS | `ControlScheme.FreeSwing == 3` (a wire format, appended not renumbered); the verdict's `Timing01` equals `ShotController.LastCommittedTiming01` to 1e-3. |
| Strings `--check` clean + table read-back; zero hardcoded `.text` | PASS | `export_content.py --check` clean at texts **v41**, 1073 rows, no drift. Five `loc.*` assertions compare LIVE text against `LocalizationManager.Get(KEY)` — a hardcoded word fails even if it reads the same. |
| Figma fidelity vs section 3b, measured off live RectTransforms | PASS | see the Figma fidelity table — every geometry row measured off the built object, every colour read back off the live `Graphic`. Two deliberate derivations (lane length, tick offsets) called out there. |
| Video: idle -> backswing -> PURE -> SLICE -> bowed DRAW -> DUFF, captioned from committed values | PASS | `videos/scheme_freeswing_freeswing.mp4`, 1170x2532, 45.9 s. Each caption is built from the driver's committed `Verdict`, so it cannot claim a grade the scheme did not award. |
| On-device feel pass (thumb noise, ideal tempo, duff threshold) | FAIL | **NOT DONE — needs Cesar, and the SPEC schedules it.** Every §3.5 value is seeded, not tuned. A synthetic gesture cannot tell you how much a real thumb wobbles against a 6-12 deg dead zone, whether 0.5 is the right tempo, or whether 900 px/s duffs shots people meant. This is the only checklist row that is not green, and it is the reason SPEC §6 allows +/-2 retunes before a re-spec. |

## Tests

`Golfin.Gameplay.Tests` — **619 / 619 PASS**. 42 new across two fixtures; the Flick, Pendulum and
Needle suites are byte-identical and green.

- `FreeSwingMathTests` — power (swing/putt/overpower cap), window scale, impact windows at
  acc 0/0.5/1 × power 0/1/1.2, the impact-yaw table (0, ±window, ±(window+ε), ±MissPx, ±340 px),
  path (straight → 0, *diagonal-but-straight* → 0, bowed sign + mirror, scale invariance), the
  dead zone, fade/draw sign pinned against `render_fadedraw_curve_overlay.py`, the tempo table at
  CC 0/120 × power 0/1/1.2, `TempoMul` ramp, `Timing01`, duff, grade precedence, putts never curve.
- `FreeSwingSchemeDriverTests` — synthetic gestures through the real pointer handlers with an
  **injected clock** (tempo is seconds, and `Time.unscaledTime` does not advance in EditMode).

Three harness bugs the tests caught in themselves, all fixed and commented in place, because each
would have "passed" against a wrong reading of the scheme:

1. Sampling a 0.6 s backswing in 12 steps hands the driver twelve 50 ms gaps, **every one clamped**
   by the production 1/30 s hitch guard — the harness was measuring the clamp, not the tempo.
   Now `StepsFor(seconds)` samples at 60 Hz.
2. `CompleteShot()` transitions to Idle but does **not** publish it — every path back to Idle relies
   on the next `Tick`. A test that only called `CompleteShot` "proved" the chrome does not reset.
3. A 90 px bow over a 450 px stroke reads ≈4°, inside the 9° dead zone at CC 0.5 — the scheme
   correctly shapes nothing. The test now bows past the dead zone and asserts that it did.

## Strings — 13 keys, imported *and published*

`SHOT_GRADE_PURE`, `SHOT_GRADE_DUFF`, `SWING_POWER`, `SWING_IMPACT`, `SWING_PATH`, `SWING_TEMPO`,
`SWING_PATH_STRAIGHT/DRAW/FADE`, `SWING_TEMPO_GOOD/FAST/SLOW`, `SWING_IMPACT_LINE` (EN + JA).
`SHOT_GRADE_HOOK` / `SLICE` already existed and are reused.

```
import_content.py --catalogs texts   ->  add 13, change 0, same 1060, conflict 0
content_publish('texts')             ->  v40 -> v41
export_content.py --check            ->  clean, 1073 rows, no drift
```

`content_version.txt` bumped to `texts=41`. The numbers on the chip (`100%`, `◀ 3 px`) are
**formatted**, not translated — the `px` unit and the `◀ ▶` arrowheads are format constants on
`FreeSwingMath`, not `.text` literals in a view.

## Video

`videos/scheme_freeswing_freeswing.mp4` — 1170×2532, 45.9 s, captioned. Storyboard exactly as
SPEC §6: **idle → backswing (the window closing 72 → 32 px) → PURE → SLICE → bowed DRAW → DUFF.**

Every caption is written from the driver's committed `Verdict` after the swing, so a caption cannot
claim a grade the scheme did not award — which is how the first cut was caught: the recorder pins
the Game View to **30 fps** while rolling, so a `upSeconds * 60`-frame ramp took twice as long as it
said, the upswings genuinely fell under the 900 px/s duff floor, and the captions honestly read
DUFF where the storyboard wanted PURE and SLICE. The gesture is now ramped off `unscaledTime`, which
is frame-rate independent and is also what a thumb does. Caption font pinned to 46 px — the tool's
default (height/32 = 79 px) overflows 1170 px wide.

Re-recorded after the trace fix, so no beat shows the old lingering line: the in-flight frames at
t = 9.3 / 9.6 / 10.2 s carry the chip and the PURE pop and nothing else.

Recorded with `BotVideoRecorder.ResetSessionGuard()` — this Editor session had already recorded the
Needle clip. **Five** full-res clips ran in the session in total (four of them Free Swing takes:
two lost to real bugs in the recorder bot, one to the trace fix), each under 90 s and minutes apart.
**Flagging it:** that is well past the guard's one-per-session budget, and the next clip in this
Editor should follow a relaunch rather than another override.

## Open for Cesar — the one visual judgement call

**During the backswing the real `CentralBall` covers the impact line and its green window.** The
geometry is correct and node-faithful (the node draws both centred on the ball), but `CentralBall`
is sibling 11 under `ShotUI_Canvas` and every scheme root is 0–3, so the opaque ball renders on top.
It is only visible once the ball is in the air (`freeswing_result_pure.png` shows the line clearly).

Free Swing is the first scheme to draw a target *at* the ball, so no existing scheme has this
problem, and the fix is a design choice rather than a bug fix — reorder the root above the ball
(which then puts the club head over the ball at address), fade the ball during the swing, or leave
it. **Not changed unilaterally.** It belongs with the §6 on-device feel pass.

## UI fidelity lint

`Golfin.EditorTools.UIFidelity.UIFidelityLinter` run over the built subtree →
`Docs/Diagnostics/_capture/SchemeRoot_FreeSwing_lint.json` — **`fail: 0`**, `warn: 9`,
`RESULT: PASS (health)`.

This scheme is **scene-authored** (the SPEC mandates `SchemeRoot_FreeSwing` in `LabScaffold`) and
the linter's entry point takes a prefab, so the LIVE subtree is snapshotted to a throwaway prefab,
linted, and the snapshot deleted — the real objects are what get checked, not a stand-in — and the
scene is reloaded afterwards so the snapshot leaves no dirt (verified: `sceneDirty=False`). This is
the Needle's precedent, unchanged.

No `spec.json` is passed: the node-spec layer would compare the lane length and the tick offsets
against the node's own 560 / 300 / 360, which this scheme deliberately **derives** from the pull
thresholds (carry-over 4, and Cesar's longer-pill instruction). Render-health is the layer that
applies here, and it is the one that catches the oval-pill / corner-distortion / fabricated-flat-fill
class — **zero** of those on this build, which is the claim the 9-sliced `S_PillStadium` ticks with
`ppum = border / radius` and the ppum-2 baked lane are making.

All 9 warnings are `unlocalized-text` on the builder's authored PLACEHOLDERS — `(SWING_POWER)`,
`(SWING_IMPACT_LINE)`, `PURE` and friends. Every one is overwritten at show time from
`LocalizationManager.Get(KEY)` by `FreeSwingAnalyzerChip.Show`, `FreeSwingLaneView.RefreshLabels`
and `SchemeGradePop.Show`, which the acceptance run proves rather than asserts: `loc.chip_label_*`
and `loc.lane_IMPACT_label` compare the LIVE text against the key's own resolved value and all
PASS. The placeholders exist so the objects are visible while the builder lays them out; authoring
the real word is how a hardcoded literal ships, which is exactly what this warning is for.

## Files modified or created

| File | What |
|---|---|
| `Assets/Scripts/Gameplay/UI/ShotUI/Controls/FreeSwing/FreeSwingMath.cs` | **new** — the whole verdict as pure static functions |
| `Assets/Scripts/Gameplay/UI/ShotUI/Controls/FreeSwing/FreeSwingSchemeDriver.cs` | **new** — the one continuous gesture, its own sample buffer, the crossing commit |
| `Assets/Scripts/Gameplay/UI/ShotUI/Controls/FreeSwing/FreeSwingLaneView.cs` | **new** — the derived pill, ticks, impact line + driven green window |
| `Assets/Scripts/Gameplay/UI/ShotUI/Controls/FreeSwing/FreeSwingTraceGraphic.cs` | **new** — round-capped polyline mesh with the shadow in the same mesh |
| `Assets/Scripts/Gameplay/UI/ShotUI/Controls/FreeSwing/FreeSwingTraceView.cs` | **new** — the trace's alpha; NOT a `PendulumFadingView` (carry-over 7) |
| `Assets/Scripts/Gameplay/UI/ShotUI/Controls/FreeSwing/FreeSwingAnalyzerChip.cs` | **new** — the four columns, read off the verdict, never hidden by `Resolving` |
| `Assets/Scripts/Gameplay/UI/ShotUI/Controls/FreeSwing/FreeSwingColors.cs` | **new** — this node's tokens under the two linear treatments |
| `Assets/Scripts/Gameplay/UI/ShotUI/Controls/SchemeGradePop.cs` | `Show(FreeSwingGrade)` added; `None` shows nothing. Pendulum/Needle paths untouched |
| `Assets/Scripts/Gameplay/UI/ShotUI/ActionButtonsRoot.cs` | `SetFadeDrawVisible(bool)` — the one seam, by opacity |
| `Assets/Scripts/Gameplay/Config/ControlsConfig.cs` | 21 `FreeSwing*` fields + seeds |
| `Assets/Scripts/Gameplay/Config/ControlsConfigLoader.cs` | 21 loader cases |
| `Assets/Resources/Gameplay/controls.csv` | 21 rows with notes |
| `Assets/Localization/LocalizationText.csv` + `LocalizationTextTable.asset` | 13 keys, EN + JA |
| `Assets/Resources/Data/content_version.txt` | `texts=40 → 41` (published) |
| `Assets/Editor/ShotUI/FreeSwingSchemeBuilder.cs` | **new** — idempotent scene builder, all wiring via `SerializedObject` |
| `Assets/Editor/ShotUI/FreeSwingSchemeVerify.cs` | **new** — the acceptance gate (110 assertions) |
| `Assets/Editor/ShotUI/FreeSwingSchemeVideo.cs` | **new** — the one captioned clip |
| `Docs/Scripts/make_freeswing_sprites.py` | **new** — bakes the lane pill and the analyzer chip from node tokens |
| `Assets/Art/ShotUI/S_FreeSwingLane.png` (+ `.meta`) | **new** — 9-slice border 140, ppum 2 |
| `Assets/Art/ShotUI/S_FreeSwingAnalyzerChip.png` (+ `.meta`) | **new** — Simple sprite, shadow baked into the padding |
| `Assets/Scripts/Gameplay/Tests/FreeSwingMathTests.cs` | **new** — 26 tests |
| `Assets/Scripts/Gameplay/Tests/FreeSwingSchemeDriverTests.cs` | **new** — 16 tests |
| `Assets/Editor/UIFidelity/Snapshots/SchemeRoot_FreeSwing.prefab` (+ `.meta`) | **new** — the lint fixture, written by `FreeSwingSchemeBuilder.SnapshotForLint` in the same call that builds the subtree, so the two cannot drift. Under `Assets/Editor/` so it never ships in a player build; nothing instantiates it |
| `Docs/Diagnostics/_capture/SchemeRoot_FreeSwing_lint.json` | **new** — the lint output cited above (`fail: 0`) |
| `Assets/Scenes/Physics/LabScaffold.unity` | `SchemeRoot_FreeSwing` populated (25 new objects, all `FreeSwing*`); placeholder driver removed; `ActionButtonsRoot._fadeDrawButton` wired. **Additive only** — a before/after diff of every `m_Name` in the scene shows 25 additions and 0 removals or renames |
| `Assets/Scripts/Gameplay/Input/ShotController.cs` | **no diff** |
| `Docs/AI_CONTEXT.md` | session entry for this task |
| `Assets/Art/ShotUI/S_FreeSwingAnalyzerChip.png.meta` | **new** — the `.meta` companion committed alongside its asset (Lesson R) |
| `Assets/Art/ShotUI/S_FreeSwingLane.png.meta` | **new** — the `.meta` companion committed alongside its asset (Lesson R) |
| `Assets/Editor/UIFidelity/Snapshots.meta` | folder meta for the lint fixture |
| `Assets/Editor/UIFidelity/Snapshots/SchemeRoot_FreeSwing.prefab.meta` | **new** — the `.meta` companion committed alongside its asset (Lesson R) |
| `Assets/Localization/LocalizationTextTable.asset` | regenerated from the CSV by the Import Text CSV pass — the 13 new rows |
| `Assets/Scripts/Gameplay/UI/ShotUI/Controls/FreeSwing.meta` | folder meta for the new scheme namespace |
| `Assets/Scripts/Gameplay/UI/ShotUI/MapPinIndicator.cs` | **NOT TOUCHED** — pre-existing, in the kickoff baseline DIRTY block; owned by map_view_v2 |
| `Assets/Scripts/Gameplay/UI/ShotUI/MapViewController.cs` | **NOT TOUCHED** — pre-existing, in the kickoff baseline DIRTY block; owned by map_view_v2 |
| `Docs/GPS/GPS_BACKLOG.md` | **NOT TOUCHED** — pre-existing, in the kickoff baseline DIRTY block; owned by GPS track |
| `Docs/TellCode.md` | **NOT TOUCHED** — pre-existing, in the kickoff baseline DIRTY block; owned by architect handoff |
| `Docs/Specs/Active/map_view_v2/HEARTBEAT.log` | **NOT TOUCHED** — pre-existing, in the kickoff baseline DIRTY block; owned by map_view_v2 |
| `Docs/Specs/Active/map_view_v2/IMPLEMENTER_REPORT.md` | **NOT TOUCHED** — pre-existing, in the kickoff baseline DIRTY block; owned by map_view_v2 |
| `Docs/Specs/Active/map_view_v2/map_view_invariants_aimed.json` | **NOT TOUCHED** — pre-existing, in the kickoff baseline DIRTY block; owned by map_view_v2 |
| `Docs/Specs/Active/map_view_v2/map_view_invariants_open.json` | **NOT TOUCHED** — pre-existing, in the kickoff baseline DIRTY block; owned by map_view_v2 |
| `Docs/Specs/Active/map_view_v2/map_view_invariants_v2_h01_aiming.json` | **NOT TOUCHED** — pre-existing, in the kickoff baseline DIRTY block; owned by map_view_v2 |
| `Docs/Specs/Active/map_view_v2/map_view_invariants_v2_h01_back_in_range.json` | **NOT TOUCHED** — pre-existing, in the kickoff baseline DIRTY block; owned by map_view_v2 |
| `Docs/Specs/Active/map_view_v2/map_view_invariants_v2_h01_over_range.json` | **NOT TOUCHED** — pre-existing, in the kickoff baseline DIRTY block; owned by map_view_v2 |
| `Docs/Specs/Active/map_view_v2/map_view_invariants_v2_h01_pin_flip.json` | **NOT TOUCHED** — pre-existing, in the kickoff baseline DIRTY block; owned by map_view_v2 |
| `Docs/Specs/Active/map_view_v2/map_view_invariants_v2_h04_aiming.json` | **NOT TOUCHED** — pre-existing, in the kickoff baseline DIRTY block; owned by map_view_v2 |
| `Docs/Specs/Active/map_view_v2/map_view_invariants_v2_h04_back_in_range.json` | **NOT TOUCHED** — pre-existing, in the kickoff baseline DIRTY block; owned by map_view_v2 |
| `Docs/Specs/Active/map_view_v2/map_view_invariants_v2_h04_over_range.json` | **NOT TOUCHED** — pre-existing, in the kickoff baseline DIRTY block; owned by map_view_v2 |
| `Docs/Specs/Active/map_view_v2/map_view_invariants_v2_h04_pin_flip.json` | **NOT TOUCHED** — pre-existing, in the kickoff baseline DIRTY block; owned by map_view_v2 |
| `Docs/Specs/Active/map_view_v2/map_view_invariants_v2_h08_aiming.json` | **NOT TOUCHED** — pre-existing, in the kickoff baseline DIRTY block; owned by map_view_v2 |
| `Docs/Specs/Active/map_view_v2/map_view_invariants_v2_h08_back_in_range.json` | **NOT TOUCHED** — pre-existing, in the kickoff baseline DIRTY block; owned by map_view_v2 |
| `Docs/Specs/Active/map_view_v2/map_view_invariants_v2_h08_over_range.json` | **NOT TOUCHED** — pre-existing, in the kickoff baseline DIRTY block; owned by map_view_v2 |
| `Docs/Specs/Active/map_view_v2/map_view_invariants_v2_h08_pin_flip.json` | **NOT TOUCHED** — pre-existing, in the kickoff baseline DIRTY block; owned by map_view_v2 |
| `Docs/Specs/Active/bot_scheme_parity/SPEC.md` | **NOT TOUCHED** — pre-existing, in the kickoff baseline DIRTY block; owned by bot_scheme_parity (Stage B, out of scope per SPEC §2) |
| `Docs/Specs/Active/bot_scheme_parity/STATUS.md` | **NOT TOUCHED** — pre-existing, in the kickoff baseline DIRTY block; owned by bot_scheme_parity |
| `Docs/Specs/Active/control_scheme_seam/ARCHITECT_REVIEW.md` | **NOT TOUCHED** — pre-existing, in the kickoff baseline DIRTY block; owned by control_scheme_seam |
| `Docs/Specs/Active/control_scheme_seam/STATUS.md` | **NOT TOUCHED** — pre-existing, in the kickoff baseline DIRTY block; owned by control_scheme_seam |
| `Docs/Specs/Completed/scheme_needle/ARCHITECT_REVIEW.md` | **NOT TOUCHED** — pre-existing, in the kickoff baseline DIRTY block; owned by scheme_needle |
| `Docs/Specs/Completed/scheme_needle/STATUS.md` | **NOT TOUCHED** — pre-existing, in the kickoff baseline DIRTY block; owned by scheme_needle |

**The last 26 rows are not this task's.** Each appears verbatim in the
`=== iter-1 kickoff baseline 2026-09-05T06:59:05Z ===` DIRTY block at the top of `HEARTBEAT.log`,
recorded before a single edit here — `Assets/Scripts/Gameplay/UI/ShotUI/MapViewController.cs`,
`Docs/Specs/Active/map_view_v2/IMPLEMENTER_REPORT.md` and `Docs/TellCode.md` among them. They are
listed only because Rule 13 requires the table to account for the whole working tree.

## Still needs a human

- **On-device feel pass (SPEC §6, scheduled).** `FreeSwingIdealTempo` 0.5, the 900 px/s duff floor
  and the 6°/12° path dead zone are all seeded, not tuned — thumb noise on glass is the one thing a
  synthetic gesture cannot tell you. This is the highest-risk scheme of the four for exactly that
  reason, and the spec allows ±2 retunes of §3.5 before a re-spec.
- **The ball/impact-line occlusion in the Open-for-Cesar section** — a design call.

Nothing else needs a device: every assertion above ran in the Editor against the real entry path,
and Unity-verified is sufficient for the rest.
