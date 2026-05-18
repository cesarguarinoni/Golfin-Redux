#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Golfin.Physics.Viewer.Editor
{
    /// <summary>
    /// putter_cone_per_shot_lifecycle smoke launcher.
    /// Opens LabScaffold, attaches PutterConeSmokeCapture with AutoStart=true,
    /// and enters play mode. The coroutine runs automatically.
    ///
    /// Can be triggered via menu GOLFIN/Smoke/Capture PutterCone Lifecycle
    /// OR by calling SmokeRunnerPutterConeMenu.Run() directly from script-execute.
    /// </summary>
    public static class SmokeRunnerPutterConeMenu
    {
        const string LabScenePath = "Assets/Scenes/Physics/LabScaffold.unity";

        [MenuItem("GOLFIN/Smoke/Capture PutterCone Lifecycle")]
        public static void Run()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("[SmokeRunnerPutterCone] Cannot start: Unity is in play mode. Stop first.");
                return;
            }

            if (!OpenLab()) return;
            AttachSmokeCapture();
            // delayCall defers until after the current editor frame so the GO is properly
            // registered before we ask Unity to enter play mode.
            EditorApplication.delayCall += SaveAndEnterPlayMode;
        }

        static bool OpenLab()
        {
            Scene lab = SceneManager.GetSceneByName("LabScaffold");
            if (!lab.IsValid() || !lab.isLoaded)
            {
                lab = EditorSceneManager.OpenScene(LabScenePath, OpenSceneMode.Single);
                if (!lab.IsValid())
                {
                    Debug.LogError("[SmokeRunnerPutterCone] Could not open LabScaffold.unity");
                    return false;
                }
            }

            // Close any stale additive hole scenes so they don't interfere.
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                if (s.name != null && s.name.StartsWith("Hole_") && s.name.EndsWith("_Geo"))
                {
                    Debug.Log($"[SmokeRunnerPutterCone] Closing stale hole scene: {s.name}");
                    EditorSceneManager.CloseScene(s, true);
                    i--;
                }
            }

            return true;
        }

        static void AttachSmokeCapture()
        {
            // Remove any stale smoke capture from a previous run.
            var stale = Object.FindObjectOfType<PutterConeSmokeCapture>(true);
            if (stale != null)
            {
                Object.DestroyImmediate(stale.gameObject);
                Debug.Log("[SmokeRunnerPutterCone] Removed stale PutterConeSmokeCapture.");
            }

            // Create a new GO for the smoke capture.
            var go = new GameObject("_PutterConeSmoke");
            var capture = go.AddComponent<PutterConeSmokeCapture>();
            capture.AutoStart   = true;
            capture.InitialDelay = 3f; // wait for LabScaffold to fully initialize in play mode

            // Move to LabScaffold scene so it serializes with the scene.
            Scene lab = SceneManager.GetSceneByName("LabScaffold");
            if (lab.IsValid() && lab.isLoaded)
                SceneManager.MoveGameObjectToScene(go, lab);

            Debug.Log("[SmokeRunnerPutterCone] PutterConeSmokeCapture attached (AutoStart=true, delay=3s).");
        }

        static void SaveAndEnterPlayMode()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[SmokeRunnerPutterCone] Delayed call: already in play mode.");
                return;
            }

            Scene lab = SceneManager.GetSceneByName("LabScaffold");
            if (lab.IsValid() && lab.isLoaded)
            {
                EditorSceneManager.MarkSceneDirty(lab);
                EditorSceneManager.SaveScene(lab);
                Debug.Log("[SmokeRunnerPutterCone] LabScaffold saved. Entering play mode...");
            }

            // Clean up the component from the scene after play mode exits.
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;

            EditorApplication.EnterPlaymode();
        }

        static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode) return;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;

            // Remove the smoke GO from the scene so it doesn't accidentally run again.
            var leftover = Object.FindObjectOfType<PutterConeSmokeCapture>(true);
            if (leftover != null)
            {
                Debug.Log("[SmokeRunnerPutterCone] Cleaning up PutterConeSmokeCapture from scene.");
                Object.DestroyImmediate(leftover.gameObject);
                Scene lab = SceneManager.GetSceneByName("LabScaffold");
                if (lab.IsValid() && lab.isLoaded)
                    EditorSceneManager.SaveScene(lab);
            }

            Debug.Log("[SmokeRunnerPutterCone] Play mode ended. Check Docs/Diagnostics/_capture/ for PNG files.");
        }
    }
}
#endif
