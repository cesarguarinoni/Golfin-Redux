using System.Collections;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Golfin.Physics;
using Golfin.Physics.Math;
using Golfin.Physics.Runtime;

namespace Golfin.Physics.Tests
{
    /// <summary>
    /// Phase 5 stress tests (tests 12–16). ~3,500 shots across all surface types.
    /// Each test creates synthetic BoxCollider geometry (large flat surface with an
    /// overlapping higher-Y surface that would cause fall-through without the fix),
    /// then fires many varied shots and checks zero fall-through frames.
    ///
    /// Outputs a summary table to the test message on completion.
    /// </summary>
    public class TerrainStressTests
    {
        const float Epsilon = 0.005f;

        readonly List<GameObject> _created = new();

        [TearDown]
        public void Cleanup()
        {
            foreach (var go in _created)
                if (go != null) Object.DestroyImmediate(go);
            _created.Clear();
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        GameObject CreateFlat(float cx, float cy, float cz, SurfaceType type, float w, float d)
        {
            var go = new GameObject($"Stress_{type}");
            _created.Add(go);
            go.transform.position = new Vector3(cx, cy - 0.01f, cz);
            var box = go.AddComponent<BoxCollider>();
            box.size = new Vector3(w, 0.02f, d);
            var marker = go.AddComponent<SurfaceMarker>();
            marker.Type = type;
            return go;
        }

        // Simple LCG for deterministic shot variation.
        uint _rng;
        void SeedRng(uint seed) => _rng = seed;
        uint NextU() { _rng = _rng * 1664525u + 1013904223u; return _rng; }
        float NextF(float lo, float hi) => lo + (NextU() / (float)uint.MaxValue) * (hi - lo);

        struct StatsAccum
        {
            public int shots;
            public long framesChecked;
            public int fallThroughs;
            public float minGap;
            public float maxGap;
            public StatsAccum(int _) { shots = 0; framesChecked = 0; fallThroughs = 0; minGap = float.MaxValue; maxGap = float.MinValue; }

            public void Record(float gap)
            {
                framesChecked++;
                if (gap < minGap) minGap = gap;
                if (gap > maxGap) maxGap = gap;
                if (gap < -Epsilon) fallThroughs++;
            }

            public string Row(string label)
            {
                float lo = minGap == float.MaxValue ? 0f : minGap;
                float hi = maxGap == float.MinValue ? 0f : maxGap;
                return $"{label,-14}| {shots,5} | {framesChecked,14} | {fallThroughs,13} | {lo,+13:F4} | {hi,+13:F4}";
            }
        }

        void CollectStats(
            Trajectory traj,
            SurfaceType surface,
            SceneGroundProvider ground,
            SceneSurfaceProvider surfaces,
            ref StatsAccum stats)
        {
            foreach (var s in traj.samples)
            {
                fp bx = s.position.x, bz = s.position.z;
                if (surfaces.Classify(bx, bz) != surface) continue;
                float groundY = ground.SampleHeight(bx, bz, surface).ToFloat();
                float gap = s.position.y.ToFloat() - groundY;
                stats.Record(gap);
            }
        }

        fp3 PuttVel(float yaw, float speed) =>
            new fp3(
                fp.FromFloat(speed * Mathf.Cos(3f * Mathf.Deg2Rad) * Mathf.Cos(yaw)),
                fp.FromFloat(speed * Mathf.Sin(3f * Mathf.Deg2Rad)),
                fp.FromFloat(speed * Mathf.Cos(3f * Mathf.Deg2Rad) * Mathf.Sin(yaw)));

        fp3 ShotVel(float yaw, float speed, float pitchRad) =>
            new fp3(
                fp.FromFloat(speed * Mathf.Cos(pitchRad) * Mathf.Cos(yaw)),
                fp.FromFloat(speed * Mathf.Sin(pitchRad)),
                fp.FromFloat(speed * Mathf.Cos(pitchRad) * Mathf.Sin(yaw)));

        // ── Test 12 ─────────────────────────────────────────────────────────────
        // 1000 green putts from random positions on a large green (40x40m)
        // with an overlapping Fairway/fringe surface 3cm higher.

        [UnityTest]
        public IEnumerator T12_Green1000Putts_ZeroFallThrough()
        {
            float cx = 500f, cz = 500f;
            // Fringe higher, green lower — the classic fringe-on-green scenario.
            CreateFlat(cx, 10.18f, cz, SurfaceType.Fairway, 50f, 50f);
            CreateFlat(cx, 10.15f, cz, SurfaceType.Green,   40f, 40f);

            yield return null;

            var ground   = new SceneGroundProvider();
            var surfaces = new SceneSurfaceProvider();
            var stats    = new StatsAccum(0);
            SeedRng(12345u);

            float gy = ground.SampleHeight(fp.FromFloat(cx), fp.FromFloat(cz), SurfaceType.Green).ToFloat();

            for (int i = 0; i < 1000; i++)
            {
                float ox = cx + NextF(-18f, 18f);
                float oz = cz + NextF(-18f, 18f);
                float speed = NextF(1.0f, 5.0f);
                float yaw   = NextF(0f, Mathf.PI * 2f);

                var origin = new fp3(fp.FromFloat(ox), fp.FromFloat(gy + 0.021f), fp.FromFloat(oz));
                var input  = new ShotInput(origin, PuttVel(yaw, speed), fp.FromInt(30), SpinState.None);
                var traj   = BallSimulation.Simulate(input, ground, AeroConfig.Default, WindConfig.Calm,
                    surfaces, SurfaceConfig.Default, PuttConfig.Default, BallPhysicsModifiers.Neutral);

                stats.shots++;
                CollectStats(traj, SurfaceType.Green, ground, surfaces, ref stats);
            }

            string header = $"\nSurface        | Shots | Frames checked | Fall-throughs | Min Y-gap (m) | Max Y-gap (m)\n" +
                             $"---------------+-------+----------------+---------------+---------------+---------------\n";
            string row = stats.Row("Green (putt)");
            TestContext.WriteLine(header + row);

            Assert.That(stats.fallThroughs, Is.Zero,
                $"T12: {stats.fallThroughs} fall-through frames in 1000 green putts. MinGap={stats.minGap:F4}m");
        }

        // ── Test 13 ─────────────────────────────────────────────────────────────
        // 500 bunker/sand shots from random positions in a Sand surface (30x30m)
        // with overlapping Fairway terrain 1.3m above.

        [UnityTest]
        public IEnumerator T13_Bunker500Shots_ZeroFallThrough()
        {
            float cx = 600f, cz = 500f;
            CreateFlat(cx, 10.0f, cz, SurfaceType.Fairway, 40f, 40f);
            CreateFlat(cx,  8.7f, cz, SurfaceType.Sand,    30f, 30f);

            yield return null;

            var ground   = new SceneGroundProvider();
            var surfaces = new SceneSurfaceProvider();
            var stats    = new StatsAccum(0);
            SeedRng(13579u);

            float gy = ground.SampleHeight(fp.FromFloat(cx), fp.FromFloat(cz), SurfaceType.Sand).ToFloat();

            for (int i = 0; i < 500; i++)
            {
                float ox = cx + NextF(-12f, 12f);
                float oz = cz + NextF(-12f, 12f);
                float speed = NextF(2.0f, 7.0f);
                float yaw   = NextF(0f, Mathf.PI * 2f);
                float pitch = NextF(0.2f, 0.6f);

                var origin = new fp3(fp.FromFloat(ox), fp.FromFloat(gy + 0.021f), fp.FromFloat(oz));
                var input  = new ShotInput(origin, ShotVel(yaw, speed, pitch), fp.FromInt(25), SpinState.None);
                var traj   = BallSimulation.Simulate(input, ground, AeroConfig.Default, WindConfig.Calm,
                    surfaces, SurfaceConfig.Default, PuttConfig.Default, BallPhysicsModifiers.Neutral);

                stats.shots++;
                CollectStats(traj, SurfaceType.Sand, ground, surfaces, ref stats);
            }

            TestContext.WriteLine(stats.Row("Bunker (Sand)"));
            Assert.That(stats.fallThroughs, Is.Zero,
                $"T13: {stats.fallThroughs} fall-through frames in 500 bunker shots. MinGap={stats.minGap:F4}m");
        }

        // ── Test 14 ─────────────────────────────────────────────────────────────
        // 500 approach shots landing on a green.
        // Ball starts 20m above, angled toward random XZ on the green.
        // Check post-landing roll-out stays on green (not above fringe).

        [UnityTest]
        public IEnumerator T14_Green500ApproachLandings_ZeroFallThrough()
        {
            float cx = 700f, cz = 500f;
            CreateFlat(cx, 10.05f, cz, SurfaceType.Fairway, 60f, 60f);
            CreateFlat(cx,  9.85f, cz, SurfaceType.Green,   35f, 35f);

            yield return null;

            var ground   = new SceneGroundProvider();
            var surfaces = new SceneSurfaceProvider();
            var stats    = new StatsAccum(0);
            SeedRng(24680u);

            float gy = ground.SampleHeight(fp.FromFloat(cx), fp.FromFloat(cz), SurfaceType.Green).ToFloat();

            for (int i = 0; i < 500; i++)
            {
                // Target somewhere on the green.
                float tx = cx + NextF(-14f, 14f);
                float tz = cz + NextF(-14f, 14f);
                // Start from a random offset, 20m above green.
                float dx = NextF(-20f, 20f);
                float dz = NextF(-20f, 20f);
                float ox = tx + dx, oz = tz + dz, oy = gy + 20f;

                float hDist = Mathf.Sqrt(dx * dx + dz * dz);
                float hYaw  = Mathf.Atan2(-dz, -dx);
                float speed = NextF(15f, 30f);
                float pitch = NextF(-0.3f, -0.1f);

                var origin = new fp3(fp.FromFloat(ox), fp.FromFloat(oy), fp.FromFloat(oz));
                var input  = new ShotInput(origin, ShotVel(hYaw, speed, pitch), fp.FromInt(30), SpinState.None);
                var traj   = BallSimulation.Simulate(input, ground, AeroConfig.Default, WindConfig.Calm,
                    surfaces, SurfaceConfig.Default, PuttConfig.Default, BallPhysicsModifiers.Neutral);

                stats.shots++;
                CollectStats(traj, SurfaceType.Green, ground, surfaces, ref stats);
            }

            TestContext.WriteLine(stats.Row("Green (land)"));
            Assert.That(stats.fallThroughs, Is.Zero,
                $"T14: {stats.fallThroughs} fall-through frames in 500 approach landings. MinGap={stats.minGap:F4}m");
        }

        // ── Test 15 ─────────────────────────────────────────────────────────────
        // 1000 fairway shots. Single Fairway surface, no overlapping conflicting surface.
        // Tests that normal shots still work correctly after the 3-arg fix.

        [UnityTest]
        public IEnumerator T15_Fairway1000Shots_ZeroFallThrough()
        {
            float cx = 800f, cz = 500f;
            // Large fairway with no overlapping surface to trip the fix.
            CreateFlat(cx, 9.5f, cz, SurfaceType.Fairway, 80f, 80f);

            yield return null;

            var ground   = new SceneGroundProvider();
            var surfaces = new SceneSurfaceProvider();
            var stats    = new StatsAccum(0);
            SeedRng(31415u);

            float gy = ground.SampleHeight(fp.FromFloat(cx), fp.FromFloat(cz), SurfaceType.Fairway).ToFloat();

            for (int i = 0; i < 1000; i++)
            {
                float ox = cx + NextF(-30f, 30f);
                float oz = cz + NextF(-30f, 30f);
                float speed = NextF(3f, 8f);   // keep within collider (low power)
                float yaw   = NextF(0f, Mathf.PI * 2f);
                float pitch = NextF(0.1f, 0.4f);

                var origin = new fp3(fp.FromFloat(ox), fp.FromFloat(gy + 0.021f), fp.FromFloat(oz));
                var input  = new ShotInput(origin, ShotVel(yaw, speed, pitch), fp.FromInt(20), SpinState.None);
                var traj   = BallSimulation.Simulate(input, ground, AeroConfig.Default, WindConfig.Calm,
                    surfaces, SurfaceConfig.Default, PuttConfig.Default, BallPhysicsModifiers.Neutral);

                stats.shots++;
                CollectStats(traj, SurfaceType.Fairway, ground, surfaces, ref stats);
            }

            TestContext.WriteLine(stats.Row("Fairway"));
            Assert.That(stats.fallThroughs, Is.Zero,
                $"T15: {stats.fallThroughs} fall-through frames in 1000 fairway shots. MinGap={stats.minGap:F4}m");
        }

        // ── Test 16 ─────────────────────────────────────────────────────────────
        // 500 rough shots. Rough surface slightly lower than surrounding Fairway/fringe.
        // Validates classification-aware sampling keeps ball on rough floor.

        [UnityTest]
        public IEnumerator T16_Rough500Shots_ZeroFallThrough()
        {
            float cx = 900f, cz = 500f;
            CreateFlat(cx, 9.6f, cz, SurfaceType.Fairway, 50f, 50f);
            CreateFlat(cx, 9.5f, cz, SurfaceType.Rough,   38f, 38f);

            yield return null;

            var ground   = new SceneGroundProvider();
            var surfaces = new SceneSurfaceProvider();
            var stats    = new StatsAccum(0);
            SeedRng(27182u);

            float gy = ground.SampleHeight(fp.FromFloat(cx), fp.FromFloat(cz), SurfaceType.Rough).ToFloat();

            for (int i = 0; i < 500; i++)
            {
                float ox = cx + NextF(-16f, 16f);
                float oz = cz + NextF(-16f, 16f);
                float speed = NextF(3f, 9f);
                float yaw   = NextF(0f, Mathf.PI * 2f);
                float pitch = NextF(0.15f, 0.45f);

                var origin = new fp3(fp.FromFloat(ox), fp.FromFloat(gy + 0.021f), fp.FromFloat(oz));
                var input  = new ShotInput(origin, ShotVel(yaw, speed, pitch), fp.FromInt(20), SpinState.None);
                var traj   = BallSimulation.Simulate(input, ground, AeroConfig.Default, WindConfig.Calm,
                    surfaces, SurfaceConfig.Default, PuttConfig.Default, BallPhysicsModifiers.Neutral);

                stats.shots++;
                CollectStats(traj, SurfaceType.Rough, ground, surfaces, ref stats);
            }

            TestContext.WriteLine(stats.Row("Rough"));
            Assert.That(stats.fallThroughs, Is.Zero,
                $"T16: {stats.fallThroughs} fall-through frames in 500 rough shots. MinGap={stats.minGap:F4}m");
        }
    }
}
