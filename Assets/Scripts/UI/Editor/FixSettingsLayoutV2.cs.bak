using UnityEngine;
using UnityEngine.UI;
using UnityEditor;

namespace Golfin.UI
{
    /// <summary>
    /// Fix Settings Screen layout issues V2 - properly configure menu rows for VerticalLayoutGroup
    /// </summary>
    public class FixSettingsLayoutV2 : EditorWindow
    {
        private Transform settingsList;

        [MenuItem("Tools/GOLFIN/Fix Settings Layout V2")]
        public static void ShowWindow()
        {
            var window = GetWindow<FixSettingsLayoutV2>("Fix Layout V2");
            window.minSize = new Vector2(350, 200);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Fix Settings Layout V2", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            EditorGUILayout.HelpBox(
                "This fixes the stacked/overlapping menu items issue.\n\n" +
                "It will:\n" +
                "- Add VerticalLayoutGroup to SettingsList\n" +
                "- Add LayoutElement to each menu row\n" +
                "- Reset RectTransform anchors to work with layout\n" +
                "- Set proper preferred heights",
                MessageType.Info
            );

            EditorGUILayout.Space();

            settingsList = EditorGUILayout.ObjectField("Settings List", settingsList, typeof(Transform), true) as Transform;

            EditorGUILayout.Space();

            using (new EditorGUI.DisabledScope(settingsList == null))
            {
                if (GUILayout.Button("Fix Layout", GUILayout.Height(40)))
                {
                    FixLayout();
                }
            }

            EditorGUILayout.Space();

            if (GUILayout.Button("Remove Layout Groups (Undo)", GUILayout.Height(30)))
            {
                RemoveLayoutGroups();
            }
        }

        private void FixLayout()
        {
            if (settingsList == null)
            {
                EditorUtility.DisplayDialog("Error", "Please assign the SettingsList GameObject!", "OK");
                return;
            }

            // Remove any existing layout components first
            var existingLayout = settingsList.GetComponent<VerticalLayoutGroup>();
            if (existingLayout != null) DestroyImmediate(existingLayout);

            var existingSizeFitter = settingsList.GetComponent<ContentSizeFitter>();
            if (existingSizeFitter != null) DestroyImmediate(existingSizeFitter);

            // Add VerticalLayoutGroup to SettingsList
            var layoutGroup = settingsList.gameObject.AddComponent<VerticalLayoutGroup>();
            layoutGroup.spacing = 5f;
            layoutGroup.childAlignment = TextAnchor.UpperCenter;
            layoutGroup.childForceExpandWidth = true;
            layoutGroup.childForceExpandHeight = false;
            layoutGroup.childControlWidth = true;
            layoutGroup.childControlHeight = true;
            layoutGroup.padding = new RectOffset(0, 0, 5, 5);

            // Add ContentSizeFitter
            var sizeFitter = settingsList.gameObject.AddComponent<ContentSizeFitter>();
            sizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Fix each child (menu row)
            int fixedCount = 0;
            foreach (Transform child in settingsList)
            {
                FixMenuRow(child);
                fixedCount++;
            }

            EditorUtility.SetDirty(settingsList.gameObject);

            Debug.Log($"[FixLayout] ✅ Fixed {fixedCount} menu rows!");
            EditorUtility.DisplayDialog("Success", 
                $"Settings layout fixed!\n\n" +
                $"Fixed {fixedCount} menu rows.\n" +
                $"Submenus will now expand/collapse properly.", 
                "OK");
        }

        private void FixMenuRow(Transform row)
        {
            // Reset RectTransform to work with VerticalLayoutGroup
            var rect = row.GetComponent<RectTransform>();
            if (rect != null)
            {
                // Set anchors to stretch horizontally, but not vertically
                rect.anchorMin = new Vector2(0, 1);
                rect.anchorMax = new Vector2(1, 1);
                rect.pivot = new Vector2(0.5f, 1);
                
                // Reset position
                rect.anchoredPosition = Vector2.zero;
                
                // Width will be controlled by layout, height needs to be set
                rect.sizeDelta = new Vector2(0, rect.sizeDelta.y);
            }

            // Add or update LayoutElement
            var layoutElement = row.GetComponent<LayoutElement>();
            if (layoutElement == null)
            {
                layoutElement = row.gameObject.AddComponent<LayoutElement>();
            }

            // Set preferred height (adjust if your rows are different sizes)
            float preferredHeight = 80f; // Default menu row height
            
            // Check if this row has a SettingsMenuItem (it might have a submenu)
            var menuItem = row.GetComponent<SettingsMenuItem>();
            if (menuItem != null)
            {
                // Rows with submenus need flexible height
                layoutElement.preferredHeight = 80f;
                layoutElement.flexibleHeight = 1f; // Allow expansion
            }
            else
            {
                // Simple rows have fixed height
                layoutElement.preferredHeight = 80f;
                layoutElement.flexibleHeight = 0f;
            }

            // Ensure minimum height
            layoutElement.minHeight = 60f;

            EditorUtility.SetDirty(row.gameObject);
        }

        private void RemoveLayoutGroups()
        {
            if (settingsList == null)
            {
                EditorUtility.DisplayDialog("Error", "Please assign the SettingsList GameObject!", "OK");
                return;
            }

            // Remove layout components
            var layoutGroup = settingsList.GetComponent<VerticalLayoutGroup>();
            if (layoutGroup != null) DestroyImmediate(layoutGroup);

            var sizeFitter = settingsList.GetComponent<ContentSizeFitter>();
            if (sizeFitter != null) DestroyImmediate(sizeFitter);

            // Remove LayoutElements from children
            foreach (Transform child in settingsList)
            {
                var layoutElement = child.GetComponent<LayoutElement>();
                if (layoutElement != null) DestroyImmediate(layoutElement);
            }

            EditorUtility.SetDirty(settingsList.gameObject);

            Debug.Log("[FixLayout] Layout groups removed (undo complete)");
            EditorUtility.DisplayDialog("Done", "Layout groups removed. You're back to manual positioning.", "OK");
        }
    }
}
