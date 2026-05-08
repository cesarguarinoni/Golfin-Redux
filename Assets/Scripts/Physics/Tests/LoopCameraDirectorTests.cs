using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Golfin.Physics;
using Golfin.Physics.Math;
using Golfin.Physics.Runtime;
using Golfin.Physics.Viewer;
using Golfin.Gameplay.Loop;

namespace Golfin.Physics.Tests
{
    // ── Test stubs ──────────────────────────────────────────────────────────────

    /// <summary>Records all IModeSetter calls for assertion in tests.</summary>
    sealed class RecordingModeSetter : IModeSetter
    {
        public readonly List<ChaseCamera.Mode> SetModeCalls      = new List<ChaseCamera.Mode>();
        public readonly List<Transform>        SetTargetCalls     = new List<Transform>();
        public int                             ResetToOriginCount;
        public Vector3?                        LastDownrangePos;
        public Vector3?                        LastDownrangeLookAt;
        public Vector3?                        LastCupZoomFocus;
        public Vector3?                        LastOBFreezePivot;

        ChaseCamera.Mode _currentMode = ChaseCamera.Mode.Chase;
        public ChaseCamera.Mode CurrentMode => _currentMode;

        public void SetMode(ChaseCamera.Mode mode) { _currentMode = mode; SetModeCalls.Add(mode); }
        public void SetTarget(Transform t)         { SetTargetCalls.Add(t); }
        public void ResetToOrigin(Vector3 o, Vector3 l) { ResetToOriginCount++; }
        public void SetDownrangeFraming(Vector3 pos, Vector3 lookAt) { LastDownrangePos = pos; LastDownrangeLookAt = lookAt; }
        public void SetCupZoomFocus(Vector3 f)     { LastCupZoomFocus = f; }
        public void SetOBFreezePivot(Vector3 p)    { LastOBFreezePivot = p; }
    }

    /// <summary>Minimal stub for IControllerAccessor. Mutate fields between test phases.</summary>
    sealed class StubControllerAccessor : IControllerAccessor
    {
        public BallStateMachine  BallSM           { get; set; }
        public Trajectory        LastTrajectory   { get; set; }
        public Vector3           LastShotOrigin   { get; set; } = Vector3.zero;
        public Vector3           LastShotLaunchDir { get; set; } = Vector3.forward;
        public Transform         CurrentBall      { get; set; }
        public bool              CurrentShotIsPutt { get; set; }
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────

    static class TrajectoryBuilder
    {
        public static Trajectory Simple(fp3 finalPos, List<TerrainHit> hits = null)
        {
            var samples = new List<TrajectorySample>
            {
                new TrajectorySample(fp.Zero, fp3.Zero, fp3.Zero),
                new TrajectorySample(fp.One,  finalPos, fp3.Zero),
            };
            return new Trajectory(
                samples, finalPos,
                fp3.Zero, fp.One,
                TerminationReason.BallStopped,
                hits ?? new List<TerrainHit>());
        }

        public static TerrainHit NonStopHit(fp3 pos, SurfaceType surf = SurfaceType.Fairway)
            => new TerrainHit(fp.One, pos, fp3.Zero, fp3.Zero, surf, isStop: false);

        public static TerrainHit StopHit(fp3 pos, SurfaceType surf = SurfaceType.Fairway)
            => new TerrainHit(fp.One, pos, fp3.Zero, fp3.Zero, surf, isStop: true);
    }

    // ── Director wire-up helper ──────────────────────────────────────────────────

    static class DirectorFactory
    {
        /// <summary>
        /// Creates a LoopCameraDirector GameObject, injects stubs, and subscribes the SM.
        /// The SM is set to Headless so transitions fire synchronously.
        /// </summary>
        public static (LoopCameraDirector director, RecordingModeSetter setter, StubControllerAccessor ctrl)
            Create(bool isPutt = false, Trajectory lastTraj = null)
        {
            var go       = new GameObject("DirectorTest");
            var director = go.AddComponent<LoopCameraDirector>();

            var setter   = new RecordingModeSetter();
            director.SetModeSetter(setter);

            var sm = new BallStateMachine(new ConstantSurfaceProvider(SurfaceType.Fairway));
            sm.Headless = true;

            var ctrl = new StubControllerAccessor
            {
                BallSM            = sm,
                CurrentShotIsPutt = isPutt,
                LastTrajectory    = lastTraj,
                LastShotLaunchDir = Vector3.forward,
                LastShotOrigin    = Vector3.zero,
            };

            director.SetControllerAccessor(ctrl);

            return (director, setter, ctrl);
        }
    }

    // ── Tests ───────────────────────────────────────────────────────────────────

    public class LoopCameraDirectorTests
    {
        // ── Test 1 ─────────────────────────────────────────────────────────────

        [Test]
        public void Director_OnFlyingEntry_NonPutt_SetsChaseMode()
        {
            var (director, setter, ctrl) = DirectorFactory.Create(isPutt: false);

            // Fire a trajectory to trigger Aiming→Flying synchronously (headless SM).
            ctrl.BallSM.OnTrajectoryComputed(
                fp3.Zero,
                TrajectoryBuilder.Simple(new fp3(fp.FromFloat(50f), fp.Zero, fp.Zero)),
                fp.FromFloat(0.02f));

            // SM in headless mode drains all transitions immediately.
            // After drain: Aiming→Flying is the first transition.
            Assert.IsTrue(setter.SetModeCalls.Contains(ChaseCamera.Mode.Chase),
                $"Expected Chase in SetMode calls. Got: [{string.Join(", ", setter.SetModeCalls)}]");
            Assert.Greater(setter.ResetToOriginCount, 0,
                "Expected ResetToOrigin to be called on Aiming→Flying");
        }

        // ── Test 2 ─────────────────────────────────────────────────────────────

        [Test]
        public void Director_OnFlyingEntry_Putt_SkipsModeChange()
        {
            var (director, setter, ctrl) = DirectorFactory.Create(isPutt: true);

            ctrl.BallSM.OnTrajectoryComputed(
                fp3.Zero,
                TrajectoryBuilder.Simple(new fp3(fp.FromFloat(5f), fp.Zero, fp.Zero)),
                fp.FromFloat(0.02f));

            // Putt: Flying / Rolling / AtRest should all be skipped.
            Assert.IsFalse(setter.SetModeCalls.Contains(ChaseCamera.Mode.Chase),
                $"Putt should skip Chase mode dispatch. Got: [{string.Join(", ", setter.SetModeCalls)}]");
        }

        // ── Test 3 ─────────────────────────────────────────────────────────────

        [Test]
        public void Director_OnInCup_SetsCupZoomMode_AndSetsCupZoomFocus()
        {
            var (director, setter, ctrl) = DirectorFactory.Create(isPutt: false);

            // Trajectory that the SM will classify as InCup — use a cup detector that
            // always returns true to force InCup terminal state.
            var sm = new BallStateMachine(
                new ConstantSurfaceProvider(SurfaceType.Green),
                new AlwaysInCupDetector());
            sm.Headless = true;
            ctrl.BallSM = sm;
            director.SetControllerAccessor(ctrl);

            var inCupPos = new fp3(fp.FromFloat(10f), fp.Zero, fp.FromFloat(5f));
            var traj = TrajectoryBuilder.Simple(inCupPos);
            sm.OnTrajectoryComputed(fp3.Zero, traj, fp.FromFloat(0.02f));

            Assert.IsTrue(setter.SetModeCalls.Contains(ChaseCamera.Mode.CupZoom),
                $"Expected CupZoom. Got: [{string.Join(", ", setter.SetModeCalls)}]");
            Assert.IsTrue(setter.LastCupZoomFocus.HasValue,
                "SetCupZoomFocus should have been called");
        }

        // ── Test 4 ─────────────────────────────────────────────────────────────

        [Test]
        public void Director_OnOB_FreezesAtFirstWaterHitXZ()
        {
            float obHeight = 5f; // default obFreezeHeightAboveTerrain

            var hits = new List<TerrainHit>
            {
                TrajectoryBuilder.NonStopHit(new fp3(fp.FromFloat(10f), fp.Zero, fp.Zero), SurfaceType.Fairway),
                TrajectoryBuilder.NonStopHit(new fp3(fp.FromFloat(25f), fp.Zero, fp.FromFloat(5f)), SurfaceType.Water),
            };

            // Trajectory terminating in water.
            var samples = new List<TrajectorySample>
            {
                new TrajectorySample(fp.Zero,  fp3.Zero, fp3.Zero),
                new TrajectorySample(fp.One,   new fp3(fp.FromFloat(25f), fp.Zero, fp.FromFloat(5f)), fp3.Zero),
            };
            var finalPos = new fp3(fp.FromFloat(25f), fp.Zero, fp.FromFloat(5f));
            var traj = new Trajectory(samples, finalPos, fp3.Zero, fp.One, TerminationReason.HitWater, hits);

            var (director, setter, ctrl) = DirectorFactory.Create(isPutt: false, lastTraj: traj);
            ctrl.LastTrajectory = traj;

            ctrl.BallSM.OnTrajectoryComputed(fp3.Zero, traj, fp.FromFloat(0.02f));

            Assert.IsTrue(setter.SetModeCalls.Contains(ChaseCamera.Mode.OBFreeze),
                $"Expected OBFreeze. Got: [{string.Join(", ", setter.SetModeCalls)}]");
            Assert.IsTrue(setter.LastOBFreezePivot.HasValue,
                "SetOBFreezePivot should have been called");

            Vector3 pivot = setter.LastOBFreezePivot.Value;
            Assert.AreEqual(25f, pivot.x, 0.01f, "Pivot X should match first Water hit");
            Assert.AreEqual(obHeight, pivot.y, 0.01f, "Pivot Y should be obFreezeHeight above terrain (terrain y=0)");
            Assert.AreEqual(5f,  pivot.z, 0.01f, "Pivot Z should match first Water hit");
        }

        // ── Test 5 ─────────────────────────────────────────────────────────────

        [Test]
        public void Director_OnOB_NoWaterHit_FallsBackToChangePosition()
        {
            float obHeight = 5f;

            // No water/OOB hits — ExitedWorldBounds.
            var finalPos = new fp3(fp.FromFloat(500f), fp.FromFloat(2f), fp.Zero);
            var samples  = new List<TrajectorySample>
            {
                new TrajectorySample(fp.Zero, fp3.Zero, fp3.Zero),
                new TrajectorySample(fp.One,  finalPos, fp3.Zero),
            };
            var traj = new Trajectory(samples, finalPos, fp3.Zero, fp.One,
                TerminationReason.ExitedWorldBounds, new List<TerrainHit>());

            var (director, setter, ctrl) = DirectorFactory.Create(isPutt: false, lastTraj: traj);
            ctrl.LastTrajectory = traj;

            ctrl.BallSM.OnTrajectoryComputed(fp3.Zero, traj, fp.FromFloat(0.02f));

            Assert.IsTrue(setter.LastOBFreezePivot.HasValue,
                "SetOBFreezePivot should have been called");
            Vector3 pivot = setter.LastOBFreezePivot.Value;
            // Should fall back to the final position (=OB transition position) + height offset.
            // BallStateMachine uses finalPosition as the OB change position for ExitedWorldBounds.
            Assert.AreEqual(500f, pivot.x, 1f,         "Pivot X should fall back to final position");
            Assert.AreEqual(2f + obHeight, pivot.y, 1f, "Pivot Y = terrain Y + obFreezeHeight");
        }

        // ── Test 6 ─────────────────────────────────────────────────────────────

        [Test]
        public void Director_OnAtRest_ChaseModePersists_TargetNotClearedByTerminalHandler()
        {
            // § controls_h R3: AtRest should NOT clear the chase target via the terminal-state
            // handler — the camera should stay in Chase mode tracking the stationary ball.
            // InCup / OB still clear the target (special framing there).
            var (director, setter, ctrl) = DirectorFactory.Create(isPutt: false);

            // Assign a real Transform as CurrentBall so ArmChaseForShot sets a non-null target.
            var ballGO = new GameObject("TestBall_R3");
            ctrl.CurrentBall = ballGO.transform;

            var traj = TrajectoryBuilder.Simple(new fp3(fp.FromFloat(50f), fp.Zero, fp.Zero));
            ctrl.BallSM.OnTrajectoryComputed(fp3.Zero, traj, fp.FromFloat(0.02f));

            // After Aiming→Flying→AtRest drains in headless mode:
            // 1. ArmChaseForShot set the target to the ball Transform.
            // 2. AtRest should NOT clear it (§controls_h R3 fix).
            // The LAST SetTarget call should be the ball (not null).
            Assert.IsNotNull(setter.SetTargetCalls[setter.SetTargetCalls.Count - 1],
                "Last SetTarget call should be the ball Transform, not null — AtRest should NOT clear the target (§controls_h R3)");

            // Mode should remain Chase (not switched to Idle or Static).
            Assert.AreEqual(ChaseCamera.Mode.Chase, setter.CurrentMode,
                "Mode should remain Chase after AtRest (§controls_h R3 — camera tracks stationary ball at rest)");

            UnityEngine.Object.DestroyImmediate(ballGO);
        }

        // ── Test 7 ─────────────────────────────────────────────────────────────

        [Test]
        public void Director_CinematicCut_FiresAt65PercentCarry()
        {
            // Trajectory with first non-stop hit at X=100 (carry=100m).
            var landingPos = new fp3(fp.FromFloat(100f), fp.Zero, fp.Zero);
            var hits = new List<TerrainHit>
            {
                TrajectoryBuilder.NonStopHit(landingPos),
                TrajectoryBuilder.StopHit(new fp3(fp.FromFloat(120f), fp.Zero, fp.Zero)),
            };
            var traj = new Trajectory(
                new List<TrajectorySample>
                {
                    new TrajectorySample(fp.Zero, fp3.Zero, fp3.Zero),
                    new TrajectorySample(fp.One, new fp3(fp.FromFloat(120f), fp.Zero, fp.Zero), fp3.Zero),
                },
                new fp3(fp.FromFloat(120f), fp.Zero, fp.Zero),
                fp3.Zero, fp.One, TerminationReason.BallStopped, hits);

            var (director, setter, ctrl) = DirectorFactory.Create(isPutt: false, lastTraj: traj);
            ctrl.LastTrajectory    = traj;
            ctrl.LastShotOrigin    = Vector3.zero;
            ctrl.LastShotLaunchDir = Vector3.forward; // +Z forward

            // Put the SM into Flying state directly (headless drain puts it past Flying in full run).
            // For this test we need SM.State == Flying so Update runs.
            // Use a separate SM that we drive to Flying manually.
            var sm2 = new BallStateMachine(new ConstantSurfaceProvider(SurfaceType.Fairway));
            sm2.Headless = false; // live mode — SM stays Flying after OnTrajectoryComputed until Tick
            ctrl.BallSM = sm2;
            director.SetControllerAccessor(ctrl);

            // OnTrajectoryComputed fires Aiming→Flying synchronously in live mode (first transition only).
            sm2.OnTrajectoryComputed(fp3.Zero, traj, fp.FromFloat(0.02f));
            // SM state should be Flying now (first transition was Aiming→Flying).
            Assert.AreEqual(BallState.Flying, sm2.State, "SM should be in Flying state");

            // Simulate ball at 70% of carry (70m on Z axis since launchDir=forward).
            var ballGO = new GameObject("Ball");
            ballGO.transform.position = new Vector3(0f, 5f, 70f); // 70m along Z (forward)
            ctrl.CurrentBall = ballGO.transform;
            ctrl.CurrentShotIsPutt = false;

            // Reset setter to see only the tick's side effects.
            setter.SetModeCalls.Clear();

            // Call TickCinematicCut directly (avoids MonoBehaviour ShouldRunBehaviour assertion
            // that fires when SendMessage("Update") is used in EditMode tests).
            director.TickCinematicCut();

            Assert.IsTrue(setter.SetModeCalls.Contains(ChaseCamera.Mode.Downrange),
                $"Expected Downrange at 70% carry. SetMode calls: [{string.Join(", ", setter.SetModeCalls)}]");
            Assert.IsTrue(setter.LastDownrangePos.HasValue,
                "SetDownrangeFraming should have been called");

            Object.DestroyImmediate(ballGO);
            Object.DestroyImmediate(director.gameObject);
        }

        // ── Test 8 ─────────────────────────────────────────────────────────────

        [Test]
        public void Director_CinematicCut_DoesNotFireOnPutt()
        {
            var landingPos = new fp3(fp.FromFloat(30f), fp.Zero, fp.Zero);
            var hits = new List<TerrainHit> { TrajectoryBuilder.NonStopHit(landingPos) };
            var traj = new Trajectory(
                new List<TrajectorySample>
                {
                    new TrajectorySample(fp.Zero, fp3.Zero, fp3.Zero),
                    new TrajectorySample(fp.One, landingPos, fp3.Zero),
                },
                landingPos, fp3.Zero, fp.One, TerminationReason.BallStopped, hits);

            // isPutt = true
            var (director, setter, ctrl) = DirectorFactory.Create(isPutt: true, lastTraj: traj);
            ctrl.LastTrajectory    = traj;
            ctrl.LastShotLaunchDir = Vector3.forward;
            ctrl.LastShotOrigin    = Vector3.zero;

            var sm2 = new BallStateMachine(new ConstantSurfaceProvider(SurfaceType.Fairway));
            sm2.Headless = false;
            ctrl.BallSM = sm2;
            ctrl.CurrentShotIsPutt = true;
            director.SetControllerAccessor(ctrl);

            sm2.OnTrajectoryComputed(fp3.Zero, traj, fp.FromFloat(0.02f));

            var ballGO = new GameObject("BallPutt");
            ballGO.transform.position = new Vector3(0f, 0f, 25f); // 83% carry
            ctrl.CurrentBall = ballGO.transform;

            setter.SetModeCalls.Clear();
            director.TickCinematicCut();

            Assert.IsFalse(setter.SetModeCalls.Contains(ChaseCamera.Mode.Downrange),
                "Putt should never trigger Downrange cut");

            Object.DestroyImmediate(ballGO);
            Object.DestroyImmediate(director.gameObject);
        }

        // ── Test 10 ─────────────────────────────────────────────────────────────

        /// <summary>
        /// §controls_g_smoke_followup — validates the new OnModeChanged observable event.
        /// When a state transition triggers a mode dispatch, OnModeChanged fires exactly once
        /// with the newly-applied mode value.
        /// </summary>
        [Test]
        public void Director_OnModeChange_RaisesEventWithNewMode()
        {
            // Arrange: standard director setup (mirrors existing tests' factory pattern).
            var (director, setter, ctrl) = DirectorFactory.Create(isPutt: false);

            var modeHistory = new System.Collections.Generic.List<ChaseCamera.Mode>();
            director.OnModeChanged += (mode) => modeHistory.Add(mode);

            // Act: drive a state change that triggers a mode dispatch (Aiming → Flying → AtRest).
            // Use headless mode so all transitions drain synchronously.
            var traj = TrajectoryBuilder.Simple(new fp3(fp.FromFloat(50f), fp.Zero, fp.Zero));
            ctrl.BallSM.OnTrajectoryComputed(fp3.Zero, traj, fp.FromFloat(0.02f));

            // Assert: event fired at least once (Flying → Chase is the first mode-change event).
            Assert.That(modeHistory.Count, Is.GreaterThanOrEqualTo(1),
                "OnModeChanged should fire at least once for a full Aiming→Flying→AtRest sequence.");
            Assert.That(modeHistory[0], Is.EqualTo(ChaseCamera.Mode.Chase),
                "First OnModeChanged event should report ChaseCamera.Mode.Chase (Aiming→Flying dispatch).");
        }

        // ── Test 9 ─────────────────────────────────────────────────────────────

        [Test]
        public void Director_CinematicCut_DoesNotFireBelowMinCarry()
        {
            // Carry = 20m < minCarryForCinematicMeters (30m).
            var landingPos = new fp3(fp.FromFloat(20f), fp.Zero, fp.Zero);
            var hits = new List<TerrainHit> { TrajectoryBuilder.NonStopHit(landingPos) };
            var traj = new Trajectory(
                new List<TrajectorySample>
                {
                    new TrajectorySample(fp.Zero, fp3.Zero, fp3.Zero),
                    new TrajectorySample(fp.One, landingPos, fp3.Zero),
                },
                landingPos, fp3.Zero, fp.One, TerminationReason.BallStopped, hits);

            var (director, setter, ctrl) = DirectorFactory.Create(isPutt: false, lastTraj: traj);
            ctrl.LastTrajectory    = traj;
            ctrl.LastShotLaunchDir = Vector3.forward;
            ctrl.LastShotOrigin    = Vector3.zero;

            var sm2 = new BallStateMachine(new ConstantSurfaceProvider(SurfaceType.Fairway));
            sm2.Headless = false;
            ctrl.BallSM = sm2;
            director.SetControllerAccessor(ctrl);

            sm2.OnTrajectoryComputed(fp3.Zero, traj, fp.FromFloat(0.02f));

            // Ball at 80% of carry (16m) — would be above 65% threshold but carry < 30m.
            var ballGO = new GameObject("BallShort");
            ballGO.transform.position = new Vector3(0f, 0f, 16f);
            ctrl.CurrentBall = ballGO.transform;
            ctrl.CurrentShotIsPutt = false;

            setter.SetModeCalls.Clear();
            director.TickCinematicCut();

            Assert.IsFalse(setter.SetModeCalls.Contains(ChaseCamera.Mode.Downrange),
                "Short carry (<30m) should not trigger cinematic cut");

            Object.DestroyImmediate(ballGO);
            Object.DestroyImmediate(director.gameObject);
        }

        // ── Test 11 — §controls_h R3 ───────────────────────────────────────────

        [Test]
        public void Director_ChaseModePersistsThroughFlying_Rolling_AtRest()
        {
            // § controls_h R3: Chase mode must remain active through Flying → Rolling → AtRest.
            // The camera should never switch to a non-Chase mode (Static/Idle/null) during this
            // sequence — only InCup/OB trigger non-Chase framing.
            var (director, setter, ctrl) = DirectorFactory.Create(isPutt: false);

            var ballGO = new GameObject("TestBall_ChaseR3");
            ctrl.CurrentBall = ballGO.transform;

            // Trajectory with a terrain hit (bounce) to generate Rolling transitions.
            var landingPos = new fp3(fp.FromFloat(80f), fp.Zero, fp.Zero);
            var restPos    = new fp3(fp.FromFloat(120f), fp.Zero, fp.Zero);
            var hits = new List<TerrainHit>
            {
                TrajectoryBuilder.NonStopHit(landingPos),
                TrajectoryBuilder.StopHit(restPos),
            };
            var traj = new Trajectory(
                new List<TrajectorySample>
                {
                    new TrajectorySample(fp.Zero, fp3.Zero, fp3.Zero),
                    new TrajectorySample(fp.One, restPos, fp3.Zero),
                },
                restPos, fp3.Zero, fp.One, TerminationReason.BallStopped, hits);

            ctrl.BallSM.OnTrajectoryComputed(fp3.Zero, traj, fp.FromFloat(0.02f));

            // After headless drain: Aiming→Flying, Flying→Rolling, Rolling→AtRest.
            // Mode should be Chase throughout (never Idle or null).
            Assert.AreEqual(ChaseCamera.Mode.Chase, setter.CurrentMode,
                "Mode must be Chase after Rolling→AtRest sequence (§controls_h R3)");

            // Mode history must not contain any non-Chase modes set by state transitions
            // (Downrange is only set by TickCinematicCut which we didn't call).
            var nonChaseModes = new[] { ChaseCamera.Mode.Overhead, ChaseCamera.Mode.GroundLevel };
            foreach (var m in nonChaseModes)
                Assert.IsFalse(setter.SetModeCalls.Contains(m),
                    $"Mode {m} should not have been set during Flying→Rolling→AtRest sequence");

            // Target should NOT be null after AtRest (R3 fix: target kept for Chase-at-rest).
            Assert.IsNotNull(setter.SetTargetCalls[setter.SetTargetCalls.Count - 1],
                "Target must not be null after AtRest — camera should track stationary ball (§controls_h R3)");

            UnityEngine.Object.DestroyImmediate(ballGO);
        }

        // ── Test 12 — §controls_h R4 ───────────────────────────────────────────

        [Test]
        public void Director_DownrangeCut_Fires_WhenProgressExceedsThreshold()
        {
            // § controls_h R2: Downrange cinematic cut must fire when progress >= 65% of carry.
            // This verifies the TickCinematicCut logic fires correctly.
            var landingPos = new fp3(fp.FromFloat(100f), fp.Zero, fp.Zero);
            var hits = new List<TerrainHit>
            {
                TrajectoryBuilder.NonStopHit(landingPos),
                TrajectoryBuilder.StopHit(new fp3(fp.FromFloat(120f), fp.Zero, fp.Zero)),
            };
            var traj = new Trajectory(
                new List<TrajectorySample>
                {
                    new TrajectorySample(fp.Zero, fp3.Zero, fp3.Zero),
                    new TrajectorySample(fp.One, new fp3(fp.FromFloat(120f), fp.Zero, fp.Zero), fp3.Zero),
                },
                new fp3(fp.FromFloat(120f), fp.Zero, fp.Zero),
                fp3.Zero, fp.One, TerminationReason.BallStopped, hits);

            var (director, setter, ctrl) = DirectorFactory.Create(isPutt: false, lastTraj: traj);

            // Non-headless SM so we stay in Flying state after OnTrajectoryComputed.
            var sm2 = new BallStateMachine(new ConstantSurfaceProvider(SurfaceType.Fairway));
            sm2.Headless = false;
            ctrl.BallSM  = sm2;
            ctrl.LastTrajectory    = traj;
            ctrl.LastShotLaunchDir = Vector3.right;   // +X direction
            ctrl.LastShotOrigin    = Vector3.zero;
            director.SetControllerAccessor(ctrl);

            sm2.OnTrajectoryComputed(fp3.Zero, traj, fp.FromFloat(0.02f));
            // SM is now in Flying state (only Aiming→Flying fires synchronously in non-headless).

            // Ball at 70m along +X — that's 70% of 100m carry. Above 65% threshold.
            var ballGO = new GameObject("BallR2");
            ballGO.transform.position = new Vector3(70f, 0f, 0f);
            ctrl.CurrentBall = ballGO.transform;

            setter.SetModeCalls.Clear();
            director.TickCinematicCut();

            Assert.IsTrue(setter.SetModeCalls.Contains(ChaseCamera.Mode.Downrange),
                "Downrange cut must fire at 70% carry (above 65% threshold) — §controls_h R2 regression check");

            UnityEngine.Object.DestroyImmediate(ballGO);
            UnityEngine.Object.DestroyImmediate(director.gameObject);
        }

        // ── Test 13 — §controls_h R3-revised ──────────────────────────────────────

        [Test]
        public void Director_DownrangeReleased_WhenBallPassesTouchdown()
        {
            // § controls_h R3-revised: after the Downrange cinematic fires, if the ball's XZ
            // progress exceeds the predicted carry (touchdown), TickCinematicCut must release
            // Downrange back to Chase with the live ball as target.
            // This happens DURING BallState.Flying (while the animator plays the visual roll),
            // before the SM's Flying→Rolling transition fires (which only fires when animator stops).
            var landingPos = new fp3(fp.FromFloat(100f), fp.Zero, fp.Zero);
            var hits = new List<TerrainHit>
            {
                TrajectoryBuilder.NonStopHit(landingPos),
                TrajectoryBuilder.StopHit(new fp3(fp.FromFloat(130f), fp.Zero, fp.Zero)),
            };
            var traj = new Trajectory(
                new List<TrajectorySample>
                {
                    new TrajectorySample(fp.Zero, fp3.Zero, fp3.Zero),
                    new TrajectorySample(fp.One, new fp3(fp.FromFloat(130f), fp.Zero, fp.Zero), fp3.Zero),
                },
                new fp3(fp.FromFloat(130f), fp.Zero, fp.Zero),
                fp3.Zero, fp.One, TerminationReason.BallStopped, hits);

            var (director, setter, ctrl) = DirectorFactory.Create(isPutt: false, lastTraj: traj);

            // Non-headless SM — stays in Flying after OnTrajectoryComputed
            var sm2 = new BallStateMachine(new ConstantSurfaceProvider(SurfaceType.Fairway));
            sm2.Headless = false;
            ctrl.BallSM = sm2;
            ctrl.LastTrajectory    = traj;
            ctrl.LastShotLaunchDir = Vector3.right; // +X direction
            ctrl.LastShotOrigin    = Vector3.zero;
            director.SetControllerAccessor(ctrl);

            sm2.OnTrajectoryComputed(fp3.Zero, traj, fp.FromFloat(0.02f));
            Assert.AreEqual(BallState.Flying, sm2.State, "SM should be Flying");

            // Ball at 70% carry — fire the Downrange cinematic cut
            var ballGO = new GameObject("BallTouchdown");
            ballGO.transform.position = new Vector3(70f, 5f, 0f); // 70% of 100m carry
            ctrl.CurrentBall = ballGO.transform;
            setter.SetModeCalls.Clear();
            director.TickCinematicCut();
            Assert.IsTrue(setter.SetModeCalls.Contains(ChaseCamera.Mode.Downrange),
                "Downrange cut should fire at 70% carry");

            // Now advance ball PAST the predicted carry (touchdown)
            ballGO.transform.position = new Vector3(105f, 0f, 0f); // past 100m carry
            setter.SetModeCalls.Clear();
            setter.SetTargetCalls.Clear();
            director.TickCinematicCut();

            // Assert: Downrange released → Chase re-applied, target set to live ball
            Assert.IsTrue(setter.SetModeCalls.Contains(ChaseCamera.Mode.Chase),
                $"Downrange should release to Chase at touchdown (ball past carry). SetMode calls: [{string.Join(", ", setter.SetModeCalls)}]");
            Assert.IsTrue(setter.SetTargetCalls.Count > 0,
                "SetTarget should be called on touchdown release with the live ball");
            Assert.AreEqual(ballGO.transform, setter.SetTargetCalls[setter.SetTargetCalls.Count - 1],
                "Target should be the live ball Transform on touchdown release");

            Object.DestroyImmediate(ballGO);
            Object.DestroyImmediate(director.gameObject);
        }

        // ── Teardown ────────────────────────────────────────────────────────────

        [TearDown]
        public void TearDown()
        {
            // Clean up any lingering test GOs.
            foreach (var go in Object.FindObjectsOfType<LoopCameraDirector>())
                Object.DestroyImmediate(go.gameObject);
        }
    }

    // ── Cup detector stub ───────────────────────────────────────────────────────

    sealed class AlwaysInCupDetector : ICupDetector
    {
        public bool IsInCup(fp3 pos, fp radius) => true;
    }
}
