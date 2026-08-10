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

        // ob_boundary_presentation: capture SetChaseClamp calls for test assertions.
        public Vector3? LastChaseClampPoint;
        public bool?    LastChaseClampActive;

        ChaseCamera.Mode _currentMode = ChaseCamera.Mode.Chase;
        public ChaseCamera.Mode CurrentMode => _currentMode;

        public void SetMode(ChaseCamera.Mode mode) { _currentMode = mode; SetModeCalls.Add(mode); }
        public void SetTarget(Transform t)         { SetTargetCalls.Add(t); }
        public void ResetToOrigin(Vector3 o, Vector3 l) { ResetToOriginCount++; }
        public void SetDownrangeFraming(Vector3 pos, Vector3 lookAt) { LastDownrangePos = pos; LastDownrangeLookAt = lookAt; }
        public void SetCupZoomFocus(Vector3 f)     { LastCupZoomFocus = f; }
        public void SetOBFreezePivot(Vector3 p)    { LastOBFreezePivot = p; }
        public void SetChaseClamp(Vector3 clampPoint, bool active)
        {
            LastChaseClampPoint  = clampPoint;
            LastChaseClampActive = active;
        }
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
        public ISurfaceProvider  SurfaceProvider  { get; set; }
    }

    /// <summary>
    /// K10 follow-up: classifies everything beyond a boundary X as OOB, everything before it as
    /// Fairway — a stand-in for the baked OB line so playable-area-exit tests stay deterministic.
    /// </summary>
    sealed class BoundaryAtXProvider : ISurfaceProvider
    {
        readonly float _boundaryX;
        public BoundaryAtXProvider(float boundaryX) { _boundaryX = boundaryX; }
        public SurfaceType Classify(fp worldX, fp worldZ)
            => worldX.ToFloat() >= _boundaryX ? SurfaceType.OOB : SurfaceType.Fairway;
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
        public void Director_OnFlyingEntry_Putt_DispatchesChaseMode()
        {
            var (director, setter, ctrl) = DirectorFactory.Create(isPutt: true);

            ctrl.BallSM.OnTrajectoryComputed(
                fp3.Zero,
                TrajectoryBuilder.Simple(new fp3(fp.FromFloat(5f), fp.Zero, fp.Zero)),
                fp.FromFloat(0.02f));

            // Post-§2f-revert (2026-05-14): putts dispatch Mode.Chase identically to iron.
            // Pre-revert, the Director early-returned for putts on Flying/Rolling/AtRest to
            // preserve a putter-specific GroundLevel framing; that divergence is deleted
            // and putts now share the iron camera path. Regression guard.
            Assert.IsTrue(setter.SetModeCalls.Contains(ChaseCamera.Mode.Chase),
                $"Putt must dispatch Chase mode post-§2f-revert. Got: [{string.Join(", ", setter.SetModeCalls)}]");
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

        // ── Test 4 — K10 follow-up (Cesar ruling 2026-08-05): OB just stops chasing ──

        [Test]
        public void Director_OnOB_StopsChasing_NoPivotTeleport()
        {
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
            // Real ball transform so the Flying arm records a NON-null target — otherwise
            // ArmChaseForShot(…, null) makes "last call is null" true regardless of the
            // terminal branch under test.
            ctrl.CurrentBall = new GameObject("WaterOBBall").transform;

            ctrl.BallSM.OnTrajectoryComputed(fp3.Zero, traj, fp.FromFloat(0.02f));

            // OB no longer enters OBFreeze or teleports to a pivot — camera stops chasing:
            // mode stays/dispatches Chase, target cleared → LateUpdate early-return → frozen in place.
            Assert.IsFalse(setter.SetModeCalls.Contains(ChaseCamera.Mode.OBFreeze),
                $"OB must NOT dispatch OBFreeze (top-down pivot cut). Got: [{string.Join(", ", setter.SetModeCalls)}]");
            Assert.AreEqual(ChaseCamera.Mode.Chase, setter.CurrentMode,
                "Mode must be Chase after OB — dormant via null-target early-return.");
            Assert.IsFalse(setter.LastOBFreezePivot.HasValue,
                "SetOBFreezePivot must NOT be called on OB (no pivot teleport).");

            // water_entry_presentation (Cesar 2026-08-06): "The call I made yesterday was not
            // for the water." This trajectory terminates in WATER, so the hard freeze is
            // DEFERRED — the camera stops advancing (chase clamp) but stays live through the
            // splash and the ball sink, and only then is the target cleared. The clear happens
            // in a coroutine, so it must NOT have fired synchronously here.
            Assert.IsNotNull(setter.SetTargetCalls[setter.SetTargetCalls.Count - 1],
                "Water OB must NOT clear the target synchronously — the camera stays live " +
                "until the splash has played, then freezes.");
        }

        // ── Test 4b — non-water OB still freezes on contact (K10 ruling intact) ──

        [Test]
        public void Director_OnBoundaryOB_ClearsTargetImmediately()
        {
            var hits = new List<TerrainHit>
            {
                TrajectoryBuilder.NonStopHit(new fp3(fp.FromFloat(10f), fp.Zero, fp.Zero), SurfaceType.Fairway),
                TrajectoryBuilder.NonStopHit(new fp3(fp.FromFloat(25f), fp.Zero, fp.FromFloat(5f)), SurfaceType.OOB),
            };

            var samples = new List<TrajectorySample>
            {
                new TrajectorySample(fp.Zero, fp3.Zero, fp3.Zero),
                new TrajectorySample(fp.One,  new fp3(fp.FromFloat(25f), fp.Zero, fp.FromFloat(5f)), fp3.Zero),
            };
            var finalPos = new fp3(fp.FromFloat(25f), fp.Zero, fp.FromFloat(5f));
            var traj = new Trajectory(samples, finalPos, fp3.Zero, fp.One, TerminationReason.HitOOB, hits);

            var (director, setter, ctrl) = DirectorFactory.Create(isPutt: false, lastTraj: traj);
            ctrl.LastTrajectory = traj;
            ctrl.CurrentBall = new GameObject("BoundaryOBBall").transform;

            ctrl.BallSM.OnTrajectoryComputed(fp3.Zero, traj, fp.FromFloat(0.02f));

            Assert.AreEqual(ChaseCamera.Mode.Chase, setter.CurrentMode,
                "Mode must be Chase after a boundary OB.");
            Assert.IsNull(setter.SetTargetCalls[setter.SetTargetCalls.Count - 1],
                "Boundary OB must still clear the target on contact — the water hold is " +
                "scoped to OBReason.Water only (K10 ruling unchanged).");
        }

        // ── Test 5 — K10 follow-up: the long-shot case that used to go top-down ──

        [Test]
        public void Director_OnOB_LongShot_NoMidpointAerialPivot()
        {
            // ExitedWorldBounds far from origin — the case whose old midpoint-25m pivot
            // produced the jarring top-down cut Cesar rejected. Now: no pivot, stop chasing.
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
            ctrl.LastShotOrigin = Vector3.zero; // explicit — matches DirectorFactory default

            ctrl.BallSM.OnTrajectoryComputed(fp3.Zero, traj, fp.FromFloat(0.02f));

            Assert.IsFalse(setter.LastOBFreezePivot.HasValue,
                "Long-shot OB must NOT set an aerial pivot (was the top-down defect).");
            Assert.AreEqual(ChaseCamera.Mode.Chase, setter.CurrentMode,
                "Mode must be Chase after long-shot OB.");
        }

        // ── Test 6 ─────────────────────────────────────────────────────────────

        [Test]
        public void Director_OnAtRest_ChaseMode_TargetClearedByTerminalHandler()
        {
            // §controls_h iter-8 fallback: AtRest DOES clear the chase target —
            // ApplyCameraYaw (the pre-§2b two-writer) takes over Aiming-camera position.
            // Mode is still Chase (ModeMap dispatches Chase on AtRest); only the target is cleared.
            var (director, setter, ctrl) = DirectorFactory.Create(isPutt: false);

            // Assign a real Transform as CurrentBall so ArmChaseForShot sets a non-null target.
            var ballGO = new GameObject("TestBall_iter8");
            ctrl.CurrentBall = ballGO.transform;

            var traj = TrajectoryBuilder.Simple(new fp3(fp.FromFloat(50f), fp.Zero, fp.Zero));
            ctrl.BallSM.OnTrajectoryComputed(fp3.Zero, traj, fp.FromFloat(0.02f));

            // After Aiming→Flying→AtRest drains in headless mode:
            // 1. ArmChaseForShot set the target to the ball Transform.
            // 2. AtRest clears it — the LAST SetTarget call should be null.
            Assert.IsNull(setter.SetTargetCalls[setter.SetTargetCalls.Count - 1],
                "Last SetTarget call should be null — AtRest CLEARS target (iter-8 fallback; ApplyCameraYaw owns Aiming position)");

            // Mode should still be Chase (ModeMap dispatches Chase on AtRest).
            Assert.AreEqual(ChaseCamera.Mode.Chase, setter.CurrentMode,
                "Mode should be Chase after AtRest (ModeMap dispatches Chase; target cleared but mode unchanged)");

            UnityEngine.Object.DestroyImmediate(ballGO);
        }

        // ── Test 7 — DELETED §controls_h iter-6 ───────────────────────────────
        // Director_CinematicCut_FiresAt65PercentCarry — deleted because cinematic cut
        // is removed in iter-6. TickCinematicCut is now a no-op; Downrange mode is gone.

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

            // Target IS null after AtRest (iter-8 fallback: ApplyCameraYaw owns position during Aiming).
            Assert.IsNull(setter.SetTargetCalls[setter.SetTargetCalls.Count - 1],
                "Target must be null after AtRest — ApplyCameraYaw owns Aiming-camera position (iter-8 fallback)");

            UnityEngine.Object.DestroyImmediate(ballGO);
        }

        // ── Test 12 — DELETED §controls_h iter-6 ──────────────────────────────
        // Director_DownrangeCut_Fires_WhenProgressExceedsThreshold — deleted because
        // cinematic cut (Downrange) is removed in iter-6. TickCinematicCut is a no-op.

        // ── Test 13 — DELETED §controls_h iter-6 ──────────────────────────────
        // Director_DownrangeReleased_WhenBallPassesTouchdown — deleted because the
        // release-at-touchdown logic is gone. The entire cinematic cut is deleted.

        // ── Test 14 — §controls_h iter-8 fallback ─────────────────────────────
        // Replaces the deleted iter-6 test (LateUpdateRunsWithNullTarget_UsesShotOriginAsFocus).
        // Pre-§2b early-return restored: LateUpdate does NOT modify transform when target is null
        // in Chase mode (ApplyCameraYaw owns position during Aiming instead).

        [Test]
        public void ChaseCamera_LateUpdate_EarlyReturnsWhenNullTargetInChaseMode()
        {
            // Verify A: with null target in Chase mode, LateUpdate does NOT modify transform.
            var go = new GameObject("ChaseCam");
            var cam = go.AddComponent<ChaseCamera>();
            cam.SetMode(ChaseCamera.Mode.Chase);
            cam.SetTarget(null);
            cam.ResetToOrigin(Vector3.zero, Vector3.right);

            Vector3 initialPos = new Vector3(123f, 456f, 789f);
            cam.transform.position = initialPos;

            for (int i = 0; i < 60; i++) cam.FrameCamera(1f / 60f);

            Assert.That(cam.transform.position, Is.EqualTo(initialPos),
                "LateUpdate should early-return on null target in Chase mode; transform should be unchanged.");

            Object.DestroyImmediate(go);
        }

        // ── Test 15 — DELETED §controls_h iter-8 fallback ────────────────────
        // ChaseCamera_SetAimDirection_UpdatesChasePose — SetAimDirection is deleted in iter-8.

        [Test]
        public void Director_NeverEntersDownrange_DuringFlying()
        {
            // Verify C1: cinematic cut is gone. No matter how long a shot flies, Director
            // does not promote Chase to Downrange.
            var (director, modeSetter, controllerStub) = DirectorFactory.Create();

            // Drive Aiming→Flying.
            controllerStub.BallSM.OnTrajectoryComputed(
                fp3.Zero,
                TrajectoryBuilder.Simple(new fp3(fp.FromFloat(200f), fp.Zero, fp.Zero)),
                fp.FromFloat(0.02f));

            // Simulate a long shot: 500 frames of cinematic-cut tick.
            modeSetter.SetModeCalls.Clear();
            for (int i = 0; i < 500; i++) director.TickCinematicCut();

            // Mode must NOT be Downrange (TickCinematicCut is now a no-op).
            Assert.That(modeSetter.SetModeCalls, Has.None.EqualTo(ChaseCamera.Mode.Downrange),
                "TickCinematicCut must never set Downrange — cinematic cut deleted in iter-6");
        }

        // ── Test 17 — §controls_h iter-8 fallback ─────────────────────────────
        // Replaces the deleted iter-3 R3 test (Director_AtRestKeepsTargetOnBall).
        // Director now CLEARS target on AtRest — ApplyCameraYaw owns Aiming-camera position.

        [Test]
        public void Director_AtRest_ClearsTarget()
        {
            // Verify H: Director clears _target on AtRest entry.
            var (director, modeSetter, controllerStub) = DirectorFactory.Create();
            var ballGO = new GameObject("Ball");
            controllerStub.CurrentBall = ballGO.transform;

            controllerStub.BallSM.OnTrajectoryComputed(
                fp3.Zero,
                TrajectoryBuilder.Simple(new fp3(fp.FromFloat(50f), fp.Zero, fp.Zero)),
                fp.FromFloat(0.02f));

            // After Aiming→Flying→AtRest drains in headless mode:
            // ArmChaseForShot set the target to the ball Transform, then AtRest clears it.
            Assert.That(modeSetter.SetTargetCalls[modeSetter.SetTargetCalls.Count - 1],
                Is.Null,
                "Target should be CLEARED on AtRest — ApplyCameraYaw owns Aiming-camera position.");

            Object.DestroyImmediate(ballGO);
        }

        // ── Tests 18–19: DELETED §controls_h iter-8 fallback ─────────────────
        // ChaseCamera_SetAiming_TrueUsesAimFraming — SetAiming is deleted in iter-8.
        // ChaseCamera_SetAiming_FalseUsesFollowFraming — SetAiming is deleted in iter-8.

        // ── Tests 20–24: ob_boundary_presentation (Order 1240) ────────────────

        // Test 20: Water hit → clamp armed at hit XZ.
        [Test]
        public void Director_OBClamp_WaterHit_ArmedAtHitXZ()
        {
            var waterHitPos = new fp3(fp.FromFloat(30f), fp.Zero, fp.FromFloat(7f));
            var hits = new List<TerrainHit>
            {
                TrajectoryBuilder.NonStopHit(waterHitPos, SurfaceType.Water),
            };
            var finalPos = new fp3(fp.FromFloat(50f), fp.Zero, fp.FromFloat(10f));
            var traj = new Trajectory(
                new List<TrajectorySample>
                {
                    new TrajectorySample(fp.Zero, fp3.Zero, fp3.Zero),
                    new TrajectorySample(fp.One,  finalPos, fp3.Zero),
                },
                finalPos, fp3.Zero, fp.One, TerminationReason.HitWater, hits);

            var (director, setter, ctrl) = DirectorFactory.Create(isPutt: false, lastTraj: traj);
            ctrl.LastTrajectory = traj;

            ctrl.BallSM.OnTrajectoryComputed(fp3.Zero, traj, fp.FromFloat(0.02f));

            Assert.IsTrue(setter.LastChaseClampActive.HasValue && setter.LastChaseClampActive.Value,
                "Clamp should be armed (active=true) when trajectory has a Water hit");
            Assert.IsTrue(setter.LastChaseClampPoint.HasValue,
                "SetChaseClamp should have been called");
            Assert.AreEqual(30f, setter.LastChaseClampPoint.Value.x, 0.01f,
                "Clamp point X should match Water hit");
            Assert.AreEqual(7f,  setter.LastChaseClampPoint.Value.z, 0.01f,
                "Clamp point Z should match Water hit");
        }

        // Test 21: OOB hit → clamp armed.
        [Test]
        public void Director_OBClamp_OOBHit_Armed()
        {
            var oobHitPos = new fp3(fp.FromFloat(40f), fp.Zero, fp.FromFloat(3f));
            var hits = new List<TerrainHit>
            {
                TrajectoryBuilder.NonStopHit(oobHitPos, SurfaceType.OOB),
            };
            var finalPos = oobHitPos;
            var traj = new Trajectory(
                new List<TrajectorySample>
                {
                    new TrajectorySample(fp.Zero, fp3.Zero, fp3.Zero),
                    new TrajectorySample(fp.One,  finalPos, fp3.Zero),
                },
                finalPos, fp3.Zero, fp.One, TerminationReason.BallStopped, hits);

            var (director, setter, ctrl) = DirectorFactory.Create(isPutt: false, lastTraj: traj);
            ctrl.LastTrajectory = traj;

            ctrl.BallSM.OnTrajectoryComputed(fp3.Zero, traj, fp.FromFloat(0.02f));

            Assert.IsTrue(setter.LastChaseClampActive.HasValue && setter.LastChaseClampActive.Value,
                "Clamp should be armed (active=true) when trajectory has an OOB hit");
        }

        // Test 22: No OB hit → clamp NOT armed (non-OB shots byte-identical to HEAD).
        [Test]
        public void Director_OBClamp_NoOBHit_NotArmed()
        {
            var fairwayHit = new fp3(fp.FromFloat(60f), fp.Zero, fp.Zero);
            var hits = new List<TerrainHit>
            {
                TrajectoryBuilder.NonStopHit(fairwayHit, SurfaceType.Fairway),
            };
            var traj = new Trajectory(
                new List<TrajectorySample>
                {
                    new TrajectorySample(fp.Zero, fp3.Zero, fp3.Zero),
                    new TrajectorySample(fp.One,  fairwayHit, fp3.Zero),
                },
                fairwayHit, fp3.Zero, fp.One, TerminationReason.BallStopped, hits);

            var (director, setter, ctrl) = DirectorFactory.Create(isPutt: false, lastTraj: traj);
            ctrl.LastTrajectory = traj;

            ctrl.BallSM.OnTrajectoryComputed(fp3.Zero, traj, fp.FromFloat(0.02f));

            // active=false → clamp is disarmed; non-OB camera path is unaffected.
            Assert.IsTrue(setter.LastChaseClampActive.HasValue,
                "SetChaseClamp must have been called (with active=false) even for non-OB shots");
            Assert.IsFalse(setter.LastChaseClampActive.Value,
                "Clamp must be DISARMED (active=false) for trajectories with no OB hit");
        }

        // Test 23: ExitedWorldBounds → clamp armed at finalPosition.
        [Test]
        public void Director_OBClamp_ExitedWorldBounds_ArmedAtFinalPosition()
        {
            var finalPos = new fp3(fp.FromFloat(500f), fp.FromFloat(2f), fp.Zero);
            var traj = new Trajectory(
                new List<TrajectorySample>
                {
                    new TrajectorySample(fp.Zero, fp3.Zero, fp3.Zero),
                    new TrajectorySample(fp.One,  finalPos, fp3.Zero),
                },
                finalPos, fp3.Zero, fp.One, TerminationReason.ExitedWorldBounds,
                new List<TerrainHit>());

            var (director, setter, ctrl) = DirectorFactory.Create(isPutt: false, lastTraj: traj);
            ctrl.LastTrajectory = traj;

            ctrl.BallSM.OnTrajectoryComputed(fp3.Zero, traj, fp.FromFloat(0.02f));

            // ExitedWorldBounds produces no OB terrain hit, so TryFindFirstOBHit returns false
            // and falls back to traj.finalPosition.
            Assert.IsTrue(setter.LastChaseClampActive.HasValue && setter.LastChaseClampActive.Value,
                "Clamp should be armed for ExitedWorldBounds");
            Assert.IsTrue(setter.LastChaseClampPoint.HasValue,
                "SetChaseClamp should have been called");
            Assert.AreEqual(500f, setter.LastChaseClampPoint.Value.x, 1f,
                "ExitedWorldBounds clamp X should fall back to finalPosition.x");
        }

        // Test 24 (K10 follow-up): clamp still arms at the OB hit; NO freeze pivot is ever set.
        // (Replaces the old clamp↔pivot-agreement test — the pivot is deleted per Cesar's
        // ruling that OB stops chasing in place instead of cutting to an aerial view.)
        [Test]
        public void Director_OBClamp_Armed_NoFreezePivotOnOB()
        {
            var waterHitPos = new fp3(fp.FromFloat(25f), fp.Zero, fp.FromFloat(5f));
            var hits = new List<TerrainHit>
            {
                TrajectoryBuilder.NonStopHit(new fp3(fp.FromFloat(10f), fp.Zero, fp.Zero), SurfaceType.Fairway),
                TrajectoryBuilder.NonStopHit(waterHitPos, SurfaceType.Water),
            };
            var finalPos = new fp3(fp.FromFloat(25f), fp.Zero, fp.FromFloat(5f));
            var traj = new Trajectory(
                new List<TrajectorySample>
                {
                    new TrajectorySample(fp.Zero,  fp3.Zero,     fp3.Zero),
                    new TrajectorySample(fp.One,   finalPos,     fp3.Zero),
                },
                finalPos, fp3.Zero, fp.One, TerminationReason.HitWater, hits);

            var (director, setter, ctrl) = DirectorFactory.Create(isPutt: false, lastTraj: traj);
            ctrl.LastTrajectory = traj;

            ctrl.BallSM.OnTrajectoryComputed(fp3.Zero, traj, fp.FromFloat(0.02f));

            // The flight-phase clamp still derives from the first OB hit (unchanged behavior).
            Assert.IsTrue(setter.LastChaseClampPoint.HasValue, "SetChaseClamp should have been called");
            Assert.AreEqual(25f, setter.LastChaseClampPoint.Value.x, 0.01f,
                "Chase clamp X must match the first Water hit (TryFindFirstOBHit)");
            Assert.AreEqual(5f, setter.LastChaseClampPoint.Value.z, 0.01f,
                "Chase clamp Z must match the first Water hit (TryFindFirstOBHit)");

            // But the terminal OB state must NOT set a pivot anymore.
            Assert.IsFalse(setter.LastOBFreezePivot.HasValue,
                "SetOBFreezePivot must not be called — OB stops chasing in place.");
        }

        // ── Tests 25–26: K10 ob_recovery_fixes (camera wedge exit) ────────────

        // Test 25 (updated for the K10 follow-up ruling): the whole OB→Aiming camera lifecycle
        // stays in Chase — OB itself no longer enters OBFreeze (stops chasing in place), and
        // re-arming into Aiming keeps Chase so the aim owner / orbit drag (Chase-gated) own
        // the camera. Also guards the defensive OBFreeze→Chase exit: if some legacy path DID
        // set OBFreeze, Aiming still exits it (asserted via a manually-forced OBFreeze).
        [Test]
        public void Director_OBToAiming_StaysChase_AndExitsForcedOBFreeze()
        {
            var waterHit = new fp3(fp.FromFloat(25f), fp.Zero, fp.FromFloat(5f));
            var hits = new List<TerrainHit>
            {
                TrajectoryBuilder.NonStopHit(waterHit, SurfaceType.Water),
            };
            var traj = new Trajectory(
                new List<TrajectorySample>
                {
                    new TrajectorySample(fp.Zero, fp3.Zero, fp3.Zero),
                    new TrajectorySample(fp.One,  waterHit, fp3.Zero),
                },
                waterHit, fp3.Zero, fp.One, TerminationReason.HitWater, hits);

            var (director, setter, ctrl) = DirectorFactory.Create(isPutt: false, lastTraj: traj);
            ctrl.LastTrajectory = traj;

            // OB terminal → Chase (stop chasing in place) + target cleared.
            ctrl.BallSM.OnTrajectoryComputed(fp3.Zero, traj, fp.FromFloat(0.02f));
            Assert.AreEqual(ChaseCamera.Mode.Chase, setter.CurrentMode,
                "OB must leave the camera in Chase (dormant), not OBFreeze.");

            // Re-arm → Aiming: still Chase.
            ctrl.BallSM.ReArm();
            Assert.AreEqual(ChaseCamera.Mode.Chase, setter.CurrentMode,
                "OB→Aiming must keep Chase — aim owner and orbit drag are Chase-gated.");

            // Defensive branch: a lingering OBFreeze (legacy/unknown path) is exited on Aiming.
            setter.SetMode(ChaseCamera.Mode.OBFreeze);
            // Drive a fresh OB→ReArm cycle so →Aiming fires with mode == OBFreeze.
            ctrl.BallSM.OnTrajectoryComputed(fp3.Zero, traj, fp.FromFloat(0.02f));
            setter.SetMode(ChaseCamera.Mode.OBFreeze);   // force the wedge state pre-Aiming
            ctrl.BallSM.ReArm();
            Assert.AreEqual(ChaseCamera.Mode.Chase, setter.CurrentMode,
                "A lingering OBFreeze must still be exited to Chase on Aiming (defensive guard).");
        }

        // Test 26: same-class — after a hole-out, re-arming into Aiming must exit CupZoom → Chase.
        // CupZoom has the identical structural flaw (no null-target early-return, orbit + yaw
        // gated Chase-only); reachable via InCup → modal close → RearmAfterHoleComplete → ReArm.
        [Test]
        public void Director_ReArmAfterInCup_ExitsCupZoomToChase()
        {
            var (director, setter, ctrl) = DirectorFactory.Create(isPutt: false);
            var sm = new BallStateMachine(
                new ConstantSurfaceProvider(SurfaceType.Green),
                new AlwaysInCupDetector());
            sm.Headless = true;
            ctrl.BallSM = sm;
            director.SetControllerAccessor(ctrl);

            var inCupPos = new fp3(fp.FromFloat(10f), fp.Zero, fp.FromFloat(5f));
            sm.OnTrajectoryComputed(fp3.Zero, TrajectoryBuilder.Simple(inCupPos), fp.FromFloat(0.02f));
            Assert.AreEqual(ChaseCamera.Mode.CupZoom, setter.CurrentMode,
                "Precondition: mode should be CupZoom after an InCup terminal state.");

            setter.SetModeCalls.Clear();
            sm.ReArm();

            Assert.AreEqual(ChaseCamera.Mode.Chase, setter.CurrentMode,
                "K10 same-class: re-entering Aiming from CupZoom must switch to Chase " +
                "(otherwise the first aim phase after every hole-out wedges over the previous cup).");
        }

        // Test 27: end-to-end with the REAL ChaseCamera — K10 + follow-up ruling combined.
        // On OB the real camera must go dormant IMMEDIATELY (stop chasing in place — no
        // OBFreeze pivot teleport / top-down cut) and STAY dormant through ReArm→Aiming so
        // the aim owner (ApplyCameraYaw) and orbit drag (HandleCameraOrbit) own the transform.
        [Test]
        public void Director_RealChaseCamera_OBToAiming_CameraGoesDormant()
        {
            var camGO = new GameObject("RealChaseCam");
            var chase = camGO.AddComponent<ChaseCamera>();

            var dirGO    = new GameObject("DirectorE2E");
            var director = dirGO.AddComponent<LoopCameraDirector>();
            director.SetModeSetter(chase); // real ChaseCamera, not RecordingModeSetter

            var sm = new BallStateMachine(new ConstantSurfaceProvider(SurfaceType.Fairway));
            sm.Headless = true;

            var waterHit = new fp3(fp.FromFloat(25f), fp.Zero, fp.FromFloat(5f));
            var hits = new List<TerrainHit> { TrajectoryBuilder.NonStopHit(waterHit, SurfaceType.Water) };
            var traj = new Trajectory(
                new List<TrajectorySample>
                {
                    new TrajectorySample(fp.Zero, fp3.Zero, fp3.Zero),
                    new TrajectorySample(fp.One,  waterHit, fp3.Zero),
                },
                waterHit, fp3.Zero, fp.One, TerminationReason.HitWater, hits);

            var ctrl = new StubControllerAccessor
            {
                BallSM            = sm,
                LastTrajectory    = traj,
                LastShotOrigin    = Vector3.zero,
                LastShotLaunchDir = Vector3.forward,
            };
            director.SetControllerAccessor(ctrl);

            // Park the camera where the (clamped) chase would have left it, then drive →OB.
            Vector3 parked = new Vector3(111f, 222f, 333f);
            chase.transform.position = parked;
            sm.OnTrajectoryComputed(fp3.Zero, traj, fp.FromFloat(0.02f));

            Assert.AreEqual(ChaseCamera.Mode.Chase, chase.CurrentMode,
                "OB must leave the real camera in Chase — never OBFreeze (top-down pivot).");

            // Dormant immediately on OB: LateUpdate must NOT move the camera — it stays
            // exactly where the chase left it ("just stop chasing", Cesar 2026-08-05).
            for (int i = 0; i < 30; i++) chase.FrameCamera(1f / 60f);
            Assert.That(chase.transform.position, Is.EqualTo(parked),
                "On OB the camera must freeze in place — no pivot teleport, no re-pointing.");

            // Still dormant through ReArm→Aiming: aim owner / orbit drag own the transform.
            sm.ReArm();
            Assert.AreEqual(ChaseCamera.Mode.Chase, chase.CurrentMode,
                "After ReArm→Aiming the real ChaseCamera must remain in Chase.");
            Vector3 held = new Vector3(10f, 20f, 30f);
            chase.transform.position = held;
            for (int i = 0; i < 30; i++) chase.FrameCamera(1f / 60f);
            Assert.AreEqual(held, chase.transform.position,
                "In Chase with a null target the camera is dormant — aim/drag own it (no wedge).");

            Object.DestroyImmediate(camGO);
            Object.DestroyImmediate(dirGO);
        }

        // ── Tests 28–31: K10 follow-up — freeze on leaving the playable area ──

        // Helper: a flight that crosses the OB line at X=100 mid-air and sails far beyond it.
        static Trajectory CrossingTrajectory()
        {
            var samples = new List<TrajectorySample>();
            for (int i = 0; i <= 20; i++)   // X = 0,10,...,200 — crosses X=100 at sample 10
                samples.Add(new TrajectorySample(
                    fp.FromFloat(i * 0.1f),
                    new fp3(fp.FromFloat(i * 10f), fp.FromFloat(20f), fp.Zero),
                    fp3.Zero));
            return new Trajectory(samples, new fp3(fp.FromFloat(200f), fp.Zero, fp.Zero),
                fp3.Zero, fp.One, TerminationReason.ExitedWorldBounds, new List<TerrainHit>());
        }

        // Test 28: the clamp point is the AIRBORNE boundary crossing, not the ±2 km
        // ExitedWorldBounds fallback that made the old clamp a no-op.
        [Test]
        public void Director_ClampsAtPlayableAreaExit_NotWorldBoundsFallback()
        {
            var traj = CrossingTrajectory();
            var (director, setter, ctrl) = DirectorFactory.Create(isPutt: false, lastTraj: traj);
            ctrl.LastTrajectory  = traj;
            ctrl.SurfaceProvider = new BoundaryAtXProvider(100f);

            ctrl.BallSM.OnTrajectoryComputed(fp3.Zero, traj, fp.FromFloat(0.02f));

            Assert.IsTrue(setter.LastChaseClampActive.HasValue && setter.LastChaseClampActive.Value,
                "Clamp must be armed for a flight that leaves the playable area.");
            Assert.AreEqual(100f, setter.LastChaseClampPoint.Value.x, 0.01f,
                "Clamp must sit at the boundary crossing (X=100), NOT at finalPosition (X=200) — " +
                "ExitedWorldBounds is the sim's 2 km safety net, not the course edge.");
        }

        // Test 29 — the behaviour Cesar chose, on the REAL ChaseCamera: once the ball is past
        // the boundary the camera HOLDS its position (never advances further out) but keeps
        // ROTATING to watch the ball sail away. Position pinned, rotation live.
        [Test]
        public void ChaseCamera_ClampedAtBoundary_HoldsPositionButKeepsTrackingBall()
        {
            var camGO = new GameObject("ClampCam");
            var chase = camGO.AddComponent<ChaseCamera>();
            chase.SetMode(ChaseCamera.Mode.Chase);
            chase.ResetToOrigin(Vector3.zero, Vector3.right);      // shot along +X
            chase.SetChaseClamp(new Vector3(100f, 0f, 0f), true);  // OB line at X=100

            var ballGO = new GameObject("BallOut");
            chase.SetTarget(ballGO.transform);

            // Ball well past the boundary — converge.
            ballGO.transform.position = new Vector3(150f, 20f, 0f);
            for (int i = 0; i < 400; i++) chase.FrameCamera(1f / 60f);
            Vector3 posAt150 = chase.transform.position;
            Quaternion rotAt150 = chase.transform.rotation;

            Assert.Less(posAt150.x, 100f,
                "Camera must never advance past the boundary (X=100) even though the ball is at 150.");

            // Ball keeps flying out — position must NOT follow, rotation MUST track.
            ballGO.transform.position = new Vector3(400f, 20f, 0f);
            for (int i = 0; i < 400; i++) chase.FrameCamera(1f / 60f);

            Assert.AreEqual(posAt150.x, chase.transform.position.x, 0.01f,
                "Camera X must stay pinned at the boundary as the ball flies further out.");
            Assert.AreNotEqual(rotAt150, chase.transform.rotation,
                "Camera must keep rotating to follow the ball out (not a frozen pose).");

            // And it is genuinely looking at the ball.
            Vector3 toBall = (ballGO.transform.position - chase.transform.position).normalized;
            Assert.Greater(Vector3.Dot(chase.transform.forward, toBall), 0.9f,
                "Camera should be aimed at the live ball position.");

            Object.DestroyImmediate(ballGO);
            Object.DestroyImmediate(camGO);
        }

        // Test 30: the target is never cleared mid-flight — the camera keeps tracking, so the
        // ball stays on screen as it leaves (regression guard for the reverted freeze variant).
        [Test]
        public void Director_DoesNotClearTargetMidFlight_OnOBShot()
        {
            var traj = CrossingTrajectory();
            var (director, setter, ctrl) = DirectorFactory.Create(isPutt: false, lastTraj: traj);
            ctrl.LastTrajectory  = traj;
            ctrl.SurfaceProvider = new BoundaryAtXProvider(100f);

            var sm = new BallStateMachine(new ConstantSurfaceProvider(SurfaceType.Fairway));
            sm.Headless = false;                 // only Aiming→Flying fires; ball stays airborne
            ctrl.BallSM = sm;
            var ballGO = new GameObject("BallInFlight");
            ctrl.CurrentBall = ballGO.transform;
            director.SetControllerAccessor(ctrl);

            sm.OnTrajectoryComputed(fp3.Zero, traj, fp.FromFloat(0.02f));

            Assert.IsNotNull(setter.SetTargetCalls[setter.SetTargetCalls.Count - 1],
                "Chase target must be the ball while it is in flight.");

            // Even with the ball past the boundary, nothing may clear the target mid-flight —
            // the clamp alone keeps the camera inside while rotation tracks the ball out.
            ballGO.transform.position = new Vector3(105f, 20f, 0f);
            director.TickCinematicCut();
            Assert.IsNotNull(setter.SetTargetCalls[setter.SetTargetCalls.Count - 1],
                "Target must NOT be cleared mid-flight — the camera keeps watching the ball leave.");

            Object.DestroyImmediate(ballGO);
        }

        // Test 31: a normal in-bounds shot never arms the clamp (byte-identical to before).
        [Test]
        public void Director_InBoundsShot_DoesNotArmClamp()
        {
            var traj = TrajectoryBuilder.Simple(new fp3(fp.FromFloat(50f), fp.Zero, fp.Zero));
            var (director, setter, ctrl) = DirectorFactory.Create(isPutt: false, lastTraj: traj);
            ctrl.LastTrajectory  = traj;
            ctrl.SurfaceProvider = new BoundaryAtXProvider(100f);   // never reached

            ctrl.BallSM.OnTrajectoryComputed(fp3.Zero, traj, fp.FromFloat(0.02f));

            Assert.IsTrue(setter.LastChaseClampActive.HasValue,
                "SetChaseClamp must still be called for in-bounds shots.");
            Assert.IsFalse(setter.LastChaseClampActive.Value,
                "An in-bounds shot must leave the clamp DISARMED — normal chase is unaffected.");
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
        // Velocity-aware overload: always returns true (test-only stub ignores speed gate).
        public bool IsInCup(fp3 pos, fp radius, fp3 velocity) => true;
    }
}
