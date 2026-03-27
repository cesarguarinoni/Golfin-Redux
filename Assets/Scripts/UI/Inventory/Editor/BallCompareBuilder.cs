#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Golfin.Inventory.Editor
{
    /// <summary>
    /// Builds the Compare Mode hierarchy inside BallDetailPanel, then wires
    /// BallCompareController and BallDetailPanel.compareController.
    ///
    /// Strategy (mirrors ClubCompareRightPanelBuilder):
    ///   1. Clone RightPanel → CompareInfoPanel (inherits all fonts/colors/layout)
    ///   2. Wrap in CompareRightPanel (right-half overlay)
    ///   3. Add ComparePlaceholder overlay
    ///   4. Add DiffLabel TMP to each stat row
    ///   5. Add CloseCompareButton to RightPanel
    ///   6. Add VerticalDivider
    ///   7. Add + wire BallCompareController
    ///
    /// Run: GOLFIN/Setup/Ball Compare
    /// Safe to re-run.
    /// </summary>
    public static class BallCompareBuilder
    {
        private const float BUTTON_H  = 36f;
        private const float PANEL_PAD = 8f;

        [MenuItem("GOLFIN/Setup/Ball Compare")]
        public static void Run()
        {
            // ── Find BallDetailPanel ──────────────────────────────────────────
            BallDetailPanel? detailScript = null;
            foreach (var obj in Resources.FindObjectsOfTypeAll<BallDetailPanel>())
            {
                if (obj.gameObject.scene.isLoaded) { detailScript = obj; break; }
            }

            if (detailScript == null)
            {
                EditorUtility.DisplayDialog("Ball Compare Builder",
                    "BallDetailPanel not found in scene.", "OK");
                return;
            }

            var root = detailScript.transform;

            // ── Remove stale compare elements ─────────────────────────────────
            DestroyIfExists(root, "CompareRightPanel");
            DestroyIfExists(root, "VerticalDivider");
            DestroyIfExists(root.Find("RightPanel"), "CloseCompareButton");

            // ── Build hierarchy ───────────────────────────────────────────────
            BuildVerticalDivider(root);
            var crp         = BuildCompareRightPanel(root);  // returns CompareRightPanel
            var cipGO       = crp.Find("CompareInfoPanel")?.gameObject;
            var placeholder = crp.Find("ComparePlaceholder")?.gameObject;

            // ── Add CloseCompareButton to RightPanel ──────────────────────────
            var rightPanelT = root.Find("RightPanel");
            var closeBtnGO  = BuildButton(rightPanelT!, "CloseCompareButton", "CLOSE COMPARE");
            var compareBtnT = rightPanelT?.Find("CompareButton");
            if (compareBtnT != null)
                closeBtnGO.transform.SetSiblingIndex(compareBtnT.GetSiblingIndex() + 1);
            closeBtnGO.SetActive(false);

            // ── Add / get BallCompareController ───────────────────────────────
            var ctrl = detailScript.GetComponent<BallCompareController>();
            if (ctrl == null) ctrl = detailScript.gameObject.AddComponent<BallCompareController>();

            // ── Wire BallCompareController ────────────────────────────────────
            var cso = new SerializedObject(ctrl);

            // Layout
            Prop(cso, "leftPanel",     root.Find("LeftPanel")?.gameObject);
            Prop(cso, "rightPanel",    rightPanelT?.GetComponent<RectTransform>());
            Prop(cso, "compareRightPanel",  crp.gameObject);
            Prop(cso, "comparePlaceholder", placeholder);

            var placeholderText = crp.Find("ComparePlaceholder/PlaceholderText")
                                     ?.GetComponent<TextMeshProUGUI>();
            Prop(cso, "comparePlaceholderText", placeholderText);
            Prop(cso, "compareInfoPanel", cipGO);
            Prop(cso, "verticalDivider",  root.Find("VerticalDivider")?.gameObject);

            // Buttons
            Prop(cso, "compareButton",      compareBtnT?.GetComponent<Button>());
            Prop(cso, "closeCompareButton", closeBtnGO.GetComponent<Button>());

            // Right column — name + quantity
            var cipT = cipGO?.transform;
            Prop(cso, "compareNameText",     cipT?.Find("BallNameText")?.GetComponent<TextMeshProUGUI>());
            Prop(cso, "compareQuantityText", cipT?.Find("OwnedPanel/QuantityText")?.GetComponent<TextMeshProUGUI>());

            // Right column — stat rows (cloned names match RightPanel exactly)
            WireStatRow(cso, "Power",          cipT, "PowerRow");
            WireStatRow(cso, "Rebound",        cipT, "ReboundRow");
            WireStatRow(cso, "WindResistance", cipT, "WindResistanceRow");
            WireStatRow(cso, "Roll",           cipT, "RollRow");
            WireStatRow(cso, "Spin",           cipT, "SpinRow");

            // Right column — COMPARE button (repurposed from cloned CompareButton)
            Prop(cso, "compareRightCompareButton",
                 cipT?.Find("CompareButton")?.GetComponent<Button>());

            // Carousel
            var carousel = Object.FindObjectOfType<BallCarouselController>(true);
            Prop(cso, "carousel", carousel);

            cso.ApplyModifiedProperties();
            EditorUtility.SetDirty(ctrl);

            // ── Wire BallDetailPanel.compareController ────────────────────────
            var dso = new SerializedObject(detailScript);
            dso.FindProperty("compareController").objectReferenceValue = ctrl;
            dso.ApplyModifiedProperties();
            EditorUtility.SetDirty(detailScript);

            EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());

            Debug.Log("[BallCompareBuilder] Done.");
            EditorUtility.DisplayDialog("Ball Compare Builder",
                "Done!\n\nCompareRightPanel built (cloned from RightPanel).\n" +
                "BallCompareController wired.\n\n" +
                "Hit Play → BALLS tab → COMPARE button to test.", "OK");
        }

        // ── Hierarchy Builders ────────────────────────────────────────────────

        private static void BuildVerticalDivider(Transform parent)
        {
            var go = new GameObject("VerticalDivider");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(2f, 0f);
            go.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.2f);
            go.SetActive(false);
        }

        /// <summary>Returns the CompareRightPanel transform.</summary>
        private static Transform BuildCompareRightPanel(Transform parent)
        {
            var go = new GameObject("CompareRightPanel");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            go.AddComponent<Image>().color = new Color(0.10f, 0.10f, 0.14f, 1f);
            go.AddComponent<CanvasGroup>();

            BuildComparePlaceholder(go.transform);
            BuildCompareInfoPanel(go.transform);

            go.SetActive(false);
            return go.transform;
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
            go.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f);

            var textGO = new GameObject("PlaceholderText");
            textGO.transform.SetParent(go.transform, false);
            var textRT = textGO.AddComponent<RectTransform>();
            textRT.anchorMin = new Vector2(0f, 0.3f);
            textRT.anchorMax = new Vector2(1f, 0.7f);
            textRT.offsetMin = new Vector2(PANEL_PAD, 0f);
            textRT.offsetMax = new Vector2(-PANEL_PAD, 0f);
            var tmp = textGO.AddComponent<TextMeshProUGUI>();
            tmp.text                = "TAP ON ANY OTHER BALL TO COMPARE STATS";
            tmp.fontSize            = 12f;
            tmp.color               = new Color(0.7f, 0.7f, 0.7f, 1f);
            tmp.alignment           = TextAlignmentOptions.Center;
            tmp.enableWordWrapping  = true;
        }

        private static void BuildCompareInfoPanel(Transform compareRightPanel)
        {
            var detailPanel = compareRightPanel.parent;
            var rightPanel  = detailPanel.Find("RightPanel");
            if (rightPanel == null)
            {
                Debug.LogError("[BallCompareBuilder] RightPanel not found — cannot clone.");
                return;
            }

            // Clone RightPanel — inherits all fonts, colors, layout settings
            var clone = Object.Instantiate(rightPanel.gameObject, compareRightPanel, false);
            clone.name = "CompareInfoPanel";

            var rt = clone.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            // Remove duplicated BallDetailPanel component
            var dp = clone.GetComponent<BallDetailPanel>();
            if (dp != null) Object.DestroyImmediate(dp);

            // Add DiffLabel TMP to each stat row
            string[] rows = { "PowerRow", "ReboundRow", "WindResistanceRow", "RollRow", "SpinRow" };
            foreach (var rowName in rows)
            {
                var row = FindByName(clone.transform, rowName);
                if (row == null) { Debug.LogWarning($"[BallCompareBuilder] {rowName} not found in clone."); continue; }

                var diffGO  = new GameObject("DiffLabel");
                diffGO.transform.SetParent(row, false);
                diffGO.transform.SetSiblingIndex(row.childCount - 1); // last child (after StatNumber)
                var le      = diffGO.AddComponent<LayoutElement>();
                le.preferredWidth = 30f;
                var diffTMP = diffGO.AddComponent<TextMeshProUGUI>();
                diffTMP.text      = "";
                diffTMP.fontSize  = 10f;
                diffTMP.fontStyle = FontStyles.Bold;
                diffTMP.color     = new Color(0.2f, 0.8f, 0.3f, 1f);
                diffTMP.alignment = TextAlignmentOptions.Left;
                diffGO.SetActive(false);
            }

            clone.SetActive(false);
        }

        private static GameObject BuildButton(Transform parent, string name, string label)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = BUTTON_H;

            go.AddComponent<Image>().color = new Color(0.2f, 0.2f, 0.3f, 1f);
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

        // ── Wiring Helpers ────────────────────────────────────────────────────

        private static void WireStatRow(SerializedObject so, string statName,
            Transform? cipT, string rowName)
        {
            var row = cipT != null ? FindByName(cipT, rowName) : null;
            if (row == null) { Debug.LogWarning($"[BallCompareBuilder] {rowName} not found."); return; }

            // Name: Name+Bar/StatsName
            Prop(so, $"compare{statName}Name",
                 row.Find("Name+Bar/StatsName")?.GetComponent<TextMeshProUGUI>());

            // Bar: Name+Bar/BarContainer (Image — BallSegmentedBar added at runtime)
            Prop(so, $"compare{statName}Bar",
                 row.Find("Name+Bar/BarContainer")?.GetComponent<Image>());

            // Number: StatNumber
            Prop(so, $"compare{statName}Number",
                 row.Find("StatNumber")?.GetComponent<TextMeshProUGUI>());

            // Diff: DiffLabel (added by this builder above)
            Prop(so, $"compare{statName}Diff",
                 row.Find("DiffLabel")?.GetComponent<TextMeshProUGUI>());
        }

        private static void Prop(SerializedObject so, string propName, Object? value)
        {
            var p = so.FindProperty(propName);
            if (p != null)
                p.objectReferenceValue = value;
            else
                Debug.LogWarning($"[BallCompareBuilder] Property not found: {propName}");
        }

        // ── Utilities ─────────────────────────────────────────────────────────

        private static void DestroyIfExists(Transform? parent, string childName)
        {
            if (parent == null) return;
            var child = parent.Find(childName);
            if (child != null) Object.DestroyImmediate(child.gameObject);
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
    }
}
#endif
