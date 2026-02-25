using UnityEngine;

/// <summary>
/// Defines a playable character. Characters have passive bonuses
/// that affect gameplay. Fixed upgrade path, no stat distribution.
/// </summary>
[CreateAssetMenu(fileName = "NewCharacter", menuName = "GOLFIN/Character Data")]
public class CharacterData : ScriptableObject
{
    [Header("Identity")]
    public string characterId;
    public string characterName;
    public string description;
    public Rarity rarity;
    public Sprite portrait;
    public Sprite fullBody;

    [Header("Base Stats (Level 1)")]
    [Range(0, 100)] public float basePowerBonus = 0f;    // % bonus to club power
    [Range(0, 100)] public float baseAccuracyBonus = 0f; // % bonus to accuracy
    [Range(0, 100)] public float baseSpecial = 50f;      // Unique ability strength

    [Header("Special Ability")]
    public SpecialAbility specialAbility;
    [TextArea] public string abilityDescription;

    [Header("Upgrade Path")]
    public int maxLevel = 15;
    public float powerBonusPerLevel = 0.5f;
    public float accuracyBonusPerLevel = 0.4f;
    public float specialPerLevel = 2f;
    public int xpBase = 150;
    public float xpExponent = 1.6f;

    public CharacterStats GetStatsAtLevel(int level)
    {
        level = Mathf.Clamp(level, 1, maxLevel);
        int lvlBonus = level - 1;
        return new CharacterStats
        {
            powerBonus = basePowerBonus + powerBonusPerLevel * lvlBonus,
            accuracyBonus = baseAccuracyBonus + accuracyBonusPerLevel * lvlBonus,
            special = baseSpecial + specialPerLevel * lvlBonus
        };
    }

    public int GetXPForLevel(int level)
    {
        return Mathf.RoundToInt(xpBase * Mathf.Pow(level, xpExponent));
    }
}

public enum SpecialAbility
{
    None,
    PowerDrive,     // Extra distance on drives
    PrecisionPutt,  // Better putting accuracy
    WindMaster,     // Reduced wind effect
    SpinWizard      // Enhanced spin control
}

[System.Serializable]
public struct CharacterStats
{
    public float powerBonus;
    public float accuracyBonus;
    public float special;
}
