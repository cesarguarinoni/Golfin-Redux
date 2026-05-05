// AeroCalibrationHarness — Layer-2 aero calibration sweep runner.
// Created by controls_e_aero_overlay_pass (2026-05-05).
// Updated with corrected targets (iteration 2, 2026-05-05) per ARCHITECT_REVIEW.md FAIL-1 (Lesson K).
//
// Usage:
//   Menu: GOLFIN > Physics > Run Aero Calibration Sweep
//   Code: string report = AeroCalibrationHarness.RunCalibrationSweep();
//
// Trackman source (corrected per Lesson K — unit-mismatch fix):
//   Primary:   Trackman PGA-AVERAGES-INTERACTIVE PDF, YARDS table row
//              URL: https://teeituprva.com/wp-content/uploads/2019/03/PGA-AVERAGES-INTERACTIVE.pdf
//   Secondary: Maryland Golf Camps (cross-verification: 275/172/148/136 confirmed)
//              URL: https://marylandgolfcamps.com/how-far-do-professionals-hit-each-club-golf.html
//
// Tour-pro targets used: 4 clubs from tripwire test (AeroCalibrationTripwireTests.cs).
// These are a subset of the 8-club spec set, constrained to what the tripwire gates.

using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEngine;
using Golfin.Physics;
using Golfin.Physics.Math;
using Golfin.Physics.Runtime;

namespace Golfin.Physics.Editor
{
    public static class AeroCalibrationHarness
    {
        // Calibration set — mirrors AeroCalibrationTripwireTests.Clubs[].
        // Format: (id, speedMps, launchDeg, spinRpm, tourProTargetYd)
        // Source: Trackman composite Tour averages (2024 PGA Tour season, March 2025 update).
        // URL: https://blog.trackmangolf.com/trackman-average-tour-stats/
        // Corrected per ARCHITECT_REVIEW.md FAIL-1 (Lesson K: unit-mismatch fix, 2026-05-05).
        // Targets are Trackman PDF YARDS row values, cross-verified against Maryland Golf Camps.
        private static readonly (string id, float speedMps, float launchDeg, float spinRpm, float targetYd)[] CalibrationClubs =
        {
            // driver: carry 275 yd (Trackman PDF YARDS row "Driver ... 275")     yards (Trackman PDF YARDS table)
            ("club_driver_gf",    75.0f, 10.9f,  2686f, 275f),
            // 7-iron: carry 172 yd (Trackman PDF YARDS row "7 Iron ... 172")     yards (Trackman PDF YARDS table)
            ("club_iron7_mireo",  52.5f, 16.3f,  7097f, 172f),
            // 9-iron: carry 148 yd (Trackman PDF YARDS row "9 Iron ... 148")     yards (Trackman PDF YARDS table)
            ("club_iron9_klyro",  48.5f, 20.0f,  8647f, 148f),
            // PW: carry 136 yd (Trackman PDF YARDS row "PW ... 136")             yards (Trackman PDF YARDS table)
            ("club_pwedge_royal", 46.0f, 24.0f,  9300f, 136f),
        };

        private const float TolerancePct = 10f;

        [MenuItem("GOLFIN/Physics/Run Aero Calibration Sweep")]
        public static void RunFromMenu()
        {
            string report = RunCalibrationSweep();
            Debug.Log(report);
        }

        /// <summary>
        /// Public entry point usable from CLI / pipeline / EditMode tests.
        /// Returns a multi-line report string suitable for pasting into IMPLEMENTER_REPORT.md.
        /// Uses LUT values hardcoded to match the tripwire test, plus overlay from Resources.
        /// </summary>
        public static string RunCalibrationSweep()
        {
            // Build AeroConfig with hardcoded LUT values (mirrors MakeLutConfig in AeroCalibrationTripwireTests)
            // plus overlay loaded from Resources.
            var cfg = MakeConfigWithOverlay();

            var ground  = new FlatGround(fp.Zero);
            var results = new List<string>();
            int passCount  = 0;

            float worstErrPct  = 0f;
            string worstClub   = "";

            foreach (var (id, speedMps, launchDeg, spinRpm, targetYd) in CalibrationClubs)
            {
                var input  = MakeShot(speedMps, launchDeg, spinRpm);
                var traj   = BallSimulation.Simulate(input, ground, cfg);
                float actual  = CarryYards(traj);
                float errPct  = System.Math.Abs(actual - targetYd) / targetYd * 100f;
                bool  ok      = errPct <= TolerancePct;

                // Compute spin parameter for diagnostics
                float rps      = spinRpm * 2f * (float)System.Math.PI / 60f;
                float spinParam = 0.02135f * rps / speedMps;

                results.Add(string.Format(CultureInfo.InvariantCulture,
                    "  {0,-22} target={1,5:F0}yd  actual={2,5:F0}yd  err={3,+6.1f}%  S={4:F3}  {5}",
                    id, targetYd, actual, actual - targetYd > 0 ? errPct : -errPct, spinParam, ok ? "PASS" : "FAIL"));

                if (ok) passCount++;

                if (errPct > worstErrPct) { worstErrPct = errPct; worstClub = id; }
            }

            int total = CalibrationClubs.Length;
            string header = string.Format(CultureInfo.InvariantCulture,
                "[AeroCalibrationHarness] Sweep — Trackman 2024 PGA Tour averages (PDF YARDS row, corrected per Lesson K)\n" +
                "  Overlay active: {0}, IsValid: {1}\n" +
                "  Tolerance: ±{2:F0}%\n",
                cfg.UseLiftOverlay, cfg.LiftOverlay.IsValid, TolerancePct);

            string summary = string.Format(CultureInfo.InvariantCulture,
                "  Summary: {0}/{1} clubs PASS  worst={2} ({3:F1}%)",
                passCount, total, worstClub, worstErrPct);

            return header + string.Join("\n", results) + "\n" + summary;
        }

        // Build config with hardcoded LUT (tripwire parity) + overlay from Resources CSV.
        private static AeroConfig MakeConfigWithOverlay()
        {
            // Drag LUT — aero_drag_lut.csv v3 iter2
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

            // Lift LUT — aero_lift_lut.csv v3 iter2
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

            var cfg         = AeroConfig.Default;
            cfg.DragLut     = new CoefficientLut(dragX, dragY);
            cfg.LiftLut     = new CoefficientLut(liftX, liftY);
            cfg.UseDragLut  = true;
            cfg.UseLiftLut  = true;

            // Load overlay from Resources CSV so callers see the current-file state.
            cfg.LiftOverlay    = PhysicsConfigLoader.LoadLiftOverlay();
            cfg.UseLiftOverlay = cfg.LiftOverlay.IsValid;

            return cfg;
        }

        private static ShotInput MakeShot(float speedMps, float angleDeg, float spinRpm)
        {
            double ar = angleDeg * System.Math.PI / 180.0;
            var vel = new fp3(
                fp.Zero,
                fp.FromDouble(speedMps * System.Math.Sin(ar)),
                fp.FromDouble(speedMps * System.Math.Cos(ar)));
            float rps  = spinRpm * 2f * (float)System.Math.PI / 60f;
            var spin   = new SpinState(new fp3(fp.FromInt(-1), fp.Zero, fp.Zero), fp.FromFloat(rps));
            return new ShotInput(fp3.Zero, vel, fp.FromInt(30), spin);
        }

        private static float CarryYards(Trajectory t) => t.finalPosition.z.ToFloat() * 1.09361f;
    }
}
