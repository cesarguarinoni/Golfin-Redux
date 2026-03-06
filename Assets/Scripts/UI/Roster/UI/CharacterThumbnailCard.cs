#nullable enable
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Golfin.Roster
{
    /// <summary>
    /// Individual character card in the carousel
    /// Shows:
    /// - Character portrait (thumbnail)
    /// - Character name
    /// - Rarity badge (C/U/R/M/L/S)
    /// - Level badge (Lv X)
    /// - Selection highlight
    /// </summary>
    public class CharacterThumbnailCard : MonoBehaviour
    {
        [Header("UI Components")]
        [SerializeField] private Image portraitImage;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private Image rarityBadgeImage;
        [SerializeField] private TextMeshProUGUI rarityLabelText;
        [SerializeField] private TextMeshProUGUI levelText;
        [SerializeField] private Image selectionHighlight;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Button cardButton;
        
        private string characterId = "";
        private bool isSelected = false;
        
        // Events
        public System.Action OnClicked;
        
        /// <summary>
        /// Initialize card with character data
        /// </summary>
        public void Initialize(string charId)
        {
            characterId = charId;
            
            var playerChar = CharacterManager.Instance.GetPlayerCharacter(characterId);
            var baseData = CharacterManager.Instance.GetCharacter(characterId);
            
            if (playerChar == null || baseData == null)
            {
                Debug.LogError($"[CharacterThumbnailCard] Character {charId} not found!");
                return;
            }
            
            // Set portrait
            if (portraitImage != null && baseData.portraitThumbnail != null)
            {
                portraitImage.sprite = baseData.portraitThumbnail;
            }
            
            // Set name
            if (nameText != null)
            {
                nameText.text = baseData.characterNickname;
            }
            
            // Set rarity
            var rarityColor = RarityHelper.GetRarityColor(baseData.rarity);
            var rarityLabel = RarityHelper.GetRarityLabel(baseData.rarity);
            
            if (rarityBadgeImage != null)
            {
                rarityBadgeImage.color = rarityColor;
            }
            
            if (rarityLabelText != null)
            {
                rarityLabelText.text = rarityLabel;
                rarityLabelText.color = Color.white;
            }
            
            // Set level
            if (levelText != null)
            {
                int maxLevel = CharacterManager.Instance.GetMaxLevel(characterId);
                levelText.text = $"Lv {playerChar.currentLevel}/{maxLevel}";
            }
            
            // Set background color to match rarity
            if (backgroundImage != null)
            {
                backgroundImage.color = rarityColor;
            }
            
            // Wire button
            if (cardButton != null)
            {
                cardButton.onClick.AddListener(() => OnClicked?.Invoke());
            }
            
            Debug.Log($"[CharacterThumbnailCard] Initialized: {baseData.characterName}");
        }
        
        /// <summary>
        /// Set selection state
        /// </summary>
        public void SetSelected(bool selected)
        {
            isSelected = selected;
            
            if (selectionHighlight != null)
            {
                selectionHighlight.enabled = selected;
            }
            
            Debug.Log($"[CharacterThumbnailCard] {characterId} selection: {selected}");
        }
        
        /// <summary>
        /// Get character ID
        /// </summary>
        public string GetCharacterId() => characterId;
        
        /// <summary>
        /// Check if selected
        /// </summary>
        public bool IsSelected() => isSelected;
    }
}
