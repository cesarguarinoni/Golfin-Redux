#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Golfin.Physics.Viewer.Editor
{
    /// <summary>
    /// §2d screenshot capture launcher.
    /// Loads LabScaffold + Hole_01_Geo additively in edit mode, attaches SmokeRunner2dHost,
    /// saves, then enters play mode to capture the 3 required HoleCompleteWidget screenshots.
    ///
    /// Invoked via: GOLFIN > Smoke > Capture 2d HoleComplete Screenshots
    ///
    /// Pre-requisite: run GOLFIN/Build/Build HoleComplete Widgets (§2d) first.
    /// </summary>
    public static class SmokeRunner2dMenu
    {
        const string LabScenePath  = "Assets/Scenes/Physics/LabScaffold.unity";
        const string HoleScenePath = "Assets/Golf/Courses/lomond-country-club/Generated/Hole_01_Geo.unity";
        const string HoleSceneName = "Hole_01_Geo";

        [MenuItem("GOLFIN/Smoke/Capture 2d HoleComplete Screenshots")]
        public static void Run()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("[SmokeRunner2dMenu] Cannot start: Unity is currently in play mode. Stop play mode first.");
                return;
            }

            // 1. Open LabScaffold as base scene.
            Scene lab = SceneManager.GetSceneByName("LabScaffold");
            if (!lab.IsValid() || !lab.isLoaded)
            {
                lab = EditorSceneManager.OpenScene(LabScenePath, OpenSceneMode.Single);
                if (!lab.IsValid())
                {
                    Debug.LogError("[SmokeRunner2dMenu] Could not open LabScaffold.unity");
                    return;
                }
            }

            // 2. Load Hole_01_Geo additively (makes LabRoot visible in play mode).
            Scene hole = SceneManager.GetSceneByName(HoleSceneName);
            if (!hole.IsValid() || !hole.isLoaded)
            {
                hole = EditorSceneManager.OpenScene(HoleScenePath, OpenSceneMode.Additive);
                if (!hole.IsValid())
                {
                    Debug.LogError($"[SmokeRunner2dMenu] Could not open {HoleScenePath}.");
                    return;
                }
            }

            // 3. Find LabRoot and attach SmokeRunner2dHost.
            var labRoot = GameObject.Find("LabRoot");
            if (labRoot == null)
            {
                Debug.LogError("[SmokeRunner2dMenu] Could not find 'LabRoot'. Is LabScaffold loaded?");
                return;
            }

            // Remove stale host if present.
            var stale = labRoot.GetComponent<SmokeRunner2dHost>();
            if (stale != null) Object.DestroyImmediate(stale);
            labRoot.AddComponent<SmokeRunner2dHost>();

            // Arm the host so it runs in play mode. The flag is a plain static (not
            // serialized), so it resets to false on every domain reload — accidental
            // leftover components can never auto-run on a fresh Unity session.
            SmokeRunner2dHost.Armed = true;

            Debug.Log("[SmokeRunner2dMenu] SmokeRunner2dHost attached and armed. Scheduling save + play mode...");

            // 4. Schedule save + enter play mode on next frame (avoids "during import" errors).
            EditorApplication.delayCall += SaveAndEnterPlayMode;
        }

        static void SaveAndEnterPlayMode()
        {
            EditorApplication.delayCall -= SaveAndEnterPlayMode;

            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[SmokeRunner2dMenu] Delayed call: already in play mode.");
                return;
            }

            Scene lab = SceneManager.GetSceneByName("LabScaffold");
            if (lab.IsValid() && lab.isLoaded)
            {
                EditorSceneManager.MarkSceneDirty(lab);
                EditorSceneManager.SaveScene(lab);
                Debug.Log("[SmokeRunner2dMenu] LabScaffold saved. Entering play mode for §2d screenshot capture...");
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

            // Clean up SmokeRunner2dHost from LabScaffold after capture run.
            Scene lab = SceneManager.GetSceneByName("LabScaffold");
            if (!lab.IsValid()) return;

            // Disarm first so any stale host that somehow survived can't re-run.
            SmokeRunner2dHost.Armed = false;

            foreach (var go in lab.GetRootGameObjects())
            {
                var host = go.GetComponentInChildren<SmokeRunner2dHost>();
                if (host != null)
                {
                    Object.DestroyImmediate(host);
                    EditorSceneManager.MarkSceneDirty(lab);
                    EditorSceneManager.SaveScene(lab);
                    Debug.Log("[SmokeRunner2dMenu] Cleaned SmokeRunner2dHost from LabScaffold after §2d capture run.");
                    break;
                }
            }
        }
    }
}
#endif
