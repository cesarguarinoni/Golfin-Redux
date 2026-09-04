// ─────────────────────────────────────────────────────────────────────────────
// game_polish_a §A12 — an EditMode sweep that leaves a readable artifact.
//
// WHY THIS EXISTS RATHER THAN JUST USING THE MCP tests-run TOOL. Two reasons,
// both hit during this task:
//   · tests-run returns through a 60-second tool timeout, and the project's full
//     EditMode sweep takes minutes. The run completes server-side; the CALLER
//     never sees the result, so "green" would be an assumption rather than a
//     reading.
//   · Registering ITestAdaptor callbacks from a script-execute snippet does not
//     survive the domain reloads the run itself causes — the callbacks are in a
//     transient dynamic assembly, so the report stops mid-run with no sign that
//     it stopped. (The first attempt here recorded 17 of several hundred tests
//     and looked like a completed run.)
// A compiled Editor script's callbacks survive the reload, and the report is a
// file on disk that can be quoted rather than a claim.
//
// The output is Docs/Diagnostics/_capture/game_polish_a_tests.txt: one line per
// test, then a summary line. It is rewritten on every TestFinished, so a run that
// dies halfway leaves evidence of exactly where it died.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace Golfin.UI.Polish.EditorTools
{
    public static class GamePolishTestReport
    {
        const string ReportPath = "Docs/Diagnostics/_capture/game_polish_a_tests.txt";

        [MenuItem("GOLFIN/Game Polish/Run EditMode sweep -> report file", priority = 266)]
        public static void Run() => Run(null);

        /// <summary>
        /// The Polish assembly on its own — seconds rather than the twenty minutes the full sweep
        /// takes, because that sweep also loads every real hole. Used to re-confirm THIS task's
        /// two suites after a fix without re-running the terrain tests that have nothing to do
        /// with it. The full sweep is still what A12 quotes.
        /// </summary>
        [MenuItem("GOLFIN/Game Polish/Run the Polish suites only -> report file", priority = 267)]
        public static void RunPolishOnly() => Run("Golfin.UI.Polish.Tests");

        public static void Run(string? assembly)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath)!);
            File.WriteAllText(ReportPath, "RUN REQUESTED " + System.DateTime.Now.ToString("u") +
                                          (assembly == null ? " (full EditMode)" : " (" + assembly + ")") + "\n");

            var filter = new Filter { testMode = TestMode.EditMode };
            if (assembly != null) filter.assemblyNames = new[] { assembly };

            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.RegisterCallbacks(new Callbacks());
            api.Execute(new ExecutionSettings(filter));
            Debug.Log("[GamePolishTests] sweep requested -> " + ReportPath);
        }

        /// <summary>
        /// Compiled, so it survives the domain reloads the run triggers. The buffer is rebuilt
        /// from the file on every callback for the same reason: a reload wipes statics, and a
        /// StringBuilder that starts empty after one would silently truncate the report.
        /// </summary>
        private sealed class Callbacks : ICallbacks
        {
            public void RunStarted(ITestAdaptor tests)
                => Append("RUN STARTED — " + tests.TestCaseCount + " cases", reset: true);

            public void TestStarted(ITestAdaptor test) { }

            public void TestFinished(ITestResultAdaptor result)
            {
                if (result.Test.IsSuite) return;
                string msg = (result.Message ?? string.Empty).Replace("\n", " | ");
                Append(result.TestStatus + "\t" + result.Test.FullName + (msg.Length > 0 ? "\t" + msg : ""));
            }

            public void RunFinished(ITestResultAdaptor result)
                => Append($"RUN FINISHED passed={result.PassCount} failed={result.FailCount} " +
                          $"skipped={result.SkipCount} inconclusive={result.InconclusiveCount} " +
                          $"duration={result.Duration:0.0}s");

            static void Append(string line, bool reset = false)
            {
                var sb = new StringBuilder();
                if (!reset && File.Exists(ReportPath)) sb.Append(File.ReadAllText(ReportPath));
                sb.AppendLine(line);
                File.WriteAllText(ReportPath, sb.ToString());
            }
        }
    }
}
