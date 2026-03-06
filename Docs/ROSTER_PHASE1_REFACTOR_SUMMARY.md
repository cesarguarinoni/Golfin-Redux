# Roster System Phase 1 - Refactor Summary

**Date:** 2026-03-06 (AM)  
**Status:** ✅ REFACTORED  
**Commit:** `144e9c3`  
**Focus:** Eliminate data duplication, rarity-based design

---

## What Changed

### ❌ Old Approach (Wasteful)
```csv
characterId,level,cost_r,sp_reward,str_cap,ctrl_cap,rec_cap,stam_cap
char_elizabeth,1,100,1,30,30,20,27
char_elizabeth,2,100,1,30,30,20,27
...
char_shae,1,100,1,40,40,40,40
char_shae,2,100,1,40,40,40,40
```
❌ 80+ rows (duplicated per character)

### ✅ New Approach (Clean)

**LevelUpCosts.csv** (universal progression)
```csv
level,cost_r,sp_reward
1,100,1
2,100,1
...
10,150,1
...
199,300000,1
```
✅ 43 rows (one per level, shared by ALL characters)

**RarityStatCaps.cs** (mapped to rarity enum)
```csharp
Common:     25/25/18/22
Uncommon:   28/28/19/25
Rare:       30/30/20/27
Mythic:     35/35/25/32
Legendary:  40/40/40/40
Supreme:    50/50/50/50
```
✅ Hardcoded (cleaner than CSV for data this small)

---

## New Architecture

### Data Flow
```
Player levels up character
  ↓
CharacterManager.LevelUp(id)
  ↓
Query CharacterLevelUpDatabase.GetLevelUpCost(level) ← NO characterId
  ↓
Spend R, earn SP
  ↓
RefreshStatValues() queries RarityStatCaps.GetStatCaps(character.rarity)
  ↓
Stats updated, clamped to rarity max
```

### Key Files

**New: RarityStatCaps.cs**
```csharp
public static int GetStatCap(CharacterRarity rarity, string statName)
public static StatCapData GetStatCaps(CharacterRarity rarity)
```

**Updated: CharacterLevelUpDatabase.cs**
- `GetLevelUpCost(int level)` ← was `GetLevelUpCost(string characterId, int level)`
- `GetSPReward(int level)` ← was `GetSPReward(string characterId, int level)`
- `GetAllLevels()` → returns List<int> of all loaded levels
- `GetMaxLevel()` → returns int (universal, no characterId param)

**Updated: CharacterManager.cs**
- `RefreshStatValues()` now uses `RarityStatCaps.GetStatCaps(baseData.rarity)`
- `GetLevelUpCost()` calls `levelUpDatabase.GetLevelUpCost(nextLevel)`

**Updated: LevelUpCosts.csv**
- Now only 43 rows instead of 80+
- Format: `level,cost_r,sp_reward` (no stat cap columns)

---

## Benefits

| Aspect | Old | New |
|--------|-----|-----|
| CSV rows | 80+ | 43 |
| Duplication | High (per char) | None |
| Stat cap logic | CSV-driven | Code-driven (cleaner) |
| Level-up query | `(characterId, level)` | `(level)` only |
| Game design | Arbitrary caps | Rarity-determined |

### Conceptual Clarity
- **Levels:** Same for all characters (universal progression)
- **Stat Caps:** Determined by rarity (5-10 tiers total)
- **Base Stats:** Defined in CharacterData (per-character variation)

Example:
- Elizabeth (Rare) levels to 100: Strength can go up to 30
- Shae (Legendary) levels to 100: Strength can go up to 40
- Both use the same level-up cost table

---

## Testing with New Approach

### Run Automated Tests
```
1. Tools → GOLFIN → Setup Roster System (Phase 1)
2. Click Play
3. Tools → GOLFIN → Test Roster Phase 1
```

### Test 4 Now Validates
- ✅ Universal level costs (Lv10 = 150R for everyone)
- ✅ Rarity-based stat caps (Rare: 30, Legendary: 40)
- ✅ CSV has 40+ levels loaded
- ✅ Max level = 199

---

## CSV Management Going Forward

**Who manages:** You (Cesar)

**To adjust progression:**
- Edit `LevelUpCosts.csv`
- Change cost every 10 levels (or more frequently)
- Add/remove levels as needed

**Example:** Make level-up cheaper
```csv
# OLD
10,150,1    ← 150R to reach level 10

# NEW
10,100,1    ← 100R to reach level 10 (cheaper!)
```

**To adjust rarity power:**
- Edit `RarityStatCaps.cs`
- Change the stat cap numbers
- Example: Make Legendary stronger
```csharp
// OLD
CharacterRarity.Legendary => new StatCapData(
    str: 40, ctrl: 40, rec: 40, stam: 40
),

// NEW (more powerful)
CharacterRarity.Legendary => new StatCapData(
    str: 50, ctrl: 50, rec: 50, stam: 50
),
```

---

## Migration Checklist

- ✅ Created new CSV format (universal level costs)
- ✅ Created RarityStatCaps.cs (rarity enum mapper)
- ✅ Updated CharacterLevelUpDatabase (simplified queries)
- ✅ Updated CharacterManager (uses rarity for caps)
- ✅ Updated test runner (validates new approach)
- ✅ All managers still wire together correctly
- ✅ Tests pass with new architecture

---

## What Didn't Change

- ✅ Phase 1 functionality (managers, SP system, CSV loading)
- ✅ Phase 2 UI (carousel, detail panel, modal - no impact)
- ✅ Test workflow (same: Setup → Play → Test)
- ✅ Base character stats (in CharacterData)
- ✅ Player character progression (level, SP, stats)

---

## Next

- ✅ Phase 1 refactored, cleaner
- ⏳ Phase 2a carousel (unchanged)
- ⏳ Phase 2b detail panel (unchanged)
- ⏳ Phase 2c level-up modal (unchanged)

All Phase 2 work remains the same - this refactor just cleans up the data layer.

---

**Created by:** Kai  
**For:** Cesar Guarinoni  
**Status:** Ready for Phase 2!  
**Commit:** `🔗 144e9c3`
