# TellCode.md — Instructions from Claude (Architect) to Claude Code

> Claude Code: Read this file at the start of each task. Execute the latest instruction block.
> After completing, add a status line at the bottom: `✅ DONE: [date] [brief summary]`
> Claude (Architect) will update this file with new instructions as needed.

---

## Current Task (2026-03-26) — Phase E2: Club Repair Modal

Full spec: `Docs/SPEC_ClubPhaseE2_RepairModal.md`
Pattern to follow: `Assets/Scripts/UI/Inventory/ClubLevelUpModalController.cs` (Phase E1)

Do these sub-tasks in order.

---

### Sub-task 1: Create RepairKitManager Singleton

**New file:** `Assets/Scripts/RepairKitManager.cs`
**No namespace** (matches ClubManager, RewardPointsManager pattern)

Standalone singleton (DontDestroyOnLoad) that manages repair kit inventory:
- `standardKitCount` (starting: 5 for testing), `premiumKitCount` (starting: 2)
- Constants: `STANDARD_RESTORE_PERCENT = 0.5f`, `PREMIUM_RESTORE_PERCENT = 1.0f`, `MAX_STACK = 99`
- Methods: `GetStandardCount()`, `GetPremiumCount()`, `HasAnyKit()`
- `UseStandardKit(currentDurability, maxDurability)` → returns new durability, decrements count
- `UsePremiumKit(maxDurability)` → returns maxDurability, decrements count
- `AddKits(int standard, int premium)` — for mission rewards later
- Event: `OnInventoryChanged`

See full spec for implementation details.

---

### Sub-task 2: Add OnClubRepaired Event + RepairClub Method

**File:** `Assets/Scripts/ClubManager.cs`

Add event:
```csharp
/// <summary>Fired after a club is repaired. Arg = clubId.</summary>
public event System.Action<string>? OnClubRepaired;
```

Add real repair method:
```csharp
public void RepairClub(string clubId, int newDurability)
{
    if (!ownedClubs.TryGetValue(clubId, out var club)) { /* warn + return */ }
    int old = club.currentDurability;
    club.currentDurability = Mathf.Clamp(newDurability, 0, club.maxDurability);
    Debug.Log($"[ClubManager] '{clubId}' repaired: {old} → {club.currentDurability}/{club.maxDurability}");
    OnClubRepaired?.Invoke(clubId);
}
```

Mark the old `Repair(string clubId)` stub as `[System.Obsolete("Use RepairClub(clubId, newDurability)")]`.

---

### Sub-task 3: Create ClubRepairModalController

**New file:** `Assets/Scripts/UI/Inventory/ClubRepairModalController.cs`
**Namespace:** `Golfin.Inventory`
**Extends:** `Golfin.UI.Modals.ModalController`

Simpler than Level Up modal — no SP allocation, no level preview. Just:
1. Show club name, rarity, level, durability bar
2. Kit selection: Standard (×count) or Premium (×count) — toggle buttons
3. Preview bar shows durability after repair (green overlay)
4. CONFIRM: consume kit via RepairKitManager, update club via ClubManager.RepairClub
5. CANCEL: close modal, no changes

**Key fields:**
- Club info: `clubNameText`, `rarityLabel`, `levelText`
- Durability: `durabilityBar` (blue), `durabilityBarPreview` (green), `durabilityValueText`, `durabilityChangeText`
- Kit buttons: `standardKitButton`, `standardKitCountText`, `standardKitSelected`, `premiumKitButton`, `premiumKitCountText`, `premiumKitSelected`
- Messages: `noKitsMessage`
- Actions: `cancelButton`, `confirmButton`

**Confirm enabled when:** kit is selected AND club is not at full durability.

See full spec for complete field list, logic, and color values.

---

### Sub-task 4: Wire into Existing Panels

**ClubDetailPanel.cs:**
- Add `[SerializeField] private ClubRepairModalController? repairModal;`
- Update `OnRepairClicked()` → `repairModal?.Open(currentClubId, rightPanel);`
- Subscribe to `ClubManager.Instance.OnClubRepaired` in `OnEnable/OnDisable` → refresh panel
- In `UpdatePanel()`, set repair button interactable: `needsRepair && hasKits`

**ClubCompareController.cs:**
- Add `[SerializeField] private ClubRepairModalController? repairModal;`
- Update `OnRightRepairClicked()` → `repairModal?.Open(_rightClubId, ...);`
- Subscribe to `OnClubRepaired` in `OnEnable/OnDisable` → refresh compare

---

### Sub-task 5: Editor Auto-Wire + Hierarchy Builder

**New file:** `Assets/Scripts/UI/Inventory/Editor/ClubRepairModalAutoWire.cs`
**MenuItem:** `GOLFIN/Wire/Club Repair Modal`

Build the repair modal hierarchy (simpler than Level Up — just club info + durability bar + kit selector + action buttons). Wire all SerializeFields. Also wire `repairModal` references on ClubDetailPanel and ClubCompareController.

---

### Sub-task 6: RepairKitManager Setup Script

**New file:** `Assets/Scripts/Editor/RepairKitManagerSetup.cs`
**MenuItem:** `GOLFIN/Setup/Repair Kit Manager`

Finds or creates RepairKitManager on the Managers GameObject.

---

### Sub-task 7: Localization

Add these keys to the localization CSV:
```
CLUB_REPAIR_TITLE,Repair,修理
CLUB_REPAIR_DURABILITY,Durability,耐久性
CLUB_REPAIR_STANDARD_KIT,Standard Kit,標準修理キット
CLUB_REPAIR_PREMIUM_KIT,Premium Kit,プレミアム修理キット
CLUB_REPAIR_NO_KITS,You don't own any Repair Kits.,修理キットを持っていません。
CLUB_REPAIR_FULL_DURABILITY,This club is at full durability.,このクラブは最大耐久性です。
CLUB_REPAIR_CANCEL,Cancel,キャンセル
CLUB_REPAIR_CONFIRM,Repair,修理する
CLUB_REPAIR_TOAST,{0} was repaired. Durability {1} → {2}.,{0}が修理されました。耐久性 {1} → {2}。
```

---

### Reminders
- Read the full spec in `Docs/SPEC_ClubPhaseE2_RepairModal.md` before starting
- **Repair uses Repair Kits, NOT RP** — completely separate from the Level Up modal's RP cost
- The modal is simpler than Level Up — no SP allocation rows, no level preview. Just: pick kit → see durability preview → confirm
- RepairKitManager is standalone for now; will integrate with Items system later
- Repair button on ClubDetailPanel/ClubCompareController should be grayed out when: club at full durability OR no kits owned
- Toast system doesn't exist yet — use `Debug.Log` + tag with `// TODO: Toast` for later
- The AutoWire should build a simpler hierarchy than the LevelUp modal
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
✅ SPEC READY: 2026-03-26 — Phase E2 Repair Modal spec written (SPEC_ClubPhaseE2_RepairModal.md)
✅ DONE: 2026-03-26 — Phase E2 code complete: RepairKitManager, ClubManager.RepairClub/OnClubRepaired, ClubRepairModalController, ClubDetailPanel/ClubCompareController updated, ClubRepairModalAutoWire, RepairKitManagerSetup, 9 localization keys. Pending: Unity hierarchy build + wire run.
