// PutterGreenReaderSceneSetup.cs — Editor utility for wiring PutterGreenReader.
//
// ITER-2 REVISED: Arrow material/mesh wiring removed. PutterGreenReader now uses
// a procedural mesh + PutterGreenGrid material instead of arrow instancing.
//
// Run: GOLFIN > Setup > Wire PutterGreenReader in LabScaffold
// Also: GOLFIN > Setup > Wire PutterGreenReader in PhysicsLab_TestGreen
//
// Safe to run multiple times (idempotent — skips if already wired).
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Golfin.Physics.Viewer;

namespace Golfin.Physics.Viewer.Editor
{
    public static class PutterGreenReaderSceneSetup
    {
        private const string GridMatPath = "Assets/Materials/PutterGreenGrid.mat";

        [MenuItem("GOLFIN/Setup/Wire PutterGreenReader in LabScaffold")]
        public static void WirePutterGreenReader()
            => WireInCurrentScene();

        [MenuItem("GOLFIN/Setup/Wire PutterGreenReader in PhysicsLab_TestGreen")]
        public static void WirePutterGreenReaderTestGreen()
            => WireInCurrentScene();

        public static PutterGreenReader WireInCurrentScene()
        {
            var labCtrl = Object.FindObjectOfType<PhysicsLabController>();
            if (labCtrl == null)
            {
                Debug.LogError("[PutterGreenReaderSetup] PhysicsLabController not found in scene. Open the target scene first.");
                return null;
            }

            // Ensure PutterGreenReader exists on the LabController GO.
            var reader = labCtrl.GetComponent<PutterGreenReader>();
            if (reader == null)
            {
                reader = labCtrl.gameObject.AddComponent<PutterGreenReader>();
                Debug.Log("[PutterGreenReaderSetup] Added PutterGreenReader to LabRoot.");
            }
            else
            {
                Debug.Log("[PutterGreenReaderSetup] PutterGreenReader already present on LabRoot.");
            }

            // Wire _putterGreenReader on PhysicsLabController.
            var soCtrl = new SerializedObject(labCtrl);
            var readerProp = soCtrl.FindProperty("_putterGreenReader");
            if (readerProp != null && readerProp.objectReferenceValue != (Object)reader)
            {
                readerProp.objectReferenceValue = reader;
                soCtrl.ApplyModifiedProperties();
                EditorUtility.SetDirty(labCtrl.gameObject);
                Debug.Log("[PutterGreenReaderSetup] Wired _putterGreenReader on PhysicsLabController.");
            }

            // Wire SerializeFields on PutterGreenReader.
            var soReader = new SerializedObject(reader);

            // _shotController
            var shotProp = soReader.FindProperty("_shotController");
            if (shotProp != null && shotProp.objectReferenceValue == null)
            {
                var sc = labCtrl.GetComponentInChildren<Golfin.Gameplay.Input.ShotController>(true);
                if (sc != null)
                {
                    shotProp.objectReferenceValue = sc;
                    Debug.Log("[PutterGreenReaderSetup] Wired _shotController on PutterGreenReader.");
                }
            }

            // _labController
            var labProp = soReader.FindProperty("_labController");
            if (labProp != null && labProp.objectReferenceValue == null)
            {
                labProp.objectReferenceValue = labCtrl;
                Debug.Log("[PutterGreenReaderSetup] Wired _labController on PutterGreenReader.");
            }

            // _worldCamera
            var camProp = soReader.FindProperty("_worldCamera");
            if (camProp != null && camProp.objectReferenceValue == null)
            {
                var chaseCamera = labCtrl.GetComponentInChildren<ChaseCamera>(true);
                if (chaseCamera != null)
                {
                    var cam = chaseCamera.GetComponent<Camera>();
                    if (cam != null)
                    {
                        camProp.objectReferenceValue = cam;
                        Debug.Log("[PutterGreenReaderSetup] Wired _worldCamera on PutterGreenReader.");
                    }
                }
            }

            // _gridMaterial — PutterGreenGrid.mat (iter-2 warped wireframe shader).
            var matProp = soReader.FindProperty("_gridMaterial");
            if (matProp != null && matProp.objectReferenceValue == null)
            {
                var gridMat = AssetDatabase.LoadAssetAtPath<Material>(GridMatPath);
                if (gridMat != null)
                {
                    matProp.objectReferenceValue = gridMat;
                    Debug.Log($"[PutterGreenReaderSetup] Wired _gridMaterial ({GridMatPath}) on PutterGreenReader.");
                }
                else
                {
                    Debug.LogWarning($"[PutterGreenReaderSetup] Grid material not found at {GridMatPath}. " +
                                     "Create it first via the Unity MCP or import the asset.");
                }
            }

            soReader.ApplyModifiedProperties();

            // Mark scene dirty.
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());

            Debug.Log("[PutterGreenReaderSetup] Done. Save the scene to persist the wiring.");
            return reader;
        }
    }
}
#endif
