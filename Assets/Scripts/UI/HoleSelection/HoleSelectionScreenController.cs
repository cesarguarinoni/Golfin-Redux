using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using GolfinRedux.UI;
using Golfin.UI.Matchmaking;

namespace GolfinRedux.UI.HoleSelection
{
    /// <summary>
    /// Full-screen controller for the Hole Selection screen.
    /// Instantiates one HoleCard per HoleData in the database, wires up
    /// tap events, enforces the single-expanded invariant, and centres
    /// the expanded card in the scroll viewport.
    /// </summary>
    public class HoleSelectionScreenController : MonoBehaviour
    {
        [Header("Filters (visual only — no click logic in this task)")]
        [SerializeField] private GameObject filtersContainer;

        [Header("Cards List")]
        [SerializeField] private ScrollRect cardsScrollRect;
        [SerializeField] private RectTransform cardsContent;
        [SerializeField] private HoleCardController cardPrefab;

        [Header("Matchmaking Modal")]
        [SerializeField] private MatchmakingModalController matchmakingModal;

        [Header("Hole Database")]
        [SerializeField] private HoleDatabase holeDatabase;

        private readonly List<HoleCardController> _cards = new List<HoleCardController>();

        private void OnEnable()
        {
            // 1. Resolve database
            HoleDatabase db = holeDatabase;
            if (db == null) db = HoleDatabaseLoader.RuntimeDatabase;

            if (db == null)
            {
                Debug.LogWarning("[HoleSelection] No hole database available — screen will be empty.");
                return;
            }

            // 2. Clear prior children
            foreach (Transform child in cardsContent)
                Destroy(child.gameObject);

            _cards.Clear();

            // 3. Sort holes by holeNumber and instantiate cards
            var holes = new List<HoleData>(db.holes);
            holes.Sort((a, b) => a.holeNumber.CompareTo(b.holeNumber));

            foreach (var hole in holes)
            {
                if (cardPrefab == null)
                {
                    Debug.LogError("[HoleSelection] cardPrefab is null — cannot instantiate cards.");
                    break;
                }

                HoleCardController card = Instantiate(cardPrefab, cardsContent);

                HoleCardMode mode = HoleProgressionService.Instance.HasPlayed(hole.holeNumber)
                    ? HoleCardMode.Replay
                    : HoleCardMode.Play;

                HoleCardState state = !HoleProgressionService.Instance.IsUnlocked(hole.holeNumber)
                    ? HoleCardState.Locked
                    : HoleCardState.Collapsed;

                card.Bind(hole, mode, state);
                card.OnCardTapped += HandleCardTapped;
                card.OnActionButtonClicked += HandleActionClicked;

                _cards.Add(card);
            }

            // 4. Reset scroll to top
            if (cardsScrollRect != null)
                cardsScrollRect.verticalNormalizedPosition = 1f;
        }

        private void OnDisable()
        {
            foreach (var card in _cards)
            {
                if (card == null) continue;
                card.OnCardTapped -= HandleCardTapped;
                card.OnActionButtonClicked -= HandleActionClicked;
            }
            _cards.Clear();
        }

        private void HandleCardTapped(HoleCardController card)
        {
            if (card == null) return;

            // Belt-and-suspenders: ignore locked cards
            if (card.State == HoleCardState.Locked) return;

            if (card.State == HoleCardState.Expanded)
            {
                card.SetState(HoleCardState.Collapsed);
                return;
            }

            // Collapse the currently-expanded card (if any)
            foreach (var c in _cards)
            {
                if (c != null && c != card && c.State == HoleCardState.Expanded)
                    c.SetState(HoleCardState.Collapsed);
            }

            card.SetState(HoleCardState.Expanded);
            StartCoroutine(CentreCardNextFrame(card));
        }

        private void HandleActionClicked(HoleCardController card)
        {
            if (matchmakingModal != null)
                matchmakingModal.Open(card.HoleNumber - 1); // holeNumber is 1-based; index is 0-based
            else
                Debug.LogWarning("[HoleSelection] No matchmaking modal wired — action button is dead.");
        }

        private IEnumerator CentreCardNextFrame(HoleCardController card)
        {
            yield return null;
            Canvas.ForceUpdateCanvases();

            if (cardsScrollRect == null || card == null || card.rootRect == null) yield break;

            var content  = cardsScrollRect.content;
            var viewport = cardsScrollRect.viewport;
            var cardRt   = card.rootRect;

            // Position of card's centre in content-local space, measured from content top.
            float cardCentreFromTop = -cardRt.anchoredPosition.y + cardRt.rect.height * 0.5f;
            float scrollableHeight  = content.rect.height - viewport.rect.height;
            if (scrollableHeight <= 0f) yield break;

            float targetCentreFromTop = cardCentreFromTop - viewport.rect.height * 0.5f;
            float normalized = Mathf.Clamp01(1f - targetCentreFromTop / scrollableHeight);
            cardsScrollRect.verticalNormalizedPosition = normalized;
        }
    }
}
