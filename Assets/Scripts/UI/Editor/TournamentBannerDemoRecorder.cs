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
    /// Showcase recorder for the tournament sign-up modal's cross-promotion strip
    /// (`tournament_banners`). Produces a clean MP4 at full iPhone-14 1170×2532.
    ///
    /// The point of the clip is the CONTRAST, since that is the whole feature:
    ///   boot → Tournaments → tap SIGN UP on the tournament that HAS a banner
    ///   assigned in the dashboard → hold on the 1411-tall modal with the strip →
    ///   BACK → open a tournament with NO assignment → hold on the 1167-tall modal
    ///   with no strip and no gap → exit.
    ///
    /// Nothing here is faked: the banner arrives from `GET /tournaments/golfin`'s
    /// `modal_banner`, whose art is whatever is live in the Banners panel. If the
    /// row is switched off in the dashboard, this clip records the no-banner state
    /// for both tournaments — which is the correct behaviour, not a broken recording.
    ///
    /// Modelled on TournamentDemoRecorder (same RecorderController setup, same
    /// FindButton helper) rather than hand-rolling a capture path.
    ///
    /// Output: Docs/Specs/Active/tournament_banners/videos/raw.mp4
    /// Usage:  GOLFIN > Tournaments > Record Banner Demo Video
    /// </summary>
    public static class TournamentBannerDemoRecorder
    {
        const string ShellScenePath = "Assets/Scenes/ShellScene.unity";
        const string OutputDir      = "Docs/Specs/Active/tournament_banners/videos";
        const string ArmedKey       = "TournamentBannerDemoRecorder.Armed";

        static RecorderController _recorder;

        [InitializeOnLoadMethod]
        static void RegisterHook()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        [MenuItem("GOLFIN/Tournaments/Record Banner Demo Video")]
        public static void LaunchDemo()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[TournamentBannerDemo] Already in play mode — stop first.");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ShellScenePath);
            Directory.CreateDirectory(OutputDir);
            SessionState.SetBool(ArmedKey, true);
            EditorApplication.EnterPlaymode();
            Debug.Log("[TournamentBannerDemo] Armed. Entering play mode…");
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
                    Debug.LogWarning($"[TournamentBannerDemo] Could not pin iPhone-14 — recording at {w}x{h}.");
                }
            }

            var movie = ScriptableObject.CreateInstance<MovieRecorderSettings>();
            movie.name         = "TournamentBannerDemo";
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
            Debug.Log($"[TournamentBannerDemo] Recording → {OutputDir}/raw.mp4 ({w}x{h} @ 30fps)");

            var host = new GameObject("[TournamentBannerDemoBot]");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.AddComponent<TournamentBannerDemoRunner>().StartDemo();
        }

        static void StopRecorder()
        {
            if (_recorder == null) return;
            try
            {
                if (_recorder.IsRecording()) _recorder.StopRecording();
                Debug.Log("[TournamentBannerDemo] Recording stopped.");
            }
            catch (Exception e) { Debug.LogWarning($"[TournamentBannerDemo] StopRecorder: {e.Message}"); }
            _recorder = null;
        }
    }

    public class TournamentBannerDemoRunner : MonoBehaviour
    {
        /// <summary>The tournament the dashboard has a banner assigned to.</summary>
        const string WithBanner    = "kasumigaseki_open";
        /// <summary>Any tournament with no assignment — the state every other one is in.</summary>
        const string WithoutBanner = "lomond_championship";

        public void StartDemo() => StartCoroutine(Sequence());

        static Button FindButton(string buttonName)
        {
            return Resources.FindObjectsOfTypeAll<Button>()
                .FirstOrDefault(b => b.gameObject.name == buttonName
                    && !string.IsNullOrEmpty(b.gameObject.scene.name)
                    && b.GetComponentInParent<Canvas>() != null);
        }

        static GolfinRedux.UI.Tournaments.TournamentSignupModalController Modal()
        {
            return Resources.FindObjectsOfTypeAll<GolfinRedux.UI.Tournaments.TournamentSignupModalController>()
                .FirstOrDefault(m => !string.IsNullOrEmpty(m.gameObject.scene.name));
        }

        /// <summary>Log the measured height so the clip is self-auditing in the console.</summary>
        static void LogHeight(string tag)
        {
            var modal = Modal();
            var panel = modal?.transform.Find("Panel") as RectTransform;
            var strip = modal?.transform.Find("Panel/Content/BannerRoot");
            if (panel == null) return;
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(panel);
            Debug.Log($"[TournamentBannerDemo] {tag}: panel={panel.rect.size} " +
                      $"strip={(strip != null && strip.gameObject.activeSelf ? "ON" : "off")}");
        }

        IEnumerator Sequence()
        {
            // Boot through splash/loading → Home.
            yield return new WaitForSecondsRealtime(4.5f);

            GolfinRedux.UI.ScreenManager.Instance?.ShowScreen(GolfinRedux.UI.ScreenId.TournamentSelection);
            yield return new WaitForSecondsRealtime(1.2f);   // fade-in
            yield return new WaitForSecondsRealtime(2.5f);   // hold on the card list

            // === Phase A: the tournament WITH a banner assigned in the dashboard ===
            var modal = Modal();
            if (modal == null)
            {
                Debug.LogError("[TournamentBannerDemo] No TournamentSignupModalController in the scene.");
                EditorApplication.ExitPlaymode();
                yield break;
            }

            modal.Open(WithBanner);
            yield return new WaitForSecondsRealtime(1.0f);   // let the art download land
            yield return new WaitForSecondsRealtime(1.0f);
            LogHeight("WITH banner (" + WithBanner + ")");
            yield return new WaitForSecondsRealtime(4.0f);   // hold on the strip

            // === Phase B: BACK, then one with NO assignment ===
            var back = FindButton("CancelButton");
            if (back != null) back.onClick.Invoke();
            yield return new WaitForSecondsRealtime(1.5f);

            modal.Open(WithoutBanner);
            yield return new WaitForSecondsRealtime(1.2f);
            LogHeight("WITHOUT banner (" + WithoutBanner + ")");
            yield return new WaitForSecondsRealtime(4.0f);   // hold on the no-strip state

            var back2 = FindButton("CancelButton");
            if (back2 != null) back2.onClick.Invoke();
            yield return new WaitForSecondsRealtime(1.5f);

            Debug.Log("[TournamentBannerDemo] Sequence done — exiting play mode.");
            EditorApplication.ExitPlaymode();
        }
    }
}
#endif
