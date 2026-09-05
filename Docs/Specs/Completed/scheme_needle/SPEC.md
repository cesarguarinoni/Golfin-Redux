# SPEC — `scheme_needle` (player-facing name: **Tap Timing**)

**Status:** SPEC_READY (2026-09-05). Spec 2 of the control-schemes track — `Docs/CONTROL_SCHEMES_PLAN.md` §3, decisions §7. Builds on `control_scheme_seam` (`8913901a7`) and reuses `scheme_pendulum` (`501bf5881`, DONE) wherever the two schemes share a phase. Flick and Pendulum must stay byte-identical.
**Figma:** file `5gEAHjl6xAtW8iYY7NMvWd`, page "Shot Controls — Schemes", section **2b — Needle (club handle)** (`14091:102411`) — frames Aim `14091:102515`, Pull `14091:102612`, Timing `14091:102412`, Result PERFECT `14091:102712`, Putt `14091:102814`. Renders in `reference/`.
**One line:** Golf-Clash-style — pull the club back inside a power circle and **release**; a needle sweeps an arc above the ball **once**; **tap** to stop it. Blue zone = PERFECT, early = HOOK (left), late = SLICE (right), no tap = SHANK.

**Carry-overs from `scheme_pendulum` (Cesar's review rounds, 2026-09-05) — apply from the first build, not after review:**
1. The needle has its **own** speed constants, trackable by eye (≤ 1 full sweep per ~1.2 s at CC 0). Never reuse the flick's arrow line or Pendulum's Hz.
2. **The target shrinks with power:** the perfect and good zones scale by `WindowScaleForPower` (1.35 at power 0 → 0.55 at 120 %), and the DRAWN zones read the same number every drag frame, from the PEAK power, so the player is judged against the target they watched close.
3. **The club head hides while the ball is in flight** (CanvasGroup at commit, back on the next non-flight state — not on a `Flicking` event, which is never published).
4. **Geometry is derived, not authored:** a ring is drawn where the club head LANDS at that power; the circle radius derives from the deepest ring + club half-height.
5. **Colours:** Unity blends linear, Figma composites sRGB. Elements over a known parent (zones on the arc, arc fill) are pre-composited opaque at the reference render's pixels; translucent veils over turf (the power rings) get a solved alpha fitted to turf. Assert tints off the live `Image`.
6. Commit from **peak** pull, never the live value; latch at the upswing reversal is N/A here (release ends the pull — there is no up-flick).
7. Every hard-coded pull distance in tests, bots and the video runner derives from config.

---

## 1. Player-facing behaviour (two touches)

1. **Aim.** Camera heading / map target as today (`MapTargetCarryM`); on the course the targeting line and the landing ring (`RingFrac`, radius from Club Accuracy) already exist — no new aim UI. Spin via the existing selector. Straight / Fade-Draw toggle shared (decision 3): when armed, the lateral pull offset is the curve amount, as in Pendulum.
2. **Pull.** Touch the club head (same `ClubHandle` clone as Pendulum), drag **down** inside a **power circle** centred on the ball (Figma `Pull`): rings at 80 % (white), 100 % (gold) and 120 % (red), plus a red overpower crescent between the 100 and 120 rings in the bottom arc. `PowerGaugeWidget` shows % + yards. Club head follows the finger; power = vertical pull (`NeedlePull*Px`); lateral = curve only when Fade-Draw is armed.
3. **Release.** Lifting the finger **commits the power** (peak) and starts the swing phase: the club head snaps back to the ball, the rings fade to 25 %, and the **accuracy arc** (Figma `Timing`) appears above the ball — navy 180° arc, amber GOOD zone, blue PERFECT zone at the top, and a white needle that starts at the left end and sweeps to the right end **once**. "TAP!" hint under the arc. Releasing inside `PullStartThresholdPx` = no shot (as today). A release with power ≤ 0.02 → cancel.
4. **Tap.** Anywhere on the shot area (the whole `Shoot Controls` rect, not just the handle). Needle position `n ∈ [−1, 1]` (0 = top / centre):
   - `|n| ≤ PerfectZone` → **PERFECT**: `ErrorYaw = 0`, `TimingMul = 1`.
   - `|n| ≤ GoodZone` → **GOOD** (pop reads **HOOK** if `n < 0`, **SLICE** if `n > 0`): `ErrorYaw = n × halfCone × NeedleYawGain`, `TimingMul = lerp(1, TimingPowerMulGold, |n| / GoodZone)`.
   - else → **HOOK / SLICE** (big): `ErrorYaw = sign(n) × halfCone × NeedleMissYawGain`, `TimingMul = TimingPowerMulGold`.
   - **No tap** before the needle reaches the right end → **SHANK**: `ErrorYaw = +halfCone × NeedleMissYawGain`, `TimingMul = TimingPowerMulRed`.
   - `timing01 = 1 − |n|` (SHANK: 0). Needle colour white → amber → red by `|n|` while sweeping (Golf Clash cue).
5. **Result.** The needle freezes where tapped, a tap pip marks the arc, the grade pop shows (Figma `Result`), ball flies, club head hidden until the next shot.
6. **Putt.** Same two touches; 100 % ring only (no overpower crescent, no 120 ring), arc flattened (460×300 ellipse per the Putt frame), needle slower by `PuttArrowSpeedMultiplier`, zones from Putter Accuracy. `PuttPathPredictor` / `PutterAimLine` untouched.

## 2. Non-goals

Free Swing (own spec); any change to Flick / Pendulum behaviour or constants; physics/resolver; grade SFX (backlog); `needle_grade` telemetry key (backlog); Golf-Clash-style drag-the-landing-marker aiming (map view already covers it).

## 3. Design

### 3.1 State machine mapping (seam, no additions expected)
`BeginExternalDrag(ownsTiming: true)` on pointer-down; `SetExternalPower(power, curve)` on drag (state → `Timing` once power > 0 — the controller's `Timing` covers both our Pull and our Needle phases; the driver keeps its own `_phase ∈ {Pull, Needle, Done}`); on release the driver stays in the controller's `Timing` state (it owns timing, so `TickArrow` is skipped) and runs the needle; `CommitExternal(intent)` on tap or on SHANK timeout; `CancelExternalDrag()` on a zero-power release. `RejectExternalDrag()` is not used (no flick gate in this scheme). If any seam gap appears (e.g. `ShotInProgressUiGate` closing the tap area on release), NOTE it and add the smallest public seam — do not special-case inside the driver.

### 3.2 `NeedleSchemeDriver` (`Assets/Scripts/Gameplay/UI/ShotUI/Controls/Needle/`, on `SchemeRoot_Needle`)
`MonoBehaviour, IShotSchemeDriver, IPointerDownHandler, IDragHandler, IPointerUpHandler` on the `NeedleHandle` (clone of `ClubHandle` with `ClubHandleSpriteBinder`, as Pendulum), **plus** a full-rect invisible `NeedleTapCatcher` Image (raycast target, alpha 0) over `Shoot Controls` that is enabled only during the needle phase and forwards `IPointerDownHandler` to `OnTap()`.
- `Scheme => Needle`, `IsImplemented => true`.
- **Pointer down (handle):** `_origin`, `BeginExternalDrag(true)`, `_peakPower = 0`, `_phase = Pull`.
- **Drag:** `pullPx` in canvas px (same conversion as Pendulum); `power = NeedleMath.Power(pullPx, isPutt)`; `curve` as Pendulum when Fade-Draw armed; `_peakPower = max`; `SetExternalPower(power, curve)`; handle follows; rings/crescent/zones redraw from **peak** power (`WindowScaleForPower`).
- **Pointer up:** `_peakPower ≤ 0.02` → `CancelExternalDrag()`; else `_phase = Needle`, `_needle = −1`, tap catcher on, handle returns to rest (0.15 s), rings fade to 25 %, arc + needle shown.
- **Update (Needle phase):** `_needle += 2 × dt / NeedleSweepSeconds(cc, peakPower, forgive, isPutt)`; needle colour by `|_needle|`; `_needle ≥ 1` → `Commit(grade = SHANK)`.
- **OnTap:** `Commit(NeedleMath.Grade(_needle, accNorm, peakPower, halfCone))`.
- **Commit:** freeze needle, place `TapPip`, show `GradePop`, hide handle (CanvasGroup), tap catcher off, `CommitExternal(new ShotIntent(peakPower, 0, errorYaw, timingMul, timing01, curve))`.
- Reset on the next `Idle` state event (arc hidden, rings hidden, handle back).

### 3.3 Views (`Needle/`, uGUI under `SchemeRoot_Needle`, geometry 1:1 from Figma; reuse the Pendulum view classes where the element is the same)
- `NeedlePowerCircleView` — three ring Images (sliced circle sprites or `Image` + `Mask`), radii **derived**: `r80/r100/r120 = HandleRestBelowBall + NeedlePull{80,100,120}Px` (where the club head lands), stroke 3/4/3 px, white 25 % / gold `#FFD23A` 35 % / red `#FF5A5A` 25 % as translucent veils over turf (solved alpha, carry-over 5); `OverpowerCrescent` = filled arc between r100 and r120 spanning ±34° around the bottom, `#FF5A5A` at a solved alpha; `Label100` "100%" Rubik Medium 28 gold at +120 px right of the 100 ring's bottom. Appears on `Pulling`, fades to 25 % on release, off on `Idle`. Putt: r100 ring only.
- `NeedleArcView` — `AccuracyArc` 460×460 half-ring, thickness 44, navy `#001E39` (pre-composited over turf → treat as opaque `#13313A`-class value read from the render), 2 px white 35 % stroke; `ZoneGood` amber `#FFEBA6` and `ZonePerfect` blue `#4DA3FF` as **arc segments whose angular width is driven** by the windows (`PerfectZone × 90°` half-angle each side of the top; `GoodZone × 90°`), pre-composited opaque over the arc; `Needle` 10×240 white rounded bar pivoting at the ball centre (rotation = `n × 90°`), drop shadow; `NeedleHub` 36 px white disc with navy ring; `TapHint` "TAP!" Rubik Medium 44 white with the navy drop shadow, 90 px below the ball; `TapPip` 28 px white disc with navy ring placed on the arc at the tapped angle. Putt: arc 460×300 (ellipse), needle 160.
- `GradePop` — reuse `PendulumGradePop` unchanged (it reads a key + colour); keys `SHOT_GRADE_PERFECT` blue `#4DA3FF`, `SHOT_GRADE_HOOK` / `SHOT_GRADE_SLICE` amber `#FFEBA6`, `SHOT_GRADE_SHANK` red `#FF5A5A`. If the class is Pendulum-named, rename to `SchemeGradePop` in the same commit (both drivers use it); no behaviour change.
- `BallRestGhost` — reuse `S_PendulumBallGhost.png`.
- Fading: reuse `PendulumFadingView` for the circle and the arc.

### 3.4 Maths (pure static `NeedleMath`, tested)
```
Power(pullPx, isPutt):        same piecewise as PendulumMath.Power with the Needle* keys; putt caps at 1
WindowScaleForPower(p):       lerp(NeedleWindowScaleAtZeroPower, NeedleWindowScaleAtMaxPower, p / 1.2)   // same shape as Pendulum
PerfectZone(acc, p):          lerp(NeedlePerfectZoneAtAcc0, NeedlePerfectZoneAtAcc120, acc) × WindowScaleForPower(p)
GoodZone(p):                  max(NeedleGoodZone01 × WindowScaleForPower(p), PerfectZone + 0.02)
SweepSeconds(cc, p, f, putt): base = max(NeedleSweepSecAtCC0 + clamp(cc,0,100) × NeedleSweepSecPerCC, NeedleMinSweepSec)
                              over = 1 + max(0, p − 1) × NeedleOverpowerGain × (1 − f)      // faster when overpowered, Strength buys it back
                              putt → base / PuttArrowSpeedMultiplier (slower), no over;  swing → base / over
Grade(n, acc, p, halfCone):   |n| ≤ Perfect → PERFECT (0, 1)
                              |n| ≤ Good    → HOOK|SLICE-small: (n × halfCone × NeedleYawGain, lerp(1, Gold, |n|/Good))
                              else          → HOOK|SLICE-big:   (sign(n) × halfCone × NeedleMissYawGain, Gold)
                              SHANK (timeout): (+halfCone × NeedleMissYawGain, Red); timing01 = 0
                              timing01 = 1 − |n|
```
Sign: `n < 0` (left of top, early) = HOOK = ball left; `n > 0` = SLICE = ball right — verify against `ShotAimParityTests`' yaw convention and state it in the report.

### 3.5 Config (`controls.csv` + `ControlsConfig.Default` + loader cases; seed values; notes column explains each, as Pendulum's do)
```
NeedleMinUsefulPullPx,40
NeedlePull80Px,304
NeedlePull100Px,380
NeedlePull120Px,456
NeedleOverpowerGain,1.0
NeedlePerfectZoneAtAcc0_01,0.08
NeedlePerfectZoneAtAcc120_01,0.20
NeedleGoodZone01,0.40
NeedleYawGain,1.0
NeedleMissYawGain,1.5
NeedleCurveHalfWidthPx,150
NeedleSweepSecAtCC0,1.2
NeedleSweepSecPerCC,0.006
NeedleMinSweepSec,0.8
NeedleWindowScaleAtZeroPower,1.35
NeedleWindowScaleAtMaxPower,0.55
```
(Pull distances seeded equal to Pendulum's so the pull feels the same across schemes; kept as separate keys per the Pendulum precedent.)

### 3.6 Strings — importer path, EN + JA in the same commit, then `Tools ▸ Localization ▸ Import Text CSV` with a forced reimport of the CSV asset (Pendulum report §5)
| key | EN | JA |
|---|---|---|
| `SHOT_GRADE_PERFECT` | PERFECT | パーフェクト |
| `SHOT_GRADE_HOOK` | HOOK | フック |
| `SHOT_GRADE_SLICE` | SLICE | スライス |
| `SHOT_GRADE_SHANK` | SHANK | シャンク |
| `SHOT_TAP_HINT` | TAP! | タップ! |

### 3.7 Telemetry
Nothing new: `scheme=2`, `timing01`, `timing_mul` from the intent. Report the timing01→band mapping once.

## 4. Files (expected)
- `Controls/Needle/NeedleSchemeDriver.cs`, `NeedleMath.cs`, `NeedlePowerCircleView.cs`, `NeedleArcView.cs`, `NeedleTapCatcher.cs` (new); `PendulumGradePop` → `SchemeGradePop` (rename only, if done)
- `LabScaffold.unity`: `SchemeRoot_Needle` populated; `PlaceholderSchemeDriver` replaced
- `controls.csv`, `ControlsConfig.cs`, `ControlsConfigLoader.cs`; `LocalizationText.csv`
- Art: ring/arc sprites baked with the palette token method (`UI_ELEMENT_PALETTE.md`), no Figma raster exports (they come back opaque on this file — seam report §4)
- `ShotController.cs`: ideally **no change**; any seam gap goes through the smallest public addition + a NOTE.

## 5. Tests (EditMode)
1. `NeedleMathTests`: power at 39/40/304/380/456/500 px (swing + putt); sweep seconds at CC 0/50/100/120 with p 1.0/1.2 and forgive 0/0.75, putt slower; zones at acc 0/0.5/1 × power 0/1/1.2 (shrink); grade table n = 0, ±perfect, ±(perfect+ε), ±good, ±1, SHANK → expected (errorYaw, timingMul, timing01), sign convention.
2. `NeedleSchemeDriverTests` (scene-less root, `ConfigureForTests` like Pendulum's): down → drag 380 px → release → arc phase, handle at rest, catcher on; tap at n = 0 → `CommitExternal` once, PERFECT intent values; tap at n = 0.9 → SLICE-big; no tap → SHANK exactly once at `_needle ≥ 1`; zero-power release → cancel, no arc; drag past the origin on release still commits **peak** power; putt caps at 1 and ignores overpower; Fade-Draw armed → `FadeDraw01 == curve`; drawn zone angles equal the graded windows at the peak power (`TheGradedWindow_IsTheOneThatWasDrawn` pattern); handle hidden in flight and back at the next Idle.
3. Parity: Flick and Pendulum suites unchanged and green; full EditMode sweep per assembly, zero new failures.

## 6. Acceptance (real-entry, like Pendulum's 47-item invariant JSON — produce `needle_invariants.json`)
- Lab + Lomond 2 with Tap Timing selected: no cone, no bar; pull → rings + crescent; release → arc + needle, club head back on the ball; tap on the blue → PERFECT, full power, straight; tap early → HOOK left with visible yaw; late → SLICE right; no tap → SHANK short + right; the needle is trackable by eye (one sweep ≥ 1.0 s at CC 0, logged).
- Zones visibly narrow as the pull deepens (measure blue arc length at 0 %, 100 %, 120 %).
- Overpower speeds the needle on a Strength-0 character and barely on Strength 120 (log sweep seconds).
- Putt: 100 % ring only, flat arc, slower needle; `PuttPathPredictor` still updates with power.
- Club change swaps the handle sprite; club head hidden while the ball is in flight, back for the next shot.
- Flick and Pendulum selected: pixel-identical to their last approved screenshots.
- `shot_taken`: `scheme=2`, `timing01 = 1 − |n|` (0 on SHANK).
- Strings: `--check` clean + table read-back; zero hardcoded `.text` (grep quoted).
- Figma fidelity vs section 2b (measured off live RectTransforms as Pendulum did); colours within a few RGB of `reference/` after the linear-space treatment.
- Video: one clip through the real entry path — idle → pull to 120 % → PERFECT → HOOK → SHANK — for the daily report.

## 7. Out of scope → backlog rows (this session)
Grade SFX; `needle_grade` telemetry key; bot executor (`bot_scheme_parity` Stage B adds `DriveBot` here after Pendulum's); drag-the-landing-marker aiming.
