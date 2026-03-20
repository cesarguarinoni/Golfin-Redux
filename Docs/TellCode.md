# TellCode.md — Instructions from Claude (Architect) to Claude Code

> Claude Code: Read this file at the start of each task. Execute the latest instruction block.
> After completing, add a status line at the bottom: `✅ DONE: [date] [brief summary]`
> Claude (Architect) will update this file with new instructions as needed.

---

## Current Task (2026-03-20) — Club Inventory Phase C + Cleanup

### Priority 1: Quick Cleanup

**1a. Fix Guillermo's character ID in Characters.csv**
The character renamed from Alejandro to Guillermo still has `char_alejandro` as his ID. Change it to `char_guillermo`. Search the entire codebase for any references to `char_alejandro` and update them.

**1b. Add Screenshots to .gitignore**
Append this line to `.gitignore`:
```
Assets/Screenshots/
```

**1c. Delete stale files in project root**
If they still exist, delete these from the project root (NOT from Assets/Scripts):
- `RE2.sln`
- `Golfin Redux.sln` (the one with the space — keep `GolfinRedux.sln`)

**1d. Add ClubDatabaseCSV to Script Execution Order**
In Unity: Edit → Project Settings → Script Execution Order
- `ClubDatabaseCSV` at **-200** (same as CharacterDatabaseCSV)
- `ClubManager` at **-100** (same as CharacterManager)
If already set, verify the values.

---

### Priority 2: Club Inventory Phase C — Carousel + Detail Panel

The InventoryScreen shell exists (tabs, filter bar, ClubsMainSection placeholder). Now build the actual club carousel and detail panel inside ClubsMainSection.

**Reference spec:** `Docs/CLUB_INVENTORY_SPEC.md` — Sections 3, 5, 6, 8

**2a. Club Carousel**

Inside `ClubsContent > ClubsMainSection`, build:
```
ClubsMainSection
├── ClubCarouselSection
│   ├── LeftArrow (Button)
│   ├── RightArrow (Button)
│   ├── ScrollView
│   │   └── Viewport → Content (HorizontalLayoutGroup + ContentSizeFitter)
│   └── PaginationDots
└── ClubDetailPanel
    └── (built in step 2b)
```

Create `ClubCarouselController.cs` in `Assets/Scripts/UI/Inventory/`. It should:
- Follow the same pattern as `CarouselController.cs` from Roster
- Read clubs from `ClubManager.Instance.GetAllOwnedClubs()`
- Subscribe to `ClubFilterBar.OnFilterChanged` to filter by type
- Instantiate `ClubThumbnailCard` prefab for each club
- Fire `OnClubSelected(string clubId)` event when a card is tapped
- Support pagination dots and arrow navigation (same as Roster carousel)
- Cards per page = 6

**2b. Club Detail Panel Hierarchy**

Build under ClubsMainSection, below the carousel. Layout is DIFFERENT from Roster:

```
ClubDetailPanel
├── LeftPanel (~45% width)
│   ├── ClubImage (Image — top portion, club photo)
│   └── InfoSection (bottom portion)
│       ├── InfoHeader (TMP — "INFO")
│       └── InfoText (TMP — club description, wrapping)
│
└── RightPanel (~55% width)
    ├── ClubNameText (TMP — "IRON 7 MIREO")
    ├── StatusIcons (equipped icon, level-up ready)
    ├── Divider
    ├── RarityLevelRow
    │   ├── RarityLabel (TMP — "RARE", colored)
    │   ├── LevelText (TMP — "Lv 80")
    │   └── LevelTextMax (TMP — "/119")
    ├── Divider
    ├── StatsPanel
    │   ├── PowerRow (icon + name + bar + value)
    │   ├── AccuracyRow (icon + name + bar + value)
    │   ├── LieResistanceRow (icon + name + bar + value)
    │   ├── LoftRow (icon + name + bar + value)
    │   ├── DurabilityRow (icon + name + bar + value as current/max)
    │   └── DistanceRow (icon + "DISTANCE" + value + "yd" — NO bar, just text)
    ├── Divider
    ├── ButtonsPanel
    │   ├── LevelUpButton (Button — "LEVEL UP")
    │   └── RepairButton (Button — "REPAIR")
    ├── CompareButton (Button — "COMPARE")
    ├── BagLabel (TMP — "IN BAG 1", blue, only visible when equipped)
    └── EquipButton (Button — "EQUIP" / "EQUIPPED")
```

KEY LAYOUT DIFFERENCE: In Roster, the left panel is ONLY the full-body portrait. In Clubs, the left panel has the club image on TOP and the INFO text on BOTTOM. This is because clubs have 6 stats (vs 4) so the right panel needs full height for stats.

**2c. Create ClubDetailPanel.cs**

`Assets/Scripts/UI/Inventory/ClubDetailPanel.cs` — follows CharacterDetailPanel pattern but for clubs:

- Subscribe to `ClubCarouselController.OnClubSelected`
- In `UpdatePanel(string clubId)`:
  - Get `PlayerClubData` from `ClubManager.Instance.GetClubData(clubId)`
  - Get `ClubDataRuntime` from `ClubDatabaseCSV.Instance.GetClub(clubId)`
  - Set club image (portraitFull, fall back to portraitSprite, fall back to Placeholder)
  - Set name, rarity (use RarityHelper.GetRarityColor), level
  - Set 5 stat bars (Power, Accuracy, Lie Resistance, Loft, Durability)
  - Durability bar: use current/max format, turn RED when `playerClub.IsDurabilityLow`
  - Distance: just text, no bar — format as "{value} yd"
  - Set INFO text from template.info
  - Set BagLabel: "IN BAG {N}" if equipped, hide if not
  - EQUIP button: "EQUIPPED" (gold) if equipped, "EQUIP" (gold) if not
  - LEVEL UP: logs to console for now
  - REPAIR: logs to console for now
  - COMPARE: logs to console for now (Phase D)

- Stat bar filling uses `Image.fillAmount` — make sure Image Type is Filled
- Use `RarityHelper.GetRarityColor()` for rarity label color
- Use localization keys for all labels (add to LocalizationText.csv):
  ```
  CLUB_POWER,POWER,パワー
  CLUB_ACCURACY,ACCURACY,アキュラシー
  CLUB_LIE_RESISTANCE,LIE RESISTANCE,ライ耐性
  CLUB_LOFT,LOFT,ロフト
  CLUB_DURABILITY,DURABILITY,耐久性
  CLUB_DISTANCE,DISTANCE,飛距離
  CLUB_LEVEL_UP,LEVEL UP,レベルアップ
  CLUB_REPAIR,REPAIR,修理
  CLUB_COMPARE,COMPARE,比較
  CLUB_EQUIP,EQUIP,装備
  CLUB_EQUIPPED,EQUIPPED,装備済み
  CLUB_INFO,INFO,情報
  CLUB_IN_BAG,IN BAG,バッグ
  ```

**2d. Wire EQUIP button**

When EQUIP is tapped:
- Call `ClubManager.Instance.EquipClub(clubId, 1)` (Bag 1 only for now)
- Refresh panel to show "EQUIPPED" + "IN BAG 1"

When EQUIPPED is tapped on an already-equipped club:
- Call `ClubManager.Instance.EquipClub(clubId, 0)` (unequip)
- Refresh panel

**2e. Create Editor auto-wire + builder scripts**

Follow the pattern of DetailPanelAutoWire and InventoryScreenBuilder:
- `ClubDetailPanelBuilder.cs` — builds the hierarchy
- `ClubDetailPanelAutoWire.cs` — wires all serialized fields
- Add to GOLFIN menu

**2f. Stat bar icons**

The stat icons for clubs should be loaded from `Assets/Art/` or `Assets/Resources/`. Check what icon assets exist. The references show distinct icons for Power (muscle), Accuracy (crosshair), Lie Resistance (mountains), Loft (angle arrow), Durability (shield/checkmark), Distance (wavy line). If icons don't exist, use placeholder images and log warnings.

---

### Reminders
- Platform: Windows (PowerShell, no bash/chmod/sed)
- Use `== null` not `??` for Unity objects
- All new text must use `LocalizationManager.Get("KEY")`
- Load sprites via `Resources.Load<Sprite>()`, not Inspector arrays
- Image bars must have Image Type = Filled, Fill Method = Horizontal, Fill Origin = Left
- Push to GitHub after completing

---

## Completed Tasks

✅ DONE: 2026-03-20 — Task 1-4 (previous session): ScreenshotTool, compress script, CLAUDE.md update, root cleanup
✅ DONE: 2026-03-20 — Priority 1a: char_alejandro → char_guillermo in Characters.csv (only reference was in CSV)
✅ DONE: 2026-03-20 — Priority 1b: Assets/Screenshots/ added to .gitignore
✅ DONE: 2026-03-20 — Priority 1c: Deleted RE2.sln and "Golfin Redux.sln" from project root
✅ DONE: 2026-03-20 — Priority 1d: ⚠️ Script Execution Order must be set manually in Unity: Edit → Project Settings → Script Execution Order → ClubDatabaseCSV = -200, ClubManager = -100
✅ DONE: 2026-03-20 — Priority 2a: ClubCarouselController.cs created (Assets/Scripts/UI/Inventory/)
✅ DONE: 2026-03-20 — Priority 2b/2e: ClubDetailPanelBuilder.cs — GOLFIN/Inventory/Build Club Phase C menu item
✅ DONE: 2026-03-20 — Priority 2c/2d: ClubDetailPanel.cs with all stat bars, equip toggle, event wiring
✅ DONE: 2026-03-20 — Priority 2e: ClubDetailPanelAutoWire.cs — GOLFIN/Inventory/Wire Club Detail Panel menu item
✅ DONE: 2026-03-20 — Localization: 13 CLUB_ keys added to LocalizationText.csv (EN + JP)

⚠️ MANUAL STEPS REQUIRED (cannot be done via code):
1. Unity: Edit → Project Settings → Script Execution Order
   ClubDatabaseCSV = -200, ClubManager = -100
2. In Play mode / scene: Run GOLFIN/Inventory/Build Club Phase C
3. Run GOLFIN/Inventory/Wire Club Detail Panel
4. Assign ClubThumbnailCard.prefab to ClubCarouselController.clubCardPrefab in Inspector
5. Assign ClubFilterBar to ClubCarouselController.filterBar in Inspector
