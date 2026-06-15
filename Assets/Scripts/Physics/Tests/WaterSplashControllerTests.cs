using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Golfin.Physics;
using Golfin.Physics.Math;
using Golfin.Physics.Viewer;
using Golfin.Gameplay.Loop;

namespace Golfin.Physics.Tests
{
    /// <summary>
    /// EditMode tests for WaterSplashController.
    ///
    /// ALL tests invoke the PRODUCTION HandleStateChanged (now internal + InternalsVisibleTo)
    /// either via Configure() (production wiring path) or via direct subscription.
    /// No test re-implements the trigger predicate; every test proves the real controller code.
    ///
    /// Verifies spec §5:
    ///   WaterSplash_TriggersOnlyOnWaterOB - fires exactly once on OBReason.Water.
    ///   WaterSplash_DoesNotFireOnOOB      - never fires on OOB (non-water OB).
    ///   WaterSplash_NullPrefab_DoesNotThrow - no exception, null guard works.
    /// </summary>
    [TestFixture]
    public class WaterSplashControllerTests
    {
        // ── Helpers ───────────────────────────────────────────────────────────

        static fp3 AnyPos() => new fp3(fp.FromFloat(10f), fp.Zero, fp.Zero);

        // Minimal surface provider for BallStateMachine ctor.
        class FakeSurface : ISurfaceProvider
        {
            public SurfaceType Classify(fp x, fp z) => SurfaceType.Fairway;
        }

        /// <summary>
        /// Build a rig where the REAL WaterSplashController.HandleStateChanged
        /// is subscribed to sm.OnStateChanged via Configure().
        ///
        /// _splashPrefab is null (no prefab in tests), so HandleStateChanged
        /// will set _nullPrefabLogged=true and return — no Instantiate.
        /// We assert on NullPrefabWarningLogged to confirm the handler ran.
        /// </summary>
        (BallStateMachine sm, WaterSplashController ctrl, GameObject go) BuildRig()
        {
            // WaterSplashController is a MonoBehaviour — needs a GameObject host.
            var go   = new GameObject("TestHost_WaterSplash");
            var ctrl = go.AddComponent<WaterSplashController>();
            var sm   = new BallStateMachine(new FakeSurface());

            // Keep the test prefab-less: suppress the Resources.Load fallback so no
            // ParticleSystem is instantiated into the editor scene during EditMode tests.
            ctrl.SuppressResourcesLoadForTests();

            // Production wiring path: Configure subscribes HandleStateChanged.
            // _splashPrefab stays null (default) → PlaySplash returns after setting _nullPrefabLogged.
            ctrl.Configure(null, sm, null);

            return (sm, ctrl, go);
        }

        // ── Trajectory factories ──────────────────────────────────────────────

        static fp3 Pos(float x, float y, float z)
            => new fp3(fp.FromFloat(x), fp.FromFloat(y), fp.FromFloat(z));

        static TrajectorySample Sample(float t, float x, float y, float z)
            => new TrajectorySample(fp.FromFloat(t), Pos(x, y, z), fp3.Zero);

        static TerrainHit Hit(float t, float x, float y, float z, SurfaceType surf, bool isStop)
            => new TerrainHit(fp.FromFloat(t), Pos(x, y, z), fp3.Zero, fp3.Zero, surf, isStop);

        /// Water-terminating trajectory → triggers BallStateMachine → OBReason.Water.
        static Trajectory MakeWaterTrajectory(fp3 start)
        {
            var samples = new List<TrajectorySample>
            {
                Sample(0f, start.x.ToFloat(), start.y.ToFloat(), start.z.ToFloat()),
                Sample(1.5f, 15f, -0.1f, 0f),
            };
            var hits = new List<TerrainHit>
            {
                Hit(1.5f, 15f, -0.1f, 0f, SurfaceType.Water, true),
            };
            return new Trajectory(samples, Pos(15f, -0.1f, 0f), fp3.Zero,
                                   fp.FromFloat(1.5f), TerminationReason.HitWater, hits);
        }

        /// OOB trajectory (no water) → no splash.
        static Trajectory MakeOOBTrajectory(fp3 start)
        {
            var samples = new List<TrajectorySample>
            {
                Sample(0f, start.x.ToFloat(), start.y.ToFloat(), start.z.ToFloat()),
                Sample(2f, 30f, 0f, 0f),
            };
            var hits = new List<TerrainHit>
            {
                Hit(2f, 30f, 0f, 0f, SurfaceType.OOB, true),
            };
            return new Trajectory(samples, Pos(30f, 0f, 0f), fp3.Zero,
                                   fp.FromFloat(2f), TerminationReason.HitOOB, hits);
        }

        /// Normal fairway landing → no splash.
        static Trajectory MakeAtRestTrajectory(fp3 start)
        {
            var samples = new List<TrajectorySample>
            {
                Sample(0f, start.x.ToFloat(), start.y.ToFloat(), start.z.ToFloat()),
                Sample(1f, 10f, 2f, 0f),
                Sample(2f, 20f, 0f, 0f),
            };
            var hits = new List<TerrainHit>
            {
                Hit(1.5f, 15f, 0f, 0f, SurfaceType.Fairway, false),
                Hit(2f,   20f, 0f, 0f, SurfaceType.Fairway, true),
            };
            return new Trajectory(samples, Pos(20f, 0f, 0f), fp3.Zero,
                                   fp.FromFloat(2f), TerminationReason.BallStopped, hits);
        }

        // ── Tests ─────────────────────────────────────────────────────────────

        [TearDown]
        public void TearDown()
        {
            // Clean up GameObject created per-test (BuildRig).
            foreach (var go in GameObject.FindObjectsOfType<GameObject>())
            {
                if (go.name == "TestHost_WaterSplash")
                    GameObject.DestroyImmediate(go);
            }
        }

        /// <summary>
        /// WaterSplash_TriggersOnlyOnWaterOB:
        /// Feeds a Water-terminating trajectory to the BallStateMachine and ticks it.
        /// The REAL HandleStateChanged (production code) fires via Configure() subscription.
        /// Asserts that NullPrefabWarningLogged == true after exactly one OBReason.Water OB.
        /// </summary>
        [Test]
        public void WaterSplash_TriggersOnlyOnWaterOB()
        {
            var (sm, ctrl, _) = BuildRig();

            // Arm the SM: Idle → Flying via OnTrajectoryComputed.
            sm.OnTrajectoryComputed(
                AnyPos(),
                MakeWaterTrajectory(AnyPos()),
                fp.FromFloat(0.021f));

            // Tick true (animator playing) then false (stopped) → SM fires OnStateChanged
            // with BallState.OB + OBReason.Water → real HandleStateChanged runs.
            sm.Tick(true);
            sm.Tick(false);

            Assert.AreEqual(1, ctrl.WaterOBFireCount,
                "HandleStateChanged must fire the splash exactly once on a single OBReason.Water " +
                "transition. WaterOBFireCount is incremented by the real production handler after " +
                "the predicate passes. If 0, the production handler was never called.");
            Assert.IsTrue(ctrl.NullPrefabWarningLogged,
                "With no prefab and Resources suppressed, the null-prefab path must have run.");
        }

        /// <summary>
        /// WaterSplash_DoesNotFireOnOOB:
        /// OOB trajectory does NOT carry OBReason.Water — splash must not trigger.
        /// Verifies that the real predicate in HandleStateChanged correctly rejects it.
        /// </summary>
        [Test]
        public void WaterSplash_DoesNotFireOnOOB()
        {
            var (sm, ctrl, _) = BuildRig();

            sm.OnTrajectoryComputed(AnyPos(), MakeOOBTrajectory(AnyPos()), fp.FromFloat(0.021f));
            sm.Tick(true);
            sm.Tick(false);

            Assert.AreEqual(0, ctrl.WaterOBFireCount,
                "Splash must NOT fire for a non-water OB. WaterOBFireCount should remain 0.");
            Assert.IsFalse(ctrl.NullPrefabWarningLogged,
                "Non-water OB must not reach PlaySplash. NullPrefabWarningLogged should remain false.");
        }

        /// <summary>
        /// WaterSplash_NullPrefab_DoesNotThrow:
        /// With _splashPrefab null (default), HandleStateChanged must be a silent no-op.
        /// No exceptions, no behavior change.
        /// Uses the FireWaterSplashForTest editor seam to invoke the real handler directly.
        /// </summary>
        [Test]
        public void WaterSplash_NullPrefab_DoesNotThrow()
        {
            var go   = new GameObject("TestHost_WaterSplash");
            var ctrl = go.AddComponent<WaterSplashController>();
            ctrl.SuppressResourcesLoadForTests();

            // Direct invocation via the internal editor seam.
            // _splashPrefab is null and Resources load suppressed → must NOT throw.
            Assert.DoesNotThrow(
                () => ctrl.FireWaterSplashForTest(AnyPos()),
                "WaterSplashController must be a no-op when _splashPrefab is null.");

            // Predicate passed once; null guard logged once.
            Assert.AreEqual(1, ctrl.WaterOBFireCount,
                "Handler must have run exactly once via the editor seam.");
            Assert.IsTrue(ctrl.NullPrefabWarningLogged,
                "NullPrefabWarningLogged must be set after first no-op call.");

            GameObject.DestroyImmediate(go);
        }

        /// <summary>
        /// WaterSplash_DoesNotFireOnAtRest:
        /// A normal fairway landing must not trigger the splash.
        /// Exercises the real predicate path with BallState.AtRest (non-OB).
        /// </summary>
        [Test]
        public void WaterSplash_DoesNotFireOnAtRest()
        {
            var (sm, ctrl, _) = BuildRig();

            sm.OnTrajectoryComputed(AnyPos(), MakeAtRestTrajectory(AnyPos()), fp.FromFloat(0.021f));
            sm.Tick(true);
            sm.Tick(false);

            Assert.AreEqual(0, ctrl.WaterOBFireCount,
                "Splash must NOT fire on a normal fairway landing. WaterOBFireCount should remain 0.");
            Assert.IsFalse(ctrl.NullPrefabWarningLogged,
                "Normal landing must not reach PlaySplash. NullPrefabWarningLogged should remain false.");
        }
    }
}
