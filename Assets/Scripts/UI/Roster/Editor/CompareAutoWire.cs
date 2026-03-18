#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Golfin.Roster.Editor
{
    /// <summary>
    /// Wires all serialized fields on CompareController and
    /// the compareController field on CharacterDetailPanel.
    ///
    /// Run after: GOLFIN > Build Compare Right Panel
    /// </summary>
    public static class CompareAutoWire
    {
        [MenuItem("GOLFIN/Wire Compare Panel")]
        public static void Wire()
        {
            var detailPanel = FindDetailPanel();
            if (detailPanel == null)
            {
                Debug.LogError("[CompareAutoWire] DetailPanel not found.");
                return;
            }

            // ── Get or add CompareController ──────────────────────────────────
            var controller = detailPanel.GetComponent<CompareController>();
            if (controller == null)
            {
                controller = detailPanel.gameObject.AddComponent<CompareController>();
                Debug.Log("[CompareAutoWire] Added CompareController to DetailPanel.");
            }

            var so     = new SerializedObject(controller);
            var root   = detailPanel;
            int wired  = 0;
            int failed = 0;

            // ── Normal Mode ───────────────────────────────────────────────────
            wired += WireGO(so,     "leftPanel",   root, "LeftPanel",   ref failed);
            wired += WireRT(so,     "rightPanel",  root, "RightPanel",  ref failed);

            // ── Compare Layout ────────────────────────────────────────────────
            wired += WireGO(so,     "compareRightPanel",  root, "CompareRightPanel",                         ref failed);
            wired += WireGO(so,     "comparePlaceholder", root, "CompareRightPanel/ComparePlaceholder",      ref failed);
            wired += WireGO(so,     "compareInfoPanel",   root, "CompareRightPanel/CompareInfoPanel",        ref failed);
            wired += WireGO(so,     "verticalDivider",    root, "VerticalDivider",                           ref failed);

            // ── Left Column Buttons ───────────────────────────────────────────
            // Try ButtonsPanel first; fall back to RightPanel root if not found
            var buttonsRoot = root.Find("RightPanel/ButtonsPanel") ?? root.Find("RightPanel");
            wired += WireButtonFrom(so, "compareButton",      buttonsRoot, "LevelUpButton",      ref failed); // existing
            // NOTE: "compareButton" wires to the existing CompareButton in the detail panel
            wired += WireButtonFrom(so, "compareButton",      root, "RightPanel/CompareButton",  ref failed);
            wired += WireButtonFrom(so, "closeCompareButton", root, "RightPanel/ButtonsPanel/CloseCompareButton", ref failed);
            wired += WireButtonFrom(so, "selectButton",       root, "RightPanel/SelectButton",   ref failed);
            wired += WireButtonFrom(so, "swapButton",         root, "RightPanel/ButtonsPanel/SwapButton",    ref failed);

            // ── Right Column Info ─────────────────────────────────────────────
            var infoRoot = root.Find("CompareRightPanel/CompareInfoPanel");
            if (infoRoot == null)
            {
                Debug.LogWarning("[CompareAutoWire] CompareInfoPanel not found — run Build first.");
                failed += 20;
            }
            else
            {
                wired += WireTMPFrom(so, "compareNameText",     infoRoot, "CompareNameText",                    ref failed);
                wired += WireTMPFrom(so, "compareRarityLabel",  infoRoot, "CompareRarityRow/CompareRarityLabel",ref failed);
                wired += WireTMPFrom(so, "compareLevelText",    infoRoot, "CompareRarityRow/CompareLevelText",  ref failed);
                wired += WireTMPFrom(so, "compareMaxLevelText", infoRoot, "CompareRarityRow/CompareMaxLevelText",ref failed);

                wired += WireGOFrom(so,  "compareStrengthRow",    infoRoot, "CompareStatsPanel/CompareStats1", ref failed);
                wired += WireGOFrom(so,  "compareClubControlRow", infoRoot, "CompareStatsPanel/CompareStats2", ref failed);
                wired += WireGOFrom(so,  "compareRecoveryRow",    infoRoot, "CompareStatsPanel/CompareStats3", ref failed);
                wired += WireGOFrom(so,  "compareStaminaRow",     infoRoot, "CompareStatsPanel/CompareStats4", ref failed);

                wired += WireButtonFrom(so, "compareLevelUpButton", infoRoot, "CompareButtonsPanel/CompareLevelUpButton", ref failed);
                wired += WireButtonFrom(so, "compareBoostButton",   infoRoot, "CompareButtonsPanel/CompareBoostButton",   ref failed);
                wired += WireTMPFrom(so,    "compareBioText",       infoRoot, "CompareBioPanel/CompareBioText",            ref failed);
                wired += WireButtonFrom(so, "compareRightCompareButton", infoRoot, "CompareActionsPanel/CompareRightCompareButton", ref failed);
                wired += WireButtonFrom(so, "compareRightSelectButton",  infoRoot, "CompareActionsPanel/CompareRightSelectButton",  ref failed);
                wired += WireTMPFrom(so,    "compareRightSelectButtonText", infoRoot, "CompareActionsPanel/CompareRightSelectButton/Text", ref failed);
            }

            // ── Carousel ──────────────────────────────────────────────────────
            var carousel = Object.FindObjectOfType<CarouselController>();
            if (carousel != null)
            {
                var prop = so.FindProperty("carousel");
                if (prop != null) { prop.objectReferenceValue = carousel; wired++; }
            }
            else { Debug.LogWarning("[CompareAutoWire] CarouselController not found."); failed++; }

            // ── Level Up Modal ─────────────────────────────────────────────────
            var modal = Object.FindObjectOfType<LevelUpModalController>();
            if (modal != null)
            {
                var prop = so.FindProperty("levelUpModal");
                if (prop != null) { prop.objectReferenceValue = modal; wired++; }
            }
            else { Debug.LogWarning("[CompareAutoWire] LevelUpModalController not found."); failed++; }

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(controller);

            // ── Wire CharacterDetailPanel.compareController ────────────────────
            var cdp = detailPanel.GetComponent<CharacterDetailPanel>();
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
                    Debug.Log("[CompareAutoWire] Wired CharacterDetailPanel.compareController.");
                }
            }

            Debug.Log($"[CompareAutoWire] Done — {wired} fields wired, {failed} failed.");
            if (failed > 0)
                Debug.LogWarning("[CompareAutoWire] Some fields not found. Check paths or wire manually.");
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private static Transform? FindDetailPanel()
        {
            var direct = GameObject.Find("DetailPanel");
            if (direct != null) return direct.transform;
            var canvas = GameObject.Find("Canvas");
            return canvas?.transform.Find("ScreensRoot/RosterScreen/CarouselSection/DetailPanel");
        }

        // Wire a GameObject field by path relative to `root`
        private static int WireGO(SerializedObject so, string field, Transform root,
            string path, ref int failed)
        {
            var t = root.Find(path);
            return SetProp(so, field, t?.gameObject, path, ref failed);
        }

        // Wire a RectTransform field
        private static int WireRT(SerializedObject so, string field, Transform root,
            string path, ref int failed)
        {
            var t = root.Find(path);
            return SetProp(so, field, t?.GetComponent<RectTransform>(), path, ref failed);
        }

        // Wire a TMP field relative to `from`
        private static int WireTMPFrom(SerializedObject so, string field, Transform from,
            string path, ref int failed)
        {
            var t = from.Find(path);
            var tmp = t?.GetComponent<TextMeshProUGUI>();
            return SetProp(so, field, tmp, path, ref failed);
        }

        // Wire a Button field relative to `from`
        private static int WireButtonFrom(SerializedObject so, string field, Transform? from,
            string path, ref int failed)
        {
            if (from == null) { failed++; return 0; }
            var t   = from.Find(path);
            var btn = t?.GetComponent<Button>();
            return SetProp(so, field, btn, path, ref failed);
        }

        // Wire a GameObject field relative to `from`
        private static int WireGOFrom(SerializedObject so, string field, Transform from,
            string path, ref int failed)
        {
            var t = from.Find(path);
            return SetProp(so, field, t?.gameObject, path, ref failed);
        }

        private static int SetProp<T>(SerializedObject so, string field, T? obj, string path,
            ref int failed) where T : Object
        {
            var prop = so.FindProperty(field);
            if (prop == null)
            {
                Debug.LogWarning($"[CompareAutoWire] Field '{field}' not found on component.");
                failed++; return 0;
            }
            if (obj == null)
            {
                Debug.LogWarning($"[CompareAutoWire] Path '{path}' not found for '{field}'. Wire manually.");
                failed++; return 0;
            }
            prop.objectReferenceValue = obj;
            return 1;
        }
    }
}
#endif
