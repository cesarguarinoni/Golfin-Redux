// §controls_i — temporary auto-runner: fires once after scripts reload, runs BallAnimatorTests, writes results.
// DELETE this file after the pipeline confirms tests pass.
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;
using UnityEditor.TestTools.TestRunner.Api;

namespace Golfin.Physics.Tests.Editor
{
    [InitializeOnLoad]
    public static class BallAnimatorTestAutoRunner
    {
        static readonly string ResultsPath =
            Path.Combine(Application.dataPath, "../Docs/Specs/Active/controls_i_ball_visual_rotation/test_results.txt");

        static BallAnimatorTestAutoRunner()
        {
            // Only run if the results file doesn't exist yet (one-shot).
            if (File.Exists(ResultsPath)) return;
            // Don't run in play mode — only in edit mode.
            if (EditorApplication.isPlaying || EditorApplication.isCompiling) return;

            // Defer execution to avoid issues during domain reload.
            EditorApplication.delayCall += RunTests;
        }

        static void RunTests()
        {
            if (EditorApplication.isPlaying || EditorApplication.isCompiling) return;

            Debug.Log("[BallAnimatorTestAutoRunner] Running BallAnimatorTests (§controls_i)...");
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            var filter = new Filter
            {
                testMode = TestMode.EditMode,
                testNames = new[]
                {
                    "Golfin.Physics.Tests.BallAnimatorTests.Update_AppliesRotation_WhenBallTranslatesHorizontally",
                    "Golfin.Physics.Tests.BallAnimatorTests.Update_DoesNotRotate_WhenBallStationary"
                }
            };
            api.RegisterCallbacks(new AutoRunCallbacks(ResultsPath));
            api.Execute(new ExecutionSettings(filter));
        }
    }

    internal class AutoRunCallbacks : ICallbacks
    {
        readonly string _path;
        public AutoRunCallbacks(string path) { _path = path; }
        public void RunStarted(ITestAdaptor testsToRun) { }
        public void RunFinished(ITestResultAdaptor result)
        {
            File.WriteAllText(_path,
                $"Passed={result.PassCount}\nFailed={result.FailCount}\nSkipped={result.SkipCount}\nDuration={result.Duration}\nStatus={result.TestStatus}\n");
            Debug.Log($"[BallAnimatorTestAutoRunner] DONE — Passed={result.PassCount}, Failed={result.FailCount}");
        }
        public void TestStarted(ITestAdaptor test) { }
        public void TestFinished(ITestResultAdaptor result)
        {
            Debug.Log($"[BallAnimatorTestAutoRunner] {result.Test.FullName}: {result.TestStatus} — {result.Message}");
        }
    }
}
#endif
