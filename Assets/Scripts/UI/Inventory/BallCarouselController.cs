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
    /// Ball carousel controller — mirrors ClubCarouselController but reads from
    /// BallManager. No filter bar (all balls shown in a flat list).
    /// 6 balls per page; fires OnBallSelected(string ballId) when a card is tapped.
    /// </summary>
    public class BallCarouselController : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Transform contentParent = null!;
        [SerializeField] private GameObject ballCardPrefab = null!;
        [SerializeField] private Button leftArrowButton = null!;
        [SerializeField] private Button rightArrowButton = null!;
        [SerializeField] private Transform paginationDotsParent = null!;
        [SerializeField] private GameObject? paginationDotPrefab;

        [SerializeField] private GameObject? ballEmptyCardPrefab;

        [Header("Settings")]
        [SerializeField] private int cardsPerPage = 6;
        [SerializeField] private int minCardCount  = 6;   // always show at least this many slots
        [SerializeField] private float scrollSmoothness = 0.3f;

        /// <summary>Fired when a card is tapped. Arg = ballId.</summary>
        public event System.Action<string>? OnBallSelected;

        private readonly List<BallThumbnailCard> cards = new();
        private PaginationDotStrip? _dots;
        private ScrollRect? scrollRect;
        private int currentPage = 0;
        private int totalPages = 1;
        private string selectedBallId = "";
        private bool viewportExpanded = false;
        private bool _isAnimating = false;

        // ── Lifecycle ──────────────────────────────────────────────────────────

        private void Awake()
        {
            scrollRect = GetComponentInChildren<ScrollRect>();
            if (scrollRect == null)
                Debug.LogError("[BallCarouselController] ScrollRect not found in children.");
        }

        private void Start()
        {
            PopulateCarousel();
            SetupArrowButtons();
            SetupPagination();
        }

        private void OnEnable()
        {
            if (BallManager.Instance != null)
                BallManager.Instance.OnInventoryChanged += OnInventoryChanged;
        }

        private void OnDisable()
        {
            if (BallManager.Instance != null)
                BallManager.Instance.OnInventoryChanged -= OnInventoryChanged;
        }

        // ── Population ─────────────────────────────────────────────────────────

        private void PopulateCarousel()
        {
            if (BallManager.Instance == null)
            {
                Debug.LogWarning("[BallCarouselController] BallManager not ready.");
                return;
            }

            var ballIds = BallManager.Instance.GetAllOwnedBallIds();

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

            if (ballCardPrefab == null)
            {
                Debug.LogWarning("[BallCarouselController] ballCardPrefab not assigned — assign in Inspector.");
                return;
            }

            string previousId = selectedBallId;

            foreach (var ballId in ballIds)
            {
                var cardGO = Instantiate(ballCardPrefab, contentParent);

                var le = cardGO.GetComponent<LayoutElement>();
                if (le == null) le = cardGO.AddComponent<LayoutElement>();
                le.preferredWidth  = 135f;
                le.preferredHeight = 165f;

                var card = cardGO.GetComponent<BallThumbnailCard>();
                if (card != null)
                {
                    card.Initialize(ballId);
                    var id = ballId;
                    card.OnClicked += () => SelectBall(id);
                    cards.Add(card);
                }
            }

            // Pad to minCardCount with empty (non-selectable) slot cards.
            // LayoutElement is baked into the prefab — no AddComponent needed.
            int totalSlots = Mathf.Max(cards.Count, minCardCount);
            for (int i = cards.Count; i < totalSlots; i++)
            {
                if (ballEmptyCardPrefab == null) break;
                var emptyGO = Instantiate(ballEmptyCardPrefab, contentParent);

                // Safety: ensure LayoutElement has correct sizes (baked in prefab, but guard anyway)
                var le = emptyGO.GetComponent<LayoutElement>();
                if (le == null) le = emptyGO.AddComponent<LayoutElement>();
                le.ignoreLayout    = false;
                le.preferredWidth  = 135f;
                le.preferredHeight = 165f;
                le.minWidth        = 135f;
                le.minHeight       = 165f;

                // Safety: ensure not clickable
                var btn = emptyGO.GetComponent<Button>();
                if (btn != null) btn.interactable = false;
            }

            // Keep previous selection if still visible, otherwise select first
            if (cards.Count > 0)
            {
                var keep = cards.FirstOrDefault(c => c.GetBallId() == previousId);
                SelectBall(keep != null ? keep.GetBallId() : cards[0].GetBallId());
            }
            else
            {
                selectedBallId = "";
            }

            RebuildPagination();
            Debug.Log($"[BallCarouselController] {cards.Count} ball cards populated.");
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

        public void SelectBall(string ballId)
        {
            if (!string.IsNullOrEmpty(selectedBallId))
            {
                var prev = cards.Find(c => c.GetBallId() == selectedBallId);
                if (prev != null) prev.SetSelected(false);
            }

            selectedBallId = ballId;
            var card = cards.Find(c => c.GetBallId() == ballId);
            if (card != null) card.SetSelected(true);

            OnBallSelected?.Invoke(ballId);
            Debug.Log($"[BallCarouselController] Selected: {ballId}");
        }

        // ── Event Handlers ─────────────────────────────────────────────────────

        private void OnInventoryChanged()
        {
            PopulateCarousel();
        }

        // ── Accessors ──────────────────────────────────────────────────────────

        public string GetSelectedBallId() => selectedBallId;
    }
}
