using Golfin.Physics.Math;

namespace Golfin.Physics
{
    public struct SurfaceCoefficients
    {
        public fp Restitution;       // Cr, 0..1
        public fp TangentFriction;   // μ, 0..1 (0 = frictionless)
        public fp RollingResistance; // 1/s, decel during roll
        public fp StopSpeed;         // m/s, threshold for stop detection
    }

    public struct SurfaceConfig
    {
        // Indexed by (int)SurfaceType. Length = number of SurfaceType values.
        public SurfaceCoefficients[] Coefficients;

        public SurfaceCoefficients this[SurfaceType t] => Coefficients[(int)t];

        public static SurfaceConfig Default
        {
            get
            {
                var c = new SurfaceCoefficients[12]; // must match SurfaceType count
                c[(int)SurfaceType.Fairway]     = new SurfaceCoefficients { Restitution = fp.FromFloat(0.50f), TangentFriction = fp.FromFloat(0.55f), RollingResistance = fp.FromFloat(0.18f), StopSpeed = fp.FromFloat(0.10f) };
                c[(int)SurfaceType.Green]        = new SurfaceCoefficients { Restitution = fp.FromFloat(0.40f), TangentFriction = fp.FromFloat(0.75f), RollingResistance = fp.FromFloat(0.12f), StopSpeed = fp.FromFloat(0.05f) };
                c[(int)SurfaceType.GreenCollar]  = new SurfaceCoefficients { Restitution = fp.FromFloat(0.45f), TangentFriction = fp.FromFloat(0.65f), RollingResistance = fp.FromFloat(0.15f), StopSpeed = fp.FromFloat(0.08f) };
                c[(int)SurfaceType.Semirough]    = new SurfaceCoefficients { Restitution = fp.FromFloat(0.38f), TangentFriction = fp.FromFloat(0.70f), RollingResistance = fp.FromFloat(0.28f), StopSpeed = fp.FromFloat(0.15f) };
                c[(int)SurfaceType.Rough]        = new SurfaceCoefficients { Restitution = fp.FromFloat(0.25f), TangentFriction = fp.FromFloat(0.82f), RollingResistance = fp.FromFloat(0.45f), StopSpeed = fp.FromFloat(0.22f) };
                c[(int)SurfaceType.Tee]          = new SurfaceCoefficients { Restitution = fp.FromFloat(0.55f), TangentFriction = fp.FromFloat(0.45f), RollingResistance = fp.FromFloat(0.15f), StopSpeed = fp.FromFloat(0.10f) };
                c[(int)SurfaceType.Sand]         = new SurfaceCoefficients { Restitution = fp.FromFloat(0.15f), TangentFriction = fp.FromFloat(0.85f), RollingResistance = fp.FromFloat(0.70f), StopSpeed = fp.FromFloat(0.25f) };
                c[(int)SurfaceType.BunkerLip]    = new SurfaceCoefficients { Restitution = fp.FromFloat(0.20f), TangentFriction = fp.FromFloat(0.80f), RollingResistance = fp.FromFloat(0.55f), StopSpeed = fp.FromFloat(0.20f) };
                c[(int)SurfaceType.CartPath]     = new SurfaceCoefficients { Restitution = fp.FromFloat(0.70f), TangentFriction = fp.FromFloat(0.18f), RollingResistance = fp.FromFloat(0.06f), StopSpeed = fp.FromFloat(0.08f) };
                c[(int)SurfaceType.Water]        = new SurfaceCoefficients { Restitution = fp.Zero,            TangentFriction = fp.One,              RollingResistance = fp.One,              StopSpeed = fp.Zero            };
                c[(int)SurfaceType.OOB]          = new SurfaceCoefficients { Restitution = fp.FromFloat(0.20f), TangentFriction = fp.FromFloat(0.80f), RollingResistance = fp.FromFloat(0.50f), StopSpeed = fp.FromFloat(0.20f) };
                // Bridge: timber deck. DESIGN-FEEL STARTING VALUES, ARCHITECT-TUNABLE (bridge_transplant SPEC B2)
                // — sits between CartPath (hard concrete, too lively) and Fairway. Pending Cesar's feel pass.
                c[(int)SurfaceType.Bridge]       = new SurfaceCoefficients { Restitution = fp.FromFloat(0.45f), TangentFriction = fp.FromFloat(0.35f), RollingResistance = fp.FromFloat(0.12f), StopSpeed = fp.FromFloat(0.10f) };
                return new SurfaceConfig { Coefficients = c };
            }
        }
    }
}
