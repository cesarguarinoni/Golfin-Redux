using Golfin.Physics.Math;

namespace Golfin.Physics
{
    /// <summary>
    /// Putt-tuned coefficients. Indexed by SurfaceType, but only Green and GreenCollar
    /// are read by RunPuttPhase — other entries exist for hot-reload completeness.
    /// </summary>
    public struct PuttConfig
    {
        public SurfaceCoefficients[] Coefficients;
        public SurfaceCoefficients this[SurfaceType t] => Coefficients[(int)t];

        public static PuttConfig Default
        {
            get
            {
                int n = System.Enum.GetValues(typeof(SurfaceType)).Length;
                var c = new SurfaceCoefficients[n];

                // Non-putt surfaces get conservative defaults (Restitution=0, TangentFriction=1).
                for (int i = 0; i < n; i++)
                    c[i] = new SurfaceCoefficients
                    {
                        Restitution       = fp.Zero,
                        TangentFriction   = fp.One,
                        RollingResistance = fp.FromFloat(0.20f),
                        StopSpeed         = fp.FromFloat(0.05f),
                    };

                // Green: ~Stimp 10 feel.
                c[(int)SurfaceType.Green] = new SurfaceCoefficients
                {
                    Restitution       = fp.Zero,
                    TangentFriction   = fp.One,
                    RollingResistance = fp.FromFloat(0.10f),
                    StopSpeed         = fp.FromFloat(0.04f),
                };
                // GreenCollar: slightly slower than green.
                c[(int)SurfaceType.GreenCollar] = new SurfaceCoefficients
                {
                    Restitution       = fp.Zero,
                    TangentFriction   = fp.One,
                    RollingResistance = fp.FromFloat(0.14f),
                    StopSpeed         = fp.FromFloat(0.05f),
                };
                return new PuttConfig { Coefficients = c };
            }
        }
    }
}
