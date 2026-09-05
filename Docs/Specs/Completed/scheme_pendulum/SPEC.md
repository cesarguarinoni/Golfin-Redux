# SPEC — `scheme_pendulum`

**Status:** SPEC_READY (2026-09-05). Spec 1 of the control-schemes track — `Docs/CONTROL_SCHEMES_PLAN.md` §2, decisions §7. Builds on `control_scheme_seam` (`8913901a7`), which must stay byte-identical for Flick.
**Figma:** file `5gEAHjl6xAtW8iYY7NMvWd`, page "Shot Controls — Schemes", section **1b — Pendulum (club handle)** (`14091:33667`) — frames Idle `14091:33668`, Pull `14091:33762`, Timing `14091:33867`, Result JUST `14091:33972`, Putt `14091:34072`. Renders in `reference/`. Cesar 2026-09-05: the handle is the **club head of the selected club** (batch 2), not the ball.
**One line:** a Neko-Golf-style scheme — pull the club straight back for power, a marker swings left↔right on a bar under the ball, flick up when it is on the red centre; the marker offset at the flick is the miss.

---

## 1. Player-facing behaviour

One continuous touch, like Flick.

1. **Idle.** Club head (the selected club's sprite via `ClubHandleSpriteBinder`) rests on the ball, exactly where `ClubHandle` sits today. No cone. Targeting line = camera heading / map target as today (`MapViewController`, `MapTargetCarryM`).
2. **Pull.** Touch the club head, drag **down**. A pull lane fades in under it (Figma `Pull`): 100 % tick (gold) at `PendulumPull100Px`, 120 % tick (red) at `PendulumPull120Px`. Power is the vertical pull only; a lateral drag does nothing in Straight mode. `PowerGaugeWidget` shows % + yards as today.
3. **Pendulum.** The moment power > 0, the bar appears above the ball rest position (Figma `Timing`): navy track, amber GOOD band, green JUST band, red centre pip, white marker. The marker moves sinusoidally (slow at the ends, fast through the middle — the "pause at the edges" of 白猫GOLF). Overpowering (pull past 100 %) speeds it up, less so with Strength.
4. **Flick up.** Release with an upward flick (same gate as Flick). Marker offset `m ∈ [−1, 1]` at the flick → **JUST / GOOD / MISS** pop (Figma `Result`), ball flies. A slow release = **reset, no shot**, same `FlickRejected` toast as Flick (decision 4).
5. **Fade/Draw.** The existing Straight / Fade-Draw toggle is shared (decision 3). When armed, the lateral pull offset becomes the curve amount (the job the handle has in Flick's FadeDraw mode); aim stays locked to the camera heading as in Flick.
6. **Putt.** Same gesture: lane capped at 100 % (no 120 tick, no overpower), bar narrower and slower (Figma `Putt`), windows from Putter Accuracy. `PuttPathPredictor`, `PutterAimLine` and the putter gravity well are untouched.

## 2. Non-goals

Needle and Free Swing (own specs); any change to Flick, `controls.csv` values Flick reads, `StatModifierResolver`, physics; grade SFX (backlog); a `pendulum_grade` telemetry key (backlog — `timing01` carries the distribution).

## 3. Design

### 3.1 Seam additions (tiny, `ShotController`)
- `SetExternalPower`: clamp to `MaxOverpowerNormalized` (1.2) instead of `Clamp01`. Flick's `ClubHandleDragger` never exceeds 1.0, so Flick is unchanged; `ShotControllerSeamParityTests` stays green. Add one test: `SetExternalPower(1.2f, 0)` publishes 1.2; putts still clamp at commit (`CommitExternal` already does).
- `public void RejectExternalDrag()` — for a driver that owns timing: `_externalDragActive = false; FlickRejected?.Invoke(LastFlickSpeedScreenHeights); TransitionToIdle();` (the failed-gate branch of `EndExternalDrag`, exposed).
- Read-only stat seams so the driver carries no resolver plumbing: `public float ClubAccuracyNorm01 => GetClubAccuracyNorm();`, `public int CharacterClubControl => GetStatBundle().Character.ClubControl;`, `public float OverpowerForgiveness01 => Mathf.Clamp01(GetStatBundle().Character.Strength * StatCoefficients.Default.CharStrengthPerPoint.ToFloat());` (same formula the resolver uses for `OverpowerForgivenessFraction`; if `fp.ToFloat()` is not accessible from this assembly, NOTE it and mirror the constant).
- `IShotSchemeDriver.IsImplemented` already exists on `PlaceholderSchemeDriver`; make it part of the interface if it is not, and `ShotSchemeHost.Apply` deactivates `SchemeRoot_Flick` when the active driver reports `IsImplemented == true`. (Needle / Free Swing placeholders keep the Flick fallback.)

### 3.2 `PendulumSchemeDriver` (`Assets/Scripts/Gameplay/UI/ShotUI/Controls/Pendulum/`, on `SchemeRoot_Pendulum`)
`MonoBehaviour, IShotSchemeDriver, IPointerDownHandler, IDragHandler, IPointerUpHandler` — the same three handlers `ClubHandleDragger` uses, on the new `PendulumHandle` Image (a copy of the `ClubHandle` object: same `Image`, same `ClubHandleSpriteBinder`, same rect/anchors, raycast target on).

- `Scheme => Pendulum`, `IsImplemented => true`, `Bind(controller)` stores it, `Activate/Deactivate` toggle the handle + views and reset state.
- **Pointer down:** `_origin = e.position; _controller.PushTouchSample(e.position); _controller.BeginExternalDrag(ownsTiming: true); _phase = 0; _sweeps = 0;`.
- **Drag:** `PushTouchSample`; `pullPx = max(0, origin.y − e.position.y)` in **canvas** px (convert via `RectTransformUtility.ScreenPointToLocalPointInRectangle` on the scheme root, like the dragger does with `_coneRect`, so the lane and the maths share units); `power = PendulumPower(pullPx)` (§3.4); `curve = FadeDrawActive ? clamp((e.position.x − origin.x) / PendulumCurveHalfWidthPx, −1, 1) : 0`; `_controller.SetExternalPower(power, curve)` — `finetune` is only read by the FadeDraw branch of `AimYawFor`/`CommitFlick` today; for the intent we pass `AimOffset01 = 0` and `FadeDraw01 = curve` explicitly (§3.5). Handle sprite follows the finger vertically (clamped to the lane), horizontally only when FadeDraw is armed.
- **Update (while state is `Pulling`/`Timing` and power > 0):** `_phase += Hz * dt` (§3.4), `m = sin(2π·_phase)`; when `_phase` crosses an integer, `_sweeps++`; `_sweeps ≥ PendulumMaxSweeps` → `_controller.CancelExternalDrag()` (safety, mirrors `MaxTotalPasses`). Publish `m` to the bar view.
- **Pointer up:** `PushTouchSample(e.position)`; `if (!_controller.EvaluateFlickGate()) { _controller.RejectExternalDrag(); return; }`; `power ≤ 0.02` → `CancelExternalDrag()`; else grade (§3.5) and `_controller.CommitExternal(intent)`.
- Never calls `EndExternalDrag` (that is the Flick path).

### 3.3 Views (`Pendulum/` folder, all uGUI under `SchemeRoot_Pendulum`, geometry 1:1 from the Figma frames)
- `PendulumLaneView` — the pill (`PowerLane` frame: 120×400 in the club-handle frame, radius 60, white 14 % fill, 3 px 50 % white stroke, **clips** its children), `Tick100` gold `#FFD23A`, `Tick120` red `#FF5A5A`, labels "100%" / "120%" Rubik Medium 28 outside right. Fades in on `Pulling`, out on `Idle`/`Resolving` (reuse `ConeAlphaController`'s fade constants `ConeFadeInSeconds` / `ConeFadeOutSeconds`). Putt: lane 320 tall, no 120 tick.
- `PendulumBarView` — `PendulumTrack` 720×44 r22 navy `#001E39` 78 % + 2 px 35 % white stroke, `BandGood` 288×36 `#FFEBA6` 75 %, `BandJust` 100×36 `#ADEBAD` 90 %, `CentrePip` 10×60 `#FF3B3B`, `PendulumMarker` 56 px white disc, 4 px navy stroke, drop shadow. Band widths are **driven from the windows** (§3.5): `BandJust.width = JustWindow01 × 720`, `BandGood.width = GoodWindow01 × 720` — the Figma numbers are the Acc≈60 case. Putt: 520 wide. Sits 150 px above the ball rest (Figma), fades with the lane.
- `PendulumGradePop` — one `LocalizedText` (Rubik Bold 120, 6 px navy outline, drop shadow) 360 px above the ball: JUST! in `#ADEBAD`, GOOD in `#FFEBA6`, MISS in `#FF5A5A`; scale-in 0.12 s, hold 0.6 s, fade 0.25 s (`TapFeedbackFX` timing constants if they fit; otherwise these). Shown from `CommitExternal` time; hidden on `Idle`.
- The ghost rest marker (`BallRestGhost`, 100 px dashed white 60 %) is optional — include it, it is 1 Image.

### 3.4 Maths (pure static `PendulumMath`, tested)
```
PendulumPower(pullPx):  < MinUsefulPullPx → 0
                        ≤ Pull100Px → (pull − MinUseful) / (Pull100 − MinUseful)            // 0..1
                        else (swing only) → min(1 + (pull − Pull100)/(Pull120 − Pull100) × 0.2, 1.2); putt → 1
Hz(cc, power, forgive01, isPutt):
   base = max(BaseArrowSpeedHzAtCC0 + clamp(cc,0,100) × ArrowSpeedHzPerCC, MinArrowSpeedHz)   // Flick's constants, reused
   over = 1 + max(0, power − 1) × PendulumOverpowerGain × (1 − forgive01)
   putt → base × PuttArrowSpeedMultiplier (no over)
Windows(accNorm):  just = lerp(PendulumJustWindowAtAcc0, PendulumJustWindowAtAcc120, accNorm)
                   good = PendulumGoodWindow01   (fixed; must be > just)
Grade(m, accNorm, halfConeRad):
   |m| ≤ just → JUST : errorYaw 0,                                       timingMul 1
   |m| ≤ good → GOOD : errorYaw m × halfConeRad,                         timingMul TimingPowerMulGold
   else       → MISS : errorYaw sign(m) × halfConeRad × PendulumMissYawGain, timingMul TimingPowerMulRed
   timing01 = 1 − |m|
```
Sign convention: marker right of centre → ball right of aim (`+yaw` is whatever `AimYawFor` treats as right today — verify against `ShotAimParityTests` and state it in the report). `halfConeRad = controller.ConeHalfAngleDeg × Deg2Rad` — Club Accuracy keeps its "error tolerance" job through the same 5°→20° range.

### 3.5 Intent
`new ShotIntent(powerNormalized: power, aimOffset01: 0, errorYawRad, timingMul, timing01, fadeDraw01: curve)`. Spin comes from `PendingSpinInput` inside `CommitExternal` (unchanged). Aim = `AimYawFor(0)` = camera heading in Straight, the locked heading in FadeDraw — identical to Flick with a centred handle.

### 3.6 Config (`controls.csv` + `ControlsConfig` + `ControlsConfigLoader` cases, seed values)
```
PendulumMinUsefulPullPx,40
PendulumPull100Px,300
PendulumPull120Px,360
PendulumOverpowerGain,1.0
PendulumJustWindowAtAcc0_01,0.08
PendulumJustWindowAtAcc120_01,0.20
PendulumGoodWindow01,0.45
PendulumMissYawGain,1.5
PendulumCurveHalfWidthPx,150
PendulumMaxSweeps,10
```
Both mirrors (csv + `ControlsConfig.Default`) per the F13 rule. ±2 tuning attempts on device before flagging.

### 3.7 Strings — importer path, EN + JA in the same commit
| key | EN | JA |
|---|---|---|
| `SHOT_GRADE_JUST` | JUST! | ジャスト! |
| `SHOT_GRADE_GOOD` | GOOD | グッド |
| `SHOT_GRADE_MISS` | MISS | ミス |

`import_content.py --catalogs texts` PLAN → `--apply` → publish → `export_content.py --check` clean → run `Tools/Localization/Import Text CSV` (the seam report's lesson: the runtime reads the generated table). Zero hardcoded `.text`.

### 3.8 Telemetry
Nothing new: `shot_taken` already carries `scheme=1`, `timing01`, `timing_mul`; `timing_band` is derived from `timing01` by the existing bands and is *approximately* the grade. Report the mapping once (JUST → green? depends on `TimingBandGreenY01`) so the dashboard reader knows.

## 4. Files (expected)
- `ShotController.cs` (§3.1 only), `ShotSchemeHost.cs` (`IsImplemented` rule), `IShotSchemeDriver.cs`
- `Controls/Pendulum/PendulumSchemeDriver.cs`, `PendulumMath.cs`, `PendulumLaneView.cs`, `PendulumBarView.cs`, `PendulumGradePop.cs` (new)
- `LabScaffold.unity`: `SchemeRoot_Pendulum` populated (handle copy of `ClubHandle` + views), `PlaceholderSchemeDriver` replaced by `PendulumSchemeDriver`
- `Assets/Resources/Gameplay/controls.csv`, `ControlsConfig.cs`, `ControlsConfigLoader.cs`
- `Assets/Localization/LocalizationText.csv`
- Art: none new — sprites are flat uGUI (`S_Common_BGCorner8`-style sliced rounded rects or `Image` + `Mask`), the club sprites via the binder. If a rounded-rect sprite of the right radius is missing, bake one with the `UI_ELEMENT_PALETTE.md` token method, not a Figma export.

## 5. Tests (EditMode)
1. `PendulumMathTests`: power curve at 39/40/300/330/360/400 px (swing + putt); Hz at cc 0/50/100/120 with power 1.0/1.2 and forgive 0/0.75; putt Hz; windows at acc 0/0.5/1; grade table: m = 0, ±just, ±(just+ε), ±good, ±1 → the expected (errorYaw, timingMul, timing01); sign convention.
2. `PendulumSchemeDriverTests` (driver on a scene-less root with `ShotController` + `ConfigureForTests`): down → drag 200 px → state `Timing`, power published; synthetic fast up-flick samples → `CommitExternal` fires `OnShotResolved` once with `scheme` intent values; slow release → `FlickRejected` + Idle, no resolve; `PendulumMaxSweeps` → cancel; putt path clamps at 1.0 and skips overpower; FadeDraw armed → `FadeDraw01 == curve`, Straight → 0.
3. Seam: `SetExternalPower(1.2)` publishes 1.2; **all existing shot tests unchanged and green**; `ShotSchemeHost` deactivates `SchemeRoot_Flick` for an implemented driver and keeps it for placeholders.
4. Full EditMode sweep per assembly, zero new failures.

## 6. Acceptance
- Lab + Lomond 2 with Pendulum selected: no cone, no arrows; pull → lane + bar; flick on the pip → JUST!, full power, straight; flick at the band edge → GOOD with a visible yaw; outside → MISS, short and crooked; slow release → toast + reset, marker never advances the sweep count past `PendulumMaxSweeps`.
- Flick selected: pixel-identical to `control_scheme_seam`'s `lab_idle/lab_timing` screenshots; parity suite green.
- Putt: 100 % cap, slower marker, `PuttPathPredictor` line still updates with power.
- Club change (`ClubSelectionBroadcast.OnClubChanged`) swaps the handle sprite on the Pendulum handle exactly as on `ClubHandle`.
- Overpower: 120 % pull visibly speeds the marker on a Strength-0 character and barely on Strength 120 (log the Hz).
- `shot_taken` from a Pendulum shot: `scheme=1`, `timing01` = 1 − |m| (log both).
- `--check` clean; grep quoted; Figma fidelity vs section 1b frames (Element Reuse Map + `figma_diff.py`).
- On-device feel pass (Cesar): sinusoid vs linear marker, bar height, window sizes — ±2 retunes of §3.6 before a re-spec.

## 7. Out of scope → backlog rows (this session)
Grade SFX (JUST/GOOD/MISS chimes — CC0 placeholders sourced by the Architect when taken up); `pendulum_grade` telemetry key; bot parity (bots stay on `CommitFlick`, `TimingMul = 1`).
