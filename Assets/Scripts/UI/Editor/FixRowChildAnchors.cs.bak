using UnityEngine;
using UnityEditor;

namespace Golfin.UI
{
    /// <summary>
    /// Fix row child anchors: Button/Icon/Label/Arrow should be top-anchored,
    /// not center-anchored, so they stay at the top when row expands
    /// </summary>
    public class FixRowChildAnchors : EditorWindow
    {
        private Transform settingsList;

        [MenuItem("Tools/GOLFIN/Fix Row Child Anchors")]
        public static void ShowWindow()
        {
            var window = GetWindow<FixRowChildAnchors>("Fix Child Anchors");
            window.minSize = new Vector2(350, 200);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Fix Row Child Anchors", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            EditorGUILayout.HelpBox(
                "When a row expands, its children (Button, Icon, Label, Arrow) " +
                "should stay at the TOP of the row, not shift to the middle.\n\n" +
                "This tool anchors all row children to the top of their parent row.",
                MessageType.Info
            );

            EditorGUILayout.Space();

            settingsList = EditorGUILayout.ObjectField("Settings List", settingsList, typeof(Transform), true) as Transform;

            EditorGUILayout.Space();

            using (new EditorGUI.DisabledScope(settingsList == null))
            {
                if (GUILayout.Button("Fix All Row Children", GUILayout.Height(40)))
                {
                    FixAllRows();
                }
            }
        }

        private void FixAllRows()
        {
            if (settingsList == null)
            {
                EditorUtility.DisplayDialog("Error", "Please assign SettingsList!", "OK");
                return;
            }

            int fixedCount = 0;
            string report = "Fixed Row Children:\n\n";

            foreach (Transform row in settingsList)
            {
                report += $"{row.name}:\n";
                
                foreach (Transform child in row)
                {
                    // Skip the submenu container (it should be bottom-anchored)
                    if (child.name.Contains("Submenu"))
                    {
                        report += $"  Skipped: {child.name} (submenu)\n";
                        continue;
                    }

                    var rect = child.GetComponent<RectTransform>();
                    if (rect != null)
                    {
                        // Get current anchor type
                        bool isStretched = (rect.anchorMax.x - rect.anchorMin.x) > 0.01f;
                        
                        if (isStretched)
                        {
                            // It's a stretched element (like Button background)
                            // Anchor to top-stretch: (0,1) to (1,1)
                            rect.anchorMin = new Vector2(0, 1);
                            rect.anchorMax = new Vector2(1, 1);
                            rect.pivot = new Vector2(0.5f, 1);
                            
                            report += $"  ✅ {child.name}: Top-Stretch\n";
                        }
                        else
                        {
                            // It's a fixed-position element (Icon, Label, Arrow)
                            // Keep horizontal anchor (left/right/center), set vertical to top
                            float horizontalAnchor = rect.anchorMin.x; // Could be 0 (left), 0.5 (center), 1 (right)
                            
                            rect.anchorMin = new Vector2(horizontalAnchor, 1); // Top
                            rect.anchorMax = new Vector2(horizontalAnchor, 1); // Top
                            rect.pivot = new Vector2(rect.pivot.x, 1); // Keep horizontal pivot, set vertical to top
                            
                            string hPos = horizontalAnchor == 0 ? "Left" : (horizontalAnchor == 1 ? "Right" : "Center");
                            report += $"  ✅ {child.name}: Top-{hPos}\n";
                        }
                        
                        EditorUtility.SetDirty(child.gameObject);
                        fixedCount++;
                    }
                }
                
                report += "\n";
            }

            report += $"Fixed {fixedCount} child elements!";
            Debug.Log(report);
            EditorUtility.DisplayDialog("Success", report, "OK");
        }
    }
}
