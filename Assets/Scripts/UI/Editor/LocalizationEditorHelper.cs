using UnityEngine;
using UnityEditor;

namespace Golfin.UI
{
    /// <summary>
    /// Helper methods for adding localization to UI elements in Editor scripts.
    /// Use this in all editor scripts that create TextMeshPro objects.
    /// </summary>
    public static class LocalizationEditorHelper
    {
        /// <summary>
        /// Add LocalizedText component to a GameObject and set its key.
        /// Call this immediately after creating any TextMeshProUGUI in editor scripts.
        /// </summary>
        /// <param name="textObject">GameObject with TextMeshProUGUI component</param>
        /// <param name="key">Localization key (e.g., "SETTINGS_MENU_USER_PROFILE")</param>
        /// <returns>The added LocalizedText component</returns>
        public static LocalizedText AddLocalizedText(GameObject textObject, string key)
        {
            if (textObject == null)
            {
                Debug.LogError("Cannot add LocalizedText: textObject is null");
                return null;
            }

            if (string.IsNullOrEmpty(key))
            {
                Debug.LogWarning($"Adding LocalizedText to {textObject.name} with empty key");
            }

            // Add LocalizedText component if it doesn't exist
            var localizedText = textObject.GetComponent<LocalizedText>();
            if (localizedText == null)
            {
                localizedText = textObject.AddComponent<LocalizedText>();
            }

            // Set the key using SerializedObject (key is a private SerializeField)
            SerializedObject serializedObject = new SerializedObject(localizedText);
            SerializedProperty keyProperty = serializedObject.FindProperty("key");

            if (keyProperty != null)
            {
                keyProperty.stringValue = key;
                serializedObject.ApplyModifiedProperties();
            }
            else
            {
                Debug.LogError($"Could not find 'key' field on LocalizedText component for {textObject.name}");
            }

            EditorUtility.SetDirty(textObject);
            return localizedText;
        }

        /// <summary>
        /// Generate a localization key from a screen name and element description.
        /// Example: GenerateKey("Settings", "Menu", "User Profile") → "SETTINGS_MENU_USER_PROFILE"
        /// </summary>
        /// <param name="parts">Key parts to join with underscores</param>
        /// <returns>Properly formatted localization key</returns>
        public static string GenerateKey(params string[] parts)
        {
            if (parts == null || parts.Length == 0)
            {
                Debug.LogWarning("GenerateKey called with no parts");
                return "UNKNOWN_KEY";
            }

            // Clean each part: uppercase, replace spaces with underscores, remove special chars
            var cleanedParts = new string[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                cleanedParts[i] = parts[i]
                    .ToUpper()
                    .Replace(" ", "_")
                    .Replace("-", "_")
                    .Replace("(", "")
                    .Replace(")", "");
            }

            return string.Join("_", cleanedParts);
        }

        /// <summary>
        /// Helper to add localization with auto-generated key.
        /// Example: AddLocalizedTextAuto(label, "Settings", "Menu", "User Profile")
        /// </summary>
        public static LocalizedText AddLocalizedTextAuto(GameObject textObject, params string[] keyParts)
        {
            string key = GenerateKey(keyParts);
            return AddLocalizedText(textObject, key);
        }

        /// <summary>
        /// Batch add LocalizedText to multiple text objects with a common prefix.
        /// Example: BatchAddLocalization(texts, "SETTINGS_MENU")
        /// where texts is a Dictionary<string, GameObject> with keys like "UserProfile", "SoundSettings"
        /// </summary>
        public static void BatchAddLocalization(System.Collections.Generic.Dictionary<string, GameObject> textObjects, string keyPrefix)
        {
            if (textObjects == null || textObjects.Count == 0)
            {
                Debug.LogWarning("BatchAddLocalization called with no objects");
                return;
            }

            int count = 0;
            foreach (var kvp in textObjects)
            {
                string fullKey = $"{keyPrefix}_{kvp.Key.ToUpper().Replace(" ", "_")}";
                if (AddLocalizedText(kvp.Value, fullKey) != null)
                {
                    count++;
                }
            }

            Debug.Log($"Added LocalizedText to {count} objects with prefix '{keyPrefix}'");
        }

        /// <summary>
        /// Check if a CSV key exists in LocalizationText.csv.
        /// Useful for validation before adding components.
        /// </summary>
        public static bool KeyExistsInCSV(string key)
        {
            string csvPath = "Assets/Localization/LocalizationText.csv";
            if (!System.IO.File.Exists(csvPath))
            {
                Debug.LogWarning($"LocalizationText.csv not found at {csvPath}");
                return false;
            }

            string[] lines = System.IO.File.ReadAllLines(csvPath);
            foreach (string line in lines)
            {
                if (line.StartsWith(key + ","))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Log a reminder to add the key to LocalizationText.csv if it doesn't exist.
        /// </summary>
        public static void RemindToAddKey(string key, string englishText, string screenName)
        {
            if (!KeyExistsInCSV(key))
            {
                Debug.LogWarning(
                    $"[Localization] Key '{key}' not found in CSV!\n" +
                    $"Add this line to LocalizationText.csv:\n" +
                    $"{key},{englishText},[Japanese translation]\n" +
                    $"Screen: {screenName}"
                );
            }
        }
    }
}
