# Spec — Phase 6 Stat Coupling Layer (`StatModifierResolver`)

> **Status:** Parked, not yet active. Manual Scene Snapshot tool is the active task.
> **Spec date:** 2026-04-22
> **Estimated effort:** ~1 day Code work + ~half day Cesar coefficient calibration
> **Reference docs:** `Docs/PHYSICS_TUNING_TARGETS.md` Section 8 (Specialized Roles), Section 4 (character stats), Section 2 (club stats), Section 3 (putter stats). This spec supersedes Section 5 (ball stats) — ball stat list locked below.
> **Promote to active by:** copying the body below into `Docs/TellCode.md` as the next ACTIVE TASK once the snapshot tool is done.

---

## Context

Phases 1–5 built a deterministic ball simulation that takes a raw `ShotInput` (origin, velocity, spin) and returns a `Trajectory`. Nothing currently maps gameplay state — equipped club, equipped ball, character stats, current stamina — to physics inputs. Phase 6 builds that layer.

Locked design: **Specialized Roles (Option D)**, per `PHYSICS_TUNING_TARGETS.md` Section 8. Each stat owns a distinct physics input. Multiplicative stacking only when stats genuinely share a lane (Club Power × Ball Power, Club Accuracy × Character Club Control). No "everything multiplies everything" dogpile.

The deliverable is **pure API**: a resolver and a builder that gameplay code (when it exists) will call. **No gameplay integration this phase.** No flick controls, no power gauge, no aim arrow. The lab keeps using raw `ShotInput` as today — Phase 6 ships the API gameplay needs without coupling to it.

## Locked design decisions

**Ball stats — final list (overrides `PHYSICS_TUNING_TARGETS.md` Section 5):**

| Stat | Range | Physics lane | Effect |
|---|---|---|---|
| **Power** | −10..+10 | Initial velocity | Multiplies into `velocityMultiplier` (shares lane with Club Power) |
| **Rebound** | −10..+10 | Bounce restitution | Multiplies surface `Restitution` at bounce time (single-source from ball) |
| **Wind Cut** | −10..+10 | Aero drag from wind delta | Reduces effective drag from wind delta (single-source from ball; more = better) |
| **Roll** | −10..+10 | Rolling resistance | Multiplies surface `RollingResistance` during roll/putt (single-source; more = farther roll) |
| **Spin** | −10..+10 | Spin magnitude | Multiplies `ShotInput.spin` magnitude (single-source from ball) |

Per-point coefficient: each ball stat point = ±1% effect on its lane (so range is ±10%). Tunable in `stats.csv`.

**Wind Cut directionality:** higher = better (cuts through wind, less drift). Implemented as a fraction subtracted from the wind delta vector magnitude before drag is computed.

**Stamina:** linear scaling with a 20% floor. `effective_char_stat = base × max(0.20, current_stamina / max_stamina)`. Even at 0 stamina, character stats retain 20% of their value — player can still hit but every character-modulated effect is weak. Floor lives in `stat_caps.csv`, tunable.

**Ball stats have no rarity** (per memory) and balls are consumable per hole. The Ball type just carries the 5 stat values directly.

## Scope boundaries — read before starting

**In scope:**
- 4 stat data structs (`ClubStats`, `PutterStats`, `BallStats`, `CharacterStats`) and 1 wrapper (`StatBundle`)
- `StatCoefficients` struct + `stats.csv` loader extension
- `StatCaps` struct + `stat_caps.csv` loader extension
- `ResolvedShotModifiers` struct (the resolver's output)
- `StatModifierResolver` (Core, pure C#, no Unity)
- `BallPhysicsModifiers` struct — runtime scalars passed alongside `SurfaceConfig` to apply Rebound/Roll/Wind Cut to bounce/roll/aero phases
- `BallSimulation` overload that accepts `BallPhysicsModifiers` alongside existing args, applies the multipliers at the right points
- `ShotInputBuilder` (Stats assembly)
- `Assets/Resources/Physics/stats.csv` and `stat_caps.csv`
- New asmdef `Golfin.Physics.Stats.asmdef` (Core), references Core + Math
- `StatResolverTests.cs` — ~10 tests in `Golfin.Physics.Tests`

**Out of scope:**
- Flick controls, power gauge, aim arrow, any gameplay UI. Phase 6 ships pure API.
- Lab integration — lab keeps using raw `ShotInput`. A future fully-featured lab will use the resolver.
- Stamina degradation curve (per-hole drop, per-shot consumption). Section 10 open item; deferred. Phase 6 just *consumes* a current stamina value, doesn't model how it changes over a round.
- Loft randomization at club spawn. Treat `LoftDegrees` as a fixed field on `ClubStats` set at instantiation; randomization is club-creation logic, not physics.
- Putter "gravity well" — assist layer per architecture rule, never in sim.
- Per-club power coefficient curves (the linear-mapping revisit flagged in Section 1). Defer until playtest.
- Hookup in actual gameplay code — gameplay doesn't exist yet.
- Per-club power coefficient calibration sweep beyond the initial driver target. Cesar tunes against carry distances during the half-day calibration pass.
- Any change to existing Phase 1–5 test signatures or `ShotInput` struct.

---

## Part A — Stat data structs (Core)

`Assets/Scripts/Physics/Stats/` is a new folder with a new asmdef.

### `Golfin.Physics.Stats.asmdef`

```json
{
    "name": "Golfin.Physics.Stats",
    "rootNamespace": "Golfin.Physics.Stats",
    "references": [
        "Golfin.Physics.Core",
        "Golfin.Physics.Math"
    ],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "autoReferenced": true,
    "noEngineReferences": true
}
```

`autoReferenced: true` so importers/gameplay can see it without explicit refs. `noEngineReferences: true` because this is pure resolver math — no Unity.

### `ClubStats.cs`

```csharp
using Golfin.Physics.Math;

namespace Golfin.Physics.Stats
{
    public readonly struct ClubStats
    {
        public readonly int Power;            // 0..120 (effective points across all rarities)
        public readonly int Accuracy;         // 0..120
        public readonly int LieResistance;    // 0..120
        public readonly int Durability;       // 0..120 (not used by resolver; informational)
        public readonly fp LoftDegrees;       // fixed at instantiation
        public readonly fp BaseVelocityMps;   // from clubs.csv per club type
        public readonly fp BaseBackspinRpm;   // from clubs.csv per club type

        public ClubStats(int power, int accuracy, int lieResistance, int durability,
                         fp loftDegrees, fp baseVelocityMps, fp baseBackspinRpm)
        {
            Power = power; Accuracy = accuracy; LieResistance = lieResistance;
            Durability = durability; LoftDegrees = loftDegrees;
            BaseVelocityMps = baseVelocityMps; BaseBackspinRpm = baseBackspinRpm;
        }
    }
}
```

### `PutterStats.cs`

```csharp
using Golfin.Physics.Math;

namespace Golfin.Physics.Stats
{
    public readonly struct PutterStats
    {
        public readonly int Control;          // 0..120 — off-center forgiveness
        public readonly int Accuracy;         // 0..120 — gravity well (assist; resolver outputs it but doesn't apply)
        public readonly int Weight;           // 0..120 — aim cycle count
        public readonly int Durability;       // 0..120
        public readonly fp LoftDegrees;
        public readonly fp BaseVelocityMps;   // putter "max" velocity for full power gauge

        public PutterStats(int control, int accuracy, int weight, int durability,
                           fp loftDegrees, fp baseVelocityMps)
        { Control = control; Accuracy = accuracy; Weight = weight; Durability = durability;
          LoftDegrees = loftDegrees; BaseVelocityMps = baseVelocityMps; }
    }
}
```

### `BallStats.cs`

```csharp
namespace Golfin.Physics.Stats
{
    public readonly struct BallStats
    {
        public readonly int Power;        // -10..+10
        public readonly int Rebound;      // -10..+10
        public readonly int WindCut;      // -10..+10 (more = less wind effect)
        public readonly int Roll;         // -10..+10 (more = less rolling resistance, ball rolls farther)
        public readonly int Spin;         // -10..+10 (more = higher applied spin magnitude)

        public BallStats(int power, int rebound, int windCut, int roll, int spin)
        { Power = power; Rebound = rebound; WindCut = windCut; Roll = roll; Spin = spin; }

        public static BallStats Neutral => new BallStats(0, 0, 0, 0, 0);
    }
}
```

### `CharacterStats.cs`

```csharp
namespace Golfin.Physics.Stats
{
    public readonly struct CharacterStats
    {
        public readonly int Strength;       // 0..120 — overpower forgiveness
        public readonly int ClubControl;    // 0..120 — slows aim arrow
        public readonly int Recovery;       // 0..120 — stamina/hour (informational; not used per-shot)
        public readonly int Stamina;        // 0..120 — stamina cap (informational; current passed separately)

        public CharacterStats(int strength, int clubControl, int recovery, int stamina)
        { Strength = strength; ClubControl = clubControl; Recovery = recovery; Stamina = stamina; }
    }
}
```

### `StatBundle.cs`

```csharp
using Golfin.Physics.Math;

namespace Golfin.Physics.Stats
{
    /// <summary>
    /// Everything the resolver needs for one shot. Either Club or Putter is set, not both.
    /// CurrentStamina is the live value (0..MaxStamina), not the cap.
    /// MaxStamina is character base × stat_caps stamina cap multiplier.
    /// </summary>
    public readonly struct StatBundle
    {
        public readonly ClubStats? Club;
        public readonly PutterStats? Putter;
        public readonly BallStats Ball;
        public readonly CharacterStats Character;
        public readonly fp CurrentStamina;
        public readonly fp MaxStamina;

        public StatBundle(ClubStats club, BallStats ball, CharacterStats character,
                          fp currentStamina, fp maxStamina)
        { Club = club; Putter = null; Ball = ball; Character = character;
          CurrentStamina = currentStamina; MaxStamina = maxStamina; }

        public StatBundle(PutterStats putter, BallStats ball, CharacterStats character,
                          fp currentStamina, fp maxStamina)
        { Club = null; Putter = putter; Ball = ball; Character = character;
          CurrentStamina = currentStamina; MaxStamina = maxStamina; }

        public bool IsPutt => Putter.HasValue;
    }
}
```

---

## Part B — `StatCoefficients` and `StatCaps`

### `StatCoefficients.cs`

Values are loaded from `stats.csv`. Defaults below match `PHYSICS_TUNING_TARGETS.md` Section 8 placeholder math; Cesar will tune against carry distances during calibration.

```csharp
using Golfin.Physics.Math;

namespace Golfin.Physics.Stats
{
    public struct StatCoefficients
    {
        public fp ClubPowerPerPoint;            // pCoeff:  velocity multiplier per Club Power point. Default 0.005 (120 pts = +60%).
        public fp ClubAccuracyPerPoint;         // aCoeff:  aim cone reduction per Club Accuracy point. Default 0.0042.
        public fp ClubLieResistancePerPoint;    // lrCoeff: lie penalty reduction per Club Lie Resistance point. Default 0.0042.

        public fp BallPowerPerPoint;            // bpCoeff: velocity multiplier per Ball Power point. Default 0.01 (10 pts = +10%).
        public fp BallReboundPerPoint;          // brCoeff: restitution multiplier per Ball Rebound point. Default 0.01.
        public fp BallWindCutPerPoint;          // bwCoeff: wind delta reduction per Ball Wind Cut point. Default 0.01.
        public fp BallRollPerPoint;             // bRollCoeff: rolling resistance multiplier per Ball Roll point. Default 0.01.
        public fp BallSpinPerPoint;             // bsCoeff: spin magnitude multiplier per Ball Spin point. Default 0.01.

        public fp CharStrengthPerPoint;         // strCoeff: overpower forgiveness per Character Strength point. Default 0.00625.
        public fp CharClubControlPerPoint;      // ccCoeff: aim cone reduction per Character Club Control point. Default 0.0035.

        public fp PutterControlPerPoint;        // putCtlCoeff: off-center forgiveness per Putter Control point. Default 0.0042.
        public fp PutterAccuracyPerPoint;       // putAccCoeff: gravity well radius per Putter Accuracy point. Default 0.0075.
        public fp PutterWeightPerPoint;         // putWgtCoeff: aim cycles per Putter Weight point. Default 0.125.

        public fp StaminaFloorFraction;         // 0.20 = stamina-modulated stats retain 20% at zero stamina

        public static StatCoefficients Default => new StatCoefficients
        {
            ClubPowerPerPoint         = fp.FromFloat(0.005f),
            ClubAccuracyPerPoint      = fp.FromFloat(0.0042f),
            ClubLieResistancePerPoint = fp.FromFloat(0.0042f),
            BallPowerPerPoint         = fp.FromFloat(0.01f),
            BallReboundPerPoint       = fp.FromFloat(0.01f),
            BallWindCutPerPoint       = fp.FromFloat(0.01f),
            BallRollPerPoint          = fp.FromFloat(0.01f),
            BallSpinPerPoint          = fp.FromFloat(0.01f),
            CharStrengthPerPoint      = fp.FromFloat(0.00625f),
            CharClubControlPerPoint   = fp.FromFloat(0.0035f),
            PutterControlPerPoint     = fp.FromFloat(0.0042f),
            PutterAccuracyPerPoint    = fp.FromFloat(0.0075f),
            PutterWeightPerPoint      = fp.FromFloat(0.125f),
            StaminaFloorFraction      = fp.FromFloat(0.20f),
        };
    }
}
```

### `StatCaps.cs`

```csharp
using Golfin.Physics.Math;

namespace Golfin.Physics.Stats
{
    public struct StatCaps
    {
        public fp VelocityMultiplierMax;     // 2.0 — Section 8 soft cap
        public fp AimConeReductionMax;       // 0.95 — never less than 5% of base cone
        public fp LieResistanceMax;          // 0.75 — Section 8 hard cap
        public fp OverpowerForgivenessMax;   // 0.75 — Section 8
        public fp StaminaCapMultiplierMax;   // 1.20 — character stamina stat caps total stamina at 120% of base
        public fp PutterOffCenterForgivenessMax;  // 0.50
        public fp ReboundMultiplierMax;      // 1.20
        public fp ReboundMultiplierMin;      // 0.80
        public fp RollMultiplierMax;         // 1.20
        public fp RollMultiplierMin;         // 0.80
        public fp WindCutMax;                // 0.30 — wind delta can be reduced by at most 30%

        public static StatCaps Default => new StatCaps
        {
            VelocityMultiplierMax        = fp.FromFloat(2.0f),
            AimConeReductionMax          = fp.FromFloat(0.95f),
            LieResistanceMax             = fp.FromFloat(0.75f),
            OverpowerForgivenessMax      = fp.FromFloat(0.75f),
            StaminaCapMultiplierMax      = fp.FromFloat(1.20f),
            PutterOffCenterForgivenessMax = fp.FromFloat(0.50f),
            ReboundMultiplierMax         = fp.FromFloat(1.20f),
            ReboundMultiplierMin         = fp.FromFloat(0.80f),
            RollMultiplierMax            = fp.FromFloat(1.20f),
            RollMultiplierMin            = fp.FromFloat(0.80f),
            WindCutMax                   = fp.FromFloat(0.30f),
        };
    }
}
```

---

## Part C — `ResolvedShotModifiers` and `BallPhysicsModifiers`

`ResolvedShotModifiers` is what the resolver returns. `BallPhysicsModifiers` is the slice of those modifiers that gets passed downstream into `BallSimulation` for runtime application during bounce/roll/aero.

### `ResolvedShotModifiers.cs`

```csharp
using Golfin.Physics.Math;

namespace Golfin.Physics.Stats
{
    /// <summary>
    /// All resolved per-shot modifiers. Builders consume this to construct ShotInput,
    /// then pass the BallPhysicsModifiers slice into BallSimulation alongside surface configs.
    /// </summary>
    public readonly struct ResolvedShotModifiers
    {
        // Pre-shot (consumed by ShotInputBuilder to construct ShotInput)
        public readonly fp VelocityMultiplier;       // 1.0 = base; 2.0 = max
        public readonly fp AimConeReductionFraction; // 0.0 = base cone; 0.95 = base cone × 0.05
        public readonly fp SpinMagnitudeMultiplier;  // 1.0 = base; ±0.10 from ball

        // Post-shot (consumed during simulation as BallPhysicsModifiers)
        public readonly BallPhysicsModifiers BallPhysics;

        // Informational / assist-layer (resolver outputs but sim does NOT consume)
        public readonly fp LieResistanceFraction;        // 0..0.75
        public readonly fp OverpowerForgivenessFraction; // 0..0.75
        public readonly fp PutterOffCenterForgiveness;   // 0..0.50
        public readonly fp PutterGravityWellRadiusM;     // 0.10..1.00 m (assist; gameplay layer applies)
        public readonly int PutterAimCycles;             // 5..20 (UI layer applies)

        public ResolvedShotModifiers(
            fp velocityMultiplier, fp aimConeReductionFraction, fp spinMagnitudeMultiplier,
            BallPhysicsModifiers ballPhysics,
            fp lieResistanceFraction, fp overpowerForgivenessFraction,
            fp putterOffCenterForgiveness, fp putterGravityWellRadiusM, int putterAimCycles)
        {
            VelocityMultiplier = velocityMultiplier;
            AimConeReductionFraction = aimConeReductionFraction;
            SpinMagnitudeMultiplier = spinMagnitudeMultiplier;
            BallPhysics = ballPhysics;
            LieResistanceFraction = lieResistanceFraction;
            OverpowerForgivenessFraction = overpowerForgivenessFraction;
            PutterOffCenterForgiveness = putterOffCenterForgiveness;
            PutterGravityWellRadiusM = putterGravityWellRadiusM;
            PutterAimCycles = putterAimCycles;
        }
    }
}
```

### `BallPhysicsModifiers.cs` (in `Golfin.Physics` Core namespace, NOT Stats)

This struct lives in **Core** because `BallSimulation` consumes it. The Stats assembly produces it. Core has no awareness of stats — it just multiplies by whatever scalars come in.

```csharp
using Golfin.Physics.Math;

namespace Golfin.Physics
{
    /// <summary>
    /// Per-shot ball-driven multipliers applied during simulation. Produced by the Stats
    /// resolver (or constructed manually for tests / lab); consumed by BallSimulation at
    /// bounce, roll, and aero phases.
    ///
    /// Default = Neutral = no modification (all multipliers = 1.0, WindCutFraction = 0).
    /// </summary>
    public readonly struct BallPhysicsModifiers
    {
        public readonly fp ReboundMultiplier;        // multiplies SurfaceCoefficients.Restitution at bounce
        public readonly fp RollResistanceMultiplier; // multiplies SurfaceCoefficients.RollingResistance during roll/putt
        public readonly fp WindCutFraction;          // 0..0.30; subtracted from |windDelta| before drag

        public BallPhysicsModifiers(fp reboundMultiplier, fp rollResistanceMultiplier, fp windCutFraction)
        { ReboundMultiplier = reboundMultiplier;
          RollResistanceMultiplier = rollResistanceMultiplier;
          WindCutFraction = windCutFraction; }

        public static BallPhysicsModifiers Neutral => new BallPhysicsModifiers(
            fp.One, fp.One, fp.Zero);
    }
}
```

---

## Part D — `StatModifierResolver` (the core math)

`Assets/Scripts/Physics/Stats/StatModifierResolver.cs`. Pure C#, no Unity, deterministic, no allocations beyond the returned struct.

```csharp
using Golfin.Physics.Math;

namespace Golfin.Physics.Stats
{
    public static class StatModifierResolver
    {
        public static ResolvedShotModifiers Resolve(StatBundle bundle, StatCoefficients coeffs, StatCaps caps)
        {
            // Step 1: Apply stamina scaling to character stats.
            // effective = base × max(floor, current/max)
            fp staminaFraction = (bundle.MaxStamina > fp.Zero)
                ? bundle.CurrentStamina / bundle.MaxStamina
                : fp.One;
            fp staminaMultiplier = fpMath.Max(coeffs.StaminaFloorFraction, staminaFraction);
            staminaMultiplier = fpMath.Min(staminaMultiplier, fp.One); // never amplify above 100%

            fp effStrength    = fp.FromInt(bundle.Character.Strength)    * staminaMultiplier;
            fp effClubControl = fp.FromInt(bundle.Character.ClubControl) * staminaMultiplier;

            // Step 2: Velocity multiplier.
            // Lane: Club Power × Ball Power (multiplicative; shared lane per Section 8).
            fp clubPower = bundle.IsPutt
                ? fp.Zero
                : fp.FromInt(bundle.Club.Value.Power);
            fp velFromClub = fp.One + clubPower * coeffs.ClubPowerPerPoint;
            fp velFromBall = fp.One + fp.FromInt(bundle.Ball.Power) * coeffs.BallPowerPerPoint;
            fp velocityMultiplier = velFromClub * velFromBall;
            velocityMultiplier = fpMath.Min(velocityMultiplier, caps.VelocityMultiplierMax);
            velocityMultiplier = fpMath.Max(velocityMultiplier, fp.Zero);

            // Step 3: Aim cone reduction.
            // Lane: Club Accuracy × Character Club Control (both shrink the cone).
            // reduction = 1 - (1 - clubReduction) × (1 - charReduction)
            fp clubAcc = bundle.IsPutt
                ? fp.FromInt(bundle.Putter.Value.Accuracy)
                : fp.FromInt(bundle.Club.Value.Accuracy);
            fp clubAccReduction = bundle.IsPutt ? fp.Zero : (clubAcc * coeffs.ClubAccuracyPerPoint);
            fp charControlReduction = effClubControl * coeffs.CharClubControlPerPoint;
            fp unreducedFraction = (fp.One - clubAccReduction) * (fp.One - charControlReduction);
            fp aimConeReduction = fp.One - unreducedFraction;
            aimConeReduction = fpMath.Min(aimConeReduction, caps.AimConeReductionMax);
            aimConeReduction = fpMath.Max(aimConeReduction, fp.Zero);

            // Step 4: Spin magnitude multiplier. Single-source from Ball Spin.
            fp spinMul = fp.One + fp.FromInt(bundle.Ball.Spin) * coeffs.BallSpinPerPoint;
            spinMul = fpMath.Max(spinMul, fp.Zero);

            // Step 5: Lie resistance. Lane: Club Lie Resistance only.
            fp clubLR = bundle.IsPutt ? fp.Zero : fp.FromInt(bundle.Club.Value.LieResistance);
            fp lieResist = clubLR * coeffs.ClubLieResistancePerPoint;
            lieResist = fpMath.Min(lieResist, caps.LieResistanceMax);
            lieResist = fpMath.Max(lieResist, fp.Zero);

            // Step 6: Overpower forgiveness — single-source, Character Strength only.
            fp overpower = effStrength * coeffs.CharStrengthPerPoint;
            overpower = fpMath.Min(overpower, caps.OverpowerForgivenessMax);
            overpower = fpMath.Max(overpower, fp.Zero);

            // Step 7: Putter-only outputs.
            fp putterOffCenter = fp.Zero;
            fp gravityWellRadius = fp.Zero;
            int aimCycles = 0;
            if (bundle.IsPutt)
            {
                putterOffCenter = fp.FromInt(bundle.Putter.Value.Control) * coeffs.PutterControlPerPoint;
                putterOffCenter = fpMath.Min(putterOffCenter, caps.PutterOffCenterForgivenessMax);
                gravityWellRadius = fpMath.Clamp(
                    fp.FromFloat(0.10f) + fp.FromInt(bundle.Putter.Value.Accuracy) * coeffs.PutterAccuracyPerPoint,
                    fp.FromFloat(0.10f), fp.FromFloat(1.00f));
                aimCycles = 5 + (int)((fp.FromInt(bundle.Putter.Value.Weight) * coeffs.PutterWeightPerPoint).ToFloat());
                if (aimCycles > 20) aimCycles = 20;
            }

            // Step 8: BallPhysicsModifiers — the slice consumed by BallSimulation.
            fp reboundMul = fp.One + fp.FromInt(bundle.Ball.Rebound) * coeffs.BallReboundPerPoint;
            reboundMul = fpMath.Clamp(reboundMul, caps.ReboundMultiplierMin, caps.ReboundMultiplierMax);

            // Roll: more Ball.Roll = LESS rolling resistance = farther roll.
            fp rollMul = fp.One - fp.FromInt(bundle.Ball.Roll) * coeffs.BallRollPerPoint;
            rollMul = fpMath.Clamp(rollMul, caps.RollMultiplierMin, caps.RollMultiplierMax);

            // Wind Cut: more = better. Clamp to [0, WindCutMax].
            fp windCutFraction = fp.FromInt(bundle.Ball.WindCut) * coeffs.BallWindCutPerPoint;
            windCutFraction = fpMath.Clamp(windCutFraction, fp.Zero, caps.WindCutMax);

            var ballPhysics = new BallPhysicsModifiers(reboundMul, rollMul, windCutFraction);

            return new ResolvedShotModifiers(
                velocityMultiplier, aimConeReduction, spinMul,
                ballPhysics,
                lieResist, overpower,
                putterOffCenter, gravityWellRadius, aimCycles);
        }
    }
}
```

**Notes on shape:**
- Resolver is one static method, ~80 LOC. No state, no allocs beyond the returned struct.
- All fp ops; no float; no Unity.
- `fpMath.Clamp` / `Min` / `Max` are assumed to exist. If `Clamp` doesn't, use `Min(Max(...))`.
- Ball stats can be negative — math handles it. A −10 Power ball gives velocity × 0.90 from the ball lane.
- Putter velocity multiplier intentionally bypasses Club Power lane (putters don't have that stat).

---

## Part E — `BallSimulation` integration (minimal)

`BallPhysicsModifiers` flows into `BallSimulation` as one new optional argument. Existing overloads forward `BallPhysicsModifiers.Neutral` so all Phase 1–5 tests stay bit-exact.

### Add new overload (most general)

In `Assets/Scripts/Physics/Core/BallSimulation.cs`, add after the existing 7-arg overload:

```csharp
/// <summary>
/// Phase 6 entry. Adds BallPhysicsModifiers for ball-driven runtime scalars
/// (Rebound multiplies restitution, Roll multiplies rolling resistance,
/// WindCut reduces wind-delta drag).
///
/// Existing 7-arg overload forwards BallPhysicsModifiers.Neutral so Phase 1–5
/// tests remain bit-exact.
/// </summary>
public static Trajectory Simulate(
    ShotInput input,
    IGroundProvider ground,
    AeroConfig aero,
    WindConfig wind,
    ISurfaceProvider surfaces,
    SurfaceConfig surfaceCfg,
    PuttConfig puttCfg,
    BallPhysicsModifiers ballMods)
{
    // [body of the existing 7-arg overload moves here, with four injection points:]
    //
    // 1. AT BOUNCE (in the bounce handler near where `cr` / restitution is read):
    //      cr = cr * ballMods.ReboundMultiplier;
    //
    // 2. AT ROLL (in RunRollPhase):
    //      fp3 aResistance = vel * (-(coeff.RollingResistance * ballMods.RollResistanceMultiplier));
    //
    // 3. AT PUTT (in RunPuttPhase, same pattern as roll):
    //      fp3 aResistance = vel * (-(coeff.RollingResistance * ballMods.RollResistanceMultiplier));
    //
    // 4. AT AERO (in the wind-delta drag computation in the airborne RK4 loop):
    //      fp windCutScale = fp.One - ballMods.WindCutFraction;
    //      windDelta = windDelta * windCutScale;
    //    Apply BEFORE the drag force is computed from the relative velocity.
}
```

The existing 7-arg overload becomes a thin forward:

```csharp
public static Trajectory Simulate(
    ShotInput input, IGroundProvider ground, AeroConfig aero, WindConfig wind,
    ISurfaceProvider surfaces, SurfaceConfig surfaceCfg, PuttConfig puttCfg)
    => Simulate(input, ground, aero, wind, surfaces, surfaceCfg, puttCfg, BallPhysicsModifiers.Neutral);
```

**Critical implementation note:** the body MUST move into the 8-arg overload, not stay in the 7-arg. Otherwise the 7-arg path doesn't see ballMods.

**Critical correctness note:** all Phase 1–5 tests call overloads that route through `BallPhysicsModifiers.Neutral`. Neutral has multipliers = 1.0 and WindCutFraction = 0, so:
- `cr × 1.0 = cr` (bit-exact)
- `coeff.RollingResistance × 1.0 = coeff.RollingResistance` (bit-exact)
- `windDelta × (1.0 - 0.0) = windDelta` (bit-exact)

This is the bit-exact gate. **If any Phase 1–5 test changes by even one bit after this overload is added, something is wrong** — likely a multiplication ordering issue. Stop and report.

---

## Part F — `ShotInputBuilder` (Stats assembly)

`Assets/Scripts/Physics/Stats/ShotInputBuilder.cs`. Builds a `ShotInput` from a `StatBundle` + flick magnitude + aim direction + origin.

```csharp
using Golfin.Physics.Math;

namespace Golfin.Physics.Stats
{
    public static class ShotInputBuilder
    {
        /// <summary>
        /// Build a ShotInput from resolved stats + per-shot inputs.
        /// Returns the ShotInput plus the BallPhysicsModifiers to pass into BallSimulation.
        ///
        /// flickMagnitude01: 0..1 normalized power gauge value. Values >1 are "overpower" —
        ///   reduced by overpowerForgivenessFraction, then clamped at 1.2.
        /// aimYawRadians: rotation around world Y. 0 = +X axis (per project convention).
        /// origin: world-space ball position at impact.
        /// seed: PRNG seed for any per-shot variance.
        ///
        /// IMPORTANT: This builder does NOT apply aim cone reduction. Aim cone is gameplay-layer
        /// concern — the resolver outputs the cone reduction value, and the gameplay aim reticle
        /// is what consumes it to produce the final aimYawRadians already corrected for wobble.
        /// </summary>
        public static (ShotInput input, BallPhysicsModifiers ballMods) Build(
            StatBundle bundle,
            StatCoefficients coeffs, StatCaps caps,
            fp flickMagnitude01,
            fp aimYawRadians,
            fp originX, fp originY, fp originZ,
            uint seed)
        {
            var resolved = StatModifierResolver.Resolve(bundle, coeffs, caps);

            // Apply overpower forgiveness if flick > 1.0
            fp effectiveFlick = flickMagnitude01;
            if (effectiveFlick > fp.One)
            {
                fp overshoot = effectiveFlick - fp.One;
                fp reducedOvershoot = overshoot * (fp.One - resolved.OverpowerForgivenessFraction);
                effectiveFlick = fp.One + reducedOvershoot;
                if (effectiveFlick > fp.FromFloat(1.2f)) effectiveFlick = fp.FromFloat(1.2f);
            }
            else if (effectiveFlick < fp.Zero)
            {
                effectiveFlick = fp.Zero;
            }

            // Resolve base velocity. Either Club or Putter — never both.
            fp baseVelMps = bundle.IsPutt
                ? bundle.Putter.Value.BaseVelocityMps
                : bundle.Club.Value.BaseVelocityMps;
            fp velMagnitude = baseVelMps * effectiveFlick * resolved.VelocityMultiplier;

            // Resolve launch pitch from loft.
            fp loftDeg = bundle.IsPutt
                ? bundle.Putter.Value.LoftDegrees
                : bundle.Club.Value.LoftDegrees;
            fp launchPitchRadians = loftDeg * fpMath.DegToRad;

            // Build velocity vector. Convention (per Phase 1–5):
            //   +X is "forward" at aimYaw=0; +Y is up; +Z is right.
            fp cosPitch = fpMath.Cos(launchPitchRadians);
            fp sinPitch = fpMath.Sin(launchPitchRadians);
            fp cosYaw   = fpMath.Cos(aimYawRadians);
            fp sinYaw   = fpMath.Sin(aimYawRadians);

            fp3 velocity = new fp3(
                velMagnitude * cosPitch * cosYaw,
                velMagnitude * sinPitch,
                velMagnitude * cosPitch * sinYaw);

            // Spin: backspin around the right-vector. Putts have zero spin per Phase 5 design.
            SpinState spin;
            if (bundle.IsPutt)
            {
                spin = SpinState.None;
            }
            else
            {
                fp3 spinAxis = new fp3(-sinYaw, fp.Zero, cosYaw);
                fp baseRpm = bundle.Club.Value.BaseBackspinRpm;
                fp baseRadPerSec = baseRpm * fpMath.TwoPi / fp.FromInt(60);
                fp spinMagRadPerSec = baseRadPerSec * resolved.SpinMagnitudeMultiplier;
                spin = new SpinState(spinAxis, spinMagRadPerSec);
            }

            var origin = new fp3(originX, originY, originZ);
            var input = new ShotInput(origin, velocity, fp.FromFloat(60f), spin, seed);

            return (input, resolved.BallPhysics);
        }
    }
}
```

**`fpMath.DegToRad` and `fpMath.TwoPi`** — verify these exist. `TwoPi` was added in Phase 3 per the history log. `DegToRad` may need to be added; if missing, define as `fp DegToRad => fpMath.Pi / fp.FromInt(180);`.

---

## Part G — `stats.csv` and `stat_caps.csv`

`Assets/Resources/Physics/stats.csv`:

```csv
key,value,notes
club_power_per_point,0.005,velocity multiplier per Club Power point (120 pts = +60%)
club_accuracy_per_point,0.0042,aim cone reduction per Club Accuracy point
club_lie_resistance_per_point,0.0042,lie penalty reduction per Club Lie Resistance point
ball_power_per_point,0.01,velocity multiplier per Ball Power point (10 pts = +10%)
ball_rebound_per_point,0.01,restitution multiplier per Ball Rebound point
ball_wind_cut_per_point,0.01,wind drag reduction per Ball Wind Cut point
ball_roll_per_point,0.01,rolling resistance reduction per Ball Roll point (more = farther roll)
ball_spin_per_point,0.01,spin magnitude multiplier per Ball Spin point
char_strength_per_point,0.00625,overpower forgiveness per Character Strength point
char_club_control_per_point,0.0035,aim cone reduction per Character Club Control point
putter_control_per_point,0.0042,off-center forgiveness per Putter Control point
putter_accuracy_per_point,0.0075,gravity well radius per Putter Accuracy point (assist; informational)
putter_weight_per_point,0.125,aim cycles per Putter Weight point (UI; informational)
stamina_floor_fraction,0.20,minimum effective fraction for stamina-modulated stats at 0 stamina
```

`Assets/Resources/Physics/stat_caps.csv`:

```csv
key,value,notes
velocity_multiplier_max,2.0,Section 8 soft cap on combined Club×Ball velocity boost
aim_cone_reduction_max,0.95,never less than 5% of base cone (some miss is always possible)
lie_resistance_max,0.75,Section 8 hard cap
overpower_forgiveness_max,0.75,Section 8 hard cap on Strength
stamina_cap_multiplier_max,1.20,Character Stamina stat caps total stamina at 120% (informational)
putter_off_center_forgiveness_max,0.50,Section 8 hard cap
rebound_multiplier_max,1.20,Ball Rebound can boost restitution +20% max
rebound_multiplier_min,0.80,Ball Rebound can reduce restitution to 80% min
roll_multiplier_max,1.20,Ball Roll can swing rolling resistance ±20%
roll_multiplier_min,0.80,
wind_cut_max,0.30,wind delta drag can be reduced by at most 30%
```

### Loader extensions in `PhysicsConfigLoader.cs`

Add two methods following the existing `LoadAeroConfig` pattern. `PhysicsConfigLoader` references `Golfin.Physics.Stats` — add it to `Golfin.Physics.Runtime.asmdef` references list.

```csharp
public static StatCoefficients LoadStatCoefficients()
{
    var cfg = StatCoefficients.Default;
    var ta = Resources.Load<TextAsset>("Physics/stats");
    if (ta == null) { Debug.LogWarning("[PhysicsConfigLoader] Physics/stats.csv not found — using defaults"); return cfg; }

    foreach (var raw in ta.text.Split('\n'))
    {
        var line = raw.Trim();
        if (line.Length == 0 || line.StartsWith("#") || line.StartsWith("key,")) continue;
        var parts = line.Split(',');
        if (parts.Length < 2) continue;
        string key = parts[0].Trim();
        if (!float.TryParse(parts[1].Trim(),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float v)) continue;
        switch (key)
        {
            case "club_power_per_point":           cfg.ClubPowerPerPoint           = fp.FromFloat(v); break;
            case "club_accuracy_per_point":        cfg.ClubAccuracyPerPoint        = fp.FromFloat(v); break;
            case "club_lie_resistance_per_point":  cfg.ClubLieResistancePerPoint   = fp.FromFloat(v); break;
            case "ball_power_per_point":           cfg.BallPowerPerPoint           = fp.FromFloat(v); break;
            case "ball_rebound_per_point":         cfg.BallReboundPerPoint         = fp.FromFloat(v); break;
            case "ball_wind_cut_per_point":        cfg.BallWindCutPerPoint         = fp.FromFloat(v); break;
            case "ball_roll_per_point":            cfg.BallRollPerPoint            = fp.FromFloat(v); break;
            case "ball_spin_per_point":            cfg.BallSpinPerPoint            = fp.FromFloat(v); break;
        }
    }
    return cfg;
}
```

*Note: switch above is abbreviated; Code should fill in remaining keys (`char_strength_per_point`, `char_club_control_per_point`, `putter_*`, `stamina_floor_fraction`) following the same one-line-per-key pattern. Same for `LoadStatCaps()` against `stat_caps.csv` — pattern matches `LoadAeroConfig`.*

```csharp
public static StatCaps LoadStatCaps()
{
    var caps = StatCaps.Default;
    var ta = Resources.Load<TextAsset>("Physics/stat_caps");
    if (ta == null) { Debug.LogWarning("[PhysicsConfigLoader] Physics/stat_caps.csv not found — using defaults"); return caps; }

    foreach (var raw in ta.text.Split('\n'))
    {
        var line = raw.Trim();
        if (line.Length == 0 || line.StartsWith("#") || line.StartsWith("key,")) continue;
        var parts = line.Split(',');
        if (parts.Length < 2) continue;
        string key = parts[0].Trim();
        if (!float.TryParse(parts[1].Trim(),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float v)) continue;
        switch (key)
        {
            case "velocity_multiplier_max":            caps.VelocityMultiplierMax        = fp.FromFloat(v); break;
            case "aim_cone_reduction_max":             caps.AimConeReductionMax          = fp.FromFloat(v); break;
            case "lie_resistance_max":                 caps.LieResistanceMax             = fp.FromFloat(v); break;
            case "overpower_forgiveness_max":          caps.OverpowerForgivenessMax      = fp.FromFloat(v); break;
            case "stamina_cap_multiplier_max":         caps.StaminaCapMultiplierMax      = fp.FromFloat(v); break;
            case "putter_off_center_forgiveness_max":  caps.PutterOffCenterForgivenessMax = fp.FromFloat(v); break;
            case "rebound_multiplier_max":             caps.ReboundMultiplierMax         = fp.FromFloat(v); break;
            case "rebound_multiplier_min":             caps.ReboundMultiplierMin         = fp.FromFloat(v); break;
            case "roll_multiplier_max":                caps.RollMultiplierMax            = fp.FromFloat(v); break;
            case "roll_multiplier_min":                caps.RollMultiplierMin            = fp.FromFloat(v); break;
            case "wind_cut_max":                       caps.WindCutMax                   = fp.FromFloat(v); break;
        }
    }
    return caps;
}
```

---

## Part H — Tests

`Assets/Scripts/Physics/Tests/StatResolverTests.cs` — new. Namespace `Golfin.Physics.Tests`. **10 tests, EditMode.**

1. **`Stats_Phase5Overloads_BitExact`** — run a 7-iron shot through the existing 7-arg `Simulate(...)` AND through the new 8-arg `Simulate(...)` with `BallPhysicsModifiers.Neutral`. Trajectories must be bit-exact identical. **Blocking gate.** Compare every `samples[i].position.x.raw` etc.

2. **`Stats_Resolve_Neutral_AllNeutral`** — bundle of all-zero stats, neutral ball, neutral character, full stamina. Assert: `VelocityMultiplier == 1.0`, `AimConeReductionFraction == 0.0`, `BallPhysics == BallPhysicsModifiers.Neutral`, `LieResistanceFraction == 0.0`, `OverpowerForgivenessFraction == 0.0`.

3. **`Stats_Resolve_MaxedSupreme_RespectsVelocityCap`** — Supreme driver (Power 120) + +10 Power ball + maxed Strength character + full stamina. Assert: `VelocityMultiplier <= caps.VelocityMultiplierMax (== 2.0)`. Math gives `1.60 × 1.10 = 1.76`. Then override coeffs to push past 2.0 and assert cap engages.

4. **`Stats_Resolve_StrengthDoesNotAddVelocity`** — bundle A: Strength 120, all other stats 0. Bundle B: Strength 0, all other stats 0. Assert: `velocityMultiplier_A == velocityMultiplier_B` (both 1.0). **Catches the most likely Section 8 violation.**

5. **`Stats_Resolve_StaminaHalfReducesCharacterContribution`** — bundle with Strength 120, ClubControl 120, current stamina = max/2. Assert: `OverpowerForgivenessFraction` equals what 60-effective-Strength would produce (`60 × 0.00625 = 0.375`, clamped). Confirms Step 1 of resolver applies before everything else.

6. **`Stats_Resolve_StaminaZero_HitsFloor`** — same maxed character bundle but `currentStamina = 0`. Assert: effective Strength = `120 × 0.20 = 24`, so `OverpowerForgivenessFraction = 24 × 0.00625 = 0.15`. Confirms the 20% floor.

7. **`Stats_Resolve_BallStatsAffectBallPhysics`** — bundle with Ball.Rebound = +10. Assert: `BallPhysics.ReboundMultiplier == 1.10`. Then -10 → 0.90. Then +999 → clamped to 1.20. Same pattern for Roll and WindCut.

8. **`Stats_Resolve_BitExactDeterminism`** — same `StatBundle`/coeffs/caps → call `Resolve` 5 times. Assert all 5 outputs bit-equal (compare every `.raw` field).

9. **`Stats_Build_DriverNeutralStats_GivesReasonableShotInput`** — `ShotInputBuilder.Build(...)` with a driver bundle (BaseVelocity 70 m/s, loft 11°, 2700 rpm), neutral ball, neutral character, flick = 1.0, aimYaw = 0. Assert: `input.velocity.x ≈ 70 × cos(11°) ≈ 68.7 m/s` (within fp tolerance), `input.velocity.y ≈ 70 × sin(11°) ≈ 13.4 m/s`, `input.velocity.z ≈ 0`. Spin magnitude ≈ 282.7 rad/s.

10. **`Stats_Build_OverpowerWithStrength_ReducesOvershoot`** — flick = 1.10. Bundle A: Strength 0. Bundle B: Strength 120. Assert: `velocityMagnitude_B < velocityMagnitude_A` because B's forgiveness reduces the overshoot from 0.10 → 0.025. Both at-or-below 1.20 hard ceiling.

All existing tests must still pass (Phase 1–5 + Viewer = 39). Phase 6 adds 10. **Target: 49 tests total, 49 pass.**

---

## Part I — Tuning window (optional polish, do if time allows)

If implementation goes faster than expected, add a "Stats" foldout to `PhysicsTuningWindow.cs`:
- All 14 coefficient sliders + 11 cap sliders
- "Reload stats.csv" + "Reload stat_caps.csv" buttons
- "Resolve preview" panel: input fields for stats, button "Resolve", shows resolved struct.

If time-pressed, defer. CSV editing + lab fire-and-check is workable.

---

## Part J — Unity-MCP autonomous validation

1. Compile clean. `console-get-logs` after each major change, max 5 iterations.
2. `tests-run` filter `Golfin.Physics.Tests`. All 49 pass.
3. **`Stats_Phase5Overloads_BitExact` is the blocking gate.** If it fails, stop and report.
4. Manual `script-execute`: call `StatModifierResolver.Resolve(...)` with maxed-Supreme bundle, print resolved struct to console. Capture log.
5. `ShotInputBuilder.Build(...)` with driver bundle → `BallSimulation.Simulate(...)`. Report carry distance — should be in lab's `driver_calm` window (240–280m).

## Done report

- 49-test pass/fail summary.
- Phase 5 bit-exact gate result.
- Resolved struct dump from a maxed-Supreme bundle.
- Driver carry from the `Build → Simulate` round trip.
- Final `stats.csv` and `stat_caps.csv` contents.
- Any anomalies, deviations, surprises.
- Note: did `fpMath.DegToRad` already exist? Did `fpMath.Clamp` already exist?

## DO NOT

- Modify Phase 1–5 tests.
- Modify `ShotInput` struct.
- Apply aim cone reduction inside `ShotInputBuilder` (gameplay layer).
- Apply gravity well anywhere in the sim (assist layer).
- Tune `clubs.csv` to make tests pass.
- Touch any Phase 1–5 CSV.
- Add `BaseBackspinRpm` to `clubs.csv` — it's a constructor arg on `ClubStats`, gameplay supplies per-instance.
- Build any UI beyond the optional Part I tuning window foldout.
- Hook any of this up to the lab.

## Iteration budget

- 0 iterations on resolver math. If a test fails, the math or test is wrong — re-read Section 8, fix the wrong side, re-run.
- 3 iterations max on `BallSimulation` injection points. If bit-exact gate keeps failing after 3 attempts, stop and report.
- Coefficient calibration is OUT OF SCOPE for Code. Cesar tunes against carry distances after Code reports done.
