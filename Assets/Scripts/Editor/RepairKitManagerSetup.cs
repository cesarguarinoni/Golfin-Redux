#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Finds or creates RepairKitManager on the Managers GameObject.
///
/// Run: GOLFIN/Setup/Repair Kit Manager
/// </summary>
public static class RepairKitManagerSetup
{
    [MenuItem("GOLFIN/Setup/Repair Kit Manager")]
    public static void Setup()
    {
        // Find existing RepairKitManager (including inactive)
        var existing = Object.FindObjectOfType<RepairKitManager>(includeInactive: true);
        if (existing != null)
        {
            Debug.Log($"[RepairKitManagerSetup] RepairKitManager already exists on '{existing.gameObject.name}'.");
            Selection.activeGameObject = existing.gameObject;
            return;
        }

        // Find the Managers GameObject (same one as ClubManager, RewardPointsManager)
        var clubMgr = Object.FindObjectOfType<ClubManager>(includeInactive: true);
        GameObject managersGO;

        if (clubMgr != null)
        {
            managersGO = clubMgr.gameObject;
            Debug.Log($"[RepairKitManagerSetup] Found Managers GameObject: '{managersGO.name}'.");
        }
        else
        {
            // Fallback: find or create a Managers object
            managersGO = GameObject.Find("Managers");
            if (managersGO == null)
            {
                managersGO = new GameObject("Managers");
                Debug.Log("[RepairKitManagerSetup] Created new 'Managers' GameObject.");
            }
        }

        var repairKitMgr = managersGO.AddComponent<RepairKitManager>();
        EditorUtility.SetDirty(managersGO);
        Selection.activeGameObject = managersGO;

        Debug.Log($"[RepairKitManagerSetup] RepairKitManager added to '{managersGO.name}'. " +
                  "Save the scene to persist.");
    }
}
#endif
