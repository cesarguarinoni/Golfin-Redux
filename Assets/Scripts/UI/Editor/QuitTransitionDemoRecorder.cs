#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Input;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using GolfinRedux.UI;
using GolfinRedux.UI.HoleSelection;

namespace Golfin.EditorTools
{
    /// <summary>
    /// Regression harness for the PRACTICE quit transition (gear → QUIT → CONFIRM).
    ///
    /// Drives the REAL player path — Home → PRACTICE PLAY → Hole Selection → hole card PLAY →
    /// gameplay — then quits through the real gear/QUIT/CONFIRM <c>Button.onClick</c> chain and
    /// records the teardown.
    ///
    /// The defect this exists for: unloading Hole_NN_Geo + LabScaffold takes several frames, and
    /// once LabScaffold goes there is nothing left to render, so the player watched the bare shell
    /// scene (empty camera clear, no UI) until ScreenManager finally faded Home in.
    ///
    /// Two deliverables, per PIPELINE_HARDENING §3 (video for a human, JSON for the gate):
    ///   • tasks/quit_transition_demo/video/raw.mp4          — the clip
    ///   • tasks/quit_transition_demo/quit_invariants.json   — per-frame samples from the CONFIRM
    ///     tap until Home is up, plus the verdict. The invariant is deterministic:
    ///
    ///        every frame must have SOMETHING to show —
    ///        gameplay still loaded, OR a shell screen active, OR the black curtain at full alpha.
    ///
    ///     A frame with none of the three IS the empty-scene flash.
    ///
    /// Usage: GOLFIN > Demos > Record Quit Transition
    /// Recorder plumbing mirrors InGameSettingsDemoRecorder (same render-state lock before
    /// StartRecording to avoid the first-frame Y-flip).
    /// </summary>
    public static class QuitTransitionDemoRecorder
    {
        const string ShellScenePath = "Assets/Scenes/ShellScene.unity";
        const string OutputDir      = "tasks/quit_transition_demo";
        const string RawOutputDir   = OutputDir + "/video";
        const string ArmedKey       = "QuitTransitionDemoRecorder.Armed";

        static RecorderController _recorder;
        static double _recordStartRealtime;
        static readonly List<(double t, string text)> _marks = new List<(double, string)>();

        [InitializeOnLoadMethod]
        static void RegisterHook()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        [MenuItem("GOLFIN/Demos/Record Quit Transition")]
        public static void LaunchDemo()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[QuitTransitionDemo] Already in play mode — stop first.");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ShellScenePath);
            Directory.CreateDirectory(RawOutputDir);
            SessionState.SetBool(ArmedKey, true);
            EditorApplication.EnterPlaymode();
            Debug.Log("[QuitTransitionDemo] Armed. Entering play mode...");
        }

        static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                if (!SessionState.GetBool(ArmedKey, false)) return;
                SessionState.SetBool(ArmedKey, false);
                SpawnRunner();
            }
            else if (state == PlayModeStateChange.ExitingPlayMode)
            {
                if (_recorder != null) StopRecorderAndWriteSidecar();
            }
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

        static void SpawnRunner()
        {
            if (!TryEnsureIPhone14Selected())
                Debug.LogWarning("[QuitTransitionDemo] Could not pin iPhone-14 Game View size — will record at current resolution.");

            _marks.Clear();
            var host = new GameObject("[QuitTransitionDemoBot]");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.AddComponent<QuitTransitionDemoRunner>();
            Debug.Log("[QuitTransitionDemo] Runner spawned. Navigating the real player path...");
        }

        public static void StartRecorder()
        {
            bool pinned = TryEnsureIPhone14Selected();
            int w = 1170, h = 2532;
            if (!pinned)
            {
                PlayModeWindow.GetRenderingResolution(out uint cw, out uint ch);
                if (cw > 0 && ch > 0)
                {
                    w = Mathf.Max(2, (int)cw); h = Mathf.Max(2, (int)ch);
                    if (w % 2 != 0) w--; if (h % 2 != 0) h--;
                    Debug.LogWarning($"[QuitTransitionDemo] Recording at {w}x{h} (not iPhone-14).");
                }
            }

            // Lock render state BEFORE StartRecording — the Y-flip guard.
            QualitySettings.vSyncCount  = 0;
            Application.targetFrameRate = 30;

            var movie = ScriptableObject.CreateInstance<MovieRecorderSettings>();
            movie.name         = "QuitTransitionDemo";
            movie.Enabled      = true;
            movie.OutputFormat = MovieRecorderSettings.VideoRecorderOutputFormat.MP4;
            movie.ImageInputSettings = new GameViewInputSettings { OutputWidth = w, OutputHeight = h };
            movie.AudioInputSettings.PreserveAudio = false;
            movie.OutputFile = $"{RawOutputDir}/raw";

            var settings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
            settings.AddRecorderSettings(movie);
            settings.SetRecordModeToManual();
            settings.FrameRate = 30;
            settings.FrameRatePlayback = FrameRatePlayback.Variable;

            _recorder = new RecorderController(settings);
            _recorder.PrepareRecording();
            _recorder.StartRecording();
            _recordStartRealtime = Time.realtimeSinceStartupAsDouble;
            Debug.Log($"[QuitTransitionDemo] Recording started → {RawOutputDir}/raw.mp4 ({w}x{h} @ 30fps)");
        }

        public static void Mark(string text)
        {
            _marks.Add((Time.realtimeSinceStartupAsDouble, text));
            Debug.Log($"[QuitTransitionDemo] MARK t+{Time.realtimeSinceStartupAsDouble - _recordStartRealtime:F2}s :: {text}");
        }

        /// <summary>Writes the per-frame invariant dump. Called by the runner once Home is up.</summary>
        public static void WriteInvariants(List<QuitTransitionDemoRunner.Sample> samples)
        {
            try
            {
                Directory.CreateDirectory(OutputDir);

                var offenders = samples.Where(s => !s.Covered).ToList();
                var sb = new System.Text.StringBuilder();
                sb.Append("{\n");
                sb.Append("  \"assertion\": \"every frame from CONFIRM to Home shows gameplay, a shell screen, or a full-alpha curtain\",\n");
                sb.Append("  \"verdict\": \"").Append(offenders.Count == 0 ? "PASS" : "FAIL").Append("\",\n");
                sb.Append("  \"frames\": ").Append(samples.Count).Append(",\n");
                sb.Append("  \"uncovered_frames\": ").Append(offenders.Count).Append(",\n");
                sb.Append("  \"samples\": [\n");
                for (int i = 0; i < samples.Count; i++)
                {
                    var s = samples[i];
                    sb.Append("    {\"t\": ").Append(s.T.ToString("F3"))
                      .Append(", \"fadeAlpha\": ").Append(s.FadeAlpha.ToString("F3"))
                      .Append(", \"curtainOrder\": ").Append(s.CurtainOrder)
                      .Append(", \"labLoaded\": ").Append(s.LabLoaded ? "true" : "false")
                      .Append(", \"geoLoaded\": ").Append(s.GeoLoaded ? "true" : "false")
                      .Append(", \"activeScreens\": ").Append(s.ActiveScreens)
                      .Append(", \"screen\": \"").Append(s.Screen)
                      .Append("\", \"covered\": ").Append(s.Covered ? "true" : "false").Append('}');
                    if (i < samples.Count - 1) sb.Append(',');
                    sb.Append('\n');
                }
                sb.Append("  ]\n}\n");
                File.WriteAllText($"{OutputDir}/quit_invariants.json", sb.ToString());
                Debug.Log($"[QuitTransitionDemo] Invariants: {(offenders.Count == 0 ? "PASS" : "FAIL")} " +
                          $"({offenders.Count}/{samples.Count} uncovered frames) → {OutputDir}/quit_invariants.json");
            }
            catch (Exception e) { Debug.LogWarning($"[QuitTransitionDemo] invariant write failed: {e.Message}"); }
        }

        static void StopRecorderAndWriteSidecar()
        {
            if (_recorder == null) return;
            try
            {
                if (_recorder.IsRecording()) _recorder.StopRecording();
                Debug.Log("[QuitTransitionDemo] Recording stopped.");
            }
            catch (Exception e) { Debug.LogWarning($"[QuitTransitionDemo] StopRecorder: {e.Message}"); }
            _recorder = null;

            try
            {
                Directory.CreateDirectory(RawOutputDir);
                var sb = new System.Text.StringBuilder();
                sb.Append("{\n  \"record_start_realtime\": ").Append(_recordStartRealtime.ToString("F4"))
                  .Append(",\n  \"fps\": 30,\n  \"marks\": [\n");
                for (int i = 0; i < _marks.Count; i++)
                {
                    double rel = _marks[i].t - _recordStartRealtime;
                    sb.Append("    {\"t\": ").Append(rel.ToString("F3"))
                      .Append(", \"text\": \"").Append(_marks[i].text.Replace("\"", "\\\"")).Append("\"}");
                    if (i < _marks.Count - 1) sb.Append(',');
                    sb.Append('\n');
                }
                sb.Append("  ]\n}\n");
                File.WriteAllText($"{RawOutputDir}/record_info.json", sb.ToString());
            }
            catch (Exception e) { Debug.LogWarning($"[QuitTransitionDemo] sidecar write failed: {e.Message}"); }
        }
    }

    /// <summary>Runtime driver. Every interaction goes through a real widget's onClick.</summary>
    public class QuitTransitionDemoRunner : MonoBehaviour
    {
        public struct Sample
        {
            public double T;
            public float  FadeAlpha;
            public int    CurtainOrder;
            public bool   LabLoaded;
            public bool   GeoLoaded;
            public int    ActiveScreens;
            public string Screen;
            public bool   Covered;
        }

        void Start() => StartCoroutine(Sequence());

        static string CurrentScreen()
        {
            var sm = ScreenManager.Instance;
            return sm != null ? sm.CurrentScreen.ToString() : null;
        }

        static Button ActiveButtonNamed(string name)
            => UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .FirstOrDefault(b => b.gameObject.name == name && b.gameObject.activeInHierarchy);

        static Golfin.UI.Modals.InGameSettingsModalController Modal()
            => UnityEngine.Object.FindObjectsByType<Golfin.UI.Modals.InGameSettingsModalController>(
                   FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();

        static Button Child(Transform root, string path) => root.Find(path)?.GetComponent<Button>();

        IEnumerator WaitForScreen(string name, float timeout)
        {
            float e = 0f;
            while (e < timeout && CurrentScreen() != name) { yield return new WaitForSecondsRealtime(0.25f); e += 0.25f; }
            if (CurrentScreen() != name) Debug.LogWarning($"[QuitTransitionDemoBot] TIMEOUT waiting for screen '{name}' (at '{CurrentScreen()}')");
        }

        IEnumerator WaitForHoleLoaded(float timeout)
        {
            float e = 0f;
            while (e < timeout)
            {
                if (SceneLoaded("LabScaffold") && GeoLoaded()) { Debug.Log($"[QuitTransitionDemoBot] Hole loaded after {e:F1}s."); yield break; }
                yield return new WaitForSecondsRealtime(0.5f); e += 0.5f;
            }
            Debug.LogWarning("[QuitTransitionDemoBot] TIMEOUT waiting for hole load");
        }

        static bool SceneLoaded(string name)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var sc = SceneManager.GetSceneAt(i);
                if (sc.isLoaded && sc.name == name) return true;
            }
            return false;
        }

        static bool GeoLoaded()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var sc = SceneManager.GetSceneAt(i);
                if (sc.isLoaded && sc.name.StartsWith("Hole_") && sc.name.EndsWith("_Geo")) return true;
            }
            return false;
        }

        static Transform _screensRoot;
        static int ActiveScreenCount()
        {
            if (_screensRoot == null)
            {
                var go = GameObject.Find("Canvas/ScreensRoot");
                if (go != null) _screensRoot = go.transform;
            }
            if (_screensRoot == null) return -1;
            int n = 0;
            for (int i = 0; i < _screensRoot.childCount; i++)
                if (_screensRoot.GetChild(i).gameObject.activeSelf) n++;
            return n;
        }

        static CanvasGroup _fadeGroup;
        static Canvas      _fadeCanvas;
        static void ResolveFade()
        {
            var fc = FadeController.Instance;
            if (fc == null) return;
            if (_fadeGroup == null) _fadeGroup = fc.GetComponent<CanvasGroup>();
            // Re-resolved every frame on purpose: FadeController adds the sorting Canvas
            // lazily when the curtain first goes up, so a one-shot lookup would cache null
            // and report curtainOrder=0 for the entire transition.
            if (_fadeCanvas == null) _fadeCanvas = fc.GetComponent<Canvas>();
        }

        /// <summary>
        /// Samples every frame from the CONFIRM tap until Home is active + settled, then dumps the
        /// invariant JSON. Hosted on this DontDestroyOnLoad bot, so the gameplay unload can't kill it.
        /// </summary>
        IEnumerator MonitorTransition(double t0)
        {
            var samples = new List<Sample>();
            float homeHeld = 0f;

            while (homeHeld < 1.0f && samples.Count < 2000)
            {
                yield return null;
                ResolveFade();

                bool lab  = SceneLoaded("LabScaffold");
                bool geo  = GeoLoaded();
                int  live = ActiveScreenCount();
                float a   = _fadeGroup != null ? _fadeGroup.alpha : 0f;
                bool onHome = CurrentScreen() == "Home" && live > 0;

                var s = new Sample
                {
                    T             = Time.realtimeSinceStartupAsDouble - t0,
                    FadeAlpha     = a,
                    CurtainOrder  = (_fadeCanvas != null && _fadeCanvas.overrideSorting) ? _fadeCanvas.sortingOrder : 0,
                    LabLoaded     = lab,
                    GeoLoaded     = geo,
                    ActiveScreens = live,
                    Screen        = CurrentScreen(),
                    // "Something is on screen": gameplay still rendering, a shell screen up, or
                    // the curtain fully opaque. Anything else is the empty-scene flash.
                    Covered       = lab || live > 0 || a >= 0.999f,
                };
                samples.Add(s);

                if (onHome) homeHeld += Time.unscaledDeltaTime; else homeHeld = 0f;
            }

            QuitTransitionDemoRecorder.WriteInvariants(samples);
        }

        IEnumerator Sequence()
        {
            yield return new WaitForSecondsRealtime(6f);
            LocalizationManager.SetLanguage(Language.English);

            if (CurrentScreen() == "Splash")
            {
                ActiveButtonNamed("StartButton")?.onClick.Invoke();
                yield return new WaitForSecondsRealtime(3f);
            }
            // Fresh save: the splash routes to the starter-character picker before Home.
            // Clear it through the real widgets (SelectButton -> ConfirmButton), same as a
            // first-run player would; on a save that already has a starter this is skipped.
            if (CurrentScreen() == "StartingCharacterSelection")
            {
                yield return new WaitForSecondsRealtime(2.5f);
                ActiveButtonNamed("SelectButton")?.onClick.Invoke();
                yield return new WaitForSecondsRealtime(2f);
                ActiveButtonNamed("ConfirmButton")?.onClick.Invoke();
                Debug.Log("[QuitTransitionDemoBot] Starter character chosen.");
                yield return new WaitForSecondsRealtime(2f);
            }

            yield return WaitForScreen("Home", 45f);
            yield return new WaitForSecondsRealtime(1.5f);

            var practicePlay = ActiveButtonNamed("PlayButton");
            if (practicePlay == null) { Debug.LogError("[QuitTransitionDemoBot] No active mode-card PlayButton at Home. Aborting."); EditorApplication.ExitPlaymode(); yield break; }
            practicePlay.onClick.Invoke();
            Debug.Log("[QuitTransitionDemoBot] Clicked PRACTICE > PLAY.");
            yield return WaitForScreen("HoleSelection", 30f);
            yield return new WaitForSecondsRealtime(2f);

            var cards = UnityEngine.Object.FindObjectsByType<HoleCardController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            var target = cards.FirstOrDefault(c => c.State == HoleCardState.Expanded)
                      ?? cards.OrderBy(c => c.HoleNumber).FirstOrDefault(c => c.State != HoleCardState.Locked);
            if (target == null) { Debug.LogError("[QuitTransitionDemoBot] No playable hole card. Aborting."); EditorApplication.ExitPlaymode(); yield break; }
            if (target.State != HoleCardState.Expanded)
            {
                var tap = typeof(HoleCardController).GetField("cardTapButton", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(target) as Button;
                tap?.onClick.Invoke();
                yield return new WaitForSecondsRealtime(1.5f);
            }
            var action = typeof(HoleCardController).GetField("actionButton", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(target) as Button;
            if (action == null) { Debug.LogError("[QuitTransitionDemoBot] Hole card actionButton missing. Aborting."); EditorApplication.ExitPlaymode(); yield break; }
            action.onClick.Invoke();
            Debug.Log($"[QuitTransitionDemoBot] Clicked PLAY on hole {target.HoleNumber}.");

            yield return WaitForHoleLoaded(120f);
            yield return new WaitForSecondsRealtime(6f);   // HUD Awake/Start/OnEnable + tee settle

            var modal = Modal();
            var gear  = GameObject.Find("LabRoot/ShotUI_Canvas/SettingsButton")?.GetComponent<Button>();
            if (modal == null || gear == null) { Debug.LogError("[QuitTransitionDemoBot] SANITY FAIL: modal or gear missing. Aborting."); EditorApplication.ExitPlaymode(); yield break; }
            var t = modal.transform;

            QuitTransitionDemoRecorder.StartRecorder();
            yield return new WaitForSecondsRealtime(1f);

            QuitTransitionDemoRecorder.Mark("Practice round in play");
            yield return new WaitForSecondsRealtime(2f);

            gear.onClick.Invoke();
            QuitTransitionDemoRecorder.Mark("Gear opens the in-game settings");
            yield return new WaitForSecondsRealtime(2.5f);

            Child(t, "Panel/PlayingCard/ButtonsRow/QuitButton")?.onClick.Invoke();
            QuitTransitionDemoRecorder.Mark("QUIT");
            yield return new WaitForSecondsRealtime(2f);

            double t0 = Time.realtimeSinceStartupAsDouble;
            StartCoroutine(MonitorTransition(t0));
            Child(t, "ConfirmDialog/ConfirmCard/ButtonsRow/ConfirmQuitButton")?.onClick.Invoke();
            QuitTransitionDemoRecorder.Mark("CONFIRM — teardown runs behind the curtain");
            yield return new WaitForSecondsRealtime(6f);

            QuitTransitionDemoRecorder.Mark("Home, with no empty frame in between");
            yield return new WaitForSecondsRealtime(3f);

            Debug.Log("[QuitTransitionDemoBot] Sequence complete — exiting play mode.");
            EditorApplication.ExitPlaymode();
        }
    }
}
#endif
