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
        // Hole_09_Geo (par-4) — versus_bot_hardening_sloped scenario: used to verify H3 green-slope read.
        const string Hole09GeoPath = "Assets/Golf/Courses/lomond-country-club/Generated/Hole_09_Geo.unity";
        // Hole_16_Geo (par-4, water hazard) — versus_bot_hardening_water scenario (DEPRECATED iter-1): Hole16 water polygon
        // is laterally offset from the tee→pin line — the bot's straight aim never crosses water. Kept for reference.
        const string Hole16GeoPath = "Assets/Golf/Courses/lomond-country-club/Generated/Hole_16_Geo.unity";
        // Hole_06_Geo (par-4, water hazard) — versus_bot_hardening_water_h06 scenario (iter-2): water polygon centroid
        // (-19.7,-7.9) has perp distance 6.3m from tee(80.2,-24.5)→pin(-72.5,-8.8) straight line, entering at 79m
        // and exiting at 122m (t=0.51..0.79). DEPRECATED iter-2: probe at dist=74m (pin) is safe (pin is beyond water);
        // the old landing-only probe never fired H2 code even though water crossed the line.
        const string Hole06GeoPath = "Assets/Golf/Courses/lomond-country-club/Generated/Hole_06_Geo.unity";
        // Hole_18_Geo (par-5, water hazard) — versus_bot_hardening_water_h18 scenario (iter-2 final):
        // tee→pin dist=222.8m; water polygon crosses the straight tee→pin line from 100m to 188m (88m of water).
        // With the flight-path probe (every 8m from LayupMinDist to carry), probe at d=106m hits Water → H2 fires.
        // Bot lays up to ~96m (just before water entry at 100m). The probe-at-landing-point approach (Hole06)
        // never fired because the landing point (pin at 74m) was beyond the water band.
        // Geometry verified via Python ray-casting against the Hole_18_Geo water polygon JSON.
        const string Hole18GeoPath = "Assets/Golf/Courses/lomond-country-club/Generated/Hole_18_Geo.unity";
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
        /// Order 345 / H2 visual gate: records a full 1v1 bot match on Hole 16 (par-4, water hazard).
        /// Verifies the bot lays up / retargets around the water instead of repeatedly going OB.
        /// Both P1 and P2 are driven by VersusBot.
        /// Output: tasks/loop_v2_smoke_bot/versus_bot_hardening_water/video/raw.mp4
        /// </summary>
        [MenuItem("GOLFIN/Capture 1v1/Record Bot Hardening - Water Hole")]
        public static void RecordBotHardeningWater()
        {
            BotVideoRecorder.Arm();
            LaunchHole(Hole16GeoPath, "versus_bot_hardening_water");
        }

        [MenuItem("GOLFIN/Capture 1v1/Record Bot Hardening - Water Hole", isValidateFunction: true)]
        static bool ValidateBotHardeningWater() => !EditorApplication.isPlaying;

        /// <summary>
        /// Order 345 / H2 iter-2 visual gate: records a full 1v1 bot match on Hole 06 (par-4, water hazard).
        /// Hole 06 was chosen because its water polygon centroid (-19.7,-7.9) has perpendicular distance
        /// 6.3m from the tee(80.2,-24.5)→pin(-72.5,-8.8) straight line, with the tee→pin segment entering
        /// the water bbox at ~79m and exiting at ~122m (t≈0.51..0.79). The bot's straight aim line crosses
        /// the water, forcing the H2 proactive layup/retarget code to fire.
        /// Uses 120s watchdog — par-4 with water may require multiple retarget shots.
        /// Both P1 and P2 are driven by VersusBot.
        /// Output: tasks/loop_v2_smoke_bot/versus_bot_hardening_water_h06/video/raw.mp4
        /// </summary>
        [MenuItem("GOLFIN/Capture 1v1/Record Bot Hardening - Water Hole 06 (H2 iter-2)")]
        public static void RecordBotHardeningWaterH06()
        {
            BotVideoRecorder.Arm();
            LaunchHole(Hole06GeoPath, "versus_bot_hardening_water_h06");
        }

        [MenuItem("GOLFIN/Capture 1v1/Record Bot Hardening - Water Hole 06 (H2 iter-2)", isValidateFunction: true)]
        static bool ValidateBotHardeningWaterH06() => !EditorApplication.isPlaying;

        /// <summary>
        /// Order 345 / H2 iter-2 FINAL visual gate: records a full 1v1 bot match on Hole 18 (par-5, water hazard).
        /// Hole 18 was chosen: tee→pin dist=222.8m; water polygon crosses the straight tee→pin line from
        /// 100m to 188m (88m continuous water band). With the iter-2 flight-path probe (every 8m from LayupMinDist
        /// to carry), the probe at d=106m hits Water → H2 code fires, bot lays up to ~96m (just short of water).
        /// Previous Hole06 attempt failed: Hole06 pin is at 74m, probe-at-landing always landed at pin (safe,
        /// beyond water); the old code never triggered. The new flight-path loop fixes this.
        /// Uses 120s watchdog — par-5 with water + layup may require multiple shots per player.
        /// Both P1 and P2 are driven by VersusBot. Starts from real tee (no _debugStartLie).
        /// Output: tasks/loop_v2_smoke_bot/versus_bot_hardening_water_h18/video/raw.mp4
        /// </summary>
        [MenuItem("GOLFIN/Capture 1v1/Record Bot Hardening - Water Hole 18 (H2 iter-2 final)")]
        public static void RecordBotHardeningWaterH18()
        {
            BotVideoRecorder.Arm();
            LaunchHole(Hole18GeoPath, "versus_bot_hardening_water_h18");
        }

        [MenuItem("GOLFIN/Capture 1v1/Record Bot Hardening - Water Hole 18 (H2 iter-2 final)", isValidateFunction: true)]
        static bool ValidateBotHardeningWaterH18() => !EditorApplication.isPlaying;

        /// <summary>
        /// Order 345 / H3 visual gate: records a full 1v1 bot match on Hole 9 (par-4).
        /// Verifies the bot's putts curve with the green slope (H3 additive slope read).
        /// Both P1 and P2 are driven by VersusBot.
        /// Output: tasks/loop_v2_smoke_bot/versus_bot_hardening_sloped/video/raw.mp4
        /// </summary>
        [MenuItem("GOLFIN/Capture 1v1/Record Bot Hardening - Sloped Green")]
        public static void RecordBotHardeningSloped()
        {
            BotVideoRecorder.Arm();
            LaunchHole(Hole09GeoPath, "versus_bot_hardening_sloped");
        }

        [MenuItem("GOLFIN/Capture 1v1/Record Bot Hardening - Sloped Green", isValidateFunction: true)]
        static bool ValidateBotHardeningSloped() => !EditorApplication.isPlaying;

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

        /// <summary>
        /// Order 350 audio fidelity clip: drives a near-green 1v1 match to completion on Hole_04
        /// so the result modal appears → VersusResultHandler fires MatchWin/MatchLose stinger SFX.
        /// Both P1 and P2 are bot-driven (P2 also near pin so match ends quickly).
        /// Near-green start: (-36.12, 17.0, 27.59) [~3m from Hole_04 pin] — same as Clip B.
        /// Audio ON. Custom output path for the sound_effects task.
        /// Max 30s recording — near-green match resolves in ~22-27s.
        /// </summary>
        [MenuItem("GOLFIN/Capture 1v1/Record Audio Match Stinger")]
        public static void RecordAudioMatchStinger()
        {
            BotVideoRecorder.CaptureAudio = true;
            BotVideoRecorder.MaxRecordSecondsSessionOverride = 30;
            BotVideoRecorder.CustomOutputPath =
                "Docs/Specs/Active/sound_effects/videos/audio_match_stinger";
            BotVideoRecorder.Arm();
            Launch("audio_match_stinger");
        }

        [MenuItem("GOLFIN/Capture 1v1/Record Audio Match Stinger", isValidateFunction: true)]
        static bool ValidateAudioMatchStinger() => !EditorApplication.isPlaying;

        /// <summary>
        /// Order 346 / 2b visual gate — sloppy bot (DebugLevelOverride=1, bracket minLevel=1).
        /// aimError=±6°, powerError=±0.12, clubNoise=25% — visibly wandering aim and wrong clubs.
        /// Records a full match on Hole_04 (same hole as Clip B).
        /// Output: tasks/loop_v2_smoke_bot/versus_bot_difficulty_lv1/video/raw.mp4
        /// </summary>
        [MenuItem("GOLFIN/Capture 1v1/Record Bot Difficulty - Sloppy (Lv1)")]
        public static void RecordBotDifficultyLv1()
        {
            BotVideoRecorder.Arm();
            Launch("versus_bot_difficulty_lv1");
        }

        [MenuItem("GOLFIN/Capture 1v1/Record Bot Difficulty - Sloppy (Lv1)", isValidateFunction: true)]
        static bool ValidateBotDifficultyLv1() => !EditorApplication.isPlaying;

        /// <summary>
        /// Order 346 / 2b visual gate — hardened bot (DebugLevelOverride=200, bracket minLevel=180).
        /// aimError=±0.4°, powerError=±0.01, clubNoise=0% — plays like the 345 hardened baseline.
        /// Records a full match on Hole_04 (same hole as Lv1 clip for side-by-side comparison).
        /// Output: tasks/loop_v2_smoke_bot/versus_bot_difficulty_lv200/video/raw.mp4
        /// </summary>
        [MenuItem("GOLFIN/Capture 1v1/Record Bot Difficulty - Hardened (Lv200)")]
        public static void RecordBotDifficultyLv200()
        {
            BotVideoRecorder.Arm();
            Launch("versus_bot_difficulty_lv200");
        }

        [MenuItem("GOLFIN/Capture 1v1/Record Bot Difficulty - Hardened (Lv200)", isValidateFunction: true)]
        static bool ValidateBotDifficultyLv200() => !EditorApplication.isPlaying;

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

        // ── Launcher (bot hardening scenarios — LabScaffold + per-hole Geo) ──

        /// <summary>
        /// Generic launcher for bot-hardening scenarios: loads LabScaffold + the specified geoPath.
        /// Follows the same pattern as Launch() but uses a caller-supplied geo scene path instead
        /// of the hardcoded Hole_04_Geo path.
        /// </summary>
        static void LaunchHole(string holeGeoPath, string scenarioKey)
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("[VersusHudCaptureMenu] Stop play mode first before launching a scenario.");
                return;
            }

            Debug.Log($"[VersusHudCaptureMenu] LaunchHole: scenario='{scenarioKey}' geoPath='{holeGeoPath}'");

            var labScene = EditorSceneManager.OpenScene(LabScenePath, OpenSceneMode.Single);
            if (!labScene.IsValid())
            {
                Debug.LogError($"[VersusHudCaptureMenu] Failed to open {LabScenePath}");
                return;
            }

            var geoScene = EditorSceneManager.OpenScene(holeGeoPath, OpenSceneMode.Additive);
            if (!geoScene.IsValid())
            {
                Debug.LogWarning($"[VersusHudCaptureMenu] {holeGeoPath} not loaded — recording will show empty scene background.");
            }

            Vector3 teeLie = QueryTeePosition(geoScene);
            SessionState.SetFloat(TeeLieXKey, teeLie.x);
            SessionState.SetFloat(TeeLieYKey, teeLie.y);
            SessionState.SetFloat(TeeLieZKey, teeLie.z);
            Debug.Log($"[VersusHudCaptureMenu] Tee position queried from {System.IO.Path.GetFileNameWithoutExtension(holeGeoPath)}: {teeLie:F3}");

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

            // Scoped watchdog bump: a real-tee Hole-04 par-3 match. Bumped to 60s (iter-2) so
            // a complete hole-out (HoleCompleted log line) is guaranteed visible in the recording.
            // Previously 40s — self-review (iter-1) flagged no visible hole-out in 39.93s clip.
            BotVideoRecorder.MaxRecordSecondsOverride = 60;

            Debug.Log("[VersusHudCaptureMenu] OnMatchReadyToBegin received — starting BotVideoRecorder now " +
                      "(hole loaded, BallSM ready). 60s window (iter-2, extended for hole-out visibility).");
            BotVideoRecorder.Begin();
        }

        // ── Deferred recorder handler for versus_bot_hardening_water (Order 345 / H2, DEPRECATED iter-1 Hole16) ──
        // Uses a 60s watchdog — par-4 water hole can take more shots than the par-3 Hole_04.
        static void OnBotHardeningWaterReadyHandler()
        {
            VersusMatchController.OnMatchReadyToBegin -= OnBotHardeningWaterReadyHandler;
            BotVideoRecorder.MaxRecordSecondsOverride = 60;
            Debug.Log("[VersusHudCaptureMenu] versus_bot_hardening_water: OnMatchReadyToBegin received — " +
                      "starting BotVideoRecorder now. 60s watchdog covers par-4 water hole.");
            BotVideoRecorder.Begin();
        }

        // ── Deferred recorder handler for versus_bot_hardening_water_h06 (Order 345 / H2 iter-2, Hole 06) ──
        // Uses a 120s watchdog — Hole 06 par-4 with active water crossing; bot may require extra shots to lay up.
        // Hole 06 tee(80.2,-24.5)→pin(-72.5,-8.8): water polygon crosses the straight aim line at 79m–122m.
        static void OnBotHardeningWaterH06ReadyHandler()
        {
            VersusMatchController.OnMatchReadyToBegin -= OnBotHardeningWaterH06ReadyHandler;
            BotVideoRecorder.MaxRecordSecondsOverride = 120;
            Debug.Log("[VersusHudCaptureMenu] versus_bot_hardening_water_h06: OnMatchReadyToBegin received — " +
                      "starting BotVideoRecorder now. 120s watchdog covers Hole06 par-4 with H2 water layup.");
            BotVideoRecorder.Begin();
        }

        // ── Deferred recorder handler for versus_bot_hardening_water_h18 (Order 345 / H2 iter-2 final, Hole 18) ──
        // Uses a 180s watchdog — Hole 18 par-5 with 88m water band; 2-player bot match needs extra time to complete.
        // Hole 18 tee→pin 222.8m; flight-path probe at d=106m hits Water; bot lays up to ~96m.
        // iter-3: bumped 120s → 180s to guarantee hole-out proof (par-5 + 2 bots + H2 layup penalty shots = ~5-6 shots
        // per player, each shot ~5-6s, total ~60-80s match time + overhead).
        static void OnBotHardeningWaterH18ReadyHandler()
        {
            VersusMatchController.OnMatchReadyToBegin -= OnBotHardeningWaterH18ReadyHandler;
            BotVideoRecorder.MaxRecordSecondsOverride = 180;
            Debug.Log("[VersusHudCaptureMenu] versus_bot_hardening_water_h18: OnMatchReadyToBegin received — " +
                      "starting BotVideoRecorder now. 180s watchdog (iter-3, bumped for guaranteed hole-out on par-5).");
            BotVideoRecorder.Begin();
        }

        // ── Deferred recorder handler for versus_bot_hardening_sloped (Order 345 / H3) ──
        // iter-2: uses a 60s watchdog + near-green _debugStartLie so the bot selects Putter (targetDist≤20m).
        // H09 nearGreen position (12m from pin along tee→pin axis): (170.48, 17.472, 38.63).
        // This ensures H3 slope-read code fires (gated on isPutt) — iter-1 failed because 60s from tee
        // never put the bot in putter range.
        static void OnBotHardeningSlopedReadyHandler()
        {
            VersusMatchController.OnMatchReadyToBegin -= OnBotHardeningSlopedReadyHandler;
            BotVideoRecorder.MaxRecordSecondsOverride = 60;
            Debug.Log("[VersusHudCaptureMenu] versus_bot_hardening_sloped: OnMatchReadyToBegin received — " +
                      "starting BotVideoRecorder now. 60s watchdog, near-green start (12m from H09 pin) ensures Putter selection.");
            BotVideoRecorder.Begin();
        }

        // ── Deferred recorder handler for versus_bot_difficulty_* (Order 346 / 2b) ───────
        // 60s watchdog — Hole_04 par-3 with error-injected bot; sloppy bracket may take
        // extra shots (OB shots, recoveries), but the 60s cap proved sufficient for 345 par-3.
        static void OnBotDifficultyReadyHandler()
        {
            VersusMatchController.OnMatchReadyToBegin -= OnBotDifficultyReadyHandler;
            BotVideoRecorder.MaxRecordSecondsOverride = 60;
            Debug.Log("[VersusHudCaptureMenu] versus_bot_difficulty: OnMatchReadyToBegin — " +
                      "starting BotVideoRecorder. 60s watchdog (Hole_04 par-3).");
            BotVideoRecorder.Begin();
        }

        // ── Deferred recorder handler for audio_match_stinger (Order 350 audio fidelity) ──
        // Near-green start resolves in ~22-27s. Uses default 30s watchdog (no override needed).
        // CaptureAudio=true set by RecordAudioMatchStinger() before Arm() — already in SessionState.
        static void OnAudioMatchStingerReadyHandler()
        {
            VersusMatchController.OnMatchReadyToBegin -= OnAudioMatchStingerReadyHandler;
            Debug.Log("[VersusHudCaptureMenu] audio_match_stinger: OnMatchReadyToBegin received — " +
                      "starting BotVideoRecorder now. Default 30s watchdog (near-green ~22-27s).");
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
                    // immediately before MatchFlow() begins. This shifts the watchdog window so it
                    // covers the match itself, not the ~5s hole-load overhead.
                    // iter-2: watchdog bumped 40s → 60s (OnMatchReadyToBeginHandler) so hole-out
                    // (HoleCompleted log line) is guaranteed visible in the recording.
                    VersusMatchController.OnMatchReadyToBegin += OnMatchReadyToBeginHandler;
                    Debug.Log("[VersusHudCaptureMenu] versus_full_match_flow: BotVideoRecorder deferred " +
                              "— will start when VersusMatchController.OnMatchReadyToBegin fires " +
                              "(hole loaded, BallSM ready, match about to begin). 60s window (iter-2, covers hole-out).");
                    // IMPORTANT: BotVideoRecorder.Begin() is NOT called here — return before the
                    // unconditional Begin() at the bottom of this handler.
                    return;
                }
                else if (scenario == "versus_bot_hardening_water")
                {
                    // Order 345 / H2: full match on Hole 16 (par-4, water hazard).
                    // Both players bot-driven; starts from real tee (BUG-A fix pattern).
                    // BotVideoRecorder deferred to OnMatchReadyToBegin (60s watchdog).
                    var teeLie = new Vector3(
                        SessionState.GetFloat(TeeLieXKey, 0f),
                        SessionState.GetFloat(TeeLieYKey, 0f),
                        SessionState.GetFloat(TeeLieZKey, 0f));
                    Debug.Log($"[VersusHudCaptureMenu] versus_bot_hardening_water: teeLie={teeLie:F3}");

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

                    var vmc2 = Object.FindAnyObjectByType<VersusMatchController>();
                    if (vmc2 != null)
                    {
                        vmc2._debugBothBots = true;
                        Debug.Log("[VersusHudCaptureMenu] versus_bot_hardening_water: _debugBothBots = true, starting from real tee.");
                    }
                    else
                    {
                        Debug.LogWarning("[VersusHudCaptureMenu] versus_bot_hardening_water: VersusMatchController not found.");
                    }

                    VersusMatchController.OnMatchReadyToBegin += OnBotHardeningWaterReadyHandler;
                    Debug.Log("[VersusHudCaptureMenu] versus_bot_hardening_water: BotVideoRecorder deferred to OnMatchReadyToBegin (60s).");
                    return;
                }
                else if (scenario == "versus_bot_hardening_water_h06")
                {
                    // Order 345 / H2 iter-2: full match on Hole 06 (par-4, water crossing the tee→pin line).
                    // Hole 06 was chosen: water centroid (-19.7,-7.9) has perp-dist 6.3m from tee→pin,
                    // bbox intersects segment at t≈0.51–0.79 (79m–122m from tee). Water is in the way.
                    // Both players bot-driven; starts from real tee (BUG-A fix pattern).
                    // BotVideoRecorder deferred to OnMatchReadyToBegin (120s watchdog).
                    var teeLie = new Vector3(
                        SessionState.GetFloat(TeeLieXKey, 0f),
                        SessionState.GetFloat(TeeLieYKey, 0f),
                        SessionState.GetFloat(TeeLieZKey, 0f));
                    Debug.Log($"[VersusHudCaptureMenu] versus_bot_hardening_water_h06: teeLie={teeLie:F3}");

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

                    var vmcH06 = Object.FindAnyObjectByType<VersusMatchController>();
                    if (vmcH06 != null)
                    {
                        vmcH06._debugBothBots = true;
                        // No _debugStartLie — start from real tee so bot must fly over/around water.
                        Debug.Log("[VersusHudCaptureMenu] versus_bot_hardening_water_h06: _debugBothBots = true, " +
                                  "starting from real tee. Water on tee→pin line forces H2 layup to fire.");
                    }
                    else
                    {
                        Debug.LogWarning("[VersusHudCaptureMenu] versus_bot_hardening_water_h06: VersusMatchController not found.");
                    }

                    VersusMatchController.OnMatchReadyToBegin += OnBotHardeningWaterH06ReadyHandler;
                    Debug.Log("[VersusHudCaptureMenu] versus_bot_hardening_water_h06: BotVideoRecorder deferred to OnMatchReadyToBegin (120s).");
                    return;
                }
                else if (scenario == "versus_bot_hardening_water_h18")
                {
                    // Order 345 / H2 iter-2 final: full match on Hole 18 (par-5, 88m water band on tee→pin line).
                    // Flight-path probe (every 8m from 10m to carry) detects Water at d=106m.
                    // Bot lays up to ~96m (TrySafeLanding walks back from 106m in 8m steps).
                    // Both players bot-driven; starts from real tee — no _debugStartLie needed.
                    // BotVideoRecorder deferred to OnMatchReadyToBegin (120s watchdog).
                    var teeLie = new Vector3(
                        SessionState.GetFloat(TeeLieXKey, 0f),
                        SessionState.GetFloat(TeeLieYKey, 0f),
                        SessionState.GetFloat(TeeLieZKey, 0f));
                    Debug.Log($"[VersusHudCaptureMenu] versus_bot_hardening_water_h18: teeLie={teeLie:F3}");

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

                    var vmcH18 = Object.FindAnyObjectByType<VersusMatchController>();
                    if (vmcH18 != null)
                    {
                        vmcH18._debugBothBots = true;
                        // No _debugStartLie — start from real tee so bot must fly over/around the 88m water band.
                        // Flight-path probe (every 8m) detects Water at d=106m → bot lays up to ~96m.
                        Debug.Log("[VersusHudCaptureMenu] versus_bot_hardening_water_h18: _debugBothBots = true, " +
                                  "starting from real tee. 88m water band on tee→pin forces H2 flight-path probe to fire.");
                    }
                    else
                    {
                        Debug.LogWarning("[VersusHudCaptureMenu] versus_bot_hardening_water_h18: VersusMatchController not found.");
                    }

                    VersusMatchController.OnMatchReadyToBegin += OnBotHardeningWaterH18ReadyHandler;
                    Debug.Log("[VersusHudCaptureMenu] versus_bot_hardening_water_h18: BotVideoRecorder deferred to OnMatchReadyToBegin (120s).");
                    return;
                }
                else if (scenario == "versus_bot_hardening_sloped")
                {
                    // Order 345 / H3: full match on Hole 9 (par-4, sloped green).
                    // Both players bot-driven; starts from real tee (BUG-A fix pattern).
                    // BotVideoRecorder deferred to OnMatchReadyToBegin (60s watchdog).
                    var teeLie = new Vector3(
                        SessionState.GetFloat(TeeLieXKey, 0f),
                        SessionState.GetFloat(TeeLieYKey, 0f),
                        SessionState.GetFloat(TeeLieZKey, 0f));
                    Debug.Log($"[VersusHudCaptureMenu] versus_bot_hardening_sloped: teeLie={teeLie:F3}");

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

                    var vmc3 = Object.FindAnyObjectByType<VersusMatchController>();
                    if (vmc3 != null)
                    {
                        vmc3._debugBothBots = true;
                        // iter-2 FIX: Use near-green _debugStartLie so the bot selects Putter (targetDist≤20m).
                        // H09 pin=(182.30, 18.42, 40.70); nearGreen 12m from pin along tee→pin axis:
                        //   nearGreen=(170.48, Y=17.472, 38.63) — terrain-raycast confirmed in EditMode.
                        // This ensures H3 slope-read code fires (gated on isPutt = targetDist≤20m).
                        // iter-1 failure: from tee (361m away), 60s watchdog never put the bot in putter range.
                        var nearGreenH09 = new Vector3(170.48f, 17.472f, 38.63f);
                        vmc3._debugStartLie = nearGreenH09;
                        Debug.Log("[VersusHudCaptureMenu] versus_bot_hardening_sloped: _debugBothBots = true, " +
                                  "_debugStartLie = (170.48, 17.472, 38.63) [12m from H09 pin]. " +
                                  "Bot will select Putter, H3 slope-read fires. 60s watchdog.");
                    }
                    else
                    {
                        Debug.LogWarning("[VersusHudCaptureMenu] versus_bot_hardening_sloped: VersusMatchController not found.");
                    }

                    VersusMatchController.OnMatchReadyToBegin += OnBotHardeningSlopedReadyHandler;
                    Debug.Log("[VersusHudCaptureMenu] versus_bot_hardening_sloped: BotVideoRecorder deferred to OnMatchReadyToBegin (60s). Near-green start ensures H3 slope-read fires.");
                    return;
                }
                else if (scenario == "versus_bot_difficulty_lv1" || scenario == "versus_bot_difficulty_lv200")
                {
                    // Order 346 / 2b visual gate: full match on Hole_04 with DebugLevelOverride.
                    // lv1: DebugLevelOverride=1 → bracket minLevel=1 (aim±6°, pow±0.12, clubNoise=25%)
                    // lv200: DebugLevelOverride=200 → bracket minLevel=180 (aim±0.4°, pow±0.01, no noise)
                    int debugLevel = scenario == "versus_bot_difficulty_lv1" ? 1 : 200;

                    var teeLie = new Vector3(
                        SessionState.GetFloat(TeeLieXKey, 0f),
                        SessionState.GetFloat(TeeLieYKey, 0f),
                        SessionState.GetFloat(TeeLieZKey, 0f));
                    Debug.Log($"[VersusHudCaptureMenu] {scenario}: teeLie={teeLie:F3} DebugLevelOverride={debugLevel}");

                    Golfin.Gameplay.Session.GameSession.IsVersus = true;
                    // Players[1] Level is set here but will be overridden by DebugLevelOverride on VersusBot.
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
                        Level       = debugLevel,
                        TurnCount   = 0,
                        Lie         = teeLie
                    };
                    Golfin.Gameplay.UI.HUD.MatchContext.ActiveIndex = 0;
                    Golfin.Gameplay.UI.HUD.MatchContext.Raise();

                    var vmcDiff = Object.FindAnyObjectByType<VersusMatchController>();
                    if (vmcDiff != null)
                    {
                        vmcDiff._debugBothBots = true;
                        // Inject DebugLevelOverride on VersusBot to force the bracket.
                        var bot = Object.FindAnyObjectByType<VersusBot>();
                        if (bot != null)
                        {
                            bot.DebugLevelOverride = debugLevel;
                            Debug.Log($"[VersusHudCaptureMenu] {scenario}: VersusBot.DebugLevelOverride={debugLevel} set directly.");
                        }
                        else
                        {
                            Debug.LogWarning($"[VersusHudCaptureMenu] {scenario}: VersusBot not found at EnteredPlayMode — " +
                                             "DebugLevelOverride will fall back to MatchContext.Players[1].Level={debugLevel}.");
                        }
                        Debug.Log($"[VersusHudCaptureMenu] {scenario}: _debugBothBots=true, DebugLevelOverride={debugLevel}, real tee start.");
                    }
                    else
                    {
                        Debug.LogWarning($"[VersusHudCaptureMenu] {scenario}: VersusMatchController not found.");
                    }

                    // Add a named handler so ExitingPlayMode cleanup can remove it.
                    VersusMatchController.OnMatchReadyToBegin += OnBotDifficultyReadyHandler;
                    Debug.Log($"[VersusHudCaptureMenu] {scenario}: BotVideoRecorder deferred to OnMatchReadyToBegin (60s watchdog).");
                    return;
                }
                else if (scenario == "audio_match_stinger")
                {
                    // Order 350 audio fidelity: drive a near-green 1v1 match on Hole_04 to completion
                    // so the result modal fires MatchWin/MatchLose/MatchDraw stinger SFX.
                    // Near-green start: (-36.12, 17.0, 27.59) [~3m from Hole_04 pin].
                    // CaptureAudio=true already set in SessionState by RecordAudioMatchStinger().
                    // BotVideoRecorder deferred to OnMatchReadyToBegin to avoid Y-flip / hole-load overhead.
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

                    var vmcStinger = Object.FindAnyObjectByType<VersusMatchController>();
                    if (vmcStinger != null)
                    {
                        vmcStinger._debugBothBots = true;
                        vmcStinger._debugStartLie = nearGreenLie;
                        Debug.Log("[VersusHudCaptureMenu] audio_match_stinger: _debugBothBots=true, " +
                                  "_debugStartLie=(-36.12, 17.0, 27.59) [~3m from Hole_04 pin]. " +
                                  "Match ends quickly so result stinger SFX fires within 30s window.");
                    }
                    else
                    {
                        Debug.LogWarning("[VersusHudCaptureMenu] audio_match_stinger: VersusMatchController not found.");
                    }

                    VersusMatchController.OnMatchReadyToBegin += OnAudioMatchStingerReadyHandler;
                    Debug.Log("[VersusHudCaptureMenu] audio_match_stinger: BotVideoRecorder deferred to OnMatchReadyToBegin.");
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
                VersusMatchController.OnMatchReadyToBegin -= OnBotHardeningWaterReadyHandler;
                VersusMatchController.OnMatchReadyToBegin -= OnBotHardeningWaterH06ReadyHandler;
                VersusMatchController.OnMatchReadyToBegin -= OnBotHardeningWaterH18ReadyHandler;
                VersusMatchController.OnMatchReadyToBegin -= OnBotHardeningSlopedReadyHandler;
                VersusMatchController.OnMatchReadyToBegin -= OnBotDifficultyReadyHandler;
                VersusMatchController.OnMatchReadyToBegin -= OnAudioMatchStingerReadyHandler;

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
