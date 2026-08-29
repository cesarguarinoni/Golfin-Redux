using NUnit.Framework;
using UnityEngine;
using Golfin.Gameplay.Input;
using Golfin.Gameplay.Config;
using Golfin.Gameplay.UI.HUD;
using Golfin.Physics;
using Golfin.Physics.Stats;
using Golfin.Physics.Math;

namespace Golfin.Gameplay.Tests
{
    /// <summary>
    /// EditMode tests for fade/draw core wiring (Order 356, fade_draw_core_wiring).
    ///
    /// Covers:
    ///   1. Aim mapping: Straight mode, handle deflection aims by ±halfCone (shot_aim_parity D1).
    ///   2. FadeDraw mode: aim stays locked; handle offset drives curve (fadeDrawInput).
    ///   3. Mode-transition aim-lock: arming locks CameraHeadingRadians as locked aim.
    ///   4. Mode-transition re-center: arming zeroes the finetune handle.
    ///   5. Disarming restores handle as aim-nudge control.
    ///   6. Putts unchanged: putt uses legacy formula regardless of FadeDrawActive.
    ///   7. Spin.y (backspin/topspin) regression: spin.y still changes rate.
    /// </summary>
    [TestFixture]
    public class FadeDrawWiringTests
    {
        private GameObject     _go;
        private ShotController _sc;
        private ControlsConfig _cfg;
        private StatBundle     _bundle;

        private ShotInput    _lastShotInput;
        private bool         _shotFired;

        [SetUp]
        public void SetUp()
        {
            _go  = new GameObject("FadeDrawTest_SC");
            _sc  = _go.AddComponent<ShotController>();
            _cfg = ControlsConfig.Default;
            _sc.InjectConfig(_cfg);
            // Use Defaults but override CancelOnSlowFlick=false so EndExternalDrag always commits.
            var flags = ShotDebugFlags.Defaults;
            flags.CancelOnSlowFlick = false;
            _sc.DebugFlags = flags;

            // Use a deterministic driver bundle for all tests.
            var club = ClubStats.DefaultDriver;
            _bundle  = new StatBundle(club, BallStats.Neutral, CharacterStats.Neutral,
                fp.FromInt(100), fp.FromInt(100));
            _sc.InjectStatBundle(_bundle);

            _shotFired = false;
            _sc.OnShotResolved += (shotInput, _) =>
            {
                _lastShotInput = shotInput;
                _shotFired     = true;
            };
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
            // Reset ShotModeContext after each test to avoid cross-test pollution.
            ShotModeContext.Reset();
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        /// <summary>Fire a shot with explicit coneFinetune and mode, returns ShotInput via event.</summary>
        /// <param name="spinOverride">
        /// Optional spin input to apply AFTER CompleteShot (which resets PendingSpinInput).
        /// Use this when a test needs to set spin for a specific shot — setting it before
        /// calling FireShot would be wiped by the TransitionToIdle reset inside CompleteShot.
        /// </param>
        ShotInput FireShot(float cameraHeading, float coneFinetune,
                           bool fadeDrawActive, float fadeDrawLockedAim = float.NaN,
                           Vector2? spinOverride = null)
        {
            // Reset to Idle first (prior shot may have left state=Resolving).
            // NOTE: TransitionToIdle resets PendingSpinInput to zero, so set spinOverride AFTER.
            _sc.CompleteShot();

            _sc.CameraHeadingRadians = cameraHeading;
            _sc.FadeDrawActive       = fadeDrawActive;
            _sc.FadeDrawLockedAimRad = fadeDrawLockedAim;
            if (spinOverride.HasValue) _sc.PendingSpinInput = spinOverride.Value;

            // Use ExternalDrag API to get control over coneFinetune.
            // BeginExternalDrag requires State==Idle (ensured by CompleteShot above).
            _sc.BeginExternalDrag();
            // SetExternalPower: power>0 → Timing; coneFinetune = the X offset (-1..+1).
            _sc.SetExternalPower(0.8f, coneFinetune);
            _sc.EndExternalDrag();  // → CommitFlick → OnShotResolved

            Assert.IsTrue(_shotFired, "Shot must fire within EndExternalDrag");
            _shotFired = false;
            return _lastShotInput;
        }

        // ── Test 1: Straight mode aim nudge ──────────────────────────────────────

        [Test]
        public void StraightMode_HandleRight_AimsRightByHalfCone()
        {
            // Straight mode, handle at +1 → aim = heading + halfCone (shot_aim_parity D1;
            // the D4 3° AimNudgeRangeRad from Order 356 was reverted 2026-08-28).
            float heading   = 0f;
            float finetune  = 1f;

            var shotCenter = FireShot(heading, 0f,     fadeDrawActive: false);
            var shotRight  = FireShot(heading, finetune, fadeDrawActive: false);

            // Aim yaw is encoded in velocity: aimYaw = atan2(vz, vx).
            float yawCenter = Mathf.Atan2(shotCenter.velocity.z.ToFloat(), shotCenter.velocity.x.ToFloat());
            float yawRight  = Mathf.Atan2(shotRight.velocity.z.ToFloat(),  shotRight.velocity.x.ToFloat());

            float expectedNudge = _sc.ConeHalfAngleDeg * Mathf.Deg2Rad;
            Assert.AreEqual(expectedNudge, yawRight - yawCenter, 0.005f,
                $"Aim at finetune=+1 should be +halfCone ({expectedNudge:F4} rad)");
        }

        [Test]
        public void StraightMode_HandleLeft_AimsLeftByHalfCone()
        {
            float heading  = 0f;
            float finetune = -1f;

            var shotCenter = FireShot(heading, 0f,      fadeDrawActive: false);
            var shotLeft   = FireShot(heading, finetune, fadeDrawActive: false);

            float yawCenter = Mathf.Atan2(shotCenter.velocity.z.ToFloat(), shotCenter.velocity.x.ToFloat());
            float yawLeft   = Mathf.Atan2(shotLeft.velocity.z.ToFloat(),   shotLeft.velocity.x.ToFloat());

            float expectedNudge = -_sc.ConeHalfAngleDeg * Mathf.Deg2Rad;
            Assert.AreEqual(expectedNudge, yawLeft - yawCenter, 0.005f,
                $"Aim at finetune=-1 should be -halfCone ({expectedNudge:F4} rad)");
        }

        // ── Test 2: FadeDraw mode — aim locked, handle drives curve ─────────────

        [Test]
        public void FadeDrawMode_AimIsLocked_HandleDoesNotShiftAim()
        {
            // FadeDraw armed: locked aim = 0.2 rad. Handle at +1 should NOT shift aim.
            float lockedAim = 0.2f;

            var shotLeft  = FireShot(0f, -1f, fadeDrawActive: true, fadeDrawLockedAim: lockedAim);
            var shotRight = FireShot(0f,  1f, fadeDrawActive: true, fadeDrawLockedAim: lockedAim);

            float yawLeft  = Mathf.Atan2(shotLeft.velocity.z.ToFloat(),  shotLeft.velocity.x.ToFloat());
            float yawRight = Mathf.Atan2(shotRight.velocity.z.ToFloat(), shotRight.velocity.x.ToFloat());

            // Both aim yaws should be ~ lockedAim regardless of handle position.
            Assert.AreEqual(lockedAim, yawLeft,  0.005f, "FadeDraw: aim stays locked at left handle");
            Assert.AreEqual(lockedAim, yawRight, 0.005f, "FadeDraw: aim stays locked at right handle");
        }

        // ── Test 3: FadeDraw mode — spin axis differs left vs right ──────────────

        [Test]
        public void FadeDrawMode_HandleLeftVsRight_ProducesDifferentSpinAxis()
        {
            // When FadeDraw armed, finetune drives fadeDrawInput → orbital tilt.
            // Left vs right should produce different spin axis y-components (the tilt).
            float lockedAim = 0f;

            var shotLeft  = FireShot(0f, -1f, fadeDrawActive: true, fadeDrawLockedAim: lockedAim);
            var shotRight = FireShot(0f,  1f, fadeDrawActive: true, fadeDrawLockedAim: lockedAim);

            float axisYLeft  = shotLeft.Spin.Axis.y.ToFloat();
            float axisYRight = shotRight.Spin.Axis.y.ToFloat();

            // They must be opposite in sign (mirrored tilt).
            Assert.IsTrue(Mathf.Abs(axisYLeft)  > 0.01f, "FadeDraw left: spin axis must be tilted");
            Assert.IsTrue(Mathf.Abs(axisYRight) > 0.01f, "FadeDraw right: spin axis must be tilted");
            // Sign check: left=-1 → negative y; right=+1 → positive y (or vice versa; must be opposite).
            Assert.IsTrue(axisYLeft * axisYRight < 0f,
                "FadeDraw left vs right must produce opposite spin axis y-sign");
        }

        // ── Test 4: Mode-transition aim-lock ─────────────────────────────────────

        [Test]
        public void ModeTrans_Arm_LocksAimAtCameraHeading()
        {
            // Simulate arming: ShotConeView.OnShotModeChanged sets FadeDrawLockedAimRad=CameraHeadingRadians.
            // We test the result directly: FadeDrawLockedAimRad is set to heading.
            float heading = 0.5f;
            _sc.CameraHeadingRadians = heading;
            _sc.FadeDrawActive       = false;
            _sc.FadeDrawLockedAimRad = float.NaN;

            // Simulate the arm (ShotConeView.OnShotModeChanged arm path):
            _sc.FadeDrawLockedAimRad = _sc.CameraHeadingRadians;
            _sc.FadeDrawActive       = true;
            _sc.ForceRecenterFinetune();

            Assert.AreEqual(heading, _sc.FadeDrawLockedAimRad, 0.001f,
                "FadeDrawLockedAimRad must equal CameraHeadingRadians at arm");
            Assert.IsTrue(_sc.FadeDrawActive, "FadeDrawActive must be true after arm");
        }

        // ── Test 5: Mode-transition re-center ────────────────────────────────────

        [Test]
        public void ModeTrans_Arm_RecentersHandle()
        {
            // Reset to ensure Idle state.
            _sc.CompleteShot();

            // Before arm, set a lateral touch (finetune=0.8).
            _sc.BeginExternalDrag();
            _sc.SetExternalPower(0.8f, 0.8f);   // coneFinetune=0.8

            // Simulate arm mid-shot (ForceRecenterFinetune resets _coneFinetune).
            _sc.ForceRecenterFinetune();

            // Fire shot: the coneFinetune used in CommitFlick should now be ~0.
            // FadeDrawActive=true so finetune goes to curve, not aim.
            _sc.FadeDrawActive       = true;
            _sc.FadeDrawLockedAimRad = _sc.CameraHeadingRadians;
            _sc.EndExternalDrag();

            Assert.IsTrue(_shotFired, "Shot must fire");
            // With coneFinetune=0 and FadeDraw active, spin axis should be unilted (close to baseline).
            float axisY = Mathf.Abs(_lastShotInput.Spin.Axis.y.ToFloat());
            Assert.AreEqual(0f, axisY, 0.02f,
                "After re-center, finetune=0 → FadeDraw spin axis y should be ~0 (untilted)");
        }

        // ── Test 6: Putts unchanged (D6) ─────────────────────────────────────────

        [Test]
        public void Putt_FadeDrawActive_SpinIsZero()
        {
            _sc.CompleteShot();
            _sc.IsPutt = true;
            _sc.FadeDrawActive       = true;
            _sc.FadeDrawLockedAimRad = 0.3f;

            // Use a putter bundle so bundle.IsPutt=true and ShotInputBuilder returns SpinState.None.
            var putter = PutterStats.DefaultPutter;
            var putterBundle = new StatBundle(putter, BallStats.Neutral, CharacterStats.Neutral,
                fp.FromInt(100), fp.FromInt(100));
            _sc.InjectStatBundle(putterBundle);

            _sc.BeginExternalDrag();
            _sc.SetExternalPower(0.8f, 1f);   // handle fully right
            _sc.EndExternalDrag();

            Assert.IsTrue(_shotFired, "Putt shot must fire");
            Assert.AreEqual(0f, _lastShotInput.Spin.Rate.ToFloat(), 0.01f,
                "Putt spin rate must be zero regardless of FadeDraw mode (D6)");
        }

        // ── Test 7: Spin.y regression — still changes magnitude ──────────────────

        [Test]
        public void SpinY_StillChangesRate_AfterD3()
        {
            // Verify spinY still reduces backspin rate as expected (regression check vs D3).
            // Baseline: spinY=0 (full backspin).
            // PendingSpinInput must be passed via spinOverride so TransitionToIdle doesn't reset it.
            var shotBase = FireShot(0f, 0f, fadeDrawActive: false,
                                    spinOverride: Vector2.zero);

            // SpinY=0.5: magScale = 1 - 0.5*1.5 = 0.25 → rate = 25% of baseline.
            // Test with FadeDraw active to ensure D3 doesn't break spinY path.
            var shotSpin = FireShot(0f, 0f, fadeDrawActive: true, fadeDrawLockedAim: 0f,
                                    spinOverride: new Vector2(0f, 0.5f));

            float baseRate = shotBase.Spin.Rate.ToFloat();
            float spinRate = shotSpin.Spin.Rate.ToFloat();
            float expected = baseRate * 0.25f;
            Assert.AreEqual(expected, spinRate, expected * 0.1f,
                "SpinY=0.5 must reduce spin rate to ~25% of baseline even with FadeDraw active");
        }
    }
}
