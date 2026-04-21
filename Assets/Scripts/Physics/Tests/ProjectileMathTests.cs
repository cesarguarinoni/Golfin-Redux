using NUnit.Framework;
using Golfin.Physics;
using Golfin.Physics.Math;

namespace Golfin.Physics.Tests
{
    public class ProjectileMathTests
    {
        private static readonly double g = 9.80665;

        /// <summary>
        /// Classic range equation: R = v² * sin(2θ) / g, for launch from y=0
        /// to y=0 on flat ground.
        /// </summary>
        private static double AnalyticalRange(double speed, double angleRad)
        {
            return speed * speed * System.Math.Sin(2.0 * angleRad) / g;
        }

        [Test]
        public void Simulate_Gravity_Only_MatchesAnalyticalRange_Within_1Percent()
        {
            var rng = new System.Random(12345);
            int failures = 0;
            double worstErrorPct = 0;
            int checkedCount = 0;

            for (int i = 0; i < 1000; i++)
            {
                double speed = 10.0 + rng.NextDouble() * 70.0;
                double angleDeg = 5.0 + rng.NextDouble() * 75.0;
                double angleRad = angleDeg * System.Math.PI / 180.0;

                double vz = speed * System.Math.Cos(angleRad);
                double vy = speed * System.Math.Sin(angleRad);

                var input = new ShotInput(
                    origin: new fp3(fp.Zero, fp.Zero, fp.Zero),
                    velocity: new fp3(fp.Zero, fp.FromDouble(vy), fp.FromDouble(vz)),
                    maxDuration: fp.FromInt(30));

                var ground = new FlatGround(fp.Zero);
                var traj = BallSimulation.Simulate(input, ground);

                Assert.AreEqual(TerminationReason.HitGround, traj.termination,
                    $"Shot {i}: expected HitGround, got {traj.termination} " +
                    $"(speed={speed:F2}, angle={angleDeg:F2}°)");

                double simulatedRange = traj.finalPosition.z.ToDouble();
                double expectedRange = AnalyticalRange(speed, angleRad);
                double errorPct = System.Math.Abs(simulatedRange - expectedRange)
                                  / expectedRange * 100.0;

                if (errorPct > worstErrorPct) worstErrorPct = errorPct;
                if (errorPct > 1.0) failures++;
                checkedCount++;
            }

            UnityEngine.Debug.Log($"[ProjectileMathTests] 1000 random shots: " +
                                  $"failures (>1% error): {failures}, " +
                                  $"worst error: {worstErrorPct:F3}%");

            Assert.AreEqual(1000, checkedCount);
            Assert.AreEqual(0, failures,
                $"{failures} shots exceeded 1% range error. " +
                $"Worst error: {worstErrorPct:F3}%");
        }

        [Test]
        public void Simulate_ZeroVelocity_BallDropsAndHitsGround()
        {
            var input = new ShotInput(
                origin: new fp3(fp.Zero, fp.FromDouble(10), fp.Zero),
                velocity: fp3.Zero,
                maxDuration: fp.FromInt(30));
            var traj = BallSimulation.Simulate(input, new FlatGround(fp.Zero));

            Assert.AreEqual(TerminationReason.HitGround, traj.termination);
            // t = sqrt(2h/g) = sqrt(20/9.80665) ≈ 1.4285 s
            double expected = System.Math.Sqrt(20.0 / g);
            double actual = traj.finalTime.ToDouble();
            Assert.AreEqual(expected, actual, 0.01, $"Expected drop time {expected}, got {actual}");
        }

        [Test]
        public void Simulate_IsDeterministic_SameInputsSameBytes()
        {
            var input = new ShotInput(
                origin: fp3.Zero,
                velocity: new fp3(fp.Zero, fp.FromDouble(20), fp.FromDouble(30)),
                maxDuration: fp.FromInt(30));
            var ground = new FlatGround(fp.Zero);

            var a = BallSimulation.Simulate(input, ground);
            var b = BallSimulation.Simulate(input, ground);

            Assert.AreEqual(a.samples.Count, b.samples.Count);
            for (int i = 0; i < a.samples.Count; i++)
            {
                Assert.AreEqual(a.samples[i].position.x.raw, b.samples[i].position.x.raw, $"sample {i} x mismatch");
                Assert.AreEqual(a.samples[i].position.y.raw, b.samples[i].position.y.raw, $"sample {i} y mismatch");
                Assert.AreEqual(a.samples[i].position.z.raw, b.samples[i].position.z.raw, $"sample {i} z mismatch");
                Assert.AreEqual(a.samples[i].velocity.x.raw, b.samples[i].velocity.x.raw, $"sample {i} vx mismatch");
                Assert.AreEqual(a.samples[i].velocity.y.raw, b.samples[i].velocity.y.raw, $"sample {i} vy mismatch");
                Assert.AreEqual(a.samples[i].velocity.z.raw, b.samples[i].velocity.z.raw, $"sample {i} vz mismatch");
            }
        }

        [Test]
        public void Simulate_SampleCount_IsReasonable()
        {
            // 45° launch at 30 m/s → ~4.3s flight, ~1030 samples at 240 Hz + init + hit
            var input = new ShotInput(
                origin: fp3.Zero,
                velocity: new fp3(fp.Zero, fp.FromDouble(21.213), fp.FromDouble(21.213)),
                maxDuration: fp.FromInt(30));
            var traj = BallSimulation.Simulate(input, new FlatGround(fp.Zero));

            Assert.AreEqual(TerminationReason.HitGround, traj.termination);
            Assert.GreaterOrEqual(traj.samples.Count, 1000);
            Assert.LessOrEqual(traj.samples.Count, 1100);
        }
    }
}
