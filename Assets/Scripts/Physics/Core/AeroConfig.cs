using Golfin.Physics.Math;

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
        public fp SpinDecayRate;        // 1/s — exponential spin decay. 0 = no decay (default, backward-compatible).
        public fp SpinDragFactor;       // dimensionless — adds Cd_spin × S² to drag (induced drag). 0 = off (default).

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
            // DragLut / LiftLut default-constructed (IsValid=false) — constant-mode fallback
            UseDragLut          = false,
            UseLiftLut          = false,
        };

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
        };
    }
}
