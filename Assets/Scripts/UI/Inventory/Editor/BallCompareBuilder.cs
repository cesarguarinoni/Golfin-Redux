#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Golfin.Inventory.Editor
{
    /// <summary>
    /// Builds the Ball Compare hierarchy inside BallDetailPanel and wires
    /// BallCompareController + BallDetailPanel.compareController.
    ///
    /// Creates:
    ///   BallDetailPanel
    ///     VerticalDivider     (2px line, inactive)
    ///     CompareRightPanel   (full-stretch, inactive, CanvasGroup for fade)
    ///       ComparePlaceholder
    ///         ComparePlaceholderText
    ///       CompareInfoPanel
    ///         CompareNameText
    ///         CompareQuantityText
    ///         [5 stat rows — mirroring left panel structure]
    ///         ButtonsRow
    ///           CloseCompareButton
    ///           CompareRightCompareButton
    ///
    /// Run: GOLFIN/Setup/Ball Compare
    /// </summary>
    public static class BallCompareBuilder
    {
        [MenuItem("GOLFIN/Setup/Ball Compare")]
        public static void Run()
        {
            // ── Find BallDetailPanel ──────────────────────────────────────────
            BallDetailPanel? detailPanel = null;
            foreach (var obj in Resources.FindObjectsOfTypeAll<BallDetailPanel>())
            {
                if (obj.gameObject.scene.isLoaded) { detailPanel = obj; break; }
            }

            if (detailPanel == null)
            {
                EditorUtility.DisplayDialog("Ball Compare Builder",
                    "BallDetailPanel not found in scene. Build BallDetailPanel first.", "OK");
                return;
            }

            var root = detailPanel.transform;

            // ── Guard: already built? ─────────────────────────────────────────
            if (root.Find("CompareRightPanel") != null)
            {
                bool rebuild = EditorUtility.DisplayDialog("Ball Compare Builder",
                    "CompareRightPanel already exists. Rebuild?", "Rebuild", "Cancel");
                if (!rebuild) return;
                DestroyImmediate(root.Find("CompareRightPanel")!.gameObject);
                var existingDiv = root.Find("VerticalDivider");
                if (existingDiv != null) DestroyImmediate(existingDiv.gameObject);
            }

            // ── Find RightPanel to read its font/size for matching style ──────
            var rightPanelT = root.Find("RightPanel");
            var sampleTMP   = rightPanelT != null
                ? rightPanelT.GetComponentInChildren<TextMeshProUGUI>()
                : null;
            TMP_FontAsset? font = sampleTMP != null ? sampleTMP.font : null;

            // ── VerticalDivider ───────────────────────────────────────────────
            var divGO = new GameObject("VerticalDivider", typeof(RectTransform), typeof(Image));
            divGO.transform.SetParent(root, false);
            {
                var rt = divGO.GetComponent<RectTransform>();
                rt.anchorMin        = new Vector2(0.5f, 0.1f);
                rt.anchorMax        = new Vector2(0.5f, 0.9f);
                rt.sizeDelta        = new Vector2(2f, 0f);
                rt.anchoredPosition = Vector2.zero;

                var img = divGO.GetComponent<Image>();
                img.color         = new Color(1f, 1f, 1f, 0.2f);
                img.raycastTarget = false;
            }
            divGO.SetActive(false);

            // ── CompareRightPanel ─────────────────────────────────────────────
            var crpGO = new GameObject("CompareRightPanel", typeof(RectTransform));
            crpGO.transform.SetParent(root, false);
            {
                var rt = crpGO.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.sizeDelta = Vector2.zero;
                rt.anchoredPosition = Vector2.zero;

                var cg = crpGO.AddComponent<CanvasGroup>();
                cg.alpha = 0f;
            }
            crpGO.SetActive(false);
            var crpT = crpGO.transform;

            // ── ComparePlaceholder ────────────────────────────────────────────
            var placeholderGO = MakeStretchChild("ComparePlaceholder", crpT);
            placeholderGO.SetActive(true);
            var placeholderText = MakeTMP("ComparePlaceholderText", placeholderGO.transform, font,
                "Select a ball to compare", 18f, TextAnchor.MiddleCenter);
            placeholderText.alignment = TextAlignmentOptions.Center;
            placeholderText.color     = new Color(1f, 1f, 1f, 0.5f);

            // ── CompareInfoPanel ──────────────────────────────────────────────
            var cipGO = MakeStretchChild("CompareInfoPanel", crpT);
            cipGO.SetActive(false);
            var cipT = cipGO.transform;

            // Add a VerticalLayoutGroup so children stack naturally
            var vlg = cipGO.AddComponent<VerticalLayoutGroup>();
            vlg.childForceExpandWidth  = true;
            vlg.childForceExpandHeight = false;
            vlg.spacing    = 4f;
            vlg.padding    = new RectOffset(8, 8, 12, 12);
            vlg.childAlignment = TextAnchor.UpperCenter;

            // Name
            var compareNameText = MakeTMP("CompareNameText", cipT, font, "BALL NAME", 22f, TextAnchor.MiddleCenter);
            AddLayoutElement(compareNameText.gameObject, preferredHeight: 30f);

            // Quantity
            var compareQuantityText = MakeTMP("CompareQuantityText", cipT, font, "x99", 16f, TextAnchor.MiddleCenter);
            AddLayoutElement(compareQuantityText.gameObject, preferredHeight: 24f);

            // Stat rows
            string[] statNames = { "Power", "Rebound", "WindResistance", "Roll", "Spin" };
            var statRows = new (TextMeshProUGUI name, Image bar, TextMeshProUGUI number, TextMeshProUGUI diff)[5];

            for (int i = 0; i < statNames.Length; i++)
            {
                statRows[i] = BuildStatRow($"{statNames[i]}Row", cipT, font);
            }

            // Buttons row
            var buttonsRowGO = new GameObject("ButtonsRow", typeof(RectTransform));
            buttonsRowGO.transform.SetParent(cipT, false);
            {
                var hlg = buttonsRowGO.AddComponent<HorizontalLayoutGroup>();
                hlg.spacing              = 8f;
                hlg.childForceExpandWidth  = true;
                hlg.childForceExpandHeight = false;
                hlg.padding = new RectOffset(0, 0, 4, 0);
                AddLayoutElement(buttonsRowGO, preferredHeight: 36f);
            }

            var closeBtn = MakeButton("CloseCompareButton", buttonsRowGO.transform, font, "CLOSE");
            var compareRightBtn = MakeButton("CompareRightCompareButton", buttonsRowGO.transform, font, "COMPARE");

            // ── Add BallCompareController ─────────────────────────────────────
            var ctrl = detailPanel.GetComponent<BallCompareController>();
            if (ctrl == null) ctrl = detailPanel.gameObject.AddComponent<BallCompareController>();

            // ── Wire BallCompareController ────────────────────────────────────
            var so = new SerializedObject(ctrl);

            // Layout panels
            WireGO(so, "leftPanel",     root.Find("LeftPanel")?.gameObject);
            WireRT(so, "rightPanel",    root.Find("RightPanel")?.GetComponent<RectTransform>());
            WireGO(so, "compareRightPanel",  crpGO);
            WireGO(so, "comparePlaceholder", placeholderGO);
            WireTMP(so, "comparePlaceholderText", placeholderText);
            WireGO(so, "compareInfoPanel",   cipGO);
            WireGO(so, "verticalDivider",    divGO);

            // Buttons — find compareButton on RightPanel
            var compareBtnObj = rightPanelT?.Find("CompareButton")?.GetComponent<Button>();
            WireObj(so, "compareButton",      compareBtnObj);
            WireObj(so, "closeCompareButton", closeBtn);

            // Right column info
            WireTMP(so, "compareNameText",     compareNameText);
            WireTMP(so, "compareQuantityText", compareQuantityText);

            // Stat rows
            WireStatRow(so, "Power",         statRows[0]);
            WireStatRow(so, "Rebound",       statRows[1]);
            WireStatRow(so, "WindResistance",statRows[2]);
            WireStatRow(so, "Roll",          statRows[3]);
            WireStatRow(so, "Spin",          statRows[4]);

            // Right column buttons
            WireObj(so, "compareRightCompareButton", compareRightBtn);

            // Carousel — reuse the one already wired on BallDetailPanel
            var carouselObj = detailPanel.GetComponent<BallCarouselController>();
            if (carouselObj == null)
                carouselObj = Object.FindObjectOfType<BallCarouselController>(true);
            WireObj(so, "carousel", carouselObj);

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(ctrl);

            // ── Wire BallDetailPanel.compareController ────────────────────────
            var dpSO = new SerializedObject(detailPanel);
            dpSO.FindProperty("compareController").objectReferenceValue = ctrl;
            dpSO.ApplyModifiedProperties();
            EditorUtility.SetDirty(detailPanel);

            EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());

            Debug.Log("[BallCompareBuilder] Done.");
            EditorUtility.DisplayDialog("Ball Compare Builder",
                "Compare hierarchy built and wired!\n\n" +
                "Hit Play → switch to BALLS tab → tap COMPARE to test.", "OK");
        }

        // ── Factory Helpers ───────────────────────────────────────────────────

        private static (TextMeshProUGUI name, Image bar, TextMeshProUGUI number, TextMeshProUGUI diff)
            BuildStatRow(string goName, Transform parent, TMP_FontAsset? font)
        {
            var rowGO = new GameObject(goName, typeof(RectTransform));
            rowGO.transform.SetParent(parent, false);
            var hlg = rowGO.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing              = 4f;
            hlg.childForceExpandWidth  = false;
            hlg.childForceExpandHeight = true;
            AddLayoutElement(rowGO, preferredHeight: 28f);

            var nameT  = MakeTMP("StatName", rowGO.transform, font, "STAT", 12f, TextAnchor.MiddleLeft);
            AddLayoutElement(nameT.gameObject, preferredWidth: 70f);

            var barGO = new GameObject("Bar", typeof(RectTransform), typeof(Image));
            barGO.transform.SetParent(rowGO.transform, false);
            var barLE = barGO.AddComponent<LayoutElement>();
            barLE.flexibleWidth = 1f;
            barLE.preferredHeight = 12f;
            var barImg = barGO.GetComponent<Image>();
            barImg.color = new Color(0.25f, 0.25f, 0.3f, 0.5f);
            barImg.raycastTarget = false;
            barImg.type = Image.Type.Filled;
            barImg.fillMethod = Image.FillMethod.Horizontal;
            barImg.fillOrigin = 0;

            var numberT = MakeTMP("StatNumber", rowGO.transform, font, "+0", 12f, TextAnchor.MiddleRight);
            AddLayoutElement(numberT.gameObject, preferredWidth: 28f);

            var diffT = MakeTMP("DiffLabel", rowGO.transform, font, "", 11f, TextAnchor.MiddleRight);
            AddLayoutElement(diffT.gameObject, preferredWidth: 28f);
            diffT.color = new Color(0.2f, 0.8f, 0.3f, 1f);
            diffT.gameObject.SetActive(false);

            return (nameT, barImg, numberT, diffT);
        }

        private static Button MakeButton(string goName, Transform parent, TMP_FontAsset? font, string label)
        {
            var go  = new GameObject(goName, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = new Color(0.2f, 0.2f, 0.25f, 1f);
            var txt = MakeTMP("Text", go.transform, font, label, 12f, TextAnchor.MiddleCenter);
            txt.alignment = TextAlignmentOptions.Center;
            return go.GetComponent<Button>();
        }

        private static TextMeshProUGUI MakeTMP(string goName, Transform parent, TMP_FontAsset? font,
            string text, float size, TextAnchor anchor)
        {
            var go  = new GameObject(goName, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.text      = text;
            tmp.fontSize  = size;
            tmp.color     = Color.white;
            tmp.raycastTarget = false;
            if (font != null) tmp.font = font;

            // TextAnchor → TMP alignment
            tmp.alignment = anchor switch
            {
                TextAnchor.MiddleLeft   => TextAlignmentOptions.MidlineLeft,
                TextAnchor.MiddleRight  => TextAlignmentOptions.MidlineRight,
                TextAnchor.MiddleCenter => TextAlignmentOptions.Center,
                TextAnchor.UpperCenter  => TextAlignmentOptions.Top,
                _ => TextAlignmentOptions.Left
            };

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;

            return tmp;
        }

        private static GameObject MakeStretchChild(string goName, Transform parent)
        {
            var go = new GameObject(goName, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin        = Vector2.zero;
            rt.anchorMax        = Vector2.one;
            rt.sizeDelta        = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;
            return go;
        }

        private static void AddLayoutElement(GameObject go, float preferredWidth = -1f, float preferredHeight = -1f)
        {
            var le = go.GetComponent<LayoutElement>();
            if (le == null) le = go.AddComponent<LayoutElement>();
            if (preferredWidth  >= 0f) le.preferredWidth  = preferredWidth;
            if (preferredHeight >= 0f) le.preferredHeight = preferredHeight;
        }

        // ── Wire Helpers ──────────────────────────────────────────────────────

        private static void WireStatRow(SerializedObject so, string statName,
            (TextMeshProUGUI name, Image bar, TextMeshProUGUI number, TextMeshProUGUI diff) row)
        {
            string prefix = $"compare{statName}";
            WireTMP(so, $"{prefix}Name",   row.name);
            WireObj(so, $"{prefix}Bar",    row.bar);
            WireTMP(so, $"{prefix}Number", row.number);
            WireTMP(so, $"{prefix}Diff",   row.diff);
        }

        private static void WireGO(SerializedObject so, string prop, GameObject? obj)
        {
            var p = so.FindProperty(prop);
            if (p != null) p.objectReferenceValue = obj;
            else Debug.LogWarning($"[BallCompareBuilder] Property '{prop}' not found.");
        }

        private static void WireRT(SerializedObject so, string prop, RectTransform? rt)
        {
            var p = so.FindProperty(prop);
            if (p != null) p.objectReferenceValue = rt;
            else Debug.LogWarning($"[BallCompareBuilder] Property '{prop}' not found.");
        }

        private static void WireTMP(SerializedObject so, string prop, TextMeshProUGUI? tmp)
        {
            var p = so.FindProperty(prop);
            if (p != null) p.objectReferenceValue = tmp;
            else Debug.LogWarning($"[BallCompareBuilder] Property '{prop}' not found.");
        }

        private static void WireObj(SerializedObject so, string prop, Object? obj)
        {
            var p = so.FindProperty(prop);
            if (p != null) p.objectReferenceValue = obj;
            else Debug.LogWarning($"[BallCompareBuilder] Property '{prop}' not found.");
        }
    }
}
#endif
