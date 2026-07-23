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
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using GolfinRedux.UI.HoleSelection;
using Golfin.Gameplay.UI.HUD;
using Golfin.Gameplay.UI.ShotUI;

namespace Golfin.EditorTools
{
    /// <summary>
    /// Demo recorder for in-game gameplay HUD localization (EN⇄JP).
    /// Shows: STRAIGHT/FADE-DRAW toggle + map SHOOT button in both EN and JP.
    ///
    /// Sequence:
    ///   boot → Home → HoleSelection → Hole 1 → wait for hole load →
    ///   (recording starts here) →
    ///   EN: STRAIGHT (2s) → FADE/DRAW (2s) → map SHOOT (2.5s) → close →
    ///   switch to JP: ストレート (2s) → フェード/ドロー (2s) → map 打つ (2.5s) →
    ///   exit.
    ///
    /// Output raw: tasks/gameplay_localization_demo/video/raw.mp4
    /// Final:      Docs/Reports/Media/gameplay_localization_demo.mp4 (after captioning)
    /// Usage: GOLFIN > Localization > Record Gameplay HUD Demo Video
    /// </summary>
    public static class GameplayLocalizationDemoRecorder
    {
        const string ShellScenePath = "Assets/Scenes/ShellScene.unity";
        const string RawOutputDir   = "tasks/gameplay_localization_demo/video";
        const string FinalOutputDir = "Docs/Reports/Media";
        const string ArmedKey       = "GameplayLocalizationDemoRecorder.Armed";

        static RecorderController _recorder;

        [InitializeOnLoadMethod]
        static void RegisterHook()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        [MenuItem("GOLFIN/Localization/Record Gameplay HUD Demo Video")]
        public static void LaunchDemo()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[GameplayLocalizationDemo] Already in play mode — stop first.");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            EditorSceneManager.OpenScene(ShellScenePath);
            Directory.CreateDirectory(RawOutputDir);
            Directory.CreateDirectory(FinalOutputDir);
            SessionState.SetBool(ArmedKey, true);
            EditorApplication.EnterPlaymode();
            Debug.Log("[GameplayLocalizationDemo] Armed. Entering play mode...");
        }

        static void OnPlayModeChanged(PlayModeStateChange state)
        {
            // ArmedKey gates EnteredPlayMode only (to prevent double-spawn on non-demo entries).
            // ExitingPlayMode is gated on _recorder!=null — ArmedKey is already cleared by then.
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                if (!SessionState.GetBool(ArmedKey, false)) return;
                SessionState.SetBool(ArmedKey, false);
                SpawnBotOnly();
            }
            else if (state == PlayModeStateChange.ExitingPlayMode)
            {
                if (_recorder != null) StopRecorderAndCopy();
            }
        }

        static bool TryEnsureIPhone14Selected()
        {
            try
            {
                var asm = Assembly.Load("Golfin.Physics.Viewer.Bot.Editor");
                var t   = asm?.GetType("Golfin.Physics.Viewer.Editor.GameViewSizeUtil");
                var m   = t?.GetMethod("EnsureIPhone14Selected",
                              BindingFlags.Public | BindingFlags.Static);
                return m != null && (bool)m.Invoke(null, null);
            }
            catch { return false; }
        }

        /// <summary>Called at EnteredPlayMode — only spawns the coroutine runner; recording deferred.</summary>
        static void SpawnBotOnly()
        {
            bool selected = TryEnsureIPhone14Selected();
            if (!selected)
                Debug.LogWarning("[GameplayLocalizationDemo] Could not pin iPhone-14 Game View size — will record at current resolution.");

            var host = new GameObject("[GameplayLocalizationDemoBot]");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.AddComponent<GameplayLocalizationDemoRunner>();
            Debug.Log("[GameplayLocalizationDemo] Bot spawned. Waiting for hole load to start recording...");
        }

        /// <summary>
        /// Called by the runner once the hole is loaded and the HUD is stable.
        /// Locks render state and starts the Unity Recorder.
        /// </summary>
        public static void StartRecorder()
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
                    Debug.LogWarning($"[GameplayLocalizationDemo] Recording at {w}x{h} (not iPhone-14).");
                }
            }

            // Lock render state BEFORE StartRecording to avoid Y-flip on first frame (BotVideoRecorder pattern).
            QualitySettings.vSyncCount  = 0;
            Application.targetFrameRate = 30;

            var movie = ScriptableObject.CreateInstance<MovieRecorderSettings>();
            movie.name         = "GameplayLocalizationDemo";
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
            Debug.Log($"[GameplayLocalizationDemo] Recording started → {RawOutputDir}/raw.mp4 ({w}x{h} @ 30fps)");
        }

        static void StopRecorderAndCopy()
        {
            if (_recorder == null) return;
            try
            {
                if (_recorder.IsRecording())
                    _recorder.StopRecording();
                Debug.Log("[GameplayLocalizationDemo] Recording stopped.");
            }
            catch (Exception e) { Debug.LogWarning($"[GameplayLocalizationDemo] StopRecorder: {e.Message}"); }
            _recorder = null;

            string src = Path.GetFullPath($"{RawOutputDir}/raw.mp4");
            if (File.Exists(src))
            {
                string dst = Path.GetFullPath(Path.Combine(FinalOutputDir, "gameplay_localization_demo_raw.mp4"));
                File.Copy(src, dst, overwrite: true);
                Debug.Log($"[GameplayLocalizationDemo] Raw copied to {dst}");
            }
            else
                Debug.LogWarning($"[GameplayLocalizationDemo] raw.mp4 not found at {src} — check recorder output.");
        }
    }

    /// <summary>Runtime coroutine driver for the gameplay localization demo.</summary>
    public class GameplayLocalizationDemoRunner : MonoBehaviour
    {
        void Start() => StartCoroutine(Sequence());

        // ── Helpers ──────────────────────────────────────────────────────────

        static T FindActive<T>() where T : Component
            => Resources.FindObjectsOfTypeAll<T>()
                .FirstOrDefault(c => c != null
                    && !string.IsNullOrEmpty(c.gameObject.scene.name)
                    && c.gameObject.activeInHierarchy);

        static string GetCurrentScreenName()
        {
            try
            {
                foreach (var m in FindObjectsOfType<MonoBehaviour>())
                {
                    if (m.GetType().Name != "ScreenManager") continue;
                    var f = m.GetType().GetField("_currentScreen",
                        BindingFlags.NonPublic | BindingFlags.Instance);
                    return f?.GetValue(m)?.ToString();
                }
            }
            catch { /* */ }
            return null;
        }

        // ── Navigation ───────────────────────────────────────────────────────

        IEnumerator NavigateToHome(float timeout = 60f)
        {
            float elapsed = 0f;
            while (elapsed < timeout)
            {
                string cur = GetCurrentScreenName();
                if (cur == "Home") { Debug.Log("[GameplayLocalizationBot] At Home."); yield break; }
                if (cur == "Splash")
                {
                    // Click the Start / PLAY button on the splash screen
                    var btn = FindButtonByName("StartButton")
                           ?? FindButtonByText("PLAY")
                           ?? FindButtonByText("START");
                    if (btn != null)
                    {
                        btn.onClick.Invoke();
                        Debug.Log($"[GameplayLocalizationBot] Clicked '{btn.gameObject.name}' on Splash.");
                        yield return new WaitForSecondsRealtime(2f);
                        elapsed += 2f;
                        continue;
                    }
                }
                yield return new WaitForSecondsRealtime(0.5f);
                elapsed += 0.5f;
            }
            Debug.LogWarning("[GameplayLocalizationBot] NavigateToHome TIMEOUT");
        }

        IEnumerator ShowHoleSelection()
        {
            // Use ScreenManager.ShowScreen via reflection
            foreach (var mono in FindObjectsOfType<MonoBehaviour>())
            {
                if (mono.GetType().Name != "ScreenManager") continue;
                var asm = mono.GetType().Assembly;
                var sidType = asm.GetType("GolfinRedux.UI.ScreenId");
                if (sidType == null) break;
                var holeSelVal = Enum.Parse(sidType, "HoleSelection");
                // Try ShowScreen(ScreenId, bool) first, then ShowScreen(ScreenId)
                var showMethod = mono.GetType().GetMethod("ShowScreen",
                    new[] { sidType, typeof(bool) })
                    ?? mono.GetType().GetMethod("ShowScreen", new[] { sidType });
                if (showMethod != null)
                {
                    var parms = showMethod.GetParameters().Length == 2
                        ? new object[] { holeSelVal, false }
                        : new object[] { holeSelVal };
                    showMethod.Invoke(mono, parms);
                    Debug.Log("[GameplayLocalizationBot] ShowScreen(HoleSelection) invoked.");
                }
                break;
            }
            yield return new WaitForSecondsRealtime(2.5f);
        }

        IEnumerator ClickHoleOneActionButton()
        {
            // Find HoleCardController with HoleNumber == 1
            var cards = FindObjectsOfType<HoleCardController>(false);
            HoleCardController target = cards.FirstOrDefault(c => c.HoleNumber == 1)
                                     ?? cards.FirstOrDefault();
            if (target == null)
            {
                Debug.LogWarning("[GameplayLocalizationBot] No HoleCardController found — cannot click hole 1.");
                yield break;
            }

            // Access private [SerializeField] actionButton via reflection
            var btnField = typeof(HoleCardController).GetField("actionButton",
                BindingFlags.NonPublic | BindingFlags.Instance);
            var actionBtn = btnField?.GetValue(target) as Button;

            if (actionBtn != null && actionBtn.gameObject.activeInHierarchy)
            {
                actionBtn.onClick.Invoke();
                Debug.Log($"[GameplayLocalizationBot] Clicked actionButton on HoleCard #{target.HoleNumber}.");
            }
            else
            {
                Debug.LogWarning($"[GameplayLocalizationBot] actionButton not active/found on Hole {target.HoleNumber}.");
            }
            yield return new WaitForSecondsRealtime(1.5f);
        }

        IEnumerator WaitForLabScaffold(float timeout = 45f)
        {
            float e = 0f;
            bool found = false;
            while (e < timeout && !found)
            {
                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    var sc = SceneManager.GetSceneAt(i);
                    if (sc.isLoaded && sc.name == "LabScaffold") { found = true; break; }
                }
                if (!found) { yield return new WaitForSecondsRealtime(0.5f); e += 0.5f; }
            }
            Debug.Log($"[GameplayLocalizationBot] LabScaffold ready={found} after {e:F1}s.");
        }

        IEnumerator WaitForHoleGeo(float timeout = 90f)
        {
            float e = 0f;
            bool found = false;
            while (e < timeout && !found)
            {
                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    var sc = SceneManager.GetSceneAt(i);
                    if (sc.isLoaded && sc.name.StartsWith("Hole_") && sc.name.EndsWith("_Geo"))
                    {
                        Debug.Log($"[GameplayLocalizationBot] Hole geo '{sc.name}' loaded after {e:F1}s.");
                        found = true; break;
                    }
                }
                if (!found) { yield return new WaitForSecondsRealtime(0.5f); e += 0.5f; }
            }
            if (!found) Debug.LogWarning("[GameplayLocalizationBot] WaitForHoleGeo TIMEOUT");
        }

        static Button FindButtonByName(string name)
        {
            foreach (var b in FindObjectsOfType<Button>(false))
                if (b.gameObject.name.Equals(name, StringComparison.OrdinalIgnoreCase)) return b;
            return null;
        }

        static Button FindButtonByText(string text)
        {
            string lower = text.ToLowerInvariant();
            foreach (var b in FindObjectsOfType<Button>(false))
                foreach (var t in b.GetComponentsInChildren<TMPro.TMP_Text>(false))
                    if (t.text != null && t.text.ToLowerInvariant().Contains(lower)) return b;
            return null;
        }

        // ── Main coroutine ────────────────────────────────────────────────────

        IEnumerator Sequence()
        {
            // ── Phase 0: Boot settle ──────────────────────────────────────────
            yield return new WaitForSecondsRealtime(5f);
            Debug.Log("[GameplayLocalizationBot] Boot complete.");

            // Set English as baseline so the HUD builds in EN when hole loads
            LocalizationManager.SetLanguage(Language.English);
            ShotModeContext.Reset();

            // ── Phase 1: Navigate to Home ─────────────────────────────────────
            yield return NavigateToHome();
            yield return new WaitForSecondsRealtime(1.5f);

            // ── Phase 2: Open HoleSelection ───────────────────────────────────
            yield return ShowHoleSelection();

            // ── Phase 3: Click Hole 1 action button ──────────────────────────
            yield return ClickHoleOneActionButton();

            // ── Phase 4: Wait for LabScaffold host scene ──────────────────────
            yield return WaitForLabScaffold();

            // ── Phase 5: Wait for Hole geo ────────────────────────────────────
            yield return WaitForHoleGeo();

            // Extra settle so HUD Awake/Start/OnEnable all fire and labels bind
            yield return new WaitForSecondsRealtime(5f);

            // ── Sanity check EN ───────────────────────────────────────────────
            var fdWidget = FindObjectOfType<FadeDrawButtonWidget>(false);
            if (fdWidget == null)
            {
                Debug.LogError("[GameplayLocalizationBot] SANITY FAIL: FadeDrawButtonWidget not found. Aborting.");
                EditorApplication.ExitPlaymode();
                yield break;
            }

            var primaryText = fdWidget.GetComponentsInChildren<TMPro.TMP_Text>(false).FirstOrDefault();
            string enText = primaryText?.text ?? "(null)";
            if (string.IsNullOrEmpty(enText) || enText.StartsWith("GAMEPLAY_"))
            {
                Debug.LogError($"[GameplayLocalizationBot] SANITY FAIL: EN raw key or empty — '{enText}'. LocalizationManager not initialised. Aborting.");
                EditorApplication.ExitPlaymode();
                yield break;
            }
            Debug.Log($"[GameplayLocalizationBot] SANITY PASS EN: FadeDrawButton='{enText}'");

            // Prewarm the MapView RT to avoid Y-flip on first map open
            var mvcPrewarm = FindObjectOfType<MapViewController>(false);
            mvcPrewarm?.PrewarmRT();
            yield return new WaitForEndOfFrame();
            yield return new WaitForSecondsRealtime(0.3f);

            // ── Start recording (deferred — hole is stable) ───────────────────
            GameplayLocalizationDemoRecorder.StartRecorder();
            yield return new WaitForSecondsRealtime(1f); // let recorder latch first frames

            // ── EN section ────────────────────────────────────────────────────
            Debug.Log("[GameplayLocalizationBot] === EN SECTION ===");

            // Show STRAIGHT
            ShotModeContext.Reset();
            yield return new WaitForSecondsRealtime(2f);

            // Toggle to FADE/DRAW
            ShotModeContext.Toggle();
            yield return new WaitForSecondsRealtime(2f);

            // Open map (fires HoleCardWidget.OpenMapView → MapViewController.OpenViaWidget → Open)
            var holeCard = FindObjectOfType<HoleCardWidget>(false);
            if (holeCard != null && holeCard.MapButton != null && holeCard.MapButton.gameObject.activeInHierarchy)
            {
                holeCard.MapButton.onClick.Invoke();
                Debug.Log("[GameplayLocalizationBot] Map opened via HoleCardWidget.MapButton (EN).");
            }
            else
            {
                Debug.LogWarning("[GameplayLocalizationBot] HoleCardWidget/MapButton not found — calling MapViewController.Open() directly.");
                FindObjectOfType<MapViewController>(false)?.Open();
            }
            yield return new WaitForSecondsRealtime(0.5f); // map animation settle

            // Confirm SHOOT button shows EN text
            Debug.Log("[GameplayLocalizationBot] Map open EN — SHOOT button should read 'SHOOT'");
            yield return new WaitForSecondsRealtime(2.5f);

            // Close map
            var mvc = FindObjectOfType<MapViewController>(false);
            if (mvc != null && mvc.IsOpen) mvc.Close();
            Debug.Log("[GameplayLocalizationBot] Map closed (EN).");
            yield return new WaitForSecondsRealtime(1f);

            // ── Switch to Japanese ────────────────────────────────────────────
            Debug.Log("[GameplayLocalizationBot] Switching to Japanese.");
            LocalizationManager.SetLanguage(Language.Japanese);
            // ShotModeContext.Reset() fires OnChanged → FadeDrawButtonWidget.Refresh() → Get() now returns JP
            ShotModeContext.Reset();
            yield return new WaitForSecondsRealtime(0.5f);

            // Sanity check JP
            string jpText = primaryText?.text ?? "(null)";
            if (string.IsNullOrEmpty(jpText) || jpText.StartsWith("GAMEPLAY_"))
            {
                Debug.LogError($"[GameplayLocalizationBot] SANITY FAIL: JP raw key or empty — '{jpText}'. Aborting.");
                EditorApplication.ExitPlaymode();
                yield break;
            }
            Debug.Log($"[GameplayLocalizationBot] SANITY PASS JP: FadeDrawButton='{jpText}'");

            // ── JP section ────────────────────────────────────────────────────
            Debug.Log("[GameplayLocalizationBot] === JP SECTION ===");

            // Show ストレート
            yield return new WaitForSecondsRealtime(2f);

            // Toggle to フェード/ドロー
            ShotModeContext.Toggle();
            yield return new WaitForSecondsRealtime(2f);

            // Open map — ClubButtonWidget.SetShootMode(true) calls Get("GAMEPLAY_SHOOT") → "打つ"
            var holeCard2 = FindObjectOfType<HoleCardWidget>(false);
            if (holeCard2 != null && holeCard2.MapButton != null && holeCard2.MapButton.gameObject.activeInHierarchy)
            {
                holeCard2.MapButton.onClick.Invoke();
                Debug.Log("[GameplayLocalizationBot] Map opened via HoleCardWidget.MapButton (JP).");
            }
            else
            {
                Debug.LogWarning("[GameplayLocalizationBot] HoleCardWidget/MapButton not found (JP) — calling Open() directly.");
                FindObjectOfType<MapViewController>(false)?.Open();
            }
            yield return new WaitForSecondsRealtime(0.5f); // map animation settle

            Debug.Log("[GameplayLocalizationBot] Map open JP — SHOOT button should read '打つ'");
            yield return new WaitForSecondsRealtime(2.5f);

            // Close map
            var mvc2 = FindObjectOfType<MapViewController>(false);
            if (mvc2 != null && mvc2.IsOpen) mvc2.Close();
            Debug.Log("[GameplayLocalizationBot] Map closed (JP). Sequence complete.");
            yield return new WaitForSecondsRealtime(0.5f);

            // Done — exit play mode triggers StopRecorderAndCopy in ExitingPlayMode handler
            EditorApplication.ExitPlaymode();
        }
    }
}
#endif
