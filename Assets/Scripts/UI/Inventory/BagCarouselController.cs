#nullable enable
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

using Golfin.UI.Common;

namespace Golfin.Inventory
{
    /// <summary>
    /// Bag carousel — horizontal scroll of bag portraits.
    /// Shows unlocked bags (BagThumbnailCard) + locked bags (BagSlotLockedPrefab).
    /// Always shows at least 6 slots. Fires OnBagSelected(int bagSlot) when tapped.
    /// </summary>
    public class BagCarouselController : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Transform contentParent = null!;
        [SerializeField] private GameObject bagCardPrefab = null!;       // unlocked bag
        [SerializeField] private GameObject bagLockedCardPrefab = null!; // locked bag
        [SerializeField] private Button leftArrowButton = null!;
        [SerializeField] private Button rightArrowButton = null!;
        [SerializeField] private Transform paginationDotsParent = null!;
        [SerializeField] private GameObject? paginationDotPrefab;

        [Header("Detail Panel")]
        [SerializeField] private BagDetailPanel? detailPanel;

        [Header("Settings")]
        [SerializeField] private int cardsPerPage = 6;
        [SerializeField] private int minCardCount = 6;
        [SerializeField] private float scrollSmoothness = 0.3f;

        /// <summary>Fired when a bag card is tapped. Arg = bagSlot (1-based).</summary>
        public event System.Action<int>? OnBagSelected;

        private readonly List<BagThumbnailCard> cards = new();
        private PaginationDotStrip? _dots;
        private ScrollRect? scrollRect;
        private int currentPage = 0;
        private int totalPages = 1;
        private int selectedBagSlot = 0;
        private bool viewportExpanded = false;
        private bool _isAnimating = false;

        // ── Lifecycle ──────────────────────────────────────────────────────

        private void Awake()
        {
            scrollRect = GetComponentInChildren<ScrollRect>();
        }

        private void Start()
        {
            PopulateCarousel();
            SetupArrowButtons();
            SetupPagination();
        }

        private void OnEnable()
        {
            if (BagManager.Instance != null)
            {
                BagManager.Instance.OnBagChanged += OnBagChanged;
                BagManager.Instance.OnEquippedBagChanged += OnEquippedChanged;
            }
        }

        private void OnDisable()
        {
            if (BagManager.Instance != null)
            {
                BagManager.Instance.OnBagChanged -= OnBagChanged;
                BagManager.Instance.OnEquippedBagChanged -= OnEquippedChanged;
            }
        }

        private void OnBagChanged(int _) => PopulateCarousel();
        private void OnEquippedChanged(int _) => RefreshEquippedStates();

        // ── Population ─────────────────────────────────────────────────────

        public void PopulateCarousel()
        {
            if (BagManager.Instance == null || BagDatabaseCSV.Instance == null) return;

            // Expand viewport once
            if (!viewportExpanded && scrollRect?.viewport != null)
            {
                var vp = scrollRect.viewport;
                const float overflow = 8f;
                vp.offsetMin -= new Vector2(overflow, overflow);
                vp.offsetMax += new Vector2(overflow, overflow);
                viewportExpanded = true;
            }

            // Clear
            cards.Clear();
            foreach (Transform child in contentParent)
                Destroy(child.gameObject);

            var allBags = BagDatabaseCSV.Instance.GetAllBags();
            int previousSlot = selectedBagSlot;

            // ── Step 1: spawn only UNLOCKED bags as thumbnail cards ────────────
            for (int i = 0; i < allBags.Count; i++)
            {
                int bagSlot = i + 1;
                if (!BagManager.Instance.IsBagUnlocked(bagSlot)) continue;

                var cardGO = Instantiate(bagCardPrefab, contentParent);
                cardGO.SetActive(true); // prefab may have been saved inactive
                var cardLE = cardGO.GetComponent<LayoutElement>();
                if (cardLE == null) cardLE = cardGO.AddComponent<LayoutElement>();
                cardLE.preferredWidth = 135f;
                cardLE.preferredHeight = 165f;

                var card = cardGO.GetComponent<BagThumbnailCard>();
                if (card != null)
                {
                    bool isEquipped = BagManager.Instance.EquippedBagSlot == bagSlot;
                    card.Initialize(bagSlot, allBags[i], isEquipped);
                    int slot = bagSlot;
                    card.OnClicked += () => SelectBag(slot);
                    cards.Add(card);
                }
            }

            // ── Step 2: pad with locked cards up to minCardCount total ─────────
            int currentCount = contentParent.childCount;
            for (int i = currentCount; i < minCardCount; i++)
            {
                if (bagLockedCardPrefab == null) break;
                var lockedGO = Instantiate(bagLockedCardPrefab, contentParent);
                lockedGO.SetActive(true); // prefab may have been saved inactive
                var le = lockedGO.GetComponent<LayoutElement>();
                if (le == null) le = lockedGO.AddComponent<LayoutElement>();
                le.preferredWidth = 135f;
                le.preferredHeight = 165f;
            }

            // Restore selection
            if (cards.Count > 0)
            {
                var keep = cards.Find(c => c.GetBagSlot() == previousSlot);
                SelectBag(keep != null ? keep.GetBagSlot() : cards[0].GetBagSlot());
            }

            RebuildPagination();
        }

        private void RefreshEquippedStates()
        {
            if (BagManager.Instance == null) return;
            foreach (var card in cards)
                card.SetEquipped(BagManager.Instance.EquippedBagSlot == card.GetBagSlot());
        }

        // ── Selection ──────────────────────────────────────────────────────

        public void SelectBag(int bagSlot)
        {
            foreach (var card in cards)
                card.SetSelected(card.GetBagSlot() == bagSlot);

            selectedBagSlot = bagSlot;
            OnBagSelected?.Invoke(bagSlot);
            detailPanel?.ShowBag(bagSlot);
        }

        public int GetSelectedBagSlot() => selectedBagSlot;

        // ── Arrows + Pagination ────────────────────────────────────────────

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
            float elapsed = 0f;
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

        private void SetupPagination() { RebuildPagination(); }

        private void RebuildPagination()
        {
            totalPages  = Mathf.CeilToInt(cards.Count > 0 ? (float)cards.Count / cardsPerPage : 1);
            currentPage = 0;

            if (paginationDotsParent == null) return;
            _dots ??= new PaginationDotStrip(paginationDotsParent, paginationDotPrefab);
            _dots.Rebuild(totalPages, currentPage);

            UpdateArrowButtonStates();
        }

        private void RefreshDotColors()
        {
            _dots?.Refresh(currentPage);
        }

        private void UpdateArrowButtonStates()
        {
            bool multiPage = totalPages > 1;
            // Hide arrows entirely when everything fits on one page
            if (leftArrowButton  != null) leftArrowButton.gameObject.SetActive(multiPage && currentPage > 0);
            if (rightArrowButton != null) rightArrowButton.gameObject.SetActive(multiPage && currentPage < totalPages - 1);
        }
    }
}
