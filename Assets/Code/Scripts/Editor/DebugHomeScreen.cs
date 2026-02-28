using UnityEngine;
using UnityEditor;

/// <summary>
/// Debug script to check HomeScreen fields and SettingsScreen location
/// </summary>
public class DebugHomeScreen
{
    [MenuItem("Tools/GOLFIN/Debug HomeScreen Fields")]
    public static void DebugFields()
    {
        Debug.Log("[DEBUG] === HOMESCREEN FIELDS CHECK ===");
        
        HomeScreen homeScreen = Object.FindAnyObjectByType<HomeScreen>();
        if (homeScreen == null)
        {
            Debug.LogError("[DEBUG] HomeScreen not found in scene!");
            return;
        }
        
        Debug.Log("[DEBUG] HomeScreen found: " + homeScreen.name);
        
        // Check if fields exist via reflection
        var settingsButtonField = typeof(HomeScreen).GetField("settingsButton", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var onSettingsField = typeof(HomeScreen).GetField("OnSettingsPressed", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        
        Debug.Log("[DEBUG] settingsButton field exists: " + (settingsButtonField != null));
        Debug.Log("[DEBUG] OnSettingsPressed field exists: " + (onSettingsField != null));
        
        // Check for SettingsScreen
        GameObject settingsScreen = GameObject.Find("SettingsScreen");
        Debug.Log("[DEBUG] SettingsScreen GameObject found: " + (settingsScreen != null));
        if (settingsScreen != null)
        {
            Debug.Log("[DEBUG] SettingsScreen location: " + GetGameObjectPath(settingsScreen));
        }
        
        // Check for SettingsButton
        GameObject settingsButton = GameObject.Find("SettingsButton");
        Debug.Log("[DEBUG] SettingsButton GameObject found: " + (settingsButton != null));
        if (settingsButton != null)
        {
            Debug.Log("[DEBUG] SettingsButton location: " + GetGameObjectPath(settingsButton));
        }
    }
    
    private static string GetGameObjectPath(GameObject obj)
    {
        string path = obj.name;
        Transform parent = obj.transform.parent;
        while (parent != null)
        {
            path = parent.name + "/" + path;
            parent = parent.parent;
        }
        return path;
    }
}