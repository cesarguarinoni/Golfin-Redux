# TellCode.md — Instructions from Claude (Architect) to Claude Code

> Claude Code: Read this file at the start of each task. Execute the latest instruction block.
> After completing, add a status line at the bottom: `✅ DONE: [date] [brief summary]`
> Claude (Architect) will update this file with new instructions as needed.

---

## Current Task (2026-03-31) — Phase I2: Item Use → Club Selection Modal

When the player taps USE on a repair kit in the Items tab, a modal overlay appears showing
all owned clubs. The player taps a club card, the repair kit is consumed, the club is repaired,
and the modal closes.

### Reference Mockup

See `Item_Screen_-_Use.png` in project knowledge. Key observations:
- Modal has a **custom background** image: `Assets/Art/ItemsScreen/RepairBackground.png`
- **Title:** "SELECT CLUB" (centered, top)
- **Filter bar** (ALL | DRIVERS | WOODS | IRONS | WEDGES | PUTTERS) — same 6-tab layout
  as `ClubFilterBar`, positioned **below the carousel area** (between title area and club grid)
- **Club cards** in a **4-column grid** showing:
  - Rarity background, portrait, club name (type + brand), rarity badge, level badge
  - **All 5 stat bars** with icons + fill bars + numbers (Power, Accuracy, Lie Resistance, Loft, Durability)
  - Distance value
  - LEVEL UP button (always **disabled/grayed out** in this modal)
  - REPAIR button (always **disabled/grayed out** in this modal)
  - **"USE REPAIR KIT"** button at the bottom of each card
- **Vertical scroll** for the club grid area only (title, filter bar, and Cancel button stay fixed)
- **Scrollbar** visible on the right edge
- **Cancel button** at the bottom (uses `Assets/Art/ItemsScreen/ButtonCancel.png`)
- Cards that are **already at full durability** should have their "USE REPAIR KIT" button disabled

### Architecture

```
ItemDetailPanel
  └── OnUseClicked()
        └── ItemUseModalController.Open(currentItemId)
              ├── Reads ClubManager.GetAllOwnedClubs() (or filtered subset)
              ├── Builds club cards with stat bars
              ├── Each card's "USE REPAIR KIT" button:
              │     1. ItemManager.UseItem(itemId)
              │     2. Apply repair: ClubManager.RepairClub(clubId, newDurability)
              │     3. Close modal
              │     4. ItemDetailPanel refreshes via OnInventoryChanged
              └── Filter bar filters which clubs are shown
```

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
