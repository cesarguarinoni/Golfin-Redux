#if UNITY_EDITOR
// Assets/Scripts/UI/Editor/GachaRevealDemoRecorder.cs
// gacha_reveal_animation — smoke evidence harness. Same shape as GachaDemoRecorder.cs
// (arm → EnterPlaymode → InitializeOnLoad hook starts the runner), driving the REAL entry
// points only: the splash StartButton, the bottom-nav Gacha button, and the banner card's
// own PullX1Button / PullX10Button.
//
// TWO PASSES, deliberately. Stills and the Recorder must not run in the same session: a
// ScreenCapture read mid-recording is one of the two documented Y-flip triggers, and
// CaptureCore.RecordingActive hard-blocks SnapPlayModeSafe while a clip is rolling.
//
//   GOLFIN > Gacha > Reveal — Stills Pass    (no Recorder; writes the 5 acceptance PNGs)
//   GOLFIN > Gacha > Reveal — Video Pass     (Recorder WITH audio; x10 end-to-end + a SKIP)
//
// TIMING COMES FROM THE SFX BUS, not from added instrumentation. Every timeline step already
// publishes an SfxId (BagDrop=A, BagShake=B, CardPop=C, CardLand=D, CardExit=F,
// RevealComplete=G), so subscribing to SfxBus.OnPlay yields the ordered trace the acceptance
// list asks for AND exact per-phase durations, with zero temporary Debug.Log in production code.
//
// Output:
//   stills   Docs/Diagnostics/_capture/*.png  (copied to the task's screenshots/ afterwards)
//   video    tasks/loop_v2_smoke_bot/gacha_reveal/video/raw.mp4
//   trace    tasks/loop_v2_smoke_bot/gacha_reveal/screenshots/sfx_trace.log

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Golfin.Audio.Events;
using Golfin.Diagnostics.Runtime;
using Golfin.Gameplay.UI.Quality;
using GolfinRedux.UI.Gacha;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Input;
using UnityEngine;
using UnityEngine.UI;

namespace Golfin.EditorTools
{
    public static class GachaRevealDemoRecorder
    {
        const string RawDir   = "tasks/loop_v2_smoke_bot/gacha_reveal/video";
        const string LogDir   = "tasks/loop_v2_smoke_bot/gacha_reveal/screenshots";
        const string ArmedKey = "GachaRevealDemoRecorder.Mode";   // "" | "stills" | "video"

        static RecorderController _recorder;

        [InitializeOnLoadMethod]
        static void RegisterHook()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        [MenuItem("GOLFIN/Gacha/Reveal — Stills Pass")]
        public static void LaunchStills() => Launch("stills");

        [MenuItem("GOLFIN/Gacha/Reveal — Video Pass")]
        public static void LaunchVideo() => Launch("video");

        [MenuItem("GOLFIN/Gacha/Reveal — Timing Pass (High vs Low)")]
        public static void LaunchTiming() => Launch("timing");

        [MenuItem("GOLFIN/Gacha/Reveal — FX Debug Pass")]
        public static void LaunchFx() => Launch("fx");

        static void Launch(string mode)
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[GachaReveal] Already in play mode — stop first.");
                return;
            }
            Directory.CreateDirectory(RawDir);
            Directory.CreateDirectory(LogDir);
            File.WriteAllText(Path.Combine(LogDir, "sfx_trace.log"), "");
            // Frames must keep coming while the Editor is unfocused, or a capture returns the
            // wrong frame and SnapPlayModeSafe reports a path it never wrote.
            Application.runInBackground = true;
            SessionState.SetString(ArmedKey, mode);
            EditorApplication.EnterPlaymode();
            Debug.Log($"[GachaReveal] Armed ({mode}). Entering play mode...");
        }

        static void OnPlayModeChanged(PlayModeStateChange state)
        {
            var mode = SessionState.GetString(ArmedKey, "");
            if (string.IsNullOrEmpty(mode)) return;

            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                SessionState.SetString(ArmedKey, "");
                if (mode == "video") StartRecorder();
                var host = new GameObject("[GachaRevealBot]");
                UnityEngine.Object.DontDestroyOnLoad(host);
                host.AddComponent<GachaRevealDemoRunner>().Init(mode, LogDir);
            }
            else if (state == PlayModeStateChange.ExitingPlayMode)
            {
                StopRecorder();
            }
        }

        static bool TryEnsureIPhone14Selected()
        {
            try
            {
                var asm = Assembly.Load("Golfin.Physics.Viewer.Bot.Editor");
                var t   = asm?.GetType("Golfin.Physics.Viewer.Editor.GameViewSizeUtil");
                var m   = t?.GetMethod("EnsureIPhone14Selected", BindingFlags.Public | BindingFlags.Static);
                return m != null && (bool)m.Invoke(null, null);
            }
            catch { return false; }
        }

        static void StartRecorder()
        {
            bool pinned = TryEnsureIPhone14Selected();
            int w = 1170, h = 2532;
            if (!pinned)
            {
                PlayModeWindow.GetRenderingResolution(out uint cw, out uint ch);
                if (cw > 0 && ch > 0)
                {
                    w = (int)cw; h = (int)ch;
                    if (w % 2 != 0) w--; if (h % 2 != 0) h--;
                    Debug.LogWarning($"[GachaReveal] Could not pin iPhone-14 — recording at {w}x{h}.");
                }
            }

            var rawPath = Path.Combine(RawDir, "raw.mp4");
            if (File.Exists(rawPath)) File.Delete(rawPath);

            var movie = ScriptableObject.CreateInstance<MovieRecorderSettings>();
            movie.name                            = "GachaRevealDemo";
            movie.Enabled                         = true;
            movie.OutputFormat                    = MovieRecorderSettings.VideoRecorderOutputFormat.MP4;
            movie.ImageInputSettings              = new GameViewInputSettings { OutputWidth = w, OutputHeight = h };
            // The reveal is half sound design — this is the one demo recorder that keeps audio.
            movie.AudioInputSettings.PreserveAudio = true;
            movie.OutputFile                      = Path.Combine(RawDir, "raw");

            var settings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
            settings.AddRecorderSettings(movie);
            settings.SetRecordModeToManual();
            settings.FrameRate         = 30;
            settings.FrameRatePlayback = FrameRatePlayback.Variable;

            _recorder = new RecorderController(settings);
            _recorder.PrepareRecording();
            _recorder.StartRecording();
            CaptureCore.RecordingActive = true;     // hard-blocks stills for the whole clip
            Debug.Log($"[GachaReveal] Recording (audio ON) → {rawPath} ({w}x{h} @ 30fps)");
        }

        static void StopRecorder()
        {
            CaptureCore.RecordingActive = false;
            if (_recorder == null) return;
            try { if (_recorder.IsRecording()) _recorder.StopRecording(); Debug.Log("[GachaReveal] Recorder stopped."); }
            catch (Exception e) { Debug.LogWarning($"[GachaReveal] StopRecorder: {e.Message}"); }
            _recorder = null;
        }
    }

    // ── Runner ────────────────────────────────────────────────────────────────

    public class GachaRevealDemoRunner : MonoBehaviour
    {
        string _mode, _logDir, _tracePath;
        readonly List<string> _trace = new List<string>();
        float _t0;

        public void Init(string mode, string logDir)
        {
            _mode      = mode;
            _logDir    = logDir;
            _tracePath = Path.Combine(logDir, "sfx_trace.log");
            SfxBus.OnPlay += OnSfx;
            StartCoroutine(Sequence());
        }

        void OnDestroy() { SfxBus.OnPlay -= OnSfx; }

        // ── SFX trace = the phase timeline ────────────────────────────────────

        void OnSfx(SfxId id)
        {
            float t = Time.realtimeSinceStartup - _t0;
            string line = $"[t={t:F3}] {id}";
            _trace.Add(line);
            File.AppendAllText(_tracePath, line + "\n");
        }

        void Mark(string text)
        {
            float t = Time.realtimeSinceStartup - _t0;
            string line = $"[t={t:F3}] --- {text} ---";
            _trace.Add(line);
            File.AppendAllText(_tracePath, line + "\n");
            Debug.Log($"[GachaReveal]{line}");
        }

        int TraceMark() => _trace.Count;

        /// <summary>Realtime seconds between the first and last SFX since <paramref name="from"/>.</summary>
        float SpanSince(int from)
        {
            var slice = _trace.Skip(from).Where(l => !l.Contains("---")).ToList();
            if (slice.Count < 2) return -1f;
            return ParseT(slice.Last()) - ParseT(slice.First());
        }
        static float ParseT(string line)
        {
            int a = line.IndexOf('=') + 1, b = line.IndexOf(']');
            return float.Parse(line.Substring(a, b - a), System.Globalization.CultureInfo.InvariantCulture);
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        static T FindActive<T>() where T : Component =>
            Resources.FindObjectsOfTypeAll<T>()
                .FirstOrDefault(c => !string.IsNullOrEmpty(c.gameObject.scene.name) && c.gameObject.activeInHierarchy);

        static Button FindButton(string name) =>
            Resources.FindObjectsOfTypeAll<Button>().FirstOrDefault(b =>
                b.gameObject.name == name && !string.IsNullOrEmpty(b.gameObject.scene.name) && b.isActiveAndEnabled);

        static IEnumerator Wait(float s) { float t = 0f; while (t < s) { t += Time.unscaledDeltaTime; yield return null; } }

        // SnapAtEndOfFrameAndPause, NOT SnapPlayModeSafe: the latter calls
        // CaptureScreenshotAsTexture synchronously, which returns null with the Editor
        // unfocused and still logs a path it never wrote (the documented phantom-path trap).
        // Yielding WaitForEndOfFrame first is what makes the grab real. skipPause keeps the
        // runner's coroutine alive.
        IEnumerator Snap(string label)
        {
            if (_mode == "video") yield break;                  // never mid-recording
            string dest = $"Docs/Diagnostics/_capture/{label}.png";
            if (File.Exists(dest)) File.Delete(dest);
            yield return CaptureCore.SnapAtEndOfFrameAndPause(label, dest, skipPause: true);

            if (!File.Exists(dest)) { Debug.LogError($"[GachaReveal] SNAP FAILED '{label}' — no file at '{dest}'."); yield break; }
            long len = new FileInfo(dest).Length;
            if (len < 5000)        { Debug.LogError($"[GachaReveal] SNAP SUSPECT '{label}' — only {len} bytes."); yield break; }
            Debug.Log($"[GachaReveal] snapped '{label}' → {dest} ({len} bytes)");
        }

        /// <summary>
        /// What the scrim owes the player, measured rather than eyeballed: the top-most UI
        /// object under the bottom-nav Gacha button's centre. With the modal open this must be
        /// the modal's own scrim, never the nav button.
        /// </summary>
        string NavBarProbe()
        {
            var nav = Resources.FindObjectsOfTypeAll<Button>().FirstOrDefault(b =>
                b.gameObject.name == "NavGachaButton" && !string.IsNullOrEmpty(b.gameObject.scene.name));
            if (nav == null) return "NavGachaButton not found";

            var rt = (RectTransform)nav.transform;
            var corners = new Vector3[4]; rt.GetWorldCorners(corners);
            Vector3 centre = (corners[0] + corners[2]) * 0.5f;
            var cam = nav.GetComponentInParent<Canvas>()?.rootCanvas.worldCamera;
            Vector2 screen = cam != null ? (Vector2)cam.WorldToScreenPoint(centre) : (Vector2)centre;

            var ped = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current) { position = screen };
            var hits = new List<UnityEngine.EventSystems.RaycastResult>();
            UnityEngine.EventSystems.EventSystem.current.RaycastAll(ped, hits);
            string top = hits.Count > 0 ? hits[0].gameObject.name + " (" + PathOf(hits[0].gameObject.transform) + ")" : "<nothing>";
            return $"navBtnScreen={screen} topMostHit={top} navInteractable={nav.interactable}";
        }

        static string PathOf(Transform t) { var s = t.name; while (t.parent != null) { t = t.parent; s = t.name + "/" + s; } return s; }

        static GachaRevealModalController Modal => GachaRevealModalController.Instance;

        static string PhaseName()
        {
            var m = Modal;
            if (m == null) return "<no modal>";
            var f = typeof(GachaRevealModalController).GetField("_phase", BindingFlags.NonPublic | BindingFlags.Instance);
            return f == null ? "?" : f.GetValue(m).ToString();
        }

        /// <summary>Blocks until the modal reports <paramref name="phase"/>, or the timeout expires.</summary>
        IEnumerator WaitForPhase(string phase, float timeout = 20f)
        {
            float t = 0f;
            while (t < timeout && PhaseName() != phase) { t += Time.unscaledDeltaTime; yield return null; }
            if (PhaseName() != phase) Debug.LogWarning($"[GachaReveal] timed out waiting for phase '{phase}' (now '{PhaseName()}').");
        }

        /// <summary>Waits for the Nth Hold of the current reveal (0-based), so we can snap a chosen card.</summary>
        IEnumerator WaitForHoldIndex(int index, float timeout = 40f)
        {
            float t = 0f;
            int seen = -1; bool inHold = false;
            while (t < timeout)
            {
                bool nowHold = PhaseName() == "Hold";
                if (nowHold && !inHold) seen++;
                inHold = nowHold;
                if (nowHold && seen == index) yield break;
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            Debug.LogWarning($"[GachaReveal] timed out waiting for hold #{index}.");
        }

        // ── Sequence ─────────────────────────────────────────────────────────

        IEnumerator Sequence()
        {
            _t0 = Time.realtimeSinceStartup;

            // Phase 0 — boot through the REAL gate (splash StartButton), then wait for the nav bar.
            Golfin.UI.PersistentUIManager puim = null;
            float waited = 0f; bool tappedStart = false;
            while (waited < 45f)
            {
                puim = FindActive<Golfin.UI.PersistentUIManager>();
                if (puim != null && puim.inventoryButton != null && puim.inventoryButton.gameObject.activeInHierarchy) break;
                if (!tappedStart)
                {
                    var splash  = FindActive<GolfinRedux.UI.SplashScreenController>();
                    var startBtn = splash != null ? splash.transform.Find("StartButton")?.GetComponent<Button>() : null;
                    if (startBtn != null && startBtn.gameObject.activeInHierarchy)
                    {
                        startBtn.onClick.Invoke(); tappedStart = true;
                        Debug.Log("[GachaReveal] tapped the real StartButton.");
                    }
                }
                waited += Time.unscaledDeltaTime; yield return null;
            }
            if (puim == null)
            {
                Debug.LogError("[GachaReveal] BOOT GATE — never reached Home. Sign in once in the Editor and re-run.");
                EditorApplication.ExitPlaymode(); yield break;
            }
            yield return Wait(2f);

            // Rewards Center via the real bottom-nav button.
            Mark("nav → Rewards Center (GACHA)");
            var navGacha = FindButton("NavGachaButton");
            if (navGacha == null) { Debug.LogError("[GachaReveal] NavGachaButton not found."); EditorApplication.ExitPlaymode(); yield break; }
            navGacha.onClick.Invoke();
            yield return Wait(2.5f);

            if (Modal == null)
            {
                Debug.LogError("[GachaReveal] GachaRevealModalController.Instance is NULL — the scene instance is missing or inactive.");
                EditorApplication.ExitPlaymode(); yield break;
            }

            if (_mode == "stills")      yield return StillsPass();
            else if (_mode == "timing")  yield return TimingPass();
            else if (_mode == "fx")      yield return FxPass();
            else                         yield return VideoPass();

            // Nav-bar pixel sample is written by the stills pass; dump the trace either way.
            File.AppendAllText(_tracePath, "\n=== full trace ===\n" + string.Join("\n", _trace) + "\n");
            Debug.Log("[GachaReveal] Sequence complete. Exiting play mode.");
            yield return Wait(0.5f);
            EditorApplication.ExitPlaymode();
        }

        // 5 acceptance stills + the measured x1 timing + the SKIP / tap / Low-tier checks.
        IEnumerator StillsPass()
        {
            // ── (1) modal open, bag alone mid-shake — via PULL x1 ─────────────
            // A clean before-frame so the nav-bar dim can be measured, not eyeballed.
            Mark("before modal — " + NavBarProbe());
            yield return Snap("gacha_reveal_00_before_modal");

            Mark("PULL x1 (real banner-card button)");
            int m0 = TraceMark();
            var pullX1 = FindButton("PullX1Button");
            if (pullX1 == null) { Debug.LogError("[GachaReveal] PullX1Button not found."); yield break; }
            pullX1.onClick.Invoke();

            yield return WaitForPhase("Shake");
            yield return Wait(0.25f);                       // into the shake, before the card exists
            Mark("modal open — " + NavBarProbe());
            yield return Snap("gacha_reveal_01_bag_shake");

            yield return WaitForPhase("Hold");
            yield return Wait(0.35f);
            yield return Snap("gacha_reveal_03_legendary_hold");   // x1 marquee is the Legendary

            // Let it finish naturally → Prizes x1.
            yield return WaitForPhase("Idle", 30f);
            float x1Total = SpanSince(m0);
            Mark($"x1 Legendary total (BagDrop→RevealComplete) = {x1Total:F2}s");
            yield return Wait(1.2f);
            yield return Snap("gacha_reveal_04b_prizes_x1");

            // BACK to the Rewards Center.
            var back = FindButton("BackButton");
            if (back != null) back.onClick.Invoke();
            yield return Wait(2.0f);

            // ── (2) Common card hold + (4) Prizes x10 — via PULL x10 ─────────
            Mark("PULL x10 (real banner-card button)");
            int m1 = TraceMark();
            var pullX10 = FindButton("PullX10Button");
            if (pullX10 == null) { Debug.LogError("[GachaReveal] PullX10Button not found."); yield break; }
            pullX10.onClick.Invoke();

            // Pool order: 0 Legendary, 1 Mythic, 2-3 Rare, 4-7 Common, 8-9 Uncommon.
            yield return WaitForHoldIndex(4);
            yield return Wait(0.35f);
            yield return Snap("gacha_reveal_02_common_hold");

            yield return WaitForPhase("Idle", 90f);
            float x10Total = SpanSince(m1);
            Mark($"x10 total (BagDrop→RevealComplete) = {x10Total:F2}s");
            yield return Wait(1.2f);
            yield return Snap("gacha_reveal_04_prizes_x10");

            // ── SKIP at card 3 of 10, from the Prizes screen's PULL (pull again) ──
            Mark("PULL again on Prizes (x10) → SKIP at card 3");
            var pullAgain = FindButton("PullButton");
            if (pullAgain != null) pullAgain.onClick.Invoke(); else Debug.LogError("[GachaReveal] Prizes PullButton not found.");
            yield return WaitForHoldIndex(2);              // 0-based → the 3rd card
            yield return Wait(0.2f);
            var skip = FindButton("SkipButton");
            if (skip != null) skip.onClick.Invoke(); else Debug.LogError("[GachaReveal] SkipButton not found.");
            yield return Wait(1.5f);
            Mark("after SKIP — leftovers check: " + LeftoverReport());
            yield return Snap("gacha_reveal_05b_after_skip");

            // Pull again straight after, to prove nothing was inherited.
            Mark("pull again immediately after SKIP");
            pullAgain = FindButton("PullButton");
            if (pullAgain != null) pullAgain.onClick.Invoke();
            yield return WaitForPhase("Shake");
            yield return Wait(0.2f);
            Mark("first shake of the NEXT pull — leftovers: " + LeftoverReport());

            // ── tap during pop = no-op; tap during hold = ends it ─────────────
            yield return WaitForPhase("Pop");
            float holdLenBaseline = 0f;
            Modal.OnTapAnywhere();
            Mark("tapped during Pop (expected: no-op) phase=" + PhaseName());
            yield return WaitForPhase("Hold");
            float tHold = Time.realtimeSinceStartup;
            yield return Wait(0.30f);
            Modal.OnTapAnywhere();                          // fast-forward the hold
            yield return WaitForPhaseChangeFrom("Hold");
            holdLenBaseline = Time.realtimeSinceStartup - tHold;
            Mark($"tap during Hold ended it after {holdLenBaseline:F2}s (this card is the Legendary marquee — tier hold 2.00s)");

            var skip2 = FindButton("SkipButton");
            if (skip2 != null) skip2.onClick.Invoke();
            yield return Wait(1.5f);

            // ── (5) Low tier Legendary hold ──────────────────────────────────
            Mark("QualityTier → Low");
            QualityTierService.SetOverride((int)QualityTier.Low);
            yield return Wait(0.5f);

            back = FindButton("BackButton");
            if (back != null) back.onClick.Invoke();
            yield return Wait(2.0f);

            int m2 = TraceMark();
            var pullX1Low = FindButton("PullX1Button");
            if (pullX1Low != null) pullX1Low.onClick.Invoke();
            yield return WaitForPhase("Hold");
            yield return Wait(0.35f);
            yield return Snap("gacha_reveal_05_low_legendary_hold");
            yield return WaitForPhase("Idle", 30f);
            float x1LowTotal = SpanSince(m2);
            Mark($"x1 Legendary total on LOW = {x1LowTotal:F2}s (High was {x1Total:F2}s)");

            QualityTierService.SetOverride(QualityTierService.AutoPref);
            Mark("QualityTier restored to Auto");
            yield return Wait(1.0f);
        }

        IEnumerator WaitForPhaseChangeFrom(string phase, float timeout = 10f)
        {
            float t = 0f;
            while (t < timeout && PhaseName() == phase) { t += Time.unscaledDeltaTime; yield return null; }
        }

        /// <summary>What a "clean idle" audit sees: live card, running emitters, bag rotation.</summary>
        string LeftoverReport()
        {
            var m = Modal;
            if (m == null) return "<no modal>";
            var T = typeof(GachaRevealModalController);
            var card = T.GetField("_liveCard", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(m) as GameObject;
            string emitters = "";
            foreach (var n in new[] { "_bagMouthFx", "_cardBurstFx", "_cardIdleFx", "_cardRainFx" })
            {
                var ps = T.GetField(n, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(m) as ParticleSystem;
                if (ps != null && (ps.isEmitting || ps.particleCount > 0)) emitters += n + "(" + ps.particleCount + ") ";
            }
            var pivot = T.GetField("_bagPivot", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(m) as RectTransform;
            float z = pivot != null ? pivot.localEulerAngles.z : 0f;
            if (z > 180f) z -= 360f;
            return $"card={(card == null ? "none" : card.name)} liveEmitters=[{emitters}] bagRotZ={z:F2}";
        }

        // Short loop for iterating on the emitters: pull x1 (Legendary), dump every emitter's
        // live state at the burst and again during the hold, snap, done.
        IEnumerator FxPass()
        {
            var pull = FindButton("PullX1Button");
            if (pull == null) { Debug.LogError("[GachaReveal] PullX1Button not found."); yield break; }
            pull.onClick.Invoke();

            yield return WaitForPhase("Hold");
            yield return Wait(0.12f);
            Mark("FX @hold+0.12 " + FxReport());
            yield return Snap("gacha_fxdebug_hold_early");
            yield return Wait(0.5f);
            Mark("FX @hold+0.62 " + FxReport());
            yield return Snap("gacha_fxdebug_hold_late");
            yield return WaitForPhase("Idle", 30f);
            yield return Wait(1.0f);
        }

        string FxReport()
        {
            var m = Modal;
            if (m == null) return "<no modal>";
            var T = typeof(GachaRevealModalController);
            var sb = new System.Text.StringBuilder();
            foreach (var n in new[] { "_bagMouthFx", "_cardBurstFx", "_cardIdleFx", "_cardRainFx" })
            {
                var ps = T.GetField(n, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(m) as ParticleSystem;
                if (ps == null) { sb.Append($"\n  {n}: NULL"); continue; }
                var psr = ps.GetComponent<ParticleSystemRenderer>();
                var up  = ps.transform.parent != null
                        ? ps.transform.parent.GetComponent(Type.GetType("Coffee.UIExtensions.UIParticle, Coffee.UIParticle"))
                        : null;
                var main = ps.main; var sh = ps.shape;
                var buf = new ParticleSystem.Particle[Mathf.Max(1, ps.particleCount)];
                int got = ps.GetParticles(buf);
                string p0 = got > 0 ? buf[0].position.ToString("F2") + " v=" + buf[0].velocity.ToString("F2") : "-";
                string pN = got > 1 ? buf[got - 1].position.ToString("F2") : "-";
                sb.Append($"\n  {n}: count={ps.particleCount} emitting={ps.isEmitting} playing={ps.isPlaying}"
                        + $" mat={(psr.sharedMaterial != null ? psr.sharedMaterial.name : "NULL")}"
                        + $" tex={(psr.sharedMaterial != null && psr.sharedMaterial.mainTexture != null ? psr.sharedMaterial.mainTexture.name : "NULL")}"
                        + $" uiParticle={(up != null ? "yes scale=" + up.GetType().GetProperty("scale").GetValue(up) : "MISSING")}"
                        + $" uiParticleEnabled={(up != null ? ((Behaviour)up).enabled.ToString() : "-")}"
                        + $" shape={sh.shapeType} rot={sh.rotation} radius={sh.radius:F2} scale={sh.scale}"
                        + $" startSpeed={main.startSpeed.constant:F2} startSize={main.startSize.constant:F3}"
                        + $" gravity={main.gravityModifier.constant:F2} simSpace={main.simulationSpace} scalingMode={main.scalingMode}"
                        + $" worldPos={ps.transform.position:F1} p0={p0} pLast={pN}");
            }
            return sb.ToString();
        }

        /// <summary>
        /// The Low-vs-High acceptance check, measured instead of argued. One sample is not
        /// enough: step A's `while (t &lt; _enterDuration)` accumulates Time.unscaledDeltaTime,
        /// which is NOT clamped the way deltaTime is, so a hitch on the frame the modal opens
        /// can retire the whole 0.35 s enter in one frame. That noise lands on both tiers, so
        /// the window that actually answers "do timings change with quality?" is
        /// BagShake→RevealComplete, reported here alongside the raw total.
        /// </summary>
        IEnumerator TimingPass()
        {
            const int Reps = 3;
            var high = new List<float>(); var highTotal = new List<float>();
            var low  = new List<float>(); var lowTotal  = new List<float>();

            for (int tier = 0; tier < 2; tier++)
            {
                bool isLow = tier == 1;
                if (isLow)
                {
                    QualityTierService.SetOverride((int)QualityTier.Low);
                    Mark("QualityTier → Low (" + QualityTierService.Current + ")");
                    yield return Wait(1.0f);
                }
                else Mark("QualityTier = " + QualityTierService.Current + " (auto)");

                for (int r = 0; r < Reps; r++)
                {
                    int m = TraceMark();
                    var pull = FindButton("PullX1Button");
                    if (pull == null) { Debug.LogError("[GachaReveal] PullX1Button not found."); yield break; }
                    pull.onClick.Invoke();
                    yield return WaitForPhase("Idle", 30f);
                    yield return Wait(1.2f);

                    var ev = _trace.Skip(m).Where(l => !l.Contains("---")).ToList();
                    float shakeToEnd = -1f, total = -1f;
                    var shake = ev.FirstOrDefault(l => l.EndsWith("GachaBagShake"));
                    var done  = ev.LastOrDefault(l => l.EndsWith("GachaRevealComplete"));
                    if (shake != null && done != null) shakeToEnd = ParseT(done) - ParseT(shake);
                    if (ev.Count >= 2) total = ParseT(ev.Last()) - ParseT(ev.First());
                    (isLow ? low : high).Add(shakeToEnd);
                    (isLow ? lowTotal : highTotal).Add(total);
                    Mark($"{(isLow ? "LOW " : "HIGH")} rep{r}: shake→complete={shakeToEnd:F3}s total={total:F3}s");

                    var back = FindButton("BackButton");
                    if (back != null) back.onClick.Invoke();
                    yield return Wait(2.0f);
                }
            }

            QualityTierService.SetOverride(QualityTierService.AutoPref);
            Func<List<float>, string> stat = xs => $"mean={xs.Average():F3} min={xs.Min():F3} max={xs.Max():F3}";
            Mark("RESULT shake→complete  HIGH " + stat(high) + " | LOW " + stat(low)
                 + $" | |dMean|={Mathf.Abs(high.Average() - low.Average()):F3}s");
            Mark("RESULT total           HIGH " + stat(highTotal) + " | LOW " + stat(lowTotal)
                 + $" | |dMean|={Mathf.Abs(highTotal.Average() - lowTotal.Average()):F3}s");
            Mark("QualityTier restored to Auto");
        }

        // x10 end-to-end with audio, then a SKIP on a second pull.
        IEnumerator VideoPass()
        {
            Mark("PULL x10 (real banner-card button) — full reveal with audio");
            var pullX10 = FindButton("PullX10Button");
            if (pullX10 == null) { Debug.LogError("[GachaReveal] PullX10Button not found."); yield break; }
            pullX10.onClick.Invoke();

            yield return WaitForPhase("Idle", 90f);
            yield return Wait(2.5f);                        // let the Prizes entrance play out

            Mark("PULL again → SKIP at card 3");
            var pullAgain = FindButton("PullButton");
            if (pullAgain != null) pullAgain.onClick.Invoke();
            yield return WaitForHoldIndex(2);
            yield return Wait(0.4f);
            var skip = FindButton("SkipButton");
            if (skip != null) skip.onClick.Invoke();
            yield return Wait(2.5f);
        }
    }
}
#endif
