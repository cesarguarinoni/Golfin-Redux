#if UNITY_EDITOR
using System.Collections;
using UnityEngine;

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
    }
}
#endif
