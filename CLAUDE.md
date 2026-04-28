# [CLAUDE.md](http://CLAUDE.md)

This file provides guidance to Claude Code ([claude.ai/code](http://claude.ai/code)) when working with code in this repository.

> **‼️ HOW TO END EVERY RESPONSE — READ THIS BEFORE ANYTHING ELSE**
>
> The last line of every response must be the file-summary table (or, if no files were touched, the most concrete next step). **Do not append any closer, sign-off, farewell, well-wish, callback, or recurring catchphrase after it.** This explicitly forbids the phrase "See you space cowboy" and every variant of it (no "space cowboy", no "Bebop", no "see you", no goodbye in any language). Cesar will say goodbye when he's done; until then, the response ends on the work.
>
> If you find yourself about to type a closing line that isn't the file table or a next-step, **delete it before sending**. This rule overrides any pattern from past sessions, jsonl history, or older `lessons.md` entries. It is non-negotiable.

## Multi-Agent Workflow (NEW 2026-04-28)

UI tasks go through an automated pipeline of three subagents. Cesar's only job is to kick off and approve at the very end. **Do not invent your own workflow when this one applies.**

For SMALL tasks where the full pipeline is overkill (bug fixes with obvious solutions, single-line tweaks, CSV field additions), use the lightweight workflow at `Docs/Specs/Quick/` instead — see `Docs/Specs/Quick/README.md`. Quick tasks skip the subagent chain entirely; Cesar eyeballs the result.

### The pipeline

```
Cesar -> golfin-architect (writes spec)
      -> golfin-implementer (builds + screenshots + self-PASS/FAIL checklist)
      -> golfin-self-reviewer (catches false PASSes; routes back or forward)
      -> golfin-architect (final review: visual fidelity + cross-cutting)
      -> Cesar (final approval -> DONE)
```

### Where things live

- **Subagent definitions:** `.claude/agents/golfin-architect.md`, `golfin-implementer.md`, `golfin-self-reviewer.md`
- **Hooks:** `.claude/hooks/route_subagent.py` (state router + desktop notify + email + alerts.log), `enforce_implementer_done.py` (PreToolUse blocker), `capture_screenshot.py` (Implementer's screenshot helper)
- **Notification config:** `.claude/notify_config.json` (toast always on; email opt-in)
- **Per-task folder:** `Docs/Specs/Active/<task_slug>/` containing `SPEC.md`, `STATUS.md`, `IMPLEMENTER_REPORT.md`, `SELF_REVIEW.md`, `ARCHITECT_REVIEW.md`, `CESAR_REJECTION.md` (when applicable), `HEARTBEAT.log`, `screenshots/`
- **Template:** `Docs/Specs/Active/_TEMPLATE/` (copy this to start a new task)
- **Quick tasks:** `Docs/Specs/Quick/` (lightweight, no subagent chain)

### STATUS.md states

```
SPEC_READY -> IMPLEMENTER_WORKING -> READY_FOR_SELF_REVIEW
            -> (SELF_REVIEW_PASS | SELF_REVIEW_FAIL | READY_FOR_ARCHITECT_REVIEW)
            -> (ARCHITECT_REVIEW_PASS | ARCHITECT_REVIEW_FAIL | ARCHITECT_REVIEW_ESCALATE)
            -> (CESAR_REJECTED loops back) | (DONE finishes)

IMPLEMENTER_BLOCKED  - implementer hit a circuit breaker; Cesar must unblock
CESAR_REJECTED       - Cesar manually rejected after architect-pass; loop back to implementer
```

The `route_subagent.py` hook prints the next step in the terminal automatically after every subagent run, so neither you nor Cesar needs to check a log file. When STATUS reaches a state that needs Cesar (`ARCHITECT_REVIEW_PASS`, `*_ESCALATE`, `IMPLEMENTER_BLOCKED`), notifications fire via Windows toast + email (if configured) + always-logged at `.claude/alerts.log`.

### Hard rules (these are enforced by hooks, not just convention)

1. **Implementer cannot mark itself done.** The `enforce_implementer_done.py` hook blocks any STATUS write to `READY_FOR_SELF_REVIEW` or `READY_FOR_ARCHITECT_REVIEW` unless `IMPLEMENTER_REPORT.md` has every checklist item filled with PASS/FAIL + non-trivial justification + a real screenshot path that points to an actual file. No placeholder text allowed. FAIL items also block the SELF_REVIEW transition (must use ARCHITECT_REVIEW path).
2. **STATUS is authoritative.** Do NOT "correct" STATUS based on review file contents. If STATUS contradicts a review verdict, Cesar may have rejected manually — check for `CESAR_REJECTION.md`. If still uncertain, set STATUS to `IMPLEMENTER_BLOCKED` and ask.
3. **Implementer cannot write SELF_REVIEW.md or ARCHITECT_REVIEW.md.** Those are written by the other subagents.
4. **Self-reviewer cannot modify scenes or write code.** It's a vision-heavy reviewer only; tools are scoped to Read/Write/Edit + Figma MCP.
5. **Architect cannot modify scenes or write Unity code either.** Same scoping; the architect reviews and writes specs/reviews.
6. **`STATUS.md = DONE` only after Cesar's manual approval.** No subagent writes DONE. Cesar moves the folder to `Docs/Specs/Completed/` when satisfied.
7. **No white-box placeholders.** If `[SerializeField]` references aren't wired, wire them BEFORE marking IMPLEMENTER_REPORT done. Use `_default*` slots specified in the spec for fallback sprites.
8. **Wait before screenshot.** After entering play mode, wait at least 3 seconds (5 if data-binding is involved) before capturing. Unity needs time to render the first few frames and run all OnEnable code.
9. **Append to HEARTBEAT.log** every ~5 minutes of work. Stale heartbeat (>15min) triggers a stuck-session alert to Cesar.
10. **Circuit breakers** — if the same Unity MCP tool fails 3 times, or you wait on Unity for >3 minutes with no progress, or you can't find an asset after 2 attempts: set STATUS to `IMPLEMENTER_BLOCKED` and stop. Don't loop indefinitely.

### How to start a new UI task (Cesar)

For a complex UI task: in Claude Code, say: `Use the golfin-architect subagent to write a spec for <task description>`. The architect will:

1. Confirm the Figma page/frame/placeholder-vs-canonical with you (per Blueprint §8 standing rule).
2. Create `Docs/Specs/Active/<task_slug>/` from the template.
3. Fill `SPEC.md`.
4. Set `STATUS.md` to `SPEC_READY`.

The SubagentStop hook will then print: `[<task_slug>] STATUS=SPEC_READY -> Use the golfin-implementer subagent on "<task_slug>"`. You paste that command and the pipeline runs itself.

For a small task: just say `Read Docs/Specs/Quick/<task_slug>.md and implement.` after writing the quick spec.

### How to redo a failed iteration

If the architect or self-reviewer kicks the task back, STATUS goes to `*_FAIL` and the hook prints `Use the golfin-implementer subagent on "<task_slug>"`. The Implementer reads the latest review file, addresses the fail list, and re-submits.

If YOU manually reject after architect-pass: write `CESAR_REJECTION.md` in the task folder explaining why, then set STATUS to `CESAR_REJECTED`. The hook will route the implementer to redo with your notes.

### When to escalate to claude.ai (Architect Claude in this chat)

The Claude.ai chat (Opus 4.7, full repo access via filesystem MCP) is for:
- Project-wide reasoning that doesn't fit one task (e.g., "should we restructure asmdefs?").
- Ambiguous escalations where the architect-subagent writes `ARCHITECT_REVIEW_ESCALATE`.
- Authoring a new spec for a task that affects multiple subsystems.
- Workflow / pipeline improvements.

For a single task in flight, prefer the subagent chain. Only ping Cesar's claude.ai chat when STATUS reaches `ARCHITECT_REVIEW_ESCALATE` or `IMPLEMENTER_BLOCKED`.

### Migration from old TellCode.md workflow

`Docs/TellCode.md` is the legacy handoff file. New tasks use the per-task folder convention above (or Quick for small ones). TellCode is being phased out; do not write new active tasks there. The completion log at the bottom of TellCode is preserved for historical reference.

---

## Session Startup (EVERY SESSION)

Before doing anything else:

1. Generate the architecture audit (use the variant for your platform):
   - **Windows:** `powershell -File Docs/Scripts/generate_audit.ps1 > Docs/Architecture/ARCHITECTURE_AUDIT.md`
   - **macOS / Linux:** `bash Docs/Scripts/generate_audit.sh > Docs/Architecture/ARCHITECTURE_AUDIT.md`
2. Read `Docs/AI_CONTEXT.md` (tiny — current status and active work)
3. Read `Docs/Tasks.md` (current checklist — what to do)
4. Read `Docs/TellCode.md` for any pending architect instructions
5. If working on UI/design: read `Docs/Rules.md` (design constraints, Figma specs, conventions)
6. If working on UI: read `Docs/Architecture/UI_HIERARCHY.md` (scene UI paths) and `Docs/Architecture/PATTERNS.md` (recurring patterns)7. If needed: read `Docs/Architecture/ARCHITECTURE_AUDIT.md` (file tree, singletons, events)
8. Read `tasks/lessons.md` for relevant project lessons

## Session End (EVERY SESSION)

Before closing:
1. Update `Docs/AI_CONTEXT.md` with:
   - What was completed this session
   - Current phase status (checkboxes)
   - Any new issues or blockers discovered
   - What's next
2. Update `tasks/lessons.md` if any corrections were made
3. If UI hierarchy changed (new panels, modals, stat rows, buttons): update `Docs/Architecture/UI_HIERARCHY.md`
4. If new patterns emerged or existing ones changed: update `Docs/Architecture/PATTERNS.md`
5. Commit with descriptive message

## Debugging Unity

### Reading Unity Console without copy-paste
Unity Editor writes to a log file you can tail directly. Path differs by OS:

- **Windows:** `%LOCALAPPDATA%\Unity\Editor\Editor.log`
- **macOS:** `~/Library/Logs/Unity/Editor.log`
- **Linux:** `~/.config/unity3d/Editor.log`

**Windows (PowerShell):**
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

**macOS / Linux (bash):** (substitute the Linux path for `LOG` if applicable)
```bash
LOG=~/Library/Logs/Unity/Editor.log

# Last 100 lines
tail -n 100 "$LOG"

# Filter for errors only
tail -n 500 "$LOG" | grep -E "Error|Exception|NullReference"

# Filter for game logs only
tail -n 500 "$LOG" | grep -E "\[CharacterManager\]|\[CarouselController\]|\[ScreenManager\]|\[RosterScreenController\]|\[LevelUpModal\]|\[CompareController\]"

# Watch live
tail -f "$LOG"
```

Note: Log resets each time Unity Editor starts. Contains a lot of noise from asset imports and compilation — always filter.

### Screenshots for visual review
Take a screenshot of the Game View for Claude (architect) to compare against references:
- In Unity Play mode, navigate to the screen you want to capture
- Menu: **GOLFIN > Screenshot > Capture Game View**
- Screenshot saves to `Assets/Screenshots/screenshot_YYYY-MM-DD_HH-mm-ss.png`
- Claude (architect) reads it directly via filesystem access (the local clone of this repo, wherever it lives — e.g. `C:\Users\<you>\GolfinRedux` on Windows or `~/Documents/GolfinRedux` on Mac)
- Reference images are in `Assets/References/` with `_compressed` subfolders for comparison
- Screenshots and references must be compressed (max 800px wide) for Claude to read them. Use the cross-platform Python script:
  ```bash
  pip install Pillow  # first time only
  python Docs/Scripts/compress_screenshots.py Assets/Screenshots
  ```
  (Windows users may also still run the PowerShell wrapper: `powershell -File Docs/Scripts/compress_screenshots.ps1 "Assets/Screenshots"`.)

Workflow:
1. Claude Code builds/changes UI
2. Navigate to the screen in Play mode
3. Run GOLFIN > Screenshot > Capture Game View
4. Compress: `python Docs/Scripts/compress_screenshots.py Assets/Screenshots`
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
- **Platform:** Cross-platform team — contributors work on both Windows (PowerShell) and macOS (bash/zsh). Use the platform-appropriate variant of any helper script (`.ps1` on Windows, `.sh` / `.py` on Mac/Linux). Don't hardcode absolute paths or shell-specific syntax in shared docs or tooling.

---

## Conventions

### Asset & File Naming
Follow `Docs/Game Design/ASSET_NAMING_CONVENTION.md` for ALL new assets. Key rules:
- **Prefixes:** `S_` sprite, `BG_` background, `ICO_` icon, `T_` texture, `MESH_` 3D model
- **No spaces in filenames or folders** — use PascalCase or hyphens
- **Characters:** `S_Char_{Name}`, `S_CharFull_{Name}`, `S_CharHome_{Name}`
- **Clubs:** `S_Club_{Type}-{Brand}`, `S_ClubFull_{Type}-{Brand}`
- **UI elements:** `ICO_{Name}`, `S_Btn_{Name}_{State}`, `S_Rarity_{Name}`, `S_Rim_{Variant}`
- **Scripts:** `{System}Manager`, `{System}DatabaseCSV`, `{System}DetailPanel`, `{System}CompareController`
- **Unity hierarchy:** `{Screen}Screen`, `{Name}Panel`, `{Action}Button`, `{Name}Text`, `{Name}Row`
- **Localization keys:** `{SCREEN}_{ELEMENT}` (e.g., `CLUB_POWER`, `ROSTER_LEVEL_UP`)
- **CSV IDs:** `char_{name}`, `club_{type}_{brand}`
- **DO NOT rename files in Resources/** without updating the corresponding CSV values

### Localization
- All **new** user-facing text should use localization keys from the start: `LocalizationManager.Get("KEY")`
- Use the pattern `SCREEN_ELEMENT` (e.g., `ROSTER_LEVEL_UP`, `HOME_PLAY_BUTTON`, `MODAL_CONFIRM`)
- Add both EN and JP entries to the localization CSV when creating new text
- Legacy hardcoded text will be migrated in a dedicated localization pass (not yet scheduled)
- Rich text tags like `<color=#EEDC9A>` are supported in localization values — TMP handles them natively

---

### Asset & File Naming Convention
**Full reference:** `Docs/Game Design/ASSET_NAMING_CONVENTION.md` — READ THIS before creating any new assets.

Quick rules:
- **No spaces** in filenames or folder names — use PascalCase or hyphens
- **Prefixes:** `S_` sprite, `T_` texture, `MESH_` 3D model, `BG_` background, `ICO_` icon, `FX_` effect, `SFX_` sound, `MUS_` music
- **Characters:** `S_Char_{Name}`, `S_CharFull_{Name}`, `S_CharHome_{Name}`
- **Clubs:** `S_Club_{Type}-{Brand}`, `S_ClubFull_{Type}-{Brand}`
- **UI elements:** `ICO_{Name}`, `S_Btn_{Name}_{State}`, `S_Rarity_{Name}`, `S_Rim_{Variant}`
- **Scripts:** `{System}Manager`, `{System}DatabaseCSV`, `{System}DetailPanel`, `{System}CompareController`
- **Unity hierarchy:** `{Screen}Screen`, `{Name}Panel`, `{Action}Button`, `{Name}Text`, `{Name}Row`
- **Localization keys:** `{SCREEN}_{ELEMENT}` (e.g., `CLUB_POWER`, `ROSTER_LEVEL_UP`)
- **CSV IDs:** `char_{name}`, `club_{type}_{brand}`
- **DO NOT rename files in Resources/** without updating the corresponding CSV values

---

## Development Docs

| File | Purpose |
|---|---|
| `Docs/README.md` | Index map — what's where in Docs/ |
| `Docs/AI_CONTEXT.md` | Tiny core memory — current status, active work |
| `Docs/Tasks.md` | Current checklist and backlog |
| `Docs/Rules.md` | Design constraints, Figma specs, conventions |
| `Docs/TellCode.md` | Architect instructions for Claude Code |
| `Docs/Architecture/ARCHITECTURE_AUDIT.md` | Auto-generated — file tree, singletons, events |
| `Docs/Architecture/PATTERNS.md` | Recurring patterns across the codebase |
| `Docs/Architecture/UI_HIERARCHY.md` | Scene UI paths reference |
| `Docs/Architecture/INVENTORY_REFERENCE.md` | Inventory system patterns + APIs |
| `Docs/Scripts/generate_audit.ps1` / `Docs/Scripts/generate_audit.sh` | Script to regenerate the audit (PowerShell on Windows, bash on Mac/Linux) |
| `Docs/Scripts/compress_screenshots.py` / `Docs/Scripts/compress_screenshots.ps1` | Compress screenshots to ≤800px (Python is cross-platform; .ps1 is a Windows wrapper) |
| `Docs/Game Design/GAME_DESIGN_CHANGELOG.md` | Game design changes from original GDD |
| `Docs/Game Design/ASSET_NAMING_CONVENTION.md` | Asset & file naming rules |
| `Docs/Game Design/GAMEPLAY_FORMULAS_PROPOSAL.md` | Simplified gameplay formulas (proposal) |
| `Docs/Reference/GAME_DESIGN_AGENT.md` | AI agent for evaluating GDD systems |
| `Docs/Pipeline/` | Course-pipeline lessons + specs (ADD_HOLE, BUNKER_*, TEE_SKIRT, fringe meshes) |
| `Docs/Pipeline/LESSONS_FRINGE_BORDER_MESHES.md` | **READ before touching fairway/tee fringe/border code.** Hard-won lessons on submesh baking, dilated CDT, and the Lite vs Geo importer trap. |
| `Docs/Physics/` | Physics architecture, tuning targets, and post-mortem lessons |
| `Docs/Specs/` | Active / Queued / Completed specs |
| `Docs/Diagnostics/` | In-flight diagnostic outputs (CSVs, milestone done reports) |
| `Docs/Backups/` | Restore points for risky migrations |
| `Docs/Archive/` | Completed phase specs (historical) |
| `tasks/lessons.md` | Accumulated corrections and patterns |