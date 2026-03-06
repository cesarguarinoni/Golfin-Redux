#if UNITY_EDITOR
using UnityEditor;
using System.IO;

namespace Golfin.Editor
{
    /// <summary>
    /// Temporarily hides legacy menu items by renaming them
    /// Run this once to clean up the Tools menu
    /// </summary>
    public static class MenuItemRemover
    {
        [MenuItem("Tools/GOLFIN/!!! CLEANUP: Hide Legacy Menus", priority = 0)]
        public static void HideLegacyMenus()
        {
            string[] scriptsToRename = new string[]
            {
                "Assets/Scripts/UI/Editor/AboutSubmenuBuilder.cs",
                "Assets/Scripts/UI/Editor/DiagnoseLayoutIssue.cs",
                "Assets/Scripts/UI/Editor/FixRowChildAnchors.cs",
                "Assets/Scripts/UI/Editor/FixSettingsLayout.cs",
                "Assets/Scripts/UI/Editor/FixSettingsLayoutV2.cs",
                "Assets/Scripts/UI/Editor/FixSubmenuPositioning.cs",
                "Assets/Scripts/UI/Editor/LocalizeSettingsScreen.cs",
                "Assets/Scripts/UI/Editor/ScreenDeactivatorEditor.cs",
                "Assets/Scripts/UI/Editor/SettingsPhase2Builder.cs",
                "Assets/Scripts/UI/Editor/VerifyAccordionSetup.cs",
                "Assets/Scripts/UI/Editor/VerifyButtonWiring.cs",
                "Assets/Scripts/UI/Editor/VerifySubmenuParenting.cs"
            };
            
            int renamed = 0;
            foreach (var scriptPath in scriptsToRename)
            {
                if (File.Exists(scriptPath))
                {
                    string newPath = scriptPath + ".bak";
                    if (!File.Exists(newPath))
                    {
                        File.Move(scriptPath, newPath);
                        File.Move(scriptPath + ".meta", newPath + ".meta");
                        renamed++;
                    }
                }
            }
            
            AssetDatabase.Refresh();
            
            EditorUtility.DisplayDialog("Cleanup Complete",
                $"Renamed {renamed} legacy editor scripts.\n\n" +
                "Unity will recompile now.\n" +
                "After recompile, only Roster menus will be visible under Tools → GOLFIN.",
                "OK");
        }
        
        [MenuItem("Tools/GOLFIN/!!! RESTORE: Restore Legacy Menus", priority = 1)]
        public static void RestoreLegacyMenus()
        {
            string[] scriptsToRestore = new string[]
            {
                "Assets/Scripts/UI/Editor/AboutSubmenuBuilder.cs.bak",
                "Assets/Scripts/UI/Editor/DiagnoseLayoutIssue.cs.bak",
                "Assets/Scripts/UI/Editor/FixRowChildAnchors.cs.bak",
                "Assets/Scripts/UI/Editor/FixSettingsLayout.cs.bak",
                "Assets/Scripts/UI/Editor/FixSettingsLayoutV2.cs.bak",
                "Assets/Scripts/UI/Editor/FixSubmenuPositioning.cs.bak",
                "Assets/Scripts/UI/Editor/LocalizeSettingsScreen.cs.bak",
                "Assets/Scripts/UI/Editor/ScreenDeactivatorEditor.cs.bak",
                "Assets/Scripts/UI/Editor/SettingsPhase2Builder.cs.bak",
                "Assets/Scripts/UI/Editor/VerifyAccordionSetup.cs.bak",
                "Assets/Scripts/UI/Editor/VerifyButtonWiring.cs.bak",
                "Assets/Scripts/UI/Editor/VerifySubmenuParenting.cs.bak"
            };
            
            int restored = 0;
            foreach (var bakPath in scriptsToRestore)
            {
                if (File.Exists(bakPath))
                {
                    string originalPath = bakPath.Replace(".bak", "");
                    if (!File.Exists(originalPath))
                    {
                        File.Move(bakPath, originalPath);
                        File.Move(bakPath + ".meta", originalPath + ".meta");
                        restored++;
                    }
                }
            }
            
            AssetDatabase.Refresh();
            
            EditorUtility.DisplayDialog("Restore Complete",
                $"Restored {restored} legacy editor scripts.\n\n" +
                "Unity will recompile now.",
                "OK");
        }
    }
}
#endif
