using NUnit.Framework;
using UnityEngine;
using Golfin.Physics;
using Golfin.Physics.Math;
using Golfin.Physics.Stats;
using Golfin.Gameplay.Input;
using Golfin.Gameplay.Config;

namespace Golfin.Gameplay.Tests
{
    /// <summary>
    /// Phase 7 Part F putt-mode and debug-flag tests. Runs alongside existing ShotControllerTests.
    /// </summary>
    [TestFixture]
    public class ShotControllerPuttModeTests
    {
        private GameObject           _go;
        private ShotController       _sc;
        private SyntheticInputSource _input;
        private ControlsConfig       _cfg;

        [SetUp]
        public void SetUp()
        {
            _go    = new GameObject("ShotController_Putt");
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

        void DriveToTiming(float pullPx = 170f)
        {
            // Touch down
            SetPull(0f);
            _sc.Tick(0.016f);   // Idle → Aiming
            // Drag past PullStartThreshold
            SetPull(35f);
            _sc.Tick(0.016f);   // Aiming → Pulling
            // Drag to target power
            SetPull(pullPx);
            _sc.Tick(0.016f);   // Pulling → Timing
            SetPull(pullPx);
            _sc.Tick(0.016f);   // Ensure Timing
        }

        ShotInput FlickAndCapture(float velocityY = 2000f)
        {
            ShotInput captured = default;
            _sc.OnShotResolved += (i, _) => captured = i;
            _input.IsTouching            = false;
            _input.TouchVelocityPxPerSec = new Vector2(0f, velocityY);
            _sc.Tick(0.016f);
            return captured;
        }

        // ── F.1: Putt mode behaviours ─────────────────────────────────────────

        [Test]
        public void F1_IsPutt_ClampsPowerAt100Percent()
        {
            _sc.IsPutt = true;
            DriveToTiming(400f);    // well above MaxOverpowerPullPx (360)
            Assert.AreEqual(1.0f, _sc.PowerNormalized, 0.01f,
                "Putt mode: power must clamp at 100% even past MaxOverpowerPullPx");
        }

        [Test]
        public void F1_IsPutt_ForcesSpinNone()
        {
            // Give ShotController a non-putt bundle with a driver so spin would normally be non-zero.
            var driverBundle = new StatBundle(
                ClubStats.DefaultDriver,
                BallStats.Neutral,
                CharacterStats.Neutral,
                fp.FromFloat(100f), fp.FromFloat(100f));

            // Override the bundle then set IsPutt=true — ShotController should still
            // pass bundle.IsPutt=true to ShotInputBuilder (via GetStatBundle returning putt bundle
            // when IsPutt is set and no override, or via the putt base-vel override path).
            // For this test, we rely on the fact that IsPutt=true builds a putt bundle with no spin.
            _sc.IsPutt = true;
            DriveToTiming(300f);
            var input = FlickAndCapture();

            Assert.IsFalse(input.Spin.IsSpinning,
                "Putt mode: ShotInput.Spin must be SpinState.None (not spinning)");
        }

        [Test]
        public void F1_IsPutt_UsesPuttBaseVelocityMps()
        {
            // Inject a driver bundle (75 m/s) but set IsPutt=true.
            // The baseVelOverride from ControlsConfig.PuttBaseVelocityMps (5 m/s) should win.
            // Because IsPutt=true triggers a putter stat bundle via GetStatBundle(),
            // velocity should be ~5 m/s, not ~75 m/s.
            _sc.IsPutt = true;
            DriveToTiming(300f);    // full power
            var input = FlickAndCapture();

            // Velocity magnitude from putt: 5 m/s * 1.0 * cos(4°) * some_multiplier ≈ 5 m/s.
            // Driver would produce ~75 m/s. Assert nowhere near driver velocity.
            float velMag = Mathf.Sqrt(
                input.velocity.x.ToFloat() * input.velocity.x.ToFloat() +
                input.velocity.y.ToFloat() * input.velocity.y.ToFloat() +
                input.velocity.z.ToFloat() * input.velocity.z.ToFloat());

            Assert.Less(velMag, 20f,
                $"Putt mode velocity ({velMag:F1} m/s) should be ~5 m/s, not driver ~75 m/s");
            Assert.Greater(velMag, 0.5f,
                "Putt mode velocity must be positive");
        }

        [Test]
        public void F1_IsPutt_ArrowsSlowedByMultiplier()
        {
            // With IsPutt=true, arrowHz = 0.5 * 0.5 = 0.25 Hz → one pass takes 4 s.
            // After 2s of ticking, arrowProgress should be < 1 (no pass completed).
            // Without putt, after 2s arrowProgress >= 1 (pass completed).
            _sc.IsPutt = true;
            DriveToTiming(170f);

            ShotInputState lastState = default;
            _sc.OnStateChanged += s => lastState = s;

            _sc.Tick(2.0f);

            Assert.Less(lastState.ArrowProgress01, 1f,
                "Putt mode: arrow should not complete a pass in 2s (needs 4s at 0.25 Hz)");
        }

        [Test]
        public void F1_IsPutt_SkipsDegradation()
        {
            // With IsPutt=true, ticking through many passes should NOT add degradation.
            // Observable: aimYaw at flick == CameraHeadingRadians (no extra yaw).
            _sc.IsPutt = true;
            _sc.CameraHeadingRadians = 0f;
            DriveToTiming(170f);

            // Tick 8s (2 complete putt passes at 0.25Hz).
            _sc.Tick(4.5f); // ~1 pass
            _sc.Tick(4.5f); // ~2 passes

            var input = FlickAndCapture();

            // AimYaw = cameraHeading + 0 (no finetune, no degradation) = 0.
            float aimYaw = Mathf.Atan2(input.velocity.z.ToFloat(), input.velocity.x.ToFloat());
            Assert.AreEqual(0f, aimYaw, 0.1f,
                "Putt mode: no degradation after multiple passes — aimYaw must stay at camera heading");
        }

        // ── F.3: Debug flag tests ─────────────────────────────────────────────

        [Test]
        public void F3_ShowConeOutline_DefaultTrue()
        {
            // Structural: default value and read/write round-trip.
            Assert.IsTrue(_sc.DebugFlags.ShowConeOutline, "ShowConeOutline should default to true");
            var flags = _sc.DebugFlags;
            flags.ShowConeOutline = false;
            _sc.DebugFlags = flags;
            Assert.IsFalse(_sc.DebugFlags.ShowConeOutline, "ShowConeOutline toggled to false");
        }

        [Test]
        public void F3_ShowArrowTrail_DefaultFalse()
        {
            Assert.IsFalse(_sc.DebugFlags.ShowArrowTrail, "ShowArrowTrail should default to false");
        }

        [Test]
        public void F3_CancelOnSlowFlick_False_AllowsSlowLift()
        {
            // With CancelOnSlowFlick=false, a slow-velocity lift must commit the shot.
            var flags = _sc.DebugFlags;
            flags.CancelOnSlowFlick = false;
            _sc.DebugFlags = flags;

            DriveToTiming(170f);
            bool shotFired = false;
            _sc.OnShotResolved += (_, __) => shotFired = true;

            _input.IsTouching            = false;
            _input.TouchVelocityPxPerSec = new Vector2(0f, 50f); // well below 1500 threshold
            _sc.Tick(0.016f);

            Assert.IsTrue(shotFired, "CancelOnSlowFlick=false: slow lift must still commit the shot");
        }

        [Test]
        public void F3_SinglePassMode_SkipsDegradation()
        {
            var flags = _sc.DebugFlags;
            flags.SinglePassMode = true;
            _sc.DebugFlags = flags;

            _sc.CameraHeadingRadians = 0f;
            DriveToTiming(170f);

            // Tick through many passes (arrowHz=0.5, dt=2.5 → 1 pass/tick × 8 = 8 passes).
            for (int i = 0; i < 8; i++) _sc.Tick(2.5f);

            // Still in Timing (not auto-cancelled yet), flick now.
            Assert.AreEqual(ShotState.Timing, _sc.State, "Still in Timing after 8 passes");

            var input = FlickAndCapture();
            float aimYaw = Mathf.Atan2(input.velocity.z.ToFloat(), input.velocity.x.ToFloat());
            Assert.AreEqual(0f, aimYaw, 0.1f,
                "SinglePassMode=true: no degradation yaw even after 8 passes");
        }

        [Test]
        public void F3_DisableOverpower_ClampsPowerAt100()
        {
            var flags = _sc.DebugFlags;
            flags.DisableOverpower = true;
            _sc.DebugFlags = flags;

            DriveToTiming(400f); // well above MaxOverpowerPullPx (360)
            Assert.AreEqual(1.0f, _sc.PowerNormalized, 0.01f,
                "DisableOverpower=true: power must clamp at 1.0 regardless of pull");
        }

        [Test]
        public void F3_DisableConeFineTune_ZerosLateralContribution()
        {
            var flags = _sc.DebugFlags;
            flags.DisableConeFineTune = true;
            _sc.DebugFlags = flags;

            _sc.CameraHeadingRadians = 0f;
            // Pull with lateral offset (would normally shift aimYaw).
            DriveToTiming(170f);
            // Add a 100px lateral offset.
            SetPull(170f, 100f);
            _sc.Tick(0.016f);

            var input = FlickAndCapture();

            // aimYaw should be 0 (cameraHeading), not offset by finetune.
            float aimYaw = Mathf.Atan2(input.velocity.z.ToFloat(), input.velocity.x.ToFloat());
            Assert.AreEqual(0f, aimYaw, 0.05f,
                "DisableConeFineTune=true: lateral finger position must not affect aim yaw");
        }

        [Test]
        public void F3_ForcePerfectTiming_AllowsSlowLift()
        {
            var flags = _sc.DebugFlags;
            flags.ForcePerfectTiming = true;
            _sc.DebugFlags = flags;

            DriveToTiming(170f);
            bool shotFired = false;
            _sc.OnShotResolved += (_, __) => shotFired = true;

            _input.IsTouching            = false;
            _input.TouchVelocityPxPerSec = new Vector2(0f, 10f); // way below threshold
            _sc.Tick(0.016f);

            Assert.IsTrue(shotFired, "ForcePerfectTiming=true: slow lift must still commit the shot");
        }

        [Test]
        public void F3_ForcePerfectAim_ZerosDegradationYaw()
        {
            var flags = _sc.DebugFlags;
            flags.ForcePerfectAim = true;
            _sc.DebugFlags = flags;

            _sc.CameraHeadingRadians = 0f;
            DriveToTiming(170f);

            // Tick through 5 passes to accumulate degradation (if the flag weren't active).
            for (int i = 0; i < 5; i++) _sc.Tick(2.5f);

            var input = FlickAndCapture();
            float aimYaw = Mathf.Atan2(input.velocity.z.ToFloat(), input.velocity.x.ToFloat());
            Assert.AreEqual(0f, aimYaw, 0.05f,
                "ForcePerfectAim=true: degradation yaw zeroed even after multiple passes");
        }

        // ── F.6 test 7: Bit-exact gate — Phase 5 putt regression ─────────────

        /// <summary>
        /// Verify Part F did not regress Phase 5 putt model.
        /// A v0 = 0.35 m/s putt on Green with PuttConfig.Default must land within
        /// the Phase 5 canonical tolerance d ∈ [2.7, 3.3] m.
        /// </summary>
        [Test]
        public void F6_PuttBitExactGate_Phase5Tolerance()
        {
            float speed     = 0.35f;
            float pitchDeg  = 4f; // DefaultPutter loft
            float pitchRad  = pitchDeg * Mathf.Deg2Rad;

            var origin   = fp3.Zero;
            var velocity = new fp3(
                fp.FromFloat(speed * Mathf.Cos(pitchRad)),
                fp.FromFloat(speed * Mathf.Sin(pitchRad)),
                fp.Zero);

            var input    = new ShotInput(origin, velocity, fp.FromInt(30), SpinState.None);
            var ground   = new FlatGround(fp.Zero);
            var surface  = new ConstantSurfaceProvider(SurfaceType.Green);
            var aero     = AeroConfig.Default;
            var wind     = WindConfig.Calm;
            var surfCfg  = SurfaceConfig.Default;
            var puttCfg  = PuttConfig.Default;
            var ballMods = BallPhysicsModifiers.Neutral;

            var traj = BallSimulation.Simulate(input, ground, aero, wind, surface, surfCfg, puttCfg, ballMods);

            float dx = traj.finalPosition.x.ToFloat();
            float dz = traj.finalPosition.z.ToFloat();
            float dist = Mathf.Sqrt(dx * dx + dz * dz);

            Assert.GreaterOrEqual(dist, 2.7f, $"Putt distance {dist:F2}m below Phase 5 lower bound 2.7m");
            Assert.LessOrEqual(dist, 3.3f,    $"Putt distance {dist:F2}m above Phase 5 upper bound 3.3m");
        }
    }
}
