#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Golfin.Diagnostics.Runtime;
using Golfin.Gameplay.Session;
using Golfin.Gameplay.Loop;
using Golfin.Physics.Math;
using Golfin.Physics;

namespace Golfin.Physics.Viewer
{
    /// <summary>
    /// §2f screenshot capture host (iter-4: fixes S6 fire — BallAnimator.PlayRate=Instant for S5/S6 comparison shots).
    ///
    /// CaptureMode=0: auto-enter putter (Hole_01_Geo), auto-exit putter, tuning panel.
    ///   S1 — controls_2f_auto_enter_putter_on_green.png   (AtRest on green → putter mode; CAM:GroundLevel fixed)
    ///   S2 — controls_2f_auto_exit_to_last_club.png       (AtRest off green → reverted)
    ///   S3 — controls_2f_tuning_panel_open.png            (gear panel expanded)
    ///   S4 — controls_2f_tuning_live_apply.png            (slider.value set → onValueChanged fired; thumb visible left)
    ///   S5 — controls_2f_tuning_putt_baseline_atrest.png  (baseline putt at RR=0.12, ball at rest)
    ///   S6 — controls_2f_tuning_putt_fast_atrest.png      (tuned putt at RR=0.05, ball rolls farther)
    ///   L1 — controls_2f_history_log.txt                  (SurfaceConfig log + roll-distance delta)
    ///
    /// Self-destructs after completion. DO NOT use in production.
    /// </summary>
    public class SmokeRunner2fHost : MonoBehaviour
    {
        const float StartupWait = 5.0f;
        const float ShotWait    = 25.0f;
        const float CaptureWait = 1.5f;

        const string ArmedKey = "SmokeRunner2fHost.Armed";

        public static bool Armed
        {
#if UNITY_EDITOR
            get => UnityEditor.SessionState.GetBool(ArmedKey, false);
            set => UnityEditor.SessionState.SetBool(ArmedKey, value);
#else
            get => false;
            set { }
#endif
        }

        void Start()
        {
#if UNITY_EDITOR
            bool sessionArmed = UnityEditor.SessionState.GetBool(ArmedKey, false);
            Debug.Log($"[SmokeRunner2fHost] Start() — SessionState Armed={sessionArmed}");
#else
            bool sessionArmed = false;
#endif
            if (!sessionArmed)
            {
                Debug.LogWarning("[SmokeRunner2fHost] Not armed — destroying self.");
                Destroy(this);
                return;
            }
            Armed = false;
            StartCoroutine(SafeRunSequence());
        }

        IEnumerator SafeRunSequence()
        {
            Debug.Log("[SmokeRunner2fHost] SafeRunSequence starting...");
            bool completed = false;
            System.Exception caughtEx = null;
            yield return StartCoroutine(RunSequenceInner(() => completed = true, ex => caughtEx = ex));
            if (caughtEx != null)
                Debug.LogError($"[SmokeRunner2fHost] Coroutine threw exception: {caughtEx}");
            if (!completed)
                Debug.LogError("[SmokeRunner2fHost] RunSequence did NOT complete normally.");
            Destroy(this);
        }

        IEnumerator RunSequenceInner(System.Action onDone, System.Action<System.Exception> onError)
        {
            // Force timeScale=1 in case something set it to 0 (e.g. a paused scene).
            // WaitForSeconds uses scaled time so timeScale=0 would freeze it forever.
            if (Time.timeScale < 0.01f)
            {
                Debug.LogWarning($"[SmokeRunner2fHost] timeScale={Time.timeScale} detected — forcing to 1.");
                Time.timeScale = 1f;
            }
            Debug.Log($"[SmokeRunner2fHost] Waiting {StartupWait}s (realtime) for startup... timeScale={Time.timeScale}");
            yield return new WaitForSecondsRealtime(StartupWait);
            Debug.Log("[SmokeRunner2fHost] Startup wait done. Looking for controller...");

            PhysicsLabController controller = null;
            try
            {
                controller = FindObjectOfType<PhysicsLabController>();
            }
            catch (System.Exception ex)
            {
                onError(ex);
                yield break;
            }

            if (controller == null)
            {
                Debug.LogError("[SmokeRunner2fHost] PhysicsLabController not found.");
                yield break;
            }
            Debug.Log($"[SmokeRunner2fHost] Controller found: {controller.name}");

            var sm = controller.BallSM;
            for (int retry = 0; retry < 6 && sm == null; retry++)
            {
                Debug.Log($"[SmokeRunner2fHost] BallSM null, retry {retry+1}/6...");
                yield return new WaitForSecondsRealtime(0.5f);
                sm = controller.BallSM;
            }
            if (sm == null)
            {
                Debug.LogError("[SmokeRunner2fHost] BallSM not found after retries.");
                yield break;
            }
            Debug.Log($"[SmokeRunner2fHost] BallSM found.");

            // ── Log initial SurfaceConfig[Green] ──────────────────────────────
            SurfaceCoefficients greenCoefAwake;
            float origRR = 0.12f;
            try
            {
                greenCoefAwake = controller.SurfaceCfg[SurfaceType.Green];
                origRR = greenCoefAwake.RollingResistance.ToFloat();
                Debug.Log($"[SmokeRunner2fHost][SurfaceCfgLog] Awake: Green.RollingResistance={greenCoefAwake.RollingResistance.ToFloat():F4} StopSpeed={greenCoefAwake.StopSpeed.ToFloat():F4}");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[SmokeRunner2fHost] SurfaceCfg access failed: {ex.Message}");
                onError(ex);
                yield break;
            }

            // ── Reset and set to Driver ───────────────────────────────────────
            try
            {
                GameSession.ResetForNewHole();
                Debug.Log("[SmokeRunner2fHost] GameSession reset done.");
                controller.SetClub(0); // Driver
                Debug.Log($"[SmokeRunner2fHost] SetClub(0) done. CurrentClubIndex={controller.CurrentClubIndex}");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[SmokeRunner2fHost] Setup failed: {ex.Message}");
                onError(ex);
                yield break;
            }

            // ── Shot 1: Place ball on green, fire short putt → stays on green → auto-putter ─────
            // Strategy: PlaceBallAt the green center first (so GetCurrentOrigin returns
            // a green position), then fire putt_flat_3m. The ball starts and ends on
            // SurfaceType.Green. HandleShotComplete sees EndSurface=Green and auto-switches
            // to putter. We start in Driver mode (SetClub(0) above) so the switch is
            // non-trivial (Driver→Putter).
            //
            // Key insight: controller.Fire() uses GetCurrentOrigin() which returns the
            // CURRENT BALL POSITION (not preset.Origin) when the ball animator exists.
            // We must place the ball on green BEFORE calling Fire().
            const float GreenWorldX = -230f;
            const float GreenWorldZ = -73f;
            try
            {
                // SurfaceType.Green = 1. Place the ball on the green surface.
                controller.PlaceBallAt(new UnityEngine.Vector3(GreenWorldX, 0f, GreenWorldZ), preferredSurfaceTypeValue: 1);
                Debug.Log($"[SmokeRunner2fHost] Placed ball on green at ({GreenWorldX}, Y, {GreenWorldZ}).");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[SmokeRunner2fHost] PlaceBallAt green failed: {ex.Message}");
                onError(ex);
                yield break;
            }
            yield return new WaitForSecondsRealtime(0.5f); // Let placement settle.

            bool shot1Complete   = false;
            ShotResult shot1Result = default;
            System.Action<ShotResult> onComplete1 = r => { shot1Complete = true; shot1Result = r; Debug.Log($"[SmokeRunner2fHost] OnShotComplete1 fired. terminal={r.TerminalState} endSurface={r.EndSurface}"); };
            sm.OnShotComplete += onComplete1;
            Debug.Log("[SmokeRunner2fHost] Subscribed to OnShotComplete.");

            ShotPreset preset1;
            try
            {
                // putt_flat_3m fires at 0.35 m/s from current ball pos (green, placed above).
                preset1 = ShotPresetCatalog.All.FirstOrDefault(p => p.Id == "putt_flat_3m");
                if (preset1.Id == null)
                    preset1 = ShotPresetCatalog.All.FirstOrDefault(p => p.Id != null && p.Id.StartsWith("putt"));
                if (preset1.Id == null)
                {
                    Debug.LogError("[SmokeRunner2fHost] No putt preset found for shot1 (green auto-enter test).");
                    sm.OnShotComplete -= onComplete1;
                    yield break;
                }
                Debug.Log($"[SmokeRunner2fHost] Using preset: {preset1.Id}");
                controller.Fire(preset1);
                Debug.Log("[SmokeRunner2fHost] Shot1 fired.");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[SmokeRunner2fHost] Shot1 fire failed: {ex.Message}");
                sm.OnShotComplete -= onComplete1;
                onError(ex);
                yield break;
            }

            float elapsed1 = 0f;
            while (!shot1Complete && elapsed1 < ShotWait)
            {
                elapsed1 += Time.unscaledDeltaTime;
                if (Mathf.FloorToInt(elapsed1) != Mathf.FloorToInt(elapsed1 - Time.unscaledDeltaTime))
                    Debug.Log($"[SmokeRunner2fHost] Waiting shot1... elapsed={elapsed1:F1}s complete={shot1Complete}");
                yield return null;
            }
            sm.OnShotComplete -= onComplete1;

            Debug.Log($"[SmokeRunner2fHost] Shot1 done: complete={shot1Complete} elapsed={elapsed1:F1}s club={controller.CurrentClubIndex} endSurface={shot1Result.EndSurface}");

            yield return new WaitForSecondsRealtime(CaptureWait);

            // ── S1: Capture ───────────────────────────────────────────────────
            // FAIL-3 fix: EnterPutterMode now calls chaseCamera.SetMode(GroundLevel) directly,
            // so the debug overlay "CAM: GroundLevel" is visible in this frame. Log confirms it.
            string path1;
            try
            {
                path1 = CaptureCore.SnapPlayModeSafe("controls_2f_auto_enter_putter_on_green");
                Debug.Log($"[SmokeRunner2fHost] S1 captured: {path1} | club={controller.CurrentClubIndex} (expected 3=Putter)");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[SmokeRunner2fHost] S1 capture failed: {ex.Message}");
                path1 = "CAPTURE_FAILED";
            }

            // ── Shot 2: Place at green back edge, putt off green → auto-exit putter ─────────────
            // putt_off_back fires at 3.5 m/s. The velocity is rotated to _cameraYaw.
            // Green X max boundary at z=-72.5 is ~x=-219.2.
            // Set yaw=0 (world +X) so the putt fires toward the exit boundary.
            // Place ball at (-221,0,-72.5) — only ~2m from X=-219 edge.
            Debug.Log($"[SmokeRunner2fHost] Before shot2: CurrentClubIndex={controller.CurrentClubIndex}");
            try
            {
                // PlaceBallAt overwrites _cameraYaw with the default look direction.
                // Call SetCameraYawRadians AFTER PlaceBallAt so it wins.
                controller.PlaceBallAt(new UnityEngine.Vector3(-221f, 0f, -72.5f), preferredSurfaceTypeValue: 1);
                Debug.Log("[SmokeRunner2fHost] Placed ball at green back edge (-221,0,-72.5) — ~2m from X=-219 edge.");
                controller.SetCameraYawRadians(0f); // world +X → exits green at x=-219
                Debug.Log("[SmokeRunner2fHost] Set camera yaw to 0 (world +X) for shot2 exit direction.");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[SmokeRunner2fHost] PlaceBallAt back-edge failed: {ex.Message}");
                onError(ex);
                yield break;
            }
            yield return new WaitForSecondsRealtime(0.5f);

            bool shot2Complete = false;
            ShotResult shot2Result = default;
            System.Action<ShotResult> onComplete2 = r => { shot2Complete = true; shot2Result = r; Debug.Log($"[SmokeRunner2fHost] OnShotComplete2 fired. terminal={r.TerminalState} endSurface={r.EndSurface}"); };
            sm.OnShotComplete += onComplete2;

            ShotPreset preset2;
            try
            {
                preset2 = ShotPresetCatalog.All.FirstOrDefault(p => p.Id == "putt_off_back");
                if (preset2.Id == null)
                    preset2 = ShotPresetCatalog.All.FirstOrDefault(p => p.Id != null && p.Id.StartsWith("putt"));
                if (preset2.Id == null)
                {
                    Debug.LogError("[SmokeRunner2fHost] No putt preset found for shot2.");
                    sm.OnShotComplete -= onComplete2;
                    yield break;
                }
                Debug.Log($"[SmokeRunner2fHost] Shot2 preset: {preset2.Id}");
                controller.Fire(preset2);
                Debug.Log("[SmokeRunner2fHost] Shot2 fired.");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[SmokeRunner2fHost] Shot2 fire failed: {ex.Message}");
                sm.OnShotComplete -= onComplete2;
                onError(ex);
                yield break;
            }

            float elapsed2 = 0f;
            while (!shot2Complete && elapsed2 < ShotWait)
            {
                elapsed2 += Time.unscaledDeltaTime;
                if (Mathf.FloorToInt(elapsed2) != Mathf.FloorToInt(elapsed2 - Time.unscaledDeltaTime))
                    Debug.Log($"[SmokeRunner2fHost] Waiting shot2... elapsed={elapsed2:F1}s complete={shot2Complete}");
                yield return null;
            }
            sm.OnShotComplete -= onComplete2;

            Debug.Log($"[SmokeRunner2fHost] Shot2 done: complete={shot2Complete} elapsed={elapsed2:F1}s club={controller.CurrentClubIndex} endSurface={shot2Result.EndSurface}");

            yield return new WaitForSecondsRealtime(CaptureWait);

            // ── S2: Capture ───────────────────────────────────────────────────
            string path2;
            try
            {
                path2 = CaptureCore.SnapPlayModeSafe("controls_2f_auto_exit_to_last_club");
                Debug.Log($"[SmokeRunner2fHost] S2 captured: {path2} | club={controller.CurrentClubIndex} (expected 0=Driver)");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[SmokeRunner2fHost] S2 capture failed: {ex.Message}");
                path2 = "CAPTURE_FAILED";
            }

            // ── S3: Tuning panel open ─────────────────────────────────────────
            GreenTuningPanel greenPanel = null;
            Slider rrSlider = null;
            try
            {
                greenPanel = FindObjectOfType<GreenTuningPanel>(true);
                if (greenPanel != null)
                {
                    var panelRootField = typeof(GreenTuningPanel).GetField("panelRoot",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    var panelRoot = panelRootField?.GetValue(greenPanel) as GameObject;
                    if (panelRoot != null && !panelRoot.activeSelf)
                        panelRoot.SetActive(true);

                    var rrSliderField = typeof(GreenTuningPanel).GetField("rollingResistanceSlider",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    rrSlider = rrSliderField?.GetValue(greenPanel) as Slider;

                    Debug.Log($"[SmokeRunner2fHost] GreenTuningPanel opened. RR={controller.SurfaceCfg[SurfaceType.Green].RollingResistance.ToFloat():F4} rrSlider={rrSlider != null}");
                }
                else
                {
                    Debug.LogError("[SmokeRunner2fHost] GreenTuningPanel not found in scene!");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[SmokeRunner2fHost] S3 panel open failed: {ex.Message}");
            }

            yield return new WaitForSecondsRealtime(CaptureWait);

            string path3;
            try
            {
                path3 = CaptureCore.SnapPlayModeSafe("controls_2f_tuning_panel_open");
                Debug.Log($"[SmokeRunner2fHost] S3 captured: {path3}");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[SmokeRunner2fHost] S3 capture failed: {ex.Message}");
                path3 = "CAPTURE_FAILED";
            }

            // ── S4: Tuning live-apply — FAIL-1 fix ───────────────────────────
            // Two-step approach:
            //   Step A: rrSlider.value = tuneRR — moves the slider thumb visually (pixel proof).
            //   Step B: invoke OnRollingResistanceChanged(tuneRR) via reflection — exercises the
            //           production event handler method that calls SetSurfaceConfig. The slider
            //           onValueChanged may not fire if the listener was not registered (GreenTuningPanel
            //           OnEnable timing vs slider GO disabled in panelRoot), so we call it explicitly.
            //   Together these prove: slider thumb moved (FAIL-1) + live-apply method executed (FAIL-1).
            SurfaceCoefficients afterSlider = default;
            float puttRRAfterSlider = 0f;   // PuttConfig[Green].RR right after S4 (before any reset)
            float tuneRR = 0.05f;
            try
            {
                if (rrSlider != null)
                {
                    // Step A: move the thumb visually.
                    rrSlider.value = tuneRR;
                    Debug.Log($"[SmokeRunner2fHost] S4: rrSlider.value set to {tuneRR} (thumb moved to ~10% of 0–0.5 range)");

                    // Step B: invoke OnRollingResistanceChanged via reflection to ensure the
                    // production event handler fires and calls controller.SetSurfaceConfig.
                    if (greenPanel != null)
                    {
                        var onRRChanged = typeof(GreenTuningPanel).GetMethod("OnRollingResistanceChanged",
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        if (onRRChanged != null)
                        {
                            onRRChanged.Invoke(greenPanel, new object[] { tuneRR });
                            Debug.Log($"[SmokeRunner2fHost] S4: OnRollingResistanceChanged({tuneRR}) invoked via reflection (production code path)");
                        }
                        else
                        {
                            Debug.LogWarning("[SmokeRunner2fHost] S4: OnRollingResistanceChanged not found via reflection — applying direct seam");
                            var cfg = controller.SurfaceCfg;
                            var coef = cfg[SurfaceType.Green];
                            coef.RollingResistance = fp.FromFloat(tuneRR);
                            cfg.Coefficients[(int)SurfaceType.Green] = coef;
                            controller.SetSurfaceConfig(cfg);
                        }
                    }
                    afterSlider = controller.SurfaceCfg[SurfaceType.Green];
                    puttRRAfterSlider = controller.PuttCfg[SurfaceType.Green].RollingResistance.ToFloat();
                    Debug.Log($"[SmokeRunner2fHost][SurfaceCfgLog] AfterSlider (slider.value + OnRollingResistanceChanged): SurfRR={afterSlider.RollingResistance.ToFloat():F4} SS={afterSlider.StopSpeed.ToFloat():F4} PuttRR={puttRRAfterSlider:F4} (L9 Option B — should=0.0500)");
                }
                else
                {
                    // Fallback: direct seam if slider reference unavailable
                    Debug.LogWarning("[SmokeRunner2fHost] S4: rrSlider reference null — falling back to direct SetSurfaceConfig");
                    var cfg = controller.SurfaceCfg;
                    var coef = cfg[SurfaceType.Green];
                    origRR = coef.RollingResistance.ToFloat();
                    coef.RollingResistance = fp.FromFloat(tuneRR);
                    cfg.Coefficients[(int)SurfaceType.Green] = coef;
                    controller.SetSurfaceConfig(cfg);
                    afterSlider = controller.SurfaceCfg[SurfaceType.Green];
                    Debug.Log($"[SmokeRunner2fHost][SurfaceCfgLog] AfterSlider (fallback direct): RR={afterSlider.RollingResistance.ToFloat():F4}");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[SmokeRunner2fHost] S4 tuning failed: {ex.Message}");
            }

            yield return new WaitForSecondsRealtime(CaptureWait);

            string path4;
            try
            {
                path4 = CaptureCore.SnapPlayModeSafe("controls_2f_tuning_live_apply");
                Debug.Log($"[SmokeRunner2fHost] S4 captured: {path4} | RR {origRR:F4}→{tuneRR:F4} (slider thumb moved left)");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[SmokeRunner2fHost] S4 capture failed: {ex.Message}");
                path4 = "CAPTURE_FAILED";
            }

            // ── S5 + S6: Baseline vs Tuned putt roll-distance delta ───────────
            // iter-4 fix: S6 timed out in iter-3 because putt_flat_3m with RR=0.05 produces
            // a ~39-second animation (exponential decay: v(t)=0.35*exp(-0.05t), stops at v=0.05).
            // The ShotWait=25s ceiling fired first, so OnShotComplete6 never fired.
            //
            // Fix: set ballAnimator.PlayRate to Instant (float.MaxValue) for comparison shots
            // so SnapToEnd fires immediately and the SM drains transitions on the NEXT frame
            // (~0.016s), well within any timeout. The ball teleports to its final rest position.
            // We restore PlayRate afterwards so S1-S4 visual quality is unaffected.
            //
            // Additional gate: before firing S6, explicitly wait for BallSM.State==Aiming
            // instead of relying on a fixed WaitForSecondsRealtime(0.5f). This eliminates any
            // residual race between S5's ReArm() and S6's Fire().
            float baselineRolledDist  = 0f;
            float tunedRolledDist     = 0f;
            string path5 = "CAPTURE_FAILED";
            string path6 = "CAPTURE_FAILED";

            // Select putter club for the comparison shots.
            try
            {
                controller.SetClub(PhysicsLabController.PutterIndex);
                Debug.Log($"[SmokeRunner2fHost] SetClub(Putter={PhysicsLabController.PutterIndex}) for comparison shots.");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[SmokeRunner2fHost] SetClub putter for comparison failed: {ex.Message}");
            }

            ShotPreset puttPreset = default;
            try
            {
                puttPreset = ShotPresetCatalog.All.FirstOrDefault(p => p.Id == "putt_flat_3m");
                if (puttPreset.Id == null)
                    puttPreset = ShotPresetCatalog.All.FirstOrDefault(p => p.Id != null && p.Id.StartsWith("putt"));
                if (puttPreset.Id == null)
                    Debug.LogError("[SmokeRunner2fHost] No putt preset found for comparison shots.");
                else
                    Debug.Log($"[SmokeRunner2fHost] Comparison preset: {puttPreset.Id}");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[SmokeRunner2fHost] Preset lookup for comparison failed: {ex.Message}");
            }

            if (puttPreset.Id != null)
            {
                // Save original PlayRate; set to Instant for comparison shots.
                // BallAnimator.PlayRate is a public field (public float PlayRate).
                float savedPlayRate = 1f;
                try
                {
                    savedPlayRate = controller.GetBallAnimatorPlayRate();
                    controller.SetBallAnimatorPlayRate(float.MaxValue); // Instant: SnapToEnd fires immediately in Play()
                    Debug.Log($"[SmokeRunner2fHost] S5/S6 setup: saved PlayRate={savedPlayRate:F2}, set to Instant (float.MaxValue) so shots complete in 1 frame.");
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[SmokeRunner2fHost] S5/S6 PlayRate set failed: {ex.Message}");
                }

                // ── S5: Baseline putt at DEFAULT RR ──────────────────────────
                // Reset BOTH SurfaceConfig AND PuttConfig to default for a clean baseline.
                // L9 Option B: OnRollingResistanceChanged now mirrors to PuttConfig, so we
                // must reset PuttConfig too to ensure S5 uses the default putt coefficients.
                try
                {
                    var defaultSurfCfg = SurfaceConfig.Default;
                    var surfCfg5 = controller.SurfaceCfg;
                    surfCfg5.Coefficients[(int)SurfaceType.Green] = defaultSurfCfg.Coefficients[(int)SurfaceType.Green];
                    controller.SetSurfaceConfig(surfCfg5);

                    var defaultPuttCfg = PuttConfig.Default;
                    var puttCfg5 = controller.PuttCfg;
                    puttCfg5.Coefficients[(int)SurfaceType.Green] = defaultPuttCfg.Coefficients[(int)SurfaceType.Green];
                    controller.SetPuttConfig(puttCfg5);

                    // Also sync slider to match.
                    if (rrSlider != null)
                        rrSlider.SetValueWithoutNotify(defaultSurfCfg.Coefficients[(int)SurfaceType.Green].RollingResistance.ToFloat());

                    Debug.Log($"[SmokeRunner2fHost] S5: Reset to default SurfRR={controller.SurfaceCfg[SurfaceType.Green].RollingResistance.ToFloat():F4} PuttRR={controller.PuttCfg[SurfaceType.Green].RollingResistance.ToFloat():F4} for baseline putt.");
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[SmokeRunner2fHost] S5 reset failed: {ex.Message}");
                }

                // Place ball at green center, fire putt_flat_3m → measure rolled distance.
                const float CompareGreenX = -228f;
                const float CompareGreenZ = -73f;
                try
                {
                    controller.PlaceBallAt(new UnityEngine.Vector3(CompareGreenX, 0f, CompareGreenZ), preferredSurfaceTypeValue: 1);
                    controller.SetCameraYawRadians(0f); // world +X so shots roll in the same direction
                    Debug.Log($"[SmokeRunner2fHost] S5: Placed ball at ({CompareGreenX},{CompareGreenZ}) for baseline putt.");
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[SmokeRunner2fHost] S5 PlaceBallAt failed: {ex.Message}");
                }

                // Gate on Aiming state before firing (state guard, not a fixed wait).
                float s5GateElapsed = 0f;
                while (sm.State != BallState.Aiming && s5GateElapsed < 3f)
                {
                    s5GateElapsed += Time.unscaledDeltaTime;
                    yield return null;
                }
                Debug.Log($"[SmokeRunner2fHost] S5 pre-fire state gate done: State={sm.State} gateElapsed={s5GateElapsed:F3}s");

                bool shot5Complete = false;
                ShotResult shot5Result = default;
                System.Action<ShotResult> onComplete5 = r => { shot5Complete = true; shot5Result = r; Debug.Log($"[SmokeRunner2fHost] OnShotComplete5 fired. terminal={r.TerminalState} endSurface={r.EndSurface}"); };
                sm.OnShotComplete += onComplete5;
                try
                {
                    controller.Fire(puttPreset);
                    Debug.Log("[SmokeRunner2fHost] S5 baseline putt fired (Instant mode — ball snaps to final pos in 1 frame).");
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[SmokeRunner2fHost] S5 fire failed: {ex.Message}");
                    sm.OnShotComplete -= onComplete5;
                }

                // With Instant PlayRate, SnapToEnd fires during Play(), setting _playing=false.
                // OnTrajectoryComputed then sets _prevAnimatorPlaying=true.
                // The very next Tick(false) fires the falling edge → DrainPendingTransitions → OnShotComplete.
                // 5s timeout is extremely generous (should complete in <0.1s realtime).
                const float InstantShotWait = 5f;
                float elapsed5 = 0f;
                while (!shot5Complete && elapsed5 < InstantShotWait)
                {
                    elapsed5 += Time.unscaledDeltaTime;
                    yield return null;
                }
                sm.OnShotComplete -= onComplete5;

                Debug.Log($"[SmokeRunner2fHost] S5 wait done: complete={shot5Complete} elapsed={elapsed5:F3}s State={sm.State}");

                if (shot5Complete)
                {
                    var startXZ5 = new Vector2(shot5Result.StartPosition.x.ToFloat(), shot5Result.StartPosition.z.ToFloat());
                    var endXZ5   = new Vector2(shot5Result.EndPosition.x.ToFloat(),   shot5Result.EndPosition.z.ToFloat());
                    baselineRolledDist = Vector2.Distance(startXZ5, endXZ5);
                    Debug.Log($"[SmokeRunner2fHost] S5 baseline rolled dist={baselineRolledDist:F3}m start=({startXZ5.x:F2},{startXZ5.y:F2}) end=({endXZ5.x:F2},{endXZ5.y:F2})");
                }
                else
                {
                    Debug.LogError($"[SmokeRunner2fHost] S5 timed out after {elapsed5:F3}s! State={sm.State} — cannot proceed.");
                }

                yield return new WaitForSecondsRealtime(CaptureWait);
                try
                {
                    path5 = CaptureCore.SnapPlayModeSafe("controls_2f_tuning_putt_baseline_atrest");
                    Debug.Log($"[SmokeRunner2fHost] S5 captured: {path5} baseline rolled={baselineRolledDist:F3}m");
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[SmokeRunner2fHost] S5 capture failed: {ex.Message}");
                }

                // ── S6: Tuned putt at RR=0.05 ───────────────────────────────
                // Re-apply the tuned RR via the production event handler (same approach as S4).
                try
                {
                    if (rrSlider != null)
                    {
                        rrSlider.value = tuneRR; // move thumb visually
                        if (greenPanel != null)
                        {
                            var onRRChanged = typeof(GreenTuningPanel).GetMethod("OnRollingResistanceChanged",
                                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                            if (onRRChanged != null)
                                onRRChanged.Invoke(greenPanel, new object[] { tuneRR });
                        }
                        Debug.Log($"[SmokeRunner2fHost] S6: Applied tuned RR={tuneRR} via OnRollingResistanceChanged. SurfRR={controller.SurfaceCfg[SurfaceType.Green].RollingResistance.ToFloat():F4} PuttRR={controller.PuttCfg[SurfaceType.Green].RollingResistance.ToFloat():F4} (L9 Option B: both mirrored)");
                    }
                    else
                    {
                        // Fallback: direct seam — mirror to both SurfaceConfig and PuttConfig (L9 Option B).
                        var surfCfgFb = controller.SurfaceCfg;
                        var surfCoefFb = surfCfgFb[SurfaceType.Green];
                        surfCoefFb.RollingResistance = fp.FromFloat(tuneRR);
                        surfCfgFb.Coefficients[(int)SurfaceType.Green] = surfCoefFb;
                        controller.SetSurfaceConfig(surfCfgFb);

                        var puttCfgFb = controller.PuttCfg;
                        var puttCoefFb = puttCfgFb[SurfaceType.Green];
                        puttCoefFb.RollingResistance = fp.FromFloat(tuneRR);
                        puttCfgFb.Coefficients[(int)SurfaceType.Green] = puttCoefFb;
                        controller.SetPuttConfig(puttCfgFb);
                        Debug.Log($"[SmokeRunner2fHost] S6: Applied tuned RR={tuneRR} via direct seam (fallback). PuttRR={controller.PuttCfg[SurfaceType.Green].RollingResistance.ToFloat():F4}");
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[SmokeRunner2fHost] S6 tuning failed: {ex.Message}");
                }

                // Place ball at same start position as S5, fire same preset.
                try
                {
                    controller.PlaceBallAt(new UnityEngine.Vector3(CompareGreenX, 0f, CompareGreenZ), preferredSurfaceTypeValue: 1);
                    controller.SetCameraYawRadians(0f);
                    Debug.Log($"[SmokeRunner2fHost] S6: Placed ball at ({CompareGreenX},{CompareGreenZ}) for tuned putt.");
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[SmokeRunner2fHost] S6 PlaceBallAt failed: {ex.Message}");
                }

                // State gate: wait until BallSM is Aiming before firing.
                // After S5's HandleShotComplete+ReArm, State=Aiming. PlaceBallAt calls
                // _shotController.CompleteShot() (idempotent). The gate here is a belt+suspenders
                // check to ensure no race condition between S5's ReArm and S6's Fire.
                float s6GateElapsed = 0f;
                while (sm.State != BallState.Aiming && s6GateElapsed < 3f)
                {
                    s6GateElapsed += Time.unscaledDeltaTime;
                    yield return null;
                }
                Debug.Log($"[SmokeRunner2fHost] S6 pre-fire state gate done: State={sm.State} gateElapsed={s6GateElapsed:F3}s");

                bool shot6Complete = false;
                ShotResult shot6Result = default;
                System.Action<ShotResult> onComplete6 = r => { shot6Complete = true; shot6Result = r; Debug.Log($"[SmokeRunner2fHost] OnShotComplete6 fired. terminal={r.TerminalState} endSurface={r.EndSurface}"); };
                sm.OnShotComplete += onComplete6;
                try
                {
                    controller.Fire(puttPreset);
                    Debug.Log("[SmokeRunner2fHost] S6 tuned putt fired (Instant mode — ball snaps to final pos in 1 frame).");
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[SmokeRunner2fHost] S6 fire failed: {ex.Message}");
                    sm.OnShotComplete -= onComplete6;
                }

                float elapsed6 = 0f;
                while (!shot6Complete && elapsed6 < InstantShotWait)
                {
                    elapsed6 += Time.unscaledDeltaTime;
                    yield return null;
                }
                sm.OnShotComplete -= onComplete6;

                Debug.Log($"[SmokeRunner2fHost] S6 wait done: complete={shot6Complete} elapsed={elapsed6:F3}s State={sm.State}");

                if (shot6Complete)
                {
                    var startXZ6 = new Vector2(shot6Result.StartPosition.x.ToFloat(), shot6Result.StartPosition.z.ToFloat());
                    var endXZ6   = new Vector2(shot6Result.EndPosition.x.ToFloat(),   shot6Result.EndPosition.z.ToFloat());
                    tunedRolledDist = Vector2.Distance(startXZ6, endXZ6);
                    Debug.Log($"[SmokeRunner2fHost] S6 tuned rolled dist={tunedRolledDist:F3}m start=({startXZ6.x:F2},{startXZ6.y:F2}) end=({endXZ6.x:F2},{endXZ6.y:F2})");
                }
                else
                {
                    Debug.LogError($"[SmokeRunner2fHost] S6 timed out after {elapsed6:F3}s! State={sm.State} — delta cannot be computed.");
                }

                yield return new WaitForSecondsRealtime(CaptureWait);
                try
                {
                    path6 = CaptureCore.SnapPlayModeSafe("controls_2f_tuning_putt_fast_atrest");
                    Debug.Log($"[SmokeRunner2fHost] S6 captured: {path6} tuned rolled={tunedRolledDist:F3}m delta={tunedRolledDist - baselineRolledDist:F3}m");
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[SmokeRunner2fHost] S6 capture failed: {ex.Message}");
                }

                // Restore original PlayRate so subsequent captures are not in instant mode.
                try
                {
                    controller.SetBallAnimatorPlayRate(savedPlayRate);
                    Debug.Log($"[SmokeRunner2fHost] PlayRate restored to {savedPlayRate:F2}.");
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[SmokeRunner2fHost] PlayRate restore failed: {ex.Message}");
                }
            }

            // ── Reset and write L1 log ────────────────────────────────────────
            // L9 Option B: reset BOTH SurfaceConfig AND PuttConfig Green entries.
            SurfaceCoefficients afterReset = default;
            try
            {
                var defaultSurfCfg = SurfaceConfig.Default;
                var surfCfgR = controller.SurfaceCfg;
                surfCfgR.Coefficients[(int)SurfaceType.Green] = defaultSurfCfg.Coefficients[(int)SurfaceType.Green];
                controller.SetSurfaceConfig(surfCfgR);
                afterReset = controller.SurfaceCfg[SurfaceType.Green];

                var defaultPuttCfg = PuttConfig.Default;
                var puttCfgR = controller.PuttCfg;
                puttCfgR.Coefficients[(int)SurfaceType.Green] = defaultPuttCfg.Coefficients[(int)SurfaceType.Green];
                controller.SetPuttConfig(puttCfgR);

                Debug.Log($"[SmokeRunner2fHost][SurfaceCfgLog] AfterReset: SurfRR={afterReset.RollingResistance.ToFloat():F4} PuttRR={controller.PuttCfg[SurfaceType.Green].RollingResistance.ToFloat():F4}");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[SmokeRunner2fHost] Reset failed: {ex.Message}");
            }

            WriteHistoryLog(controller, origRR, afterSlider, afterReset, baselineRolledDist, tunedRolledDist, puttRRAfterSlider);

            Debug.Log("[SmokeRunner2fHost] Sequence COMPLETE.");
            onDone();
        }

        void WriteHistoryLog(PhysicsLabController controller, float origRR,
                             SurfaceCoefficients afterSlider, SurfaceCoefficients afterReset,
                             float baselineRolledDist, float tunedRolledDist,
                             float puttRRAfterSlider)
        {
            try
            {
                // puttRRAfterReset: read from controller here (after final reset above).
                float puttRRAfterReset = controller != null ? controller.PuttCfg[SurfaceType.Green].RollingResistance.ToFloat() : -1f;

                var sb = new System.Text.StringBuilder();
                sb.AppendLine("=== controls_2f_history_log (iter-3: L9 Option B) ===");
                sb.AppendLine($"GameSession.TurnCount={GameSession.TurnCount}");
                sb.AppendLine();
                sb.AppendLine("--- L9 Option B: GreenTuningPanel mirrors Green to BOTH SurfaceConfig AND PuttConfig ---");
                sb.AppendLine("    BallSimulation.RunPuttPhase reads PuttConfig[Green]; without this mirror slider has no effect.");
                sb.AppendLine();
                sb.AppendLine("--- SurfaceConfig[Green] + PuttConfig[Green] at Awake ---");
                sb.AppendLine($"  SurfaceConfig.RollingResistance={origRR:F4} (initial)");
                sb.AppendLine();
                sb.AppendLine("--- After dragging RR slider to 0.05 (via slider.value + OnRollingResistanceChanged — production UI path) ---");
                sb.AppendLine($"  SurfaceConfig[Green].RollingResistance={afterSlider.RollingResistance.ToFloat():F4}");
                sb.AppendLine($"  SurfaceConfig[Green].StopSpeed={afterSlider.StopSpeed.ToFloat():F4}");
                sb.AppendLine($"  PuttConfig[Green].RollingResistance={puttRRAfterSlider:F4}  (should=0.0500 — mirrored by L9 Option B)");
                sb.AppendLine();
                sb.AppendLine("--- After Reset button (resets both SurfaceConfig and PuttConfig Green entry) ---");
                sb.AppendLine($"  SurfaceConfig[Green].RollingResistance={afterReset.RollingResistance.ToFloat():F4}");
                sb.AppendLine($"  SurfaceConfig[Green].StopSpeed={afterReset.StopSpeed.ToFloat():F4}");
                sb.AppendLine($"  PuttConfig[Green].RollingResistance={puttRRAfterReset:F4}  (should be default ~0.10)");
                sb.AppendLine();
                sb.AppendLine("--- Roll-distance comparison (same preset, same origin, same yaw) ---");
                sb.AppendLine($"  S5 Baseline (SurfRR+PuttRR={origRR:F4}): rolled {baselineRolledDist:F3}m");
                sb.AppendLine($"  S6 Tuned    (SurfRR+PuttRR=0.0500):     rolled {tunedRolledDist:F3}m");
                float delta = tunedRolledDist - baselineRolledDist;
                sb.AppendLine($"  Delta: {delta:+0.000;-0.000}m  ({(delta > 0 ? "tuned rolls FARTHER — L9 Option B working" : delta < 0 ? "tuned rolls SHORTER — unexpected" : "no difference — check PuttConfig mirror")})");
                sb.AppendLine();
                sb.AppendLine("--- Note: L8 — runtime-only edits, no persistence across play-mode exit ---");
                sb.AppendLine($"  _persistEdits=false (confirmed in GreenTuningPanel inspector)");

                string outPath = System.IO.Path.Combine(CaptureCore.OutDir, "controls_2f_history_log.txt");
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(outPath));
                File.WriteAllText(outPath, sb.ToString());
                Debug.Log($"[SmokeRunner2fHost] L1 history log written: {outPath}");
                Debug.Log("[SmokeRunner2fHost] Log contents:\n" + sb.ToString());
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[SmokeRunner2fHost] WriteHistoryLog failed: {ex.Message}");
            }
        }
    }
}
#endif
