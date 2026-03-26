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
/// </summary>
public class BagManager : MonoBehaviour
{
    public static BagManager Instance { get; private set; } = null!;

    public const int MAX_BAGS         = 10;
    public const int MAX_CLUBS_PER_BAG = 8;

    /// <summary>Fired when bag contents change. Arg = bagSlot that changed.</summary>
    public event System.Action<int>? OnBagChanged;

    private int unlockedBags = 1;  // bag 1 unlocked at start

    // Thumbnail name per slot (1-based index). Empty string → fallback to "Mireo".
    private static readonly string[] BagThumbnailNames =
    {
        "",       // index 0 — unused (slots are 1-based)
        "Mireo",  // bag 1
        "Golfin", // bag 2
        "", "", "", "", "", "", "", ""  // bags 3–10, fallback Mireo
    };

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

    /// <summary>Bag slots are 1-based.</summary>
    public bool IsBagUnlocked(int bagSlot) => bagSlot >= 1 && bagSlot <= unlockedBags;

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

    public int GetUnlockedBagCount() => unlockedBags;

    /// <summary>Returns the thumbnail name for a bag slot (falls back to "Mireo").</summary>
    public static string GetBagThumbnailName(int bagSlot)
    {
        if (bagSlot < 1 || bagSlot >= BagThumbnailNames.Length) return "Mireo";
        string name = BagThumbnailNames[bagSlot];
        return string.IsNullOrEmpty(name) ? "Mireo" : name;
    }

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

    /// <summary>Unlocks the next bag slot (for future shop/progression).</summary>
    public void UnlockNextBag()
    {
        if (unlockedBags >= MAX_BAGS) return;
        unlockedBags++;
        Debug.Log($"[BagManager] Bag {unlockedBags} unlocked.");
    }
}
