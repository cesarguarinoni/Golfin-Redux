#if UNITY_EDITOR
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Golfin.Gameplay.UI.Controls;
using Golfin.Gameplay.UI.Controls.Bot;

namespace Golfin.Physics.Viewer.Editor
{
    /// <summary>
    /// Writes the three <c>execSigma*</c> columns of <c>bot_difficulty.csv</c>
    /// (bot_scheme_parity §5) — the calibration guard that keeps 1v1 difficulty where it was when
    /// a bot swings a graded scheme instead of Flick.
    ///
    /// <para>THE COMMITTED NUMBERS COME FROM HERE, NEVER FROM A HAND EDIT. Each bracket × scheme
    /// is bisected over 20 000 samples of that scheme's own grader until the bot's expected
    /// absolute yaw error matches Flick's <c>aimErrorDegMax / 2</c> to within 3 %. A sigma typed
    /// by eye would be a difficulty change disguised as a control-scheme change, which is the one
    /// thing this whole track must not do.</para>
    ///
    /// <para>RE-RUN IT whenever a grader's tuning moves — <c>PendulumJustWindow*</c>,
    /// <c>PendulumGoodWindow01</c>, <c>PendulumMissYawGain</c>, the Needle zone keys, the Free
    /// Swing impact window or miss range, or the cone half-angle. <c>controls.csv</c> carries a
    /// comment saying so next to those keys.</para>
    ///
    /// <para>Menu: <b>Tools ▸ Golfin ▸ Bots ▸ Calibrate Scheme Sigma</b>. It prints the table and
    /// rewrites the CSV in place; diff it before committing.</para>
    /// </summary>
    public static class BotSchemeCalibrationHarness
    {
        private const string CsvPath = "Assets/Resources/Data/bot_difficulty.csv";
        private const int    Samples = 20000;

        private const string Header =
            "minLevel,aimErrorDegMax,powerErrorMax,clubNoiseChance," +
            "execSigmaPendulum01,execSigmaNeedle01,execSigmaFreeSwing01";

        [MenuItem("Tools/Golfin/Bots/Calibrate Scheme Sigma")]
        public static void Calibrate()
        {
            string full = Path.Combine(Directory.GetCurrentDirectory(), CsvPath);
            if (!File.Exists(full))
            {
                Debug.LogError($"[BotSchemeCalibration] {CsvPath} not found.");
                return;
            }

            var lines = File.ReadAllLines(full);
            var outp  = new List<string>(lines.Length + 4);
            var table = new StringBuilder();
            table.AppendLine("[BotSchemeCalibration] target E|ErrorYaw| = aimErrorDegMax / 2, " +
                             $"{Samples} samples, acc={BotSchemeSigmaCalibrator.ReferenceAccuracyNorm01:F2} " +
                             $"power={BotSchemeSigmaCalibrator.ReferencePower01:F2} " +
                             $"halfCone={BotSchemeSigmaCalibrator.ReferenceHalfConeDegDefault:F2}°");
            table.AppendLine("  minLevel | target° |  σ pend (got°) |  σ needle (got°) |  σ free (got°)");

            bool headerWritten = false;
            int  rows = 0;

            foreach (var raw in lines)
            {
                string line = raw.TrimEnd();
                if (line.StartsWith("#")) { outp.Add(line); continue; }
                if (line.StartsWith("minLevel"))
                {
                    outp.Add(Header);
                    headerWritten = true;
                    continue;
                }
                if (string.IsNullOrWhiteSpace(line)) { outp.Add(line); continue; }

                var p = line.Split(',');
                if (p.Length < 4 ||
                    !int.TryParse(p[0].Trim(), out int minLevel) ||
                    !TryF(p[1], out float aimErr) || !TryF(p[2], out float powErr) ||
                    !TryF(p[3], out float clubNoise))
                {
                    outp.Add(line);
                    continue;
                }

                float target = aimErr * 0.5f;
                float sP = BotSchemeSigmaCalibrator.CalibrateDefault(ControlScheme.Pendulum,  target, Samples,
                                                              BotSchemeSigmaCalibrator.DefaultSeed, out float gP);
                float sN = BotSchemeSigmaCalibrator.CalibrateDefault(ControlScheme.Needle,    target, Samples,
                                                              BotSchemeSigmaCalibrator.DefaultSeed, out float gN);
                float sF = BotSchemeSigmaCalibrator.CalibrateDefault(ControlScheme.FreeSwing, target, Samples,
                                                              BotSchemeSigmaCalibrator.DefaultSeed, out float gF);

                outp.Add(string.Format(CultureInfo.InvariantCulture,
                                       "{0},{1},{2},{3},{4:0.####},{5:0.####},{6:0.####}",
                                       minLevel, F(aimErr), F(powErr), F(clubNoise), sP, sN, sF));
                table.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "  {0,8} | {1,6:0.00} | {2,6:0.0000} ({3,5:0.00}) | {4,6:0.0000} ({5,5:0.00}) | {6,6:0.0000} ({7,5:0.00})",
                    minLevel, target, sP, gP, sN, gN, sF, gF));
                rows++;
            }

            if (!headerWritten) outp.Insert(0, Header);

            File.WriteAllLines(full, outp);
            AssetDatabase.ImportAsset(CsvPath, ImportAssetOptions.ForceUpdate);
            Debug.Log(table.ToString() + $"\n[BotSchemeCalibration] wrote {rows} bracket rows to {CsvPath}.");
        }

        private static bool TryF(string s, out float v) =>
            float.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out v);

        /// <summary>Re-emit a number the way the CSV already spells it — trailing zeros and all —
        /// so the calibration diff is three new columns and nothing else.</summary>
        private static string F(float v) => v.ToString("0.0###", CultureInfo.InvariantCulture);
    }
}
#endif
