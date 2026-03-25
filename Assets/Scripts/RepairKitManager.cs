#nullable enable
using UnityEngine;

/// <summary>
/// Singleton — manages Repair Kit inventory (Standard and Premium).
/// Standalone for Phase E2; will integrate with Items system (G-016) later.
///
/// No namespace (matches ClubManager, RewardPointsManager pattern).
/// Attach to: Managers GameObject (same as ClubManager, RewardPointsManager).
/// </summary>
public class RepairKitManager : MonoBehaviour
{
    public static RepairKitManager Instance { get; private set; } = null!;

    public enum KitType { None, Standard, Premium }

    // ── Event ─────────────────────────────────────────────────────────────────

    /// <summary>Fired whenever kit counts change.</summary>
    public event System.Action? OnInventoryChanged;

    // ── Config ────────────────────────────────────────────────────────────────

    public const float STANDARD_RESTORE_PERCENT = 0.5f;  // 50%
    public const float PREMIUM_RESTORE_PERCENT  = 1.0f;  // 100%
    public const int   MAX_STACK = 99;

    // ── Inventory ─────────────────────────────────────────────────────────────

    private int standardKitCount = 5;  // starting amount for testing
    private int premiumKitCount  = 2;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null!;
    }

    // ── Query API ─────────────────────────────────────────────────────────────

    public int  GetStandardCount() => standardKitCount;
    public int  GetPremiumCount()  => premiumKitCount;
    public bool HasAnyKit()        => standardKitCount > 0 || premiumKitCount > 0;

    // ── Use API ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Picks the best kit for the situation and uses it.
    /// Kit selection: ≤50% missing → prefer Standard; >50% → prefer Premium; fallback to whichever available.
    /// Returns (newDurability, kitUsed). Returns (currentDurability, None) if no kit available.
    /// </summary>
    public (int newDurability, KitType kitUsed) UseBestKit(int currentDurability, int maxDurability)
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
        // Small repair (≤50% missing) → prefer Standard to avoid wasting Premium
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

    /// <summary>Add kits (from mission rewards, etc). Capped at MAX_STACK.</summary>
    public void AddKits(int standard, int premium)
    {
        standardKitCount = Mathf.Min(standardKitCount + standard, MAX_STACK);
        premiumKitCount  = Mathf.Min(premiumKitCount  + premium,  MAX_STACK);
        OnInventoryChanged?.Invoke();
    }
}
