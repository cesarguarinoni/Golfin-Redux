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
| Items | (TBD) | (TBD) |
| Bags | `Assets/Scripts/UI/Inventory/BagSelectionModalController.cs` | — |
| Managers | `Assets/Scripts/ClubManager.cs`, `Assets/Scripts/BallManager.cs` | `Assets/Scripts/UI/Inventory/Editor/*ManagerSetup.cs` |

### Data
| File | Location |
|------|----------|
| Clubs.csv | `Assets/Data/Clubs.csv` |
| Balls.csv | `Assets/Data/Balls.csv` |
| Club sprites (thumb) | `Resources/Clubs/Portraits/` |
| Club sprites (full) | `Resources/Clubs/Full/` |
| Ball sprites (thumb) | `Resources/Balls/Thumbnails/` |
| Ball sprites (full) | `Resources/Balls/Full/` |
| Rarity backgrounds | `Resources/Rarities/` (Common, Uncommon, Rare, Mythic, Legendary, Supreme) |

### Prefabs
| Prefab | Location |
|--------|----------|
| ClubThumbnailCard | `Assets/Prefabs/UI/Inventory/ClubThumbnailCard.prefab` |
| BallThumbnailCard | `Assets/Prefabs/UI/Inventory/BallThumbnailCard.prefab` |
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

| Field | Characters | Clubs | Balls | Items |
|-------|-----------|-------|-------|-------|
| Rarity | ✅ 6 tiers | ✅ 6 tiers | ❌ None | ❌ None |
| Level | ✅ | ✅ (rarity-based start) | ❌ | ❌ |
| Stats | 4 (STR/CC/REC/STA) | 5 bars + Distance | 5 bars (-10 to +10) | ❌ |
| Durability | ❌ | ✅ (current/max) | ❌ | ❌ |
| Equip | ❌ (selected differently) | ✅ (bag slots) | ❌ (chosen in-game) | ❌ (use from inventory) |
| Quantity | ❌ (unique) | ❌ (unique) | ✅ (stack to 99, ∞) | ✅ (stack to 99) |
| SP Allocation | ✅ (ManualSP) | ✅ (ManualSP) | ❌ | ❌ |
| Compare | ✅ | ✅ | ✅ (future) | ❌ |

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

## Items Screen (Future — Tab Index 3)

Not yet implemented. Based on confluence doc:
- Shows owned consumable items (repair kits, etc.)
- Each item has: name, quantity, image, text description
- "Use" button to consume selected item
- No stats, no rarity, no level
- Simplest of all inventory screens
- Pattern: flat list, no carousel pagination needed (or reuse carousel)
