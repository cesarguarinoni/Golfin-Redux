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
using Golfin.Diagnostics.Runtime;
using Golfin.Gameplay.UI.Controls;
using Golfin.Gameplay.UI.ShotUI;

namespace Golfin.UI.EditorTools
{
    /// <summary>
    /// miss_grade_duff §4 — the VISUAL acceptance run for the Flick scheme's new grade pop and
    /// the power gauge's DUFF flash.
    ///
    /// <para>REAL ENTRY PATH, not a render harness (Cesar's standing rule): it boots
    /// <c>ShellScene</c>, taps the title screen's own StartButton, PLAY and a hole card, and then
    /// swings by driving <see cref="ClubHandleDragger"/>'s IPointerDown / IDrag / IPointerUp
    /// handlers — the same events a thumb raises. Every number and every pixel below therefore
    /// comes from a shot the player could have taken.</para>
    ///
    /// <para>Structurally a sibling of <c>ShotTimingTelemetryVerify</c> and deliberately so: the
    /// boot sequence, the hold-still-then-flick idiom and the lead-time re-fit are that tool's,
    /// which is the only code in the project known to land a REAL timed flick in a chosen band.
    /// What is new here is what it reads back afterwards — the pop's localisation KEY and colour,
    /// and the gauge's flash state — plus one full-resolution frame per band.</para>
    ///
    /// <para>Menu: <b>GOLFIN ▸ ShotUI ▸ Verify Miss-Duff Flick Pops</b>.</para>
    /// </summary>
    public static class MissDuffFlickVerify
    {
        const string ShellScenePath = "Assets/Scenes/ShellScene.unity";
        const string ArmedKey       = "MissDuffFlickVerify.Armed";
        public const string TaskDir = "Docs/Specs/Active/miss_grade_duff";

        [InitializeOnLoadMethod]
        static void Hook() => EditorApplication.playModeStateChanged += OnPlayModeChanged;

        [MenuItem("GOLFIN/ShotUI/Verify Miss-Duff Flick Pops")]
        public static void Launch()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[MissDuff] Already in play mode — stop first.");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            EditorSceneManager.OpenScene(ShellScenePath);
            Directory.CreateDirectory($"{TaskDir}/screenshots");
            Directory.CreateDirectory($"{TaskDir}/evidence");
            SessionState.SetBool(ArmedKey, true);
            EditorApplication.EnterPlaymode();
            Debug.Log("[MissDuff] Armed. Entering play mode…");
        }

        static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode) return;
            if (!SessionState.GetBool(ArmedKey, false)) return;
            SessionState.SetBool(ArmedKey, false);

            Application.runInBackground = true;   // MANDATORY for MCP-driven runs
            var host = new GameObject("[MissDuffFlickBot]");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.AddComponent<MissDuffFlickRunner>();
        }
    }

    public class MissDuffFlickRunner : MonoBehaviour
    {
        const BindingFlags NP = BindingFlags.NonPublic | BindingFlags.Instance;
        const BindingFlags PI = BindingFlags.Public    | BindingFlags.Instance;

        static readonly int[] HolePreference = { 1, 10, 4, 9 };

        readonly List<string> _log  = new List<string>();
        readonly List<string> _rows = new List<string>();

        ClubHandleDragger _dragger;
        RectTransform     _coneRect;
        ConeMeshGraphic   _coneGraphic;
        GraphicRaycaster  _raycaster;
        Camera            _uiCam;
        SchemeGradePop    _pop;
        TMPro.TMP_Text    _popLabel;
        PowerGaugeWidget  _gauge;

        // Golfin.Gameplay.Input is autoReferenced:false — reflection, as its sibling tool does.
        Component    _sc;
        FieldInfo    _fArrowProgress;
        PropertyInfo _pState, _pTimingMul, _pTimingAtLatch, _pShotWasMiss, _pFlickGrade, _pPower;

        void Note(string k, object v) { _log.Add($"{k}: {v}"); Debug.Log($"[MissDuff] {k}: {v}"); }

        void Start() => StartCoroutine(Sequence());

        // ── boot helpers ─────────────────────────────────────────────────────────
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
            Debug.LogWarning($"[MissDuff] TIMEOUT waiting for '{goName}'");
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
            Debug.LogWarning($"[MissDuff] TIMEOUT waiting for hole {hole} card");
        }

        bool BindShotController()
        {
            var t = AppDomain.CurrentDomain.GetAssemblies()
                       .FirstOrDefault(a => a.GetName().Name == "Golfin.Gameplay.Input")
                       ?.GetType("Golfin.Gameplay.Input.ShotController");
            if (t == null) return false;
            _sc = UnityEngine.Object.FindObjectsByType(t, FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                    .FirstOrDefault() as Component;
            if (_sc == null) return false;

            _fArrowProgress = t.GetField("_arrowProgress", NP);
            _pState         = t.GetProperty("State", PI);
            _pTimingMul     = t.GetProperty("LastTimingPowerMul", PI);
            _pTimingAtLatch = t.GetProperty("LastTimingAtLatch", PI);
            _pShotWasMiss   = t.GetProperty("LastShotWasMiss", PI);
            _pFlickGrade    = t.GetProperty("LastFlickGrade", PI);
            _pPower         = t.GetProperty("PowerNormalized", PI);
            return _fArrowProgress != null && _pState != null && _pShotWasMiss != null && _pFlickGrade != null;
        }

        string StateName       => _pState.GetValue(_sc).ToString();
        float  ArrowProgress   => (float)_fArrowProgress.GetValue(_sc);
        float  CommittedMul    => (float)_pTimingMul.GetValue(_sc);
        float  LiveTimingLatch => (float)_pTimingAtLatch.GetValue(_sc);
        bool   ShotWasMiss     => (bool)_pShotWasMiss.GetValue(_sc);
        string FlickGradeName  => _pFlickGrade.GetValue(_sc)?.ToString() ?? "null";
        float  Power           => (float)_pPower.GetValue(_sc);

        // ── real drag driving (the sibling tool's idiom, verbatim) ───────────────
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

        /// <summary>Arrow travel between deciding to flick and the latch actually landing.
        /// Measured and re-fitted after every swing, never assumed.</summary>
        float _leadProgress = 0.12f;

        IEnumerator SwingInBand(float lo, float hi, float power, string tag)
        {
            float localY = Mathf.Clamp01(1f - power) * ConeHeightPx;
            Vector2 top  = ConeLocalToScreen(0f, ConeHeightPx * 0.92f);
            Vector2 hold = ConeLocalToScreen(0f, localY);

            PointerDownAt(top);
            yield return null;
            for (int i = 1; i <= 8; i++) { DragTo(Vector2.Lerp(top, hold, i / 8f)); yield return null; }

            float waited = 0f, triggerAt = float.NaN;
            bool armed = false;
            while (waited < 12f)
            {
                DragTo(hold);                       // hold still: no rise => no latch
                float p = ArrowProgress;
                // WRAP-AWARE, unlike the sibling tool. ShotTimingTelemetryVerify refuses any
                // prediction past 1.0 because a wrap would drop a green aim into the red band.
                // The DUFF band IS below the red line, so for this tool the wrap is the only way
                // in: with a ~0.12 lead, arming for a latch at 0.02-0.12 means flicking at
                // 0.90-1.00 and letting the arrow roll over. Refusing the wrap is what made four
                // duff attempts arm never and auto-cancel at MaxTotalPasses.
                float predicted = Mathf.Repeat(p + _leadProgress, 1f);
                if (StateName == "Timing" && predicted >= lo && predicted <= hi)
                { armed = true; triggerAt = p; break; }
                yield return null;
                waited += Time.unscaledDeltaTime;
            }
            if (!armed) Note($"{tag}_arm", $"NEVER ENTERED [{lo:F2},{hi:F2}] (lead {_leadProgress:F3})");

            float stepPx = Screen.height * 0.10f;
            for (int i = 1; i <= 4; i++) { DragTo(new Vector2(hold.x, hold.y + stepPx * i)); yield return null; }
            PointerUp();
            yield return null;

            float latched = LiveTimingLatch;
            if (armed && !float.IsNaN(latched))
            {
                float observed = latched - triggerAt;
                if (observed < 0f) observed += 1f;
                if (observed > 0f && observed < 0.5f) _leadProgress = observed;
            }
            Note($"{tag}_latched_timing01", latched.ToString("F3"));
        }

        IEnumerator WaitForIdle(float timeout = 45f)
        {
            float t = 0f;
            while (t < timeout)
            {
                if (!ShotInProgressUiGate.ShotInProgress && StateName == "Idle") yield break;
                yield return new WaitForSecondsRealtime(0.2f); t += 0.2f;
            }
            Debug.LogWarning($"[MissDuff] STALLED waiting for Idle (state={StateName})");
        }

        // ── the acceptance measurement ──────────────────────────────────────────

        /// <summary>
        /// One band: swing until the latch really lands in it, then — while the pop is still on
        /// screen — read back the KEY it resolved, the colour it painted, the gauge's flash and
        /// its percentage, and save one full-resolution frame.
        /// </summary>
        IEnumerator MeasureBand(string band, float lo, float hi, string expectGrade, int maxAttempts = 4)
        {
            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                yield return WaitForIdle();
                float powerAtCommit = 0f;
                float latch;

                // Snapshot the power the instant before the flick: the gauge shows
                // PowerNormalized x TimingMul, and PowerNormalized is reset when the shot ends.
                yield return SwingInBand(lo, hi, 0.55f, $"{band}_a{attempt}");
                powerAtCommit = Power;
                latch = LiveTimingLatch;

                string grade = FlickGradeName;
                if (grade != expectGrade)
                {
                    Note($"{band}_retry", $"attempt {attempt} graded {grade} (latch {latch:F3}) — re-taking");
                    yield return WaitForIdle();
                    continue;
                }

                // The pop and the flash are up for SchemeGradePop.DisplaySeconds; sample early in
                // that window so neither has begun to fade.
                yield return new WaitForSecondsRealtime(0.25f);

                string key   = _pop != null ? _pop.LastKeyShown : "<no pop>";
                string word  = _popLabel != null ? _popLabel.text : "<no label>";
                Color  col   = _popLabel != null ? _popLabel.color : Color.clear;
                float  alpha = PopAlpha();
                var    gauge = ReadGauge();

                string shot = Path.Combine(MissDuffFlickVerify.TaskDir, "screenshots",
                                           $"flick_{band}.png");
                // END OF FRAME FIRST. SnapPlayModeSafe composites via
                // ScreenCapture.CaptureScreenshotAsTexture (the only path that captures Screen
                // Space Overlay canvases, i.e. the pop and the gauge), and that API returns null
                // anywhere but end-of-frame — which is how it can hand back a path for a file it
                // never wrote. Its own summary says the caller owns this yield.
                yield return new WaitForEndOfFrame();
                string snapped = CaptureCore.SnapPlayModeSafe($"missduff_flick_{band}");
                string copied  = "<none>";
                if (!string.IsNullOrEmpty(snapped) && File.Exists(snapped) &&
                    new FileInfo(snapped).Length > 1024)
                {
                    File.Copy(snapped, shot, true);
                    var fi = new FileInfo(shot);
                    copied = $"{shot} ({fi.Length / 1024} KB, md5 {Md5(shot)})";
                }
                else Note($"{band}_CAPTURE_FAILED",
                          $"SnapPlayModeSafe returned '{snapped}' — exists={File.Exists(snapped ?? "")}");

                _rows.Add($"| {band} | {latch:F3} | {grade} | {key} | \"{word}\" | " +
                          $"#{ColorUtility.ToHtmlStringRGB(col)} | alpha {alpha:F2} | " +
                          $"{CommittedMul:F3} | {ShotWasMiss} | {gauge} | `{shot}` |");
                Note($"{band}_ACCEPTED",
                     $"latch={latch:F3} grade={grade} key={key} word='{word}' " +
                     $"color=#{ColorUtility.ToHtmlStringRGB(col)} popAlpha={alpha:F2} " +
                     $"mul={CommittedMul:F3} isMiss={ShotWasMiss} power={powerAtCommit:F2} gauge=[{gauge}] png={copied}");

                yield return WaitForIdle();
                yield break;
            }
            Note($"{band}_FAILED", $"no swing graded {expectGrade} in {maxAttempts} attempts");
            _rows.Add($"| {band} | — | FAILED | — | — | — | — | — | — | — | — |");
        }

        /// <summary>Frames are md5'd because SnapPlayModeSafe has been seen to return real but
        /// STALE, byte-identical files for two different states — a path alone proves nothing.</summary>
        static string Md5(string path)
        {
            using (var md5 = System.Security.Cryptography.MD5.Create())
            using (var fs = File.OpenRead(path))
                return BitConverter.ToString(md5.ComputeHash(fs)).Replace("-", "").Substring(0, 8).ToLowerInvariant();
        }

        float PopAlpha()
        {
            if (_pop == null) return -1f;
            var cg = _pop.GetComponent<CanvasGroup>();
            return cg != null ? cg.alpha : -1f;
        }

        /// <summary>The gauge's live state: is it visible, is the arc overridden red, what % does
        /// it read. Read off the components, never off a pixel.</summary>
        string ReadGauge()
        {
            if (_gauge == null) return "gauge not found";
            var cg  = _gauge.GetComponent<CanvasGroup>();
            var g   = typeof(PowerGaugeWidget).GetField("_gauge", NP)?.GetValue(_gauge) as PowerGaugeGraphic;
            var pct = typeof(PowerGaugeWidget).GetField("_pctText", NP)?.GetValue(_gauge) as TMPro.TMP_Text;
            Color? ovr = g != null ? g.ArcColorOverride : null;
            return $"alpha={(cg != null ? cg.alpha.ToString("F2") : "?")} " +
                   $"arcOverride={(ovr.HasValue ? "#" + ColorUtility.ToHtmlStringRGB(ovr.Value) : "none")} " +
                   $"progress={(g != null ? g.Progress01.ToString("F3") : "?")} " +
                   $"pct='{(pct != null ? pct.text : "?")}' " +
                   $"pctColor={(pct != null ? "#" + ColorUtility.ToHtmlStringRGB(pct.color) : "?")}";
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

            // The Flick pop lives under SchemeRoot_Flick; find it by name so a second scheme's pop
            // can never be measured by accident.
            _pop = UnityEngine.Object.FindObjectsByType<SchemeGradePop>(
                       FindObjectsInactive.Include, FindObjectsSortMode.None)
                   .FirstOrDefault(p => p.gameObject.name == "FlickGradePop");
            _popLabel = _pop != null
                ? (TMPro.TMP_Text)typeof(SchemeGradePop).GetField("_label", NP)?.GetValue(_pop)
                : null;
            _gauge = UnityEngine.Object.FindObjectsByType<PowerGaugeWidget>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();

            Note("resolution", $"{Screen.width}x{Screen.height}");
            Note("driver", "real ClubHandleDragger IPointerDown/IDrag/IPointerUp events");
            Note("pop_found", _pop != null ? _pop.gameObject.name : "FlickGradePop NOT FOUND");
            Note("gauge_found", _gauge != null ? _gauge.gameObject.name : "PowerGaugeWidget NOT FOUND");
            Note("cone_bands", $"red={ConeBandPalette.BandRedY01} gold={ConeBandPalette.BandGoldY01} " +
                               $"green={ConeBandPalette.BandGreenY01}");

            // Aim BELOW / ON / BETWEEN / ABOVE the four band lines. The windows are the bands
            // themselves, narrowed away from the edges so a one-frame lead error cannot land the
            // swing in a neighbour and report it as this one.
            yield return MeasureBand("duff", 0.02f, 0.12f, "Duff");
            yield return MeasureBand("thin", 0.18f, 0.40f, "Thin");
            yield return MeasureBand("good", 0.50f, 0.80f, "Good");
            yield return MeasureBand("pure", 0.88f, 0.97f, "Pure");

            Write();
            yield return new WaitForSecondsRealtime(1f);
            EditorApplication.ExitPlaymode();
        }

        void Fail(string why) { Note("ABORT", why); Write(); EditorApplication.ExitPlaymode(); }

        void Write()
        {
            string dir = Path.Combine(MissDuffFlickVerify.TaskDir, "evidence");
            Directory.CreateDirectory(dir);
            var sb = new StringBuilder();
            sb.AppendLine("# miss_grade_duff — Flick grade pops + gauge DUFF flash, live in play mode");
            sb.AppendLine();
            sb.AppendLine("Driven through the REAL ClubHandleDragger pointer handlers after booting");
            sb.AppendLine("ShellScene and tapping StartButton -> PLAY -> a hole card.");
            sb.AppendLine();
            sb.AppendLine("| band | latch t | grade | key | word | colour | pop | timingMul | isMiss | gauge | png |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|");
            foreach (var r in _rows) sb.AppendLine(r);
            sb.AppendLine();
            sb.AppendLine("## Log");
            foreach (var l in _log) sb.AppendLine("- " + l);
            File.WriteAllText(Path.Combine(dir, "flick_pops.md"), sb.ToString());
            Debug.Log("[MissDuff] evidence written to " + dir + "/flick_pops.md");
        }
    }
}
#endif
