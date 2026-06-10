#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

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
    /// iter-4 update: HUD scenarios open LabScaffold + Hole_04_Geo additively so the
    /// real course is visible behind the HUD (standing rule feedback_capture_resolution_iphone14).
    /// Hole_04 is a par-3 (~110m tee→pin) so the versus_full_match_flow scenario resolves
    /// comfortably inside the 30s BotVideoRecorder watchdog (Cesar decision 2026-06-09).
    /// nav_menu scenario opens ShellScene (production flow) for the end-to-end navigation video.
    /// </summary>
    public static class VersusHudCaptureMenu
    {
        const string LabScenePath  = "Assets/Scenes/Physics/LabScaffold.unity";
        // Hole_04_Geo (par-3, tee→pin ~110m) loaded additively behind the HUD.
        // For the versus_full_match_flow scenario, Hole_04 is used so the full match
        // resolves inside the 30s BotVideoRecorder watchdog (iron shots ~3-5s vs 16s driver).
        // All other HUD scenarios also load Hole_04 as the background course.
        // CAPTURE SCENARIO ONLY — production 1v1 route is unaffected.
        const string GeoScenePath  = "Assets/Golf/Courses/lomond-country-club/Generated/Hole_04_Geo.unity";
        const string ShellScenePath = "Assets/Scenes/ShellScene.unity";
        const string ScenarioKey   = "VersusHudCaptureMenu.Scenario";
        const string ArmedKey      = "VersusHudCaptureMenu.Armed";
        const string RestoreReloadKey = "VersusHudCaptureMenu.RestoreSceneReload";
        // BUG A fix (iter-5): store the real tee world position from the loaded geo scene so the
        // versus_full_match_flow seeding block can set Players[i].Lie correctly.
        const string TeeLieXKey    = "VersusHudCaptureMenu.TeeLie.x";
        const string TeeLieYKey    = "VersusHudCaptureMenu.TeeLie.y";
        const string TeeLieZKey    = "VersusHudCaptureMenu.TeeLie.z";

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
        /// Phase 2a visual gate: records a full 1v1 match from turn 1 to WIN/LOSE/DRAW banner.
        /// Both P1 and P2 are driven by VersusBot (debug full-bot mode).
        /// Output: tasks/loop_v2_smoke_bot/versus_full_match_flow/video/raw.mp4
        /// </summary>
        [MenuItem("GOLFIN/Capture 1v1/Record Full Match Flow (Phase 2a)")]
        public static void RecordFullMatchFlow()
        {
            BotVideoRecorder.Arm();
            Launch("versus_full_match_flow");
        }

        [MenuItem("GOLFIN/Capture 1v1/Record Full Match Flow (Phase 2a)", isValidateFunction: true)]
        static bool ValidateFullMatchFlow() => !EditorApplication.isPlaying;

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

        /// <summary>
        /// Clip B — resolution proof clip (iter-9 two-clip strategy, Cesar 2026-06-10).
        /// Seeds BOTH players' Lie to a near-green position (~3m from Hole_04 pin) so the
        /// match resolves quickly (≤25s), within the default 30s watchdog.
        /// CRITICAL: This near-green seeding is ISOLATED to this scenario. The
        /// versus_full_match_flow scenario still starts from the real tee (BUG-A fix intact).
        /// Purpose: pixel-confirm that HideShotUI() (Defect 1 fix) produces a CLEAN banner
        /// with NO aim cone / power bar / power dial / putter graphic / trajectory line.
        /// Output: tasks/loop_v2_smoke_bot/versus_resolution_clip/video/raw.mp4
        /// </summary>
        [MenuItem("GOLFIN/Capture 1v1/Record Resolution Clip (Clip B - Near-Green)")]
        public static void RecordResolutionClip()
        {
            BotVideoRecorder.Arm();
            Launch("versus_resolution_clip");
        }

        [MenuItem("GOLFIN/Capture 1v1/Record Resolution Clip (Clip B - Near-Green)", isValidateFunction: true)]
        static bool ValidateResolutionClip() => !EditorApplication.isPlaying;

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

            // 2. Also load Hole_04_Geo ADDITIVELY so the real course is visible behind the HUD.
            //    Hole_04 is a par-3 (~110m tee→pin) chosen for the versus_full_match_flow
            //    scenario so the full match resolves inside the 30s BotVideoRecorder watchdog
            //    (iron shots ~3-5s vs driver shots ~16s on Hole 18 par-5).
            //    This satisfies the standing rule: capture must be over a real loaded hole,
            //    not the flat-ground LabScaffold default. (Defect-5 fix, iter-4; par-3 switch, iter-4.)
            var geoScene = EditorSceneManager.OpenScene(GeoScenePath, OpenSceneMode.Additive);
            if (!geoScene.IsValid())
            {
                Debug.LogWarning($"[VersusHudCaptureMenu] Hole_04_Geo not loaded — recording will show empty scene background.");
            }

            // BUG A fix (iter-5): query the actual tee world position from the loaded geo scene.
            // Store in SessionState so the versus_full_match_flow EnteredPlayMode handler can set
            // Players[i].Lie to the real tee, not the serialized stale value from a prior hole.
            // VersusMatchController.Start() also waits for IsHoleReady, but this seeding provides
            // an explicit fallback for the initial ResetMatchState call in MatchFlow().
            Vector3 teeLie = QueryTeePosition(geoScene);
            SessionState.SetFloat(TeeLieXKey, teeLie.x);
            SessionState.SetFloat(TeeLieYKey, teeLie.y);
            SessionState.SetFloat(TeeLieZKey, teeLie.z);
            Debug.Log($"[VersusHudCaptureMenu] Tee position queried from {System.IO.Path.GetFileNameWithoutExtension(GeoScenePath)}: {teeLie:F3} (stored in SessionState).");

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

        // ── BUG A fix (iter-5): Tee position query ──────────────────────────────
        /// <summary>
        /// Scans the loaded geo scene for TeeMarker_regular_L and TeeMarker_regular_R GameObjects
        /// and returns their world-space midpoint as the authoritative tee position.
        /// Falls back to TeeMarker_regular_* (any) midpoint, then to Vector3.zero if none found.
        /// Called from Launch() in Editor mode AFTER the geo scene is opened additively.
        /// </summary>
        static Vector3 QueryTeePosition(UnityEngine.SceneManagement.Scene geoScene)
        {
            if (!geoScene.IsValid() || !geoScene.isLoaded)
                return Vector3.zero;

            var roots = geoScene.GetRootGameObjects();
            Vector3 leftPos  = Vector3.zero;
            Vector3 rightPos = Vector3.zero;
            bool    leftFound  = false;
            bool    rightFound = false;

            foreach (var root in roots)
            {
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == "TeeMarker_regular_L") { leftPos  = t.position; leftFound  = true; }
                    if (t.name == "TeeMarker_regular_R") { rightPos = t.position; rightFound = true; }
                    if (leftFound && rightFound) break;
                }
                if (leftFound && rightFound) break;
            }

            if (leftFound && rightFound)
                return (leftPos + rightPos) * 0.5f;

            // Fallback: any TeeMarker_regular_* (take midpoint of all found).
            var teePositions = new System.Collections.Generic.List<Vector3>();
            foreach (var root in roots)
            {
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name.StartsWith("TeeMarker_regular"))
                        teePositions.Add(t.position);
                }
            }
            if (teePositions.Count > 0)
            {
                Vector3 sum = Vector3.zero;
                foreach (var p in teePositions) sum += p;
                return sum / teePositions.Count;
            }

            Debug.LogWarning("[VersusHudCaptureMenu] QueryTeePosition: no TeeMarker_regular_* found in geo scene. Returning Vector3.zero.");
            return Vector3.zero;
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

        // ── Deferred recorder handler (FIX 1 — §15 visual gate) ─────────────────
        // Called by VersusMatchController.OnMatchReadyToBegin after hole load + BallSM ready.
        // One-shot: unsubscribes itself immediately so it can't fire more than once per session.
        static void OnMatchReadyToBeginHandler()
        {
            VersusMatchController.OnMatchReadyToBegin -= OnMatchReadyToBeginHandler;

            // Scoped watchdog bump: a real-tee Hole-04 match takes ~32s (Cesar-approved 2026-06-10).
            // Set override to 40s ONLY for this scenario; the 30s default guard stays intact for every
            // other recording. Override is cleared by DurationWatchdog when it fires, or by End() path.
            // Hard cap: do NOT set above 40s; GPU-safety re-evaluation required for higher values.
            BotVideoRecorder.MaxRecordSecondsOverride = 40;

            Debug.Log("[VersusHudCaptureMenu] OnMatchReadyToBegin received — starting BotVideoRecorder now " +
                      "(hole loaded, BallSM ready). 40s window (Cesar-approved 2026-06-10) covers the full real-tee match flow.");
            BotVideoRecorder.Begin();
        }

        // ── Deferred recorder handler for versus_resolution_clip (Clip B) ────────────
        // Uses the default 30s watchdog — no MaxRecordSecondsOverride needed for near-green start.
        static void OnResolutionClipReadyHandler()
        {
            VersusMatchController.OnMatchReadyToBegin -= OnResolutionClipReadyHandler;

            // DEFAULT 30s watchdog applies. Near-green (~3m from pin) resolves in ~22-27s.
            // MaxRecordSecondsOverride stays at 0 (default) — do NOT set it.
            Debug.Log("[VersusHudCaptureMenu] versus_resolution_clip: OnMatchReadyToBegin received — " +
                      "starting BotVideoRecorder now. Default 30s watchdog (near-green resolves fast).");
            BotVideoRecorder.Begin();
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
                else if (scenario == "versus_full_match_flow")
                {
                    // Phase 2a full-match flow: both P1 and P2 are bot-driven.
                    // VersusMatchController is already in the scene (LabScaffold).
                    // We just pre-seed context and flip _debugBothBots.

                    // BUG A fix (iter-5): read the real tee position stored in SessionState
                    // by Launch() (queried from the geo scene's TeeMarker_regular_* GOs).
                    // This ensures Players[i].Lie is the actual tee, not Vector3.zero.
                    // Note: VersusMatchController.Start() also waits for IsHoleReady before
                    // calling MatchFlow(), so ResetMatchState(tee) will use the correct
                    // BallPosition. The Lie set here is a matching explicit safety net.
                    var teeLie = new Vector3(
                        SessionState.GetFloat(TeeLieXKey, 0f),
                        SessionState.GetFloat(TeeLieYKey, 0f),
                        SessionState.GetFloat(TeeLieZKey, 0f));
                    Debug.Log($"[VersusHudCaptureMenu] versus_full_match_flow: teeLie from SessionState = {teeLie:F3}");

                    Golfin.Gameplay.Session.GameSession.IsVersus = true;
                    Golfin.Gameplay.UI.HUD.MatchContext.Players[0] = new Golfin.Gameplay.UI.HUD.MatchContext.Player
                    {
                        DisplayName = "CAMILA",
                        Level       = 13,
                        TurnCount   = 1,
                        Lie         = teeLie
                    };
                    Golfin.Gameplay.UI.HUD.MatchContext.Players[1] = new Golfin.Gameplay.UI.HUD.MatchContext.Player
                    {
                        DisplayName = "TARO",
                        Level       = 17,
                        TurnCount   = 0,
                        Lie         = teeLie
                    };
                    Golfin.Gameplay.UI.HUD.MatchContext.ActiveIndex = 0;
                    Golfin.Gameplay.UI.HUD.MatchContext.Raise();

                    // Flip the debug flag on VersusMatchController so P1 is also bot-driven.
                    // Also set _debugStartLie so both players start near the Hole_04 pin instead
                    // of at the tee. This ensures the full match resolves inside the 30s
                    // BotVideoRecorder window (short putts ~5-6s vs tee-shot ~11s).
                    // Capture-scenario only — production always starts from BallPosition at tee.
                    var vmc = Object.FindAnyObjectByType<VersusMatchController>();
                    if (vmc != null)
                    {
                        vmc._debugBothBots = true;
                        // DEFECT 3 fix (iter-7): near-pin _debugStartLie override REMOVED.
                        // The bot now starts from the REAL TEE (BallPosition after IsHoleReady).
                        // BUG A fix (iter-5) seeds Players[i].Lie to the tee from VersusMatchController.Start();
                        // VersusMatchController.MatchFlow() reads _controller.BallPosition after IsHoleReady,
                        // so _debugStartLie = Vector3.zero (default) means "use tee, don't override".
                        // BUG B fix (iter-5) ensures the bot picks Iron7 (~110m), not Driver full-power,
                        // so the ~107m tee shot resolves in ~5-8s (not ~16s), fitting within the 30s window.
                        Debug.Log("[VersusHudCaptureMenu] versus_full_match_flow: _debugBothBots = true, " +
                                  "starting from REAL TEE (no _debugStartLie override). BUG B ensures Iron7 for ~107m.");
                    }
                    else
                    {
                        Debug.LogWarning("[VersusHudCaptureMenu] versus_full_match_flow: VersusMatchController not found — match may wait for human input.");
                    }

                    // FIX 1 (§15 visual gate — defer recorder start):
                    // Do NOT call BotVideoRecorder.Begin() here (EnteredPlayMode). Instead,
                    // subscribe to VersusMatchController.OnMatchReadyToBegin — which fires after
                    // IsVersus is confirmed AND BallSM is ready AND the hole has finished loading,
                    // immediately before MatchFlow() begins. This shifts the 40s watchdog
                    // window so it covers the match itself, not the ~5s hole-load overhead.
                    // The subscription is one-shot (unsubscribes after firing so it never leaks).
                    // MaxRecordSeconds constant (30s) is preserved — OnMatchReadyToBeginHandler sets
                    // MaxRecordSecondsOverride=40 for this scenario only (Cesar-approved 2026-06-10).
                    VersusMatchController.OnMatchReadyToBegin += OnMatchReadyToBeginHandler;
                    Debug.Log("[VersusHudCaptureMenu] versus_full_match_flow: BotVideoRecorder deferred " +
                              "— will start when VersusMatchController.OnMatchReadyToBegin fires " +
                              "(hole loaded, BallSM ready, match about to begin). 40s window (Cesar-approved) covers full match flow.");
                    // IMPORTANT: BotVideoRecorder.Begin() is NOT called here — return before the
                    // unconditional Begin() at the bottom of this handler.
                    return;
                }
                else if (scenario == "versus_resolution_clip")
                {
                    // Clip B — near-green resolution proof (iter-9 two-clip strategy, Cesar 2026-06-10).
                    // Seeds _debugStartLie = (-36.12, 17.0, 27.59) [~3m from Hole_04 pin].
                    // ISOLATED to this scenario ONLY — versus_full_match_flow is NOT touched.
                    // BUG-A fix (real tee seed) stays intact in versus_full_match_flow.
                    // Purpose: pixel-confirm HideShotUI Defect-1 fix via CLEAN WIN/LOSE/DRAW banner.
                    // The default 30s watchdog is sufficient — near-green match resolves in ~22-27s.
                    var nearGreenLie = new Vector3(-36.12f, 17.0f, 27.59f);

                    Golfin.Gameplay.Session.GameSession.IsVersus = true;
                    Golfin.Gameplay.UI.HUD.MatchContext.Players[0] = new Golfin.Gameplay.UI.HUD.MatchContext.Player
                    {
                        DisplayName = "CAMILA",
                        Level       = 13,
                        TurnCount   = 1,
                        Lie         = nearGreenLie
                    };
                    Golfin.Gameplay.UI.HUD.MatchContext.Players[1] = new Golfin.Gameplay.UI.HUD.MatchContext.Player
                    {
                        DisplayName = "TARO",
                        Level       = 17,
                        TurnCount   = 0,
                        Lie         = nearGreenLie
                    };
                    Golfin.Gameplay.UI.HUD.MatchContext.ActiveIndex = 0;
                    Golfin.Gameplay.UI.HUD.MatchContext.Raise();

                    var vmc = Object.FindAnyObjectByType<VersusMatchController>();
                    if (vmc != null)
                    {
                        vmc._debugBothBots = true;
                        // Clip B: near-green start to get fast resolution and pixel-confirm the
                        // clean WIN/LOSE/DRAW banner (HideShotUI Defect-1 fix).
                        // _debugStartLie is set here ONLY for this isolated scenario.
                        vmc._debugStartLie = nearGreenLie;
                        Debug.Log("[VersusHudCaptureMenu] versus_resolution_clip: _debugBothBots = true, " +
                                  "_debugStartLie = (-36.12, 17.0, 27.59) [~3m from Hole_04 pin]. " +
                                  "ISOLATED to this scenario — versus_full_match_flow is unaffected. " +
                                  "30s watchdog is sufficient (near-green resolves in ~22-27s).");
                    }
                    else
                    {
                        Debug.LogWarning("[VersusHudCaptureMenu] versus_resolution_clip: VersusMatchController not found.");
                    }

                    // Direct (non-deferred) recorder start: fall through to BotVideoRecorder.Begin()
                    // at the bottom of this handler. Near-green (~3m from pin) resolves in ~8-10s,
                    // well within the default 30s watchdog. The deferred OnMatchReadyToBegin
                    // pattern is NOT used here because VersusMatchController.Start() fires the event
                    // before EnteredPlayMode can register the subscription (timing race → no recording).
                    // The ~5s hole-load overhead before recording starts is acceptable.
                    Debug.Log("[VersusHudCaptureMenu] versus_resolution_clip: starting BotVideoRecorder " +
                              "directly (non-deferred). Default 30s watchdog covers near-green resolution.");
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
                // Clean up deferred-recorder subscriptions if match never reached readiness
                // (e.g. play mode exited early or scenario was aborted before hole load).
                VersusMatchController.OnMatchReadyToBegin -= OnMatchReadyToBeginHandler;
                VersusMatchController.OnMatchReadyToBegin -= OnResolutionClipReadyHandler;

                // Clear the per-scenario watchdog override so it never leaks to a future recording.
                BotVideoRecorder.MaxRecordSecondsOverride = 0;

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
