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
    /// Item carousel controller — mirrors BallCarouselController but reads from
    /// ItemManager. No filter bar (all items shown in a flat list).
    /// 6 items per page; fires OnItemSelected(string itemId) when a card is tapped.
    /// </summary>
    public class ItemCarouselController : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Transform contentParent = null!;
        [SerializeField] private GameObject itemCardPrefab = null!;
        [SerializeField] private Button leftArrowButton = null!;
        [SerializeField] private Button rightArrowButton = null!;
        [SerializeField] private Transform paginationDotsParent = null!;
        [SerializeField] private GameObject? paginationDotPrefab;

        [SerializeField] private GameObject? itemEmptyCardPrefab;

        [Header("Settings")]
        [SerializeField] private int cardsPerPage = 6;
        [SerializeField] private int minCardCount  = 6;
        [SerializeField] private float scrollSmoothness = 0.3f;

        /// <summary>Fired when a card is tapped. Arg = itemId.</summary>
        public event System.Action<string>? OnItemSelected;

        private readonly List<ItemThumbnailCard> cards = new();
        private PaginationDotStrip? _dots;
        private ScrollRect? scrollRect;
        private int currentPage = 0;
        private int totalPages = 1;
        private string selectedItemId = "";
        private bool viewportExpanded = false;
        private bool _isAnimating = false;

        // ── Lifecycle ──────────────────────────────────────────────────────────

        private void Awake()
        {
            scrollRect = GetComponentInChildren<ScrollRect>();
            if (scrollRect == null)
                Debug.LogError("[ItemCarouselController] ScrollRect not found in children.");
        }

        private void Start()
        {
            PopulateCarousel();
            SetupArrowButtons();
            SetupPagination();
        }

        private void OnEnable()
        {
            if (ItemManager.Instance != null)
                ItemManager.Instance.OnInventoryChanged += OnInventoryChanged;
        }

        private void OnDisable()
        {
            if (ItemManager.Instance != null)
                ItemManager.Instance.OnInventoryChanged -= OnInventoryChanged;
        }

        // ── Population ─────────────────────────────────────────────────────────

        private void PopulateCarousel()
        {
            if (ItemManager.Instance == null)
            {
                Debug.LogWarning("[ItemCarouselController] ItemManager not ready.");
                return;
            }

            var itemIds = ItemManager.Instance.GetAllOwnedItemIds();

            var layoutGroup = contentParent.GetComponent<HorizontalLayoutGroup>();
            if (layoutGroup != null)
            {
                layoutGroup.childForceExpandWidth  = false;
                layoutGroup.childForceExpandHeight = false;
            }

            var sizeFitter = contentParent.GetComponent<ContentSizeFitter>();
            if (sizeFitter == null)
                sizeFitter = contentParent.gameObject.AddComponent<ContentSizeFitter>();
            sizeFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            sizeFitter.verticalFit   = ContentSizeFitter.FitMode.Unconstrained;

            if (!viewportExpanded && scrollRect != null && scrollRect.viewport != null)
            {
                var vp = scrollRect.viewport;
                const float overflow = 8f;
                vp.offsetMin -= new Vector2(overflow, overflow);
                vp.offsetMax += new Vector2(overflow, overflow);
                viewportExpanded = true;
            }

            cards.Clear();
            foreach (Transform child in contentParent)
                Destroy(child.gameObject);

            if (itemCardPrefab == null)
            {
                Debug.LogWarning("[ItemCarouselController] itemCardPrefab not assigned — assign in Inspector.");
                return;
            }

            string previousId = selectedItemId;

            foreach (var itemId in itemIds)
            {
                var cardGO = Instantiate(itemCardPrefab, contentParent);

                var le = cardGO.GetComponent<LayoutElement>();
                if (le == null) le = cardGO.AddComponent<LayoutElement>();
                le.preferredWidth  = 135f;
                le.preferredHeight = 165f;

                var card = cardGO.GetComponent<ItemThumbnailCard>();
                if (card != null)
                {
                    card.Initialize(itemId);
                    var id = itemId;
                    card.OnClicked += () => SelectItem(id);
                    cards.Add(card);
                }
            }

            int totalSlots = Mathf.Max(cards.Count, minCardCount);
            for (int i = cards.Count; i < totalSlots; i++)
            {
                if (itemEmptyCardPrefab == null) break;
                var emptyGO = Instantiate(itemEmptyCardPrefab, contentParent);

                var le = emptyGO.GetComponent<LayoutElement>();
                if (le == null) le = emptyGO.AddComponent<LayoutElement>();
                le.ignoreLayout    = false;
                le.preferredWidth  = 135f;
                le.preferredHeight = 165f;
                le.minWidth        = 135f;
                le.minHeight       = 165f;

                var btn = emptyGO.GetComponent<Button>();
                if (btn != null) btn.interactable = false;
            }

            if (cards.Count > 0)
            {
                var keep = cards.FirstOrDefault(c => c.GetItemId() == previousId);
                SelectItem(keep != null ? keep.GetItemId() : cards[0].GetItemId());
            }
            else
            {
                selectedItemId = "";
            }

            RebuildPagination();
            Debug.Log($"[ItemCarouselController] {cards.Count} item cards populated.");
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

        public void SelectItem(string itemId)
        {
            if (!string.IsNullOrEmpty(selectedItemId))
            {
                var prev = cards.Find(c => c.GetItemId() == selectedItemId);
                if (prev != null) prev.SetSelected(false);
            }

            selectedItemId = itemId;
            var card = cards.Find(c => c.GetItemId() == itemId);
            if (card != null) card.SetSelected(true);

            OnItemSelected?.Invoke(itemId);
            Debug.Log($"[ItemCarouselController] Selected: {itemId}");
        }

        // ── Event Handlers ─────────────────────────────────────────────────────

        private void OnInventoryChanged()
        {
            PopulateCarousel();
        }

        // ── Accessors ──────────────────────────────────────────────────────────

        public string GetSelectedItemId() => selectedItemId;
    }
}
