# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Basic Rules
1. Plan Mode Default

Enter plan mode for ANY non-trivial task (3+ steps or architectural decisions)
If something goes sideways, STOP and re-plan immediately – don't keep pushing
Use plan mode for verification steps, not just building
Write detailed specs upfront to reduce ambiguity

2. Subagent Strategy

Use subagents liberally to keep main context window clean
Offload research, exploration, and parallel analysis to subagents
For complex problems, throw more compute at it via subagents
One task per subagent for focused execution

3. Self-Improvement Loop

After ANY correction from the user: update tasks/lessons.md with the pattern
Write rules for yourself that prevent the same mistake
Ruthlessly iterate on these lessons until mistake rate drops
Review lessons at session start for relevant project

4. Verification Before Done

Never mark a task complete without proving it works
Diff behavior between main and your changes when relevant
Ask yourself: "Would a staff engineer approve this?"
Run tests, check logs, demonstrate correctness

5. Demand Elegance (Balanced)

For non-trivial changes: pause and ask "is there a more elegant way?"
If a fix feels hacky: "Knowing everything I know now, implement the elegant solution"
Skip this for simple, obvious fixes – don't over-engineer
Challenge your own work before presenting it

6. Autonomous Bug Fixing

When given a bug report: just fix it. Don't ask for hand-holding
Point at logs, errors, failing tests – then resolve them
Zero context switching required from the user
Go fix failing CI tests without being told how

Task Management

Plan First: Write plan to tasks/todo.md with checkable items
Verify Plan: Check in before starting implementation
Track Progress: Mark items complete as you go
Explain Changes: High-level summary at each step
Document Results: Add review section to tasks/todo.md
Capture Lessons: Update tasks/lessons.md after corrections

Core Principles

Simplicity First: Make every change as simple as possible. Impact minimal code.
No Laziness: Find root causes. No temporary fixes. Senior developer standards.

## Project Overview

**Golfin Redux** is a golf-themed mobile game built in Unity (C#). The current focus is on the character roster management system — players collect characters, level them up by spending Reward Points, and allocate Skill Points (SP) across four stats.

## Build & Development

This is a Unity project — there are no custom CLI build commands. Development workflow:
- Open in Unity Editor via `Golfin Redux.sln` or by opening the project folder in Unity Hub
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
| `CharacterManager` | `Assets/Scripts/CharacterManager.cs` | Central hub for character operations — owns roster, selection, level-up logic |
| `RewardPointsManager` | `Assets/Scripts/UI/Roster/Managers/RewardPointsManager.cs` | In-game currency (R points), persisted via PlayerPrefs JSON |
| `ScreenManager` | `Assets/Scripts/UI/Core/ScreenManager.cs` | Screen activation/deactivation with fade transitions |
| `PersistentUIManager` | `Assets/Scripts/UI/Core/PersistentUIManager.cs` | Top/bottom nav bars visibility |

### Character System
Two-layer data model:
- **`CharacterData`** (ScriptableObject template) — base stats, portrait, rarity, identity. Loaded from `Assets/Data/CharacterDatabase.asset`.
- **`PlayerCharacterData`** (runtime instance) — player's owned copy with level (1–199), SP earned/spent, and current stat values.

Four stats: Strength, Club Control, Recovery, Stamina. Stat caps are rarity-based (Common max 25 → Supreme max 50) defined in `RarityStatCaps`.

SP allocation uses the Strategy pattern: `ManualSPAllocation` (player-controlled) or `AutomaticStatAllocation`, both implementing `StatAllocationStrategy`.

Level-up economy is CSV-driven: `Assets/Data/LevelUpCosts.csv` defines Reward Point cost and SP reward per level for all 199 levels.

### Roster UI
- `RosterScreenController` — top-level roster screen, subscribes to `CharacterManager` and `RewardPointsManager` events
- `CarouselController` — horizontal character card carousel with pagination, smooth scroll, selection events
- `CharacterThumbnailCard` — individual carousel card (portrait, rarity badge, level)
- `CharacterDetailPanel` — modal showing full portrait, stats, level-up button
- `StatBar` — reusable stat visualization component

### Data Files
| File | Purpose |
|---|---|
| `Assets/Data/CharacterDatabase.asset` | ScriptableObject collection of all `CharacterData` templates |
| `Assets/Data/HoleDatabase.asset` | ScriptableObject collection of holes (5 populated) |
| `Assets/Data/LevelUpCosts.csv` | Level economy (columns: `costs_r`, `sp_reward`) for 199 levels |
| `Assets/Data/Characters.csv` | Character import template for `CharacterDatabaseCSV` |
| `Assets/Data/HoleDatabase.csv` | Hole definitions for `HoleDatabaseLoader` |

### Localization
`LocalizationManager` loads CSV files from `Assets/Localization/`. Key prefixes: `HOLE_*`, `CHAR_*`, `ROSTER_*`, `HOME_*`. Currently supports English and Japanese.

### Key Patterns
- **Events**: C# `System.Action` delegates (e.g., `CharacterManager.OnCharacterLeveledUp`, `CarouselController.OnCharacterSelected`)
- **Modals**: `SettingsController` and `CharacterDetailPanel` extend `ModalController`
- **Prefab Builder Tools**: Editor scripts in `Assets/Scripts/UI/Roster/Editor/` auto-generate UI hierarchies (use `RosterSystemSetupTool` as the master tool)
- **UIAutoWire**: `Assets/Scripts/Utilities/UIAutoWire.cs` utility for component auto-discovery

## Development Docs

Detailed planning documents live in `Docs/`. Key files:
- `Docs/ROSTER_DEVELOPMENT_PLAN_UPDATED.md` — current 7-phase roadmap
- `Docs/AI_CONTEXT.md` — project context summary
- `Docs/ROSTER_PHASE_ANALYSIS_2026_03_06.md` — gap analysis for Phase 2a
