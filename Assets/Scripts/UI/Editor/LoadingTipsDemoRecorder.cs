#if UNITY_EDITOR
// Assets/Scripts/UI/Editor/LoadingTipsDemoRecorder.cs
// loading_tips — the daily-report clip.
//
// Modelled on LoanDemoRecorder / StoreHistoryDemoRecorder: Unity Recorder, GAME VIEW source,
// 1170x2532 @ 30fps, with the caption sidecar build_bot_video.py --mode captionsjson burns off
// timestamps the run itself recorded, so a caption cannot drift when a hold changes.
//
// EVERY TAP IS A REAL WIDGET. The Splash StartButton, the Home mode card's PlayButton, the hole
// card's ACTION button, and — for the tips themselves — ProTipCard.OnPointerClick, which IS the
// card's handler (it is an IPointerClickHandler, not a Button). No ShowScreen(target) shortcut.
//
// ONE INSTRUMENT, AND IT IS NOT THE SCREEN. LoadingScreenController.minLoadingTime is widened at
// RUNTIME so the real loading screen stays up long enough to read several tips. Nothing else is
// faked: the screen, the navigation into it, the catalog, the sprites and the motion are shipped
// code. The field is a serialized default; the write is runtime-only and dies with play mode.
//
// GAME VIEW SOURCE, NOT CAMERA — a camera source drops the Overlay HUD under URP
// (reference_gameplay_capture_gameview_not_camera_urp). And NOTHING calls CaptureCore while the
// Recorder runs: a backbuffer read mid-recording flips Recorder frames on Metal
// (reference_botvideorecorder_yflip_fix). Stills come out of the finished mp4.
//
// Usage: GOLFIN > Loading Tips > Record demo
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
using UnityEngine.EventSystems;
using UnityEngine.UI;
using GolfinRedux.UI;

namespace Golfin.EditorTools
{
    public static class LoadingTipsDemoRecorder
    {
        const string OutputDir  = "Docs/Specs/Active/loading_tips/videos";
        const string CaptionDir = "Docs/Reports/Media/loading_tips";
        const string ArmedKey   = "LoadingTipsDemoRecorder.Armed";
        const string ShellScene = "Assets/Scenes/ShellScene.unity";

        static RecorderController _recorder;

        [InitializeOnLoadMethod]
        static void RegisterHook()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        [MenuItem("GOLFIN/Loading Tips/Record demo", priority = 264)]
        public static void LaunchDemo()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("[TipsDemo] Already playing — stop first."); return; }
            Directory.CreateDirectory(OutputDir);
            Directory.CreateDirectory(CaptionDir);

            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != ShellScene)
                EditorSceneManager.OpenScene(ShellScene, OpenSceneMode.Single);

            // A FRESH INSTALL is the story: the first pool, in order, from tip 1.
            LoadingTipStore.Clear();

            Application.runInBackground = true;   // else the Recorder starves while Unity is behind
            SessionState.SetBool(ArmedKey, true);
            EditorApplication.EnterPlaymode();
            Debug.Log("[TipsDemo] Armed (tip state cleared). Entering play mode…");
        }

        static void OnPlayModeChanged(PlayModeStateChange state)
        {
            // Stopping is unconditional and idempotent — see LoanDemoRecorder's note: gating the
            // stop on the armed flag is how you get an mp4 with every frame and no moov atom.
            if (state == PlayModeStateChange.ExitingPlayMode) { StopRecorder(); return; }

            if (!SessionState.GetBool(ArmedKey, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                SessionState.SetBool(ArmedKey, false);
                StartRecorderAndBot();
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
                    Debug.LogWarning($"[TipsDemo] Could not pin iPhone-14 — recording at {w}x{h}.");
                }
            }

            var movie = ScriptableObject.CreateInstance<MovieRecorderSettings>();
            movie.name         = "LoadingTipsDemo";
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
            LoadingTipsDemoRunner.RecordStart = Time.realtimeSinceStartup;
            Debug.Log($"[TipsDemo] Recording → {OutputDir}/raw.mp4 ({w}x{h} @ 30fps)");

            var host = new GameObject("[LoadingTipsDemoBot]");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.AddComponent<LoadingTipsDemoRunner>().StartDemo();
        }

        static void StopRecorder()
        {
            if (_recorder == null) return;
            try { if (_recorder.IsRecording()) _recorder.StopRecording(); Debug.Log("[TipsDemo] Recording stopped."); }
            catch (Exception e) { Debug.LogWarning($"[TipsDemo] StopRecorder: {e.Message}"); }
            _recorder = null;
        }
    }

    public class LoadingTipsDemoRunner : MonoBehaviour
    {
        /// <summary>Clock at StartRecording(), so every caption is stamped in seconds since the
        /// first frame — what build_bot_video.py's `--mode captionsjson` expects.</summary>
        public static float RecordStart;

        const string CaptionDir = "Docs/Reports/Media/loading_tips";
        const float  Hold   = 3.6f;

        readonly List<(float start, string text)> _caps = new List<(float, string)>();
        readonly Dictionary<int, float> _capMaxEnd = new Dictionary<int, float>();

        /// <summary>Open a caption. The previous one ends where this begins. Stamped at the moment
        /// the claim BECOMES TRUE — a caption opened on the tap describes a screen the viewer
        /// cannot see yet (reference_caption_window_must_match_the_frame).</summary>
        void Cap(string text) => _caps.Add((Time.realtimeSinceStartup - RecordStart, text));

        /// <summary>A caption that ends after a fixed span instead of running to the next one.
        /// Only the TITLE uses it: build_bot_video.py draws caption 0 CENTRED, so left running it
        /// would sit over the tip card it exists to introduce.</summary>
        void CapFor(string text, float seconds)
        {
            _capMaxEnd[_caps.Count] = (Time.realtimeSinceStartup - RecordStart) + seconds;
            Cap(text);
        }

        void WriteCaptions()
        {
            var sb = new System.Text.StringBuilder("{\n  \"captions\": [\n");
            float end = Time.realtimeSinceStartup - RecordStart;
            for (int i = 0; i < _caps.Count; i++)
            {
                float s = _caps[i].start;
                float e = (i + 1 < _caps.Count) ? _caps[i + 1].start : end;
                if (_capMaxEnd.TryGetValue(i, out float cap)) e = Mathf.Min(e, cap);
                sb.Append("    {\"start\": ").Append(s.ToString("F2"))
                  .Append(", \"end\": ").Append(e.ToString("F2"))
                  .Append(", \"text\": \"")
                  .Append(_caps[i].text.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n"))
                  .Append("\"}").Append(i + 1 < _caps.Count ? ",\n" : "\n");
            }
            sb.Append("  ]\n}\n");
            Directory.CreateDirectory(CaptionDir);
            File.WriteAllText(CaptionDir + "/captions.json", sb.ToString());
            Debug.Log("[TipsDemo] wrote " + _caps.Count + " captions.");
        }

        public void StartDemo() => StartCoroutine(Sequence());

        // ── finding real widgets ─────────────────────────────────────────────

        static T FindLive<T>() where T : Component
            => Resources.FindObjectsOfTypeAll<T>()
                .FirstOrDefault(c => c != null
                                  && !string.IsNullOrEmpty(c.gameObject.scene.name)
                                  && c.gameObject.activeInHierarchy);

        static Button Live(string name)
            => Resources.FindObjectsOfTypeAll<Button>().FirstOrDefault(b =>
                   b.gameObject.name == name
                   && !string.IsNullOrEmpty(b.gameObject.scene.name)
                   && b.gameObject.activeInHierarchy
                   && b.interactable);

        static IEnumerator TapWhenLive(string name, float timeout = 20f)
        {
            float t = 0f;
            while (t < timeout)
            {
                Button b = Live(name);
                if (b != null) { b.onClick.Invoke(); Debug.Log("[TipsDemo] tapped " + name); yield break; }
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            Debug.LogWarning("[TipsDemo] never found an interactable " + name);
        }

        /// <summary>The card's OWN handler — ProTipCard is an IPointerClickHandler, so this is the
        /// same entry point a finger uses. Press feedback is driven too, because a real pointer
        /// sends down before click.</summary>
        static void TapCard(ProTipCard card)
        {
            var ped = new PointerEventData(EventSystem.current);
            (card.GetComponent("ButtonPressFeedback") as IPointerDownHandler)?.OnPointerDown(ped);
            card.OnPointerClick(ped);
        }

        /// <summary>Log which tip is on screen at each beat, so the recorded ORDER can be checked
        /// against the caption afterwards instead of taken on trust.</summary>
        static void LogTip(ProTipCard card, string label)
        {
            var tmp = card.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true)
                          .FirstOrDefault(x => x.name == "TipText");
            string head = tmp == null ? "?" : tmp.text;
            if (head.Length > 46) head = head.Substring(0, 46);
            Debug.Log("[TipsDemo] " + label + ": " + head.Replace("\n", " "));
        }

        IEnumerator Sequence()
        {
            Application.runInBackground = true;

            // NO TITLE CAPTION HERE. build_bot_video.py prepends its OWN centred title card
            // (its `--title`, which defaults to a stale "Loop v2 — Stage F"), so a title written
            // here is a second one — the first take burned both. Pass --title at build time.

            // ── through the Splash gate, by tapping the REAL StartButton ──────
            float t = 0f;
            while (t < 25f)
            {
                var splash = FindFirstObjectByType<SplashScreenController>();
                Transform btn = splash == null ? null : splash.transform.Find("StartButton");
                if (btn != null && btn.gameObject.activeInHierarchy)
                {
                    btn.GetComponent<Button>()?.onClick.Invoke();
                    Debug.Log("[TipsDemo] tapped StartButton");
                    break;
                }
                t += Time.unscaledDeltaTime;
                yield return null;
            }

            // Home settles.
            yield return new WaitForSecondsRealtime(4.0f);

            // ── the ONE instrument: hold the real loading screen open ─────────
            var lsc = Resources.FindObjectsOfTypeAll<LoadingScreenController>()
                .FirstOrDefault(c => c != null && !string.IsNullOrEmpty(c.gameObject.scene.name));
            FieldInfo minTime = typeof(LoadingScreenController)
                .GetField("minLoadingTime", BindingFlags.Instance | BindingFlags.NonPublic);
            if (lsc != null && minTime != null) minTime.SetValue(lsc, 90f);

            // ── a real round: Home → mode card PLAY → hole card ACTION ────────
            yield return TapWhenLive("PlayButton");
            yield return new WaitForSecondsRealtime(2.2f);
            yield return TapWhenLive("ActionButton");

            // ── wait for the loading screen the player just asked for ─────────
            ProTipCard card = null;
            t = 0f;
            while (t < 20f)
            {
                card = FindLive<ProTipCard>();
                if (card != null) break;
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            if (card == null) { Debug.LogWarning("[TipsDemo] no ProTipCard appeared"); WriteCaptions(); EditorApplication.ExitPlaymode(); yield break; }

            // THE AUTO-CYCLE IS PINNED FOR THE CLIP, and this is the difference between a caption
            // that is true and one that is not. autoCycleInterval is 8 s; held open for 40 s it
            // interleaves with the taps, so the first take showed SWING -> CLUB -> RARITIES ->
            // GRADES and the "walks the tutorial in order" caption described a sequence the viewer
            // could not see. Widened so the TAP is the only thing that advances; the order on
            // screen is then the catalog's own `order`, which is what the caption claims.
            FieldInfo cycle = typeof(ProTipCard)
                .GetField("autoCycleInterval", BindingFlags.Instance | BindingFlags.NonPublic);
            if (cycle != null) cycle.SetValue(card, 600f);

            yield return new WaitForSecondsRealtime(1.2f);
            Cap("A fresh install opens on tip 1\nand walks the tutorial in order");
            LogTip(card, "beat 1");
            yield return new WaitForSecondsRealtime(Hold);

            Cap("Every tip is drawn, and every one\nis about a system we ship today");
            yield return new WaitForSecondsRealtime(Hold);

            // ── the first pool, in order, one real tap at a time ──────────────
            // From a cleared store the order is deterministic: SWING, ACCURACY, GRADES, VIEW,
            // CLUB, FORECAST, RARITIES, CONTROLS.
            string[] beats =
            {
                // ONE CAPTION PER BEAT. A caption runs until the NEXT one opens, so a null here
                // let "Six shot grades now, not three" stay on screen through the map tip — a
                // caption describing a frame the viewer is no longer looking at
                // (reference_caption_window_must_match_the_frame).
                "Tap the card for the next tip.\nIt cross-fades and resizes",  // ACCURACY
                "Six shot grades now, not three",                             // GRADES
                "The map opens from the hole card",                           // VIEW
                "Tap the club button to open it,\nhold it to swap in one move",       // CLUB
                "The aim line is carry only.\nThe wind is on you",              // FORECAST
                "Rarity sets the stats and the cap",                          // RARITIES
                "Not a flick fan? There are\nthree other control schemes",      // CONTROLS
            };
            for (int i = 0; i < beats.Length; i++)
            {
                TapCard(card);
                yield return new WaitForSecondsRealtime(0.55f);
                LogTip(card, "beat " + (i + 2));
                Cap(beats[i]);
                yield return new WaitForSecondsRealtime(Hold);
            }

            // ── the same card in Japanese; this tap also starts pass 2 ────────
            LocalizationManager.SetLanguage(Language.Japanese);
            TapCard(card);
            yield return new WaitForSecondsRealtime(0.6f);
            LogTip(card, "japanese / pass 2");
            Cap("All 34 ship in Japanese too");
            yield return new WaitForSecondsRealtime(Hold + 0.8f);

            TapCard(card);
            yield return new WaitForSecondsRealtime(0.6f);
            LogTip(card, "japanese 2");
            yield return new WaitForSecondsRealtime(Hold - 0.8f);

            LocalizationManager.SetLanguage(Language.English);
            TapCard(card);
            yield return new WaitForSecondsRealtime(0.6f);
            LogTip(card, "final");
            Cap("The next loading screen picks up\nwhere this one left off");
            yield return new WaitForSecondsRealtime(Hold + 0.6f);

            WriteCaptions();

            // ExitPlaymode(), NOT `isPlaying = false` — the graceful path is what lets the
            // Recorder's ExitingPlayMode hook finalise the container.
            yield return new WaitForSecondsRealtime(0.8f);
            Debug.Log("[TipsDemo] Sequence done — exiting play mode.");
            EditorApplication.ExitPlaymode();
        }
    }
}
#endif
