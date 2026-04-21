using Golfin.Physics.Math;

namespace Golfin.Physics
{
    /// <summary>
    /// Computes aerodynamic force (drag + Magnus lift) at a given velocity and spin.
    /// Gravity is handled separately in BallSimulation.
    /// Result is in Newtons; BallSimulation divides by BallMass to get acceleration.
    /// </summary>
    public static class AeroModel
    {
        /// <summary>
        /// Returns drag + Magnus lift force in Newtons.
        /// Returns zero if speed is negligible or both Cd/Cl are zero (vacuum path).
        /// </summary>
        public static fp3 ComputeAeroForce(fp3 velocity, SpinState spin, AeroConfig cfg)
        {
            fp speedSq = fpMath.Dot(velocity, velocity);
            if (speedSq <= fp.Epsilon) return fp3.Zero;

            fp speed = fpMath.Sqrt(speedSq);
            fp3 vHat = velocity / speed;

            // Drag: opposes velocity. Magnitude = ½ ρ A Cd |v|²
            // Reorder multiply before divide to preserve Q16.16 precision.
            fp dragScalar = (cfg.AirDensity * cfg.BallCrossSection * cfg.DragCoefficient * speedSq) * fp.Half;
            fp3 drag = vHat * (-dragScalar);

            if (!spin.IsSpinning || cfg.LiftCoefficientBase <= fp.Epsilon)
                return drag;

            // Lift (Magnus): ½ ρ A Cl_eff |v|² (ŵ × v̂)
            // Cl_eff = Cl_base * clamp(spinRate / spinRateRef, 0, ClMaxMult)
            fp spinScale = fpMath.Clamp(spin.Rate / cfg.SpinRateReference, fp.Zero, cfg.LiftMaxMultiplier);
            fp clEff = cfg.LiftCoefficientBase * spinScale;
            fp liftScalar = (cfg.AirDensity * cfg.BallCrossSection * clEff * speedSq) * fp.Half;
            fp3 liftDir = fpMath.Cross(spin.Axis, vHat);
            fp3 lift = liftDir * liftScalar;

            return drag + lift;
        }
    }
}
