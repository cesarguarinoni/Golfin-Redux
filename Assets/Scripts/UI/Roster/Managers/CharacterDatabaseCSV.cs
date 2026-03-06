#nullable enable
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace Golfin.Roster
{
    /// <summary>
    /// CSV-driven character database
    /// Loads character data from Characters.csv at runtime
    /// Much easier to edit and balance than ScriptableObjects
    /// </summary>
    public class CharacterDatabaseCSV : MonoBehaviour
    {
        public static CharacterDatabaseCSV Instance { get; private set; }
        
        [Header("CSV File")]
        [SerializeField] private TextAsset charactersCSV;
        
        [Header("Sprites")]
        [SerializeField] private Sprite[] characterPortraits;  // Assign in Inspector
        
        private Dictionary<string, CharacterDataRuntime> characterMap = new Dictionary<string, CharacterDataRuntime>();
        private List<CharacterDataRuntime> allCharacters = new List<CharacterDataRuntime>();
        
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            
            Instance = this;
            DontDestroyOnLoad(gameObject);
            
            LoadCharactersFromCSV();
        }
        
        private void LoadCharactersFromCSV()
        {
            if (charactersCSV == null)
            {
                Debug.LogError("[CharacterDatabaseCSV] charactersCSV is null! Please assign Characters.csv");
                return;
            }
            
            characterMap.Clear();
            allCharacters.Clear();
            
            string[] lines = charactersCSV.text.Split('\n');
            
            // Skip header row
            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (string.IsNullOrEmpty(line)) continue;
                
                string[] fields = line.Split(',');
                if (fields.Length < 9) continue;
                
                var character = ParseCharacterFromCSV(fields);
                if (character != null)
                {
                    characterMap[character.characterId] = character;
                    allCharacters.Add(character);
                }
            }
            
            Debug.Log($"[CharacterDatabaseCSV] Loaded {allCharacters.Count} characters from CSV");
        }
        
        private CharacterDataRuntime? ParseCharacterFromCSV(string[] fields)
        {
            try
            {
                var character = new CharacterDataRuntime
                {
                    characterId = fields[0].Trim(),
                    characterName = fields[1].Trim(),
                    rarity = ParseRarity(fields[2].Trim()),
                    baseStrength = int.Parse(fields[3].Trim()),
                    baseClubControl = int.Parse(fields[4].Trim()),
                    baseRecovery = int.Parse(fields[5].Trim()),
                    baseStamina = int.Parse(fields[6].Trim()),
                    portraitSpriteName = fields[7].Trim(),
                    maxLevel = int.Parse(fields[8].Trim())
                };
                
                // Find sprite by name
                character.portraitSprite = FindSpriteByName(character.portraitSpriteName);
                
                return character;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[CharacterDatabaseCSV] Failed to parse character: {e.Message}");
                return null;
            }
        }
        
        private CharacterRarity ParseRarity(string rarityStr)
        {
            return rarityStr.ToLower() switch
            {
                "common" => CharacterRarity.Common,
                "uncommon" => CharacterRarity.Uncommon,
                "rare" => CharacterRarity.Rare,
                "mythic" => CharacterRarity.Mythic,
                "legendary" => CharacterRarity.Legendary,
                "supreme" => CharacterRarity.Supreme,
                _ => CharacterRarity.Common
            };
        }
        
        private Sprite? FindSpriteByName(string spriteName)
        {
            if (characterPortraits == null || characterPortraits.Length == 0)
            {
                Debug.LogWarning($"[CharacterDatabaseCSV] No character portraits assigned in Inspector!");
                return null;
            }
            
            foreach (var sprite in characterPortraits)
            {
                if (sprite != null && sprite.name == spriteName)
                {
                    return sprite;
                }
            }
            
            Debug.LogWarning($"[CharacterDatabaseCSV] Sprite '{spriteName}' not found in characterPortraits array");
            return null;
        }
        
        /// <summary>
        /// Get character data by ID
        /// </summary>
        public CharacterDataRuntime? GetCharacter(string characterId)
        {
            if (characterMap.TryGetValue(characterId, out var data))
                return data;
            
            Debug.LogWarning($"[CharacterDatabaseCSV] Character {characterId} not found");
            return null;
        }
        
        /// <summary>
        /// Get all characters
        /// </summary>
        public List<CharacterDataRuntime> GetAllCharacters()
        {
            return allCharacters.ToList();
        }
    }
    
    /// <summary>
    /// Runtime character data (loaded from CSV)
    /// Lightweight alternative to ScriptableObject CharacterData
    /// </summary>
    public class CharacterDataRuntime
    {
        public string characterId = "";
        public string characterName = "";
        public CharacterRarity rarity = CharacterRarity.Common;
        public int baseStrength = 10;
        public int baseClubControl = 10;
        public int baseRecovery = 10;
        public int baseStamina = 10;
        public string portraitSpriteName = "";
        public Sprite? portraitSprite = null;
        public int maxLevel = 199;
        
        public Color GetRarityColor() => RarityHelper.GetRarityColor(rarity);
        public string GetRarityLabel() => RarityHelper.GetRarityLabel(rarity);
        
        public override string ToString()
        {
            return $"{characterName} ({rarity}): STR={baseStrength}, CTRL={baseClubControl}, REC={baseRecovery}, STAM={baseStamina}";
        }
    }
}
