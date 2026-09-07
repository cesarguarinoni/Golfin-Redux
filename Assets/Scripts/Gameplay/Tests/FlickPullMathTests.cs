using NUnit.Framework;
using UnityEngine;
using Golfin.Gameplay.Config;
using Golfin.Gameplay.Input;

namespace Golfin.Gameplay.Tests
{
    /// <summary>
    /// flick_pull_mapping §3.4 — Flick's pull reads like the other three schemes' do.
    ///
    /// <para>WHAT THIS FIXTURE IS ACTUALLY GUARDING. The bug it closes was not an arithmetic slip:
    /// the old mapping (<c>power = 1 - handleY / ConeHeightPx</c>) was internally consistent and
    /// perfectly testable, and it still read 31.8% the instant the club was touched because it
    /// measured from the cone's BASE instead of from the club. So the table below is not "the
    /// formula is the formula" — the first two rows are the claim that touching and nudging the
    /// club produce NOTHING, and the last is the claim that the base of the cone means 120%.</para>
    ///
    /// <para>The three identities at the bottom are the ones no single call site can hold on its
    /// own: the drawn club is the inverse of the finger, 120% is exactly 1.2 x 100%, and the
    /// club's rest fraction times the cone height IS the 120% pull. Break any one and the cone
    /// still draws, the shot still fires, and the number under the player's thumb quietly stops
    /// matching the line they are aiming at.</para>
    /// </summary>
    [TestFixture]
    public class FlickPullMathTests
    {
        private static ControlsConfig Cfg => ControlsConfig.Default;

        // ── The table (SPEC §3.4) ───────────────────────────────────────────────

        [TestCase(0f,    0f,    TestName = "at rest, the club is touched and nothing is dialled in")]
        [TestCase(39f,   0f,    TestName = "inside the 40px dead zone, still nothing")]
        [TestCase(40f,   0f,    TestName = "MinUsefulPullPx is the first pull that counts, and it counts as zero")]
        [TestCase(290f,  0.5f,  TestName = "halfway between the dead zone and 100%")]
        [TestCase(540f,  1f,    TestName = "FlickPull100Px is 100%")]
        [TestCase(594f,  1.1f,  TestName = "halfway up the overpower ramp")]
        [TestCase(648f,  1.2f,  TestName = "FlickPull120Px — the cone's BASE — is 120%")]
        [TestCase(700f,  1.2f,  TestName = "past the base the pull saturates, it does not keep climbing")]
        public void Power_MatchesTheSpecTable(float pullPx, float expected)
            => Assert.AreEqual(expected, FlickPullMath.Power(pullPx, Cfg, isPutt: false), 1e-4f);

        [Test]
        public void APutt_CapsAtOneHundredPercent_EvenAtTheBase()
        {
            Assert.AreEqual(1f, FlickPullMath.Power(648f, Cfg, isPutt: true), 1e-4f, "at the base");
            Assert.AreEqual(1f, FlickPullMath.Power(900f, Cfg, isPutt: true), 1e-4f, "and past it");
            Assert.AreEqual(0.5f, FlickPullMath.Power(290f, Cfg, isPutt: true), 1e-4f,
                            "below 100% a putt reads exactly what a swing does");
        }

        // ── The inverse ─────────────────────────────────────────────────────────

        [TestCase(0.25f)]
        [TestCase(0.5f)]
        [TestCase(1f)]
        [TestCase(1.2f)]
        public void PullPxForPower_RoundTrips(float power)
        {
            float pull = FlickPullMath.PullPxForPower(power, Cfg, isPutt: false);
            Assert.AreEqual(power, FlickPullMath.Power(pull, Cfg, isPutt: false), 1e-4f,
                            $"{power:P0} -> {pull:F2}px -> back");
        }

        /// <summary>The one place the inverse is deliberately NOT the pointwise inverse: the dead
        /// zone is a flat segment, so power 0 maps back to the rest — where the club is drawn —
        /// rather than to the far end of the dead zone, which would hang the club 40px below a
        /// finger that has not moved.</summary>
        [Test]
        public void AtZeroPower_TheClubIsDrawnAtTheRest_NotAtTheDeadZoneEdge()
            => Assert.AreEqual(0f, FlickPullMath.PullPxForPower(0f, Cfg, isPutt: false), 1e-4f);

        [Test]
        public void OnAPutt_TheInverseStopsAtTheHundredPercentPull()
            => Assert.AreEqual(Cfg.FlickPull100Px,
                               FlickPullMath.PullPxForPower(1.2f, Cfg, isPutt: true), 1e-4f);

        // ── Where the club is DRAWN, cone-local ─────────────────────────────────

        /// <summary>Tolerance is SUB-PIXEL, not loose: <c>FlickHandleStartY01</c> is 0.8182 and
        /// 648/792 is 0.81818…, so the rest lands 0.014px past the 120% pull and every drawn
        /// position inherits that. It is the same rounding the config identity below allows ±1 for,
        /// and it is a fifth of a thousandth of a screen.</summary>
        [Test]
        public void TheDrawnClub_MeetsTheFinger_AtZero_OneHundred_AndOneTwenty()
        {
            float h    = Cfg.FlickConeHeightPx;
            float rest = FlickPullMath.RestYPx(h, Cfg);

            Assert.AreEqual(rest,  FlickPullMath.ConeLocalYForPower(0f,   rest, h, Cfg, false), 1e-4f, "0%");
            Assert.AreEqual(108f,  FlickPullMath.ConeLocalYForPower(1f,   rest, h, Cfg, false), 0.05f, "100%");
            Assert.AreEqual(0f,    FlickPullMath.ConeLocalYForPower(1.2f, rest, h, Cfg, false), 0.05f, "120% = the base");
        }

        [Test]
        public void TheDrawnClub_NeverLeavesTheCone()
        {
            float h    = Cfg.FlickConeHeightPx;
            float rest = FlickPullMath.RestYPx(h, Cfg);
            for (float p = -0.5f; p <= 2f; p += 0.05f)
            {
                float y = FlickPullMath.ConeLocalYForPower(p, rest, h, Cfg, false);
                Assert.That(y, Is.InRange(0f, h), $"power {p:F2} drew the club at cone-local y {y:F2}");
            }
        }

        [Test]
        public void TheMarks_AreDerivedFromTheThresholds_NotAuthored()
        {
            Assert.AreEqual(108f, FlickPullMath.Mark100YPx(Cfg), 1e-4f,
                            "Pull120 - Pull100 above the base");
            Assert.AreEqual(0f,   FlickPullMath.Mark120YPx(Cfg), 1e-4f, "the base IS 120%");
            Assert.AreEqual(FlickPullMath.ConeLocalYForPower(1f, FlickPullMath.RestYPx(Cfg.FlickConeHeightPx, Cfg),
                                                            Cfg.FlickConeHeightPx, Cfg, false),
                            FlickPullMath.Mark100YPx(Cfg), 0.05f,
                            "the 100% LABEL is at the height the club lands at 100% — one number, " +
                            "not two (0.014px apart, the FlickHandleStartY01 rounding)");
        }

        // ── The identities that tie the config together ─────────────────────────

        [Test]
        public void OneTwentyIsExactlyOnePointTwoTimesOneHundred()
            => Assert.AreEqual(1.2f * Cfg.FlickPull100Px, Cfg.FlickPull120Px, 1e-3f);

        [Test]
        public void TheClubsRestFraction_TimesTheConeHeight_IsTheOneTwentyPull()
            => Assert.AreEqual(Cfg.FlickPull120Px,
                               Cfg.FlickHandleStartY01 * Cfg.FlickConeHeightPx, 1f,
                               "FlickHandleStartY01 x FlickConeHeightPx must BE FlickPull120Px — " +
                               "that identity is what makes the cone's base read 120%. Re-cut the " +
                               "cone and this key has to be re-derived as Pull120 / newHeight.");

        [Test]
        public void ThePullTravel_MatchesTheOtherSchemes()
        {
            Assert.AreEqual(Cfg.PendulumPull100Px,  Cfg.FlickPull100Px, 1e-3f, "vs Pendulum");
            Assert.AreEqual(Cfg.FreeSwingPull100Px, Cfg.FlickPull100Px, 1e-3f, "vs Free Swing");
            Assert.AreEqual(Cfg.MinUsefulPullPx,    Cfg.PendulumMinUsefulPullPx, 1e-3f,
                            "the dead zone is the shared MinUsefulPullPx, not a fifth copy of 40");
        }

        /// <summary>Overpower is <c>SetExternalPower</c>'s ceiling, and this mapping is what feeds
        /// it. A pull that produced more than the controller will accept would silently clip.</summary>
        [Test]
        public void TheMappingsCeiling_IsTheControllersCeiling()
            => Assert.AreEqual(ShotController.MaxOverpowerNormalized,
                               FlickPullMath.Power(1e6f, Cfg, isPutt: false), 1e-4f);

        /// <summary>A retune has to move BOTH ends together — the table above is pinned to the
        /// shipped numbers, so this is the guard that the shape, not the constants, is the rule.</summary>
        [Test]
        public void ARetunedThreshold_MovesTheHundredPercentPointWithIt()
        {
            var cfg = ControlsConfig.Default;
            cfg.FlickPull100Px = 400f;
            cfg.FlickPull120Px = 480f;

            Assert.AreEqual(1f,   FlickPullMath.Power(400f, cfg, false), 1e-4f);
            Assert.AreEqual(1.2f, FlickPullMath.Power(480f, cfg, false), 1e-4f);
            Assert.AreEqual(80f,  FlickPullMath.Mark100YPx(cfg), 1e-4f,
                            "and the drawn 100% label moves with the power it names");
        }
    }
}
