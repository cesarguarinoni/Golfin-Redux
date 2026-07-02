// ─────────────────────────────────────────────────────────────────────────────
// VersusResultModalController  —  Stage 1
//
// ShellScene-resident ModalController subclass that presents VersusResultScreen
// after a 1v1 match ends.  Mirrors HoleCompleteModalController:
//   • Canvas.overrideSorting = true, sortingOrder = 901 (above HoleComplete 900)
//   • GraphicRaycaster added in Awake
//   • Show()/Hide() delegate to the child panel / VersusResultScreenController
//
// D3 NEW MATCH: hides this modal → unloads gameplay → re-opens MatchmakingModalController
// D4 pattern  : modal is a ModalController subclass, not a ScreenManager screen
//
// Reward row stays placeholder until Stage 2 — no reward binding here.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Golfin.Gameplay.Session;
using Golfin.Gameplay.UI.HUD;
using Golfin.UI.GameplayTransition;
using Golfin.UI.Modals;
using Golfin.UI;

namespace Golfin.UI.Matchmaking
{
    /// <summary>
    /// Hosts <see cref="VersusResultScreenController"/> inside a ModalController shell.
    /// Called by <see cref="VersusResultHandler"/> after the match banner sequence.
    /// </summary>
    public class VersusResultModalController : ModalController
    {
        [Header("Screen controller (child of modalPanel)")]
        [SerializeField] private VersusResultScreenController _screen = null!;

        [Header("Matchmaking modal reference (for NEW MATCH / D3 re-queue)")]
        [SerializeField] private MatchmakingModalController _matchmakingModal = null!;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        protected override void Awake()
        {
            base.Awake();

            // Canvas sorting: above HoleCompleteModalController (900), below LoadingScreen (1000).
            var canvas = GetComponent<Canvas>();
            if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder    = 901;

            if (GetComponent<GraphicRaycaster>() == null)
                gameObject.AddComponent<GraphicRaycaster>();
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Populate the result screen from live MatchContext + LeaderboardManager, then show.
        /// Called by VersusResultHandler after the banner delay.
        /// </summary>
        public void ShowResult(
            GameSession.MatchOutcome outcome,
            MatchContext.Player      localPlayer,
            MatchContext.Player      opponentPlayer,
            int                      holeNumber)
        {
            if (_screen != null)
            {
                _screen.ShowResult(outcome, localPlayer, opponentPlayer, holeNumber, OnNewMatch);
            }
            else
            {
                Debug.LogWarning("[VersusResultModalController] _screen is null — wire VersusResultScreenController.");
            }

            // Restore persistent Shell chrome (TopBar + bottom nav) so they are visible
            // over the post-match backdrop — matches the Figma reference where both bars
            // are present.  Mirrors the bar-restore that ScreenManager performs when
            // navigating back to Home after a hole.
            if (PersistentUIManager.Instance != null)
                PersistentUIManager.Instance.ShowBars();

            base.Show();
        }

        public override void Show()
        {
            // No-op: callers must use ShowResult() so live data is bound first.
        }

        public override void Hide()
        {
            base.Hide();
        }

        // ── NEW MATCH (D3) ────────────────────────────────────────────────────

        private void OnNewMatch()
        {
            Hide();
            StartCoroutine(NewMatchRoutine());
        }

        private IEnumerator NewMatchRoutine()
        {
            // Small frame gap so Hide() completes before unload.
            yield return null;

            // Unload gameplay → return to ShellScene.
            if (GameplaySceneLoader.Instance != null)
            {
                yield return StartCoroutine(GameplaySceneLoader.Instance.UnloadGameplay());
                Debug.Log("[VersusResultModalController] Gameplay unloaded for NEW MATCH re-queue.");
            }
            else
            {
                Debug.LogWarning("[VersusResultModalController] GameplaySceneLoader.Instance is null.");
            }

            // Reset session flag so re-entry works cleanly.
            GameSession.IsVersus = false;

            // Re-open matchmaking to re-queue versus_1v1 (D3).
            if (_matchmakingModal != null)
            {
                _matchmakingModal.Open();
                Debug.Log("[VersusResultModalController] MatchmakingModalController.Open() called (D3 re-queue).");
            }
            else
            {
                Debug.LogWarning("[VersusResultModalController] _matchmakingModal is null — cannot re-queue matchmaking.");
            }
        }
    }
}
