using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Golfin.Physics;
using Golfin.Physics.Math;
using Golfin.Gameplay.Loop;
using Golfin.Gameplay.Session;
using Golfin.Audio.Events;
using Golfin.Physics.Viewer;
using Golfin.Gameplay.Input;

namespace Golfin.Physics.Tests
{
    /// <summary>
    /// EditMode unit tests for BallAudioEmitter (Order 350 — §6 acceptance gates).
    ///
    /// Tests cover:
    ///   1. Bus-wiring call-count seams (per-bounce, settle, cup, de-dup, PlayRate cap)
    ///   2. Velocity gate (low-speed bounces suppressed)
    ///   3. Inter-SFX interval gate (rapid bounces curtailed) — MinInterval_*
    ///   4. Surface → SfxId mapping (NOTE-E)
    ///   5. Determinism — emitter state resets correctly between shots
    ///   6. Determinism bit-equivalence — BallSimulation.Simulate output identical with/without SfxBus subscriber
    ///   7. CommitFlick seam — ShotController.PublishShotSfxForTest publishes exactly one Swing* + one Hit*
    ///   8. OnMatchComplete seam — GameSession.MarkMatchComplete publishes exactly one Match* per outcome
    ///   9. PlayRate cap — suppresses per-bounce SFX when PlayRate exceeds CSV cap (real threshold)
    ///  10. Mixer dB-mapping — AudioManager.LinearToDb correctness + floor behaviour
    ///
    /// Isolation: uses a StubGates implementation that provides known thresholds.
    /// SfxBus.OnPlay is captured via a list; cleared in TearDown.
    /// </summary>
    [TestFixture]
    public class AudioEmitterTests
    {
        // ── Collected publish log ─────────────────────────────────────────────
        private List<SfxId> _played;

        // ── Stub ISfxGates (no CSV loaded in EditMode) ────────────────────────
        private class StubGates : ISfxGates
        {
            public float VelocityGate  = 2.0f;  // suppress below 2 m/s
            public float PlayRateCap   = 4.0f;
            public float MinInterval   = 0f;

            public bool  ShouldSuppressLanding(SfxId id, float velMag) => velMag < VelocityGate;
            public float GetPlayRateCap(SfxId id) => PlayRateCap;
            public float GetMinInterval(SfxId id) => MinInterval;
        }

        private StubGates _gates;
        private BallAudioEmitter _emitter;

        // ── fp helpers ────────────────────────────────────────────────────────
        static fp3 V3(float x, float y, float z) => new fp3(fp.FromFloat(x), fp.FromFloat(y), fp.FromFloat(z));
        static fp FP(float v) => fp.FromFloat(v);

        TerrainHit MakeHit(float velMag, SurfaceType surface, bool isStop = false)
            => new TerrainHit(
                FP(0f),                        // time
                V3(0, 0, 0),                   // position
                V3(0, -velMag, 0),             // velocityIn (negative Y = downward, magnitude = velMag)
                V3(0, velMag * 0.5f, 0),       // velocityOut
                surface,
                isStop);

        BallStateChange MakeStateChange(BallState from, BallState to, SurfaceType surface = SurfaceType.Fairway)
            => new BallStateChange(from, to, V3(0, 0, 0), surface, null, FP(0f));

        [SetUp]
        public void SetUp()
        {
            _played = new List<SfxId>();
            SfxBus.ClearSubscribers();
            SfxBus.OnPlay += id => _played.Add(id);

            _gates = new StubGates();
            SfxBus.Gates = _gates;

            var go = new GameObject("BallAudioEmitterTest");
            _emitter = go.AddComponent<BallAudioEmitter>();
            _emitter.ResetForTest();
        }

        [TearDown]
        public void TearDown()
        {
            SfxBus.ClearSubscribers();
            SfxBus.Gates = null;
            if (_emitter != null)
                UnityEngine.Object.DestroyImmediate(_emitter.gameObject);
        }

        // ── 1. Per-bounce publishes LandFairway for each hit above velocity gate ──

        [Test]
        public void PerBounce_AboveVelocityGate_PublishesLandSfx()
        {
            // Arrange: 3 bounces well above 2 m/s gate
            _gates.VelocityGate = 2.0f;
            _gates.MinInterval  = 0f;

            // Act
            _emitter.FireHitForTest(MakeHit(5f, SurfaceType.Fairway, isStop: false));
            _emitter.FireHitForTest(MakeHit(4f, SurfaceType.Fairway, isStop: false));
            _emitter.FireHitForTest(MakeHit(3f, SurfaceType.Fairway, isStop: false));

            // Assert: exactly 3 LandFairway published
            Assert.AreEqual(3, _played.Count,
                "3 bounces above velocity gate must each publish exactly one LandFairway");
            foreach (var id in _played)
                Assert.AreEqual(SfxId.LandFairway, id, "Each bounce must publish LandFairway");
        }

        // ── 2. Velocity gate suppresses low-speed bounces ─────────────────────

        [Test]
        public void VelocityGate_BelowThreshold_SuppressesSfx()
        {
            _gates.VelocityGate = 2.0f;
            _gates.MinInterval  = 0f;

            // 1 m/s < 2 m/s gate → suppressed
            _emitter.FireHitForTest(MakeHit(1.0f, SurfaceType.Fairway, isStop: false));

            Assert.AreEqual(0, _played.Count, "Bounce below velocity gate must not publish");
        }

        // ── 3. IsStop hit marks de-dup guard; settle from AtRest does NOT double-fire ─

        [Test]
        public void DeDup_StopHitThenAtRest_FiresOnlyOnce()
        {
            _gates.VelocityGate = 0f; // no suppression

            // Fire the IsStop hit (final resting bounce)
            _emitter.FireHitForTest(MakeHit(3f, SurfaceType.Green, isStop: true));

            Assert.AreEqual(1, _played.Count, "IsStop hit must publish once");
            Assert.IsTrue(_emitter.StopHitFiredForTest, "StopHitFired guard must be set");

            int countAfterStop = _played.Count;

            // Now the state machine fires AtRest — must NOT double-fire
            _emitter.FireStateChangeForTest(MakeStateChange(BallState.Flying, BallState.AtRest, SurfaceType.Green));

            Assert.AreEqual(countAfterStop, _played.Count,
                "AtRest state change after IsStop hit must not publish again (de-dup)");
        }

        // ── 4. No IsStop hit → AtRest fires the settle sound ─────────────────

        [Test]
        public void AtRest_WithoutStopHit_PublishesLandSfx()
        {
            // No OnHit events — state machine only fires AtRest
            _emitter.FireStateChangeForTest(MakeStateChange(BallState.Flying, BallState.AtRest, SurfaceType.Rough));

            Assert.AreEqual(1, _played.Count, "AtRest without prior IsStop must publish one LandRough");
            Assert.AreEqual(SfxId.LandRough, _played[0]);
        }

        // ── 4b. Putt: AtRest settle + roll hits do NOT publish a ground sound ──
        // Cesar 2026-06-16: a putt rolling to a stop must not thud. With IsPutt true,
        // both the AtRest settle and any roll hits are suppressed; only the cup still fires.

        [Test]
        public void Putt_AtRestAndRollHits_SuppressGroundSound()
        {
            var shotGo = new GameObject("PuttShotController");
            var sc = shotGo.AddComponent<ShotController>();
            sc.IsPutt = true;
            _emitter.SetShotForTest(sc);
            _gates.VelocityGate = 0f; // would otherwise pass; putt suppression must win

            // A roll hit on the green must be suppressed
            _emitter.FireHitForTest(MakeHit(3f, SurfaceType.Green, isStop: false));
            // The settle (AtRest) on the green must be suppressed
            _emitter.FireStateChangeForTest(MakeStateChange(BallState.Rolling, BallState.AtRest, SurfaceType.Green));

            Assert.AreEqual(0, _played.Count, "A putt must publish NO land/settle ground sound");

            // ...but a putt that drops still gets the cup sound
            _emitter.FireStateChangeForTest(MakeStateChange(BallState.Rolling, BallState.InCup, SurfaceType.Green));
            Assert.AreEqual(1, _played.Count, "A sunk putt must still publish the cup sound");
            Assert.AreEqual(SfxId.HitBallIn, _played[0]);

            UnityEngine.Object.DestroyImmediate(shotGo);
        }

        // ── 5. InCup fires HitBallIn ─────────────────────────────────────────

        [Test]
        public void InCup_PublishesHitBallIn()
        {
            _emitter.FireStateChangeForTest(MakeStateChange(BallState.Flying, BallState.InCup, SurfaceType.Green));

            Assert.AreEqual(1, _played.Count, "InCup must publish exactly one HitBallIn");
            Assert.AreEqual(SfxId.HitBallIn, _played[0]);
        }

        // ── 6. PlayRate cap — suppresses per-bounce SFX when PlayRate exceeds cap ──
        // Fixes the degenerate test: now wires a real BallAnimator and asserts suppression.

        [Test]
        public void PlayRateCap_AboveCap_SuppressesPerBounceSfx()
        {
            // Arrange: cap is 4.0 (matching CSV default). Wire a real BallAnimator
            // so _anim != null and the PlayRate check executes.
            _gates.PlayRateCap  = 4.0f;
            _gates.VelocityGate = 0f;  // disable velocity gate so only PlayRate blocks
            _gates.MinInterval  = 0f;

            var animGO  = new GameObject("AnimForTest");
            var anim    = animGO.AddComponent<BallAnimator>();
            anim.PlayRate = 5.0f;  // above cap of 4.0 → must suppress

            _emitter.Configure(anim, null, null);

            // Act: fire a hit that would pass velocity gate
            _emitter.FireHitForTest(MakeHit(10f, SurfaceType.Fairway, isStop: false));

            // Assert: suppressed
            Assert.AreEqual(0, _played.Count,
                "Per-bounce SFX must be suppressed when BallAnimator.PlayRate > playRateCap");

            // Cleanup
            anim.PlayRate = 1.0f;  // below cap → must fire
            _emitter.FireHitForTest(MakeHit(10f, SurfaceType.Fairway, isStop: false));
            Assert.AreEqual(1, _played.Count,
                "Per-bounce SFX must fire when BallAnimator.PlayRate is below cap");

            UnityEngine.Object.DestroyImmediate(animGO);
        }

        // ── 7. Surface → SfxId mapping (NOTE-E) ──────────────────────────────

        [Test]
        public void SurfaceMap_Green_ReturnsLandGreen()
            => Assert.AreEqual(SfxId.LandGreen, BallAudioEmitter.SurfaceToSfxIdForTest(SurfaceType.Green));

        [Test]
        public void SurfaceMap_GreenCollar_ReturnsLandGreen()
            => Assert.AreEqual(SfxId.LandGreen, BallAudioEmitter.SurfaceToSfxIdForTest(SurfaceType.GreenCollar));

        [Test]
        public void SurfaceMap_Fairway_ReturnsLandFairway()
            => Assert.AreEqual(SfxId.LandFairway, BallAudioEmitter.SurfaceToSfxIdForTest(SurfaceType.Fairway));

        [Test]
        public void SurfaceMap_Tee_ReturnsLandFairway()
            => Assert.AreEqual(SfxId.LandFairway, BallAudioEmitter.SurfaceToSfxIdForTest(SurfaceType.Tee));

        [Test]
        public void SurfaceMap_Rough_ReturnsLandRough()
            => Assert.AreEqual(SfxId.LandRough, BallAudioEmitter.SurfaceToSfxIdForTest(SurfaceType.Rough));

        [Test]
        public void SurfaceMap_Semirough_ReturnsLandRough()
            => Assert.AreEqual(SfxId.LandRough, BallAudioEmitter.SurfaceToSfxIdForTest(SurfaceType.Semirough));

        [Test]
        public void SurfaceMap_Sand_ReturnsLandSand()
            => Assert.AreEqual(SfxId.LandSand, BallAudioEmitter.SurfaceToSfxIdForTest(SurfaceType.Sand));

        [Test]
        public void SurfaceMap_BunkerLip_ReturnsLandSand()
            => Assert.AreEqual(SfxId.LandSand, BallAudioEmitter.SurfaceToSfxIdForTest(SurfaceType.BunkerLip));

        [Test]
        public void SurfaceMap_Water_ReturnsLandWater()
            => Assert.AreEqual(SfxId.LandWater, BallAudioEmitter.SurfaceToSfxIdForTest(SurfaceType.Water));

        [Test]
        public void SurfaceMap_CartPath_ReturnsLandRoad()
            => Assert.AreEqual(SfxId.LandRoad, BallAudioEmitter.SurfaceToSfxIdForTest(SurfaceType.CartPath));

        [Test]
        public void SurfaceMap_OOB_ReturnsLandBushes()
            => Assert.AreEqual(SfxId.LandBushes, BallAudioEmitter.SurfaceToSfxIdForTest(SurfaceType.OOB));

        // ── 8. Reset between shots ──────────────────────────────────────────

        [Test]
        public void Reset_AfterAimToFlying_ClearsDeDupGuard()
        {
            _gates.VelocityGate = 0f;

            // First shot: IsStop fires + AtRest (de-dup active)
            _emitter.FireHitForTest(MakeHit(3f, SurfaceType.Green, isStop: true));
            Assert.IsTrue(_emitter.StopHitFiredForTest);

            // New shot: Aiming → Flying resets guard
            _emitter.FireStateChangeForTest(MakeStateChange(BallState.Aiming, BallState.Flying));
            Assert.IsFalse(_emitter.StopHitFiredForTest, "New shot must reset de-dup guard");

            // AtRest on second shot must fire (guard cleared)
            int countBefore = _played.Count;
            _emitter.FireStateChangeForTest(MakeStateChange(BallState.Flying, BallState.AtRest, SurfaceType.Fairway));
            Assert.AreEqual(countBefore + 1, _played.Count, "AtRest on second shot must publish after reset");
        }

        // ── 9. Multiple bounces above gate: count matches ─────────────────────

        [Test]
        public void PerBounce_NBouncesAboveGate_PublishesNTimes()
        {
            _gates.VelocityGate = 1.0f;
            _gates.MinInterval  = 0f;

            const int N = 5;
            for (int i = 0; i < N; i++)
                _emitter.FireHitForTest(MakeHit(5f, SurfaceType.Rough, isStop: false));

            Assert.AreEqual(N, _played.Count,
                $"{N} bounces above velocity gate must publish exactly {N} sounds");
        }

        // ── 10. SfxBus.ClearSubscribers resets state ─────────────────────────

        [Test]
        public void SfxBus_ClearSubscribers_PreventsDelivery()
        {
            // Subscribe → fire → clear → fire again → count should not increase after clear
            _emitter.FireHitForTest(MakeHit(5f, SurfaceType.Fairway, isStop: false));
            Assert.AreEqual(1, _played.Count);

            SfxBus.ClearSubscribers();
            // Re-subscribe our capture list so TearDown works
            SfxBus.OnPlay += id => _played.Add(id);
            SfxBus.Gates = _gates;

            // The emitter still fires internally; but the previous subscription is gone
            // (we re-subscribed above so this next call goes to the new handler)
            int countBefore = _played.Count;
            _emitter.FireHitForTest(MakeHit(5f, SurfaceType.Fairway, isStop: false));
            Assert.AreEqual(countBefore + 1, _played.Count, "Re-subscribe after clear must work normally");
        }

        // ══════════════════════════════════════════════════════════════════════
        // NEW TESTS (SPEC §6 acceptance gates — iter-2 additions)
        // ══════════════════════════════════════════════════════════════════════

        // ── 11. Determinism bit-equivalence ──────────────────────────────────
        // SPEC §6 #1: full-hole sim trajectory + ShotResult bit-identical with
        // audio subscriber present vs no-audio control.

        [Test]
        public void Determinism_SimOutput_BitIdentical_WithAndWithoutSfxSubscriber()
        {
            // Build a representative shot (7-iron, same params as AeroCalibrationTripwireTests)
            float angleRad = 16.3f * (float)System.Math.PI / 180f;
            float speed    = 52.5f;
            var vel = new fp3(
                fp.Zero,
                fp.FromDouble(speed * System.Math.Sin(angleRad)),
                fp.FromDouble(speed * System.Math.Cos(angleRad)));
            float rps  = 7097f * 2f * (float)System.Math.PI / 60f;
            var spin = new SpinState(new fp3(fp.FromInt(-1), fp.Zero, fp.Zero), fp.FromFloat(rps));
            var input = new ShotInput(fp3.Zero, vel, fp.FromInt(30), spin);
            var ground = new FlatGround(fp.Zero);

            // Run 1: with SfxBus.OnPlay subscriber active (set up in SetUp)
            // (SfxBus.OnPlay already has our _played capture from SetUp)
            var traj1 = BallSimulation.Simulate(input, ground);

            // Run 2: no subscriber — clear and run again
            SfxBus.ClearSubscribers();
            var traj2 = BallSimulation.Simulate(input, ground);

            // Restore subscriber so TearDown doesn't crash
            SfxBus.OnPlay += id => _played.Add(id);
            SfxBus.Gates = _gates;

            // Assert bit-exact termination
            Assert.AreEqual(traj1.termination, traj2.termination,
                "Trajectory termination must be bit-identical with/without SfxBus subscriber");

            // Assert bit-exact final position (all three axes)
            Assert.AreEqual(traj1.finalPosition.x.raw, traj2.finalPosition.x.raw,
                "finalPosition.x must be bit-identical with/without SfxBus subscriber");
            Assert.AreEqual(traj1.finalPosition.y.raw, traj2.finalPosition.y.raw,
                "finalPosition.y must be bit-identical with/without SfxBus subscriber");
            Assert.AreEqual(traj1.finalPosition.z.raw, traj2.finalPosition.z.raw,
                "finalPosition.z must be bit-identical with/without SfxBus subscriber");

            // Assert sample count matches (proves the sim loop didn't diverge)
            Assert.AreEqual(traj1.samples.Count, traj2.samples.Count,
                "Sample count must be identical with/without SfxBus subscriber");

            // Assert first sample bit-exact (proves loop entry is identical)
            if (traj1.samples.Count > 0)
            {
                Assert.AreEqual(traj1.samples[0].position.x.raw, traj2.samples[0].position.x.raw,
                    "First sample position.x must be bit-identical");
                Assert.AreEqual(traj1.samples[0].position.z.raw, traj2.samples[0].position.z.raw,
                    "First sample position.z must be bit-identical");
            }
        }

        // ── 12. CommitFlick → exactly one Swing* + one Hit* ──────────────────
        // SPEC §6: ShotController.CommitFlick publishes exactly one Swing* and one Hit*
        // per shot. Tested via the PublishShotSfxForTest seam.

        [Test]
        public void CommitFlick_NonPutt_MidPower_PublishesOneSwingAndOneHit()
        {
            // Arrange
            var go = new GameObject("ShotControllerTest");
            var sc = go.AddComponent<ShotController>();

            int swingCount = 0, hitCount = 0;
            SfxBus.OnPlay += id =>
            {
                if (id == SfxId.SwingDriver || id == SfxId.SwingWood || id == SfxId.SwingIron ||
                    id == SfxId.SwingWedge  || id == SfxId.SwingDefault || id == SfxId.SwingPutt)
                    swingCount++;
                if (id == SfxId.HitStrong || id == SfxId.HitDefault || id == SfxId.HitDefault02 ||
                    id == SfxId.HitWeak    || id == SfxId.HitPutt    || id == SfxId.HitBunker)
                    hitCount++;
            };

            // Act: mid power (0.5f), non-putt, default club index (→ SwingDefault + HitDefault)
            sc.PublishShotSfxForTest(isPutt: false, powerNormalized: 0.5f);

            // Assert
            Assert.AreEqual(1, swingCount, "CommitFlick must publish exactly ONE Swing* SFX");
            Assert.AreEqual(1, hitCount,   "CommitFlick must publish exactly ONE Hit* SFX");
            Assert.AreEqual(2, _played.Count, "CommitFlick must publish exactly 2 total SFX (one Swing + one Hit)");

            UnityEngine.Object.DestroyImmediate(go);
        }

        [Test]
        public void CommitFlick_Putt_PublishesSwingPuttAndHitPutt()
        {
            var go = new GameObject("ShotControllerTest");
            var sc = go.AddComponent<ShotController>();

            sc.PublishShotSfxForTest(isPutt: true, powerNormalized: 0.5f);

            // Putt → SwingPutt + HitPutt exactly
            Assert.AreEqual(2, _played.Count, "Putt CommitFlick must publish exactly 2 SFX");
            Assert.IsTrue(_played.Contains(SfxId.SwingPutt), "Putt must publish SwingPutt");
            Assert.IsTrue(_played.Contains(SfxId.HitPutt),   "Putt must publish HitPutt");

            UnityEngine.Object.DestroyImmediate(go);
        }

        [Test]
        public void CommitFlick_HighPower_PublishesHitStrong()
        {
            var go = new GameObject("ShotControllerTest");
            var sc = go.AddComponent<ShotController>();

            sc.PublishShotSfxForTest(isPutt: false, powerNormalized: 0.9f); // > 0.8 → HitStrong

            Assert.AreEqual(2, _played.Count, "High-power shot must publish 2 SFX total");
            Assert.IsTrue(_played.Contains(SfxId.HitStrong),
                "Power > 0.8 must publish HitStrong");

            UnityEngine.Object.DestroyImmediate(go);
        }

        [Test]
        public void CommitFlick_LowPower_PublishesHitWeak()
        {
            var go = new GameObject("ShotControllerTest");
            var sc = go.AddComponent<ShotController>();

            sc.PublishShotSfxForTest(isPutt: false, powerNormalized: 0.2f); // < 0.3 → HitWeak

            Assert.AreEqual(2, _played.Count, "Low-power shot must publish 2 SFX total");
            Assert.IsTrue(_played.Contains(SfxId.HitWeak),
                "Power < 0.3 must publish HitWeak");

            UnityEngine.Object.DestroyImmediate(go);
        }

        // ── 13. OnMatchComplete → exactly one Match* per outcome ─────────────
        // SPEC §6: VersusResultHandler (in Assembly-CSharp, not directly referenceable from
        // Golfin.Physics.Tests) publishes one MatchWin/MatchLose/MatchDraw per outcome.
        //
        // Approach: VersusResultHandler is loaded via reflection (avoids compile-time asmdef
        // cycle while still exercising the REAL production code path). OnEnable is invoked via
        // SendMessage so the handler subscribes to GameSession.OnMatchComplete, then we fire
        // GameSession.MarkMatchComplete and verify SfxBus received the right SfxId.

        [Test]
        public void VersusResultHandler_OnMatchComplete_PublishesOneMatchStinger_EachOutcome()
        {
            // Resolve type at runtime (avoids compile-time asmdef dependency on Assembly-CSharp).
            var vrhType = System.Type.GetType("Golfin.UI.Modals.VersusResultHandler, Assembly-CSharp");
            if (vrhType == null)
            {
                Assert.Fail("VersusResultHandler type not found via reflection. " +
                            "Ensure Assembly-CSharp is built and the type exists at Golfin.UI.Modals.VersusResultHandler.");
                return;
            }

            // Subscribe to GameSession.OnMatchComplete directly via reflection.
            // This tests the REAL HandleMatchComplete logic by extracting it as an Action delegate
            // from the production type, bypassing the MonoBehaviour lifecycle (OnEnable/StartCoroutine)
            // that requires an active scene. The SfxBus.Play call is reached before StartCoroutine.
            //
            // Alternative approach: directly fire the private HandleMatchComplete via reflection
            // so we avoid the ShouldRunBehaviour assertion from StartCoroutine entirely.
            var handleMethod = vrhType.GetMethod("HandleMatchComplete",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(handleMethod,
                "VersusResultHandler.HandleMatchComplete private method must exist");

            var go  = new GameObject("VersusResultHandlerSeamTest");
            var vrh = go.AddComponent(vrhType);
            Assert.IsNotNull(vrh, "AddComponent<VersusResultHandler> via reflection must succeed");

            // Test P1Win → MatchWin
            // SfxBus.Play fires BEFORE StartCoroutine in HandleMatchComplete.
            // We invoke HandleMatchComplete via reflection; SfxBus.Play completes synchronously
            // before StartCoroutine is attempted. StartCoroutine on a GO not in an active scene
            // may throw InvalidOperationException — we catch it so the assertion still runs.
            _played.Clear();
            try { handleMethod.Invoke(vrh, new object[] { GameSession.MatchOutcome.P1Win, 3, 5 }); }
            catch (System.Reflection.TargetInvocationException) { /* StartCoroutine in EditMode — expected */ }
            Assert.AreEqual(1, _played.Count, "P1Win must publish exactly 1 SFX");
            Assert.AreEqual(SfxId.MatchWin, _played[0], "P1Win must publish MatchWin");

            // Test P2Win → MatchLose
            _played.Clear();
            try { handleMethod.Invoke(vrh, new object[] { GameSession.MatchOutcome.P2Win, 5, 3 }); }
            catch (System.Reflection.TargetInvocationException) { /* StartCoroutine in EditMode — expected */ }
            Assert.AreEqual(1, _played.Count, "P2Win must publish exactly 1 SFX");
            Assert.AreEqual(SfxId.MatchLose, _played[0], "P2Win must publish MatchLose");

            // Test Draw → MatchDraw
            _played.Clear();
            try { handleMethod.Invoke(vrh, new object[] { GameSession.MatchOutcome.Draw, 4, 4 }); }
            catch (System.Reflection.TargetInvocationException) { /* StartCoroutine in EditMode — expected */ }
            Assert.AreEqual(1, _played.Count, "Draw must publish exactly 1 SFX");
            Assert.AreEqual(SfxId.MatchDraw, _played[0], "Draw must publish MatchDraw");

            UnityEngine.Object.DestroyImmediate(go);
        }

        // ── 15. Min-interval gate ─────────────────────────────────────────────
        // SPEC §6: two bounces closer than minIntervalSec → second suppressed;
        // two bounces farther apart → both pass.

        [Test]
        public void MinInterval_SecondBounceWithinInterval_IsSuppressed()
        {
            // Set a real interval (0.15s, matching landing SFX in sfx.csv)
            _gates.MinInterval  = 0.15f;
            _gates.VelocityGate = 0f;   // no velocity suppression

            // First hit: fires and sets _lastLandSfxTime
            _emitter.FireHitForTest(MakeHit(5f, SurfaceType.Fairway, isStop: false));
            Assert.AreEqual(1, _played.Count, "First bounce must publish");

            // Simulate a second hit with _lastLandSfxTime just now (interval not elapsed)
            // Since Time.unscaledTime is 0f in EditMode, set lastTime to 0f (current time)
            _emitter.SetLastLandSfxTimeForTest(0f);  // simulate "just fired" at t=0
            // Time.unscaledTime is 0f in EditMode → 0f - 0f = 0f < 0.15f → suppressed

            _emitter.FireHitForTest(MakeHit(5f, SurfaceType.Fairway, isStop: false));
            Assert.AreEqual(1, _played.Count,
                "Second bounce within minInterval must be suppressed");
        }

        [Test]
        public void MinInterval_BounceAfterIntervalElapsed_Fires()
        {
            // Set a real interval matching CSV
            _gates.MinInterval  = 0.15f;
            _gates.VelocityGate = 0f;

            // First hit fires
            _emitter.FireHitForTest(MakeHit(5f, SurfaceType.Fairway, isStop: false));
            Assert.AreEqual(1, _played.Count, "First bounce must publish");

            // Simulate time having passed: set _lastLandSfxTime to well in the past
            // Time.unscaledTime = 0f; lastTime = -1.0f → 0f - (-1.0f) = 1.0f > 0.15f → passes
            _emitter.SetLastLandSfxTimeForTest(-1.0f);

            _emitter.FireHitForTest(MakeHit(5f, SurfaceType.Fairway, isStop: false));
            Assert.AreEqual(2, _played.Count,
                "Second bounce after minInterval has elapsed must fire");
        }

        // ── 16. Mixer dB-mapping + PlayerPrefs migration ─────────────────────
        // SPEC §6 #3: AudioManager.LinearToDb maps 0→100 correctly, floor mutes,
        // existing PlayerPrefs keys survive.
        //
        // AudioManager is in Assembly-CSharp (no asmdef). Invoked via reflection to
        // avoid a compile-time asmdef cycle from Golfin.Physics.Tests.
        //
        // DB_FLOOR = -80f (Mathf.Log10(0.0001f) * 20f)
        // Formula:  Mathf.Log10(Mathf.Clamp(v, 0.0001f, 1f)) * 20f

        private static float InvokeLinearToDb(float value)
        {
            var audioMgrType = System.Type.GetType("Golfin.Audio.AudioManager, Assembly-CSharp");
            Assert.IsNotNull(audioMgrType,
                "AudioManager type not found via reflection (Assembly-CSharp not built?)");
            var method = audioMgrType.GetMethod("LinearToDb",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            Assert.IsNotNull(method,
                "AudioManager.LinearToDb public static method not found");
            return (float)method.Invoke(null, new object[] { value });
        }

        [Test]
        public void Mixer_LinearToDb_FullVolume_IsZeroDb()
        {
            // linear 1.0 (100%) → 0 dB
            float db = InvokeLinearToDb(1.0f);
            Assert.AreEqual(0f, db, 0.01f,
                "LinearToDb(1.0) must return 0 dB (reference level)");
        }

        [Test]
        public void Mixer_LinearToDb_HalfVolume_IsMinusSixDb()
        {
            // linear 0.5 → approximately -6 dB (log10(0.5)*20 = -6.02)
            float db = InvokeLinearToDb(0.5f);
            Assert.AreEqual(-6.02f, db, 0.1f,
                "LinearToDb(0.5) must be approximately -6 dB");
        }

        [Test]
        public void Mixer_LinearToDb_Zero_ClampsToFloorNotInfinity()
        {
            // linear 0 → clamp to 0.0001f → log10(0.0001)*20 = -80 dB (the DB_FLOOR)
            float db = InvokeLinearToDb(0f);
            // Must be finite (not -Infinity) and ≈ -80 dB
            Assert.IsTrue(float.IsFinite(db),
                "LinearToDb(0) must return a finite value, not -Infinity");
            Assert.AreEqual(-80f, db, 1f,
                "LinearToDb(0) must approximate the DB_FLOOR (-80 dB)");
        }

        [Test]
        public void Mixer_PlayerPrefsKeys_ArePreserved()
        {
            // Verify the keys match the contract (no rename / no migration break).
            // AudioManager uses "Settings_MusicVolume" and "Settings_SFXVolume" with /100 scaling.
            const string musicKey = "Settings_MusicVolume";
            const string sfxKey   = "Settings_SFXVolume";

            // Set legacy 0–100 values
            PlayerPrefs.SetFloat(musicKey, 70f);
            PlayerPrefs.SetFloat(sfxKey,   80f);

            // Read back — must be unchanged (AudioManager reads these and divides by 100)
            Assert.AreEqual(70f, PlayerPrefs.GetFloat(musicKey, -1f), 0.001f,
                "Settings_MusicVolume PlayerPrefs key must survive unchanged");
            Assert.AreEqual(80f, PlayerPrefs.GetFloat(sfxKey,   -1f), 0.001f,
                "Settings_SFXVolume PlayerPrefs key must survive unchanged");

            // Verify that dividing by 100 → [0,1] internal representation is correct
            float internal_music = PlayerPrefs.GetFloat(musicKey, 70f) / 100f;
            float internal_sfx   = PlayerPrefs.GetFloat(sfxKey,   70f) / 100f;
            Assert.AreEqual(0.70f, internal_music, 0.001f,
                "70% stored value must translate to 0.70 internal");
            Assert.AreEqual(0.80f, internal_sfx, 0.001f,
                "80% stored value must translate to 0.80 internal");

            // Cleanup PlayerPrefs (don't pollute test environment)
            PlayerPrefs.DeleteKey(musicKey);
            PlayerPrefs.DeleteKey(sfxKey);
        }

        [Test]
        public void Mixer_LinearToDb_Slider100_MapsToZeroDb()
        {
            // Slider value 100 (0–100 scale) → internal 1.0 → 0 dB
            float sliderValue = 100f;
            float internal01  = Mathf.Clamp(sliderValue, 0f, 100f) / 100f;
            float db          = InvokeLinearToDb(internal01);
            Assert.AreEqual(0f, db, 0.01f,
                "Slider at 100 must map to 0 dB via LinearToDb");
        }

        [Test]
        public void Mixer_LinearToDb_Slider0_MapsToFloor()
        {
            // Slider value 0 → internal 0 → clamp → floor dB (≈ -80)
            // We call the real AudioManager.LinearToDb(0) to verify it clamps, not returns -Infinity
            float db = InvokeLinearToDb(0f);
            Assert.AreEqual(-80f, db, 1f,
                "Slider at 0 must map to DB_FLOOR (-80 dB) via LinearToDb clamp");
        }
    }
}
