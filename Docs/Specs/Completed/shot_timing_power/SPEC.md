# SPEC — `shot_timing_power`

> **Authoritative spec for this task.** Implementer reads this and ONLY this for the work definition. STATUS.md tracks pipeline state. Reports/reviews go in their own files.

## Status

See `STATUS.md`. Filed 2026-08-28 by the Architect (Cowork). **Run AFTER `shot_aim_parity`** — both edit `ShotController.CommitFlick` and `PushTouchSample`.

## Goal

Make the coloured timing slab matter. Cesar (2026-08-28): "I am not sure the colored arrows going through the ball right now are having any effect either." They are not: the slab's position and colour at the moment of the flick are never read. The only timing effect today is the **pass counter** — every pass beyond the clean passes adds `DegradationYawDegPerPass` (2°) of aim error, and `MaxTotalPasses` (10) auto-cancels. Flicking on green or on red gives the identical shot.

`SHOT_CONTROLS_DESIGN.md §3.4` defines the missing piece: *"The player flicks when an arrow reaches the apex (= perfect timing). Off-time flicks reduce power."* `ShotDebugFlags.ForcePerfectTiming` already exists for it and is wired in `PhysicsLabUI` (toggle 6) but only bypasses the flick-velocity check. This task wires the power effect.

## Where the code stands (read before editing)

- `ShotController.TickArrow()` advances `_arrowProgress` 0→1 per pass at `arrowHz` (CC-dependent, 2.0 Hz at CC 0 → 0.5 Hz at CC 50, floor `MinArrowSpeedHz`); on wrap it bumps `_passIndex` and degradation.
- `PublishState()` ships `_arrowProgress` as `ShotInputState.ArrowProgress01`; `ShotConeView.UpdateSlab` places `TimingSlabGraphic` at `CurrentY01 = prog` and colours it via `SlabColorFromProgress` using `ConeBandPalette.BandRedY01 = 0.00 / BandGoldY01 = 0.45 / BandGreenY01 = 0.85` (red → gold → green, green = top 15 % of the cone).
- `CommitFlick()` reads `PowerNormalized` (handle pull, 0..1.2) and `_degradationYawRad`. It never reads `_arrowProgress`.
- Aim is latched at the upswing reversal in `PushTouchSample` (`_aimLocked`) — that moment is "the bottom of the swing", the instant the player reacted to the slab. `shot_aim_parity` D3 adds unlatch-on-new-low.

## Decisions (Architect, for Cesar to overrule)

- **D1 — timing is sampled at the aim latch, not at release.** The player reacts to the slab by starting the flick; the finger leaves the screen 50–150 ms later, by which time the slab has moved (at 2 Hz, 0.1 s = 20 % of the cone). Sampling `_arrowProgress` when `_aimLocked` flips true makes "flick when it's green" mean what it says. Unlatch (D3 of `shot_aim_parity`) clears the sample; re-latch re-samples.
- **D2 — effect = power multiplier, per the design doc.** No extra aim error from timing (the pass counter already does that). Multiplier is a piecewise-linear map of `timing01` through the same band edges the slab is drawn with:
  `timing01 ∈ [0, gold)`  → lerp(`TimingPowerMulRed`, `TimingPowerMulGold`, timing01 / gold)
  `timing01 ∈ [gold, green)` → lerp(`TimingPowerMulGold`, 1.0, (timing01 − gold) / (green − gold))
  `timing01 ≥ green` → 1.0
  Proposed values: `TimingPowerMulRed = 0.70`, `TimingPowerMulGold = 0.90`. Tunable in `controls.csv`; Cesar tunes by feel.
- **D3 — band edges become config, shared by gameplay and drawing.** `TimingBandGoldY01 = 0.45`, `TimingBandGreenY01 = 0.85` move into `ControlsConfig`/`controls.csv`; `ConeBandPalette.BandGoldY01/BandGreenY01` become getters over `ControlsConfig.Default` so the coloured bands and the multiplier can never drift apart. (`Golfin.Gameplay.UI` already references `Golfin.Gameplay.Config` — `ShotConeView` uses `ControlsConfig.Default`.)
- **D4 — no penalty without a touch swing.** Bots, capture drivers, `FireDebugShot`, EditMode tests and the legacy `IInputSource` path push no samples and never latch → no timing sample → multiplier 1.0. Their shots stay byte-identical. `DebugFlags.ForcePerfectTiming` also forces 1.0 (it finally does what its name says).
- **D5 — applies to putts too.** The putter slab (`_putterTimingSlabRT`) uses the same bands; §3.4 exempts putts only from *degradation* (§4), not from off-time power. If Cesar wants putts exempt it is one `IsPutt` guard in `TimingPowerMultiplier()` — flag it, don't decide it.
- **D6 — multiplier applies after overpower.** `flickMag = PowerNormalized (≤1.2 or clamped) × timingMul`. A 120 % pull on red is 84 %; on green it stays 120 %.

## Architecture context

- **Asmdef boundaries:** `Golfin.Gameplay.Input` ← `Golfin.Gameplay.Config` (already); `Golfin.Gameplay.UI` ← both (already). No new references.
- **Existing code:** `ShotController.cs` (`TickArrow`, `PushTouchSample`, `CommitFlick`, `ResetSwingSamples`, `LogResolution`), `ShotDebugFlags.cs` (`ForcePerfectTiming`), `ConeBandPalette.cs`, `ShotConeView.SlabColorFromProgress`, `ControlsConfig.cs`, `ControlsConfigLoader.cs`, `controls.csv`, `ShotTelemetryRelay.cs` (see §5).

## Implementation

### 1. Config

`ControlsConfig`: `TimingBandGoldY01 = 0.45f`, `TimingBandGreenY01 = 0.85f`, `TimingPowerMulRed = 0.70f`, `TimingPowerMulGold = 0.90f` (+ loader cases + four `controls.csv` rows with the D2 comment). `ConeBandPalette`: `BandGoldY01 => ControlsConfig.Default.TimingBandGoldY01`, `BandGreenY01 => ControlsConfig.Default.TimingBandGreenY01`; `BandRedY01` stays `0f`. Consumers (verified): `ShotConeView.SlabColorFromProgress` (reads live — fine) and `ConeMeshGraphic._bandRedY01/_bandGoldY01/_bandGreenY01`, which use the palette only as **serialized field defaults** — a prefab could already carry a different value. Make `ConeMeshGraphic` re-sync those three fields from `ConeBandPalette` in `OnEnable` (before the first mesh build) so the drawn band lines are the config's, and report the prefab's serialized values if they differed from 0.45/0.85.

### 2. Sample at the latch — `ShotController`

```csharp
private float _timingAtLatch = float.NaN;   // _arrowProgress when the aim latched; NaN = no touch swing

// PushTouchSample, where `_aimLocked = true;` is set:
_aimLocked     = true;
_timingAtLatch = _arrowProgress;

// PushTouchSample, D3 unlatch branch (shot_aim_parity): add
_timingAtLatch = float.NaN;

// ResetSwingSamples(): add
_timingAtLatch = float.NaN;
```

`TickArrow` is unchanged — `_arrowProgress` at the latch frame is exactly what the slab showed that frame (`UpdateSlab` uses the same value one publish earlier; a one-frame skew is 1–3 % of the cone and acceptable).

### 3. Apply — `CommitFlick`

```csharp
float timingMul = TimingPowerMultiplier();
float flickMag  = PowerNormalized;
if (IsPutt || DebugFlags.DisableOverpower) flickMag = Mathf.Min(flickMag, 1f);
flickMag *= timingMul;                                        // D6: after overpower

/// 1.0 unless a touch swing latched with the slab below the green band.
private float TimingPowerMultiplier()
{
    if (DebugFlags.ForcePerfectTiming || float.IsNaN(_timingAtLatch)) return 1f;   // D4
    float t = Mathf.Clamp01(_timingAtLatch);
    float gold = _config.TimingBandGoldY01, green = _config.TimingBandGreenY01;
    if (t >= green) return 1f;
    if (t >= gold)  return Mathf.Lerp(_config.TimingPowerMulGold, 1f, (t - gold) / Mathf.Max(1e-4f, green - gold));
    return Mathf.Lerp(_config.TimingPowerMulRed, _config.TimingPowerMulGold, t / Mathf.Max(1e-4f, gold));
}
```

Expose `public float LastTimingAtLatch => _timingAtLatch;` and `public float LastTimingPowerMul { get; private set; }` (set in `CommitFlick`) for the log, telemetry and tests. Extend the `LogResolution` line: `timing01={_timingAtLatch:F2} timingMul={timingMul:F2}`.

`PublishShotSfx()` picks Hit SFX from `PowerNormalized` — leave it; the swing *felt* full-power, the ball just goes shorter.

### 4. Feedback (minimal, no new UI)

On `CommitFlick` the HUD power text (`ShotConeView.UpdateHUD`, shown through `Flicking`) already displays `pct%` from `state.PowerNormalized`. Do **not** change `PowerNormalized`. Instead `ShotInputState` gains `TimingPowerMul` (1.0 by default) and `UpdateHUD` shows `{pct}% × {mul:F2}` only when `mul < 1` during `Flicking`. One extra struct field + one string branch; nothing else. If this reads as clutter on device, Cesar drops it — flag in the report.

### 5. Telemetry

`ShotTelemetryRelay` relays only `FlickRejected` / `ShotCancelled` (verified) — there is no shot-committed payload. **Do nothing here**; do not invent a new event. If `TelemetryHooks.cs` turns out to carry a shot-resolved payload after all, add `timing01` / `timing_mul` there from `LastTimingAtLatch` / `LastTimingPowerMul` and say so in the report.

### 6. Tests — `Assets/Scripts/Gameplay/Tests/ShotTimingPowerTests.cs`

Harness as `FadeDrawWiringTests` (inject config with `BaseArrowSpeedHzAtCC0 = 1`, `ArrowSpeedHzPerCC = 0`, `MinArrowSpeedHz = 0.1`, bundle CC = 0 → `arrowHz = 1`, so `Tick(0.3f)` after entering Timing puts `_arrowProgress` at 0.3 exactly). Measure `|velocity|` of the resolved `ShotInput`.

1. `NoTouchSamples_MultiplierIsOne` — Begin → SetExternalPower(1, 0) → Tick(0.3) → End: `LastTimingPowerMul == 1`, speed equals a baseline shot. (Bots.)
2. `LatchOnGreen_FullPower` — Begin → SetExternalPower(1,0) → Tick(0.9) → push samples down then up 5 % screen (latch) → End: mul == 1.
3. `LatchOnRedBase_RedMultiplier` — Tick(0.0) → latch → mul == `TimingPowerMulRed` and speed == baseline × 0.70 (±1 %).
4. `LatchMidGold_Interpolates` — Tick(0.65) → mul == lerp(0.90, 1, 0.5) = 0.95.
5. `ForcePerfectTiming_OverridesRed` — as 3 with `DebugFlags.ForcePerfectTiming = true` → mul == 1.
6. `Unlatch_ClearsSample` — latch at 0.1, push a lower sample (unlatch), Tick(0.8), re-latch → mul == 1 (sampled at 0.9).
7. `FireDebugShot_Unaffected` — `FireDebugShot(1, Green)` → mul == 1.
8. `ConeBandPalette_MatchesConfig` — `ConeBandPalette.BandGoldY01 == ControlsConfig.Default.TimingBandGoldY01` (and green).

### 7. Changelog

`Docs/Physics/PHYSICS_TUNING_CHANGELOG.md` — **F15 — Timing slab drives a power multiplier (0.70 / 0.90 / 1.0)**, D1–D6, the "sampled at the latch" rationale with the 20 %-per-0.1 s number, files, tests.

## Acceptance checklist (Implementer fills in `IMPLEMENTER_REPORT.md`)

- [ ] `ShotTimingPowerTests` 1–8 pass; whole `Golfin.Gameplay.Tests` assembly green (no filter).
- [ ] Editor play, Hole 01, driver, `LogResolution` on: three shots pulled to 100 % flicked on green / gold / red (watch the slab colour at the moment you start the upflick) → log shows `timing01` in the matching band, `timingMul` ≈ 1.0 / ~0.9 / ~0.7, and carry ordered green > gold > red.
- [ ] Editor play: same three with the debug toggle 6 (`ForcePerfectTiming`) ON → all three carries equal.
- [ ] Editor play, putter: red-band putt is visibly shorter than green-band putt at the same pull.
- [ ] HUD shows `× 0.xx` during `Flicking` on an off-time shot and nothing extra on a green one.
- [ ] Bot parity: one `Scenarios` smoke shot's carry unchanged before/after (sampleless drivers multiply by 1.0).
- [ ] `ConeBandPalette` bands still draw at 0.45 / 0.85 (screenshot of the cone in Timing).
- [ ] Unity Console has no errors related to this task.
- [ ] Spec deviations (if any) flagged with justification.

## Out of scope

- Any change to arrow speed, clean passes, `DegradationYawDegPerPass`, `MaxTotalPasses` (F13 stays the record).
- Timing affecting aim error, spin, or loft.
- New UI (a "PERFECT!" flash etc.) — separate polish task if Cesar wants it.
- Flick-vector aiming (scheme C) — `Docs/Specs/Queued/flick_vector_aim_DESIGN_NOTE.md`.

## Files this task touches

- `Assets/Scripts/Gameplay/Input/ShotController.cs` — `_timingAtLatch`, `TimingPowerMultiplier`, `CommitFlick`, `PushTouchSample`, `ResetSwingSamples`, log, two public getters.
- `Assets/Scripts/Gameplay/Input/ShotInputState.cs` — `TimingPowerMul` field.
- `Assets/Scripts/Gameplay/Config/ControlsConfig.cs`, `ControlsConfigLoader.cs`, `Assets/Resources/Gameplay/controls.csv` — four keys.
- `Assets/Scripts/Gameplay/UI/ShotUI/ConeBandPalette.cs` — getters over config; `ShotConeView.cs` — `UpdateHUD` branch.
- `Assets/Scripts/Gameplay/UI/ShotUI/ShotTelemetryRelay.cs` / `Assets/Scripts/TelemetryRuntime/TelemetryHooks.cs` — only if a shot payload already exists.
- `Assets/Scripts/Gameplay/Tests/ShotTimingPowerTests.cs` (new).
- `Docs/Physics/PHYSICS_TUNING_CHANGELOG.md` (F15), `Docs/Game Design/SHOT_CONTROLS_DESIGN.md` (§3.4: "implemented 2026-08-28, `shot_timing_power`, sampled at the upswing reversal"), `Docs/AI_CONTEXT.md`.

## Smoke evidence

EditMode summary + the three green/gold/red `LogResolution` lines + one Timing-state screenshot showing the bands.
