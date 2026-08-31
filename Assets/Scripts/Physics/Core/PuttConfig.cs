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

        // ── cup_capture_and_lipout (2026-08-05) ───────────────────────────────────────
        // Consumed by PhysicsLabController when it builds the CupSpec handed to the sim.
        // See CupSpec for the full per-field rationale.

        /// <summary>
        /// Cup depth used to synthesize the fall-in animation.
        /// REAL-WORLD CONSTANT: regulation minimum depth 4 in (0.1016 m) — USGA/R&amp;A Rules
        /// of Golf, Equipment Rules. Tunable via putt.csv "cup_depth_m".
        /// </summary>
        public fp CupDepth;

        /// <summary>
        /// Restitution of the radial velocity component on a lip-out.
        /// DESIGN-FEEL VALUE, ARCHITECT-TUNABLE — not physically calibrated (Lesson K).
        /// Tunable via putt.csv "lip_restitution".
        /// </summary>
        public fp LipRestitution;

        /// <summary>
        /// Fraction of speed retained after a lip-out (0.70 = ~30% lost to the rim).
        /// DESIGN-FEEL VALUE, ARCHITECT-TUNABLE. Tunable via putt.csv "lip_speed_damping".
        /// </summary>
        public fp LipSpeedDamping;

        /// <summary>
        /// Fraction of the horizontal speed the rim removes that becomes an upward pop
        /// (dimensionless — see CupSpec.LipPopVy). 1.0 ≈ a 2 cm hop on a clip costing 0.6 m/s.
        /// DESIGN-FEEL VALUE, ARCHITECT-TUNABLE. Tunable via putt.csv "lip_pop_fraction".
        /// </summary>
        public fp LipPopVy;

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
                // Bridge: timber deck (bridge_transplant SPEC B3). The loop above already
                // seeds every index with the conservative default, so this row is not
                // rescuing a zero — it just makes a putt across a deck roll like the smooth
                // surface it is (RollingResistance matches SurfaceConfig's Bridge row).
                // DESIGN-FEEL, ARCHITECT-TUNABLE — pending Cesar's feel pass.
                c[(int)SurfaceType.Bridge] = new SurfaceCoefficients
                {
                    Restitution       = fp.Zero,
                    TangentFriction   = fp.One,
                    RollingResistance = fp.FromFloat(0.12f),
                    StopSpeed         = fp.FromFloat(0.05f),
                };
                // CupCaptureSpeed: 1.5 m/s — USGA lip-out anchor (≈5 ft/s).
                // See Penner, A.R. (2002) "The physics of putting." Canadian Journal of Physics 80(2): 83–96 (see lip-out analysis).
                // Architect-locked 2026-05-14. Tunable via putt.csv "cup_capture_speed".
                // CupDepth 0.10 m: regulation minimum cup depth 4 in (0.1016 m), USGA/R&A
                // Rules of Golf, Equipment Rules — real-world constant.
                // Lip* values: DESIGN-FEEL, architect-tunable; initial values pending lab
                // verification (SPEC_CUP_CAPTURE_AND_LIPOUT §4.6 / §7). Not calibrated.
                return new PuttConfig
                {
                    Coefficients     = c,
                    CupCaptureSpeed  = fp.FromFloat(1.5f),
                    CupDepth         = fp.FromFloat(0.10f),
                    LipRestitution   = fp.FromFloat(0.35f),
                    LipSpeedDamping  = fp.FromFloat(0.70f),
                    LipPopVy         = fp.FromFloat(1.0f),
                };
            }
        }
    }
}
