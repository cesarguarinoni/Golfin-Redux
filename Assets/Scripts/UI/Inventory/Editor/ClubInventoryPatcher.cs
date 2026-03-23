#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Golfin.Inventory.Editor
{
    /// <summary>
    /// One-shot patchers for the Club Inventory screen layout.
    /// Run these in order after the scene has been manually adjusted.
    ///
    /// GOLFIN/Patch Club Inventory/All Fixes    — runs all patches in sequence
    /// GOLFIN/Patch Club Inventory/Fix3 EQUIP Spacer
    /// GOLFIN/Patch Club Inventory/Fix6 Spacing Gaps
    /// GOLFIN/Patch Club Inventory/Fix9 Copy Pagination Dot Prefab
    /// GOLFIN/Patch Club Inventory/Fix10 Font Sizes
    /// GOLFIN/Patch Club Inventory/Fix11 Stat Row Layout
    /// </summary>
    public static class ClubInventoryPatcher
    {
        // ── Master patch ──────────────────────────────────────────────────────

        [MenuItem("GOLFIN/Patch Club Inventory/All Fixes")]
        public static void PatchAll()
        {
            int fixes = 0;
            fixes += Fix3_EquipSpacer()    ? 1 : 0;
            fixes += Fix6_SpacingGaps()    ? 1 : 0;
            fixes += Fix9_CopyDotPrefab()  ? 1 : 0;
            fixes += Fix10_FontSizes()     ? 1 : 0;
            fixes += Fix11_StatRowLayout() ? 1 : 0;

            EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());

            EditorUtility.DisplayDialog("Club Inventory Patcher",
                $"All patches applied ({fixes}/5 succeeded).\nSave the scene and enter Play Mode to verify.",
                "OK");
        }

        // ── Fix 3: EQUIP button spacer ────────────────────────────────────────

        [MenuItem("GOLFIN/Patch Club Inventory/Fix3 EQUIP Spacer")]
        public static void PatchFix3()
        {
            bool ok = Fix3_EquipSpacer();
            MarkDirtyAndLog("Fix3 EQUIP Spacer", ok);
        }

        private static bool Fix3_EquipSpacer()
        {
            var rightPanel = FindByName(null, "RightPanel");
            if (rightPanel == null)
            {
                Debug.LogWarning("[ClubInventoryPatcher] RightPanel not found (Fix 3 skipped).");
                return false;
            }

            // Ensure RightPanel VLG has 24px bottom padding
            var vlg = rightPanel.GetComponent<VerticalLayoutGroup>();
            if (vlg != null)
            {
                Undo.RecordObject(vlg, "Fix3 EQUIP Spacer");
                vlg.padding = new RectOffset(vlg.padding.left, vlg.padding.right,
                                             vlg.padding.top, 24);
                EditorUtility.SetDirty(vlg);
            }

            // Find EquipButton
            var equipBtn = rightPanel.Find("EquipButton");
            if (equipBtn == null)
            {
                Debug.LogWarning("[ClubInventoryPatcher] EquipButton not found in RightPanel (Fix 3 skipped).");
                return false;
            }

            // Check if spacer already exists
            var existingSpacer = rightPanel.Find("EquipSpacer");
            if (existingSpacer != null)
            {
                Debug.Log("[ClubInventoryPatcher] EquipSpacer already exists (Fix 3 skipped).");
                return true;
            }

            // Insert flexible spacer before EquipButton
            var spacerGO = new GameObject("EquipSpacer");
            Undo.RegisterCreatedObjectUndo(spacerGO, "Fix3 EQUIP Spacer");
            spacerGO.transform.SetParent(rightPanel, false);
            spacerGO.transform.SetSiblingIndex(equipBtn.GetSiblingIndex());

            var le = spacerGO.AddComponent<LayoutElement>();
            le.flexibleHeight = 1f;

            Debug.Log("[ClubInventoryPatcher] Fix 3: EquipSpacer added.");
            return true;
        }

        // ── Fix 6: Spacing gaps ───────────────────────────────────────────────

        [MenuItem("GOLFIN/Patch Club Inventory/Fix6 Spacing Gaps")]
        public static void PatchFix6()
        {
            bool ok = Fix6_SpacingGaps();
            MarkDirtyAndLog("Fix6 Spacing Gaps", ok);
        }

        private static bool Fix6_SpacingGaps()
        {
            bool any = false;

            // Gap between TabBar and ContentArea: find InventoryScreen's VLG or set
            // ClubsMainSection top offset to account for 12px below FilterBar.
            // The FilterBar is anchored at the top of ClubsContent.
            // ClubsMainSection has offsetMax.y = -FILTERBAR_H.
            // We need ClubsMainSection offsetMax.y = -(FILTERBAR_H + 12)

            var clubsMainSection = FindByName(null, "ClubsMainSection");
            if (clubsMainSection != null)
            {
                var rt = clubsMainSection.GetComponent<RectTransform>();
                if (rt != null)
                {
                    Undo.RecordObject(rt, "Fix6 Spacing Gaps");
                    // FilterBar is 48px. Add 12px gap → -60 on offsetMax.y
                    if (Mathf.Approximately(rt.offsetMax.y, -48f))
                    {
                        rt.offsetMax = new Vector2(rt.offsetMax.x, -60f);
                        EditorUtility.SetDirty(rt);
                        Debug.Log("[ClubInventoryPatcher] Fix 6: ClubsMainSection gap from FilterBar set to 12px.");
                        any = true;
                    }
                    else
                    {
                        Debug.Log($"[ClubInventoryPatcher] Fix 6: ClubsMainSection offsetMax.y={rt.offsetMax.y} (may already be correct or manually changed — skipping).");
                    }
                }
            }
            else
            {
                Debug.LogWarning("[ClubInventoryPatcher] ClubsMainSection not found (Fix 6 skipped).");
            }

            // Gap: CarouselSection bottom to DetailPanel — set detail panel offsetMax.y
            // CarouselSection height + 17px gap → DetailPanel starts 17px below carousel
            var detailPanel = FindByName(null, "ClubDetailPanel");
            var carouselSection = FindByName(null, "ClubCarouselSection");
            if (detailPanel != null && carouselSection != null)
            {
                var carouselRT = carouselSection.GetComponent<RectTransform>();
                var detailRT   = detailPanel.GetComponent<RectTransform>();
                if (carouselRT != null && detailRT != null)
                {
                    float carouselH = Mathf.Abs(carouselRT.offsetMin.y); // distance from top
                    float gapTarget = carouselH + 17f;
                    if (!Mathf.Approximately(Mathf.Abs(detailRT.offsetMax.y), gapTarget))
                    {
                        Undo.RecordObject(detailRT, "Fix6 Spacing Gaps");
                        detailRT.offsetMax = new Vector2(detailRT.offsetMax.x, -gapTarget);
                        EditorUtility.SetDirty(detailRT);
                        Debug.Log($"[ClubInventoryPatcher] Fix 6: DetailPanel offsetMax.y set to -{gapTarget}.");
                        any = true;
                    }
                }
            }

            return any;
        }

        // ── Fix 9: Copy pagination dot prefab from Roster carousel ────────────

        [MenuItem("GOLFIN/Patch Club Inventory/Fix9 Copy Pagination Dot Prefab")]
        public static void PatchFix9()
        {
            bool ok = Fix9_CopyDotPrefab();
            MarkDirtyAndLog("Fix9 Pagination Dot Prefab", ok);
        }

        private static bool Fix9_CopyDotPrefab()
        {
            // Find Roster CarouselController
            var rosterCarousel = Object.FindObjectOfType<Golfin.Roster.CarouselController>();
            if (rosterCarousel == null)
            {
                Debug.LogWarning("[ClubInventoryPatcher] Roster CarouselController not found (Fix 9 skipped).");
                return false;
            }

            var rosterSO = new SerializedObject(rosterCarousel);
            var dotPrefabProp = rosterSO.FindProperty("paginationDotPrefab");
            if (dotPrefabProp == null || dotPrefabProp.objectReferenceValue == null)
            {
                Debug.LogWarning("[ClubInventoryPatcher] Roster paginationDotPrefab not assigned (Fix 9 skipped).");
                return false;
            }

            var dotPrefab = dotPrefabProp.objectReferenceValue;

            // Find ClubCarouselController
            var clubCarousel = Object.FindObjectOfType<ClubCarouselController>();
            if (clubCarousel == null)
            {
                Debug.LogWarning("[ClubInventoryPatcher] ClubCarouselController not found (Fix 9 skipped).");
                return false;
            }

            var clubSO = new SerializedObject(clubCarousel);
            var clubDotProp = clubSO.FindProperty("paginationDotPrefab");
            if (clubDotProp == null)
            {
                Debug.LogWarning("[ClubInventoryPatcher] ClubCarouselController.paginationDotPrefab field not found (Fix 9 skipped).");
                return false;
            }

            Undo.RecordObject(clubCarousel, "Fix9 Pagination Dot Prefab");
            clubDotProp.objectReferenceValue = dotPrefab;
            clubSO.ApplyModifiedProperties();

            Debug.Log($"[ClubInventoryPatcher] Fix 9: paginationDotPrefab '{dotPrefab.name}' copied to ClubCarouselController.");
            return true;
        }

        // ── Fix 10: Font sizes in Club Detail Panel ───────────────────────────

        [MenuItem("GOLFIN/Patch Club Inventory/Fix10 Font Sizes")]
        public static void PatchFix10()
        {
            bool ok = Fix10_FontSizes();
            MarkDirtyAndLog("Fix10 Font Sizes", ok);
        }

        private static bool Fix10_FontSizes()
        {
            var detailPanel = FindByName(null, "ClubDetailPanel");
            if (detailPanel == null)
            {
                Debug.LogWarning("[ClubInventoryPatcher] ClubDetailPanel not found (Fix 10 skipped).");
                return false;
            }

            int changed = 0;

            // Helper: set font size on a named child TMP
            changed += SetTMPSize(detailPanel, "ClubNameText",   32f, FontStyles.Bold);
            changed += SetTMPSize(detailPanel, "RarityLabel",    32f, FontStyles.Bold);
            changed += SetTMPSize(detailPanel, "LevelText",      32f, FontStyles.Normal);
            changed += SetTMPSize(detailPanel, "LevelTextMax",   24f, FontStyles.Normal);
            changed += SetTMPSize(detailPanel, "InfoHeader",     34f, FontStyles.Bold);
            changed += SetTMPSize(detailPanel, "InfoText",       24f, FontStyles.Normal);

            // Stat rows: StatsName = 24 Medium, StatNumber = 24 Bold
            string[] statRows = { "PowerRow", "AccuracyRow", "LieResistanceRow", "LoftRow", "DurabilityRow", "DistanceRow" };
            foreach (var rowName in statRows)
            {
                var row = FindDeep(detailPanel, rowName);
                if (row == null) continue;

                var nameField = row.Find("StatsName");
                if (nameField != null) changed += SetTMPOnTransform(nameField, 24f, FontStyles.Medium);

                var numField = row.Find("StatNumber");
                if (numField != null) changed += SetTMPOnTransform(numField, 24f, FontStyles.Bold);

                // DistanceRow has a DistanceValue instead of StatNumber
                var distVal = row.Find("DistanceValue");
                if (distVal != null) changed += SetTMPOnTransform(distVal, 24f, FontStyles.Bold);
            }

            // Buttons: LevelUpButton, RepairButton, CompareButton text = 28 SemiBold
            string[] regularButtons = { "LevelUpButton", "RepairButton", "CompareButton" };
            foreach (var btnName in regularButtons)
            {
                var btnT = FindDeep(detailPanel, btnName);
                if (btnT == null) continue;
                var textChild = btnT.Find("Text (TMP)");
                if (textChild != null) changed += SetTMPOnTransform(textChild, 28f, FontStyles.Bold);
            }

            // EquipButton = 47 SemiBold
            var equipBtn = FindDeep(detailPanel, "EquipButton");
            if (equipBtn != null)
            {
                var textChild = equipBtn.Find("Text (TMP)");
                if (textChild != null) changed += SetTMPOnTransform(textChild, 47f, FontStyles.Bold);
            }

            Debug.Log($"[ClubInventoryPatcher] Fix 10: {changed} TMP font sizes updated.");
            return changed > 0;
        }

        // ── Fix 11: Stat row layout ───────────────────────────────────────────

        [MenuItem("GOLFIN/Patch Club Inventory/Fix11 Stat Row Layout")]
        public static void PatchFix11()
        {
            bool ok = Fix11_StatRowLayout();
            MarkDirtyAndLog("Fix11 Stat Row Layout", ok);
        }

        private static bool Fix11_StatRowLayout()
        {
            var detailPanel = FindByName(null, "ClubDetailPanel");
            if (detailPanel == null)
            {
                Debug.LogWarning("[ClubInventoryPatcher] ClubDetailPanel not found (Fix 11 skipped).");
                return false;
            }

            int changed = 0;

            // StatsPanel spacing between rows = 17px
            var statsPanel = FindDeep(detailPanel, "StatsPanel");
            if (statsPanel != null)
            {
                var vlg = statsPanel.GetComponent<VerticalLayoutGroup>();
                if (vlg != null)
                {
                    Undo.RecordObject(vlg, "Fix11 Stat Row Layout");
                    vlg.spacing = 17f;
                    EditorUtility.SetDirty(vlg);
                    changed++;
                }
            }

            // Each stat row: icon 39×32, StatNumber column preferredWidth = 82, bar height = 14
            string[] statRows = { "PowerRow", "AccuracyRow", "LieResistanceRow", "LoftRow", "DurabilityRow" };
            foreach (var rowName in statRows)
            {
                var row = FindDeep(detailPanel, rowName);
                if (row == null) continue;

                // Row height = 32px (icon height)
                var rowLE = row.GetComponent<LayoutElement>();
                if (rowLE != null)
                {
                    Undo.RecordObject(rowLE, "Fix11 Stat Row Layout");
                    rowLE.preferredHeight = 32f;
                    EditorUtility.SetDirty(rowLE);
                    changed++;
                }

                // HLG spacing = 12px
                var hlg = row.GetComponent<HorizontalLayoutGroup>();
                if (hlg != null)
                {
                    Undo.RecordObject(hlg, "Fix11 Stat Row Layout");
                    hlg.spacing = 12f;
                    EditorUtility.SetDirty(hlg);
                    changed++;
                }

                // StatsName preferredWidth = 82
                var nameLE = row.Find("StatsName")?.GetComponent<LayoutElement>();
                if (nameLE != null)
                {
                    Undo.RecordObject(nameLE, "Fix11 Stat Row Layout");
                    nameLE.preferredWidth = 82f;
                    EditorUtility.SetDirty(nameLE);
                    changed++;
                }

                // Bar preferredHeight = 14
                var barLE = row.Find("Bar")?.GetComponent<LayoutElement>();
                if (barLE != null)
                {
                    Undo.RecordObject(barLE, "Fix11 Stat Row Layout");
                    barLE.preferredHeight = 14f;
                    EditorUtility.SetDirty(barLE);
                    changed++;
                }

                // StatNumber preferredWidth = 82
                var numLE = row.Find("StatNumber")?.GetComponent<LayoutElement>();
                if (numLE != null)
                {
                    Undo.RecordObject(numLE, "Fix11 Stat Row Layout");
                    numLE.preferredWidth = 82f;
                    EditorUtility.SetDirty(numLE);
                    changed++;
                }
            }

            // RightPanel VLG spacing = 17px
            var rightPanel = FindDeep(detailPanel, "RightPanel");
            if (rightPanel == null) rightPanel = FindByName(null, "RightPanel");
            if (rightPanel != null)
            {
                var vlg = rightPanel.GetComponent<VerticalLayoutGroup>();
                if (vlg != null)
                {
                    Undo.RecordObject(vlg, "Fix11 Stat Row Layout");
                    vlg.spacing = 17f;
                    vlg.padding = new RectOffset(17, 17, 17, 24);
                    EditorUtility.SetDirty(vlg);
                    changed++;
                }
            }

            // EQUIP button preferred height = 85, regular buttons = 38
            string[] regularButtons = { "LevelUpButton", "RepairButton", "CompareButton" };
            foreach (var btnName in regularButtons)
            {
                var btnLE = FindDeep(detailPanel, btnName)?.GetComponent<LayoutElement>();
                if (btnLE != null)
                {
                    Undo.RecordObject(btnLE, "Fix11 Stat Row Layout");
                    btnLE.preferredHeight = 38f;
                    EditorUtility.SetDirty(btnLE);
                    changed++;
                }
            }

            var equipLE = FindDeep(detailPanel, "EquipButton")?.GetComponent<LayoutElement>();
            if (equipLE != null)
            {
                Undo.RecordObject(equipLE, "Fix11 Stat Row Layout");
                equipLE.preferredHeight = 85f;
                EditorUtility.SetDirty(equipLE);
                changed++;
            }

            // Detail panel internal padding = 17px all sides (handled via RightPanel VLG above)
            // Left/Right panel split: 46%/54%
            var leftPanel = FindDeep(detailPanel, "LeftPanel");
            if (leftPanel != null)
            {
                var rt = leftPanel.GetComponent<RectTransform>();
                if (rt != null)
                {
                    Undo.RecordObject(rt, "Fix11 Stat Row Layout");
                    rt.anchorMin = new Vector2(0f,    0f);
                    rt.anchorMax = new Vector2(0.46f, 1f);
                    rt.offsetMin = new Vector2(17f, 17f);
                    rt.offsetMax = new Vector2(0f, -17f);
                    EditorUtility.SetDirty(rt);
                    changed++;
                }
            }

            if (rightPanel != null)
            {
                var rt = rightPanel.GetComponent<RectTransform>();
                if (rt != null)
                {
                    Undo.RecordObject(rt, "Fix11 Stat Row Layout");
                    rt.anchorMin = new Vector2(0.46f, 0f);
                    rt.anchorMax = new Vector2(1f,    1f);
                    rt.offsetMin = Vector2.zero;
                    rt.offsetMax = Vector2.zero;
                    EditorUtility.SetDirty(rt);
                    changed++;
                }
            }

            Debug.Log($"[ClubInventoryPatcher] Fix 11: {changed} layout properties updated.");
            return changed > 0;
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static int SetTMPSize(Transform root, string name, float size, FontStyles style)
        {
            var t = FindDeep(root, name);
            if (t == null) return 0;
            return SetTMPOnTransform(t, size, style);
        }

        private static int SetTMPOnTransform(Transform t, float size, FontStyles style)
        {
            var tmp = t.GetComponent<TextMeshProUGUI>();
            if (tmp == null) return 0;
            Undo.RecordObject(tmp, "Fix Font Sizes");
            tmp.fontSize  = size;
            tmp.fontStyle = style;
            EditorUtility.SetDirty(tmp);
            return 1;
        }

        private static Transform? FindDeep(Transform? root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;
            foreach (Transform child in root)
            {
                var result = FindDeep(child, name);
                if (result != null) return result;
            }
            return null;
        }

        private static Transform? FindByName(Transform? parent, string name)
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            foreach (var rootGO in scene.GetRootGameObjects())
            {
                var t = FindDeep(rootGO.transform, name);
                if (t != null) return t;
            }
            return null;
        }

        private static void MarkDirtyAndLog(string fixName, bool success)
        {
            if (success)
            {
                EditorSceneManager.MarkSceneDirty(
                    UnityEngine.SceneManagement.SceneManager.GetActiveScene());
                EditorUtility.DisplayDialog("Club Inventory Patcher",
                    $"{fixName}: Applied successfully.\nSave the scene and test in Play Mode.", "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("Club Inventory Patcher",
                    $"{fixName}: Could not apply (check Console for details).", "OK");
            }
        }
    }
}
#endif
