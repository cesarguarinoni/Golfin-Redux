using UnityEngine;
using System.Collections.Generic;

namespace GolfinRedux.UI
{
    /// <summary>
    /// Automatically loads HoleDatabase from CSV at runtime.
    /// Similar to LocalizationBootstrap - just edit the CSV and it updates!
    /// </summary>
    public class HoleDatabaseLoader : MonoBehaviour
    {
        [Header("CSV Settings")]
        [SerializeField] private TextAsset holeDatabaseCSV;
        [SerializeField] private bool autoLoadOnAwake = true;

        private static HoleDatabase _runtimeDatabase;

        public static HoleDatabase RuntimeDatabase => _runtimeDatabase;

        private void Awake()
        {
            if (autoLoadOnAwake && holeDatabaseCSV != null)
            {
                LoadFromCSV();
            }
        }

        public void LoadFromCSV()
        {
            if (holeDatabaseCSV == null)
            {
                Debug.LogError("[HoleDatabaseLoader] No CSV file assigned!");
                return;
            }

            _runtimeDatabase = ScriptableObject.CreateInstance<HoleDatabase>();
            _runtimeDatabase.holes = new List<HoleData>();

            string csvText = holeDatabaseCSV.text;
            string[] lines = csvText.Split('\n');

            if (lines.Length < 2)
            {
                Debug.LogWarning("[HoleDatabaseLoader] CSV file is empty or has no data rows.");
                return;
            }

            int loadedCount = 0;

            // Skip header row (line 0)
            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (string.IsNullOrEmpty(line))
                    continue;

                string[] fields = line.Split(',');
                if (fields.Length < 2)
                    continue;

                try
                {
                    HoleData hole = new HoleData(fields[0].Trim(), int.Parse(fields[1].Trim()));

                    // Parse up to 3 rewards (each reward has Type + Amount)
                    for (int r = 0; r < 3; r++)
                    {
                        int typeIdx = 2 + (r * 2);
                        int amountIdx = 3 + (r * 2);

                        if (typeIdx >= fields.Length || amountIdx >= fields.Length)
                            break;

                        string typeStr = fields[typeIdx].Trim();
                        string amountStr = fields[amountIdx].Trim();

                        if (string.IsNullOrEmpty(typeStr) || string.IsNullOrEmpty(amountStr))
                            continue;

                        RewardType type = ParseRewardType(typeStr);
                        int amount = int.Parse(amountStr);

                        hole.AddReward(type, amount);
                    }

                    _runtimeDatabase.holes.Add(hole);
                    loadedCount++;
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[HoleDatabaseLoader] Failed to parse line {i + 1}: {line}\nError: {e.Message}");
                }
            }

            Debug.Log($"[HoleDatabaseLoader] Loaded {loadedCount} holes from CSV");
        }

        private RewardType ParseRewardType(string typeStr)
        {
            switch (typeStr.ToLower())
            {
                case "points":
                    return RewardType.Points;
                case "repairkit":
                case "repair kit":
                    return RewardType.RepairKit;
                case "ball":
                    return RewardType.Ball;
                default:
                    Debug.LogWarning($"[HoleDatabaseLoader] Unknown reward type: {typeStr}, defaulting to Points");
                    return RewardType.Points;
            }
        }

        /// <summary>
        /// Get hole by index from runtime database.
        /// Returns null if database not loaded or index out of range.
        /// </summary>
        public static HoleData GetHole(int index)
        {
            if (_runtimeDatabase == null)
            {
                Debug.LogWarning("[HoleDatabaseLoader] Runtime database not loaded yet!");
                return null;
            }

            return _runtimeDatabase.GetHole(index);
        }

        /// <summary>
        /// Get total number of holes in runtime database.
        /// </summary>
        public static int GetHoleCount()
        {
            if (_runtimeDatabase == null)
                return 0;

            return _runtimeDatabase.holes.Count;
        }
    }
}
