# PATTERNS.md — Recurring Code Patterns

> Reference for Claude Code when implementing new features.
> Instead of re-explaining these in every TellCode spec, just say "follow X pattern."
> Updated: 2026-03-26

---

## 1. ModalController Pattern

**Base class:** `Golfin.UI.Modals.ModalController`  
**Fields:** `modalPanel` (GO), `backdrop` (GO), `closeButton` (Button)  
**Behavior:** Root GO stays active always. Only `modalPanel` is toggled by `Show()`/`Hide()`. Never deactivate the root.

**To create a new modal:**
1. Create a new class inheriting `ModalController`
2. Override `OnShow()` / `OnHide()` for custom logic
3. Scene hierarchy: `ModalName → Backdrop + ModalPanel → (content sections)`
4. The `ModalPanel` needs a `CanvasGroup` (auto-added if missing) for fade animation
5. Wire `modalPanel` and `backdrop` fields via AutoWire script
6. Parent the modal under the screen's Canvas root for correct positioning

**Gotcha:** If the modal is parented inside a screen hierarchy (not Canvas root), don't do runtime anchor repositioning — position it in the editor instead.

---

## 2. AutoWire Pattern (Editor Scripts)

**Purpose:** Wire `[SerializeField]` references in the Inspector automatically via menu items.  
**Namespace:** `Golfin.Roster.Editor` or `Golfin.Inventory.Editor`  
**Guard:** Wrap entire file in `#if UNITY_EDITOR` / `#endif`

**Standard structure:**
```csharp
#if UNITY_EDITOR
[MenuItem("GOLFIN/Wire/Feature Name")]
public static void Wire()
{
    // 1. Find root object (use FindObjectOfType<T>(true) to include inactive)
    // 2. Create SerializedObject from the component
    // 3. Wire fields using helper methods
    // 4. ApplyModifiedProperties + SetDirty
    // 5. Wire back-references on other components if needed
}
#endif
```

**Critical rules:**
- `GameObject.Find()` misses inactive objects → use `Resources.FindObjectsOfTypeAll<GameObject>()` filtered by `go.scene.isLoaded`
- `FindObjectOfType<T>()` misses inactive → always pass `true` (includeInactive)
- Helper methods: `WireGO`, `WireRT`, `WireTMP`, `WireTMPFrom`, `WireButton`, `WireButtonFrom`, `WireImage`, `WireImageFrom`
- Each helper: find property → find transform → get component → set objectReferenceValue
- Return `int` (0 or 1) and increment `ref failed` on miss
- Deep search variant (`WireTMPDeep`, `WireButtonDeep`) for when exact path is unknown

**Back-reference pattern:** After wiring the main component, also wire references on related components (e.g., `CharacterDetailPanel.compareController`, `ClubDetailPanel.levelUpModal`).

---

## 3. Builder Pattern (Editor Scripts)

**Purpose:** Create UI GameObjects in the scene hierarchy via menu items.  
**When:** Adding new UI elements (DiffLabels, stat rows, modal sections) that need specific RectTransform/Layout/TMP setup.

**Standard structure:**
```csharp
[MenuItem("GOLFIN/Build/Feature Name")]
public static void Build()
{
    // 1. Find parent object (use Resources.FindObjectsOfTypeAll for inactive)
    // 2. Check if child already exists (idempotent — skip if present)
    // 3. Create GameObject, parent it, set sibling index
    // 4. Add components: LayoutElement, TextMeshProUGUI, Image, etc.
    // 5. Set properties (fontSize, color, alignment, preferredWidth)
    // 6. SetActive(false) if hidden by default
    // 7. Undo.RegisterCreatedObjectUndo for undo support
}
```

**Execution order:** Always run Builder first, then AutoWire.
- `GOLFIN/Build/...` → creates GameObjects
- `GOLFIN/Wire/...` → wires SerializeField references

---

## 4. CSV → Manager Data Flow

**Pattern:** CSV file → Database singleton → Manager singleton → UI

**Database layer** (`CharacterDatabaseCSV`, `ClubDatabaseCSV`, `BagDatabaseCSV`):
- Singleton with `Instance` property
- `[SerializeField] TextAsset` for the CSV file
- Parses CSV in `Awake()` into a `Dictionary<string, DataRuntime>`
- Provides lookup methods: `GetCharacter(id)`, `GetAllCharacters()`, etc.
- Sprites loaded via `Resources.Load<Sprite>("path")` — not Inspector assignment

**Manager layer** (`CharacterManager`, `ClubManager`, `BagManager`):
- Singleton, `DontDestroyOnLoad`
- Holds player-specific runtime data (levels, SP, equipped state)
- Exposes C# `Action` events: `OnCharacterSelected`, `OnClubRepaired`, etc.
- Methods modify state and fire events
- UI subscribes to events, never polls

**Script Execution Order:** Database = -200, Manager = -100 (so Database loads before Manager reads it)

---

## 5. Stat Display Pattern

Two variants exist — character stats and club stats have different child structures.

### Character Stat Row
```
CharacterStats1 (HorizontalLayoutGroup)
├── Name+Bar
│   ├── StatsName      (TMP — stat label)
│   └── Bar            (Image — fill amount 0-1)
├── DiffLabel          (TMP — "+5"/"-3", hidden by default, compare only)
└── StatNumber         (TMP — "25")
```

### Club Stat Row
```
PowerRow (HorizontalLayoutGroup)
├── StatsName          (TMP — stat label)
├── Bar                (Image — fill amount 0-1)
├── DiffLabel          (TMP — compare only)
└── StatNumber         (TMP — "25")
```

**Stat bar fill:** `bar.fillAmount = currentValue / (float)maxValue`  
**Diff labels:** Green `#33CC4D` for positive, Red `#CC3333` for negative, hidden when equal.  
**Colors (exact):**
- `DiffPositiveColor = new Color(0.2f, 0.8f, 0.18f, 1f)`
- `DiffNegativeColor = new Color(0.9f, 0.2f, 0.2f, 1f)`

---

## 6. Compare Mode Pattern

**Structure:** DetailPanel has both `RightPanel` (normal) and `CompareRightPanel` (compare mode).  
**CompareRightPanel** contains `ComparePlaceholder` (shown when no right character selected) and `CompareInfoPanel` (clone of RightPanel's info section).

**Flow:**
1. Tap Compare → `EnterCompareMode()`: shrink left panel, show CompareRightPanel with placeholder
2. Tap a carousel card → `SetCompareCharacter(id)`: hide placeholder, show CompareInfoPanel, populate right column
3. Tap Close → `ExitCompareMode()`: restore layout, hide CompareRightPanel

**Left column** shows original selection. **Right column** shows comparison target.  
**Diff = right - left** (positive means right character/club is better).

**Key fields on CompareController:**
- `_leftCharacterId` / `_rightCharacterId`
- `compareRightPanel`, `comparePlaceholder`, `compareInfoPanel`
- `verticalDivider`

---

## 7. Event-Driven UI Pattern

**Subscribe in `OnEnable`, unsubscribe in `OnDisable`:**
```csharp
private void OnEnable()
{
    CharacterManager.Instance.OnCharacterSelected += HandleCharacterSelected;
}

private void OnDisable()
{
    CharacterManager.Instance.OnCharacterSelected -= HandleCharacterSelected;
}
```

**Manager fires events after state change:**
```csharp
public event Action<string>? OnCharacterSelected;

public void SelectCharacter(string characterId)
{
    _selectedCharacterId = characterId;
    OnCharacterSelected?.Invoke(characterId);
}
```

**UI never modifies data directly** — always goes through Manager methods.

---

## 8. Localization Key Pattern

**Naming:** `roster.levelup.cost`, `inventory.repair.confirm`, `bag.modal.header`  
**Character/Club names:** `nameKey` field in CSV, looked up at display time  
**Bio text:** `bioKey` field in CSV  
**Static labels:** Wired as `[SerializeField] TMP` fields, set in `Awake()` or `OnShow()` via localization lookup  
**Pattern exists but not fully wired** — keys are defined, lookup system TBD.

---

## Quick Reference: File Locations

| Pattern | Character (Roster) | Club (Inventory) |
|---|---|---|
| Database CSV | `Roster/Managers/CharacterDatabaseCSV.cs` | `Inventory/ClubDatabaseCSV.cs` |
| Manager | `Roster/Managers/CharacterManager.cs` (external) | `Inventory/ClubManager.cs` (external) |
| Detail Panel | `Roster/UI/CharacterDetailPanel.cs` | `Inventory/ClubDetailPanel.cs` |
| Compare Controller | `Roster/UI/CompareController.cs` | `Inventory/ClubCompareController.cs` |
| Level Up Modal | `Roster/UI/LevelUpModalController.cs` | `Inventory/ClubLevelUpModalController.cs` |
| AutoWire (Detail) | `Roster/Editor/DetailPanelAutoWire.cs` | `Inventory/Editor/ClubDetailPanelAutoWire.cs` |
| AutoWire (Compare) | `Roster/Editor/CompareAutoWire.cs` | `Inventory/Editor/ClubCompareAutoWire.cs` |
| AutoWire (LevelUp) | `Roster/Editor/LevelUpModalAutoWire.cs` | `Inventory/Editor/ClubLevelUpModalAutoWire.cs` |
| Carousel | `Roster/UI/CarouselController.cs` | `Inventory/ClubCarouselController.cs` |
| Thumbnail Card | `Roster/UI/CharacterThumbnailCard.cs` | `Inventory/ClubThumbnailCard.cs` |

All paths relative to `Assets/Scripts/UI/`.
