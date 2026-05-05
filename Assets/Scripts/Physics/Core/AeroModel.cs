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
            fp cd;
            if (cfg.UseDragLut && cfg.DragLut.IsValid)
            {
                cd = cfg.DragLut.Evaluate(speed);

                // Layer 2 corner-case overlay (controls_f_drag_calibration_audit).
                // Smoothstep blend across v ∈ [45, 55] m/s prevents discontinuity between
                // Layer 1 (Bearman-Harvey valid) and overlay (extrapolation territory).
                // See Docs/Physics/CALIBRATION_METHODOLOGY.md §9.
                if (cfg.UseDragOverlay && cfg.DragOverlay.IsValid)
                {
                    fp multRaw = cfg.DragOverlay.Evaluate(speed);
                    fp mult    = BlendDragOverlay(speed, multRaw);
                    cd = cd * mult;
                }
            }
            else
            {
                cd = cfg.DragCoefficient;
            }

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

                // Layer 2 corner-case overlay. Smoothstep blend across S in [0.25, 0.35]
                // prevents discontinuity between Layer 1 (Bearman-Harvey valid) and overlay
                // (extrapolation territory). See Docs/Physics/CALIBRATION_METHODOLOGY.md.
                if (cfg.UseLiftOverlay && cfg.LiftOverlay.IsValid)
                {
                    fp multRaw = cfg.LiftOverlay.Evaluate(spinParam);
                    fp mult    = BlendOverlay(spinParam, multRaw);
                    cl = cl * mult;
                }
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

        // Smoothstep-blended overlay multiplier. Returns 1.0 below S=0.25, full multiplier
        // above S=0.35, smoothstep interpolation between. This preserves Layer 1
        // (Bearman-Harvey) as canonical inside its valid range.
        private static fp BlendOverlay(fp spinParam, fp overlayMultiplier)
        {
            fp lo = fp.FromFloat(0.25f);
            fp hi = fp.FromFloat(0.35f);
            if (spinParam <= lo) return fp.One;
            if (spinParam >= hi) return overlayMultiplier;

            // Smoothstep: t² × (3 − 2t)
            fp t       = (spinParam - lo) / (hi - lo);
            fp two     = fp.FromFloat(2f);
            fp three   = fp.FromFloat(3f);
            fp smoothT = (t * t) * (three - (two * t));

            // Linear blend between 1.0 and overlayMultiplier using the smoothed t
            return fp.One + (overlayMultiplier - fp.One) * smoothT;
        }

        // Smoothstep-blended drag overlay multiplier. Returns 1.0 below v=45 m/s,
        // full multiplier above v=55 m/s, smoothstep interpolation between.
        // This preserves Layer 1 (Bearman-Harvey) as canonical inside its valid range.
        private static fp BlendDragOverlay(fp speed, fp overlayMultiplier)
        {
            fp lo = fp.FromFloat(45f);
            fp hi = fp.FromFloat(55f);
            if (speed <= lo) return fp.One;
            if (speed >= hi) return overlayMultiplier;

            // Smoothstep: t² × (3 − 2t)
            fp t       = (speed - lo) / (hi - lo);
            fp two     = fp.FromFloat(2f);
            fp three   = fp.FromFloat(3f);
            fp smoothT = (t * t) * (three - (two * t));

            // Linear blend between 1.0 and overlayMultiplier using the smoothed t
            return fp.One + (overlayMultiplier - fp.One) * smoothT;
        }
    }
}
