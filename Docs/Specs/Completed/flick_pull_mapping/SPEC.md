# SPEC — `flick_pull_mapping` (Quick)

> Follow-up to `flick_shot_view` (ARCHITECT_REVIEW_PASS, awaiting Cesar's DONE — run this after it closes; both touch `ShotConeView`, `ClubHandleDragger`, `controls.csv`, Flick tiles). Written 2026-09-07 (Architect, Cowork). Project mirror: `claude/FLICK_PULL_MAPPING_SPEC.md`.

## Status

`STATUS.md` starts at `SPEC_READY`.

## Goal

Flick's power is measured from the cone **base**: `ClubHandleDragger.ProcessDrag` does `power = 1 − handleY / ConeHeightPx`, so the club already reads **31.8 %** the instant it is touched (rest at 0.6818 × 792 for the 540 px pull parity chosen in `flick_shot_view`). Cesar: *"way too high."* Flick also has **no 120 %** — the handle path clamps at 1.0 at the base; the Figma driver frame (14153:4602) draws 100 % and 120 % lines, and the other three schemes have 120 %.

Make Flick pull like the other three: power measured **from the rest point**, 0 % at touch, a 40 px dead zone, `Pull100 = 540`, `Pull120 = 648` (the lanes' exact 1.2×), the **base of the cone is 120 %**, never on putts. The cone, bands, slab, arrows and camera are untouched.

## 1. Decisions (Cesar, 2026-09-07)

| # | Decision |
|---|---|
| D1 | Power is rest-relative: `pullPx = restY − handleY` (cone-local, base = 0). `power = ComputeFlickPower(pullPx)` with the same shape as `ShotController.ComputePower`: `< MinUsefulPullPx (40)` → 0; linear to 1.0 at `FlickPull100Px = 540`; linear to 1.2 at `FlickPull120Px = 648`; putts clamp at 1.0 (`ShotController.IsPutt`, same rule as every scheme). |
| D2 | The club rests **648 px above the base** so the base is 120 %: `FlickHandleStartY01 = 648 / 792 = 0.8182`. The 100 % point is 108 px above the base. On putts the rest stays where it is; the pull simply saturates at 540. |
| D3 | 100 % and 120 % **tick lines with labels** on the cone at 108 px and 0 px above the base, the same `Label100/Label120` atoms the Pendulum lane uses (`PendulumLaneView`), positioned from the config, red 120 % / gold 100 % as the lanes. Hidden on putts (no 120 %; the 100 % tick stays). |
| D4 | The idle-cone alpha item (report Q2 of `flick_shot_view`) stays parked — Cesar: "leave for now". |
| D5 | Lateral aim (`finetune`) keeps its geometry: `maxX = halfBase × (1 − handleY / ConeHeightPx)` as today — the cone's width at the finger's height is a drawing fact, not a power fact. |

## 2. Config — `controls.csv` + `ControlsConfig`

```
FlickPull100Px,540,flick_pull_mapping (2026-09-07): finger travel from the club's rest to 100 %, rest-relative like the other three schemes. = PendulumPull100Px / FreeSwingPull100Px.
FlickPull120Px,648,flick_pull_mapping: travel to 120 % = the cone base. Exact 1.2 × FlickPull100Px (tests assert it). Never on putts.
```

Change: `FlickHandleStartY01,0.8182,... flick_pull_mapping: 0.6818 -> 0.8182 = FlickPull120Px / FlickConeHeightPx so the base is 120 % and the club rests 648 px above it (0 % at touch, was 31.8 %).`
`MinUsefulPullPx` (40) is reused as the dead zone — no new key. Add a `Default` assertion (or loader warning) that `FlickHandleStartY01 × FlickConeHeightPx == FlickPull120Px ± 1` so the three cannot drift apart silently.

## 3. Implementation

### 3.1 `ClubHandleDragger.ProcessDrag` (`Assets/Scripts/Gameplay/UI/ShotUI/ClubHandleDragger.cs`)

```csharp
float restY  = _coneView.HandleRestYPx;                  // ShotConeView already exposes it (line ~88)
float pullPx = Mathf.Max(0f, restY - handleY);           // handleY clamped [0, ConeHeightPx] as today
float power  = FlickPullMath.Power(pullPx, cfg, _shotController.IsPutt);
```

`FlickPullMath` (static, `Golfin.Gameplay.Input`, next to `FlickMath` from `miss_grade_duff`): `Power(pullPx, in ControlsConfig cfg, bool isPutt)` — `MinUsefulPullPx` → 0; `(pull − min) / (Pull100 − min)` to 1; `1 + (pull − Pull100) / (Pull120 − Pull100) × 0.2` capped 1.2; `isPutt` → `Mathf.Min(power, 1f)`. Same shape as `ShotController.ComputePower` — do not call that (private, and it reads the raw-touch keys). `SetExternalPower` already clamps to `MaxOverpowerNormalized` (1.2), so 120 % flows through unchanged; `_peakPower` logic untouched.

`ClubHandleDragger` needs a reference to `ShotConeView` (or `HandleRestYPx` passed in) — wire in the scene via the builder, do not `Find`.

### 3.2 `ShotConeView` handle placement (line ~466)

Today `handleY = _handleStartYPx × (1 − power)` — the inverse of the old mapping. Replace with the inverse of the new one: `handleY = _handleStartYPx − FlickPullMath.PullPxForPower(power, cfg, isPutt)` clamped `[0, _handleStartYPx]`, so the drawn club and the finger agree at 0 %, 100 % and 120 %. `PullPxForPower` is the exact inverse (tests assert round-trip); bots (`FlickBotExecutor` → `SetExternalPower` normalised) render correctly through the same line.

### 3.3 Ticks

Two tick lines + labels under `SchemeRoot_Flick/BallSpace/ConeRoot/ConeMesh` (so they scale with the cone), positioned by `ShotConeView` on `Awake` from cfg: 100 % at `y = Pull120 − Pull100 = 108`, 120 % at `y = 0` (cone-local, base = 0). Reuse the Pendulum lane's tick + label prefab/atoms and colours (`PendulumLaneView` — gold 100 %, red 120 %); label x offset as the lane's (+76 right of the axis). Putt: 120 % tick hidden, 100 % tick stays; `ShotConeView` already branches on `IsPutt` for the track. Follow `ConeAlphaController`'s alpha so they fade with the cone.

### 3.4 Tests

- New `FlickPullMathTests`: 0 / 39 / 40 / 290 / 540 / 594 / 648 / 700 px → 0 / 0 / 0 / 0.5 / 1.0 / 1.1 / 1.2 / 1.2; putt: 648 → 1.0; `PullPxForPower` round-trips at 0.25 / 0.5 / 1.0 / 1.2; `Pull120 == 1.2 × Pull100`; `FlickHandleStartY01 × FlickConeHeightPx == Pull120 ± 1`.
- `ShotAimParityTests`, `ShotTimingPowerTests`, `ShotControllerFlickGateTests`: unchanged and green (they drive `SetExternalPower` normalised).
- Bots: existing Flick bot tests green; `bot_difficulty.csv` untouched.

### 3.5 Tiles

Recapture the **three Flick** confirm tiles (the club rests higher; two tick lines appear). Other nine byte-identical.

## 4. Acceptance

- [ ] Touch the club at rest: gauge reads **0 %**; drag 40 px → still 0 %; 540 px → **100 %**; 648 px (base) → **120 %**, gauge in the red overpower arc; release past the base clamps at 120 %.
- [ ] Putt: base reads **100 %**, 120 % tick hidden.
- [ ] Drawn club and finger coincide at 0 / 100 / 120 % (screenshot each with the tick lines visible).
- [ ] Rest at canvas y = base + 648 = **−447.84** (ball −303.84, so the club sits 144 px under the ball); 100 % tick at −987.84; 120 % tick at −1095.84.
- [ ] Lateral aim at 100 % still spans the cone's full width at that height (D5) — `finetune` ±1 reachable.
- [ ] `miss_grade_duff` live band check (PURE / GOOD / THIN / DUFF) re-run — unaffected by design; prove.
- [ ] EditMode counts quoted; parity tests unmodified; `bot_difficulty.csv` zero diff.
- [ ] Flick tiles recaptured (3), nine byte-identical.
- [ ] Device (Cesar): 0 % at touch feels right; 120 % reachable without the thumb leaving the glass.

## 5. Out of scope
Idle cone alpha (parked, D4); cone half-angle retune; any change to the three lane schemes.

## 6. Files
`ClubHandleDragger.cs`, `ShotConeView.cs`, NEW `Assets/Scripts/Gameplay/Input/FlickPullMath.cs`, `ControlsConfig.cs`, `ControlsConfigLoader.cs`, `controls.csv`, the Flick builder / `ActionButtonsBuilder` wiring, `LabScaffold.unity` (ticks + wiring), NEW `FlickPullMathTests.cs`, three Flick tiles, `Docs/AI_CONTEXT.md`.
