#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Golfin.Inventory.Editor
{
    /// <summary>
    /// Builds the ItemUseClubCard prefab from scratch.
    /// This is a tall club card (180 × 410) with portrait, stats, and USE REPAIR KIT button.
    ///
    /// Layout:
    ///   ItemUseClubCard (root — VerticalLayoutGroup)
    ///     CardTop         — portrait + rarity bg + badges
    ///     NameText        — "DRIVER\nG&F"
    ///     StatsPanel      — 5 stat rows (Power/Accuracy/LieRes/Loft/Durability)
    ///       StatRow_Power / StatRow_Accuracy / StatRow_LieRes / StatRow_Loft / StatRow_Durability
    ///         Bar (Image — filled)
    ///         StatNum (TMP)
    ///     DistanceRow     — distance value
    ///     ButtonRow       — LevelUpBtn + RepairBtn (both disabled)
    ///     UseRepairKitBtn — full-width gold button
    ///
    /// Run: GOLFIN/Build/Item Use Club Card Prefab
    /// </summary>
    public static class ItemUseClubCardBuilder
    {
        private const string DEST_PREFAB  = "Assets/Prefabs/UI/Inventory/ItemUseClubCard.prefab";
        private const string DEST_FOLDER  = "Assets/Prefabs/UI/Inventory";

        [MenuItem("GOLFIN/Build/Item Use Club Card Prefab")]
        public static void Run()
        {
            if (!AssetDatabase.IsValidFolder(DEST_FOLDER))
                AssetDatabase.CreateFolder("Assets/Prefabs/UI", "Inventory");

            // Build in scene, then save as prefab
            var root = BuildHierarchy();
            bool success = false;

            try
            {
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, DEST_PREFAB);
                success = prefab != null;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }

            AssetDatabase.Refresh();

            if (success)
            {
                Debug.Log($"[ItemUseClubCardBuilder] ✓ Prefab saved to {DEST_PREFAB}");
                EditorUtility.DisplayDialog("Item Use Club Card Builder",
                    "ItemUseClubCard.prefab created!\n\n" +
                    "Next: GOLFIN/Build/Item Use Modal\n" +
                    "Assign the prefab to ItemUseModalController.clubCardPrefab.", "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("Item Use Club Card Builder", "Failed to save prefab.", "OK");
            }
        }

        private static GameObject BuildHierarchy()
        {
            // ── Root ──────────────────────────────────────────────────────────
            var root = new GameObject("ItemUseClubCard", typeof(RectTransform));

            var le = root.AddComponent<LayoutElement>();
            le.preferredWidth  = 180f;
            le.preferredHeight = 410f;

            var vlg = root.AddComponent<VerticalLayoutGroup>();
            vlg.spacing            = 4f;
            vlg.padding            = new RectOffset(6, 6, 6, 6);
            vlg.childForceExpandWidth  = true;
            vlg.childForceExpandHeight = false;
            vlg.childAlignment     = TextAnchor.UpperCenter;

            // Rarity background on root
            var rootImg = root.AddComponent<Image>();
            rootImg.color         = new Color(0.15f, 0.15f, 0.2f, 1f);
            rootImg.raycastTarget = false;

            var card = root.AddComponent<ItemUseClubCard>();

            // ── CardTop ───────────────────────────────────────────────────────
            var cardTop = MakeChild(root, "CardTop");
            var cardTopLE = cardTop.AddComponent<LayoutElement>();
            cardTopLE.preferredHeight = 140f;
            cardTopLE.flexibleWidth   = 1f;

            // Background (rarity sprite)
            var bg = cardTop.AddComponent<Image>();
            bg.raycastTarget = false;

            // Portrait (centered child)
            var portrait = MakeChild(cardTop, "Portrait");
            var portraitRT = portrait.GetComponent<RectTransform>();
            portraitRT.anchorMin        = new Vector2(0.1f, 0.1f);
            portraitRT.anchorMax        = new Vector2(0.9f, 0.9f);
            portraitRT.sizeDelta        = Vector2.zero;
            portraitRT.anchoredPosition = Vector2.zero;
            var portraitImg = portrait.AddComponent<Image>();
            portraitImg.preserveAspect = true;
            portraitImg.raycastTarget  = false;

            // RarityBadge (top-left)
            var rarityBadge = MakeChild(cardTop, "RarityBadge");
            var rarityBadgeRT = rarityBadge.GetComponent<RectTransform>();
            rarityBadgeRT.anchorMin        = new Vector2(0f, 1f);
            rarityBadgeRT.anchorMax        = new Vector2(0f, 1f);
            rarityBadgeRT.pivot            = new Vector2(0f, 1f);
            rarityBadgeRT.sizeDelta        = new Vector2(24f, 20f);
            rarityBadgeRT.anchoredPosition = new Vector2(4f, -4f);
            var rarityBadgeTMP = rarityBadge.AddComponent<TextMeshProUGUI>();
            rarityBadgeTMP.text      = "R";
            rarityBadgeTMP.fontSize  = 13f;
            rarityBadgeTMP.fontStyle = FontStyles.Bold;
            rarityBadgeTMP.alignment = TextAlignmentOptions.Center;

            // LevelBadge (top-right)
            var levelBadge = MakeChild(cardTop, "LevelBadge");
            var levelBadgeRT = levelBadge.GetComponent<RectTransform>();
            levelBadgeRT.anchorMin        = new Vector2(1f, 1f);
            levelBadgeRT.anchorMax        = new Vector2(1f, 1f);
            levelBadgeRT.pivot            = new Vector2(1f, 1f);
            levelBadgeRT.sizeDelta        = new Vector2(40f, 20f);
            levelBadgeRT.anchoredPosition = new Vector2(-4f, -4f);
            var levelBadgeTMP = levelBadge.AddComponent<TextMeshProUGUI>();
            levelBadgeTMP.text      = "Lv10";
            levelBadgeTMP.fontSize  = 11f;
            levelBadgeTMP.alignment = TextAlignmentOptions.Right;
            levelBadgeTMP.color     = Color.white;

            // ── NameText ──────────────────────────────────────────────────────
            var nameGO = MakeChild(root, "NameText");
            var nameLE = nameGO.AddComponent<LayoutElement>();
            nameLE.preferredHeight = 32f;
            nameLE.flexibleWidth   = 1f;
            var nameTMP = nameGO.AddComponent<TextMeshProUGUI>();
            nameTMP.text      = "DRIVER\nG&F";
            nameTMP.fontSize  = 10f;
            nameTMP.fontStyle = FontStyles.Bold;
            nameTMP.alignment = TextAlignmentOptions.Center;
            nameTMP.color     = Color.white;

            // ── StatsPanel ────────────────────────────────────────────────────
            var statsPanel = MakeChild(root, "StatsPanel");
            var statsPanelLE = statsPanel.AddComponent<LayoutElement>();
            statsPanelLE.preferredHeight = 120f;
            statsPanelLE.flexibleWidth   = 1f;
            var statsVLG = statsPanel.AddComponent<VerticalLayoutGroup>();
            statsVLG.spacing            = 2f;
            statsVLG.childForceExpandWidth  = true;
            statsVLG.childForceExpandHeight = false;

            string[] statNames = { "Power", "Accuracy", "LieRes", "Loft", "Durability" };
            foreach (var stat in statNames)
                BuildStatRow(statsPanel, stat);

            // ── DistanceRow ───────────────────────────────────────────────────
            var distRow = MakeChild(root, "DistanceRow");
            var distRowLE = distRow.AddComponent<LayoutElement>();
            distRowLE.preferredHeight = 18f;
            distRowLE.flexibleWidth   = 1f;
            var distHLG = distRow.AddComponent<HorizontalLayoutGroup>();
            distHLG.spacing = 4f;
            distHLG.childForceExpandWidth = false;

            var distLabel = MakeChild(distRow, "DistLabel");
            var distLabelTMP = distLabel.AddComponent<TextMeshProUGUI>();
            distLabelTMP.text     = "DIST";
            distLabelTMP.fontSize = 9f;
            distLabelTMP.color    = new Color(0.7f, 0.7f, 0.7f);
            distLabel.AddComponent<LayoutElement>().preferredWidth = 40f;

            var distVal = MakeChild(distRow, "DistanceValue");
            var distValTMP = distVal.AddComponent<TextMeshProUGUI>();
            distValTMP.text      = "150 yd";
            distValTMP.fontSize  = 9f;
            distValTMP.alignment = TextAlignmentOptions.Right;
            distValTMP.color     = Color.white;
            distVal.AddComponent<LayoutElement>().flexibleWidth = 1f;

            // ── ButtonRow (LevelUp + Repair, both disabled) ───────────────────
            var btnRow = MakeChild(root, "ButtonRow");
            var btnRowLE = btnRow.AddComponent<LayoutElement>();
            btnRowLE.preferredHeight = 28f;
            btnRowLE.flexibleWidth   = 1f;
            var btnHLG = btnRow.AddComponent<HorizontalLayoutGroup>();
            btnHLG.spacing = 4f;
            btnHLG.childForceExpandWidth  = true;
            btnHLG.childForceExpandHeight = true;

            BuildSmallButton(btnRow, "LevelUpBtn",  "LEVEL UP", new Color(0.4f, 0.4f, 0.5f));
            BuildSmallButton(btnRow, "RepairBtn",   "REPAIR",   new Color(0.4f, 0.4f, 0.5f));

            // ── UseRepairKitBtn (full-width, gold) ────────────────────────────
            var useBtn = MakeChild(root, "UseRepairKitBtn");
            var useBtnLE = useBtn.AddComponent<LayoutElement>();
            useBtnLE.preferredHeight = 32f;
            useBtnLE.flexibleWidth   = 1f;
            var useBtnImg = useBtn.AddComponent<Image>();
            useBtnImg.color = new Color(1f, 0.8f, 0.2f, 1f); // gold
            var useBtnComp = useBtn.AddComponent<Button>();
            useBtnComp.targetGraphic = useBtnImg;
            var useBtnLabel = MakeChild(useBtn, "UseRepairKitText");
            var useBtnLabelRT = useBtnLabel.GetComponent<RectTransform>();
            useBtnLabelRT.anchorMin  = Vector2.zero;
            useBtnLabelRT.anchorMax  = Vector2.one;
            useBtnLabelRT.sizeDelta  = Vector2.zero;
            var useBtnTMP = useBtnLabel.AddComponent<TextMeshProUGUI>();
            useBtnTMP.text      = "USE REPAIR KIT";
            useBtnTMP.fontSize  = 9f;
            useBtnTMP.fontStyle = FontStyles.Bold;
            useBtnTMP.color     = Color.black;
            useBtnTMP.alignment = TextAlignmentOptions.Center;

            // ── Wire SerializeFields ──────────────────────────────────────────
            var so = new SerializedObject(card);
            so.FindProperty("backgroundImage").objectReferenceValue  = bg;
            so.FindProperty("portraitImage").objectReferenceValue    = portraitImg;
            so.FindProperty("nameText").objectReferenceValue         = nameTMP;
            so.FindProperty("rarityBadgeText").objectReferenceValue  = rarityBadgeTMP;
            so.FindProperty("levelText").objectReferenceValue        = levelBadgeTMP;
            so.FindProperty("distanceValue").objectReferenceValue    = distValTMP;
            so.FindProperty("levelUpButton").objectReferenceValue    = GetButton(root, "ButtonRow/LevelUpBtn");
            so.FindProperty("repairButton").objectReferenceValue     = GetButton(root, "ButtonRow/RepairBtn");
            so.FindProperty("useRepairKitButton").objectReferenceValue = useBtnComp;
            so.FindProperty("useRepairKitText").objectReferenceValue   = useBtnTMP;
            WireStatBars(so, root);
            so.ApplyModifiedProperties();

            return root;
        }

        private static void BuildStatRow(GameObject parent, string statName)
        {
            var row = MakeChild(parent, $"StatRow_{statName}");
            var rowLE = row.AddComponent<LayoutElement>();
            rowLE.preferredHeight = 18f;
            rowLE.flexibleWidth   = 1f;
            var rowHLG = row.AddComponent<HorizontalLayoutGroup>();
            rowHLG.spacing = 3f;
            rowHLG.childForceExpandWidth  = false;
            rowHLG.childForceExpandHeight = true;
            rowHLG.childAlignment = TextAnchor.MiddleLeft;

            // Bar (filled image)
            var barGO = MakeChild(row, "Bar");
            var barLE = barGO.AddComponent<LayoutElement>();
            barLE.preferredWidth  = 80f;
            barLE.flexibleWidth   = 1f;
            var barImg = barGO.AddComponent<Image>();
            barImg.color         = new Color(0.2f, 0.5f, 0.9f, 1f);
            barImg.type          = Image.Type.Filled;
            barImg.fillMethod    = Image.FillMethod.Horizontal;
            barImg.fillOrigin    = 0;
            barImg.fillAmount    = 0.5f;
            barImg.raycastTarget = false;

            // StatNum
            var numGO = MakeChild(row, "StatNum");
            numGO.AddComponent<LayoutElement>().preferredWidth = 28f;
            var numTMP = numGO.AddComponent<TextMeshProUGUI>();
            numTMP.text      = "50";
            numTMP.fontSize  = 9f;
            numTMP.alignment = TextAlignmentOptions.Right;
            numTMP.color     = Color.white;
        }

        private static void BuildSmallButton(GameObject parent, string goName, string label, Color color)
        {
            var btn = MakeChild(parent, goName);
            var btnImg = btn.AddComponent<Image>();
            btnImg.color = color;
            var btnComp = btn.AddComponent<Button>();
            btnComp.targetGraphic = btnImg;
            btnComp.interactable  = false;

            var labelGO = MakeChild(btn, "Text");
            var labelRT = labelGO.GetComponent<RectTransform>();
            labelRT.anchorMin = Vector2.zero;
            labelRT.anchorMax = Vector2.one;
            labelRT.sizeDelta = Vector2.zero;
            var labelTMP = labelGO.AddComponent<TextMeshProUGUI>();
            labelTMP.text      = label;
            labelTMP.fontSize  = 8f;
            labelTMP.fontStyle = FontStyles.Bold;
            labelTMP.color     = new Color(0.6f, 0.6f, 0.6f);
            labelTMP.alignment = TextAlignmentOptions.Center;
        }

        private static void WireStatBars(SerializedObject so, GameObject root)
        {
            var stats = new[]
            {
                ("statBarPower",     "statNumPower",     "StatsPanel/StatRow_Power/Bar",          "StatsPanel/StatRow_Power/StatNum"),
                ("statBarAccuracy",  "statNumAccuracy",  "StatsPanel/StatRow_Accuracy/Bar",       "StatsPanel/StatRow_Accuracy/StatNum"),
                ("statBarLieRes",    "statNumLieRes",    "StatsPanel/StatRow_LieRes/Bar",         "StatsPanel/StatRow_LieRes/StatNum"),
                ("statBarLoft",      "statNumLoft",      "StatsPanel/StatRow_Loft/Bar",           "StatsPanel/StatRow_Loft/StatNum"),
                ("statBarDurability","statNumDurability","StatsPanel/StatRow_Durability/Bar",     "StatsPanel/StatRow_Durability/StatNum"),
            };

            foreach (var (barProp, numProp, barPath, numPath) in stats)
            {
                var barT = root.transform.Find(barPath);
                var numT = root.transform.Find(numPath);
                if (barT != null) so.FindProperty(barProp).objectReferenceValue = barT.GetComponent<Image>();
                if (numT != null) so.FindProperty(numProp).objectReferenceValue = numT.GetComponent<TextMeshProUGUI>();
            }
        }

        private static Button? GetButton(GameObject root, string path)
        {
            var t = root.transform.Find(path);
            return t == null ? null : t.GetComponent<Button>();
        }

        private static GameObject MakeChild(GameObject parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent.transform, false);
            return go;
        }
    }
}
#endif
