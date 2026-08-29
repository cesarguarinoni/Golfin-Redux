using NUnit.Framework;
using UnityEngine;
using Golfin.Gameplay.Input;
using Golfin.Gameplay.Config;
using Golfin.Gameplay.UI.ShotUI;
using Golfin.Physics;
using Golfin.Physics.Stats;
using Golfin.Physics.Math;

namespace Golfin.Gameplay.Tests
{
    /// <summary>
    /// shot_timing_power (F15, 2026-08-29) — the acceptance gate for "the coloured slab matters".
    ///
    /// Before this task the slab's position and colour at the moment of the flick were never
    /// read: flicking on green and flicking on red produced byte-identical shots, and the only
    /// timing consequence was the pass counter's aim degradation. SHOT_CONTROLS_DESIGN §3.4 says
    /// off-time flicks reduce POWER; these tests fail the moment that stops being true.
    ///
    /// Covers:
    ///   1. Sampleless drivers (bots, capture, tests) pay nothing — mul is exactly 1.
    ///   2. A latch at or above the green band is full power.
    ///   3. A latch at the cone base pays TimingPowerMulRed, and the speed really scales.
    ///   4. Mid-gold interpolates through the band edges.
    ///   5. ForcePerfectTiming buys out of the penalty (the flag finally does what it says).
    ///   6. Unlatching (thumb dips below the swing's low) discards the sample; the re-latch wins.
    ///   7. FireDebugShot is unaffected.
    ///   8. The drawn bands and the gameplay bands are the same numbers.
    /// </summary>
    [TestFixture]
    public class ShotTimingPowerTests
    {
        private GameObject     _go;
        private ShotController _sc;
        private ControlsConfig _cfg;

        private ShotInput _lastShotInput;
        private bool      _shotFired;

        // Screen-relative so the test does not depend on Game View resolution.
        private float ScreenH       => Mathf.Max(1f, Screen.height);
        private float AboveReversal => ScreenH * 0.05f;   // >> _reversalThreshold (0.01)

        /// <summary>Q16.16 fixed point round-trips through ShotInputBuilder, so a speed ratio
        /// recovered from the velocity carries a little noise. 1% per the spec.</summary>
        private const float SpeedTolerance01 = 0.01f;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("ShotTimingPowerTests_SC");
            _sc = _go.AddComponent<ShotController>();

            // arrowHz = Base + CC*Slope = 1 + 0 = 1 (CharacterStats.Neutral has ClubControl 0),
            // so Tick(x) puts _arrowProgress at exactly x. Band edges + multipliers stay at
            // their shipped defaults — this fixture tests the wiring, not the tuning.
            _cfg = ControlsConfig.Default;
            _cfg.BaseArrowSpeedHzAtCC0 = 1f;
            _cfg.ArrowSpeedHzPerCC     = 0f;
            _cfg.MinArrowSpeedHz       = 0.1f;
            _sc.InjectConfig(_cfg);

            var flags = ShotDebugFlags.Defaults;
            flags.CancelOnSlowFlick = false;
            flags.ForcePerfectAim   = true;   // isolate power: no degradation yaw in the velocity
            _sc.DebugFlags = flags;

            _sc.InjectStatBundle(new StatBundle(ClubStats.DefaultDriver, BallStats.Neutral,
                CharacterStats.Neutral, fp.FromInt(100), fp.FromInt(100)));

            _shotFired = false;
            _sc.OnShotResolved += (shotInput, _) => { _lastShotInput = shotInput; _shotFired = true; };
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_go);

        // ── Helpers ──────────────────────────────────────────────────────────────

        private static float Speed(ShotInput s) => new Vector3(
            s.velocity.x.ToFloat(), s.velocity.y.ToFloat(), s.velocity.z.ToFloat()).magnitude;

        /// <summary>Open a swing at full pull and advance the arrow to <paramref name="timing01"/>.</summary>
        private void BeginSwingAt(float timing01)
        {
            _sc.CompleteShot();                 // back to Idle (also clears any prior sample)
            _sc.BeginExternalDrag();
            _sc.SetExternalPower(1f, 0f);       // power > 0 → Timing, so the arrow ticks
            if (timing01 > 0f) _sc.Tick(timing01);
        }

        /// <summary>Down-then-up touch swing: the up move past the reversal threshold latches
        /// the aim, which is the instant the timing is sampled (D1). Returns the low point.</summary>
        private float PushLatchingSwing(float lowY = 300f)
        {
            _sc.PushTouchSample(new Vector2(100f, lowY + 600f));   // finger lands high
            _sc.PushTouchSample(new Vector2(100f, lowY));          // pulls down — bottom of swing
            _sc.PushTouchSample(new Vector2(100f, lowY + AboveReversal));   // upswing → latches
            Assert.IsTrue(_sc.IsAimLocked, "Sanity: the upswing must have latched the aim");
            return lowY;
        }

        /// <summary>Release into a shot. bypassFlickGate so the synthetic sample timestamps
        /// (all within one editor frame) never decide the outcome — timing is the variable
        /// under test, flick speed is not.</summary>
        private ShotInput Release()
        {
            _sc.EndExternalDrag(bypassFlickGate: true);
            Assert.IsTrue(_shotFired, "Shot must fire within EndExternalDrag");
            _shotFired = false;
            return _lastShotInput;
        }

        /// <summary>A full-pull shot with no touch samples at all — the bot/capture path.</summary>
        private ShotInput BaselineShot()
        {
            _sc.CompleteShot();
            _sc.BeginExternalDrag();
            _sc.SetExternalPower(1f, 0f);
            return Release();
        }

        // ── 1. Sampleless drivers pay nothing (D4) ───────────────────────────────

        [Test]
        public void NoTouchSamples_MultiplierIsOne()
        {
            var baseline = BaselineShot();

            Assert.IsTrue(float.IsNaN(_sc.LastTimingAtLatch),
                "A driver that pushes no touch samples must never produce a timing sample");
            Assert.AreEqual(1f, _sc.LastTimingPowerMul, 1e-5f,
                "Bots, capture drivers and tests must be byte-identical to pre-F15");

            // Same swing again, with the arrow parked deep in the red band: still no penalty.
            _sc.CompleteShot();
            _sc.BeginExternalDrag();
            _sc.SetExternalPower(1f, 0f);
            _sc.Tick(0.05f);
            var inRedBand = Release();

            Assert.AreEqual(1f, _sc.LastTimingPowerMul, 1e-5f);
            Assert.AreEqual(Speed(baseline), Speed(inRedBand), Speed(baseline) * SpeedTolerance01,
                "Arrow position must not touch a sampleless shot's speed");
        }

        // ── 2. Green band = full power ───────────────────────────────────────────

        [Test]
        public void LatchOnGreen_FullPower()
        {
            var baseline = BaselineShot();

            BeginSwingAt(0.9f);               // above TimingBandGreenY01 (0.85)
            PushLatchingSwing();
            var onGreen = Release();

            Assert.AreEqual(0.9f, _sc.LastTimingAtLatch, 1e-3f,
                "The sample is the arrow progress at the latch frame");
            Assert.AreEqual(1f, _sc.LastTimingPowerMul, 1e-5f,
                "At or above the green band the flick costs nothing");
            Assert.AreEqual(Speed(baseline), Speed(onGreen), Speed(baseline) * SpeedTolerance01,
                "A green flick is exactly a full-power shot");
        }

        // ── 3. Red base pays TimingPowerMulRed, and the ball really goes shorter ──

        [Test]
        public void LatchOnRedBase_RedMultiplier()
        {
            var baseline = BaselineShot();

            BeginSwingAt(0f);                 // arrow still at the cone base
            PushLatchingSwing();
            var onRed = Release();

            Assert.AreEqual(0f, _sc.LastTimingAtLatch, 1e-3f);
            Assert.AreEqual(_cfg.TimingPowerMulRed, _sc.LastTimingPowerMul, 1e-5f,
                "timing01 = 0 is exactly TimingPowerMulRed");
            Assert.AreEqual(Speed(baseline) * _cfg.TimingPowerMulRed, Speed(onRed),
                Speed(baseline) * SpeedTolerance01,
                "The multiplier must reach the resolved velocity, not just the log line");
        }

        // ── 4. Between the gold and green lines it interpolates ───────────────────

        [Test]
        public void LatchMidGold_Interpolates()
        {
            // Halfway from the gold line (0.45) to the green line (0.85).
            float mid = 0.5f * (_cfg.TimingBandGoldY01 + _cfg.TimingBandGreenY01);   // 0.65
            float expected = Mathf.Lerp(_cfg.TimingPowerMulGold, 1f, 0.5f);          // 0.95

            BeginSwingAt(mid);
            PushLatchingSwing();
            Release();

            Assert.AreEqual(mid, _sc.LastTimingAtLatch, 1e-3f);
            Assert.AreEqual(expected, _sc.LastTimingPowerMul, 1e-4f,
                $"Halfway gold→green must be lerp({_cfg.TimingPowerMulGold}, 1, 0.5)");
        }

        // ── 5. ForcePerfectTiming opts out (D4) ──────────────────────────────────

        [Test]
        public void ForcePerfectTiming_OverridesRed()
        {
            var flags = _sc.DebugFlags;
            flags.ForcePerfectTiming = true;
            _sc.DebugFlags = flags;

            var baseline = BaselineShot();

            BeginSwingAt(0f);                 // deepest red
            PushLatchingSwing();
            var forced = Release();

            Assert.AreEqual(1f, _sc.LastTimingPowerMul, 1e-5f,
                "ForcePerfectTiming must waive the penalty entirely");
            Assert.AreEqual(Speed(baseline), Speed(forced), Speed(baseline) * SpeedTolerance01);
        }

        // ── 6. Unlatching discards the sample; the re-latch is what counts (D1/D3) ─

        [Test]
        public void Unlatch_ClearsSample()
        {
            BeginSwingAt(0.1f);               // red band

            float low = PushLatchingSwing();
            Assert.AreEqual(0.1f, _sc.LastTimingAtLatch, 1e-3f, "First latch samples the red band");

            // The thumb comes back DOWN below the swing's low — that "reversal" was a wobble.
            _sc.PushTouchSample(new Vector2(100f, low - 10f));
            Assert.IsFalse(_sc.IsAimLocked, "Sanity: dipping below the low re-opens the aim");
            Assert.IsTrue(float.IsNaN(_sc.LastTimingAtLatch),
                "An unlatch must throw the stale timing sample away, not keep charging for it");

            _sc.Tick(0.8f);                   // arrow now at 0.9 — green band
            _sc.PushTouchSample(new Vector2(100f, low - 10f + AboveReversal));   // real upswing
            Assert.IsTrue(_sc.IsAimLocked);
            Release();

            Assert.AreEqual(0.9f, _sc.LastTimingAtLatch, 1e-3f, "The re-latch re-samples");
            Assert.AreEqual(1f, _sc.LastTimingPowerMul, 1e-5f,
                "The shot is judged on the flick the player actually made");
        }

        // ── 7. FireDebugShot is untouched ────────────────────────────────────────

        [Test]
        public void FireDebugShot_Unaffected()
        {
            var baseline = BaselineShot();

            // Even coming straight off a red-band latched swing, the debug shot resets the swing.
            BeginSwingAt(0f);
            PushLatchingSwing();
            _sc.CompleteShot();

            _sc.FireDebugShot(1f, DebugShotAccuracy.Green);
            Assert.IsTrue(_shotFired, "FireDebugShot must resolve a shot");
            _shotFired = false;

            Assert.AreEqual(1f, _sc.LastTimingPowerMul, 1e-5f);
            Assert.AreEqual(Speed(baseline), Speed(_lastShotInput), Speed(baseline) * SpeedTolerance01,
                "Debug/bot shots must stay byte-identical to pre-F15");
        }

        // ── 8. Drawn bands and gameplay bands are one number (D3) ────────────────

        [Test]
        public void ConeBandPalette_MatchesConfig()
        {
            Assert.AreEqual(ControlsConfig.Default.TimingBandGoldY01, ConeBandPalette.BandGoldY01, 1e-6f,
                "The gold line the player sees must be the gold edge the multiplier uses");
            Assert.AreEqual(ControlsConfig.Default.TimingBandGreenY01, ConeBandPalette.BandGreenY01, 1e-6f,
                "The green line the player sees must be the green edge the multiplier uses");
            Assert.AreEqual(0f, ConeBandPalette.BandRedY01, 1e-6f,
                "The red edge is the cone base by construction");
        }
    }
}
