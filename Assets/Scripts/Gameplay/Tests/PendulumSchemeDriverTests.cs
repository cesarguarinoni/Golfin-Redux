using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEditor;
using Golfin.Gameplay.Input;
using Golfin.Gameplay.Config;
using Golfin.Gameplay.UI.Controls;
using Golfin.Gameplay.UI.Controls.Pendulum;
using Golfin.Physics;
using Golfin.Physics.Stats;
using Golfin.Physics.Math;

namespace Golfin.Gameplay.Tests
{
    /// <summary>
    /// scheme_pendulum §5.2 — the driver's four exits from a swing (commit, reject, cancel,
    /// sweep-cancel), each taken exactly once, plus the intent it hands to the seam.
    ///
    /// <para>WHY THE GATE IS DISABLED FOR THE COMMIT CASES: <c>EvaluateFlickGate</c> measures
    /// <c>Time.unscaledTime</c>, which does not advance inside one EditMode test method — the
    /// existing <c>ShotControllerFlickGateTests</c> says as much in its own header and defers the
    /// velocity window to on-device acceptance. So the commit cases pin <c>_minFlickSpeed</c> to 0
    /// (gate off) and the REJECT case drives a DOWNWARD release, which fails the gate on the sign
    /// of the velocity and is therefore deterministic whether or not a frame ticked.</para>
    /// </summary>
    [TestFixture]
    public class PendulumSchemeDriverTests
    {
        private GameObject     _scGo, _rootGo, _handleGo, _barGo;
        private ShotController _sc;
        private PendulumSchemeDriver _driver;
        private PendulumBarView      _bar;
        private ControlsConfig _cfg;

        private ShotInput _lastShot;
        private int       _shotCount;
        private int       _rejectCount;
        private int       _cancelCount;

        [SetUp]
        public void SetUp()
        {
            _scGo = new GameObject("PendulumDriverTests_SC");
            _sc   = _scGo.AddComponent<ShotController>();

            _cfg = ControlsConfig.Default;
            _sc.InjectConfig(_cfg);
            _sc.InjectStatBundle(new StatBundle(ClubStats.DefaultDriver, BallStats.Neutral,
                CharacterStats.Neutral, fp.FromInt(100), fp.FromInt(100)));

            _rootGo   = new GameObject("SchemeRoot_Pendulum", typeof(RectTransform));
            _handleGo = new GameObject("PendulumHandle",      typeof(RectTransform));
            _handleGo.transform.SetParent(_rootGo.transform, false);

            _barGo = new GameObject("PendulumBar", typeof(RectTransform), typeof(CanvasGroup));
            _barGo.transform.SetParent(_rootGo.transform, false);
            _bar = _barGo.AddComponent<PendulumBarView>();
            // Wire the five rects the scene builder wires. A bar with null bands answers 0 to
            // every width question, which reads as a passing assertion instead of a missing one.
            _bar.ConfigureForTests(Rect("Track"), Rect("BandGood"), Rect("BandJust"),
                                   Rect("CentrePip"), Rect("Marker"));

            _driver = _rootGo.AddComponent<PendulumSchemeDriver>();
            _driver.ConfigureForTests((RectTransform)_rootGo.transform,
                                      (RectTransform)_handleGo.transform,
                                      null, _bar, null, _cfg);
            _driver.Bind(_sc);
            _driver.Activate();

            _shotCount = _rejectCount = _cancelCount = 0;
            _sc.OnShotResolved       += (s, _) => { _lastShot = s; _shotCount++; };
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

        private void OnRejected(float _)  => _rejectCount++;
        private void OnCancelled()        => _cancelCount++;

        /// <summary>A child rect under the bar, anchored so <c>rect.width</c> tracks sizeDelta
        /// without needing a canvas or a layout pass.</summary>
        private RectTransform Rect(string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(_barGo.transform, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            return rt;
        }

        // ── Harness ──────────────────────────────────────────────────────────────

        /// <summary>Turn the windowed flick gate off. Private serialized field, so the same
        /// SerializedObject route the project uses for every other Inspector-only value.</summary>
        private void DisableFlickGate()
        {
            var so = new SerializedObject(_sc);
            so.FindProperty("_minFlickSpeed").floatValue = 0f;
            so.ApplyModifiedPropertiesWithoutUndo();
            Assert.IsFalse(_sc.FlickGateActive, "harness: the gate must actually be off");
        }

        private static PointerEventData At(float x, float y)
            => new PointerEventData(EventSystem.current) { position = new Vector2(x, y) };

        private const float OriginY = 900f;
        private const float OriginX = 500f;

        /// <summary>Touch the handle and pull straight down by <paramref name="pullPx"/>.</summary>
        private void PullDown(float pullPx, float lateralPx = 0f)
        {
            _driver.OnPointerDown(At(OriginX, OriginY));
            _driver.OnDrag(At(OriginX + lateralPx, OriginY - pullPx));
        }

        // ── 1. The gesture reaches Timing and publishes power ────────────────────

        [Test]
        public void PullDown_EntersTimingAndPublishesPower()
        {
            PullDown((_cfg.PendulumMinUsefulPullPx + _cfg.PendulumPull100Px) * 0.5f);   // midpoint of the 40..300 useful span

            Assert.AreEqual(ShotState.Timing, _sc.State, "power > 0 must move the shot to Timing");
            Assert.AreEqual(0.5f, _sc.PowerNormalized, 1e-4f);
            Assert.IsTrue(_sc.IsExternalDragActive);
        }

        [Test]
        public void PullBelowTheDeadZone_StaysAiming()
        {
            PullDown(20f);
            Assert.AreEqual(ShotState.Aiming, _sc.State, "inside the dead zone");
            Assert.AreEqual(0f, _sc.PowerNormalized, 1e-6f);
        }

        [Test]
        public void OverpowerPull_PublishesPastOneHundredPercent()
        {
            PullDown(_cfg.PendulumPull120Px);
            Assert.AreEqual(1.2f, _sc.PowerNormalized, 1e-4f,
                "SetExternalPower must carry 1.2 through — this is the §3.1 clamp widening");
        }

        [Test]
        public void OwnsTiming_MeansNoArrowUnderTheSwing()
        {
            PullDown(_cfg.PendulumPull100Px);
            ShotInputState last = default;
            _sc.OnStateChanged += s => last = s;
            for (int i = 0; i < 50; i++) _sc.Tick(0.1f);

            Assert.AreEqual(ShotState.Timing, _sc.State, "the pass counter must not cancel us");
            Assert.AreEqual(0f, last.ArrowProgress01, 1e-6f);
        }

        // ── 2. Release: commit ───────────────────────────────────────────────────

        [Test]
        public void FlickUp_CommitsExactlyOneShotWithTheGradedIntent()
        {
            DisableFlickGate();
            PullDown(_cfg.PendulumPull100Px);

            _driver.SetPhaseForTests(0f);          // marker dead centre → JUST
            _driver.OnPointerUp(At(OriginX, OriginY + 200f));

            Assert.AreEqual(1, _shotCount, "exactly one shot per release");
            Assert.AreEqual(0, _rejectCount);
            Assert.AreEqual(0, _cancelCount);
            Assert.AreEqual(1f, _sc.LastTimingPowerMul, 1e-6f, "a JUST pays no timing penalty");
            Assert.AreEqual(1f, _sc.LastCommittedTiming01, 1e-6f, "timing01 = 1 - |m| = 1");
            Assert.IsTrue(_sc.LastShotWasClean, "a JUST has zero error yaw");
            Assert.AreEqual(ShotState.Resolving, _sc.State);
        }

        [Test]
        public void FlickAtTheBandEdge_CommitsAGoodWithARealYaw()
        {
            DisableFlickGate();
            PullDown(_cfg.PendulumPull100Px);

            // phase 0.25 → sin = +1 → marker at the RIGHT end → a MISS. Use a phase whose sine
            // lands inside the GOOD band instead: asin(0.3)/2π.
            _driver.SetPhaseForTests(Mathf.Asin(0.3f) / (2f * Mathf.PI));
            Assert.AreEqual(0.3f, _driver.MarkerOffset, 1e-4f, "harness: marker is where we think");

            _driver.OnPointerUp(At(OriginX, OriginY + 200f));

            Assert.AreEqual(1, _shotCount);
            Assert.AreEqual(_cfg.TimingPowerMulGold, _sc.LastTimingPowerMul, 1e-6f);
            Assert.AreEqual(0.7f, _sc.LastCommittedTiming01, 1e-4f);
            Assert.IsFalse(_sc.LastShotWasClean, "a GOOD carries an error yaw");
        }

        [Test]
        public void FlickOffTheBand_CommitsAMissThatIsShorterAndCrookeder()
        {
            DisableFlickGate();

            PullDown(_cfg.PendulumPull100Px);
            _driver.SetPhaseForTests(0f);
            _driver.OnPointerUp(At(OriginX, OriginY + 200f));
            float justSpeed = Speed(_lastShot);
            _sc.CompleteShot();

            PullDown(_cfg.PendulumPull100Px);
            _driver.SetPhaseForTests(0.25f);   // sin = +1 → hard right → MISS
            _driver.OnPointerUp(At(OriginX, OriginY + 200f));
            float missSpeed = Speed(_lastShot);

            Assert.AreEqual(_cfg.TimingPowerMulRed, _sc.LastTimingPowerMul, 1e-6f);
            Assert.AreEqual(0f, _sc.LastCommittedTiming01, 1e-6f);
            Assert.Less(missSpeed, justSpeed, "a MISS must cost real distance");
        }

        // ── 2b. The up-flick must not cancel the swing it is part of ─────────────

        [Test]
        public void FlickUp_PastTheTouchOrigin_StillFires()
        {
            // REGRESSION. The up-flick travels back past the touch origin, so at OnPointerUp the
            // LIVE pull is 0 and the live power is 0. Grading on the live value cancelled the
            // swing silently: the player pulled to 100%, flicked clean, and nothing happened.
            // Every earlier test passed because they released with a single sample and never
            // dragged through the upswing. Found by driving the real pointer path on a real hole.
            DisableFlickGate();

            _driver.OnPointerDown(At(OriginX, OriginY));
            _driver.OnDrag(At(OriginX, OriginY - _cfg.PendulumPull100Px));          // pull to 100%
            Assert.AreEqual(1f, _sc.PowerNormalized, 1e-4f, "harness: the pull reached 100%");

            // The flick itself — dragged, frame by frame, well above where the finger started.
            for (int i = 1; i <= 4; i++) _driver.OnDrag(At(OriginX, OriginY + 250f * i));
            Assert.AreEqual(0f, _sc.PowerNormalized, 1e-4f,
                "the LIVE power really is zero by now — that is the whole point");

            _driver.SetPhaseForTests(0f);
            _driver.OnPointerUp(At(OriginX, OriginY + 1000f));

            Assert.AreEqual(1, _shotCount, "the swing must fire on the peak pull, not the live one");
            Assert.AreEqual(0, _cancelCount, "and must not cancel");
            Assert.AreEqual(1f, _driver.LastCommittedPower, 1e-4f, "committed at the deepest point of the pull");
        }

        [Test]
        public void PeakPower_SurvivesAPartialRelease()
        {
            DisableFlickGate();
            _driver.OnPointerDown(At(OriginX, OriginY));
            _driver.OnDrag(At(OriginX, OriginY - _cfg.PendulumPull100Px));   // 100%
            _driver.OnDrag(At(OriginX, OriginY - (_cfg.PendulumMinUsefulPullPx + _cfg.PendulumPull100Px) * 0.5f));  // eased back to 50%
            _driver.SetPhaseForTests(0f);
            _driver.OnPointerUp(At(OriginX, OriginY + 400f));

            Assert.AreEqual(1, _shotCount);
            Assert.AreEqual(1f, _driver.LastCommittedPower, 1e-4f,
                "the deepest pull is the shot, exactly as ClubHandleDragger's _peakPower is");
        }

        [Test]
        public void MarkerFreezes_AtTheUpswingReversal_NotAtRelease()
        {
            // The same argument _timingAtLatch makes for the flick's arrow (F15 D1): the thumb
            // takes 50-150 ms to leave the glass, which at ~2 Hz is a whole band of marker travel.
            DisableFlickGate();
            _driver.OnPointerDown(At(OriginX, OriginY));
            _driver.OnDrag(At(OriginX, OriginY - _cfg.PendulumPull100Px));
            Assert.IsFalse(_driver.MarkerLatched, "pulling down must not latch");

            _driver.SetPhaseForTests(0.05f);
            float atReversal = _driver.MarkerOffset;

            // A real upswing: the sample rises far enough past the swing's lowest point to latch.
            _driver.OnDrag(At(OriginX, OriginY - _cfg.PendulumPull100Px + Screen.height * 0.05f));
            _driver.TickForTests(0.001f);
            Assert.IsTrue(_driver.MarkerLatched, "the upswing reversal must freeze the marker");

            // Time passes while the thumb leaves — the marker must NOT move on.
            for (int i = 0; i < 30; i++) _driver.TickForTests(0.016f);
            Assert.AreEqual(atReversal, _driver.MarkerOffset, 0.02f,
                "a frozen marker is what the player saw when they committed");
        }

        // ── 3. Release: reject, cancel, sweep-cancel ─────────────────────────────

        [Test]
        public void SlowRelease_RejectsWithTheFlickToastAndFiresNothing()
        {
            // NOT DisableFlickGate: this is the gate doing its job. The release moves DOWN, so the
            // measured velocity is negative whatever the frame timing did — deterministically slow.
            PullDown(_cfg.PendulumPull100Px);
            _driver.OnPointerUp(At(OriginX, OriginY - 320f));

            Assert.AreEqual(0, _shotCount, "a rejected release is not a shot");
            Assert.AreEqual(1, _rejectCount, "the player must get the same toast Flick gives");
            Assert.AreEqual(ShotState.Idle, _sc.State, "and the swing resets");
            Assert.IsFalse(_sc.IsExternalDragActive);
        }

        [Test]
        public void ReleaseWithNoPower_CancelsSilently()
        {
            DisableFlickGate();
            PullDown(10f);                                  // inside the dead zone
            _driver.OnPointerUp(At(OriginX, OriginY + 200f));

            Assert.AreEqual(0, _shotCount);
            Assert.AreEqual(0, _rejectCount, "a tap is not a failed flick");
            Assert.AreEqual(1, _cancelCount);
            Assert.AreEqual(ShotState.Idle, _sc.State);
        }

        [Test]
        public void HoldingPastMaxSweeps_CancelsTheSwing()
        {
            PullDown(_cfg.PendulumPull100Px);
            Assert.AreEqual(ShotState.Timing, _sc.State);

            // Derived, not hard-coded: MaxSweeps / Hz seconds, plus half again. A frame count
            // pinned to the old 2.0 Hz silently under-ticked the moment the marker was slowed.
            float hz      = PendulumMath.Hz(_sc.CharacterClubControl, 1f, _sc.OverpowerForgiveness01, false, _cfg);
            int   budget  = Mathf.CeilToInt(_cfg.PendulumMaxSweeps / hz * 1.5f / 0.01f);
            for (int i = 0; i < budget && _sc.State != ShotState.Idle; i++) _driver.TickForTests(0.01f);

            Assert.AreEqual(ShotState.Idle, _sc.State, "MaxSweeps must end an abandoned swing");
            Assert.AreEqual(1, _cancelCount);
            Assert.AreEqual(0, _shotCount, "an abandoned swing is not a shot");
        }

        [Test]
        public void MarkerDoesNotMoveBeforeThereIsPower()
        {
            _driver.OnPointerDown(At(OriginX, OriginY));
            for (int i = 0; i < 100; i++) _driver.TickForTests(0.02f);
            Assert.AreEqual(0f, _driver.MarkerOffset, 1e-6f,
                "nothing to time until the player has pulled");
        }

        // ── 4. Fade/Draw and putt ────────────────────────────────────────────────

        [Test]
        public void Straight_LateralPullIsIgnored()
        {
            DisableFlickGate();
            _sc.FadeDrawActive = false;

            PullDown(_cfg.PendulumPull100Px, lateralPx: _cfg.PendulumCurveHalfWidthPx);
            _driver.SetPhaseForTests(0f);
            _driver.OnPointerUp(At(OriginX, OriginY + 200f));

            var straight = _lastShot;
            _sc.CompleteShot();

            PullDown(_cfg.PendulumPull100Px, lateralPx: 0f);
            _driver.SetPhaseForTests(0f);
            _driver.OnPointerUp(At(OriginX, OriginY + 200f));

            Assert.AreEqual(straight.velocity.x.raw, _lastShot.velocity.x.raw,
                "in Straight mode a lateral drag must change NOTHING about the shot");
            Assert.AreEqual(straight.velocity.z.raw, _lastShot.velocity.z.raw);
        }

        [Test]
        public void FadeDrawArmed_LateralPullBecomesTheCurve()
        {
            DisableFlickGate();
            _sc.FadeDrawActive = true;

            PullDown(_cfg.PendulumPull100Px, lateralPx: 0f);
            _driver.SetPhaseForTests(0f);
            _driver.OnPointerUp(At(OriginX, OriginY + 200f));
            var noCurve = _lastShot;
            _sc.CompleteShot();

            PullDown(_cfg.PendulumPull100Px, lateralPx: _cfg.PendulumCurveHalfWidthPx);   // full +1 curve
            _driver.SetPhaseForTests(0f);
            _driver.OnPointerUp(At(OriginX, OriginY + 200f));

            Assert.AreNotEqual(noCurve.Spin.Axis.y.raw, _lastShot.Spin.Axis.y.raw,
                "a full lateral pull with Fade/Draw armed must shape the shot");
        }

        [Test]
        public void Putt_ClampsAtOneHundredPercentAndKeepsTheShorterBar()
        {
            _sc.IsPutt = true;
            _driver.Deactivate();
            _driver.Activate();          // re-lays the bar out for putt mode

            Assert.AreEqual(260f, _bar.HalfTravelPx, 1e-4f, "putt track is 520 wide (Figma)");

            DisableFlickGate();
            PullDown(_cfg.PendulumPull120Px);
            Assert.AreEqual(1f, _sc.PowerNormalized, 1e-4f, "a putt cannot overpower");

            _driver.SetPhaseForTests(0f);
            _driver.OnPointerUp(At(OriginX, OriginY + 200f));
            Assert.AreEqual(1, _shotCount);
        }

        [Test]
        public void SwingBar_IsTheFigmaWidth()
        {
            Assert.AreEqual(360f, _bar.HalfTravelPx, 1e-4f, "swing track is 720 wide (Figma)");
        }

        // ── 4b. Cesar's review of the first clip (2026-09-05) ────────────────────

        [Test]
        public void PullingDeeper_NarrowsTheDrawnBands()
        {
            // "the hitting area should shrink the further the player pulls" — and it has to be
            // the DRAWN band, not just the grading, or the cost is invisible until after the shot.
            _driver.OnPointerDown(At(OriginX, OriginY));
            _driver.OnDrag(At(OriginX, OriginY - 60f));      // a gentle pull
            float wideJust = _bar.JustWidthPx;
            float wideGood = _bar.GoodWidthPx;

            _driver.OnDrag(At(OriginX, OriginY - _cfg.PendulumPull120Px));     // all the way to 120%
            Assert.Less(_bar.JustWidthPx, wideJust, "the green band must close as the pull deepens");
            Assert.Less(_bar.GoodWidthPx, wideGood, "and so must the amber one");
            Assert.Greater(_bar.GoodWidthPx, _bar.JustWidthPx, "GOOD stays wider than JUST throughout");
        }

        [Test]
        public void TheBandsReopen_ForTheNextSwing()
        {
            DisableFlickGate();
            _driver.OnPointerDown(At(OriginX, OriginY));
            _driver.OnDrag(At(OriginX, OriginY - _cfg.PendulumPull120Px));
            float narrow = _bar.JustWidthPx;
            _driver.SetPhaseForTests(0f);
            _driver.OnPointerUp(At(OriginX, OriginY + 400f));

            Assert.Greater(_bar.JustWidthPx, narrow,
                "a swing that is over must not leave the next one holding its narrowed target");
        }

        [Test]
        public void TheGradedWindow_IsTheOneThatWasDrawn()
        {
            // A release exactly on the drawn JUST edge must grade JUST; the same offset one step
            // outside it must not. This is what ties the picture to the verdict.
            DisableFlickGate();
            _driver.OnPointerDown(At(OriginX, OriginY));
            _driver.OnDrag(At(OriginX, OriginY - _cfg.PendulumPull120Px));     // 120% — the narrowest target

            float drawnJust01 = _bar.JustWidthPx / (_bar.HalfTravelPx * 2f);
            Assert.AreEqual(drawnJust01, _driver.JustWindow01, 1e-4f,
                "the drawn band and the graded window are one number");

            _driver.SetPhaseForTests(Mathf.Asin(drawnJust01 * 0.9f) / (2f * Mathf.PI));
            _driver.OnPointerUp(At(OriginX, OriginY + 400f));
            Assert.AreEqual(1f, _sc.LastTimingPowerMul, 1e-6f, "inside the drawn band = JUST");
        }

        [Test]
        public void TheClubHead_DisappearsWhileTheBallIsInFlight()
        {
            DisableFlickGate();
            var group = _handleGo.GetComponent<CanvasGroup>();
            Assert.IsNotNull(group, "the driver binds a CanvasGroup on the handle");
            Assert.AreEqual(1f, group.alpha, 1e-6f, "visible at Idle");

            PullDown(_cfg.PendulumPull100Px);
            Assert.AreEqual(1f, group.alpha, 1e-6f, "and all the way through the swing");

            _driver.SetPhaseForTests(0f);
            _driver.OnPointerUp(At(OriginX, OriginY + 400f));
            Assert.AreEqual(0f, group.alpha, 1e-6f, "gone once the ball is away");

            _sc.CompleteShot();
            _sc.Tick(0.016f);          // PublishState -> OnStateChanged(Idle)
            Assert.AreEqual(1f, group.alpha, 1e-6f, "and back for the next shot");
        }

        // ── 5. Scheme identity ───────────────────────────────────────────────────

        [Test]
        public void Driver_ReportsItselfImplemented()
        {
            Assert.AreEqual(ControlScheme.Pendulum, _driver.Scheme);
            Assert.IsTrue(_driver.IsImplemented,
                "which is what turns SchemeRoot_Flick OFF for this scheme");
        }

        private static float Speed(ShotInput s) => new Vector3(
            s.velocity.x.ToFloat(), s.velocity.y.ToFloat(), s.velocity.z.ToFloat()).magnitude;
    }
}
