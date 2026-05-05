// Tripwire test added by controls_d_velocity_cap_diagnosis (iteration 2), 2026-05-05.
// Required by ARCHITECT_REVIEW.md FAIL-1 addendum (human Architect override).
//
// This test is [Ignore]-tagged because the lift LUT extrapolates Bearman-Harvey 1976
// data past its valid spin-parameter range (S > 0.30), causing iron/wedge over-prediction.
// The tag will be removed by controls_e_aero_overlay_pass, which adds a Layer-2 coefficient
// overlay to correct carries into Tour-pro range. Definition of done for that spec:
// this test passes.
//
// Tour-pro targets (PGA TOUR 2K23 dev blog / Trackman composite Tour data, 2023 averages):
//   driver:  290 yd ±10%  (range 261–319)
//   iron7:   175 yd ±10%  (range 158–193)
//   iron9:   145 yd ±10%  (range 131–160)
//   pwedge:  115 yd ±10%  (range 104–127)

using System.Collections.Generic;
using NUnit.Framework;
using Golfin.Physics;
using Golfin.Physics.Math;

namespace Golfin.Physics.Tests
{
    /// <summary>
    /// Tripwire test for the Layer-2 aero calibration. Currently <c>[Ignore]</c>-tagged
    /// because the lift LUT extrapolates Bearman-Harvey 1976 data past its valid
    /// spin-parameter range (S &gt; 0.30), causing iron/wedge over-prediction. Will be
    /// enabled by <c>controls_e_aero_overlay_pass</c>. Definition of done for that spec:
    /// this test passes.
    /// </summary>
    public class AeroCalibrationTripwireTests
    {
        // Club data mirrored from AerodynamicsTests.Clubs[] (that field is private, so
        // copied here). Keep in sync with AerodynamicsTests.cs.
        // Format: (id, speedMps, angleDeg, spinRpm, tourProTargetYd)
        private static readonly (string id, float speedMps, float angleDeg, float spinRpm, float tourProTargetYd)[] Clubs =
        {
            // Tour-pro targets from PGA TOUR 2K23 dev blog / Trackman composite Tour data (2023 averages).
            // driver: average Tour ball speed ~167 mph ≈ 75 m/s club-exit; carry ~290 yd.
            // iron7:  52.5 m/s club-exit; carry ~175 yd.
            // iron9:  48.5 m/s club-exit; carry ~145 yd.
            // pwedge: 46.0 m/s club-exit; carry ~115 yd.
            ("club_driver_gf",    75.0f, 10.9f,  2686f, 290f),
            ("club_iron7_mireo",  52.5f, 16.3f,  7097f, 175f),
            ("club_iron9_klyro",  48.5f, 20.0f,  8647f, 145f),
            ("club_pwedge_royal", 46.0f, 24.0f,  9300f, 115f),
        };

        private static ShotInput MakeShot(float speedMps, float angleDeg, float spinRpm = 0f)
        {
            double ar = angleDeg * System.Math.PI / 180.0;
            var vel = new fp3(
                fp.Zero,
                fp.FromDouble(speedMps * System.Math.Sin(ar)),
                fp.FromDouble(speedMps * System.Math.Cos(ar)));
            if (spinRpm <= 0f)
                return new ShotInput(fp3.Zero, vel, fp.FromInt(30));
            float rps = spinRpm * 2f * (float)System.Math.PI / 60f;
            var spin = new SpinState(new fp3(fp.FromInt(-1), fp.Zero, fp.Zero), fp.FromFloat(rps));
            return new ShotInput(fp3.Zero, vel, fp.FromInt(30), spin);
        }

        private static float CarryYards(Trajectory t) => t.finalPosition.z.ToFloat() * 1.09361f;

        // Mirrors aero_drag_lut.csv / aero_lift_lut.csv exactly — update in sync with those files
        // and with AerodynamicsTests.MakeLutConfig().
        private static AeroConfig MakeLutConfig()
        {
            // aero_drag_lut.csv — v3 iter2, post-crisis floor 0.23
            var dragX = new fp[] {
                fp.FromFloat(5f),  fp.FromFloat(10f), fp.FromFloat(15f), fp.FromFloat(18f),
                fp.FromFloat(22f), fp.FromFloat(26f), fp.FromFloat(30f), fp.FromFloat(40f),
                fp.FromFloat(50f), fp.FromFloat(60f), fp.FromFloat(70f), fp.FromFloat(80f),
                fp.FromFloat(100f) };
            var dragY = new fp[] {
                fp.FromFloat(0.50f), fp.FromFloat(0.48f), fp.FromFloat(0.45f), fp.FromFloat(0.40f),
                fp.FromFloat(0.28f), fp.FromFloat(0.24f), fp.FromFloat(0.23f), fp.FromFloat(0.23f),
                fp.FromFloat(0.23f), fp.FromFloat(0.23f), fp.FromFloat(0.23f), fp.FromFloat(0.23f),
                fp.FromFloat(0.23f) };

            // aero_lift_lut.csv — v3 iter2, Bearman-Harvey +0.01 nudge
            var liftX = new fp[] {
                fp.FromFloat(0.00f), fp.FromFloat(0.02f), fp.FromFloat(0.05f), fp.FromFloat(0.08f),
                fp.FromFloat(0.10f), fp.FromFloat(0.12f), fp.FromFloat(0.15f), fp.FromFloat(0.20f),
                fp.FromFloat(0.25f), fp.FromFloat(0.30f), fp.FromFloat(0.40f), fp.FromFloat(0.50f),
                fp.FromFloat(0.60f) };
            var liftY = new fp[] {
                fp.FromFloat(0.000f), fp.FromFloat(0.034f), fp.FromFloat(0.066f), fp.FromFloat(0.093f),
                fp.FromFloat(0.110f), fp.FromFloat(0.125f), fp.FromFloat(0.146f), fp.FromFloat(0.177f),
                fp.FromFloat(0.202f), fp.FromFloat(0.224f), fp.FromFloat(0.260f), fp.FromFloat(0.288f),
                fp.FromFloat(0.300f) };

            var cfg = AeroConfig.Default;
            cfg.DragLut    = new CoefficientLut(dragX, dragY);
            cfg.LiftLut    = new CoefficientLut(liftX, liftY);
            cfg.UseDragLut = true;
            cfg.UseLiftLut = true;
            return cfg;
        }

        /// <summary>
        /// Tripwire test for the Layer-2 aero calibration. Currently <c>[Ignore]</c>-tagged
        /// because the lift LUT extrapolates Bearman-Harvey 1976 data past its valid
        /// spin-parameter range (S &gt; 0.30), causing iron/wedge over-prediction. Will be
        /// enabled by <c>controls_e_aero_overlay_pass</c>. Definition of done for that spec:
        /// this test passes.
        /// </summary>
        [Test]
        [Ignore("Awaiting controls_e_aero_overlay_pass calibration. See ESCALATION_TO_ARCHITECT.md.")]
        public void Aero_AllClubs_WithinTourCarryRange_PerSpinRegime()
        {
            var cfg    = MakeLutConfig();
            var ground = new FlatGround(fp.Zero);
            var results = new List<string>();
            bool anyFailed = false;
            const float tolerancePct = 10f;

            foreach (var (id, speedMps, angleDeg, spinRpm, tourProTargetYd) in Clubs)
            {
                var input    = MakeShot(speedMps, angleDeg, spinRpm);
                float actual = CarryYards(BallSimulation.Simulate(input, ground, cfg));
                float errPct = System.Math.Abs(actual - tourProTargetYd) / tourProTargetYd * 100f;
                bool  ok     = errPct <= tolerancePct;

                results.Add($"  {id,-20} target={tourProTargetYd:F0}yd  actual={actual:F0}yd  err={errPct:F1}%  {(ok ? "OK" : "FAIL")}");
                if (!ok) anyFailed = true;
            }

            string table = "\n" + string.Join("\n", results);
            UnityEngine.Debug.Log("[AeroCalibrationTripwireTests] Tour-carry tripwire:" + table);

            Assert.IsFalse(anyFailed,
                $"One or more clubs exceed ±{tolerancePct:F0}% of Tour-pro carry targets (LUT mode). " +
                "This test gates controls_e_aero_overlay_pass completion. " +
                "Do NOT remove [Ignore] until the overlay calibration lands." + table);
        }
    }
}
