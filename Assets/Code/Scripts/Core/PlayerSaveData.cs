using UnityEngine;

/// <summary>
/// Local player save data — expandable to server sync later.
/// Uses JSON serialization to PlayerPrefs (migrateable to file/server).
/// 
/// Architecture:
///   PlayerSaveData (data model, pure C#)
///   └── SaveManager (load/save/reset, static access)
///       └── Future: ISaveProvider interface for server sync
/// </summary>
[System.Serializable]
public class PlayerSaveData
{
    // ─── Player Identity ──────────────────────────────
    public string username = "USERNAME";
    public int currency = 50000;

    // ─── Character Selection ──────────────────────────
    public int selectedCharacterIndex = -1;  // -1 = random

    // ─── Settings ─────────────────────────────────────
    public string language = "en";
    public float musicVolume = 1f;
    public float sfxVolume = 1f;
    public bool haptics = true;

    // ─── Game Progress (expandable) ───────────────────
    public int lastPlayedHole = 5;
    public string lastCourseName = "Lomond Country Club";

    // ─── Metadata ─────────────────────────────────────
    public string saveVersion = "1.0";
    public long lastSaveTimestamp;
}
