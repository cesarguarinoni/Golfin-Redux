#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Golfin.Inventory.Editor
{
    /// <summary>
    /// Adds missing GameObjects to RightPanel inside ItemDetailPanel:
    ///   RestoresHeader   — TMP label ("RESTORES")
    ///   EffectIcon       — Image placeholder (checkmark / durability icon)
    ///   EffectText       — TMP label ("DURABILITY 50%")
    ///   ProTipHeader     — TMP label ("*PRO TIP")
    ///   ProTipText       — TMP label (pro tip body)
    ///   UseButton        — Button + child TMP label ("USE")
    ///
    /// Safe to re-run — skips any child that already exists by name.
    ///
    /// Run: GOLFIN/Build/Item Right Panel
    /// Then run: GOLFIN/Wire/Item Detail Panel
    /// </summary>
    public static class ItemRightPanelBuilder
    {
        [MenuItem("GOLFIN/Build/Item Right Panel")]
        public static void Run()
        {
            // ── Find ItemDetailPanel ──────────────────────────────────────────
            ItemDetailPanel? panel = null;
            foreach (var p in Resources.FindObjectsOfTypeAll<ItemDetailPanel>())
                if (p.gameObject.scene.isLoaded) { panel = p; break; }

            if (panel == null)
            {
                EditorUtility.DisplayDialog("Item Right Panel Builder",
                    "ItemDetailPanel not found in scene.\n\nRun GOLFIN/Build/Items Content first.", "OK");
                return;
            }

            var rightPanel = panel.transform.Find("RightPanel");
            if (rightPanel == null)
            {
                EditorUtility.DisplayDialog("Item Right Panel Builder",
                    "RightPanel not found under ItemDetailPanel.", "OK");
                return;
            }

            // ── Clone font/style from an existing TMP sibling ─────────────────
            // Use ItemNameText as the style source, falling back to null (plain TMP)
            var styleSource = rightPanel.Find("ItemNameText")?.GetComponent<TextMeshProUGUI>();

            int added = 0;

            // ── Add missing children ───────────────────────────────────────────

            // RestoresHeader
            if (rightPanel.Find("RestoresHeader") == null)
            {
                CreateTMP(rightPanel, "RestoresHeader", "RESTORES", styleSource, fontSize: 18f, bold: true);
                added++;
            }

            // EffectIcon  (Image — user assigns sprite in Inspector)
            if (rightPanel.Find("EffectIcon") == null)
            {
                CreateImage(rightPanel, "EffectIcon", new Vector2(24f, 24f));
                added++;
            }

            // EffectText
            if (rightPanel.Find("EffectText") == null)
            {
                CreateTMP(rightPanel, "EffectText", "DURABILITY 50%", styleSource, fontSize: 16f, bold: false);
                added++;
            }

            // ProTipHeader
            if (rightPanel.Find("ProTipHeader") == null)
            {
                CreateTMP(rightPanel, "ProTipHeader", "*PRO TIP", styleSource, fontSize: 18f, bold: true);
                added++;
            }

            // ProTipText
            if (rightPanel.Find("ProTipText") == null)
            {
                CreateTMP(rightPanel, "ProTipText", "Pro tip placeholder...", styleSource, fontSize: 14f, bold: false);
                added++;
            }

            // UseButton
            if (rightPanel.Find("UseButton") == null)
            {
                CreateButton(rightPanel, "UseButton", "USE", styleSource);
                added++;
            }

            EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());

            string msg = added > 0
                ? $"{added} object(s) added to RightPanel.\n\nNext: GOLFIN/Wire/Item Detail Panel"
                : "All objects already exist — nothing added.";

            Debug.Log($"[ItemRightPanelBuilder] Done — {added} added.");
            EditorUtility.DisplayDialog("Item Right Panel Builder", msg, "OK");
        }

        // ── Factory helpers ───────────────────────────────────────────────────

        private static TextMeshProUGUI CreateTMP(Transform parent, string goName, string defaultText,
            TextMeshProUGUI? styleSource, float fontSize, bool bold)
        {
            var go = new GameObject(goName, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin        = new Vector2(0f, 1f);
            rt.anchorMax        = new Vector2(1f, 1f);
            rt.pivot            = new Vector2(0.5f, 1f);
            rt.sizeDelta        = new Vector2(0f, 30f);
            rt.anchoredPosition = Vector2.zero;

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text      = defaultText;
            tmp.fontSize  = fontSize;
            tmp.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            tmp.color     = Color.white;

            if (styleSource != null && styleSource.font != null)
                tmp.font = styleSource.font;

            Debug.Log($"[ItemRightPanelBuilder] ✓ Created TMP: {goName}");
            return tmp;
        }

        private static Image CreateImage(Transform parent, string goName, Vector2 size)
        {
            var go = new GameObject(goName, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin        = new Vector2(0f, 1f);
            rt.anchorMax        = new Vector2(0f, 1f);
            rt.pivot            = new Vector2(0f, 1f);
            rt.sizeDelta        = size;
            rt.anchoredPosition = Vector2.zero;

            var img = go.AddComponent<Image>();
            img.color = Color.white;

            Debug.Log($"[ItemRightPanelBuilder] ✓ Created Image: {goName}");
            return img;
        }

        private static Button CreateButton(Transform parent, string goName, string label,
            TextMeshProUGUI? styleSource)
        {
            // Button root
            var go = new GameObject(goName, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin        = new Vector2(0f, 1f);
            rt.anchorMax        = new Vector2(1f, 1f);
            rt.pivot            = new Vector2(0.5f, 1f);
            rt.sizeDelta        = new Vector2(0f, 48f);
            rt.anchoredPosition = Vector2.zero;

            var img = go.AddComponent<Image>();
            img.color = new Color(1f, 0.8f, 0.2f, 1f);   // gold

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;

            // Label child
            var labelGO = new GameObject("Text", typeof(RectTransform));
            labelGO.transform.SetParent(go.transform, false);

            var labelRT = labelGO.GetComponent<RectTransform>();
            labelRT.anchorMin  = Vector2.zero;
            labelRT.anchorMax  = Vector2.one;
            labelRT.sizeDelta  = Vector2.zero;
            labelRT.offsetMin  = Vector2.zero;
            labelRT.offsetMax  = Vector2.zero;

            var tmp = labelGO.AddComponent<TextMeshProUGUI>();
            tmp.text      = label;
            tmp.fontSize  = 18f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color     = Color.black;
            tmp.alignment = TextAlignmentOptions.Center;

            if (styleSource != null && styleSource.font != null)
                tmp.font = styleSource.font;

            Debug.Log($"[ItemRightPanelBuilder] ✓ Created Button: {goName}");
            return btn;
        }
    }
}
#endif
