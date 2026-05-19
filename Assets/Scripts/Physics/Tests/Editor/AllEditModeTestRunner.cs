#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

/// <summary>
/// loop_v2_smoke_bot task: runs ALL EditMode test assemblies and writes summary
/// to Docs/Diagnostics/all_editmode_test_results.txt.
/// Usage: GOLFIN/Smoke/Run All EditMode Tests
/// Safe to leave in project — only fires on menu click, never auto-runs.
/// </summary>
public static class AllEditModeTestRunner
{
    const string ResultsPath = "Docs/Diagnostics/all_editmode_test_results.txt";

    [MenuItem("GOLFIN/Smoke/Run All EditMode Tests")]
    public static void Run()
    {
        Debug.Log("[AllEditModeTestRunner] Starting ALL EditMode tests...");
        var api = ScriptableObject.CreateInstance<TestRunnerApi>();
        var filter = new Filter { testMode = TestMode.EditMode };
        api.RegisterCallbacks(new Callbacks());
        api.Execute(new ExecutionSettings(filter));
    }

    sealed class Callbacks : ICallbacks
    {
        int _pass, _fail, _skip, _total;
        readonly StringBuilder _sb = new StringBuilder();

        public void RunStarted(ITestAdaptor testsToRun)
        {
            _sb.AppendLine($"=== ALL EditMode Test Gate — loop_v2_smoke_bot ===");
            _sb.AppendLine($"Timestamp: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            _sb.AppendLine();
        }

        public void RunFinished(ITestResultAdaptor result)
        {
            _sb.AppendLine();
            _sb.AppendLine($"TOTAL  : {_total}");
            _sb.AppendLine($"PASSED : {_pass}");
            _sb.AppendLine($"FAILED : {_fail}");
            _sb.AppendLine($"SKIPPED: {_skip}");
            _sb.AppendLine();
            _sb.AppendLine(_fail == 0 ? "GATE: PASS" : "GATE: FAIL");

            string text = _sb.ToString();
            File.WriteAllText(ResultsPath, text);
            Debug.Log($"[AllEditModeTestRunner] Results → {ResultsPath}");
            Debug.Log($"[AllEditModeTestRunner] Total={_total} Passed={_pass} Failed={_fail} Skipped={_skip} — {(_fail == 0 ? "GATE PASS" : "GATE FAIL")}");
        }

        public void TestStarted(ITestAdaptor test) { }

        public void TestFinished(ITestResultAdaptor result)
        {
            if (result.HasChildren) return;
            _total++;
            switch (result.TestStatus)
            {
                case TestStatus.Passed:  _pass++;  break;
                case TestStatus.Failed:
                    _fail++;
                    _sb.AppendLine($"  FAIL: {result.Test.FullName}");
                    if (!string.IsNullOrEmpty(result.Message))
                        _sb.AppendLine($"        {result.Message}");
                    break;
                default:               _skip++;  break;
            }
        }
    }
}
#endif
