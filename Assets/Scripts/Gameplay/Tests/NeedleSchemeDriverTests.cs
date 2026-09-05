using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using Golfin.Gameplay.Input;
using Golfin.Gameplay.Config;
using Golfin.Gameplay.UI.Controls;
using Golfin.Gameplay.UI.Controls.Needle;
using Golfin.Physics;
using Golfin.Physics.Stats;
using Golfin.Physics.Math;
using TMPro;

namespace Golfin.Gameplay.Tests
{
    /// <summary>
    /// scheme_needle §5.2 — the driver's two touches and its three exits from a swing (tap-commit,
    /// SHANK timeout, zero-power cancel), each taken exactly once, plus the intent it hands the
    /// seam and the geometry it draws while doing it.
    ///
    /// <para>NO FLICK-GATE HARNESS HERE, unlike <c>PendulumSchemeDriverTests</c>. This scheme never
    /// calls <c>EvaluateFlickGate</c> — the release is the end of the power gesture, not the shot —
    /// so the whole "pin <c>_minFlickSpeed</c> to 0 because <c>Time.unscaledTime</c> does not
    /// advance in EditMode" dance is simply not needed. That absence is itself part of what these
    /// tests pin.</para>
    /// </summary>
    [TestFixture]
    public class NeedleSchemeDriverTests
    {
        private GameObject _scGo, _rootGo, _handleGo, _circleGo, _arcGo, _catcherGo;
        private ShotController        _sc;
        private NeedleSchemeDriver    _driver;
        private NeedlePowerCircleView _circle;
        private NeedleArcView         _arc;
        private NeedleTapCatcher      _catcher;
        private TextMeshProUGUI       _hint;
        private ControlsConfig        _cfg;

        private ShotInput _lastShot;
        private int _shotCount, _rejectCount, _cancelCount;

        [SetUp]
        public void SetUp()
        {
            _scGo = new GameObject("NeedleDriverTests_SC");
            _sc   = _scGo.AddComponent<ShotController>();

            _cfg = ControlsConfig.Default;
            _sc.InjectConfig(_cfg);
            _sc.InjectStatBundle(new StatBundle(ClubStats.DefaultDriver, BallStats.Neutral,
                CharacterStats.Neutral, fp.FromInt(100), fp.FromInt(100)));

            _rootGo   = new GameObject("SchemeRoot_Needle", typeof(RectTransform));
            _handleGo = new GameObject("NeedleHandle",      typeof(RectTransform));
            _handleGo.transform.SetParent(_rootGo.transform, false);

            _circleGo = new GameObject("NeedleCircleRoot", typeof(RectTransform), typeof(CanvasGroup));
            _circleGo.transform.SetParent(_rootGo.transform, false);
            _circle = _circleGo.AddComponent<NeedlePowerCircleView>();
            _circle.ConfigureForTests(Arc(_circleGo, "Ring80"), Arc(_circleGo, "Ring100"),
                                      Arc(_circleGo, "Ring120"), Arc(_circleGo, "OverpowerCrescent"),
                                      null, null,
                                      new GameObject("Dim", typeof(RectTransform), typeof(CanvasGroup))
                                          .GetComponent<CanvasGroup>());

            _arcGo = new GameObject("NeedleArcRoot", typeof(RectTransform), typeof(CanvasGroup));
            _arcGo.transform.SetParent(_rootGo.transform, false);
            _arc = _arcGo.AddComponent<NeedleArcView>();
            // Wire the graphics the scene builder wires: a view with null zones answers 0 to every
            // angle question, which reads as a passing assertion instead of a missing one.
            _hint = new GameObject("TapHint", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            _hint.transform.SetParent(_arcGo.transform, false);
            _hint.text = "(SHOT_TAP_HINT)";      // the builder's layout placeholder
            _arc.ConfigureForTests(Arc(_arcGo, "AccuracyArc"), Arc(_arcGo, "ZoneGood"),
                                   Arc(_arcGo, "ZonePerfect"), Rect(_arcGo, "Needle"),
                                   Rect(_arcGo, "NeedleHub"), Rect(_arcGo, "TapPip"), _hint);

            _catcherGo = new GameObject("NeedleTapCatcher", typeof(RectTransform));
            _catcherGo.transform.SetParent(_rootGo.transform, false);
            _catcher = _catcherGo.AddComponent<NeedleTapCatcher>();

            _driver = _rootGo.AddComponent<NeedleSchemeDriver>();
            _driver.ConfigureForTests((RectTransform)_rootGo.transform,
                                      (RectTransform)_handleGo.transform,
                                      _circle, _arc, _catcher, null, _cfg);
            _driver.Bind(_sc);
            _driver.Activate();

            _shotCount = _rejectCount = _cancelCount = 0;
            _sc.OnShotResolved           += (s, _) => { _lastShot = s; _shotCount++; };
            ShotController.FlickRejected += OnRejected;
            ShotController.ShotCancelled += OnCancelled;
        }

        [TearDown]
        public void TearDown()
        {
            ShotController.FlickRejected -= OnRejected;
            ShotController.ShotCancelled -= OnCancelled;
            _driver.Deactivate();
            Object.DestroyImmediate(_rootGo);
            Object.DestroyImmediate(_scGo);
        }

        private void OnRejected(float _) => _rejectCount++;
        private void OnCancelled()       => _cancelCount++;

        private static NeedleArcGraphic Arc(GameObject parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            go.transform.SetParent(parent.transform, false);
            return go.AddComponent<NeedleArcGraphic>();
        }

        private static RectTransform Rect(GameObject parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent.transform, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            return rt;
        }

        // ── Harness ──────────────────────────────────────────────────────────────

        private static PointerEventData At(float x, float y)
            => new PointerEventData(EventSystem.current) { position = new Vector2(x, y) };

        private const float OriginX = 500f;
        private const float OriginY = 900f;

        /// <summary>Touch the club head and pull straight down. Derived from config, never a
        /// literal pixel count — a retune of the thresholds must not silently change what these
        /// tests are pulling to (scheme_needle carry-over 7).</summary>
        private void PullDown(float pullPx, float lateralPx = 0f)
        {
            _driver.OnPointerDown(At(OriginX, OriginY));
            _driver.OnDrag(At(OriginX + lateralPx, OriginY - pullPx));
        }

        private void Release() => _driver.OnPointerUp(At(OriginX, OriginY - 10f));

        // ── 1. Touch one: the pull ───────────────────────────────────────────────

        [Test]
        public void PullDown_EntersTimingAndPublishesPower()
        {
            PullDown((_cfg.NeedleMinUsefulPullPx + _cfg.NeedlePull100Px) * 0.5f);

            Assert.AreEqual(ShotState.Timing, _sc.State, "power > 0 must move the shot to Timing");
            Assert.AreEqual(0.5f, _sc.PowerNormalized, 1e-4f);
            Assert.IsTrue(_sc.IsExternalDragActive);
            Assert.IsFalse(_driver.IsNeedlePhase, "the needle has not started yet");
        }

        [Test]
        public void PullBelowTheDeadZone_StaysAiming()
        {
            PullDown(20f);
            Assert.AreEqual(ShotState.Aiming, _sc.State);
            Assert.AreEqual(0f, _sc.PowerNormalized, 1e-6f);
        }

        [Test]
        public void OverpowerPull_PublishesPastOneHundredPercent()
        {
            PullDown(_cfg.NeedlePull120Px);
            Assert.AreEqual(1.2f, _sc.PowerNormalized, 1e-4f);
        }

        [Test]
        public void OwnsTiming_MeansNoArrowUnderTheSwing()
        {
            PullDown(_cfg.NeedlePull100Px);
            ShotInputState last = default;
            _sc.OnStateChanged += s => last = s;
            for (int i = 0; i < 50; i++) _sc.Tick(0.1f);

            Assert.AreEqual(ShotState.Timing, _sc.State, "the pass counter must not cancel us");
            Assert.AreEqual(0f, last.ArrowProgress01, 1e-6f);
        }

        [Test]
        public void TheRings_AreDrawnWhereTheClubHeadLands()
        {
            // The circle IS the config, drawn. A ring is at HandleRestBelowBall + the threshold —
            // where the club LANDS at that power — and this asserts the DRAWN radius against the
            // CONFIG, not against a recomputation of the same formula.
            float rest = _circle.HandleRestBelowBall;
            Assert.AreEqual(rest + _cfg.NeedlePull80Px,  _circle.Ring80Radius,  2f);
            Assert.AreEqual(rest + _cfg.NeedlePull100Px, _circle.Ring100Radius, 2f);
            Assert.AreEqual(rest + _cfg.NeedlePull120Px, _circle.Ring120Radius, 2f);
        }

        // ── 2. The release: power commits, the needle starts ─────────────────────

        [Test]
        public void Release_StartsTheNeedleAtTheLeftEndAndArmsTheCatcher()
        {
            PullDown(_cfg.NeedlePull100Px);
            Assert.IsFalse(_catcher.IsArmed, "nothing to tap while the finger is still down");

            Release();

            Assert.IsTrue(_driver.IsNeedlePhase);
            Assert.AreEqual(-1f, _driver.NeedleOffset, 1e-6f, "the needle starts at the arc's left end");
            Assert.IsTrue(_catcher.IsArmed, "the tap area is live");
            Assert.AreEqual(ShotState.Timing, _sc.State, "still the controller's Timing — no seam change");
            Assert.AreEqual(0, _shotCount, "the release is NOT the shot in this scheme");
            Assert.AreEqual(0, _cancelCount);
        }

        [Test]
        public void Release_RepublishesThePeakPower_NotTheLiveOne()
        {
            _driver.OnPointerDown(At(OriginX, OriginY));
            _driver.OnDrag(At(OriginX, OriginY - _cfg.NeedlePull100Px));            // 100%
            _driver.OnDrag(At(OriginX, OriginY - _cfg.NeedleMinUsefulPullPx - 10f)); // eased back
            Assert.Less(_sc.PowerNormalized, 0.2f, "harness: the live power really did fall");

            Release();
            Assert.AreEqual(1f, _sc.PowerNormalized, 1e-4f,
                "the gauge, the map ring and the putt predictor must all show what will FIRE");
            Assert.AreEqual(1f, _driver.PeakPower, 1e-4f);
        }

        [Test]
        public void Release_ReturnsTheClubHeadToTheBall()
        {
            var handle = (RectTransform)_handleGo.transform;
            PullDown(_cfg.NeedlePull100Px);
            Assert.Less(handle.anchoredPosition.y, -1f, "harness: the club really moved down");

            Release();
            for (int i = 0; i < 30; i++) _driver.TickForTests(0.016f);
            Assert.AreEqual(0f, handle.anchoredPosition.y, 1f, "and snaps back for the tap");
        }

        [Test]
        public void ReleaseWithNoPower_CancelsSilently_AndNoNeedleStarts()
        {
            PullDown(10f);                                  // inside the dead zone
            Release();

            Assert.AreEqual(0, _shotCount);
            Assert.AreEqual(0, _rejectCount, "there is no flick gate in this scheme to reject with");
            Assert.AreEqual(1, _cancelCount);
            Assert.AreEqual(ShotState.Idle, _sc.State);
            Assert.IsFalse(_driver.IsNeedlePhase);
            Assert.IsFalse(_catcher.IsArmed);
        }

        [Test]
        public void ASlowRelease_IsNotRejected()
        {
            // Pendulum's release IS the shot, so it is gated on flick speed. This one is not: the
            // shot happens on a later tap, and gating a gentle lay-up's release would reject
            // exactly the gesture the scheme asks for.
            PullDown(_cfg.NeedlePull100Px);
            _driver.OnPointerUp(At(OriginX, OriginY - _cfg.NeedlePull100Px));   // no upward travel

            Assert.AreEqual(0, _rejectCount);
            Assert.IsTrue(_driver.IsNeedlePhase, "the needle starts regardless of how the finger left");
        }

        // ── 3. Touch two: the tap ────────────────────────────────────────────────

        [Test]
        public void TapAtTheTop_CommitsExactlyOnePerfectShot()
        {
            PullDown(_cfg.NeedlePull100Px);
            Release();
            _driver.SetNeedleForTests(0f);
            _driver.OnTap();

            Assert.AreEqual(1, _shotCount, "exactly one shot per tap");
            Assert.AreEqual(0, _cancelCount);
            Assert.AreEqual(NeedleGrade.Perfect, _driver.LastCommittedGrade);
            Assert.AreEqual(1f, _sc.LastTimingPowerMul,     1e-6f, "a PERFECT pays no timing penalty");
            Assert.AreEqual(1f, _sc.LastCommittedTiming01,  1e-6f, "timing01 = 1 - |n| = 1");
            Assert.IsTrue(_sc.LastShotWasClean, "and carries zero error yaw");
            Assert.AreEqual(1f, _driver.LastCommittedPower, 1e-4f);
            Assert.AreEqual(ShotState.Resolving, _sc.State);
            Assert.IsFalse(_catcher.IsArmed, "the tap area closes with the shot");
        }

        [Test]
        public void TapLate_IsASliceThatGoesRight()
        {
            PullDown(_cfg.NeedlePull100Px);
            Release();
            _driver.SetNeedleForTests(0.9f);
            _driver.OnTap();

            Assert.AreEqual(1, _shotCount);
            Assert.AreEqual(NeedleGrade.Slice, _driver.LastCommittedGrade);
            Assert.Greater(_driver.LastCommittedErrorYawRad, 0f, "SLICE goes right");
            Assert.IsFalse(_sc.LastShotWasClean);
            Assert.AreEqual(0.1f, _sc.LastCommittedTiming01, 1e-4f);
        }

        [Test]
        public void TapEarly_IsAHookThatGoesLeft_AndMirrorsTheSlice()
        {
            PullDown(_cfg.NeedlePull100Px);
            Release();
            _driver.SetNeedleForTests(-0.9f);
            _driver.OnTap();
            float hookYaw = _driver.LastCommittedErrorYawRad;
            _sc.CompleteShot();

            PullDown(_cfg.NeedlePull100Px);
            Release();
            _driver.SetNeedleForTests(0.9f);
            _driver.OnTap();

            Assert.AreEqual(NeedleGrade.Slice, _driver.LastCommittedGrade);
            Assert.Less(hookYaw, 0f, "HOOK goes left");
            Assert.AreEqual(-hookYaw, _driver.LastCommittedErrorYawRad, 1e-6f);
        }

        [Test]
        public void ATapWithNoNeedlePhase_DoesNothing()
        {
            _driver.OnTap();                                 // at Idle
            Assert.AreEqual(0, _shotCount);

            PullDown(_cfg.NeedlePull100Px);
            _driver.OnTap();                                 // mid-pull
            Assert.AreEqual(0, _shotCount, "the first touch is the pull, not a tap");
        }

        [Test]
        public void ASecondTap_CannotFireASecondShot()
        {
            PullDown(_cfg.NeedlePull100Px);
            Release();
            _driver.SetNeedleForTests(0f);
            _driver.OnTap();
            _driver.OnTap();
            _driver.OnTap();

            Assert.AreEqual(1, _shotCount, "one swing is one shot, whatever the thumb does after");
        }

        // ── 4. No tap: the SHANK timeout ─────────────────────────────────────────

        [Test]
        public void NoTap_ShanksExactlyOnceWhenTheNeedleRunsOut()
        {
            PullDown(_cfg.NeedlePull100Px);
            Release();

            float sweep = _driver.SweepSeconds;
            Assert.Greater(sweep, 0f, "harness: the sweep time was computed at the release");

            // Derived from the sweep, never a frame count: a count pinned to today's 1.2s would
            // silently under-tick the moment the needle is retuned (the Pendulum scar).
            int budget = Mathf.CeilToInt(sweep * 2f / 0.01f);
            for (int i = 0; i < budget && _shotCount == 0; i++) _driver.TickForTests(0.01f);

            Assert.AreEqual(1, _shotCount, "not tapping is a SHANK, not an escape");
            Assert.AreEqual(NeedleGrade.Shank, _driver.LastCommittedGrade);
            Assert.AreEqual(_cfg.TimingPowerMulRed, _sc.LastTimingPowerMul, 1e-6f);
            Assert.AreEqual(0f, _sc.LastCommittedTiming01, 1e-6f);
            Assert.Greater(_driver.LastCommittedErrorYawRad, 0f, "and it goes right");
            Assert.IsFalse(_catcher.IsArmed);

            // And it does not keep firing.
            for (int i = 0; i < 200; i++) _driver.TickForTests(0.01f);
            Assert.AreEqual(1, _shotCount);
        }

        [Test]
        public void TheNeedle_TakesTheWholeSweepToCrossTheArc()
        {
            PullDown(_cfg.NeedlePull100Px);
            Release();
            float sweep = _driver.SweepSeconds;

            // Half the sweep should put it at the top, within a frame's travel.
            int half = Mathf.RoundToInt(sweep * 0.5f / 0.01f);
            for (int i = 0; i < half; i++) _driver.TickForTests(0.01f);
            Assert.AreEqual(0f, _driver.NeedleOffset, 0.05f,
                "half a sweep from the left end is the top of the arc");
        }

        [Test]
        public void TheNeedle_DoesNotMoveBeforeTheRelease()
        {
            PullDown(_cfg.NeedlePull100Px);
            for (int i = 0; i < 100; i++) _driver.TickForTests(0.02f);
            Assert.AreEqual(-1f, _driver.NeedleOffset, 1e-6f, "the pull phase has no clock");
            Assert.AreEqual(0, _shotCount);
        }

        // ── 5. The peak-power carry-over ─────────────────────────────────────────

        [Test]
        public void PeakPower_SurvivesAPartialRelease()
        {
            _driver.OnPointerDown(At(OriginX, OriginY));
            _driver.OnDrag(At(OriginX, OriginY - _cfg.NeedlePull100Px));    // 100%
            _driver.OnDrag(At(OriginX, OriginY - (_cfg.NeedleMinUsefulPullPx + _cfg.NeedlePull100Px) * 0.5f));
            Release();
            _driver.SetNeedleForTests(0f);
            _driver.OnTap();

            Assert.AreEqual(1, _shotCount);
            Assert.AreEqual(1f, _driver.LastCommittedPower, 1e-4f,
                "the deepest pull is the shot, exactly as ClubHandleDragger's _peakPower is");
        }

        [Test]
        public void PullingPastTheOriginOnRelease_StillCommitsThePeak()
        {
            _driver.OnPointerDown(At(OriginX, OriginY));
            _driver.OnDrag(At(OriginX, OriginY - _cfg.NeedlePull100Px));
            for (int i = 1; i <= 4; i++) _driver.OnDrag(At(OriginX, OriginY + 250f * i));
            Assert.AreEqual(0f, _sc.PowerNormalized, 1e-4f, "the LIVE power really is zero by now");

            _driver.OnPointerUp(At(OriginX, OriginY + 1000f));
            _driver.SetNeedleForTests(0f);
            _driver.OnTap();

            Assert.AreEqual(1, _shotCount);
            Assert.AreEqual(1f, _driver.LastCommittedPower, 1e-4f);
        }

        // ── 6. The drawn target is the graded one ────────────────────────────────

        [Test]
        public void PullingDeeper_NarrowsTheDrawnZones()
        {
            _driver.OnPointerDown(At(OriginX, OriginY));
            _driver.OnDrag(At(OriginX, OriginY - _cfg.NeedleMinUsefulPullPx - 20f));   // a gentle pull
            float widePerfect = _arc.PerfectHalfAngleDeg;
            float wideGood    = _arc.GoodHalfAngleDeg;
            Assert.Greater(widePerfect, 0f, "harness: the zones are actually drawn");

            _driver.OnDrag(At(OriginX, OriginY - _cfg.NeedlePull120Px));               // all the way
            Assert.Less(_arc.PerfectHalfAngleDeg, widePerfect, "the blue zone must close as you pull");
            Assert.Less(_arc.GoodHalfAngleDeg,    wideGood,    "and so must the amber one");
            Assert.Greater(_arc.GoodHalfAngleDeg, _arc.PerfectHalfAngleDeg,
                "amber stays wider than blue throughout");
        }

        [Test]
        public void TheGradedWindow_IsTheOneThatWasDrawn()
        {
            _driver.OnPointerDown(At(OriginX, OriginY));
            _driver.OnDrag(At(OriginX, OriginY - _cfg.NeedlePull120Px));   // 120% — the narrowest

            // The drawn half-angle, converted back to the window it represents, IS the window the
            // grade uses. This is what ties the picture to the verdict.
            Assert.AreEqual(_driver.PerfectZone01,
                            _arc.PerfectHalfAngleDeg / NeedleMath.ArcHalfSweepDeg, 1e-4f);
            Assert.AreEqual(_driver.GoodZone01,
                            _arc.GoodHalfAngleDeg / NeedleMath.ArcHalfSweepDeg, 1e-4f);

            Release();
            _driver.SetNeedleForTests(_driver.PerfectZone01 * 0.9f);       // just inside the blue
            _driver.OnTap();
            Assert.AreEqual(NeedleGrade.Perfect, _driver.LastCommittedGrade);
            Assert.AreEqual(1f, _sc.LastTimingPowerMul, 1e-6f);
        }

        [Test]
        public void TheZonesHold_ThroughTheNeedlePhase()
        {
            _driver.OnPointerDown(At(OriginX, OriginY));
            _driver.OnDrag(At(OriginX, OriginY - _cfg.NeedlePull120Px));
            float narrow = _arc.PerfectHalfAngleDeg;

            Release();
            for (int i = 0; i < 10; i++) _driver.TickForTests(0.01f);
            Assert.AreEqual(narrow, _arc.PerfectHalfAngleDeg, 1e-4f,
                "the target must not re-open under the player while they are aiming at it");
        }

        [Test]
        public void TheZonesReopen_ForTheNextSwing()
        {
            _driver.OnPointerDown(At(OriginX, OriginY));
            _driver.OnDrag(At(OriginX, OriginY - _cfg.NeedlePull120Px));
            float narrow = _arc.PerfectHalfAngleDeg;
            Release();
            _driver.SetNeedleForTests(0f);
            _driver.OnTap();

            _sc.CompleteShot();
            _sc.Tick(0.016f);                     // PublishState -> OnStateChanged(Idle)
            Assert.Greater(_arc.PerfectHalfAngleDeg, narrow,
                "a swing that is over must not leave the next one holding its narrowed target");
        }

        [Test]
        public void TheNeedle_RotatesLeftForNegativeOffsets()
        {
            // The on-screen direction, not the number that was passed in: getting this backwards
            // would swap HOOK and SLICE visually while every grade assertion still passed.
            PullDown(_cfg.NeedlePull100Px);
            Release();

            _driver.SetNeedleForTests(-1f);
            Assert.AreEqual(90f, Mathf.DeltaAngle(0f, _arc.NeedleRotationDeg), 0.01f,
                "n = -1 puts the needle at the arc's LEFT end (+90 deg CCW in Unity)");

            _driver.SetNeedleForTests(1f);
            Assert.AreEqual(-90f, Mathf.DeltaAngle(0f, _arc.NeedleRotationDeg), 0.01f);

            _driver.SetNeedleForTests(0f);
            Assert.AreEqual(0f, Mathf.DeltaAngle(0f, _arc.NeedleRotationDeg), 0.01f, "straight up");
        }

        // ── 7. Fade/Draw and putt ────────────────────────────────────────────────

        [Test]
        public void Straight_LateralPullIsIgnored()
        {
            _sc.FadeDrawActive = false;

            PullDown(_cfg.NeedlePull100Px, lateralPx: _cfg.NeedleCurveHalfWidthPx);
            Release(); _driver.SetNeedleForTests(0f); _driver.OnTap();
            var straight = _lastShot;
            _sc.CompleteShot();

            PullDown(_cfg.NeedlePull100Px, lateralPx: 0f);
            Release(); _driver.SetNeedleForTests(0f); _driver.OnTap();

            Assert.AreEqual(straight.velocity.x.raw, _lastShot.velocity.x.raw,
                "in Straight mode a lateral drag must change NOTHING about the shot");
            Assert.AreEqual(straight.velocity.z.raw, _lastShot.velocity.z.raw);
        }

        [Test]
        public void FadeDrawArmed_LateralPullBecomesTheCurve()
        {
            _sc.FadeDrawActive = true;

            PullDown(_cfg.NeedlePull100Px, lateralPx: 0f);
            Release(); _driver.SetNeedleForTests(0f); _driver.OnTap();
            var noCurve = _lastShot;
            _sc.CompleteShot();

            PullDown(_cfg.NeedlePull100Px, lateralPx: _cfg.NeedleCurveHalfWidthPx);   // full +1
            Release(); _driver.SetNeedleForTests(0f); _driver.OnTap();

            Assert.AreNotEqual(noCurve.Spin.Axis.y.raw, _lastShot.Spin.Axis.y.raw,
                "a full lateral pull with Fade/Draw armed must shape the shot");
        }

        [Test]
        public void Putt_CapsAtOneHundredPercent_AndSweepsSlower()
        {
            _sc.IsPutt = true;
            _driver.Deactivate();
            _driver.Activate();                  // re-lays the circle and arc out for putt mode

            PullDown(_cfg.NeedlePull120Px);
            Assert.AreEqual(1f, _sc.PowerNormalized, 1e-4f, "a putt cannot overpower");

            Release();
            Assert.AreEqual(NeedleMath.SweepSeconds(_sc.CharacterClubControl, 1f,
                                                    _sc.OverpowerForgiveness01, true, _cfg),
                            _driver.SweepSeconds, 1e-4f);
            Assert.Greater(_driver.SweepSeconds,
                           NeedleMath.SweepSeconds(_sc.CharacterClubControl, 1f,
                                                   _sc.OverpowerForgiveness01, false, _cfg),
                           "a putt's needle is slower than a swing's");

            _driver.SetNeedleForTests(0f);
            _driver.OnTap();
            Assert.AreEqual(1, _shotCount);
        }

        [Test]
        public void Putt_FlattensTheArcAndDropsTheOverpowerRings()
        {
            _sc.IsPutt = true;
            _driver.Deactivate();
            _driver.Activate();

            Assert.Less(_arc.ArcRadiusY, _arc.ArcRadiusX, "the Putt frame flattens the arc to 460x300");
            Assert.AreEqual(_circle.HandleRestBelowBall + _cfg.NeedlePull100Px,
                            _circle.Ring100Radius, 2f, "the 100% ring stays");
        }

        // ── 8. The club head ─────────────────────────────────────────────────────

        [Test]
        public void TheClubHead_DisappearsWhileTheBallIsInFlight()
        {
            var group = _handleGo.GetComponent<CanvasGroup>();
            Assert.IsNotNull(group, "the driver binds a CanvasGroup on the handle");
            Assert.AreEqual(1f, group.alpha, 1e-6f, "visible at Idle");

            PullDown(_cfg.NeedlePull100Px);
            Release();
            Assert.AreEqual(1f, group.alpha, 1e-6f, "and all through the needle phase");

            _driver.SetNeedleForTests(0f);
            _driver.OnTap();
            Assert.AreEqual(0f, group.alpha, 1e-6f, "gone once the ball is away");

            _sc.CompleteShot();
            _sc.Tick(0.016f);
            Assert.AreEqual(1f, group.alpha, 1e-6f, "and back for the next shot");
        }

        // ── 8b. The result readout outlives the shot ─────────────────────────────

        [Test]
        public void TheArc_IsNotToldAboutResolving_SoTheResultStaysReadable()
        {
            // The shared fading view drops its target at Resolving — right for the Pendulum's bar,
            // which is stale once the ball is away, and wrong here: the frozen needle, the pip and
            // the zone the tap landed in ARE the result readout. CommitExternal reaches Resolving
            // synchronously, so forwarding it faded the arc out about two frames after the tap.
            // The acceptance capture measured the arc's navy at (34,55,53) against its own
            // (10,38,55), and at (70,93,42) — grass — one shot later.
            PullDown(_cfg.NeedlePull100Px);
            Release();
            _driver.SetNeedleForTests(0f);
            _driver.OnTap();

            Assert.AreEqual(ShotState.Resolving, _sc.State, "harness: the commit really did resolve");
            Assert.AreNotEqual(ShotState.Resolving, _driver.LastStateForwardedToArc,
                "Resolving must never reach the arc — it is what puts the readout away");

            _sc.CompleteShot();
            _sc.Tick(0.016f);
            Assert.AreEqual(ShotState.Idle, _driver.LastStateForwardedToArc,
                "and Idle must, or the arc would hang over the next shot");
        }

        // ── 8c. Zero hardcoded text ──────────────────────────────────────────────

        [Test]
        public void TheTapHint_ResolvesItsKey_NotTheBuildersPlaceholder()
        {
            // The builder authors a word so the object is visible while it is being laid out, and
            // for one iteration that placeholder was what shipped — the UI fidelity linter's
            // unlocalized-text warning is what caught it. The prompt resolves SHOT_TAP_HINT at
            // SHOW time, so what is asserted here is the KEY's own value.
            PullDown(_cfg.NeedlePull100Px);
            Assert.AreEqual("(SHOT_TAP_HINT)", _hint.text, "harness: still the placeholder mid-pull");

            Release();
            Assert.AreEqual(LocalizationManager.Get(NeedleMath.KeyTapHint), _arc.TapHintText,
                "the prompt must read the localised value of SHOT_TAP_HINT");
            Assert.AreNotEqual("(SHOT_TAP_HINT)", _arc.TapHintText);
        }

        // ── 9. Scheme identity ───────────────────────────────────────────────────

        [Test]
        public void Driver_ReportsItselfImplemented()
        {
            Assert.AreEqual(ControlScheme.Needle, _driver.Scheme);
            Assert.IsTrue(_driver.IsImplemented,
                "which is what turns SchemeRoot_Flick OFF for this scheme");
        }
    }
}
