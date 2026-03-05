# Roster System Phase 1 - Testing Guide

**Date:** 2026-03-06  
**What to Test:** Data structures, managers, setup tool, CSV loading  
**Est. Time:** 15 minutes

---

## 🧪 Full Testing Workflow

### Part 1: Setup Tool (5 minutes)

**1. Open Unity**
- Load the Golfin-Redux project
- Open any scene (or create a new one)

**2. Run Setup Tool**
- Go to **Unity Menu → Tools → GOLFIN → Setup Roster System (Phase 1)**
- Click the menu item

**3. Check Results**

**Expected:** Dialog appears saying ✅ "Roster System Phase 1 Setup Complete!"

**Verify in Hierarchy:**
```
✓ CharacterManager (GameObject)
✓ CharacterLevelUpDatabase (GameObject)
✓ RewardPointsManager (GameObject)
```

**Check Console:**
Should see these logs (in order):
```
[CharacterLevelUpDatabase] Loaded 80 level-up records from CSV
[CharacterManager] Initialized with 4 sample characters
[RewardPointsManager] Loaded 50000 points
```

**If you see errors:**
- ❌ "CharacterLevelUpDatabase already exists in scene" → Delete old instances first
- ❌ "CSV file not assigned" → Manually assign `Assets/Data/CharacterLevelUpCosts.csv` to the database
- ❌ "CharacterDatabase not assigned" → Create a CharacterDatabase ScriptableObject (File → Create → GOLFIN → Character Database)

---

### Part 2: CSV Loading Test (3 minutes)

**1. Select CharacterLevelUpDatabase in Hierarchy**

**2. Inspector should show:**
- Component: CharacterLevelUpDatabase
- Field: "Level Up Costs Csv" = CharacterLevelUpCosts.csv (TextAsset)

**3. Check Console for:**
```
[CharacterLevelUpDatabase] Loaded 80 level-up records from CSV
```

**4. Play the scene (Click Play button)**

**5. Nothing should happen (that's normal)** - Just verify no errors appear

**6. Stop Play**

---

### Part 3: Manager Functionality Test (5 minutes)

**1. Keep Play mode running**

**2. Open Console window (Window → General → Console)**

**3. Create a simple test script or use Console commands:**

```csharp
// Test Reward Points
var rpManager = Golfin.Roster.RewardPointsManager.Instance;
Debug.Log($"Current Points: {rpManager.GetPoints()}");
// Expected: "Current Points: 50000"

// Test Character Manager
var charManager = Golfin.Roster.CharacterManager.Instance;
var playerChar = charManager.GetPlayerCharacter("char_elizabeth");
Debug.Log($"Elizabeth: Level {playerChar.currentLevel}, SP Earned: {playerChar.totalSPEarned}");
// Expected: "Elizabeth: Level 1, SP Earned: 0"

// Test Level-Up
int spEarned = charManager.LevelUp("char_elizabeth");
Debug.Log($"Level up! Earned {spEarned} SP");
// Expected: "Level up! Earned 1 SP"
// Expected: Character level → 2
// Expected: Points → 49900 (50000 - 100)

// Verify
var points = rpManager.GetPoints();
Debug.Log($"Points after level-up: {points}");
// Expected: "Points after level-up: 49900"
```

**4. Check Console for:**
```
✓ Current Points: 50000
✓ Elizabeth: Level 1, SP Earned: 0
✓ Level up! Earned 1 SP
✓ Points after level-up: 49900
```

**If different:**
- ❌ Points not decreasing → RewardPointsManager not connected
- ❌ Level not increasing → CharacterManager not wired
- ❌ SP not earned → CSV not loading correctly

---

### Part 4: CSV Data Validation (2 minutes)

**1. Select CharacterLevelUpDatabase in Inspector**

**2. In Play mode, check Console:**
```
[CharacterLevelUpDatabase] Loaded 80 level-up records from CSV
```

**3. Verify CSV has all characters:**
- char_elizabeth (40 levels)
- char_shae (23 levels)
- char_james (14 levels)
- char_olivia (14 levels)
- **Total: ~80+ rows** (exact count shown in console)

**4. Test a lookup:**
```csharp
var db = Golfin.Roster.CharacterLevelUpDatabase.Instance;
int cost = db.GetLevelUpCost("char_elizabeth", 10);
Debug.Log($"Elizabeth Lv10 cost: {cost}R");
// Expected: "Elizabeth Lv10 cost: 150R"
```

**5. Check Console:**
```
✓ Elizabeth Lv10 cost: 150R
```

---

## ✅ Checklist

### All Tests Pass?
- [ ] Setup tool creates 3 GameObjects
- [ ] CSV loads (80+ records logged)
- [ ] All 4 characters initialized
- [ ] Reward Points = 50000
- [ ] Level-up spends R correctly
- [ ] Level increases
- [ ] SP earned correctly
- [ ] Cost lookup works
- [ ] No errors in Console

### Warnings That Are OK (Don't Worry):
- ⚠️ "Deactivating GameObject while a script in the same scene is being initialized" - Normal in tests
- ⚠️ "DontDestroyOnLoad only works for root GameObjects" - If you nested the manager (don't do this)

### Errors That Need Fixing:
- ❌ "Character X not found" - CSV spelling mismatch
- ❌ "CSV is empty or malformed" - CSV file corrupted
- ❌ "Cannot spend negative amount" - Bug in code (report to Kai)
- ❌ "Database not loaded yet" - CSV not assigned to inspector

---

## 📝 What Each Manager Does (Verify Behavior)

### CharacterManager
- `LevelUp(id)` → Spends R, increases level, earns SP
- `GetPlayerCharacter(id)` → Returns owned character data
- `GetAllOwnedCharacters()` → Returns list of 4 sample characters
- `SelectCharacter(id)` → Sets active character

### RewardPointsManager
- `GetPoints()` → Returns 50000 initially
- `SpendPoints(amount)` → Decreases points, returns true if success
- `EarnPoints(amount)` → Increases points
- `CanAfford(amount)` → Returns true/false

### CharacterLevelUpDatabase
- `GetLevelUpCost(id, level)` → Returns R cost for level
- `GetSPReward(id, level)` → Returns SP earned
- `GetStatCap(id, level, stat)` → Returns max stat value

---

## 🎯 Known Test Patterns

**Pattern 1: Cascading Success**
```
1. Setup tool runs → All managers created
2. Play mode → CSV loads automatically
3. Test level-up → R spent, level increased, SP earned
4. Everything should flow together
```

**Pattern 2: Persistence**
```
1. Set points to 1000000
2. Stop Play
3. Play again
4. Points still 1000000 (saved in PlayerPrefs)
```

**Pattern 3: Event System**
```
// Subscribe to events
CharacterManager.Instance.OnCharacterLeveledUp += (id) => {
    Debug.Log($"Character leveled up: {id}");
};
// Level up
charManager.LevelUp("char_elizabeth");
// Console should show: "Character leveled up: char_elizabeth"
```

---

## 🐛 Troubleshooting

**Q: Setup tool says "CharacterManager already exists"**
- Delete all 3 managers from Hierarchy
- Run tool again

**Q: CSV not loading**
- Check: Is CharacterLevelUpCosts.csv in Assets/Data/ ?
- Check: Is it assigned to CharacterLevelUpDatabase in inspector?
- Check: Does the file have data (not empty)?

**Q: Level-up doesn't spend points**
- Verify RewardPointsManager is in scene
- Check console for errors
- Manually check: `RewardPointsManager.Instance.GetPoints()`

**Q: Character not leveling up**
- Check: Does character exist in CSV?
- Check: Does CSV have the next level entry?
- Example: Leveling to 2 requires a row with `level=2`

**Q: Points stuck at 50000**
- Check: Is it level 1? (No cost for level 1)
- Try: `charManager.LevelUp("char_elizabeth")` (should level to 2, costs 100R)

---

## ✨ Success Indicators

🎉 **All Green When:**
1. Setup tool creates all 3 GameObjects without errors
2. Console shows CSV loaded (80+ records)
3. CharacterManager finds 4 sample characters
4. RewardPointsManager shows 50000 points
5. Level-up works: R spent, level increased, SP earned
6. CSV lookups return correct values
7. No errors in Console (warnings are OK)

---

**Created by:** Kai  
**For:** Cesar (Quality verification)  
**Estimated Test Time:** 15 minutes  
**Status:** Ready to test!
