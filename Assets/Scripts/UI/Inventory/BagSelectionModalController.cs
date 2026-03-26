#nullable enable
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Golfin.UI.Modals;

namespace Golfin.Inventory
{
    /// <summary>
    /// Bag Selection Modal — Phase E3.
    ///
    /// Shows a 5×2 grid of bag slots when the player taps EQUIP on a club.
    /// Player picks which bag to assign the club to.
    /// Slot 1 is unlocked at start; slots 2–10 are locked.
    /// Bags at capacity (8/8) block assignment and show a toast.
    /// </summary>
    public class BagSelectionModalController : ModalController
    {
        [Header("Bag Grid")]
        [SerializeField] private Transform   bagGridParent = null!;
        [SerializeField] private GameObject  bagSlotPrefab = null!;

        [Header("Cancel")]
        [SerializeField] private Button cancelButton = null!;

        // ── State ─────────────────────────────────────────────────────────────
        private string currentClubId = "";
        private readonly List<GameObject> spawnedSlots = new();

        // ── Colors ────────────────────────────────────────────────────────────
        private static readonly Color LockedColor    = new Color(0.4f, 0.4f, 0.4f, 0.5f);
        private static readonly Color AvailableColor = Color.white;

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
            // Destroy previously spawned slots
            foreach (var slot in spawnedSlots)
                if (slot != null) Destroy(slot);
            spawnedSlots.Clear();

            if (bagSlotPrefab == null || bagGridParent == null) return;
            if (BagManager.Instance == null || ClubManager.Instance == null) return;

            var currentClub = ClubManager.Instance.GetClubData(currentClubId);

            for (int i = 1; i <= BagManager.MAX_BAGS; i++)
            {
                int bagSlot = i; // capture for lambda
                var slotGO = Instantiate(bagSlotPrefab, bagGridParent);
                slotGO.SetActive(true);
                spawnedSlots.Add(slotGO);

                bool unlocked  = BagManager.Instance.IsBagUnlocked(bagSlot);
                bool full      = unlocked && BagManager.Instance.IsBagFull(bagSlot);
                int  count     = unlocked ? BagManager.Instance.GetClubCountInBag(bagSlot) : 0;
                bool hasClub   = currentClub != null && currentClub.equippedBagSlot == bagSlot;

                // ── Child references (by name, matches AutoWire-built hierarchy) ──
                var bagImage     = FindChild<Image>(slotGO, "BagImage");
                var bagLabel     = FindChild<TextMeshProUGUI>(slotGO, "BagLabel");
                var countLabel   = FindChild<TextMeshProUGUI>(slotGO, "CountLabel");
                var fullBadge    = FindChildGO(slotGO, "FullBadge");
                var equippedIcon = FindChildGO(slotGO, "EquippedIcon");

                // ── Thumbnail ──────────────────────────────────────────────────
                if (bagImage != null)
                {
                    string thumbName = BagManager.GetBagThumbnailName(bagSlot);
                    var sprite = Resources.Load<Sprite>($"Bags/Thumbnail/{thumbName}");
                    if (sprite == null)
                        sprite = Resources.Load<Sprite>("Bags/Thumbnail/Mireo");
                    if (sprite != null) bagImage.sprite = sprite;
                    bagImage.color = unlocked ? AvailableColor : LockedColor;
                }

                // ── Labels ─────────────────────────────────────────────────────
                if (bagLabel != null)
                    bagLabel.text = unlocked
                        ? $"BAG {bagSlot}"
                        : LocalizationManager.Get("BAG_LOCKED").ToUpper();

                if (countLabel != null)
                {
                    countLabel.gameObject.SetActive(unlocked);
                    if (unlocked)
                        countLabel.text = $"{count}/{BagManager.MAX_CLUBS_PER_BAG}";
                }

                // ── Badges ─────────────────────────────────────────────────────
                fullBadge?.SetActive(full);
                equippedIcon?.SetActive(hasClub);

                // ── Click handler ──────────────────────────────────────────────
                var btn = slotGO.GetComponent<Button>();
                if (btn == null) btn = slotGO.AddComponent<Button>();
                btn.interactable = unlocked;
                if (unlocked)
                {
                    btn.onClick.RemoveAllListeners();
                    btn.onClick.AddListener(() => OnSlotClicked(bagSlot));
                }
            }
        }

        // ── Slot Click ────────────────────────────────────────────────────────

        private void OnSlotClicked(int bagSlot)
        {
            if (BagManager.Instance == null) return;

            if (BagManager.Instance.IsBagFull(bagSlot))
            {
                Debug.Log($"[BagSelectionModal] Bag {bagSlot} is full ({BagManager.MAX_CLUBS_PER_BAG}/{BagManager.MAX_CLUBS_PER_BAG}). Remove a club first."); // TODO: Toast
                return;
            }

            BagManager.Instance.AssignClubToBag(currentClubId, bagSlot);
            Hide();
            // ClubDetailPanel refreshes automatically via OnClubEquipped event
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static T? FindChild<T>(GameObject parent, string childName) where T : Component
        {
            var t = parent.transform.Find(childName);
            return t == null ? null : t.GetComponent<T>();
        }

        private static GameObject? FindChildGO(GameObject parent, string childName)
        {
            var t = parent.transform.Find(childName);
            return t == null ? null : t.gameObject;
        }
    }
}
