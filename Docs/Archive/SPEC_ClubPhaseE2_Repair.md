# SPEC: Phase E2 — Club Repair (One-Tap, No Modal)

> **Status:** Ready for Implementation
> **Author:** Claude (Architect)
> **Date:** 2026-03-26
> **Revision:** v2 — simplified from modal to one-tap auto-repair

---

## Overview

When the player taps REPAIR on a club, the system automatically picks the best Repair Kit from inventory and applies it. No modal, no kit selection — just instant repair with a Debug.Log toast message.

### Kit Selection Logic

1. Calculate `missingPercent = 1 - (currentDurability / maxDurability)`
2. If `missingPercent <= 0.5` AND player has a Standard Kit → use Standard (restores 50% of max)
3. Else if player has a Premium Kit → use Premium (restores 100% of max)
4. Else if player has a Standard Kit → use Standard (better than nothing)
5. Else → no kits available, log warning

**Why this order:** Standard kits restore 50%, so if you're only missing ≤50% durability, using a Premium would be wasteful. Use Standard for small repairs, Premium for big ones. Fall back to Standard if no Premium available.

### Design Reference (Confluence)
- **Standard Repair Kit 🛠️** — Restores 50% of `maxDurability`
- **Premium Repair Kit ⭐** — Restores 100% of `maxDurability`
- Kits stack up to 99 per type
- Consumable: used once then removed from inventory
- Toast: "The [Club Name] was repaired. Durability [Old] → [New]."

---

## Sub-task 1: RepairKitManager Singleton

**New file:** `Assets/Scripts/RepairKitManager.cs`
**No namespace** (matches ClubManager, RewardPointsManager pattern)

```csharp
public class RepairKitManager : MonoBehaviour
{
    public static RepairKitManager Instance { get; private set; }

    public event System.Action? OnInventoryChanged;

    private int standardKitCount = 5;  // starting amount for testing
    private int premiumKitCount  = 2;

    public const float STANDARD_RESTORE_PERCENT = 0.5f;
    public const float PREMIUM_RESTORE_PERCENT  = 1.0f;
    public const int   MAX_STACK = 99;

    // Awake: singleton + DontDestroyOnLoad

    public int  GetStandardCount() => standardKitCount;
    public int  GetPremiumCount()  => premiumKitCount;
    public bool HasAnyKit()        => standardKitCount > 0 || premiumKitCount > 0;

    public enum KitType { None, Standard, Premium }

    /// <summary>
    /// Picks the best kit for the situation and uses it.
    /// Returns (newDurability, kitUsed). Returns (currentDurability, None) if no kit available.
    /// </summary>
    public (int newDurability, KitType kitUsed) UsebestKit(int currentDurability, int maxDurability)
    {
        if (currentDurability >= maxDurability)
            return (currentDurability, KitType.None);

        float missingPercent = 1f - (float)currentDurability / maxDurability;
        KitType chosen = ChooseKit(missingPercent);

        if (chosen == KitType.None)
            return (currentDurability, KitType.None);

        int newDurability;
        if (chosen == KitType.Standard)
        {
            standardKitCount--;
            int restored = Mathf.CeilToInt(maxDurability * STANDARD_RESTORE_PERCENT);
            newDurability = Mathf.Min(currentDurability + restored, maxDurability);
        }
        else // Premium
        {
            premiumKitCount--;
            newDurability = maxDurability;
        }

        OnInventoryChanged?.Invoke();
        return (newDurability, chosen);
    }

    private KitType ChooseKit(float missingPercent)
    {
        // Small repair (≤50% missing) → prefer Standard
        if (missingPercent <= 0.5f && standardKitCount > 0)
            return KitType.Standard;
        // Big repair (>50% missing) → prefer Premium
        if (premiumKitCount > 0)
            return KitType.Premium;
        // Fallback: Standard is better than nothing
        if (standardKitCount > 0)
            return KitType.Standard;
        return KitType.None;
    }

    public void AddKits(int standard, int premium)
    {
        standardKitCount = Mathf.Min(standardKitCount + standard, MAX_STACK);
        premiumKitCount  = Mathf.Min(premiumKitCount  + premium,  MAX_STACK);
        OnInventoryChanged?.Invoke();
    }
}
```

---

## Sub-task 2: Add OnClubRepaired Event + RepairClub Method to ClubManager

**File:** `Assets/Scripts/ClubManager.cs`

Add event:
```csharp
public event System.Action<string>? OnClubRepaired;
```

Replace the `Repair()` stub with:
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

Mark old `Repair(string)` as `[System.Obsolete]`.

---

## Sub-task 3: Update ClubDetailPanel — One-Tap Repair

**File:** `Assets/Scripts/UI/Inventory/ClubDetailPanel.cs`

Update `OnRepairClicked()`:
```csharp
private void OnRepairClicked()
{
    if (string.IsNullOrEmpty(currentClubId)) return;
    if (ClubManager.Instance == null || RepairKitManager.Instance == null) return;

    var playerClub = ClubManager.Instance.GetClubData(currentClubId);
    if (playerClub == null) return;

    var (newDurability, kitUsed) = RepairKitManager.Instance.UseBestKit(
        playerClub.currentDurability, playerClub.maxDurability);

    if (kitUsed == RepairKitManager.KitType.None)
    {
        Debug.Log("[ClubDetailPanel] No repair kits available."); // TODO: Toast
        return;
    }

    int oldDurability = playerClub.currentDurability;
    ClubManager.Instance.RepairClub(currentClubId, newDurability);

    var template = ClubDatabaseCSV.Instance?.GetClub(currentClubId);
    string clubName = template?.name ?? currentClubId;
    Debug.Log($"[ClubDetailPanel] {clubName} repaired with {kitUsed}. " +
              $"Durability {oldDurability} → {newDurability}."); // TODO: Toast
}
```

Subscribe to `OnClubRepaired` in `OnEnable`/`OnDisable` → refresh panel.

Update repair button interactable in `UpdatePanel()`:
```csharp
bool needsRepair = playerClub.currentDurability < playerClub.maxDurability;
bool hasKits = RepairKitManager.Instance != null && RepairKitManager.Instance.HasAnyKit();
if (repairButton != null) repairButton.interactable = needsRepair && hasKits;
```

---

## Sub-task 4: Update ClubCompareController — One-Tap Repair

**File:** `Assets/Scripts/UI/Inventory/ClubCompareController.cs`

Update `OnRightRepairClicked()` with same pattern as ClubDetailPanel.
Subscribe to `OnClubRepaired` in `OnEnable`/`OnDisable` → refresh compare panel.

---

## Sub-task 5: RepairKitManager Setup Script

**New file:** `Assets/Scripts/Editor/RepairKitManagerSetup.cs`
**MenuItem:** `GOLFIN/Setup/Repair Kit Manager`

Finds or creates RepairKitManager on the Managers GameObject.

---

## Sub-task 6: Localization (minimal — toast messages only)

```
CLUB_REPAIR_NO_KITS,You don't own any Repair Kits.,修理キットを持っていません。
CLUB_REPAIR_FULL,This club is at full durability.,このクラブは最大耐久性です。
CLUB_REPAIR_TOAST,{0} was repaired. Durability {1} → {2}.,{0}が修理されました。耐久性 {1} → {2}。
```

---

## Reminders
- **No modal** — repair is a one-tap action
- Kit selection is automatic: Standard for small repairs (≤50% missing), Premium for big ones
- RepairKitManager is standalone for now; integrates with Items system later
- Repair button grayed out when: club at full durability OR no kits
- Toast system doesn't exist yet — `Debug.Log` + `// TODO: Toast`
- Delete `Docs/SPEC_ClubPhaseE2_RepairModal.md` (old modal spec) — this file replaces it
- Push to GitHub after completing
