#if UNITY_EDITOR
using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Input;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Golfin.Gameplay.UI.ShotUI;

namespace Golfin.EditorTools
{
    /// <summary>
    /// Demo recorder for Order 355 (map_view_strict_crop_indicators).
    ///
    /// Shows, through the REAL player entry path and nothing else:
    ///   boot → PLAY → mode card → Hole 1 card ActionButton → hole load →
    ///   (recording starts) →
    ///   tap the real HoleMap button → map opens (strict crop; ball seated low;
    ///   flag off-screen so its indicator floats at the edge with an arrow) →
    ///   pan toward the hole (indicator walks the edge, then DOCKS over the hole;
    ///   the ball leaves frame and its own indicator appears pointing back) →
    ///   pan to the containment stop (camera stops dead, no off-course revealed) →
    ///   tap SHOOT → map closes, world restored.
    ///
    /// Every camera move goes through the PRODUCTION <c>MapViewController.PanCamera</c>
    /// and every UI interaction through the real widget's <c>onClick</c> — nothing in this
    /// file re-implements framing, clamping or indicator math.
    ///
    /// NOT shown: pinch. The Editor has no <c>Touchscreen</c>, so the two-finger branch of
    /// <c>HandleTouchInput</c> cannot be driven honestly; the zoom-out gate is covered by the
    /// EditMode tests and the numbers in IMPLEMENTER_REPORT §5(c) instead of a staged frame.
    ///
    /// GameView input (not TaggedCamera): the indicators live on a ScreenSpaceOverlay canvas,
    /// which a camera-source recording drops under URP (see memory
    /// `reference_gameplay_capture_gameview_not_camera_urp`).
    ///
    /// Output raw: tasks/loop_v2_smoke_bot/map_view_strict_crop/video/raw.mp4
    /// Captions:   tasks/loop_v2_smoke_bot/map_view_strict_crop/screenshots/history.log
    ///             (`Step:` lines → Docs/Scripts/build_bot_video.py --mode steps)
    /// Usage: GOLFIN > MapView > Record Strict Crop Demo Video
    /// </summary>
    public static class MapViewStrictCropDemoRecorder
    {
        const string ShellScenePath = "Assets/Scenes/ShellScene.unity";
        const string ScenarioDir    = "tasks/loop_v2_smoke_bot/map_view_strict_crop";
        const string ArmedKey       = "MapViewStrictCropDemoRecorder.Armed";
        const int    Fps            = 30;

        static RecorderController _recorder;
        static StringBuilder      _log;
        static float              _recordStart;

        internal static string VideoDir  => $"{ScenarioDir}/video";
        internal static string ShotsDir  => $"{ScenarioDir}/screenshots";

        [InitializeOnLoadMethod]
        static void RegisterHook()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        [MenuItem("GOLFIN/MapView/Record Strict Crop Demo Video")]
        public static void LaunchDemo()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[MapViewStrictCropDemo] Already in play mode — stop first.");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            EditorSceneManager.OpenScene(ShellScenePath);
            Directory.CreateDirectory(VideoDir);
            Directory.CreateDirectory(ShotsDir);
            SessionState.SetBool(ArmedKey, true);
            EditorApplication.EnterPlaymode();
            Debug.Log("[MapViewStrictCropDemo] Armed. Entering play mode...");
        }

        static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                if (!SessionState.GetBool(ArmedKey, false)) return;
                SessionState.SetBool(ArmedKey, false);
                var host = new GameObject("[MapViewStrictCropDemoBot]");
                UnityEngine.Object.DontDestroyOnLoad(host);
                host.AddComponent<MapViewStrictCropDemoRunner>();
                Debug.Log("[MapViewStrictCropDemo] Bot spawned. Waiting for hole load...");
            }
            else if (state == PlayModeStateChange.ExitingPlayMode)
            {
                if (_recorder != null) StopRecorderAndWriteLog();
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
                    Debug.LogWarning($"[MapViewStrictCropDemo] Recording at {w}x{h} (not iPhone-14).");
                }
            }

            // Lock render state BEFORE StartRecording — the BotVideoRecorder Y-flip rule: any
            // render-state change after StartRecording flips the output.
            QualitySettings.vSyncCount  = 0;
            Application.targetFrameRate = Fps;

            var movie = ScriptableObject.CreateInstance<MovieRecorderSettings>();
            movie.name         = "MapViewStrictCropDemo";
            movie.Enabled      = true;
            movie.OutputFormat = MovieRecorderSettings.VideoRecorderOutputFormat.MP4;
            movie.ImageInputSettings = new GameViewInputSettings { OutputWidth = w, OutputHeight = h };
            movie.AudioInputSettings.PreserveAudio = false;
            movie.OutputFile = $"{VideoDir}/raw";

            var settings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
            settings.AddRecorderSettings(movie);
            settings.SetRecordModeToManual();
            settings.FrameRate = Fps;
            settings.FrameRatePlayback = FrameRatePlayback.Variable;

            _recorder = new RecorderController(settings);
            _recorder.PrepareRecording();
            _recorder.StartRecording();

            _recordStart = Time.realtimeSinceStartup;
            _log = new StringBuilder();
            Debug.Log($"[MapViewStrictCropDemo] Recording started → {VideoDir}/raw.mp4 ({w}x{h} @ {Fps}fps)");
        }

        /// <summary>Emit one caption line for build_bot_video.py --mode steps.</summary>
        public static void Step(string text)
        {
            if (_log == null) return;
            float t = Time.realtimeSinceStartup;
            _log.AppendLine($"[t={t.ToString("F3", CultureInfo.InvariantCulture)}] Step: '{text}'");
            Debug.Log($"[MapViewStrictCropDemo] Step: {text}");
        }

        static void StopRecorderAndWriteLog()
        {
            try
            {
                if (_recorder.IsRecording()) _recorder.StopRecording();
                Debug.Log("[MapViewStrictCropDemo] Recording stopped.");
            }
            catch (Exception e) { Debug.LogWarning($"[MapViewStrictCropDemo] StopRecorder: {e.Message}"); }
            _recorder = null;

            Directory.CreateDirectory(VideoDir);
            Directory.CreateDirectory(ShotsDir);
            File.WriteAllText($"{VideoDir}/record_info.json",
                "{\"record_start_realtime\": " +
                _recordStart.ToString("F4", CultureInfo.InvariantCulture) +
                ", \"mp4\": \"" + VideoDir + "/raw.mp4\", \"fps\": " + Fps + "}");
            if (_log != null) File.WriteAllText($"{ShotsDir}/history.log", _log.ToString());
            Debug.Log($"[MapViewStrictCropDemo] record_info.json + history.log written under {ScenarioDir}");
        }
    }

    /// <summary>Runtime coroutine driver. Real widget clicks + production PanCamera only.</summary>
    public class MapViewStrictCropDemoRunner : MonoBehaviour
    {
        const int HoleNumber = 1;

        static readonly BindingFlags NP = BindingFlags.NonPublic | BindingFlags.Instance;

        void Start() => StartCoroutine(Sequence());

        // ── Real-widget helpers ──────────────────────────────────────────────

        static Button FindButton(string goName) => UnityEngine.Object
            .FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
            .FirstOrDefault(b => b.gameObject.name == goName);

        static void ClickReal(Button b)
        {
            var ped = new PointerEventData(EventSystem.current);
            ExecuteEvents.Execute(b.gameObject, ped, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(b.gameObject, ped, ExecuteEvents.pointerUpHandler);
            b.onClick.Invoke();
        }

        IEnumerator ClickWhenPresent(string goName, float timeout = 90f)
        {
            float t = 0f;
            while (t < timeout)
            {
                var b = FindButton(goName);
                if (b != null) { ClickReal(b); yield break; }
                yield return new WaitForSecondsRealtime(0.25f); t += 0.25f;
            }
            Debug.LogWarning($"[MapViewStrictCropBot] TIMEOUT waiting for '{goName}'");
        }

        IEnumerator ClickHoleCard(int hole, float timeout = 60f)
        {
            float t = 0f;
            while (t < timeout)
            {
                foreach (var c in UnityEngine.Object
                             .FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                             .Where(m => m.GetType().Name == "HoleCardController"))
                {
                    var p = c.GetType().GetProperty("HoleNumber");
                    if (p == null || (int)p.GetValue(c) != hole) continue;
                    var btn = c.GetType().GetField("actionButton", NP)?.GetValue(c) as Button;
                    if (btn == null) continue;
                    ClickReal(btn);
                    yield break;
                }
                yield return new WaitForSecondsRealtime(0.25f); t += 0.25f;
            }
            Debug.LogWarning($"[MapViewStrictCropBot] TIMEOUT waiting for hole {hole} card");
        }

        static MapViewController FindMvc() => UnityEngine.Object
            .FindObjectsByType<MapViewController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
            .FirstOrDefault();

        /// <summary>Drive the PRODUCTION PanCamera once per frame so the pan reads as a real drag.</summary>
        IEnumerator SmoothPan(MapViewController mvc, Vector2 perFrameDelta, float seconds)
        {
            var pan = typeof(MapViewController).GetMethod("PanCamera", NP);
            float end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end)
            {
                pan.Invoke(mvc, new object[] { perFrameDelta });
                yield return null;
            }
        }

        // ── Sequence ─────────────────────────────────────────────────────────

        IEnumerator Sequence()
        {
            Application.runInBackground = true;

            yield return new WaitForSecondsRealtime(5f);
            yield return ClickWhenPresent("StartButton");
            yield return new WaitForSecondsRealtime(2.5f);
            yield return ClickWhenPresent("PlayButton");
            yield return new WaitForSecondsRealtime(2.5f);
            yield return ClickHoleCard(HoleNumber);

            // Wait for the hole to finish loading — the real HoleMap button only exists then.
            float t = 0f;
            while (FindButton("HoleMap") == null && t < 120f)
            { yield return new WaitForSecondsRealtime(0.5f); t += 0.5f; }
            yield return new WaitForSecondsRealtime(4f);   // let the HUD settle

            MapViewStrictCropDemoRecorder.StartRecorder();
            yield return new WaitForSecondsRealtime(0.5f);

            MapViewStrictCropDemoRecorder.Step("Hole 1, at the tee — open the hole map");
            yield return new WaitForSecondsRealtime(2.4f);
            yield return ClickWhenPresent("HoleMap");

            // The §11 invariant dump re-aims twice right after open (SetAimYawDirectly →
            // PositionMapCamera), so give it room before panning or the pan gets re-framed away.
            MapViewStrictCropDemoRecorder.Step("Every pixel is playable area — no world beyond the hole");
            yield return new WaitForSecondsRealtime(4.5f);

            MapViewStrictCropDemoRecorder.Step("Ball sits low; the flag is off-screen, so its\\nindicator floats at the edge with an arrow");
            yield return new WaitForSecondsRealtime(3.4f);

            var mvc = FindMvc();
            if (mvc != null)
            {
                MapViewStrictCropDemoRecorder.Step("Pan toward the hole — the indicator walks the edge");
                yield return SmoothPan(mvc, new Vector2(9f, -11f), 4.0f);

                MapViewStrictCropDemoRecorder.Step("It docks over the hole the moment the hole enters view");
                yield return new WaitForSecondsRealtime(3.0f);

                MapViewStrictCropDemoRecorder.Step("The ball is off-screen now — its own indicator\\npoints back at it, clear of the SHOOT button");
                yield return new WaitForSecondsRealtime(3.4f);

                MapViewStrictCropDemoRecorder.Step("Keep panning — the crop stops the camera dead\\nat the boundary. No off-course is ever revealed");
                yield return SmoothPan(mvc, new Vector2(14f, -14f), 4.5f);
                yield return new WaitForSecondsRealtime(2.0f);

                MapViewStrictCropDemoRecorder.Step("Pan back toward the ball");
                yield return SmoothPan(mvc, new Vector2(-14f, 14f), 4.0f);
                yield return new WaitForSecondsRealtime(1.5f);
            }

            MapViewStrictCropDemoRecorder.Step("SHOOT closes the map and writes the aim back");
            yield return new WaitForSecondsRealtime(2.2f);
            var shoot = typeof(MapViewController).GetField("_shootButton", NP).GetValue(mvc) as Button;
            if (shoot != null) ClickReal(shoot);

            MapViewStrictCropDemoRecorder.Step("World restored — back to the shot");
            yield return new WaitForSecondsRealtime(3.5f);

            EditorApplication.ExitPlaymode();
        }
    }
}
#endif
