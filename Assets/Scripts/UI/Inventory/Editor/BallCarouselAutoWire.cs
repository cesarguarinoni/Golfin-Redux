#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Golfin.Inventory.Editor
{
    /// <summary>
    /// Wires BallCarouselSection in the scene:
    /// 1. Removes ClubCarouselController (if present), adds BallCarouselController
    /// 2. Wires contentParent, arrows, paginationDots
    /// 3. Assigns BallThumbnailCard prefab from Assets/Prefabs/UI/Inventory/
    ///
    /// Run: GOLFIN/Wire/Ball Carousel
    /// </summary>
    public static class BallCarouselAutoWire
    {
        private const string PREFAB_PATH       = "Assets/Prefabs/UI/Inventory/BallThumbnailCard.prefab";
        private const string EMPTY_PREFAB_PATH = "Assets/Prefabs/UI/Inventory/BallThumbnailEmptyCard.prefab";

        [MenuItem("GOLFIN/Wire/Ball Carousel")]
        public static void Run()
        {
            // ── Find BallCarouselSection (inactive-safe) ──────────────────────
            GameObject? sectionGO = null;
            foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (go.name == "BallCarouselSection" && go.scene.isLoaded)
                { sectionGO = go; break; }
            }

            if (sectionGO == null)
            {
                EditorUtility.DisplayDialog("Wire Ball Carousel",
                    "BallCarouselSection not found in scene.\n\n" +
                    "Clone ClubCarouselSection, place it under BallMainSection, and rename it BallCarouselSection.", "OK");
                return;
            }

            var root = sectionGO.transform;

            // ── Swap component ────────────────────────────────────────────────
            var clubCtrl = sectionGO.GetComponent<ClubCarouselController>();
            if (clubCtrl != null)
            {
                Object.DestroyImmediate(clubCtrl);
                Debug.Log("[BallCarouselAutoWire] ✓ Removed ClubCarouselController.");
            }

            var ctrl = sectionGO.GetComponent<BallCarouselController>();
            if (ctrl == null)
            {
                ctrl = sectionGO.AddComponent<BallCarouselController>();
                Debug.Log("[BallCarouselAutoWire] ✓ Added BallCarouselController.");
            }

            // ── Wire fields ───────────────────────────────────────────────────
            var so     = new SerializedObject(ctrl);
            int wired  = 0;
            int failed = 0;

            void Wire(string propName, Object? obj, string label)
            {
                if (obj == null) { Debug.LogWarning($"[BallCarouselAutoWire] NOT FOUND: {label}"); failed++; return; }
                so.FindProperty(propName).objectReferenceValue = obj;
                Debug.Log($"[BallCarouselAutoWire] ✓ {propName}");
                wired++;
            }

            // contentParent = ScrollView/Viewport/Content
            Wire("contentParent", root.Find("ScrollView/Viewport/Content"), "ScrollView/Viewport/Content");

            // arrows
            Wire("leftArrowButton",  root.Find("LeftArrow")?.GetComponent<Button>(),  "LeftArrow Button");
            Wire("rightArrowButton", root.Find("RightArrow")?.GetComponent<Button>(), "RightArrow Button");

            // pagination
            Wire("paginationDotsParent", root.Find("PaginationDots"), "PaginationDots");

            // prefabs
            var prefab      = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_PATH);
            var emptyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(EMPTY_PREFAB_PATH);
            Wire("ballCardPrefab",      prefab,      $"prefab at {PREFAB_PATH}");
            Wire("ballEmptyCardPrefab", emptyPrefab, $"prefab at {EMPTY_PREFAB_PATH}");

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(ctrl);
            EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());

            string msg = $"{wired} fields wired, {failed} failed.\n\n";
            if (failed > 0)
                msg += "Check Console — fix missing paths manually in Inspector.";
            else
                msg += "All fields wired! Hit Play and switch to the BALLS tab to test.";

            Debug.Log($"[BallCarouselAutoWire] Done — {wired} wired, {failed} failed.");
            EditorUtility.DisplayDialog("Wire Ball Carousel", msg, "OK");
        }
    }
}
#endif
