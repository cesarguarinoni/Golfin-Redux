# TellCode.md — Instructions from Claude (Architect) to Claude Code

> Claude Code: Read this file at the start of each task. Execute the latest instruction block.
> After completing, add a status line at the bottom: `✅ DONE: [date] [brief summary]`
> Claude (Architect) will update this file with new instructions as needed.

---

## Current Task (2026-03-25) — Phase E1: Club Level Up Modal

Full spec: `Docs/SPEC_ClubPhaseE1_LevelUpModal.md`
Reference: `Docs/Game Design/GAMEPLAY_FORMULAS_PROPOSAL.md`, `Docs/Game Design/New Levels.xlsx`
Pattern to follow: `Assets/Scripts/UI/Roster/UI/LevelUpModalController.cs` (character version)

Do these sub-tasks in order.

---

### Sub-task 1: Extend PlayerClubData with SP fields

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
public int GetLoft(ClubDataRuntime template)          => template.baseLoft;  // fixed — no SP
public int GetDistance(ClubDataRuntime template)      => template.baseDistance;
```

Durability SP increases maxDurability (+5 per SP point). When SP is committed, update `maxDurability` field directly so existing `IsDurabilityLow` works unchanged.

---

### Sub-task 2: ClubManager additions

**File:** `Assets/Scripts/ClubManager.cs`

Add these methods:
```csharp
public void SetLevel(string clubId, int newLevel)
// Sets level without RP check. Modal handles payment.

public void RefreshStatValues(string clubId)
// Fires OnClubLeveledUp to refresh UI. Stats computed on-the-fly.
```

Update `InitializeClubs()` to seed `totalSPEarned`:
```csharp
// After creating each playerClub, sum SP rewards from Lv 2 → startingLevel:
int totalSP = 0;
for (int lv = 2; lv <= playerClub.currentLevel; lv++)
    totalSP += CharacterLevelUpDatabase.Instance.GetSPReward(lv);
playerClub.totalSPEarned = totalSP;
```

---

### Sub-task 3: Create ClubLevelUpModalController

**New file:** `Assets/Scripts/UI/Inventory/ClubLevelUpModalController.cs`
**Namespace:** `Golfin.Inventory`
**Extends:** `Golfin.UI.Modals.ModalController`

Mirror `LevelUpModalController.cs` closely but with these differences:

| Aspect | Character | Club |
|---|---|---|
| Stats with SP | 4 (STR, CC, REC, STAM) | 4 (Power, Accuracy, LieRes, Durability) |
| Fixed stat | None | **Loft** — show value, no `+` button |
| Stat cap | Rarity-based `RarityStatCaps` | **Flat 20 SP per stat** for all rarities |
| Cap display | `/{rarityStatCap}` | `/{baseStat + 20}` |
| Rarity advantage | Higher per-stat caps | More total SP (more levels to gain) |
| Durability SP | N/A | Increases `maxDurability` (+5 per SP) |
| Distance | N/A | Not shown in modal |

**Key formulas:**
- RP cost to next level: `CharacterLevelUpDatabase.Instance.GetLevelUpCost(nextLevel)` (= nextLevel × 5)
- SP reward per level: `CharacterLevelUpDatabase.Instance.GetSPReward(nextLevel)` (= 1)
- Available SP: `previewTotalSPEarned - currentTotalSpent - totalPending`
- Stat cap display: `template.base{Stat} + MAX_SP_PER_STAT`
- CONFIRM enabled: `availableSP == 0 && totalPending > 0`

**Confirm commits:**
1. `RewardPointsManager.Instance.Spend(totalRPCost)` — single RP transaction
2. `ClubManager.Instance.SetLevel(clubId, previewLevel)`
3. `playerClub.totalSPEarned = previewTotalSPEarned`
4. `playerClub.spent{Stat} += pending{Stat}` for each stat
5. `playerClub.maxDurability = template.maxDurability + (playerClub.spentDurability * 5)`
6. `ClubManager.Instance.RefreshStatValues(clubId)`

**Copy from character modal:**
- `Open()` anchor-panel repositioning logic
- `OnHide()` + `RestorePositionAfterFade()` coroutine
- `UpdateStatRow()` helper (blue bar + orange pending bar behind it)
- Color constants
- Localization pattern

---

### Sub-task 4: Wire into existing panels

**ClubDetailPanel.cs:**
- Add `[SerializeField] ClubLevelUpModalController? levelUpModal;`
- `OnLevelUpClicked()` → `levelUpModal?.Open(currentClubId, rightPanel);`

**ClubCompareController.cs:**
- Add `[SerializeField] ClubLevelUpModalController? levelUpModal;`
- `OnRightLevelUpClicked()` → `levelUpModal?.Open(_rightClubId, compareRightPanel.GetComponent<RectTransform>());`
- Subscribe to `ClubManager.Instance.OnClubLeveledUp` in `OnEnable/OnDisable` to refresh compare when a club is leveled up in the modal.

---

### Sub-task 5: Editor auto-wire

**New file:** `Assets/Scripts/UI/Inventory/Editor/ClubLevelUpModalAutoWire.cs`
**MenuItem:** `GOLFIN/Wire/Club Level Up Modal`

Wire all SerializeFields on `ClubLevelUpModalController`. Also wire `levelUpModal` references on `ClubDetailPanel` and `ClubCompareController`.

---

### Sub-task 6: Localization

Add these keys to the localization CSV:
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

### Reminders
- Read the full spec in `Docs/SPEC_ClubPhaseE1_LevelUpModal.md` before starting
- **Reuse the Roster Level Up modal as much as possible.** The Club Level Up modal is nearly identical to the Character one. Clone the existing `LevelUpModal` hierarchy in Unity (same layout, same images, same button styles, same stat row structure). Only change what's different: swap the 4 character stats for the 5 club stats (Power, Accuracy, Lie Res, Loft fixed, Durability), remove the character-specific fields, and rebind data to ClubManager instead of CharacterManager.
- Mirror `LevelUpModalController.cs` code as closely as possible — same patterns, same code style, same color constants, same animation approach.
- The AutoWire script should clone/duplicate the existing Roster LevelUpModal hierarchy and rewire it, similar to how `ClubCompareRightPanelBuilder` clones the roster compare panel.
- Loft row: display-only, no barPending, no pending label, no plus button
- Per-stat SP cap = 20 (flat, NOT rarity-based)
- Push to GitHub after completing

---

## Completed Tasks

✅ DONE: 2026-03-20 — ScreenshotTool, compress script, CLAUDE.md update
✅ DONE: 2026-03-20 — Phase C code: ClubCarouselController, ClubDetailPanel, builders, auto-wire
✅ DONE: 2026-03-21 — New leveling economy: rarity-based starting/max levels
✅ DONE: 2026-03-23 — TextGradients, visual fixes, filter dividers, arrows, viewport, fade, level text
✅ DONE: 2026-03-25 — Club Compare Phase D: ClubCompareController, builder, auto-wire, stat differences
✅ DONE: 2026-03-24 — Project cleanup: GOLFIN menu reorganized, Art/References folders renamed PascalCase, 5 editor scripts archived
✅ DONE: 2026-03-25 — Phase E1 Club Level Up Modal: PlayerClubData SP fields, ClubManager.SetLevel/RefreshStatValues, ClubLevelUpModalController, ClubDetailPanel/ClubCompareController wired, ClubLevelUpModalAutoWire, localization keys. Pending: Unity hierarchy clone + wire run.
