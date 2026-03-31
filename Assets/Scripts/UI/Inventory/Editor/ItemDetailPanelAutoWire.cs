#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Golfin.Inventory.Editor
{
    /// <summary>
    /// Auto-wires ItemDetailPanel + ItemCarouselController fields.
    ///
    /// Expected hierarchy under ItemDetailPanel:
    ///   LeftPanel/ItemImage
    ///   LeftPanel/BrandText
    ///   RightPanel/ItemNameText
    ///   RightPanel/RarityText
    ///   RightPanel/QuantityText
    ///   RightPanel/RestoresHeader
    ///   RightPanel/EffectText
    ///   RightPanel/ProTipHeader
    ///   RightPanel/ProTipText
    ///   RightPanel/CompareButton
    ///   RightPanel/UseButton
    ///   BottomPanel/InfoHeader  (or RightPanel/InfoHeader if no BottomPanel)
    ///   BottomPanel/InfoText    (or RightPanel/InfoText if no BottomPanel)
    ///
    /// Run: GOLFIN/Wire/Item Detail Panel
    /// </summary>
    public static class ItemDetailPanelAutoWire
    {
        [MenuItem("GOLFIN/Wire/Item Detail Panel")]
        public static void Run()
        {
            // Find ItemDetailPanel (search inactive objects)
            ItemDetailPanel? panel = null;
            foreach (var p in Resources.FindObjectsOfTypeAll<ItemDetailPanel>())
            {
                if (p.gameObject.scene.isLoaded) { panel = p; break; }
            }

            if (panel == null)
            {
                EditorUtility.DisplayDialog("Auto-Wire Failed",
                    "ItemDetailPanel not found in scene.\n\n" +
                    "Add an ItemDetailPanel component to the scene hierarchy first.", "OK");
                return;
            }

            var so     = new SerializedObject(panel);
            var root   = panel.transform;
            int wired  = 0;
            int failed = 0;

            // ── Helpers ───────────────────────────────────────────────────────

            int WireTMP(string prop, string path)
            {
                var t = root.Find(path);
                if (t == null) { LogFail(prop, path, "path not found"); failed++; return 0; }
                var cmp = t.GetComponent<TextMeshProUGUI>();
                if (cmp == null) { LogFail(prop, path, "no TMP"); failed++; return 0; }
                so.FindProperty(prop).objectReferenceValue = cmp;
                Debug.Log($"[ItemDetailPanelAutoWire] ✓ {prop}");
                wired++; return 1;
            }

            int WireImage(string prop, string path)
            {
                var t = root.Find(path);
                if (t == null) { LogFail(prop, path, "path not found"); failed++; return 0; }
                var cmp = t.GetComponent<Image>();
                if (cmp == null) { LogFail(prop, path, "no Image"); failed++; return 0; }
                so.FindProperty(prop).objectReferenceValue = cmp;
                Debug.Log($"[ItemDetailPanelAutoWire] ✓ {prop}");
                wired++; return 1;
            }

            int WireButton(string prop, string path)
            {
                var t = root.Find(path);
                if (t == null) { LogFail(prop, path, "path not found"); failed++; return 0; }
                var cmp = t.GetComponent<Button>();
                if (cmp == null) { LogFail(prop, path, "no Button"); failed++; return 0; }
                so.FindProperty(prop).objectReferenceValue = cmp;
                Debug.Log($"[ItemDetailPanelAutoWire] ✓ {prop}");
                wired++; return 1;
            }

            // ── Left Panel ────────────────────────────────────────────────────
            WireImage("itemImage", "LeftPanel/ItemImage");
            WireTMP  ("brandText", "LeftPanel/BrandText");

            // ── Right Panel ───────────────────────────────────────────────────
            WireTMP   ("itemNameText",   "RightPanel/ItemNameText");
            WireTMP   ("rarityText",     "RightPanel/RarityText");
            WireTMP   ("quantityText",   "RightPanel/QuantityText");
            WireTMP   ("restoresHeader", "RightPanel/RestoresHeader");
            WireTMP   ("effectText",     "RightPanel/EffectText");
            WireTMP   ("proTipHeader",   "RightPanel/ProTipHeader");
            WireTMP   ("proTipText",     "RightPanel/ProTipText");
            WireButton("compareButton",  "RightPanel/CompareButton");

            // ── Bottom Panel (falls back to RightPanel if no BottomPanel) ────────
            bool hasBottomPanel = root.Find("BottomPanel") != null;
            string bottom = hasBottomPanel ? "BottomPanel" : "RightPanel";
            WireTMP   ("infoHeader", $"{bottom}/InfoHeader");
            WireTMP   ("infoText",   $"{bottom}/InfoText");
            WireButton("useButton",  "RightPanel/UseButton");

            // ── Carousel (sibling in scene) ───────────────────────────────────
            ItemCarouselController? carousel = null;
            foreach (var c in Resources.FindObjectsOfTypeAll<ItemCarouselController>())
                if (c.gameObject.scene.isLoaded) { carousel = c; break; }

            if (carousel != null)
            {
                so.FindProperty("carousel").objectReferenceValue = carousel;
                Debug.Log("[ItemDetailPanelAutoWire] ✓ carousel");
                wired++;
            }
            else
            {
                Debug.LogWarning("[ItemDetailPanelAutoWire] ItemCarouselController not found — wire 'carousel' manually.");
                failed++;
            }

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(panel);
            EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());

            string msg = $"{wired} fields wired, {failed} failed.\n\n" +
                (failed > 0
                    ? "Check Console for missing paths.\nEnsure the hierarchy matches the expected paths."
                    : "All fields wired successfully!");

            Debug.Log($"[ItemDetailPanelAutoWire] Done — {wired} wired, {failed} failed.");
            EditorUtility.DisplayDialog("Item Detail Panel Auto-Wire", msg, "OK");
        }

        private static void LogFail(string prop, string path, string reason)
            => Debug.LogWarning($"[ItemDetailPanelAutoWire] NOT FOUND '{prop}' at '{path}' — {reason}. Wire manually.");
    }
}
#endif
