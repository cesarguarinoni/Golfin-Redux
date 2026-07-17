using NUnit.Framework;
using Golfin.Physics;
using Golfin.Physics.Math;
using Golfin.Physics.Stats;

namespace Golfin.Physics.Tests
{
    /// <summary>
    /// Phase 6 stat-coupling tests. Total target: 49 tests (39 prior + 10 here), all pass.
    /// </summary>
    public class StatResolverTests
    {
        // ── Helpers ─────────────────────────────────────────────────────────────────

        private static StatCoefficients Coeffs => StatCoefficients.Default;
        private static StatCaps         Caps   => StatCaps.Default;

        private static ClubStats IronClub(int power = 0, int accuracy = 0, int lieResistance = 0)
            => new ClubStats(power, accuracy, lieResistance, 0,
                fp.FromFloat(16.3f), fp.FromFloat(52.5f), fp.FromFloat(7097f));

        private static CharacterStats NeutralChar => CharacterStats.Neutral;
        private static BallStats      NeutralBall  => BallStats.Neutral;

        private static fp FullCur => fp.FromInt(100);
        private static fp FullMax => fp.FromInt(100);

        // ── Test 1: Bit-exact gate ─────────────────────────────────────────────────

        /// <summary>
        /// 7-arg Phase 5 overload vs 8-arg Phase 6 with BallPhysicsModifiers.Neutral
        /// must produce bit-identical final positions.
        /// </summary>
        [Test]
        public void Stats_Phase5Overloads_BitExact()
        {
            float angleRad = 16.3f * (float)System.Math.PI / 180f;
            var   origin   = fp3.Zero;
            var   velocity = new fp3(fp.Zero,
                fp.FromDouble(52.5 * System.Math.Sin(angleRad)),
                fp.FromDouble(52.5 * System.Math.Cos(angleRad)));
            float rps  = 7097f * 2f * (float)System.Math.PI / 60f;
            var   spin = new SpinState(new fp3(fp.FromInt(-1), fp.Zero, fp.Zero), fp.FromFloat(rps));
            var   input = new ShotInput(origin, velocity, fp.FromInt(30), spin);

            var ground  = new FlatGround(fp.Zero);
            var aero    = AeroConfig.Default;
            var wind    = WindConfig.Calm;
            var surf    = new ConstantSurfaceProvider(SurfaceType.Fairway);
            var surfCfg = SurfaceConfig.Default;
            var putt    = PuttConfig.Default;

            var t7 = BallSimulation.Simulate(input, ground, aero, wind, surf, surfCfg, putt);
            var t8 = BallSimulation.Simulate(input, ground, aero, wind, surf, surfCfg, putt, BallPhysicsModifiers.Neutral);

            Assert.AreEqual(t7.termination,         t8.termination,         "Termination");
            Assert.AreEqual(t7.finalPosition.x.raw, t8.finalPosition.x.raw, "finalPosition.x");
            Assert.AreEqual(t7.finalPosition.y.raw, t8.finalPosition.y.raw, "finalPosition.y");
            Assert.AreEqual(t7.finalPosition.z.raw, t8.finalPosition.z.raw, "finalPosition.z");
        }

        // ── Test 2: Neutral bundle → velocity multiplier 1.0 ──────────────────────

        [Test]
        public void Stats_NeutralBundle_VelocityMultiplierIsOne()
        {
            var bundle = new StatBundle(IronClub(), NeutralBall, NeutralChar, FullCur, FullMax);
            var r = StatModifierResolver.Resolve(bundle, Coeffs, Caps);
            Assert.AreEqual(fp.One.raw, r.VelocityMultiplier.raw,
                "Neutral bundle → VelocityMultiplier == 1.0");
        }

        // ── Test 3: Club Power 60 raises velocity multiplier to ~1.30 ─────────────

        [Test]
        public void Stats_ClubPower60_VelocityMultiplierOnePointThree()
        {
            // 60 × 0.005 = 0.30 → factor = 1.30; ball neutral → combined = 1.30
            var bundle = new StatBundle(IronClub(power: 60), NeutralBall, NeutralChar, FullCur, FullMax);
            var r = StatModifierResolver.Resolve(bundle, Coeffs, Caps);
            float actual = r.VelocityMultiplier.ToFloat();
            Assert.IsTrue(System.Math.Abs(actual - 1.30f) < 0.001f,
                $"Club 60 pts → vel ~1.30 (got {actual:F5})");
        }

        // ── Test 4: Ball Power is multiplicative with Club Power ───────────────────

        [Test]
        public void Stats_BallPower_MultiplicativeWithClub()
        {
            // Club 60 → ×1.30. Ball +10 → ×1.10. Combined = 1.43.
            var ball   = new BallStats(10, 0, 0, 0, 0);
            var bundle = new StatBundle(IronClub(power: 60), ball, NeutralChar, FullCur, FullMax);
            var r = StatModifierResolver.Resolve(bundle, Coeffs, Caps);
            float actual = r.VelocityMultiplier.ToFloat();
            Assert.IsTrue(System.Math.Abs(actual - 1.43f) < 0.001f,
                $"Combined vel ~1.43 (got {actual:F5})");
        }

        // ── Test 5: Velocity multiplier never exceeds 2.0 cap ─────────────────────

        [Test]
        public void Stats_VelocityMultiplier_HardCapAtTwo()
        {
            // Max club power (120) + max ball power (+10) — product must not exceed 2.0.
            var ball   = new BallStats(10, 0, 0, 0, 0);
            var bundle = new StatBundle(IronClub(power: 120), ball, NeutralChar, FullCur, FullMax);
            var r = StatModifierResolver.Resolve(bundle, Coeffs, Caps);
            fp cap = fp.FromFloat(2.0f);
            Assert.LessOrEqual(r.VelocityMultiplier.raw, cap.raw,
                "VelocityMultiplier must never exceed the 2.0 cap");
        }

        // ── Test 6 (Order 731): Resolver is stamina-agnostic ─────────────────────
        // The resolver must NOT re-apply stamina.  Stamina degradation is done exactly
        // once at LiveStatProviderHost.BuildCharacterStats before the bundle is built.
        // The old Stats_ZeroStamina_FloorPreservesCharStats test asserted the resolver-side
        // floor that Order 731 deletes and is replaced by this test.

        [Test]
        public void Stats_Resolver_IsStaminaAgnostic()
        {
            // Same CharacterStats + different CurrentStamina must produce identical ResolvedShotModifiers.
            // (The bundle's stamina fields are present for bookkeeping; the resolver must not use them.)
            var charStats = new CharacterStats(120, 25, 0, 0);
            var club      = IronClub(power: 60, accuracy: 50);
            fp fullCur  = fp.FromInt(100);
            fp halfCur  = fp.FromInt(50);
            fp zeroCur  = fp.Zero;
            fp maxStam  = fp.FromInt(100);

            var bundleFull = new StatBundle(club, NeutralBall, charStats, fullCur, maxStam);
            var bundleHalf = new StatBundle(club, NeutralBall, charStats, halfCur, maxStam);
            var bundleZero = new StatBundle(club, NeutralBall, charStats, zeroCur, maxStam);

            var rFull = StatModifierResolver.Resolve(bundleFull, Coeffs, Caps);
            var rHalf = StatModifierResolver.Resolve(bundleHalf, Coeffs, Caps);
            var rZero = StatModifierResolver.Resolve(bundleZero, Coeffs, Caps);

            // VelocityMultiplier must be bit-identical regardless of stamina level.
            Assert.AreEqual(rFull.VelocityMultiplier.raw, rHalf.VelocityMultiplier.raw,
                "VelocityMultiplier must not change with stamina (half vs full)");
            Assert.AreEqual(rFull.VelocityMultiplier.raw, rZero.VelocityMultiplier.raw,
                "VelocityMultiplier must not change with stamina (zero vs full)");

            // AimConeReductionFraction must be bit-identical (no ClubControl or stamina component).
            Assert.AreEqual(rFull.AimConeReductionFraction.raw, rHalf.AimConeReductionFraction.raw,
                "AimConeReductionFraction must not change with stamina (half vs full)");
            Assert.AreEqual(rFull.AimConeReductionFraction.raw, rZero.AimConeReductionFraction.raw,
                "AimConeReductionFraction must not change with stamina (zero vs full)");

            // OverpowerForgiveness must be bit-identical (effStrength is now raw from bundle).
            Assert.AreEqual(rFull.OverpowerForgivenessFraction.raw, rHalf.OverpowerForgivenessFraction.raw,
                "OverpowerForgiveness must not change with stamina (half vs full)");
            Assert.AreEqual(rFull.OverpowerForgivenessFraction.raw, rZero.OverpowerForgivenessFraction.raw,
                "OverpowerForgiveness must not change with stamina (zero vs full)");
        }

        // ── Test Order731-A: AimConeReductionFraction is a pure function of Club.Accuracy ──
        // Varying Character.ClubControl must not affect it.

        [Test]
        public void Stats_AimConeReductionFraction_PureFunctionOfClubAccuracy()
        {
            var club = IronClub(accuracy: 50);
            fp cur = FullCur; fp max = FullMax;

            // Different ClubControl values — cone must be identical.
            var charZeroCC    = new CharacterStats(0,   0,  0, 0);
            var charMaxCC     = new CharacterStats(0, 120,  0, 0);
            var charMidCC     = new CharacterStats(0,  25,  0, 0);

            var rZeroCC = StatModifierResolver.Resolve(new StatBundle(club, NeutralBall, charZeroCC, cur, max), Coeffs, Caps);
            var rMaxCC  = StatModifierResolver.Resolve(new StatBundle(club, NeutralBall, charMaxCC,  cur, max), Coeffs, Caps);
            var rMidCC  = StatModifierResolver.Resolve(new StatBundle(club, NeutralBall, charMidCC,  cur, max), Coeffs, Caps);

            Assert.AreEqual(rZeroCC.AimConeReductionFraction.raw, rMaxCC.AimConeReductionFraction.raw,
                $"AimConeReductionFraction must not change with ClubControl (0 vs 120). " +
                $"Zero={rZeroCC.AimConeReductionFraction.ToFloat():F4}, Max={rMaxCC.AimConeReductionFraction.ToFloat():F4}");
            Assert.AreEqual(rZeroCC.AimConeReductionFraction.raw, rMidCC.AimConeReductionFraction.raw,
                $"AimConeReductionFraction must not change with ClubControl (0 vs 25). " +
                $"Zero={rZeroCC.AimConeReductionFraction.ToFloat():F4}, Mid={rMidCC.AimConeReductionFraction.ToFloat():F4}");

            // Also assert that Club.Accuracy DOES drive cone — non-zero accuracy must reduce.
            var clubZeroAcc = IronClub(accuracy: 0);
            var rZeroAcc = StatModifierResolver.Resolve(new StatBundle(clubZeroAcc, NeutralBall, charZeroCC, cur, max), Coeffs, Caps);
            Assert.Greater(rZeroCC.AimConeReductionFraction.raw, rZeroAcc.AimConeReductionFraction.raw,
                "Accuracy=50 must produce larger AimConeReductionFraction than Accuracy=0");
        }

        // ── Test 7: Ball Rebound +10 → ReboundMultiplier == 1.20 ─────────────────
        // Updated for ball_rebound_perceptibility (Order 417, 2026-07-17): BallReboundPerPoint
        // raised 0.01 → 0.02, so Rebound +10 → 1 + 10×0.02 = 1.20 (lands exactly on the
        // ReboundMultiplierMax = 1.20 cap; no clamp change). At 0.01 the ±10 swing moved total
        // distance only ~4.8m (below the 10m bar); at 0.02 it's ~10.7m.

        [Test]
        public void Stats_BallRebound_MultiplierCorrect()
        {
            var ball   = new BallStats(0, 10, 0, 0, 0);
            var bundle = new StatBundle(IronClub(), ball, NeutralChar, FullCur, FullMax);
            var r = StatModifierResolver.Resolve(bundle, Coeffs, Caps);
            float actual = r.BallPhysics.ReboundMultiplier.ToFloat();
            Assert.IsTrue(System.Math.Abs(actual - 1.20f) < 0.001f,
                $"Ball Rebound +10 → ReboundMultiplier ~1.20 (got {actual:F5})");
        }

        // ── Test 8: Ball WindCut +10 → WindCutFraction == 0.10 ───────────────────

        [Test]
        public void Stats_BallWindCut_FractionCorrect()
        {
            var ball   = new BallStats(0, 0, 10, 0, 0);
            var bundle = new StatBundle(IronClub(), ball, NeutralChar, FullCur, FullMax);
            var r = StatModifierResolver.Resolve(bundle, Coeffs, Caps);
            float actual = r.BallPhysics.WindCutFraction.ToFloat();
            Assert.IsTrue(System.Math.Abs(actual - 0.10f) < 0.001f,
                $"Ball WindCut +10 → WindCutFraction ~0.10 (got {actual:F5})");
        }

        // ── Test 9: Ball Roll +10 → RollResistanceMultiplier == 0.80 ─────────────
        // Updated for ball_roll_coefficient_retune (2026-06-02): BallRollPerPoint raised
        // from 0.01 to 0.02, so rollMul = 1 − 10 × 0.02 = 0.80 (hits RollMultiplierMin cap).

        [Test]
        public void Stats_BallRoll_ReducesRollingResistance()
        {
            // rollMul = 1 − 10 × 0.02 = 0.80 — exactly at the RollMultiplierMin cap
            var ball   = new BallStats(0, 0, 0, 10, 0);
            var bundle = new StatBundle(IronClub(), ball, NeutralChar, FullCur, FullMax);
            var r = StatModifierResolver.Resolve(bundle, Coeffs, Caps);
            float actual = r.BallPhysics.RollResistanceMultiplier.ToFloat();
            Assert.IsTrue(System.Math.Abs(actual - 0.80f) < 0.001f,
                $"Ball Roll +10 → RollResistanceMultiplier ~0.80 (got {actual:F5})");
            Assert.Less(r.BallPhysics.RollResistanceMultiplier.raw, fp.One.raw,
                "Ball Roll +10 must reduce rolling resistance below 1.0");
        }

        // ── Test F7-A: Strength=50 swing produces higher velocityMultiplier than Strength=5 ─

        /// <summary>
        /// F7 (2026-05-25): Character.Strength now feeds velocityMultiplier via velFromChar.
        /// Strength=50 must produce a strictly higher velocityMultiplier than Strength=5.
        /// </summary>
        [Test]
        public void Stats_CharStrength50_VelocityMultiplierGreaterThan_Strength5()
        {
            // Same club + ball so the only difference is Character.Strength.
            var club = IronClub(power: 50);
            var ball = NeutralBall;
            fp cur = FullCur; fp max = FullMax;

            var charHigh = new CharacterStats(50, 0, 0, 0); // Strength = 50 (Supreme cap)
            var charLow  = new CharacterStats( 5, 0, 0, 0); // Strength =  5 (Common baseline)

            var bundleHigh = new StatBundle(club, ball, charHigh, cur, max);
            var bundleLow  = new StatBundle(club, ball, charLow,  cur, max);

            var rHigh = StatModifierResolver.Resolve(bundleHigh, Coeffs, Caps);
            var rLow  = StatModifierResolver.Resolve(bundleLow,  Coeffs, Caps);

            Assert.Greater(rHigh.VelocityMultiplier.raw, rLow.VelocityMultiplier.raw,
                $"Strength=50 ({rHigh.VelocityMultiplier.ToFloat():F4}) must exceed " +
                $"Strength=5 ({rLow.VelocityMultiplier.ToFloat():F4}) on a swing (F7).");
        }

        // ── Test F7-B: Putter — Strength has no effect on velocityMultiplier ────────

        /// <summary>
        /// F7 putter exemption: putters keep velFromChar = 1.0, so Strength=50 and Strength=5
        /// must produce identical velocityMultiplier on a putt bundle.
        /// </summary>
        [Test]
        public void Stats_Putter_CharStrengthHasNoEffectOnVelocityMultiplier()
        {
            var putter = PutterStats.DefaultPutter;
            var ball   = NeutralBall;
            fp cur = FullCur; fp max = FullMax;

            var charHigh = new CharacterStats(50, 0, 0, 0);
            var charLow  = new CharacterStats( 5, 0, 0, 0);

            var bundleHigh = new StatBundle(putter, ball, charHigh, cur, max);
            var bundleLow  = new StatBundle(putter, ball, charLow,  cur, max);

            var rHigh = StatModifierResolver.Resolve(bundleHigh, Coeffs, Caps);
            var rLow  = StatModifierResolver.Resolve(bundleLow,  Coeffs, Caps);

            Assert.AreEqual(rHigh.VelocityMultiplier.raw, rLow.VelocityMultiplier.raw,
                $"Putter: Strength must NOT affect velocityMultiplier (F7 putter exemption). " +
                $"High={rHigh.VelocityMultiplier.ToFloat():F4}, Low={rLow.VelocityMultiplier.ToFloat():F4}");
        }

        // ── Test 10: ShotInputBuilder.Build + Simulate produces realistic carry ────

        [Test]
        public void Stats_ShotInputBuilder_IronCarryInRange()
        {
            var bundle = new StatBundle(IronClub(), NeutralBall, NeutralChar, FullCur, FullMax);

            var (input, ballMods) = ShotInputBuilder.Build(
                bundle, Coeffs, Caps,
                fp.One,    // full power gauge
                fp.Zero,   // aim yaw = 0 → +X forward
                fp.Zero, fp.Zero, fp.Zero,
                12345u);

            var ground  = new FlatGround(fp.Zero);
            var aero    = AeroConfig.Default;
            var wind    = WindConfig.Calm;
            var surf    = new ConstantSurfaceProvider(SurfaceType.Fairway);
            var surfCfg = SurfaceConfig.Default;
            var putt    = PuttConfig.Default;

            var result = BallSimulation.Simulate(input, ground, aero, wind, surf, surfCfg, putt, ballMods);

            float fx   = result.finalPosition.x.ToFloat();
            float fz   = result.finalPosition.z.ToFloat();
            float dist = (float)System.Math.Sqrt(fx * fx + fz * fz);

            // Standard 7-iron at full power, neutral stats, calm wind — expect 100–220 m carry.
            Assert.Greater(dist, 100f, $"Iron carry should exceed 100 m (got {dist:F1} m)");
            Assert.Less(dist,    220f, $"Iron carry should be under 220 m (got {dist:F1} m)");
        }
    }
}
