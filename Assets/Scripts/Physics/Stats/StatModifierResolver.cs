using Golfin.Physics.Math;

namespace Golfin.Physics.Stats
{
    public static class StatModifierResolver
    {
        public static ResolvedShotModifiers Resolve(StatBundle bundle, StatCoefficients coeffs, StatCaps caps)
        {
            // Step 1: Apply stamina scaling to character stats.
            // effective = base × max(floor, current/max). Capped at 1.0 — stamina never amplifies.
            fp staminaFraction = (bundle.MaxStamina > fp.Zero)
                ? bundle.CurrentStamina / bundle.MaxStamina
                : fp.One;
            fp staminaMultiplier = fpMath.Max(coeffs.StaminaFloorFraction, staminaFraction);
            staminaMultiplier    = fpMath.Min(staminaMultiplier, fp.One);

            fp effStrength    = fp.FromInt(bundle.Character.Strength)    * staminaMultiplier;
            fp effClubControl = fp.FromInt(bundle.Character.ClubControl) * staminaMultiplier;

            // Step 2: Velocity multiplier.
            // Lane: Club Power × Ball Power (multiplicative, shared lane per Section 8).
            fp clubPower  = bundle.IsPutt ? fp.Zero : fp.FromInt(bundle.Club.Value.Power);
            fp velFromClub = fp.One + clubPower * coeffs.ClubPowerPerPoint;
            fp velFromBall = fp.One + fp.FromInt(bundle.Ball.Power) * coeffs.BallPowerPerPoint;
            fp velocityMultiplier = velFromClub * velFromBall;
            velocityMultiplier    = fpMath.Min(velocityMultiplier, caps.VelocityMultiplierMax);
            velocityMultiplier    = fpMath.Max(velocityMultiplier, fp.Zero);

            // Step 3: Aim cone reduction.
            // Lane: Club Accuracy × Character Club Control. Putters don't use Club Accuracy here.
            // reduction = 1 − (1 − clubReduction) × (1 − charReduction)
            fp clubAccReduction  = bundle.IsPutt
                ? fp.Zero
                : fp.FromInt(bundle.Club.Value.Accuracy) * coeffs.ClubAccuracyPerPoint;
            fp charControlReduction = effClubControl * coeffs.CharClubControlPerPoint;
            fp unreducedFraction    = (fp.One - clubAccReduction) * (fp.One - charControlReduction);
            fp aimConeReduction     = fp.One - unreducedFraction;
            aimConeReduction        = fpMath.Min(aimConeReduction, caps.AimConeReductionMax);
            aimConeReduction        = fpMath.Max(aimConeReduction, fp.Zero);

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
