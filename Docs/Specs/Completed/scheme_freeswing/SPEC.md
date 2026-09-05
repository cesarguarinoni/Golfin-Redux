# SPEC — `scheme_freeswing` (player-facing name: **Free Swing**)

**Status:** SPEC_READY (2026-09-05). Spec 3 of the control-schemes track — `Docs/CONTROL_SCHEMES_PLAN.md` §4, decisions §7 (#3 path-derived curve, toggle hidden; #5 fires on crossing the impact line; #6 overswing to 120 %, never on putts; #7 putt variant). Builds on `control_scheme_seam` (`8913901a7`), `scheme_pendulum` (`501bf5881`) and `scheme_needle` (`d54468b6c`), all DONE. Flick, Pendulum and Tap Timing must stay byte-identical.
**Figma:** file `5gEAHjl6xAtW8iYY7NMvWd`, page "Shot Controls — Schemes", section **3b — Free Swing (club handle)** (`14091:102934`) — frames Idle `14091:103039`, Backswing `14091:103137`, Downswing `14091:102935`, Result `14091:103241`, Putt `14091:103354`. Renders in `reference/`.
**One line:** Tiger-Woods-TrueSwing-style — one continuous drag: pull the club straight **down** for power, then drag back **up**; the shot fires the frame the finger crosses the impact line. Where you cross it is the impact (hook/slice), how straight you came up is the shape (draw/fade), and how quick and even the upswing was is the tempo (power).

**Carry-overs — apply from the first build (Pendulum + Needle review rounds, 2026-09-05):**
1. Own constants; nothing shared with Flick's arrow, Pendulum's Hz or Needle's sweep.
2. Windows shrink with power (`WindowScaleForPower`, 1.35 → 0.55 at 120 %) from the **peak** backswing, and the DRAWN impact window is the graded one, redrawn every drag frame.
3. Club head hidden at commit (CanvasGroup), back on the next non-flight state — not on a `Flicking` event.
4. Geometry derived: ticks are drawn where the club head LANDS at that power; the lane derives its height from the deepest tick + follow-through + club half-height.
5. Linear-space colours: elements over a known parent pre-composited opaque; veils over turf get a solved alpha; assert tints off the live `Image`.
6. Config-derived distances everywhere (tests, acceptance bot, video runner).
7. **The result readout must not be told about `Resolving`** — `CommitExternal` reaches it synchronously and a shared fading view would drop the analyzer chip two frames after the shot (Needle report §10). Only `Idle` hides it.
8. Per-scheme unique object names (`FreeSwing*`) — find-by-name walks inactive roots (Needle report §10).
9. Frame-rate clamp on any time-based measure (`dt ≤ 1/30`): tempo and speed must survive a hitch frame.

---

## 1. Player-facing behaviour (one continuous touch, no release needed)

1. **Idle.** Club head (the `ClubHandle` clone with `ClubHandleSpriteBinder`) on the ball; a faint swing lane ghost (Figma `Idle`). Straight/Fade-Draw toggle **hidden** in this scheme (decision 3) — the path shapes the shot.
2. **Backswing.** Touch the club head, drag **down** the lane (Figma `Backswing`): 100 % gold and 120 % red ticks, `IMPACT` line at the ball with a green **impact window** on it whose width is Club Accuracy (and shrinks as you pull deeper). A **finger trace** draws behind the finger. `PowerGaugeWidget` shows the peak.
3. **Upswing.** Reverse and drag **up**. The trace continues; the moment the finger **crosses the impact line going up** the shot fires — release afterwards is ignored (decision 5). Lifting the finger during the backswing, or coming back down without crossing = cancel.
4. **Result** (Figma `Result`): club head hidden, and an **analyzer chip** appears above the ball for `FreeSwingAnalyzerSeconds`: `POWER 98%` · `IMPACT ◀ 3 px` · `PATH STRAIGHT|DRAW|FADE` · `TEMPO GOOD|FAST|SLOW` — plus the grade pop for the exceptional cases: **DUFF** (too slow an upswing), **HOOK / SLICE** (impact well outside the window). Clean impact + good tempo = **PURE** pop.
5. **Putt.** Same gesture, lane half height, 100 % cap, no overswing, no path→curve (putts never curve), impact window from Putter Accuracy, tempo window unchanged. `PuttPathPredictor` / `PutterAimLine` untouched.

## 2. Non-goals

Any change to Flick / Pendulum / Tap Timing; physics/resolver; grade SFX (backlog); `freeswing_*` telemetry keys beyond the shared `timing01` (backlog); bot `DriveBot` (`bot_scheme_parity` Stage B); a 3-click variant (backlog).

## 3. Design

### 3.1 Seam (ideally no `ShotController` change)
`BeginExternalDrag(ownsTiming:true)` at pointer-down; `SetExternalPower(peak, 0)` on drag (state → `Timing`, arrow suppressed); `CommitExternal(intent)` on the crossing; `CancelExternalDrag()` on the two cancel paths. No flick gate, no `RejectExternalDrag`. The driver keeps its **own** sample buffer (`FreeSwingSampleWindow` entries of `(canvasPos, unscaledTime)`) — do NOT widen `ShotController.PushTouchSample`, that ring is Flick's gate.
**One small seam is expected:** hiding the Fade-Draw toggle. `ActionButtonsRoot` (or whichever component owns the `FADE/DRAW` `In-Game Select Button`) gets `public void SetFadeDrawVisible(bool)` that sets the button's CanvasGroup alpha 0 + blocksRaycasts false (opacity, not `SetActive` — the row is a layout group and hiding the object recentres SPIN; the same lesson as the Figma frames). The driver calls it on `Activate(false)` / `Deactivate(true)`; if `FadeDrawActive` is armed when Free Swing activates, disarm it first through the existing toggle path so `AimYawFor` stays on the camera heading.

### 3.2 `FreeSwingSchemeDriver` (`Controls/FreeSwing/`, on `SchemeRoot_FreeSwing`)
`MonoBehaviour, IShotSchemeDriver, IPointerDownHandler, IDragHandler, IPointerUpHandler` on `FreeSwingHandle` (the `ClubHandle` clone).
- **Pointer down:** `_origin` (canvas), `BeginExternalDrag(true)`, buffer cleared + first sample, `_phase = Back`, `_peakPull = 0`, `_tReversal = NaN`.
- **Drag (every frame with a sample):** push `(pos, t)`; `pull = origin.y − pos.y` (canvas px);
  - `Back`: `_peakPull = max(_peakPull, pull)`; `power = FreeSwingMath.Power(_peakPull, isPutt)`; `SetExternalPower(power, 0)`; handle follows the finger vertically (clamped to the lane; **lateral follow too** — the path is the point); redraw the impact window from the peak. Reversal = first sample with `pos.y > prev.y` after `_peakPull ≥ MinUsefulPullPx` → `_phase = Up`, `_tReversal = t`, `_reversalPos = pos`.
  - `Up`: if `pos.y` goes back **below** the reversal by more than `FreeSwingReversalSlopPx` (a second backswing) → treat as a new backswing (`_phase = Back`, peak keeps growing). If `pos.y ≥ origin.y` (crossed the impact line): interpolate the crossing point between the last two samples → `xI = crossX − origin.x`; compute tempo, path, speed (§3.4); `Commit()`.
- **Pointer up:** in `Back`, or in `Up` without a crossing → `CancelExternalDrag()` (trace fades). After a commit → ignored.
- **Commit:** `SetExternalPower(peakPower, 0)` (republish the peak — Needle §1 carry-over), hide handle, show analyzer + pop, `CommitExternal(new ShotIntent(peakPower, 0, errorYaw, timingMul, timing01, fadeDraw01))`. Exactly one commit per touch.
- Reset views on the next `Idle` state event.

### 3.3 Views (`FreeSwing/`, unique `FreeSwing*` names; reuse Pendulum/Needle classes where identical)
- `FreeSwingLaneView` — the pill: width 140, radius 70, white 14 % fill (solved alpha over turf) + 3 px 50 % stroke, **clips**; height derived = `FreeSwingFollowThroughPx` above the ball + deepest tick + club half-height + slack. `Tick100` gold / `Tick120` red drawn where the club head lands; labels "100%"/"120%" Rubik Medium 28 outside right. `ImpactLine` 140×6 white at the ball; `ImpactWindow` green `#ADEBAD` 60 % rounded bar on the impact line, width = `ImpactWindowPx × 2 × scale` (driven, redrawn from the peak). Putt: half lane, no 120 tick. Fades with `PendulumFadingView`, **`Idle`-only hide** for the chip (carry-over 7); the lane itself may fade on `Resolving`.
- `FreeSwingTraceView` — a `UILineRenderer`-style mesh (own `Graphic`, like `NeedleArcGraphic`) drawing the buffered samples as an 8 px white round-capped polyline with the navy drop shadow; 85 % alpha while swinging, 60 % on the result, cleared on `Idle`.
- `FreeSwingAnalyzerChip` — 840×150 r32 navy-gradient panel (`S_NeedleResultChip.png` style — bake with `make_needle_sprites.py`'s method, do not reuse the Needle PNG if the size differs) with four columns: label Rubik Medium 24 white 70 % + value Rubik Bold 32 (POWER white, IMPACT/PATH green when clean else amber/red, TEMPO amber when off). All labels/values localised (§3.6). Shown at commit for `FreeSwingAnalyzerSeconds` then fades; **never hidden by `Resolving`**.
- `SchemeGradePop` — reused: `SHOT_GRADE_PURE` green, `SHOT_GRADE_DUFF` red, `SHOT_GRADE_HOOK` / `SHOT_GRADE_SLICE` amber (existing keys).
- `FreeSwingBallRestGhost` = `S_PendulumBallGhost.png`.

### 3.4 Maths (pure static `FreeSwingMath`, tested)
```
Power(pull, isPutt):            Pendulum's piecewise with the FreeSwing* keys; putt caps at 1
WindowScaleForPower(p):         lerp(FreeSwingWindowScaleAtZeroPower, FreeSwingWindowScaleAtMaxPower, p / 1.2)
ImpactWindowPx(acc, p):         lerp(ImpactWindowAtAcc0Px, ImpactWindowAtAcc120Px, acc) × WindowScaleForPower(p)
ImpactYaw(xI, acc, p, halfCone):|xI| ≤ window → 0
                                |xI| ≤ FreeSwingImpactMissPx → (xI / ImpactMissPx) × halfCone × FreeSwingYawGain
                                else → sign(xI) × halfCone × FreeSwingMissYawGain            // HOOK (xI<0) / SLICE (xI>0) pop
Path(samplesUp):                signed mean lateral offset of the upstroke samples from the straight line reversalPos→crossing,
                                as an angle: pathDeg = atan2(meanOffsetPx, upstrokeLengthPx) in degrees (+ = bowed right)
PathDeadzoneDeg(cc):            lerp(PathDeadzoneAtCC0Deg, PathDeadzoneAtCC120Deg, cc)
FadeDraw01(pathDeg, cc, putt):  putt → 0; |pathDeg| ≤ deadzone → 0
                                else clamp(sign × (|pathDeg| − deadzone) / (PathFullDeg − deadzone), −1, 1)
Tempo(tB, tD):                  r = tD / tB;  e = |r − FreeSwingIdealTempo|
TempoWindow(cc, p):             lerp(TempoWindowAtCC0, TempoWindowAtCC120, cc) × WindowScaleForPower(p)
TempoMul(e, w):                 e ≤ w → 1;  e ≤ 2w → lerp(1, TimingPowerMulGold, (e − w)/w);  else TimingPowerMulRed
UpSpeed(samplesUp):             upstroke length / (tCross − tReversal), canvas px/s (dt clamped per sample)
Duff:                           UpSpeed < FreeSwingDuffSpeedPxPerSec → timingMul = TimingPowerMulRed, errorYaw = clamp(2 × ImpactYaw, ±halfCone × MissYawGain), fadeDraw = 0, pop DUFF
timing01:                       1 − clamp(e / (2w), 0, 1)   (DUFF → 0)
Grade pop:                      DUFF > HOOK/SLICE (|xI| > ImpactMissPx) > PURE (impact clean AND tempo e ≤ w) > none (chip only)
```
Sign conventions to verify and state in the report: `xI < 0` (crossed left of the origin) = HOOK = ball left, matching Needle; `FadeDraw01 > 0` must be the same direction Flick's handle-right produces (`FadeDrawWiringTests`).

### 3.5 Config (`controls.csv` + `ControlsConfig.Default` + loader; notes column as Pendulum/Needle)
```
FreeSwingMinUsefulPullPx,40
FreeSwingPull100Px,380
FreeSwingPull120Px,456
FreeSwingFollowThroughPx,160
FreeSwingReversalSlopPx,24
FreeSwingImpactWindowAtAcc0Px,22
FreeSwingImpactWindowAtAcc120Px,60
FreeSwingImpactMissPx,140
FreeSwingYawGain,1.0
FreeSwingMissYawGain,1.5
FreeSwingPathDeadzoneAtCC0Deg,6
FreeSwingPathDeadzoneAtCC120Deg,12
FreeSwingPathFullDeg,30
FreeSwingIdealTempo,0.5
FreeSwingTempoWindowAtCC0,0.25
FreeSwingTempoWindowAtCC120,0.45
FreeSwingDuffSpeedPxPerSec,900
FreeSwingWindowScaleAtZeroPower,1.35
FreeSwingWindowScaleAtMaxPower,0.55
FreeSwingAnalyzerSeconds,1.5
FreeSwingSampleWindow,90
```
(Pull distances equal to Pendulum/Needle so the pull feels the same; separate keys per precedent. `IdealTempo` 0.5 = a downswing half as long as the backswing — TW's "3:1" is a console-stick number; tune on device, ±2 attempts.)

### 3.6 Strings — importer path, EN + JA, then `Import Text CSV` with a forced CSV reimport
| key | EN | JA |
|---|---|---|
| `SHOT_GRADE_PURE` | PURE | ピュア |
| `SHOT_GRADE_DUFF` | DUFF | ダフリ |
| `SWING_POWER` | POWER | パワー |
| `SWING_IMPACT` | IMPACT | インパクト |
| `SWING_PATH` | PATH | 軌道 |
| `SWING_TEMPO` | TEMPO | テンポ |
| `SWING_PATH_STRAIGHT` | STRAIGHT | ストレート |
| `SWING_PATH_DRAW` | DRAW | ドロー |
| `SWING_PATH_FADE` | FADE | フェード |
| `SWING_TEMPO_GOOD` | GOOD | グッド |
| `SWING_TEMPO_FAST` | FAST | 速い |
| `SWING_TEMPO_SLOW` | SLOW | 遅い |
| `SWING_IMPACT_LINE` | IMPACT | インパクト |
`SHOT_GRADE_HOOK` / `SLICE` already exist. Numeric values (`98%`, `◀ 3 px`) are formatted, not localised strings; the `px` unit and the arrows are glyphs in the format string constant, not `.text` literals.

### 3.7 Telemetry
`scheme=3`, `timing01` (tempo), `timing_mul` from the intent. Nothing new.

## 4. Files (expected)
- `Controls/FreeSwing/FreeSwingSchemeDriver.cs`, `FreeSwingMath.cs`, `FreeSwingLaneView.cs`, `FreeSwingTraceView.cs` (+ its `Graphic`), `FreeSwingAnalyzerChip.cs` (new); `ActionButtonsRoot.SetFadeDrawVisible` (small seam)
- `LabScaffold.unity`: `SchemeRoot_FreeSwing` populated; placeholder driver replaced
- `controls.csv`, `ControlsConfig.cs`, `ControlsConfigLoader.cs`; `LocalizationText.csv`
- `Assets/Editor/ShotUI/FreeSwingSchemeBuilder.cs`, `FreeSwingSchemeVerify.cs`, `FreeSwingSchemeVideo.cs` (the Needle pattern)
- Art: chip sprite baked via the token method; no Figma raster exports
- `ShotController.cs`: no change expected

## 5. Tests (EditMode)
1. `FreeSwingMathTests`: power (swing/putt); windows at acc 0/0.5/1 × power 0/1/1.2; impact yaw table (0, ±window, ±(window+ε), ±MissPx, ±200 px); path: straight line → 0, bowed 3° at CC 0 → 0 (deadzone), bowed 20° → the expected FadeDraw01, sign; tempo table (r = 0.5, 0.75, 1.0, 1.5 at CC 0 and 120, power 0/1.2); duff; timing01; putt never curves.
2. `FreeSwingSchemeDriverTests` (scene-less root, `ConfigureForTests`): synthetic gestures as sample sequences — straight down 380 px then straight up through the origin at 2000 px/s → `CommitExternal` once, PURE, `FadeDraw01 = 0`, power 1.0; crossing 40 px right → SLICE-small yaw > 0; bowed upstroke → `FadeDraw01 ≠ 0` with the asserted sign; slow upstroke (400 px/s) → DUFF, Red; lift during backswing → cancel, no commit; up-then-down-then-up (double pump) → one commit with the deeper peak; release after crossing → ignored; hitch frame (0.4 s gap) does not change tempo more than the clamp allows; drawn impact window equals the graded window at the peak; handle hidden in flight, back at Idle; Fade-Draw toggle hidden on Activate and restored on Deactivate; putt caps at 1, no curve.
3. Parity: Flick, Pendulum, Needle suites unchanged and green; full EditMode sweep per assembly.

## 6. Acceptance (real-entry, `freeswing_invariants.json`, like Needle's 133)
- Free Swing selected: no cone, no bar, no arc; Fade-Draw button invisible and untappable, SPIN button not recentred (measure its x).
- Backswing → lane, ticks where the club lands, trace drawing, impact window narrowing as the pull deepens (measure at 0 / 100 / 120 %).
- Straight upswing through the line → shot fires **before** the finger lifts (log the frame delta), PURE pop, chip reads POWER/IMPACT/PATH/TEMPO consistent with the committed intent; chip still fully visible 0.5 s into the flight.
- Crossing off-centre → HOOK / SLICE pops with mirrored yaws; bowed path → the ball visibly curves the stated way; slow upswing → DUFF short and crooked.
- Lift mid-backswing → nothing fires; double pump → one shot at the deeper power.
- Putt: half lane, 100 % cap, no curve ever, `PuttPathPredictor` live.
- Overswing to 120 % speeds nothing (no timing widget) but narrows the windows.
- Club change swaps the handle sprite; hidden in flight; back next shot.
- Flick / Pendulum / Tap Timing pixel-identical to their approved captures.
- `shot_taken`: `scheme=3`, `timing01` = tempo score.
- Strings `--check` clean + table read-back; zero hardcoded `.text`.
- Figma fidelity vs section 3b measured off live RectTransforms; colours within a few RGB after the linear treatment.
- Video: idle → backswing → PURE → SLICE → bowed DRAW → DUFF, captioned from committed values.
- On-device feel pass (Cesar): thumb noise vs deadzone, ideal tempo, duff threshold — the highest-risk scheme; ±2 retunes of §3.5 before a re-spec.

## 7. Out of scope → backlog rows (this session)
Grade SFX; `freeswing_path`/`tempo` telemetry keys; bot `DriveBot` (Stage B); TW 3-click as a fifth scheme (already listed).
