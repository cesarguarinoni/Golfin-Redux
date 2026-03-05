using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;
using Golfin.UI.Modals;
using static Golfin.UI.LocalizationEditorHelper;

namespace Golfin.UI
{
    /// <summary>
    /// Unity Editor tool to build About Modal UI hierarchy automatically.
    /// Menu: Tools → GOLFIN → Build About Modal
    /// </summary>
    public class AboutModalBuilder : EditorWindow
    {
        private Transform settingsCanvas;
        
        [MenuItem("Tools/GOLFIN/Build About Modal")]
        public static void ShowWindow()
        {
            var window = GetWindow<AboutModalBuilder>("Build About Modal");
            window.minSize = new Vector2(400, 200);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Build About Modal", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            EditorGUILayout.HelpBox(
                "This will create the About Modal UI hierarchy:\n" +
                "• Backdrop (dark overlay)\n" +
                "• Modal Panel (centered)\n" +
                "• Title, Version, Licenses text\n" +
                "• Close button\n" +
                "• AboutModal component with references wired",
                MessageType.Info
            );

            EditorGUILayout.Space();

            settingsCanvas = EditorGUILayout.ObjectField("Settings Canvas", settingsCanvas, typeof(Transform), true) as Transform;

            EditorGUILayout.Space();

            using (new EditorGUI.DisabledScope(settingsCanvas == null))
            {
                if (GUILayout.Button("Build About Modal", GUILayout.Height(40)))
                {
                    BuildModal();
                }
            }
        }

        private void BuildModal()
        {
            if (settingsCanvas == null)
            {
                EditorUtility.DisplayDialog("Error", "Please assign Settings Canvas!", "OK");
                return;
            }

            // Check if already exists
            Transform existing = settingsCanvas.Find("AboutModal");
            if (existing != null)
            {
                bool overwrite = EditorUtility.DisplayDialog("About Modal Exists",
                    "About Modal already exists. Overwrite?",
                    "Yes", "Cancel");
                
                if (overwrite)
                {
                    DestroyImmediate(existing.gameObject);
                }
                else
                {
                    return;
                }
            }

            // Create About Modal root
            GameObject aboutModalGO = new GameObject("AboutModal");
            aboutModalGO.transform.SetParent(settingsCanvas, false);
            
            RectTransform aboutModalRect = aboutModalGO.AddComponent<RectTransform>();
            SetFullScreen(aboutModalRect);
            
            AboutModal aboutModal = aboutModalGO.AddComponent<AboutModal>();

            // 1. Create Backdrop
            GameObject backdropGO = new GameObject("Backdrop");
            backdropGO.transform.SetParent(aboutModalGO.transform, false);
            
            RectTransform backdropRect = backdropGO.AddComponent<RectTransform>();
            SetFullScreen(backdropRect);
            
            Image backdropImage = backdropGO.AddComponent<Image>();
            backdropImage.color = new Color(0, 0, 0, 0.7f); // Dark overlay
            
            Button backdropButton = backdropGO.AddComponent<Button>();
            backdropButton.transition = Selectable.Transition.None;
            // Clicking backdrop closes modal
            
            // 2. Create Modal Panel
            GameObject panelGO = new GameObject("ModalPanel");
            panelGO.transform.SetParent(aboutModalGO.transform, false);
            
            RectTransform panelRect = panelGO.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(600, 700);
            panelRect.anchoredPosition = Vector2.zero;
            
            Image panelImage = panelGO.AddComponent<Image>();
            panelImage.color = new Color(0.15f, 0.15f, 0.15f, 1f); // Dark gray
            
            // Add VerticalLayoutGroup for content
            VerticalLayoutGroup vlg = panelGO.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(40, 40, 40, 40);
            vlg.spacing = 20f;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            // 3. Title Text
            GameObject titleGO = CreateText(panelGO.transform, "TitleText", "About", 36);
            TMP_Text titleText = titleGO.GetComponent<TMP_Text>();
            titleText.fontStyle = FontStyles.Bold;
            titleText.alignment = TextAlignmentOptions.Center;
            
            LayoutElement titleLayout = titleGO.AddComponent<LayoutElement>();
            titleLayout.preferredHeight = 60;
            
            AddLocalizedTextAuto(titleGO, "Settings", "About", "Title");

            // 4. Version Text (will be set by AboutModal)
            GameObject versionGO = CreateText(panelGO.transform, "VersionText", "GOLFIN Redux\nVersion 1.0.0", 20);
            TMP_Text versionText = versionGO.GetComponent<TMP_Text>();
            versionText.alignment = TextAlignmentOptions.Center;
            versionText.color = new Color(0.8f, 0.8f, 0.8f, 1f);
            
            LayoutElement versionLayout = versionGO.AddComponent<LayoutElement>();
            versionLayout.preferredHeight = 80;

            // 5. Divider
            GameObject dividerGO = new GameObject("Divider");
            dividerGO.transform.SetParent(panelGO.transform, false);
            
            RectTransform dividerRect = dividerGO.AddComponent<RectTransform>();
            Image dividerImage = dividerGO.AddComponent<Image>();
            dividerImage.color = new Color(0.5f, 0.5f, 0.5f, 1f);
            
            LayoutElement dividerLayout = dividerGO.AddComponent<LayoutElement>();
            dividerLayout.preferredHeight = 2;

            // 6. Licenses Scroll View
            GameObject scrollViewGO = new GameObject("LicensesScrollView");
            scrollViewGO.transform.SetParent(panelGO.transform, false);
            
            RectTransform scrollRect = scrollViewGO.AddComponent<RectTransform>();
            
            ScrollRect scrollRectComponent = scrollViewGO.AddComponent<ScrollRect>();
            scrollRectComponent.vertical = true;
            scrollRectComponent.horizontal = false;
            scrollRectComponent.movementType = ScrollRect.MovementType.Clamped;
            
            LayoutElement scrollLayout = scrollViewGO.AddComponent<LayoutElement>();
            scrollLayout.flexibleHeight = 1f; // Take remaining space

            // 6a. Viewport
            GameObject viewportGO = new GameObject("Viewport");
            viewportGO.transform.SetParent(scrollViewGO.transform, false);
            
            RectTransform viewportRect = viewportGO.AddComponent<RectTransform>();
            SetFullScreen(viewportRect);
            
            Image viewportMask = viewportGO.AddComponent<Image>();
            viewportMask.color = Color.clear;
            Mask mask = viewportGO.AddComponent<Mask>();
            mask.showMaskGraphic = false;
            
            scrollRectComponent.viewport = viewportRect;

            // 6b. Content
            GameObject contentGO = new GameObject("Content");
            contentGO.transform.SetParent(viewportGO.transform, false);
            
            RectTransform contentRect = contentGO.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.pivot = new Vector2(0.5f, 1);
            contentRect.anchoredPosition = Vector2.zero;
            
            ContentSizeFitter contentFitter = contentGO.AddComponent<ContentSizeFitter>();
            contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            
            scrollRectComponent.content = contentRect;

            // 6c. Licenses Text
            GameObject licensesGO = CreateText(contentGO.transform, "LicensesText", "Third-Party Licenses:\n\nLoading...", 16);
            TMP_Text licensesText = licensesGO.GetComponent<TMP_Text>();
            licensesText.alignment = TextAlignmentOptions.TopLeft;
            licensesText.color = new Color(0.9f, 0.9f, 0.9f, 1f);
            
            RectTransform licensesRect = licensesGO.GetComponent<RectTransform>();
            SetFullWidth(licensesRect);
            
            LayoutElement licensesLayout = licensesGO.AddComponent<LayoutElement>();
            licensesLayout.preferredHeight = 400;

            // 7. Close Button
            GameObject closeButtonGO = new GameObject("CloseButton");
            closeButtonGO.transform.SetParent(panelGO.transform, false);
            
            RectTransform closeButtonRect = closeButtonGO.AddComponent<RectTransform>();
            Image closeButtonImage = closeButtonGO.AddComponent<Image>();
            closeButtonImage.color = new Color(0.8f, 0.2f, 0.2f, 1f); // Red
            
            Button closeButton = closeButtonGO.AddComponent<Button>();
            
            LayoutElement closeButtonLayout = closeButtonGO.AddComponent<LayoutElement>();
            closeButtonLayout.preferredHeight = 60;

            // Close button label
            GameObject closeLabelGO = CreateText(closeButtonGO.transform, "Label", "CLOSE", 20);
            TMP_Text closeLabel = closeLabelGO.GetComponent<TMP_Text>();
            closeLabel.fontStyle = FontStyles.Bold;
            closeLabel.alignment = TextAlignmentOptions.Center;
            closeLabel.color = Color.white;
            
            RectTransform closeLabelRect = closeLabelGO.GetComponent<RectTransform>();
            SetFullScreen(closeLabelRect);
            
            AddLocalizedTextAuto(closeLabelGO, "Settings", "Close");

            // Wire up AboutModal component
            aboutModal.modalPanel = panelGO;
            aboutModal.backdrop = backdropGO;
            aboutModal.closeButton = closeButton;
            
            // Use reflection to set private fields
            SerializedObject serializedAbout = new SerializedObject(aboutModal);
            serializedAbout.FindProperty("versionText").objectReferenceValue = versionText;
            serializedAbout.FindProperty("licensesText").objectReferenceValue = licensesText;
            serializedAbout.ApplyModifiedProperties();
            
            // Also wire backdrop to close on click
            backdropButton.onClick.AddListener(() => aboutModal.Hide());

            EditorUtility.SetDirty(aboutModalGO);
            EditorUtility.SetDirty(settingsCanvas.gameObject);

            Debug.Log("[AboutModalBuilder] About Modal created successfully!");
            EditorUtility.DisplayDialog("Success",
                "About Modal created!\n\n" +
                "Next steps:\n" +
                "1. Assign aboutModal reference in SettingsControllerPhase2\n" +
                "2. Test: Click About in Settings\n" +
                "3. Customize version/licenses if needed",
                "OK");
            
            Selection.activeGameObject = aboutModalGO;
        }

        private GameObject CreateText(Transform parent, string name, string text, int fontSize)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            
            RectTransform rect = go.AddComponent<RectTransform>();
            
            TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = Color.white;
            
            return go;
        }

        private void SetFullScreen(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private void SetFullWidth(RectTransform rect)
        {
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(1, 1);
            rect.pivot = new Vector2(0.5f, 1);
            rect.anchoredPosition = Vector2.zero;
        }
    }
}
