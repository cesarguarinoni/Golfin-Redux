// ─────────────────────────────────────────────────────────────────────────────
// VersusResultModalController  —  Stage 1 + Stage 2 + Stage 3
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
// Stage 2: receives List<HoleReward> from VersusResultHandler and passes to screen controller.
// Stage 3: scale+fade pop-in on ShowResult() — subtle 0.9→1.0 scale over 0.2s ease-out.
//
// game_polish_b §D2 RETROFIT. That pop is no longer this file's: `animateShow` is set on
// the scene object and ModalController.Show() runs UiMotion.Pop, which is the SAME curve —
// 0.9 → 1.0 over 0.20 s on `1 - (1-t)^3` — from the same constants (UiMotion.PopDur 0.20,
// fromScale 0.9). Proven frame by frame rather than asserted: RetrofitParityRecorder logs
// modalPanel.localScale.x on a fixed 1/60 clock before and after, and the two logs agree to
// within the §D2 gate of 0.005 per frame. See Docs/Diagnostics/_capture/game_polish_b_retrofit_*.json.
//
// What DID change, and it is the one thing to look at: the alpha. The old pop ran the scale
// coroutine ALONGSIDE ModalController's legacy FadeIn, which was a LINEAR alpha over 0.2 s;
// UiMotion.Pop drives the alpha on the same ease-out cubic as the scale. The panel therefore
// reaches full opacity slightly sooner. The §D2 gate is on scale, this is inside a 0.2 s
// window, and one curve for both properties is the point of the retrofit — flagged in the
// report as deviation D-2 rather than left for a reviewer to find.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Golfin.Gameplay.Session;
using Golfin.Gameplay.UI.HUD;
using Golfin.UI.GameplayTransition;
using Golfin.UI.Modals;
using Golfin.UI;
using GolfinRedux.UI;

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
        /// Stage 2: rewardList comes from modes.csv via ModesDatabaseCSV.
        /// Stage 3 / §D2: base.Show() pops the panel in — this method no longer owns any tween.
        /// </summary>
        public void ShowResult(
            GameSession.MatchOutcome outcome,
            MatchContext.Player      localPlayer,
            MatchContext.Player      opponentPlayer,
            int                      holeNumber,
            List<HoleReward>?        rewardList = null)
        {
            if (_screen != null)
            {
                _screen.ShowResult(outcome, localPlayer, opponentPlayer, holeNumber, rewardList, OnNewMatch);
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

            // §D2 — the pop lives in ModalController now, behind `animateShow`, and it is
            // interruption-safe there in a way this file's coroutine never was: a re-open
            // during the previous tween SETTLES the old one on Vector3.one instead of merely
            // stopping it, so the panel can no longer be stranded at 0.94.
            base.Show();
        }

        public override void Show()
        {
            // No-op: callers must use ShowResult() so live data is bound first.
        }

        // Hide() is not overridden any more: §D2 removed the pop-in coroutine this override
        // existed to stop, and ModalController.Hide already settles the Unpop on Vector3.one.

        // ── NEW MATCH (D3) ────────────────────────────────────────────────────

        private void OnNewMatch()
        {
            Hide();
            StartCoroutine(NewMatchRoutine());
        }

        private IEnumerator NewMatchRoutine()
        {
            // Small frame gap so Hide() completes before the curtain drops.
            yield return null;

            // Tear gameplay down behind the curtain and land on Home before re-queueing.
            // Two things this fixes over the old unload-then-Open: the unload takes several
            // frames with nothing left to render (the bare shell scene was on show), and
            // nobody ever swapped the screen — so the matchmaking modal re-opened over that
            // emptiness instead of over Home, which is where a player opens it from.
            if (GameplaySceneLoader.Instance != null)
            {
                yield return GameplaySceneLoader.Instance.ExitToScreen(
                    ScreenId.Home,
                    () => GameSession.IsVersus = false);   // reset while the screen is black
                Debug.Log("[VersusResultModalController] Gameplay unloaded for NEW MATCH re-queue.");
            }
            else
            {
                Debug.LogWarning("[VersusResultModalController] GameplaySceneLoader.Instance is null.");
                GameSession.IsVersus = false;
            }

            // Re-open matchmaking to re-queue versus_1v1 (D3) — after the reveal, so the
            // queue animation plays in front of the player exactly as it does from Home.
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
