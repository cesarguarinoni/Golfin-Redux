#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Golfin.Roster.Editor
{
    /// <summary>
    /// Builds and wires status icon GameObjects for:
    ///   1. Detail Panel  — IconSelectedBig + IconLevelUpBig in RightPanel/CharacterNamePanel
    ///   2. Card Prefab   — IconSelectedSmall + IconLevelUpSmall + IconStaminaSmall per card
    ///   3. Compare Panel — IconSelectedBig + IconLevelUpBig in CompareInfoPanel/CharacterNamePanel
    ///
    /// Run: GOLFIN > Build Status Icons (All)
    /// Or run each step separately.
    /// </summary>
    public static class StatusIconBuilder
    {
        private const string ART_PATH    = "Assets/Art/Roster Screen/";
        private const string PREFAB_PATH = "Assets/Prefabs/UI/Roster/CharacterThumbnailCardGlowUp.prefab";

        // ── Menu items ────────────────────────────────────────────────────────

        [MenuItem("GOLFIN/Build Status Icons (All)")]
        public static void BuildAll()
        {
            int total = 0;
            total += BuildDetailPanelIcons()  ? 1 : 0;
            total += BuildCardPrefabIcons()   ? 1 : 0;
            total += BuildComparePanelIcons() ? 1 : 0;

            EditorUtility.DisplayDialog("Status Icons",
                total == 3
                    ? "All three steps completed successfully."
                    : $"{total}/3 steps succeeded. Check the Console for details.",
                "OK");
        }

        [MenuItem("GOLFIN/Build Status Icons — 1. Detail Panel")]
        public static void MenuDetailPanel()  => BuildDetailPanelIcons();

        [MenuItem("GOLFIN/Build Status Icons — 2. Card Prefab")]
        public static void MenuCardPrefab()   => BuildCardPrefabIcons();

        [MenuItem("GOLFIN/Build Status Icons — 3. Compare Panel")]
        public static void MenuComparePanel() => BuildComparePanelIcons();

        // ── 1. Detail Panel ───────────────────────────────────────────────────

        private static bool BuildDetailPanelIcons()
        {
            var detailPanel = FindDetailPanel();
            if (detailPanel == null) { Debug.LogError("[StatusIconBuilder] DetailPanel not found."); return false; }

            var panel = detailPanel.GetComponent<CharacterDetailPanel>();
            if (panel == null) { Debug.LogError("[StatusIconBuilder] CharacterDetailPanel component not found."); return false; }

            // Parent: RightPanel/CharacterNamePanel
            var namePanel = detailPanel.Find("RightPanel/CharacterNamePanel");
            if (namePanel == null) { Debug.LogError("[StatusIconBuilder] RightPanel/CharacterNamePanel not found."); return false; }

            // Create / replace icon row
            var row = GetOrCreateIconRow(namePanel, "StatusIconsRow", topRight: true, iconSize: 28f, spacing: 4f);

            var selectedGO  = GetOrCreateIcon(row, "IconSelectedBig",  ART_PATH + "IconSelectedBig.png",  28f);
            var levelUpGO   = GetOrCreateIcon(row, "IconLevelUpBig",   ART_PATH + "IconLevelUpBig.png",   28f);

            // Wire to CharacterDetailPanel
            var so = new SerializedObject(panel);
            so.FindProperty("selectedIcon")    .objectReferenceValue = selectedGO;
            so.FindProperty("levelUpReadyIcon").objectReferenceValue = levelUpGO;
            so.ApplyModifiedProperties();

            EditorUtility.SetDirty(panel);
            EditorSceneManager.MarkSceneDirty(detailPanel.gameObject.scene);

            Debug.Log("[StatusIconBuilder] ✓ Detail Panel icons created and wired.");
            return true;
        }

        // ── 2. Card Prefab ────────────────────────────────────────────────────

        private static bool BuildCardPrefabIcons()
        {
            // LoadPrefabContents gives us an editable root — changes are saved back via SaveAsPrefabAsset
            var prefabRoot = PrefabUtility.LoadPrefabContents(PREFAB_PATH);
            if (prefabRoot == null)
            {
                Debug.LogError($"[StatusIconBuilder] Could not load prefab at {PREFAB_PATH}");
                return false;
            }

            var card = prefabRoot.GetComponent<CharacterThumbnailCard>();
            if (card == null)
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
                Debug.LogError("[StatusIconBuilder] CharacterThumbnailCard component not found on prefab root.");
                return false;
            }

            // Icon row at the bottom-center of the card
            var row = GetOrCreateIconRow(prefabRoot.transform, "StatusIconsRow",
                                         topRight: false, iconSize: 20f, spacing: 4f);

            // Anchor the row to the bottom-center of the card
            var rowRT = row.GetComponent<RectTransform>();
            rowRT.anchorMin        = new Vector2(0.5f, 0f);
            rowRT.anchorMax        = new Vector2(0.5f, 0f);
            rowRT.pivot            = new Vector2(0.5f, 0f);
            rowRT.anchoredPosition = new Vector2(0f, 6f);    // 6px above the bottom edge
            rowRT.sizeDelta        = new Vector2(80f, 20f);  // wide enough for 3 × 20px icons + gaps

            var selectedGO  = GetOrCreateIcon(row, "IconSelectedSmall",  ART_PATH + "IconSelectedSmall.png",  20f);
            var levelUpGO   = GetOrCreateIcon(row, "IconLevelUpSmall",   ART_PATH + "IconLevelUpSmall.png",   20f);
            var staminaGO   = GetOrCreateIcon(row, "IconStaminaSmall",   ART_PATH + "IconStaminaSmall.png",   20f);

            // Wire fields
            var so = new SerializedObject(card);
            so.FindProperty("selectedIcon")    .objectReferenceValue = selectedGO;
            so.FindProperty("levelUpReadyIcon").objectReferenceValue = levelUpGO;
            so.FindProperty("staminaIcon")     .objectReferenceValue = staminaGO;
            so.ApplyModifiedProperties();

            PrefabUtility.SaveAsPrefabAsset(prefabRoot, PREFAB_PATH);
            PrefabUtility.UnloadPrefabContents(prefabRoot);

            AssetDatabase.SaveAssets();
            Debug.Log("[StatusIconBuilder] ✓ Card prefab icons created and wired.");
            return true;
        }

        // ── 3. Compare Panel ──────────────────────────────────────────────────

        private static bool BuildComparePanelIcons()
        {
            var detailPanel = FindDetailPanel();
            if (detailPanel == null) { Debug.LogError("[StatusIconBuilder] DetailPanel not found."); return false; }

            var controller = detailPanel.GetComponent<CompareController>();
            if (controller == null) { Debug.LogError("[StatusIconBuilder] CompareController component not found."); return false; }

            // Parent: CompareRightPanel/CompareInfoPanel/CharacterNamePanel
            var namePanel = detailPanel.Find("CompareRightPanel/CompareInfoPanel/CharacterNamePanel");
            if (namePanel == null)
            {
                Debug.LogError("[StatusIconBuilder] CompareRightPanel/CompareInfoPanel/CharacterNamePanel not found.");
                return false;
            }

            var row = GetOrCreateIconRow(namePanel, "StatusIconsRow", topRight: true, iconSize: 28f, spacing: 4f);

            var selectedGO  = GetOrCreateIcon(row, "IconSelectedBig",  ART_PATH + "IconSelectedBig.png",  28f);
            var levelUpGO   = GetOrCreateIcon(row, "IconLevelUpBig",   ART_PATH + "IconLevelUpBig.png",   28f);

            // Wire to CompareController
            var so = new SerializedObject(controller);
            so.FindProperty("compareSelectedIcon")    .objectReferenceValue = selectedGO;
            so.FindProperty("compareLevelUpReadyIcon").objectReferenceValue = levelUpGO;
            so.ApplyModifiedProperties();

            EditorUtility.SetDirty(controller);
            EditorSceneManager.MarkSceneDirty(detailPanel.gameObject.scene);

            Debug.Log("[StatusIconBuilder] ✓ Compare Panel icons created and wired.");
            return true;
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        /// <summary>
        /// Gets or creates a named HorizontalLayoutGroup row inside <paramref name="parent"/>.
        /// <paramref name="topRight"/> positions it in the top-right corner of the parent.
        /// </summary>
        private static Transform GetOrCreateIconRow(Transform parent, string rowName,
                                                     bool topRight, float iconSize, float spacing)
        {
            var existing = parent.Find(rowName);
            if (existing != null) return existing;

            var rowGO = new GameObject(rowName, typeof(RectTransform));
            rowGO.transform.SetParent(parent, false);

            var rt = rowGO.GetComponent<RectTransform>();
            if (topRight)
            {
                rt.anchorMin        = new Vector2(1f, 1f);
                rt.anchorMax        = new Vector2(1f, 1f);
                rt.pivot            = new Vector2(1f, 1f);
                rt.anchoredPosition = new Vector2(-2f, -2f); // 2px inset from top-right corner
                rt.sizeDelta        = new Vector2(iconSize * 2 + spacing + 4f, iconSize);
            }

            var layout = rowGO.AddComponent<HorizontalLayoutGroup>();
            layout.spacing              = spacing;
            layout.childAlignment       = TextAnchor.MiddleCenter;
            layout.childControlWidth    = false;
            layout.childControlHeight   = false;
            layout.childForceExpandWidth  = false;
            layout.childForceExpandHeight = false;

            return rowGO.transform;
        }

        /// <summary>
        /// Gets or creates a named icon Image inside <paramref name="parent"/>.
        /// Starts hidden — runtime code controls visibility.
        /// </summary>
        private static GameObject GetOrCreateIcon(Transform parent, string iconName,
                                                   string spritePath, float size)
        {
            // Reuse if already present
            var existing = parent.Find(iconName);
            if (existing != null) return existing.gameObject;

            var go = new GameObject(iconName, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);

            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(size, size);

            var img    = go.GetComponent<Image>();
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            if (sprite != null)
                img.sprite = sprite;
            else
                Debug.LogWarning($"[StatusIconBuilder] Sprite not found at '{spritePath}' — assign manually.");

            img.preserveAspect = true;
            img.raycastTarget  = false; // icons are display-only

            // Hidden by default; runtime logic calls SetActive(true/false)
            go.SetActive(false);

            return go;
        }

        private static Transform FindDetailPanel()
        {
            var go = GameObject.Find("DetailPanel");
            return go != null ? go.transform : null;
        }
    }
}
#endif
