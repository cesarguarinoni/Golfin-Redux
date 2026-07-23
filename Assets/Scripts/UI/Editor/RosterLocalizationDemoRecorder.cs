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
using Golfin.Roster;

namespace Golfin.EditorTools
{
    /// <summary>
    /// Demo recorder for the Roster bio localization feature.
    /// Produces a single ~20s MP4 at full iPhone-14 1170x2532.
    ///
    /// Sequence:
    ///   boot → Home → Roster (EN) → Elizabeth (long bio, hold 2.5s) →
    ///   James (short bio, hold 1.5s) → switch to JP →
    ///   Elizabeth (JP bio fits above divider, hold 2.5s) →
    ///   Shae (long JP bio, hold 2.5s) → switch back to EN →
    ///   Elizabeth (EN bookend, hold 1.5s) → exit.
    ///
    /// Output: Docs/Specs/Active/localize_shellscene/videos/raw.mp4
    /// Usage:  GOLFIN > Localization > Record Roster Demo Video
    /// </summary>
    public static class RosterLocalizationDemoRecorder
    {
        const string ShellScenePath = "Assets/Scenes/ShellScene.unity";
        const string OutputDir      = "Docs/Specs/Active/localize_shellscene/videos";
        const string ArmedKey       = "RosterLocalizationDemoRecorder.Armed";

        static RecorderController _recorder;

        [InitializeOnLoadMethod]
        static void RegisterHook()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        [MenuItem("GOLFIN/Localization/Record Roster Demo Video")]
        public static void LaunchDemo()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[RosterLocalizationDemo] Already in play mode — stop first.");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            EditorSceneManager.OpenScene(ShellScenePath);
            Directory.CreateDirectory(OutputDir);
            SessionState.SetBool(ArmedKey, true);
            EditorApplication.EnterPlaymode();
            Debug.Log("[RosterLocalizationDemo] Armed. Entering play mode...");
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
                    Debug.LogWarning($"[RosterLocalizationDemo] Could not pin iPhone-14 — recording at {w}x{h}.");
                }
            }

            var movie = ScriptableObject.CreateInstance<MovieRecorderSettings>();
            movie.name         = "RosterLocalizationDemo";
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
            Debug.Log($"[RosterLocalizationDemo] Recording → {OutputDir}/raw.mp4 ({w}x{h} @ 30fps)");

            var host = new GameObject("[RosterLocalizationDemoBot]");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.AddComponent<RosterLocalizationDemoRunner>().StartDemo();
        }

        static void StopRecorder()
        {
            if (_recorder != null)
            {
                try
                {
                    if (_recorder.IsRecording())
                        _recorder.StopRecording();
                    Debug.Log("[RosterLocalizationDemo] Recording stopped.");
                }
                catch (Exception e) { Debug.LogWarning($"[RosterLocalizationDemo] StopRecorder: {e.Message}"); }
                _recorder = null;
            }
        }
    }

    public class RosterLocalizationDemoRunner : MonoBehaviour
    {
        public void StartDemo() => StartCoroutine(Sequence());

        static T FindActive<T>() where T : Component
        {
            return Resources.FindObjectsOfTypeAll<T>()
                .FirstOrDefault(c => c != null
                    && !string.IsNullOrEmpty(c.gameObject.scene.name)
                    && c.gameObject.activeInHierarchy);
        }

        static void SelectChar(CarouselController carousel, string characterId)
        {
            if (carousel != null)
            {
                carousel.SelectCharacter(characterId);
                Debug.Log($"[RosterLocalizationBot] Selected '{characterId}' via CarouselController.");
            }
            else
            {
                CharacterManager.Instance?.SelectCharacter(characterId);
                Debug.LogWarning($"[RosterLocalizationBot] CarouselController not found — fallback CharacterManager.SelectCharacter('{characterId}').");
            }
        }

        IEnumerator Sequence()
        {
            // ── Phase 0: Boot ──────────────────────────────────────────────────
            // ShellScene takes ~4-5s through Logo → Splash → Loading → Home
            yield return new WaitForSecondsRealtime(5.0f);
            Debug.Log("[RosterLocalizationBot] Boot complete.");

            // Ensure we start in English for a clean demo
            LocalizationManager.SetLanguage(Language.English);

            // ── Phase 1: Navigate to Roster ────────────────────────────────────
            var puim = FindActive<Golfin.UI.PersistentUIManager>();
            if (puim != null && puim.charactersButton != null)
            {
                Debug.Log("[RosterLocalizationBot] Tapping Characters nav button (real entry).");
                puim.charactersButton.onClick.Invoke();
            }
            else
            {
                Debug.LogWarning("[RosterLocalizationBot] charactersButton not found — fallback ShowScreen(Roster).");
                GolfinRedux.UI.ScreenManager.Instance?.ShowScreen(GolfinRedux.UI.ScreenId.Roster);
            }
            yield return new WaitForSecondsRealtime(1.8f); // fade-in + panel bind

            // ── Phase 2: ENGLISH — Elizabeth (long bio) ───────────────────────
            var carousel = FindActive<CarouselController>();
            SelectChar(carousel, "char_elizabeth");
            yield return new WaitForSecondsRealtime(0.3f); // let panel redraw
            Debug.Log("[RosterLocalizationBot] EN — Elizabeth bio. Holding 2.5s...");
            yield return new WaitForSecondsRealtime(2.5f);

            // ── Phase 3: ENGLISH — James (short bio) ──────────────────────────
            carousel = FindActive<CarouselController>();
            SelectChar(carousel, "char_james");
            yield return new WaitForSecondsRealtime(0.3f);
            Debug.Log("[RosterLocalizationBot] EN — James bio. Holding 1.5s...");
            yield return new WaitForSecondsRealtime(1.5f);

            // ── Phase 4: Switch to JAPANESE ────────────────────────────────────
            Debug.Log("[RosterLocalizationBot] Switching to Japanese.");
            LocalizationManager.SetLanguage(Language.Japanese);
            yield return new WaitForSecondsRealtime(0.8f); // let all LocalizedText components refresh

            // ── Phase 5: JAPANESE — Elizabeth (long JP bio, fits above divider)
            carousel = FindActive<CarouselController>();
            SelectChar(carousel, "char_elizabeth");
            yield return new WaitForSecondsRealtime(0.3f);
            Debug.Log("[RosterLocalizationBot] JP — Elizabeth bio. Holding 2.5s...");
            yield return new WaitForSecondsRealtime(2.5f);

            // ── Phase 6: JAPANESE — Shae (long JP bio) ────────────────────────
            carousel = FindActive<CarouselController>();
            SelectChar(carousel, "char_shae");
            yield return new WaitForSecondsRealtime(0.3f);
            Debug.Log("[RosterLocalizationBot] JP — Shae bio. Holding 2.5s...");
            yield return new WaitForSecondsRealtime(2.5f);

            // ── Phase 7: Switch back to ENGLISH — Elizabeth (bookend) ──────────
            Debug.Log("[RosterLocalizationBot] Switching back to English.");
            LocalizationManager.SetLanguage(Language.English);
            yield return new WaitForSecondsRealtime(0.5f);

            carousel = FindActive<CarouselController>();
            SelectChar(carousel, "char_elizabeth");
            yield return new WaitForSecondsRealtime(0.3f);
            Debug.Log("[RosterLocalizationBot] EN — Elizabeth bookend. Holding 1.5s...");
            yield return new WaitForSecondsRealtime(1.5f);

            // ── Done ──────────────────────────────────────────────────────────
            Debug.Log("[RosterLocalizationBot] Sequence done — exiting play mode.");
            EditorApplication.ExitPlaymode();
        }
    }
}
#endif
