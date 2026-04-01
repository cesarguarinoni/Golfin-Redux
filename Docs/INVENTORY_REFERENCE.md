# INVENTORY_REFERENCE.md — Quick Reference for Inventory Screens

> Created 2026-03-27 by Claude (Architect).
> Use this doc to avoid re-reading every file when building new inventory screens.
> Covers patterns, file locations, and key APIs.

---

## Architecture Pattern (all inventory screens follow this)

```
CSV file (Assets/Data/)
  → DatabaseCSV singleton (loads CSV, resolves sprites)
    → Manager singleton (owns player state, fires events)
      → CarouselController (paginated card grid, fires OnXSelected)
        → ThumbnailCard (individual card component)
      → DetailPanel (subscribes to carousel selection, binds data)
      → CompareController (optional — side-by-side stat comparison)
```

Each screen has matching Editor scripts:
- `*Builder.cs` — creates prefabs or UI hierarchies (MenuItem under `GOLFIN/Setup/`)
- `*AutoWire.cs` — wires SerializeField references (MenuItem under `GOLFIN/Wire/`)
- `*ManagerSetup.cs` — creates singleton GOs in scene

---

## File Locations

### Scripts
| System | Runtime Scripts | Editor Scripts |
|--------|----------------|----------------|
| Clubs | `Assets/Scripts/UI/Inventory/Club*.cs` | `Assets/Scripts/UI/Inventory/Editor/Club*.cs` |
| Balls | `Assets/Scripts/UI/Inventory/Ball*.cs` | `Assets/Scripts/UI/Inventory/Editor/Ball*.cs` |
| Items | `Assets/Scripts/UI/Inventory/Item*.cs` | `Assets/Scripts/UI/Inventory/Editor/Item*.cs` |
| Bags | `Assets/Scripts/UI/Inventory/Bag*.cs` | (auto-wire TBD) |
| Managers | `Assets/Scripts/ClubManager.cs`, `Assets/Scripts/BallManager.cs`, `Assets/Scripts/BagManager.cs`, `Assets/Scripts/ItemManager.cs` | `Assets/Scripts/UI/Inventory/Editor/*ManagerSetup.cs` |

### Data
| File | Location |
|------|----------|
| Clubs.csv | `Assets/Data/Clubs.csv` |
| Balls.csv | `Assets/Data/Balls.csv` |
| Club sprites (thumb) | `Resources/Clubs/Portraits/` |
| Club sprites (full) | `Resources/Clubs/Full/` |
| Ball sprites (thumb) | `Resources/Balls/Thumbnails/` |
| Ball sprites (full) | `Resources/Balls/Full/` |
| Items.csv | `Assets/Data/Items.csv` |
| Item sprites (thumb) | `Resources/Items/Thumbnails/` |
| Item sprites (full) | `Resources/Items/Full/` |
| Bags.csv | `Assets/Data/Bags.csv` |
| Bag sprites (thumb) | `Resources/Bags/Thumbnail/` |
| Bag sprites (full) | `Resources/Bags/Full/` |
| Rarity backgrounds | `Resources/Rarities/` (Common, Uncommon, Rare, Mythic, Legendary, Supreme) |

### Prefabs
| Prefab | Location |
|--------|----------|
| ClubThumbnailCard | `Assets/Prefabs/UI/Inventory/ClubThumbnailCard.prefab` |
| BallThumbnailCard | `Assets/Prefabs/UI/Inventory/BallThumbnailCard.prefab` |
| ItemThumbnailCard | `Assets/Prefabs/UI/Inventory/ItemThumbnailCard.prefab` |
| BagThumbnailCard | `Assets/Prefabs/UI/Inventory/BagThumbnailCard.prefab` |
| BagSwapClubCard | `Assets/Prefabs/UI/Inventory/BagSwapClubCard.prefab` |
| BagEmptyClubCard | `Assets/Prefabs/UI/Inventory/BagEmptyClubCard.prefab` |
| BagClubCard | `Assets/Prefabs/UI/Inventory/BagClubCard.prefab` |
| BagSlotPrefab | `Assets/Prefabs/UI/Inventory/BagSlotPrefab.prefab` |
| BagSlotLockedPrefab | `Assets/Prefabs/UI/Inventory/BagSlotLockedPrefab.prefab` |
| ItemUseClubCard | `Assets/Prefabs/UI/Inventory/ItemUseClubCard.prefab` |
| Source (character card) | `Assets/Prefabs/UI/Roster/CharacterThumbnailCardGlowUp.prefab` |

---

## InventoryScreenController (Tab System)

**File:** `Assets/Scripts/UI/Inventory/InventoryScreenController.cs`

- Tab index 0 = CLUBS
- Tab index 1 = BAGS
- Tab index 2 = BALLS
- Tab index 3 = ITEMS

Arrays: `tabButtons[]`, `tabPanels[]`, `tabIndicators[]`
- Active tab gets gold gradient text (`TextGradients.ApplyGold`)
- Inactive tabs get silver gradient (`TextGradients.ApplySilver`)
- Default tab on entry = 0 (CLUBS)

---

## Data Model Comparison

| Field | Characters | Clubs | Balls | Items | Bags |
|-------|-----------|-------|-------|-------|------|
| Rarity | ✅ 6 tiers | ✅ 6 tiers | ❌ None | ✅ 3 tiers | ✅ 6 tiers |
| Level | ✅ | ✅ (rarity-based start) | ❌ | ❌ | ❌ (future) |
| Stats | 4 (STR/CC/REC/STA) | 5 bars + Distance | 5 bars (-10 to +10) | ❌ | ❌ |
| Durability | ❌ | ✅ (current/max) | ❌ | ❌ | ❌ |
| Equip | ❌ (selected differently) | ✅ (bag slots) | ❌ (chosen in-game) | ❌ (use from inventory) | ✅ (one active bag) |
| Quantity | ❌ (unique) | ❌ (unique) | ✅ (stack to 99, ∞) | ✅ (stack to 99) | ❌ (unique) |
| SP Allocation | ✅ (ManualSP) | ✅ (ManualSP) | ❌ | ❌ | ❌ |
| Compare | ✅ | ✅ | ✅ (future) | ❌ | ❌ |
| Club Grid | ❌ | ❌ | ❌ | ❌ | ✅ (8-slot grid) |
| Description | Bio text | ❌ | ❌ | ✅ | ✅ (from CSV) |

---

## Singleton Pattern (all managers)

```csharp
public static XManager Instance { get; private set; } = null!;

private void Awake()
{
    if (Instance != null && Instance != this) { Destroy(gameObject); return; }
    Instance = this;
    DontDestroyOnLoad(gameObject);
    Initialize();
}

private void OnDestroy()
{
    if (Instance == this) Instance = null!;
}
```

Script Execution Order matters: DatabaseCSV must Awake before its Manager.

---

## Event Pattern (UI binding)

```csharp
// Subscribe in OnEnable, unsubscribe in OnDisable
private void OnEnable()
{
    carousel.OnXSelected += UpdatePanel;
    XManager.Instance.OnSomeEvent += HandleEvent;
    LocalizationManager.OnLanguageChanged += RefreshLocalizedText;
}

private void OnDisable()
{
    carousel.OnXSelected -= UpdatePanel;
    XManager.Instance.OnSomeEvent -= HandleEvent;
    LocalizationManager.OnLanguageChanged -= RefreshLocalizedText;
}
```

---

## Stat Bar Update Pattern

### Clubs (0 to max, always positive)
```csharp
bar.fillAmount = cap > 0 ? (float)value / cap : 0f;
// Color: blue normally, red when durability low
```

### Balls (-10 to +10, positive or negative)
```csharp
bar.fillAmount = (float)Mathf.Abs(value) / BALL_STAT_MAX;
bar.color = value >= 0 ? StatPositiveColor : StatNegativeColor;
// Number: "+10", "-6", "0"
```

### Characters (0 to rarity cap)
```csharp
bar.fillAmount = cap > 0 ? (float)value / cap : 0f;
// Color via RarityHelper
```

---

## CarouselController Pattern

All carousels share:
- `contentParent` (Transform) — holds card instances
- `cardPrefab` (GameObject) — instantiated per item
- `leftArrowButton` / `rightArrowButton` — pagination
- `paginationDotsParent` + `paginationDotPrefab` — page indicators
- `cardsPerPage = 6`
- `OnXSelected` event (Action<string>) — fired when a card is tapped
- `ScrollRect` for smooth page transitions

Clubs adds: `ClubFilterBar` for type filtering.
Balls: no filter bar.

---

## CSV Parser

Both `ClubDatabaseCSV` and `BallDatabaseCSV` use an identical `ParseCSVLine()` method that handles
quoted fields with commas. If a third CSV database is needed, consider extracting to a shared
`CSVParser` utility class.

---

## Localization Key Conventions

| Screen | Prefix | Examples |
|--------|--------|---------|
| Clubs | `CLUB_` | `CLUB_POWER`, `CLUB_ACCURACY`, `CLUB_EQUIP`, `CLUB_EQUIPPED` |
| Balls | `BALL_` | `BALL_POWER`, `BALL_REBOUND`, `BALL_OWNED` |
| Characters | (various) | `RARITY_COMMON`, `STAT_STRENGTH` |

---

## Existing Utilities (don't duplicate)

- `RarityHelper` — colors, labels, badge text colors for rarities
- `RarityStatCaps` — stat caps by rarity (characters only)
- `TextGradients` — `ApplyGold()`, `ApplySilver()` for tab/button text
- `LocalizationManager` — `LocalizationManager.Get(key)`, `OnLanguageChanged` event
- `RepairKitManager` — singleton for repair kit inventory
- `BagManager` — singleton for bag management
- `BagDatabaseCSV` — bag definitions

---

## Items Screen (Tab Index 3) — Phase I1

**Full spec:** `Docs/Specs/PHASE_I1_ITEMS_SCREEN.md`

Shows owned consumable items (repair kits for now, 3 tiers).

| System | Runtime Scripts | Editor Scripts |
|--------|----------------|----------------|
| Items | `Assets/Scripts/UI/Inventory/Item*.cs` | `Assets/Scripts/UI/Inventory/Editor/Item*.cs` |
| Manager | `Assets/Scripts/ItemManager.cs` | `Assets/Scripts/Editor/ItemManagerSetup.cs` |
| Database | `Assets/Scripts/UI/Inventory/ItemDatabaseCSV.cs` | (wired by setup) |

| Data | Location |
|------|----------|
| Items.csv | `Assets/Data/Items.csv` |
| Item sprites (thumb) | `Resources/Items/Thumbnails/` |
| Item sprites (full) | `Resources/Items/Full/` |

**Key differences from Balls:**
- Items HAVE rarity (Common/Rare/Mythic) — shown on card + detail panel
- Items have NO stat bars — instead show effect text ("DURABILITY 50%")
- Items have a USE button (triggers club selection modal in Phase I2)
- Compare button always grayed out (non-functional)
- Carousel reuses BallThumbnailCard prefab pattern (cloned + swapped component)
- Empty card: reuses BallThumbnailEmptyCard directly
- ItemManager replaces RepairKitManager as the single item inventory manager


---

## Bags Screen (Tab Index 1) — Phase J

Shows the player's golf bags. Each bag holds up to 8 clubs. One bag is "equipped" for gameplay.

| System | Runtime Scripts | Editor Scripts |
|--------|----------------|----------------|
| Bags | `Assets/Scripts/UI/Inventory/Bag*.cs` | (auto-wire TBD) |
| Manager | `Assets/Scripts/BagManager.cs` | `Assets/Scripts/UI/Inventory/Editor/BagManagerSetup.cs` |
| Database | `Assets/Scripts/BagDatabaseCSV.cs` | (wired by setup) |

| Data | Location |
|------|----------|
| Bags.csv | `Assets/Data/Bags.csv` |
| Bag sprites (thumb) | `Resources/Bags/Thumbnail/` |
| Bag sprites (full) | `Resources/Bags/Full/` |

### CSV Columns
`id, name, rarity, thumbnail, fullImage, description, unlocked`

### BagManager Key APIs
- `EquippedBagSlot` — which bag goes to the field (1-based, only one at a time)
- `EquipBag(int bagSlot)` — equips a bag, fires `OnEquippedBagChanged`
- `AssignClubToBag(clubId, bagSlot)` — adds club, fires `OnBagChanged`
- `RemoveClubFromBag(clubId)` — removes club, fires `OnBagChanged`
- `GetClubsInBag(bagSlot)` — returns `List<PlayerClubData>`
- `IsBagFull(bagSlot)` — checks 8-club limit

### Key Differences from Other Screens
- Bags have rarity but NO stats — detail panel shows description text instead
- Club grid uses existing `BagSwapClubCard` prefab (has `ItemUseClubCard` component for data binding)
- Empty slots use `BagEmptyClubCard` prefab
- Swap/Equip modal (`BagClubModalController`) is a single modal with `BagClubModalMode` enum
- Modal uses new `BagClubCard` component (not `ItemUseClubCard`) — action button text is configurable
- Carousel shows locked bags using `BagSlotLockedPrefab`
- `BagThumbnailCard` carousel card cloned from `BagSlotPrefab`
- Equipped bag button: gold (EQUIPPED, disabled) / silver (EQUIP, active)
