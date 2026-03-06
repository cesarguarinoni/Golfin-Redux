#nullable enable
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace Golfin.Roster
{
    /// <summary>
    /// Loads universal level-up progression from CSV
    /// All characters share the same level costs and SP rewards
    /// Stat caps are determined by rarity (see RarityStatCaps.cs)
    /// 
    /// CSV Format (LevelUpCosts.csv):
    /// level,cost_r,sp_reward
    /// 1,100,1
    /// 2,100,1
    /// ...
    /// 199,300000,1
    /// </summary>
    public class CharacterLevelUpDatabase : MonoBehaviour
    {
        public static CharacterLevelUpDatabase Instance { get; private set; }
        
        [SerializeField] private TextAsset levelUpCostsCsv;
        
        private Dictionary<int, CharacterLevelUpData> levelData = 
            new Dictionary<int, CharacterLevelUpData>();
        
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
        /// Load level-up progression from CSV content
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
            string[] requiredColumns = { "level", "cost_r", "sp_reward" };
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
                        level: int.Parse(values[headerDict["level"]].Trim()),
                        cost_r: int.Parse(values[headerDict["cost_r"]].Trim()),
                        sp_reward: int.Parse(values[headerDict["sp_reward"]].Trim())
                    );
                    
                    levelData[data.level] = data;
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
        /// Get level-up data for a specific level
        /// </summary>
        public CharacterLevelUpData? GetLevelUpData(int level)
        {
            if (!isLoaded)
            {
                Debug.LogError("[CharacterLevelUpDatabase] Database not loaded yet!");
                return null;
            }
            
            if (levelData.TryGetValue(level, out var data))
            {
                return data;
            }
            
            Debug.LogWarning($"[CharacterLevelUpDatabase] No data found for level {level}");
            return null;
        }
        
        /// <summary>
        /// Get the cost to level up to a specific level
        /// </summary>
        public int GetLevelUpCost(int toLevel)
        {
            var data = GetLevelUpData(toLevel);
            return data?.cost_r ?? 0;
        }
        
        /// <summary>
        /// Get SP reward for leveling up to a specific level
        /// </summary>
        public int GetSPReward(int toLevel)
        {
            var data = GetLevelUpData(toLevel);
            return data?.sp_reward ?? 0;
        }
        
        /// <summary>
        /// Get all loaded levels
        /// </summary>
        public List<int> GetAllLevels()
        {
            return levelData.Keys.OrderBy(k => k).ToList();
        }
        
        /// <summary>
        /// Get max level (typically 199)
        /// </summary>
        public int GetMaxLevel()
        {
            return levelData.Count > 0 ? levelData.Keys.Max() : 1;
        }
    }
}
