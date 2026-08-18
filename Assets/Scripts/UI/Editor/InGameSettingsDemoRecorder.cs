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
using GolfinRedux.UI.HoleSelection;

namespace Golfin.EditorTools
{
    /// <summary>
    /// Demo recorder for the in-game settings overlay (`ingame_settings_modal`).
    ///
    /// Drives the REAL player path end to end — Home → PRACTICE PLAY → Hole Selection →
    /// hole card PLAY → matchmaking → gameplay — then exercises the overlay through the
    /// real gear <c>Button.onClick</c>:
    ///
    ///   gear opens settings (NOT the old GreenTuningPanel cheat) → drag SOUND → drag MUSIC →
    ///   BACK → gear re-opens with the values persisted → JP → QUIT → confirm → BACK →
    ///   EN → QUIT → CONFIRM → gameplay tears down and lands on Home.
    ///
    /// Recording starts once the hole is stable, so the load screens are not in the clip.
    ///
    /// Output raw: tasks/ingame_settings_demo/video/raw.mp4  (+ record_info.json caption marks)
    /// Final:      Docs/Specs/Active/ingame_settings_modal/videos/ (after the caption pass)
    /// Usage:      GOLFIN > Demos > Record In-Game Settings Demo Video
    ///
    /// Pattern lifted from GameplayLocalizationDemoRecorder (same render-state lock before
    /// StartRecording to avoid the first-frame Y-flip, same deferred-start structure).
    /// </summary>
    public static class InGameSettingsDemoRecorder
    {
        const string ShellScenePath = "Assets/Scenes/ShellScene.unity";
        const string RawOutputDir   = "tasks/ingame_settings_demo/video";
        const string ArmedKey       = "InGameSettingsDemoRecorder.Armed";

        static RecorderController _recorder;
        static double _recordStartRealtime;
        static readonly List<(double t, string text)> _marks = new List<(double, string)>();

        [InitializeOnLoadMethod]
        static void RegisterHook()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        [MenuItem("GOLFIN/Demos/Record In-Game Settings Demo Video")]
        public static void LaunchDemo()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[InGameSettingsDemo] Already in play mode — stop first.");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ShellScenePath);
            Directory.CreateDirectory(RawOutputDir);
            SessionState.SetBool(ArmedKey, true);
            EditorApplication.EnterPlaymode();
            Debug.Log("[InGameSettingsDemo] Armed. Entering play mode...");
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
                Debug.LogWarning("[InGameSettingsDemo] Could not pin iPhone-14 Game View size — will record at current resolution.");

            _marks.Clear();
            var host = new GameObject("[InGameSettingsDemoBot]");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.AddComponent<InGameSettingsDemoRunner>();
            Debug.Log("[InGameSettingsDemo] Runner spawned. Navigating the real player path...");
        }

        /// <summary>Called by the runner once the hole is loaded and the HUD is stable.</summary>
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
                    Debug.LogWarning($"[InGameSettingsDemo] Recording at {w}x{h} (not iPhone-14).");
                }
            }

            // Lock render state BEFORE StartRecording — BotVideoRecorder's Y-flip guard.
            QualitySettings.vSyncCount  = 0;
            Application.targetFrameRate = 30;

            var movie = ScriptableObject.CreateInstance<MovieRecorderSettings>();
            movie.name         = "InGameSettingsDemo";
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
            Debug.Log($"[InGameSettingsDemo] Recording started → {RawOutputDir}/raw.mp4 ({w}x{h} @ 30fps)");
        }

        /// <summary>Stamp a caption mark on the recorder clock.</summary>
        public static void Mark(string text)
        {
            _marks.Add((Time.realtimeSinceStartupAsDouble, text));
            Debug.Log($"[InGameSettingsDemo] MARK t+{Time.realtimeSinceStartupAsDouble - _recordStartRealtime:F2}s :: {text}");
        }

        static void StopRecorderAndWriteSidecar()
        {
            if (_recorder == null) return;
            try
            {
                if (_recorder.IsRecording()) _recorder.StopRecording();
                Debug.Log("[InGameSettingsDemo] Recording stopped.");
            }
            catch (Exception e) { Debug.LogWarning($"[InGameSettingsDemo] StopRecorder: {e.Message}"); }
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
                Debug.Log($"[InGameSettingsDemo] Wrote {_marks.Count} caption marks → {RawOutputDir}/record_info.json");
            }
            catch (Exception e) { Debug.LogWarning($"[InGameSettingsDemo] sidecar write failed: {e.Message}"); }
        }
    }

    /// <summary>Runtime coroutine driver. Every interaction goes through a real widget's onClick.</summary>
    public class InGameSettingsDemoRunner : MonoBehaviour
    {
        void Start() => StartCoroutine(Sequence());

        // ── Reflection-free-ish helpers ──────────────────────────────────────

        static string CurrentScreen()
        {
            var sm = GolfinRedux.UI.ScreenManager.Instance;
            return sm != null ? sm.CurrentScreen.ToString() : null;
        }

        static Button ActiveButtonNamed(string name)
            => UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .FirstOrDefault(b => b.gameObject.name == name && b.gameObject.activeInHierarchy);

        static Golfin.UI.Modals.InGameSettingsModalController Modal()
            => UnityEngine.Object.FindObjectsByType<Golfin.UI.Modals.InGameSettingsModalController>(
                   FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();

        static Button Child(Transform root, string path) => root.Find(path)?.GetComponent<Button>();

        /// <summary>Sweep a slider like a drag — fires onValueChanged every frame, exactly as a real drag does.</summary>
        IEnumerator Sweep(Slider s, float to, float seconds)
        {
            if (s == null) yield break;
            float from = s.value, e = 0f;
            while (e < seconds)
            {
                e += Time.unscaledDeltaTime;
                s.value = Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(e / seconds)));
                yield return null;
            }
            s.value = to;
        }

        IEnumerator WaitForScreen(string name, float timeout)
        {
            float e = 0f;
            while (e < timeout && CurrentScreen() != name) { yield return new WaitForSecondsRealtime(0.25f); e += 0.25f; }
            if (CurrentScreen() != name) Debug.LogWarning($"[InGameSettingsDemoBot] TIMEOUT waiting for screen '{name}' (at '{CurrentScreen()}')");
        }

        IEnumerator WaitForHoleLoaded(float timeout)
        {
            float e = 0f;
            while (e < timeout)
            {
                bool lab = false, geo = false;
                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    var sc = SceneManager.GetSceneAt(i);
                    if (!sc.isLoaded) continue;
                    if (sc.name == "LabScaffold") lab = true;
                    if (sc.name.StartsWith("Hole_") && sc.name.EndsWith("_Geo")) geo = true;
                }
                if (lab && geo) { Debug.Log($"[InGameSettingsDemoBot] Hole loaded after {e:F1}s."); yield break; }
                yield return new WaitForSecondsRealtime(0.5f); e += 0.5f;
            }
            Debug.LogWarning("[InGameSettingsDemoBot] TIMEOUT waiting for hole load");
        }

        // ── Sequence ─────────────────────────────────────────────────────────

        IEnumerator Sequence()
        {
            yield return new WaitForSecondsRealtime(6f);   // boot settle
            LocalizationManager.SetLanguage(Language.English);

            // ── Real player path in ──────────────────────────────────────────
            // Splash/login gate (skipped entirely when the session is already authenticated).
            if (CurrentScreen() == "Splash")
            {
                ActiveButtonNamed("StartButton")?.onClick.Invoke();
                yield return new WaitForSecondsRealtime(3f);
            }
            yield return WaitForScreen("Home", 45f);
            yield return new WaitForSecondsRealtime(1.5f);

            var practicePlay = ActiveButtonNamed("PlayButton");
            if (practicePlay == null) { Debug.LogError("[InGameSettingsDemoBot] No active mode-card PlayButton at Home. Aborting."); EditorApplication.ExitPlaymode(); yield break; }
            practicePlay.onClick.Invoke();
            Debug.Log("[InGameSettingsDemoBot] Clicked PRACTICE > PLAY.");
            yield return WaitForScreen("HoleSelection", 30f);
            yield return new WaitForSecondsRealtime(2f);

            var cards = UnityEngine.Object.FindObjectsByType<HoleCardController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            var target = cards.FirstOrDefault(c => c.State == HoleCardState.Expanded)
                      ?? cards.OrderBy(c => c.HoleNumber).FirstOrDefault(c => c.State != HoleCardState.Locked);
            if (target == null) { Debug.LogError("[InGameSettingsDemoBot] No playable hole card. Aborting."); EditorApplication.ExitPlaymode(); yield break; }
            if (target.State != HoleCardState.Expanded)
            {
                var tap = typeof(HoleCardController).GetField("cardTapButton", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(target) as Button;
                tap?.onClick.Invoke();
                yield return new WaitForSecondsRealtime(1.5f);
            }
            var action = typeof(HoleCardController).GetField("actionButton", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(target) as Button;
            if (action == null) { Debug.LogError("[InGameSettingsDemoBot] Hole card actionButton missing. Aborting."); EditorApplication.ExitPlaymode(); yield break; }
            action.onClick.Invoke();
            Debug.Log($"[InGameSettingsDemoBot] Clicked PLAY on hole {target.HoleNumber}.");

            yield return WaitForHoleLoaded(120f);
            yield return new WaitForSecondsRealtime(6f);   // HUD Awake/Start/OnEnable + tee settle

            var modal = Modal();
            var gear  = GameObject.Find("LabRoot/ShotUI_Canvas/SettingsButton")?.GetComponent<Button>();
            if (modal == null || gear == null) { Debug.LogError("[InGameSettingsDemoBot] SANITY FAIL: modal or gear missing. Aborting."); EditorApplication.ExitPlaymode(); yield break; }
            var t = modal.transform;
            var sfx   = t.Find("Panel/SoundCard/SfxSlider").GetComponent<Slider>();
            var music = t.Find("Panel/SoundCard/MusicSlider").GetComponent<Slider>();

            // ── Record ───────────────────────────────────────────────────────
            InGameSettingsDemoRecorder.StartRecorder();
            yield return new WaitForSecondsRealtime(1f);

            InGameSettingsDemoRecorder.Mark("Gameplay HUD — the gear used to open a debug cheat panel");
            yield return new WaitForSecondsRealtime(2.5f);

            gear.onClick.Invoke();
            InGameSettingsDemoRecorder.Mark("Tap the gear: the new In-Game Settings overlay");
            yield return new WaitForSecondsRealtime(3.5f);

            InGameSettingsDemoRecorder.Mark("SOUND slider drives SFX volume live");
            yield return Sweep(sfx, 0.12f, 1.6f);
            yield return new WaitForSecondsRealtime(0.6f);
            yield return Sweep(sfx, 0.85f, 1.6f);
            yield return new WaitForSecondsRealtime(0.8f);

            InGameSettingsDemoRecorder.Mark("MUSIC slider drives music volume live");
            yield return Sweep(music, 0.15f, 1.6f);
            yield return new WaitForSecondsRealtime(0.6f);
            yield return Sweep(music, 0.55f, 1.6f);
            yield return new WaitForSecondsRealtime(0.8f);

            Child(t, "Panel/PlayingCard/ButtonsRow/BackButton")?.onClick.Invoke();
            InGameSettingsDemoRecorder.Mark("BACK closes it — gameplay carries on untouched");
            yield return new WaitForSecondsRealtime(2.5f);

            gear.onClick.Invoke();
            InGameSettingsDemoRecorder.Mark("Re-open: the volumes persisted via AudioManager");
            yield return new WaitForSecondsRealtime(3f);

            // PLAYING card is bound to the live hole, not the Figma mock-up
            InGameSettingsDemoRecorder.Mark("PLAYING card = the live hole: course, par, map,\nstrategy text and that hole's real rewards");
            yield return new WaitForSecondsRealtime(3.5f);

            // ── Japanese ─────────────────────────────────────────────────────
            LocalizationManager.SetLanguage(Language.Japanese);
            modal.Hide(); yield return new WaitForSecondsRealtime(0.35f); modal.Show();
            InGameSettingsDemoRecorder.Mark("Japanese");
            yield return new WaitForSecondsRealtime(3.5f);

            Child(t, "Panel/PlayingCard/ButtonsRow/QuitButton")?.onClick.Invoke();
            InGameSettingsDemoRecorder.Mark("QUIT is confirm-gated — no rewards if you leave");
            yield return new WaitForSecondsRealtime(3.5f);

            Child(t, "ConfirmDialog/ConfirmCard/ButtonsRow/ConfirmBackButton")?.onClick.Invoke();
            InGameSettingsDemoRecorder.Mark("BACK on the confirm returns to settings");
            yield return new WaitForSecondsRealtime(2f);

            // ── Back to English for the quit ─────────────────────────────────
            LocalizationManager.SetLanguage(Language.English);
            modal.Hide(); yield return new WaitForSecondsRealtime(0.35f); modal.Show();
            yield return new WaitForSecondsRealtime(1.5f);

            Child(t, "Panel/PlayingCard/ButtonsRow/QuitButton")?.onClick.Invoke();
            yield return new WaitForSecondsRealtime(2.5f);

            Child(t, "ConfirmDialog/ConfirmCard/ButtonsRow/ConfirmQuitButton")?.onClick.Invoke();
            InGameSettingsDemoRecorder.Mark("CONFIRM: round discarded, gameplay torn down");
            yield return new WaitForSecondsRealtime(4.5f);

            InGameSettingsDemoRecorder.Mark("Lands back on Home, clean — ready to start another round");
            yield return new WaitForSecondsRealtime(4.5f);

            Debug.Log("[InGameSettingsDemoBot] Sequence complete — exiting play mode.");
            EditorApplication.ExitPlaymode();
        }
    }
}
#endif
