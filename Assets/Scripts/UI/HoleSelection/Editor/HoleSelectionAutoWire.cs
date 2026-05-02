#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using GolfinRedux.UI;
using GolfinRedux.UI.HoleSelection;
using Golfin.UI.Matchmaking;

namespace GolfinRedux.UI.HoleSelection.Editor
{
    /// <summary>
    /// Auto-wires HoleSelectionScreenController fields in the active scene (ShellScene).
    /// Also wires the HoleCardController fields on the HoleCard prefab.
    /// Also wires ScreenManager._holeSelectionScreen.
    ///
    /// Run: GOLFIN/Wire/Hole Selection
    /// </summary>
    public static class HoleSelectionAutoWire
    {
        private const string HOLE_CARD_PREFAB_PATH   = "Assets/Prefabs/UI/HoleSelection/HoleCard.prefab";
        private const string HOLE_DATABASE_PATH      = "Assets/Data/HoleDatabase.asset";

        // Mutable holder so local functions can update counters (lambdas can't capture ref/out params).
        private class Counters { public int Wired; public int Failed; }

        [MenuItem("GOLFIN/Wire/Hole Selection")]
        public static void Run()
        {
            var c = new Counters();

            // ── Part A: Wire HoleCard prefab ─────────────────────────────────
            WireHoleCardPrefab(c);

            // ── Part B: Wire HoleSelectionScreen in active scene ──────────────
            WireHoleSelectionScreen(c);

            // ── Part C: Wire ScreenManager._holeSelectionScreen ───────────────
            WireScreenManager(c);

            EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());

            string msg = $"Hole Selection auto-wire complete.\n{c.Wired} fields wired, {c.Failed} failed." +
                         (c.Failed > 0
                             ? "\nCheck Console for missing paths — some fields need manual assignment."
                             : "\nAll fields wired successfully! Save the scene (Cmd+S / Ctrl+S).");

            EditorUtility.DisplayDialog("GOLFIN — Wire Hole Selection", msg, "OK");
        }

        // ── Part A ────────────────────────────────────────────────────────────

        private static void WireHoleCardPrefab(Counters counters)
        {
            var prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(HOLE_CARD_PREFAB_PATH);
            if (prefabAsset == null)
            {
                Debug.LogWarning($"[HoleSelectionAutoWire] HoleCard prefab not found at '{HOLE_CARD_PREFAB_PATH}'. Skipping Part A.");
                counters.Failed++;
                return;
            }

            // Open prefab in isolation
            var prefabStage = PrefabStageUtility.OpenPrefab(HOLE_CARD_PREFAB_PATH);
            if (prefabStage == null)
            {
                Debug.LogWarning("[HoleSelectionAutoWire] Could not open HoleCard prefab in isolation. Skipping Part A.");
                counters.Failed++;
                return;
            }

            var prefabRoot = prefabStage.prefabContentsRoot;
            var ctrl       = prefabRoot.GetComponent<HoleCardController>();
            if (ctrl == null)
            {
                Debug.LogWarning("[HoleSelectionAutoWire] HoleCardController not found on HoleCard prefab root. Skipping Part A.");
                StageUtility.GoToMainStage();
                counters.Failed++;
                return;
            }

            var so = new SerializedObject(ctrl);

            void FailA(string prop, string path, string reason = "path not found")
            {
                Debug.LogWarning($"[HoleSelectionAutoWire] PREFAB FAIL '{prop}' at '{path}' — {reason}. Wire manually.");
                counters.Failed++;
            }

            int WireTMP(string prop, string path)
            {
                var t = prefabRoot.transform.Find(path);
                if (t == null) { FailA(prop, path); return 0; }
                var c = t.GetComponent<TextMeshProUGUI>();
                if (c == null) { FailA(prop, path, "no TMP component"); return 0; }
                so.FindProperty(prop).objectReferenceValue = c;
                Debug.Log($"[HoleSelectionAutoWire] OK prefab:{prop}"); counters.Wired++; return 1;
            }

            int WireImage(string prop, string path)
            {
                var t = prefabRoot.transform.Find(path);
                if (t == null) { FailA(prop, path); return 0; }
                var c = t.GetComponent<Image>();
                if (c == null) { FailA(prop, path, "no Image component"); return 0; }
                so.FindProperty(prop).objectReferenceValue = c;
                Debug.Log($"[HoleSelectionAutoWire] OK prefab:{prop}"); counters.Wired++; return 1;
            }

            int WireButton(string prop, string path)
            {
                var t = prefabRoot.transform.Find(path);
                if (t == null) { FailA(prop, path); return 0; }
                var c = t.GetComponent<Button>();
                if (c == null) { FailA(prop, path, "no Button component"); return 0; }
                so.FindProperty(prop).objectReferenceValue = c;
                Debug.Log($"[HoleSelectionAutoWire] OK prefab:{prop}"); counters.Wired++; return 1;
            }

            int WireGO(string prop, string path)
            {
                var t = prefabRoot.transform.Find(path);
                if (t == null) { FailA(prop, path); return 0; }
                so.FindProperty(prop).objectReferenceValue = t.gameObject;
                Debug.Log($"[HoleSelectionAutoWire] OK prefab:{prop}"); counters.Wired++; return 1;
            }

            // rootRect — the controller itself
            so.FindProperty("rootRect").objectReferenceValue = prefabRoot.GetComponent<RectTransform>();
            Debug.Log("[HoleSelectionAutoWire] OK prefab:rootRect"); counters.Wired++;

            WireGO("collapsedContainer", "CollapsedContainer");
            WireGO("expandedContainer",  "ExpandedContainer");

            WireTMP("titleTextCollapsed",    "CollapsedContainer/TitleArea/Title");
            WireTMP("subtitleTextCollapsed", "CollapsedContainer/TitleArea/Subtitle");
            WireTMP("titleTextExpanded",     "ExpandedContainer/TitleAreaExp/TitleExp");
            WireTMP("subtitleTextExpanded",  "ExpandedContainer/TitleAreaExp/SubtitleExp");

            WireImage("holeImage",        "ExpandedContainer/Tutorial/HoleImage");
            WireTMP("descriptionText",    "ExpandedContainer/Tutorial/DescriptionText");

            // Collapsed rewards (indices 0..2)
            var collapsedSlotsArr  = so.FindProperty("collapsedRewardSlots");
            var collapsedIconsArr  = so.FindProperty("collapsedRewardIcons");
            var collapsedAmountsArr = so.FindProperty("collapsedRewardAmounts");
            collapsedSlotsArr.arraySize  = 3;
            collapsedIconsArr.arraySize  = 3;
            collapsedAmountsArr.arraySize = 3;

            for (int i = 0; i < 3; i++)
            {
                int n = i + 1;
                string slotPath   = $"CollapsedContainer/RewardsRow/RewardSlot{n}";
                string iconPath   = $"CollapsedContainer/RewardsRow/RewardSlot{n}/Reward{n}Icon";
                string amountPath = $"CollapsedContainer/RewardsRow/RewardSlot{n}/Reward{n}Amount";

                var slotT = prefabRoot.transform.Find(slotPath);
                if (slotT != null) { collapsedSlotsArr.GetArrayElementAtIndex(i).objectReferenceValue = slotT.gameObject; counters.Wired++; Debug.Log($"[HoleSelectionAutoWire] OK prefab:collapsedRewardSlots[{i}]"); }
                else { FailA($"collapsedRewardSlots[{i}]", slotPath); }

                var iconT = prefabRoot.transform.Find(iconPath);
                var iconC = iconT != null ? iconT.GetComponent<Image>() : null;
                if (iconC != null) { collapsedIconsArr.GetArrayElementAtIndex(i).objectReferenceValue = iconC; counters.Wired++; Debug.Log($"[HoleSelectionAutoWire] OK prefab:collapsedRewardIcons[{i}]"); }
                else { FailA($"collapsedRewardIcons[{i}]", iconPath); }

                var amtT = prefabRoot.transform.Find(amountPath);
                var amtC = amtT != null ? amtT.GetComponent<TextMeshProUGUI>() : null;
                if (amtC != null) { collapsedAmountsArr.GetArrayElementAtIndex(i).objectReferenceValue = amtC; counters.Wired++; Debug.Log($"[HoleSelectionAutoWire] OK prefab:collapsedRewardAmounts[{i}]"); }
                else { FailA($"collapsedRewardAmounts[{i}]", amountPath); }
            }

            // Expanded rewards (indices 0..2)
            var expandedSlotsArr   = so.FindProperty("expandedRewardSlots");
            var expandedIconsArr   = so.FindProperty("expandedRewardIcons");
            var expandedAmountsArr = so.FindProperty("expandedRewardAmounts");
            expandedSlotsArr.arraySize   = 3;
            expandedIconsArr.arraySize   = 3;
            expandedAmountsArr.arraySize = 3;

            for (int i = 0; i < 3; i++)
            {
                int n = i + 1;
                string slotPath   = $"ExpandedContainer/RewardsRowExp/RewardSlot{n}Exp";
                string iconPath   = $"ExpandedContainer/RewardsRowExp/RewardSlot{n}Exp/Reward{n}IconExp";
                string amountPath = $"ExpandedContainer/RewardsRowExp/RewardSlot{n}Exp/Reward{n}AmountExp";

                var slotT = prefabRoot.transform.Find(slotPath);
                if (slotT != null) { expandedSlotsArr.GetArrayElementAtIndex(i).objectReferenceValue = slotT.gameObject; counters.Wired++; Debug.Log($"[HoleSelectionAutoWire] OK prefab:expandedRewardSlots[{i}]"); }
                else { FailA($"expandedRewardSlots[{i}]", slotPath); }

                var iconT = prefabRoot.transform.Find(iconPath);
                var iconC = iconT != null ? iconT.GetComponent<Image>() : null;
                if (iconC != null) { expandedIconsArr.GetArrayElementAtIndex(i).objectReferenceValue = iconC; counters.Wired++; Debug.Log($"[HoleSelectionAutoWire] OK prefab:expandedRewardIcons[{i}]"); }
                else { FailA($"expandedRewardIcons[{i}]", iconPath); }

                var amtT = prefabRoot.transform.Find(amountPath);
                var amtC = amtT != null ? amtT.GetComponent<TextMeshProUGUI>() : null;
                if (amtC != null) { expandedAmountsArr.GetArrayElementAtIndex(i).objectReferenceValue = amtC; counters.Wired++; Debug.Log($"[HoleSelectionAutoWire] OK prefab:expandedRewardAmounts[{i}]"); }
                else { FailA($"expandedRewardAmounts[{i}]", amountPath); }
            }

            WireButton("actionButton",   "ExpandedContainer/ActionButton");
            WireTMP("actionButtonLabel", "ExpandedContainer/ActionButton/Label");
            WireButton("cardTapButton",  "CardTapButton");
            WireGO("lockedOverlay",      "LockedOverlay");

            // Reward icon sprites — pull from scene's HomeScreenController
            HomeScreenController homeCtrl = null;
            foreach (var h in Resources.FindObjectsOfTypeAll<HomeScreenController>())
            {
                if (h.gameObject.scene.isLoaded) { homeCtrl = h; break; }
            }

            if (homeCtrl != null)
            {
                var homeSO = new SerializedObject(homeCtrl);
                var pIcon  = homeSO.FindProperty("pointsIcon")?.objectReferenceValue    as Sprite;
                var rkIcon = homeSO.FindProperty("repairKitIcon")?.objectReferenceValue as Sprite;
                var bIcon  = homeSO.FindProperty("ballIcon")?.objectReferenceValue      as Sprite;

                if (pIcon  != null) { so.FindProperty("pointsIcon").objectReferenceValue    = pIcon;  counters.Wired++; Debug.Log("[HoleSelectionAutoWire] OK prefab:pointsIcon"); }
                else { FailA("pointsIcon",    "(HomeScreenController)", "HomeScreen has no pointsIcon assigned"); }

                if (rkIcon != null) { so.FindProperty("repairKitIcon").objectReferenceValue = rkIcon; counters.Wired++; Debug.Log("[HoleSelectionAutoWire] OK prefab:repairKitIcon"); }
                else { FailA("repairKitIcon", "(HomeScreenController)", "HomeScreen has no repairKitIcon assigned"); }

                if (bIcon  != null) { so.FindProperty("ballIcon").objectReferenceValue      = bIcon;  counters.Wired++; Debug.Log("[HoleSelectionAutoWire] OK prefab:ballIcon"); }
                else { FailA("ballIcon",       "(HomeScreenController)", "HomeScreen has no ballIcon assigned"); }
            }
            else
            {
                Debug.LogWarning("[HoleSelectionAutoWire] HomeScreenController not found in scene — reward icons must be wired manually on HoleCard prefab.");
                counters.Failed += 3;
            }

            so.ApplyModifiedProperties();
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, HOLE_CARD_PREFAB_PATH);
            StageUtility.GoToMainStage();

            Debug.Log("[HoleSelectionAutoWire] Part A complete — HoleCard prefab wired.");
        }

        // ── Part B ────────────────────────────────────────────────────────────

        private static void WireHoleSelectionScreen(Counters counters)
        {
            HoleSelectionScreenController ctrl = null;
            foreach (var c in Resources.FindObjectsOfTypeAll<HoleSelectionScreenController>())
            {
                if (c.gameObject.scene.isLoaded) { ctrl = c; break; }
            }

            if (ctrl == null)
            {
                Debug.LogWarning("[HoleSelectionAutoWire] HoleSelectionScreenController not found in active scene. Add it to a HoleSelectionScreen GameObject first. Skipping Part B.");
                counters.Failed++;
                return;
            }

            var so   = new SerializedObject(ctrl);
            var root = ctrl.transform;

            void FailB(string prop, string path, string reason = "path not found")
            {
                Debug.LogWarning($"[HoleSelectionAutoWire] SCENE FAIL '{prop}' at '{path}' — {reason}. Wire manually.");
                counters.Failed++;
            }

            // cardsScrollRect
            {
                var t = root.Find("Content/CardsScrollView");
                if (t != null)
                {
                    var sr = t.GetComponent<ScrollRect>();
                    if (sr != null) { so.FindProperty("cardsScrollRect").objectReferenceValue = sr; counters.Wired++; Debug.Log("[HoleSelectionAutoWire] OK scene:cardsScrollRect"); }
                    else { FailB("cardsScrollRect", "Content/CardsScrollView", "no ScrollRect component"); }
                }
                else { FailB("cardsScrollRect", "Content/CardsScrollView"); }
            }

            // cardsContent
            {
                var t = root.Find("Content/CardsScrollView/Viewport/Content");
                if (t != null)
                {
                    var rt = t.GetComponent<RectTransform>();
                    if (rt != null) { so.FindProperty("cardsContent").objectReferenceValue = rt; counters.Wired++; Debug.Log("[HoleSelectionAutoWire] OK scene:cardsContent"); }
                    else { FailB("cardsContent", "Content/CardsScrollView/Viewport/Content", "no RectTransform"); }
                }
                else { FailB("cardsContent", "Content/CardsScrollView/Viewport/Content"); }
            }

            // filtersContainer
            {
                var t = root.Find("Content/Filters");
                if (t != null) { so.FindProperty("filtersContainer").objectReferenceValue = t.gameObject; counters.Wired++; Debug.Log("[HoleSelectionAutoWire] OK scene:filtersContainer"); }
                else { FailB("filtersContainer", "Content/Filters"); }
            }

            // cardPrefab
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HOLE_CARD_PREFAB_PATH);
                var prefabCtrl = prefab != null ? prefab.GetComponent<HoleCardController>() : null;
                if (prefabCtrl != null) { so.FindProperty("cardPrefab").objectReferenceValue = prefabCtrl; counters.Wired++; Debug.Log("[HoleSelectionAutoWire] OK scene:cardPrefab"); }
                else { FailB("cardPrefab", HOLE_CARD_PREFAB_PATH, "no HoleCardController on prefab root"); }
            }

            // holeDatabase
            {
                var holeDb = AssetDatabase.LoadAssetAtPath<HoleDatabase>(HOLE_DATABASE_PATH);
                if (holeDb != null) { so.FindProperty("holeDatabase").objectReferenceValue = holeDb; counters.Wired++; Debug.Log("[HoleSelectionAutoWire] OK scene:holeDatabase"); }
                else { FailB("holeDatabase", HOLE_DATABASE_PATH); }
            }

            // matchmakingModal
            {
                MatchmakingModalController modal = null;
                foreach (var m in Resources.FindObjectsOfTypeAll<MatchmakingModalController>())
                {
                    if (m.gameObject.scene.isLoaded) { modal = m; break; }
                }
                if (modal != null) { so.FindProperty("matchmakingModal").objectReferenceValue = modal; counters.Wired++; Debug.Log("[HoleSelectionAutoWire] OK scene:matchmakingModal"); }
                else { FailB("matchmakingModal", "(scene MatchmakingModalController)", "not found in scene"); }
            }

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(ctrl);

            Debug.Log("[HoleSelectionAutoWire] Part B complete — HoleSelectionScreen wired.");
        }

        // ── Part C ────────────────────────────────────────────────────────────

        private static void WireScreenManager(Counters counters)
        {
            ScreenManager sm = null;
            foreach (var s in Resources.FindObjectsOfTypeAll<ScreenManager>())
            {
                if (s.gameObject.scene.isLoaded) { sm = s; break; }
            }

            if (sm == null)
            {
                Debug.LogWarning("[HoleSelectionAutoWire] ScreenManager not found in active scene. Skipping Part C.");
                counters.Failed++;
                return;
            }

            // Find HoleSelectionScreen GO in scene
            HoleSelectionScreenController holeCtrl = null;
            foreach (var c in Resources.FindObjectsOfTypeAll<HoleSelectionScreenController>())
            {
                if (c.gameObject.scene.isLoaded) { holeCtrl = c; break; }
            }

            if (holeCtrl == null)
            {
                Debug.LogWarning("[HoleSelectionAutoWire] HoleSelectionScreenController not found in scene — cannot wire ScreenManager._holeSelectionScreen.");
                counters.Failed++;
                return;
            }

            var so = new SerializedObject(sm);
            so.FindProperty("_holeSelectionScreen").objectReferenceValue = holeCtrl.gameObject;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(sm);
            counters.Wired++;

            Debug.Log("[HoleSelectionAutoWire] OK scene:ScreenManager._holeSelectionScreen");
            Debug.Log("[HoleSelectionAutoWire] Part C complete — ScreenManager wired.");
        }
    }
}
#endif
