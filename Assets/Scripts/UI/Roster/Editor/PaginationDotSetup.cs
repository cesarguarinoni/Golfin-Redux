#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Golfin.Roster.Editor
{
    /// <summary>
    /// GOLFIN > Setup Pagination Dots
    ///
    /// One-click tool that:
    ///   1. Creates Assets/Prefabs/UI/Roster/PaginationDot.prefab
    ///      (16×16 Image using Unity's built-in Knob sprite)
    ///   2. Adds a HorizontalLayoutGroup to the PaginationDots GameObject
    ///      under CarouselSection if one isn't already present
    ///   3. Auto-wires paginationDotPrefab and paginationDotsParent on
    ///      CarouselController
    /// </summary>
    public static class PaginationDotSetup
    {
        private const string PREFAB_PATH = "Assets/Prefabs/UI/Roster/PaginationDot.prefab";

        [MenuItem("GOLFIN/Setup/Pagination Dots")]
        public static void Run()
        {
            // ── 1. Find CarouselController in the open scene ─────────────────
            var carousel = Object.FindObjectOfType<CarouselController>();
            if (carousel == null)
            {
                EditorUtility.DisplayDialog("Error",
                    "CarouselController not found in the open scene.\n\nMake sure ShellScene is open.",
                    "OK");
                return;
            }

            // ── 2. Create (or reuse) PaginationDot prefab ────────────────────
            var dotPrefab = CreateOrLoadDotPrefab();
            if (dotPrefab == null)
            {
                EditorUtility.DisplayDialog("Error",
                    "Failed to create PaginationDot prefab.", "OK");
                return;
            }

            // ── 3. Find PaginationDots GameObject ────────────────────────────
            var dotsParentGO = GameObject.Find("PaginationDots");
            if (dotsParentGO == null)
            {
                // Try to find it as a child of CarouselSection
                var carouselSection = GameObject.Find("CarouselSection");
                if (carouselSection != null)
                    dotsParentGO = carouselSection.transform
                        .Find("ScrollView/PaginationDots")?.gameObject;
            }

            if (dotsParentGO == null)
            {
                EditorUtility.DisplayDialog("Error",
                    "PaginationDots GameObject not found in the scene.\n\n" +
                    "Expected path: CarouselSection > ScrollView > PaginationDots",
                    "OK");
                return;
            }

            // ── 4. Ensure HorizontalLayoutGroup on PaginationDots ────────────
            var hlg = dotsParentGO.GetComponent<HorizontalLayoutGroup>();
            if (hlg == null)
                hlg = dotsParentGO.AddComponent<HorizontalLayoutGroup>();

            hlg.spacing             = 8f;
            hlg.childAlignment      = TextAnchor.MiddleCenter;
            hlg.childForceExpandWidth  = false;
            hlg.childForceExpandHeight = false;
            hlg.childControlWidth   = false;
            hlg.childControlHeight  = false;
            EditorUtility.SetDirty(dotsParentGO);

            // ── 5. Wire fields on CarouselController ─────────────────────────
            var so = new SerializedObject(carousel);
            so.FindProperty("paginationDotPrefab").objectReferenceValue  = dotPrefab;
            so.FindProperty("paginationDotsParent").objectReferenceValue = dotsParentGO.transform;
            so.ApplyModifiedProperties();

            EditorSceneManager.MarkSceneDirty(carousel.gameObject.scene);

            EditorUtility.DisplayDialog("Done!",
                "Pagination dots configured:\n\n" +
                $"• Prefab:  {PREFAB_PATH}\n" +
                "• HorizontalLayoutGroup added to PaginationDots\n" +
                "• CarouselController fields wired\n\n" +
                "Hit Play to see the dots in action.",
                "OK");

            Debug.Log("[PaginationDotSetup] Setup complete.");
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static GameObject CreateOrLoadDotPrefab()
        {
            // Reuse existing prefab if it's already there
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_PATH);
            if (existing != null)
            {
                Debug.Log($"[PaginationDotSetup] Reusing existing prefab at {PREFAB_PATH}");
                return existing;
            }

            // Ensure directory exists
            var dir = Path.GetDirectoryName(PREFAB_PATH);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir!);

            // Build the dot GameObject
            var dotGO = new GameObject("PaginationDot", typeof(RectTransform), typeof(Image));
            var rt = dotGO.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(16f, 16f);

            var img = dotGO.GetComponent<Image>();

            // Use Unity's built-in Knob sprite (round white circle, always available)
            var knob = Resources.GetBuiltinResource<Sprite>("UI/Skin/Knob.psd");
            if (knob != null)
            {
                img.sprite = knob;
                img.type   = Image.Type.Simple;
            }
            img.color = Color.white;

            // Save as prefab, then clean up the temporary scene object
            var prefabAsset = PrefabUtility.SaveAsPrefabAsset(dotGO, PREFAB_PATH);
            Object.DestroyImmediate(dotGO);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[PaginationDotSetup] Created prefab at {PREFAB_PATH}");
            return prefabAsset;
        }
    }
}
#endif
