#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Finds or creates BagManager on the Managers GameObject.
/// Run: GOLFIN/Setup/Bag Manager
/// </summary>
public static class BagManagerSetup
{
    [MenuItem("GOLFIN/Setup/Bag Manager")]
    public static void Setup()
    {
        var existing = Object.FindObjectOfType<BagManager>(includeInactive: true);
        if (existing != null)
        {
            Debug.Log($"[BagManagerSetup] BagManager already exists on '{existing.gameObject.name}'.");
            Selection.activeGameObject = existing.gameObject;
            return;
        }

        // Find the Managers GameObject (same one as ClubManager, RepairKitManager)
        var clubMgr = Object.FindObjectOfType<ClubManager>(includeInactive: true);
        GameObject managersGO;

        if (clubMgr != null)
        {
            managersGO = clubMgr.gameObject;
            Debug.Log($"[BagManagerSetup] Found Managers GameObject: '{managersGO.name}'.");
        }
        else
        {
            managersGO = GameObject.Find("Managers");
            if (managersGO == null)
            {
                managersGO = new GameObject("Managers");
                Debug.Log("[BagManagerSetup] Created new 'Managers' GameObject.");
            }
        }

        managersGO.AddComponent<BagManager>();
        EditorUtility.SetDirty(managersGO);
        Selection.activeGameObject = managersGO;

        Debug.Log($"[BagManagerSetup] BagManager added to '{managersGO.name}'. Save the scene to persist.");
    }
}
#endif
