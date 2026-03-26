#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Golfin.Inventory;

/// <summary>
/// Wires SerializeFields on BagSelectionModalController.
/// Run: GOLFIN/Wire/Bag Selection Modal
///
/// ONLY wires references. Never creates, modifies, or destroys any GameObjects.
/// </summary>
public static class BagSelectionModalAutoWire
{
    private const string BagSlotPrefabPath       = "Assets/Prefabs/UI/Inventory/BagSlotPrefab.prefab";
    private const string BagSlotLockedPrefabPath = "Assets/Prefabs/UI/Inventory/BagSlotLockedPrefab.prefab";

    [MenuItem("GOLFIN/Wire/Bag Selection Modal")]
    public static void Wire()
    {
        var controller = Object.FindObjectOfType<BagSelectionModalController>(includeInactive: true);
        if (controller == null)
        {
            Debug.LogError("[BagSelectionAutoWire] BagSelectionModalController not found in scene.");
            return;
        }

        var root = controller.gameObject;
        var so   = new SerializedObject(controller);
        int wired = 0, failed = 0;

        // ── Find children by name (read-only) ─────────────────────────────────
        var backdrop   = root.transform.Find("Backdrop");
        var modalPanel = root.transform.Find("ModalPanel");
        var bagGrid    = modalPanel?.Find("BagGrid");
        var cancelBtn  = modalPanel?.Find("CancelButton");

        // bagSlotPrefab: prefer asset, fall back to scene child
        var bagSlotPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BagSlotPrefabPath)
                         ?? bagGrid?.Find("BagSlotPrefab")?.gameObject;

        var lockedPrefab  = AssetDatabase.LoadAssetAtPath<GameObject>(BagSlotLockedPrefabPath);

        // ── Wire ──────────────────────────────────────────────────────────────
        wired += Set(so, "backdrop",            backdrop?.gameObject);
        wired += Set(so, "modalPanel",          modalPanel?.gameObject);
        wired += SetComp<Button>(so, "cancelButton", cancelBtn?.gameObject, ref failed);
        wired += Set(so, "bagGridParent",       bagGrid);
        wired += Set(so, "bagSlotPrefab",       bagSlotPrefab);

        if (lockedPrefab != null)
            wired += Set(so, "bagSlotLockedPrefab", lockedPrefab);
        else
        {
            Debug.LogWarning($"[BagSelectionAutoWire] BagSlotLockedPrefab not found at '{BagSlotLockedPrefabPath}'. Wire manually in Inspector.");
            failed++;
        }

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(controller);

        // ── Wire back-references ──────────────────────────────────────────────
        var dp = Object.FindObjectOfType<ClubDetailPanel>(includeInactive: true);
        if (dp != null)
        {
            var dpSO = new SerializedObject(dp);
            var p    = dpSO.FindProperty("bagSelectionModal");
            if (p != null) { p.objectReferenceValue = controller; dpSO.ApplyModifiedProperties(); EditorUtility.SetDirty(dp); wired++; }
        }

        var cc = Object.FindObjectOfType<ClubCompareController>(includeInactive: true);
        if (cc != null)
        {
            var ccSO = new SerializedObject(cc);
            var p    = ccSO.FindProperty("bagSelectionModal");
            if (p != null) { p.objectReferenceValue = controller; ccSO.ApplyModifiedProperties(); EditorUtility.SetDirty(cc); wired++; }
        }

        Selection.activeGameObject = root;
        Debug.Log($"[BagSelectionAutoWire] Done. {wired} wired, {failed} failed.");
    }

    private static int Set(SerializedObject so, string field, Object? value)
    {
        var p = so.FindProperty(field);
        if (p == null) { Debug.LogWarning($"[BagSelectionAutoWire] Field '{field}' not found."); return 0; }
        p.objectReferenceValue = value;
        return 1;
    }

    private static int SetComp<T>(SerializedObject so, string field, GameObject? go, ref int failed) where T : Component
    {
        if (go == null) { failed++; return 0; }
        var p = so.FindProperty(field);
        if (p == null) { failed++; return 0; }
        p.objectReferenceValue = go.GetComponent<T>();
        return 1;
    }
}
#endif
