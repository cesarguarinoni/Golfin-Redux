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
        /// gameplay scenes to load → fire putt toward cup → wait for InCup →
        /// result modal appears. This is the default visual gate for Stage C1.
        ///
        /// Captures: home, matchmaking_searching, opponent_found, gameplay_armed,
        ///           ball_in_cup, result_modal.
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
            yield return new WaitForSecondsRealtime(4f); // let Awake/Start settle
            yield return d.Capture("gameplay_armed");

            // 6. Find cup position and fire a putt.
            Vector3 cupPos = d.FindCupPosition();
            yield return d.FireShot(cupPos, power01: 0.65f, timeoutSeconds: 35f);

            // 7. Wait for InCup (or any terminal state).
            yield return d.WaitForBallState("InCup", timeoutSeconds: 35f);
            yield return d.Capture("ball_in_cup");

            // 8. Wait for result modal (HoleCompleteWidget animates in).
            yield return new WaitForSecondsRealtime(3f);
            yield return d.Capture("result_modal");

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
        /// Home → Hole Selection (bottom-nav) → collapse Hole 1 card → back to Home.
        /// Smoke test for Stage E's hole-selection entry point.
        ///
        /// Design note (iter-2): Only Hole 1 is unlocked and auto-expands on screen open.
        /// There is no collapsed→expanded transition to drive (Holes 2-4 are LOCKED).
        /// This scenario instead drives the COLLAPSE of the already-expanded Hole 1 card
        /// by clicking "CardTapButton" — the toggle-expand button on HoleCard(Clone) that
        /// is separate from the PLAY/REPLAY action button. This exercises real UI state
        /// change (expanded → collapsed) even though only one hole is unlocked.
        /// When Stage E unlocks more holes, this scenario can be updated to drive
        /// a collapsed→expanded transition on a different hole card.
        ///
        /// Captures: home, hole_selection_expanded, hole_selection_collapsed, home_returned.
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
            // Hole 1 is already expanded on screen open — capture this initial state.
            yield return d.Capture("hole_selection_expanded");

            // 4. Click the CardTapButton on the expanded Hole 1 card to collapse it.
            //    "CardTapButton" is the toggle-expand button child of HoleCard(Clone)
            //    (HoleCardController.cs:68 — [SerializeField] private Button cardTapButton).
            //    It is a standard UnityEngine.UI.Button, interactable on unlocked cards.
            yield return d.Click("CardTapButton", settleSeconds: 1.0f);
            yield return new WaitForSecondsRealtime(0.5f); // collapse animation
            yield return d.Capture("hole_selection_collapsed");

            // 5. Navigate back to Home. Bottom-nav home button is "NavHomeButton".
            yield return d.Click("NavHomeButton", settleSeconds: 1.0f);

            // 6. Confirm Home screen.
            yield return d.WaitForScreen("Home", timeoutSeconds: 10f);
            yield return d.Capture("home_returned");

            d.LogStep("=== Hole Selection Browse: all captures done ===");
        }
    }
}
#endif
