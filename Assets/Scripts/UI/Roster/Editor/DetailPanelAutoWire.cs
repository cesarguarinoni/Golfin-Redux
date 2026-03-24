using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;

namespace Golfin.Roster.Editor
{
    public static class DetailPanelAutoWire
    {
        [MenuItem("GOLFIN/Wire/Roster Detail Panel")]
        public static void WireDetailPanel()
        {
            // Find DetailPanel in scene
            var detailPanel = GameObject.Find("DetailPanel");
            if (detailPanel == null)
            {
                // Try nested path
                detailPanel = GameObject.Find("RosterScreen/CarouselSection/DetailPanel");
            }
            if (detailPanel == null)
            {
                Debug.LogError("[AutoWire] DetailPanel not found in scene. Make sure the scene is open and DetailPanel exists.");
                return;
            }

            // Get or add the component
            var panel = detailPanel.GetComponent<CharacterDetailPanel>();
            if (panel == null)
            {
                Debug.LogError("[AutoWire] CharacterDetailPanel component not found on DetailPanel. Add it first.");
                return;
            }

            var so = new SerializedObject(panel);
            var root = detailPanel.transform;
            int wired = 0;
            int failed = 0;

            // --- Portrait ---
            wired += WireImage(so, "characterImage", root, "LeftPanel/Character", ref failed);

            // --- Header ---
            wired += WireTMP(so, "characterNameText", root, "RightPanel/CharacterNamePanel/CharacterNameText", ref failed);

            // --- Rarity & Level (3 TMP children of RarityRow) ---
            var rarityRow = root.Find("RightPanel/RarityPanel/RarityRow");
            if (rarityRow != null && rarityRow.childCount >= 3)
            {
                wired += WireTMPDirect(so, "rarityLabel", rarityRow.GetChild(0), ref failed);
                wired += WireTMPDirect(so, "currentLevelText", rarityRow.GetChild(1), ref failed);
                wired += WireTMPDirect(so, "maxLevelText", rarityRow.GetChild(2), ref failed);
            }
            else
            {
                Debug.LogWarning("[AutoWire] RarityRow not found or has fewer than 3 children. Wire rarityLabel, currentLevelText, maxLevelText manually.");
                failed += 3;
            }

            // --- Stat Bars ---
            // Strength (CharacterStats1)
            wired += WireTMP(so, "strengthName", root, "RightPanel/CharacterStatsPanel/CharacterStats1/Name+Bar/StatsName", ref failed);
            wired += WireImage(so, "strengthBar", root, "RightPanel/CharacterStatsPanel/CharacterStats1/Name+Bar/Bar", ref failed);
            wired += WireTMP(so, "strengthNumber", root, "RightPanel/CharacterStatsPanel/CharacterStats1/StatNumber", ref failed);

            // Club Control (CharacterStats2)
            wired += WireTMP(so, "clubControlName", root, "RightPanel/CharacterStatsPanel/CharacterStats2/Name+Bar/StatsName", ref failed);
            wired += WireImage(so, "clubControlBar", root, "RightPanel/CharacterStatsPanel/CharacterStats2/Name+Bar/Bar", ref failed);
            wired += WireTMP(so, "clubControlNumber", root, "RightPanel/CharacterStatsPanel/CharacterStats2/StatNumber", ref failed);

            // Recovery (CharacterStats3)
            wired += WireTMP(so, "recoveryName", root, "RightPanel/CharacterStatsPanel/CharacterStats3/Name+Bar/StatsName", ref failed);
            wired += WireImage(so, "recoveryBar", root, "RightPanel/CharacterStatsPanel/CharacterStats3/Name+Bar/Bar", ref failed);
            wired += WireTMP(so, "recoveryNumber", root, "RightPanel/CharacterStatsPanel/CharacterStats3/StatNumber", ref failed);

            // Stamina (CharacterStats4)
            wired += WireTMP(so, "staminaName", root, "RightPanel/CharacterStatsPanel/CharacterStats4/Name+Bar/StatsName", ref failed);
            wired += WireImage(so, "staminaBar", root, "RightPanel/CharacterStatsPanel/CharacterStats4/Name+Bar/Bar", ref failed);
            wired += WireTMP(so, "staminaNumber", root, "RightPanel/CharacterStatsPanel/CharacterStats4/StatNumber", ref failed);

            // --- Buttons ---
            wired += WireButton(so, "levelUpButton", root, "RightPanel/ButtonsPanel/LevelUpButton", ref failed);
            wired += WireButton(so, "boostButton", root, "RightPanel/ButtonsPanel/BoostButton", ref failed);
            wired += WireButton(so, "compareButton", root, "RightPanel/CompareButton", ref failed);
            wired += WireButton(so, "selectButton", root, "RightPanel/SelectButton", ref failed);
            wired += WireTMP(so, "selectButtonText", root, "RightPanel/SelectButton/Text (TMP)", ref failed);

            // --- Bio ---
            wired += WireTMP(so, "bioText", root, "RightPanel/BioPanel/BioText", ref failed);

            // --- Carousel (search up the hierarchy) ---
            var carouselObj = detailPanel.GetComponentInParent<CarouselController>();
            if (carouselObj == null)
            {
                // Try finding in scene
                carouselObj = Object.FindObjectOfType<CarouselController>();
            }
            if (carouselObj != null)
            {
                var prop = so.FindProperty("carousel");
                if (prop != null)
                {
                    prop.objectReferenceValue = carouselObj;
                    wired++;
                }
            }
            else
            {
                Debug.LogWarning("[AutoWire] CarouselController not found. Wire 'carousel' manually.");
                failed++;
            }

            // --- Status Icons (leave null, log info) ---
            Debug.Log("[AutoWire] selectedIcon and lowStaminaIcon left null — add icon GameObjects later.");

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(panel);

            Debug.Log($"[AutoWire] Done. {wired} fields wired, {failed} failed. Check warnings above for any manual fixes needed.");
        }

        // --- Helper Methods ---

        private static int WireTMP(SerializedObject so, string fieldName, Transform root, string path, ref int failed)
        {
            var prop = so.FindProperty(fieldName);
            if (prop == null)
            {
                Debug.LogWarning($"[AutoWire] Field '{fieldName}' not found on component.");
                failed++;
                return 0;
            }

            var target = root.Find(path);
            if (target == null)
            {
                Debug.LogWarning($"[AutoWire] Path '{path}' not found for field '{fieldName}'. Wire manually.");
                failed++;
                return 0;
            }

            var tmp = target.GetComponent<TextMeshProUGUI>();
            if (tmp == null)
            {
                Debug.LogWarning($"[AutoWire] No TextMeshProUGUI on '{path}' for field '{fieldName}'. Wire manually.");
                failed++;
                return 0;
            }

            prop.objectReferenceValue = tmp;
            return 1;
        }

        private static int WireTMPDirect(SerializedObject so, string fieldName, Transform target, ref int failed)
        {
            var prop = so.FindProperty(fieldName);
            if (prop == null)
            {
                Debug.LogWarning($"[AutoWire] Field '{fieldName}' not found on component.");
                failed++;
                return 0;
            }

            var tmp = target.GetComponent<TextMeshProUGUI>();
            if (tmp == null)
            {
                Debug.LogWarning($"[AutoWire] No TextMeshProUGUI on '{target.name}' for field '{fieldName}'. Wire manually.");
                failed++;
                return 0;
            }

            prop.objectReferenceValue = tmp;
            return 1;
        }

        private static int WireImage(SerializedObject so, string fieldName, Transform root, string path, ref int failed)
        {
            var prop = so.FindProperty(fieldName);
            if (prop == null)
            {
                Debug.LogWarning($"[AutoWire] Field '{fieldName}' not found on component.");
                failed++;
                return 0;
            }

            var target = root.Find(path);
            if (target == null)
            {
                Debug.LogWarning($"[AutoWire] Path '{path}' not found for field '{fieldName}'. Wire manually.");
                failed++;
                return 0;
            }

            var img = target.GetComponent<Image>();
            if (img == null)
            {
                Debug.LogWarning($"[AutoWire] No Image on '{path}' for field '{fieldName}'. Wire manually.");
                failed++;
                return 0;
            }

            prop.objectReferenceValue = img;
            return 1;
        }

        private static int WireButton(SerializedObject so, string fieldName, Transform root, string path, ref int failed)
        {
            var prop = so.FindProperty(fieldName);
            if (prop == null)
            {
                Debug.LogWarning($"[AutoWire] Field '{fieldName}' not found on component.");
                failed++;
                return 0;
            }

            var target = root.Find(path);
            if (target == null)
            {
                Debug.LogWarning($"[AutoWire] Path '{path}' not found for field '{fieldName}'. Wire manually.");
                failed++;
                return 0;
            }

            var btn = target.GetComponent<Button>();
            if (btn == null)
            {
                Debug.LogWarning($"[AutoWire] No Button on '{path}' for field '{fieldName}'. Wire manually.");
                failed++;
                return 0;
            }

            prop.objectReferenceValue = btn;
            return 1;
        }
    }
}
