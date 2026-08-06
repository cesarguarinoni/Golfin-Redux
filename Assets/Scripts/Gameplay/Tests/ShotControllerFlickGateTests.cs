using NUnit.Framework;
using UnityEngine;
using Golfin.Gameplay.Input;

namespace Golfin.Gameplay.Tests
{
    /// <summary>
    /// SHOT_FLICK_FIX_SPEC coverage for the parts that are deterministic without a real finger:
    /// the programmatic-driver bypass (bots must keep firing) and the upswing aim latch, which is
    /// position-based only. The velocity window itself depends on real per-frame timing
    /// (Time.unscaledTime) and is covered by the manual on-device acceptance tests.
    /// </summary>
    public class ShotControllerFlickGateTests
    {
        private GameObject    _go;
        private ShotController _sc;

        // Screen-relative so the test does not depend on Game View resolution.
        private float ScreenH        => Mathf.Max(1f, Screen.height);
        private float AboveReversal  => ScreenH * 0.05f;   // >> reversalThreshold (0.01)
        private float BelowReversal   => ScreenH * 0.002f; // << reversalThreshold — jitter

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("ShotControllerFlickGateTests");
            _sc = _go.AddComponent<ShotController>();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_go);

        // ── Bug 1: gate must not touch programmatic drivers ──────────────────

        [Test]
        public void NoTouchSamples_GatePasses_BotsStillFire()
        {
            // Bots / capture drivers / tests drive Begin→Set→End with zero touch samples.
            _sc.BeginExternalDrag();
            _sc.SetExternalPower(0.8f, 0f);

            Assert.IsTrue(_sc.EvaluateFlickGate(),
                "A driver that pushes no touch samples must never be gated.");
        }

        [Test]
        public void SingleTouchSample_GateFails_NoMeasurableTravel()
        {
            _sc.BeginExternalDrag();
            _sc.PushTouchSample(new Vector2(100f, 500f));

            Assert.IsFalse(_sc.EvaluateFlickGate(),
                "A tap with one sample has no measurable travel and must not fire.");
        }

        [Test]
        public void FlickGateActive_TrueWithSpecDefaults()
        {
            Assert.IsTrue(_sc.FlickGateActive,
                "Default minFlickSpeed=1.2 and debugDisableFlickGate=false → gate owns the release.");
        }

        // ── Bug 2: aim latches on upward reversal ────────────────────────────

        [Test]
        public void PullDownThenUp_LatchesAim()
        {
            _sc.BeginExternalDrag();
            _sc.PushTouchSample(new Vector2(100f, 900f));   // finger lands
            _sc.PushTouchSample(new Vector2(100f, 600f));   // pull back down
            Assert.IsFalse(_sc.IsAimLocked, "Pulling down must not latch.");

            _sc.PushTouchSample(new Vector2(100f, 600f + AboveReversal));  // upswing
            Assert.IsTrue(_sc.IsAimLocked, "Upward reversal past the threshold must latch the aim.");
        }

        [Test]
        public void MicroJitter_DoesNotLatch()
        {
            _sc.BeginExternalDrag();
            _sc.PushTouchSample(new Vector2(100f, 900f));
            _sc.PushTouchSample(new Vector2(100f, 600f));
            _sc.PushTouchSample(new Vector2(100f, 600f + BelowReversal));  // up a hair
            _sc.PushTouchSample(new Vector2(100f, 600f));                  // back down

            Assert.IsFalse(_sc.IsAimLocked,
                "Cumulative-since-lowest means sub-threshold jitter must never latch.");
        }

        [Test]
        public void OnceLatched_LateralMovementNoLongerSteersAim()
        {
            _sc.BeginExternalDrag();
            _sc.PushTouchSample(new Vector2(100f, 900f));
            _sc.PushTouchSample(new Vector2(100f, 600f));
            _sc.SetExternalPower(0.8f, 0.30f);            // aim at bottom of swing
            Assert.AreEqual(0.30f, _sc.ConeFinetune, 0.001f);

            _sc.PushTouchSample(new Vector2(100f, 600f + AboveReversal));   // latch here
            _sc.SetExternalPower(0.8f, -0.90f);           // finger drifts hard left mid-flick

            Assert.IsTrue(_sc.IsAimLocked);
            Assert.AreEqual(0.30f, _sc.ConeFinetune, 0.001f,
                "Aim must freeze at the bottom-of-swing value through the upswing.");
        }

        [Test]
        public void SwingReset_UnlatchesAim()
        {
            _sc.BeginExternalDrag();
            _sc.PushTouchSample(new Vector2(100f, 900f));
            _sc.PushTouchSample(new Vector2(100f, 600f));
            _sc.PushTouchSample(new Vector2(100f, 600f + AboveReversal));
            Assert.IsTrue(_sc.IsAimLocked);

            _sc.CancelExternalDrag();   // the existing reset path

            Assert.IsFalse(_sc.IsAimLocked, "Every path back to pull-back must unlatch the aim.");
            Assert.AreEqual(ShotState.Idle, _sc.State);
        }

        [Test]
        public void FailedFlickGate_ResetsInsteadOfFiring()
        {
            bool fired = false;
            _sc.OnShotResolved += (_, __) => fired = true;

            _sc.BeginExternalDrag();
            _sc.SetExternalPower(0.8f, 0f);
            _sc.PushTouchSample(new Vector2(100f, 500f));   // one sample → gate fails
            _sc.EndExternalDrag();

            Assert.IsFalse(fired, "A release that fails the flick gate must not fire a shot.");
            Assert.AreEqual(ShotState.Idle, _sc.State, "It must reset the swing instead.");
        }

        [Test]
        public void BypassFlickGate_FiresOnPlainRelease()
        {
            bool fired = false;
            _sc.OnShotResolved += (_, __) => fired = true;

            _sc.BeginExternalDrag();
            _sc.SetExternalPower(0.8f, 0f);
            _sc.PushTouchSample(new Vector2(100f, 500f));   // would fail the gate
            _sc.EndExternalDrag(bypassFlickGate: true);     // ClubHandleDragger._releaseToFire

            Assert.IsTrue(fired, "The debug bypass must still fire on any release.");
        }
    }
}
