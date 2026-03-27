#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Golfin.Inventory.Editor
{
    /// <summary>
    /// Clones ClubDetailPanel and adapts it for balls:
    /// - Renames ClubImage → BallImage, ClubNameText → BallNameText
    /// - Replaces RarityLevelRow with OwnedPanel (OwnedLabel + QuantityText)
    /// - Renames stat rows: Accuracy→Rebound, LieResistance→WindResistance, Loft→Roll, Durability→Spin
    /// - Deletes DistanceRow, ButtonsPanel, EquipButton, SwapButton, BagLabel, CloseCompareButton, CompareRightPanel
    /// - Swaps ClubDetailPanel component → BallDetailPanel
    ///
    /// Run: GOLFIN/Setup/Build Ball Detail Panel
    /// Then run: GOLFIN/Wire/Ball Detail Panel
    /// </summary>
    public static class BallDetailPanelBuilder
    {
        [MenuItem("GOLFIN/Setup/Build Ball Detail Panel")]
        public static void Run()
        {
            // ── Find ClubDetailPanel ──────────────────────────────────────────
            var clubPanelGO = FindInScene("ClubDetailPanel");
            if (clubPanelGO == null)
            {
                EditorUtility.DisplayDialog("Build Ball Detail Panel",
                    "ClubDetailPanel not found in scene.\n\nBuild Club Phase C first.", "OK");
                return;
            }

            // ── Remove existing BallDetailPanel if any ────────────────────────
            var existing = FindInScene("BallDetailPanel");
            if (existing != null)
            {
                bool rebuild = EditorUtility.DisplayDialog("Build Ball Detail Panel",
                    "BallDetailPanel already exists in scene. Rebuild it?", "Rebuild", "Cancel");
                if (!rebuild) return;
                Object.DestroyImmediate(existing);
            }

            // ── Duplicate ClubDetailPanel ─────────────────────────────────────
            var ballPanelGO = Object.Instantiate(clubPanelGO, clubPanelGO.transform.parent);
            ballPanelGO.name = "BallDetailPanel";

            // Place it right after ClubDetailPanel in the hierarchy
            ballPanelGO.transform.SetSiblingIndex(clubPanelGO.transform.GetSiblingIndex() + 1);

            var root = ballPanelGO.transform;

            // ── Swap component: ClubDetailPanel → BallDetailPanel ─────────────
            var clubComp = ballPanelGO.GetComponent<ClubDetailPanel>();
            if (clubComp != null) Object.DestroyImmediate(clubComp);
            ballPanelGO.AddComponent<BallDetailPanel>();

            // ── LeftPanel ─────────────────────────────────────────────────────
            var leftPanel = root.Find("LeftPanel");
            if (leftPanel != null)
                RenameChild(leftPanel, "ClubImage", "BallImage");
            else
                Debug.LogWarning("[BallDetailPanelBuilder] LeftPanel not found.");

            // ── RightPanel ────────────────────────────────────────────────────
            var rightPanel = root.Find("RightPanel");
            if (rightPanel != null)
            {
                // ClubNameText → BallNameText
                RenameChild(rightPanel, "ClubNameText", "BallNameText");

                // RarityLevelRow → OwnedPanel with OwnedLabel + QuantityText
                BuildOwnedPanel(rightPanel);

                // Stat rows
                var statsPanel = rightPanel.Find("StatsPanel");
                if (statsPanel != null)
                {
                    RenameChild(statsPanel, "AccuracyRow",     "ReboundRow");
                    RenameChild(statsPanel, "LieResistanceRow","WindResistanceRow");
                    RenameChild(statsPanel, "LoftRow",         "RollRow");
                    RenameChild(statsPanel, "DurabilityRow",   "SpinRow");
                    DestroyChild(statsPanel, "DistanceRow");
                }
                else
                {
                    Debug.LogWarning("[BallDetailPanelBuilder] StatsPanel not found.");
                }

                // Remove club-only UI elements
                DestroyChild(rightPanel, "ButtonsPanel");
                DestroyChild(rightPanel, "BagLabel");
                DestroyChild(rightPanel, "EquipButton");
                DestroyChild(rightPanel, "SwapButton");
                DestroyChild(rightPanel, "CloseCompareButton");
            }
            else
            {
                Debug.LogWarning("[BallDetailPanelBuilder] RightPanel not found.");
            }

            // ── Remove compare panel (Phase H8) ──────────────────────────────
            DestroyChild(root, "CompareRightPanel");
            DestroyChild(root, "VerticalDivider");

            // ── Finish ────────────────────────────────────────────────────────
            EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());

            Debug.Log("[BallDetailPanelBuilder] ✓ BallDetailPanel built.");
            EditorUtility.DisplayDialog("Build Ball Detail Panel",
                "BallDetailPanel created!\n\n" +
                "Next step: GOLFIN/Wire/Ball Detail Panel\n\n" +
                "Also assign:\n" +
                "• BallThumbnailCard prefab → BallCarouselController.ballCardPrefab",
                "OK");
        }

        // ── OwnedPanel construction ────────────────────────────────────────────
        // RarityLevelRow has: RarityLabel  +  LevelPanel (LevelText, LevelTextMax)
        // Target:             OwnedLabel   +  QuantityText  (flat, no LevelPanel wrapper)

        private static void BuildOwnedPanel(Transform rightPanel)
        {
            var rarityRow = rightPanel.Find("RarityLevelRow");
            if (rarityRow == null)
            {
                Debug.LogWarning("[BallDetailPanelBuilder] RarityLevelRow not found.");
                return;
            }

            rarityRow.name = "OwnedPanel";

            // Rename RarityLabel → OwnedLabel
            RenameChild(rarityRow, "RarityLabel", "OwnedLabel");

            // Pull LevelText out of LevelPanel, rename it QuantityText
            var levelPanel = rarityRow.Find("LevelPanel");
            if (levelPanel != null)
            {
                var levelText = levelPanel.Find("LevelText");
                if (levelText != null)
                {
                    levelText.name = "QuantityText";
                    levelText.SetParent(rarityRow, false);
                }
                Object.DestroyImmediate(levelPanel.gameObject);
            }
            else
            {
                // Fallback: just rename LevelText if it's already a direct child
                RenameChild(rarityRow, "LevelText", "QuantityText");
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static void RenameChild(Transform parent, string from, string to)
        {
            var t = parent.Find(from);
            if (t != null)
                t.name = to;
            else
                Debug.LogWarning($"[BallDetailPanelBuilder] '{from}' not found under '{parent.name}'.");
        }

        private static void DestroyChild(Transform parent, string childName)
        {
            var t = parent.Find(childName);
            if (t != null) Object.DestroyImmediate(t.gameObject);
            // Silently skip if not found (ok — some may already be gone)
        }

        private static GameObject? FindInScene(string name)
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            foreach (var rootGO in scene.GetRootGameObjects())
            {
                var t = FindTransformByName(rootGO.transform, name);
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
    }
}
#endif
