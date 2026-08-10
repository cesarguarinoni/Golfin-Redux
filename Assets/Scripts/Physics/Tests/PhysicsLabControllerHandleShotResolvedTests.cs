using NUnit.Framework;
using UnityEngine;
using Golfin.Physics;
using Golfin.Physics.Math;
using Golfin.Physics.Runtime;
using Golfin.Physics.Viewer;
using Golfin.Gameplay.Loop;

namespace Golfin.Physics.Tests
{
    /// <summary>
    /// §controls_h integration test: verifies that after HandleShotResolved returns,
    /// the LoopCameraDirector has armed the chase camera with a valid (non-null, post-Play)
    /// ball Transform.
    ///
    /// This test catches the regression class from controls_g_smoke_followup where
    /// _ballSM.OnTrajectoryComputed was called BEFORE BallAnimator.Play(), leaving the
    /// Director with a destroyed Transform reference.
    ///
    /// Root cause verified: PhysicsLabController.HandleShotResolved line ordering bug
    /// (fixed in this task). See SPEC.md § Implementation § A for diagnosis.
    /// </summary>
    public class PhysicsLabControllerHandleShotResolvedTests
    {
        // ── Scaffold ────────────────────────────────────────────────────────────

        /// <summary>
        /// Creates a minimal real scene graph:
        ///   LabRoot
        ///     ├── PhysicsLabController
        ///     ├── BallAnimator          (no prefab — spawns a sphere primitive)
        ///     └── LoopCameraDirector    (injected with RecordingModeSetter)
        ///
        /// Returns the controller, a BallAnimator reference, the Director, and the
        /// RecordingModeSetter for assertion.
        /// </summary>
        static (PhysicsLabController ctrl, BallAnimator animator,
                LoopCameraDirector director, RecordingModeSetter setter)
            CreatePhysicsLabSetup()
        {
            var root = new GameObject("LabRoot_Test");

            // BallAnimator — no prefab assigned → SpawnInstance falls back to CreatePrimitive(Sphere)
            var animatorGO = new GameObject("BallAnimator_Test");
            animatorGO.transform.SetParent(root.transform);
            var animator = animatorGO.AddComponent<BallAnimator>();

            // PhysicsLabController — attach to root, then inject deps
            var ctrl = root.AddComponent<PhysicsLabController>();

            // BallStateMachine in live mode (Headless=false):
            // OnTrajectoryComputed fires ONLY Aiming→Flying synchronously,
            // then queues remaining transitions for Tick(). This is the production path.
            var sm = new BallStateMachine(new ConstantSurfaceProvider(SurfaceType.Fairway));
            sm.Headless = false;

            ctrl.InjectForTests(animator, sm);

            // LoopCameraDirector with RecordingModeSetter — subscribe to the SM
            var directorGO = new GameObject("Director_Test");
            directorGO.transform.SetParent(root.transform);
            var director  = directorGO.AddComponent<LoopCameraDirector>();
            var setter    = new RecordingModeSetter();
            director.SetModeSetter(setter);

            // Create a stub accessor that reads from the real controller's internal state.
            // We wrap the real ctrl rather than using StubControllerAccessor so the
            // Director reads the ACTUAL _lastShotOrigin/_lastShotLaunchDir/CurrentBall
            // that HandleShotResolved sets — that's what we're testing.
            var adapter = new PhysicsLabControllerAdapter_Test(ctrl, sm);
            director.SetControllerAccessor(adapter);

            return (ctrl, animator, director, setter);
        }

        /// <summary>
        /// Builds a 50-yard driver ShotInput at origin with forward launch direction.
        /// Velocity matches a ~46m/s driver preset (no spin for simplicity).
        /// </summary>
        static ShotInput MakeDriverShotInput()
        {
            // ~46m/s at 10.9° pitch → ~200m carry on flat ground (well within driver range)
            float velMps   = 46f;
            float pitchRad = 10.9f * Mathf.Deg2Rad;
            var velocity = new fp3(
                fp.FromFloat(velMps * Mathf.Cos(pitchRad)),  // X forward
                fp.FromFloat(velMps * Mathf.Sin(pitchRad)),  // Y up
                fp.Zero);                                     // Z
            return new ShotInput(fp3.Zero, velocity, fp.FromInt(60));
        }

        // ── Test ────────────────────────────────────────────────────────────────

        /// <summary>
        /// §controls_h regression test.
        ///
        /// Verifies that after HandleShotResolved returns:
        /// (1) The Director has set a NON-NULL chase target — proving BallAnimator.Play()
        ///     ran BEFORE OnTrajectoryComputed, so the fresh ball Transform was available
        ///     when ArmChaseForShot fired.
        /// (2) The chase target equals ballAnimator.CurrentBall — the post-Play() Transform,
        ///     NOT a stale or destroyed pre-Play() reference.
        /// (3) The mode is Chase — the Aiming→Flying dispatch fired correctly.
        ///
        /// Before the fix, assertion (1) would fail: _target was a destroyed Transform
        /// (equating to null) because Play() ran AFTER OnTrajectoryComputed.
        /// </summary>
        [Test]
        public void Director_HandleShotResolvedFlow_TargetIsValidAfterPlay()
        {
            var (ctrl, animator, director, setter) = CreatePhysicsLabSetup();

            try
            {
                // Arrange
                var input    = MakeDriverShotInput();
                var ballMods = BallPhysicsModifiers.Neutral;

                // Act: drive the real touch-path flow.
                ctrl.HandleShotResolvedForTests(input, ballMods);

                // Assert 1: chase target is NOT null.
                // Failure means SM fired before BallAnimator.Play() — old ball was destroyed.
                Assert.That(setter.SetTargetCalls.Count, Is.GreaterThan(0),
                    "SetTarget should have been called at least once during Aiming→Flying transition.");

                // The LAST SetTarget call during the shot setup should be the valid ball transform.
                // (SetTarget(null) is called on terminal states — in live mode those don't fire
                // synchronously, so only the ArmChaseForShot SetTarget should be in the list.)
                Transform lastTarget = setter.SetTargetCalls[setter.SetTargetCalls.Count - 1];

                Assert.That(lastTarget, Is.Not.Null,
                    "Chase target should be non-null after HandleShotResolved — was the SM " +
                    "transition fired before BallAnimator.Play()? (controls_h regression)");

                // Assert 2: target matches the post-Play() ball.
                Assert.That(lastTarget, Is.EqualTo(animator.CurrentBall),
                    "Chase target should be the post-Play() ball Transform, not a stale or " +
                    "destroyed one. If this fails, BallAnimator.Play() ran AFTER OnTrajectoryComputed.");

                // Assert 3: mode is Chase (not Downrange — cinematic cut hasn't ticked yet).
                Assert.That(setter.CurrentMode, Is.EqualTo(ChaseCamera.Mode.Chase),
                    "Mode should be Chase after Aiming→Flying transition. Cinematic cut has " +
                    "not ticked yet (no Update loop in EditMode tests).");
            }
            finally
            {
                // Clean up GameObjects created in the test.
                foreach (var go in Object.FindObjectsOfType<PhysicsLabController>())
                    Object.DestroyImmediate(go.gameObject);
                foreach (var go in Object.FindObjectsOfType<LoopCameraDirector>())
                    Object.DestroyImmediate(go.gameObject);
                foreach (var go in Object.FindObjectsOfType<BallAnimator>())
                    Object.DestroyImmediate(go.gameObject);
            }
        }

        // ── Teardown ────────────────────────────────────────────────────────────

        [TearDown]
        public void TearDown()
        {
            foreach (var go in Object.FindObjectsOfType<PhysicsLabController>())
                Object.DestroyImmediate(go.gameObject);
            foreach (var go in Object.FindObjectsOfType<LoopCameraDirector>())
                Object.DestroyImmediate(go.gameObject);
            foreach (var go in Object.FindObjectsOfType<BallAnimator>())
                Object.DestroyImmediate(go.gameObject);
        }
    }

    // ── Test adapter ────────────────────────────────────────────────────────────

    /// <summary>
    /// Wraps the real PhysicsLabController's internal state accessors for the Director.
    /// Unlike the production <c>PhysicsLabControllerAdapter</c> (private to the Viewer assembly),
    /// this lives in the Tests assembly and reads the same <c>internal</c> properties.
    /// </summary>
    sealed class PhysicsLabControllerAdapter_Test : IControllerAccessor
    {
        readonly PhysicsLabController _ctrl;
        readonly BallStateMachine     _sm;

        public PhysicsLabControllerAdapter_Test(PhysicsLabController ctrl, BallStateMachine sm)
        {
            _ctrl = ctrl;
            _sm   = sm;
        }

        // Use the injected SM (same instance the controller has) so the Director
        // subscribes to the real event stream.
        public BallStateMachine  BallSM            => _sm;
        public Trajectory        LastTrajectory    => _ctrl.LastTrajectory;
        public Vector3           LastShotOrigin    => _ctrl.LastShotOrigin;
        public Vector3           LastShotLaunchDir => _ctrl.LastShotLaunchDir;
        public Transform         CurrentBall       => _ctrl.CurrentBall;
        public bool              CurrentShotIsPutt => _ctrl.CurrentShotIsPutt;
        // K10 follow-up: same internal accessor the production adapter reads. Null on the
        // flat-ground sessions these tests use → the playable-area freeze stays inert.
        public ISurfaceProvider  SurfaceProvider   => _ctrl.SurfaceProviderForCamera;
    }
}
