# TellCode.md — Instructions from Claude (Architect) to Claude Code

> Claude Code: Read this file at the start of each task. Execute the latest instruction block.
> After completing, add a status line at the bottom: `✅ DONE: [date] [brief summary]`
> Claude (Architect) will update this file with new instructions as needed.

---

## Current Task (2026-04-01) — Phase J: Bags Inventory Screen

The BAGS tab (index 1) in the Inventory screen. Shows the player's bags in a horizontal
carousel, a detail panel with bag info and an 8-slot club grid, and two modals (Swap Club /
Equip Club) for managing club assignments.

### Reference Mockups

See uploaded images: `Bags_Screen_-_Menu.png`, `Bags_Screen_-_Swap_Club.png`,
`Bags_Screen_-_Equip_Club.png` in project knowledge.

---

### IMPORTANT — Prefab / UI Policy

**Claude Code must NOT build UI hierarchies from scratch.**
Kai will create/clone all prefabs and scene panels by hand in Unity.
Claude Code only writes `.cs` scripts, editor auto-wire scripts, and CSV changes.
Kai will wire SerializeField references manually in Inspector after verifying the hierarchy.
The auto-wire scripts are provided as a convenience — Kai runs them, checks the result,
and fixes anything that doesn't match.

---

## Phase J Overview

```
BagsContent (tab panel, index 1)
├── BagCarousel (horizontal scroll, BagCarouselController)
│   └── [BagThumbnailCard / BagSlotLockedPrefab instances]
├── BagDetailPanel (BagDetailPanel)
│   ├── InfoArea (bag image, name, equipped icon, description)
│   └── ClubGrid (4×2 grid of BagSwapClubCard / BagEmptyClubCard)
├── EquipBagButton (gold "EQUIPPED" / silver "EQUIP")
└── BagClubModal (BagClubModalController : ModalController)
    ├── FilterBar (ClubFilterBar)
    ├── ScrollArea (4-column grid of BagClubCard)
    └── CancelButton
```

---

### Step J1: Update Bags.csv — Add `description` and `fullImage` columns

**File:** `Assets/Data/Bags.csv`

Add two new columns: `description` and `fullImage`.

```csv
id,name,rarity,thumbnail,fullImage,description,unlocked
bag_mireo,Mireo,Rare,Mireo,Mireo,"Add any 8 clubs you want to take out to the field to your bag. Remember you always need at least 1 Driver and 1 Putter.",true
bag_golfin,Golfin,Common,,,,false
bag_locked3,Bag 3,Common,,,,false
bag_locked4,Bag 4,Common,,,,false
bag_locked5,Bag 5,Common,,,,false
bag_locked6,Bag 6,Common,,,,false
bag_locked7,Bag 7,Common,,,,false
bag_locked8,Bag 8,Common,,,,false
bag_locked9,Bag 9,Common,,,,false
bag_locked10,Bag 10,Common,,,,false
```

Full-size bag sprites go in: `Resources/Bags/Full/{name}.png`
Thumbnail sprites (already exist): `Resources/Bags/Thumbnail/{name}.png`

---

### Step J2: Update `BagDataRuntime` + `BagDatabaseCSV`

**File:** `Assets/Scripts/BagDatabaseCSV.cs`

**A) Add fields to `BagDataRuntime`:**

```csharp
// Add after existing fields:
public string description     = "";
public string fullImageName   = "";         // filename in Resources/Bags/Full/
public Sprite? fullImageSprite = null;      // loaded from Resources
```

**B) Update `ParseRow()` to read the new columns:**

Add after the `thumbnail` line:
```csharp
string fullImage   = Get("fullImage");
string description = Get("description");
```

Add sprite loading after the thumbnail sprite load:
```csharp
Sprite? fullSprite = null;
if (!string.IsNullOrEmpty(fullImage))
    fullSprite = Resources.Load<Sprite>($"Bags/Full/{fullImage}");
```

Add to the `return new BagDataRuntime { ... }` block:
```csharp
description      = description,
fullImageName    = fullImage,
fullImageSprite  = fullSprite,
```

---

### Step J3: Add `equippedBagSlot` to `BagManager`

**File:** `Assets/Scripts/BagManager.cs`

The "EQUIPPED" button means "this is the bag I take to the field." Only one bag
can be equipped at a time. Equipping a different bag unequips the previous one.

**A) Add state + event after existing fields:**

```csharp
/// <summary>The bag slot (1-based) currently equipped for gameplay. 0 = none.</summary>
public int EquippedBagSlot { get; private set; } = 0;

/// <summary>Fired when the equipped bag changes. Arg = new equippedBagSlot.</summary>
public event System.Action<int>? OnEquippedBagChanged;
```

**B) In Awake(), after the unlock loop, auto-equip the first unlocked bag:**

```csharp
// Auto-equip first unlocked bag
if (EquippedBagSlot == 0 && unlockedSlots.Count > 0)
{
    EquippedBagSlot = 1; // bag_mireo is slot 1
    Debug.Log($"[BagManager] Auto-equipped Bag {EquippedBagSlot}.");
}
```

**C) Add public method:**

```csharp
/// <summary>Equips a bag for gameplay. Only one bag can be equipped at a time.</summary>
public void EquipBag(int bagSlot)
{
    if (!IsBagUnlocked(bagSlot))
    {
        Debug.Log($"[BagManager] Cannot equip locked Bag {bagSlot}.");
        return;
    }
    int oldSlot = EquippedBagSlot;
    EquippedBagSlot = bagSlot;
    Debug.Log($"[BagManager] Equipped Bag {bagSlot} (was Bag {oldSlot}).");
    OnEquippedBagChanged?.Invoke(bagSlot);
}
```

---

### Step J4: Create `BagCarouselController.cs`

**File:** `Assets/Scripts/UI/Inventory/BagCarouselController.cs`
**Namespace:** `Golfin.Inventory`

Mirrors `BallCarouselController` pattern. Horizontal scroll, paginated, with locked slots.

```csharp
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
            int totalSlots = Mathf.Max(cards.Count + (allBags.Count - cards.Count), minCardCount);
            int currentCount = contentParent.childCount;
            for (int i = currentCount; i < totalSlots; i++)
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

        // ── Arrows + Pagination — same as BallCarouselController ────────
        // (Copy arrow/pagination logic from BallCarouselController verbatim)

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
```

---

### Step J5: Create `BagThumbnailCard.cs`

**File:** `Assets/Scripts/UI/Inventory/BagThumbnailCard.cs`
**Namespace:** `Golfin.Inventory`

Small portrait card in the carousel. Kai builds the prefab by hand — this binds data.
Child layout mirrors `BagSlotPrefab` from the BagSelectionModal:
- BagImage, BagLabel, RarityBadge (with Text child), EquippedIcon

```csharp
#nullable enable
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Golfin.Roster;

namespace Golfin.Inventory
{
    /// <summary>
    /// Thumbnail card for a bag in the carousel. Uses same child names as BagSlotPrefab:
    /// BagImage, BagLabel, RarityBadge, RarityBadge/Text, EquippedIcon.
    /// </summary>
    public class BagThumbnailCard : MonoBehaviour
    {
        [SerializeField] private Image?           bagImage;
        [SerializeField] private TextMeshProUGUI?  bagLabel;
        [SerializeField] private Image?           rarityBadgeImage;
        [SerializeField] private TextMeshProUGUI?  rarityBadgeText;
        [SerializeField] private Image?           backgroundImage;  // rarity-colored bg
        [SerializeField] private GameObject?       equippedIcon;

        public event System.Action? OnClicked;

        private int _bagSlot;
        private bool _selected;

        public void Initialize(int bagSlot, BagDataRuntime data, bool isEquipped)
        {
            _bagSlot = bagSlot;

            if (bagImage != null && data.thumbnailSprite != null)
                bagImage.sprite = data.thumbnailSprite;

            if (bagLabel != null)
                bagLabel.text = data.name.ToUpper();

            // Rarity badge
            if (rarityBadgeImage != null)
            {
                var raritySprite = Resources.Load<Sprite>($"Rarities/{data.rarity}");
                if (raritySprite != null) rarityBadgeImage.sprite = raritySprite;
            }
            if (rarityBadgeText != null)
            {
                rarityBadgeText.text = RarityHelper.GetRarityLabel(data.rarity);
                rarityBadgeText.color = RarityHelper.GetRarityBadgeTextColor(data.rarity);
            }

            // Rarity background
            if (backgroundImage != null)
            {
                var bgSprite = Resources.Load<Sprite>($"Rarities/{data.rarity}");
                if (bgSprite != null) backgroundImage.sprite = bgSprite;
            }

            SetEquipped(isEquipped);

            // Click handler
            var btn = GetComponent<Button>();
            if (btn == null) btn = gameObject.AddComponent<Button>();
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => OnClicked?.Invoke());
        }

        public void SetEquipped(bool equipped)
        {
            if (equippedIcon != null) equippedIcon.SetActive(equipped);
        }

        public void SetSelected(bool selected)
        {
            _selected = selected;
            // Scale animation: selected = 1.08, unselected = 1.0
            transform.localScale = selected ? Vector3.one * 1.08f : Vector3.one;
        }

        public int GetBagSlot() => _bagSlot;
    }
}
```

**NOTE:** Kai creates the BagThumbnailCard prefab by cloning BagSlotPrefab from the
Bag Selection Modal. Same child structure (BagImage, BagLabel, RarityBadge, EquippedIcon).
Save to: `Assets/Prefabs/UI/Inventory/BagThumbnailCard.prefab`

---

### Step J6: Create `BagDetailPanel.cs`

**File:** `Assets/Scripts/UI/Inventory/BagDetailPanel.cs`
**Namespace:** `Golfin.Inventory`

Shows the selected bag's info and its 8-slot club grid.

```csharp
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
        [SerializeField] private Image           bagFullImage   = null!;
        [SerializeField] private TextMeshProUGUI bagNameText    = null!;
        [SerializeField] private GameObject       equippedIcon   = null!;
        [SerializeField] private TextMeshProUGUI descriptionText = null!;

        [Header("Club Grid")]
        [SerializeField] private Transform  clubGridParent      = null!;  // 4×2 GridLayoutGroup
        [SerializeField] private GameObject clubCardPrefab       = null!;  // BagSwapClubCard
        [SerializeField] private GameObject emptyClubCardPrefab  = null!;  // BagEmptyClubCard

        [Header("Equip Bag Button")]
        [SerializeField] private Button          equipBagButton   = null!;
        [SerializeField] private TextMeshProUGUI equipBagText     = null!;

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

                // Bind data into existing prefab children (same as BagSelectionModal pattern)
                BindSwapClubCard(cardGO, playerClub, template);

                // Wire SWAP button
                var swapBtn = FindChild<Button>(cardGO, "SwapBtn");
                if (swapBtn != null)
                {
                    var capturedClubId = playerClub.clubId;
                    swapBtn.onClick.RemoveAllListeners();
                    swapBtn.onClick.AddListener(() => OpenModal(BagClubModalMode.Swap, capturedClubId));
                }

                // Wire LEVEL UP button (functional — opens club level up modal)
                // NOTE: For now, disabled in this context. Phase J+ can enable.
                var levelUpBtn = FindChild<Button>(cardGO, "LevelUpBtn");
                if (levelUpBtn != null) levelUpBtn.interactable = false;

                // Wire REPAIR button (functional — one-tap repair)
                // NOTE: For now, disabled in this context. Phase J+ can enable.
                var repairBtn = FindChild<Button>(cardGO, "RepairBtn");
                if (repairBtn != null) repairBtn.interactable = false;
            }

            // Empty slots
            for (int i = clubsInBag.Count; i < maxSlots; i++)
            {
                if (emptyClubCardPrefab == null) break;
                var emptyGO = Instantiate(emptyClubCardPrefab, clubGridParent);
                spawnedCards.Add(emptyGO);

                // Wire EQUIP CLUB button on empty card
                // NOTE: BagEmptyClubCard has "EquipBtn" child with Button component
                var equipBtn = emptyGO.GetComponentInChildren<Button>();
                if (equipBtn != null)
                {
                    equipBtn.onClick.RemoveAllListeners();
                    equipBtn.onClick.AddListener(() => OpenModal(BagClubModalMode.Equip, null));
                }
            }
        }

        private void BindSwapClubCard(GameObject cardGO, PlayerClubData playerClub, ClubDataRuntime template)
        {
            // Use ItemUseClubCard’s prefab children (same hierarchy):
            // CardTop (backgroundImage), Portrait, RarityBadge, LevelBadge,
            // NameText, StatsPanel rows, DistanceRow, ButtonRow, SwapBtn

            // NOTE: BagSwapClubCard prefab already has ItemUseClubCard component wired.
            // We use it for data binding only — the SwapBtn text is already "SWAP" in prefab.
            var cardComp = cardGO.GetComponent<ItemUseClubCard>();
            if (cardComp != null)
            {
                bool needsRepair = playerClub.currentDurability < playerClub.maxDurability;
                cardComp.Initialize(playerClub, template, 0, needsRepair);

                // Override the action button text to "SWAP" (prefab already says SWAP)
                var swapText = FindChild<TextMeshProUGUI>(cardGO, "SwapBtn/SwapText");
                if (swapText != null)
                    swapText.text = LocalizationManager.Get("BAG_SWAP");
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

            if (equipBagText != null)
                equipBagText.text = isEquipped
                    ? LocalizationManager.Get("BAG_EQUIPPED")
                    : LocalizationManager.Get("BAG_EQUIP");

            // Visual: gold for equipped, silver for unequipped
            // NOTE: Kai styles the button sprite swap. Code just sets text + interactable.
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
            // A club was equipped/unequipped — refresh if it affects current bag
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
```

---

### Step J7: Create `BagClubModalController.cs` + `BagClubCard.cs`

#### Enum: `BagClubModalMode`

Put this at the top of `BagClubModalController.cs` (or in a separate file if preferred):

```csharp
namespace Golfin.Inventory
{
    public enum BagClubModalMode
    {
        Swap,   // replace an existing club in the bag
        Equip   // fill an empty slot
    }
}
```

#### `BagClubModalController.cs`

**File:** `Assets/Scripts/UI/Inventory/BagClubModalController.cs`
**Namespace:** `Golfin.Inventory`
**Extends:** `ModalController`

Almost identical to `ItemUseModalController` but:
- Title is "SWAP CLUB" or "EQUIP CLUB" based on mode
- Action button on each card is "SWAP" or "EQUIP"
- Swap mode: removes the old club from bag, adds the new one
- Equip mode: just adds the new club to the bag
- Clubs already in the same bag are excluded from the list (except the one being swapped)

```csharp
#nullable enable
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Golfin.UI.Modals;

namespace Golfin.Inventory
{
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

        private void OnDisable()
        {
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

            // Get clubs (filtered)
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
```

#### `BagClubCard.cs`

**File:** `Assets/Scripts/UI/Inventory/BagClubCard.cs`
**Namespace:** `Golfin.Inventory`

New component for club cards inside the Bag modal. Same visual layout as `ItemUseClubCard`
but the action button says "SWAP" or "EQUIP" instead of "USE REPAIR KIT".
LEVEL UP and REPAIR buttons are always disabled.

**Prefab:** Kai clones `BagSwapClubCard.prefab`, replaces the `ItemUseClubCard` component
with `BagClubCard`, and saves as `Assets/Prefabs/UI/Inventory/BagClubCard.prefab`.
The child hierarchy is identical — same names, same structure.

```csharp
#nullable enable
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Golfin.Roster;

namespace Golfin.Inventory
{
    /// <summary>
    /// Club card inside the Bag Swap/Equip modal.
    /// Same visual layout as ItemUseClubCard (portrait, stats, buttons)
    /// but action button label is configurable ("SWAP" or "EQUIP").
    /// </summary>
    public class BagClubCard : MonoBehaviour
    {
        [Header("Card Top")]
        [SerializeField] private Image           backgroundImage  = null!;
        [SerializeField] private Image           portraitImage    = null!;
        [SerializeField] private TextMeshProUGUI nameText         = null!;
        [SerializeField] private TextMeshProUGUI rarityBadgeText  = null!;
        [SerializeField] private TextMeshProUGUI levelText        = null!;

        [Header("Stat Bars")]
        [SerializeField] private Image           statBarPower     = null!;
        [SerializeField] private TextMeshProUGUI statNumPower     = null!;
        [SerializeField] private Image           statBarAccuracy  = null!;
        [SerializeField] private TextMeshProUGUI statNumAccuracy  = null!;
        [SerializeField] private Image           statBarLieRes    = null!;
        [SerializeField] private TextMeshProUGUI statNumLieRes    = null!;
        [SerializeField] private Image           statBarLoft      = null!;
        [SerializeField] private TextMeshProUGUI statNumLoft      = null!;
        [SerializeField] private Image           statBarDurability = null!;
        [SerializeField] private TextMeshProUGUI statNumDurability = null!;

        [Header("Distance")]
        [SerializeField] private TextMeshProUGUI? distanceValue;

        [Header("Action Buttons")]
        [SerializeField] private Button          levelUpButton  = null!;
        [SerializeField] private Button          repairButton   = null!;
        [SerializeField] private Button          actionButton   = null!;  // SWAP or EQUIP
        [SerializeField] private TextMeshProUGUI actionButtonText = null!;

        public event System.Action? OnActionClicked;

        private const int STAT_MAX = 100;
        private static readonly Color StatBarColor       = new(0.2f, 0.5f, 0.9f, 1f);
        private static readonly Color DurabilityOkColor  = new(0.2f, 0.5f, 0.9f, 1f);
        private static readonly Color DurabilityLowColor = new(0.9f, 0.2f, 0.2f, 1f);

        public void Initialize(PlayerClubData playerClub, ClubDataRuntime template, string actionLabel)
        {
            // Portrait
            if (portraitImage != null && template.portraitSprite != null)
                portraitImage.sprite = template.portraitSprite;

            // Rarity bg
            if (backgroundImage != null)
            {
                var bgSprite = Resources.Load<Sprite>($"Rarities/{template.rarity}");
                if (bgSprite != null) { backgroundImage.sprite = bgSprite; backgroundImage.color = Color.white; }
            }

            // Name
            if (nameText != null)
            {
                string typeLine = template.GetTypeLabel();
                string brand = template.brand.ToUpper();
                nameText.text = string.IsNullOrEmpty(brand) ? typeLine : $"{typeLine}\n{brand}";
            }

            // Rarity badge
            if (rarityBadgeText != null)
            {
                rarityBadgeText.text = RarityHelper.GetRarityLabel(template.rarity);
                rarityBadgeText.color = RarityHelper.GetRarityBadgeTextColor(template.rarity);
            }

            // Level
            if (levelText != null)
                levelText.text = $"Lv{playerClub.currentLevel}";

            // Stats
            SetBar(statBarPower, statNumPower, playerClub.GetPower(template), STAT_MAX, StatBarColor);
            SetBar(statBarAccuracy, statNumAccuracy, playerClub.GetAccuracy(template), STAT_MAX, StatBarColor);
            SetBar(statBarLieRes, statNumLieRes, playerClub.GetLieResistance(template), STAT_MAX, StatBarColor);
            SetBar(statBarLoft, statNumLoft, playerClub.GetLoft(template), STAT_MAX, StatBarColor);

            int curDur = playerClub.currentDurability;
            int maxDur = playerClub.maxDurability;
            if (statBarDurability != null)
            {
                statBarDurability.fillAmount = maxDur > 0 ? (float)curDur / maxDur : 0f;
                statBarDurability.color = playerClub.IsDurabilityLow ? DurabilityLowColor : DurabilityOkColor;
            }
            if (statNumDurability != null) statNumDurability.text = $"{curDur}";

            // Distance
            if (distanceValue != null)
                distanceValue.text = $"{playerClub.GetDistance(template)} yd";

            // Buttons
            if (levelUpButton != null) levelUpButton.interactable = false;
            if (repairButton  != null) repairButton.interactable  = false;

            if (actionButton != null)
            {
                actionButton.interactable = true;
                actionButton.onClick.RemoveAllListeners();
                actionButton.onClick.AddListener(() => OnActionClicked?.Invoke());
            }
            if (actionButtonText != null)
                actionButtonText.text = actionLabel;
        }

        private static void SetBar(Image? bar, TextMeshProUGUI? num, int value, int cap, Color color)
        {
            if (bar != null) { bar.fillAmount = cap > 0 ? (float)value / cap : 0f; bar.color = color; }
            if (num != null) num.text = $"{value}";
        }
    }
}
```

---

### Step J8: Localization Keys — ✅ ALREADY ADDED TO CSV

Added to `Assets/Localization/LocalizationText.csv` by Claude (Architect):

| Key | EN | JP |
|-----|----|----|  
| `BAG_EQUIPPED` | EQUIPPED | 装備中 |
| `BAG_EQUIP` | EQUIP | 装備 |
| `BAG_SWAP` | SWAP | 交換 |
| `BAG_SWAP_CLUB` | SWAP CLUB | クラブを交換 |
| `BAG_EQUIP_CLUB` | EQUIP CLUB | クラブを装備 |
| `BAG_EQUIP_ACTION` | EQUIP | 装備 |
| `BAG_CANCEL` | CANCEL | キャンセル |

**Claude Code: Do NOT edit LocalizationText.csv — these keys are already present.**

---

### Step J9: Kai’s Manual Unity Work (NOT for Claude Code)

This checklist is for Kai to do by hand in the Unity Editor:

1. **Create `BagThumbnailCard` prefab:**
   - Clone `BagSlotPrefab` → rename to `BagThumbnailCard`
   - Add `BagThumbnailCard` component
   - Wire children: BagImage, BagLabel, RarityBadge, RarityBadge/Text, EquippedIcon
   - Save to `Assets/Prefabs/UI/Inventory/BagThumbnailCard.prefab`

2. **Create `BagClubCard` prefab:**
   - Clone `BagSwapClubCard.prefab` → rename to `BagClubCard`
   - Remove the `ItemUseClubCard` component, add `BagClubCard` component
   - Wire all SerializeField refs (same child names as BagSwapClubCard)
   - The SwapBtn becomes `actionButton`, SwapText becomes `actionButtonText`
   - Save to `Assets/Prefabs/UI/Inventory/BagClubCard.prefab`

3. **Create `BagsContent` panel in InventoryScreen:**
   - Under the existing tab panels, create a new `BagsContent` GO
   - Add `BagCarouselController` component (horizontal ScrollRect + content parent)
   - Add `BagDetailPanel` component
   - Create club grid area (4×2 GridLayoutGroup, same cell size as mockup)
   - Add `EquipBagButton` with text
   - Wire prefab refs in Inspector

4. **Create `BagClubModal` hierarchy:**
   - Clone from `ItemUseModal` (same structure: Backdrop, ModalPanel, FilterBar, ScrollArea, Cancel)
   - Replace `ItemUseModalController` with `BagClubModalController`
   - Add `ClubFilterBar` to the FilterBar GO
   - Wire all refs
   - Add `GraphicRaycaster` to modal panel
   - Set `raycastTarget = false` on decorative images

5. **Wire `InventoryScreenController`:**
   - `tabPanels[1]` = BagsContent

6. **Add bag full images** to `Resources/Bags/Full/`

---

### Verification Checklist

- [ ] BAGS tab shows carousel of bag portraits (unlocked + locked)
- [ ] Always shows at least 6 slots
- [ ] Selecting a bag updates the detail panel (image, name, description)
- [ ] Equipped icon shows on the carousel card of the equipped bag
- [ ] Club grid shows 8 slots (equipped clubs + empty cards)
- [ ] Tapping SWAP on a club card opens Swap Club modal with correct title
- [ ] Tapping EQUIP CLUB on an empty card opens Equip Club modal
- [ ] Swap modal excludes clubs already in the bag (except the one being swapped)
- [ ] Equip modal excludes clubs already in the bag
- [ ] Filter bar works (ALL/DRIVERS/WOODS/IRONS/WEDGES/PUTTERS)
- [ ] WEDGES filter shows all 3 wedge types
- [ ] Selecting a club in Swap mode: old club removed, new club added
- [ ] Selecting a club in Equip mode: club added to empty slot
- [ ] After modal closes, club grid refreshes automatically
- [ ] EQUIPPED button shows gold when bag is equipped, silver when not
- [ ] Tapping EQUIP on a non-equipped bag equips it
- [ ] Previously equipped bag loses equipped state
- [ ] No console errors

### What’s NOT in this phase

- ❌ Mandatory club validation (1 Putter, 1 Driver, 1 Iron/Wedge) — Phase J+
- ❌ Sort/filters on carousel (by rarity, etc.)
- ❌ Bag level up
- ❌ Compare mode for bags
- ❌ Toast notifications

---

## Previous Task (2026-03-31) — Phase I2: Item Use → Club Selection Modal

> Moved to Completed Tasks below.

---

### Step 1: Create `ItemUseModalController.cs`

**File:** `Assets/Scripts/UI/Inventory/ItemUseModalController.cs`
**Namespace:** `Golfin.Inventory`
**Extends:** `ModalController` (from `Golfin.UI.Modals` — same as BagSelectionModalController)

```csharp
#nullable enable
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Golfin.UI.Modals;
using Golfin.Roster;

namespace Golfin.Inventory
{
    /// <summary>
    /// Modal for selecting which club to apply a repair kit to.
    /// Shows all owned clubs in a scrollable 4-column grid with stats.
    /// Each card has a "USE REPAIR KIT" button that consumes the item and repairs the club.
    /// </summary>
    public class ItemUseModalController : ModalController
    {
        [Header("Modal UI")]
        [SerializeField] private TextMeshProUGUI titleText    = null!;  // "SELECT CLUB"
        [SerializeField] private Button          cancelButton = null!;
        [SerializeField] private Image           backgroundImage = null!;  // RepairBackground.png

        [Header("Filter Bar")]
        [SerializeField] private ClubFilterBar? filterBar;

        [Header("Club Grid (Scrollable)")]
        [SerializeField] private Transform    gridParent    = null!;  // content parent inside ScrollRect
        [SerializeField] private ScrollRect   scrollRect    = null!;
        [SerializeField] private GameObject   clubCardPrefab = null!; // ItemUseClubCard prefab

        // ── State ──────────────────────────────────────────────────────────
        private string currentItemId = "";
        private int    restorePercent = 0;
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

        private void OnDisable()
        {
            if (filterBar != null)
                filterBar.OnFilterChanged -= OnFilterChanged;
        }

        // ── Public API ─────────────────────────────────────────────────────

        /// <summary>
        /// Opens the modal for a specific item (repair kit).
        /// Called from ItemDetailPanel.OnUseClicked().
        /// </summary>
        public void Open(string itemId)
        {
            currentItemId = itemId;

            var template = ItemDatabaseCSV.Instance?.GetItem(itemId);
            restorePercent = template?.restorePercent ?? 0;

            if (titleText != null)
                titleText.text = LocalizationManager.Get("ITEM_SELECT_CLUB");

            // Reset filter to ALL
            if (filterBar != null)
                filterBar.SetFilter(0);

            BuildClubCards(null); // null = ALL
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
            // Clear old cards
            foreach (var card in spawnedCards)
                if (card != null) Destroy(card);
            spawnedCards.Clear();

            if (ClubManager.Instance == null || ClubDatabaseCSV.Instance == null) return;

            // Get clubs (filtered)
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

                var cardGO = Instantiate(clubCardPrefab, gridParent);
                spawnedCards.Add(cardGO);

                var cardComp = cardGO.GetComponent<ItemUseClubCard>();
                if (cardComp != null)
                {
                    bool needsRepair = playerClub.currentDurability < playerClub.maxDurability;
                    cardComp.Initialize(playerClub, template, restorePercent, needsRepair);
                    cardComp.OnUseRepairKit += () => OnRepairKitUsed(playerClub.clubId);
                }
            }

            // Reset scroll to top
            if (scrollRect != null)
                scrollRect.verticalNormalizedPosition = 1f;
        }

        // ── Repair Action ──────────────────────────────────────────────────

        private void OnRepairKitUsed(string clubId)
        {
            if (string.IsNullOrEmpty(currentItemId)) return;

            var playerClub = ClubManager.Instance?.GetClubData(clubId);
            if (playerClub == null) return;

            // Calculate new durability
            int restored = Mathf.CeilToInt(playerClub.maxDurability * restorePercent / 100f);
            int newDurability = Mathf.Min(playerClub.currentDurability + restored, playerClub.maxDurability);

            // Consume item
            ItemManager.Instance?.UseItem(currentItemId);

            // Apply repair
            ClubManager.Instance?.RepairClub(clubId, newDurability);

            var template = ClubDatabaseCSV.Instance?.GetClub(clubId);
            Debug.Log($"[ItemUseModal] Used {currentItemId} on {template?.name ?? clubId}. " +
                      $"Durability {playerClub.currentDurability} → {newDurability}");

            // Close modal
            Hide();
        }
    }
}
```

---

### Step 2: Create `ItemUseClubCard.cs`

**File:** `Assets/Scripts/UI/Inventory/ItemUseClubCard.cs`
**Namespace:** `Golfin.Inventory`

This is the **rich club card** shown inside the modal. It's similar to `ClubThumbnailCard`
but taller/wider with full stat bars, LEVEL UP (disabled), REPAIR (disabled), and
"USE REPAIR KIT" button.

**NOTE to Claude Code:** This is a NEW prefab — do NOT reuse ClubThumbnailCard.
Build a new `ItemUseClubCard` prefab (Steps 4-5 explain hierarchy). The mockup shows
these cards are significantly larger than carousel thumbnails.

```csharp
#nullable enable
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Golfin.Inventory
{
    /// <summary>
    /// Club card inside the Item Use modal. Shows club portrait, name, rarity,
    /// level, 5 stat bars (Power/Accuracy/LieResistance/Loft/Durability),
    /// Distance, disabled Level Up + Repair buttons, and "USE REPAIR KIT" action.
    /// </summary>
    public class ItemUseClubCard : MonoBehaviour
    {
        [Header("Card Top")]
        [SerializeField] private Image           backgroundImage   = null!;  // rarity bg
        [SerializeField] private Image           portraitImage      = null!;
        [SerializeField] private TextMeshProUGUI nameText           = null!;  // "DRIVER\nG&F"
        [SerializeField] private TextMeshProUGUI rarityBadgeText    = null!;  // "R"
        [SerializeField] private TextMeshProUGUI levelText          = null!;  // "Lv10"

        [Header("Stat Bars")]
        [SerializeField] private Image statIconPower    = null!;
        [SerializeField] private Image statBarPower     = null!;
        [SerializeField] private TextMeshProUGUI statNumPower = null!;

        [SerializeField] private Image statIconAccuracy    = null!;
        [SerializeField] private Image statBarAccuracy     = null!;
        [SerializeField] private TextMeshProUGUI statNumAccuracy = null!;

        [SerializeField] private Image statIconLieRes    = null!;
        [SerializeField] private Image statBarLieRes     = null!;
        [SerializeField] private TextMeshProUGUI statNumLieRes = null!;

        [SerializeField] private Image statIconLoft    = null!;
        [SerializeField] private Image statBarLoft     = null!;
        [SerializeField] private TextMeshProUGUI statNumLoft = null!;

        [SerializeField] private Image statIconDurability    = null!;
        [SerializeField] private Image statBarDurability     = null!;
        [SerializeField] private TextMeshProUGUI statNumDurability = null!;

        [Header("Distance")]
        [SerializeField] private Image?           distanceIcon  = null;
        [SerializeField] private TextMeshProUGUI? distanceValue = null;

        [Header("Action Buttons")]
        [SerializeField] private Button          levelUpButton     = null!;
        [SerializeField] private Button          repairButton      = null!;
        [SerializeField] private Button          useRepairKitButton = null!;
        [SerializeField] private TextMeshProUGUI useRepairKitText   = null!;

        /// <summary>Fired when "USE REPAIR KIT" is tapped.</summary>
        public event System.Action? OnUseRepairKit;

        private const int STAT_MAX = 100;
        private static readonly Color DurabilityLowColor = new Color(0.9f, 0.2f, 0.2f, 1f);
        private static readonly Color DurabilityOkColor  = new Color(0.2f, 0.5f, 0.9f, 1f);
        private static readonly Color StatBarColor       = new Color(0.2f, 0.5f, 0.9f, 1f);

        /// <summary>
        /// Bind all visuals from player + template data.
        /// </summary>
        /// <param name="playerClub">Player's club instance</param>
        /// <param name="template">Club template from CSV</param>
        /// <param name="restorePercent">Repair kit's restore % (for display/logic)</param>
        /// <param name="needsRepair">False = already at full durability → disable USE button</param>
        public void Initialize(PlayerClubData playerClub, ClubDataRuntime template,
                               int restorePercent, bool needsRepair)
        {
            // ── Portrait & Background ──────────────────────────────────────
            if (portraitImage != null && template.portraitSprite != null)
                portraitImage.sprite = template.portraitSprite;

            if (backgroundImage != null)
            {
                var bgSprite = Resources.Load<Sprite>($"Rarities/{template.rarity}");
                if (bgSprite != null)
                {
                    backgroundImage.sprite = bgSprite;
                    backgroundImage.color  = Color.white;
                }
            }

            // ── Name (type on top line, brand below) ───────────────────────
            if (nameText != null)
            {
                string fullName = template.name;
                string brand    = template.brand;
                string typePart = fullName;
                if (!string.IsNullOrEmpty(brand))
                {
                    int brandIndex = fullName.IndexOf(brand, System.StringComparison.OrdinalIgnoreCase);
                    if (brandIndex >= 0) typePart = fullName.Substring(0, brandIndex).Trim();
                }
                nameText.text = $"{typePart.ToUpper()}\n{brand.ToUpper()}";
            }

            // ── Rarity badge ───────────────────────────────────────────────
            if (rarityBadgeText != null)
            {
                rarityBadgeText.text  = RarityHelper.GetRarityLabel(template.rarity);
                rarityBadgeText.color = RarityHelper.GetRarityBadgeTextColor(template.rarity);
            }

            // ── Level ──────────────────────────────────────────────────────
            if (levelText != null)
                levelText.text = $"Lv{playerClub.currentLevel}";

            // ── Stat Bars ──────────────────────────────────────────────────
            SetStatBar(statBarPower, statNumPower, playerClub.GetPower(template), STAT_MAX, StatBarColor);
            SetStatBar(statBarAccuracy, statNumAccuracy, playerClub.GetAccuracy(template), STAT_MAX, StatBarColor);
            SetStatBar(statBarLieRes, statNumLieRes, playerClub.GetLieResistance(template), STAT_MAX, StatBarColor);
            SetStatBar(statBarLoft, statNumLoft, playerClub.GetLoft(template), STAT_MAX, StatBarColor);

            // Durability (special — current/max, color based on state)
            int curDur = playerClub.currentDurability;
            int maxDur = playerClub.maxDurability;
            if (statBarDurability != null)
            {
                statBarDurability.fillAmount = maxDur > 0 ? (float)curDur / maxDur : 0f;
                statBarDurability.color = playerClub.IsDurabilityLow ? DurabilityLowColor : DurabilityOkColor;
            }
            if (statNumDurability != null)
                statNumDurability.text = $"{curDur}";

            // Distance
            if (distanceValue != null)
                distanceValue.text = $"{playerClub.GetDistance(template)} yd";

            // ── Buttons ────────────────────────────────────────────────────
            // Level Up and Repair are always disabled in this modal
            if (levelUpButton != null) levelUpButton.interactable = false;
            if (repairButton  != null) repairButton.interactable  = false;

            // USE REPAIR KIT — only active if club needs repair
            if (useRepairKitButton != null)
            {
                useRepairKitButton.interactable = needsRepair;
                useRepairKitButton.onClick.AddListener(() => OnUseRepairKit?.Invoke());
            }

            if (useRepairKitText != null)
                useRepairKitText.text = LocalizationManager.Get("ITEM_USE_REPAIR_KIT");
        }

        private void SetStatBar(Image? bar, TextMeshProUGUI? num, int value, int cap, Color color)
        {
            if (bar != null)
            {
                bar.fillAmount = cap > 0 ? (float)value / cap : 0f;
                bar.color = color;
            }
            if (num != null)
                num.text = $"{value}";
        }
    }
}
```

---

### Step 3: Wire `ItemDetailPanel` → `ItemUseModalController`

**File:** `Assets/Scripts/UI/Inventory/ItemDetailPanel.cs`

Add a serialized field and update `OnUseClicked()`:

**A) Add field after the existing `carousel` field:**

```csharp
        [Header("Modals")]
        [SerializeField] private ItemUseModalController? useModal;
```

**B) Replace the `OnUseClicked()` method:**

Replace:
```csharp
        private void OnUseClicked()
        {
            // Phase I2: open club selection modal
            Debug.Log($"[ItemDetailPanel] USE clicked for '{currentItemId}' — modal not yet wired.");
        }
```
With:
```csharp
        private void OnUseClicked()
        {
            if (useModal != null && !string.IsNullOrEmpty(currentItemId))
                useModal.Open(currentItemId);
            else
                Debug.Log($"[ItemDetailPanel] USE clicked for '{currentItemId}' — wire ItemUseModalController.");
        }
```

---

### Step 4: Build the `ItemUseClubCard` Prefab

**Location:** `Assets/Prefabs/UI/Inventory/ItemUseClubCard.prefab`

This is a **new prefab** (not a clone of ClubThumbnailCard). It's much taller because it
includes stat bars. From the mockup, the layout is approximately:

```
ItemUseClubCard (root — VerticalLayoutGroup)
├── CardTop (rarity bg + portrait + rarity badge + level badge)
│   ├── Background (Image — rarity bg sprite, stretch)
│   ├── Portrait (Image — club thumbnail, centered)
│   ├── RarityBadge (TMP — "R", top-left corner)
│   └── LevelBadge (TMP — "Lv10", top-right corner)
├── NameText (TMP — "DRIVER\nG&F")
├── StatsPanel (VerticalLayoutGroup, compact)
│   ├── StatRow_Distance (icon + "180 yd")
│   ├── StatRow_Power (icon + bar + number)
│   ├── StatRow_Accuracy (icon + bar + number)
│   ├── StatRow_LieRes (icon + bar + number)
│   ├── StatRow_Loft (icon + bar + number)
│   └── StatRow_Durability (icon + bar + number)
├── ButtonRow (HorizontalLayoutGroup)
│   ├── LevelUpBtn (disabled)
│   └── RepairBtn (disabled)
└── UseRepairKitBtn (full-width button at bottom)
```

**Stat icons:** Reuse the same stat icon sprites from `ClubDetailPanel`'s StatsPanel.
Check the existing ClubsContent hierarchy for the icon sprite paths/names.

**Card dimensions:** From mockup, cards are arranged 4 per row. Card size: 180w × 410h.
Use `LayoutElement` with `preferredWidth = 180, preferredHeight = 410`.

**IMPORTANT:** The actual card styling should try to match the mockup's look. The stat bars
are compact (small icons, thin bars, tiny numbers). Level Up and Repair buttons are small
and sit side by side. "USE REPAIR KIT" is a full-width button at the very bottom.

**NOTE:** Read the existing `ClubThumbnailCard.prefab` YAML to reference the rarity badge,
level badge, and portrait positioning patterns. Don't clone it — the structure is different —
but reference it for visual consistency.

---

### Step 5: Build the Modal UI Hierarchy

The modal needs to be built as a child of the `ItemsContent` panel (or as a sibling in the
Inventory screen that can overlay). Pattern: same as `BagSelectionModal`.

```
ItemUseModal (root — Canvas overlay or panel with ModalController)
├── Background (Image — RepairBackground.png from Assets/Art/ItemsScreen/)
├── ModalContainer (VerticalLayoutGroup, centered)
│   ├── TitleText (TMP — "SELECT CLUB", centered)
│   ├── TopDivider (Image — thin horizontal line)
│   ├── FilterBar (clone of ClubFilterBar — 6 buttons: ALL|DRIVERS|WOODS|IRONS|WEDGES|PUTTERS)
│   │   └── Add ClubFilterBar component to this GO
│   ├── ScrollArea (ScrollRect — clips the club grid)
│   │   ├── Viewport (RectTransform with mask)
│   │   │   └── GridContent (GridLayoutGroup — 4 columns)
│   │   │       └── [ItemUseClubCard instances spawned here]
│   │   └── Scrollbar (Scrollbar — vertical, right side)
│   ├── BottomDivider (Image — thin horizontal line)
│   └── CancelButton (Button — uses ButtonCancel.png from Assets/Art/ItemsScreen/)
│       └── Text (TMP — "CANCEL")
```

**GridLayoutGroup settings on GridContent:**
- Cell size: 180 × 410
- Spacing: ~8 × 8
- Constraint: Fixed Column Count = 4
- Start corner: Upper Left
- childAlignment: UpperCenter

**ScrollRect settings:**
- Vertical only (horizontal = false)
- Content = GridContent
- Viewport = Viewport (with Mask or RectMask2D)
- Vertical Scrollbar = the Scrollbar GO

**Fixed elements:** Title, TopDivider, FilterBar, BottomDivider, and CancelButton are
**outside** the ScrollRect. Only GridContent scrolls.

**Graphic Raycaster:** The modal panel must have a `GraphicRaycaster` component or buttons
won't receive clicks.

**Raycast Targets:** Set `raycastTarget = false` on all non-interactive Images (background,
dividers, decorative elements).

---

### Step 6: Create Editor Scripts

#### 6a: `ItemUseModalAutoWire.cs`

**File:** `Assets/Scripts/UI/Inventory/Editor/ItemUseModalAutoWire.cs`

Wire all SerializeField references on `ItemUseModalController`:
- titleText, cancelButton, backgroundImage
- filterBar (the ClubFilterBar on the modal's FilterBar GO)
- gridParent (GridContent transform inside ScrollArea)
- scrollRect
- clubCardPrefab (the ItemUseClubCard prefab)

Also wire `ItemDetailPanel.useModal` → the modal controller.

**NOTE:** Claude Code should implement the auto-wire after building the hierarchy (Step 5).
Pattern: same as `BagSelectionModalAutoWire`.

---

### Step 7: Add Localization Keys

| Key | EN | JP |
|-----|----|----|
| `ITEM_SELECT_CLUB` | SELECT CLUB | クラブを選択 |
| `ITEM_USE_REPAIR_KIT` | USE REPAIR KIT | 修理キットを使用 |
| `ITEM_CANCEL` | CANCEL | キャンセル |

---

### Step 8: Filter Bar — ClubFilterBar Reuse

The filter bar in the modal is a **second instance** of `ClubFilterBar`. It:
1. Uses the same 6-button layout (ALL | DRIVERS | WOODS | IRONS | WEDGES | PUTTERS)
2. Has its own `ClubFilterBar` component (separate from the one in ClubsContent)
3. `ItemUseModalController` subscribes to this instance's `OnFilterChanged` event
4. Position: **below the title/divider, above the scrollable club grid**

To build it:
- Clone the FilterBar GO from ClubsContent (or create a new one with 6 buttons)
- Add `ClubFilterBar` component
- Wire the 6 buttons into the `filterButtons` array
- The dividers will auto-inject via `InjectDividers()` in Start()

Alternatively, if it's easier: create a FilterBar prefab from the existing one and instantiate it.

---

### Verification Checklist

- [ ] Items tab → select a repair kit → tap USE
- [ ] Modal opens with "SELECT CLUB" title
- [ ] Background uses RepairBackground.png
- [ ] Filter bar shows ALL | DRIVERS | WOODS | IRONS | WEDGES | PUTTERS
- [ ] Tapping each filter tab filters the club grid
- [ ] WEDGES filter shows all 3 wedge types combined
- [ ] Club cards show: rarity bg, portrait, name, rarity badge, level, all 5 stat bars, distance
- [ ] Level Up and Repair buttons visible but grayed out
- [ ] "USE REPAIR KIT" button active on clubs that need repair
- [ ] "USE REPAIR KIT" button disabled on clubs at full durability
- [ ] Tapping "USE REPAIR KIT" → kit consumed, club repaired, modal closes
- [ ] After modal closes, Items tab shows updated quantity (x98)
- [ ] Clubs tab also reflects the repaired durability
- [ ] Grid scrolls vertically when more than 8 clubs visible
- [ ] Scrollbar visible on right side
- [ ] Cancel button closes modal without consuming anything
- [ ] No console errors

### What's NOT in this phase

- ❌ Toast notifications ("Club X repaired! Durability Y → Z")
- ❌ Repair animation/particle effects
- ❌ RepairKitManager full deprecation (it's already bypassed — ClubDetailPanel uses ItemManager)

---

## Completed Tasks

✅ DONE: 2026-03-31 — Phase I2 Item Use → Club Selection Modal: ItemUseModalController, ItemUseClubCard, FilterBar reuse, ItemDetailPanel wiring, localization keys.

✅ DONE: 2026-03-31 — Phase I1 Items Inventory: ItemDataRuntime, PlayerItemData, ItemDatabaseCSV, ItemManager, Items.csv, ItemThumbnailCard, ItemCarouselController, ItemDetailPanel, editor scripts, localization keys, ItemsContent panel.

✅ DONE: 2026-03-27 — Phase H Balls Inventory: BallData, BallDatabaseCSV, BallManager, Balls.csv, BallThumbnailCard, BallCarouselController, BallDetailPanel, BallManagerSetup, BallDetailPanelAutoWire, 7 localization keys

✅ DONE: 2026-03-26 — Phase G Character Compare stat diff labels: CompareRightPanelDiffBuilder, CompareController diff fields/methods, CompareAutoWire diff wiring

✅ DONE: 2026-03-20 — ScreenshotTool, compress script, CLAUDE.md update
✅ DONE: 2026-03-20 — Phase C code: ClubCarouselController, ClubDetailPanel, builders, auto-wire
✅ DONE: 2026-03-21 — New leveling economy: rarity-based starting/max levels
✅ DONE: 2026-03-23 — TextGradients, visual fixes, filter dividers, arrows, viewport, fade, level text
✅ DONE: 2026-03-25 — Club Compare Phase D: ClubCompareController, builder, auto-wire, stat differences
✅ DONE: 2026-03-24 — Project cleanup: GOLFIN menu reorganized, Art/References folders renamed PascalCase, 5 editor scripts archived
✅ DONE: 2026-03-25 — Phase E1 Club Level Up Modal: PlayerClubData SP fields, ClubManager.SetLevel/RefreshStatValues, ClubLevelUpModalController, ClubDetailPanel/ClubCompareController wired, ClubLevelUpModalAutoWire, localization keys.
✅ DONE: 2026-03-26 — Phase E2 Club Repair One-Tap: RepairKitManager singleton, ClubManager.RepairClub/OnClubRepaired, ClubDetailPanel+ClubCompareController one-tap repair, localization keys, cleanup old modal files.
✅ DONE: 2026-03-26 — Phase E3 Bag Selection Modal: BagManager singleton, BagSelectionModalController, equip buttons wired, auto-wire script, localization keys.
✅ DONE: 2026-03-26 — Phase E3b Bags CSV + Data-Driven Bag Slots: BagDatabaseCSV, BagManager CSV integration, two-prefab bag grid, ClubManager multi-club-per-bag fix, bag name labels.
✅ DONE: 2026-03-26 — Phase E4 Bag ↔ Club management (assign/unassign from bag modal).
✅ DONE: 2026-03-26 — Phase F Level Up Modal polish (SP allocation UI).
✅ DONE: 2026-03-30 — Fix Club Filter Bar: 8→6 tabs + unified WEDGES.
✅ DONE: 2026-03-30 — Fix filter button raycast targets: EnsureButtonRaycastTargets().
