# TellCode.md — Instructions from Claude (Architect) to Claude Code

> Claude Code: Read this file at the start of each task. Execute the latest instruction block.
> After completing, add a status line at the bottom: `✅ DONE: [date] [brief summary]`
> Claude (Architect) will update this file with new instructions as needed.

---

## Current Task (2026-03-26) — Phase E3: Bag Selection Modal

Full spec: `Docs/SPEC_ClubPhaseE3_BagSelection.md`

When the player taps **EQUIP** on a club, a modal appears with a 5×2 grid of bag slots.
1 bag unlocked at start, 9 locked. Each bag holds max 8 clubs.
If a bag is full → toast error, block. CANCEL closes without changes.

Do these sub-tasks in order.

---

### Sub-task 1: Create BagManager Singleton

**New file:** `Assets/Scripts/BagManager.cs`
**No namespace** (matches ClubManager, RepairKitManager pattern)

Standalone singleton (DontDestroyOnLoad):
- Constants: `MAX_BAGS = 10`, `MAX_CLUBS_PER_BAG = 8`
- `unlockedBags = 1` (private field)
- Event: `public event System.Action<int>? OnBagChanged` (arg = bagSlot)

Query API:
- `IsBagUnlocked(int bagSlot)` — 1-based, returns `bagSlot <= unlockedBags`
- `GetClubCountInBag(int bagSlot)` — queries ClubManager for clubs with `equippedBagSlot == bagSlot`
- `IsBagFull(int bagSlot)` — `GetClubCountInBag(bagSlot) >= MAX_CLUBS_PER_BAG`
- `GetClubsInBag(int bagSlot)` — returns `List<PlayerClubData>` from ClubManager
- `GetUnlockedBagCount()` — returns `unlockedBags`

Mutate API:
- **`AssignClubToBag(string clubId, int bagSlot)`** → returns bool
  1. Check `IsBagUnlocked` → false → return false
  2. Check `IsBagFull` → true → return false
  3. Call `ClubManager.Instance.EquipClub(clubId, bagSlot)`
  4. Fire `OnBagChanged?.Invoke(bagSlot)`
  5. Return true
- `RemoveClubFromBag(string clubId)` — sets `equippedBagSlot = 0` via `ClubManager.Instance.EquipClub(clubId, 0)`
- `UnlockNextBag()` — increments `unlockedBags` (capped at MAX_BAGS), for future use

**Important:** No separate data store. `PlayerClubData.equippedBagSlot` is the source of truth.

---

### Sub-task 2: Create BagSelectionModalController

**New file:** `Assets/Scripts/UI/Inventory/BagSelectionModalController.cs`
**Namespace:** `Golfin.Inventory`
**Extends:** `Golfin.UI.Modals.ModalController`

SerializeFields:
```csharp
[Header("Bag Grid")]
[SerializeField] private Transform bagGridParent = null!;
[SerializeField] private GameObject bagSlotPrefab = null!;

[Header("Cancel")]
[SerializeField] private Button cancelButton = null!;
```

Public API:
```csharp
public void Open(string clubId)
```
- Stores `currentClubId`
- Destroys old slot instances, instantiates 10 new ones from prefab
- For each slot (1–10): configure appearance based on locked/unlocked/full/equipped state
- Calls `Show()` (inherited from ModalController)

Each slot instance children (found by name):
- `BagImage` (Image), `BagLabel` (TMP), `CountLabel` (TMP), `FullBadge` (GameObject), `EquippedIcon` (GameObject)

Slot states:
- **Locked:** dim alpha, label = "LOCKED", no click
- **Unlocked, not full:** label = "BAG {n}", count = "{x}/8", clickable
- **Unlocked, full:** same + FullBadge visible, clickable (shows toast)
- **Club already in this bag:** EquippedIcon visible

`OnSlotClicked(int bagSlot)`:
1. If full → `Debug.Log($"Bag {bagSlot} is full (8/8).")` // TODO: Toast → return
2. `BagManager.Instance.AssignClubToBag(currentClubId, bagSlot)`
3. `Hide()` (closes modal, ClubDetailPanel refreshes via OnClubEquipped event)

Wire `cancelButton` → `Hide()` in `Awake` or `Start`.

---

### Sub-task 3: Wire Equip Buttons to Bag Selection Modal

**File:** `Assets/Scripts/UI/Inventory/ClubDetailPanel.cs`

Add SerializeField:
```csharp
[Header("Bag Selection")]
[SerializeField] private BagSelectionModalController? bagSelectionModal;
```

Replace `OnEquipClicked()`:
```csharp
private void OnEquipClicked()
{
    if (string.IsNullOrEmpty(currentClubId) || ClubManager.Instance == null) return;
    var playerClub = ClubManager.Instance.GetClubData(currentClubId);
    if (playerClub == null) return;

    if (playerClub.IsEquipped)
    {
        // Unequip — remove from bag
        BagManager.Instance?.RemoveClubFromBag(currentClubId);
    }
    else
    {
        // Open bag selection modal
        if (bagSelectionModal != null)
            bagSelectionModal.Open(currentClubId);
        else
            Debug.Log("[ClubDetailPanel] EQUIP clicked — wire BagSelectionModal.");
    }
}
```

**File:** `Assets/Scripts/UI/Inventory/ClubCompareController.cs`

Same pattern — add `bagSelectionModal` SerializeField.
Update the right-panel equip button to open `bagSelectionModal.Open(rightClubId)` when not equipped,
or `BagManager.Instance.RemoveClubFromBag(rightClubId)` when already equipped.

---

### Sub-task 4: BagSelectionModalAutoWire (Editor Script)

**New file:** `Assets/Scripts/Editor/BagSelectionModalAutoWire.cs`
**MenuItem:** `GOLFIN/Wire/Bag Selection Modal`

Creates modal hierarchy under ClubsScreen Canvas:
1. BagSelectionModal GameObject with BagSelectionModalController
2. Backdrop (dark overlay Image)
3. ModalPanel with CanvasGroup
4. Title TMP ("CHOOSE A BAG")
5. BagGrid (GridLayoutGroup, 5 cols, cell ~130×130, spacing 8)
6. BagSlotPrefab template with children: BagImage, BagLabel, CountLabel, FullBadge, EquippedIcon
7. CancelButton
8. Wires all SerializeFields on BagSelectionModalController
9. Wires `bagSelectionModal` on ClubDetailPanel and ClubCompareController

---

### Sub-task 5: BagManagerSetup (Editor Script)

**New file:** `Assets/Scripts/Editor/BagManagerSetup.cs`
**MenuItem:** `GOLFIN/Setup/Bag Manager`

Finds or creates BagManager on the Managers GameObject.

---

### Sub-task 6: Localization

Add to the localization CSV:
```
BAG_CHOOSE_TITLE,Choose a Bag,バッグを選択
BAG_LOCKED,Locked,ロック
BAG_FULL_TOAST,Bag {0} is full ({1}/{1}). Remove a club first.,バッグ{0}は満杯です（{1}/{1}）。先にクラブを外してください。
BAG_EQUIPPED_TOAST,{0} equipped to Bag {1}.,{0}をバッグ{1}に装備しました。
BAG_UNEQUIPPED_TOAST,{0} removed from Bag {1}.,{0}をバッグ{1}から外しました。
```

---

### Reminders
- Read the full spec in `Docs/SPEC_ClubPhaseE3_BagSelection.md` before starting
- `PlayerClubData.equippedBagSlot` already exists — BagManager is a convenience layer on top
- `ClubManager.EquipClub(clubId, bagSlot)` already handles unequipping previous club in that slot
- BagManager adds the "is bag full?" and "is bag unlocked?" guards
- Toast system doesn't exist yet — `Debug.Log` + `// TODO: Toast`
- Grid = 5×2 = 10 slots; only slot 1 unlocked at start
- Modal does NOT show bag contents — just slot availability
- **Bag thumbnails:** `Resources/Bags/Thumbnail/{BagName}.png` — each bag has its own portrait; only `Mireo.png` exists now, use as fallback for missing
- **Rarity letter badge** at top-left of bag portrait — same as club/character portraits (mockup is missing it but must be there for consistency)
- Bag portrait sits on a **rarity-colored background** like club portraits
- Push to GitHub after completing

---

## Completed Tasks

✅ DONE: 2026-03-20 — ScreenshotTool, compress script, CLAUDE.md update
✅ DONE: 2026-03-20 — Phase C code: ClubCarouselController, ClubDetailPanel, builders, auto-wire
✅ DONE: 2026-03-21 — New leveling economy: rarity-based starting/max levels
✅ DONE: 2026-03-23 — TextGradients, visual fixes, filter dividers, arrows, viewport, fade, level text
✅ DONE: 2026-03-25 — Club Compare Phase D: ClubCompareController, builder, auto-wire, stat differences
✅ DONE: 2026-03-24 — Project cleanup: GOLFIN menu reorganized, Art/References folders renamed PascalCase, 5 editor scripts archived
✅ DONE: 2026-03-25 — Phase E1 Club Level Up Modal: PlayerClubData SP fields, ClubManager.SetLevel/RefreshStatValues, ClubLevelUpModalController, ClubDetailPanel/ClubCompareController wired, ClubLevelUpModalAutoWire, localization keys.
✅ DONE: 2026-03-26 — Phase E2 Club Repair One-Tap: RepairKitManager singleton, ClubManager.RepairClub/OnClubRepaired, ClubDetailPanel+ClubCompareController one-tap repair, localization keys, cleanup old modal files.
