# Phase 6 Stat Coupling — Completion Report
**Date:** 2026-04-22
**Status:** ✅ COMPLETE — 49/49 tests pass

---

## What Was Built

The full stat→physics modifier layer using the **Specialized Roles model (Option D)** from `SPEC_PHASE6_STAT_COUPLING.md`. Each stat owns exactly one physics lane; no stat conflicts with another.

### New Assembly: `Golfin.Physics.Stats` (`noEngineReferences: true`)

| File | Purpose |
|---|---|
| `ClubStats.cs` | Power/Accuracy/LieResistance/Durability (int 0..120) + LoftDegrees/BaseVelocityMps/BaseBackspinRpm (fp) |
| `PutterStats.cs` | Control/Accuracy/Weight/Durability (int) + LoftDegrees/BaseVelocityMps (fp) |
| `BallStats.cs` | Power/Rebound/WindCut/Roll/Spin (int −10..+10); `Neutral` preset |
| `CharacterStats.cs` | Strength/ClubControl/Recovery/Stamina (int 0..120); `Neutral` preset |
| `StatBundle.cs` | Club? or Putter? + Ball + Character + CurrentStamina/MaxStamina |
| `StatCoefficients.cs` | 14 per-stat fp coefficients; `Default` matches `stats.csv` |
| `StatCaps.cs` | 11 fp caps; `Default` matches `stat_caps.csv` |
| `ResolvedShotModifiers.cs` | Full resolver output: VelocityMultiplier, AimConeReductionFraction, SpinMagnitudeMultiplier, BallPhysics, LieResistanceFraction, OverpowerForgivenessFraction, PutterOffCenterForgiveness, PutterGravityWellRadiusM, PutterAimCycles |
| `StatModifierResolver.cs` | 8-step static resolver (see below) |
| `ShotInputBuilder.cs` | `Build()` → `(ShotInput, BallPhysicsModifiers)` ValueTuple |

### Resolver Steps (StatModifierResolver)

1. **Stamina scaling** — `effStrength = Strength × max(0.20, current/max)`, same for ClubControl. Floor prevents char stats going to zero.
2. **Velocity multiplier** — `(1 + ClubPower × 0.005) × (1 + BallPower × 0.01)`, capped at 2.0.
3. **Aim cone reduction** — `1 − (1 − clubAcc) × (1 − charControl)`, capped at 0.95. (Consumed by aim reticle UI, not sim.)
4. **Spin multiplier** — `1 + BallSpin × 0.01`. (Applied by ShotInputBuilder.)
5. **Lie resistance** — `ClubLieResistance × 0.0042`, capped at 0.75. (Consumed by gameplay layer.)
6. **Overpower forgiveness** — `effStrength × 0.00625`, capped at 0.75. (Consumed by ShotInputBuilder overpower clamp.)
7. **Putter-only** — offCenter forgiveness, gravity well radius, aim cycles. (Consumed by UI/assist layer.)
8. **BallPhysicsModifiers** — ReboundMultiplier, RollResistanceMultiplier, WindCutFraction. (Consumed by BallSimulation.)

### BallPhysicsModifiers (Core, `Golfin.Physics`)

Placed in Core (not Stats) so `BallSimulation` can consume it without depending on the Stats assembly.

- `ReboundMultiplier` — multiplies `SurfaceCoefficients.Restitution` at each bounce
- `RollResistanceMultiplier` — multiplies `SurfaceCoefficients.RollingResistance` in RunRollPhase + RunPuttPhase
- `WindCutFraction` — scales wind vector by `(1 − fraction)` before aero drag at each of 4 RK4 sub-steps
- `Neutral` static preset — all multipliers = 1.0, WindCutFraction = 0 → bit-exact with Phase 1–5

### BallSimulation Changes

- Phase 3 4-arg now forwards to private `SimulateAirborne(..., Neutral)` — extracted the RK4 loop
- Phase 5 7-arg forwards to Phase 6 8-arg with `Neutral`
- Phase 6 8-arg: authoritative implementation; all four injection points active
- Subsequent bounce arcs call `SimulateAirborne(nextInput, ..., ballMods)` — WindCut persists across all arcs

### CSV & Loader

- `Assets/Resources/Physics/stats.csv` — 14 coefficient rows
- `Assets/Resources/Physics/stat_caps.csv` — 11 cap rows
- `PhysicsConfigLoader.LoadStatCoefficients()` + `LoadStatCaps()` — same key→field switch pattern as other loaders

---

## Test Results

**49/49 EditMode tests pass (2.85s)**

| Suite | Tests | Result |
|---|---|---|
| ProjectileMathTests (Phase 1) | 4 | ✅ |
| AerodynamicsTests (Phase 2/2.1) | 15 | ✅ |
| WindTests (Phase 3) | 6 | ✅ |
| SurfaceTests (Phase 4) | 8 | ✅ |
| PuttTests (Phase 5) | 6 | ✅ |
| ViewerTests (Phase 6 Viewer) | 10 | ✅ |
| **StatResolverTests (Phase 6 Stats)** | **10** | **✅** |

### New Tests (StatResolverTests)

1. `Stats_Phase5Overloads_BitExact` — 7-arg vs 8-arg Neutral: bit-exact ✅
2. `Stats_NeutralBundle_VelocityMultiplierIsOne` — all-zero stats → vel×1.0 ✅
3. `Stats_ClubPower60_VelocityMultiplierOnePointThree` — 60pts → ×1.30 ✅
4. `Stats_BallPower_MultiplicativeWithClub` — club ×1.30 × ball ×1.10 = 1.43 ✅
5. `Stats_VelocityMultiplier_HardCapAtTwo` — max club+ball ≤ 2.0 ✅
6. `Stats_ZeroStamina_FloorPreservesCharStats` — Strength×0.20 floor → overpower 0.15 ✅
7. `Stats_BallRebound_MultiplierCorrect` — Rebound +10 → 1.10 ✅
8. `Stats_BallWindCut_FractionCorrect` — WindCut +10 → 0.10 ✅
9. `Stats_BallRoll_ReducesRollingResistance` — Roll +10 → 0.90 < 1.0 ✅
10. `Stats_ShotInputBuilder_IronCarryInRange` — full iron shot: 100–220 m carry ✅

### One Fix During Testing

Initial tolerances used raw Q16.16 unit comparison (`±1` or `±2` raw units). Multi-step fixed-point multiplications accumulate 4–19 raw units of rounding (≈ 0.00006–0.0003 decimal — gameplay-irrelevant). Switched 6 tests to `ToFloat() ± 0.001f`.

---

## Deviations from Spec

- **Lab integration deferred** — as specified. PhysicsLab continues to use raw `ShotInput` directly.
- **Aim cone consumption deferred** — `AimConeReductionFraction` is resolved and available in `ResolvedShotModifiers` but the aim reticle UI does not yet consume it. Phase 7 (gameplay layer) will wire this.
- **Lie/overpower/putter outputs** — resolved and available but no gameplay-layer consumer yet. Also Phase 7.

---

## What's Next (Phase 7 scope)

- Aim reticle UI consumes `AimConeReductionFraction` to shrink wobble zone
- Overpower zone uses `OverpowerForgivenessFraction` to soften penalty band
- Lie modifier applies `LieResistanceFraction` to reduce rough/bunker launch penalty
- Putter assist: `PutterGravityWellRadiusM` for gravity-well aim assist; `PutterAimCycles` for aim arrow UI
- `PhysicsConfigLoader` usage in gameplay scene to load coefficients/caps from CSV at runtime
