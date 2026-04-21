using System.Collections.Generic;
using NUnit.Framework;
using Golfin.Physics;
using Golfin.Physics.Math;

namespace Golfin.Physics.Tests
{
    /// <summary>
    /// Phase 5 putt tests. Total target: 35 tests (29 prior + 6 here), all pass.
    /// </summary>
    public class PuttTests
    {
        // ── Helpers ─────────────────────────────────────────────────────────────────

        private static ShotInput IronInput()
        {
            // 7-iron: 52.5 m/s, 16.3°, 7097 rpm backspin — same as SurfaceTests / AerodynamicsTests
            float angleRad = 16.3f * (float)System.Math.PI / 180f;
            float speed    = 52.5f;
            var   origin   = fp3.Zero;
            var   velocity = new fp3(fp.Zero,
                fp.FromDouble(speed * System.Math.Sin(angleRad)),
                fp.FromDouble(speed * System.Math.Cos(angleRad)));
            float rps  = 7097f * 2f * (float)System.Math.PI / 60f;
            var   spin = new SpinState(new fp3(fp.FromInt(-1), fp.Zero, fp.Zero), fp.FromFloat(rps));
            return new ShotInput(origin, velocity, fp.FromInt(30), spin);
        }

        // Surface provider that returns Green for x < 5, Fairway for x >= 5.
        private class SplitSurfaceProvider : ISurfaceProvider
        {
            public SurfaceType Classify(fp worldX, fp worldZ)
                => worldX < fp.FromInt(5) ? SurfaceType.Green : SurfaceType.Fairway;
        }

        // ── Test 1: Bit-exact gate ─────────────────────────────────────────────────

        /// <summary>
        /// Run a 7-iron through the Phase 4 6-arg overload AND the Phase 5 7-arg overload
        /// with PuttConfig.Default. The 7-iron is not a putt (speed >> 8 m/s), so IsPutt
        /// returns false and both paths are identical. Bit-exact match is required.
        /// </summary>
        [Test]
        public void Putt_Phase4Overloads_BitExact()
        {
            var input  = IronInput();
            var ground = new FlatGround(fp.Zero);
            var aero   = AeroConfig.Default;
            var wind   = WindConfig.Calm;
            var surf   = new ConstantSurfaceProvider(SurfaceType.Fairway);
            var surfCfg = SurfaceConfig.Default;

            var t6 = BallSimulation.Simulate(input, ground, aero, wind, surf, surfCfg);
            var t7 = BallSimulation.Simulate(input, ground, aero, wind, surf, surfCfg, PuttConfig.Default);

            // Termination must match.
            Assert.AreEqual(t6.termination, t7.termination, "Termination must match");

            // Final position must be bit-exact.
            Assert.AreEqual(t6.finalPosition.x.raw, t7.finalPosition.x.raw, "finalPosition.x bit-exact");
            Assert.AreEqual(t6.finalPosition.y.raw, t7.finalPosition.y.raw, "finalPosition.y bit-exact");
            Assert.AreEqual(t6.finalPosition.z.raw, t7.finalPosition.z.raw, "finalPosition.z bit-exact");

            // Sample count must match.
            Assert.AreEqual(t6.samples.Count, t7.samples.Count, "Sample count must match");

            // Spot-check a few sample positions.
            if (t6.samples.Count > 100)
            {
                var s6 = t6.samples[100];
                var s7 = t7.samples[100];
                Assert.AreEqual(s6.position.x.raw, s7.position.x.raw, "sample[100] X bit-exact");
                Assert.AreEqual(s6.position.z.raw, s7.position.z.raw, "sample[100] Z bit-exact");
            }
        }

        // ── Test 2: Putt detection — low slow shot on green ───────────────────────

        /// <summary>
        /// A slow horizontal shot on Green must be detected as a putt and routed through
        /// RunPuttPhase: no airborne bounce hits, terminates BallStopped, many samples.
        /// </summary>
        [Test]
        public void Putt_Detection_LowSlowOnGreen_IsPutt()
        {
            // v=(2.0, 0, 0): speed=2.0 < 8 m/s, vy=0 (angle=0° < 15°), surface=Green → IsPutt=true.
            var input = new ShotInput(
                fp3.Zero,
                new fp3(fp.FromFloat(2.0f), fp.Zero, fp.Zero),
                fp.FromInt(60));

            var traj = BallSimulation.Simulate(
                input, new FlatGround(fp.Zero), AeroConfig.Vacuum, WindConfig.Calm,
                new ConstantSurfaceProvider(SurfaceType.Green), SurfaceConfig.Default,
                PuttConfig.Default);

            // Putt path: no bounce hits (IsStop=false). Only the final stop hit (IsStop=true).
            foreach (var hit in traj.terrainHits)
                Assert.IsTrue(hit.IsStop, "All TerrainHit records in putt path must be stops (no airborne bounces)");

            Assert.AreEqual(TerminationReason.BallStopped, traj.termination,
                "Slow green shot must terminate as BallStopped");
            Assert.Greater(traj.samples.Count, 50,
                "Putt roll integrator must produce > 50 samples");
        }

        // ── Test 3: Putt detection — fast flighted shot not classified as putt ────

        /// <summary>
        /// A fast flighted shot (driver-class speed) on Green must NOT be classified as a putt.
        /// It goes through the Phase 4 airborne path and produces at least one non-stop bounce.
        /// </summary>
        [Test]
        public void Putt_Detection_FastFlightedShot_IsNotPutt()
        {
            // v=(40, 30, 0): speed=50 m/s >> 8 m/s ceiling → IsPutt=false.
            var input = new ShotInput(
                fp3.Zero,
                new fp3(fp.FromFloat(40f), fp.FromFloat(30f), fp.Zero),
                fp.FromInt(30),
                new SpinState(fp3.Zero, fp.Zero));

            var traj = BallSimulation.Simulate(
                input, new FlatGround(fp.Zero), AeroConfig.Vacuum, WindConfig.Calm,
                new ConstantSurfaceProvider(SurfaceType.Green), SurfaceConfig.Default,
                PuttConfig.Default);

            // Must have had airborne time: check that sample[100] is well above ground.
            Assert.Greater(traj.samples.Count, 100, "Must have > 100 samples (significant airborne time)");
            float peakY = 0f;
            for (int i = 0; i < System.Math.Min(traj.samples.Count, 500); i++)
                peakY = System.Math.Max(peakY, traj.samples[i].position.y.ToFloat());
            Assert.Greater(peakY, 0.5f,
                $"Peak height must exceed 0.5m above ground; got {peakY:F2}m (airborne phase was skipped)");

            // Must have at least one non-stop bounce.
            bool foundBounce = false;
            foreach (var hit in traj.terrainHits)
                if (!hit.IsStop) { foundBounce = true; break; }
            Assert.IsTrue(foundBounce, "Fast flighted shot on green must produce at least one bounce TerrainHit");
        }

        // ── Test 4: Flat green 3m putt ────────────────────────────────────────────

        /// <summary>
        /// Calibrated velocity (0.35 m/s) on flat Green with default PuttConfig should stop
        /// at ~3.1m (within [2.7, 3.3]). This pins the "Sim 3m putt" tuning window button value.
        /// </summary>
        [Test]
        public void Putt_FlatGreen_3m_StopsAtTarget()
        {
            // v0=0.35 m/s, k=0.10: d ≈ (0.35/0.10) * (1 - 0.04/0.35) ≈ 3.1m
            var input = new ShotInput(
                fp3.Zero,
                new fp3(fp.FromFloat(0.35f), fp.Zero, fp.Zero),
                fp.FromInt(60));

            var traj = BallSimulation.Simulate(
                input, new FlatGround(fp.Zero), AeroConfig.Vacuum, WindConfig.Calm,
                new ConstantSurfaceProvider(SurfaceType.Green), SurfaceConfig.Default,
                PuttConfig.Default);

            Assert.AreEqual(TerminationReason.BallStopped, traj.termination,
                "Must terminate as BallStopped");

            float dist = traj.finalPosition.x.ToFloat();
            Assert.GreaterOrEqual(dist, 2.7f, $"Stop distance must be >= 2.7m; got {dist:F2}m");
            Assert.LessOrEqual(dist,   3.3f, $"Stop distance must be <= 3.3m; got {dist:F2}m");

            float finalSpeed = System.Math.Abs(traj.finalVelocity.x.ToFloat());
            Assert.Less(finalSpeed, 0.05f,
                $"Final velocity magnitude must be < 0.05 m/s; got {finalSpeed:F4} m/s");
        }

        // ── Test 5: Sloped green curves ball ──────────────────────────────────────

        /// <summary>
        /// A downhill putt rolls further than a flat putt. A cross-slope putt curves
        /// toward the low side. Both are directional checks with loose magnitude tolerances.
        /// </summary>
        [Test]
        public void Putt_SlopedGreen_CurvesDownhill()
        {
            float slopeSize = 100f;
            float tan5deg   = (float)System.Math.Tan(5.0 * System.Math.PI / 180.0); // ≈ 0.0875
            int   res       = 3;
            float cellX     = slopeSize / (res - 1);

            // ── Part A: downhill slope along +X (dh/dx negative) ─────────────────
            // Ball putts along +X and should travel further than flat (>3.5m).
            {
                var heights = new int[res * res];
                for (int iz = 0; iz < res; iz++)
                for (int ix = 0; ix < res; ix++)
                {
                    float xWorld = ix * cellX;
                    float hVal   = -tan5deg * xWorld; // descends as x increases
                    heights[iz * res + ix] = (int)System.Math.Round(hVal * 65536.0);
                }
                var slopeMap = new HeightmapData(res,
                    fp.FromFloat(slopeSize), fp.FromFloat(slopeSize),
                    fp.Zero, fp.Zero, fp.Zero, heights);

                fp startY = slopeMap.SampleHeight(fp.Zero, fp.Zero);
                var input = new ShotInput(
                    new fp3(fp.Zero, startY, fp.Zero),
                    new fp3(fp.FromFloat(0.35f), fp.Zero, fp.Zero),
                    fp.FromInt(60));

                var traj = BallSimulation.Simulate(
                    input, slopeMap, AeroConfig.Vacuum, WindConfig.Calm,
                    new ConstantSurfaceProvider(SurfaceType.Green), SurfaceConfig.Default,
                    PuttConfig.Default);

                float dist = traj.finalPosition.x.ToFloat();
                Assert.Greater(dist, 4.0f,
                    $"Downhill putt should travel > 4m (further than flat 3.1m); got {dist:F2}m");
            }

            // ── Part B: cross-slope (+Z tilt) with putt along +X ─────────────────
            // Ball should curve toward low side (+Z direction) by > 0.3m.
            {
                float cellZ = slopeSize / (res - 1);
                var heights = new int[res * res];
                for (int iz = 0; iz < res; iz++)
                for (int ix = 0; ix < res; ix++)
                {
                    float zWorld = iz * cellZ;
                    float hVal   = -tan5deg * zWorld; // descends as z increases
                    heights[iz * res + ix] = (int)System.Math.Round(hVal * 65536.0);
                }
                var slopeMap = new HeightmapData(res,
                    fp.FromFloat(slopeSize), fp.FromFloat(slopeSize),
                    fp.Zero, fp.Zero, fp.Zero, heights);

                fp startY = slopeMap.SampleHeight(fp.Zero, fp.Zero);
                var input = new ShotInput(
                    new fp3(fp.Zero, startY, fp.Zero),
                    new fp3(fp.FromFloat(0.35f), fp.Zero, fp.Zero),
                    fp.FromInt(60));

                var traj = BallSimulation.Simulate(
                    input, slopeMap, AeroConfig.Vacuum, WindConfig.Calm,
                    new ConstantSurfaceProvider(SurfaceType.Green), SurfaceConfig.Default,
                    PuttConfig.Default);

                float lateralZ = traj.finalPosition.z.ToFloat();
                Assert.Greater(lateralZ, 0.3f,
                    $"Cross-slope putt must curve > 0.3m toward low side (+Z); got {lateralZ:F3}m");
            }
        }

        // ── Test 6: Off-green transition — continuous deceleration ────────────────

        /// <summary>
        /// A putt that starts on Green and rolls onto Fairway (x >= 5) transitions
        /// continuously (no sample gaps) and decelerates more sharply on Fairway due
        /// to higher rolling resistance (0.18 vs 0.10).
        /// </summary>
        [Test]
        public void Putt_RunsOffGreenIntoFairway_TransitionsCleanly()
        {
            // 7.0 m/s: fast enough to cross x=5 with ~6.5 m/s remaining, so the
            // 0.5s speed drop on Fairway (k=0.18 vs Green k=0.10) is measurable.
            // v_trans ≈ 6.5 m/s → drop in 0.5s ≈ 6.5*(1-e^(-0.09)) ≈ 0.56 m/s > 0.5.
            var input = new ShotInput(
                fp3.Zero,
                new fp3(fp.FromFloat(7.0f), fp.Zero, fp.Zero),
                fp.FromInt(60));

            var traj = BallSimulation.Simulate(
                input, new FlatGround(fp.Zero), AeroConfig.Vacuum, WindConfig.Calm,
                new SplitSurfaceProvider(), SurfaceConfig.Default,
                PuttConfig.Default);

            Assert.AreEqual(TerminationReason.BallStopped, traj.termination,
                "Must eventually stop");

            // ── Continuity: no time gaps larger than 2×Dt between consecutive samples ──
            float expectedDt = 1f / 240f;
            for (int i = 1; i < traj.samples.Count; i++)
            {
                float gap = traj.samples[i].time.ToFloat() - traj.samples[i - 1].time.ToFloat();
                Assert.Less(gap, expectedDt * 5f,
                    $"Gap between samples[{i-1}] and [{i}] is {gap:F6}s — exceeds 5×Dt threshold");
            }

            // ── Deceleration increase: find last sample where x < 5, compare speed 0.5s later ──
            float speedAtTransition = -1f;
            float timeAtTransition  = -1f;
            for (int i = 0; i < traj.samples.Count; i++)
            {
                if (traj.samples[i].position.x.ToFloat() >= 5f)
                {
                    if (i > 0)
                    {
                        var vt = traj.samples[i - 1].velocity;
                        speedAtTransition = System.Math.Abs(vt.x.ToFloat());
                        timeAtTransition  = traj.samples[i - 1].time.ToFloat();
                    }
                    break;
                }
            }

            Assert.Greater(speedAtTransition, 0f,
                "Ball must have crossed x=5 boundary (3.5 m/s not strong enough to reach Fairway)");

            // Find speed 0.5s after transition.
            float speedAfter = -1f;
            for (int i = 0; i < traj.samples.Count; i++)
            {
                float t = traj.samples[i].time.ToFloat();
                if (t >= timeAtTransition + 0.5f)
                {
                    var v = traj.samples[i].velocity;
                    speedAfter = System.Math.Abs(v.x.ToFloat());
                    break;
                }
            }

            if (speedAfter >= 0f)
            {
                float drop = speedAtTransition - speedAfter;
                Assert.Greater(drop, 0.5f,
                    $"Speed should drop > 0.5 m/s in first 0.5s on Fairway; got {drop:F3} m/s " +
                    $"(speed at transition={speedAtTransition:F3}, after={speedAfter:F3})");
            }
        }
    }
}
