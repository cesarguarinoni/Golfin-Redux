#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Creates ItemDatabaseCSV + ItemManager components on the Managers GameObject.
/// Also wires the Items.csv TextAsset and sets Script Execution Order.
///
/// Run: GOLFIN/Setup/Item Manager
/// </summary>
public static class ItemManagerSetup
{
    [MenuItem("GOLFIN/Setup/Item Manager")]
    public static void Setup()
    {
        // ── ItemDatabaseCSV ───────────────────────────────────────────────────

        var existingDB = Object.FindObjectOfType<Golfin.Inventory.ItemDatabaseCSV>(includeInactive: true);
        if (existingDB != null)
        {
            Debug.Log($"[ItemManagerSetup] ItemDatabaseCSV already exists on '{existingDB.gameObject.name}'.");
        }

        // ── ItemManager ───────────────────────────────────────────────────────

        var existingMgr = Object.FindObjectOfType<ItemManager>(includeInactive: true);
        if (existingMgr != null)
        {
            Debug.Log($"[ItemManagerSetup] ItemManager already exists on '{existingMgr.gameObject.name}'.");
            Selection.activeGameObject = existingMgr.gameObject;
            return;
        }

        // Find the Managers GameObject
        var clubMgr = Object.FindObjectOfType<ClubManager>(includeInactive: true);
        GameObject managersGO;

        if (clubMgr != null)
        {
            managersGO = clubMgr.gameObject;
            Debug.Log($"[ItemManagerSetup] Found Managers GameObject: '{managersGO.name}'.");
        }
        else
        {
            managersGO = GameObject.Find("Managers");
            if (managersGO == null)
            {
                managersGO = new GameObject("Managers");
                Debug.Log("[ItemManagerSetup] Created new 'Managers' GameObject.");
            }
        }

        // Add ItemDatabaseCSV if missing
        var dbComp = managersGO.GetComponent<Golfin.Inventory.ItemDatabaseCSV>();
        if (dbComp == null)
        {
            dbComp = managersGO.AddComponent<Golfin.Inventory.ItemDatabaseCSV>();

            // Wire Items.csv TextAsset
            var csvAsset = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Data/Items.csv");
            if (csvAsset != null)
            {
                var so = new SerializedObject(dbComp);
                so.FindProperty("itemsCSV").objectReferenceValue = csvAsset;
                so.ApplyModifiedProperties();
                Debug.Log("[ItemManagerSetup] itemsCSV wired to Assets/Data/Items.csv");
            }
            else
            {
                Debug.LogWarning("[ItemManagerSetup] Items.csv not found at Assets/Data/Items.csv — wire manually.");
            }
        }

        // Add ItemManager
        managersGO.AddComponent<ItemManager>();

        // Script Execution Order: ItemDatabaseCSV = -90, ItemManager = -80
        SetExecutionOrder("ItemDatabaseCSV", -90);
        SetExecutionOrder("ItemManager",     -80);

        EditorUtility.SetDirty(managersGO);
        Selection.activeGameObject = managersGO;

        Debug.Log($"[ItemManagerSetup] ItemDatabaseCSV + ItemManager added to '{managersGO.name}'. Save the scene to persist.");
    }

    private static void SetExecutionOrder(string scriptName, int order)
    {
        foreach (var monoScript in MonoImporter.GetAllRuntimeMonoScripts())
        {
            if (monoScript.GetClass() != null && monoScript.GetClass().Name == scriptName)
            {
                if (MonoImporter.GetExecutionOrder(monoScript) != order)
                {
                    MonoImporter.SetExecutionOrder(monoScript, order);
                    Debug.Log($"[ItemManagerSetup] Execution order for {scriptName} set to {order}.");
                }
                return;
            }
        }
        Debug.LogWarning($"[ItemManagerSetup] Could not find MonoScript for '{scriptName}'.");
    }
}
#endif
