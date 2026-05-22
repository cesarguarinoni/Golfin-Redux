#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Golfin.Physics.Viewer;

namespace Golfin.Physics.Viewer.Editor
{
    /// <summary>
    /// One-shot scene setup for puttpath_predictor_perf_and_design task.
    ///
    /// Replaces the deleted PuttPathPredictor component on LabRoot with a new
    /// PutterGreenReader component, wires the _putterGreenReader SerializeField
    /// on PhysicsLabController, and creates a placeholder arrow material + mesh.
    ///
    /// Run: GOLFIN > Setup > Wire PutterGreenReader in LabScaffold
    ///
    /// Safe to run multiple times (idempotent — skips if already wired).
    /// </summary>
    public static class PutterGreenReaderSceneSetup
    {
        [MenuItem("GOLFIN/Setup/Wire PutterGreenReader in LabScaffold")]
        public static void WirePutterGreenReader()
        {
            var labCtrl = Object.FindObjectOfType<PhysicsLabController>();
            if (labCtrl == null)
            {
                Debug.LogError("[PutterGreenReaderSetup] PhysicsLabController not found in scene. Open LabScaffold.unity first.");
                return;
            }

            // Check if already wired.
            var existing = labCtrl.GetComponent<PutterGreenReader>();
            if (existing == null)
            {
                existing = labCtrl.gameObject.AddComponent<PutterGreenReader>();
                Debug.Log("[PutterGreenReaderSetup] Added PutterGreenReader to LabRoot.");
            }
            else
            {
                Debug.Log("[PutterGreenReaderSetup] PutterGreenReader already present on LabRoot.");
            }

            // Wire the _putterGreenReader SerializeField on PhysicsLabController via SerializedObject.
            var so = new SerializedObject(labCtrl);
            var prop = so.FindProperty("_putterGreenReader");
            if (prop != null)
            {
                if (prop.objectReferenceValue == null || prop.objectReferenceValue != existing)
                {
                    prop.objectReferenceValue = existing;
                    so.ApplyModifiedProperties();
                    EditorUtility.SetDirty(labCtrl.gameObject);
                    Debug.Log("[PutterGreenReaderSetup] Wired _putterGreenReader on PhysicsLabController.");
                }
                else
                {
                    Debug.Log("[PutterGreenReaderSetup] _putterGreenReader already wired.");
                }
            }
            else
            {
                Debug.LogWarning("[PutterGreenReaderSetup] _putterGreenReader field not found on PhysicsLabController. Recompile may be needed.");
            }

            // Wire _shotController on PutterGreenReader.
            var soReader = new SerializedObject(existing);
            var shotProp = soReader.FindProperty("_shotController");
            if (shotProp != null && shotProp.objectReferenceValue == null)
            {
                var sc = labCtrl.GetComponentInChildren<Golfin.Gameplay.Input.ShotController>(true);
                if (sc != null)
                {
                    shotProp.objectReferenceValue = sc;
                    soReader.ApplyModifiedProperties();
                    Debug.Log("[PutterGreenReaderSetup] Wired _shotController on PutterGreenReader.");
                }
            }

            // Wire _labController on PutterGreenReader.
            var labProp = soReader.FindProperty("_labController");
            if (labProp != null && labProp.objectReferenceValue == null)
            {
                labProp.objectReferenceValue = labCtrl;
                soReader.ApplyModifiedProperties();
                Debug.Log("[PutterGreenReaderSetup] Wired _labController on PutterGreenReader.");
            }

            // Wire _worldCamera on PutterGreenReader.
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
                        soReader.ApplyModifiedProperties();
                        Debug.Log("[PutterGreenReaderSetup] Wired _worldCamera on PutterGreenReader.");
                    }
                }
            }

            // Create placeholder arrow material if not already present.
            EnsureArrowMaterialExists();

            // Create placeholder arrow mesh (flat quad) if not already present.
            EnsureArrowMeshExists();

            // Wire arrow mesh + material on PutterGreenReader.
            soReader = new SerializedObject(existing); // re-fetch after ApplyModifiedProperties
            WireArrowAssets(soReader);

            // Save scene.
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());

            Debug.Log("[PutterGreenReaderSetup] Done. Save the scene to persist the wiring.");
        }

        private static void EnsureArrowMaterialExists()
        {
            const string MatPath = "Assets/Art/UI/GreenReader/MAT_GreenArrow.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(MatPath) != null) return;

            // Create a URP/Lit material with GPU instancing enabled.
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            if (mat.shader == null || mat.shader.name == "Hidden/InternalErrorShader")
            {
                // Fallback to Unlit if URP/Lit not found.
                mat = new Material(Shader.Find("Universal Render Pipeline/Unlit") ??
                                   Shader.Find("Standard") ??
                                   Shader.Find("Hidden/InternalErrorShader"));
            }

            mat.name = "MAT_GreenArrow";
            mat.enableInstancing = true;

            // Set DisableBatching tag to opt out of SRP Batcher so GPU Instancing takes effect.
            // The material must be a non-SRP-Batcher-compatible variant for RenderMeshInstanced
            // to batch all cells into one draw call. NOTE: For URP/Lit, the correct approach is
            // to use a custom shader with "DisableBatching" = "True" in the SubShader Tags.
            // For the placeholder, we note this requirement for the Architect/Cesar to verify.
            // The mat.enableInstancing flag ensures instancing is requested; SRP Batcher behavior
            // depends on the shader implementation.

            System.IO.Directory.CreateDirectory(
                System.IO.Path.GetDirectoryName(
                    Application.dataPath + "/../" + MatPath.Replace("Assets/", "")));
            AssetDatabase.CreateAsset(mat, MatPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[PutterGreenReaderSetup] Created placeholder arrow material at {MatPath}. " +
                      "Verify 'Enable GPU Instancing' is checked and SRP Batcher is opted out.");
        }

        private static void EnsureArrowMeshExists()
        {
            const string MeshPath = "Assets/Art/UI/GreenReader/MESH_GreenArrow.asset";
            if (AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath) != null) return;

            // Create a simple quad mesh (2 triangles) as placeholder.
            // Arrow texture direction: forward = +Z, backward = -Z.
            var mesh = new Mesh { name = "GreenArrowQuad" };
            mesh.vertices = new Vector3[]
            {
                new Vector3(-0.5f, 0f,  0.5f),  // top-left
                new Vector3( 0.5f, 0f,  0.5f),  // top-right
                new Vector3( 0.5f, 0f, -0.5f),  // bottom-right
                new Vector3(-0.5f, 0f, -0.5f),  // bottom-left
            };
            mesh.uv = new Vector2[]
            {
                new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(1, 0), new Vector2(0, 0),
            };
            mesh.triangles = new int[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            AssetDatabase.CreateAsset(mesh, MeshPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[PutterGreenReaderSetup] Created placeholder arrow mesh at {MeshPath}.");
        }

        private static void WireArrowAssets(SerializedObject soReader)
        {
            const string MatPath  = "Assets/Art/UI/GreenReader/MAT_GreenArrow.mat";
            const string MeshPath = "Assets/Art/UI/GreenReader/MESH_GreenArrow.asset";

            var mat  = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);

            var meshProp = soReader.FindProperty("_arrowMesh");
            if (meshProp != null && mesh != null && meshProp.objectReferenceValue == null)
            {
                meshProp.objectReferenceValue = mesh;
                soReader.ApplyModifiedProperties();
                Debug.Log("[PutterGreenReaderSetup] Wired _arrowMesh on PutterGreenReader.");
            }

            var matProp = soReader.FindProperty("_arrowMaterial");
            if (matProp != null && mat != null && matProp.objectReferenceValue == null)
            {
                matProp.objectReferenceValue = mat;
                soReader.ApplyModifiedProperties();
                Debug.Log("[PutterGreenReaderSetup] Wired _arrowMaterial on PutterGreenReader.");
            }
        }
    }
}
#endif
