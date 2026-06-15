#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Golfin.Gameplay.Loop;
using Golfin.Physics;
using Golfin.Physics.Math;

namespace Golfin.Physics.Viewer.Bot
{
    /// <summary>
    /// CAPTURE-ONLY rig for water_splash_fx (Order 349). Editor-only (#if UNITY_EDITOR) so it
    /// never compiles into a player build and has ZERO production gameplay footprint.
    ///
    /// GREY-WATER ROOT CAUSE (confirmed by Cesar 2026-06-14): the real cause is a DUPLICATE
    /// directional light. ShellScene stays additively loaded during gameplay (GameplaySceneLoader
    /// loads the host + hole Additive and never unloads ShellScene) and carries its own intensity-2
    /// Directional Light, on top of the hole's own directional light. The two suns double-light the
    /// surface from opposing azimuths and flatten the URPWater reflection to grey. The fix lives in
    /// the load path (PhysicsLabController.OnHoleLoaded → DisableShellDirectionalLight) and applies
    /// to BOTH normal play and capture, so this rig no longer touches water materials or URP
    /// settings. (The earlier capture-only "FIX A" — toggling supportsCameraOpaqueTexture and
    /// swapping the water reflection to a cubemap — was a wrong diagnosis and has been removed.)
    ///
    /// What it does, entirely in capture/bot code (no diff to BallSimulation / BallStateMachine /
    /// OBDropResolver / scenes):
    ///   1. Waits for the real-flow hole to be ready (PhysicsLabController.IsHoleReady).
    ///   2. Aims the deterministic Driver shot toward the Hole-6 water polygon and fires it
    ///      through the production ShotController path.
    ///   3. Subscribes to the SAME BallStateMachine.OnStateChanged signal WaterSplashController
    ///      uses, and captures the splash on the natural chase camera as the ball reaches water.
    ///
    /// The optional cinematic camera-hold (UseNaturalCamera=false) disables the ChaseCamera
    /// component and hard-sets the Camera transform each LateUpdate of this rig, then re-enables
    /// ChaseCamera on release. No production camera/OB code is modified — this rig simply
    /// out-prioritises the camera for the hold window during capture.
    /// </summary>
    public class WaterSplashCaptureRig : MonoBehaviour
    {
        // ── Tunables (set before StartCapture or via fields) ───────────────────
        public float AimYawRadians   = 2.9804f;   // toward Hole-6 Water_1 centre (iter-4 deterministic)
        public float Power01         = 0.45f;     // lands at the water centre (iter-4 deterministic)
        public float HoldSeconds     = 1.8f;      // dwell over the splash (spray 0.9 + ripple 1.2)
        // NATURAL-CAMERA mode: do NOT override the chase camera position. The natural ChaseCamera
        // follows the ball across the water at the correct height/angle. With the duplicate-light
        // grey-water fix now in the load path, the water reads blue from the natural perspective,
        // so we let the chase camera ride and capture at the moment of impact + a few frames for
        // splash particles to appear above the surface.
        // CamBackMeters/CamHeightMeters/LookAcrossMeters/LookUpMeters are preserved as tunables
        // but only used when UseNaturalCamera=false.
        public bool  UseNaturalCamera  = true;    // true = let chase camera ride; false = cinematic override
        public float CamBackMeters   = 9f;        // only used when UseNaturalCamera=false
        public float CamHeightMeters = 2.2f;      // only used when UseNaturalCamera=false
        public float LookAcrossMeters = 16f;      // only used when UseNaturalCamera=false
        public float LookUpMeters     = 1.0f;     // only used when UseNaturalCamera=false
        public float ReadyTimeout    = 30f;       // max wait for IsHoleReady
        public float FireDelay       = 0.6f;      // settle after ready before firing
        // Hole-6 water entry for ball-position-based in-flight capture.
        // The ball animation takes ~3.09s to reach this point; when the animated ball
        // is within BallNearWaterThresholdM metres of this point, the "in_flight" frame
        // is captured. This shows the ball (and natural camera) pointing toward water.
        public Vector3 WaterEntryHint = new Vector3(-19.90f, 7.27f, -8.27f);
        public float   BallNearWaterThresholdM = 8f; // capture when ball is within this many metres (iter-7: tightened from 18m)
        public float   ShotFlightSec = 3.09f;   // expected flight time; used as fallback cap

        // ── Runtime ────────────────────────────────────────────────────────────
        PhysicsLabController _plc;
        ChaseCamera          _chase;
        Camera               _cam;
        BallStateMachine     _sm;

        bool    _waterHit;
        Vector3 _entryPoint;
        Vector3 _camHoldPos;
        Quaternion _camHoldRot;
        bool    _camHeldThisFrame;

        public bool Finished { get; private set; }
        public bool WaterFramed { get; private set; }

        /// <summary>Kick off the full capture sequence. Idempotent guard via Finished.</summary>
        public void StartCapture()
        {
            StartCoroutine(RunSequence());
        }

        IEnumerator RunSequence()
        {
            Debug.Log("[WaterSplashCaptureRig] Sequence start — locating PhysicsLabController.");

            // 1. Find the controller (real flow hosts it on the additive LabScaffold).
            float t0 = Time.realtimeSinceStartup;
            while (_plc == null && Time.realtimeSinceStartup - t0 < ReadyTimeout)
            {
                _plc = Object.FindFirstObjectByType<PhysicsLabController>();
                if (_plc != null && _plc.IsHoleReady) break;
                _plc = null;
                yield return null;
            }
            if (_plc == null)
            {
                Debug.LogError("[WaterSplashCaptureRig] PhysicsLabController never became ready — aborting.");
                Finished = true;
                yield break;
            }

            _chase = _plc.GetComponentInChildren<ChaseCamera>();
            if (_chase == null) _chase = Object.FindFirstObjectByType<ChaseCamera>();
            _cam   = _chase != null ? _chase.GetComponent<Camera>() : Camera.main;
            _sm    = _plc.BallSM;
            if (_sm == null || _cam == null)
            {
                Debug.LogError($"[WaterSplashCaptureRig] Missing refs (sm={_sm != null}, cam={_cam != null}) — aborting.");
                Finished = true;
                yield break;
            }

            _sm.OnStateChanged += OnState;

            // Grey water is fixed in the load path now (PhysicsLabController disables the duplicate
            // ShellScene directional light on hole load). This rig no longer mutates water materials
            // or URP settings — just give the hole a few frames to finish rendering before firing.
            DynamicGI.UpdateEnvironment();
            for (int settleFrame = 0; settleFrame < 4; settleFrame++) yield return null;

            Debug.Log($"[WaterSplashCaptureRig] Ready. cam={_cam.name}, chase={(_chase != null ? _chase.name : "null")}. Settling {FireDelay}s before fire.");

            // Give the hole a moment to fully settle/render.
            yield return new WaitForSeconds(FireDelay);

            // 2. Aim + fire the deterministic shot through the production path.
            // Driver = club index 0; inject the lab velocity bundle for it, set aim yaw, then fire
            // via the production ShotController path. All seams are existing (internal/public) methods.
            _plc.SetClub(0);
            _plc.InjectLabBundleForCurrentClub();
            _plc.SetCameraYawRadians(AimYawRadians);
            Debug.Log($"[WaterSplashCaptureRig] Firing Driver power={Power01} aimYaw={AimYawRadians} rad.");
            _plc.FireViaShotController(Power01, Golfin.Gameplay.Input.DebugShotAccuracy.Green);

            // 3. NATURAL-CAMERA CAPTURE STRATEGY (iter-7+):
            //    The BallStateMachine.OnStateChanged fires DURING Update() in the same frame that
            //    the OB drop runs (PlaceAtRest puts the ball back at the tee drop position).
            //    By the time our coroutine's while(!_waterHit) exits (next frame), the ball is
            //    already at the drop position and the chase camera is pointing back at the tee.
            //
            //    Fix: poll the BallAnimator's live animated position each frame.  When the ball
            //    gets within BallNearWaterThresholdM metres of the known water entry point, capture
            //    the "peak" frame (ball in flight, natural camera pointing at the water/splash).
            //    Then wait for _waterHit (OB confirmed), and capture the "ripple" frame (splash
            //    particles still visible 0.5s after impact even after ball repositions).
            //
            //    Fallback: if no BallAnimator found, fall back to the timer-based approach
            //    (fireTime + ShotFlightSec - 0.15s).

            float fireTime = Time.realtimeSinceStartup;
            bool peakCaptured = false;

            // --- PHASE A: Poll for in-flight "peak" capture ---
            // Use BallAnimator.Instance.CurrentBall to track animated ball position.
            var ballAnim = Object.FindFirstObjectByType<BallAnimator>();
            if (UseNaturalCamera && ballAnim != null)
            {
                // Poll until ball is within threshold of water entry, OR timeout.
                float maxWait = ShotFlightSec + 1.0f; // 4.09s cap
                while (!_waterHit && Time.realtimeSinceStartup - fireTime < maxWait)
                {
                    Transform ballXf = ballAnim.CurrentBall;
                    if (ballXf != null)
                    {
                        float dist = Vector3.Distance(ballXf.position, WaterEntryHint);
                        if (dist < BallNearWaterThresholdM)
                        {
                            // Ball is approaching water — wait one more frame for splash particles
                            // to fire (WaterSplashController.HandleStateChanged runs in Update()),
                            // then capture while the ball is still near the water.
                            yield return null;
                            Debug.Log($"[WaterSplashCaptureRig] Ball near water (dist={dist:F1}m) — capturing peak. cam={_cam.transform.position:F2}");
                            WaterFramed = true;
                            yield return CaptureFrame("wsplash_peak");
                            peakCaptured = true;
                            break;
                        }
                    }
                    yield return null;
                }
                if (!peakCaptured && !_waterHit)
                    Debug.LogWarning($"[WaterSplashCaptureRig] Ball never reached threshold {BallNearWaterThresholdM}m — falling back to timer.");
            }

            // --- PHASE B: Fallback timer-based peak capture (non-natural mode or no BallAnimator) ---
            if (!peakCaptured && !UseNaturalCamera)
            {
                // Cinematic mode: wait for OB then park the camera.
                while (!_waterHit && Time.realtimeSinceStartup - fireTime < 12f)
                    yield return null;

                if (!_waterHit)
                {
                    Debug.LogError("[WaterSplashCaptureRig] No OBReason.Water within 12s — aborting.");
                    _sm.OnStateChanged -= OnState;
                    Finished = true;
                    yield break;
                }

                Vector3 shotDir2 = new Vector3(Mathf.Cos(AimYawRadians), 0f, Mathf.Sin(AimYawRadians));
                shotDir2.y = 0f; shotDir2.Normalize();
                _camHoldPos = _entryPoint - shotDir2 * CamBackMeters + Vector3.up * CamHeightMeters;
                Vector3 lookTarget2 = _entryPoint + shotDir2 * LookAcrossMeters + Vector3.up * LookUpMeters;
                _camHoldRot = Quaternion.LookRotation(lookTarget2 - _camHoldPos);
                if (_chase != null) _chase.enabled = false;
                DisableLoopCameraDirector();
                _camHeldThisFrame = true;
                Debug.Log($"[WaterSplashCaptureRig] WATER HIT at {_entryPoint:F2} — CINEMATIC camera at {_camHoldPos:F2}.");
                WaterFramed = true;
                for (int fi = 0; fi < 4; fi++) yield return null;
                yield return CaptureFrame("wsplash_peak");
                peakCaptured = true;
            }

            // --- PHASE C: Wait for OB confirmation and capture splash at impact ---
            // After the ball reached the water, wait for the OB state change if it hasn't fired yet.
            if (!_waterHit)
            {
                float capRemain = fireTime + ShotFlightSec + 2.0f - Time.realtimeSinceStartup;
                float obDeadline = Mathf.Max(capRemain, 3.0f);
                float obStart = Time.realtimeSinceStartup;
                while (!_waterHit && Time.realtimeSinceStartup - obStart < obDeadline)
                    yield return null;
            }

            if (!_waterHit)
            {
                Debug.LogError("[WaterSplashCaptureRig] OBReason.Water never confirmed — shot missed water. Aborting.");
                _sm.OnStateChanged -= OnState;
                Finished = true;
                yield break;
            }

            // IMPACT CAPTURE (iter-7+):
            // OnState() set _waterHit this frame (same frame as the OB event).
            // WaterSplashController.HandleStateChanged fires on OnStateChanged too,
            // so _splashInstance.Play() was already called THIS frame.
            //
            // In UseNaturalCamera mode: the ChaseCamera was following the ball toward water;
            // 2 frames after the OB event the camera should still be near the water (it hasn't
            // yet received the drop-position target from OBDropResolver's re-aim logic).
            // Capture the splash particle burst here — this is the canonical "water splash" frame.
            //
            // WaterSplashController lifetimes: Ripple_Ring=0.60s, Jet_Crown/Scatter_Droplets=0.80s.
            // Even if the camera has partially panned we still have 0.8s of particle life.
            // BURST capture across the splash life. The game-side WaterSplashCameraHold freezes the
            // camera on the water entry for ~1.2s, so every frame here is looking at the splash. Also
            // logs the live splash particle count so we can confirm the VFX actually fired and is framed.
            WaterFramed = true;
            yield return null; // let the burst spawn
            for (int bframe = 0; bframe < 16; bframe++)
            {
                int splashParticles = 0;
                foreach (var ps in Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None))
                    if (ps.transform.root.name.Contains("WaterSplash")) splashParticles += ps.particleCount;
                Debug.Log($"[WaterSplashCaptureRig] burst {bframe:D2} splashParticles={splashParticles}");
                yield return CaptureFrame($"wsplash_burst_{bframe:D2}");
                yield return new WaitForSeconds(0.04f);
            }

            // Remaining hold time.
            float remaining = HoldSeconds - 0.5f;
            if (remaining > 0f) yield return new WaitForSeconds(remaining);

            // 4. Release.
            _camHeldThisFrame = false;
            if (!UseNaturalCamera)
            {
                if (_chase != null) _chase.enabled = true;
                RestoreLoopCameraDirector();
            }
            _sm.OnStateChanged -= OnState;
            Debug.Log("[WaterSplashCaptureRig] Hold released — sequence finished.");
            Finished = true;
        }

        // ── LoopCameraDirector helpers ─────────────────────────────────────────
        // LoopCameraDirector also writes camera transform in LateUpdate (on LabRoot).
        // We disable it for the hold window and restore after.
        MonoBehaviour _loopCamDir;

        void DisableLoopCameraDirector()
        {
            // Locate by type name to avoid a hard reference to the type.
            var allMB = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            foreach (var mb in allMB)
            {
                if (mb.GetType().Name == "LoopCameraDirector")
                {
                    _loopCamDir = mb;
                    mb.enabled  = false;
                    Debug.Log("[WaterSplashCaptureRig] LoopCameraDirector disabled for hold window.");
                    break;
                }
            }
        }

        void RestoreLoopCameraDirector()
        {
            if (_loopCamDir != null)
            {
                _loopCamDir.enabled = true;
                Debug.Log("[WaterSplashCaptureRig] LoopCameraDirector re-enabled.");
            }
        }

        /// <summary>
        /// Captures the current Game View to a PNG file under Docs/Diagnostics/_capture/.
        /// Must be called from a coroutine so the WaitForEndOfFrame yield is valid.
        /// Camera must already be parked at the hold position before calling.
        /// </summary>
        IEnumerator CaptureFrame(string label)
        {
            // In cinematic mode, force the camera to the hold position one final time before capture.
            // In natural-camera mode (_camHeldThisFrame=false), let the chase camera keep its position.
            if (_camHeldThisFrame && _cam != null)
                _cam.transform.SetPositionAndRotation(_camHoldPos, _camHoldRot);

            yield return new WaitForEndOfFrame();

            Texture2D tex = ScreenCapture.CaptureScreenshotAsTexture();
            if (tex == null)
            {
                Debug.LogError($"[WaterSplashCaptureRig] CaptureFrame '{label}': CaptureScreenshotAsTexture returned null.");
                yield break;
            }

            string outDir = "/Users/cesar/Documents/GolfinRedux/Docs/Diagnostics/_capture";
            if (!Directory.Exists(outDir)) Directory.CreateDirectory(outDir);

            string ts = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string path = Path.Combine(outDir, $"{label}_{ts}.png");
            byte[] bytes = tex.EncodeToPNG();
            Object.Destroy(tex);
            File.WriteAllBytes(path, bytes);
            Debug.Log($"[WaterSplashCaptureRig] CaptureFrame '{label}' → {path} ({bytes.Length / 1024}KB)");
            CapturedPaths.Add(path);
        }

        /// <summary>Absolute paths of all captured frames, in order. Populated by CaptureFrame.</summary>
        public List<string> CapturedPaths { get; } = new List<string>();

        void OnState(BallStateChange change)
        {
            if (_waterHit) return;
            if (change.Next != BallState.OB) return;
            if (!change.OBReason.HasValue) return;
            if (change.OBReason.Value != OBReason.Water) return;

            _entryPoint = new Vector3(
                change.Position.x.ToFloat(),
                change.Position.y.ToFloat(),
                change.Position.z.ToFloat());
            _waterHit = true;
        }

        // LateUpdate runs after ChaseCamera.LateUpdate; we override the transform during the hold.
        void LateUpdate()
        {
            if (!_camHeldThisFrame || _cam == null) return;
            _cam.transform.position = _camHoldPos;
            _cam.transform.rotation = _camHoldRot;
        }

        void OnDestroy()
        {
            if (_sm != null) _sm.OnStateChanged -= OnState;
            if (_chase != null) _chase.enabled = true;
            RestoreLoopCameraDirector();
        }
    }
}
#endif
