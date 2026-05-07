// SmokeTestRunner2a.cs
// Iteration 4 — callback-driven 3-shot smoke test for loop_v1_2a_ball_state_machine.
//
// WHAT THIS DOES:
//   Drives 3 real shots via ShotController.FireDebugShot (the real flick path:
//     FireDebugShot → CommitFlick → OnShotResolved → HandleShotResolved → _ballSM.OnTrajectoryComputed)
//   Waits for each shot to reach terminal state via BallStateMachine.OnShotComplete event (callback).
//   For shot 3 (putter), manually positions the ball near the green using PlaceAtRest()
//   so the camera shows ball on green, not OB.
//
// §2b: now delegates to Golfin.Diagnostics.Runtime.CaptureCore.SnapAtEndOfFrameAndPause,
//   which lives in a runtime-accessible assembly. The inline capture method below is
//   preserved as a fallback comment but the active path calls CaptureCore directly.
//
// ITERATION HISTORY:
//   Iter 1: smoke ran via inline script-execute; file not persisted to disk.
//   Iter 2: smoke ran via inline script-execute; screenshot was stale pre-shot frame.
//   Iter 3: this file was written via script-execute reflection (in-memory only);
//            the .cs file was never committed to disk or to git. Cesar's post-approval
//            find . -name "SmokeTestRunner*" confirmed zero on-disk results.
//   Iter 4: file written via Write tool to disk (worktree path AND main repo path),
//            confirmed with ls before running smoke. Smoke driven from compiled assembly.

using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using Golfin.Gameplay.Input;
using Golfin.Gameplay.Loop;
using Golfin.Diagnostics.Runtime;

namespace Golfin.Physics.Viewer
{
    /// <summary>
    /// Smoke-test MonoBehaviour for §2a. Attach to any GameObject in LabScaffold.unity,
    /// enter play mode, and it fires 3 shots capturing the 3rd shot at-rest frame automatically.
    ///
    /// This file is kept in the repository (Assets/Scripts/Physics/Viewer/SmokeTestRunner2a.cs)
    /// so the smoke-test path is auditable. Iteration 4 persists this file to disk via the
    /// Write tool (not script-execute reflection) to satisfy Cesar's audit requirement.
    /// </summary>
    public class SmokeTestRunner2a : MonoBehaviour
    {
        // ── Results (readable after the test completes) ──────────────────────────
        public static string LastSmokeTestResult    = "NOT_RUN";
        public static string CapturedScreenshotPath = "";

        // ── Internal bookkeeping ─────────────────────────────────────────────────
        int              _shotsComplete     = 0;    // incremented by OnShotComplete callback
        BallStateMachine _ballSM            = null;

        // Green position for Hole 1 (from ShotPresetCatalog; Y snapped at runtime by the ball animator)
        static readonly Vector3 k_GreenPos = new Vector3(-230f, 8f, -73f);

        // ── Lifecycle ────────────────────────────────────────────────────────────
        void Start()
        {
            Debug.Log("[SmokeTest2a] Start() — beginning 3-shot smoke test (callback-driven, iteration 4)");
            Debug.Log("[SmokeTest2a] File is persisted to disk at Assets/Scripts/Physics/Viewer/SmokeTestRunner2a.cs");
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
            // Step 0: wait for PhysicsLabController.ScanForLoadedHoleSceneAtStartup
            for (int i = 0; i < 8; i++) yield return null;

            // Step 1: find required components
            var labController  = FindFirstObjectByType<PhysicsLabController>();
            var shotController = FindFirstObjectByType<ShotController>();
            if (labController == null || shotController == null)
            {
                Debug.LogError("[SmokeTest2a] FAIL: PhysicsLabController or ShotController not found.");
                LastSmokeTestResult = "FAIL_NO_CONTROLLER";
                yield break;
            }
            Debug.Log($"[SmokeTest2a] Found labController='{labController.gameObject.name}'  shotController='{shotController.gameObject.name}'");

            // Step 2: report loaded scenes (H5 verification)
            bool holeFound = false;
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                Debug.Log($"[SmokeTest2a] LoadedScene[{i}]: '{s.name}' isLoaded={s.isLoaded}");
                if (s.name == "Hole_01_Geo") holeFound = true;
            }
            Debug.Log($"[SmokeTest2a] Hole_01_Geo present={holeFound}; H5 SetSurfaceProvider exercised={holeFound}");

            // Step 3: grab private _ballSM from PhysicsLabController via reflection
            var smField = typeof(PhysicsLabController)
                .GetField("_ballSM", BindingFlags.NonPublic | BindingFlags.Instance);
            _ballSM = smField?.GetValue(labController) as BallStateMachine;
            if (_ballSM == null)
            {
                Debug.LogError("[SmokeTest2a] FAIL: could not retrieve _ballSM.");
                LastSmokeTestResult = "FAIL_NO_SM";
                yield break;
            }
            Debug.Log($"[SmokeTest2a] Got _ballSM. Initial SM.State={_ballSM.State}");

            // Step 4: grab private ballAnimator for ball repositioning
            var animField = typeof(PhysicsLabController)
                .GetField("ballAnimator", BindingFlags.NonPublic | BindingFlags.Instance);
            var ballAnimator = animField?.GetValue(labController) as BallAnimator;
            Debug.Log($"[SmokeTest2a] ballAnimator found={ballAnimator != null}");

            // Step 5: subscribe to OnShotComplete BEFORE firing any shots
            _ballSM.OnShotComplete += OnShotCompleteCallback;
            Debug.Log("[SmokeTest2a] Subscribed to _ballSM.OnShotComplete");

            // =========================================================
            // SHOT 1: DRIVER  (SetClub index 0, power=0.15)
            // Low power keeps ball from going OB.
            // =========================================================
            labController.SetClub(0);
            yield return null;
            Debug.Log($"[SmokeTest2a][§2a-debug] PRE-SHOT-1: SM.State={_ballSM.State}  ShotController.State={shotController.State}");
            Debug.Log("[SmokeTest2a] === SHOT 1 (Driver, power=0.15) — firing via ShotController.FireDebugShot ===");

            shotController.FireDebugShot(0.15f, DebugShotAccuracy.Green);
            yield return null;
            Debug.Log($"[SmokeTest2a] Shot 1 fired. SM.State={_ballSM.State}  ShotController.State={shotController.State}");

            float timeout = 60f;
            while (_shotsComplete < 1 && timeout > 0f) { timeout -= Time.deltaTime; yield return null; }
            if (timeout <= 0f) { Debug.LogError("[SmokeTest2a] TIMEOUT shot 1"); LastSmokeTestResult = "FAIL_TIMEOUT_SHOT1"; yield break; }

            yield return null;
            Debug.Log($"[SmokeTest2a][§2a-debug] POST-SHOT-1 RE-ARM: SM.State={_ballSM.State}  ShotController.State={shotController.State}  — ready for shot 2");

            // =========================================================
            // SHOT 2: IRON 7  (SetClub index 1, power=0.05)
            // =========================================================
            labController.SetClub(1);
            yield return null;
            Debug.Log($"[SmokeTest2a][§2a-debug] PRE-SHOT-2: SM.State={_ballSM.State}  ShotController.State={shotController.State}");
            Debug.Log("[SmokeTest2a] === SHOT 2 (Iron 7, power=0.05) — firing via ShotController.FireDebugShot ===");

            shotController.FireDebugShot(0.05f, DebugShotAccuracy.Green);
            yield return null;
            Debug.Log($"[SmokeTest2a] Shot 2 fired. SM.State={_ballSM.State}  ShotController.State={shotController.State}");

            timeout = 60f;
            while (_shotsComplete < 2 && timeout > 0f) { timeout -= Time.deltaTime; yield return null; }
            if (timeout <= 0f) { Debug.LogError("[SmokeTest2a] TIMEOUT shot 2"); LastSmokeTestResult = "FAIL_TIMEOUT_SHOT2"; yield break; }

            yield return null;
            Debug.Log($"[SmokeTest2a][§2a-debug] POST-SHOT-2 RE-ARM: SM.State={_ballSM.State}  ShotController.State={shotController.State}  — ready for shot 3");

            // =========================================================
            // SHOT 3: PUTTER from GREEN
            // Manually place ball on the green, then fire putter.
            // This is setup-not-bypass: the SM must still process the full
            // Aiming→Flying→Rolling→AtRest lifecycle from this origin.
            // =========================================================

            // Place ball on green (Hole 1 green center: x=-230, z=-73, Y snapped by BallAnimator)
            if (ballAnimator != null)
            {
                ballAnimator.PlaceAtRest(k_GreenPos);
                Debug.Log($"[SmokeTest2a] Ball placed at green position: {k_GreenPos}. CurrentBall.pos={ballAnimator.CurrentBall?.position}");
            }
            else
            {
                Debug.LogWarning("[SmokeTest2a] ballAnimator null — cannot place ball on green for shot 3");
            }

            yield return null; // let placement settle

            labController.SetClub(3);
            yield return null;
            Debug.Log($"[SmokeTest2a][§2a-debug] PRE-SHOT-3: SM.State={_ballSM.State}  ShotController.State={shotController.State}");
            Debug.Log("[SmokeTest2a] === SHOT 3 (Putter, power=0.05, from green) — firing via ShotController.FireDebugShot ===");

            shotController.FireDebugShot(0.05f, DebugShotAccuracy.Green);
            yield return null;
            Debug.Log($"[SmokeTest2a] Shot 3 fired. SM.State={_ballSM.State}  ShotController.State={shotController.State}");

            timeout = 60f;
            while (_shotsComplete < 3 && timeout > 0f) { timeout -= Time.deltaTime; yield return null; }
            if (timeout <= 0f) { Debug.LogError("[SmokeTest2a] TIMEOUT shot 3"); LastSmokeTestResult = "FAIL_TIMEOUT_SHOT3"; yield break; }

            yield return null;
            Debug.Log($"[SmokeTest2a][§2a-debug] POST-SHOT-3 RE-ARM: SM.State={_ballSM.State}  ShotController.State={shotController.State}");
            Debug.Log("[SmokeTest2a] All 3 shots complete. Capturing at-rest frame at next end-of-frame...");

            // =========================================================
            // CAPTURE — §2b: delegates to CaptureCore.SnapAtEndOfFrameAndPause
            // (formerly an inline copy; now factored into Golfin.Diagnostics.Runtime).
            // Capture-then-pause (correct order per CLAUDE.md rules).
            // =========================================================
            const string capLabel = "loop_v1_2a_iter4_real_flick3_atrest";
            string capPath = $"{CaptureCore.OutDir}/{capLabel}_f{Time.frameCount}.png";
            CapturedScreenshotPath = capPath;
            yield return StartCoroutine(CaptureCore.SnapAtEndOfFrameAndPause(capLabel));
            Debug.Log($"[SmokeTest2a] Screenshot written: {CapturedScreenshotPath}");

            // Unsubscribe
            _ballSM.OnShotComplete -= OnShotCompleteCallback;
            _ballSM = null;

            LastSmokeTestResult = "PASS";
            Debug.Log("[SmokeTest2a] === SMOKE TEST COMPLETE (PASS) ===");
        }

        // ── OnShotComplete callback ──────────────────────────────────────────────
        void OnShotCompleteCallback(ShotResult result)
        {
            _shotsComplete++;
            string obPart = result.OBReason.HasValue ? $" OBReason={result.OBReason.Value}" : "";
            // Echo the same log format as HandleShotComplete so reviewers can verify
            Debug.Log($"[SmokeTest2a][§2a-debug] OnShotComplete #{_shotsComplete}: terminal={result.TerminalState}{obPart} end={result.EndPosition}");
        }

    }
}
