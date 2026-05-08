// § controls_h iter-4 gate: runs Golfin.Physics.Tests EditMode tests via TestRunnerApi
// and writes results to Docs/Specs/Active/controls_h_chase_camera_regression/iter4_test_results.txt
// Usage: GOLFIN > Tests > Run Iter4 Gate
// This file is safe to leave in the project (it won't auto-run; only fires on menu click).
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEditor.TestTools.TestRunner.Api;

namespace Golfin.Physics.Tests.Editor
{
    public static class Iter4TestRunner
    {
        const string ResultsPath =
            "Docs/Specs/Active/controls_h_chase_camera_regression/iter4_test_results.txt";

        [MenuItem("GOLFIN/Tests/Run Iter4 Gate")]
        public static void RunTests()
        {
            Debug.Log("[Iter4TestRunner] Starting EditMode test run for Golfin.Physics.Tests ...");

            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            var filter = new Filter
            {
                testMode         = TestMode.EditMode,
                assemblyNames    = new[] { "Golfin.Physics.Tests" },
            };

            api.RegisterCallbacks(new Callbacks());
            api.Execute(new ExecutionSettings(filter));
        }

        class Callbacks : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun)
            {
                Debug.Log($"[Iter4TestRunner] Run started. Tree root: {testsToRun.Name}");
            }

            public void RunFinished(ITestResultAdaptor result)
            {
                int passed  = 0;
                int failed  = 0;
                int skipped = 0;
                int total   = 0;
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("=== controls_h iter-4 EditMode gate ===");
                sb.AppendLine($"Timestamp: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine($"Assembly: Golfin.Physics.Tests");
                sb.AppendLine();

                CollectResults(result, sb, ref passed, ref failed, ref skipped, ref total);

                sb.AppendLine();
                sb.AppendLine($"TOTAL  : {total}");
                sb.AppendLine($"PASSED : {passed}");
                sb.AppendLine($"FAILED : {failed}");
                sb.AppendLine($"SKIPPED: {skipped}");
                sb.AppendLine();
                sb.AppendLine(failed == 0 ? "GATE: PASS" : "GATE: FAIL");

                string text = sb.ToString();
                File.WriteAllText(ResultsPath, text);
                Debug.Log($"[Iter4TestRunner] Results written to {ResultsPath}");
                Debug.Log($"[Iter4TestRunner] {(failed == 0 ? "ALL PASS" : "FAILURES DETECTED")} — Total={total} Passed={passed} Failed={failed} Skipped={skipped}");
            }

            public void TestStarted(ITestAdaptor test)  { }
            public void TestFinished(ITestResultAdaptor result)
            {
                if (result.TestStatus == TestStatus.Failed)
                    Debug.LogError($"[Iter4TestRunner] FAIL: {result.Test.FullName}\n{result.Message}");
            }

            static void CollectResults(ITestResultAdaptor node, System.Text.StringBuilder sb,
                ref int passed, ref int failed, ref int skipped, ref int total)
            {
                if (!node.HasChildren || node.Children == null)
                {
                    // Leaf test
                    total++;
                    switch (node.TestStatus)
                    {
                        case TestStatus.Passed:  passed++;  sb.AppendLine($"  PASS  {node.Test.FullName}"); break;
                        case TestStatus.Failed:  failed++;  sb.AppendLine($"  FAIL  {node.Test.FullName}\n        {node.Message}"); break;
                        case TestStatus.Skipped: skipped++; sb.AppendLine($"  SKIP  {node.Test.FullName}"); break;
                        default:                            sb.AppendLine($"  ???   {node.Test.FullName}"); break;
                    }
                    return;
                }

                foreach (var child in node.Children)
                    CollectResults(child, sb, ref passed, ref failed, ref skipped, ref total);
            }
        }
    }
}
