#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Golfin.Inventory.Editor
{
    /// <summary>
    /// Builds the Compare Mode hierarchy inside ClubDetailPanel.
    ///
    /// Adds to ClubDetailPanel:
    ///   VerticalDivider           (1px line at horizontal center)
    ///   CompareRightPanel         (right 50%, contains placeholder + info)
    ///     ComparePlaceholder      (full-area overlay)
    ///       PlaceholderText       (TMP)
    ///     CompareInfoPanel        (full-area panel — mirrors RightPanel + DiffLabels)
    ///       ClubNameText
    ///       Divider
    ///       RarityLevelRow
    ///       Divider
    ///       StatsPanel
    ///         PowerRow            (StatsName | DiffLabel | Bar | StatNumber)
    ///         AccuracyRow
    ///         LieResistanceRow
    ///         LoftRow
    ///         DurabilityRow
    ///         DistanceRow         (StatsName | DiffLabel | Spacer | DistanceValue)
    ///       Divider
    ///       ButtonsPanel          (LevelUpButton | RepairButton)
    ///       CompareButton
    ///       BagLabel
    ///       EquipButton
    ///
    /// Also adds to RightPanel:
    ///   CloseCompareButton        (shown in compare mode, placed after CompareButton)
    ///   SwapButton                (shown in compare mode when left club not equipped, placed after EquipButton)
    ///
    /// Adds ClubCompareController component to ClubDetailPanel.
    ///
    /// Run: GOLFIN/Inventory/Build Club Compare Panel
    /// Safe to re-run — removes existing compare elements before rebuilding.
    /// </summary>
    public static class ClubCompareRightPanelBuilder
    {
        private const float DIVIDER_H = 1f;
        private const float ROW_H     = 26f;
        private const float BUTTON_H  = 36f;
        private const float PANEL_PAD = 8f;

        [MenuItem("GOLFIN/Build/Club Compare Panel")]
        public static void Run()
        {
            var detailPanelGO = FindByName("ClubDetailPanel");
            if (detailPanelGO == null)
            {
                EditorUtility.DisplayDialog("Build Club Compare Panel",
                    "ClubDetailPanel not found.\n\nRun GOLFIN/Inventory/Build Club Phase C first.", "OK");
                return;
            }

            var root = detailPanelGO.transform;

            // ── Remove stale compare elements ──────────────────────────────────
            DestroyIfExists(root, "CompareRightPanel");
            DestroyIfExists(root, "VerticalDivider");
            DestroyIfExists(root.Find("RightPanel"), "CloseCompareButton");
            DestroyIfExists(root.Find("RightPanel"), "SwapButton");

            // ── Build new elements ─────────────────────────────────────────────
            BuildVerticalDivider(root);
            BuildCompareRightPanel(root);
            AddLeftPanelButtons(root.Find("RightPanel"));

            // ── Add / get ClubCompareController ───────────────────────────────
            if (detailPanelGO.GetComponent<ClubCompareController>() == null)
                detailPanelGO.AddComponent<ClubCompareController>();

            EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());

            EditorUtility.DisplayDialog("Build Club Compare Panel",
                "Done!\n\n" +
                "• CompareRightPanel built with placeholder + info panel\n" +
                "• VerticalDivider added\n" +
                "• CloseCompareButton and SwapButton added to RightPanel\n" +
                "• ClubCompareController added to ClubDetailPanel\n\n" +
                "Next step: Run GOLFIN/Inventory/Wire Club Compare Panel",
                "OK");

            Debug.Log("[ClubCompareRightPanelBuilder] ✓ Compare panel built.");
        }

        // ── Vertical Divider ───────────────────────────────────────────────────

        private static void BuildVerticalDivider(Transform parent)
        {
            var go = new GameObject("VerticalDivider");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(2f, 0f);

            go.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.2f);
            go.SetActive(false); // hidden until compare mode
        }

        // ── Compare Right Panel ────────────────────────────────────────────────

        private static void BuildCompareRightPanel(Transform parent)
        {
            var go = new GameObject("CompareRightPanel");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            go.AddComponent<Image>().color = new Color(0.10f, 0.10f, 0.14f, 1f);

            // Add a CanvasGroup so FadeIn/FadeOut works
            go.AddComponent<CanvasGroup>();

            // ComparePlaceholder (full-area overlay)
            BuildComparePlaceholder(go.transform);

            // CompareInfoPanel (full-area — hidden until a club is selected)
            BuildCompareInfoPanel(go.transform);

            go.SetActive(false); // hidden until compare mode
        }

        private static void BuildComparePlaceholder(Transform parent)
        {
            var go = new GameObject("ComparePlaceholder");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            go.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f); // transparent bg

            // PlaceholderText
            var textGO = new GameObject("PlaceholderText");
            textGO.transform.SetParent(go.transform, false);
            var textRT = textGO.AddComponent<RectTransform>();
            textRT.anchorMin = new Vector2(0f, 0.3f);
            textRT.anchorMax = new Vector2(1f, 0.7f);
            textRT.offsetMin = new Vector2(PANEL_PAD, 0f);
            textRT.offsetMax = new Vector2(-PANEL_PAD, 0f);

            var tmp = textGO.AddComponent<TextMeshProUGUI>();
            tmp.text      = "TAP ON ANY OTHER CLUB TO COMPARE STATS";
            tmp.fontSize  = 12f;
            tmp.color     = new Color(0.7f, 0.7f, 0.7f, 1f);
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.enableWordWrapping = true;
        }

        private static void BuildCompareInfoPanel(Transform parent)
        {
            var go = new GameObject("CompareInfoPanel");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
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

            BuildDivider(go.transform);
            BuildRarityLevelRow(go.transform);
            BuildDivider(go.transform);
            BuildCompareStatsPanel(go.transform);
            BuildDivider(go.transform);
            BuildButtonsPanel(go.transform);
            BuildTextButton(go.transform, "CompareButton",  "COMPARE", BUTTON_H);

            // BagLabel
            var bagGO = new GameObject("BagLabel");
            bagGO.transform.SetParent(go.transform, false);
            AddLayoutElement(bagGO, preferredHeight: 18f);
            var bagTMP = bagGO.AddComponent<TextMeshProUGUI>();
            bagTMP.text      = "IN BAG 1";
            bagTMP.fontSize  = 11f;
            bagTMP.color     = new Color(0.3f, 0.6f, 1f, 1f);
            bagTMP.alignment = TextAlignmentOptions.Center;
            bagGO.SetActive(false);

            BuildTextButton(go.transform, "EquipButton", "EQUIP", BUTTON_H);

            go.SetActive(false); // hidden until a club is tapped
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

            var rarityGO = new GameObject("RarityLabel");
            rarityGO.transform.SetParent(go.transform, false);
            AddLayoutElement(rarityGO, preferredWidth: 70f);
            var rarityTMP = rarityGO.AddComponent<TextMeshProUGUI>();
            rarityTMP.text      = "COMMON";
            rarityTMP.fontSize  = 12f;
            rarityTMP.fontStyle = FontStyles.Bold;
            rarityTMP.color     = Color.white;

            var levelGO = new GameObject("LevelText");
            levelGO.transform.SetParent(go.transform, false);
            AddLayoutElement(levelGO, preferredWidth: 50f);
            var levelTMP = levelGO.AddComponent<TextMeshProUGUI>();
            levelTMP.text     = "Lv 1";
            levelTMP.fontSize = 12f;
            levelTMP.color    = Color.white;

            var maxGO = new GameObject("LevelTextMax");
            maxGO.transform.SetParent(go.transform, false);
            AddLayoutElement(maxGO, preferredWidth: 40f);
            var maxTMP = maxGO.AddComponent<TextMeshProUGUI>();
            maxTMP.text     = "/119";
            maxTMP.fontSize = 12f;
            maxTMP.color    = new Color(0.6f, 0.6f, 0.6f, 1f);
        }

        private static void BuildCompareStatsPanel(Transform parent)
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

            BuildCompareStatRow(go.transform, "PowerRow");
            BuildCompareStatRow(go.transform, "AccuracyRow");
            BuildCompareStatRow(go.transform, "LieResistanceRow");
            BuildCompareStatRow(go.transform, "LoftRow");
            BuildCompareStatRow(go.transform, "DurabilityRow");
            BuildCompareDistanceRow(go.transform);
        }

        /// <summary>
        /// Stat row with a DiffLabel between the stat name and the bar:
        /// StatsName (72px) | DiffLabel (35px, hidden) | Bar (flex) | StatNumber (42px)
        /// </summary>
        private static void BuildCompareStatRow(Transform parent, string name)
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
            nameTMP.text         = name.Replace("Row", "").ToUpper();
            nameTMP.fontSize     = 9f;
            nameTMP.color        = new Color(0.7f, 0.7f, 0.7f, 1f);
            nameTMP.overflowMode = TextOverflowModes.Ellipsis;

            // DiffLabel (initially hidden — shown when compare data is loaded)
            var diffGO = new GameObject("DiffLabel");
            diffGO.transform.SetParent(go.transform, false);
            AddLayoutElement(diffGO, preferredWidth: 35f);
            var diffTMP = diffGO.AddComponent<TextMeshProUGUI>();
            diffTMP.text      = "+0";
            diffTMP.fontSize  = 9f;
            diffTMP.fontStyle = FontStyles.Bold;
            diffTMP.color     = new Color(0.2f, 0.8f, 0.3f, 1f);
            diffTMP.alignment = TextAlignmentOptions.Left;
            diffGO.SetActive(false);

            // Bar
            var barGO = new GameObject("Bar");
            barGO.transform.SetParent(go.transform, false);
            var barLE = AddLayoutElement(barGO, preferredHeight: 8f);
            barLE.flexibleWidth = 1f;

            var barImg = barGO.AddComponent<Image>();
            barImg.color      = new Color(0.2f, 0.5f, 0.9f, 1f);
            barImg.type       = Image.Type.Filled;
            barImg.fillMethod = Image.FillMethod.Horizontal;
            barImg.fillOrigin = 0;
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

        /// <summary>
        /// Distance row with DiffLabel:
        /// StatsName (72px) | DiffLabel (35px, hidden) | Spacer (flex) | DistanceValue (55px)
        /// </summary>
        private static void BuildCompareDistanceRow(Transform parent)
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

            // DiffLabel
            var diffGO = new GameObject("DiffLabel");
            diffGO.transform.SetParent(go.transform, false);
            AddLayoutElement(diffGO, preferredWidth: 35f);
            var diffTMP = diffGO.AddComponent<TextMeshProUGUI>();
            diffTMP.text      = "+0";
            diffTMP.fontSize  = 9f;
            diffTMP.fontStyle = FontStyles.Bold;
            diffTMP.color     = new Color(0.2f, 0.8f, 0.3f, 1f);
            diffTMP.alignment = TextAlignmentOptions.Left;
            diffGO.SetActive(false);

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

        // ── Left Column Button Additions ───────────────────────────────────────

        /// <summary>
        /// Adds CloseCompareButton (after CompareButton) and SwapButton (after EquipButton)
        /// to the existing RightPanel VLG. Both start hidden.
        /// </summary>
        private static void AddLeftPanelButtons(Transform? rightPanel)
        {
            if (rightPanel == null)
            {
                Debug.LogWarning("[ClubCompareRightPanelBuilder] RightPanel not found — skipping left-column buttons.");
                return;
            }

            // CloseCompareButton — insert immediately after CompareButton
            var compareBtn = rightPanel.Find("CompareButton");
            int closeIdx = compareBtn != null ? compareBtn.GetSiblingIndex() + 1 : rightPanel.childCount;

            var closeBtnGO = BuildTextButtonGO(rightPanel, "CloseCompareButton", "CLOSE COMPARE", BUTTON_H);
            closeBtnGO.transform.SetSiblingIndex(closeIdx);
            closeBtnGO.SetActive(false);

            // SwapButton — insert immediately after EquipButton
            var equipBtn = rightPanel.Find("EquipButton");
            int swapIdx  = equipBtn != null ? equipBtn.GetSiblingIndex() + 1 : rightPanel.childCount;

            var swapBtnGO = BuildTextButtonGO(rightPanel, "SwapButton", "SWAP", BUTTON_H);
            swapBtnGO.transform.SetSiblingIndex(swapIdx);
            swapBtnGO.SetActive(false);
        }

        // ── Shared Builders ────────────────────────────────────────────────────

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
            BuildTextButtonGO(parent, name, label, height);
        }

        private static GameObject BuildTextButtonGO(Transform parent, string name, string label, float height)
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

            return go;
        }

        private static void BuildDivider(Transform parent)
        {
            var go = new GameObject("Divider");
            go.transform.SetParent(parent, false);
            AddLayoutElement(go, preferredHeight: DIVIDER_H);
            go.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.1f);
        }

        // ── Utilities ──────────────────────────────────────────────────────────

        private static LayoutElement AddLayoutElement(GameObject go,
            float preferredWidth = -1f, float preferredHeight = -1f)
        {
            var le = go.AddComponent<LayoutElement>();
            if (preferredWidth  >= 0f) le.preferredWidth  = preferredWidth;
            if (preferredHeight >= 0f) le.preferredHeight = preferredHeight;
            return le;
        }

        private static void DestroyIfExists(Transform? parent, string childName)
        {
            if (parent == null) return;
            var child = parent.Find(childName);
            if (child != null) Object.DestroyImmediate(child.gameObject);
        }

        private static GameObject? FindByName(string name)
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
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
