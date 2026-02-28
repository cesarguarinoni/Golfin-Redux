using UnityEngine;  
using UnityEditor;  
using UnityEngine.UI;  
  
public class BottomNavPositionFix  
{ 
    [MenuItem("Tools/GOLFIN/Fix Bottom Nav Position")]  
    public static void FixBottomNavPosition()  
    {  
        Debug.Log("[GOLFIN] Fixing Bottom Navigation positioning...");  
        HomeScreen[] homeScreens = Object.FindObjectsByType<HomeScreen>(FindObjectsSortMode.None);  
        int fixedObjects = 0;  
  
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
                    Debug.Log("[GOLFIN] Found HorizontalLayoutGroup - Old padding: " + hlg.padding.ToString());  
                    hlg.padding = new RectOffset(60, 60, 40, 20);  
                    Debug.Log("[GOLFIN] Updated padding: Left=60, Right=60, Top=40, Bottom=20");  
                    EditorUtility.SetDirty(bottomNav.gameObject);  
                    fixedObjects++;  
                }  
                else  
                {  
                    Debug.LogWarning("[GOLFIN] No HorizontalLayoutGroup found on " + bottomNav.name);  
                }  
            }  
            else  
            {  
                Debug.LogWarning("[GOLFIN] Bottom navigation not found in " + homeScreen.name);  
            }  
        } 
  
        Debug.Log("[GOLFIN] Bottom Nav position fix complete! Fixed " + fixedObjects + " objects");  
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
