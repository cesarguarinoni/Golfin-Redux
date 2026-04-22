# SPEC — Phase E3: Bag Selection Modal

> Status: READY FOR IMPLEMENTATION
> Author: Claude (Architect)
> Date: 2026-03-26

---

## Overview

When the player taps **EQUIP** on a club (from ClubDetailPanel or ClubCompareController),
a modal appears showing a grid of bag slots. The player picks which bag to assign the club to.

- **1 bag unlocked** at start (Bag 1). Remaining 9 slots show "LOCKED".
- Each bag holds **MAX_CLUBS_PER_BAG = 8** clubs.
- If a bag is **FULL** (8/8), tapping it shows an error toast — the club is NOT added.
- **CANCEL** button closes the modal without changes.
- A club can only be in **one bag** at a time (equipping to a new bag removes it from the old one).

Reference: `Bags_Selection_Screen.png` (project knowledge)

### Assets
- **Bag thumbnails:** `Resources/Bags/Thumbnail/{BagName}.png` — each bag has its own portrait (only `Mireo.png` exists for now; use it as fallback for missing portraits)
  - Displayed on a **rarity-colored background** (same as club/character portraits)
  - **Rarity letter badge** at top-left corner (e.g. "C", "R", "L") — matches club portrait pattern
  - NOTE: The mockup is missing the rarity letter, but it must be there for UI consistency
- **Locked slots:** No special asset needed — dim/greyed slot with "LOCKED" text
- **FULL badge:** Text label overlay, no special sprite
- **Equipped checkmark:** Reuse existing equipped icon pattern

---

## Constants

```
MAX_BAGS = 10           // total bag slots (grid: 5 columns × 2 rows)
MAX_CLUBS_PER_BAG = 8   // clubs per bag
STARTING_UNLOCKED = 1   // bags unlocked at start
```

---

## Sub-task 1: BagManager Singleton

**New file:** `Assets/Scripts/BagManager.cs`
**No namespace** (matches ClubManager, RepairKitManager pattern)

```csharp
public class BagManager : MonoBehaviour
{
    public static BagManager Instance { get; private set; }

    public const int MAX_BAGS = 10;
    public const int MAX_CLUBS_PER_BAG = 8;

    /// <summary>Fired when bag contents change. Arg = bagSlot.</summary>
    public event System.Action<int>? OnBagChanged;

    private int unlockedBags = 1;  // start with 1

    // ── Query API ──
    public bool IsBagUnlocked(int bagSlot)  // bagSlot 1-based
    public int  GetClubCountInBag(int bagSlot)
    public bool IsBagFull(int bagSlot)
    public List<PlayerClubData> GetClubsInBag(int bagSlot)
    public int  GetUnlockedBagCount()

    // ── Mutate ──
    /// <summary>
    /// Assigns club to bagSlot. If club is already in another bag, removes it first.
    /// Returns true on success, false if bag is full or locked.
    /// </summary>
    public bool AssignClubToBag(string clubId, int bagSlot)

    /// <summary>Removes club from whatever bag it's in. Sets equippedBagSlot = 0.</summary>
    public void RemoveClubFromBag(string clubId)

    /// <summary>Unlocks the next bag (for future shop/progression).</summary>
    public void UnlockNextBag()
}
```

### Implementation Notes for BagManager

- `AssignClubToBag()` flow:
  1. Check `IsBagUnlocked(bagSlot)` → false → return false
  2. Check `IsBagFull(bagSlot)` → true → return false
  3. If club already in a bag, remove it first (`RemoveClubFromBag`)
  4. `ClubManager.Instance.EquipClub(clubId, bagSlot)` — sets `equippedBagSlot`
  5. Fire `OnBagChanged?.Invoke(bagSlot)`
  6. Return true

- `GetClubsInBag()` queries `ClubManager.Instance.GetAllOwnedClubs()` and filters by `equippedBagSlot == bagSlot`

- `GetClubCountInBag()` = `GetClubsInBag(bagSlot).Count`

- No separate data store needed — `PlayerClubData.equippedBagSlot` is the source of truth.

---

## Sub-task 2: BagSelectionModalController

**New file:** `Assets/Scripts/UI/Inventory/BagSelectionModalController.cs`
**Namespace:** `Golfin.Inventory`
**Extends:** `Golfin.UI.Modals.ModalController`

### UI Hierarchy (to be cloned from existing modal pattern)

```
BagSelectionModal (GameObject, inactive by default)
├── Backdrop (Image, dark overlay)
├── ModalPanel
│   ├── Title ("CHOOSE A BAG")
│   ├── BagGrid (GridLayoutGroup, 5 columns, 2 rows)
│   │   ├── BagSlot_1 ... BagSlot_10
│   │   │   ├── BagImage (Image — bag thumbnail or locked icon)
│   │   │   ├── BagLabel ("BAG 1" / "LOCKED")
│   │   │   ├── CountLabel ("3/8" or hidden when locked)
│   │   │   ├── FullBadge ("FULL" badge, shown when 8/8)
│   │   │   └── EquippedIcon (checkmark, shown if this club is already in this bag)
│   │   └── ...
│   └── CancelButton ("CANCEL")
```

### SerializeFields

```csharp
[Header("Bag Grid")]
[SerializeField] private Transform bagGridParent = null!;       // GridLayoutGroup container
[SerializeField] private GameObject bagSlotPrefab = null!;       // slot template (disable after clone)

[Header("Cancel")]
[SerializeField] private Button cancelButton = null!;
```

### Public API

```csharp
/// <summary>
/// Opens the modal for a specific club. Called from ClubDetailPanel/ClubCompareController.
/// </summary>
public void Open(string clubId)
```

### Slot Rendering

Each slot is a prefab instance with child references found by name:
- `BagImage`, `BagLabel`, `CountLabel`, `FullBadge`, `EquippedIcon`

For each slot (1 to MAX_BAGS):
- **Locked:** dim appearance, `BagLabel = "LOCKED"`, no click handler
- **Unlocked + not full:** `BagLabel = "BAG {n}"`, `CountLabel = "{count}/{MAX}"`, clickable
- **Unlocked + full:** same but `FullBadge` visible, still clickable (shows toast on tap)
- **Club already here:** `EquippedIcon` visible (checkmark)

### OnSlotClicked(int bagSlot)

```
1. if bag is full:
     Debug.Log("Bag {n} is full (8/8). Remove a club first.")  // TODO: Toast
     return
2. BagManager.Instance.AssignClubToBag(currentClubId, bagSlot)
3. Hide()                     // close modal
4. // ClubDetailPanel refreshes via OnClubEquipped event
```

---

## Sub-task 3: Wire Equip Button to Bag Selection Modal

### ClubDetailPanel.cs changes

**File:** `Assets/Scripts/UI/Inventory/ClubDetailPanel.cs`

Add SerializeField:
```csharp
[Header("Bag Selection")]
[SerializeField] private BagSelectionModalController? bagSelectionModal;
```

Change `OnEquipClicked()`:
```csharp
private void OnEquipClicked()
{
    if (string.IsNullOrEmpty(currentClubId) || ClubManager.Instance == null) return;
    var playerClub = ClubManager.Instance.GetClubData(currentClubId);
    if (playerClub == null) return;

    if (playerClub.IsEquipped)
    {
        // Already equipped → unequip (remove from bag)
        BagManager.Instance?.RemoveClubFromBag(currentClubId);
    }
    else
    {
        // Not equipped → open bag selection modal
        if (bagSelectionModal != null)
            bagSelectionModal.Open(currentClubId);
        else
            Debug.Log("[ClubDetailPanel] EQUIP clicked — wire BagSelectionModal.");
    }
}
```

### ClubCompareController.cs changes

**File:** `Assets/Scripts/UI/Inventory/ClubCompareController.cs`

Same pattern — add `bagSelectionModal` SerializeField.
Update the right-panel equip button handler to open `bagSelectionModal.Open(rightClubId)` instead of direct `EquipClub()`.

---

## Sub-task 4: BagSelectionModalAutoWire (Editor Script)

**New file:** `Assets/Scripts/Editor/BagSelectionModalAutoWire.cs`
**MenuItem:** `GOLFIN/Wire/Bag Selection Modal`

Creates the modal hierarchy under the ClubsScreen Canvas:
1. Creates BagSelectionModal GameObject with ModalController setup
2. Creates backdrop, modal panel, title, grid, cancel button
3. Creates a BagSlotPrefab template with BagImage, BagLabel, CountLabel, FullBadge, EquippedIcon
4. Wires all SerializeFields on BagSelectionModalController
5. Wires `bagSelectionModal` reference on ClubDetailPanel and ClubCompareController

---

## Sub-task 5: BagManagerSetup (Editor Script)

**New file:** `Assets/Scripts/Editor/BagManagerSetup.cs`
**MenuItem:** `GOLFIN/Setup/Bag Manager`

Finds or creates BagManager on the Managers GameObject.

---

## Sub-task 6: Localization

Add to localization CSV:
```
BAG_CHOOSE_TITLE,Choose a Bag,バッグを選択
BAG_LOCKED,Locked,ロック
BAG_FULL_TOAST,Bag {0} is full ({1}/{1}). Remove a club first.,バッグ{0}は満杯です（{1}/{1}）。先にクラブを外してください。
BAG_EQUIPPED_TOAST,{0} equipped to Bag {1}.,{0}をバッグ{1}に装備しました。
BAG_UNEQUIPPED_TOAST,{0} removed from Bag {1}.,{0}をバッグ{1}から外しました。
```

---

## Execution Order

1. BagManager singleton (Sub-task 1)
2. BagSelectionModalController (Sub-task 2)
3. Wire Equip buttons (Sub-task 3)
4. AutoWire editor script (Sub-task 4)
5. BagManagerSetup editor script (Sub-task 5)
6. Localization keys (Sub-task 6)

---

## Reminders

- `PlayerClubData.equippedBagSlot` already exists and is the source of truth
- `ClubManager.EquipClub(clubId, bagSlot)` already handles unequipping the previous club in that slot — but BagManager adds the "is bag full?" guard
- Toast system doesn't exist yet — use `Debug.Log` + `// TODO: Toast`
- Grid is 5×2 = 10 slots; only slot 1 is unlocked at start
- The modal does NOT show bag contents — just slot availability. Bag contents screen is a future task.
- Push to GitHub after completing
