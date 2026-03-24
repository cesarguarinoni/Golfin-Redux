#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Golfin.Inventory.Editor
{
    /// <summary>
    /// Wires all serialized fields on ClubDetailPanel from the hierarchy built by ClubDetailPanelBuilder.
    ///
    /// Run: GOLFIN/Inventory/Wire Club Detail Panel
    /// Run this AFTER GOLFIN/Inventory/Build Club Phase C.
    /// </summary>
    public static class ClubDetailPanelAutoWire
    {
        [MenuItem("GOLFIN/Wire/Club Detail Panel")]
        public static void Run()
        {
            var detailPanelGO = FindByName("ClubDetailPanel");
            if (detailPanelGO == null)
            {
                EditorUtility.DisplayDialog("Wire Club Detail Panel",
                    "ClubDetailPanel not found in scene.\n\n" +
                    "Run GOLFIN/Inventory/Build Club Phase C first.", "OK");
                return;
            }

            var panel = detailPanelGO.GetComponent<ClubDetailPanel>();
            if (panel == null)
            {
                EditorUtility.DisplayDialog("Wire Club Detail Panel",
                    "ClubDetailPanel component not found on the GameObject.\n" +
                    "Re-run GOLFIN/Inventory/Build Club Phase C.", "OK");
                return;
            }

            var so   = new SerializedObject(panel);
            var root = detailPanelGO.transform;
            int wired = 0, failed = 0;

            // ── Left Panel ────────────────────────────────────────────────────
            wired += WireImage (so, "clubImage",   root, "LeftPanel/ClubImage",                     ref failed);
            wired += WireTMP   (so, "infoHeader",  root, "LeftPanel/InfoSection/InfoHeader",         ref failed);
            wired += WireTMP   (so, "infoText",    root, "LeftPanel/InfoSection/InfoText",           ref failed);

            // ── Right Panel — Name & Level ─────────────────────────────────────
            wired += WireTMP   (so, "clubNameText",     root, "RightPanel/ClubNameText",                  ref failed);
            wired += WireTMP   (so, "rarityLabel",      root, "RightPanel/RarityLevelRow/RarityLabel",    ref failed);
            wired += WireTMP   (so, "currentLevelText", root, "RightPanel/RarityLevelRow/LevelText",      ref failed);
            wired += WireTMP   (so, "maxLevelText",     root, "RightPanel/RarityLevelRow/LevelTextMax",   ref failed);

            // ── Stat Bars ──────────────────────────────────────────────────────
            wired += WireStatRow(so, "powerName",         "powerBar",         "powerNumber",
                root, "RightPanel/StatsPanel/PowerRow",         ref failed);

            wired += WireStatRow(so, "accuracyName",      "accuracyBar",      "accuracyNumber",
                root, "RightPanel/StatsPanel/AccuracyRow",      ref failed);

            wired += WireStatRow(so, "lieResistanceName", "lieResistanceBar", "lieResistanceNumber",
                root, "RightPanel/StatsPanel/LieResistanceRow", ref failed);

            wired += WireStatRow(so, "loftName",          "loftBar",          "loftNumber",
                root, "RightPanel/StatsPanel/LoftRow",          ref failed);

            wired += WireStatRow(so, "durabilityName",    "durabilityBar",    "durabilityNumber",
                root, "RightPanel/StatsPanel/DurabilityRow",    ref failed);

            // ── Distance ──────────────────────────────────────────────────────
            wired += WireTMP(so, "distanceName",  root, "RightPanel/StatsPanel/DistanceRow/StatsName",     ref failed);
            wired += WireTMP(so, "distanceValue", root, "RightPanel/StatsPanel/DistanceRow/DistanceValue", ref failed);

            // ── Buttons ───────────────────────────────────────────────────────
            wired += WireButton(so, "levelUpButton", root, "RightPanel/ButtonsPanel/LevelUpButton", ref failed);
            wired += WireButton(so, "repairButton",  root, "RightPanel/ButtonsPanel/RepairButton",  ref failed);
            wired += WireButton(so, "compareButton", root, "RightPanel/CompareButton",              ref failed);
            wired += WireButton(so, "equipButton",   root, "RightPanel/EquipButton",                ref failed);
            wired += WireTMP   (so, "equipButtonText", root, "RightPanel/EquipButton/Text (TMP)",   ref failed);
            wired += WireTMP   (so, "bagLabel",      root, "RightPanel/BagLabel",                   ref failed);

            // ── Carousel ──────────────────────────────────────────────────────
            var carousel = Object.FindObjectOfType<ClubCarouselController>();
            if (carousel != null)
            {
                so.FindProperty("carousel").objectReferenceValue = carousel;
                wired++;
            }
            else
            {
                Debug.LogWarning("[ClubDetailPanelAutoWire] ClubCarouselController not found — wire 'carousel' manually.");
                failed++;
            }

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(panel);
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());

            string msg = $"Done!\n\n{wired} fields wired, {failed} failed.\n\n";
            if (failed > 0)
                msg += "Check the Console for which paths failed and wire those manually.";
            else
                msg += "All fields wired successfully.";

            msg += "\n\nRemaining manual steps:\n" +
                   "• Assign ClubThumbnailCard prefab → ClubCarouselController.clubCardPrefab\n" +
                   "• Assign ClubFilterBar → ClubCarouselController.filterBar\n" +
                   "• Set Script Execution Order: ClubDatabaseCSV = -200, ClubManager = -100";

            EditorUtility.DisplayDialog("Wire Club Detail Panel", msg, "OK");
            Debug.Log($"[ClubDetailPanelAutoWire] ✓ {wired} wired, {failed} failed.");
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        /// <summary>Wires name, bar, and number fields for a stat row.</summary>
        private static int WireStatRow(SerializedObject so,
            string nameProp, string barProp, string numberProp,
            Transform root, string rowPath, ref int failed)
        {
            int count = 0;
            count += WireTMP  (so, nameProp,   root, rowPath + "/StatsName",  ref failed);
            count += WireImage(so, barProp,    root, rowPath + "/Bar",         ref failed);
            count += WireTMP  (so, numberProp, root, rowPath + "/StatNumber",  ref failed);
            return count;
        }

        private static int WireTMP(SerializedObject so, string fieldName,
            Transform root, string path, ref int failed)
        {
            var prop = so.FindProperty(fieldName);
            if (prop == null) { LogFail(fieldName, "property not found"); failed++; return 0; }

            var target = root.Find(path);
            if (target == null) { LogFail(fieldName, $"path '{path}' not found"); failed++; return 0; }

            var tmp = target.GetComponent<TextMeshProUGUI>();
            if (tmp == null) { LogFail(fieldName, $"no TMP on '{path}'"); failed++; return 0; }

            prop.objectReferenceValue = tmp;
            return 1;
        }

        private static int WireImage(SerializedObject so, string fieldName,
            Transform root, string path, ref int failed)
        {
            var prop = so.FindProperty(fieldName);
            if (prop == null) { LogFail(fieldName, "property not found"); failed++; return 0; }

            var target = root.Find(path);
            if (target == null) { LogFail(fieldName, $"path '{path}' not found"); failed++; return 0; }

            var img = target.GetComponent<Image>();
            if (img == null) { LogFail(fieldName, $"no Image on '{path}'"); failed++; return 0; }

            prop.objectReferenceValue = img;
            return 1;
        }

        private static int WireButton(SerializedObject so, string fieldName,
            Transform root, string path, ref int failed)
        {
            var prop = so.FindProperty(fieldName);
            if (prop == null) { LogFail(fieldName, "property not found"); failed++; return 0; }

            var target = root.Find(path);
            if (target == null) { LogFail(fieldName, $"path '{path}' not found"); failed++; return 0; }

            var btn = target.GetComponent<Button>();
            if (btn == null) { LogFail(fieldName, $"no Button on '{path}'"); failed++; return 0; }

            prop.objectReferenceValue = btn;
            return 1;
        }

        private static void LogFail(string field, string reason)
            => Debug.LogWarning($"[ClubDetailPanelAutoWire] '{field}' — {reason}. Wire manually.");

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
