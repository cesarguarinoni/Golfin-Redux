#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Golfin.Inventory.Editor
{
    /// <summary>
    /// Auto-wires all Phase J components in one pass:
    ///   1. BagCarouselController — contentParent, arrows, pagination, prefabs, detailPanel
    ///   2. BagDetailPanel       — info area, club grid, equip button, prefabs
    ///   3. InventoryScreenController.tabPanels[1] = BagsContent
    ///
    /// Expected hierarchy (Cesar builds this by hand):
    ///
    ///   BagsContent                          ← root GO (must exist by name)
    ///   ├── BagCarousel                      ← BagCarouselController component here
    ///   │   ├── LeftArrow                    (Button)
    ///   │   ├── RightArrow                   (Button)
    ///   │   ├── PaginationDots               (Transform)
    ///   │   └── ScrollView/Viewport/Content  (Transform — contentParent)
    ///   ├── BagDetailPanel                   ← BagDetailPanel component here
    ///   │   ├── InfoArea
    ///   │   │   ├── BagFullImage             (Image)
    ///   │   │   ├── BagNameText              (TMP)
    ///   │   │   ├── EquippedIcon             (GO)
    ///   │   │   └── DescriptionText          (TMP)
    ///   │   └── ClubGrid                     (Transform — GridLayoutGroup)
    ///   └── EquipBagButton                   (Button)
    ///       └── Text                         (TMP)
    ///
    /// Prefabs expected at:
    ///   Assets/Prefabs/UI/Inventory/BagThumbnailCard.prefab
    ///   Assets/Prefabs/UI/Inventory/BagSlotLockedPrefab.prefab   (or BagSlotLocked.prefab)
    ///   Assets/Prefabs/UI/Inventory/BagSwapClubCard.prefab
    ///   Assets/Prefabs/UI/Inventory/BagEmptyClubCard.prefab
    ///
    /// Run: GOLFIN/Wire/Bags Content
    /// </summary>
    public static class BagsContentAutoWire
    {
        private const string PREFAB_BAG_CARD    = "Assets/Prefabs/UI/Inventory/BagThumbnailCard.prefab";
        private const string PREFAB_LOCKED_A    = "Assets/Prefabs/UI/Inventory/BagSlotLockedPrefab.prefab";
        private const string PREFAB_LOCKED_B    = "Assets/Prefabs/UI/Inventory/BagSlotLocked.prefab"; // alt name
        private const string PREFAB_SWAP_CARD   = "Assets/Prefabs/UI/Inventory/BagSwapClubCard.prefab";
        private const string PREFAB_EMPTY_CARD  = "Assets/Prefabs/UI/Inventory/BagEmptyClubCard.prefab";

        [MenuItem("GOLFIN/Wire/Bags Content")]
        public static void Run()
        {
            int wired = 0, failed = 0;

            // ── 1. Find BagsContent root ──────────────────────────────────────
            GameObject? bagsContent = null;
            foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (go.name == "BagsContent" && go.scene.isLoaded) { bagsContent = go; break; }
            }
            if (bagsContent == null)
            {
                EditorUtility.DisplayDialog("Bags Content Auto-Wire",
                    "BagsContent GO not found in scene.\n\nCreate it first (child of InventoryScreen content area).", "OK");
                return;
            }
            var bagsRoot = bagsContent.transform;

            // ── 2. Find BagCarouselController ─────────────────────────────────
            BagCarouselController? carousel = bagsContent.GetComponentInChildren<BagCarouselController>(true);
            if (carousel == null)
            {
                Debug.LogWarning("[BagsContentAutoWire] BagCarouselController not found under BagsContent — add it to BagCarousel GO.");
                failed++;
            }
            else
            {
                var cso      = new SerializedObject(carousel);
                var cRoot    = carousel.transform;

                void WireCarousel(string prop, Object? obj, string label)
                {
                    if (obj == null) { Debug.LogWarning($"[BagsContentAutoWire] NOT FOUND: {label}"); failed++; return; }
                    cso.FindProperty(prop).objectReferenceValue = obj;
                    Debug.Log($"[BagsContentAutoWire] ✓ carousel.{prop}"); wired++;
                }

                // contentParent — try ScrollView/Viewport/Content then ScrollRect child
                Transform? contentParent = cRoot.Find("ScrollView/Viewport/Content");
                if (contentParent == null)
                {
                    var sr = carousel.GetComponentInChildren<ScrollRect>(true);
                    if (sr?.content != null) contentParent = sr.content;
                }
                WireCarousel("contentParent", contentParent, "ScrollView/Viewport/Content");

                // Arrows
                WireCarousel("leftArrowButton",  cRoot.Find("LeftArrow")?.GetComponent<Button>(),  "LeftArrow Button");
                WireCarousel("rightArrowButton", cRoot.Find("RightArrow")?.GetComponent<Button>(), "RightArrow Button");

                // Pagination
                WireCarousel("paginationDotsParent", cRoot.Find("PaginationDots"), "PaginationDots");

                // Prefabs
                var bagCardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_BAG_CARD);
                WireCarousel("bagCardPrefab", bagCardPrefab, PREFAB_BAG_CARD);

                var lockedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_LOCKED_A)
                                ?? AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_LOCKED_B);
                WireCarousel("bagLockedCardPrefab", lockedPrefab, "BagSlotLocked prefab");

                cso.ApplyModifiedProperties();
                EditorUtility.SetDirty(carousel);
            }

            // ── 3. Find BagDetailPanel ────────────────────────────────────────
            BagDetailPanel? detailPanel = bagsContent.GetComponentInChildren<BagDetailPanel>(true);
            if (detailPanel == null)
            {
                Debug.LogWarning("[BagsContentAutoWire] BagDetailPanel not found under BagsContent — add component to BagDetailPanel GO.");
                failed++;
            }
            else
            {
                var dso   = new SerializedObject(detailPanel);
                var dRoot = detailPanel.transform;

                void WireDetail(string prop, Object? obj, string label)
                {
                    if (obj == null) { Debug.LogWarning($"[BagsContentAutoWire] NOT FOUND: {label}"); failed++; return; }
                    dso.FindProperty(prop).objectReferenceValue = obj;
                    Debug.Log($"[BagsContentAutoWire] ✓ detailPanel.{prop}"); wired++;
                }

                // Info area
                WireDetail("bagFullImage",    dRoot.Find("InfoArea/BagFullImage")?.GetComponent<Image>(),           "InfoArea/BagFullImage");
                WireDetail("bagNameText",     dRoot.Find("InfoArea/BagNameText")?.GetComponent<TextMeshProUGUI>(),  "InfoArea/BagNameText");
                WireDetail("equippedIcon",    dRoot.Find("InfoArea/EquippedIcon")?.gameObject,                       "InfoArea/EquippedIcon");
                WireDetail("descriptionText", dRoot.Find("InfoArea/DescriptionText")?.GetComponent<TextMeshProUGUI>(), "InfoArea/DescriptionText");

                // Club grid
                WireDetail("clubGridParent", dRoot.Find("ClubGrid"), "ClubGrid");

                // Club card prefabs
                var swapCardPrefab  = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_SWAP_CARD);
                var emptyCardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_EMPTY_CARD);
                WireDetail("clubCardPrefab",      swapCardPrefab,  PREFAB_SWAP_CARD);
                WireDetail("emptyClubCardPrefab", emptyCardPrefab, PREFAB_EMPTY_CARD);

                // EquipBagButton — direct child of BagsContent (sibling of BagDetailPanel)
                var equipBtnT = bagsRoot.Find("EquipBagButton");
                WireDetail("equipBagButton", equipBtnT?.GetComponent<Button>(), "EquipBagButton");
                WireDetail("equipBagText",   equipBtnT?.Find("Text")?.GetComponent<TextMeshProUGUI>(), "EquipBagButton/Text");

                // BagClubModal — find in scene (may not exist yet — soft fail)
                BagClubModalController? modal = null;
                foreach (var m in Resources.FindObjectsOfTypeAll<BagClubModalController>())
                    if (m.gameObject.scene.isLoaded) { modal = m; break; }

                if (modal != null)
                {
                    dso.FindProperty("clubModal").objectReferenceValue = modal;
                    Debug.Log("[BagsContentAutoWire] ✓ detailPanel.clubModal"); wired++;
                }
                else
                {
                    Debug.LogWarning("[BagsContentAutoWire] BagClubModalController not found — run GOLFIN/Wire/Bag Club Modal after building the modal.");
                    // Not counted as a failure — modal may not be built yet
                }

                dso.ApplyModifiedProperties();
                EditorUtility.SetDirty(detailPanel);

                // ── Wire carousel.detailPanel ─────────────────────────────────
                if (carousel != null)
                {
                    var cso2 = new SerializedObject(carousel);
                    cso2.FindProperty("detailPanel").objectReferenceValue = detailPanel;
                    cso2.ApplyModifiedProperties();
                    EditorUtility.SetDirty(carousel);
                    Debug.Log("[BagsContentAutoWire] ✓ carousel.detailPanel"); wired++;
                }
            }

            // ── 4. Wire InventoryScreenController.tabPanels[1] ───────────────
            InventoryScreenController? invCtrl = null;
            foreach (var c in Resources.FindObjectsOfTypeAll<InventoryScreenController>())
                if (c.gameObject.scene.isLoaded) { invCtrl = c; break; }

            if (invCtrl != null)
            {
                var iso = new SerializedObject(invCtrl);
                var tabPanelsProp = iso.FindProperty("tabPanels");

                // Ensure array is at least 2 elements
                if (tabPanelsProp.arraySize < 2)
                    tabPanelsProp.arraySize = 2;

                tabPanelsProp.GetArrayElementAtIndex(1).objectReferenceValue = bagsContent;
                iso.ApplyModifiedProperties();
                EditorUtility.SetDirty(invCtrl);
                Debug.Log("[BagsContentAutoWire] ✓ InventoryScreenController.tabPanels[1] = BagsContent"); wired++;
            }
            else
            {
                Debug.LogWarning("[BagsContentAutoWire] InventoryScreenController not found — wire tabPanels[1] manually.");
                failed++;
            }

            // ── Done ──────────────────────────────────────────────────────────
            EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());

            string msg = $"{wired} fields wired, {failed} failed.\n\n" +
                (failed > 0
                    ? "Check Console for missing paths.\nFix the hierarchy, then re-run."
                    : "All fields wired!\n\nNext: run GOLFIN/Wire/Bag Club Modal after building the modal.\nThen save the scene (Ctrl+S).");

            Debug.Log($"[BagsContentAutoWire] Done — {wired} wired, {failed} failed.");
            EditorUtility.DisplayDialog("Bags Content Auto-Wire", msg, "OK");
        }
    }
}
#endif
