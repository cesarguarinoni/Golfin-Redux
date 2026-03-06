#nullable enable
using UnityEngine;
using TMPro;
using System.Collections.Generic;

namespace Golfin.Roster
{
    /// <summary>
    /// Main controller for the Roster Screen
    /// Manages carousel, detail panel, and modal interactions
    /// Placed on RosterScreen GameObject in ShellScene
    /// </summary>
    public class RosterScreenController : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI rewardPointsText;
        [SerializeField] private CarouselController carousel;
        
        private string currentCharacterId = "";
        
        private void Start()
        {
            InitializeScreen();
            UpdateRewardPointsDisplay();
        }
        
        private void InitializeScreen()
        {
            Debug.Log("[RosterScreenController] Initializing Roster Screen");
            
            // Get first owned character
            var characters = CharacterManager.Instance.GetAllOwnedCharacters();
            if (characters.Count > 0)
            {
                currentCharacterId = characters[0].characterId;
                CharacterManager.Instance.SelectCharacter(currentCharacterId);
                Debug.Log($"[RosterScreenController] Selected first character: {currentCharacterId}");
            }
            else
            {
                Debug.LogError("[RosterScreenController] No characters found!");
            }
        }
        
        private void OnEnable()
        {
            // Subscribe to events
            RewardPointsManager.Instance.OnPointsChanged += UpdateRewardPointsDisplay;
            CharacterManager.Instance.OnCharacterSelected += OnCharacterSelected;
        }
        
        private void OnDisable()
        {
            // Unsubscribe from events
            RewardPointsManager.Instance.OnPointsChanged -= UpdateRewardPointsDisplay;
            CharacterManager.Instance.OnCharacterSelected -= OnCharacterSelected;
        }
        
        /// <summary>
        /// Called when carousel selects a character
        /// </summary>
        public void OnCarouselCharacterSelected(string characterId)
        {
            currentCharacterId = characterId;
            CharacterManager.Instance.SelectCharacter(characterId);
            
            Debug.Log($"[RosterScreenController] Character selected from carousel: {characterId}");
        }
        
        /// <summary>
        /// Called when CharacterManager selects a character
        /// </summary>
        private void OnCharacterSelected(string characterId)
        {
            currentCharacterId = characterId;
            // TODO: Update detail panel to show this character
            Debug.Log($"[RosterScreenController] Character selected (event): {characterId}");
        }
        
        private void UpdateRewardPointsDisplay(int points = -1)
        {
            if (rewardPointsText == null) return;
            
            int currentPoints = RewardPointsManager.Instance.GetPoints();
            rewardPointsText.text = $"R {currentPoints}";
        }
        
        /// <summary>
        /// Get currently selected character ID
        /// </summary>
        public string GetCurrentCharacterId() => currentCharacterId;
    }
}
