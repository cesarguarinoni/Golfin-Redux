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
// Stage 3: scale+fade pop-in on ShowResult() — subtle 0.9→1.0 scale over 0.2s ease-out,
//          layered on ModalController's existing CanvasGroup fade. Implemented via coroutine
//          (project standard — no DOTween dependency in this project).
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

        // ── Stage 3: pop-in tween state ───────────────────────────────────────
        private Coroutine? _popInCoroutine;
        private const float PopInDuration   = 0.20f;  // seconds — within 0.15–0.25 spec
        private const float PopInStartScale = 0.9f;   // 90% → 100%

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
        /// Stage 3: triggers the scale+fade pop-in on modalPanel after base.Show().
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

            base.Show();

            // Stage 3: kick off scale pop-in on the modalPanel RectTransform.
            // base.Show() already starts the CanvasGroup fade-in; we add a complementary
            // scale 0.9→1.0 that is independent (different property) so they don't fight.
            // Kill any prior coroutine (e.g. re-open before previous tween completes).
            if (modalPanel != null)
            {
                if (_popInCoroutine != null)
                {
                    StopCoroutine(_popInCoroutine);
                    _popInCoroutine = null;
                    // Ensure panel is at final scale even if interrupted
                    modalPanel.transform.localScale = Vector3.one;
                }
                _popInCoroutine = StartCoroutine(PopInScaleRoutine());
            }
        }

        public override void Show()
        {
            // No-op: callers must use ShowResult() so live data is bound first.
        }

        public override void Hide()
        {
            // Kill any running pop-in before hiding so the next open starts fresh.
            if (_popInCoroutine != null)
            {
                StopCoroutine(_popInCoroutine);
                _popInCoroutine = null;
                // Ensure modalPanel is at final scale even if hidden mid-tween.
                if (modalPanel != null)
                    modalPanel.transform.localScale = Vector3.one;
            }
            base.Hide();
        }

        // ── Stage 3: Pop-in scale coroutine ──────────────────────────────────

        /// <summary>
        /// Animate modalPanel.localScale from PopInStartScale (0.9) → 1.0 over PopInDuration (0.2s)
        /// using an ease-out cubic curve. Runs concurrently with ModalController's CanvasGroup fade-in.
        /// Sets final scale to Vector3.one on completion so re-open is always clean.
        /// </summary>
        private IEnumerator PopInScaleRoutine()
        {
            if (modalPanel == null) yield break;

            var rt = modalPanel.transform;
            // Start at 90% scale
            rt.localScale = new Vector3(PopInStartScale, PopInStartScale, 1f);

            float elapsed = 0f;
            while (elapsed < PopInDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / PopInDuration);
                // Ease-out cubic: t' = 1 - (1-t)^3
                float tEased = 1f - Mathf.Pow(1f - t, 3f);
                float s = Mathf.Lerp(PopInStartScale, 1f, tEased);
                rt.localScale = new Vector3(s, s, 1f);
                yield return null;
            }

            // Guarantee final state even if a frame slipped
            rt.localScale = Vector3.one;
            _popInCoroutine = null;
            Debug.Log("[VersusResultModalController] Pop-in scale complete.");
        }

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
