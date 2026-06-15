using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Golfin.UI;
using Golfin.UI.Matchmaking;
using Golfin.Gameplay.Session;

namespace GolfinRedux.UI.ModeSelect
{
    /// <summary>
    /// Full-screen vertical Mode Select controller.
    /// Cloned from HoleSelectionScreenController; adapted for ModeData.
    ///
    /// ITER-6 FIDELITY CHANGES (§6.3):
    ///   Item 11: CardsContainer back panel 1074w, gradient, 3px white/0.9 border, rounded-20, pad-24, gap-24.
    ///   Item 12: Cards 978 wide, inset 48 inside the 1074 panel.
    ///   Item 16: Per-card chevron HIDDEN on full-screen list cards (SetShowChevron(false)).
    ///   Item 17: ENTRY FEE/REWARDS labels kept on all cards.
    /// </summary>
    public class ModeSelectScreenController : MonoBehaviour
    {
        [Header("Cards List")]
        [SerializeField] private ScrollRect cardsScrollRect;
        [SerializeField] private RectTransform cardsContent;
        [SerializeField] private ModeCardController cardPrefab;

        [Header("Back Panel (§6.2 Cards Container — 1074w, gradient, 3px border, rounded-20)")]
        [SerializeField] private RectTransform cardsContainerPanel;

        [Header("Screen Manager (optional — falls back to singleton)")]
        [SerializeField] private ScreenManager screenManager;

        [Header("1v1 Matchmaking Modal")]
        [SerializeField] private MatchmakingModalController matchmakingModal1v1;

        [Header("Initial state")]
        [Tooltip("Mode id to show expanded when the screen opens. Empty = all collapsed.")]
        [SerializeField] private string _initialExpandedModeId = "practice";

        private readonly List<ModeCardController> _cards = new List<ModeCardController>();
        // NOTE: _savedUsernameText and the SetUsername("MODE SELECTION") call were removed in
        // iter-10 (leaderboard_wiring). The "MODE SELECTION" top-bar center text is now driven
        // centrally by PersistentUIManager.HighlightScreen(ScreenId.ModeSelection) — the same
        // mechanism used for "LEADERBOARD" on the Rankings screen. This avoids corrupting the
        // cached _username field via transient SetUsername calls (BLOCKER 1 in REDTEAM_REVIEW).

        private void OnEnable()
        {
            StopAllCoroutines();
            StartCoroutine(RebuildCardsNextFrame());
        }

        private IEnumerator RebuildCardsNextFrame()
        {
            yield return null;
            RebuildCards();
        }

        private void OnDisable()
        {
            UnwireCards();
            _cards.Clear();
        }

        private void RebuildCards()
        {
            UnwireCards();
            if (cardsContent != null)
                foreach (Transform child in cardsContent) Destroy(child.gameObject);
            _cards.Clear();

            var db = ModesDatabaseCSV.Instance;
            if (db == null)
            {
                Debug.LogWarning("[ModeSelectScreen] ModesDatabaseCSV.Instance is null.");
                return;
            }

            if (cardPrefab == null)
            {
                Debug.LogError("[ModeSelectScreen] cardPrefab is null.");
                return;
            }

            var modes = db.GetAllModes();

            foreach (var mode in modes)
            {
                var card = Instantiate(cardPrefab, cardsContent);
                ModeCardState state = mode.locked
                    ? ModeCardState.Locked
                    : (!string.IsNullOrEmpty(_initialExpandedModeId) && mode.id == _initialExpandedModeId
                        ? ModeCardState.Expanded
                        : ModeCardState.Collapsed);
                // §6.3 item 16: NO expand chevron on full-screen list cards
                card.SetShowChevron(false);
                card.Bind(mode, state);
                card.OnCardTapped  += HandleCardTapped;
                card.OnPlayClicked += HandlePlayClicked;
                _cards.Add(card);
            }

            if (cardsScrollRect != null)
                cardsScrollRect.verticalNormalizedPosition = 1f;
        }

        private void UnwireCards()
        {
            foreach (var c in _cards)
            {
                if (c == null) continue;
                c.OnCardTapped  -= HandleCardTapped;
                c.OnPlayClicked -= HandlePlayClicked;
            }
        }

        private void HandleCardTapped(ModeCardController card)
        {
            if (card == null || card.State == ModeCardState.Locked) return;

            if (card.State == ModeCardState.Expanded)
            {
                card.SetState(ModeCardState.Collapsed);
                return;
            }

            foreach (var c in _cards)
            {
                if (c != null && c != card && c.State == ModeCardState.Expanded)
                    c.SetState(ModeCardState.Collapsed);
            }

            card.SetState(ModeCardState.Expanded);
            StartCoroutine(CentreCardNextFrame(card));
        }

        private void HandlePlayClicked(ModeCardController card)
        {
            if (card == null || card.State == ModeCardState.Locked) return;

            var db = ModesDatabaseCSV.Instance;
            if (db == null) return;

            var mode = db.GetMode(card.ModeId);
            if (mode == null) return;

            ScreenManager sm = screenManager != null ? screenManager : ScreenManager.Instance;

            switch (mode.target)
            {
                case "hole_select":
                    if (sm != null)
                        sm.ShowScreen(ScreenId.HoleSelection);
                    else
                        Debug.LogWarning("[ModeSelectScreen] ScreenManager not found.");
                    break;

                case "matchmaking_1v1":
                    // 1v1 path: flag the session as versus BEFORE opening matchmaking.
                    GameSession.IsVersus = true;
                    // Pick a random hole (1-18), then open matchmaking modal.
                    // MatchmakingModalController.Open expects a 0-based index.
                    if (matchmakingModal1v1 != null)
                    {
                        int randomHoleIndex = UnityEngine.Random.Range(0, 18); // 0-based → hole numbers 1-18
                        matchmakingModal1v1.Open(randomHoleIndex);
                    }
                    else
                    {
                        Debug.LogWarning("[ModeSelectScreen] 1v1 PLAY — matchmakingModal1v1 not wired in Inspector.");
                    }
                    break;

                case "none":
                default:
                    Debug.LogWarning($"[ModeSelectScreen] PLAY on mode '{card.ModeId}' has no route.");
                    break;
            }
        }

        private IEnumerator CentreCardNextFrame(ModeCardController card)
        {
            yield return null;
            Canvas.ForceUpdateCanvases();

            if (cardsScrollRect == null || card == null || card.rootRect == null) yield break;

            var content  = cardsScrollRect.content;
            var viewport = cardsScrollRect.viewport;
            var cardRt   = card.rootRect;

            float cardCentreFromTop  = -cardRt.anchoredPosition.y + cardRt.rect.height * 0.5f;
            float scrollableHeight   = content.rect.height - viewport.rect.height;
            if (scrollableHeight <= 0f) yield break;

            float targetCentreFromTop = cardCentreFromTop - viewport.rect.height * 0.5f;
            float normalized = Mathf.Clamp01(1f - targetCentreFromTop / scrollableHeight);
            cardsScrollRect.verticalNormalizedPosition = normalized;
        }
    }
}
