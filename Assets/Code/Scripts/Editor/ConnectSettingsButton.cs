using UnityEngine;
using UnityEditor;
using UnityEngine.UI;

/// <summary>
/// Editor script to connect Settings Button to Settings Screen
/// Searches for SettingsButton by GameObject name and component type
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

        // Find Settings Button by multiple methods
        PressableButton settingsButton = FindSettingsButton();
        if (settingsButton == null)
        {
            Debug.LogError("[GOLFIN] Settings Button not found! See suggestions below.");
            LogSettingsButtonSuggestions();
            return;
        }

        // Clear existing listeners to avoid duplicates
        settingsButton.onClick.RemoveAllListeners();

        // Add Settings Screen show listener
        settingsButton.onClick.AddListener(() => {
            Debug.Log("[GOLFIN] Opening Settings Screen via Settings Button");
            
            // Make Settings Screen visible and interactive
            GameObject settingsGO = settingsScreen.gameObject;
            settingsGO.SetActive(true);
            
            // Trigger Settings Screen enter logic with proper fade-in
            settingsScreen.OnScreenEnter();
        });

        Debug.Log("[GOLFIN] ✅ Settings Button successfully connected to Settings Screen!");
        Debug.Log("[GOLFIN] Settings Button found: " + settingsButton.name + " (Parent: " + settingsButton.transform.parent?.name + ")");
        Debug.Log("[GOLFIN] Test: Press the Settings Button (top-right) to open Settings Screen");
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

    private static PressableButton FindSettingsButton()
    {
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
                PressableButton button = buttonGO.GetComponent<PressableButton>();
                if (button != null)
                {
                    Debug.Log("[GOLFIN] Found Settings Button by name: " + name);
                    return button;
                }
            }
        }

        // Method 2: Search for PressableButton in top-right area (Canvas → Top Bar area)
        Canvas canvas = Object.FindAnyObjectByType<Canvas>();
        if (canvas != null)
        {
            PressableButton[] allButtons = canvas.GetComponentsInChildren<PressableButton>(true);
            
            foreach (PressableButton button in allButtons)
            {
                // Check if button name or parent name suggests it's a settings button
                string buttonName = button.name.ToLower();
                string parentName = button.transform.parent?.name?.ToLower() ?? "";
                
                if (buttonName.Contains("setting") || buttonName.Contains("gear") || 
                    buttonName.Contains("option") || buttonName.Contains("menu") ||
                    parentName.Contains("top") || parentName.Contains("header"))
                {
                    Debug.Log("[GOLFIN] Found potential Settings Button: " + button.name + " (Parent: " + parentName + ")");
                    return button;
                }
            }
        }

        // Method 3: Ask user to identify the button manually
        return null;
    }

    private static void LogSettingsButtonSuggestions()
    {
        Debug.Log("[GOLFIN] === SETTINGS BUTTON SEARCH HELP ===");
        
        Canvas canvas = Object.FindAnyObjectByType<Canvas>();
        if (canvas != null)
        {
            PressableButton[] allButtons = canvas.GetComponentsInChildren<PressableButton>(true);
            Debug.Log("[GOLFIN] Found " + allButtons.Length + " PressableButton components in scene:");
            
            for (int i = 0; i < allButtons.Length && i < 10; i++) // Limit to first 10
            {
                PressableButton button = allButtons[i];
                string parentName = button.transform.parent?.name ?? "No Parent";
                Debug.Log("[GOLFIN] - " + button.name + " (Parent: " + parentName + ")");
            }
        }
        
        Debug.Log("[GOLFIN] MANUAL CONNECTION:");
        Debug.Log("[GOLFIN] 1. Find your Settings Button GameObject in the Hierarchy");
        Debug.Log("[GOLFIN] 2. Rename it to 'SettingsButton' (exact name)");
        Debug.Log("[GOLFIN] 3. Run this tool again");
    }

    [MenuItem("Tools/GOLFIN/Disconnect Settings Button")]
    public static void DisconnectSettingsButton()
    {
        PressableButton settingsButton = FindSettingsButton();
        if (settingsButton != null)
        {
            settingsButton.onClick.RemoveAllListeners();
            Debug.Log("[GOLFIN] Settings button listeners cleared");
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
            PressableButton[] allButtons = canvas.GetComponentsInChildren<PressableButton>(true);
            Debug.Log("[GOLFIN] Found " + allButtons.Length + " total PressableButton components:");
            
            foreach (PressableButton button in allButtons)
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