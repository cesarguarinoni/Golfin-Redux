using UnityEngine;
using UnityEngine.UI;
using UnityEditor;

namespace Golfin.UI
{
    /// <summary>
    /// Fix Settings Screen layout issues - adds VerticalLayoutGroup to SettingsList
    /// </summary>
    public class FixSettingsLayout : EditorWindow
    {
        private Transform settingsList;

        [MenuItem("Tools/GOLFIN/Fix Settings Layout")]
        public static void ShowWindow()
        {
            var window = GetWindow<FixSettingsLayout>("Fix Layout");
            window.minSize = new Vector2(350, 150);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Fix Settings Layout", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            EditorGUILayout.HelpBox(
                "This fixes the overlapping issue when submenus expand. " +
                "It adds a VerticalLayoutGroup to SettingsList so items move down automatically.",
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
        }

        private void FixLayout()
        {
            if (settingsList == null)
            {
                EditorUtility.DisplayDialog("Error", "Please assign the SettingsList GameObject!", "OK");
                return;
            }

            // Check if VerticalLayoutGroup already exists
            var existingLayout = settingsList.GetComponent<VerticalLayoutGroup>();
            if (existingLayout != null)
            {
                Debug.Log("[FixLayout] VerticalLayoutGroup already exists. Reconfiguring...");
                DestroyImmediate(existingLayout);
            }

            // Add VerticalLayoutGroup
            var layoutGroup = settingsList.gameObject.AddComponent<VerticalLayoutGroup>();
            
            // Configure settings
            layoutGroup.spacing = 10f;
            layoutGroup.childAlignment = TextAnchor.UpperCenter;
            layoutGroup.childForceExpandWidth = true;
            layoutGroup.childForceExpandHeight = false;
            layoutGroup.childControlWidth = true;
            layoutGroup.childControlHeight = true;
            layoutGroup.padding = new RectOffset(0, 0, 10, 10);

            // Also add ContentSizeFitter to automatically adjust size
            var sizeFitter = settingsList.GetComponent<ContentSizeFitter>();
            if (sizeFitter == null)
            {
                sizeFitter = settingsList.gameObject.AddComponent<ContentSizeFitter>();
            }
            sizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            EditorUtility.SetDirty(settingsList.gameObject);

            Debug.Log("[FixLayout] ✅ Settings layout fixed! Items will now move down when submenus expand.");
            EditorUtility.DisplayDialog("Success", 
                "Settings layout fixed!\n\n" +
                "Added VerticalLayoutGroup + ContentSizeFitter.\n" +
                "Items will now move down when submenus expand.", 
                "OK");
        }
    }
}
