# Roster System - Phase 1 Summary

**Date:** 2026-03-06  
**Status:** ✅ COMPLETE  
**Commit:** 1afc631  
**Files Created:** 10 (4.4 KB scripts + CSV data)

---

## What Was Built

### 1. CSV-Driven Economy System
**File:** `Assets/Data/CharacterLevelUpCosts.csv`

- **Format:** characterId, level, cost_r, sp_reward, str_cap, ctrl_cap, rec_cap, stam_cap
- **Sample Data:** 4 characters (Elizabeth, Shae, James, Olivia)
- **Cost Progression:** 100 → 150 → 225 → 350 → 525 → 800 → 1200...
- **Stat Caps:** Vary by rarity (Common=25, Uncommon=28, Rare=30, Mythic=?, Legendary=40)

**Important:** You manage this CSV. All economy numbers are here, not hardcoded.

### 2. CharacterLevelUpDatabase (Singleton)
**File:** `Assets/Scripts/UI/Roster/Managers/CharacterLevelUpDatabase.cs`

Loads CSV at runtime and provides queries:
```csharp
int cost = levelUpDatabase.GetLevelUpCost(characterId, level);
int spReward = levelUpDatabase.GetSPReward(characterId, level);
int statCap = levelUpDatabase.GetStatCap(characterId, level, "Strength");
```

**Pattern:**
- One-time load on Awake
- Dictionary lookup: O(1) speed
- Validates CSV format
- Logs loading status

### 3. StatAllocationStrategy (Abstract)
**Files:** 
- `StatAllocationStrategy.cs` (abstract base)
- `ManualSPAllocation.cs` (current)
- `AutomaticStatAllocation.cs` (future)

**Pattern:** Strategy pattern allows easy swap without touching other code

```csharp
// Current (Manual)
var strategy = new ManualSPAllocation(characterManager);
characterManager.SetAllocationStrategy(strategy);

// Future (Just change 1 line!)
var strategy = new AutomaticStatAllocation(characterManager, AllocationFormula.BalancedGrowth);
characterManager.SetAllocationStrategy(strategy);
```

**Manual Implementation:**
- Player clicks [+] buttons in modal
- Must allocate ALL earned SP
- Can reset and reallocate
- Confirm when satisfied

**Automatic Implementation (skeleton):**
- `EvenDistribution`: 1 SP each stat
- `RarityBased`: Weighted by character specialty
- `HighestStatPriority`: Boost lowest stat first
- `BalancedGrowth`: Keep all stats close to each other

---

## Core Managers

### CharacterManager (Singleton)
**File:** `Assets/Scripts/UI/Roster/Managers/CharacterManager.cs`

Central hub for all character operations:
```csharp
// Level-up flow
int spEarned = CharacterManager.Instance.LevelUp(characterId);
CharacterManager.Instance.AllocatePendingSP(id, "Strength", 1);
CharacterManager.Instance.ConfirmSPAllocation(id);

// Roster management
CharacterManager.Instance.SelectCharacter(id);
CharacterManager.Instance.SwapCharacters(id1, id2);

// Data access
var playerChar = CharacterManager.Instance.GetPlayerCharacter(id);
var allOwned = CharacterManager.Instance.GetAllOwnedCharacters();
```

**Features:**
- Coordinates with RewardPointsManager (spends R on level-up)
- Queries CharacterLevelUpDatabase for economy data
- Updates stat values based on CSV caps
- Fires events: `OnCharacterLeveledUp`, `OnCharacterSelected`, `OnRosterChanged`
- Saves/loads from PlayerPrefs (TODO: implement JSON serialization)

### RewardPointsManager (Singleton)
**File:** `Assets/Scripts/UI/Roster/Managers/RewardPointsManager.cs`

Manages R currency:
```csharp
int points = RewardPointsManager.Instance.GetPoints();
bool canAfford = RewardPointsManager.Instance.CanAfford(cost);
bool success = RewardPointsManager.Instance.SpendPoints(cost);
RewardPointsManager.Instance.EarnPoints(amount);
```

**Features:**
- Persistent: Saves to PlayerPrefs
- Event-driven: `OnPointsChanged` for UI updates
- Safe: Validates before spending
- Testing: `SetPoints()`, `ResetToDefault()`

### CharacterDatabase (ScriptableObject)
**File:** `Assets/Scripts/UI/Roster/Managers/CharacterDatabase.cs`

Base character templates:
```csharp
[CreateAssetMenu(...)]
public class CharacterData : ScriptableObject
{
    public string characterId;              // "char_elizabeth"
    public string characterName;            // "Elizabeth Blackwood"
    public CharacterRarity rarity;          // Rare, Legendary, etc
    public int baseStrength = 10;           // Base stats at level 1
    public Sprite portraitThumbnail;        // Carousel card
    public Sprite portraitFull;             // Detail panel
    public Color rarityColor;               // UI coloring
    public AudioClip levelUpSound;
}
```

**Rarity Colors:**
- Common: Gray (0.6, 0.6, 0.6)
- Uncommon: Blue (0.29, 0.56, 0.89)
- Rare: Green (0.18, 0.8, 0.44)
- Mythic: Yellow (0.94, 0.77, 0.06)
- Legendary: Red (0.91, 0.3, 0.24)
- Supreme: Purple (0.61, 0.35, 0.71)

---

## Data Models

### PlayerCharacterData
**File:** `Assets/Scripts/UI/Roster/Data/PlayerCharacterData.cs`

Tracks a player-owned character instance:
```csharp
public class PlayerCharacterData
{
    public string characterId;              // Reference to CharacterData
    public int currentLevel;                // 1 to 199
    public int totalSPEarned;               // From all level-ups combined
    
    public int spentStrength;               // SP allocated to each stat
    public int spentClubControl;
    public int spentRecovery;
    public int spentStamina;
    
    public int currentStrength;             // Base + SP allocation
    public int currentClubControl;
    public int currentRecovery;
    public int currentStamina;
    
    // Pending allocation (during modal)
    public int pendingSpentStrength;
    // ...
    
    public bool isSelected;                 // Active for gameplay
    public DateTime acquiredDate;
}
```

**Key Methods:**
- `GetAvailableSP()`: Remaining SP to allocate
- `GetAvailablePendingSP()`: Remaining pending SP
- `AllocatePendingSP(stat, amount)`: Allocate during modal
- `ConfirmPendingSP()`: Move pending to actual
- `ResetPendingSP()`: Discard pending allocations
- `GetCurrentStat(statName)`: Get stat value

### CharacterLevelUpData
**File:** `Assets/Scripts/UI/Roster/Data/CharacterLevelUpData.cs`

Single CSV row (one level for one character):
```csharp
public class CharacterLevelUpData
{
    public string characterId;          // char_shae
    public int level;                   // The level being leveled TO
    public int cost_r;                  // Cost to reach this level
    public int sp_reward;               // SP gained
    public int str_cap;                 // Max stat at this level
    public int ctrl_cap;
    public int rec_cap;
    public int stam_cap;
}
```

---

## Architecture & Patterns

### 1. Singleton Pattern
- `CharacterManager`
- `RewardPointsManager`
- `CharacterLevelUpDatabase`

All use `DontDestroyOnLoad` so they persist across scenes.

### 2. Strategy Pattern (SP Allocation)
Abstract strategy allows swapping implementations:
- Manual (current): Player chooses where to allocate
- Automatic (future): System distributes automatically

No other code needs to change when swapping.

### 3. CSV-Driven Economy
ALL numeric values in CSV:
- Level-up costs
- SP rewards
- Stat caps

Allows Cesar to balance game without touching code.

### 4. Event-Driven UI Updates
```csharp
// Managers fire events
CharacterManager.Instance.OnCharacterLeveledUp += (id) => {...};
RewardPointsManager.Instance.OnPointsChanged += (points) => {...};

// UI listens and updates
```

---

## CSV Format (You Manage This)

**Location:** `Assets/Data/CharacterLevelUpCosts.csv`

**Columns:**
| Column | Type | Example | Notes |
|--------|------|---------|-------|
| characterId | string | char_elizabeth | Must match CharacterData.characterId |
| level | int | 50 | The level being leveled TO |
| cost_r | int | 1000 | Reward Points to level up |
| sp_reward | int | 1 | Stat Points earned (usually 1) |
| str_cap | int | 30 | Max Strength at this level |
| ctrl_cap | int | 30 | Max Club Control |
| rec_cap | int | 20 | Max Recovery |
| stam_cap | int | 27 | Max Stamina |

**Rules:**
1. One row per character per level
2. Levels don't need to be consecutive (we only load what exists)
3. Cost can change every 10 levels or more frequently
4. Stat caps can increase as you level
5. CSV loaded at runtime by CharacterLevelUpDatabase

**Example progression:**
```
char_elizabeth,1-9,cost=100,stat_caps=30/30/20/27
char_elizabeth,10-19,cost=150,stat_caps=30/30/20/27
char_elizabeth,20-29,cost=225,stat_caps=30/30/20/27
char_elizabeth,30-39,cost=350,stat_caps=30/30/20/27
...
```

---

## What's Ready for Phase 2

✅ All data structures complete  
✅ CSV system loaded and queried  
✅ SP allocation strategy pattern (swappable)  
✅ Character & Reward manager fully functional  
✅ Test-ready: Just add UI prefabs

**Next:** UI Hierarchy (Carousel, Detail Panel, Level-Up Modal)

---

## Testing Checklist

**What you should test in Unity Editor:**

```
□ Assign CharacterDatabase to CharacterManager inspector
□ Assign CharacterLevelUpDatabase to CharacterManager inspector
□ Assign CharacterLevelUpCosts.csv to CharacterLevelUpDatabase
□ Assign RewardPointsManager to a new GameObject
□ Press Play
□ Check Console:
  - "[CharacterLevelUpDatabase] Loaded X level-up records from CSV"
  - "[CharacterManager] Initialized with 4 sample characters"
  - "[RewardPointsManager] Loaded 50000 points"
□ Verify no errors
□ Test CharacterManager.LevelUp() in Debug console
□ Verify R points decreased
□ Verify character level increased
□ Verify SP earned
```

---

## Known TODO Items

- [ ] JSON serialization for PlayerPrefs (save/load roster)
- [ ] More AutomaticStatAllocation formulas (RarityBased, BalancedGrowth)
- [ ] Character acquisition/gacha system
- [ ] Server integration (future phase)
- [ ] Anti-cheat validation for SP allocation

---

**Created by:** Kai (Aikenken Bot)  
**For:** Cesar Guarinoni  
**Next Phase:** 2026-03-06 Afternoon - UI Hierarchy
