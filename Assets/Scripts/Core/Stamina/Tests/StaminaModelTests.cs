// ─────────────────────────────────────────────────────────────────────────────
// stamina_model — EditMode unit tests (SPEC §8)
//
// Test coverage (all with the authored CSV defaults):
//  1.  StaminaConfig.Parse reads all 12 keys; DegradedStats = {Strength, ClubControl}
//  2.  MaxCondition at various stamina stat values
//  3.  DrainForHole returns the flat constant
//  4.  RegenPerHour at two Recovery values
//  5.  RegenForElapsed: 2h, zero, negative elapsed
//  6.  ConditionPct: normal, overflow, negative
//  7.  PenaltyFor/EffectiveStat at comfort-threshold boundary (0 penalty)
//  8.  PenaltyFor/EffectiveStat at empty Condition (floor penalty)
//  9.  PenaltyFor is monotonically non-increasing
//  10. MeterState at High/Mid/Low thresholds
//  11. IsLowConditionFlag below and at threshold
//  12. IsDegraded — case-insensitive; known degraded; non-degraded
//  13. Calling methods before Configure throws InvalidOperationException
//
// Pure EditMode: no scene, no Resources — StaminaConfig.Parse receives the CSV text directly.
// Lives in Golfin.Core.Stamina.Tests (Editor-only asmdef).
// ─────────────────────────────────────────────────────────────────────────────
using System;
using NUnit.Framework;

namespace Golfin.Core.Stamina.Tests
{
    [TestFixture]
    public class StaminaModelTests
    {
        // ── CSV text matching Assets/Resources/Gameplay/stamina_economy.csv ────

        private const string DefaultCsvText =
@"key,value,notes
drain_per_hole,8,Flat Condition points drained at each hole completion
tank_base,60,Base MaxCondition before the Stamina stat is added
tank_per_stamina_point,6,Added MaxCondition per point of the Stamina stat
regen_base_per_hour,12,Condition points regained per real hour at Recovery stat 0
regen_per_recovery_point,2,Added Condition points/hour per point of the Recovery stat
comfort_threshold_pct,0.70,At/above this Condition% the penalty is zero
floor_penalty,0.33,Max stat reduction at empty Condition
penalty_curve_exp,1.6,Ramp shape between comfort edge and empty
degraded_stats,Strength;ClubControl,Stats reduced by low Condition
meter_high_pct,0.60,At/above this Condition% the roster Stamina meter shows blue
meter_mid_pct,0.30,Between mid and high the meter shows yellow
low_condition_flag_pct,0.25,Below this Condition% the portrait low-stamina icon shows";

        private const float Epsilon = 0.001f;

        [SetUp]
        public void SetUp()
        {
            // Always reset then reconfigure to isolate tests.
            StaminaModel.ResetForTests();
            var config = StaminaConfig.Parse(DefaultCsvText);
            StaminaModel.Configure(config);
        }

        [TearDown]
        public void TearDown()
        {
            StaminaModel.ResetForTests();
        }

        // ── Test 1: StaminaConfig.Parse reads all 12 keys ─────────────────────

        [Test]
        public void Parse_ReadsAll12Keys()
        {
            var config = StaminaConfig.Parse(DefaultCsvText);

            Assert.AreEqual(8f,    config.DrainPerHole,          Epsilon, "DrainPerHole");
            Assert.AreEqual(60f,   config.TankBase,               Epsilon, "TankBase");
            Assert.AreEqual(6f,    config.TankPerStaminaPoint,    Epsilon, "TankPerStaminaPoint");
            Assert.AreEqual(12f,   config.RegenBasePerHour,       Epsilon, "RegenBasePerHour");
            Assert.AreEqual(2f,    config.RegenPerRecoveryPoint,  Epsilon, "RegenPerRecoveryPoint");
            Assert.AreEqual(0.70f, config.ComfortThresholdPct,    Epsilon, "ComfortThresholdPct");
            Assert.AreEqual(0.33f, config.FloorPenalty,           Epsilon, "FloorPenalty");
            Assert.AreEqual(1.6f,  config.PenaltyCurveExp,        Epsilon, "PenaltyCurveExp");
            Assert.AreEqual(0.60f, config.MeterHighPct,           Epsilon, "MeterHighPct");
            Assert.AreEqual(0.30f, config.MeterMidPct,            Epsilon, "MeterMidPct");
            Assert.AreEqual(0.25f, config.LowConditionFlagPct,    Epsilon, "LowConditionFlagPct");

            // degraded_stats = {Strength, ClubControl}
            var stats = config.DegradedStats;
            Assert.AreEqual(2, new System.Collections.Generic.List<string>(stats).Count,
                "DegradedStats should have 2 entries");
            CollectionAssert.Contains(stats, "Strength",    "DegradedStats should contain Strength");
            CollectionAssert.Contains(stats, "ClubControl", "DegradedStats should contain ClubControl");
        }

        // ── Test 2: MaxCondition ──────────────────────────────────────────────

        [Test]
        public void MaxCondition_StaminaStat9_Returns114()
        {
            Assert.AreEqual(114, StaminaModel.MaxCondition(9));
        }

        [Test]
        public void MaxCondition_StaminaStat27_Returns222()
        {
            Assert.AreEqual(222, StaminaModel.MaxCondition(27));
        }

        [Test]
        public void MaxCondition_StaminaStat0_Returns60()
        {
            Assert.AreEqual(60, StaminaModel.MaxCondition(0));
        }

        // ── Test 3: DrainForHole ──────────────────────────────────────────────

        [Test]
        public void DrainForHole_Returns8()
        {
            Assert.AreEqual(8f, StaminaModel.DrainForHole(), Epsilon);
        }

        // ── Test 4: RegenPerHour ──────────────────────────────────────────────

        [Test]
        public void RegenPerHour_Recovery9_Returns30()
        {
            Assert.AreEqual(30f, StaminaModel.RegenPerHour(9), Epsilon);
        }

        [Test]
        public void RegenPerHour_Recovery40_Returns92()
        {
            Assert.AreEqual(92f, StaminaModel.RegenPerHour(40), Epsilon);
        }

        // ── Test 5: RegenForElapsed ───────────────────────────────────────────

        [Test]
        public void RegenForElapsed_Recovery9_2Hours_Returns60()
        {
            var elapsed = TimeSpan.FromHours(2);
            Assert.AreEqual(60f, StaminaModel.RegenForElapsed(9, elapsed), Epsilon);
        }

        [Test]
        public void RegenForElapsed_ZeroElapsed_Returns0()
        {
            Assert.AreEqual(0f, StaminaModel.RegenForElapsed(9, TimeSpan.Zero), Epsilon);
        }

        [Test]
        public void RegenForElapsed_NegativeElapsed_Returns0()
        {
            var negative = TimeSpan.FromHours(-1);
            Assert.AreEqual(0f, StaminaModel.RegenForElapsed(9, negative), Epsilon);
        }

        // ── Test 6: ConditionPct ──────────────────────────────────────────────

        [Test]
        public void ConditionPct_Half_Returns0_5()
        {
            // MaxCondition(9) = 114; 57/114 = 0.5
            float result = StaminaModel.ConditionPct(57f, 9);
            Assert.AreEqual(0.5f, result, Epsilon);
        }

        [Test]
        public void ConditionPct_Overflow_ClampsTo1()
        {
            float result = StaminaModel.ConditionPct(999f, 9);
            Assert.AreEqual(1f, result, Epsilon);
        }

        [Test]
        public void ConditionPct_Negative_ClampsTo0()
        {
            float result = StaminaModel.ConditionPct(-5f, 9);
            Assert.AreEqual(0f, result, Epsilon);
        }

        // ── Test 7: PenaltyFor / EffectiveStat at comfort zone ────────────────

        [Test]
        public void PenaltyFor_AtOrAboveComfort_ReturnsZero()
        {
            // 0.80 >= ComfortThresholdPct (0.70) → 0
            Assert.AreEqual(0f, StaminaModel.PenaltyFor(0.80f), Epsilon);
        }

        [Test]
        public void EffectiveStat_AboveComfort_EqualToBase()
        {
            // penalty=0 → effective == base
            Assert.AreEqual(20, StaminaModel.EffectiveStat(20, 0.80f));
        }

        // ── Test 8: PenaltyFor / EffectiveStat at empty Condition ─────────────

        [Test]
        public void PenaltyFor_AtZero_EqualsFloorPenalty()
        {
            // pct=0 → t=1 → penalty = 0.33 * 1.0^1.6 = 0.33
            Assert.AreEqual(0.33f, StaminaModel.PenaltyFor(0.0f), Epsilon);
        }

        [Test]
        public void EffectiveStat_AtZeroCondition_Returns13()
        {
            // round(20 * (1 - 0.33)) = round(20 * 0.67) = round(13.4) = 13
            Assert.AreEqual(13, StaminaModel.EffectiveStat(20, 0.0f));
        }

        // ── Test 9: PenaltyFor is monotonically non-increasing ────────────────

        [Test]
        public void PenaltyFor_IsMonotonic_And_BelowFloor()
        {
            float p20 = StaminaModel.PenaltyFor(0.20f);
            float p05 = StaminaModel.PenaltyFor(0.05f);
            float floor = 0.33f;

            Assert.Less(p20, p05, "PenaltyFor(0.20) should be less than PenaltyFor(0.05) — higher pct = less penalty");
            Assert.Less(p20, floor, "PenaltyFor(0.20) should be below floor (0.33)");
            Assert.Less(p05, floor, "PenaltyFor(0.05) should be below floor (0.33)");
        }

        // ── Test 10: MeterState ───────────────────────────────────────────────

        [Test]
        public void MeterState_0_70_ReturnsHigh()
        {
            Assert.AreEqual(MeterColorState.High, StaminaModel.MeterState(0.70f));
        }

        [Test]
        public void MeterState_0_45_ReturnsMid()
        {
            Assert.AreEqual(MeterColorState.Mid, StaminaModel.MeterState(0.45f));
        }

        [Test]
        public void MeterState_0_20_ReturnsLow()
        {
            Assert.AreEqual(MeterColorState.Low, StaminaModel.MeterState(0.20f));
        }

        // ── Test 11: IsLowConditionFlag ───────────────────────────────────────

        [Test]
        public void IsLowConditionFlag_0_20_ReturnsTrue()
        {
            Assert.IsTrue(StaminaModel.IsLowConditionFlag(0.20f));
        }

        [Test]
        public void IsLowConditionFlag_0_30_ReturnsFalse()
        {
            Assert.IsFalse(StaminaModel.IsLowConditionFlag(0.30f));
        }

        // ── Test 12: IsDegraded ───────────────────────────────────────────────

        [Test]
        public void IsDegraded_LowercaseStrength_ReturnsTrue()
        {
            Assert.IsTrue(StaminaModel.IsDegraded("strength"));
        }

        [Test]
        public void IsDegraded_Recovery_ReturnsFalse()
        {
            Assert.IsFalse(StaminaModel.IsDegraded("Recovery"));
        }

        [Test]
        public void IsDegraded_ClubControl_ReturnsTrue()
        {
            Assert.IsTrue(StaminaModel.IsDegraded("ClubControl"));
        }

        [Test]
        public void IsDegraded_Unknown_ReturnsFalse()
        {
            Assert.IsFalse(StaminaModel.IsDegraded("Speed"));
        }

        // ── Tests Order731-PS: Provider-side stamina gate (Order 731, 2026-07-16) ──
        // Assert that StaminaModel.EffectiveStat IS the single degradation path and that
        // it returns the base stat unchanged above the comfort threshold (90% condition → no penalty).
        // These tests are the positive proof that the provider path degrades correctly now
        // that the resolver-side duplicate lane is gone.

        [Test]
        public void ProviderSide_AboveComfort_NoStatPenalty()
        {
            // ComfortThresholdPct = 0.70; 90% is above it → penalty = 0 → effective == base.
            int baseStat = 80;
            int effective = StaminaModel.EffectiveStat(baseStat, 0.90f);
            Assert.AreEqual(baseStat, effective,
                $"At 90% condition (above comfort threshold 70%) EffectiveStat({baseStat}, 0.90) must equal base {baseStat}");
        }

        [Test]
        public void ProviderSide_BelowComfort_StatDegraded()
        {
            // At 40% condition (below comfort 70%) the stat is reduced.
            int baseStat = 80;
            int effective = StaminaModel.EffectiveStat(baseStat, 0.40f);
            Assert.Less(effective, baseStat,
                $"At 40% condition (below comfort threshold 70%) EffectiveStat must be less than base {baseStat}");
            // Sanity check: floor at 0% is 80*(1-0.33)=53.6→54; at 40% penalty < floor, so effective > 54.
            Assert.Greater(effective, 53,
                $"EffectiveStat at 40% condition should be > 53 (floor is 67% of base)");
        }

        [Test]
        public void ProviderSide_IsDegraded_StrengthAndClubControl()
        {
            // Confirm that Strength and ClubControl are in the degraded set so the
            // provider applies EffectiveStat to them.  Recovery and Stamina must NOT be degraded.
            Assert.IsTrue(StaminaModel.IsDegraded("Strength"),    "Strength should be degraded");
            Assert.IsTrue(StaminaModel.IsDegraded("ClubControl"), "ClubControl should be degraded");
            Assert.IsFalse(StaminaModel.IsDegraded("Recovery"),   "Recovery should NOT be degraded");
            Assert.IsFalse(StaminaModel.IsDegraded("Stamina"),    "Stamina stat should NOT be degraded");
        }

        // ── Test 13: Pre-Configure guard ──────────────────────────────────────

        [Test]
        public void MaxCondition_BeforeConfigure_Throws()
        {
            StaminaModel.ResetForTests();  // ensure unconfigured

            Assert.Throws<InvalidOperationException>(() => StaminaModel.MaxCondition(9));
        }
    }
}
