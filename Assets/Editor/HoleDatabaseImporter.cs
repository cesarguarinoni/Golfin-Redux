using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using GolfinRedux.UI;

public class HoleDatabaseImporter : EditorWindow
{
    private TextAsset csvFile;
    private HoleDatabase targetDatabase;

    [MenuItem("Golfin/Import Holes from CSV")]
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
        EditorGUILayout.HelpBox("CSV Format:\ncourseNameKey,holeNumber,reward1Type,reward1Amount,reward2Type,reward2Amount,reward3Type,reward3Amount\n\nReward types: Points, RepairKit, Ball\nLeave reward columns empty if not needed.", MessageType.Info);
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
