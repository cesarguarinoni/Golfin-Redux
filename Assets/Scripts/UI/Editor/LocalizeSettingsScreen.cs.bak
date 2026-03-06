using UnityEngine;
using UnityEditor;
using TMPro;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Reflection;

namespace Golfin.UI
{
    /// <summary>
    /// Auto-localizes all TextMeshPro components in Settings Screen
    /// Uses existing LocalizedText component and generates CSV entries
    /// </summary>
    public class LocalizeSettingsScreen : EditorWindow
    {
        private Transform settingsPanel;
        private string csvOutputPath = "Assets/Localization/SettingsKeys_Generated.csv";
        private bool addLocalizedTextComponent = true;
        
        // Predefined translations for common Settings terms
        private Dictionary<string, (string english, string japanese)> commonTranslations = new Dictionary<string, (string, string)>()
        {
            // Menu items
            {"User Profile", ("User Profile", "ユーザープロフィール")},
            {"Sound Settings", ("Sound Settings", "サウンド設定")},
            {"Language", ("Language", "言語")},
            {"Terms of Use", ("Terms of Use", "利用規約")},
            {"Privacy Policy", ("Privacy Policy", "プライバシーポリシー")},
            {"FAQ", ("FAQ", "よくある質問")},
            {"About", ("About", "アバウト")},
            {"Contact", ("Contact", "お問い合わせ")},
            {"Log Out", ("Log Out", "ログアウト")},
            
            // Buttons
            {"CLOSE", ("CLOSE", "閉じる")},
            {"SAVE", ("SAVE", "保存")},
            {"CANCEL", ("CANCEL", "キャンセル")},
            {"DONE", ("DONE", "完了")},
            {"EDIT", ("EDIT", "編集")},
            
            // Sound Settings
            {"Music Volume", ("Music Volume", "音楽の音量")},
            {"SFX Volume", ("SFX Volume", "効果音の音量")},
            
            // Language Selection
            {"English", ("English", "英語")},
            {"Japanese", ("Japanese", "日本語")},
            
            // User Profile
            {"Username", ("Username", "ユーザー名")},
            {"Account Linking", ("Account Linking", "アカウント連携")},
            {"Link Google", ("Link Google", "Googleと連携")},
            {"Link Apple", ("Link Apple", "Appleと連携")},
            {"Link Twitter", ("Link Twitter", "Twitterと連携")},
            
            // Common
            {"Settings", ("Settings", "設定")},
        };

        [MenuItem("Tools/GOLFIN/Localize Settings Screen")]
        public static void ShowWindow()
        {
            var window = GetWindow<LocalizeSettingsScreen>("Localize Settings");
            window.minSize = new Vector2(400, 300);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Localize Settings Screen", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            EditorGUILayout.HelpBox(
                "This tool will:\n" +
                "1. Find all TextMeshPro components in Settings Screen\n" +
                "2. Generate localization keys (SETTINGS_ITEM_NAME format)\n" +
                "3. Create a CSV file with English/Japanese translations\n" +
                "4. Add LocalizedText component + assign keys (if enabled)",
                MessageType.Info
            );

            EditorGUILayout.Space();

            settingsPanel = EditorGUILayout.ObjectField("Settings Panel", settingsPanel, typeof(Transform), true) as Transform;
            
            EditorGUILayout.Space();
            csvOutputPath = EditorGUILayout.TextField("CSV Output Path", csvOutputPath);
            
            EditorGUILayout.Space();
            addLocalizedTextComponent = EditorGUILayout.Toggle("Add LocalizedText Component", addLocalizedTextComponent);

            EditorGUILayout.Space();

            using (new EditorGUI.DisabledScope(settingsPanel == null))
            {
                if (GUILayout.Button("Generate Keys + Localize", GUILayout.Height(40)))
                {
                    GenerateKeys();
                }
            }

            EditorGUILayout.Space();

            if (GUILayout.Button("Open Generated CSV", GUILayout.Height(30)))
            {
                if (File.Exists(csvOutputPath))
                {
                    EditorUtility.RevealInFinder(csvOutputPath);
                }
                else
                {
                    EditorUtility.DisplayDialog("File Not Found", 
                        $"CSV file doesn't exist yet: {csvOutputPath}\nGenerate keys first!", 
                        "OK");
                }
            }
        }

        private void GenerateKeys()
        {
            if (settingsPanel == null)
            {
                EditorUtility.DisplayDialog("Error", "Please assign Settings Panel!", "OK");
                return;
            }

            var allTexts = settingsPanel.GetComponentsInChildren<TextMeshProUGUI>(true);
            
            if (allTexts.Length == 0)
            {
                EditorUtility.DisplayDialog("No Texts Found", 
                    "No TextMeshProUGUI components found under Settings Panel.", 
                    "OK");
                return;
            }

            var csvBuilder = new StringBuilder();
            csvBuilder.AppendLine("key,English,Japanese");

            var report = new StringBuilder();
            report.AppendLine("Generated Localization Keys:\n");

            int count = 0;
            int componentsAdded = 0;
            
            foreach (var text in allTexts)
            {
                // Skip if text is empty or just whitespace
                if (string.IsNullOrWhiteSpace(text.text)) continue;

                // Generate key from hierarchy path or text content
                string key = GenerateKey(text);
                string english = text.text.Trim();
                string japanese = GetJapaneseTranslation(english);

                // Add to CSV
                csvBuilder.AppendLine($"{key},{english},{japanese}");

                // Add LocalizedText component if enabled
                if (addLocalizedTextComponent)
                {
                    var localizedText = text.GetComponent<LocalizedText>();
                    if (localizedText == null)
                    {
                        localizedText = text.gameObject.AddComponent<LocalizedText>();
                        componentsAdded++;
                    }
                    
                    // Set the key using reflection (it's a private SerializeField)
                    SetLocalizedTextKey(localizedText, key);
                    EditorUtility.SetDirty(text.gameObject);
                }

                // Add to report
                report.AppendLine($"{text.gameObject.name}:");
                report.AppendLine($"  Key: {key}");
                report.AppendLine($"  EN: {english}");
                report.AppendLine($"  JP: {japanese}");
                if (addLocalizedTextComponent)
                {
                    report.AppendLine($"  ✅ LocalizedText component added/updated");
                }
                report.AppendLine();

                count++;
            }

            // Write CSV file
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(csvOutputPath));
                File.WriteAllText(csvOutputPath, csvBuilder.ToString());
                
                report.AppendLine($"Generated {count} localization keys!");
                report.AppendLine($"CSV saved to: {csvOutputPath}");
                
                if (addLocalizedTextComponent)
                {
                    report.AppendLine($"Added LocalizedText to {componentsAdded} objects");
                }
                
                report.AppendLine("\nNext steps:");
                report.AppendLine("1. Review the generated CSV");
                report.AppendLine("2. Adjust Japanese translations if needed");
                report.AppendLine("3. Merge into main LocalizationText.csv");
                report.AppendLine("4. Test language switching in Play Mode");

                Debug.Log(report.ToString());
                EditorUtility.DisplayDialog("Success", report.ToString(), "OK");

                AssetDatabase.Refresh();
            }
            catch (System.Exception e)
            {
                EditorUtility.DisplayDialog("Error", $"Failed to write CSV: {e.Message}", "OK");
            }
        }

        private string GenerateKey(TextMeshProUGUI text)
        {
            // Try to get a meaningful key from GameObject name or parent hierarchy
            string objectName = text.gameObject.name;
            
            // Remove common suffixes
            objectName = objectName
                .Replace("Label", "")
                .Replace("Text", "")
                .Replace("Title", "")
                .Replace("Button", "")
                .Replace(" ", "_")
                .Trim();

            // Check parent for context
            string parentName = text.transform.parent?.name ?? "";
            
            if (parentName.Contains("UserProfile"))
            {
                return $"SETTINGS_USER_{objectName.ToUpper()}";
            }
            else if (parentName.Contains("Sound"))
            {
                return $"SETTINGS_SOUND_{objectName.ToUpper()}";
            }
            else if (parentName.Contains("Language"))
            {
                return $"SETTINGS_LANG_{objectName.ToUpper()}";
            }
            else if (parentName.Contains("Row") || parentName.Contains("Item"))
            {
                // It's a menu item
                return $"SETTINGS_MENU_{objectName.ToUpper()}";
            }
            else
            {
                // Generic settings key
                return $"SETTINGS_{objectName.ToUpper()}";
            }
        }

        private string GetJapaneseTranslation(string english)
        {
            // Check common translations
            if (commonTranslations.ContainsKey(english))
            {
                return commonTranslations[english].japanese;
            }

            // For unknown strings, return romanized placeholder
            return $"[{english}]";
        }

        private void SetLocalizedTextKey(LocalizedText localizedText, string key)
        {
            // Use SerializedObject to set private field
            SerializedObject serializedObject = new SerializedObject(localizedText);
            SerializedProperty keyProperty = serializedObject.FindProperty("key");
            
            if (keyProperty != null)
            {
                keyProperty.stringValue = key;
                serializedObject.ApplyModifiedProperties();
            }
            else
            {
                Debug.LogWarning($"Could not find 'key' field on LocalizedText component for {localizedText.gameObject.name}");
            }
        }
    }
}
