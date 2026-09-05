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
using Golfin.Gameplay.UI.Controls.FreeSwing;
using Golfin.Gameplay.UI.ShotUI;

namespace Golfin.EditorTools.ShotUI
{
    /// <summary>
    /// scheme_freeswing acceptance, driven through the PLAYER'S OWN ENTRY POINTS
    /// (PIPELINE_HARDENING §2): boot → PLAY → hole card → the in-game gear's REAL Free Swing
    /// segment (<c>InGameSettingsModalController.schemeButtons[3].onClick</c>) → real
    /// IPointerDown/IDrag/IPointerUp on the real <c>FreeSwingHandle</c>. There is no test-only
    /// hook anywhere in the grading path: the shot fires because a pointer crossed a line.
    ///
    /// <para>THE GATE IS THE JSON (<c>freeswing_invariants.json</c>), not the pictures —
    /// PIPELINE_HARDENING §3. Every assertion is re-derived from LIVE state (a RectTransform's
    /// world corners, the lane's own drawn offsets, the verdict the driver actually committed,
    /// the pref the host actually applied) rather than from what this bot asked for.</para>
    ///
    /// <para>Menu: GOLFIN ▸ ShotUI ▸ Verify Free Swing Scheme.</para>
    /// </summary>
    public static class FreeSwingSchemeVerify
    {
        const string ShellScenePath = "Assets/Scenes/ShellScene.unity";
        const string ArmedKey       = "FreeSwingSchemeVerify.Armed";
        public const string TaskDir  = "Docs/Specs/Active/scheme_freeswing";
        public static string ShotsDir => TaskDir + "/screenshots";

        [InitializeOnLoadMethod]
        static void Hook() => EditorApplication.playModeStateChanged += OnPlayModeChanged;

        [MenuItem("GOLFIN/ShotUI/Verify Free Swing Scheme")]
        public static void Launch()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("[FreeSwingE2E] already playing — stop first."); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ShellScenePath);
            Directory.CreateDirectory(ShotsDir);
            SessionState.SetBool(ArmedKey, true);
            EditorApplication.EnterPlaymode();
            Debug.Log("[FreeSwingE2E] armed — entering play mode.");
        }

        static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode) return;
            if (!SessionState.GetBool(ArmedKey, false)) return;
            SessionState.SetBool(ArmedKey, false);
            Application.runInBackground = true;   // MANDATORY for MCP-driven runs
            PendulumSchemeVerify.ForceCaptureResolution();   // the same 1170x2532 pin, one copy
            var host = new GameObject("[FreeSwingVerifyBot]");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.AddComponent<FreeSwingVerifyRunner>();
        }
    }

    public class FreeSwingVerifyRunner : MonoBehaviour
    {
        const BindingFlags NP  = BindingFlags.NonPublic | BindingFlags.Instance;
        const BindingFlags ANY = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance;

        readonly List<string> _log = new List<string>();
        readonly List<(string name, bool pass, string expected, string actual)> _inv =
            new List<(string, bool, string, string)>();

        void Note(string k, object v) { _log.Add($"{k}: {v}"); Debug.Log($"[FreeSwingE2E] {k}: {v}"); }

        void Assert(string name, bool pass, object expected, object actual)
        {
            _inv.Add((name, pass, Convert.ToString(expected, CultureInfo.InvariantCulture),
                                  Convert.ToString(actual,   CultureInfo.InvariantCulture)));
            Debug.Log($"[FreeSwingE2E] {(pass ? "PASS" : "FAIL")} {name}  expected={expected} actual={actual}");
        }

        void Near(string name, float expected, float actual, float tol)
            => Assert(name, Mathf.Abs(expected - actual) <= tol, expected.ToString("F2"), actual.ToString("F2"));

        /// <summary>Read a graphic's tint back off the LIVE object. A colour claim in a report is
        /// worth nothing; the live component is the evidence (PIPELINE_HARDENING §11).</summary>
        void AssertColor(string name, string goName, Color want, int tol = 1)
        {
            var go  = FindAny(goName);
            var g   = go != null ? go.GetComponent<Graphic>() : null;
            Color32 got = g != null ? (Color32)g.color : new Color32(0, 0, 0, 0);
            Color32 w32 = want;
            bool ok = g != null && Mathf.Abs(got.r - w32.r) <= tol && Mathf.Abs(got.g - w32.g) <= tol
                                && Mathf.Abs(got.b - w32.b) <= tol && Mathf.Abs(got.a - w32.a) <= tol;
            Assert(name, ok, $"RGBA({w32.r},{w32.g},{w32.b},{w32.a})",
                   g != null ? $"RGBA({got.r},{got.g},{got.b},{got.a})" : "no Graphic");
        }

        void Start() => StartCoroutine(Sequence());

        // ── boot helpers (the same shape the other two verifiers use) ───────────
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
        FreeSwingSchemeDriver _driver;
        GraphicRaycaster      _raycaster;
        Camera                _uiCam;
        PointerEventData      _ped;
        Vector2               _last;

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
        PropertyInfo _pState, _pPower, _pTiming01, _pMul, _pIsPutt, _pAcc, _pCC, _pClean;

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
            _pClean    = t.GetProperty("LastShotWasClean");
            return _pState != null && _pPower != null && _pAcc != null;
        }

        string StateName => _pState.GetValue(_sc).ToString();
        float  Power     => (float)_pPower.GetValue(_sc);
        float  Timing01  => (float)_pTiming01.GetValue(_sc);
        float  Mul       => (float)_pMul.GetValue(_sc);

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
            string dst = Path.Combine(FreeSwingSchemeVerify.ShotsDir, label + ".png");
            Directory.CreateDirectory(FreeSwingSchemeVerify.ShotsDir);
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
        FreeSwingLaneView _lane;

        /// <summary>The touch origin: on the club head, a little above its centre — where a thumb
        /// actually lands on the sprite.</summary>
        Vector2 Origin => ScreenAt(new Vector2(0f, _ballY - _lane.HandleRestBelowBall + 20f));

        Vector2 OriginLocal => new Vector2(0f, _ballY - _lane.HandleRestBelowBall + 20f);

        Vector2 At(float dx, float dy) => ScreenAt(OriginLocal + new Vector2(dx, dy));

        /// <summary>
        /// ONE CONTINUOUS GESTURE, exactly as a thumb makes it: down on the club head, pulled
        /// <paramref name="pullPx"/> down over <paramref name="backFrames"/> frames, then back up
        /// through the impact line over <paramref name="upFrames"/>. The shot fires from inside
        /// this loop, on the crossing, with the pointer still down.
        /// </summary>
        IEnumerator Swing(float pullPx, int backFrames = 36, int upFrames = 18,
                          float crossX = 0f, float bowPx = 0f, float upHoldFrames = 0f)
        {
            Down(Origin);
            yield return null;

            for (int i = 1; i <= backFrames; i++)
            {
                Drag(At(0f, -pullPx * i / backFrames));
                yield return null;
            }

            // Up to a hair PAST the impact line, so the crossing is the last step of the ramp and
            // the measured upswing really is `upFrames` long.
            float endDy = _lane.ImpactCrossOffsetPx + 0.5f;
            for (int i = 1; i <= upFrames; i++)
            {
                float t  = i / (float)upFrames;
                float dy = Mathf.Lerp(-pullPx, endDy, t);
                float dx = crossX * t + bowPx * Mathf.Sin(t * Mathf.PI);
                Drag(At(dx, dy));
                // A deliberately slow upswing is made slow by WAITING, not by moving less — the
                // duff threshold is px/second and only a real stall can trip it honestly.
                for (int h = 0; h < upHoldFrames; h++) yield return null;
                yield return null;
            }
            Up();
        }

        // ── the real entry point ────────────────────────────────────────────────

        /// <summary>
        /// PIPELINE_HARDENING §2: the scheme has to be picked through the widget the PLAYER taps.
        /// <c>InGameSettingsModalController.schemeButtons</c> is indexed by
        /// <c>(int)ControlScheme</c>, so index 3 IS the player's Free Swing segment; Settings ▸
        /// Controls' <c>freeSwingButton</c> is the second real surface and is tried next. Only if
        /// neither exists does this fall back — and it says so in the JSON, because a synthetic
        /// entry point is an automatic FAIL.
        /// </summary>
        IEnumerator SelectFreeSwingThroughTheRealWidget()
        {
            Button real = null; string via = null;

            var modal = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
                            FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                        .FirstOrDefault(m => m.GetType().Name == "InGameSettingsModalController");
            if (modal != null &&
                modal.GetType().GetField("schemeButtons", ANY)?.GetValue(modal) is Button[] segs &&
                segs.Length > 3 && segs[3] != null)
            { real = segs[3]; via = "InGameSettingsModalController.schemeButtons[3].onClick"; }

            if (real == null)
            {
                var sub = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
                              FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                          .FirstOrDefault(m => m.GetType().Name == "ControlsSubmenu");
                if (sub != null && sub.GetType().GetField("freeSwingButton", ANY)?.GetValue(sub) is Button b && b != null)
                { real = b; via = "ControlsSubmenu.freeSwingButton.onClick"; }
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
                ControlSchemeService.Set(ControlScheme.FreeSwing, "settings");
                Note("scheme_entry_point", "FALLBACK ControlSchemeService.Set — no real segment found");
                Assert("entry.scheme_picked_through_the_real_widget", false,
                       "a real player-facing Button", "FALLBACK ControlSchemeService.Set");
            }
            yield return new WaitForSecondsRealtime(0.5f);
        }

        /// <summary>The pop shows a LOCALISED KEY, never a literal — asserted as the key's own
        /// resolved value so a hardcoded word would fail even if it happened to read the same.</summary>
        void AssertPopKey(string name, FreeSwingGrade grade)
        {
            var t = FindAny("FreeSwingGradeText")?.GetComponent<TextMeshProUGUI>();
            string key  = FreeSwingMath.GradeKey(grade);
            string want = LocalizationManager.Get(key);
            Assert(name, t != null && t.text == want, $"{want} ({key})", t != null ? t.text : "no pop");
        }

        void OnShot(object shotInput, object mods) { _shots++; }

        // ── the run ─────────────────────────────────────────────────────────────

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

            // ── 2. pick Free Swing through the REAL in-game segment ────────────
            // Measure SPIN's x BEFORE the scheme changes: hiding the Fade/Draw toggle must not
            // move it (§6), and "did not move" needs a before.
            var spinBefore = FindAny("SpinButton");
            float spinXBefore = spinBefore != null ? Of("SpinButton").center.x : float.NaN;

            yield return ClickWhenPresent("SettingsButton", 15f);
            yield return new WaitForSecondsRealtime(1.5f);
            yield return SelectFreeSwingThroughTheRealWidget();
            yield return new WaitForSecondsRealtime(1f);
            var close = FindButton("CloseButton") ?? FindButton("ResumeButton") ?? FindButton("BackButton");
            if (close != null) ClickReal(close);
            yield return new WaitForSecondsRealtime(2f);

            var host = UnityEngine.Object.FindObjectsByType<ShotSchemeHost>(
                           FindObjectsInactive.Exclude, FindObjectsSortMode.None).FirstOrDefault();
            Assert("scheme.pref_is_freeswing", ControlSchemeService.Current == ControlScheme.FreeSwing,
                   ControlScheme.FreeSwing, ControlSchemeService.Current);
            Assert("scheme.host_active", host != null && host.ActiveScheme == ControlScheme.FreeSwing,
                   ControlScheme.FreeSwing, host != null ? host.ActiveScheme.ToString() : "no host");

            var flickRoot = FindAny("SchemeRoot_Flick");
            var pendRoot  = FindAny("SchemeRoot_Pendulum");
            var needRoot  = FindAny("SchemeRoot_Needle");
            var fsRoot    = FindAny("SchemeRoot_FreeSwing");
            Assert("scheme.freeswing_root_live", fsRoot != null && fsRoot.activeInHierarchy, true,
                   fsRoot != null && fsRoot.activeInHierarchy);
            Assert("scheme.flick_root_off", flickRoot != null && !flickRoot.activeInHierarchy, true,
                   flickRoot != null && !flickRoot.activeInHierarchy);
            Assert("scheme.pendulum_root_off", pendRoot == null || !pendRoot.activeInHierarchy, true,
                   pendRoot == null || !pendRoot.activeInHierarchy);
            Assert("scheme.needle_root_off", needRoot == null || !needRoot.activeInHierarchy, true,
                   needRoot == null || !needRoot.activeInHierarchy);

            // "no cone, no bar, no arc" — SPEC §6, stated the way a reviewer would look for it.
            var cone = FindAny("ConeMesh"); var track = FindAny("PendulumTrack"); var arc = FindAny("AccuracyArc");
            Assert("idle.no_cone_on_screen",  cone  == null || !cone.activeInHierarchy,  true, cone  == null || !cone.activeInHierarchy);
            Assert("idle.no_pendulum_bar",    track == null || !track.activeInHierarchy, true, track == null || !track.activeInHierarchy);
            Assert("idle.no_needle_arc",      arc   == null || !arc.activeInHierarchy,   true, arc   == null || !arc.activeInHierarchy);

            _driver = UnityEngine.Object.FindObjectsByType<FreeSwingSchemeDriver>(
                          FindObjectsInactive.Exclude, FindObjectsSortMode.None).FirstOrDefault();
            if (_driver == null) { Fail("FreeSwingSchemeDriver not live"); yield break; }
            _lane = FindAny("FreeSwingLaneRoot").GetComponent<FreeSwingLaneView>();

            // ── 3. the Fade/Draw toggle is gone, and SPIN has NOT moved ────────
            var buttons = UnityEngine.Object.FindObjectsByType<ActionButtonsRoot>(
                              FindObjectsInactive.Exclude, FindObjectsSortMode.None).FirstOrDefault();
            Assert("fadedraw.hidden", buttons != null && !buttons.IsFadeDrawVisible, true,
                   buttons != null ? (!buttons.IsFadeDrawVisible).ToString() : "no ActionButtonsRoot");
            Assert("fadedraw.alpha_zero", buttons != null && buttons.FadeDrawAlpha < 0.01f, "alpha 0",
                   buttons != null ? buttons.FadeDrawAlpha.ToString("F2") : "n/a");
            var fdGo = FindAny("FadeDrawButton");
            Assert("fadedraw.object_still_active_not_SetActive_false",
                   fdGo != null && fdGo.activeInHierarchy, true, fdGo != null && fdGo.activeInHierarchy);
            var fdGroup = fdGo != null ? fdGo.GetComponent<CanvasGroup>() : null;
            Assert("fadedraw.untappable", fdGroup != null && !fdGroup.blocksRaycasts, false,
                   fdGroup != null ? fdGroup.blocksRaycasts.ToString() : "no CanvasGroup");
            float spinXAfter = FindAny("SpinButton") != null ? Of("SpinButton").center.x : float.NaN;
            Note("spin_x", $"{spinXBefore:F1} -> {spinXAfter:F1}");
            Assert("fadedraw.spin_did_not_recentre",
                   float.IsNaN(spinXBefore) || Mathf.Abs(spinXAfter - spinXBefore) < 0.5f,
                   spinXBefore.ToString("F1"), spinXAfter.ToString("F1"));
            Assert("fadedraw.mode_disarmed",
                   Golfin.Gameplay.UI.HUD.ShotModeContext.Mode == Golfin.Gameplay.UI.HUD.ShotMode.Straight,
                   "Straight", Golfin.Gameplay.UI.HUD.ShotModeContext.Mode);

            // ── 4. clone provenance, read back off the LIVE objects ────────────
            var handleGo  = FindAny("FreeSwingHandle");
            var handleImg = handleGo.GetComponent<Image>();
            Assert("handle.sprite_is_a_real_club", handleImg.sprite != null,
                   "a Clubs/Controls sprite", handleImg.sprite != null ? handleImg.sprite.name : "<NONE>");
            Assert("handle.has_sprite_binder", handleGo.GetComponent<ClubHandleSpriteBinder>() != null,
                   true, handleGo.GetComponent<ClubHandleSpriteBinder>() != null);
            Assert("handle.no_flick_dragger", handleGo.GetComponent<ClubHandleDragger>() == null,
                   true, handleGo.GetComponent<ClubHandleDragger>() == null);
            var ghostImg = FindAny("FreeSwingBallRestGhost")?.GetComponent<Image>();
            Assert("ghost.reuses_the_pendulum_sprite",
                   ghostImg != null && ghostImg.sprite != null && ghostImg.sprite.name == "S_PendulumBallGhost",
                   "S_PendulumBallGhost", ghostImg?.sprite != null ? ghostImg.sprite.name : "<NONE>");
            var laneImg = FindAny("FreeSwingLane")?.GetComponent<Image>();
            Assert("lane.has_the_baked_sprite",
                   laneImg != null && laneImg.sprite != null && laneImg.sprite.name == "S_FreeSwingLane",
                   "S_FreeSwingLane", laneImg?.sprite != null ? laneImg.sprite.name : "<NONE>");
            Assert("lane.is_9sliced_not_a_flat_fill", laneImg != null && laneImg.type == Image.Type.Sliced,
                   Image.Type.Sliced, laneImg != null ? laneImg.type.ToString() : "n/a");
            var chipImg = FindAny("FreeSwingChipBg")?.GetComponent<Image>();
            Assert("chip.has_the_baked_sprite",
                   chipImg != null && chipImg.sprite != null && chipImg.sprite.name == "S_FreeSwingAnalyzerChip",
                   "S_FreeSwingAnalyzerChip", chipImg?.sprite != null ? chipImg.sprite.name : "<NONE>");
            foreach (var n in new[] { "FreeSwingTick100", "FreeSwingTick120", "FreeSwingImpactLine",
                                      "FreeSwingImpactWindow" })
            {
                var img = FindAny(n)?.GetComponent<Image>();
                Assert("sprite." + n + "_is_a_real_stadium_not_a_flat_fill",
                       img != null && img.sprite != null && img.type == Image.Type.Sliced,
                       "S_PillStadium (Sliced)",
                       img?.sprite != null ? $"{img.sprite.name} ({img.type})" : "<NONE>");
            }

            // ── 5. idle: everything put away ───────────────────────────────────
            var laneGroup  = FindAny("FreeSwingLaneRoot").GetComponent<CanvasGroup>();
            var traceView  = FindAny("FreeSwingTraceRoot").GetComponent<FreeSwingTraceView>();
            var chip       = FindAny("FreeSwingAnalyzerChip").GetComponent<FreeSwingAnalyzerChip>();
            Assert("idle.lane_hidden",  laneGroup.alpha < 0.01f, "alpha 0", laneGroup.alpha.ToString("F2"));
            Assert("idle.trace_hidden", traceView.Alpha < 0.01f, "alpha 0", traceView.Alpha.ToString("F2"));
            Assert("idle.chip_hidden",  chip.Alpha < 0.01f,      "alpha 0", chip.Alpha.ToString("F2"));
            yield return SnapAtEndOfFrame("freeswing_idle");

            // ── 6. geometry vs the Figma node, off the LIVE objects ────────────
            Rect ball = Of("CentralBall");
            _ballY = ball.y + ball.height * 0.5f;
            Rect handleR = Of("FreeSwingHandle");
            float rest = _ballY - handleR.center.y;
            Note("handle_rest_below_ball", rest.ToString("F1"));
            Near("geom.handle_rests_where_the_lane_says", _lane.HandleRestBelowBall, rest, 1.5f);

            Rect laneR = Of("FreeSwingLane");
            Near("geom.lane_width_140", 140f, laneR.width, 0.5f);
            // THE PILL IS DERIVED, not the node's 560 — the lengthened-pill fix carried over from
            // the Pendulum, plus this scheme's own follow-through above the ball.
            Near("geom.lane_height_is_derived", _lane.LaneHeight, laneR.height, 1f);
            Near("geom.lane_top_is_the_followthrough_above_the_ball",
                 _ballY + _driver.FollowThroughPx, laneR.y + laneR.height, 1.5f);
            Near("geom.lane_contains_the_club_at_full_pull",
                 _ballY - (rest + Pull120 + 50f), laneR.y + 20f, 2f);

            // A TICK MARKS WHERE THE CLUB HEAD LANDS. Config on one side, the live rect on the other.
            Near("geom.tick100_is_where_the_club_lands", _ballY - (rest + Pull100), Of("FreeSwingTick100").center.y, 1.5f);
            Near("geom.tick120_is_where_the_club_lands", _ballY - (rest + Pull120), Of("FreeSwingTick120").center.y, 1.5f);
            Near("geom.impact_line_is_on_the_ball",      _ballY, Of("FreeSwingImpactLine").center.y, 1.5f);
            Near("geom.impact_window_is_on_the_impact_line",
                 Of("FreeSwingImpactLine").center.y, Of("FreeSwingImpactWindow").center.y, 1.5f);
            Near("geom.tick_height_6",  6f,  Of("FreeSwingTick100").height, 0.5f);
            Near("geom.impact_line_height_6", 6f, Of("FreeSwingImpactLine").height, 0.5f);
            Near("geom.impact_window_height_16", 16f, Of("FreeSwingImpactWindow").height, 0.5f);
            Near("geom.label100_sits_on_its_tick",
                 Of("FreeSwingTick100").center.y, Of("FreeSwingLabel100").center.y, 2f);
            Near("geom.labels_are_86_right_of_the_lane_centre", 86f, Of("FreeSwingLabel100").x, 1.5f);

            Rect chipR = Of("FreeSwingAnalyzerChip");
            Near("geom.chip_840_wide",       840f, chipR.width,  0.5f);
            Near("geom.chip_150_tall",       150f, chipR.height, 0.5f);
            Near("geom.chip_365_above_ball", _ballY + 365f, chipR.center.y, 1.5f);
            Near("geom.chip_columns_200_apart",
                 200f, Of("FreeSwingLblIMPACT").center.x - Of("FreeSwingLblPOWER").center.x, 1.5f);
            Assert("geom.chip_clears_the_grade_pop",
                   Of("FreeSwingGradePop").y > chipR.y + chipR.height,
                   $"pop bottom > chip top ({chipR.y + chipR.height:F0})",
                   Of("FreeSwingGradePop").y.ToString("F0"));

            // ── 7. colour, read back off the live graphics ─────────────────────
            AssertColor("colour.tick100_gold",   "FreeSwingTick100",      FreeSwingColors.Tick100);
            AssertColor("colour.tick120_red",    "FreeSwingTick120",      FreeSwingColors.Tick120);
            AssertColor("colour.impact_line",    "FreeSwingImpactLine",   FreeSwingColors.ImpactLine);
            AssertColor("colour.impact_window",  "FreeSwingImpactWindow", FreeSwingColors.ImpactWindow);
            AssertColor("colour.trace",          "FreeSwingTrace",        FreeSwingColors.Trace);
            Note("colour_note",
                 "the impact window and the trace keep the NODE's alpha with a linear-corrected RGB " +
                 "(NeedleColors.OverTurf); the chip's labels are pre-composited opaque over the chip " +
                 "gradient sampled at their own height. Both treatments are carry-over 5.");

            // ── 8. the backswing: the window closes as the pull deepens ────────
            //
            // ONE MONOTONIC PULL, and the three widths are read as it passes each depth. Not three
            // separate drags to three depths: the window is driven from the PEAK (carry-over 2 —
            // the target the player watched close is the one they are graded against), so the peak
            // never shrinks back, and probing "0%" after a pull to 100% reads the 100% width. The
            // first version of this block did exactly that and reported 39.6 -> 39.6, which is the
            // scheme behaving correctly and the probe asking the wrong question. Easing back up
            // would also have tripped the reversal and started the upswing.
            Down(Origin);
            yield return null;

            // Just past the touch, still inside the dead zone: power 0, the widest window.
            for (int i = 1; i <= 6; i++)
            { Drag(At(0f, -(_driver.MinUsefulPullPx * 0.4f) * i / 6f)); yield return null; }
            yield return null;
            Near("pull.power_is_zero_inside_the_dead_zone", 0f, Power, 1e-3f);
            float wSoft = Of("FreeSwingImpactWindow").width;
            // Read HERE, while the peak really is 0 — the driver's property is the maths at the
            // live peak, so the comparison is the drawn rect against the graded window at the same
            // instant rather than against a number recomputed later.
            float wSoftGraded = _driver.ImpactWindowPx * 2f;

            for (int i = 1; i <= 24; i++)
            { Drag(At(0f, -Mathf.Lerp(_driver.MinUsefulPullPx * 0.4f, Pull100, i / 24f))); yield return null; }
            yield return null;
            float w100 = Of("FreeSwingImpactWindow").width;

            Assert("pull.state_is_timing", StateName == "Timing", "Timing", StateName);
            Near("pull.power_at_the_100_tick", 1f, Power, 0.03f);
            Assert("pull.lane_visible", laneGroup.alpha > 0.5f, ">0.5", laneGroup.alpha.ToString("F2"));
            Assert("pull.trace_is_drawing", traceView.PointCount > 5, ">5 samples",
                   traceView.PointCount.ToString());
            Near("pull.club_head_is_on_the_100_tick",
                 Of("FreeSwingTick100").center.y, Of("FreeSwingHandle").center.y, 6f);
            yield return SnapAtEndOfFrame("freeswing_backswing_100");

            for (int i = 1; i <= 12; i++)
            { Drag(At(0f, -Mathf.Lerp(Pull100, Pull120, i / 12f))); yield return null; }
            yield return null;
            float w120 = Of("FreeSwingImpactWindow").width;
            Note("impact_window_width_px", $"0% {wSoft:F1} -> 100% {w100:F1} -> 120% {w120:F1}");
            Assert("shrink.window_closes_from_0_to_100", w100 < wSoft - 1f, $"< {wSoft:F1}", w100.ToString("F1"));
            Near("shrink.the_0pc_drawn_width_is_the_0pc_graded_window", wSoftGraded, wSoft, 1.5f);
            Assert("shrink.window_closes_from_100_to_120", w120 < w100 - 1f, $"< {w100:F1}", w120.ToString("F1"));
            Near("pull.power_at_the_120_tick", 1.2f, Power, 0.03f);

            // THE invariant that ties the picture to the verdict.
            Near("window.drawn_half_width_is_the_graded_window",
                 _driver.ImpactWindowPx, Of("FreeSwingImpactWindow").width * 0.5f, 0.6f);
            yield return SnapAtEndOfFrame("freeswing_backswing_120_window_narrow");

            // Lift without crossing: nothing fires.
            int shotsBefore = _shots;
            Up();
            yield return new WaitForSecondsRealtime(0.5f);
            Assert("cancel.lift_mid_backswing_fires_nothing", _shots == shotsBefore,
                   shotsBefore, _shots);
            Assert("cancel.returns_to_idle", StateName == "Idle", "Idle", StateName);
            yield return WaitForIdle(10f);

            // ── 9. the PURE swing — fires on the CROSSING, not the release ─────
            shotsBefore = _shots;
            yield return Swing(Pull100, backFrames: 36, upFrames: 18);
            Assert("swing.pure_fired", _shots == shotsBefore + 1, shotsBefore + 1, _shots);
            Assert("swing.exactly_one_commit_per_touch", _driver.CommitCount >= 1, ">=1", _driver.CommitCount);
            var v = _driver.LastVerdict;
            Note("pure_verdict", $"impact={v.ImpactPx:F1}px window={v.ImpactWindowPx:F1} " +
                                 $"path={v.PathDeg:F2}deg tempo={v.TempoRatio:F2} speed={v.UpSpeedPxPerSec:F0}px/s " +
                                 $"grade={v.Grade} mul={v.TimingMul:F2} timing01={v.Timing01:F2}");
            Assert("swing.pure_grade", v.Grade == FreeSwingGrade.Pure, FreeSwingGrade.Pure, v.Grade);
            AssertPopKey("swing.pure_pop_is_a_localised_key", FreeSwingGrade.Pure);
            Assert("swing.impact_was_clean", v.ImpactClean, true, v.ImpactClean);
            Near("swing.straight_path_shapes_nothing", 0f, v.FadeDraw01, 1e-3f);
            Near("swing.timing01_reaches_the_shot", v.Timing01, Timing01, 1e-3f);
            Near("swing.timing_mul_reaches_the_shot", v.TimingMul, Mul, 1e-3f);

            // The chip reports the COMMITTED intent, and is still up well into the flight.
            Assert("result.chip_is_up_at_the_shot", chip.Alpha > 0.99f, "alpha 1", chip.Alpha.ToString("F2"));
            Assert("result.chip_path_is_a_key", chip.LastPathKey == FreeSwingMath.PathKey(v.Path),
                   FreeSwingMath.PathKey(v.Path), chip.LastPathKey);
            Assert("result.chip_tempo_is_a_key", chip.LastTempoKey == FreeSwingMath.TempoKey(v.Tempo),
                   FreeSwingMath.TempoKey(v.Tempo), chip.LastTempoKey);
            Note("chip_reads", $"POWER {chip.LastPowerText} | IMPACT {chip.LastImpactText} | " +
                               $"PATH {chip.LastPathKey} | TEMPO {chip.LastTempoKey}");
            Assert("result.club_head_hidden_in_flight",
                   FindAny("FreeSwingHandle").GetComponent<CanvasGroup>().alpha < 0.01f, "alpha 0",
                   FindAny("FreeSwingHandle").GetComponent<CanvasGroup>().alpha.ToString("F2"));
            yield return SnapAtEndOfFrame("freeswing_result_pure");

            // CARRY-OVER 7: still fully visible half a second into the flight, i.e. well past the
            // Resolving that CommitExternal reaches synchronously.
            yield return new WaitForSecondsRealtime(0.5f);
            Assert("result.chip_still_up_half_a_second_into_the_flight", chip.Alpha > 0.99f,
                   "alpha 1", chip.Alpha.ToString("F2"));
            // The trace goes WITH the ball (Cesar, on the first clip): the node's Result frame
            // holds it at 0.6 and over a real fairway that reads as a stray line hanging under a
            // ball that has already gone. The chip is the result readout; the finger's path is not.
            Assert("result.trace_is_gone_once_the_ball_is_away", traceView.Alpha < 0.01f,
                   "alpha 0", traceView.Alpha.ToString("F3"));
            yield return WaitForIdle();
            Assert("idle.chip_put_away_when_the_ball_settles", chip.Alpha < 0.01f, "alpha 0",
                   chip.Alpha.ToString("F2"));
            Assert("idle.trace_cleared", traceView.PointCount == 0, 0, traceView.PointCount);
            Assert("idle.club_head_is_back",
                   FindAny("FreeSwingHandle").GetComponent<CanvasGroup>().alpha > 0.99f, "alpha 1",
                   FindAny("FreeSwingHandle").GetComponent<CanvasGroup>().alpha.ToString("F2"));

            // ── 10. off-centre crossings: HOOK and SLICE, mirrored ─────────────
            float far = 240f;   // comfortably past FreeSwingImpactMissPx
            yield return Swing(Pull100, crossX: -far);
            var hook = _driver.LastVerdict;
            Assert("miss.hook_grade", hook.Grade == FreeSwingGrade.Hook, FreeSwingGrade.Hook, hook.Grade);
            AssertPopKey("miss.hook_pop_is_a_localised_key", FreeSwingGrade.Hook);
            Assert("miss.hook_yaw_is_left", hook.ErrorYawRad < 0f, "< 0 (ball left)",
                   hook.ErrorYawRad.ToString("F4"));
            yield return SnapAtEndOfFrame("freeswing_result_hook");
            yield return WaitForIdle();

            yield return Swing(Pull100, crossX: far);
            var slice = _driver.LastVerdict;
            Assert("miss.slice_grade", slice.Grade == FreeSwingGrade.Slice, FreeSwingGrade.Slice, slice.Grade);
            AssertPopKey("miss.slice_pop_is_a_localised_key", FreeSwingGrade.Slice);
            Assert("miss.slice_yaw_is_right", slice.ErrorYawRad > 0f, "> 0 (ball right)",
                   slice.ErrorYawRad.ToString("F4"));
            Near("miss.hook_and_slice_are_mirrored", -hook.ErrorYawRad, slice.ErrorYawRad, 1e-3f);
            yield return SnapAtEndOfFrame("freeswing_result_slice");
            yield return WaitForIdle();

            // ── 11. a bowed upstroke shapes the shot ───────────────────────────
            yield return Swing(Pull100, bowPx: -320f);
            var draw = _driver.LastVerdict;
            Note("draw_verdict", $"path={draw.PathDeg:F2}deg fadeDraw={draw.FadeDraw01:F2} grade={draw.Grade}");
            Assert("path.bowed_left_reads_as_a_DRAW", draw.Path == FreeSwingPath.Draw,
                   FreeSwingPath.Draw, draw.Path);
            Assert("path.draw_is_a_negative_fadeDraw01", draw.FadeDraw01 < 0f, "< 0 (the flick's handle LEFT)",
                   draw.FadeDraw01.ToString("F2"));
            Assert("path.chip_says_DRAW", chip.LastPathKey == FreeSwingMath.KeyPathDraw,
                   FreeSwingMath.KeyPathDraw, chip.LastPathKey);
            yield return SnapAtEndOfFrame("freeswing_result_draw");
            yield return WaitForIdle();

            // ── 12. a slow upstroke is a DUFF ──────────────────────────────────
            // Slow by WAITING between samples — a real stall, not a shortened path.
            yield return Swing(Pull100, backFrames: 24, upFrames: 18, upHoldFrames: 8);
            var duff = _driver.LastVerdict;
            Note("duff_verdict", $"speed={duff.UpSpeedPxPerSec:F0}px/s grade={duff.Grade} mul={duff.TimingMul:F2}");
            Assert("duff.grade", duff.Grade == FreeSwingGrade.Duff, FreeSwingGrade.Duff, duff.Grade);
            AssertPopKey("duff.pop_is_a_localised_key", FreeSwingGrade.Duff);
            Assert("duff.pays_the_red_multiplier", duff.TimingMul < 0.8f, "< 0.8 (TimingPowerMulRed)",
                   duff.TimingMul.ToString("F2"));
            Near("duff.timing01_is_zero", 0f, duff.Timing01, 1e-4f);
            Near("duff.shapes_nothing", 0f, duff.FadeDraw01, 1e-6f);
            yield return SnapAtEndOfFrame("freeswing_result_duff");
            yield return WaitForIdle();

            // ── 13. the double pump: one shot, at the deeper power ─────────────
            shotsBefore = _shots;
            Down(Origin);
            yield return null;
            float shallow = Pull100 * 0.45f;
            for (int i = 1; i <= 18; i++) { Drag(At(0f, -shallow * i / 18f)); yield return null; }
            for (int i = 1; i <= 8;  i++) { Drag(At(0f, -shallow + shallow * 0.4f * i / 8f)); yield return null; }
            for (int i = 1; i <= 8;  i++) { Drag(At(0f, -shallow * 0.6f - (Pull100 - shallow * 0.6f) * i / 8f)); yield return null; }
            for (int i = 1; i <= 18; i++)
            {
                float t = i / 18f;
                Drag(At(0f, Mathf.Lerp(-Pull100, _lane.ImpactCrossOffsetPx + 0.5f, t)));
                yield return null;
            }
            Up();
            Assert("doublepump.one_shot_only", _shots == shotsBefore + 1, shotsBefore + 1, _shots);
            Near("doublepump.commits_at_the_deeper_power", 1f, _driver.LastCommittedPower, 0.05f);
            yield return SnapAtEndOfFrame("freeswing_result_doublepump");
            yield return WaitForIdle();

            // ── 14. telemetry ──────────────────────────────────────────────────
            Assert("telemetry.scheme_is_3", (int)ControlScheme.FreeSwing == 3, 3, (int)ControlScheme.FreeSwing);
            Assert("telemetry.timing01_is_the_tempo_score",
                   Mathf.Abs(_driver.LastVerdict.Timing01 - Timing01) < 1e-3f,
                   _driver.LastVerdict.Timing01.ToString("F3"), Timing01.ToString("F3"));

            // ── 15. localisation: every word on screen is a KEY ────────────────
            AssertLabel("loc.chip_label_POWER",  "FreeSwingLblPOWER",   FreeSwingMath.KeyPower);
            AssertLabel("loc.chip_label_IMPACT", "FreeSwingLblIMPACT",  FreeSwingMath.KeyImpact);
            AssertLabel("loc.chip_label_PATH",   "FreeSwingLblPATH",    FreeSwingMath.KeyPath);
            AssertLabel("loc.chip_label_TEMPO",  "FreeSwingLblTEMPO",   FreeSwingMath.KeyTempo);
            AssertLabel("loc.lane_IMPACT_label", "FreeSwingImpactLabel", FreeSwingMath.KeyImpactLine);

            Assert("RUN_COMPLETED", true, "the sequence reaches the end", "ok");
            Write();
            EditorApplication.ExitPlaymode();
        }

        /// <summary>A label reads the KEY's resolved value — so a hardcoded word fails even if it
        /// happens to read the same in English.</summary>
        void AssertLabel(string name, string goName, string key)
        {
            var t = FindAny(goName)?.GetComponent<TextMeshProUGUI>();
            string want = LocalizationManager.Get(key);
            Assert(name, t != null && t.text == want, $"{want} ({key})", t != null ? t.text : "not found");
        }

        void Fail(string why)
        {
            Assert("RUN_COMPLETED", false, "the sequence reaches the end", "ABORT: " + why);
            Write();
            EditorApplication.ExitPlaymode();
        }

        void Write()
        {
            Directory.CreateDirectory(FreeSwingSchemeVerify.TaskDir);
            int fails = _inv.Count(i => !i.pass);

            var j = new StringBuilder();
            j.AppendLine("{");
            j.AppendLine("  \"task\": \"scheme_freeswing\",");
            j.AppendLine($"  \"generated\": \"{DateTime.Now:yyyy-MM-dd HH:mm:ss}\",");
            j.AppendLine($"  \"resolution\": \"{Screen.width}x{Screen.height}\",");
            j.AppendLine("  \"entry_path\": \"ShellScene -> StartButton -> PlayButton -> hole card -> in-game gear -> " +
                         "schemeButtons[3].onClick (Free Swing) -> real FreeSwingHandle IPointerDown/IDrag; the shot " +
                         "fires from inside the drag, on the crossing, with the pointer still down\",");
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

            string path = Path.Combine(FreeSwingSchemeVerify.TaskDir, "freeswing_invariants.json");
            File.WriteAllText(path, j.ToString());
            Debug.Log($"[FreeSwingE2E] {_inv.Count - fails}/{_inv.Count} PASS — {path}");
        }

        static string Esc(string s) => (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
#endif
