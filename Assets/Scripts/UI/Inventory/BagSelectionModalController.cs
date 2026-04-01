#nullable enable
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Golfin.UI.Modals;
using Golfin.Roster;   // RarityHelper

namespace Golfin.Inventory
{
    /// <summary>
    /// Bag Selection Modal — Phase E3 / E3b.
    ///
    /// Shows a grid of bag slots when the player taps EQUIP on a club.
    /// Uses production-styled prefabs by Kai — only binds data into existing children.
    ///   bagSlotPrefab       — unlocked bag slot
    ///   bagSlotLockedPrefab — locked bag slot (no data binding, already styled)
    /// </summary>
    public class BagSelectionModalController : ModalController
    {
        [Header("Bag Grid")]
        [SerializeField] private Transform  bagGridParent       = null!;
        [SerializeField] private GameObject bagSlotPrefab       = null!;   // unlocked bags
        [SerializeField] private GameObject bagSlotLockedPrefab = null!;   // locked bags

        [Header("Cancel")]
        [SerializeField] private Button cancelButton = null!;

        // ── State ─────────────────────────────────────────────────────────────
        private string currentClubId = "";
        private readonly List<GameObject> spawnedSlots = new();

        // ── Lifecycle ─────────────────────────────────────────────────────────

        protected override void Awake()
        {
            base.Awake();
            cancelButton?.onClick.AddListener(Hide);
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>Opens the modal for a specific club. Called from ClubDetailPanel / ClubCompareController.</summary>
        public void Open(string clubId)
        {
            currentClubId = clubId;
            BuildSlots();
            Show();
        }

        // ── Slot Building ─────────────────────────────────────────────────────

        private void BuildSlots()
        {
            foreach (var slot in spawnedSlots)
                if (slot != null) Destroy(slot);
            spawnedSlots.Clear();

            if (bagGridParent == null) return;

            // Hide any scene-object templates sitting inside the grid (do NOT call SetActive on prefab assets)
            foreach (Transform child in bagGridParent)
                if (child.name == "BagSlotPrefab" || child.name == "BagSlotLockedPrefab")
                    child.gameObject.SetActive(false);

            if (BagManager.Instance == null || ClubManager.Instance == null) return;

            var currentClub = ClubManager.Instance.GetClubData(currentClubId);
            int bagCount = BagDatabaseCSV.Instance != null
                ? BagDatabaseCSV.Instance.GetBagCount()
                : BagManager.MAX_BAGS;

            for (int i = 1; i <= bagCount; i++)
            {
                int bagSlot = i; // capture for lambda
                bool unlocked = BagManager.Instance.IsBagUnlocked(bagSlot);

                if (!unlocked)
                {
                    // Locked — use locked prefab as-is, no data binding (Kai's prefab already shows "LOCKED")
                    if (bagSlotLockedPrefab == null) continue;
                    var lockedGO = Instantiate(bagSlotLockedPrefab, bagGridParent);
                    lockedGO.SetActive(true);
                    spawnedSlots.Add(lockedGO);
                    continue;
                }

                // Unlocked — use regular prefab, bind data
                if (bagSlotPrefab == null) continue;
                var slotGO = Instantiate(bagSlotPrefab, bagGridParent);
                slotGO.SetActive(true);
                spawnedSlots.Add(slotGO);

                var bagData  = BagDatabaseCSV.Instance?.GetBagBySlot(bagSlot);
                bool full    = BagManager.Instance.IsBagFull(bagSlot);
                bool hasClub = currentClub != null && currentClub.equippedBagSlot == bagSlot;

                // ── BagImage ───────────────────────────────────────────────────
                var bagImage = FindChild<Image>(slotGO, "BagImage");
                if (bagImage != null && bagData?.thumbnailSprite != null)
                    bagImage.sprite = bagData.thumbnailSprite;

                // ── BagLabel ───────────────────────────────────────────────────
                var bagLabel = FindChild<TextMeshProUGUI>(slotGO, "BagLabel");
                if (bagLabel != null)
                    bagLabel.text = bagData != null ? bagData.name.ToUpper() : $"BAG {bagSlot}";

                // ── RarityBadge ────────────────────────────────────────────────
                if (bagData != null)
                {
                    var rarityBadge = FindChild<Image>(slotGO, "RarityBadge");
                    if (rarityBadge != null)
                    {
                        var raritySprite = Resources.Load<Sprite>($"Rarities/{bagData.rarity}");
                        if (raritySprite != null) rarityBadge.sprite = raritySprite;
                    }

                    var rarityText = FindChild<TextMeshProUGUI>(slotGO, "RarityBadge/Text");
                    if (rarityText != null)
                    {
                        rarityText.text  = RarityHelper.GetRarityLabel(bagData.rarity);
                        rarityText.color = RarityHelper.GetRarityBadgeTextColor(bagData.rarity);
                    }
                }

                // ── FullBadge / EquippedIcon ───────────────────────────────────
                FindChildGO(slotGO, "FullBadge")?.SetActive(full);
                FindChildGO(slotGO, "EquippedIcon")?.SetActive(hasClub);

                // ── Click handler ──────────────────────────────────────────────
                var btn = slotGO.GetComponent<Button>();
                if (btn == null) btn = slotGO.AddComponent<Button>();
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => OnSlotClicked(bagSlot));
            }
        }

        // ── Slot Click ────────────────────────────────────────────────────────

        private void OnSlotClicked(int bagSlot)
        {
            if (BagManager.Instance == null) return;

            if (BagManager.Instance.IsBagFull(bagSlot))
            {
                Debug.Log($"[BagSelectionModal] Bag {bagSlot} is full. Remove a club first."); // TODO: Toast
                return;
            }

            BagManager.Instance.AssignClubToBag(currentClubId, bagSlot);
            Hide();
            // ClubDetailPanel refreshes automatically via OnClubEquipped event
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static T? FindChild<T>(GameObject parent, string path) where T : Component
        {
            var t = parent.transform.Find(path);
            return t == null ? null : t.GetComponent<T>();
        }

        private static GameObject? FindChildGO(GameObject parent, string path)
        {
            var t = parent.transform.Find(path);
            return t == null ? null : t.gameObject;
        }
    }
}
