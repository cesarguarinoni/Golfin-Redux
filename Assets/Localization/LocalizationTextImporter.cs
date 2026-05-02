// Assets/Scripts/Localization/Editor/LocalizationTextImporter.cs
#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class LocalizationTextImporter
{
    private const string CsvPath = "Assets/Localization/LocalizationText.csv";
    private const string AssetPath = "Assets/Localization/LocalizationTextTable.asset";

    [MenuItem("Tools/Localization/Import Text CSV")]
    public static void ImportFromMenu()
    {
        ImportCsv(CsvPath, AssetPath, true);
    }

    public static void ImportCsv(string csvPath = CsvPath, string assetPath = AssetPath, bool logResult = false)
    {
        var csvAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(csvPath);
        if (csvAsset == null)
        {
            Debug.LogError($"[Localization] CSV not found at path: {csvPath}");
            return;
        }

        var table = AssetDatabase.LoadAssetAtPath<LocalizationTextTable>(assetPath);
        if (table == null)
        {
            table = ScriptableObject.CreateInstance<LocalizationTextTable>();
            AssetDatabase.CreateAsset(table, assetPath);
        }

        table.rows.Clear();

        using (var reader = new StringReader(csvAsset.text))
        {
            bool isHeader = true;
            string line;

            while ((line = reader.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var cols = ParseCsvLine(line);

                if (isHeader)
                {
                    isHeader = false;
                    continue;
                }

                if (cols.Count < 3)
                    continue;

                var row = new LocalizedTextRow
                {
                    key     = cols[0].Trim(),
                    english = cols[1].Trim(),
                    japanese = cols[2].Trim()
                };

                if (!string.IsNullOrEmpty(row.key))
                    table.rows.Add(row);
            }
        }

        EditorUtility.SetDirty(table);
        AssetDatabase.SaveAssets();

        if (logResult)
            Debug.Log($"[Localization] CSV imported. Rows: {table.rows.Count}");
    }

    // RFC 4180-compatible CSV parser — handles quoted fields containing commas, newlines,
    // and escaped double-quotes (""). Unquoted fields are returned as-is.
    private static List<string> ParseCsvLine(string line)
    {
        var fields = new List<string>();
        int i = 0;
        int len = line.Length;

        while (i <= len)
        {
            if (i == len)
            {
                // Trailing empty field after final comma
                fields.Add(string.Empty);
                break;
            }

            if (line[i] == '"')
            {
                // Quoted field
                i++; // skip opening quote
                var sb = new System.Text.StringBuilder();
                while (i < len)
                {
                    if (line[i] == '"')
                    {
                        if (i + 1 < len && line[i + 1] == '"')
                        {
                            sb.Append('"');
                            i += 2;
                        }
                        else
                        {
                            i++; // skip closing quote
                            break;
                        }
                    }
                    else
                    {
                        sb.Append(line[i]);
                        i++;
                    }
                }
                fields.Add(sb.ToString());
                // skip comma separator
                if (i < len && line[i] == ',') i++;
            }
            else
            {
                // Unquoted field — read until comma or end
                int start = i;
                while (i < len && line[i] != ',') i++;
                fields.Add(line.Substring(start, i - start));
                if (i < len) i++; // skip comma
            }
        }

        return fields;
    }
}
#endif
