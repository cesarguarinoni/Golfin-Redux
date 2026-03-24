# TellCode.md — Instructions from Claude (Architect) to Claude Code

> Claude Code: Read this file at the start of each task. Execute the latest instruction block.
> After completing, add a status line at the bottom: `✅ DONE: [date] [brief summary]`
> Claude (Architect) will update this file with new instructions as needed.

---

## Current Task (2026-03-25) — Project Cleanup: Menu, Assets, File Tree

Three cleanup tasks. Do them in order.

---

### Task 1: Reorganize GOLFIN Unity Menu

The menu has 30+ items scattered across the root and inconsistent submenus. Reorganize into clean categories. **Only change the `[MenuItem(...)]` paths — do NOT rename files or move code.**

**New menu structure:**

```
GOLFIN/
├── Build/
│   ├── Inventory Screen
│   └── Club Compare Panel
│
├── Wire/
│   ├── Roster Detail Panel
│   ├── Roster Compare Panel
│   ├── Roster Level Up Modal
│   ├── Club Detail Panel
│   └── Club Compare Panel
│
├── Setup/
│   ├── Club Managers
│   ├── Club Thumbnail Card Prefab
│   ├── Pagination Dots
│   └── Status Icons (All)
│
├── Debug/
│   ├── List All Characters
│   ├── Validate References
│   ├── Grant 100000 RP
│   ├── Reset Player Progress
│   └── Remove Missing Scripts
│
├── Screenshot/
│   ├── Capture Game View
│   └── Capture Named
│
└── Utilities/
    └── Deactivate Unnecessary Screens
```

**Items to ARCHIVE (move MenuItems to comments, move files to Editor/Archive):**
These are one-time patches that are no longer needed:
- `Patch - Fix InventoryScreen Layout` (InventoryScreenBuilder.cs — the patch method only)
- `Patch - Rebuild FilterBar` (FilterBarPatcher.cs)
- `Patch Club Inventory/All Fixes` and all sub-patches (ClubInventoryPatcher.cs — entire file)
- `Build Status Icons — 1. Detail Panel` (keep only "All" variant)
- `Build Status Icons — 2. Card Prefab` (keep only "All" variant)
- `Build Status Icons — 3. Compare Panel` (keep only "All" variant)
- `Build Club Phase C` (one-time builder, already run)
- `Tools/GOLFIN/!!! CLEANUP` and `!!! RESTORE` (MenuItemRemover.cs — archive entire file)

**Files to keep:**

| Current MenuItem | New MenuItem | File |
|---|---|---|
| `GOLFIN/Build Inventory Screen` | `GOLFIN/Build/Inventory Screen` | InventoryScreenBuilder.cs |
| `GOLFIN/Inventory/Build Club Compare Panel` | `GOLFIN/Build/Club Compare Panel` | ClubCompareRightPanelBuilder.cs |
| `GOLFIN/Wire Detail Panel` | `GOLFIN/Wire/Roster Detail Panel` | DetailPanelAutoWire.cs |
| `GOLFIN/Wire Compare Panel` | `GOLFIN/Wire/Roster Compare Panel` | CompareAutoWire.cs |
| `GOLFIN/Wire Level Up Modal` | `GOLFIN/Wire/Roster Level Up Modal` | LevelUpModalAutoWire.cs |
| `GOLFIN/Inventory/Wire Club Detail Panel` | `GOLFIN/Wire/Club Detail Panel` | ClubDetailPanelAutoWire.cs |
| `GOLFIN/Inventory/Wire Club Compare Panel` | `GOLFIN/Wire/Club Compare Panel` | ClubCompareAutoWire.cs |
| `GOLFIN/Setup Club Managers` | `GOLFIN/Setup/Club Managers` | ClubManagerSetup.cs |
| `GOLFIN/Setup Club Thumbnail Card Prefab` | `GOLFIN/Setup/Club Thumbnail Card Prefab` | ClubThumbnailCardBuilder.cs |
| `GOLFIN/Setup Pagination Dots` | `GOLFIN/Setup/Pagination Dots` | PaginationDotSetup.cs |
| `GOLFIN/Build Status Icons (All)` | `GOLFIN/Setup/Status Icons (All)` | StatusIconBuilder.cs |
| `GOLFIN/Debug/*` | `GOLFIN/Debug/*` (keep as-is) | RosterDebugTools.cs |
| `GOLFIN/Screenshot/*` | `GOLFIN/Screenshot/*` (keep as-is) | ScreenshotTool.cs |
| `GOLFIN/Deactivate Unnecessary Screens` | `GOLFIN/Utilities/Deactivate Unnecessary Screens` | ScreenDeactivator.cs |

---

### Task 2: Asset Naming Convention

Rename art asset folders to use PascalCase with no spaces. This doesn't affect code wiring because sprites are loaded via `Resources.Load()` using the filenames inside the folders, not the folder names under `Art/`.

**Art folder renames:**

| Current | New |
|---|---|
| `Assets/Art/Clubs Inventory` | `Assets/Art/ClubsInventory` |
| `Assets/Art/Home Screen` | `Assets/Art/HomeScreen` |
| `Assets/Art/Loading Screen` | `Assets/Art/LoadingScreen` |
| `Assets/Art/Logo Screen` | `Assets/Art/LogoScreen` |
| `Assets/Art/Roster Screen` | `Assets/Art/RosterScreen` |
| `Assets/Art/Splash Screen` | `Assets/Art/SplashScreen` |

**IMPORTANT:** Before renaming, search for any code that references these folder paths directly (e.g., `"Art/Roster Screen/"`). There might be some in Editor scripts like StatusIconBuilder.cs or ClubInventoryPatcher.cs that load sprites by path. Update those references.

```powershell
# Search for Art folder references in code
Get-ChildItem -Path "C:\Users\cesar\GolfinRedux\Assets\Scripts" -Recurse -Filter "*.cs" | Select-String "Art/" | Select-Object Filename, Line
```

**Reference folder renames:**

| Current | New |
|---|---|
| `Assets/References/Home Screen` | `Assets/References/HomeScreen` |
| `Assets/References/Loading Screen` | `Assets/References/LoadingScreen` |
| `Assets/References/Logo Screen` | `Assets/References/LogoScreen` |
| `Assets/References/Roster Screen` | `Assets/References/RosterScreen` |
| `Assets/References/Splash Screen` | `Assets/References/SplashScreen` |

These are reference images only — no code references them. Safe to rename.

**Sprite naming convention (for NEW sprites going forward — don't rename existing ones):**
- Thumbnails: `{CharacterName}.png` (e.g., `James.png`) — already follows this
- Full body: `BigRoster{Name}.png` — already follows this
- Club portraits: `{ClubType}-{Brand}.png` (e.g., `Iron7-Mireo.png`) — already follows this
- Club full: `{ClubType}-{Brand}.png` — already follows this
- Rarity backgrounds: `{RarityName}.png` — already follows this
- Stat icons: `Icon{StatName}.png`
- UI elements: `{ScreenName}_{ElementName}.png`

---

### Task 3: File Tree Cleanup

**Scripts to archive** (move to `Assets/Scripts/Editor/Archive/`, comment out MenuItems):
- `Assets/Scripts/UI/Inventory/Editor/ClubInventoryPatcher.cs` — all one-time patches
- `Assets/Scripts/UI/Inventory/Editor/FilterBarPatcher.cs` — one-time fix
- `Assets/Scripts/UI/Editor/MenuItemRemover.cs` — no longer needed
- `Assets/Scripts/UI/Inventory/Editor/ClubDetailPanelBuilder.cs` — one-time builder (Phase C already built)

**Check if `Assets/Scripts/UI/ExampleAutoWireScreen.cs` is still used.** If it's just an example/template, archive it.

**Check `Assets/Scripts/UI/Editor/LocalizationEditorHelper.cs`** — does it have MenuItems? If so, categorize under GOLFIN/Utilities/.

---

### Reminders
- Only change MenuItem path strings — don't rename .cs files (Unity tracks by GUID, but renaming adds confusion)
- Test that the menu still works after changes (MenuItem paths must be unique)
- Comment out MenuItems on archived files (prefix with `// [MenuItem - archived]`)
- Push to GitHub after completing

---

## Completed Tasks

✅ DONE: 2026-03-20 — ScreenshotTool, compress script, CLAUDE.md update
✅ DONE: 2026-03-20 — Phase C code: ClubCarouselController, ClubDetailPanel, builders, auto-wire
✅ DONE: 2026-03-21 — New leveling economy: rarity-based starting/max levels
✅ DONE: 2026-03-23 — TextGradients, visual fixes, filter dividers, arrows, viewport, fade, level text
✅ DONE: 2026-03-25 — Club Compare Phase D: ClubCompareController, builder, auto-wire, stat differences
✅ DONE: 2026-03-24 — Project cleanup: GOLFIN menu reorganized, Art/References folders renamed PascalCase, 5 editor scripts archived
