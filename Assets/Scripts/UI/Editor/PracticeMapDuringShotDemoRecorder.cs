#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
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
using Golfin.Gameplay.Session;
using Golfin.Gameplay.UI.ShotUI;
using Golfin.Physics.Viewer;
using Golfin.Physics.Viewer.Editor;

namespace Golfin.EditorTools
{
    /// <summary>
    /// Video + invariant proof for the Practice-mode map-thumbnail regression
    /// (Cesar, 2026-08-27: "Map on the top left disappears on UI in practice mode — it should
    /// not be clickable, but it should still show").
    ///
    /// Fix under test: <see cref="ShotInProgressUiGate"/> used to hide HoleMapContainer for the
    /// duration of every shot. It now hides it in VERSUS only; in Practice/solo the thumbnail
    /// stays on screen and is merely made non-interactable.
    ///
    /// Shows, through the REAL player entry path and nothing else:
    ///   boot → PLAY → Hole 1 card → hole load → (recording starts) → pull the club handle →
    ///   flick → BALL IN FLIGHT: the top-left map is still on screen and a real pointer tap on
    ///   it does nothing → ball settles → the same real tap now opens the map view.
    ///
    /// The gate is a deterministic invariant JSON (PIPELINE_HARDENING Rule 3), NOT a human
    /// reading the video: every assertion below is sampled at runtime and written to
    ///   Docs/Diagnostics/_capture/practice_map_during_shot_invariants.json
    /// The video is the artifact for Cesar; the JSON is what passes or fails.
    ///
    /// The during-flight tap is dispatched as a genuine EventSystem raycast + pointerClick at
    /// the map's own screen centre. It deliberately does NOT call Button.onClick.Invoke(),
    /// which bypasses `interactable` and would make a broken build look fixed.
    ///
    /// RECORDING IS NOT HAND-ROLLED — it goes through the sanctioned engine
    /// <see cref="BotVideoRecorder"/> via CustomOutputPath + ArmDeferred/BeginDeferred/End,
    /// exactly as PowerGaugeMarkerDemoRecorder does. No Scenarios.cs entry is added
    /// (standing ban on editing Assets/Scripts/Physics/).
    ///
    /// Output raw: tasks/loop_v2_smoke_bot/practice_map_during_shot/video/raw.mp4
    /// Captions:   tasks/loop_v2_smoke_bot/practice_map_during_shot/screenshots/history.log
    /// Usage: GOLFIN > ShotUI > Record Practice Map During Shot Demo
    /// </summary>
    public static class PracticeMapDuringShotDemoRecorder
    {
        const string ShellScenePath = "Assets/Scenes/ShellScene.unity";
        const string ScenarioDir    = "tasks/loop_v2_smoke_bot/practice_map_during_shot";
        const string ArmedKey       = "PracticeMapDuringShotDemoRecorder.Armed";
        const string ScenarioKey    = "practice_map_during_shot";

        internal const string InvariantsPath =
            "Docs/Diagnostics/_capture/practice_map_during_shot_invariants.json";

        /// <summary>Clip runs ~50s; BotVideoRecorder's default watchdog is 30s.</summary>
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

        [MenuItem("GOLFIN/ShotUI/Record Practice Map During Shot Demo")]
        public static void LaunchDemo()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[PracticeMapDemo] Already in play mode — stop first.");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            EditorSceneManager.OpenScene(ShellScenePath);
            Directory.CreateDirectory(VideoDir);
            Directory.CreateDirectory(ShotsDir);
            Directory.CreateDirectory(Path.GetDirectoryName(InvariantsPath));

            // ResetSessionGuard: this harness records exactly ONE clip. The guard exists to stop
            // batch accumulation wedging the GPU; the Editor.log for this launch shows a single
            // prior RecorderController.StartRecording and the editor is idle.
            BotVideoRecorder.ResetSessionGuard();
            LoopV2SmokeBot.Scenario = ScenarioKey;   // so record_info.json lands beside history.log
            BotVideoRecorder.CustomOutputPath = $"{VideoDir}/raw";
            BotVideoRecorder.MaxRecordSecondsSessionOverride = WatchdogSeconds;
            BotVideoRecorder.ArmDeferred();

            SessionState.SetBool(ArmedKey, true);
            EditorApplication.EnterPlaymode();
            Debug.Log("[PracticeMapDemo] Armed. Entering play mode (deferred recording)...");
        }

        static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                if (!SessionState.GetBool(ArmedKey, false)) return;
                SessionState.SetBool(ArmedKey, false);
                Application.runInBackground = true;   // MANDATORY for MCP-driven runs
                BotVideoRecorder.Begin();             // no-op for a deferred arm

                var host = new GameObject("[PracticeMapDuringShotBot]");
                UnityEngine.Object.DontDestroyOnLoad(host);
                host.AddComponent<PracticeMapDuringShotRunner>();
                Debug.Log("[PracticeMapDemo] Bot spawned. Waiting for hole load...");
            }
            else if (state == PlayModeStateChange.ExitingPlayMode)
            {
                // BotVideoRecorder.End() is deliberately NOT called here — LoopV2SmokeBotMenu's
                // ExitingPlayMode hook calls it unconditionally, and exactly one End() per
                // session is the documented contract.
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
            Debug.Log($"[PracticeMapDemo] Step: {text}");
        }

        static void WriteCaptionLog()
        {
            if (_log == null) return;
            Directory.CreateDirectory(ShotsDir);
            File.WriteAllText($"{ShotsDir}/history.log", _log.ToString());
            _log = null;
            Debug.Log($"[PracticeMapDemo] history.log written under {ShotsDir}");
        }
    }

    /// <summary>Runtime coroutine driver. Real widget taps + the production flick path only.</summary>
    public class PracticeMapDuringShotRunner : MonoBehaviour
    {
        const BindingFlags NP = BindingFlags.NonPublic | BindingFlags.Instance;
        const BindingFlags PI = BindingFlags.Public    | BindingFlags.Instance;
        const int HoleNumber = 1;

        Component  _sc;
        MethodInfo _mBeginDrag, _mSetPower, _mEndDrag, _mCancelDrag;

        // Cached BEFORE the flick so a hidden container is still observable through the
        // reference (activeInHierarchy on a cached ref reports false when it is switched off).
        Button        _mapButton;
        GameObject    _mapContainer;
        RectTransform _mapRect;
        Canvas        _canvas;
        MapViewController _mvc;

        readonly List<Assertion> _asserts = new List<Assertion>();

        class Assertion
        {
            public string id, description, expected, actual, verdict;
        }

        void Assert(string id, string description, object expected, object actual)
        {
            bool ok = string.Equals(expected?.ToString(), actual?.ToString(), StringComparison.Ordinal);
            _asserts.Add(new Assertion
            {
                id = id, description = description,
                expected = expected?.ToString() ?? "null",
                actual   = actual?.ToString()   ?? "null",
                verdict  = ok ? "PASS" : "FAIL"
            });
            Debug.Log($"[PracticeMapDemo] {(ok ? "PASS" : "FAIL")} {id}: {description} " +
                      $"(expected={expected}, actual={actual})");
        }

        void Note(string id, string description, object value)
        {
            _asserts.Add(new Assertion
            {
                id = id, description = description,
                expected = "(informational)", actual = value?.ToString() ?? "null", verdict = "INFO"
            });
            Debug.Log($"[PracticeMapDemo] INFO {id}: {description} = {value}");
        }

        void Start() => StartCoroutine(Sequence());

        // ── real-input helpers ────────────────────────────────────────────────
        static Button FindButton(string goName) => UnityEngine.Object
            .FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
            .FirstOrDefault(b => b.gameObject.name == goName);

        /// <summary>Navigation click, used only for boot-flow buttons that are known-live.</summary>
        static void ClickReal(Button b)
        {
            var ped = new PointerEventData(EventSystem.current);
            ExecuteEvents.Execute(b.gameObject, ped, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(b.gameObject, ped, ExecuteEvents.pointerUpHandler);
            b.onClick.Invoke();
        }

        /// <summary>
        /// A GENUINE tap at a screen point: EventSystem raycast, then pointerDown/Up/Click on
        /// whatever is actually under the finger. Never calls onClick.Invoke(), so a
        /// non-interactable Button correctly swallows it. Returns the name of what was hit.
        /// </summary>
        static string TapScreenPoint(Vector2 screenPoint)
        {
            var es = EventSystem.current;
            if (es == null) return "<no EventSystem>";

            var ped = new PointerEventData(es) { position = screenPoint };
            var hits = new List<RaycastResult>();
            es.RaycastAll(ped, hits);
            if (hits.Count == 0) return "<nothing hit>";

            var go = hits[0].gameObject;
            ExecuteEvents.ExecuteHierarchy(go, ped, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.ExecuteHierarchy(go, ped, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.ExecuteHierarchy(go, ped, ExecuteEvents.pointerClickHandler);
            return go.name;
        }

        /// <summary>Breadcrumb for stage logging — what the player could actually tap right now.</summary>
        static string VisibleButtons() => "buttons: " + string.Join(", ", UnityEngine.Object
            .FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
            .Select(b => b.gameObject.name).Distinct().OrderBy(n => n).Take(25));

        IEnumerator ClickWhenPresent(string goName, float timeout = 90f)
        {
            float t = 0f;
            while (t < timeout)
            {
                var b = FindButton(goName);
                if (b != null) { ClickReal(b); yield break; }
                yield return new WaitForSecondsRealtime(0.25f); t += 0.25f;
            }
            Debug.LogWarning($"[PracticeMapDemo] TIMEOUT waiting for '{goName}'");
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
            Debug.LogWarning($"[PracticeMapDemo] TIMEOUT waiting for hole {hole} card");
        }

        bool BindShotController()
        {
            var t = AppDomain.CurrentDomain.GetAssemblies()
                       .FirstOrDefault(a => a.GetName().Name == "Golfin.Gameplay.Input")
                       ?.GetType("Golfin.Gameplay.Input.ShotController");
            if (t == null) return false;
            _sc = UnityEngine.Object.FindObjectsByType(t, FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                    .FirstOrDefault() as Component;
            if (_sc == null) return false;
            _mBeginDrag  = t.GetMethod("BeginExternalDrag",  PI, null, Type.EmptyTypes, null);
            _mSetPower   = t.GetMethod("SetExternalPower",   PI, null, new[] { typeof(float), typeof(float) }, null);
            _mEndDrag    = t.GetMethod("EndExternalDrag",    PI);
            _mCancelDrag = t.GetMethod("CancelExternalDrag", PI, null, Type.EmptyTypes, null);
            return _mBeginDrag != null && _mSetPower != null && _mEndDrag != null;
        }

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

        // ── map geometry ──────────────────────────────────────────────────────
        /// <summary>Screen-space rect of the map container (Overlay canvases give screen px directly).</summary>
        Rect MapScreenRect()
        {
            var corners = new Vector3[4];
            _mapRect.GetWorldCorners(corners);
            Camera cam = (_canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                       ? _canvas.worldCamera : null;
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            for (int i = 0; i < 4; i++)
            {
                Vector2 sp = RectTransformUtility.WorldToScreenPoint(cam, corners[i]);
                min = Vector2.Min(min, sp);
                max = Vector2.Max(max, sp);
            }
            return new Rect(min, max - min);
        }

        /// <summary>Reflect the gate's own hide list so the report cites the real wiring.</summary>
        static string GateHideListNames(out bool containsMapContainer, GameObject mapContainer)
        {
            containsMapContainer = false;
            var gate = UnityEngine.Object.FindObjectsByType<ShotInProgressUiGate>(
                           FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
            if (gate == null) return "<gate not found>";
            var f = typeof(ShotInProgressUiGate).GetField("_hideDuringShot", NP);
            if (f?.GetValue(gate) is not List<GameObject> list) return "<field not readable>";
            foreach (var go in list) if (go == mapContainer) containsMapContainer = true;
            return string.Join(", ", list.Select(g => g == null ? "<null>" : g.name));
        }

        IEnumerator Sequence()
        {
            // ── boot through the REAL entry path ──────────────────────────────
            // StartButton gets a SHORT wait: DevAutoSignIn taps the Splash StartButton itself
            // as soon as the session restores, so on an authenticated editor run the button is
            // usually gone before we look. Timing out here is expected and harmless — it must
            // not burn 90s of wall clock before the rest of the flow (2026-08-27).
            yield return new WaitForSecondsRealtime(5f);
            yield return ClickWhenPresent("StartButton", 15f);
            yield return new WaitForSecondsRealtime(2.5f);
            Debug.Log("[PracticeMapDemo] stage: past Splash → clicking PlayButton");
            yield return ClickWhenPresent("PlayButton");
            yield return new WaitForSecondsRealtime(2.5f);
            Debug.Log("[PracticeMapDemo] stage: clicking Hole " + HoleNumber + " card. " + VisibleButtons());
            yield return ClickHoleCard(HoleNumber);

            float t = 0f;
            while (FindButton("HoleMap") == null && t < 120f)
            { yield return new WaitForSecondsRealtime(0.5f); t += 0.5f; }
            Debug.Log($"[PracticeMapDemo] stage: hole-load wait ended after {t:F1}s. " + VisibleButtons());
            yield return new WaitForSecondsRealtime(4f);

            _mapButton = FindButton("HoleMap");
            _mvc       = UnityEngine.Object.FindObjectsByType<MapViewController>(
                             FindObjectsInactive.Exclude, FindObjectsSortMode.None).FirstOrDefault();
            if (_mapButton == null || _mvc == null || !BindShotController())
            {
                Debug.LogError("[PracticeMapDemo] Could not bind HoleMap / MapViewController / " +
                               "ShotController — aborting.");
                EditorApplication.ExitPlaymode();
                yield break;
            }

            _mapContainer = _mapButton.transform.parent.gameObject;   // HoleMapContainer
            _mapRect      = _mapContainer.GetComponent<RectTransform>();
            _canvas       = _mapContainer.GetComponentInParent<Canvas>();

            string hideList = GateHideListNames(out bool listHasMap, _mapContainer);
            Note("I1", "ShotInProgressUiGate._hideDuringShot contents", hideList);
            Note("I2", "map container GameObject name", _mapContainer.name);

            PracticeMapDuringShotDemoRecorder.StartRecorder();
            yield return new WaitForSecondsRealtime(0.5f);

            // ── A1/A2: we really are in Practice, and the map starts live ─────
            Assert("A1", "Practice mode (GameSession.IsVersus == false)", false, GameSession.IsVersus);
            Assert("A2", "HoleMapContainer is the object the gate is wired to hide", true, listHasMap);
            Assert("A3", "PRE-SHOT: map container visible", true, _mapContainer.activeInHierarchy);
            Assert("A4", "PRE-SHOT: map button interactable", true, _mapButton.interactable);

            Rect pre = MapScreenRect();
            Note("I3", "PRE-SHOT map screen rect (x,y,w,h)",
                 $"{pre.x:F0},{pre.y:F0},{pre.width:F0},{pre.height:F0}");
            // Cesar's report said "top left", but the HUD's only map thumbnail is the HoleCard's,
            // which sits TOP-RIGHT beside the LOMOND / HOLE / PAR chips — top-left is the player
            // card. Measured 2026-08-27: rect (942,2194,180,180) on 1170x2532. Assert the real
            // corner so this gate never again fails on a mis-stated side.
            Assert("A5", "PRE-SHOT: map sits in the TOP-RIGHT corner",
                   true, pre.center.x > Screen.width * 0.5f && pre.center.y > Screen.height * 0.5f);

            PracticeMapDuringShotDemoRecorder.Step("Practice, hole 1.\\nThe map sits top-left\\nand is tappable");
            yield return new WaitForSecondsRealtime(2.6f);

            // ── flick, through the production ShotController path ─────────────
            PracticeMapDuringShotDemoRecorder.Step("Pull back and flick");
            yield return RampGaugeTo(0.85f, 1.5f);
            yield return new WaitForSecondsRealtime(0.5f);
            _mEndDrag.Invoke(_sc, new object[] { true });

            // Wait for the gate to actually engage — never sample "during flight" on faith.
            float gateWait = 0f;
            while (!ShotInProgressUiGate.ShotInProgress && gateWait < 5f)
            { yield return null; gateWait += Time.unscaledDeltaTime; }
            Assert("A6", "the shot committed and the gate engaged (ShotInProgress)",
                   true, ShotInProgressUiGate.ShotInProgress);

            PracticeMapDuringShotDemoRecorder.Step("Ball in flight —\\nthe map is STILL on screen");

            // ── A7/A8: sample the WHOLE flight, not one lucky frame ───────────
            int samples = 0, visibleSamples = 0, interactableSamples = 0;
            float flight = 0f;
            bool tapped = false;
            string tapHit = "<not attempted>";
            while (ShotInProgressUiGate.ShotInProgress && flight < 25f)
            {
                samples++;
                if (_mapContainer.activeInHierarchy) visibleSamples++;
                if (_mapButton.interactable)         interactableSamples++;

                // Halfway through, tap the map for real. One tap is enough; repeating it would
                // just re-prove the same frame.
                if (!tapped && flight > 0.8f)
                {
                    tapped = true;
                    PracticeMapDuringShotDemoRecorder.Step("Tapping the map now…\\nnothing happens");
                    tapHit = TapScreenPoint(MapScreenRect().center);
                }
                yield return null;
                flight += Time.unscaledDeltaTime;
            }

            Note("I4", "in-flight samples taken", samples);
            Note("I5", "flight duration sampled (s)", flight.ToString("F2", CultureInfo.InvariantCulture));
            Note("I6", "what the in-flight tap actually hit", tapHit);
            Assert("A7", "IN-FLIGHT: map container visible on EVERY sampled frame",
                   samples, visibleSamples);
            Assert("A8", "IN-FLIGHT: map button non-interactable on EVERY sampled frame",
                   0, interactableSamples);
            Assert("A9", "IN-FLIGHT: the real tap did NOT open the map view", false, _mvc.IsOpen);

            // ── A10/A11: the map comes back to life once the ball settles ─────
            yield return new WaitForSecondsRealtime(1.5f);
            Assert("A10", "AFTER SETTLE: map container still visible", true, _mapContainer.activeInHierarchy);
            Assert("A11", "AFTER SETTLE: map button interactable again", true, _mapButton.interactable);

            PracticeMapDuringShotDemoRecorder.Step("Shot over —\\nthe same tap opens the map");
            yield return new WaitForSecondsRealtime(1.4f);
            string tapHit2 = TapScreenPoint(MapScreenRect().center);
            Note("I7", "what the after-settle tap hit", tapHit2);
            yield return new WaitForSecondsRealtime(2.5f);
            Assert("A12", "AFTER SETTLE: the same real tap DOES open the map view", true, _mvc.IsOpen);

            yield return new WaitForSecondsRealtime(2.0f);
            if (_mvc.IsOpen) _mvc.Close();
            yield return new WaitForSecondsRealtime(2.0f);

            WriteInvariants();
            EditorApplication.ExitPlaymode();
        }

        void WriteInvariants()
        {
            int fails = _asserts.Count(a => a.verdict == "FAIL");
            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine("  \"task\": \"practice_map_during_shot\",");
            sb.AppendLine("  \"subject\": \"ShotInProgressUiGate hides HoleMapContainer in Versus only\",");
            sb.AppendLine($"  \"screen\": \"{Screen.width}x{Screen.height}\",");
            sb.AppendLine($"  \"fail\": {fails},");
            sb.AppendLine("  \"assertions\": [");
            for (int i = 0; i < _asserts.Count; i++)
            {
                var a = _asserts[i];
                sb.Append("    { ");
                sb.Append($"\"id\": \"{Esc(a.id)}\", ");
                sb.Append($"\"description\": \"{Esc(a.description)}\", ");
                sb.Append($"\"expected\": \"{Esc(a.expected)}\", ");
                sb.Append($"\"actual\": \"{Esc(a.actual)}\", ");
                sb.Append($"\"verdict\": \"{Esc(a.verdict)}\"");
                sb.AppendLine(i == _asserts.Count - 1 ? " }" : " },");
            }
            sb.AppendLine("  ]");
            sb.AppendLine("}");

            string path = PracticeMapDuringShotDemoRecorder.InvariantsPath;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, sb.ToString());
            Debug.Log($"[PracticeMapDemo] Invariants written → {path} (fail={fails})");
        }

        static string Esc(string s) => (s ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
#endif
