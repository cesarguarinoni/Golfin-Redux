using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using GolfinRedux.UI;
using Golfin.UI.Matchmaking;
using Golfin.Utilities;

namespace GolfinRedux.UI.HoleSelection
{
    public enum CourseFilter { Lomond, Yaita }
    public enum TeeFilter    { Ladies, Front, Regular, Back }

    [System.Serializable]
    public class FilterPill
    {
        public Button button;
        public TextMeshProUGUI label;
        public Image background;
        public GameObject lockIcon; // shown only when locked == true
        public bool locked; // can't be selected
        public string filterId; // "Lomond" / "Yaita" / "Ladies" / "Front" / "Regular" / "Back"
    }

    /// <summary>
    /// Full-screen controller for the Hole Selection screen.
    /// Instantiates one HoleCard per HoleData in the database, wires up
    /// tap events, enforces the single-expanded invariant, and centres
    /// the expanded card in the scroll viewport.
    /// Also manages course/tee filter pill state and dynamic counts.
    /// </summary>
    public class HoleSelectionScreenController : MonoBehaviour
    {
        [Header("Filters")]
        [SerializeField] private GameObject filtersContainer;
        [SerializeField] private List<FilterPill> coursePills = new List<FilterPill>();
        [SerializeField] private List<FilterPill> teePills    = new List<FilterPill>();
        // Parent RectTransforms of each pill row — dividers are injected here (Cesar correction 5)
        [SerializeField] private RectTransform courseFilterRow;
        [SerializeField] private RectTransform teeFilterRow;

        [Header("Cards List")]
        [SerializeField] private ScrollRect cardsScrollRect;
        [SerializeField] private RectTransform cardsContent;
        [SerializeField] private HoleCardController cardPrefab;

        [Header("Matchmaking Modal")]
        [SerializeField] private MatchmakingModalController matchmakingModal;

        [Header("Hole Database")]
        [SerializeField] private HoleDatabase holeDatabase;

        private readonly List<HoleCardController> _cards = new List<HoleCardController>();
        private bool _dividersInjected = false;

        private CourseFilter _activeCourse = CourseFilter.Lomond;
        private TeeFilter    _activeTee    = TeeFilter.Ladies;

        // Pill colour rules per Figma + project convention (mirrors ClubFilterBar / InventoryScreenController):
        //   active selectable → gold gradient (TextGradients.ApplyGold)
        //   inactive selectable → silver gradient (TextGradients.ApplySilver)
        //   locked → silver gradient + lock icon visible
        // The two LADIES/FRONT pills in row 2 are independent toggles per spec, so "white active"
        // doesn't apply — they share the gold/silver convention with everything else.

        private void OnEnable()
        {
            // Inject dividers once (they persist — no need to re-inject on each enable)
            if (!_dividersInjected)
            {
                InjectDividers(courseFilterRow, coursePills.Count);
                InjectDividers(teeFilterRow,    teePills.Count);
                _dividersInjected = true;
            }
            BuildFilterPillListeners();
            RebuildCards();
        }

        /// <summary>
        /// Injects 1-px white-30%-alpha vertical line dividers between adjacent pills.
        /// Mirrors ClubFilterBar.InjectDividers() — ignoreLayout=true, anchored at fractional x.
        /// Cesar correction 5.
        /// </summary>
        private void InjectDividers(RectTransform row, int pillCount)
        {
            if (row == null || pillCount < 2) return;
            int dividerCount = pillCount - 1;
            for (int i = 0; i < dividerCount; i++)
            {
                var divGO = new GameObject("FilterDivider");
                divGO.transform.SetParent(row, false);

                var le = divGO.AddComponent<LayoutElement>();
                le.ignoreLayout = true;

                float xPos = (float)(i + 1) / pillCount;
                var rt = divGO.GetComponent<RectTransform>();
                rt.anchorMin        = new Vector2(xPos, 0.15f);
                rt.anchorMax        = new Vector2(xPos, 0.85f);
                rt.sizeDelta        = new Vector2(1f, 0f); // 1 px wide; height from anchors
                rt.anchoredPosition = Vector2.zero;

                var img = divGO.AddComponent<Image>();
                img.color         = new Color(1f, 1f, 1f, 0.3f);
                img.raycastTarget = false;
            }
        }

        private void BuildFilterPillListeners()
        {
            foreach (var p in coursePills)
            {
                if (p == null || p.button == null) continue;
                p.button.onClick.RemoveAllListeners();
                if (!p.locked)
                {
                    var captured = p;
                    p.button.onClick.AddListener(() => OnCoursePillClicked(captured));
                }
                p.button.interactable = !p.locked;
            }

            foreach (var p in teePills)
            {
                if (p == null || p.button == null) continue;
                p.button.onClick.RemoveAllListeners();
                if (!p.locked)
                {
                    var captured = p;
                    p.button.onClick.AddListener(() => OnTeePillClicked(captured));
                }
                p.button.interactable = !p.locked;
            }

            UpdatePillVisuals();
            UpdatePillCounts();
        }

        private void OnCoursePillClicked(FilterPill pill)
        {
            if (pill == null || pill.locked) return;
            if (System.Enum.TryParse<CourseFilter>(pill.filterId, true, out var result))
                _activeCourse = result;
            UpdatePillVisuals();
            RebuildCards();
        }

        private void OnTeePillClicked(FilterPill pill)
        {
            if (pill == null || pill.locked) return;
            if (System.Enum.TryParse<TeeFilter>(pill.filterId, true, out var result))
                _activeTee = result;
            UpdatePillVisuals();
            // Tees don't filter the card list in this task (no per-tee data); just visual swap.
        }

        private void UpdatePillVisuals()
        {
            foreach (var p in coursePills)
            {
                if (p == null || p.label == null) continue;
                if (p.lockIcon != null) p.lockIcon.SetActive(p.locked);
                if (p.label != null) p.label.textWrappingMode = TMPro.TextWrappingModes.NoWrap; // single-line pills (fixes YAITA - KIKYOU wrap)
                if (p.locked) { TextGradients.ApplySilver(p.label); continue; }
                bool isActive = string.Equals(p.filterId, _activeCourse.ToString(), System.StringComparison.OrdinalIgnoreCase);
                if (isActive) TextGradients.ApplyGold(p.label);
                else          TextGradients.ApplySilver(p.label);
            }
            foreach (var p in teePills)
            {
                if (p == null || p.label == null) continue;
                if (p.lockIcon != null) p.lockIcon.SetActive(p.locked);
                if (p.label != null) p.label.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
                if (p.locked) { TextGradients.ApplySilver(p.label); continue; }
                bool isActive = string.Equals(p.filterId, _activeTee.ToString(), System.StringComparison.OrdinalIgnoreCase);
                if (isActive) TextGradients.ApplyGold(p.label);
                else          TextGradients.ApplySilver(p.label);
            }
        }

        /// <summary>
        /// Update the dynamic count fragments on each pill (e.g. "LOMOND 28/72").
        /// </summary>
        private void UpdatePillCounts()
        {
            // Per spec: filter count fragments are hardcoded literal strings (visual placeholders).
            // No dynamic calculation in this task — that's a future filter-wiring spec.
            foreach (var p in coursePills)
            {
                if (p == null || p.label == null) continue;
                if      (string.Equals(p.filterId, "Lomond", System.StringComparison.OrdinalIgnoreCase)) p.label.text = "LOMOND 28/72";
                else if (string.Equals(p.filterId, "Yaita",  System.StringComparison.OrdinalIgnoreCase)) p.label.text = "YAITA - KIKYOU";
            }

            foreach (var p in teePills)
            {
                if (p == null || p.label == null) continue;
                if      (p.filterId == "Ladies")  p.label.text = "LADIES 18/18";
                else if (p.filterId == "Front")   p.label.text = "FRONT 10/18";
                else if (p.filterId == "Regular") p.label.text = "REGULAR 0/18";
                else if (p.filterId == "Back")    p.label.text = "BACK 0/18";
            }
        }

        private void RebuildCards()
        {
            HoleDatabase db = holeDatabase;
            if (db == null) db = HoleDatabaseLoader.RuntimeDatabase;

            if (db == null)
            {
                Debug.LogWarning("[HoleSelection] No hole database available — screen will be empty.");
                return;
            }

            // Clear prior children and event subscriptions
            foreach (var c in _cards)
            {
                if (c == null) continue;
                c.OnCardTapped -= HandleCardTapped;
                c.OnActionButtonClicked -= HandleActionClicked;
            }
            foreach (Transform child in cardsContent) Destroy(child.gameObject);
            _cards.Clear();

            // For Yaita filter: no holes (no Yaita data exists)
            if (_activeCourse == CourseFilter.Yaita)
            {
                if (cardsScrollRect != null) cardsScrollRect.verticalNormalizedPosition = 1f;
                return;
            }

            // Lomond filter: all 18 holes
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
