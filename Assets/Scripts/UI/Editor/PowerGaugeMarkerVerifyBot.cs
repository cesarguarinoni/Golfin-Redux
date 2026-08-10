#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Golfin.Diagnostics.Runtime;
using Golfin.Gameplay.UI.HUD;
using Golfin.Gameplay.UI.ShotUI;

namespace Golfin.EditorTools
{
    /// <summary>
    /// Acceptance harness for power_gauge_target_marker (SPEC §5 "Editor manual").
    ///
    /// Runs the whole matrix through the REAL player entry path and nothing else:
    ///   boot → PLAY → mode card → Hole 1 card → hole load →
    ///   tap the real HoleMap button → place a landing target with the production
    ///   TrySetAimFromScreenPoint (the same call the finger drives) → tap the real SHOOT
    ///   button (Close → CloseImmediate → MapTargetCarryM write-back) →
    ///   pull the club handle (BeginExternalDrag/SetExternalPower — the ClubHandleDragger
    ///   path) so the gauge is on screen → capture.
    ///
    /// Matrix (SPEC §5): notch at the mapped %, notch moves on club change, notch gone after
    /// a committed shot, no notch in putter mode, and the yards text now tracking the selected
    /// club instead of the never-wired 250f default.
    ///
    /// The GATE is the invariant JSON (per-assertion PASS/FAIL) written next to the frames —
    /// the screenshots are the artifact for Cesar, not the pass criterion.
    ///
    /// ShotController lives in Golfin.Gameplay.Input (autoReferenced:false) so it is driven by
    /// reflection; everything else (MapViewController, PowerGaugeWidget/Graphic, ClubContext,
    /// CaptureCore) is autoReferenced and used directly.
    ///
    /// Usage: GOLFIN > ShotUI > Verify Power Gauge Target Marker
    /// Output: Docs/Specs/Active/power_gauge_target_marker/screenshots/
    ///         + marker_invariants.json
    /// </summary>
    public static class PowerGaugeMarkerVerifyBot
    {
        const string ShellScenePath = "Assets/Scenes/ShellScene.unity";
        const string TaskDir        = "Docs/Specs/Active/power_gauge_target_marker";
        const string ArmedKey       = "PowerGaugeMarkerVerifyBot.Armed";

        internal static string ShotsDir => $"{TaskDir}/screenshots";
        internal static string JsonPath => $"{TaskDir}/marker_invariants.json";

        [InitializeOnLoadMethod]
        static void RegisterHook()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        [MenuItem("GOLFIN/ShotUI/Verify Power Gauge Target Marker")]
        public static void Launch()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[MarkerVerify] Already in play mode — stop first.");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            EditorSceneManager.OpenScene(ShellScenePath);
            Directory.CreateDirectory(ShotsDir);
            SessionState.SetBool(ArmedKey, true);
            EditorApplication.EnterPlaymode();
            Debug.Log("[MarkerVerify] Armed. Entering play mode...");
        }

        static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode) return;
            if (!SessionState.GetBool(ArmedKey, false)) return;
            SessionState.SetBool(ArmedKey, false);

            Application.runInBackground = true;   // frames render while the editor is unfocused
            var host = new GameObject("[PowerGaugeMarkerVerifyBot]");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.AddComponent<PowerGaugeMarkerVerifyRunner>();
            Debug.Log("[MarkerVerify] Bot spawned. Waiting for hole load...");
        }
    }

    internal class PowerGaugeMarkerVerifyRunner : MonoBehaviour
    {
        const BindingFlags NP = BindingFlags.NonPublic | BindingFlags.Instance;
        const BindingFlags PI = BindingFlags.Public    | BindingFlags.Instance;
        const int   HoleNumber     = 1;
        const float kYardsToMeters = 0.9144f;
        const float kFracTol       = 0.005f;

        readonly StringBuilder _json = new StringBuilder();
        int  _asserts, _fails;

        // Reflection handles onto ShotController (Golfin.Gameplay.Input — not referenceable).
        Component    _sc;
        PropertyInfo _pMapTarget, _pState;
        MethodInfo   _mBeginDrag, _mSetPower, _mEndDrag, _mCancelDrag;

        PowerGaugeWidget  _widget;
        PowerGaugeGraphic _gauge;

        void Start() => StartCoroutine(Sequence());

        // ── real-widget helpers (same idiom as MapViewStrictCropDemoRecorder) ────
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
            Debug.LogWarning($"[MarkerVerify] TIMEOUT waiting for '{goName}'");
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
            Debug.LogWarning($"[MarkerVerify] TIMEOUT waiting for hole {hole} card");
        }

        static MapViewController FindMvc() => UnityEngine.Object
            .FindObjectsByType<MapViewController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
            .FirstOrDefault();

        // ── ShotController reflection ───────────────────────────────────────────
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
            _pState      = t.GetProperty("State");
            _mBeginDrag  = t.GetMethod("BeginExternalDrag",  PI, null, Type.EmptyTypes, null);
            _mSetPower   = t.GetMethod("SetExternalPower",   PI, null, new[] { typeof(float), typeof(float) }, null);
            _mEndDrag    = t.GetMethod("EndExternalDrag",    PI);
            _mCancelDrag = t.GetMethod("CancelExternalDrag", PI, null, Type.EmptyTypes, null);
            return _pMapTarget != null && _mBeginDrag != null && _mSetPower != null;
        }

        float MapTargetM => (float)_pMapTarget.GetValue(_sc);

        /// <summary>Hold the gauge on screen at a power level (the ClubHandleDragger path).</summary>
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

        void ReleaseWithoutShooting() => _mCancelDrag.Invoke(_sc, null);

        // ── assertions ──────────────────────────────────────────────────────────
        void Assert(string id, bool ok, string detail)
        {
            _asserts++; if (!ok) _fails++;
            _json.AppendLine($"    {{ \"id\": \"{id}\", \"verdict\": \"{(ok ? "PASS" : "FAIL")}\", " +
                             $"\"detail\": \"{detail.Replace("\"", "'")}\" }},");
            Debug.Log($"[MarkerVerify] {(ok ? "PASS" : "FAIL")}  {id} — {detail}");
        }

        /// <summary>
        /// Capture at end-of-frame (CaptureScreenshotAsTexture needs a composited backbuffer),
        /// then VERIFY the PNG exists before citing it — SnapPlayModeSafe hands back a path for
        /// a file it never wrote when the editor is unfocused
        /// (memory `reference_snapplaymodesafe_phantom_path`).
        /// </summary>
        IEnumerator SnapCo(string label)
        {
            yield return new WaitForEndOfFrame();

            string p = CaptureCore.SnapPlayModeSafe(label);
            bool   ok = !string.IsNullOrEmpty(p) && File.Exists(p);
            if (ok)
            {
                string dest = $"{PowerGaugeMarkerVerifyBot.ShotsDir}/{label}.png";
                File.Copy(p, dest, true);
                var px = new Texture2D(2, 2);
                px.LoadImage(File.ReadAllBytes(dest));
                Assert($"capture.{label}", px.width >= 900 || px.height >= 900,
                       $"{dest} is {px.width}x{px.height}");
                UnityEngine.Object.DestroyImmediate(px);
            }
            else
            {
                Assert($"capture.{label}", false,
                       $"capture MISSING (SnapPlayModeSafe returned '{p}' but no file exists) — " +
                       "editor unfocused?");
            }
        }

        // ── sequence ────────────────────────────────────────────────────────────
        IEnumerator Sequence()
        {
            _json.AppendLine("{");
            _json.AppendLine("  \"task\": \"power_gauge_target_marker\",");
            _json.AppendLine("  \"entry\": \"ShellScene boot -> PLAY -> mode card -> Hole 1 card -> real HoleMap button\",");
            _json.AppendLine("  \"assertions\": [");

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

            _widget = UnityEngine.Object.FindObjectsByType<PowerGaugeWidget>(
                          FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
            _gauge  = UnityEngine.Object.FindObjectsByType<PowerGaugeGraphic>(
                          FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();

            Assert("harness.shot_controller_bound", BindShotController(), "ShotController resolved via reflection");
            Assert("harness.gauge_found", _widget != null && _gauge != null,
                   $"PowerGaugeWidget={_widget != null} PowerGaugeGraphic={_gauge != null}");
            if (_sc == null || _widget == null || _gauge == null) { Finish(); yield break; }

            int   clubYards  = ClubContext.SelectedDistance;
            float serialized = (float)(typeof(PowerGaugeWidget)
                                 .GetField("_maxCarryYards", NP)?.GetValue(_widget) ?? 0f);

            // ── A. No map session yet → no marker ────────────────────────────────
            Assert("A.default_no_target", Mathf.Approximately(MapTargetM, -1f),
                   $"MapTargetCarryM={MapTargetM:F2} at the tee before any map session");

            yield return HoldGaugeAt(0.6f, 1.0f);
            Assert("A.no_notch_without_target", _gauge.MarkerFrac01 < 0f,
                   $"MarkerFrac01={_gauge.MarkerFrac01:F3} (expect <0 = notch not drawn)");
            yield return SnapCo("A_no_target_no_notch");
            ReleaseWithoutShooting();
            yield return new WaitForSecondsRealtime(0.6f);

            // ── B. Map a target → close → notch at the matching % ────────────────
            yield return ClickWhenPresent("HoleMap");
            yield return new WaitForSecondsRealtime(4.5f);   // §11 invariant dump re-aims twice

            var mvc = FindMvc();
            Assert("B.map_opened", mvc != null && mvc.IsOpen, $"MapViewController.IsOpen={mvc?.IsOpen}");

            // Production touch-follow path: a screen point below centre = a target short of
            // the default club-carry landing, so the expected notch is comfortably under 100%.
            bool placed = mvc != null && mvc.TrySetAimFromScreenPoint(
                              new Vector2(Screen.width * 0.5f, Screen.height * 0.42f));
            Assert("B.target_placed_via_production_path", placed,
                   "MapViewController.TrySetAimFromScreenPoint (the finger's own call) returned true");
            yield return new WaitForSecondsRealtime(1.0f);
            yield return SnapCo("B_map_target_placed");

            float aimedCarryM = (float)(typeof(MapViewController).GetField("_aimedCarryM", NP)?.GetValue(mvc) ?? -1f);

            var shoot = typeof(MapViewController).GetField("_shootButton", NP).GetValue(mvc) as Button;
            Assert("B.shoot_button_is_real_widget", shoot != null,
                   "map SHOOT button resolved; closing through its own onClick");
            if (shoot != null) ClickReal(shoot);
            yield return new WaitForSecondsRealtime(1.5f);

            Assert("B.writeback_metres", Mathf.Abs(MapTargetM - aimedCarryM) < 0.01f,
                   $"MapTargetCarryM={MapTargetM:F2}m == map _aimedCarryM={aimedCarryM:F2}m");

            float expectFrac = Mathf.Clamp(MapTargetM / (clubYards * kYardsToMeters), 0.02f, 1.2f);
            yield return HoldGaugeAt(0.6f, 1.2f);
            Assert("B.notch_at_expected_pct", Mathf.Abs(_gauge.MarkerFrac01 - expectFrac) < kFracTol,
                   $"MarkerFrac01={_gauge.MarkerFrac01:F4} expected={expectFrac:F4} " +
                   $"(target {MapTargetM:F1}m / club {clubYards}yd = {clubYards * kYardsToMeters:F1}m)");
            Assert("B.notch_not_unreachable", !_gauge.MarkerUnreachable,
                   $"MarkerUnreachable={_gauge.MarkerUnreachable} for an in-reach target");

            // Yards text: the §3.2 wiring fix. BEFORE, _maxCarryYards was never written by
            // anything, so the text read 250 x power regardless of the club in hand.
            // NOTE: with a 250yd driver in hand this assertion CANNOT discriminate old from
            // new (250 == the old default) — the discriminating measurement is E, after the
            // club change. This row only records the starting state.
            var distText = typeof(PowerGaugeWidget).GetField("_distanceText", NP)?.GetValue(_widget) as TMPro.TMP_Text;
            string shown = distText != null ? distText.text : "<null>";
            Assert("B.yards_text_tracks_club",
                   distText != null && Mathf.Abs(clubYards * 0.6f - ParseYards(shown)) < 1.0f,
                   $"gauge reads '{shown}' at 60% power; club={clubYards}yd -> expect {clubYards * 0.6f:F1} yd. " +
                   $"serialized _maxCarryYards={serialized:F0}; NOT discriminating when club==250yd");
            yield return SnapCo("B_notch_at_mapped_target");
            ReleaseWithoutShooting();
            yield return new WaitForSecondsRealtime(0.6f);

            // ── C. Club change → same target, notch moves ────────────────────────
            float fracBefore   = _gauge.MarkerFrac01;
            int   yardsBefore  = clubYards;
            int   yardsAfter   = yardsBefore;
            if (ClubContext.EquippedBag != null && ClubContext.EquippedBag.Count > 1)
            {
                int next = (ClubContext.SelectedIndex + 1) % ClubContext.EquippedBag.Count;
                ClubContext.RequestSelection(next);          // the widget's own request path
                yield return new WaitForSecondsRealtime(1.5f);
                yardsAfter = ClubContext.SelectedDistance;
            }
            yield return HoldGaugeAt(0.6f, 1.2f);
            float fracAfter = _gauge.MarkerFrac01;
            Assert("C.club_change_moves_notch",
                   yardsAfter == yardsBefore || Mathf.Abs(fracAfter - fracBefore) > kFracTol,
                   $"club {yardsBefore}yd -> {yardsAfter}yd; frac {fracBefore:F4} -> {fracAfter:F4} " +
                   $"(target unchanged at {MapTargetM:F1}m)");
            Assert("C.target_metres_unchanged", MapTargetM > 0f,
                   $"MapTargetCarryM={MapTargetM:F2}m survived the club change (stored in metres, not %)");

            // THE discriminating yards-text measurement (§3.2). The club is no longer 250yd, so
            // the OLD code — _maxCarryYards stuck at its never-written 250f default — would print
            // 250 x power here no matter which club is in hand.
            string shownAfter = distText != null ? distText.text : "<null>";
            float  measured   = ParseYards(shownAfter);
            Assert("C.yards_text_discriminating",
                   distText != null && yardsAfter != 250 && Mathf.Abs(yardsAfter * 0.6f - measured) < 1.0f,
                   $"club now {yardsAfter}yd; gauge reads '{shownAfter}' at 60% power = {measured:F1} yd " +
                   $"(AFTER: {yardsAfter * 0.6f:F1} yd). BEFORE this change the SAME frame read " +
                   $"{250f * 0.6f:F1} yd — the 250f default that nothing ever wrote.");
            yield return SnapCo("C_notch_after_club_change");

            // ── D. Commit the shot → notch gone next stroke ───────────────────────
            _mEndDrag.Invoke(_sc, new object[] { true });    // bypassFlickGate: programmatic release
            yield return new WaitForSecondsRealtime(2.0f);
            Assert("D.commit_clears_target", Mathf.Approximately(MapTargetM, -1f),
                   $"MapTargetCarryM={MapTargetM:F2} after the committed flick");
            yield return new WaitForSecondsRealtime(6.0f);   // let the ball settle
            yield return HoldGaugeAt(0.6f, 1.2f);
            Assert("D.no_notch_next_stroke", _gauge.MarkerFrac01 < 0f,
                   $"MarkerFrac01={_gauge.MarkerFrac01:F3} on the stroke after the shot");
            yield return SnapCo("D_no_notch_after_shot");
            ReleaseWithoutShooting();

            // ── E. Putter mode → never a notch ───────────────────────────────────
            _widget.SetUnitMode(PowerGaugeWidget.DistanceUnit.Meters);
            typeof(PowerGaugeWidget).GetField("_maxCarryYards", NP)?.SetValue(_widget, (float)yardsAfter);
            _pMapTarget.SetValue(_sc, 40f);                  // a target that WOULD mark in Yards mode
            yield return HoldGaugeAt(0.6f, 1.2f);
            Assert("E.putter_mode_forces_no_notch", _gauge.MarkerFrac01 < 0f,
                   $"MarkerFrac01={_gauge.MarkerFrac01:F3} in Meters/putter mode with MapTargetCarryM=40m set");
            yield return SnapCo("E_putter_mode_no_notch");
            ReleaseWithoutShooting();
            _widget.SetUnitMode(PowerGaugeWidget.DistanceUnit.Yards);
            _pMapTarget.SetValue(_sc, -1f);

            Finish();
            yield return new WaitForSecondsRealtime(1.0f);
            EditorApplication.ExitPlaymode();
        }

        static float ParseYards(string s)
        {
            var digits = new string(s.TakeWhile(c => char.IsDigit(c) || c == '.' || c == '-').ToArray());
            return float.TryParse(digits, System.Globalization.NumberStyles.Float,
                                  System.Globalization.CultureInfo.InvariantCulture, out float v) ? v : float.NaN;
        }

        void Finish()
        {
            // trim the trailing comma of the last assertion
            string body = _json.ToString().TrimEnd('\n', '\r');
            if (body.EndsWith(",")) body = body.Substring(0, body.Length - 1);

            var final = new StringBuilder(body);
            final.AppendLine();
            final.AppendLine("  ],");
            final.AppendLine($"  \"total\": {_asserts},");
            final.AppendLine($"  \"fail\": {_fails},");
            final.AppendLine($"  \"verdict\": \"{(_fails == 0 ? "PASS" : "FAIL")}\"");
            final.AppendLine("}");

            Directory.CreateDirectory(Path.GetDirectoryName(PowerGaugeMarkerVerifyBot.JsonPath));
            File.WriteAllText(PowerGaugeMarkerVerifyBot.JsonPath, final.ToString());
            Debug.Log($"[MarkerVerify] {_asserts - _fails}/{_asserts} PASS → {PowerGaugeMarkerVerifyBot.JsonPath}");
        }
    }
}
#endif
