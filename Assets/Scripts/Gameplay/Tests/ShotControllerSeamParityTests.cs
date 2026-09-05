using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Golfin.Gameplay.Input;
using Golfin.Gameplay.Config;
using Golfin.Physics;
using Golfin.Physics.Stats;
using Golfin.Physics.Math;

namespace Golfin.Gameplay.Tests
{
    /// <summary>
    /// control_scheme_seam §6.1–§6.3 — the seam is only worth having if it changed NOTHING.
    ///
    /// <para>The whole spec is a refactor whose success condition is "Flick is byte-identical",
    /// so the load-bearing test is not that <c>CommitExternal</c> works, it is that the shot it
    /// produces is FIXED-POINT EQUAL to the one the flick path produces from the same numbers.
    /// A tolerance would hide exactly the class of bug this refactor could introduce (a term
    /// applied in the wrong order, a clamp on the wrong side of a multiply), so every
    /// comparison here is on raw <c>fp</c> values with <c>AreEqual</c> and no delta.</para>
    ///
    /// <para>Also covers the two new guards: <c>CommitExternal</c> refuses to fire outside a
    /// live external drag, and <c>ownsTiming</c> really does take the arrow away.</para>
    /// </summary>
    [TestFixture]
    public class ShotControllerSeamParityTests
    {
        private GameObject     _go;
        private ShotController _sc;
        private ControlsConfig _cfg;

        private ShotInput _lastShotInput;
        private int       _shotCount;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("ShotControllerSeamParityTests_SC");
            _sc = _go.AddComponent<ShotController>();

            // arrowHz = Base + CC*Slope = 1 + 0, so Tick(x) puts _arrowProgress at exactly x —
            // the same fixture trick ShotTimingPowerTests uses.
            _cfg = ControlsConfig.Default;
            _cfg.BaseArrowSpeedHzAtCC0 = 1f;
            _cfg.ArrowSpeedHzPerCC     = 0f;
            _cfg.MinArrowSpeedHz       = 0.1f;
            _sc.InjectConfig(_cfg);

            var flags = ShotDebugFlags.Defaults;
            flags.CancelOnSlowFlick = false;
            _sc.DebugFlags = flags;

            _sc.InjectStatBundle(new StatBundle(ClubStats.DefaultDriver, BallStats.Neutral,
                CharacterStats.Neutral, fp.FromInt(100), fp.FromInt(100)));

            _shotCount = 0;
            _sc.OnShotResolved += (shotInput, _) => { _lastShotInput = shotInput; _shotCount++; };
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_go);

        // ── Helpers ──────────────────────────────────────────────────────────────

        /// <summary>ShotInputBuilder is handed <c>UnityEngine.Random.Range(...)</c> as its seed,
        /// so two runs of the same shot only match if the global RNG is in the same place. Pin
        /// it before every commit; this is a comparison harness detail, not a production one.</summary>
        private static void PinRandom() => Random.InitState(20260904);

        private static void AssertSameShot(ShotInput a, ShotInput b, string what)
        {
            // fp is Q16.16 over a long; comparing .raw is exact equality with no tolerance to
            // hide a term that moved.
            Assert.AreEqual(a.velocity.x.raw, b.velocity.x.raw, $"{what}: velocity.x");
            Assert.AreEqual(a.velocity.y.raw, b.velocity.y.raw, $"{what}: velocity.y");
            Assert.AreEqual(a.velocity.z.raw, b.velocity.z.raw, $"{what}: velocity.z");
            Assert.AreEqual(a.Spin.Axis.x.raw, b.Spin.Axis.x.raw, $"{what}: spin axis.x");
            Assert.AreEqual(a.Spin.Axis.y.raw, b.Spin.Axis.y.raw, $"{what}: spin axis.y");
            Assert.AreEqual(a.Spin.Axis.z.raw, b.Spin.Axis.z.raw, $"{what}: spin axis.z");
            Assert.AreEqual(a.Spin.Rate.raw, b.Spin.Rate.raw, $"{what}: spin rate");
            Assert.AreEqual(a.origin.x.raw, b.origin.x.raw, $"{what}: origin.x");
            Assert.AreEqual(a.origin.y.raw, b.origin.y.raw, $"{what}: origin.y");
            Assert.AreEqual(a.origin.z.raw, b.origin.z.raw, $"{what}: origin.z");
        }

        /// <summary>Drive one flick through the production external-drag path.
        /// <paramref name="spin"/> is pushed AFTER the reset on purpose: TransitionToIdle clears
        /// PendingSpinInput, so a spin set before the reset silently becomes zero and the test
        /// would compare two spinless shots while claiming to test spin.</summary>
        private ShotInput FireFlick(float power, float finetune, Vector2 spin = default)
        {
            _sc.CompleteShot();
            _sc.PendingSpinInput = spin;
            PinRandom();
            _sc.BeginExternalDrag();
            _sc.SetExternalPower(power, finetune);
            _sc.EndExternalDrag(bypassFlickGate: true);
            Assert.AreEqual(1, _shotCount, "flick must fire exactly one shot");
            _shotCount = 0;
            return _lastShotInput;
        }

        /// <summary>The same shot expressed as a ShotIntent, through the scheme seam.</summary>
        private ShotInput FireIntent(ShotIntent intent, bool ownsTiming, Vector2 spin = default)
        {
            _sc.CompleteShot();
            _sc.PendingSpinInput = spin;
            PinRandom();
            _sc.BeginExternalDrag(ownsTiming);
            _sc.SetExternalPower(intent.PowerNormalized, intent.AimOffset01);
            _sc.CommitExternal(intent);
            Assert.AreEqual(1, _shotCount, "CommitExternal must fire exactly one shot");
            _shotCount = 0;
            return _lastShotInput;
        }

        // ── 1. Parity: the same numbers produce the same shot ────────────────────

        [Test]
        public void CommitExternal_MatchesCommitFlick_ForTheSameNumbers()
        {
            const float power    = 0.83f;
            const float finetune = 0.37f;

            var viaFlick = FireFlick(power, finetune);

            // No touch samples were pushed, so the flick's own timing multiplier is 1 and its
            // timing01 is NaN (D4). The intent states exactly that rather than assuming it.
            Assert.AreEqual(1f, _sc.LastTimingPowerMul, 1e-6f, "sampleless flick pays no timing");

            var viaSeam = FireIntent(new ShotIntent(power, finetune, 0f, 1f, float.NaN, 0f),
                                     ownsTiming: false);

            AssertSameShot(viaFlick, viaSeam, "flick vs seam");
        }

        [Test]
        public void CommitExternal_MatchesCommitFlick_WithSpin()
        {
            var spin = new Vector2(0.4f, -0.7f);

            var noSpin   = FireFlick(1f, -0.6f);
            var viaFlick = FireFlick(1f, -0.6f, spin);
            var viaSeam  = FireIntent(new ShotIntent(1f, -0.6f, 0f, 1f, float.NaN, 0f),
                                      ownsTiming: false, spin: spin);

            // Tripwire: a full-swing shot always carries SOME backspin, so "Rate != 0" would pass
            // even if PendingSpinInput were being dropped. Comparing against the same shot with
            // no HUD spin is what actually proves the input reached the builder.
            Assert.AreNotEqual(noSpin.Spin.Axis.x.raw, viaFlick.Spin.Axis.x.raw,
                "sanity: PendingSpinInput must change the shot, or this test proves nothing");
            AssertSameShot(viaFlick, viaSeam, "spin");
        }

        [Test]
        public void CommitExternal_MatchesTheDebugPath_UnderOverpower()
        {
            // NOT via FireFlick: the flick's own ClubHandleDragger derives power as
            // 1 - handleY/coneHeight and so never asks for more than 1.0, no matter what the
            // clamp allows (scheme_pendulum §3.1 widened it to MaxOverpowerNormalized).
            // FireDebugShot is therefore the flick-side reference for a 1.2 shot, and it goes
            // through the very same CommitFlick tail. It also starts with its own reset, so it
            // can never carry spin — this case is power only.
            _sc.CompleteShot();
            PinRandom();
            _sc.FireDebugShot(1.2f, DebugShotAccuracy.Green);
            Assert.AreEqual(1, _shotCount);
            _shotCount = 0;
            var viaDebug = _lastShotInput;

            var viaSeam = FireIntent(new ShotIntent(1.2f, 0f, 0f, 1f, float.NaN, 0f),
                                     ownsTiming: false);

            AssertSameShot(viaDebug, viaSeam, "overpower");
        }

        [Test]
        public void CommitExternal_MatchesCommitFlick_ForAPutt()
        {
            _sc.IsPutt = true;
            var spin = new Vector2(0.9f, 0.9f);               // must be discarded on a putt

            var viaFlick = FireFlick(1.2f, 0.5f, spin);       // 1.2 must clamp to 1.0
            var viaSeam  = FireIntent(new ShotIntent(1.2f, 0.5f, 0f, 1f, float.NaN, 0f),
                                      ownsTiming: false, spin: spin);

            // Both paths must discard PendingSpinInput on a putt. Asserting Spin.Rate == 0 would
            // be wrong: ShotInputBuilder gives every shot a base spin from the club and velocity,
            // so the putt rule is "the HUD's spin input is ignored", not "there is no spin".
            var puttNoSpin = FireFlick(1.2f, 0.5f);
            AssertSameShot(viaFlick, puttNoSpin, "putt ignores PendingSpinInput");
            AssertSameShot(viaFlick, viaSeam, "putt clamp + spin lock");
        }

        [Test]
        public void CommitExternal_ErrorYaw_LandsWhereDegradationYawDoes()
        {
            // The flick's aim degradation and a scheme's ErrorYawRad are the same term: an
            // extra yaw added to AimYawFor(). FireDebugShot with a Yellow preset applies exactly
            // one DegradationYawDegPerPass, so an intent carrying that same angle must match it.
            float degradYawRad = _cfg.DegradationYawDegPerPass * Mathf.Deg2Rad;

            _sc.CompleteShot();
            PinRandom();
            _sc.FireDebugShot(0.75f, DebugShotAccuracy.Yellow);
            Assert.AreEqual(1, _shotCount);
            _shotCount = 0;
            var viaDebug = _lastShotInput;

            var viaSeam = FireIntent(new ShotIntent(0.75f, 0f, degradYawRad, 1f, float.NaN, 0f),
                                     ownsTiming: false);

            AssertSameShot(viaDebug, viaSeam, "degradation yaw vs ErrorYawRad");
            Assert.IsFalse(_sc.LastShotWasClean, "a non-zero error yaw is not a clean shot");
        }

        [Test]
        public void CommitExternal_TimingMul_ScalesPowerTheSameWayTheFlickBandDoes()
        {
            var full = FireIntent(new ShotIntent(1f, 0f, 0f, 1f, 0.9f, 0f), ownsTiming: false);
            var half = FireIntent(new ShotIntent(1f, 0f, 0f, 0.5f, 0.1f, 0f), ownsTiming: false);

            float fullSpeed = Speed(full);
            float halfSpeed = Speed(half);

            Assert.Less(halfSpeed, fullSpeed, "a 0.5 multiplier must cost real speed");
            Assert.AreEqual(0.5f, halfSpeed / fullSpeed, 0.01f,
                "TimingMul must scale the committed magnitude, exactly as TimingPowerMultiplier does");
            Assert.AreEqual(0.5f, _sc.LastTimingPowerMul, 1e-6f, "latched for telemetry");
            Assert.AreEqual(0.1f, _sc.LastCommittedTiming01, 1e-6f, "timing01 latched for telemetry");
        }

        private static float Speed(ShotInput s) => new Vector3(
            s.velocity.x.ToFloat(), s.velocity.y.ToFloat(), s.velocity.z.ToFloat()).magnitude;

        // ── 2. CommitExternal refuses to fire outside a live external drag ───────

        [Test]
        public void CommitExternal_WithoutExternalDrag_DoesNothing()
        {
            _sc.CompleteShot();
            Assert.AreEqual(ShotState.Idle, _sc.State);

            LogAssert.ignoreFailingMessages = true;   // the guard logs a warning by design
            _sc.CommitExternal(new ShotIntent(1f, 0f, 0f, 1f, float.NaN, 0f));
            LogAssert.ignoreFailingMessages = false;

            Assert.AreEqual(0, _shotCount, "no shot may resolve without an external drag");
            Assert.AreEqual(ShotState.Idle, _sc.State, "state must not move");
        }

        [Test]
        public void CommitExternal_WhileStillAiming_DoesNothing()
        {
            _sc.CompleteShot();
            _sc.BeginExternalDrag(true);
            Assert.AreEqual(ShotState.Aiming, _sc.State, "no power pushed yet");

            LogAssert.ignoreFailingMessages = true;
            _sc.CommitExternal(new ShotIntent(1f, 0f, 0f, 1f, float.NaN, 0f));
            LogAssert.ignoreFailingMessages = false;

            Assert.AreEqual(0, _shotCount, "Aiming is too early to commit");
            Assert.AreEqual(ShotState.Aiming, _sc.State);
        }

        // ── 2b. The overpower ceiling (scheme_pendulum §3.1) ────────────────────

        [Test]
        public void SetExternalPower_PublishesOverpowerInsteadOfClampingItToOne()
        {
            _sc.CompleteShot();
            _sc.BeginExternalDrag(true);
            _sc.SetExternalPower(1.2f, 0f);

            Assert.AreEqual(1.2f, _sc.PowerNormalized, 1e-6f,
                "a scheme that reads its own pull lane must be able to publish 120%");
        }

        [Test]
        public void SetExternalPower_StillCeilingsAtTheOverpowerMaximum()
        {
            _sc.CompleteShot();
            _sc.BeginExternalDrag(true);
            _sc.SetExternalPower(5f, 0f);

            Assert.AreEqual(ShotController.MaxOverpowerNormalized, _sc.PowerNormalized, 1e-6f);

            _sc.SetExternalPower(-3f, 0f);
            Assert.AreEqual(0f, _sc.PowerNormalized, 1e-6f, "and floors at 0");
        }

        [Test]
        public void SetExternalPower_LeavesTheFlickPathUntouched()
        {
            // The widened ceiling can only be REACHED by a driver that asks for >1. Flick's own
            // ClubHandleDragger derives power as 1 - handleY/coneHeight, which is 0..1 by
            // construction — this is the whole reason widening the clamp is parity-safe.
            _sc.CompleteShot();
            _sc.BeginExternalDrag();
            _sc.SetExternalPower(1f, 0f);
            Assert.AreEqual(1f, _sc.PowerNormalized, 1e-6f);
        }

        [Test]
        public void PuttStillClampsAtCommit_NotAtPublish()
        {
            // The gauge may read 120% on a putt (the driver is free to publish it) but the SHOT
            // must be a 100% putt — the clamp lives in CommitExternal/CommitFlick, where it has
            // always lived, and this is the test that says so.
            _sc.IsPutt = true;
            _sc.CompleteShot();
            PinRandom();
            _sc.BeginExternalDrag(true);
            _sc.SetExternalPower(1.2f, 0f);
            Assert.AreEqual(1.2f, _sc.PowerNormalized, 1e-6f, "published as pulled");
            _sc.CommitExternal(new ShotIntent(1.2f, 0f, 0f, 1f, float.NaN, 0f));
            var over = _lastShotInput;
            _shotCount = 0;

            var exactlyOne = FireIntent(new ShotIntent(1f, 0f, 0f, 1f, float.NaN, 0f),
                                        ownsTiming: true);

            AssertSameShot(over, exactlyOne, "a 120% putt is a 100% putt");
            _sc.IsPutt = false;
        }

        // ── 3. ownsTiming takes the arrow away ──────────────────────────────────

        [Test]
        public void OwnsTiming_SuppressesTheArrowAndItsAutoCancel()
        {
            _sc.CompleteShot();
            _sc.BeginExternalDrag(true);
            _sc.SetExternalPower(1f, 0f);
            Assert.AreEqual(ShotState.Timing, _sc.State);

            ShotInputState last = default;
            _sc.OnStateChanged += s => last = s;

            // Five seconds at 1 Hz would be five full passes — well past MaxTotalPasses, which
            // is the auto-cancel that would otherwise yank the swing out from under the driver.
            for (int i = 0; i < 50; i++) _sc.Tick(0.1f);

            Assert.AreEqual(ShotState.Timing, _sc.State,
                "a driver that owns timing must never be auto-cancelled by the pass counter");
            Assert.AreEqual(0, last.PassIndex, "no arrow passes may accumulate");
            Assert.AreEqual(0f, last.ArrowProgress01, 1e-6f, "the arrow must not advance at all");
            Assert.IsFalse(last.IsDegrading, "no per-pass aim degradation");
        }

        [Test]
        public void OwnsTimingFalse_LeavesTheArrowExactlyAsItWas()
        {
            _sc.CompleteShot();
            _sc.BeginExternalDrag(false);
            _sc.SetExternalPower(1f, 0f);

            ShotInputState last = default;
            _sc.OnStateChanged += s => last = s;

            _sc.Tick(0.4f);

            Assert.AreEqual(0.4f, last.ArrowProgress01, 1e-4f,
                "the default path must still tick the arrow — this is the flick, unchanged");
        }

        [Test]
        public void OwnsTiming_ResetsOnTheNextSwing()
        {
            _sc.CompleteShot();
            _sc.BeginExternalDrag(true);
            _sc.SetExternalPower(1f, 0f);
            _sc.CommitExternal(new ShotIntent(1f, 0f, 0f, 1f, float.NaN, 0f));
            _shotCount = 0;

            _sc.CompleteShot();                 // TransitionToIdle clears _ownsTiming
            _sc.BeginExternalDrag();            // flick again
            _sc.SetExternalPower(1f, 0f);

            ShotInputState last = default;
            _sc.OnStateChanged += s => last = s;
            _sc.Tick(0.4f);

            Assert.AreEqual(0.4f, last.ArrowProgress01, 1e-4f,
                "a previous ownsTiming swing must not leave the arrow disabled");
        }
    }
}
