using System.Linq;
using NUnit.Framework;
using Golfin.Physics;
using Golfin.Physics.Math;
using Golfin.Physics.Runtime;
using Golfin.Physics.Viewer;

namespace Golfin.Physics.Tests
{
    /// <summary>
    /// 4 EditMode tests covering the ShotPresetCatalog data and driver-calm simulation.
    /// Target: 35 existing + 4 new = 39 tests total, 39 pass.
    /// </summary>
    public class ViewerTests
    {
        // ── Test 1 ─────────────────────────────────────────────────────────────

        [Test]
        public void Viewer_PresetCatalog_AllIdsUnique()
        {
            var all  = ShotPresetCatalog.All;
            var ids  = all.Select(p => p.Id).ToList();
            var distinct = ids.Distinct().ToList();

            Assert.AreEqual(ids.Count, distinct.Count,
                $"ShotPresetCatalog has duplicate IDs. Total={ids.Count}, Distinct={distinct.Count}. " +
                $"Duplicates: {string.Join(", ", ids.GroupBy(x => x).Where(g => g.Count() > 1).Select(g => g.Key))}");
        }

        // ── Test 2 ─────────────────────────────────────────────────────────────

        [Test]
        public void Viewer_PresetCatalog_SceneCountsCorrect()
        {
            int rangeCount     = ShotPresetCatalog.ForScene(PresetScene.Range).Count();
            int hole1Count     = ShotPresetCatalog.ForScene(PresetScene.Hole1).Count();
            int dashboardCount = ShotPresetCatalog.ForScene(PresetScene.Dashboard).Count();

            Assert.AreEqual(10, rangeCount,
                $"Expected 10 Range presets, got {rangeCount}.");
            Assert.AreEqual(5, hole1Count,
                $"Expected 5 Hole1 presets, got {hole1Count}.");
            Assert.GreaterOrEqual(dashboardCount, 1,
                $"Expected ≥1 Dashboard presets, got {dashboardCount}.");
        }

        // ── Test 3 ─────────────────────────────────────────────────────────────

        [Test]
        public void Viewer_DriverCalm_CarryInExpectedRange()
        {
            var preset = ShotPresetCatalog.All.First(p => p.Id == "driver_calm");

            var input   = new ShotInput(preset.Origin, preset.Velocity, fp.FromInt(60), preset.Spin);
            var ground  = new FlatGround(fp.Zero);
            var aero    = PhysicsConfigLoader.LoadAeroConfig();
            var surface = new ConstantSurfaceProvider(SurfaceType.Fairway);
            var surfCfg = PhysicsConfigLoader.LoadSurfaceConfig();
            var puttCfg = PhysicsConfigLoader.LoadPuttConfig();

            var traj = BallSimulation.Simulate(input, ground, aero, preset.Wind, surface, surfCfg, puttCfg);

            // Carry = XZ distance from origin to first TerrainHit (or finalPosition if no hits)
            float carryM;
            if (traj.terrainHits != null && traj.terrainHits.Count > 0)
            {
                var hit = traj.terrainHits[0];
                float dx = hit.Position.x.ToFloat() - preset.Origin.x.ToFloat();
                float dz = hit.Position.z.ToFloat() - preset.Origin.z.ToFloat();
                carryM = UnityEngine.Mathf.Sqrt(dx * dx + dz * dz);
            }
            else
            {
                float dx = traj.finalPosition.x.ToFloat() - preset.Origin.x.ToFloat();
                float dz = traj.finalPosition.z.ToFloat() - preset.Origin.z.ToFloat();
                carryM = UnityEngine.Mathf.Sqrt(dx * dx + dz * dz);
            }

            float carryYd = carryM * 1.09361f;
            UnityEngine.Debug.Log($"[ViewerTests] driver_calm carry: {carryM:F1}m ({carryYd:F1}yd). Target: 175–230m (191–251yd).");

            Assert.GreaterOrEqual(carryM, 175f,
                $"driver_calm carry {carryM:F1}m is below 175m minimum. Termination: {traj.termination}");
            Assert.LessOrEqual(carryM, 230f,
                $"driver_calm carry {carryM:F1}m exceeds 230m maximum. Termination: {traj.termination}");
        }

        // ── Test 4 ─────────────────────────────────────────────────────────────

        [Test]
        public void Viewer_FireRepeatability_IsBitExact()
        {
            var preset = ShotPresetCatalog.All.First(p => p.Id == "driver_calm");

            var input   = new ShotInput(preset.Origin, preset.Velocity, fp.FromInt(60), preset.Spin);
            var ground  = new FlatGround(fp.Zero);
            var aero    = PhysicsConfigLoader.LoadAeroConfig();
            var surface = new ConstantSurfaceProvider(SurfaceType.Fairway);
            var surfCfg = PhysicsConfigLoader.LoadSurfaceConfig();
            var puttCfg = PhysicsConfigLoader.LoadPuttConfig();

            const int N = 5;
            var finals = new fp3[N];
            for (int i = 0; i < N; i++)
            {
                var t = BallSimulation.Simulate(input, ground, aero, preset.Wind, surface, surfCfg, puttCfg);
                finals[i] = t.finalPosition;
            }

            for (int i = 1; i < N; i++)
            {
                Assert.AreEqual(finals[0].x.ToFloat(), finals[i].x.ToFloat(), 0f,
                    $"finalPosition.x differs between run 0 and run {i}: {finals[0].x.ToFloat():F6} vs {finals[i].x.ToFloat():F6}");
                Assert.AreEqual(finals[0].y.ToFloat(), finals[i].y.ToFloat(), 0f,
                    $"finalPosition.y differs between run 0 and run {i}");
                Assert.AreEqual(finals[0].z.ToFloat(), finals[i].z.ToFloat(), 0f,
                    $"finalPosition.z differs between run 0 and run {i}");
            }

            UnityEngine.Debug.Log($"[ViewerTests] Repeatability: {N}/{N} runs bit-exact at final position ({finals[0].x.ToFloat():F4}, {finals[0].y.ToFloat():F4}, {finals[0].z.ToFloat():F4})");
        }
    }
}
