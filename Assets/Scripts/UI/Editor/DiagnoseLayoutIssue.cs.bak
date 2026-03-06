using UnityEngine;
using UnityEngine.UI;
using UnityEditor;

namespace Golfin.UI
{
    /// <summary>
    /// Diagnose layout issues - check all layout components and their settings
    /// </summary>
    public class DiagnoseLayoutIssue : EditorWindow
    {
        private Transform settingsList;

        [MenuItem("Tools/GOLFIN/Diagnose Layout Issue")]
        public static void ShowWindow()
        {
            var window = GetWindow<DiagnoseLayoutIssue>("Layout Diagnostics");
            window.minSize = new Vector2(400, 300);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Layout Diagnostics", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            settingsList = EditorGUILayout.ObjectField("Settings List", settingsList, typeof(Transform), true) as Transform;

            EditorGUILayout.Space();

            if (GUILayout.Button("Diagnose", GUILayout.Height(40)))
            {
                Diagnose();
            }

            EditorGUILayout.Space();

            if (GUILayout.Button("Auto-Fix Issues", GUILayout.Height(40)))
            {
                AutoFix();
            }
        }

        private void Diagnose()
        {
            if (settingsList == null)
            {
                EditorUtility.DisplayDialog("Error", "Please assign SettingsList!", "OK");
                return;
            }

            string report = "=== LAYOUT DIAGNOSTICS ===\n\n";

            // Check SettingsList
            report += "SETTINGS LIST:\n";
            var vlg = settingsList.GetComponent<VerticalLayoutGroup>();
            if (vlg != null)
            {
                report += $"✅ VerticalLayoutGroup found\n";
                report += $"   Child Alignment: {vlg.childAlignment}\n";
                if (vlg.childAlignment != TextAnchor.UpperCenter && vlg.childAlignment != TextAnchor.UpperLeft)
                {
                    report += $"   ⚠️ WARNING: Should be UpperCenter, not {vlg.childAlignment}!\n";
                }
                report += $"   Spacing: {vlg.spacing}\n";
                report += $"   Control Width: {vlg.childControlWidth}\n";
                report += $"   Control Height: {vlg.childControlHeight}\n";
            }
            else
            {
                report += "❌ No VerticalLayoutGroup\n";
            }

            var csf = settingsList.GetComponent<ContentSizeFitter>();
            if (csf != null)
            {
                report += $"✅ ContentSizeFitter found\n";
                report += $"   Vertical Fit: {csf.verticalFit}\n";
                if (csf.horizontalFit != ContentSizeFitter.FitMode.Unconstrained)
                {
                    report += $"   ⚠️ Horizontal Fit: {csf.horizontalFit} (should be Unconstrained)\n";
                }
            }

            report += "\n";

            // Check each row
            report += "MENU ROWS:\n";
            foreach (Transform child in settingsList)
            {
                report += $"\n{child.name}:\n";
                
                var rect = child.GetComponent<RectTransform>();
                if (rect != null)
                {
                    report += $"   Anchor: ({rect.anchorMin.x},{rect.anchorMin.y}) to ({rect.anchorMax.x},{rect.anchorMax.y})\n";
                    report += $"   Pivot: ({rect.pivot.x},{rect.pivot.y})\n";
                    if (rect.pivot.y != 1f)
                    {
                        report += $"   ⚠️ Pivot Y should be 1.0, is {rect.pivot.y}\n";
                    }
                }

                var le = child.GetComponent<LayoutElement>();
                if (le != null)
                {
                    report += $"   ✅ LayoutElement: preferred={le.preferredHeight}, flexible={le.flexibleHeight}\n";
                }
                else
                {
                    report += $"   ❌ No LayoutElement\n";
                }

                var rowCsf = child.GetComponent<ContentSizeFitter>();
                if (rowCsf != null)
                {
                    report += $"   ⚠️ ContentSizeFitter on row! This may interfere.\n";
                    report += $"      Vertical: {rowCsf.verticalFit}, Horizontal: {rowCsf.horizontalFit}\n";
                }
            }

            Debug.Log(report);
            EditorUtility.DisplayDialog("Diagnostics", report, "OK");
        }

        private void AutoFix()
        {
            if (settingsList == null)
            {
                EditorUtility.DisplayDialog("Error", "Please assign SettingsList!", "OK");
                return;
            }

            int fixes = 0;
            string report = "AUTO-FIX REPORT:\n\n";

            // Fix SettingsList
            var vlg = settingsList.GetComponent<VerticalLayoutGroup>();
            if (vlg != null && vlg.childAlignment != TextAnchor.UpperCenter)
            {
                vlg.childAlignment = TextAnchor.UpperCenter;
                report += "✅ Fixed VerticalLayoutGroup alignment → UpperCenter\n";
                fixes++;
            }

            // Fix each row
            foreach (Transform child in settingsList)
            {
                var rect = child.GetComponent<RectTransform>();
                if (rect != null && rect.pivot.y != 1f)
                {
                    rect.pivot = new Vector2(rect.pivot.x, 1f);
                    report += $"✅ Fixed {child.name} pivot → (0.5, 1)\n";
                    fixes++;
                }

                // Remove ContentSizeFitter from rows (it interferes)
                var rowCsf = child.GetComponent<ContentSizeFitter>();
                if (rowCsf != null)
                {
                    DestroyImmediate(rowCsf);
                    report += $"✅ Removed ContentSizeFitter from {child.name}\n";
                    fixes++;
                }

                EditorUtility.SetDirty(child.gameObject);
            }

            EditorUtility.SetDirty(settingsList.gameObject);

            report += $"\nFixed {fixes} issues!";
            Debug.Log(report);
            EditorUtility.DisplayDialog("Auto-Fix Complete", report, "OK");
        }
    }
}
