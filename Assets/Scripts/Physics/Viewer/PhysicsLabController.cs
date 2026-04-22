using System;
using System.Collections.Generic;
using UnityEngine;
using Golfin.Physics;
using Golfin.Physics.Math;
using Golfin.Physics.Runtime;

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
        [SerializeField] HeightProvider     heightProvider;   // Hole1 only

        // Published after every Fire
        public event Action<ShotReadout> OnShotFired;
        // Published after Fire×N
        public event Action<bool, int> OnRepeatabilityResult;   // (passed, count)

        // In-memory configs — mutated by DashboardUI sliders
        public AeroConfig    AeroCfg    { get; private set; }
        public WindConfig    WindCfg    { get; private set; }
        public SurfaceConfig SurfaceCfg { get; private set; }
        public PuttConfig    PuttCfg    { get; private set; }

        // Current ghost trajectory (for Fire & Compare)
        Trajectory _previousTrajectory;

        // ── Unity lifecycle ────────────────────────────────────────────────────

        void Awake()
        {
            AeroCfg    = PhysicsConfigLoader.LoadAeroConfig();
            WindCfg    = PhysicsConfigLoader.LoadWindConfig();
            SurfaceCfg = PhysicsConfigLoader.LoadSurfaceConfig();
            PuttCfg    = PhysicsConfigLoader.LoadPuttConfig();
        }

        // ── Public API ─────────────────────────────────────────────────────────

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

            // Show the last trajectory
            var last = RunSim(preset);
            trajectoryRenderer.Draw(last);
            ballAnimator.Play(last);
        }

        public void Clear()
        {
            trajectoryRenderer.Clear();
            _previousTrajectory = null;
        }

        // Called by DashboardUI to reload configs from CSV
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

        // Mutators for Dashboard sliders (in-memory only)
        public void SetAeroConfig(AeroConfig cfg)    => AeroCfg    = cfg;
        public void SetSurfaceConfig(SurfaceConfig cfg) => SurfaceCfg = cfg;
        public void SetPuttConfig(PuttConfig cfg)    => PuttCfg    = cfg;
        public void SetWindConfig(WindConfig cfg)    => WindCfg    = cfg;

        // ── Internal ───────────────────────────────────────────────────────────

        void FireInternal(ShotPreset preset)
        {
            var trajectory = RunSim(preset);
            _previousTrajectory = trajectory;

            trajectoryRenderer.Draw(trajectory);
            ballAnimator.Play(trajectory);

            // Camera — use first trajectory sample so Y is terrain-snapped (not preset's raw Y=0)
            var s0 = trajectory.samples != null && trajectory.samples.Count > 0
                ? trajectory.samples[0].position
                : preset.Origin;
            Vector3 origin    = new Vector3(s0.x.ToFloat(), s0.y.ToFloat(), s0.z.ToFloat());
            Vector3 launchDir = new Vector3(preset.Velocity.x.ToFloat(), 0f, preset.Velocity.z.ToFloat()).normalized;
            if (launchDir == Vector3.zero) launchDir = Vector3.right;

            if (chaseCamera != null)
            {
                chaseCamera.SetTarget(ballAnimator.CurrentBall);
                chaseCamera.ResetToOrigin(origin, launchDir);
            }

            // Readout
            var readout = BuildReadout(preset, trajectory);
            OnShotFired?.Invoke(readout);
            LogReadout(readout);
        }

        Trajectory RunSim(ShotPreset preset)
        {
            var input = new ShotInput(
                preset.Origin,
                preset.Velocity,
                fp.FromInt(60),
                preset.Spin);

            var ground  = BuildGroundProvider();
            var surface = BuildSurfaceProvider(preset);
            var wind    = preset.Wind;

            return BallSimulation.Simulate(input, ground, AeroCfg, wind, surface, SurfaceCfg, PuttCfg);
        }

        IGroundProvider BuildGroundProvider()
        {
            if (currentScene == PresetScene.Hole1 && heightProvider != null && heightProvider.Data != null)
                return heightProvider.Data;
            return new FlatGround(fp.Zero);
        }

        ISurfaceProvider BuildSurfaceProvider(ShotPreset preset)
        {
            if (currentScene == PresetScene.Hole1)
                return new SceneSurfaceProvider();

            // Range / Dashboard: use preset surface override or default Fairway
            SurfaceType surfaceType = preset.HasSurfaceOverride ? preset.SurfaceOverride : SurfaceType.Fairway;
            return new ConstantSurfaceProvider(surfaceType);
        }

        ShotReadout BuildReadout(ShotPreset preset, Trajectory t)
        {
            float carryM = 0f;
            SurfaceType finalSurface = SurfaceType.Fairway;

            if (t.terrainHits != null && t.terrainHits.Count > 0)
            {
                var firstHit = t.terrainHits[0];
                carryM = XZDist(preset.Origin, firstHit.Position);

                var lastHit = t.terrainHits[t.terrainHits.Count - 1];
                finalSurface = lastHit.Surface;
            }

            float totalM   = XZDist(preset.Origin, t.finalPosition);
            float peakY    = 0f;
            float originY  = preset.Origin.y.ToFloat();
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
