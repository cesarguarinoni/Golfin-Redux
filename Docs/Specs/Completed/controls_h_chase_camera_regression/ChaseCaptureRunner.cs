using System.Collections;
using System.IO;
using UnityEngine;
using Golfin.Gameplay.Loop;
using Golfin.Diagnostics.Runtime;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Golfin.Physics.Viewer
{
    /// <summary>
    /// One-shot coroutine runner that fires two preset shots (driver + iron) and captures
    /// mid-flight Chase-mode screenshots for controls_h_chase_camera_regression iteration 2.
    ///
    /// Attach to any active GameObject in LabScaffold (or call via script-execute), then
    /// call RunCaptures() or call it from a MenuItem wrapper.
    ///
    /// Timing: fires shot → polls for BallState.Flying → waits 0.8s extra (mid-arc) →
    /// snaps at end-of-frame. This guarantees the ball is actually moving through the air,
    /// not sitting on the tee.
    /// </summary>
    public class ChaseCaptureRunner : MonoBehaviour
    {
        [Header("References (auto-found if null)")]
        [SerializeField] PhysicsLabController lab;
        [SerializeField] LoopCameraDirector   director;

        // Output paths written by the coroutine (for reading back).
        public static string LastDriverCapturePath;
        public static string LastIronCapturePath;

        void Awake()
        {
            if (lab      == null) lab      = FindObjectOfType<PhysicsLabController>();
            if (director == null) director = FindObjectOfType<LoopCameraDirector>();
        }

        /// <summary>
        /// Entry point: find or create this component on a new GameObject, then run.
        /// Called from script-execute in play mode.
        /// </summary>
        public static ChaseCaptureRunner StartInScene()
        {
            var go = new GameObject("[ChaseCaptureRunner]");
            var runner = go.AddComponent<ChaseCaptureRunner>();
            runner.StartCoroutine(runner.CaptureAll());
            return runner;
        }

        public void RunCaptures()
        {
            StartCoroutine(CaptureAll());
        }

        IEnumerator CaptureAll()
        {
            Debug.Log("[ChaseCaptureRunner] Starting mid-flight chase capture sequence.");

            // ── 1. Find the lab controller ────────────────────────────────────────
            if (lab == null) lab = FindObjectOfType<PhysicsLabController>();
            if (lab == null)
            {
                Debug.LogError("[ChaseCaptureRunner] PhysicsLabController not found in scene. Aborting.");
                yield break;
            }

            // Wait a frame to ensure scene has fully started.
            yield return null;
            yield return null;

            // ── 2. Driver mid-flight capture ──────────────────────────────────────
            Debug.Log("[ChaseCaptureRunner] Firing driver_hole1 for mid-flight capture...");
            var driverPreset = ShotPresetCatalog.All[10]; // index 10 = driver_hole1
            Debug.Log($"[ChaseCaptureRunner] Using preset: {driverPreset.DisplayName}");

            // Fire the driver shot via the SM path.
            lab.Fire(driverPreset);

            // Poll until ball state reaches Flying (non-blocking, max 5 seconds).
            BallStateMachine ballSM = GetBallSM();
            float timeout = 5f;
            float elapsed = 0f;
            while (elapsed < timeout)
            {
                ballSM = GetBallSM();
                if (ballSM != null && ballSM.State == BallState.Flying) break;
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (ballSM == null || ballSM.State != BallState.Flying)
            {
                Debug.LogWarning($"[ChaseCaptureRunner] Ball did not reach Flying state within {timeout}s (state={ballSM?.State}). Proceeding anyway.");
            }
            else
            {
                Debug.Log($"[ChaseCaptureRunner] Ball is Flying. Waiting 0.8s for mid-arc position...");
            }

            // Wait 0.8s mid-arc so the camera has moved well away from the tee.
            yield return new WaitForSeconds(0.8f);

            // Snap at end of frame (synchronous RT grab, skipPause=true for MCP safety).
            string driverOut = Path.GetFullPath("Docs/Diagnostics/_capture/controls_h_driver_chase_midflight_v2.png");
            Directory.CreateDirectory(Path.GetDirectoryName(driverOut));
            yield return StartCoroutine(CaptureCore.SnapAtEndOfFrameAndPause(
                "controls_h_driver_chase_midflight_v2",
                outputPath: driverOut,
                skipPause: true));
            LastDriverCapturePath = driverOut;
            Debug.Log($"[ChaseCaptureRunner] Driver mid-flight captured: {driverOut}");

            // Wait for ball to settle before firing next shot (max 12s).
            Debug.Log("[ChaseCaptureRunner] Waiting for driver shot to settle...");
            elapsed = 0f;
            timeout = 12f;
            while (elapsed < timeout)
            {
                ballSM = GetBallSM();
                if (ballSM != null &&
                    (ballSM.State == BallState.AtRest || ballSM.State == BallState.OB ||
                     ballSM.State == BallState.InCup))
                    break;
                elapsed += Time.deltaTime;
                yield return null;
            }
            Debug.Log($"[ChaseCaptureRunner] Driver shot settled (state={ballSM?.State}). Waiting 1s before next shot...");
            yield return new WaitForSeconds(1f);

            // ── 3. Iron mid-flight capture ────────────────────────────────────────
            Debug.Log("[ChaseCaptureRunner] Firing iron7_hole1 for chase-landing capture...");
            var ironPreset = ShotPresetCatalog.All[11]; // index 11 = iron7_hole1
            Debug.Log($"[ChaseCaptureRunner] Using preset: {ironPreset.DisplayName}");

            lab.Fire(ironPreset);

            // Poll until Flying.
            elapsed = 0f;
            timeout = 5f;
            while (elapsed < timeout)
            {
                ballSM = GetBallSM();
                if (ballSM != null && ballSM.State == BallState.Flying) break;
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (ballSM == null || ballSM.State != BallState.Flying)
            {
                Debug.LogWarning($"[ChaseCaptureRunner] Iron ball did not reach Flying state within {timeout}s (state={ballSM?.State}). Proceeding anyway.");
            }
            else
            {
                Debug.Log("[ChaseCaptureRunner] Iron ball is Flying. Waiting 1.0s for near-landing position...");
            }

            // Wait 1.0s — iron7 has ~3s flight; 1s puts us roughly mid-arc/early descent.
            yield return new WaitForSeconds(1.0f);

            string ironOut = Path.GetFullPath("Docs/Diagnostics/_capture/controls_h_iron_chase_landing_v2.png");
            Directory.CreateDirectory(Path.GetDirectoryName(ironOut));
            yield return StartCoroutine(CaptureCore.SnapAtEndOfFrameAndPause(
                "controls_h_iron_chase_landing_v2",
                outputPath: ironOut,
                skipPause: true));
            LastIronCapturePath = ironOut;
            Debug.Log($"[ChaseCaptureRunner] Iron chase-landing captured: {ironOut}");

            Debug.Log("[ChaseCaptureRunner] All captures complete. Driver: " +
                      LastDriverCapturePath + " / Iron: " + LastIronCapturePath);

            // Self-destruct so we don't persist in scene.
            Destroy(gameObject);
        }

        BallStateMachine GetBallSM()
        {
            if (lab == null) return null;
            // BallSM is declared internal on PhysicsLabController.
            // ChaseCaptureRunner is in the same Golfin.Physics.Viewer assembly, so
            // internal is accessible directly — no interface cast needed.
            return lab.BallSM;
        }
    }
}
