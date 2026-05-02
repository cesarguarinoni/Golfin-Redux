using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using GolfinRedux.UI;

public class HoleDatabaseImporter : EditorWindow
{
    private TextAsset csvFile;
    private HoleDatabase targetDatabase;

    [MenuItem("GOLFIN/Import Holes from CSV")]
    static void ShowWindow()
    {
        GetWindow<HoleDatabaseImporter>("Hole Database Importer");
    }

    void OnGUI()
    {
        GUILayout.Label("Import Holes from CSV", EditorStyles.boldLabel);
        GUILayout.Space(10);

        csvFile = (TextAsset)EditorGUILayout.ObjectField("CSV File", csvFile, typeof(TextAsset), false);
        targetDatabase = (HoleDatabase)EditorGUILayout.ObjectField("Target Database", targetDatabase, typeof(HoleDatabase), false);

        GUILayout.Space(10);

        if (GUILayout.Button("Import"))
        {
            if (csvFile == null)
            {
                EditorUtility.DisplayDialog("Error", "Please select a CSV file to import.", "OK");
                return;
            }

            if (targetDatabase == null)
            {
                EditorUtility.DisplayDialog("Error", "Please select a target HoleDatabase asset.", "OK");
                return;
            }

            ImportCSV();
        }

        GUILayout.Space(10);
        EditorGUILayout.HelpBox("CSV Format (19 columns):\ncourseNameKey,holeNumber,par,descriptionKey,holeImageName,windSpeedMph,windDirectionDegrees,reward1Type,reward1Amount,reward2Type,reward2Amount,reward3Type,reward3Amount,replayReward1Type,replayReward1Amount,replayReward2Type,replayReward2Amount,replayReward3Type,replayReward3Amount\n\nReward types: Points, RepairKit, Ball\nLeave reward columns empty if not needed.\nWind direction: 0=North, 90=East, clockwise.\nColumns 0-1: course key + hole number\nColumns 2-4: par, description key, hole image name\nColumns 5-6: wind speed/direction\nColumns 7-12: play rewards (up to 3)\nColumns 13-18: replay rewards (up to 3)", MessageType.Info);
    }

    void ImportCSV()
    {
        string csvText = csvFile.text;
        string[] lines = csvText.Split('\n');

        if (lines.Length < 2)
        {
            EditorUtility.DisplayDialog("Error", "CSV file is empty or has no data rows.", "OK");
            return;
        }

        targetDatabase.holes.Clear();

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

                targetDatabase.holes.Add(hole);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Failed to parse line {i + 1}: {line}\nError: {e.Message}");
            }
        }

        EditorUtility.SetDirty(targetDatabase);
        AssetDatabase.SaveAssets();

        EditorUtility.DisplayDialog("Success", $"Imported {targetDatabase.holes.Count} holes from CSV.", "OK");
        Debug.Log($"[HoleDatabaseImporter] Imported {targetDatabase.holes.Count} holes to {targetDatabase.name}");
    }

    RewardType ParseRewardType(string typeStr)
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
                Debug.LogWarning($"Unknown reward type: {typeStr}, defaulting to Points");
                return RewardType.Points;
        }
    }
}
