#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using Golfin.Diagnostics.Runtime;
using Golfin.Gameplay.UI.Controls;
using Golfin.Gameplay.UI.Controls.Pendulum;

namespace Golfin.EditorTools.ShotUI
{
    /// <summary>
    /// scheme_pendulum acceptance, driven through the PLAYER'S OWN ENTRY POINTS
    /// (PIPELINE_HARDENING §2): boot → PLAY → hole card → the in-game gear's real PENDULUM
    /// segment → a real IPointerDown/IDrag/IPointerUp gesture on the real PendulumHandle.
    ///
    /// <para>The gate is the JSON it writes (<c>pendulum_invariants.json</c>), not the pictures —
    /// PIPELINE_HARDENING §3. Every assertion is re-derived from LIVE state (a RectTransform's
    /// world corners, the shot controller's committed values, the pref the host actually applied)
    /// rather than from what this bot just asked for.</para>
    ///
    /// <para>Menu: GOLFIN ▸ ShotUI ▸ Verify Pendulum Scheme.</para>
    /// </summary>
    public static class PendulumSchemeVerify
    {
        const string ShellScenePath = "Assets/Scenes/ShellScene.unity";
        const string ArmedKey       = "PendulumSchemeVerify.Armed";
        public const string TaskDir  = "Docs/Specs/Active/scheme_pendulum";
        public static string ShotsDir => TaskDir + "/screenshots";

        [InitializeOnLoadMethod]
        static void Hook() => EditorApplication.playModeStateChanged += OnPlayModeChanged;

        [MenuItem("GOLFIN/ShotUI/Verify Pendulum Scheme")]
        public static void Launch()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("[PendulumE2E] already playing — stop first."); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ShellScenePath);
            Directory.CreateDirectory(ShotsDir);
            SessionState.SetBool(ArmedKey, true);
            EditorApplication.EnterPlaymode();
            Debug.Log("[PendulumE2E] armed — entering play mode.");
        }

        /// <summary>
        /// Pin the Game View to 1170x2532 — the standing capture resolution. Doing it BEFORE
        /// play mode does not survive: EnsureSelectedSizeAreValid re-picks on the play-mode
        /// window, and the run then measures Screen.height against a 1080-tall landscape view,
        /// which silently rescales every gesture this bot computes in screen px.
        /// </summary>
        public static void ForceCaptureResolution()
        {
            const BindingFlags NPI = BindingFlags.NonPublic | BindingFlags.Instance;
            try
            {
                var tSizes = Type.GetType("UnityEditor.GameViewSizes, UnityEditor");
                var inst   = typeof(ScriptableSingleton<>).MakeGenericType(tSizes)
                               .GetProperty("instance", BindingFlags.Public | BindingFlags.Static).GetValue(null);
                // currentGroup, NOT GetGroup(Standalone): the Game View shows the group for the
                // ACTIVE BUILD TARGET, which here is iOS. Pinning an index in the wrong group
                // "succeeds" and then selects whatever sits at that index in the group actually
                // on screen — which is how three runs logged "pinned to 1170x2532" while
                // rendering 1920x1080.
                var group  = tSizes.GetProperty("currentGroup",
                               BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic).GetValue(inst);
                var gt     = group.GetType();

                int total = (int)gt.GetMethod("GetTotalCount").Invoke(group, null);
                int idx = -1;
                for (int i = 0; i < total; i++)
                {
                    var gv = gt.GetMethod("GetGameViewSize").Invoke(group, new object[] { i });
                    if ((int)gv.GetType().GetProperty("width").GetValue(gv) == 1170 &&
                        (int)gv.GetType().GetProperty("height").GetValue(gv) == 2532) { idx = i; break; }
                }
                if (idx < 0)
                {
                    Debug.LogWarning("[PendulumE2E] no 1170x2532 entry in the active Game View group — " +
                                     "capturing at whatever is selected. Deliberately NOT adding a custom " +
                                     "size: the editor is shared and a run should leave no presets behind.");
                    return;
                }

                var gvType = Type.GetType("UnityEditor.GameView, UnityEditor");
                var chosen = gt.GetMethod("GetGameViewSize").Invoke(group, new object[] { idx });
                var cb     = gvType.GetMethod("SizeSelectionCallback", NPI | BindingFlags.Public);
                var cur    = gvType.GetProperty("currentGameViewSize", NPI | BindingFlags.Public);
                foreach (EditorWindow w in Resources.FindObjectsOfTypeAll(gvType))
                {
                    cb.Invoke(w, new object[] { idx, chosen });
                    w.Repaint();
                    // Read it BACK. "the setter was called" is not evidence the view moved.
                    var c = cur.GetValue(w);
                    Debug.Log("[PendulumE2E] Game View now "
                              + c.GetType().GetProperty("width").GetValue(c) + "x"
                              + c.GetType().GetProperty("height").GetValue(c) + " (index " + idx + ").");
                }
            }
            catch (Exception e) { Debug.LogWarning("[PendulumE2E] could not pin the Game View: " + e.Message); }
        }

        static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode) return;
            if (!SessionState.GetBool(ArmedKey, false)) return;
            SessionState.SetBool(ArmedKey, false);
            Application.runInBackground = true;   // MANDATORY for MCP-driven runs
            ForceCaptureResolution();
            var host = new GameObject("[PendulumVerifyBot]");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.AddComponent<PendulumVerifyRunner>();
        }
    }

    public class PendulumVerifyRunner : MonoBehaviour
    {
        const BindingFlags NP = BindingFlags.NonPublic | BindingFlags.Instance;

        readonly List<string> _log = new List<string>();
        readonly List<(string name, bool pass, string expected, string actual)> _inv =
            new List<(string, bool, string, string)>();

        void Note(string k, object v) { _log.Add($"{k}: {v}"); Debug.Log($"[PendulumE2E] {k}: {v}"); }

        void Assert(string name, bool pass, object expected, object actual)
        {
            _inv.Add((name, pass, Convert.ToString(expected, CultureInfo.InvariantCulture),
                                  Convert.ToString(actual,   CultureInfo.InvariantCulture)));
            Debug.Log($"[PendulumE2E] {(pass ? "PASS" : "FAIL")} {name}  expected={expected} actual={actual}");
        }

        /// <summary>Read an Image's tint back off the live object and compare to the node value.
        /// A colour claim in a report is worth nothing; the live component is the evidence.</summary>
        void AssertColor(string name, GameObject go, Color32 want)
        {
            var img = go != null ? go.GetComponent<Image>() : null;
            Color32 got = img != null ? (Color32)img.color : new Color32(0, 0, 0, 0);
            bool ok = img != null && got.r == want.r && got.g == want.g && got.b == want.b && got.a == want.a;
            Assert(name, ok, $"RGBA({want.r},{want.g},{want.b},{want.a})",
                   img != null ? $"RGBA({got.r},{got.g},{got.b},{got.a})" : "no Image");
        }

        void Near(string name, float expected, float actual, float tol)
            => Assert(name, Mathf.Abs(expected - actual) <= tol, expected.ToString("F2"), actual.ToString("F2"));

        void Start() => StartCoroutine(Sequence());

        // ── boot helpers (same shape as ShotTimingTelemetryVerify) ──────────────
        static Button FindButton(string n) => UnityEngine.Object
            .FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
            .FirstOrDefault(b => b.gameObject.name == n);

        static void ClickReal(Button b)
        {
            var ped = new PointerEventData(EventSystem.current);
            ExecuteEvents.Execute(b.gameObject, ped, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(b.gameObject, ped, ExecuteEvents.pointerUpHandler);
            b.onClick.Invoke();
        }

        IEnumerator ClickWhenPresent(string n, float timeout = 90f)
        {
            for (float t = 0f; t < timeout; t += 0.25f)
            {
                var b = FindButton(n);
                if (b != null) { ClickReal(b); yield break; }
                yield return new WaitForSecondsRealtime(0.25f);
            }
            Note("TIMEOUT", "waiting for button " + n);
        }

        static IEnumerable<MonoBehaviour> HoleCards() => UnityEngine.Object
            .FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
            .Where(m => m.GetType().Name == "HoleCardController");

        IEnumerator ClickHoleCard(int hole, float timeout = 30f)
        {
            for (float t = 0f; t < timeout; t += 0.25f)
            {
                foreach (var c in HoleCards())
                {
                    var p = c.GetType().GetProperty("HoleNumber");
                    if (p == null || (int)p.GetValue(c) != hole) continue;
                    if (c.GetType().GetField("actionButton", NP)?.GetValue(c) is Button btn)
                    { ClickReal(btn); yield break; }
                }
                yield return new WaitForSecondsRealtime(0.25f);
            }
            Note("TIMEOUT", "hole card " + hole);
        }

        static GameObject FindAny(string name)
        {
            foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
                if (t.name == name && t.gameObject.scene.IsValid()) return t.gameObject;
            return null;
        }

        // ── gesture ─────────────────────────────────────────────────────────────
        PendulumSchemeDriver _driver;
        GraphicRaycaster     _raycaster;
        Camera               _uiCam;
        PointerEventData     _ped;
        Vector2              _last;

        void Down(Vector2 p)
        {
            var rr = new RaycastResult { module = _raycaster, screenPosition = p };
            _ped = new PointerEventData(EventSystem.current)
            { position = p, pointerId = 0, button = PointerEventData.InputButton.Left };
            _ped.pointerPressRaycast = rr; _ped.pointerCurrentRaycast = rr;
            _last = p;
            ExecuteEvents.Execute(_driver.gameObject, _ped, ExecuteEvents.pointerDownHandler);
        }

        void Drag(Vector2 p)
        {
            _ped.delta = p - _last; _ped.position = p; _last = p;
            ExecuteEvents.Execute(_driver.gameObject, _ped, ExecuteEvents.dragHandler);
        }

        void Up() => ExecuteEvents.Execute(_driver.gameObject, _ped, ExecuteEvents.pointerUpHandler);

        // ── ShotController reflection (Golfin.Gameplay.Input is autoReferenced:false) ──
        Component    _sc;
        PropertyInfo _pState, _pPower, _pTiming01, _pMul, _pIsPutt, _pAcc, _pCC, _pForgive, _pConeDeg;

        bool BindShot()
        {
            _sc = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
                    FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                  .FirstOrDefault(m => m.GetType().Name == "ShotController");
            if (_sc == null) return false;
            var t = _sc.GetType();
            _pState   = t.GetProperty("State");
            _pPower   = t.GetProperty("PowerNormalized");
            _pTiming01= t.GetProperty("LastCommittedTiming01");
            _pMul     = t.GetProperty("LastTimingPowerMul");
            _pIsPutt  = t.GetProperty("IsPutt");
            _pAcc     = t.GetProperty("ClubAccuracyNorm01");
            _pCC      = t.GetProperty("CharacterClubControl");
            _pForgive = t.GetProperty("OverpowerForgiveness01");
            _pConeDeg = t.GetProperty("ConeHalfAngleDeg");
            return _pState != null && _pPower != null && _pAcc != null;
        }

        string StateName => _pState.GetValue(_sc).ToString();
        float  Power     => (float)_pPower.GetValue(_sc);
        float  Timing01  => (float)_pTiming01.GetValue(_sc);
        float  Mul       => (float)_pMul.GetValue(_sc);
        float  AccNorm   => (float)_pAcc.GetValue(_sc);

        int _rejects, _shots;

        // ── capture ─────────────────────────────────────────────────────────────
        readonly Dictionary<string, string> _md5 = new Dictionary<string, string>();

        string _lastSnap;

        /// <summary>Capture at END OF FRAME. CaptureCore's play-mode path is
        /// ScreenCapture.CaptureScreenshotAsTexture, which only has a composited backbuffer to
        /// read after the frame is done — called mid-Update it writes nothing and
        /// SnapPlayModeSafe still hands back the path it would have used.</summary>
        IEnumerator SnapAtEndOfFrame(string label)
        {
            yield return new WaitForEndOfFrame();
            _lastSnap = Snap(label);
        }

        string Snap(string label)
        {
            string p = CaptureCore.SnapPlayModeSafe(label);
            // SnapPlayModeSafe has twice returned a path for a file it never wrote, and twice
            // returned byte-identical STALE frames for two different states. Both are checked.
            if (string.IsNullOrEmpty(p) || !File.Exists(p)) { Note("CAPTURE_MISSING", label + " -> " + p); return null; }
            string h = Md5(p);
            foreach (var kv in _md5)
                if (kv.Value == h) Note("CAPTURE_STALE", $"{label} is byte-identical to {kv.Key}");
            _md5[label] = h;
            string dst = Path.Combine(PendulumSchemeVerify.ShotsDir, label + ".png");
            Directory.CreateDirectory(PendulumSchemeVerify.ShotsDir);
            File.Copy(p, dst, true);
            Note("capture", $"{label} -> {dst} ({new FileInfo(dst).Length} bytes)");
            return dst;
        }

        static string Md5(string path)
        {
            using (var md5 = System.Security.Cryptography.MD5.Create())
            using (var fs = File.OpenRead(path))
                return BitConverter.ToString(md5.ComputeHash(fs));
        }

        // ── geometry ────────────────────────────────────────────────────────────
        RectTransform _canvasRt;

        Rect CanvasRect(RectTransform rt)
        {
            var c = new Vector3[4]; rt.GetWorldCorners(c);
            Vector2 lo = _canvasRt.InverseTransformPoint(c[0]);
            Vector2 hi = _canvasRt.InverseTransformPoint(c[2]);
            return new Rect(lo.x, lo.y, hi.x - lo.x, hi.y - lo.y);
        }

        Rect Of(string name) => CanvasRect(FindAny(name).GetComponent<RectTransform>());

        IEnumerator Sequence()
        {
            // ── 1. boot through the real entry path ────────────────────────────
            yield return new WaitForSecondsRealtime(5f);
            yield return ClickWhenPresent("StartButton", 25f);
            yield return new WaitForSecondsRealtime(2.5f);
            yield return ClickWhenPresent("PlayButton");
            yield return new WaitForSecondsRealtime(2.5f);

            int hole = 0;
            foreach (int h in new[] { 2, 1, 10, 4 })
            {
                if (!HoleCards().Any(c => (int)(c.GetType().GetProperty("HoleNumber")?.GetValue(c) ?? -1) == h)) continue;
                hole = h; yield return ClickHoleCard(h); break;
            }
            if (hole == 0) { Fail("no hole card available"); yield break; }
            Note("hole", hole);

            for (float t = 0f; FindButton("HoleMap") == null && t < 120f; t += 0.5f)
                yield return new WaitForSecondsRealtime(0.5f);
            yield return new WaitForSecondsRealtime(4f);

            PendulumSchemeVerify.ForceCaptureResolution();
            yield return null; yield return null;
            Note("resolution", $"{Screen.width}x{Screen.height}");
            Assert("capture.resolution_is_1170x2532", Screen.width == 1170 && Screen.height == 2532,
                   "1170x2532", $"{Screen.width}x{Screen.height}");
            var rootCanvas = FindAny("ShotUI_Canvas");
            _canvasRt  = rootCanvas.GetComponent<RectTransform>();
            _raycaster = rootCanvas.GetComponent<Canvas>().rootCanvas.GetComponent<GraphicRaycaster>();
            _uiCam     = _raycaster.eventCamera;

            if (!BindShot()) { Fail("ShotController not reachable"); yield break; }

            ShotControllerRejectHook(true);

            // ── 2. pick Pendulum through the REAL in-game settings segment ─────
            yield return ClickWhenPresent("SettingsButton", 15f);
            yield return new WaitForSecondsRealtime(1.5f);
            var seg = FindButton("PendulumSegment") ?? FindButton("Pendulum")
                   ?? UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                      .FirstOrDefault(b => b.name.IndexOf("pendulum", StringComparison.OrdinalIgnoreCase) >= 0);
            if (seg != null) { ClickReal(seg); Note("scheme_entry_point", $"real widget onClick: {seg.name}"); }
            else
            {
                ControlSchemeService.Set(ControlScheme.Pendulum, "settings");
                Note("scheme_entry_point", "FALLBACK ControlSchemeService.Set — no 'Pendulum' Button found in the in-game modal");
            }
            yield return new WaitForSecondsRealtime(1f);
            var close = FindButton("CloseButton") ?? FindButton("ResumeButton") ?? FindButton("BackButton");
            if (close != null) ClickReal(close);
            yield return new WaitForSecondsRealtime(2f);

            var host = UnityEngine.Object.FindObjectsByType<ShotSchemeHost>(
                           FindObjectsInactive.Exclude, FindObjectsSortMode.None).FirstOrDefault();
            Assert("scheme.pref_is_pendulum", ControlSchemeService.Current == ControlScheme.Pendulum,
                   ControlScheme.Pendulum, ControlSchemeService.Current);
            Assert("scheme.host_active", host != null && host.ActiveScheme == ControlScheme.Pendulum,
                   ControlScheme.Pendulum, host != null ? host.ActiveScheme.ToString() : "no host");

            var flickRoot = FindAny("SchemeRoot_Flick");
            var pendRoot  = FindAny("SchemeRoot_Pendulum");
            Assert("scheme.pendulum_root_live", pendRoot != null && pendRoot.activeInHierarchy, true,
                   pendRoot != null && pendRoot.activeInHierarchy);
            Assert("scheme.flick_root_off", flickRoot != null && !flickRoot.activeInHierarchy, true,
                   flickRoot != null && !flickRoot.activeInHierarchy);

            _driver = UnityEngine.Object.FindObjectsByType<PendulumSchemeDriver>(
                          FindObjectsInactive.Exclude, FindObjectsSortMode.None).FirstOrDefault();
            if (_driver == null) { Fail("PendulumSchemeDriver not live"); yield break; }

            // Clone provenance, read back off the LIVE object rather than trusted from the report.
            var handleImg = FindAny("PendulumHandle").GetComponent<Image>();
            Assert("handle.sprite_is_a_real_club", handleImg.sprite != null,
                   "a Clubs/Controls sprite", handleImg.sprite != null ? handleImg.sprite.name : "<NONE>");
            Assert("handle.has_sprite_binder",
                   FindAny("PendulumHandle").GetComponent<Golfin.Gameplay.UI.ShotUI.ClubHandleSpriteBinder>() != null,
                   true, FindAny("PendulumHandle").GetComponent<Golfin.Gameplay.UI.ShotUI.ClubHandleSpriteBinder>() != null);
            Assert("handle.no_flick_dragger",
                   FindAny("PendulumHandle").GetComponent<Golfin.Gameplay.UI.ShotUI.ClubHandleDragger>() == null,
                   true, FindAny("PendulumHandle").GetComponent<Golfin.Gameplay.UI.ShotUI.ClubHandleDragger>() == null);

            yield return SnapAtEndOfFrame("pendulum_idle");

            // ── 3. geometry vs the Figma node ─────────────────────────────────
            Rect ball  = Of("CentralBall");
            Rect lane  = Of("PowerLane");
            Rect track = Of("PendulumTrack");
            Rect just  = Of("BandJust");
            Rect good  = Of("BandGood");
            Rect pip   = Of("CentrePip");
            float ballY = ball.y + ball.height * 0.5f;

            Near("geom.lane_width_120",       120f, lane.width, 0.5f);

            // The pill and its lines are DERIVED from one another now, so assert the relationship
            // rather than three literals that can drift apart: a tick marks where the club LANDS
            // at that power, and the lane ends below the club's bottom edge at full pull.
            Rect handleR = Of("PendulumHandle");
            float laneTop = lane.y + lane.height, laneBot = lane.y;
            float t100 = Of("Tick100").center.y, t120 = Of("Tick120").center.y;
            float restCentre = handleR.center.y;          // club centre at rest, below the ball
            Near("geom.lane_top_on_ball",      ballY, laneTop, 0.5f);
            Near("geom.tick100_marks_100pc_pull", restCentre - Pull100, t100, 0.5f);
            Near("geom.tick120_marks_120pc_pull", restCentre - Pull120, t120, 0.5f);
            float clubBottomAtFullPull = restCentre - Pull120 - handleR.height * 0.5f;
            Assert("geom.lane_contains_the_club_at_full_pull", clubBottomAtFullPull > laneBot,
                   $"club bottom > lane bottom ({laneBot - ballY:F0})",
                   (clubBottomAtFullPull - ballY).ToString("F0"));
            // And the lines sit LOW in the pill, near the node's 75% / 90%.
            float p100 = (laneTop - t100) / lane.height, p120 = (laneTop - t120) / lane.height;
            Note("tick_depth_pc", $"100pc at {p100 * 100f:F0} / 120pc at {p120 * 100f:F0} of the pill");
            Assert("geom.ticks_sit_low_in_the_pill", p100 > 0.65f && p120 > 0.80f,
                   ">65pc and >80pc down", $"{p100 * 100f:F0}pc / {p120 * 100f:F0}pc");

            Near("geom.track_width_720",      720f, track.width, 0.5f);
            Near("geom.track_height_44",      44f,  track.height, 0.5f);
            Near("geom.track_128_above_ball", ballY + 128f, track.center.y, 0.5f);
            Near("geom.pip_centred",          0f, pip.center.x, 0.5f);
            Near("geom.band_height_36",       36f, just.height, 0.5f);

            // COLOUR. Figma composites in sRGB, Unity blends in linear, so the node's alphas
            // rendered every translucent element too light. The bands are now pre-composited
            // opaque at the reference render's own pixels — which makes them exactly checkable.
            AssertColor("colour.band_good", FindAny("BandGood"), new Color32(196, 188, 138, 255));
            AssertColor("colour.band_just", FindAny("BandJust"), new Color32(175, 230, 170, 255));
            AssertColor("colour.centre_pip", FindAny("CentrePip"), new Color32(255, 59, 59, 255));

            // The bands are the WINDOWS, drawn — recomputed from the live club, not from a literal.
            float accNorm = AccNorm;
            Note("club_accuracy_norm01", accNorm.ToString("F3"));
            justWindow01 = _driver.JustWindow01;
            Near("bands.just_width_is_the_window", justWindow01 * 720f, just.width, 1.5f);
            Assert("bands.good_wider_than_just", good.width > just.width, "good > just",
                   $"{good.width:F0} vs {just.width:F0}");

            // ── 4. a real pull ────────────────────────────────────────────────
            Vector2 top  = ScreenAt(new Vector2(0f, ballY - 30f));
            _topProbe    = top;
            Vector2 hold = ScreenAt(new Vector2(0f, ballY - 30f - Pull100));   // exactly 100% of pull

            Down(top);
            yield return null;
            for (int i = 1; i <= 10; i++) { Drag(Vector2.Lerp(top, hold, i / 10f)); yield return null; }
            for (int i = 0; i < 5; i++)   { Drag(hold); yield return null; }

            Assert("pull.state_is_timing", StateName == "Timing", "Timing", StateName);
            Near("pull.power_at_100_tick", 1f, Power, 0.03f);

            // Cesar's first-clip note: "the horizontal ball is moving way too fast."
            Assert("marker.is_trackable_by_eye", _driver.MarkerHz <= 1.0f + 1e-3f,
                   "<= 1.0 Hz (a >= 1 s round trip)", _driver.MarkerHz.ToString("F3"));
            Note("marker_hz", _driver.MarkerHz.ToString("F3"));
            Note("marker_offset_live", _driver.MarkerOffset.ToString("F3"));
            yield return SnapAtEndOfFrame("pendulum_pull_and_timing");

            // The marker really moves, and it moves at the Hz the maths says.
            float m0 = _driver.MarkerOffset;
            yield return new WaitForSecondsRealtime(0.15f);
            Drag(hold);
            Assert("marker.is_moving", Mathf.Abs(_driver.MarkerOffset - m0) > 1e-3f,
                   "marker offset changes over time", $"{m0:F3} -> {_driver.MarkerOffset:F3}");
            Rect mk = Of("PendulumMarker");
            Near("marker.x_matches_offset", _driver.MarkerOffset * 360f, mk.center.x, 4f);

            // End the inspection swing without firing it: a release with no upward travel fails
            // the flick gate, which is the cheapest way back to Idle.
            float stepPx = Screen.height * 0.10f;
            Drag(hold); yield return null;
            Up(); yield return null;
            yield return WaitForIdle();
            yield return new WaitForSecondsRealtime(1f);

            // ── 4b. the target closes as the pull deepens ─────────────────────
            // Driven on the LIVE bar: down, ease to a lay-up, read the band, pull to 120%, read
            // it again. This is the drawn rect, not the formula that drew it.
            Down(_topProbe); yield return null;
            Drag(ScreenAt(new Vector2(0f, ballY - 30f - Pull100 * 0.22f)));  yield return null;
            float bandAtLayUp = Of("BandJust").width;
            float goodAtLayUp = Of("BandGood").width;
            Drag(ScreenAt(new Vector2(0f, ballY - 30f - Pull120))); yield return null;
            float bandAt120 = Of("BandJust").width;
            float goodAt120 = Of("BandGood").width;
            Note("band_just_px", $"layup {bandAtLayUp:F0} -> 120% {bandAt120:F0}");
            Assert("shrink.just_band_closes_as_the_pull_deepens", bandAt120 < bandAtLayUp - 1f,
                   $"< {bandAtLayUp:F0}px", bandAt120.ToString("F0"));
            Assert("shrink.good_band_closes_too", goodAt120 < goodAtLayUp - 1f,
                   $"< {goodAtLayUp:F0}px", goodAt120.ToString("F0"));
            Assert("shrink.good_stays_wider_than_just", goodAt120 > bandAt120,
                   "good > just at 120%", $"{goodAt120:F0} vs {bandAt120:F0}");
            Drag(ScreenAt(new Vector2(0f, ballY - 30f))); yield return null;
            Up(); yield return null;
            yield return WaitForIdle();
            yield return new WaitForSecondsRealtime(1f);

            // ── 5. flick on the pip → JUST ────────────────────────────────────
            int justShots = 0;
            for (int attempt = 1; attempt <= 4; attempt++)
            {
                yield return JustAttempt(top, hold, stepPx);
                justShots = _shots;
                Note($"just_attempt{attempt}", $"lead={_leadPhase:F4} committedMarker={_driver.LastCommittedMarker:F3} " +
                                               $"timing01={Timing01:F3} mul={Mul:F3}");
                // Stop on the last attempt as well as on success: the refit's 1.5 s settle would
                // outlive the grade pop (0.12 + 0.60 + 0.25 s), and the assertions below read it.
                if (Mathf.Abs(_driver.LastCommittedMarker) <= justWindow01 || attempt == 4) break;
                // Refit: the marker overshot centre by asin(m)/2pi sweeps, so lead by that much more.
                _leadPhase += Mathf.Asin(Mathf.Clamp(_driver.LastCommittedMarker, -1f, 1f)) / (2f * Mathf.PI);
                yield return WaitForIdle();
                yield return new WaitForSecondsRealtime(1.5f);
            }

            float mCommitted = _driver.LastCommittedMarker;
            Note("just_lead_phase", _leadPhase.ToString("F4"));
            Note("just_committed_marker", mCommitted.ToString("F3"));
            Note("just_grade_reached", _driver.LastCommittedGrade.ToString());
            Note("just_window01_for_this_club", justWindow01.ToString("F3"));
            Note("marker_travel_per_frame_at_centre",
                 (_driver.MarkerHz * Time.unscaledDeltaTime * 2f * Mathf.PI).ToString("F3"));

            Assert("flick.fired_a_shot", justShots >= 1, ">=1", justShots);
            Assert("flick.marker_was_latched_by_the_upswing", _driver.LastCommittedMarkerWasLatched,
                   true, _driver.LastCommittedMarkerWasLatched);
            Near("flick.peak_power_committed", 1f, _driver.LastCommittedPower, 0.03f);

            // THE invariant: whatever the marker was, the shot the pipeline committed is exactly
            // the one PendulumMath grades that marker as. Asserting "it was a JUST" would only be
            // asserting that this bot can hit a sub-frame target; this asserts the scheme.
            Near("flick.timing01_matches_the_grade", _driver.LastCommittedTiming01, Timing01, 0.005f);
            Near("flick.timing_mul_matches_the_grade", _driver.LastCommittedTimingMul, Mul, 1e-3f);
            Near("flick.timing01_is_one_minus_abs_marker", 1f - Mathf.Abs(mCommitted), Timing01, 0.005f);

            var popText = FindAny("GradeText")?.GetComponent<TextMeshProUGUI>();
            string wantKey = PendulumMath.GradeKey(_driver.LastCommittedGrade);
            Assert("flick.pop_reads_the_localised_key_for_the_grade_committed",
                   popText != null && popText.text == LocalizationManager.Get(wantKey),
                   LocalizationManager.Get(wantKey) + $" ({wantKey}; blanked to '<not shown>' before the swing)",
                   popText != null ? popText.text : "no pop");
            var handleGroup = FindAny("PendulumHandle").GetComponent<CanvasGroup>();
            Assert("handle.hidden_while_the_ball_is_in_flight",
                   handleGroup != null && handleGroup.alpha < 0.01f, "alpha 0",
                   handleGroup != null ? handleGroup.alpha.ToString("F2") : "no CanvasGroup");

            var popGroup = FindAny("PendulumGradePop").GetComponent<CanvasGroup>();
            Assert("flick.pop_is_visible", popGroup != null && popGroup.alpha > 0.5f, ">0.5",
                   popGroup != null ? popGroup.alpha.ToString("F2") : "no group");
            yield return SnapAtEndOfFrame("pendulum_result_just");

            yield return WaitForIdle();
            yield return new WaitForSecondsRealtime(1.5f);
            Assert("handle.returns_for_the_next_shot",
                   handleGroup != null && handleGroup.alpha > 0.99f, "alpha 1",
                   handleGroup != null ? handleGroup.alpha.ToString("F2") : "no CanvasGroup");

            // ── 6. an UNFORCED swing — the grade must match where the marker was ──
            Down(top);
            yield return null;
            for (int i = 1; i <= 10; i++) { Drag(Vector2.Lerp(top, hold, i / 10f)); yield return null; }
            for (int i = 0; i < 20; i++)  { Drag(hold); yield return null; }
            for (int i = 1; i <= 4; i++)  { Drag(new Vector2(hold.x, hold.y + stepPx * i)); yield return null; }
            // Read the marker AFTER the upswing has latched it: that frozen value is the one the
            // shot is graded on, so it is the one the assertion has to compare against.
            bool  latched  = _driver.MarkerLatched;
            Up();
            yield return null; yield return null;

            // The value the driver ACTUALLY graded on, latched at commit — not re-derived here.
            float mAtFlick = _driver.LastCommittedMarker;
            Assert("unforced.marker_latched", latched, true, latched);
            float expectedTiming01 = 1f - Mathf.Abs(mAtFlick);
            Note("unforced_marker_at_flick", mAtFlick.ToString("F3"));
            Note("unforced_committed_timing01", Timing01.ToString("F3"));
            Note("unforced_committed_mul", Mul.ToString("F3"));
            // Exact once the marker is latched — the tolerance only covers float printing.
            Assert("grade.timing01_is_one_minus_abs_m", Mathf.Abs(expectedTiming01 - Timing01) < 0.01f,
                   expectedTiming01.ToString("F3"), Timing01.ToString("F3"));
            yield return SnapAtEndOfFrame("pendulum_unforced_swing");

            yield return WaitForIdle();
            yield return new WaitForSecondsRealtime(1.5f);

            // ── 7. a SLOW release → toast, reset, no shot ──────────────────────
            int shotsBefore = _shots, rejBefore = _rejects;
            Down(top);
            yield return null;
            for (int i = 1; i <= 10; i++) { Drag(Vector2.Lerp(top, hold, i / 10f)); yield return null; }
            for (int i = 0; i < 5; i++)   { Drag(hold); yield return null; }
            // Lift by CREEPING upward one pixel a frame — a real slow release, not a teleport.
            for (int i = 1; i <= 6; i++)  { Drag(new Vector2(hold.x, hold.y + i)); yield return null; }
            Up();
            yield return null; yield return null;

            Assert("slow.no_shot", _shots == shotsBefore, shotsBefore, _shots);
            Assert("slow.toast_fired", _rejects == rejBefore + 1, rejBefore + 1, _rejects);
            Assert("slow.back_to_idle", StateName == "Idle", "Idle", StateName);
            yield return SnapAtEndOfFrame("pendulum_slow_release_rejected");

            // ── 8. leave it as we found it ────────────────────────────────────
            ShotControllerRejectHook(false);
            ControlSchemeService.Set(ControlScheme.Flick, "settings");
            PlayerPrefs.DeleteKey(ControlSchemeService.PrefKey);
            PlayerPrefs.Save();
            Note("cleanup", "pref reset to absent (reads as Flick)");

            Write();
            yield return new WaitForSecondsRealtime(1f);
            EditorApplication.ExitPlaymode();
        }

        /// <summary>
        /// One complete JUST attempt: a fresh pull to 100%, the marker started
        /// <see cref="_leadPhase"/> BEFORE centre, then a real up-flick.
        ///
        /// <para>The lead exists because the upswing latch lands a few frames after this bot
        /// decides to flick, and at ~1.8 Hz those frames are ~0.09 of a sweep — enough to turn a
        /// JUST into a MISS. A human leads by feel; a bot has to measure it, which is the same
        /// lead-and-refit <c>ShotTimingTelemetryVerify</c> does for the flick's arrow. What is NOT
        /// faked is the grade: the shot is still judged by the driver from wherever the marker
        /// actually was.</para>
        /// </summary>
        IEnumerator JustAttempt(Vector2 top, Vector2 hold, float stepPx)
        {
            FindAny("GradeText").GetComponent<TextMeshProUGUI>().text = "<not shown>";

            Down(top);
            yield return null;
            for (int i = 1; i <= 10; i++) { Drag(Vector2.Lerp(top, hold, i / 10f)); yield return null; }
            for (int i = 0; i < 3; i++)   { Drag(hold); yield return null; }

            _driver.SetPhaseForTests(-_leadPhase);
            Drag(hold);
            yield return null;
            for (int i = 1; i <= 4; i++) { Drag(new Vector2(hold.x, hold.y + stepPx * i)); yield return null; }
            Up();
            yield return null;
            yield return null;
        }

        float justWindow01;
        Vector2 _topProbe;

        /// <summary>The pull thresholds from the CONFIG the driver is actually using — never
        /// re-derived from the ticks, which is what the tick assertions are testing.</summary>
        float Pull100 => _driver.Pull100Px;
        float Pull120 => _driver.Pull120Px;


        /// <summary>Sweeps of marker travel between this bot deciding to flick and the upswing
        /// latch firing. Seeded from the first measured run and refit after every attempt.</summary>
        float _leadPhase = 0.09f;

        Vector2 ScreenAt(Vector2 canvasLocal)
            => RectTransformUtility.WorldToScreenPoint(_uiCam, _canvasRt.TransformPoint(canvasLocal));

        IEnumerator WaitForIdle(float timeout = 45f)
        {
            for (float t = 0f; t < timeout; t += 0.25f)
            {
                if (StateName == "Idle") yield break;
                yield return new WaitForSecondsRealtime(0.25f);
            }
            Note("TIMEOUT", "waiting for Idle");
        }

        // ShotController's FlickRejected / OnShotResolved are static / instance events on an
        // assembly this one cannot reference by type, so they are subscribed by reflection.
        Delegate _rejectHandler;
        void ShotControllerRejectHook(bool on)
        {
            var t  = _sc.GetType();
            var ev = t.GetEvent("FlickRejected", BindingFlags.Public | BindingFlags.Static);
            if (ev == null) { Note("hook", "FlickRejected not found"); return; }
            if (on)
            {
                _rejectHandler = Delegate.CreateDelegate(ev.EventHandlerType, this,
                                   GetType().GetMethod(nameof(OnReject), NP));
                ev.AddEventHandler(null, _rejectHandler);
                var res = t.GetEvent("OnShotResolved");
                if (res != null)
                    res.AddEventHandler(_sc, Delegate.CreateDelegate(res.EventHandlerType, this,
                                        GetType().GetMethod(nameof(OnShot), NP)));
            }
            else if (_rejectHandler != null) ev.RemoveEventHandler(null, _rejectHandler);
        }

        void OnReject(float speed) { _rejects++; Note("FlickRejected", speed.ToString("F2")); }
        void OnShot(object shotInput, object mods) { _shots++; }

        void Fail(string why)
        {
            Assert("RUN_COMPLETED", false, "the sequence reaches the end", "ABORT: " + why);
            Write();
            EditorApplication.ExitPlaymode();
        }

        void Write()
        {
            Directory.CreateDirectory(PendulumSchemeVerify.TaskDir);
            int fails = _inv.Count(i => !i.pass);

            var j = new StringBuilder();
            j.AppendLine("{");
            j.AppendLine($"  \"task\": \"scheme_pendulum\",");
            j.AppendLine($"  \"generated\": \"{DateTime.Now:yyyy-MM-dd HH:mm:ss}\",");
            j.AppendLine($"  \"resolution\": \"{Screen.width}x{Screen.height}\",");
            j.AppendLine($"  \"entry_path\": \"ShellScene -> StartButton -> PlayButton -> hole card -> in-game settings -> real PendulumHandle pointer events\",");
            j.AppendLine($"  \"total\": {_inv.Count}, \"passed\": {_inv.Count - fails}, \"failed\": {fails},");
            j.AppendLine("  \"assertions\": [");
            for (int i = 0; i < _inv.Count; i++)
            {
                var a = _inv[i];
                j.AppendLine($"    {{ \"name\": \"{a.name}\", \"result\": \"{(a.pass ? "PASS" : "FAIL")}\", " +
                             $"\"expected\": \"{Esc(a.expected)}\", \"actual\": \"{Esc(a.actual)}\" }}{(i < _inv.Count - 1 ? "," : "")}");
            }
            j.AppendLine("  ],");
            j.AppendLine("  \"notes\": [");
            for (int i = 0; i < _log.Count; i++)
                j.AppendLine($"    \"{Esc(_log[i])}\"{(i < _log.Count - 1 ? "," : "")}");
            j.AppendLine("  ]");
            j.AppendLine("}");

            string path = Path.Combine(PendulumSchemeVerify.TaskDir, "pendulum_invariants.json");
            File.WriteAllText(path, j.ToString());
            Debug.Log($"[PendulumE2E] {_inv.Count - fails}/{_inv.Count} PASS — {path}");
        }

        static string Esc(string s) => (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
#endif
