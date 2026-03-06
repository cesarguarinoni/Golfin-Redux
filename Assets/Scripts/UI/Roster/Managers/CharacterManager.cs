#nullable enable
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace Golfin.Roster
{
    /// <summary>
    /// Central manager for all character operations
    /// Handles level-up, SP allocation, stat updates, roster management
    /// Works with CharacterLevelUpDatabase for economy data
    /// </summary>
    public class CharacterManager : MonoBehaviour
    {
        public static CharacterManager Instance { get; private set; }
        
        [SerializeField] private CharacterDatabase characterDatabase;
        [SerializeField] private CharacterLevelUpDatabase levelUpDatabase;
        
        private Dictionary<string, PlayerCharacterData> ownedCharacters = 
            new Dictionary<string, PlayerCharacterData>();
        
        private string selectedCharacterId = "";
        private StatAllocationStrategy allocationStrategy;
        
        // Events
        public event System.Action<string> OnCharacterLeveledUp;
        public event System.Action<string> OnCharacterSelected;
        public event System.Action OnRosterChanged;
        
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            
            Instance = this;
            DontDestroyOnLoad(gameObject);
            
            // Set default allocation strategy to Manual
            allocationStrategy = new ManualSPAllocation(this);
            
            // Load or initialize roster
            LoadRoster();
        }
        
        private void LoadRoster()
        {
            ownedCharacters.Clear();
            
            // TODO: Load from PlayerPrefs JSON
            // For now, initialize with sample characters
            
            if (characterDatabase == null)
            {
                Debug.LogError("[CharacterManager] CharacterDatabase not assigned!");
                return;
            }
            
            // Initialize with 4 sample characters
            InitializeWithSampleCharacters();
        }
        
        private void InitializeWithSampleCharacters()
        {
            string[] sampleIds = { "char_elizabeth", "char_shae", "char_james", "char_olivia" };
            
            foreach (var id in sampleIds)
            {
                if (!ownedCharacters.ContainsKey(id))
                {
                    var playerChar = new PlayerCharacterData(id);
                    playerChar.currentLevel = 1;
                    ownedCharacters[id] = playerChar;
                }
            }
            
            // Select first character by default
            if (ownedCharacters.Count > 0)
            {
                selectedCharacterId = ownedCharacters.Keys.First();
                ownedCharacters[selectedCharacterId].isSelected = true;
            }
            
            Debug.Log($"[CharacterManager] Initialized with {ownedCharacters.Count} sample characters");
        }
        
        /// <summary>
        /// Get base character template data
        /// </summary>
        public CharacterData? GetCharacter(string characterId)
        {
            if (characterDatabase == null)
            {
                Debug.LogError("[CharacterManager] CharacterDatabase not assigned!");
                return null;
            }
            
            return characterDatabase.GetCharacter(characterId);
        }
        
        /// <summary>
        /// Get a specific player-owned character
        /// </summary>
        public PlayerCharacterData? GetPlayerCharacter(string characterId)
        {
            if (ownedCharacters.TryGetValue(characterId, out var data))
                return data;
            
            Debug.LogWarning($"[CharacterManager] Character {characterId} not found in roster");
            return null;
        }
        
        /// <summary>
        /// Get all owned characters
        /// </summary>
        public List<PlayerCharacterData> GetAllOwnedCharacters()
        {
            return ownedCharacters.Values.ToList();
        }
        
        /// <summary>
        /// Get currently selected character
        /// </summary>
        public PlayerCharacterData? GetSelectedCharacter()
        {
            if (string.IsNullOrEmpty(selectedCharacterId))
                return null;
            
            return GetPlayerCharacter(selectedCharacterId);
        }
        
        /// <summary>
        /// Check if can level up (has enough Reward Points)
        /// </summary>
        public bool CanLevelUp(string characterId)
        {
            var playerChar = GetPlayerCharacter(characterId);
            if (playerChar == null) return false;
            
            int nextLevel = playerChar.currentLevel + 1;
            int cost = levelUpDatabase.GetLevelUpCost(characterId, nextLevel);
            
            return RewardPointsManager.Instance.CanAfford(cost);
        }
        
        /// <summary>
        /// Get cost to level up
        /// </summary>
        public int GetLevelUpCost(string characterId)
        {
            if (levelUpDatabase == null)
            {
                Debug.LogError("[CharacterManager] CharacterLevelUpDatabase not assigned!");
                return 0;
            }
            
            var playerChar = GetPlayerCharacter(characterId);
            if (playerChar == null) return 0;
            
            int nextLevel = playerChar.currentLevel + 1;
            return levelUpDatabase.GetLevelUpCost(nextLevel);
        }
        
        /// <summary>
        /// Get max level (universal for all characters)
        /// </summary>
        public int GetMaxLevel(string characterId)
        {
            return levelUpDatabase.GetMaxLevel();
        }
        
        /// <summary>
        /// Level up a character (spend R, earn SP)
        /// Returns SP earned, or 0 if failed
        /// </summary>
        public int LevelUp(string characterId)
        {
            var playerChar = GetPlayerCharacter(characterId);
            if (playerChar == null)
            {
                Debug.LogError($"[CharacterManager] Character {characterId} not found");
                return 0;
            }
            
            int maxLevel = GetMaxLevel(characterId);
            if (playerChar.currentLevel >= maxLevel)
            {
                Debug.LogWarning($"[CharacterManager] {characterId} already at max level");
                return 0;
            }
            
            int nextLevel = playerChar.currentLevel + 1;
            int cost = levelUpDatabase.GetLevelUpCost(characterId, nextLevel);
            int spReward = levelUpDatabase.GetSPReward(characterId, nextLevel);
            
            // Spend Reward Points
            if (!RewardPointsManager.Instance.SpendPoints(cost))
            {
                Debug.LogWarning($"[CharacterManager] Not enough points to level up {characterId}");
                return 0;
            }
            
            // Update level and SP
            playerChar.currentLevel = nextLevel;
            playerChar.totalSPEarned += spReward;
            
            OnCharacterLeveledUp?.Invoke(characterId);
            SaveRoster();
            
            Debug.Log($"[CharacterManager] {characterId} leveled up to {nextLevel}! Earned {spReward} SP");
            return spReward;
        }
        
        /// <summary>
        /// Allocate pending SP to a stat
        /// </summary>
        public bool AllocatePendingSP(string characterId, string statName, int amount)
        {
            var playerChar = GetPlayerCharacter(characterId);
            if (playerChar == null) return false;
            
            return playerChar.AllocatePendingSP(statName, amount);
        }
        
        /// <summary>
        /// Reset pending SP allocations
        /// </summary>
        public void ResetPendingSP(string characterId)
        {
            var playerChar = GetPlayerCharacter(characterId);
            if (playerChar != null)
            {
                playerChar.ResetPendingSP();
            }
        }
        
        /// <summary>
        /// Confirm SP allocation (commit to stats)
        /// </summary>
        public void ConfirmSPAllocation(string characterId)
        {
            var playerChar = GetPlayerCharacter(characterId);
            if (playerChar == null) return;
            
            // Use the allocation strategy
            int earnedSP = playerChar.pendingSpentStrength + 
                          playerChar.pendingSpentClubControl + 
                          playerChar.pendingSpentRecovery + 
                          playerChar.pendingSpentStamina;
            
            allocationStrategy.AllocateSP(characterId, earnedSP);
            SaveRoster();
        }
        
        /// <summary>
        /// Refresh current stat values based on base + SP allocation
        /// Stat caps are determined by rarity (fixed, not per-level)
        /// </summary>
        public void RefreshStatValues(string characterId)
        {
            var playerChar = GetPlayerCharacter(characterId);
            if (playerChar == null) return;
            
            var baseData = characterDatabase?.GetCharacter(characterId);
            if (baseData == null) return;
            
            // Get stat caps for this character's rarity (same regardless of level)
            var rarityStatCaps = RarityStatCaps.GetStatCaps(baseData.rarity);
            int strCap = rarityStatCaps.strengthCap;
            int ctrlCap = rarityStatCaps.clubControlCap;
            int recCap = rarityStatCaps.recoveryCap;
            int stamCap = rarityStatCaps.staminaCap;
            
            // Update current values (clamped to rarity-based caps)
            playerChar.currentStrength = Mathf.Min(baseData.baseStrength + playerChar.spentStrength, strCap);
            playerChar.currentClubControl = Mathf.Min(baseData.baseClubControl + playerChar.spentClubControl, ctrlCap);
            playerChar.currentRecovery = Mathf.Min(baseData.baseRecovery + playerChar.spentRecovery, recCap);
            playerChar.currentStamina = Mathf.Min(baseData.baseStamina + playerChar.spentStamina, stamCap);
            
            Debug.Log($"[CharacterManager] Refreshed stats for {characterId} ({baseData.rarity}): STR={playerChar.currentStrength}/{strCap} CTRL={playerChar.currentClubControl}/{ctrlCap} REC={playerChar.currentRecovery}/{recCap} STAM={playerChar.currentStamina}/{stamCap}");
        }
        
        /// <summary>
        /// Select a character as the active one
        /// </summary>
        public void SelectCharacter(string characterId)
        {
            if (!ownedCharacters.ContainsKey(characterId))
            {
                Debug.LogError($"[CharacterManager] Cannot select unknown character: {characterId}");
                return;
            }
            
            // Deselect previous
            if (!string.IsNullOrEmpty(selectedCharacterId))
            {
                ownedCharacters[selectedCharacterId].isSelected = false;
            }
            
            // Select new
            selectedCharacterId = characterId;
            ownedCharacters[characterId].isSelected = true;
            
            OnCharacterSelected?.Invoke(characterId);
            SaveRoster();
            
            Debug.Log($"[CharacterManager] Selected {characterId}");
        }
        
        /// <summary>
        /// Swap two characters (roster slot management)
        /// </summary>
        public void SwapCharacters(string characterId1, string characterId2)
        {
            if (!ownedCharacters.ContainsKey(characterId1) || !ownedCharacters.ContainsKey(characterId2))
            {
                Debug.LogError("[CharacterManager] Cannot swap unknown characters");
                return;
            }
            
            bool wasSelected1 = ownedCharacters[characterId1].isSelected;
            bool wasSelected2 = ownedCharacters[characterId2].isSelected;
            
            // Swap selection states
            ownedCharacters[characterId1].isSelected = wasSelected2;
            ownedCharacters[characterId2].isSelected = wasSelected1;
            
            if (wasSelected1)
                selectedCharacterId = characterId2;
            else if (wasSelected2)
                selectedCharacterId = characterId1;
            
            OnRosterChanged?.Invoke();
            SaveRoster();
            
            Debug.Log($"[CharacterManager] Swapped {characterId1} and {characterId2}");
        }
        
        /// <summary>
        /// Set the allocation strategy (Manual, Automatic, etc)
        /// </summary>
        public void SetAllocationStrategy(StatAllocationStrategy strategy)
        {
            allocationStrategy = strategy;
            Debug.Log($"[CharacterManager] Changed allocation strategy to: {strategy.GetStrategyName()}");
        }
        
        /// <summary>
        /// Save roster to PlayerPrefs (JSON serialization)
        /// </summary>
        private void SaveRoster()
        {
            // TODO: Implement JSON serialization of ownedCharacters
            // For now, just a placeholder
            Debug.Log("[CharacterManager] Roster saved (TODO: implement JSON)");
        }
        
        /// <summary>
        /// Get current stat value
        /// </summary>
        public int GetCurrentStat(string characterId, string statName)
        {
            var playerChar = GetPlayerCharacter(characterId);
            if (playerChar == null) return 0;
            
            return playerChar.GetCurrentStat(statName);
        }
    }
}
