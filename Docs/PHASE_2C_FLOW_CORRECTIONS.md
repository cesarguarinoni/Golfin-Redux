# Phase 2c: Flow Corrections Addendum

**Date:** 2026-03-18  
**Context:** Corrects the Level-Up Modal flow. The main spec assumed level up was immediate. In reality, EVERYTHING is a preview until CONFIRM.

**This file takes priority over the main spec where they conflict.**

---

## CORRECTION: Level Up Flow

### Wrong (what the spec says):
- LEVEL UP button immediately calls `CharacterManager.LevelUp()` which deducts RP and increments level
- CANCEL keeps the level but discards SP allocation

### Correct (how it should work):
- LEVEL UP button is a **PREVIEW** — nothing is committed to CharacterManager
- The modal locally tracks: previewed level, previewed SP earned, pending SP allocation
- CONFIRM is the ONLY action that commits anything
- CANCEL reverts EVERYTHING — as if the modal was never opened

---

## Updated State Machine

```
OPEN MODAL
  → Show current level, cost, reward (preview of what would happen)
  → LEVEL UP button enabled if player can afford it
  → CONFIRM disabled
  → CANCEL enabled

PLAYER TAPS "LEVEL UP" (preview only)
  → Modal locally increments preview level
  → Modal locally adds SP earned to available pool
  → Modal updates display: new level, new cost for NEXT level, available SP
  → Player can tap LEVEL UP again (multi-level if they can afford it)
  → [+] buttons become enabled
  → CONFIRM still disabled (SP not fully allocated)
  → CANCEL enabled (reverts everything)

PLAYER TAPS [+] BUTTONS (allocate SP)
  → Pending SP assigned to chosen stat
  → Available SP decreases
  → Orange bar segment + "+N" label appear
  → RESET becomes enabled

PLAYER ALLOCATES ALL SP (available SP = 0)
  → CONFIRM becomes enabled (gold)
  → [+] buttons disabled (no SP left)

PLAYER TAPS "CONFIRM"
  → NOW commit everything to CharacterManager:
    1. Call CharacterManager.LevelUp() for each previewed level
       (or add a new method: LevelUpTo(characterId, targetLevel))
    2. Apply SP allocation to PlayerCharacterData
    3. Refresh stat values
  → Close modal
  → Detail panel refreshes

PLAYER TAPS "CANCEL"
  → Discard ALL previewed changes (level, SP, everything)
  → Close modal
  → Nothing changed

PLAYER TAPS "RESET"
  → Clear SP allocation only
  → Previewed level stays
  → Available SP restored to full earned amount
  → [+] buttons re-enabled
  → CONFIRM disabled again
```

---

## Updated Core State in LevelUpModalController

Replace the spec's state tracking with:

```csharp
private string characterId;

// Preview state — nothing committed until Confirm
private int previewLevel;           // starts at current level
private int previewTotalSPEarned;   // starts at current totalSPEarned
private int totalRPCost;            // accumulated RP cost across all previewed levels

// Pending SP allocation
private int pendingStrength;
private int pendingClubControl;
private int pendingRecovery;
private int pendingStamina;
```

---

## Updated Open()

```csharp
public void Open(string characterId)
{
    this.characterId = characterId;
    
    var playerData = CharacterManager.Instance.GetCharacterData(characterId);
    if (playerData == null) return;
    
    // Initialize preview from current state
    previewLevel = playerData.currentLevel;
    previewTotalSPEarned = playerData.totalSPEarned;
    totalRPCost = 0;
    
    // Clear pending
    pendingStrength = 0;
    pendingClubControl = 0;
    pendingRecovery = 0;
    pendingStamina = 0;
    
    RefreshDisplay();
    Show();
}
```

---

## Updated OnLevelUpClicked (PREVIEW ONLY)

```csharp
private void OnLevelUpClicked()
{
    int maxLevel = CharacterManager.Instance.GetMaxLevel(characterId);
    if (previewLevel >= maxLevel) return;
    
    int nextLevel = previewLevel + 1;
    int cost = CharacterLevelUpDatabase.Instance.GetLevelUpCost(nextLevel);
    
    // Check if player can afford accumulated cost + this level's cost
    int totalAfterThis = totalRPCost + cost;
    if (!RewardPointsManager.Instance.CanAfford(totalAfterThis)) return;
    
    // Preview the level up (do NOT call CharacterManager.LevelUp!)
    previewLevel = nextLevel;
    totalRPCost += cost;
    
    int spReward = CharacterLevelUpDatabase.Instance.GetSPReward(nextLevel);
    previewTotalSPEarned += spReward;
    
    Debug.Log($"[LevelUpModal] Preview: Level {previewLevel}, total RP cost: {totalRPCost}, SP earned: {previewTotalSPEarned}");
    
    RefreshDisplay();
}
```

---

## Updated Available SP Calculation

```csharp
// In RefreshDisplay():
var playerData = CharacterManager.Instance.GetCharacterData(characterId);
int currentTotalSpent = playerData.spentStrength + playerData.spentClubControl 
    + playerData.spentRecovery + playerData.spentStamina;
int totalPending = pendingStrength + pendingClubControl + pendingRecovery + pendingStamina;
int availableSP = previewTotalSPEarned - currentTotalSpent - totalPending;
```

---

## Updated CONFIRM Button State

```csharp
// CONFIRM is ONLY enabled when ALL earned SP has been allocated
int availableSP = previewTotalSPEarned - currentTotalSpent - totalPending;
bool allSPAllocated = availableSP == 0 && totalPending > 0;
confirmButton.interactable = allSPAllocated;
```

---

## Updated OnConfirmClicked

```csharp
private void OnConfirmClicked()
{
    var playerData = CharacterManager.Instance.GetCharacterData(characterId);
    if (playerData == null) return;
    
    // NOW commit the level ups
    // Option A: Call LevelUp() in a loop for each level gained
    int levelsGained = previewLevel - playerData.currentLevel;
    for (int i = 0; i < levelsGained; i++)
    {
        CharacterManager.Instance.LevelUp(characterId);
    }
    
    // Apply SP allocation
    playerData.spentStrength += pendingStrength;
    playerData.spentClubControl += pendingClubControl;
    playerData.spentRecovery += pendingRecovery;
    playerData.spentStamina += pendingStamina;
    
    // Recalculate stats
    CharacterManager.Instance.RefreshStatValues(characterId);
    
    Debug.Log($"[LevelUpModal] Confirmed: +{levelsGained} levels, SP: STR+{pendingStrength} CC+{pendingClubControl} REC+{pendingRecovery} STAM+{pendingStamina}");
    
    // Reset and close
    pendingStrength = 0;
    pendingClubControl = 0;
    pendingRecovery = 0;
    pendingStamina = 0;
    
    Hide();
}
```

---

## Updated OnCancelClicked

```csharp
private void OnCancelClicked()
{
    // Discard EVERYTHING — level preview, SP allocation, all of it
    // Since we never called CharacterManager.LevelUp(), nothing needs reverting
    pendingStrength = 0;
    pendingClubControl = 0;
    pendingRecovery = 0;
    pendingStamina = 0;
    
    Debug.Log("[LevelUpModal] Cancelled — all changes discarded");
    Hide();
}
```

---

## Updated LEVEL UP Button Affordability Check

Since the player might preview multiple level-ups before confirming, the cost check needs to account for accumulated RP cost:

```csharp
// In RefreshDisplay():
int nextLevel = previewLevel + 1;
int nextCost = CharacterLevelUpDatabase.Instance.GetLevelUpCost(nextLevel);
int totalIfLevelUp = totalRPCost + nextCost;
bool canAffordNext = RewardPointsManager.Instance.CanAfford(totalIfLevelUp);
bool isMaxLevel = previewLevel >= maxLevel;

levelUpButton.interactable = !isMaxLevel && canAffordNext;

// Display shows cost for NEXT level, not accumulated total
costValue.text = nextCost.ToString();
```

---

## Summary of Key Differences from Main Spec

| Aspect | Main Spec (Wrong) | Correction (Right) |
|--------|-------------------|---------------------|
| LEVEL UP button | Immediately calls CharacterManager.LevelUp() | Preview only — local state |
| RP deduction | On LEVEL UP click | On CONFIRM only |
| Level increment | On LEVEL UP click | On CONFIRM only |
| CANCEL behavior | Keeps level, discards SP | Reverts EVERYTHING |
| CONFIRM enabled when | Any pending SP allocation | ALL SP fully allocated (available = 0) |
| Multi-level support | Not addressed | Player can tap LEVEL UP multiple times |
