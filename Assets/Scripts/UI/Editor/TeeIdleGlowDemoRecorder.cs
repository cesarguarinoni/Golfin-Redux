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
    /// Records the REAL tee-idle glow (Part B of shot_ui_translucency_glow) as one
    /// continuous MP4, driven entirely through the player's own entry path:
    ///
    ///   ShellScene → SplashScreen/StartButton (PLAY) → HomeScreen PRACTICE card
    ///   PlayButton → hole-selection PLAY → Lomond Hole 1 tee.
    ///
    /// Timeline (record-start relative, unscaled — the glow itself runs on unscaled time):
    ///   t=0.0   timer zeroed via the public NotifyOtherInteraction() bus, no glow
    ///   t=5.0   glow onset (idleGlowDelay), pulses at glowPulsePeriod
    ///   t=9.0   REAL Spin button onClick → SpinPanel opens (overlay) → glow fades ≤0.15 s
    ///   t=12.0  SpinPanel.Close() → modal branch releases, countdown restarts from 0
    ///   t=17.0  glow returns
    ///   t=19.0  stop
    ///
    /// Covers acceptance items 4 (5 s onset), 5 (other-button reset) and 6 (modal
    /// pause + restart-on-close) in a single readable clip.
    ///
    /// Modeled on ClubControlArrowDemoRecorder (own RecorderController, GameView input
    /// source so the ScreenSpaceOverlay ShotUI is actually captured — a camera source
    /// drops Overlay UI under URP). Game View size is pinned BEFORE StartRecording and
    /// nothing reads a RenderTexture mid-record, per the Y-flip discipline.
    ///
    /// Usage: GOLFIN > Physics > Record Tee Idle Glow Demo
    /// Output: Docs/Specs/Active/shot_ui_translucency_glow/videos/raw_tee_idle_glow.mp4
    /// </summary>
    public static class TeeIdleGlowDemoRecorder
    {
        const string ShellScenePath = "Assets/Scenes/ShellScene.unity";
        const string OutputDir      = "Docs/Specs/Active/shot_ui_translucency_glow/videos";
        const string RawStem        = "raw_tee_idle_glow";
        const string ArmedKey       = "TeeIdleGlowDemo.Armed";

        static RecorderController _recorder;

        public static string RawPathNoExt => $"{OutputDir}/{RawStem}";

        [InitializeOnLoadMethod]
        static void RegisterHook()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        [MenuItem("GOLFIN/Physics/Record Tee Idle Glow Demo")]
        public static void LaunchDemo()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[GlowDemo] Already in play mode — stop first.");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            EditorSceneManager.OpenScene(ShellScenePath);
            Directory.CreateDirectory(OutputDir);
            SessionState.SetBool(ArmedKey, true);
            EditorApplication.EnterPlaymode();
            Debug.Log("[GlowDemo] Armed. Entering play mode — will drive the real boot path.");
        }

        static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(ArmedKey, false)) return;

            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                SessionState.SetBool(ArmedKey, false);
                Application.runInBackground = true;   // frames render while unfocused

                var host = new GameObject("[TeeIdleGlowDemoBot]");
                UnityEngine.Object.DontDestroyOnLoad(host);
                host.AddComponent<TeeIdleGlowDemoRunner>().Begin();
            }
            else if (state == PlayModeStateChange.ExitingPlayMode)
            {
                StopClip();   // safety net if the coroutine aborted early
            }
        }

        static bool TryEnsureIPhone14Selected()
        {
            try
            {
                var asm = Assembly.Load("Golfin.Physics.Viewer.BotEditor");
                var t   = asm?.GetType("Golfin.Physics.Viewer.Editor.GameViewSizeUtil");
                var m   = t?.GetMethod("EnsureIPhone14Selected",
                              BindingFlags.Public | BindingFlags.Static);
                return m != null && (bool)m.Invoke(null, null);
            }
            catch { return false; }
        }

        public static void StartClip(string fileNoExt)
        {
            // Pin the device size BEFORE StartRecording — locking render state here is what
            // keeps the Game View RT from being recreated mid-record (Y-flip trigger).
            bool selected = TryEnsureIPhone14Selected();
            int w = 1170, h = 2532;
            if (!selected)
            {
                PlayModeWindow.GetRenderingResolution(out uint cw, out uint ch);
                if (cw > 0 && ch > 0)
                {
                    w = Mathf.Max(2, (int)cw); h = Mathf.Max(2, (int)ch);
                    if (w % 2 != 0) w--;
                    if (h % 2 != 0) h--;
                    Debug.LogWarning($"[GlowDemo] Could not pin iPhone-14 — recording at {w}x{h}.");
                }
            }

            QualitySettings.vSyncCount  = 0;
            Application.targetFrameRate = 30;

            var movie = ScriptableObject.CreateInstance<MovieRecorderSettings>();
            movie.name         = "TeeIdleGlowDemo";
            movie.Enabled      = true;
            movie.OutputFormat = MovieRecorderSettings.VideoRecorderOutputFormat.MP4;
            movie.ImageInputSettings = new GameViewInputSettings { OutputWidth = w, OutputHeight = h };
            movie.AudioInputSettings.PreserveAudio = false;
            movie.OutputFile = fileNoExt;

            var settings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
            settings.AddRecorderSettings(movie);
            settings.SetRecordModeToManual();
            settings.FrameRate = 30;
            settings.FrameRatePlayback = FrameRatePlayback.Variable;   // video time == wall clock

            _recorder = new RecorderController(settings);
            _recorder.PrepareRecording();
            _recorder.StartRecording();
            Debug.Log($"[GlowDemo] Recording started → {fileNoExt}.mp4 ({w}x{h} @ 30fps)");
        }

        public static void StopClip()
        {
            if (_recorder == null) return;
            try
            {
                if (_recorder.IsRecording())
                    _recorder.StopRecording();
                Debug.Log($"[GlowDemo] Recording stopped → {RawPathNoExt}.mp4");
            }
            catch (Exception e) { Debug.LogWarning($"[GlowDemo] StopClip: {e.Message}"); }
            _recorder = null;
        }
    }

    /// <summary>
    /// Runtime bot that walks the real boot path, then records the glow lifecycle.
    /// The ShotUI types live in Golfin.Gameplay.UI (autoReferenced:false), so the glow
    /// controller and spin panel are reached by type-name reflection.
    /// </summary>
    public class TeeIdleGlowDemoRunner : MonoBehaviour
    {
        const string GlowTypeName = "Golfin.Gameplay.UI.ShotUI.TeeIdleGlowController";
        const string SpinBtnType  = "Golfin.Gameplay.UI.ShotUI.SpinButtonWidget";
        const string SpinPanelType= "Golfin.Gameplay.UI.ShotUI.SpinPanelWidget";

        public void Begin() => StartCoroutine(Run());

        static Type FindType(string full) =>
            AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType(full)).FirstOrDefault(t => t != null);

        static Button FindActiveButtonNamed(string goName) =>
            UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .FirstOrDefault(b => b.name == goName && b.gameObject.activeInHierarchy);

        static Button FindActiveButtonLabelled(string label) =>
            UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .FirstOrDefault(b =>
                {
                    var t = b.GetComponentInChildren<TMPro.TextMeshProUGUI>();
                    return t != null && string.Equals(t.text.Trim(), label, StringComparison.OrdinalIgnoreCase);
                });

        IEnumerator WaitUntilOrFail(Func<bool> cond, float timeout, string what)
        {
            float t0 = Time.realtimeSinceStartup;
            while (!cond())
            {
                if (Time.realtimeSinceStartup - t0 > timeout)
                {
                    Debug.LogError($"[GlowDemo] TIMEOUT waiting for {what} ({timeout:F0}s) — aborting.");
                    TeeIdleGlowDemoRecorder.StopClip();
                    EditorApplication.isPlaying = false;
                    yield break;
                }
                yield return null;
            }
            Debug.Log($"[GlowDemo] reached: {what}");
        }

        IEnumerator Run()
        {
            // ── 1. Splash → PLAY ────────────────────────────────────────────────
            yield return WaitUntilOrFail(() => FindActiveButtonNamed("StartButton") != null, 60f, "splash PLAY");
            FindActiveButtonNamed("StartButton").onClick.Invoke();

            // ── 2. Home → PRACTICE card PLAY ────────────────────────────────────
            yield return WaitUntilOrFail(() => FindActiveButtonNamed("PlayButton") != null, 60f, "home PRACTICE PLAY");
            FindActiveButtonNamed("PlayButton").onClick.Invoke();

            // ── 3. Hole selection → PLAY ────────────────────────────────────────
            // The Home PRACTICE card's own button is ALSO labelled "PLAY" and is still alive
            // for a few frames after step 2, so match on the label while EXCLUDING that
            // button's GameObject name — otherwise we just re-click Home and never advance.
            Func<Button> holePlay = () =>
                UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                    .FirstOrDefault(b =>
                    {
                        if (b.name == "PlayButton") return false;          // Home card button
                        var t = b.GetComponentInChildren<TMPro.TextMeshProUGUI>();
                        return t != null && string.Equals(t.text.Trim(), "PLAY", StringComparison.OrdinalIgnoreCase);
                    });

            yield return WaitUntilOrFail(() => holePlay() != null, 90f, "hole-selection PLAY");
            holePlay().onClick.Invoke();

            // ── 4. Wait for the tee (glow controller live in the loaded gameplay stack) ──
            var glowType = FindType(GlowTypeName);
            if (glowType == null) { Debug.LogError("[GlowDemo] TeeIdleGlowController type not found."); yield break; }

            yield return WaitUntilOrFail(
                () => UnityEngine.Object.FindFirstObjectByType(glowType) != null, 180f, "tee (glow controller)");

            var glow = UnityEngine.Object.FindFirstObjectByType(glowType);

            // Let the hole settle so nothing recreates the Game View RT after StartRecording.
            yield return new WaitForSecondsRealtime(3f);

            // ── 5. Record ───────────────────────────────────────────────────────
            TeeIdleGlowDemoRecorder.StartClip(TeeIdleGlowDemoRecorder.RawPathNoExt);
            yield return null;

            // Zero the countdown through the PUBLIC bus so the clip's own t=0 is the
            // countdown start — the 5 s onset is then measurable from the video itself.
            var notify = glowType.GetMethod("NotifyOtherInteraction",
                             BindingFlags.Public | BindingFlags.Static);
            notify?.Invoke(null, null);
            Debug.Log("[GlowDemo] t=0.0 countdown zeroed — expecting glow onset at t=5.0");

            yield return new WaitForSecondsRealtime(9f);   // 5 s countdown + ~3.3 pulse cycles

            // ── 6. Real Spin button tap → panel opens, glow must drop out ───────
            var spinType = FindType(SpinBtnType);
            var spin = spinType != null ? UnityEngine.Object.FindFirstObjectByType(spinType) as MonoBehaviour : null;
            if (spin != null)
            {
                var btn = spin.GetComponentInChildren<Button>();
                if (btn != null)
                {
                    btn.onClick.Invoke();   // fires OnClick (opens panel) + NotifyOtherInteraction
                    Debug.Log("[GlowDemo] t=9.0 REAL Spin button onClick invoked");
                }
                else Debug.LogWarning("[GlowDemo] Spin widget has no Button — skipping tap phase.");
            }
            else Debug.LogWarning("[GlowDemo] SpinButtonWidget not found — skipping tap phase.");

            yield return new WaitForSecondsRealtime(3f);   // panel open, glow suppressed

            // ── 7. Close the panel → countdown restarts from 0 ──────────────────
            var panelType = FindType(SpinPanelType);
            var panel = panelType != null ? UnityEngine.Object.FindFirstObjectByType(panelType) as MonoBehaviour : null;
            panelType?.GetMethod("Close", BindingFlags.Public | BindingFlags.Instance)?.Invoke(panel, null);
            Debug.Log("[GlowDemo] t=12.0 SpinPanel.Close() — countdown restarts from 0");

            yield return new WaitForSecondsRealtime(7f);   // 5 s re-arm + ~1.6 pulse cycles

            // ── 8. Done ─────────────────────────────────────────────────────────
            TeeIdleGlowDemoRecorder.StopClip();
            Debug.Log("[GlowDemo] complete.");
            yield return new WaitForSecondsRealtime(1f);
            EditorApplication.isPlaying = false;
        }
    }
}
#endif
