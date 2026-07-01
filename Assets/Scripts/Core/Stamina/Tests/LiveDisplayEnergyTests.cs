// ─────────────────────────────────────────────────────────────────────────────
// LiveDisplayEnergyTests — EditMode unit tests (Phase 5 §Acceptance)
//
// Tests StaminaModel.LiveDisplayEnergy — the pure read-only projection helper
// that CharacterDetailPanel uses for its live Condition tick.
//
// No UnityEngine scene required — pure math over StaminaModel.
// Lives in Golfin.Core.Stamina.Tests (Editor-only asmdef, already references
// Golfin.Core.Stamina where LiveDisplayEnergy lives).
// ─────────────────────────────────────────────────────────────────────────────
using System;
using NUnit.Framework;

namespace Golfin.Core.Stamina.Tests
{
    [TestFixture]
    public class LiveDisplayEnergyTests
    {
        // CSV matching Assets/Resources/Gameplay/stamina_economy.csv
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
            StaminaModel.ResetForTests();
            var config = StaminaConfig.Parse(DefaultCsvText);
            StaminaModel.Configure(config);
        }

        [TearDown]
        public void TearDown()
        {
            StaminaModel.ResetForTests();
        }

        // ── Test 1: Zero elapsed → no gain ────────────────────────────────────

        [Test]
        public void LiveDisplayEnergy_ZeroElapsed_ReturnsCurrentEnergy()
        {
            // hasTimestamp=true, simElapsed=0 → RegenForElapsed returns 0 → same as current
            float result = StaminaModel.LiveDisplayEnergy(
                currentEnergy: 50f,
                maxEnergy:     114f,
                recovery:      9,
                hasTimestamp:  true,
                simElapsed:    TimeSpan.Zero);

            Assert.AreEqual(50f, result, Epsilon, "Zero elapsed should return currentEnergy unchanged");
        }

        // ── Test 2: 2h elapsed + Recovery 9 → +60 clamped to max ─────────────
        // RegenPerHour(9) = 12 + 9*2 = 30.  2h → +60 pts.

        [Test]
        public void LiveDisplayEnergy_2h_Recovery9_Gains60()
        {
            // currentEnergy=10, max=200; gain=60 → 70
            float result = StaminaModel.LiveDisplayEnergy(
                currentEnergy: 10f,
                maxEnergy:     200f,
                recovery:      9,
                hasTimestamp:  true,
                simElapsed:    TimeSpan.FromHours(2));

            Assert.AreEqual(70f, result, Epsilon, "10 + 60 regen = 70");
        }

        [Test]
        public void LiveDisplayEnergy_2h_Recovery9_ClampsToMax()
        {
            // currentEnergy=80, max=114; gain=60 → 140 clamped to 114
            float result = StaminaModel.LiveDisplayEnergy(
                currentEnergy: 80f,
                maxEnergy:     114f,
                recovery:      9,
                hasTimestamp:  true,
                simElapsed:    TimeSpan.FromHours(2));

            Assert.AreEqual(114f, result, Epsilon, "Should clamp to maxEnergy=114");
        }

        // ── Test 3: default timestamp (hasTimestamp=false) → currentEnergy unchanged ──

        [Test]
        public void LiveDisplayEnergy_NoTimestamp_ReturnsCurrentEnergy()
        {
            // hasTimestamp=false → skip regen entirely, regardless of elapsed
            float result = StaminaModel.LiveDisplayEnergy(
                currentEnergy: 75f,
                maxEnergy:     114f,
                recovery:      9,
                hasTimestamp:  false,
                simElapsed:    TimeSpan.FromHours(10));  // large elapsed ignored

            Assert.AreEqual(75f, result, Epsilon, "No timestamp should return currentEnergy as-is");
        }

        // ── Test 4: demoExtra accelerates (virtual hours added to simElapsed) ──
        // Caller adds demo-accel virtual hours to simElapsed before calling.
        // 1 virtual hour + Recovery 9 → +30 pts.

        [Test]
        public void LiveDisplayEnergy_DemoExtra_AddsVirtualHours()
        {
            // Real elapsed = 0, demoExtra = 1 virtual hour → gain = RegenPerHour(9)*1 = 30
            TimeSpan simElapsed = TimeSpan.FromHours(1.0);
            float result = StaminaModel.LiveDisplayEnergy(
                currentEnergy: 10f,
                maxEnergy:     200f,
                recovery:      9,
                hasTimestamp:  true,
                simElapsed:    simElapsed);

            Assert.AreEqual(40f, result, Epsilon, "1 virtual hour Recovery9 → +30 → 10+30=40");
        }

        // ── Test 5: negative elapsed → no gain (RegenForElapsed guards) ───────

        [Test]
        public void LiveDisplayEnergy_NegativeElapsed_ReturnsCurrentEnergy()
        {
            float result = StaminaModel.LiveDisplayEnergy(
                currentEnergy: 50f,
                maxEnergy:     200f,
                recovery:      9,
                hasTimestamp:  true,
                simElapsed:    TimeSpan.FromHours(-2));

            Assert.AreEqual(50f, result, Epsilon, "Negative elapsed should yield zero gain");
        }
    }
}
