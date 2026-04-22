#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Golfin.SceneSnapshot;

namespace Golfin.SceneSnapshot.Tests
{
    [TestFixture]
    public class SceneSnapshotTests
    {
        private List<string> _tempFiles = new List<string>();
        private List<Scene> _tempScenes = new List<Scene>();

        [TearDown]
        public void TearDown()
        {
            foreach (var path in _tempFiles)
            {
                // Asset paths (Assets/...) must be deleted via AssetDatabase; absolute paths via File
                if (path.StartsWith("Assets/"))
                    AssetDatabase.DeleteAsset(path);
                else if (File.Exists(path))
                    File.Delete(path);
            }
            _tempFiles.Clear();

            foreach (var scene in _tempScenes)
                if (scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            _tempScenes.Clear();
        }

        // ── 1. Empty scene ────────────────────────────────────────────

        [Test]
        public void Snapshot_Capture_EmptySceneProducesEmptySnapshot()
        {
            var scene = NewScene();
            SceneSnapshotData snap = SceneSnapshotCapture.Capture(scene, null);

            Assert.AreEqual(0, snap.Props.Count, "Empty scene should have no props");
            Assert.IsNull(snap.Terrain, "Empty scene should have no terrain snapshot");
        }

        // ── 2. Stamps GUIDs ───────────────────────────────────────────

        [Test]
        public void Snapshot_Capture_StampsGuidsOnManualProps()
        {
            var scene = NewScene();

            var root = CreateGO("ManualRoot", scene);
            var cube1 = CreateGO("Cube1", scene); cube1.transform.SetParent(root.transform);
            var cube2 = CreateGO("Cube2", scene); cube2.transform.SetParent(root.transform);

            Assert.IsNull(cube1.GetComponent<ManualPropId>(), "No ManualPropId before capture");

            SceneSnapshotCapture.Capture(scene, null);

            ManualPropId id1 = cube1.GetComponent<ManualPropId>();
            ManualPropId id2 = cube2.GetComponent<ManualPropId>();

            Assert.IsNotNull(id1, "Cube1 should have ManualPropId after capture");
            Assert.IsNotNull(id2, "Cube2 should have ManualPropId after capture");
            Assert.IsFalse(string.IsNullOrEmpty(id1.Guid), "Cube1 GUID should not be empty");
            Assert.IsFalse(string.IsNullOrEmpty(id2.Guid), "Cube2 GUID should not be empty");
            Assert.AreNotEqual(id1.Guid, id2.Guid, "GUIDs must be distinct");
        }

        // ── 3. Skips importer roots ───────────────────────────────────

        [Test]
        public void Snapshot_Capture_SkipsImporterRoots()
        {
            var scene = NewScene();
            CreateGO("Hole_01_Geo", scene);   // should be skipped
            CreateGO("MyBridge", scene);       // should be captured

            SceneSnapshotData snap = SceneSnapshotCapture.Capture(scene, null);

            Assert.IsTrue(snap.Props.Count >= 1, "At least MyBridge should appear in props");
            foreach (var p in snap.Props)
                Assert.AreNotEqual("Hole_01_Geo", p.Name, "Importer root must not be in props");
        }

        // ── 4. Restore updates existing transform ────────────────────

        [Test]
        public void Snapshot_Restore_UpdatesExistingPropTransform()
        {
            var scene = NewScene();
            var cube = CreateGO("MyCube", scene);
            cube.transform.position = Vector3.zero;

            SceneSnapshotData snap = SceneSnapshotCapture.Capture(scene, null);

            // Move cube
            cube.transform.position = new Vector3(10, 0, 0);

            RestoreReport report = SceneSnapshotRestore.Restore(scene, snap);

            Assert.AreEqual(0, report.Created.Count, "No new objects should be created");
            Assert.AreEqual(1, report.Updated.Count, "Cube should be updated");
            Assert.That(cube.transform.localPosition.x, Is.EqualTo(0f).Within(0.001f),
                "Cube should be back at x=0");
        }

        // ── 5. Restore adds missing prop from prefab ─────────────────

        [Test]
        public void Snapshot_Restore_AddsMissingPropFromPrefab()
        {
            var scene = NewScene();

            string tempPrefabPath = "Assets/SceneSnapshotTest_TempPrefab.prefab";
            var go = new GameObject("TempProp");
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, tempPrefabPath);
            Object.DestroyImmediate(go);
            _tempFiles.Add(tempPrefabPath);

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.transform.position = new Vector3(5, 2, 1);

            SceneSnapshotData snap = SceneSnapshotCapture.Capture(scene, null);
            string capturedGuid = instance.GetComponent<ManualPropId>().Guid;

            Object.DestroyImmediate(instance);

            RestoreReport report = SceneSnapshotRestore.Restore(scene, snap);

            Assert.AreEqual(1, report.Created.Count, "One prefab should be re-instantiated");
            Assert.AreEqual(0, report.Failed.Count, "No failures");

            GameObject restored = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                ManualPropId id = root.GetComponent<ManualPropId>();
                if (id != null && id.Guid == capturedGuid) { restored = root; break; }
            }
            Assert.IsNotNull(restored, "Restored object should exist in scene");
            Assert.That(restored.transform.localPosition.x, Is.EqualTo(5f).Within(0.001f));
        }

        // ── 6. Restore leaves new objects alone ──────────────────────

        [Test]
        public void Snapshot_Restore_LeavesNewObjectsAlone()
        {
            var scene = NewScene();
            CreateGO("OriginalCube", scene);

            SceneSnapshotData snap = SceneSnapshotCapture.Capture(scene, null);

            CreateGO("NewCube", scene);

            SceneSnapshotRestore.Restore(scene, snap);

            bool found = false;
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == "NewCube") { found = true; break; }
            Assert.IsTrue(found, "NewCube added after capture should still be in scene after restore");
        }

        // ── 7. JSON round-trip ────────────────────────────────────────

        [Test]
        public void Snapshot_RoundTrip_JsonReadable()
        {
            var scene = NewScene();
            CreateGO("Prop_A", scene);
            CreateGO("Prop_B", scene);
            CreateGO("Prop_C", scene);

            SceneSnapshotData snap = SceneSnapshotCapture.Capture(scene, null);
            Assert.AreEqual(3, snap.Props.Count, "3 props before serialize");

            string json = JsonUtility.ToJson(snap, true);
            Assert.IsFalse(string.IsNullOrEmpty(json), "JSON must not be empty");

            SceneSnapshotData snap2 = JsonUtility.FromJson<SceneSnapshotData>(json);
            Assert.IsNotNull(snap2, "Deserialized snapshot must not be null");
            Assert.AreEqual(3, snap2.Props.Count, "Prop count must survive JSON round-trip");

            for (int i = 0; i < snap.Props.Count; i++)
                Assert.AreEqual(snap.Props[i].Guid, snap2.Props[i].Guid,
                    $"GUID {i} must match after round-trip");
        }

        // ── 8. Terrain tree instances round-trip ─────────────────────

        [Test]
        public void Snapshot_Terrain_TreeInstancesRoundTrip()
        {
            var scene = NewScene();

            var terrainData = new TerrainData();
            terrainData.heightmapResolution = 33;
            terrainData.size = new Vector3(100, 10, 100);

            string tempPrefabPath = "Assets/SceneSnapshotTest_TreePrefab.prefab";
            var treePrefabGo = new GameObject("TreeProto");
            GameObject treePrefab = PrefabUtility.SaveAsPrefabAsset(treePrefabGo, tempPrefabPath);
            Object.DestroyImmediate(treePrefabGo);
            _tempFiles.Add(tempPrefabPath);

            terrainData.treePrototypes = new[] { new TreePrototype { prefab = treePrefab } };

            terrainData.SetTreeInstances(new[]
            {
                new TreeInstance { position = new Vector3(0.1f, 0f, 0.1f), prototypeIndex = 0, widthScale = 1f, heightScale = 1f },
                new TreeInstance { position = new Vector3(0.5f, 0f, 0.5f), prototypeIndex = 0, widthScale = 1f, heightScale = 1f },
                new TreeInstance { position = new Vector3(0.9f, 0f, 0.9f), prototypeIndex = 0, widthScale = 1f, heightScale = 1f }
            }, false);

            var terrainGO = Terrain.CreateTerrainGameObject(terrainData);
            SceneManager.MoveGameObjectToScene(terrainGO, scene);

            SceneSnapshotData snap = SceneSnapshotCapture.Capture(scene, null);

            Assert.IsNotNull(snap.Terrain, "Terrain snapshot should exist");
            Assert.AreEqual(3, snap.Terrain.TreeInstances.Count, "Should capture 3 tree instances");

            // Clear trees
            terrainData.SetTreeInstances(new TreeInstance[0], false);
            Assert.AreEqual(0, terrainData.treeInstanceCount, "Trees cleared");

            SceneSnapshotRestore.Restore(scene, snap);

            Assert.AreEqual(3, terrainData.treeInstanceCount, "3 tree instances restored");

            TreeInstance[] restored = terrainData.treeInstances;
            Assert.That(restored[0].position.x, Is.EqualTo(0.1f).Within(1e-5f));
            Assert.That(restored[1].position.x, Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(restored[2].position.x, Is.EqualTo(0.9f).Within(1e-5f));

            Object.DestroyImmediate(terrainGO);
            Object.DestroyImmediate(terrainData);
        }

        // ─── Helpers ─────────────────────────────────────────────────

        private Scene NewScene()
        {
            // Unity refuses to create an additive scene when any open scene is untitled (unsaved path).
            // Save all unsaved open scenes to temp paths first.
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene s = SceneManager.GetSceneAt(i);
                if (string.IsNullOrEmpty(s.path))
                {
                    string tempPath = "Assets/SnapshotTest_TempScene_" + i + ".unity";
                    EditorSceneManager.SaveScene(s, tempPath);
                    _tempFiles.Add(tempPath);
                }
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            _tempScenes.Add(scene);
            return scene;
        }

        private static GameObject CreateGO(string name, Scene scene)
        {
            var go = new GameObject(name);
            SceneManager.MoveGameObjectToScene(go, scene);
            return go;
        }
    }
}
#endif
