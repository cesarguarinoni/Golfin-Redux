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

namespace Golfin.EditorTools
{
    /// <summary>
    /// Showcase recorder for the Mission Selection screen (missions_v1 Phase C).
    /// Produces a clean MP4 at full iPhone-14 1170x2532 for the pipeline video gate.
    ///
    /// Sequence:
    ///   boot → tap the title gate → Home → bottom-nav tee button → Mode Select →
    ///   tap PLAY on the REAL Missions mode card → Mission Selection →
    ///   hold on the daily card + tier strip + expanded NEXT MISSION →
    ///   scroll the campaign list down and back → tap the BEGINNER tier tab →
    ///   back out → exit.
    ///
    /// Everything is driven through the widget a player actually taps — the mode card's own
    /// playButton, not ScreenManager.ShowScreen. The carousel keeps three copies of every card
    /// and subscribes only the live one, so the copy with a live OnPlayClicked is the one driven.
    ///
    /// NOTE: this spends the real 50 RP `mode_entry_fee:missions`, exactly as a player would.
    ///
    /// Output: Docs/Specs/Active/missions_v1/videos/raw.mp4
    /// Usage:  GOLFIN > Missions > Record Demo Video  (or call LaunchDemo()).
    /// </summary>
    public static class MissionsDemoRecorder
    {
        const string ShellScenePath = "Assets/Scenes/ShellScene.unity";
        const string OutputDir      = "Docs/Specs/Active/missions_v1/videos";
        const string ArmedKey       = "MissionsDemoRecorder.Armed";

        static RecorderController _recorder;

        [InitializeOnLoadMethod]
        static void RegisterHook()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        [MenuItem("GOLFIN/Missions/Record Demo Video")]
        public static void LaunchDemo()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[MissionsDemo] Already in play mode — stop first.");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            EditorSceneManager.OpenScene(ShellScenePath);
            Directory.CreateDirectory(OutputDir);
            SessionState.SetBool(ArmedKey, true);
            EditorApplication.EnterPlaymode();
            Debug.Log("[MissionsDemo] Armed. Entering play mode...");
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
                if (cw > 0 && ch > 0)
                {
                    w = Mathf.Max(2, (int)cw); h = Mathf.Max(2, (int)ch);
                    if (w % 2 != 0) w--; if (h % 2 != 0) h--;
                    Debug.LogWarning($"[MissionsDemo] Could not pin iPhone-14 — recording at {w}x{h}.");
                }
            }

            var movie = ScriptableObject.CreateInstance<MovieRecorderSettings>();
            movie.name         = "MissionsDemo";
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
            Debug.Log($"[MissionsDemo] Recording → {OutputDir}/raw.mp4 ({w}x{h} @ 30fps)");

            var host = new GameObject("[MissionsDemoBot]");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.AddComponent<MissionsDemoRunner>().StartDemo();
        }

        static void StopRecorder()
        {
            if (_recorder != null)
            {
                try
                {
                    if (_recorder.IsRecording())
                        _recorder.StopRecording();
                    Debug.Log("[MissionsDemo] Recording stopped.");
                }
                catch (Exception e) { Debug.LogWarning($"[MissionsDemo] StopRecorder: {e.Message}"); }
                _recorder = null;
            }
        }
    }

    public class MissionsDemoRunner : MonoBehaviour
    {
        public void StartDemo() => StartCoroutine(Sequence());

        static Button FindButton(string buttonName)
        {
            return Resources.FindObjectsOfTypeAll<Button>()
                .FirstOrDefault(b => b.gameObject.name == buttonName
                    && !string.IsNullOrEmpty(b.gameObject.scene.name)
                    && b.isActiveAndEnabled
                    && b.GetComponentInParent<Canvas>() != null);
        }

        /// <summary>
        /// The mode carousel keeps three copies of every card for the infinite scroll and wires
        /// its PLAY handler to exactly one of them. Driving the wrong copy fires a button nothing
        /// is listening to — which is precisely what a ShowScreen shortcut would hide.
        /// </summary>
        static bool TapMissionsModeCard()
        {
            var cardType = Type.GetType("GolfinRedux.UI.ModeSelect.ModeCardController, Assembly-CSharp");
            if (cardType == null) return false;
            var idProp = cardType.GetProperty("ModeId");
            var evF    = cardType.GetField("OnPlayClicked", BindingFlags.NonPublic | BindingFlags.Instance);
            var pbF    = cardType.GetField("playButton",    BindingFlags.NonPublic | BindingFlags.Instance);
            if (idProp == null || pbF == null) return false;

            UnityEngine.Object subscribed = null;
            foreach (var c in Resources.FindObjectsOfTypeAll(cardType))
            {
                var mono = c as MonoBehaviour;
                if (mono == null || string.IsNullOrEmpty(mono.gameObject.scene.name)) continue;
                if ((idProp.GetValue(c) as string) != "missions") continue;
                var d = evF?.GetValue(c) as Delegate;
                if (d != null && d.GetInvocationList().Length > 0) { subscribed = c; break; }
            }
            if (subscribed == null) return false;

            var pb = pbF.GetValue(subscribed) as Button;
            if (pb == null) return false;
            pb.gameObject.SetActive(true);
            pb.onClick.Invoke();
            Debug.Log("[MissionsDemo] Tapped the real Missions mode card playButton.");
            return true;
        }

        static IEnumerator ScrollTo(ScrollRect sr, float target, float seconds)
        {
            if (sr == null) { yield return new WaitForSecondsRealtime(seconds); yield break; }
            float start = sr.verticalNormalizedPosition, t = 0f;
            while (t < seconds)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / seconds));
                sr.verticalNormalizedPosition = Mathf.Lerp(start, target, k);
                yield return null;
            }
            sr.verticalNormalizedPosition = target;
        }

        IEnumerator Sequence()
        {
            // Boot through splash/loading. The app opens on a title gate ScreenManager does not
            // manage, so it has to be tapped like a player would.
            yield return new WaitForSecondsRealtime(4.5f);
            foreach (var n in new[] { "StartButton", "PlayButton", "TapToStart" })
            {
                var b = FindButton(n);
                if (b != null) { b.onClick.Invoke(); Debug.Log($"[MissionsDemo] Tapped the gate '{n}'."); break; }
            }
            yield return new WaitForSecondsRealtime(2.5f);   // Home settles

            // Bottom-nav tee button → Mode Select.
            var pum = GameObject.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
                        .FirstOrDefault(m => m.GetType().Name == "PersistentUIManager");
            var mainPlay = pum?.GetType()
                .GetField("mainPlayButton", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.GetValue(pum) as Button;
            if (mainPlay != null) mainPlay.onClick.Invoke();
            yield return new WaitForSecondsRealtime(3.0f);   // carousel settles on Mode Select

            // The real Missions card.
            if (!TapMissionsModeCard())
                Debug.LogWarning("[MissionsDemo] No subscribed Missions card found.");
            yield return new WaitForSecondsRealtime(1.5f);   // fade into Mission Selection

            // Hold: daily card (live countdown + server-decided reward), tier strip, NEXT expanded.
            yield return new WaitForSecondsRealtime(5.0f);

            var scroll = Resources.FindObjectsOfTypeAll<ScrollRect>()
                .FirstOrDefault(s => !string.IsNullOrEmpty(s.gameObject.scene.name)
                    && s.GetComponentsInParent<Transform>(true)
                        .Any(t => t.name == "MissionSelectionScreen"));

            yield return ScrollTo(scroll, 0f, 5.0f);          // down through the locked campaign
            yield return new WaitForSecondsRealtime(1.5f);
            yield return ScrollTo(scroll, 1f, 3.0f);          // back to the top
            yield return new WaitForSecondsRealtime(1.5f);

            // Tier strip: BEGINNER is the only unlocked tier, so it is the only tab that responds.
            var beginner = Resources.FindObjectsOfTypeAll<Button>()
                .FirstOrDefault(b => b.gameObject.name == "Tab_BEGINNER"
                    && !string.IsNullOrEmpty(b.gameObject.scene.name) && b.isActiveAndEnabled);
            if (beginner != null) beginner.onClick.Invoke();
            yield return new WaitForSecondsRealtime(2.5f);

            var back = FindButton("BackButton") ?? FindButton("CloseButton");
            if (back != null) back.onClick.Invoke();
            yield return new WaitForSecondsRealtime(1.5f);

            Debug.Log("[MissionsDemo] Sequence done — exiting play mode.");
            EditorApplication.ExitPlaymode();
        }
    }
}
#endif
