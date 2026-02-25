using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Manages player inventory: owned clubs, balls, characters, and their levels.
/// Currently uses local PlayerPrefs storage. Designed to be swappable to
/// server-side storage via IInventoryBackend interface.
/// </summary>
public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance { get; private set; }

    [Header("Game Data (assign all ScriptableObjects)")]
    [SerializeField] private ClubData[] allClubs;
    [SerializeField] private BallData[] allBalls;
    [SerializeField] private CharacterData[] allCharacters;

    // Runtime state
    private Dictionary<string, OwnedItem> _ownedClubs = new();
    private Dictionary<string, OwnedItem> _ownedBalls = new();
    private Dictionary<string, OwnedItem> _ownedCharacters = new();

    private string _equippedBallId;
    private string _equippedCharacterId;
    private string[] _equippedClubIds = new string[5]; // one per ClubType

    // Events
    public System.Action<string> OnItemLevelUp;
    public System.Action<string> OnItemAcquired;
    public System.Action OnLoadoutChanged;

    private IInventoryBackend _backend;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        // Default to local storage. Swap to ServerInventoryBackend for multiplayer.
        _backend = new LocalInventoryBackend();
        LoadAll();
    }

    // ─── Queries ──────────────────────────────────────────────────

    public ClubData GetClubData(string clubId) =>
        allClubs?.FirstOrDefault(c => c.clubId == clubId);

    public BallData GetBallData(string ballId) =>
        allBalls?.FirstOrDefault(b => b.ballId == ballId);

    public CharacterData GetCharacterData(string charId) =>
        allCharacters?.FirstOrDefault(c => c.characterId == charId);

    public int GetClubLevel(string clubId) =>
        _ownedClubs.TryGetValue(clubId, out var item) ? item.level : 0;

    public int GetBallLevel(string ballId) =>
        _ownedBalls.TryGetValue(ballId, out var item) ? item.level : 0;

    public int GetCharacterLevel(string charId) =>
        _ownedCharacters.TryGetValue(charId, out var item) ? item.level : 0;

    public bool OwnsClub(string clubId) => _ownedClubs.ContainsKey(clubId);
    public bool OwnsBall(string ballId) => _ownedBalls.ContainsKey(ballId);
    public bool OwnsCharacter(string charId) => _ownedCharacters.ContainsKey(charId);

    public List<string> GetOwnedClubIds() => new(_ownedClubs.Keys);
    public List<string> GetOwnedBallIds() => new(_ownedBalls.Keys);
    public List<string> GetOwnedCharacterIds() => new(_ownedCharacters.Keys);

    public string GetEquippedBallId() => _equippedBallId;
    public string GetEquippedCharacterId() => _equippedCharacterId;
    public string GetEquippedClubId(ClubType type) =>
        _equippedClubIds[(int)type];

    // ─── Mutations ────────────────────────────────────────────────

    public void AcquireClub(string clubId)
    {
        if (!_ownedClubs.ContainsKey(clubId))
        {
            _ownedClubs[clubId] = new OwnedItem { id = clubId, level = 1, xp = 0 };
            SaveAll();
            OnItemAcquired?.Invoke(clubId);
        }
    }

    public void AcquireBall(string ballId)
    {
        if (!_ownedBalls.ContainsKey(ballId))
        {
            _ownedBalls[ballId] = new OwnedItem { id = ballId, level = 1, xp = 0 };
            SaveAll();
            OnItemAcquired?.Invoke(ballId);
        }
    }

    public void AcquireCharacter(string charId)
    {
        if (!_ownedCharacters.ContainsKey(charId))
        {
            _ownedCharacters[charId] = new OwnedItem { id = charId, level = 1, xp = 0 };
            SaveAll();
            OnItemAcquired?.Invoke(charId);
        }
    }

    public bool TryLevelUpClub(string clubId)
    {
        if (!_ownedClubs.TryGetValue(clubId, out var item)) return false;
        var data = GetClubData(clubId);
        if (data == null || item.level >= data.maxLevel) return false;

        int xpNeeded = data.GetXPForLevel(item.level);
        if (item.xp < xpNeeded) return false;

        item.xp -= xpNeeded;
        item.level++;
        _ownedClubs[clubId] = item;
        SaveAll();
        OnItemLevelUp?.Invoke(clubId);
        return true;
    }

    public void AddXPToClub(string clubId, int xp)
    {
        if (_ownedClubs.TryGetValue(clubId, out var item))
        {
            item.xp += xp;
            _ownedClubs[clubId] = item;
            SaveAll();
        }
    }

    public void EquipClub(string clubId, ClubType type)
    {
        _equippedClubIds[(int)type] = clubId;
        SaveAll();
        OnLoadoutChanged?.Invoke();
    }

    public void EquipBall(string ballId)
    {
        _equippedBallId = ballId;
        SaveAll();
        OnLoadoutChanged?.Invoke();
    }

    public void EquipCharacter(string charId)
    {
        _equippedCharacterId = charId;
        SaveAll();
        OnLoadoutChanged?.Invoke();
    }

    // ─── Persistence ──────────────────────────────────────────────

    private void LoadAll()
    {
        var data = _backend.Load();
        _ownedClubs = data.clubs ?? new();
        _ownedBalls = data.balls ?? new();
        _ownedCharacters = data.characters ?? new();
        _equippedClubIds = data.equippedClubs ?? new string[5];
        _equippedBallId = data.equippedBall;
        _equippedCharacterId = data.equippedCharacter;

        // Give starter items if first time
        if (_ownedClubs.Count == 0) GiveStarterItems();
    }

    private void SaveAll()
    {
        _backend.Save(new InventorySaveData
        {
            clubs = _ownedClubs,
            balls = _ownedBalls,
            characters = _ownedCharacters,
            equippedClubs = _equippedClubIds,
            equippedBall = _equippedBallId,
            equippedCharacter = _equippedCharacterId
        });
    }

    private void GiveStarterItems()
    {
        // Give one starter club of each type + one ball + one character
        foreach (var club in allClubs)
        {
            if (club.rarity == Rarity.Common)
            {
                AcquireClub(club.clubId);
                EquipClub(club.clubId, club.clubType);
            }
        }
        foreach (var ball in allBalls)
        {
            if (ball.rarity == Rarity.Common)
            {
                AcquireBall(ball.ballId);
                if (string.IsNullOrEmpty(_equippedBallId))
                    EquipBall(ball.ballId);
            }
        }
        foreach (var ch in allCharacters)
        {
            if (ch.rarity == Rarity.Common)
            {
                AcquireCharacter(ch.characterId);
                if (string.IsNullOrEmpty(_equippedCharacterId))
                    EquipCharacter(ch.characterId);
            }
        }
    }
}

// ─── Data Structures ──────────────────────────────────────────────

[System.Serializable]
public struct OwnedItem
{
    public string id;
    public int level;
    public int xp;
}

[System.Serializable]
public class InventorySaveData
{
    public Dictionary<string, OwnedItem> clubs;
    public Dictionary<string, OwnedItem> balls;
    public Dictionary<string, OwnedItem> characters;
    public string[] equippedClubs;
    public string equippedBall;
    public string equippedCharacter;
}

// ─── Backend Interface (swap for server-side) ─────────────────────

public interface IInventoryBackend
{
    InventorySaveData Load();
    void Save(InventorySaveData data);
}

/// <summary>Local storage using PlayerPrefs + JSON</summary>
public class LocalInventoryBackend : IInventoryBackend
{
    private const string SAVE_KEY = "golfin_inventory";

    public InventorySaveData Load()
    {
        string json = PlayerPrefs.GetString(SAVE_KEY, "");
        if (string.IsNullOrEmpty(json)) return new InventorySaveData();
        return JsonUtility.FromJson<InventorySaveData>(json);
    }

    public void Save(InventorySaveData data)
    {
        string json = JsonUtility.ToJson(data);
        PlayerPrefs.SetString(SAVE_KEY, json);
        PlayerPrefs.Save();
    }
}
