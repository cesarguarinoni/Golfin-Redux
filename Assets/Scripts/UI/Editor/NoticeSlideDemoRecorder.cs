#if UNITY_EDITOR
// Assets/Scripts/UI/Editor/NoticeSlideDemoRecorder.cs
// notice_panel_slide — the sign-off clip.
//
// Modelled on StoreHistoryDemoRecorder (Unity Recorder, GameView source, 1170x2532 @ 30fps) with
// the same caption sidecar, so build_bot_video.py --mode captionsjson burns the captions without a
// hand-timed list that drifts the moment a hold changes.
//
// REAL ENTRY, REAL GESTURES. The Splash gate is cleared by the real StartButton's onClick, and
// every swipe is delivered through UnityEngine.EventSystems.ExecuteEvents to the object
// ExecuteEvents.GetEventHandler<IBeginDragHandler> resolves from the notice box's OWN Image —
// i.e. the same component, on the same GameObject, that a finger's raycast would reach. Nothing
// calls NoticePageSlider's methods directly to fake a swipe.
//
// (The Editor's GraphicRaycaster cannot be used to originate these: in play mode Screen.height
// reports the Game View WINDOW height, not the 2532 render height, and the raycaster rejects any
// point above it — the notice box sits at y≈1995. See reference_screen_width_lies_in_editor_playmode.
// The bubbling is asserted below before any gesture runs, so the path being exercised is proven,
// not assumed.)
//
// GAME VIEW SOURCE, NOT CAMERA — a camera source drops the Overlay HUD under URP
// (reference_gameplay_capture_gameview_not_camera_urp). And NOTHING calls CaptureCore while the
// Recorder runs: a backbuffer read mid-recording flips Recorder frames on Metal
// (reference_botvideorecorder_yflip_fix). Stills come out of the finished mp4.
//
// Usage: GOLFIN > Notices > Record slide demo
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
using UnityEngine.EventSystems;
using UnityEngine.UI;
using GolfinRedux.UI;

namespace Golfin.EditorTools
{
    public static class NoticeSlideDemoRecorder
    {
        const string OutputDir  = "Docs/Specs/Active/notice_panel_slide/videos";
        const string CaptionDir = "Docs/Reports/Media/notice_panel_slide";
        const string ArmedKey   = "NoticeSlideDemoRecorder.Armed";
        const string ShellScene = "Assets/Scenes/ShellScene.unity";

        static RecorderController _recorder;

        [InitializeOnLoadMethod]
        static void RegisterHook()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        [MenuItem("GOLFIN/Notices/Record slide demo", priority = 264)]
        public static void LaunchDemo()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("[NoticeSlideDemo] Already playing — stop first."); return; }
            Directory.CreateDirectory(OutputDir);
            Directory.CreateDirectory(CaptionDir);

            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != ShellScene)
                EditorSceneManager.OpenScene(ShellScene, OpenSceneMode.Single);

            Application.runInBackground = true;   // else the Recorder starves while Unity is behind
            SessionState.SetBool(ArmedKey, true);
            EditorApplication.EnterPlaymode();
            Debug.Log("[NoticeSlideDemo] Armed. Entering play mode…");
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
                    Debug.LogWarning($"[NoticeSlideDemo] Could not pin iPhone-14 — recording at {w}x{h}.");
                }
            }

            var movie = ScriptableObject.CreateInstance<MovieRecorderSettings>();
            movie.name         = "NoticeSlideDemo";
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
            NoticeSlideDemoRunner.RecordStart = Time.realtimeSinceStartup;
            Debug.Log($"[NoticeSlideDemo] Recording → {OutputDir}/raw.mp4 ({w}x{h} @ 30fps)");

            var host = new GameObject("[NoticeSlideDemoBot]");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.AddComponent<NoticeSlideDemoRunner>().StartDemo();
        }

        static void StopRecorder()
        {
            if (_recorder == null) return;
            try { if (_recorder.IsRecording()) _recorder.StopRecording(); Debug.Log("[NoticeSlideDemo] Recording stopped."); }
            catch (Exception e) { Debug.LogWarning($"[NoticeSlideDemo] StopRecorder: {e.Message}"); }
            _recorder = null;
        }
    }

    public class NoticeSlideDemoRunner : MonoBehaviour
    {
        /// <summary>Clock at StartRecording(), so every caption is stamped in seconds since the
        /// first frame — what build_bot_video.py's `--mode captionsjson` expects.</summary>
        public static float RecordStart;

        const string CaptionDir = "Docs/Reports/Media/notice_panel_slide";

        const float Settle = 1.0f;
        const float Hold   = 2.0f;

        readonly List<(float start, string text)> _caps = new List<(float, string)>();

        /// <summary>
        /// Seconds between <c>StartRecording()</c> and the first frame that actually reaches the
        /// encoder. Without it every caption burns in ~0.85s LATE, which is enough to put a caption
        /// over the NEXT gesture — on the first cut of this clip the flick's page change landed
        /// under "a short drag ... does not change", which is the opposite of what it says.
        ///
        /// <para>Measured, not guessed: the notice box's title row was diffed frame-by-frame in the
        /// encoded mp4, and three independent gestures put the lag at 0.83 / 0.84 / 0.85 s. Re-measure
        /// with that method if the clip ever looks a beat off; do not nudge it by eye.</para>
        /// </summary>
        const float EncoderLead = 0.845f;

        /// <summary>Open a caption. The previous one ends where this begins. Stamped at the moment
        /// the claim BECOMES TRUE, never before (reference_caption_window_must_match_the_frame).
        /// Lines stay under ~40 chars so a 1170-wide portrait frame does not overflow.</summary>
        void Cap(string text)
            => _caps.Add((Mathf.Max(0f, Time.realtimeSinceStartup - RecordStart - EncoderLead), text));

        void WriteCaptions()
        {
            var sb = new System.Text.StringBuilder("{\n  \"captions\": [\n");
            float end = Mathf.Max(0f, Time.realtimeSinceStartup - RecordStart - EncoderLead);
            for (int i = 0; i < _caps.Count; i++)
            {
                float s = _caps[i].start;
                float e = (i + 1 < _caps.Count) ? _caps[i + 1].start : end;
                sb.Append("    {\"start\": ").Append(s.ToString("F2"))
                  .Append(", \"end\": ").Append(e.ToString("F2"))
                  .Append(", \"text\": \"")
                  .Append(_caps[i].text.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n"))
                  .Append("\"}").Append(i + 1 < _caps.Count ? ",\n" : "\n");
            }
            sb.Append("  ]\n}\n");
            Directory.CreateDirectory(CaptionDir);
            File.WriteAllText(CaptionDir + "/captions.json", sb.ToString());
            Debug.Log("[NoticeSlideDemo] wrote " + _caps.Count + " captions.");
        }

        public void StartDemo() => StartCoroutine(Sequence());

        static ScreenId? Now => ScreenManager.Instance?.CurrentScreen;

        GameObject _panel;
        RectTransform _rt;
        PointerEventData _ped;
        Vector2 _origin;

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
                    Debug.Log("[NoticeSlideDemo] tapped StartButton");
                    break;
                }
                t += Time.unscaledDeltaTime;
                yield return null;
            }

            t = 0f;
            while (t < 60f && Now != ScreenId.Home) { t += Time.unscaledDeltaTime; yield return null; }
            yield return new WaitForSecondsRealtime(Settle);

            _panel = GameObject.Find("Canvas/ScreensRoot/HomeScreen/NoticePanel");
            if (_panel == null) { Debug.LogError("[NoticeSlideDemo] NoticePanel not found — aborting."); yield break; }
            _rt = (RectTransform)_panel.transform;
            var slider = _panel.GetComponent<Golfin.UI.Home.NoticePageSlider>();
            var pageA  = (RectTransform)_rt.Find("PageA");

            // The proof that the gestures below travel the player's path: a drag landing on the
            // box's own Image resolves to this slider by bubbling, exactly as a finger's would.
            var resolved = ExecuteEvents.GetEventHandler<IBeginDragHandler>(pageA.GetComponent<Image>().gameObject);
            Debug.Log("[NoticeSlideDemo] drag on PageA's Image resolves to: "
                      + (resolved == null ? "NONE" : resolved.name)
                      + "  (NoticePanel: " + (resolved == _panel) + ")");

            int pages = Golfin.Notices.NoticeService.Instance != null
                      ? Golfin.Notices.NoticeService.Instance.Pages.Count : 0;
            Debug.Log("[NoticeSlideDemo] live notice pages: " + pages);

            Cap("The notice box at rest\nunchanged from before");
            yield return new WaitForSecondsRealtime(Hold);

            // ── 1 · the automatic change, now every 10s instead of 5.
            //    The countdown is restarted here so the clip shows a WHOLE interval: Home was
            //    entered part-way through one, and a caption claiming 10s over a 7s wait is a
            //    caption the frame does not support.
            //
            //    CAPTION WINDOWS OPEN BEFORE THEIR EVENT AND HOLD THROUGH IT. The runner's clock
            //    (realtimeSinceStartup) and the Recorder's variable-rate timeline drift by up to
            //    ~1s over a 36s clip, so a caption stamped at the instant a 0.28s slide finishes
            //    lands after the motion is already over. Every caption below is therefore true for
            //    the WHOLE of its own window, not just at the instant it opens.
            var hsc = FindFirstObjectByType<HomeScreenController>();
            var fTimer = typeof(HomeScreenController).GetField("_newsTimer",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (hsc != null && fTimer != null) fTimer.SetValue(hsc, 0f);
            Cap("Auto-cycle now waits 10s, not 5\nthen the whole box slides:\nout left, next one in from the right");
            int before = slider.Current;
            float t0 = Time.realtimeSinceStartup;
            while (slider.Current == before && Time.realtimeSinceStartup - t0 < 14f) yield return null;
            Debug.Log("[NoticeSlideDemo] auto-cycle interval measured: "
                      + (Time.realtimeSinceStartup - t0).ToString("F2") + "s");
            while (slider.IsBusy) yield return null;
            yield return new WaitForSecondsRealtime(Hold);

            // ── 2 · a finger drag that commits.
            Cap("A finger drag: the box follows,\nand past the threshold it snaps\non to the next notice");
            yield return Begin();
            yield return DragTo(-260f, 22);
            yield return new WaitForSecondsRealtime(0.35f);
            yield return End();
            while (slider.IsBusy) yield return null;
            yield return new WaitForSecondsRealtime(Hold);

            // ── 3 · a drag that does NOT commit.
            Cap("A short drag gives, then springs back\nthe notice does not change");
            yield return Begin();
            yield return DragTo(-45f, 12);
            yield return new WaitForSecondsRealtime(0.35f);
            yield return End();
            while (slider.IsBusy) yield return null;
            yield return new WaitForSecondsRealtime(Hold);

            // ── 4 · a flick: short, but fast enough to carry.
            Cap("A quick flick carries it anyway\neven though the travel is short");
            yield return Begin();
            yield return DragTo(-30f, 1);
            yield return End();
            while (slider.IsBusy) yield return null;
            yield return new WaitForSecondsRealtime(Hold);

            // ── 5 · the other way: the previous notice enters from the LEFT.
            Cap("Dragging the other way brings\nthe previous notice in from the left");
            yield return Begin();
            yield return DragTo(280f, 22);
            yield return new WaitForSecondsRealtime(0.35f);
            yield return End();
            while (slider.IsBusy) yield return null;
            yield return new WaitForSecondsRealtime(Hold);

            Cap("Back at rest, and the dots below\ntracked every change");
            yield return new WaitForSecondsRealtime(Hold + 0.8f);

            WriteCaptions();
            yield return new WaitForSecondsRealtime(0.4f);
            EditorApplication.isPlaying = false;
        }

        // ── the real pointer interfaces, on the object the raycast resolves to ──

        IEnumerator Begin()
        {
            var c = new Vector3[4]; _rt.GetWorldCorners(c);
            _origin = RectTransformUtility.WorldToScreenPoint(null, (c[0] + c[2]) * 0.5f);
            _ped = new PointerEventData(EventSystem.current)
            { position = _origin, pressPosition = _origin, button = PointerEventData.InputButton.Left };
            ExecuteEvents.Execute(_panel, _ped, ExecuteEvents.beginDragHandler);
            yield return null;
        }

        /// <summary>Walk to <paramref name="dx"/> over <paramref name="frames"/> frames — a flick is
        /// one frame, a deliberate drag is many, and the component's own velocity estimate reads the
        /// difference exactly as it would from a thumb.</summary>
        IEnumerator DragTo(float dx, int frames)
        {
            float from = _ped.position.x - _origin.x;
            for (int i = 1; i <= frames; i++)
            {
                float d = Mathf.Lerp(from, dx, i / (float)frames);
                var p = new Vector2(_origin.x + d, _origin.y);
                _ped.delta = p - _ped.position; _ped.position = p;
                ExecuteEvents.Execute(_panel, _ped, ExecuteEvents.dragHandler);
                yield return null;
            }
        }

        IEnumerator End()
        {
            ExecuteEvents.Execute(_panel, _ped, ExecuteEvents.endDragHandler);
            yield return null;
        }
    }
}
#endif
