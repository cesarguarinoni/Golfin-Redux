// iter-4b: ExitingPlayMode restore pattern (fix Time.time=0 freeze from EnteredPlayMode restore)
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

        // Stage C1 menu items:

        [MenuItem("GOLFIN/Smoke/Loop v2/C1 - Hole 1 Play Next")]
        public static void RunHole1PlayNext()        => Launch("hole1_play_next");

        [MenuItem("GOLFIN/Smoke/Loop v2/C1 - Hole 1 Menu")]
        public static void RunHole1Menu()            => Launch("hole1_menu");

        [MenuItem("GOLFIN/Smoke/Loop v2/C1 - Hole 1 Retry After Fail")]
        public static void RunHole1RetryAfterFail()  => Launch("hole1_retry_after_fail");

        [MenuItem("GOLFIN/Smoke/Loop v2/C1 - Hole 18 Course Cleared")]
        public static void RunHole18CourseCleared()  => Launch("hole18_course_cleared");

        // Stage E menu item:

        [MenuItem("GOLFIN/Smoke/Loop v2/Hole Selection Entry → Replay Rewards")]
        public static void RunHoleSelectionEntryToReplayRewards()
            => Launch("hole_selection_entry_to_replay_rewards");

        // Save layer durability:

        [MenuItem("GOLFIN/Smoke/Loop v2/Save Layer Durability")]
        public static void RunSaveLayerDurability()
            => Launch("save_layer_durability");

        // Putter green reader smoke test:

        [MenuItem("GOLFIN/Smoke/Loop v2/Putter Aim Green Reader Visible")]
        public static void RunPutterAimGreenReaderVisible()
            => Launch("putter_aim_green_reader_visible");

        // Iter-2 warped grid visual gate (TestGreen scene):

        [MenuItem("GOLFIN/Smoke/Loop v2/Putter Aim Warped Grid On TestGreen")]
        public static void RunPutterAimWarpedGridOnTestGreen()
            => Launch("putter_aim_warped_grid_on_test_green");

        // Live stat provider visual gate (live_stat_provider_wiring task):

        [MenuItem("GOLFIN/Smoke/Loop v2/Live Stat Provider — High Build")]
        public static void RunLiveStatProviderVisualGateHigh()
        {
            BotVideoRecorder.RecordVideo = true;
            Launch("live_stat_provider_visual_gate_high");
        }

        [MenuItem("GOLFIN/Smoke/Loop v2/Live Stat Provider — Low Build")]
        public static void RunLiveStatProviderVisualGateLow()
        {
            BotVideoRecorder.RecordVideo = true;
            Launch("live_stat_provider_visual_gate_low");
        }

        // stat_to_physics_mapping_audit (2026-05-25):

        [MenuItem("GOLFIN/Smoke/Loop v2/Stat Lane — Surface Roll")]
        public static void RunStatLaneSurfaceRoll()
        {
            BotVideoRecorder.RecordVideo = true;
            Launch("stat_lane_surface_roll");
        }

        // ── Validation items (disable menu entries when in play mode) ─────────

        [MenuItem("GOLFIN/Smoke/Loop v2/Hole 1 Playthrough", isValidateFunction: true)]
        static bool ValidateRunHole1()               => !EditorApplication.isPlaying;

        [MenuItem("GOLFIN/Smoke/Loop v2/Settings Round Trip", isValidateFunction: true)]
        static bool ValidateRunSettings()            => !EditorApplication.isPlaying;

        [MenuItem("GOLFIN/Smoke/Loop v2/Hole Selection Browse", isValidateFunction: true)]
        static bool ValidateRunHoleSelection()       => !EditorApplication.isPlaying;

        [MenuItem("GOLFIN/Smoke/Loop v2/C1 - Hole 1 Play Next", isValidateFunction: true)]
        static bool ValidateHole1PlayNext()          => !EditorApplication.isPlaying;

        [MenuItem("GOLFIN/Smoke/Loop v2/C1 - Hole 1 Menu", isValidateFunction: true)]
        static bool ValidateHole1Menu()              => !EditorApplication.isPlaying;

        [MenuItem("GOLFIN/Smoke/Loop v2/C1 - Hole 1 Retry After Fail", isValidateFunction: true)]
        static bool ValidateHole1RetryAfterFail()    => !EditorApplication.isPlaying;

        [MenuItem("GOLFIN/Smoke/Loop v2/C1 - Hole 18 Course Cleared", isValidateFunction: true)]
        static bool ValidateHole18CourseCleared()    => !EditorApplication.isPlaying;

        [MenuItem("GOLFIN/Smoke/Loop v2/Hole Selection Entry → Replay Rewards", isValidateFunction: true)]
        static bool ValidateHoleSelectionEntryToReplayRewards() => !EditorApplication.isPlaying;

        [MenuItem("GOLFIN/Smoke/Loop v2/Save Layer Durability", isValidateFunction: true)]
        static bool ValidateSaveLayerDurability() => !EditorApplication.isPlaying;

        [MenuItem("GOLFIN/Smoke/Loop v2/Putter Aim Green Reader Visible", isValidateFunction: true)]
        static bool ValidatePutterAimGreenReaderVisible() => !EditorApplication.isPlaying;

        [MenuItem("GOLFIN/Smoke/Loop v2/Putter Aim Warped Grid On TestGreen", isValidateFunction: true)]
        static bool ValidatePutterAimWarpedGridOnTestGreen() => !EditorApplication.isPlaying;

        [MenuItem("GOLFIN/Smoke/Loop v2/Live Stat Provider — High Build", isValidateFunction: true)]
        static bool ValidateLiveStatProviderVisualGateHigh() => !EditorApplication.isPlaying;

        [MenuItem("GOLFIN/Smoke/Loop v2/Live Stat Provider — Low Build", isValidateFunction: true)]
        static bool ValidateLiveStatProviderVisualGateLow() => !EditorApplication.isPlaying;

        [MenuItem("GOLFIN/Smoke/Loop v2/Stat Lane — Surface Roll", isValidateFunction: true)]
        static bool ValidateStatLaneSurfaceRoll() => !EditorApplication.isPlaying;

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

            // 3. Guard against "Enter Play Mode Options: Disable Scene Reload".
            //    If DisableSceneReload is set, Unity won't reinitialize the scene state
            //    between sessions — the previous session's ScreenManager/MonoBehaviour state
            //    persists and the game loop freezes at frame 1 (Time.time stays ~0.02).
            //    Temporarily force full reload for this play mode entry only.
            bool hadSceneReloadDisabled = EditorSettings.enterPlayModeOptionsEnabled &&
                (EditorSettings.enterPlayModeOptions & EnterPlayModeOptions.DisableSceneReload) != 0;
            if (hadSceneReloadDisabled)
            {
                Debug.LogWarning("[LoopV2SmokeBotMenu] DisableSceneReload detected — temporarily enabling scene reload for this run.");
                EditorSettings.enterPlayModeOptions &= ~EnterPlayModeOptions.DisableSceneReload;
            }

            // Store flag in SessionState so the handler can restore the option after EnteredPlayMode.
            UnityEditor.SessionState.SetBool("LoopV2SmokeBot.RestoreSceneReload", hadSceneReloadDisabled);

            Debug.Log($"[LoopV2SmokeBotMenu] Armed. Scenario='{scenarioKey}'. Entering play mode…");

            // 4. Enter play mode. The [DidReloadScripts]-registered handler will
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
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                // NOTE: Do NOT touch EditorSettings.enterPlayModeOptions here.
                // Modifying DisableSceneReload at EnteredPlayMode causes Unity to
                // freeze the game loop (Time.time stays 0) — the option change during
                // play mode triggers an internal scene state reset. Restore is deferred
                // to ExitingPlayMode where it is safe.

                // Only inject the host if the bot is armed (set by a menu item invocation).
                if (!Golfin.Physics.Viewer.LoopV2SmokeBot.Armed) return;

                // Headless play-loop guard. When the Unity Editor is not the foreground
                // app (every automated/MCP-driven run), Unity throttles the player loop
                // to a halt if runInBackground is off — the game freezes at frame 1 with
                // Time.time stuck near 0. Application.runInBackground is a RUNTIME flag:
                // setting it here (at EnteredPlayMode, before frame 1) keeps the loop
                // ticking unattended and does NOT mutate ProjectSettings.asset, so it
                // leaves zero git-diff footprint. Reverts automatically when play ends.
                Application.runInBackground = true;

                // Create the host GO in-memory (in the play-mode scene). Never saved to disk.
                var go = new GameObject("[LoopV2SmokeBot]");
                go.AddComponent<Golfin.Physics.Viewer.LoopV2SmokeBot>();
                Debug.Log($"[LoopV2SmokeBotMenu] Injected [LoopV2SmokeBot] host into play-mode scene " +
                          $"(scenario={Golfin.Physics.Viewer.LoopV2SmokeBot.Scenario}, not saved to disk).");

                // Optional demo-video capture via the Unity Recorder. No-op unless
                // BotVideoRecorder.RecordVideo is armed. Editor-driven — no scene object.
                BotVideoRecorder.Begin();
            }
            else if (state == PlayModeStateChange.ExitingPlayMode)
            {
                // Finalize any in-progress demo-video recording before play mode tears down.
                BotVideoRecorder.End();

                // Safe point to restore DisableSceneReload — play mode is ending, no game loop impact.
                bool restore = UnityEditor.SessionState.GetBool("LoopV2SmokeBot.RestoreSceneReload", false);
                if (restore)
                {
                    EditorSettings.enterPlayModeOptions |= EnterPlayModeOptions.DisableSceneReload;
                    UnityEditor.SessionState.SetBool("LoopV2SmokeBot.RestoreSceneReload", false);
                    Debug.Log("[LoopV2SmokeBotMenu] Restored DisableSceneReload option (at ExitingPlayMode).");
                }
            }
        }
    }
}
#endif
