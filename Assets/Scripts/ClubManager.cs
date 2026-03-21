#nullable enable
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Golfin.Inventory;

/// <summary>
/// Singleton — owns all player club data, handles equip/unequip and bag assignment.
/// Mirrors CharacterManager pattern.
///
/// Execution order: after ClubDatabaseCSV (set in Project Settings > Script Execution Order).
/// </summary>
public class ClubManager : MonoBehaviour
{
    public static ClubManager Instance { get; private set; } = null!;

    // ── Events ────────────────────────────────────────────────────────────────

    /// <summary>Fired when a club's equip state changes. Arg = clubId.</summary>
    public event System.Action<string>? OnClubEquipped;

    /// <summary>Fired when any club's level changes. Arg = clubId.</summary>
    public event System.Action<string>? OnClubLeveledUp;

    /// <summary>Fired when the owned-club list changes (add/remove).</summary>
    public event System.Action? OnInventoryChanged;

    // ── State ─────────────────────────────────────────────────────────────────

    private readonly Dictionary<string, PlayerClubData> ownedClubs = new();

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        InitializeClubs();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null!;
    }

    // ── Initialization ────────────────────────────────────────────────────────

    private int GetStartingLevel(CharacterRarity rarity) => rarity switch
    {
        CharacterRarity.Common    => 10,
        CharacterRarity.Uncommon  => 40,
        CharacterRarity.Rare      => 80,
        CharacterRarity.Mythic    => 120,
        CharacterRarity.Legendary => 160,
        CharacterRarity.Supreme   => 200,
        _                         => 10
    };

    /// <summary>
    /// Seeds PlayerClubData for every club in the database.
    /// Starting level is rarity-based per the new leveling economy.
    /// </summary>
    private void InitializeClubs()
    {
        var db = ClubDatabaseCSV.Instance;
        if (db == null)
        {
            Debug.LogError("[ClubManager] ClubDatabaseCSV.Instance is null — check Script Execution Order.");
            return;
        }

        ownedClubs.Clear();

        foreach (var template in db.GetAllClubs())
        {
            var playerClub = new PlayerClubData
            {
                clubId            = template.clubId,
                currentLevel      = GetStartingLevel(template.rarity),
                maxDurability     = template.maxDurability,
                currentDurability = template.maxDurability,  // start at full durability
                equippedBagSlot   = 0,
            };
            ownedClubs[template.clubId] = playerClub;
        }

        Debug.Log($"[ClubManager] Initialized {ownedClubs.Count} clubs.");
    }

    // ── Query API ─────────────────────────────────────────────────────────────

    public PlayerClubData? GetClubData(string clubId)
    {
        ownedClubs.TryGetValue(clubId, out var data);
        return data;
    }

    public List<PlayerClubData> GetAllOwnedClubs()
        => ownedClubs.Values.ToList();

    public List<PlayerClubData> GetOwnedClubsOfType(ClubType type)
    {
        var db = ClubDatabaseCSV.Instance;
        if (db == null) return new List<PlayerClubData>();

        return ownedClubs.Values
            .Where(pc =>
            {
                var template = db.GetClub(pc.clubId);
                return template != null && template.type == type;
            })
            .ToList();
    }

    /// <summary>Returns the club currently equipped to a given bag slot, or null.</summary>
    public PlayerClubData? GetEquippedClub(int bagSlot = 1)
        => ownedClubs.Values.FirstOrDefault(c => c.equippedBagSlot == bagSlot);

    /// <summary>Returns the template (ClubDataRuntime) for a given player club.</summary>
    public ClubDataRuntime? GetTemplate(string clubId)
        => ClubDatabaseCSV.Instance?.GetClub(clubId);

    public int GetMaxLevel(string clubId)
        => ClubDatabaseCSV.Instance?.GetClub(clubId)?.maxLevel ?? 119;

    // ── Equip ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Equips a club to the specified bag slot.
    /// Any club already in that slot is unequipped first.
    /// Pass bagSlot = 0 to unequip.
    /// </summary>
    public void EquipClub(string clubId, int bagSlot = 1)
    {
        if (!ownedClubs.TryGetValue(clubId, out var club))
        {
            Debug.LogWarning($"[ClubManager] EquipClub: club '{clubId}' not found.");
            return;
        }

        // Unequip any club currently in this slot
        if (bagSlot > 0)
        {
            var current = GetEquippedClub(bagSlot);
            if (current != null && current.clubId != clubId)
            {
                current.equippedBagSlot = 0;
                OnClubEquipped?.Invoke(current.clubId);
            }
        }

        club.equippedBagSlot = bagSlot;
        Debug.Log($"[ClubManager] '{clubId}' " +
                  (bagSlot > 0 ? $"equipped to Bag {bagSlot}." : "unequipped."));

        OnClubEquipped?.Invoke(clubId);
    }

    // ── Level Up (stub — full modal in later phase) ───────────────────────────

    /// <summary>
    /// Placeholder level-up. Logs to console; full modal implemented later.
    /// </summary>
    public void LevelUp(string clubId)
    {
        if (!ownedClubs.TryGetValue(clubId, out var club))
        {
            Debug.LogWarning($"[ClubManager] LevelUp: club '{clubId}' not found.");
            return;
        }

        int maxLevel = GetMaxLevel(clubId);
        if (club.currentLevel >= maxLevel)
        {
            Debug.Log($"[ClubManager] '{clubId}' is already at max level {maxLevel}.");
            return;
        }

        club.currentLevel++;
        Debug.Log($"[ClubManager] '{clubId}' leveled up to {club.currentLevel}/{maxLevel}.");
        OnClubLeveledUp?.Invoke(clubId);
    }

    // ── Repair (stub — modal in later phase) ──────────────────────────────────

    /// <summary>
    /// Placeholder repair. Logs to console; Repair Modal implemented later.
    /// </summary>
    public void Repair(string clubId)
    {
        if (!ownedClubs.TryGetValue(clubId, out var club))
        {
            Debug.LogWarning($"[ClubManager] Repair: club '{clubId}' not found.");
            return;
        }

        Debug.Log($"[ClubManager] Repair requested for '{clubId}' " +
                  $"(durability {club.currentDurability}/{club.maxDurability}) — modal coming in later phase.");
    }
}
