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

        // Find Settings Screen in scene  
        SettingsScreen settingsScreen = Object.FindAnyObjectByType<SettingsScreen>();
        if (settingsScreen == null)
        {
            Debug.LogError("[GOLFIN] SettingsScreen not found! Generate it first using Tools → GOLFIN → Generate Settings Screen");
            return;
        }

        // Get navMore button via reflection (since it's private SerializeField)
        var navMoreField = typeof(HomeScreen).GetField("navMore", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        PressableButton navMoreButton = navMoreField?.GetValue(homeScreen) as PressableButton;

        if (navMoreButton == null)
        {
            Debug.LogError("[GOLFIN] navMore button not found or not assigned in HomeScreen!");
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
            
            // Trigger Settings Screen enter logic
            settingsScreen.OnScreenEnter();
            
            // Optionally use ScreenManager for proper transition
            if (ScreenManager.Instance != null)
            {
                ScreenManager.Instance.TransitionTo(settingsScreen);
            }
        });

        Debug.Log("[GOLFIN] ✅ navMore button successfully connected to Settings Screen!");
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
}