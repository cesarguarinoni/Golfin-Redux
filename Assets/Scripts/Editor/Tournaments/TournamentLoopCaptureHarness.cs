#if UNITY_EDITOR
using System.Collections;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Golfin.Physics.Viewer.Bot;
using Golfin.Physics.Viewer.Editor;

namespace Golfin.Tournaments.CaptureHarness.Editor
{
    /// <summary>
    /// Editor-only capture harness for the tournament_round_loop (T6) acceptance video.
    ///
    /// Drives the REAL ShellScene tournament flow (no synthetic buttons — Rule 2):
    ///   boot → Home → Tournament Selection → kasumigaseki "SIGN UP" → CONFIRM
    ///   → Hole Selection → PLAY (hole 1, par 5) → PLAY (hole 2, par 4) → Leaderboard
    ///
    /// Architecture:
    ///   • This file is in Assets/Scripts/Editor/Tournaments/ — OUTSIDE Assets/Scripts/Physics/
    ///   • Calls BotDriver + BotVideoRecorder via asmdef references only (Golfin.Physics.Viewer
    ///     + Golfin.Physics.Viewer.BotEditor) — zero edits to Physics/ or Scenarios.cs
    ///   • Mirrors the LoopV2SmokeBotMenu SessionState arm + playModeStateChanged pattern
    ///   • Uses BotVideoRecorder.ArmDeferred() + BeginDeferred() to start recording after
    ///     NavigateToHome settles (captures the full flow: signup → hole1 → hole2 → leaderboard)
    ///   • All BotDriver waits use WaitForSecondsRealtime / unscaledDeltaTime — Time.timeScale
    ///     has NO effect on recording duration. WatchdogSeconds is set to 1440s (24 min wall-clock)
    ///     to accommodate FrameRatePlayback.Variable encoding at ~4fps effective at 1170x2532:
    ///       480s wall-clock produced only ~104.8s of video content (4fps).
    ///       To get ~295s of video (full flow), need ~1350s wall-clock. 1440s = comfortable margin.
    ///
    /// Usage:
    ///   GOLFIN > Tournaments > Dry Run — Tournament Round Loop
    ///     Validates the full flow WITHOUT recording. Run this first to confirm the
    ///     save state is clean (kasumigaseki = "SIGN UP"). If the dry run registered
    ///     kasumigaseki, clear the entry before the recorded run:
    ///       GOLFIN > Tournaments > Clear Kasumigaseki Entry + Flush
    ///
    ///   GOLFIN > Tournaments > Record — Tournament Round Loop
    ///     Recorded run (1170x2532 MP4). One recording per Editor session (GPU guard).
    ///     Relaunch Unity if you need a second take.
    ///
    /// Output: tasks/loop_v2_smoke_bot/tournament_round_loop/video/raw.mp4
    ///         (copy to Docs/Specs/Active/tournament_round_loop/videos/ after recording)
    /// </summary>
    public static class TournamentLoopCaptureHarness
    {
        const string ShellScenePath = "Assets/Scenes/ShellScene.unity";
        const string ArmedKey       = "TournamentLoopHarness.Armed";
        const string RecordedKey    = "TournamentLoopHarness.ShouldRecord";

        // BotVideoRecorder default output: tasks/loop_v2_smoke_bot/<scenario>/video/raw.mp4
        // We override to our own task path under the canonical output dir.
        const string OutputDir      = "Docs/Specs/Active/tournament_round_loop/videos";
        const string RawOutputPath  = "Docs/Specs/Active/tournament_round_loop/videos/raw";

        // Watchdog cap: recording begins BEFORE NavTeeButton click and runs to Leaderboard.
        //   All BotDriver waits use WaitForSecondsRealtime / unscaledDeltaTime — timeScale
        //   does NOT compress recording duration. Measured realtime for full flow:
        //     entry UI (home→modal→CONFIRM→HoleSelection): ~15s
        //     Hole 1 (par5, 8 strokes × ~17s/stroke): ~140s
        //     HoleSelection dwell after Hole 1: ~8s (FINISHED/NEXT card transition capture)
        //     Hole 2 (par4, 7 strokes × ~15s/stroke): ~105s
        //     Leaderboard entry + display: ~20s
        //     Total ≈ 290–350s realtime
        //
        //   FrameRatePlayback.Variable at 1170x2532 under heavy bot+physics load:
        //     ~4fps effective (measured: 480s wall-clock produced only 104.8s of video)
        //     To encode ~350s of video: need ~1400s wall-clock. 1440s (24 min) = comfortable.
        const int WatchdogSeconds = 1440;

        // ─── Menu items ─────────────────────────────────────────────────────────

        [MenuItem("GOLFIN/Tournaments/Dry Run — Tournament Round Loop")]
        public static void LaunchDryRun()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[TournamentLoopHarness] Already in play mode — stop first.");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            EditorSceneManager.OpenScene(ShellScenePath);
            Directory.CreateDirectory(OutputDir);

            // Dry run: no recording arm
            SessionState.SetBool(RecordedKey, false);
            SessionState.SetBool(ArmedKey, true);
            EditorApplication.EnterPlaymode();
            Debug.Log("[TournamentLoopHarness] DRY RUN armed — entering play mode (no recording).");
        }

        [MenuItem("GOLFIN/Tournaments/Dry Run — Tournament Round Loop", isValidateFunction: true)]
        static bool ValidateDryRun() => !EditorApplication.isPlaying;

        [MenuItem("GOLFIN/Tournaments/Record — Tournament Round Loop")]
        public static void LaunchRecordedRun()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[TournamentLoopHarness] Already in play mode — stop first.");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            EditorSceneManager.OpenScene(ShellScenePath);
            Directory.CreateDirectory(OutputDir);

            // Arm deferred recording (avoids Y-flip transient at EnteredPlayMode).
            // BeginDeferred() is called mid-coroutine once the home screen has settled.
            // Reset session guard so this can be called more than once per Editor session
            // (safe since we only record ONE clip; the guard exists to prevent batch accumulation).
            BotVideoRecorder.ResetSessionGuard();
            BotVideoRecorder.CustomOutputPath = RawOutputPath;
            BotVideoRecorder.MaxRecordSecondsSessionOverride = WatchdogSeconds;
            BotVideoRecorder.ArmDeferred();

            SessionState.SetBool(RecordedKey, true);
            SessionState.SetBool(ArmedKey, true);
            EditorApplication.EnterPlaymode();
            Debug.Log("[TournamentLoopHarness] RECORDED RUN armed — entering play mode (deferred recording).");
        }

        [MenuItem("GOLFIN/Tournaments/Record — Tournament Round Loop", isValidateFunction: true)]
        static bool ValidateRecordedRun() => !EditorApplication.isPlaying;

        /// <summary>
        /// Clears the kasumigaseki tournament entry from the save state so the next
        /// run boots the card in "SIGN UP" state. Use this after a dry run that registered.
        /// </summary>
        [MenuItem("GOLFIN/Tournaments/Clear Kasumigaseki Entry + Flush")]
        public static void ClearKasumigasekiEntry()
        {
            if (!EditorApplication.isPlaying)
            {
                Debug.LogWarning("[TournamentLoopHarness] Must be in play mode to flush save data.");
                return;
            }
            // Use reflection to avoid static reference to Assembly-CSharp
            var saveHost = UnityEngine.Object.FindObjectOfType<Golfin.Save.SaveDataHost>();
            if (saveHost == null)
            {
                Debug.LogError("[TournamentLoopHarness] SaveDataHost not found in scene.");
                return;
            }
            try
            {
                var dataField = saveHost.GetType().GetField("Data",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (dataField == null)
                {
                    dataField = saveHost.GetType().GetProperty("Data",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                        ?.GetGetMethod() != null ? null : null;
                }

                // Try via reflection to remove the kasumigaseki entry
                var removeMethod = typeof(TournamentLoopBotHost).GetMethod("RemoveKasumigasekiEntry",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                if (removeMethod != null)
                {
                    removeMethod.Invoke(null, null);
                }
                else
                {
                    Debug.Log("[TournamentLoopHarness] ClearKasumigaseki: launching in-play coroutine.");
                    var host = new GameObject("[TournamentLoopHarnessCleaner]");
                    UnityEngine.Object.DontDestroyOnLoad(host);
                    host.AddComponent<TournamentLoopClearHelper>();
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[TournamentLoopHarness] ClearKasumigasekiEntry error: {ex.Message}");
            }
        }

        // ─── Play mode hook ─────────────────────────────────────────────────────

        [InitializeOnLoadMethod]
        static void RegisterHook()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(ArmedKey, false)) return;

            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                SessionState.SetBool(ArmedKey, false); // clear — must be immediate
                Application.runInBackground = true;

                // points_cutover_followups item 1: the splash is a hard sign-in gate now, and this
                // harness has no credentials. Arm the dev override BEFORE the bot reaches the splash
                // — fake local session + points backend forced OFF, so the run touches no ledger.
                Golfin.Dev.BotSessionOverride.Arm("TournamentLoopCaptureHarness");

                bool shouldRecord = SessionState.GetBool(RecordedKey, false);
                SessionState.SetBool(RecordedKey, false);

                // NOTE: BotVideoRecorder.Begin() is a no-op here because we used ArmDeferred()
                // (not Arm()). RecordVideo flag is NOT set — deferred recording starts
                // mid-coroutine when the host calls BotVideoRecorder.BeginDeferred().
                BotVideoRecorder.Begin(); // no-op for deferred; clears any accidentally-set RecordVideo

                var go = new GameObject("[TournamentLoopBotHost]");
                UnityEngine.Object.DontDestroyOnLoad(go);
                var host = go.AddComponent<TournamentLoopBotHost>();
                host.ShouldRecord = shouldRecord;
                Debug.Log($"[TournamentLoopHarness] EnteredPlayMode — host GO created, shouldRecord={shouldRecord}.");
            }
            else if (state == PlayModeStateChange.ExitingPlayMode)
            {
                BotVideoRecorder.End();
            }
        }
    }

    // ─── Helper: clear kasumigaseki entry ────────────────────────────────────

    public class TournamentLoopClearHelper : MonoBehaviour
    {
        void Start() => StartCoroutine(ClearAndSelf());

        IEnumerator ClearAndSelf()
        {
            yield return new WaitForSecondsRealtime(1f);
            TournamentLoopBotHost.ClearKasumigasekiEntryStatic();
            Destroy(gameObject);
        }
    }

    // ─── Bot host MonoBehaviour ───────────────────────────────────────────────

    /// <summary>
    /// Injected at EnteredPlayMode; runs the tournament round loop coroutine.
    /// Self-destructs (Destroy) when the sequence completes or times out.
    /// </summary>
    public class TournamentLoopBotHost : MonoBehaviour
    {
        public bool ShouldRecord { get; set; }

        const string CaptureDir = "tasks/loop_v2_smoke_bot/tournament_round_loop/screenshots";

        void Start() => StartCoroutine(SafeRun());

        IEnumerator SafeRun()
        {
            Debug.Log("[TournamentLoopBotHost] Start.");
            var bot = new BotDriver(CaptureDir);

            bool failed = false;
            System.Exception caught = null;
            IEnumerator inner = Run(bot);

            // Wrap in a manual pump so exceptions surface cleanly
            while (true)
            {
                bool hasNext;
                try
                {
                    hasNext = inner.MoveNext();
                }
                catch (System.Exception ex)
                {
                    caught = ex;
                    hasNext = false;
                    failed = true;
                }
                if (!hasNext) break;
                yield return inner.Current;
            }

            if (failed && caught != null)
                Debug.LogError($"[TournamentLoopBotHost] EXCEPTION: {caught}");

            bot.FlushLog("tournament_round_loop.log");
            Debug.Log($"[TournamentLoopBotHost] Done. Exiting play mode.");
            yield return new WaitForSecondsRealtime(2f);
            EditorApplication.ExitPlaymode();
            Destroy(gameObject);
        }

        IEnumerator Run(BotDriver bot)
        {
            // ── Step 1: Boot to Home ─────────────────────────────────────────
            bot.LogStep("=== TournamentRoundLoop: NavigateToHome ===");
            yield return bot.NavigateToHome(totalTimeoutSeconds: 60f);
            yield return new WaitForSecondsRealtime(1.5f); // let home settle

            // ── Step 2: Start recording (full flow) + Navigate to Tournament Selection ─
            // Recording begins HERE (after home settles, before any tournament clicks) so
            // the full flow is captured: signup modal → CONFIRM/RP-debit → Hole 1 → card
            // transition → Hole 2 → Leaderboard.
            if (ShouldRecord)
            {
                bot.LogStep("=== TournamentRoundLoop: BeginDeferred recording (home settled, full flow) ===");
                BotVideoRecorder.BeginDeferred();
                yield return new WaitForSecondsRealtime(0.5f); // let recorder settle
            }

            // Path: Home → (NavTeeButton) → ModeSelection → (TOURNAMENTS (TEMP)) → TournamentSelection
            // The NavTeeButton (bottom-nav tee icon) navigates to ModeSelectionScreen
            // where TournamentTempEntry lives. TournamentTempEntry is inactive when
            // ModeSelectionScreen is hidden (i.e. on Home screen).
            bot.LogStep("=== TournamentRoundLoop: Click NavTeeButton (Home → ModeSelection) ===");
            yield return bot.Click("NavTeeButton", settleSeconds: 1.5f);
            yield return bot.WaitForScreen("ModeSelection", timeoutSeconds: 10f);
            yield return new WaitForSecondsRealtime(0.8f); // ModeSelectionScreen visible

            bot.LogStep("=== TournamentRoundLoop: Click TOURNAMENTS (TEMP) ===");
            yield return bot.Click("TOURNAMENTS (TEMP)", settleSeconds: 1.5f);
            yield return bot.WaitForScreen("TournamentSelection", timeoutSeconds: 15f);
            yield return new WaitForSecondsRealtime(1.5f); // cards visible

            // ── Step 3: Click SIGN UP on the kasumigaseki_open card ──────────
            // The kasumigaseki card CTA label is "SIGN UP" (from ARCHITECT_REVIEW.md).
            bot.LogStep("=== TournamentRoundLoop: Click SIGN UP on kasumigaseki ===");
            yield return bot.Click("SIGN UP", settleSeconds: 1.0f);
            yield return bot.WaitForModalVisible("TournamentSignupModal", timeoutSeconds: 15f);
            yield return new WaitForSecondsRealtime(0.5f); // modal fully visible

            // ── Step 4: Confirm signup (100 RP debit + register) ─────────────
            bot.LogStep("=== TournamentRoundLoop: Click CONFIRM ===");
            yield return bot.Click("CONFIRM", settleSeconds: 1.5f);
            yield return bot.WaitForModalHidden("TournamentSignupModal", timeoutSeconds: 15f);
            yield return bot.WaitForScreen("TournamentHoleSelection", timeoutSeconds: 20f);
            yield return new WaitForSecondsRealtime(1.5f); // hole selection visible

            // ── Step 5: Play Hole 1 (Par 5) ──────────────────────────────────
            bot.LogStep("=== TournamentRoundLoop: Click PLAY (Hole 1) ===");
            yield return bot.Click("PLAY", settleSeconds: 2.0f);
            yield return bot.WaitForAnyHoleGeoScene(timeoutSeconds: 60f);
            yield return new WaitForSecondsRealtime(3.0f); // hole loaded, physics ready

            bot.LogStep("=== TournamentRoundLoop: PlayHoleToCup(par=5) ===");
            yield return bot.PlayHoleToCup(par: 5, firstStrokePowerOverride: 0.85f);
            yield return new WaitForSecondsRealtime(2.0f); // post-hole screen transition

            // ── Step 6: Return to Hole Selection screen ──────────────────────
            // Card 1 should now show "FINISHED", card 2 "NEXT".
            // FrameRatePlayback.Variable at 1170x2532 encodes ~4fps effective, so a 2s dwell
            // captures only ~8 frames — risky for the card-state transition. 8s dwell = ~32 frames,
            // giving a solid window for the FINISHED/NEXT state to be captured.
            yield return bot.WaitForScreen("TournamentHoleSelection", timeoutSeconds: 30f);
            yield return new WaitForSecondsRealtime(8.0f); // dwell: ~32 frames at 4fps for FINISHED/NEXT capture

            // ── Step 7: Play Hole 2 (Par 4) ──────────────────────────────────
            bot.LogStep("=== TournamentRoundLoop: Click PLAY (Hole 2) ===");
            yield return bot.Click("PLAY", settleSeconds: 2.0f);
            yield return bot.WaitForAnyHoleGeoScene(timeoutSeconds: 60f);
            yield return new WaitForSecondsRealtime(3.0f); // hole loaded, physics ready

            bot.LogStep("=== TournamentRoundLoop: PlayHoleToCup(par=4) ===");
            yield return bot.PlayHoleToCup(par: 4, firstStrokePowerOverride: 0.85f);
            yield return new WaitForSecondsRealtime(2.0f); // post-hole screen transition

            // ── Step 8: Navigate to Leaderboard ──────────────────────────────
            // After 2 holes the tournament is still InProgress (kasumigaseki is 18 holes).
            // TournamentRoundHandler routes back to TournamentHoleSelection after each hole.
            // The podium icon (LeaderboardButton, top-right on TournamentHoleSelection) is
            // the real player-facing entry to the Leaderboard screen — click it now.
            yield return bot.WaitForScreen("TournamentHoleSelection", timeoutSeconds: 30f);
            yield return new WaitForSecondsRealtime(1.5f); // screen settled after hole unload
            bot.LogStep("=== TournamentRoundLoop: Click LeaderboardButton (→ TournamentLeaderboard) ===");
            yield return bot.Click("LeaderboardButton", settleSeconds: 1.5f);
            yield return bot.WaitForScreen("TournamentLeaderboard", timeoutSeconds: 20f);
            yield return new WaitForSecondsRealtime(3.0f); // leaderboard fully visible — good frame for capture

            // Final capture: leaderboard with real standings
            yield return bot.Capture("leaderboard_final");
            yield return new WaitForSecondsRealtime(2.0f);

            bot.LogStep("=== TournamentRoundLoop: SEQUENCE COMPLETE ===");
        }

        /// <summary>
        /// Clear the kasumigaseki tournament entry so the next run boots in "SIGN UP" state.
        /// Call this after a dry run that registered kasumigaseki. Uses reflection to avoid
        /// a static reference to Assembly-CSharp types.
        /// </summary>
        public static void ClearKasumigasekiEntryStatic()
        {
            // Find SaveDataHost
            var saveHost = UnityEngine.Object.FindObjectOfType<Golfin.Save.SaveDataHost>();
            if (saveHost == null)
            {
                Debug.LogError("[TournamentLoopBotHost] ClearKasumigasekiEntry: SaveDataHost not found.");
                return;
            }

            // Use reflection to access TournamentProgressService or SaveData.tournamentEntries
            // and remove the kasumigaseki entry.
            try
            {
                // Try TournamentProgressService path first
                var allBehaviours = UnityEngine.Object.FindObjectsOfType<MonoBehaviour>();
                foreach (var mb in allBehaviours)
                {
                    string typeName = mb.GetType().Name;
                    if (typeName == "TournamentProgressService" || typeName == "TournamentService")
                    {
                        // Try calling a Reset or Clear method
                        var resetMethod = mb.GetType().GetMethod("ResetEntry",
                            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        if (resetMethod == null)
                            resetMethod = mb.GetType().GetMethod("ClearEntry",
                                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        if (resetMethod != null)
                        {
                            resetMethod.Invoke(mb, new object[] { "kasumigaseki_open" });
                            Debug.Log($"[TournamentLoopBotHost] ClearKasumigasekiEntry via {typeName}.{resetMethod.Name}");
                            return;
                        }

                        // Try removing from tournamentEntries dictionary directly via reflection
                        var dataField = saveHost.GetType().GetField("_data",
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        if (dataField == null)
                            dataField = saveHost.GetType().GetField("Data",
                                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                        if (dataField != null)
                        {
                            var saveData = dataField.GetValue(saveHost);
                            if (saveData != null)
                            {
                                var entriesField = saveData.GetType().GetField("tournamentEntries",
                                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                                if (entriesField != null)
                                {
                                    var dict = entriesField.GetValue(saveData) as System.Collections.IDictionary;
                                    if (dict != null && dict.Contains("kasumigaseki_open"))
                                    {
                                        dict.Remove("kasumigaseki_open");
                                        // Flush
                                        var flushMethod = saveHost.GetType().GetMethod("FlushNow",
                                            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                                        flushMethod?.Invoke(saveHost, null);
                                        Debug.Log("[TournamentLoopBotHost] ClearKasumigasekiEntry: removed from tournamentEntries + flushed.");
                                        return;
                                    }
                                }
                            }
                        }
                    }
                }
                Debug.LogWarning("[TournamentLoopBotHost] ClearKasumigasekiEntry: could not find entry to clear. May already be clean.");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[TournamentLoopBotHost] ClearKasumigasekiEntry error: {ex.Message}");
            }
        }
    }
}
#endif
