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
        public fp DragCoefficient;      // dimensionless, default 0.25
        public fp LiftCoefficientBase;  // dimensionless, default 0.20
        public fp SpinRateReference;    // rad/s, default 300 (~2865 rpm driver baseline)
        public fp LiftMaxMultiplier;    // default 1.5

        public static AeroConfig Default => new AeroConfig
        {
            AirDensity          = fp.FromFloat(1.225f),
            BallMass            = fp.FromFloat(0.04593f),
            BallCrossSection    = fp.FromFloat(0.001432f),
            DragCoefficient     = fp.FromFloat(0.25f),
            LiftCoefficientBase = fp.FromFloat(0.20f),
            SpinRateReference   = fp.FromFloat(300f),
            LiftMaxMultiplier   = fp.FromFloat(1.5f),
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
        };
    }
}
