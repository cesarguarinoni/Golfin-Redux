using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;
using Golfin.Gameplay.Input;
using Golfin.Gameplay.Config;
using Golfin.Gameplay.UI.Controls;
using Golfin.Gameplay.UI.Controls.FreeSwing;
using Golfin.Gameplay.UI.HUD;
using Golfin.Gameplay.UI.ShotUI;
using Golfin.Physics;
using Golfin.Physics.Stats;
using Golfin.Physics.Math;

namespace Golfin.Gameplay.Tests
{
    /// <summary>
    /// scheme_freeswing §5.2 — the one continuous gesture, driven as a sample sequence.
    ///
    /// <para>A DRIVEN CLOCK, WHICH THE OTHER TWO SCHEMES DID NOT NEED. Tempo and the duff
    /// threshold are SECONDS, and <c>Time.unscaledTime</c> does not advance in EditMode, so half
    /// this scheme would be untestable without <c>ConfigureForTests</c>'s clock injection. The
    /// harness advances it per sample, which is also what lets the hitch-frame test exist at
    /// all.</para>
    ///
    /// <para>NO FLICK-GATE HARNESS, like the Needle's and unlike the Pendulum's: this scheme never
    /// calls <c>EvaluateFlickGate</c>, because the release is not the shot. That absence is itself
    /// part of what these tests pin — <c>ReleaseAfterCrossing_IsIgnored</c> is the assertion.</para>
    /// </summary>
    [TestFixture]
    public class FreeSwingSchemeDriverTests
    {
        private GameObject _scGo, _rootGo, _handleGo, _laneGo, _traceGo, _chipGo, _popGo, _buttonsGo;
        private ShotController         _sc;
        private FreeSwingSchemeDriver  _driver;
        private FreeSwingLaneView      _lane;
        private FreeSwingTraceView     _trace;
        private FreeSwingAnalyzerChip  _chip;
        private SchemeGradePop         _pop;
        private ActionButtonsRoot      _buttons;
        private FadeDrawButtonWidget   _fadeDrawButton;
        private RectTransform          _laneRt, _windowRt, _tick100Rt, _tick120Rt, _impactRt;
        private ControlsConfig         _cfg;

        private float _now;

        private ShotInput _lastShot;
        private int _shotCount, _cancelCount;

        [SetUp]
        public void SetUp()
        {
            _now = 0f;

            _scGo = new GameObject("FreeSwingDriverTests_SC");
            _sc   = _scGo.AddComponent<ShotController>();

            _cfg = ControlsConfig.Default;
            _sc.InjectConfig(_cfg);
            _sc.InjectStatBundle(new StatBundle(ClubStats.DefaultDriver, BallStats.Neutral,
                CharacterStats.Neutral, fp.FromInt(100), fp.FromInt(100)));

            _rootGo   = new GameObject("SchemeRoot_FreeSwing", typeof(RectTransform));
            _handleGo = new GameObject("FreeSwingHandle",      typeof(RectTransform));
            _handleGo.transform.SetParent(_rootGo.transform, false);
            ((RectTransform)_handleGo.transform).anchoredPosition = new Vector2(0f, -HandleRest);

            // ── Lane, wired the way the scene builder wires it. A view with null rects answers 0
            // to every geometry question, which reads as a passing assertion instead of a missing
            // one — the trap NeedleSchemeDriverTests names.
            _laneGo = new GameObject("FreeSwingLaneRoot", typeof(RectTransform), typeof(CanvasGroup));
            _laneGo.transform.SetParent(_rootGo.transform, false);
            _lane = _laneGo.AddComponent<FreeSwingLaneView>();
            _laneRt    = Rect(_laneGo, "SwingLane");
            _tick100Rt = Rect(_laneGo, "Tick100");
            _tick120Rt = Rect(_laneGo, "Tick120");
            _impactRt  = Rect(_laneGo, "ImpactLine");
            _windowRt  = Rect(_laneGo, "ImpactWindow");
            _lane.ConfigureForTests(_laneRt, _tick100Rt, _tick120Rt, _impactRt, _windowRt,
                                    Tmp(_laneGo, "Label100"), Tmp(_laneGo, "Label120"),
                                    Tmp(_laneGo, "ImpactLabel"));

            _traceGo = new GameObject("FreeSwingTraceRoot", typeof(RectTransform), typeof(CanvasGroup));
            _traceGo.transform.SetParent(_rootGo.transform, false);
            _trace = _traceGo.AddComponent<FreeSwingTraceView>();
            var graphicGo = new GameObject("FreeSwingTrace", typeof(RectTransform), typeof(CanvasRenderer));
            graphicGo.transform.SetParent(_traceGo.transform, false);
            _trace.ConfigureForTests(graphicGo.AddComponent<FreeSwingTraceGraphic>(),
                                     _traceGo.GetComponent<CanvasGroup>());

            _chipGo = new GameObject("FreeSwingAnalyzerChip", typeof(RectTransform), typeof(CanvasGroup));
            _chipGo.transform.SetParent(_rootGo.transform, false);
            _chip = _chipGo.AddComponent<FreeSwingAnalyzerChip>();
            _chip.ConfigureForTests(_chipGo.GetComponent<CanvasGroup>(),
                                    Tmp(_chipGo, "LblPOWER"),  Tmp(_chipGo, "LblIMPACT"),
                                    Tmp(_chipGo, "LblPATH"),   Tmp(_chipGo, "LblTEMPO"),
                                    Tmp(_chipGo, "ValPOWER"),  Tmp(_chipGo, "ValIMPACT"),
                                    Tmp(_chipGo, "ValPATH"),   Tmp(_chipGo, "ValTEMPO"));

            _popGo = new GameObject("FreeSwingGradePop", typeof(RectTransform), typeof(CanvasGroup));
            _popGo.transform.SetParent(_rootGo.transform, false);
            _pop = _popGo.AddComponent<SchemeGradePop>();
            _pop.ConfigureForTests(Tmp(_popGo, "FreeSwingGradeText"), _popGo.GetComponent<CanvasGroup>());

            _buttonsGo = new GameObject("ActionButtonsRoot", typeof(RectTransform), typeof(CanvasGroup));
            var fdGo = new GameObject("FadeDrawButton", typeof(RectTransform));
            fdGo.transform.SetParent(_buttonsGo.transform, false);
            _fadeDrawButton = fdGo.AddComponent<FadeDrawButtonWidget>();
            _buttons = _buttonsGo.AddComponent<ActionButtonsRoot>();
            _buttons.ConfigureForTests(_buttonsGo.GetComponent<CanvasGroup>(), _fadeDrawButton);

            ShotModeContext.Mode = ShotMode.Straight;

            _driver = _rootGo.AddComponent<FreeSwingSchemeDriver>();
            _driver.ConfigureForTests((RectTransform)_rootGo.transform,
                                      (RectTransform)_handleGo.transform,
                                      _lane, _trace, _chip, _pop, _buttons, _cfg, () => _now);
            _driver.Bind(_sc);
            _driver.Activate();

            _shotCount = _cancelCount = 0;
            _sc.OnShotResolved           += (s, _) => { _lastShot = s; _shotCount++; };
            ShotController.ShotCancelled += OnCancelled;
        }

        [TearDown]
        public void TearDown()
        {
            ShotController.ShotCancelled -= OnCancelled;
            _driver.Deactivate();
            ShotModeContext.Mode = ShotMode.Straight;
            Object.DestroyImmediate(_rootGo);
            Object.DestroyImmediate(_buttonsGo);
            Object.DestroyImmediate(_scGo);
        }

        private void OnCancelled() => _cancelCount++;

        /// <summary>The club-head rest offset the scene builder wires into the lane. The tests
        /// need it because it IS the crossing offset — see FreeSwingLaneView.ImpactCrossOffsetPx.</summary>
        private const float HandleRest = 70f;

        private static RectTransform Rect(GameObject parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent.transform, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            return rt;
        }

        private static TextMeshProUGUI Tmp(GameObject parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent.transform, false);
            return go.AddComponent<TextMeshProUGUI>();
        }

        // ── Harness ──────────────────────────────────────────────────────────────
        //
        // Every distance is derived from the config (carry-over 6), never a literal pixel count:
        // a retune of the thresholds must not silently change what these gestures are pulling to.

        private const float OriginX = 500f;
        private const float OriginY = 900f;

        private static PointerEventData At(float x, float y)
            => new PointerEventData(EventSystem.current) { position = new Vector2(x, y) };

        /// <summary>Touch the club head. The driver reads local coordinates through the scheme
        /// root, which here is an unparented RectTransform at the origin, so screen == local.</summary>
        private void Down() => _driver.OnPointerDown(At(OriginX, OriginY));

        /// <summary>One sample at a wall-clock offset. The clock is DRIVEN, so a test states the
        /// gesture's timing explicitly instead of hoping the frame rate cooperates.</summary>
        private void Sample(float x, float y, float dt)
        {
            _now += dt;
            _driver.ProcessDrag(new Vector2(x, y));
        }

        /// <summary>
        /// How many samples a gesture of this length gets: one per 60fps frame.
        ///
        /// <para>NOT AN ARBITRARY STEP COUNT. The driver clamps every inter-sample dt to
        /// <c>FreeSwingMath.MaxStepSeconds</c> (1/30 s), so a harness that sampled a 0.6 s
        /// backswing in 12 steps would hand the driver twelve 50 ms gaps, every one of them
        /// clamped, and measure a 0.4 s backswing — the harness would be triggering the
        /// hitch-protection on every frame and the tempo assertions would be pinning the clamp
        /// rather than the tempo. Sampling at the rate the device actually samples at is what
        /// makes <see cref="AHitchFrameCannotTurnAGoodSwingIntoADuff"/> a test of ONE bad frame.</para>
        /// </summary>
        private static int StepsFor(float seconds) => Mathf.Max(6, Mathf.RoundToInt(seconds * 60f));

        /// <summary>Pull straight down to <paramref name="pullPx"/> over <paramref name="seconds"/>.</summary>
        private void PullDown(float pullPx, float seconds = 0.6f, int steps = 0, float lateral = 0f)
        {
            if (steps <= 0) steps = StepsFor(seconds);
            for (int i = 1; i <= steps; i++)
            {
                float t = i / (float)steps;
                Sample(OriginX + lateral * t, OriginY - pullPx * t, seconds / steps);
            }
        }

        /// <summary>Drag back UP through the impact line, ending <paramref name="crossX"/> px
        /// right of the origin, over <paramref name="seconds"/>, optionally bowed.</summary>
        private void SwingUp(float fromPull, float seconds = 0.3f, int steps = 0,
                             float crossX = 0f, float bowPx = 0f)
        {
            if (steps <= 0) steps = StepsFor(seconds);
            // Travel from the bottom of the backswing to a HAIR past the impact line, so the
            // crossing lands on the last sample and the upswing really did take `seconds`.
            // Ramping well past the line instead (the first version overshot by 40px) fires the
            // shot at ~92% of the ramp and silently measures a 0.28 s upswing for a 0.30 s
            // gesture, which is a 6% tempo error the ratio assertions cannot absorb.
            float startY = OriginY - fromPull;
            float endY   = OriginY + HandleRest + 0.5f;
            for (int i = 1; i <= steps; i++)
            {
                float t = i / (float)steps;
                float y = Mathf.Lerp(startY, endY, t);
                float x = OriginX + crossX * t + bowPx * Mathf.Sin(t * Mathf.PI);
                Sample(x, y, seconds / steps);
            }
        }

        private void Release() => _driver.OnPointerUp(At(OriginX, OriginY));

        /// <summary>
        /// Land the ball and let the controller PUBLISH the Idle it just transitioned to.
        ///
        /// <para><c>CompleteShot</c> calls <c>TransitionToIdle</c>, which does NOT publish — every
        /// path back to Idle relies on the next <c>Tick</c> to do it (ShotController.Tick's
        /// no-input-source branch). A test that only called <c>CompleteShot</c> would assert
        /// against a driver that was never told the shot was over, and would then "prove" that
        /// the chrome does not reset — a failure of the harness dressed as a failure of the
        /// driver.</para>
        /// </summary>
        private void SettleToIdle()
        {
            _sc.CompleteShot();
            _sc.Tick(0.016f);
        }

        // ── 1. The backswing ─────────────────────────────────────────────────────

        [Test]
        public void PullDown_EntersTimingAndPublishesThePeakPower()
        {
            Down();
            PullDown((_cfg.FreeSwingMinUsefulPullPx + _cfg.FreeSwingPull100Px) * 0.5f);

            Assert.AreEqual(ShotState.Timing, _sc.State, "power > 0 must move the shot to Timing");
            Assert.AreEqual(0.5f, _sc.PowerNormalized, 1e-3f);
            Assert.IsTrue(_sc.IsExternalDragActive);
            Assert.IsTrue(_driver.IsBackswing);
            Assert.AreEqual(0, _shotCount, "nothing fires on the way down");
        }

        [Test]
        public void PullBelowTheDeadZone_StaysAiming()
        {
            Down();
            PullDown(_cfg.FreeSwingMinUsefulPullPx - 10f);
            Assert.AreEqual(ShotState.Aiming, _sc.State);
            Assert.AreEqual(0f, _sc.PowerNormalized, 1e-6f);
        }

        [Test]
        public void OverpowerPull_PublishesPastOneHundredPercent()
        {
            Down();
            PullDown(_cfg.FreeSwingPull120Px);
            Assert.AreEqual(1.2f, _sc.PowerNormalized, 1e-3f);
        }

        [Test]
        public void OwnsTiming_MeansNoArrowUnderTheSwing()
        {
            Down();
            PullDown(_cfg.FreeSwingPull100Px);
            ShotInputState last = default;
            _sc.OnStateChanged += s => last = s;
            for (int i = 0; i < 50; i++) _sc.Tick(0.1f);

            Assert.AreEqual(ShotState.Timing, _sc.State, "the pass counter must not cancel us");
            Assert.AreEqual(0f, last.ArrowProgress01, 1e-6f);
        }

        [Test]
        public void TheTicksAreDrawnWhereTheClubHeadLands_AndTheImpactLineWhereItReturns()
        {
            // The lane IS the config, drawn: a tick is at HandleRestBelowBall + the threshold, and
            // this asserts the DRAWN offset against the CONFIG rather than against a
            // recomputation of the same formula.
            float above = _cfg.FreeSwingFollowThroughPx;
            Assert.AreEqual(above + HandleRest + _cfg.FreeSwingPull100Px,
                            _lane.DrawnTick100FromLaneTop, 0.5f);
            Assert.AreEqual(above + HandleRest + _cfg.FreeSwingPull120Px,
                            _lane.DrawnTick120FromLaneTop, 0.5f);
            Assert.AreEqual(above, _lane.DrawnImpactFromLaneTop, 0.5f,
                "the impact line sits on the ball, FollowThroughPx down from the lane's top edge");
        }

        [Test]
        public void TheLaneIsDerivedFromTheDeepestTick_NotAuthored()
        {
            // The lengthened-pill fix, imitated from the Pendulum: a longer pull needs a longer
            // pill, and the pill's length has to come from the SAME field the tick does or the two
            // drift apart. Plus the one term no other scheme has — the follow-through above the ball.
            Assert.AreEqual(_cfg.FreeSwingFollowThroughPx + HandleRest + _cfg.FreeSwingPull120Px
                            + 50f /* club half-height */ + 20f /* tail */,
                            _lane.LaneHeight, 1f);
            Assert.AreEqual(_lane.LaneHeight, _laneRt.sizeDelta.y, 0.5f, "and it is actually applied");
        }

        [Test]
        public void PuttLane_DropsTheOneTwentyTick_AndIsShorter()
        {
            float swing = _lane.LaneHeight;
            _sc.IsPutt = true;
            _driver.Activate();

            Assert.IsFalse(_lane.Tick120Shown, "a putt has no 120% tick to pull past");
            Assert.Less(_lane.LaneHeight, swing);
        }

        [Test]
        public void TheDrawnImpactWindow_IsTheGRADEDWindow_AndClosesAsThePullDeepens()
        {
            // Carry-over 2, made checkable: the drawn HALF-width is compared against the maths at
            // the PEAK power, measured off the live rect rather than recomputed.
            Down();
            PullDown(_cfg.FreeSwingMinUsefulPullPx + 1f);
            float soft = _lane.DrawnImpactHalfWidthPx;

            PullDown(_cfg.FreeSwingPull120Px);
            float hard = _lane.DrawnImpactHalfWidthPx;

            Assert.Less(hard, soft, "the green window must visibly close as the player pulls");
            Assert.AreEqual(FreeSwingMath.ImpactWindowPx(_sc.ClubAccuracyNorm01, _driver.PeakPower, _cfg),
                            hard, 0.5f,
                            "and the drawn window must BE the graded one, not merely resemble it");
        }

        // ── 2. The crossing IS the shot ──────────────────────────────────────────

        [Test]
        public void StraightSwingThroughTheLine_FiresOnTheCROSSING_NotOnTheRelease()
        {
            Down();
            PullDown(_cfg.FreeSwingPull100Px, seconds: 0.6f);
            SwingUp(_cfg.FreeSwingPull100Px, seconds: 0.3f);

            Assert.AreEqual(1, _shotCount, "the shot fired while the finger was still down");
            Assert.AreEqual(1, _driver.CommitCount);
            Assert.AreEqual(0, _cancelCount);
            Assert.AreEqual(1f, _driver.LastCommittedPower, 1e-3f);
            Assert.AreEqual(FreeSwingGrade.Pure, _driver.LastVerdict.Grade,
                "clean impact at the ideal 2:1 tempo is a PURE");
            Assert.AreEqual(0f, _driver.LastVerdict.FadeDraw01, 1e-3f, "a straight path shapes nothing");
        }

        [Test]
        public void ReleaseAfterCrossing_IsIgnored()
        {
            Down();
            PullDown(_cfg.FreeSwingPull100Px);
            SwingUp(_cfg.FreeSwingPull100Px);
            Assert.AreEqual(1, _shotCount);

            Release();
            Assert.AreEqual(1, _shotCount, "the lift after a commit is not an event");
            Assert.AreEqual(0, _cancelCount, "and it must NOT cancel the shot that already fired");
            Assert.AreEqual(1, _driver.CommitCount);
        }

        [Test]
        public void ExactlyOneCommitPerTouch_HoweverFarTheFingerTravelsAfterwards()
        {
            Down();
            PullDown(_cfg.FreeSwingPull100Px);
            SwingUp(_cfg.FreeSwingPull100Px);
            // Keep dragging: down again, up again, all on the same touch.
            for (int i = 0; i < 20; i++) Sample(OriginX, OriginY - 200f + i * 30f, 0.016f);

            Assert.AreEqual(1, _driver.CommitCount);
            Assert.AreEqual(1, _shotCount);
        }

        [Test]
        public void CrossingOffCentreToTheRight_IsAPositiveYaw_AndTheChipSaysSoTheSameWay()
        {
            float off = _cfg.FreeSwingImpactMissPx * 0.5f;
            Down();
            PullDown(_cfg.FreeSwingPull100Px);
            SwingUp(_cfg.FreeSwingPull100Px, crossX: off);

            var v = _driver.LastVerdict;
            Assert.Greater(v.ImpactPx, 0f, "the club head crossed right of the lane centre");
            Assert.Greater(v.ErrorYawRad, 0f, "which sends the ball right");
            StringAssert.StartsWith(FreeSwingMath.ArrowRight, _chip.LastImpactText,
                "and the chip's arrowhead must point the same way the ball went");
        }

        [Test]
        public void CrossingWellLeft_PopsHOOK_AndMirrorsACrossingWellRight()
        {
            float far = _cfg.FreeSwingImpactMissPx * 1.6f;

            Down();
            PullDown(_cfg.FreeSwingPull100Px);
            SwingUp(_cfg.FreeSwingPull100Px, crossX: -far);
            float hookYaw = _driver.LastVerdict.ErrorYawRad;
            Assert.AreEqual(FreeSwingGrade.Hook, _driver.LastVerdict.Grade);
            Assert.AreEqual(FreeSwingMath.KeyHook, _pop.LastKeyShown);

            SettleToIdle();
            _now += 1f;

            Down();
            PullDown(_cfg.FreeSwingPull100Px);
            SwingUp(_cfg.FreeSwingPull100Px, crossX: far);
            Assert.AreEqual(FreeSwingGrade.Slice, _driver.LastVerdict.Grade);
            Assert.AreEqual(FreeSwingMath.KeySlice, _pop.LastKeyShown);
            Assert.AreEqual(-hookYaw, _driver.LastVerdict.ErrorYawRad, 1e-4f, "mirrored");
        }

        [Test]
        public void ABowedUpstroke_ShapesTheShot_AndTheSignMatchesFlicksHandle()
        {
            // Bowed LEFT is a DRAW (fadeDrawInput < 0, the flick's handle-left), bowed right a
            // FADE. Pinned here as well as in the maths because the driver is what decides which
            // samples the bow is measured from.
            Down();
            PullDown(_cfg.FreeSwingPull100Px);
            // Bowed far enough to CLEAR the dead zone. At 90px of bow over a ~450px stroke the
            // path reads about 4 degrees, which is inside the 9-degree dead zone at Club Control
            // 0.5 and correctly shapes nothing — the first version of this test asserted a curve
            // from a swing the scheme is designed to call straight.
            SwingUp(_cfg.FreeSwingPull100Px, bowPx: -320f);

            var v = _driver.LastVerdict;
            Assert.Less(v.PathDeg, 0f, "the upstroke bowed left");
            Assert.Greater(Mathf.Abs(v.PathDeg), FreeSwingMath.PathDeadzoneDeg(0.5f, _cfg),
                           "harness: and bowed enough to be past the dead zone");
            Assert.Less(v.FadeDraw01, 0f);
            Assert.AreEqual(FreeSwingPath.Draw, v.Path);
            Assert.AreEqual(FreeSwingMath.KeyPathDraw, _chip.LastPathKey);
            // ...and the curve REACHED THE BALL. ShotInput carries no fadeDraw field — the shape
            // arrives as a tilt of the spin axis (ShotInputBuilder step: tiltAngle = fadeDrawInput
            // * fadeDrawMaxTiltRad + ...), which is the same y-component FadeDrawTiltTests reads.
            // Asserting the verdict alone would leave a driver that computed a perfect curve and
            // dropped it on the way into the intent looking green.
            Assert.Greater(Mathf.Abs(_lastShot.Spin.Axis.y.ToFloat()), 1e-4f,
                "a bowed upstroke must tilt the resolved spin axis");
        }

        [Test]
        public void ASlowUpstroke_IsADUFF()
        {
            // Slow enough that the path length over its own duration is under the threshold.
            Down();
            PullDown(_cfg.FreeSwingPull100Px, seconds: 0.6f);
            SwingUp(_cfg.FreeSwingPull100Px, seconds: 3.0f);

            Assert.AreEqual(FreeSwingGrade.Duff, _driver.LastVerdict.Grade);
            Assert.AreEqual(FreeSwingMath.KeyDuff, _pop.LastKeyShown);
            Assert.AreEqual(_cfg.TimingPowerMulRed, _driver.LastVerdict.TimingMul, 1e-4f);
            Assert.Less(_driver.LastVerdict.UpSpeedPxPerSec, _cfg.FreeSwingDuffSpeedPxPerSec);
        }

        // ── 3. The two cancel paths ──────────────────────────────────────────────

        [Test]
        public void LiftDuringTheBackswing_CancelsAndFiresNothing()
        {
            Down();
            PullDown(_cfg.FreeSwingPull100Px);
            Release();

            Assert.AreEqual(0, _shotCount);
            Assert.AreEqual(1, _cancelCount);
            Assert.AreEqual(0, _driver.CommitCount);
            Assert.AreEqual(ShotState.Idle, _sc.State);
        }

        [Test]
        public void LiftMidUpstrokeBeforeTheLine_CancelsAndFiresNothing()
        {
            Down();
            PullDown(_cfg.FreeSwingPull100Px);
            // Halfway back up, still well below the impact line.
            Sample(OriginX, OriginY - _cfg.FreeSwingPull100Px * 0.5f, 0.1f);
            Assert.IsTrue(_driver.IsUpstroke, "harness: the reversal really was detected");
            Release();

            Assert.AreEqual(0, _shotCount);
            Assert.AreEqual(1, _cancelCount);
        }

        [Test]
        public void AReversalBelowTheMinimumPull_DoesNotArmTheUpswing()
        {
            // A thumb that twitches on touch-down must not start the upswing at 0% power.
            Down();
            Sample(OriginX, OriginY - (_cfg.FreeSwingMinUsefulPullPx - 10f), 0.05f);
            Sample(OriginX, OriginY - (_cfg.FreeSwingMinUsefulPullPx - 20f), 0.05f);
            Assert.IsTrue(_driver.IsBackswing, "still loading — that was not a backswing");
            Assert.IsFalse(_driver.IsUpstroke);
        }

        // ── 4. The double pump ───────────────────────────────────────────────────

        [Test]
        public void DoublePump_IsOneShotAtTheDEEPERPower()
        {
            float shallow = _cfg.FreeSwingPull100Px * 0.5f;
            float deep    = _cfg.FreeSwingPull100Px;

            Down();
            PullDown(shallow, seconds: 0.3f);
            // Back up past the reversal slop — a genuine second backswing, not thumb noise.
            Sample(OriginX, OriginY - shallow * 0.5f, 0.05f);
            Assert.IsTrue(_driver.IsUpstroke);
            Sample(OriginX, OriginY - shallow * 0.5f - _cfg.FreeSwingReversalSlopPx - 10f, 0.05f);
            Assert.IsTrue(_driver.IsBackswing, "past the slop is a second backswing");

            // ...then go deeper, and swing out for real.
            PullDown(deep, seconds: 0.3f);
            SwingUp(deep, seconds: 0.3f);

            Assert.AreEqual(1, _driver.CommitCount, "one shot, not two");
            Assert.AreEqual(1, _shotCount);
            Assert.AreEqual(FreeSwingMath.Power(deep, _cfg, false), _driver.LastCommittedPower, 1e-3f,
                "at the DEEPER of the two pulls — the one the player asked for");
        }

        [Test]
        public void ThumbNoiseInsideTheSlop_DoesNotReArmTheBackswing()
        {
            Down();
            PullDown(_cfg.FreeSwingPull100Px, seconds: 0.4f);
            float y = OriginY - _cfg.FreeSwingPull100Px;
            Sample(OriginX, y + 30f, 0.05f);                                   // reversal
            Assert.IsTrue(_driver.IsUpstroke);
            Sample(OriginX, y + 30f - (_cfg.FreeSwingReversalSlopPx - 4f), 0.05f);   // a wobble
            Assert.IsTrue(_driver.IsUpstroke, "inside the slop the upswing simply continues");
        }

        // ── 5. Tempo, and the hitch frame ────────────────────────────────────────

        [Test]
        public void TempoIsMeasuredAsUpOverBack()
        {
            Down();
            PullDown(_cfg.FreeSwingPull100Px, seconds: 0.60f);
            SwingUp(_cfg.FreeSwingPull100Px, seconds: 0.30f);

            Assert.AreEqual(0.60f, _driver.LastCommittedBackSeconds, 0.02f);
            Assert.AreEqual(0.30f, _driver.LastCommittedUpSeconds,   0.02f);
            Assert.AreEqual(0.5f,  _driver.LastVerdict.TempoRatio,   0.05f);
        }

        [Test]
        public void AHitchFrameCannotTurnAGoodSwingIntoADuff()
        {
            // Carry-over 9. A 0.4 s stall mid-upswing is clamped to one 30fps step, so the measured
            // upstroke is at most MaxStepSeconds longer than the honest one. Without the clamp the
            // same gesture reports a swing several times slower than the thumb actually moved.
            Down();
            PullDown(_cfg.FreeSwingPull100Px, seconds: 0.60f);

            int   steps  = StepsFor(0.30f);
            float startY = OriginY - _cfg.FreeSwingPull100Px;
            float endY   = OriginY + HandleRest + 40f;
            for (int i = 1; i <= steps; i++)
            {
                float t  = i / (float)steps;
                // One catastrophic frame in the middle of the upswing.
                float dt = i == steps / 2 ? 0.40f : 0.30f / steps;
                Sample(OriginX, Mathf.Lerp(startY, endY, t), dt);
            }

            // A BOUND, not an exact figure: the crossing ends the gesture a sample or two before
            // the loop does, so the honest total is not knowable in advance. What IS knowable —
            // and what carry-over 9 promises — is that one 0.4 s stall can add at most one
            // 30fps step to the measured upswing.
            Assert.Less(_driver.LastCommittedUpSeconds, 0.30f + FreeSwingMath.MaxStepSeconds,
                        "the hitch contributed at most one 30fps step");
            Assert.Greater(_driver.LastCommittedUpSeconds, FreeSwingMath.MaxStepSeconds,
                           "harness: the upswing really was sampled");
            Assert.AreNotEqual(FreeSwingGrade.Duff, _driver.LastVerdict.Grade,
                               "and a good swing survives the hitch");
        }

        // ── 6. The club head ─────────────────────────────────────────────────────

        [Test]
        public void TheClubHeadFollowsTheFinger_VerticallyAndLaterally()
        {
            var handle = (RectTransform)_handleGo.transform;
            Down();
            PullDown(_cfg.FreeSwingPull100Px, lateral: 40f);

            Assert.AreEqual(-(HandleRest + _cfg.FreeSwingPull100Px), handle.anchoredPosition.y, 2f);
            Assert.AreEqual(40f, handle.anchoredPosition.x, 2f,
                "laterally too — the path IS the point in this scheme");
        }

        [Test]
        public void TheClubHeadIsHiddenInFlight_AndBackAtIdle()
        {
            var group = _handleGo.GetComponent<CanvasGroup>();
            Down();
            PullDown(_cfg.FreeSwingPull100Px);
            SwingUp(_cfg.FreeSwingPull100Px);

            Assert.AreEqual(0f, group.alpha, 1e-4f, "the club goes away with the ball");

            SettleToIdle();
            Assert.AreEqual(1f, group.alpha, 1e-4f, "and comes back for the next shot");
        }

        // ── 7. The Fade/Draw toggle ──────────────────────────────────────────────

        [Test]
        public void ActivateHidesTheFadeDrawToggle_AndDeactivateGivesItBack()
        {
            Assert.IsFalse(_buttons.IsFadeDrawVisible);
            Assert.AreEqual(0f, _buttons.FadeDrawAlpha, 1e-4f, "by OPACITY, so SPIN does not recentre");

            _driver.Deactivate();
            Assert.IsTrue(_buttons.IsFadeDrawVisible);
            Assert.AreEqual(1f, _buttons.FadeDrawAlpha, 1e-4f);
        }

        [Test]
        public void ActivateDisarmsFadeDrawIfItWasArmed()
        {
            // Through the existing toggle path, so ShotConeView's aim lock is cleared too — half
            // an unarm would leave AimYawFor reading a stale locked heading on every shot.
            ShotModeContext.Mode = ShotMode.FadeDraw;
            _driver.Activate();
            Assert.AreEqual(ShotMode.Straight, ShotModeContext.Mode);
        }

        [Test]
        public void HidingTheToggle_LeavesTheRestOfTheRowAlone()
        {
            // The layout-group lesson: hiding by SetActive would let the row re-centre SPIN. The
            // button's own rect must be exactly where it was.
            var fdRt = (RectTransform)_fadeDrawButton.transform;
            Vector2 before = fdRt.anchoredPosition;
            _buttons.SetFadeDrawVisible(false);
            Assert.IsTrue(fdRt.gameObject.activeSelf, "still ACTIVE — only transparent");
            Assert.AreEqual(before, fdRt.anchoredPosition);
        }

        // ── 8. The result readout ────────────────────────────────────────────────

        [Test]
        public void TheChipSurvivesResolving_ButTheTraceGoesWithTheBall()
        {
            // Carry-over 7, and the Needle §10 scar: CommitExternal reaches Resolving
            // synchronously, so a chip driven off the state would be gone about two frames after
            // the shot — before a human has read a word of it.
            Down();
            PullDown(_cfg.FreeSwingPull100Px);
            SwingUp(_cfg.FreeSwingPull100Px);

            Assert.AreEqual(ShotState.Resolving, _sc.State, "harness: we really are past the shot");
            Assert.AreEqual(1f, _chip.Alpha, 1e-4f, "and the chip is still up");
            // The TRACE is the opposite case, and deliberately so (Cesar, on the first clip): the
            // chip is the result readout and stays, the finger's path belongs to the swing and
            // goes with the ball. SNAPPED, so the LIVE alpha is asserted and not just the target —
            // a fade would leave the line under the ball for a dozen frames, and EditMode never
            // pumps Update, so a target-only assertion could not tell the two apart.
            Assert.AreEqual(0f, _trace.Alpha, 1e-4f,
                            "the trace must be gone on the frame the ball is away");

            SettleToIdle();
            Assert.AreEqual(0f, _chip.Alpha, 1e-4f, "Idle is the only thing that puts the CHIP away");
            Assert.AreEqual(0f, _trace.Alpha, 1e-4f);
        }

        [Test]
        public void TheChipReportsTheCommittedIntent_NotTheFinger()
        {
            Down();
            PullDown(_cfg.FreeSwingPull100Px, seconds: 0.6f);
            SwingUp(_cfg.FreeSwingPull100Px, seconds: 0.3f);

            var v = _driver.LastVerdict;
            Assert.AreEqual(FreeSwingMath.PathKey(v.Path),   _chip.LastPathKey);
            Assert.AreEqual(FreeSwingMath.TempoKey(v.Tempo), _chip.LastTempoKey);
            // POWER reads what FIRED — the peak after the tempo multiplier — not the raw peak.
            StringAssert.Contains(Mathf.RoundToInt(v.PowerNormalized * v.TimingMul * 100f).ToString(),
                                  _chip.LastPowerText);
        }

        [Test]
        public void TheTraceDrawsTheGesture_AndIsClearedAtIdle()
        {
            Down();
            PullDown(_cfg.FreeSwingPull100Px);
            Assert.Greater(_trace.PointCount, 5, "the trace follows the finger down");

            SwingUp(_cfg.FreeSwingPull100Px);
            SettleToIdle();
            Assert.AreEqual(0, _trace.PointCount, "and is cleared when the ball settles");
        }

        [Test]
        public void TheSampleBufferIsCappedAtTheConfiguredWindow()
        {
            // The driver's OWN ring, never ShotController.PushTouchSample — that one is Flick's
            // gate, and widening it would change the shipping scheme.
            Down();
            int cap = Mathf.RoundToInt(_cfg.FreeSwingSampleWindow);
            for (int i = 0; i < cap * 3; i++) Sample(OriginX, OriginY - 100f - (i % 7), 0.008f);
            Assert.LessOrEqual(_driver.Samples.Count, cap);
        }

        // ── 9. Telemetry ─────────────────────────────────────────────────────────

        [Test]
        public void Timing01IsTheTempoScore_AndReachesTheResolvedShot()
        {
            Down();
            PullDown(_cfg.FreeSwingPull100Px, seconds: 0.6f);
            SwingUp(_cfg.FreeSwingPull100Px, seconds: 0.3f);

            // NEAR 1, not exactly 1, and the gap is sampling granularity rather than slop. The
            // reversal is detected ON a sample, and the dt that carried the finger INTO that
            // sample was still backswing time — so a nominally 0.60/0.30 gesture measures
            // 0.617/0.283 at 60 Hz, one frame of asymmetry. Asserting an exact 1 would be
            // asserting that a finger reverses between frames, which no real thumb does; what the
            // scheme actually promises is that an ideal-tempo swing is graded GOOD and pays
            // nothing, and that is what this pins.
            Assert.Greater(_driver.LastVerdict.Timing01, 0.85f, "the ideal 2:1 tempo scores near 1");
            Assert.AreEqual(FreeSwingTempo.Good, _driver.LastVerdict.Tempo);
            Assert.AreEqual(1f, _driver.LastVerdict.TimingMul, 1e-4f, "and costs no power");
            Assert.AreEqual(_driver.LastVerdict.Timing01, _sc.LastCommittedTiming01, 1e-4f,
                            "and the score reaches the resolved shot, which is what telemetry stamps");
        }

        // ── 10. Putts ────────────────────────────────────────────────────────────

        [Test]
        public void APutt_CapsAtOneHundredPercentAndNeverCurves()
        {
            _sc.IsPutt = true;
            _driver.Activate();

            Down();
            PullDown(_cfg.FreeSwingPull120Px, seconds: 0.6f);
            Assert.AreEqual(1f, _sc.PowerNormalized, 1e-3f, "no overpower on a putt");

            SwingUp(_cfg.FreeSwingPull120Px, seconds: 0.3f, bowPx: -320f);
            Assert.AreEqual(0f, _driver.LastVerdict.FadeDraw01, 1e-6f, "putts never curve");
            Assert.AreEqual(FreeSwingPath.Straight, _driver.LastVerdict.Path);
        }
    }
}
