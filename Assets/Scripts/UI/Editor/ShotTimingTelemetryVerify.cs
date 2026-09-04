#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Golfin.Gameplay.Session;
using Golfin.Gameplay.UI.ShotUI;
using Golfin.Telemetry;

namespace Golfin.EditorTools
{
    /// <summary>
    /// Live end-to-end proof for `shot_timing_telemetry` (2026-08-29): a REAL green flick and a
    /// REAL red flick, driven through the actual <see cref="ClubHandleDragger"/> pointer handlers
    /// on a real hole, produce `shot_taken` events whose `timing_band` / `timing_mul` match the
    /// flick — and a sampleless debug shot produces nulls rather than a fake 0.
    ///
    /// Why a harness and not a hand test: the timing is sampled at the aim latch, so hitting a
    /// chosen band means releasing on a specific frame of a 2 Hz sweep. The runner holds the
    /// handle at the bottom of the swing, watches the live arrow, and flicks when it enters the
    /// band it is aiming for. Everything below the flick is production code — the same
    /// IPointerDown/IDrag/IPointerUp path a finger drives (PIPELINE_HARDENING §2, real-entry).
    ///
    /// It also flips <c>TelemetryService.SendsEnabled</c> on (Editor sends are off by default so a
    /// day of play-mode iteration cannot land in the beta dataset) and wraps the sender so the
    /// exact wire JSON is quotable, then lets the events reach admin.golfin.world for §21.
    ///
    /// Menu: GOLFIN ▸ ShotUI ▸ Verify Shot Timing Telemetry.
    /// </summary>
    public static class ShotTimingTelemetryVerify
    {
        const string ShellScenePath = "Assets/Scenes/ShellScene.unity";
        const string ArmedKey       = "ShotTimingTelemetryVerify.Armed";
        public const string EvidenceDir = "Docs/Specs/Active/shot_timing_telemetry/evidence";

        [InitializeOnLoadMethod]
        static void Hook() => EditorApplication.playModeStateChanged += OnPlayModeChanged;

        [MenuItem("GOLFIN/ShotUI/Verify Shot Timing Telemetry")]
        public static void Launch()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[TimingE2E] Already in play mode — stop first.");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            EditorSceneManager.OpenScene(ShellScenePath);
            Directory.CreateDirectory(EvidenceDir);
            SessionState.SetBool(ArmedKey, true);
            EditorApplication.EnterPlaymode();
            Debug.Log("[TimingE2E] Armed. Entering play mode…");
        }

        static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode) return;
            if (!SessionState.GetBool(ArmedKey, false)) return;
            SessionState.SetBool(ArmedKey, false);

            Application.runInBackground = true;   // MANDATORY for MCP-driven runs
            var host = new GameObject("[ShotTimingTelemetryBot]");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.AddComponent<ShotTimingTelemetryRunner>();
        }
    }

    public class ShotTimingTelemetryRunner : MonoBehaviour
    {
        const BindingFlags NP = BindingFlags.NonPublic | BindingFlags.Instance;
        const BindingFlags PI = BindingFlags.Public    | BindingFlags.Instance;

        // Hole 1 is what the acceptance list names; the fallbacks exist only so a card that is
        // not unlocked in this save cannot abort the whole run.
        static readonly int[] HolePreference = { 1, 10, 4, 9 };

        readonly List<string> _log = new List<string>();
        readonly List<string> _batches = new List<string>();

        ClubHandleDragger _dragger;
        RectTransform     _coneRect;
        ConeMeshGraphic   _coneGraphic;
        GraphicRaycaster  _raycaster;
        Camera            _uiCam;

        Component  _sc;                // ShotController (Golfin.Gameplay.Input, not referenced)
        FieldInfo  _fArrowProgress;
        PropertyInfo _pState, _pCommittedTiming, _pTimingMul, _pTimingAtLatch, _pIsPutt;
        MethodInfo _mFireDebugShot;
        Type       _tAccuracy;

        void Note(string k, object v) { _log.Add($"{k}: {v}"); Debug.Log($"[TimingE2E] {k}: {v}"); }

        void Start() => StartCoroutine(Sequence());

        // ── boot helpers (same shape as ShotAimParityDemoRecorder) ───────────────
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
            Debug.LogWarning($"[TimingE2E] TIMEOUT waiting for '{goName}'");
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
            Debug.LogWarning($"[TimingE2E] TIMEOUT waiting for hole {hole} card");
        }

        // ── ShotController binding (reflection: Golfin.Gameplay.Input is autoReferenced:false) ─
        bool BindShotController()
        {
            var t = AppDomain.CurrentDomain.GetAssemblies()
                       .FirstOrDefault(a => a.GetName().Name == "Golfin.Gameplay.Input")
                       ?.GetType("Golfin.Gameplay.Input.ShotController");
            if (t == null) return false;
            _sc = UnityEngine.Object.FindObjectsByType(t, FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                    .FirstOrDefault() as Component;
            if (_sc == null) return false;

            _fArrowProgress   = t.GetField("_arrowProgress", NP);
            _pState           = t.GetProperty("State", PI);
            _pCommittedTiming = t.GetProperty("LastCommittedTiming01", PI);
            _pTimingMul       = t.GetProperty("LastTimingPowerMul", PI);
            _pTimingAtLatch   = t.GetProperty("LastTimingAtLatch", PI);
            _pIsPutt          = t.GetProperty("IsPutt", PI);
            _mFireDebugShot   = t.GetMethod("FireDebugShot", PI);
            _tAccuracy        = t.Assembly.GetType("Golfin.Gameplay.Input.DebugShotAccuracy");
            return _fArrowProgress != null && _pState != null && _pCommittedTiming != null;
        }

        string StateName        => _pState.GetValue(_sc).ToString();
        float  ArrowProgress    => (float)_fArrowProgress.GetValue(_sc);
        float  CommittedTiming  => (float)_pCommittedTiming.GetValue(_sc);
        float  CommittedMul     => (float)_pTimingMul.GetValue(_sc);
        float  LiveTimingLatch  => (float)_pTimingAtLatch.GetValue(_sc);

        // ── real drag driving (verbatim idiom from ShotAimParityDemoRecorder) ────
        float ConeHeightPx => _coneGraphic != null ? _coneGraphic.HeightPx : 1009f;

        Vector2 ConeLocalToScreen(float localX, float localY)
        {
            Vector3 world = _coneRect.TransformPoint(new Vector3(localX, localY, 0f));
            return RectTransformUtility.WorldToScreenPoint(_uiCam, world);
        }

        PointerEventData _ped;
        Vector2 _lastPointerPos;

        void PointerDownAt(Vector2 screenPos)
        {
            var rr = new RaycastResult { module = _raycaster, screenPosition = screenPos };
            _ped = new PointerEventData(EventSystem.current)
            { position = screenPos, pointerId = 0, button = PointerEventData.InputButton.Left };
            _ped.pointerPressRaycast   = rr;
            _ped.pointerCurrentRaycast = rr;
            _lastPointerPos = screenPos;
            ExecuteEvents.Execute(_dragger.gameObject, _ped, ExecuteEvents.pointerDownHandler);
        }

        void DragTo(Vector2 screenPos)
        {
            _ped.delta      = screenPos - _lastPointerPos;
            _ped.position   = screenPos;
            _lastPointerPos = screenPos;
            ExecuteEvents.Execute(_dragger.gameObject, _ped, ExecuteEvents.dragHandler);
        }

        void PointerUp() => ExecuteEvents.Execute(_dragger.gameObject, _ped, ExecuteEvents.pointerUpHandler);

        /// <summary>
        /// How far the arrow travels between the frame this runner decides to flick and the frame
        /// the aim actually latches. Measured, not assumed: iter-1 aimed at 0.883 and latched at
        /// 0.008 — it had swept past 1.0 and wrapped into the red band. Seeded with that
        /// observation (~0.12 of a pass ≈ 4 frames at 2 Hz / 60 fps) and re-fitted after every
        /// swing, so a slower editor frame does not silently re-break the aim.
        /// </summary>
        float _leadProgress = 0.12f;

        /// <summary>
        /// One real swing whose LATCH lands inside [lo, hi] of the slab.
        ///
        /// Pull to <paramref name="power"/>, then hold the handle dead still at the bottom —
        /// a sample at the same Y never rises above the swing's low, so it cannot latch — while
        /// the arrow sweeps. Flick when the arrow is <see cref="_leadProgress"/> SHORT of the
        /// target window, so the latch itself falls inside it; a prediction that would cross the
        /// apex (≥ 0.99) is skipped and the next pass is used instead, because past 1.0 the arrow
        /// wraps to 0 and a green aim becomes a red flick.
        /// </summary>
        IEnumerator SwingInBand(float lo, float hi, float power, string tag)
        {
            float localY = Mathf.Clamp01(1f - power) * ConeHeightPx;
            Vector2 top  = ConeLocalToScreen(0f, ConeHeightPx * 0.92f);
            Vector2 hold = ConeLocalToScreen(0f, localY);

            PointerDownAt(top);
            yield return null;
            for (int i = 1; i <= 8; i++)   // pull down to power, one drag per rendered frame
            {
                DragTo(Vector2.Lerp(top, hold, i / 8f));
                yield return null;
            }

            float waited    = 0f;
            bool  armed     = false;
            float triggerAt = float.NaN;
            while (waited < 20f)
            {
                DragTo(hold);                       // hold still: no rise ⇒ no latch
                float p = ArrowProgress;
                float predicted = p + _leadProgress;
                if (StateName == "Timing" && predicted >= lo && predicted <= hi && predicted <= 0.99f)
                { armed = true; triggerAt = p; break; }
                yield return null;
                waited += Time.unscaledDeltaTime;
            }
            Note($"{tag}_arrow_at_flick", armed
                ? $"{triggerAt:F3} (predicting {(triggerAt + _leadProgress):F3} with lead {_leadProgress:F3})"
                : "NEVER ENTERED BAND");

            float stepPx = Screen.height * 0.10f;
            for (int i = 1; i <= 4; i++)            // the flick itself
            {
                DragTo(new Vector2(hold.x, hold.y + stepPx * i));
                yield return null;
            }
            PointerUp();
            yield return null;

            float latched = LiveTimingLatch;
            if (armed && !float.IsNaN(latched))
            {
                float observed = latched - triggerAt;
                if (observed < 0f) observed += 1f;              // the arrow wrapped mid-flick
                if (observed > 0f && observed < 0.5f) _leadProgress = observed;
            }
            Note($"{tag}_latched_timing01", latched.ToString("F3"));
            Note($"{tag}_lead_refit", _leadProgress.ToString("F3"));
            Note($"{tag}_committed_timing01", CommittedTiming.ToString("F3"));
            Note($"{tag}_committed_mul", CommittedMul.ToString("F3"));
        }

        IEnumerator WaitForIdle(float timeout = 45f)
        {
            float t = 0f;
            while (t < timeout)
            {
                if (!ShotInProgressUiGate.ShotInProgress && StateName == "Idle") yield break;
                yield return new WaitForSecondsRealtime(0.2f); t += 0.2f;
            }
            Debug.LogWarning($"[TimingE2E] STALLED waiting for Idle (state={StateName})");
        }

        /// <summary>Waits for the shot record the ball's rest appends, then reports its keys.</summary>
        IEnumerator ReportShot(string tag, int beforeCount, float timeout = 45f)
        {
            float t = 0f;
            while (GameSession.ShotHistory.Count <= beforeCount && t < timeout)
            { yield return new WaitForSecondsRealtime(0.25f); t += 0.25f; }

            if (GameSession.ShotHistory.Count <= beforeCount)
            {
                Note($"{tag}_RESULT", "NO ShotRecord was appended within the timeout");
                yield break;
            }

            var shot = GameSession.ShotHistory[GameSession.ShotHistory.Count - 1];
            var payload = new Dictionary<string, object>();
            // Same call TelemetryHooks makes, scheme included (control_scheme_seam §3.5) — this
            // tool exists to prove the SHIPPING payload on a real hole, so it must not build a
            // simpler one than production does.
            GameSession.AppendShotTimingKeys(payload, shot,
                (int)Golfin.Gameplay.UI.Controls.ControlSchemeService.Current);
            Note($"{tag}_record", $"club={shot.ClubLabel} terminal={shot.TerminalState} " +
                                  $"dist={shot.DistanceXZMeters:F1}m Timing01={shot.Timing01:F3} " +
                                  $"TimingPowerMul={shot.TimingPowerMul:F3}");
            Note($"{tag}_payload", $"timing01={(payload["timing01"] ?? "null")} " +
                                   $"timing_mul={payload["timing_mul"]} " +
                                   $"timing_band={(payload["timing_band"] ?? "null")} " +
                                   $"scheme={payload["scheme"]}");
        }

        /// <summary>
        /// Keep swinging until one really lands in <paramref name="band"/>. The lead re-fit makes
        /// attempt 2 land where attempt 1 was aiming; the retry exists so a mis-timed swing is
        /// re-taken rather than reported as if it were the shot that was asked for.
        /// </summary>
        IEnumerator SwingUntilBand(string band, float lo, float hi, int maxAttempts = 3)
        {
            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                yield return WaitForIdle();
                string tag = attempt == 1 ? band : $"{band}_try{attempt}";
                int before = GameSession.ShotHistory.Count;
                yield return SwingInBand(lo, hi, 0.55f, tag);
                yield return ReportShot(tag, before);

                string got = GameSession.ShotHistory.Count > before
                           ? GameSession.TimingBand(GameSession.ShotHistory[GameSession.ShotHistory.Count - 1].Timing01)
                           : null;
                if (got == band)
                {
                    Note($"{band}_ACCEPTED", $"attempt {attempt} landed in the {band} band");
                    yield return WaitForIdle();
                    yield break;
                }
                Note($"{band}_retry", $"attempt {attempt} landed in '{got ?? "null"}' — re-taking the swing");
            }
            Note($"{band}_FAILED", $"no swing landed in the {band} band in {maxAttempts} attempts");
        }

        IEnumerator Sequence()
        {
            // ── telemetry: turn Editor sends on and tap the wire ─────────────────
            var svc = TelemetryService.Instance;
            svc.SendsEnabled = true;
            var inner = svc.Sender;
            svc.Sender = (json, cb) =>
            {
                _batches.Add(json);
                Debug.Log($"[TimingE2E] BATCH → {json}");
                inner(json, cb);
            };
            Note("telemetry", $"SendsEnabled={svc.SendsEnabled} session={svc.SessionId}");

            // ── boot through the REAL entry path ─────────────────────────────────
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
            yield return new WaitForSecondsRealtime(4f);

            _dragger = UnityEngine.Object.FindObjectsByType<ClubHandleDragger>(
                           FindObjectsInactive.Exclude, FindObjectsSortMode.None).FirstOrDefault();
            if (_dragger == null || !BindShotController()) { Fail("could not bind dragger/ShotController"); yield break; }

            _coneRect    = typeof(ClubHandleDragger).GetField("_coneRect", NP)?.GetValue(_dragger) as RectTransform;
            _coneGraphic = typeof(ClubHandleDragger).GetField("_coneGraphic", NP)?.GetValue(_dragger) as ConeMeshGraphic;
            var canvas   = _dragger.GetComponentInParent<Canvas>();
            _raycaster   = canvas != null ? canvas.rootCanvas.GetComponent<GraphicRaycaster>() : null;
            _uiCam       = _raycaster != null ? _raycaster.eventCamera
                         : (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null);
            if (_coneRect == null) { Fail("_coneRect not readable"); yield break; }

            Note("resolution", $"{Screen.width}x{Screen.height}");
            Note("driver", "real ClubHandleDragger IPointerDown/IDrag/IPointerUp events");

            // ── 1. a GREEN flick, then 2. a RED one ─────────────────────────────
            yield return SwingUntilBand("green", 0.88f, 0.97f);
            yield return SwingUntilBand("red",   0.10f, 0.35f);

            // ── 3. a sampleless shot (the bot / debug driver path) ──────────────
            int before = GameSession.ShotHistory.Count;
            if (_mFireDebugShot != null && _tAccuracy != null)
            {
                _mFireDebugShot.Invoke(_sc, new object[] { 0.5f, Enum.Parse(_tAccuracy, "Green"), 0f });
                yield return ReportShot("debug", before);
                yield return WaitForIdle();
            }
            else Note("debug_RESULT", "FireDebugShot not reachable by reflection");

            // ── 4. push everything to the server (§21) ──────────────────────────
            for (int i = 0; i < 6; i++)
            {
                svc.Flush();
                yield return new WaitForSecondsRealtime(2f);
            }
            Note("queued_after_flush", svc.QueuedCount);
            Note("batches_sent", _batches.Count);

            Write();
            yield return new WaitForSecondsRealtime(1f);
            EditorApplication.ExitPlaymode();
        }

        void Fail(string why)
        {
            Note("ABORT", why);
            Write();
            EditorApplication.ExitPlaymode();
        }

        void Write()
        {
            Directory.CreateDirectory(ShotTimingTelemetryVerify.EvidenceDir);
            var sb = new StringBuilder();
            sb.AppendLine("# shot_timing_telemetry — live Editor E2E");
            sb.AppendLine();
            foreach (var l in _log) sb.AppendLine(l);
            sb.AppendLine();
            sb.AppendLine("## Wire batches");
            foreach (var b in _batches) { sb.AppendLine(b); sb.AppendLine(); }
            File.WriteAllText(Path.Combine(ShotTimingTelemetryVerify.EvidenceDir, "live_e2e.txt"), sb.ToString());
            Debug.Log("[TimingE2E] evidence written to " + ShotTimingTelemetryVerify.EvidenceDir + "/live_e2e.txt");
        }
    }
}
#endif
