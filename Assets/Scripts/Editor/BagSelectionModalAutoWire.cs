#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Golfin.Inventory;

/// <summary>
/// Builds the BagSelectionModal hierarchy and wires all SerializeFields.
/// Also wires bagSelectionModal on ClubDetailPanel and ClubCompareController.
///
/// Run: GOLFIN/Wire/Bag Selection Modal
///
/// Hierarchy created:
///   BagSelectionModal  (root — BagSelectionModalController)
///     Backdrop         (Image, dark overlay)
///     ModalPanel       (CanvasGroup for fade)
///       Title          (TMP — "CHOOSE A BAG")
///       BagGrid        (GridLayoutGroup — 5 cols × 2 rows)
///         BagSlotPrefab (disabled template — Button + children)
///           BagImage
///           BagLabel
///           CountLabel
///           FullBadge
///           EquippedIcon
///       CancelButton
///         Text         (TMP)
/// </summary>
public static class BagSelectionModalAutoWire
{
    [MenuItem("GOLFIN/Wire/Bag Selection Modal")]
    public static void Build()
    {
        // ── Find or create root ───────────────────────────────────────────────
        var existing = Object.FindObjectOfType<BagSelectionModalController>(includeInactive: true);
        GameObject root;

        if (existing != null)
        {
            root = existing.gameObject;
            Debug.Log("[BagSelectionAutoWire] Found existing BagSelectionModal — rewiring.");
        }
        else
        {
            // Parent: find the Canvas, place modal as direct child
            var canvas = Object.FindObjectOfType<Canvas>(includeInactive: true);
            Transform parent = canvas != null ? canvas.transform : null!;

            root = new GameObject("BagSelectionModal");
            if (parent != null) root.transform.SetParent(parent, false);

            var rt = root.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            root.AddComponent<BagSelectionModalController>();
            Debug.Log("[BagSelectionAutoWire] Created BagSelectionModal.");
        }

        var controller = root.GetComponent<BagSelectionModalController>();
        if (controller == null)
        {
            Debug.LogError("[BagSelectionAutoWire] No BagSelectionModalController found on root.");
            return;
        }

        var so = new SerializedObject(controller);
        int wired = 0, failed = 0;

        // ── Backdrop ──────────────────────────────────────────────────────────
        var backdrop = GetOrCreate(root.transform, "Backdrop");
        SetFullStretch(backdrop);
        var backdropImg = GetOrAdd<Image>(backdrop);
        backdropImg.color          = new Color(0f, 0f, 0f, 0.75f);
        backdropImg.raycastTarget  = true;

        // ── Modal Panel ───────────────────────────────────────────────────────
        var modalPanel = GetOrCreate(root.transform, "ModalPanel");
        var mpRT = SetRectTransform(modalPanel, new Vector2(0.1f, 0.1f), new Vector2(0.9f, 0.9f));
        GetOrAdd<CanvasGroup>(modalPanel);

        // ── Title ─────────────────────────────────────────────────────────────
        var title = GetOrCreate(modalPanel.transform, "Title");
        SetAnchored(title, new Vector2(0f, 0.85f), new Vector2(1f, 1f));
        var titleTMP = GetOrAdd<TextMeshProUGUI>(title);
        titleTMP.text      = "CHOOSE A BAG";
        titleTMP.fontSize  = 28;
        titleTMP.alignment = TextAlignmentOptions.Center;
        titleTMP.color     = Color.white;

        // ── Bag Grid ──────────────────────────────────────────────────────────
        var bagGrid = GetOrCreate(modalPanel.transform, "BagGrid");
        SetAnchored(bagGrid, new Vector2(0f, 0.2f), new Vector2(1f, 0.85f));
        var grid = GetOrAdd<GridLayoutGroup>(bagGrid);
        grid.constraint      = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 5;
        grid.cellSize        = new Vector2(120f, 120f);
        grid.spacing         = new Vector2(8f, 8f);
        grid.padding         = new RectOffset(8, 8, 8, 8);
        grid.childAlignment  = TextAnchor.UpperCenter;

        // ── Bag Slot Prefab (template, disabled) ──────────────────────────────
        var prefab = GetOrCreate(bagGrid.transform, "BagSlotPrefab");
        SetRectTransform(prefab, Vector2.zero, Vector2.zero); // sized by grid
        GetOrAdd<Button>(prefab);
        var prefabImg = GetOrAdd<Image>(prefab);
        prefabImg.color = new Color(0.15f, 0.15f, 0.15f, 1f);

        BuildSlotChild<Image>(prefab.transform, "BagImage",
            rt => { rt.anchorMin = new Vector2(0.1f, 0.3f); rt.anchorMax = new Vector2(0.9f, 0.95f);
                    rt.offsetMin = rt.offsetMax = Vector2.zero; });

        BuildSlotChild<TextMeshProUGUI>(prefab.transform, "BagLabel",
            rt => { rt.anchorMin = new Vector2(0f, 0.05f); rt.anchorMax = new Vector2(1f, 0.3f);
                    rt.offsetMin = rt.offsetMax = Vector2.zero; },
            tmp => { tmp.text = "BAG 1"; tmp.fontSize = 14; tmp.alignment = TextAlignmentOptions.Center; tmp.color = Color.white; });

        BuildSlotChild<TextMeshProUGUI>(prefab.transform, "CountLabel",
            rt => { rt.anchorMin = new Vector2(0.5f, 0.7f); rt.anchorMax = new Vector2(1f, 0.95f);
                    rt.offsetMin = rt.offsetMax = Vector2.zero; },
            tmp => { tmp.text = "0/8"; tmp.fontSize = 11; tmp.alignment = TextAlignmentOptions.Right; tmp.color = Color.gray; });

        var fullBadge = GetOrCreate(prefab.transform, "FullBadge");
        SetAnchored(fullBadge, new Vector2(0f, 0.5f), new Vector2(1f, 0.75f));
        var fullBadgeImg = GetOrAdd<Image>(fullBadge);
        fullBadgeImg.color = new Color(0.8f, 0.2f, 0.2f, 0.9f);
        var fullBadgeText = GetOrCreate(fullBadge.transform, "Text");
        SetFullStretch(fullBadgeText);
        var fullBadgeTMP = GetOrAdd<TextMeshProUGUI>(fullBadgeText);
        fullBadgeTMP.text = "FULL"; fullBadgeTMP.fontSize = 14;
        fullBadgeTMP.alignment = TextAlignmentOptions.Center; fullBadgeTMP.color = Color.white;
        fullBadge.SetActive(false);

        var equippedIcon = GetOrCreate(prefab.transform, "EquippedIcon");
        SetAnchored(equippedIcon, new Vector2(0f, 0.7f), new Vector2(0.35f, 0.95f));
        var equippedImg = GetOrAdd<Image>(equippedIcon);
        equippedImg.color = new Color(0.2f, 0.8f, 0.2f, 1f);
        equippedIcon.SetActive(false);

        prefab.SetActive(false); // template stays disabled

        // ── Cancel Button ─────────────────────────────────────────────────────
        var cancelBtn = GetOrCreate(modalPanel.transform, "CancelButton");
        SetAnchored(cancelBtn, new Vector2(0.2f, 0.03f), new Vector2(0.8f, 0.18f));
        GetOrAdd<Button>(cancelBtn);
        var cancelImg = GetOrAdd<Image>(cancelBtn);
        cancelImg.color = new Color(0.3f, 0.3f, 0.3f, 1f);
        var cancelText = GetOrCreate(cancelBtn.transform, "Text");
        SetFullStretch(cancelText);
        var cancelTMP = GetOrAdd<TextMeshProUGUI>(cancelText);
        cancelTMP.text = "CANCEL"; cancelTMP.fontSize = 18;
        cancelTMP.alignment = TextAlignmentOptions.Center; cancelTMP.color = Color.white;

        // ── Wire SerializeFields on controller ────────────────────────────────
        wired += SetProp(so, "modalPanel",    modalPanel);
        wired += SetProp(so, "backdrop",      backdrop);
        wired += SetPropComponent<Button>(so, "cancelButton", cancelBtn, ref failed);
        wired += SetProp(so, "bagGridParent", bagGrid.transform);
        wired += SetProp(so, "bagSlotPrefab", prefab);

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(controller);

        // ── Wire back-references ──────────────────────────────────────────────
        WireDetailPanelReference(controller, ref wired, ref failed);
        WireCompareControllerReference(controller, ref wired, ref failed);

        EditorUtility.SetDirty(root);
        Debug.Log($"[BagSelectionAutoWire] Done. {wired} fields wired, {failed} failed.");
        if (failed > 0)
            Debug.LogWarning("[BagSelectionAutoWire] Some fields failed. Check warnings above.");

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
        dpSO.ApplyModifiedProperties();
        EditorUtility.SetDirty(dp);
        wired++;
        Debug.Log("[BagSelectionAutoWire] Wired ClubDetailPanel.bagSelectionModal.");
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
        ccSO.ApplyModifiedProperties();
        EditorUtility.SetDirty(cc);
        wired++;
        Debug.Log("[BagSelectionAutoWire] Wired ClubCompareController.bagSelectionModal.");
    }

    // ── Hierarchy helpers ─────────────────────────────────────────────────────

    private static GameObject GetOrCreate(Transform parent, string name)
    {
        var existing = parent.Find(name);
        if (existing != null) return existing.gameObject;

        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        return go;
    }

    private static T GetOrAdd<T>(GameObject go) where T : Component
    {
        var c = go.GetComponent<T>();
        if (c == null) c = go.AddComponent<T>();
        return c;
    }

    private static void SetFullStretch(GameObject go)
    {
        var rt = go.GetComponent<RectTransform>();
        if (rt == null) rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    private static RectTransform SetRectTransform(GameObject go, Vector2 anchorMin, Vector2 anchorMax)
    {
        var rt = go.GetComponent<RectTransform>();
        if (rt == null) rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        return rt;
    }

    private static void SetAnchored(GameObject go, Vector2 anchorMin, Vector2 anchorMax)
        => SetRectTransform(go, anchorMin, anchorMax);

    private static void BuildSlotChild<T>(Transform parent, string name,
        System.Action<RectTransform> layoutFn,
        System.Action<T>? componentFn = null) where T : Component
    {
        var go  = GetOrCreate(parent, name);
        var rt  = go.GetComponent<RectTransform>();
        if (rt == null) rt = go.AddComponent<RectTransform>();
        layoutFn(rt);
        var comp = GetOrAdd<T>(go);
        componentFn?.Invoke(comp);
        if (typeof(T) == typeof(Image))
            ((Image)(object)comp).raycastTarget = false;
    }

    // ── SerializedObject helpers ──────────────────────────────────────────────

    private static int SetProp(SerializedObject so, string field, Object? value)
    {
        var prop = so.FindProperty(field);
        if (prop == null) return 0;
        prop.objectReferenceValue = value;
        return 1;
    }

    private static int SetPropComponent<T>(SerializedObject so, string field,
        GameObject go, ref int failed) where T : Component
    {
        var prop = so.FindProperty(field);
        if (prop == null) { failed++; return 0; }
        prop.objectReferenceValue = go.GetComponent<T>();
        return 1;
    }
}
#endif
