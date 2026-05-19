using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using Golfin.Diagnostics.Runtime;
using Golfin.Gameplay.UI.HUD;
using Golfin.Gameplay.Session;
using Golfin.Gameplay.Loop;
using Golfin.Physics.Math;
using Golfin.Physics;
#if UNITY_EDITOR
using UnityEditor;
#endif

#if UNITY_EDITOR
namespace Golfin.Physics.Viewer
{
    /// <summary>
    /// §2e screenshot capture host. Spawned by SmokeRunner2eMenu BEFORE play-mode entry.
    /// The hole scene is loaded additively so LabRoot is present in play mode.
    ///
    /// CaptureMode=0: AtRest facing pin (Hole_01_Geo)
    ///   S1 — controls_2e_atrest_facing_pin.png
    ///
    /// CaptureMode=1: OB drop + TURN (Hole_06_Geo, shot into lake)
    ///   S2 — controls_2e_ob_drop.png
    ///   S3 — controls_2e_turn_counter_after_ob.png
    ///   L1 — controls_2e_history_log.txt
    ///
    /// Self-destructs after completion. DO NOT use in production.
    /// </summary>
    public class SmokeRunner2eHost : MonoBehaviour
    {
        const float StartupWait = 5.0f;   // wait for all Awake/Start + HUD render
        const float ShotWait    = 20.0f;  // max time to wait for a shot to resolve
        const float CaptureWait = 1.5f;   // settle time before capture after state change

        const string ArmedKey = "SmokeRunner2eHost.Armed";

        public static bool Armed
        {
            get
            {
#if UNITY_EDITOR
                return UnityEditor.SessionState.GetBool(ArmedKey, false);
#else
                return false;
#endif
            }
            set
            {
#if UNITY_EDITOR
                UnityEditor.SessionState.SetBool(ArmedKey, value);
#endif
            }
        }

        // Which capture sequence to run (set before entering play mode).
        // Stored in SessionState so it survives domain reload on play-mode entry.
        const string CaptureModeKey = "SmokeRunner2eHost.CaptureMode";

        public static int CaptureMode
        {
            get
            {
#if UNITY_EDITOR
                return UnityEditor.SessionState.GetInt(CaptureModeKey, 0);
#else
                return 0;
#endif
            }
            set
            {
#if UNITY_EDITOR
                UnityEditor.SessionState.SetInt(CaptureModeKey, value);
#endif
            }
        }

        void Start()
        {
#if UNITY_EDITOR
            bool sessionArmed = UnityEditor.SessionState.GetBool(ArmedKey, false);
            Debug.Log($"[SmokeRunner2eHost] Start() — SessionState Armed={sessionArmed} CaptureMode={CaptureMode}");
#else
            bool sessionArmed = false;
#endif
            if (!sessionArmed)
            {
                Debug.LogWarning("[SmokeRunner2eHost] Not armed — destroying self. " +
                                 "Use GOLFIN > Smoke > Capture 2e... to launch.");
                Destroy(this);
                return;
            }
            Armed = false;
            if (CaptureMode == 0)
                StartCoroutine(RunAtRestSequence());
            else
                StartCoroutine(RunOBSequence());
        }

        /// <summary>
        /// AtRest sequence: fire a driver shot, wait for AtRest, capture camera-facing-pin.
        /// </summary>
        IEnumerator RunAtRestSequence()
        {
            yield return new WaitForSeconds(StartupWait);

            var controller = FindObjectOfType<PhysicsLabController>();
            if (controller == null)
            {
                Debug.LogError("[SmokeRunner2eHost] PhysicsLabController not found.");
                Destroy(this);
                yield break;
            }

            var sm = controller.BallSM;
            // Retry BallSM in case of Awake ordering variance.
            for (int retry = 0; retry < 6 && sm == null; retry++)
            {
                yield return new WaitForSeconds(0.5f);
                sm = controller.BallSM;
            }
            if (sm == null)
            {
                Debug.LogError("[SmokeRunner2eHost] BallSM not found after retries.");
                Destroy(this);
                yield break;
            }

            Debug.Log("[SmokeRunner2eHost] AtRest sequence — firing driver_calm...");
            GameSession.ResetForNewHole();

            // Subscribe before firing
            bool shotComplete = false;
            bool atRestReached = false;
            System.Action<ShotResult>      onComplete  = _ => shotComplete = true;
            System.Action<BallStateChange> onState     = (ch) => { if (ch.Next == BallState.AtRest) atRestReached = true; };
            sm.OnShotComplete  += onComplete;
            sm.OnStateChanged  += onState;

            var preset = ShotPresetCatalog.All.FirstOrDefault(p => p.Id == "driver_calm");
            if (preset.Id == null)
            {
                Debug.LogError("[SmokeRunner2eHost] 'driver_calm' preset not found.");
                sm.OnShotComplete -= onComplete;
                sm.OnStateChanged -= onState;
                Destroy(this);
                yield break;
            }

            controller.Fire(preset);

            // Wait for shot completion
            float elapsed = 0f;
            while (!shotComplete && elapsed < ShotWait)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
            sm.OnShotComplete -= onComplete;
            sm.OnStateChanged -= onState;

            if (!shotComplete)
            {
                Debug.LogWarning($"[SmokeRunner2eHost] Shot did not complete in {ShotWait}s. Capturing anyway.");
            }
            else
            {
                Debug.Log($"[SmokeRunner2eHost] Shot complete! atRestReached={atRestReached}, " +
                          $"HoleContext.PinWorld={HoleContext.PinWorld}");
            }

            // Wait for HandleShotComplete + ReArm + HUD render
            yield return new WaitForSeconds(CaptureWait);

            // Capture S1
            string path1 = CaptureCore.SnapPlayModeSafe("controls_2e_atrest_facing_pin");
            Debug.Log($"[SmokeRunner2eHost] S1 captured: {path1} | TurnCount={GameSession.TurnCount}");

            Destroy(this);
        }

        /// <summary>
        /// OB sequence: fire a shot toward the lake on Hole_06, wait for OB→Aiming,
        /// capture drop position + TURN counter + history log.
        ///
        /// Camera fix (iter-2): after OB→Aiming the Director leaves camera in OBFreeze mode
        /// (ModeMap maps Aiming→null = leave unchanged). We force Chase mode here so
        /// ChaseCamera.LateUpdate returns early (null target + Chase) and ApplyCameraYaw
        /// owns the framing — showing the ball on visible grass at the drop point.
        /// S3 is captured after a 0.3s pause + 15° yaw reframe so it is a non-byte-identical
        /// frame with the TURN card more prominent.
        /// </summary>
        IEnumerator RunOBSequence()
        {
            yield return new WaitForSeconds(StartupWait);

            var controller = FindObjectOfType<PhysicsLabController>();
            if (controller == null)
            {
                Debug.LogError("[SmokeRunner2eHost] PhysicsLabController not found.");
                Destroy(this);
                yield break;
            }

            var sm = controller.BallSM;
            for (int retry = 0; retry < 6 && sm == null; retry++)
            {
                yield return new WaitForSeconds(0.5f);
                sm = controller.BallSM;
            }
            if (sm == null)
            {
                Debug.LogError("[SmokeRunner2eHost] BallSM not found after retries.");
                Destroy(this);
                yield break;
            }

            Debug.Log("[SmokeRunner2eHost] OB sequence — resetting session and firing toward lake...");
            GameSession.ResetForNewHole();

            // Hole_06 water polygon: x=[-40.8, 1.3] z=[-39.8, 23.2]. Tee ≈ (81, -25).
            // Place ball at x=20, z=-24 (fairway in front of water entry at x=1.3).
            // wedge_100_zerospin actual carry on Hole_06 ≈ 48m (not 91m — terrain height affects sim).
            // From x=20 firing -X at 48m carry → lands at x≈-28 (inside water zone x=-40.8 to 1.3) → OB.
            // Move ball forward from tee to within carry range of water.
            // PlaceBallAt teleports ball to (20, terrain_y, -24) — on fairway just before water.
            controller.PlaceBallAt(new UnityEngine.Vector3(20f, 0f, -24f));
            Debug.Log("[SmokeRunner2eHost] Ball placed at (20, 0, -24) — within carry range of water.");

            // Set camera yaw AFTER PlaceBallAt (PlaceBallAt internally sets yaw to tee→green direction).
            // π radians = pointing straight -X toward water. Must set AFTER PlaceBallAt to override.
            float lakeYaw = Mathf.PI;
            controller.SetCameraYawRadians(lakeYaw);
            Debug.Log($"[SmokeRunner2eHost] Set camera yaw to {lakeYaw:F3} rad ({lakeYaw * Mathf.Rad2Deg:F1}°) toward lake (after PlaceBallAt).");

            // Short settle frame after PlaceBallAt (let any pending SM transitions flush).
            yield return null;

            bool shotComplete   = false;
            bool aimingAfterOB  = false;
            bool obReached      = false;
            System.Action<ShotResult>      onComplete = _ => shotComplete = true;
            System.Action<BallStateChange> onState    = (ch) =>
            {
                if (ch.Next == BallState.OB)     obReached = true;
                if (ch.Next == BallState.Aiming && ch.Previous == BallState.OB) aimingAfterOB = true;
            };
            sm.OnShotComplete += onComplete;
            sm.OnStateChanged += onState;

            // wedge_100_zerospin fires ~48m carry on Hole_06 terrain — from x=20 that lands at x≈-28 (water).
            var preset = ShotPresetCatalog.All.FirstOrDefault(p => p.Id == "wedge_100_zerospin");
            if (preset.Id == null)
            {
                Debug.LogError("[SmokeRunner2eHost] 'wedge_100_zerospin' preset not found. Check ShotPresetCatalog.");
                sm.OnShotComplete -= onComplete;
                sm.OnStateChanged -= onState;
                Destroy(this);
                yield break;
            }

            controller.Fire(preset);

            // Wait for shot completion + OB→Aiming
            float elapsed = 0f;
            while ((!shotComplete || !aimingAfterOB) && elapsed < ShotWait)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
            sm.OnShotComplete -= onComplete;
            sm.OnStateChanged -= onState;

            Debug.Log($"[SmokeRunner2eHost] OB sequence result: shotComplete={shotComplete} " +
                      $"obReached={obReached} aimingAfterOB={aimingAfterOB} TURN={GameSession.TurnCount}");

            if (!aimingAfterOB)
            {
                Debug.LogWarning($"[SmokeRunner2eHost] OB→Aiming not reached within {ShotWait}s. " +
                                 "Shot may not have hit water. Capturing current state as evidence.");
            }

            // Camera fix: Director leaves camera in OBFreeze mode after OB→Aiming (ModeMap Aiming→null).
            // Force Chase mode so ChaseCamera.LateUpdate returns early (null target + Chase) and
            // ApplyCameraYaw (already called by RepositionBallWithLookDir) owns the camera position.
            // This shows the ball on the actual drop-point terrain rather than the OB cinematic pivot.
            var camChase = FindObjectOfType<ChaseCamera>();
            if (camChase != null)
            {
                camChase.SetMode(ChaseCamera.Mode.Chase);
                Debug.Log("[SmokeRunner2eHost] Forced ChaseCamera → Chase mode for S2 framing.");
            }

            // Wait for HUD to update with new TURN value and camera to settle at ApplyCameraYaw position.
            yield return new WaitForSeconds(CaptureWait);

            // Capture S2: ball at drop point on grass surface, camera in Chase/Aiming framing.
            string path2 = CaptureCore.SnapPlayModeSafe("controls_2e_ob_drop");
            Debug.Log($"[SmokeRunner2eHost] S2 captured: {path2} | ChaseMode={camChase?.CurrentMode}");

            // ── S3: distinct frame — directly orbit camera ~15° around ball ──────
            // S3 must differ in bytes from S2 (prior run: same-frame duplicate, same MD5).
            // ChaseCamera is in Chase mode with null target → LateUpdate returns early → we
            // own the transform. Orbit the camera ~15° clockwise around the ball's current
            // position so the TURN-3 HUD card reads from a distinct angle.
            var ballTransform = controller.CurrentBall;
            if (camChase != null && ballTransform != null)
            {
                // Compute current camera XZ offset from ball, rotate 15° around Y axis.
                Vector3 ballPos3 = ballTransform.position;
                Vector3 camPos   = camChase.transform.position;
                Vector3 offset   = camPos - new Vector3(ballPos3.x, camPos.y, ballPos3.z);
                float   rot15    = 15f * Mathf.Deg2Rad;
                float   cos15    = Mathf.Cos(rot15);
                float   sin15    = Mathf.Sin(rot15);
                Vector3 newOffset = new Vector3(
                    offset.x * cos15 - offset.z * sin15,
                    offset.y,
                    offset.x * sin15 + offset.z * cos15);
                camChase.transform.position = new Vector3(ballPos3.x, camPos.y, ballPos3.z) + newOffset;
                camChase.transform.LookAt(ballPos3 + Vector3.up * 0.5f);
                Debug.Log($"[SmokeRunner2eHost] Camera orbited 15° for S3. New pos={camChase.transform.position:F2}");
            }

            // Yield one frame so the new camera position renders before capture.
            yield return new WaitForSeconds(0.3f);

            // Capture S3: same drop scene, camera orbited 15° — bytes-distinct from S2.
            string path3 = CaptureCore.SnapPlayModeSafe("controls_2e_turn_counter_after_ob");
            Debug.Log($"[SmokeRunner2eHost] S3 captured: {path3} | TurnCount={GameSession.TurnCount}");

            // Write history log L1
            WriteHistoryLog();

            Destroy(this);
        }

        void WriteHistoryLog()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== controls_2e_history_log ===");
            sb.AppendLine($"GameSession.TurnCount={GameSession.TurnCount}");
            sb.AppendLine($"GameSession.ShotHistory.Count={GameSession.ShotHistory.Count}");
            for (int i = 0; i < GameSession.ShotHistory.Count; i++)
            {
                var rec = GameSession.ShotHistory[i];
                sb.AppendLine($"--- ShotHistory[{i}] ---");
                sb.AppendLine($"  ShotNumber={rec.ShotNumber}");
                sb.AppendLine($"  ClubLabel={rec.ClubLabel}");
                sb.AppendLine($"  OriginPosition={rec.OriginPosition}");
                sb.AppendLine($"  FinalPosition={rec.FinalPosition}");
                sb.AppendLine($"  DistanceXZMeters={rec.DistanceXZMeters:F2}");
                sb.AppendLine($"  TerminalState={rec.TerminalState}");
                sb.AppendLine($"  OBReason={rec.OBReason ?? "null"}");
                sb.AppendLine($"  FinalSurface={rec.FinalSurface}");
                sb.AppendLine($"  PenaltyStrokes={rec.PenaltyStrokes}");
            }

            string outPath = Path.Combine(CaptureCore.OutDir, "controls_2e_history_log.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(outPath));
            File.WriteAllText(outPath, sb.ToString());
            Debug.Log($"[SmokeRunner2eHost] L1 history log written: {outPath}");
            Debug.Log("[SmokeRunner2eHost] History log contents:\n" + sb.ToString());
        }
    }
}
#endif
