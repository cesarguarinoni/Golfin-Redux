using UnityEngine;
using UnityEditor;

namespace Golfin.UI
{
    /// <summary>
    /// Verify that SettingsControllerPhase2 has all menu items properly assigned
    /// </summary>
    public class VerifyAccordionSetup : EditorWindow
    {
        [MenuItem("Tools/GOLFIN/Verify Accordion Setup")]
        public static void ShowWindow()
        {
            Verify();
        }

        private static void Verify()
        {
            var controller = FindObjectOfType<SettingsControllerPhase2>();
            
            if (controller == null)
            {
                EditorUtility.DisplayDialog("No Controller", 
                    "No SettingsControllerPhase2 component found in the scene.", 
                    "OK");
                return;
            }

            string report = "Accordion Setup Report:\n\n";
            bool allGood = true;

            // Use reflection to check private fields
            var userProfileField = typeof(SettingsControllerPhase2).GetField("userProfileItem");
            var soundSettingsField = typeof(SettingsControllerPhase2).GetField("soundSettingsItem");
            var languageField = typeof(SettingsControllerPhase2).GetField("languageItem");

            report += $"Controller: {controller.gameObject.name}\n\n";

            // Check User Profile
            if (userProfileField != null)
            {
                var userProfileItem = userProfileField.GetValue(controller) as SettingsMenuItem;
                if (userProfileItem != null)
                {
                    report += $"✅ User Profile Item: {userProfileItem.gameObject.name}\n";
                }
                else
                {
                    report += $"❌ User Profile Item: NOT ASSIGNED!\n";
                    allGood = false;
                }
            }

            // Check Sound Settings
            if (soundSettingsField != null)
            {
                var soundSettingsItem = soundSettingsField.GetValue(controller) as SettingsMenuItem;
                if (soundSettingsItem != null)
                {
                    report += $"✅ Sound Settings Item: {soundSettingsItem.gameObject.name}\n";
                }
                else
                {
                    report += $"❌ Sound Settings Item: NOT ASSIGNED!\n";
                    allGood = false;
                }
            }

            // Check Language
            if (languageField != null)
            {
                var languageItem = languageField.GetValue(controller) as SettingsMenuItem;
                if (languageItem != null)
                {
                    report += $"✅ Language Item: {languageItem.gameObject.name}\n";
                }
                else
                {
                    report += $"❌ Language Item: NOT ASSIGNED!\n";
                    allGood = false;
                }
            }

            report += "\n";

            // Check accordion items list
            var accordionItemsField = typeof(SettingsControllerPhase2).GetField("_accordionItems", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (accordionItemsField != null)
            {
                var accordionItems = accordionItemsField.GetValue(controller) as System.Collections.Generic.List<SettingsMenuItem>;
                if (accordionItems != null)
                {
                    report += $"Accordion Items List: {accordionItems.Count} items\n";
                    
                    if (accordionItems.Count == 0)
                    {
                        report += "⚠️ WARNING: No items in accordion list! Events won't work.\n";
                        allGood = false;
                    }
                }
            }

            if (allGood)
            {
                report += "\n✅ All accordion items are properly assigned!";
            }
            else
            {
                report += "\n❌ Some items are missing. Assign them in the Inspector:";
                report += "\n1. Select the GameObject with SettingsControllerPhase2";
                report += "\n2. In Inspector, find 'Menu Items with Accordion' section";
                report += "\n3. Drag each row (UserProfileRow, SoundSettingsRow, LanguageRow) into the fields";
            }

            Debug.Log(report);
            EditorUtility.DisplayDialog("Accordion Setup Check", report, "OK");
        }
    }
}
