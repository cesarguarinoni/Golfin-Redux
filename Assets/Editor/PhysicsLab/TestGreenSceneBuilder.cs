// TestGreenSceneBuilder.cs — Editor utility to create PhysicsLab_TestGreen.unity.
//
// Menu: Window/Golfin/Build PhysicsLab_TestGreen Scene
//
// Creates a new scene at Assets/Scenes/Physics/PhysicsLab_TestGreen.unity containing:
//   - Standard lighting (Directional Light + ambient)
//   - PhysicsLabController (cloned setup from PhysicsLab_Hole1)
//   - A sculpted green mesh using TestGreen_25x25.asset
//   - Camera positioned 3/4 overhead (15m height, 45° down-angle)
//   - BakedZoneClassifier configured for the green footprint
//
// SPEC: puttpath_predictor_perf_and_design §Architecture §§ Test green.
#if UNITY_EDITOR
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Golfin.Physics;          // SurfaceType
using Golfin.Physics.Viewer;
using Golfin.Physics.Runtime.Baked;
using Golfin.Physics.Runtime;

namespace Golfin.Physics.Editor
{
    public static class TestGreenSceneBuilder
    {
        private const string ScenePath    = "Assets/Scenes/Physics/PhysicsLab_TestGreen.unity";
        private const string MeshPath     = "Assets/Meshes/TestGreen_25x25.asset";
        private const string GridMatPath  = "Assets/Materials/PutterGreenGrid.mat";

        [MenuItem("Window/Golfin/Build PhysicsLab_TestGreen Scene")]
        public static void BuildScene()
        {
            // Ensure the test green mesh exists first.
            if (AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath) == null)
            {
                Debug.Log("[TestGreenSceneBuilder] Mesh not found — building TestGreen_25x25 first...");
                TestGreenMeshBuilder.BuildAndSave();
            }

            // Create new scene.
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "PhysicsLab_TestGreen";

            // ── Lighting ─────────────────────────────────────────────────────

            // Directional light (sunlight).
            var lightGo = new GameObject("Directional Light");
            var light   = lightGo.AddComponent<Light>();
            light.type      = LightType.Directional;
            light.intensity = 1.2f;
            light.color     = new Color(1f, 0.95f, 0.85f);
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            lightGo.transform.position = new Vector3(0f, 10f, 0f);

            // ── Camera ───────────────────────────────────────────────────────

            // 3/4 overhead view of a 25×25m green centered at (12.5, ?, 12.5).
            var camGo  = new GameObject("Main Camera");
            var cam    = camGo.AddComponent<Camera>();
            cam.clearFlags           = CameraClearFlags.SolidColor;
            cam.backgroundColor      = new Color(0.1f, 0.2f, 0.1f, 1f);
            cam.fieldOfView          = 60f;
            cam.nearClipPlane        = 0.1f;
            cam.farClipPlane         = 200f;

            // Position: 15m high, offset -10m in Z to look at center.
            // Look-at: (12.5, 0, 12.5) — centre of the 25m green.
            Vector3 greenCenter  = new Vector3(12.5f, 0f, 12.5f);
            Vector3 camOffset    = new Vector3(0f, 15f, -10f);
            camGo.transform.position = greenCenter + camOffset;
            camGo.transform.LookAt(greenCenter);

            camGo.tag = "MainCamera";

            // ── Green mesh GO ─────────────────────────────────────────────────

            var greenMesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            if (greenMesh == null)
            {
                Debug.LogError($"[TestGreenSceneBuilder] TestGreen_25x25.asset not found at {MeshPath}. Scene created without green mesh.");
            }

            var greenGo  = new GameObject("TestGreenMesh");
            var mf       = greenGo.AddComponent<MeshFilter>();
            var mr       = greenGo.AddComponent<MeshRenderer>();

            if (greenMesh != null)
                mf.sharedMesh = greenMesh;

            // Use a simple green material for the grass surface.
            var greenMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ??
                                        Shader.Find("Standard"));
            greenMat.name  = "TestGreenGrass";
            greenMat.color = new Color(0.15f, 0.55f, 0.12f);
            mr.sharedMaterial = greenMat;

            // SurfaceMarker — marks this mesh as a Green surface for BakedZoneClassifier.
            // The classifier will scan this mesh and classify it as SurfaceType.Green.
            var marker   = greenGo.AddComponent<SurfaceMarker>();
            marker.Type  = SurfaceType.Green;
            Debug.Log("[TestGreenSceneBuilder] SurfaceMarker.Type = Green set on TestGreenMesh.");

            // ── PhysicsLabController root ─────────────────────────────────────

            // We copy the pattern from LabScaffold: a LabRoot GO that holds PhysicsLabController.
            // For the TestGreen scene, we don't need the full gameplay loop — just:
            //   - PhysicsLabController (to hold the PutterGreenReader + classifier reference)
            //   - BakedZoneClassifier (set up by the controller at play time)
            //   - PutterGreenReader (wired to the controller)
            //
            // NOTE: In the real LabScaffold, PhysicsLabController is a complex component that
            // loads zones and creates BakedZoneClassifier in Start(). For the TestGreen scene
            // to work, the PhysicsLabController must be able to find the SurfaceMarker-tagged
            // green mesh and run the bake step.
            //
            // A simpler alternative: add a custom GreenLabSetup script that directly
            // creates a BakedZoneClassifier from the mesh at Awake() and sets it on a
            // PutterGreenReader. This avoids the full PhysicsLabController dependency.
            // SPEC §Architecture §§ Test green says "standard PhysicsLabController" —
            // so we use that path and rely on the normal bake flow.

            var labRoot = new GameObject("LabRoot");
            var labCtrl = labRoot.AddComponent<PhysicsLabController>();

            // PutterGreenReader on the same GO.
            var greenReader    = labRoot.AddComponent<PutterGreenReader>();
            // TestGreenLabSetup drives the bake directly from zones.json (no Geo scene needed).
            var testGreenSetup = labRoot.AddComponent<TestGreenLabSetup>();

            // Wire _gridMaterial.
            var soReader = new SerializedObject(greenReader);
            var matProp  = soReader.FindProperty("_gridMaterial");
            if (matProp != null)
            {
                var gridMat = AssetDatabase.LoadAssetAtPath<Material>(GridMatPath);
                if (gridMat != null)
                {
                    matProp.objectReferenceValue = gridMat;
                    Debug.Log("[TestGreenSceneBuilder] Wired PutterGreenGrid.mat on PutterGreenReader.");
                }
                else
                {
                    Debug.LogWarning($"[TestGreenSceneBuilder] PutterGreenGrid.mat not found at {GridMatPath}. " +
                                     "Import the shader and create the material first.");
                }
            }

            // Wire _labController.
            var labProp = soReader.FindProperty("_labController");
            if (labProp != null)
            {
                labProp.objectReferenceValue = labCtrl;
            }

            // Wire _greenReader on TestGreenLabSetup.
            var soSetup      = new SerializedObject(testGreenSetup);
            var setupRdrProp = soSetup.FindProperty("_greenReader");
            if (setupRdrProp != null)
            {
                setupRdrProp.objectReferenceValue = greenReader;
                soSetup.ApplyModifiedProperties();
                Debug.Log("[TestGreenSceneBuilder] Wired _greenReader on TestGreenLabSetup.");
            }

            soReader.ApplyModifiedProperties();

            // ── Save scene ────────────────────────────────────────────────────

            string dir = Path.GetDirectoryName(ScenePath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();
            Debug.Log($"[TestGreenSceneBuilder] Saved scene to {ScenePath}");
        }
    }
}
#endif
