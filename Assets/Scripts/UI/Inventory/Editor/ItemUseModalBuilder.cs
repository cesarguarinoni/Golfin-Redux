#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Golfin.Inventory.Editor
{
    /// <summary>
    /// Builds the ItemUseModal scene hierarchy inside ItemsContent.
    ///
    /// Structure:
    ///   ItemUseModal (root — ModalController, full-screen overlay)
    ///     Background (Image — RepairBackground.png)
    ///     ModalContainer (VerticalLayoutGroup)
    ///       TitleText (TMP — "SELECT CLUB")
    ///       TopDivider (Image — thin horizontal line)
    ///       FilterBar (ClubFilterBar — 6 buttons)
    ///         ALLFilter / DRIVERSFilter / WOODSFilter / IRONSFilter / WEDGESFilter / PUTTERSFilter
    ///       ScrollArea (ScrollRect — clips club grid)
    ///         Viewport (RectMask2D)
    ///           GridContent (GridLayoutGroup — 4 columns)
    ///         Scrollbar (Scrollbar — vertical)
    ///       BottomDivider (Image — thin horizontal line)
    ///       CancelButton (Button — ButtonCancel.png)
    ///
    /// Run AFTER: GOLFIN/Build/Item Use Club Card Prefab
    /// Run: GOLFIN/Build/Item Use Modal
    /// Then: GOLFIN/Wire/Item Use Modal
    /// </summary>
    public static class ItemUseModalBuilder
    {
        private const string CANCEL_SPRITE_PATH = "Assets/Art/ItemsScreen/ButtonCancel.png";
        private const string BG_SPRITE_PATH     = "Assets/Art/ItemsScreen/RepairBackground.png";
        private static readonly string[] FILTER_LABELS = { "ALL", "DRIVERS", "WOODS", "IRONS", "WEDGES", "PUTTERS" };

        [MenuItem("GOLFIN/Build/Item Use Modal")]
        public static void Run()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();

            // Find ItemsContent
            var itemsContent = FindByName(scene, "ItemsContent");
            if (itemsContent == null)
            {
                EditorUtility.DisplayDialog("Build Item Use Modal",
                    "ItemsContent not found in scene. Build Items Content first.", "OK");
                return;
            }

            // Confirm rebuild
            var existing = FindByName(scene, "ItemUseModal");
            if (existing != null)
            {
                bool rebuild = EditorUtility.DisplayDialog("Build Item Use Modal",
                    "ItemUseModal already exists. Rebuild it?", "Rebuild", "Cancel");
                if (!rebuild) return;
                Object.DestroyImmediate(existing);
            }

            var modalRoot = BuildModal(itemsContent.transform);

            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = modalRoot;

            Debug.Log("[ItemUseModalBuilder] ✓ ItemUseModal built.");
            EditorUtility.DisplayDialog("Build Item Use Modal",
                "ItemUseModal built!\n\n" +
                "Next steps:\n" +
                "1. Run GOLFIN/Wire/Item Use Modal\n" +
                "2. Wire filterButtons array on ClubFilterBar in Inspector\n" +
                "3. Save scene (Ctrl+S)", "OK");
        }

        private static GameObject BuildModal(Transform parent)
        {
            // ── Root (full-screen overlay) ─────────────────────────────────────
            var root = new GameObject("ItemUseModal", typeof(RectTransform));
            root.transform.SetParent(parent, false);

            var rootRT = root.GetComponent<RectTransform>();
            rootRT.anchorMin        = Vector2.zero;
            rootRT.anchorMax        = Vector2.one;
            rootRT.sizeDelta        = Vector2.zero;
            rootRT.anchoredPosition = Vector2.zero;

            // ModalController component — modalPanel = this root's child "ModalPanel"
            root.AddComponent<ItemUseModalController>();

            // GraphicRaycaster so buttons inside receive clicks
            root.AddComponent<GraphicRaycaster>();

            // Hidden by default (ModalController.Awake hides modalPanel)
            // We keep root active; ModalController manages modalPanel visibility

            // ── Background ────────────────────────────────────────────────────
            var bgGO  = MakeStretchChild(root, "Background");
            var bgImg = bgGO.AddComponent<Image>();
            bgImg.color = new Color(0f, 0f, 0f, 0.85f); // dark overlay fallback
            bgImg.raycastTarget = true; // block clicks behind modal

            var bgSprite = AssetDatabase.LoadAssetAtPath<Sprite>(BG_SPRITE_PATH);
            if (bgSprite != null) bgImg.sprite = bgSprite;
            else Debug.LogWarning($"[ItemUseModalBuilder] RepairBackground.png not found at {BG_SPRITE_PATH} — assign manually.");

            // ── ModalPanel (managed by ModalController) ───────────────────────
            var modalPanel = new GameObject("ModalPanel", typeof(RectTransform));
            modalPanel.transform.SetParent(root.transform, false);
            var mpRT = modalPanel.GetComponent<RectTransform>();
            mpRT.anchorMin        = new Vector2(0.05f, 0.05f);
            mpRT.anchorMax        = new Vector2(0.95f, 0.95f);
            mpRT.sizeDelta        = Vector2.zero;
            mpRT.anchoredPosition = Vector2.zero;

            // Wire ModalController.modalPanel
            var modalCtrl = root.GetComponent<ItemUseModalController>();
            var soModal   = new SerializedObject(modalCtrl.GetComponent<ModalController>() ?? (UnityEngine.Component)modalCtrl);
            // We'll let AutoWire handle field wiring; just set modalPanel via base class field
            var baseSO = new SerializedObject(modalCtrl);
            baseSO.FindProperty("modalPanel").objectReferenceValue = modalPanel;
            baseSO.ApplyModifiedProperties();

            // ── ModalContainer (VerticalLayoutGroup inside ModalPanel) ─────────
            var container    = MakeStretchChild(modalPanel, "ModalContainer");
            var containerVLG = container.AddComponent<VerticalLayoutGroup>();
            containerVLG.spacing            = 8f;
            containerVLG.padding            = new RectOffset(12, 12, 12, 12);
            containerVLG.childForceExpandWidth  = true;
            containerVLG.childForceExpandHeight = false;
            containerVLG.childAlignment     = TextAnchor.UpperCenter;

            // ── TitleText ─────────────────────────────────────────────────────
            var titleGO = MakeChild(container, "TitleText");
            titleGO.AddComponent<LayoutElement>().preferredHeight = 40f;
            var titleTMP = titleGO.AddComponent<TextMeshProUGUI>();
            titleTMP.text      = "SELECT CLUB";
            titleTMP.fontSize  = 22f;
            titleTMP.fontStyle = FontStyles.Bold;
            titleTMP.alignment = TextAlignmentOptions.Center;
            titleTMP.color     = Color.white;

            // ── TopDivider ────────────────────────────────────────────────────
            var topDiv    = MakeChild(container, "TopDivider");
            var topDivLE  = topDiv.AddComponent<LayoutElement>();
            topDivLE.preferredHeight = 1f;
            var topDivImg = topDiv.AddComponent<Image>();
            topDivImg.color         = new Color(1f, 1f, 1f, 0.3f);
            topDivImg.raycastTarget = false;

            // ── FilterBar ─────────────────────────────────────────────────────
            var filterBarGO = MakeChild(container, "FilterBar");
            filterBarGO.AddComponent<LayoutElement>().preferredHeight = 44f;
            var filterBarHLG = filterBarGO.AddComponent<HorizontalLayoutGroup>();
            filterBarHLG.spacing           = 0f;
            filterBarHLG.childForceExpandWidth  = true;
            filterBarHLG.childForceExpandHeight = true;
            var filterBarComp = filterBarGO.AddComponent<ClubFilterBar>();

            // Filter buttons
            var filterBtns = new Button[FILTER_LABELS.Length];
            for (int i = 0; i < FILTER_LABELS.Length; i++)
            {
                var btnGO  = MakeChild(filterBarGO, $"{FILTER_LABELS[i]}Filter");
                var btnImg = btnGO.AddComponent<Image>();
                btnImg.color = new Color(1f, 1f, 1f, 0f); // transparent
                var btn    = btnGO.AddComponent<Button>();
                btn.targetGraphic = btnImg;
                filterBtns[i]     = btn;

                var labelGO  = MakeChild(btnGO, "Text");
                var labelRT  = labelGO.GetComponent<RectTransform>();
                labelRT.anchorMin = Vector2.zero;
                labelRT.anchorMax = Vector2.one;
                labelRT.sizeDelta = Vector2.zero;
                var labelTMP     = labelGO.AddComponent<TextMeshProUGUI>();
                labelTMP.text      = FILTER_LABELS[i];
                labelTMP.fontSize  = 11f;
                labelTMP.fontStyle = FontStyles.Bold;
                labelTMP.alignment = TextAlignmentOptions.Center;
                labelTMP.color     = Color.white;
            }

            // Wire filterButtons array on ClubFilterBar
            var filterBarSO = new SerializedObject(filterBarComp);
            var filterBtnsArr = filterBarSO.FindProperty("filterButtons");
            filterBtnsArr.arraySize = filterBtns.Length;
            for (int i = 0; i < filterBtns.Length; i++)
                filterBtnsArr.GetArrayElementAtIndex(i).objectReferenceValue = filterBtns[i];
            filterBarSO.ApplyModifiedProperties();

            // ── ScrollArea ────────────────────────────────────────────────────
            var scrollArea = MakeChild(container, "ScrollArea");
            var scrollAreaLE = scrollArea.AddComponent<LayoutElement>();
            scrollAreaLE.flexibleHeight = 1f;
            scrollAreaLE.flexibleWidth  = 1f;
            var scrollRT = scrollArea.GetComponent<RectTransform>();
            scrollRT.anchorMin = new Vector2(0f, 0f);
            scrollRT.anchorMax = new Vector2(1f, 1f);

            var scrollRectComp = scrollArea.AddComponent<ScrollRect>();
            scrollRectComp.horizontal       = false;
            scrollRectComp.vertical         = true;
            scrollRectComp.scrollSensitivity = 20f;

            // Viewport
            var viewport = MakeChild(scrollArea, "Viewport");
            var viewportRT = viewport.GetComponent<RectTransform>();
            viewportRT.anchorMin        = Vector2.zero;
            viewportRT.anchorMax        = new Vector2(1f, 1f);
            viewportRT.offsetMin        = new Vector2(0f, 0f);
            viewportRT.offsetMax        = new Vector2(-16f, 0f); // leave room for scrollbar
            viewport.AddComponent<RectMask2D>();
            scrollRectComp.viewport = viewportRT;

            // GridContent
            var gridContent = MakeChild(viewport, "GridContent");
            var gridRT = gridContent.GetComponent<RectTransform>();
            gridRT.anchorMin        = new Vector2(0f, 1f);
            gridRT.anchorMax        = new Vector2(1f, 1f);
            gridRT.pivot            = new Vector2(0.5f, 1f);
            gridRT.sizeDelta        = Vector2.zero;
            gridRT.anchoredPosition = Vector2.zero;

            var gridLayout = gridContent.AddComponent<GridLayoutGroup>();
            gridLayout.cellSize        = new Vector2(180f, 410f);
            gridLayout.spacing         = new Vector2(8f, 8f);
            gridLayout.constraint      = GridLayoutGroup.Constraint.FixedColumnCount;
            gridLayout.constraintCount = 4;
            gridLayout.startCorner     = GridLayoutGroup.Corner.UpperLeft;
            gridLayout.childAlignment  = TextAnchor.UpperCenter;
            gridLayout.padding         = new RectOffset(4, 4, 4, 4);

            var gridFitter = gridContent.AddComponent<ContentSizeFitter>();
            gridFitter.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;
            gridFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            scrollRectComp.content = gridRT;

            // Scrollbar
            var scrollbarGO = MakeChild(scrollArea, "Scrollbar");
            var scrollbarRT = scrollbarGO.GetComponent<RectTransform>();
            scrollbarRT.anchorMin        = new Vector2(1f, 0f);
            scrollbarRT.anchorMax        = new Vector2(1f, 1f);
            scrollbarRT.pivot            = new Vector2(1f, 0.5f);
            scrollbarRT.sizeDelta        = new Vector2(14f, 0f);
            scrollbarRT.anchoredPosition = Vector2.zero;

            var scrollbarImg  = scrollbarGO.AddComponent<Image>();
            scrollbarImg.color = new Color(0.2f, 0.2f, 0.2f, 0.5f);
            var scrollbarComp = scrollbarGO.AddComponent<Scrollbar>();
            scrollbarComp.direction = Scrollbar.Direction.BottomToTop;

            var handleArea = MakeChild(scrollbarGO, "Sliding Area");
            var handleAreaRT = handleArea.GetComponent<RectTransform>();
            handleAreaRT.anchorMin  = Vector2.zero;
            handleAreaRT.anchorMax  = Vector2.one;
            handleAreaRT.sizeDelta  = new Vector2(-10f, -10f);
            handleAreaRT.offsetMin  = new Vector2(5f, 5f);
            handleAreaRT.offsetMax  = new Vector2(-5f, -5f);

            var handle = MakeChild(handleArea, "Handle");
            var handleRT = handle.GetComponent<RectTransform>();
            handleRT.anchorMin = Vector2.zero;
            handleRT.anchorMax = Vector2.one;
            handleRT.sizeDelta = Vector2.zero;
            var handleImg  = handle.AddComponent<Image>();
            handleImg.color = new Color(0.8f, 0.8f, 0.8f, 0.6f);
            scrollbarComp.handleRect  = handleRT;
            scrollbarComp.targetGraphic = handleImg;
            scrollRectComp.verticalScrollbar = scrollbarComp;

            // ── BottomDivider ─────────────────────────────────────────────────
            var botDiv    = MakeChild(container, "BottomDivider");
            var botDivLE  = botDiv.AddComponent<LayoutElement>();
            botDivLE.preferredHeight = 1f;
            var botDivImg = botDiv.AddComponent<Image>();
            botDivImg.color         = new Color(1f, 1f, 1f, 0.3f);
            botDivImg.raycastTarget = false;

            // ── CancelButton ──────────────────────────────────────────────────
            var cancelGO  = MakeChild(container, "CancelButton");
            cancelGO.AddComponent<LayoutElement>().preferredHeight = 52f;
            var cancelImg = cancelGO.AddComponent<Image>();

            var cancelSprite = AssetDatabase.LoadAssetAtPath<Sprite>(CANCEL_SPRITE_PATH);
            if (cancelSprite != null)
                cancelImg.sprite = cancelSprite;
            else
            {
                cancelImg.color = new Color(0.3f, 0.3f, 0.35f, 1f);
                Debug.LogWarning($"[ItemUseModalBuilder] ButtonCancel.png not found at {CANCEL_SPRITE_PATH} — assign manually.");
            }

            var cancelBtn = cancelGO.AddComponent<Button>();
            cancelBtn.targetGraphic = cancelImg;

            var cancelLabelGO  = MakeChild(cancelGO, "Text");
            var cancelLabelRT  = cancelLabelGO.GetComponent<RectTransform>();
            cancelLabelRT.anchorMin = Vector2.zero;
            cancelLabelRT.anchorMax = Vector2.one;
            cancelLabelRT.sizeDelta = Vector2.zero;
            var cancelTMP    = cancelLabelGO.AddComponent<TextMeshProUGUI>();
            cancelTMP.text      = LocalizationManager.Get("ITEM_CANCEL");
            cancelTMP.fontSize  = 18f;
            cancelTMP.fontStyle = FontStyles.Bold;
            cancelTMP.alignment = TextAlignmentOptions.Center;
            cancelTMP.color     = Color.white;

            return root;
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static GameObject MakeStretchChild(GameObject parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent.transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin        = Vector2.zero;
            rt.anchorMax        = Vector2.one;
            rt.sizeDelta        = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;
            return go;
        }

        private static GameObject MakeChild(GameObject parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent.transform, false);
            return go;
        }

        private static GameObject? FindByName(UnityEngine.SceneManagement.Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var t = FindTransformByName(root.transform, name);
                if (t != null) return t.gameObject;
            }
            return null;
        }

        private static Transform? FindTransformByName(Transform parent, string name)
        {
            if (parent.name == name) return parent;
            foreach (Transform child in parent)
            {
                var result = FindTransformByName(child, name);
                if (result != null) return result;
            }
            return null;
        }
    }
}
#endif
