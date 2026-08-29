#nullable enable
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Golfin.Inventory;
using Golfin.Save;

/// <summary>
/// Singleton — owns all player ball data (quantities).
/// Mirrors ClubManager pattern but much simpler (no equip, no level, no durability).
///
/// Read-through facade over SaveData.ballQuantities.
/// On Awake: seeds defaults from CSV, then overlays quantities from SaveData.
/// Mutators (AddBalls) write through to SaveData and call MarkDirty.
///
/// Execution order: after BallDatabaseCSV (set in Project Settings > Script Execution Order).
/// </summary>
public class BallManager : MonoBehaviour
{
    public static BallManager Instance { get; private set; } = null!;

    /// <summary>Fired when the owned-ball list or any quantity changes.</summary>
    public event System.Action? OnInventoryChanged;

    private readonly Dictionary<string, PlayerBallData> ownedBalls = new();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        InitializeBalls();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null!;
    }

    /// <summary>
    /// Seeds PlayerBallData for every ball in the database.
    /// First ball (Golfin) gets unlimited quantity (-1), others get test quantity.
    /// Then overlays quantities from SaveData (player-specific persistence).
    /// </summary>
    private void InitializeBalls()
    {
        var db = BallDatabaseCSV.Instance;
        if (db == null)
        {
            Debug.LogError("[BallManager] BallDatabaseCSV.Instance is null — check Script Execution Order.");
            return;
        }

        ownedBalls.Clear();
        bool first = true;

        // Step 1: seed defaults from CSV.
        // content_two_way §4 — AVAILABLE, not All: the ball counterpart of ItemManager's seed, and
        // what the bag renders. Step 2 still restores a saved quantity for any id, renderable or
        // not, so an owned ball whose art is late is never lost.
        foreach (var template in db.GetAvailableBalls())
        {
            var playerBall = new PlayerBallData
            {
                ballId   = template.ballId,
                quantity = first ? -1 : 99,  // first ball = unlimited (∞), rest = test qty
            };
            ownedBalls[template.ballId] = playerBall;
            first = false;
        }

        // Step 2: overlay quantities from SaveData
        if (SaveDataHost.Instance != null)
        {
            var saveData = SaveDataHost.Instance.Data;
            foreach (var kvp in saveData.ballQuantities)
            {
                if (ownedBalls.TryGetValue(kvp.Key, out var playerBall))
                    playerBall.quantity = kvp.Value;
                else
                    ownedBalls[kvp.Key] = new PlayerBallData { ballId = kvp.Key, quantity = kvp.Value };
            }
            Debug.Log($"[BallManager] Overlaid SaveData ball quantities ({saveData.ballQuantities.Count} entries)");
        }
        else
        {
            Debug.LogWarning("[BallManager] SaveDataHost.Instance is null — ball quantities NOT loaded from save.");
        }

        Debug.Log($"[BallManager] Initialized {ownedBalls.Count} balls.");
    }

    /// <summary>
    /// Re-read quantities from the save (starter_restore_gate §4). The ball counterpart of
    /// <c>ItemManager.ReloadFromSave</c> — same cache, same reason, same safe re-run.
    /// </summary>
    public void ReloadFromSave()
    {
        InitializeBalls();
        OnInventoryChanged?.Invoke();
    }

    // ── Public API ────────────────────────────────────────────────────

    public PlayerBallData? GetBallData(string ballId)
        => ownedBalls.TryGetValue(ballId, out var data) ? data : null;

    public List<string> GetAllOwnedBallIds()
        => ownedBalls.Where(kvp => kvp.Value.quantity != 0)
                     .Select(kvp => kvp.Key)
                     .ToList();

    public int GetQuantity(string ballId)
        => ownedBalls.TryGetValue(ballId, out var data) ? data.quantity : 0;

    /// <summary>Returns display string: "∞" for unlimited, "x99" for normal.</summary>
    public string GetQuantityDisplay(string ballId)
    {
        if (!ownedBalls.TryGetValue(ballId, out var data)) return "x0";
        return data.IsUnlimited ? "∞" : $"x{data.quantity}";
    }

    /// <summary>
    /// Add balls (from hole rewards, shop purchases, etc.). UNCAPPED — see the note in the body.
    /// If the ball is unlimited (quantity == -1), the add is a no-op but
    /// OnInventoryChanged still fires so subscribers can re-render.
    /// Writes through to SaveData and calls MarkDirty.
    /// </summary>
    public void AddBalls(string ballId, int count)
    {
        if (!ownedBalls.TryGetValue(ballId, out var data))
        {
            data = new Golfin.Inventory.PlayerBallData { ballId = ballId, quantity = 0 };
            ownedBalls[ballId] = data;
        }

        // UNCAPPED, deliberately (2026-08-27). This used to clamp to 99, which was a
        // SILENT SWALLOW on a paid purchase: InventoryGrants.Apply marks a grant applied
        // and acks it BEFORE attempting delivery, so a clamped add debited the player and
        // delivered nothing. The server-side grant path (InventoryGrants.AddQuantity) never
        // capped, so the two disagreed about the same number as well.
        //
        // A stackable a player can BUY has to be buyable without a ceiling — Cesar's call,
        // the same day the shop started selling items. `-1` still means unlimited and is
        // still left alone; that is a sentinel, not a quantity.
        if (!data.IsUnlimited)
            data.quantity += count;

        // Sync to SaveData
        SyncBallToSaveData(ballId, data.quantity);
        OnInventoryChanged?.Invoke();
    }

    private void SyncBallToSaveData(string ballId, int quantity)
    {
        if (SaveDataHost.Instance == null) return;
        SaveDataHost.Instance.Data.ballQuantities[ballId] = quantity;
        SaveDataHost.Instance.MarkDirty();
    }
}
