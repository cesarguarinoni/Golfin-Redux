using UnityEngine;

/// <summary>
/// Defines a club template. Each club type has fixed base stats
/// that improve along a defined upgrade path.
/// </summary>
[CreateAssetMenu(fileName = "NewClub", menuName = "GOLFIN/Club Data")]
public class ClubData : ScriptableObject
{
    [Header("Identity")]
    public string clubId;
    public string clubName;
    public ClubType clubType;
    public Rarity rarity;
    public Sprite icon;

    [Header("Base Stats (Level 1)")]
    [Range(0, 100)] public float basePower = 50f;       // Distance potential
    [Range(0, 100)] public float baseAccuracy = 50f;     // Shot precision
    [Range(0, 100)] public float baseControl = 50f;      // Spin/curve control

    [Header("Upgrade Path")]
    public int maxLevel = 10;
    [Tooltip("Stat gain per level (flat)")]
    public float powerPerLevel = 3f;
    public float accuracyPerLevel = 2f;
    public float controlPerLevel = 2.5f;
    [Tooltip("XP required per level: xpBase * level^xpExponent")]
    public int xpBase = 100;
    public float xpExponent = 1.5f;

    /// <summary>Get stats at a specific level</summary>
    public ClubStats GetStatsAtLevel(int level)
    {
        level = Mathf.Clamp(level, 1, maxLevel);
        int lvlBonus = level - 1;
        return new ClubStats
        {
            power = basePower + powerPerLevel * lvlBonus,
            accuracy = baseAccuracy + accuracyPerLevel * lvlBonus,
            control = baseControl + controlPerLevel * lvlBonus
        };
    }

    /// <summary>XP required to reach next level</summary>
    public int GetXPForLevel(int level)
    {
        return Mathf.RoundToInt(xpBase * Mathf.Pow(level, xpExponent));
    }
}

public enum ClubType
{
    Driver,
    Wood,
    Iron,
    Wedge,
    Putter
}

public enum Rarity
{
    Common,
    Uncommon,
    Rare,
    Legendary
}

[System.Serializable]
public struct ClubStats
{
    public float power;
    public float accuracy;
    public float control;
}
