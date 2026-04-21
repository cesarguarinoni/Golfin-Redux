using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Golfin.Physics.Runtime;

namespace Golfin.Physics.Viewer.Editor
{
    /// <summary>
    /// Builds the three PhysicsLab scenes via GOLFIN > Physics Lab > Build Scenes.
    /// Re-running is safe — overwrites existing scenes.
    /// </summary>
    public static class PhysicsLabSceneBuilder
    {
        const string SceneRoot  = "Assets/Scenes/Physics";
        const string BallPrefab = "Assets/Art/3D/Balls/Common/Prefabs/Pf_GOLFIN_Ball.prefab";
        const string HeightmapAsset = "Assets/Golf/Courses/lomond-country-club/Data/hole-01-geo/heightmap.bytes";

        // ── Menu ──────────────────────────────────────────────────────────────

        [MenuItem("GOLFIN/Physics Lab/Build All Scenes")]
        public static void BuildAllScenes()
        {
            // Prompt save first
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Directory.CreateDirectory(SceneRoot);

            BuildRangeScene();
            BuildHole1Scene();
            BuildDashboardScene();

            AssetDatabase.Refresh();
            Debug.Log("[PhysicsLabSceneBuilder] All three lab scenes created.");
        }

        [MenuItem("GOLFIN/Physics Lab/Build Range Scene")]
        public static void BuildRangeScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            AddDirectionalLight();
            var (labRoot, traj, anim, cam) = AddLabCore(PresetScene.Range);
            AddCanvas_Lab(labRoot, PresetScene.Range);
            AddGroundPlane(2000f, 200f, new Color(0.18f, 0.35f, 0.14f));
            AddRulerMarkers();
            WireCamera(cam, labRoot, traj, anim);

            var path = $"{SceneRoot}/PhysicsLab_Range.unity";
            EditorSceneManager.SaveScene(scene, path);
            Debug.Log($"[PhysicsLabSceneBuilder] Saved {path}");
        }

        [MenuItem("GOLFIN/Physics Lab/Build Hole1 Scene")]
        public static void BuildHole1Scene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            AddDirectionalLight();
            var (labRoot, traj, anim, cam) = AddLabCore(PresetScene.Hole1);
            AddCanvas_Lab(labRoot, PresetScene.Hole1);

            // HeightProvider for actual terrain sampling
            var heightGO = new GameObject("HeightProvider");
            var heightProv = heightGO.AddComponent<HeightProvider>();
            var hmAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(HeightmapAsset);
            if (hmAsset != null)
                SetSerializedField(heightProv, "heightmapAsset", hmAsset);
            else
                Debug.LogWarning($"[PhysicsLabSceneBuilder] Heightmap not found at {HeightmapAsset}. Assign manually.");

            // Wire HeightProvider into controller
            var ctrl = labRoot.GetComponent<PhysicsLabController>();
            if (ctrl != null)
                SetSerializedField(ctrl, "heightProvider", heightProv.GetComponent<HeightProvider>());

            WireCamera(cam, labRoot, traj, anim);

            var path = $"{SceneRoot}/PhysicsLab_Hole1.unity";
            EditorSceneManager.SaveScene(scene, path);
            Debug.Log($"[PhysicsLabSceneBuilder] Saved {path}");
        }

        [MenuItem("GOLFIN/Physics Lab/Build Dashboard Scene")]
        public static void BuildDashboardScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            AddDirectionalLight();
            var (labRoot, traj, anim, cam) = AddLabCore(PresetScene.Dashboard);
            AddCanvas_Dashboard(labRoot);
            AddGroundPlane(200f, 50f, new Color(0.18f, 0.35f, 0.14f));
            WireCamera(cam, labRoot, traj, anim);

            var path = $"{SceneRoot}/PhysicsLab_Dashboard.unity";
            EditorSceneManager.SaveScene(scene, path);
            Debug.Log($"[PhysicsLabSceneBuilder] Saved {path}");
        }

        // ── Scene building helpers ─────────────────────────────────────────────

        static void AddDirectionalLight()
        {
            var go = new GameObject("Directional Light");
            go.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            var light = go.AddComponent<Light>();
            light.type      = LightType.Directional;
            light.intensity = 1.1f;
            light.color     = new Color(1f, 0.96f, 0.88f);
        }

        static (GameObject labRoot, TrajectoryRenderer traj, BallAnimator anim, Camera cam)
            AddLabCore(PresetScene scene)
        {
            // LabRoot
            var labRoot = new GameObject("LabRoot");
            var ctrl = labRoot.AddComponent<PhysicsLabController>();
            SetSerializedField(ctrl, "currentScene", (int)scene);

            // TrajectoryRenderer
            var trajGO = new GameObject("TrajectoryRenderer");
            trajGO.transform.SetParent(labRoot.transform);
            var lr   = trajGO.AddComponent<LineRenderer>();
            lr.useWorldSpace    = true;
            lr.startWidth       = 0.08f;
            lr.endWidth         = 0.08f;
            lr.material         = new Material(Shader.Find("Sprites/Default"));
            var traj = trajGO.AddComponent<TrajectoryRenderer>();

            // BallAnimator
            var animGO = new GameObject("BallAnimator");
            animGO.transform.SetParent(labRoot.transform);
            var anim = animGO.AddComponent<BallAnimator>();
            var ballPrefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(BallPrefab);
            if (ballPrefabAsset != null)
                SetSerializedField(anim, "ballPrefab", ballPrefabAsset);
            else
                Debug.LogWarning($"[PhysicsLabSceneBuilder] Ball prefab not found at {BallPrefab}.");

            // Wire controller refs
            SetSerializedField(ctrl, "trajectoryRenderer", traj);
            SetSerializedField(ctrl, "ballAnimator",       anim);

            // Camera
            var camGO = new GameObject("Main Camera");
            camGO.tag = "MainCamera";
            camGO.transform.position = new Vector3(0f, 5f, -15f);
            camGO.transform.rotation = Quaternion.Euler(15f, 0f, 0f);
            var cam     = camGO.AddComponent<Camera>();
            cam.farClipPlane  = 3000f;
            cam.fieldOfView   = 60f;
            var chaseCam = camGO.AddComponent<ChaseCamera>();

            SetSerializedField(ctrl, "chaseCamera", chaseCam);

            return (labRoot, traj, anim, cam);
        }

        static void AddCanvas_Lab(GameObject labRoot, PresetScene scene)
        {
            var canvasGO = new GameObject("LabCanvas");
            var canvas   = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            canvasGO.AddComponent<CanvasScaler>();
            canvasGO.AddComponent<GraphicRaycaster>();

            var ui = canvasGO.AddComponent<PhysicsLabUI>();
            var ctrl = labRoot.GetComponent<PhysicsLabController>();
            SetSerializedField(ui, "controller", ctrl);
            SetSerializedField(ui, "initialScene", (int)scene);
        }

        static void AddCanvas_Dashboard(GameObject labRoot)
        {
            var canvasGO = new GameObject("DashboardCanvas");
            var canvas   = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            canvasGO.AddComponent<CanvasScaler>();
            canvasGO.AddComponent<GraphicRaycaster>();

            var ui = canvasGO.AddComponent<DashboardUI>();
            var ctrl = labRoot.GetComponent<PhysicsLabController>();
            SetSerializedField(ui, "controller", ctrl);
        }

        static void AddGroundPlane(float length, float width, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Plane);
            go.name = "GroundPlane";
            go.transform.position   = new Vector3(length * 0.5f, -0.01f, 0f);
            go.transform.localScale = new Vector3(length * 0.1f, 1f, width * 0.1f);
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            if (mat.shader.name == "Hidden/InternalErrorShader")
                mat = new Material(Shader.Find("Standard"));
            mat.color = color;
            go.GetComponent<Renderer>().sharedMaterial = mat;
        }

        static void AddRulerMarkers()
        {
            var rulerRoot = new GameObject("RulerMarkers");
            for (int m = 50; m <= 350; m += 50)
            {
                var marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                marker.name = $"Marker_{m}m";
                marker.transform.SetParent(rulerRoot.transform);
                marker.transform.position   = new Vector3(m, 0f, 0f);
                marker.transform.localScale = new Vector3(0.5f, 1f, 0.5f);
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                if (mat.shader.name == "Hidden/InternalErrorShader")
                    mat = new Material(Shader.Find("Standard"));
                mat.color = (m % 100 == 0) ? Color.red : Color.yellow;
                marker.GetComponent<Renderer>().sharedMaterial = mat;
            }
        }

        static void WireCamera(Camera cam, GameObject labRoot, TrajectoryRenderer traj, BallAnimator anim)
        {
            // ChaseCamera is on the camera GO, already linked in AddLabCore
            // Nothing extra needed here — the controller wires it at runtime
        }

        // ── Serialized field setter via SerializedObject ───────────────────────

        static void SetSerializedField(Object target, string fieldName, Object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(fieldName);
            if (prop == null)
            {
                Debug.LogWarning($"[PhysicsLabSceneBuilder] Field '{fieldName}' not found on {target.GetType().Name}");
                return;
            }
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetSerializedField(Object target, string fieldName, int enumValue)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(fieldName);
            if (prop == null)
            {
                Debug.LogWarning($"[PhysicsLabSceneBuilder] Field '{fieldName}' not found on {target.GetType().Name}");
                return;
            }
            prop.enumValueIndex = enumValue;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
