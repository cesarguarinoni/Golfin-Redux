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

            // Parse header to get column indices
            if (lines.Length < 2)
            {
                Debug.LogError("[CharacterDatabaseCSV] CSV is empty or has no data rows");
                return;
            }

            var headers = ParseCSVLine(lines[0]);
            var headerIndex = new Dictionary<string, int>();
            for (int i = 0; i < headers.Count; i++)
            {
                headerIndex[headers[i].Trim()] = i;
            }

            // Parse data rows
            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (string.IsNullOrEmpty(line)) continue;

                var fields = ParseCSVLine(line);
                var character = ParseCharacterFromCSV(fields, headerIndex);
                if (character != null)
                {
                    characterMap[character.characterId] = character;
                    allCharacters.Add(character);
                }
            }

            Debug.Log($"[CharacterDatabaseCSV] Loaded {allCharacters.Count} characters from CSV");
        }

        /// <summary>
        /// Parse a CSV line handling quoted fields (which may contain commas)
        /// </summary>
        private List<string> ParseCSVLine(string line)
        {
            var fields = new List<string>();
            bool inQuotes = false;
            var current = new System.Text.StringBuilder();

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (c == '"')
                {
                    // Handle escaped quotes ""
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++; // skip next quote
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (c == ',' && !inQuotes)
                {
                    fields.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }

            fields.Add(current.ToString());
            return fields;
        }

        private CharacterDataRuntime? ParseCharacterFromCSV(List<string> fields, Dictionary<string, int> headerIndex)
        {
            try
            {
                string GetField(string name, string defaultValue = "")
                {
                    if (headerIndex.TryGetValue(name, out int idx) && idx < fields.Count)
                        return fields[idx].Trim();
                    return defaultValue;
                }

                int GetIntField(string name, int defaultValue = 0)
                {
                    string val = GetField(name);
                    return int.TryParse(val, out int result) ? result : defaultValue;
                }

                var character = new CharacterDataRuntime
                {
                    characterId = GetField("id"),
                    characterName = GetField("name"),
                    characterLastName = GetField("lastName"),
                    rarity = ParseRarity(GetField("rarity", "Common")),
                    baseStrength = GetIntField("baseStrength", 10),
                    baseClubControl = GetIntField("baseClubControl", 10),
                    baseRecovery = GetIntField("baseRecovery", 10),
                    baseStamina = GetIntField("baseStamina", 10),
                    portraitSpriteName = GetField("portraitSprite"),
                    maxLevel = GetIntField("maxLevel", 199),
                    bio = GetField("bio")
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
        public string characterLastName = "";
        public CharacterRarity rarity = CharacterRarity.Common;
        public int baseStrength = 10;
        public int baseClubControl = 10;
        public int baseRecovery = 10;
        public int baseStamina = 10;
        public string portraitSpriteName = "";
        public Sprite? portraitSprite = null;
        public int maxLevel = 199;
        public string bio = "";

        public Color GetRarityColor() => RarityHelper.GetRarityColor(rarity);
        public string GetRarityLabel() => RarityHelper.GetRarityLabel(rarity);

        /// <summary>
        /// Get display name formatted as "FIRSTNAME\nLASTNAME" for the detail panel
        /// </summary>
        public string GetDisplayName()
        {
            return string.IsNullOrEmpty(characterLastName)
                ? characterName.ToUpper()
                : $"{characterName.ToUpper()}\n{characterLastName.ToUpper()}";
        }

        public override string ToString()
        {
            return $"{characterName} ({rarity}): STR={baseStrength}, CTRL={baseClubControl}, REC={baseRecovery}, STAM={baseStamina}";
        }
    }
}
