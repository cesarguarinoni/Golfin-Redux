#nullable enable
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Golfin.Inventory;

/// <summary>
/// Singleton — owns all player ball data (quantities).
/// Mirrors ClubManager pattern but much simpler (no equip, no level, no durability).
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

        foreach (var template in db.GetAllBalls())
        {
            var playerBall = new PlayerBallData
            {
                ballId   = template.ballId,
                quantity = first ? -1 : 99,  // first ball = unlimited (∞), rest = test qty
            };
            ownedBalls[template.ballId] = playerBall;
            first = false;
        }

        Debug.Log($"[BallManager] Initialized {ownedBalls.Count} balls.");
    }

    // ── Public API ────────────────────────────────────────────────────────

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
}
