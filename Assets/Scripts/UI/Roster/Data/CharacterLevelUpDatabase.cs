#nullable enable
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace Golfin.Roster
{
    /// <summary>
    /// Loads and manages character level-up economy data from CSV
    /// Singleton pattern - use CharacterLevelUpDatabase.Instance
    /// 
    /// CSV Format:
    /// characterId,level,cost_r,sp_reward,str_cap,ctrl_cap,rec_cap,stam_cap
    /// char_elizabeth,1,100,1,30,30,20,27
    /// char_elizabeth,2,100,1,30,30,20,27
    /// ...
    /// </summary>
    public class CharacterLevelUpDatabase : MonoBehaviour
    {
        public static CharacterLevelUpDatabase Instance { get; private set; }
        
        [SerializeField] private TextAsset levelUpCostsCsv;
        
        private Dictionary<(string characterId, int level), CharacterLevelUpData> levelData = 
            new Dictionary<(string, int), CharacterLevelUpData>();
        
        private bool isLoaded = false;
        
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            
            Instance = this;
            
            if (levelUpCostsCsv != null)
            {
                LoadFromCSV(levelUpCostsCsv.text);
            }
            else
            {
                Debug.LogError("[CharacterLevelUpDatabase] CSV file not assigned in inspector!");
            }
        }
        
        /// <summary>
        /// Load level-up data from CSV content
        /// </summary>
        public void LoadFromCSV(string csvContent)
        {
            levelData.Clear();
            
            string[] lines = csvContent.Split('\n');
            if (lines.Length < 2)
            {
                Debug.LogError("[CharacterLevelUpDatabase] CSV is empty or malformed");
                return;
            }
            
            // Parse header
            string[] headers = lines[0].Split(',');
            var headerDict = new Dictionary<string, int>();
            for (int i = 0; i < headers.Length; i++)
            {
                headerDict[headers[i].Trim()] = i;
            }
            
            // Validate required columns
            string[] requiredColumns = { "characterId", "level", "cost_r", "sp_reward", "str_cap", "ctrl_cap", "rec_cap", "stam_cap" };
            foreach (var col in requiredColumns)
            {
                if (!headerDict.ContainsKey(col))
                {
                    Debug.LogError($"[CharacterLevelUpDatabase] Missing required column: {col}");
                    return;
                }
            }
            
            // Parse data rows
            int rowCount = 0;
            for (int i = 1; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i]))
                    continue;
                
                string[] values = lines[i].Split(',');
                if (values.Length < requiredColumns.Length)
                {
                    Debug.LogWarning($"[CharacterLevelUpDatabase] Skipping malformed row {i}: not enough columns");
                    continue;
                }
                
                try
                {
                    var data = new CharacterLevelUpData(
                        characterId: values[headerDict["characterId"]].Trim(),
                        level: int.Parse(values[headerDict["level"]].Trim()),
                        cost_r: int.Parse(values[headerDict["cost_r"]].Trim()),
                        sp_reward: int.Parse(values[headerDict["sp_reward"]].Trim()),
                        str_cap: int.Parse(values[headerDict["str_cap"]].Trim()),
                        ctrl_cap: int.Parse(values[headerDict["ctrl_cap"]].Trim()),
                        rec_cap: int.Parse(values[headerDict["rec_cap"]].Trim()),
                        stam_cap: int.Parse(values[headerDict["stam_cap"]].Trim())
                    );
                    
                    levelData[(data.characterId, data.level)] = data;
                    rowCount++;
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[CharacterLevelUpDatabase] Error parsing row {i}: {e.Message}");
                }
            }
            
            isLoaded = true;
            Debug.Log($"[CharacterLevelUpDatabase] Loaded {rowCount} level-up records from CSV");
        }
        
        /// <summary>
        /// Get level-up data for a character at a specific level
        /// </summary>
        public CharacterLevelUpData? GetLevelUpData(string characterId, int level)
        {
            if (!isLoaded)
            {
                Debug.LogError("[CharacterLevelUpDatabase] Database not loaded yet!");
                return null;
            }
            
            if (levelData.TryGetValue((characterId, level), out var data))
            {
                return data;
            }
            
            Debug.LogWarning($"[CharacterLevelUpDatabase] No data found for {characterId} at level {level}");
            return null;
        }
        
        /// <summary>
        /// Get the cost to level up a character
        /// </summary>
        public int GetLevelUpCost(string characterId, int toLevel)
        {
            var data = GetLevelUpData(characterId, toLevel);
            return data?.cost_r ?? 0;
        }
        
        /// <summary>
        /// Get SP reward for leveling up
        /// </summary>
        public int GetSPReward(string characterId, int toLevel)
        {
            var data = GetLevelUpData(characterId, toLevel);
            return data?.sp_reward ?? 0;
        }
        
        /// <summary>
        /// Get stat cap at a specific level
        /// </summary>
        public int GetStatCap(string characterId, int level, string statName)
        {
            var data = GetLevelUpData(characterId, level);
            if (data == null) return 0;
            
            return data.GetStatCap(statName);
        }
        
        /// <summary>
        /// Get all levels for a character
        /// </summary>
        public List<int> GetCharacterLevels(string characterId)
        {
            return levelData
                .Where(kvp => kvp.Key.characterId == characterId)
                .Select(kvp => kvp.Key.level)
                .OrderBy(l => l)
                .ToList();
        }
        
        /// <summary>
        /// Get max level for a character (typically 199)
        /// </summary>
        public int GetMaxLevel(string characterId)
        {
            var levels = GetCharacterLevels(characterId);
            return levels.Count > 0 ? levels.Last() : 1;
        }
    }
}
