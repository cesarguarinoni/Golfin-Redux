#nullable enable
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Golfin.Roster;

namespace Golfin.Inventory
{
    /// <summary>
    /// Detail panel for the BAGS tab. Shows bag info + 8-slot club grid.
    /// Subscribes to BagCarouselController.OnBagSelected.
    /// </summary>
    public class BagDetailPanel : MonoBehaviour
    {
        [Header("Info Area")]
        [SerializeField] private Image           bagFullImage    = null!;
        [SerializeField] private TextMeshProUGUI bagNameText     = null!;
        [SerializeField] private GameObject       equippedIcon    = null!;
        [SerializeField] private TextMeshProUGUI descriptionText  = null!;

        [Header("Club Grid")]
        [SerializeField] private Transform  clubGridParent     = null!;  // 4×2 GridLayoutGroup
        [SerializeField] private GameObject clubCardPrefab      = null!;  // BagClubCard.prefab — must have BagClubCard component
        [SerializeField] private GameObject emptyClubCardPrefab = null!;  // BagEmptyClubCard

        [Header("Equip Bag Button")]
        [SerializeField] private Button          equipBagButton  = null!;
        [SerializeField] private TextMeshProUGUI equipBagText    = null!;

        [Header("Modal")]
        [SerializeField] private BagClubModalController? clubModal;

        // ── State ──────────────────────────────────────────────────────────
        private int currentBagSlot = 0;
        private readonly List<GameObject> spawnedCards = new();

        // ── Lifecycle ──────────────────────────────────────────────────────

        private void Awake()
        {
            equipBagButton?.onClick.AddListener(OnEquipBagClicked);
        }

        private void OnEnable()
        {
            if (BagManager.Instance != null)
            {
                BagManager.Instance.OnBagChanged += OnBagContentsChanged;
                BagManager.Instance.OnEquippedBagChanged += OnEquippedBagChanged;
            }
            if (ClubManager.Instance != null)
                ClubManager.Instance.OnClubEquipped += OnClubEquipStateChanged;
        }

        private void OnDisable()
        {
            if (BagManager.Instance != null)
            {
                BagManager.Instance.OnBagChanged -= OnBagContentsChanged;
                BagManager.Instance.OnEquippedBagChanged -= OnEquippedBagChanged;
            }
            if (ClubManager.Instance != null)
                ClubManager.Instance.OnClubEquipped -= OnClubEquipStateChanged;
        }

        // ── Public API (called by carousel) ────────────────────────────────

        public void ShowBag(int bagSlot)
        {
            currentBagSlot = bagSlot;
            var bagData = BagDatabaseCSV.Instance?.GetBagBySlot(bagSlot);
            if (bagData == null) return;

            // Info area
            if (bagFullImage != null && bagData.fullImageSprite != null)
                bagFullImage.sprite = bagData.fullImageSprite;
            else if (bagFullImage != null && bagData.thumbnailSprite != null)
                bagFullImage.sprite = bagData.thumbnailSprite; // fallback

            if (bagNameText != null)
                bagNameText.text = bagData.name.ToUpper();

            if (descriptionText != null)
                descriptionText.text = bagData.description;

            RefreshEquipButton();
            BuildClubGrid();
        }

        // ── Club Grid ──────────────────────────────────────────────────────

        private void BuildClubGrid()
        {
            foreach (var card in spawnedCards)
                if (card != null) Destroy(card);
            spawnedCards.Clear();

            if (BagManager.Instance == null || clubGridParent == null) return;

            var clubsInBag = BagManager.Instance.GetClubsInBag(currentBagSlot);
            int maxSlots = BagManager.MAX_CLUBS_PER_BAG; // 8

            // Equipped clubs
            for (int i = 0; i < clubsInBag.Count && i < maxSlots; i++)
            {
                var playerClub = clubsInBag[i];
                var template = ClubDatabaseCSV.Instance?.GetClub(playerClub.clubId);
                if (template == null) continue;

                var cardGO = Instantiate(clubCardPrefab, clubGridParent);
                spawnedCards.Add(cardGO);

                // Use BagClubCard directly — same component as modal, stat bars guaranteed wired
                var card = cardGO.GetComponent<BagClubCard>();
                if (card != null)
                {
                    var capturedClubId = playerClub.clubId;
                    card.Initialize(playerClub, template, LocalizationManager.Get("BAG_SWAP"));
                    card.OnActionClicked += () => OpenModal(BagClubModalMode.Swap, capturedClubId);
                }
            }

            // Empty slots
            for (int i = clubsInBag.Count; i < maxSlots; i++)
            {
                if (emptyClubCardPrefab == null) break;
                var emptyGO = Instantiate(emptyClubCardPrefab, clubGridParent);
                spawnedCards.Add(emptyGO);

                var equipBtn = emptyGO.GetComponentInChildren<Button>();
                if (equipBtn != null)
                {
                    equipBtn.onClick.RemoveAllListeners();
                    equipBtn.onClick.AddListener(() => OpenModal(BagClubModalMode.Equip, null));
                }
            }
        }

        // ── Modal ──────────────────────────────────────────────────────────

        private void OpenModal(BagClubModalMode mode, string? existingClubId)
        {
            if (clubModal != null)
                clubModal.Open(currentBagSlot, mode, existingClubId);
        }

        // ── Equip Bag Button ───────────────────────────────────────────────

        private void OnEquipBagClicked()
        {
            if (BagManager.Instance == null) return;
            BagManager.Instance.EquipBag(currentBagSlot);
        }

        private void RefreshEquipButton()
        {
            if (BagManager.Instance == null) return;
            bool isEquipped = BagManager.Instance.EquippedBagSlot == currentBagSlot;

            if (equippedIcon != null)
                equippedIcon.SetActive(isEquipped);

            if (equipBagText != null)
                equipBagText.text = isEquipped
                    ? LocalizationManager.Get("BAG_EQUIPPED")
                    : LocalizationManager.Get("BAG_EQUIP");

            if (equipBagButton != null)
                equipBagButton.interactable = !isEquipped;
        }

        // ── Event Handlers ─────────────────────────────────────────────────

        private void OnBagContentsChanged(int bagSlot)
        {
            if (bagSlot == currentBagSlot)
                BuildClubGrid();
        }

        private void OnEquippedBagChanged(int newSlot)
        {
            RefreshEquipButton();
        }

        private void OnClubEquipStateChanged(string clubId)
        {
            BuildClubGrid();
        }

        // ── Helpers ────────────────────────────────────────────────────────

        private static T? FindChild<T>(GameObject parent, string path) where T : Component
        {
            var t = parent.transform.Find(path);
            return t == null ? null : t.GetComponent<T>();
        }
    }
}
