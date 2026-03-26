#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Golfin.Inventory;

/// <summary>
/// Wires SerializeFields on BagSelectionModalController.
/// Run: GOLFIN/Wire/Bag Selection Modal
///
/// When the modal already exists in the scene, ONLY wires references — never
/// touches layout, transforms, colors, or components. This preserves Kai's styling.
///
/// When no modal exists, builds a plain-white prototype hierarchy from scratch
/// (intended as a first-pass placeholder before Kai styles it).
/// </summary>
public static class BagSelectionModalAutoWire
{
    private const string BagSlotPrefabPath       = "Assets/Prefabs/UI/Inventory/BagSlotPrefab.prefab";
    private const string BagSlotLockedPrefabPath = "Assets/Prefabs/UI/Inventory/BagSlotLockedPrefab.prefab";

    [MenuItem("GOLFIN/Wire/Bag Selection Modal")]
    public static void Build()
    {
        var existing = Object.FindObjectOfType<BagSelectionModalController>(includeInactive: true);

        if (existing != null)
        {
            // ── Rewire only — do NOT touch layout or styling ──────────────────
            Debug.Log("[BagSelectionAutoWire] Found existing BagSelectionModal — rewiring fields only.");
            RewireExisting(existing);
        }
        else
        {
            // ── First-time build ──────────────────────────────────────────────
            BuildFresh();
        }
    }

    // ── Rewire path (existing modal) ──────────────────────────────────────────

    private static void RewireExisting(BagSelectionModalController controller)
    {
        var root = controller.gameObject;
        var so   = new SerializedObject(controller);
        int wired = 0, failed = 0;

        // Find existing children by name — no hierarchy modifications
        var backdrop   = Find(root.transform,  "Backdrop");
        var modalPanel = Find(root.transform,  "ModalPanel");
        var bagGrid    = modalPanel != null ? Find(modalPanel, "BagGrid")      : null;
        var cancelBtn  = modalPanel != null ? Find(modalPanel, "CancelButton") : null;

        // bagSlotPrefab: prefer the asset prefab; fall back to scene child of BagGrid
        GameObject? bagSlotPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BagSlotPrefabPath);
        if (bagSlotPrefab == null && bagGrid != null)
            bagSlotPrefab = FindGO(bagGrid, "BagSlotPrefab");

        // bagSlotLockedPrefab: always from asset
        var lockedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BagSlotLockedPrefabPath);

        // Wire
        wired += SetProp(so, "backdrop",            backdrop);
        wired += SetProp(so, "modalPanel",           modalPanel?.gameObject);
        wired += SetPropComponent<Button>(so, "cancelButton", cancelBtn?.gameObject, ref failed);
        wired += SetProp(so, "bagGridParent",        bagGrid);
        wired += SetProp(so, "bagSlotPrefab",        bagSlotPrefab);

        if (lockedPrefab != null)
            wired += SetProp(so, "bagSlotLockedPrefab", lockedPrefab);
        else
        {
            Debug.LogWarning($"[BagSelectionAutoWire] BagSlotLockedPrefab not found at '{BagSlotLockedPrefabPath}'. Wire manually.");
            failed++;
        }

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(controller);

        WireDetailPanelReference(controller, ref wired, ref failed);
        WireCompareControllerReference(controller, ref wired, ref failed);

        EditorUtility.SetDirty(root);
        Debug.Log($"[BagSelectionAutoWire] Rewire done. {wired} fields wired, {failed} failed.");
        if (failed > 0)
            Debug.LogWarning("[BagSelectionAutoWire] Some fields failed — check warnings above.");

        Selection.activeGameObject = root;
    }

    // ── Fresh build path (no modal in scene yet) ──────────────────────────────

    private static void BuildFresh()
    {
        var inventoryScreen = GameObject.Find("InventoryScreen");
        Transform parent = inventoryScreen != null
            ? inventoryScreen.transform
            : (Object.FindObjectOfType<Canvas>(includeInactive: true)?.transform ?? null!);

        var root = new GameObject("BagSelectionModal");
        if (parent != null) root.transform.SetParent(parent, false);

        var rt = root.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        root.AddComponent<BagSelectionModalController>();

        var controller = root.GetComponent<BagSelectionModalController>();
        var so = new SerializedObject(controller);
        int wired = 0, failed = 0;

        // Backdrop
        var backdrop = CreateChild(root.transform, "Backdrop");
        FullStretch(backdrop); GetOrAdd<Image>(backdrop).color = new Color(0f, 0f, 0f, 0.75f);

        // ModalPanel
        var modalPanel = CreateChild(root.transform, "ModalPanel");
        Anchor(modalPanel, new Vector2(0.1f, 0.1f), new Vector2(0.9f, 0.9f));
        GetOrAdd<CanvasGroup>(modalPanel);

        // BagGrid
        var bagGrid = CreateChild(modalPanel.transform, "BagGrid");
        Anchor(bagGrid, new Vector2(0f, 0.2f), new Vector2(1f, 0.85f));
        var grid = GetOrAdd<GridLayoutGroup>(bagGrid);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 5; grid.cellSize = new Vector2(120f, 120f);
        grid.spacing = new Vector2(8f, 8f); grid.padding = new RectOffset(8, 8, 8, 8);

        // BagSlotPrefab scene template (placeholder — Kai will replace with real prefab)
        var prefab = CreateChild(bagGrid.transform, "BagSlotPrefab");
        GetOrAdd<Button>(prefab); GetOrAdd<Image>(prefab).color = new Color(0.15f, 0.15f, 0.15f, 1f);
        var img  = CreateChild(prefab.transform, "BagImage");  Anchor(img,  new Vector2(0.1f, 0.3f),  new Vector2(0.9f, 0.95f)); GetOrAdd<Image>(img);
        var lbl  = CreateChild(prefab.transform, "BagLabel");  Anchor(lbl,  new Vector2(0f, 0.05f),   new Vector2(1f, 0.3f));    var t = GetOrAdd<TextMeshProUGUI>(lbl); t.text = "BAG 1"; t.fontSize = 14; t.alignment = TextAlignmentOptions.Center; t.color = Color.white;
        var full = CreateChild(prefab.transform, "FullBadge"); Anchor(full, new Vector2(0f, 0.5f),    new Vector2(1f, 0.75f));   GetOrAdd<Image>(full).color = new Color(0.8f, 0.2f, 0.2f, 0.9f);
        var ftxt = CreateChild(full.transform,   "Text");      FullStretch(ftxt); var ft = GetOrAdd<TextMeshProUGUI>(ftxt); ft.text = "FULL"; ft.fontSize = 14; ft.alignment = TextAlignmentOptions.Center; ft.color = Color.white;
        full.SetActive(false);
        var equip = CreateChild(prefab.transform, "EquippedIcon"); Anchor(equip, new Vector2(0f, 0.7f), new Vector2(0.35f, 0.95f)); GetOrAdd<Image>(equip).color = new Color(0.2f, 0.8f, 0.2f, 1f);
        equip.SetActive(false);
        prefab.SetActive(false);

        // CancelButton
        var cancelBtn = CreateChild(modalPanel.transform, "CancelButton");
        Anchor(cancelBtn, new Vector2(0.2f, 0.03f), new Vector2(0.8f, 0.18f));
        GetOrAdd<Button>(cancelBtn); GetOrAdd<Image>(cancelBtn).color = new Color(0.3f, 0.3f, 0.3f, 1f);
        var ctxt = CreateChild(cancelBtn.transform, "Text"); FullStretch(ctxt);
        var ct = GetOrAdd<TextMeshProUGUI>(ctxt); ct.text = "CANCEL"; ct.fontSize = 18; ct.alignment = TextAlignmentOptions.Center; ct.color = Color.white;

        // Wire
        wired += SetProp(so, "backdrop",      backdrop);
        wired += SetProp(so, "modalPanel",    modalPanel.gameObject);
        wired += SetPropComponent<Button>(so, "cancelButton", cancelBtn.gameObject, ref failed);
        wired += SetProp(so, "bagGridParent", bagGrid);
        wired += SetProp(so, "bagSlotPrefab", prefab.gameObject);

        var lockedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BagSlotLockedPrefabPath);
        if (lockedPrefab != null) wired += SetProp(so, "bagSlotLockedPrefab", lockedPrefab);
        else { Debug.LogWarning($"[BagSelectionAutoWire] BagSlotLockedPrefab not found at '{BagSlotLockedPrefabPath}'."); failed++; }

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(controller);

        WireDetailPanelReference(controller, ref wired, ref failed);
        WireCompareControllerReference(controller, ref wired, ref failed);

        EditorUtility.SetDirty(root);
        Debug.Log($"[BagSelectionAutoWire] Fresh build done. {wired} fields wired, {failed} failed.");
        Selection.activeGameObject = root;
    }

    // ── Back-reference helpers ────────────────────────────────────────────────

    private static void WireDetailPanelReference(BagSelectionModalController modal,
        ref int wired, ref int failed)
    {
        var dp = Object.FindObjectOfType<ClubDetailPanel>(includeInactive: true);
        if (dp == null) { Debug.LogWarning("[BagSelectionAutoWire] ClubDetailPanel not found."); failed++; return; }
        var dpSO = new SerializedObject(dp);
        var prop = dpSO.FindProperty("bagSelectionModal");
        if (prop == null) { Debug.LogWarning("[BagSelectionAutoWire] 'bagSelectionModal' not on ClubDetailPanel."); failed++; return; }
        prop.objectReferenceValue = modal;
        dpSO.ApplyModifiedProperties(); EditorUtility.SetDirty(dp);
        wired++; Debug.Log("[BagSelectionAutoWire] Wired ClubDetailPanel.bagSelectionModal.");
    }

    private static void WireCompareControllerReference(BagSelectionModalController modal,
        ref int wired, ref int failed)
    {
        var cc = Object.FindObjectOfType<ClubCompareController>(includeInactive: true);
        if (cc == null) { Debug.LogWarning("[BagSelectionAutoWire] ClubCompareController not found."); failed++; return; }
        var ccSO = new SerializedObject(cc);
        var prop = ccSO.FindProperty("bagSelectionModal");
        if (prop == null) { Debug.LogWarning("[BagSelectionAutoWire] 'bagSelectionModal' not on ClubCompareController."); failed++; return; }
        prop.objectReferenceValue = modal;
        ccSO.ApplyModifiedProperties(); EditorUtility.SetDirty(cc);
        wired++; Debug.Log("[BagSelectionAutoWire] Wired ClubCompareController.bagSelectionModal.");
    }

    // ── Hierarchy helpers ─────────────────────────────────────────────────────

    private static Transform CreateChild(Transform parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        return go.transform;
    }

    private static T GetOrAdd<T>(Transform t) where T : Component => GetOrAdd<T>(t.gameObject);
    private static T GetOrAdd<T>(GameObject go) where T : Component
    {
        var c = go.GetComponent<T>();
        return c != null ? c : go.AddComponent<T>();
    }

    private static void FullStretch(Transform t)
    {
        var rt = t.GetComponent<RectTransform>() ?? t.gameObject.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    private static void Anchor(Transform t, Vector2 min, Vector2 max)
    {
        var rt = t.GetComponent<RectTransform>() ?? t.gameObject.AddComponent<RectTransform>();
        rt.anchorMin = min; rt.anchorMax = max;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    // Find helpers — read-only, no creation
    private static Transform? Find(Transform parent, string name) => parent.Find(name);
    private static GameObject? FindGO(Transform parent, string name) => parent.Find(name)?.gameObject;

    // ── SerializedObject helpers ──────────────────────────────────────────────

    private static int SetProp(SerializedObject so, string field, Object? value)
    {
        var prop = so.FindProperty(field);
        if (prop == null) { Debug.LogWarning($"[BagSelectionAutoWire] Field '{field}' not found."); return 0; }
        prop.objectReferenceValue = value;
        return 1;
    }

    private static int SetPropComponent<T>(SerializedObject so, string field,
        GameObject? go, ref int failed) where T : Component
    {
        if (go == null) { failed++; return 0; }
        var prop = so.FindProperty(field);
        if (prop == null) { failed++; return 0; }
        prop.objectReferenceValue = go.GetComponent<T>();
        return 1;
    }
}
#endif
