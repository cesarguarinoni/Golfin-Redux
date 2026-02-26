using UnityEngine;

/// <summary>
/// Manages local save/load via PlayerPrefs (JSON serialized).
/// 
/// Usage:
///   SaveManager.Data.currency = 1000;
///   SaveManager.Save();
///   
///   int coins = SaveManager.Data.currency;
/// 
/// Migration to server:
///   1. Extract ISaveProvider interface
///   2. Implement ServerSaveProvider (REST/WebSocket)
///   3. SaveManager delegates to provider
///   4. Local stays as fallback/cache
/// </summary>
public static class SaveManager
{
    private const string SAVE_KEY = "golfin_player_save";
    private static PlayerSaveData _data;

    /// <summary>Current player data. Auto-loads on first access.</summary>
    public static PlayerSaveData Data
    {
        get
        {
            if (_data == null) Load();
            return _data;
        }
    }

    /// <summary>Load from PlayerPrefs. Creates default if none exists.</summary>
    public static void Load()
    {
        string json = PlayerPrefs.GetString(SAVE_KEY, "");
        if (!string.IsNullOrEmpty(json))
        {
            _data = JsonUtility.FromJson<PlayerSaveData>(json);
            Debug.Log($"[SaveManager] Loaded save (v{_data.saveVersion})");
        }
        else
        {
            _data = new PlayerSaveData();
            Debug.Log("[SaveManager] Created new save data");
        }
    }

    /// <summary>Save to PlayerPrefs.</summary>
    public static void Save()
    {
        if (_data == null) return;
        _data.lastSaveTimestamp = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        string json = JsonUtility.ToJson(_data, true);
        PlayerPrefs.SetString(SAVE_KEY, json);
        PlayerPrefs.Save();
        Debug.Log("[SaveManager] Saved");
    }

    /// <summary>Reset to defaults.</summary>
    public static void Reset()
    {
        _data = new PlayerSaveData();
        Save();
        Debug.Log("[SaveManager] Reset to defaults");
    }

    /// <summary>Delete all save data.</summary>
    public static void Delete()
    {
        PlayerPrefs.DeleteKey(SAVE_KEY);
        _data = null;
        Debug.Log("[SaveManager] Deleted");
    }

    /// <summary>Export save as JSON string (for debug/server sync).</summary>
    public static string ExportJson()
    {
        return _data != null ? JsonUtility.ToJson(_data, true) : "{}";
    }

    /// <summary>Import save from JSON string (for server sync).</summary>
    public static void ImportJson(string json)
    {
        _data = JsonUtility.FromJson<PlayerSaveData>(json);
        Save();
        Debug.Log("[SaveManager] Imported from JSON");
    }
}
