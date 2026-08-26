#nullable enable
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Golfin.Content;
using Golfin.Inventory;
using Golfin.Roster;
using Golfin.Save;

/// <summary>
/// Singleton — owns all player club data, handles equip/unequip and bag assignment.
/// Mirrors CharacterManager pattern.
///
/// <para>
/// <b>Execution order: after ClubDatabaseCSV — and now ASSERTED rather than assumed.</b>
/// The guarantee is NOT [DefaultExecutionOrder] (neither class has one) and NOT Project Settings
/// (this project has no ProjectSettings/MonoManager.asset at all). It is the <c>executionOrder:</c>
/// field committed into ClubDatabaseCSV.cs.meta (-90) and ClubManager.cs.meta (-80), written ONCE
/// by the <c>GOLFIN ▸ Setup ▸ Club Managers</c> menu item and never re-asserted afterwards —
/// unlike SaveDataHost's, which an [InitializeOnLoad] hook re-applies on every reload. A
/// regenerated or merge-mangled .meta silently drops both to 0, where the relative order is
/// UNDEFINED and this manager would hydrate from an empty catalog. Hence the
/// <c>ClubDatabaseCSV.IsLoaded</c> check in InitializeClubs (content_overlay_catalogs).
/// </para>
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
    /// Starter bag for a fresh save: the standard Common GOLFIN set — one club of every type
    /// (Driver / Wood / Iron / P.Wedge / A.Wedge / S.Wedge / Putter). 7 clubs, so it fits
    /// MAX_CLUBS_PER_BAG (8). IDs match Clubs.csv. Satisfies the A4 bag-safety invariant.
    ///
    /// Changed 2026-08-20 (Cesar) from the legacy mixed-brand 5 to the GOLFIN Commons: every new
    /// player now starts on the same neutral, lowest-rarity baseline instead of inheriting a
    /// Legendary Royal Swing wedge and a Rare MireO iron by accident of what shipped first.
    /// </summary>
    private static readonly string[] DefaultBagIds =
    {
        "club_driver_golfin_common", "club_wood_golfin_common",  "club_iron_golfin_common",
        "club_pwedge_golfin_common", "club_awedge_golfin_common", "club_swedge_golfin_common",
        "club_putter_golfin_common",
    };

    /// <summary>
    /// The clubs a GRANDFATHERED player (a save from before club ownership was persisted) is owed.
    /// These are the 7 rows that shipped in Clubs.csv before the 792-row roster expansion — exactly
    /// what such a player could have had — so nobody loses a bag on upgrade.
    ///
    /// <para>
    /// <b>This list is the whole point.</b> Grandfather seeding used to hand over the ENTIRE
    /// catalog. That read as "existing players keep every club" when the catalog was these 7 rows;
    /// against the 799-row roster it would have granted every club in the game, for free and
    /// permanently, on the first load. Pinning it here means growing Clubs.csv can never again
    /// widen what a grandfathered save receives.
    /// </para>
    /// </summary>
    private static readonly string[] LegacyGrandfatherIds =
    {
        "club_driver_gf",     "club_wood_gf",      "club_iron9_klyro", "club_iron7_mireo",
        "club_awedge_fyloe",  "club_pwedge_royal", "club_putter_golfinx",
    };

    /// <summary>
    /// What a grandfathered save gets EQUIPPED — the bag DefaultBagIds named before the GOLFIN
    /// starter change. Kept separate so the fix to grandfather ownership does not also silently
    /// re-arrange an existing player's bag.
    /// </summary>
    private static readonly string[] LegacyDefaultBagIds =
        { "club_driver_gf", "club_wood_gf", "club_iron7_mireo", "club_pwedge_royal", "club_putter_golfinx" };

    /// <summary>Required club types a playable bag must contain (A4 bag-safety). ClubType enum names.</summary>
    private static readonly string[] RequiredBagTypes =
        { nameof(ClubType.Driver), nameof(ClubType.Wood), nameof(ClubType.Iron), nameof(ClubType.Putter) };

    /// <summary>
    /// Role groups for A4 bag-safety: each inner array defines a set of alternative club types where
    /// any ONE of them satisfies the role. Used for wedge (A_Wedge/P_Wedge/S_Wedge are equivalent roles).
    /// ClubType enum names, matching the convention of RequiredBagTypes.
    /// </summary>
    private static readonly string[][] RequiredBagTypeGroups =
    {
        // Any wedge sub-type satisfies the "wedge" role (Order 761).
        new[] { nameof(ClubType.A_Wedge), nameof(ClubType.P_Wedge), nameof(ClubType.S_Wedge) },
    };

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

        // ── DB-BEFORE-MANAGER, ASSERTED (content_overlay_catalogs) ───────────
        // Instance != null only proves ClubDatabaseCSV's Awake STARTED. IsLoaded proves LoadCSV
        // finished with rows. Without this, a broken order produces a zero-row catalog, which makes
        // BuildCatalog empty, which makes seeding grant nothing — a player with no clubs and not a
        // single error in the log. See the class remarks for where the ordering actually comes from.
        if (!db.IsLoaded)
        {
            Debug.LogError(
                "[ClubManager] EXECUTION ORDER BROKEN: ClubDatabaseCSV exists but has loaded no rows " +
                "yet, so the club catalog is about to be read EMPTY. ClubDatabaseCSV must stay ahead " +
                "of ClubManager (-90 vs -80, from their .cs.meta executionOrder fields — re-run " +
                "GOLFIN ▸ Setup ▸ Club Managers if a .meta lost the field).");
        }

        ownedClubs.Clear();

        var host = SaveDataHost.Instance;
        var catalog = BuildCatalog(db);

        if (host == null)
        {
            // No persistence available (e.g. a lab/test scene without SaveDataHost). Seed the starter
            // bag, NOT the catalog: such a scene needs a playable bag, not ownership of all 799 clubs.
            // (This used to seed the whole DB, which was survivable while the DB was 7 rows.)
            // Nothing here is persisted either way.
            Debug.LogWarning("[ClubManager] SaveDataHost.Instance is null — seeding the starter bag in-memory (not persisted).");
            var scratch = new SaveData();
            ClubOwnershipService.SeedStarter(scratch, catalog, DefaultBagIds);
            HydrateFrom(scratch);
            Debug.Log($"[ClubManager] Initialized {ownedClubs.Count} clubs in-memory (no SaveDataHost).");
            return;
        }

        var save = host.Data;

        // ── THE CLAMP STEP (content_overlay_catalogs §2) ─────────────────────
        //
        // ONCE, HERE, and never at a read site. This is the first point in the boot where BOTH
        // halves are available — the overlaid club definitions (ClubDatabaseCSV, order -90) and the
        // loaded save (SaveDataHost, -100) — and it runs BEFORE HydrateFrom, so the runtime dict is
        // built from already-clamped values.
        //
        // The case that matters: a published maxDurability BELOW an owned club's currentDurability.
        // The saved copy of maxDurability follows the catalog down, currentDurability follows it,
        // and both movements are logged with id / field / old / new. A silent clamp is
        // indistinguishable from a bug report six weeks later.
        //
        // equippedBagSlot is deliberately NOT touched: I6 says a club whose row became
        // is_active=false stays exactly as equipped as it was.
        var clampEvents = ContentClamp.ClampClubs(save.ownedClubs, BuildClampDefinitions(db));
        ContentClamp.LogAll(clampEvents, "clubs");
        if (clampEvents.Count > 0) host.MarkDirty();

        if (!save.clubOwnershipSeeded)
        {
            if (save.grandfatherClubs)
            {
                ClubOwnershipService.SeedGrandfather(save, catalog, LegacyGrandfatherIds, LegacyDefaultBagIds);
                Debug.Log($"[ClubManager] Grandfather-seeded {save.ownedClubs.Count} clubs (existing player, D-A3; " +
                          $"pinned to the legacy {LegacyGrandfatherIds.Length}, not the {catalog.Count}-row catalog).");
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

        // Order 761 v9 backfill: grant + equip the default-bag wedge for existing players
        // seeded before the wedge was added to DefaultBagIds. The v8→v9 migrator sets the signal
        // (wedgeBackfillPending=true) for any already-seeded save. Grant is idempotent (no dup
        // if already owned). Runs BEFORE A4 so the bag is complete when A4 checks.
        //
        // 2026-08-26 guard: the club it grants is the LEGENDARY 'P.Wedge Royal Swing'. Since the
        // starter bag became the GOLFIN Commons it already carries 'club_pwedge_golfin_common', so
        // a save that already owns a wedge does not need this backfill — and running it anyway is
        // what handed new players an extra Royal Swing. Only saves with NO wedge at all (the
        // fresh-seeded-post-610 cohort this backfill was written for) may take it; A4 bag-safety
        // below equips whatever wedge such a player already owns.
        if (save.wedgeBackfillPending)
        {
            const string wedgeId = "club_pwedge_royal";
            var wedgeTemplate = db.GetClub(wedgeId);
            if (OwnsAnyWedge(db))
            {
                // The common case now: the save already has a wedge (the Common GOLFIN one from
                // DefaultBagIds, or the legacy royal from the grandfather set). Nothing is owed.
                Debug.Log($"[ClubManager] Wedge backfill: save already owns a wedge — skipping the Legendary '{wedgeId}' grant.");
            }
            else if (wedgeTemplate == null)
            {
                Debug.LogWarning($"[ClubManager] Wedge backfill: '{wedgeId}' not found in DB — backfill skipped.");
            }
            else
            {
                // No wedge at all: grant and equip to bag slot 1. The fresh-seeded-post-610 cohort
                // this backfill was written for.
                var spec = BuildSpec(wedgeTemplate);
                var persisted = ClubOwnershipService.MakePersisted(spec, 1);
                save.ownedClubs.Add(persisted);
                ownedClubs[wedgeId] = ToRuntime(persisted);
                Debug.Log($"[ClubManager] Wedge backfill: granted + equipped '{wedgeId}' (no wedge owned).");
            }
            // Cleared on every pending load, taken or skipped — the signal is one-shot.
            save.wedgeBackfillPending = false;
            host.MarkDirty();
        }

        // A4 bag-safety: never leave the player with an unplayable bag. If the persisted bag is missing a
        // required type (corrupt/legacy save), re-equip the default bag for any owned required-type club.
        if (!ClubOwnershipService.HasPlayableBag(save, catalog, RequiredBagTypes, RequiredBagTypeGroups))
        {
            // Both sets: a fresh save's bag is DefaultBagIds, a grandfathered save's is the legacy
            // one. Before the GOLFIN starter change these were the same list, so iterating only
            // DefaultBagIds covered everyone; now it would silently skip every existing player.
            int fixedUp = 0;
            foreach (var id in DefaultBagIds.Concat(LegacyDefaultBagIds))
                if (ownedClubs.TryGetValue(id, out var pc) && pc.equippedBagSlot == 0) { pc.equippedBagSlot = 1; fixedUp++; }
            if (fixedUp > 0) { PersistOwnedClubs(); Debug.LogWarning($"[ClubManager] Bag-safety repair: re-equipped {fixedUp} default clubs."); }
        }

        // demo_build_slice §3.4 soft-gating: guarantee the exact 7-club demo bag (one per type),
        // all owned + in the equipped bag (slot 1; a bag holds up to 8). Mirrors the wedge-backfill
        // grant+equip pattern above. No-op in the full game.
        if (GolfinRedux.Demo.DemoGate.IsDemo)
        {
            int demoFixed = 0;
            foreach (var clubId in GolfinRedux.Demo.DemoConfig.Instance.ClubIds)
            {
                var template = db.GetClub(clubId);
                if (template == null)
                {
                    Debug.LogWarning($"[ClubManager] Demo club '{clubId}' not found in DB — skipped.");
                    continue;
                }
                if (!ownedClubs.ContainsKey(clubId))
                {
                    var spec = BuildSpec(template);
                    var persisted = ClubOwnershipService.MakePersisted(spec, 1); // grant + equip to bag 1
                    save.ownedClubs.Add(persisted);
                    ownedClubs[clubId] = ToRuntime(persisted);
                    demoFixed++;
                }
                else if (ownedClubs[clubId].equippedBagSlot != 1)
                {
                    ownedClubs[clubId].equippedBagSlot = 1;
                    var pc = save.ownedClubs.Find(c => c.clubId == clubId);
                    if (pc != null) pc.equippedBagSlot = 1;
                    demoFixed++;
                }
            }
            if (demoFixed > 0) host.MarkDirty();
            Debug.Log($"[ClubManager] Demo: ensured {GolfinRedux.Demo.DemoConfig.Instance.ClubIds.Length} clubs owned + in bag 1 ({demoFixed} changed).");
        }

        Debug.Log($"[ClubManager] Loaded {ownedClubs.Count} owned clubs from save (schema v{save.schemaVersion}).");
    }

    /// <summary>
    /// True when the runtime dict holds at least one club of any wedge type (A/P/S). Mirrors the
    /// wedge role group in RequiredBagTypeGroups — any wedge sub-type satisfies the wedge role.
    /// </summary>
    private bool OwnsAnyWedge(ClubDatabaseCSV db)
    {
        foreach (var id in ownedClubs.Keys)
        {
            var t = db.GetClub(id);
            if (t == null) continue;
            if (t.type == ClubType.A_Wedge || t.type == ClubType.P_Wedge || t.type == ClubType.S_Wedge)
                return true;
        }
        return false;
    }

    /// <summary>
    /// The bounds every owned club must fit inside, from the (possibly overlaid) catalog.
    /// <para>
    /// Club SP caps FLAT per stat (<see cref="PlayerClubData.MAX_SP_PER_STAT"/>) rather than by
    /// rarity, so — unlike characters — a club rarity change cannot orphan allocated SP. The SP
    /// clamp still runs, because a negative or corrupt value is a state a save can genuinely be in.
    /// </para>
    /// <para>
    /// <c>startLevel</c> comes from the Clubs.csv column when the row carries one and falls back to
    /// the rarity table otherwise, so a published <c>startLevel</c> is honoured by the clamp without
    /// changing how a NEW club is granted (<see cref="BuildSpec"/> keeps the rarity table).
    /// </para>
    /// </summary>
    private Dictionary<string, ClubClampDefinition> BuildClampDefinitions(ClubDatabaseCSV db)
    {
        var map = new Dictionary<string, ClubClampDefinition>(System.StringComparer.Ordinal);
        foreach (var t in db.GetAllClubs())
        {
            if (string.IsNullOrEmpty(t.clubId)) continue;
            map[t.clubId] = new ClubClampDefinition(
                t.clubId,
                t.maxDurability,
                t.startLevel > 0 ? t.startLevel : GetStartingLevel(t.rarity),
                t.maxLevel,
                PlayerClubData.MAX_SP_PER_STAT);
        }
        return map;
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
