using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Golfin.Physics.Viewer;

namespace Golfin.Physics.Tests
{
    /// <summary>
    /// F-Hotfix.D tests 10-12: end-to-end ball placement integration.
    /// Tests use [UnityTest] to run across frames so MonoBehaviour lifecycle fires.
    /// </summary>
    public class BallPlacementIntegrationTests
    {
        // ── Reflection helpers ─────────────────────────────────────────────────

        static System.Type      _smType;
        static System.Reflection.FieldInfo _stField;

        static void EnsureReflection()
        {
            if (_smType == null)
            {
                _smType  = System.Type.GetType("Golfin.Course.SurfaceMarker, Assembly-CSharp");
                _stField = _smType?.GetField("surfaceType");
            }
        }

        readonly System.Collections.Generic.List<GameObject> _created
            = new System.Collections.Generic.List<GameObject>();

        GameObject Track(GameObject go) { _created.Add(go); return go; }

        [TearDown]
        public void Cleanup()
        {
            foreach (var go in _created)
                if (go != null) Object.DestroyImmediate(go);
            _created.Clear();
        }

        // BoxCollider registers synchronously — reliable for EditMode physics raycasts.
        GameObject CreateFlatCollider(float x, float y, float z, float size = 4f)
        {
            var go = Track(new GameObject("FlatCollider"));
            go.transform.position = new Vector3(x, y, z);
            go.AddComponent<BoxCollider>().size = new Vector3(size, 0.02f, size);
            return go;
        }

        static Component AddSurfaceMarker(GameObject go, int typeValue)
        {
            EnsureReflection();
            if (_smType == null) return null;
            var marker = go.AddComponent(_smType) as Component;
            _stField?.SetValue(marker, System.Enum.ToObject(_stField.FieldType, typeValue));
            return marker;
        }

        PhysicsLabController SetupMinimalController(out BallAnimator ballAnim)
        {
            var host = Track(new GameObject("LabHost"));

            var animGO = Track(new GameObject("BallAnimator"));
            ballAnim   = animGO.AddComponent<BallAnimator>();

            var controller = host.AddComponent<PhysicsLabController>();

            // Wire ballAnimator field via reflection (serialized private field).
            typeof(PhysicsLabController)
                .GetField("ballAnimator", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(controller, ballAnim);

            return controller;
        }

        // ── Test 10 ────────────────────────────────────────────────────────────
        // PlaceBallAt Green uses the Green collider (Y=10.15) even when fringe (Y=10.18) is higher.
        // This is the exact Bug 1 regression scenario.

        [UnityTest]
        public IEnumerator PlaceBallAt_Green_BallLandsAtGreenY_NotFringeY()
        {
            EnsureReflection();
            if (_smType == null) { Assert.Ignore("SurfaceMarker not found."); yield break; }

            float testX = 600f, testZ = 600f;

            // Fringe/fairway collider higher.
            var fairwayGO = CreateFlatCollider(testX, 10.18f, testZ);
            AddSurfaceMarker(fairwayGO, 0); // Fairway

            // Green collider lower.
            var greenGO = CreateFlatCollider(testX, 10.15f, testZ);
            AddSurfaceMarker(greenGO, 1); // Green

            yield return null; // wait for PhysX

            PhysicsLabController controller = SetupMinimalController(out BallAnimator ballAnim);
            yield return null;

            controller.PlaceBallAt(new Vector3(testX, 10f, testZ), 1 /* Green */);

            float ballY = ballAnim.CurrentBall?.position.y ?? -1f;

            Assert.That(ballY, Is.EqualTo(10.15f).Within(0.05f),
                $"Ball Y={ballY} — expected Green Y=10.15, not fringe Y=10.18 (Bug 1 regression).");
        }

        // ── Test 11 ────────────────────────────────────────────────────────────
        // PlaceBallAt called twice moves the ball to the correct position both times.
        // Regression for "Place Here stops working after first placement."

        [UnityTest]
        public IEnumerator PlaceBallAt_CalledTwice_BallTeleportsBothTimes()
        {
            float ax = 601f, az = 600f;
            float bx = 603f, bz = 600f; // 2m apart so clearly different

            // Flat terrain at Y=10 covering both positions.
            CreateFlatCollider(602f, 10f, az, 6f);

            yield return null;

            var controller = SetupMinimalController(out BallAnimator ballAnim);
            yield return null;

            // First placement.
            controller.PlaceBallAt(new Vector3(ax, 10f, az));
            float posAx = ballAnim.CurrentBall?.position.x ?? -999f;
            Assert.That(posAx, Is.EqualTo(ax).Within(0.1f), $"First placement X mismatch: got {posAx}");

            // Second placement.
            controller.PlaceBallAt(new Vector3(bx, 10f, bz));
            float posBx = ballAnim.CurrentBall?.position.x ?? -999f;
            Assert.That(posBx, Is.EqualTo(bx).Within(0.1f), $"Second placement X mismatch: got {posBx}");
            Assert.That(posAx, Is.Not.EqualTo(posBx).Within(0.05f), "Ball X must change between placements.");
        }

        // ── Test 12 ────────────────────────────────────────────────────────────
        // OnHoleLoaded sets _useSceneProviders=true — this is the observable the coroutine depends on.

        [UnityTest]
        public IEnumerator OnHoleLoaded_Sets_UseSceneProviders_True()
        {
            var controllerGO = Track(new GameObject("TestController12"));
            var controller   = controllerGO.AddComponent<PhysicsLabController>();

            yield return null;

            // Confirm _useSceneProviders starts false.
            bool before = (bool)(typeof(PhysicsLabController)
                .GetField("_useSceneProviders", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.GetValue(controller) ?? false);

            Assert.IsFalse(before, "_useSceneProviders should start false.");

            // Simulate what the coroutine does: call OnHoleLoaded for a scene.
            // A non-existent scene name produces no markers but still sets the flag.
            controller.OnHoleLoaded("Hole_TEST_Geo");

            bool after = (bool)(typeof(PhysicsLabController)
                .GetField("_useSceneProviders", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.GetValue(controller) ?? false);

            Assert.IsTrue(after, "_useSceneProviders must be true after OnHoleLoaded.");
        }
    }
}
