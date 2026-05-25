using System.Collections;
using UnityEngine;
using Golfin.Gameplay.Input;
using Golfin.Gameplay.Loop;
using Golfin.Diagnostics.Runtime;

namespace Golfin.Physics.Viewer
{
    /// <summary>
    /// putter_cone_per_shot_lifecycle smoke runner — iteration 4 (Approach C).
    ///
    /// APPROACH C (2026-05-17): PutterTrack is the actual putter aim viz.
    ///   LogConeState now logs PutterTrack.activeSelf instead of _coneGraphic.enabled.
    ///   Iron cone (_coneGraphic) is permanently disabled in putter mode — its state
    ///   is not the lifecycle indicator in Approach C.
    ///
    /// ITERATION 3b FIX (frozen-Time.time in MCP-driven play mode):
    ///   Unity MCP "Enter Play Mode without Domain Reload" causes Time.time to stay at 0
    ///   while Time.unscaledTime advances normally. WaitForSeconds(n) uses Time.time, so
    ///   WaitForSeconds hangs forever. WaitForSecondsRealtime uses Time.unscaledTime, so
    ///   it works correctly. ALL WaitForSeconds calls replaced with WaitForSecondsRealtime.
    ///
    /// ITERATION 3a FIX (frozen-GameView RT):
    ///   SnapPlayModeSafe calls GrabGameViewRT() which calls gv.Repaint() — a scheduled
    ///   repaint, NOT a synchronous render. When called inside a coroutine, the RT may not
    ///   have updated. Fix: use SnapAtEndOfFrameAndPause with skipPause:true — it yields
    ///   WaitForEndOfFrame BEFORE reading the RT, guaranteeing a fresh frame.
    ///
    /// This script auto-destroys after capture. Safe to leave in the scene during dev;
    /// it does nothing unless StartSmokeCapture() is called or AutoStart=true.
    /// </summary>
    public class PutterConeSmokeCapture : MonoBehaviour
    {
        [Tooltip("Seconds to wait after entering play mode before starting capture (real-time).")]
        public float InitialDelay = 3f;

        [Tooltip("If true, starts capture automatically on Start() without needing external trigger.")]
        public bool AutoStart = false;

        // Audit state — populated by OnStateChanged subscriber before each snap.
        private string _lastShotControllerState = "none";

        // Run timestamp set at coroutine start.
        private string _runTimestamp;

        void Start()
        {
            if (AutoStart) StartSmokeCapture();
        }

        public void StartSmokeCapture() => StartCoroutine(SmokeRoutine());

        IEnumerator SmokeRoutine()
        {
            _runTimestamp = System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            Debug.Log($"[PutterConeSmoke] SmokeRoutine started. Waiting {InitialDelay}s (realtime). Timestamp={_runTimestamp}");
            // WaitForSecondsRealtime uses unscaled wall-clock time, not Time.time.
            // This survives MCP frozen-time (Time.time=0) environments.
            yield return new WaitForSecondsRealtime(InitialDelay);
            Debug.Log("[PutterConeSmoke] Initial delay done. Finding components...");

            var lab = FindObjectOfType<PhysicsLabController>();
            if (lab == null)
            {
                Debug.LogError("[PutterConeSmoke] PhysicsLabController not found — abort");
                Destroy(gameObject);
                yield break;
            }

            var shotController = FindObjectOfType<Golfin.Gameplay.Input.ShotController>();
            var coneView       = FindObjectOfType<Golfin.Gameplay.UI.ShotUI.ShotConeView>();

            if (shotController == null || coneView == null)
            {
                Debug.LogError("[PutterConeSmoke] ShotController or ShotConeView not found — abort");
                Destroy(gameObject);
                yield break;
            }

            // APPROACH C: find the PutterTrack GameObject by name (it's a child of the shot Canvas).
            // PhysicsLabController._putterTrack is not accessible from here, so search by name.
            // The GO is named "PutterTrack" in the LabScaffold hierarchy.
            var putterTrackGO = GameObject.Find("PutterTrack");
            Debug.Log($"[PutterConeSmoke] Components found: lab={lab.name} shot={shotController.name} " +
                      $"coneView={coneView.name} putterTrack={(putterTrackGO != null ? putterTrackGO.name : "NULL")}");
            shotController.OnStateChanged += LogShotControllerState;

            string outDir = CaptureCore.OutDir;

            // ── Piece 2a: Regular-mode (Driver) ball at Aiming ───────────────────
            Debug.Log("[PutterConeSmoke] P2a: Switching to Driver mode...");
            lab.SetClub(0);
            lab.InjectLabBundleForCurrentClub(); // LAB path
            yield return new WaitForSecondsRealtime(0.5f);

            shotController.BeginExternalDrag();
            yield return new WaitForEndOfFrame();
            yield return new WaitForEndOfFrame();

            LogPutterTrackState("PRE-P2a", putterTrackGO, shotController);
            string pathP2a = $"{outDir}/putter_cone_p2a_regular_aiming_{_runTimestamp}.png";
            yield return CaptureCore.SnapAtEndOfFrameAndPause(
                "putter_cone_p2a_regular_aiming", pathP2a, skipPause: true);
            Debug.Log($"[PutterConeSmoke] P2a = {pathP2a}");

            shotController.CancelExternalDrag();
            yield return new WaitForEndOfFrame();

            // ── Enter putter mode ────────────────────────────────────────────────
            Debug.Log("[PutterConeSmoke] Entering putter mode...");
            lab.SetClub(PhysicsLabController.PutterIndex);
            lab.InjectLabBundleForCurrentClub(); // LAB path
            yield return new WaitForSecondsRealtime(0.5f);

            // ── P1-Frame1 + P2b: Aiming — PutterTrack VISIBLE ───────────────────
            shotController.BeginExternalDrag();
            yield return new WaitForEndOfFrame();
            yield return new WaitForEndOfFrame();

            LogPutterTrackState("PRE-P1F1", putterTrackGO, shotController);
            string pathP1F1 = $"{outDir}/putter_cone_p1f1_aiming_puttertrack_visible_{_runTimestamp}.png";
            yield return CaptureCore.SnapAtEndOfFrameAndPause(
                "putter_cone_p1f1_aiming_puttertrack_visible", pathP1F1, skipPause: true);
            Debug.Log($"[PutterConeSmoke] P1-F1 (aiming, PutterTrack VISIBLE) = {pathP1F1}");

            // P2b: same state, extra snap for ball-size parity
            string pathP2b = $"{outDir}/putter_cone_p2b_putt_aiming_ballsize_{_runTimestamp}.png";
            yield return CaptureCore.SnapAtEndOfFrameAndPause(
                "putter_cone_p2b_putt_aiming_ballsize", pathP2b, skipPause: true);

            // Fire a debug shot.
            shotController.CancelExternalDrag();
            yield return new WaitForEndOfFrame();

            Debug.Log("[PutterConeSmoke] Firing debug shot (power=0.5, accuracy=Green)...");
            shotController.FireDebugShot(0.5f, DebugShotAccuracy.Green);
            yield return new WaitForEndOfFrame();
            yield return new WaitForEndOfFrame();
            yield return new WaitForSecondsRealtime(0.05f);

            // ── P1-Frame2: Just after fire — PutterTrack HIDDEN ──────────────────
            LogPutterTrackState("PRE-P1F2", putterTrackGO, shotController);
            string pathP1F2 = $"{outDir}/putter_cone_p1f2_just_fired_puttertrack_hidden_{_runTimestamp}.png";
            yield return CaptureCore.SnapAtEndOfFrameAndPause(
                "putter_cone_p1f2_just_fired_puttertrack_hidden", pathP1F2, skipPause: true);
            Debug.Log($"[PutterConeSmoke] P1-F2 (just fired, PutterTrack HIDDEN) = {pathP1F2}");

            // Wait for ball rolling phase.
            yield return new WaitForSecondsRealtime(1.5f);

            // ── P1-Frame3: Ball rolling — PutterTrack still HIDDEN ───────────────
            LogPutterTrackState("PRE-P1F3", putterTrackGO, shotController);
            string pathP1F3 = $"{outDir}/putter_cone_p1f3_rolling_puttertrack_hidden_{_runTimestamp}.png";
            yield return CaptureCore.SnapAtEndOfFrameAndPause(
                "putter_cone_p1f3_rolling_puttertrack_hidden", pathP1F3, skipPause: true);
            Debug.Log($"[PutterConeSmoke] P1-F3 (rolling, PutterTrack HIDDEN) = {pathP1F3}");

            // Wait for AtRest + auto-rearm.
            yield return new WaitForSecondsRealtime(5f);
            Debug.Log($"[PutterConeSmoke] POST-AtRest: ShotController={_lastShotControllerState}");

            // ── Re-aim ─────────────────────────────────────────────────────────
            shotController.BeginExternalDrag();
            yield return new WaitForEndOfFrame();
            yield return new WaitForEndOfFrame();

            // ── P1-Frame4: Next aiming — PutterTrack VISIBLE again ───────────────
            LogPutterTrackState("PRE-P1F4", putterTrackGO, shotController);
            string pathP1F4 = $"{outDir}/putter_cone_p1f4_next_aiming_puttertrack_visible_{_runTimestamp}.png";
            yield return CaptureCore.SnapAtEndOfFrameAndPause(
                "putter_cone_p1f4_next_aiming_puttertrack_visible", pathP1F4, skipPause: true);
            Debug.Log($"[PutterConeSmoke] P1-F4 (next aiming, PutterTrack VISIBLE again) = {pathP1F4}");

            shotController.CancelExternalDrag();
            yield return new WaitForEndOfFrame();

            // ── Piece 2c: Exit putter → Driver ─────────────────────────────────
            lab.SetClub(0);
            lab.InjectLabBundleForCurrentClub(); // LAB path
            yield return new WaitForSecondsRealtime(0.5f);

            shotController.BeginExternalDrag();
            yield return new WaitForEndOfFrame();
            yield return new WaitForEndOfFrame();

            LogPutterTrackState("PRE-P2c", putterTrackGO, shotController);
            string pathP2c = $"{outDir}/putter_cone_p2c_after_exit_putter_ballsize_{_runTimestamp}.png";
            yield return CaptureCore.SnapAtEndOfFrameAndPause(
                "putter_cone_p2c_after_exit_putter_ballsize", pathP2c, skipPause: true);
            Debug.Log($"[PutterConeSmoke] P2c (driver after putter exit, ball should be 150) = {pathP2c}");

            shotController.CancelExternalDrag();
            yield return new WaitForEndOfFrame();

            // ── Production-flow capture ──────────────────────────────────────────
            Debug.Log("[PutterConeSmoke] PRODUCTION FLOW: switching back to putter...");
            lab.SetClub(PhysicsLabController.PutterIndex);
            lab.InjectLabBundleForCurrentClub(); // LAB path
            yield return new WaitForSecondsRealtime(0.5f);

            shotController.BeginExternalDrag();
            yield return new WaitForEndOfFrame();
            yield return new WaitForEndOfFrame();

            LogPutterTrackState("PRE-PROD-aiming", putterTrackGO, shotController);
            string pathProdAiming = $"{outDir}/putter_cone_prod_aiming_puttertrack_visible_{_runTimestamp}.png";
            yield return CaptureCore.SnapAtEndOfFrameAndPause(
                "putter_cone_prod_aiming_puttertrack_visible", pathProdAiming, skipPause: true);
            Debug.Log($"[PutterConeSmoke] PROD-aiming (PutterTrack VISIBLE) = {pathProdAiming}");

            shotController.CancelExternalDrag();
            yield return new WaitForEndOfFrame();
            shotController.FireDebugShot(0.4f, DebugShotAccuracy.Green);
            yield return new WaitForEndOfFrame();
            yield return new WaitForEndOfFrame();
            yield return new WaitForSecondsRealtime(0.1f);

            LogPutterTrackState("PRE-PROD-fired", putterTrackGO, shotController);
            string pathProdFired = $"{outDir}/putter_cone_prod_fired_puttertrack_hidden_{_runTimestamp}.png";
            yield return CaptureCore.SnapAtEndOfFrameAndPause(
                "putter_cone_prod_fired_puttertrack_hidden", pathProdFired, skipPause: true);
            Debug.Log($"[PutterConeSmoke] PROD-fired (PutterTrack HIDDEN) = {pathProdFired}");

            yield return new WaitForSecondsRealtime(3f);

            // ── Summary ──────────────────────────────────────────────────────────
            Debug.Log("[PutterConeSmoke] ===== DONE =====");
            Debug.Log($"  P2a  = {pathP2a}");
            Debug.Log($"  P1F1 = {pathP1F1}  [PutterTrack VISIBLE expected]");
            Debug.Log($"  P1F2 = {pathP1F2}  [PutterTrack HIDDEN expected]");
            Debug.Log($"  P1F3 = {pathP1F3}  [PutterTrack HIDDEN expected]");
            Debug.Log($"  P1F4 = {pathP1F4}  [PutterTrack VISIBLE expected]");
            Debug.Log($"  P2b  = {pathP2b}");
            Debug.Log($"  P2c  = {pathP2c}");
            Debug.Log($"  PROD aiming = {pathProdAiming}");
            Debug.Log($"  PROD fired  = {pathProdFired}");
            Debug.Log("[PutterConeSmoke] Check Docs/Diagnostics/_capture/ for all frames.");

            shotController.OnStateChanged -= LogShotControllerState;
            Destroy(gameObject);
        }

        /// <summary>
        /// APPROACH C: logs PutterTrack.activeSelf (the actual putter aim viz lifecycle indicator).
        /// </summary>
        private void LogPutterTrackState(string tag, GameObject putterTrackGO,
            Golfin.Gameplay.Input.ShotController shotCtrl)
        {
            bool trackActive = putterTrackGO != null && putterTrackGO.activeSelf;
            Debug.Log($"[PutterConeSmoke][{tag}] ShotController.State={shotCtrl?.State} " +
                      $"PutterTrack.activeSelf={trackActive}");
        }

        private void LogShotControllerState(ShotInputState state)
        {
            _lastShotControllerState = state.State.ToString();
        }
    }
}
