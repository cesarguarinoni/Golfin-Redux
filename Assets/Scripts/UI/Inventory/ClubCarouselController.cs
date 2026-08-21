#nullable enable
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

using Golfin.UI.Common;

namespace Golfin.Inventory
{
    /// <summary>
    /// Club carousel controller — mirrors CarouselController (Roster) but reads from
    /// ClubManager and responds to ClubFilterBar.OnFilterChanged.
    /// 6 clubs per page; fires OnClubSelected(string clubId) when a card is tapped.
    /// </summary>
    public class ClubCarouselController : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Transform contentParent = null!;
        [SerializeField] private GameObject clubCardPrefab = null!;
        [SerializeField] private Button leftArrowButton = null!;
        [SerializeField] private Button rightArrowButton = null!;
        [SerializeField] private Transform paginationDotsParent = null!;
        [SerializeField] private GameObject? paginationDotPrefab;

        [Header("Filter Bar")]
        [SerializeField] private ClubFilterBar? filterBar;

        [Header("Settings")]
        [SerializeField] private int cardsPerPage = 6;
        [SerializeField] private float scrollSmoothness = 0.3f;

        /// <summary>Fired when a card is tapped. Arg = clubId.</summary>
        public event System.Action<string>? OnClubSelected;

        private readonly List<ClubThumbnailCard> cards = new();
        private PaginationDotStrip? _dots;
        private ScrollRect? scrollRect;
        private int currentPage = 0;
        private int totalPages = 1;
        private string selectedClubId = "";
        private bool viewportExpanded = false;
        private bool _isAnimating = false;
        private ClubType? _currentFilter = null;

        // ── Lifecycle ──────────────────────────────────────────────────────────

        private void Awake()
        {
            scrollRect = GetComponentInChildren<ScrollRect>();
            if (scrollRect == null)
                Debug.LogError("[ClubCarouselController] ScrollRect not found in children.");
        }

        private void Start()
        {
            PopulateCarousel(_currentFilter);
            SetupArrowButtons();
            SetupPagination();
        }

        private void OnEnable()
        {
            if (filterBar != null)
                filterBar.OnFilterChanged += OnFilterChanged;

            if (ClubManager.Instance != null)
                ClubManager.Instance.OnClubEquipped += OnClubEquippedChanged;
        }

        private void OnDisable()
        {
            if (filterBar != null)
                filterBar.OnFilterChanged -= OnFilterChanged;

            if (ClubManager.Instance != null)
                ClubManager.Instance.OnClubEquipped -= OnClubEquippedChanged;
        }

        // ── Population ─────────────────────────────────────────────────────────

        private void PopulateCarousel(ClubType? filter)
        {
            if (ClubManager.Instance == null)
            {
                Debug.LogWarning("[ClubCarouselController] ClubManager not ready.");
                return;
            }

            List<PlayerClubData> clubs;
            if (filter == null)
            {
                clubs = ClubManager.Instance.GetAllOwnedClubs();
            }
            else if (filterBar != null && filterBar.IsWedgeFilter)
            {
                // Unified WEDGES tab — gather all 3 wedge types
                var a = ClubManager.Instance.GetOwnedClubsOfType(ClubType.A_Wedge);
                var p = ClubManager.Instance.GetOwnedClubsOfType(ClubType.P_Wedge);
                var s = ClubManager.Instance.GetOwnedClubsOfType(ClubType.S_Wedge);
                clubs = new List<PlayerClubData>(a.Count + p.Count + s.Count);
                clubs.AddRange(a);
                clubs.AddRange(p);
                clubs.AddRange(s);
            }
            else
            {
                clubs = ClubManager.Instance.GetOwnedClubsOfType(filter.Value);
            }

            // Layout: prevent cards from stretching
            var layoutGroup = contentParent.GetComponent<HorizontalLayoutGroup>();
            if (layoutGroup != null)
            {
                layoutGroup.childForceExpandWidth  = false;
                layoutGroup.childForceExpandHeight = false;
            }

            // ContentSizeFitter so ScrollRect can scroll
            var sizeFitter = contentParent.GetComponent<ContentSizeFitter>();
            if (sizeFitter == null)
                sizeFitter = contentParent.gameObject.AddComponent<ContentSizeFitter>();
            sizeFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            sizeFitter.verticalFit   = ContentSizeFitter.FitMode.Unconstrained;

            // Expand viewport once to avoid clipping the scale-up animation
            if (!viewportExpanded && scrollRect != null && scrollRect.viewport != null)
            {
                var vp = scrollRect.viewport;
                const float overflow = 8f;
                vp.offsetMin -= new Vector2(overflow, overflow);
                vp.offsetMax += new Vector2(overflow, overflow);
                viewportExpanded = true;
            }

            // Clear old cards
            cards.Clear();
            foreach (Transform child in contentParent)
                Destroy(child.gameObject);

            if (clubCardPrefab == null)
            {
                Debug.LogWarning("[ClubCarouselController] clubCardPrefab not assigned — assign in Inspector.");
                return;
            }

            string previousId = selectedClubId;

            foreach (var playerClub in clubs)
            {
                var cardGO = Instantiate(clubCardPrefab, contentParent);

                var le = cardGO.GetComponent<LayoutElement>();
                if (le == null) le = cardGO.AddComponent<LayoutElement>();
                le.preferredWidth  = 135f;
                le.preferredHeight = 165f;

                var card = cardGO.GetComponent<ClubThumbnailCard>();
                if (card != null)
                {
                    card.Initialize(playerClub.clubId);
                    var id = playerClub.clubId;
                    card.OnClicked += () => SelectClub(id);
                    cards.Add(card);
                }
            }

            // Keep previous selection if still visible, otherwise select first
            if (cards.Count > 0)
            {
                var keep = cards.FirstOrDefault(c => c.GetClubId() == previousId);
                SelectClub(keep != null ? keep.GetClubId() : cards[0].GetClubId());
            }
            else
            {
                selectedClubId = "";
            }

            RebuildPagination();
            Debug.Log($"[ClubCarouselController] {cards.Count} cards (filter: {filter?.ToString() ?? "ALL"}).");
        }

        // ── Arrow navigation ───────────────────────────────────────────────────

        private void SetupArrowButtons()
        {
            if (leftArrowButton  != null) leftArrowButton.onClick.AddListener(() => GoToPage(currentPage - 1));
            if (rightArrowButton != null) rightArrowButton.onClick.AddListener(() => GoToPage(currentPage + 1));
        }

        private void GoToPage(int page)
        {
            page = Mathf.Clamp(page, 0, totalPages - 1);
            if (page == currentPage) return;
            currentPage = page;

            float targetPos = totalPages > 1 ? (float)currentPage / (totalPages - 1) : 0f;
            StartCoroutine(SmoothScroll(targetPos));
            RefreshDotColors();
            UpdateArrowButtonStates();
        }

        private IEnumerator SmoothScroll(float targetPos)
        {
            if (scrollRect == null) yield break;
            _isAnimating = true;

            float elapsed  = 0f;
            float startPos = scrollRect.horizontalNormalizedPosition;

            while (elapsed < scrollSmoothness)
            {
                elapsed += Time.deltaTime;
                scrollRect.horizontalNormalizedPosition =
                    Mathf.Lerp(startPos, targetPos, elapsed / scrollSmoothness);
                yield return null;
            }

            scrollRect.horizontalNormalizedPosition = targetPos;
            _isAnimating = false;
        }

        // ── Pagination ─────────────────────────────────────────────────────────

        private void SetupPagination()
        {
            RebuildPagination();
            if (scrollRect != null)
                scrollRect.onValueChanged.AddListener(OnScrollValueChanged);
        }

        private void RebuildPagination()
        {
            totalPages  = Mathf.CeilToInt(cards.Count > 0 ? (float)cards.Count / cardsPerPage : 1);
            currentPage = 0;

            if (paginationDotsParent == null) return;
            _dots ??= new PaginationDotStrip(paginationDotsParent, paginationDotPrefab);
            _dots.Rebuild(totalPages, currentPage);

            UpdateArrowButtonStates();
        }

        private void OnScrollValueChanged(Vector2 scrollPos)
        {
            if (_isAnimating || totalPages <= 1) return;
            int newPage = Mathf.Clamp(
                Mathf.RoundToInt(scrollPos.x * (totalPages - 1)), 0, totalPages - 1);
            if (newPage == currentPage) return;
            currentPage = newPage;
            RefreshDotColors();
            UpdateArrowButtonStates();
        }

        private void RefreshDotColors()
        {
            _dots?.Refresh(currentPage);
        }

        private void UpdateArrowButtonStates()
        {
            if (leftArrowButton  != null) leftArrowButton.interactable  = currentPage > 0;
            if (rightArrowButton != null) rightArrowButton.interactable = currentPage < totalPages - 1;
        }

        // ── Selection ──────────────────────────────────────────────────────────

        public void SelectClub(string clubId)
        {
            if (!string.IsNullOrEmpty(selectedClubId))
            {
                var prev = cards.Find(c => c.GetClubId() == selectedClubId);
                if (prev != null) prev.SetSelected(false);
            }

            selectedClubId = clubId;
            var card = cards.Find(c => c.GetClubId() == clubId);
            if (card != null) card.SetSelected(true);

            OnClubSelected?.Invoke(clubId);
            Debug.Log($"[ClubCarouselController] Selected: {clubId}");
        }

        // ── Event Handlers ─────────────────────────────────────────────────────

        private void OnFilterChanged(ClubType? filter)
        {
            _currentFilter = filter;
            PopulateCarousel(filter);
        }

        private void OnClubEquippedChanged(string _)
        {
            foreach (var card in cards)
                card.RefreshIcons();
        }

        // ── Accessors ──────────────────────────────────────────────────────────

        public string GetSelectedClubId() => selectedClubId;
    }
}
