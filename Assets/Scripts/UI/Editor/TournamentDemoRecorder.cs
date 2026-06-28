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
    /// Showcase recorder for the Tournament Selection + Leaderboard screens.
    /// Produces a clean MP4 at full iPhone-14 1170x2532 for the pipeline video gate.
    ///
    /// Sequence:
    ///   boot → Home → Tournament Selection (6 live cards, default All tab) →
    ///   pause on selection → tab to Playing → tab to Closed → tap LEADERBOARD
    ///   on an Ended card → Leaderboard screen with live standings →
    ///   back to Selection → exit.
    ///
    /// Output: Docs/Specs/Active/tournament_screens_live_bind/videos/raw.mp4
    /// Usage:  GOLFIN > Tournaments > Record Demo Video  (or call LaunchDemo()).
    /// </summary>
    public static class TournamentDemoRecorder
    {
        const string ShellScenePath = "Assets/Scenes/ShellScene.unity";
        const string OutputDir      = "Docs/Specs/Active/tournament_screens_live_bind/videos";
        const string ArmedKey       = "TournamentDemoRecorder.Armed";

        static RecorderController _recorder;

        [InitializeOnLoadMethod]
        static void RegisterHook()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        [MenuItem("GOLFIN/Tournaments/Record Demo Video")]
        public static void LaunchDemo()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[TournamentDemo] Already in play mode — stop first.");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            EditorSceneManager.OpenScene(ShellScenePath);
            Directory.CreateDirectory(OutputDir);
            SessionState.SetBool(ArmedKey, true);
            EditorApplication.EnterPlaymode();
            Debug.Log("[TournamentDemo] Armed. Entering play mode...");
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
                    Debug.LogWarning($"[TournamentDemo] Could not pin iPhone-14 — recording at {w}x{h}.");
                }
            }

            var movie = ScriptableObject.CreateInstance<MovieRecorderSettings>();
            movie.name         = "TournamentDemo";
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
            Debug.Log($"[TournamentDemo] Recording → {OutputDir}/raw.mp4 ({w}x{h} @ 30fps)");

            var host = new GameObject("[TournamentDemoBot]");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.AddComponent<TournamentDemoRunner>().StartDemo();
        }

        static void StopRecorder()
        {
            if (_recorder != null)
            {
                try
                {
                    if (_recorder.IsRecording())
                        _recorder.StopRecording();
                    Debug.Log("[TournamentDemo] Recording stopped.");
                }
                catch (Exception e) { Debug.LogWarning($"[TournamentDemo] StopRecorder: {e.Message}"); }
                _recorder = null;
            }
        }
    }

    public class TournamentDemoRunner : MonoBehaviour
    {
        public void StartDemo() => StartCoroutine(Sequence());

        /// <summary>Find a Button by its GameObject name anywhere in active scenes (same pattern as RankingsDemoRunner).</summary>
        static Button FindButton(string buttonName)
        {
            return Resources.FindObjectsOfTypeAll<Button>()
                .FirstOrDefault(b => b.gameObject.name == buttonName
                    && !string.IsNullOrEmpty(b.gameObject.scene.name)
                    && b.GetComponentInParent<Canvas>() != null);
        }

        IEnumerator Sequence()
        {
            // Boot through splash/loading → Home.
            yield return new WaitForSecondsRealtime(4.5f);

            // Navigate to Tournament Selection.
            GolfinRedux.UI.ScreenManager.Instance?.ShowScreen(GolfinRedux.UI.ScreenId.TournamentSelection);
            yield return new WaitForSecondsRealtime(1.2f); // fade-in

            // === Phase A: All-tab (default) — all 6 live cards visible ===
            yield return new WaitForSecondsRealtime(3.0f);  // pause to show full list

            // Scroll to Playing tab (EnteredActive/EnteredFinished cards)
            var playingTab = FindButton("PlayingTab");
            if (playingTab != null) playingTab.onClick.Invoke();
            yield return new WaitForSecondsRealtime(2.0f);

            // Scroll to Closed tab (Ended cards)
            var closedTab = FindButton("ClosedTab");
            if (closedTab != null) closedTab.onClick.Invoke();
            yield return new WaitForSecondsRealtime(2.0f);

            // Return to All tab
            var allTab = FindButton("AllTab");
            if (allTab != null) allTab.onClick.Invoke();
            yield return new WaitForSecondsRealtime(1.5f);

            // === Phase B: Tap LEADERBOARD CTA on first Ended card ===
            // Find all TournamentSelectionCards and click the first Ended one
            var cards = Resources.FindObjectsOfTypeAll<GolfinRedux.UI.Tournaments.TournamentSelectionCard>();
            GolfinRedux.UI.Tournaments.TournamentSelectionCard target = null;
            foreach (var card in cards)
            {
                if (card.State == GolfinRedux.UI.Tournaments.TournamentSelectionCard.CardState.Ended
                    && !string.IsNullOrEmpty(card.gameObject.scene.name)
                    && target == null)
                {
                    target = card;
                }
            }
            if (target != null)
            {
                var btn = target.GetComponentInChildren<Button>();
                if (btn != null)
                {
                    Debug.Log($"[TournamentDemo] Tapping LEADERBOARD on '{target.TournamentId}'");
                    btn.onClick.Invoke();
                }
            }
            else
            {
                Debug.LogWarning("[TournamentDemo] No Ended card found — navigating to leaderboard directly.");
                GolfinRedux.UI.ScreenManager.Instance?.ShowScreen(GolfinRedux.UI.ScreenId.TournamentLeaderboard);
            }
            yield return new WaitForSecondsRealtime(1.2f); // fade to leaderboard

            // === Phase C: Leaderboard — podium + standings visible ===
            yield return new WaitForSecondsRealtime(3.5f);

            // Back to Selection
            var closeBtn = FindButton("CloseButton");
            if (closeBtn == null) closeBtn = FindButton("BackButton");
            if (closeBtn != null) closeBtn.onClick.Invoke();
            else GolfinRedux.UI.ScreenManager.Instance?.ShowScreen(GolfinRedux.UI.ScreenId.TournamentSelection);
            yield return new WaitForSecondsRealtime(1.0f);

            Debug.Log("[TournamentDemo] Sequence done — exiting play mode.");
            EditorApplication.ExitPlaymode();
        }
    }
}
#endif
