#if UNITY_EDITOR
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
using Golfin.Gameplay.UI.Controls.FreeSwing;
// The recorder's namespace is Golfin.Physics.Viewer.Editor (NOT ...Viewer.Bot.Editor, which is
// only its folder). Aliased so the call sites stay readable.
using Rec = Golfin.Physics.Viewer.Editor.BotVideoRecorder;

namespace Golfin.EditorTools.ShotUI
{
    /// <summary>
    /// Records ONE clip of the Free Swing scheme played on a real hole, through the player's own
    /// entry points — boot → PLAY → hole card → the real Free Swing segment in the in-game gear →
    /// real pointer events on the real club handle. Storyboard (SPEC §6): idle → backswing → PURE
    /// → SLICE → bowed DRAW → DUFF.
    ///
    /// <para>SEPARATE FROM <see cref="FreeSwingSchemeVerify"/> ON PURPOSE. That bot is the GATE and
    /// takes stills mid-run; a <c>ScreenCapture</c> read during a recording is one of the two
    /// documented Y-flip triggers, and its pacing is optimised for assertions rather than for
    /// something a human watches. This one asserts nothing, captures nothing, and is paced to be
    /// legible: hold on idle, let the backswing and the trace be readable, let each ball land.</para>
    ///
    /// <para>THE CAPTIONS ARE WRITTEN FROM WHAT ACTUALLY HAPPENED — the grade, the impact offset,
    /// the path and the tempo are read off the driver's committed verdict AFTER each swing and
    /// stamped into the sidecar, so a caption cannot claim a PURE the scheme did not award.</para>
    ///
    /// <para>Menu: GOLFIN ▸ ShotUI ▸ Record Free Swing Scheme Video.</para>
    /// </summary>
    public static class FreeSwingSchemeVideo
    {
        const string ShellScenePath = "Assets/Scenes/ShellScene.unity";
        const string ArmedKey       = "FreeSwingSchemeVideo.Armed";
        public const string TaskDir  = "Docs/Specs/Active/scheme_freeswing";
        public static string VideosDir => TaskDir + "/videos";

        /// <summary>Watchdog budget. Six beats with real ball flight between them, one of which is
        /// a deliberately SLOW duff. Still inside the 90 s ceiling the recorder documents for a
        /// SINGLE clip (the 2026-06-09 WindowServer reboot was cumulative multi-clip load, and this
        /// records exactly once).</summary>
        const int MaxSeconds = 88;

        [InitializeOnLoadMethod]
        static void Hook() => EditorApplication.playModeStateChanged += OnPlayModeChanged;

        [MenuItem("GOLFIN/ShotUI/Record Free Swing Scheme Video")]
        public static void Launch()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("[FreeSwingVid] already playing — stop first."); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            EditorSceneManager.OpenScene(ShellScenePath);
            Directory.CreateDirectory(VideosDir);

            string raw = Path.Combine(Directory.GetCurrentDirectory(), VideosDir, "freeswing_raw");
            Rec.CustomOutputPath = raw;
            Rec.MaxRecordSecondsSessionOverride = MaxSeconds;
            // Deferred, not Arm(): starting at EnteredPlayMode would record the boot transient,
            // which is the other documented Y-flip trigger.
            Rec.ArmDeferred();

            SessionState.SetBool(ArmedKey, true);
            EditorApplication.EnterPlaymode();
            Debug.Log("[FreeSwingVid] armed — entering play mode. Raw clip → " + raw + ".mp4");
        }

        static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode) return;
            if (!SessionState.GetBool(ArmedKey, false)) return;
            SessionState.SetBool(ArmedKey, false);
            Application.runInBackground = true;   // MANDATORY for MCP-driven runs
            PendulumSchemeVerify.ForceCaptureResolution();
            var host = new GameObject("[FreeSwingVideoBot]");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.AddComponent<FreeSwingVideoRunner>();
        }
    }

    public class FreeSwingVideoRunner : MonoBehaviour
    {
        const BindingFlags NP  = BindingFlags.NonPublic | BindingFlags.Instance;
        const BindingFlags ANY = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance;

        readonly List<string> _log = new List<string>();
        readonly List<(float start, float end, string text)> _caps = new List<(float, float, string)>();

        float _t0;
        float Now => Time.realtimeSinceStartup - _t0;

        void Note(string k, object v) { _log.Add($"{k}: {v}"); Debug.Log($"[FreeSwingVid] {k}: {v}"); }
        void Cap(float start, float end, string text)
        {
            _caps.Add((start, end, text));
            Debug.Log($"[FreeSwingVid] caption [{start:F1}-{end:F1}] {text.Replace("\n", " / ")}");
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

        // ── gesture ─────────────────────────────────────────────────────────────
        FreeSwingSchemeDriver _driver;
        FreeSwingLaneView     _lane;
        RectTransform         _canvasRt;
        GraphicRaycaster      _raycaster;
        Camera                _uiCam;
        PointerEventData      _ped;
        Vector2               _last;
        float                 BallY;

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

        Vector2 ScreenAt(Vector2 canvasLocal)
            => RectTransformUtility.WorldToScreenPoint(_uiCam, _canvasRt.TransformPoint(canvasLocal));

        Vector2 OriginLocal => new Vector2(0f, BallY - _lane.HandleRestBelowBall + 20f);
        Vector2 At(float dx, float dy) => ScreenAt(OriginLocal + new Vector2(dx, dy));

        Component _sc; PropertyInfo _pState;
        string StateName => _pState.GetValue(_sc).ToString();

        IEnumerator WaitForIdle(float timeout = 30f)
        {
            for (float t = 0f; t < timeout; t += 0.25f)
            {
                if (StateName == "Idle") yield break;
                yield return new WaitForSecondsRealtime(0.25f);
            }
            Note("TIMEOUT", "waiting for Idle");
        }

        /// <summary>
        /// ONE continuous swing, paced to be WATCHED rather than to be asserted: the backswing is
        /// slow enough to read the ticks and the trace, there is a beat at the bottom, and the
        /// upstroke is a real move through the impact line. The shot fires inside this routine.
        /// </summary>
        IEnumerator Swing(float pullPx, float backSeconds = 0.7f, float upSeconds = 0.35f,
                          float crossX = 0f, float bowPx = 0f, float holdAtBottom = 0.5f)
        {
            Down(At(0f, 0f));
            yield return null;

            // RAMPED BY WALL CLOCK, NOT BY FRAME COUNT. The recorder pins the Game View to 30 fps
            // while it is rolling, so `upSeconds * 60` frames take TWICE upSeconds of real time —
            // and this scheme's duff threshold is px per SECOND. The first cut of this clip swung
            // 520 px in 0.6 s instead of 0.3 s, measured 753 px/s against a 900 px/s floor, and
            // the driver correctly called three of the four swings DUFF. The captions said so,
            // which is how it was caught. Driving the ramp off unscaledTime makes the gesture take
            // the time it says it takes at any frame rate, which is also what a thumb does.
            for (float t0 = Time.unscaledTime; ; )
            {
                float k = Mathf.Clamp01((Time.unscaledTime - t0) / Mathf.Max(backSeconds, 1e-3f));
                Drag(At(0f, -pullPx * k));
                yield return null;
                if (k >= 1f) break;
            }
            for (float t = 0f; t < holdAtBottom; t += Time.unscaledDeltaTime)
            { Drag(At(0f, -pullPx)); yield return null; }

            float endDy = _lane.ImpactCrossOffsetPx + 0.5f;
            for (float t0 = Time.unscaledTime; ; )
            {
                float k = Mathf.Clamp01((Time.unscaledTime - t0) / Mathf.Max(upSeconds, 1e-3f));
                Drag(At(crossX * k + bowPx * Mathf.Sin(k * Mathf.PI), Mathf.Lerp(-pullPx, endDy, k)));
                yield return null;
                if (k >= 1f) break;
            }
            Up();
        }

        IEnumerator Sequence()
        {
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
            Note("hole", hole);

            for (float t = 0f; FindButton("HoleMap") == null && t < 120f; t += 0.5f)
                yield return new WaitForSecondsRealtime(0.5f);
            yield return new WaitForSecondsRealtime(4f);

            PendulumSchemeVerify.ForceCaptureResolution();
            yield return null; yield return null;

            var rootCanvas = FindAny("ShotUI_Canvas");
            _canvasRt  = rootCanvas.GetComponent<RectTransform>();
            _raycaster = rootCanvas.GetComponent<Canvas>().rootCanvas.GetComponent<GraphicRaycaster>();
            _uiCam     = _raycaster.eventCamera;

            _sc = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
                      FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                  .FirstOrDefault(m => m.GetType().Name == "ShotController");
            _pState = _sc?.GetType().GetProperty("State");
            if (_pState == null) { Note("ABORT", "ShotController not reachable"); Finish(); yield break; }

            // ── pick Free Swing through the REAL in-game segment ────────────────
            yield return ClickWhenPresent("SettingsButton", 15f);
            yield return new WaitForSecondsRealtime(1.5f);
            var modal = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
                            FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                        .FirstOrDefault(m => m.GetType().Name == "InGameSettingsModalController");
            Button seg = null;
            if (modal != null &&
                modal.GetType().GetField("schemeButtons", ANY)?.GetValue(modal) is Button[] segs &&
                segs.Length > 3) seg = segs[3];
            if (seg != null) { ClickReal(seg); Note("scheme_entry_point", "real widget onClick: schemeButtons[3] (" + seg.name + ")"); }
            else { ControlSchemeService.Set(ControlScheme.FreeSwing, "settings"); Note("scheme_entry_point", "FALLBACK Set()"); }
            yield return new WaitForSecondsRealtime(1f);
            var close = FindButton("CloseButton") ?? FindButton("ResumeButton") ?? FindButton("BackButton");
            if (close != null) ClickReal(close);
            yield return new WaitForSecondsRealtime(2.5f);

            _driver = UnityEngine.Object.FindObjectsByType<FreeSwingSchemeDriver>(
                          FindObjectsInactive.Exclude, FindObjectsSortMode.None).FirstOrDefault();
            if (_driver == null) { Note("ABORT", "Free Swing driver not live"); Finish(); yield break; }
            _lane = FindAny("FreeSwingLaneRoot").GetComponent<FreeSwingLaneView>();

            var ball = FindAny("CentralBall").GetComponent<RectTransform>();
            var bc = new Vector3[4]; ball.GetWorldCorners(bc);
            BallY = ((Vector2)_canvasRt.InverseTransformPoint((bc[0] + bc[2]) * 0.5f)).y;

            yield return WaitForIdle();
            yield return new WaitForSecondsRealtime(1f);

            // ── ROLL ────────────────────────────────────────────────────────────
            Rec.BeginDeferred();
            _t0 = Time.realtimeSinceStartup;
            Note("recording_started", $"{Screen.width}x{Screen.height}");

            float s0 = Now;
            yield return new WaitForSecondsRealtime(2.5f);
            Cap(s0, Now, "FREE SWING — idle\none drag: down for power, then back up");

            // Beat 1 — a slow readable BACKSWING, and nothing else: hold at a lay-up, then pull
            // to 120% so the ticks, the trace and the CLOSING impact window are all legible, then
            // LIFT. No shot. It is its own beat because it cannot also be the PURE beat: the holds
            // that make the window readable are backswing SECONDS, and a 4 s backswing under a
            // 0.35 s upswing is a tempo ratio of 0.09, which the scheme correctly grades FAST. The
            // first cut of this video captioned that swing "PURE" and the driver had graded it
            // None — the captions are written from the verdict, so it said so.
            s0 = Now;
            {
                Down(At(0f, 0f));
                yield return null;
                float shallow = _driver.Pull100Px * 0.25f;
                for (float t0 = Time.unscaledTime; ; )
                {
                    float k = Mathf.Clamp01((Time.unscaledTime - t0) / 0.4f);
                    Drag(At(0f, -shallow * k)); yield return null; if (k >= 1f) break;
                }
                for (float t = 0f; t < 1.2f; t += Time.unscaledDeltaTime) { Drag(At(0f, -shallow)); yield return null; }
                float wide = _driver.ImpactWindowPx * 2f;
                for (float t0 = Time.unscaledTime; ; )
                {
                    float k = Mathf.Clamp01((Time.unscaledTime - t0) / 0.9f);
                    Drag(At(0f, -Mathf.Lerp(shallow, _driver.Pull120Px, k))); yield return null; if (k >= 1f) break;
                }
                for (float t = 0f; t < 1.6f; t += Time.unscaledDeltaTime) { Drag(At(0f, -_driver.Pull120Px)); yield return null; }
                float narrow = _driver.ImpactWindowPx * 2f;
                Note("impact_window_width_px", $"layup {wide:F0} -> 120pc {narrow:F0}");
                // ESCAPED percent. A bare % makes ffmpeg drawtext emit "Stray %" and render NOTHING
                // for the caption while the encode still reports success — escaped at the SOURCE so
                // a re-record cannot reintroduce it.
                Cap(s0, Now + 1.0f, $"pull deeper and the target CLOSES\ngreen window {wide:F0} to {narrow:F0} px");
                Up();   // lift without crossing: nothing fires
            }
            yield return new WaitForSecondsRealtime(1.2f);

            // Beat 2 — a properly paced swing: 0.6 s down, 0.3 s up, straight through the line.
            // That is the scheme's ideal 2:1 tempo, so it is a PURE, and the caption says so
            // because the driver said so.
            s0 = Now;
            yield return Swing(_driver.Pull100Px, backSeconds: 0.6f, upSeconds: 0.3f, holdAtBottom: 0f);
            yield return new WaitForSecondsRealtime(0.4f);
            Cap(s0, Now + 2.4f, Caption("down, then straight back up"));
            yield return new WaitForSecondsRealtime(2.0f);
            yield return WaitForIdle();
            yield return new WaitForSecondsRealtime(1f);

            // Beat 3 — same tempo, crossing well RIGHT of centre: SLICE.
            s0 = Now;
            yield return Swing(_driver.Pull100Px, backSeconds: 0.6f, upSeconds: 0.3f,
                               crossX: 240f, holdAtBottom: 0f);
            yield return new WaitForSecondsRealtime(0.4f);
            Cap(s0, Now + 2.4f, Caption("cross right of the line"));
            yield return new WaitForSecondsRealtime(2.0f);
            yield return WaitForIdle();
            yield return new WaitForSecondsRealtime(1f);

            // Beat 4 — same tempo, a bowed upstroke: DRAW.
            s0 = Now;
            yield return Swing(_driver.Pull100Px, backSeconds: 0.6f, upSeconds: 0.3f,
                               bowPx: -320f, holdAtBottom: 0f);
            yield return new WaitForSecondsRealtime(0.4f);
            Cap(s0, Now + 2.4f, Caption("bow the upstroke left"));
            yield return new WaitForSecondsRealtime(2.0f);
            yield return WaitForIdle();
            yield return new WaitForSecondsRealtime(1f);

            // Beat 5 — creep the finger back up through the line: DUFF. Slow by TRAVELLING slowly
            // over the same distance, which is what the px/second threshold actually measures.
            s0 = Now;
            yield return Swing(_driver.Pull100Px, backSeconds: 0.5f, upSeconds: 1.8f, holdAtBottom: 0f);
            yield return new WaitForSecondsRealtime(0.4f);
            Cap(s0, Now + 2.4f, Caption("creep back up and it is a duff"));
            yield return new WaitForSecondsRealtime(2.0f);
            yield return WaitForIdle();
            yield return new WaitForSecondsRealtime(1.2f);

            Finish();
        }

        /// <summary>A caption that can only ever say what the driver actually committed.</summary>
        string Caption(string action)
        {
            var v = _driver.LastVerdict;
            string grade = v.Grade == FreeSwingGrade.None ? "" : "  ·  " + v.Grade.ToString().ToUpperInvariant();
            Note("swing", $"grade={v.Grade} impact={v.ImpactPx:F1}px window={v.ImpactWindowPx:F1} " +
                          $"path={v.PathDeg:F2}deg fadeDraw={v.FadeDraw01:F2} tempo={v.TempoRatio:F2} " +
                          $"speed={v.UpSpeedPxPerSec:F0}px/s power={v.PowerNormalized:F2} mul={v.TimingMul:F2}");
            // Two lines, each under 40 characters, and every percent escaped — the portrait caption
            // rules (a 79px default overflows 1170 wide, and a bare % renders NOTHING).
            return $"{action}{grade}\n" +
                   $"impact {v.ImpactPx:F0} px  ·  {v.Path}  ·  {v.Tempo}";
        }

        void Finish()
        {
            Rec.End();

            // Leave the pref as we found it.
            ControlSchemeService.Set(ControlScheme.Flick, "settings");
            PlayerPrefs.DeleteKey(ControlSchemeService.PrefKey);
            PlayerPrefs.Save();

            Directory.CreateDirectory(FreeSwingSchemeVideo.VideosDir);
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
            string path = Path.Combine(FreeSwingSchemeVideo.VideosDir, "freeswing_captions.json");
            File.WriteAllText(path, sb.ToString());
            Debug.Log($"[FreeSwingVid] captions sidecar → {path} ({_caps.Count} captions)");

            EditorApplication.ExitPlaymode();
        }
    }
}
#endif
