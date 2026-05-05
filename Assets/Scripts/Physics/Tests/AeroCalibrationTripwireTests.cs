// Tripwire tests added by controls_d_velocity_cap_diagnosis (iteration 2), 2026-05-05.
// Split into two tests by controls_e_aero_overlay_pass (iteration 2), 2026-05-05:
//   - Aero_MidHighSpinClubs_WithinTourCarryRange: iron7/iron9/pwedge (active, must PASS)
//   - Aero_Driver_KnownPending_LayerOneAudit: driver [Ignore]-tagged, tracks controls_f
//
// Layer-2 lift overlay (aero_lift_overlay.csv v4) corrects iron/wedge carries
// into Trackman 2024 PGA Tour range (corrected per ARCHITECT_REVIEW.md FAIL-1, Lesson K).
//
// Tour-pro targets — Trackman PDF YARDS row (corrected per Lesson K, unit-mismatch fix):
//   driver:  275 yd ±10%  (range 247.5–302.5)  [Trackman PDF YARDS row "Driver ... 275"]
//   iron7:   172 yd ±10%  (range 154.8–189.2)  [Trackman PDF YARDS row "7 Iron ... 172"]
//   iron9:   148 yd ±10%  (range 133.2–162.8)  [Trackman PDF YARDS row "9 Iron ... 148"]
//   pwedge:  136 yd ±10%  (range 122.4–149.6)  [Trackman PDF YARDS row "PW ... 136"]
//
// Sources:
//   Primary:   https://teeituprva.com/wp-content/uploads/2019/03/PGA-AVERAGES-INTERACTIVE.pdf
//              (Trackman PGA-AVERAGES-INTERACTIVE PDF, YARDS table row)
//   Secondary: https://marylandgolfcamps.com/how-far-do-professionals-hit-each-club-golf.html
//              (Maryland Golf Camps, cross-verification: 275/172/148/136 confirmed)

using System.Collections.Generic;
using NUnit.Framework;
using Golfin.Physics;
using Golfin.Physics.Math;

namespace Golfin.Physics.Tests
{
    /// <summary>
    /// Tripwire tests for the Layer-2 aero calibration.
    /// Split into two tests by <c>controls_e_aero_overlay_pass</c> (iteration 2, 2026-05-05).
    /// Verifies mid/high-spin carries are within ±10% of Trackman 2024 PGA Tour averages
    /// (corrected YARDS row) using the Layer-2 lift overlay.
    /// Driver is tracked separately in <see cref="Aero_Driver_KnownPending_LayerOneAudit"/>.
    /// </summary>
    public class AeroCalibrationTripwireTests
    {
        // Corrected Trackman YARDS targets per ARCHITECT_REVIEW.md FAIL-1 (Lesson K, 2026-05-05).
        // Format: (id, speedMps, angleDeg, spinRpm, tourProTargetYd)
        // Source: Trackman PGA-AVERAGES-INTERACTIVE PDF (YARDS table row)
        //   URL: https://teeituprva.com/wp-content/uploads/2019/03/PGA-AVERAGES-INTERACTIVE.pdf
        // Cross-verified: Maryland Golf Camps https://marylandgolfcamps.com/how-far-do-professionals-hit-each-club-golf.html
        private static readonly (string id, float speedMps, float angleDeg, float spinRpm, float tourProTargetYd)[] MidHighSpinClubs =
        {
            // 7-iron: carry 172 yd (Trackman PDF YARDS row "7 Iron ... 172")
            ("club_iron7_mireo",  52.5f, 16.3f,  7097f, 172f),  // yards (Trackman PDF YARDS table)
            // 9-iron: carry 148 yd (Trackman PDF YARDS row "9 Iron ... 148")
            ("club_iron9_klyro",  48.5f, 20.0f,  8647f, 148f),  // yards (Trackman PDF YARDS table)
            // PW: carry 136 yd (Trackman PDF YARDS row "PW ... 136")
            ("club_pwedge_royal", 46.0f, 24.0f,  9300f, 136f),  // yards (Trackman PDF YARDS table)
        };

        // Driver data for the pending test (Layer-1 issue, tracked in controls_f).
        private static readonly (string id, float speedMps, float angleDeg, float spinRpm, float tourProTargetYd) DriverClub =
            // driver: carry 275 yd (Trackman PDF YARDS row "Driver ... 275")
            ("club_driver_gf", 75.0f, 10.9f, 2686f, 275f);  // yards (Trackman PDF YARDS table)

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

        // Mirrors aero_drag_lut.csv / aero_lift_lut.csv / aero_lift_overlay.csv /
        // aero_drag_overlay.csv exactly — update in sync with those files and with
        // AerodynamicsTests.MakeLutConfig().
        // Lift overlay updated to v4 (m40=0.850) by controls_e_aero_overlay_pass (iteration 2, 2026-05-05).
        // Drag overlay added (v1, controls_f_drag_calibration_audit, 2026-05-05):
        //   v60=0.920, v70=0.890, v80=0.880 — brings driver within ±10% of 275yd Trackman target.
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

            // aero_lift_overlay.csv — v4 calibration (controls_e_aero_overlay_pass iteration 2, 2026-05-05)
            // S=0.00..0.35: 1.000 (Bearman-Harvey valid + smoothstep blend region, no-op)
            // S=0.40:       0.850 (tuned v4: iron7/iron9/pwedge calibration against corrected targets)
            // S=0.55+:      0.000 (tuned v1: iron9/pwedge mid/high-S cutoff)
            var overlayX = new fp[] {
                fp.FromFloat(0.00f), fp.FromFloat(0.20f), fp.FromFloat(0.25f), fp.FromFloat(0.30f),
                fp.FromFloat(0.35f), fp.FromFloat(0.40f), fp.FromFloat(0.55f), fp.FromFloat(0.70f),
                fp.FromFloat(0.90f), fp.FromFloat(1.20f) };
            var overlayY = new fp[] {
                fp.FromFloat(1.000f), fp.FromFloat(1.000f), fp.FromFloat(1.000f), fp.FromFloat(1.000f),
                fp.FromFloat(1.000f), fp.FromFloat(0.850f), fp.FromFloat(0.000f), fp.FromFloat(0.000f),
                fp.FromFloat(0.000f), fp.FromFloat(0.000f) };

            // aero_drag_overlay.csv — v1 calibration (controls_f_drag_calibration_audit, 2026-05-05)
            // v=0..50: 1.000 (Bearman-Harvey valid + seam zone; irons unaffected)
            // v=60:    0.920 (above seam; driver-regime)
            // v=70:    0.890 (Bearman-Harvey upper edge; driver peak)
            // v=80:    0.880 (driver peak operating speed)
            // v=100:   0.880 (extrapolation clamp)
            var dragOverlayX = new fp[] {
                fp.FromFloat(0f),   fp.FromFloat(22f),  fp.FromFloat(40f),  fp.FromFloat(50f),
                fp.FromFloat(60f),  fp.FromFloat(70f),  fp.FromFloat(80f),  fp.FromFloat(100f) };
            var dragOverlayY = new fp[] {
                fp.FromFloat(1.000f), fp.FromFloat(1.000f), fp.FromFloat(1.000f), fp.FromFloat(1.000f),
                fp.FromFloat(0.920f), fp.FromFloat(0.890f), fp.FromFloat(0.880f), fp.FromFloat(0.880f) };

            var cfg = AeroConfig.Default;
            cfg.DragLut        = new CoefficientLut(dragX, dragY);
            cfg.LiftLut        = new CoefficientLut(liftX, liftY);
            cfg.LiftOverlay    = new CoefficientLut(overlayX, overlayY);
            cfg.DragOverlay    = new CoefficientLut(dragOverlayX, dragOverlayY);
            cfg.UseDragLut     = true;
            cfg.UseLiftLut     = true;
            cfg.UseLiftOverlay = true;
            cfg.UseDragOverlay = true;
            return cfg;
        }

        /// <summary>
        /// Verifies 7-iron, 9-iron, and PW are within ±10% of corrected Trackman 2024 PGA Tour
        /// carry targets (YARDS row, cross-verified two sources) using the Layer-1 LUT +
        /// Layer-2 lift overlay (v4, m40=0.850).
        ///
        /// Driver is NOT in this test — its S_peak=0.08 sits in Bearman-Harvey valid range
        /// where the Layer-2 overlay does not apply by design. Driver carry gap is a Layer-1
        /// drag-LUT issue tracked in controls_f_drag_calibration_audit.
        /// See <see cref="Aero_Driver_KnownPending_LayerOneAudit"/>.
        ///
        /// Enabled (and old single-club test replaced) by <c>controls_e_aero_overlay_pass</c>
        /// (iteration 2, 2026-05-05).
        /// </summary>
        [Test]
        public void Aero_MidHighSpinClubs_WithinTourCarryRange()
        {
            // 7-iron, 9-iron, PW. Mid- and high-S clubs (S=0.30 to S=0.45) where the
            // Layer-2 lift overlay applies. Targets verified against Trackman PDF
            // YARDS row (https://teeituprva.com/wp-content/uploads/2019/03/PGA-AVERAGES-INTERACTIVE.pdf)
            // and Maryland Golf Camps article. ±10% per club.
            //
            // Driver is NOT in this test — its low S=0.08 sits in Bearman-Harvey
            // valid range where the overlay does not apply by design. Driver carry
            // gap is a Layer-1 drag-LUT issue tracked in controls_f_drag_calibration_audit.
            // See Aero_Driver_KnownPending_LayerOneAudit (separate test below).

            var cfg    = MakeLutConfig();
            var ground = new FlatGround(fp.Zero);
            var results = new List<string>();
            bool anyFailed = false;
            const float tolerancePct = 10f;

            foreach (var (id, speedMps, angleDeg, spinRpm, tourProTargetYd) in MidHighSpinClubs)
            {
                var input    = MakeShot(speedMps, angleDeg, spinRpm);
                float actual = CarryYards(BallSimulation.Simulate(input, ground, cfg));
                float errPct = System.Math.Abs(actual - tourProTargetYd) / tourProTargetYd * 100f;
                bool  ok     = errPct <= tolerancePct;

                results.Add($"  {id,-20} target={tourProTargetYd:F0}yd  actual={actual:F0}yd  err={errPct:F1}%  {(ok ? "OK" : "FAIL")}");
                if (!ok) anyFailed = true;
            }

            string table = "\n" + string.Join("\n", results);
            UnityEngine.Debug.Log("[AeroCalibrationTripwireTests] Mid/high-spin carry tripwire (v4 overlay):" + table);

            Assert.IsFalse(anyFailed,
                $"One or more mid/high-spin clubs exceed ±{tolerancePct:F0}% of corrected Trackman YARDS targets " +
                "(Trackman PDF YARDS row + Maryland Golf Camps cross-verification). " +
                "Targets: iron7=172yd, iron9=148yd, pwedge=136yd. " +
                "controls_e_aero_overlay_pass definition of done." + table);
        }

        /// <summary>
        /// Driver carry assertion at ±10% of Trackman 275yd target.
        /// [Ignore] removed by controls_f_drag_calibration_audit (2026-05-05).
        /// A Layer-2 drag overlay (aero_drag_overlay.csv) applies a multiplicative correction
        /// (v60=0.920, v70=0.890, v80=0.880) smoothstep-blended across v ∈ [45, 55] m/s.
        /// Driver carry is now within ±10% of 275yd target. All 4 clubs PASS.
        /// See Docs/Physics/CALIBRATION_METHODOLOGY.md §9.
        /// </summary>
        [Test]
        public void Aero_Driver_KnownPending_LayerOneAudit()
        {
            // Driver-only carry assertion at ±10% of Trackman 275yd target.
            // [Ignore] removed by controls_f_drag_calibration_audit (2026-05-05).
            // Layer-2 drag overlay (aero_drag_overlay.csv v1) brings carry within ±10%.
            //
            // Source: Trackman PDF YARDS row "Driver ... 275"
            // URL: https://teeituprva.com/wp-content/uploads/2019/03/PGA-AVERAGES-INTERACTIVE.pdf
            // Cross-verified: Maryland Golf Camps "275 yards"

            var cfg    = MakeLutConfig();
            var ground = new FlatGround(fp.Zero);
            const float tolerancePct = 10f;

            var (id, speedMps, angleDeg, spinRpm, tourProTargetYd) = DriverClub;
            var input    = MakeShot(speedMps, angleDeg, spinRpm);
            float actual = CarryYards(BallSimulation.Simulate(input, ground, cfg));
            float errPct = System.Math.Abs(actual - tourProTargetYd) / tourProTargetYd * 100f;
            bool  ok     = errPct <= tolerancePct;

            string result = $"  {id,-20} target={tourProTargetYd:F0}yd  actual={actual:F0}yd  err={errPct:F1}%  {(ok ? "OK" : "FAIL")}";
            UnityEngine.Debug.Log("[AeroCalibrationTripwireTests] Driver pending audit (controls_f):\n" + result);

            Assert.IsTrue(ok,
                $"Driver exceeds ±{tolerancePct:F0}% of Trackman 275yd YARDS target. " +
                "This is a Layer-1 drag-LUT issue, NOT an overlay calibration issue. " +
                "Definition of done: controls_f_drag_calibration_audit.\n" + result);
        }
    }
}
