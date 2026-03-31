# Phase I1 — Items Inventory Screen

> Spec written by Claude (Architect) — 2026-03-31
> For implementation by Claude Code.

---

## Overview

The Items tab (index 3) in the Inventory screen shows consumable items. For now, only
Repair Kits exist (3 tiers). The screen reuses the Balls carousel pattern (same prefabs,
same pagination) and has a detail panel with item info + a USE button.

No stat bars. No compare (button always grayed out). No filter bar.

---

## Part 1 — Data Layer

### 1A. Items.csv

**File:** `Assets/Data/Items.csv`

```csv
id,name,category,rarity,restorePercent,thumbnailSprite,fullSprite,proTip,info
repairkit_common,Repair Kit,RepairKit,Common,50,RepairKit-Common,RepairKit-Common,"Clubs will automatically use the best repair kit available when you repair them from the Clubs tab.","Essential and efficient, this Repair Kit restores up to 50% of any club's durability. Designed for quick fixes and reliable performance, it's a must have for keeping your equipment in solid shape round after round."
repairkit_rare,Repair Kit,RepairKit,Rare,75,RepairKit-Rare,RepairKit-Rare,"Clubs will automatically use the best repair kit available when you repair them from the Clubs tab.","A step above the standard kit, this Rare Repair Kit restores up to 75% of any club's durability. Ideal for mid-range repairs when you need your gear back in top form."
repairkit_mythic,Repair Kit,RepairKit,Mythic,100,RepairKit-Mythic,RepairKit-Mythic,"Clubs will automatically use the best repair kit available when you repair them from the Clubs tab.","The ultimate repair solution. This Mythic Repair Kit fully restores any club's durability to 100%. Save it for when your best gear needs a complete overhaul."
```

**Fields:**
- `id` — unique item ID (e.g. `repairkit_common`)
- `name` — display name (e.g. "Repair Kit")
- `category` — item category for future grouping (e.g. "RepairKit")
- `rarity` — Common / Rare / Mythic (used for card background + rarity label)
- `restorePercent` — durability restore % (50, 75, 100)
- `thumbnailSprite` — sprite name in `Resources/Items/Thumbnails/`
- `fullSprite` — sprite name in `Resources/Items/Full/`
- `proTip` — pro tip text shown in detail panel
- `info` — description text shown in INFO section

**Sprites already exist at:**
- `Assets/Resources/Items/Thumbnails/RepairKit-Common.png`
- `Assets/Resources/Items/Thumbnails/RepairKit-Rare.png`
- `Assets/Resources/Items/Thumbnails/RepairKit-Mythic.png`
- `Assets/Resources/Items/Full/RepairKit-Common.png`
- `Assets/Resources/Items/Full/RepairKit-Rare.png`
- `Assets/Resources/Items/Full/RepairKit-Mythic.png`

### 1B. ItemDataRuntime (data class)

**File:** `Assets/Scripts/UI/Inventory/ItemDataRuntime.cs`
**Namespace:** `Golfin.Inventory`

```csharp
public class ItemDataRuntime
{
    public string  itemId             = "";
    public string  name               = "";
    public string  category           = "";   // "RepairKit", future: "Buff", etc.
    public string  rarity             = "";   // "Common", "Rare", "Mythic"
    public int     restorePercent     = 0;    // 50, 75, 100
    public string  thumbnailSpriteName = "";
    public string  fullSpriteName      = "";
    public string  proTip             = "";
    public string  info               = "";

    // Resolved at load time
    public Sprite? thumbnailSprite;
    public Sprite? fullSprite;
}
```

### 1C. PlayerItemData (player instance)

**File:** `Assets/Scripts/UI/Inventory/PlayerItemData.cs`
**Namespace:** `Golfin.Inventory`

```csharp
public class PlayerItemData
{
    public string itemId   = "";
    public int    quantity = 0;

    public bool IsUnlimited => quantity < 0;
}
```

### 1D. ItemDatabaseCSV singleton

**File:** `Assets/Scripts/UI/Inventory/ItemDatabaseCSV.cs`
**Namespace:** `Golfin.Inventory`

**Clone from:** `BallDatabaseCSV.cs` — then rename/adjust:
- Class name: `ItemDatabaseCSV`
- Data type: `BallDataRuntime` → `ItemDataRuntime`
- Sprite paths: `"Items/Thumbnails"` and `"Items/Full"`
- CSV field: `[SerializeField] private TextAsset itemsCSV = null!;`
- ParseRow reads: `id, name, category, rarity, restorePercent, thumbnailSprite, fullSprite, proTip, info`
- Public API:
  - `GetItem(string itemId)` → `ItemDataRuntime?`
  - `GetAllItems()` → `List<ItemDataRuntime>`
  - `GetItemsByCategory(string category)` → `List<ItemDataRuntime>` (filter by category field)

**Script Execution Order:** ItemDatabaseCSV must run before ItemManager (set in Project Settings).

### 1E. ItemManager singleton

**File:** `Assets/Scripts/ItemManager.cs` (top-level, no namespace — matches ClubManager/BallManager)

This **replaces** `RepairKitManager.cs` as the single source of truth for item inventory.

```csharp
public class ItemManager : MonoBehaviour
{
    public static ItemManager Instance { get; private set; } = null!;

    public event System.Action? OnInventoryChanged;

    public const int MAX_STACK = 99;

    private readonly Dictionary<string, PlayerItemData> ownedItems = new();

    // —— Lifecycle ——
    private void Awake() { /* singleton pattern, call InitializeItems() */ }
    private void OnDestroy() { /* cleanup singleton */ }

    private void InitializeItems()
    {
        // Seed from ItemDatabaseCSV — give test quantities:
        // repairkit_common = 99, repairkit_rare = 99, repairkit_mythic = 99
        var db = ItemDatabaseCSV.Instance;
        foreach (var template in db.GetAllItems())
        {
            ownedItems[template.itemId] = new PlayerItemData
            {
                itemId   = template.itemId,
                quantity = 99,  // test data
            };
        }
    }

    // —— Query API ——
    public PlayerItemData? GetItemData(string itemId);
    public List<string> GetAllOwnedItemIds();  // items with quantity > 0
    public int GetQuantity(string itemId);
    public string GetQuantityDisplay(string itemId);  // "x99" or "∞"

    // —— Mutate API ——
    public bool UseItem(string itemId, int count = 1);  // decrements, fires OnInventoryChanged
    public void AddItems(string itemId, int count);     // increments, caps at MAX_STACK

    // —— Repair Kit convenience ——
    /// <summary>
    /// Returns the best repair kit for the given damage level.
    /// Logic: if missing% <= 50 → prefer common; if <= 75 → prefer rare; else → prefer mythic.
    /// Falls back to whatever is available (mythic > rare > common).
    /// </summary>
    public (string? itemId, int restorePercent) GetBestRepairKit(float missingPercent);

    /// <summary>
    /// Uses the best available repair kit and returns the new durability.
    /// Replaces RepairKitManager.UseBestKit().
    /// </summary>
    public (int newDurability, string? itemUsed) UseBestRepairKit(int currentDurability, int maxDurability);
}
```

**Migration from RepairKitManager:**
- All call sites that reference `RepairKitManager.Instance.UseBestKit(...)` must be updated
  to call `ItemManager.Instance.UseBestRepairKit(...)` instead.
- The return type changes from `(int, KitType)` to `(int, string?)` where the string is the
  itemId used (e.g. `"repairkit_common"`) or null if none available.
- **Affected files:**
  - `ClubDetailPanel.cs` — `OnRepairClicked()` method
  - `ClubCompareController.cs` — `OnRepairLeftClicked()` / `OnRepairRightClicked()` methods
  - Any other file referencing `RepairKitManager`
- After migration is verified, **delete** `RepairKitManager.cs` and `RepairKitManagerSetup.cs`.

---

## Part 2 — UI Layer (Items Screen)

### 2A. ItemThumbnailCard

**File:** `Assets/Scripts/UI/Inventory/ItemThumbnailCard.cs`
**Namespace:** `Golfin.Inventory`

**Clone from:** `BallThumbnailCard.cs` — then rename/adjust:
- Class name: `ItemThumbnailCard`
- `Initialize(string itemId)` — reads from `ItemDatabaseCSV` + `ItemManager`
- Card shows: thumbnail sprite, item name, quantity badge ("x99"), rarity background
- Background loads rarity sprite from `Resources/Rarities/{rarity}`
  (Common = grey-blue, Rare = green, Mythic = red/purple)
- Shows rarity badge letter ("C", "R", "M") in the top-left corner —
  use the same badge pattern as ClubThumbnailCard (the `rarityBadgeText` child)
- Name text below the card shows `"{name}\n{rarity}"` like the mockup
  (e.g. "REPAIR KIT" + "REGULAR" on a second line — but we use rarity names instead)

**Prefab:** Clone `BallThumbnailCard.prefab` → save as
`Assets/Prefabs/UI/Inventory/ItemThumbnailCard.prefab`, swap the component
from `BallThumbnailCard` → `ItemThumbnailCard`.

**Empty card:** Reuse `BallThumbnailEmptyCard.prefab` directly (same "EMPTY" slot look).

### 2B. ItemCarouselController

**File:** `Assets/Scripts/UI/Inventory/ItemCarouselController.cs`
**Namespace:** `Golfin.Inventory`

**Clone from:** `BallCarouselController.cs` — then rename/adjust:
- Class name: `ItemCarouselController`
- References `ItemManager.Instance` instead of `BallManager.Instance`
- Card prefab field: `itemCardPrefab` (points to ItemThumbnailCard prefab)
- Empty card prefab field: `itemEmptyCardPrefab` (points to BallThumbnailEmptyCard prefab)
- Event: `OnItemSelected` (`Action<string>` — fires itemId)
- `PopulateCarousel()` — reads from `ItemManager.Instance.GetAllOwnedItemIds()`
- Card type: `ItemThumbnailCard` instead of `BallThumbnailCard`
- Method names: `SelectItem()` instead of `SelectBall()`, `GetSelectedItemId()`, etc.
- No filter bar

### 2C. ItemDetailPanel

**File:** `Assets/Scripts/UI/Inventory/ItemDetailPanel.cs`
**Namespace:** `Golfin.Inventory`

This is the detail area below the carousel. Layout from mockup:

**Left column:**
- Full item image (`Image itemImage`)
- Brand/logo text below image ("GOLFIN") — `TextMeshProUGUI brandText`

**Right column:**
- Item name (`TextMeshProUGUI itemNameText`) — "REPAIR KIT"
- Rarity label (`TextMeshProUGUI rarityText`) — "COMMON" (colored by rarity)
- Quantity (`TextMeshProUGUI quantityText`) — "x99"
- Divider
- "RESTORES" header (`TextMeshProUGUI restoresHeader`)
- Effect text with checkmark icon (`TextMeshProUGUI effectText`) — "DURABILITY 50%"
- Divider
- "*PRO TIP" header + pro tip text (`TextMeshProUGUI proTipHeader`, `TextMeshProUGUI proTipText`)
- Divider
- COMPARE button (`Button compareButton`) — **always `interactable = false`** (grayed out)

**Bottom area (full width):**
- "INFO" header (`TextMeshProUGUI infoHeader`)
- Info description text (`TextMeshProUGUI infoText`)
- USE button (`Button useButton`) — gold, triggers the Use Item modal (Phase I2)

**SerializeFields:**
```csharp
[Header("Left Panel")]
[SerializeField] private Image           itemImage      = null!;
[SerializeField] private TextMeshProUGUI brandText      = null!;

[Header("Right Panel")]
[SerializeField] private TextMeshProUGUI itemNameText   = null!;
[SerializeField] private TextMeshProUGUI rarityText     = null!;
[SerializeField] private TextMeshProUGUI quantityText   = null!;
[SerializeField] private TextMeshProUGUI restoresHeader = null!;
[SerializeField] private TextMeshProUGUI effectText     = null!;
[SerializeField] private TextMeshProUGUI proTipHeader   = null!;
[SerializeField] private TextMeshProUGUI proTipText     = null!;

[Header("Bottom")]
[SerializeField] private TextMeshProUGUI infoHeader     = null!;
[SerializeField] private TextMeshProUGUI infoText       = null!;

[Header("Buttons")]
[SerializeField] private Button compareButton = null!;
[SerializeField] private Button useButton     = null!;

[Header("Carousel")]
[SerializeField] private ItemCarouselController? carousel;
```

**Key logic:**
- Subscribe to `carousel.OnItemSelected` in OnEnable
- `UpdatePanel(string itemId)` reads `ItemDatabaseCSV.Instance.GetItem()` +
  `ItemManager.Instance.GetItemData()` and populates all fields
- Effect text: `$"DURABILITY {template.restorePercent}%"`
- Rarity text: `template.rarity.ToUpper()`, colored via rarity (use `RarityHelper` if
  it supports string-based lookup, otherwise use a local switch)
- Compare button: `compareButton.interactable = false;` in Start()
- USE button: `useButton.onClick` fires `OnUseClicked()` → opens the
  club selection modal (Phase I2, initially just logs a message)
- USE button should be disabled if `quantity == 0`

---

## Part 3 — Editor Scripts

### 3A. ItemManagerSetup.cs

**File:** `Assets/Scripts/Editor/ItemManagerSetup.cs`

Clone `RepairKitManagerSetup.cs` and adjust:
- Menu: `GOLFIN/Setup/Item Manager`
- Creates `ItemDatabaseCSV` and `ItemManager` components on the Managers GO
- Wires `itemsCSV` TextAsset from `Assets/Data/Items.csv`
- Sets Script Execution Order: ItemDatabaseCSV = -90, ItemManager = -80
  (before other managers that might depend on it)

### 3B. ItemDetailPanelAutoWire.cs

**File:** `Assets/Scripts/UI/Inventory/Editor/ItemDetailPanelAutoWire.cs`

Mirrors `BallDetailPanelAutoWire` pattern:
- Menu: `GOLFIN/Wire/Item Detail Panel`
- Finds `ItemDetailPanel` component in scene
- Wires all SerializeFields by finding child GameObjects by name
  under the ItemsContent hierarchy

### 3C. ItemCarouselAutoWire.cs (optional — can combine with 3B)

- Menu: `GOLFIN/Wire/Item Carousel`
- Finds `ItemCarouselController` in scene, wires prefab references
  and UI element references

### 3D. ItemThumbnailCardBuilder.cs

**File:** `Assets/Scripts/UI/Inventory/Editor/ItemThumbnailCardBuilder.cs`

- Menu: `GOLFIN/Build/Item Thumbnail Card`
- **Clones** `BallThumbnailCard.prefab` via `AssetDatabase.CopyAsset()`
- Saves as `Assets/Prefabs/UI/Inventory/ItemThumbnailCard.prefab`
- Opens the new prefab, removes the `BallThumbnailCard` component,
  adds `ItemThumbnailCard` component
- Preserves all child GameObjects and their styling (portrait, name, quantity badge, etc.)

---

## Part 4 — Integration

### 4A. InventoryScreenController — no code changes needed

The `ItemsContent` panel (tab index 3) already exists and is wired. The tab system
already shows/hides it. We just need to populate it with the carousel + detail panel
at runtime.

### 4B. ItemsContent hierarchy (built by Builder or manually in scene)

The existing `ItemsContent` placeholder label should be replaced with:

```
ItemsContent
  ItemCarousel          (has ItemCarouselController, ScrollRect)
    Viewport
      Content           (HorizontalLayoutGroup)
    LeftArrow           (Button)
    RightArrow          (Button)
    PaginationDots      (HorizontalLayoutGroup)
  ItemDetailPanel       (has ItemDetailPanel component)
    LeftPanel
      ItemImage
      BrandText
    RightPanel
      ItemNameText
      RarityText
      QuantityText
      RestoresHeader
      EffectText
      ProTipHeader
      ProTipText
      CompareButton
    BottomPanel
      InfoHeader
      InfoText
      UseButton
```

This mirrors the BallsContent structure. **Clone approach:** If Claude Code can
duplicate the BallsContent hierarchy programmatically, do so and rename children.
Otherwise build it in the editor builder script.

---

## Part 5 — Localization Keys

Add to the localization CSV/data:

| Key | EN | JP |
|-----|-----|-----|
| `ITEM_RESTORES` | RESTORES | 回復 |
| `ITEM_PRO_TIP` | *PRO TIP | *プロのコツ |
| `ITEM_INFO` | INFO | 情報 |
| `ITEM_USE` | USE | 使う |
| `ITEM_COMPARE` | COMPARE | 比較 |
| `ITEM_OWNED` | OWNED | 所持数 |
| `ITEM_DURABILITY` | DURABILITY | 耐久度 |

---

## Cloning Checklist (for Claude Code)

| Source | Target | What to change |
|--------|--------|----------------|
| `BallDatabaseCSV.cs` | `ItemDatabaseCSV.cs` | Class name, sprite paths, CSV field name, ParseRow fields |
| `BallThumbnailCard.cs` | `ItemThumbnailCard.cs` | Class name, reads ItemDatabaseCSV/ItemManager, adds rarity bg+badge |
| `BallCarouselController.cs` | `ItemCarouselController.cs` | Class name, references ItemManager, event name, prefab fields |
| `BallDetailPanel.cs` | `ItemDetailPanel.cs` | Class name, remove stat bars, add rarity/effect/proTip fields, USE button |
| `BallThumbnailCard.prefab` | `ItemThumbnailCard.prefab` | Clone via AssetDatabase.CopyAsset, swap component |
| `BallThumbnailEmptyCard.prefab` | (reuse directly) | No changes — same empty slot visual |
| `RepairKitManagerSetup.cs` | `ItemManagerSetup.cs` | Class name, creates ItemDatabaseCSV + ItemManager |

---

## Verification

1. Play the scene, go to ITEMS tab
2. Should see 3 repair kit cards (Common, Rare, Mythic) + 3 empty slots
3. First card auto-selected, detail panel shows Common Repair Kit info
4. Tap Rare card → detail panel updates with Rare info, rarity label changes
5. Quantity shows "x99" on all cards
6. Compare button is always grayed out
7. USE button is gold and clickable (Phase I2 will wire the modal)
8. Rarity backgrounds match (Common = grey-blue, Rare = green, Mythic = red/purple)
9. Carousel pagination dots show correctly
10. Go to Clubs tab → repair still works via ItemManager (migration verified)

---

## NOT in this phase

- Club selection modal (Phase I2)
- Sort/filter for items
- New item indicator badge
- Item acquisition from missions
- Sound effects
