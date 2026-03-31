#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Golfin.Inventory.Editor
{
    /// <summary>
    /// Builds the Items screen content inside ItemsContent by cloning BallsContent:
    ///   1. Finds BallsContent in the scene (used as a structural template)
    ///   2. Duplicates its children into ItemsContent (replacing the "Placeholder" label)
    ///   3. Renames Ball → Item throughout the new hierarchy
    ///   4. Swaps BallCarouselController → ItemCarouselController
    ///   5. Swaps BallDetailPanel → ItemDetailPanel
    ///   6. Removes BallSegmentedBar + BallCompareController if present
    ///
    /// Run: GOLFIN/Build/Items Content
    /// Then run: GOLFIN/Wire/Item Detail Panel
    /// </summary>
    public static class ItemsContentBuilder
    {
        [MenuItem("GOLFIN/Build/Items Content")]
        public static void Run()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();

            // ── Find BallsContent (template) ──────────────────────────────────
            var ballsContent = FindByName(scene, "BallsContent");
            if (ballsContent == null)
            {
                EditorUtility.DisplayDialog("Build Items Content",
                    "BallsContent not found in scene.\n\nBuild the Balls screen first.", "OK");
                return;
            }

            // ── Find ItemsContent (target) ────────────────────────────────────
            var itemsContent = FindByName(scene, "ItemsContent");
            if (itemsContent == null)
            {
                EditorUtility.DisplayDialog("Build Items Content",
                    "ItemsContent not found in scene.", "OK");
                return;
            }

            // ── Confirm rebuild if children already exist ─────────────────────
            bool hasExisting = itemsContent.transform.childCount > 0;
            if (hasExisting)
            {
                bool rebuild = EditorUtility.DisplayDialog("Build Items Content",
                    "ItemsContent already has children. Rebuild it?", "Rebuild", "Cancel");
                if (!rebuild) return;

                // Destroy all existing children (Placeholder label, any prior build)
                for (int i = itemsContent.transform.childCount - 1; i >= 0; i--)
                    Object.DestroyImmediate(itemsContent.transform.GetChild(i).gameObject);
            }

            // ── Clone BallsContent children into ItemsContent ─────────────────
            // BallsContent has: BallMainSection (+ optional Rim)
            // We only want BallMainSection
            var ballMainSection = ballsContent.transform.Find("BallMainSection");
            if (ballMainSection == null)
            {
                // Fallback: clone all children
                Debug.LogWarning("[ItemsContentBuilder] BallMainSection not found — cloning all BallsContent children.");
                foreach (Transform child in ballsContent.transform)
                {
                    var clone = Object.Instantiate(child.gameObject, itemsContent.transform);
                    clone.name = child.name.Replace("Ball", "Item").Replace("Balls", "Items");
                }
            }
            else
            {
                var itemMainSection = Object.Instantiate(ballMainSection.gameObject, itemsContent.transform);
                itemMainSection.name = "ItemMainSection";
                RenameAllBallToItem(itemMainSection.transform);
            }

            // ── Swap BallCarouselController → ItemCarouselController ───────────
            var carouselGO = FindChildByName(itemsContent.transform, "ItemCarousel");
            if (carouselGO == null)
                carouselGO = FindChildByName(itemsContent.transform, "BallCarousel");
            if (carouselGO == null)
                carouselGO = FindChildByNameContains(itemsContent.transform, "Carousel");

            if (carouselGO != null)
            {
                carouselGO.name = "ItemCarousel";
                var oldCarousel = carouselGO.GetComponent<BallCarouselController>();
                if (oldCarousel != null) Object.DestroyImmediate(oldCarousel);
                if (carouselGO.GetComponent<ItemCarouselController>() == null)
                    carouselGO.AddComponent<ItemCarouselController>();
                Debug.Log("[ItemsContentBuilder] ✓ ItemCarouselController added.");
            }
            else
            {
                Debug.LogWarning("[ItemsContentBuilder] Carousel GO not found — add ItemCarouselController manually.");
            }

            // ── Swap BallDetailPanel → ItemDetailPanel ────────────────────────
            var detailGO = FindChildByName(itemsContent.transform, "ItemDetailPanel");
            if (detailGO == null)
                detailGO = FindChildByName(itemsContent.transform, "BallDetailPanel");
            if (detailGO == null)
                detailGO = FindChildByNameContains(itemsContent.transform, "DetailPanel");

            if (detailGO != null)
            {
                detailGO.name = "ItemDetailPanel";
                var oldPanel = detailGO.GetComponent<BallDetailPanel>();
                if (oldPanel != null) Object.DestroyImmediate(oldPanel);

                // Remove Ball-specific components
                foreach (var seg in detailGO.GetComponentsInChildren<BallSegmentedBar>(includeInactive: true))
                    Object.DestroyImmediate(seg);
                foreach (var compare in detailGO.GetComponentsInChildren<BallCompareController>(includeInactive: true))
                    Object.DestroyImmediate(compare);

                if (detailGO.GetComponent<ItemDetailPanel>() == null)
                    detailGO.AddComponent<ItemDetailPanel>();
                Debug.Log("[ItemsContentBuilder] ✓ ItemDetailPanel added.");
            }
            else
            {
                Debug.LogWarning("[ItemsContentBuilder] Detail panel GO not found — add ItemDetailPanel manually.");
            }

            // ── Rename remaining stat rows (not needed for items — remove them) ─
            // StatsPanel children are ball-specific stat bars; Items has no stat bars.
            // Find and remove any StatsPanel that was cloned.
            var statsPanel = FindChildByName(itemsContent.transform, "StatsPanel");
            if (statsPanel != null)
            {
                Object.DestroyImmediate(statsPanel);
                Debug.Log("[ItemsContentBuilder] ✓ StatsPanel removed (items have no stat bars).");
            }

            // ── Finish ────────────────────────────────────────────────────────
            EditorSceneManager.MarkSceneDirty(scene);

            Debug.Log("[ItemsContentBuilder] ✓ Items Content built.");
            EditorUtility.DisplayDialog("Build Items Content",
                "Items Content built successfully!\n\n" +
                "Next steps:\n" +
                "1. Run GOLFIN/Wire/Item Detail Panel\n" +
                "2. Assign ItemThumbnailCard.prefab → ItemCarouselController.itemCardPrefab\n" +
                "3. Assign BallThumbnailEmptyCard.prefab → ItemCarouselController.itemEmptyCardPrefab\n" +
                "4. Save the scene (Ctrl+S)",
                "OK");
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        /// <summary>Recursively renames all "Ball" and "Balls" occurrences to "Item"/"Items" in child names.</summary>
        private static void RenameAllBallToItem(Transform t)
        {
            foreach (Transform child in t)
                RenameAllBallToItem(child);

            string newName = t.name
                .Replace("BallDetail", "ItemDetail")
                .Replace("BallCarousel", "ItemCarousel")
                .Replace("BallMain", "ItemMain")
                .Replace("Balls", "Items")
                .Replace("Ball", "Item");

            if (newName != t.name)
            {
                Debug.Log($"[ItemsContentBuilder] Renamed '{t.name}' → '{newName}'");
                t.name = newName;
            }
        }

        private static GameObject? FindByName(UnityEngine.SceneManagement.Scene scene, string name)
        {
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

        private static GameObject? FindChildByName(Transform root, string name)
        {
            var t = FindTransformByName(root, name);
            return t != null ? t.gameObject : null;
        }

        private static GameObject? FindChildByNameContains(Transform root, string substring)
        {
            foreach (Transform child in root)
            {
                if (child.name.Contains(substring)) return child.gameObject;
                var result = FindChildByNameContains(child, substring);
                if (result != null) return result;
            }
            return null;
        }
    }
}
#endif
