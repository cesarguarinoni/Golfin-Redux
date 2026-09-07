#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Golfin.Diagnostics.Runtime;
using Golfin.Gameplay.UI.Controls;
using Golfin.Gameplay.UI.ShotUI;

namespace Golfin.UI.EditorTools
{
    /// <summary>
    /// flick_pull_mapping § 4 — the acceptance run for Flick's rest-relative pull.
    ///
    /// <para>REAL ENTRY PATH, not a render harness (Cesar's standing rule): boots
    /// <c>ShellScene</c>, taps the title screen's own StartButton, PLAY and a hole card, then
    /// drives the REAL <see cref="ClubHandleDragger"/> pointer handlers. The boot idiom is
    /// <see cref="FlickShotViewVerify"/>'s, verbatim.</para>
    ///
    /// <para>WHAT IT ACTUALLY MEASURES, and why a screenshot could not: the claim is
    /// "the gauge reads 0% at touch, 100% at 540px and 120% at 648px". The finger is put at each
    /// of those four cone-local depths and <b>the gauge's own percentage TEXT</b> is read back —
    /// not <c>PowerNormalized</c> alone, because the number Cesar is complaining about is the one
    /// the widget prints. Alongside it, the DRAWN club's y at each depth, so "the club and the
    /// finger coincide" is a subtraction rather than an impression.</para>
    ///
    /// <para>Menu: <b>GOLFIN ▸ ShotUI ▸ Verify Flick Pull Mapping</b>.</para>
    /// </summary>
    public static class FlickPullMappingVerify
    {
        const string ShellScenePath = "Assets/Scenes/ShellScene.unity";
        const string ArmedKey       = "FlickPullMappingVerify.Armed";
        public const string TaskDir = "Docs/Specs/Active/flick_pull_mapping";

        [InitializeOnLoadMethod]
        static void Hook() => EditorApplication.playModeStateChanged += OnPlayModeChanged;

        [MenuItem("GOLFIN/ShotUI/Verify Flick Pull Mapping")]
        public static void Launch()
        {
            if (EditorApplication.isPlaying)
            { Debug.LogWarning("[FlickPull] Already in play mode — stop first."); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            EditorSceneManager.OpenScene(ShellScenePath);
            Directory.CreateDirectory($"{TaskDir}/screenshots");
            Directory.CreateDirectory($"{TaskDir}/evidence");
            SessionState.SetBool(ArmedKey, true);
            EditorApplication.EnterPlaymode();
            Debug.Log("[FlickPull] Armed. Entering play mode…");
        }

        static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode) return;
            if (!SessionState.GetBool(ArmedKey, false)) return;
            SessionState.SetBool(ArmedKey, false);

            Application.runInBackground = true;   // MANDATORY for MCP-driven runs
            Golfin.EditorTools.ShotUI.PendulumSchemeVerify.ForceCaptureResolution();
            var host = new GameObject("[FlickPullMappingBot]");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.AddComponent<FlickPullMappingRunner>();
        }
    }

    public class FlickPullMappingRunner : MonoBehaviour
    {
        const BindingFlags NP = BindingFlags.NonPublic | BindingFlags.Instance;
        const BindingFlags AN = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance;

        static readonly int[] HolePreference = { 2, 1, 10, 4, 9 };

        readonly List<string> _log = new List<string>();
        readonly List<(string name, bool pass, string detail)> _asserts = new List<(string, bool, string)>();
        readonly Dictionary<string, object> _json = new Dictionary<string, object>();

        void Note(string k, object v) { _log.Add($"{k}: {v}"); Debug.Log($"[FlickPull] {k}: {v}"); }
        void Assert(string name, bool pass, string detail)
        {
            _asserts.Add((name, pass, detail));
            Debug.Log($"[FlickPull] {(pass ? "PASS" : "FAIL")} {name} — {detail}");
        }

        /// <summary><c>Golfin.Gameplay.Config</c> is autoReferenced:false, so the tuning struct is
        /// read by reflection — <see cref="FlickShotViewVerify"/>'s idiom, verbatim.</summary>
        static readonly Dictionary<string, float> _cfgFields = ReadConfig();

        static Dictionary<string, float> ReadConfig()
        {
            var map = new Dictionary<string, float>();
            var t = AppDomain.CurrentDomain.GetAssemblies()
                      .FirstOrDefault(a => a.GetName().Name == "Golfin.Gameplay.Config")
                      ?.GetType("Golfin.Gameplay.Config.ControlsConfig");
            object def = t?.GetField("Default", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
            if (def == null) return map;
            foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.Instance))
                if (f.FieldType == typeof(float)) map[f.Name] = (float)f.GetValue(def);
            return map;
        }

        static float Cfg(string key) => _cfgFields.TryGetValue(key, out float v) ? v : float.NaN;

        void Start() => StartCoroutine(Sequence());

        // ── boot helpers (FlickShotViewVerify's, verbatim) ───────────────────────

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
            Debug.LogWarning($"[FlickPull] TIMEOUT waiting for '{goName}'");
        }

        static IEnumerable<MonoBehaviour> HoleCards() => UnityEngine.Object
            .FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
            .Where(m => m.GetType().Name == "HoleCardController");

        static bool HoleCardExists(int hole)
        {
            foreach (var c in HoleCards())
            {
                var pr = c.GetType().GetProperty("HoleNumber");
                if (pr == null || (int)pr.GetValue(c) != hole) continue;
                if (c.GetType().GetField("actionButton", NP)?.GetValue(c) is Button b && b.interactable)
                    return true;
            }
            return false;
        }

        IEnumerator ClickHoleCard(int hole, float timeout = 30f)
        {
            float t = 0f;
            while (t < timeout)
            {
                foreach (var c in HoleCards())
                {
                    var p = c.GetType().GetProperty("HoleNumber");
                    if (p == null || (int)p.GetValue(c) != hole) continue;
                    if (c.GetType().GetField("actionButton", NP)?.GetValue(c) is Button btn)
                    { ClickReal(btn); yield break; }
                }
                yield return new WaitForSecondsRealtime(0.25f); t += 0.25f;
            }
            Debug.LogWarning($"[FlickPull] TIMEOUT waiting for hole {hole} card");
        }

        // ── live geometry ───────────────────────────────────────────────────────
        RectTransform   _canvasRect, _ball, _coneMesh, _clubHandle;
        TMP_Text        _label100, _label120;
        ConeMeshGraphic _coneGraphic;
        ShotConeView    _coneView;
        ClubHandleDragger _dragger;
        RectTransform   _coneRect;
        GraphicRaycaster _caster;
        Camera          _uiCam;
        Component       _sc;                    // ShotController — not a referenced assembly
        PropertyInfo    _pPower, _pIsPutt;
        TMP_Text        _pctText;

        float CanvasH => _canvasRect.rect.height;

        float CanvasY(RectTransform rt, float pivotY01)
        {
            var c = new Vector3[4];
            rt.GetWorldCorners(c);                       // 0 BL, 1 TL, 2 TR, 3 BR
            return _canvasRect.InverseTransformPoint(Vector3.Lerp(c[0], c[1], pivotY01)).y;
        }

        float Power   => _pPower != null ? (float)_pPower.GetValue(_sc) : float.NaN;
        string GaugeText => _pctText != null ? _pctText.text : "<no gauge>";

        bool Bind()
        {
            _coneView = UnityEngine.Object.FindObjectsByType<ShotConeView>(
                            FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
            if (_coneView == null) return false;

            var vt = typeof(ShotConeView);
            _coneGraphic = vt.GetField("_coneGraphic", NP)?.GetValue(_coneView) as ConeMeshGraphic;
            _clubHandle  = vt.GetField("_clubHandle",  NP)?.GetValue(_coneView) as RectTransform;
            _label100    = vt.GetField("_label100",    NP)?.GetValue(_coneView) as TMP_Text;
            _label120    = vt.GetField("_label120",    NP)?.GetValue(_coneView) as TMP_Text;
            _coneMesh    = _coneGraphic != null ? _coneGraphic.rectTransform : null;

            _dragger = UnityEngine.Object.FindObjectsByType<ClubHandleDragger>(
                           FindObjectsInactive.Exclude, FindObjectsSortMode.None).FirstOrDefault();
            if (_dragger != null)
            {
                _coneRect = typeof(ClubHandleDragger).GetField("_coneRect", NP)?.GetValue(_dragger) as RectTransform;
                var canvas = _dragger.GetComponentInParent<Canvas>();
                _caster = canvas != null ? canvas.rootCanvas.GetComponent<GraphicRaycaster>() : null;
                _uiCam  = _caster != null ? _caster.eventCamera : null;
            }

            var t = AppDomain.CurrentDomain.GetAssemblies()
                      .FirstOrDefault(a => a.GetName().Name == "Golfin.Gameplay.Input")
                      ?.GetType("Golfin.Gameplay.Input.ShotController");
            if (t != null)
            {
                _sc = UnityEngine.Object.FindObjectsByType(t, FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                        .FirstOrDefault() as Component;
                _pPower  = t.GetProperty("PowerNormalized");
                _pIsPutt = t.GetProperty("IsPutt");
            }

            var gauge = UnityEngine.Object.FindObjectsByType<PowerGaugeWidget>(
                            FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
            if (gauge != null)
                _pctText = typeof(PowerGaugeWidget).GetField("_pctText", NP)?.GetValue(gauge) as TMP_Text;

            var layout = ShotLayoutController.Active;
            if (layout != null)
            {
                _canvasRect = typeof(ShotLayoutController).GetField("_canvasRect", NP)?.GetValue(layout) as RectTransform;
                _ball       = typeof(ShotLayoutController).GetField("_centralBall", NP)?.GetValue(layout) as RectTransform;
            }
            return _canvasRect != null && _ball != null && _coneMesh != null && _clubHandle != null
                   && _dragger != null && _coneRect != null && _sc != null;
        }

        // ── real pointer driving ────────────────────────────────────────────────

        Vector2 ConeLocalToScreen(float localY) => RectTransformUtility.WorldToScreenPoint(
            _uiCam, _coneRect.TransformPoint(new Vector3(0f, localY, 0f)));

        PointerEventData _ped;
        Vector2 _lastPointer;

        void PointerDownAt(Vector2 p)
        {
            var rr = new RaycastResult { module = _caster, screenPosition = p };
            _ped = new PointerEventData(EventSystem.current)
            { position = p, pointerId = 0, button = PointerEventData.InputButton.Left };
            _ped.pointerPressRaycast = _ped.pointerCurrentRaycast = rr;
            _lastPointer = p;
            ExecuteEvents.Execute(_dragger.gameObject, _ped, ExecuteEvents.pointerDownHandler);
        }

        void DragTo(Vector2 p)
        {
            _ped.delta = p - _lastPointer;
            _ped.position = p;
            _lastPointer = p;
            ExecuteEvents.Execute(_dragger.gameObject, _ped, ExecuteEvents.dragHandler);
        }

        /// <summary>Release with NO upward motion, so <c>EvaluateFlickGate</c> rejects it and the
        /// swing resets. The whole run therefore measures the control without ever taking a shot,
        /// which is what keeps the ball on the tee for the frames after each one.</summary>
        void PointerUpNoFlick()
        {
            _ped.delta = Vector2.zero;
            ExecuteEvents.Execute(_dragger.gameObject, _ped, ExecuteEvents.pointerUpHandler);
        }

        // ── frame capture ───────────────────────────────────────────────────────

        IEnumerator SaveFrame(string label, string destName, Action<string> onDone)
        {
            yield return new WaitForEndOfFrame();
            string snapped = CaptureCore.SnapPlayModeSafe(label);
            if (string.IsNullOrEmpty(snapped) || !File.Exists(snapped) ||
                new FileInfo(snapped).Length < 1024)
            { Note($"{label}_CAPTURE_FAILED", $"SnapPlayModeSafe returned '{snapped}'"); onDone(null); yield break; }

            string dst = Path.Combine(FlickPullMappingVerify.TaskDir, "screenshots", destName);
            File.Copy(snapped, dst, true);
            Note($"{label}_png", $"{dst} ({new FileInfo(dst).Length / 1024} KB, md5 {Md5(dst)})");
            onDone(dst);
        }

        static string Md5(string path)
        {
            using (var md5 = System.Security.Cryptography.MD5.Create())
            using (var fs = File.OpenRead(path))
                return BitConverter.ToString(md5.ComputeHash(fs)).Replace("-", "").Substring(0, 8).ToLowerInvariant();
        }

        // ── the run ─────────────────────────────────────────────────────────────

        IEnumerator Sequence()
        {
            yield return new WaitForSecondsRealtime(5f);
            yield return ClickWhenPresent("StartButton", 20f);
            yield return new WaitForSecondsRealtime(2.5f);
            yield return ClickWhenPresent("PlayButton");
            yield return new WaitForSecondsRealtime(2.5f);

            int hole = 0;
            foreach (int h in HolePreference)
            {
                if (!HoleCardExists(h)) continue;
                hole = h;
                yield return ClickHoleCard(h);
                break;
            }
            if (hole == 0) { Fail("no hole card was available"); yield break; }
            Note("hole", hole);

            float t0 = 0f;
            while (FindButton("HoleMap") == null && t0 < 120f)
            { yield return new WaitForSecondsRealtime(0.5f); t0 += 0.5f; }
            yield return new WaitForSecondsRealtime(10f);

            if (ControlSchemeService.Current != ControlScheme.Flick)
                ControlSchemeService.Set(ControlScheme.Flick, "flick_pull_mapping acceptance");
            yield return new WaitForSecondsRealtime(1.5f);

            if (!Bind()) { Fail("could not bind the shot-view rects / dragger / controller"); yield break; }

            Note("resolution", $"{Screen.width}x{Screen.height}");
            Note("canvas", $"{_canvasRect.rect.width} x {CanvasH}");
            Note("scheme", ControlSchemeService.Current);
            Note("gauge_text_bound", _pctText != null ? _pctText.name : "<none>");

            yield return MeasureGeometry();
            yield return MeasureGauge();
            yield return MeasureLateralReach();
            yield return MeasurePutt();

            Write();
            yield return new WaitForSecondsRealtime(1f);
            EditorApplication.ExitPlaymode();
        }

        // ── § 4: the rest and the two tick lines, in canvas y ───────────────────

        IEnumerator MeasureGeometry()
        {
            yield return null;
            float ballY    = _ball.anchoredPosition.y;
            float coneBase = CanvasY(_coneMesh, 0f);
            float coneH    = _coneGraphic.HeightPx;
            float restLocal = _coneView.HandleRestYPx;
            float restY    = coneBase + restLocal;
            // The LINES are gone (Cesar, 2026-09-07: "remove the 100% and 120% lines, leave only
            // the labels"), so the mark's height is read off the LABEL's own vertical centre.
            float tick100Y = _label100 != null ? CanvasY(_label100.rectTransform, 0.5f) : float.NaN;
            float tick120Y = _label120 != null ? CanvasY(_label120.rectTransform, 0.5f) : float.NaN;

            _json["hole_resolution"]   = $"{Screen.width}x{Screen.height}";
            _json["canvas_height"]     = CanvasH;
            _json["ball_y"]            = ballY;
            _json["cone_base_y"]       = coneBase;
            _json["cone_height_px"]    = coneH;
            _json["handle_rest_local"] = restLocal;
            _json["handle_rest_y"]     = restY;
            _json["tick100_y"]         = tick100Y;
            _json["tick120_y"]         = tick120Y;
            _json["label100_x"]        = _label100 != null ? _label100.rectTransform.anchoredPosition.x : float.NaN;
            _json["label120_x"]        = _label120 != null ? _label120.rectTransform.anchoredPosition.x : float.NaN;
            _json["no_tick_lines"]     = NoTickLinesUnderTheCone();

            Assert("rest_is_pull120_above_the_base",
                   Mathf.Abs(restLocal - Cfg("FlickPull120Px")) <= 1f,
                   $"rest {restLocal:F2}px above the base vs FlickPull120Px {Cfg("FlickPull120Px"):F0}");
            Assert("rest_canvas_y_448",
                   Mathf.Abs(restY - (-447.84f)) <= 2f,
                   $"rest at canvas y {restY:F2} (spec: -447.84 = base {coneBase:F2} + 648)");
            Assert("club_sits_144_under_the_ball",
                   Mathf.Abs((ballY - restY) - 144f) <= 2f,
                   $"ball {ballY:F2} - rest {restY:F2} = {ballY - restY:F2}px");
            Assert("label100_canvas_y_988",
                   Mathf.Abs(tick100Y - (-987.84f)) <= 2f, $"100% label centred at {tick100Y:F2} (spec -987.84)");
            Assert("label120_canvas_y_1096",
                   Mathf.Abs(tick120Y - (-1095.84f)) <= 2f, $"120% label centred at {tick120Y:F2} (spec -1095.84)");
            Assert("label120_is_at_the_cone_base",
                   Mathf.Abs(tick120Y - coneBase) <= 1f,
                   $"120% label {tick120Y:F2} vs cone base {coneBase:F2}");
            Assert("label_gap_is_pull120_minus_pull100",
                   Mathf.Abs((tick100Y - tick120Y) - (Cfg("FlickPull120Px") - Cfg("FlickPull100Px"))) <= 1f,
                   $"{tick100Y - tick120Y:F2}px apart vs 648-540 = 108");
            // Cesar's call, asserted rather than assumed: the two drawn rules are GONE from the
            // live hierarchy, not merely deactivated or moved off-screen.
            Assert("the_tick_lines_are_gone", NoTickLinesUnderTheCone(),
                   "no Tick100/Tick120 GameObject survives under ConeMesh");
        }

        /// <summary>True when neither retired line GameObject exists under the cone, active or
        /// not. Checked against the LIVE hierarchy: a builder that only deactivated them would
        /// still be shipping the thing Cesar asked to remove.</summary>
        bool NoTickLinesUnderTheCone()
        {
            if (_coneMesh == null) return false;
            foreach (Transform c in _coneMesh)
                if (c.name == "Tick100" || c.name == "Tick120") return false;
            return true;
        }

        // ── § 4: the gauge reading at four depths ───────────────────────────────

        /// <summary>The four rows of the acceptance table. Each is a REAL pointer press on the club
        /// followed by a drag to the exact cone-local y that pull corresponds to, held for a few
        /// frames so the widget has actually repainted, and then read back off the gauge's own
        /// TEXT — plus the drawn club's y, which is the "club and finger coincide" claim.</summary>
        IEnumerator MeasureGauge()
        {
            float coneH = _coneGraphic.HeightPx;
            float rest  = _coneView.HandleRestYPx;

            var rows = new List<string>();
            foreach (var (pull, expect, shot, label) in new (float, float, string, string)[]
            {
                (0f,   0f,   "flick_pull_000_rest",  "touch (0px)"),
                (40f,  0f,   null,                   "dead zone (40px)"),
                (290f, 0.5f, null,                   "half (290px)"),
                (540f, 1f,   "flick_pull_540_100pct","100% (540px)"),
                (648f, 1.2f, "flick_pull_648_120pct","120% (648px) = the base"),
                (700f, 1.2f, null,                   "past the base (700px)"),
            })
            {
                float targetLocal = Mathf.Clamp(rest - pull, 0f, coneH);
                Vector2 start = ConeLocalToScreen(rest);
                Vector2 hold  = ConeLocalToScreen(targetLocal);

                PointerDownAt(start);
                yield return null;
                float powerAtTouch = Power;
                string gaugeAtTouch = GaugeText;

                for (int i = 1; i <= 8; i++) { DragTo(Vector2.Lerp(start, hold, i / 8f)); yield return null; }
                for (int i = 0; i < 4; i++) { DragTo(hold); yield return null; }

                float measured   = Power;
                string gauge     = GaugeText;
                float clubY      = CanvasY(_clubHandle, 0f);
                float fingerY    = CanvasY(_coneMesh, 0f) + targetLocal;

                _json[$"power_at_{(int)pull}px"]      = measured;
                _json[$"gauge_at_{(int)pull}px"]      = gauge;
                _json[$"club_y_at_{(int)pull}px"]     = clubY;
                _json[$"finger_y_at_{(int)pull}px"]   = fingerY;
                rows.Add($"| {label} | {gauge} | {measured:F4} | {expect:F2} | {clubY:F2} | {fingerY:F2} | {clubY - fingerY:+0.00;-0.00} |");

                Assert($"power_at_{(int)pull}px", Mathf.Abs(measured - expect) <= 0.005f,
                       $"{label}: gauge '{gauge}', PowerNormalized {measured:F4} (expected {expect:F2})");
                // The dead zone is a flat segment of the mapping, so the club STAYS AT REST through
                // it — deliberately (FlickPullMath.PullPxForPower). Coincidence is asserted only
                // where the mapping is invertible, i.e. from the first pull that reads > 0.
                if (measured > 0f)
                    Assert($"club_meets_finger_at_{(int)pull}px", Mathf.Abs(clubY - fingerY) <= 2f,
                           $"{label}: club {clubY:F2} vs finger {fingerY:F2} ({clubY - fingerY:F2}px apart)");

                if (pull == 0f)
                {
                    Assert("gauge_reads_zero_the_instant_the_club_is_touched",
                           Mathf.Abs(powerAtTouch) <= 0.005f,
                           $"on pointer-down, before any drag: gauge '{gaugeAtTouch}', power {powerAtTouch:F4} " +
                           $"(was 0.318 under the base-relative mapping)");
                }

                if (shot != null)
                {
                    string png = null;
                    yield return SaveFrame(shot, shot + ".png", p => png = p);
                    _json[$"frame_{(int)pull}px"] = png ?? "<none>";
                }

                PointerUpNoFlick();
                yield return new WaitForSecondsRealtime(1.2f);
            }

            _json["gauge_table"] = string.Join("\n", rows);
        }

        // ── § 4 (D5): lateral aim still spans the cone at 100% ──────────────────

        IEnumerator MeasureLateralReach()
        {
            float coneH = _coneGraphic.HeightPx;
            float rest  = _coneView.HandleRestYPx;
            float local = rest - Cfg("FlickPull100Px");

            float halfBase = coneH * Mathf.Tan(_coneGraphic.HalfAngleDeg * Mathf.Deg2Rad);
            float maxX     = halfBase * (1f - local / coneH);

            Vector2 start = ConeLocalToScreen(rest);
            PointerDownAt(start);
            yield return null;
            for (int i = 1; i <= 8; i++)
            { DragTo(Vector2.Lerp(start, ConeLocalToScreen(local), i / 8f)); yield return null; }

            // Overshoot deliberately: ProcessDrag clamps to +/-maxX, which IS finetune +/-1.
            float reachedRight = 0f, reachedLeft = 0f;
            for (int side = 0; side < 2; side++)
            {
                float sign = side == 0 ? 1f : -1f;
                Vector3 world = _coneRect.TransformPoint(new Vector3(sign * maxX * 1.1f, local, 0f));
                for (int i = 0; i < 4; i++)
                { DragTo(RectTransformUtility.WorldToScreenPoint(_uiCam, world)); yield return null; }
                float reached = HandleFinetune();
                if (side == 0) reachedRight = reached; else reachedLeft = reached;
            }
            PointerUpNoFlick();
            yield return new WaitForSecondsRealtime(1.2f);

            _json["lateral_maxX_at_100pct"] = maxX;
            _json["finetune_right"] = reachedRight;
            _json["finetune_left"]  = reachedLeft;
            Assert("lateral_aim_still_reaches_plus_one_at_100pct", reachedRight >= 0.99f,
                   $"finetune right {reachedRight:F3} at maxX {maxX:F1}px");
            Assert("lateral_aim_still_reaches_minus_one_at_100pct", reachedLeft <= -0.99f,
                   $"finetune left {reachedLeft:F3}");
        }

        float HandleFinetune()
        {
            var p = _sc.GetType().GetProperty("HandleFinetune");
            return p != null ? (float)p.GetValue(_sc) : float.NaN;
        }

        // ── § 4: a putt caps at 100% and hides the 120% tick ────────────────────

        IEnumerator MeasurePutt()
        {
            if (_pIsPutt == null) { Note("putt", "IsPutt not reachable — skipped"); yield break; }

            bool wasPutt = (bool)_pIsPutt.GetValue(_sc);
            _pIsPutt.SetValue(_sc, true);
            _coneView.SetPuttMode(true);
            yield return new WaitForSecondsRealtime(0.5f);

            bool tick120Shown = _label120 != null && _label120.gameObject.activeSelf;
            bool tick100Shown = _label100 != null && _label100.gameObject.activeSelf;

            float coneH = _coneGraphic.HeightPx;
            float rest  = _coneView.HandleRestYPx;
            Vector2 start = ConeLocalToScreen(rest);
            PointerDownAt(start);
            yield return null;
            for (int i = 1; i <= 8; i++)
            { DragTo(Vector2.Lerp(start, ConeLocalToScreen(0f), i / 8f)); yield return null; }
            for (int i = 0; i < 4; i++) { DragTo(ConeLocalToScreen(0f)); yield return null; }

            float puttPowerAtBase = Power;
            string puttGauge = GaugeText;
            string puttPng = null;
            yield return SaveFrame("flick_pull_putt_base", "flick_pull_putt_base.png", p => puttPng = p);
            PointerUpNoFlick();
            yield return new WaitForSecondsRealtime(1.0f);

            _json["putt_power_at_base"] = puttPowerAtBase;
            _json["putt_gauge_at_base"] = puttGauge;
            _json["putt_label120_shown"] = tick120Shown;
            _json["putt_label100_shown"] = tick100Shown;
            _json["frame_putt"]         = puttPng ?? "<none>";

            Assert("putt_base_reads_100pct", Mathf.Abs(puttPowerAtBase - 1f) <= 0.005f,
                   $"gauge '{puttGauge}', power {puttPowerAtBase:F4} at the base");
            Assert("putt_hides_the_120_label", !tick120Shown, $"Label120 active={tick120Shown}");
            Assert("putt_keeps_the_100_label",  tick100Shown, $"Label100 active={tick100Shown}");

            // Put the world back exactly as it was found (feedback_restore_playable_state).
            _coneView.SetPuttMode(false);
            _pIsPutt.SetValue(_sc, wasPutt);
            yield return new WaitForSecondsRealtime(0.5f);
        }

        void Fail(string why) { Note("ABORT", why); Write(); EditorApplication.ExitPlaymode(); }

        static string J(object v)
        {
            switch (v)
            {
                case null:    return "null";
                case bool b:  return b ? "true" : "false";
                case float f: return float.IsNaN(f) ? "null" : f.ToString("F4", CultureInfo.InvariantCulture);
                case int i:   return i.ToString(CultureInfo.InvariantCulture);
                default:      return "\"" + v.ToString().Replace("\\", "\\\\").Replace("\"", "\\\"")
                                             .Replace("\n", "\\n") + "\"";
            }
        }

        void Write()
        {
            string dir = Path.Combine(FlickPullMappingVerify.TaskDir, "evidence");
            Directory.CreateDirectory(dir);

            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine("  \"task\": \"flick_pull_mapping\",");
            sb.AppendLine("  \"measured\": {");
            sb.AppendLine(string.Join(",\n", _json.Select(kv => $"    \"{kv.Key}\": {J(kv.Value)}")));
            sb.AppendLine("  },");
            sb.AppendLine("  \"assertions\": [");
            sb.AppendLine(string.Join(",\n", _asserts.Select(a =>
                $"    {{ \"name\": \"{a.name}\", \"verdict\": \"{(a.pass ? "PASS" : "FAIL")}\", \"detail\": {J(a.detail)} }}")));
            sb.AppendLine("  ],");
            sb.AppendLine($"  \"fail_count\": {_asserts.Count(a => !a.pass)}");
            sb.AppendLine("}");
            File.WriteAllText(Path.Combine(dir, "flick_pull_mapping_invariants.json"), sb.ToString());

            var md = new StringBuilder();
            md.AppendLine("# flick_pull_mapping — live pull measurements");
            md.AppendLine();
            md.AppendLine("Booted ShellScene -> StartButton -> PLAY -> hole card, then drove the REAL");
            md.AppendLine("ClubHandleDragger pointer handlers. Every number read off live rects and the");
            md.AppendLine("power gauge's own text in play mode.");
            md.AppendLine();
            if (_json.TryGetValue("gauge_table", out object tbl))
            {
                md.AppendLine("| pull | gauge text | PowerNormalized | expected | club y | finger y | delta |");
                md.AppendLine("|---|---|---|---|---|---|---|");
                md.AppendLine(tbl.ToString());
                md.AppendLine();
            }
            md.AppendLine("| assertion | verdict | detail |");
            md.AppendLine("|---|---|---|");
            foreach (var a in _asserts) md.AppendLine($"| {a.name} | {(a.pass ? "PASS" : "**FAIL**")} | {a.detail} |");
            md.AppendLine();
            md.AppendLine("## Log");
            foreach (var l in _log) md.AppendLine("- " + l);
            File.WriteAllText(Path.Combine(dir, "flick_pull_mapping.md"), md.ToString());

            Debug.Log($"[FlickPull] evidence written — fails={_asserts.Count(a => !a.pass)}");
        }
    }
}
#endif
