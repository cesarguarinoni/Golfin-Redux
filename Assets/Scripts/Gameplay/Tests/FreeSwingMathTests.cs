using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Golfin.Gameplay.Config;
using Golfin.Gameplay.Input;
using Golfin.Gameplay.UI.Controls.FreeSwing;

namespace Golfin.Gameplay.Tests
{
    /// <summary>
    /// scheme_freeswing §5.1 — the pure verdict, table by table.
    ///
    /// <para>Every threshold is read off <see cref="ControlsConfig"/> and never written as a
    /// literal (carry-over 6): a retune of the CSV must move what these tests assert, or they are
    /// pinning a number nobody ships. Where a literal IS asserted it is a SHAPE — a sign, a zero,
    /// an ordering — which is what a retune must never change.</para>
    /// </summary>
    [TestFixture]
    public class FreeSwingMathTests
    {
        private ControlsConfig _cfg;

        /// <summary>A driver's half-cone in radians. Any non-zero value works; a round one makes
        /// the yaw tables readable.</summary>
        private const float HalfCone = 0.20f;

        [SetUp]
        public void SetUp() => _cfg = ControlsConfig.Default;

        // ── Power ────────────────────────────────────────────────────────────────

        [Test]
        public void Power_BelowTheDeadZone_IsZero()
        {
            Assert.AreEqual(0f, FreeSwingMath.Power(_cfg.FreeSwingMinUsefulPullPx - 1f, _cfg, false), 1e-6f);
            Assert.AreEqual(0f, FreeSwingMath.Power(0f, _cfg, false), 1e-6f);
        }

        [Test]
        public void Power_RampsLinearlyToOneHundredPercent()
        {
            Assert.AreEqual(0f, FreeSwingMath.Power(_cfg.FreeSwingMinUsefulPullPx, _cfg, false), 1e-4f);
            float mid = (_cfg.FreeSwingMinUsefulPullPx + _cfg.FreeSwingPull100Px) * 0.5f;
            Assert.AreEqual(0.5f, FreeSwingMath.Power(mid, _cfg, false), 1e-4f);
            Assert.AreEqual(1f, FreeSwingMath.Power(_cfg.FreeSwingPull100Px, _cfg, false), 1e-4f);
        }

        [Test]
        public void Power_OverpowerRampsToOneTwenty_AndCapsThere()
        {
            Assert.AreEqual(1.2f, FreeSwingMath.Power(_cfg.FreeSwingPull120Px, _cfg, false), 1e-4f);
            Assert.AreEqual(ShotController.MaxOverpowerNormalized,
                            FreeSwingMath.Power(_cfg.FreeSwingPull120Px * 3f, _cfg, false), 1e-4f);
        }

        [Test]
        public void Power_PuttCapsAtOneHundredPercent_HoweverDeepThePull()
        {
            Assert.AreEqual(1f, FreeSwingMath.Power(_cfg.FreeSwingPull100Px, _cfg, true), 1e-4f);
            Assert.AreEqual(1f, FreeSwingMath.Power(_cfg.FreeSwingPull120Px * 2f, _cfg, true), 1e-4f);
        }

        [Test]
        public void Power_SeededEqualToPendulumAndNeedle_SoThePullFeelsTheSame()
        {
            // Not a coupling — three separate fields — but a claim the spec makes about day one,
            // and a claim worth failing loudly if somebody retunes one and not the others by
            // accident. Deleting this test is the correct response to a DELIBERATE divergence.
            Assert.AreEqual(_cfg.PendulumPull100Px, _cfg.FreeSwingPull100Px, 1e-4f);
            Assert.AreEqual(_cfg.NeedlePull120Px,   _cfg.FreeSwingPull120Px, 1e-4f);
        }

        // ── Window scale ─────────────────────────────────────────────────────────

        [Test]
        public void WindowScale_ShrinksMonotonicallyFromZeroPowerToMax()
        {
            float atZero = FreeSwingMath.WindowScaleForPower(0f, _cfg);
            float atOne  = FreeSwingMath.WindowScaleForPower(1f, _cfg);
            float atMax  = FreeSwingMath.WindowScaleForPower(ShotController.MaxOverpowerNormalized, _cfg);

            Assert.AreEqual(_cfg.FreeSwingWindowScaleAtZeroPower, atZero, 1e-4f);
            Assert.AreEqual(_cfg.FreeSwingWindowScaleAtMaxPower,  atMax,  1e-4f);
            Assert.Less(atOne, atZero, "a 100% pull must be tighter than a lay-up");
            Assert.Less(atMax, atOne,  "and a 120% pull tighter still — the only cost overpower carries");
        }

        // ── Impact window ────────────────────────────────────────────────────────

        [Test]
        public void ImpactWindow_WidensWithAccuracy_AtEveryPower()
        {
            foreach (float p in new[] { 0f, 1f, 1.2f })
            {
                float lo  = FreeSwingMath.ImpactWindowPx(0f,   p, _cfg);
                float mid = FreeSwingMath.ImpactWindowPx(0.5f, p, _cfg);
                float hi  = FreeSwingMath.ImpactWindowPx(1f,   p, _cfg);
                Assert.Less(lo, mid, $"power {p}");
                Assert.Less(mid, hi, $"power {p}");

                float scale = FreeSwingMath.WindowScaleForPower(p, _cfg);
                Assert.AreEqual(_cfg.FreeSwingImpactWindowAtAcc0Px   * scale, lo, 1e-3f);
                Assert.AreEqual(_cfg.FreeSwingImpactWindowAtAcc120Px * scale, hi, 1e-3f);
            }
        }

        [Test]
        public void ImpactWindow_ClosesAsThePullDeepens()
        {
            Assert.Greater(FreeSwingMath.ImpactWindowPx(0.5f, 0f,   _cfg),
                           FreeSwingMath.ImpactWindowPx(0.5f, 1f,   _cfg));
            Assert.Greater(FreeSwingMath.ImpactWindowPx(0.5f, 1f,   _cfg),
                           FreeSwingMath.ImpactWindowPx(0.5f, 1.2f, _cfg));
        }

        // ── Impact yaw ───────────────────────────────────────────────────────────

        [Test]
        public void ImpactYaw_IsZeroInsideTheWindow_AndAtItsEdge()
        {
            float w = FreeSwingMath.ImpactWindowPx(0.5f, 1f, _cfg);
            Assert.AreEqual(0f, FreeSwingMath.ImpactYawRad(0f,  0.5f, 1f, HalfCone, _cfg), 1e-6f);
            Assert.AreEqual(0f, FreeSwingMath.ImpactYawRad( w,  0.5f, 1f, HalfCone, _cfg), 1e-6f);
            Assert.AreEqual(0f, FreeSwingMath.ImpactYawRad(-w,  0.5f, 1f, HalfCone, _cfg), 1e-6f);
        }

        [Test]
        public void ImpactYaw_JustOutsideTheWindow_BendsALittleAndTheRightWay()
        {
            float w = FreeSwingMath.ImpactWindowPx(0.5f, 1f, _cfg);
            float right = FreeSwingMath.ImpactYawRad( w + 1f, 0.5f, 1f, HalfCone, _cfg);
            float left  = FreeSwingMath.ImpactYawRad(-w - 1f, 0.5f, 1f, HalfCone, _cfg);

            // SIGN: crossing RIGHT of centre yaws positive, which AimYawFor sends right = SLICE.
            Assert.Greater(right, 0f, "crossing right must send the ball right (SLICE)");
            Assert.Less(left,    0f, "crossing left must send the ball left (HOOK)");
            Assert.AreEqual(-left, right, 1e-5f, "and the two must mirror exactly");
            Assert.Less(Mathf.Abs(right), HalfCone, "a hair outside is a hair off line, not a shank");
        }

        [Test]
        public void ImpactYaw_AtTheMissThreshold_IsExactlyOneConeHalfAngle()
        {
            // The gain's whole definition: xI = ImpactMissPx bends the shot one half-cone.
            float atMiss = FreeSwingMath.ImpactYawRad(_cfg.FreeSwingImpactMissPx, 0.5f, 1f, HalfCone, _cfg);
            Assert.AreEqual(HalfCone * _cfg.FreeSwingYawGain, atMiss, 1e-5f);
        }

        [Test]
        public void ImpactYaw_PastTheMissThreshold_IsAFlatWiderThrow()
        {
            float a = FreeSwingMath.ImpactYawRad(_cfg.FreeSwingImpactMissPx + 1f,   0.5f, 1f, HalfCone, _cfg);
            float b = FreeSwingMath.ImpactYawRad(_cfg.FreeSwingImpactMissPx + 200f, 0.5f, 1f, HalfCone, _cfg);

            Assert.AreEqual(HalfCone * _cfg.FreeSwingMissYawGain, a, 1e-5f);
            Assert.AreEqual(a, b, 1e-6f,
                "flat, not ramped: the very worst swing must not always land in the same place");
            Assert.Greater(a, HalfCone, "a big miss is thrown WIDER than the cone edge");
        }

        // ── Path ─────────────────────────────────────────────────────────────────

        [Test]
        public void PathDeg_AStraightUpstroke_IsZero()
        {
            var up = new List<Vector2>();
            for (int i = 1; i < 10; i++) up.Add(new Vector2(0f, i * 40f));
            Assert.AreEqual(0f, FreeSwingMath.PathDeg(Vector2.zero, new Vector2(0f, 400f), up), 1e-4f);
        }

        [Test]
        public void PathDeg_ADiagonalButSTRAIGHTUpstroke_IsAlsoZero()
        {
            // Offsets are measured from the CHORD, not from vertical. A swing that drifts sideways
            // while travelling dead straight has shaped nothing and must not curve.
            var up = new List<Vector2>();
            for (int i = 1; i < 10; i++) up.Add(new Vector2(i * 10f, i * 40f));
            Assert.AreEqual(0f, FreeSwingMath.PathDeg(Vector2.zero, new Vector2(100f, 400f), up), 1e-3f);
        }

        [Test]
        public void PathDeg_BowedRightIsPositive_BowedLeftIsNegative_AndTheyMirror()
        {
            var right = new List<Vector2>();
            var left  = new List<Vector2>();
            for (int i = 1; i < 10; i++)
            {
                float y = i * 40f;
                float bow = Mathf.Sin(i / 10f * Mathf.PI) * 40f;
                right.Add(new Vector2( bow, y));
                left .Add(new Vector2(-bow, y));
            }
            var end = new Vector2(0f, 400f);

            float r = FreeSwingMath.PathDeg(Vector2.zero, end, right);
            float l = FreeSwingMath.PathDeg(Vector2.zero, end, left);

            Assert.Greater(r, 0f, "bowed right must read positive");
            Assert.Less(l,   0f);
            Assert.AreEqual(-l, r, 1e-4f);
        }

        [Test]
        public void PathDeg_IsAnANGLE_SoTheSameGestureScalesWithTheStroke()
        {
            // A 40px bow on a 400px stroke and a 20px bow on a 200px stroke are the SAME swing,
            // and this is why the measure is atan2 rather than raw pixels: a lay-up and a driver
            // must ask for the same gesture, not the same displacement.
            var big   = new List<Vector2>();
            var small = new List<Vector2>();
            for (int i = 1; i < 10; i++)
            {
                float t = i / 10f;
                float bow = Mathf.Sin(t * Mathf.PI);
                big  .Add(new Vector2(bow * 40f, t * 400f));
                small.Add(new Vector2(bow * 20f, t * 200f));
            }
            Assert.AreEqual(FreeSwingMath.PathDeg(Vector2.zero, new Vector2(0f, 400f), big),
                            FreeSwingMath.PathDeg(Vector2.zero, new Vector2(0f, 200f), small), 1e-3f);
        }

        [Test]
        public void PathDeg_NoUpstrokeSamples_IsZeroRatherThanNaN()
        {
            Assert.AreEqual(0f, FreeSwingMath.PathDeg(Vector2.zero, new Vector2(0f, 400f),
                                                      new List<Vector2>()), 1e-6f);
            Assert.AreEqual(0f, FreeSwingMath.PathDeg(Vector2.zero, new Vector2(0f, 400f), null), 1e-6f);
        }

        // ── FadeDraw01 ───────────────────────────────────────────────────────────

        [Test]
        public void FadeDraw_InsideTheDeadzone_IsStraight()
        {
            float dead = FreeSwingMath.PathDeadzoneDeg(0f, _cfg);
            Assert.AreEqual(_cfg.FreeSwingPathDeadzoneAtCC0Deg, dead, 1e-4f);
            Assert.AreEqual(0f, FreeSwingMath.FadeDraw01(dead - 0.5f, 0f, false, _cfg), 1e-6f);
            Assert.AreEqual(FreeSwingPath.Straight,
                            FreeSwingMath.PathFor(dead - 0.5f, 0f, false, _cfg));
        }

        [Test]
        public void FadeDraw_ClubControlWidensTheDeadzone()
        {
            // The opposite direction from the accuracy windows, and the point: here the stat buys
            // forgiveness of thumb noise, not precision of aim.
            Assert.Less(FreeSwingMath.PathDeadzoneDeg(0f, _cfg),
                        FreeSwingMath.PathDeadzoneDeg(1f, _cfg));

            float bow = _cfg.FreeSwingPathDeadzoneAtCC0Deg + 1f;
            Assert.AreNotEqual(0f, FreeSwingMath.FadeDraw01(bow, 0f, false, _cfg));
            Assert.AreEqual(0f, FreeSwingMath.FadeDraw01(bow, 1f, false, _cfg), 1e-6f,
                "a steady golfer's small wobble is not a shot shape");
        }

        [Test]
        public void FadeDraw_BowedRightIsAFADE_BowedLeftIsADRAW()
        {
            // SIGN, pinned against render_fadedraw_curve_overlay.py: fadeDrawInput +1 is the
            // flick's handle at full RIGHT and it FADES; -1 is handle LEFT and it DRAWS. A flipped
            // sign here would curve every Free Swing shot the wrong way while every magnitude-only
            // assertion stayed green.
            float full = _cfg.FreeSwingPathFullDeg;
            Assert.AreEqual( 1f, FreeSwingMath.FadeDraw01( full, 0f, false, _cfg), 1e-4f);
            Assert.AreEqual(-1f, FreeSwingMath.FadeDraw01(-full, 0f, false, _cfg), 1e-4f);
            Assert.AreEqual(FreeSwingPath.Fade, FreeSwingMath.PathFor( full, 0f, false, _cfg));
            Assert.AreEqual(FreeSwingPath.Draw, FreeSwingMath.PathFor(-full, 0f, false, _cfg));
        }

        [Test]
        public void FadeDraw_RampsFromTheDeadzoneEdgeToFull_AndClampsPastIt()
        {
            float dead = FreeSwingMath.PathDeadzoneDeg(0f, _cfg);
            float mid  = dead + (_cfg.FreeSwingPathFullDeg - dead) * 0.5f;
            Assert.AreEqual(0.5f, FreeSwingMath.FadeDraw01(mid, 0f, false, _cfg), 1e-4f);
            Assert.AreEqual(1f, FreeSwingMath.FadeDraw01(_cfg.FreeSwingPathFullDeg * 4f, 0f, false, _cfg), 1e-4f);
        }

        [Test]
        public void FadeDraw_PuttsNeverCurve_HoweverBowedTheUpstroke()
        {
            Assert.AreEqual(0f, FreeSwingMath.FadeDraw01(_cfg.FreeSwingPathFullDeg * 4f, 0f, true, _cfg), 1e-6f);
            Assert.AreEqual(FreeSwingPath.Straight,
                            FreeSwingMath.PathFor(-_cfg.FreeSwingPathFullDeg, 0f, true, _cfg));
        }

        // ── Tempo ────────────────────────────────────────────────────────────────

        [Test]
        public void TempoRatio_IsUpOverBack()
        {
            Assert.AreEqual(0.5f, FreeSwingMath.TempoRatio(1.0f, 0.5f), 1e-5f);
            Assert.AreEqual(2.0f, FreeSwingMath.TempoRatio(0.25f, 0.5f), 1e-5f);
        }

        [Test]
        public void TempoTable_TheIdealCostsNothing_AndTheWordIsGOOD()
        {
            foreach (float cc in new[] { 0f, 1f })
            foreach (float p in new[] { 0f, 1f, 1.2f })
            {
                float w = FreeSwingMath.TempoWindow(cc, p, _cfg);
                float e = FreeSwingMath.TempoError(_cfg.FreeSwingIdealTempo, _cfg);
                Assert.AreEqual(0f, e, 1e-6f);
                Assert.AreEqual(1f, FreeSwingMath.TempoMul(e, w, _cfg), 1e-5f, $"cc {cc} power {p}");
                Assert.AreEqual(1f, FreeSwingMath.Timing01(e, w), 1e-5f);
                Assert.AreEqual(FreeSwingTempo.Good,
                                FreeSwingMath.TempoFor(_cfg.FreeSwingIdealTempo, e, w, _cfg));
            }
        }

        [Test]
        public void TempoTable_QuickIsFAST_SlowIsSLOW_AndBothCostPower()
        {
            float w = FreeSwingMath.TempoWindow(0f, 1f, _cfg);

            float fastRatio = _cfg.FreeSwingIdealTempo - w * 1.5f;
            float slowRatio = _cfg.FreeSwingIdealTempo + w * 1.5f;
            float eF = FreeSwingMath.TempoError(fastRatio, _cfg);
            float eS = FreeSwingMath.TempoError(slowRatio, _cfg);

            Assert.AreEqual(FreeSwingTempo.Fast, FreeSwingMath.TempoFor(fastRatio, eF, w, _cfg));
            Assert.AreEqual(FreeSwingTempo.Slow, FreeSwingMath.TempoFor(slowRatio, eS, w, _cfg));
            Assert.Less(FreeSwingMath.TempoMul(eF, w, _cfg), 1f);
            Assert.AreEqual(FreeSwingMath.TempoMul(eF, w, _cfg),
                            FreeSwingMath.TempoMul(eS, w, _cfg), 1e-5f,
                            "the penalty is symmetric: too quick costs what too slow costs");
        }

        [Test]
        public void TempoMul_RampsToGoldAtOneWindowOut_ThenDropsToRed()
        {
            float w = FreeSwingMath.TempoWindow(0f, 1f, _cfg);
            Assert.AreEqual(1f, FreeSwingMath.TempoMul(w, w, _cfg), 1e-5f);
            Assert.AreEqual(_cfg.TimingPowerMulGold, FreeSwingMath.TempoMul(2f * w, w, _cfg), 1e-5f);
            Assert.AreEqual(_cfg.TimingPowerMulRed,  FreeSwingMath.TempoMul(2.01f * w, w, _cfg), 1e-5f);
        }

        [Test]
        public void TempoWindow_WidensWithClubControl_AndShrinksWithPower()
        {
            Assert.Less(FreeSwingMath.TempoWindow(0f, 1f, _cfg),
                        FreeSwingMath.TempoWindow(1f, 1f, _cfg));
            Assert.Greater(FreeSwingMath.TempoWindow(0.5f, 0f, _cfg),
                           FreeSwingMath.TempoWindow(0.5f, 1.2f, _cfg));
        }

        [Test]
        public void Timing01_FallsFromOneToZeroAcrossTwoWindows()
        {
            float w = FreeSwingMath.TempoWindow(0f, 1f, _cfg);
            Assert.AreEqual(1f,   FreeSwingMath.Timing01(0f,       w), 1e-5f);
            Assert.AreEqual(0.5f, FreeSwingMath.Timing01(w,        w), 1e-5f);
            Assert.AreEqual(0f,   FreeSwingMath.Timing01(2f * w,   w), 1e-5f);
            Assert.AreEqual(0f,   FreeSwingMath.Timing01(20f * w,  w), 1e-5f, "clamped, never negative");
        }

        // ── The duff ─────────────────────────────────────────────────────────────

        [Test]
        public void Duff_ASlowUpstrokeIsRedAndShapeless_HoweverGoodTheImpact()
        {
            float slow = _cfg.FreeSwingDuffSpeedPxPerSec - 1f;
            var v = FreeSwingMath.Grade(0f, _cfg.FreeSwingPathFullDeg, _cfg.FreeSwingIdealTempo,
                                        slow, 1f, 0.5f, 0.5f, HalfCone, false, _cfg);

            Assert.AreEqual(FreeSwingGrade.Duff, v.Grade);
            Assert.AreEqual(_cfg.TimingPowerMulRed, v.TimingMul, 1e-5f);
            Assert.AreEqual(0f, v.FadeDraw01, 1e-6f, "a swing that slow shaped nothing");
            Assert.AreEqual(FreeSwingPath.Straight, v.Path);
            Assert.AreEqual(0f, v.Timing01, 1e-6f);
        }

        [Test]
        public void Duff_DoublesTheImpactYawButClampsAtTheMissCeiling()
        {
            float slow = _cfg.FreeSwingDuffSpeedPxPerSec - 1f;
            float xI   = _cfg.FreeSwingImpactMissPx * 0.4f;

            float clean = FreeSwingMath.ImpactYawRad(xI, 0.5f, 1f, HalfCone, _cfg);
            var   v     = FreeSwingMath.Grade(xI, 0f, _cfg.FreeSwingIdealTempo, slow, 1f,
                                              0.5f, 0.5f, HalfCone, false, _cfg);
            Assert.AreEqual(2f * clean, v.ErrorYawRad, 1e-5f);

            // ...and a duffed BIG miss is clamped rather than thrown twice as wide as any miss.
            var wild = FreeSwingMath.Grade(_cfg.FreeSwingImpactMissPx * 5f, 0f,
                                           _cfg.FreeSwingIdealTempo, slow, 1f,
                                           0.5f, 0.5f, HalfCone, false, _cfg);
            Assert.AreEqual(HalfCone * _cfg.FreeSwingMissYawGain, wild.ErrorYawRad, 1e-5f);
        }

        [Test]
        public void Duff_AtOrAboveTheThreshold_IsNotADuff()
        {
            var v = FreeSwingMath.Grade(0f, 0f, _cfg.FreeSwingIdealTempo,
                                        _cfg.FreeSwingDuffSpeedPxPerSec, 1f,
                                        0.5f, 0.5f, HalfCone, false, _cfg);
            Assert.AreNotEqual(FreeSwingGrade.Duff, v.Grade);
        }

        // ── Grade precedence ─────────────────────────────────────────────────────

        [Test]
        public void Grade_CleanImpactAndGoodTempo_IsPURE()
        {
            var v = FreeSwingMath.Grade(0f, 0f, _cfg.FreeSwingIdealTempo, 3000f, 1f,
                                        0.5f, 0.5f, HalfCone, false, _cfg);
            Assert.AreEqual(FreeSwingGrade.Pure, v.Grade);
            Assert.AreEqual(0f, v.ErrorYawRad, 1e-6f);
            Assert.AreEqual(1f, v.TimingMul, 1e-5f);
            Assert.IsTrue(v.ImpactClean);
        }

        [Test]
        public void Grade_CleanImpactButOffTempo_IsNoPopAtAll()
        {
            float w = FreeSwingMath.TempoWindow(0.5f, 1f, _cfg);
            var v = FreeSwingMath.Grade(0f, 0f, _cfg.FreeSwingIdealTempo + w * 1.5f, 3000f, 1f,
                                        0.5f, 0.5f, HalfCone, false, _cfg);
            Assert.AreEqual(FreeSwingGrade.None, v.Grade,
                "an ordinary swing gets the chip and no banner");
            Assert.Less(v.TimingMul, 1f);
        }

        [Test]
        public void Grade_ABigMissPopsHOOKorSLICE_AndOutranksPURE()
        {
            var hook = FreeSwingMath.Grade(-_cfg.FreeSwingImpactMissPx - 1f, 0f,
                                           _cfg.FreeSwingIdealTempo, 3000f, 1f,
                                           0.5f, 0.5f, HalfCone, false, _cfg);
            var slice = FreeSwingMath.Grade(_cfg.FreeSwingImpactMissPx + 1f, 0f,
                                            _cfg.FreeSwingIdealTempo, 3000f, 1f,
                                            0.5f, 0.5f, HalfCone, false, _cfg);

            Assert.AreEqual(FreeSwingGrade.Hook,  hook.Grade);
            Assert.AreEqual(FreeSwingGrade.Slice, slice.Grade);
            Assert.Less(hook.ErrorYawRad,     0f, "HOOK goes left");
            Assert.Greater(slice.ErrorYawRad, 0f, "SLICE goes right");
            Assert.AreEqual(-hook.ErrorYawRad, slice.ErrorYawRad, 1e-5f, "mirrored");
        }

        [Test]
        public void Grade_ASmallMissIsNoPop_ButStillBendsTheShot()
        {
            float w = FreeSwingMath.ImpactWindowPx(0.5f, 1f, _cfg);
            var v = FreeSwingMath.Grade(w + 5f, 0f, _cfg.FreeSwingIdealTempo, 3000f, 1f,
                                        0.5f, 0.5f, HalfCone, false, _cfg);
            Assert.AreEqual(FreeSwingGrade.None, v.Grade);
            Assert.Greater(v.ErrorYawRad, 0f);
            Assert.IsFalse(v.ImpactClean);
        }

        [Test]
        public void Grade_DuffOutranksEverything()
        {
            // A wild impact AND a duff: the DUFF is what the player needs told, because a swing
            // that never happened has nothing else worth reading about it.
            var v = FreeSwingMath.Grade(_cfg.FreeSwingImpactMissPx * 3f, 0f,
                                        _cfg.FreeSwingIdealTempo,
                                        _cfg.FreeSwingDuffSpeedPxPerSec - 1f, 1f,
                                        0.5f, 0.5f, HalfCone, false, _cfg);
            Assert.AreEqual(FreeSwingGrade.Duff, v.Grade);
        }

        [Test]
        public void Grade_TheVerdictCarriesTheGRADEDWindow_NotTheFullWidthOne()
        {
            // Carry-over 2 is only honest if the number the chip reads is the number the swing was
            // judged against, at the power it fired at.
            var soft = FreeSwingMath.Grade(0f, 0f, _cfg.FreeSwingIdealTempo, 3000f, 0f,
                                           0.5f, 0.5f, HalfCone, false, _cfg);
            var hard = FreeSwingMath.Grade(0f, 0f, _cfg.FreeSwingIdealTempo, 3000f, 1.2f,
                                           0.5f, 0.5f, HalfCone, false, _cfg);
            Assert.Greater(soft.ImpactWindowPx, hard.ImpactWindowPx);
            Assert.AreEqual(FreeSwingMath.ImpactWindowPx(0.5f, 1.2f, _cfg), hard.ImpactWindowPx, 1e-4f);
        }

        [Test]
        public void Grade_APuttNeverCurves_EvenOnAFullyBowedUpstroke()
        {
            var v = FreeSwingMath.Grade(0f, _cfg.FreeSwingPathFullDeg, _cfg.FreeSwingIdealTempo,
                                        3000f, 1f, 0.5f, 0.5f, HalfCone, true, _cfg);
            Assert.AreEqual(0f, v.FadeDraw01, 1e-6f);
            Assert.AreEqual(FreeSwingPath.Straight, v.Path);
        }

        // ── Keys ─────────────────────────────────────────────────────────────────

        [Test]
        public void GradeKeys_AreKeys_AndNoneHasNone()
        {
            Assert.AreEqual("SHOT_GRADE_PURE",  FreeSwingMath.GradeKey(FreeSwingGrade.Pure));
            Assert.AreEqual("SHOT_GRADE_DUFF",  FreeSwingMath.GradeKey(FreeSwingGrade.Duff));
            Assert.AreEqual("SHOT_GRADE_HOOK",  FreeSwingMath.GradeKey(FreeSwingGrade.Hook));
            Assert.AreEqual("SHOT_GRADE_SLICE", FreeSwingMath.GradeKey(FreeSwingGrade.Slice));
            Assert.IsNull(FreeSwingMath.GradeKey(FreeSwingGrade.None),
                "None has no word — the pop must not resolve a blank key over the ball");
        }

        [Test]
        public void PathAndTempoWords_AreKEYS_NeverLiterals()
        {
            Assert.AreEqual("SWING_PATH_STRAIGHT", FreeSwingMath.PathKey(FreeSwingPath.Straight));
            Assert.AreEqual("SWING_PATH_DRAW",     FreeSwingMath.PathKey(FreeSwingPath.Draw));
            Assert.AreEqual("SWING_PATH_FADE",     FreeSwingMath.PathKey(FreeSwingPath.Fade));
            Assert.AreEqual("SWING_TEMPO_GOOD",    FreeSwingMath.TempoKey(FreeSwingTempo.Good));
            Assert.AreEqual("SWING_TEMPO_FAST",    FreeSwingMath.TempoKey(FreeSwingTempo.Fast));
            Assert.AreEqual("SWING_TEMPO_SLOW",    FreeSwingMath.TempoKey(FreeSwingTempo.Slow));
        }
    }
}
