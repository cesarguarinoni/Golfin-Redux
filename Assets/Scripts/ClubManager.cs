#nullable enable
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Golfin.Inventory;
using Golfin.Roster;
using Golfin.Save;

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

    /// <summary>Fired after a club is repaired. Arg = clubId.</summary>
    public event System.Action<string>? OnClubRepaired;

    // ── State ─────────────────────────────────────────────────────────────────

    private readonly Dictionary<string, PlayerClubData> ownedClubs = new();

    /// <summary>
    /// Starter bag (fresh save) + the default equip set for grandfathered saves. IDs match Clubs.csv
    /// (the same set the lab stub uses). Guarantees the A4 bag-safety invariant: one of each required
    /// club type (Driver / Wood / Iron / Putter).
    /// </summary>
    private static readonly string[] DefaultBagIds =
        { "club_driver_gf", "club_wood_gf", "club_iron7_mireo", "club_putter_golfinx" };

    /// <summary>Required club types a playable bag must contain (A4 bag-safety). ClubType enum names.</summary>
    private static readonly string[] RequiredBagTypes =
        { nameof(ClubType.Driver), nameof(ClubType.Wood), nameof(ClubType.Iron), nameof(ClubType.Putter) };

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
    /// Loads owned clubs from the persisted save (Order 610 Phase A). On a save that has never been
    /// club-seeded, seeds the FULL DB for a grandfathered (existing) player or only the starter set for
    /// a fresh player, persists once, then hydrates the runtime dict. Ownership is now gated + persisted
    /// (was: every club auto-owned each session, nothing persisted).
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

        var host = SaveDataHost.Instance;
        var catalog = BuildCatalog(db);

        if (host == null)
        {
            // No persistence available (e.g. a lab/test scene without SaveDataHost). Fall back to the
            // pre-610 behaviour — own the full DB in-memory so the bag is playable — but DO NOT persist.
            Debug.LogWarning("[ClubManager] SaveDataHost.Instance is null — seeding full DB in-memory (not persisted).");
            var scratch = new SaveData();
            ClubOwnershipService.SeedGrandfather(scratch, catalog, DefaultBagIds);
            HydrateFrom(scratch);
            Debug.Log($"[ClubManager] Initialized {ownedClubs.Count} clubs in-memory (no SaveDataHost).");
            return;
        }

        var save = host.Data;

        if (!save.clubOwnershipSeeded)
        {
            if (save.grandfatherClubs)
            {
                ClubOwnershipService.SeedGrandfather(save, catalog, DefaultBagIds);
                Debug.Log($"[ClubManager] Grandfather-seeded {save.ownedClubs.Count} clubs (existing player, D-A3).");
            }
            else
            {
                ClubOwnershipService.SeedStarter(save, catalog, DefaultBagIds);
                Debug.Log($"[ClubManager] Starter-seeded {save.ownedClubs.Count} clubs (fresh save).");
            }
            save.clubOwnershipSeeded = true;
            save.grandfatherClubs    = false;
            host.MarkDirty();
        }

        HydrateFrom(save);

        // A4 bag-safety: never leave the player with an unplayable bag. If the persisted bag is missing a
        // required type (corrupt/legacy save), re-equip the default bag for any owned required-type club.
        if (!ClubOwnershipService.HasPlayableBag(save, catalog, RequiredBagTypes))
        {
            int fixedUp = 0;
            foreach (var id in DefaultBagIds)
                if (ownedClubs.TryGetValue(id, out var pc) && pc.equippedBagSlot == 0) { pc.equippedBagSlot = 1; fixedUp++; }
            if (fixedUp > 0) { PersistOwnedClubs(); Debug.LogWarning($"[ClubManager] Bag-safety repair: re-equipped {fixedUp} default clubs."); }
        }

        Debug.Log($"[ClubManager] Loaded {ownedClubs.Count} owned clubs from save (schema v{save.schemaVersion}).");
    }

    /// <summary>Builds the pure ClubCatalogSpec list from the club DB for the ownership service.</summary>
    private List<ClubCatalogSpec> BuildCatalog(ClubDatabaseCSV db)
    {
        var list = new List<ClubCatalogSpec>();
        foreach (var template in db.GetAllClubs())
            list.Add(BuildSpec(template));
        return list;
    }

    /// <summary>One ClubCatalogSpec for a template — starting level + seeded SP resolved here (Assembly-CSharp).</summary>
    private ClubCatalogSpec BuildSpec(ClubDataRuntime template)
    {
        int startingLevel = GetStartingLevel(template.rarity);
        int totalSP = 0;
        if (CharacterLevelUpDatabase.Instance != null)
            for (int lv = 2; lv <= startingLevel; lv++)
                totalSP += CharacterLevelUpDatabase.Instance.GetSPReward(lv);
        return new ClubCatalogSpec(template.clubId, startingLevel, template.maxDurability,
                                   totalSP, template.type.ToString());
    }

    /// <summary>Hydrate the runtime dict from a save's persisted club list.</summary>
    private void HydrateFrom(SaveData save)
    {
        ownedClubs.Clear();
        foreach (var pc in save.ownedClubs)
            ownedClubs[pc.clubId] = ToRuntime(pc);
    }

    private static PlayerClubData ToRuntime(PersistedClub p) => new PlayerClubData
    {
        clubId             = p.clubId,
        currentLevel       = p.currentLevel,
        currentDurability  = p.currentDurability,
        maxDurability      = p.maxDurability,
        equippedBagSlot    = p.equippedBagSlot,
        totalSPEarned      = p.totalSPEarned,
        spentPower         = p.spentPower,
        spentAccuracy      = p.spentAccuracy,
        spentLieResistance = p.spentLieResistance,
        spentDurability    = p.spentDurability,
    };

    private static PersistedClub ToPersisted(PlayerClubData c) => new PersistedClub
    {
        clubId             = c.clubId,
        currentLevel       = c.currentLevel,
        currentDurability  = c.currentDurability,
        maxDurability      = c.maxDurability,
        equippedBagSlot    = c.equippedBagSlot,
        totalSPEarned      = c.totalSPEarned,
        spentPower         = c.spentPower,
        spentAccuracy      = c.spentAccuracy,
        spentLieResistance = c.spentLieResistance,
        spentDurability    = c.spentDurability,
    };

    /// <summary>Rewrite the persisted club list from the runtime dict + schedule a debounced save.</summary>
    private void PersistOwnedClubs()
    {
        var host = SaveDataHost.Instance;
        if (host == null) return;
        host.Data.ownedClubs = ownedClubs.Values.Select(ToPersisted).ToList();
        host.MarkDirty();
    }

    // ── Ownership / grant (Order 610 Phase A) ───────────────────────────────────

    /// <summary>True if the player owns this club (membership == ownership; clubs are unique).</summary>
    public bool IsOwned(string clubId) => ownedClubs.ContainsKey(clubId);

    /// <summary>
    /// Grants a club to the player (A5). RP is spent by the caller (ShopTransaction) BEFORE this.
    /// Idempotent: an already-owned club is a no-op (no dup, no stat reset). New clubs land UNEQUIPPED
    /// (D5 = no auto-equip). Persists + fires OnInventoryChanged on success.
    /// </summary>
    public ClubGrantResult GrantClub(string clubId)
    {
        if (string.IsNullOrEmpty(clubId)) return ClubGrantResult.Invalid;
        if (ownedClubs.ContainsKey(clubId)) return ClubGrantResult.AlreadyOwned;

        var template = ClubDatabaseCSV.Instance?.GetClub(clubId);
        if (template == null)
        {
            Debug.LogWarning($"[ClubManager] GrantClub: club '{clubId}' not found in DB.");
            return ClubGrantResult.Invalid;
        }

        var spec = BuildSpec(template);
        ownedClubs[clubId] = ToRuntime(ClubOwnershipService.MakePersisted(spec, 0));
        PersistOwnedClubs();
        Debug.Log($"[ClubManager] Granted '{clubId}' (owned={ownedClubs.Count}).");
        OnInventoryChanged?.Invoke();
        return ClubGrantResult.Success;
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

    /// <summary>
    /// Returns one club equipped to a given bag slot, or null.
    /// Obsolete: bags hold multiple clubs — use BagManager.GetClubsInBag() instead.
    /// </summary>
    [System.Obsolete("Bags hold multiple clubs. Use BagManager.GetClubsInBag(bagSlot) instead.")]
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
    /// Bags hold up to 8 clubs — capacity is enforced by BagManager.AssignClubToBag().
    /// Pass bagSlot = 0 to unequip.
    /// </summary>
    public void EquipClub(string clubId, int bagSlot = 1)
    {
        if (!ownedClubs.TryGetValue(clubId, out var club))
        {
            Debug.LogWarning($"[ClubManager] EquipClub: club '{clubId}' not found.");
            return;
        }

        club.equippedBagSlot = bagSlot;
        Debug.Log($"[ClubManager] '{clubId}' " +
                  (bagSlot > 0 ? $"equipped to Bag {bagSlot}." : "unequipped."));

        PersistOwnedClubs();
        OnClubEquipped?.Invoke(clubId);
    }

    // ── Level Up (modal) ──────────────────────────────────────────────────

    /// <summary>
    /// Sets a club's level directly. RP payment handled by the modal before calling this.
    /// </summary>
    public void SetLevel(string clubId, int newLevel)
    {
        if (!ownedClubs.TryGetValue(clubId, out var club))
        {
            Debug.LogWarning($"[ClubManager] SetLevel: club '{clubId}' not found.");
            return;
        }

        int maxLevel = GetMaxLevel(clubId);
        club.currentLevel = Mathf.Clamp(newLevel, 1, maxLevel);
        Debug.Log($"[ClubManager] '{clubId}' level set to {club.currentLevel}/{maxLevel}.");
        PersistOwnedClubs();
    }

    /// <summary>
    /// Fires OnClubLeveledUp to trigger UI refresh after SP/level commit.
    /// </summary>
    public void RefreshStatValues(string clubId)
    {
        if (!ownedClubs.ContainsKey(clubId))
        {
            Debug.LogWarning($"[ClubManager] RefreshStatValues: club '{clubId}' not found.");
            return;
        }
        OnClubLeveledUp?.Invoke(clubId);
    }

    // ── Level Up (stub — placeholder) ─────────────────────────────────────────

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
        PersistOwnedClubs();
        OnClubLeveledUp?.Invoke(clubId);
    }

    // ── Repair ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Repairs a club to the given newDurability value.
    /// Called after RepairKitManager.UseBestKit() consumes a kit.
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
        PersistOwnedClubs();
        OnClubRepaired?.Invoke(clubId);
    }

    /// <summary>
    /// Placeholder repair stub — kept for legacy callers during transition.
    /// </summary>
    [System.Obsolete("Use RepairClub(clubId, newDurability)")]
    public void Repair(string clubId)
    {
        if (!ownedClubs.TryGetValue(clubId, out var club))
        {
            Debug.LogWarning($"[ClubManager] Repair: club '{clubId}' not found.");
            return;
        }

        Debug.Log($"[ClubManager] Repair requested for '{clubId}' " +
                  $"(durability {club.currentDurability}/{club.maxDurability}) — use RepairClub() instead.");
    }
}
