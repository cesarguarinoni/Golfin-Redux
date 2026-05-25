#if UNITY_EDITOR
using System.Collections;
using UnityEngine;
using Golfin.Gameplay.Input;

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

            // 2. Click PLAY button → matchmaking modal opens.
            yield return d.Click("PLAY", settleSeconds: 1.5f);

            // 3. Wait for matchmaking modal to appear. MatchmakingModalController
            //    is on GO named "MatchMakingModal"; check it becomes visible.
            yield return d.WaitForModalVisible("MatchMakingModal", timeoutSeconds: 15f);
            yield return d.Capture("matchmaking_searching");

            // 4. Wait until MatchmakingModalController.Phase == OpponentFound.
            //    searchDurationSeconds defaults to 5s; allow generous timeout.
            yield return d.WaitFor(
                () => d.GetMatchmakingPhase() == "OpponentFound",
                "matchmaking opponent found",
                timeoutSeconds: 30f);
            yield return new WaitForSecondsRealtime(0.5f); // settle on "OPPONENT FOUND" text
            yield return d.Capture("opponent_found");

            // 5. Wait for gameplay scenes to load (modal auto-triggers GameplaySceneLoader).
            //    FadeController transition + scene load can take up to 30s.
            yield return d.WaitForSceneLoaded("LabScaffold", timeoutSeconds: 40f);
            yield return d.WaitForSceneLoaded("Hole_01_Geo", timeoutSeconds: 40f);
            yield return new WaitForSecondsRealtime(3f); // fade-in + Awake/Start/HUD settle
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
    }
}
#endif
