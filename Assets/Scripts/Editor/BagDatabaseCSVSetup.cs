#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Finds or creates BagDatabaseCSV on the Managers GameObject.
/// Run: GOLFIN/Setup/Bag Database CSV
///
/// Script Execution Order: -90 (before BagManager).
/// </summary>
public static class BagDatabaseCSVSetup
{
    [MenuItem("GOLFIN/Setup/Bag Database CSV")]
    public static void Setup()
    {
        var existing = Object.FindObjectOfType<BagDatabaseCSV>(includeInactive: true);
        if (existing != null)
        {
            Debug.Log($"[BagDatabaseCSVSetup] BagDatabaseCSV already exists on '{existing.gameObject.name}'.");
            Selection.activeGameObject = existing.gameObject;
            return;
        }

        // Co-locate with the other database singletons
        var clubMgr = Object.FindObjectOfType<ClubManager>(includeInactive: true);
        GameObject managersGO;

        if (clubMgr != null)
        {
            managersGO = clubMgr.gameObject;
            Debug.Log($"[BagDatabaseCSVSetup] Found Managers GameObject: '{managersGO.name}'.");
        }
        else
        {
            managersGO = GameObject.Find("Managers") ?? new GameObject("Managers");
        }

        managersGO.AddComponent<BagDatabaseCSV>();
        EditorUtility.SetDirty(managersGO);

        // Set execution order to -90 (before BagManager)
        var script = MonoScript.FromMonoBehaviour(managersGO.GetComponent<BagDatabaseCSV>());
        if (script != null)
        {
            MonoImporter.SetExecutionOrder(script, -90);
            Debug.Log("[BagDatabaseCSVSetup] Execution order set to -90.");
        }

        Selection.activeGameObject = managersGO;
        Debug.Log($"[BagDatabaseCSVSetup] BagDatabaseCSV added to '{managersGO.name}'. Assign Bags.csv in Inspector, then save.");
    }
}
#endif
