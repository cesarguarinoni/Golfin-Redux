#nullable enable
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Golfin.Roster
{
    public class CharacterDetailPanel : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Image characterImage;       // UI Image for character
        [SerializeField] private TextMeshProUGUI characterRarity; // UI Text for character rarity
[SerializeField] private TextMeshProUGUI characterStats; // UI Text for character stats
[SerializeField] private TextMeshProUGUI characterLevel; // UI Text for character level
[SerializeField] private TextMeshProUGUI bioText; // UI Text for character bio
        [SerializeField] private Button levelUpButton;       // Level Up button
        [SerializeField] private Button selectButton;        // Select button

        private void OnEnable()
        {
            CarouselController.OnCharacterSelected += UpdatePanel;
        }

        private void OnDisable()
        {
            CarouselController.OnCharacterSelected -= UpdatePanel;
        }

        // Update UI with character details
        private void UpdatePanel(string characterId)
        {
            var characterData = CharacterManager.Instance.GetCharacterData(characterId);
            if(characterData != null)
            {
                characterImage.sprite = characterData.Image; // Assuming Image is a Sprite field
                characterStats.text = characterData.GetStatsDisplay(); // Format stats as needed
            }
        }

        // Level Up button functionality
        public void OnLevelUpClicked()
        {
            // TODO: Open modal for level up
        }

        // Select button functionality
        public void OnSelectClicked()
        {
            // Logic to handle selection
        }
    }
}