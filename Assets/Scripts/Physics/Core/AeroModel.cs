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
        /// Supports velocity-indexed Cd LUT and spin-parameter-indexed Cl LUT (Phase 2.1).
        /// Falls back to constant-mode coefficients when LUTs are absent or disabled.
        /// </summary>
        /// <summary>
        /// Aero force under wind. Drag and lift are computed against velocity_relative =
        /// ball_velocity - wind_velocity. Result in Newtons.
        /// </summary>
        public static fp3 ComputeAeroForce(fp3 velocity, fp3 windVelocity, SpinState spin, AeroConfig cfg)
        {
            fp3 vRel = velocity - windVelocity;
            fp speedSq = fpMath.Dot(vRel, vRel);
            if (speedSq <= fp.Epsilon) return fp3.Zero;

            fp speed = fpMath.Sqrt(speedSq);
            fp3 vRelHat = vRel / speed;

            // Drag: opposes relative velocity direction. Magnitude = ½ ρ A Cd(|vRel|) |vRel|²
            fp cd = (cfg.UseDragLut && cfg.DragLut.IsValid)
                ? cfg.DragLut.Evaluate(speed)
                : cfg.DragCoefficient;

            fp dragScalar = (cfg.AirDensity * cfg.BallCrossSection * cd * speedSq) * fp.Half;
            fp3 drag = vRelHat * (-dragScalar);

            if (!spin.IsSpinning) return drag;

            // Lift (Magnus): ½ ρ A Cl(S) |vRel|² (ŵ × v̂rel)
            // Spin parameter uses relative speed — dimple flow responds to airflow, not ground speed.
            fp cl;
            if (cfg.UseLiftLut && cfg.LiftLut.IsValid)
            {
                fp spinParam = (cfg.BallRadius * spin.Rate) / speed;
                cl = cfg.LiftLut.Evaluate(spinParam);
            }
            else
            {
                fp spinScale = fpMath.Clamp(spin.Rate / cfg.SpinRateReference, fp.Zero, cfg.LiftMaxMultiplier);
                cl = cfg.LiftCoefficientBase * spinScale;
            }

            if (cl <= fp.Epsilon) return drag;

            fp liftScalar = (cfg.AirDensity * cfg.BallCrossSection * cl * speedSq) * fp.Half;
            fp3 liftDir = fpMath.Cross(spin.Axis, vRelHat);
            return drag + liftDir * liftScalar;
        }

        // Back-compat: wind-free call forwards to wind-aware overload with zero wind.
        public static fp3 ComputeAeroForce(fp3 velocity, SpinState spin, AeroConfig cfg)
            => ComputeAeroForce(velocity, fp3.Zero, spin, cfg);
    }
}
