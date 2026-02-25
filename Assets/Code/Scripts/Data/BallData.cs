using UnityEngine;

/// <summary>
/// Defines a ball template. Balls affect shot physics.
/// </summary>
[CreateAssetMenu(fileName = "NewBall", menuName = "GOLFIN/Ball Data")]
public class BallData : ScriptableObject
{
    [Header("Identity")]
    public string ballId;
    public string ballName;
    public Rarity rarity;
    public Sprite icon;

    [Header("Base Stats (Level 1)")]
    [Range(0, 100)] public float baseDistance = 50f;     // Extra distance %
    [Range(0, 100)] public float baseSpin = 50f;         // Spin effectiveness
    [Range(0, 100)] public float baseWindResist = 50f;   // Wind resistance

    [Header("Upgrade Path")]
    public int maxLevel = 8;
    public float distancePerLevel = 2f;
    public float spinPerLevel = 2.5f;
    public float windResistPerLevel = 2f;
    public int xpBase = 80;
    public float xpExponent = 1.4f;

    public BallStats GetStatsAtLevel(int level)
    {
        level = Mathf.Clamp(level, 1, maxLevel);
        int lvlBonus = level - 1;
        return new BallStats
        {
            distance = baseDistance + distancePerLevel * lvlBonus,
            spin = baseSpin + spinPerLevel * lvlBonus,
            windResist = baseWindResist + windResistPerLevel * lvlBonus
        };
    }

    public int GetXPForLevel(int level)
    {
        return Mathf.RoundToInt(xpBase * Mathf.Pow(level, xpExponent));
    }
}

[System.Serializable]
public struct BallStats
{
    public float distance;
    public float spin;
    public float windResist;
}
