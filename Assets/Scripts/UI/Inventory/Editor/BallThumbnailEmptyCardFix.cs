#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Golfin.Inventory.Editor
{
    /// <summary>
    /// Fixes BallThumbnailEmptyCard.prefab:
    ///   1. Adds LayoutElement (preferredWidth=135, preferredHeight=165) to the root
    ///      so the HLG positions it correctly on instantiation — no runtime AddComponent needed.
    ///   2. Sets Button.interactable = false (empty slots are not selectable).
    ///   3. Disables RaycastTarget on all child Images.
    ///
    /// Run: GOLFIN/Fix/Ball Thumbnail Empty Card Prefab
    /// </summary>
    public static class BallThumbnailEmptyCardFix
    {
        private const string PREFAB_PATH = "Assets/Prefabs/UI/Inventory/BallThumbnailEmptyCard.prefab";

        [MenuItem("GOLFIN/Fix/Ball Thumbnail Empty Card Prefab")]
        public static void Run()
        {
            var prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_PATH);
            if (prefabAsset == null)
            {
                EditorUtility.DisplayDialog("Fix Empty Card",
                    $"Prefab not found at:\n{PREFAB_PATH}", "OK");
                return;
            }

            string path     = AssetDatabase.GetAssetPath(prefabAsset);
            var prefabRoot  = PrefabUtility.LoadPrefabContents(path);

            try
            {
                // ── 1. Set RectTransform sizeDelta so HLG has a concrete size ────
                // The HLG reads the child's RectTransform.rect when LayoutElement
                // values are 0/-1 (unset). Root was (0,0) — fix it first.
                var rt = prefabRoot.GetComponent<RectTransform>();
                rt.anchorMin        = new Vector2(0f, 0f);
                rt.anchorMax        = new Vector2(0f, 0f);
                rt.pivot            = new Vector2(0.5f, 0.5f);
                rt.sizeDelta        = new Vector2(135f, 165f);
                rt.anchoredPosition = Vector2.zero;
                EditorUtility.SetDirty(rt);
                Debug.Log("[EmptyCardFix] ✓ RectTransform sizeDelta set to 135×165.");

                // ── 2. Bake LayoutElement so sizes survive layout rebuilds ──────
                var le = prefabRoot.GetComponent<LayoutElement>();
                if (le == null)
                    le = prefabRoot.AddComponent<LayoutElement>();

                le.ignoreLayout    = false;
                le.preferredWidth  = 135f;
                le.preferredHeight = 165f;
                le.minWidth        = 135f;
                le.minHeight       = 165f;
                EditorUtility.SetDirty(le);
                Debug.Log("[EmptyCardFix] ✓ LayoutElement baked — 135×165.");

                // ── 3. Make button non-interactable ───────────────────────────
                var btn = prefabRoot.GetComponent<Button>();
                if (btn != null)
                {
                    btn.interactable = false;
                    EditorUtility.SetDirty(btn);
                    Debug.Log("[EmptyCardFix] ✓ Button.interactable = false.");
                }

                // ── 4. Disable RaycastTarget on all Images ────────────────────
                foreach (var img in prefabRoot.GetComponentsInChildren<Image>(true))
                {
                    img.raycastTarget = false;
                    EditorUtility.SetDirty(img);
                }
                Debug.Log("[EmptyCardFix] ✓ RaycastTarget disabled on all Images.");

                EditorUtility.SetDirty(prefabRoot);
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, path);

                EditorUtility.DisplayDialog("Fix Empty Card",
                    "Done!\n\n" +
                    "• LayoutElement baked (135×165)\n" +
                    "• Button.interactable = false\n" +
                    "• RaycastTarget disabled on all Images\n\n" +
                    "Hit Play — empty cards will slot in correctly after the real balls.",
                    "OK");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }
    }
}
#endif
