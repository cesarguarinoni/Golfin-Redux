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
            uint seed)
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

            // Base velocity from Club or Putter.
            fp baseVelMps = bundle.IsPutt
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

            return (input, resolved.BallPhysics);
        }
    }
}
