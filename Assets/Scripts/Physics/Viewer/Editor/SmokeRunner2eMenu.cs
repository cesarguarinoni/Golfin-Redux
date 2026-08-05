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
    ///
    /// Exit-path contract (2026-08-05, hole_scene_leftover):
    ///   - The pre-run scene setup is snapshotted BEFORE any OpenScene and restored at
    ///     EnteredEditMode, so a capture run no longer leaves LabScaffold + Hole_06_Geo as
    ///     the editor hierarchy. A leftover hole scene is not just clutter — it makes
    ///     ScanForLoadedHoleSceneAtStartup wire the lab to the WRONG HOLE on the next play
    ///     run that does not pre-clean.
    ///   - Host creation follows the LoopV2SmokeBotMenu "Option B" pattern: the
    ///     [SmokeRunner2eHost] GameObject is injected at EnteredPlayMode and NEVER saved to
    ///     disk. LabScaffold is not written at all by a normal run — the only legitimate
    ///     write left is stripping serialized host residue left by an older build.
    ///   - The playModeStateChanged handler is re-registered by [InitializeOnLoadMethod] so a
    ///     domain reload mid-flow cannot orphan the cleanup. It is a no-op unless armed.
    ///
    /// Staging DURING the run is unchanged: same scenes, same additive order, same host,
    /// same capture sequence. This is exit-path only.
    /// </summary>
    public static class SmokeRunner2eMenu
    {
        const string LabScenePath    = "Assets/Scenes/Physics/LabScaffold.unity";
        const string Hole01ScenePath = "Assets/Golf/Courses/lomond-country-club/Generated/Hole_01_Geo.unity";
        const string Hole01SceneName = "Hole_01_Geo";
        const string Hole06ScenePath = "Assets/Golf/Courses/lomond-country-club/Generated/Hole_06_Geo.unity";
        const string Hole06SceneName = "Hole_06_Geo";

        // SessionState (NOT EditorPrefs — must not leak across projects/sessions).
        const string SetupKey   = "SmokeRunner2eMenu.SceneSetup";
        const string CleanupKey = "SmokeRunner2eMenu.CleanupPending";

        // ── Menu Item 1: AtRest capture on Hole_01 ──────────────────────────────

        [MenuItem("GOLFIN/Smoke/Capture 2e AtRest Facing Pin (Hole_01)")]
        public static void RunAtRest()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("[SmokeRunner2eMenu] Cannot start: Unity is in play mode. Stop first.");
                return;
            }

            // Snapshot BEFORE any OpenScene — this is what the exit hook restores.
            CaptureSceneSetup.Capture(SetupKey);

            if (!OpenLabAndHole(Hole01ScenePath, Hole01SceneName)) return;
            Arm(captureMode: 0);
            EditorApplication.delayCall += () => EnterPlayMode(Hole01SceneName);
        }

        [MenuItem("GOLFIN/Smoke/Capture 2e AtRest Facing Pin (Hole_01)", isValidateFunction: true)]
        static bool ValidateRunAtRest() => !EditorApplication.isPlaying;

        // ── Menu Item 2: OB capture on Hole_06 ─────────────────────────────────

        [MenuItem("GOLFIN/Smoke/Capture 2e OB Drop + TURN (Hole_06)")]
        public static void RunOB()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("[SmokeRunner2eMenu] Cannot start: Unity is in play mode. Stop first.");
                return;
            }

            CaptureSceneSetup.Capture(SetupKey);

            if (!OpenLabAndHole(Hole06ScenePath, Hole06SceneName)) return;
            Arm(captureMode: 1);
            EditorApplication.delayCall += () => EnterPlayMode(Hole06SceneName);
        }

        [MenuItem("GOLFIN/Smoke/Capture 2e OB Drop + TURN (Hole_06)", isValidateFunction: true)]
        static bool ValidateRunOB() => !EditorApplication.isPlaying;

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

        /// <summary>
        /// Arm the host via SessionState only. The host component is NOT added here — it is
        /// injected at EnteredPlayMode so it is never serialized into LabScaffold.
        /// </summary>
        static void Arm(int captureMode)
        {
            SmokeRunner2eHost.Armed      = true;
            SmokeRunner2eHost.CaptureMode = captureMode;
            SessionState.SetBool(CleanupKey, true);
            Debug.Log($"[SmokeRunner2eMenu] Armed via SessionState (captureMode={captureMode}). " +
                      "Host will be injected at EnteredPlayMode — never saved to disk.");
        }

        static void EnterPlayMode(string holeSceneName)
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[SmokeRunner2eMenu] Delayed call: already in play mode.");
                return;
            }

            Debug.Log($"[SmokeRunner2eMenu] Entering play mode (hole={holeSceneName}). LabScaffold NOT saved.");
            EditorApplication.EnterPlaymode();
        }

        // ── Play-mode handler ───────────────────────────────────────────────────

        /// <summary>
        /// Re-register after every domain reload so the handler survives the compile cycle
        /// that may occur between the menu click and actual play-mode entry — and so the
        /// EnteredEditMode cleanup can never be orphaned. No-op unless armed.
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
                if (!SmokeRunner2eHost.Armed) return;

                // Inject the host in-memory, in the play-mode scene. Never saved to disk.
                // SmokeRunner2eHost resolves its dependencies via FindObjectOfType, so it does
                // not need to live on LabRoot; its first action is a 5s startup wait, so the
                // EnteredPlayMode creation time changes nothing about the capture sequence.
                var go = new GameObject("[SmokeRunner2eHost]");
                go.AddComponent<SmokeRunner2eHost>();
                Debug.Log($"[SmokeRunner2eMenu] Injected [SmokeRunner2eHost] into the play-mode scene " +
                          $"(captureMode={SmokeRunner2eHost.CaptureMode}, not saved to disk).");
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                if (!SessionState.GetBool(CleanupKey, false)) return;
                SessionState.SetBool(CleanupKey, false);

                SmokeRunner2eHost.Armed = false;

                // Residue sweep: older builds of this launcher serialized the host into
                // LabScaffold. Strip it and save the scene ONCE, clean. No-op (and no write)
                // when there is nothing to strip, which is the normal case now.
                CaptureSceneSetup.StripSerializedHost<SmokeRunner2eHost>();

                // Close the staged hole scene without saving and put the hierarchy back the
                // way the run found it.
                CaptureSceneSetup.Restore(SetupKey);

                Debug.Log("[SmokeRunner2eMenu] §2e capture run cleaned up: host disarmed, hole scene closed, scene setup restored.");
            }
        }
    }
}
#endif
