// Assets/Scripts/UI/ModeSelect/Editor/ModesFallbackSync.cs
#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using GolfinRedux.UI.ModeSelect;

namespace GolfinRedux.UI.ModeSelect.EditorTools
{
    /// <summary>
    /// Keeps <see cref="ModesDatabaseCSV.FallbackCsv"/> — the copy of modes.csv the game parses
    /// when <c>Resources.Load</c> cannot produce the real file — equal to the file itself.
    ///
    /// <para>
    /// <b>Why this exists.</b> The fallback used to be a hand-built list of <c>ModeData</c>
    /// objects: a second model of the same five rows, kept in sync by whoever remembered. Nobody
    /// did. On 2026-09-09 it still had Missions <c>locked</c> with <c>target=none</c> six weeks
    /// after missions_v1 unlocked it, paid 20 RP against the CSV's 35, and had no
    /// <c>tournaments</c> row at all — so the one code path whose job is to save a broken build
    /// would instead have shown players a game that does not exist. Making it a copy of the TEXT
    /// removes the per-field drift; this file removes the rest, because a copy nobody checks is
    /// just a slower way to drift.
    /// </para>
    /// <para>
    /// <b>Two halves, and both are needed.</b> <see cref="Validate"/> answers "do they differ?" and
    /// <see cref="SyncMenu"/> fixes it in one click — a guard that only accuses, when the repair is
    /// hand-editing a 6-line string literal, is a guard people learn to skip.
    /// <see cref="ModesFallbackBuildHook"/> is what makes it stick: divergence FAILS THE BUILD.
    /// </para>
    /// </summary>
    public static class ModesFallbackSync
    {
        public const string CsvPath = "Assets/Resources/Data/modes.csv";
        public const string SourcePath = "Assets/Scripts/UI/ModeSelect/ModesDatabaseCSV.cs";

        private const string BeginMarker = "        public const string FallbackCsv =\n@\"";
        private const string EndMarker = "\n\";\n";

        /// <summary>
        /// Line endings are normalised on BOTH sides before comparing. The CSV and the .cs are LF
        /// in the repo, but an editor or a git autocrlf checkout can hand either one CRLF, and a
        /// guard that fails on somebody's line-ending settings is a guard that gets disabled.
        /// </summary>
        private static string Normalize(string s) => s.Replace("\r\n", "\n").Replace("\r", "\n");

        /// <summary>
        /// null when the embedded copy matches the CSV; otherwise a message naming the difference.
        /// </summary>
        public static string Validate()
        {
            if (!File.Exists(CsvPath))
                return $"{CsvPath} is missing — there is nothing to check the embedded fallback against.";

            string onDisk = Normalize(File.ReadAllText(CsvPath));
            string embedded = Normalize(ModesDatabaseCSV.FallbackCsv);
            if (string.Equals(onDisk, embedded, StringComparison.Ordinal)) return null;

            string[] a = onDisk.Split('\n');
            string[] b = embedded.Split('\n');
            string where = $"{CsvPath} has {a.Length} line(s), the embedded copy has {b.Length}";
            for (int i = 0; i < Math.Min(a.Length, b.Length); i++)
            {
                if (a[i] == b[i]) continue;
                where = $"first difference at line {i + 1}:\n" +
                        $"  modes.csv : {a[i]}\n" +
                        $"  embedded  : {b[i]}";
                break;
            }

            return "ModesDatabaseCSV.FallbackCsv has drifted from " + CsvPath + ".\n" + where +
                   "\nRun  Tools > Golfin > Modes > Sync Fallback CSV  to regenerate it.";
        }

        /// <summary>
        /// Rewrites the literal in <see cref="SourcePath"/> from the CSV on disk. Returns true when
        /// the file was changed. Generation, never transcription — the escaping (a literal quote
        /// doubles inside a verbatim string) is exactly the sort of thing a human gets wrong once
        /// and then cannot see.
        /// </summary>
        public static bool Sync()
        {
            string csv = Normalize(File.ReadAllText(CsvPath));
            string src = File.ReadAllText(SourcePath);
            string normalizedSrc = Normalize(src);

            int begin = normalizedSrc.IndexOf(BeginMarker, StringComparison.Ordinal);
            if (begin < 0)
                throw new InvalidOperationException(
                    $"Could not find the FallbackCsv literal in {SourcePath}. If it was renamed or " +
                    "reindented, update ModesFallbackSync.BeginMarker to match.");

            int bodyStart = begin + BeginMarker.Length;
            int end = normalizedSrc.IndexOf(EndMarker, bodyStart, StringComparison.Ordinal);
            if (end < 0)
                throw new InvalidOperationException(
                    $"Found the start of the FallbackCsv literal in {SourcePath} but not its end.");

            string rebuilt = normalizedSrc.Substring(0, bodyStart)
                           + csv.Replace("\"", "\"\"").TrimEnd('\n')
                           + normalizedSrc.Substring(end);

            if (string.Equals(rebuilt, normalizedSrc, StringComparison.Ordinal)) return false;

            File.WriteAllText(SourcePath, rebuilt);
            AssetDatabase.ImportAsset(SourcePath, ImportAssetOptions.ForceUpdate);
            return true;
        }

        [MenuItem("Tools/Golfin/Modes/Sync Fallback CSV")]
        private static void SyncMenu()
        {
            // Console, not a dialog: a modal freezes the Editor, which is death over MCP.
            if (Sync()) Debug.Log($"[ModesFallbackSync] Regenerated ModesDatabaseCSV.FallbackCsv from {CsvPath}.");
            else Debug.Log("[ModesFallbackSync] Already in sync — nothing to do.");
        }

        [MenuItem("Tools/Golfin/Modes/Validate Fallback CSV")]
        private static void ValidateMenu()
        {
            string problem = Validate();
            if (problem == null) Debug.Log($"[ModesFallbackSync] FallbackCsv matches {CsvPath}.");
            else Debug.LogError("[ModesFallbackSync] " + problem);
        }
    }

    /// <summary>
    /// Fails the build when the embedded fallback disagrees with modes.csv.
    ///
    /// <para>
    /// Modelled on <c>LocalizationBuildHook</c>, and failing rather than warning for the same
    /// reason it does: the build would otherwise SUCCEED and ship a fallback describing a
    /// different game, and a warning in a batchmode log is a warning nobody reads. The EditMode
    /// test catches this in the fast loop; this catches it in the one place that cannot be
    /// skipped.
    /// </para>
    /// </summary>
    public sealed class ModesFallbackBuildHook : IPreprocessBuildWithReport
    {
        /// <summary>Early, alongside LocalizationBuildHook. Nothing here depends on other callbacks.</summary>
        public int callbackOrder => -100;

        public void OnPreprocessBuild(BuildReport report)
        {
            string problem = ModesFallbackSync.Validate();
            if (problem != null) throw new BuildFailedException("[ModesFallbackSync] " + problem);
        }
    }
}
#endif
