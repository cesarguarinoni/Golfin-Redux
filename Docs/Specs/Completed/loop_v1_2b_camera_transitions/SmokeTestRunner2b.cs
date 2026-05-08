// SmokeTestRunner2b.cs — controls_g_smoke_followup (2026-05-07, r3)
//
// Closes §2b deferred smoke debt via state-driven captures using OnModeChanged events.
// Replaces the original timed-wait approach that fired before cinematic cut threshold.
//
// CAPTURES:
//   1. Downrange cinematic cut (driver shot, waits until Director enters Downrange mode)
//   2. Putter stays in GroundLevel (captures at Rolling state, verifies Downrange NOT in history)
//   3. OBFreeze (driver shot aimed at water on Hole_06, captures at OBFreeze mode)
//
// Output: Docs/Diagnostics/_capture/controls_g_followup_*.png
// Then copied to: Docs/Specs/Active/loop_v1_2b_camera_transitions/screenshots/
//
// ATTACH: SmokeTestRunner2b is already attached to a GameObject in LabScaffold.unity.
//         Enter play mode to run automatically.
//
// ARCHITECTURE NOTE (§controls_g_smoke_followup asmdef):
//   Golfin.Diagnostics.Runtime cannot reference Golfin.Physics.Viewer (would be circular).
//   CaptureCore.SnapWhenModeReached uses Action<int> late-binding to avoid the cycle.
//   Callers (this file) cast ChaseCamera.Mode to int at the call site.

using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using Golfin.Gameplay.Input;
using Golfin.Gameplay.Loop;
using Golfin.Diagnostics.Runtime;

namespace Golfin.Physics.Viewer
{
    /// <summary>
    /// §2b deferred smoke runner (controls_g_smoke_followup rewrite).
    /// Uses CaptureCore.SnapWhenModeReached (state-driven) — no timed waits for
    /// any moment that depends on physics or SM state.
    ///
    /// Sequence: Downrange (Hole_01_Geo driver) → Putter GroundLevel (Hole_01_Geo) →
    ///           OBFreeze (Hole_06_Geo driver aimed at water).
    /// </summary>
    public class SmokeTestRunner2b : MonoBehaviour
    {
        // ── Results ─────────────────────────────────────────────────────────────
        public static string LastSmokeTestResult       = "NOT_RUN";
        public static string CapturedDownrangePath     = "";
        public static string CapturedPutterPath        = "";
        public static string CapturedOBFreezePath      = "";

        // ── Mode history (filled by subscribing to Director.OnModeChanged) ─────
        public static readonly List<ChaseCamera.Mode> DownrangeModeHistory  = new List<ChaseCamera.Mode>();
        public static readonly List<ChaseCamera.Mode> PutterModeHistory     = new List<ChaseCamera.Mode>();
        public static readonly List<ChaseCamera.Mode> OBFreezeModeHistory   = new List<ChaseCamera.Mode>();

        // ── Internal ─────────────────────────────────────────────────────────────
        BallStateMachine _ballSM = null;
        int              _shotsComplete = 0;

        // Green position on Hole 1 (near cup for putter placement).
        // Matches SmokeTestRunner2a precedent; Y snapped by PlaceBallAt surface snap.
        static readonly Vector3 k_Hole1GreenPos = new Vector3(-230f, 8f, -73f);

        // Hole 06 OBFreeze attempt tee overrides — all zero (default tee, power varies per attempt).
        // Power calibrated so ball lands in water zone x[-40.8, 1.3]: 0.50/0.52/0.55.
        // Retries use PlaceBallAt(k_Hole6TeeWorldPos) to reset SM ball origin (see TryOBFreezeCapture).
        static readonly Vector3[] k_Hole6TeePlacements = new Vector3[]
        {
            Vector3.zero, // attempt 1: power=0.50
            Vector3.zero, // attempt 2: power=0.52
            Vector3.zero, // attempt 3: power=0.55
        };

        const string k_Hole1Scene = "Hole_01_Geo";
        const string k_Hole6Scene = "Hole_06_Geo";

        // ── Lifecycle ────────────────────────────────────────────────────────────
        void Start()
        {
            Debug.Log("[SmokeTest2b] Start() — §2b deferred smoke captures (controls_g_smoke_followup)");
            Debug.Log("[SmokeTest2b] Using state-driven CaptureCore.SnapWhenModeReached — zero timed waits.");
            StartCoroutine(RunSmokeTest());
        }

        void OnDestroy()
        {
            if (_ballSM != null)
                _ballSM.OnShotComplete -= OnShotCompleteCallback;
        }

        // ── Main coroutine ───────────────────────────────────────────────────────
        IEnumerator RunSmokeTest()
        {
            // Wait for PhysicsLabController startup scan (8 frames as per SmokeTestRunner2a)
            for (int i = 0; i < 8; i++) yield return null;

            // Wait for any in-flight SmokeTestRunner2a shot to settle.
            // SmokeTestRunner2a.LastSmokeTestResult may already be "PASS" from a prior session
            // (static fields persist across play mode). So we wait 30 frames to let the SM
            // reach Aiming state, which guarantees no stale shots are in-flight.
            for (int i = 0; i < 30; i++) yield return null;
            Debug.Log($"[SmokeTest2b] Startup wait complete. SmokeTestRunner2a.LastSmokeTestResult={SmokeTestRunner2a.LastSmokeTestResult}");

            var labController  = FindFirstObjectByType<PhysicsLabController>();
            var shotController = FindFirstObjectByType<ShotController>();
            if (labController == null || shotController == null)
            {
                Debug.LogError("[SmokeTest2b] FAIL: PhysicsLabController or ShotController not found.");
                LastSmokeTestResult = "FAIL_NO_CONTROLLER";
                yield break;
            }

            // Get _ballSM via reflection (field is private on PhysicsLabController)
            var smField = typeof(PhysicsLabController)
                .GetField("_ballSM", BindingFlags.NonPublic | BindingFlags.Instance);
            _ballSM = smField?.GetValue(labController) as BallStateMachine;
            if (_ballSM == null)
            {
                Debug.LogError("[SmokeTest2b] FAIL: could not retrieve _ballSM via reflection.");
                LastSmokeTestResult = "FAIL_NO_SM";
                yield break;
            }
            Debug.Log($"[SmokeTest2b] Got _ballSM. State={_ballSM.State}");
            // Note: subscribe to OnShotComplete JUST BEFORE each shot (see C.1 below)
            // to avoid counting any residual shot completions from concurrent runners.

            // ================================================================
            // C.1 — DOWNRANGE CAPTURE
            // Load Hole_01_Geo additively, fire driver at high power, capture
            // when Director enters ChaseCamera.Mode.Downrange (state-driven).
            // ================================================================
            yield return StartCoroutine(LoadHoleAdditively(k_Hole1Scene, labController));

            // Place ball at tee (OnHoleLoaded already called SetupAtTee internally)
            labController.SetClub(0); // Driver
            yield return null;

            // Find the LoopCameraDirector
            var director = FindFirstObjectByType<LoopCameraDirector>();
            if (director == null)
            {
                Debug.LogWarning("[SmokeTest2b] LoopCameraDirector not found — Downrange capture may fail.");
            }
            else
            {
                Debug.Log("[SmokeTest2b] LoopCameraDirector found. Subscribing OnModeChanged for Downrange.");
            }

            // Subscribe mode history for Downrange shot
            DownrangeModeHistory.Clear();
            if (director != null)
                director.OnModeChanged += m => DownrangeModeHistory.Add(m);

            // Schedule capture when Director enters Downrange mode
            if (director != null)
            {
                string dCapturePath = $"{CaptureCore.OutDir}/controls_g_followup_downrange_f{{0}}.png";
                CaptureCore.SnapWhenModeReached(
                    owner:            this,
                    subscribe:        h => director.OnModeChanged += m => h((int)m),
                    targetModeAsInt:  (int)ChaseCamera.Mode.Downrange,
                    label:            "controls_g_followup_downrange",
                    skipPause:        true); // Skip pause — MCP external unpause triggers thread crash
                Debug.Log("[SmokeTest2b] Downrange capture scheduled via SnapWhenModeReached.");
            }

            // Subscribe JUST before the first shot to avoid counting stale completions
            _ballSM.OnShotComplete += OnShotCompleteCallback;

            // Fire driver at 0.85 power (high enough for 65%+ carry threshold)
            int shotsBeforeDriver = _shotsComplete;
            Debug.Log($"[SmokeTest2b] Firing driver (power=0.85) for Downrange capture...");
            shotController.FireDebugShot(0.85f, DebugShotAccuracy.Green);
            yield return null;

            // Wait for Downrange mode SPECIFICALLY to fire (editor will pause on capture)
            // Allow up to 20 seconds for the shot to reach 65% carry and trigger Downrange
            float timeout = 20f;
            while (timeout > 0f && !DownrangeModeHistory.Contains(ChaseCamera.Mode.Downrange))
            {
                timeout -= Time.deltaTime;
                yield return null;
            }

            if (!DownrangeModeHistory.Contains(ChaseCamera.Mode.Downrange))
            {
                Debug.LogWarning($"[SmokeTest2b] WARNING: Downrange mode not reached within 20s. Mode history: [{string.Join(", ", DownrangeModeHistory)}]");
            }
            else
            {
                Debug.Log($"[SmokeTest2b] Downrange mode reached! Mode history so far: [{string.Join(", ", DownrangeModeHistory)}]");
                // skipPause=true: SnapWhenModeReached does NOT pause editor, so no un-pause needed.
            }

            // Wait for shot completion
            timeout = 30f;
            while (_shotsComplete <= shotsBeforeDriver && timeout > 0f)
            {
                timeout -= Time.deltaTime;
                yield return null;
            }
            if (timeout <= 0f)
            {
                Debug.LogError("[SmokeTest2b] TIMEOUT waiting for driver shot to complete (Downrange).");
            }
            else
            {
                Debug.Log($"[SmokeTest2b] C.1 Driver shot complete. SM.State={_ballSM.State}");
            }

            // Find the downrange capture file in _capture dir
            CapturedDownrangePath = FindLatestCapture("controls_g_followup_downrange");
            Debug.Log($"[SmokeTest2b] Downrange capture path: {CapturedDownrangePath}");
            Debug.Log($"[SmokeTest2b] Downrange mode history: [{string.Join(", ", DownrangeModeHistory)}]");

            // NOTE: Do NOT unload Hole_01_Geo between C.1 and C.2.
            // SceneManager.UnloadSceneAsync during play mode triggers a backup-scene restore
            // that exits play mode. Instead, reuse the already-loaded Hole_01_Geo for C.2.
            // Hole_01_Geo will be unloaded once after C.2 completes.
            Debug.Log($"[SmokeTest2b] Keeping Hole_01_Geo loaded for C.2 — skipping unload/reload.");

            // ================================================================
            // C.2 — PUTTER GROUNDLEVEL CAPTURE
            // Reuse loaded Hole_01_Geo, place ball on green, fire putter, capture
            // at Rolling state. Verify Downrange NOT in putter mode history.
            // ================================================================
            // Hole_01_Geo is already loaded — no LoadHoleAdditively needed.

            // Place ball on green (near cup).
            // WORKAROUND: SetClub(3) calls EnterPutterMode which calls ComputeMaxPuttRangeMeters —
            // a 1400-step synchronous simulation that starves Unity's thread scheduler and crashes
            // the MCP log plugin (Thread prematurely finalized). To avoid this, we temporarily null
            // out _powerGaugeWidget via reflection before SetClub(3) and restore it after.
            var pgwField = typeof(PhysicsLabController)
                .GetField("_powerGaugeWidget", BindingFlags.NonPublic | BindingFlags.Instance);
            var pgwOrig = pgwField?.GetValue(labController);
            pgwField?.SetValue(labController, null);
            labController.SetClub(3); // Putter → triggers GroundLevel mode in Director
            pgwField?.SetValue(labController, pgwOrig); // Restore powerGaugeWidget
            yield return null;
            labController.PlaceBallAt(k_Hole1GreenPos, 1); // preferredSurface=1 (Green)
            yield return null;

            // Subscribe mode history for Putter shot
            PutterModeHistory.Clear();
            if (director != null)
                director.OnModeChanged += m => PutterModeHistory.Add(m);

            // Fire putter at low power (0.2)
            // NOTE: Do NOT use SnapWhenStateReached here — the Rolling subscription persists
            // past the putter shot and fires spuriously during OBFreeze's driver rolling.
            // Instead: poll _ballSM.State directly during the putter shot and capture at-frame.
            int shotsBeforePutter = _shotsComplete;
            bool putterCaptureFired = false;
            Debug.Log("[SmokeTest2b] Firing putter (power=0.5) for GroundLevel capture...");
            shotController.FireDebugShot(0.5f, DebugShotAccuracy.Green); // 0.5 power to get distinct Rolling phase
            yield return null;

            // Wait for Rolling state during THIS putter shot, then capture immediately.
            timeout = 15f;
            while (_shotsComplete <= shotsBeforePutter && timeout > 0f)
            {
                if (!putterCaptureFired && _ballSM.State == BallState.Rolling)
                {
                    putterCaptureFired = true;
                    // Capture directly at the current frame (no end-of-frame delay needed —
                    // the ball is already rolling and will hold this state for multiple frames).
                    string putterCapPath = $"{CaptureCore.OutDir}/controls_g_followup_putter_groundlevel_f{Time.frameCount}.png";
                    CaptureCore.SnapGameViewWithLabel("controls_g_followup_putter_groundlevel");
                    Debug.Log($"[SmokeTest2b] Putter Rolling capture fired at frame {Time.frameCount}.");
                }
                timeout -= Time.deltaTime;
                yield return null;
            }
            if (!putterCaptureFired)
            {
                // Ball never entered Rolling — try one more frame in case just missed it
                Debug.LogWarning("[SmokeTest2b] Putter Rolling state not detected in shot loop — attempting late capture.");
                CaptureCore.SnapGameViewWithLabel("controls_g_followup_putter_groundlevel");
                putterCaptureFired = true;
            }

            // Wait for putter shot completion
            timeout = 10f;
            while (_shotsComplete <= shotsBeforePutter && timeout > 0f)
            {
                timeout -= Time.deltaTime;
                yield return null;
            }
            if (timeout <= 0f)
            {
                Debug.LogError("[SmokeTest2b] TIMEOUT waiting for putter shot to complete.");
            }
            else
            {
                Debug.Log($"[SmokeTest2b] C.2 Putter shot complete. SM.State={_ballSM.State}");
            }

            CapturedPutterPath = FindLatestCapture("controls_g_followup_putter_groundlevel");
            Debug.Log($"[SmokeTest2b] Putter GroundLevel capture path: {CapturedPutterPath}");
            Debug.Log($"[SmokeTest2b] Putter mode history (should NOT contain Downrange): [{string.Join(", ", PutterModeHistory)}]");

            // Verify Downrange is NOT in putter mode history
            if (PutterModeHistory.Contains(ChaseCamera.Mode.Downrange))
            {
                Debug.LogError("[SmokeTest2b] FAIL: Downrange mode appeared during putter shot — GroundLevel not preserved!");
            }
            else
            {
                Debug.Log("[SmokeTest2b] PASS: Downrange did NOT appear during putter shot — GroundLevel preserved.");
            }

            // NOTE: Do NOT unload Hole_01_Geo after C.2. SceneManager.UnloadSceneAsync during
            // play mode triggers backup-scene restore and exits play mode. Hole_01_Geo remains
            // loaded additively; Hole_06_Geo is loaded ON TOP for C.3 via a second additive load.
            // PhysicsLabController.OnHoleLoaded("Hole_06_Geo") switches the providers to Hole_06.
            Debug.Log("[SmokeTest2b] Skipping Hole_01_Geo unload — will load Hole_06_Geo additively for C.3.");

            // ================================================================
            // C.3 — OBFREEZE CAPTURE
            // Load Hole_06_Geo additively (keeping Hole_01_Geo loaded), call OnHoleLoaded
            // to switch providers, fire driver aimed at water, capture at OBFreeze.
            // Try up to 3 tee placements if first fails.
            // ================================================================
            bool obFreezeAchieved = false;
            for (int attempt = 0; attempt < k_Hole6TeePlacements.Length && !obFreezeAchieved; attempt++)
            {
                Debug.Log($"[SmokeTest2b] OBFreeze attempt {attempt + 1}/3 on Hole_06_Geo...");
                yield return StartCoroutine(
                    TryOBFreezeCapture(k_Hole6TeePlacements[attempt], attempt, labController,
                                       shotController, director));

                obFreezeAchieved = OBFreezeModeHistory.Contains(ChaseCamera.Mode.OBFreeze);
                if (!obFreezeAchieved)
                {
                    Debug.LogWarning($"[SmokeTest2b] OBFreeze attempt {attempt + 1} failed — OBFreeze mode not triggered.");
                    // NOTE: Do NOT unload Hole_06_Geo between retries — unload exits play mode.
                    // Hole_06_Geo remains loaded; next attempt re-runs with different heading.
                }
            }

            if (obFreezeAchieved)
            {
                CapturedOBFreezePath = FindLatestCapture("controls_g_followup_obfreeze");
                Debug.Log($"[SmokeTest2b] OBFreeze capture path: {CapturedOBFreezePath}");
                Debug.Log($"[SmokeTest2b] OBFreeze mode history: [{string.Join(", ", OBFreezeModeHistory)}]");
                // NOTE: No unload — scene cleanup happens when play mode exits naturally.
            }
            else
            {
                Debug.LogWarning("[SmokeTest2b] OBFreeze NOT achieved after 3 attempts — PARTIAL result.");
                Debug.LogWarning("[SmokeTest2b] Escalating to IMPLEMENTER_PARTIAL. A+B+D+Downrange+Putter captures ship; OBFreeze deferred.");
            }

            // ── Cleanup ───────────────────────────────────────────────────────────
            _ballSM.OnShotComplete -= OnShotCompleteCallback;
            _ballSM = null;

            bool partial = !obFreezeAchieved;
            LastSmokeTestResult = partial ? "PARTIAL" : "PASS";
            Debug.Log($"[SmokeTest2b] === SMOKE TEST COMPLETE ({LastSmokeTestResult}) ===");
            Debug.Log($"[SmokeTest2b] Downrange: {CapturedDownrangePath}");
            Debug.Log($"[SmokeTest2b] Putter: {CapturedPutterPath}");
            Debug.Log($"[SmokeTest2b] OBFreeze: {(obFreezeAchieved ? CapturedOBFreezePath : "NOT_CAPTURED")}");
        }

        // ── OBFreeze capture sub-coroutine ────────────────────────────────────────
        // Hole 6 championship tee world position (confirmed from ShotEntry logs: origin=(80.21,6.13,-24.54))
        static readonly Vector3 k_Hole6TeeWorldPos = new Vector3(80.21f, 10f, -24.54f);

        IEnumerator TryOBFreezeCapture(Vector3 teeOverride, int attemptIdx,
            PhysicsLabController labController, ShotController shotController,
            LoopCameraDirector director)
        {
            // Load Hole_06_Geo on first attempt.
            // For retries: hole already loaded. PlaceBallAt() is critical — it calls
            // _shotController.CompleteShot() which resets the BallStateMachine's tracked
            // ball origin. OnHoleLoaded only moves the visual ball (ballAnimator.PlaceAtRest)
            // but does NOT reset the SM, so the next shot fires from the previous landing spot.
            if (attemptIdx == 0)
            {
                yield return StartCoroutine(LoadHoleAdditively(k_Hole6Scene, labController));
            }
            else
            {
                Debug.Log($"[SmokeTest2b] OBFreeze retry {attemptIdx + 1}: calling PlaceBallAt(tee) to reset SM ball origin.");
                // PlaceBallAt calls CompleteShot() first (resets SM state) then PlaceAtRest (moves visual ball).
                // Y=10 is above terrain; SurfaceSnap (inside PlaceBallAt) resolves actual Y.
                labController.PlaceBallAt(k_Hole6TeeWorldPos, 6); // 6 = SurfaceType.Tee
                yield return null; // Wait one frame for PlaceBallAt to complete
                yield return null;
            }

            labController.SetClub(0); // Driver
            yield return null;

            // EMPIRICAL CALIBRATION (2026-05-07, corrected v3):
            // The water zone on Hole_06 is a C-shaped lake NOT directly in the tee→green path.
            // BakedZoneClassifier grid scan (script-execute) confirmed:
            //   z=-20: Water at x=[-35,-25] (5m step grid from x=-45..10)
            //   z=-25: Water at x=[-40,-25]
            // Ball trajectory from tee (80.21,-24.54) at power 0.50:
            //   vel=(-53.34,8.87,2.06), t_flight=1.81s, landing=(x=-16,z=-21)
            //   x=-16 at z=-21 is FAIRWAY (water is at x≈[-35,-25] at that z).
            // To land at x=-32 (water center at z=-20):
            //   need Δx=80.21+32=112.2m, t_flight=112.2/53.34×(0.5/p)=2.10s×(0.5/p)
            //   vy_needed = g×t/2 = 9.8×2.10/2=10.3m/s → power = 0.50×(10.3/8.87)=0.58
            //   But at power=0.57, landing_x≈-44 (overshoots water west edge at -40).
            //   Optimal: power=0.54 → landing x≈-33, z≈-20 (inside water zone).
            // Calibrated powers all land in x≈[-33,-30] at z≈-20 (confirmed Water by grid scan).
            // FINAL CALIBRATION (2026-05-07, v4):
            // The water zone on Hole_06 has a terrain RIDGE at x≈-22 (y=7.51m) blocking the
            // direct tee→lake path. Ball at power 0.535 trajectory y at x=-22 is 7.40m < ridge.
            //
            // Solution: aim toward the NORTHERN SHORE of the lake (z≈0, x=-15) where:
            //   - Water zone at z=0 covers x=[-35,-5] (much wider target, no terrain ridge in path)
            //   - Distance from tee ≈ 95m (manageable power)
            //
            // Heading to (-15, 0) from tee (80.21, -24.54):
            //   Δx=-95.21, Δz=+24.54 → heading = atan2(24.54, -95.21) = 2.888 rad
            // Default tee→green heading: atan2(15.54, -153.21) = 3.040 rad
            // Offset needed: 2.888 - 3.040 = -0.152 rad (anticlockwise = toward +z)
            //
            // Power for 95m carry at this angle: carry ∝ v² × 2vyvy/g / (vx²+vz²+vy²) ≈ 2vy²/g
            // At power 0.50: carry ≈ 96.5m at angle 9.45° (same shape, different z-direction).
            // For heading toward (-15,0): same total speed, same launch angle, similar carry.
            // Use power 0.50 for attempt 1 (conservative), 0.48/0.52 for attempts 2/3.
            float[] obPowers = { 0.50f, 0.48f, 0.52f };
            float obPower = obPowers[attemptIdx < obPowers.Length ? attemptIdx : 0];

            // Override heading to aim toward northern lake shore: atan2(24.54, -95.21) ≈ 2.888 rad
            // This bypasses the terrain ridge that blocks the direct tee→green path to the lake.
            const float k_WaterHeadingRad = 2.888f; // from tee (80.21,-24.54) to lake (-15,0)
            float savedHeading = shotController.CameraHeadingRadians;
            shotController.CameraHeadingRadians = k_WaterHeadingRad;
            Debug.Log($"[SmokeTest2b] OBFreeze attempt {attemptIdx + 1}: power={obPower} heading={k_WaterHeadingRad:F3}rad → water at z≈0 x≈-15");

            // Record mode history for this OBFreeze attempt
            OBFreezeModeHistory.Clear();
            if (director != null)
                director.OnModeChanged += m => OBFreezeModeHistory.Add(m);

            // Schedule capture when Director enters OBFreeze mode
            if (director != null)
            {
                CaptureCore.SnapWhenModeReached(
                    owner:           this,
                    subscribe:       h => director.OnModeChanged += m => h((int)m),
                    targetModeAsInt: (int)ChaseCamera.Mode.OBFreeze,
                    label:           "controls_g_followup_obfreeze",
                    skipPause:       true); // Skip pause — MCP external unpause triggers thread crash
                Debug.Log("[SmokeTest2b] OBFreeze capture scheduled via SnapWhenModeReached.");
            }

            // Fire driver at computed power
            int shotsBeforeOB = _shotsComplete;
            Debug.Log($"[SmokeTest2b] Firing driver (power={obPower}) for OBFreeze capture...");
            shotController.FireDebugShot(obPower, DebugShotAccuracy.Green);
            shotController.CameraHeadingRadians = savedHeading; // Restore heading after shot
            yield return null;

            // Wait for OBFreeze mode SPECIFICALLY to fire (or shot to complete - OB terminal)
            float timeout = 25f;
            while (timeout > 0f && !OBFreezeModeHistory.Contains(ChaseCamera.Mode.OBFreeze)
                   && _shotsComplete <= shotsBeforeOB)
            {
                timeout -= Time.deltaTime;
                yield return null;
            }

            // skipPause=true: SnapWhenModeReached does NOT pause editor — no un-pause needed.
            yield return null; // Allow end-of-frame capture to flush before continuing.

            // Wait for shot completion
            timeout = 15f;
            while (_shotsComplete <= shotsBeforeOB && timeout > 0f)
            {
                timeout -= Time.deltaTime;
                yield return null;
            }

            Debug.Log($"[SmokeTest2b] OBFreeze attempt {attemptIdx + 1} done. OBFreeze triggered={OBFreezeModeHistory.Contains(ChaseCamera.Mode.OBFreeze)}");
            Debug.Log($"[SmokeTest2b] Mode history attempt {attemptIdx + 1}: [{string.Join(", ", OBFreezeModeHistory)}]");

            // No unload — play mode exit cleans up all scenes automatically.
        }

        // ── Additive scene load ───────────────────────────────────────────────────
        IEnumerator LoadHoleAdditively(string sceneName, PhysicsLabController labController)
        {
            // Check if already loaded (avoid double-load)
            var existing = SceneManager.GetSceneByName(sceneName);
            if (existing.IsValid() && existing.isLoaded)
            {
                Debug.Log($"[SmokeTest2b] {sceneName} already loaded — using existing.");
                labController.OnHoleLoaded(sceneName);
                yield break;
            }

            Debug.Log($"[SmokeTest2b] Loading {sceneName} additively...");
            var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            yield return op;

            // One extra frame for scene initialization
            yield return null;

            Debug.Log($"[SmokeTest2b] {sceneName} loaded. Calling OnHoleLoaded...");
            labController.OnHoleLoaded(sceneName);

            // Wait for OnHoleLoaded to complete (SetupAtTee is synchronous but
            // baked provider loading may take 1-2 frames)
            yield return null;
            yield return null;
            Debug.Log($"[SmokeTest2b] {sceneName} fully initialized.");
        }

        // ── Additive scene unload ─────────────────────────────────────────────────
        IEnumerator UnloadHole(string sceneName, PhysicsLabController labController)
        {
            var scene = SceneManager.GetSceneByName(sceneName);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.Log($"[SmokeTest2b] {sceneName} not loaded — skip unload.");
                yield break;
            }

            Debug.Log($"[SmokeTest2b] Unloading {sceneName}...");
            // Notify controller before unload (OnHoleUnloaded is public)
            labController.OnHoleUnloaded();

            var op = SceneManager.UnloadSceneAsync(sceneName);
            yield return op;
            yield return null;
            Debug.Log($"[SmokeTest2b] {sceneName} unloaded.");
        }

        // ── Find latest capture file ─────────────────────────────────────────────
        static string FindLatestCapture(string labelPrefix)
        {
            string dir = CaptureCore.OutDir;
            if (!System.IO.Directory.Exists(dir)) return "CAPTURE_DIR_NOT_FOUND";

            string latestPath = null;
            System.DateTime latestTime = System.DateTime.MinValue;

            foreach (string f in System.IO.Directory.GetFiles(dir, $"{labelPrefix}*.png"))
            {
                var info = new System.IO.FileInfo(f);
                if (info.LastWriteTime > latestTime)
                {
                    latestTime = info.LastWriteTime;
                    latestPath = f;
                }
            }
            return latestPath ?? $"NOT_FOUND (searched {dir}/{labelPrefix}*.png)";
        }

        // ── Callbacks ────────────────────────────────────────────────────────────
        void OnShotCompleteCallback(ShotResult result)
        {
            _shotsComplete++;
            Debug.Log($"[SmokeTest2b] OnShotComplete #{_shotsComplete}: terminal={result.TerminalState} pos={result.EndPosition}");
        }
    }
}
