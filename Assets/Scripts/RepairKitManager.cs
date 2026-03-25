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
    /// Uses a Standard Kit. Returns the new durability value after partial restoration.
    /// Returns 0 (unchanged) if no Standard Kits are available.
    /// </summary>
    public int UseStandardKit(int currentDurability, int maxDurability)
    {
        if (standardKitCount <= 0) return currentDurability;
        standardKitCount--;
        int restored     = Mathf.CeilToInt(maxDurability * STANDARD_RESTORE_PERCENT);
        int newDurability = Mathf.Min(currentDurability + restored, maxDurability);
        OnInventoryChanged?.Invoke();
        return newDurability;
    }

    /// <summary>
    /// Uses a Premium Kit. Returns maxDurability (full restoration).
    /// Returns 0 (unchanged) if no Premium Kits are available.
    /// </summary>
    public int UsePremiumKit(int maxDurability)
    {
        if (premiumKitCount <= 0) return maxDurability;
        premiumKitCount--;
        OnInventoryChanged?.Invoke();
        return maxDurability;
    }

    /// <summary>Add kits (from mission rewards, etc). Capped at MAX_STACK.</summary>
    public void AddKits(int standard, int premium)
    {
        standardKitCount = Mathf.Min(standardKitCount + standard, MAX_STACK);
        premiumKitCount  = Mathf.Min(premiumKitCount  + premium,  MAX_STACK);
        OnInventoryChanged?.Invoke();
    }
}
