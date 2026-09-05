using NUnit.Framework;
using UnityEngine;
using Golfin.Gameplay.Config;
using Golfin.Gameplay.Input;
using Golfin.Gameplay.UI.Controls.Pendulum;

namespace Golfin.Gameplay.Tests
{
    /// <summary>
    /// scheme_pendulum §5.1 — the grade IS the scheme, so it is tested as a table rather than
    /// through a scene. Every case names a number the SPEC states, so a retune that moves a
    /// threshold has to move a test line with it.
    /// </summary>
    [TestFixture]
    public class PendulumMathTests
    {
        private ControlsConfig _cfg;

        /// <summary>The power at which <c>WindowScaleForPower</c> is exactly 1.0, solved from the
        /// shipped seeds. The window/grade table below is about ACCURACY, so it is evaluated at
        /// the neutral power — otherwise every one of those numbers would silently be testing the
        /// power shrink instead, and a retune of the shrink would break tests about accuracy.</summary>
        private float UnityPower1;

        [SetUp]
        public void SetUp()
        {
            _cfg = ControlsConfig.Default;
            float a = _cfg.PendulumWindowScaleAtZeroPower, b = _cfg.PendulumWindowScaleAtMaxPower;
            UnityPower1 = (a - 1f) / (a - b) * ShotController.MaxOverpowerNormalized;
            Assert.AreEqual(1f, PendulumMath.WindowScaleForPower(UnityPower1, _cfg), 1e-5f,
                "fixture: UnityPower1 must be the neutral-scale power");
        }

        // ── Power ────────────────────────────────────────────────────────────────

        [Test]
        public void Power_BelowTheDeadZone_IsZero()
        {
            Assert.AreEqual(0f, PendulumMath.Power(0f,  _cfg, false), 1e-6f);
            Assert.AreEqual(0f, PendulumMath.Power(_cfg.PendulumMinUsefulPullPx - 1f, _cfg, false), 1e-6f,
                "one px inside the dead zone");
        }

        [Test]
        public void Power_AtTheDeadZoneEdge_IsZeroAndRisesFromThere()
        {
            Assert.AreEqual(0f, PendulumMath.Power(_cfg.PendulumMinUsefulPullPx, _cfg, false), 1e-6f,
                "the dead-zone edge is the zero point");
            Assert.Greater(PendulumMath.Power(_cfg.PendulumMinUsefulPullPx + 1f, _cfg, false), 0f);
        }

        [Test]
        public void Power_At100Tick_IsExactlyOne()
        {
            // The lane's gold tick is drawn at PendulumPull100Px. Pulling to the line must read
            // 100% or the line is a lie — this is the assertion that ties the two together.
            Assert.AreEqual(1f, PendulumMath.Power(_cfg.PendulumPull100Px, _cfg, false), 1e-6f);
        }

        [Test]
        public void Power_MidPull_IsLinearAcrossTheDeadZone()
        {
            // Derived, not hard-coded: the midpoint of the useful span is 50% whatever it is.
            float mid = (_cfg.PendulumMinUsefulPullPx + _cfg.PendulumPull100Px) * 0.5f;
            Assert.AreEqual(0.5f, PendulumMath.Power(mid, _cfg, false), 1e-6f);
        }

        [Test]
        public void Power_BetweenTheTicks_RampsToOnePointTwo()
        {
            float midOver = (_cfg.PendulumPull100Px + _cfg.PendulumPull120Px) * 0.5f;
            Assert.AreEqual(1.1f, PendulumMath.Power(midOver, _cfg, false), 1e-5f, "halfway up the overpower ramp");
            Assert.AreEqual(1.2f, PendulumMath.Power(_cfg.PendulumPull120Px, _cfg, false), 1e-5f);
        }

        [Test]
        public void Power_PastThe120Tick_IsCappedAtOnePointTwo()
        {
            Assert.AreEqual(1.2f, PendulumMath.Power(_cfg.PendulumPull120Px + 40f, _cfg, false), 1e-5f);
            Assert.AreEqual(1.2f, PendulumMath.Power(4000f, _cfg, false), 1e-5f);
        }

        [Test]
        public void Power_OnAPutt_NeverExceedsOne()
        {
            Assert.AreEqual(1f, PendulumMath.Power(_cfg.PendulumPull100Px, _cfg, true), 1e-6f);
            Assert.AreEqual(1f, PendulumMath.Power(_cfg.PendulumPull120Px, _cfg, true), 1e-6f);
            Assert.AreEqual(1f, PendulumMath.Power(4000f, _cfg, true), 1e-6f);
            float puttMid = (_cfg.PendulumMinUsefulPullPx + _cfg.PendulumPull100Px) * 0.5f;
            Assert.AreEqual(0.5f, PendulumMath.Power(puttMid, _cfg, true), 1e-6f, "below 100% is unchanged");
        }

        // ── Marker speed ─────────────────────────────────────────────────────────

        [Test]
        public void Hz_UsesThePendulumsOwnLine_NotTheFlicksArrow()
        {
            // Base 1.0, slope −0.015 — HALF the flick's arrow, after Cesar reviewed the first clip.
            Assert.AreEqual(1.000f, PendulumMath.Hz(0f,  1f, 0f, false, _cfg), 1e-5f);
            Assert.AreEqual(0.625f, PendulumMath.Hz(25f, 1f, 0f, false, _cfg), 1e-5f);

            // And it is genuinely INDEPENDENT: moving the flick's arrow must not move the marker.
            var faster = _cfg;
            faster.BaseArrowSpeedHzAtCC0 = 9f;
            faster.ArrowSpeedHzPerCC     = 0f;
            Assert.AreEqual(PendulumMath.Hz(0f, 1f, 0f, false, _cfg),
                            PendulumMath.Hz(0f, 1f, 0f, false, faster), 1e-6f,
                "the marker must not be reachable from the flick's arrow constants");
        }

        [Test]
        public void Hz_IsSlowEnoughToTrack()
        {
            // The readability bar Cesar set: a full sweep at 1.82 Hz (0.55 s round trip) was
            // "way too fast". At CC 0..50 the marker must stay at or under 1 Hz.
            for (float cc = 0f; cc <= 50f; cc += 10f)
                Assert.LessOrEqual(PendulumMath.Hz(cc, 1f, 0f, false, _cfg), 1.0f + 1e-5f, $"cc {cc}");
        }

        [Test]
        public void Hz_IsFlooredSoTheMarkerCanNeverStopOrReverse()
        {
            // Past CC = Base/|slope| = 66.7 the raw line goes negative. PendulumMinHz = 0.35.
            Assert.AreEqual(_cfg.PendulumMinHz, PendulumMath.Hz(100f, 1f, 0f, false, _cfg), 1e-5f);
            Assert.AreEqual(_cfg.PendulumMinHz, PendulumMath.Hz(120f, 1f, 0f, false, _cfg), 1e-5f,
                "clamped CC — 120 reads as 100");
            Assert.Greater(PendulumMath.Hz(100f, 1f, 0f, false, _cfg), 0f);
        }

        [Test]
        public void Hz_Overpower_SpeedsTheMarkerUpAndStrengthBuysItBack()
        {
            float clean   = PendulumMath.Hz(0f, 1.0f, 0f,    false, _cfg);
            float weak120 = PendulumMath.Hz(0f, 1.2f, 0f,    false, _cfg);
            float strong120 = PendulumMath.Hz(0f, 1.2f, 0.75f, false, _cfg);

            Assert.AreEqual(clean * 1.2f, weak120, 1e-5f,
                "gain 1.0 at 120% pull on Strength 0 = +20% marker speed");
            Assert.AreEqual(clean * 1.05f, strong120, 1e-5f,
                "0.75 forgiveness (Strength 120) leaves a quarter of the penalty");
            Assert.Less(strong120, weak120, "Strength must buy the timing penalty back");
        }

        [Test]
        public void Hz_OnAPutt_IsSlowerAndImmuneToOverpower()
        {
            float putt = PendulumMath.Hz(0f, 1f, 0f, true, _cfg);
            Assert.AreEqual(_cfg.PendulumBaseHzAtCC0 * _cfg.PuttArrowSpeedMultiplier, putt, 1e-5f);
            Assert.Less(putt, PendulumMath.Hz(0f, 1f, 0f, false, _cfg), "putts must be slower");
            Assert.AreEqual(putt, PendulumMath.Hz(0f, 1.2f, 0f, true, _cfg), 1e-6f,
                "a putt cannot overpower, so it cannot be sped up by overpower either");
        }

        // ── Windows ──────────────────────────────────────────────────────────────

        [Test]
        public void JustWindow_LerpsWithClubAccuracy()
        {
            Assert.AreEqual(0.08f, PendulumMath.JustWindow01(0f,   UnityPower1, _cfg), 1e-5f);
            Assert.AreEqual(0.14f, PendulumMath.JustWindow01(0.5f, UnityPower1, _cfg), 1e-5f);
            Assert.AreEqual(0.20f, PendulumMath.JustWindow01(1f,   UnityPower1, _cfg), 1e-5f);
        }

        [Test]
        public void GoodWindow_IsAlwaysStrictlyWiderThanJust()
        {
            for (float a = 0f; a <= 1.001f; a += 0.1f)
                Assert.Greater(PendulumMath.GoodWindow01(a, UnityPower1, _cfg), PendulumMath.JustWindow01(a, UnityPower1, _cfg),
                    $"acc {a:F1}");

            // And it holds even when a retune makes the fixed GOOD narrower than the lerped JUST,
            // which would otherwise draw a green band wider than the amber one around it.
            var broken = _cfg;
            broken.PendulumGoodWindow01 = 0.05f;
            Assert.Greater(PendulumMath.GoodWindow01(1f, UnityPower1, broken), PendulumMath.JustWindow01(1f, UnityPower1, broken));
        }

        // ── Power shrinks the target ─────────────────────────────────────────────

        [Test]
        public void WindowScale_ShrinksMonotonicallyAsThePullDeepens()
        {
            float prev = float.MaxValue;
            for (float p = 0f; p <= ShotController.MaxOverpowerNormalized + 1e-4f; p += 0.1f)
            {
                float scale = PendulumMath.WindowScaleForPower(p, _cfg);
                Assert.Less(scale, prev, $"power {p:F1} must be tighter than the step before it");
                prev = scale;
            }
        }

        [Test]
        public void WindowScale_HitsItsSeedsAtTheEnds()
        {
            Assert.AreEqual(_cfg.PendulumWindowScaleAtZeroPower,
                            PendulumMath.WindowScaleForPower(0f, _cfg), 1e-5f);
            Assert.AreEqual(_cfg.PendulumWindowScaleAtMaxPower,
                            PendulumMath.WindowScaleForPower(ShotController.MaxOverpowerNormalized, _cfg), 1e-5f);
            // A driver that somehow published past the ceiling must not invert the window.
            Assert.AreEqual(_cfg.PendulumWindowScaleAtMaxPower,
                            PendulumMath.WindowScaleForPower(99f, _cfg), 1e-5f);
            Assert.Greater(PendulumMath.WindowScaleForPower(99f, _cfg), 0f, "never zero or negative");
        }

        [Test]
        public void BothWindows_ShrinkWithPower()
        {
            const float acc = 0.5f;
            Assert.Less(PendulumMath.JustWindow01(acc, 1.2f, _cfg),
                        PendulumMath.JustWindow01(acc, 0.3f, _cfg),
                        "a 120% pull must be a narrower JUST than a lay-up");
            Assert.Less(PendulumMath.GoodWindow01(acc, 1.2f, _cfg),
                        PendulumMath.GoodWindow01(acc, 0.3f, _cfg));
        }

        [Test]
        public void GoodStaysWiderThanJust_AtEveryPower()
        {
            for (float p = 0f; p <= 1.2f + 1e-4f; p += 0.1f)
                for (float a = 0f; a <= 1.001f; a += 0.25f)
                    Assert.Greater(PendulumMath.GoodWindow01(a, p, _cfg),
                                   PendulumMath.JustWindow01(a, p, _cfg), $"power {p:F1} acc {a:F2}");
        }

        [Test]
        public void TheSameRelease_IsAJustOnALayUpAndAMissAtFullPower()
        {
            // The whole point of the shrink, as one assertion: identical timing, different risk.
            const float m = 0.14f;
            Assert.AreEqual(PendulumGrade.Just,
                PendulumMath.Grade(m, 0.5f, 0.2f, HalfCone, _cfg).Grade, "gentle pull forgives it");
            Assert.AreNotEqual(PendulumGrade.Just,
                PendulumMath.Grade(m, 0.5f, 1.2f, HalfCone, _cfg).Grade, "a 120% pull does not");
        }

        // ── Grade ────────────────────────────────────────────────────────────────

        private const float HalfCone = 0.2f;   // rad; a readable stand-in for ConeHalfAngleDeg

        [Test]
        public void Grade_DeadCentre_IsAPerfectJust()
        {
            var v = PendulumMath.Grade(0f, 0.5f, UnityPower1, HalfCone, _cfg);
            Assert.AreEqual(PendulumGrade.Just, v.Grade);
            Assert.AreEqual(0f, v.ErrorYawRad, 1e-6f, "a JUST goes exactly where it is aimed");
            Assert.AreEqual(1f, v.TimingMul,   1e-6f, "and pays nothing");
            Assert.AreEqual(1f, v.Timing01,    1e-6f);
        }

        [Test]
        public void Grade_OnTheJustEdge_IsStillJust_JustOutsideIsGood()
        {
            float just = PendulumMath.JustWindow01(0.5f, UnityPower1, _cfg);   // 0.14

            Assert.AreEqual(PendulumGrade.Just, PendulumMath.Grade( just, 0.5f, UnityPower1, HalfCone, _cfg).Grade);
            Assert.AreEqual(PendulumGrade.Just, PendulumMath.Grade(-just, 0.5f, UnityPower1, HalfCone, _cfg).Grade);
            Assert.AreEqual(PendulumGrade.Good, PendulumMath.Grade(just + 0.001f, 0.5f, UnityPower1, HalfCone, _cfg).Grade);
        }

        [Test]
        public void Grade_OnTheGoodEdge_IsStillGood_JustOutsideIsMiss()
        {
            float good = PendulumMath.GoodWindow01(0.5f, UnityPower1, _cfg);   // 0.45

            Assert.AreEqual(PendulumGrade.Good, PendulumMath.Grade( good, 0.5f, UnityPower1, HalfCone, _cfg).Grade);
            Assert.AreEqual(PendulumGrade.Good, PendulumMath.Grade(-good, 0.5f, UnityPower1, HalfCone, _cfg).Grade);
            Assert.AreEqual(PendulumGrade.Miss, PendulumMath.Grade(good + 0.001f, 0.5f, UnityPower1, HalfCone, _cfg).Grade);
        }

        [Test]
        public void Grade_Good_BendsProportionallyAndCostsTheGoldMultiplier()
        {
            var v = PendulumMath.Grade(0.3f, 0.5f, UnityPower1, HalfCone, _cfg);
            Assert.AreEqual(PendulumGrade.Good, v.Grade);
            Assert.AreEqual(0.3f * HalfCone, v.ErrorYawRad, 1e-6f);
            Assert.AreEqual(_cfg.TimingPowerMulGold, v.TimingMul, 1e-6f);
            Assert.AreEqual(0.7f, v.Timing01, 1e-6f);
        }

        [Test]
        public void Grade_Miss_IsThrownPastTheConeAndCostsTheRedMultiplier()
        {
            var v = PendulumMath.Grade(1f, 0.5f, UnityPower1, HalfCone, _cfg);
            Assert.AreEqual(PendulumGrade.Miss, v.Grade);
            Assert.AreEqual(HalfCone * _cfg.PendulumMissYawGain, v.ErrorYawRad, 1e-6f);
            Assert.AreEqual(_cfg.TimingPowerMulRed, v.TimingMul, 1e-6f);
            Assert.AreEqual(0f, v.Timing01, 1e-6f);
        }

        [Test]
        public void Grade_SignConvention_MarkerRightSendsTheBallRight()
        {
            // ShotController.AimYawFor is CameraHeading + finetune * halfCone, and a POSITIVE
            // finetune is the handle pushed right — so a positive yaw is the ball's right. A
            // marker right of the pip must therefore produce a POSITIVE error yaw, or the miss
            // reads backwards from what the player watched.
            Assert.Greater(PendulumMath.Grade( 0.3f, 0.5f, UnityPower1, HalfCone, _cfg).ErrorYawRad, 0f);
            Assert.Less   (PendulumMath.Grade(-0.3f, 0.5f, UnityPower1, HalfCone, _cfg).ErrorYawRad, 0f);
            Assert.Greater(PendulumMath.Grade( 1.0f, 0.5f, UnityPower1, HalfCone, _cfg).ErrorYawRad, 0f);
            Assert.Less   (PendulumMath.Grade(-1.0f, 0.5f, UnityPower1, HalfCone, _cfg).ErrorYawRad, 0f);

            // Symmetric in magnitude — the scheme has no favoured side.
            Assert.AreEqual(PendulumMath.Grade(0.3f, 0.5f, UnityPower1, HalfCone, _cfg).ErrorYawRad,
                           -PendulumMath.Grade(-0.3f, 0.5f, UnityPower1, HalfCone, _cfg).ErrorYawRad, 1e-6f);
        }

        [Test]
        public void Grade_WiderAccuracy_TurnsAGoodIntoAJust()
        {
            // The same release, two clubs: this is Club Accuracy doing its job in this scheme.
            Assert.AreEqual(PendulumGrade.Good, PendulumMath.Grade(0.17f, 0f, UnityPower1, HalfCone, _cfg).Grade);
            Assert.AreEqual(PendulumGrade.Just, PendulumMath.Grade(0.17f, 1f, UnityPower1, HalfCone, _cfg).Grade);
        }

        [Test]
        public void Grade_ClampsAnOutOfRangeMarker()
        {
            var v = PendulumMath.Grade(5f, 0.5f, UnityPower1, HalfCone, _cfg);
            Assert.AreEqual(0f, v.Timing01, 1e-6f, "|m| is clamped to 1, so timing01 floors at 0");
            Assert.AreEqual(HalfCone * _cfg.PendulumMissYawGain, v.ErrorYawRad, 1e-6f);
        }

        // ── Marker motion ────────────────────────────────────────────────────────

        [Test]
        public void MarkerAt_IsSinusoidalAndStartsAtCentre()
        {
            Assert.AreEqual( 0f, PendulumMath.MarkerAt(0f),    1e-6f);
            Assert.AreEqual( 1f, PendulumMath.MarkerAt(0.25f), 1e-5f, "quarter cycle = right end");
            Assert.AreEqual( 0f, PendulumMath.MarkerAt(0.5f),  1e-5f);
            Assert.AreEqual(-1f, PendulumMath.MarkerAt(0.75f), 1e-5f, "three quarters = left end");
            Assert.AreEqual( 0f, PendulumMath.MarkerAt(1f),    1e-5f, "one full sweep");
        }

        [Test]
        public void MarkerAt_IsSlowestAtTheEnds_TheWholeReasonItIsNotATriangle()
        {
            // Δ over the same phase step, at the end vs through the middle. If these were equal
            // the marker would be linear and the centre pip would be no harder than the edge.
            const float step = 0.01f;
            float atEnd    = Mathf.Abs(PendulumMath.MarkerAt(0.25f + step) - PendulumMath.MarkerAt(0.25f));
            float atCentre = Mathf.Abs(PendulumMath.MarkerAt(0f    + step) - PendulumMath.MarkerAt(0f));
            Assert.Less(atEnd, atCentre * 0.25f, "the ends must visibly dwell");
        }

        [Test]
        public void GradeKey_IsAlwaysALocalisationKey_NeverALiteral()
        {
            Assert.AreEqual("SHOT_GRADE_JUST", PendulumMath.GradeKey(PendulumGrade.Just));
            Assert.AreEqual("SHOT_GRADE_GOOD", PendulumMath.GradeKey(PendulumGrade.Good));
            Assert.AreEqual("SHOT_GRADE_MISS", PendulumMath.GradeKey(PendulumGrade.Miss));
        }
    }
}
