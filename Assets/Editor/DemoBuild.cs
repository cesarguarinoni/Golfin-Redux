using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Profile;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace GolfinRedux.DemoEditor
{
    /// <summary>
    /// Batchmode demo build entry points (demo_build_slice §3.5). Invoked headlessly by
    /// Tools/build-demo.sh via -executeMethod. Activates the demo build profile through
    /// the BuildProfile API (NOT the -activeBuildProfile CLI flag, which has a reported
    /// Unity 6 batchmode bug — §3.1), builds the player, and dumps a BuildReport summary
    /// plus the top 50 packed assets by size to Builds/build-report-&lt;profile&gt;.txt so the
    /// QA pass can eyeball the payload for anything that shouldn't ship.
    ///
    /// The MCP-package + Assets/Plugins/NuGet exclusion (keeping the SignalR client out of
    /// the store binary) is handled by build-demo.sh BEFORE Unity launches — see the script.
    /// This method also flags any Microsoft.*/SignalR/McpPlugin asset that slips into the
    /// packed-asset list, so a failed exclusion is caught in the report, not on the store.
    /// </summary>
    public static class DemoBuild
    {
        const string ProfileDir = "Assets/Settings/Build Profiles";

        // iOS batchmode emits an Xcode PROJECT folder (not an .ipa); the .ipa comes from an
        // Xcode archive afterwards (testflight_distribution / Order 424). BuildReport is fully
        // populated at this point, which is all §3.5 needs.
        [MenuItem("GOLFIN/Demo/Build iOS-Demo")]
        public static void BuildIOSDemo() => Build("iOS-Demo", "Builds/iOS-Demo", isFolder: true);

        [MenuItem("GOLFIN/Demo/Build Android-Demo")]
        public static void BuildAndroidDemo() => Build("Android-Demo", "Builds/Android-Demo/GolfinDemo.apk", isFolder: false);

        static void Build(string profileName, string outputPath, bool isFolder)
        {
            var profile = AssetDatabase.LoadAssetAtPath<BuildProfile>($"{ProfileDir}/{profileName}.asset");
            if (profile == null)
            {
                Fail($"Build profile not found: {ProfileDir}/{profileName}.asset");
                return;
            }

            // Activate via API — the -activeBuildProfile CLI flag exits batchmode when the
            // profile is already active and needs a project-relative path (Unity 6 bug, §3.1).
            BuildProfile.SetActiveBuildProfile(profile);

            var full = Path.GetFullPath(outputPath);
            var containingDir = isFolder ? full : Path.GetDirectoryName(full);
            if (!string.IsNullOrEmpty(containingDir))
                Directory.CreateDirectory(containingDir);

            // Tell the scene stripper this IS a demo build, regardless of what define the
            // editor's own assemblies were compiled with (they may be a non-demo profile).
            DemoSceneProcessor.ForceDemoStrip = true;
            BuildReport report;
            try
            {
                report = BuildPipeline.BuildPlayer(new BuildPlayerWithProfileOptions
                {
                    buildProfile = profile,
                    locationPathName = outputPath,
                    options = BuildOptions.None,
                });
            }
            finally
            {
                DemoSceneProcessor.ForceDemoStrip = false;
            }

            WriteReport(report, profileName);

            if (report.summary.result != BuildResult.Succeeded)
            {
                Fail($"Build {report.summary.result} — {report.summary.totalErrors} error(s). See Builds/build-report-{profileName}.txt");
                return;
            }
            Debug.Log($"[DemoBuild] {profileName} SUCCEEDED → {report.summary.outputPath}");
        }

        static void WriteReport(BuildReport report, string profileName)
        {
            var s = report.summary;
            var sb = new StringBuilder();
            sb.AppendLine($"=== GOLFIN demo build report — {profileName} ===");
            sb.AppendLine($"result       : {s.result}");
            sb.AppendLine($"platform     : {s.platform}");
            sb.AppendLine($"output       : {s.outputPath}");
            sb.AppendLine($"total size   : {s.totalSize / (1024f * 1024f):F2} MB ({s.totalSize} bytes)");
            sb.AppendLine($"errors/warn  : {s.totalErrors} / {s.totalWarnings}");
            sb.AppendLine($"duration     : {(s.buildEndedAt - s.buildStartedAt).TotalSeconds:F1}s");
            sb.AppendLine();

            var contents = report.packedAssets
                .SelectMany(pa => pa.contents)
                .OrderByDescending(c => c.packedSize)
                .ToList();

            // Exclusion tripwire: no MCP/SignalR/Roslyn *assembly* should reach the payload.
            // Match managed DLLs only (by name), skip Unity's own packages, and require a dot
            // around "SignalR" — so Unity Timeline's SignalReceiver.cs no longer false-flags.
            string[] banned = { "Microsoft.AspNetCore", "Microsoft.Extensions.", "Microsoft.CodeAnalysis", ".SignalR.", "McpPlugin", "ReflectorNet", "com.ivanmurzak", "IvanMurzak" };
            var leaks = contents
                .Where(c =>
                {
                    var p = c.sourceAssetPath ?? "";
                    if (!p.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) return false;        // managed assemblies only
                    if (p.StartsWith("Packages/com.unity.", StringComparison.OrdinalIgnoreCase)) return false; // Unity's own packages
                    return banned.Any(b => p.IndexOf(b, StringComparison.OrdinalIgnoreCase) >= 0);
                })
                .ToList();
            sb.AppendLine(leaks.Count == 0
                ? "MCP/SignalR exclusion: PASS — no MCP/SignalR/Roslyn assets in payload."
                : $"MCP/SignalR exclusion: *** FAIL *** — {leaks.Count} banned asset(s) shipped:");
            foreach (var c in leaks)
                sb.AppendLine($"    LEAK  {c.packedSize / 1024f:F1} KB  {c.sourceAssetPath}");
            sb.AppendLine();

            sb.AppendLine("=== top 50 packed assets by size — eyeball for anything that shouldn't ship ===");
            foreach (var c in contents.Take(50))
                sb.AppendLine($"{c.packedSize / 1024f,10:F1} KB  {c.sourceAssetPath}");

            Directory.CreateDirectory("Builds");
            var path = Path.Combine("Builds", $"build-report-{profileName}.txt");
            File.WriteAllText(path, sb.ToString());
            Debug.Log($"[DemoBuild] report → {Path.GetFullPath(path)}");
        }

        static void Fail(string msg)
        {
            Debug.LogError($"[DemoBuild] {msg}");
            if (Environment.GetCommandLineArgs().Contains("-batchmode"))
                EditorApplication.Exit(1);
        }
    }
}
