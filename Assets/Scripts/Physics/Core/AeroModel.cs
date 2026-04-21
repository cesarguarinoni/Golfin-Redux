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
        public static fp3 ComputeAeroForce(fp3 velocity, SpinState spin, AeroConfig cfg)
        {
            fp speedSq = fpMath.Dot(velocity, velocity);
            if (speedSq <= fp.Epsilon) return fp3.Zero;

            fp speed = fpMath.Sqrt(speedSq);
            fp3 vHat = velocity / speed;

            // Drag: opposes velocity. Magnitude = ½ ρ A Cd(|v|) |v|²
            fp cd = (cfg.UseDragLut && cfg.DragLut.IsValid)
                ? cfg.DragLut.Evaluate(speed)
                : cfg.DragCoefficient;

            // Spin-induced drag: Cd_total += SpinDragFactor × S²  (induced drag from lift)
            if (cfg.SpinDragFactor > fp.Epsilon && spin.IsSpinning)
            {
                fp spinParam = (cfg.BallRadius * spin.Rate) / speed;
                cd = cd + cfg.SpinDragFactor * spinParam * spinParam;
            }

            fp dragScalar = (cfg.AirDensity * cfg.BallCrossSection * cd * speedSq) * fp.Half;
            fp3 drag = vHat * (-dragScalar);

            if (!spin.IsSpinning) return drag;

            // Lift (Magnus): ½ ρ A Cl(S) |v|² (ŵ × v̂)
            fp cl;
            if (cfg.UseLiftLut && cfg.LiftLut.IsValid)
            {
                // Spin parameter S = r · ω / |v|
                fp spinParam = (cfg.BallRadius * spin.Rate) / speed;
                cl = cfg.LiftLut.Evaluate(spinParam);
            }
            else
            {
                // Constant-mode legacy path: linear-capped Cl
                fp spinScale = fpMath.Clamp(spin.Rate / cfg.SpinRateReference, fp.Zero, cfg.LiftMaxMultiplier);
                cl = cfg.LiftCoefficientBase * spinScale;
            }

            if (cl <= fp.Epsilon) return drag;

            fp liftScalar = (cfg.AirDensity * cfg.BallCrossSection * cl * speedSq) * fp.Half;
            fp3 liftDir = fpMath.Cross(spin.Axis, vHat);
            fp3 lift = liftDir * liftScalar;

            return drag + lift;
        }
    }
}
