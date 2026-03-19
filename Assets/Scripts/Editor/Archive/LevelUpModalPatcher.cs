#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Golfin.Roster.Editor
{
    /// <summary>
    /// Surgical patches for the LevelUpModal hierarchy.
    /// NEVER destroys or rebuilds anything — only modifies the specific nodes listed.
    /// Safe to run on a modal you have already customised.
    ///
    /// Patch A — Row Alignment  (GOLFIN/Patch LevelUpModal — Row Alignment)
    ///   Inserts a flexibleWidth spacer into CostRow and RewardRow so that
    ///   the label stays left and the value(s) are pushed to the right.
    ///   Changes childForceExpandWidth → false on those two rows only.
    ///
    /// Patch B — Split Stat Values  (GOLFIN/Patch LevelUpModal — Split Stat Values)
    ///   Replaces the single "StatValue" TMP in each stat row with two siblings:
    ///   "StatValueCurrent" (keeps original font size) and "StatValueMax" (smaller).
    ///   Idempotent: skips any row that already has StatValueCurrent.
    /// </summary>
    public static class LevelUpModalPatcher
    {
        // ── Menu items ───────────────────────────────────────────────────────

        // [MenuItem - archived]
        public static void PatchRowAlignment()
        {
            var root = FindModalRoot();
            if (root == null) return;

            int fixed_ = 0;
            fixed_ += PatchRow(root, "ModalPanel/InfoSection/CostRow",   "CostLabel",   "RPIcon");
            fixed_ += PatchRow(root, "ModalPanel/InfoSection/RewardRow",  "RewardLabel", "RewardValue");

            MarkDirtyAndSave(root.gameObject);
            Debug.Log($"[LevelUpPatcher] Row alignment: {fixed_} row(s) patched.");
            EditorUtility.DisplayDialog("Done", $"Row alignment patched ({fixed_} rows updated).\nNo other changes made.", "OK");
        }

        // [MenuItem - archived]
        public static void PatchPendingLabelPlacement()
        {
            var root = FindModalRoot();
            if (root == null) return;

            int fixed_ = 0;
            fixed_ += PatchPendingLabel(root, "ModalPanel/SPSection/StatRow_Strength");
            fixed_ += PatchPendingLabel(root, "ModalPanel/SPSection/StatRow_ClubControl");
            fixed_ += PatchPendingLabel(root, "ModalPanel/SPSection/StatRow_Recovery");
            fixed_ += PatchPendingLabel(root, "ModalPanel/SPSection/StatRow_Stamina");

            MarkDirtyAndSave(root.gameObject);
            Debug.Log($"[LevelUpPatcher] PendingLabel placement: {fixed_} row(s) adjusted.");
            EditorUtility.DisplayDialog("Done",
                $"PendingLabel placement: {fixed_} row(s) adjusted.\n" +
                "PendingLabel is now the immediate sibling after StatName in each row.\n" +
                "No sizes, fonts, or positions were changed.",
                "OK");
        }

        // [MenuItem - archived]
        public static void PatchStatValues()
        {
            var root = FindModalRoot();
            if (root == null) return;

            int fixed_ = 0;
            fixed_ += PatchStatRow(root, "ModalPanel/SPSection/StatRow_Strength");
            fixed_ += PatchStatRow(root, "ModalPanel/SPSection/StatRow_ClubControl");
            fixed_ += PatchStatRow(root, "ModalPanel/SPSection/StatRow_Recovery");
            fixed_ += PatchStatRow(root, "ModalPanel/SPSection/StatRow_Stamina");

            MarkDirtyAndSave(root.gameObject);
            Debug.Log($"[LevelUpPatcher] Stat value split: {fixed_} row(s) patched.");
            EditorUtility.DisplayDialog("Done", $"Stat value split: {fixed_} row(s) updated.\nNo other changes made.", "OK");
        }

        // ── Patch A: Row Alignment ───────────────────────────────────────────

        /// <summary>
        /// For a given row:
        ///  1. Sets childForceExpandWidth = false on its HorizontalLayoutGroup.
        ///  2. Inserts a Spacer (flexibleWidth=1) between leftChildName and rightChildName
        ///     if one does not already exist at that position.
        /// Returns 1 if anything changed, 0 if already correct.
        /// </summary>
        private static int PatchRow(Transform root, string rowPath, string leftChildName, string rightChildName)
        {
            var row = root.Find(rowPath);
            if (row == null)
            {
                Debug.LogWarning($"[LevelUpPatcher] '{rowPath}' not found — skipping.");
                return 0;
            }

            bool changed = false;

            // 1. Fix HorizontalLayoutGroup
            var hlg = row.GetComponent<HorizontalLayoutGroup>();
            if (hlg != null && hlg.childForceExpandWidth)
            {
                hlg.childForceExpandWidth = false;
                changed = true;
                Debug.Log($"[LevelUpPatcher] {row.name}: childForceExpandWidth → false");
            }

            // 2. Check if Spacer already exists between left and right children
            var leftChild  = row.Find(leftChildName);
            var rightChild = row.Find(rightChildName);

            if (leftChild == null)
            {
                Debug.LogWarning($"[LevelUpPatcher] '{leftChildName}' not found in {row.name} — skipping spacer insert.");
                return changed ? 1 : 0;
            }
            if (rightChild == null)
            {
                Debug.LogWarning($"[LevelUpPatcher] '{rightChildName}' not found in {row.name} — skipping spacer insert.");
                return changed ? 1 : 0;
            }

            int leftIndex  = leftChild.GetSiblingIndex();
            int rightIndex = rightChild.GetSiblingIndex();

            // Check if a Spacer already sits between them
            bool spacerExists = false;
            for (int i = leftIndex + 1; i < rightIndex; i++)
            {
                if (row.GetChild(i).name == "Spacer")
                {
                    spacerExists = true;
                    break;
                }
            }

            if (!spacerExists)
            {
                var spacer = new GameObject("Spacer");
                spacer.transform.SetParent(row, false);
                spacer.AddComponent<RectTransform>();
                spacer.AddComponent<LayoutElement>().flexibleWidth = 1;

                // Place immediately after the left child
                spacer.transform.SetSiblingIndex(leftIndex + 1);
                changed = true;
                Debug.Log($"[LevelUpPatcher] {row.name}: inserted Spacer at index {leftIndex + 1}");
            }
            else
            {
                Debug.Log($"[LevelUpPatcher] {row.name}: Spacer already present — skipped.");
            }

            return changed ? 1 : 0;
        }

        // ── Patch B: PendingLabel Placement ─────────────────────────────────

        /// <summary>
        /// Ensures PendingLabel is the immediate sibling after StatName within the
        /// same parent transform. If they share a parent with a HorizontalLayoutGroup,
        /// PendingLabel will render inline to the right of StatName.
        /// Does NOT change any sizes, fonts, RectTransform values, or component settings.
        /// </summary>
        private static int PatchPendingLabel(Transform root, string rowPath)
        {
            var row = root.Find(rowPath);
            if (row == null)
            {
                Debug.LogWarning($"[LevelUpPatcher] '{rowPath}' not found — skipping.");
                return 0;
            }

            var statName     = row.Find("StatName");
            var pendingLabel = row.Find("PendingLabel");

            if (statName == null)
            {
                Debug.LogWarning($"[LevelUpPatcher] {row.name}: 'StatName' not found — skipping.");
                return 0;
            }
            if (pendingLabel == null)
            {
                Debug.LogWarning($"[LevelUpPatcher] {row.name}: 'PendingLabel' not found — skipping.");
                return 0;
            }

            // Verify they share the same parent (must be in the same HLG to be inline)
            if (statName.parent != pendingLabel.parent)
            {
                Debug.LogWarning($"[LevelUpPatcher] {row.name}: StatName and PendingLabel are in different parents " +
                    $"('{statName.parent.name}' vs '{pendingLabel.parent.name}'). " +
                    "Manual hierarchy fix needed — they must share the same HorizontalLayoutGroup.");
                return 0;
            }

            var hlg = statName.parent.GetComponent<HorizontalLayoutGroup>();
            if (hlg == null)
            {
                Debug.LogWarning($"[LevelUpPatcher] {row.name}: Parent '{statName.parent.name}' has no HorizontalLayoutGroup. " +
                    "PendingLabel can't be inline without one.");
                return 0;
            }

            int nameIndex    = statName.GetSiblingIndex();
            int pendingIndex = pendingLabel.GetSiblingIndex();
            int targetIndex  = nameIndex + 1;

            if (pendingIndex == targetIndex)
            {
                Debug.Log($"[LevelUpPatcher] {row.name}: PendingLabel already at correct sibling index {targetIndex} — skipped.");
                return 0;
            }

            pendingLabel.SetSiblingIndex(targetIndex);
            Debug.Log($"[LevelUpPatcher] {row.name}: Moved PendingLabel from index {pendingIndex} → {targetIndex} (after StatName).");
            return 1;
        }

        // ── Patch D: Split Stat Values ───────────────────────────────────────

        /// <summary>
        /// In a stat row, replaces the single "StatValue" TMP with two siblings:
        ///   StatValueCurrent — keeps the original font size, shows the number
        ///   StatValueMax     — smaller font (original - 2pt), shows "/cap"
        /// Idempotent: if StatValueCurrent already exists the row is skipped.
        /// The original StatValue's font size, color, and sibling index are preserved.
        /// </summary>
        private static int PatchStatRow(Transform root, string rowPath)
        {
            var row = root.Find(rowPath);
            if (row == null)
            {
                Debug.LogWarning($"[LevelUpPatcher] '{rowPath}' not found — skipping.");
                return 0;
            }

            // Already patched?
            if (row.Find("StatValueCurrent") != null)
            {
                Debug.Log($"[LevelUpPatcher] {row.name}: StatValueCurrent already exists — skipped.");
                return 0;
            }

            var statValue = row.Find("StatValue");
            if (statValue == null)
            {
                Debug.LogWarning($"[LevelUpPatcher] {row.name}: 'StatValue' not found — skipping.");
                return 0;
            }

            // Capture settings from the original before destroying it
            var origTMP    = statValue.GetComponent<TextMeshProUGUI>();
            float origSize = origTMP != null ? origTMP.fontSize       : 13f;
            Color origColor= origTMP != null ? origTMP.color          : Color.white;
            int   sibIndex = statValue.GetSiblingIndex();

            // Also capture LayoutElement preferred widths if present
            var origLE = statValue.GetComponent<LayoutElement>();
            float origPrefW = origLE != null ? origLE.preferredWidth  : 56f;

            // Destroy the old combined field
            Object.DestroyImmediate(statValue.gameObject);

            // StatValueCurrent — e.g. "10"
            var currentGO  = new GameObject("StatValueCurrent");
            currentGO.transform.SetParent(row, false);
            currentGO.AddComponent<RectTransform>();
            var currentTMP = currentGO.AddComponent<TextMeshProUGUI>();
            currentTMP.text      = "0";
            currentTMP.fontSize  = origSize;
            currentTMP.color     = origColor;
            currentTMP.alignment = TextAlignmentOptions.Right;
            var currentLE        = currentGO.AddComponent<LayoutElement>();
            currentLE.preferredWidth = Mathf.Max(origPrefW * 0.5f, 26f);
            currentGO.transform.SetSiblingIndex(sibIndex);

            // StatValueMax — e.g. "/25"
            var maxGO  = new GameObject("StatValueMax");
            maxGO.transform.SetParent(row, false);
            maxGO.AddComponent<RectTransform>();
            var maxTMP = maxGO.AddComponent<TextMeshProUGUI>();
            maxTMP.text      = "/0";
            maxTMP.fontSize  = Mathf.Max(origSize - 2f, 9f);  // 2pt smaller
            maxTMP.color     = origColor;
            maxTMP.alignment = TextAlignmentOptions.Right;
            var maxLE        = maxGO.AddComponent<LayoutElement>();
            maxLE.preferredWidth = Mathf.Max(origPrefW * 0.6f, 30f);
            maxGO.transform.SetSiblingIndex(sibIndex + 1);

            Debug.Log($"[LevelUpPatcher] {row.name}: StatValue split → StatValueCurrent (font {origSize}) + StatValueMax (font {maxTMP.fontSize})");
            return 1;
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static Transform FindModalRoot()
        {
            var go = GameObject.Find("LevelUpModal");
            if (go != null) return go.transform;

            var canvas = GameObject.Find("Canvas");
            if (canvas != null)
            {
                var nested = canvas.transform.Find("ScreensRoot/RosterScreen/LevelUpModal");
                if (nested != null) return nested;
            }

            Debug.LogError("[LevelUpPatcher] LevelUpModal not found in scene. Open ShellScene and make sure the modal exists.");
            EditorUtility.DisplayDialog("Error", "LevelUpModal not found in scene.", "OK");
            return null;
        }

        private static void MarkDirtyAndSave(GameObject go)
        {
            EditorUtility.SetDirty(go);
            EditorSceneManager.MarkSceneDirty(go.scene);
        }
    }
}
#endif
