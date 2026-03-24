#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Golfin.Inventory.Editor
{
    /// <summary>
    /// Wires all serialized fields on ClubCompareController and
    /// the compareController field on ClubDetailPanel.
    ///
    /// Run after: GOLFIN/Inventory/Build Club Compare Panel
    ///
    /// Field → Path mapping:
    ///
    /// Normal Mode:
    ///   leftPanel         → ClubDetailPanel/LeftPanel
    ///   rightPanel        → ClubDetailPanel/RightPanel (RectTransform)
    ///
    /// Compare Mode Layout:
    ///   compareRightPanel     → ClubDetailPanel/CompareRightPanel
    ///   comparePlaceholder    → ClubDetailPanel/CompareRightPanel/ComparePlaceholder
    ///   comparePlaceholderText → ClubDetailPanel/CompareRightPanel/ComparePlaceholder/PlaceholderText
    ///   compareInfoPanel      → ClubDetailPanel/CompareRightPanel/CompareInfoPanel
    ///   verticalDivider       → ClubDetailPanel/VerticalDivider
    ///
    /// Left Column Buttons (on RightPanel):
    ///   compareButton      → ClubDetailPanel/RightPanel/CompareButton
    ///   closeCompareButton → ClubDetailPanel/RightPanel/CloseCompareButton
    ///   equipButton        → ClubDetailPanel/RightPanel/EquipButton
    ///   swapButton         → ClubDetailPanel/RightPanel/SwapButton
    ///
    /// Right Column — all inside CompareRightPanel/CompareInfoPanel:
    ///   compareNameText        → ClubNameText
    ///   compareRarityLabel     → RarityLevelRow/RarityLabel
    ///   compareLevelText       → RarityLevelRow/LevelText
    ///   compareMaxLevelText    → RarityLevelRow/LevelTextMax
    ///   comparePower*/Accuracy*/LieRes*/Loft*/Durability* → StatsPanel/{row}/StatsName|Bar|StatNumber|DiffLabel
    ///   compareDistanceName    → StatsPanel/DistanceRow/StatsName
    ///   compareDistanceValue   → StatsPanel/DistanceRow/DistanceValue
    ///   compareDistanceDiff    → StatsPanel/DistanceRow/DiffLabel
    ///   compareLevelUpButton   → ButtonsPanel/LevelUpButton
    ///   compareRepairButton    → ButtonsPanel/RepairButton
    ///   compareRightCompareButton → CompareButton
    ///   compareRightEquipButton   → EquipButton
    ///   compareRightEquipText     → EquipButton/Text (TMP)
    ///   compareBagLabel           → BagLabel
    ///
    /// Carousel:
    ///   carousel → ClubCarouselController in scene
    /// </summary>
    public static class ClubCompareAutoWire
    {
        [MenuItem("GOLFIN/Wire/Club Compare Panel")]
        public static void Run()
        {
            var detailPanelGO = FindByName("ClubDetailPanel");
            if (detailPanelGO == null)
            {
                EditorUtility.DisplayDialog("Wire Club Compare Panel",
                    "ClubDetailPanel not found.\n\n" +
                    "Run GOLFIN/Inventory/Build Club Compare Panel first.", "OK");
                return;
            }

            var controller = detailPanelGO.GetComponent<ClubCompareController>();
            if (controller == null)
            {
                EditorUtility.DisplayDialog("Wire Club Compare Panel",
                    "ClubCompareController not found on ClubDetailPanel.\n\n" +
                    "Run GOLFIN/Inventory/Build Club Compare Panel first.", "OK");
                return;
            }

            var so   = new SerializedObject(controller);
            var root = detailPanelGO.transform;
            int wired = 0, failed = 0;

            // ── Normal Mode ───────────────────────────────────────────────────
            wired += WireGO(so, "leftPanel",  root, "LeftPanel",  ref failed);
            wired += WireRT(so, "rightPanel", root, "RightPanel", ref failed);

            // ── Compare Layout ────────────────────────────────────────────────
            wired += WireGO(so, "compareRightPanel",  root, "CompareRightPanel",                            ref failed);
            wired += WireGO(so, "comparePlaceholder", root, "CompareRightPanel/ComparePlaceholder",         ref failed);
            wired += WireTMP(so, "comparePlaceholderText",
                root, "CompareRightPanel/ComparePlaceholder/PlaceholderText", ref failed);
            wired += WireGO(so, "compareInfoPanel",   root, "CompareRightPanel/CompareInfoPanel",           ref failed);
            wired += WireGO(so, "verticalDivider",    root, "VerticalDivider",                              ref failed);

            // ── Left Column Buttons ───────────────────────────────────────────
            wired += WireButton(so, "compareButton",      root, "RightPanel/CompareButton",      ref failed);
            wired += WireButton(so, "closeCompareButton", root, "RightPanel/CloseCompareButton", ref failed);
            wired += WireButton(so, "equipButton",        root, "RightPanel/EquipButton",        ref failed);
            wired += WireButton(so, "swapButton",         root, "RightPanel/SwapButton",         ref failed);

            // ── Right Column — inside CompareRightPanel/CompareInfoPanel ──────
            var infoRoot = root.Find("CompareRightPanel/CompareInfoPanel");
            if (infoRoot == null)
            {
                Debug.LogWarning("[ClubCompareAutoWire] CompareInfoPanel not found — run Build first.");
                failed += 25;
            }
            else
            {
                // Name
                wired += WireTMPFrom(so, "compareNameText", infoRoot, "ClubNameText", ref failed);

                // Rarity + Level
                wired += WireTMPFrom(so, "compareRarityLabel",  infoRoot, "RarityLevelRow/RarityLabel",  ref failed);
                wired += WireTMPFrom(so, "compareLevelText",    infoRoot, "RarityLevelRow/LevelText",    ref failed);
                wired += WireTMPFrom(so, "compareMaxLevelText", infoRoot, "RarityLevelRow/LevelTextMax", ref failed);

                // Power
                wired += WireStatRow(so,
                    "comparePowerName",   "comparePowerBar",   "comparePowerNumber",   "comparePowerDiff",
                    infoRoot, "StatsPanel/PowerRow", ref failed);

                // Accuracy
                wired += WireStatRow(so,
                    "compareAccuracyName", "compareAccuracyBar", "compareAccuracyNumber", "compareAccuracyDiff",
                    infoRoot, "StatsPanel/AccuracyRow", ref failed);

                // Lie Resistance
                wired += WireStatRow(so,
                    "compareLieResName", "compareLieResBar", "compareLieResNumber", "compareLieResDiff",
                    infoRoot, "StatsPanel/LieResistanceRow", ref failed);

                // Loft
                wired += WireStatRow(so,
                    "compareLoftName", "compareLoftBar", "compareLoftNumber", "compareLoftDiff",
                    infoRoot, "StatsPanel/LoftRow", ref failed);

                // Durability
                wired += WireStatRow(so,
                    "compareDurabilityName", "compareDurabilityBar", "compareDurabilityNumber", "compareDurabilityDiff",
                    infoRoot, "StatsPanel/DurabilityRow", ref failed);

                // Distance
                wired += WireTMPFrom(so, "compareDistanceName",  infoRoot, "StatsPanel/DistanceRow/StatsName",     ref failed);
                wired += WireTMPFrom(so, "compareDistanceValue", infoRoot, "StatsPanel/DistanceRow/DistanceValue", ref failed);
                wired += WireTMPFrom(so, "compareDistanceDiff",  infoRoot, "StatsPanel/DistanceRow/DiffLabel",     ref failed);

                // Buttons
                wired += WireButtonFrom(so, "compareLevelUpButton",      infoRoot, "ButtonsPanel/LevelUpButton", ref failed);
                wired += WireButtonFrom(so, "compareRepairButton",       infoRoot, "ButtonsPanel/RepairButton",  ref failed);
                wired += WireButtonFrom(so, "compareRightCompareButton", infoRoot, "CompareButton",              ref failed);
                wired += WireButtonFrom(so, "compareRightEquipButton",   infoRoot, "EquipButton",                ref failed);
                wired += WireTMPFrom(so,   "compareRightEquipText",      infoRoot, "EquipButton/Text (TMP)",     ref failed);
                wired += WireTMPFrom(so,   "compareBagLabel",            infoRoot, "BagLabel",                   ref failed);
            }

            // ── Carousel ──────────────────────────────────────────────────────
            var carousel = Object.FindObjectOfType<ClubCarouselController>();
            if (carousel != null)
            {
                var prop = so.FindProperty("carousel");
                if (prop != null) { prop.objectReferenceValue = carousel; wired++; }
            }
            else
            {
                Debug.LogWarning("[ClubCompareAutoWire] ClubCarouselController not found — wire 'carousel' manually.");
                failed++;
            }

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(controller);

            // ── Wire ClubDetailPanel.compareController ─────────────────────────
            var cdp = detailPanelGO.GetComponent<ClubDetailPanel>();
            if (cdp != null)
            {
                var cdpSO = new SerializedObject(cdp);
                var prop  = cdpSO.FindProperty("compareController");
                if (prop != null)
                {
                    prop.objectReferenceValue = controller;
                    cdpSO.ApplyModifiedProperties();
                    EditorUtility.SetDirty(cdp);
                    wired++;
                    Debug.Log("[ClubCompareAutoWire] Wired ClubDetailPanel.compareController.");
                }
                else
                {
                    Debug.LogWarning("[ClubCompareAutoWire] 'compareController' field not found on ClubDetailPanel — " +
                        "make sure you've recompiled after modifying ClubDetailPanel.cs.");
                    failed++;
                }
            }

            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());

            string msg = $"Done!\n\n{wired} fields wired, {failed} failed.\n\n";
            msg += failed > 0
                ? "Check the Console for details. Wire failed fields manually."
                : "All fields wired successfully.\n\nTest by entering Play mode and tapping COMPARE.";

            EditorUtility.DisplayDialog("Wire Club Compare Panel", msg, "OK");
            Debug.Log($"[ClubCompareAutoWire] ✓ {wired} wired, {failed} failed.");
        }

        // ── Stat Row Helper ────────────────────────────────────────────────────

        private static int WireStatRow(SerializedObject so,
            string nameProp, string barProp, string numberProp, string diffProp,
            Transform infoRoot, string rowPath, ref int failed)
        {
            int count = 0;
            count += WireTMPFrom  (so, nameProp,   infoRoot, rowPath + "/StatsName",  ref failed);
            count += WireImageFrom(so, barProp,    infoRoot, rowPath + "/Bar",         ref failed);
            count += WireTMPFrom  (so, numberProp, infoRoot, rowPath + "/StatNumber",  ref failed);
            count += WireTMPFrom  (so, diffProp,   infoRoot, rowPath + "/DiffLabel",   ref failed);
            return count;
        }

        // ── Wire Helpers ───────────────────────────────────────────────────────

        private static int WireGO(SerializedObject so, string field, Transform root, string path, ref int failed)
        {
            var t = root.Find(path);
            return SetProp(so, field, t?.gameObject, path, ref failed);
        }

        private static int WireRT(SerializedObject so, string field, Transform root, string path, ref int failed)
        {
            var t = root.Find(path);
            return SetProp(so, field, t?.GetComponent<RectTransform>(), path, ref failed);
        }

        private static int WireTMP(SerializedObject so, string field, Transform root, string path, ref int failed)
        {
            var t   = root.Find(path);
            var tmp = t?.GetComponent<TextMeshProUGUI>();
            return SetProp(so, field, tmp, path, ref failed);
        }

        private static int WireTMPFrom(SerializedObject so, string field, Transform from, string path, ref int failed)
        {
            var t   = from.Find(path);
            var tmp = t?.GetComponent<TextMeshProUGUI>();
            return SetProp(so, field, tmp, path, ref failed);
        }

        private static int WireImageFrom(SerializedObject so, string field, Transform from, string path, ref int failed)
        {
            var t   = from.Find(path);
            var img = t?.GetComponent<Image>();
            return SetProp(so, field, img, path, ref failed);
        }

        private static int WireButton(SerializedObject so, string field, Transform root, string path, ref int failed)
        {
            var t   = root.Find(path);
            var btn = t?.GetComponent<Button>();
            return SetProp(so, field, btn, path, ref failed);
        }

        private static int WireButtonFrom(SerializedObject so, string field, Transform from, string path, ref int failed)
        {
            var t   = from.Find(path);
            var btn = t?.GetComponent<Button>();
            return SetProp(so, field, btn, path, ref failed);
        }

        private static int SetProp<T>(SerializedObject so, string field, T? obj, string path,
            ref int failed) where T : Object
        {
            var prop = so.FindProperty(field);
            if (prop == null)
            {
                Debug.LogWarning($"[ClubCompareAutoWire] Field '{field}' not found on component.");
                failed++; return 0;
            }
            if (obj == null)
            {
                Debug.LogWarning($"[ClubCompareAutoWire] Path '{path}' not found for '{field}'. Wire manually.");
                failed++; return 0;
            }
            prop.objectReferenceValue = obj;
            return 1;
        }

        // ── Scene Search ───────────────────────────────────────────────────────

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
