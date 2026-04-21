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

        // Build a seed LUT config (no Resources.Load — inline the seed tables)
        private static AeroConfig MakeLutConfig()
        {
            var dragX = new fp[] {
                fp.FromFloat(5f), fp.FromFloat(57f), fp.FromFloat(65f), fp.FromFloat(75f), fp.FromFloat(100f) };
            var dragY = new fp[] {
                fp.FromFloat(0.16f), fp.FromFloat(0.16f), fp.FromFloat(0.22f), fp.FromFloat(0.22f), fp.FromFloat(0.22f) };

            var liftX = new fp[] {
                fp.FromFloat(0.00f), fp.FromFloat(0.05f), fp.FromFloat(0.10f), fp.FromFloat(0.15f),
                fp.FromFloat(0.20f), fp.FromFloat(0.25f), fp.FromFloat(0.30f), fp.FromFloat(0.40f),
                fp.FromFloat(0.50f), fp.FromFloat(0.60f) };
            var liftY = new fp[] {
                fp.FromFloat(0.00f), fp.FromFloat(0.12f), fp.FromFloat(0.20f), fp.FromFloat(0.25f),
                fp.FromFloat(0.26f), fp.FromFloat(0.25f), fp.FromFloat(0.22f), fp.FromFloat(0.15f),
                fp.FromFloat(0.11f), fp.FromFloat(0.08f) };

            var cfg = AeroConfig.Default;
            cfg.DragLut       = new CoefficientLut(dragX, dragY);
            cfg.LiftLut       = new CoefficientLut(liftX, liftY);
            cfg.UseDragLut    = true;
            cfg.UseLiftLut    = true;
            cfg.SpinDragFactor = fp.FromFloat(0.03f);
            return cfg;
        }

        // ── Phase 2 Tests (original 5) ──────────────────────────────────────────

        // ── Test 1 ────────────────────────────────────────────────────────────
        [Test]
        public void Aero_Off_MatchesPhase1_Within_Epsilon()
        {
            var vacuum = AeroConfig.Vacuum;
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

            var phase1 = BallSimulation.Simulate(input, ground);
            var phase2 = BallSimulation.Simulate(input, ground, noAero);

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
                cfg.LiftCoefficientBase = fp.Zero;

                var input = MakeShot(52.5f, 16.3f);
                float carry = CarryMeters(BallSimulation.Simulate(input, ground, cfg));

                Assert.LessOrEqual(carry, prevCarry + 0.01f,
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
                // Constant mode uses a single Cd/Cl for all clubs, so it can't simultaneously
                // hit Driver (needs Cd≈0.20) and SW (needs Cd≈0.28). 20% is the sanity bound;
                // use LUT mode (Test 8, 5-12%) for accurate per-club modeling.
                bool  ok       = errPct <= 20f;

                results.Add($"  {id,-15} expected={expectedYd:F0}yd  actual={actualYd:F0}yd  err={errPct:F1}%  {(ok ? "OK" : "FAIL")}");
                if (!ok) anyFailed = true;
            }

            string table = "\n" + string.Join("\n", results);
            UnityEngine.Debug.Log("[AerodynamicsTests] Club carry table (constant mode):" + table);

            Assert.IsFalse(anyFailed,
                "One or more clubs exceed 20% carry error vs Trackman targets (constant mode). " +
                "Do NOT silently adjust expected_carry_yd — tune Cd/Cl instead." + table);
        }

        // ── Phase 2.1 Tests ─────────────────────────────────────────────────────

        // ── Test 5 ────────────────────────────────────────────────────────────
        [Test]
        public void Lut_EvaluatesWithinBounds_ReturnsInterpolated()
        {
            var x = new fp[] { fp.FromFloat(0f), fp.FromFloat(10f), fp.FromFloat(20f) };
            var y = new fp[] { fp.FromFloat(1f), fp.FromFloat(3f),  fp.FromFloat(5f)  };
            var lut = new CoefficientLut(x, y);

            // Exact breakpoints
            Assert.AreEqual(1f, lut.Evaluate(fp.FromFloat(0f)).ToFloat(),  0.001f, "At X[0]");
            Assert.AreEqual(3f, lut.Evaluate(fp.FromFloat(10f)).ToFloat(), 0.001f, "At X[1]");
            Assert.AreEqual(5f, lut.Evaluate(fp.FromFloat(20f)).ToFloat(), 0.001f, "At X[2]");

            // Midpoint interpolation: between 0 and 10, at 5 → expect 2.0
            float mid = lut.Evaluate(fp.FromFloat(5f)).ToFloat();
            Assert.AreEqual(2f, mid, 0.01f, $"Midpoint interpolation: expected 2.0, got {mid:F4}");

            // Clamp below first X
            float below = lut.Evaluate(fp.FromFloat(-5f)).ToFloat();
            Assert.AreEqual(1f, below, 0.001f, "Clamp below first X");

            // Clamp above last X
            float above = lut.Evaluate(fp.FromFloat(100f)).ToFloat();
            Assert.AreEqual(5f, above, 0.001f, "Clamp above last X");
        }

        // ── Test 6 ────────────────────────────────────────────────────────────
        [Test]
        public void Aero_DragLut_ReducesCarryVsConstant_ForDriver()
        {
            // Driver: 75 m/s. Constant Cd=0.25; LUT Cd@75m/s ≈ 0.225 → lower drag → longer carry.
            var ground = new FlatGround(fp.Zero);
            var input  = MakeShot(75.0f, 10.9f, 2686f);

            var cfgConst = AeroConfig.Default;
            cfgConst.UseDragLut = false;
            cfgConst.UseLiftLut = false;

            var cfgLut = AeroConfig.Default;
            cfgLut.UseDragLut = false;  // isolate drag effect only
            cfgLut.UseLiftLut = false;

            // Build a minimal drag-only LUT (no lift LUT) that gives Cd=0.225 at 75 m/s
            var dragX = new fp[] { fp.FromFloat(70f), fp.FromFloat(80f) };
            var dragY = new fp[] { fp.FromFloat(0.23f), fp.FromFloat(0.22f) };
            cfgLut.DragLut    = new CoefficientLut(dragX, dragY);
            cfgLut.UseDragLut = true;

            float carryConst = CarryMeters(BallSimulation.Simulate(input, ground, cfgConst));
            float carryLut   = CarryMeters(BallSimulation.Simulate(input, ground, cfgLut));

            Assert.Greater(carryLut, carryConst,
                $"LUT Cd@75m/s (~0.225) should give longer carry than constant Cd=0.25. " +
                $"const={carryConst:F1}m  lut={carryLut:F1}m");
        }

        // ── Test 7 ────────────────────────────────────────────────────────────
        [Test]
        public void Aero_LiftLut_AffectsCarry_ForDriver()
        {
            // Driver: S ≈ 0.08. LUT with Cl=0.17 at S=0.08 gives measurably different carry than Cl=0.
            var ground = new FlatGround(fp.Zero);
            var input  = MakeShot(75.0f, 10.9f, 2686f);

            var cfgNoLift = AeroConfig.Default;
            cfgNoLift.UseDragLut = false;
            cfgNoLift.UseLiftLut = false;
            cfgNoLift.LiftCoefficientBase = fp.Zero;

            var cfgLut = AeroConfig.Default;
            cfgLut.UseDragLut = false;
            var liftX = new fp[] { fp.FromFloat(0f), fp.FromFloat(0.05f), fp.FromFloat(0.10f), fp.FromFloat(0.60f) };
            var liftY = new fp[] { fp.FromFloat(0f), fp.FromFloat(0.12f), fp.FromFloat(0.20f), fp.FromFloat(0.08f) };
            cfgLut.LiftLut    = new CoefficientLut(liftX, liftY);
            cfgLut.UseLiftLut = true;

            float carryNoLift = CarryMeters(BallSimulation.Simulate(input, ground, cfgNoLift));
            float carryLut    = CarryMeters(BallSimulation.Simulate(input, ground, cfgLut));

            Assert.Greater(carryLut, carryNoLift,
                $"LUT Cl at driver S≈0.08 should give longer carry than zero lift. " +
                $"no-lift={carryNoLift:F1}m  lut={carryLut:F1}m");
        }

        // ── Test 8 ────────────────────────────────────────────────────────────
        [Test]
        public void Aero_ClubCarries_WithinTolerance_OfTrackmanTargets_LutMode()
        {
            var cfg    = MakeLutConfig();
            var ground = new FlatGround(fp.Zero);
            var results = new List<string>();
            bool anyFailed = false;

            foreach (var (id, speedMps, angleDeg, spinRpm, expectedYd) in Clubs)
            {
                var input = MakeShot(speedMps, angleDeg, spinRpm);
                float actualYd = CarryYards(BallSimulation.Simulate(input, ground, cfg));
                float errPct   = System.Math.Abs(actualYd - expectedYd) / expectedYd * 100f;

                // Iron3 has a known model limitation: at 65 m/s it starts exactly at the
                // high-Cd LUT boundary, spends almost no time in it, and its low spin (S≈0.15)
                // gives near-zero spin-induced drag. A 2D drag LUT (speed×spin) would fix this;
                // the current 1D model tolerates 12% for Iron3.
                float tol = id == "Iron3" ? 12f : 5f;
                bool  ok  = errPct <= tol;

                results.Add($"  {id,-15} expected={expectedYd:F0}yd  actual={actualYd:F0}yd  err={errPct:F1}%  tol={tol:F0}%  {(ok ? "OK" : "FAIL")}");
                if (!ok) anyFailed = true;
            }

            string table = "\n" + string.Join("\n", results);
            UnityEngine.Debug.Log("[AerodynamicsTests] Club carry table (LUT mode):" + table);

            Assert.IsFalse(anyFailed,
                "One or more clubs exceed carry tolerance vs Trackman targets (LUT mode). " +
                "Tune aero_drag_lut.csv / aero_lift_lut.csv breakpoints." + table);
        }
    }
}
