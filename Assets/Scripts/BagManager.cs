#nullable enable
using UnityEngine;
using System.Collections.Generic;
using Golfin.Inventory;

/// <summary>
/// Singleton — convenience layer over PlayerClubData.equippedBagSlot.
/// Adds "is bag unlocked?" and "is bag full?" guards.
///
/// No namespace (matches ClubManager, RepairKitManager pattern).
/// Attach to: Managers GameObject.
///
/// Source of truth: PlayerClubData.equippedBagSlot (owned by ClubManager).
/// Unlock state: seeded from BagDatabaseCSV.startsUnlocked at Awake.
/// </summary>
public class BagManager : MonoBehaviour
{
    public static BagManager Instance { get; private set; } = null!;

    /// <summary>Total bags available — driven by CSV row count, fallback 10.</summary>
    public static int MAX_BAGS
        => BagDatabaseCSV.Instance != null ? BagDatabaseCSV.Instance.GetBagCount() : 10;

    public const int MAX_CLUBS_PER_BAG = 8;

    /// <summary>Fired when bag contents change. Arg = bagSlot that changed.</summary>
    public event System.Action<int>? OnBagChanged;

    /// <summary>The bag slot (1-based) currently equipped for gameplay. 0 = none.</summary>
    public int EquippedBagSlot { get; private set; } = 0;

    /// <summary>Fired when the equipped bag changes. Arg = new equippedBagSlot.</summary>
    public event System.Action<int>? OnEquippedBagChanged;

    private readonly HashSet<int> unlockedSlots = new();

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Seed unlock state from CSV (BagDatabaseCSV runs at order -90, before us)
        if (BagDatabaseCSV.Instance != null)
        {
            var bags = BagDatabaseCSV.Instance.GetAllBags();
            for (int i = 0; i < bags.Count; i++)
                if (bags[i].startsUnlocked) unlockedSlots.Add(i + 1);
        }
        else
        {
            // Fallback: unlock bag 1 if CSV not loaded yet
            unlockedSlots.Add(1);
            Debug.LogWarning("[BagManager] BagDatabaseCSV not ready in Awake — bag 1 unlocked as fallback.");
        }

        // Auto-equip first unlocked bag
        if (EquippedBagSlot == 0 && unlockedSlots.Count > 0)
        {
            EquippedBagSlot = 1; // bag_mireo is slot 1
            Debug.Log($"[BagManager] Auto-equipped Bag {EquippedBagSlot}.");
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null!;
    }

    // ── Query API ─────────────────────────────────────────────────────────────

    /// <summary>Bag slots are 1-based.</summary>
    public bool IsBagUnlocked(int bagSlot) => unlockedSlots.Contains(bagSlot);

    public int GetClubCountInBag(int bagSlot) => GetClubsInBag(bagSlot).Count;

    public bool IsBagFull(int bagSlot) => GetClubCountInBag(bagSlot) >= MAX_CLUBS_PER_BAG;

    /// <summary>
    /// The clubs in a bag — or, while a MISSION is running, the clubs that mission handed the
    /// player (missions_v1 §B3).
    ///
    /// ⚠️ THE OVERRIDE IS READ HERE AND NOWHERE ELSE, and that is what makes it cheap. The
    /// in-game club selector, the HUD and everything else already ask BagManager rather than
    /// walking ClubManager themselves, so one read covers all of them and no caller needed a
    /// line changed. `PlayerClubData.equippedBagSlot` — the real source of truth — is never
    /// written by a mission, so nothing about a supplied bag can outlive the hole.
    ///
    /// A `supplied:` loadout may name clubs the player does not own, so those are materialised
    /// from the CATALOG rather than from owned clubs; an `own:` mask filters what they have.
    /// Either way the returned list is the mission's, in the mission's order.
    /// </summary>
    public List<PlayerClubData> GetClubsInBag(int bagSlot)
    {
        var session = Golfin.Gameplay.Missions.MissionSessionBag.Current;
        if (session != null) return ResolveSessionClubs(session);

        if (ClubManager.Instance == null) return new List<PlayerClubData>();
        var result = new List<PlayerClubData>();
        foreach (var club in ClubManager.Instance.GetAllOwnedClubs())
            if (club.equippedBagSlot == bagSlot) result.Add(club);
        return result;
    }

    /// <summary>
    /// Turn the mission's club ids into PlayerClubData, preferring the player's OWN instance
    /// when they happen to own that club — so an `own:` loadout keeps their levels and
    /// durability, while a `supplied:` one falls back to a transient instance at the catalog
    /// default. A transient instance is never persisted and never wears: it is not owned.
    /// </summary>
    private List<PlayerClubData> ResolveSessionClubs(System.Collections.Generic.IReadOnlyList<string> clubIds)
    {
        var result = new List<PlayerClubData>();
        foreach (string id in clubIds)
        {
            PlayerClubData? owned = null;
            if (ClubManager.Instance != null)
                foreach (var club in ClubManager.Instance.GetAllOwnedClubs())
                    if (club.clubId == id) { owned = club; break; }

            if (owned != null) { result.Add(owned); continue; }

            var runtime = ClubDatabaseCSV.Instance?.GetClub(id);
            if (runtime == null)
            {
                // Surfaced, never silently dropped: a missing club is a mission with a hole in
                // its bag, and the screen's warning path is what should have caught it.
                Debug.LogWarning($"[BagManager] mission bag names '{id}', which is not in the club catalog.");
                continue;
            }
            result.Add(new PlayerClubData
            {
                clubId            = id,
                currentLevel      = runtime.startLevel,
                maxDurability     = runtime.maxDurability,
                // Full, and it stays full: a supplied club is not owned, so nothing persists
                // its wear and the player is never charged for a repair they did not cause.
                currentDurability = runtime.maxDurability,
                equippedBagSlot   = EquippedBagSlot,
            });
        }
        return result;
    }

    public int GetUnlockedBagCount() => unlockedSlots.Count;

    // ── Mutate API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Assigns a club to a bag slot.
    /// Returns false if the bag is locked or full.
    /// If the club is already in another bag, it is removed first.
    /// </summary>
    public bool AssignClubToBag(string clubId, int bagSlot)
    {
        if (!IsBagUnlocked(bagSlot))
        {
            Debug.Log($"[BagManager] Bag {bagSlot} is locked.");
            return false;
        }

        if (IsBagFull(bagSlot))
        {
            Debug.Log($"[BagManager] Bag {bagSlot} is full ({MAX_CLUBS_PER_BAG}/{MAX_CLUBS_PER_BAG})."); // TODO: Toast
            return false;
        }

        if (ClubManager.Instance == null) return false;

        // Remove from current bag if already equipped
        var playerClub = ClubManager.Instance.GetClubData(clubId);
        if (playerClub != null && playerClub.IsEquipped)
            RemoveClubFromBag(clubId);

        ClubManager.Instance.EquipClub(clubId, bagSlot);
        OnBagChanged?.Invoke(bagSlot);

        Debug.Log($"[BagManager] '{clubId}' assigned to Bag {bagSlot}.");
        return true;
    }

    /// <summary>Removes a club from its current bag (sets equippedBagSlot = 0).</summary>
    public void RemoveClubFromBag(string clubId)
    {
        if (ClubManager.Instance == null) return;
        var playerClub = ClubManager.Instance.GetClubData(clubId);
        if (playerClub == null) return;

        int oldSlot = playerClub.equippedBagSlot;
        ClubManager.Instance.EquipClub(clubId, 0);

        if (oldSlot > 0) OnBagChanged?.Invoke(oldSlot);
        Debug.Log($"[BagManager] '{clubId}' removed from Bag {oldSlot}.");
    }

    /// <summary>Equips a bag for gameplay. Only one bag can be equipped at a time.</summary>
    public void EquipBag(int bagSlot)
    {
        if (!IsBagUnlocked(bagSlot))
        {
            Debug.Log($"[BagManager] Cannot equip locked Bag {bagSlot}.");
            return;
        }
        int oldSlot = EquippedBagSlot;
        EquippedBagSlot = bagSlot;
        Debug.Log($"[BagManager] Equipped Bag {bagSlot} (was Bag {oldSlot}).");
        OnEquippedBagChanged?.Invoke(bagSlot);
    }

    /// <summary>Unlocks the next locked bag slot (for shop/progression).</summary>
    public void UnlockNextBag()
    {
        int maxBags = MAX_BAGS;
        for (int slot = 1; slot <= maxBags; slot++)
        {
            if (!unlockedSlots.Contains(slot))
            {
                unlockedSlots.Add(slot);
                Debug.Log($"[BagManager] Bag {slot} unlocked.");
                return;
            }
        }
        Debug.Log("[BagManager] All bags already unlocked.");
    }
}
