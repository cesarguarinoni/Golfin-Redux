using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Golfin.Physics;
using Golfin.Physics.Math;
using Golfin.Physics.Runtime;
using Golfin.Gameplay.Input;
using Golfin.Gameplay.UI.ShotUI;

namespace Golfin.Physics.Viewer
{
    /// <summary>
    /// Scene brain for the physics lab. Owns configs, wires sim → renderer → animator → UI.
    /// Attach to a LabRoot GameObject; assign references in the Inspector.
    /// </summary>
    public class PhysicsLabController : MonoBehaviour
    {
        [Header("Scene Identity")]
        [SerializeField] PresetScene currentScene = PresetScene.Range;

        [Header("References")]
        [SerializeField] TrajectoryRenderer trajectoryRenderer;
        [SerializeField] BallAnimator       ballAnimator;
        [SerializeField] ChaseCamera        chaseCamera;

        [Header("Shot Controller (Live Touch)")]
        [SerializeField] ShotController _shotController;
        [SerializeField] ShotConeView   _shotConeView;
        [SerializeField] Transform      _ballSpawnPoint;

        [Header("Camera")]
        [Tooltip("XZ look direction when the scene starts (toward the target hole). Set to match your hole orientation.")]
        [SerializeField] Vector3 _defaultLookDirection = Vector3.forward;
        [SerializeField] float   _orbitSensitivity     = 0.5f;

        // Published after every Fire
        public event Action<ShotReadout> OnShotFired;
        // Published after Fire×N
        public event Action<bool, int> OnRepeatabilityResult;

        // In-memory configs
        public AeroConfig    AeroCfg    { get; private set; }
        public WindConfig    WindCfg    { get; private set; }
        public SurfaceConfig SurfaceCfg { get; private set; }
        public PuttConfig    PuttCfg    { get; private set; }

        Trajectory _previousTrajectory;

        // Camera orbit state
        float   _cameraYaw;
        Vector3 _orbitCenter;
        bool    _prevBallPlaying;

        // ── Unity lifecycle ────────────────────────────────────────────────────

        void Awake()
        {
            AeroCfg    = PhysicsConfigLoader.LoadAeroConfig();
            WindCfg    = PhysicsConfigLoader.LoadWindConfig();
            SurfaceCfg = PhysicsConfigLoader.LoadSurfaceConfig();
            PuttCfg    = PhysicsConfigLoader.LoadPuttConfig();

            if (_shotController != null)
                _shotController.OnShotResolved += HandleShotResolved;

            if (_shotConeView != null)
            {
                if (chaseCamera != null)
                    _shotConeView.SetCamera(chaseCamera.GetComponent<Camera>());

                _shotConeView.SetMaxCarryYards(ComputeMaxCarryYards());
            }
        }

        void OnDestroy()
        {
            if (_shotController != null)
                _shotController.OnShotResolved -= HandleShotResolved;
        }

        void Start() => SetupAtTee();

        void Update()
        {
            HandleCameraOrbit();
        }

        // ── Public API ─────────────────────────────────────────────────────────

        public void ResetToTee()
        {
            if (_shotController != null) _shotController.CompleteShot();
            SetupAtTee();
        }

        private void SetupAtTee()
        {
            if (_ballSpawnPoint == null) return;
            Vector3 sp = _ballSpawnPoint.position;
            RaycastHit hit;
            float surfaceY = UnityEngine.Physics.Raycast(
                new Vector3(sp.x, 500f, sp.z), Vector3.down, out hit, 1000f)
                ? hit.point.y : sp.y;
            Vector3 teePos = new Vector3(sp.x, surfaceY, sp.z);

            _orbitCenter = teePos;

            if (ballAnimator != null) ballAnimator.PlaceAtRest(teePos);

            // Compute look direction from serialized field, fall back to +X
            Vector3 lookDir = _defaultLookDirection.sqrMagnitude > 0.001f
                ? new Vector3(_defaultLookDirection.x, 0f, _defaultLookDirection.z).normalized
                : Vector3.right;

            _cameraYaw = Mathf.Atan2(lookDir.z, lookDir.x);

            if (_shotController != null)
                _shotController.CameraHeadingRadians = _cameraYaw;

            Camera cam = chaseCamera != null ? chaseCamera.GetComponent<Camera>() : null;
            if (cam != null)
                ApplyCameraYaw(cam);
        }

        // ── Camera orbit ───────────────────────────────────────────────────────

        void HandleCameraOrbit()
        {
            // Don't orbit while the player is in the middle of a shot drag or ball is flying.
            if (_shotController != null && _shotController.IsExternalDragActive) return;

            bool isPlaying = ballAnimator != null && ballAnimator.IsPlaying;

            // When the ball just stopped, park the camera and clear chase target.
            if (_prevBallPlaying && !isPlaying)
            {
                if (ballAnimator?.CurrentBall != null)
                    _orbitCenter = ballAnimator.CurrentBall.position;

                if (chaseCamera != null) chaseCamera.SetTarget(null);
            }
            _prevBallPlaying = isPlaying;

            if (isPlaying) return;

            var mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.isPressed) return;

            float dx = mouse.delta.x.ReadValue();
            if (Mathf.Abs(dx) < 0.5f) return;

            _cameraYaw += dx * _orbitSensitivity * Mathf.Deg2Rad;

            if (_shotController != null)
                _shotController.CameraHeadingRadians = _cameraYaw;

            Camera cam = chaseCamera?.GetComponent<Camera>();
            if (cam != null) ApplyCameraYaw(cam);
        }

        void ApplyCameraYaw(Camera cam)
        {
            // yaw=0 → +X forward (matching ShotInputBuilder convention)
            Vector3 lookDir = new Vector3(Mathf.Cos(_cameraYaw), 0f, Mathf.Sin(_cameraYaw));
            cam.transform.position = _orbitCenter - lookDir * 8f + Vector3.up * 3f;
            cam.transform.LookAt(_orbitCenter + lookDir * 3f + Vector3.up * 0.5f);
        }

        // ── Preset firing ──────────────────────────────────────────────────────

        public void Fire(ShotPreset preset)
        {
            _previousTrajectory = null;
            FireInternal(preset);
        }

        public void FireCompare(ShotPreset preset)
        {
            if (_previousTrajectory != null)
                trajectoryRenderer.SetGhost(true);
            FireInternal(preset);
        }

        public void FireRepeatability(ShotPreset preset, int count = 5)
        {
            var positions = new fp3[count];
            for (int i = 0; i < count; i++)
            {
                var t = RunSim(preset);
                positions[i] = t.finalPosition;
            }

            bool bitExact = true;
            for (int i = 1; i < count; i++)
            {
                if (positions[i].x.ToFloat() != positions[0].x.ToFloat() ||
                    positions[i].y.ToFloat() != positions[0].y.ToFloat() ||
                    positions[i].z.ToFloat() != positions[0].z.ToFloat())
                {
                    bitExact = false;
                    break;
                }
            }

            Debug.Log($"[PhysicsLab] Fire×{count}: {(bitExact ? "✓ BIT-EXACT" : "✗ DRIFT DETECTED")}");
            OnRepeatabilityResult?.Invoke(bitExact, count);

            var last = RunSim(preset);
            trajectoryRenderer.Draw(last);
            ballAnimator.Play(last);
        }

        public void Clear()
        {
            trajectoryRenderer.Clear();
            _previousTrajectory = null;
        }

        public void ReloadConfigs()
        {
            AeroCfg    = PhysicsConfigLoader.LoadAeroConfig();
            WindCfg    = PhysicsConfigLoader.LoadWindConfig();
            SurfaceCfg = PhysicsConfigLoader.LoadSurfaceConfig();
            PuttCfg    = PhysicsConfigLoader.LoadPuttConfig();
        }

        public void ResetToDefaults()
        {
            AeroCfg    = AeroConfig.Default;
            WindCfg    = WindConfig.Calm;
            SurfaceCfg = SurfaceConfig.Default;
            PuttCfg    = PuttConfig.Default;
        }

        public void SetAeroConfig(AeroConfig cfg)       => AeroCfg    = cfg;
        public void SetSurfaceConfig(SurfaceConfig cfg) => SurfaceCfg = cfg;
        public void SetPuttConfig(PuttConfig cfg)       => PuttCfg    = cfg;
        public void SetWindConfig(WindConfig cfg)       => WindCfg    = cfg;

        // ── Touch shot integration ─────────────────────────────────────────────

        void HandleShotResolved(ShotInput input, BallPhysicsModifiers ballMods)
        {
            fp3 ballOrigin;
            if (ballAnimator != null && ballAnimator.CurrentBall != null)
            {
                var p = ballAnimator.CurrentBall.position;
                ballOrigin = new fp3(fp.FromFloat(p.x), fp.FromFloat(p.y), fp.FromFloat(p.z));
            }
            else if (_ballSpawnPoint != null)
            {
                Vector3 sp = _ballSpawnPoint.position;
                RaycastHit hit;
                float surfaceY = UnityEngine.Physics.Raycast(
                    new Vector3(sp.x, 500f, sp.z), Vector3.down, out hit, 1000f)
                    ? hit.point.y
                    : sp.y;
                ballOrigin = new fp3(fp.FromFloat(sp.x), fp.FromFloat(surfaceY), fp.FromFloat(sp.z));
            }
            else
            {
                ballOrigin = input.origin;
            }

            var correctedInput = new ShotInput(ballOrigin, input.velocity, input.maxDuration, input.Spin, input.seed);

            var trajectory = RunSimFromController(correctedInput, ballMods);
            _previousTrajectory = trajectory;

            trajectoryRenderer.Draw(trajectory);
            ballAnimator.Play(trajectory);

            if (_shotConeView != null && ballAnimator != null && ballAnimator.CurrentBall != null)
                _shotConeView.SetBallTransform(ballAnimator.CurrentBall);

            var s0 = trajectory.samples != null && trajectory.samples.Count > 0
                ? trajectory.samples[0].position
                : correctedInput.origin;
            Vector3 origin    = new Vector3(s0.x.ToFloat(), s0.y.ToFloat(), s0.z.ToFloat());
            Vector3 launchDir = new Vector3(correctedInput.velocity.x.ToFloat(), 0f,
                                             correctedInput.velocity.z.ToFloat()).normalized;
            if (launchDir == Vector3.zero) launchDir = Vector3.right;

            if (chaseCamera != null)
            {
                chaseCamera.SetTarget(ballAnimator.CurrentBall);
                chaseCamera.ResetToOrigin(origin, launchDir);
            }

            // Keep orbit center at launch so user can spin back to see the shot
            _orbitCenter = origin;

            float carryM  = 0f;
            SurfaceType finalSurface = SurfaceType.Fairway;
            if (trajectory.terrainHits != null && trajectory.terrainHits.Count > 0)
            {
                carryM       = XZDist(correctedInput.origin, trajectory.terrainHits[0].Position);
                finalSurface = trajectory.terrainHits[trajectory.terrainHits.Count - 1].Surface;
            }
            float totalM = XZDist(correctedInput.origin, trajectory.finalPosition);
            float peakY  = 0f;
            float originY = correctedInput.origin.y.ToFloat();
            foreach (var s in trajectory.samples)
            {
                float y = s.position.y.ToFloat() - originY;
                if (y > peakY) peakY = y;
            }
            int bounceCount = 0;
            if (trajectory.terrainHits != null)
                foreach (var h in trajectory.terrainHits)
                    if (!h.IsStop) bounceCount++;

            var readout = new ShotReadout
            {
                PresetDisplayName  = "[Touch Shot]",
                CarryMeters        = carryM,
                TotalMeters        = totalM,
                MaxHeightMeters    = peakY,
                BounceCount        = bounceCount,
                TerminationReason  = trajectory.termination.ToString(),
                FinalSurface       = finalSurface,
                SimDurationSeconds = trajectory.finalTime.ToFloat(),
            };
            OnShotFired?.Invoke(readout);
            LogReadout(readout);
        }

        Trajectory RunSimFromController(ShotInput input, BallPhysicsModifiers ballMods)
        {
            var ground  = BuildGroundProvider();
            var surface = BuildSurfaceProvider(default(ShotPreset));
            return BallSimulation.Simulate(input, ground, AeroCfg, WindCfg, surface, SurfaceCfg, PuttCfg, ballMods);
        }

        float ComputeMaxCarryYards()
        {
            float velMps   = 75f;
            float pitchRad = 10.9f * Mathf.Deg2Rad;
            var vel = new fp3(
                fp.FromFloat(velMps * Mathf.Cos(pitchRad)),
                fp.FromFloat(velMps * Mathf.Sin(pitchRad)),
                fp.Zero);
            var simInput = new ShotInput(fp3.Zero, vel, fp.FromInt(60));
            var ground   = new FlatGround(fp.Zero);
            var surface  = new ConstantSurfaceProvider(SurfaceType.Fairway);
            var traj = BallSimulation.Simulate(simInput, ground, AeroCfg, WindConfig.Calm,
                surface, SurfaceCfg, PuttCfg, BallPhysicsModifiers.Neutral);

            fp3 landPos = traj.terrainHits != null && traj.terrainHits.Count > 0
                ? traj.terrainHits[0].Position
                : traj.finalPosition;
            return XZDist(fp3.Zero, landPos) * 1.09361f;
        }

        // ── Internal ───────────────────────────────────────────────────────────

        void FireInternal(ShotPreset preset)
        {
            var trajectory = RunSim(preset);
            _previousTrajectory = trajectory;

            trajectoryRenderer.Draw(trajectory);
            ballAnimator.Play(trajectory);

            var s0 = trajectory.samples != null && trajectory.samples.Count > 0
                ? trajectory.samples[0].position
                : preset.Origin;
            Vector3 origin    = new Vector3(s0.x.ToFloat(), s0.y.ToFloat(), s0.z.ToFloat());
            Vector3 launchDir = new Vector3(preset.Velocity.x.ToFloat(), 0f, preset.Velocity.z.ToFloat()).normalized;
            if (launchDir == Vector3.zero) launchDir = Vector3.right;

            _orbitCenter = origin;

            if (chaseCamera != null)
            {
                chaseCamera.SetTarget(ballAnimator.CurrentBall);
                chaseCamera.ResetToOrigin(origin, launchDir);
            }

            var readout = BuildReadout(preset, trajectory);
            OnShotFired?.Invoke(readout);
            LogReadout(readout);
        }

        Trajectory RunSim(ShotPreset preset)
        {
            // Surface-snap the origin Y for Hole1 presets (stored with Y=0).
            fp3 origin = preset.Origin;
            if (currentScene == PresetScene.Hole1)
            {
                float ox = origin.x.ToFloat(), oz = origin.z.ToFloat();
                if (UnityEngine.Physics.Raycast(new Vector3(ox, 500f, oz), Vector3.down,
                    out RaycastHit hit, 1000f))
                    origin = new fp3(origin.x, fp.FromFloat(hit.point.y), origin.z);
            }

            var input = new ShotInput(origin, preset.Velocity, fp.FromInt(60), preset.Spin);
            var ground  = BuildGroundProvider();
            var surface = BuildSurfaceProvider(preset);
            var wind    = preset.Wind;

            return BallSimulation.Simulate(input, ground, AeroCfg, wind, surface, SurfaceCfg, PuttCfg);
        }

        IGroundProvider BuildGroundProvider()
        {
            if (currentScene == PresetScene.Hole1)
                return new SceneGroundProvider();
            return new FlatGround(fp.Zero);
        }

        ISurfaceProvider BuildSurfaceProvider(ShotPreset preset)
        {
            if (currentScene == PresetScene.Hole1)
                return new SceneSurfaceProvider();

            SurfaceType surfaceType = preset.HasSurfaceOverride ? preset.SurfaceOverride : SurfaceType.Fairway;
            return new ConstantSurfaceProvider(surfaceType);
        }

        ShotReadout BuildReadout(ShotPreset preset, Trajectory t)
        {
            float carryM = 0f;
            SurfaceType finalSurface = SurfaceType.Fairway;

            if (t.terrainHits != null && t.terrainHits.Count > 0)
            {
                carryM       = XZDist(preset.Origin, t.terrainHits[0].Position);
                finalSurface = t.terrainHits[t.terrainHits.Count - 1].Surface;
            }

            float totalM  = XZDist(preset.Origin, t.finalPosition);
            float peakY   = 0f;
            float originY = preset.Origin.y.ToFloat();
            foreach (var s in t.samples)
            {
                float y = s.position.y.ToFloat() - originY;
                if (y > peakY) peakY = y;
            }

            int bounceCount = 0;
            if (t.terrainHits != null)
                foreach (var h in t.terrainHits)
                    if (!h.IsStop) bounceCount++;

            return new ShotReadout
            {
                PresetDisplayName  = preset.DisplayName,
                CarryMeters        = carryM,
                TotalMeters        = totalM,
                MaxHeightMeters    = peakY,
                BounceCount        = bounceCount,
                TerminationReason  = t.termination.ToString(),
                FinalSurface       = finalSurface,
                SimDurationSeconds = t.finalTime.ToFloat(),
            };
        }

        static float XZDist(fp3 a, fp3 b)
        {
            float dx = b.x.ToFloat() - a.x.ToFloat();
            float dz = b.z.ToFloat() - a.z.ToFloat();
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        static void LogReadout(ShotReadout r)
        {
            Debug.Log($"[PhysicsLab] {r.PresetDisplayName}\n" +
                      $"  Carry:   {r.CarryMeters:F1}m ({r.CarryMeters * 1.09361f:F1}yd)\n" +
                      $"  Total:   {r.TotalMeters:F1}m ({r.TotalMeters * 1.09361f:F1}yd)\n" +
                      $"  Peak:    {r.MaxHeightMeters:F1}m\n" +
                      $"  Bounces: {r.BounceCount}\n" +
                      $"  Ended:   {r.TerminationReason} on {r.FinalSurface}\n" +
                      $"  Time:    {r.SimDurationSeconds:F2}s");
        }
    }

    public struct ShotReadout
    {
        public string      PresetDisplayName;
        public float       CarryMeters;
        public float       TotalMeters;
        public float       MaxHeightMeters;
        public int         BounceCount;
        public string      TerminationReason;
        public SurfaceType FinalSurface;
        public float       SimDurationSeconds;
    }
}
