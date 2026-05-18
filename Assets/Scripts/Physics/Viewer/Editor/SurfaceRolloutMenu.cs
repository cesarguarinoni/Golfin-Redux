#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Golfin.Physics.Viewer;

namespace Golfin.Physics.Viewer.Editor
{
    /// <summary>
    /// MenuItem launcher for SurfaceRolloutHarness.
    /// 1. Verify LabScaffold is active.
    /// 2. Open Hole 1 additively (the canonical surface-rich hole for Sub-modes 1a + 1b).
    /// 3. Create timestamped output dir under Docs/Specs/Active/phase_b_surface_tuning/captures/.
    /// 4. Attach SurfaceRolloutHarness to LabRoot with outputDir set.
    /// 5. Save scene + enter play mode.
    /// (Harness Start() coroutine handles the rest.)
    /// </summary>
    public static class SurfaceRolloutMenu
    {
        const string LabScenePath     = "Assets/Scenes/Physics/LabScaffold.unity";
        const string Hole01ScenePath  = "Assets/Golf/Courses/lomond-country-club/Generated/Hole_01_Geo.unity";
        const string Hole09ScenePath  = "Assets/Golf/Courses/lomond-country-club/Generated/Hole_09_Geo.unity";
        const string Hole18ScenePath  = "Assets/Golf/Courses/lomond-country-club/Generated/Hole_18_Geo.unity";
        const string Hole01SceneName  = "Hole_01_Geo";

        [MenuItem("GOLFIN/Physics/Run Surface Rollout Sweep")]
        public static void Run()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("[SurfaceRolloutMenu] Cannot start: Unity is already in play mode. Stop first.");
                return;
            }

            // 1. Ensure LabScaffold is loaded
            if (!EnsureLabScaffoldLoaded()) return;

            // 2. Close any stale hole scenes (not 01/09/18); open 01, 09, 18 additively.
            // All 3 are pre-loaded so Sub-mode 2 can switch between them without
            // SceneManager.LoadSceneAsync (which requires scenes to be in Build Settings).
            CloseStaleHoleScenes("Hole_01_Geo", "Hole_09_Geo", "Hole_18_Geo");
            if (!EnsureHoleLoaded(Hole01ScenePath, Hole01SceneName)) return;
            EnsureHoleLoaded(Hole09ScenePath, "Hole_09_Geo", allowMissing: true);
            EnsureHoleLoaded(Hole18ScenePath, "Hole_18_Geo", allowMissing: true);

            // 3. Create timestamped output dir
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string repoRoot  = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string outDir    = Path.Combine(repoRoot,
                "Docs/Specs/Active/phase_b_surface_tuning/captures", timestamp);
            Directory.CreateDirectory(outDir);
            Debug.Log($"[SurfaceRolloutMenu] Output dir: {outDir}");

            // 4. Attach / re-attach harness on LabRoot
            if (!AttachHarness(outDir)) return;

            // 5. Save scene and enter play mode
            EditorApplication.delayCall += SaveAndEnterPlayMode;
        }

        static void CloseStaleHoleScenes(params string[] keepNames)
        {
            var keepSet = new System.Collections.Generic.HashSet<string>(keepNames);
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                if (s.name == null || !s.name.StartsWith("Hole_") || !s.name.EndsWith("_Geo")) continue;
                if (keepSet.Contains(s.name)) continue;
                Debug.Log($"[SurfaceRolloutMenu] Closing stale hole scene: {s.name}");
                EditorSceneManager.CloseScene(s, true);
                i--;
            }
        }

        // ─────────────────────────────────────────────────────────────────────────────

        static bool EnsureLabScaffoldLoaded()
        {
            var lab = SceneManager.GetSceneByName("LabScaffold");
            if (lab.IsValid() && lab.isLoaded) return true;

            lab = EditorSceneManager.OpenScene(LabScenePath, OpenSceneMode.Single);
            if (!lab.IsValid())
            {
                Debug.LogError($"[SurfaceRolloutMenu] Could not open {LabScenePath}. Is the path correct?");
                return false;
            }
            return true;
        }

        static bool EnsureHoleLoaded(string scenePath, string sceneName, bool allowMissing = false)
        {
            var hole = SceneManager.GetSceneByName(sceneName);
            if (hole.IsValid() && hole.isLoaded) return true;

            hole = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            if (!hole.IsValid())
            {
                if (allowMissing)
                {
                    Debug.LogWarning($"[SurfaceRolloutMenu] Could not open {scenePath} — Sub-mode 2 will skip this hole.");
                    return false;
                }
                Debug.LogError($"[SurfaceRolloutMenu] Could not open {scenePath}.");
                return false;
            }
            Debug.Log($"[SurfaceRolloutMenu] Opened {sceneName} additively.");
            return true;
        }

        static bool AttachHarness(string outputDir)
        {
            var labRoot = GameObject.Find("LabRoot");
            if (labRoot == null)
            {
                Debug.LogError("[SurfaceRolloutMenu] LabRoot GameObject not found in LabScaffold. Aborting.");
                return false;
            }

            // Remove any stale harness from a previous aborted run (only the harness — NOT smoke runners).
            // NOTE: We intentionally do NOT remove SmokeRunner2c/2d/2e/2fHost components from LabRoot.
            // Those are production components that must persist in the scene file; touching them caused
            // the iter-4 scene corruption. We ONLY add/remove the SurfaceRolloutHarness itself.
            var stale = labRoot.GetComponent<SurfaceRolloutHarness>();
            if (stale != null)
            {
                UnityEngine.Object.DestroyImmediate(stale);
                Debug.Log("[SurfaceRolloutMenu] Removed stale SurfaceRolloutHarness.");
            }

            // Add the harness to LabRoot IN MEMORY only. We do NOT call SaveScene, so LabScaffold.unity
            // is never written to disk. The harness survives the domain-reload into play mode because it
            // is part of the scene's in-memory state, not because it's in the file. OnPlayModeStateChanged
            // removes it from memory after the sweep so the in-memory scene matches the file.
            var harness = labRoot.AddComponent<SurfaceRolloutHarness>();
            harness._outputDir          = outputDir;
            harness._runSubMode1a       = true;
            harness._runSubMode1b       = true;
            harness._runSubMode2        = true;
            harness._holesForSubMode1   = new[] { 1, 9, 18 };   // §iter-7: sub-mode 1 now sweeps all 3 holes
            harness._holesForSubMode2   = new[] { 1, 9, 18 };

            SurfaceRolloutHarness.Armed = true;
            Debug.Log($"[SurfaceRolloutMenu] SurfaceRolloutHarness added to LabRoot (in-memory only, not saved). outputDir={outputDir}");
            return true;
        }

        static void SaveAndEnterPlayMode()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[SurfaceRolloutMenu] Delayed call: already in play mode.");
                return;
            }

            // NOTE: We do NOT call SaveScene. The harness is attached to LabRoot only in memory.
            // The scene file is untouched. Entering play mode without saving is correct — Unity
            // serializes the in-memory scene into play mode state; the harness Start() will run.
            Debug.Log("[SurfaceRolloutMenu] Entering play mode (no scene save — harness is in-memory only)...");

            // Hook cleanup: after play mode exits, remove harness from scene memory
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;

            EditorApplication.EnterPlaymode();
        }

        static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode) return;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;

            // Disarm in case play mode was stopped early
            SurfaceRolloutHarness.Armed = false;

            // Remove the harness component from LabRoot. This restores in-memory scene to match
            // the file. We do NOT call MarkSceneDirty/SaveScene — the file was never touched.
            var lab = SceneManager.GetSceneByName("LabScaffold");
            if (!lab.IsValid()) return;

            foreach (var go in lab.GetRootGameObjects())
            {
                var h = go.GetComponentInChildren<SurfaceRolloutHarness>();
                if (h != null)
                {
                    UnityEngine.Object.DestroyImmediate(h);
                    // NOTE: deliberately no MarkSceneDirty or SaveScene here.
                    // The file was never touched; the in-memory state is now clean.
                    Debug.Log("[SurfaceRolloutMenu] Post-play-mode cleanup: SurfaceRolloutHarness removed from LabRoot. Scene file unchanged.");
                    break;
                }
            }
        }
    }
}
#endif
