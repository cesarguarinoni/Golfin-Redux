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
        const string LabScenePath   = "Assets/Scenes/Physics/LabScaffold.unity";
        const string Hole06GeoPath  = "Assets/Golf/Courses/lomond-country-club/Generated/Hole_06_Geo.unity";

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
            BotVideoRecorder.Arm();
            Launch("live_stat_provider_visual_gate_high");
        }

        [MenuItem("GOLFIN/Smoke/Loop v2/Live Stat Provider — Low Build")]
        public static void RunLiveStatProviderVisualGateLow()
        {
            BotVideoRecorder.Arm();
            Launch("live_stat_provider_visual_gate_low");
        }

        // stat_to_physics_mapping_audit (2026-05-25):

        [MenuItem("GOLFIN/Smoke/Loop v2/Stat Lane — Surface Roll")]
        public static void RunStatLaneSurfaceRoll()
        {
            BotVideoRecorder.Arm();
            Launch("stat_lane_surface_roll");
        }

        // spin_and_shot_shape_wiring (2026-05-26):

        [MenuItem("GOLFIN/Smoke/Loop v2/Spin And Shape Visual Gate")]
        public static void RunSpinAndShapeVisualGate()
        {
            BotVideoRecorder.Arm();
            Launch("SpinAndShapeVisualGate");
        }

        // practice_1v1_matchmaking_split (2026-06-06):

        [MenuItem("GOLFIN/Smoke/Loop v2/Practice Flow Gate")]
        public static void RunPracticeFlowGate()
        {
            // Records at the full iPhone-14 1170×2532 device preset (see BotVideoRecorder).
            BotVideoRecorder.Arm();
            Launch("practice_flow_gate");
        }

        [MenuItem("GOLFIN/Smoke/Loop v2/Matchmaking 1v1 Gate")]
        public static void RunMatchmaking1v1Gate()
        {
            // Records at the full iPhone-14 1170×2532 device preset (see BotVideoRecorder).
            BotVideoRecorder.Arm();
            Launch("matchmaking_1v1_gate");
        }

        [MenuItem("GOLFIN/Smoke/Loop v2/Matchmaking 1v1 Cancel Gate")]
        public static void RunMatchmaking1v1CancelGate()
        {
            // Records at the full iPhone-14 1170×2532 device preset (see BotVideoRecorder).
            BotVideoRecorder.Arm();
            Launch("matchmaking_1v1_cancel_gate");
        }

        // tree_collisions (Order 348, 2026-06-12): §9 visual gate — trunk strike, canopy hit, control.

        [MenuItem("GOLFIN/Smoke/Loop v2/Tree Collision Gate")]
        public static void RunTreeCollisionGate()
        {
            // Records at full iPhone-14 1170×2532.
            // Direct scene load (LabScaffold+Hole_01_Geo) ~0.5s; 3 shots at ~11s each + waits ~60s.
            // 65s override covers: 5s startup + 0.5s load + 3×(2s place + 13s flight + 2.5s post) = 54s.
            // SessionState-backed so it survives the domain reload between menu-click and Begin().
            BotVideoRecorder.MaxRecordSecondsSessionOverride = 65;
            BotVideoRecorder.Arm();
            Launch("tree_collision_gate");
        }

        // tree_collisions iter-8c: minimal normal-play trunk strike (no camera tricks, normal chase cam)

        [MenuItem("GOLFIN/Smoke/Loop v2/Tree Trunk Normal Play")]
        public static void RunTreeTrunkNormalPlay()
        {
            // Single low/flat shot straight at a bare lower trunk.
            // Normal chase camera — zero camera overrides.
            // Clip: 5s startup + 1s load + 0.5s place + ~8s flight + 3.5s settle = ~18s.
            // 30s override is generous cover.
            BotVideoRecorder.MaxRecordSecondsSessionOverride = 30;
            BotVideoRecorder.Arm();
            Launch("tree_trunk_normal_play");
        }

        [MenuItem("GOLFIN/Smoke/Loop v2/Tree Trunk Normal Play", isValidateFunction: true)]
        static bool ValidateTreeTrunkNormalPlay() => !EditorApplication.isPlaying;

        // ── Order 350 audio fidelity clips ───────────────────────────────────

        [MenuItem("GOLFIN/Smoke/Loop v2/Audio — UI and Music Slider")]
        public static void RunAudioUiMusicSlider()
        {
            // Clip 1: boot to Home (music playing) → open Settings → drag music slider to 5%
            // → UI tap SFX audible. ~20–25s. Audio ON.
            // One-recording-per-session guard applies — reset it first if needed:
            //   GOLFIN > Capture > Reset Video Session Guard
            BotVideoRecorder.CaptureAudio = true;
            BotVideoRecorder.MaxRecordSecondsSessionOverride = 35; // generous cover for 20-25s
            BotVideoRecorder.CustomOutputPath =
                "Docs/Specs/Active/sound_effects/videos/audio_ui_and_music_slider";
            BotVideoRecorder.Arm();
            Launch("audio_ui_music_slider");
        }

        [MenuItem("GOLFIN/Smoke/Loop v2/Audio — Gameplay Shots")]
        public static void RunAudioGameplayShots()
        {
            // Clip 2: real ShellScene boot → Practice card → Hole 1 → music quieted →
            // PlayHoleToCup. Cesar hears swing+hit+land+cup over nearly-silent music.
            // Practice path bypasses matchmaking, loads hole directly. ~50–80s. Audio ON.
            // Cap = 90s to cover worst-case hole load (~35s) + shots (~30s).
            BotVideoRecorder.CaptureAudio = true;
            BotVideoRecorder.MaxRecordSecondsSessionOverride = 90;
            BotVideoRecorder.CustomOutputPath =
                "Docs/Specs/Active/sound_effects/videos/audio_gameplay_shots";
            BotVideoRecorder.Arm();
            Launch("audio_gameplay_shots");
        }

        [MenuItem("GOLFIN/Smoke/Loop v2/Audio — Gameplay Shots V3 (Hit Audibility Fix)")]
        public static void RunAudioGameplayShotsV3()
        {
            // Clip 3 (v3): deferred-start recording — begins AFTER hole is fully loaded so no
            // Y-flip first frame. Mid-power (0.5) first stroke → HitDefault (vol 1.0 > swing 0.55).
            // Cap = 45s covers the recorded gameplay segment (~25-35s).
            BotVideoRecorder.CaptureAudio = true;
            BotVideoRecorder.MaxRecordSecondsSessionOverride = 45;
            BotVideoRecorder.CustomOutputPath =
                "Docs/Specs/Active/sound_effects/videos/audio_gameplay_v3";
            BotVideoRecorder.ArmDeferred();   // deferred start — scenario calls BeginDeferred()
            Launch("audio_gameplay_v3");
        }

        [MenuItem("GOLFIN/Smoke/Loop v2/Audio — Gameplay Shots V3 (Hit Audibility Fix)", isValidateFunction: true)]
        static bool ValidateAudioGameplayShotsV3() => !EditorApplication.isPlaying;

        // Order 350 focused fidelity clips (2026-06-16):

        [MenuItem("GOLFIN/Smoke/Loop v2/Audio — Putt To Cup (Focused Clip)")]
        public static void RunAudioPuttToCup()
        {
            // Clip: real ShellScene boot → Practice → Hole 1 → music quieted → putt into cup.
            // Deferred start: recording begins mid-coroutine after hole fully loaded (no Y-flip).
            // Target: HitPutt audible, NO settle-thud (putt suppression), HitBallIn at cup.
            // 25s covers: 5s startup + hole load ~15s + putt flight ~3s + settle ~2s.
            BotVideoRecorder.CaptureAudio = true;
            BotVideoRecorder.MaxRecordSecondsSessionOverride = 25;
            BotVideoRecorder.CustomOutputPath =
                "Docs/Specs/Active/sound_effects/videos/audio_putt_to_cup";
            BotVideoRecorder.ArmDeferred();   // deferred start — scenario calls BeginDeferred()
            Launch("audio_putt_to_cup");
        }

        [MenuItem("GOLFIN/Smoke/Loop v2/Audio — Putt To Cup (Focused Clip)", isValidateFunction: true)]
        static bool ValidateAudioPuttToCup() => !EditorApplication.isPlaying;

        [MenuItem("GOLFIN/Smoke/Loop v2/Audio — Water Splash SFX (Focused Clip)")]
        public static void RunAudioWaterSplashSfx()
        {
            // Clip: real ShellScene boot → Practice → HoleSelection → Hole 6 (unlocked at
            // runtime) → music quieted → Driver aimed at water → LandWater SFX + splash VFX.
            // ShellScene boot is REQUIRED for AudioManager init; direct LabScaffold bypasses
            // it and produces silent audio (-91 dB) even with CaptureAudio=true.
            // 30s covers: hole load ~15s + 4s settle + 1s arm + 5s flight + 5s dwell.
            BotVideoRecorder.CaptureAudio = true;
            BotVideoRecorder.MaxRecordSecondsSessionOverride = 30;
            BotVideoRecorder.CustomOutputPath =
                "Docs/Specs/Active/sound_effects/videos/audio_water_splash_sfx";
            BotVideoRecorder.ArmDeferred();   // deferred start — scenario calls BeginDeferred()
            Launch("audio_water_splash_sfx");
        }

        [MenuItem("GOLFIN/Smoke/Loop v2/Audio — Water Splash SFX (Focused Clip)", isValidateFunction: true)]
        static bool ValidateAudioWaterSplashSfx() => !EditorApplication.isPlaying;

        [MenuItem("GOLFIN/Smoke/Loop v2/Audio — Match Stinger (Focused Clip)")]
        public static void RunAudioMatchStinger()
        {
            // Clip: real ShellScene boot → HomeScreen → ClickModeCardPlay("versus_1v1") →
            // matchmaking → hole loads → VersusMatchController._debugBothBots=true → match runs
            // to completion → stinger SFX fires at VersusResultHandler.HandleMatchComplete.
            // ShellScene boot is REQUIRED for AudioManager init; VersusHudCaptureMenu.Launch()
            // opens LabScaffold directly and produces silent audio (-91 dB) even with CaptureAudio=true.
            // 120s covers: ShellScene boot ~5s + matchmaking ~10s + hole load ~20s + match play ~90s
            // (worst-case non-hole-4, from tee) — actual typical time for hole 4 near-green is ~25s.
            BotVideoRecorder.CaptureAudio = true;
            BotVideoRecorder.MaxRecordSecondsSessionOverride = 120;
            BotVideoRecorder.CustomOutputPath =
                "Docs/Specs/Active/sound_effects/videos/audio_match_stinger";
            BotVideoRecorder.ArmDeferred();   // deferred start — scenario calls Begin() mid-coroutine
            Launch("audio_match_stinger");
        }

        [MenuItem("GOLFIN/Smoke/Loop v2/Audio — Match Stinger (Focused Clip)", isValidateFunction: true)]
        static bool ValidateAudioMatchStinger() => !EditorApplication.isPlaying;

        [MenuItem("GOLFIN/Smoke/Loop v2/Audio — UI and Music Slider", isValidateFunction: true)]
        static bool ValidateAudioUiMusicSlider() => !EditorApplication.isPlaying;

        [MenuItem("GOLFIN/Smoke/Loop v2/Audio — Gameplay Shots", isValidateFunction: true)]
        static bool ValidateAudioGameplayShots() => !EditorApplication.isPlaying;

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

        [MenuItem("GOLFIN/Smoke/Loop v2/Spin And Shape Visual Gate", isValidateFunction: true)]
        static bool ValidateSpinAndShapeVisualGate() => !EditorApplication.isPlaying;

        [MenuItem("GOLFIN/Smoke/Loop v2/Practice Flow Gate", isValidateFunction: true)]
        static bool ValidatePracticeFlowGate() => !EditorApplication.isPlaying;

        [MenuItem("GOLFIN/Smoke/Loop v2/Matchmaking 1v1 Gate", isValidateFunction: true)]
        static bool ValidateMatchmaking1v1Gate() => !EditorApplication.isPlaying;

        [MenuItem("GOLFIN/Smoke/Loop v2/Matchmaking 1v1 Cancel Gate", isValidateFunction: true)]
        static bool ValidateMatchmaking1v1CancelGate() => !EditorApplication.isPlaying;

        [MenuItem("GOLFIN/Smoke/Loop v2/Tree Collision Gate", isValidateFunction: true)]
        static bool ValidateTreeCollisionGate() => !EditorApplication.isPlaying;

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

        // ── Direct-lab launcher (no ShellScene) ──────────────────────────────

        /// <summary>
        /// Opens LabScaffold (single mode) + the specified geo scene (additive),
        /// then arms the scenario and enters play mode — without ShellScene.
        ///
        /// The grey-water fix lives in PhysicsLabController.OnHoleLoaded, which
        /// disables the duplicate ShellScene directional light even in a direct
        /// LabScaffold load. Water renders correctly without ShellScene.
        /// </summary>
        static void LaunchDirectLab(string holeGeoPath, string scenarioKey)
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("[LoopV2SmokeBotMenu] Stop play mode first before launching a direct-lab scenario.");
                return;
            }

            Debug.Log($"[LoopV2SmokeBotMenu] LaunchDirectLab: scenario='{scenarioKey}' geo='{holeGeoPath}'");

            // 1. Open LabScaffold as the host (single mode — never saves).
            var labScene = EditorSceneManager.OpenScene(LabScenePath, OpenSceneMode.Single);
            if (!labScene.IsValid())
            {
                Debug.LogError($"[LoopV2SmokeBotMenu] Failed to open LabScaffold at {LabScenePath}");
                return;
            }

            // 2. Open the hole geo additively.
            var geoScene = EditorSceneManager.OpenScene(holeGeoPath, OpenSceneMode.Additive);
            if (!geoScene.IsValid())
                Debug.LogWarning($"[LoopV2SmokeBotMenu] Failed to open geo scene '{holeGeoPath}' — PhysicsLabController will load it at runtime.");

            // 3. Arm via SessionState (mirrors Launch()).
            Golfin.Physics.Viewer.LoopV2SmokeBot.Scenario = scenarioKey;
            Golfin.Physics.Viewer.LoopV2SmokeBot.Armed    = true;

            // 4. DisableSceneReload guard (same as Launch()).
            bool hadSceneReloadDisabled = EditorSettings.enterPlayModeOptionsEnabled &&
                (EditorSettings.enterPlayModeOptions & EnterPlayModeOptions.DisableSceneReload) != 0;
            if (hadSceneReloadDisabled)
            {
                Debug.LogWarning("[LoopV2SmokeBotMenu] DisableSceneReload detected — temporarily enabling scene reload for this run.");
                EditorSettings.enterPlayModeOptions &= ~EnterPlayModeOptions.DisableSceneReload;
            }
            UnityEditor.SessionState.SetBool("LoopV2SmokeBot.RestoreSceneReload", hadSceneReloadDisabled);

            Debug.Log($"[LoopV2SmokeBotMenu] DirectLab armed. Scenario='{scenarioKey}'. Entering play mode…");
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
