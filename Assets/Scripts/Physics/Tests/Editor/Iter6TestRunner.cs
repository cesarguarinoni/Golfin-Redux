#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using System.IO;

/// <summary>
/// §controls_h iter-6 test runner. Runs the Physics.Tests EditMode suite
/// and writes results to /tmp/iter6_test_results.txt.
/// Invoked via: GOLFIN > Run Physics Tests (Iter6)
/// </summary>
public static class Iter6TestRunner
{
    [MenuItem("GOLFIN/Run Physics Tests (Iter6)")]
    public static void Run()
    {
        var api = ScriptableObject.CreateInstance<TestRunnerApi>();
        var filter = new Filter
        {
            testMode = TestMode.EditMode,
            assemblyNames = new string[] { "Golfin.Physics.Tests" }
        };

        api.RegisterCallbacks(new Callbacks());
        api.Execute(new ExecutionSettings(filter));
        Debug.Log("[Iter6TestRunner] Test run started.");
    }

    sealed class Callbacks : ICallbacks
    {
        int _pass, _fail, _skip;
        readonly System.Text.StringBuilder _sb = new System.Text.StringBuilder();
        readonly System.Text.StringBuilder _fails = new System.Text.StringBuilder();

        public void RunStarted(ITestAdaptor testsToRun)
            => Debug.Log($"[Iter6TestRunner] Starting. Total scheduled: {CountLeaf(testsToRun)}");

        public void RunFinished(ITestResultAdaptor result)
        {
            string summary = $"PASS:{_pass} FAIL:{_fail} SKIP:{_skip}\n";
            string fullText = summary + _sb.ToString();
            File.WriteAllText("/tmp/iter6_test_results.txt", fullText);
            Debug.Log($"[Iter6TestRunner] COMPLETE — PASS:{_pass} FAIL:{_fail} SKIP:{_skip}");
            if (_fail > 0)
                Debug.LogWarning($"[Iter6TestRunner] FAILURES:\n{_fails}");
        }

        public void TestStarted(ITestAdaptor test) {}

        public void TestFinished(ITestResultAdaptor result)
        {
            if (result.HasChildren) return;
            string status;
            if (result.TestStatus == TestStatus.Passed)   { status = "PASS"; _pass++; }
            else if (result.TestStatus == TestStatus.Skipped) { status = "SKIP"; _skip++; }
            else                                          { status = "FAIL"; _fail++; }
            _sb.AppendLine($"{status}: {result.Name}");
            if (result.TestStatus != TestStatus.Passed && result.TestStatus != TestStatus.Skipped)
            {
                _fails.AppendLine($"  FAIL: {result.Name}");
                if (result.Message != null) _fails.AppendLine($"    MSG: {result.Message}");
            }
        }

        static int CountLeaf(ITestAdaptor a)
        {
            if (!a.HasChildren) return 1;
            int c = 0;
            foreach (var ch in a.Children) c += CountLeaf(ch);
            return c;
        }
    }
}
#endif
