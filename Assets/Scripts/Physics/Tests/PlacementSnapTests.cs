using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Golfin.Physics.Viewer;

namespace Golfin.Physics.Tests
{
    /// <summary>
    /// F-Hotfix.D regression tests for type-aware SurfaceSnap and camera depression lift.
    /// Tests 1-4: SurfaceSnap preferred-type selection.
    /// Tests 5-6: Camera depression lift via PlaceBallAt.
    /// </summary>
    public class PlacementSnapTests
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

        static Component AddSurfaceMarker(GameObject go, int surfaceTypeValue)
        {
            EnsureReflection();
            Assert.IsNotNull(_smType, "Golfin.Course.SurfaceMarker not found — Assembly-CSharp must be loaded.");
            var marker = go.AddComponent(_smType) as Component;
            _stField.SetValue(marker, System.Enum.ToObject(_stField.FieldType, surfaceTypeValue));
            return marker;
        }

        // BoxCollider registers synchronously in PhysX — more reliable than MeshCollider in EditMode.
        static GameObject CreateFlatCollider(float x, float y, float z, float size = 4f)
        {
            var go = new GameObject("FlatCollider");
            go.transform.position = new Vector3(x, y, z);
            var box = go.AddComponent<BoxCollider>();
            box.size = new Vector3(size, 0.02f, size); // very thin box, top face at y+0.01
            return go;
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

        // ── Test 1 ─────────────────────────────────────────────────────────────
        // Bug 1 reproduction: green (Y=10.15) overlapping fringe-on-fairway (Y=10.18).
        // With preferredType=1 (Green), snap must return 10.15.

        [UnityTest]
        public IEnumerator SurfaceSnap_WithPreferredType_PicksMatchingMarker()
        {
            EnsureReflection();
            if (_smType == null) { Assert.Ignore("SurfaceMarker type not found."); yield break; }

            float testX = 500f, testZ = 500f;

            var fairwayGO = Track(CreateFlatCollider(testX, 10.18f, testZ));
            AddSurfaceMarker(fairwayGO, 0); // Fairway

            var greenGO = Track(CreateFlatCollider(testX, 10.15f, testZ));
            AddSurfaceMarker(greenGO, 1); // Green

            yield return null; // let PhysX register

            float result = PlacementSnapHelper.Snap(testX, testZ, 0f, preferredSurfaceTypeValue: 1);

            // BoxCollider top face is center+0.01, so green box at 10.15 → hit at ~10.16.
            // Key assertion: result must be near Green (10.15-10.17), NOT near Fairway (10.18-10.20).
            Assert.That(result, Is.LessThan(10.17f),
                $"Expected Green collider hit (~10.15) but got {result}. Fairway fringe must NOT win over Green.");
        }

        // ── Test 2 ─────────────────────────────────────────────────────────────
        // When no hit matches preferred type, fall back to first (closest from Y=500) hit.

        [UnityTest]
        public IEnumerator SurfaceSnap_WithPreferredType_AndNoMatch_FallsBackToFirstHit()
        {
            EnsureReflection();
            if (_smType == null) { Assert.Ignore("SurfaceMarker type not found."); yield break; }

            float testX = 501f, testZ = 500f;

            var fairwayGO = Track(CreateFlatCollider(testX, 10.18f, testZ));
            AddSurfaceMarker(fairwayGO, 0); // Fairway

            var greenGO = Track(CreateFlatCollider(testX, 10.15f, testZ));
            AddSurfaceMarker(greenGO, 1); // Green

            yield return null;

            // Ask for Bunker (4) — no match → first hit (closest from Y=500) = highest Y = 10.18.
            float result = PlacementSnapHelper.Snap(testX, testZ, 0f, preferredSurfaceTypeValue: 4);

            Assert.That(result, Is.EqualTo(10.18f).Within(0.05f),
                $"Expected fallback to first-hit Y≈10.18 but got {result}.");
        }

        // ── Test 3 ─────────────────────────────────────────────────────────────
        // Ball collider at mid-height must be excluded (defense in depth).

        [UnityTest]
        public IEnumerator SurfaceSnap_IgnoresBallCollider()
        {
            float testX = 502f, testZ = 500f;

            // Terrain at Y=5.
            Track(CreateFlatCollider(testX, 5f, testZ, 6f));

            // BallAnimator host — Awake sets BallAnimator.Instance.
            var ballHost = Track(new GameObject("FakeBallHost"));
            ballHost.AddComponent<BallAnimator>();

            // BoxCollider at Y=8, parented to ballHost → should be excluded.
            var sphereGO = Track(new GameObject("BallColliderProxy"));
            sphereGO.transform.position = new Vector3(testX, 8f, testZ);
            sphereGO.AddComponent<BoxCollider>().size = new Vector3(1f, 1f, 1f);
            sphereGO.transform.SetParent(ballHost.transform);

            yield return null;

            float result = PlacementSnapHelper.Snap(testX, testZ, 0f);

            // Should skip the sphere and hit terrain at Y≈5.
            Assert.That(result, Is.EqualTo(5f).Within(0.2f),
                $"Expected terrain Y≈5 but got {result}. Ball collider was not excluded.");
        }

        // ── Test 4 ─────────────────────────────────────────────────────────────
        // No geometry → returns defaultY.

        [Test]
        public void SurfaceSnap_NoHits_ReturnsDefaultY()
        {
            float result = PlacementSnapHelper.Snap(99999f, 99999f, 42f);
            Assert.That(result, Is.EqualTo(42f).Within(0.001f), "Expected defaultY=42 when no hits.");
        }

        // ── Test 5 ─────────────────────────────────────────────────────────────
        // Ball in depression: camera FollowHeightOffset should be lifted > 0.5.

        [UnityTest]
        public IEnumerator PlaceBallAt_InDepression_LiftsCamera()
        {
            float testX = 503f, testZ = 500f;

            // Surrounding rim at Y=10 (±2m from ball).
            Track(CreateFlatCollider(testX + 2f, 10f, testZ, 2f));
            Track(CreateFlatCollider(testX - 2f, 10f, testZ, 2f));
            Track(CreateFlatCollider(testX, 10f, testZ + 2f, 2f));
            Track(CreateFlatCollider(testX, 10f, testZ - 2f, 2f));

            // Bunker floor at Y=8.6 at the ball XZ (the center).
            Track(CreateFlatCollider(testX, 8.6f, testZ, 3f));

            yield return null;

            var controllerGO = Track(new GameObject("TestController5"));
            var controller   = controllerGO.AddComponent<PhysicsLabController>();
            var chaseCamGO   = Track(new GameObject("TestChaseCamera5"));
            var chaseCamera  = chaseCamGO.AddComponent<ChaseCamera>();
            chaseCamGO.AddComponent<Camera>();

            typeof(PhysicsLabController)
                .GetField("chaseCamera", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(controller, chaseCamera);

            yield return null;

            // Place ball at bunker XZ — SurfaceSnap (no preferred type match) finds Y≈8.6.
            controller.PlaceBallAt(new Vector3(testX, 8.6f, testZ));

            // AdjustCameraForDepression: surrounding Y=10, ball Y≈8.6 → depth≈1.4 > 0.5 → lifts camera.
            Assert.That(chaseCamera.FollowHeightOffset, Is.GreaterThan(0.5f),
                $"FollowHeightOffset={chaseCamera.FollowHeightOffset} — expected > 0.5 for bunker depression.");
        }

        // ── Test 6 ─────────────────────────────────────────────────────────────
        // Ball on flat ground: camera FollowHeightOffset remains ≤ 0.5.

        [UnityTest]
        public IEnumerator PlaceBallAt_OnFlatGround_DoesNotLiftCamera()
        {
            float testX = 504f, testZ = 500f;

            // Everything flat at Y=10.
            Track(CreateFlatCollider(testX,      10f, testZ,      8f));
            Track(CreateFlatCollider(testX + 2f, 10f, testZ,      2f));
            Track(CreateFlatCollider(testX - 2f, 10f, testZ,      2f));
            Track(CreateFlatCollider(testX,      10f, testZ + 2f, 2f));
            Track(CreateFlatCollider(testX,      10f, testZ - 2f, 2f));

            yield return null;

            var controllerGO = Track(new GameObject("TestController6"));
            var controller   = controllerGO.AddComponent<PhysicsLabController>();
            var chaseCamGO   = Track(new GameObject("TestChaseCamera6"));
            var chaseCamera  = chaseCamGO.AddComponent<ChaseCamera>();
            chaseCamGO.AddComponent<Camera>();

            typeof(PhysicsLabController)
                .GetField("chaseCamera", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(controller, chaseCamera);

            yield return null;

            controller.PlaceBallAt(new Vector3(testX, 10f, testZ));

            Assert.That(chaseCamera.FollowHeightOffset, Is.LessThanOrEqualTo(0.5f),
                $"FollowHeightOffset={chaseCamera.FollowHeightOffset} — expected ≤ 0.5 on flat ground.");
        }

        // ── Tests 7–9: ob_ball_in_air — never snap onto a tree ─────────────────

        /// <summary>
        /// Stand-in for a terrain fir: Unity builds tree colliders from the prototype prefab's own
        /// Collider, and the BPS firs carry a CapsuleCollider ~34 m tall (radius 0.88, centre
        /// y 17.2). Placed so it straddles the ball's raycast column above the ground.
        /// </summary>
        GameObject CreateTreeCollider(float x, float groundY, float z)
        {
            var go = new GameObject("Fir 03 (tree collider)");
            go.transform.position = new Vector3(x, groundY, z);
            var cap = go.AddComponent<CapsuleCollider>();
            cap.radius    = 0.8832352f;
            cap.height    = 34.387943f;
            cap.direction = 1;                                   // Y axis
            cap.center    = new Vector3(0f, 17.193968f, 0f);
            return Track(go);
        }

        // Test 7 — the reported bug: an OB drop (preferredSurfaceTypeValue: null) under a canopy
        // must land on the ground, NOT metres up the tree's capsule.
        [UnityTest]
        public IEnumerator SurfaceSnap_UnderTreeCanopy_SnapsToGround_NotTheTree()
        {
            EnsureReflection();
            if (_smType == null) { Assert.Ignore("SurfaceMarker type not found."); yield break; }

            float testX = 640f, testZ = 640f, groundY = 12f;

            var fairwayGO = Track(CreateFlatCollider(testX, groundY, testZ, 8f));
            AddSurfaceMarker(fairwayGO, 0);                      // Fairway
            CreateTreeCollider(testX, groundY, testZ);           // canopy overhead

            yield return null;

            // null preferred type == exactly what the OB drop path passes.
            float result = PlacementSnapHelper.Snap(testX, testZ, 0f, preferredSurfaceTypeValue: null);

            Assert.That(result, Is.EqualTo(groundY).Within(0.2f),
                $"Ball must be placed on the ground (~{groundY}), got {result}. A value up near " +
                "the capsule (~29–46) is the 'ball starts in the air' defect.");
        }

        // Test 8 — the same column with the tree only: with no playable surface at all the helper
        // must still return something sane rather than silently standing the ball on foliage.
        [UnityTest]
        public IEnumerator SurfaceSnap_TreeOnlyColumn_DoesNotReportCanopyAsSurface()
        {
            float testX = 680f, testZ = 680f, groundY = 5f;
            CreateTreeCollider(testX, groundY, testZ);

            yield return null;

            float result = PlacementSnapHelper.Snap(testX, testZ, defaultY: groundY,
                                                    preferredSurfaceTypeValue: null);

            // No SurfaceMarker and no terrain in this rig, so the last-resort branch is allowed to
            // return the only hit — but it must never be treated as a *playable surface*.
            Assert.IsFalse(
                (bool)typeof(PlacementSnapHelper)
                    .GetMethod("IsPlayableSurface", System.Reflection.BindingFlags.NonPublic
                                                  | System.Reflection.BindingFlags.Static)
                    .Invoke(null, new object[] { GameObject.Find("Fir 03 (tree collider)").GetComponent<CapsuleCollider>(), _smType }),
                "A tree capsule must never classify as a playable surface.");
            Assert.That(result, Is.Not.NaN);
        }

        // Test 9 — the fix must not disturb the normal case: nearest playable surface still wins,
        // and the result is now deterministic regardless of RaycastAll's (undefined) ordering.
        [UnityTest]
        public IEnumerator SurfaceSnap_StackedSurfaces_PicksNearestPlayableSurface()
        {
            EnsureReflection();
            if (_smType == null) { Assert.Ignore("SurfaceMarker type not found."); yield break; }

            float testX = 720f, testZ = 720f;

            var lower = Track(CreateFlatCollider(testX, 10.00f, testZ, 6f));
            AddSurfaceMarker(lower, 0);                          // Fairway, buried
            var upper = Track(CreateFlatCollider(testX, 10.30f, testZ, 6f));
            AddSurfaceMarker(upper, 0);                          // Fairway, visible top

            yield return null;

            float result = PlacementSnapHelper.Snap(testX, testZ, 0f, preferredSurfaceTypeValue: null);

            Assert.That(result, Is.GreaterThan(10.2f),
                $"Nearest (highest) playable surface must win — expected ~10.31, got {result}.");
        }
    }
}
