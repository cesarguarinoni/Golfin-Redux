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

            var rightPanel = detailPanel.Find("RightPanel");
            if (rightPanel == null) { Debug.LogError("[StatusIconBuilder] RightPanel not found."); return false; }

            // Clean up any old row that may have been created inside CharacterNamePanel
            var oldRow = detailPanel.Find("RightPanel/CharacterNamePanel/StatusIconsRow");
            if (oldRow != null) Object.DestroyImmediate(oldRow.gameObject);

            // Create row as direct child of RightPanel so layout groups can't disturb it.
            // CharacterNamePanel: center y = -100 from RightPanel top, height 120.
            // To vertically centre 28px icons on it: top of row = -100 + 14 = -86.
            var row = GetOrCreateIconRow(rightPanel, "StatusIconsRow", iconSize: 28f, spacing: 6f,
                                         anchorMin: new Vector2(1f, 1f),
                                         anchorMax: new Vector2(1f, 1f),
                                         pivot:     new Vector2(1f, 1f),
                                         anchoredPosition: new Vector2(-8f, -86f));

            var selectedGO = GetOrCreateIcon(row, "IconSelectedBig", ART_PATH + "IconSelectedBig.png", 28f);
            var levelUpGO  = GetOrCreateIcon(row, "IconLevelUpBig",  ART_PATH + "IconLevelUpBig.png",  28f);

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

            // Icon row — bottom-centre of card, 6px above the bottom edge
            var row = GetOrCreateIconRow(prefabRoot.transform, "StatusIconsRow",
                                         iconSize: 20f, spacing: 4f,
                                         anchorMin:        new Vector2(0.5f, 0f),
                                         anchorMax:        new Vector2(0.5f, 0f),
                                         pivot:            new Vector2(0.5f, 0f),
                                         anchoredPosition: new Vector2(0f, 6f));

            // Widen the row to fit 3 icons (3×20 + 2×4 gap = 68px)
            var rowRT = row.GetComponent<RectTransform>();
            rowRT.sizeDelta = new Vector2(68f, 20f);

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
            // Mirror the RightPanel structure exactly — icons live inside CharacterNamePanel.
            var infoPanel = detailPanel.Find("CompareRightPanel/CompareInfoPanel");
            if (infoPanel == null)
            {
                Debug.LogError("[StatusIconBuilder] CompareRightPanel/CompareInfoPanel not found.");
                return false;
            }

            // Clean up any stale rows that landed in the wrong place
            var staleRoot     = infoPanel.Find("StatusIconsRow");
            var staleNamePane = infoPanel.Find("CharacterNamePanel/StatusIconsRow");
            if (staleRoot     != null) Object.DestroyImmediate(staleRoot.gameObject);
            if (staleNamePane != null) Object.DestroyImmediate(staleNamePane.gameObject);

            var namePanel = infoPanel.Find("CharacterNamePanel");
            if (namePanel == null)
            {
                Debug.LogError("[StatusIconBuilder] CompareInfoPanel/CharacterNamePanel not found.");
                return false;
            }

            // Place row inside CharacterNamePanel — same parent as the working RightPanel row.
            // Add ignoreLayout so the parent VLG cannot override the position.
            // CharacterNamePanel is 489×120; anchor top-right, 8px inset.
            var row = GetOrCreateIconRow(namePanel, "StatusIconsRow", iconSize: 28f, spacing: 6f,
                                         anchorMin:        new Vector2(1f, 1f),
                                         anchorMax:        new Vector2(1f, 1f),
                                         pivot:            new Vector2(1f, 1f),
                                         anchoredPosition: new Vector2(-8f, -8f));

            // ignoreLayout frees the row from the VerticalLayoutGroup on CharacterNamePanel
            var le = row.gameObject.GetComponent<LayoutElement>()
                  ?? row.gameObject.AddComponent<LayoutElement>();
            le.ignoreLayout = true;

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
        /// Explicit anchor, pivot and position so the caller controls exact placement.
        /// </summary>
        private static Transform GetOrCreateIconRow(Transform parent, string rowName,
                                                     float iconSize, float spacing,
                                                     Vector2 anchorMin, Vector2 anchorMax,
                                                     Vector2 pivot,     Vector2 anchoredPosition)
        {
            var existing = parent.Find(rowName);
            if (existing != null) return existing;

            var rowGO = new GameObject(rowName, typeof(RectTransform));
            rowGO.transform.SetParent(parent, false);

            var rt = rowGO.GetComponent<RectTransform>();
            rt.anchorMin        = anchorMin;
            rt.anchorMax        = anchorMax;
            rt.pivot            = pivot;
            rt.anchoredPosition = anchoredPosition;
            rt.sizeDelta        = new Vector2(iconSize * 2 + spacing + 4f, iconSize);

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
