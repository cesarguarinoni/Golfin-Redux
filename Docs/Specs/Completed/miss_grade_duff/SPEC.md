# SPEC — `miss_grade_duff`

> **Authoritative spec for this task.** Implementer reads this and ONLY this for the work definition. STATUS.md tracks pipeline state. Reports/reviews go in their own files.
> Written 2026-09-07 (Architect, Cowork). Project mirror: `claude/MISS_GRADE_DUFF_SPEC.md`. **Run AFTER `shot_view_layout`** — §3.6 adds `FlickGradePop` under `SchemeRoot_Flick` in `LabScaffold.unity`, which that task re-parents. (If this one must go first, add `FlickGradePop` to `shot_view_layout` §3.2's Flick list.)

## Status

See `STATUS.md`. Starts at `SPEC_READY`.

## Goal

A missed swing must **feel** missed. Today every graded miss — Pendulum **MISS**, Needle **SHANK**, Free Swing **DUFF**, and a Flick whose aim latched at the very bottom of the cone — still launches the ball at **70 %** power (`TimingPowerMulRed`) on the club's normal loft, thrown 1.5 half-cones wide. That is a bad shot, not a miss. Cesar (2026-09-07): *"Missing should miss completely or hit the ball very badly and unpowered (red indicators)."*

This task turns every miss into a **duff**: the ball is topped — a fraction of the power, a low flat launch, the wide yaw it already has — with the red grade pop the schemes already show plus a red flash on the power gauge. Strokes are counted as today. No whiff (air shot) in this version: without a golfer animation to sell it, a ball that does not move reads as a bug (D1).

Two more things Cesar asked for in the same breath, same task: **Flick gets shot-quality pops** like the other three schemes (it has none today), and the grade **vocabulary is unified** across all four schemes in real golf terms — no more JUST (a 白猫GOLF / JP-game word, not golf).

## 1. Decisions (Cesar, 2026-09-07)

| # | Decision |
|---|---|
| D1 | **Duff, not whiff.** Ball moves a few yards. Whiff is backlogged as an option for when a 3D golfer animation exists. |
| D2 | **Scope = every miss grade in every scheme:** Pendulum `MISS`, Needle `SHANK`, Free Swing `DUFF`, **and the Flick red zone** (aim latched below a new `TimingBandRedY01` line at the bottom of the cone). Needle's big HOOK/SLICE and Free Swing's big HOOK/SLICE **stay real shots** (Golf Clash / TrueSwing behaviour — deliberate, they are shaped misses, not fluffed contact). |
| D3 | **Putt miss = duff at 30 % of intended distance, on the miss yaw, never whiff.** No launch-pitch change on putts (putter loft is ~3°, there is nothing to top). |
| D4 | Miss **yaw is unchanged** (`*MissYawGain` 1.5 × half-cone). Only power and launch change. Consequence: `BotSchemeSigmaCalibrator` bisects on `MeanAbsYawDeg`, so **bot sigma calibration is untouched** and `bot_difficulty.csv` must not change. Bots that draw a MISS duff exactly like players — that is the parity `bot_scheme_parity` wanted. |
| D5 | Red indicators = the grade pop (DUFF, red — Flick gets its own pop, §3.6) + a **red flash of the power-gauge arc** showing the resolved (duffed) power for the pop's duration. |
| D6 | `TimingPowerMulRed` (0.70) **keeps its job** as the bottom of the Flick *ramp*; the new `MissPowerMul` is a separate, flat value for the duff zone. Nothing above the red line changes for Flick. |
| D7 | **One grade vocabulary, real golf terms, all four schemes:** `PURE` (flush contact) · `GOOD` · `HOOK` / `SLICE` (shaped near-miss, Needle + Free Swing only) · `THIN` (weak contact — Flick's red→gold ramp zone only) · `DUFF` (fat, the miss of D2). JP: ピュア · グッド · フック · スライス · トップ · ダフリ — トップ and ダフリ are the real Japanese terms for thin and fat. Retired: JUST, PERFECT, SHANK, MISS. |
| D8 | **One grade colour set = the cone bands the Flick player already reads:** PURE green `#ADEBAD`, GOOD/HOOK/SLICE/THIN amber `#FFEBA6`, DUFF red `#FF5A5A` (`ConeBandPalette`). The Needle's blue perfect zone (`#4DA3FF`) recolours to the PURE green so zone and pop agree. Cesar may veto the recolour — flag it in the report as a one-constant change. |

## 2. Config — `ControlsConfig` + `Assets/Resources/Gameplay/controls.csv`

New rows (fields in `ControlsConfig`, seeds in `Default`, cases in `ControlsConfigLoader`):

```
MissPowerMul,0.20,miss_grade_duff (2026-09-07): flat power multiplier for a DUFF — Pendulum MISS, Needle SHANK, Free Swing DUFF, Flick aim latched below TimingBandRedY01. Replaces TimingPowerMulRed (0.70) for those cases only. A topped ball dribbles ~20 % of the intended carry. Cesar tunes.
PuttMissPowerMul,0.30,same, on the green (D3). Never lower than a putt can still visibly roll.
MissLaunchPitchScale,0.35,launch pitch = club loft × this on a DUFF (full swing only): a topped ball flies low and flat. 1.0 = today. Putts unaffected.
TimingBandRedY01,0.15,slab progress (0 = cone base, 1 = apex) at the RED band line of the Flick cone. At/below it the flick is a DUFF (MissPowerMul + MissLaunchPitchScale). Above it the existing ramp TimingPowerMulRed→Gold→1 applies, re-based to start at this line instead of 0. Drawn by ConeBandPalette.BandRedY01 (moved out of the const, same F15 pattern as Gold/Green — the line the player reads and the penalty they pay share one number).
```

Append to the `TimingPowerMulRed` note: *"miss_grade_duff: now the multiplier AT TimingBandRedY01, not at 0 — below the red line the shot is a DUFF at MissPowerMul."* Update the header comment about the confirm tiles: **no** UI geometry changes here → no recapture (the red band line moving to 0.15 is on the Flick cone, which has no tiles).

## 3. Implementation

### 3.1 One duff carrier through the seam — `ShotIntent` + `ResolveAndPublish`

- `ShotIntent` (`Assets/Scripts/Gameplay/Input/ShotIntent.cs`) gains `public readonly bool IsMiss;` (constructor overload with a default `false` so every existing call site compiles unchanged).
- `ShotController.ResolveAndPublish(...)` gains `float launchPitchScale` (default 1). It passes it to `ShotInputBuilder.Build` (§3.2). `LastShotWasMiss` public getter set alongside `LastTimingPowerMul` for the gauge (§3.5) and tests.
- `CommitExternal(in ShotIntent i)`: `launchPitchScale = i.IsMiss && !IsPutt ? _config.MissLaunchPitchScale : 1f`. The multiplier itself is already in `i.TimingMul` (schemes set it, §3.3) — do not recompute it here.
- `CommitFlick()` (Flick path): `TimingPowerMultiplier()` becomes `TimingPowerMultiplier(out bool isMiss)`:

```csharp
float t = Mathf.Clamp01(_timingAtLatch);
float red = _config.TimingBandRedY01, gold = _config.TimingBandGoldY01, green = _config.TimingBandGreenY01;
if (t >= green) { isMiss = false; return 1f; }
if (t >= gold)  { isMiss = false; return Mathf.Lerp(_config.TimingPowerMulGold, 1f, (t - gold) / Mathf.Max(1e-4f, green - gold)); }
if (t >= red)   { isMiss = false; return Mathf.Lerp(_config.TimingPowerMulRed, _config.TimingPowerMulGold, (t - red) / Mathf.Max(1e-4f, gold - red)); }
isMiss = true;  return IsPutt ? _config.PuttMissPowerMul : _config.MissPowerMul;
```

  `DebugFlags.ForcePerfectTiming` / `NaN` latch → `isMiss = false, 1f` as today (bots and programmatic drivers on the Flick path never duff — D4 of the seam holds).

### 3.2 `ShotInputBuilder.Build` (`Assets/Scripts/Physics/Stats/ShotInputBuilder.cs`)

One new trailing optional parameter `fp launchPitchScale = default` (0 → treated as 1, the same legacy-no-op convention every other optional there uses). Line 74: `launchPitchRadians = loftDeg * DegToRad * scale`. Clamp the result to `[2°, loft]` so a duff still leaves the ground on a deterministic path. Deterministic fixed-point (`fp`) like everything else in the builder — the tournament `ShotCommand` replays this. **NOTE (Code):** confirm `ShotCommand` serialises everything `Build` consumes; if the replay side rebuilds from `flickMag/aimYaw` only, the pitch scale must ride in the command too, else a duff replays as a full-loft shot on the opponent's device. Report what you found.

### 3.3 Scheme graders (pure math, `Golfin.Gameplay.UI.Controls`)

| Grader | Change |
|---|---|
| `PendulumMath.Grade` — MISS branch | `TimingMul = isPutt ? cfg.PuttMissPowerMul : cfg.MissPowerMul` (was `TimingPowerMulRed`). `Grade` already takes `power`; add `bool isPutt` (the driver knows `IsPutt`). Verdict gains `IsMiss = true` on this branch only. |
| `NeedleMath.Shank` | same substitution; `IsMiss = true`. `Grade`'s big HOOK/SLICE branch **unchanged** (`TimingPowerMulGold`, D2). |
| `FreeSwingMath.Grade` — DUFF exit | same substitution; `IsMiss = true`. Big HOOK/SLICE branch unchanged. |
| Drivers (`PendulumSchemeDriver`, `NeedleSchemeDriver`, `FreeSwingSchemeDriver`) | copy `verdict.IsMiss` into the `ShotIntent` they hand to `CommitExternal`. No other driver change. |

Each `Verdict` struct gets `public readonly bool IsMiss;`. Grade keys, pops and telemetry `timing01` unchanged.

### 3.4 Cone drawing — `ConeBandPalette.BandRedY01`

`const float BandRedY01 = 0.00f` becomes a value fed from `ControlsConfig.TimingBandRedY01`, exactly as `BandGoldY01` / `BandGreenY01` were moved in F15 (`shot_timing_power`). `ConeMeshGraphic` / `TimingSlabGraphic` draw the red line at 0.15 instead of the base; band colours unchanged. Acceptance: the drawn red line and `TimingBandRedY01` are one number (Rule D3 of F15).

### 3.5 Red indicator — `PowerGaugeWidget`

On `ShotController.OnShotResolved`, if `LastShotWasMiss`: tint the gauge arc + `PctText` red (`ConeBandPalette` red `#FF3B3B`), show the **resolved** percentage (`PowerNormalized × TimingMul`, e.g. "24 %"), hold for `SchemeGradePop`'s display duration (reuse its constant — do not add a new one), then restore the idle look. **NOTE (Code):** `PowerGaugeWidget` does not subscribe to `OnShotResolved` today — find how it learns the shot ended (it subscribes to `OnStateChanged`; `Resolving` is the moment) and hook there. Flick gets this flash and nothing else (D5).

### 3.6 Unified grades + Flick pop (D7, D8)

**Keys.** Every grader maps to the unified key set. Old keys are retired, not aliased:

| Scheme | Today | Unified |
|---|---|---|
| Pendulum | JUST / GOOD / MISS | `SHOT_GRADE_PURE` / `SHOT_GRADE_GOOD` / `SHOT_GRADE_DUFF` |
| Needle | PERFECT / HOOK / SLICE / SHANK | `PURE` / `HOOK` / `SLICE` / `DUFF` |
| Free Swing | PURE / HOOK / SLICE / DUFF (+ None) | unchanged |
| Flick | — (no pop) | `PURE` (t ≥ green) / `GOOD` (gold ≤ t < green) / `THIN` (red ≤ t < gold) / `DUFF` (t < red); no pop when the flick gate rejects (that is a reset, not a shot) |

Enums stay as they are in code (`PendulumGrade.Just`, `NeedleGrade.Shank` …) — renaming enum members touches telemetry-adjacent code for no player value; only `GradeKey()` and the pop colour switch change. Add `FlickGrade { Pure, Good, Thin, Duff }` + `FlickMath.GradeKey/Grade(t, cfg)` (pure static, next to `TimingPowerMultiplier`'s thresholds — the same three numbers, no duplicates).

**Strings — through the two-way importer, EN + JA in the same commit:** add `SHOT_GRADE_THIN,THIN,トップ`; edit `SCHEME_POPUP_PENDULUM_LINE3` ("Green = PURE, amber = GOOD, outside = DUFF" / 緑＝ピュア、黄＝グッド、外＝ダフリ) and `SCHEME_POPUP_NEEDLE_LINE3` ("… zone = PURE … No tap before the end = DUFF" / ピュア … ダフリ); grep every `SCHEME_POPUP_*` and `TIP_*` row for JUST / PERFECT / SHANK / MISS and fix each (report the list). `SHOT_GRADE_JUST/PERFECT/SHANK/MISS` rows are **deleted** once nothing references them (`--check` clean proves it). Path: `LocalizationText.csv` → `import_content.py --catalogs texts` PLAN → `--apply` → publish `texts` → `export_content.py --check`. Zero new hardcoded `.text` literals.

**Pop colours.** `SchemeGradePop` collapses its two colour groups (`_justColor/_goodColor/_missColor`, `_perfectColor/_nearMissColor/_shankColor`) into one: `_pureColor #ADEBAD`, `_nearColor #FFEBA6` (GOOD/HOOK/SLICE/THIN), `_duffColor #FF5A5A`, sourced from `ConeBandPalette` so the cone bands, the Pendulum bar bands and the pops are one palette. `Show(...)` overloads for each grade enum map to those three. `NeedleColors` perfect-zone blue → the PURE green (D8).

**Flick pop.** `FlickGradePop`: a clone of `PendulumGradePop` (same `SchemeGradePop` component, same 360×142 rect, same +289 above the ball) under `SchemeRoot_Flick` — after `shot_view_layout`, inside its `BallSpace`. `ClubHandleDragger` / `ShotController.CommitFlick` publish the Flick grade: expose `LastFlickGrade` next to `LastTimingPowerMul`, and a tiny `FlickGradePopBinder` on the root listens to `OnStateChanged` → `Resolving` and calls `Show`. Bots and programmatic drivers (no samples → `TimingMul = 1`) show nothing (as with the other schemes' bot swings — confirm by reading `BotSwing`; if the other schemes DO pop for bots, match them).

**Confirm pop-up tiles:** words appear only in the LINE strings, not in the captured tiles → no recapture. Verify by opening the pop-up for all four schemes and reading every line.

### 3.7 Telemetry

No schema change. A duff is identifiable as `timing_mul == MissPowerMul` (or `PuttMissPowerMul`) on `shot_taken`; `scheme_evaluation`'s `err_yaw_deg` already separates it from a shaped miss. If `scheme_evaluation` has not shipped when this lands, nothing here depends on it.

### 3.8 Tests (EditMode)

- `PendulumMathTests`, `NeedleMathTests`, `FreeSwingMathTests`: the MISS / SHANK / DUFF cases assert `TimingMul == cfg.MissPowerMul` and `IsMiss`; putt variants assert `PuttMissPowerMul`; big HOOK/SLICE cases assert **unchanged** multipliers and `IsMiss == false`.
- `ShotTimingPowerTests`: new cases — `t < red` → `MissPowerMul` + `isMiss`; `t == red` → `TimingPowerMulRed`; `t == gold` → `TimingPowerMulGold` (unchanged); `t ≥ green` → 1. Existing cases at/above gold must pass **unmodified**; any existing case that sampled `t ∈ (0, gold)` is re-derived against the re-based ramp and the change called out in the report.
- `ShotInputBuilderTests`: `launchPitchScale` default → identical `ShotInput` to today (byte-for-byte on the fp fields); `0.35` → pitch = loft × 0.35 clamped ≥ 2°; putt ignores it.
- New `FlickMathTests`: grade at t = 0.10 / 0.15 / 0.44 / 0.45 / 0.84 / 0.85 / 1.0 → DUFF / THIN / THIN / GOOD / GOOD / PURE / PURE; `GradeKey` for every enum in all four schemes returns only the five unified keys (a reflection-free explicit table).
- `BotSchemeSigmaCalibrator`: run the bisection harness; **`bot_difficulty.csv` diff must be empty** (D4).
- Full EditMode run count in the report.

## 4. Acceptance checklist (Implementer fills in `IMPLEMENTER_REPORT.md`)

- [ ] Pendulum MISS in the Lab: ball travels ≤ 25 % of the 100 % carry for that club (write yards intended vs landed), low flat flight, red MISS pop, gauge flashes red with the resolved %.
- [ ] Needle SHANK: same; Needle big HOOK/SLICE still flies at the GOLD multiplier (write the number).
- [ ] Free Swing DUFF: same; big HOOK/SLICE unchanged.
- [ ] Flick: aim latched at slab progress 0.10 → duff (red flash, ≤ 25 % carry); at 0.15 → 70 % (ramp bottom); at 0.45 → 90 %; at ≥ 0.85 → 100 %. Drawn red band line sits at 0.15 of the cone.
- [ ] Putt miss (any scheme): ball rolls ~30 % of the intended distance on the miss yaw; never stationary.
- [ ] `bot_difficulty.csv`: zero diff after re-running the sigma harness.
- [ ] Tournament replay NOTE (§3.2) answered: a duff replays identically on the receiving side, or the gap is described.
- [ ] Flick shows PURE / GOOD / THIN / DUFF pops at the four slab bands (screenshot each); no pop on a rejected flick; no pop on a bot swing (or matched to the other schemes — say which).
- [ ] Every scheme's pop uses the unified words; JUST / PERFECT / SHANK / MISS appear nowhere in the game (grep `LocalizationText.csv` and the scheme confirm pop-up lines for all four schemes, EN and JA).
- [ ] Strings went through the importer: PLAN verdicts quoted, `texts` published, `export_content.py --check` clean; `SHOT_GRADE_THIN` present, the four retired keys gone.
- [ ] Needle perfect zone drawn in PURE green (or the veto noted).
- [ ] `grep` for new hardcoded `.text` literals quoted (zero).
- [ ] All EditMode tests green; counts quoted; the pre-existing parity tests (`ShotAimParityTests`, `ShotControllerFlickGateTests`) unmodified.
- [ ] Unity Console clean; spec deviations flagged with justification.

## 5. Out of scope (rows added to `Docs/GPS/GPS_BACKLOG.md` this session)

- **Whiff** (air shot, ball untouched) as the far-outside outcome — wants a golfer animation first (D1).
- Duff SFX (a thud / topped-ball click) — rides with the parked grade-SFX rows; Architect sources a CC0 placeholder when taken up.
- Camera: a short "watch it dribble" cut instead of the flight cameras on a duff.
- A `grade` telemetry column (rows already parked per scheme).
- Figma: scheme frames + confirm pop-up frames still say JUST / PERFECT / SHANK — Architect updates them with the geometry redraw already parked from `shot_view_layout`.

## 6. Files this task touches

- `Assets/Scripts/Gameplay/Config/ControlsConfig.cs`, `ControlsConfigLoader.cs`, `Assets/Resources/Gameplay/controls.csv`
- `Assets/Scripts/Gameplay/Input/ShotIntent.cs`, `ShotController.cs` (`TimingPowerMultiplier`, `CommitFlick`, `CommitExternal`, `ResolveAndPublish`, `LastShotWasMiss`)
- `Assets/Scripts/Physics/Stats/ShotInputBuilder.cs` (+ `ShotCommand` only if the replay NOTE demands it)
- `Assets/Scripts/Gameplay/UI/ShotUI/Controls/Pendulum/PendulumMath.cs`, `Needle/NeedleMath.cs`, `FreeSwing/FreeSwingMath.cs` + the three drivers (one line each)
- `Assets/Scripts/Gameplay/UI/ShotUI/ConeBandPalette.cs`, `PowerGaugeWidget.cs`, `Controls/SchemeGradePop.cs`, `Controls/Needle/NeedleColors.cs`, NEW `Controls/FlickMath.cs` + `FlickGradePopBinder.cs`
- `Assets/Scenes/Physics/LabScaffold.unity` (`FlickGradePop` under `SchemeRoot_Flick`/`BallSpace`)
- `Assets/Localization/LocalizationText.csv` (+ importer run, `texts` publish)
- Tests: `PendulumMathTests`, `NeedleMathTests`, `FreeSwingMathTests`, `ShotTimingPowerTests`, NEW `FlickMathTests`, `Assets/Scripts/Physics/Tests/ShotInputBuilderTests.cs`
- `Docs/AI_CONTEXT.md`
