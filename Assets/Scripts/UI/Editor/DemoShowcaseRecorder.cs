#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Input;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using GolfinRedux.UI;
using GolfinRedux.UI.HoleSelection;

namespace Golfin.EditorTools
{
    /// <summary>
    /// Showcase recorder for the GOLFIN DEMO — produces a clean MP4 at full iPhone-14
    /// 1170x2532 for the daily report. Requires the iOS-Demo build profile active (GOLFIN_DEMO).
    ///
    /// Sequence: boot → Home (welcome banner, Olivia, Practice-only) → hole picker
    /// (Hole 1 unlocked, others locked) → tee off Hole 1 (Olivia + Driver).
    ///
    /// Output: Docs/Reports/Media/demo_showcase_raw.mp4
    /// Usage:  GOLFIN > Demo > Record Showcase Video  (or call LaunchDemo()).
    /// </summary>
    public static class DemoShowcaseRecorder
    {
        const string ShellScenePath = "Assets/Scenes/ShellScene.unity";
        const string OutputDir      = "Docs/Reports/Media";
        const string OutputName     = "demo_showcase_raw";
        const string ArmedKey       = "DemoShowcaseRecorder.Armed";

        static RecorderController _recorder;

        [InitializeOnLoadMethod]
        static void RegisterHook()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        [MenuItem("GOLFIN/Demo/Record Showcase Video")]
        public static void LaunchDemo()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("[DemoShowcase] Already in play mode — stop first."); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ShellScenePath);
            Directory.CreateDirectory(OutputDir);
            SessionState.SetBool(ArmedKey, true);
            EditorApplication.EnterPlaymode();
            Debug.Log("[DemoShowcase] Armed. Entering play mode...");
        }

        static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(ArmedKey, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode) { SessionState.SetBool(ArmedKey, false); StartRecorderAndBot(); }
            else if (state == PlayModeStateChange.ExitingPlayMode) StopRecorder();
        }

        static bool TryEnsureIPhone14Selected()
        {
            try
            {
                var asm = Assembly.Load("Golfin.Physics.Viewer.Bot.Editor");
                var t   = asm?.GetType("Golfin.Physics.Viewer.Editor.GameViewSizeUtil");
                var m   = t?.GetMethod("EnsureIPhone14Selected", BindingFlags.Public | BindingFlags.Static);
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
                if (cw > 0 && ch > 0) { w = Mathf.Max(2,(int)cw); h = Mathf.Max(2,(int)ch); if (w%2!=0) w--; if (h%2!=0) h--; }
            }

            var movie = ScriptableObject.CreateInstance<MovieRecorderSettings>();
            movie.name = "DemoShowcase";
            movie.Enabled = true;
            movie.OutputFormat = MovieRecorderSettings.VideoRecorderOutputFormat.MP4;
            movie.ImageInputSettings = new GameViewInputSettings { OutputWidth = w, OutputHeight = h };
            movie.AudioInputSettings.PreserveAudio = false;
            movie.OutputFile = $"{OutputDir}/{OutputName}";

            var settings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
            settings.AddRecorderSettings(movie);
            settings.SetRecordModeToManual();
            settings.FrameRate = 30;
            settings.FrameRatePlayback = FrameRatePlayback.Variable;

            _recorder = new RecorderController(settings);
            _recorder.PrepareRecording();
            _recorder.StartRecording();
            Debug.Log($"[DemoShowcase] Recording → {OutputDir}/{OutputName}.mp4 ({w}x{h} @ 30fps)");

            var host = new GameObject("[DemoShowcaseBot]");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.AddComponent<DemoShowcaseRunner>().StartDemo();
        }

        static void StopRecorder()
        {
            if (_recorder != null)
            {
                try { if (_recorder.IsRecording()) _recorder.StopRecording(); Debug.Log("[DemoShowcase] Recording stopped."); }
                catch (Exception e) { Debug.LogWarning($"[DemoShowcase] StopRecorder: {e.Message}"); }
                _recorder = null;
            }
        }
    }

    public class DemoShowcaseRunner : MonoBehaviour
    {
        public void StartDemo() => StartCoroutine(Sequence());

        static HoleCardController FindHole1()
            => UnityEngine.Object.FindObjectsOfType<HoleCardController>(true).FirstOrDefault(c => c.HoleNumber == 1);

        static void InvokePrivateButton(HoleCardController card, string field)
        {
            var btn = typeof(HoleCardController).GetField(field, BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(card) as Button;
            btn?.onClick.Invoke();
        }

        IEnumerator Sequence()
        {
            Application.runInBackground = true;

            // Boot: logo → Splash gate ("Play" + Login, no Create Account in the demo).
            yield return new WaitForSecondsRealtime(6.5f);
            // Hold on the Splash gate, then tap Play → Loading → Home.
            yield return new WaitForSecondsRealtime(2.5f);
            var splash = UnityEngine.Object.FindObjectOfType<SplashScreenController>(true);
            if (splash != null) splash.OnStartClicked();
            else Debug.LogWarning("[DemoShowcase] SplashScreenController not found.");

            // Loading → Home: hold on the welcome banner + Olivia + Practice-only carousel.
            yield return new WaitForSecondsRealtime(5f);

            // Open the hole picker (Practice → hole select).
            ScreenManager.Instance?.ShowScreen(ScreenId.HoleSelection);
            yield return new WaitForSecondsRealtime(3.5f); // show Hole 1 unlocked, others locked

            // Tee off Hole 1: expand its card, then hit PLAY.
            var card = FindHole1();
            if (card != null)
            {
                InvokePrivateButton(card, "cardTapButton"); // expand
                yield return new WaitForSecondsRealtime(1.3f);
                InvokePrivateButton(card, "actionButton");  // PLAY → gameplay load
            }
            else Debug.LogWarning("[DemoShowcase] Hole 1 card not found.");

            // Wait for the gameplay scene load (LabScaffold + Hole_01_Geo), then hold on the tee.
            yield return new WaitForSecondsRealtime(11f);
            yield return new WaitForSecondsRealtime(5f);

            Debug.Log("[DemoShowcase] Sequence done — exiting play mode.");
            EditorApplication.ExitPlaymode();
        }
    }
}
#endif
