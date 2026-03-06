using UnityEngine;
using UnityEditor;

namespace Golfin.UI
{
    /// <summary>
    /// Verify that each SettingsMenuItem's submenu is actually a child of that row
    /// </summary>
    public class VerifySubmenuParenting : EditorWindow
    {
        [MenuItem("Tools/GOLFIN/Verify Submenu Parenting")]
        public static void ShowWindow()
        {
            Verify();
        }

        private static void Verify()
        {
            var allMenuItems = FindObjectsOfType<SettingsMenuItem>();
            
            if (allMenuItems.Length == 0)
            {
                EditorUtility.DisplayDialog("No Menu Items", 
                    "No SettingsMenuItem components found in the scene.", 
                    "OK");
                return;
            }

            bool allCorrect = true;
            string report = "Submenu Parenting Report:\n\n";

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
                        // Check if submenu is a child of this menuItem
                        bool isChild = submenu.transform.parent == menuItem.transform;
                        
                        string status = isChild ? "✅ OK" : "❌ WRONG PARENT";
                        string actualParent = submenu.transform.parent != null ? submenu.transform.parent.name : "ROOT";
                        
                        report += $"{status} | Row: {menuItem.name}\n";
                        report += $"  Submenu: {submenu.name}\n";
                        report += $"  Parent: {actualParent}\n";
                        report += $"  Expected: {menuItem.name}\n\n";
                        
                        if (!isChild)
                        {
                            allCorrect = false;
                        }
                    }
                    else
                    {
                        report += $"⚠️ WARN | Row: {menuItem.name}\n";
                        report += $"  No submenu assigned\n\n";
                    }
                }
            }

            if (allCorrect)
            {
                report += "\n✅ All submenus are correctly parented!";
            }
            else
            {
                report += "\n❌ Some submenus have incorrect parents!\n";
                report += "Fix: Drag each submenu to be a child of its row in Hierarchy.";
            }

            Debug.Log(report);
            EditorUtility.DisplayDialog("Submenu Parenting Check", report, "OK");
        }
    }
}
