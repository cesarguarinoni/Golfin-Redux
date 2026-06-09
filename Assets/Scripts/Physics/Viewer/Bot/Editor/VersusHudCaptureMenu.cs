#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Golfin.Physics.Viewer.Editor
{
    /// <summary>
    /// Menu launcher for 1v1 versus HUD video capture scenarios (Phase 1).
    ///
    /// Follows the LoopV2SmokeBotMenu pattern (Option B host-creation):
    ///   - [DidReloadScripts] re-registers the playModeStateChanged handler after
    ///     every domain reload — so it survives the compile cycle between menu click
    ///     and actual play mode entry.
    ///   - The scenario key is stored in SessionState (survives domain reloads).
    ///     At EnteredPlayMode, the handler re-reads the key from SessionState and
    ///     writes it into VersusHudCaptureBot.Scenario (runtime static, NOT persistent).
    ///   - The [VersusHudCaptureBot] host GO is injected at EnteredPlayMode and
    ///     NEVER saved to disk.
    ///   - BotVideoRecorder captures the session to
    ///     tasks/loop_v2_smoke_bot/<scenario>/video/raw.mp4.
    ///
    /// iter-4 update: HUD scenarios open LabScaffold + Hole_18_Geo additively so the
    /// real course is visible behind the HUD (standing rule feedback_capture_resolution_iphone14).
    /// nav_menu scenario opens ShellScene (production flow) for the end-to-end navigation video.
    /// </summary>
    public static class VersusHudCaptureMenu
    {
        const string LabScenePath  = "Assets/Scenes/Physics/LabScaffold.unity";
        // Hole_18_Geo loaded additively behind the HUD for Defect-5 fix.
        const string GeoScenePath  = "Assets/Golf/Courses/lomond-country-club/Generated/Hole_18_Geo.unity";
        const string ShellScenePath = "Assets/Scenes/ShellScene.unity";
        const string ScenarioKey   = "VersusHudCaptureMenu.Scenario";
        const string ArmedKey      = "VersusHudCaptureMenu.Armed";
        const string RestoreReloadKey = "VersusHudCaptureMenu.RestoreSceneReload";

        // ── Menu items ──────────────────────────────────────────────────────

        [MenuItem("GOLFIN/Capture 1v1/Record Versus Launch")]
        public static void RecordVersusLaunch()
        {
            BotVideoRecorder.Arm();
            Launch("versus_launch");
        }

        [MenuItem("GOLFIN/Capture 1v1/Record Turn Swap")]
        public static void RecordTurnSwap()
        {
            BotVideoRecorder.Arm();
            Launch("turn_swap");
        }

        [MenuItem("GOLFIN/Capture 1v1/Record Banner Show")]
        public static void RecordBannerShow()
        {
            BotVideoRecorder.Arm();
            Launch("banner_show");
        }

        [MenuItem("GOLFIN/Capture 1v1/Record Solo Regression")]
        public static void RecordSoloRegression()
        {
            BotVideoRecorder.Arm();
            Launch("solo_regression");
        }

        /// <summary>
        /// Records the full production navigation flow: Home → Mode Select → 1v1 →
        /// Matchmaking (OPPONENT FOUND) → in-game HUD (both cards full, P1 active) →
        /// opponent's turn trigger. Starts from ShellScene, not LabScaffold.
        /// </summary>
        [MenuItem("GOLFIN/Capture 1v1/Record Nav Menu to Opponent Turn")]
        public static void RecordNavMenu()
        {
            BotVideoRecorder.Arm();
            LaunchNavMenu("nav_menu_to_opponent_turn");
        }

        // ── Validation ──────────────────────────────────────────────────────

        [MenuItem("GOLFIN/Capture 1v1/Record Versus Launch", isValidateFunction: true)]
        static bool ValidateVersusLaunch() => !EditorApplication.isPlaying;

        [MenuItem("GOLFIN/Capture 1v1/Record Turn Swap", isValidateFunction: true)]
        static bool ValidateTurnSwap() => !EditorApplication.isPlaying;

        [MenuItem("GOLFIN/Capture 1v1/Record Banner Show", isValidateFunction: true)]
        static bool ValidateBannerShow() => !EditorApplication.isPlaying;

        [MenuItem("GOLFIN/Capture 1v1/Record Solo Regression", isValidateFunction: true)]
        static bool ValidateSoloRegression() => !EditorApplication.isPlaying;

        [MenuItem("GOLFIN/Capture 1v1/Record Nav Menu to Opponent Turn", isValidateFunction: true)]
        static bool ValidateNavMenu() => !EditorApplication.isPlaying;

        // ── Launcher (HUD scenarios — LabScaffold + Hole_18_Geo) ─────────────

        static void Launch(string scenarioKey)
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("[VersusHudCaptureMenu] Stop play mode first before launching a scenario.");
                return;
            }

            Debug.Log($"[VersusHudCaptureMenu] Launching scenario: '{scenarioKey}'");

            // 1. Open LabScaffold (single mode) — the HUD host scene.
            var labScene = EditorSceneManager.OpenScene(LabScenePath, OpenSceneMode.Single);
            if (!labScene.IsValid())
            {
                Debug.LogError($"[VersusHudCaptureMenu] Failed to open {LabScenePath}");
                return;
            }

            // 2. Also load Hole_18_Geo ADDITIVELY so the real course is visible behind the HUD.
            //    This satisfies the standing rule: capture must be over a real loaded hole,
            //    not the flat-ground LabScaffold default. (Defect-5 fix, iter-4.)
            var geoScene = EditorSceneManager.OpenScene(GeoScenePath, OpenSceneMode.Additive);
            if (!geoScene.IsValid())
            {
                Debug.LogWarning($"[VersusHudCaptureMenu] Hole_18_Geo not loaded — recording will show empty scene background.");
            }

            ArmAndEnterPlayMode(scenarioKey);
        }

        // ── Launcher (nav_menu scenario — ShellScene production flow) ────────

        static void LaunchNavMenu(string scenarioKey)
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("[VersusHudCaptureMenu] Stop play mode first.");
                return;
            }

            Debug.Log($"[VersusHudCaptureMenu] Launching nav scenario: '{scenarioKey}'");

            // Nav scenario: open ShellScene (the real production scene) single mode.
            // The nav bot will drive the UI from Home → ModeSelect → 1v1 matchmaking.
            // The matchmaking controller loads LabScaffold + a geo scene via GameplaySceneLoader.
            var shellScene = EditorSceneManager.OpenScene(ShellScenePath, OpenSceneMode.Single);
            if (!shellScene.IsValid())
            {
                Debug.LogError($"[VersusHudCaptureMenu] Failed to open {ShellScenePath}");
                return;
            }

            ArmAndEnterPlayMode(scenarioKey);
        }

        static void ArmAndEnterPlayMode(string scenarioKey)
        {
            // Arm via SessionState (survives domain reloads between Launch() and EnteredPlayMode).
            SessionState.SetString(ScenarioKey, scenarioKey);
            SessionState.SetBool(ArmedKey, true);

            // Also set LoopV2SmokeBot.Scenario so BotVideoRecorder.Begin() uses the correct
            // output folder (tasks/loop_v2_smoke_bot/<scenarioKey>/video/raw.mp4).
            Golfin.Physics.Viewer.LoopV2SmokeBot.Scenario = scenarioKey;

            // Guard against DisableSceneReload (mirrors LoopV2SmokeBotMenu).
            bool hadSceneReloadDisabled = EditorSettings.enterPlayModeOptionsEnabled &&
                (EditorSettings.enterPlayModeOptions & EnterPlayModeOptions.DisableSceneReload) != 0;
            if (hadSceneReloadDisabled)
            {
                Debug.LogWarning("[VersusHudCaptureMenu] DisableSceneReload detected — temporarily enabling scene reload for this run.");
                EditorSettings.enterPlayModeOptions &= ~EnterPlayModeOptions.DisableSceneReload;
            }
            SessionState.SetBool(RestoreReloadKey, hadSceneReloadDisabled);

            Debug.Log($"[VersusHudCaptureMenu] Armed (SessionState). Scenario='{scenarioKey}'. Entering play mode…");

            // Enter play mode. The [DidReloadScripts]-registered handler will inject
            // the bot GO at EnteredPlayMode (reading scenario from SessionState).
            EditorApplication.EnterPlaymode();
        }

        // ── Play-mode injection handler ─────────────────────────────────────

        /// <summary>
        /// Re-register after every domain reload so the handler survives any compile
        /// cycle that occurs between menu click and actual play mode entry.
        /// No-op unless SessionState Armed=true.
        /// </summary>
        [UnityEditor.Callbacks.DidReloadScripts]
        static void OnScriptsReloaded()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                // Read armed flag from SessionState (survives the domain reload).
                if (!SessionState.GetBool(ArmedKey, false)) return;
                SessionState.SetBool(ArmedKey, false);   // clear immediately

                string scenario = SessionState.GetString(ScenarioKey, "");

                // Keep the player loop ticking in background (headless/MCP-driven runs).
                Application.runInBackground = true;

                if (scenario == "nav_menu_to_opponent_turn")
                {
                    // Nav scenario: inject the nav bot (drives ShellScene UI flow).
                    Golfin.Physics.Viewer.VersusHudNavCaptureBot.Armed = true;
                    var go = new GameObject("[VersusHudNavCaptureBot]");
                    Object.DontDestroyOnLoad(go);
                    go.AddComponent<Golfin.Physics.Viewer.VersusHudNavCaptureBot>();
                    Debug.Log($"[VersusHudCaptureMenu] Injected [VersusHudNavCaptureBot] " +
                              $"(scenario='{scenario}', not saved to disk).");
                }
                else
                {
                    // HUD scenarios: inject the standard capture bot.
                    // Transfer scenario to the runtime bot's static properties.
                    // Static fields ARE cleared on domain reload, so we must re-set here
                    // (after the reload) from the SessionState values we persisted above.
                    Golfin.Physics.Viewer.VersusHudCaptureBot.Scenario = scenario;
                    Golfin.Physics.Viewer.VersusHudCaptureBot.Armed    = true;

                    // ── DEFECT-1 FIX (iter-5): Pre-seed MatchContext + GameSession BEFORE
                    // the bot GO is injected, so VersusHudController.Start() and
                    // OnMatchContextChanged() see real data from the FIRST frame.
                    // EnteredPlayMode fires after all scene Awake/Start/OnEnable have run,
                    // so calling Raise() here triggers the reactive OnChanged subscription
                    // that VersusHudController registered in OnEnable() — both cards refresh
                    // with real data before the banner animation even starts.
                    if (scenario != "solo_regression")
                    {
                        Golfin.Gameplay.Session.GameSession.IsVersus = true;
                        Golfin.Gameplay.UI.HUD.MatchContext.Players[0] = new Golfin.Gameplay.UI.HUD.MatchContext.Player
                        {
                            DisplayName = "CAMILA",
                            Level       = 13,
                            TurnCount   = 1
                        };
                        Golfin.Gameplay.UI.HUD.MatchContext.Players[1] = new Golfin.Gameplay.UI.HUD.MatchContext.Player
                        {
                            DisplayName = "TARO",
                            Level       = 17,
                            TurnCount   = 0
                        };
                        Golfin.Gameplay.UI.HUD.MatchContext.ActiveIndex = 0;
                        Golfin.Gameplay.UI.HUD.MatchContext.Raise();

                        Golfin.Gameplay.UI.HUD.PlayerContext.DisplayName = "CAMILA";
                        Golfin.Gameplay.UI.HUD.PlayerContext.Level       = 13;
                        Golfin.Gameplay.UI.HUD.PlayerContext.Raise();

                        Debug.Log("[VersusHudCaptureMenu] DEFECT-1 FIX: MatchContext pre-seeded before bot injection. " +
                                  "Both cards will show real data from frame 1.");
                    }

                    // Inject the host GO in-memory (play-mode scene only — never saved to disk).
                    var go = new GameObject("[VersusHudCaptureBot]");
                    go.AddComponent<Golfin.Physics.Viewer.VersusHudCaptureBot>();
                    Debug.Log($"[VersusHudCaptureMenu] Injected [VersusHudCaptureBot] host " +
                              $"(scenario='{scenario}', not saved to disk).");
                }

                // Start the Unity Recorder capture (armed by BotVideoRecorder.Arm() in Launch()).
                BotVideoRecorder.Begin();
            }
            else if (state == PlayModeStateChange.ExitingPlayMode)
            {
                BotVideoRecorder.End();

                // Restore DisableSceneReload if we temporarily cleared it.
                bool restore = SessionState.GetBool(RestoreReloadKey, false);
                if (restore)
                {
                    EditorSettings.enterPlayModeOptions |= EnterPlayModeOptions.DisableSceneReload;
                    SessionState.SetBool(RestoreReloadKey, false);
                    Debug.Log("[VersusHudCaptureMenu] Restored DisableSceneReload option (at ExitingPlayMode).");
                }
            }
        }
    }
}
#endif
