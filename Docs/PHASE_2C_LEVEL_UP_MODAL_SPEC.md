# Phase 2c: Level-Up Modal — Implementation Spec

**Author:** Claude (Architect)  
**Date:** 2026-03-18  
**For:** Claude Code implementation  
**Prerequisites:** Phase 2b complete (detail panel data-bound, CharacterManager functional)  
**Visual References:** Character_Level_Up.png (State 1), Character_Level_Up-1.png (State 2), Character_Level_Up-2.png (State 3)

---

## Overview

The Level-Up Modal is an overlay dialog that opens when the player taps LEVEL UP on the detail panel. It allows the player to:
1. See their current level + cost to level up
2. Tap LEVEL UP to spend Reward Points and gain SP
3. Allocate earned SP across 4 stats using [+] buttons
4. Reset pending allocations
5. Cancel (discard everything) or Confirm (apply level + SP allocation)

The modal sits ON TOP of the roster screen (not a separate screen). It should use `ModalController` as a base class for fade-in/backdrop behavior.

---

## 1. Three Visual States

### State 1: Before Level Up (0 SP available)
- Character name: "SHAE O'CONNELL"
- Rarity: "LEGENDARY" (colored) + "Lv 160/199"
- NEXT LEVEL: → "Lv 161" (green text)
- COST: RP icon + "805"
- REWARD: "1 SP" (green text)
- LEVEL UP button: gold, enabled (if player can afford)
- AVAILABLE SP: "0 SP"
- 4 stat rows: icon + name + bar + value/cap + [+] button
- [+] buttons: DISABLED (no SP to spend)
- RESET button: disabled (nothing to reset)
- CANCEL / CONFIRM buttons at bottom
- CONFIRM: disabled or gray (no changes to confirm)

### State 2: After Level Up (1 SP earned, not yet allocated)
- Level updates to "Lv 161/199"
- NEXT LEVEL: → "Lv 162"
- COST updates to "810" (next level's cost)
- AVAILABLE SP: "1 SP" (green)
- [+] buttons: ENABLED (SP available to allocate)
- LEVEL UP button: stays enabled if player can afford next level
- RESET: still disabled (no pending allocation yet)
- CONFIRM: still disabled (no allocation to confirm)

### State 3: SP Allocated (spent 1 SP on Strength)
- AVAILABLE SP: "0 SP" (spent it)
- STRENGTH: shows "+1" in orange next to the label
- STRENGTH bar: blue portion (current) + orange segment (pending +1)
- STRENGTH value: "31/80" (was 30, now 31 pending)
- [+] buttons: DISABLED again (no SP left)
- RESET: ENABLED (can undo the allocation)
- CONFIRM: ENABLED + gold highlight (there are pending changes)

---

## 2. Modal Hierarchy

Create as a new prefab or build directly under RosterScreen. The modal overlays everything.

```
LevelUpModal (extends ModalController)
├── Backdrop (Image, dark semi-transparent, blocks clicks behind)
├── ModalPanel (main white/dark bordered panel)
│   ├── HeaderSection
│   │   ├── CharacterNameText (TMP — "SHAE\nO'CONNELL")
│   │   └── StatusIcons (eye + bolt, same as detail panel)
│   │
│   ├── Divider
│   │
│   ├── InfoSection
│   │   ├── RarityLevelRow
│   │   │   ├── RarityLabel (TMP — "LEGENDARY", colored)
│   │   │   └── LevelText (TMP — "Lv 160/199")
│   │   ├── NextLevelRow (HorizontalLayout)
│   │   │   ├── NextLevelLabel (TMP — "NEXT LEVEL")
│   │   │   ├── ArrowIcon (TMP or Image — "→")
│   │   │   └── NextLevelValue (TMP — "Lv 161", green)
│   │   ├── CostRow (HorizontalLayout)
│   │   │   ├── CostLabel (TMP — "COST")
│   │   │   ├── RPIcon (Image — RP coin sprite)
│   │   │   └── CostValue (TMP — "805")
│   │   └── RewardRow (HorizontalLayout)
│   │       ├── RewardLabel (TMP — "REWARD")
│   │       └── RewardValue (TMP — "1 SP", green)
│   │
│   ├── LevelUpButton (Button — gold, "LEVEL UP")
│   │
│   ├── Divider
│   │
│   ├── SPSection
│   │   ├── AvailableSPRow (HorizontalLayout)
│   │   │   ├── AvailableSPLabel (TMP — "AVAILABLE SP")
│   │   │   └── AvailableSPValue (TMP — "0 SP" or "1 SP", green when > 0)
│   │   │
│   │   ├── StatRow_Strength
│   │   │   ├── StatIcon (Image)
│   │   │   ├── StatName (TMP — "STRENGHT")
│   │   │   ├── PendingLabel (TMP — "+1", orange, hidden when 0)
│   │   │   ├── StatBar (Image — fillAmount, blue + orange pending segment)
│   │   │   ├── StatValue (TMP — "30/80")
│   │   │   └── PlusButton (Button — [+], gold)
│   │   │
│   │   ├── StatRow_ClubControl (same structure)
│   │   ├── StatRow_Recovery (same structure)
│   │   ├── StatRow_Stamina (same structure)
│   │   │
│   │   └── ResetButton (Button — "RESET", silver/gray)
│   │
│   └── FooterSection (HorizontalLayout)
│       ├── CancelButton (Button — "CANCEL", silver)
│       └── ConfirmButton (Button — "CONFIRM", gold when active, gray when disabled)
```

---

## 3. LevelUpModalController.cs

New script. Extends `ModalController` for the Show/Hide/Backdrop behavior.

### Serialized Fields

```csharp
[Header("Character Info")]
[SerializeField] private TextMeshProUGUI characterNameText;
[SerializeField] private TextMeshProUGUI rarityLabel;
[SerializeField] private TextMeshProUGUI levelText;
[SerializeField] private TextMeshProUGUI nextLevelValue;
[SerializeField] private TextMeshProUGUI costValue;
[SerializeField] private Image rpIcon;
[SerializeField] private TextMeshProUGUI rewardValue;

[Header("Level Up")]
[SerializeField] private Button levelUpButton;

[Header("SP Allocation")]
[SerializeField] private TextMeshProUGUI availableSPValue;

[Header("Stat Rows — Strength")]
[SerializeField] private Image strengthIcon;
[SerializeField] private TextMeshProUGUI strengthName;
[SerializeField] private TextMeshProUGUI strengthPending;   // "+1" orange label
[SerializeField] private Image strengthBar;                  // blue fill
[SerializeField] private Image strengthBarPending;           // orange fill overlay
[SerializeField] private TextMeshProUGUI strengthValue;
[SerializeField] private Button strengthPlusButton;

[Header("Stat Rows — Club Control")]
// same 7 fields as Strength...

[Header("Stat Rows — Recovery")]
// same 7 fields...

[Header("Stat Rows — Stamina")]
// same 7 fields...

[Header("Actions")]
[SerializeField] private Button resetButton;
[SerializeField] private Button cancelButton;
[SerializeField] private Button confirmButton;
[SerializeField] private TextMeshProUGUI confirmButtonText;
[SerializeField] private Image confirmButtonImage;

[Header("Colors")]
[SerializeField] private Color activeButtonColor = new Color(0.85f, 0.72f, 0.2f, 1f);   // gold
[SerializeField] private Color inactiveButtonColor = new Color(0.6f, 0.6f, 0.6f, 1f);   // gray
[SerializeField] private Color pendingBarColor = new Color(1f, 0.7f, 0.2f, 1f);          // orange
[SerializeField] private Color greenTextColor = new Color(0.2f, 0.8f, 0.2f, 1f);         // green for values
```

### Core State

```csharp
private string characterId;
private int pendingStrength;
private int pendingClubControl;
private int pendingRecovery;
private int pendingStamina;
private bool hasLeveledUp;  // tracks if player pressed LEVEL UP this session
```

### Open(string characterId)

Called by CharacterDetailPanel when LEVEL UP is tapped:

```csharp
public void Open(string characterId)
{
    this.characterId = characterId;
    pendingStrength = 0;
    pendingClubControl = 0;
    pendingRecovery = 0;
    pendingStamina = 0;
    hasLeveledUp = false;
    
    RefreshDisplay();
    Show(); // ModalController.Show() handles fade-in + backdrop
}
```

### RefreshDisplay()

Updates ALL UI elements based on current state:

```csharp
private void RefreshDisplay()
{
    var playerData = CharacterManager.Instance.GetCharacterData(characterId);
    var csvChar = CharacterDatabaseCSV.Instance?.GetCharacter(characterId);
    if (playerData == null) return;

    // --- Header ---
    characterNameText.text = csvChar != null 
        ? csvChar.GetDisplayName() 
        : characterId.ToUpper();
    
    var rarity = csvChar?.rarity ?? CharacterRarity.Common;
    rarityLabel.text = rarity.ToString().ToUpper();
    rarityLabel.color = RarityHelper.GetRarityColor(rarity);
    
    int currentLevel = playerData.currentLevel;
    int maxLevel = CharacterManager.Instance.GetMaxLevel(characterId);
    levelText.text = $"Lv {currentLevel}/{maxLevel}";
    
    // --- Next Level / Cost / Reward ---
    bool isMaxLevel = currentLevel >= maxLevel;
    int nextLevel = currentLevel + 1;
    
    if (isMaxLevel)
    {
        nextLevelValue.text = "MAX";
        costValue.text = "—";
        rewardValue.text = "—";
        levelUpButton.interactable = false;
    }
    else
    {
        nextLevelValue.text = $"Lv {nextLevel}";
        nextLevelValue.color = greenTextColor;
        
        int cost = CharacterManager.Instance.GetLevelUpCost(characterId);
        costValue.text = cost.ToString();
        
        // SP reward from level-up database
        int spReward = CharacterLevelUpDatabase.Instance.GetSPReward(nextLevel);
        rewardValue.text = $"{spReward} SP";
        rewardValue.color = greenTextColor;
        
        // Can afford?
        bool canAfford = RewardPointsManager.Instance.CanAfford(cost);
        levelUpButton.interactable = canAfford;
    }
    
    // --- Available SP ---
    int totalPending = pendingStrength + pendingClubControl + pendingRecovery + pendingStamina;
    int availableSP = playerData.GetAvailableSP() - totalPending;
    availableSPValue.text = $"{availableSP} SP";
    availableSPValue.color = availableSP > 0 ? greenTextColor : Color.white;
    
    // --- Stat Rows ---
    var caps = RarityStatCaps.GetStatCaps(rarity);
    int baseStr = csvChar?.baseStrength ?? 0;
    int baseCc = csvChar?.baseClubControl ?? 0;
    int baseRec = csvChar?.baseRecovery ?? 0;
    int baseStam = csvChar?.baseStamina ?? 0;
    
    UpdateStatRow(strengthBar, strengthBarPending, strengthValue, strengthName, strengthPending, strengthPlusButton,
        baseStr + playerData.spentStrength, pendingStrength, caps.strengthCap, "STRENGHT", availableSP);
    UpdateStatRow(clubControlBar, clubControlBarPending, clubControlValue, clubControlName, clubControlPending, clubControlPlusButton,
        baseCc + playerData.spentClubControl, pendingClubControl, caps.clubControlCap, "CLUB CONTROL", availableSP);
    UpdateStatRow(recoveryBar, recoveryBarPending, recoveryValue, recoveryName, recoveryPending, recoveryPlusButton,
        baseRec + playerData.spentRecovery, pendingRecovery, caps.recoveryCap, "RECOVERY", availableSP);
    UpdateStatRow(staminaBar, staminaBarPending, staminaValue, staminaName, staminaPending, staminaPlusButton,
        baseStam + playerData.spentStamina, pendingStamina, caps.staminaCap, "STAMINA", availableSP);
    
    // --- Reset / Cancel / Confirm ---
    bool hasPending = totalPending > 0;
    resetButton.interactable = hasPending;
    confirmButton.interactable = hasPending;
    confirmButtonImage.color = hasPending ? activeButtonColor : inactiveButtonColor;
}
```

### UpdateStatRow Helper

```csharp
private void UpdateStatRow(Image bar, Image barPending, TextMeshProUGUI valueText, 
    TextMeshProUGUI nameText, TextMeshProUGUI pendingText, Button plusButton,
    int currentValue, int pendingAmount, int cap, string statLabel, int availableSP)
{
    // Bar fill
    float fillCurrent = cap > 0 ? (float)currentValue / cap : 0f;
    float fillPending = cap > 0 ? (float)(currentValue + pendingAmount) / cap : 0f;
    bar.fillAmount = fillCurrent;
    
    if (barPending != null)
    {
        barPending.fillAmount = fillPending;
        barPending.color = pendingBarColor;
        barPending.gameObject.SetActive(pendingAmount > 0);
    }
    
    // Value text
    int displayValue = currentValue + pendingAmount;
    valueText.text = $"{displayValue}/{cap}";
    
    // Pending label (+1, +2, etc)
    if (pendingText != null)
    {
        if (pendingAmount > 0)
        {
            pendingText.text = $"+{pendingAmount}";
            pendingText.color = pendingBarColor;
            pendingText.gameObject.SetActive(true);
        }
        else
        {
            pendingText.gameObject.SetActive(false);
        }
    }
    
    // [+] button enabled if SP available AND stat not at cap
    bool canAllocate = availableSP > 0 && (currentValue + pendingAmount) < cap;
    plusButton.interactable = canAllocate;
}
```

### Button Handlers

```csharp
// --- LEVEL UP ---
private void OnLevelUpClicked()
{
    int spEarned = CharacterManager.Instance.LevelUp(characterId);
    if (spEarned > 0)
    {
        hasLeveledUp = true;
        Debug.Log($"[LevelUpModal] Leveled up! Earned {spEarned} SP");
        RefreshDisplay();
        // TODO: Play level-up SFX + VFX
    }
}

// --- PLUS BUTTONS ---
private void OnStrengthPlus()
{
    pendingStrength++;
    RefreshDisplay();
}
private void OnClubControlPlus()
{
    pendingClubControl++;
    RefreshDisplay();
}
private void OnRecoveryPlus()
{
    pendingRecovery++;
    RefreshDisplay();
}
private void OnStaminaPlus()
{
    pendingStamina++;
    RefreshDisplay();
}

// --- RESET ---
private void OnResetClicked()
{
    pendingStrength = 0;
    pendingClubControl = 0;
    pendingRecovery = 0;
    pendingStamina = 0;
    RefreshDisplay();
}

// --- CANCEL ---
private void OnCancelClicked()
{
    // Discard pending SP allocation
    // Note: If player already pressed LEVEL UP, the level increase is KEPT
    // Only the SP allocation is discarded
    pendingStrength = 0;
    pendingClubControl = 0;
    pendingRecovery = 0;
    pendingStamina = 0;
    Hide(); // ModalController.Hide()
}

// --- CONFIRM ---
private void OnConfirmClicked()
{
    var playerData = CharacterManager.Instance.GetCharacterData(characterId);
    if (playerData == null) return;
    
    // Apply pending SP to player data
    playerData.spentStrength += pendingStrength;
    playerData.spentClubControl += pendingClubControl;
    playerData.spentRecovery += pendingRecovery;
    playerData.spentStamina += pendingStamina;
    
    // Recalculate stat values
    CharacterManager.Instance.RefreshStatValues(characterId);
    
    Debug.Log($"[LevelUpModal] Confirmed SP: STR+{pendingStrength} CC+{pendingClubControl} REC+{pendingRecovery} STAM+{pendingStamina}");
    
    // Reset and close
    pendingStrength = 0;
    pendingClubControl = 0;
    pendingRecovery = 0;
    pendingStamina = 0;
    
    Hide(); // ModalController.Hide()
    
    // Fire event so detail panel refreshes
    // CharacterManager already has OnCharacterLeveledUp which was fired during LevelUp()
    // But we also need to notify about SP allocation. 
    // For now, the detail panel can refresh when the modal closes.
}
```

### Start() — Wire Button Listeners

```csharp
private void Start()
{
    levelUpButton?.onClick.AddListener(OnLevelUpClicked);
    strengthPlusButton?.onClick.AddListener(OnStrengthPlus);
    clubControlPlusButton?.onClick.AddListener(OnClubControlPlus);
    recoveryPlusButton?.onClick.AddListener(OnRecoveryPlus);
    staminaPlusButton?.onClick.AddListener(OnStaminaPlus);
    resetButton?.onClick.AddListener(OnResetClicked);
    cancelButton?.onClick.AddListener(OnCancelClicked);
    confirmButton?.onClick.AddListener(OnConfirmClicked);
}
```

---

## 4. CharacterDetailPanel Integration

Update `OnLevelUpClicked()` in CharacterDetailPanel to open the modal:

```csharp
[SerializeField] private LevelUpModalController levelUpModal;

private void OnLevelUpClicked()
{
    if (levelUpModal != null && !string.IsNullOrEmpty(currentCharacterId))
    {
        levelUpModal.Open(currentCharacterId);
    }
}
```

Also: when the modal closes, the detail panel should refresh. Subscribe to the modal's close event or refresh in OnEnable.

---

## 5. Stat Bar Pending Segment (Orange)

The visual reference shows an orange segment on the stat bar for pending SP. This requires TWO overlapping fill images per stat:

1. **Blue bar** (background fill) — shows current confirmed stat value
2. **Orange bar** (overlay fill) — shows current + pending, only the extra segment is visible

Implementation: The orange bar sits on top of the blue bar, same size. Both use `Image.fillAmount`. The orange bar's fillAmount is `(current + pending) / cap` while the blue is `current / cap`. The orange bar is only visible when there's a pending allocation.

Alternative simpler approach: use a single bar and change the fill portion's color to orange for the pending segment. This is harder to do with Unity's built-in Image fill. The two-image approach is recommended.

---

## 6. Existing Code to Leverage

### PlayerCharacterData (already has pending SP system):
- `AllocatePendingSP(statName, amount)` — use this OR the simpler local tracking in the modal
- `ConfirmPendingSP()` — commits pending to actual spent
- `ResetPendingSP()` — clears pending
- `GetAvailableSP()` — total SP earned minus total spent
- `GetAvailablePendingSP()` — accounts for pending allocation too

**Decision for implementer:** You can either use PlayerCharacterData's built-in pending system OR track pending locally in the modal (as shown in the spec). The local approach is simpler since the modal manages its own state and only writes to PlayerCharacterData on Confirm. Either works.

### CharacterManager:
- `LevelUp(characterId)` — deducts RP, increments level, adds SP, fires event. Returns SP earned.
- `GetLevelUpCost(characterId)` — returns RP cost for next level
- `RefreshStatValues(characterId)` — recalculates current stats from base + spent
- `GetMaxLevel(characterId)` — returns 199

### CharacterLevelUpDatabase:
- `GetLevelUpCost(level)` — RP cost for a specific level
- `GetSPReward(level)` — SP earned at a specific level

### ModalController:
- `Show()` — fade in modal + backdrop
- `Hide()` — fade out modal + backdrop
- Has `modalPanel`, `backdrop`, `useAnimation`, `animationDuration`

### RewardPointsManager:
- `CanAfford(cost)` — bool check
- `SpendPoints(amount)` — already called inside CharacterManager.LevelUp()

---

## 7. Implementation Order

1. **Create the modal hierarchy** — build the UI under RosterScreen (or as a prefab)
2. **Create LevelUpModalController.cs** — extend ModalController, add all serialized fields
3. **Wire Inspector references**
4. **Implement Open() + RefreshDisplay()** — test: modal opens, shows correct data
5. **Implement LEVEL UP button** — test: RP deducted, level increments, SP shows
6. **Implement [+] buttons** — test: SP allocates, bars update, pending labels show
7. **Implement RESET** — test: pending cleared, display reverts
8. **Implement CONFIRM** — test: SP committed, modal closes, detail panel refreshes
9. **Implement CANCEL** — test: pending discarded, modal closes
10. **Polish: button states** — disabled colors, gold highlights on Confirm

---

## 8. Files to Create/Modify

| File | Action |
|------|--------|
| `LevelUpModalController.cs` | **CREATE** — new script in `Assets/Scripts/UI/Roster/UI/` |
| `CharacterDetailPanel.cs` | **MODIFY** — add levelUpModal field, update OnLevelUpClicked |
| Unity hierarchy | **BUILD** — LevelUpModal UI structure under RosterScreen |
| Unity Inspector | **WIRE** — all serialized fields on LevelUpModalController |

---

## 9. What NOT to Build

- VFX/SFX for level up (future polish)
- Multiple level-ups in sequence animation
- SP respec/refund system
- Boost modal (separate feature)
- Level up from Compare view

---

## 10. Testing Checklist

- [ ] Tapping LEVEL UP on detail panel opens the modal
- [ ] Modal shows correct character name, rarity, level
- [ ] NEXT LEVEL, COST, REWARD show correct values from CSV
- [ ] LEVEL UP button deducts RP and increments level
- [ ] After level up, AVAILABLE SP shows earned amount
- [ ] [+] buttons allocate SP to the correct stat
- [ ] Stat bar shows orange pending segment
- [ ] "+N" label appears next to stat name in orange
- [ ] [+] buttons disable when no SP available or stat at cap
- [ ] RESET clears all pending allocations
- [ ] CANCEL closes modal without applying SP (but keeps level if already leveled)
- [ ] CONFIRM applies SP allocation and closes modal
- [ ] Detail panel refreshes with new stats after CONFIRM
- [ ] RP display in top bar updates after level up
- [ ] Cannot level up past max level (199)
- [ ] Cannot level up if insufficient RP
