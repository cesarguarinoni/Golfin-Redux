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
using Golfin.Diagnostics.Runtime;
using Golfin.Gameplay.UI.Controls;
using Golfin.Gameplay.UI.ShotUI;

namespace Golfin.UI.EditorTools
{
    /// <summary>
    /// flick_shot_view § 4 — the acceptance run for Flick's new framing.
    ///
    /// <para>REAL ENTRY PATH, not a render harness (Cesar's standing rule): boots
    /// <c>ShellScene</c>, taps the title screen's own StartButton, PLAY and a hole card, and reads
    /// every number off the LIVE rects afterwards. Structurally a sibling of
    /// <see cref="MissDuffFlickVerify"/> — the boot idiom is that tool's, verbatim.</para>
    ///
    /// <para>WHY A JSON AND NOT A LOOK. The claim being made is arithmetic — "the cone's base is
    /// the shared bottom baseline", "the ball is at viewport 0.38" — and a frame cannot carry a
    /// per-assertion verdict. The frames are here for Cesar; the JSON is the gate.</para>
    ///
    /// <para>The BEFORE column is measured, not quoted: the ball widget is put back on viewport
    /// 0.5 and the aim camera re-posed, so the pitch and horizon pair come from the same hole in
    /// the same session as the after pair.</para>
    ///
    /// <para>Menu: <b>GOLFIN ▸ ShotUI ▸ Verify Flick Shot View</b>.</para>
    /// </summary>
    public static class FlickShotViewVerify
    {
        const string ShellScenePath = "Assets/Scenes/ShellScene.unity";
        const string ArmedKey       = "FlickShotViewVerify.Armed";
        public const string TaskDir = "Docs/Specs/Active/flick_shot_view";

        [InitializeOnLoadMethod]
        static void Hook() => EditorApplication.playModeStateChanged += OnPlayModeChanged;

        [MenuItem("GOLFIN/ShotUI/Verify Flick Shot View")]
        public static void Launch()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[FlickView] Already in play mode — stop first.");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            EditorSceneManager.OpenScene(ShellScenePath);
            Directory.CreateDirectory($"{TaskDir}/screenshots");
            Directory.CreateDirectory($"{TaskDir}/evidence");
            SessionState.SetBool(ArmedKey, true);
            EditorApplication.EnterPlaymode();
            Debug.Log("[FlickView] Armed. Entering play mode…");
        }

        static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode) return;
            if (!SessionState.GetBool(ArmedKey, false)) return;
            SessionState.SetBool(ArmedKey, false);

            Application.runInBackground = true;   // MANDATORY for MCP-driven runs
            // The shared 1170x2532 pin. Every number in the spec is in canvas px at that height,
            // and the Game View shows whatever size the ACTIVE BUILD TARGET's group is on —
            // pinning it is what stops a 1920x1080 window quietly answering a 2532 question.
            Golfin.EditorTools.ShotUI.PendulumSchemeVerify.ForceCaptureResolution();
            var host = new GameObject("[FlickShotViewBot]");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.AddComponent<FlickShotViewRunner>();
        }
    }

    public class FlickShotViewRunner : MonoBehaviour
    {
        const BindingFlags NP = BindingFlags.NonPublic | BindingFlags.Instance;
        const BindingFlags AN = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance;

        // Lomond hole 2 is the reference hole shot_view_layout measured on; the rest are the
        // fallbacks its sibling tool uses when a card is locked.
        static readonly int[] HolePreference = { 2, 1, 10, 4, 9 };

        readonly List<string> _log = new List<string>();
        readonly List<(string name, bool pass, string detail)> _asserts =
            new List<(string, bool, string)>();
        readonly Dictionary<string, object> _json = new Dictionary<string, object>();

        /// <summary>Golfin.Gameplay.Config is autoReferenced:false, so the tuning struct is read
        /// by reflection — the same reason MissDuffFlickVerify reaches ShotController that way.
        /// Reflected off ControlsConfig.Default, i.e. the struct the drivers actually run on.</summary>
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

        void Note(string k, object v) { _log.Add($"{k}: {v}"); Debug.Log($"[FlickView] {k}: {v}"); }
        void Assert(string name, bool pass, string detail)
        {
            _asserts.Add((name, pass, detail));
            Debug.Log($"[FlickView] {(pass ? "PASS" : "FAIL")} {name} — {detail}");
        }

        void Start() => StartCoroutine(Sequence());

        // ── boot helpers (MissDuffFlickVerify's, verbatim) ───────────────────────
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
            Debug.LogWarning($"[FlickView] TIMEOUT waiting for '{goName}'");
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
            Debug.LogWarning($"[FlickView] TIMEOUT waiting for hole {hole} card");
        }

        // ── the aim camera, reached the way ShotLayoutController reaches it ──────
        MonoBehaviour _lab;
        Camera        _aimCam;

        bool BindLab()
        {
            _lab = UnityEngine.Object
                .FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(m => m.GetType().Name == "PhysicsLabController");
            if (_lab == null) return false;
            var chase = _lab.GetType().GetField("chaseCamera", AN)?.GetValue(_lab) as Component;
            _aimCam = chase != null ? chase.GetComponent<Camera>() : null;
            return _aimCam != null;
        }

        /// <summary>Re-pose the aim camera off the CURRENT ball-widget position — the same
        /// reflection call <c>ShotLayoutController.NudgeAimCamera</c> makes, so the before/after
        /// pitches below are the production solve and not a second implementation of it.</summary>
        void ReposeAimCamera()
        {
            var m = _lab.GetType().GetMethod("ApplyCameraYaw", AN);
            if (m != null && _aimCam != null) m.Invoke(_lab, new object[] { _aimCam });
        }

        static float Pitch(Camera cam)
        {
            float x = cam.transform.eulerAngles.x;
            return x > 180f ? x - 360f : x;      // signed: down-tilt positive, as the prior task quoted
        }

        // ── frame capture + horizon ─────────────────────────────────────────────

        /// <summary>Save one full-resolution frame through the sanctioned capture path and return
        /// its path. <c>SnapPlayModeSafe</c> composites through
        /// <c>ScreenCapture.CaptureScreenshotAsTexture</c>, which returns null anywhere but
        /// end-of-frame — hence the caller-owned yield, and hence the existence check: the same
        /// call has been seen to hand back a path for a file it never wrote.</summary>
        IEnumerator SaveFrame(string label, string destName, System.Action<string> onDone)
        {
            yield return new WaitForEndOfFrame();
            string snapped = CaptureCore.SnapPlayModeSafe(label);
            if (string.IsNullOrEmpty(snapped) || !File.Exists(snapped) ||
                new FileInfo(snapped).Length < 1024)
            {
                Note($"{label}_CAPTURE_FAILED", $"SnapPlayModeSafe returned '{snapped}'");
                onDone(null);
                yield break;
            }
            string dst = Path.Combine(FlickShotViewVerify.TaskDir, "screenshots", destName);
            File.Copy(snapped, dst, true);
            Note($"{label}_png", $"{dst} ({new FileInfo(dst).Length / 1024} KB, md5 {Md5(dst)})");
            onDone(dst);
        }

        static string Md5(string path)
        {
            using (var md5 = System.Security.Cryptography.MD5.Create())
            using (var fs = File.OpenRead(path))
                return BitConverter.ToString(md5.ComputeHash(fs)).Replace("-", "")
                                   .Substring(0, 8).ToLowerInvariant();
        }

        /// <summary>The far-left strip the horizon is measured in. NARROWER THAN THE 40–300 THE
        /// PRIOR TASK QUOTES, and that is the whole correction: the player card starts at x≈42 and
        /// runs from y≈135 down, so a scan that begins at x=40 stops on the card's top edge and
        /// answers 6.2 % for EVERY framing — which is exactly what the first run of this tool
        /// reported, identically for the before and after frames. x 2–38 is clear of every HUD
        /// element in both.</summary>
        const int HorizonBandX0 = 2, HorizonBandX1 = 38;

        /// <summary>The horizon this ruler reads on <c>shot_view_layout</c>'s own accepted
        /// <c>screenshots/pendulum_038_hole2.png</c> — the framing Cesar signed off, and the one
        /// Flick now shares to the last decimal of camera pitch. That task's report quotes 37.8 %
        /// for those same pixels; this ruler runs ~3.5 points lower on every frame (their Flick
        /// 25.5 % reads 21.8 % here). So the gate is "matches the accepted framing", not a raw
        /// percentage that would be comparing two different rulers.</summary>
        const float HorizonReferencePct = 34.28f;

        /// <summary>
        /// The sky/turf boundary as a percentage from the top. Sky is the half where blue beats
        /// green; the boundary is the bottom of the first sky run of 20+ rows, taken as the median
        /// across columns so one tree cannot move it.
        /// </summary>
        static float HorizonPercent(string pngPath, out string detail)
        {
            detail = "";
            if (pngPath == null || !File.Exists(pngPath)) { detail = "no frame"; return float.NaN; }
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!tex.LoadImage(File.ReadAllBytes(pngPath))) { detail = "decode failed"; return float.NaN; }

            int w = tex.width, h = tex.height;
            var rows = new List<int>();
            for (int x = HorizonBandX0; x < Mathf.Min(HorizonBandX1, w); x += 2)
            {
                int y = h - 1, run = 0, boundary = -1;
                for (; y >= 0; y--)
                {
                    Color c = tex.GetPixel(x, y);
                    bool sky = c.b >= c.g;
                    if (sky) { run++; }
                    else if (run >= 20) { boundary = y; break; }
                    else run = 0;
                }
                if (boundary >= 0) rows.Add(h - 1 - boundary);   // rows from the TOP
            }
            UnityEngine.Object.DestroyImmediate(tex);
            if (rows.Count == 0) { detail = "no sky/turf boundary found"; return float.NaN; }
            rows.Sort();
            int med = rows[rows.Count / 2];
            detail = $"{rows.Count} columns, median row {med} of {h}";
            return 100f * med / h;
        }

        // ── live geometry ───────────────────────────────────────────────────────
        RectTransform _canvasRect, _ball, _coneMesh, _clubHandle, _buttons;
        ConeMeshGraphic _coneGraphic;
        ShotConeView    _coneView;

        float CanvasH => _canvasRect.rect.height;

        /// <summary>A rect's y in CANVAS-centre space — the space every number in the spec is in.
        /// Taken through world corners rather than by adding anchoredPositions, so a re-parenting
        /// cannot quietly make the arithmetic wrong.</summary>
        float CanvasY(RectTransform rt, float pivotY01)
        {
            var c = new Vector3[4];
            rt.GetWorldCorners(c);                       // 0 BL, 1 TL, 2 TR, 3 BR
            Vector3 world = Vector3.Lerp(c[0], c[1], pivotY01);
            return _canvasRect.InverseTransformPoint(world).y;
        }

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

            // Ten seconds, not the sibling tool's four: HoleMap appearing means the shot UI is up,
            // not that the world has finished streaming in.
            //
            // The SKY BEING A DIFFERENT COLOUR BETWEEN RUNS IS NOT A SETTLE PROBLEM and no wait
            // fixes it — `SkyRandomizer` draws a preset per session, so one run photographs a clear
            // noon and the next an overcast one. The horizon detector is blue-vs-green, which holds
            // on both. Worth knowing before anyone else spends three runs on it.
            yield return new WaitForSecondsRealtime(10f);

            // Flick is the shipping default, but say so out loud rather than assuming it.
            if (ControlSchemeService.Current != ControlScheme.Flick)
                ControlSchemeService.Set(ControlScheme.Flick, "flick_shot_view acceptance");
            yield return new WaitForSecondsRealtime(1.5f);

            if (!Bind()) { Fail("could not bind the shot-view rects"); yield break; }
            if (!BindLab()) { Fail("could not bind PhysicsLabController / aim camera"); yield break; }

            Note("resolution", $"{Screen.width}x{Screen.height}");
            Note("canvas", $"{_canvasRect.rect.width} x {CanvasH}");
            Note("scheme", ControlSchemeService.Current);

            // ── BEFORE: put the widget back on the old 0.5 anchor and re-pose ────
            float afterBallY = _ball.anchoredPosition.y;
            SetBallY(0f);
            yield return null;
            ReposeAimCamera();
            yield return new WaitForSecondsRealtime(1.0f);
            float pitchBefore = Pitch(_aimCam);
            Vector3 camPosBefore = _aimCam.transform.position;
            // A CAMERA-ONLY A/B. Only the ball WIDGET goes back to 0.5 — BallSpace, and so the
            // cone and the club, stay where this task put them, because the cone that stood at the
            // 0.5 anchor was 1160px tall and no longer exists to photograph. The pair below is
            // therefore an honest before/after of the CAMERA, and nothing else in the frame.
            string beforePng = null;
            yield return SaveFrame("flickview_camera_before_vy050", "camera_before_vy050.png", p => beforePng = p);
            float horizonBefore = HorizonPercent(beforePng, out string hbDetail);

            // ── AFTER: back to the resolved anchor ───────────────────────────────
            SetBallY(afterBallY);
            yield return null;
            ReposeAimCamera();
            yield return new WaitForSecondsRealtime(1.0f);
            float pitchAfter = Pitch(_aimCam);
            Vector3 camPosAfter = _aimCam.transform.position;
            string afterPng = null;
            yield return SaveFrame("flickview_flick_038_hole2", "flick_038_hole2.png", p => afterPng = p);
            float horizonAfter = HorizonPercent(afterPng, out string haDetail);

            // ── the numbers ──────────────────────────────────────────────────────
            float H         = CanvasH;
            float ballY     = _ball.anchoredPosition.y;
            float ballVy    = ballY / H + 0.5f;
            float baseline  = ShotLayoutMath.Baseline(Cfg("BottomBaselinePx"), 0f);
            float baselineY = -H * 0.5f + baseline;
            float coneBase  = CanvasY(_coneMesh, 0f);
            float coneH     = _coneGraphic.HeightPx;
            float apex      = coneBase + coneH;
            float restY     = coneBase + _coneView.HandleRestYPx;
            float liveHandleY = CanvasY(_clubHandle, 0f);
            float halfBase20 = coneH * Mathf.Tan(Cfg("ConeHalfAngleAtAcc100Deg") * Mathf.Deg2Rad);
            float halfBase5  = coneH * Mathf.Tan(Cfg("ConeHalfAngleAtAcc0Deg")  * Mathf.Deg2Rad);
            float btnInner   = ButtonInnerEdgeX();

            _json["hole"]                 = hole;
            _json["resolution"]           = $"{Screen.width}x{Screen.height}";
            _json["canvas_height"]        = H;
            _json["cone_height_px"]       = coneH;
            _json["cone_apex_gap_px"]     = Cfg("FlickConeApexGapPx");
            _json["handle_rest_01"]       = Cfg("FlickHandleStartY01");
            _json["ball_y"]               = ballY;
            _json["ball_viewport_y"]      = ballVy;
            _json["cone_apex_y"]          = apex;
            _json["cone_base_y"]          = coneBase;
            _json["baseline_y"]           = baselineY;
            _json["handle_rest_y"]        = restY;
            _json["handle_live_y"]        = liveHandleY;
            _json["cone_half_base_20deg"] = halfBase20;
            _json["cone_half_base_5deg"]  = halfBase5;
            _json["button_inner_edge_x"]  = btnInner;
            _json["aim_cam_pitch_before"] = pitchBefore;
            _json["aim_cam_pitch_after"]  = pitchAfter;
            _json["aim_cam_pos_before"]   = camPosBefore.ToString("F2");
            _json["aim_cam_pos_after"]    = camPosAfter.ToString("F2");
            _json["horizon_pct_before"]   = horizonBefore;
            _json["horizon_pct_after"]    = horizonAfter;
            _json["horizon_detail_before"] = hbDetail;
            _json["horizon_detail_after"]  = haDetail;
            _json["frame_before"]         = beforePng ?? "<none>";
            _json["frame_after"]          = afterPng  ?? "<none>";

            Assert("ball_at_038",        Mathf.Abs(ballY   - (-304f))  <= 2f,  $"ball y {ballY:F2} (vy {ballVy:F4})");
            Assert("cone_height_792",    Mathf.Abs(coneH   - 792f)     <= 0.5f, $"cone height {coneH:F1}");
            // The apex is ON the ball (Cesar, 2026-09-07), so this is an equality against the ball
            // rather than a target y — the assertion says the thing he asked for, in his words.
            Assert("cone_apex_touches_the_ball", Mathf.Abs(apex - ballY) <= 1f,
                   $"apex {apex:F2} vs ball {ballY:F2} — gap {ballY - apex:F2}px");
            Assert("cone_base_1096",     Mathf.Abs(coneBase- (-1096f)) <= 2f,  $"base {coneBase:F2}");
            Assert("base_on_baseline",   Mathf.Abs(coneBase- baselineY)<= 2f,  $"base {coneBase:F2} vs baseline {baselineY:F2}");
            Assert("handle_rest_556",    Mathf.Abs(restY   - (-556f))  <= 2f,  $"handle rest {restY:F2}");
            Assert("pull_travel_matches_pendulum",
                   Mathf.Abs(_coneView.HandleRestYPx - Cfg("PendulumPull100Px")) <= 1f,
                   $"rest-to-100% travel {_coneView.HandleRestYPx:F1}px vs PendulumPull100Px " +
                   $"{Cfg("PendulumPull100Px"):F0} and FreeSwingPull100Px {Cfg("FreeSwingPull100Px"):F0} " +
                   $"(was 960 on the old 1160 cone)");
            Assert("cone_clears_buttons", halfBase20 < btnInner,               $"half-base@20 {halfBase20:F1} < button inner edge {btnInner:F1}");
            Assert("horizon_matches_accepted_framing",
                   Mathf.Abs(horizonAfter - HorizonReferencePct) <= 1.0f,
                   $"horizon {horizonAfter:F2}% vs {HorizonReferencePct:F2}% on shot_view_layout's " +
                   $"accepted pendulum_038_hole2.png measured by this same ruler " +
                   $"(that task quotes 37.8% for those pixels) — {haDetail}");
            Assert("horizon_gained_over_the_old_anchor", horizonAfter - horizonBefore >= 10f,
                   $"{horizonBefore:F2}% -> {horizonAfter:F2}% (+{horizonAfter - horizonBefore:F2} points)");
            Assert("camera_pitched_up",  pitchAfter < pitchBefore,             $"{pitchBefore:F3}deg -> {pitchAfter:F3}deg");
            Assert("camera_did_not_move", (camPosAfter - camPosBefore).magnitude < 0.01f,
                   $"{camPosBefore:F2} -> {camPosAfter:F2} (the ball anchor is the ONLY thing that changed)");

            // ── one frame with the cone actually drawn ───────────────────────────
            yield return CapturePullFrame();

            // ── putt geometry ────────────────────────────────────────────────────
            yield return MeasurePutt();

            // ── scheme switch: Flick <-> Pendulum both frame at 0.38 ─────────────
            yield return SwitchAndCompare();

            Write();
            yield return new WaitForSecondsRealtime(1f);
            EditorApplication.ExitPlaymode();
        }

        bool Bind()
        {
            _coneView = UnityEngine.Object.FindObjectsByType<ShotConeView>(
                            FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
            if (_coneView == null) return false;

            _coneGraphic = typeof(ShotConeView).GetField("_coneGraphic", NP)?.GetValue(_coneView) as ConeMeshGraphic;
            _clubHandle  = typeof(ShotConeView).GetField("_clubHandle",  NP)?.GetValue(_coneView) as RectTransform;
            _coneMesh    = _coneGraphic != null ? _coneGraphic.rectTransform : null;

            var layout = ShotLayoutController.Active;
            if (layout != null)
            {
                _canvasRect = typeof(ShotLayoutController).GetField("_canvasRect", NP)?.GetValue(layout) as RectTransform;
                _ball       = typeof(ShotLayoutController).GetField("_centralBall", NP)?.GetValue(layout) as RectTransform;
                _buttons    = typeof(ShotLayoutController).GetField("_actionButtonsCluster", NP)?.GetValue(layout) as RectTransform;
            }
            return _canvasRect != null && _ball != null && _coneMesh != null && _clubHandle != null;
        }

        void SetBallY(float y)
        {
            _ball.anchoredPosition = new Vector2(_ball.anchoredPosition.x, y);
        }

        /// <summary>The inward edge of the nearest action button, in canvas px from the centre.
        /// Measured off the live buttons, because the number the spec quotes (382) is the one the
        /// cone's half-base has to clear.</summary>
        float ButtonInnerEdgeX()
        {
            if (_buttons == null) return float.PositiveInfinity;
            float inner = float.PositiveInfinity;
            foreach (var b in _buttons.GetComponentsInChildren<Button>(true))
            {
                var rt = b.transform as RectTransform;
                if (rt == null) continue;
                var c = new Vector3[4];
                rt.GetWorldCorners(c);
                float lx = Mathf.Abs(_canvasRect.InverseTransformPoint(c[0]).x);
                float rx = Mathf.Abs(_canvasRect.InverseTransformPoint(c[2]).x);
                inner = Mathf.Min(inner, Mathf.Min(lx, rx));
            }
            return inner;
        }

        /// <summary>
        /// Hold a REAL pull at ~55 % and photograph it, so the frame Cesar looks at contains the
        /// 641 cone rather than an Idle tee.
        ///
        /// <para>The release carries no upward motion at all, so <c>EvaluateFlickGate</c> rejects
        /// it and the swing resets — the acceptance run photographs the control without ever
        /// taking a shot, which is what keeps the ball on the tee for the frames after this one.
        /// </para>
        /// </summary>
        IEnumerator CapturePullFrame()
        {
            var dragger = UnityEngine.Object.FindObjectsByType<ClubHandleDragger>(
                              FindObjectsInactive.Exclude, FindObjectsSortMode.None).FirstOrDefault();
            if (dragger == null) { Note("pull_frame", "no ClubHandleDragger — skipped"); yield break; }

            var coneRect = typeof(ClubHandleDragger).GetField("_coneRect", NP)?.GetValue(dragger) as RectTransform;
            var canvas   = dragger.GetComponentInParent<Canvas>();
            var caster   = canvas != null ? canvas.rootCanvas.GetComponent<GraphicRaycaster>() : null;
            Camera uiCam = caster != null ? caster.eventCamera : null;
            if (coneRect == null) { Note("pull_frame", "no _coneRect — skipped"); yield break; }

            float coneH = _coneGraphic.HeightPx;
            Func<float, Vector2> at = local01 => RectTransformUtility.WorldToScreenPoint(
                uiCam, coneRect.TransformPoint(new Vector3(0f, coneH * local01, 0f)));

            var rr  = new RaycastResult { module = caster, screenPosition = at(0.92f) };
            var ped = new PointerEventData(EventSystem.current)
            { position = at(0.92f), pointerId = 0, button = PointerEventData.InputButton.Left };
            ped.pointerPressRaycast = ped.pointerCurrentRaycast = rr;
            ExecuteEvents.Execute(dragger.gameObject, ped, ExecuteEvents.pointerDownHandler);
            yield return null;

            Vector2 from = at(0.92f), to = at(0.45f);
            for (int i = 1; i <= 8; i++)
            {
                Vector2 next = Vector2.Lerp(from, to, i / 8f);
                ped.delta = next - ped.position;
                ped.position = next;
                ExecuteEvents.Execute(dragger.gameObject, ped, ExecuteEvents.dragHandler);
                yield return null;
            }
            for (int i = 0; i < 6; i++)
            {
                ped.delta = Vector2.zero;
                ExecuteEvents.Execute(dragger.gameObject, ped, ExecuteEvents.dragHandler);
                yield return null;
            }

            float handleAtPull = CanvasY(_clubHandle, 0f);
            _json["pull_handle_y"] = handleAtPull;
            Note("pull_handle_y", handleAtPull.ToString("F2"));

            string pullPng = null;
            yield return SaveFrame("flickview_pull_55pct", "flick_pull_792cone.png", p => pullPng = p);
            _json["frame_pull"] = pullPng ?? "<none>";

            ped.delta = Vector2.zero;                       // no rise => the flick gate rejects
            ExecuteEvents.Execute(dragger.gameObject, ped, ExecuteEvents.pointerUpHandler);
            yield return new WaitForSecondsRealtime(1.5f);
            Note("pull_frame_state_after_release", "handle back at " + CanvasY(_clubHandle, 0f).ToString("F2"));
        }

        IEnumerator MeasurePutt()
        {
            var trackGO = typeof(ShotConeView).GetField("_putterTrack", NP)?.GetValue(_coneView) as GameObject;
            var slabRT  = typeof(ShotConeView).GetField("_putterTimingSlabRT", NP)?.GetValue(_coneView) as RectTransform;
            RectTransform track = trackGO != null ? trackGO.transform as RectTransform
                                : (slabRT != null ? slabRT.parent as RectTransform : null);
            if (track == null) { Note("putt", "PutterTrack not reachable"); yield break; }

            // Ask the view to place it, exactly as it does when the track comes up in putt mode.
            _coneView.ApplyConfiguredGeometry();
            yield return null;

            float top    = CanvasY(track, 1f);
            float bottom = CanvasY(track, 0f);
            _json["putt_track_height"] = _coneView.PutterTrackHeightPx;
            _json["putt_track_top_y"]  = top;
            _json["putt_track_bottom_y"] = bottom;
            // Same shape as the cone's apex assertion: the claim is "it touches the ball", so the
            // test compares against the ball rather than against a y somebody wrote down.
            Assert("putt_track_top_touches_the_ball", Mathf.Abs(top - _ball.anchoredPosition.y) <= 1f,
                   $"track top {top:F2} vs ball {_ball.anchoredPosition.y:F2} — gap {_ball.anchoredPosition.y - top:F2}px");
            Assert("putt_track_bottom_1096",Mathf.Abs(bottom - (-1096f)) <= 2f, $"track bottom {bottom:F2}");
            Assert("putt_track_height_792", Mathf.Abs(_coneView.PutterTrackHeightPx - 792f) <= 0.5f,
                   $"track height {_coneView.PutterTrackHeightPx:F1} — the same 792 the cone drops");
        }

        IEnumerator SwitchAndCompare()
        {
            float flickBall = _ball.anchoredPosition.y;
            float flickPitch = Pitch(_aimCam);

            ControlSchemeService.Set(ControlScheme.Pendulum, "flick_shot_view acceptance");
            yield return new WaitForSecondsRealtime(1.5f);
            ReposeAimCamera();
            yield return null;
            float pendBall = _ball.anchoredPosition.y;
            float pendPitch = Pitch(_aimCam);

            ControlSchemeService.Set(ControlScheme.Flick, "flick_shot_view acceptance");
            yield return new WaitForSecondsRealtime(1.5f);
            ReposeAimCamera();
            yield return null;
            float backBall = _ball.anchoredPosition.y;
            float backPitch = Pitch(_aimCam);

            _json["switch_flick_ball_y"]    = flickBall;
            _json["switch_pendulum_ball_y"] = pendBall;
            _json["switch_back_ball_y"]     = backBall;
            _json["switch_flick_pitch"]     = flickPitch;
            _json["switch_pendulum_pitch"]  = pendPitch;
            _json["switch_back_pitch"]      = backPitch;

            Assert("switch_same_framing", Mathf.Abs(flickBall - pendBall) <= 0.5f,
                   $"Flick {flickBall:F2} vs Pendulum {pendBall:F2}");
            Assert("switch_no_camera_pop", Mathf.Abs(flickPitch - pendPitch) <= 0.05f,
                   $"pitch Flick {flickPitch:F3} vs Pendulum {pendPitch:F3}");
            Assert("switch_round_trip", Mathf.Abs(flickBall - backBall) <= 0.5f,
                   $"back to Flick {backBall:F2}");
        }

        void Fail(string why) { Note("ABORT", why); Write(); EditorApplication.ExitPlaymode(); }

        static string J(object v)
        {
            switch (v)
            {
                case null:   return "null";
                case bool b: return b ? "true" : "false";
                case float f: return float.IsNaN(f) ? "null" : f.ToString("F4", CultureInfo.InvariantCulture);
                case int i:  return i.ToString(CultureInfo.InvariantCulture);
                default:     return "\"" + v.ToString().Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
            }
        }

        void Write()
        {
            string dir = Path.Combine(FlickShotViewVerify.TaskDir, "evidence");
            Directory.CreateDirectory(dir);

            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine("  \"task\": \"flick_shot_view\",");
            sb.AppendLine("  \"measured\": {");
            sb.AppendLine(string.Join(",\n", _json.Select(kv => $"    \"{kv.Key}\": {J(kv.Value)}")));
            sb.AppendLine("  },");
            sb.AppendLine("  \"assertions\": [");
            sb.AppendLine(string.Join(",\n", _asserts.Select(a =>
                $"    {{ \"name\": \"{a.name}\", \"verdict\": \"{(a.pass ? "PASS" : "FAIL")}\", \"detail\": {J(a.detail)} }}")));
            sb.AppendLine("  ],");
            sb.AppendLine($"  \"fail_count\": {_asserts.Count(a => !a.pass)}");
            sb.AppendLine("}");
            File.WriteAllText(Path.Combine(dir, "flick_shot_view_invariants.json"), sb.ToString());

            var md = new StringBuilder();
            md.AppendLine("# flick_shot_view — live framing measurements");
            md.AppendLine();
            md.AppendLine("Booted ShellScene -> StartButton -> PLAY -> hole card. Every number read off");
            md.AppendLine("live rects in play mode; the BEFORE column is the same session with the ball");
            md.AppendLine("widget put back on viewport 0.5 and the aim camera re-posed.");
            md.AppendLine();
            md.AppendLine("| assertion | verdict | detail |");
            md.AppendLine("|---|---|---|");
            foreach (var a in _asserts) md.AppendLine($"| {a.name} | {(a.pass ? "PASS" : "**FAIL**")} | {a.detail} |");
            md.AppendLine();
            md.AppendLine("## Log");
            foreach (var l in _log) md.AppendLine("- " + l);
            File.WriteAllText(Path.Combine(dir, "flick_shot_view.md"), md.ToString());

            Debug.Log($"[FlickView] evidence written — fails={_asserts.Count(a => !a.pass)}");
        }
    }
}
#endif
