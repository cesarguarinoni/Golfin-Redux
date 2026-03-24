#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

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

            // ── Capture existing CompareInfoPanel formatting before destroying ──
            // Preserves manual Inspector fixes (font size, autosize, alignment, etc.)
            // made to ClubNameText and RarityLevelRow children so they survive a rebuild.
            var savedFormats = CaptureCompareInfoPanelFormats(root);

            // ── Remove stale compare elements ──────────────────────────────────
            DestroyIfExists(root, "CompareRightPanel");
            DestroyIfExists(root, "VerticalDivider");
            DestroyIfExists(root.Find("RightPanel"), "CloseCompareButton");
            DestroyIfExists(root.Find("RightPanel"), "SwapButton");

            // ── Build new elements ─────────────────────────────────────────────
            BuildVerticalDivider(root);
            BuildCompareRightPanel(root);
            AddLeftPanelButtons(root.Find("RightPanel"));

            // ── Restore saved formatting onto newly-cloned CompareInfoPanel ────
            RestoreCompareInfoPanelFormats(root, savedFormats);

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

        // ── Formatting Snapshot — preserves manual Inspector edits on rebuild ─

        private struct TMPSnapshot
        {
            public TMP_FontAsset? font;
            public float          fontSize;
            public float          fontSizeMin;
            public float          fontSizeMax;
            public bool           enableAutoSizing;
            public FontStyles     fontStyle;
            public TextAlignmentOptions alignment;
            public bool           enableWordWrapping;
            public TextOverflowModes overflowMode;
        }

        private struct RectSnapshot
        {
            public Vector2 anchorMin, anchorMax, pivot;
            public Vector2 offsetMin, offsetMax;
        }

        private struct ElementSnapshot
        {
            public TMPSnapshot  tmp;
            public RectSnapshot rect;
        }

        /// <summary>
        /// Names of elements inside CompareInfoPanel whose formatting should survive a rebuild.
        /// Includes ClubNameText, and all three children of RarityLevelRow.
        /// </summary>
        private static readonly string[] SnapshotTargets =
        {
            "ClubNameText",
            "RarityLabel",
            "LevelText",
            "LevelTextMax"
        };

        private static Dictionary<string, ElementSnapshot> CaptureCompareInfoPanelFormats(Transform root)
        {
            var result = new Dictionary<string, ElementSnapshot>();
            var cip    = root.Find("CompareRightPanel/CompareInfoPanel");
            if (cip == null) return result; // nothing to capture on first build

            foreach (var name in SnapshotTargets)
            {
                var t   = FindTransformByName(cip, name);
                var tmp = t?.GetComponent<TextMeshProUGUI>();
                var rt  = t?.GetComponent<RectTransform>();
                if (tmp == null || rt == null) continue;

                result[name] = new ElementSnapshot
                {
                    tmp = new TMPSnapshot
                    {
                        font              = tmp.font,
                        fontSize          = tmp.fontSize,
                        fontSizeMin       = tmp.fontSizeMin,
                        fontSizeMax       = tmp.fontSizeMax,
                        enableAutoSizing  = tmp.enableAutoSizing,
                        fontStyle         = tmp.fontStyle,
                        alignment         = tmp.alignment,
                        enableWordWrapping = tmp.enableWordWrapping,
                        overflowMode      = tmp.overflowMode
                    },
                    rect = new RectSnapshot
                    {
                        anchorMin = rt.anchorMin, anchorMax = rt.anchorMax,
                        pivot     = rt.pivot,
                        offsetMin = rt.offsetMin, offsetMax = rt.offsetMax
                    }
                };
            }

            return result;
        }

        private static void RestoreCompareInfoPanelFormats(
            Transform root, Dictionary<string, ElementSnapshot> saved)
        {
            if (saved.Count == 0) return;

            var cip = root.Find("CompareRightPanel/CompareInfoPanel");
            if (cip == null) return;

            foreach (var kv in saved)
            {
                var t   = FindTransformByName(cip, kv.Key);
                var tmp = t?.GetComponent<TextMeshProUGUI>();
                var rt  = t?.GetComponent<RectTransform>();
                if (tmp == null) continue;

                var s = kv.Value.tmp;
                if (s.font != null) tmp.font = s.font;
                tmp.fontSize          = s.fontSize;
                tmp.fontSizeMin       = s.fontSizeMin;
                tmp.fontSizeMax       = s.fontSizeMax;
                tmp.enableAutoSizing  = s.enableAutoSizing;
                tmp.fontStyle         = s.fontStyle;
                tmp.alignment         = s.alignment;
                tmp.enableWordWrapping = s.enableWordWrapping;
                tmp.overflowMode      = s.overflowMode;
                EditorUtility.SetDirty(tmp);

                if (rt != null)
                {
                    var r         = kv.Value.rect;
                    rt.anchorMin  = r.anchorMin;
                    rt.anchorMax  = r.anchorMax;
                    rt.pivot      = r.pivot;
                    rt.offsetMin  = r.offsetMin;
                    rt.offsetMax  = r.offsetMax;
                    EditorUtility.SetDirty(rt);
                }
            }
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

        private static void BuildCompareInfoPanel(Transform compareRightPanel)
        {
            // Find the existing styled RightPanel
            var detailPanel = compareRightPanel.parent;
            var rightPanel  = detailPanel.Find("RightPanel");
            if (rightPanel == null)
            {
                Debug.LogError("[ClubCompareRightPanelBuilder] RightPanel not found — cannot clone.");
                return;
            }

            // Clone the entire RightPanel — inherits all fonts, colors, sprites, layout
            var clone = Object.Instantiate(rightPanel.gameObject, compareRightPanel, false);
            clone.name = "CompareInfoPanel";

            // Reset RectTransform to fill parent
            var rt = clone.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            // Add DiffLabel TMP to each stat row — insert after first child (StatsName/icon), before bar
            string[] statRows = { "PowerRow", "AccuracyRow", "LieResistanceRow", "LoftRow", "DurabilityRow" };
            foreach (var rowName in statRows)
            {
                var row = FindTransformByName(clone.transform, rowName);
                if (row == null) continue;

                var diffGO  = new GameObject("DiffLabel");
                diffGO.transform.SetParent(row, false);
                diffGO.transform.SetSiblingIndex(1); // after icon/name, before bar
                var diffLE  = diffGO.AddComponent<LayoutElement>();
                diffLE.preferredWidth = 45f;
                var diffTMP = diffGO.AddComponent<TextMeshProUGUI>();
                diffTMP.text      = "";
                diffTMP.fontSize  = 10f;
                diffTMP.fontStyle = FontStyles.Bold;
                diffTMP.color     = new Color(0.2f, 0.8f, 0.3f, 1f);
                diffTMP.alignment = TextAlignmentOptions.Left;
                diffGO.SetActive(false);
            }

            // Add DiffLabel to DistanceRow too
            var distRow = FindTransformByName(clone.transform, "DistanceRow");
            if (distRow != null)
            {
                var diffGO  = new GameObject("DiffLabel");
                diffGO.transform.SetParent(distRow, false);
                diffGO.transform.SetSiblingIndex(1);
                var diffLE  = diffGO.AddComponent<LayoutElement>();
                diffLE.preferredWidth = 45f;
                var diffTMP = diffGO.AddComponent<TextMeshProUGUI>();
                diffTMP.text      = "";
                diffTMP.fontSize  = 10f;
                diffTMP.fontStyle = FontStyles.Bold;
                diffTMP.color     = new Color(0.2f, 0.8f, 0.3f, 1f);
                diffTMP.alignment = TextAlignmentOptions.Left;
                diffGO.SetActive(false);
            }

            // Remove ClubDetailPanel script if it was duplicated by the clone
            var detailPanelScript = clone.GetComponent<ClubDetailPanel>();
            if (detailPanelScript != null) Object.DestroyImmediate(detailPanelScript);

            clone.SetActive(false); // hidden until a club is tapped
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
