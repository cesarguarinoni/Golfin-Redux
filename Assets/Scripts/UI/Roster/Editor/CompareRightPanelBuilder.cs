#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Golfin.Roster.Editor
{
    /// <summary>
    /// Adds the Compare Mode UI nodes to the existing DetailPanel hierarchy.
    /// DOES NOT touch LeftPanel, RightPanel, or any existing children.
    ///
    /// Nodes created / modified:
    ///   DetailPanel
    ///   ├── RightPanel/ButtonsPanel
    ///   │   ├── CloseCompareButton   (NEW — hidden by default)
    ///   │   └── SwapButton           (NEW — hidden by default)
    ///   ├── VerticalDivider          (NEW — hidden by default)
    ///   └── CompareRightPanel        (NEW — hidden by default)
    ///       ├── ComparePlaceholder   (TMP)
    ///       └── CompareInfoPanel     (VerticalLayoutGroup)
    ///           ├── CompareNameText
    ///           ├── CompareRarityRow
    ///           │   ├── CompareRarityLabel
    ///           │   ├── CompareLevelText
    ///           │   └── CompareMaxLevelText
    ///           ├── CompareStatsPanel
    ///           │   ├── CompareStats1   (Name+Bar/Bar + StatNumber)
    ///           │   ├── CompareStats2
    ///           │   ├── CompareStats3
    ///           │   └── CompareStats4
    ///           ├── CompareButtonsPanel
    ///           │   ├── CompareLevelUpButton
    ///           │   └── CompareBoostButton
    ///           ├── CompareBioPanel
    ///           │   └── CompareBioText
    ///           └── CompareActionsPanel
    ///               ├── CompareRightCompareButton
    ///               └── CompareRightSelectButton
    ///                   └── Text (TMP)
    ///
    /// Run GOLFIN > Wire Compare Panel after this to populate Inspector fields.
    /// </summary>
    public static class CompareRightPanelBuilder
    {
        [MenuItem("GOLFIN/Build Compare Right Panel")]
        public static void Build()
        {
            var detailPanel = FindDetailPanel();
            if (detailPanel == null)
            {
                EditorUtility.DisplayDialog("Error",
                    "DetailPanel not found in scene. Open ShellScene and make sure DetailPanel exists.",
                    "OK");
                return;
            }

            // Guard: don't double-build
            if (detailPanel.Find("CompareRightPanel") != null)
            {
                if (!EditorUtility.DisplayDialog("Replace existing?",
                    "CompareRightPanel already exists. Replace it?",
                    "Yes, Replace", "Cancel"))
                    return;
                Object.DestroyImmediate(detailPanel.Find("CompareRightPanel").gameObject);
            }

            // ── 1. Add CloseCompareButton + SwapButton to RightPanel/ButtonsPanel ──
            AddLeftColumnButtons(detailPanel);

            // ── 2. VerticalDivider ──────────────────────────────────────────────
            BuildVerticalDivider(detailPanel);

            // ── 3. CompareRightPanel ────────────────────────────────────────────
            BuildCompareRightPanel(detailPanel);

            // ── Finalise ────────────────────────────────────────────────────────
            EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());

            Selection.activeGameObject = detailPanel.gameObject;

            EditorUtility.DisplayDialog("Done!",
                "Compare Mode hierarchy added to DetailPanel.\n\n" +
                "Next: run GOLFIN > Wire Compare Panel to populate Inspector fields.",
                "OK");

            Debug.Log("[CompareBuilder] Build complete.");
        }

        // ── Find helpers ─────────────────────────────────────────────────────

        private static Transform? FindDetailPanel()
        {
            var direct = GameObject.Find("DetailPanel");
            if (direct != null) return direct.transform;
            var canvas = GameObject.Find("Canvas");
            return canvas?.transform.Find("ScreensRoot/RosterScreen/CarouselSection/DetailPanel");
        }

        // ── Left column extra buttons ─────────────────────────────────────────

        private static void AddLeftColumnButtons(Transform detailPanel)
        {
            // Try to find ButtonsPanel — adjust path if your hierarchy differs
            var buttonsPanel = detailPanel.Find("RightPanel/ButtonsPanel");
            if (buttonsPanel == null)
            {
                Debug.LogWarning("[CompareBuilder] RightPanel/ButtonsPanel not found. " +
                    "CloseCompareButton and SwapButton will be added to DetailPanel root — " +
                    "move them manually into the correct ButtonsPanel.");
                buttonsPanel = detailPanel;
            }

            // CloseCompareButton
            if (buttonsPanel.Find("CloseCompareButton") == null)
            {
                var btn = MakeButton("CloseCompareButton", buttonsPanel, "CLOSE COMPARE",
                    new Color(0.45f, 0.45f, 0.45f, 1f));
                btn.SetActive(false); // hidden until compare mode
                Debug.Log("[CompareBuilder] ✓ Created CloseCompareButton");
            }

            // SwapButton
            if (buttonsPanel.Find("SwapButton") == null)
            {
                var btn = MakeButton("SwapButton", buttonsPanel, "SWAP",
                    new Color(0.85f, 0.72f, 0.2f, 1f));
                btn.SetActive(false); // hidden until compare mode
                Debug.Log("[CompareBuilder] ✓ Created SwapButton");
            }
        }

        // ── VerticalDivider ───────────────────────────────────────────────────

        private static void BuildVerticalDivider(Transform parent)
        {
            var go = new GameObject("VerticalDivider");
            go.transform.SetParent(parent, false);

            var rect = go.AddComponent<RectTransform>();
            // Anchor at horizontal centre, full vertical stretch
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot     = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(2f, 0f);
            rect.anchoredPosition = Vector2.zero;

            go.AddComponent<Image>().color = new Color(0.35f, 0.35f, 0.35f, 1f);
            go.SetActive(false); // hidden until compare mode

            Debug.Log("[CompareBuilder] ✓ Created VerticalDivider");
        }

        // ── CompareRightPanel ─────────────────────────────────────────────────

        private static void BuildCompareRightPanel(Transform parent)
        {
            // Root: CompareRightPanel — right half of the detail panel
            var panel = MakeRect("CompareRightPanel", parent);
            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0f);
            panelRect.anchorMax = new Vector2(1f,   1f);
            panelRect.sizeDelta = Vector2.zero;
            panelRect.anchoredPosition = Vector2.zero;
            panel.SetActive(false);

            // ComparePlaceholder — shown when no second character selected
            var placeholder = new GameObject("ComparePlaceholder");
            placeholder.transform.SetParent(panel.transform, false);
            var phRect = placeholder.AddComponent<RectTransform>();
            phRect.anchorMin = Vector2.zero;
            phRect.anchorMax = Vector2.one;
            phRect.sizeDelta = Vector2.zero;
            phRect.anchoredPosition = Vector2.zero;
            var phTMP = placeholder.AddComponent<TextMeshProUGUI>();
            phTMP.text      = "TAP ON ANY OTHER CHARACTER\nTO COMPARE STATS";
            phTMP.fontSize  = 16;
            phTMP.alignment = TextAlignmentOptions.Center;
            phTMP.color     = new Color(0.6f, 0.6f, 0.6f, 1f);
            placeholder.SetActive(true);

            // CompareInfoPanel — shown once second character is selected
            var info = MakeRect("CompareInfoPanel", panel.transform);
            var infoRect = info.GetComponent<RectTransform>();
            infoRect.anchorMin = Vector2.zero;
            infoRect.anchorMax = Vector2.one;
            infoRect.sizeDelta = Vector2.zero;
            infoRect.anchoredPosition = Vector2.zero;
            var vLayout = info.AddComponent<VerticalLayoutGroup>();
            vLayout.padding            = new RectOffset(8, 8, 8, 8);
            vLayout.spacing            = 6;
            vLayout.childAlignment     = TextAnchor.UpperLeft;
            vLayout.childControlWidth  = true;
            vLayout.childControlHeight = false;
            vLayout.childForceExpandWidth  = true;
            vLayout.childForceExpandHeight = false;
            info.SetActive(false);

            BuildCompareInfoContents(info.transform);

            Debug.Log("[CompareBuilder] ✓ Created CompareRightPanel");
        }

        private static void BuildCompareInfoContents(Transform parent)
        {
            // CompareNameText
            var nameTMP = MakeTMP("CompareNameText", parent, "NAME", 18, TextAlignmentOptions.Left);
            LE(nameTMP.gameObject, preferredHeight: 44);

            // CompareRarityRow
            {
                var row = MakeHRow("CompareRarityRow", parent, 22);
                MakeTMP("CompareRarityLabel",   row.transform, "COMMON", 14, TextAlignmentOptions.Left);
                MakeTMP("CompareLevelText",      row.transform, "Lv 1",  14, TextAlignmentOptions.Center);
                MakeTMP("CompareMaxLevelText",   row.transform, "/199",  13, TextAlignmentOptions.Left);
            }

            // CompareStatsPanel
            {
                var statsPanel = MakeRect("CompareStatsPanel", parent);
                LE(statsPanel, preferredHeight: 140);
                var vl = statsPanel.AddComponent<VerticalLayoutGroup>();
                vl.spacing = 4; vl.childControlWidth = true; vl.childControlHeight = false;
                vl.childForceExpandWidth = true; vl.childForceExpandHeight = false;

                BuildCompareStatRow(statsPanel.transform, "CompareStats1", "STRENGTH");
                BuildCompareStatRow(statsPanel.transform, "CompareStats2", "CLUB CONTROL");
                BuildCompareStatRow(statsPanel.transform, "CompareStats3", "RECOVERY");
                BuildCompareStatRow(statsPanel.transform, "CompareStats4", "STAMINA");
            }

            // CompareButtonsPanel (Level Up + Boost)
            {
                var btnPanel = MakeRect("CompareButtonsPanel", parent);
                LE(btnPanel, preferredHeight: 36);
                var hl = btnPanel.AddComponent<HorizontalLayoutGroup>();
                hl.spacing = 6; hl.childControlWidth = true; hl.childControlHeight = true;
                hl.childForceExpandWidth = true; hl.childForceExpandHeight = true;

                MakeButton("CompareLevelUpButton", btnPanel.transform, "LEVEL UP",
                    new Color(0.85f, 0.72f, 0.2f, 1f));
                MakeButton("CompareBoostButton", btnPanel.transform, "BOOST",
                    new Color(0.45f, 0.45f, 0.45f, 1f));
            }

            // CompareBioPanel
            {
                var bioPanel = MakeRect("CompareBioPanel", parent);
                LE(bioPanel, preferredHeight: 60);
                var vl = bioPanel.AddComponent<VerticalLayoutGroup>();
                vl.childControlWidth = true; vl.childControlHeight = false;
                vl.childForceExpandWidth = true; vl.childForceExpandHeight = false;

                MakeTMP("CompareBioText", bioPanel.transform,
                    "Bio text.", 12, TextAlignmentOptions.TopLeft);
            }

            // CompareActionsPanel (Compare + Select/Swap)
            {
                var actions = MakeRect("CompareActionsPanel", parent);
                LE(actions, preferredHeight: 40);
                var hl = actions.AddComponent<HorizontalLayoutGroup>();
                hl.spacing = 6; hl.childControlWidth = true; hl.childControlHeight = true;
                hl.childForceExpandWidth = true; hl.childForceExpandHeight = true;

                MakeButton("CompareRightCompareButton", actions.transform, "COMPARE",
                    new Color(0.45f, 0.45f, 0.45f, 1f));

                // Select button has a named child TMP so the controller can change its text
                var selGO = MakeRect("CompareRightSelectButton", actions.transform);
                selGO.AddComponent<Image>().color = new Color(0.85f, 0.72f, 0.2f, 1f);
                selGO.AddComponent<Button>();
                var selTMP = MakeTMP("Text", selGO.transform, "SWAP", 15, TextAlignmentOptions.Center);
                selTMP.color = Color.black;
                StretchFull(selTMP.GetComponent<RectTransform>());
            }
        }

        /// <summary>
        /// Builds one stat row using the SAME internal structure as CharacterStats1-4 in DetailPanel:
        ///   rowName
        ///   ├── StatIcon   (Image placeholder)
        ///   ├── Name+Bar   (container)
        ///   │   ├── StatsName  (TMP)
        ///   │   └── Bar        (Image, Filled Horizontal Left)
        ///   └── StatNumber (TMP)
        ///
        /// CompareController.UpdateCompareStatRow uses Find("Name+Bar/Bar") and Find("StatNumber"),
        /// so these paths must match exactly.
        /// </summary>
        private static void BuildCompareStatRow(Transform parent, string rowName, string label)
        {
            var row = MakeRect(rowName, parent);
            LE(row, preferredHeight: 28);
            var hl = row.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = 4; hl.childAlignment = TextAnchor.MiddleLeft;
            hl.childControlWidth = false; hl.childControlHeight = true;
            hl.childForceExpandWidth = false; hl.childForceExpandHeight = true;

            // StatIcon
            var icon = MakeRect("StatIcon", row.transform);
            icon.AddComponent<Image>().color = new Color(0.55f, 0.55f, 0.55f, 1f);
            LE(icon, preferredWidth: 22, preferredHeight: 22);

            // Name+Bar
            var nameBar = MakeRect("Name+Bar", row.transform);
            LE(nameBar, preferredWidth: 120);
            var namBarLayout = nameBar.AddComponent<HorizontalLayoutGroup>();
            namBarLayout.spacing = 4; namBarLayout.childControlWidth = true;
            namBarLayout.childControlHeight = true;
            namBarLayout.childForceExpandWidth = true; namBarLayout.childForceExpandHeight = true;

            MakeTMP("StatsName", nameBar.transform, label, 12, TextAlignmentOptions.Left);

            var barGO = MakeRect("Bar", nameBar.transform);
            LE(barGO, preferredWidth: 60, preferredHeight: 10);
            var barImg = barGO.AddComponent<Image>();
            barImg.color      = new Color(0.2f, 0.6f, 1f, 1f);
            barImg.type       = Image.Type.Filled;
            barImg.fillMethod = Image.FillMethod.Horizontal;
            barImg.fillOrigin = 0;
            barImg.fillAmount = 0.5f;

            // StatNumber
            var numTMP = MakeTMP("StatNumber", row.transform, "10/25", 12, TextAlignmentOptions.Right);
            LE(numTMP.gameObject, preferredWidth: 44);
        }

        // ── Primitive helpers ─────────────────────────────────────────────────

        private static GameObject MakeRect(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            return go;
        }

        private static TextMeshProUGUI MakeTMP(string name, Transform parent,
            string text, float fontSize, TextAlignmentOptions align)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text      = text;
            tmp.fontSize  = fontSize;
            tmp.color     = Color.white;
            tmp.alignment = align;
            return tmp;
        }

        private static GameObject MakeHRow(string name, Transform parent, float height)
        {
            var go = MakeRect(name, parent);
            LE(go, preferredHeight: height);
            var hl = go.AddComponent<HorizontalLayoutGroup>();
            hl.childAlignment = TextAnchor.MiddleLeft;
            hl.childControlWidth = true; hl.childControlHeight = true;
            hl.childForceExpandWidth = true; hl.childForceExpandHeight = true;
            return go;
        }

        private static GameObject MakeButton(string name, Transform parent, string label, Color bgColor)
        {
            var go = MakeRect(name, parent);
            go.AddComponent<Image>().color = bgColor;
            go.AddComponent<Button>();
            var tmp = MakeTMP("Text", go.transform, label, 14, TextAlignmentOptions.Center);
            tmp.color = Color.black;
            StretchFull(tmp.GetComponent<RectTransform>());
            return go;
        }

        private static void StretchFull(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;
            rect.anchoredPosition = Vector2.zero;
        }

        private static void LE(GameObject go, float preferredWidth = -1, float preferredHeight = -1)
        {
            var le = go.AddComponent<LayoutElement>();
            if (preferredWidth  >= 0) le.preferredWidth  = preferredWidth;
            if (preferredHeight >= 0) le.preferredHeight = preferredHeight;
        }

        // Overload for Transform to reduce casts
        private static void LE(Transform t, float preferredWidth = -1, float preferredHeight = -1)
            => LE(t.gameObject, preferredWidth, preferredHeight);
    }
}
#endif
