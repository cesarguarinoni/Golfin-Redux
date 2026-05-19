#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Golfin.Physics.Viewer.Editor
{
    /// <summary>
    /// Menu launcher for Loop v2 smoke bot scenarios.
    ///
    /// Each menu item:
    ///   1. Verifies not already in play mode.
    ///   2. Opens ShellScene.unity (the production shell).
    ///   3. Creates a [LoopV2SmokeBot] GameObject and adds LoopV2SmokeBot.
    ///   4. Sets SessionState scenario key + armed flag.
    ///   5. Saves the scene and enters play mode via delayCall.
    ///
    /// The bot self-destructs after the scenario completes (or if not armed).
    /// Captures land in tasks/loop_v2_smoke_bot/<scenario>/screenshots/.
    /// </summary>
    public static class LoopV2SmokeBotMenu
    {
        const string ShellScenePath = "Assets/Scenes/ShellScene.unity";

        [MenuItem("GOLFIN/Smoke/Loop v2/Hole 1 Playthrough")]
        public static void RunHole1Playthrough()  => Launch("hole1_playthrough");

        [MenuItem("GOLFIN/Smoke/Loop v2/Settings Round Trip")]
        public static void RunSettingsRoundTrip() => Launch("settings_round_trip");

        [MenuItem("GOLFIN/Smoke/Loop v2/Hole Selection Browse")]
        public static void RunHoleSelectionBrowse() => Launch("hole_selection_browse");

        // ── Validation items (disable menu entries when in play mode) ─────────

        [MenuItem("GOLFIN/Smoke/Loop v2/Hole 1 Playthrough", isValidateFunction: true)]
        static bool ValidateRunHole1()  => !EditorApplication.isPlaying;

        [MenuItem("GOLFIN/Smoke/Loop v2/Settings Round Trip", isValidateFunction: true)]
        static bool ValidateRunSettings() => !EditorApplication.isPlaying;

        [MenuItem("GOLFIN/Smoke/Loop v2/Hole Selection Browse", isValidateFunction: true)]
        static bool ValidateRunHoleSelection() => !EditorApplication.isPlaying;

        // ── Launcher ─────────────────────────────────────────────────────────

        static void Launch(string scenarioKey)
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("[LoopV2SmokeBotMenu] Stop play mode first before launching a scenario.");
                return;
            }

            Debug.Log($"[LoopV2SmokeBotMenu] Launching scenario: '{scenarioKey}'");

            // 1. Open ShellScene (saves current scene if dirty).
            var shell = EditorSceneManager.OpenScene(ShellScenePath, OpenSceneMode.Single);
            if (!shell.IsValid())
            {
                Debug.LogError($"[LoopV2SmokeBotMenu] Failed to open {ShellScenePath}");
                return;
            }

            // 2. Create the bot host GameObject in the scene.
            var go = new GameObject("[LoopV2SmokeBot]");
            go.AddComponent<Golfin.Physics.Viewer.LoopV2SmokeBot>();

            // 3. Arm via SessionState.
            Golfin.Physics.Viewer.LoopV2SmokeBot.Scenario = scenarioKey;
            Golfin.Physics.Viewer.LoopV2SmokeBot.Armed    = true;

            Debug.Log($"[LoopV2SmokeBotMenu] Armed. Scenario='{scenarioKey}'. Saving scene and entering play mode…");

            // 4. Save the scene (so the GO is present when play mode starts) and enter play mode.
            EditorSceneManager.SaveScene(shell);

            // Use delayCall to let Unity finish scene save before entering play mode.
            EditorApplication.delayCall += () =>
            {
                EditorApplication.EnterPlaymode();
            };
        }
    }
}
#endif
