using Golfin.Physics.Math;

namespace Golfin.Physics
{
    /// <summary>
    /// Immutable description of the hole cup, handed to <see cref="BallSimulation"/> so the
    /// integrator itself can capture the ball (drop it in) or deflect it off the rim (lip-out).
    ///
    /// Why this exists (cup_capture_and_lipout, 2026-08-05): before this type, the sim was
    /// cup-blind. Cup detection was a POST-HOC scan over the finished trajectory
    /// (BallStateMachine.OnTrajectoryComputed → RealCupDetector), which by contract must not
    /// modify the path. Measured on Hole 6: a putt arriving at the cup at 0.42 m/s — well
    /// inside the 1.5 m/s capture gate — was flagged InCup at sample 580 of 1765 while the
    /// simulation carried on rolling for another 4.78 s past the hole. The player watched the
    /// ball roll over the cup, stop somewhere else, and only then saw the hole-complete modal.
    /// Deciding capture inside the integrator is the only place the ball's path can actually
    /// change. Full measurements: Docs/Physics/CUP_CAPTURE_STEP0_DIAGNOSIS.md.
    ///
    /// Determinism rules (same as the rest of Physics.Core): pure fp math, no Unity API,
    /// no Time/Random. Golfin.Physics.Core has noEngineReferences=true — no Vector3 here.
    /// Callers in Unity assemblies convert Vector3 → fp3 before constructing.
    ///
    /// Gate: <see cref="Enabled"/> == false (i.e. <see cref="Disabled"/>) makes every cup
    /// branch in the integrator dead code, so output is bit-exact with the pre-cup sim.
    /// That equivalence is a blocking test (CupCaptureSimTests.LegacyGate_*).
    /// </summary>
    public readonly struct CupSpec
    {
        /// <summary>False = the sim ignores the cup entirely (legacy bit-exact path).</summary>
        public readonly bool Enabled;

        /// <summary>Authored pin world position. XZ is what the sim tests against; Y is the cup lip.</summary>
        public readonly fp3 Pin;

        /// <summary>
        /// Cup mouth radius. Regulation 4.25 in diameter → 0.054 m.
        /// Real-world constant (USGA/R&amp;A Rules of Golf, Equipment Rules — hole diameter 108 mm).
        /// </summary>
        public readonly fp Radius;

        /// <summary>
        /// Speed gate: at or below this the ball drops; above it, it lips out.
        /// Sourced from PuttConfig.CupCaptureSpeed — do NOT duplicate the constant here.
        /// 1.5 m/s per USGA lip-out anchor (≈5 ft/s); Penner, A.R. (2002) "The physics of
        /// putting." Canadian Journal of Physics 80(2): 83–96 (see lip-out analysis).
        /// </summary>
        public readonly fp CaptureSpeed;

        /// <summary>
        /// Cup depth used to synthesize the fall-in. Regulation minimum depth is 4 in
        /// (0.1016 m) — USGA/R&amp;A Rules of Golf, Equipment Rules. Real-world constant.
        /// </summary>
        public readonly fp Depth;

        /// <summary>
        /// Restitution applied to the RADIAL velocity component on a lip-out (the component
        /// pointing at the cup centre bounces back off the far rim).
        /// DESIGN-FEEL VALUE, ARCHITECT-TUNABLE — not physically calibrated. Per Lesson K:
        /// cite what is real, flag what is tuned. Tunable via putt.csv "lip_restitution".
        /// </summary>
        public readonly fp LipRestitution;

        /// <summary>
        /// Overall speed retained after a lip-out (0.70 = ball loses ~30% of its speed to the rim).
        /// DESIGN-FEEL VALUE, ARCHITECT-TUNABLE. Tunable via putt.csv "lip_speed_damping".
        /// </summary>
        public readonly fp LipSpeedDamping;

        /// <summary>
        /// Fraction of the horizontal speed removed by the rim that is converted into an
        /// upward pop — DIMENSIONLESS, not m/s. Hitting an angled wall turns part of the
        /// horizontal impulse into vertical, so a heavy clip pops and a clean skim does not.
        /// 1.0 means a clip costing 0.6 m/s gives ≈0.6 m/s up, i.e. a ~2 cm hop.
        ///
        /// Rendered as a short ballistic hop offset on the emitted samples — the roll/putt
        /// integrators snap the ball to the surface every step, so a real vy would be
        /// projected away same-step.
        ///
        /// Was an absolute 0.30 m/s scaled by dip, which produced a 0.4 mm hop at speed —
        /// invisible on screen. DESIGN-FEEL VALUE, ARCHITECT-TUNABLE.
        /// Tunable via putt.csv "lip_pop_fraction".
        /// </summary>
        public readonly fp LipPopVy;

        public CupSpec(fp3 pin, fp radius, fp captureSpeed, fp depth,
                       fp lipRestitution, fp lipSpeedDamping, fp lipPopVy)
        {
            Enabled         = true;
            Pin             = pin;
            Radius          = radius;
            CaptureSpeed    = captureSpeed;
            Depth           = depth;
            LipRestitution  = lipRestitution;
            LipSpeedDamping = lipSpeedDamping;
            LipPopVy        = lipPopVy;
        }

        /// <summary>Regulation cup mouth radius: 4.25 in diameter → 0.054 m.</summary>
        public static readonly fp DefaultRadius = fp.FromFloat(0.054f);

        /// <summary>Regulation minimum cup depth: 4 in → 0.1016 m, rounded to 0.10 m.</summary>
        public static readonly fp DefaultDepth = fp.FromFloat(0.10f);

        /// <summary>No cup — the integrator's cup branches are skipped entirely.</summary>
        public static CupSpec Disabled => default;
    }
}
