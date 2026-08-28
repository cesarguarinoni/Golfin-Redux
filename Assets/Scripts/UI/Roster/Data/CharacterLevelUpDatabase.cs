#nullable enable
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Golfin.Content;

namespace Golfin.Roster
{
    /// <summary>
    /// Loads universal level-up progression from CSV.
    /// All characters AND clubs share the same level costs and SP rewards
    /// (ClubLevelUpModalController reads this database too).
    /// Stat caps are determined by rarity (see RarityStatCaps.cs)
    ///
    /// CSV Format (LevelUpCosts.csv):
    /// level,cost_r,sp_reward
    /// 1,100,1
    /// 2,100,1
    /// ...
    /// 199,300000,1
    ///
    /// <para>
    /// OVERLAID BY THE <c>level_up_costs</c> CONTENT CATALOG since progress_server_side (§2) — the
    /// standard treatment (bundled row + patch by id, appended rows admitted, <c>RequireReady</c> so
    /// an EditMode run reads bundled only). What is NOT standard, and is the reason this overlay
    /// matters more than the others: the SERVER prices from the same catalog. A stale client previews
    /// the bundled cost and is answered <c>cost_changed</c>; a current one previews the number it will
    /// actually be charged. The overlay is a next-launch effect (I5), so a cost published mid-session
    /// shows up on the next boot — until then the server's refusal is what keeps the two honest.
    /// </para>
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
        
        /// <summary>Re-read the bundled CSV, applying whatever the content overlay currently holds.
        /// Called by the level-up modals after a <c>cost_changed</c> refusal so the preview is
        /// rebuilt against the published costs rather than the ones the player was already
        /// shown.</summary>
        public void Reload()
        {
            if (levelUpCostsCsv == null)
            {
                Debug.LogError("[CharacterLevelUpDatabase] Reload: CSV file not assigned in inspector!");
                return;
            }
            LoadFromCSV(levelUpCostsCsv.text);
        }

        /// <summary>
        /// Load level-up progression from CSV content, patched by the <c>level_up_costs</c> overlay
        /// when one has been installed.
        /// </summary>
        public void LoadFromCSV(string csvContent)
        {
            levelData.Clear();

            ContentCatalog? overlay = ContentCatalogStore.RequireReady(nameof(CharacterLevelUpDatabase))
                ? ContentCatalogStore.Catalog(ContentCatalogs.LevelUpCosts)
                : null;

            var seen = new HashSet<int>();
            int overlaid = 0, deactivated = 0;

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
                    int level = int.Parse(values[headerDict["level"]].Trim());
                    seen.Add(level);

                    ContentRow? patch = null;
                    if (overlay != null) overlay.ById.TryGetValue(level.ToString(), out patch);

                    var fields = ContentFields.Csv(values, headerDict, patch);

                    // I6 — a deactivated cost row is a level nobody can buy. The SERVER honours it
                    // the same way (its join requires is_active), so dropping it here is what keeps
                    // the preview and the charge agreeing: the modal stops offering the level rather
                    // than offering one the server will refuse with costs_missing.
                    if (!fields.IsActive)
                    {
                        deactivated++;
                        continue;
                    }

                    var data = new CharacterLevelUpData(
                        level:     level,
                        cost_r:    fields.GetInt("cost_r"),
                        sp_reward: fields.GetInt("sp_reward")
                    );

                    if (patch != null) overlaid++;

                    levelData[data.level] = data;
                    rowCount++;
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[CharacterLevelUpDatabase] Error parsing row {i}: {e.Message}");
                }
            }

            // APPEND — an overlay row for a level the bundled CSV does not carry. This is how a
            // raised maxLevel becomes buyable without a build: the ref's maxLevel and the cost rows
            // above 240 are published together, and both halves land on the next launch.
            if (overlay != null)
            {
                foreach (var row in overlay.Rows)
                {
                    if (!int.TryParse(row.Id, out int level)) continue;
                    if (seen.Contains(level)) continue;

                    var fields = ContentFields.OverlayOnly(row);
                    if (!fields.IsActive) { deactivated++; continue; }

                    levelData[level] = new CharacterLevelUpData(
                        level:     level,
                        cost_r:    fields.GetInt("cost_r"),
                        sp_reward: fields.GetInt("sp_reward"));
                    rowCount++;
                    overlaid++;
                }
            }

            isLoaded = true;
            Debug.Log($"[CharacterLevelUpDatabase] Loaded {rowCount} level-up records" +
                      (overlay == null
                          ? " — BUNDLED only, no level_up_costs overlay this launch."
                          : $" — overlay v{overlay.Version}: {overlaid} row(s) patched/appended, " +
                            $"{deactivated} deactivated (unbuyable, as the server also treats them)."));
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
