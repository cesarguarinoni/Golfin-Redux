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

    public List<PlayerClubData> GetClubsInBag(int bagSlot)
    {
        if (ClubManager.Instance == null) return new List<PlayerClubData>();
        var result = new List<PlayerClubData>();
        foreach (var club in ClubManager.Instance.GetAllOwnedClubs())
            if (club.equippedBagSlot == bagSlot) result.Add(club);
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
