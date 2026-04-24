using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Golfin.Physics.Viewer;

namespace Golfin.Physics.Tests
{
    /// <summary>
    /// F-Hotfix.D tests 7-9: PlacementEntries build/clear lifecycle.
    /// Uses reflection to create SurfaceMarker components (Assembly-CSharp not directly referenceable).
    /// </summary>
    public class PlacementEntriesTests
    {
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

        readonly List<GameObject> _created = new List<GameObject>();
        GameObject Track(GameObject go) { _created.Add(go); return go; }

        [TearDown]
        public void Cleanup()
        {
            foreach (var go in _created)
                if (go != null) Object.DestroyImmediate(go);
            _created.Clear();
        }

        GameObject MakeSurfaceMarkerGO(string name, int typeValue, Vector3 pos)
        {
            EnsureReflection();
            var go = Track(new GameObject(name));
            go.transform.position = pos;
            if (_smType != null && _stField != null)
            {
                var marker = go.AddComponent(_smType) as Component;
                _stField.SetValue(marker, System.Enum.ToObject(_stField.FieldType, typeValue));
            }
            return go;
        }

        // ── Test 7 ─────────────────────────────────────────────────────────────
        // OnHoleLoaded populates all categories.

        [UnityTest]
        public IEnumerator BuildPlacementEntries_OnHoleLoad_PopulatesAllCategories()
        {
            EnsureReflection();
            if (_smType == null) { Assert.Ignore("SurfaceMarker type not found."); yield break; }

            // Build a fake scene by creating GOs with SurfaceMarkers.
            // PhysicsLabController.OnHoleLoaded scans SceneManager.GetSceneByName — we call the
            // internal BuildPlacementEntries path indirectly by populating via reflection and calling
            // the public OnHoleLoaded with a stub scene name.
            //
            // Instead, test BuildPlacementEntries by calling it via reflection on a fresh controller.

            var controllerGO = Track(new GameObject("TestController7"));
            var controller   = controllerGO.AddComponent<PhysicsLabController>();

            yield return null;

            // Populate lists and invoke BuildPlacementEntries via reflection.
            var teePos    = new Vector3(10f, 0f, 10f);
            var greenGOs  = new List<GameObject> { MakeSurfaceMarkerGO("Green1",   1, new Vector3(0,0,50)) };
            var bunkerGOs = new List<GameObject>
            {
                MakeSurfaceMarkerGO("Bunker1", 4, new Vector3(0,0,20)),
                MakeSurfaceMarkerGO("Bunker2", 4, new Vector3(0,0,25))
            };
            var fairwayGOs = new List<GameObject> { MakeSurfaceMarkerGO("Fairway1", 0, new Vector3(0,0,30)) };
            var waterGOs   = new List<GameObject> { MakeSurfaceMarkerGO("Water1",   5, new Vector3(0,0,5)) };

            var method = typeof(PhysicsLabController).GetMethod("BuildPlacementEntries",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            Assert.IsNotNull(method, "BuildPlacementEntries method not found via reflection.");

            method.Invoke(controller, new object[] { true, teePos, greenGOs, bunkerGOs, fairwayGOs, waterGOs });

            // Expected: 1 Tee + 1 Green + 2 Bunker + 1 Fairway + 1 Water = 6 entries.
            Assert.AreEqual(6, controller.PlacementEntries.Count,
                $"Expected 6 entries but got {controller.PlacementEntries.Count}: " +
                string.Join(", ", controller.PlacementEntries.ConvertAll(e => e.Label)));

            // Verify each category is represented.
            bool hasTee     = controller.PlacementEntries.Exists(e => e.Label.StartsWith("Tee"));
            bool hasGreen   = controller.PlacementEntries.Exists(e => e.Label.StartsWith("Green"));
            bool hasBunker  = controller.PlacementEntries.Exists(e => e.Label.StartsWith("Bunker"));
            bool hasFairway = controller.PlacementEntries.Exists(e => e.Label.StartsWith("Fairway"));
            bool hasWater   = controller.PlacementEntries.Exists(e => e.Label.StartsWith("Near Water"));

            Assert.IsTrue(hasTee,     "Missing Tee entry.");
            Assert.IsTrue(hasGreen,   "Missing Green entry.");
            Assert.IsTrue(hasBunker,  "Missing Bunker entry.");
            Assert.IsTrue(hasFairway, "Missing Fairway entry.");
            Assert.IsTrue(hasWater,   "Missing Near Water entry.");
        }

        // ── Test 8 ─────────────────────────────────────────────────────────────
        // OnHoleUnloaded clears all entries.

        [UnityTest]
        public IEnumerator BuildPlacementEntries_OnHoleUnload_Clears()
        {
            EnsureReflection();

            var controllerGO = Track(new GameObject("TestController8"));
            var controller   = controllerGO.AddComponent<PhysicsLabController>();

            yield return null;

            // Seed some entries via reflection.
            var teePos     = new Vector3(10f, 0f, 10f);
            var greenGOs   = new List<GameObject> { MakeSurfaceMarkerGO("G1", 1, Vector3.zero) };
            var empty      = new List<GameObject>();

            var method = typeof(PhysicsLabController).GetMethod("BuildPlacementEntries",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (method == null) { Assert.Ignore("BuildPlacementEntries not found."); yield break; }

            method.Invoke(controller, new object[] { true, teePos, greenGOs, empty, empty, empty });

            Assert.Greater(controller.PlacementEntries.Count, 0, "Should have entries after hole load.");

            controller.OnHoleUnloaded();

            Assert.AreEqual(0, controller.PlacementEntries.Count,
                "PlacementEntries must be empty after OnHoleUnloaded.");
        }

        // ── Test 9 ─────────────────────────────────────────────────────────────
        // Multiple GOs of the same surface type produce unique labeled entries.

        [UnityTest]
        public IEnumerator BuildPlacementEntries_MultipleGOsSameType_ProducesUniqueLabels()
        {
            EnsureReflection();

            var controllerGO = Track(new GameObject("TestController9"));
            var controller   = controllerGO.AddComponent<PhysicsLabController>();

            yield return null;

            var teePos     = Vector3.zero;
            var bunkerGOs  = new List<GameObject>
            {
                MakeSurfaceMarkerGO("BunkerA", 4, new Vector3(0,0,20)),
                MakeSurfaceMarkerGO("BunkerA", 4, new Vector3(0,0,25)), // same name, same type
            };
            var empty = new List<GameObject>();

            var method = typeof(PhysicsLabController).GetMethod("BuildPlacementEntries",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (method == null) { Assert.Ignore("BuildPlacementEntries not found."); yield break; }

            method.Invoke(controller, new object[] { false, teePos, empty, bunkerGOs, empty, empty });

            Assert.AreEqual(2, controller.PlacementEntries.Count,
                "Expected 2 bunker entries.");

            string label0 = controller.PlacementEntries[0].Label;
            string label1 = controller.PlacementEntries[1].Label;
            Assert.AreNotEqual(label0, label1,
                $"Labels must be unique but both are '{label0}'.");
        }
    }
}
