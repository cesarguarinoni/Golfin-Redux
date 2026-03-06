using UnityEngine;
using UnityEditor;

namespace Golfin.UI
{
    /// <summary>
    /// Fix submenu positioning so they appear below their parent row, not at absolute positions
    /// </summary>
    public class FixSubmenuPositioning : EditorWindow
    {
        [MenuItem("Tools/GOLFIN/Fix Submenu Positioning")]
        public static void ShowWindow()
        {
            Fix();
        }

        private static void Fix()
        {
            var allMenuItems = FindObjectsOfType<SettingsMenuItem>();
            
            if (allMenuItems.Length == 0)
            {
                EditorUtility.DisplayDialog("No Menu Items", 
                    "No SettingsMenuItem components found in the scene.", 
                    "OK");
                return;
            }

            int fixedCount = 0;
            string report = "Submenu Positioning Fix Report:\n\n";

            foreach (var menuItem in allMenuItems)
            {
                // Use reflection to get private submenuContainer field
                var field = typeof(SettingsMenuItem).GetField("submenuContainer", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                
                if (field != null)
                {
                    var submenu = field.GetValue(menuItem) as GameObject;
                    
                    if (submenu != null)
                    {
                        var rect = submenu.GetComponent<RectTransform>();
                        if (rect != null)
                        {
                            // Fix the anchoring: should be anchored to bottom of parent row
                            // This makes it appear RIGHT BELOW the row, not at an absolute position
                            
                            // Set anchors to bottom-stretch
                            rect.anchorMin = new Vector2(0, 0);  // Bottom-left
                            rect.anchorMax = new Vector2(1, 0);  // Bottom-right
                            rect.pivot = new Vector2(0.5f, 1);   // Top-center of submenu
                            
                            // Position at bottom of parent (Y=0 because anchored to bottom)
                            rect.anchoredPosition = new Vector2(0, 0);
                            
                            // Width stretches with parent, height controlled by script
                            // Don't change sizeDelta here - let script control it
                            
                            report += $"✅ Fixed: {menuItem.name} / {submenu.name}\n";
                            report += $"   Anchor: Bottom-stretch (0,0 to 1,0)\n";
                            report += $"   Position: (0, 0)\n\n";
                            
                            EditorUtility.SetDirty(submenu);
                            fixedCount++;
                        }
                    }
                }
            }

            if (fixedCount > 0)
            {
                report += $"\n✅ Fixed {fixedCount} submenu positioning issues!\n";
                report += "Submenus will now appear directly below their parent rows.";
            }
            else
            {
                report += "\n⚠️ No submenus found to fix.";
            }

            Debug.Log(report);
            EditorUtility.DisplayDialog("Fix Complete", report, "OK");
        }
    }
}
