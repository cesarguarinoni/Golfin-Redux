#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Input;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Golfin.EditorTools
{
    /// <summary>
    /// Showcase recorder for the Rankings (Leaderboard) screen — produces a clean
    /// MP4 at full iPhone-14 1170x2532 for the daily report.
    ///
    /// Sequence: boot → Home → open Leaderboard → hold on DAILY podium+list →
    /// cycle WEEKLY / MONTHLY / HISTORY tabs (each board reshuffles) → back to DAILY.
    ///
    /// Output: Docs/Specs/Active/leaderboard_wiring/videos/raw.mp4
    /// Usage:  GOLFIN > Rankings > Record Showcase Video  (or call LaunchDemo()).
    /// </summary>
    public static class RankingsDemoRecorder
    {
        const string ShellScenePath = "Assets/Scenes/ShellScene.unity";
        const string OutputDir      = "Docs/Specs/Active/leaderboard_wiring/videos";
        const string ArmedKey       = "RankingsDemoRecorder.Armed";

        static RecorderController _recorder;

        [InitializeOnLoadMethod]
        static void RegisterHook()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        [MenuItem("GOLFIN/Rankings/Record Showcase Video")]
        public static void LaunchDemo()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[RankingsDemo] Already in play mode — stop first.");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            EditorSceneManager.OpenScene(ShellScenePath);
            Directory.CreateDirectory(OutputDir);
            SessionState.SetBool(ArmedKey, true);
            EditorApplication.EnterPlaymode();
            Debug.Log("[RankingsDemo] Armed. Entering play mode...");
        }

        static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(ArmedKey, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                SessionState.SetBool(ArmedKey, false);
                StartRecorderAndBot();
            }
            else if (state == PlayModeStateChange.ExitingPlayMode)
            {
                StopRecorder();
            }
        }

        static bool TryEnsureIPhone14Selected()
        {
            try
            {
                var asm = System.Reflection.Assembly.Load("Golfin.Physics.Viewer.Bot.Editor");
                var t   = asm?.GetType("Golfin.Physics.Viewer.Editor.GameViewSizeUtil");
                var m   = t?.GetMethod("EnsureIPhone14Selected",
                              System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                return m != null && (bool)m.Invoke(null, null);
            }
            catch { return false; }
        }

        static void StartRecorderAndBot()
        {
            bool selected = TryEnsureIPhone14Selected();
            int w = 1170, h = 2532;
            if (!selected)
            {
                PlayModeWindow.GetRenderingResolution(out uint cw, out uint ch);
                if (cw > 0 && ch > 0)
                {
                    w = Mathf.Max(2, (int)cw); h = Mathf.Max(2, (int)ch);
                    if (w % 2 != 0) w--; if (h % 2 != 0) h--;
                    Debug.LogWarning($"[RankingsDemo] Could not pin iPhone-14 — recording at {w}x{h}.");
                }
            }

            var movie = ScriptableObject.CreateInstance<MovieRecorderSettings>();
            movie.name         = "RankingsDemo";
            movie.Enabled      = true;
            movie.OutputFormat = MovieRecorderSettings.VideoRecorderOutputFormat.MP4;
            movie.ImageInputSettings = new GameViewInputSettings { OutputWidth = w, OutputHeight = h };
            movie.AudioInputSettings.PreserveAudio = false;
            movie.OutputFile = $"{OutputDir}/raw";

            var settings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
            settings.AddRecorderSettings(movie);
            settings.SetRecordModeToManual();
            settings.FrameRate = 30;
            settings.FrameRatePlayback = FrameRatePlayback.Variable;

            _recorder = new RecorderController(settings);
            _recorder.PrepareRecording();
            _recorder.StartRecording();
            Debug.Log($"[RankingsDemo] Recording → {OutputDir}/raw.mp4 ({w}x{h} @ 30fps)");

            var host = new GameObject("[RankingsDemoBot]");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.AddComponent<RankingsDemoRunner>().StartDemo();
        }

        static void StopRecorder()
        {
            if (_recorder != null)
            {
                try { if (_recorder.IsRecording()) _recorder.StopRecording(); Debug.Log("[RankingsDemo] Recording stopped."); }
                catch (Exception e) { Debug.LogWarning($"[RankingsDemo] StopRecorder: {e.Message}"); }
                _recorder = null;
            }
        }
    }

    public class RankingsDemoRunner : MonoBehaviour
    {
        public void StartDemo() => StartCoroutine(Sequence());

        static void ClickTab(string tabName)
        {
            var go = Resources.FindObjectsOfTypeAll<Button>()
                .FirstOrDefault(b => b.gameObject.name == tabName
                    && !string.IsNullOrEmpty(b.gameObject.scene.name)
                    && b.GetComponentInParent<Canvas>() != null);
            if (go != null) go.onClick.Invoke();
            else Debug.LogWarning($"[RankingsDemo] tab '{tabName}' not found");
        }

        IEnumerator Sequence()
        {
            // Boot through splash/loading → Home.
            yield return new WaitForSecondsRealtime(4.5f);

            // Brief Home glimpse for context, then open the Leaderboard.
            yield return new WaitForSecondsRealtime(1.2f);
            GolfinRedux.UI.ScreenManager.Instance?.ShowScreen(GolfinRedux.UI.ScreenId.Leaderboard);
            yield return new WaitForSecondsRealtime(1.0f); // fade

            // DAILY (default) — podium + rank-4 list.
            yield return new WaitForSecondsRealtime(2.6f);

            ClickTab("WeeklyTab");
            yield return new WaitForSecondsRealtime(2.6f);

            ClickTab("MonthlyTab");
            yield return new WaitForSecondsRealtime(2.6f);

            ClickTab("HistoryTab");
            yield return new WaitForSecondsRealtime(2.6f);

            ClickTab("DailyTab");
            yield return new WaitForSecondsRealtime(1.8f);

            Debug.Log("[RankingsDemo] Sequence done — exiting play mode.");
            EditorApplication.ExitPlaymode();
        }
    }
}
#endif
