#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Golfin.Physics.Viewer.Editor
{
    /// <summary>
    /// §2f screenshot capture launcher.
    /// Runs: LabScaffold + Hole_01_Geo → driver → auto-enter putter →
    ///       putt → auto-exit putter → tuning panel → slider apply.
    /// </summary>
    public static class SmokeRunner2fMenu
    {
        const string LabScenePath    = "Assets/Scenes/Physics/LabScaffold.unity";
        const string Hole01ScenePath = "Assets/Golf/Courses/lomond-country-club/Generated/Hole_01_Geo.unity";
        const string Hole01SceneName = "Hole_01_Geo";

        [MenuItem("GOLFIN/Smoke/Capture 2f Putter Auto-Switch + Tuning (Hole_01)")]
        public static void Run()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("[SmokeRunner2fMenu] Cannot start: Unity is in play mode. Stop first.");
                return;
            }

            if (!OpenLabAndHole()) return;
            AttachHost();
            EditorApplication.delayCall += SaveAndEnterPlayMode;
        }

        static bool OpenLabAndHole()
        {
            Scene lab = SceneManager.GetSceneByName("LabScaffold");
            if (!lab.IsValid() || !lab.isLoaded)
            {
                lab = EditorSceneManager.OpenScene(LabScenePath, OpenSceneMode.Single);
                if (!lab.IsValid())
                {
                    Debug.LogError("[SmokeRunner2fMenu] Could not open LabScaffold.unity");
                    return false;
                }
            }

            // Close any hole scenes that are NOT Hole_01_Geo (e.g. Hole_06_Geo left over
            // from a prior §2e run). This prevents ScanForLoadedHoleSceneAtStartup from
            // wiring to the wrong hole when play mode starts.
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                if (s.name != null && s.name.StartsWith("Hole_") && s.name.EndsWith("_Geo")
                    && s.name != Hole01SceneName)
                {
                    Debug.Log($"[SmokeRunner2fMenu] Closing stale hole scene: {s.name}");
                    EditorSceneManager.CloseScene(s, true);
                    i--; // scene count changed
                }
            }

            Scene hole = SceneManager.GetSceneByName(Hole01SceneName);
            if (!hole.IsValid() || !hole.isLoaded)
            {
                hole = EditorSceneManager.OpenScene(Hole01ScenePath, OpenSceneMode.Additive);
                if (!hole.IsValid())
                {
                    Debug.LogError($"[SmokeRunner2fMenu] Could not open {Hole01ScenePath}.");
                    return false;
                }
            }

            return true;
        }

        static void AttachHost()
        {
            var labRoot = GameObject.Find("LabRoot");
            if (labRoot == null)
            {
                Debug.LogError("[SmokeRunner2fMenu] LabRoot not found.");
                return;
            }

            // Remove stale 2f host
            var stale2f = labRoot.GetComponent<SmokeRunner2fHost>();
            if (stale2f != null) Object.DestroyImmediate(stale2f);

            // Also clear out 2e and 2d hosts to prevent interference
            var stale2e = labRoot.GetComponent<SmokeRunner2eHost>();
            if (stale2e != null)
            {
                Object.DestroyImmediate(stale2e);
                Debug.Log("[SmokeRunner2fMenu] Removed stale SmokeRunner2eHost.");
            }
            // Clear 2e armed state
            SmokeRunner2eHost.Armed = false;
            Debug.Log("[SmokeRunner2fMenu] SmokeRunner2eHost.Armed cleared.");

            labRoot.AddComponent<SmokeRunner2fHost>();
            SmokeRunner2fHost.Armed = true;
            Debug.Log("[SmokeRunner2fMenu] SmokeRunner2fHost attached and armed.");
        }

        static void SaveAndEnterPlayMode()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[SmokeRunner2fMenu] Delayed call: already in play mode.");
                return;
            }

            Scene lab = SceneManager.GetSceneByName("LabScaffold");
            if (lab.IsValid() && lab.isLoaded)
            {
                EditorSceneManager.MarkSceneDirty(lab);
                EditorSceneManager.SaveScene(lab);
                Debug.Log("[SmokeRunner2fMenu] LabScaffold saved. Entering play mode...");
            }

            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.EnterPlaymode();
        }

        static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode) return;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;

            SmokeRunner2fHost.Armed = false;

            Scene lab = SceneManager.GetSceneByName("LabScaffold");
            if (!lab.IsValid()) return;

            foreach (var go in lab.GetRootGameObjects())
            {
                var host = go.GetComponentInChildren<SmokeRunner2fHost>();
                if (host != null)
                {
                    Object.DestroyImmediate(host);
                    EditorSceneManager.MarkSceneDirty(lab);
                    EditorSceneManager.SaveScene(lab);
                    Debug.Log("[SmokeRunner2fMenu] Cleaned SmokeRunner2fHost after §2f capture run.");
                    break;
                }
            }
        }
    }
}
#endif
