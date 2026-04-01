#nullable enable
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

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

        [Header("Settings")]
        [SerializeField] private int cardsPerPage = 6;
        [SerializeField] private int minCardCount = 6;
        [SerializeField] private float scrollSmoothness = 0.3f;

        /// <summary>Fired when a bag card is tapped. Arg = bagSlot (1-based).</summary>
        public event System.Action<int>? OnBagSelected;

        private readonly List<BagThumbnailCard> cards = new();
        private readonly List<Image> paginationDots = new();
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

            for (int i = 0; i < allBags.Count; i++)
            {
                int bagSlot = i + 1;
                bool unlocked = BagManager.Instance.IsBagUnlocked(bagSlot);

                if (!unlocked)
                {
                    // Locked card
                    if (bagLockedCardPrefab != null)
                    {
                        var lockedGO = Instantiate(bagLockedCardPrefab, contentParent);
                        var le = lockedGO.GetComponent<LayoutElement>();
                        if (le == null) le = lockedGO.AddComponent<LayoutElement>();
                        le.preferredWidth = 135f;
                        le.preferredHeight = 165f;
                    }
                    continue;
                }

                // Unlocked card
                var cardGO = Instantiate(bagCardPrefab, contentParent);
                var cardLE = cardGO.GetComponent<LayoutElement>();
                if (cardLE == null) cardLE = cardGO.AddComponent<LayoutElement>();
                cardLE.preferredWidth = 135f;
                cardLE.preferredHeight = 165f;

                var card = cardGO.GetComponent<BagThumbnailCard>();
                if (card != null)
                {
                    var bagData = allBags[i];
                    bool isEquipped = BagManager.Instance.EquippedBagSlot == bagSlot;
                    card.Initialize(bagSlot, bagData, isEquipped);
                    int slot = bagSlot;
                    card.OnClicked += () => SelectBag(slot);
                    cards.Add(card);
                }
            }

            // Pad to minCardCount with locked prefabs
            int currentCount = contentParent.childCount;
            for (int i = currentCount; i < minCardCount; i++)
            {
                if (bagLockedCardPrefab == null) break;
                var extraLocked = Instantiate(bagLockedCardPrefab, contentParent);
                var le = extraLocked.GetComponent<LayoutElement>();
                if (le == null) le = extraLocked.AddComponent<LayoutElement>();
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
            totalPages = Mathf.CeilToInt(cards.Count > 0 ? (float)cards.Count / cardsPerPage : 1);
            currentPage = 0;
            paginationDots.Clear();
            if (paginationDotsParent == null) return;
            foreach (Transform child in paginationDotsParent) Destroy(child.gameObject);
            for (int i = 0; i < totalPages; i++)
            {
                Image dotImg;
                if (paginationDotPrefab != null)
                    dotImg = Instantiate(paginationDotPrefab, paginationDotsParent).GetComponent<Image>();
                else
                {
                    var dotGO = new GameObject($"Dot_{i}", typeof(RectTransform), typeof(Image));
                    dotGO.transform.SetParent(paginationDotsParent, false);
                    dotGO.GetComponent<RectTransform>().sizeDelta = new Vector2(12f, 12f);
                    dotImg = dotGO.GetComponent<Image>();
                }
                if (dotImg != null) paginationDots.Add(dotImg);
            }
            RefreshDotColors();
            UpdateArrowButtonStates();
        }

        private void RefreshDotColors()
        {
            for (int i = 0; i < paginationDots.Count; i++)
                if (paginationDots[i] != null)
                    paginationDots[i].color = i == currentPage ? Color.white : new Color(1f, 1f, 1f, 0.35f);
        }

        private void UpdateArrowButtonStates()
        {
            if (leftArrowButton  != null) leftArrowButton.interactable  = currentPage > 0;
            if (rightArrowButton != null) rightArrowButton.interactable = currentPage < totalPages - 1;
        }
    }
}
