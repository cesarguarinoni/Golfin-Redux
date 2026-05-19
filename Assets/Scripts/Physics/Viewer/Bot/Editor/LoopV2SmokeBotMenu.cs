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
    /// at EnteredPlayMode via playModeStateChanged, never saved to disk. ShellScene is
    /// never mutated by the launcher — zero scene contamination.
    ///
    /// Robustness note: [InitializeOnLoadMethod] re-registers the playModeStateChanged
    /// handler after every domain reload, so it survives the compile cycle that may
    /// occur between clicking the menu item and Unity actually entering play mode.
    /// The handler is a no-op unless SessionState Armed=true.
    ///
    /// Each menu item:
    ///   1. Verifies not already in play mode.
    ///   2. Opens ShellScene.unity (single mode — never saves).
    ///   3. Sets SessionState scenario key + armed flag.
    ///   4. Enters play mode. The playModeStateChanged handler (re-registered on every
    ///      domain reload by [InitializeOnLoadMethod]) injects the host GO at
    ///      EnteredPlayMode time. The GO is never saved to disk.
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

            // 1. Open ShellScene (single mode). Does NOT save the file.
            var shell = EditorSceneManager.OpenScene(ShellScenePath, OpenSceneMode.Single);
            if (!shell.IsValid())
            {
                Debug.LogError($"[LoopV2SmokeBotMenu] Failed to open {ShellScenePath}");
                return;
            }

            // 2. Arm via SessionState (survives domain reloads; does NOT touch scene files).
            Golfin.Physics.Viewer.LoopV2SmokeBot.Scenario = scenarioKey;
            Golfin.Physics.Viewer.LoopV2SmokeBot.Armed    = true;

            Debug.Log($"[LoopV2SmokeBotMenu] Armed. Scenario='{scenarioKey}'. Entering play mode…");

            // 3. Enter play mode. The [InitializeOnLoadMethod]-registered handler will
            //    fire at EnteredPlayMode and inject the host GO (because Armed=true).
            EditorApplication.EnterPlaymode();
        }

        // ── Play-mode injection handler ───────────────────────────────────────

        /// <summary>
        /// Re-register the playModeStateChanged handler after every domain reload.
        /// This ensures the handler survives any compile cycle that occurs between
        /// clicking the menu item and Unity actually entering play mode.
        /// The handler is a no-op unless SessionState Armed=true.
        /// </summary>
        [UnityEditor.Callbacks.DidReloadScripts]
        static void OnScriptsReloaded()
        {
            // Remove then re-add to avoid double-subscription.
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode) return;

            // Only inject if the bot is armed (set by a menu item invocation).
            if (!Golfin.Physics.Viewer.LoopV2SmokeBot.Armed) return;

            // Create the host GO in-memory (in the play-mode scene). Never saved to disk.
            var go = new GameObject("[LoopV2SmokeBot]");
            go.AddComponent<Golfin.Physics.Viewer.LoopV2SmokeBot>();
            Debug.Log($"[LoopV2SmokeBotMenu] Injected [LoopV2SmokeBot] host into play-mode scene " +
                      $"(scenario={Golfin.Physics.Viewer.LoopV2SmokeBot.Scenario}, not saved to disk).");
        }
    }
}
#endif
