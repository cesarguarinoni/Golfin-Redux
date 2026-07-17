using Golfin.Physics.Math;

namespace Golfin.Physics.Stats
{
    public static class StatModifierResolver
    {
        public static ResolvedShotModifiers Resolve(StatBundle bundle, StatCoefficients coeffs, StatCaps caps)
        {
            // Step 1: Character stats — consumed raw from the bundle.
            // Stamina degradation is applied EXACTLY ONCE at LiveStatProviderHost.BuildCharacterStats
            // via StaminaModel.EffectiveStat before the bundle is built.  Do NOT re-apply it here.
            // (Order 731 — retired the duplicate resolver-side staminaMultiplier lane, 2026-07-16.)
            fp effStrength = fp.FromInt(bundle.Character.Strength);

            // Step 2: Velocity multiplier.
            // Lane: Club Power × Ball Power × Character Strength (multiplicative).
            // NOTE F7 (2026-05-25): added Character.Strength factor so the bus's
            // live-stat resolution is observable on swing carry. Full lane audit
            // pending in `Docs/Specs/Queued/stat_to_physics_mapping_audit/SPEC.md`.
            fp clubPower   = bundle.IsPutt ? fp.Zero : fp.FromInt(bundle.Club.Value.Power);
            fp velFromClub = fp.One + clubPower * coeffs.ClubPowerPerPoint;
            fp velFromBall = fp.One + fp.FromInt(bundle.Ball.Power) * coeffs.BallPowerPerPoint;
            fp velFromChar = bundle.IsPutt
                ? fp.One
                : fp.One + effStrength * coeffs.CharStrengthVelocityPerPoint;
            fp velocityMultiplier = velFromClub * velFromBall * velFromChar;
            velocityMultiplier    = fpMath.Min(velocityMultiplier, caps.VelocityMultiplierMax);
            velocityMultiplier    = fpMath.Max(velocityMultiplier, fp.Zero);

            // Step 3: Aim cone reduction — single-source from Club Accuracy (design §6, ruling 2026-07-16).
            // Character ClubControl drives arrow speed (ShotController.TickArrow), NOT cone width.
            // The charControlReduction term was removed by Order 731 (2026-07-16).
            fp clubAccReduction = bundle.IsPutt
                ? fp.Zero
                : fp.FromInt(bundle.Club.Value.Accuracy) * coeffs.ClubAccuracyPerPoint;
            fp aimConeReduction = clubAccReduction;
            aimConeReduction    = fpMath.Min(aimConeReduction, caps.AimConeReductionMax);
            aimConeReduction    = fpMath.Max(aimConeReduction, fp.Zero);

            // Step 4: Spin magnitude multiplier — single-source from Ball Spin.
            fp spinMul = fp.One + fp.FromInt(bundle.Ball.Spin) * coeffs.BallSpinPerPoint;
            spinMul    = fpMath.Max(spinMul, fp.Zero);

            // Step 5: Lie resistance — single-source from Club Lie Resistance.
            fp lieResist = bundle.IsPutt
                ? fp.Zero
                : fp.FromInt(bundle.Club.Value.LieResistance) * coeffs.ClubLieResistancePerPoint;
            lieResist = fpMath.Min(lieResist, caps.LieResistanceMax);
            lieResist = fpMath.Max(lieResist, fp.Zero);

            // Step 6: Overpower forgiveness — single-source, Character Strength only.
            fp overpower = effStrength * coeffs.CharStrengthPerPoint;
            overpower    = fpMath.Min(overpower, caps.OverpowerForgivenessMax);
            overpower    = fpMath.Max(overpower, fp.Zero);

            // Step 7: Putter-only outputs.
            fp  putterOffCenter  = fp.Zero;
            fp  gravityWellRadius = fp.Zero;
            int aimCycles        = 0;
            if (bundle.IsPutt)
            {
                putterOffCenter = fp.FromInt(bundle.Putter.Value.Control) * coeffs.PutterControlPerPoint;
                putterOffCenter = fpMath.Min(putterOffCenter, caps.PutterOffCenterForgivenessMax);
                putterOffCenter = fpMath.Max(putterOffCenter, fp.Zero);

                gravityWellRadius = fpMath.Clamp(
                    fp.FromFloat(0.10f) + fp.FromInt(bundle.Putter.Value.Accuracy) * coeffs.PutterAccuracyPerPoint,
                    fp.FromFloat(0.10f), fp.FromFloat(1.00f));

                aimCycles = 5 + (int)((fp.FromInt(bundle.Putter.Value.Weight) * coeffs.PutterWeightPerPoint).ToFloat());
                if (aimCycles > 20) aimCycles = 20;
                if (aimCycles < 5)  aimCycles = 5;
            }

            // Step 8: BallPhysicsModifiers — the slice consumed by BallSimulation.
            fp reboundMul = fp.One + fp.FromInt(bundle.Ball.Rebound) * coeffs.BallReboundPerPoint;
            reboundMul    = fpMath.Clamp(reboundMul, caps.ReboundMultiplierMin, caps.ReboundMultiplierMax);

            // Roll: more Ball.Roll = LESS rolling resistance = ball rolls farther.
            fp rollMul = fp.One - fp.FromInt(bundle.Ball.Roll) * coeffs.BallRollPerPoint;
            rollMul    = fpMath.Clamp(rollMul, caps.RollMultiplierMin, caps.RollMultiplierMax);

            // WindCut: more = better (cuts through wind). Clamped to [0, WindCutMax].
            fp windCutFraction = fp.FromInt(bundle.Ball.WindCut) * coeffs.BallWindCutPerPoint;
            windCutFraction    = fpMath.Clamp(windCutFraction, fp.Zero, caps.WindCutMax);

            var ballPhysics = new Golfin.Physics.BallPhysicsModifiers(reboundMul, rollMul, windCutFraction);

            return new ResolvedShotModifiers(
                velocityMultiplier, aimConeReduction, spinMul,
                ballPhysics,
                lieResist, overpower,
                putterOffCenter, gravityWellRadius, aimCycles);
        }
    }
}
