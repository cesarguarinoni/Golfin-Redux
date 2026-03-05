using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;
using static Golfin.UI.LocalizationEditorHelper;

namespace Golfin.UI
{
    /// <summary>
    /// Unity Editor tool to build About accordion submenu UI hierarchy.
    /// Menu: Tools → GOLFIN → Build About Submenu
    /// </summary>
    public class AboutSubmenuBuilder : EditorWindow
    {
        private Transform aboutRow;
        
        [MenuItem("Tools/GOLFIN/Build About Submenu")]
        public static void ShowWindow()
        {
            var window = GetWindow<AboutSubmenuBuilder>("Build About Submenu");
            window.minSize = new Vector2(400, 200);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Build About Submenu", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            EditorGUILayout.HelpBox(
                "This will create the About submenu hierarchy inside the About row:\n" +
                "• AboutSubmenu container\n" +
                "• APP VERSION section with version text\n" +
                "• LICENCES section with licenses list\n" +
                "• AboutSubmenu component with references wired\n\n" +
                "Pattern matches UserProfile/SoundSettings/Language submenus.",
                MessageType.Info
            );

            EditorGUILayout.Space();

            aboutRow = EditorGUILayout.ObjectField("About Row", aboutRow, typeof(Transform), true) as Transform;

            EditorGUILayout.Space();

            using (new EditorGUI.DisabledScope(aboutRow == null))
            {
                if (GUILayout.Button("Build About Submenu", GUILayout.Height(40)))
                {
                    BuildSubmenu();
                }
            }
        }

        private void BuildSubmenu()
        {
            if (aboutRow == null)
            {
                EditorUtility.DisplayDialog("Error", "Please assign About Row!", "OK");
                return;
            }

            // Check if already exists
            Transform existing = aboutRow.Find("AboutSubmenu");
            if (existing != null)
            {
                bool overwrite = EditorUtility.DisplayDialog("Submenu Exists",
                    "About submenu already exists. Overwrite?",
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

            // Create About Submenu container
            GameObject submenuGO = new GameObject("AboutSubmenu");
            submenuGO.transform.SetParent(aboutRow, false);
            
            RectTransform submenuRect = submenuGO.AddComponent<RectTransform>();
            // Anchor to bottom of parent row (0,0) to (1,0)
            submenuRect.anchorMin = new Vector2(0, 0);
            submenuRect.anchorMax = new Vector2(1, 0);
            submenuRect.pivot = new Vector2(0.5f, 1); // Top-center pivot
            submenuRect.anchoredPosition = new Vector2(0, -50); // Offset below row
            submenuRect.sizeDelta = new Vector2(0, 280); // Width matches parent, height 280px
            
            // Add AboutSubmenu component
            AboutSubmenu aboutSubmenu = submenuGO.AddComponent<AboutSubmenu>();
            
            // Add VerticalLayoutGroup for content
            VerticalLayoutGroup vlg = submenuGO.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(40, 40, 20, 20);
            vlg.spacing = 15f;
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            // 1. APP VERSION Section
            GameObject versionSectionGO = CreateSectionHeader(submenuGO.transform, "VersionSection", "APP VERSION");
            AddLocalizedTextAuto(versionSectionGO, "Settings", "About", "App Version");
            
            // Version Text (will be populated by AboutSubmenu)
            GameObject versionTextGO = CreateText(submenuGO.transform, "VersionText", "GOLFIN Redux\nVersion 1.0.0", 16);
            TMP_Text versionText = versionTextGO.GetComponent<TMP_Text>();
            versionText.alignment = TextAlignmentOptions.Left;
            versionText.color = new Color(0.8f, 0.8f, 0.8f, 1f);
            versionText.enableWordWrapping = true;
            versionText.overflowMode = TextOverflowModes.Overflow;
            
            LayoutElement versionLayout = versionTextGO.AddComponent<LayoutElement>();
            versionLayout.preferredHeight = 50;

            // 2. LICENCES Section
            GameObject licensesSectionGO = CreateSectionHeader(submenuGO.transform, "LicencesSection", "LICENCES");
            AddLocalizedTextAuto(licensesSectionGO, "Settings", "About", "Licences");
            
            // Licenses Text (will be populated by AboutSubmenu)
            GameObject licensesTextGO = CreateText(submenuGO.transform, "LicensesText", "MIT License\nGPL\nApache License\nBSD License", 14);
            TMP_Text licensesText = licensesTextGO.GetComponent<TMP_Text>();
            licensesText.alignment = TextAlignmentOptions.TopLeft;
            licensesText.color = new Color(0.7f, 0.7f, 0.7f, 1f);
            licensesText.enableWordWrapping = true;
            licensesText.overflowMode = TextOverflowModes.Overflow;
            
            LayoutElement licensesLayout = licensesTextGO.AddComponent<LayoutElement>();
            licensesLayout.preferredHeight = 140;

            // Wire up AboutSubmenu component using SerializedObject
            SerializedObject serializedAbout = new SerializedObject(aboutSubmenu);
            serializedAbout.FindProperty("versionText").objectReferenceValue = versionText;
            serializedAbout.FindProperty("licensesText").objectReferenceValue = licensesText;
            serializedAbout.ApplyModifiedProperties();

            EditorUtility.SetDirty(submenuGO);
            EditorUtility.SetDirty(aboutRow.gameObject);

            Debug.Log("[AboutSubmenuBuilder] About submenu created successfully!");
            EditorUtility.DisplayDialog("Success",
                "About submenu created!\n\n" +
                "Next steps:\n" +
                "1. Add SettingsMenuItem component to About Row\n" +
                "2. Assign submenu container reference\n" +
                "3. Add About Row to accordion in SettingsControllerPhase2\n" +
                "4. Test: Click About → Expands inline!",
                "OK");
            
            Selection.activeGameObject = submenuGO;
        }

        private GameObject CreateSectionHeader(Transform parent, string name, string text)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            
            RectTransform rect = go.AddComponent<RectTransform>();
            
            TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = 14;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = new Color(0.6f, 0.6f, 0.6f, 1f); // Gray
            tmp.alignment = TextAlignmentOptions.Left;
            
            LayoutElement layout = go.AddComponent<LayoutElement>();
            layout.preferredHeight = 20;
            
            return go;
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
            tmp.alignment = TextAlignmentOptions.Left;
            
            return go;
        }
    }
}
