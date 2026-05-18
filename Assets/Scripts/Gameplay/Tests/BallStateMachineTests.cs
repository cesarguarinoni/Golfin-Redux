using System;
using System.Collections.Generic;
using NUnit.Framework;
using Golfin.Physics;
using Golfin.Physics.Math;
using Golfin.Gameplay.Loop;

namespace Golfin.Gameplay.Tests
{
    // ── Test helpers ──────────────────────────────────────────────────────────

    /// <summary>Constant-surface provider for tests.</summary>
    class FakeSurface : ISurfaceProvider
    {
        readonly SurfaceType _type;
        public FakeSurface(SurfaceType t = SurfaceType.Fairway) { _type = t; }
        public SurfaceType Classify(fp worldX, fp worldZ) => _type;
    }

    /// <summary>Cup detector that returns true for one specific position.</summary>
    class StubCupDetector : ICupDetector
    {
        readonly fp3 _cupPos;
        public StubCupDetector(fp3 cupPos) { _cupPos = cupPos; }
        public bool IsInCup(fp3 position, fp ballRadius)
            => position.x == _cupPos.x && position.y == _cupPos.y && position.z == _cupPos.z;
        // Velocity-aware overload: stub ignores velocity gate (geometry-only, for test isolation).
        public bool IsInCup(fp3 position, fp ballRadius, fp3 velocity)
            => IsInCup(position, ballRadius);
    }

    /// <summary>Utility builders for synthetic Trajectory objects.</summary>
    static class TrajectoryFactory
    {
        public static fp3 At(float x, float y, float z)
            => new fp3(fp.FromFloat(x), fp.FromFloat(y), fp.FromFloat(z));

        public static fp T(float t) => fp.FromFloat(t);

        public static TrajectorySample Sample(float t, float x, float y, float z)
            => new TrajectorySample(T(t), At(x, y, z), fp3.Zero);

        public static TerrainHit Hit(float t, float x, float y, float z, SurfaceType surf, bool isStop)
            => new TerrainHit(T(t), At(x, y, z), fp3.Zero, fp3.Zero, surf, isStop);

        /// <summary>
        /// Simple: one airborne arc, lands, rolls, stops. BallStopped termination.
        /// terrainHits[0] = non-stop (first bounce onto fairway)
        /// terrainHits[1] = stop     (roll to rest on fairway)
        /// </summary>
        public static Trajectory BallStoppedOnFairway(fp3 startPos)
        {
            var samples = new List<TrajectorySample>
            {
                Sample(0f,  startPos.x.ToFloat(), startPos.y.ToFloat(), startPos.z.ToFloat()),
                Sample(1f,  10f, 2f, 0f),
                Sample(2f,  20f, 0.1f, 0f),
                Sample(3f,  25f, 0f, 0f),
            };
            var hits = new List<TerrainHit>
            {
                Hit(2f,  20f, 0f, 0f, SurfaceType.Fairway, false), // bounce
                Hit(3f,  25f, 0f, 0f, SurfaceType.Fairway, true),  // stop
            };
            return new Trajectory(samples, At(25f, 0f, 0f), fp3.Zero, T(3f),
                                  TerminationReason.BallStopped, hits);
        }

        /// <summary>Single OB-water shot — no terrain hits before water event.</summary>
        public static Trajectory HitWater(fp3 startPos)
        {
            var samples = new List<TrajectorySample>
            {
                Sample(0f,  startPos.x.ToFloat(), startPos.y.ToFloat(), startPos.z.ToFloat()),
                Sample(1.5f, 15f, -0.5f, 0f),
            };
            var hits = new List<TerrainHit>
            {
                Hit(1.5f, 15f, -0.5f, 0f, SurfaceType.Water, true),
            };
            return new Trajectory(samples, At(15f, -0.5f, 0f), fp3.Zero, T(1.5f),
                                  TerminationReason.HitWater, hits);
        }

        public static Trajectory HitOOB(fp3 startPos)
        {
            var samples = new List<TrajectorySample>
            {
                Sample(0f,  startPos.x.ToFloat(), startPos.y.ToFloat(), startPos.z.ToFloat()),
                Sample(2f,  30f, 0f, 0f),
            };
            var hits = new List<TerrainHit>
            {
                Hit(2f, 30f, 0f, 0f, SurfaceType.OOB, true),
            };
            return new Trajectory(samples, At(30f, 0f, 0f), fp3.Zero, T(2f),
                                  TerminationReason.HitOOB, hits);
        }

        public static Trajectory ExitedWorldBounds(fp3 startPos)
        {
            var samples = new List<TrajectorySample>
            {
                Sample(0f,  startPos.x.ToFloat(), startPos.y.ToFloat(), startPos.z.ToFloat()),
                Sample(5f,  999f, 999f, 999f),
            };
            return new Trajectory(samples, At(999f, 999f, 999f), fp3.Zero, T(5f),
                                  TerminationReason.ExitedWorldBounds, new List<TerrainHit>());
        }

        /// <summary>
        /// Three-bounce shot that then stops.
        /// Expected state sequence: Flying → Rolling → Flying → Rolling → Flying → Rolling → AtRest
        /// </summary>
        public static Trajectory ThreeBounces(fp3 startPos)
        {
            var samples = new List<TrajectorySample>
            {
                Sample(0f,   startPos.x.ToFloat(), startPos.y.ToFloat(), startPos.z.ToFloat()),
                Sample(1f,   10f, 2f, 0f),
                Sample(2f,   20f, 0f, 0f),
                Sample(3f,   30f, 1f, 0f),
                Sample(4f,   40f, 0f, 0f),
                Sample(5f,   50f, 0.5f, 0f),
                Sample(6f,   55f, 0f, 0f),
                Sample(7f,   58f, 0f, 0f),
            };
            var hits = new List<TerrainHit>
            {
                Hit(2f, 20f, 0f, 0f, SurfaceType.Fairway, false), // bounce 1
                Hit(4f, 40f, 0f, 0f, SurfaceType.Fairway, false), // bounce 2
                Hit(6f, 55f, 0f, 0f, SurfaceType.Fairway, false), // bounce 3
                Hit(7f, 58f, 0f, 0f, SurfaceType.Fairway, true),  // stop
            };
            return new Trajectory(samples, At(58f, 0f, 0f), fp3.Zero, T(7f),
                                  TerminationReason.BallStopped, hits);
        }

        /// <summary>
        /// Cup-scan trajectory: sample at t=2 is the in-cup position.
        /// BallStopped termination (cup detected overrides AtRest).
        /// </summary>
        public static Trajectory WithCupSample(fp3 startPos, fp3 cupPos)
        {
            var samples = new List<TrajectorySample>
            {
                Sample(0f,   startPos.x.ToFloat(), startPos.y.ToFloat(), startPos.z.ToFloat()),
                Sample(1f,   10f, 2f, 0f),
                // cup sample at t=2:
                new TrajectorySample(T(2f), cupPos, fp3.Zero),
                Sample(3f,   30f, 0f, 0f),
            };
            var hits = new List<TerrainHit>
            {
                Hit(3f, 30f, 0f, 0f, SurfaceType.Green, false), // bounce (roll)
                Hit(4f, 30f, 0f, 0f, SurfaceType.Green, true),
            };
            return new Trajectory(samples, At(30f, 0f, 0f), fp3.Zero, T(4f),
                                  TerminationReason.BallStopped, hits);
        }
    }

    // ── Test class ────────────────────────────────────────────────────────────

    [TestFixture]
    public class BallStateMachineTests
    {
        static readonly fp3  kStart = TrajectoryFactory.At(0f, 0f, 0f);
        static readonly fp   kBallR = fp.FromFloat(0.02135f);

        BallStateMachine MakeSM(ICupDetector cup = null)
            => new BallStateMachine(new FakeSurface(), cup);

        // ─────────────────────────────────────────────────────────────────────
        // 1. Initial state
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        public void Aiming_IsInitialState()
        {
            var sm = MakeSM();
            Assert.AreEqual(BallState.Aiming, sm.State);
        }

        // ─────────────────────────────────────────────────────────────────────
        // 2. OnTrajectoryComputed transitions to Flying; no ShotComplete yet
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        public void OnTrajectoryComputed_FromAiming_TransitionsToFlying()
        {
            var sm = MakeSM();
            var changes = new List<BallStateChange>();
            bool shotCompleteFired = false;

            sm.OnStateChanged  += c => changes.Add(c);
            sm.OnShotComplete  += _ => shotCompleteFired = true;

            var traj = TrajectoryFactory.BallStoppedOnFairway(kStart);
            sm.OnTrajectoryComputed(kStart, traj, kBallR);

            Assert.AreEqual(BallState.Flying, sm.State, "State should be Flying immediately after OnTrajectoryComputed (non-headless)");
            Assert.AreEqual(1, changes.Count, "Exactly one OnStateChanged should have fired");
            Assert.AreEqual(BallState.Aiming, changes[0].Previous, "Previous should be Aiming");
            Assert.AreEqual(BallState.Flying, changes[0].Next,     "Next should be Flying");
            Assert.IsFalse(shotCompleteFired, "OnShotComplete must NOT fire before Tick drains");
        }

        // ─────────────────────────────────────────────────────────────────────
        // 3. Flying → Rolling → AtRest after animator stops
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        public void Flying_IsPlayingFalse_DrainsToAtRest()
        {
            var sm = MakeSM();
            var changes = new List<BallStateChange>();
            ShotResult? result = null;

            sm.OnStateChanged += c => changes.Add(c);
            sm.OnShotComplete += r => result = r;

            var traj = TrajectoryFactory.BallStoppedOnFairway(kStart);
            sm.OnTrajectoryComputed(kStart, traj, kBallR);

            // Simulate animator: playing for one tick, then stops.
            sm.Tick(true);   // still playing
            sm.Tick(false);  // animator finished → drain

            // Expected sequence AFTER the first Aiming→Flying:
            // Flying → Rolling (bounce at t=2), Rolling → AtRest (stop at t=3)
            // First change already fired: Aiming→Flying
            // So changes[0] = Aiming→Flying, changes[1] = Flying→Rolling, changes[2] = Rolling→AtRest
            Assert.AreEqual(3, changes.Count, $"Expected 3 total state changes, got {changes.Count}");
            Assert.AreEqual(BallState.Aiming,  changes[0].Previous);
            Assert.AreEqual(BallState.Flying,  changes[0].Next);
            Assert.AreEqual(BallState.Flying,  changes[1].Previous);
            Assert.AreEqual(BallState.Rolling, changes[1].Next);
            Assert.AreEqual(BallState.Rolling, changes[2].Previous);
            Assert.AreEqual(BallState.AtRest,  changes[2].Next);

            Assert.AreEqual(BallState.AtRest, sm.State);
            Assert.IsTrue(result.HasValue, "OnShotComplete should have fired");
            Assert.AreEqual(BallState.AtRest, result.Value.TerminalState);
        }

        // ─────────────────────────────────────────────────────────────────────
        // 4. HitWater → OB(Water)
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        public void Flying_HitWater_TerminalIsOBWater()
        {
            var sm = MakeSM();
            ShotResult? result = null;
            sm.OnShotComplete += r => result = r;

            var traj = TrajectoryFactory.HitWater(kStart);
            sm.OnTrajectoryComputed(kStart, traj, kBallR);
            sm.Tick(true);
            sm.Tick(false);

            Assert.IsTrue(result.HasValue);
            Assert.AreEqual(BallState.OB,              result.Value.TerminalState);
            Assert.IsTrue(result.Value.OBReason.HasValue);
            Assert.AreEqual(Golfin.Gameplay.Loop.OBReason.Water, result.Value.OBReason.Value);
        }

        // ─────────────────────────────────────────────────────────────────────
        // 5. HitOOB → OB(OutOfBounds)
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        public void Flying_HitOOB_TerminalIsOBOutOfBounds()
        {
            var sm = MakeSM();
            ShotResult? result = null;
            sm.OnShotComplete += r => result = r;

            var traj = TrajectoryFactory.HitOOB(kStart);
            sm.OnTrajectoryComputed(kStart, traj, kBallR);
            sm.Tick(true);
            sm.Tick(false);

            Assert.IsTrue(result.HasValue);
            Assert.AreEqual(BallState.OB, result.Value.TerminalState);
            Assert.AreEqual(Golfin.Gameplay.Loop.OBReason.OutOfBounds, result.Value.OBReason.Value);
        }

        // ─────────────────────────────────────────────────────────────────────
        // 6. ExitedWorldBounds → OB(ExitedWorldBounds)
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        public void Flying_ExitedWorldBounds_TerminalIsOBExited()
        {
            var sm = MakeSM();
            ShotResult? result = null;
            sm.OnShotComplete += r => result = r;

            var traj = TrajectoryFactory.ExitedWorldBounds(kStart);
            sm.OnTrajectoryComputed(kStart, traj, kBallR);
            sm.Tick(true);
            sm.Tick(false);

            Assert.IsTrue(result.HasValue);
            Assert.AreEqual(BallState.OB, result.Value.TerminalState);
            Assert.AreEqual(Golfin.Gameplay.Loop.OBReason.ExitedWorldBounds, result.Value.OBReason.Value);
        }

        // ─────────────────────────────────────────────────────────────────────
        // 7. Cup detector positive scan → InCup
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        public void CupDetector_PositiveScan_TerminalIsInCup()
        {
            fp3 cupPos = TrajectoryFactory.At(15f, 0f, 5f);
            var sm = MakeSM(new StubCupDetector(cupPos));
            ShotResult? result = null;
            sm.OnShotComplete += r => result = r;

            var traj = TrajectoryFactory.WithCupSample(kStart, cupPos);
            sm.OnTrajectoryComputed(kStart, traj, kBallR);
            sm.Tick(true);
            sm.Tick(false);

            Assert.IsTrue(result.HasValue);
            Assert.AreEqual(BallState.InCup, result.Value.TerminalState);
            Assert.AreEqual(cupPos.x, result.Value.EndPosition.x, "EndPosition.x should be cup x");
            Assert.AreEqual(cupPos.z, result.Value.EndPosition.z, "EndPosition.z should be cup z");
        }

        // ─────────────────────────────────────────────────────────────────────
        // 8. Three bounces → correct state sequence
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        public void MultipleBounces_StateSequencePreserved()
        {
            var sm = MakeSM();
            var changes = new List<BallStateChange>();
            sm.OnStateChanged += c => changes.Add(c);

            var traj = TrajectoryFactory.ThreeBounces(kStart);
            sm.OnTrajectoryComputed(kStart, traj, kBallR);
            sm.Tick(true);
            sm.Tick(false);

            // Expected sequence:
            // [0] Aiming→Flying (fires in OnTrajectoryComputed)
            // [1] Flying→Rolling  (bounce 1)
            // [2] Rolling→Flying  (re-launch 1)
            // [3] Flying→Rolling  (bounce 2)
            // [4] Rolling→Flying  (re-launch 2)
            // [5] Flying→Rolling  (bounce 3)
            // [6] Rolling→AtRest  (stop)
            Assert.AreEqual(7, changes.Count, $"Expected 7 transitions, got {changes.Count}");

            Assert.AreEqual(BallState.Aiming,  changes[0].Previous); Assert.AreEqual(BallState.Flying,  changes[0].Next);
            Assert.AreEqual(BallState.Flying,  changes[1].Previous); Assert.AreEqual(BallState.Rolling, changes[1].Next);
            Assert.AreEqual(BallState.Rolling, changes[2].Previous); Assert.AreEqual(BallState.Flying,  changes[2].Next);
            Assert.AreEqual(BallState.Flying,  changes[3].Previous); Assert.AreEqual(BallState.Rolling, changes[3].Next);
            Assert.AreEqual(BallState.Rolling, changes[4].Previous); Assert.AreEqual(BallState.Flying,  changes[4].Next);
            Assert.AreEqual(BallState.Flying,  changes[5].Previous); Assert.AreEqual(BallState.Rolling, changes[5].Next);
            Assert.AreEqual(BallState.Rolling, changes[6].Previous); Assert.AreEqual(BallState.AtRest,  changes[6].Next);

            Assert.AreEqual(BallState.AtRest, sm.State);
        }

        // ─────────────────────────────────────────────────────────────────────
        // 9. Headless: all transitions fire synchronously inside OnTrajectoryComputed
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        public void Headless_FiresAllTransitionsSynchronously()
        {
            var sm = MakeSM();
            sm.Headless = true;

            var changes = new List<BallStateChange>();
            ShotResult? result = null;
            sm.OnStateChanged += c => changes.Add(c);
            sm.OnShotComplete += r => result = r;

            var traj = TrajectoryFactory.BallStoppedOnFairway(kStart);
            sm.OnTrajectoryComputed(kStart, traj, kBallR);

            // No Tick call — everything should already be done.
            Assert.IsTrue(result.HasValue, "OnShotComplete must fire inside OnTrajectoryComputed in headless mode");
            Assert.AreEqual(BallState.AtRest, sm.State);

            // Compare with non-headless path result.
            var smLive = MakeSM();
            ShotResult? liveResult = null;
            smLive.OnShotComplete += r => liveResult = r;
            smLive.OnTrajectoryComputed(kStart, traj, kBallR);
            smLive.Tick(true);
            smLive.Tick(false);

            Assert.IsTrue(liveResult.HasValue);
            Assert.AreEqual(result.Value.TerminalState, liveResult.Value.TerminalState);
            Assert.AreEqual(result.Value.BounceCount,   liveResult.Value.BounceCount);
            Assert.AreEqual(result.Value.SimDuration,   liveResult.Value.SimDuration);
            Assert.AreEqual(result.Value.EndPosition.x, liveResult.Value.EndPosition.x);
            Assert.AreEqual(result.Value.EndPosition.z, liveResult.Value.EndPosition.z);
        }

        // ─────────────────────────────────────────────────────────────────────
        // 10. ReArm from AtRest returns to Aiming
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        public void ReArm_FromAtRest_ReturnsToAiming()
        {
            var sm = MakeSM();
            var rearmChanges = new List<BallStateChange>();

            // Drive to AtRest.
            var traj = TrajectoryFactory.BallStoppedOnFairway(kStart);
            sm.OnTrajectoryComputed(kStart, traj, kBallR);
            sm.Tick(true); sm.Tick(false);
            Assert.AreEqual(BallState.AtRest, sm.State);

            sm.OnStateChanged += c => rearmChanges.Add(c);
            sm.ReArm();

            Assert.AreEqual(BallState.Aiming, sm.State);
            Assert.AreEqual(1, rearmChanges.Count);
            Assert.AreEqual(BallState.AtRest, rearmChanges[0].Previous);
            Assert.AreEqual(BallState.Aiming, rearmChanges[0].Next);
        }

        // ─────────────────────────────────────────────────────────────────────
        // 11. ReArm from InCup returns to Aiming
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        public void ReArm_FromInCup_ReturnsToAiming()
        {
            fp3 cupPos = TrajectoryFactory.At(15f, 0f, 5f);
            var sm = MakeSM(new StubCupDetector(cupPos));
            sm.Headless = true;

            var traj = TrajectoryFactory.WithCupSample(kStart, cupPos);
            sm.OnTrajectoryComputed(kStart, traj, kBallR);
            Assert.AreEqual(BallState.InCup, sm.State);

            var rearmChanges = new List<BallStateChange>();
            sm.OnStateChanged += c => rearmChanges.Add(c);
            sm.ReArm();

            Assert.AreEqual(BallState.Aiming, sm.State);
            Assert.AreEqual(1, rearmChanges.Count);
            Assert.AreEqual(BallState.InCup,  rearmChanges[0].Previous);
            Assert.AreEqual(BallState.Aiming, rearmChanges[0].Next);
        }

        // ─────────────────────────────────────────────────────────────────────
        // 12. ReArm from OB returns to Aiming
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        public void ReArm_FromOB_ReturnsToAiming()
        {
            var sm = MakeSM();
            sm.Headless = true;

            var traj = TrajectoryFactory.HitWater(kStart);
            sm.OnTrajectoryComputed(kStart, traj, kBallR);
            Assert.AreEqual(BallState.OB, sm.State);

            var rearmChanges = new List<BallStateChange>();
            sm.OnStateChanged += c => rearmChanges.Add(c);
            sm.ReArm();

            Assert.AreEqual(BallState.Aiming, sm.State);
            Assert.AreEqual(1, rearmChanges.Count);
            Assert.AreEqual(BallState.OB,     rearmChanges[0].Previous);
            Assert.AreEqual(BallState.Aiming, rearmChanges[0].Next);
        }

        // ─────────────────────────────────────────────────────────────────────
        // 13. Determinism: same trajectory through two SM instances = identical sequences
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        public void Determinism_SameTrajectoryTwice_IdenticalEventSequence()
        {
            var traj = TrajectoryFactory.ThreeBounces(kStart);

            var changes1 = new List<BallStateChange>();
            var sm1 = MakeSM();
            sm1.OnStateChanged += c => changes1.Add(c);
            sm1.OnTrajectoryComputed(kStart, traj, kBallR);
            sm1.Tick(true); sm1.Tick(false);

            var changes2 = new List<BallStateChange>();
            var sm2 = MakeSM();
            sm2.OnStateChanged += c => changes2.Add(c);
            sm2.OnTrajectoryComputed(kStart, traj, kBallR);
            sm2.Tick(true); sm2.Tick(false);

            Assert.AreEqual(changes1.Count, changes2.Count, "Event count must match");
            for (int i = 0; i < changes1.Count; i++)
            {
                Assert.AreEqual(changes1[i].Previous, changes2[i].Previous, $"[{i}] Previous mismatch");
                Assert.AreEqual(changes1[i].Next,     changes2[i].Next,     $"[{i}] Next mismatch");
                Assert.AreEqual(changes1[i].SimTime,  changes2[i].SimTime,  $"[{i}] SimTime mismatch");
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // 14. Illegal transition: Aiming → Rolling is structurally impossible
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        public void IllegalTransition_AimingToRolling_IsStructurallyImpossible()
        {
            // The SM has no public API that transitions Aiming → Rolling directly.
            // OnTrajectoryComputed always inserts Aiming→Flying first (the only
            // Aiming→* transition), so Aiming→Rolling cannot occur via any public path.
            //
            // We verify this structurally: after construction, State==Aiming.
            // Calling Tick() while Aiming does nothing (no pending transitions).
            var sm = MakeSM();
            Assert.AreEqual(BallState.Aiming, sm.State);
            sm.Tick(true);
            sm.Tick(false);
            Assert.AreEqual(BallState.Aiming, sm.State,
                "Aiming→Rolling is impossible: Tick on fresh SM leaves state as Aiming");
        }

        // ─────────────────────────────────────────────────────────────────────
        // 15. Null surface provider throws ArgumentNullException
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        public void NullSurfaceProvider_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new BallStateMachine(null));
        }

        // ─────────────────────────────────────────────────────────────────────
        // 16. Null cup detector falls back to NullCupDetector
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        public void NullCupDetector_FallsBackToNullDetector()
        {
            // Constructing with null cup detector must succeed (no throw).
            var sm = new BallStateMachine(new FakeSurface(), null);
            sm.Headless = true;

            ShotResult? result = null;
            sm.OnShotComplete += r => result = r;

            // A trajectory that would be in-cup if a real detector existed
            // (sample at the cup position) should resolve as AtRest with null detector.
            fp3 fakeCupPos = TrajectoryFactory.At(15f, 0f, 5f);
            var traj = TrajectoryFactory.WithCupSample(kStart, fakeCupPos);
            // NullCupDetector always returns false, so IsInCup never fires → AtRest.
            sm.OnTrajectoryComputed(kStart, traj, kBallR);

            Assert.IsTrue(result.HasValue);
            Assert.AreEqual(BallState.AtRest, result.Value.TerminalState,
                "NullCupDetector should cause trajectory to resolve as AtRest, not InCup");
        }
    }
}
