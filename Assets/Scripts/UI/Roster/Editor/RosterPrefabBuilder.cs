#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;

namespace Golfin.Roster.Editor
{
    /// <summary>
    /// Automated prefab builder for Roster UI components
    /// Creates CharacterThumbnailCard and StatBar prefabs programmatically
    /// </summary>
    public static class RosterPrefabBuilder
    {
        private const string PREFAB_PATH = "Assets/Prefabs/UI/Roster/";
        
        public static void BuildCharacterThumbnailCard()
        {
            Debug.Log("[PrefabBuilder] Building CharacterThumbnailCard prefab...");
            
            // Ensure directory exists
            if (!Directory.Exists(PREFAB_PATH))
            {
                Directory.CreateDirectory(PREFAB_PATH);
            }
            
            // Create root GameObject
            var card = new GameObject("CharacterThumbnailCard");
            
            var rect = card.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(150, 200);
            
            // Button component (must be before LayoutElement to be proper hierarchy)
            var button = card.AddComponent<Button>();
            var buttonImage = card.AddComponent<Image>();
            buttonImage.color = Color.white;
            
            // LayoutElement - tells HorizontalLayoutGroup the preferred size
            var layoutElement = card.AddComponent<LayoutElement>();
            layoutElement.preferredWidth = 150;
            layoutElement.preferredHeight = 200;
            layoutElement.flexibleWidth = 0;
            layoutElement.flexibleHeight = 0;
            Debug.Log("[PrefabBuilder] Added LayoutElement to card");
            
            // Background
            var background = new GameObject("Background");
            background.transform.SetParent(card.transform, false);
            
            var bgRect = background.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;
            bgRect.anchoredPosition = Vector2.zero;
            
            var bgImage = background.AddComponent<Image>();
            bgImage.color = Color.gray; // Default, will be changed based on rarity
            
            // Portrait
            var portrait = new GameObject("Portrait");
            portrait.transform.SetParent(card.transform, false);
            
            var portraitRect = portrait.AddComponent<RectTransform>();
            portraitRect.anchorMin = new Vector2(0.1f, 0.3f);
            portraitRect.anchorMax = new Vector2(0.9f, 0.9f);
            portraitRect.sizeDelta = Vector2.zero;
            portraitRect.anchoredPosition = Vector2.zero;
            
            var portraitImage = portrait.AddComponent<Image>();
            portraitImage.color = Color.white;
            portraitImage.preserveAspect = true;
            
            // Name Label
            var nameLabel = new GameObject("NameLabel");
            nameLabel.transform.SetParent(card.transform, false);
            
            var nameLabelRect = nameLabel.AddComponent<RectTransform>();
            nameLabelRect.anchorMin = new Vector2(0, 0);
            nameLabelRect.anchorMax = new Vector2(1, 0.2f);
            nameLabelRect.sizeDelta = Vector2.zero;
            nameLabelRect.anchoredPosition = Vector2.zero;
            
            var nameText = nameLabel.AddComponent<TextMeshProUGUI>();
            nameText.text = "Elizabeth";
            nameText.fontSize = 16;
            nameText.alignment = TextAlignmentOptions.Center;
            nameText.color = Color.white;
            nameText.fontStyle = FontStyles.Bold;
            
            // Rarity Badge (top-left)
            var rarityBadge = new GameObject("RarityBadge");
            rarityBadge.transform.SetParent(card.transform, false);
            
            var rarityRect = rarityBadge.AddComponent<RectTransform>();
            rarityRect.anchorMin = new Vector2(0, 1);
            rarityRect.anchorMax = new Vector2(0, 1);
            rarityRect.pivot = new Vector2(0, 1);
            rarityRect.sizeDelta = new Vector2(30, 30);
            rarityRect.anchoredPosition = new Vector2(5, -5);
            
            var rarityBg = rarityBadge.AddComponent<Image>();
            rarityBg.color = new Color(0, 0, 0, 0.7f);
            
            var rarityText = new GameObject("Text");
            rarityText.transform.SetParent(rarityBadge.transform, false);
            
            var rarityTextRect = rarityText.AddComponent<RectTransform>();
            rarityTextRect.anchorMin = Vector2.zero;
            rarityTextRect.anchorMax = Vector2.one;
            rarityTextRect.sizeDelta = Vector2.zero;
            rarityTextRect.anchoredPosition = Vector2.zero;
            
            var rarityTMP = rarityText.AddComponent<TextMeshProUGUI>();
            rarityTMP.text = "R";
            rarityTMP.fontSize = 18;
            rarityTMP.alignment = TextAlignmentOptions.Center;
            rarityTMP.color = Color.white;
            rarityTMP.fontStyle = FontStyles.Bold;
            
            // Level Badge (top-right)
            var levelBadge = new GameObject("LevelBadge");
            levelBadge.transform.SetParent(card.transform, false);
            
            var levelRect = levelBadge.AddComponent<RectTransform>();
            levelRect.anchorMin = new Vector2(1, 1);
            levelRect.anchorMax = new Vector2(1, 1);
            levelRect.pivot = new Vector2(1, 1);
            levelRect.sizeDelta = new Vector2(50, 30);
            levelRect.anchoredPosition = new Vector2(-5, -5);
            
            var levelBg = levelBadge.AddComponent<Image>();
            levelBg.color = new Color(0, 0, 0, 0.7f);
            
            var levelText = new GameObject("Text");
            levelText.transform.SetParent(levelBadge.transform, false);
            
            var levelTextRect = levelText.AddComponent<RectTransform>();
            levelTextRect.anchorMin = Vector2.zero;
            levelTextRect.anchorMax = Vector2.one;
            levelTextRect.sizeDelta = Vector2.zero;
            levelTextRect.anchoredPosition = Vector2.zero;
            
            var levelTMP = levelText.AddComponent<TextMeshProUGUI>();
            levelTMP.text = "Lv 1";
            levelTMP.fontSize = 14;
            levelTMP.alignment = TextAlignmentOptions.Center;
            levelTMP.color = Color.white;
            
            // Selection Highlight (border)
            var highlight = new GameObject("SelectionHighlight");
            highlight.transform.SetParent(card.transform, false);
            
            var highlightRect = highlight.AddComponent<RectTransform>();
            highlightRect.anchorMin = Vector2.zero;
            highlightRect.anchorMax = Vector2.one;
            highlightRect.sizeDelta = new Vector2(10, 10); // Slightly larger than card
            highlightRect.anchoredPosition = Vector2.zero;
            
            var highlightImage = highlight.AddComponent<Image>();
            highlightImage.color = Color.cyan;
            highlightImage.raycastTarget = false;
            highlight.SetActive(false); // Initially hidden
            
            // Add CharacterThumbnailCard script
            var cardScript = card.AddComponent<CharacterThumbnailCard>();
            
            // Wire references using SerializedObject
            var so = new SerializedObject(cardScript);
            so.FindProperty("portraitImage").objectReferenceValue = portraitImage;
            so.FindProperty("nameText").objectReferenceValue = nameText;
            so.FindProperty("rarityBadgeImage").objectReferenceValue = rarityBg;
            so.FindProperty("rarityLabelText").objectReferenceValue = rarityTMP;
            so.FindProperty("levelText").objectReferenceValue = levelTMP;
            so.FindProperty("selectionHighlight").objectReferenceValue = highlightImage;
            so.FindProperty("backgroundImage").objectReferenceValue = bgImage;
            so.FindProperty("cardButton").objectReferenceValue = button;
            so.ApplyModifiedProperties();
            
            // Save as prefab
            string prefabPath = PREFAB_PATH + "CharacterThumbnailCard.prefab";
            var prefabAsset = PrefabUtility.SaveAsPrefabAsset(card, prefabPath);
            
            // Clean up
            Object.DestroyImmediate(card);
            
            // Force Unity to save and refresh
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            Debug.Log($"[PrefabBuilder] ✓ Created CharacterThumbnailCard prefab at {prefabPath}");
            Debug.Log($"[PrefabBuilder] ✓ Prefab saved and refreshed");
            
            EditorUtility.DisplayDialog("Prefab Created",
                "CharacterThumbnailCard prefab created!\n\n" +
                $"Location: {prefabPath}\n\n" +
                "You can now assign this prefab to CarouselController.",
                "OK");
        }
        
        public static void BuildStatBar()
        {
            Debug.Log("[PrefabBuilder] Building StatBar prefab...");
            
            // Ensure directory exists
            if (!Directory.Exists(PREFAB_PATH))
            {
                Directory.CreateDirectory(PREFAB_PATH);
            }
            
            // Create root GameObject
            var statBar = new GameObject("StatBar");
            
            var rect = statBar.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(400, 40);
            
            // Icon (left)
            var icon = new GameObject("Icon");
            icon.transform.SetParent(statBar.transform, false);
            
            var iconRect = icon.AddComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0, 0.5f);
            iconRect.anchorMax = new Vector2(0, 0.5f);
            iconRect.pivot = new Vector2(0, 0.5f);
            iconRect.sizeDelta = new Vector2(32, 32);
            iconRect.anchoredPosition = new Vector2(0, 0);
            
            var iconImage = icon.AddComponent<Image>();
            iconImage.color = Color.white;
            iconImage.preserveAspect = true;
            
            // Label
            var label = new GameObject("Label");
            label.transform.SetParent(statBar.transform, false);
            
            var labelRect = label.AddComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0, 0.5f);
            labelRect.anchorMax = new Vector2(0, 0.5f);
            labelRect.pivot = new Vector2(0, 0.5f);
            labelRect.sizeDelta = new Vector2(150, 30);
            labelRect.anchoredPosition = new Vector2(40, 0);
            
            var labelText = label.AddComponent<TextMeshProUGUI>();
            labelText.text = "STRENGTH";
            labelText.fontSize = 16;
            labelText.alignment = TextAlignmentOptions.Left;
            labelText.color = Color.white;
            labelText.fontStyle = FontStyles.Bold;
            
            // Bar Container
            var barContainer = new GameObject("BarContainer");
            barContainer.transform.SetParent(statBar.transform, false);
            
            var barContainerRect = barContainer.AddComponent<RectTransform>();
            barContainerRect.anchorMin = new Vector2(0, 0.5f);
            barContainerRect.anchorMax = new Vector2(1, 0.5f);
            barContainerRect.pivot = new Vector2(0, 0.5f);
            barContainerRect.sizeDelta = new Vector2(-280, 20);
            barContainerRect.anchoredPosition = new Vector2(200, 0);
            
            // Background Bar
            var background = new GameObject("Background");
            background.transform.SetParent(barContainer.transform, false);
            
            var bgRect = background.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;
            bgRect.anchoredPosition = Vector2.zero;
            
            var bgImage = background.AddComponent<Image>();
            bgImage.color = new Color(0.2f, 0.2f, 0.2f, 1f);
            
            // Fill Bar
            var fill = new GameObject("Fill");
            fill.transform.SetParent(barContainer.transform, false);
            
            var fillRect = fill.AddComponent<RectTransform>();
            fillRect.anchorMin = new Vector2(0, 0);
            fillRect.anchorMax = new Vector2(0, 1);
            fillRect.pivot = new Vector2(0, 0.5f);
            fillRect.sizeDelta = new Vector2(100, 0); // Will be set dynamically
            fillRect.anchoredPosition = Vector2.zero;
            
            var fillImage = fill.AddComponent<Image>();
            fillImage.color = new Color(0.2f, 0.6f, 1f, 1f); // Blue
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            
            // Value Text
            var valueText = new GameObject("ValueText");
            valueText.transform.SetParent(statBar.transform, false);
            
            var valueRect = valueText.AddComponent<RectTransform>();
            valueRect.anchorMin = new Vector2(1, 0.5f);
            valueRect.anchorMax = new Vector2(1, 0.5f);
            valueRect.pivot = new Vector2(1, 0.5f);
            valueRect.sizeDelta = new Vector2(80, 30);
            valueRect.anchoredPosition = new Vector2(0, 0);
            
            var valueTMP = valueText.AddComponent<TextMeshProUGUI>();
            valueTMP.text = "12/30";
            valueTMP.fontSize = 16;
            valueTMP.alignment = TextAlignmentOptions.Right;
            valueTMP.color = Color.white;
            
            // Add StatBar script
            var statBarScript = statBar.AddComponent<StatBar>();
            
            // Wire references using SerializedObject
            var so = new SerializedObject(statBarScript);
            so.FindProperty("icon").objectReferenceValue = iconImage;
            so.FindProperty("label").objectReferenceValue = labelText;
            so.FindProperty("fillBar").objectReferenceValue = fillImage;
            so.FindProperty("valueText").objectReferenceValue = valueTMP;
            so.ApplyModifiedProperties();
            
            // Save as prefab
            string prefabPath = PREFAB_PATH + "StatBar.prefab";
            PrefabUtility.SaveAsPrefabAsset(statBar, prefabPath);
            
            // Clean up
            Object.DestroyImmediate(statBar);
            
            Debug.Log($"[PrefabBuilder] ✓ Created StatBar prefab at {prefabPath}");
            
            EditorUtility.DisplayDialog("Prefab Created",
                "StatBar prefab created!\n\n" +
                $"Location: {prefabPath}\n\n" +
                "This prefab will be used in the Detail Panel.",
                "OK");
        }
    }
}
#endif
