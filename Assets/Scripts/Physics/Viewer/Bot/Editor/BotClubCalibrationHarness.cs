// BotClubCalibrationHarness.cs
// versus_bot_hardening H1: generates Assets/Resources/Data/bot_clubs.csv
// from headless BallSimulation.Simulate probe sims (production stat path).
//
// Usage: GOLFIN > Bot > Calibrate Clubs
// Output: Assets/Resources/Data/bot_clubs.csv
// The CSV is committed and read at runtime by VersusBot.
#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Golfin.Physics;
using Golfin.Physics.Math;
using Golfin.Physics.Stats;
using Golfin.Gameplay.Defaults;

namespace Golfin.Physics.Viewer.Editor
{
    /// <summary>
    /// Editor-only harness that probes each lab club across power ∈ [0..1] (0.05 steps)
    /// using the PRODUCTION stat path (DefaultStatProvider) on flat ground, measures flat
    /// XZ carry, and emits Assets/Resources/Data/bot_clubs.csv.
    ///
    /// Club indices: 0=Driver, 1=Iron7, 2=Wedge, 3=Putter.
    /// CSV columns: club,power01,carry_meters
    /// </summary>
    public static class BotClubCalibrationHarness
    {
        private const string OutputPath = "Assets/Resources/Data/bot_clubs.csv";

        // Power steps: 0.0 to 1.0 inclusive in 0.05 increments = 21 probes per club.
        private const float PowerStep    = 0.05f;
        private const float PowerMin     = 0.0f;
        private const float PowerMax     = 1.0f;

        // Putter uses a flat ground with Green surface.
        // Non-putters use Fairway.

        [MenuItem("GOLFIN/Bot/Calibrate Clubs")]
        public static void Run()
        {
            Debug.Log("[BotCalibration] Starting club calibration probe sims...");

            var rows = new List<string>();
            rows.Add("club,power01,carry_meters");

            // ── Non-putter clubs: 0=Driver, 1=Iron7, 2=Wedge ──────────────────
            var swingGround   = new FlatGround(fp.Zero);
            var swingSurfaces = new ConstantSurfaceProvider(SurfaceType.Fairway);
            var aero          = AeroConfig.Vacuum;
            var wind          = WindConfig.Calm;
            var surfaceCfg    = SurfaceConfig.Default;
            var puttCfg       = PuttConfig.Default;
            var ballMods      = BallPhysicsModifiers.Neutral;
            var coeffs        = StatCoefficients.Default;
            var caps          = StatCaps.Default;

            string[] clubNames = new string[] { "driver", "iron7", "wedge" };
            for (int clubIdx = 0; clubIdx <= 2; clubIdx++)
            {
                var bundle = DefaultStatProvider.BuildSwingBundle(clubIdx);

                for (float power = PowerMin; power <= PowerMax + 1e-5f; power += PowerStep)
                {
                    float p = Mathf.Clamp01(power);
                    fp fpPower = fp.FromFloat(p);

                    var (shotInput, bMods) = ShotInputBuilder.Build(
                        bundle, coeffs, caps,
                        fpPower,
                        fp.Zero,  // aim straight (+X direction)
                        fp.Zero, fp.Zero, fp.Zero,
                        seed: 12345u);

                    var traj = BallSimulation.Simulate(
                        shotInput, swingGround, aero, wind, swingSurfaces, surfaceCfg, puttCfg, bMods);

                    float carryX = traj.finalPosition.x.ToFloat();
                    float carryZ = traj.finalPosition.z.ToFloat();
                    float carry  = Mathf.Sqrt(carryX * carryX + carryZ * carryZ);

                    rows.Add($"{clubNames[clubIdx]},{p:F2},{carry:F2}");
                }

                Debug.Log($"[BotCalibration] {clubNames[clubIdx]}: max carry at power=1.0 = {GetLastCarry(rows, clubNames[clubIdx]):F1}m");
            }

            // ── Putter: club 3 ─────────────────────────────────────────────────
            // Putter sims on Green surface. Origin on Y=0 flat (no cup, just carry/roll).
            var puttGround   = new FlatGround(fp.Zero);
            var puttSurfaces = new ConstantSurfaceProvider(SurfaceType.Green);
            var puttBundle   = DefaultStatProvider.BuildPuttBundle();

            for (float power = PowerMin; power <= PowerMax + 1e-5f; power += PowerStep)
            {
                float p = Mathf.Clamp01(power);
                fp fpPower = fp.FromFloat(p);

                var (shotInput, bMods) = ShotInputBuilder.Build(
                    puttBundle, coeffs, caps,
                    fpPower,
                    fp.Zero,
                    fp.Zero, fp.Zero, fp.Zero,
                    seed: 12345u);

                var traj = BallSimulation.Simulate(
                    shotInput, puttGround, aero, wind, puttSurfaces, surfaceCfg, puttCfg, bMods);

                float carryX = traj.finalPosition.x.ToFloat();
                float carryZ = traj.finalPosition.z.ToFloat();
                float carry  = Mathf.Sqrt(carryX * carryX + carryZ * carryZ);

                rows.Add($"putter,{p:F2},{carry:F2}");
            }
            Debug.Log($"[BotCalibration] putter: max carry at power=1.0 = {GetLastCarry(rows, "putter"):F1}m");

            // ── Write CSV ──────────────────────────────────────────────────────
            string dir = Path.GetDirectoryName(Path.Combine(Application.dataPath, "..", OutputPath));
            Directory.CreateDirectory(dir);

            var sb = new StringBuilder();
            foreach (var r in rows) sb.AppendLine(r);
            File.WriteAllText(
                Path.GetFullPath(Path.Combine(Application.dataPath, "..", OutputPath)),
                sb.ToString());

            AssetDatabase.ImportAsset(OutputPath);
            AssetDatabase.Refresh();

            Debug.Log($"[BotCalibration] Done. Wrote {rows.Count - 1} data rows to {OutputPath}.");
            EditorUtility.DisplayDialog("Bot Calibration", $"bot_clubs.csv written ({rows.Count - 1} rows).", "OK");
        }

        private static float GetLastCarry(List<string> rows, string clubName)
        {
            for (int i = rows.Count - 1; i >= 1; i--)
            {
                if (rows[i].StartsWith(clubName + ","))
                {
                    var parts = rows[i].Split(',');
                    if (parts.Length >= 3 && float.TryParse(parts[2],
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out float v))
                        return v;
                }
            }
            return 0f;
        }
    }
}
#endif
