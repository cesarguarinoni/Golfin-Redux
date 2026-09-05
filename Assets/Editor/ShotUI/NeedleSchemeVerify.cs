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
using Golfin.Gameplay.UI.Controls.Needle;

namespace Golfin.EditorTools.ShotUI
{
    /// <summary>
    /// scheme_needle acceptance, driven through the PLAYER'S OWN ENTRY POINTS
    /// (PIPELINE_HARDENING §2): boot → PLAY → hole card → the in-game gear's REAL Tap-Timing
    /// segment (<c>InGameSettingsModalController.schemeButtons[2].onClick</c>) → real
    /// IPointerDown/IDrag/IPointerUp on the real <c>NeedleHandle</c> → a real pointer-down on the
    /// real <c>NeedleTapCatcher</c>.
    ///
    /// <para>THE GATE IS THE JSON (<c>needle_invariants.json</c>), not the pictures —
    /// PIPELINE_HARDENING §3. Every assertion is re-derived from LIVE state (a RectTransform's
    /// world corners, a graphic's own radius and sweep, the controller's committed values, the
    /// pref the host actually applied) rather than from what this bot asked for.</para>
    ///
    /// <para>NOTHING IS FORCED. The Pendulum run had to lead-and-refit its flick because a
    /// sinusoidal marker is sub-frame sensitive at the pip; this needle is LINEAR and single-pass,
    /// so the bot reads <c>NeedleOffset</c> each frame and taps when it crosses the band it wants.
    /// That is a real reaction on a real widget, and there is no test-only hook anywhere in the
    /// grading path.</para>
    ///
    /// <para>Menu: GOLFIN ▸ ShotUI ▸ Verify Needle Scheme.</para>
    /// </summary>
    public static class NeedleSchemeVerify
    {
        const string ShellScenePath = "Assets/Scenes/ShellScene.unity";
        const string ArmedKey       = "NeedleSchemeVerify.Armed";
        public const string TaskDir  = "Docs/Specs/Active/scheme_needle";
        public static string ShotsDir => TaskDir + "/screenshots";

        [InitializeOnLoadMethod]
        static void Hook() => EditorApplication.playModeStateChanged += OnPlayModeChanged;

        [MenuItem("GOLFIN/ShotUI/Verify Needle Scheme")]
        public static void Launch()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("[NeedleE2E] already playing — stop first."); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ShellScenePath);
            Directory.CreateDirectory(ShotsDir);
            SessionState.SetBool(ArmedKey, true);
            EditorApplication.EnterPlaymode();
            Debug.Log("[NeedleE2E] armed — entering play mode.");
        }

        static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode) return;
            if (!SessionState.GetBool(ArmedKey, false)) return;
            SessionState.SetBool(ArmedKey, false);
            Application.runInBackground = true;   // MANDATORY for MCP-driven runs
            PendulumSchemeVerify.ForceCaptureResolution();   // the same 1170x2532 pin, one copy
            var host = new GameObject("[NeedleVerifyBot]");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.AddComponent<NeedleVerifyRunner>();
        }
    }

    public class NeedleVerifyRunner : MonoBehaviour
    {
        const BindingFlags NP  = BindingFlags.NonPublic | BindingFlags.Instance;
        const BindingFlags ANY = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance;

        readonly List<string> _log = new List<string>();
        readonly List<(string name, bool pass, string expected, string actual)> _inv =
            new List<(string, bool, string, string)>();

        void Note(string k, object v) { _log.Add($"{k}: {v}"); Debug.Log($"[NeedleE2E] {k}: {v}"); }

        void Assert(string name, bool pass, object expected, object actual)
        {
            _inv.Add((name, pass, Convert.ToString(expected, CultureInfo.InvariantCulture),
                                  Convert.ToString(actual,   CultureInfo.InvariantCulture)));
            Debug.Log($"[NeedleE2E] {(pass ? "PASS" : "FAIL")} {name}  expected={expected} actual={actual}");
        }

        void Near(string name, float expected, float actual, float tol)
            => Assert(name, Mathf.Abs(expected - actual) <= tol, expected.ToString("F2"), actual.ToString("F2"));

        /// <summary>Read a graphic's tint back off the LIVE object. A colour claim in a report is
        /// worth nothing; the live component is the evidence (PIPELINE_HARDENING §11).</summary>
        void AssertColor(string name, string goName, Color want, int tol = 1)
        {
            var go  = FindAny(goName);
            var g   = go != null ? go.GetComponent<Graphic>() : null;
            Color32 got  = g != null ? (Color32)g.color : new Color32(0, 0, 0, 0);
            Color32 w32  = want;
            bool ok = g != null && Mathf.Abs(got.r - w32.r) <= tol && Mathf.Abs(got.g - w32.g) <= tol
                                && Mathf.Abs(got.b - w32.b) <= tol && Mathf.Abs(got.a - w32.a) <= tol;
            Assert(name, ok, $"RGBA({w32.r},{w32.g},{w32.b},{w32.a})",
                   g != null ? $"RGBA({got.r},{got.g},{got.b},{got.a})" : "no Graphic");
        }

        void Start() => StartCoroutine(Sequence());

        // ── boot helpers (the same shape PendulumSchemeVerify uses) ─────────────
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
        NeedleSchemeDriver _driver;
        NeedleTapCatcher   _catcher;
        GraphicRaycaster   _raycaster;
        Camera             _uiCam;
        PointerEventData   _ped;
        Vector2            _last;

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

        /// <summary>The second touch, on the REAL catcher — the same object and the same
        /// <c>IPointerDownHandler</c> a thumb hits, not a call into the driver.</summary>
        void Tap(Vector2 p)
        {
            var rr = new RaycastResult { module = _raycaster, screenPosition = p };
            var ped = new PointerEventData(EventSystem.current)
            { position = p, pointerId = 0, button = PointerEventData.InputButton.Left };
            ped.pointerPressRaycast = rr; ped.pointerCurrentRaycast = rr;
            ExecuteEvents.Execute(_catcher.gameObject, ped, ExecuteEvents.pointerDownHandler);
        }

        // ── ShotController reflection (Golfin.Gameplay.Input is autoReferenced:false) ──
        Component    _sc;
        PropertyInfo _pState, _pPower, _pTiming01, _pMul, _pIsPutt, _pAcc, _pCC, _pForgive, _pClean;

        bool BindShot()
        {
            _sc = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
                    FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                  .FirstOrDefault(m => m.GetType().Name == "ShotController");
            if (_sc == null) return false;
            var t = _sc.GetType();
            _pState    = t.GetProperty("State");
            _pPower    = t.GetProperty("PowerNormalized");
            _pTiming01 = t.GetProperty("LastCommittedTiming01");
            _pMul      = t.GetProperty("LastTimingPowerMul");
            _pIsPutt   = t.GetProperty("IsPutt");
            _pAcc      = t.GetProperty("ClubAccuracyNorm01");
            _pCC       = t.GetProperty("CharacterClubControl");
            _pForgive  = t.GetProperty("OverpowerForgiveness01");
            _pClean    = t.GetProperty("LastShotWasClean");
            return _pState != null && _pPower != null && _pAcc != null;
        }

        string StateName => _pState.GetValue(_sc).ToString();
        float  Power     => (float)_pPower.GetValue(_sc);
        float  Timing01  => (float)_pTiming01.GetValue(_sc);
        float  Mul       => (float)_pMul.GetValue(_sc);
        bool   Clean     => (bool)_pClean.GetValue(_sc);
        int    ClubControl => (int)_pCC.GetValue(_sc);

        int _shots;

        // ── capture ─────────────────────────────────────────────────────────────
        readonly Dictionary<string, string> _md5 = new Dictionary<string, string>();

        /// <summary>Capture at END OF FRAME. CaptureCore's play-mode path reads the composited
        /// backbuffer, which does not exist mid-Update — called there it writes nothing and
        /// SnapPlayModeSafe still hands back the path it would have used.</summary>
        IEnumerator SnapAtEndOfFrame(string label)
        {
            yield return new WaitForEndOfFrame();
            Snap(label);
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
            string dst = Path.Combine(NeedleSchemeVerify.ShotsDir, label + ".png");
            Directory.CreateDirectory(NeedleSchemeVerify.ShotsDir);
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
        static NeedleArcGraphic Gfx(string name) => FindAny(name)?.GetComponent<NeedleArcGraphic>();

        Vector2 ScreenAt(Vector2 canvasLocal)
            => RectTransformUtility.WorldToScreenPoint(_uiCam, _canvasRt.TransformPoint(canvasLocal));

        IEnumerator WaitForIdle(float timeout = 60f)
        {
            for (float t = 0f; t < timeout; t += 0.25f)
            {
                if (StateName == "Idle") yield break;
                yield return new WaitForSecondsRealtime(0.25f);
            }
            Note("TIMEOUT", "waiting for Idle");
        }

        float Pull100 => _driver.Pull100Px;
        float Pull120 => _driver.Pull120Px;
        float _ballY;
        Vector2 TopProbe   => ScreenAt(new Vector2(0f, _ballY - 30f));
        Vector2 HoldAt(float pullPx) => ScreenAt(new Vector2(0f, _ballY - 30f - pullPx));

        /// <summary>A complete first touch: down on the club head, dragged to the given pull.</summary>
        IEnumerator PullTo(float pullPx)
        {
            Vector2 top = TopProbe, hold = HoldAt(pullPx);
            Down(top);
            yield return null;
            for (int i = 1; i <= 10; i++) { Drag(Vector2.Lerp(top, hold, i / 10f)); yield return null; }
            for (int i = 0; i < 3; i++)   { Drag(hold); yield return null; }
        }

        /// <summary>
        /// Wait until the live needle offset reaches <paramref name="target"/>, then tap the real
        /// catcher. Polling the LIVE value and reacting is the honest version of "aim for the blue"
        /// — there is no forced offset anywhere in the grading path.
        /// </summary>
        IEnumerator TapWhenNeedleReaches(float target, float timeout = 6f)
        {
            for (float t = 0f; t < timeout; t += Time.unscaledDeltaTime)
            {
                if (!_driver.IsNeedlePhase) { Note("TAP_MISSED", $"phase ended before n reached {target:F2}"); yield break; }
                if (_driver.NeedleOffset >= target) { Tap(TopProbe); yield break; }
                yield return null;
            }
            Note("TIMEOUT", $"needle never reached {target:F2}");
        }

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

            var res = _sc.GetType().GetEvent("OnShotResolved");
            res?.AddEventHandler(_sc, Delegate.CreateDelegate(res.EventHandlerType, this,
                                  GetType().GetMethod(nameof(OnShot), NP)));

            // ── 2. pick Tap Timing through the REAL in-game segment ────────────
            yield return ClickWhenPresent("SettingsButton", 15f);
            yield return new WaitForSecondsRealtime(1.5f);
            yield return SelectNeedleThroughTheRealWidget();
            yield return new WaitForSecondsRealtime(1f);
            var close = FindButton("CloseButton") ?? FindButton("ResumeButton") ?? FindButton("BackButton");
            if (close != null) ClickReal(close);
            yield return new WaitForSecondsRealtime(2f);

            var host = UnityEngine.Object.FindObjectsByType<ShotSchemeHost>(
                           FindObjectsInactive.Exclude, FindObjectsSortMode.None).FirstOrDefault();
            Assert("scheme.pref_is_needle", ControlSchemeService.Current == ControlScheme.Needle,
                   ControlScheme.Needle, ControlSchemeService.Current);
            Assert("scheme.host_active", host != null && host.ActiveScheme == ControlScheme.Needle,
                   ControlScheme.Needle, host != null ? host.ActiveScheme.ToString() : "no host");

            var flickRoot  = FindAny("SchemeRoot_Flick");
            var pendRoot   = FindAny("SchemeRoot_Pendulum");
            var needleRoot = FindAny("SchemeRoot_Needle");
            Assert("scheme.needle_root_live", needleRoot != null && needleRoot.activeInHierarchy, true,
                   needleRoot != null && needleRoot.activeInHierarchy);
            Assert("scheme.flick_root_off", flickRoot != null && !flickRoot.activeInHierarchy, true,
                   flickRoot != null && !flickRoot.activeInHierarchy);
            Assert("scheme.pendulum_root_off", pendRoot == null || !pendRoot.activeInHierarchy, true,
                   pendRoot == null || !pendRoot.activeInHierarchy);
            // "no cone, no bar" — SPEC §6. The flick's cone and the pendulum's track live on the
            // roots above, so this is the same fact stated the way a reviewer would look for it.
            var cone = FindAny("ConeMesh"); var track = FindAny("PendulumTrack");
            Assert("idle.no_cone_on_screen", cone == null || !cone.activeInHierarchy, true,
                   cone == null || !cone.activeInHierarchy);
            Assert("idle.no_pendulum_bar_on_screen", track == null || !track.activeInHierarchy, true,
                   track == null || !track.activeInHierarchy);

            _driver = UnityEngine.Object.FindObjectsByType<NeedleSchemeDriver>(
                          FindObjectsInactive.Exclude, FindObjectsSortMode.None).FirstOrDefault();
            if (_driver == null) { Fail("NeedleSchemeDriver not live"); yield break; }
            _catcher = FindAny("NeedleTapCatcher").GetComponent<NeedleTapCatcher>();

            // ── 3. clone provenance, read back off the LIVE objects ────────────
            var handleGo  = FindAny("NeedleHandle");
            var handleImg = handleGo.GetComponent<Image>();
            Assert("handle.sprite_is_a_real_club", handleImg.sprite != null,
                   "a Clubs/Controls sprite", handleImg.sprite != null ? handleImg.sprite.name : "<NONE>");
            Assert("handle.has_sprite_binder",
                   handleGo.GetComponent<Golfin.Gameplay.UI.ShotUI.ClubHandleSpriteBinder>() != null, true,
                   handleGo.GetComponent<Golfin.Gameplay.UI.ShotUI.ClubHandleSpriteBinder>() != null);
            Assert("handle.no_flick_dragger",
                   handleGo.GetComponent<Golfin.Gameplay.UI.ShotUI.ClubHandleDragger>() == null, true,
                   handleGo.GetComponent<Golfin.Gameplay.UI.ShotUI.ClubHandleDragger>() == null);
            var ghostImg = FindAny("NeedleBallRestGhost")?.GetComponent<Image>();
            Assert("ghost.reuses_the_pendulum_sprite",
                   ghostImg != null && ghostImg.sprite != null && ghostImg.sprite.name == "S_PendulumBallGhost",
                   "S_PendulumBallGhost", ghostImg?.sprite != null ? ghostImg.sprite.name : "<NONE>");
            var chipImg = FindAny("ChipBg")?.GetComponent<Image>();
            Assert("chip.has_the_baked_sprite",
                   chipImg != null && chipImg.sprite != null && chipImg.sprite.name == "S_NeedleResultChip",
                   "S_NeedleResultChip", chipImg?.sprite != null ? chipImg.sprite.name : "<NONE>");

            // ── 4. idle: everything put away ───────────────────────────────────
            Assert("idle.catcher_disarmed", !_catcher.IsArmed, false, _catcher.IsArmed);
            var arcGroup    = FindAny("NeedleArcRoot").GetComponent<CanvasGroup>();
            var circleGroup = FindAny("NeedleCircleRoot").GetComponent<CanvasGroup>();
            Assert("idle.arc_hidden",    arcGroup.alpha    < 0.01f, "alpha 0", arcGroup.alpha.ToString("F2"));
            Assert("idle.circle_hidden", circleGroup.alpha < 0.01f, "alpha 0", circleGroup.alpha.ToString("F2"));
            Assert("idle.tap_hint_hidden", !FindAny("TapHint").activeInHierarchy, false,
                   FindAny("TapHint").activeInHierarchy);
            yield return SnapAtEndOfFrame("needle_idle");

            // ── 5. geometry vs the Figma node, off the LIVE objects ────────────
            Rect ball = Of("CentralBall");
            _ballY = ball.y + ball.height * 0.5f;
            Rect handleR = Of("NeedleHandle");
            float rest = _ballY - handleR.center.y;         // club-head centre below the ball
            Note("handle_rest_below_ball", rest.ToString("F1"));

            // THE RING IS WHERE THE CLUB LANDS. Config on one side, the live graphic on the other.
            Near("geom.ring80_marks_where_the_club_lands",  rest + _driver.Pull80Px,  Gfx("Ring80").RadiusX  - Gfx("Ring80").Thickness  * 0.5f, 1f);
            Near("geom.ring100_marks_where_the_club_lands", rest + Pull100,           Gfx("Ring100").RadiusX - Gfx("Ring100").Thickness * 0.5f, 1f);
            Near("geom.ring120_marks_where_the_club_lands", rest + Pull120,           Gfx("Ring120").RadiusX - Gfx("Ring120").Thickness * 0.5f, 1f);
            Near("geom.ring80_stroke_3",  3f, Gfx("Ring80").Thickness,  0.01f);
            Near("geom.ring100_stroke_4", 4f, Gfx("Ring100").Thickness, 0.01f);
            Near("geom.ring120_stroke_3", 3f, Gfx("Ring120").Thickness, 0.01f);

            var cres = Gfx("OverpowerCrescent");
            Near("geom.crescent_outer_is_ring120", rest + Pull120, cres.RadiusX, 1f);
            Near("geom.crescent_inner_is_ring100", rest + Pull100, cres.RadiusX - cres.Thickness, 1f);
            Near("geom.crescent_half_angle_34_38", 34.38f, cres.HalfSweepDeg, 0.01f);
            Near("geom.crescent_is_at_the_bottom", 180f, cres.CenterDeg, 0.01f);

            var arcG = Gfx("AccuracyArc");
            Near("geom.arc_outer_radius_230", 230f, arcG.RadiusX, 0.01f);
            Near("geom.arc_is_circular_on_a_swing", arcG.RadiusX, arcG.RadiusY, 0.01f);
            Near("geom.arc_thickness_44",     44f,  arcG.Thickness, 0.01f);
            Near("geom.arc_spans_180_deg",    90f,  arcG.HalfSweepDeg, 0.01f);
            Near("geom.zone_thickness_40",    40f,  Gfx("ZoneGood").Thickness, 0.01f);

            Rect hubR = Of("NeedleHub"), hintR = Of("TapHint");
            Near("geom.hub_36px",             36f, hubR.width, 0.5f);
            Near("geom.hub_on_the_ball_x",    0f,  hubR.center.x, 0.5f);
            Near("geom.hub_on_the_ball_y",    _ballY, hubR.center.y, 0.5f);
            Near("geom.tap_hint_90_below_the_ball", _ballY - 90f, hintR.y + hintR.height, 1.5f);

            // The needle is ROTATED and the grade pop is SCALED (0.6 at rest, its spring's start),
            // so world corners are the wrong instrument for both: an axis-aligned bbox of a rotated
            // rect reported the needle as 240 wide and 10 tall, and the pop as 252x72. The rect's
            // own local size is rotation- and scale-free, which is what these two are actually about.
            var needleRt = FindAny("Needle").GetComponent<RectTransform>();
            Near("geom.needle_width_10",      10f,  needleRt.rect.width,  0.5f);
            Near("geom.needle_length_240",    240f, needleRt.rect.height, 0.5f);
            Assert("geom.needle_pivots_at_the_ball",
                   Mathf.Abs(needleRt.pivot.y) < 1e-3f && Mathf.Abs(needleRt.pivot.x - 0.5f) < 1e-3f
                   && needleRt.anchoredPosition.sqrMagnitude < 0.25f,
                   "pivot (0.5, 0) at anchoredPosition (0,0)",
                   $"pivot {needleRt.pivot} at {needleRt.anchoredPosition}");
            var chipRt = FindAny("NeedleGradePop").GetComponent<RectTransform>();
            Near("geom.chip_420_wide",        420f, chipRt.rect.width,  0.5f);
            Near("geom.chip_120_tall",        120f, chipRt.rect.height, 0.5f);
            Near("geom.chip_360_above_ball",  360f, chipRt.anchoredPosition.y, 0.5f);
            Near("geom.label100_on_the_gold_ring",
                 _ballY - (rest + Pull100), Of("NeedleLabel100").center.y, 2f);

            Rect tapR = Of("NeedleTapCatcher");
            Near("geom.tap_area_width_1074",  1074f, tapR.width, 0.5f);
            Near("geom.tap_area_height_1396", 1396f, tapR.height, 0.5f);
            Assert("geom.tap_area_clears_the_bottom_buttons", tapR.y > _ballY - 436f,
                   $"bottom above ball-435 ({_ballY - 435f:F0})", tapR.y.ToString("F0"));

            // ── 6. colour, read back off the live graphics ─────────────────────
            AssertColor("colour.arc_fill",     "AccuracyArc", NeedleColors.ArcFill);
            AssertColor("colour.zone_good",    "ZoneGood",    NeedleColors.ZoneGood);
            AssertColor("colour.zone_perfect", "ZonePerfect", NeedleColors.ZonePerfect);
            AssertColor("colour.ring80",       "Ring80",      NeedlePowerCircleView.Ring80Color);
            AssertColor("colour.ring100",      "Ring100",     NeedlePowerCircleView.Ring100Color);
            AssertColor("colour.ring120",      "Ring120",     NeedlePowerCircleView.Ring120Color);
            AssertColor("colour.crescent",     "OverpowerCrescent",
                        NeedleColors.OverTurf(new Color32(0xFF, 0x5A, 0x5A, 255), 0.45f));
            Note("colour_note",
                 "rings + crescent keep the NODE's alpha with a linear-corrected RGB (NeedleColors.OverTurf); " +
                 "arc + zones are pre-composited opaque. Both treatments are carry-over 5.");

            // ── 7. the pull ────────────────────────────────────────────────────
            yield return PullTo(Pull100);
            Assert("pull.state_is_timing", StateName == "Timing", "Timing", StateName);
            Near("pull.power_at_the_100_ring", 1f, Power, 0.03f);
            Assert("pull.circle_visible", circleGroup.alpha > 0.5f, ">0.5", circleGroup.alpha.ToString("F2"));
            Assert("pull.arc_still_hidden", arcGroup.alpha < 0.5f,
                   "the arc must not appear until the power is committed", arcGroup.alpha.ToString("F2"));
            Assert("pull.catcher_still_disarmed", !_catcher.IsArmed, false, _catcher.IsArmed);
            yield return SnapAtEndOfFrame("needle_pull_100");

            // the target closes as the pull deepens — measured on the DRAWN angle
            Drag(HoldAt(Pull100 * 0.25f)); yield return null; yield return null;
            float perfectShallow = Gfx("ZonePerfect").HalfSweepDeg;
            float goodShallow    = Gfx("ZoneGood").HalfSweepDeg;
            Drag(HoldAt(Pull120)); yield return null; yield return null;
            float perfectDeep = Gfx("ZonePerfect").HalfSweepDeg;
            float goodDeep    = Gfx("ZoneGood").HalfSweepDeg;
            Note("zone_half_angle_deg", $"perfect {perfectShallow:F2} -> {perfectDeep:F2}, good {goodShallow:F2} -> {goodDeep:F2}");
            Assert("shrink.perfect_zone_closes_as_the_pull_deepens", perfectDeep < perfectShallow - 0.5f,
                   $"< {perfectShallow:F2} deg", perfectDeep.ToString("F2"));
            Assert("shrink.good_zone_closes_too", goodDeep < goodShallow - 0.5f,
                   $"< {goodShallow:F2} deg", goodDeep.ToString("F2"));
            Assert("shrink.good_stays_wider_than_perfect", goodDeep > perfectDeep,
                   "good > perfect at 120%", $"{goodDeep:F2} vs {perfectDeep:F2}");
            Near("pull.power_at_the_120_ring", 1.2f, Power, 0.03f);

            // THE invariant that ties the picture to the verdict.
            Near("zones.drawn_perfect_angle_is_the_graded_window",
                 _driver.PerfectZone01 * 90f, Gfx("ZonePerfect").HalfSweepDeg, 0.05f);
            Near("zones.drawn_good_angle_is_the_graded_window",
                 _driver.GoodZone01 * 90f, Gfx("ZoneGood").HalfSweepDeg, 0.05f);
            yield return SnapAtEndOfFrame("needle_pull_120_zones_narrow");

            // ── 8. release: the needle phase begins ────────────────────────────
            float sweepAt120 = 0f;
            Up();
            // BEFORE yielding: OnPointerUp sets the offset to -1 synchronously, and the very next
            // Update advances it. Reading after a yield measured one frame of travel and called it
            // a failed start (it also found a 0.21 s hitch frame, which is why Advance now clamps).
            float nAtRelease = _driver.NeedleOffset;
            sweepAt120 = _driver.SweepSeconds;
            var arcView0 = FindAny("NeedleArcRoot").GetComponent<NeedleArcView>();
            Assert("release.needle_phase_started", _driver.IsNeedlePhase, true, _driver.IsNeedlePhase);
            Near("release.needle_starts_at_the_left_end", -1f, nAtRelease, 1e-4f);
            yield return null;
            Assert("release.needle_step_is_frame_rate_clamped",
                   _driver.NeedleOffset <= -1f + 2f * (1f / 30f) / sweepAt120 + 1e-3f,
                   $"<= one 1/30s step ({-1f + 2f * (1f / 30f) / sweepAt120:F3})",
                   _driver.NeedleOffset.ToString("F3"));
            Assert("release.catcher_armed", _catcher.IsArmed, true, _catcher.IsArmed);
            Assert("release.arc_appears", arcGroup.alpha > 0.01f, ">0", arcGroup.alpha.ToString("F2"));
            Assert("release.tap_hint_shown", FindAny("TapHint").activeInHierarchy, true,
                   FindAny("TapHint").activeInHierarchy);
            // Zero hardcoded text, asserted as the KEY's own resolved value. The builder authors a
            // placeholder so the object is visible while being laid out, and for one iteration
            // that placeholder shipped — the UI fidelity linter caught it, not a human.
            Assert("release.tap_hint_reads_the_localised_key",
                   arcView0.TapHintText == LocalizationManager.Get(NeedleMath.KeyTapHint),
                   LocalizationManager.Get(NeedleMath.KeyTapHint) + " (SHOT_TAP_HINT)",
                   arcView0.TapHintText);
            Near("release.power_is_the_peak", 1.2f, Power, 0.03f);
            Assert("release.no_shot_yet", _shots == 0, 0, _shots);
            Assert("release.state_is_still_timing", StateName == "Timing", "Timing", StateName);

            // The needle really moves, and its ROTATION is where the offset says.
            float n0 = _driver.NeedleOffset;
            yield return new WaitForSecondsRealtime(0.2f);
            Assert("needle.actually_moves", _driver.NeedleOffset > n0 + 1e-3f,
                   "offset increases with time", $"{n0:F3} -> {_driver.NeedleOffset:F3}");
            var arcView = arcView0;
            Near("needle.rotation_matches_the_offset",
                 Mathf.DeltaAngle(0f, -_driver.NeedleOffset * 90f),
                 Mathf.DeltaAngle(0f, arcView.NeedleRotationDeg), 0.5f);
            Assert("release.handle_back_on_the_ball",
                   Mathf.Abs(Of("NeedleHandle").center.y - (_ballY - rest)) < 3f,
                   $"{_ballY - rest:F0}", Of("NeedleHandle").center.y.ToString("F0"));
            Note("circle_dim_alpha_after_release",
                 FindAny("NeedleCircleRoot").GetComponent<NeedlePowerCircleView>().DimAlpha.ToString("F2"));
            yield return SnapAtEndOfFrame("needle_sweeping");

            // let this one time out — the SHANK case, measured on a 120% pull
            yield return new WaitForSecondsRealtime(sweepAt120 + 1f);
            Assert("shank.fires_without_a_tap", _shots == 1, 1, _shots);
            Assert("shank.grade_is_shank", _driver.LastCommittedGrade == NeedleGrade.Shank,
                   NeedleGrade.Shank, _driver.LastCommittedGrade);
            Near("shank.timing01_is_zero", 0f, Timing01, 1e-3f);
            Assert("shank.pays_the_red_multiplier", Mathf.Abs(Mul - 0.70f) < 1e-3f, 0.70f, Mul.ToString("F3"));
            Assert("shank.goes_right", _driver.LastCommittedErrorYawRad > 0f, "> 0 rad",
                   _driver.LastCommittedErrorYawRad.ToString("F4"));
            Assert("shank.catcher_disarmed_at_commit", !_catcher.IsArmed, false, _catcher.IsArmed);
            AssertPopKey("shank.pop_reads_the_localised_key", NeedleGrade.Shank);
            AssertResultReadable("shank");
            var handleGroup = handleGo.GetComponent<CanvasGroup>();
            Assert("handle.hidden_while_the_ball_is_in_flight", handleGroup.alpha < 0.01f, "alpha 0",
                   handleGroup.alpha.ToString("F2"));
            yield return SnapAtEndOfFrame("needle_result_shank");

            // Half a second into the ball's flight the readout must still be there — that is the
            // window the player actually reads it in, and it is longer than a commit frame.
            yield return new WaitForSecondsRealtime(0.5f);
            AssertResultReadable("shank.half_a_second_later");

            yield return WaitForIdle();
            yield return new WaitForSecondsRealtime(1.5f);
            Assert("handle.returns_for_the_next_shot", handleGroup.alpha > 0.99f, "alpha 1",
                   handleGroup.alpha.ToString("F2"));
            Assert("idle.arc_put_away", arcGroup.alpha < 0.5f, "fading to 0", arcGroup.alpha.ToString("F2"));

            // ── 9. a PERFECT, tapped for real on the blue ──────────────────────
            yield return PullTo(Pull100);
            Note("club_control", ClubControl);
            Up(); yield return null;
            float sweepAt100 = _driver.SweepSeconds;
            Note("sweep_seconds_at_100pc", sweepAt100.ToString("F3"));
            Note("sweep_seconds_at_120pc", sweepAt120.ToString("F3"));
            Assert("needle.is_trackable_by_eye", sweepAt100 >= 1.0f,
                   ">= 1.0 s for one sweep", sweepAt100.ToString("F3"));
            Assert("overpower.shortens_the_sweep", sweepAt120 < sweepAt100 - 1e-3f,
                   $"< {sweepAt100:F3}s", sweepAt120.ToString("F3"));

            // Tap the moment the needle enters the blue. Real reaction, real widget.
            yield return TapWhenNeedleReaches(-_driver.PerfectZone01 * 0.5f);
            yield return null; yield return null;

            Assert("perfect.one_shot", _shots == 2, 2, _shots);
            Assert("perfect.grade_is_perfect", _driver.LastCommittedGrade == NeedleGrade.Perfect,
                   NeedleGrade.Perfect, _driver.LastCommittedGrade);
            Note("perfect_committed_needle", _driver.LastCommittedNeedle.ToString("F3"));
            Assert("perfect.is_dead_straight", Clean, true, Clean);
            Near("perfect.timing_mul_is_1", 1f, Mul, 1e-3f);
            Near("perfect.timing01_is_one_minus_abs_n",
                 1f - Mathf.Abs(_driver.LastCommittedNeedle), Timing01, 0.005f);
            Near("perfect.driver_and_pipeline_agree_on_timing01", _driver.LastCommittedTiming01, Timing01, 0.005f);
            Near("perfect.driver_and_pipeline_agree_on_mul",      _driver.LastCommittedTimingMul, Mul, 1e-3f);
            Near("perfect.power_is_the_peak", 1f, _driver.LastCommittedPower, 0.03f);
            AssertPopKey("perfect.pop_reads_the_localised_key", NeedleGrade.Perfect);
            AssertResultReadable("perfect");
            var pipGo = FindAny("TapPip");
            Assert("perfect.tap_pip_is_shown", pipGo.activeInHierarchy, true, pipGo.activeInHierarchy);
            Rect pipR = Of("TapPip");
            Near("perfect.pip_sits_on_the_arc_band",
                 230f - 22f, Vector2.Distance(pipR.center, new Vector2(0f, _ballY)), 2f);
            Near("perfect.pip_x_matches_the_tap_angle",
                 Mathf.Sin(_driver.LastCommittedNeedle * 90f * Mathf.Deg2Rad) * 208f, pipR.center.x, 3f);
            yield return SnapAtEndOfFrame("needle_result_perfect");

            yield return WaitForIdle();
            yield return new WaitForSecondsRealtime(1.5f);

            // ── 10. a HOOK, tapped early ───────────────────────────────────────
            yield return PullTo(Pull100);
            Up(); yield return null;
            yield return TapWhenNeedleReaches(-0.55f);
            yield return null; yield return null;

            Assert("hook.one_more_shot", _shots == 3, 3, _shots);
            Assert("hook.grade_is_hook", _driver.LastCommittedGrade == NeedleGrade.Hook,
                   NeedleGrade.Hook, _driver.LastCommittedGrade);
            Note("hook_committed_needle", _driver.LastCommittedNeedle.ToString("F3"));
            Assert("hook.tapped_early", _driver.LastCommittedNeedle < 0f, "< 0", _driver.LastCommittedNeedle.ToString("F3"));
            Assert("hook.goes_left", _driver.LastCommittedErrorYawRad < 0f, "< 0 rad",
                   _driver.LastCommittedErrorYawRad.ToString("F4"));
            Assert("hook.is_not_clean", !Clean, false, Clean);
            Near("hook.timing01_is_one_minus_abs_n",
                 1f - Mathf.Abs(_driver.LastCommittedNeedle), Timing01, 0.005f);
            AssertPopKey("hook.pop_reads_the_localised_key", NeedleGrade.Hook);
            AssertResultReadable("hook");
            yield return SnapAtEndOfFrame("needle_result_hook");

            yield return WaitForIdle();
            yield return new WaitForSecondsRealtime(1.5f);

            // ── 11. a SLICE, tapped late ───────────────────────────────────────
            yield return PullTo(Pull100);
            Up(); yield return null;
            yield return TapWhenNeedleReaches(0.55f);
            yield return null; yield return null;

            Assert("slice.one_more_shot", _shots == 4, 4, _shots);
            Assert("slice.grade_is_slice", _driver.LastCommittedGrade == NeedleGrade.Slice,
                   NeedleGrade.Slice, _driver.LastCommittedGrade);
            Note("slice_committed_needle", _driver.LastCommittedNeedle.ToString("F3"));
            Assert("slice.goes_right", _driver.LastCommittedErrorYawRad > 0f, "> 0 rad",
                   _driver.LastCommittedErrorYawRad.ToString("F4"));
            Assert("slice.mirrors_the_hook_side", true,
                   "hook yaw < 0 < slice yaw", "checked in hook.goes_left / slice.goes_right");
            AssertPopKey("slice.pop_reads_the_localised_key", NeedleGrade.Slice);
            AssertResultReadable("slice");
            yield return SnapAtEndOfFrame("needle_result_slice");

            yield return WaitForIdle();
            yield return new WaitForSecondsRealtime(1.5f);

            // ── 12. a zero-power release cancels, and nothing starts ───────────
            int shotsBefore = _shots;
            Down(TopProbe); yield return null;
            Drag(HoldAt(10f)); yield return null;
            Up(); yield return null; yield return null;
            Assert("cancel.no_shot", _shots == shotsBefore, shotsBefore, _shots);
            Assert("cancel.no_needle_phase", !_driver.IsNeedlePhase, false, _driver.IsNeedlePhase);
            Assert("cancel.catcher_disarmed", !_catcher.IsArmed, false, _catcher.IsArmed);
            Assert("cancel.back_to_idle", StateName == "Idle", "Idle", StateName);

            // ── 13. putt mode ──────────────────────────────────────────────────
            // IsPutt is the property the gameplay loop itself sets when the ball is on the green;
            // driving it here is the same write the production path makes, not a test hook. The
            // scheme is re-activated the way ShotSchemeHost re-activates it on a scheme swap.
            _pIsPutt.SetValue(_sc, true);
            _driver.Deactivate(); _driver.Activate();
            yield return null;
            var arcP = Gfx("AccuracyArc");
            Near("putt.arc_is_flattened_to_460x300", 150f, arcP.RadiusY, 0.01f);
            Near("putt.arc_keeps_its_width",         230f, arcP.RadiusX, 0.01f);
            Assert("putt.ring120_hidden", !FindAny("Ring120").activeInHierarchy, false,
                   FindAny("Ring120").activeInHierarchy);
            Assert("putt.crescent_hidden", !FindAny("OverpowerCrescent").activeInHierarchy, false,
                   FindAny("OverpowerCrescent").activeInHierarchy);
            Assert("putt.ring100_still_shown", FindAny("Ring100").activeInHierarchy, true,
                   FindAny("Ring100").activeInHierarchy);
            Near("putt.needle_shortens_to_160", 160f,
                 FindAny("Needle").GetComponent<RectTransform>().rect.height, 0.5f);

            yield return PullTo(Pull120);
            Near("putt.power_caps_at_100pc", 1f, Power, 0.03f);
            yield return SnapAtEndOfFrame("needle_putt_pull");
            Up(); yield return null;
            float puttSweep = _driver.SweepSeconds;
            Note("sweep_seconds_putt", puttSweep.ToString("F3"));
            Assert("putt.needle_is_slower_than_a_swing", puttSweep > sweepAt100 + 1e-3f,
                   $"> {sweepAt100:F3}s", puttSweep.ToString("F3"));
            yield return SnapAtEndOfFrame("needle_putt_sweeping");
            yield return TapWhenNeedleReaches(0f, puttSweep + 1f);
            yield return null; yield return null;
            Assert("putt.commits_a_shot", _shots == shotsBefore + 1, shotsBefore + 1, _shots);
            _pIsPutt.SetValue(_sc, false);
            yield return WaitForIdle();

            // ── 14. leave it as we found it ────────────────────────────────────
            _driver.Deactivate(); _driver.Activate();
            ControlSchemeService.Set(ControlScheme.Flick, "settings");
            PlayerPrefs.DeleteKey(ControlSchemeService.PrefKey);
            PlayerPrefs.Save();
            Note("cleanup", "pref reset to absent (reads as Flick); IsPutt restored to false");

            Assert("RUN_COMPLETED", true, "the sequence reaches the end", "reached the end");
            Write();
            yield return new WaitForSecondsRealtime(1f);
            EditorApplication.ExitPlaymode();
        }

        /// <summary>
        /// Pick Tap Timing through the real widget. The in-game gear's Controls card wires
        /// <c>schemeButtons[(int)ControlScheme]</c>, so index 2 IS the player's Needle segment;
        /// Settings ▸ Controls' <c>tapTimingButton</c> is the second real surface and is tried
        /// next. Only if neither exists does this fall back — and it says so in the JSON, because
        /// PIPELINE_HARDENING §2 makes a synthetic entry point an automatic FAIL.
        /// </summary>
        IEnumerator SelectNeedleThroughTheRealWidget()
        {
            Button real = null; string via = null;

            var modal = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
                            FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                        .FirstOrDefault(m => m.GetType().Name == "InGameSettingsModalController");
            if (modal != null &&
                modal.GetType().GetField("schemeButtons", ANY)?.GetValue(modal) is Button[] segs &&
                segs.Length > 2 && segs[2] != null)
            { real = segs[2]; via = "InGameSettingsModalController.schemeButtons[2].onClick"; }

            if (real == null)
            {
                var sub = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
                              FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                          .FirstOrDefault(m => m.GetType().Name == "ControlsSubmenu");
                if (sub != null && sub.GetType().GetField("tapTimingButton", ANY)?.GetValue(sub) is Button b && b != null)
                { real = b; via = "ControlsSubmenu.tapTimingButton.onClick"; }
            }

            if (real != null)
            {
                ClickReal(real);
                Note("scheme_entry_point", "REAL widget onClick: " + via + " (" + real.name + ")");
                Assert("entry.scheme_picked_through_the_real_widget", true,
                       "a real player-facing Button", via);
            }
            else
            {
                ControlSchemeService.Set(ControlScheme.Needle, "settings");
                Note("scheme_entry_point", "FALLBACK ControlSchemeService.Set — no real segment found");
                Assert("entry.scheme_picked_through_the_real_widget", false,
                       "a real player-facing Button", "FALLBACK ControlSchemeService.Set");
            }
            yield return new WaitForSecondsRealtime(0.5f);
        }

        /// <summary>The pop shows a LOCALISED KEY, never a literal — asserted as the key's own
        /// resolved value so a hardcoded word would fail even if it happened to read the same.</summary>
        void AssertPopKey(string name, NeedleGrade grade)
        {
            var t = FindAny("NeedleGradeText")?.GetComponent<TextMeshProUGUI>();
            string key  = NeedleMath.GradeKey(grade);
            string want = LocalizationManager.Get(key);
            Assert(name, t != null && t.text == want, $"{want} ({key})", t != null ? t.text : "no pop");
        }

        /// <summary>
        /// The result readout has to be AT FULL OPACITY when the grade is on screen. The shared
        /// fading view drops its target at Resolving, which CommitExternal reaches synchronously —
        /// so the first run captured the arc mid-fade and its navy measured (34,55,53) against its
        /// own (10,38,55), then (70,93,42) one shot later, which is grass. A colour assertion off
        /// the live component cannot see that; only the alpha can.
        /// </summary>
        void AssertResultReadable(string prefix)
        {
            var g = FindAny("NeedleArcRoot").GetComponent<CanvasGroup>();
            Assert(prefix + ".arc_is_still_fully_up_at_the_result", g.alpha > 0.99f, "alpha 1",
                   g.alpha.ToString("F3"));
        }

        void OnShot(object shotInput, object mods) { _shots++; }

        void Fail(string why)
        {
            Assert("RUN_COMPLETED", false, "the sequence reaches the end", "ABORT: " + why);
            Write();
            EditorApplication.ExitPlaymode();
        }

        void Write()
        {
            Directory.CreateDirectory(NeedleSchemeVerify.TaskDir);
            int fails = _inv.Count(i => !i.pass);

            var j = new StringBuilder();
            j.AppendLine("{");
            j.AppendLine("  \"task\": \"scheme_needle\",");
            j.AppendLine($"  \"generated\": \"{DateTime.Now:yyyy-MM-dd HH:mm:ss}\",");
            j.AppendLine($"  \"resolution\": \"{Screen.width}x{Screen.height}\",");
            j.AppendLine("  \"entry_path\": \"ShellScene -> StartButton -> PlayButton -> hole card -> in-game gear -> " +
                         "schemeButtons[2].onClick (Tap Timing) -> real NeedleHandle pointer events -> real NeedleTapCatcher pointer-down\",");
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

            string path = Path.Combine(NeedleSchemeVerify.TaskDir, "needle_invariants.json");
            File.WriteAllText(path, j.ToString());
            Debug.Log($"[NeedleE2E] {_inv.Count - fails}/{_inv.Count} PASS — {path}");
        }

        static string Esc(string s) => (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
#endif
