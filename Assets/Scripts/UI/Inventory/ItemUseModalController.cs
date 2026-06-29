#nullable enable
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Golfin.UI.Modals;

namespace Golfin.Inventory
{
    /// <summary>
    /// Modal for selecting which club to apply a repair kit to.
    /// Shows all owned clubs in a scrollable 4-column grid with stats.
    /// Each card has a "USE REPAIR KIT" button that consumes the item and repairs the club.
    ///
    /// Opened by ItemDetailPanel.OnUseClicked().
    /// </summary>
    public class ItemUseModalController : ModalController
    {
        [Header("Modal UI")]
        [SerializeField] private TextMeshProUGUI titleText      = null!;  // "SELECT CLUB"
        [SerializeField] private Button          cancelButton   = null!;
        [SerializeField] private Image           backgroundImage = null!; // RepairBackground.png

        [Header("Filter Bar")]
        [SerializeField] private ClubFilterBar? filterBar;

        [Header("Club Grid (Scrollable)")]
        [SerializeField] private Transform  gridParent     = null!;  // GridContent inside ScrollRect
        [SerializeField] private ScrollRect scrollRect     = null!;
        [SerializeField] private GameObject clubCardPrefab = null!;  // ItemUseClubCard prefab

        // ── State ──────────────────────────────────────────────────────────────
        private string currentItemId   = "";
        private int    restorePercent  = 0;
        private readonly List<GameObject> spawnedCards = new();

        // ── Lifecycle ──────────────────────────────────────────────────────────

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

        // ── Public API ─────────────────────────────────────────────────────────

        /// <summary>
        /// Opens the modal for a specific item (repair kit).
        /// Called from ItemDetailPanel.OnUseClicked().
        /// </summary>
        public void Open(string itemId)
        {
            currentItemId = itemId;

            var template   = ItemDatabaseCSV.Instance?.GetItem(itemId);
            restorePercent = template?.restorePercent ?? 0;

            if (titleText != null)
                titleText.text = LocalizationManager.Get("ITEM_SELECT_CLUB");

            // Reset filter to ALL
            if (filterBar != null)
                filterBar.SetFilter(0);

            BuildClubCards(null); // null = ALL
            Show();
        }

        // ── Filter ─────────────────────────────────────────────────────────────

        private void OnFilterChanged(ClubType? filter)
        {
            BuildClubCards(filter);
        }

        // ── Card Building ──────────────────────────────────────────────────────

        private void BuildClubCards(ClubType? filter)
        {
            // Clear previous cards
            foreach (var card in spawnedCards)
                if (card != null) Destroy(card);
            spawnedCards.Clear();

            if (ClubManager.Instance == null || ClubDatabaseCSV.Instance == null) return;

            // Get clubs (respecting unified WEDGES tab)
            List<PlayerClubData> clubs;
            if (filter == null)
            {
                clubs = ClubManager.Instance.GetAllOwnedClubs();
            }
            else if (filterBar != null && filterBar.IsWedgeFilter)
            {
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

            foreach (var playerClub in clubs)
            {
                var template = ClubDatabaseCSV.Instance.GetClub(playerClub.clubId);
                if (template == null) continue;

                var cardGO   = Instantiate(clubCardPrefab, gridParent);
                spawnedCards.Add(cardGO);

                var cardComp = cardGO.GetComponent<ItemUseClubCard>();
                if (cardComp != null)
                {
                    bool needsRepair = playerClub.currentDurability < playerClub.maxDurability;
                    cardComp.Initialize(playerClub, template, restorePercent, needsRepair);

                    var capturedId = playerClub.clubId;
                    cardComp.OnUseRepairKit += () => OnRepairKitUsed(capturedId);
                }
            }

            // Reset scroll to top
            if (scrollRect != null)
                scrollRect.verticalNormalizedPosition = 1f;
        }

        // ── Repair Action ──────────────────────────────────────────────────────

        private void OnRepairKitUsed(string clubId)
        {
            if (string.IsNullOrEmpty(currentItemId)) return;

            var playerClub = ClubManager.Instance?.GetClubData(clubId);
            if (playerClub == null) return;

            // Calculate new durability based on this kit's restorePercent
            int restored      = Mathf.CeilToInt(playerClub.maxDurability * restorePercent / 100f);
            int newDurability = Mathf.Min(playerClub.currentDurability + restored, playerClub.maxDurability);

            // Consume the item
            ItemManager.Instance?.UseItem(currentItemId);

            // Apply repair (fires ClubManager.OnClubRepaired which refreshes ClubDetailPanel)
            ClubManager.Instance?.RepairClub(clubId, newDurability);

            var template = ClubDatabaseCSV.Instance?.GetClub(clubId);
            Debug.Log($"[ItemUseModal] Used {currentItemId} on {template?.name ?? clubId}. " +
                      $"Durability {playerClub.currentDurability} → {newDurability}.");

            // Close modal — ItemDetailPanel refreshes via ItemManager.OnInventoryChanged
            Hide();
        }
    }
}
