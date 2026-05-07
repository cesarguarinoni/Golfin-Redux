using Golfin.Physics.Math; // COMPILE_TRIGGER_v2

namespace Golfin.Physics
{
    /// <summary>
    /// Aerodynamic constants loaded from Resources/Physics/aero.csv.
    /// Pure data struct — no Unity references. Loading is handled in PhysicsConfigLoader (Runtime).
    /// </summary>
    public struct AeroConfig
    {
        public fp AirDensity;           // kg/m³, default 1.225
        public fp BallMass;             // kg, default 0.04593
        public fp BallCrossSection;     // m², default 0.001432
        public fp DragCoefficient;      // dimensionless, constant-mode Cd fallback
        public fp LiftCoefficientBase;  // dimensionless, constant-mode Cl fallback
        public fp SpinRateReference;    // rad/s, constant-mode only
        public fp LiftMaxMultiplier;    // constant-mode only

        // Phase 2.1: LUT support
        public fp BallRadius;           // m, default 0.02135 — for spin parameter S = r·ω/|v|
        public CoefficientLut DragLut;  // Cd(speed). When IsValid=false, falls back to DragCoefficient.
        public CoefficientLut LiftLut;  // Cl(S).     When IsValid=false, falls back to linear-capped Cl.
        public bool UseDragLut;
        public bool UseLiftLut;
        public fp SpinDecayRate;    // 1/s, exponential spin decay; 0=no decay

        // Layer 2 — corner-case overlay (controls_e_aero_overlay_pass).
        // Cl multiplier(S). When IsValid=false OR UseLiftOverlay=false, no-op (multiplier=1.0).
        public CoefficientLut LiftOverlay;
        public bool UseLiftOverlay;

        // Layer 2 drag — corner-case overlay (controls_f_drag_calibration_audit).
        // Cd multiplier(speed). When IsValid=false OR UseDragOverlay=false, no-op (multiplier=1.0).
        public CoefficientLut DragOverlay;
        public bool UseDragOverlay;

        public static AeroConfig Default => new AeroConfig
        {
            AirDensity          = fp.FromFloat(1.225f),
            BallMass            = fp.FromFloat(0.04593f),
            BallCrossSection    = fp.FromFloat(0.001432f),
            DragCoefficient     = fp.FromFloat(0.25f),
            LiftCoefficientBase = fp.FromFloat(0.20f),
            SpinRateReference   = fp.FromFloat(300f),
            LiftMaxMultiplier   = fp.FromFloat(1.5f),
            BallRadius          = fp.FromFloat(0.02135f),
            // DragLut / LiftLut / LiftOverlay / DragOverlay default-constructed (IsValid=false) — constant-mode fallback
            UseDragLut          = false,
            UseLiftLut          = false,
            SpinDecayRate       = fp.FromFloat(0.02f),
            UseLiftOverlay      = false,
            // LiftOverlay default-constructed (IsValid=false) — overlay is opt-in via CSV
            UseDragOverlay      = false,
            // DragOverlay default-constructed (IsValid=false) — overlay is opt-in via CSV
        };

        /// <summary>
        /// Throws InvalidOperationException if any field has a value that would cause
        /// AeroModel.ComputeAeroForce to divide by zero. Call after LoadAeroConfig returns,
        /// or in tests after constructing a custom config.
        /// </summary>
        public void AssertValid()
        {
            if (SpinRateReference <= fp.Zero)
                throw new System.InvalidOperationException(
                    $"AeroConfig.SpinRateReference must be > 0 (got {SpinRateReference.ToFloat()}). " +
                    $"Constant-mode lift branch divides by this. Check Resources/Physics/aero.csv 'spin_rate_reference' row.");

            if (BallMass <= fp.Zero)
                throw new System.InvalidOperationException(
                    $"AeroConfig.BallMass must be > 0 (got {BallMass.ToFloat()}). " +
                    $"BallSimulation divides by this. Check Resources/Physics/aero.csv 'ball_mass' row.");
        }

        // Vacuum variant — Cd=0, Cl=0. Degenerates to gravity-only integration.
        // Used by the no-aero Simulate overload so Phase 1 tests remain valid.
        public static AeroConfig Vacuum => new AeroConfig
        {
            AirDensity          = fp.FromFloat(1.225f),
            BallMass            = fp.FromFloat(0.04593f),
            BallCrossSection    = fp.FromFloat(0.001432f),
            DragCoefficient     = fp.Zero,
            LiftCoefficientBase = fp.Zero,
            SpinRateReference   = fp.FromFloat(300f),
            LiftMaxMultiplier   = fp.FromFloat(1.5f),
            BallRadius          = fp.FromFloat(0.02135f),
            UseDragLut          = false,
            UseLiftLut          = false,
            SpinDecayRate       = fp.Zero,
            UseLiftOverlay      = false,
            // LiftOverlay default-constructed (IsValid=false)
            UseDragOverlay      = false,
            // DragOverlay default-constructed (IsValid=false)
        };
    }
}
