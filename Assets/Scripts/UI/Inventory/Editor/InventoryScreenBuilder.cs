#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Golfin.Inventory.Editor
{
    /// <summary>
    /// Builds the InventoryScreen hierarchy inside the active scene's ScreensRoot.
    ///
    /// Creates:
    ///   InventoryScreen
    ///     Header           — "INVENTORY" title
    ///     TabBar           — CLUBS / BAGS / BALLS / ITEMS buttons + active indicators
    ///     ContentArea
    ///       ClubsContent   — FilterBar + ClubsMainSection placeholder
    ///       BagsContent    — hidden placeholder
    ///       BallsContent   — hidden placeholder
    ///       ItemsContent   — hidden placeholder
    ///
    /// Adds InventoryScreenController to InventoryScreen and ClubFilterBar to FilterBar.
    /// Wires InventoryScreen to ScreenManager._inventoryScreen.
    ///
    /// Run: GOLFIN/Build Inventory Screen
    /// Safe to re-run — destroys and recreates InventoryScreen each time.
    /// </summary>
    public static class InventoryScreenBuilder
    {
        private static readonly string[] TAB_LABELS    = { "CLUBS", "BAGS", "BALLS", "ITEMS" };
        private static readonly string[] FILTER_LABELS = { "ALL", "DRIVERS", "WOODS", "IRONS", "A.WEDGES", "P.WEDGES", "S.WEDGES", "PUTTERS" };

        // Heights (px)
        private const float HEADER_H    = 60f;
        private const float TABBAR_H    = 50f;
        private const float INDICATOR_H = 3f;   // underline thickness
        private const float FILTERBAR_H = 48f;

        [MenuItem("GOLFIN/Build Inventory Screen")]
        public static void Run()
        {
            var screensRoot = FindScreensRoot();
            if (screensRoot == null)
            {
                EditorUtility.DisplayDialog("Build Inventory Screen",
                    "Could not find ScreensRoot.\n\n" +
                    "Make sure the ShellScene is open and ScreenManager has _rosterScreen assigned.",
                    "OK");
                return;
            }

            // ── Destroy any existing InventoryScreen ──────────────────────────
            var existing = screensRoot.Find("InventoryScreen");
            if (existing != null)
            {
                Object.DestroyImmediate(existing.gameObject);
                Debug.Log("[InventoryScreenBuilder] Removed existing InventoryScreen.");
            }

            // ── Build ─────────────────────────────────────────────────────────
            var root = BuildRoot(screensRoot);
            var (header, tabBar, contentArea) = BuildTopLevelSections(root);
            var (tabButtons, tabIndicators) = BuildTabBar(tabBar);
            var (clubsContent, bagsContent, ballsContent, itemsContent) = BuildContentPanels(contentArea);
            var (filterBar, filterButtons) = BuildFilterBar(clubsContent);
            BuildClubsMainSection(clubsContent);

            // ── Add components ────────────────────────────────────────────────
            var controller = root.gameObject.AddComponent<InventoryScreenController>();
            var filterBarComp = filterBar.gameObject.AddComponent<ClubFilterBar>();

            // ── Wire InventoryScreenController ────────────────────────────────
            var soCtrl = new SerializedObject(controller);
            SetObjectArray(soCtrl, "tabButtons",   tabButtons);
            SetObjectArray(soCtrl, "tabPanels",    new Object[]
            {
                clubsContent.gameObject, bagsContent.gameObject,
                ballsContent.gameObject, itemsContent.gameObject
            });
            SetObjectArray(soCtrl, "tabIndicators", tabIndicators);
            soCtrl.FindProperty("clubFilterBar").objectReferenceValue = filterBarComp;
            soCtrl.ApplyModifiedProperties();

            // ── Wire ClubFilterBar ────────────────────────────────────────────
            var soFilter = new SerializedObject(filterBarComp);
            SetObjectArray(soFilter, "filterButtons", filterButtons);
            soFilter.ApplyModifiedProperties();

            // ── Wire ScreenManager._inventoryScreen ───────────────────────────
            WireScreenManager(root.gameObject);

            // ── InventoryScreen starts hidden (ScreenManager shows it) ────────
            root.gameObject.SetActive(false);

            EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());

            EditorUtility.DisplayDialog("Build Inventory Screen",
                "Done!\n\n" +
                "• InventoryScreen created under ScreensRoot\n" +
                "• InventoryScreenController wired (4 tabs, 4 content panels)\n" +
                "• ClubFilterBar wired (8 filter buttons)\n" +
                "• ScreenManager._inventoryScreen set\n" +
                "• Nav button already wired (PersistentUIManager.inventoryButton)\n\n" +
                "Hit Play and tap the Inventory nav button to test.",
                "OK");

            Debug.Log("[InventoryScreenBuilder] ✓ InventoryScreen built and wired.");
        }

        // ── Root ─────────────────────────────────────────────────────────────

        private static RectTransform BuildRoot(Transform screensRoot)
        {
            var go = new GameObject("InventoryScreen");
            go.transform.SetParent(screensRoot, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            // Background — semi-transparent dark so it's clearly visible
            var bg = go.AddComponent<Image>();
            bg.color = new Color(0.08f, 0.08f, 0.12f, 1f);

            return rt;
        }

        // ── Top-level sections ────────────────────────────────────────────────

        private static (RectTransform header, RectTransform tabBar, RectTransform contentArea)
            BuildTopLevelSections(RectTransform root)
        {
            var header      = MakeStretchChild(root, "Header",      HEADER_H, anchorFromTop: 0f);
            var tabBar      = MakeStretchChild(root, "TabBar",      TABBAR_H, anchorFromTop: HEADER_H);
            var contentArea = MakeStretchFill(root, "ContentArea",  topOffset: HEADER_H + TABBAR_H);

            // Header background
            header.gameObject.AddComponent<Image>().color = new Color(0.05f, 0.05f, 0.09f, 1f);

            // Header title
            var titleGO = new GameObject("InventoryTitle");
            titleGO.transform.SetParent(header, false);
            var titleRT = titleGO.AddComponent<RectTransform>();
            titleRT.anchorMin = Vector2.zero; titleRT.anchorMax = Vector2.one;
            titleRT.offsetMin = new Vector2(20f, 0f); titleRT.offsetMax = Vector2.zero;
            var titleTMP = titleGO.AddComponent<TextMeshProUGUI>();
            titleTMP.text = "INVENTORY";
            titleTMP.fontSize = 22f;
            titleTMP.fontStyle = FontStyles.Bold;
            titleTMP.color = Color.white;
            titleTMP.alignment = TextAlignmentOptions.MidlineLeft;

            // TabBar background
            tabBar.gameObject.AddComponent<Image>().color = new Color(0.06f, 0.06f, 0.10f, 1f);

            // ContentArea background
            contentArea.gameObject.AddComponent<Image>().color = new Color(0.10f, 0.10f, 0.14f, 1f);

            return (header, tabBar, contentArea);
        }

        // ── Tab bar ───────────────────────────────────────────────────────────

        private static (Object[] buttons, Object[] indicators) BuildTabBar(RectTransform tabBar)
        {
            int count   = TAB_LABELS.Length;
            var buttons    = new Object[count];
            var indicators = new Object[count];

            float tabW = 1f / count;

            for (int i = 0; i < count; i++)
            {
                float xMin = i * tabW;
                float xMax = xMin + tabW;

                // Tab container
                var tabGO = new GameObject(TAB_LABELS[i] + "Tab");
                tabGO.transform.SetParent(tabBar, false);
                var tabRT = tabGO.AddComponent<RectTransform>();
                tabRT.anchorMin = new Vector2(xMin, 0f);
                tabRT.anchorMax = new Vector2(xMax, 1f);
                tabRT.offsetMin = Vector2.zero;
                tabRT.offsetMax = Vector2.zero;

                // Button
                var btn = tabGO.AddComponent<Button>();
                var btnImg = tabGO.AddComponent<Image>();
                btnImg.color = Color.clear;
                buttons[i] = btn;

                // Label
                var labelGO = new GameObject("Label");
                labelGO.transform.SetParent(tabGO.transform, false);
                var labelRT = labelGO.AddComponent<RectTransform>();
                labelRT.anchorMin = new Vector2(0f, INDICATOR_H / TABBAR_H);
                labelRT.anchorMax = Vector2.one;
                labelRT.offsetMin = Vector2.zero;
                labelRT.offsetMax = Vector2.zero;
                var tmp = labelGO.AddComponent<TextMeshProUGUI>();
                tmp.text = TAB_LABELS[i];
                tmp.fontSize = 13f;
                tmp.fontStyle = FontStyles.Bold;
                tmp.color = i == 0 ? Color.white : new Color(0.55f, 0.55f, 0.55f);
                tmp.alignment = TextAlignmentOptions.Center;

                // Active indicator (underline)
                var indGO = new GameObject("ActiveIndicator");
                indGO.transform.SetParent(tabGO.transform, false);
                var indRT = indGO.AddComponent<RectTransform>();
                indRT.anchorMin = new Vector2(0.1f, 0f);
                indRT.anchorMax = new Vector2(0.9f, 0f);
                indRT.offsetMin = Vector2.zero;
                indRT.offsetMax = new Vector2(0f, INDICATOR_H);
                var indImg = indGO.AddComponent<Image>();
                indImg.color = Color.white;
                indImg.enabled = (i == 0);   // only CLUBS lit on start
                indicators[i] = indImg;
            }

            return (buttons, indicators);
        }

        // ── Content panels ────────────────────────────────────────────────────

        private static (RectTransform clubs, RectTransform bags, RectTransform balls, RectTransform items)
            BuildContentPanels(RectTransform contentArea)
        {
            var clubs  = MakeStretchFill(contentArea, "ClubsContent",  0f);
            var bags   = MakeStretchFill(contentArea, "BagsContent",   0f);
            var balls  = MakeStretchFill(contentArea, "BallsContent",  0f);
            var items  = MakeStretchFill(contentArea, "ItemsContent",  0f);

            // Placeholder labels for non-clubs tabs
            AddPlaceholderLabel(bags,  "BAGS — Coming Soon");
            AddPlaceholderLabel(balls, "BALLS — Coming Soon");
            AddPlaceholderLabel(items, "ITEMS — Coming Soon");

            // Start with only clubs visible (controller will manage this at runtime)
            bags.gameObject.SetActive(false);
            balls.gameObject.SetActive(false);
            items.gameObject.SetActive(false);

            return (clubs, bags, balls, items);
        }

        // ── Filter bar ────────────────────────────────────────────────────────

        private static (RectTransform filterBar, Object[] buttons) BuildFilterBar(RectTransform clubsContent)
        {
            // Simple flat container — HorizontalLayoutGroup distributes buttons evenly.
            // No ScrollRect/Mask nesting (avoids stencil-buffer invisibility bugs).
            var container = new GameObject("FilterBar");
            container.transform.SetParent(clubsContent, false);
            var containerRT = container.AddComponent<RectTransform>();
            containerRT.anchorMin = new Vector2(0f, 1f);
            containerRT.anchorMax = new Vector2(1f, 1f);
            containerRT.pivot     = new Vector2(0.5f, 1f);
            containerRT.offsetMin = new Vector2(0f, -FILTERBAR_H);
            containerRT.offsetMax = Vector2.zero;

            var containerImg = container.AddComponent<Image>();
            containerImg.color = new Color(0.06f, 0.06f, 0.10f, 1f);

            // HLG: buttons fill the bar width evenly (flexibleWidth = 1)
            var hlg = container.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing               = 2f;
            hlg.padding               = new RectOffset(4, 4, 4, 4);
            hlg.childAlignment        = TextAnchor.MiddleCenter;
            hlg.childForceExpandHeight = true;
            hlg.childForceExpandWidth  = true;   // distribute evenly across full width

            // Build filter buttons directly inside the container
            int count   = FILTER_LABELS.Length;
            var buttons = new Object[count];

            for (int i = 0; i < count; i++)
            {
                var btnGO = new GameObject(FILTER_LABELS[i] + "Filter");
                btnGO.transform.SetParent(container.transform, false);

                var btnImg = btnGO.AddComponent<Image>();
                btnImg.color = i == 0
                    ? new Color(1f, 1f, 1f, 0.20f)   // ALL active
                    : new Color(1f, 1f, 1f, 0f);      // others inactive

                var btn = btnGO.AddComponent<Button>();
                buttons[i] = btn;

                // Label — stretch fill button
                var labelGO = new GameObject("Label");
                labelGO.transform.SetParent(btnGO.transform, false);
                var labelRT = labelGO.AddComponent<RectTransform>();
                labelRT.anchorMin = Vector2.zero;
                labelRT.anchorMax = Vector2.one;
                labelRT.offsetMin = Vector2.zero;
                labelRT.offsetMax = Vector2.zero;
                var tmp = labelGO.AddComponent<TextMeshProUGUI>();
                tmp.text                    = FILTER_LABELS[i];
                tmp.fontSize                = 10f;
                tmp.fontStyle               = FontStyles.Bold;
                tmp.color                   = i == 0 ? Color.white : new Color(0.55f, 0.55f, 0.55f);
                tmp.alignment               = TextAlignmentOptions.Center;
                tmp.enableAutoSizing        = false;
                tmp.overflowMode            = TextOverflowModes.Ellipsis;
            }

            return (containerRT, buttons);
        }

        // ── ClubsMainSection placeholder ──────────────────────────────────────

        private static void BuildClubsMainSection(RectTransform clubsContent)
        {
            var sectionGO = new GameObject("ClubsMainSection");
            sectionGO.transform.SetParent(clubsContent, false);
            var rt = sectionGO.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.offsetMin = new Vector2(0f, 0f);
            rt.offsetMax = new Vector2(0f, -FILTERBAR_H);

            // Placeholder label (Phase C replaces this with carousel + detail panel)
            AddPlaceholderLabel(rt, "Carousel + Detail Panel\n(Phase C)");
        }

        // ── ScreenManager wiring ──────────────────────────────────────────────

        private static void WireScreenManager(GameObject inventoryScreen)
        {
            var sm = Object.FindObjectOfType<GolfinRedux.UI.ScreenManager>();
            if (sm == null)
            {
                Debug.LogWarning("[InventoryScreenBuilder] ScreenManager not found — wire _inventoryScreen manually.");
                return;
            }

            var so = new SerializedObject(sm);
            so.FindProperty("_inventoryScreen").objectReferenceValue = inventoryScreen;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(sm);
            Debug.Log("[InventoryScreenBuilder] ✓ ScreenManager._inventoryScreen wired.");
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static Transform FindScreensRoot()
        {
            // Locate ScreensRoot by getting the parent of the existing _rosterScreen
            var sm = Object.FindObjectOfType<GolfinRedux.UI.ScreenManager>();
            if (sm == null) return null;

            var so        = new SerializedObject(sm);
            var rosterProp = so.FindProperty("_rosterScreen");
            if (rosterProp?.objectReferenceValue is GameObject rosterGO)
                return rosterGO.transform.parent;

            Debug.LogWarning("[InventoryScreenBuilder] _rosterScreen not set on ScreenManager — cannot find ScreensRoot.");
            return null;
        }

        /// <summary>Creates a child that stretches from <paramref name="anchorFromTop"/> down by <paramref name="height"/>.</summary>
        private static RectTransform MakeStretchChild(RectTransform parent, string name, float height, float anchorFromTop)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot     = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(0f, -(anchorFromTop + height));
            rt.offsetMax = new Vector2(0f, -anchorFromTop);
            return rt;
        }

        /// <summary>Creates a child that fills the parent minus a top offset.</summary>
        private static RectTransform MakeStretchFill(RectTransform parent, string name, float topOffset)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = new Vector2(0f, -topOffset);
            return rt;
        }

        private static void AddPlaceholderLabel(RectTransform parent, string text)
        {
            var go = new GameObject("Placeholder");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text      = text;
            tmp.fontSize  = 18f;
            tmp.color     = new Color(0.4f, 0.4f, 0.4f);
            tmp.alignment = TextAlignmentOptions.Center;
        }

        private static void SetObjectArray(SerializedObject so, string propName, Object[] values)
        {
            var prop = so.FindProperty(propName);
            if (prop == null) { Debug.LogWarning($"[InventoryScreenBuilder] Property '{propName}' not found."); return; }
            prop.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
    }
}
#endif
