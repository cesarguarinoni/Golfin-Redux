#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Golfin.Inventory.Editor
{
    /// <summary>
    /// Clones BallThumbnailCard.prefab as ItemThumbnailCard.prefab,
    /// replaces the BallThumbnailCard component with ItemThumbnailCard.
    ///
    /// Run: GOLFIN/Build/Item Thumbnail Card
    /// Safe to re-run — overwrites the existing ItemThumbnailCard.prefab.
    /// </summary>
    public static class ItemThumbnailCardBuilder
    {
        private const string SOURCE_PREFAB = "Assets/Prefabs/UI/Inventory/BallThumbnailCard.prefab";
        private const string DEST_PREFAB   = "Assets/Prefabs/UI/Inventory/ItemThumbnailCard.prefab";
        private const string DEST_FOLDER   = "Assets/Prefabs/UI/Inventory";

        [MenuItem("GOLFIN/Build/Item Thumbnail Card")]
        public static void Run()
        {
            // ── 1. Check source prefab exists ─────────────────────────────────
            var sourcePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SOURCE_PREFAB);
            if (sourcePrefab == null)
            {
                EditorUtility.DisplayDialog("Item Thumbnail Card Builder",
                    $"Source prefab not found:\n{SOURCE_PREFAB}\n\nBuild the Ball Thumbnail Card first.", "OK");
                return;
            }

            // ── 2. Ensure destination folder exists ───────────────────────────
            if (!AssetDatabase.IsValidFolder(DEST_FOLDER))
                AssetDatabase.CreateFolder("Assets/Prefabs/UI", "Inventory");

            // ── 3. Copy the prefab ────────────────────────────────────────────
            AssetDatabase.CopyAsset(SOURCE_PREFAB, DEST_PREFAB);
            AssetDatabase.SaveAssets();

            var destPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DEST_PREFAB);
            if (destPrefab == null)
            {
                EditorUtility.DisplayDialog("Item Thumbnail Card Builder",
                    $"Failed to create prefab at:\n{DEST_PREFAB}", "OK");
                return;
            }

            // ── 4. Open prefab for editing ────────────────────────────────────
            var prefabStage = UnityEditor.SceneManagement.PrefabStageUtility.OpenPrefab(DEST_PREFAB);
            if (prefabStage == null)
            {
                Debug.LogWarning("[ItemThumbnailCardBuilder] Could not open prefab stage — proceeding with direct edit.");
            }

            var root = destPrefab;
            using (var editScope = new PrefabUtility.EditPrefabContentsScope(DEST_PREFAB))
            {
                var prefabRoot = editScope.prefabContentsRoot;

                // ── 5. Remove BallThumbnailCard, add ItemThumbnailCard ─────────
                var oldComp = prefabRoot.GetComponent<BallThumbnailCard>();
                if (oldComp != null)
                    Object.DestroyImmediate(oldComp);

                var newComp = prefabRoot.AddComponent<ItemThumbnailCard>();

                // ── 6. Wire SerializeFields by child name ─────────────────────
                var so = new SerializedObject(newComp);

                WireImage (so, prefabRoot, "portraitImage",      "Portrait");
                WireTMP   (so, prefabRoot, "nameText",           "NameText");
                WireTMP   (so, prefabRoot, "quantityText",       "QuantityText");
                WireTMP   (so, prefabRoot, "rarityBadgeText",    "RarityBadge");
                WireImage (so, prefabRoot, "selectionHighlight", "SelectionHighlight");
                WireImage (so, prefabRoot, "backgroundImage",    "Background");
                WireButton(so, prefabRoot, "cardButton",         "");  // Button on root

                so.ApplyModifiedProperties();
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[ItemThumbnailCardBuilder] ItemThumbnailCard.prefab created at {DEST_PREFAB}");
            EditorUtility.DisplayDialog("Item Thumbnail Card Builder",
                $"ItemThumbnailCard.prefab created successfully.\n\n" +
                "Assign it to ItemCarouselController.itemCardPrefab in the Inspector,\n" +
                "or run GOLFIN/Wire/Item Detail Panel to auto-wire.", "OK");
        }

        private static void WireImage(SerializedObject so, GameObject root, string prop, string childName)
        {
            Image? cmp;
            if (string.IsNullOrEmpty(childName))
            {
                cmp = root.GetComponent<Image>();
            }
            else
            {
                var t = root.transform.Find(childName);
                cmp = t != null ? t.GetComponent<Image>() : null;
            }

            if (cmp != null)
            {
                so.FindProperty(prop).objectReferenceValue = cmp;
                Debug.Log($"[ItemThumbnailCardBuilder] ✓ {prop}");
            }
            else
            {
                Debug.LogWarning($"[ItemThumbnailCardBuilder] '{prop}' — child '{childName}' not found. Wire manually.");
            }
        }

        private static void WireTMP(SerializedObject so, GameObject root, string prop, string childName)
        {
            var t   = root.transform.Find(childName);
            var cmp = t != null ? t.GetComponent<TextMeshProUGUI>() : null;

            if (cmp != null)
            {
                so.FindProperty(prop).objectReferenceValue = cmp;
                Debug.Log($"[ItemThumbnailCardBuilder] ✓ {prop}");
            }
            else
            {
                Debug.LogWarning($"[ItemThumbnailCardBuilder] '{prop}' — child '{childName}' not found. Wire manually.");
            }
        }

        private static void WireButton(SerializedObject so, GameObject root, string prop, string childName)
        {
            Button? cmp;
            if (string.IsNullOrEmpty(childName))
            {
                cmp = root.GetComponent<Button>();
            }
            else
            {
                var t = root.transform.Find(childName);
                cmp = t != null ? t.GetComponent<Button>() : null;
            }

            if (cmp != null)
            {
                so.FindProperty(prop).objectReferenceValue = cmp;
                Debug.Log($"[ItemThumbnailCardBuilder] ✓ {prop}");
            }
            else
            {
                Debug.LogWarning($"[ItemThumbnailCardBuilder] '{prop}' — child '{childName}' not found. Wire manually.");
            }
        }
    }
}
#endif
