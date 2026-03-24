#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Golfin.Inventory.Editor
{
    /// <summary>
    /// Targeted patch for the FilterBar inside ClubsContent.
    /// Tears down the old ScrollRect/Viewport/Content structure and replaces it
    /// with a plain HorizontalLayoutGroup — no masking, no nesting.
    ///
    /// Safer than a full rebuild: leaves the rest of InventoryScreen untouched.
    ///
    /// Run: GOLFIN/Patch — Rebuild FilterBar
    /// </summary>
    public static class FilterBarPatcher
    {
        private static readonly string[] FILTER_LABELS =
            { "ALL", "DRIVERS", "WOODS", "IRONS", "A.WEDGES", "P.WEDGES", "S.WEDGES", "PUTTERS" };

        private const float FILTERBAR_H = 48f;

        // [MenuItem - archived] "GOLFIN/Patch - Rebuild FilterBar"
        public static void Run()
        {
            // ── Find FilterBar ────────────────────────────────────────────────
            var filterBarGO = FindFilterBar();
            if (filterBarGO == null)
            {
                Debug.LogError("[FilterBarPatcher] FilterBar not found in scene. Run GOLFIN/Build Inventory Screen first.");
                EditorUtility.DisplayDialog("Patch FilterBar", "FilterBar not found in scene.\nRun GOLFIN/Build Inventory Screen first.", "OK");
                return;
            }

            // ── Find ClubFilterBar component ──────────────────────────────────
            var filterBarComp = filterBarGO.GetComponent<ClubFilterBar>();
            if (filterBarComp == null)
            {
                filterBarComp = filterBarGO.AddComponent<ClubFilterBar>();
                Debug.Log("[FilterBarPatcher] Added missing ClubFilterBar component.");
            }

            // ── Destroy all children (removes old Viewport / Content / buttons) ─
            int removed = 0;
            while (filterBarGO.transform.childCount > 0)
            {
                Object.DestroyImmediate(filterBarGO.transform.GetChild(0).gameObject);
                removed++;
            }
            Debug.Log($"[FilterBarPatcher] Removed {removed} old children from FilterBar.");

            // ── Remove old ScrollRect if present ──────────────────────────────
            var oldScroll = filterBarGO.GetComponent<ScrollRect>();
            if (oldScroll != null) { Object.DestroyImmediate(oldScroll); Debug.Log("[FilterBarPatcher] Removed ScrollRect."); }

            // ── Fix RectTransform ─────────────────────────────────────────────
            var rt = filterBarGO.GetComponent<RectTransform>();
            if (rt == null) rt = filterBarGO.AddComponent<RectTransform>();
            rt.anchorMin        = new Vector2(0f, 1f);
            rt.anchorMax        = new Vector2(1f, 1f);
            rt.pivot            = new Vector2(0.5f, 1f);
            rt.offsetMin        = new Vector2(0f, -FILTERBAR_H);
            rt.offsetMax        = Vector2.zero;

            // ── Fix/add background Image ──────────────────────────────────────
            var bg = filterBarGO.GetComponent<Image>();
            if (bg == null) bg = filterBarGO.AddComponent<Image>();
            bg.color = new Color(0.06f, 0.06f, 0.10f, 1f);

            // ── Add HorizontalLayoutGroup ─────────────────────────────────────
            var oldHlg = filterBarGO.GetComponent<HorizontalLayoutGroup>();
            if (oldHlg != null) Object.DestroyImmediate(oldHlg);

            var hlg = filterBarGO.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing                = 2f;
            hlg.padding                = new RectOffset(4, 4, 4, 4);
            hlg.childAlignment         = TextAnchor.MiddleCenter;
            hlg.childForceExpandHeight = true;
            hlg.childForceExpandWidth  = true;
            hlg.childControlWidth      = true;
            hlg.childControlHeight     = true;

            // ── Build filter buttons ──────────────────────────────────────────
            int count   = FILTER_LABELS.Length;
            var buttons = new Object[count];

            for (int i = 0; i < count; i++)
            {
                var btnGO = new GameObject(FILTER_LABELS[i] + "Filter");
                btnGO.transform.SetParent(filterBarGO.transform, false);

                // Background image — visible even for inactive buttons so we can see them
                var btnImg = btnGO.AddComponent<Image>();
                btnImg.color = i == 0
                    ? new Color(1f, 1f, 1f, 0.25f)   // ALL active — slight white tint
                    : new Color(1f, 1f, 1f, 0.05f);   // others — very subtle, still visible container

                var btn = btnGO.AddComponent<Button>();
                buttons[i] = btn;

                // Label
                var labelGO = new GameObject("Label");
                labelGO.transform.SetParent(btnGO.transform, false);
                var labelRT = labelGO.AddComponent<RectTransform>();
                labelRT.anchorMin = Vector2.zero;
                labelRT.anchorMax = Vector2.one;
                labelRT.offsetMin = Vector2.zero;
                labelRT.offsetMax = Vector2.zero;
                var tmp = labelGO.AddComponent<TextMeshProUGUI>();
                tmp.text         = FILTER_LABELS[i];
                tmp.fontSize     = 10f;
                tmp.fontStyle    = FontStyles.Bold;
                tmp.color        = i == 0 ? Color.white : new Color(0.6f, 0.6f, 0.6f, 1f);
                tmp.alignment    = TextAlignmentOptions.Center;
                tmp.raycastTarget = false;   // label doesn't need raycasts — button parent handles it
            }

            // ── Re-wire ClubFilterBar ─────────────────────────────────────────
            var so = new SerializedObject(filterBarComp);
            var arrProp = so.FindProperty("filterButtons");
            arrProp.arraySize = count;
            for (int i = 0; i < count; i++)
                arrProp.GetArrayElementAtIndex(i).objectReferenceValue = buttons[i];
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(filterBarComp);

            // ── Also fix ClubsMainSection sibling order ────────────────────────
            // FilterBar must be the LAST sibling to render on top of ClubsMainSection
            var clubsContent = filterBarGO.transform.parent;
            if (clubsContent != null)
            {
                filterBarGO.transform.SetAsLastSibling();
                Debug.Log("[FilterBarPatcher] FilterBar moved to last sibling (renders on top).");
            }

            EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());

            EditorUtility.DisplayDialog("Patch FilterBar",
                "Done!\n\n" +
                "• Old ScrollRect / Viewport / Content removed\n" +
                "• HorizontalLayoutGroup added (childForceExpandWidth)\n" +
                "• 8 filter buttons rebuilt directly in FilterBar\n" +
                "• ClubFilterBar re-wired\n" +
                "• FilterBar moved to last sibling (renders on top)\n\n" +
                "Save the scene, enter Play Mode, and navigate to Inventory.",
                "OK");

            Debug.Log("[FilterBarPatcher] ✓ FilterBar patched successfully.");
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static GameObject FindFilterBar()
        {
            // Walk every root GameObject, look for FilterBar by name
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            foreach (var root in scene.GetRootGameObjects())
            {
                var t = root.transform.Find("InventoryScreen/ContentArea/ClubsContent/FilterBar");
                if (t != null) return t.gameObject;

                // Fallback: deep search
                var found = FindByName(root.transform, "FilterBar");
                if (found != null && found.GetComponent<ClubFilterBar>() != null)
                    return found.gameObject;
            }
            return null;
        }

        private static Transform FindByName(Transform parent, string name)
        {
            if (parent.name == name) return parent;
            foreach (Transform child in parent)
            {
                var result = FindByName(child, name);
                if (result != null) return result;
            }
            return null;
        }
    }
}
#endif
