using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;

namespace Golfin.UI
{
    /// <summary>
    /// Unity Editor tool to automatically build Phase 2 Settings submenus.
    /// Menu: Tools → GOLFIN → Build Phase 2 Submenus
    /// </summary>
    public class SettingsPhase2Builder : EditorWindow
    {
        private Transform userProfileRow;
        private Transform soundSettingsRow;
        private Transform languageRow;

        [MenuItem("Tools/GOLFIN/Build Phase 2 Submenus")]
        public static void ShowWindow()
        {
            var window = GetWindow<SettingsPhase2Builder>("Phase 2 Builder");
            window.minSize = new Vector2(400, 300);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Settings Phase 2 Submenu Builder", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            EditorGUILayout.HelpBox(
                "This tool will automatically create the Phase 2 submenus with proper hierarchy, " +
                "components, and references. Just assign the parent rows below.",
                MessageType.Info
            );

            EditorGUILayout.Space();

            // Row assignments
            userProfileRow = EditorGUILayout.ObjectField("User Profile Row", userProfileRow, typeof(Transform), true) as Transform;
            soundSettingsRow = EditorGUILayout.ObjectField("Sound Settings Row", soundSettingsRow, typeof(Transform), true) as Transform;
            languageRow = EditorGUILayout.ObjectField("Language Row", languageRow, typeof(Transform), true) as Transform;

            EditorGUILayout.Space();

            // Build buttons
            using (new EditorGUI.DisabledScope(userProfileRow == null))
            {
                if (GUILayout.Button("Build User Profile Submenu", GUILayout.Height(30)))
                {
                    BuildUserProfileSubmenu(userProfileRow);
                }
            }

            using (new EditorGUI.DisabledScope(soundSettingsRow == null))
            {
                if (GUILayout.Button("Build Sound Settings Submenu", GUILayout.Height(30)))
                {
                    BuildSoundSettingsSubmenu(soundSettingsRow);
                }
            }

            using (new EditorGUI.DisabledScope(languageRow == null))
            {
                if (GUILayout.Button("Build Language Submenu", GUILayout.Height(30)))
                {
                    BuildLanguageSubmenu(languageRow);
                }
            }

            EditorGUILayout.Space();

            if (GUILayout.Button("Build All Submenus", GUILayout.Height(40)))
            {
                if (userProfileRow != null) BuildUserProfileSubmenu(userProfileRow);
                if (soundSettingsRow != null) BuildSoundSettingsSubmenu(soundSettingsRow);
                if (languageRow != null) BuildLanguageSubmenu(languageRow);

                EditorUtility.DisplayDialog("Success", "All submenus built successfully!", "OK");
            }
        }

        private void BuildUserProfileSubmenu(Transform parent)
        {
            // Create container
            var submenu = CreateChild(parent, "UserProfileSubmenu");
            SetRectTransform(submenu, AnchorPreset.TopStretch, Vector2.zero, new Vector2(0, 250));
            var submenuScript = submenu.gameObject.AddComponent<UserProfileSubmenu>();

            // Username Section
            var usernameSection = CreateChild(submenu, "UsernameSection");
            SetRectTransform(usernameSection, AnchorPreset.TopStretch, new Vector2(0, -10), new Vector2(-40, 150));

            // Username Label
            var usernameLabel = CreateTextMeshPro(usernameSection, "UsernameLabel", "Username", 20);
            SetRectTransform(usernameLabel.transform, AnchorPreset.TopLeft, new Vector2(20, -15), new Vector2(200, 30));

            // Username Input Field
            var inputField = CreateInputField(usernameSection, "UsernameInputField");
            SetRectTransform(inputField.transform, AnchorPreset.TopStretch, new Vector2(0, -50), new Vector2(-40, 40));
            inputField.characterLimit = 16;

            // Save Button
            var saveButton = CreateButton(usernameSection, "SaveUsernameButton", "Save");
            SetRectTransform(saveButton.transform, AnchorPreset.TopLeft, new Vector2(20, -100), new Vector2(150, 40));

            // Feedback Text
            var feedbackText = CreateTextMeshPro(usernameSection, "FeedbackText", "", 16);
            SetRectTransform(feedbackText.transform, AnchorPreset.TopStretch, new Vector2(0, -150), new Vector2(-40, 30));
            feedbackText.color = Color.red;
            feedbackText.gameObject.SetActive(false);

            // Account Linking Section (placeholder)
            var accountSection = CreateChild(submenu, "AccountLinkingSection");
            SetRectTransform(accountSection, AnchorPreset.TopStretch, new Vector2(0, -170), new Vector2(-40, 70));

            // Wire up references
            submenuScript.GetType().GetField("usernameInputField", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(submenuScript, inputField);
            submenuScript.GetType().GetField("saveUsernameButton", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(submenuScript, saveButton.GetComponent<Button>());
            submenuScript.GetType().GetField("feedbackText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(submenuScript, feedbackText);

            EditorUtility.SetDirty(submenu.gameObject);
            Debug.Log("[Phase2Builder] User Profile Submenu created successfully!");
        }

        private void BuildSoundSettingsSubmenu(Transform parent)
        {
            // Create container
            var submenu = CreateChild(parent, "SoundSettingsSubmenu");
            SetRectTransform(submenu, AnchorPreset.TopStretch, Vector2.zero, new Vector2(0, 180));
            var submenuScript = submenu.gameObject.AddComponent<SoundSettingsSubmenu>();

            // Music Volume Section
            var musicSection = CreateChild(submenu, "MusicVolumeSection");
            SetRectTransform(musicSection, AnchorPreset.TopStretch, new Vector2(0, -10), new Vector2(-40, 80));

            var musicLabel = CreateTextMeshPro(musicSection, "MusicLabel", "Music Volume", 18);
            SetRectTransform(musicLabel.transform, AnchorPreset.TopLeft, new Vector2(20, -15), new Vector2(200, 30));

            var musicSlider = CreateSlider(musicSection, "MusicVolumeSlider", 0.8f);
            SetRectTransform(musicSlider.transform, AnchorPreset.TopStretch, new Vector2(20, -50), new Vector2(-240, 20));

            var musicText = CreateTextMeshPro(musicSection, "MusicVolumeText", "80", 18);
            SetRectTransform(musicText.transform, AnchorPreset.TopRight, new Vector2(-20, -50), new Vector2(60, 30));
            musicText.alignment = TextAlignmentOptions.Right;

            // SFX Volume Section
            var sfxSection = CreateChild(submenu, "SFXVolumeSection");
            SetRectTransform(sfxSection, AnchorPreset.TopStretch, new Vector2(0, -100), new Vector2(-40, 80));

            var sfxLabel = CreateTextMeshPro(sfxSection, "SFXLabel", "SFX Volume", 18);
            SetRectTransform(sfxLabel.transform, AnchorPreset.TopLeft, new Vector2(20, -15), new Vector2(200, 30));

            var sfxSlider = CreateSlider(sfxSection, "SFXVolumeSlider", 0.8f);
            SetRectTransform(sfxSlider.transform, AnchorPreset.TopStretch, new Vector2(20, -50), new Vector2(-240, 20));

            var sfxText = CreateTextMeshPro(sfxSection, "SFXVolumeText", "80", 18);
            SetRectTransform(sfxText.transform, AnchorPreset.TopRight, new Vector2(-20, -50), new Vector2(60, 30));
            sfxText.alignment = TextAlignmentOptions.Right;

            // Wire up references
            submenuScript.GetType().GetField("musicVolumeSlider", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(submenuScript, musicSlider);
            submenuScript.GetType().GetField("musicVolumeText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(submenuScript, musicText);
            submenuScript.GetType().GetField("sfxVolumeSlider", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(submenuScript, sfxSlider);
            submenuScript.GetType().GetField("sfxVolumeText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(submenuScript, sfxText);

            EditorUtility.SetDirty(submenu.gameObject);
            Debug.Log("[Phase2Builder] Sound Settings Submenu created successfully!");
        }

        private void BuildLanguageSubmenu(Transform parent)
        {
            // Create container
            var submenu = CreateChild(parent, "LanguageSubmenu");
            SetRectTransform(submenu, AnchorPreset.TopStretch, Vector2.zero, new Vector2(0, 120));
            var submenuScript = submenu.gameObject.AddComponent<LanguageSubmenu>();

            // English Button
            var englishButton = CreateButton(submenu, "EnglishButton", "English");
            SetRectTransform(englishButton.transform, AnchorPreset.TopStretch, new Vector2(0, -10), new Vector2(-40, 50));
            var englishCheckmark = CreateImage(englishButton.transform, "Checkmark");
            SetRectTransform(englishCheckmark.transform, AnchorPreset.RightCenter, new Vector2(-20, 0), new Vector2(24, 24));
            englishCheckmark.color = Color.green;
            englishCheckmark.gameObject.SetActive(true); // Default active

            // Japanese Button
            var japaneseButton = CreateButton(submenu, "JapaneseButton", "日本語");
            SetRectTransform(japaneseButton.transform, AnchorPreset.TopStretch, new Vector2(0, -70), new Vector2(-40, 50));
            var japaneseCheckmark = CreateImage(japaneseButton.transform, "Checkmark");
            SetRectTransform(japaneseCheckmark.transform, AnchorPreset.RightCenter, new Vector2(-20, 0), new Vector2(24, 24));
            japaneseCheckmark.color = Color.green;
            japaneseCheckmark.gameObject.SetActive(false); // Default inactive

            // Wire up references
            submenuScript.GetType().GetField("englishButton", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(submenuScript, englishButton.GetComponent<Button>());
            submenuScript.GetType().GetField("japaneseButton", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(submenuScript, japaneseButton.GetComponent<Button>());
            submenuScript.GetType().GetField("englishCheckmark", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(submenuScript, englishCheckmark.gameObject);
            submenuScript.GetType().GetField("japaneseCheckmark", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(submenuScript, japaneseCheckmark.gameObject);

            EditorUtility.SetDirty(submenu.gameObject);
            Debug.Log("[Phase2Builder] Language Submenu created successfully!");
        }

        // Helper methods

        private Transform CreateChild(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rectTransform = go.AddComponent<RectTransform>();
            return rectTransform;
        }

        private TextMeshProUGUI CreateTextMeshPro(Transform parent, string name, string text, float fontSize)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = Color.white;
            return tmp;
        }

        private TMP_InputField CreateInputField(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            var inputField = go.AddComponent<TMP_InputField>();
            
            // Create child Text for display
            var textGo = new GameObject("Text");
            textGo.transform.SetParent(go.transform, false);
            var textRect = textGo.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;
            var textComponent = textGo.AddComponent<TextMeshProUGUI>();
            textComponent.color = Color.white;

            inputField.textComponent = textComponent;
            inputField.textViewport = textRect;

            return inputField;
        }

        private Transform CreateButton(Transform parent, string name, string labelText)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            var button = go.AddComponent<Button>();
            var image = go.AddComponent<Image>();
            image.color = new Color(0.2f, 0.2f, 0.2f, 1f);
            button.targetGraphic = image;

            // Create label
            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(go.transform, false);
            var labelRect = labelGo.AddComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.sizeDelta = Vector2.zero;
            var label = labelGo.AddComponent<TextMeshProUGUI>();
            label.text = labelText;
            label.fontSize = 20;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;

            return go.transform;
        }

        private Slider CreateSlider(Transform parent, string name, float defaultValue)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            var slider = go.AddComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = defaultValue;
            slider.wholeNumbers = false;

            // Create background
            var bgGo = new GameObject("Background");
            bgGo.transform.SetParent(go.transform, false);
            var bgRect = bgGo.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;
            var bgImage = bgGo.AddComponent<Image>();
            bgImage.color = new Color(0.2f, 0.2f, 0.2f, 1f);

            // Create fill area
            var fillAreaGo = new GameObject("Fill Area");
            fillAreaGo.transform.SetParent(go.transform, false);
            var fillAreaRect = fillAreaGo.AddComponent<RectTransform>();
            fillAreaRect.anchorMin = Vector2.zero;
            fillAreaRect.anchorMax = Vector2.one;
            fillAreaRect.sizeDelta = Vector2.zero;

            var fillGo = new GameObject("Fill");
            fillGo.transform.SetParent(fillAreaGo.transform, false);
            var fillRect = fillGo.AddComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.sizeDelta = Vector2.zero;
            var fillImage = fillGo.AddComponent<Image>();
            fillImage.color = new Color(0.2f, 0.6f, 1f, 1f);

            slider.fillRect = fillRect;

            return slider;
        }

        private Image CreateImage(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            var image = go.AddComponent<Image>();
            return image;
        }

        private void SetRectTransform(Transform transform, AnchorPreset preset, Vector2 position, Vector2 sizeDelta)
        {
            var rect = transform.GetComponent<RectTransform>();
            if (rect == null) return;

            switch (preset)
            {
                case AnchorPreset.TopLeft:
                    rect.anchorMin = new Vector2(0, 1);
                    rect.anchorMax = new Vector2(0, 1);
                    rect.pivot = new Vector2(0, 1);
                    break;
                case AnchorPreset.TopCenter:
                    rect.anchorMin = new Vector2(0.5f, 1);
                    rect.anchorMax = new Vector2(0.5f, 1);
                    rect.pivot = new Vector2(0.5f, 1);
                    break;
                case AnchorPreset.TopRight:
                    rect.anchorMin = new Vector2(1, 1);
                    rect.anchorMax = new Vector2(1, 1);
                    rect.pivot = new Vector2(1, 1);
                    break;
                case AnchorPreset.TopStretch:
                    rect.anchorMin = new Vector2(0, 1);
                    rect.anchorMax = new Vector2(1, 1);
                    rect.pivot = new Vector2(0.5f, 1);
                    break;
                case AnchorPreset.RightCenter:
                    rect.anchorMin = new Vector2(1, 0.5f);
                    rect.anchorMax = new Vector2(1, 0.5f);
                    rect.pivot = new Vector2(1, 0.5f);
                    break;
            }

            rect.anchoredPosition = position;
            rect.sizeDelta = sizeDelta;
        }

        private enum AnchorPreset
        {
            TopLeft,
            TopCenter,
            TopRight,
            TopStretch,
            RightCenter
        }
    }
}
