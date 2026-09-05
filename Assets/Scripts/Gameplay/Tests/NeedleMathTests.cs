using NUnit.Framework;
using UnityEngine;
using Golfin.Gameplay.Config;
using Golfin.Gameplay.Input;
using Golfin.Gameplay.UI.Controls.Needle;

namespace Golfin.Gameplay.Tests
{
    /// <summary>
    /// scheme_needle §5.1 — the whole of <see cref="NeedleMath"/>, which is the whole of the
    /// scheme's decision-making. Nothing here needs a scene, a canvas or a frame, which is exactly
    /// why the maths was split out of the driver.
    /// </summary>
    [TestFixture]
    public class NeedleMathTests
    {
        private ControlsConfig _cfg;
        private const float HalfCone = 0.3f;      // radians; a stand-in for ConeHalfAngleDeg

        [SetUp]
        public void SetUp() => _cfg = ControlsConfig.Default;

        // ── Power ────────────────────────────────────────────────────────────────

        [Test]
        public void Power_IsZeroInsideTheDeadZone()
        {
            Assert.AreEqual(0f, NeedleMath.Power(0f, _cfg, false), 1e-6f);
            Assert.AreEqual(0f, NeedleMath.Power(_cfg.NeedleMinUsefulPullPx - 1f, _cfg, false), 1e-6f,
                "39px is inside the 40px dead zone");
        }

        [Test]
        public void Power_RampsLinearlyToOneHundredPercent()
        {
            Assert.AreEqual(0f, NeedleMath.Power(_cfg.NeedleMinUsefulPullPx, _cfg, false), 1e-6f,
                "the dead-zone edge is exactly 0%");
            Assert.AreEqual(1f, NeedleMath.Power(_cfg.NeedlePull100Px, _cfg, false), 1e-4f);

            float mid = (_cfg.NeedleMinUsefulPullPx + _cfg.NeedlePull100Px) * 0.5f;
            Assert.AreEqual(0.5f, NeedleMath.Power(mid, _cfg, false), 1e-4f,
                "the midpoint of the useful span is half power");
        }

        [Test]
        public void Power_At80Ring_IsWhatTheRingClaims()
        {
            // The 80% ring is drawn at NeedlePull80Px, so pulling to it has to read ~80%. It is
            // not exactly 0.80 because the dead zone eats the first 40px of the ramp — which is
            // the honest number, and the reason this test states it rather than asserting 0.8.
            float expected = (_cfg.NeedlePull80Px - _cfg.NeedleMinUsefulPullPx) /
                             (_cfg.NeedlePull100Px - _cfg.NeedleMinUsefulPullPx);
            Assert.AreEqual(expected, NeedleMath.Power(_cfg.NeedlePull80Px, _cfg, false), 1e-4f);
            Assert.That(NeedleMath.Power(_cfg.NeedlePull80Px, _cfg, false), Is.InRange(0.7f, 0.85f));
        }

        [Test]
        public void Power_OverpowersToTwelveTenthsAndClamps()
        {
            Assert.AreEqual(1.2f, NeedleMath.Power(_cfg.NeedlePull120Px, _cfg, false), 1e-4f);
            Assert.AreEqual(ShotController.MaxOverpowerNormalized,
                            NeedleMath.Power(500f, _cfg, false), 1e-4f,
                "past the 120% ring the number stops — there is nothing further to pull to");
        }

        [Test]
        public void Power_OnAPutt_CapsAtOneHundredPercent()
        {
            Assert.AreEqual(1f, NeedleMath.Power(_cfg.NeedlePull100Px, _cfg, true), 1e-4f);
            Assert.AreEqual(1f, NeedleMath.Power(_cfg.NeedlePull120Px, _cfg, true), 1e-4f,
                "a putt draws no 120% ring, so there is nothing to pull past");
            Assert.AreEqual(1f, NeedleMath.Power(500f, _cfg, true), 1e-4f);
        }

        // ── Sweep speed ──────────────────────────────────────────────────────────

        [Test]
        public void Sweep_IsTrackableByEyeAtClubControlZero()
        {
            // Cesar's Pendulum note, made an assertion: a timing element the player cannot follow
            // is not a timing element. One full pass, at the worst stat, at full power.
            float s = NeedleMath.SweepSeconds(0f, 1f, 0f, false, _cfg);
            Assert.GreaterOrEqual(s, 1.0f, "one sweep at CC 0 must last at least a second");
            Assert.AreEqual(_cfg.NeedleSweepSecAtCC0, s, 1e-4f);
        }

        [Test]
        public void Sweep_SlowsWithClubControl()
        {
            float cc0   = NeedleMath.SweepSeconds(0f,   1f, 0f, false, _cfg);
            float cc50  = NeedleMath.SweepSeconds(50f,  1f, 0f, false, _cfg);
            float cc100 = NeedleMath.SweepSeconds(100f, 1f, 0f, false, _cfg);

            Assert.Less(cc0, cc50);
            Assert.Less(cc50, cc100);
            Assert.AreEqual(_cfg.NeedleSweepSecAtCC0 + 100f * _cfg.NeedleSweepSecPerCC, cc100, 1e-4f);
        }

        [Test]
        public void Sweep_ClampsClubControlAtOneHundred()
        {
            Assert.AreEqual(NeedleMath.SweepSeconds(100f, 1f, 0f, false, _cfg),
                            NeedleMath.SweepSeconds(120f, 1f, 0f, false, _cfg), 1e-6f,
                "Club Control past 100 must not keep buying time");
        }

        [Test]
        public void Sweep_IsFasterWhenOverpowered_AndStrengthBuysItBack()
        {
            float clean   = NeedleMath.SweepSeconds(0f, 1.0f, 0f,    false, _cfg);
            float over    = NeedleMath.SweepSeconds(0f, 1.2f, 0f,    false, _cfg);
            float forgive = NeedleMath.SweepSeconds(0f, 1.2f, 0.75f, false, _cfg);

            Assert.Less(over, clean, "a 120% pull costs timing");
            Assert.Greater(forgive, over, "and Strength buys most of it back");
            Assert.Less(forgive, clean,  "but never all of it");
            Assert.AreEqual(clean / 1.2f, over, 1e-4f, "gain 1.0 at 120% = a 1.2x speed-up");
        }

        [Test]
        public void Sweep_OnAPutt_IsSlowerAndIgnoresOverpower()
        {
            float swing = NeedleMath.SweepSeconds(0f, 1f, 0f, false, _cfg);
            float putt  = NeedleMath.SweepSeconds(0f, 1f, 0f, true,  _cfg);
            Assert.Greater(putt, swing, "PuttArrowSpeedMultiplier is shared and slows the needle");
            Assert.AreEqual(swing / _cfg.PuttArrowSpeedMultiplier, putt, 1e-4f);

            Assert.AreEqual(putt, NeedleMath.SweepSeconds(0f, 1.2f, 0f, true, _cfg), 1e-6f,
                "a putt never overpowers, so it never pays the speed-up");
        }

        [Test]
        public void Sweep_HasAFloor()
        {
            var fast = _cfg;
            fast.NeedleSweepSecAtCC0 = 0.2f;      // a retune below the floor
            fast.NeedleSweepSecPerCC = -0.01f;    // and an inverted slope
            Assert.AreEqual(fast.NeedleMinSweepSec,
                            NeedleMath.SweepSeconds(100f, 1f, 0f, false, fast), 1e-4f,
                "the floor is what stops a mis-tuned line running the needle arbitrarily fast");
        }

        // ── Zones ────────────────────────────────────────────────────────────────

        [Test]
        public void Zones_WidenWithClubAccuracy()
        {
            float acc0 = NeedleMath.PerfectZone01(0f,   1f, _cfg);
            float acc1 = NeedleMath.PerfectZone01(1f,   1f, _cfg);
            Assert.Less(acc0, acc1, "Accuracy is timing tolerance in this scheme");
            Assert.AreEqual(_cfg.NeedlePerfectZoneAtAcc0_01   * NeedleMath.WindowScaleForPower(1f, _cfg), acc0, 1e-5f);
            Assert.AreEqual(_cfg.NeedlePerfectZoneAtAcc120_01 * NeedleMath.WindowScaleForPower(1f, _cfg), acc1, 1e-5f);
        }

        [Test]
        public void Zones_ShrinkAsThePullDeepens()
        {
            foreach (float acc in new[] { 0f, 0.5f, 1f })
            {
                float p0   = NeedleMath.PerfectZone01(acc, 0f,   _cfg);
                float p1   = NeedleMath.PerfectZone01(acc, 1f,   _cfg);
                float p12  = NeedleMath.PerfectZone01(acc, 1.2f, _cfg);
                Assert.Greater(p0, p1,  $"acc {acc}: a lay-up is more forgiving than a full swing");
                Assert.Greater(p1, p12, $"acc {acc}: and a full swing than an overpowered one");

                float g0  = NeedleMath.GoodZone01(acc, 0f,   _cfg);
                float g12 = NeedleMath.GoodZone01(acc, 1.2f, _cfg);
                Assert.Greater(g0, g12, $"acc {acc}: the amber zone shrinks too");
            }
        }

        [Test]
        public void Zones_ScaleEndpointsAreTheConfiguredOnes()
        {
            Assert.AreEqual(_cfg.NeedleWindowScaleAtZeroPower,
                            NeedleMath.WindowScaleForPower(0f, _cfg), 1e-5f);
            Assert.AreEqual(_cfg.NeedleWindowScaleAtMaxPower,
                            NeedleMath.WindowScaleForPower(ShotController.MaxOverpowerNormalized, _cfg), 1e-5f);
        }

        [Test]
        public void GoodZone_StaysStrictlyWiderThanPerfect()
        {
            var squeezed = _cfg;
            squeezed.NeedleGoodZone01 = 0.01f;                 // narrower than PERFECT
            squeezed.NeedlePerfectZoneAtAcc0_01   = 0.30f;
            squeezed.NeedlePerfectZoneAtAcc120_01 = 0.30f;

            float perfect = NeedleMath.PerfectZone01(0.5f, 1f, squeezed);
            float good    = NeedleMath.GoodZone01(0.5f, 1f, squeezed);
            Assert.Greater(good, perfect,
                "an amber zone inside the blue one would draw as a stripe nobody can land in, " +
                "and Grade would silently never return a small hook or slice");
        }

        // ── Grade ────────────────────────────────────────────────────────────────

        [Test]
        public void Grade_DeadCentreIsPerfect()
        {
            var v = NeedleMath.Grade(0f, 0.5f, 1f, HalfCone, _cfg);
            Assert.AreEqual(NeedleGrade.Perfect, v.Grade);
            Assert.AreEqual(0f, v.ErrorYawRad, 1e-6f, "a PERFECT is dead straight");
            Assert.AreEqual(1f, v.TimingMul,   1e-6f, "and pays no power penalty");
            Assert.AreEqual(1f, v.Timing01,    1e-6f);
        }

        [Test]
        public void Grade_AtTheBlueEdgeIsStillPerfect_JustOutsideIsNot()
        {
            float perfect = NeedleMath.PerfectZone01(0.5f, 1f, _cfg);

            Assert.AreEqual(NeedleGrade.Perfect,
                            NeedleMath.Grade(perfect, 0.5f, 1f, HalfCone, _cfg).Grade,
                            "the drawn edge is INSIDE the zone");
            Assert.AreEqual(NeedleGrade.Perfect,
                            NeedleMath.Grade(-perfect, 0.5f, 1f, HalfCone, _cfg).Grade);
            Assert.AreNotEqual(NeedleGrade.Perfect,
                            NeedleMath.Grade(perfect + 1e-3f, 0.5f, 1f, HalfCone, _cfg).Grade);
        }

        [Test]
        public void Grade_SignConvention_EarlyIsHookLeft_LateIsSliceRight()
        {
            // The whole scheme reads wrong if this is backwards. Positive ErrorYawRad is the
            // ball's RIGHT — that is ShotController.AimYawFor's convention, pinned by
            // ShotAimParityTests — so a needle right of the top (tapped LATE) must yaw positive.
            var late  = NeedleMath.Grade(+0.3f, 0.5f, 1f, HalfCone, _cfg);
            var early = NeedleMath.Grade(-0.3f, 0.5f, 1f, HalfCone, _cfg);

            Assert.AreEqual(NeedleGrade.Slice, late.Grade);
            Assert.Greater(late.ErrorYawRad, 0f, "SLICE goes RIGHT");
            Assert.AreEqual(NeedleGrade.Hook, early.Grade);
            Assert.Less(early.ErrorYawRad, 0f, "HOOK goes LEFT");
            Assert.AreEqual(-late.ErrorYawRad, early.ErrorYawRad, 1e-6f, "and they mirror");
        }

        [Test]
        public void Grade_SmallMissScalesWithTheOffset()
        {
            float perfect = NeedleMath.PerfectZone01(0.5f, 1f, _cfg);
            float good    = NeedleMath.GoodZone01(0.5f, 1f, _cfg);
            float near    = perfect + (good - perfect) * 0.1f;
            float far     = perfect + (good - perfect) * 0.9f;

            var vNear = NeedleMath.Grade(near, 0.5f, 1f, HalfCone, _cfg);
            var vFar  = NeedleMath.Grade(far,  0.5f, 1f, HalfCone, _cfg);

            Assert.AreEqual(NeedleGrade.Slice, vNear.Grade);
            Assert.AreEqual(NeedleGrade.Slice, vFar.Grade);
            Assert.Less(vNear.ErrorYawRad, vFar.ErrorYawRad,
                "just outside the blue barely bends the shot; the amber edge bends it a lot more");
            Assert.AreEqual(near * HalfCone * _cfg.NeedleYawGain, vNear.ErrorYawRad, 1e-6f);
            Assert.Less(vFar.TimingMul, vNear.TimingMul, "and costs more power the further out it is");
        }

        [Test]
        public void Grade_PastTheAmberIsAFlatBigMiss()
        {
            float good = NeedleMath.GoodZone01(0.5f, 1f, _cfg);
            var edge  = NeedleMath.Grade(good + 1e-3f, 0.5f, 1f, HalfCone, _cfg);
            var worst = NeedleMath.Grade(1f,           0.5f, 1f, HalfCone, _cfg);

            Assert.AreEqual(NeedleGrade.Slice, edge.Grade);
            Assert.AreEqual(HalfCone * _cfg.NeedleMissYawGain, edge.ErrorYawRad, 1e-6f);
            Assert.AreEqual(edge.ErrorYawRad, worst.ErrorYawRad, 1e-6f,
                "flat, not ramped: a ramp would land every worst-case tap in the same place");
            Assert.AreEqual(_cfg.TimingPowerMulGold, worst.TimingMul, 1e-6f);
            Assert.AreEqual(0f, worst.Timing01, 1e-6f);
        }

        [Test]
        public void Grade_Timing01_IsOneMinusTheOffset()
        {
            foreach (float n in new[] { -1f, -0.5f, 0f, 0.25f, 1f })
                Assert.AreEqual(1f - Mathf.Abs(n),
                                NeedleMath.Grade(n, 0.5f, 1f, HalfCone, _cfg).Timing01, 1e-6f,
                                $"timing01 at n={n}");
        }

        [Test]
        public void Grade_ClampsOutOfRangeOffsets()
        {
            Assert.AreEqual(NeedleMath.Grade(1f,  0.5f, 1f, HalfCone, _cfg).ErrorYawRad,
                            NeedleMath.Grade(9f,  0.5f, 1f, HalfCone, _cfg).ErrorYawRad, 1e-6f);
            Assert.AreEqual(0f, NeedleMath.Grade(9f, 0.5f, 1f, HalfCone, _cfg).Timing01, 1e-6f);
        }

        [Test]
        public void Shank_IsTheWorstOutcomeTheSchemeCanProduce()
        {
            var shank = NeedleMath.Shank(HalfCone, _cfg);
            var worst = NeedleMath.Grade(1f, 0.5f, 1f, HalfCone, _cfg);

            Assert.AreEqual(NeedleGrade.Shank, shank.Grade);
            Assert.AreEqual(worst.ErrorYawRad, shank.ErrorYawRad, 1e-6f, "as wide as a big slice");
            Assert.Less(shank.TimingMul, worst.TimingMul,
                "and shorter than one — 'do not tap' must never be the safe play");
            Assert.AreEqual(_cfg.TimingPowerMulRed, shank.TimingMul, 1e-6f);
            Assert.AreEqual(0f, shank.Timing01, 1e-6f);
            Assert.Greater(shank.ErrorYawRad, 0f, "the needle was at the RIGHT end when it timed out");
        }

        [Test]
        public void GradeKeys_AreKeysAndAreDistinct()
        {
            // Zero hardcoded text: what the pop shows is resolved from these, so the test asserts
            // KEYS. A word here would be the bug the rule exists to prevent.
            Assert.AreEqual("SHOT_GRADE_PERFECT", NeedleMath.GradeKey(NeedleGrade.Perfect));
            Assert.AreEqual("SHOT_GRADE_HOOK",    NeedleMath.GradeKey(NeedleGrade.Hook));
            Assert.AreEqual("SHOT_GRADE_SLICE",   NeedleMath.GradeKey(NeedleGrade.Slice));
            Assert.AreEqual("SHOT_GRADE_SHANK",   NeedleMath.GradeKey(NeedleGrade.Shank));
            Assert.AreEqual("SHOT_TAP_HINT",      NeedleMath.KeyTapHint);
        }

        // ── Colour treatment ─────────────────────────────────────────────────────

        [Test]
        public void OverTurf_ReproducesFigmasCompositeThroughUnitysLinearBlend()
        {
            // The carry-over-5 contract, stated as arithmetic: blend the corrected colour over
            // turf the way Unity does (in LINEAR) and you land on the colour Figma composites (in
            // sRGB). Without the correction the ring reads 12-16 RGB too light on the red channel.
            foreach (var (srgb, alpha) in new[]
                     {
                         ((Color32)new Color32(255, 255, 255, 255), 0.25f),   // Ring80
                         ((Color32)new Color32(0xFF, 0xD2, 0x3A, 255), 0.35f),// Ring100
                         ((Color32)new Color32(0xFF, 0x5A, 0x5A, 255), 0.25f),// Ring120
                         ((Color32)new Color32(0xFF, 0x5A, 0x5A, 255), 0.45f),// OverpowerCrescent
                     })
            {
                Color corrected = NeedleColors.OverTurf(srgb, alpha);
                Color32 turf    = NeedleColors.TurfSrgb;

                for (int ch = 0; ch < 3; ch++)
                {
                    float src  = ch == 0 ? srgb.r : ch == 1 ? srgb.g : srgb.b;
                    float back = ch == 0 ? turf.r : ch == 1 ? turf.g : turf.b;
                    float want = alpha * src + (1f - alpha) * back;             // Figma's own composite
                    float got  = Srgb(alpha * Lin(Chan(corrected, ch) * 255f) +
                                      (1f - alpha) * Lin(back));                 // Unity's
                    Assert.AreEqual(want, got, 1.5f,
                        $"{srgb} @ {alpha}, channel {ch}: linear blend must land on Figma's pixel");
                }
            }
        }

        private static float Chan(Color c, int i) => i == 0 ? c.r : i == 1 ? c.g : c.b;
        private static float Lin(float s255)
        {
            float c = Mathf.Clamp01(s255 / 255f);
            return c <= 0.04045f ? c / 12.92f : Mathf.Pow((c + 0.055f) / 1.055f, 2.4f);
        }
        private static float Srgb(float lin)
        {
            lin = Mathf.Clamp01(lin);
            return 255f * (lin <= 0.0031308f ? lin * 12.92f : 1.055f * Mathf.Pow(lin, 1f / 2.4f) - 0.055f);
        }
    }
}
