using UnityEngine;
using UnityEditor;
using UnityEngine.UI;

/// <summary>
/// Editor script to connect HomeScreen navMore button to Settings Screen
/// </summary>
public class ConnectSettingsButton
{
    [MenuItem("Tools/GOLFIN/Connect Settings Button")]
    public static void ConnectSettingsToNavMore()
    {
        Debug.Log("[GOLFIN] Connecting navMore button to Settings Screen...");

        // Find HomeScreen in scene
        HomeScreen homeScreen = Object.FindAnyObjectByType<HomeScreen>();
        if (homeScreen == null)
        {
            Debug.LogError("[GOLFIN] HomeScreen not found in scene!");
            return;
        }

        // Find Settings Screen - check BOTH active and inactive objects
        SettingsScreen settingsScreen = null;
        
        // Method 1: Try to find by component (active objects only)
        settingsScreen = Object.FindAnyObjectByType<SettingsScreen>();
        
        // Method 2: If not found, search all objects including inactive
        if (settingsScreen == null)
        {
            GameObject settingsGO = GameObject.Find("SettingsScreen");
            if (settingsGO != null)
            {
                settingsScreen = settingsGO.GetComponent<SettingsScreen>();
                Debug.Log("[GOLFIN] Found inactive SettingsScreen GameObject");
            }
        }
        
        // Method 3: Search all objects in scene including inactive
        if (settingsScreen == null)
        {
            SettingsScreen[] allSettings = Resources.FindObjectsOfTypeAll<SettingsScreen>();
            foreach (var settings in allSettings)
            {
                // Make sure it's a scene object, not a prefab
                if (settings.gameObject.scene.name != null)
                {
                    settingsScreen = settings;
                    Debug.Log("[GOLFIN] Found SettingsScreen in scene (inactive objects search)");
                    break;
                }
            }
        }
        
        if (settingsScreen == null)
        {
            Debug.LogError("[GOLFIN] SettingsScreen not found! Generate it first using Tools → GOLFIN → Generate Settings Screen");
            Debug.LogError("[GOLFIN] Make sure the SettingsScreen GameObject is named 'SettingsScreen' and has SettingsScreen component");
            return;
        }

        // Get navMore button via reflection (since it's private SerializeField)
        var navMoreField = typeof(HomeScreen).GetField("navMore", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        PressableButton navMoreButton = navMoreField?.GetValue(homeScreen) as PressableButton;

        if (navMoreButton == null)
        {
            Debug.LogError("[GOLFIN] navMore button not found or not assigned in HomeScreen!");
            Debug.LogError("[GOLFIN] Make sure navMore button is assigned in HomeScreen Inspector");
            return;
        }

        // Clear existing listeners to avoid duplicates
        navMoreButton.onClick.RemoveAllListeners();

        // Add Settings Screen show listener
        navMoreButton.onClick.AddListener(() => {
            Debug.Log("[GOLFIN] Opening Settings Screen via navMore button");
            
            // Make Settings Screen visible and interactive
            GameObject settingsGO = settingsScreen.gameObject;
            settingsGO.SetActive(true);
            
            // Trigger Settings Screen enter logic with proper fade-in
            settingsScreen.OnScreenEnter();
        });

        Debug.Log("[GOLFIN] ✅ navMore button successfully connected to Settings Screen!");
        Debug.Log("[GOLFIN] Settings Screen found: " + settingsScreen.name + " (Active: " + settingsScreen.gameObject.activeInHierarchy + ")");
        Debug.Log("[GOLFIN] Test: Press the 'More' button in the bottom navigation to open Settings");
    }

    [MenuItem("Tools/GOLFIN/Disconnect Settings Button")]
    public static void DisconnectSettingsButton()
    {
        HomeScreen homeScreen = Object.FindAnyObjectByType<HomeScreen>();
        if (homeScreen == null)
        {
            Debug.LogWarning("[GOLFIN] HomeScreen not found");
            return;
        }

        var navMoreField = typeof(HomeScreen).GetField("navMore", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        PressableButton navMoreButton = navMoreField?.GetValue(homeScreen) as PressableButton;

        if (navMoreButton != null)
        {
            navMoreButton.onClick.RemoveAllListeners();
            Debug.Log("[GOLFIN] navMore button listeners cleared");
        }
    }
    
    [MenuItem("Tools/GOLFIN/Debug Settings Screen")]
    public static void DebugSettingsScreen()
    {
        Debug.Log("[GOLFIN] === SETTINGS SCREEN DEBUG INFO ===");
        
        // Search for SettingsScreen objects
        SettingsScreen[] allSettings = Resources.FindObjectsOfTypeAll<SettingsScreen>();
        Debug.Log("[GOLFIN] Found " + allSettings.Length + " SettingsScreen objects total");
        
        foreach (var settings in allSettings)
        {
            bool isSceneObject = settings.gameObject.scene.name != null;
            Debug.Log("[GOLFIN] - " + settings.name + " | Active: " + settings.gameObject.activeInHierarchy + " | Scene: " + isSceneObject);
        }
        
        // Search for SettingsScreen GameObject by name
        GameObject settingsGO = GameObject.Find("SettingsScreen");
        if (settingsGO != null)
        {
            SettingsScreen component = settingsGO.GetComponent<SettingsScreen>();
            Debug.Log("[GOLFIN] GameObject 'SettingsScreen' found | Has component: " + (component != null) + " | Active: " + settingsGO.activeInHierarchy);
        }
        else
        {
            Debug.Log("[GOLFIN] GameObject 'SettingsScreen' not found");
        }
    }
}