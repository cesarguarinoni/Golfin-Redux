using NUnit.Framework;
using UnityEngine;
using Golfin.Gameplay.Input;
using Golfin.Gameplay.Config;

namespace Golfin.Gameplay.Tests
{
    [TestFixture]
    public class ShotControllerTests
    {
        private GameObject           _go;
        private ShotController       _sc;
        private SyntheticInputSource _input;
        private ControlsConfig       _cfg;

        [SetUp]
        public void SetUp()
        {
            _go    = new GameObject("ShotController");
            _sc    = _go.AddComponent<ShotController>();
            _input = new SyntheticInputSource();
            _cfg   = ControlsConfig.Default;
            _sc.InjectInputSource(_input);
            _sc.InjectConfig(_cfg);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        void SetPull(float pullPx, float lateralPx = 0f)
        {
            _input.TouchOriginPx   = new Vector2(540f, 1000f);
            _input.TouchPositionPx = new Vector2(540f + lateralPx, 1000f - pullPx);
            _input.IsTouching      = true;
        }

        void DriveToAiming()
        {
            SetPull(0f);
            _sc.Tick(0.016f);   // Idle → Aiming
        }

        void DriveToPulling(float pullPx = 35f)
        {
            DriveToAiming();
            SetPull(pullPx);    // > PullStartThresholdPx (30)
            _sc.Tick(0.016f);   // Aiming → Pulling
        }

        void DriveToTiming(float pullPx = 170f)
        {
            DriveToPulling(pullPx);
            SetPull(pullPx);    // power > 0 (> MinUsefulPullPx 40)
            _sc.Tick(0.016f);   // Pulling → Timing
        }

        // ── Tests ─────────────────────────────────────────────────────────────

        [Test]
        public void Test01_TouchDown_TransitionsToAiming()
        {
            SetPull(0f);
            _sc.Tick(0.016f);
            Assert.AreEqual(ShotState.Aiming, _sc.State);
        }

        [Test]
        public void Test02_LiftInAiming_CancelsToIdle()
        {
            DriveToAiming();
            _input.IsTouching = false;
            _sc.Tick(0.016f);
            Assert.AreEqual(ShotState.Idle, _sc.State);
        }

        [Test]
        public void Test03_PullExceedsThreshold_TransitionsToPulling()
        {
            DriveToAiming();
            SetPull(35f);       // 35 > PullStartThresholdPx (30)
            _sc.Tick(0.016f);
            Assert.AreEqual(ShotState.Pulling, _sc.State);
        }

        [Test]
        public void Test04_PowerFormula_MidPull_ReturnsHalf()
        {
            // pull=170 → (170-40)/(300-40) = 130/260 = 0.5
            DriveToTiming(170f);
            Assert.AreEqual(0.5f, _sc.PowerNormalized, 0.01f);
        }

        [Test]
        public void Test04b_PowerFormula_MaxPull_ReturnsOne()
        {
            // pull=300 = Max100PercentPullPx → power exactly 1.0
            DriveToTiming(300f);
            Assert.AreEqual(1.0f, _sc.PowerNormalized, 0.01f);
        }

        [Test]
        public void Test04c_OverpowerFormula_ClampedAt120Percent()
        {
            // pull=360 = MaxOverpowerPullPx → power = 1.0 + 1.0*0.2 = 1.2
            DriveToTiming(360f);
            Assert.AreEqual(1.2f, _sc.PowerNormalized, 0.01f);

            // pull=500 → still clamped at 1.2
            SetPull(500f);
            _sc.Tick(0.016f);
            Assert.AreEqual(1.2f, _sc.PowerNormalized, 0.01f);
        }

        [Test]
        public void Test05_LiftInTiming_BelowFlickVelocity_CancelsToIdle()
        {
            DriveToTiming(170f);
            _input.IsTouching            = false;
            _input.TouchVelocityPxPerSec = new Vector2(0f, 100f);  // < 1500 threshold
            _sc.Tick(0.016f);
            Assert.AreEqual(ShotState.Idle, _sc.State);
        }

        [Test]
        public void Test06_ValidFlick_TransitionsToResolving_FiresEvent()
        {
            DriveToTiming(170f);
            bool shotFired = false;
            _sc.OnShotResolved += (_, __) => shotFired = true;

            _input.IsTouching            = false;
            _input.TouchVelocityPxPerSec = new Vector2(0f, 2000f);  // > 1500 threshold
            _sc.Tick(0.016f);

            Assert.AreEqual(ShotState.Resolving, _sc.State);
            Assert.IsTrue(shotFired, "OnShotResolved should fire on valid flick");
        }

        [Test]
        public void Test07_OnStateChanged_FiresEveryTick()
        {
            int callCount = 0;
            _sc.OnStateChanged += _ => callCount++;

            SetPull(0f);
            _sc.Tick(0.016f);
            _sc.Tick(0.016f);
            _sc.Tick(0.016f);

            Assert.AreEqual(3, callCount);
        }

        [Test]
        public void Test08_IsPutt_ClampsPowerAt100Percent()
        {
            _sc.IsPutt = true;
            DriveToTiming(400f);    // well above MaxOverpowerPullPx (360)
            Assert.AreEqual(1.0f, _sc.PowerNormalized, 0.01f, "Putt mode clamps at 100%");
        }

        [Test]
        public void Test09_ArrowDegradation_StartsAfterCleanPasses()
        {
            // CC=0 (neutral defaults) → arrowHz=0.5, cleanPasses=1.
            // dt=2.5 → arrowProgress += 1.25 → 1 pass per tick.
            // After pass 1: passIndex=1 >= cleanPasses=1 → IsDegrading=true.
            DriveToTiming(170f);
            ShotInputState lastState = default;
            _sc.OnStateChanged += s => lastState = s;

            _sc.Tick(2.5f);

            Assert.IsTrue(lastState.IsDegrading,
                "Should degrade after exhausting clean passes");
        }

        [Test]
        public void Test10_MaxTotalPasses_AutoCancelsToIdle()
        {
            // MaxTotalPasses=10, arrowHz=0.5, dt=2.5 → exactly 1 pass per tick.
            // After 10 ticks in Timing the controller auto-cancels.
            DriveToTiming(170f);

            for (int i = 0; i < 10; i++)
                _sc.Tick(2.5f);

            Assert.AreEqual(ShotState.Idle, _sc.State,
                "Shot should auto-cancel after MaxTotalPasses");
        }
    }
}
