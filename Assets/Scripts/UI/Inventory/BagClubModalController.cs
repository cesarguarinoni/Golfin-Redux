#nullable enable
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Golfin.UI.Modals;

namespace Golfin.Inventory
{
    public enum BagClubModalMode
    {
        Swap,   // replace an existing club in the bag
        Equip   // fill an empty slot
    }

    /// <summary>
    /// Modal for swapping or equipping a club in a bag slot.
    /// Shows all owned clubs in a scrollable 4-column grid with filter bar.
    /// Mode determines title, button labels, and behavior.
    /// </summary>
    public class BagClubModalController : ModalController
    {
        [Header("Modal UI")]
        [SerializeField] private TextMeshProUGUI titleText    = null!;
        [SerializeField] private Button          cancelButton = null!;

        [Header("Filter Bar")]
        [SerializeField] private ClubFilterBar? filterBar;

        [Header("Club Grid (Scrollable)")]
        [SerializeField] private Transform  gridParent     = null!;
        [SerializeField] private ScrollRect scrollRect     = null!;
        [SerializeField] private GameObject clubCardPrefab = null!; // BagClubCard prefab

        // ── State ──────────────────────────────────────────────────────────
        private int currentBagSlot = 0;
        private BagClubModalMode mode = BagClubModalMode.Equip;
        private string? swapClubId = null; // club being replaced (Swap mode only)
        private readonly List<GameObject> spawnedCards = new();

        // ── Lifecycle ──────────────────────────────────────────────────────

        protected override void Awake()
        {
            base.Awake();
            cancelButton?.onClick.AddListener(Hide);
        }

        private void OnEnable()
        {
            if (filterBar != null)
                filterBar.OnFilterChanged += OnFilterChanged;
        }

        protected override void OnDisable()
        {
            // Call base FIRST so the S2 OpenModalCount leak guard fires.
            base.OnDisable();
            if (filterBar != null)
                filterBar.OnFilterChanged -= OnFilterChanged;
        }

        // ── Public API ─────────────────────────────────────────────────────

        public void Open(int bagSlot, BagClubModalMode openMode, string? existingClubId)
        {
            currentBagSlot = bagSlot;
            mode = openMode;
            swapClubId = existingClubId;

            if (titleText != null)
                titleText.text = mode == BagClubModalMode.Swap
                    ? LocalizationManager.Get("BAG_SWAP_CLUB")
                    : LocalizationManager.Get("BAG_EQUIP_CLUB");

            if (filterBar != null)
                filterBar.SetFilter(0);

            BuildClubCards(null);
            Show();
        }

        // ── Filter ─────────────────────────────────────────────────────────

        private void OnFilterChanged(ClubType? filter)
        {
            BuildClubCards(filter);
        }

        // ── Card Building ──────────────────────────────────────────────────

        private void BuildClubCards(ClubType? filter)
        {
            foreach (var card in spawnedCards)
                if (card != null) Destroy(card);
            spawnedCards.Clear();

            if (ClubManager.Instance == null || ClubDatabaseCSV.Instance == null) return;

            List<PlayerClubData> clubs = GetFilteredClubs(filter);

            // Get clubs already in this bag (to exclude them)
            var clubsInBag = BagManager.Instance != null
                ? BagManager.Instance.GetClubsInBag(currentBagSlot)
                : new List<PlayerClubData>();
            var inBagIds = new HashSet<string>();
            foreach (var c in clubsInBag)
                inBagIds.Add(c.clubId);

            string actionLabel = mode == BagClubModalMode.Swap
                ? LocalizationManager.Get("BAG_SWAP")
                : LocalizationManager.Get("BAG_EQUIP_ACTION");

            foreach (var playerClub in clubs)
            {
                // Skip clubs already in this bag (except the one being swapped)
                if (inBagIds.Contains(playerClub.clubId) && playerClub.clubId != swapClubId)
                    continue;

                var template = ClubDatabaseCSV.Instance.GetClub(playerClub.clubId);
                if (template == null) continue;

                var cardGO = Instantiate(clubCardPrefab, gridParent);
                spawnedCards.Add(cardGO);

                var cardComp = cardGO.GetComponent<BagClubCard>();
                if (cardComp != null)
                {
                    cardComp.Initialize(playerClub, template, actionLabel);
                    var capturedId = playerClub.clubId;
                    cardComp.OnActionClicked += () => OnCardAction(capturedId);
                }
            }

            if (scrollRect != null)
                scrollRect.verticalNormalizedPosition = 1f;
        }

        private List<PlayerClubData> GetFilteredClubs(ClubType? filter)
        {
            if (filter == null)
                return ClubManager.Instance!.GetAllOwnedClubs();

            if (filterBar != null && filterBar.IsWedgeFilter)
            {
                var a = ClubManager.Instance!.GetOwnedClubsOfType(ClubType.A_Wedge);
                var p = ClubManager.Instance.GetOwnedClubsOfType(ClubType.P_Wedge);
                var s = ClubManager.Instance.GetOwnedClubsOfType(ClubType.S_Wedge);
                var combined = new List<PlayerClubData>(a.Count + p.Count + s.Count);
                combined.AddRange(a); combined.AddRange(p); combined.AddRange(s);
                return combined;
            }

            return ClubManager.Instance!.GetOwnedClubsOfType(filter.Value);
        }

        // ── Action ─────────────────────────────────────────────────────────

        private void OnCardAction(string selectedClubId)
        {
            if (BagManager.Instance == null) return;

            if (mode == BagClubModalMode.Swap && !string.IsNullOrEmpty(swapClubId))
            {
                // Remove old club from bag first
                BagManager.Instance.RemoveClubFromBag(swapClubId);
            }

            // Assign new club to bag
            BagManager.Instance.AssignClubToBag(selectedClubId, currentBagSlot);

            Debug.Log($"[BagClubModal] {mode}: '{selectedClubId}' → Bag {currentBagSlot}" +
                      (mode == BagClubModalMode.Swap ? $" (replaced '{swapClubId}')" : ""));

            Hide();
            // BagDetailPanel refreshes via BagManager.OnBagChanged
        }
    }
}
