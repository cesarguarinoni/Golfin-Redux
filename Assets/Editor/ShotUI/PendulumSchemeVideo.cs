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
// The recorder's namespace is Golfin.Physics.Viewer.Editor (NOT ...Viewer.Bot.Editor,
// which is only its folder). Aliased so the call sites stay readable.
using Rec = Golfin.Physics.Viewer.Editor.BotVideoRecorder;

namespace Golfin.EditorTools.ShotUI
{
    /// <summary>
    /// Records ONE clip of the Pendulum scheme being played on a real hole, through the player's
    /// own entry points (PIPELINE_HARDENING §2) — boot → PLAY → hole card → the real PENDULUM
    /// segment in the in-game gear → real pointer events on the real club handle.
    ///
    /// <para>SEPARATE FROM <see cref="PendulumSchemeVerify"/> ON PURPOSE. That bot is the GATE and
    /// takes stills mid-run; a <c>ScreenCapture</c> read during a recording is one of the two
    /// documented Y-flip triggers, and its pacing is optimised for assertions rather than for
    /// something a human watches. This one asserts nothing, captures nothing, and is paced to be
    /// legible: hold on idle, let the marker visibly sweep, let each ball come to rest.</para>
    ///
    /// <para>THE CAPTIONS ARE WRITTEN FROM WHAT ACTUALLY HAPPENED — the grade, marker offset,
    /// power and Hz are read off the driver AFTER each swing commits and stamped into the sidecar,
    /// so a caption cannot claim a JUST the scheme did not award.</para>
    ///
    /// <para>Menu: GOLFIN ▸ ShotUI ▸ Record Pendulum Scheme Video.</para>
    /// </summary>
    public static class PendulumSchemeVideo
    {
        const string ShellScenePath = "Assets/Scenes/ShellScene.unity";
        const string ArmedKey       = "PendulumSchemeVideo.Armed";
        public const string TaskDir  = "Docs/Specs/Active/scheme_pendulum";
        public static string VideosDir => TaskDir + "/videos";

        /// <summary>Watchdog budget. The default cap is 30 s and this clip needs ~50: three swings
        /// with real ball flight between them. 60 is inside the 90 s ceiling the recorder documents
        /// for a SINGLE clip (the 2026-06-09 WindowServer reboot was cumulative multi-clip load,
        /// and this run records exactly once).</summary>
        const int MaxSeconds = 60;

        [InitializeOnLoadMethod]
        static void Hook() => EditorApplication.playModeStateChanged += OnPlayModeChanged;

        [MenuItem("GOLFIN/ShotUI/Record Pendulum Scheme Video")]
        public static void Launch()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("[PendulumVid] already playing — stop first."); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            EditorSceneManager.OpenScene(ShellScenePath);
            Directory.CreateDirectory(VideosDir);

            string raw = Path.Combine(Directory.GetCurrentDirectory(), VideosDir, "pendulum_raw");
            Rec.CustomOutputPath = raw;
            Rec.MaxRecordSecondsSessionOverride = MaxSeconds;
            // Deferred, not Arm(): starting at EnteredPlayMode would record the boot transient,
            // which is the other documented Y-flip trigger.
            Rec.ArmDeferred();

            SessionState.SetBool(ArmedKey, true);
            EditorApplication.EnterPlaymode();
            Debug.Log("[PendulumVid] armed — entering play mode. Raw clip → " + raw + ".mp4");
        }

        static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode) return;
            if (!SessionState.GetBool(ArmedKey, false)) return;
            SessionState.SetBool(ArmedKey, false);
            Application.runInBackground = true;   // MANDATORY for MCP-driven runs
            PendulumSchemeVerify.ForceCaptureResolution();
            var host = new GameObject("[PendulumVideoBot]");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.AddComponent<PendulumVideoRunner>();
        }
    }

    public class PendulumVideoRunner : MonoBehaviour
    {
        const BindingFlags NP = BindingFlags.NonPublic | BindingFlags.Instance;

        readonly List<string> _log = new List<string>();
        readonly List<(float start, float end, string text)> _caps =
            new List<(float, float, string)>();

        float _t0;                       // Time.realtimeSinceStartup at BeginDeferred
        float Now => Time.realtimeSinceStartup - _t0;

        void Note(string k, object v) { _log.Add($"{k}: {v}"); Debug.Log($"[PendulumVid] {k}: {v}"); }
        void Cap(float start, float end, string text)
        {
            _caps.Add((start, end, text));
            Debug.Log($"[PendulumVid] caption [{start:F1}-{end:F1}] {text.Replace("\n", " / ")}");
        }

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
        PendulumSchemeDriver _driver;
        RectTransform        _canvasRt;
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

        int _rejects;
        void OnReject(float speed) { _rejects++; Note("FlickRejected", speed.ToString("F2")); }
        Delegate _rejectHandler;

        void HookRejects(bool on)
        {
            var ev = _sc.GetType().GetEvent("FlickRejected", BindingFlags.Public | BindingFlags.Static);
            if (ev == null) return;
            if (on)
            {
                _rejectHandler = Delegate.CreateDelegate(ev.EventHandlerType, this,
                                   GetType().GetMethod(nameof(OnReject), NP));
                ev.AddEventHandler(null, _rejectHandler);
            }
            else if (_rejectHandler != null) ev.RemoveEventHandler(null, _rejectHandler);
        }

        // ── the swing ───────────────────────────────────────────────────────────
        Vector2 _top, _hold;
        float   _stepPx;
        // Sweeps of marker travel between deciding to flick and the upswing latch firing.
        // Seeded from take 1, which flicked with lead 0.031 and latched at +0.184 — i.e. it was
        // asin(0.184)/2pi = 0.0296 short. Recording load lengthens the frame, so the lead measured
        // WITHOUT the encoder running is not the lead needed WITH it; this is the measured one.
        // Take 2 flicked with lead 0.031 at 1.82 Hz and latched 0.0296 short, i.e. ~33 ms of
        // latency under recording load. The marker is now 0.91 Hz, so the same 33 ms is half the
        // phase: 0.91 * 0.033 = 0.030.
        float   _leadPhase = 0.030f;

        /// <summary>
        /// One swing, paced for a viewer: pull over ~0.8 s, hold so the marker visibly sweeps,
        /// then a real up-flick. <paramref name="lead"/> starts the marker that far before centre —
        /// the human plays this by feel; a bot has to measure it, and the shot is still graded by
        /// the driver from wherever the marker actually was.
        /// </summary>
        IEnumerator Swing(float pullPx, float holdSeconds, float? lead, bool slowRelease)
        {
            Vector2 hold = ScreenAt(new Vector2(0f, BallY - 30f - pullPx));

            Down(_top);
            yield return null;
            for (int i = 1; i <= 24; i++) { Drag(Vector2.Lerp(_top, hold, i / 24f)); yield return null; }

            for (float t = 0f; t < holdSeconds; t += Time.unscaledDeltaTime)
            { Drag(hold); yield return null; }

            if (lead.HasValue) { _driver.SetPhaseForTests(-lead.Value); Drag(hold); yield return null; }

            if (slowRelease)
            {
                // A real slow lift: one pixel a frame. Fails the flick gate on the measured speed.
                for (int i = 1; i <= 8; i++) { Drag(new Vector2(hold.x, hold.y + i)); yield return null; }
            }
            else
            {
                for (int i = 1; i <= 4; i++) { Drag(new Vector2(hold.x, hold.y + _stepPx * i)); yield return null; }
            }
            Up();
            yield return null; yield return null;
        }

        float BallY;

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
            HookRejects(true);

            // ── pick Pendulum through the REAL in-game settings segment ────────
            yield return ClickWhenPresent("SettingsButton", 15f);
            yield return new WaitForSecondsRealtime(1.5f);
            var seg = FindButton("PendulumSegment")
                   ?? UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                      .FirstOrDefault(b => b.name.IndexOf("pendulum", StringComparison.OrdinalIgnoreCase) >= 0);
            if (seg != null) { ClickReal(seg); Note("scheme_entry_point", "real widget onClick: " + seg.name); }
            else { ControlSchemeService.Set(ControlScheme.Pendulum, "settings"); Note("scheme_entry_point", "FALLBACK Set()"); }
            yield return new WaitForSecondsRealtime(1f);
            var close = FindButton("CloseButton") ?? FindButton("ResumeButton") ?? FindButton("BackButton");
            if (close != null) ClickReal(close);
            yield return new WaitForSecondsRealtime(2.5f);

            _driver = UnityEngine.Object.FindObjectsByType<PendulumSchemeDriver>(
                          FindObjectsInactive.Exclude, FindObjectsSortMode.None).FirstOrDefault();
            if (_driver == null) { Note("ABORT", "Pendulum driver not live"); Finish(); yield break; }

            var ball = FindAny("CentralBall").GetComponent<RectTransform>();
            var bc = new Vector3[4]; ball.GetWorldCorners(bc);
            BallY   = ((Vector2)_canvasRt.InverseTransformPoint((bc[0] + bc[2]) * 0.5f)).y;
            _top    = ScreenAt(new Vector2(0f, BallY - 30f));
            _stepPx = Screen.height * 0.10f;

            yield return WaitForIdle();
            yield return new WaitForSecondsRealtime(1f);

            // ── ROLL ────────────────────────────────────────────────────────────
            Rec.BeginDeferred();
            _t0 = Time.realtimeSinceStartup;
            Note("recording_started", $"{Screen.width}x{Screen.height}");

            float s0 = Now;
            yield return new WaitForSecondsRealtime(2.5f);
            Cap(s0, Now, "PENDULUM — idle\nno cone, no timing arrows: just the ball and the club head");

            // Beat 1 — the target closes as the pull deepens. Ease to a lay-up, hold so the wide
            // green band is readable, then pull through to 120% and hold on the narrow one.
            s0 = Now;
            {
                // Derived from the config, not literals: the pull thresholds moved when the
                // pill was lengthened, and a hard-coded 360 quietly became 94% instead of 120%.
                Vector2 layUp = ScreenAt(new Vector2(0f, BallY - 30f - _driver.Pull100Px * 0.22f));
                Vector2 full  = ScreenAt(new Vector2(0f, BallY - 30f - _driver.Pull120Px));
                Down(_top);
                yield return null;
                for (int i = 1; i <= 12; i++) { Drag(Vector2.Lerp(_top, layUp, i / 12f)); yield return null; }
                for (float t = 0f; t < 1.6f; t += Time.unscaledDeltaTime) { Drag(layUp); yield return null; }
                float wide = FindAny("BandJust").GetComponent<RectTransform>().rect.width;
                for (int i = 1; i <= 20; i++) { Drag(Vector2.Lerp(layUp, full, i / 20f)); yield return null; }
                for (float t = 0f; t < 1.4f; t += Time.unscaledDeltaTime) { Drag(full); yield return null; }
                float narrow = FindAny("BandJust").GetComponent<RectTransform>().rect.width;
                Note("band_just_px", $"layup {wide:F0} -> 120pc {narrow:F0}");

                for (int i = 1; i <= 4; i++) { Drag(new Vector2(full.x, full.y + _stepPx * i)); yield return null; }
                Up();
                yield return null; yield return null;
                Cap(s0, Now + 2.0f, $"pull deeper and the target CLOSES\ngreen band {wide:F0} to {narrow:F0} px  —  " +
                                    GradeWord());
            }
            yield return new WaitForSecondsRealtime(0.8f);
            yield return WaitForIdle();
            yield return new WaitForSecondsRealtime(1f);

            // Swings 1..2 — aim for the pip, refitting the lead from what actually committed.
            // Two attempts fit the budget; if the first lands a JUST the second is skipped.
            for (int attempt = 1; attempt <= 2; attempt++)
            {
                s0 = Now;
                yield return Swing(_driver.Pull100Px, attempt == 1 ? 2.0f : 1.6f, _leadPhase, slowRelease: false);
                Cap(s0, Now + 2.0f, GradeCaption("pull to 100\\% and flick on the red pip"));
                bool just = _driver.LastCommittedGrade == PendulumGrade.Just;
                yield return new WaitForSecondsRealtime(1f);
                yield return WaitForIdle();
                yield return new WaitForSecondsRealtime(1f);
                if (just) break;
                _leadPhase += Mathf.Asin(Mathf.Clamp(_driver.LastCommittedMarker, -1f, 1f)) / (2f * Mathf.PI);
                Note("lead_refit", _leadPhase.ToString("F4"));
            }

            // Swing 2 — no lead: released wherever the marker happened to be.
            s0 = Now;
            yield return Swing(_driver.Pull100Px * 0.8f, 1.2f, null, slowRelease: true);
            yield return new WaitForSecondsRealtime(1.2f);
            Cap(s0, Now, "slow release — the gate rejects it\nswing resets, no shot taken");


            yield return new WaitForSecondsRealtime(1.2f);
            Finish();
        }

        /// <summary>Just the grade the driver awarded, for a caption that is mostly about
        /// something else.</summary>
        string GradeWord()
        {
            Note("swing", $"grade={_driver.LastCommittedGrade} marker={_driver.LastCommittedMarker:F3} " +
                          $"power={_driver.LastCommittedPower:F2} hz={_driver.MarkerHz:F2}");
            return _driver.LastCommittedGrade.ToString().ToUpperInvariant() +
                   $" at {_driver.LastCommittedPower * 100f:F0}\\% power";
        }

        /// <summary>A caption that can only ever say what the driver actually awarded.</summary>
        string GradeCaption(string action)
        {
            float m = _driver.LastCommittedMarker;
            string grade = _driver.LastCommittedGrade.ToString().ToUpperInvariant();
            Note("swing", $"grade={grade} marker={m:F3} power={_driver.LastCommittedPower:F2} " +
                          $"timing01={_driver.LastCommittedTiming01:F3} mul={_driver.LastCommittedTimingMul:F2} " +
                          $"hz={_driver.MarkerHz:F2} latched={_driver.LastCommittedMarkerWasLatched}");
            // ESCAPED percent. A bare % makes ffmpeg drawtext emit "Stray %" and render NOTHING
            // for the caption, while the encode still reports success — escape at the SOURCE so a
            // re-record cannot reintroduce it.
            return $"{action}\n{grade}  ·  power {_driver.LastCommittedPower * 100f:F0}\\%" +
                   $"  ·  timing {_driver.LastCommittedTiming01:F2}";
        }

        void Finish()
        {
            HookRejects(false);
            Rec.End();

            // Leave the pref as we found it.
            ControlSchemeService.Set(ControlScheme.Flick, "settings");
            PlayerPrefs.DeleteKey(ControlSchemeService.PrefKey);
            PlayerPrefs.Save();

            Directory.CreateDirectory(PendulumSchemeVideo.VideosDir);
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
            string path = Path.Combine(PendulumSchemeVideo.VideosDir, "pendulum_captions.json");
            File.WriteAllText(path, sb.ToString());
            Debug.Log($"[PendulumVid] captions sidecar → {path} ({_caps.Count} captions)");

            EditorApplication.ExitPlaymode();
        }
    }
}
#endif
