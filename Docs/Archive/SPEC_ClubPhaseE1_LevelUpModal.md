# Spec: Club Inventory — Phase E1 (Club Level Up Modal)

> **Author:** Claude (Architect)  
> **Date:** 2026-03-25  
> **For:** Claude Code (Implementer)  
> **Status:** Ready for implementation  
> **Depends on:** Phase D (Compare) ✅ complete  
> **Reference docs:** `Docs/Game Design/GAMEPLAY_FORMULAS_PROPOSAL.md`, `Docs/Game Design/New Levels.xlsx`

---

## Overview

Club Level Up modal — mirrors the existing Character `LevelUpModalController`. Clubs have SP allocation across 4 stats: Power, Accuracy, Lie Resistance, Durability. Loft is fixed (display only, no SP). Uses same RP cost table (`CharacterLevelUpDatabase`).

**Reference screenshot:** Uploaded Figma image showing A. WEDGE FILOE (Mythic, Lv 120/159) with SP allocation UI.

---

## Key Design Data (from New Levels.xlsx)

### Rarity Level Ranges

| Rarity | Starting Level | Max Level | Levels Gained | Total SP Earned |
|---|---|---|---|---|
| Common | 10 | 39 | 29 | 29 |
| Uncommon | 40 | 79 | 39 | 39 |
| Rare | 80 | 119 | 39 | 39 |
| Mythic | 120 | 159 | 39 | 39 |
| Legendary | 160 | 199 | 39 | 39 |
| Supreme | 200 | 239 | 39 | 39 |

### SP Caps

- **Max SP per individual stat = 20** (flat, same for all rarities)
- 4 allocatable stats: Power, Accuracy, Lie Resistance, Durability
- Loft: fixed (no SP allocation)
- Rarity advantage = more total SP budget (more levels to gain), not higher per-stat caps
- A Common club (29 SP) can max 1 stat and partially fill another
- A Legendary club (39 SP) can almost max 2 stats

### RP Cost Formula

`cost = level × 5` (Level 121 → 605 RP — confirmed by screenshot)

This is already in `CharacterLevelUpDatabase` via `LevelUpCosts.csv`. **Reuse it.**

### SP Reward

1 SP per level-up (same as characters). Already in `CharacterLevelUpDatabase`.

### Stat Display

The UI shows `currentValue/cap` where:
- **Cap** = `baseStat + MAX_SP_PER_STAT(20)`
- **Current value** = `baseStat + spentSP`

### Durability SP

From GAMEPLAY_FORMULAS_PROPOSAL.md:
- SP spent on Durability increases `maxDurability` (the cap), not `currentDurability`
- Per SP point: +5 max durability
- When SP is committed, update `playerClub.maxDurability` directly so existing code works unchanged.

---

## Pre-requisite: Extend PlayerClubData

**File:** `Assets/Scripts/UI/Inventory/ClubData.cs`

Add to `PlayerClubData`:

```csharp
public int totalSPEarned      = 0;
public int spentPower          = 0;
public int spentAccuracy       = 0;
public int spentLieResistance  = 0;
public int spentDurability     = 0;
public const int MAX_SP_PER_STAT = 20;
```

Update `Get{Stat}()` methods:

```csharp
public int GetPower(ClubDataRuntime template)         => template.basePower + spentPower;
public int GetAccuracy(ClubDataRuntime template)      => template.baseAccuracy + spentAccuracy;
public int GetLieResistance(ClubDataRuntime template) => template.baseLieResistance + spentLieResistance;
public int GetLoft(ClubDataRuntime template)          => template.baseLoft;  // fixed
public int GetDistance(ClubDataRuntime template)      => template.baseDistance;
```
---

## Pre-requisite: ClubManager Changes

**File:** `Assets/Scripts/ClubManager.cs`

Add methods:
- `SetLevel(string clubId, int newLevel)` — sets level without RP check. Modal handles payment.
- `RefreshStatValues(string clubId)` — fires `OnClubLeveledUp` to refresh UI.

Update `InitializeClubs()` to seed `totalSPEarned` based on starting level:
```csharp
int totalSP = 0;
for (int lv = 2; lv <= playerClub.currentLevel; lv++)
    totalSP += CharacterLevelUpDatabase.Instance.GetSPReward(lv);
playerClub.totalSPEarned = totalSP;
```

---

## New File: ClubLevelUpModalController.cs

**Path:** `Assets/Scripts/UI/Inventory/ClubLevelUpModalController.cs`
**Namespace:** `Golfin.Inventory`
**Extends:** `Golfin.UI.Modals.ModalController`

Mirror `LevelUpModalController.cs` closely. Key differences:

| Aspect | Character | Club |
|---|---|---|
| Stats with SP | 4 (STR, CC, REC, STAM) | 4 (Power, Accuracy, LieRes, Durability) |
| Fixed stat | None | **Loft** — display only, no + button |
| Stat cap | Rarity-based `RarityStatCaps` | **Flat 20 SP per stat** for all rarities |
| Cap display | `/{rarityStatCap}` | `/{baseStat + 20}` |
| Rarity advantage | Higher stat caps | More total SP (more levels to gain) |
| Durability SP | N/A | Increases `maxDurability` (+5 per SP) |
| Distance | N/A | Not shown in modal |

### Confirm Logic

1. `RewardPointsManager.Instance.Spend(totalRPCost)` — single RP transaction
2. `ClubManager.Instance.SetLevel(clubId, previewLevel)`
3. `playerClub.totalSPEarned = previewTotalSPEarned`
4. `playerClub.spent{Stat} += pending{Stat}` for each stat
5. `playerClub.maxDurability = template.maxDurability + (playerClub.spentDurability * 5)`
6. `ClubManager.Instance.RefreshStatValues(clubId)`

### Available SP Calculation

```csharp
int currentTotalSpent = playerClub.spentPower + playerClub.spentAccuracy
                      + playerClub.spentLieResistance + playerClub.spentDurability;
int totalPending = pendingPower + pendingAccuracy + pendingLieRes + pendingDurability;
int availableSP  = previewTotalSPEarned - currentTotalSpent - totalPending;
```

CONFIRM enabled only when `availableSP == 0 && totalPending > 0`.

---

## Integration Points

### ClubDetailPanel.cs
- Add `[SerializeField] ClubLevelUpModalController? levelUpModal;`
- `OnLevelUpClicked()` → `levelUpModal?.Open(currentClubId, rightPanel);`

### ClubCompareController.cs
- Add `[SerializeField] ClubLevelUpModalController? levelUpModal;`
- `OnRightLevelUpClicked()` → `levelUpModal?.Open(_rightClubId, ...)`
- Subscribe to `OnClubLeveledUp` in `OnEnable/OnDisable` to refresh compare display.

---

## Editor Wiring

**New file:** `Assets/Scripts/UI/Inventory/Editor/ClubLevelUpModalAutoWire.cs`
**MenuItem:** `GOLFIN/Wire/Club Level Up Modal`

---

## Localization Keys

```
CLUB_MODAL_LEVEL_UP,Level Up,レベルアップ
CLUB_MODAL_NEXT_LEVEL,Next Level,次のレベル
CLUB_MODAL_COST,Cost,コスト
CLUB_MODAL_REWARD,Reward,報酬
CLUB_MODAL_SP_SUFFIX,SP,SP
CLUB_MODAL_AVAILABLE_SP,Available SP,利用可能SP
CLUB_MODAL_RESET,Reset,リセット
CLUB_MODAL_CANCEL,Cancel,キャンセル
CLUB_MODAL_CONFIRM,Confirm,確認
CLUB_STAT_POWER,Power,パワー
CLUB_STAT_ACCURACY,Accuracy,精度
CLUB_STAT_LIE_RES,Lie Res.,ライ抵抗
CLUB_STAT_LOFT_FIXED,Loft (Fixed),ロフト（固定）
CLUB_STAT_DURABILITY,Durability,耐久性
CLUB_MODAL_MAX,MAX,MAX
```

---

## Implementation Checklist

- [ ] Extend `PlayerClubData` with SP fields + `MAX_SP_PER_STAT = 20`
- [ ] Update `PlayerClubData.Get{Stat}()` methods to add `spent` values
- [ ] Add `ClubManager.SetLevel()` and `ClubManager.RefreshStatValues()`
- [ ] Update `ClubManager.InitializeClubs()` to seed `totalSPEarned`
- [ ] Create `ClubLevelUpModalController.cs` (mirror character modal, Loft fixed)
- [ ] Update `ClubDetailPanel.OnLevelUpClicked()` to open modal
- [ ] Update `ClubCompareController` — wire level-up to modal, subscribe to `OnClubLeveledUp`
- [ ] Create `ClubLevelUpModalAutoWire.cs` — MenuItem `GOLFIN/Wire/Club Level Up Modal`
- [ ] Add localization keys to CSV
- [ ] Test: preview levels, allocate SP, confirm commits, cancel discards, Loft fixed, per-stat cap = 20