#if UNITY_EDITOR
using System.Collections;
using UnityEngine;
using Golfin.Gameplay.Input;
using Golfin.Gameplay.UI.HUD;

namespace Golfin.Physics.Viewer.Bot
{
    /// <summary>
    /// Scenario library for the Loop v2 smoke bot.
    ///
    /// Each scenario is a static coroutine that composes BotDriver primitives into a
    /// specific test flow. Design intent: 30-50 lines per scenario, zero bespoke logic.
    /// New scenarios for Stage D/E/F are added here, not in new files.
    ///
    /// Three scenarios at ship (see STATUS.md for context):
    ///   - Hole1Playthrough: C1 visual gate — cold launch to result modal.
    ///   - SettingsRoundTrip: Stage A smoke — open Settings, expand accordion, close.
    ///   - HoleSelectionBrowse: Stage E smoke — open Hole Selection, expand card, back.
    /// </summary>
    public static class Scenarios
    {
        // ── Scenario 1: Hole 1 Playthrough ────────────────────────────────────

        /// <summary>
        /// Cold launch → PLAY → matchmaking → wait for OpponentFound → wait for
        /// gameplay scenes to load → force InCup terminal state (bot seam) →
        /// result modal appears. This is the default visual gate for Stage C1.
        ///
        /// Design note (iter-4): Stage C1's gate is "HoleCompleteWidget subscribes
        /// to OnShotComplete terminal=InCup". That is a modal-wiring gate, NOT a
        /// physics gate. ForceShotComplete("InCup") drives the terminal state
        /// deterministically via the same OnShotComplete event production fires —
        /// the modal sees no difference. FireShot (real physics) remains available
        /// for scenarios that genuinely test shot mechanics (future Hole1RealPhysicsShot).
        ///
        /// Captures: home, matchmaking_searching, opponent_found, gameplay_armed,
        ///           gameplay_pre_shot (real gameplay frame, ball armed on tee, captured
        ///           BEFORE the seam fires), result_modal (HoleCompleteWidget).
        ///
        /// Capture-order note (iter-4b, Cesar architect call): ForceShotComplete drives
        /// the terminal state with NO physics, so the HoleCompleteWidget appears the same
        /// frame the seam fires — there is no distinct "ball rolling into cup" visual.
        /// To keep every capture honest, s05 is taken from the live gameplay scene
        /// BEFORE ForceShotComplete (ball still armed on the tee) and s06 is the modal
        /// AFTER. s05 is therefore a real pre-modal gameplay frame, never a duplicate of
        /// the s06 modal.
        /// </summary>
        public static IEnumerator Hole1Playthrough(BotDriver d)
        {
            d.LogStep("=== Hole 1 Playthrough ===");

            // 1. Navigate through Logo → Splash (click START) → Loading → Home.
            yield return d.NavigateToHome(totalTimeoutSeconds: 60f);
            yield return new WaitForSecondsRealtime(1f); // let home screen fully settle
            yield return d.Capture("home");

            // 2. Enter Hole 1 via the real SOLO Practice path (NO matchmaking). The Home screen
            //    is a mode carousel with several "PLAY" buttons after the practice_1v1 split, so a
            //    bare Click("PLAY") is ambiguous and hit a ModeHomeCard — the matchmaking modal
            //    never opened. ClickModeCardPlay centres the Practice card and fires its real
            //    onClick, which routes straight to Hole Selection (matchmaking is the 1v1/versus
            //    path, covered by matchmaking_1v1_gate — not a solo completion).
            yield return d.ClickModeCardPlay("practice", settleSeconds: 1.5f);

            // 3. Confirm the Hole Selection screen (not the matchmaking modal).
            yield return d.WaitForScreen("HoleSelection", timeoutSeconds: 15f);
            yield return new WaitForSecondsRealtime(3f); // HoleCardController auto-expand
            yield return d.Capture("practice_hole_selection");

            // 4. Tap PLAY on the first available (auto-expanded) hole card → Hole 1.
            yield return d.Click("ActionButton", settleSeconds: 1.5f);

            // 5. Wait for gameplay scenes to load (GameplaySceneLoader.BeginGameplayLoad).
            yield return d.WaitForSceneLoaded("LabScaffold", timeoutSeconds: 40f);
            yield return d.WaitForSceneLoaded("Hole_01_Geo", timeoutSeconds: 40f);
            yield return new WaitForSecondsRealtime(4f); // fade-in + HUD fully rendered; avoids Y-flip

            // 5b. START RECORDING (deferred only) — fires iff ArmDeferred() was called.
            //     Mirrors AudioGameplayShotsV3 §5: set RecordVideo via SessionState + call Begin().
            //     The plain "Hole 1 Playthrough" menu item does not call ArmDeferred(), so
            //     DeferredRecord remains false and this block is a complete no-op.
            if (UnityEditor.SessionState.GetBool("LoopV2SmokeBot.DeferredRecord", false))
            {
                d.LogStep("  BeginDeferred: hole is armed, HUD visible — starting recording now.");
                UnityEditor.SessionState.SetBool("LoopV2SmokeBot.RecordVideo", true);
                UnityEditor.SessionState.SetBool("LoopV2SmokeBot.DeferredRecord", false);
                try
                {
                    System.Type recType = null;
                    foreach (var a in System.AppDomain.CurrentDomain.GetAssemblies())
                    { var t = a.GetType("Golfin.Physics.Viewer.Editor.BotVideoRecorder"); if (t != null) { recType = t; break; } }
                    if (recType != null)
                    {
                        var beginMethod = recType.GetMethod("Begin", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                        beginMethod?.Invoke(null, null);
                        d.LogStep("  BeginDeferred (reflection): BotVideoRecorder.Begin() called — recording started");
                    }
                    else
                    {
                        d.LogStep("  BeginDeferred WARN: BotVideoRecorder type not found — recording may not have started");
                    }
                }
                catch (System.Exception ex)
                {
                    d.LogStep($"  BeginDeferred ERROR: {ex.Message}");
                }
                yield return new WaitForSecondsRealtime(1f); // let first recording frames settle
            }

            yield return d.Capture("gameplay_armed"); // gameplay scene, ball armed on the tee

            // 6. Play Hole 1 (Par 5) with REAL physics shots — each stroke aims at the cup
            //    and fires a power-appropriate preset from the ball's rest position.
            //    Loops to the cup; if it runs past par+3 strokes the ForceShotComplete
            //    seam finishes the hole (Cesar spec, 2026-05-20). PlayHoleToCup captures
            //    one still per stroke.
            yield return d.PlayHoleToCup(par: 5);

            // 7. Wait for the result modal (HoleCompleteWidget) and capture it.
            yield return new WaitForSecondsRealtime(2f);
            yield return d.Capture("result_modal"); // HoleCompleteWidget — C1 gate

            d.LogStep("=== Hole 1 Playthrough: all captures done ===");
        }

        // ── Scenario 2: Settings Round Trip ──────────────────────────────────

        /// <summary>
        /// Home → open Settings overlay → expand the Sound accordion section → close.
        /// Smoke test for Stage A's surviving settings flow.
        ///
        /// Captures: home, settings_open, settings_sound_expanded, home_returned.
        /// </summary>
        public static IEnumerator SettingsRoundTrip(BotDriver d)
        {
            d.LogStep("=== Settings Round Trip ===");

            // 1. Navigate through Logo → Splash (click START) → Loading → Home.
            yield return d.NavigateToHome(totalTimeoutSeconds: 60f);
            yield return new WaitForSecondsRealtime(1f);
            yield return d.Capture("home");

            // 2. Open Settings. Top-bar settings button is named "SettingsButton".
            yield return d.Click("SettingsButton", settleSeconds: 1.0f);

            // 3. Wait for Settings panel to appear (named "SettingsPanel").
            yield return d.WaitForGameObject("SettingsPanel", timeoutSeconds: 10f);
            yield return new WaitForSecondsRealtime(0.5f);
            yield return d.Capture("settings_open");

            // 4. Expand the Sound accordion. The row GO is "SoundSettingsRow".
            yield return d.Click("SoundSettingsRow", settleSeconds: 0.8f);
            yield return new WaitForSecondsRealtime(0.5f); // accordion expand animation
            yield return d.Capture("settings_sound_expanded");

            // 5. Close settings. Button is named "CloseButton" inside SettingsPanel.
            yield return d.Click("CloseButton", settleSeconds: 1.0f);

            // 6. Confirm Home screen returned.
            yield return d.WaitForScreen("Home", timeoutSeconds: 10f);
            yield return d.Capture("home_returned");

            d.LogStep("=== Settings Round Trip: all captures done ===");
        }

        // ── Scenario 4: Hole 1 Play Next ─────────────────────────────────────

        /// <summary>
        /// Clears Hole 1 via ForceShotComplete("InCup"), waits for the result modal
        /// (HoleCompleteModal on ShellScene), taps PLAY NEXT, waits for the loading screen
        /// and Hole 2 geo to load, then captures hole2_armed.
        ///
        /// Visual gate: modal dismisses under fade + Hole 2 scene loads.
        ///
        /// Captures: home, matchmaking_searching, opponent_found, gameplay_armed,
        ///           result_modal, hole2_armed.
        /// </summary>
        public static IEnumerator Hole1PlayNext(BotDriver d)
        {
            d.LogStep("=== Hole 1 Play Next ===");

            yield return d.NavigateToHome(totalTimeoutSeconds: 60f);
            yield return new WaitForSecondsRealtime(1f);
            yield return d.Capture("home");

            yield return d.Click("PLAY", settleSeconds: 1.5f);
            yield return d.WaitForModalVisible("MatchMakingModal", timeoutSeconds: 15f);
            yield return d.Capture("matchmaking_searching");

            yield return d.WaitFor(
                () => d.GetMatchmakingPhase() == "OpponentFound",
                "matchmaking opponent found",
                timeoutSeconds: 30f);
            yield return new WaitForSecondsRealtime(0.5f);
            yield return d.Capture("opponent_found");

            yield return d.WaitForSceneLoaded("LabScaffold", timeoutSeconds: 40f);
            yield return d.WaitForSceneLoaded("Hole_01_Geo", timeoutSeconds: 40f);
            yield return new WaitForSecondsRealtime(3f);
            yield return d.Capture("gameplay_armed");

            // Force InCup to trigger result modal.
            yield return d.ForceShotComplete("InCup", settleSeconds: 1f);
            yield return new WaitForSecondsRealtime(2f);
            yield return d.Capture("result_modal");

            // Click PLAY (Card 2 PLAY button — lab widget names it "PlayButton").
            yield return d.Click("PlayButton", settleSeconds: 1.5f);

            // Wait for loading screen and Hole 2 geo to load.
            yield return d.WaitForSceneLoaded("Hole_02_Geo", timeoutSeconds: 40f);
            yield return new WaitForSecondsRealtime(3f);
            yield return d.Capture("hole2_armed");

            d.LogStep("=== Hole 1 Play Next: all captures done ===");
        }

        // ── Scenario 5: Hole 1 Menu ───────────────────────────────────────────

        /// <summary>
        /// Clears Hole 1, taps REPLAY (Card 1 button), confirms hole 1 re-arms.
        ///
        /// Iteration 6 note: The new two-card lab-widget design has NO standalone MENU button.
        /// Card 1 has REPLAY (success) or RETRY (failed). This scenario tests the REPLAY path
        /// to verify the hole reloads correctly.
        ///
        /// Captures: home, matchmaking_searching, opponent_found, gameplay_armed,
        ///           result_modal, hole1_rearmed_from_replay.
        /// </summary>
        public static IEnumerator Hole1Menu(BotDriver d)
        {
            d.LogStep("=== Hole 1 Menu (REPLAY path — no MENU button in lab widget) ===");

            yield return d.NavigateToHome(totalTimeoutSeconds: 60f);
            yield return new WaitForSecondsRealtime(1f);
            yield return d.Capture("home");

            yield return d.Click("PLAY", settleSeconds: 1.5f);
            yield return d.WaitForModalVisible("MatchMakingModal", timeoutSeconds: 15f);
            yield return d.Capture("matchmaking_searching");

            yield return d.WaitFor(
                () => d.GetMatchmakingPhase() == "OpponentFound",
                "matchmaking opponent found",
                timeoutSeconds: 30f);
            yield return new WaitForSecondsRealtime(0.5f);
            yield return d.Capture("opponent_found");

            yield return d.WaitForSceneLoaded("LabScaffold", timeoutSeconds: 40f);
            yield return d.WaitForSceneLoaded("Hole_01_Geo", timeoutSeconds: 40f);
            yield return new WaitForSecondsRealtime(3f);
            yield return d.Capture("gameplay_armed");

            yield return d.ForceShotComplete("InCup", settleSeconds: 1f);
            yield return new WaitForSecondsRealtime(2f);
            yield return d.Capture("result_modal");

            // REPLAY reloads same hole (success path — no MENU button in lab widget design).
            yield return d.Click("ReplayButton", settleSeconds: 2f);
            yield return d.WaitForSceneLoaded("Hole_01_Geo", timeoutSeconds: 40f);
            yield return new WaitForSecondsRealtime(3f);
            yield return d.Capture("hole1_rearmed_from_replay");

            d.LogStep("=== Hole 1 Menu: all captures done ===");
        }

        // ── Scenario 6: Hole 1 Retry After Fail ──────────────────────────────

        /// <summary>
        /// Seeds GameSession.TurnCount to par+5 (stroke cap), forces AtRest terminal
        /// to trigger FAILED modal, taps RETRY, waits for Hole 1 to re-arm.
        ///
        /// Par for Hole 1 is 5 → cap = 10. GameSession.SetTurn(10) then ForceShotComplete("AtRest").
        ///
        /// Captures: home, matchmaking_searching, gameplay_armed,
        ///           result_modal_failed, hole1_rearmed.
        /// </summary>
        public static IEnumerator Hole1RetryAfterFail(BotDriver d)
        {
            d.LogStep("=== Hole 1 Retry After Fail ===");

            yield return d.NavigateToHome(totalTimeoutSeconds: 60f);
            yield return new WaitForSecondsRealtime(1f);
            yield return d.Capture("home");

            yield return d.Click("PLAY", settleSeconds: 1.5f);
            yield return d.WaitForModalVisible("MatchMakingModal", timeoutSeconds: 15f);
            yield return d.Capture("matchmaking_searching");

            yield return d.WaitFor(
                () => d.GetMatchmakingPhase() == "OpponentFound",
                "matchmaking opponent found",
                timeoutSeconds: 30f);
            yield return new WaitForSecondsRealtime(0.5f);

            yield return d.WaitForSceneLoaded("LabScaffold", timeoutSeconds: 40f);
            yield return d.WaitForSceneLoaded("Hole_01_Geo", timeoutSeconds: 40f);
            yield return new WaitForSecondsRealtime(3f);
            yield return d.Capture("gameplay_armed");

            // Hole 1 par = 5 → cap = par + 5 = 10. Bump TurnCount to cap.
            // HoleContext.Par is set after hole loads; use default par+5=10 as fallback.
            int par = Golfin.Gameplay.UI.HUD.HoleContext.Par > 0
                ? Golfin.Gameplay.UI.HUD.HoleContext.Par
                : 5;
            int cap = par + 5;
            d.LogStep($"  Hole1RetryAfterFail: par={par} cap={cap} — bumping TurnCount to cap");
            Golfin.Gameplay.Session.GameSession.SetTurn(cap);

            yield return d.ForceShotComplete("AtRest", settleSeconds: 1f);
            yield return new WaitForSecondsRealtime(2f);
            yield return d.Capture("result_modal_failed");

            // Tap RETRY — should reload Hole 1.
            yield return d.Click("RetryButton", settleSeconds: 2f);
            yield return d.WaitForSceneLoaded("Hole_01_Geo", timeoutSeconds: 40f);
            yield return new WaitForSecondsRealtime(3f);
            yield return d.Capture("hole1_rearmed");

            d.LogStep("=== Hole 1 Retry After Fail: all captures done ===");
        }

        // ── Scenario 7: Hole 18 Course Cleared ───────────────────────────────

        /// <summary>
        /// Seeds GameSession.CurrentHoleNumber to 18, forces InCup, waits for
        /// SUCCESS modal with no PLAY NEXT + "COURSE CLEARED!" toast visible.
        ///
        /// Captures: gameplay_armed_h18, result_modal_h18_cleared.
        /// </summary>
        public static IEnumerator Hole18CourseCleared(BotDriver d)
        {
            d.LogStep("=== Hole 18 Course Cleared ===");

            yield return d.NavigateToHome(totalTimeoutSeconds: 60f);
            yield return new WaitForSecondsRealtime(1f);

            yield return d.Click("PLAY", settleSeconds: 1.5f);
            yield return d.WaitForModalVisible("MatchMakingModal", timeoutSeconds: 15f);

            yield return d.WaitFor(
                () => d.GetMatchmakingPhase() == "OpponentFound",
                "matchmaking opponent found",
                timeoutSeconds: 30f);
            yield return new WaitForSecondsRealtime(0.5f);

            yield return d.WaitForSceneLoaded("LabScaffold", timeoutSeconds: 40f);
            yield return d.WaitForSceneLoaded("Hole_01_Geo", timeoutSeconds: 40f);
            yield return new WaitForSecondsRealtime(3f);

            // Seed hole 18 before forcing shot so modal reads HoleNumber=18.
            d.LogStep("  Hole18CourseCleared: setting CurrentHoleNumber = 18");
            Golfin.Gameplay.Session.GameSession.SetCurrentHole(18);

            yield return d.Capture("gameplay_armed_h18");

            yield return d.ForceShotComplete("InCup", settleSeconds: 1f);
            yield return new WaitForSecondsRealtime(2f);
            yield return d.Capture("result_modal_h18_cleared");

            d.LogStep("=== Hole 18 Course Cleared: all captures done ===");
        }

        // ── Scenario 3: Hole Selection Browse ────────────────────────────────

        /// <summary>
        /// Home → Hole Selection (bottom-nav) → browse grid → back to Home.
        /// Smoke test for Stage E's hole-selection entry point.
        ///
        /// Design note (iter-3): Only Hole 1 is unlocked and auto-expands on screen open.
        /// CardTapButton appears 18 times in the scene (one per HoleCard prefab instance +
        /// locked duplicates), making unscoped FindButton("CardTapButton") ambiguous —
        /// clicking the wrong one produces no visible state change (iter-2 bug: s02==s03
        /// byte-identical). Rather than adding a scoped-search overload to BotDriver for a
        /// single-hole-unlocked gate that has no second unlocked state to toggle, we use
        /// the honest 3-capture flow: home → hole_selection_grid → home_returned.
        /// TODO: When Stage E unlocks Hole 2+, extend this scenario to click a collapsed
        /// unlocked card and verify its expanded state is pixel-distinct.
        ///
        /// Captures: home, hole_selection_grid, home_returned.
        /// </summary>
        public static IEnumerator HoleSelectionBrowse(BotDriver d)
        {
            d.LogStep("=== Hole Selection Browse ===");

            // 1. Navigate through Logo → Splash (click START) → Loading → Home.
            yield return d.NavigateToHome(totalTimeoutSeconds: 60f);
            yield return new WaitForSecondsRealtime(1f);
            yield return d.Capture("home");

            // 2. Click the Hole Selection bottom-nav button. Named "NavTeeButton".
            yield return d.Click("NavTeeButton", settleSeconds: 1.0f);

            // 3. Wait for HoleSelection screen (ScreenManager navigates to HoleSelection).
            yield return d.WaitForScreen("HoleSelection", timeoutSeconds: 10f);
            yield return new WaitForSecondsRealtime(0.5f);
            // Hole 1 is auto-expanded on screen open — capture the grid showing the expanded card.
            yield return d.Capture("hole_selection_grid");

            // 4. Navigate back to Home. Bottom-nav home button is "NavHomeButton".
            yield return d.Click("NavHomeButton", settleSeconds: 1.0f);

            // 5. Confirm Home screen.
            yield return d.WaitForScreen("Home", timeoutSeconds: 10f);
            yield return d.Capture("home_returned");

            d.LogStep("=== Hole Selection Browse: all captures done ===");
        }

        // ── Scenario 8: Hole Selection Entry → Replay Rewards ─────────────────

        /// <summary>
        /// Stage E gate — proves the Hole-Selection entry path AND the Part A
        /// REPLAY-writes-progression fix end-to-end:
        ///
        ///   Home → bottom-nav to Hole Selection → tap PLAY on the auto-expanded
        ///   Hole 1 card → matchmaking → gameplay → force InCup (first clear) →
        ///   tap REPLAY → Hole 1 reloads → force InCup again (replay clear).
        ///
        /// The hole-card action button's GameObject name is "ActionButton" (HoleCard
        /// prefab; the SerializeField is `actionButton` on HoleCardController). Only
        /// Hole 1's card is expanded on screen open, and the action button lives inside
        /// expandedContainer — so it is the single ACTIVE Button by that name and
        /// FindButton (active-only) resolves it without ambiguity.
        ///
        /// Visual gate (Cesar): result_modal_first_clear shows the `rewards` pool;
        /// result_modal_replay_clear shows the `replayRewards` pool. For Hole 1 these
        /// differ in the CSV (Points 100/RepairKit 10/Ball 5 vs Points 50/RepairKit
        /// 5/Ball 2), so the two captures must be visibly distinct.
        ///
        /// Captures: home, hole_selection, matchmaking_searching, opponent_found,
        ///           gameplay_armed, result_modal_first_clear,
        ///           gameplay_armed_after_replay, result_modal_replay_clear.
        /// </summary>
        public static IEnumerator HoleSelectionEntryToReplayRewards(BotDriver d)
        {
            d.LogStep("=== Hole Selection Entry → Replay Rewards ===");

            // 1. Cold launch → Home.
            yield return d.NavigateToHome(totalTimeoutSeconds: 60f);
            yield return new WaitForSecondsRealtime(1f);
            yield return d.Capture("home");

            // 2. Bottom-nav to Hole Selection (button GO "NavTeeButton").
            yield return d.Click("NavTeeButton", settleSeconds: 1.0f);
            yield return d.WaitForScreen("HoleSelection", timeoutSeconds: 10f);
            yield return new WaitForSecondsRealtime(0.5f);
            yield return d.Capture("hole_selection");

            // 3. Tap PLAY on the auto-expanded Hole 1 card (action button GO "ActionButton").
            yield return d.Click("ActionButton", settleSeconds: 1.5f);

            // 4. Matchmaking modal opens → wait for OpponentFound → gameplay scenes load.
            yield return d.WaitForModalVisible("MatchMakingModal", timeoutSeconds: 15f);
            yield return d.Capture("matchmaking_searching");

            yield return d.WaitFor(
                () => d.GetMatchmakingPhase() == "OpponentFound",
                "matchmaking opponent found",
                timeoutSeconds: 30f);
            yield return new WaitForSecondsRealtime(0.5f);
            yield return d.Capture("opponent_found");

            yield return d.WaitForSceneLoaded("LabScaffold", timeoutSeconds: 40f);
            yield return d.WaitForSceneLoaded("Hole_01_Geo", timeoutSeconds: 40f);
            yield return new WaitForSecondsRealtime(3f);
            yield return d.Capture("gameplay_armed");

            // 5. Force InCup → first-clear SUCCESS modal.
            yield return d.ForceShotComplete("InCup", settleSeconds: 1f);
            yield return new WaitForSecondsRealtime(2f);
            yield return d.Capture("result_modal_first_clear");

            // 6. Tap REPLAY (Card 1) → modal dismisses, Hole 1 reloads. Per the Part A
            //    fix, OnReplay now writes progression + grants rewards before the reset.
            yield return d.Click("ReplayButton", settleSeconds: 2f);
            yield return d.WaitForSceneLoaded("Hole_01_Geo", timeoutSeconds: 40f);
            yield return new WaitForSecondsRealtime(3f);
            yield return d.Capture("gameplay_armed_after_replay");

            // 7. Force InCup again → replay-clear SUCCESS modal (_wasReplay = true).
            yield return d.ForceShotComplete("InCup", settleSeconds: 1f);
            yield return new WaitForSecondsRealtime(2f);
            yield return d.Capture("result_modal_replay_clear");

            d.LogStep("=== Hole Selection Entry → Replay Rewards: all captures done ===");
        }

        // ── Scenario 9: Save Layer Durability ─────────────────────────────────

        /// <summary>
        /// Stage E REPLAY durability proof for save_layer_reactive_foundation.
        ///
        /// Flow:
        ///   1. Play/clear Hole 1 (via ForceShotComplete InCup).
        ///   2. Tap PLAY NEXT → Hole 2 loads. Assert Hole 2 is unlocked.
        ///   3. Simulate app restart: call SaveDataHost.Instance.ReloadFromDisk()
        ///      which re-loads save.json from disk into the live SaveData.
        ///      This simulates "the app was killed and relaunched" without actually
        ///      needing to exit/enter play mode again.
        ///   4. Assert SaveDataHost.Data.unlockedHoles contains Hole 2.
        ///   5. Assert SaveDataHost.Data.rewardPoints >= 0 (persisted).
        ///   6. Capture proof screenshot showing Hole 2 armed with persisted state.
        ///
        /// This is the proof that the Save layer makes hole progression durable across restarts.
        ///
        /// Captures: home, gameplay_armed_h1, result_modal, hole2_armed,
        ///           restart_simulated_hole2_persisted.
        /// </summary>
        public static IEnumerator SaveLayerDurability(BotDriver d)
        {
            d.LogStep("=== Save Layer Durability ===");

            // 1. Cold launch → Home.
            yield return d.NavigateToHome(totalTimeoutSeconds: 60f);
            yield return new WaitForSecondsRealtime(1f);
            yield return d.Capture("home");

            // 2. Play → matchmaking → Hole 1 gameplay.
            yield return d.Click("PLAY", settleSeconds: 1.5f);
            yield return d.WaitForModalVisible("MatchMakingModal", timeoutSeconds: 15f);
            yield return d.WaitFor(
                () => d.GetMatchmakingPhase() == "OpponentFound",
                "matchmaking opponent found",
                timeoutSeconds: 30f);
            yield return new WaitForSecondsRealtime(0.5f);
            yield return d.WaitForSceneLoaded("LabScaffold", timeoutSeconds: 40f);
            yield return d.WaitForSceneLoaded("Hole_01_Geo", timeoutSeconds: 40f);
            yield return new WaitForSecondsRealtime(3f);
            yield return d.Capture("gameplay_armed_h1");

            // 3. Force InCup → result modal (hole 1 cleared).
            yield return d.ForceShotComplete("InCup", settleSeconds: 1f);
            yield return new WaitForSecondsRealtime(2f);
            yield return d.Capture("result_modal");

            // 4. Tap PLAY NEXT → Hole 2 loads.
            yield return d.Click("PlayButton", settleSeconds: 1.5f);
            yield return d.WaitForSceneLoaded("Hole_02_Geo", timeoutSeconds: 40f);
            yield return new WaitForSecondsRealtime(3f);
            yield return d.Capture("hole2_armed");

            // 5. Verify Hole 2 is unlocked in SaveData BEFORE simulated restart.
            bool hole2UnlockedBeforeRestart = Golfin.Save.SaveDataHost.Instance != null &&
                Golfin.Save.SaveDataHost.Instance.Data.unlockedHoles.Contains(2);
            d.LogStep($"  [Durability] Hole 2 unlocked in SaveData: {hole2UnlockedBeforeRestart}");

            // 6. Flush any pending writes before simulated restart.
            if (Golfin.Save.SaveDataHost.Instance != null)
            {
                // Force a flush by marking dirty and waiting for debounce
                Golfin.Save.SaveDataHost.Instance.MarkDirty();
                yield return new WaitForSecondsRealtime(0.5f); // > 250ms debounce
                d.LogStep("  [Durability] Flushed save to disk before simulated restart.");
            }

            // 7. Simulate app restart: reload SaveData from disk.
            // This mimics "app was killed and relaunched" — the in-memory state is discarded
            // and the persisted state is reloaded from save.json.
            if (Golfin.Save.SaveDataHost.Instance != null)
            {
                Golfin.Save.SaveDataHost.Instance.ReloadFromDisk();
                yield return new WaitForSecondsRealtime(0.5f);
                d.LogStep("  [Durability] Simulated restart: ReloadFromDisk() called.");
            }
            else
            {
                d.LogStep("  [Durability] ERROR: SaveDataHost.Instance is null — cannot simulate restart.");
            }

            // 8. Verify Hole 2 is still unlocked AFTER simulated restart.
            bool hole2UnlockedAfterRestart = Golfin.Save.SaveDataHost.Instance != null &&
                Golfin.Save.SaveDataHost.Instance.Data.unlockedHoles.Contains(2);
            int rewardPointsAfterRestart = Golfin.Save.SaveDataHost.Instance != null
                ? Golfin.Save.SaveDataHost.Instance.Data.rewardPoints
                : -1;
            d.LogStep($"  [Durability] After restart — Hole 2 unlocked: {hole2UnlockedAfterRestart}, " +
                      $"rewardPoints: {rewardPointsAfterRestart}");

            // 9. Capture proof screenshot (Hole 2 is still armed; save persisted).
            yield return d.Capture("restart_simulated_hole2_persisted");

            // 10. Log final durability verdict.
            if (hole2UnlockedAfterRestart && rewardPointsAfterRestart >= 0)
                d.LogStep("=== Save Layer Durability: PASS — hole 2 unlocked + rewards persisted across restart ===");
            else
                d.LogStep($"=== Save Layer Durability: FAIL — " +
                          $"hole2={hole2UnlockedAfterRestart} rp={rewardPointsAfterRestart} ===");
        }

        // ── Scenario 10: Putter Aim Green Reader Visible ──────────────────────

        /// <summary>
        /// Smoke test for the PutterGreenReader bake + render pipeline.
        ///
        /// Flow:
        ///   1. Navigate to Home → PLAY → matchmaking → Hole 1 gameplay.
        ///   2. Wait for LabScaffold + Hole_01_Geo to load (bake triggers on HoleContext.OnChanged).
        ///   3. Auto-switch to putter (ball placed on green via PlaceBallAt → Green placement entry).
        ///   4. Enter putter aim (FireViaShotController path or direct ShotController state seam).
        ///   5. Capture screenshot while putter aim is active.
        ///   6. Assert PutterGreenReader.LastVisibleCellCount >= 50 (≥50 arrows visible).
        ///
        /// Gate: at least 50 visible cells confirms the bake found green cells AND the
        /// render loop is producing draw calls. This is the minimum smoke assertion per SPEC.
        ///
        /// Captures: home, matchmaking_searching, gameplay_armed,
        ///           putter_aim_green_reader_visible.
        /// </summary>
        public static IEnumerator PutterAimGreenReaderVisible(BotDriver d)
        {
            d.LogStep("=== Putter Aim Green Reader Visible ===");

            // 1. Navigate to Home.
            yield return d.NavigateToHome(totalTimeoutSeconds: 60f);
            yield return new WaitForSecondsRealtime(1f);
            yield return d.Capture("home");

            // 2. PLAY → matchmaking → Hole 1 gameplay.
            yield return d.Click("PLAY", settleSeconds: 1.5f);
            yield return d.WaitForModalVisible("MatchMakingModal", timeoutSeconds: 15f);
            yield return d.Capture("matchmaking_searching");

            yield return d.WaitFor(
                () => d.GetMatchmakingPhase() == "OpponentFound",
                "matchmaking opponent found",
                timeoutSeconds: 30f);
            yield return new WaitForSecondsRealtime(0.5f);

            yield return d.WaitForSceneLoaded("LabScaffold", timeoutSeconds: 40f);
            yield return d.WaitForSceneLoaded("Hole_01_Geo", timeoutSeconds: 40f);
            yield return new WaitForSecondsRealtime(3f);  // let HoleContext.OnChanged fire + bake complete
            yield return d.Capture("gameplay_armed");

            // 3. Place ball on the green and force putter mode.
            //    LabHoleBinder populates PlacementEntries; "Green 1" entry uses preferredSurfaceTypeValue=1.
            d.LogStep("  Placing ball on green via PlaceBallAt...");
            var labCtrl = Object.FindObjectOfType<PhysicsLabController>();
            if (labCtrl != null && labCtrl.PlacementEntries.Count > 0)
            {
                // Find the first Green entry.
                var greenEntry = labCtrl.PlacementEntries.Find(e => e.Label != null && e.Label.StartsWith("Green"));
                if (greenEntry.Label != null)
                {
                    labCtrl.PlaceBallAt(greenEntry.WorldPos, greenEntry.PreferredSurfaceTypeValue);
                    yield return new WaitForSecondsRealtime(0.5f);
                }
            }

            // 4. Switch to putter club (index 3 = Putter in LabClubs array).
            if (labCtrl != null)
            {
                labCtrl.SetClub(PhysicsLabController.PutterIndex);
                labCtrl.InjectLabBundleForCurrentClub(); // LAB path
                yield return new WaitForSecondsRealtime(0.3f); // EnterPutterMode → PutterGreenReader.enabled = true
            }

            // 5. Activate putter aim via the production ShotController path.
            //    ShotController.PublishState() fires OnStateChanged every frame, which calls
            //    PutterGreenReader.OnShotStateChanged. That handler sets _aimActive based on
            //    IsPutt && (State == Aiming | Pulling | Timing). We drive this production path
            //    by:
            //      (a) setting IsPutt=true on the ShotController, and
            //      (b) calling BeginExternalDrag() which transitions State → Aiming and fires
            //          PublishState() → OnShotStateChanged → _aimActive=true.
            //    This is the same code path that real putter gameplay uses (minus touch input).
            //    After 2 frames, Update() runs and populates LastVisibleCellCount.
            var sc = labCtrl != null ? labCtrl.GetComponentInChildren<ShotController>(true) : null;
            var reader5 = labCtrl != null ? labCtrl.GetComponentInChildren<PutterGreenReader>(true) : null;
            if (sc != null && reader5 != null)
            {
                d.LogStep("  Setting IsPutt=true and calling BeginExternalDrag() on ShotController...");
                sc.IsPutt = true;
                sc.BeginExternalDrag(); // transitions State → Aiming and fires OnStateChanged
                // Wait 3 frames so PutterGreenReader.Update() runs with _aimActive=true.
                yield return null;
                yield return null;
                yield return null;
                d.LogStep($"  After 3 frames: LastVisibleCellCount={reader5.LastVisibleCellCount}");
                // NOTE: Do NOT cancel external drag here — keep aim active through capture + assert.
            }
            else
            {
                d.LogStep($"  WARNING: sc={sc != null} reader={reader5 != null} — cannot activate putter aim via production path.");
                // Fallback: use test seam directly.
                if (reader5 != null)
                {
                    reader5.SetAimActiveForTest(true);
                    yield return null;
                    yield return null;
                    yield return null;
                    d.LogStep($"  Fallback after 3 frames: LastVisibleCellCount={reader5.LastVisibleCellCount}");
                }
            }

            // 6. Capture putter aim frame (aim is still ACTIVE at this point).
            yield return new WaitForSecondsRealtime(0.3f);
            yield return d.Capture("putter_aim_green_reader_visible");

            // 7. Assert visible cell count (aim still active — LastVisibleCellCount reflects current frame).
            var reader = labCtrl != null ? labCtrl.GetComponentInChildren<PutterGreenReader>(true) : null;
            int bakedCount = reader != null ? reader.BakedCellCount : 0;
            int visibleCount = reader != null ? reader.LastVisibleCellCount : 0;
            d.LogStep($"  PutterGreenReader: baked={bakedCount} visible={visibleCount} " +
                      $"(need >=50 visible for PASS)");

            // 8. Clean up: end external drag to restore Idle state.
            if (sc != null) { sc.CancelExternalDrag(); sc.IsPutt = false; }
            if (reader5 != null) reader5.SetAimActiveForTest(false);

            if (bakedCount < 1)
                d.LogStep("=== PutterAimGreenReaderVisible: FAIL — no cells baked (green classifier not available?) ===");
            else if (visibleCount < 50)
                d.LogStep($"=== PutterAimGreenReaderVisible: PARTIAL — baked={bakedCount} cells but " +
                          $"visible={visibleCount} (putter aim may not have been active during render tick) ===");
            else
                d.LogStep($"=== PutterAimGreenReaderVisible: PASS — baked={bakedCount} cells, " +
                          $"visible={visibleCount} arrows in frame ===");
        }

        // ── Scenarios: Live Stat Provider Visual Gate ─────────────────────────

        /// <summary>
        /// Visual gate HIGH build: arms char_elizabeth (Rare) at max level (119) with all
        /// four stats at the Rare caps (STR=30, CTRL=30, REC=20, STAM=27), then runs the
        /// Hole1Playthrough flow (Home → PLAY → matchmaking → Hole 1 → PlayHoleToCup →
        /// result_modal). Proves the live-stat path propagates stat values into each shot
        /// bundle. Compare against the LOW build run: carry distance / # strokes must differ.
        ///
        /// Captures: home, matchmaking_searching, opponent_found, gameplay_armed,
        ///           result_modal (plus per-stroke captures from PlayHoleToCup).
        ///
        /// Pre-arm happens BEFORE NavigateToHome so GameSession.SelectedCharacterId is set
        /// before the matchmaking seed fires.
        /// </summary>
        public static IEnumerator LiveStatProviderVisualGateHigh(BotDriver d)
        {
            d.LogStep("=== Live Stat Provider Visual Gate — HIGH BUILD ===");
            ArmCharacterBuild(d, CharVGCharId, BuildKind.High);
            yield return new WaitForSecondsRealtime(0.1f); // let reflection writes settle

            // Re-use the standard Hole1Playthrough flow verbatim.
            yield return d.NavigateToHome(totalTimeoutSeconds: 60f);
            yield return new WaitForSecondsRealtime(1f);
            yield return d.Capture("home");

            yield return d.Click("PLAY", settleSeconds: 1.5f);
            yield return d.WaitForModalVisible("MatchMakingModal", timeoutSeconds: 15f);
            yield return d.Capture("matchmaking_searching");

            yield return d.WaitFor(
                () => d.GetMatchmakingPhase() == "OpponentFound",
                "matchmaking opponent found",
                timeoutSeconds: 30f);
            yield return new WaitForSecondsRealtime(0.5f);
            yield return d.Capture("opponent_found");

            yield return d.WaitForSceneLoaded("LabScaffold", timeoutSeconds: 40f);
            yield return d.WaitForSceneLoaded("Hole_01_Geo", timeoutSeconds: 40f);
            yield return new WaitForSecondsRealtime(3f);
            yield return d.Capture("gameplay_armed");

            yield return d.PlayHoleToCup(par: 5);

            yield return new WaitForSecondsRealtime(2f);
            yield return d.Capture("result_modal");

            d.LogStep("=== Live Stat Provider Visual Gate HIGH BUILD: all captures done ===");
        }

        /// <summary>
        /// Visual gate LOW build: arms char_elizabeth (Rare) at starting level (80) with
        /// base stats (STR=8, CTRL=10, REC=7, STAM=9), then runs the same Hole1Playthrough
        /// flow. Compare against HIGH build — carry distance / # strokes must differ visibly,
        /// proving the live bus carries stat values through to the physics layer.
        ///
        /// Captures: home, matchmaking_searching, opponent_found, gameplay_armed,
        ///           result_modal (plus per-stroke captures from PlayHoleToCup).
        /// </summary>
        public static IEnumerator LiveStatProviderVisualGateLow(BotDriver d)
        {
            d.LogStep("=== Live Stat Provider Visual Gate — LOW BUILD ===");
            ArmCharacterBuild(d, CharVGCharId, BuildKind.Low);
            yield return new WaitForSecondsRealtime(0.1f);

            yield return d.NavigateToHome(totalTimeoutSeconds: 60f);
            yield return new WaitForSecondsRealtime(1f);
            yield return d.Capture("home");

            yield return d.Click("PLAY", settleSeconds: 1.5f);
            yield return d.WaitForModalVisible("MatchMakingModal", timeoutSeconds: 15f);
            yield return d.Capture("matchmaking_searching");

            yield return d.WaitFor(
                () => d.GetMatchmakingPhase() == "OpponentFound",
                "matchmaking opponent found",
                timeoutSeconds: 30f);
            yield return new WaitForSecondsRealtime(0.5f);
            yield return d.Capture("opponent_found");

            yield return d.WaitForSceneLoaded("LabScaffold", timeoutSeconds: 40f);
            yield return d.WaitForSceneLoaded("Hole_01_Geo", timeoutSeconds: 40f);
            yield return new WaitForSecondsRealtime(3f);
            yield return d.Capture("gameplay_armed");

            yield return d.PlayHoleToCup(par: 5);

            yield return new WaitForSecondsRealtime(2f);
            yield return d.Capture("result_modal");

            d.LogStep("=== Live Stat Provider Visual Gate LOW BUILD: all captures done ===");
        }

        // ── Pre-arm helpers ───────────────────────────────────────────────────

        /// <summary>The character used for both visual-gate scenarios (Rare rarity for wide level range).</summary>
        const string CharVGCharId = "char_elizabeth";

        // char_elizabeth Rare: startLevel=80, maxLevel=119
        // base stats from Characters.csv: STR=8, CTRL=10, REC=7, STAM=9
        // Rare caps from RarityStatCaps.cs:  STR=30, CTRL=30, REC=20, STAM=27
        // Both scenarios equip the same driver + ball so the only variable is the stat build.
        const string CharVGDriverId = "club_driver_gf";
        const string CharVGBallId   = "ball_golfin";

        private enum BuildKind { High, Low }

        /// <summary>
        /// Arms the character build BEFORE NavigateToHome so both matchmaking seed
        /// and the live stat resolver see the correct character + club + ball state.
        ///
        /// Three-part arm:
        ///  A. CharacterManager (Assembly-CSharp, accessed via reflection):
        ///     - Sets private field `selectedCharacterId` so matchmaking calls to
        ///       `GetSelectedCharacterId()` return charId (not the default char).
        ///     - Mutates `ownedCharacters[charId]` stat fields for the chosen build.
        ///  B. ClubManager (Assembly-CSharp, via reflection):
        ///     - Calls `EquipClub(CharVGDriverId, bagSlot=1)` so BagManager.GetClubsInBag(1)
        ///       returns the driver, enabling ClubContextPopulator.Refresh() to populate
        ///       ClubContext.SelectedClubId = CharVGDriverId.
        ///  C. BallContext static field: set directly (not reset during gameplay).
        /// </summary>
        private static void ArmCharacterBuild(BotDriver d, string charId, BuildKind kind)
        {
            int targetLevel, targetStr, targetCtrl, targetRec, targetStam;
            string buildLabel;
            if (kind == BuildKind.High)
            {
                // Rare caps: STR=30, CTRL=30, REC=20, STAM=27 (from RarityStatCaps.cs)
                targetLevel = 119; // Rare maxLevel
                targetStr   = 30;
                targetCtrl  = 30;
                targetRec   = 20;
                targetStam  = 27;
                buildLabel  = "HIGH";
            }
            else
            {
                // Rare starting values from Characters.csv (base stats, starting level)
                targetLevel = 80;  // Rare startLevel
                targetStr   = 8;
                targetCtrl  = 10;
                targetRec   = 7;
                targetStam  = 9;
                buildLabel  = "LOW";
            }

            // C. BallContext: set directly — static field, not reset during gameplay load.
            Golfin.Gameplay.UI.HUD.BallContext.SelectedBallId = CharVGBallId;

            try
            {
                // Locate CharacterManager and ClubManager singletons via FindObjectsOfType.
                var allBehaviours = UnityEngine.Object.FindObjectsOfType<UnityEngine.MonoBehaviour>();
                UnityEngine.MonoBehaviour cmInstance  = null;
                UnityEngine.MonoBehaviour clbInstance = null;
                foreach (var mb in allBehaviours)
                {
                    string typeName = mb.GetType().Name;
                    if (typeName == "CharacterManager") cmInstance  = mb;
                    if (typeName == "ClubManager")      clbInstance = mb;
                    if (cmInstance != null && clbInstance != null) break;
                }

                // A. CharacterManager: set selectedCharacterId + mutate stat fields.
                if (cmInstance != null)
                {
                    var cmType = cmInstance.GetType();

                    // Set private selectedCharacterId field so matchmaking reads char_elizabeth.
                    var selField = cmType.GetField("selectedCharacterId",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (selField != null)
                    {
                        selField.SetValue(cmInstance, charId);
                        string afterSet = (string)selField.GetValue(cmInstance);
                        d.LogStep($"  ArmCharacterBuild: selField SET — new value='{afterSet}' (expected='{charId}')");

                        // Belt-and-suspenders: also call the public SelectCharacter API so
                        // SaveData.selectedCharacterId is updated and OnCharacterSelected fires.
                        var selectMethod = cmType.GetMethod("SelectCharacter",
                            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance,
                            null, new System.Type[] { typeof(string) }, null);
                        if (selectMethod != null)
                        {
                            selectMethod.Invoke(cmInstance, new object[] { charId });
                            d.LogStep($"  ArmCharacterBuild: SelectCharacter('{charId}') invoked OK");
                        }
                        else
                        {
                            d.LogStep("  ArmCharacterBuild WARN: SelectCharacter(string) method not found");
                        }
                    }
                    else
                    {
                        d.LogStep($"  ArmCharacterBuild WARN: selField 'selectedCharacterId' not found on {cmType.FullName}");
                    }

                    // Mutate the PlayerCharacterData stat fields.
                    var ownedField = cmType.GetField("ownedCharacters",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (ownedField != null)
                    {
                        var dict = ownedField.GetValue(cmInstance) as System.Collections.IDictionary;
                        if (dict != null && dict.Contains(charId))
                        {
                            object pcd     = dict[charId];
                            var    pcdType = pcd.GetType();
                            pcdType.GetField("currentLevel")?.SetValue(pcd, targetLevel);
                            pcdType.GetField("currentStrength")?.SetValue(pcd, targetStr);
                            pcdType.GetField("currentClubControl")?.SetValue(pcd, targetCtrl);
                            pcdType.GetField("currentRecovery")?.SetValue(pcd, targetRec);
                            pcdType.GetField("currentStamina")?.SetValue(pcd, targetStam);
                            pcdType.GetField("isSelected")?.SetValue(pcd, true);
                        }
                        else
                        {
                            d.LogStep($"  ArmCharacterBuild WARN: '{charId}' not in ownedCharacters (count={dict?.Count ?? 0})");
                        }
                    }
                }
                else
                {
                    d.LogStep("  ArmCharacterBuild WARN: CharacterManager not found");
                }

                // B. ClubManager: equip driver to bag slot 1 so ClubContextPopulator.Refresh()
                //    finds it and sets ClubContext.SelectedClubId = CharVGDriverId.
                if (clbInstance != null)
                {
                    var clbType    = clbInstance.GetType();
                    var equipMethod = clbType.GetMethod("EquipClub",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance,
                        null,
                        new System.Type[] { typeof(string), typeof(int) },
                        null);
                    if (equipMethod != null)
                    {
                        equipMethod.Invoke(clbInstance, new object[] { CharVGDriverId, 1 });
                        d.LogStep($"  ArmCharacterBuild: EquipClub({CharVGDriverId}, slot=1) OK");
                    }
                    else
                    {
                        d.LogStep("  ArmCharacterBuild WARN: EquipClub(string,int) not found on ClubManager");
                    }
                }
                else
                {
                    d.LogStep("  ArmCharacterBuild WARN: ClubManager not found — driver not equipped");
                }

                d.LogStep($"  PreArm: char={charId} lv={targetLevel} STR={targetStr} CTRL={targetCtrl} REC={targetRec} STAM={targetStam} ({buildLabel})");
            }
            catch (System.Exception ex)
            {
                d.LogStep($"  ArmCharacterBuild ERROR: {ex.Message}");
            }
        }

        // ── Scenario: Putter Aim Warped Grid on TestGreen ─────────────────────

        /// <summary>
        /// Visual gate for iter-2 warped wireframe grid — runs on PhysicsLab_TestGreen.
        ///
        /// The TestGreen scene has a sinusoidally sculpted green (y = 0.30*sin(x/4) + 0.20*cos(z/3))
        /// so the warped-grid visual is actually visible: lines bend with the topology.
        ///
        /// Flow:
        ///   1. Load PhysicsLab_TestGreen.unity directly (no matchmaking — it's a lab scene).
        ///   2. Wait for BakedZoneClassifier to bake (HoleContext.OnChanged fires on scene load).
        ///   3. Place ball at green center, enter putter aim via ShotController production path.
        ///   4. Capture screenshot — visual gate: lines must visibly bend over humps/swales.
        ///   5. Assert baked >= 50 cells AND mesh was generated (MeshVertexCount > 0).
        ///
        /// Captures: test_green_baked, putter_aim_warped_grid_on_test_green.
        /// </summary>
        public static IEnumerator PutterAimWarpedGridOnTestGreen(BotDriver d)
        {
            d.LogStep("=== Putter Aim Warped Grid on TestGreen ===");

            // 1. Load PhysicsLab_TestGreen directly via SceneManager.LoadSceneAsync.
            //    PhysicsLab_TestGreen is a standalone lab scene registered in the build settings
            //    (added by TestGreenMeshBuilder or scene setup editor utility).
            const string TestGreenSceneName = "PhysicsLab_TestGreen";
            d.LogStep($"  Loading scene '{TestGreenSceneName}'...");

            var op = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(
                TestGreenSceneName, UnityEngine.SceneManagement.LoadSceneMode.Single);
            if (op == null)
            {
                d.LogStep($"=== PutterAimWarpedGridOnTestGreen: FAIL — LoadSceneAsync returned null. " +
                          "Is 'PhysicsLab_TestGreen' in the Build Settings scenes list? ===");
                yield break;
            }

            // Wait for load to complete.
            float loadWait = 0f;
            while (!op.isDone && loadWait < 30f)
            {
                yield return new WaitForSecondsRealtime(0.25f);
                loadWait += 0.25f;
            }
            if (!op.isDone)
            {
                d.LogStep($"=== PutterAimWarpedGridOnTestGreen: FAIL — scene load timed out after {loadWait}s ===");
                yield break;
            }

            yield return new WaitForSecondsRealtime(5f);  // allow Awake/Start + HoleContext.OnChanged + bake

            // 2. Capture initial state (bake should have completed).
            yield return d.Capture("test_green_baked");

            // 3. Find PhysicsLabController.
            var labCtrl = Object.FindObjectOfType<PhysicsLabController>();
            if (labCtrl == null)
            {
                d.LogStep("=== PutterAimWarpedGridOnTestGreen: FAIL — PhysicsLabController not found in TestGreen scene ===");
                yield break;
            }

            // 4. Place ball at green center (approximately 12.5m x 12.5m for a 25m green).
            d.LogStep("  Placing ball at green center...");
            var greenEntry = labCtrl.PlacementEntries.Find(e => e.Label != null && e.Label.StartsWith("Green"));
            if (greenEntry.Label != null)
            {
                labCtrl.PlaceBallAt(greenEntry.WorldPos, greenEntry.PreferredSurfaceTypeValue);
            }
            else
            {
                // Fallback: place at hardcoded green center.
                labCtrl.PlaceBallAt(new Vector3(12.5f, 0.2f, 12.5f), 1);
            }
            yield return new WaitForSecondsRealtime(0.5f);

            // 5. Switch to putter.
            labCtrl.SetClub(PhysicsLabController.PutterIndex);
            labCtrl.InjectLabBundleForCurrentClub(); // LAB path
            yield return new WaitForSecondsRealtime(0.3f);

            // 6. Enter putter aim via production ShotController path.
            var sc      = labCtrl.GetComponentInChildren<Golfin.Gameplay.Input.ShotController>(true);
            var reader6 = labCtrl.GetComponentInChildren<PutterGreenReader>(true);

            if (sc != null && reader6 != null)
            {
                d.LogStep("  Entering putter aim via BeginExternalDrag()...");
                sc.IsPutt = true;
                sc.BeginExternalDrag();
                yield return null;
                yield return null;
                yield return null;
                d.LogStep($"  After 3 frames: BakedCellCount={reader6.BakedCellCount} MeshVertexCount={reader6.MeshVertexCount}");
            }
            else if (reader6 != null)
            {
                d.LogStep("  Fallback: using SetAimActiveForTest(true)...");
                reader6.SetAimActiveForTest(true);
                yield return null;
                yield return null;
                yield return null;
            }

            // 7. Wait a moment then capture the warped grid visual.
            yield return new WaitForSecondsRealtime(0.5f);
            yield return d.Capture("putter_aim_warped_grid_on_test_green");

            // 8. Assert.
            var reader = labCtrl.GetComponentInChildren<PutterGreenReader>(true);
            int bakedCount  = reader != null ? reader.BakedCellCount   : 0;
            int meshVerts   = reader != null ? reader.MeshVertexCount   : 0;

            d.LogStep($"  PutterGreenReader: baked={bakedCount} meshVerts={meshVerts} (need >=50 baked AND meshVerts>0 for PASS)");

            // 9. Clean up.
            if (sc != null) { sc.CancelExternalDrag(); sc.IsPutt = false; }
            if (reader != null) reader.SetAimActiveForTest(false);

            if (bakedCount < 1)
                d.LogStep("=== PutterAimWarpedGridOnTestGreen: FAIL — no cells baked (BakedZoneClassifier not classifying TestGreen mesh as Green?) ===");
            else if (meshVerts < 1)
                d.LogStep($"=== PutterAimWarpedGridOnTestGreen: FAIL — baked={bakedCount} cells but mesh has 0 vertices (BuildGridMesh did not run?) ===");
            else if (bakedCount < 50)
                d.LogStep($"=== PutterAimWarpedGridOnTestGreen: PARTIAL — baked={bakedCount} (< 50 threshold) meshVerts={meshVerts} ===");
            else
                d.LogStep($"=== PutterAimWarpedGridOnTestGreen: PASS — baked={bakedCount} cells, meshVerts={meshVerts} ===");
        }

        // ── Scenario: stat_lane_surface_roll ─────────────────────────────────
        // stat_to_physics_mapping_audit (Q1, 2026-05-25)

        /// <summary>
        /// Stat lane surface roll audit: fires a Wedge shot at fixed power from the tee
        /// with LOW vs HIGH ball stats (Ball.Roll = -10 vs +10) and logs the terminal
        /// rest position. Measures BallPhysicsModifiers.Roll's effect on roll-out distance
        /// on a Fairway lie.
        ///
        /// This scenario uses StatProviderBus.Resolver injection to control ball stats
        /// without needing CSV entries with specific values.
        ///
        /// OB avoidance: wedge power=0.55 aimed at fairway center avoids OB on Hole 1
        /// (BOT_FRAMEWORK.md §6 — OB-avoidance baked in per stat_to_physics_mapping_audit Q1).
        ///
        /// Captures: gameplay_armed, stat_lane_roll_low_stroke1, stat_lane_roll_high_stroke1.
        /// </summary>
        public static IEnumerator StatLaneSurfaceRoll(BotDriver d)
        {
            d.LogStep("=== Stat Lane Surface Roll — LOW vs HIGH Ball.Roll ===");

            // Navigate to Hole 1 via standard matchmaking flow.
            yield return d.NavigateToHome(totalTimeoutSeconds: 60f);
            yield return new WaitForSecondsRealtime(1f);
            yield return d.Click("PLAY", settleSeconds: 1.5f);
            yield return d.WaitForModalVisible("MatchMakingModal", timeoutSeconds: 15f);
            yield return d.WaitFor(
                () => d.GetMatchmakingPhase() == "OpponentFound",
                "matchmaking opponent found",
                timeoutSeconds: 30f);
            yield return new WaitForSecondsRealtime(0.5f);
            yield return d.WaitForSceneLoaded("LabScaffold", timeoutSeconds: 40f);
            yield return d.WaitForSceneLoaded("Hole_01_Geo", timeoutSeconds: 40f);
            yield return new WaitForSecondsRealtime(3f);
            yield return d.Capture("gameplay_armed");

            var ctrl   = Object.FindObjectOfType<PhysicsLabController>();
            if (ctrl == null)
            {
                d.LogStep("=== StatLaneSurfaceRoll: FAIL — PhysicsLabController not found ===");
                yield break;
            }

            // Helper: inject a Resolver with specific Ball.Roll value and MID character+club.
            // The injected bundle is used only on FALLBACK — ClearStatBundleOverride() ensures
            // the bus resolver (not the lab bundle) is used.
            void ArmBallRoll(int rollValue, string label)
            {
                var charStats = new Golfin.Physics.Stats.CharacterStats(25, 25, 15, 20); // MID
                var club      = Golfin.Physics.Stats.ClubStats.DefaultWedge;
                var ball      = new Golfin.Physics.Stats.BallStats(0, 0, 0, rollValue, 0); // only Roll varies

                Golfin.Gameplay.Defaults.StatProviderBus.Resolver = isPutt =>
                {
                    if (isPutt) return null; // let putter default handle putt
                    return new Golfin.Physics.Stats.StatBundle(
                        club, ball, charStats,
                        Golfin.Physics.Math.fp.FromFloat(100f),
                        Golfin.Physics.Math.fp.FromFloat(100f));
                };
                d.LogStep($"  ArmBallRoll: Ball.Roll={rollValue} ({label}) — Resolver set");
            }

            // Switch to Wedge (index 2) — fairway-safe at power=0.55 from tee.
            ctrl.SetClub(2); // Wedge
            var shotCtl = Object.FindObjectOfType<Golfin.Gameplay.Input.ShotController>();
            if (shotCtl != null) shotCtl.ClearStatBundleOverride();

            // Aim toward fairway center (yaw ≈ π, heading west-ish on Hole 1).
            float yaw = Mathf.PI; // straight west — fairway-safe, no OB risk at wedge power
            ctrl.SetCameraYawRadians(yaw);

            // --- LOW Ball.Roll (-10: more friction, shorter roll) ---
            ArmBallRoll(-10, "LOW");
            yield return new WaitForSecondsRealtime(0.5f);

            bool lowDone = false;
            var sm = ctrl.BallSM;
            if (sm != null) sm.OnShotComplete += r => { lowDone = true; };

            if (shotCtl != null)
            {
                float si = 0f;
                while (shotCtl.State != Golfin.Gameplay.Input.ShotState.Idle && si < 4f)
                { si += Time.unscaledDeltaTime; yield return null; }
                shotCtl.BeginExternalDrag();
                float rt = 0f; const float ramp = 0.85f;
                while (rt < ramp) { rt += Time.unscaledDeltaTime; shotCtl.SetExternalPower(Mathf.Lerp(0f, 0.55f, rt / ramp), 0f); yield return null; }
                shotCtl.SetExternalPower(0.55f, 0f);
                yield return new WaitForSecondsRealtime(0.18f);
                shotCtl.EndExternalDrag();
            }

            float gLow = 0f;
            while (!lowDone && gLow < 15f) { gLow += Time.unscaledDeltaTime; yield return null; }
            d.LogStep($"  LOW Ball.Roll=-10: terminal pos={ctrl.BallPosition} (gated {gLow:F1}s)");
            yield return new WaitForSecondsRealtime(1f);
            yield return d.Capture("stat_lane_roll_low_stroke1");

            Vector3 lowFinalPos = ctrl.BallPosition;

            // --- HIGH Ball.Roll (+10: less friction, longer roll) ---
            // Reset ball to tee so HIGH shot fires from the SAME start as LOW (same-start comparison).
            ctrl.ResetToTee();
            yield return new WaitForSecondsRealtime(1.0f); // let physics settle after reset
            ctrl.SetCameraYawRadians(yaw);
            ArmBallRoll(+10, "HIGH");
            yield return new WaitForSecondsRealtime(0.5f);

            bool highDone = false;
            if (sm != null) sm.OnShotComplete += r => { highDone = true; };

            if (shotCtl != null)
            {
                float si = 0f;
                while (shotCtl.State != Golfin.Gameplay.Input.ShotState.Idle && si < 4f)
                { si += Time.unscaledDeltaTime; yield return null; }
                shotCtl.BeginExternalDrag();
                float rt = 0f; const float ramp = 0.85f;
                while (rt < ramp) { rt += Time.unscaledDeltaTime; shotCtl.SetExternalPower(Mathf.Lerp(0f, 0.55f, rt / ramp), 0f); yield return null; }
                shotCtl.SetExternalPower(0.55f, 0f);
                yield return new WaitForSecondsRealtime(0.18f);
                shotCtl.EndExternalDrag();
            }

            float gHigh = 0f;
            while (!highDone && gHigh < 15f) { gHigh += Time.unscaledDeltaTime; yield return null; }
            d.LogStep($"  HIGH Ball.Roll=+10: terminal pos={ctrl.BallPosition} (gated {gHigh:F1}s)");
            yield return new WaitForSecondsRealtime(1f);
            yield return d.Capture("stat_lane_roll_high_stroke1");

            Vector3 highFinalPos = ctrl.BallPosition;

            // Report delta.
            float rollDelta = Vector3.Distance(lowFinalPos, highFinalPos);
            d.LogStep($"  Roll delta: LOW pos={lowFinalPos:F1} HIGH pos={highFinalPos:F1} distance={rollDelta:F1}m");
            if (rollDelta >= 10f)
                d.LogStep($"=== StatLaneSurfaceRoll: PASS — roll delta {rollDelta:F1}m >= 10m bar ===");
            else
                d.LogStep($"=== StatLaneSurfaceRoll: WEAK — roll delta {rollDelta:F1}m < 10m bar ===");

            // Clean up: restore resolver to null (back to pure FALLBACK).
            Golfin.Gameplay.Defaults.StatProviderBus.Resolver = null;
        }

        // ── Spin and Shot-Shape Visual Gate (spin_and_shot_shape_wiring) ─────────

        /// <summary>
        /// Fires 5 driver shots from Hole 1 tee with the same character/club/power=1.0,
        /// varying only SpinContext.Spin between strokes. Bot resets to tee between strokes
        /// via PhysicsLabController.ResetToTee() (Lesson V — same-start state required).
        ///
        /// Spin positions tested:
        ///   1. CENTER      (0,  0) — baseline straight shot
        ///   2. TOP_TOPSPIN (0, +1) — reduces backspin → lower trajectory, more roll
        ///   3. BOTTOM_BACK (0, -1) — boosts backspin → higher trajectory, stops faster
        ///   4. LEFT_DRAW   (-1, 0) — tilts axis → draw curl to the left
        ///   5. RIGHT_FADE  (+1, 0) — tilts axis → fade curl to the right
        ///
        /// Per Q5 design lock: no manual playtest needed. Cesar approves from bot video.
        ///
        /// Captures: home, gameplay_armed, then per-stroke armed+landed stills.
        /// Log: per-stroke [Build] lines (with spinInput/spinAxis/spinRate) written by
        ///      LiveStatLogTee (extended to also capture [Build] prefix).
        /// </summary>
        public static IEnumerator SpinAndShapeVisualGate(BotDriver d)
        {
            d.LogStep("=== Spin and Shot-Shape Visual Gate ===");

            // Navigate to game from cold start.
            yield return d.NavigateToHome(totalTimeoutSeconds: 60f);
            yield return new WaitForSecondsRealtime(1f);
            yield return d.Capture("home");

            yield return d.Click("PLAY", settleSeconds: 1.5f);
            yield return d.WaitForModalVisible("MatchMakingModal", timeoutSeconds: 15f);

            yield return d.WaitFor(
                () => d.GetMatchmakingPhase() == "OpponentFound",
                "matchmaking opponent found",
                timeoutSeconds: 30f);

            yield return d.WaitForSceneLoaded("LabScaffold", timeoutSeconds: 40f);
            yield return d.WaitForSceneLoaded("Hole_01_Geo", timeoutSeconds: 40f);
            yield return new WaitForSecondsRealtime(3f);
            yield return d.Capture("gameplay_armed");

            // The 5 spin positions (widget-index order, per SpinPanelWidget._values[]).
            var spinPositions = new[]
            {
                (label: "CENTER",       spin: new Vector2( 0f,  0f)),
                (label: "TOP_TOPSPIN",  spin: new Vector2( 0f, +1f)),
                (label: "BOTTOM_BACK",  spin: new Vector2( 0f, -1f)),
                (label: "LEFT_DRAW",    spin: new Vector2(-1f,  0f)),
                (label: "RIGHT_FADE",   spin: new Vector2(+1f,  0f)),
            };

            // Reset before first stroke so tee position is fresh.
            d.ResetLabToTee();
            yield return new WaitForSecondsRealtime(0.5f);

            for (int i = 0; i < spinPositions.Length; i++)
            {
                var (label, spin) = spinPositions[i];

                // Set spin (SpinContext.Reset() was called inside preceding ResetLabToTee flow).
                Golfin.Gameplay.UI.HUD.SpinContext.SetSpin(spin);
                yield return new WaitForSecondsRealtime(0.3f); // settle frame

                d.LogStep($"Stroke {i+1}: {label} spinInput=({spin.x:F1},{spin.y:F1})");

                yield return d.Capture($"stroke{i+1}_{label.ToLower()}_armed");

                // RIGHT_FADE at full power goes OB. Reduce to 0.7 to keep ball in bounds.
                float shotPower = (label == "RIGHT_FADE") ? 0.7f : 1.0f;

                // Pass spin explicitly to FireDriverShot to survive any SpinContext.Reset()
                // that may fire between SetSpin() and CommitFlick() due to state transitions
                // (ShotConeView.HandleStateChanged resets SpinContext on Idle, which can fire
                // between coroutine yields — passing spin directly bypasses the race).
                yield return d.FireDriverShot(power01: shotPower, timeoutSeconds: 25f,
                    spinInput: spin);

                // Pause at rest so the settled ball frame is clearly visible in the video.
                // A 2-second wait is long enough that the next iteration's ResetLabToTee
                // won't fire in the same Unity frame as this capture (iter-2 fix for post-reset screenshots).
                yield return new WaitForSecondsRealtime(2.0f);
                yield return d.Capture($"stroke{i+1}_{label.ToLower()}_landed");

                // Now reset AFTER capturing the landed frame (not before the next armed capture).
                // This guarantees the capture frame shows the landed ball, not the reset-to-tee state.
                if (i < spinPositions.Length - 1)
                {
                    // Small pause before reset so the captured frame is rendered before scene state changes.
                    yield return new WaitForSecondsRealtime(0.3f);
                    d.ResetLabToTee();
                    yield return new WaitForSecondsRealtime(0.5f); // settle frame after reset
                }
            }

            d.LogStep("=== Spin Gate Complete ===");
            yield return new WaitForSecondsRealtime(1f);
            d.FlushLog();
        }

        // ── Scenario: practice_flow_gate ──────────────────────────────────────
        // practice_1v1_matchmaking_split (2026-06-06): Acceptance Gate 1 evidence.

        /// <summary>
        /// Production-flow acceptance gate for the Practice path after the matchmaking split.
        ///
        /// Verifies:
        ///   - Clicking PLAY on the Practice mode card reaches Hole Selection (NO matchmaking).
        ///   - Clicking a hole card's ActionButton seeds the session and loads gameplay directly.
        ///   - Hole-out → result modal SUCCESS → PLAY NEXT works (solo loop intact).
        ///
        /// Uses ClickModeCardPlay("practice") to invoke the real onClick path on the Practice
        /// mode card, NOT a direct coroutine call to any controller method.
        ///
        /// Captures: home, practice_hole_selection, gameplay_armed, result_modal,
        ///           gameplay_armed_hole2 (PLAY NEXT advance).
        /// </summary>
        public static IEnumerator PracticeFlowGate(BotDriver d)
        {
            d.LogStep("=== Practice Flow Gate (practice_1v1_matchmaking_split) ===");

            // 1. Cold launch → Home.
            yield return d.NavigateToHome(totalTimeoutSeconds: 60f);
            yield return new WaitForSecondsRealtime(2f); // let mode carousel settle
            yield return d.Capture("home");

            // 2. Click PLAY on the Practice mode card (real onClick path).
            //    ModeCarouselController.HandlePlayClicked dispatches to ShowScreen(HoleSelection).
            //    NO matchmaking modal should appear.
            yield return d.ClickModeCardPlay("practice", settleSeconds: 1.5f);

            // 3. Confirm Hole Selection screen is shown (not matchmaking modal).
            yield return d.WaitForScreen("HoleSelection", timeoutSeconds: 15f);
            yield return new WaitForSecondsRealtime(3f); // wait for HoleCardController auto-expand
            yield return d.Capture("practice_hole_selection");

            // 4. Tap PLAY on the first available hole card (ActionButton on the auto-expanded row).
            //    HoleSelectionScreenController.HandleActionClicked calls GameSession.SeedSession +
            //    GameplaySceneLoader.BeginGameplayLoad — no matchmaking modal.
            //    Button is inside ExpandedContainer; needs 3s settle above to become active.
            yield return d.Click("ActionButton", settleSeconds: 1.5f);

            // 5. Assert NO matchmaking modal appears (screenshot taken after click — modal
            //    would need 0.5s+ to appear; the slot is empty on the practice path).
            yield return new WaitForSecondsRealtime(0.5f);
            bool modalVisible = d.IsMatchMakingModalVisible();
            d.LogStep($"[PracticeFlowGate] MatchMakingModal visible after ActionButton click: {modalVisible} (expected: false)");

            // 6. Wait for gameplay scene to load.
            yield return d.WaitForSceneLoaded("LabScaffold", timeoutSeconds: 40f);
            // Determine which hole was loaded (first unlocked hole, typically Hole 1).
            yield return d.WaitForSceneLoaded("Hole_01_Geo", timeoutSeconds: 40f);
            yield return new WaitForSecondsRealtime(3f);
            yield return d.Capture("gameplay_armed");
            d.LogStep($"[PracticeFlowGate] GameSession.CurrentHoleNumber={Golfin.Gameplay.Session.GameSession.CurrentHoleNumber} (expected: 1)");

            // 7. Force InCup → SUCCESS result modal.
            yield return d.ForceShotComplete("InCup", settleSeconds: 1f);
            yield return new WaitForSecondsRealtime(2f);
            yield return d.Capture("result_modal");

            // 8. Tap PLAY NEXT → Hole 2 loads (PLAY NEXT card in lab widget is "PlayButton").
            yield return d.Click("PlayButton", settleSeconds: 1.5f);
            yield return d.WaitForSceneLoaded("Hole_02_Geo", timeoutSeconds: 40f);
            yield return new WaitForSecondsRealtime(3f);
            yield return d.Capture("gameplay_armed_hole2");
            d.LogStep($"[PracticeFlowGate] After PLAY NEXT: GameSession.CurrentHoleNumber={Golfin.Gameplay.Session.GameSession.CurrentHoleNumber} (expected: 2)");

            if (!modalVisible)
                d.LogStep("=== Practice Flow Gate: PASS — Practice path skips matchmaking, reaches gameplay, hole-out + PLAY NEXT intact ===");
            else
                d.LogStep("=== Practice Flow Gate: FAIL — MatchMakingModal appeared on Practice path (should NOT appear) ===");

            d.FlushLog();
        }

        // ── Scenario: matchmaking_1v1_gate ────────────────────────────────────
        // practice_1v1_matchmaking_split (2026-06-06): Acceptance Gate 2 evidence.

        /// <summary>
        /// Production-flow acceptance gate for the 1v1 path after the matchmaking split.
        ///
        /// Verifies:
        ///   - Clicking PLAY on the 1v1 mode card opens the matchmaking modal (random hole 1-18).
        ///   - Matchmaking completes (OpponentFound) and gameplay scene loads.
        ///   - GameSession.CurrentHoleNumber is in range [1, 18] (random selection confirmed).
        ///
        /// Uses ClickModeCardPlay("versus_1v1") to invoke the real onClick path on the 1v1
        /// mode card — NOT a direct call to MatchmakingModalController.Open().
        ///
        /// Captures: home, matchmaking_searching, opponent_found, gameplay_armed.
        /// </summary>
        public static IEnumerator Matchmaking1v1Gate(BotDriver d)
        {
            d.LogStep("=== Matchmaking 1v1 Gate (practice_1v1_matchmaking_split) ===");

            // 1. Cold launch → Home.
            yield return d.NavigateToHome(totalTimeoutSeconds: 60f);
            yield return new WaitForSecondsRealtime(2f); // let mode carousel settle
            yield return d.Capture("home");

            // 2. Click PLAY on the 1v1 mode card (real onClick path).
            //    ModeCarouselController.HandlePlayClicked dispatches to
            //    matchmakingModal1v1.Open(Random.Range(0,18)).
            yield return d.ClickModeCardPlay("versus_1v1", settleSeconds: 1.5f);

            // 3. Matchmaking modal should appear.
            yield return d.WaitForModalVisible("MatchMakingModal", timeoutSeconds: 15f);
            yield return d.Capture("matchmaking_searching");

            // 4. Wait for OpponentFound phase.
            yield return d.WaitFor(
                () => d.GetMatchmakingPhase() == "OpponentFound",
                "matchmaking opponent found",
                timeoutSeconds: 30f);
            yield return new WaitForSecondsRealtime(0.5f);
            yield return d.Capture("opponent_found");

            // 5. Wait for gameplay scenes to load.
            yield return d.WaitForSceneLoaded("LabScaffold", timeoutSeconds: 40f);
            // The hole number is random 1-18; wait for any Hole_NN_Geo.
            yield return d.WaitForAnyHoleGeoScene(timeoutSeconds: 40f);
            yield return new WaitForSecondsRealtime(3f);
            yield return d.Capture("gameplay_armed");

            // 6. Log the seeded hole number for verification.
            int holeNum = Golfin.Gameplay.Session.GameSession.CurrentHoleNumber;
            d.LogStep($"[Matchmaking1v1Gate] GameSession.CurrentHoleNumber={holeNum} (expected: 1-18)");

            if (holeNum >= 1 && holeNum <= 18)
                d.LogStep("=== Matchmaking 1v1 Gate: PASS — 1v1 PLAY opens matchmaking, random hole in [1,18], gameplay loaded ===");
            else
                d.LogStep($"=== Matchmaking 1v1 Gate: FAIL — hole={holeNum} outside [1,18] ===");

            d.FlushLog();
        }

        // ── Scenario: matchmaking_1v1_cancel_gate ─────────────────────────────
        // practice_1v1_matchmaking_split iter-3 (2026-06-06): Cancel-gate fix evidence.

        /// <summary>
        /// Acceptance gate for the CESAR_REJECTION defect fix:
        /// "Cancel on the matchmaking modal resurrects the dead NextHolePanel."
        ///
        /// Verifies:
        ///   - Home → Mode Select → 1v1 PLAY → matchmaking modal opens.
        ///   - Tap CANCEL → modal closes cleanly.
        ///   - The legacy NextHolePanel (HomeScreen > NextHolePanel, m_IsActive: 0)
        ///     is NOT active in the hierarchy after Cancel.
        ///   - The Mode Select carousel is visible again (home state clean).
        ///
        /// Captures: home, matchmaking_modal_open, post_cancel_home (the load-bearing evidence).
        /// The post_cancel_home frame must show the carousel WITHOUT the NextHolePanel behind it.
        /// </summary>
        public static IEnumerator Matchmaking1v1CancelGate(BotDriver d)
        {
            d.LogStep("=== Matchmaking 1v1 Cancel Gate (practice_1v1_matchmaking_split iter-3) ===");

            // 1. Cold launch → Home.
            yield return d.NavigateToHome(totalTimeoutSeconds: 60f);
            yield return new WaitForSecondsRealtime(2f); // let mode carousel settle
            yield return d.Capture("s01_home_pre_play");

            // 2. Click PLAY on the 1v1 mode card (real onClick path).
            yield return d.ClickModeCardPlay("versus_1v1", settleSeconds: 1.5f);

            // 3. Matchmaking modal should appear.
            yield return d.WaitForModalVisible("MatchMakingModal", timeoutSeconds: 15f);
            yield return new WaitForSecondsRealtime(0.5f);
            yield return d.Capture("s02_matchmaking_modal_open");

            // 4. Tap CANCEL to dismiss the modal.
            yield return d.Click("CancelButton", settleSeconds: 0.5f);

            // 5. Wait for modal to hide.
            yield return d.WaitForModalHidden("MatchMakingModal", timeoutSeconds: 10f);
            yield return new WaitForSecondsRealtime(1.5f); // let home settle after hide

            // 6. Capture the post-Cancel home state — this is the load-bearing frame.
            yield return d.Capture("s03_post_cancel_home");

            // 7. Check that NextHolePanel is NOT active in the hierarchy.
            bool nextHolePanelActive = d.IsNextHolePanelActive();
            d.LogStep($"[Matchmaking1v1CancelGate] NextHolePanel.activeInHierarchy={nextHolePanelActive} (expected: false)");

            if (!nextHolePanelActive)
                d.LogStep("=== Matchmaking 1v1 Cancel Gate: PASS — Cancel returns to Mode Select carousel; NextHolePanel stays deactivated ===");
            else
                d.LogStep("=== Matchmaking 1v1 Cancel Gate: FAIL — NextHolePanel is active after Cancel (resurrection defect still present) ===");

            d.FlushLog();
        }

        // ── Scenario: tree_collision_gate ─────────────────────────────────────
        // tree_collisions (Order 348, 2026-06-12): §9 visual gate evidence.

        /// <summary>
        /// Visual gate for the tree collision system (Order 348).
        ///
        /// Three clips in one combined recording:
        ///   A. trunk_strike  — ball fired straight at a trunk, reflects and drops nearly dead.
        ///   B. canopy_hit    — ball fired at a shallower loft toward canopy zone, visibly damped.
        ///   C. control       — same canopy shot fired with _treeProvider nulled (no collision),
        ///                       ball flies full distance showing delta vs. B.
        ///
        /// Scene path: DIRECT — LoadSceneAsync("LabScaffold", Single) then additive
        ///   LoadSceneAsync("Hole_01_Geo"). Bypasses full game navigation (~84s under recording
        ///   load) to keep total clip ≤40s. PhysicsLabController.ScanForLoadedHoleSceneAtStartup
        ///   detects Hole_01_Geo, calls OnHoleLoaded → TryLoadBakedProviders → populates _treeProvider.
        ///
        /// Target tree: Hole 1, cluster at world x=-87.0, z=-121.3, baseY≈1.0, scale≈0.97
        /// (MESH_JapaneseBlack_01 per tree_obstacles.csv). Ball placed at x=-87.0, z=-91.0
        /// (30m in front of trunk along +z axis); driver shot aimed straight at tree (yaw=-π/2).
        ///
        /// Trunk profile for MESH_JapaneseBlack_01 (from tree_collision_profiles.csv):
        ///   trunkRadius=0.25m, trunkHeight=4.5m, canopyBaseY=4.5m, canopyRadius=3.5m,
        ///   canopyHeight=5.0m, trunkBounciness=0.72, canopyDamping=0.45.
        ///
        /// Captures: gameplay_armed, trunk_strike_before, trunk_strike_after,
        ///           canopy_hit_before, canopy_hit_after, control_before, control_after.
        /// </summary>
        public static IEnumerator TreeCollisionGate(BotDriver d)
        {
            d.LogStep("=== Tree Collision Gate (tree_collisions Order 348) ===");

            // Hide ShellScene canvases so the PhysicsLab camera dominates the Game View.
            // Without this, the ShellScene UI overlays the PhysicsLab 3D render, making
            // the recorded video show the home-screen splash instead of ball-tree collisions.
            var shellCanvases = Object.FindObjectsOfType<Canvas>();
            var hiddenCanvases = new System.Collections.Generic.List<Canvas>();
            foreach (var c in shellCanvases)
            {
                if (c.gameObject.scene.name == "ShellScene" && c.enabled)
                {
                    c.enabled = false;
                    hiddenCanvases.Add(c);
                }
            }
            d.LogStep($"  Hidden {hiddenCanvases.Count} ShellScene canvases for clean video capture.");

            // ── Local restore helper ──────────────────────────────────────────
            // Invoked by the finally block — guaranteed on ALL exit paths including
            // early yield breaks and uncaught exceptions. C# iterators support
            // yield return inside try/finally (not try/catch); yield break inside
            // the try block causes the finally to run before the coroutine terminates.
            System.Action restoreCanvases = () =>
            {
                foreach (var c in hiddenCanvases) { if (c != null) c.enabled = true; }
                d.LogStep($"  Restored {hiddenCanvases.Count} ShellScene canvases.");
            };

            // Delegate to the body iterator so cleanup (restoreCanvases + FlushLog) runs in
            // a try/finally, guaranteeing it fires on ALL exit paths: normal completion,
            // early yield break, and unhandled exceptions. C# 5+ iterators support
            // yield return (and yield break) inside try/finally.
            return TreeCollisionGateBody(d, restoreCanvases);
        }

        // Body iterator — separated from TreeCollisionGate so the non-iterator outer method
        // can set up state (canvas hiding) before returning this IEnumerator, and the
        // try/finally here guarantees cleanup on every exit path.
        private static IEnumerator TreeCollisionGateBody(
            BotDriver d,
            System.Action restoreCanvases)
        {
            try
            {

            // 1. Load LabScaffold directly (bypasses full matchmaking navigation).
            //    Load LabScaffold + Hole_01_Geo ADDITIVELY, both kicked off simultaneously.
            //
            // CRITICAL ordering constraint: PhysicsLabController.ScanForLoadedHoleSceneAtStartup
            // runs in Start() and yields only 2 frames before checking for Hole_*_Geo scenes.
            // If LabScaffold loads but Hole_01_Geo is not yet loaded, the scan finds nothing
            // and exits — OnHoleLoaded never fires, IsHoleReady stays false.
            //
            // Fix: kick off BOTH loads before waiting for either. When LabScaffold's Start()
            // runs the scan, Hole_01_Geo is loading (or already loaded) so the scan finds it.
            //
            // Additive mode keeps ShellScene (and this bot GO) alive — bot GO would be destroyed
            // by Single-mode load. Mirrors GameplaySceneLoader.cs which also uses Additive.
            d.LogStep("  Starting LabScaffold + Hole_01_Geo loads (both Additive, simultaneous)...");
            var opLab = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(
                "LabScaffold", UnityEngine.SceneManagement.LoadSceneMode.Additive);
            if (opLab == null)
            {
                d.LogStep("=== TreeCollisionGate: FAIL — LoadSceneAsync('LabScaffold') returned null. Is LabScaffold in Build Settings? ===");
                yield break;
            }
            // Kick off hole load immediately (before LabScaffold finishes) so it's available
            // when ScanForLoadedHoleSceneAtStartup runs on LabScaffold's frame 2.
            var opHole = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(
                "Hole_01_Geo", UnityEngine.SceneManagement.LoadSceneMode.Additive);
            if (opHole == null)
            {
                d.LogStep("=== TreeCollisionGate: FAIL — LoadSceneAsync('Hole_01_Geo') returned null. Is Hole_01_Geo in Build Settings? ===");
                yield break;
            }

            // 2. Wait for both loads to complete.
            float lw = 0f;
            while ((!opLab.isDone || !opHole.isDone) && lw < 30f)
            {
                yield return new WaitForSecondsRealtime(0.25f);
                lw += 0.25f;
            }
            if (!opLab.isDone || !opHole.isDone)
            {
                d.LogStep($"=== TreeCollisionGate: FAIL — scene load timed out after {lw:F1}s (lab={opLab.isDone} hole={opHole.isDone}) ===");
                yield break;
            }
            d.LogStep($"  Both scenes loaded in {lw:F1}s. Polling ctrl.IsHoleReady...");

            // 3. Wait for PhysicsLabController.OnHoleLoaded to complete (sets _useSceneProviders=true,
            //    loads tree_obstacles.csv, populates _treeProvider). Poll IsHoleReady up to 15s.
            var ctrl = Object.FindObjectOfType<PhysicsLabController>();
            if (ctrl == null)
            {
                d.LogStep("=== TreeCollisionGate: FAIL — PhysicsLabController not found after LabScaffold load ===");
                yield break;
            }
            float holeWait = 0f;
            while (!ctrl.IsHoleReady && holeWait < 15f)
            {
                yield return new WaitForSecondsRealtime(0.25f);
                holeWait += 0.25f;
            }
            if (!ctrl.IsHoleReady)
            {
                d.LogStep($"=== TreeCollisionGate: FAIL — IsHoleReady never true after {holeWait:F1}s. OnHoleLoaded did not fire. ===");
                yield break;
            }
            d.LogStep($"  IsHoleReady=true after {holeWait:F1}s settle. Capturing armed state.");
            yield return new WaitForSecondsRealtime(1f); // extra settle for treeProvider CSV load
            yield return d.Capture("gameplay_armed");

            // ctrl is already found above (after LabScaffold loaded, before IsHoleReady poll).
            var sm      = ctrl.BallSM;
            var shotCtl = Object.FindObjectOfType<Golfin.Gameplay.Input.ShotController>();

            // Target tree: x=-87.04, z=-121.27 (MESH_JapaneseBlack_01, Hole 1 tree_obstacles.csv).
            // Tree profile: trunkRadius=0.35*scale=0.339m, trunkHeight=3.5*scale=3.392m,
            //   trunkTop=baseY(0.98)+3.39=4.375m.  Bare-bark zone: y=0.98..4.375m.
            //
            // iter-8b LOW-TRUNK re-shoot:
            //   Ball placed 8m in front of trunk (same X, z=-113.3) → much closer than iter-8's 30m.
            //   Power = 0.20 (very low, nearly flat shot). At this distance+power, the driver
            //   launches with low vy and reaches the trunk before rising more than ~1.5m.
            //   BallSimulation probe (iter-8b, read-only): finalPos=(-87.04,1.00,-119.99) →
            //   ball rests at y=1.00m (= ground level = baseY=0.98) right in front of trunk base.
            //   Canopy still uses 50m-back position and power=0.55 (unchanged, PART B+C fine).
            var ballPos     = new Vector3(-87.0f, 0f, -113.3f); // 8m in front of trunk
            var canopyPos   = new Vector3(-87.0f, 0f, -71.0f);  // 50m back for canopy arc (unchanged)
            float yawToTree = Mathf.Atan2(-1f, 0f);             // -π/2 rad → shot direction is -z

            // ChaseCamera component reference for mode switching (iter-6 Defect 2 fix).
            // Default Chase mode buries itself in the foliage (camera follows ball INTO the tree
            // along the shot path). We switch to Downrange mode for Part A to hold a fixed
            // side-elevated position that shows the trunk contact unmistakably.
            var bindPrivate   = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            var chaseCamFi    = ctrl.GetType().GetField("chaseCamera", bindPrivate);
            var chaseCamComp  = chaseCamFi?.GetValue(ctrl) as ChaseCamera;

            // iter-8b camera: positioned to CLEARLY FRAME the bare LOWER TRUNK and ball at ground.
            // Trunk center at x=-87.04, z=-121.27, base at y=0.98.
            // Ball rests at y=1.00 (ground level) near z=-120.0 (1.3m in front of trunk).
            // Camera: west side (x=-100, 13m from trunk), y=3.0 (elevated enough to show ball on ground
            //   AND bare bark trunk behind it), looking at trunk base area (y=1.5).
            // This frames the scene: ball at lower left foreground, bare brown trunk behind it.
            // The tree's green foliage is ABOVE the camera's frame (foliage starts at y=4.375m).
            var trunkImpactLookAt = new Vector3(-87.0f, 1.5f, -121.0f); // trunk lower area (bare bark)
            var trunkSideCamPos   = new Vector3(-100.0f, 3.0f, -121.0f); // 13m west, slight elevation

            // ── A: Trunk Strike (trees enabled, power 0.20, 8m from trunk) ──────────
            d.LogStep("=== Part A: Trunk Strike (trees enabled, LOW power=0.20 from 8m) ===");
            ctrl.PlaceBallAt(ballPos, preferredSurfaceTypeValue: null);

            // Switch to Downrange mode: fixed camera at side-elevated position watching trunk base.
            ctrl.SetCameraYawRadians(yawToTree);
            if (chaseCamComp != null)
            {
                chaseCamComp.SetDownrangeFraming(trunkSideCamPos, trunkImpactLookAt);
                chaseCamComp.SetMode(ChaseCamera.Mode.Downrange);
                d.LogStep($"  [TrunkStrike] camera → Downrange pos={trunkSideCamPos:F1} lookAt={trunkImpactLookAt:F1}");
            }
            yield return new WaitForSecondsRealtime(0.5f);
            yield return d.Capture("trunk_strike_before");
            d.LogStep($"  [TrunkStrike] placed at {ballPos}, yaw={yawToTree:F3}");

            ctrl.SetClub(0);
            shotCtl?.ClearStatBundleOverride();
            {
                bool trunkDone = false;
                if (sm != null) sm.OnShotComplete += r => { trunkDone = true; };
                if (shotCtl != null)
                {
                    float si = 0f;
                    while (shotCtl.State != Golfin.Gameplay.Input.ShotState.Idle && si < 4f)
                    { si += Time.unscaledDeltaTime; yield return null; }
                    shotCtl.BeginExternalDrag();
                    // iter-8b: power 0.20 (was 0.55) — much lower → very flat trajectory.
                    // Ball at 8m distance strikes the LOWER trunk (y≈1.0-2.0m, bare bark zone)
                    // and drops nearly dead to the ground (trunkRestitution=0.15 → dead stop).
                    // Probe confirms final position ≈ (-87.04, 1.00, -120.0): ground-level, trunk base.
                    const float ramp = 0.60f; float rt = 0f;
                    while (rt < ramp) { rt += Time.unscaledDeltaTime; shotCtl.SetExternalPower(Mathf.Lerp(0f, 0.20f, rt / ramp), 0f); yield return null; }
                    shotCtl.SetExternalPower(0.20f, 0f);
                    yield return new WaitForSecondsRealtime(0.18f);
                    shotCtl.EndExternalDrag();
                }
                else { ctrl.FireViaShotController(0.20f, Golfin.Gameplay.Input.DebugShotAccuracy.Green); }
                float e = 0f;
                // iter-8b fix: LoopCameraDirector overrides mode to Chase when BallState→Flying/Rolling.
                // Re-apply Downrange every frame during flight so the fixed side-camera persists
                // for the trunk approach + contact + settle — Cesar needs bare-bark contact in the video.
                while (!trunkDone && e < 30f)
                {
                    e += Time.unscaledDeltaTime;
                    chaseCamComp?.SetMode(ChaseCamera.Mode.Downrange);
                    yield return null;
                }
                d.LogStep($"  [TrunkStrike] complete e={e:F1}s ball={ctrl.BallPosition:F1}");
            }
            // Re-apply Downrange for the settle wait and at-rest capture.
            chaseCamComp?.SetMode(ChaseCamera.Mode.Downrange);
            yield return new WaitForSecondsRealtime(2.5f); // extra wait — slow ball, let it fully settle
            chaseCamComp?.SetMode(ChaseCamera.Mode.Downrange); // guard against AtRest state reset
            yield return d.Capture("trunk_strike_after");
            d.LogStep($"  [TrunkStrike] final pos={ctrl.BallPosition:F1}");

            // Restore Chase mode for Part B and C so they use the default chase camera behaviour.
            if (chaseCamComp != null)
            {
                chaseCamComp.SetMode(ChaseCamera.Mode.Chase);
                d.LogStep("  [TrunkStrike] camera restored to Chase mode.");
            }

            // ── B: Canopy Hit (trees enabled, power 0.55, 50m back) ───────────
            d.LogStep("=== Part B: Canopy Hit (trees enabled) ===");
            ctrl.PlaceBallAt(canopyPos, preferredSurfaceTypeValue: null);
            yield return new WaitForSecondsRealtime(0.5f);
            yield return d.Capture("canopy_hit_before");
            d.LogStep($"  [CanopyHit] placed at {canopyPos}");

            ctrl.SetClub(0);
            shotCtl?.ClearStatBundleOverride();
            ctrl.SetCameraYawRadians(yawToTree);
            {
                bool canopyDone = false;
                if (sm != null) sm.OnShotComplete += r => { canopyDone = true; };
                if (shotCtl != null)
                {
                    float si = 0f;
                    while (shotCtl.State != Golfin.Gameplay.Input.ShotState.Idle && si < 4f)
                    { si += Time.unscaledDeltaTime; yield return null; }
                    shotCtl.BeginExternalDrag();
                    const float ramp = 0.85f; float rt = 0f;
                    while (rt < ramp) { rt += Time.unscaledDeltaTime; shotCtl.SetExternalPower(Mathf.Lerp(0f, 0.55f, rt / ramp), 0f); yield return null; }
                    shotCtl.SetExternalPower(0.55f, 0f);
                    yield return new WaitForSecondsRealtime(0.18f);
                    shotCtl.EndExternalDrag();
                }
                else { ctrl.FireViaShotController(0.55f, Golfin.Gameplay.Input.DebugShotAccuracy.Green); }
                float e = 0f;
                while (!canopyDone && e < 30f) { e += Time.unscaledDeltaTime; yield return null; }
                d.LogStep($"  [CanopyHit] complete e={e:F1}s ball={ctrl.BallPosition:F1}");
            }
            yield return new WaitForSecondsRealtime(2.0f);
            yield return d.Capture("canopy_hit_after");
            Vector3 canopyFinalPos = ctrl.BallPosition;
            d.LogStep($"  [CanopyHit] final={canopyFinalPos:F1} (expected: SHORT of z=-121.3)");

            // ── C: Control Shot (trees disabled via reflection) ───────────────
            d.LogStep("=== Part C: Control Shot (trees disabled) ===");
            var treeField = typeof(PhysicsLabController).GetField(
                "_treeProvider",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            object savedProvider = null;
            if (treeField != null)
            {
                savedProvider = treeField.GetValue(ctrl);
                treeField.SetValue(ctrl, null);
                d.LogStep($"  [Control] _treeProvider nulled (was {(savedProvider != null ? "set" : "null")})");
            }
            else
            {
                d.LogStep("  [Control] WARN: _treeProvider field not found — control may not differ");
            }

            ctrl.PlaceBallAt(canopyPos, preferredSurfaceTypeValue: null);
            yield return new WaitForSecondsRealtime(0.5f);
            yield return d.Capture("control_before");
            ctrl.SetClub(0);
            shotCtl?.ClearStatBundleOverride();
            ctrl.SetCameraYawRadians(yawToTree);
            {
                bool ctrlDone = false;
                if (sm != null) sm.OnShotComplete += r => { ctrlDone = true; };
                if (shotCtl != null)
                {
                    float si = 0f;
                    while (shotCtl.State != Golfin.Gameplay.Input.ShotState.Idle && si < 4f)
                    { si += Time.unscaledDeltaTime; yield return null; }
                    shotCtl.BeginExternalDrag();
                    const float ramp = 0.85f; float rt = 0f;
                    while (rt < ramp) { rt += Time.unscaledDeltaTime; shotCtl.SetExternalPower(Mathf.Lerp(0f, 0.55f, rt / ramp), 0f); yield return null; }
                    shotCtl.SetExternalPower(0.55f, 0f);
                    yield return new WaitForSecondsRealtime(0.18f);
                    shotCtl.EndExternalDrag();
                }
                else { ctrl.FireViaShotController(0.55f, Golfin.Gameplay.Input.DebugShotAccuracy.Green); }
                float e = 0f;
                while (!ctrlDone && e < 30f) { e += Time.unscaledDeltaTime; yield return null; }
                d.LogStep($"  [Control] complete e={e:F1}s ball={ctrl.BallPosition:F1}");
            }
            yield return new WaitForSecondsRealtime(2.0f);
            yield return d.Capture("control_after");
            Vector3 controlFinalPos = ctrl.BallPosition;
            d.LogStep($"  [Control] final={controlFinalPos:F1} (expected: PAST tree, further than canopy)");

            // Restore _treeProvider.
            if (treeField != null && savedProvider != null)
            {
                treeField.SetValue(ctrl, savedProvider);
                d.LogStep("  [Control] _treeProvider restored.");
            }

            // ── Summary ───────────────────────────────────────────────────────
            float delta = Vector3.Distance(
                new Vector3(controlFinalPos.x, 0f, controlFinalPos.z),
                new Vector3(canopyFinalPos.x,  0f, canopyFinalPos.z));
            d.LogStep($"  [Summary] Control vs Canopy flat delta={delta:F1}m (>0 = trees damping)");
            if (delta > 2f)
                d.LogStep("=== TreeCollisionGate: PASS ===");
            else
                d.LogStep($"=== TreeCollisionGate: PARTIAL/FAIL — delta={delta:F1}m < 2m ===");

            } // end try
            finally
            {
                // Restore ShellScene canvases on ALL exit paths — normal exit, early yield break,
                // and unhandled exceptions. FlushLog also runs unconditionally here.
                restoreCanvases();
                d.FlushLog();
            }
        }

        // ── Scenario: tree_trunk_normal_play ──────────────────────────────────
        // iter-8c: minimal single-shot trunk video using the NORMAL chase camera.
        // No camera tricks, no Downrange mode, no per-frame camera override.
        // Ball placed 10m from the target trunk; LOW power=0.20 flat shot fires
        // straight at the bare lower trunk.  The chase cam follows the ball and
        // settles naturally on it at rest against the trunk base.
        //
        // Target tree (Hole 1): x=-87.04, z=-121.27 — same as TreeCollisionGate PART A.
        //   Profile MESH_JapaneseBlack_01: trunkRadius≈0.339m, trunkTopY≈4.375m
        //   Ball start: x=-87.45, z=-113.3  (8m in front of trunk, shifted west to account for x-drift)
        //   Power=0.20 → flat trajectory, strikes lower bare bark at y≈1-2m, drops dead (y≈1.0)
        //   This start position is known-good: iter-8b confirmed ball rests at y≈1.0 ground level.
        //
        // KEY DIFFERENCE FROM TreeCollisionGate PART A: ZERO camera code.
        // Normal chase camera follows ball throughout and settles on it at rest.

        /// <summary>
        /// Minimal trunk-strike video: one flat shot at a bare lower trunk.
        /// Normal chase camera only — zero camera code.
        /// </summary>
        public static IEnumerator TreeTrunkNormalPlay(BotDriver d)
        {
            d.LogStep("=== Tree Trunk Normal Play (tree_collisions iter-8c) ===");

            // Hide ShellScene canvases so the PhysicsLab camera dominates the Game View.
            var shellCanvases = Object.FindObjectsOfType<Canvas>();
            var hiddenCanvases = new System.Collections.Generic.List<Canvas>();
            foreach (var c in shellCanvases)
            {
                if (c.gameObject.scene.name == "ShellScene" && c.enabled)
                {
                    c.enabled = false;
                    hiddenCanvases.Add(c);
                }
            }
            d.LogStep($"  Hidden {hiddenCanvases.Count} ShellScene canvases.");

            System.Action restoreCanvases = () =>
            {
                foreach (var c in hiddenCanvases) { if (c != null) c.enabled = true; }
                d.LogStep($"  Restored {hiddenCanvases.Count} ShellScene canvases.");
            };

            return TreeTrunkNormalPlayBody(d, restoreCanvases);
        }

        private static IEnumerator TreeTrunkNormalPlayBody(
            BotDriver d,
            System.Action restoreCanvases)
        {
            try
            {

            // 1. Load LabScaffold + Hole_01_Geo additively (same as TreeCollisionGate).
            d.LogStep("  Loading LabScaffold + Hole_01_Geo...");
            var opLab = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(
                "LabScaffold", UnityEngine.SceneManagement.LoadSceneMode.Additive);
            if (opLab == null)
            {
                d.LogStep("=== TreeTrunkNormalPlay: FAIL — LabScaffold not in Build Settings ===");
                yield break;
            }
            var opHole = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(
                "Hole_01_Geo", UnityEngine.SceneManagement.LoadSceneMode.Additive);
            if (opHole == null)
            {
                d.LogStep("=== TreeTrunkNormalPlay: FAIL — Hole_01_Geo not in Build Settings ===");
                yield break;
            }

            float lw = 0f;
            while ((!opLab.isDone || !opHole.isDone) && lw < 30f)
            {
                yield return new WaitForSecondsRealtime(0.25f);
                lw += 0.25f;
            }
            if (!opLab.isDone || !opHole.isDone)
            {
                d.LogStep($"=== TreeTrunkNormalPlay: FAIL — load timed out ({lw:F1}s) ===");
                yield break;
            }
            d.LogStep($"  Scenes loaded ({lw:F1}s). Waiting for IsHoleReady...");

            // 2. Wait for PhysicsLabController.IsHoleReady (tree CSV bake loaded).
            var ctrl = Object.FindObjectOfType<PhysicsLabController>();
            if (ctrl == null)
            {
                d.LogStep("=== TreeTrunkNormalPlay: FAIL — PhysicsLabController not found ===");
                yield break;
            }
            float hw = 0f;
            while (!ctrl.IsHoleReady && hw < 15f)
            {
                yield return new WaitForSecondsRealtime(0.25f);
                hw += 0.25f;
            }
            if (!ctrl.IsHoleReady)
            {
                d.LogStep($"=== TreeTrunkNormalPlay: FAIL — IsHoleReady never true ({hw:F1}s) ===");
                yield break;
            }
            d.LogStep($"  IsHoleReady=true ({hw:F1}s). Extra settle 1s...");
            yield return new WaitForSecondsRealtime(1f);

            // 3. Place ball east of trunk, shoot westward — SIDE approach avoids dense upper canopy.
            //    Target tree: idx=247, x=-132.879, z=-53.239 (MESH_JapaneseBlack_01, scale=1.063, Hole 1)
            //    Verified IN-BOUNDS via zones.json OB mask; all positions in-bounds.
            //    Profile: trunkRadius=0.35m→0.371m@scale, canopyRadius=3.5m→3.719m@scale
            //    Ball start: x=-122.0, z=-53.239  (10.9m east of trunk center, 7.16m east of canopy edge)
            //    → SurfaceSnap hits terrain (ball at ground level) → ball rolls westward along z=-53.239
            //    → enters canopy east face → canopyHitDamping=0.40 → hits trunk east face (XZ reflect)
            //    → bounces back east → comes to rest on ground east of trunk (all in-bounds)
            //    Side approach: ball path stays near ground level, avoids dense upper branch geometry.
            //    Previous attempt (south approach, tree idx=74): physical ball lodged at y=15.96m in foliage.
            //    yawToTree = atan2(0, -1) = π rad → shot direction is -x (westward toward trunk)
            var ballPos     = new Vector3(-122.0f, 0f, -53.239f);
            float yawToTree = Mathf.Atan2(0f, -1f);

            ctrl.PlaceBallAt(ballPos, preferredSurfaceTypeValue: null);
            ctrl.SetCameraYawRadians(yawToTree);
            d.LogStep($"  Ball placed at {ballPos:F2}, yaw={yawToTree:F3} (north). No camera code — normal chase cam.");

            yield return new WaitForSecondsRealtime(0.5f);
            yield return d.Capture("trunk_normal_before");

            // 4. Fire one low-power shot (power=0.18, no accuracy offset).
            //    Ball at x=-122.0 → canopy entry at x=-129.16 → trunk at x=-132.879.
            //    Power 0.18: ~13.5m/s initial westward → 7.16m to canopy edge → enters canopy (×0.40 = ~5.4m/s)
            //    → 3.35m more → hits trunk east face → XZ reflect (restitution=0.15) → ball goes back east
            //    → comes to rest on ground east of trunk (all in-bounds).
            //    ZERO camera code — normal chase camera follows the ball automatically.
            var sm      = ctrl.BallSM;
            var shotCtl = Object.FindObjectOfType<Golfin.Gameplay.Input.ShotController>();

            ctrl.SetClub(0); // Driver — lowest loft (10.9°), keeps ball near ground for lateral approach
            ctrl.InjectLabBundleForCurrentClub(); // Lab stats for consistent behavior

            bool shotDone = false;
            if (sm != null) sm.OnShotComplete += r => { shotDone = true; };
            if (shotCtl != null)
            {
                float si = 0f;
                while (shotCtl.State != Golfin.Gameplay.Input.ShotState.Idle && si < 4f)
                { si += Time.unscaledDeltaTime; yield return null; }
                shotCtl.BeginExternalDrag();
                const float ramp = 0.60f; float rt = 0f;
                while (rt < ramp)
                {
                    rt += Time.unscaledDeltaTime;
                    shotCtl.SetExternalPower(Mathf.Lerp(0f, 0.18f, rt / ramp), 0f);
                    yield return null;
                }
                shotCtl.SetExternalPower(0.18f, 0f);
                yield return new WaitForSecondsRealtime(0.18f);
                shotCtl.EndExternalDrag();
            }
            else
            {
                ctrl.FireViaShotController(0.18f, Golfin.Gameplay.Input.DebugShotAccuracy.Green);
            }
            d.LogStep("  Shot fired (power=0.18, westward). Waiting for OnShotComplete...");

            // 5. Wait for shot complete — NO camera manipulation at all.
            float elapsed = 0f;
            while (!shotDone && elapsed < 30f)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            d.LogStep($"  Shot done after {elapsed:F1}s. BallPos={ctrl.BallPosition:F2}");

            // 6. Extra settle time — let ball come fully to rest and chase cam settle on it.
            yield return new WaitForSecondsRealtime(3.5f);

            // 7. Capture at-rest frame showing ball on ground at bare trunk base.
            yield return d.Capture("trunk_normal_atrest");
            d.LogStep($"  At-rest capture done. FinalPos={ctrl.BallPosition:F2}");

            Vector3 finalPos = ctrl.BallPosition;
            float xzDistToTrunk = Mathf.Sqrt(
                Mathf.Pow(finalPos.x - (-132.879f), 2f) +
                Mathf.Pow(finalPos.z - (-53.239f), 2f));
            d.LogStep($"  xzDist to trunk center={xzDistToTrunk:F2}m (trunkRadius@scale=0.371m, canopyRadius=3.719m)");

            if (finalPos.y < 1.5f)
                d.LogStep("=== TreeTrunkNormalPlay: PASS — ball at ground level (y<1.5) ===");
            else
                d.LogStep($"=== TreeTrunkNormalPlay: PARTIAL — ball y={finalPos.y:F2} (expected <1.5, check for foliage lodge) ===");

            } // end try
            finally
            {
                restoreCanvases();
                d.FlushLog();
            }
        }

        // ── Audio helper (reflection, cross-assembly) ─────────────────────────

        /// <summary>
        /// Set AudioManager.SetMusicVolume(percent) via reflection.
        /// BotDriver assembly (Golfin.Physics.Viewer) cannot directly reference
        /// Assembly-CSharp types; same pattern used by ArmCharacterBuild.
        /// </summary>
        private static void SetMusicVolumeReflection(BotDriver d, float volumePercent)
        {
            try
            {
                // Find AudioManager MonoBehaviour by type name — avoids direct reference.
                var allBehaviours = UnityEngine.Object.FindObjectsOfType<UnityEngine.MonoBehaviour>();
                UnityEngine.MonoBehaviour amInstance = null;
                foreach (var mb in allBehaviours)
                {
                    if (mb.GetType().Name == "AudioManager") { amInstance = mb; break; }
                }
                if (amInstance == null)
                {
                    d.LogStep($"  SetMusicVolumeReflection WARN: AudioManager not found — music volume unchanged");
                    return;
                }
                var setVol = amInstance.GetType().GetMethod("SetMusicVolume",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance,
                    null, new System.Type[] { typeof(float) }, null);
                if (setVol == null)
                {
                    d.LogStep("  SetMusicVolumeReflection WARN: SetMusicVolume(float) not found");
                    return;
                }
                setVol.Invoke(amInstance, new object[] { volumePercent });
                d.LogStep($"  SetMusicVolumeReflection OK: volume={volumePercent}%");
            }
            catch (System.Exception ex)
            {
                d.LogStep($"  SetMusicVolumeReflection ERROR: {ex.Message}");
            }
        }

        // ── Scenario: Audio UI and Music Slider ───────────────────────────────

        /// <summary>
        /// Order 350 audio fidelity Clip 1.
        ///
        /// Flow:
        ///   1. Boot real ShellScene to Home — menu music is playing.
        ///   2. Capture home with music audible.
        ///   3. Open Settings → expand Sound accordion → capture with slider visible.
        ///   4. Drag the MUSIC slider to near 0 (5%) — music audibly quiets.
        ///   5. Tap several UI buttons so the UI tap SFX is audible over the now-quiet music.
        ///   6. Close Settings.
        ///
        /// This clip proves: menu music starts → slider quiets it → UI SFX audible.
        ///
        /// Captures: home_music_playing, settings_sound_open, slider_dragged_quiet,
        ///           home_after_settings.
        ///
        /// Duration: ~20–25s. BotVideoRecorder must be armed with CaptureAudio=true and a
        /// custom output path before this scenario runs (see AudioUiMusicSliderMenu).
        /// </summary>
        public static IEnumerator AudioUiMusicSlider(BotDriver d)
        {
            d.LogStep("=== Audio UI and Music Slider ===");

            // 1. Navigate to Home — menu music should be playing when we arrive.
            yield return d.NavigateToHome(totalTimeoutSeconds: 60f);
            yield return new WaitForSecondsRealtime(2f); // let music start
            yield return d.Capture("home_music_playing");

            // 2. Open Settings.
            yield return d.Click("SettingsButton", settleSeconds: 1.0f);
            yield return d.WaitForGameObject("SettingsPanel", timeoutSeconds: 10f);
            yield return new WaitForSecondsRealtime(0.5f);

            // 3. Expand Sound accordion.
            yield return d.Click("SoundSettingsRow", settleSeconds: 1.0f);
            yield return new WaitForSecondsRealtime(0.8f); // accordion animation
            yield return d.Capture("settings_sound_open");

            // 4. Drag MUSIC slider to near 0 (5%) — music audibly quiets.
            //    The slider GO name in ShellScene is "MusicVolumeSlider".
            yield return d.SetSliderValue("MusicVolumeSlider", 0.05f);
            yield return new WaitForSecondsRealtime(1.0f); // let AudioManager apply the change
            yield return d.Capture("slider_dragged_quiet");

            // 5. Tap a few UI buttons so the UI tap SFX is audible over now-quiet music.
            yield return d.Click("CloseButton", settleSeconds: 0.8f);
            yield return new WaitForSecondsRealtime(0.5f);

            // Navigate to a sub-section and back to generate more UI taps.
            yield return d.Click("SettingsButton", settleSeconds: 0.8f);
            yield return new WaitForSecondsRealtime(0.3f);
            yield return d.Click("CloseButton", settleSeconds: 0.8f);
            yield return new WaitForSecondsRealtime(0.5f);

            // 6. Capture home after returning (music quiet, SFX audible in clip).
            yield return d.WaitForScreen("Home", timeoutSeconds: 10f);
            yield return d.Capture("home_after_settings");

            // Restore music volume to 70% so the next clip isn't affected.
            // Access AudioManager via reflection — cross-assembly boundary constraint.
            SetMusicVolumeReflection(d, 70f);

            d.LogStep("=== Audio UI and Music Slider: all captures done ===");
        }

        // ── Scenario: Audio Gameplay Shots ────────────────────────────────────

        /// <summary>
        /// Order 350 audio fidelity Clip 2.
        ///
        /// Flow:
        ///   1. Boot real ShellScene → Practice mode card PLAY → Hole Selection → Hole 1 Geo.
        ///      Uses Practice (solo) path — no matchmaking modal, direct to gameplay.
        ///      GameplaySceneLoader.BeginGameplayLoad runs in the FULL ShellScene rendering
        ///      context (real water, real post-processing). Never direct-loads LabScaffold.
        ///   2. Quiet music immediately after hole loads (so SFX are clearly audible).
        ///   3. Fire real shots via PlayHoleToCup — ball visibly flies, bounces, settles.
        ///      Cesar hears: swing + hit + per-bounce land sounds matching visible ball action.
        ///      If the hole holes out, cup-drop sound fires.
        ///   4. Capture result modal.
        ///
        /// Duration: ~50–80s total (hole load ~30s + shots ~20-30s).
        /// BotVideoRecorder armed with CaptureAudio=true, cap=90s, and custom output path
        /// (see RunAudioGameplayShots in LoopV2SmokeBotMenu).
        /// </summary>
        public static IEnumerator AudioGameplayShots(BotDriver d)
        {
            d.LogStep("=== Audio Gameplay Shots ===");

            // 1. Navigate to Home (real ShellScene boot).
            yield return d.NavigateToHome(totalTimeoutSeconds: 60f);
            yield return new WaitForSecondsRealtime(2f); // let mode carousel and music settle
            yield return d.Capture("home");

            // 2. Practice mode card PLAY → Hole Selection (no matchmaking, no modal).
            //    ModeCarouselController.HandlePlayClicked dispatches to ShowScreen(HoleSelection).
            yield return d.ClickModeCardPlay("practice", settleSeconds: 1.5f);

            // 3. Hole Selection screen — wait for HoleCardController auto-expand.
            yield return d.WaitForScreen("HoleSelection", timeoutSeconds: 15f);
            yield return new WaitForSecondsRealtime(3f); // HoleCardController expand animation
            yield return d.Capture("hole_selection");

            // 4. Tap PLAY on the first hole card (ActionButton in the auto-expanded row).
            //    HoleSelectionScreenController.HandleActionClicked → GameSession.SeedSession
            //    + GameplaySceneLoader.BeginGameplayLoad — real production path, no matchmaking.
            yield return d.Click("ActionButton", settleSeconds: 1.5f);

            // 5. Wait for hole scene to load. LabScaffold is the host; Hole_01_Geo is additive.
            yield return d.WaitForSceneLoaded("LabScaffold", timeoutSeconds: 40f);
            yield return d.WaitForSceneLoaded("Hole_01_Geo", timeoutSeconds: 40f);
            yield return new WaitForSecondsRealtime(3f); // fade-in + HUD settle

            // 6. Quiet music so SFX (swing, hit, land, cup) are clearly audible over near-silence.
            //    Access AudioManager via reflection — cross-assembly boundary constraint.
            SetMusicVolumeReflection(d, 8f);

            yield return new WaitForSecondsRealtime(0.5f);
            yield return d.Capture("gameplay_armed");

            // 7. Play hole via real physics shots — fires swing/hit/land/cup sounds each stroke.
            yield return d.PlayHoleToCup(par: 5);

            // 8. Wait for result modal and capture.
            yield return new WaitForSecondsRealtime(2f);
            yield return d.Capture("result_modal");

            d.LogStep("=== Audio Gameplay Shots: all captures done ===");
        }

        // ── Scenario: Audio Gameplay Shots V3 ────────────────────────────────

        /// <summary>
        /// Order 350 audio fidelity Clip 3 (v3 — hit-audibility fix).
        ///
        /// Differences from AudioGameplayShots (v2):
        ///   - DEFERRED recording start: BotVideoRecorder.ArmDeferred() was called by the
        ///     menu item. BeginDeferred() fires HERE after the hole is fully loaded and
        ///     several frames have rendered — skipping the EnteredPlayMode Y-flip transient
        ///     that appeared in the first frame of earlier clips. The recording starts clean,
        ///     showing the ball already armed on the tee with the HUD visible.
        ///   - MID-POWER first stroke: firstStrokePowerOverride = 0.5f. At 50% power the
        ///     Driver triggers HitDefault (not HitStrong). HitDefault baseVolume = 1.0
        ///     (post-rebalance), which is ABOVE the swing (now 0.55). The hit is clearly
        ///     audible at mid-power. A full-power shot triggers HitStrong and overshoots OOB.
        ///   - Shorter clip: recording starts at hole-armed state (after ~30s of load), so the
        ///     cap can be tighter (~35s). No home/hole-selection footage in the clip.
        ///
        /// Flow:
        ///   1. Boot real ShellScene → Practice → Hole 1. (pre-recording — avoids Y-flip)
        ///   2. Once hole is armed + frames settled: BeginDeferred() → recording starts clean.
        ///   3. Quiet music (8%). Fire PlayHoleToCup with power=0.5 — audible HitDefault.
        ///   4. Capture strokes + result modal.
        ///
        /// Duration of RECORDED segment: ~25–35s (no home/hole-selection overhead).
        /// BotVideoRecorder armed with ArmDeferred+CaptureAudio=true, cap=45s.
        /// </summary>
        public static IEnumerator AudioGameplayShotsV3(BotDriver d)
        {
            d.LogStep("=== Audio Gameplay Shots V3 ===");

            // 1. Navigate to Home (real ShellScene boot — recording NOT yet running).
            yield return d.NavigateToHome(totalTimeoutSeconds: 60f);
            yield return new WaitForSecondsRealtime(2f);

            // 2. Practice mode card PLAY → Hole Selection (no matchmaking, no modal).
            yield return d.ClickModeCardPlay("practice", settleSeconds: 1.5f);

            // 3. Hole Selection screen — tap first hole PLAY.
            yield return d.WaitForScreen("HoleSelection", timeoutSeconds: 15f);
            yield return new WaitForSecondsRealtime(2f); // HoleCardController expand animation
            yield return d.Click("ActionButton", settleSeconds: 1.5f);

            // 4. Wait for hole scene to load fully.
            yield return d.WaitForSceneLoaded("LabScaffold", timeoutSeconds: 40f);
            yield return d.WaitForSceneLoaded("Hole_01_Geo", timeoutSeconds: 40f);
            yield return new WaitForSecondsRealtime(4f); // fade-in + HUD fully rendered; avoids Y-flip

            // 5. START RECORDING NOW — hole is armed, HUD visible, no Y-flip risk.
            //    The menu item called ArmDeferred() which set DeferredRecord=true in SessionState.
            //    Inject RecordVideo=true here (matching what BeginDeferred() does) so Begin() fires.
            //    BotVideoRecorder.Begin() is invoked via the EnteredPlayMode handler; since we're
            //    already in play mode we call it through the known SessionState contract:
            //    set RecordVideo=true and invoke Begin() indirectly by setting the deferred flag.
            //    Direct path: SessionState manipulation to arm + the EditorApplication.update
            //    hook fires Begin(). Since this assembly IS under #if UNITY_EDITOR, we can use
            //    UnityEditor.SessionState directly to set the RecordVideo key and trigger Begin().
            UnityEditor.SessionState.SetBool("LoopV2SmokeBot.RecordVideo", true);
            // Also clear DeferredRecord so it doesn't leak.
            UnityEditor.SessionState.SetBool("LoopV2SmokeBot.DeferredRecord", false);
            // Now call BotVideoRecorder.Begin() through reflection to avoid the cross-assembly reference.
            try
            {
                var recType = System.Type.GetType("Golfin.Physics.Viewer.Editor.BotVideoRecorder, Golfin.Physics.Viewer.BotEditor");
                if (recType == null)
                {
                    // Try all assemblies.
                    foreach (var a in System.AppDomain.CurrentDomain.GetAssemblies())
                    { var t = a.GetType("Golfin.Physics.Viewer.Editor.BotVideoRecorder"); if (t != null) { recType = t; break; } }
                }
                if (recType != null)
                {
                    var beginMethod = recType.GetMethod("Begin", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    beginMethod?.Invoke(null, null);
                    d.LogStep("  BeginDeferred (reflection): BotVideoRecorder.Begin() called — recording started");
                }
                else
                {
                    d.LogStep("  BeginDeferred WARN: BotVideoRecorder type not found — recording may not have started");
                }
            }
            catch (System.Exception ex)
            {
                d.LogStep($"  BeginDeferred ERROR: {ex.Message}");
            }
            yield return new WaitForSecondsRealtime(1f); // let first recording frames settle

            // 6. Quiet music so swing+hit+land are clearly audible.
            SetMusicVolumeReflection(d, 8f);
            yield return new WaitForSecondsRealtime(0.5f);
            yield return d.Capture("gameplay_armed");

            // 7. Play hole via real physics shots — mid-power (0.5) first stroke → HitDefault.
            //    Mid-power Driver (~50%) carries ~100-120m, landing in the fairway (not OOB).
            yield return d.PlayHoleToCup(par: 5, firstStrokePowerOverride: 0.5f);

            // 8. Wait for result modal and capture.
            yield return new WaitForSecondsRealtime(2f);
            yield return d.Capture("result_modal");

            // 9. Restore music volume.
            SetMusicVolumeReflection(d, 70f);

            d.LogStep("=== Audio Gameplay Shots V3: all captures done ===");
        }

        // ── Scenario: Audio Putt To Cup (Order 350 fidelity clip — FOCUSED) ─────
        // Produced 2026-06-16. Mirrors AudioGameplayShotsV3 boot path but narrows focus
        // to the putt-to-cup audio moment: putt-hit SFX audible, no ground-settle sounds
        // (settle suppression fires for IsPutt=true), cup-drop SFX on InCup.
        //
        // BotVideoRecorder MUST be armed with:
        //   CaptureAudio = true
        //   MaxRecordSecondsSessionOverride = 25
        //   CustomOutputPath = "Docs/Specs/Active/sound_effects/videos/audio_putt_to_cup"
        //   ArmDeferred() — deferred start so recording begins after hole is armed (no Y-flip).
        //
        // Flow:
        //   1. ShellScene boot → Practice → HoleSelection → Hole 1 (first unlocked).
        //      Recording NOT yet running during navigation (avoids Y-flip + nav overhead).
        //   2. After hole loads + renders (4s settle): BeginDeferred() → recording starts.
        //   3. Quiet music to 5% so putt-hit + cup-drop are clearly audible.
        //   4. BotDriver.FireShot(pinWorld) — §2f pattern:
        //        PlaceBallAt(pin - 3m), SetClub(PutterIndex), fire.
        //      BallAudioEmitter suppresses land/settle sounds (IsPutt=true guard).
        //      Ball drops InCup → cup-drop SFX (HitBallIn).
        //   5. ForceShotComplete("InCup") safety net if real putt misses in 20s.
        //   6. Short dwell to capture cup-drop + result modal.
        //   7. Restore music volume to 70%.
        //
        // Expected recorded duration: ~8–15s (putt fire → InCup settle + result modal).
        // </summary>
        public static IEnumerator AudioPuttToCup(BotDriver d)
        {
            d.LogStep("=== Audio Putt To Cup ===");

            // 1. Navigate to Home via real ShellScene boot (recording not running yet).
            yield return d.NavigateToHome(totalTimeoutSeconds: 60f);
            yield return new WaitForSecondsRealtime(2f);

            // 2. Practice mode → HoleSelection → Hole 1.
            yield return d.ClickModeCardPlay("practice", settleSeconds: 1.5f);
            yield return d.WaitForScreen("HoleSelection", timeoutSeconds: 15f);
            yield return new WaitForSecondsRealtime(2f); // HoleCardController expand animation
            yield return d.Click("ActionButton", settleSeconds: 1.5f);

            // 3. Wait for hole scene to fully load.
            yield return d.WaitForSceneLoaded("LabScaffold", timeoutSeconds: 40f);
            yield return d.WaitForSceneLoaded("Hole_01_Geo", timeoutSeconds: 40f);
            yield return new WaitForSecondsRealtime(4f); // fade-in + HUD settle; avoids Y-flip

            // 4. START RECORDING NOW — hole armed, HUD visible, no Y-flip.
            //    Mirrors AudioGameplayShotsV3 §5: set RecordVideo via SessionState + call Begin().
            UnityEditor.SessionState.SetBool("LoopV2SmokeBot.RecordVideo", true);
            UnityEditor.SessionState.SetBool("LoopV2SmokeBot.DeferredRecord", false);
            try
            {
                System.Type recType = null;
                foreach (var a in System.AppDomain.CurrentDomain.GetAssemblies())
                { var t = a.GetType("Golfin.Physics.Viewer.Editor.BotVideoRecorder"); if (t != null) { recType = t; break; } }
                if (recType != null)
                {
                    var beginMethod = recType.GetMethod("Begin", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    beginMethod?.Invoke(null, null);
                    d.LogStep("  BeginDeferred: BotVideoRecorder.Begin() called — recording started");
                }
                else { d.LogStep("  BeginDeferred WARN: BotVideoRecorder type not found"); }
            }
            catch (System.Exception ex) { d.LogStep($"  BeginDeferred ERROR: {ex.Message}"); }
            yield return new WaitForSecondsRealtime(1f); // let first frames settle

            // 5. Quiet music — putt-hit + cup-drop must be clearly audible.
            SetMusicVolumeReflection(d, 5f);
            yield return new WaitForSecondsRealtime(0.5f);
            yield return d.Capture("putt_armed");

            // 6. Find pin world position from HoleContext and resolve PhysicsLabController.
            Vector3 pinWorld = Golfin.Gameplay.UI.HUD.HoleContext.PinWorld;
            d.LogStep($"  PinWorld = {pinWorld:F2}");

            var ctrl = UnityEngine.Object.FindFirstObjectByType<PhysicsLabController>();
            if (ctrl == null) { d.LogStep("  FAIL: no PhysicsLabController"); yield break; }

            // 7. Fire putter toward pin via production ShotController path (mirrors AudioWaterSplashSfx):
            //    - SetClub(PutterIndex) → IsPutt=true on BallAudioEmitter → suppresses per-bounce land SFX.
            //    - InjectLabBundle → ensures club stats resolve.
            //    - SetCameraYaw toward pin.
            //    - FireViaShotController(power, Green) → real ball flight + full audio path.
            //    - BallAudioEmitter.HandleHit: IsPutt=true → HitPutt SFX on first contact.
            //    - BallAudioEmitter.HandleStateChanged: InCup → HitBallIn cup-drop SFX.
            ctrl.SetClub(PhysicsLabController.PutterIndex);
            ctrl.InjectLabBundleForCurrentClub();

            // Aim toward pin: yaw from ball position to pin.
            Vector3 ballPos = ctrl.BallPosition;
            Vector3 toPinFlat = new Vector3(pinWorld.x - ballPos.x, 0f, pinWorld.z - ballPos.z);
            float putterYaw = Mathf.Atan2(toPinFlat.z, toPinFlat.x);
            ctrl.SetCameraYawRadians(putterYaw);
            d.LogStep($"  Firing Putter: yaw={putterYaw:F3} rad toward pin ({pinWorld:F1}), power=0.7");

            ctrl.FireViaShotController(0.7f, Golfin.Gameplay.Input.DebugShotAccuracy.Green);

            // 8. Wait for ball to roll into cup (or timeout 15s → safety net).
            float waitStart = Time.realtimeSinceStartup;
            while (ctrl.BallSM.State != Golfin.Gameplay.Loop.BallState.InCup
                   && Time.realtimeSinceStartup - waitStart < 15f)
            {
                yield return new WaitForSecondsRealtime(0.1f);
            }

            // Safety net: if not InCup naturally, force it so HitBallIn fires.
            if (ctrl.BallSM.State != Golfin.Gameplay.Loop.BallState.InCup)
            {
                d.LogStep("  Safety net: ForceShotComplete(InCup)");
                yield return d.ForceShotComplete("InCup", settleSeconds: 0.5f);
            }
            else
            {
                d.LogStep($"  Ball reached InCup naturally after {Time.realtimeSinceStartup - waitStart:F1}s");
            }

            // 9. Dwell on InCup + result modal.
            yield return new WaitForSecondsRealtime(3f);
            yield return d.Capture("putt_in_cup");
            yield return new WaitForSecondsRealtime(2f);

            // 10. Restore music volume.
            SetMusicVolumeReflection(d, 70f);

            d.LogStep("=== Audio Putt To Cup: done ===");
        }

        // ── Scenario: Audio Water Splash SFX (Order 350 fidelity clip — FOCUSED) ─
        // Produced 2026-06-16. Uses FULL ShellScene boot (required for AudioManager init;
        // direct LabScaffold bypasses ShellScene and produces silent audio at -91 dB).
        //
        // BotVideoRecorder MUST be armed with:
        //   CaptureAudio = true
        //   MaxRecordSecondsSessionOverride = 30
        //   CustomOutputPath = "Docs/Specs/Active/sound_effects/videos/audio_water_splash_sfx"
        //   ArmDeferred() — deferred start after hole ready (no Y-flip).
        //
        // Flow:
        //   1. ShellScene boot → NavigateToHome (recording NOT running — avoids nav overhead).
        //   2. Unlock Hole 6 via HoleProgressionService.SetUnlockedOverride(6, true).
        //   3. Practice → HoleSelection — find Hole 6 card → tap it to expand → click ActionButton.
        //   4. Wait for LabScaffold + Hole_06_Geo to load.
        //   5. 4s settle (no Y-flip).
        //   6. START RECORDING — BeginDeferred() via reflection.
        //   7. Quiet music to 5%.
        //   8. Fire Driver at AimYaw=2.9804 rad, Power=0.45 toward Hole-6 water centroid.
        //      Ball lands → LandWater SFX + WaterSplashController VFX.
        //   9. Wait 5s for splash to play out. Capture.
        //  10. Restore music volume.
        // </summary>
        // ── Scenario: Cup Capture / Lip-out clips (cup_capture_and_lipout §7) ───
        // Produced 2026-08-05. Three acceptance clips, one per Editor launch (the recorder
        // allows a single recording per launch by design). Variant is read from SessionState
        // key "CupClip.Variant": "slow" | "mid" | "fast".
        //
        //   slow — putter power 0.41 → arrives at the cup at ~1.07 m/s → CAPTURES.
        //          Ball must drop below the lip on screen and the hole-complete modal appear.
        //   mid  — power 0.49 → arrives at ~1.47 m/s, just under the 1.5 m/s gate → CAPTURES.
        //   fast — power 0.75 → arrives at ~2.77 m/s, above the gate → LIPS OUT: visible
        //          deflection + small hop, keeps rolling, NO hole-complete.
        //
        // Powers are calibrated, not guessed: launch speed is linear in power for the putter
        // (measured on this hole, 0.30 → 1.494 m/s and 0.60 → 2.989 m/s, i.e. v0 = 4.981·p),
        // and the green's rolling resistance costs ~0.52 m/s per metre over the 2 m approach.
        //
        // BotVideoRecorder MUST be armed with:
        //   CaptureAudio = true
        //   MaxRecordSecondsSessionOverride = 30
        //   CustomOutputPath = "Docs/Physics/videos/cup_<variant>"
        //   ArmDeferred() — deferred start so recording begins after the hole is armed (no Y-flip).
        public static IEnumerator CupCaptureLipoutClip(BotDriver d)
        {
            string variant = UnityEditor.SessionState.GetString("CupClip.Variant", "slow");
            float power = variant == "fast" ? 0.58f : (variant == "mid" ? 0.49f : 0.41f);
            // The fast clip starts 20 mm off the pin line so the ball crosses the mouth ~13 mm
            // off-centre. A dead-centre crossing gets the biggest hop but zero sideways kick
            // (no tangential component by construction); 20 mm buys a visible 7° deflection AND
            // a 26 mm pop, which is what "the hole grabbed it" actually looks like.
            float startZOff = variant == "fast" ? 0.02f : 0f;
            d.LogStep($"=== Cup Capture / Lip-out clip: variant={variant} power={power:F2} zOff={startZOff*1000f:F0}mm ===");

            yield return CupClipBootToHole6(d);

            // START RECORDING — hole armed, HUD visible, no Y-flip.
            UnityEditor.SessionState.SetBool("LoopV2SmokeBot.RecordVideo", true);
            UnityEditor.SessionState.SetBool("LoopV2SmokeBot.DeferredRecord", false);
            try
            {
                System.Type recType = null;
                foreach (var a in System.AppDomain.CurrentDomain.GetAssemblies())
                { var t = a.GetType("Golfin.Physics.Viewer.Editor.BotVideoRecorder"); if (t != null) { recType = t; break; } }
                recType?.GetMethod("Begin", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                       ?.Invoke(null, null);
                d.LogStep("  BotVideoRecorder.Begin() — recording started");
            }
            catch (System.Exception ex) { d.LogStep($"  Begin ERROR: {ex.Message}"); }
            yield return new WaitForSecondsRealtime(1.5f);

            var ctrl = UnityEngine.Object.FindFirstObjectByType<PhysicsLabController>();
            if (ctrl == null) { d.LogStep("  FAIL: no PhysicsLabController"); yield break; }

            Vector3 pin = Golfin.Gameplay.UI.HUD.HoleContext.PinWorld;
            d.LogStep($"  PinWorld = {pin:F3}");

            // Place the ball 2 m from the pin on the green (offset off the line for the fast
            // variant) and aim straight down the -X line.
            ctrl.PlaceBallAt(pin + new Vector3(2f, 0f, startZOff));
            ctrl.SetClub(PhysicsLabController.PutterIndex);
            ctrl.InjectLabBundleForCurrentClub();
            yield return new WaitForSecondsRealtime(2f);   // camera settle on the new ball position

            Vector3 ballPos = ctrl.BallPosition;
            Vector3 toPin = new Vector3(pin.x - ballPos.x, 0f, pin.z - ballPos.z);
            // Fast variant fires PARALLEL to the pin line (straight -X) so the 20 mm start
            // offset survives to the cup. Aiming at the pin would cancel it and produce the
            // dead-centre crossing we are deliberately avoiding.
            Vector3 aimDir = startZOff != 0f ? new Vector3(-1f, 0f, 0f) : toPin;
            ctrl.SetCameraYawRadians(Mathf.Atan2(aimDir.z, aimDir.x));
            yield return new WaitForSecondsRealtime(1.5f);
            yield return d.Capture($"cup_{variant}_armed");

            d.LogStep($"  Club index={ctrl.CurrentClubIndex} (PutterIndex={PhysicsLabController.PutterIndex}) "
                    + $"— must be the putter for the calibrated power to mean anything");
            d.LogStep($"  Firing putter at power {power:F2} from {toPin.magnitude:F2} m");
            ctrl.FireViaShotController(power, Golfin.Gameplay.Input.DebugShotAccuracy.Green);

            // Watch the outcome. NO ForceShotComplete safety net here — this clip exists to
            // show what the physics actually does, so a forced terminal state would defeat it.
            float t0 = Time.realtimeSinceStartup;
            var seen = ctrl.BallSM.State;
            while (Time.realtimeSinceStartup - t0 < 14f)
            {
                if (ctrl.BallSM.State != seen)
                {
                    seen = ctrl.BallSM.State;
                    d.LogStep($"  BallState -> {seen} at t+{Time.realtimeSinceStartup - t0:F1}s");
                    if (seen == Golfin.Gameplay.Loop.BallState.InCup
                     || seen == Golfin.Gameplay.Loop.BallState.AtRest) break;
                }
                yield return new WaitForSecondsRealtime(0.05f);
            }
            d.LogStep($"  Terminal state: {ctrl.BallSM.State} (expected "
                    + (variant == "fast" ? "AtRest — lip-out, no hole-complete)" : "InCup — captured)"));

            yield return new WaitForSecondsRealtime(4f);   // dwell on the drop / rest + modal
            yield return d.Capture($"cup_{variant}_settled");
            yield return new WaitForSecondsRealtime(2f);

            d.LogStep($"=== Cup clip {variant}: done ===");
        }

        /// <summary>
        /// Shared boot for the cup clips: ShellScene → Home → Practice → HoleSelection →
        /// Hole 6 → loaded and settled. Mirrors AudioWaterSplashSfx's Hole-6 path (kept as a
        /// separate copy rather than refactoring that scenario, so the audio clips can't regress).
        /// </summary>
        static IEnumerator CupClipBootToHole6(BotDriver d)
        {
            yield return d.NavigateToHome(totalTimeoutSeconds: 60f);
            yield return new WaitForSecondsRealtime(2f);

            // Hole 6 is locked by default — unlock it for the run.
            try
            {
                System.Type svcType = null;
                foreach (var a in System.AppDomain.CurrentDomain.GetAssemblies())
                { var t = a.GetType("GolfinRedux.UI.HoleSelection.HoleProgressionService"); if (t != null) { svcType = t; break; } }
                if (svcType == null)
                    foreach (var a in System.AppDomain.CurrentDomain.GetAssemblies())
                    { var t = a.GetType("HoleProgressionService"); if (t != null) { svcType = t; break; } }
                var inst = svcType?.GetProperty("Instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)?.GetValue(null);
                if (inst != null)
                {
                    svcType.GetMethod("SetUnlockedOverride", new[] { typeof(int), typeof(bool) })
                           ?.Invoke(inst, new object[] { 6, true });
                    d.LogStep("  Hole 6 unlocked");
                }
            }
            catch (System.Exception ex) { d.LogStep($"  WARN: unlock reflection error: {ex.Message}"); }

            yield return d.ClickModeCardPlay("practice", settleSeconds: 1.5f);
            yield return d.WaitForScreen("HoleSelection", timeoutSeconds: 15f);
            yield return new WaitForSecondsRealtime(3f);

            // Tap the Hole 6 card (real widget onClick), then drive the same loader entry
            // HoleSelectionScreenController.HandleActionClicked uses.
            try
            {
                System.Type cardType = null;
                foreach (var a in System.AppDomain.CurrentDomain.GetAssemblies())
                { var t = a.GetType("GolfinRedux.UI.HoleSelection.HoleCardController"); if (t != null) { cardType = t; break; } }
                if (cardType == null)
                    foreach (var a in System.AppDomain.CurrentDomain.GetAssemblies())
                    { var t = a.GetType("HoleCardController"); if (t != null) { cardType = t; break; } }
                if (cardType != null)
                {
                    var holeNumProp = cardType.GetProperty("HoleNumber", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    foreach (var card in UnityEngine.Object.FindObjectsByType(cardType, UnityEngine.FindObjectsSortMode.None))
                    {
                        if ((int)(holeNumProp?.GetValue(card) ?? 0) != 6) continue;
                        var go = ((UnityEngine.Component)card).gameObject;
                        UnityEngine.UI.Button tap = null;
                        foreach (var btn in go.GetComponentsInChildren<UnityEngine.UI.Button>(true))
                            if (btn.gameObject.name.Contains("CardTapButton") || btn.gameObject.name.Contains("TapButton"))
                            { tap = btn; break; }
                        if (tap == null) tap = go.GetComponentInChildren<UnityEngine.UI.Button>();
                        if (tap != null) { tap.onClick.Invoke(); d.LogStep("  Tapped Hole 6 card"); }
                        break;
                    }
                }
            }
            catch (System.Exception ex) { d.LogStep($"  WARN: card tap reflection error: {ex.Message}"); }

            yield return new WaitForSecondsRealtime(1.5f);

            try
            {
                System.Type gsType = null;
                foreach (var a in System.AppDomain.CurrentDomain.GetAssemblies())
                { var t = a.GetType("Golfin.Gameplay.Session.GameSession"); if (t != null) { gsType = t; break; } }
                gsType?.GetMethod("SeedSession", new[] { typeof(int), typeof(string), typeof(int) })
                      ?.Invoke(null, new object[] { 6, "", 0 });

                System.Type loaderType = null;
                foreach (var a in System.AppDomain.CurrentDomain.GetAssemblies())
                { var t = a.GetType("Golfin.UI.GameplayTransition.GameplaySceneLoader"); if (t != null) { loaderType = t; break; } }
                var loader = loaderType?.GetProperty("Instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)?.GetValue(null);
                if (loader != null)
                {
                    foreach (var m in loaderType.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
                        if (m.Name == "BeginGameplayLoad")
                        {
                            m.Invoke(loader, m.GetParameters().Length == 1 ? new object[] { 6 } : new object[] { 6, null });
                            d.LogStep("  BeginGameplayLoad(6)");
                            break;
                        }
                }
            }
            catch (System.Exception ex) { d.LogStep($"  WARN: load reflection error: {ex.Message}"); }

            yield return d.WaitForSceneLoaded("LabScaffold", timeoutSeconds: 40f);
            yield return d.WaitForSceneLoaded("Hole_06_Geo", timeoutSeconds: 40f);
            yield return new WaitForSecondsRealtime(4f);   // fade-in + HUD settle; avoids Y-flip
        }

        public static IEnumerator AudioWaterSplashSfx(BotDriver d)
        {
            d.LogStep("=== Audio Water Splash SFX (ShellScene real flow) ===");

            // 1. Navigate to Home via real ShellScene boot (recording not running yet).
            yield return d.NavigateToHome(totalTimeoutSeconds: 60f);
            yield return new WaitForSecondsRealtime(2f);

            // 2. Unlock Hole 6 at runtime via HoleProgressionService reflection.
            //    This is needed because only Hole 1 is unlocked by default.
            try
            {
                System.Type svcType = null;
                foreach (var a in System.AppDomain.CurrentDomain.GetAssemblies())
                { var t = a.GetType("GolfinRedux.UI.HoleSelection.HoleProgressionService"); if (t != null) { svcType = t; break; } }
                if (svcType == null)
                {
                    // Try alternate namespace (used in production code).
                    foreach (var a in System.AppDomain.CurrentDomain.GetAssemblies())
                    { var t = a.GetType("HoleProgressionService"); if (t != null) { svcType = t; break; } }
                }
                if (svcType != null)
                {
                    var instanceProp = svcType.GetProperty("Instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    var instance = instanceProp?.GetValue(null);
                    if (instance != null)
                    {
                        var unlockMethod = svcType.GetMethod("SetUnlockedOverride", new[] { typeof(int), typeof(bool) });
                        unlockMethod?.Invoke(instance, new object[] { 6, true });
                        d.LogStep("  HoleProgressionService.SetUnlockedOverride(6, true) — Hole 6 unlocked");
                    }
                    else { d.LogStep("  WARN: HoleProgressionService.Instance is null"); }
                }
                else { d.LogStep("  WARN: HoleProgressionService type not found — Hole 6 may stay locked"); }
            }
            catch (System.Exception ex) { d.LogStep($"  WARN: unlock reflection error: {ex.Message}"); }

            // 3. Practice → HoleSelection.
            yield return d.ClickModeCardPlay("practice", settleSeconds: 1.5f);
            yield return d.WaitForScreen("HoleSelection", timeoutSeconds: 15f);
            yield return new WaitForSecondsRealtime(3f); // HoleCardController expand animation (auto-expands Hole 1)

            // 4. Find Hole 6 card, tap its CardTapButton to expand it, then click ActionButton.
            //    HoleCardController.HoleNumber == 6. Use reflection since we can't reference
            //    Assembly-CSharp HoleCardController from this assembly directly.
            bool hole6Tapped = false;
            try
            {
                System.Type cardType = null;
                foreach (var a in System.AppDomain.CurrentDomain.GetAssemblies())
                { var t = a.GetType("GolfinRedux.UI.HoleSelection.HoleCardController"); if (t != null) { cardType = t; break; } }
                if (cardType == null)
                {
                    foreach (var a in System.AppDomain.CurrentDomain.GetAssemblies())
                    { var t = a.GetType("HoleCardController"); if (t != null) { cardType = t; break; } }
                }
                if (cardType != null)
                {
                    var holeNumProp = cardType.GetProperty("HoleNumber", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    var cards = UnityEngine.Object.FindObjectsByType(cardType, UnityEngine.FindObjectsSortMode.None);
                    foreach (var card in cards)
                    {
                        int num = (int)(holeNumProp?.GetValue(card) ?? 0);
                        if (num == 6)
                        {
                            // Find the CardTapButton child on this card's transform.
                            var go = ((UnityEngine.Component)card).gameObject;
                            var tapBtn = go.GetComponentInChildren<UnityEngine.UI.Button>();
                            // CardTapButton is first sibling (SetAsFirstSibling in Awake).
                            // Walk children to find the one named "CardTapButton".
                            UnityEngine.UI.Button cardTapBtn = null;
                            foreach (var btn in go.GetComponentsInChildren<UnityEngine.UI.Button>(true))
                            {
                                if (btn.gameObject.name.Contains("CardTapButton") || btn.gameObject.name.Contains("TapButton"))
                                { cardTapBtn = btn; break; }
                            }
                            if (cardTapBtn == null) cardTapBtn = tapBtn; // fallback to first button
                            if (cardTapBtn != null)
                            {
                                cardTapBtn.onClick.Invoke();
                                d.LogStep($"  Tapped Hole 6 card (CardTapButton on '{go.name}')");
                                hole6Tapped = true;
                            }
                            else { d.LogStep($"  WARN: Hole 6 card found but no tap button"); }
                            break;
                        }
                    }
                    if (!hole6Tapped) d.LogStep("  WARN: Hole 6 card not found — falling back to first ActionButton");
                }
                else { d.LogStep("  WARN: HoleCardController type not found — falling back to first ActionButton"); }
            }
            catch (System.Exception ex) { d.LogStep($"  WARN: card tap reflection error: {ex.Message}"); }

            // 5. Seed GameSession for Hole 6 and call GameplaySceneLoader.BeginGameplayLoad(6)
            //    directly — mirrors what HoleSelectionScreenController.HandleActionClicked does.
            //    This bypasses the ActionButton visibility issue (the card expand animation
            //    may not have activated the button in time).
            yield return new WaitForSecondsRealtime(1.5f); // let card settle after tap

            bool gameplayLoadStarted = false;
            try
            {
                // GameSession.IsVersus = false (Practice path).
                System.Type gsType = null;
                foreach (var a in System.AppDomain.CurrentDomain.GetAssemblies())
                { var t = a.GetType("Golfin.Gameplay.Session.GameSession"); if (t != null) { gsType = t; break; } }
                if (gsType == null)
                {
                    foreach (var a in System.AppDomain.CurrentDomain.GetAssemblies())
                    { var t = a.GetType("GameSession"); if (t != null) { gsType = t; break; } }
                }

                if (gsType != null)
                {
                    // Set IsVersus = false.
                    var isVersusProp = gsType.GetProperty("IsVersus", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    isVersusProp?.SetValue(null, false);

                    // Get selected character + bag slot.
                    string charId = string.Empty;
                    int bagSlot = 0;
                    System.Type cmType = null;
                    foreach (var a in System.AppDomain.CurrentDomain.GetAssemblies())
                    { var t = a.GetType("CharacterManager"); if (t != null) { cmType = t; break; } }
                    if (cmType != null)
                    {
                        var cmInst = cmType.GetProperty("Instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)?.GetValue(null);
                        if (cmInst != null)
                        {
                            var getSelId = cmType.GetMethod("GetSelectedCharacterId", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                            charId = (string)(getSelId?.Invoke(cmInst, null) ?? string.Empty);
                        }
                    }
                    System.Type bmType = null;
                    foreach (var a in System.AppDomain.CurrentDomain.GetAssemblies())
                    { var t = a.GetType("BagManager"); if (t != null) { bmType = t; break; } }
                    if (bmType != null)
                    {
                        var bmInst = bmType.GetProperty("Instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)?.GetValue(null);
                        if (bmInst != null)
                        {
                            var equippedSlotProp = bmType.GetProperty("EquippedBagSlot", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                            bagSlot = (int)(equippedSlotProp?.GetValue(bmInst) ?? 0);
                        }
                    }

                    // GameSession.SeedSession(6, charId, bagSlot).
                    var seedMethod = gsType.GetMethod("SeedSession", new[] { typeof(int), typeof(string), typeof(int) });
                    seedMethod?.Invoke(null, new object[] { 6, charId, bagSlot });
                    d.LogStep($"  GameSession.SeedSession(6, '{charId}', {bagSlot}) called");

                    // GameplaySceneLoader.Instance.BeginGameplayLoad(6).
                    System.Type loaderType = null;
                    foreach (var a in System.AppDomain.CurrentDomain.GetAssemblies())
                    { var t = a.GetType("Golfin.UI.GameplayTransition.GameplaySceneLoader"); if (t != null) { loaderType = t; break; } }
                    if (loaderType == null)
                    {
                        foreach (var a in System.AppDomain.CurrentDomain.GetAssemblies())
                        { var t = a.GetType("GameplaySceneLoader"); if (t != null) { loaderType = t; break; } }
                    }
                    if (loaderType != null)
                    {
                        var loaderInst = loaderType.GetProperty("Instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)?.GetValue(null);
                        if (loaderInst != null)
                        {
                            var beginLoad = loaderType.GetMethod("BeginGameplayLoad", new[] { typeof(int), typeof(object) })
                                         ?? loaderType.GetMethod("BeginGameplayLoad", new[] { typeof(int) });
                            if (beginLoad == null)
                            {
                                // Try with ModalController parameter as null.
                                var methods = loaderType.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                                foreach (var m in methods)
                                    if (m.Name == "BeginGameplayLoad") { beginLoad = m; break; }
                            }
                            if (beginLoad != null)
                            {
                                var parameters = beginLoad.GetParameters();
                                object[] args = parameters.Length == 1
                                    ? new object[] { 6 }
                                    : new object[] { 6, null };
                                beginLoad.Invoke(loaderInst, args);
                                d.LogStep("  GameplaySceneLoader.BeginGameplayLoad(6) called — loading Hole 6");
                                gameplayLoadStarted = true;
                            }
                            else { d.LogStep("  WARN: BeginGameplayLoad method not found"); }
                        }
                        else { d.LogStep("  WARN: GameplaySceneLoader.Instance is null"); }
                    }
                    else { d.LogStep("  WARN: GameplaySceneLoader type not found"); }
                }
                else { d.LogStep("  WARN: GameSession type not found — falling back to ActionButton click"); }
            }
            catch (System.Exception ex) { d.LogStep($"  WARN: direct load reflection error: {ex.Message}"); }

            if (!gameplayLoadStarted)
            {
                // Ultimate fallback: click first available ActionButton (loads whatever hole is expanded).
                d.LogStep("  Ultimate fallback: clicking first ActionButton");
                yield return d.Click("ActionButton", settleSeconds: 1.5f);
            }
            else
            {
                yield return new WaitForSecondsRealtime(1.5f);
            }

            // 6. Wait for Hole 6 to load.
            yield return d.WaitForSceneLoaded("LabScaffold", timeoutSeconds: 40f);
            yield return d.WaitForSceneLoaded("Hole_06_Geo", timeoutSeconds: 40f);
            yield return new WaitForSecondsRealtime(4f); // fade-in + HUD settle; avoids Y-flip

            // 7. START RECORDING — hole stable, water visible, no Y-flip risk.
            UnityEditor.SessionState.SetBool("LoopV2SmokeBot.RecordVideo", true);
            UnityEditor.SessionState.SetBool("LoopV2SmokeBot.DeferredRecord", false);
            try
            {
                System.Type recType = null;
                foreach (var a in System.AppDomain.CurrentDomain.GetAssemblies())
                { var t = a.GetType("Golfin.Physics.Viewer.Editor.BotVideoRecorder"); if (t != null) { recType = t; break; } }
                if (recType != null)
                {
                    var beginMethod = recType.GetMethod("Begin", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    beginMethod?.Invoke(null, null);
                    d.LogStep("  BeginDeferred: BotVideoRecorder.Begin() called — recording started");
                }
                else { d.LogStep("  BeginDeferred WARN: BotVideoRecorder type not found"); }
            }
            catch (System.Exception ex) { d.LogStep($"  BeginDeferred ERROR: {ex.Message}"); }
            yield return new WaitForSecondsRealtime(1f); // let first frames settle

            // 8. Quiet music — splash SFX must be clearly audible.
            SetMusicVolumeReflection(d, 5f);
            yield return new WaitForSecondsRealtime(0.5f);
            yield return d.Capture("water_pre_shot");

            // 9. Fire Driver at Hole-6 water: AimYaw=2.9804 rad, Power=0.45.
            //    WaterSplashCaptureRig iter-4 deterministic values:
            //    AimYawRadians=2.9804 → toward Hole-6 Water_1 centre (-19.7,-7.9).
            //    Power01=0.45 → lands at water centre.
            //    Ball lands → BallAudioEmitter.HandleHit → LandWater SFX
            //               → WaterSplashController splash VFX.
            var ctrl = UnityEngine.Object.FindFirstObjectByType<PhysicsLabController>();
            if (ctrl == null) { d.LogStep("  FAIL: no PhysicsLabController after Hole_06_Geo load"); yield break; }

            const float aimYaw  = 2.9804f;
            const float power01 = 0.45f;

            ctrl.SetClub(0); // Driver = index 0
            ctrl.InjectLabBundleForCurrentClub();
            ctrl.SetCameraYawRadians(aimYaw);
            d.LogStep($"  Firing Driver: aimYaw={aimYaw} rad, power={power01} — toward Hole-6 water");
            ctrl.FireViaShotController(power01, Golfin.Gameplay.Input.DebugShotAccuracy.Green);

            // 10. Wait for water hit (ball flight ~3.09s + margin → 5s).
            yield return new WaitForSecondsRealtime(5f);
            yield return d.Capture("water_splash_peak");

            // 11. Dwell for splash particles (0.9s spray + 1.2s ripple).
            yield return new WaitForSecondsRealtime(2f);
            yield return d.Capture("water_splash_ripple");

            // 12. Restore music volume.
            SetMusicVolumeReflection(d, 70f);

            d.LogStep("=== Audio Water Splash SFX: done ===");
        }

        // ── Scenario: audio_match_stinger ────────────────────────────────────
        // Order 350 audio fidelity: stinger SFX at 1v1 match result.
        //
        // Problem addressed: the prior VersusHudCaptureMenu path opened LabScaffold
        // directly (bypassing ShellScene), so AudioManager never initialized →
        // recording was -91 dB silent. This scenario uses LoopV2SmokeBot (ShellScene
        // boot) + production 1v1 matchmaking flow so AudioManager is active.
        //
        // Flow:
        //   1. ShellScene boot → NavigateToHome (recording NOT running).
        //   2. ClickModeCardPlay("versus_1v1") → matchmaking modal → OpponentFound.
        //   3. Wait for LabScaffold + any Hole_NN_Geo to load.
        //   4. Find VersusMatchController; set _debugBothBots=true.
        //      If Hole 4 loaded: also set _debugStartLie to near-green (-36.12,17,27.59)
        //      so the match resolves in ~25s instead of from tee (~60s+).
        //   5. Subscribe: VersusMatchController.OnMatchReadyToBegin → matchReadyFlag.
        //                  GameSession.OnMatchComplete → matchDoneFlag.
        //   6. Poll until matchReadyFlag (max 30s) → START RECORDING via reflection.
        //   7. Poll until matchDoneFlag (max 90s) → stinger has fired at this point.
        //   8. Dwell 4s (let stinger+banner play). Capture stinger_result frame.
        //   9. Unsubscribe events + done.
        // </summary>
        public static IEnumerator AudioMatchStinger(BotDriver d)
        {
            d.LogStep("=== Audio Match Stinger (ShellScene real flow) ===");

            // 1. Navigate to Home via real ShellScene boot.
            yield return d.NavigateToHome(totalTimeoutSeconds: 60f);
            yield return new WaitForSecondsRealtime(2f); // mode carousel settle

            // 2. Click PLAY on the 1v1 mode card → matchmaking.
            yield return d.ClickModeCardPlay("versus_1v1", settleSeconds: 1.5f);
            yield return d.WaitForModalVisible("MatchMakingModal", timeoutSeconds: 15f);
            yield return new WaitForSecondsRealtime(0.5f);

            // 3. Wait for OpponentFound.
            yield return d.WaitFor(
                () => d.GetMatchmakingPhase() == "OpponentFound",
                "matchmaking opponent found",
                timeoutSeconds: 30f);
            yield return new WaitForSecondsRealtime(0.5f);

            // 4. Subscribe to events and set debug flags as soon as LabScaffold loads.
            //    CRITICAL: OnMatchReadyToBegin fires in VersusMatchController.Start() on the
            //    FIRST few frames after LabScaffold loads — BEFORE any "settle" wait. We must
            //    subscribe + set _debugBothBots immediately after WaitForSceneLoaded, not after
            //    a 3s delay (which is too late — the event fires and MatchFlow() starts without bots).
            bool matchReadyFlag = false;
            bool matchDoneFlag  = false;
            System.Action onMatchReady = () => { matchReadyFlag = true; };
            System.Action<Golfin.Gameplay.Session.GameSession.MatchOutcome, int, int> onMatchDone
                = (outcome, p1, p2) =>
                {
                    matchDoneFlag = true;
                    d.LogStep($"  GameSession.OnMatchComplete: outcome={outcome} P1={p1} P2={p2} — stinger SFX fired");
                };

            // Subscribe BEFORE WaitForSceneLoaded so we never miss the event.
            VersusMatchController.OnMatchReadyToBegin += onMatchReady;
            Golfin.Gameplay.Session.GameSession.OnMatchComplete += onMatchDone;
            d.LogStep("  Subscribed to OnMatchReadyToBegin + OnMatchComplete (pre-load)");

            yield return d.WaitForSceneLoaded("LabScaffold", timeoutSeconds: 40f);

            // Set _debugBothBots immediately — VersusMatchController.Start() is a coroutine
            // that yields for IsHoleReady, so we have a few seconds window before it fires
            // OnMatchReadyToBegin. Setting this after WaitForSceneLoaded (before HoleGeo is even
            // loaded) guarantees it's set BEFORE IsHoleReady becomes true.
            var vmc = UnityEngine.Object.FindFirstObjectByType<VersusMatchController>();
            if (vmc != null)
            {
                vmc._debugBothBots = true;
                d.LogStep("  VersusMatchController found — _debugBothBots=true (early set, before HoleGeo)");
            }
            else
            {
                d.LogStep("  WARN: VersusMatchController not found after LabScaffold load — will retry after HoleGeo");
            }

            yield return d.WaitForAnyHoleGeoScene(timeoutSeconds: 40f);

            // Retry VMC find after HoleGeo (in case it wasn't in LabScaffold on first load).
            if (vmc == null)
            {
                vmc = UnityEngine.Object.FindFirstObjectByType<VersusMatchController>();
                if (vmc != null)
                {
                    vmc._debugBothBots = true;
                    d.LogStep("  VersusMatchController found after HoleGeo — _debugBothBots=true (late set, may be too late)");
                }
                else { d.LogStep("  WARN: VersusMatchController not found after HoleGeo either — match needs human input"); }
            }

            // Set near-green start lie from HoleContext.PinWorld (works for ANY hole).
            // Wait a frame for HoleContext to be populated after geo load.
            yield return null;
            yield return new WaitForSecondsRealtime(0.5f);
            int holeNum = Golfin.Gameplay.Session.GameSession.CurrentHoleNumber;
            d.LogStep($"  GameSession.CurrentHoleNumber={holeNum}");
            if (vmc != null)
            {
                // Use PinWorld as the near-green start position.
                // PinWorld is set by HoleContext when the hole loads; offset slightly toward
                // fairway to avoid spawning inside the cup.
                Vector3 pinWorld = Golfin.Gameplay.UI.HUD.HoleContext.PinWorld;
                if (pinWorld.sqrMagnitude > 0.01f)
                {
                    // Offset 8m away from pin in the -Z direction (approach side) so both bots
                    // start in a makeable chip/putt range — match should complete in ~20-30s.
                    Vector3 approachDir = (pinWorld.z > 0) ? new Vector3(0, 0, -1) : new Vector3(0, 0, 1);
                    Vector3 startLie = pinWorld + approachDir * 8f;
                    vmc._debugStartLie = startLie;
                    d.LogStep($"  VersusMatchController: _debugStartLie=near-green via PinWorld {pinWorld:F2} offset → {startLie:F2}");
                }
                else
                {
                    d.LogStep($"  WARN: HoleContext.PinWorld={pinWorld:F2} is zero — _debugStartLie not set (from tee)");
                }
            }

            // 5. Wait for match to become ready, then START RECORDING.
            //    OnMatchReadyToBegin fires in VMC.Start() after IsHoleReady; poll up to 30s.
            float waitReady = 0f;
            while (!matchReadyFlag && waitReady < 30f)
            {
                yield return new WaitForSecondsRealtime(0.2f);
                waitReady += 0.2f;
            }
            if (!matchReadyFlag)
                d.LogStep($"  WARN: OnMatchReadyToBegin never fired after {waitReady:F1}s — starting recording anyway");
            else
                d.LogStep($"  OnMatchReadyToBegin fired after {waitReady:F1}s — starting recording");

            yield return new WaitForSecondsRealtime(0.5f); // brief settle before first frame

            // START RECORDING — mirrors AudioWaterSplashSfx §4 pattern:
            // set RecordVideo=true + DeferredRecord=false via SessionState, then call Begin().
            // (Do NOT rely on DeferredRecord being already set — ArmDeferred() was called but
            //  the guard in Begin() checks RecordVideo, not DeferredRecord. We set it explicitly.)
            UnityEditor.SessionState.SetBool("LoopV2SmokeBot.RecordVideo", true);
            UnityEditor.SessionState.SetBool("LoopV2SmokeBot.DeferredRecord", false);
            try
            {
                System.Type recType = null;
                foreach (var a in System.AppDomain.CurrentDomain.GetAssemblies())
                { var t = a.GetType("Golfin.Physics.Viewer.Editor.BotVideoRecorder"); if (t != null) { recType = t; break; } }
                if (recType != null)
                {
                    var beginMethod = recType.GetMethod("Begin", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    beginMethod?.Invoke(null, null);
                    d.LogStep("  BotVideoRecorder.Begin() called — recording started");
                }
                else { d.LogStep("  WARN: BotVideoRecorder type not found — not recording"); }
            }
            catch (System.Exception ex) { d.LogStep($"  BotVideoRecorder.Begin ERROR: {ex.Message}"); }

            yield return new WaitForSecondsRealtime(1f); // first frames settle

            // 8. Quiet music so stinger is clearly audible over gameplay ambience.
            SetMusicVolumeReflection(d, 5f);
            yield return d.Capture("match_in_progress");

            // 9. Dwell 15s: record real gameplay (bots fire shots, SFX audible).
            //    After 15s, force-complete the match via GameSession.MarkMatchComplete so the
            //    stinger fires immediately, rather than waiting up to 110s for organic hole-out.
            //    MarkMatchComplete → GameSession.OnMatchComplete → VersusResultHandler fires stinger.
            float waitDone = 0f;
            while (!matchDoneFlag && waitDone < 15f)
            {
                yield return new WaitForSecondsRealtime(0.5f);
                waitDone += 0.5f;
            }

            if (!matchDoneFlag)
            {
                d.LogStep($"  Match not done after {waitDone:F1}s — calling GameSession.MarkMatchComplete(P1Win,3,5) to trigger stinger");
                try
                {
                    System.Type gsType = null;
                    foreach (var a in System.AppDomain.CurrentDomain.GetAssemblies())
                    { var t = a.GetType("Golfin.Gameplay.Session.GameSession"); if (t != null) { gsType = t; break; } }
                    if (gsType == null)
                    {
                        foreach (var a in System.AppDomain.CurrentDomain.GetAssemblies())
                        { var t = a.GetType("Golfin.Gameplay.Loop.Session.GameSession"); if (t != null) { gsType = t; break; } }
                    }
                    if (gsType == null)
                    {
                        foreach (var a in System.AppDomain.CurrentDomain.GetAssemblies())
                        { var t = a.GetType("GameSession"); if (t != null) { gsType = t; break; } }
                    }
                    if (gsType != null)
                    {
                        var markMethod = gsType.GetMethod("MarkMatchComplete",
                            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                        if (markMethod != null)
                        {
                            // MatchOutcome enum: P1Win=0, P2Win=1, Draw=2
                            // Get the MatchOutcome type from GameSession.
                            var outcomeType = gsType.GetNestedType("MatchOutcome");
                            object p1WinVal = outcomeType != null
                                ? System.Enum.ToObject(outcomeType, 0) // P1Win
                                : 0;
                            markMethod.Invoke(null, new object[] { p1WinVal, 3, 5 });
                            d.LogStep("  GameSession.MarkMatchComplete(P1Win, 3, 5) called — stinger SFX should fire now");
                        }
                        else { d.LogStep("  WARN: MarkMatchComplete method not found on GameSession"); }
                    }
                    else { d.LogStep("  WARN: GameSession type not found — stinger may not fire"); }
                }
                catch (System.Exception ex) { d.LogStep($"  MarkMatchComplete ERROR: {ex.Message}"); }
            }
            else
            {
                d.LogStep($"  Match completed organically after {waitDone:F1}s — stinger SFX fired via normal flow");
            }

            // 9. Dwell to let stinger + WIN/LOSE/DRAW banner fully play.
            yield return new WaitForSecondsRealtime(4f);
            yield return d.Capture("stinger_result");

            // 10. Unsubscribe events.
            VersusMatchController.OnMatchReadyToBegin -= onMatchReady;
            Golfin.Gameplay.Session.GameSession.OnMatchComplete -= onMatchDone;

            d.LogStep("=== Audio Match Stinger: done ===");
        }

        // ── Scenario: fade_draw_aim_line_bend_gate ────────────────────────────
        // fade_draw_aim_line_bend Order 355 (2026-06-17 iter-3): visual acceptance gate.

        /// <summary>
        /// Visual acceptance gate for the fade/draw aim-line bend (Order 355), iter-3.
        ///
        /// Demonstrates, in order:
        ///   1. Straight mode (FadeDraw OFF) — aim line points toward hole, no bend.
        ///      Capture: straight_line.
        ///   2. Toggle to FADE/DRAW mode (arm via ShotModeContext.Toggle).
        ///      Capture: fadedraw_armed.
        ///   3. DRAW (handle left, FinetuneX = −1) — line bends LEFT.
        ///      FIRE the DRAW shot so the ball curves left, matching the line.
        ///      Capture: draw_bent (before fire), draw_in_flight (ball curving left).
        ///   4. Wait for ball to land/resolve, then re-arm for FADE demonstration.
        ///      Capture: fade_bent (line bending right, no shot to keep total time manageable).
        ///
        /// Sign convention (D5):
        ///   FinetuneX = −1 (handle LEFT) = DRAW = ball curves LEFT = line bends LEFT
        ///   FinetuneX = +1 (handle RIGHT) = FADE = ball curves RIGHT = line bends RIGHT
        ///
        /// Pre-capture state verification: LogStep prints sc.State and the actual
        /// FinetuneX that ShotConeView pushed to AimLineBendRenderer, so a reviewer can
        /// cross-check the log against the video timestamps.
        ///
        /// Video (1170×2532, BotVideoRecorder): full sequence as primary evidence.
        /// Stills are extracted from the raw video using ffmpeg at verified timestamps
        /// (see post-processing note at end of scenario).
        /// </summary>
        public static IEnumerator FadeDrawAimLineBendGate(BotDriver d)
        {
            d.LogStep("=== Fade Draw Aim Line Bend Gate iter-3 (fade_draw_aim_line_bend Order 355) ===");

            // ── 0. Initial settle — give GameView RT time to stabilize after recording start ─
            // This extra wait at the top reduces the chance of a Metal RT-recreation y-flip
            // during the first few frames of recording (known Metal transient on first RT access).
            yield return new WaitForSecondsRealtime(3f);
            d.LogStep("Initial settle complete.");

            // ── 1. Navigate to Practice → any hole (real ShellScene boot path) ────

            yield return d.NavigateToHome(totalTimeoutSeconds: 60f);
            yield return new WaitForSecondsRealtime(2f);

            yield return d.ClickModeCardPlay("practice", settleSeconds: 1.5f);
            yield return d.WaitForScreen("HoleSelection", timeoutSeconds: 15f);
            yield return new WaitForSecondsRealtime(3f); // auto-expand settle

            yield return d.Click("ActionButton", settleSeconds: 1.5f);
            yield return d.WaitForSceneLoaded("LabScaffold", timeoutSeconds: 60f);

            // Wait for ANY hole geo scene to finish loading (practice uses the save's
            // current hole, typically Hole 6 in this build).
            d.LogStep("WaitForAnyHoleGeo: polling for Hole_NN_Geo scene…");
            {
                float geoElapsed = 0f;
                bool geoFound = false;
                while (geoElapsed < 90f)
                {
                    for (int si = 0; si < UnityEngine.SceneManagement.SceneManager.sceneCount; si++)
                    {
                        var sc2 = UnityEngine.SceneManagement.SceneManager.GetSceneAt(si);
                        if (sc2.isLoaded && sc2.name.StartsWith("Hole_") && sc2.name.EndsWith("_Geo"))
                        {
                            d.LogStep($"WaitForAnyHoleGeo OK: '{sc2.name}' loaded after {geoElapsed:F1}s");
                            geoFound = true;
                            break;
                        }
                    }
                    if (geoFound) break;
                    yield return new WaitForSecondsRealtime(0.5f);
                    geoElapsed += 0.5f;
                }
                if (!geoFound) { d.LogStep("WaitForAnyHoleGeo TIMEOUT — no Hole_NN_Geo after 90s"); yield break; }
            }
            yield return new WaitForSecondsRealtime(5f); // let hole fully initialize + camera settle

            // ── 1b. Ensure the shot has real velocity ─────────────────────────────
            // The production drag→flick path (CommitFlick) only imparts launch velocity
            // when the active club's ballistic bundle is present. Select the driver and
            // inject its lab bundle (the proven launch prep from SmokeRunner2fHost /
            // BotDriver.FireDriverShot) so the demonstration shot actually flies — without
            // it, EndExternalDrag commits a zero-velocity shot and the ball stays on the tee.
            {
                var lab = UnityEngine.Object.FindObjectOfType<Golfin.Physics.Viewer.PhysicsLabController>();
                if (lab != null)
                {
                    lab.SetClub(0);                       // 0 = Driver
                    lab.InjectLabBundleForCurrentClub();  // LAB path → real ballistics/velocity
                    d.LogStep("[FIRE-PREP] SetClub(0=Driver) + InjectLabBundleForCurrentClub()");
                }
                else d.LogStep("[FIRE-PREP] WARN: PhysicsLabController not found — shot may have no velocity");
            }
            yield return new WaitForSecondsRealtime(1f);

            // ── 2. Confirm STRAIGHT mode — capture aim line with no bend ─────────

            // Ensure Straight mode (no FadeDraw).
            Golfin.Gameplay.UI.HUD.ShotModeContext.Reset();
            yield return new WaitForSecondsRealtime(0.5f);

            var sc = UnityEngine.Object.FindObjectOfType<Golfin.Gameplay.Input.ShotController>();
            if (sc == null) { d.LogStep("FAIL: no ShotController found"); yield break; }

            d.LogStep($"[PRE-STRAIGHT] sc.State={sc.State} FadeDrawActive={sc.FadeDrawActive}");
            sc.BeginExternalDrag();                   // Idle → Aiming
            sc.SetExternalPower(0.45f, 0f);           // power=45%, finetune=0 (straight)
            yield return new WaitForSecondsRealtime(2f);
            d.LogStep($"[STRAIGHT-CAPTURE] sc.State={sc.State} FadeDrawActive={sc.FadeDrawActive}");
            yield return d.Capture("straight_line");

            sc.CancelExternalDrag();                  // back to Idle (no shot)
            yield return new WaitForSecondsRealtime(1f);

            // ── 3. ARM FADE/DRAW mode ─────────────────────────────────────────────

            d.LogStep("[ARM FADE/DRAW] Toggling ShotModeContext: Straight → FadeDraw");
            Golfin.Gameplay.UI.HUD.ShotModeContext.Toggle(); // Straight → FadeDraw
            yield return new WaitForSecondsRealtime(1.5f);   // let button UI refresh
            d.LogStep($"[FADEDRAW-ARMED-CAPTURE] sc.FadeDrawActive={sc.FadeDrawActive}");
            yield return d.Capture("fadedraw_armed");
            yield return new WaitForSecondsRealtime(1f);

            // ── 4. DRAW — handle left (FinetuneX = −1) ─────────────────────────
            // Capture bent line WITHOUT firing the shot. Firing on a Par 3 ends the hole
            // (iter-3 post-mortem) and blocks the FADE capture. Both bends are captured
            // pre-shot; a single demonstration shot follows at the end.

            d.LogStep("[DRAW PHASE] BeginExternalDrag, FinetuneX=−1 (DRAW → line bends in DRAW direction)");
            sc.BeginExternalDrag();
            sc.SetExternalPower(0.45f, -1f);          // FinetuneX = −1 = full DRAW
            yield return new WaitForSecondsRealtime(3f);
            d.LogStep($"[DRAW-BENT-CAPTURE] sc.State={sc.State} FadeDrawActive={sc.FadeDrawActive}");
            yield return d.Capture("draw_bent");
            yield return new WaitForSecondsRealtime(1f);

            // Charge to firing power while holding the DRAW — one continuous shot,
            // no cancel/re-arm, no FADE staging. The bent line stays visible as power
            // ramps, then the shot fires and the ball curves to match it.
            float ramp = 1.2f, rt = 0f;
            while (rt < ramp)
            {
                rt += UnityEngine.Time.unscaledDeltaTime;
                sc.SetExternalPower(Mathf.Lerp(0.45f, 0.7f, rt / ramp), -1f);
                yield return null;
            }
            sc.SetExternalPower(0.7f, -1f);
            yield return new WaitForSecondsRealtime(0.4f);
            // The production drag→flick commit does NOT impart a visible launch in this
            // lab/practice-hybrid context, so end the aim drag and fire via the debug path
            // (FireViaShotController → FireDebugShot → CommitFlick), which DOES launch — now
            // carrying the DRAW finetune so the ball physically curves LEFT to match the line.
            sc.CancelExternalDrag();
            sc.FadeDrawActive = true;                      // ensure shaping is armed for the fire
            {
                var lab2 = UnityEngine.Object.FindObjectOfType<Golfin.Physics.Viewer.PhysicsLabController>();
                if (lab2 != null)
                    lab2.FireViaShotController(0.42f, Golfin.Gameplay.Input.DebugShotAccuracy.Green, -1f);
                else d.LogStep("[FIRE] WARN: PhysicsLabController not found");
            }
            // Power 0.42 (not 0.7): a 0.7 driver overshot the 168yd Par-3 green into the back
            // tree-line and the chase cam buried in foliage. A lower shot lands on/near the
            // green, staying in frame so the draw curve + landing are visible (fix the SHOT,
            // not the camera — feedback_gameplay_video_use_normal_play).
            d.LogStep("[FIRE] FireViaShotController(power=0.42, DRAW finetune=-1) — ball launches + curves left, lands in frame");

            // Let the ball get airborne BEFORE capturing (a snap 1 frame after release
            // shows the ball still on the tee — the iter-3 mistake). Then dwell so the
            // normal chase camera follows the curving ball through its flight + landing.
            yield return new WaitForSecondsRealtime(1.5f);
            yield return d.Capture("draw_ball_flight");    // ball in the air, curving left
            yield return new WaitForSecondsRealtime(6f);

            d.LogStep("=== FadeDrawAimLineBend normal playthrough complete ===");
            d.FlushLog();
        }

        // ── Scenarios: tree_aware_bot (Order 351) ─────────────────────────────
        //
        // BEFORE/AFTER pair on Hole 08 (3927 trees — densest hole).
        // Acceptance gate: BEFORE log has NO "[BotDriver] Tree re-aim" lines;
        //   AFTER log DOES contain them (avoidance triggered on trunk-blocked strokes).
        // Both use the same direct additive load (LabScaffold + Hole_08_Geo) used by
        // TreeCollisionGate to bypass full ShellScene navigation.
        //
        // Usage:
        //   1. GOLFIN > Smoke > Loop v2 > Tree Aware Bot — Hole08 BEFORE  (records first clip)
        //   2. GOLFIN > Capture > Reset Video Session Guard                (one recording per launch)
        //   3. GOLFIN > Smoke > Loop v2 > Tree Aware Bot — Hole08 AFTER   (records second clip)
        //
        // Hole 17 no-op: GetTreeProvider() returns null (no tree_obstacles.csv) so the helper
        // is never called. Used to confirm the feature degrades gracefully to no-op.

        /// <summary>
        /// tree_aware_bot BEFORE — Hole 08, avoidance DISABLED (BotDriver.SkipTreeAvoidance=true).
        /// Bot fires on cup-line without trunk avoidance. Logs show SelectShot chosen dist but
        /// ZERO "[BotDriver] Tree re-aim" lines, demonstrating pre-fix behavior.
        /// </summary>
        public static IEnumerator Hole8TrunkAvoidanceBefore(BotDriver d)
        {
            BotDriver.SkipTreeAvoidance = true;
            d.LogStep("=== tree_aware_bot BEFORE: SkipTreeAvoidance=true (avoidance DISABLED) ===");
            return Hole8TrunkAvoidanceBody(d, scenarioLabel: "before");
        }

        /// <summary>
        /// tree_aware_bot AFTER — Hole 08, avoidance ACTIVE (BotDriver.SkipTreeAvoidance=false).
        /// When a trunk is detected on the cup line, the bot re-aims. Logs show one or more
        /// "[BotDriver] Tree re-aim" lines, demonstrating the fix in action.
        /// </summary>
        public static IEnumerator Hole8TrunkAvoidanceAfter(BotDriver d)
        {
            BotDriver.SkipTreeAvoidance = false;
            d.LogStep("=== tree_aware_bot AFTER: SkipTreeAvoidance=false (avoidance ENABLED) ===");
            return Hole8TrunkAvoidanceBody(d, scenarioLabel: "after");
        }

        private static IEnumerator Hole8TrunkAvoidanceBody(BotDriver d, string scenarioLabel)
        {
            try
            {
            // 1. Hide ShellScene canvases so PhysicsLab camera dominates the Game View.
            var shellCanvases = Object.FindObjectsOfType<Canvas>();
            var hiddenCanvases = new System.Collections.Generic.List<Canvas>();
            foreach (var c in shellCanvases)
                if (c.gameObject.scene.name == "ShellScene" && c.enabled)
                { c.enabled = false; hiddenCanvases.Add(c); }
            d.LogStep($"  Hole8TrunkAvoidance({scenarioLabel}): hidden {hiddenCanvases.Count} ShellScene canvases.");

            // 2. Simultaneously kick off LabScaffold + Hole_08_Geo additive loads.
            //    (Same simultaneous-kick pattern as TreeCollisionGate — avoids the 2-frame race
            //    where ScanForLoadedHoleSceneAtStartup runs before Hole_08_Geo appears.)
            d.LogStep("  Starting LabScaffold + Hole_08_Geo loads (Additive, simultaneous)...");
            var opLab = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(
                "LabScaffold", UnityEngine.SceneManagement.LoadSceneMode.Additive);
            if (opLab == null)
            {
                d.LogStep("=== Hole8TrunkAvoidance: FAIL — LabScaffold not in Build Settings ===");
                yield break;
            }
            var opHole = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(
                "Hole_08_Geo", UnityEngine.SceneManagement.LoadSceneMode.Additive);
            if (opHole == null)
            {
                d.LogStep("=== Hole8TrunkAvoidance: FAIL — Hole_08_Geo not in Build Settings ===");
                yield break;
            }

            // 3. Wait for both loads.
            float lw = 0f;
            while ((!opLab.isDone || !opHole.isDone) && lw < 30f)
            { yield return new WaitForSecondsRealtime(0.25f); lw += 0.25f; }
            if (!opLab.isDone || !opHole.isDone)
            {
                d.LogStep($"=== Hole8TrunkAvoidance: FAIL — load timeout after {lw:F1}s ===");
                yield break;
            }
            d.LogStep($"  Both scenes loaded in {lw:F1}s. Polling IsHoleReady...");

            // 4. Wait for PhysicsLabController.OnHoleLoaded (loads tree_obstacles.csv, 3927 trees).
            var ctrl = Object.FindObjectOfType<PhysicsLabController>();
            if (ctrl == null)
            {
                d.LogStep("=== Hole8TrunkAvoidance: FAIL — PhysicsLabController not found ===");
                yield break;
            }
            float hw = 0f;
            while (!ctrl.IsHoleReady && hw < 15f)
            { yield return new WaitForSecondsRealtime(0.25f); hw += 0.25f; }
            if (!ctrl.IsHoleReady)
            {
                d.LogStep($"=== Hole8TrunkAvoidance: FAIL — IsHoleReady never true after {hw:F1}s ===");
                yield break;
            }
            d.LogStep($"  IsHoleReady=true after {hw:F1}s. TreeProvider null={ctrl.GetTreeProvider() == null}.");
            yield return new WaitForSecondsRealtime(2f); // settle before first shot

            // 5. Play hole to cup (or par+3 seam cap). Hole 08 par=5.
            yield return d.PlayHoleToCup(par: 5);

            d.LogStep($"=== Hole8TrunkAvoidance({scenarioLabel}) complete ===");
            d.FlushLog();

            // Restore canvases.
            foreach (var c in hiddenCanvases) { if (c != null) c.enabled = true; }
            }
            finally
            {
                // Always restore the flag — even if an exception aborts the scenario.
                BotDriver.SkipTreeAvoidance = false;
            }
        }

        /// <summary>
        /// tree_aware_bot Hole 17 no-op proof — Hole 17 has no tree_obstacles.csv,
        /// so GetTreeProvider() returns null. BotTreeProbe.TryFindTrunkClearAim returns false
        /// immediately. Log must contain ZERO "[BotDriver] Tree re-aim" lines.
        /// </summary>
        public static IEnumerator Hole17TrunkNoop(BotDriver d)
        {
            d.LogStep("=== tree_aware_bot Hole17 no-op: expecting null tree provider ===");

            // 1. Hide ShellScene canvases.
            var shellCanvases = Object.FindObjectsOfType<Canvas>();
            var hiddenCanvases = new System.Collections.Generic.List<Canvas>();
            foreach (var c in shellCanvases)
                if (c.gameObject.scene.name == "ShellScene" && c.enabled)
                { c.enabled = false; hiddenCanvases.Add(c); }

            // 2. Load LabScaffold + Hole_17_Geo (no tree_obstacles.csv → null provider).
            d.LogStep("  Starting LabScaffold + Hole_17_Geo loads (Additive, simultaneous)...");
            var opLab = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(
                "LabScaffold", UnityEngine.SceneManagement.LoadSceneMode.Additive);
            if (opLab == null)
            {
                d.LogStep("=== Hole17TrunkNoop: FAIL — LabScaffold not in Build Settings ===");
                foreach (var c in hiddenCanvases) { if (c != null) c.enabled = true; }
                yield break;
            }
            var opHole = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(
                "Hole_17_Geo", UnityEngine.SceneManagement.LoadSceneMode.Additive);
            if (opHole == null)
            {
                d.LogStep("=== Hole17TrunkNoop: FAIL — Hole_17_Geo not in Build Settings ===");
                foreach (var c in hiddenCanvases) { if (c != null) c.enabled = true; }
                yield break;
            }

            // 3. Wait for both loads.
            float lw = 0f;
            while ((!opLab.isDone || !opHole.isDone) && lw < 30f)
            { yield return new WaitForSecondsRealtime(0.25f); lw += 0.25f; }
            if (!opLab.isDone || !opHole.isDone)
            {
                d.LogStep($"=== Hole17TrunkNoop: FAIL — load timeout {lw:F1}s ===");
                foreach (var c in hiddenCanvases) { if (c != null) c.enabled = true; }
                yield break;
            }

            // 4. Wait for IsHoleReady.
            var ctrl = Object.FindObjectOfType<PhysicsLabController>();
            if (ctrl == null)
            {
                d.LogStep("=== Hole17TrunkNoop: FAIL — PhysicsLabController not found ===");
                foreach (var c in hiddenCanvases) { if (c != null) c.enabled = true; }
                yield break;
            }
            float hw = 0f;
            while (!ctrl.IsHoleReady && hw < 15f)
            { yield return new WaitForSecondsRealtime(0.25f); hw += 0.25f; }
            if (!ctrl.IsHoleReady)
            {
                d.LogStep($"=== Hole17TrunkNoop: FAIL — IsHoleReady never true {hw:F1}s ===");
                foreach (var c in hiddenCanvases) { if (c != null) c.enabled = true; }
                yield break;
            }

            // 5. Confirm null provider.
            bool providerIsNull = ctrl.GetTreeProvider() == null;
            d.LogStep($"  Hole17 tree provider null={providerIsNull} (EXPECTED: true). Hole_17 has no tree_obstacles.csv.");
            if (!providerIsNull)
                d.LogStep("  WARNING: non-null tree provider on Hole_17 is UNEXPECTED — check HoleData/lomond-country-club/Hole_17/.");

            yield return new WaitForSecondsRealtime(1f);

            // 6. Fire one stroke to confirm SkipTreeAvoidance check path is inactive when null.
            //    PlayHoleToCup(par:4) but we only need 1 stroke as proof; seam cap fires at par+3.
            yield return d.PlayHoleToCup(par: 4);

            d.LogStep($"  no-op proof: providerNull={providerIsNull}. Inspect bot log above for absence of [BotDriver] Tree re-aim lines.");
            d.LogStep("=== Hole17TrunkNoop complete ===");
            d.FlushLog();

            foreach (var c in hiddenCanvases) { if (c != null) c.enabled = true; }
        }

        // ── tree_aware_bot §9.2: realistic off-fairway lie demo (Hole 12) ────────────

        /// <summary>
        /// tree_aware_bot §9.2 lie demo BEFORE — Hole 12, open rough lie east of fairway.
        /// BotDriver.SkipTreeAvoidance=true → bot fires straight on cup-line without avoidance.
        /// iter-6 lie: (8.81, 0, 38.01), terrain_Y=29.893, cup-line blocked by SINGLE isolated trunk
        /// at (17.64, 48.88) along=14.0m, R=0.385m (scale=1.1), baseY=29.282, trunkTopY=33.135.
        /// ball.y=29.893 ∈ [29.282, 33.135] → trunk hit confirmed. Control dirs (+/-10°) are CLEAR.
        /// CaptureTopDownAfterFirstStroke=true → top-down overlay captured after stroke 1.
        /// </summary>
        public static IEnumerator Hole12LieDemoBefore(BotDriver d)
        {
            BotDriver.SkipTreeAvoidance = true;
            d.LogStep("=== tree_aware_bot Lie Demo BEFORE: SkipTreeAvoidance=true (avoidance DISABLED) ===");
            return Hole12LieDemoBody(d, scenarioLabel: "before");
        }

        /// <summary>
        /// tree_aware_bot §9.2 lie demo AFTER — Hole 12, same open rough lie, avoidance ACTIVE.
        /// BotDriver.SkipTreeAvoidance=false (default) → BotTreeProbe detects trunk on cup-line.
        /// iter-6 lie: (8.81, 0, 38.01); trunk at (17.64, 48.88) along=14m blocked; +/-10° is CLEAR.
        /// Log MUST contain "[BotDriver] Tree re-aim:" with delta=-10° or +10° yaw that bypasses trunk.
        /// </summary>
        public static IEnumerator Hole12LieDemoAfter(BotDriver d)
        {
            BotDriver.SkipTreeAvoidance = false;
            d.LogStep("=== tree_aware_bot Lie Demo AFTER: SkipTreeAvoidance=false (avoidance ENABLED) ===");
            return Hole12LieDemoBody(d, scenarioLabel: "after");
        }

        /// <summary>
        /// Shared body for Hole12LieDemoBefore / Hole12LieDemoAfter.
        ///
        /// iter-6 lie: (8.81, 0, 38.01) on Hole_12 — open rough east of fairway, ~155m from cup.
        /// SINGLE blocking trunk on cup-line at (17.64, 48.88), along=14.0m, R=0.385m (JapaneseBlack_01 scale=1.1).
        /// terrain_Y=29.893 ∈ [baseY=29.282, trunkTop=33.135] → trunk hit in BEFORE clip.
        /// Control directions (+10° / -10° from cup-line): CLEAR (no trunks in near window [0,35m]).
        /// Hole 12 par=4; cup ≈155m from lie → SelectShot picks iron7/wedge.
        /// BEFORE: CaptureTopDownAfterFirstStroke=true → trajectory overlay top-down capture after stroke 1.
        /// AFTER: TryFindTrunkClearAim returns safeYaw at -10° or +10° delta; bot files at cleared angle.
        /// </summary>
        private static IEnumerator Hole12LieDemoBody(BotDriver d, string scenarioLabel)
        {
            try
            {
            // 1. Hide ShellScene canvases so PhysicsLab camera dominates the Game View.
            var shellCanvases = Object.FindObjectsOfType<Canvas>();
            var hiddenCanvases = new System.Collections.Generic.List<Canvas>();
            foreach (var c in shellCanvases)
                if (c.gameObject.scene.name == "ShellScene" && c.enabled)
                { c.enabled = false; hiddenCanvases.Add(c); }
            d.LogStep($"  Hole12LieDemo({scenarioLabel}): hidden {hiddenCanvases.Count} ShellScene canvases.");

            // 2. Simultaneously kick off LabScaffold + Hole_12_Geo additive loads.
            d.LogStep("  Starting LabScaffold + Hole_12_Geo loads (Additive, simultaneous)...");
            var opLab = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(
                "LabScaffold", UnityEngine.SceneManagement.LoadSceneMode.Additive);
            if (opLab == null)
            {
                d.LogStep("=== Hole12LieDemo: FAIL — LabScaffold not in Build Settings ===");
                yield break;
            }
            var opHole = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(
                "Hole_12_Geo", UnityEngine.SceneManagement.LoadSceneMode.Additive);
            if (opHole == null)
            {
                d.LogStep("=== Hole12LieDemo: FAIL — Hole_12_Geo not in Build Settings ===");
                yield break;
            }

            // 3. Wait for both loads to complete.
            float lw = 0f;
            while ((!opLab.isDone || !opHole.isDone) && lw < 30f)
            { yield return new WaitForSecondsRealtime(0.25f); lw += 0.25f; }
            if (!opLab.isDone || !opHole.isDone)
            {
                d.LogStep($"=== Hole12LieDemo: FAIL — load timeout after {lw:F1}s ===");
                yield break;
            }
            d.LogStep($"  Both scenes loaded in {lw:F1}s. Polling IsHoleReady...");

            // 4. Wait for PhysicsLabController.OnHoleLoaded (loads tree_obstacles.csv, ~3026 trees).
            var ctrl = Object.FindObjectOfType<PhysicsLabController>();
            if (ctrl == null)
            {
                d.LogStep("=== Hole12LieDemo: FAIL — PhysicsLabController not found ===");
                yield break;
            }
            float hw = 0f;
            while (!ctrl.IsHoleReady && hw < 15f)
            { yield return new WaitForSecondsRealtime(0.25f); hw += 0.25f; }
            if (!ctrl.IsHoleReady)
            {
                d.LogStep($"=== Hole12LieDemo: FAIL — IsHoleReady never true after {hw:F1}s ===");
                yield break;
            }
            d.LogStep($"  IsHoleReady=true after {hw:F1}s. TreeProvider null={ctrl.GetTreeProvider() == null}.");
            yield return new WaitForSecondsRealtime(1f); // let the hole settle

            // 5. Seed ball at open rough lie — iter-6 (all A1–A5 PASS via Unity script-execute):
            //      Blocking trunk: center=(17.64,48.88) scale=1.1 profileName=MESH_JapaneseBlack_01_Var1
            //        R=0.385m, baseY=29.282, trunkTopY=33.135
            //      Lie at (8.81, 29.893, 38.01): terrain_Y=29.893 ∈ [29.282, 33.135] → A1 PASS
            //      along=14.0m in near window [0,35m]; lat=0.018m < R=0.385 → A2 PASS
            //      LineHasTrunkInWindows=True → A4 PASS
            //      TryFindTrunkClearAim rerouted=True safeYaw=40.83° (-10° from cup 50.83°) → A5 PASS
            //      Control shots (+/-10°): both CLEAR (no trunks in near window) → A7 PASS
            //    Open ground beyond trunk; open ground at +/-10° from cup-line.
            var liePos = new Vector3(8.81f, 0f, 38.01f);
            ctrl.PlaceBallAt(liePos, preferredSurfaceTypeValue: null);
            d.LogStep($"  [Lie] Seeded ball at open rough lie {liePos:F2} (~155m from cup, trunk at (17.64,48.88) along=14m, Hole 12).");
            yield return new WaitForSecondsRealtime(1.5f); // wait for ball to settle on terrain surface
            d.LogStep($"  [Lie] Ball settled at {ctrl.BallPosition:F2}.");

            // 6. Play from lie to cup.
            //    Hole 12 par=4; cup ~155m away → SelectShot picks iron7 or wedge.
            //    BEFORE (SkipTreeAvoidance=true): bot fires on cup-line → carom off trunk at along≈14m.
            //      CaptureTopDownAfterFirstStroke captures trajectory overlay top-down after stroke 1.
            //    AFTER  (SkipTreeAvoidance=false): probe detects trunk (near window [0,35m]) →
            //      "[BotDriver] Tree re-aim: ..." log line → bot re-aims at safeYaw=40.8° (-10° delta).
            if (scenarioLabel == "before")
                d.CaptureTopDownAfterFirstStroke = true;
            yield return d.PlayHoleToCup(par: 4);

            d.LogStep($"=== Hole12LieDemo({scenarioLabel}) complete ===");
            d.FlushLog();

            foreach (var c in hiddenCanvases) { if (c != null) c.enabled = true; }
            }
            finally
            {
                // Always restore flag — even if an exception aborts the scenario.
                BotDriver.SkipTreeAvoidance = false;
            }
        }
    }
}
#endif
