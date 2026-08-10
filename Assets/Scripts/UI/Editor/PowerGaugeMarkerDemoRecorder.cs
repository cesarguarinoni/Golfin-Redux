#if UNITY_EDITOR
using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Golfin.Gameplay.UI.HUD;
using Golfin.Physics.Viewer;
using Golfin.Physics.Viewer.Editor;
using Golfin.Gameplay.UI.ShotUI;

namespace Golfin.EditorTools
{
    /// <summary>
    /// Demo recorder for Order 357 (power_gauge_target_marker) — the daily-report clip.
    ///
    /// Shows, through the REAL player entry path and nothing else:
    ///   boot → PLAY → mode card → Hole 1 card → hole load → (recording starts) →
    ///   pull the club handle (no target yet, clean gauge) → tap the real HoleMap button →
    ///   place a landing with the production TrySetAimFromScreenPoint → tap the real map SHOOT
    ///   button → the notch is now on the gauge at that % of club carry → pull to the notch →
    ///   swap club (target unmoved, so the notch moves and the yards text corrects) → flick →
    ///   next stroke is markerless again.
    ///
    /// Every interaction goes through a real widget's onClick or the production aim call; the
    /// gauge is driven by ShotController.BeginExternalDrag/SetExternalPower — the same pair
    /// ClubHandleDragger calls. Nothing here re-implements the marker math.
    ///
    /// RECORDING IS NOT HAND-ROLLED. It goes through the sanctioned engine,
    /// <see cref="Golfin.Physics.Viewer.Editor.BotVideoRecorder"/>, via the
    /// CustomOutputPath + ArmDeferred() / BeginDeferred() / End() contract — the same one
    /// TournamentLoopCaptureHarness and the OB/zone capture menus use. That engine owns the
    /// iPhone-14 Game View pinning, the full-res 1170x2532 output, the Y-flip render-state lock,
    /// the CaptureCore recording lock, record_info.json, the duration watchdog and the
    /// one-clip-per-Editor-session GPU guard. This file only supplies the SEQUENCE and captions.
    ///
    /// No Scenarios.cs entry is added (standing ban): the output path is set with
    /// BotVideoRecorder.CustomOutputPath and LoopV2SmokeBot.Scenario is set to the scenario key
    /// purely so record_info.json lands beside history.log for build_bot_video.py.
    ///
    /// Output raw: tasks/loop_v2_smoke_bot/power_gauge_target_marker/video/raw.mp4
    /// Captions:   tasks/loop_v2_smoke_bot/power_gauge_target_marker/screenshots/history.log
    ///             (`Step:` lines → Docs/Scripts/build_bot_video.py --mode steps)
    /// Usage: GOLFIN > ShotUI > Record Power Gauge Target Marker Demo
    /// </summary>
    public static class PowerGaugeMarkerDemoRecorder
    {
        const string ShellScenePath = "Assets/Scenes/ShellScene.unity";
        const string ScenarioDir    = "tasks/loop_v2_smoke_bot/power_gauge_target_marker";
        const string ArmedKey       = "PowerGaugeMarkerDemoRecorder.Armed";
        const string ScenarioKey    = "power_gauge_target_marker";

        /// <summary>Clip runs ~75s; BotVideoRecorder's default watchdog is 30s.</summary>
        const int WatchdogSeconds = 120;

        static StringBuilder _log;

        internal static string VideoDir => $"{ScenarioDir}/video";
        internal static string ShotsDir => $"{ScenarioDir}/screenshots";

        [InitializeOnLoadMethod]
        static void RegisterHook()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        [MenuItem("GOLFIN/ShotUI/Record Power Gauge Target Marker Demo")]
        public static void LaunchDemo()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[MarkerDemo] Already in play mode — stop first.");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            EditorSceneManager.OpenScene(ShellScenePath);
            Directory.CreateDirectory(VideoDir);
            Directory.CreateDirectory(ShotsDir);

            // Sanctioned recording engine. ArmDeferred (not Arm) so Begin() is a no-op at
            // EnteredPlayMode and the clip starts once the hole is stable — that is what avoids
            // the Y-flip transient. ResetSessionGuard because this harness records exactly ONE
            // clip; the guard exists to stop batch accumulation wedging the GPU.
            BotVideoRecorder.ResetSessionGuard();
            LoopV2SmokeBot.Scenario = ScenarioKey;   // only so record_info.json lands beside history.log
            BotVideoRecorder.CustomOutputPath = $"{VideoDir}/raw";
            BotVideoRecorder.MaxRecordSecondsSessionOverride = WatchdogSeconds;
            BotVideoRecorder.ArmDeferred();

            SessionState.SetBool(ArmedKey, true);
            EditorApplication.EnterPlaymode();
            Debug.Log("[MarkerDemo] Armed. Entering play mode (deferred recording)...");
        }

        static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                if (!SessionState.GetBool(ArmedKey, false)) return;
                SessionState.SetBool(ArmedKey, false);
                Application.runInBackground = true;
                // No-op for a deferred arm; clears any accidentally-set RecordVideo.
                BotVideoRecorder.Begin();

                var host = new GameObject("[PowerGaugeMarkerDemoBot]");
                UnityEngine.Object.DontDestroyOnLoad(host);
                host.AddComponent<PowerGaugeMarkerDemoRunner>();
                Debug.Log("[MarkerDemo] Bot spawned. Waiting for hole load...");
            }
            else if (state == PlayModeStateChange.ExitingPlayMode)
            {
                // BotVideoRecorder.End() is deliberately NOT called here — LoopV2SmokeBotMenu's
                // ExitingPlayMode hook calls it unconditionally, and exactly one End() per session
                // is the documented contract (same note in ObBoundaryCaptureMenu / ZoneBakeAfterClipMenu).
                WriteCaptionLog();
            }
        }

        /// <summary>Start the clip. All Recorder plumbing lives in BotVideoRecorder.</summary>
        public static void StartRecorder()
        {
            _log = new StringBuilder();
            BotVideoRecorder.BeginDeferred();
        }

        /// <summary>Emit one caption line for build_bot_video.py --mode steps.</summary>
        public static void Step(string text)
        {
            if (_log == null) return;
            float t = Time.realtimeSinceStartup;
            _log.AppendLine($"[t={t.ToString("F3", CultureInfo.InvariantCulture)}] Step: '{text}'");
            Debug.Log($"[MarkerDemo] Step: {text}");
        }

        /// <summary>
        /// Captions only. record_info.json (the clock BotDriver/history.log share) is written by
        /// BotVideoRecorder itself at Begin, so nothing is duplicated here.
        /// </summary>
        static void WriteCaptionLog()
        {
            if (_log == null) return;
            Directory.CreateDirectory(ShotsDir);
            File.WriteAllText($"{ShotsDir}/history.log", _log.ToString());
            _log = null;
            Debug.Log($"[MarkerDemo] history.log written under {ShotsDir}");
        }
    }

    /// <summary>Runtime coroutine driver. Real widget clicks + production aim call only.</summary>
    public class PowerGaugeMarkerDemoRunner : MonoBehaviour
    {
        const BindingFlags NP = BindingFlags.NonPublic | BindingFlags.Instance;
        const BindingFlags PI = BindingFlags.Public    | BindingFlags.Instance;
        const int HoleNumber = 1;

        Component    _sc;
        PropertyInfo _pMapTarget;
        MethodInfo   _mBeginDrag, _mSetPower, _mEndDrag, _mCancelDrag;
        PowerGaugeGraphic _gauge;

        void Start() => StartCoroutine(Sequence());

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
            Debug.LogWarning($"[MarkerDemo] TIMEOUT waiting for '{goName}'");
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
                    if (c.GetType().GetField("actionButton", NP)?.GetValue(c) is Button btn)
                    { ClickReal(btn); yield break; }
                }
                yield return new WaitForSecondsRealtime(0.25f); t += 0.25f;
            }
            Debug.LogWarning($"[MarkerDemo] TIMEOUT waiting for hole {hole} card");
        }

        static MapViewController FindMvc() => UnityEngine.Object
            .FindObjectsByType<MapViewController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
            .FirstOrDefault();

        bool BindShotController()
        {
            var t = AppDomain.CurrentDomain.GetAssemblies()
                       .FirstOrDefault(a => a.GetName().Name == "Golfin.Gameplay.Input")
                       ?.GetType("Golfin.Gameplay.Input.ShotController");
            if (t == null) return false;
            _sc = UnityEngine.Object.FindObjectsByType(t, FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                    .FirstOrDefault() as Component;
            if (_sc == null) return false;
            _pMapTarget  = t.GetProperty("MapTargetCarryM");
            _mBeginDrag  = t.GetMethod("BeginExternalDrag",  PI, null, Type.EmptyTypes, null);
            _mSetPower   = t.GetMethod("SetExternalPower",   PI, null, new[] { typeof(float), typeof(float) }, null);
            _mEndDrag    = t.GetMethod("EndExternalDrag",    PI);
            _mCancelDrag = t.GetMethod("CancelExternalDrag", PI, null, Type.EmptyTypes, null);
            return _pMapTarget != null && _mBeginDrag != null && _mSetPower != null;
        }

        /// <summary>Pull the handle and hold at a fixed power (the ClubHandleDragger path).</summary>
        IEnumerator HoldGaugeAt(float power, float seconds)
        {
            _mBeginDrag.Invoke(_sc, null);
            float end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end)
            {
                _mSetPower.Invoke(_sc, new object[] { power, 0f });
                yield return null;
            }
        }

        /// <summary>Ramp the pull so the fill visibly sweeps toward (and settles on) a target power.</summary>
        IEnumerator RampGaugeTo(float target, float seconds)
        {
            _mBeginDrag.Invoke(_sc, null);
            float t0 = Time.realtimeSinceStartup, end = t0 + seconds;
            while (Time.realtimeSinceStartup < end)
            {
                float k = Mathf.Clamp01((Time.realtimeSinceStartup - t0) / seconds);
                _mSetPower.Invoke(_sc, new object[] { Mathf.SmoothStep(0f, target, k), 0f });
                yield return null;
            }
            _mSetPower.Invoke(_sc, new object[] { target, 0f });
        }

        IEnumerator Sequence()
        {
            yield return new WaitForSecondsRealtime(5f);
            yield return ClickWhenPresent("StartButton");
            yield return new WaitForSecondsRealtime(2.5f);
            yield return ClickWhenPresent("PlayButton");
            yield return new WaitForSecondsRealtime(2.5f);
            yield return ClickHoleCard(HoleNumber);

            float t = 0f;
            while (FindButton("HoleMap") == null && t < 120f)
            { yield return new WaitForSecondsRealtime(0.5f); t += 0.5f; }
            yield return new WaitForSecondsRealtime(4f);

            _gauge = UnityEngine.Object.FindObjectsByType<PowerGaugeGraphic>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
            if (!BindShotController() || _gauge == null)
            {
                Debug.LogError("[MarkerDemo] Could not bind ShotController / gauge — aborting.");
                EditorApplication.ExitPlaymode();
                yield break;
            }

            PowerGaugeMarkerDemoRecorder.StartRecorder();
            yield return new WaitForSecondsRealtime(0.5f);

            // 1 — the gauge as it is today: power only, nothing to aim at.
            PowerGaugeMarkerDemoRecorder.Step("Hole 1, at the tee.\\nPull back: power only,\\nnothing to aim at");
            yield return RampGaugeTo(0.62f, 2.0f);
            yield return new WaitForSecondsRealtime(2.6f);
            _mCancelDrag.Invoke(_sc, null);
            yield return new WaitForSecondsRealtime(1.0f);

            // 2 — open the map and place a landing.
            PowerGaugeMarkerDemoRecorder.Step("Open the hole map");
            yield return ClickWhenPresent("HoleMap");
            yield return new WaitForSecondsRealtime(4.5f);   // the invariant dump re-aims twice

            var mvc = FindMvc();
            PowerGaugeMarkerDemoRecorder.Step("Place the landing where\\nyou want the ball to stop");
            if (mvc != null)
            {
                // Sweep the touch point so the guide line and rings follow, then settle.
                for (float k = 0f; k <= 1f; k += 0.02f)
                {
                    mvc.TrySetAimFromScreenPoint(new Vector2(
                        Screen.width * Mathf.Lerp(0.62f, 0.50f, k),
                        Screen.height * Mathf.Lerp(0.30f, 0.42f, k)));
                    yield return null;
                }
            }
            yield return new WaitForSecondsRealtime(2.8f);

            PowerGaugeMarkerDemoRecorder.Step("SHOOT closes the map.\\nThe target comes\\nback with you");
            yield return new WaitForSecondsRealtime(1.6f);
            if (typeof(MapViewController).GetField("_shootButton", NP).GetValue(mvc) is Button shoot)
                ClickReal(shoot);
            yield return new WaitForSecondsRealtime(2.0f);

            // 3 — the notch.
            // PACING RULE: every pull must stay under the arrow timeout. TickArrow cancels the
            // swing after ControlsConfig.MaxTotalPasses (10) passes — ~8s at low ClubControl — and
            // a timed-out swing silently returns to Idle, so a later EndExternalDrag fires NOTHING.
            // That is exactly how the first cut of this clip ended up captioned "one marker per
            // shot" over a frame still reading TURN 1. Each segment below is a SHORT pull that
            // ends in CancelExternalDrag, which resets the arrow clock.
            float target = (float)_pMapTarget.GetValue(_sc);
            PowerGaugeMarkerDemoRecorder.Step($"The gauge now marks\\nthe power that lands\\nthere ({target:F0} m out)");
            yield return RampGaugeTo(0.62f, 2.0f);
            yield return new WaitForSecondsRealtime(2.6f);

            float notch = Mathf.Max(0.05f, _gauge.MarkerFrac01);
            PowerGaugeMarkerDemoRecorder.Step("Pull to the notch.\\nNo mental math");
            yield return RampGaugeTo(notch, 1.6f);
            yield return new WaitForSecondsRealtime(1.8f);
            _mCancelDrag.Invoke(_sc, null);
            yield return new WaitForSecondsRealtime(0.8f);

            // 4 — club change: target unmoved, so the notch moves.
            PowerGaugeMarkerDemoRecorder.Step("Switch club: the target\\nhas not moved,\\nso the notch does");
            if (ClubContext.EquippedBag != null && ClubContext.EquippedBag.Count > 1)
            {
                ClubContext.RequestSelection((ClubContext.SelectedIndex + 1) % ClubContext.EquippedBag.Count);
                yield return new WaitForSecondsRealtime(1.4f);
            }
            yield return RampGaugeTo(0.62f, 1.8f);
            yield return new WaitForSecondsRealtime(2.4f);
            float notch2 = Mathf.Max(0.05f, _gauge.MarkerFrac01);
            _mCancelDrag.Invoke(_sc, null);
            yield return new WaitForSecondsRealtime(0.7f);

            // 5 — flick. A FRESH short pull straight to the notch, released immediately, so the
            // arrow clock cannot have run out by the time EndExternalDrag lands.
            PowerGaugeMarkerDemoRecorder.Step("Flick");
            yield return RampGaugeTo(notch2, 1.5f);
            yield return new WaitForSecondsRealtime(0.6f);
            _mEndDrag.Invoke(_sc, new object[] { true });
            yield return new WaitForSecondsRealtime(1.2f);

            // Never caption a shot that did not happen. MapTargetCarryM is cleared in CommitFlick,
            // so it is the cheapest proof the flick actually committed.
            bool committed = (float)_pMapTarget.GetValue(_sc) < 0f;
            if (!committed)
            {
                Debug.LogError("[MarkerDemo] FLICK DID NOT COMMIT — the swing timed out before " +
                               "release. This clip is NOT shippable: the closing caption would " +
                               "claim a shot that never happened. Shorten the pull segments.");
            }
            else
            {
                Debug.Log("[MarkerDemo] Flick committed (MapTargetCarryM cleared).");
            }
            yield return new WaitForSecondsRealtime(7.5f);

            // 6 — one marker per mapped shot.
            PowerGaugeMarkerDemoRecorder.Step("One marker per shot.\\nGone until you\\nmap again");
            yield return RampGaugeTo(0.62f, 1.8f);
            yield return new WaitForSecondsRealtime(2.6f);
            Debug.Log($"[MarkerDemo] post-shot MarkerFrac01={_gauge.MarkerFrac01:F3} (expect < 0)");
            _mCancelDrag.Invoke(_sc, null);
            yield return new WaitForSecondsRealtime(1.5f);

            EditorApplication.ExitPlaymode();
        }
    }
}
#endif
