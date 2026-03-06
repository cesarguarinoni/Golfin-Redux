#nullable enable
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace Golfin.Roster
{
    /// <summary>
    /// Base character data templates
    /// Defines each character's base stats and properties
    /// These are the templates; PlayerCharacterData is the player's owned instance
    /// </summary>
    [CreateAssetMenu(fileName = "CharacterDatabase", menuName = "GOLFIN/Roster/Character Database")]
    public class CharacterDatabase : ScriptableObject
    {
        [SerializeField] private List<CharacterData> characters = new List<CharacterData>();
        
        private Dictionary<string, CharacterData> characterMap = new Dictionary<string, CharacterData>();
        
        private void OnEnable()
        {
            RebuildMap();
        }
        
        private void RebuildMap()
        {
            characterMap.Clear();
            foreach (var character in characters)
            {
                if (!string.IsNullOrEmpty(character.characterId))
                {
                    characterMap[character.characterId] = character;
                }
            }
        }
        
        /// <summary>
        /// Get character template by ID
        /// </summary>
        public CharacterData? GetCharacter(string characterId)
        {
            if (characterMap.TryGetValue(characterId, out var data))
                return data;
            
            Debug.LogWarning($"[CharacterDatabase] Character {characterId} not found");
            return null;
        }
        
        /// <summary>
        /// Get all characters
        /// </summary>
        public List<CharacterData> GetAllCharacters()
        {
            return characters.ToList();
        }
        
        /// <summary>
        /// Add a character (editor use)
        /// </summary>
        public void AddCharacter(CharacterData character)
        {
            if (character != null && !characters.Contains(character))
            {
                characters.Add(character);
                RebuildMap();
            }
        }
        
        /// <summary>
        /// Remove a character (editor use)
        /// </summary>
        public void RemoveCharacter(CharacterData character)
        {
            if (character != null && characters.Remove(character))
            {
                RebuildMap();
            }
        }
    }
    
    /// <summary>
    /// Character template/definition
    /// Base stats and properties for a character
    /// </summary>
    [CreateAssetMenu(fileName = "Character_", menuName = "GOLFIN/Roster/Character Data")]
    public class CharacterData : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] public string characterId = "char_";
        [SerializeField] public string characterName = "Character Name";
        [SerializeField] public string characterNickname = "Nickname";
        [SerializeField] public CharacterRarity rarity = CharacterRarity.Common;
        
        [Header("Base Stats (at Level 1)")]
        [SerializeField] public int baseStrength = 10;
        [SerializeField] public int baseClubControl = 10;
        [SerializeField] public int baseRecovery = 10;
        [SerializeField] public int baseStamina = 10;
        
        [Header("Visuals")]
        [SerializeField] public Sprite portraitThumbnail;      // Small carousel thumbnail
        [SerializeField] public Sprite portraitFull;           // Full-body detail panel
        [SerializeField] public Color rarityColor = Color.white;
        
        [Header("Localization")]
        [SerializeField] public string nameKey = "CHAR_NAME_";    // e.g., CHAR_NAME_ELIZABETH
        [SerializeField] public string bioKey = "CHAR_BIO_";       // e.g., CHAR_BIO_ELIZABETH
        
        [Header("Audio")]
        [SerializeField] public AudioClip levelUpSound;
        [SerializeField] public AudioClip selectSound;
        
        public override string ToString()
        {
            return $"{characterName} ({rarity}): STR={baseStrength}, CTRL={baseClubControl}, REC={baseRecovery}, STAM={baseStamina}";
        }
    }
    
    /// <summary>
    /// Character rarity levels
    /// Determines stat caps and visual presentation
    /// </summary>
    public enum CharacterRarity
    {
        Common,      // C - Gray
        Uncommon,    // U - Blue
        Rare,        // R - Green
        Mythic,      // M - Yellow
        Legendary,   // L - Red
        Supreme      // S - Purple
    }
    
    /// <summary>
    /// Helper to get rarity color
    /// </summary>
    public static class RarityHelper
    {
        public static Color GetRarityColor(CharacterRarity rarity)
        {
            return rarity switch
            {
                CharacterRarity.Common => new Color(0.6f, 0.6f, 0.6f),           // Gray
                CharacterRarity.Uncommon => new Color(0.29f, 0.56f, 0.89f),      // Blue
                CharacterRarity.Rare => new Color(0.18f, 0.8f, 0.44f),           // Green
                CharacterRarity.Mythic => new Color(0.94f, 0.77f, 0.06f),        // Yellow
                CharacterRarity.Legendary => new Color(0.91f, 0.3f, 0.24f),      // Red
                CharacterRarity.Supreme => new Color(0.61f, 0.35f, 0.71f),       // Purple
                _ => Color.white
            };
        }
        
        public static string GetRarityLabel(CharacterRarity rarity)
        {
            return rarity switch
            {
                CharacterRarity.Common => "C",
                CharacterRarity.Uncommon => "U",
                CharacterRarity.Rare => "R",
                CharacterRarity.Mythic => "M",
                CharacterRarity.Legendary => "L",
                CharacterRarity.Supreme => "S",
                _ => "?"
            };
        }
        
        /// <summary>
        /// Get rarity badge text color for CharacterThumbnailCardGlowUp
        /// Base: #ABC9F5, variations per rarity
        /// </summary>
        public static Color GetRarityBadgeTextColor(CharacterRarity rarity)
        {
            return rarity switch
            {
                CharacterRarity.Common => new Color(0.67f, 0.79f, 0.96f, 1f),    // #ABC9F5 (light blue-gray)
                CharacterRarity.Uncommon => new Color(0.53f, 0.81f, 0.98f, 1f),  // Brighter blue
                CharacterRarity.Rare => new Color(0.53f, 0.95f, 0.76f, 1f),      // Light green
                CharacterRarity.Mythic => new Color(1f, 0.92f, 0.53f, 1f),       // Light yellow
                CharacterRarity.Legendary => new Color(1f, 0.65f, 0.65f, 1f),    // Light red
                CharacterRarity.Supreme => new Color(0.85f, 0.65f, 0.95f, 1f),   // Light purple
                _ => new Color(0.67f, 0.79f, 0.96f, 1f) // Default
            };
        }
    }
}
