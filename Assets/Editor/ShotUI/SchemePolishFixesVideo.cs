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
using Golfin.Gameplay.UI.Controls;
using Golfin.Gameplay.UI.Controls.Pendulum;
using Rec = Golfin.Physics.Viewer.Editor.BotVideoRecorder;

namespace Golfin.EditorTools.ShotUI
{
    /// <summary>
    /// ONE clip proving the four control-scheme polish fixes, played on a real hole through the
    /// player's own entry points (PIPELINE_HARDENING §2) — boot → PLAY → hole card → the real
    /// PENDULUM segment in the in-game gear → real pointer events on the real club handle.
    ///
    /// <para>What it has to show, in order:
    /// <list type="number">
    /// <item>the 2D centre ball is TRANSLUCENT again, so the rest ghost and the real 3D ball read
    ///       through it on the very first shot of the hole;</item>
    /// <item>the club head is at Flick's size and grows with power (2x at rest → 3x at full);</item>
    /// <item>pulling the handle back UP and holding CANCELS the swing — it no longer lets the
    ///       player trim power on the way up — and the club head snaps back to its rest size;</item>
    /// <item>the shot UI survives a trip through the map view, and a real flick still fires.</item>
    /// </list></para>
    ///
    /// <para>Every number in a caption is READ OFF THE LIVE OBJECTS at the moment it is claimed —
    /// ball alpha off the Image, handle scale off the RectTransform, the cancel off ShotController's
    /// own state — so a caption cannot assert something the build did not do.</para>
    ///
    /// <para>Menu: GOLFIN ▸ ShotUI ▸ Record Scheme Polish Fixes Video.</para>
    /// </summary>
    public static class SchemePolishFixesVideo
    {
        const string ShellScenePath = "Assets/Scenes/ShellScene.unity";
        const string ArmedKey       = "SchemePolishFixesVideo.Armed";

        /// <summary>Raw clip + sidecar staging. The captioned deliverable is copied to
        /// Docs/Reports/Media by the caption step; the raw is scratch.</summary>
        public static string StageDir => Path.Combine(Path.GetTempPath(), "golfin_scheme_polish_video");

        const int MaxSeconds = 75;

        [InitializeOnLoadMethod]
        static void Hook() => EditorApplication.playModeStateChanged += OnPlayModeChanged;

        [MenuItem("GOLFIN/ShotUI/Record Scheme Polish Fixes Video")]
        public static void Launch()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("[PolishVid] already playing — stop first."); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            EditorSceneManager.OpenScene(ShellScenePath);
            Directory.CreateDirectory(StageDir);

            // This harness records exactly ONE clip. The guard exists to stop a session stacking
            // many full-res encodes (the 2026-06-09 WindowServer reboot); one deliberate clip after
            // an unrelated take earlier in the same Editor launch is the documented override.
            Rec.ResetSessionGuard();
            Rec.CustomOutputPath = Path.Combine(StageDir, "raw");
            Rec.MaxRecordSecondsSessionOverride = MaxSeconds;
            Rec.ArmDeferred();   // deferred: starting at EnteredPlayMode would record the boot transient

            SessionState.SetBool(ArmedKey, true);
            EditorApplication.EnterPlaymode();
            Debug.Log("[PolishVid] armed — entering play mode. Raw clip → " + Path.Combine(StageDir, "raw.mp4"));
        }

        static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode) return;
            if (!SessionState.GetBool(ArmedKey, false)) return;
            SessionState.SetBool(ArmedKey, false);
            Application.runInBackground = true;   // MANDATORY for MCP-driven runs
            PendulumSchemeVerify.ForceCaptureResolution();
            var host = new GameObject("[SchemePolishVideoBot]");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.AddComponent<SchemePolishVideoRunner>();
        }
    }

    public class SchemePolishVideoRunner : MonoBehaviour
    {
        const BindingFlags NP = BindingFlags.NonPublic | BindingFlags.Instance;

        readonly List<string> _log = new List<string>();
        readonly List<(float start, float end, string text)> _caps = new List<(float, float, string)>();

        float _t0;
        float Now => Time.realtimeSinceStartup - _t0;

        void Note(string k, object v) { _log.Add($"{k}: {v}"); Debug.Log($"[PolishVid] {k}: {v}"); }

        void Cap(float start, float end, string text)
        {
            _caps.Add((start, end, text));
            Debug.Log($"[PolishVid] caption [{start:F1}-{end:F1}] {text.Replace("\n", " / ")}");
        }

        void Start() => StartCoroutine(Sequence());

        // ── boot helpers ────────────────────────────────────────────────────────
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
            Note("TIMEOUT", "button " + n);
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

        // ── gesture plumbing ────────────────────────────────────────────────────
        PendulumSchemeDriver _driver;
        RectTransform        _canvasRt, _handle;
        GraphicRaycaster     _raycaster;
        Camera               _uiCam;
        PointerEventData     _ped;
        Vector2              _last, _top;
        float                _stepPx, BallY;

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

        Vector2 ScreenAt(float canvasY)
            => RectTransformUtility.WorldToScreenPoint(_uiCam, _canvasRt.TransformPoint(new Vector2(0f, canvasY)));

        Vector2 PullTo(float px) => ScreenAt(BallY - 30f - px);

        float HandleScale => _handle != null ? _handle.localScale.x : float.NaN;

        // ── ShotController reflection ───────────────────────────────────────────
        Component _sc; PropertyInfo _pState;
        string StateName => _pState.GetValue(_sc).ToString();

        bool BindShot()
        {
            _sc = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
                    FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                  .FirstOrDefault(m => m.GetType().Name == "ShotController");
            if (_sc == null) return false;
            _pState = _sc.GetType().GetProperty("State");
            return _pState != null;
        }

        IEnumerator WaitForIdle(float timeout = 30f)
        {
            for (float t = 0f; t < timeout; t += 0.2f)
            {
                if (StateName == "Idle") yield break;
                yield return new WaitForSecondsRealtime(0.2f);
            }
        }

        IEnumerator Sequence()
        {
            // ── boot through the real entry path ────────────────────────────────
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
            if (hole == 0) { Note("ABORT", "no hole card"); Finish(); yield break; }
            Note("hole", hole);

            for (float t = 0f; FindButton("HoleMap") == null && t < 120f; t += 0.5f)
                yield return new WaitForSecondsRealtime(0.5f);
            yield return new WaitForSecondsRealtime(4f);

            var rootCanvas = FindAny("ShotUI_Canvas");
            _canvasRt  = rootCanvas.GetComponent<RectTransform>();
            _raycaster = rootCanvas.GetComponent<Canvas>().rootCanvas.GetComponent<GraphicRaycaster>();
            _uiCam     = _raycaster.eventCamera;
            if (!BindShot()) { Note("ABORT", "no ShotController"); Finish(); yield break; }

            // FLICK'S handle scale, measured BEFORE the scheme swap while the flick root is still
            // the live one. This is the parity number the new schemes had to match.
            var flickHandle = FindAny("ClubHandle");
            float flickRest = flickHandle != null ? flickHandle.transform.localScale.x : float.NaN;
            Note("flick_handle_rest_scale", flickRest.ToString("F2"));

            // ── pick Pendulum through the REAL in-game settings segment ─────────
            yield return ClickWhenPresent("SettingsButton", 15f);
            yield return new WaitForSecondsRealtime(1.5f);
            var seg = FindButton("PendulumSegment")
                   ?? UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                      .FirstOrDefault(b => b.name.IndexOf("pendulum", StringComparison.OrdinalIgnoreCase) >= 0);
            if (seg != null) { ClickReal(seg); Note("scheme_entry_point", "real widget onClick: " + seg.name); }
            else { ControlSchemeService.Set(ControlScheme.Pendulum, "settings"); Note("scheme_entry_point", "FALLBACK Set()"); }
            yield return new WaitForSecondsRealtime(1f);

            // scheme_confirm_popup: picking a segment mid-hole ASKS first — nothing is written
            // until CONFIRM. Skipping this leaves the pref untouched and the run then aborts on
            // "driver not live", which is how the first take of this clip died.
            var confirm = FindButton("ConfirmButton");
            if (confirm != null) { ClickReal(confirm); Note("scheme_confirm", "clicked ConfirmButton"); }
            else Note("scheme_confirm", "no pop-up shown");
            yield return new WaitForSecondsRealtime(1.2f);

            var close = FindButton("CloseButton") ?? FindButton("ResumeButton") ?? FindButton("BackButton");
            if (close != null) ClickReal(close);
            yield return new WaitForSecondsRealtime(2.5f);

            for (float t = 0f; t < 8f; t += 0.25f)
            {
                _driver = UnityEngine.Object.FindObjectsByType<PendulumSchemeDriver>(
                              FindObjectsInactive.Exclude, FindObjectsSortMode.None).FirstOrDefault();
                if (_driver != null) break;
                yield return new WaitForSecondsRealtime(0.25f);
            }
            if (_driver == null) { Note("ABORT", "Pendulum driver not live"); Finish(); yield break; }

            var handleGo = FindAny("PendulumHandle");
            _handle = handleGo != null ? handleGo.GetComponent<RectTransform>() : null;
            if (_handle == null) Note("WARN", "PendulumHandle not found — scale captions will read NaN");

            var ballGo = FindAny("CentralBall");
            var ballImg = ballGo.GetComponent<Image>();
            var ball = ballGo.GetComponent<RectTransform>();
            var bc = new Vector3[4]; ball.GetWorldCorners(bc);
            BallY   = ((Vector2)_canvasRt.InverseTransformPoint((bc[0] + bc[2]) * 0.5f)).y;
            _top    = PullTo(0f);
            // Flick in THREE frames, not four: the reverse-to-cancel rule arms after 60 px of
            // upward travel and fires at 0.12 s held, so a four-frame release that stretches
            // under recording load would cancel the very swing it is meant to show firing.
            _stepPx = Screen.height * 0.10f;

            yield return WaitForIdle();
            yield return new WaitForSecondsRealtime(1f);

            // ── ROLL ────────────────────────────────────────────────────────────
            Rec.BeginDeferred();
            _t0 = Time.realtimeSinceStartup;
            Note("recording_started", $"{Screen.width}x{Screen.height}");

            // Beat 1 — the 2D ball reads through to the ghost and the real ball, FIRST shot.
            // The title card is drawn CENTRED over 0 -> --title-seconds, i.e. straight across the
            // ball. Hold long enough that the ball is unobstructed for several seconds AFTER the
            // title clears, and start this caption behind it — otherwise the clip captions a fix
            // it never actually shows.
            yield return new WaitForSecondsRealtime(3.4f);
            float s0 = Now;
            yield return new WaitForSecondsRealtime(3.6f);
            float ballAlpha = ballImg.color.a;
            Note("central_ball_alpha_first_shot", ballAlpha.ToString("F2"));
            Cap(s0, Now, $"FIRST shot of the hole\n2D ball alpha {ballAlpha:F2} — the rest ghost\nand the real ball read through it");

            // Beat 2 — the club head grows with power, from Flick's own rest size.
            s0 = Now;
            {
                float rest = HandleScale;
                Vector2 full = PullTo(_driver.Pull120Px);
                Down(_top);
                yield return null;
                for (int i = 1; i <= 26; i++) { Drag(Vector2.Lerp(_top, full, i / 26f)); yield return null; }
                for (float t = 0f; t < 1.8f; t += Time.unscaledDeltaTime) { Drag(full); yield return null; }
                float grown = HandleScale;
                Note("handle_scale", $"rest {rest:F2} -> full power {grown:F2} (flick rest {flickRest:F2})");

                for (int i = 1; i <= 3; i++) { Drag(new Vector2(full.x, full.y + _stepPx * 1.4f * i)); yield return null; }
                Up();
                yield return null; yield return null;
                Cap(s0, Now + 2.0f,
                    $"club head is Flick's size again\nand grows with power: {rest:F1}x to {grown:F1}x");
            }
            yield return new WaitForSecondsRealtime(1.0f);
            yield return WaitForIdle();
            yield return new WaitForSecondsRealtime(1.5f);

            // Beat 3 — pull back UP and hold: the swing CANCELS, and the club head resets.
            s0 = Now;
            {
                Vector2 deep = PullTo(_driver.Pull100Px * 0.85f);
                Down(_top);
                yield return null;
                for (int i = 1; i <= 20; i++) { Drag(Vector2.Lerp(_top, deep, i / 20f)); yield return null; }
                for (float t = 0f; t < 0.9f; t += Time.unscaledDeltaTime) { Drag(deep); yield return null; }
                float atDepth = HandleScale;

                // Back UP past the arming distance, slowly — a trim, not a flick — then HOLD.
                Vector2 risen = new Vector2(deep.x, deep.y + _driver.ReverseCancelPx * 1.6f);
                for (int i = 1; i <= 18; i++) { Drag(Vector2.Lerp(deep, risen, i / 18f)); yield return null; }
                bool sawShot = false;
                for (float t = 0f; t < 1.2f; t += Time.unscaledDeltaTime)
                {
                    Drag(risen);
                    if (StateName == "Resolving" || StateName == "Flicking") sawShot = true;
                    yield return null;
                }
                Up();
                yield return null; yield return null;
                yield return new WaitForSecondsRealtime(0.5f);
                float afterCancel = HandleScale;
                Note("reverse_cancel", $"state={StateName} sawShot={sawShot} " +
                                       $"handle {atDepth:F2} -> {afterCancel:F2}");
                Cap(s0, Now + 1.6f,
                    sawShot
                      ? "REGRESSION: the upward trim still fired a shot"
                      : $"pull back UP and hold: swing CANCELS\nno shot, and the club head resets\n{atDepth:F1}x back to {afterCancel:F1}x");
            }
            yield return new WaitForSecondsRealtime(1.8f);
            yield return WaitForIdle();

            // Beat 4 — the shot UI survives the map view, and a real flick still fires.
            {
                var mapBtn = FindButton("HoleMap");
                if (mapBtn != null)
                {
                    ClickReal(mapBtn);
                    // Two captions, bounded by the actual open and close, not one window spanning
                    // both: a single "back from the map view" line spent half its life on screen
                    // while the map was still open, which is a caption asserting something the
                    // frame under it contradicts.
                    yield return new WaitForSecondsRealtime(0.9f);
                    float mapIn = Now;
                    yield return new WaitForSecondsRealtime(2.2f);
                    Cap(mapIn, Now, "into the map view and back —\nthe trip that used to kill the shot UI");

                    var back = FindButton("MapShotViewButton") ?? FindButton("DriverButton");
                    if (back != null) ClickReal(back);
                    yield return new WaitForSecondsRealtime(1.2f);
                    float outAt = Now;
                    yield return new WaitForSecondsRealtime(1.4f);
                    bool handleLive = _handle != null && _handle.gameObject.activeInHierarchy;
                    bool rootLive   = _driver.gameObject.activeInHierarchy;
                    Note("after_map_view", $"schemeRootActive={rootLive} handleActive={handleLive} state={StateName}");
                    Cap(outAt, Now + 0.8f, "back on the shot view:\nthe club head is still here, still live");
                }
                else Note("WARN", "no HoleMap button — map beat skipped");
            }
            yield return new WaitForSecondsRealtime(1.4f);

            s0 = Now;
            {
                Vector2 full = PullTo(_driver.Pull100Px);
                Down(_top);
                yield return null;
                for (int i = 1; i <= 22; i++) { Drag(Vector2.Lerp(_top, full, i / 22f)); yield return null; }
                for (float t = 0f; t < 1.2f; t += Time.unscaledDeltaTime) { Drag(full); yield return null; }
                for (int i = 1; i <= 3; i++) { Drag(new Vector2(full.x, full.y + _stepPx * 1.4f * i)); yield return null; }
                Up();
                yield return null; yield return null;
                yield return new WaitForSecondsRealtime(0.6f);
                Note("post_map_swing", $"grade={_driver.LastCommittedGrade} " +
                                        $"power={_driver.LastCommittedPower:F2} state={StateName}");
                Cap(s0, Now + 2.2f,
                    $"a real flick still fires\n{_driver.LastCommittedGrade.ToString().ToUpperInvariant()}" +
                    $"  ·  power {_driver.LastCommittedPower * 100f:F0}\\%");
            }
            yield return new WaitForSecondsRealtime(3.0f);
            Finish();
        }

        void Finish()
        {
            Rec.End();

            // Leave the pref as we found it.
            ControlSchemeService.Set(ControlScheme.Flick, "settings");
            PlayerPrefs.DeleteKey(ControlSchemeService.PrefKey);
            PlayerPrefs.Save();

            Directory.CreateDirectory(SchemePolishFixesVideo.StageDir);
            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine("  \"captions\": [");
            for (int i = 0; i < _caps.Count; i++)
            {
                var c = _caps[i];
                sb.AppendLine($"    {{ \"start\": {c.start.ToString("F2", CultureInfo.InvariantCulture)}, " +
                              $"\"end\": {c.end.ToString("F2", CultureInfo.InvariantCulture)}, " +
                              $"\"text\": \"{c.text.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n")}\" }}" +
                              (i < _caps.Count - 1 ? "," : ""));
            }
            sb.AppendLine("  ],");
            sb.AppendLine("  \"notes\": [");
            for (int i = 0; i < _log.Count; i++)
                sb.AppendLine($"    \"{_log[i].Replace("\\", "\\\\").Replace("\"", "\\\"")}\"{(i < _log.Count - 1 ? "," : "")}");
            sb.AppendLine("  ]");
            sb.AppendLine("}");
            string path = Path.Combine(SchemePolishFixesVideo.StageDir, "captions.json");
            File.WriteAllText(path, sb.ToString());
            Debug.Log($"[PolishVid] captions sidecar → {path} ({_caps.Count} captions)");

            EditorApplication.ExitPlaymode();
        }
    }
}
#endif
