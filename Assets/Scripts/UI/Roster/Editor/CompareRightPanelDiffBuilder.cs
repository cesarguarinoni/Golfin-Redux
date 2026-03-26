#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Golfin.Roster.Editor
{
    /// <summary>
    /// Adds DiffLabel TMP children to the four character stat rows inside
    /// CompareRightPanel/CompareInfoPanel/CharacterStatsPanel.
    ///
    /// Run AFTER "GOLFIN/Build/Roster Compare Right Panel" has created CompareInfoPanel.
    /// Run BEFORE "GOLFIN/Wire/Roster Compare Panel" so the wire step can find DiffLabel.
    /// </summary>
    public static class CompareRightPanelDiffBuilder
    {
        [MenuItem("GOLFIN/Build/Roster Compare Diff Labels")]
        public static void Build()
        {
            // Find CompareInfoPanel
            var detailPanel = FindDetailPanel();
            if (detailPanel == null)
            {
                Debug.LogError("[CompareRightPanelDiffBuilder] DetailPanel not found in scene.");
                return;
            }

            var statsPanel = detailPanel.Find(
                "CompareRightPanel/CompareInfoPanel/CharacterStatsPanel");
            if (statsPanel == null)
            {
                Debug.LogError("[CompareRightPanelDiffBuilder] CharacterStatsPanel not found. " +
                               "Run 'GOLFIN/Build/Roster Compare Right Panel' first.");
                return;
            }

            string[] rowNames = { "CharacterStats1", "CharacterStats2",
                                  "CharacterStats3", "CharacterStats4" };
            int added   = 0;
            int skipped = 0;

            foreach (var rowName in rowNames)
            {
                var row = statsPanel.Find(rowName);
                if (row == null)
                {
                    Debug.LogWarning($"[CompareRightPanelDiffBuilder] '{rowName}' not found — skipping.");
                    continue;
                }

                // Skip if DiffLabel already exists
                if (row.Find("DiffLabel") != null)
                {
                    skipped++;
                    continue;
                }

                var diffGO = new GameObject("DiffLabel");
                diffGO.transform.SetParent(row, false);
                diffGO.transform.SetSiblingIndex(1); // after Name+Bar, before StatNumber

                var le = diffGO.AddComponent<LayoutElement>();
                le.preferredWidth = 45f;

                var tmp = diffGO.AddComponent<TextMeshProUGUI>();
                tmp.text      = "";
                tmp.fontSize  = 14f;
                tmp.fontStyle = FontStyles.Bold;
                tmp.color     = new Color(0.2f, 0.8f, 0.3f, 1f); // placeholder green
                tmp.alignment = TextAlignmentOptions.Left;

                diffGO.SetActive(false);

                EditorUtility.SetDirty(row.gameObject);
                added++;
            }

            EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());

            Debug.Log($"[CompareRightPanelDiffBuilder] Done — {added} DiffLabel(s) added, " +
                      $"{skipped} already existed.");

            if (added > 0)
                Debug.Log("[CompareRightPanelDiffBuilder] Next: run 'GOLFIN/Wire/Roster Compare Panel'.");
        }

        private static Transform? FindDetailPanel()
        {
            // GameObject.Find misses inactive objects — search all scene GameObjects instead
            foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (go.name == "DetailPanel" && go.scene.isLoaded)
                    return go.transform;
            }
            return null;
        }
    }
}
#endif
