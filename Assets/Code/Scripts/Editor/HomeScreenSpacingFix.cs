using UnityEngine;  
using UnityEditor;  
  
public class HomeScreenSpacingFix  
{ 
    [MenuItem("Tools/GOLFIN/Fix Issue #7 - Vertical Spacing")]  
    public static void FixVerticalSpacing()  
    {  
        Debug.Log("[GOLFIN] Looking for HomeScreen objects...");  
        HomeScreen[] homeScreens = FindObjectsOfType<HomeScreen>();  
        foreach (var homeScreen in homeScreens)  
        {  
            Transform nextHole = homeScreen.transform.Find("NextHolePanel");  
            if (nextHole != null)  
            {  
                RectTransform rt = nextHole.GetComponent<RectTransform>();  
                Vector2 pos = rt.anchoredPosition;  
                pos.y -= 20f;  
                rt.anchoredPosition = pos;  
                EditorUtility.SetDirty(nextHole.gameObject);  
                Debug.Log("[GOLFIN] Fixed spacing for " + homeScreen.name);  
            }  
        }  
    }  
} 
