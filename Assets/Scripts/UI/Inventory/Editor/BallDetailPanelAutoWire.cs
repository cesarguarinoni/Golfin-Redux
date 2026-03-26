#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Golfin.Inventory.Editor
{
    /// <summary>
    /// Auto-wires BallDetailPanel fields by searching the open scene hierarchy.
    ///
    /// Run: GOLFIN/Wire/Ball Detail Panel
    ///
    /// NOTE: Adjust Transform paths below to match the actual scene hierarchy
    /// after the BallsContent panel is built in Unity.
    /// </summary>
    public static class BallDetailPanelAutoWire
    {
        [MenuItem("GOLFIN/Wire/Ball Detail Panel")]
        public static void Run()
        {
            // Find the BallDetailPanel component (search inactive objects too)
            var allObjects = Resources.FindObjectsOfTypeAll<BallDetailPanel>();
            BallDetailPanel? panel = null;
            foreach (var p in allObjects)
            {
                if (p.gameObject.scene.isLoaded) { panel = p; break; }
            }

            if (panel == null)
            {
                Debug.LogError("[BallDetailPanelAutoWire] No BallDetailPanel found in scene. " +
                               "Add BallDetailPanel component to the detail panel GameObject first.");
                EditorUtility.DisplayDialog("Auto-Wire Failed",
                    "BallDetailPanel component not found in scene.\n\n" +
                    "Add BallDetailPanel to the detail panel GameObject, then re-run.",
                    "OK");
                return;
            }

            var so       = new SerializedObject(panel);
            var root     = panel.transform;
            int wired    = 0;
            int failed   = 0;

            // ── Helper closures ───────────────────────────────────────────────

            void Wire(string propName, Object? obj)
            {
                if (obj == null) { Debug.LogWarning($"[BallDetailPanelAutoWire] NOT FOUND: {propName}"); failed++; return; }
                var prop = so.FindProperty(propName);
                if (prop == null) { Debug.LogWarning($"[BallDetailPanelAutoWire] No property: {propName}"); failed++; return; }
                prop.objectReferenceValue = obj;
                Debug.Log($"[BallDetailPanelAutoWire] ✓ {propName}");
                wired++;
            }

            TMP_Text? FindTMP(string path)
            {
                var t = root.Find(path);
                return t != null ? t.GetComponent<TMP_Text>() : null;
            }

            Image? FindImage(string path)
            {
                var t = root.Find(path);
                return t != null ? t.GetComponent<Image>() : null;
            }

            Button? FindButton(string path)
            {
                var t = root.Find(path);
                return t != null ? t.GetComponent<Button>() : null;
            }

            // ── Left Panel ────────────────────────────────────────────────────
            Wire("ballImage",  FindImage("LeftPanel/BallImage"));
            Wire("infoHeader", FindTMP("LeftPanel/InfoPanel/InfoHeader"));
            Wire("infoText",   FindTMP("LeftPanel/InfoPanel/InfoText"));

            // ── Right Panel — Name & Quantity ─────────────────────────────────
            Wire("ballNameText", FindTMP("RightPanel/BallNamePanel/BallNameText"));
            Wire("ownedLabel",   FindTMP("RightPanel/OwnedPanel/OwnedLabel"));
            Wire("quantityText", FindTMP("RightPanel/OwnedPanel/QuantityText"));

            // ── Stat Rows ─────────────────────────────────────────────────────
            // Expected path pattern: RightPanel/BallStatsPanel/BallStats{N}/...
            void WireStatRow(string rowPath, string nameField, string barField, string numberField)
            {
                Wire(nameField,   FindTMP($"{rowPath}/StatName"));
                Wire(barField,    FindImage($"{rowPath}/Name+Bar/Bar"));
                Wire(numberField, FindTMP($"{rowPath}/StatNumber"));
            }

            WireStatRow("RightPanel/BallStatsPanel/BallStats1", "powerName",         "powerBar",         "powerNumber");
            WireStatRow("RightPanel/BallStatsPanel/BallStats2", "reboundName",       "reboundBar",       "reboundNumber");
            WireStatRow("RightPanel/BallStatsPanel/BallStats3", "windResistanceName","windResistanceBar","windResistanceNumber");
            WireStatRow("RightPanel/BallStatsPanel/BallStats4", "rollName",          "rollBar",          "rollNumber");
            WireStatRow("RightPanel/BallStatsPanel/BallStats5", "spinName",          "spinBar",          "spinNumber");

            // ── Buttons ───────────────────────────────────────────────────────
            Wire("compareButton", FindButton("RightPanel/ButtonsPanel/CompareButton"));

            // ── Carousel (sibling) ────────────────────────────────────────────
            var carouselObjects = Resources.FindObjectsOfTypeAll<BallCarouselController>();
            BallCarouselController? carousel = null;
            foreach (var c in carouselObjects)
                if (c.gameObject.scene.isLoaded) { carousel = c; break; }
            Wire("carousel", carousel);

            so.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());

            string msg = $"Wired {wired} fields, {failed} failed.\n\n" +
                         (failed > 0 ? "Check Console for missing paths — adjust hierarchy paths in BallDetailPanelAutoWire.cs." : "All fields wired successfully!");
            Debug.Log($"[BallDetailPanelAutoWire] Done — {wired} wired, {failed} failed.");
            EditorUtility.DisplayDialog("Ball Detail Panel Auto-Wire", msg, "OK");
        }
    }
}
#endif
