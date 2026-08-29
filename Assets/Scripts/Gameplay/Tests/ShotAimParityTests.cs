using NUnit.Framework;
using UnityEngine;
using Golfin.Gameplay.Input;
using Golfin.Gameplay.Config;
using Golfin.Physics;
using Golfin.Physics.Stats;
using Golfin.Physics.Math;

namespace Golfin.Gameplay.Tests
{
    /// <summary>
    /// shot_aim_parity (2026-08-28) — the acceptance gate for "the ball goes where the line points".
    ///
    /// Before this task PublishState (the targeting line) used finetune * halfCone while
    /// CommitFlick (the shot) used finetune * AimNudgeRangeRad — a ~3.7x disagreement at the
    /// median club, which read in-game as "the flick always fires centered". Both now route
    /// through ShotController.AimYawFor, and these tests fail the moment they diverge again.
    ///
    /// Covers:
    ///   1. Straight mode: published aim == committed aim == heading + finetune * halfCone.
    ///   2. FadeDraw mode: both are the locked heading (the handle buys curve, not yaw).
    ///   3. Putt mode: same parity, against the putter's half-cone.
    ///   4. The aim latch re-opens when the finger goes below the swing's lowest point (D3).
    /// </summary>
    [TestFixture]
    public class ShotAimParityTests
    {
        private GameObject     _go;
        private ShotController _sc;

        private ShotInput      _lastShotInput;
        private bool           _shotFired;
        private ShotInputState _lastState;
        private bool           _stateSeen;

        // Screen-relative so the test does not depend on Game View resolution.
        private float ScreenH       => Mathf.Max(1f, Screen.height);
        private float AboveReversal => ScreenH * 0.05f;   // >> _reversalThreshold (0.01)

        /// <summary>Q16.16 fixed point + the fpMath sin/cos approximation round-trip through
        /// ShotInputBuilder, so a yaw recovered from the velocity carries a little noise.</summary>
        private const float YawTolerance = 1e-3f;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("ShotAimParityTests_SC");
            _sc = _go.AddComponent<ShotController>();
            _sc.InjectConfig(ControlsConfig.Default);

            // CancelOnSlowFlick=false so EndExternalDrag always commits; ForcePerfectAim=true
            // so degradYaw is 0 and the committed yaw is the aim formula alone.
            var flags = ShotDebugFlags.Defaults;
            flags.CancelOnSlowFlick = false;
            flags.ForcePerfectAim   = true;
            _sc.DebugFlags = flags;

            _sc.InjectStatBundle(new StatBundle(ClubStats.DefaultDriver, BallStats.Neutral,
                CharacterStats.Neutral, fp.FromInt(100), fp.FromInt(100)));

            _shotFired = false;
            _stateSeen = false;
            _sc.OnShotResolved += (shotInput, _) => { _lastShotInput = shotInput; _shotFired = true; };
            _sc.OnStateChanged += state          => { _lastState     = state;     _stateSeen = true; };
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_go);

        // ── Helpers ──────────────────────────────────────────────────────────────

        /// <summary>Yaw actually flown, recovered from the committed velocity.</summary>
        private static float CommittedYaw(ShotInput s) =>
            Mathf.Atan2(s.velocity.z.ToFloat(), s.velocity.x.ToFloat());

        /// <summary>Drive one shot through the external-drag path and return the ShotInput.
        /// The LAST published state is the one from SetExternalPower — i.e. the targeting line
        /// as the player saw it at the instant of the flick.</summary>
        private ShotInput Fire(float finetune)
        {
            _sc.BeginExternalDrag();
            _sc.SetExternalPower(0.8f, finetune);
            _sc.EndExternalDrag();

            Assert.IsTrue(_shotFired, $"Shot must fire at finetune={finetune}");
            Assert.IsTrue(_stateSeen, "PublishState must have run before the commit");
            _shotFired = false;
            return _lastShotInput;
        }

        // ── 1. Straight mode ─────────────────────────────────────────────────────

        [Test]
        public void Straight_PublishedAimEqualsCommittedAim()
        {
            const float heading = 0.35f;
            _sc.CameraHeadingRadians = heading;
            _sc.FadeDrawActive       = false;

            float halfCone = _sc.ConeHalfAngleDeg * Mathf.Deg2Rad;
            Assert.Greater(halfCone, 0.1f,
                "Sanity: the driver bundle must give a real cone (~11.25 deg), not a degenerate one");

            foreach (float f in new[] { -1f, -0.6f, 0f, 0.35f, 1f })
            {
                _sc.CompleteShot();                        // back to Idle for the next drag
                _sc.CameraHeadingRadians = heading;        // CompleteShot does not touch heading
                var shot = Fire(f);

                float published = _lastState.AimYawRadians;
                float committed = CommittedYaw(shot);
                float expected  = heading + f * halfCone;

                Assert.AreEqual(expected, published, YawTolerance,
                    $"finetune={f}: the targeting line must point at heading + f*halfCone");
                Assert.AreEqual(published, committed, YawTolerance,
                    $"finetune={f}: the ball must fly where the line pointed " +
                    $"(line={published:F4} rad, ball={committed:F4} rad)");
            }
        }

        // ── 2. FadeDraw mode ─────────────────────────────────────────────────────

        [Test]
        public void FadeDraw_PublishedAimIsLockedHeading()
        {
            const float lockedAim = 0.4f;

            _sc.CompleteShot();
            _sc.CameraHeadingRadians = 0f;
            _sc.FadeDrawActive       = true;
            _sc.FadeDrawLockedAimRad = lockedAim;

            var shot = Fire(0.9f);

            Assert.AreEqual(lockedAim, _lastState.AimYawRadians, YawTolerance,
                "FadeDraw: the line root must sit on the locked heading, not rotate with the handle");
            Assert.AreEqual(lockedAim, CommittedYaw(shot), YawTolerance,
                "FadeDraw: the shot launches on the locked heading — the handle buys bend, not yaw");
        }

        // ── 3. Putt mode ─────────────────────────────────────────────────────────

        [Test]
        public void Putt_PublishedAimEqualsCommittedAim()
        {
            _sc.CompleteShot();
            _sc.IsPutt         = true;
            _sc.FadeDrawActive = false;
            _sc.InjectStatBundle(new StatBundle(PutterStats.DefaultPutter, BallStats.Neutral,
                CharacterStats.Neutral, fp.FromInt(100), fp.FromInt(100)));

            const float heading  = 0.2f;
            const float finetune = 0.5f;
            _sc.CameraHeadingRadians = heading;

            float halfCone = _sc.ConeHalfAngleDeg * Mathf.Deg2Rad;
            var   shot     = Fire(finetune);

            float expected = heading + finetune * halfCone;
            Assert.AreEqual(expected, _lastState.AimYawRadians, YawTolerance,
                "Putt: the line uses the putter's half-cone");
            Assert.AreEqual(_lastState.AimYawRadians, CommittedYaw(shot), YawTolerance,
                "Putt: the ball must roll where the line pointed");
        }

        // ── 4. Latch re-opens on a new low (D3) ──────────────────────────────────

        [Test]
        public void Latch_ReopensWhenFingerGoesLower()
        {
            _sc.BeginExternalDrag();
            _sc.PushTouchSample(new Vector2(100f, 900f));   // finger lands
            _sc.PushTouchSample(new Vector2(100f, 300f));   // pull back down — bottom of swing
            _sc.PushTouchSample(new Vector2(100f, 300f + AboveReversal));   // wobble up → latches
            Assert.IsTrue(_sc.IsAimLocked, "A move up past the threshold latches the aim");

            _sc.SetExternalPower(0.8f, 0.7f);              // lateral aim while latched — ignored
            Assert.AreEqual(0f, _sc.ConeFinetune, 0.001f,
                "While latched the aim stays at the bottom-of-swing value");
            Assert.AreEqual(0f, _lastState.ConeFinetuneX, 0.001f);

            _sc.PushTouchSample(new Vector2(100f, 290f));  // the thumb came back DOWN — a wobble
            Assert.IsFalse(_sc.IsAimLocked,
                "A new lowest point means the reversal was a wobble: the aim must re-open");
            Assert.AreEqual(0.7f, _sc.ConeFinetune, 0.001f,
                "Re-opening re-syncs the aim to the live handle, so no aiming input is lost");

            _sc.SetExternalPower(0.8f, 0.7f);              // and lateral input steers again
            Assert.AreEqual(0.7f, _lastState.ConeFinetuneX, 0.001f,
                "After unlatching, the published line follows the handle again");
        }

        [Test]
        public void Latch_HoldsThroughUpswing_NoNewLow()
        {
            // The D3 unlatch must not weaken the latch itself: a real flick only goes up.
            _sc.BeginExternalDrag();
            _sc.PushTouchSample(new Vector2(100f, 900f));
            _sc.PushTouchSample(new Vector2(100f, 300f));
            _sc.SetExternalPower(0.8f, 0.30f);             // aim at the bottom of the swing

            _sc.PushTouchSample(new Vector2(100f, 300f + AboveReversal));        // latch
            _sc.PushTouchSample(new Vector2(100f, 300f + AboveReversal * 2f));   // keeps rising
            _sc.SetExternalPower(0.8f, -0.90f);            // finger drifts hard left mid-flick

            Assert.IsTrue(_sc.IsAimLocked, "An upswing that never dips below the low stays latched");
            Assert.AreEqual(0.30f, _sc.ConeFinetune, 0.001f,
                "Aim stays pinned at the bottom-of-swing value through the whole upswing");
        }
    }
}
