#if UNITY_EDITOR
// Assets/Scripts/UI/Editor/StoreHistoryDemoRecorder.cs
// store_history — the daily-report clip.
//
// Modelled on GeneralShopDemoRecorder (Unity Recorder, GameView source, 1170x2532 @ 30fps) with
// GpsFlowDemoRecorder's caption sidecar, so build_bot_video.py --mode captionsjson burns the
// captions without a hand-timed list that drifts the moment a hold changes.
//
// EVERY TAP IS A REAL WIDGET'S onClick — the Splash StartButton, the top-bar "+", the Rewards
// Center's own HistoryChip, the chip row, the tab strip. No ShowScreen(target) shortcut: the app
// boots behind a title gate ScreenManager does not manage, so a bare ShowScreen leaves the frame on
// the title while CurrentScreen reports success.
//
// GAME VIEW SOURCE, NOT CAMERA — a camera source drops the Overlay HUD under URP
// (reference_gameplay_capture_gameview_not_camera_urp). And NOTHING calls CaptureCore while the
// Recorder runs: a backbuffer read mid-recording flips Recorder frames on Metal
// (reference_botvideorecorder_yflip_fix). Stills come out of the finished mp4.
//
// Usage: GOLFIN > Store History > Record demo
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Input;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using GolfinRedux.UI;

namespace Golfin.EditorTools
{
    public static class StoreHistoryDemoRecorder
    {
        const string OutputDir  = "Docs/Specs/Active/store_history/videos";
        const string CaptionDir = "Docs/Reports/Media/store_history";
        const string ArmedKey   = "StoreHistoryDemoRecorder.Armed";
        const string ShellScene = "Assets/Scenes/ShellScene.unity";

        static RecorderController _recorder;

        [InitializeOnLoadMethod]
        static void RegisterHook()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        [MenuItem("GOLFIN/Store History/Record demo", priority = 262)]
        public static void LaunchDemo()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("[StoreHistoryDemo] Already playing — stop first."); return; }
            Directory.CreateDirectory(OutputDir);
            Directory.CreateDirectory(CaptionDir);

            // ShellScene, explicitly. An EditMode sweep leaves whatever scene it opened last —
            // often an untitled empty one — and play mode there has no ScreenManager and no Splash.
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != ShellScene)
                EditorSceneManager.OpenScene(ShellScene, OpenSceneMode.Single);

            Application.runInBackground = true;   // else the Recorder starves while Unity is behind
            SessionState.SetBool(ArmedKey, true);
            EditorApplication.EnterPlaymode();
            Debug.Log("[StoreHistoryDemo] Armed. Entering play mode…");
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
                    Debug.LogWarning($"[StoreHistoryDemo] Could not pin iPhone-14 — recording at {w}x{h}.");
                }
            }

            var movie = ScriptableObject.CreateInstance<MovieRecorderSettings>();
            movie.name         = "StoreHistoryDemo";
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
            StoreHistoryDemoRunner.RecordStart = Time.realtimeSinceStartup;
            Debug.Log($"[StoreHistoryDemo] Recording → {OutputDir}/raw.mp4 ({w}x{h} @ 30fps)");

            var host = new GameObject("[StoreHistoryDemoBot]");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.AddComponent<StoreHistoryDemoRunner>().StartDemo();
        }

        static void StopRecorder()
        {
            if (_recorder == null) return;
            try { if (_recorder.IsRecording()) _recorder.StopRecording(); Debug.Log("[StoreHistoryDemo] Recording stopped."); }
            catch (Exception e) { Debug.LogWarning($"[StoreHistoryDemo] StopRecorder: {e.Message}"); }
            _recorder = null;
        }
    }

    public class StoreHistoryDemoRunner : MonoBehaviour
    {
        /// <summary>Clock at StartRecording(), so every caption is stamped in seconds since the
        /// first frame — what build_bot_video.py's `--mode captionsjson` expects.</summary>
        public static float RecordStart;

        const string CaptionDir = "Docs/Reports/Media/store_history";

        /// <summary>How long a screen holds before the next tap — long enough to read a panel and
        /// long enough for the live request behind it to land.</summary>
        const float Hold   = 3.2f;
        const float Settle = 1.2f;

        readonly List<(float start, string text)> _caps = new List<(float, string)>();

        /// <summary>Open a caption. The previous one ends where this begins. Stamped at the moment
        /// the claim BECOMES TRUE, never before — a caption that opens on the tap describes a
        /// screen the viewer cannot see yet
        /// (reference_caption_window_must_match_the_frame).</summary>
        void Cap(string text) => _caps.Add((Time.realtimeSinceStartup - RecordStart, text));

        void WriteCaptions()
        {
            var sb = new System.Text.StringBuilder("{\n  \"captions\": [\n");
            float end = Time.realtimeSinceStartup - RecordStart;
            for (int i = 0; i < _caps.Count; i++)
            {
                float s = _caps[i].start;
                float e = (i + 1 < _caps.Count) ? _caps[i + 1].start : end;
                sb.Append("    {\"start\": ").Append(s.ToString("F2"))
                  .Append(", \"end\": ").Append(e.ToString("F2"))
                  // Escape the NEWLINE too, not just the quote. A caption's line break is a real
                  // '\n' in the C# literal, and writing it raw puts a control character inside a
                  // JSON string — which json.load rejects outright, so build_bot_video.py cannot
                  // read the sidecar at all.
                  .Append(", \"text\": \"")
                  .Append(_caps[i].text.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n"))
                  .Append("\"}").Append(i + 1 < _caps.Count ? ",\n" : "\n");
            }
            sb.Append("  ]\n}\n");
            Directory.CreateDirectory(CaptionDir);
            File.WriteAllText(CaptionDir + "/captions.json", sb.ToString());
            Debug.Log("[StoreHistoryDemo] wrote " + _caps.Count + " captions.");
        }

        public void StartDemo() => StartCoroutine(Sequence());

        static ScreenId? Now => ScreenManager.Instance?.CurrentScreen;

        static bool Under(Transform t, string rootName)
        {
            while (t != null) { if (t.name == rootName) return true; t = t.parent; }
            return false;
        }

        static Button Live(string name, string underRoot = null) =>
            Resources.FindObjectsOfTypeAll<Button>().FirstOrDefault(b =>
                b.gameObject.name == name &&
                !string.IsNullOrEmpty(b.gameObject.scene.name) &&
                b.GetComponentInParent<Canvas>() != null &&
                (underRoot == null || Under(b.transform, underRoot)));

        IEnumerator WaitFor(ScreenId id, float seconds)
        {
            float t = 0f;
            while (t < seconds && Now != id) { t += Time.unscaledDeltaTime; yield return null; }
        }

        IEnumerator ScrollTo(ScrollRect sr, float from, float to, float dur)
        {
            if (sr == null) yield break;
            float t = 0f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                sr.verticalNormalizedPosition = Mathf.Lerp(from, to, Mathf.Clamp01(t / dur));
                yield return null;
            }
            sr.verticalNormalizedPosition = to;
        }

        IEnumerator Sequence()
        {
            Application.runInBackground = true;

            // ── Through the Splash gate, by tapping the REAL StartButton.
            float t = 0f;
            while (t < 25f)
            {
                var splash = FindFirstObjectByType<SplashScreenController>();
                var btn = splash == null ? null : splash.transform.Find("StartButton");
                if (btn != null && btn.gameObject.activeInHierarchy)
                {
                    btn.GetComponent<Button>()?.onClick.Invoke();
                    Debug.Log("[StoreHistoryDemo] tapped StartButton");
                    break;
                }
                t += Time.unscaledDeltaTime;
                yield return null;
            }

            yield return WaitFor(ScreenId.Home, 60f);
            yield return new WaitForSecondsRealtime(Settle);

            // ── The STORE grid, entered from the top-bar "+". This is the §8 path: the first card
            //    used to sit too high with a card-sized hole under it.
            var pum = FindFirstObjectByType<Golfin.UI.PersistentUIManager>(FindObjectsInactive.Include);
            Cap("Rewards Center from the top-bar +");
            if (pum != null && pum.shopPlusButton != null) pum.shopPlusButton.onClick.Invoke();
            yield return WaitFor(ScreenId.GeneralShop, 20f);
            yield return new WaitForSecondsRealtime(Settle + 1.0f);
            Cap("Cards now rise into their real slots\nno gap under the first one");
            yield return new WaitForSecondsRealtime(Hold);

            // ── The History chip on the STORE tab. It used to show a "coming soon" toast.
            var chip = Live("HistoryChip", "GeneralShopScreen");
            if (chip != null) chip.onClick.Invoke();
            yield return WaitFor(ScreenId.StoreHistory, 20f);
            yield return new WaitForSecondsRealtime(Settle);
            Cap("The STORE History chip opens a screen\ninstead of a coming-soon toast");
            yield return new WaitForSecondsRealtime(Hold);

            Cap("Your own purchases, from the server\nnewest first");
            yield return new WaitForSecondsRealtime(Hold);

            Cap("PRICE is what was charged\nso a sale price stays a sale price");
            yield return new WaitForSecondsRealtime(Hold);

            // ── Scroll the log.
            var sr = Resources.FindObjectsOfTypeAll<ScrollRect>().FirstOrDefault(s =>
                !string.IsNullOrEmpty(s.gameObject.scene.name) &&
                s.GetComponentInParent<Canvas>() != null && Under(s.transform, "StoreHistoryScreen"));
            yield return ScrollTo(sr, 1f, 0.15f, 2.5f);
            yield return new WaitForSecondsRealtime(0.8f);
            yield return ScrollTo(sr, 0.15f, 1f, 1.8f);
            yield return new WaitForSecondsRealtime(0.6f);

            // ── The chip row, wired here for the first time.
            var ctrl = FindFirstObjectByType<GolfinRedux.UI.Shop.StoreHistoryScreenController>(FindObjectsInactive.Include);
            Button Chip(string n) => ctrl == null ? null
                : ctrl.transform.Find("GameScreenContent/ContentContainer/FiltersBlock/CategoryRow/" + n)?.GetComponent<Button>();

            Cap("The category chips filter the log");
            Chip("TICKETSChip")?.onClick.Invoke();
            yield return new WaitForSecondsRealtime(Hold);
            Chip("BALLSChip")?.onClick.Invoke();
            yield return new WaitForSecondsRealtime(Hold);
            Chip("ALLChip")?.onClick.Invoke();
            yield return new WaitForSecondsRealtime(Settle + 0.8f);

            // ── Over to the gacha log, through the strip's own GACHA tab, for the two card fixes.
            var gachaTab = ctrl == null ? null
                : ctrl.transform.Find("GameScreenContent/ContentContainer/FiltersBlock/TabBar/DailyTab")?.GetComponent<Button>();
            gachaTab?.onClick.Invoke();
            yield return WaitFor(ScreenId.GeneralShop, 20f);
            yield return new WaitForSecondsRealtime(Settle);

            var chip2 = Live("HistoryChip", "GeneralShopScreen");
            if (chip2 != null) chip2.onClick.Invoke();
            yield return WaitFor(ScreenId.GachaHistory, 20f);
            yield return new WaitForSecondsRealtime(Settle + 0.8f);
            Cap("In the pull log, every prize kind\nnow draws its own card");
            yield return new WaitForSecondsRealtime(Hold);

            var gsr = Resources.FindObjectsOfTypeAll<ScrollRect>().FirstOrDefault(s =>
                !string.IsNullOrEmpty(s.gameObject.scene.name) &&
                s.GetComponentInParent<Canvas>() != null && Under(s.transform, "GachaHistoryScreen"));
            yield return ScrollTo(gsr, 1f, 0.5f, 2.2f);
            Cap("A short description sizes up\nto fill the card it sits on");
            yield return new WaitForSecondsRealtime(Hold + 0.8f);
            yield return ScrollTo(gsr, 0.5f, 0.05f, 2.2f);
            yield return new WaitForSecondsRealtime(Hold);

            WriteCaptions();
            Debug.Log("[StoreHistoryDemo] Sequence done — exiting play mode.");
            EditorApplication.ExitPlaymode();
        }
    }
}
#endif
