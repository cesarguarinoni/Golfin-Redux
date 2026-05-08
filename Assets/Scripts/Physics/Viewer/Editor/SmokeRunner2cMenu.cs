#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Golfin.Physics.Viewer.Editor
{
    /// <summary>
    /// §2c smoke runner launcher.
    /// Loads hole scene in edit mode, attaches SmokeRunner2cHost, saves, then enters play mode
    /// via EditorApplication.delayCall to avoid the "during import" InvalidOperationException.
    /// Invoked via: GOLFIN > Smoke > Run 2c TurnCounter
    /// </summary>
    public static class SmokeRunner2cMenu
    {
        const string LabScenePath  = "Assets/Scenes/Physics/LabScaffold.unity";
        const string HoleScenePath = "Assets/Golf/Courses/lomond-country-club/Generated/Hole_01_Geo.unity";
        const string HoleSceneName = "Hole_01_Geo";

        [MenuItem("GOLFIN/Smoke/Run 2c TurnCounter")]
        public static void Run()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("[SmokeRunner2cMenu] Cannot start: Unity is currently in play mode. Stop play mode first.");
                return;
            }

            // 1. Open LabScaffold as base scene if not already loaded.
            Scene lab = SceneManager.GetSceneByName("LabScaffold");
            if (!lab.IsValid() || !lab.isLoaded)
            {
                lab = EditorSceneManager.OpenScene(LabScenePath, OpenSceneMode.Single);
                if (!lab.IsValid())
                {
                    Debug.LogError("[SmokeRunner2cMenu] Could not open LabScaffold.unity");
                    return;
                }
            }

            // 2. Load Hole_01_Geo additively in edit mode.
            Scene hole = SceneManager.GetSceneByName(HoleSceneName);
            if (!hole.IsValid() || !hole.isLoaded)
            {
                hole = EditorSceneManager.OpenScene(HoleScenePath, OpenSceneMode.Additive);
                if (!hole.IsValid())
                {
                    Debug.LogError($"[SmokeRunner2cMenu] Could not open {HoleScenePath}.");
                    return;
                }
            }

            // 3. Find LabRoot and attach the runtime host.
            var labRoot = GameObject.Find("LabRoot");
            if (labRoot == null)
            {
                Debug.LogError("[SmokeRunner2cMenu] Could not find 'LabRoot'.");
                return;
            }
            var stale = labRoot.GetComponent<SmokeRunner2cHost>();
            if (stale != null) Object.DestroyImmediate(stale);
            labRoot.AddComponent<SmokeRunner2cHost>();

            // 4. Schedule save + enter play mode for the NEXT FRAME to avoid
            //    "this cannot be used during import" InvalidOperationException.
            EditorApplication.delayCall += SaveAndEnterPlayMode;
        }

        static void SaveAndEnterPlayMode()
        {
            EditorApplication.delayCall -= SaveAndEnterPlayMode;

            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[SmokeRunner2cMenu] Delayed call: already in play mode.");
                return;
            }

            // Find the lab scene and save it with SmokeRunner2cHost.
            Scene lab = SceneManager.GetSceneByName("LabScaffold");
            if (lab.IsValid() && lab.isLoaded)
            {
                EditorSceneManager.MarkSceneDirty(lab);
                EditorSceneManager.SaveScene(lab);
                Debug.Log("[SmokeRunner2cMenu] LabScaffold saved. Entering play mode...");
            }

            // Register cleanup callback.
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;

            EditorApplication.EnterPlaymode();
        }

        static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode) return;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;

            // Clean up SmokeRunner2cHost from LabScaffold.
            Scene lab = SceneManager.GetSceneByName("LabScaffold");
            if (!lab.IsValid()) return;

            foreach (var go in lab.GetRootGameObjects())
            {
                var host = go.GetComponentInChildren<SmokeRunner2cHost>();
                if (host != null)
                {
                    Object.DestroyImmediate(host);
                    EditorSceneManager.MarkSceneDirty(lab);
                    EditorSceneManager.SaveScene(lab);
                    Debug.Log("[SmokeRunner2cMenu] Cleaned SmokeRunner2cHost from LabScaffold after smoke run.");
                    break;
                }
            }
        }
    }
}
#endif
