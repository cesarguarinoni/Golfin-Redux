#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Golfin.Roster.Editor
{
    /// <summary>
    /// Adds the Compare Mode UI nodes to the existing DetailPanel hierarchy.
    /// DOES NOT touch LeftPanel, RightPanel content, or any existing children.
    ///
    /// Strategy for CompareRightPanel:
    ///   Clones the existing RightPanel so fonts, colors, sizes and positions
    ///   are an exact copy of whatever the user has already tuned.
    ///   Wraps all cloned children in a CompareInfoPanel, then adds a
    ///   ComparePlaceholder overlay on top.
    ///
    /// Nodes created / modified:
    ///   DetailPanel
    ///   ├── RightPanel/ButtonsPanel
    ///   │   ├── CloseCompareButton   (NEW — hidden by default)
    ///   │   └── SwapButton           (NEW — hidden by default)
    ///   ├── VerticalDivider          (NEW — hidden by default)
    ///   └── CompareRightPanel        (NEW — cloned from RightPanel, hidden by default)
    ///       ├── ComparePlaceholder   (overlay — shown until a character is selected)
    ///       └── CompareInfoPanel     (all cloned RightPanel children — hidden until selection)
    ///           ├── CharacterNamePanel / CharacterNameText
    ///           ├── RarityPanel / RarityRow / RarityLabel + LevelText + LevelTextMax
    ///           ├── CharacterStatsPanel / CharacterStats1-4
    ///           ├── ButtonsPanel / LevelUpButton + BoostButton
    ///           ├── BioPanel / BioText
    ///           ├── CompareButton     ← wired as compareRightCompareButton
    ///           └── SelectButton      ← wired as compareRightSelectButton
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

            var rightPanel = detailPanel.Find("RightPanel");
            if (rightPanel == null)
            {
                EditorUtility.DisplayDialog("Error",
                    "RightPanel not found inside DetailPanel.",
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

            // ── 3. CompareRightPanel — cloned from RightPanel ───────────────────
            BuildCompareRightPanel(detailPanel, rightPanel);

            // ── Finalise ────────────────────────────────────────────────────────
            EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());

            Selection.activeGameObject = detailPanel.gameObject;

            EditorUtility.DisplayDialog("Done!",
                "Compare Mode hierarchy added to DetailPanel.\n\n" +
                "CompareRightPanel was cloned from RightPanel — all visual settings are preserved.\n\n" +
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
            var buttonsPanel = detailPanel.Find("RightPanel/ButtonsPanel");
            if (buttonsPanel == null)
            {
                Debug.LogWarning("[CompareBuilder] RightPanel/ButtonsPanel not found. " +
                    "CloseCompareButton and SwapButton will be added to DetailPanel root.");
                buttonsPanel = detailPanel;
            }

            if (buttonsPanel.Find("CloseCompareButton") == null)
            {
                var btn = MakeButton("CloseCompareButton", buttonsPanel, "CLOSE COMPARE",
                    new Color(0.45f, 0.45f, 0.45f, 1f));
                btn.SetActive(false);
                Debug.Log("[CompareBuilder] ✓ Created CloseCompareButton");
            }

            if (buttonsPanel.Find("SwapButton") == null)
            {
                var btn = MakeButton("SwapButton", buttonsPanel, "SWAP",
                    new Color(0.85f, 0.72f, 0.2f, 1f));
                btn.SetActive(false);
                Debug.Log("[CompareBuilder] ✓ Created SwapButton");
            }
        }

        // ── VerticalDivider ───────────────────────────────────────────────────

        private static void BuildVerticalDivider(Transform parent)
        {
            // Remove existing if present (idempotent)
            var existing = parent.Find("VerticalDivider");
            if (existing != null) Object.DestroyImmediate(existing.gameObject);

            var go = new GameObject("VerticalDivider");
            go.transform.SetParent(parent, false);

            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot     = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(2f, 0f);
            rect.anchoredPosition = Vector2.zero;

            go.AddComponent<Image>().color = new Color(0.35f, 0.35f, 0.35f, 1f);
            go.SetActive(false);

            Debug.Log("[CompareBuilder] ✓ Created VerticalDivider");
        }

        // ── CompareRightPanel — clone of RightPanel ───────────────────────────

        private static void BuildCompareRightPanel(Transform detailPanel, Transform rightPanel)
        {
            // ── Clone RightPanel ──────────────────────────────────────────────
            var clone = Object.Instantiate(rightPanel.gameObject, detailPanel, false);
            clone.name = "CompareRightPanel";

            // Override anchor/size so it occupies the right half of the detail panel
            var rt = clone.GetComponent<RectTransform>();
            rt.anchorMin        = new Vector2(0.5f, 0f);
            rt.anchorMax        = new Vector2(1f,   1f);
            rt.sizeDelta        = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;
            rt.pivot            = new Vector2(0.5f, 0.5f);

            // ── Collect existing cloned children BEFORE adding anything new ──
            var childList = new List<Transform>();
            for (int i = 0; i < clone.transform.childCount; i++)
                childList.Add(clone.transform.GetChild(i));

            // ── Create CompareInfoPanel wrapper (full-stretch) ────────────────
            var infoPanel = new GameObject("CompareInfoPanel");
            infoPanel.transform.SetParent(clone.transform, false);
            var infoRT = infoPanel.AddComponent<RectTransform>();
            infoRT.anchorMin        = Vector2.zero;
            infoRT.anchorMax        = Vector2.one;
            infoRT.sizeDelta        = Vector2.zero;
            infoRT.anchoredPosition = Vector2.zero;

            // Move all original cloned children into CompareInfoPanel
            // (worldPositionStays=false preserves local RectTransform values)
            foreach (var child in childList)
                child.SetParent(infoPanel.transform, false);

            // ── Remove left-column compare buttons from the cloned ButtonsPanel ──
            // (CloseCompareButton / SwapButton belong to the LEFT column only)
            var clonedBP = infoPanel.transform.Find("ButtonsPanel");
            if (clonedBP != null)
            {
                var closeBtn = clonedBP.Find("CloseCompareButton");
                if (closeBtn != null) Object.DestroyImmediate(closeBtn.gameObject);

                var swapBtn = clonedBP.Find("SwapButton");
                if (swapBtn != null) Object.DestroyImmediate(swapBtn.gameObject);
            }

            // ── ComparePlaceholder — full-stretch overlay ─────────────────────
            var placeholder = new GameObject("ComparePlaceholder");
            placeholder.transform.SetParent(clone.transform, false);
            var phRT = placeholder.AddComponent<RectTransform>();
            phRT.anchorMin        = Vector2.zero;
            phRT.anchorMax        = Vector2.one;
            phRT.sizeDelta        = Vector2.zero;
            phRT.anchoredPosition = Vector2.zero;

            // Semi-opaque dark background so it covers CompareInfoPanel
            var phBG = placeholder.AddComponent<Image>();
            phBG.color = new Color(0.08f, 0.08f, 0.08f, 0.93f);

            // Centred instruction text
            var textGO = new GameObject("PlaceholderText");
            textGO.transform.SetParent(placeholder.transform, false);
            var textRT = textGO.AddComponent<RectTransform>();
            textRT.anchorMin        = Vector2.zero;
            textRT.anchorMax        = Vector2.one;
            textRT.sizeDelta        = Vector2.zero;
            textRT.anchoredPosition = Vector2.zero;
            var tmp = textGO.AddComponent<TextMeshProUGUI>();
            tmp.text      = "TAP ANY CHARACTER\nTO COMPARE STATS";
            tmp.fontSize  = 18;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color     = new Color(0.65f, 0.65f, 0.65f, 1f);

            // ── Initial visibility ────────────────────────────────────────────
            placeholder.SetActive(true);   // shown until a character is picked
            infoPanel.SetActive(false);    // hidden until a character is picked
            clone.SetActive(false);        // hidden until compare mode starts

            Debug.Log("[CompareBuilder] ✓ Created CompareRightPanel (cloned from RightPanel)");
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
            var go = MakeRect(name, parent);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text      = text;
            tmp.fontSize  = fontSize;
            tmp.color     = Color.white;
            tmp.alignment = align;
            return tmp;
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
            rect.anchorMin        = Vector2.zero;
            rect.anchorMax        = Vector2.one;
            rect.sizeDelta        = Vector2.zero;
            rect.anchoredPosition = Vector2.zero;
        }
    }
}
#endif
