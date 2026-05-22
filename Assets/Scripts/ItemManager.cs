#nullable enable
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Golfin.Inventory;
using Golfin.Save;

/// <summary>
/// Singleton — owns all player item data (quantities).
/// Replaces RepairKitManager as the single source of truth for item inventory.
///
/// Read-through facade over SaveData.itemQuantities.
/// On Awake: seeds defaults from CSV, then overlays quantities from SaveData.
/// Mutators (UseItem, AddItems) write through to SaveData and call MarkDirty.
///
/// No namespace (matches ClubManager, BallManager pattern).
/// Attach to: Managers GameObject.
/// Execution order: after ItemDatabaseCSV (set in Project Settings > Script Execution Order).
/// </summary>
public class ItemManager : MonoBehaviour
{
    public static ItemManager Instance { get; private set; } = null!;

    /// <summary>Fired when the item list or any quantity changes.</summary>
    public event System.Action? OnInventoryChanged;

    public const int MAX_STACK = 99;

    private readonly Dictionary<string, PlayerItemData> ownedItems = new();

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        InitializeItems();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null!;
    }

    private void InitializeItems()
    {
        var db = ItemDatabaseCSV.Instance;
        if (db == null)
        {
            Debug.LogError("[ItemManager] ItemDatabaseCSV.Instance is null — check Script Execution Order.");
            return;
        }

        ownedItems.Clear();

        // Step 1: seed defaults from CSV
        foreach (var template in db.GetAllItems())
        {
            ownedItems[template.itemId] = new PlayerItemData
            {
                itemId   = template.itemId,
                quantity = 99,  // test data
            };
        }

        // Step 2: overlay quantities from SaveData
        if (SaveDataHost.Instance != null)
        {
            var saveData = SaveDataHost.Instance.Data;
            foreach (var kvp in saveData.itemQuantities)
            {
                if (ownedItems.TryGetValue(kvp.Key, out var playerItem))
                    playerItem.quantity = kvp.Value;
                else
                    ownedItems[kvp.Key] = new PlayerItemData { itemId = kvp.Key, quantity = kvp.Value };
            }
            Debug.Log($"[ItemManager] Overlaid SaveData item quantities ({saveData.itemQuantities.Count} entries)");
        }
        else
        {
            Debug.LogWarning("[ItemManager] SaveDataHost.Instance is null — item quantities NOT loaded from save.");
        }

        Debug.Log($"[ItemManager] Initialized {ownedItems.Count} items.");
    }

    // ── Query API ─────────────────────────────────────────────────────────────

    public PlayerItemData? GetItemData(string itemId)
        => ownedItems.TryGetValue(itemId, out var data) ? data : null;

    public List<string> GetAllOwnedItemIds()
        => ownedItems.Where(kvp => kvp.Value.quantity != 0)
                     .Select(kvp => kvp.Key)
                     .ToList();

    public int GetQuantity(string itemId)
        => ownedItems.TryGetValue(itemId, out var data) ? data.quantity : 0;

    /// <summary>Returns display string: "∞" for unlimited, "x99" for normal.</summary>
    public string GetQuantityDisplay(string itemId)
    {
        if (!ownedItems.TryGetValue(itemId, out var data)) return "x0";
        return data.IsUnlimited ? "∞" : $"x{data.quantity}";
    }

    // ── Mutate API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Uses one or more of an item. Does nothing if quantity is insufficient.
    /// Returns true if successful. Writes through to SaveData.
    /// </summary>
    public bool UseItem(string itemId, int count = 1)
    {
        if (!ownedItems.TryGetValue(itemId, out var data)) return false;
        if (data.IsUnlimited) { OnInventoryChanged?.Invoke(); return true; }
        if (data.quantity < count) return false;

        data.quantity -= count;
        SyncItemToSaveData(itemId, data.quantity);
        OnInventoryChanged?.Invoke();
        return true;
    }

    /// <summary>Add items (from mission rewards, etc.). Capped at MAX_STACK. Writes through to SaveData.</summary>
    public void AddItems(string itemId, int count)
    {
        if (!ownedItems.TryGetValue(itemId, out var data))
        {
            data = new PlayerItemData { itemId = itemId, quantity = 0 };
            ownedItems[itemId] = data;
        }

        if (!data.IsUnlimited)
            data.quantity = Mathf.Min(data.quantity + count, MAX_STACK);

        SyncItemToSaveData(itemId, data.quantity);
        OnInventoryChanged?.Invoke();
    }

    private void SyncItemToSaveData(string itemId, int quantity)
    {
        if (SaveDataHost.Instance == null) return;
        SaveDataHost.Instance.Data.itemQuantities[itemId] = quantity;
        SaveDataHost.Instance.MarkDirty();
    }

    // ── Repair Kit convenience ────────────────────────────────────────────────

    /// <summary>
    /// Returns the best repair kit id + restorePercent for the given damage level.
    /// Logic:
    ///   missingPercent &lt;= 50%  → prefer common (avoid wasting higher kits)
    ///   missingPercent &lt;= 75%  → prefer rare
    ///   missingPercent &gt;  75%  → prefer mythic
    ///   Falls back to whatever is available (mythic > rare > common).
    /// Returns (null, 0) if no repair kit is available.
    /// </summary>
    public (string? itemId, int restorePercent) GetBestRepairKit(float missingPercent)
    {
        var db = ItemDatabaseCSV.Instance;
        if (db == null) return (null, 0);

        var repairKits = db.GetItemsByCategory("RepairKit");
        // Sort ascending by restorePercent so we try lowest-tier first
        repairKits.Sort((a, b) => a.restorePercent.CompareTo(b.restorePercent));

        // Ideal tier: smallest kit that covers the missing %
        ItemDataRuntime? ideal = null;
        foreach (var kit in repairKits)
        {
            if (kit.restorePercent >= (int)(missingPercent * 100f) && GetQuantity(kit.itemId) > 0)
            {
                ideal = kit;
                break;
            }
        }

        if (ideal != null)
            return (ideal.itemId, ideal.restorePercent);

        // Fallback: best available kit regardless of tier (highest restorePercent first)
        for (int i = repairKits.Count - 1; i >= 0; i--)
        {
            var kit = repairKits[i];
            if (GetQuantity(kit.itemId) > 0)
                return (kit.itemId, kit.restorePercent);
        }

        return (null, 0);
    }

    /// <summary>
    /// Uses the best available repair kit and returns the new durability.
    /// Replaces RepairKitManager.UseBestKit().
    /// Returns (currentDurability, null) if no kit is available or repair not needed.
    /// </summary>
    public (int newDurability, string? itemUsed) UseBestRepairKit(int currentDurability, int maxDurability)
    {
        if (currentDurability >= maxDurability)
            return (currentDurability, null);

        float missingPercent = 1f - (float)currentDurability / maxDurability;
        var (kitId, restorePercent) = GetBestRepairKit(missingPercent);

        if (kitId == null)
            return (currentDurability, null);

        int restored    = Mathf.CeilToInt(maxDurability * restorePercent / 100f);
        int newDurability = Mathf.Min(currentDurability + restored, maxDurability);

        UseItem(kitId);
        return (newDurability, kitId);
    }

    /// <summary>True if any repair kit is available with quantity &gt; 0.</summary>
    public bool HasAnyRepairKit()
    {
        var db = ItemDatabaseCSV.Instance;
        if (db == null) return false;

        foreach (var kit in db.GetItemsByCategory("RepairKit"))
        {
            if (GetQuantity(kit.itemId) > 0) return true;
        }
        return false;
    }
}
