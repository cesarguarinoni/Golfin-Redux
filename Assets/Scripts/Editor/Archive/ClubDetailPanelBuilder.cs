#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Golfin.Inventory.Editor
{
    /// <summary>
    /// Builds the Club Carousel + Detail Panel inside ClubsMainSection.
    ///
    /// Replaces the Phase B placeholder with:
    ///   ClubsMainSection
    ///     ClubCarouselSection  (top CAROUSEL_H pixels)
    ///       LeftArrow
    ///       RightArrow
    ///       ScrollView → Viewport → Content (HLG + ContentSizeFitter)
    ///       PaginationDots
    ///     ClubDetailPanel      (fills remaining height)
    ///       LeftPanel
    ///         ClubImage
    ///         InfoSection → InfoHeader / InfoText
    ///       RightPanel
    ///         ClubNameText
    ///         Divider
    ///         RarityLevelRow → RarityLabel / LevelText / LevelTextMax
    ///         Divider
    ///         StatsPanel → PowerRow / AccuracyRow / LieResistanceRow / LoftRow / DurabilityRow / DistanceRow
    ///         Divider
    ///         ButtonsPanel → LevelUpButton / RepairButton
    ///         CompareButton
    ///         BagLabel
    ///         EquipButton
    ///
    /// Adds ClubCarouselController to ClubCarouselSection.
    /// Adds ClubDetailPanel to ClubDetailPanel.
    ///
    /// Run: GOLFIN/Inventory/Build Club Phase C
    /// Safe to re-run — destroys and recreates ClubsMainSection children each time.
    /// </summary>
    public static class ClubDetailPanelBuilder
    {
        private const float CAROUSEL_H  = 210f;
        private const float ARROW_W     = 40f;
        private const float DOTS_H      = 20f;
        private const float DIVIDER_H   = 1f;
        private const float ROW_H       = 26f;
        private const float BUTTON_H    = 36f;
        private const float PANEL_PAD   = 8f;

        // [MenuItem - archived] "GOLFIN/Inventory/Build Club Phase C"
        public static void Run()
        {
            var mainSection = FindClubsMainSection();
            if (mainSection == null)
            {
                EditorUtility.DisplayDialog("Build Club Phase C",
                    "ClubsMainSection not found.\n\n" +
                    "Run GOLFIN/Build Inventory Screen first, then open the Inventory screen in the scene.",
                    "OK");
                return;
            }

            // Clear existing children (placeholder label from Phase B)
            for (int i = mainSection.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(mainSection.GetChild(i).gameObject);

            // Build sections
            var carouselSection = BuildCarouselSection(mainSection);
            var detailPanel     = BuildDetailPanel(mainSection);

            // Add ClubCarouselController to CarouselSection
            var carouselCtrl = carouselSection.gameObject.AddComponent<ClubCarouselController>();

            // Add ClubDetailPanel to DetailPanel
            var detailCtrl = detailPanel.gameObject.AddComponent<ClubDetailPanel>();

            // Wire ClubCarouselController fields
            WireCarouselController(carouselCtrl, carouselSection, detailCtrl);

            // Wire ClubDetailPanel.carousel reference
            var soDetail = new SerializedObject(detailCtrl);
            soDetail.FindProperty("carousel").objectReferenceValue = carouselCtrl;
            soDetail.ApplyModifiedProperties();

            EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());

            EditorUtility.DisplayDialog("Build Club Phase C",
                "Done!\n\n" +
                "• ClubCarouselSection built under ClubsMainSection\n" +
                "• ClubDetailPanel (LeftPanel + RightPanel) built\n" +
                "• ClubCarouselController and ClubDetailPanel components added\n\n" +
                "Next steps:\n" +
                "1. Run GOLFIN/Inventory/Wire Club Detail Panel to wire all fields\n" +
                "2. Set Script Execution Order: ClubDatabaseCSV = -200, ClubManager = -100\n" +
                "3. Assign ClubThumbnailCard prefab to ClubCarouselController.clubCardPrefab\n" +
                "4. Assign ClubFilterBar to ClubCarouselController.filterBar\n" +
                "   (It's on the InventoryScreenController as clubFilterBar)",
                "OK");

            Debug.Log("[ClubDetailPanelBuilder] ✓ Phase C built.");
        }

        // ── Carousel Section ───────────────────────────────────────────────────

        private static RectTransform BuildCarouselSection(Transform parent)
        {
            var go = new GameObject("ClubCarouselSection");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot     = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(0f, -CAROUSEL_H);
            rt.offsetMax = Vector2.zero;

            go.AddComponent<Image>().color = new Color(0.08f, 0.08f, 0.12f, 1f);

            // LeftArrow
            BuildArrowButton(go.transform, "LeftArrow", left: true);

            // RightArrow
            BuildArrowButton(go.transform, "RightArrow", left: false);

            // ScrollView (between the arrows)
            BuildScrollView(go.transform);

            // PaginationDots (bottom strip)
            BuildPaginationDots(go.transform);

            return rt;
        }

        private static void BuildArrowButton(Transform parent, string name, bool left)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();

            if (left)
            {
                rt.anchorMin = new Vector2(0f, 0f);
                rt.anchorMax = new Vector2(0f, 1f);
                rt.offsetMin = new Vector2(0f, DOTS_H);
                rt.offsetMax = new Vector2(ARROW_W, 0f);
            }
            else
            {
                rt.anchorMin = new Vector2(1f, 0f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.offsetMin = new Vector2(-ARROW_W, DOTS_H);
                rt.offsetMax = Vector2.zero;
            }

            var img = go.AddComponent<Image>();
            img.color = new Color(1f, 1f, 1f, 0.1f);
            go.AddComponent<Button>();

            // Label  < or >
            var labelGO = new GameObject("Label");
            labelGO.transform.SetParent(go.transform, false);
            var labelRT = labelGO.AddComponent<RectTransform>();
            labelRT.anchorMin = Vector2.zero;
            labelRT.anchorMax = Vector2.one;
            labelRT.offsetMin = Vector2.zero;
            labelRT.offsetMax = Vector2.zero;
            var tmp = labelGO.AddComponent<TextMeshProUGUI>();
            tmp.text      = left ? "<" : ">";
            tmp.fontSize  = 18f;
            tmp.color     = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
        }

        private static void BuildScrollView(Transform parent)
        {
            // ScrollView container
            var svGO = new GameObject("ScrollView");
            svGO.transform.SetParent(parent, false);
            var svRT = svGO.AddComponent<RectTransform>();
            svRT.anchorMin = new Vector2(0f, 0f);
            svRT.anchorMax = new Vector2(1f, 1f);
            svRT.offsetMin = new Vector2(ARROW_W, DOTS_H);
            svRT.offsetMax = new Vector2(-ARROW_W, 0f);

            svGO.AddComponent<Image>().color = Color.clear;
            var scrollRect = svGO.AddComponent<ScrollRect>();
            scrollRect.horizontal = true;
            scrollRect.vertical   = false;

            // Viewport (mask)
            var vpGO = new GameObject("Viewport");
            vpGO.transform.SetParent(svGO.transform, false);
            var vpRT = vpGO.AddComponent<RectTransform>();
            vpRT.anchorMin = Vector2.zero;
            vpRT.anchorMax = Vector2.one;
            vpRT.offsetMin = Vector2.zero;
            vpRT.offsetMax = Vector2.zero;
            vpGO.AddComponent<Image>().color = Color.clear;
            vpGO.AddComponent<Mask>().showMaskGraphic = false;

            // Content (HLG)
            var contentGO = new GameObject("Content");
            contentGO.transform.SetParent(vpGO.transform, false);
            var contentRT = contentGO.AddComponent<RectTransform>();
            contentRT.anchorMin = new Vector2(0f, 0f);
            contentRT.anchorMax = new Vector2(0f, 1f);
            contentRT.pivot     = new Vector2(0f, 0.5f);
            contentRT.offsetMin = Vector2.zero;
            contentRT.offsetMax = Vector2.zero;

            var hlg = contentGO.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing               = 6f;
            hlg.padding               = new RectOffset(6, 6, 4, 4);
            hlg.childAlignment        = TextAnchor.MiddleLeft;
            hlg.childForceExpandWidth  = false;
            hlg.childForceExpandHeight = false;

            var csf = contentGO.AddComponent<ContentSizeFitter>();
            csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            csf.verticalFit   = ContentSizeFitter.FitMode.Unconstrained;

            scrollRect.viewport    = vpRT;
            scrollRect.content     = contentRT;
            scrollRect.scrollSensitivity = 30f;
        }

        private static void BuildPaginationDots(Transform parent)
        {
            var go = new GameObject("PaginationDots");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = new Vector2(0f, DOTS_H);

            var hlg = go.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment        = TextAnchor.MiddleCenter;
            hlg.spacing               = 6f;
            hlg.childForceExpandWidth  = false;
            hlg.childForceExpandHeight = false;
        }

        // ── Detail Panel ───────────────────────────────────────────────────────

        private static RectTransform BuildDetailPanel(Transform parent)
        {
            var go = new GameObject("ClubDetailPanel");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = new Vector2(0f, -CAROUSEL_H);

            go.AddComponent<Image>().color = new Color(0.10f, 0.10f, 0.14f, 1f);

            // LeftPanel ~45%
            BuildLeftPanel(go.transform);

            // RightPanel ~55%
            BuildRightPanel(go.transform);

            return rt;
        }

        // ── Left Panel ─────────────────────────────────────────────────────────

        private static void BuildLeftPanel(Transform parent)
        {
            var go = new GameObject("LeftPanel");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0.45f, 1f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            // ClubImage — top 62%
            var imgGO = new GameObject("ClubImage");
            imgGO.transform.SetParent(go.transform, false);
            var imgRT = imgGO.AddComponent<RectTransform>();
            imgRT.anchorMin = new Vector2(0f, 0.38f);
            imgRT.anchorMax = Vector2.one;
            imgRT.offsetMin = new Vector2(PANEL_PAD, 0f);
            imgRT.offsetMax = new Vector2(-PANEL_PAD, -PANEL_PAD);
            var clubImg = imgGO.AddComponent<Image>();
            clubImg.preserveAspect = true;
            clubImg.color = new Color(0.8f, 0.8f, 0.8f, 1f); // placeholder tint

            // InfoSection — bottom 38%
            var infoSec = new GameObject("InfoSection");
            infoSec.transform.SetParent(go.transform, false);
            var infoRT = infoSec.AddComponent<RectTransform>();
            infoRT.anchorMin = new Vector2(0f, 0f);
            infoRT.anchorMax = new Vector2(1f, 0.38f);
            infoRT.offsetMin = new Vector2(PANEL_PAD, PANEL_PAD);
            infoRT.offsetMax = new Vector2(-PANEL_PAD, 0f);

            var infoVLG = infoSec.AddComponent<VerticalLayoutGroup>();
            infoVLG.spacing               = 3f;
            infoVLG.childAlignment        = TextAnchor.UpperLeft;
            infoVLG.childForceExpandWidth  = true;
            infoVLG.childForceExpandHeight = false;

            // InfoHeader
            var headerGO = new GameObject("InfoHeader");
            headerGO.transform.SetParent(infoSec.transform, false);
            AddLayoutElement(headerGO, preferredHeight: 18f);
            var headerTMP = headerGO.AddComponent<TextMeshProUGUI>();
            headerTMP.text      = "INFO";
            headerTMP.fontSize  = 11f;
            headerTMP.fontStyle = FontStyles.Bold;
            headerTMP.color     = new Color(0.7f, 0.7f, 0.7f, 1f);

            // InfoText
            var textGO = new GameObject("InfoText");
            textGO.transform.SetParent(infoSec.transform, false);
            var textLE = AddLayoutElement(textGO, preferredHeight: 60f);
            textLE.flexibleHeight = 1f;
            var textTMP = textGO.AddComponent<TextMeshProUGUI>();
            textTMP.text        = "";
            textTMP.fontSize    = 9f;
            textTMP.color       = new Color(0.8f, 0.8f, 0.8f, 1f);
            textTMP.overflowMode = TextOverflowModes.Ellipsis;
            textTMP.enableWordWrapping = true;
        }

        // ── Right Panel ────────────────────────────────────────────────────────

        private static void BuildRightPanel(Transform parent)
        {
            var go = new GameObject("RightPanel");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.45f, 0f);
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var vlg = go.AddComponent<VerticalLayoutGroup>();
            vlg.padding               = new RectOffset((int)PANEL_PAD, (int)PANEL_PAD, (int)PANEL_PAD, (int)PANEL_PAD);
            vlg.spacing               = 4f;
            vlg.childAlignment        = TextAnchor.UpperLeft;
            vlg.childForceExpandWidth  = true;
            vlg.childForceExpandHeight = false;

            // ClubNameText
            var nameGO = new GameObject("ClubNameText");
            nameGO.transform.SetParent(go.transform, false);
            AddLayoutElement(nameGO, preferredHeight: 22f);
            var nameTMP = nameGO.AddComponent<TextMeshProUGUI>();
            nameTMP.text      = "CLUB NAME";
            nameTMP.fontSize  = 15f;
            nameTMP.fontStyle = FontStyles.Bold;
            nameTMP.color     = Color.white;

            // Divider
            BuildDivider(go.transform);

            // RarityLevelRow
            BuildRarityLevelRow(go.transform);

            // Divider
            BuildDivider(go.transform);

            // StatsPanel
            BuildStatsPanel(go.transform);

            // Divider
            BuildDivider(go.transform);

            // ButtonsPanel (LevelUp + Repair)
            BuildButtonsPanel(go.transform);

            // CompareButton
            BuildTextButton(go.transform, "CompareButton", "COMPARE", BUTTON_H);

            // BagLabel ("IN BAG 1" — hidden by default)
            var bagGO = new GameObject("BagLabel");
            bagGO.transform.SetParent(go.transform, false);
            AddLayoutElement(bagGO, preferredHeight: 18f);
            var bagTMP = bagGO.AddComponent<TextMeshProUGUI>();
            bagTMP.text      = "IN BAG 1";
            bagTMP.fontSize  = 11f;
            bagTMP.color     = new Color(0.3f, 0.6f, 1f, 1f); // blue
            bagTMP.alignment = TextAlignmentOptions.Center;
            bagGO.SetActive(false); // hidden until equipped

            // EquipButton
            BuildTextButton(go.transform, "EquipButton", "EQUIP", BUTTON_H);
        }

        private static void BuildRarityLevelRow(Transform parent)
        {
            var go = new GameObject("RarityLevelRow");
            go.transform.SetParent(parent, false);
            AddLayoutElement(go, preferredHeight: 22f);

            var hlg = go.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment        = TextAnchor.MiddleLeft;
            hlg.childForceExpandWidth  = false;
            hlg.childForceExpandHeight = true;
            hlg.spacing               = 4f;

            // RarityLabel
            var rarityGO = new GameObject("RarityLabel");
            rarityGO.transform.SetParent(go.transform, false);
            AddLayoutElement(rarityGO, preferredWidth: 70f);
            var rarityTMP = rarityGO.AddComponent<TextMeshProUGUI>();
            rarityTMP.text      = "COMMON";
            rarityTMP.fontSize  = 12f;
            rarityTMP.fontStyle = FontStyles.Bold;
            rarityTMP.color     = Color.white;

            // LevelText
            var levelGO = new GameObject("LevelText");
            levelGO.transform.SetParent(go.transform, false);
            AddLayoutElement(levelGO, preferredWidth: 50f);
            var levelTMP = levelGO.AddComponent<TextMeshProUGUI>();
            levelTMP.text     = "Lv 1";
            levelTMP.fontSize = 12f;
            levelTMP.color    = Color.white;

            // LevelTextMax
            var maxGO = new GameObject("LevelTextMax");
            maxGO.transform.SetParent(go.transform, false);
            AddLayoutElement(maxGO, preferredWidth: 40f);
            var maxTMP = maxGO.AddComponent<TextMeshProUGUI>();
            maxTMP.text     = "/119";
            maxTMP.fontSize = 12f;
            maxTMP.color    = new Color(0.6f, 0.6f, 0.6f, 1f);
        }

        private static void BuildStatsPanel(Transform parent)
        {
            var go = new GameObject("StatsPanel");
            go.transform.SetParent(parent, false);
            var le = AddLayoutElement(go, preferredHeight: ROW_H * 6 + 4f);
            le.flexibleHeight = 1f;

            var vlg = go.AddComponent<VerticalLayoutGroup>();
            vlg.spacing               = 3f;
            vlg.childAlignment        = TextAnchor.UpperLeft;
            vlg.childForceExpandWidth  = true;
            vlg.childForceExpandHeight = false;

            BuildStatRow(go.transform, "PowerRow",         hasBar: true);
            BuildStatRow(go.transform, "AccuracyRow",      hasBar: true);
            BuildStatRow(go.transform, "LieResistanceRow", hasBar: true);
            BuildStatRow(go.transform, "LoftRow",          hasBar: true);
            BuildStatRow(go.transform, "DurabilityRow",    hasBar: true);
            BuildDistanceRow(go.transform);
        }

        /// <summary>Builds a stat row: StatsName | Bar | StatNumber</summary>
        private static void BuildStatRow(Transform parent, string name, bool hasBar)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            AddLayoutElement(go, preferredHeight: ROW_H);

            var hlg = go.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment        = TextAnchor.MiddleLeft;
            hlg.childForceExpandWidth  = false;
            hlg.childForceExpandHeight = true;
            hlg.spacing               = 4f;

            // StatsName
            var nameGO = new GameObject("StatsName");
            nameGO.transform.SetParent(go.transform, false);
            AddLayoutElement(nameGO, preferredWidth: 72f);
            var nameTMP = nameGO.AddComponent<TextMeshProUGUI>();
            nameTMP.text      = name.Replace("Row", "").ToUpper();
            nameTMP.fontSize  = 9f;
            nameTMP.color     = new Color(0.7f, 0.7f, 0.7f, 1f);
            nameTMP.overflowMode = TextOverflowModes.Ellipsis;

            // Bar
            var barGO = new GameObject("Bar");
            barGO.transform.SetParent(go.transform, false);
            var barLE = AddLayoutElement(barGO, preferredHeight: 8f);
            barLE.flexibleWidth = 1f;

            var barImg = barGO.AddComponent<Image>();
            barImg.color      = new Color(0.2f, 0.5f, 0.9f, 1f); // blue default
            barImg.type       = Image.Type.Filled;
            barImg.fillMethod = Image.FillMethod.Horizontal;
            barImg.fillOrigin = 0; // Left
            barImg.fillAmount = 0.75f;

            // StatNumber
            var numGO = new GameObject("StatNumber");
            numGO.transform.SetParent(go.transform, false);
            AddLayoutElement(numGO, preferredWidth: 42f);
            var numTMP = numGO.AddComponent<TextMeshProUGUI>();
            numTMP.text      = "75/100";
            numTMP.fontSize  = 9f;
            numTMP.color     = Color.white;
            numTMP.alignment = TextAlignmentOptions.Right;
        }

        /// <summary>Distance row: StatsName | DistanceValue (no bar)</summary>
        private static void BuildDistanceRow(Transform parent)
        {
            var go = new GameObject("DistanceRow");
            go.transform.SetParent(parent, false);
            AddLayoutElement(go, preferredHeight: ROW_H);

            var hlg = go.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment        = TextAnchor.MiddleLeft;
            hlg.childForceExpandWidth  = false;
            hlg.childForceExpandHeight = true;
            hlg.spacing               = 4f;

            // StatsName
            var nameGO = new GameObject("StatsName");
            nameGO.transform.SetParent(go.transform, false);
            AddLayoutElement(nameGO, preferredWidth: 72f);
            var nameTMP = nameGO.AddComponent<TextMeshProUGUI>();
            nameTMP.text     = "DISTANCE";
            nameTMP.fontSize = 9f;
            nameTMP.color    = new Color(0.7f, 0.7f, 0.7f, 1f);

            // Spacer
            var spacerGO = new GameObject("Spacer");
            spacerGO.transform.SetParent(go.transform, false);
            var spacerLE = AddLayoutElement(spacerGO, preferredHeight: 1f);
            spacerLE.flexibleWidth = 1f;
            spacerGO.AddComponent<Image>().color = Color.clear;

            // DistanceValue
            var valGO = new GameObject("DistanceValue");
            valGO.transform.SetParent(go.transform, false);
            AddLayoutElement(valGO, preferredWidth: 55f);
            var valTMP = valGO.AddComponent<TextMeshProUGUI>();
            valTMP.text      = "250 yd";
            valTMP.fontSize  = 10f;
            valTMP.color     = Color.white;
            valTMP.fontStyle = FontStyles.Bold;
            valTMP.alignment = TextAlignmentOptions.Right;
        }

        private static void BuildButtonsPanel(Transform parent)
        {
            var go = new GameObject("ButtonsPanel");
            go.transform.SetParent(parent, false);
            AddLayoutElement(go, preferredHeight: BUTTON_H);

            var hlg = go.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing               = 6f;
            hlg.childAlignment        = TextAnchor.MiddleCenter;
            hlg.childForceExpandWidth  = true;
            hlg.childForceExpandHeight = true;

            BuildTextButton(go.transform, "LevelUpButton", "LEVEL UP", 0f);
            BuildTextButton(go.transform, "RepairButton",  "REPAIR",   0f);
        }

        private static void BuildTextButton(Transform parent, string name, string label, float height)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            if (height > 0f) AddLayoutElement(go, preferredHeight: height);

            var img = go.AddComponent<Image>();
            img.color = new Color(0.2f, 0.2f, 0.3f, 1f);
            go.AddComponent<Button>();

            var textGO = new GameObject("Text (TMP)");
            textGO.transform.SetParent(go.transform, false);
            var textRT = textGO.AddComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.offsetMin = Vector2.zero;
            textRT.offsetMax = Vector2.zero;
            var tmp = textGO.AddComponent<TextMeshProUGUI>();
            tmp.text      = label;
            tmp.fontSize  = 11f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color     = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
        }

        private static void BuildDivider(Transform parent)
        {
            var go = new GameObject("Divider");
            go.transform.SetParent(parent, false);
            AddLayoutElement(go, preferredHeight: DIVIDER_H);
            go.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.1f);
        }

        // ── CarouselController Wiring ──────────────────────────────────────────

        private static void WireCarouselController(ClubCarouselController ctrl,
            RectTransform carouselSection, ClubDetailPanel detailPanel)
        {
            var so   = new SerializedObject(ctrl);
            var root = carouselSection;

            // contentParent = ScrollView/Viewport/Content
            var content = root.Find("ScrollView/Viewport/Content");
            if (content != null)
                so.FindProperty("contentParent").objectReferenceValue = content;
            else
                Debug.LogWarning("[ClubDetailPanelBuilder] Could not find Content — wire contentParent manually.");

            // leftArrowButton / rightArrowButton
            var leftBtn = root.Find("LeftArrow");
            if (leftBtn != null)
                so.FindProperty("leftArrowButton").objectReferenceValue = leftBtn.GetComponent<Button>();

            var rightBtn = root.Find("RightArrow");
            if (rightBtn != null)
                so.FindProperty("rightArrowButton").objectReferenceValue = rightBtn.GetComponent<Button>();

            // paginationDotsParent
            var dots = root.Find("PaginationDots");
            if (dots != null)
                so.FindProperty("paginationDotsParent").objectReferenceValue = dots;

            so.ApplyModifiedProperties();
        }

        // ── Scene search ───────────────────────────────────────────────────────

        private static Transform? FindClubsMainSection()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            foreach (var rootGO in scene.GetRootGameObjects())
            {
                var t = FindByName(rootGO.transform, "ClubsMainSection");
                if (t != null) return t;
            }
            return null;
        }

        private static Transform? FindByName(Transform parent, string name)
        {
            if (parent.name == name) return parent;
            foreach (Transform child in parent)
            {
                var result = FindByName(child, name);
                if (result != null) return result;
            }
            return null;
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private static LayoutElement AddLayoutElement(GameObject go,
            float preferredWidth = -1f, float preferredHeight = -1f)
        {
            var le = go.AddComponent<LayoutElement>();
            if (preferredWidth  >= 0f) le.preferredWidth  = preferredWidth;
            if (preferredHeight >= 0f) le.preferredHeight = preferredHeight;
            return le;
        }
    }
}
#endif
