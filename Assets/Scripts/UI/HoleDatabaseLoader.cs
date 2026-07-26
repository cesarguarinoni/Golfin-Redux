using UnityEngine;
using System.Collections.Generic;
using Golfin.Gameplay.Loop;
using Golfin.Course.Runtime;  // HoleTeesCsvParser (SPEC §3, Phase 3)

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
        [SerializeField] private TextAsset holeTeesCsv;   // Assets/Data/HoleTees.csv (optional; populates HoleData.tees)
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

                    // Parse par (col 2)
                    if (fields.Length > 2 && int.TryParse(fields[2].Trim(), out var par)) hole.par = par;

                    // Parse descriptionKey (col 3) and holeImageName (col 4)
                    if (fields.Length > 3) hole.descriptionKey = fields[3].Trim();
                    if (fields.Length > 4) hole.holeImageName  = fields[4].Trim();

                    // Wind columns shifted to indices 5 and 6
                    if (fields.Length > 5 && float.TryParse(fields[5].Trim(), out var ws)) hole.windSpeedMph = ws;
                    if (fields.Length > 6 && float.TryParse(fields[6].Trim(), out var wd)) hole.windDirectionDegrees = wd;

                    // Parse up to 3 play rewards — columns 7–12: typeIdx = 7 + r*2, amountIdx = 8 + r*2
                    for (int r = 0; r < 3; r++)
                    {
                        int typeIdx   = 7 + (r * 2);
                        int amountIdx = 8 + (r * 2);

                        if (typeIdx >= fields.Length || amountIdx >= fields.Length)
                            break;

                        string typeStr   = fields[typeIdx].Trim();
                        string amountStr = fields[amountIdx].Trim();

                        if (string.IsNullOrEmpty(typeStr) || string.IsNullOrEmpty(amountStr))
                            continue;

                        if (!int.TryParse(amountStr, out int amount)) continue;

                        RewardType type = ParseRewardType(typeStr);
                        hole.AddReward(type, amount);
                    }

                    // Parse up to 3 replay rewards — columns 13–18: typeIdx = 13 + r*2, amountIdx = 14 + r*2
                    for (int r = 0; r < 3; r++)
                    {
                        int typeIdx   = 13 + (r * 2);
                        int amountIdx = 14 + (r * 2);

                        if (typeIdx >= fields.Length || amountIdx >= fields.Length)
                            break;

                        string typeStr   = fields[typeIdx].Trim();
                        string amountStr = fields[amountIdx].Trim();

                        if (string.IsNullOrEmpty(typeStr) || string.IsNullOrEmpty(amountStr))
                            continue;

                        if (!int.TryParse(amountStr, out int amount)) continue;

                        RewardType type = ParseRewardType(typeStr);
                        hole.AddReplayReward(type, amount);
                    }

                    // Parse courseId (col 19). Blank/missing defaults to lomond-country-club.
                    string courseId = fields.Length > 19 ? fields[19].Trim() : string.Empty;
                    if (string.IsNullOrEmpty(courseId)) courseId = "lomond-country-club";
                    // Filter: only load holes belonging to the active course.
                    if (courseId != ActiveCourseContext.CurrentCourseSlug) continue;

                    _runtimeDatabase.holes.Add(hole);
                    loadedCount++;
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[HoleDatabaseLoader] Failed to parse line {i + 1}: {line}\nError: {e.Message}");
                }
            }

            Debug.Log($"[HoleDatabaseLoader] Loaded {loadedCount} holes from CSV");

            // Optional: populate tee data from HoleTees.csv
            if (holeTeesCsv != null)
            {
                PopulateTees(_runtimeDatabase.holes, holeTeesCsv.text, ActiveCourseContext.CurrentCourseSlug);
            }
        }

        private static void PopulateTees(List<HoleData> holes, string teesCsvText, string courseSlug)
        {
            var teesLookup = HoleTeesCsvParser.Parse(teesCsvText, courseSlug);
            int populated = 0;
            foreach (var hole in holes)
            {
                if (teesLookup.TryGetValue(hole.holeNumber, out var teeList))
                {
                    hole.tees = teeList;
                    populated++;
                }
            }
            Debug.Log($"[HoleDatabaseLoader] Populated tees for {populated}/{holes.Count} holes.");
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
