#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Golfin.Inventory.Editor
{
    /// <summary>
    /// Fixes BallThumbnailCard.prefab:
    ///   1. Removes ClubThumbnailCard component (was cloned from Club prefab)
    ///   2. Adds BallThumbnailCard component
    ///   3. Wires all serialized fields from the known hierarchy
    ///
    /// Run: GOLFIN/Fix/Ball Thumbnail Card Prefab
    /// </summary>
    public static class BallThumbnailCardFix
    {
        private const string PREFAB_PATH = "Assets/Prefabs/UI/Inventory/BallThumbnailCard.prefab";

        [MenuItem("GOLFIN/Fix/Ball Thumbnail Card Prefab")]
        public static void Run()
        {
            var prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_PATH);
            if (prefabAsset == null)
            {
                EditorUtility.DisplayDialog("Fix Ball Card",
                    $"Prefab not found at:\n{PREFAB_PATH}", "OK");
                return;
            }

            // Open prefab for editing
            var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
            string path = AssetDatabase.GetAssetPath(prefabAsset);
            var prefabRoot = PrefabUtility.LoadPrefabContents(path);

            try
            {
                var root = prefabRoot.transform;
                int wired = 0, failed = 0;

                // ── 1. Swap ClubThumbnailCard → BallThumbnailCard ─────────────
                var clubComp = prefabRoot.GetComponent<ClubThumbnailCard>();
                if (clubComp != null)
                {
                    Object.DestroyImmediate(clubComp);
                    Debug.Log("[BallThumbnailCardFix] ✓ Removed ClubThumbnailCard.");
                }

                var ballComp = prefabRoot.GetComponent<BallThumbnailCard>();
                if (ballComp == null)
                {
                    ballComp = prefabRoot.AddComponent<BallThumbnailCard>();
                    Debug.Log("[BallThumbnailCardFix] ✓ Added BallThumbnailCard.");
                }

                // ── 2. Wire fields ────────────────────────────────────────────
                var so = new SerializedObject(ballComp);

                void Wire(string propName, Object? obj, string label)
                {
                    if (obj == null)
                    {
                        Debug.LogWarning($"[BallThumbnailCardFix] NOT FOUND: {label}");
                        failed++;
                        return;
                    }
                    so.FindProperty(propName).objectReferenceValue = obj;
                    Debug.Log($"[BallThumbnailCardFix] ✓ {propName} → {label}");
                    wired++;
                }

                Wire("portraitImage",
                    root.Find("Portrait")?.GetComponent<Image>(),
                    "Portrait/Image");

                Wire("nameText",
                    root.Find("NameLabel")?.GetComponent<TextMeshProUGUI>(),
                    "NameLabel/TextMeshProUGUI");

                Wire("quantityText",
                    root.Find("AmountBadge/Text")?.GetComponent<TextMeshProUGUI>(),
                    "AmountBadge/Text/TextMeshProUGUI");

                Wire("selectionHighlight",
                    root.Find("SelectionHighlight")?.GetComponent<Image>(),
                    "SelectionHighlight/Image");

                Wire("backgroundImage",
                    root.Find("Background")?.GetComponent<Image>(),
                    "Background/Image");

                Wire("cardButton",
                    prefabRoot.GetComponent<Button>(),
                    "root Button");

                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(prefabRoot);

                // ── 3. Save prefab ────────────────────────────────────────────
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, path);

                string msg = $"{wired} fields wired, {failed} failed.\n\n";
                msg += failed == 0
                    ? "Prefab fixed! Hit Play — cards should now show ball names, images, and quantities."
                    : "Some fields failed — check Console for details.";

                Debug.Log($"[BallThumbnailCardFix] Done — {wired} wired, {failed} failed.");
                EditorUtility.DisplayDialog("Fix Ball Card", msg, "OK");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }
    }
}
#endif
