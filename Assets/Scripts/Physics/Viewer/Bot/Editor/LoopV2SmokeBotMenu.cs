#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Golfin.Physics.Viewer.Editor
{
    /// <summary>
    /// Menu launcher for Loop v2 smoke bot scenarios.
    ///
    /// Option B host-creation pattern: the [LoopV2SmokeBot] GameObject is created ONLY
    /// at EnteringPlayMode (via playModeStateChanged), never saved to disk. This means
    /// ShellScene.unity is never mutated by the launcher — zero scene contamination.
    ///
    /// Each menu item:
    ///   1. Verifies not already in play mode.
    ///   2. Opens ShellScene.unity (single mode).
    ///   3. Sets SessionState scenario key + armed flag.
    ///   4. Registers a one-shot playModeStateChanged handler that creates the host GO
    ///      on EnteringPlayMode (before Start() runs). The GO is never saved to disk.
    ///   5. Enters play mode. The host GO lives only in the in-memory play-mode scene.
    ///
    /// The bot self-destructs (Destroy(gameObject)) after the scenario completes.
    /// Captures land in tasks/loop_v2_smoke_bot/<scenario>/screenshots/.
    /// </summary>
    public static class LoopV2SmokeBotMenu
    {
        const string ShellScenePath = "Assets/Scenes/ShellScene.unity";

        [MenuItem("GOLFIN/Smoke/Loop v2/Hole 1 Playthrough")]
        public static void RunHole1Playthrough()    => Launch("hole1_playthrough");

        [MenuItem("GOLFIN/Smoke/Loop v2/Settings Round Trip")]
        public static void RunSettingsRoundTrip()   => Launch("settings_round_trip");

        [MenuItem("GOLFIN/Smoke/Loop v2/Hole Selection Browse")]
        public static void RunHoleSelectionBrowse() => Launch("hole_selection_browse");

        // ── Validation items (disable menu entries when in play mode) ─────────

        [MenuItem("GOLFIN/Smoke/Loop v2/Hole 1 Playthrough", isValidateFunction: true)]
        static bool ValidateRunHole1()          => !EditorApplication.isPlaying;

        [MenuItem("GOLFIN/Smoke/Loop v2/Settings Round Trip", isValidateFunction: true)]
        static bool ValidateRunSettings()       => !EditorApplication.isPlaying;

        [MenuItem("GOLFIN/Smoke/Loop v2/Hole Selection Browse", isValidateFunction: true)]
        static bool ValidateRunHoleSelection()  => !EditorApplication.isPlaying;

        // ── Launcher ─────────────────────────────────────────────────────────

        static void Launch(string scenarioKey)
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("[LoopV2SmokeBotMenu] Stop play mode first before launching a scenario.");
                return;
            }

            Debug.Log($"[LoopV2SmokeBotMenu] Launching scenario: '{scenarioKey}'");

            // 1. Open ShellScene (single mode — does NOT save the current scene).
            var shell = EditorSceneManager.OpenScene(ShellScenePath, OpenSceneMode.Single);
            if (!shell.IsValid())
            {
                Debug.LogError($"[LoopV2SmokeBotMenu] Failed to open {ShellScenePath}");
                return;
            }

            // 2. Arm via SessionState (survives domain reloads; does NOT touch any scene file).
            Golfin.Physics.Viewer.LoopV2SmokeBot.Scenario = scenarioKey;
            Golfin.Physics.Viewer.LoopV2SmokeBot.Armed    = true;

            Debug.Log($"[LoopV2SmokeBotMenu] Armed. Scenario='{scenarioKey}'. Entering play mode…");

            // 3. Register a one-shot handler that injects the host GO the moment Unity
            //    enters play mode (before any MonoBehaviour.Start() runs). The GO is
            //    created in-memory only — never saved to the scene file on disk.
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;

            // 4. Enter play mode. Unity will fire playModeStateChanged(EnteringPlayMode)
            //    synchronously before the scene starts, giving us the injection window.
            EditorApplication.EnterPlaymode();
        }

        static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                // We're now in play mode with the scene loaded. Create the host GO here.
                // This GO exists only in the in-memory play-mode scene, never on disk.
                var go = new GameObject("[LoopV2SmokeBot]");
                go.AddComponent<Golfin.Physics.Viewer.LoopV2SmokeBot>();
                Debug.Log("[LoopV2SmokeBotMenu] Injected [LoopV2SmokeBot] host into play-mode scene (not saved to disk).");

                // Unsubscribe immediately — one-shot only.
                EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            }
            else if (state == PlayModeStateChange.ExitingPlayMode)
            {
                // Clean up subscription if play mode exits before EnteredPlayMode fires.
                EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            }
        }
    }
}
#endif
