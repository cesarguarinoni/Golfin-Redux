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

        /// <summary>
        /// Speed gate for cup capture. If the ball's speed (magnitude of velocity vector)
        /// at the moment of cup-volume entry exceeds this threshold, the capture is rejected
        /// and the ball continues on its existing trajectory (fly-over / lip-out behaviour).
        ///
        /// Source: USGA lip-out guidance — a putt travelling faster than ~5 ft/s (≈1.524 m/s)
        /// at the cup rim has sufficient momentum to lip-out rather than drop.
        /// Architect-locked 2026-05-14 at 1.5 m/s as the design anchor value.
        /// Per Lesson K: real-world citation required for all calibrated constants.
        /// Reference: USGA "The Physics of Putting" + Penner, A.R. (2002) "The physics of putting."
        ///            Canadian Journal of Physics 80(2): 83–96 (see lip-out analysis) — lip-out
        ///            condition at rim speed ≈5 ft/s.
        ///
        /// Exposed here for data-driven tuning via putt.csv and DashboardUI / GreenTuningPanel.
        /// </summary>
        public fp CupCaptureSpeed;

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
                // CupCaptureSpeed: 1.5 m/s — USGA lip-out anchor (≈5 ft/s).
                // See Penner, A.R. (2002) "The physics of putting." Canadian Journal of Physics 80(2): 83–96 (see lip-out analysis).
                // Architect-locked 2026-05-14. Tunable via putt.csv "cup_capture_speed".
                return new PuttConfig
                {
                    Coefficients     = c,
                    CupCaptureSpeed  = fp.FromFloat(1.5f),
                };
            }
        }
    }
}
