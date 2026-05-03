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
            fp baseVelocityOverrideMps = default)
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

            // Spin: backspin around right-vector. Putts have no spin (Phase 5 design).
            Golfin.Physics.SpinState spin;
            if (bundle.IsPutt)
            {
                spin = Golfin.Physics.SpinState.None;
            }
            else
            {
                var spinAxis      = new fp3(-sinYaw, fp.Zero, cosYaw);
                fp  baseRpm       = bundle.Club.Value.BaseBackspinRpm;
                fp  baseRadPerSec = baseRpm * fpMath.TwoPi / fp.FromInt(60);
                fp  spinMag       = baseRadPerSec * resolved.SpinMagnitudeMultiplier;
                spin = new Golfin.Physics.SpinState(spinAxis, spinMag);
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
                    $"finalVel=({velocity.x.ToFloat():F2},{velocity.y.ToFloat():F2},{velocity.z.ToFloat():F2})");
            }
#endif
            return (input, resolved.BallPhysics);
        }
    }
}
