using Golfin.Physics.Math;

namespace Golfin.Physics.Stats
{
    public static class ShotInputBuilder
    {
#if UNITY_EDITOR
        /// <summary>
        /// Wired by the runtime layer to Debug.Log. Emits a snapshot of bundle + inputs +
        /// resolved values at the end of Build(). Null-safe; zero overhead when unwired.
        /// </summary>
        public static System.Action<string> DiagBuildLogger;
#endif

        /// <summary>
        /// Build a ShotInput from resolved stats + per-shot inputs.
        /// Returns the ShotInput plus the BallPhysicsModifiers to pass into BallSimulation.
        ///
        /// flickMagnitude01: 0..1 normalized power gauge value. Values >1 are "overpower" —
        ///   penalty reduced by overpowerForgivenessFraction, then clamped at 1.2.
        /// aimYawRadians: rotation around world Y. 0 = +X forward (project convention).
        /// origin: world-space ball position at impact.
        /// seed: PRNG seed for per-shot variance.
        ///
        /// Aim cone reduction is NOT applied here — that is the gameplay layer's concern.
        /// The resolved AimConeReductionFraction is consumed by the aim reticle UI,
        /// which produces the final aimYawRadians already adjusted for wobble.
        /// </summary>
        public static (Golfin.Physics.ShotInput input, Golfin.Physics.BallPhysicsModifiers ballMods) Build(
            StatBundle bundle,
            StatCoefficients coeffs, StatCaps caps,
            fp flickMagnitude01,
            fp aimYawRadians,
            fp originX, fp originY, fp originZ,
            uint seed,
            fp baseVelocityOverrideMps = default,
            fp spinInputX = default,                        // NEW — spin.x (draw/fade orbital tilt), 0=no tilt
            fp spinInputY = default,                        // NEW — spin.y (backspin/topspin scale), 0=no scaling
            fp spinMagScaleSlope = default,                 // NEW — 0 → no scaling (legacy behavior)
            fp spinMaxTiltRad = default)                    // NEW — 0 → no tilt (legacy behavior)
        {
            var resolved = StatModifierResolver.Resolve(bundle, coeffs, caps);

            // Apply overpower forgiveness when flick > 1.0.
            fp effectiveFlick = flickMagnitude01;
            if (effectiveFlick > fp.One)
            {
                fp overshoot        = effectiveFlick - fp.One;
                fp reducedOvershoot = overshoot * (fp.One - resolved.OverpowerForgivenessFraction);
                effectiveFlick      = fp.One + reducedOvershoot;
                fp maxFlick         = fp.FromFloat(1.2f);
                if (effectiveFlick > maxFlick) effectiveFlick = maxFlick;
            }
            else if (effectiveFlick < fp.Zero)
            {
                effectiveFlick = fp.Zero;
            }

            // Base velocity: explicit override (e.g. from ControlsConfig.PuttBaseVelocityMps) takes
            // priority when > 0; otherwise falls back to the StatBundle's club/putter value.
            fp baseVelMps = baseVelocityOverrideMps > fp.Zero
                ? baseVelocityOverrideMps
                : bundle.IsPutt
                    ? bundle.Putter.Value.BaseVelocityMps
                    : bundle.Club.Value.BaseVelocityMps;
            fp velMagnitude = baseVelMps * effectiveFlick * resolved.VelocityMultiplier;

            // Launch pitch from loft.
            fp loftDeg = bundle.IsPutt
                ? bundle.Putter.Value.LoftDegrees
                : bundle.Club.Value.LoftDegrees;
            fp launchPitchRadians = loftDeg * fpMath.DegToRad;

            // Velocity vector: +X forward at aimYaw=0, +Y up, +Z right.
            fp cosPitch = fpMath.Cos(launchPitchRadians);
            fp sinPitch = fpMath.Sin(launchPitchRadians);
            fp cosYaw   = fpMath.Cos(aimYawRadians);
            fp sinYaw   = fpMath.Sin(aimYawRadians);

            var velocity = new fp3(
                velMagnitude * cosPitch * cosYaw,
                velMagnitude * sinPitch,
                velMagnitude * cosPitch * sinYaw);

            // Spin: backspin around right-vector, modulated by per-shot spin input.
            // Putts have no spin (Phase 5 design lock; see spin_and_shot_shape_wiring SPEC §Out of scope).
            Golfin.Physics.SpinState spin;
            if (bundle.IsPutt)
            {
                spin = Golfin.Physics.SpinState.None;
            }
            else
            {
                fp baseRpm       = bundle.Club.Value.BaseBackspinRpm;
                fp baseRadPerSec = baseRpm * fpMath.TwoPi / fp.FromInt(60);
                fp baseSpinMag   = baseRadPerSec * resolved.SpinMagnitudeMultiplier;

                // Starting axis: pure right-vector backspin (legacy, when spinInput=(0,0)).
                fp3 startAxis = new fp3(-sinYaw, fp.Zero, cosYaw);

                // Q2(a): sign-flip allowed. magScale signed; e.g. spinY=+1 with slope=1.5 → -0.5 (topspin).
                fp magScale = fp.One - spinInputY * spinMagScaleSlope;

                // Q3(a): orbital tilt around velocity vector. spinX × maxTilt radians.
                fp3 finalAxis;
                if (spinInputX != fp.Zero && spinMaxTiltRad != fp.Zero)
                {
                    fp3 velocityDir = fpMath.Normalize(velocity);
                    fp  tiltAngle   = spinInputX * spinMaxTiltRad;
                    finalAxis = fpMath.Rotate(startAxis, velocityDir, tiltAngle);
                }
                else
                {
                    finalAxis = startAxis;
                }

                // SpinState convention: Rate > 0 = "backspin" (axis encodes the actual direction).
                // For negative magScale (true topspin), negate axis and use |magScale|.
                fp finalRate;
                if (magScale >= fp.Zero)
                {
                    finalRate = magScale * baseSpinMag;
                }
                else
                {
                    finalAxis = finalAxis * (-fp.One);
                    finalRate = (-magScale) * baseSpinMag;
                }

                spin = new Golfin.Physics.SpinState(finalAxis, finalRate);
            }

            var origin = new fp3(originX, originY, originZ);
            var input  = new Golfin.Physics.ShotInput(origin, velocity, fp.FromFloat(60f), spin, seed);

#if UNITY_EDITOR
            if (DiagBuildLogger != null)
            {
                string clubVel    = bundle.Club.HasValue   ? bundle.Club.Value.BaseVelocityMps.ToFloat().ToString("F2")   : "n/a";
                string putterVel  = bundle.Putter.HasValue ? bundle.Putter.Value.BaseVelocityMps.ToFloat().ToString("F2") : "n/a";
                string overrideStr = baseVelocityOverrideMps.ToFloat().ToString("F2");
                DiagBuildLogger(
                    $"[Build] isPutt={bundle.IsPutt} " +
                    $"override={overrideStr}m/s clubVel={clubVel}m/s putterVel={putterVel}m/s " +
                    $"-> baseVelMps={baseVelMps.ToFloat():F2} " +
                    $"effectiveFlick={effectiveFlick.ToFloat():F3} " +
                    $"velMultiplier={resolved.VelocityMultiplier.ToFloat():F3} " +
                    $"-> velMagnitude={velMagnitude.ToFloat():F2}m/s " +
                    $"loft={loftDeg.ToFloat():F1}deg aimYaw={aimYawRadians.ToFloat():F3}rad " +
                    $"finalVel=({velocity.x.ToFloat():F2},{velocity.y.ToFloat():F2},{velocity.z.ToFloat():F2}) " +
                    $"spinInput=({spinInputX.ToFloat():F2},{spinInputY.ToFloat():F2}) " +
                    $"spinAxis=({spin.Axis.x.ToFloat():F2},{spin.Axis.y.ToFloat():F2},{spin.Axis.z.ToFloat():F2}) " +
                    $"spinRate={spin.Rate.ToFloat():F1}rad/s");
            }
#endif
            return (input, resolved.BallPhysics);
        }
    }
}
