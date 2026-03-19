# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Session Startup (EVERY SESSION)

Before doing anything else:
1. Run: `powershell -File Docs/generate_audit.ps1 > Docs/ARCHITECTURE_AUDIT.md`
2. Read `Docs/AI_CONTEXT.md` (current phase, status, blockers)
3. Read `Docs/ARCHITECTURE_AUDIT.md` (just generated — file tree, singletons, events, health check)
4. Read any `Docs/PHASE_*_SPEC.md` files (active task specs from architect)
5. Read `tasks/lessons.md` for relevant project lessons

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

---

## Basic Rules

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
                            ├→ Settings (modal overlay)
                            ├→ Gacha (not yet implemented)
                            └→ Gameplay (not yet implemented)
```

`ScreenManager` controls transitions with fade animations via `FadeController`. `PersistentUIManager` handles the top bar and bottom nav bar, showing them only on Home and Roster screens.

### Core Singletons
| Class | Location | Purpose |
|---|---|---|
| `CharacterManager` | `Assets/Scripts/CharacterManager.cs` | Central hub — roster, selection, level-up, stat allocation |
| `RewardPointsManager` | `Assets/Scripts/UI/Roster/Managers/RewardPointsManager.cs` | R-point currency, persisted via PlayerPrefs |
| `CharacterDatabaseCSV` | `Assets/Scripts/UI/Roster/Managers/CharacterDatabaseCSV.cs` | Runtime CSV character loader (preferred over ScriptableObjects) |
| `CharacterLevelUpDatabase` | `Assets/Scripts/UI/Roster/Data/CharacterLevelUpDatabase.cs` | Level economy CSV lookup |
| `AudioManager` | `Assets/Scripts/Audio/AudioManager.cs` | Music/SFX playback |
| `ScreenManager` | `Assets/Scripts/UI/ScreenManager.cs` | Screen activation/deactivation with fade transitions |
| `PersistentUIManager` | `Assets/Scripts/UI/PersistentUIManager.cs` | Top/bottom nav bars visibility |
| `FadeController` | `Assets/Scripts/UI/FadeController.cs` | Screen fade transitions |

### Character System

**Two-layer data model:**
- **`CharacterData`** (ScriptableObject) — base template: stats, portraits (`portraitThumbnail`, `portraitFull`), rarity, identity, localization keys
- **`PlayerCharacterData`** (plain C#) — player instance: level (1–199), SP earned/spent, pending SP allocation, selection state, stamina energy

**CSV-first architecture:** `CharacterDatabaseCSV` loads character data from `Assets/Data/Characters.csv` at runtime. `CharacterManager` tries CSV first, falls back to ScriptableObject database.

**Four stats:** Strength, Club Control, Recovery, Stamina  
**Stat caps:** Rarity-based, defined in `RarityStatCaps.cs` (Common 25 → Supreme 50)  
**Six rarities:** Common, Uncommon, Rare, Mythic, Legendary, Supreme  

**SP allocation** uses Strategy pattern: `ManualSPAllocation` (player-controlled) or `AutomaticStatAllocation`, both implementing `StatAllocationStrategy`.

**Level-up economy** is CSV-driven: `Assets/Data/LevelUpCosts.csv` — RP cost and SP reward per level for all 199 levels.

**Existing utilities (USE THESE, don't duplicate):**
- `RarityHelper.GetRarityColor(rarity)` — standard rarity colors
- `RarityHelper.GetRarityLabel(rarity)` — single letter labels (C/U/R/M/L/S)
- `RarityHelper.GetRarityBadgeTextColor(rarity)` — card badge text colors
- `RarityStatCaps.GetCap(rarity, statName)` — stat maximums
- `ModalController` — base class for modal dialogs (fade, backdrop, show/hide)

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

### Data Files
| File | Purpose |
|---|---|
| `Assets/Data/Characters.csv` | Character data (CSV-first, loaded by CharacterDatabaseCSV) |
| `Assets/Data/CharacterDatabase.asset` | ScriptableObject character templates (fallback) |
| `Assets/Data/LevelUpCosts.csv` | Level economy: `costs_r`, `sp_reward` for 199 levels |
| `Assets/Data/HoleDatabase.csv` | Hole definitions |
| `Assets/Data/HoleDatabase.asset` | ScriptableObject hole collection |

### Localization
`LocalizationManager` loads CSV files from `Assets/Localization/`. Key prefixes: `HOLE_*`, `CHAR_*`, `ROSTER_*`, `HOME_*`. Currently supports English and Japanese.

### Key Patterns
- **Events:** C# `System.Action` delegates, subscribe in `OnEnable`, unsubscribe in `OnDisable`
- **Namespace:** `Golfin.Roster` for all roster/character scripts
- **Modals:** Extend `ModalController` base class
- **Prefab Builder Tools:** Editor scripts in `Assets/Scripts/UI/Roster/Editor/`
- **UIAutoWire:** `Assets/Scripts/Utilities/UIAutoWire.cs` for component auto-discovery

---

## Conventions

### Localization
- All **new** user-facing text should use localization keys from the start: `LocalizationManager.GetText("KEY")`
- Use the pattern `SCREEN_ELEMENT` (e.g., `ROSTER_LEVEL_UP`, `HOME_PLAY_BUTTON`, `MODAL_CONFIRM`)
- Add both EN and JP entries to the localization CSV when creating new text
- Legacy hardcoded text will be migrated in a dedicated localization pass (not yet scheduled)
- Rich text tags like `<color=#EEDC9A>` are supported in localization values — TMP handles them natively

---

## Development Docs

| File | Purpose |
|---|---|
| `Docs/AI_CONTEXT.md` | Living project status — current phase, blockers, next steps |
| `Docs/ARCHITECTURE_AUDIT.md` | Auto-generated — file tree, singletons, events, health check |
| `Docs/generate_audit.sh` | Script to regenerate the audit |
| `Docs/PHASE_*_SPEC.md` | Active implementation specs from architect |
| `Docs/ROSTER_DEVELOPMENT_PLAN_UPDATED.md` | 7-phase roadmap |
| `tasks/todo.md` | Current task checklist |
| `tasks/lessons.md` | Accumulated corrections and patterns |