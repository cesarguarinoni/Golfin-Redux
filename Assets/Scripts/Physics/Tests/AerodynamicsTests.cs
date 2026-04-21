using System.Collections.Generic;
using NUnit.Framework;
using Golfin.Physics;
using Golfin.Physics.Math;

namespace Golfin.Physics.Tests
{
    public class AerodynamicsTests
    {
        // Club data matching Resources/Physics/clubs.csv (hardcoded so tests need no Resources.Load)
        private static readonly (string id, float speedMps, float angleDeg, float spinRpm, float expectedYd)[] Clubs =
        {
            ("Driver",        75.0f, 10.9f,  2686f, 275f),
            ("Iron3",         65.0f, 10.4f,  4404f, 212f),
            ("Iron5",         57.0f, 14.1f,  5280f, 194f),
            ("Iron7",         52.5f, 16.3f,  7097f, 172f),
            ("Iron9",         48.5f, 20.0f,  8647f, 152f),
            ("PitchingWedge", 46.0f, 24.0f,  9300f, 136f),
            ("SandWedge",     40.0f, 28.0f, 10000f, 110f),
        };

        private static ShotInput MakeShot(float speedMps, float angleDeg, float spinRpm = 0f)
        {
            double ar = angleDeg * System.Math.PI / 180.0;
            var vel = new fp3(
                fp.Zero,
                fp.FromDouble(speedMps * System.Math.Sin(ar)),
                fp.FromDouble(speedMps * System.Math.Cos(ar)));
            if (spinRpm <= 0f)
                return new ShotInput(fp3.Zero, vel, fp.FromInt(30));
            float rps = spinRpm * 2f * (float)System.Math.PI / 60f;
            var spin = new SpinState(new fp3(fp.FromInt(-1), fp.Zero, fp.Zero), fp.FromFloat(rps));
            return new ShotInput(fp3.Zero, vel, fp.FromInt(30), spin);
        }

        private static float CarryMeters(Trajectory t) => t.finalPosition.z.ToFloat();
        private static float CarryYards(Trajectory t)  => CarryMeters(t) * 1.09361f;

        // ── Test 1 ────────────────────────────────────────────────────────────
        [Test]
        public void Aero_Off_MatchesPhase1_Within_Epsilon()
        {
            // With Cd=0 and Cl=0, the integrator must match the vacuum (gravity-only) path.
            var vacuum = AeroConfig.Vacuum;  // Cd=0, Cl=0
            var noAero = new AeroConfig
            {
                AirDensity          = AeroConfig.Default.AirDensity,
                BallMass            = AeroConfig.Default.BallMass,
                BallCrossSection    = AeroConfig.Default.BallCrossSection,
                DragCoefficient     = fp.Zero,
                LiftCoefficientBase = fp.Zero,
                SpinRateReference   = AeroConfig.Default.SpinRateReference,
                LiftMaxMultiplier   = AeroConfig.Default.LiftMaxMultiplier,
            };

            var input = MakeShot(52.5f, 16.3f);
            var ground = new FlatGround(fp.Zero);

            var phase1 = BallSimulation.Simulate(input, ground);              // vacuum overload
            var phase2 = BallSimulation.Simulate(input, ground, noAero);      // explicit Cd=Cl=0

            float diff = System.Math.Abs(CarryMeters(phase1) - CarryMeters(phase2));
            Assert.Less(diff, 0.1f,
                $"Carry diff with Cd=Cl=0: {diff:F3}m (expected < 0.1m). " +
                $"phase1={CarryMeters(phase1):F2}m  phase2={CarryMeters(phase2):F2}m");
        }

        // ── Test 2 ────────────────────────────────────────────────────────────
        [Test]
        public void Aero_DragReducesCarry_MonotonicallyWithCd()
        {
            float prevCarry = float.MaxValue;
            var ground = new FlatGround(fp.Zero);

            for (float cd = 0f; cd <= 0.55f; cd += 0.05f)
            {
                var cfg = AeroConfig.Default;
                cfg.DragCoefficient = fp.FromFloat(cd);
                cfg.LiftCoefficientBase = fp.Zero;  // isolate drag

                var input = MakeShot(52.5f, 16.3f);
                float carry = CarryMeters(BallSimulation.Simulate(input, ground, cfg));

                Assert.LessOrEqual(carry, prevCarry + 0.01f,    // small tolerance for fixed-point rounding
                    $"Carry increased from {prevCarry:F2}m to {carry:F2}m when Cd went from {cd - 0.05f:F2} to {cd:F2}");
                prevCarry = carry;
            }
        }

        // ── Test 3 ────────────────────────────────────────────────────────────
        [Test]
        public void Aero_Backspin_ExtendsCarry_VsZeroSpin()
        {
            var cfg    = AeroConfig.Default;
            var ground = new FlatGround(fp.Zero);

            var noSpin   = MakeShot(52.5f, 16.3f, 0f);
            var backspin = MakeShot(52.5f, 16.3f, 5000f);

            float carryNo   = CarryMeters(BallSimulation.Simulate(noSpin,   ground, cfg));
            float carrySpin = CarryMeters(BallSimulation.Simulate(backspin, ground, cfg));

            float improvement = (carrySpin - carryNo) / carryNo;
            Assert.Greater(improvement, 0.10f,
                $"Backspin shot should carry ≥10% farther than no-spin. " +
                $"no-spin={carryNo:F1}m  backspin={carrySpin:F1}m  improvement={improvement * 100:F1}%");
        }

        // ── Test 4 ────────────────────────────────────────────────────────────
        [Test]
        public void Aero_ClubCarries_WithinTolerance_OfTrackmanTargets()
        {
            var cfg    = AeroConfig.Default;
            var ground = new FlatGround(fp.Zero);
            var results = new List<string>();
            bool anyFailed = false;

            foreach (var (id, speedMps, angleDeg, spinRpm, expectedYd) in Clubs)
            {
                var input = MakeShot(speedMps, angleDeg, spinRpm);
                float actualYd = CarryYards(BallSimulation.Simulate(input, ground, cfg));
                float errPct   = System.Math.Abs(actualYd - expectedYd) / expectedYd * 100f;
                bool  ok       = errPct <= 10f;

                results.Add($"  {id,-15} expected={expectedYd:F0}yd  actual={actualYd:F0}yd  err={errPct:F1}%  {(ok ? "OK" : "FAIL")}");
                if (!ok) anyFailed = true;
            }

            string table = "\n" + string.Join("\n", results);
            UnityEngine.Debug.Log("[AerodynamicsTests] Club carry table:" + table);

            Assert.IsFalse(anyFailed,
                "One or more clubs exceed 10% carry error vs Trackman targets. " +
                "Do NOT silently adjust expected_carry_yd — tune Cd/Cl instead." + table);
        }
    }
}
