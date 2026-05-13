#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Golfin.Physics.Viewer.Editor
{
    /// <summary>
    /// §2e screenshot capture launcher.
    /// Two menu items:
    ///   1. AtRest sequence: LabScaffold + Hole_01_Geo → fire driver shot → wait AtRest →
    ///      capture controls_2e_atrest_facing_pin.png (camera should face pin)
    ///   2. OB sequence: LabScaffold + Hole_06_Geo → fire shot toward lake → wait OB→Aiming →
    ///      capture controls_2e_ob_drop.png, controls_2e_turn_counter_after_ob.png,
    ///      controls_2e_history_log.txt
    /// </summary>
    public static class SmokeRunner2eMenu
    {
        const string LabScenePath    = "Assets/Scenes/Physics/LabScaffold.unity";
        const string Hole01ScenePath = "Assets/Golf/Courses/lomond-country-club/Generated/Hole_01_Geo.unity";
        const string Hole01SceneName = "Hole_01_Geo";
        const string Hole06ScenePath = "Assets/Golf/Courses/lomond-country-club/Generated/Hole_06_Geo.unity";
        const string Hole06SceneName = "Hole_06_Geo";

        // ── Menu Item 1: AtRest capture on Hole_01 ──────────────────────────────

        [MenuItem("GOLFIN/Smoke/Capture 2e AtRest Facing Pin (Hole_01)")]
        public static void RunAtRest()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("[SmokeRunner2eMenu] Cannot start: Unity is in play mode. Stop first.");
                return;
            }

            if (!OpenLabAndHole(Hole01ScenePath, Hole01SceneName)) return;
            AttachHost(captureMode: 0);
            EditorApplication.delayCall += () => SaveAndEnterPlayMode(Hole01SceneName);
        }

        // ── Menu Item 2: OB capture on Hole_06 ─────────────────────────────────

        [MenuItem("GOLFIN/Smoke/Capture 2e OB Drop + TURN (Hole_06)")]
        public static void RunOB()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("[SmokeRunner2eMenu] Cannot start: Unity is in play mode. Stop first.");
                return;
            }

            if (!OpenLabAndHole(Hole06ScenePath, Hole06SceneName)) return;
            AttachHost(captureMode: 1);
            EditorApplication.delayCall += () => SaveAndEnterPlayMode(Hole06SceneName);
        }

        // ── Shared helpers ──────────────────────────────────────────────────────

        static bool OpenLabAndHole(string holePath, string holeName)
        {
            // Open LabScaffold as base scene.
            Scene lab = SceneManager.GetSceneByName("LabScaffold");
            if (!lab.IsValid() || !lab.isLoaded)
            {
                lab = EditorSceneManager.OpenScene(LabScenePath, OpenSceneMode.Single);
                if (!lab.IsValid())
                {
                    Debug.LogError("[SmokeRunner2eMenu] Could not open LabScaffold.unity");
                    return false;
                }
            }

            // Load hole scene additively (LabHoleBinder picks it up via OnEditorSceneOpened).
            Scene hole = SceneManager.GetSceneByName(holeName);
            if (!hole.IsValid() || !hole.isLoaded)
            {
                hole = EditorSceneManager.OpenScene(holePath, OpenSceneMode.Additive);
                if (!hole.IsValid())
                {
                    Debug.LogError($"[SmokeRunner2eMenu] Could not open {holePath}.");
                    return false;
                }
            }

            return true;
        }

        static void AttachHost(int captureMode)
        {
            var labRoot = GameObject.Find("LabRoot");
            if (labRoot == null)
            {
                Debug.LogError("[SmokeRunner2eMenu] LabRoot not found.");
                return;
            }

            // Remove stale host.
            var stale = labRoot.GetComponent<SmokeRunner2eHost>();
            if (stale != null) Object.DestroyImmediate(stale);

            // Attach fresh host.
            labRoot.AddComponent<SmokeRunner2eHost>();
            SmokeRunner2eHost.Armed = true;
            SmokeRunner2eHost.CaptureMode = captureMode;
            Debug.Log($"[SmokeRunner2eMenu] SmokeRunner2eHost attached (captureMode={captureMode}).");
        }

        static void SaveAndEnterPlayMode(string holeSceneName)
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[SmokeRunner2eMenu] Delayed call: already in play mode.");
                return;
            }

            Scene lab = SceneManager.GetSceneByName("LabScaffold");
            if (lab.IsValid() && lab.isLoaded)
            {
                EditorSceneManager.MarkSceneDirty(lab);
                EditorSceneManager.SaveScene(lab);
                Debug.Log($"[SmokeRunner2eMenu] LabScaffold saved. Entering play mode (hole={holeSceneName})...");
            }

            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.EnterPlaymode();
        }

        static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode) return;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;

            SmokeRunner2eHost.Armed = false;

            Scene lab = SceneManager.GetSceneByName("LabScaffold");
            if (!lab.IsValid()) return;

            foreach (var go in lab.GetRootGameObjects())
            {
                var host = go.GetComponentInChildren<SmokeRunner2eHost>();
                if (host != null)
                {
                    Object.DestroyImmediate(host);
                    EditorSceneManager.MarkSceneDirty(lab);
                    EditorSceneManager.SaveScene(lab);
                    Debug.Log("[SmokeRunner2eMenu] Cleaned SmokeRunner2eHost after §2e capture run.");
                    break;
                }
            }
        }
    }
}
#endif
