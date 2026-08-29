using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Golfin.Gameplay.Config;
using Golfin.Gameplay.Input;
using Golfin.Gameplay.Session;
using Golfin.Physics;
using Golfin.Physics.Math;
using Golfin.Physics.Stats;

namespace Golfin.Gameplay.Tests
{
    /// <summary>
    /// shot_timing_telemetry (2026-08-29) — the flick timing the player actually hit has to
    /// REACH analytics, or the F15 tuning (band edges, 0.70 / 0.90 multipliers) and the putt
    /// question stay guesses.
    ///
    /// The two failure modes these tests exist to stop:
    ///   1. Reading the LIVE swing sample at shot-complete time. `LastTimingAtLatch` is wiped by
    ///      ResetSwingSamples() the instant the ball rests, so every event would report NaN.
    ///   2. Sending a fake 0 for a swing that never had a timing sample (bot, capture driver,
    ///      FireDebugShot). A 0 reads as "flicked at the cone base" and would poison the red
    ///      share the tuning decision is made on — it must be null.
    /// </summary>
    [TestFixture]
    public class ShotTimingTelemetryTests
    {
        // ── 1. The forwarding ctors mean "no timing sample, no penalty" (D2) ─────

        [Test]
        public void ShotRecord_ForwardingCtors_DefaultTiming()
        {
            var eight = new ShotRecord(1, "Driver", Vector3.zero, new Vector3(10f, 0f, 0f),
                                       10f, "AtRest", null, "Fairway");
            var nine  = new ShotRecord(2, "Driver", Vector3.zero, new Vector3(10f, 0f, 0f),
                                       10f, "OB", "CrossedBoundary", "OOB", 1);

            Assert.IsTrue(float.IsNaN(eight.Timing01),
                "The 8-arg ctor must mean 'this record carries no timing sample', not 0");
            Assert.AreEqual(1f, eight.TimingPowerMul, 1e-6f);
            Assert.IsTrue(float.IsNaN(nine.Timing01),
                "The 9-arg ctor must mean 'this record carries no timing sample', not 0");
            Assert.AreEqual(1f, nine.TimingPowerMul, 1e-6f);
            Assert.AreEqual(1, nine.PenaltyStrokes, "The pre-existing fields must be untouched");

            var eleven = new ShotRecord(3, "Putter", Vector3.zero, Vector3.one,
                                        1f, "InCup", null, "Green", 0, 0.62f, 0.93f);
            Assert.AreEqual(0.62f, eleven.Timing01, 1e-6f);
            Assert.AreEqual(0.93f, eleven.TimingPowerMul, 1e-6f);
        }

        // ── 2. The band names come from the edges the SHOT was judged with (D3) ──

        [Test]
        public void TimingBand_EdgesMatchConfig()
        {
            var cfg = ControlsConfig.Default;
            Assert.AreEqual(0.45f, cfg.TimingBandGoldY01, 1e-6f,  "Fixture assumes the shipped gold edge");
            Assert.AreEqual(0.85f, cfg.TimingBandGreenY01, 1e-6f, "Fixture assumes the shipped green edge");

            Assert.AreEqual("green", GameSession.TimingBand(0.85f), "The green edge is inclusive");
            Assert.AreEqual("green", GameSession.TimingBand(1f));
            Assert.AreEqual("gold",  GameSession.TimingBand(0.84f));
            Assert.AreEqual("gold",  GameSession.TimingBand(0.45f), "The gold edge is inclusive");
            Assert.AreEqual("red",   GameSession.TimingBand(0.44f));
            Assert.AreEqual("red",   GameSession.TimingBand(0f));
            Assert.IsNull(GameSession.TimingBand(float.NaN),
                "A sampleless swing has no band — null, never \"red\"");
        }

        // ── 3. The payload the dashboard reads (D3) ──────────────────────────────

        [Test]
        public void ShotTaken_Payload_CarriesTiming()
        {
            var green = new ShotRecord(1, "Driver", Vector3.zero, new Vector3(200f, 0f, 0f),
                                       200f, "AtRest", null, "Fairway", 0, 0.9f, 1f);
            var payload = new Dictionary<string, object>();
            GameSession.AppendShotTimingKeys(payload, green);

            Assert.AreEqual(0.9d, (double)payload["timing01"], 1e-9d);
            Assert.AreEqual(1d, (double)payload["timing_mul"], 1e-9d);
            Assert.AreEqual("green", payload["timing_band"]);

            // A red flick: the multiplier is what makes the band cost something.
            var red = new ShotRecord(2, "Driver", Vector3.zero, new Vector3(150f, 0f, 0f),
                                     150f, "AtRest", null, "Rough", 0, 0.1f, 0.7166f);
            var redPayload = new Dictionary<string, object>();
            GameSession.AppendShotTimingKeys(redPayload, red);

            Assert.AreEqual(0.1d, (double)redPayload["timing01"], 1e-9d);
            Assert.AreEqual(0.72d, (double)redPayload["timing_mul"], 1e-9d, "2 dp per D3");
            Assert.AreEqual("red", redPayload["timing_band"]);

            // The bot / capture / debug-shot case: null, NOT 0.
            var sampleless = new ShotRecord(3, "Driver", Vector3.zero, new Vector3(220f, 0f, 0f),
                                            220f, "AtRest", null, "Fairway", 0);
            var bot = new Dictionary<string, object>();
            GameSession.AppendShotTimingKeys(bot, sampleless);

            Assert.IsTrue(bot.ContainsKey("timing01"), "The key is always present, its value is null");
            Assert.IsNull(bot["timing01"], "A fake 0 would read as a botched flick in the aggregate");
            Assert.IsNull(bot["timing_band"]);
            Assert.AreEqual(1d, (double)bot["timing_mul"], 1e-9d, "A sampleless shot pays nothing");
        }

        // ── 4. The snapshot survives the shot it belongs to (D1) ─────────────────

        [Test]
        public void ShotController_LastCommittedTiming01_SurvivesCompleteShot()
        {
            var go = new GameObject("ShotTimingTelemetryTests_SC");
            try
            {
                var sc = go.AddComponent<ShotController>();

                // arrowHz = Base + CC*Slope = 1 → Tick(x) parks the arrow at exactly x.
                var cfg = ControlsConfig.Default;
                cfg.BaseArrowSpeedHzAtCC0 = 1f;
                cfg.ArrowSpeedHzPerCC     = 0f;
                cfg.MinArrowSpeedHz       = 0.1f;
                sc.InjectConfig(cfg);

                var flags = ShotDebugFlags.Defaults;
                flags.CancelOnSlowFlick = false;
                flags.ForcePerfectAim   = true;
                sc.DebugFlags = flags;

                sc.InjectStatBundle(new StatBundle(ClubStats.DefaultDriver, BallStats.Neutral,
                    CharacterStats.Neutral, fp.FromInt(100), fp.FromInt(100)));

                bool fired = false;
                sc.OnShotResolved += (_, __) => fired = true;

                sc.BeginExternalDrag();
                sc.SetExternalPower(1f, 0f);
                sc.Tick(0.3f);                                   // red band

                float aboveReversal = Mathf.Max(1f, Screen.height) * 0.05f;
                sc.PushTouchSample(new Vector2(100f, 900f));      // finger lands high
                sc.PushTouchSample(new Vector2(100f, 300f));      // bottom of the swing
                sc.PushTouchSample(new Vector2(100f, 300f + aboveReversal));   // upswing → latch
                Assert.IsTrue(sc.IsAimLocked, "Sanity: the upswing must have latched the aim");
                Assert.AreEqual(0.3f, sc.LastTimingAtLatch, 1e-3f);

                sc.EndExternalDrag(bypassFlickGate: true);
                Assert.IsTrue(fired, "Shot must resolve within EndExternalDrag");

                // The ball comes to rest — this is the beat HoleSessionDriver builds the record on.
                sc.CompleteShot();

                Assert.IsTrue(float.IsNaN(sc.LastTimingAtLatch),
                    "The live sample is gone by shot-complete — this is exactly why D1 exists");
                Assert.AreEqual(0.3f, sc.LastCommittedTiming01, 1e-3f,
                    "The committed snapshot must still name the flick the shot was judged on");
                Assert.AreEqual("red", GameSession.TimingBand(sc.LastCommittedTiming01));
                Assert.Less(sc.LastTimingPowerMul, 1f, "A 0.3 flick is in the red band and costs power");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
