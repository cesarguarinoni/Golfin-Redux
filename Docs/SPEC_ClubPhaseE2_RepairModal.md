# SPEC: Phase E2 — Club Repair Modal

> **Status:** Ready for Implementation
> **Author:** Claude (Architect)
> **Date:** 2026-03-26
> **Pattern:** Mirrors `ClubLevelUpModalController` (Phase E1)
> **Prereq:** Phase E1 complete ✅

---

## Overview

When the player taps REPAIR on a club (from ClubDetailPanel or ClubCompareController), a modal appears showing the club's current durability, a dropdown/selector for Repair Kit type, and a CONFIRM button. Repair Kits are consumable items — **no RP cost**.

### Design Reference (Confluence)
- **Standard Repair Kit 🛠️** — Restores 50% of `maxDurability`
- **Premium Repair Kit ⭐** — Restores 100% of `maxDurability`
- Kits stack up to 99 per type
- Consumable: used once then removed from inventory
- Toast: "The [Club Name] was repaired. Durability [Old] → [New]."

### Scope Decision — No Items Screen Yet

The full Confluence design has a separate "Repair Kit Selection" screen (Inventory: Items tab). Since the Items system (G-016) is **not started**, this spec implements repair as a **self-contained modal** that manages repair kit inventory internally via `RepairKitManager` singleton. When the Items screen is built later, it will read from the same `RepairKitManager`.

---

## Sub-task 1: RepairKitManager Singleton

**New file:** `Assets/Scripts/RepairKitManager.cs`
**No namespace** (matches ClubManager, RewardPointsManager pattern)

```csharp
public class RepairKitManager : MonoBehaviour
{
    public static RepairKitManager Instance { get; private set; }

    public event System.Action? OnInventoryChanged;

    // Inventory — simple int counts
    private int standardKitCount = 5;  // starting amount for testing
    private int premiumKitCount  = 2;

    // Config — adjustable for balancing (per Confluence: "values should be adjustable")
    public const float STANDARD_RESTORE_PERCENT = 0.5f;   // 50%
    public const float PREMIUM_RESTORE_PERCENT  = 1.0f;   // 100%
    public const int   MAX_STACK = 99;

    // Awake: singleton pattern (DontDestroyOnLoad)

    public int GetStandardCount() => standardKitCount;
    public int GetPremiumCount()  => premiumKitCount;
    public bool HasAnyKit()       => standardKitCount > 0 || premiumKitCount > 0;

    /// <summary>
    /// Use a Standard kit. Returns durability restored. Caller handles club update.
    /// </summary>
    public int UseStandardKit(int currentDurability, int maxDurability)
    {
        if (standardKitCount <= 0) return 0;
        standardKitCount--;
        int restored = Mathf.CeilToInt(maxDurability * STANDARD_RESTORE_PERCENT);
        int newDurability = Mathf.Min(currentDurability + restored, maxDurability);
        OnInventoryChanged?.Invoke();
        return newDurability;
    }

    /// <summary>
    /// Use a Premium kit. Returns full maxDurability.
    /// </summary>
    public int UsePremiumKit(int maxDurability)
    {
        if (premiumKitCount <= 0) return 0;
        premiumKitCount--;
        OnInventoryChanged?.Invoke();
        return maxDurability;
    }

    /// <summary>Add kits (from mission rewards, etc).</summary>
    public void AddKits(int standard, int premium)
    {
        standardKitCount = Mathf.Min(standardKitCount + standard, MAX_STACK);
        premiumKitCount  = Mathf.Min(premiumKitCount  + premium,  MAX_STACK);
        OnInventoryChanged?.Invoke();
    }
}
```

**Attach to:** Managers GameObject (same as ClubManager, RewardPointsManager).

---

## Sub-task 2: Add `OnClubRepaired` Event to ClubManager

**File:** `Assets/Scripts/ClubManager.cs`

Add event alongside existing ones:
```csharp
/// <summary>Fired after a club is repaired. Arg = clubId.</summary>
public event System.Action<string>? OnClubRepaired;
```

Replace the `Repair()` stub with a real method:
```csharp
/// <summary>
/// Repairs a club to the given newDurability value.
/// Called by ClubRepairModalController after kit consumption.
/// </summary>
public void RepairClub(string clubId, int newDurability)
{
    if (!ownedClubs.TryGetValue(clubId, out var club))
    {
        Debug.LogWarning($"[ClubManager] RepairClub: club '{clubId}' not found.");
        return;
    }

    int oldDurability = club.currentDurability;
    club.currentDurability = Mathf.Clamp(newDurability, 0, club.maxDurability);

    Debug.Log($"[ClubManager] '{clubId}' repaired: {oldDurability} → {club.currentDurability}/{club.maxDurability}");
    OnClubRepaired?.Invoke(clubId);
}
```

Keep the old `Repair(string clubId)` method but mark it `[System.Obsolete("Use RepairClub(clubId, newDurability)")]` so existing callers still compile.

---

## Sub-task 3: Create ClubRepairModalController

**New file:** `Assets/Scripts/UI/Inventory/ClubRepairModalController.cs`
**Namespace:** `Golfin.Inventory`
**Extends:** `Golfin.UI.Modals.ModalController`

### UI Fields (SerializeField)

```
[Header("Club Info")]
clubNameText          : TextMeshProUGUI  — club name
rarityLabel           : TextMeshProUGUI  — rarity name + color
levelText             : TextMeshProUGUI  — "Lv 80/119"

[Header("Durability Display")]
durabilityLabel       : TextMeshProUGUI  — "DURABILITY" label
durabilityBar         : Image            — current durability fill (blue)
durabilityBarPreview  : Image            — preview after repair fill (green)
durabilityValueText   : TextMeshProUGUI  — "55/100"
durabilityChangeText  : TextMeshProUGUI  — "+45" green preview text

[Header("Kit Selection")]
standardKitButton     : Button           — select Standard Kit
standardKitLabel      : TextMeshProUGUI  — "Standard Kit"
standardKitCountText  : TextMeshProUGUI  — "×5"
standardKitSelected   : GameObject       — highlight/outline when selected
premiumKitButton      : Button           — select Premium Kit
premiumKitLabel       : TextMeshProUGUI  — "Premium Kit"
premiumKitCountText   : TextMeshProUGUI  — "×2"
premiumKitSelected    : GameObject       — highlight/outline when selected
noKitsMessage         : TextMeshProUGUI  — "You don't own any Repair Kits."

[Header("Actions")]
cancelButton          : Button
confirmButton         : Button

[Header("Static Labels")]
cancelButtonLabel     : TextMeshProUGUI
confirmButtonLabel    : TextMeshProUGUI
```

### State

```csharp
private string clubId = "";
private enum KitType { None, Standard, Premium }
private KitType selectedKit = KitType.None;
```

### Key Logic

**`Open(string clubId, RectTransform? anchorPanel = null)`**
- Store clubId
- Reset selectedKit to None
- If `RepairKitManager.Instance.HasAnyKit()` is false, show `noKitsMessage`, hide kit buttons, disable confirm
- If club is already at full durability, disable confirm and show message
- Auto-select Standard if available, else Premium
- Call RefreshDisplay() + Show()

**`RefreshDisplay()`**
- Show club name, rarity (with color via `RarityHelper`), level
- Durability bar: blue fill = current/max
- Preview bar: green fill = preview/max (only shown when a kit is selected)
- Change text: "+N" in green (where N = previewDurability - currentDurability)
- Standard kit: show count, disable if count == 0
- Premium kit: show count, disable if count == 0
- Confirm: enabled only if a kit is selected AND club is not at full durability

**`OnStandardKitSelected()` / `OnPremiumKitSelected()`**
- Set selectedKit
- Toggle `standardKitSelected` / `premiumKitSelected` GameObjects
- Compute preview durability:
  - Standard: `min(current + ceil(max * 0.5), max)`
  - Premium: `max`
- RefreshDisplay()

**`OnConfirmClicked()`**
1. Call `RepairKitManager.Instance.UseStandardKit()` or `UsePremiumKit()`
2. Call `ClubManager.Instance.RepairClub(clubId, newDurability)`
3. Log: `"[ClubRepairModal] Repaired '{clubId}': {old} → {new}/{max} using {kitType}"`
4. Hide()

**`OnCancelClicked()`**
- Hide()

### Colors
- Reuse same color constants as ClubLevelUpModalController where applicable
- Durability bar: blue (existing `DurabilityOkColor` from ClubDetailPanel)
- Preview bar: green `new Color(0.2f, 0.8f, 0.2f, 0.6f)` — translucent green
- Change text: `new Color(0.2f, 0.8f, 0.2f, 1f)` — solid green
- "No kits" message: `Color.gray`

---

## Sub-task 4: Wire into Existing Panels

### ClubDetailPanel.cs

Add field:
```csharp
[SerializeField] private ClubRepairModalController? repairModal;
```

Update `OnRepairClicked()`:
```csharp
private void OnRepairClicked()
{
    if (repairModal != null)
        repairModal.Open(currentClubId, rightPanel);
    else
        Debug.Log($"[ClubDetailPanel] REPAIR clicked for '{currentClubId}' — wire ClubRepairModal.");
}
```

Subscribe to `OnClubRepaired` in `OnEnable`/`OnDisable` to refresh the panel when repair completes:
```csharp
// OnEnable:
if (ClubManager.Instance != null) ClubManager.Instance.OnClubRepaired += OnClubRepaired;

// OnDisable:
if (ClubManager.Instance != null) ClubManager.Instance.OnClubRepaired -= OnClubRepaired;

private void OnClubRepaired(string repairedClubId)
{
    if (repairedClubId == currentClubId) UpdatePanel(currentClubId);
}
```

Also update repair button interactable state in `UpdatePanel()`:
```csharp
// After durability display logic:
bool needsRepair = playerClub.currentDurability < playerClub.maxDurability;
bool hasKits = RepairKitManager.Instance != null && RepairKitManager.Instance.HasAnyKit();
if (repairButton != null) repairButton.interactable = needsRepair && hasKits;
```

### ClubCompareController.cs

Add field:
```csharp
[SerializeField] private ClubRepairModalController? repairModal;
```

Update `OnRightRepairClicked()`:
```csharp
private void OnRightRepairClicked()
{
    if (string.IsNullOrEmpty(_rightClubId)) return;
    if (repairModal != null)
        repairModal.Open(_rightClubId, compareRightPanel.GetComponent<RectTransform>());
    else
        Debug.Log($"[ClubCompareController] Repair clicked for '{_rightClubId}' — wire ClubRepairModal.");
}
```

Subscribe to `OnClubRepaired` in `OnEnable`/`OnDisable` to refresh compare panel.

---

## Sub-task 5: Editor Auto-Wire

**New file:** `Assets/Scripts/UI/Inventory/Editor/ClubRepairModalAutoWire.cs`
**MenuItem:** `GOLFIN/Wire/Club Repair Modal`

Wire all SerializeFields on `ClubRepairModalController`.
Also wire `repairModal` references on `ClubDetailPanel` and `ClubCompareController`.

Pattern: clone the existing `ClubLevelUpModal` hierarchy in Unity, strip SP allocation rows, replace with kit selection UI. The AutoWire script should build the modal hierarchy if it doesn't exist, similar to `ClubLevelUpModalAutoWire`.

---

## Sub-task 6: Localization

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

## Sub-task 7: RepairKitManager Singleton Setup

**Editor script:** `Assets/Scripts/Editor/RepairKitManagerSetup.cs`
**MenuItem:** `GOLFIN/Setup/Repair Kit Manager`

Finds or creates `RepairKitManager` on the Managers GameObject (same one that has ClubManager, RewardPointsManager, etc).

---

## Reminders

- **Repair uses Repair Kits, NOT RP** — completely separate currency
- Modal is simpler than Level Up — no SP allocation, no level preview. Just: pick kit → see preview → confirm
- `OnClubRepaired` event is critical — both ClubDetailPanel and ClubCompareController need to refresh durability display
- RepairKitManager is a temporary standalone singleton. When Items system is built, it will either be refactored into ItemManager or become a facade over it
- The AutoWire script should build a simpler modal hierarchy than the LevelUp one (no stat rows, just durability bar + kit selector)
- Repair button should be grayed out when: club is at full durability OR player has no repair kits
- Toast notification system doesn't exist yet — just use `Debug.Log` for now, tag with `// TODO: Toast` for later
- Push to GitHub after completing
