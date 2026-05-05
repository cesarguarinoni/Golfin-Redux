using NUnit.Framework;
using Golfin.Physics;
using Golfin.Physics.Math;

namespace Golfin.Physics.Tests
{
    public class WindTests
    {
        // Iron7 shot flying +Z: 52.5 m/s, 16.3°, 7097 rpm backspin
        private static ShotInput MakeIron7()
        {
            double ar = 16.3 * System.Math.PI / 180.0;
            var vel = new fp3(
                fp.Zero,
                fp.FromDouble(52.5 * System.Math.Sin(ar)),
                fp.FromDouble(52.5 * System.Math.Cos(ar)));
            float rps = 7097f * 2f * (float)System.Math.PI / 60f;
            var spin = new SpinState(new fp3(fp.FromInt(-1), fp.Zero, fp.Zero), fp.FromFloat(rps));
            return new ShotInput(fp3.Zero, vel, fp.FromInt(30), spin);
        }

        // LUT-mode aero config mirroring current aero.csv/LUT files.
        // Must stay in sync with Resources/Physics/aero*.csv.
        private static AeroConfig MakeLutAero()
        {
            var dragX = new fp[] {
                fp.FromFloat(5f),  fp.FromFloat(10f), fp.FromFloat(15f), fp.FromFloat(18f),
                fp.FromFloat(22f), fp.FromFloat(26f), fp.FromFloat(30f), fp.FromFloat(40f),
                fp.FromFloat(50f), fp.FromFloat(60f), fp.FromFloat(70f), fp.FromFloat(80f), fp.FromFloat(100f),
            };
            var dragY = new fp[] {
                fp.FromFloat(0.50f), fp.FromFloat(0.48f), fp.FromFloat(0.45f), fp.FromFloat(0.40f),
                fp.FromFloat(0.28f), fp.FromFloat(0.24f), fp.FromFloat(0.23f), fp.FromFloat(0.23f),
                fp.FromFloat(0.23f), fp.FromFloat(0.23f), fp.FromFloat(0.23f), fp.FromFloat(0.23f), fp.FromFloat(0.23f),
            };
            var liftX = new fp[] {
                fp.FromFloat(0.00f), fp.FromFloat(0.02f), fp.FromFloat(0.05f), fp.FromFloat(0.08f),
                fp.FromFloat(0.10f), fp.FromFloat(0.12f), fp.FromFloat(0.15f), fp.FromFloat(0.20f),
                fp.FromFloat(0.25f), fp.FromFloat(0.30f), fp.FromFloat(0.40f), fp.FromFloat(0.50f), fp.FromFloat(0.60f),
            };
            var liftY = new fp[] {
                fp.FromFloat(0.000f), fp.FromFloat(0.034f), fp.FromFloat(0.066f), fp.FromFloat(0.093f),
                fp.FromFloat(0.110f), fp.FromFloat(0.125f), fp.FromFloat(0.146f), fp.FromFloat(0.177f),
                fp.FromFloat(0.202f), fp.FromFloat(0.224f), fp.FromFloat(0.260f), fp.FromFloat(0.288f), fp.FromFloat(0.300f),
            };
            var cfg = AeroConfig.Default;
            cfg.DragLut    = new CoefficientLut(dragX, dragY);
            cfg.LiftLut    = new CoefficientLut(liftX, liftY);
            cfg.UseDragLut = true;
            cfg.UseLiftLut = true;
            return cfg;
        }

        private static float CarryMeters(Trajectory t) => t.finalPosition.z.ToFloat();
        private static float CarryYards(Trajectory t)  => CarryMeters(t) * 1.09361f;
        private static float LateralMeters(Trajectory t) => t.finalPosition.x.ToFloat();

        // ── Test 1 ─────────────────────────────────────────────────────────────
        [Test]
        public void Wind_Calm_MatchesPhase2Aero_ExactlyEqual()
        {
            var input  = MakeIron7();
            var ground = new FlatGround(fp.Zero);
            var aero   = MakeLutAero();

            // Wind-free overload (Phase 2) vs wind-aware overload with Calm.
            // Both must forward to the identical integration path — bit-exact.
            var trajA = BallSimulation.Simulate(input, ground, aero);
            var trajB = BallSimulation.Simulate(input, ground, aero, WindConfig.Calm);

            Assert.AreEqual(trajA.finalPosition.x.raw, trajB.finalPosition.x.raw,
                "Wind_Calm: x differs — wind threading altered the wind-free path");
            Assert.AreEqual(trajA.finalPosition.y.raw, trajB.finalPosition.y.raw,
                "Wind_Calm: y differs — wind threading altered the wind-free path");
            Assert.AreEqual(trajA.finalPosition.z.raw, trajB.finalPosition.z.raw,
                "Wind_Calm: z differs — wind threading altered the wind-free path");
        }

        // ── Test 2 ─────────────────────────────────────────────────────────────
        [Test]
        public void Wind_Headwind_ReducesCarry_MonotonicallyWithSpeed()
        {
            var input  = MakeIron7();
            var ground = new FlatGround(fp.Zero);
            var aero   = MakeLutAero();

            // Ball flies +Z; headwind is -Z (opposing).
            WindConfig Hw(float mps) => new WindConfig
            {
                BaseVelocity      = new fp3(fp.Zero, fp.Zero, fp.FromFloat(-mps)),
                GustAmplitude     = fp.Zero, GustFrequency = fp.Zero,
                AltitudeFactor    = fp.Zero, AltitudeRefMeters = fp.FromInt(10), Seed = 0,
            };

            float yA = CarryYards(BallSimulation.Simulate(input, ground, aero, WindConfig.Calm));
            float yB = CarryYards(BallSimulation.Simulate(input, ground, aero, Hw(5f)));
            float yC = CarryYards(BallSimulation.Simulate(input, ground, aero, Hw(10f)));

            Assert.Greater(yA, yB, "5 m/s headwind should reduce carry vs calm");
            Assert.Greater(yB, yC, "10 m/s headwind should reduce carry more than 5 m/s");
            Assert.Greater(yA - yB, 3f, "5 m/s headwind gap must be > 3 yd (real effect, not noise)");
            Assert.Greater(yB - yC, 3f, "5→10 m/s headwind gap must be > 3 yd");
        }

        // ── Test 3 ─────────────────────────────────────────────────────────────
        [Test]
        public void Wind_Tailwind_ExtendsCarry()
        {
            var input  = MakeIron7();
            var ground = new FlatGround(fp.Zero);
            var aero   = MakeLutAero();

            // Ball flies +Z; tailwind is +Z (assisting).
            WindConfig Tw(float mps) => new WindConfig
            {
                BaseVelocity      = new fp3(fp.Zero, fp.Zero, fp.FromFloat(mps)),
                GustAmplitude     = fp.Zero, GustFrequency = fp.Zero,
                AltitudeFactor    = fp.Zero, AltitudeRefMeters = fp.FromInt(10), Seed = 0,
            };

            float calm     = CarryYards(BallSimulation.Simulate(input, ground, aero, WindConfig.Calm));
            float tailwind = CarryYards(BallSimulation.Simulate(input, ground, aero, Tw(5f)));

            Assert.Greater(tailwind, calm, "5 m/s tailwind should extend carry vs calm");
            Assert.Greater(tailwind - calm, 3f, "Tailwind carry gain must be > 3 yd (real effect, not noise)");
        }

        // ── Test 4 ─────────────────────────────────────────────────────────────
        [Test]
        public void Wind_Crosswind_ProducesLateralDrift()
        {
            var input  = MakeIron7();
            var ground = new FlatGround(fp.Zero);
            var aero   = MakeLutAero();

            // Ball flies +Z; crosswind = +X (east).
            var cross = new WindConfig
            {
                BaseVelocity      = new fp3(fp.FromInt(5), fp.Zero, fp.Zero),
                GustAmplitude     = fp.Zero, GustFrequency = fp.Zero,
                AltitudeFactor    = fp.Zero, AltitudeRefMeters = fp.FromInt(10), Seed = 0,
            };

            var calmTraj  = BallSimulation.Simulate(input, ground, aero, WindConfig.Calm);
            var crossTraj = BallSimulation.Simulate(input, ground, aero, cross);

            float lateralM = LateralMeters(crossTraj);
            Assert.Greater(lateralM, 2f, "5 m/s crosswind should drift ball > 2m east");

            float calmCarryYd  = CarryYards(calmTraj);
            float crossCarryYd = CarryYards(crossTraj);
            float carryDiffPct = System.Math.Abs(crossCarryYd - calmCarryYd) / calmCarryYd * 100f;
            Assert.Less(carryDiffPct, 3f, "Crosswind should not change downrange carry by more than 3%");
        }

        // ── Test 5 ─────────────────────────────────────────────────────────────
        [Test]
        public void Wind_Gust_SeedDeterminism()
        {
            var input  = MakeIron7();
            var ground = new FlatGround(fp.Zero);
            var aero   = MakeLutAero();

            WindConfig Gusty(uint seed) => new WindConfig
            {
                BaseVelocity      = new fp3(fp.Zero, fp.Zero, fp.FromFloat(-5f)),
                GustAmplitude     = fp.FromFloat(0.3f),
                GustFrequency     = fp.FromFloat(0.5f),
                AltitudeFactor    = fp.Zero, AltitudeRefMeters = fp.FromInt(10),
                Seed = seed,
            };

            // Same seed → identical trajectories.
            var t1a = BallSimulation.Simulate(input, ground, aero, Gusty(42));
            var t1b = BallSimulation.Simulate(input, ground, aero, Gusty(42));
            Assert.AreEqual(t1a.finalPosition.x.raw, t1b.finalPosition.x.raw, "Same seed: x must be bit-exact");
            Assert.AreEqual(t1a.finalPosition.z.raw, t1b.finalPosition.z.raw, "Same seed: z must be bit-exact");

            // Different seeds → different trajectories (at least 0.1m apart).
            // NOTE (controls_d_velocity_cap_diagnosis 2026-05-05): threshold re-snapshotted from 0.5m to 0.1m
            // after fpMath.Sqrt fix. With corrected |v| in gust-wind integration the per-step velocity
            // perturbation is smaller relative to shot energy, so seed 42 vs 99 now differ by ~0.19m
            // instead of the previous ~0.5m+. The test still validates that seed variation produces
            // measurably different trajectories; the magnitude of difference shifted with correct physics.
            var t2 = BallSimulation.Simulate(input, ground, aero, Gusty(99));
            float dx = System.Math.Abs(t1a.finalPosition.x.ToFloat() - t2.finalPosition.x.ToFloat());
            float dz = System.Math.Abs(t1a.finalPosition.z.ToFloat() - t2.finalPosition.z.ToFloat());
            float dist = System.Math.Max(dx, dz);
            Assert.Greater(dist, 0.1f, "Different seeds should produce landing positions differing by > 0.1m");
        }

        // ── Test 6 ─────────────────────────────────────────────────────────────
        [Test]
        public void Wind_Altitude_ProfileAffectsApex()
        {
            var input  = MakeIron7();
            var ground = new FlatGround(fp.Zero);
            var aero   = MakeLutAero();

            // Headwind with flat profile (no altitude factor).
            var flatWind = new WindConfig
            {
                BaseVelocity      = new fp3(fp.Zero, fp.Zero, fp.FromFloat(-5f)),
                GustAmplitude     = fp.Zero, GustFrequency = fp.Zero,
                AltitudeFactor    = fp.Zero, AltitudeRefMeters = fp.FromInt(10), Seed = 0,
            };

            // Same headwind, but wind strengthens with altitude (AltitudeFactor = 0.5).
            // At apex (~25m), altScale = 1 + 0.5 * (25/10) = 2.25 → wind is 2.25× stronger near apex.
            var altWind = new WindConfig
            {
                BaseVelocity      = new fp3(fp.Zero, fp.Zero, fp.FromFloat(-5f)),
                GustAmplitude     = fp.Zero, GustFrequency = fp.Zero,
                AltitudeFactor    = fp.FromFloat(0.5f),
                AltitudeRefMeters = fp.FromInt(10), Seed = 0,
            };

            float flatCarry = CarryYards(BallSimulation.Simulate(input, ground, aero, flatWind));
            float altCarry  = CarryYards(BallSimulation.Simulate(input, ground, aero, altWind));

            Assert.Less(altCarry, flatCarry, "Altitude-boosted headwind should reduce carry more than flat headwind");
            Assert.Greater(flatCarry - altCarry, 1f, "Altitude profile carry difference must be > 1 yd");
        }
    }
}
