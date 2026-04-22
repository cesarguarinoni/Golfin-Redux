# Phase I — Items Inventory Screen (Spec)

> Author: Claude (Architect) · 2026-03-31
> Implements: Tab index 3 (ITEMS) in InventoryScreenController
> Phase I-1 = Screen + data layer (this doc)
> Phase I-2 = Club Selection Modal (separate spec)

---

## Overview

The Items screen shows consumable items (currently only Repair Kits) in a
carousel + detail panel layout **identical to the Balls screen**. Key differences
from Balls:

- Items have **rarity** (drives thumbnail background + rarity label)
- Items have **no stats** — detail panel shows effect text instead of stat bars
- Items have a **USE button** (gold) instead of a Compare button
- Compare button exists but is **always grayed out** (non-interactable)
- Thumbnails reuse the **BallThumbnailCard** and **BallThumbnailEmptyCard** prefabs

---

## Design Decisions (diverging from GDD)

| GDD says | We're doing | Reason |
|----------|-------------|--------|
| Standard + Premium repair kits | Common + Rare + Mythic (3 tiers) | Mockups show 3 tiers with rarity badges |
| Standard restores 50%, Premium restores 100% | Common=50%, Rare=75%, Mythic=100% | 3rd tier fills the gap |
| RepairKitManager is standalone | ItemManager replaces RepairKitManager | Single system for all consumables |

> **Log these in GAME_DESIGN_CHANGELOG.md**

---

## 1. Data Layer

### 1.1 Items.csv — `Assets/Data/Items.csv`

```csv
id,name,rarity,restorePercent,thumbnailSprite,fullSprite,effectKey,proTip,info
repairkit_common,Repair Kit,Common,50,RepairKit-Common,RepairKit-Common,ITEM_EFFECT_DURABILITY,ITEM_PROTIP_REPAIRKIT,"Essential and efficient, this Repair Kit restores up to 50% of any club's durability. Designed for quick fixes and reliable performance, it's a must have for keeping your equipment in solid shape round after round."
repairkit_rare,Repair Kit,Rare,75,RepairKit-Rare,RepairKit-Rare,ITEM_EFFECT_DURABILITY,ITEM_PROTIP_REPAIRKIT,"A professional-grade Repair Kit that restores up to 75% of any club's durability. Built for serious golfers who demand peak performance from their gear."
repairkit_mythic,Repair Kit,Mythic,100,RepairKit-Mythic,RepairKit-Mythic,ITEM_EFFECT_DURABILITY,ITEM_PROTIP_REPAIRKIT,"The ultimate Repair Kit. Fully restores any club to 100% durability. When nothing less than perfection will do."
```

**Columns:**
- `id` — unique item identifier
- `name` — display name (same name, differentiated by rarity)
- `rarity` — Common / Rare / Mythic (parsed via `RarityHelper`)
- `restorePercent` — durability restore % (integer: 50, 75, 100)
- `thumbnailSprite` — filename in `Resources/Items/Thumbnails/`
- `fullSprite` — filename in `Resources/Items/Full/`
- `effectKey` — localization key for the effect label (e.g. "DURABILITY 50%")
- `proTip` — localization key for the pro tip text
- `info` — long description text

### 1.2 ItemDataRuntime + PlayerItemData — `Assets/Scripts/UI/Inventory/ItemData.cs`

**Namespace:** `Golfin.Inventory`

```csharp
public class ItemDataRuntime
{
    public string  itemId              = "";
    public string  name                = "";
    public Rarity  rarity              = Rarity.Common;
    public int     restorePercent      = 0;
    public string  thumbnailSpriteName = "";
    public Sprite? thumbnailSprite     = null;
    public string  fullSpriteName      = "";
    public Sprite? fullSprite          = null;
    public string  effectKey           = "";
    public string  proTip              = "";
    public string  info                = "";
}

public class PlayerItemData
{
    public string itemId   = "";
    public int    quantity = 0;  // max 99 per stack
}
```

**Pattern:** mirrors `BallDataRuntime` / `PlayerBallData` exactly.

### 1.3 ItemDatabaseCSV — `Assets/Scripts/UI/Inventory/ItemDatabaseCSV.cs`

**Namespace:** `Golfin.Inventory`
**Pattern:** mirrors `BallDatabaseCSV` exactly.

- Singleton with `Instance`
- `[SerializeField] private TextAsset itemsCSV`
- Sprite paths: `Items/Thumbnails` and `Items/Full`
- Parses `rarity` string → `Rarity` enum via `System.Enum.TryParse`
- Public API:
  - `GetItem(string itemId) → ItemDataRuntime?`
  - `GetAllItems() → List<ItemDataRuntime>`

### 1.4 ItemManager — `Assets/Scripts/ItemManager.cs`

**No namespace** (matches ClubManager, BallManager, RewardPointsManager pattern).
**Replaces:** `RepairKitManager.cs` — the old manager is deleted.

```csharp
public class ItemManager : MonoBehaviour
{
    public static ItemManager Instance { get; private set; } = null!;

    public event System.Action? OnInventoryChanged;

    public const int MAX_STACK = 99;

    private readonly Dictionary<string, PlayerItemData> ownedItems = new();
```

**Awake:** standard singleton + `InitializeItems()`.

**InitializeItems():**
- Reads all items from `ItemDatabaseCSV.Instance.GetAllItems()`
- Seeds test quantities: `repairkit_common = 99`, `repairkit_rare = 99`, `repairkit_mythic = 5`

**Public API:**

```csharp
PlayerItemData? GetItemData(string itemId)
List<string>    GetAllOwnedItemIds()        // where quantity > 0
int             GetQuantity(string itemId)
string          GetQuantityDisplay(string itemId)  // "x99", "x5", "x0"
bool            UseItem(string itemId)              // decrements qty, fires OnInventoryChanged
void            AddItems(string itemId, int count)  // capped at MAX_STACK
```

**UseRepairKit integration:**
```csharp
/// <summary>
/// Uses a specific repair kit on a club. Returns (newDurability, success).
/// Called by the Club Selection Modal (Phase I-2).
/// </summary>
public (int newDurability, bool success) UseRepairKit(string itemId, PlayerClubData club)
{
    var template = ItemDatabaseCSV.Instance?.GetItem(itemId);
    if (template == null || GetQuantity(itemId) <= 0) return (club.currentDurability, false);

    int maxDur = club.maxDurability;
    int restored = Mathf.CeilToInt(maxDur * (template.restorePercent / 100f));
    int newDur = Mathf.Min(club.currentDurability + restored, maxDur);

    UseItem(itemId);
    return (newDur, true);
}
```

**Migration from RepairKitManager:**
- `ClubDetailPanel` and `ClubCompareController` currently call `RepairKitManager.Instance.UseBestKit()`
- These calls must be updated to use `ItemManager.Instance` instead
- The "auto-pick best kit" logic moves into `ItemManager`:

```csharp
/// <summary>
/// Auto-picks the best repair kit based on damage amount (same logic as old RepairKitManager).
/// Used by the Clubs tab one-tap repair button.
/// </summary>
public (int newDurability, string? kitUsed) UseBestRepairKit(int currentDur, int maxDur)
{
    if (currentDur >= maxDur) return (currentDur, null);

    float missingPercent = 1f - (float)currentDur / maxDur;

    // Try to match kit to damage level (don't waste mythic on small repairs)
    string? chosenId = ChooseBestKit(missingPercent);
    if (chosenId == null) return (currentDur, null);

    var template = ItemDatabaseCSV.Instance?.GetItem(chosenId);
    if (template == null) return (currentDur, null);

    int restored = Mathf.CeilToInt(maxDur * (template.restorePercent / 100f));
    int newDur = Mathf.Min(currentDur + restored, maxDur);

    UseItem(chosenId);
    return (newDur, chosenId);
}
```

**ChooseBestKit logic (3 tiers):**
- ≤50% missing → prefer Common (50% restore), fallback to Rare, then Mythic
- ≤75% missing → prefer Rare (75%), fallback to Mythic, then Common
- >75% missing → prefer Mythic (100%), fallback to Rare, then Common

### 1.5 Script Execution Order

Add to Project Settings > Script Execution Order:
- `ItemDatabaseCSV` → before `ItemManager`
- `ItemManager` → default

---

## 2. UI Layer

### 2.1 ItemCarouselController — `Assets/Scripts/UI/Inventory/ItemCarouselController.cs`

**Namespace:** `Golfin.Inventory`
**Pattern:** Clone of `BallCarouselController` with these changes:

| BallCarouselController | ItemCarouselController |
|------------------------|------------------------|
| `BallManager.Instance` | `ItemManager.Instance` |
| `BallDatabaseCSV.Instance` | `ItemDatabaseCSV.Instance` |
| `BallThumbnailCard` | `ItemThumbnailCard` (new) |
| `OnBallSelected` event | `OnItemSelected` event |
| `GetAllOwnedBallIds()` | `GetAllOwnedItemIds()` |
| `ballCardPrefab` | `itemCardPrefab` (uses BallThumbnailCard prefab!) |
| `ballEmptyCardPrefab` | `itemEmptyCardPrefab` (uses BallThumbnailEmptyCard prefab!) |

**Key:** Reuses `BallThumbnailCard` and `BallThumbnailEmptyCard` **prefabs** but
the controller creates `ItemThumbnailCard` component behaviour to bind item data.

Wait — actually, since the card prefab is `BallThumbnailCard.prefab` and has a
`BallThumbnailCard` MonoBehaviour attached, we need a different approach:

**APPROACH: Create `ItemThumbnailCard.cs` that mirrors `BallThumbnailCard.cs`**
- Same SerializeField layout (portrait, name, quantity, highlight, background, button)
- `Initialize(string itemId)` — reads from `ItemDatabaseCSV` + `ItemManager`
- Sets rarity background via `Resources.Load<Sprite>($"Rarities/{rarity}")` (unlike balls which use Common for all)
- Sets quantity text from `ItemManager.GetQuantityDisplay()`
- Same selection animation

**BUT** we reuse the BallThumbnailCard **prefab** structure (clone it) and swap the script at runtime? No — cleaner to just **clone the BallThumbnailCard prefab** as `ItemThumbnailCard.prefab` and replace the `BallThumbnailCard` component with `ItemThumbnailCard` in the editor.

**Builder script creates this prefab.** (See §3 Editor Scripts.)

### 2.2 ItemDetailPanel — `Assets/Scripts/UI/Inventory/ItemDetailPanel.cs`

**Namespace:** `Golfin.Inventory`

Layout from mockup (no stat bars):

**Left Panel:**
- `itemImage` (Image) — full sprite
- `brandText` (TMP) — "GOLFIN" (brand label on the image)

**Bottom Left (below image):**
- `infoHeader` (TMP) — "INFO"
- `infoText` (TMP) — long description

**Right Panel:**
- `itemNameText` (TMP) — "REPAIR KIT"
- `rarityLabel` (TMP) — "COMMON" / "RARE" / "MYTHIC"
- `quantityText` (TMP) — "x99"
- `restoresLabel` (TMP) — "RESTORES"
- `effectText` (TMP) — "DURABILITY 50%" (with checkmark icon — use existing Image+TMP combo)
- `proTipHeader` (TMP) — "*PRO TIP"
- `proTipText` (TMP) — tip description
- `compareButton` (Button) — **always interactable = false** (grayed out)
- `useButton` (Button) — gold button, fires `OnUseClicked`

**Events:**
```csharp
public event System.Action<string>? OnUseItem;  // passes current itemId

private void OnUseClicked()
{
    if (!string.IsNullOrEmpty(currentItemId))
        OnUseItem?.Invoke(currentItemId);
}
```

**UpdatePanel(string itemId):**
- Reads `ItemDatabaseCSV.Instance.GetItem(itemId)` for template data
- Reads `ItemManager.Instance.GetItemData(itemId)` for quantity
- Sets rarity label text + color via `RarityHelper`
- Sets effect text: `$"DURABILITY {template.restorePercent}%"`
- Disables USE button if quantity == 0

**Subscribes to:**
- `ItemCarouselController.OnItemSelected` → `UpdatePanel`
- `ItemManager.Instance.OnInventoryChanged` → refresh current panel
- `LocalizationManager.OnLanguageChanged` → refresh localized text

---
