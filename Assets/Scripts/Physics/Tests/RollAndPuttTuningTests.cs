using NUnit.Framework;
using Golfin.Physics;
using Golfin.Physics.Math;
using Golfin.Physics.Runtime;

namespace Golfin.Physics.Tests
{
    /// <summary>
    /// Phase A validation tests for `controls_c_fix`. Five tests:
    ///   1. Stimpmeter — Green k=0.50 produces real-golf Stimp 12 distance.
    ///   2. LongPutt   — 5 m/s putter on Green→Fairway transition stays under 45m total.
    ///   3. DriverFairwayRollOut — observation-only, logs roll-out distance for Cesar.
    ///   4. CartPathStop — driver landing on CartPath terminates as BallStopped (was max-bounces).
    ///   5. StopCheckCorrectness — both Roll and Putt phases terminate well under their step caps.
    ///
    /// All five load tuning from CSV (PhysicsConfigLoader.LoadSurfaceConfig/LoadPuttConfig),
    /// so they pick up the Step 3 + Step 4 CSV edits. Tests using SurfaceConfig.Default /
    /// PuttConfig.Default are unaffected (existing 198 tests stay bit-exact).
    /// </summary>
    public class RollAndPuttTuningTests
    {
        // ── Helpers ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// Driver-class input: 64 m/s (post-cap value captured from C diagnosis Shot 2),
        /// 11° launch angle, 2700 rpm backspin along -X axis.
        /// </summary>
        private static ShotInput DriverInput()
        {
            float angleRad = 11.0f * (float)System.Math.PI / 180f;
            float speed    = 64.0f;
            var   origin   = fp3.Zero;
            var   velocity = new fp3(fp.Zero,
                fp.FromDouble(speed * System.Math.Sin(angleRad)),
                fp.FromDouble(speed * System.Math.Cos(angleRad)));
            float rps  = 2700f * 2f * (float)System.Math.PI / 60f;
            var   spin = new SpinState(new fp3(fp.FromInt(-1), fp.Zero, fp.Zero), fp.FromFloat(rps));
            return new ShotInput(origin, velocity, fp.FromInt(30), spin);
        }

        /// <summary>
        /// Surface provider: Green for x &lt; 5, Fairway for x >= 5.
        /// Mirrors SplitSurfaceProvider from PuttTests.cs.
        /// </summary>
        private class SplitSurfaceProvider : ISurfaceProvider
        {
            public SurfaceType Classify(fp worldX, fp worldZ)
                => worldX < fp.FromInt(5) ? SurfaceType.Green : SurfaceType.Fairway;
        }

        // ── Test 1: Stimpmeter — Green k=0.50 rolls to Stimp 12 distance ─────────

        /// <summary>
        /// USGA Stimpmeter releases at 1.829 m/s (6.00 ft/s); fast (Stimp 12) green roll = 3.66 m.
        /// With k=0.50 (CSV-loaded), d_max = v0/k = 1.829/0.50 = 3.66 m.
        /// Ball arrests ~0.08 m short of asymptote at stopSpeed=0.04, so ~3.58 m expected.
        /// Band [3.0, 4.5] covers Stimp 10–12 range.
        /// </summary>
        [Test]
        public void Stimpmeter_Green_RollsTo3to4Meters()
        {
            var puttCfg = PhysicsConfigLoader.LoadPuttConfig();
            var surfCfg = PhysicsConfigLoader.LoadSurfaceConfig();

            var input = new ShotInput(
                fp3.Zero,
                new fp3(fp.FromFloat(1.83f), fp.Zero, fp.Zero),
                fp.FromInt(60));

            var traj = BallSimulation.Simulate(
                input, new FlatGround(fp.Zero), AeroConfig.Vacuum, WindConfig.Calm,
                new ConstantSurfaceProvider(SurfaceType.Green), surfCfg, puttCfg);

            float dist = traj.finalPosition.x.ToFloat();
            TestContext.WriteLine($"[Stimpmeter] finalX={dist:F3}m (expected ~3.58m; band [3.0, 4.5])");

            Assert.AreEqual(TerminationReason.BallStopped, traj.termination,
                $"Must terminate as BallStopped; got {traj.termination}");
            Assert.GreaterOrEqual(dist, 3.0f, $"Roll distance must be >= 3.0m (Stimp 10 floor); got {dist:F3}m");
            Assert.LessOrEqual(dist,   4.5f, $"Roll distance must be <= 4.5m (Stimp 12 ceiling); got {dist:F3}m");
        }

        // ── Test 2: Long putt — Green→Fairway transition stays under 45 m ─────────

        /// <summary>
        /// Full-power 5 m/s putter starting on Green, crossing to Fairway at x=5.
        /// Confirms the C.2 "rolls forever" regression is fixed (ball terminates).
        /// Band [8.0, 45.0]: lower bound catches a broken sim; upper bound catches infinite roll.
        /// </summary>
        [Test]
        public void LongPutt_GreenToFairwayTransition_TotalRollUnder45m()
        {
            var puttCfg = PhysicsConfigLoader.LoadPuttConfig();
            var surfCfg = PhysicsConfigLoader.LoadSurfaceConfig();

            var input = new ShotInput(
                fp3.Zero,
                new fp3(fp.FromFloat(5.0f), fp.Zero, fp.Zero),
                fp.FromInt(60));

            var traj = BallSimulation.Simulate(
                input, new FlatGround(fp.Zero), AeroConfig.Vacuum, WindConfig.Calm,
                new SplitSurfaceProvider(), surfCfg, puttCfg);

            float dist = traj.finalPosition.x.ToFloat();
            TestContext.WriteLine($"[LongPutt] finalX={dist:F3}m (band [8.0, 45.0])");

            Assert.AreEqual(TerminationReason.BallStopped, traj.termination,
                $"Must terminate as BallStopped; got {traj.termination}");
            Assert.GreaterOrEqual(dist, 8.0f,  $"Roll distance must be >= 8m; got {dist:F3}m");
            Assert.LessOrEqual(dist,   45.0f, $"Roll distance must be <= 45m; got {dist:F3}m");
        }

        // ── Test 3: Driver on Fairway — observation only ──────────────────────────

        /// <summary>
        /// Driver at 64 m/s on flat Fairway. Observation-only: logs total distance and
        /// post-first-bounce roll-out for Cesar to use in Phase B Fairway tuning.
        /// Assertions are loose: must terminate + stay in [100, 400] m band.
        /// </summary>
        [Test]
        public void DriverFairwayRollOut_ObservationOnly_TerminatesAndLogs()
        {
            var surfCfg = PhysicsConfigLoader.LoadSurfaceConfig();
            var puttCfg = PhysicsConfigLoader.LoadPuttConfig();
            var input   = DriverInput();

            var traj = BallSimulation.Simulate(
                input, new FlatGround(fp.Zero), AeroConfig.Default, WindConfig.Calm,
                new ConstantSurfaceProvider(SurfaceType.Fairway), surfCfg, puttCfg);

            float totalDist = traj.finalPosition.z.ToFloat(); // driver fires along +Z

            // Find first non-stop terrain hit to compute post-bounce roll-out distance.
            float rollOutDist = 0f;
            if (traj.terrainHits.Count == 0)
            {
                TestContext.WriteLine("[DriverFairway] no bounces (airborne all the way to OOB or world bound)");
            }
            else
            {
                float firstBounceZ = -1f;
                foreach (var hit in traj.terrainHits)
                {
                    if (!hit.IsStop)
                    {
                        firstBounceZ = hit.Position.z.ToFloat();
                        break;
                    }
                }
                if (firstBounceZ >= 0f)
                    rollOutDist = totalDist - firstBounceZ;

                TestContext.WriteLine($"[DriverFairway] totalDistZ={totalDist:F1}m, firstBounceZ={firstBounceZ:F1}m, rollOut={rollOutDist:F1}m");
            }

            TestContext.WriteLine($"[DriverFairway] termination={traj.termination}, samples={traj.samples.Count}");

            Assert.AreEqual(TerminationReason.BallStopped, traj.termination,
                $"Must terminate as BallStopped; got {traj.termination}");

            // Catch catastrophic regression: too short means carry/aero broke; too long means infinite roll.
            float horizDist = System.Math.Abs(totalDist);
            Assert.GreaterOrEqual(horizDist, 100.0f,
                $"Total distance must be >= 100m (carry + roll); got {horizDist:F1}m");
            Assert.LessOrEqual(horizDist, 400.0f,
                $"Total distance must be <= 400m (catastrophic roll-out guard); got {horizDist:F1}m");
        }

        // ── Test 4: Driver on CartPath — must terminate as BallStopped ────────────

        /// <summary>
        /// Driver at 64 m/s landing on flat CartPath (k=0.30 after CSV fix).
        /// Before fix: CartPath k=0.06 produced 100m+ roll and never terminated (C.2).
        /// After fix: must terminate as BallStopped within reasonable step count.
        /// </summary>
        [Test]
        public void CartPathStop_DriverLanding_TerminatesAsBallStopped()
        {
            var surfCfg = PhysicsConfigLoader.LoadSurfaceConfig();
            var puttCfg = PhysicsConfigLoader.LoadPuttConfig();
            var input   = DriverInput();

            var traj = BallSimulation.Simulate(
                input, new FlatGround(fp.Zero), AeroConfig.Default, WindConfig.Calm,
                new ConstantSurfaceProvider(SurfaceType.CartPath), surfCfg, puttCfg);

            float totalDist = traj.finalPosition.z.ToFloat();
            int   stepCount = traj.samples.Count;

            TestContext.WriteLine($"[CartPathStop] termination={traj.termination}, samples={stepCount}, finalZ={totalDist:F1}m");

            Assert.AreEqual(TerminationReason.BallStopped, traj.termination,
                $"Must terminate as BallStopped; got {traj.termination} (C.2 regression if MaxBounces)");
            Assert.Less(stepCount, 60 * 240,
                $"Sample count {stepCount} must be < {60 * 240} (60-second cap); fix may not have closed C.2");
        }

        // ── Test 5: Stop-check correctness — both phases terminate well under cap ──

        /// <summary>
        /// Structural test for the stop-check fix. Before the fix, the stop-counter
        /// never incremented past 0 on flat ground due to fp16.16 rounding (C.2 evidence).
        /// After the fix, both Putt and Roll phases must terminate well under their caps.
        ///
        /// (a) Putt: 2.0 m/s on Green — k=0.50, decays to stopSpeed=0.04 in ~1900 steps.
        /// (b) Roll: driver on Fairway — must stop well before the 14400-step roll cap.
        /// </summary>
        [Test]
        public void StopCheckCorrectness_BothPhasesTerminateWellUnderCap()
        {
            var surfCfg = PhysicsConfigLoader.LoadSurfaceConfig();
            var puttCfg = PhysicsConfigLoader.LoadPuttConfig();

            // (a) Putt sub-test: 2.0 m/s putt on Green (IsPutt path).
            {
                var puttInput = new ShotInput(
                    fp3.Zero,
                    new fp3(fp.FromFloat(2.0f), fp.Zero, fp.Zero),
                    fp.FromInt(60));

                var traj = BallSimulation.Simulate(
                    puttInput, new FlatGround(fp.Zero), AeroConfig.Vacuum, WindConfig.Calm,
                    new ConstantSurfaceProvider(SurfaceType.Green), surfCfg, puttCfg);

                int puttSteps = traj.samples.Count;
                TestContext.WriteLine($"[StopCheck Putt] termination={traj.termination}, steps={puttSteps} (cap=10000)");

                Assert.AreEqual(TerminationReason.BallStopped, traj.termination,
                    $"Putt must terminate as BallStopped; got {traj.termination}");
                Assert.Less(puttSteps, 10000,
                    $"Putt step count {puttSteps} must be < 10000; stop-check fix may have a bug");
            }

            // (b) Roll sub-test: driver on Fairway (Roll phase).
            {
                var driverInput = DriverInput();

                var traj = BallSimulation.Simulate(
                    driverInput, new FlatGround(fp.Zero), AeroConfig.Default, WindConfig.Calm,
                    new ConstantSurfaceProvider(SurfaceType.Fairway), surfCfg, puttCfg);

                int rollSteps = traj.samples.Count;
                TestContext.WriteLine($"[StopCheck Roll] termination={traj.termination}, steps={rollSteps} (cap=10000)");

                Assert.AreEqual(TerminationReason.BallStopped, traj.termination,
                    $"Roll must terminate as BallStopped; got {traj.termination}");
                Assert.Less(rollSteps, 10000,
                    $"Roll step count {rollSteps} must be < 10000; stop-check fix may have a bug");
            }
        }
    }
}
