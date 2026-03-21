# TellCode.md — Instructions from Claude (Architect) to Claude Code

> Claude Code: Read this file at the start of each task. Execute the latest instruction block.
> After completing, add a status line at the bottom: `✅ DONE: [date] [brief summary]`
> Claude (Architect) will update this file with new instructions as needed.

---

## Current Task (2026-03-21) — New Leveling Economy + Phase C Wiring

### Priority 1: Adopt New Leveling Economy

The leveling CSVs and data files have been updated by the architect. The new system uses:
- **Universal cost curve:** `LevelUpCosts.csv` — 240 levels, cost = level × 5 RP, 1 SP per level
- **Rarity-based starting levels and max levels** (both characters and clubs):
  - Common: start 10, max 39
  - Uncommon: start 40, max 79
  - Rare: start 80, max 119
  - Mythic: start 120, max 159
  - Legendary: start 160, max 199
  - Supreme: start 200, max 239

**CSV files already updated:** `LevelUpCosts.csv`, `Characters.csv`, `Clubs.csv`

**Code changes needed:**

**1a. Update CharacterManager.cs — starting level by rarity**
In `LoadRoster()` or `InitializeCharacters()`, when creating `PlayerCharacterData` for each character, set `currentLevel` based on the character's rarity instead of hardcoding to 1:

```csharp
private int GetStartingLevel(CharacterRarity rarity) => rarity switch
{
    CharacterRarity.Common => 10,
    CharacterRarity.Uncommon => 40,
    CharacterRarity.Rare => 80,
    CharacterRarity.Mythic => 120,
    CharacterRarity.Legendary => 160,
    CharacterRarity.Supreme => 200,
    _ => 10
};
```

Use this when initializing player data: `currentLevel = GetStartingLevel(template.rarity)`

**1b. Update ClubManager.cs — starting level by rarity**
Same pattern. In `InitializeClubs()`, set `currentLevel` based on club rarity:

```csharp
private int GetStartingLevel(CharacterRarity rarity) => rarity switch
{
    CharacterRarity.Common => 10,
    CharacterRarity.Uncommon => 40,
    CharacterRarity.Rare => 80,
    CharacterRarity.Mythic => 120,
    CharacterRarity.Legendary => 160,
    CharacterRarity.Supreme => 200,
    _ => 10
};
```

Use: `currentLevel = GetStartingLevel(template.rarity)`

**1c. Update CharacterLevelUpDatabase.cs or wherever level-up costs are read**
The game currently reads `LevelUpCosts.csv` which now has 240 rows (was 200). Make sure the CSV parser doesn't have a hardcoded limit. The cost lookup should use the character's current level as the index into the CSV: `cost = LevelUpCosts[currentLevel].cost_r`

**1d. Delete CharacterLevelUpCosts.csv** (the old character-specific level costs file)
This file is no longer needed — the universal `LevelUpCosts.csv` is used for both characters and clubs. Remove references to it in `CharacterLevelUpDatabase.cs` if it loads that file.

**1e. Verify the Level Up Modal still works**
The Level Up Modal reads cost and SP from the level-up database. After these changes, open the modal for characters of different rarities and verify:
- A Common character at level 10 shows cost = 50 RP (level 10 × 5)
- A Supreme character at level 200 shows cost = 1000 RP (level 200 × 5)
- SP reward is always 1

### Priority 2: Phase C Manual Wiring (carried over from yesterday)

If not already done:
1. Unity: Edit → Project Settings → Script Execution Order: ClubDatabaseCSV = -200, ClubManager = -100
2. Run: GOLFIN/Inventory/Build Club Phase C
3. Run: GOLFIN/Inventory/Wire Club Detail Panel
4. Assign ClubThumbnailCard.prefab to ClubCarouselController.clubCardPrefab in Inspector
5. Assign ClubFilterBar to ClubCarouselController.filterBar in Inspector

### Reminders
- Platform: Windows (PowerShell, no bash/chmod/sed)
- Use `== null` not `??` for Unity objects
- Push to GitHub after completing

---

## Completed Tasks

✅ DONE: 2026-03-20 — Task 1-4 (previous session): ScreenshotTool, compress script, CLAUDE.md update, root cleanup
✅ DONE: 2026-03-20 — Phase C code: ClubCarouselController, ClubDetailPanel, builders, auto-wire, localization keys
✅ DONE: 2026-03-21 — New leveling economy: rarity-based starting/max levels in CharacterManager + ClubManager, CharacterLevelUpCosts.csv deleted, LevelUpCosts.csv now 240 rows
