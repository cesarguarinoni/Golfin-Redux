using UnityEngine;
using UnityEditor;
using UnityEngine.UI;

/// <summary>
/// Editor script to connect Settings Button to Settings Screen
/// Handles both Button and PressableButton components
/// </summary>
public class ConnectSettingsButton
{
    [MenuItem("Tools/GOLFIN/Connect Settings Button")]
    public static void ConnectSettingsToButton()
    {
        Debug.Log("[GOLFIN] Searching for Settings Button in scene...");

        // Find Settings Screen first
        SettingsScreen settingsScreen = FindSettingsScreen();
        if (settingsScreen == null)
        {
            Debug.LogError("[GOLFIN] SettingsScreen not found! Generate it first using Tools → GOLFIN → Generate Settings Screen");
            return;
        }

        // Find Settings Button by multiple methods (Button OR PressableButton)
        ButtonInfo buttonInfo = FindSettingsButtonInfo();
        if (buttonInfo.button == null && buttonInfo.pressableButton == null)
        {
            Debug.LogError("[GOLFIN] Settings Button not found! See suggestions below.");
            LogSettingsButtonSuggestions();
            return;
        }

        // Add Settings Screen show listener to the correct button type
        if (buttonInfo.button != null)
        {
            // Handle Unity Button
            buttonInfo.button.onClick.RemoveAllListeners();
            buttonInfo.button.onClick.AddListener(() => OpenSettingsScreen(settingsScreen));
            Debug.Log("[GOLFIN] ✅ Unity Button successfully connected to Settings Screen!");
            Debug.Log("[GOLFIN] Button found: " + buttonInfo.button.name + " (Type: Unity Button)");
        }
        else if (buttonInfo.pressableButton != null)
        {
            // Handle PressableButton  
            buttonInfo.pressableButton.onClick.RemoveAllListeners();
            buttonInfo.pressableButton.onClick.AddListener(() => OpenSettingsScreen(settingsScreen));
            Debug.Log("[GOLFIN] ✅ PressableButton successfully connected to Settings Screen!");
            Debug.Log("[GOLFIN] Button found: " + buttonInfo.pressableButton.name + " (Type: PressableButton)");
        }

        Debug.Log("[GOLFIN] Test: Press the Settings Button (top-right) to open Settings Screen");
    }

    private static void OpenSettingsScreen(SettingsScreen settingsScreen)
    {
        Debug.Log("[GOLFIN] Opening Settings Screen via Settings Button");
        
        // Make Settings Screen visible and interactive
        GameObject settingsGO = settingsScreen.gameObject;
        settingsGO.SetActive(true);
        
        // Trigger Settings Screen enter logic with proper fade-in
        settingsScreen.OnScreenEnter();
    }

    private static SettingsScreen FindSettingsScreen()
    {
        // Try multiple methods to find Settings Screen
        SettingsScreen settingsScreen = Object.FindAnyObjectByType<SettingsScreen>();
        
        if (settingsScreen == null)
        {
            GameObject settingsGO = GameObject.Find("SettingsScreen");
            if (settingsGO != null)
            {
                settingsScreen = settingsGO.GetComponent<SettingsScreen>();
            }
        }
        
        if (settingsScreen == null)
        {
            SettingsScreen[] allSettings = Resources.FindObjectsOfTypeAll<SettingsScreen>();
            foreach (var settings in allSettings)
            {
                if (settings.gameObject.scene.name != null)
                {
                    settingsScreen = settings;
                    break;
                }
            }
        }
        
        return settingsScreen;
    }

    private struct ButtonInfo
    {
        public Button button;
        public PressableButton pressableButton;
    }

    private static ButtonInfo FindSettingsButtonInfo()
    {
        ButtonInfo result = new ButtonInfo();

        // Method 1: Search for GameObject with common Settings button names
        string[] possibleNames = { 
            "SettingsButton", "Settings", "SettingButton", "settings", 
            "OptionsButton", "Options", "GearButton", "Gear",
            "MenuButton", "Menu"
        };
        
        foreach (string name in possibleNames)
        {
            GameObject buttonGO = GameObject.Find(name);
            if (buttonGO != null)
            {
                // Try Unity Button first
                Button unityButton = buttonGO.GetComponent<Button>();
                if (unityButton != null)
                {
                    Debug.Log("[GOLFIN] Found Settings Button (Unity Button) by name: " + name);
                    result.button = unityButton;
                    return result;
                }

                // Try PressableButton second
                PressableButton pressableButton = buttonGO.GetComponent<PressableButton>();
                if (pressableButton != null)
                {
                    Debug.Log("[GOLFIN] Found Settings Button (PressableButton) by name: " + name);
                    result.pressableButton = pressableButton;
                    return result;
                }
            }
        }

        // Method 2: Search all buttons in Canvas (Unity Button + PressableButton)
        Canvas canvas = Object.FindAnyObjectByType<Canvas>();
        if (canvas != null)
        {
            // Search Unity Buttons
            Button[] allUnityButtons = canvas.GetComponentsInChildren<Button>(true);
            foreach (Button button in allUnityButtons)
            {
                if (IsLikelySettingsButton(button.name, button.transform.parent?.name))
                {
                    Debug.Log("[GOLFIN] Found potential Settings Button (Unity Button): " + button.name);
                    result.button = button;
                    return result;
                }
            }

            // Search PressableButtons
            PressableButton[] allPressableButtons = canvas.GetComponentsInChildren<PressableButton>(true);
            foreach (PressableButton button in allPressableButtons)
            {
                if (IsLikelySettingsButton(button.name, button.transform.parent?.name))
                {
                    Debug.Log("[GOLFIN] Found potential Settings Button (PressableButton): " + button.name);
                    result.pressableButton = button;
                    return result;
                }
            }
        }

        return result; // Both null if not found
    }

    private static bool IsLikelySettingsButton(string buttonName, string parentName)
    {
        string name = buttonName?.ToLower() ?? "";
        string parent = parentName?.ToLower() ?? "";
        
        return name.Contains("setting") || name.Contains("gear") || 
               name.Contains("option") || name.Contains("menu") ||
               parent.Contains("top") || parent.Contains("header");
    }

    private static void LogSettingsButtonSuggestions()
    {
        Debug.Log("[GOLFIN] === SETTINGS BUTTON SEARCH HELP ===");
        
        Canvas canvas = Object.FindAnyObjectByType<Canvas>();
        if (canvas != null)
        {
            Button[] unityButtons = canvas.GetComponentsInChildren<Button>(true);
            PressableButton[] pressableButtons = canvas.GetComponentsInChildren<PressableButton>(true);
            
            Debug.Log("[GOLFIN] Found " + unityButtons.Length + " Unity Button components:");
            for (int i = 0; i < unityButtons.Length && i < 10; i++)
            {
                Button button = unityButtons[i];
                string parentName = button.transform.parent?.name ?? "No Parent";
                Debug.Log("[GOLFIN] - " + button.name + " (Parent: " + parentName + ") [Unity Button]");
            }
            
            Debug.Log("[GOLFIN] Found " + pressableButtons.Length + " PressableButton components:");
            for (int i = 0; i < pressableButtons.Length && i < 10; i++)
            {
                PressableButton button = pressableButtons[i];
                string parentName = button.transform.parent?.name ?? "No Parent";
                Debug.Log("[GOLFIN] - " + button.name + " (Parent: " + parentName + ") [PressableButton]");
            }
        }
        
        Debug.Log("[GOLFIN] MANUAL CONNECTION:");
        Debug.Log("[GOLFIN] 1. Find your Settings Button GameObject in the Hierarchy");
        Debug.Log("[GOLFIN] 2. Rename it to 'SettingsButton' (exact name)");
        Debug.Log("[GOLFIN] 3. Make sure it has Button or PressableButton component");
        Debug.Log("[GOLFIN] 4. Run this tool again");
    }

    [MenuItem("Tools/GOLFIN/Disconnect Settings Button")]
    public static void DisconnectSettingsButton()
    {
        ButtonInfo buttonInfo = FindSettingsButtonInfo();
        
        if (buttonInfo.button != null)
        {
            buttonInfo.button.onClick.RemoveAllListeners();
            Debug.Log("[GOLFIN] Unity Button listeners cleared");
        }
        else if (buttonInfo.pressableButton != null)
        {
            buttonInfo.pressableButton.onClick.RemoveAllListeners();
            Debug.Log("[GOLFIN] PressableButton listeners cleared");
        }
        else
        {
            Debug.LogWarning("[GOLFIN] Settings button not found for disconnection");
        }
    }
    
    [MenuItem("Tools/GOLFIN/Debug Settings Button")]
    public static void DebugSettingsButton()
    {
        Debug.Log("[GOLFIN] === SETTINGS BUTTON DEBUG INFO ===");
        
        Canvas canvas = Object.FindAnyObjectByType<Canvas>();
        if (canvas != null)
        {
            Button[] unityButtons = canvas.GetComponentsInChildren<Button>(true);
            PressableButton[] pressableButtons = canvas.GetComponentsInChildren<PressableButton>(true);
            
            Debug.Log("[GOLFIN] Unity Buttons (" + unityButtons.Length + " total):");
            foreach (Button button in unityButtons)
            {
                Vector3 pos = button.transform.position;
                string parentName = button.transform.parent?.name ?? "Root";
                Debug.Log("[GOLFIN] - " + button.name + " | Parent: " + parentName + " | Position: " + pos + " | Active: " + button.gameObject.activeInHierarchy);
            }
            
            Debug.Log("[GOLFIN] PressableButtons (" + pressableButtons.Length + " total):");
            foreach (PressableButton button in pressableButtons)
            {
                Vector3 pos = button.transform.position;
                string parentName = button.transform.parent?.name ?? "Root";
                Debug.Log("[GOLFIN] - " + button.name + " | Parent: " + parentName + " | Position: " + pos + " | Active: " + button.gameObject.activeInHierarchy);
            }
        }
        
        // Check for Settings Screen too
        SettingsScreen settings = FindSettingsScreen();
        Debug.Log("[GOLFIN] Settings Screen found: " + (settings != null) + (settings != null ? " (" + settings.name + ")" : ""));
    }
}