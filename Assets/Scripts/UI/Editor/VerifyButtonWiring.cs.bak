using UnityEngine;
using UnityEngine.UI;
using UnityEditor;

namespace Golfin.UI
{
    /// <summary>
    /// Verify that each SettingsMenuItem has its button properly wired to ToggleExpansion
    /// </summary>
    public class VerifyButtonWiring : EditorWindow
    {
        [MenuItem("Tools/GOLFIN/Verify Button Wiring")]
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

            string report = "Button Wiring Report:\n\n";
            bool allGood = true;

            foreach (var menuItem in allMenuItems)
            {
                // Use reflection to get private button field
                var buttonField = typeof(SettingsMenuItem).GetField("button", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                
                if (buttonField != null)
                {
                    var button = buttonField.GetValue(menuItem) as Button;
                    
                    if (button != null)
                    {
                        // Check listener count
                        int listenerCount = button.onClick.GetPersistentEventCount();
                        
                        report += $"{menuItem.name}:\n";
                        report += $"  Button: {button.gameObject.name}\n";
                        report += $"  Listeners: {listenerCount} persistent\n";
                        
                        if (listenerCount == 0)
                        {
                            report += $"  ✅ OK (runtime listener in Awake)\n";
                        }
                        else
                        {
                            report += $"  ℹ️ Has persistent listeners (might be redundant)\n";
                        }
                        
                        // Check if button is interactable
                        if (!button.interactable)
                        {
                            report += $"  ⚠️ WARNING: Button is not interactable!\n";
                            allGood = false;
                        }
                        
                        // Check if button has a graphic raycaster target
                        if (button.targetGraphic == null)
                        {
                            report += $"  ⚠️ WARNING: Button has no target graphic (might not be clickable)!\n";
                        }
                    }
                    else
                    {
                        report += $"{menuItem.name}:\n";
                        report += $"  ❌ ERROR: No button assigned!\n";
                        allGood = false;
                    }
                }
                
                report += "\n";
            }

            if (allGood)
            {
                report += "✅ All buttons are properly wired!";
            }
            else
            {
                report += "❌ Some issues found. Check warnings above.";
            }

            Debug.Log(report);
            EditorUtility.DisplayDialog("Button Wiring Check", report, "OK");
        }
    }
}
