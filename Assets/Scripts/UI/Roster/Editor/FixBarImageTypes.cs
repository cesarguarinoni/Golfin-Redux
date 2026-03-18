#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Golfin.Roster.Editor
{
    /// <summary>
    /// One-shot fix: sets every stat bar Image to Filled / Horizontal / Left.
    /// Covers all 4 bars in DetailPanel and all 8 bars (blue + orange) in LevelUpModal.
    /// Run once from GOLFIN > Fix Bar Image Types, then this script can be deleted.
    /// </summary>
    public static class FixBarImageTypes
    {
        [MenuItem("GOLFIN/Fix Bar Image Types")]
        public static void Fix()
        {
            int fixed_ = 0, missing = 0;

            // ── DetailPanel bars (4) ─────────────────────────────────────────
            // Path: DetailPanel > RightPanel > CharacterStatsPanel > CharacterStats{N} > Name+Bar > Bar
            var detailPanel = GameObject.Find("DetailPanel");
            if (detailPanel == null)
                Debug.LogWarning("[FixBars] DetailPanel not found — skipping DetailPanel bars.");
            else
            {
                string[] statNodes = { "CharacterStats1", "CharacterStats2", "CharacterStats3", "CharacterStats4" };
                var statsPanel = detailPanel.transform.Find("RightPanel/CharacterStatsPanel");
                if (statsPanel == null)
                {
                    Debug.LogWarning("[FixBars] RightPanel/CharacterStatsPanel not found under DetailPanel.");
                    missing += 4;
                }
                else
                {
                    foreach (var node in statNodes)
                    {
                        var barT = statsPanel.Find($"{node}/Name+Bar/Bar");
                        if (barT == null) { Debug.LogWarning($"[FixBars] {node}/Name+Bar/Bar not found."); missing++; continue; }
                        fixed_ += SetFilled(barT, $"DetailPanel/{node}/Bar");
                    }
                }
            }

            // ── LevelUpModal bars (8: 4 blue + 4 orange) ────────────────────
            // Path: LevelUpModal > ModalPanel > SPSection > StatRow_{X} > StatBar > Bar / BarPending
            var modal = GameObject.Find("LevelUpModal");
            if (modal == null)
                Debug.LogWarning("[FixBars] LevelUpModal not found — skipping modal bars.");
            else
            {
                var spSection = modal.transform.Find("ModalPanel/SPSection");
                if (spSection == null)
                {
                    Debug.LogWarning("[FixBars] ModalPanel/SPSection not found under LevelUpModal.");
                    missing += 8;
                }
                else
                {
                    string[] statRows = { "StatRow_Strength", "StatRow_ClubControl", "StatRow_Recovery", "StatRow_Stamina" };
                    foreach (var row in statRows)
                    {
                        var barT        = spSection.Find($"{row}/StatBar/Bar");
                        var barPendingT = spSection.Find($"{row}/StatBar/BarPending");

                        if (barT == null)        { Debug.LogWarning($"[FixBars] {row}/StatBar/Bar not found.");        missing++; }
                        else                       fixed_ += SetFilled(barT,        $"Modal/{row}/Bar");

                        if (barPendingT == null) { Debug.LogWarning($"[FixBars] {row}/StatBar/BarPending not found."); missing++; }
                        else                       fixed_ += SetFilled(barPendingT, $"Modal/{row}/BarPending");
                    }
                }
            }

            // ── Mark scene dirty ─────────────────────────────────────────────
            if (fixed_ > 0)
                EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());

            Debug.Log($"[FixBars] Done — {fixed_} bars fixed, {missing} not found.");
            if (missing == 0)
                EditorUtility.DisplayDialog("Fix Bar Image Types",
                    $"All {fixed_} stat bar Images set to Filled / Horizontal / Left.", "OK");
            else
                EditorUtility.DisplayDialog("Fix Bar Image Types — Partial",
                    $"{fixed_} fixed, {missing} not found.\nCheck Console for details.", "OK");
        }

        private static int SetFilled(Transform t, string label)
        {
            var img = t.GetComponent<Image>();
            if (img == null)
            {
                Debug.LogWarning($"[FixBars] No Image component on '{label}'.");
                return 0;
            }

            img.type       = Image.Type.Filled;
            img.fillMethod = Image.FillMethod.Horizontal;
            img.fillOrigin = 0; // Left
            EditorUtility.SetDirty(img);
            Debug.Log($"[FixBars] Fixed: {label}");
            return 1;
        }
    }
}
#endif
