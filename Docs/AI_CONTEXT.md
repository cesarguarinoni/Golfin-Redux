# AI Context — Golfin Redux

## Current Phase: Club Inventory Phase B ✅ COMPLETE (Phase C next)

### Status

#### Phase 2b — Roster Detail Panel ✅ COMPLETE
- [x] Carousel fully working: correct sizes, all 12 characters, bounce animation, viewport clip fix
- [x] Full-body portrait loading via `portraitFull` CSV column
- [x] SELECT button fully working — interactable state, SELECTED text
- [x] Level Up / Boost button interactable state
- [x] Rarity badge background removed

#### Phase 2c — Level Up Modal ✅ COMPLETE
- [x] LevelUpModalController — preview-only flow, nothing commits until CONFIRM
- [x] LevelUpModalBuilder — Editor script builds hierarchy (one-time use)
- [x] LevelUpModalAutoWire — wires all fields including CharacterDetailPanel.levelUpModal
- [x] SP color preview, level text color, stat value split, RP flow verified
- [x] RewardPointsDebugPanel — backtick toggle, runtime RP debug
- [x] Modal not appearing bug fixed

#### Phase 2d — Compare & Swap ✅ COMPLETE
- [x] CompareController — state machine, carousel interception, fade/slide animation
- [x] CompareRightPanelBuilder — clones RightPanel exactly (fonts/colors/positions preserved)
- [x] CompareAutoWire — wires all 27 fields (0 failed after path fixes)
- [x] CharacterDetailPanel — IsCompareMode guard, OnCompareClicked, ShowCharacter() public method
- [x] Top/bottom bar visibility — ScreenManager.ApplyScreen() calls ShowBars/HideBars
- [x] BigRoster portrait hides immediately on compare enter (SafeSetActive, not CanvasGroup fade)
- [x] Placeholder hides immediately when right character is selected (SafeSetActive)
- [x] Stat bars force Image.Type.Filled in UpdateCompareStatRow
- [x] CanvasGroup pre-added in builder; GetOrAddCG uses == null not ?? (Unity null safety)
- [x] After swap: detail panel updates to show newly selected character (CommitSwapAndExit + ShowCharacter)

#### Club Inventory Phase A — Foundation ✅ COMPLETE
- [x] ClubThumbnailCard.cs — mirrors CharacterThumbnailCard pattern, reads from ClubManager/ClubDatabaseCSV
- [x] ClubThumbnailCardBuilder.cs — duplicates CharacterThumbnailCardGlowUp prefab, swaps component, wires fields
- [x] Rarity backgrounds loaded from shared Resources/Rarities/ folder

#### Club Inventory Phase B — InventoryScreen ✅ COMPLETE
- [x] InventoryScreen hierarchy built by InventoryScreenBuilder.cs (GOLFIN/Build Inventory Screen)
- [x] ScreenId.Inventory added to ScreenManager enum
- [x] ScreenManager._inventoryScreen wired; ApplyScreen() activates/deactivates correctly
- [x] ScreenManager.ApplyScreen() shows persistent bars on Inventory screen
- [x] PersistentUIManager.NavigateTo() implemented — routes to ScreenManager.ShowScreen()
- [x] Tab bar: CLUBS / BAGS / BALLS / ITEMS with underline indicators
- [x] InventoryScreenController.cs — tab switching, content panel visibility
- [x] ClubFilterBar.cs — 8 filter buttons (ALL/DRIVERS/WOODS/IRONS/A.WEDGES/P.WEDGES/S.WEDGES/PUTTERS)
- [x] FilterBarPatcher.cs — surgical patch: removes ScrollRect/Mask, adds flat HLG, rebuilds buttons
- [x] **FilterBar visibility root cause found and fixed**: InventoryScreen root RT was not offset for nav bars
  - TopBar = 321px (top-anchored), BottomNavBar = 196px (bottom-anchored)
  - Fixed: `offsetMin.y = +196`, `offsetMax.y = -321`
  - GOLFIN/Patch - Fix InventoryScreen Layout — surgical scene patcher (no full rebuild needed)
  - InventoryScreenBuilder.BuildRoot() updated with correct offsets for future rebuilds

#### Codebase Health Fixes (this session)
- [x] Archived 9 obsolete editor scripts → Assets/Scripts/Editor/Archive/ (MenuItem attributes stripped)
- [x] RosterDebugTools.cs created (GOLFIN/Debug/ menu items)
- [x] FadeController.cs — removed DontDestroyOnLoad (was non-root child of Canvas)
- [x] CharacterThumbnailCard.cs — CSV-first fix (no longer triggers SO lookup when CSV has data)
- [x] CharacterDetailPanel.cs — same CSV-first fix
- [x] CharacterManager.RefreshStatValues() — fixed silent stat bug (was returning early for CSV-only chars)

---

### Workflow Rules
- **Push to GitHub after every change**

### Key Files (Phase B additions)
- `Assets/Scripts/UI/Inventory/Editor/InventoryScreenBuilder.cs` — builds full InventoryScreen hierarchy; TOPBAR_H=321, BOTTOMNAV_H=196 constants
- `Assets/Scripts/UI/Inventory/Editor/FilterBarPatcher.cs` — surgical FilterBar rebuild (GOLFIN/Patch - Rebuild FilterBar)
- `Assets/Scripts/UI/Inventory/InventoryScreenController.cs` — tab management
- `Assets/Scripts/UI/Inventory/ClubFilterBar.cs` — filter type selection, fires OnFilterChanged
- `Assets/Scripts/UI/Inventory/ClubThumbnailCard.cs` — club card, reads from ClubManager/ClubDatabaseCSV
- `Assets/Scripts/UI/Inventory/Editor/ClubThumbnailCardBuilder.cs` — duplicates CharacterThumbnailCardGlowUp prefab
- `Assets/Scripts/UI/ScreenManager.cs` — screen transitions + bar visibility (includes ScreenId.Inventory)
- `Assets/Scripts/UI/PersistentUIManager.cs` — NavigateTo() routes Inventory tab to ScreenManager

### Known Nav Bar Heights (ShellScene.unity YAML — verified)
```
TopBar:       anchorMin/Max=(0.5,1), sizeDelta=(1178, 321), pivot=(0.5,1)  → 321px, top-anchored
BottomNavBar: anchorMin/Max=(0.5,0), sizeDelta=(1178, 196), pivot=(0.5,0) → 196px, bottom-anchored
RosterScreen: full stretch (0,0)→(1,1), sizeDelta=(0,0) — internal sections offset manually
InventoryScreen: full stretch (0,0)→(1,1), offsetMin.y=+196, offsetMax.y=-321 (fixed this session)
```

### Known AutoWire Paths (verified against ShellScene.unity YAML)
```
RarityRow children:
  RarityPanel/RarityRow/RarityText           (NOT RarityLabel)
  RarityPanel/RarityRow/LevelPanel/LevelText     (nested in LevelPanel)
  RarityPanel/RarityRow/LevelPanel/LevelTextMax  (nested in LevelPanel)

SelectButton text child:
  SelectButton/Text (TMP)                    (NOT "Text")
```

### Blockers
- None.

### What's Next (Club Inventory Phase C)
1. **Run GOLFIN/Patch - Fix InventoryScreen Layout** (or re-run GOLFIN/Build Inventory Screen) to apply the nav bar offset fix to the scene
2. **Run GOLFIN/Setup Club Thumbnail Card Prefab** to create ClubThumbnailCard.prefab (builder exists, prefab not yet generated)
3. **Phase C — Carousel + Detail Panel**:
   - ClubDetailPanel hierarchy (stat bars, portrait, EQUIP button)
   - Wire carousel to ClubManager data filtered by ClubFilterBar.OnFilterChanged
   - EQUIP button logs to console (Phase E wires actual equip logic)
4. **Phase D** — Compare mode (stat differences: green +N / red -N)
5. **Phase E** — LEVEL UP / REPAIR log to console, Bag 1 direct equip

### Session startup reminder
Run: `powershell -File Docs/generate_audit.ps1 > Docs/ARCHITECTURE_AUDIT.md`
Then read: Docs/AI_CONTEXT.md, Docs/ARCHITECTURE_AUDIT.md, any Docs/PHASE_*_SPEC.md, tasks/lessons.md
