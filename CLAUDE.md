# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Session Startup (EVERY SESSION)

Before doing anything else:
1. Run: `powershell -File Docs/generate_audit.ps1 > Docs/ARCHITECTURE_AUDIT.md`
2. Read `Docs/AI_CONTEXT.md` (current phase, status, blockers)
3. Read `Docs/ARCHITECTURE_AUDIT.md` (just generated — file tree, singletons, events, health check)
4. Read any `Docs/PHASE_*_SPEC.md` files (active task specs from architect)
5. Read `tasks/lessons.md` for relevant project lessons
6. Read Docs/TellCode.md for any pending instructions from architect

## Session End (EVERY SESSION)

Before closing:
1. Update `Docs/AI_CONTEXT.md` with:
   - What was completed this session
   - Current phase status (checkboxes)
   - Any new issues or blockers discovered
   - What's next
2. Update `tasks/lessons.md` if any corrections were made
3. Commit with descriptive message

## Debugging Unity

### Reading Unity Console without copy-paste
Unity Editor logs to a fixed file on Windows. Read it directly:
```powershell
# Last 100 lines (quick check)
Get-Content -Path "$env:LOCALAPPDATA\Unity\Editor\Editor.log" -Tail 100

# Filter for errors only
Get-Content -Path "$env:LOCALAPPDATA\Unity\Editor\Editor.log" -Tail 500 | Select-String "Error|Exception|NullReference"

# Filter for game logs only
Get-Content -Path "$env:LOCALAPPDATA\Unity\Editor\Editor.log" -Tail 500 | Select-String "\[CharacterManager\]|\[CarouselController\]|\[ScreenManager\]|\[RosterScreenController\]|\[LevelUpModal\]|\[CompareController\]"

# Watch live (keep running while testing in Unity)
Get-Content -Path "$env:LOCALAPPDATA\Unity\Editor\Editor.log" -Wait -Tail 10
```

Note: Log resets each time Unity Editor starts. Contains a lot of noise from asset imports and compilation — always filter.

### Screenshots for visual review
Take a screenshot of the Game View for Claude (architect) to compare against references:
- In Unity Play mode, navigate to the screen you want to capture
- Menu: **GOLFIN > Screenshot > Capture Game View**
- Screenshot saves to `Assets/Screenshots/screenshot_YYYY-MM-DD_HH-mm-ss.png`
- Claude (architect) reads it directly via filesystem access at `C:\Users\cesar\GolfinRedux`
- Reference images are in `Assets/References/` with `_compressed` subfolders for comparison
- Screenshots and references must be compressed (max 800px wide) for Claude to read them:
  ```powershell
  pip install Pillow  # first time only
  powershell -File Docs/compress_screenshots.ps1 "Assets/Screenshots"
  ```

Workflow:
1. Claude Code builds/changes UI
2. Navigate to the screen in Play mode
3. Run GOLFIN > Screenshot > Capture Game View
4. Compress: `powershell -File Docs/compress_screenshots.ps1 "Assets/Screenshots"`
5. Claude reads `Assets/Screenshots/_compressed/` and compares against references

### TellCode.md workflow
- Claude (architect) writes instructions to `Docs/TellCode.md`
- Claude Code reads this file at the start of each task
- After completing, add a status line at the bottom of the file

---

## Basic Rules

### 0. Pre-Commit Code Verification (MANDATORY)
**Before committing ANY C# file, verify it will compile. This is not optional.**

For EVERY new or modified .cs file, check these before saving:

1. **Using directives:** Read the top of the file you're editing. Does every type you reference have a corresponding `using` statement? Common ones missed:
   - `CharacterRarity` → needs `using Golfin.Roster;`
   - `ClubType`, `ClubDataRuntime`, `PlayerClubData` → needs `using Golfin.Inventory;`
   - `TextMeshProUGUI` → needs `using TMPro;`
   - `Image`, `Button`, `ScrollRect` → needs `using UnityEngine.UI;`
   - `Keyboard`, `Key` → needs `using UnityEngine.InputSystem;`
   - `DOTween` → needs `using DG.Tweening;`
   - `List<>`, `Dictionary<>` → needs `using System.Collections.Generic;`
   - `Action`, `Func` → needs `using System;`
   - `IEnumerator` → needs `using System.Collections;`

2. **Namespace consistency:** If the file is in `Golfin.Inventory` namespace, and it references a type from `Golfin.Roster`, it MUST have `using Golfin.Roster;`. Cross-namespace references are the #1 source of compile errors.

3. **Method signatures:** When calling a method on another class, READ that class first to verify the method exists with the expected name and parameters. Don't guess.

4. **Null safety:** Use `== null` not `??` for Unity objects (see lessons.md).

5. **After writing a file, scan it once more for red flags:**
   - Any type name you're not 100% sure about → grep the codebase for it
   - Any method call on a singleton → verify the method exists on that singleton
   - Any event subscription → verify the event exists with the correct delegate signature

**If in doubt, READ THE FILE you're referencing before writing code that depends on it.**

### 1. Plan Mode Default
- Enter plan mode for ANY non-trivial task (3+ steps or architectural decisions)
- If something goes sideways, STOP and re-plan immediately — don't keep pushing
- Use plan mode for verification steps, not just building
- Write detailed specs upfront to reduce ambiguity

### 2. Subagent Strategy
- Use subagents liberally to keep main context window clean
- Offload research, exploration, and parallel analysis to subagents
- For complex problems, throw more compute at it via subagents
- One task per subagent for focused execution

### 3. Self-Improvement Loop
- After ANY correction from the user: update `tasks/lessons.md` with the pattern
- Write rules for yourself that prevent the same mistake
- Ruthlessly iterate on these lessons until mistake rate drops
- Review lessons at session start

### 4. Verification Before Done
- Never mark a task complete without proving it works
- Diff behavior between main and your changes when relevant
- Ask yourself: "Would a staff engineer approve this?"
- Run tests, check logs, demonstrate correctness

### 5. Demand Elegance (Balanced)
- For non-trivial changes: pause and ask "is there a more elegant way?"
- If a fix feels hacky: "Knowing everything I know now, implement the elegant solution"
- Skip this for simple, obvious fixes — don't over-engineer

### 6. Autonomous Bug Fixing
- When given a bug report: just fix it. Don't ask for hand-holding
- Point at logs, errors, failing tests — then resolve them
- Zero context switching required from the user

### Task Management
- Plan First: Write plan to `tasks/todo.md` with checkable items
- Verify Plan: Check in before starting implementation
- Track Progress: Mark items complete as you go
- Explain Changes: High-level summary at each step
- Document Results: Add review section to `tasks/todo.md`
- Capture Lessons: Update `tasks/lessons.md` after corrections

### Core Principles
- **Simplicity First:** Make every change as simple as possible. Impact minimal code.
- **No Laziness:** Find root causes. No temporary fixes. Senior developer standards.
- **Don't Duplicate:** Use existing utilities (RarityHelper, RarityStatCaps, ModalController) — never rewrite what exists.
- **Don't Rebuild Hierarchies:** If UI is already built in Unity, bind data to it. Don't recreate.

---

## Architect Handoff Workflow

Claude (claude.ai) acts as architect and produces spec files in `Docs/`. When specs exist:
1. Read the spec carefully before coding
2. The `_API_CORRECTIONS.md` file (if present) overrides the main spec where they conflict
3. Flag method names with `// NOTE:` if the spec's assumed API doesn't match actual code
4. After implementing a spec, move it to `Docs/archive/` and update `AI_CONTEXT.md`

---

## Project Overview

**Golfin Redux** is a golf-themed mobile game built in Unity (C#). The current focus is on the character roster management system — players collect characters, level them up by spending Reward Points, and allocate Skill Points (SP) across four stats.

## Build & Development

This is a Unity project — there are no custom CLI build commands. Development workflow:
- Open in Unity Editor via `GolfinRedux.sln` or by opening the project folder in Unity Hub
- Main scene: `Assets/Scenes/ShellScene.unity` (all UI screens live here)
- Gameplay scene: `Assets/Scenes/GameplayScene.unity`
- Editor tools for building UI hierarchies are in `Assets/Scripts/UI/Roster/Editor/`

## Architecture

### Screen Navigation Flow
```
Logo → Splash → Loading → Home (Hub)
                            ├→ Roster (character management)
                            ├→ Inventory (clubs, bags, balls, items)
                            ├→ Settings (modal overlay)
                            ├→ Gacha (not yet implemented)
                            └→ Gameplay (not yet implemented)
```

`ScreenManager` controls transitions with fade animations via `FadeController`. `PersistentUIManager` handles the top bar and bottom nav bar, showing them only on Home, Roster, and Inventory screens.

### Core Singletons
| Class | Location | Purpose |
|---|---|---|
| `CharacterManager` | `Assets/Scripts/CharacterManager.cs` | Central hub — roster, selection, level-up, stat allocation |
| `ClubManager` | `Assets/Scripts/ClubManager.cs` | Club ownership, equip/unequip, bag management |
| `RewardPointsManager` | `Assets/Scripts/UI/Roster/Managers/RewardPointsManager.cs` | R-point currency, persisted via PlayerPrefs |
| `CharacterDatabaseCSV` | `Assets/Scripts/UI/Roster/Managers/CharacterDatabaseCSV.cs` | Runtime CSV character loader (preferred over ScriptableObjects) |
| `ClubDatabaseCSV` | `Assets/Scripts/UI/Inventory/ClubDatabaseCSV.cs` | Runtime CSV club loader |
| `CharacterLevelUpDatabase` | `Assets/Scripts/UI/Roster/Data/CharacterLevelUpDatabase.cs` | Level economy CSV lookup |
| `AudioManager` | `Assets/Scripts/Audio/AudioManager.cs` | Music/SFX playback |
| `ScreenManager` | `Assets/Scripts/UI/ScreenManager.cs` | Screen activation/deactivation with fade transitions |
| `PersistentUIManager` | `Assets/Scripts/UI/PersistentUIManager.cs` | Top/bottom nav bars visibility |
| `FadeController` | `Assets/Scripts/UI/FadeController.cs` | Screen fade transitions |

### Namespaces
| Namespace | Contents |
|---|---|
| `Golfin.Roster` | CharacterData, PlayerCharacterData, RarityHelper, RarityStatCaps, CharacterRarity, all roster UI scripts |
| `Golfin.Inventory` | ClubData, ClubDataRuntime, PlayerClubData, ClubType, ClubDatabaseCSV, all inventory UI scripts |
| (global) | CharacterManager, ClubManager, LocalizationManager, ScreenManager, PersistentUIManager |

### Character System

**Two-layer data model:**
- **`CharacterData`** (ScriptableObject) — base template: stats, portraits (`portraitThumbnail`, `portraitFull`), rarity, identity, localization keys
- **`PlayerCharacterData`** (plain C#) — player instance: level, SP earned/spent, pending SP allocation, selection state, stamina energy

**CSV-first architecture:** `CharacterDatabaseCSV` loads character data from `Assets/Data/Characters.csv` at runtime. `CharacterManager` tries CSV first, falls back to ScriptableObject database.

**Four stats:** Strength, Club Control, Recovery, Stamina
**Stat caps:** Rarity-based, defined in `RarityStatCaps.cs` (Common 25 → Supreme 50)
**Six rarities:** Common, Uncommon, Rare, Mythic, Legendary, Supreme
**Starting levels by rarity:** Common 10, Uncommon 40, Rare 80, Mythic 120, Legendary 160, Supreme 200
**Max levels by rarity:** Common 39, Uncommon 79, Rare 119, Mythic 159, Legendary 199, Supreme 239

**SP allocation** uses Strategy pattern: `ManualSPAllocation` (player-controlled) or `AutomaticStatAllocation`, both implementing `StatAllocationStrategy`.

**Level-up economy** is CSV-driven: `Assets/Data/LevelUpCosts.csv` — 240 levels, cost = level × 5 RP, SP reward = 1 per level. Shared between characters and clubs.

**Existing utilities (USE THESE, don't duplicate):**
- `RarityHelper.GetRarityColor(rarity)` — standard rarity colors
- `RarityHelper.GetRarityLabel(rarity)` — single letter labels (C/U/R/M/L/S)
- `RarityHelper.GetRarityBadgeTextColor(rarity)` — card badge text colors
- `RarityStatCaps.GetCap(rarity, statName)` — stat maximums
- `ModalController` — base class for modal dialogs (fade, backdrop, show/hide)

### Club System

**Two-layer data model (mirrors character system):**
- **`ClubDataRuntime`** — template from Clubs.csv: stats, sprites, rarity, type, brand
- **`PlayerClubData`** — player instance: level, durability, equip slot

**Six club stats:** Power, Accuracy, Lie Resistance, Loft, Durability (consumable), Distance (derived, no bar)
**Club types:** Driver, Wood, Iron, A.Wedge, P.Wedge, S.Wedge, Putter
**Same rarity/level system as characters**

### Roster UI Hierarchy (Unity)
```
Canvas > ScreensRoot > RosterScreen
├── CarouselSection
│   ├── LeftArrow / RightArrow
│   ├── ScrollView → Viewport → PaginationDots
│   └── DetailPanel
│       ├── LeftPanel → Character (full-body Image)
│       └── RightPanel
│           ├── CharacterNamePanel → CharacterNameText (single TMP, use \n for first/last)
│           ├── RarityPanel → RarityRow (3 TMPs: rarity label, current lv, /max lv)
│           ├── CharacterStatsPanel
│           │   ├── CharacterStats1 (StatIcon + Name+Bar/StatsName/Bar + StatNumber)
│           │   ├── CharacterStats2 (same structure)
│           │   ├── CharacterStats3 (same structure)
│           │   └── CharacterStats4 (same structure)
│           ├── ButtonsPanel → LevelUpButton / BoostButton
│           ├── BioPanel → BioHeader / BioText
│           ├── CompareButton
│           └── SelectButton → Text (TMP) / Rim
```

**Stat row binding (Transform.Find paths):**
- `statRow.transform.Find("Name+Bar/Bar")` → `Image.fillAmount`
- `statRow.transform.Find("StatNumber")` → TMP text `"{current}/{cap}"`

**Stat bar colors:**
- Blue — normal
- Green — stat equals rarity cap (maxed)
- Red — stamina bar only, when `currentStaminaEnergy` is low (runtime energy, NOT the stat value)
- Orange — Level Up modal only, pending SP allocation preview

### Roster UI Scripts
| Script | Purpose |
|---|---|
| `RosterScreenController` | Top-level roster screen, displays RP, subscribes to manager events |
| `CarouselController` | Horizontal card carousel, pagination, fires `OnCharacterSelected` |
| `CharacterThumbnailCard` | Individual carousel card (portrait, rarity badge, level) |
| `CharacterDetailPanel` | Full character detail view (portrait, stats, buttons, bio) |
| `StatBar` | Reusable stat visualization (icon, label, fill bar, value text) |

### Events
| Publisher | Event | Subscribers |
|---|---|---|
| `CharacterManager` | `OnCharacterLeveledUp(string)` | RosterScreenController, CharacterDetailPanel |
| `CharacterManager` | `OnCharacterSelected(string)` | RosterScreenController, CharacterDetailPanel |
| `CharacterManager` | `OnRosterChanged()` | RosterScreenController, CarouselController |
| `RewardPointsManager` | `OnPointsChanged(int)` | RosterScreenController, HomeScreenController |
| `CarouselController` | `OnCharacterSelected(string)` | CharacterDetailPanel |
| `ClubManager` | `OnClubEquipped(string)` | ClubDetailPanel |
| `ClubManager` | `OnClubLeveledUp(string)` | ClubDetailPanel |
| `ClubManager` | `OnInventoryChanged()` | ClubCarouselController |

### Data Files
| File | Purpose |
|---|---|
| `Assets/Data/Characters.csv` | Character data (CSV-first, loaded by CharacterDatabaseCSV) |
| `Assets/Data/Clubs.csv` | Club data (CSV-first, loaded by ClubDatabaseCSV) |
| `Assets/Data/CharacterDatabase.asset` | ScriptableObject character templates (fallback) |
| `Assets/Data/LevelUpCosts.csv` | Level economy: 240 levels, cost = level × 5, SP = 1 (shared) |
| `Assets/Data/HoleDatabase.csv` | Hole definitions |
| `Assets/Data/HoleDatabase.asset` | ScriptableObject hole collection |

### Localization
`LocalizationManager` loads CSV files from `Assets/Localization/`. Key prefixes: `HOLE_*`, `CHAR_*`, `ROSTER_*`, `HOME_*`, `CLUB_*`, `MODAL_*`, `COMPARE_*`. Currently supports English and Japanese.

### Key Patterns
- **Events:** C# `System.Action` delegates, subscribe in `OnEnable`, unsubscribe in `OnDisable`
- **Namespaces:** `Golfin.Roster` for roster, `Golfin.Inventory` for clubs
- **Modals:** Extend `ModalController` base class
- **Sprites:** Load via `Resources.Load<Sprite>()`, NOT Inspector arrays
- **Prefab Builder Tools:** Editor scripts in `Assets/Scripts/UI/Roster/Editor/` and `Assets/Scripts/UI/Inventory/Editor/`
- **UIAutoWire:** `Assets/Scripts/Utilities/UIAutoWire.cs` for component auto-discovery
- **Unity null checks:** Always `== null` not `??` (see lessons.md)
- **Input system:** Always `UnityEngine.InputSystem`, never `UnityEngine.Input`
- **Platform:** Windows (PowerShell, no bash/chmod/sed)

---

## Conventions

### Localization
- All **new** user-facing text should use localization keys from the start: `LocalizationManager.Get("KEY")`
- Use the pattern `SCREEN_ELEMENT` (e.g., `ROSTER_LEVEL_UP`, `HOME_PLAY_BUTTON`, `MODAL_CONFIRM`)
- Add both EN and JP entries to the localization CSV when creating new text
- Legacy hardcoded text will be migrated in a dedicated localization pass (not yet scheduled)
- Rich text tags like `<color=#EEDC9A>` are supported in localization values — TMP handles them natively

---

## Development Docs

| File | Purpose |
|---|---|
| `Docs/AI_CONTEXT.md` | Living project status — current phase, blockers, next steps |
| `Docs/TellCode.md` | Architect instructions for Claude Code |
| `Docs/ARCHITECTURE_AUDIT.md` | Auto-generated — file tree, singletons, events, health check |
| `Docs/generate_audit.ps1` | Script to regenerate the audit |
| `Docs/CLUB_INVENTORY_SPEC.md` | Active club inventory spec |
| `Docs/Game Design/GAME_DESIGN_CHANGELOG.md` | Game design changes from original GDD |
| `Docs/GAME_DESIGN_AGENT.md` | AI agent for evaluating GDD systems |
| `Docs/Archive/` | Completed phase specs |
| `tasks/lessons.md` | Accumulated corrections and patterns |