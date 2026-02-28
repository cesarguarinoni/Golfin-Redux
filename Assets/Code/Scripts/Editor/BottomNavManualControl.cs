using UnityEngine;  
using UnityEditor;  
using UnityEngine.UI;  
  
public class BottomNavManualControl  
{ 
    [MenuItem("Tools/GOLFIN/Enable Manual Bottom Nav Control")]  
    public static void EnableManualControl()  
    {  
        Debug.Log("[GOLFIN] Enabling manual control for Bottom Navigation...");  
        HomeScreen[] homeScreens = Object.FindObjectsByType<HomeScreen>(FindObjectsSortMode.None);  
        int processedObjects = 0;  
  
        foreach (var homeScreen in homeScreens)  
        {  
            Transform bottomNav = FindChildRecursive(homeScreen.transform, "BottomNavBar");  
            if (bottomNav == null)  
                bottomNav = FindChildRecursive(homeScreen.transform, "bottomNav");  
            if (bottomNav == null)  
                bottomNav = FindChildRecursive(homeScreen.transform, "Bottom"); 
  
            if (bottomNav != null)  
            {  
                HorizontalLayoutGroup hlg = bottomNav.GetComponent<HorizontalLayoutGroup>();  
                if (hlg != null)  
                {  
                    Debug.Log("[GOLFIN] Disabling HorizontalLayoutGroup for manual control");  
                    hlg.enabled = false;  
                }  
  
                SetupManualButtonPositions(bottomNav);  
                EditorUtility.SetDirty(bottomNav.gameObject);  
                processedObjects++;  
            }  
            else  
            {  
                Debug.LogWarning("[GOLFIN] Bottom navigation not found in " + homeScreen.name);  
            }  
        } 
  
        Debug.Log("[GOLFIN] Manual control enabled for " + processedObjects + " bottom nav objects");  
        Debug.Log("[GOLFIN] You can now manually adjust button positions in the Scene view");  
    }  
  
    private static void SetupManualButtonPositions(Transform bottomNav)  
    {  
        Debug.Log("[GOLFIN] Setting up manual button positions for " + bottomNav.name);  
  
        // Find all navigation buttons  
        string[] navNames = {"Home", "Shop", "Play", "Bag", "More"};  
        float containerWidth = 1010f; // Standard bottom nav width  
        float buttonSpacing = containerWidth / (navNames.Length + 1); 
  
        for (int i = 0; i < navNames.Length; i++)  
        {  
            Transform navButton = FindChildRecursive(bottomNav, navNames[i]);  
            if (navButton != null)  
            {  
                RectTransform rt = navButton.GetComponent<RectTransform>();  
                if (rt != null)  
                {  
                    // Set up manual positioning  
                    rt.anchorMin = new Vector2(0f, 0.5f);  
                    rt.anchorMax = new Vector2(0f, 0.5f);  
                    rt.pivot = new Vector2(0.5f, 0.5f);  
                    rt.sizeDelta = new Vector2(80f, 80f);  
  
                    // Position buttons evenly, but allow manual adjustment  
                    float xPos = (i + 1) * buttonSpacing - (containerWidth / 2f);  
                    rt.anchoredPosition = new Vector2(xPos, 0f);  
  
                    Debug.Log("[GOLFIN] Set manual position for " + navNames[i] + ": " + rt.anchoredPosition);  
                    EditorUtility.SetDirty(navButton.gameObject);  
                }  
            }  
        }  
    } 
  
    [MenuItem("Tools/GOLFIN/Re-enable Automatic Bottom Nav")]  
    public static void ReEnableAutomaticControl()  
    {  
        Debug.Log("[GOLFIN] Re-enabling automatic control for Bottom Navigation...");  
        HomeScreen[] homeScreens = Object.FindObjectsByType<HomeScreen>(FindObjectsSortMode.None);  
  
        foreach (var homeScreen in homeScreens)  
        {  
            Transform bottomNav = FindChildRecursive(homeScreen.transform, "BottomNavBar");  
            if (bottomNav == null) bottomNav = FindChildRecursive(homeScreen.transform, "bottomNav");  
            if (bottomNav == null) bottomNav = FindChildRecursive(homeScreen.transform, "Bottom");  
  
            if (bottomNav != null)  
            {  
                HorizontalLayoutGroup hlg = bottomNav.GetComponent<HorizontalLayoutGroup>();  
                if (hlg != null)  
                {  
                    hlg.enabled = true;  
                    Debug.Log("[GOLFIN] Re-enabled HorizontalLayoutGroup for " + bottomNav.name);  
                }  
            }  
        }  
    } 
  
    private static Transform FindChildRecursive(Transform parent, string childName)  
    {  
        for (int i = 0; i < parent.childCount; i++)  
        {  
            Transform child = parent.GetChild(i);  
            if (child.name == childName)  
                return child;  
            Transform found = FindChildRecursive(child, childName);  
            if (found != null)  
                return found;  
        }  
        return null;  
    }  
} 
