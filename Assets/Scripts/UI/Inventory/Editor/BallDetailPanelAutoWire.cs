#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Golfin.Inventory.Editor
{
    /// <summary>
    /// Auto-wires BallDetailPanel fields.
    /// Run AFTER: GOLFIN/Setup/Build Ball Detail Panel
    ///
    /// Paths match the hierarchy produced by BallDetailPanelBuilder:
    ///   LeftPanel/BallImage
    ///   LeftPanel/InfoSection/InfoHeader|InfoText
    ///   RightPanel/BallNameText
    ///   RightPanel/OwnedPanel/OwnedLabel|QuantityText
    ///   RightPanel/StatsPanel/{Row}/Name+Bar/StatsName  (stat label)
    ///   RightPanel/StatsPanel/{Row}/Name+Bar/BarContainer/Bar  (bar image)
    ///   RightPanel/StatsPanel/{Row}/StatNumber
    ///   RightPanel/CompareButton
    ///
    /// Run: GOLFIN/Wire/Ball Detail Panel
    /// </summary>
    public static class BallDetailPanelAutoWire
    {
        [MenuItem("GOLFIN/Wire/Ball Detail Panel")]
        public static void Run()
        {
            // Find BallDetailPanel (search inactive objects)
            BallDetailPanel? panel = null;
            foreach (var p in Resources.FindObjectsOfTypeAll<BallDetailPanel>())
            {
                if (p.gameObject.scene.isLoaded) { panel = p; break; }
            }

            if (panel == null)
            {
                EditorUtility.DisplayDialog("Auto-Wire Failed",
                    "BallDetailPanel not found in scene.\n\n" +
                    "Run GOLFIN/Setup/Build Ball Detail Panel first.", "OK");
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
                Debug.Log($"[BallDetailPanelAutoWire] ✓ {prop}");
                wired++; return 1;
            }

            int WireImage(string prop, string path)
            {
                var t = root.Find(path);
                if (t == null) { LogFail(prop, path, "path not found"); failed++; return 0; }
                var cmp = t.GetComponent<Image>();
                if (cmp == null) { LogFail(prop, path, "no Image"); failed++; return 0; }
                so.FindProperty(prop).objectReferenceValue = cmp;
                Debug.Log($"[BallDetailPanelAutoWire] ✓ {prop}");
                wired++; return 1;
            }

            int WireButton(string prop, string path)
            {
                var t = root.Find(path);
                if (t == null) { LogFail(prop, path, "path not found"); failed++; return 0; }
                var cmp = t.GetComponent<Button>();
                if (cmp == null) { LogFail(prop, path, "no Button"); failed++; return 0; }
                so.FindProperty(prop).objectReferenceValue = cmp;
                Debug.Log($"[BallDetailPanelAutoWire] ✓ {prop}");
                wired++; return 1;
            }

            int WireStatRow(string nameP, string barP, string numP, string rowPath)
            {
                int n = 0;
                n += WireTMP  (nameP, $"{rowPath}/Name+Bar/StatsName");
                n += WireImage(barP,  $"{rowPath}/Name+Bar/BarContainer/Bar");
                n += WireTMP  (numP,  $"{rowPath}/StatNumber");
                return n;
            }

            // ── Left Panel ────────────────────────────────────────────────────
            WireImage("ballImage",  "LeftPanel/BallImage");
            WireTMP  ("infoHeader", "LeftPanel/InfoSection/InfoHeader");
            WireTMP  ("infoText",   "LeftPanel/InfoSection/InfoText");

            // ── Right Panel — Name & Quantity ─────────────────────────────────
            WireTMP("ballNameText", "RightPanel/BallNameText");
            WireTMP("ownedLabel",   "RightPanel/OwnedPanel/OwnedLabel");
            WireTMP("quantityText", "RightPanel/OwnedPanel/QuantityText");

            // ── Stat Rows ─────────────────────────────────────────────────────
            const string SP = "RightPanel/StatsPanel";
            WireStatRow("powerName",         "powerBar",         "powerNumber",
                        $"{SP}/PowerRow");
            WireStatRow("reboundName",       "reboundBar",       "reboundNumber",
                        $"{SP}/ReboundRow");
            WireStatRow("windResistanceName","windResistanceBar","windResistanceNumber",
                        $"{SP}/WindResistanceRow");
            WireStatRow("rollName",          "rollBar",          "rollNumber",
                        $"{SP}/RollRow");
            WireStatRow("spinName",          "spinBar",          "spinNumber",
                        $"{SP}/SpinRow");

            // ── Buttons ───────────────────────────────────────────────────────
            WireButton("compareButton", "RightPanel/CompareButton");

            // ── Carousel (sibling in scene) ───────────────────────────────────
            BallCarouselController? carousel = null;
            foreach (var c in Resources.FindObjectsOfTypeAll<BallCarouselController>())
                if (c.gameObject.scene.isLoaded) { carousel = c; break; }

            if (carousel != null)
            {
                so.FindProperty("carousel").objectReferenceValue = carousel;
                Debug.Log("[BallDetailPanelAutoWire] ✓ carousel");
                wired++;
            }
            else
            {
                Debug.LogWarning("[BallDetailPanelAutoWire] BallCarouselController not found — wire 'carousel' manually.");
                failed++;
            }

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(panel);
            EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());

            string msg = $"{wired} fields wired, {failed} failed.\n\n" +
                (failed > 0
                    ? "Check Console for missing paths.\nIf paths differ, re-run GOLFIN/Setup/Build Ball Detail Panel first."
                    : "All fields wired successfully!");

            Debug.Log($"[BallDetailPanelAutoWire] Done — {wired} wired, {failed} failed.");
            EditorUtility.DisplayDialog("Ball Detail Panel Auto-Wire", msg, "OK");
        }

        private static void LogFail(string prop, string path, string reason)
            => Debug.LogWarning($"[BallDetailPanelAutoWire] NOT FOUND '{prop}' at '{path}' — {reason}. Wire manually.");
    }
}
#endif
