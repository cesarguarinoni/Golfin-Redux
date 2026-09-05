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
using Golfin.Gameplay.UI.Controls.Needle;
// The recorder's namespace is Golfin.Physics.Viewer.Editor (NOT ...Viewer.Bot.Editor, which is
// only its folder). Aliased so the call sites stay readable.
using Rec = Golfin.Physics.Viewer.Editor.BotVideoRecorder;

namespace Golfin.EditorTools.ShotUI
{
    /// <summary>
    /// Records ONE clip of the Needle / "Tap Timing" scheme played on a real hole, through the
    /// player's own entry points — boot → PLAY → hole card → the real Tap Timing segment in the
    /// in-game gear → real pointer events on the real club handle and the real tap catcher.
    /// Storyboard (SPEC §6): idle → pull to 120% → PERFECT → HOOK → SHANK.
    ///
    /// <para>SEPARATE FROM <see cref="NeedleSchemeVerify"/> ON PURPOSE. That bot is the GATE and
    /// takes stills mid-run; a <c>ScreenCapture</c> read during a recording is one of the two
    /// documented Y-flip triggers, and its pacing is optimised for assertions rather than for
    /// something a human watches. This one asserts nothing, captures nothing, and is paced to be
    /// legible: hold on idle, let the needle visibly sweep, let each ball come to rest.</para>
    ///
    /// <para>THE CAPTIONS ARE WRITTEN FROM WHAT ACTUALLY HAPPENED — the grade, the needle offset
    /// and the power are read off the driver AFTER each swing commits and stamped into the sidecar,
    /// so a caption cannot claim a PERFECT the scheme did not award.</para>
    ///
    /// <para>Menu: GOLFIN ▸ ShotUI ▸ Record Needle Scheme Video.</para>
    /// </summary>
    public static class NeedleSchemeVideo
    {
        const string ShellScenePath = "Assets/Scenes/ShellScene.unity";
        const string ArmedKey       = "NeedleSchemeVideo.Armed";
        public const string TaskDir  = "Docs/Specs/Active/scheme_needle";
        public static string VideosDir => TaskDir + "/videos";

        /// <summary>Watchdog budget. Five beats with real ball flight between them; the Pendulum's
        /// three needed 60. Still inside the 90 s ceiling the recorder documents for a SINGLE clip
        /// (the 2026-06-09 WindowServer reboot was cumulative multi-clip load, and this records
        /// exactly once).</summary>
        const int MaxSeconds = 80;

        [InitializeOnLoadMethod]
        static void Hook() => EditorApplication.playModeStateChanged += OnPlayModeChanged;

        [MenuItem("GOLFIN/ShotUI/Record Needle Scheme Video")]
        public static void Launch()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("[NeedleVid] already playing — stop first."); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            EditorSceneManager.OpenScene(ShellScenePath);
            Directory.CreateDirectory(VideosDir);

            string raw = Path.Combine(Directory.GetCurrentDirectory(), VideosDir, "needle_raw");
            Rec.CustomOutputPath = raw;
            Rec.MaxRecordSecondsSessionOverride = MaxSeconds;
            // Deferred, not Arm(): starting at EnteredPlayMode would record the boot transient,
            // which is the other documented Y-flip trigger.
            Rec.ArmDeferred();

            SessionState.SetBool(ArmedKey, true);
            EditorApplication.EnterPlaymode();
            Debug.Log("[NeedleVid] armed — entering play mode. Raw clip → " + raw + ".mp4");
        }

        static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode) return;
            if (!SessionState.GetBool(ArmedKey, false)) return;
            SessionState.SetBool(ArmedKey, false);
            Application.runInBackground = true;   // MANDATORY for MCP-driven runs
            PendulumSchemeVerify.ForceCaptureResolution();
            var host = new GameObject("[NeedleVideoBot]");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.AddComponent<NeedleVideoRunner>();
        }
    }

    public class NeedleVideoRunner : MonoBehaviour
    {
        const BindingFlags NP  = BindingFlags.NonPublic | BindingFlags.Instance;
        const BindingFlags ANY = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance;

        readonly List<string> _log = new List<string>();
        readonly List<(float start, float end, string text)> _caps = new List<(float, float, string)>();

        float _t0;
        float Now => Time.realtimeSinceStartup - _t0;

        void Note(string k, object v) { _log.Add($"{k}: {v}"); Debug.Log($"[NeedleVid] {k}: {v}"); }
        void Cap(float start, float end, string text)
        {
            _caps.Add((start, end, text));
            Debug.Log($"[NeedleVid] caption [{start:F1}-{end:F1}] {text.Replace("\n", " / ")}");
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
        NeedleSchemeDriver _driver;
        NeedleTapCatcher   _catcher;
        RectTransform      _canvasRt;
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

        void Tap()
        {
            var p  = ScreenAt(new Vector2(0f, BallY - 30f));
            var rr = new RaycastResult { module = _raycaster, screenPosition = p };
            var ped = new PointerEventData(EventSystem.current)
            { position = p, pointerId = 0, button = PointerEventData.InputButton.Left };
            ped.pointerPressRaycast = rr; ped.pointerCurrentRaycast = rr;
            ExecuteEvents.Execute(_catcher.gameObject, ped, ExecuteEvents.pointerDownHandler);
        }

        Vector2 ScreenAt(Vector2 canvasLocal)
            => RectTransformUtility.WorldToScreenPoint(_uiCam, _canvasRt.TransformPoint(canvasLocal));

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

        float BallY;

        /// <summary>Touch one, paced for a viewer: the club head pulled back over ~0.8 s and held
        /// so the rings and the closing target are readable, then released.</summary>
        IEnumerator PullAndRelease(float pullPx, float holdSeconds)
        {
            Vector2 top  = ScreenAt(new Vector2(0f, BallY - 30f));
            Vector2 hold = ScreenAt(new Vector2(0f, BallY - 30f - pullPx));
            Down(top);
            yield return null;
            for (int i = 1; i <= 24; i++) { Drag(Vector2.Lerp(top, hold, i / 24f)); yield return null; }
            for (float t = 0f; t < holdSeconds; t += Time.unscaledDeltaTime) { Drag(hold); yield return null; }
            Up();
            yield return null;
        }

        /// <summary>Touch two: watch the LIVE needle and tap when it reaches the given offset.
        /// A real reaction on the real catcher — nothing about the grade is forced.</summary>
        IEnumerator TapAt(float target, float timeout = 6f)
        {
            for (float t = 0f; t < timeout; t += Time.unscaledDeltaTime)
            {
                if (!_driver.IsNeedlePhase) yield break;
                if (_driver.NeedleOffset >= target) { Tap(); yield break; }
                yield return null;
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

            // ── pick Tap Timing through the REAL in-game segment ────────────────
            yield return ClickWhenPresent("SettingsButton", 15f);
            yield return new WaitForSecondsRealtime(1.5f);
            var modal = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
                            FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                        .FirstOrDefault(m => m.GetType().Name == "InGameSettingsModalController");
            Button seg = null;
            if (modal != null &&
                modal.GetType().GetField("schemeButtons", ANY)?.GetValue(modal) is Button[] segs &&
                segs.Length > 2) seg = segs[2];
            if (seg != null) { ClickReal(seg); Note("scheme_entry_point", "real widget onClick: schemeButtons[2] (" + seg.name + ")"); }
            else { ControlSchemeService.Set(ControlScheme.Needle, "settings"); Note("scheme_entry_point", "FALLBACK Set()"); }
            yield return new WaitForSecondsRealtime(1f);
            var close = FindButton("CloseButton") ?? FindButton("ResumeButton") ?? FindButton("BackButton");
            if (close != null) ClickReal(close);
            yield return new WaitForSecondsRealtime(2.5f);

            _driver = UnityEngine.Object.FindObjectsByType<NeedleSchemeDriver>(
                          FindObjectsInactive.Exclude, FindObjectsSortMode.None).FirstOrDefault();
            if (_driver == null) { Note("ABORT", "Needle driver not live"); Finish(); yield break; }
            _catcher = FindAny("NeedleTapCatcher").GetComponent<NeedleTapCatcher>();

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
            Cap(s0, Now, "TAP TIMING — idle\nno cone and no arrows: the club and the ball");

            // Beat 1 — pull to 120%, holding so the rings, the overpower crescent and the closing
            // target are all readable, then let the needle run out: SHANK.
            s0 = Now;
            {
                Vector2 top  = ScreenAt(new Vector2(0f, BallY - 30f));
                Vector2 shal = ScreenAt(new Vector2(0f, BallY - 30f - _driver.Pull100Px * 0.25f));
                Vector2 full = ScreenAt(new Vector2(0f, BallY - 30f - _driver.Pull120Px));
                Down(top);
                yield return null;
                for (int i = 1; i <= 14; i++) { Drag(Vector2.Lerp(top, shal, i / 14f)); yield return null; }
                for (float t = 0f; t < 1.4f; t += Time.unscaledDeltaTime) { Drag(shal); yield return null; }
                float wide = _driver.PerfectZone01 * 90f;
                for (int i = 1; i <= 22; i++) { Drag(Vector2.Lerp(shal, full, i / 22f)); yield return null; }
                for (float t = 0f; t < 1.6f; t += Time.unscaledDeltaTime) { Drag(full); yield return null; }
                float narrow = _driver.PerfectZone01 * 90f;
                Note("perfect_zone_half_angle_deg", $"layup {wide:F1} -> 120pc {narrow:F1}");
                Cap(s0, Now, $"pull deeper and the target CLOSES\nblue zone {wide:F1} to {narrow:F1} degrees");
                Up();
                yield return null;
            }
            s0 = Now;
            Note("sweep_seconds_at_120", _driver.SweepSeconds.ToString("F2"));
            yield return new WaitForSecondsRealtime(_driver.SweepSeconds + 0.6f);
            Cap(s0, Now + 2.2f, GradeCaption("release, then DO NOT tap"));
            yield return new WaitForSecondsRealtime(1.6f);
            yield return WaitForIdle();
            yield return new WaitForSecondsRealtime(1f);

            // Beat 2 — a real PERFECT: tapped the moment the needle enters the blue.
            s0 = Now;
            yield return PullAndRelease(_driver.Pull100Px, 1.0f);
            Note("sweep_seconds_at_100", _driver.SweepSeconds.ToString("F2"));
            yield return TapAt(-_driver.PerfectZone01 * 0.5f);
            yield return new WaitForSecondsRealtime(0.4f);
            Cap(s0, Now + 2.2f, GradeCaption("tap on the blue"));
            yield return new WaitForSecondsRealtime(1.8f);
            yield return WaitForIdle();
            yield return new WaitForSecondsRealtime(1f);

            // Beat 3 — a real HOOK: tapped early, well left of the top.
            s0 = Now;
            yield return PullAndRelease(_driver.Pull100Px, 0.8f);
            yield return TapAt(-0.55f);
            yield return new WaitForSecondsRealtime(0.4f);
            Cap(s0, Now + 2.2f, GradeCaption("tap early, left of the top"));
            yield return new WaitForSecondsRealtime(1.8f);
            yield return WaitForIdle();
            yield return new WaitForSecondsRealtime(1.2f);

            Finish();
        }

        /// <summary>A caption that can only ever say what the driver actually awarded.</summary>
        string GradeCaption(string action)
        {
            float n = _driver.LastCommittedNeedle;
            string grade = _driver.LastCommittedGrade.ToString().ToUpperInvariant();
            Note("swing", $"grade={grade} n={n:F3} power={_driver.LastCommittedPower:F2} " +
                          $"timing01={_driver.LastCommittedTiming01:F3} mul={_driver.LastCommittedTimingMul:F2} " +
                          $"sweepSec={_driver.SweepSeconds:F2}");
            // ESCAPED percent. A bare % makes ffmpeg drawtext emit "Stray %" and render NOTHING for
            // the caption while the encode still reports success — escaped at the SOURCE so a
            // re-record cannot reintroduce it.
            return $"{action}\n{grade}  ·  power {_driver.LastCommittedPower * 100f:F0}\\%" +
                   $"  ·  timing {_driver.LastCommittedTiming01:F2}";
        }

        void Finish()
        {
            Rec.End();

            // Leave the pref as we found it.
            ControlSchemeService.Set(ControlScheme.Flick, "settings");
            PlayerPrefs.DeleteKey(ControlSchemeService.PrefKey);
            PlayerPrefs.Save();

            Directory.CreateDirectory(NeedleSchemeVideo.VideosDir);
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
            string path = Path.Combine(NeedleSchemeVideo.VideosDir, "needle_captions.json");
            File.WriteAllText(path, sb.ToString());
            Debug.Log($"[NeedleVid] captions sidecar → {path} ({_caps.Count} captions)");

            EditorApplication.ExitPlaymode();
        }
    }
}
#endif
