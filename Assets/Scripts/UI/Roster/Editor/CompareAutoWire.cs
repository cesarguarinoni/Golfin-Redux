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
    ///
    /// CompareRightPanel is now a clone of RightPanel, so the right-column
    /// fields are found using the same child paths as CharacterDetailPanel
    /// (CharacterNamePanel/CharacterNameText, RarityPanel/RarityRow/RarityLabel, etc.)
    /// but relative to CompareRightPanel/CompareInfoPanel.
    /// </summary>
    public static class CompareAutoWire
    {
        [MenuItem("GOLFIN/Wire/Roster Compare Panel")]
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
            wired += WireGO(so, "leftPanel",  root, "LeftPanel",  ref failed);
            wired += WireRT(so, "rightPanel", root, "RightPanel", ref failed);

            // ── Compare Layout ────────────────────────────────────────────────
            wired += WireGO(so, "compareRightPanel",  root, "CompareRightPanel",                    ref failed);
            wired += WireGO(so,  "comparePlaceholder",     root, "CompareRightPanel/ComparePlaceholder",                    ref failed);
            wired += WireTMPFrom(so, "comparePlaceholderText", root.Find("CompareRightPanel/ComparePlaceholder"), "PlaceholderText", ref failed);
            wired += WireGO(so, "compareInfoPanel",   root, "CompareRightPanel/CompareInfoPanel",   ref failed);
            wired += WireGO(so, "verticalDivider",    root, "VerticalDivider",                      ref failed);

            // ── Left Column Buttons ───────────────────────────────────────────
            var buttonsRoot = root.Find("RightPanel/ButtonsPanel") ?? root.Find("RightPanel");
            wired += WireButtonFrom(so, "compareButton",      root, "RightPanel/CompareButton",      ref failed);
            wired += WireButtonFrom(so, "closeCompareButton", root, "RightPanel/CloseCompareButton", ref failed);
            wired += WireButtonFrom(so, "selectButton",       root, "RightPanel/SelectButton",       ref failed);
            wired += WireButtonFrom(so, "swapButton",         buttonsRoot, "SwapButton",             ref failed);

            // ── Right Column Info (inside CompareRightPanel/CompareInfoPanel) ─
            // CompareInfoPanel is a clone of RightPanel, so paths match CharacterDetailPanel.
            var infoRoot = root.Find("CompareRightPanel/CompareInfoPanel");
            if (infoRoot == null)
            {
                Debug.LogWarning("[CompareAutoWire] CompareInfoPanel not found — run Build first.");
                failed += 15;
            }
            else
            {
                // Name
                wired += WireTMPFrom(so, "compareNameText",
                    infoRoot, "CharacterNamePanel/CharacterNameText", ref failed);

                // Rarity row — RarityText is child 0; LevelText/LevelTextMax are inside LevelPanel
                wired += WireTMPFrom(so, "compareRarityLabel",
                    infoRoot, "RarityPanel/RarityRow/RarityText",              ref failed);
                wired += WireTMPFrom(so, "compareLevelText",
                    infoRoot, "RarityPanel/RarityRow/LevelPanel/LevelText",    ref failed);
                wired += WireTMPFrom(so, "compareMaxLevelText",
                    infoRoot, "RarityPanel/RarityRow/LevelPanel/LevelTextMax", ref failed);

                // Stat rows
                wired += WireGOFrom(so, "compareStrengthRow",
                    infoRoot, "CharacterStatsPanel/CharacterStats1", ref failed);
                wired += WireGOFrom(so, "compareClubControlRow",
                    infoRoot, "CharacterStatsPanel/CharacterStats2", ref failed);
                wired += WireGOFrom(so, "compareRecoveryRow",
                    infoRoot, "CharacterStatsPanel/CharacterStats3", ref failed);
                wired += WireGOFrom(so, "compareStaminaRow",
                    infoRoot, "CharacterStatsPanel/CharacterStats4", ref failed);

                // Stat diff labels
                wired += WireTMPFrom(so, "strengthDiffLabel",
                    infoRoot, "CharacterStatsPanel/CharacterStats1/DiffLabel", ref failed);
                wired += WireTMPFrom(so, "clubControlDiffLabel",
                    infoRoot, "CharacterStatsPanel/CharacterStats2/DiffLabel", ref failed);
                wired += WireTMPFrom(so, "recoveryDiffLabel",
                    infoRoot, "CharacterStatsPanel/CharacterStats3/DiffLabel", ref failed);
                wired += WireTMPFrom(so, "staminaDiffLabel",
                    infoRoot, "CharacterStatsPanel/CharacterStats4/DiffLabel", ref failed);

                // Buttons — Level Up and Boost reuse the cloned ButtonsPanel buttons
                wired += WireButtonFrom(so, "compareLevelUpButton",
                    infoRoot, "ButtonsPanel/LevelUpButton", ref failed);
                wired += WireButtonFrom(so, "compareBoostButton",
                    infoRoot, "ButtonsPanel/BoostButton",   ref failed);

                // Bio
                wired += WireTMPFrom(so, "compareBioText",
                    infoRoot, "BioPanel/BioText", ref failed);

                // Compare-specific action buttons (reuse cloned CompareButton / SelectButton)
                wired += WireButtonFrom(so, "compareRightCompareButton",
                    infoRoot, "CompareButton",   ref failed);
                wired += WireButtonFrom(so, "compareRightSelectButton",
                    infoRoot, "SelectButton",    ref failed);
                wired += WireTMPFrom(so, "compareRightSelectButtonText",
                    infoRoot, "SelectButton/Text (TMP)", ref failed);
            }

            // ── Carousel ──────────────────────────────────────────────────────
            var carousel = Object.FindObjectOfType<CarouselController>(true);
            if (carousel != null)
            {
                var prop = so.FindProperty("carousel");
                if (prop != null) { prop.objectReferenceValue = carousel; wired++; }
            }
            else { Debug.LogWarning("[CompareAutoWire] CarouselController not found."); failed++; }

            // ── Level Up Modal ─────────────────────────────────────────────────
            var modal = Object.FindObjectOfType<LevelUpModalController>(true);
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
            // GameObject.Find misses inactive objects — search all scene GameObjects instead
            foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (go.name == "DetailPanel" && go.scene.isLoaded)
                    return go.transform;
            }
            return null;
        }

        private static int WireGO(SerializedObject so, string field, Transform root,
            string path, ref int failed)
        {
            var t = root.Find(path);
            return SetProp(so, field, t?.gameObject, path, ref failed);
        }

        private static int WireRT(SerializedObject so, string field, Transform root,
            string path, ref int failed)
        {
            var t = root.Find(path);
            return SetProp(so, field, t?.GetComponent<RectTransform>(), path, ref failed);
        }

        private static int WireTMPFrom(SerializedObject so, string field, Transform from,
            string path, ref int failed)
        {
            var t   = from.Find(path);
            var tmp = t?.GetComponent<TextMeshProUGUI>();
            return SetProp(so, field, tmp, path, ref failed);
        }

        private static int WireButtonFrom(SerializedObject so, string field, Transform? from,
            string path, ref int failed)
        {
            if (from == null) { failed++; return 0; }
            var t   = from.Find(path);
            var btn = t?.GetComponent<Button>();
            return SetProp(so, field, btn, path, ref failed);
        }

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
