#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Input;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Golfin.Diagnostics.Runtime;
using Golfin.UI;

namespace Golfin.EditorTools
{
    /// <summary>
    /// Standalone demo-recorder for tap_feedback_fx evidence (iter-3 refresh).
    ///
    /// Records a ~20s MP4 at full iPhone-14 1170x2532 showing:
    ///   (a) taps on SPLASH SCREEN (black bg — maximum contrast for white rings)
    ///   (b) taps on Home/invitational screen (menu)
    ///   (c) taps with modal open (FX above modal)
    ///   (d) multi-touch: two/three simultaneous effects
    ///   (e) taps over LabScaffold (dark navy button panels, 3D physics scene)
    ///
    /// Output:
    ///   tasks/tap_feedback_demo/video/raw.mp4
    ///
    /// Usage: GOLFIN > Tap FX > Record Demo Video
    /// </summary>
    public static class TapFeedbackDemoRecorder
    {
        const string ShellScenePath       = "Assets/Scenes/ShellScene.unity";
        const string LabScaffoldPath      = "Assets/Scenes/Physics/LabScaffold.unity";
        const string OutputDir            = "tasks/tap_feedback_demo/video";

        const string ArmedKey    = "TapFeedbackDemoRecorder.Armed";

        static RecorderController _recorder;

        [InitializeOnLoadMethod]
        static void RegisterHook()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        [MenuItem("GOLFIN/Tap FX/Record Demo Video")]
        public static void LaunchDemo()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[TapFXDemo] Already in play mode — stop first.");
                return;
            }

            // Open ShellScene
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            EditorSceneManager.OpenScene(ShellScenePath);

            Directory.CreateDirectory(OutputDir);
            SessionState.SetBool(ArmedKey, true);

            EditorApplication.EnterPlaymode();
            Debug.Log("[TapFXDemo] Armed. Entering play mode...");
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

        // Try to call GameViewSizeUtil.EnsureIPhone14Selected via reflection so we don't
        // need to import an internal type from a different asmdef.
        static bool TryEnsureIPhone14Selected()
        {
            try
            {
                var asm  = System.Reflection.Assembly.Load("Golfin.Physics.Viewer.Bot.Editor");
                if (asm == null) return false;
                var t    = asm.GetType("Golfin.Physics.Viewer.Editor.GameViewSizeUtil");
                if (t  == null) return false;
                var m    = t.GetMethod("EnsureIPhone14Selected",
                               System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (m  == null) return false;
                return (bool)m.Invoke(null, null);
            }
            catch { return false; }
        }

        static void StartRecorderAndBot()
        {
            // Try to pin Game View to iPhone-14 1170x2532
            bool selected = TryEnsureIPhone14Selected();
            int w = 1170;
            int h = 2532;

            if (!selected)
            {
                PlayModeWindow.GetRenderingResolution(out uint cw, out uint ch);
                if (cw > 0 && ch > 0)
                {
                    w = Mathf.Max(2, (int)cw);
                    h = Mathf.Max(2, (int)ch);
                    if (w % 2 != 0) w--;
                    if (h % 2 != 0) h--;
                    Debug.LogWarning($"[TapFXDemo] Could not pin to iPhone-14 size — recording at {w}x{h}.");
                }
            }

            var movie = ScriptableObject.CreateInstance<MovieRecorderSettings>();
            movie.name         = "TapFXDemo";
            movie.Enabled      = true;
            movie.OutputFormat = MovieRecorderSettings.VideoRecorderOutputFormat.MP4;
            movie.ImageInputSettings = new GameViewInputSettings
            {
                OutputWidth  = w,
                OutputHeight = h,
            };
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

            Debug.Log($"[TapFXDemo] Recording started → {OutputDir}/raw.mp4 ({w}x{h} @ 30fps)");

            // Inject the demo coroutine host
            var host = new GameObject("[TapFXDemoBot]");
            UnityEngine.Object.DontDestroyOnLoad(host);
            var runner = host.AddComponent<TapFXDemoRunner>();
            runner.LabScaffoldPath = LabScaffoldPath;
            runner.StartDemoCoroutine();
        }

        static void StopRecorder()
        {
            if (_recorder != null)
            {
                try
                {
                    if (_recorder.IsRecording())
                        _recorder.StopRecording();
                    Debug.Log("[TapFXDemo] Recording stopped.");
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[TapFXDemo] StopRecorder warning: {e.Message}");
                }
                _recorder = null;
            }
        }
    }

    /// <summary>
    /// Runtime MonoBehaviour that drives the scripted tap sequence coroutine.
    /// Added to a DontDestroyOnLoad host during play mode entry.
    /// </summary>
    public class TapFXDemoRunner : MonoBehaviour
    {
        public string LabScaffoldPath = "Assets/Scenes/Physics/LabScaffold.unity";

        public void StartDemoCoroutine()
        {
            StartCoroutine(DemoSequence());
        }

        IEnumerator DemoSequence()
        {
            // Wait for TapFeedbackController to initialise (Bootstrap runs AfterSceneLoad)
            yield return new WaitForSecondsRealtime(0.5f);

            // ── Phase A: SPLASH SCREEN taps (black bg — max contrast for white rings) ──
            // The app shows a black/logo splash for the first ~2s after load.
            Debug.Log("[TapFXDemo] Phase A: splash screen taps (black background)");
            SpawnTapAt(new Vector2(585f, 1266f));  // center
            yield return new WaitForSecondsRealtime(0.5f);
            SpawnTapAt(new Vector2(200f, 1800f));  // upper-left
            yield return new WaitForSecondsRealtime(0.4f);
            SpawnTapAt(new Vector2(970f, 800f));   // right-center
            yield return new WaitForSecondsRealtime(0.6f);

            // ── Phase B: Multi-touch on splash/early home screen ─────────────────────
            Debug.Log("[TapFXDemo] Phase B: multi-touch simulation");
            // Two simultaneous — fires two independent effects same frame
            SpawnTapAt(new Vector2(250f, 1400f));
            SpawnTapAt(new Vector2(900f, 1400f));
            yield return new WaitForSecondsRealtime(0.5f);

            // Three simultaneous effects
            SpawnTapAt(new Vector2(200f, 1900f));
            SpawnTapAt(new Vector2(585f, 1266f));
            SpawnTapAt(new Vector2(970f, 600f));
            yield return new WaitForSecondsRealtime(0.8f);

            // ── Phase C: Menu/Home screen taps ───────────────────────────────────────
            Debug.Log("[TapFXDemo] Phase C: menu screen taps");
            SpawnTapAt(new Vector2(585f, 1266f));
            yield return new WaitForSecondsRealtime(0.4f);
            SpawnTapAt(new Vector2(300f, 500f));    // bottom nav area
            yield return new WaitForSecondsRealtime(0.4f);
            SpawnTapAt(new Vector2(870f, 1800f));   // upper-right
            yield return new WaitForSecondsRealtime(0.6f);

            // ── Phase D: Modal tap (FX renders above modal — sortingOrder=5000) ──────
            // The ShellScene loads a maintenance modal on startup — tap while visible
            Debug.Log("[TapFXDemo] Phase D: modal tap (FX above modal layer)");
            SpawnTapAt(new Vector2(585f, 1400f));
            yield return new WaitForSecondsRealtime(0.4f);
            SpawnTapAt(new Vector2(200f, 1600f));
            yield return new WaitForSecondsRealtime(0.5f);

            // ── Phase E: Load LabScaffold (3D physics scene, dark navy UI) ───────────
            Debug.Log("[TapFXDemo] Phase E: loading LabScaffold (dark 3D scene)...");
            // Try name first (build settings), then full path
            var op = SceneManager.LoadSceneAsync("LabScaffold", LoadSceneMode.Single);
            if (op == null)
                op = SceneManager.LoadSceneAsync(LabScaffoldPath, LoadSceneMode.Single);
            if (op != null)
            {
                while (!op.isDone)
                    yield return null;
                Debug.Log("[TapFXDemo] LabScaffold 3D scene loaded.");
            }
            else
            {
                Debug.LogWarning("[TapFXDemo] LabScaffold load failed — staying in ShellScene.");
            }

            // Wait for 3D scene to fully render
            yield return new WaitForSecondsRealtime(3.0f);

            // ── Phase F: In-game taps on dark LabScaffold background ─────────────────
            Debug.Log("[TapFXDemo] Phase F: in-game taps (dark 3D background)");
            // Tap at center — over grey sky background
            SpawnTapAt(new Vector2(585f, 1266f));
            yield return new WaitForSecondsRealtime(0.4f);

            // Tap over dark navy button panel (bottom area, y~200-450 in screen coords)
            SpawnTapAt(new Vector2(80f, 350f));     // SPIN button area (dark navy)
            yield return new WaitForSecondsRealtime(0.3f);
            SpawnTapAt(new Vector2(1100f, 350f));   // right button area (dark navy)
            yield return new WaitForSecondsRealtime(0.3f);

            // Multi-touch in-game
            SpawnTapAt(new Vector2(300f, 1400f));
            SpawnTapAt(new Vector2(870f, 900f));
            yield return new WaitForSecondsRealtime(0.5f);

            // Additional taps for visual richness in video
            SpawnTapAt(new Vector2(585f, 1800f));
            yield return new WaitForSecondsRealtime(0.4f);
            SpawnTapAt(new Vector2(200f, 700f));
            SpawnTapAt(new Vector2(970f, 1600f));
            yield return new WaitForSecondsRealtime(1.0f);

            Debug.Log("[TapFXDemo] Sequence done — exiting play mode.");
            EditorApplication.ExitPlaymode();
        }

        /// <summary>
        /// Spawn a tap effect directly via TapFeedbackController's internal SpawnAt.
        /// Uses reflection to call the private SpawnAt method so we don't need to
        /// make it public in the shipping code.
        /// </summary>
        static void SpawnTapAt(Vector2 screenPos)
        {
            var ctrl = UnityEngine.Object.FindFirstObjectByType<TapFeedbackController>();
            if (ctrl == null)
            {
                Debug.LogWarning("[TapFXDemo] TapFeedbackController not found — skipping SpawnAt.");
                return;
            }

            var method = typeof(TapFeedbackController).GetMethod(
                "SpawnAt",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (method == null)
            {
                Debug.LogWarning("[TapFXDemo] SpawnAt method not found on TapFeedbackController.");
                return;
            }
            method.Invoke(ctrl, new object[] { screenPos });
        }
    }
}
#endif
