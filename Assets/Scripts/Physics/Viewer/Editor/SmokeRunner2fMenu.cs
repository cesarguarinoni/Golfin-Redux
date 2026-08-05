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

        // hole_scene_leftover (2026-08-05). SessionState, not EditorPrefs.
        const string SetupKey   = "SmokeRunner2fMenu.SceneSetup";
        const string CleanupKey = "SmokeRunner2fMenu.CleanupPending";

        [MenuItem("GOLFIN/Smoke/Capture 2f Putter Auto-Switch + Tuning (Hole_01)")]
        public static void Run()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("[SmokeRunner2fMenu] Cannot start: Unity is in play mode. Stop first.");
                return;
            }

            // Snapshot BEFORE any OpenScene — restored at EnteredEditMode so this run no longer
            // leaves LabScaffold + Hole_01_Geo as the editor hierarchy. The defensive pre-clean
            // in OpenLabAndHole() below STAYS — defence in depth; it should now find nothing.
            CaptureSceneSetup.Capture(SetupKey);
            // Arm the cleanup gate HERE, not in SaveAndEnterPlayMode — if that delayed call
            // ever early-returns, the snapshot would otherwise be stranded and never restored.
            SessionState.SetBool(CleanupKey, true);

            if (!OpenLabAndHole()) return;
            Arm();
            EditorApplication.delayCall += EnterPlayMode;
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

        /// <summary>
        /// Arm via SessionState only. The host component is NOT attached here — it is injected
        /// at EnteredPlayMode (LoopV2SmokeBotMenu "Option B") so it is never serialized into
        /// LabScaffold and the scene is never written by a capture run.
        ///
        /// This replaced an attach-then-SaveScene staging step. That save was not free: it also
        /// baked unrelated serialization catch-up (13 `_disabledAlpha` lines) into LabScaffold,
        /// so every §2f run dirtied the scene with churn that had nothing to do with the capture.
        /// </summary>
        static void Arm()
        {
            // Clear 2e armed state so a stale §2e arm cannot interfere with this run.
            SmokeRunner2eHost.Armed = false;
            SmokeRunner2fHost.Armed = true;
            Debug.Log("[SmokeRunner2fMenu] SmokeRunner2eHost.Armed cleared; SmokeRunner2fHost armed via SessionState. " +
                      "Host will be injected at EnteredPlayMode — never saved to disk.");
        }

        static void EnterPlayMode()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[SmokeRunner2fMenu] Delayed call: already in play mode.");
                return;
            }

            Debug.Log("[SmokeRunner2fMenu] Entering play mode. LabScaffold NOT saved.");
            EditorApplication.EnterPlaymode();
        }

        /// <summary>
        /// Re-register on EVERY domain load, not only after a compile, so a domain reload
        /// mid-flow cannot orphan the EnteredEditMode cleanup. An orphaned cleanup is how the
        /// serialized SmokeRunner2fHost residue ended up committed in LabScaffold.unity.
        /// No-op unless a run armed the cleanup gate.
        /// </summary>
        [InitializeOnLoadMethod]
        static void RegisterHandler()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                if (!SmokeRunner2fHost.Armed) return;

                // Inject the host in-memory, in the play-mode scene. Never saved to disk.
                // SmokeRunner2fHost resolves its dependencies by lookup, not by living on
                // LabRoot, so this changes nothing about the capture sequence.
                var go = new GameObject("[SmokeRunner2fHost]");
                go.AddComponent<SmokeRunner2fHost>();
                Debug.Log("[SmokeRunner2fMenu] Injected [SmokeRunner2fHost] into the play-mode scene (not saved to disk).");
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                if (!SessionState.GetBool(CleanupKey, false)) return;
                SessionState.SetBool(CleanupKey, false);

                SmokeRunner2fHost.Armed = false;

                // Safety net for residue left by an OLDER build of this launcher (which attached
                // and serialized the host). No-op — and no LabScaffold write — in the normal case.
                CaptureSceneSetup.StripSerializedHost<SmokeRunner2fHost>();

                // Close the staged hole scene without saving and restore the pre-run hierarchy.
                CaptureSceneSetup.Restore(SetupKey);

                Debug.Log("[SmokeRunner2fMenu] §2f capture run cleaned up: hole scene closed, scene setup restored.");
            }
        }
    }
}
#endif
