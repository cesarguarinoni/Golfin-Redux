#nullable enable
using UnityEngine;
using System;

namespace Golfin.Roster
{
    /// <summary>
    /// Represents a player-owned character instance
    /// Tracks level, SP earned/spent, and stat allocations
    /// Separate from CharacterData which is the base template
    /// </summary>
    [System.Serializable]
    public class PlayerCharacterData
    {
        [SerializeField]
        public string characterId;                  // Reference to CharacterData
        
        [SerializeField]
        public int currentLevel;                    // 1 to 199
        
        [SerializeField]
        public int totalSPEarned;                   // Sum of all SP rewards from level-ups
        
        // SP allocated to each stat (increases max stat value)
        [SerializeField]
        public int spentStrength;
        
        [SerializeField]
        public int spentClubControl;
        
        [SerializeField]
        public int spentRecovery;
        
        [SerializeField]
        public int spentStamina;
        
        // Current stat values (base + SP allocation)
        [SerializeField]
        public int currentStrength;
        
        [SerializeField]
        public int currentClubControl;
        
        [SerializeField]
        public int currentRecovery;
        
        [SerializeField]
        public int currentStamina;
        
        [SerializeField]
        public bool isSelected;                     // Currently active for gameplay
        
        [SerializeField]
        public bool isOwned = false;                 // Is this character owned by player? (Phase 2b: false = locked)

        [SerializeField]
        public DateTime acquiredDate;

        /// <summary>
        /// Current stamina energy (depletes as character plays holes).
        /// Separate from the stamina STAT which is leveled via SP.
        /// Drives red stamina bar indicator and low-stamina bolt icon.
        /// </summary>
        [System.NonSerialized]
        public float currentStaminaEnergy = 100f;

        [System.NonSerialized]
        public float maxStaminaEnergy = 100f;

        /// <summary>
        /// UTC timestamp of the last authoritative conditionEnergy write.
        /// Used by AccrueRegen to calculate offline recovery since last save.
        /// default(DateTime) = never written; treated as "stamp now + full pool".
        /// [NonSerialized] — persisted as ISO-8601 string in PersistedCharacter.conditionUpdatedUtc.
        /// </summary>
        [System.NonSerialized]
        public DateTime conditionUpdatedUtc = default;

        /// <summary>
        /// Check if stamina energy is below threshold (0.0 to 1.0)
        /// </summary>
        public bool IsStaminaLow(float threshold = 0.25f)
        {
            return maxStaminaEnergy > 0 && (currentStaminaEnergy / maxStaminaEnergy) < threshold;
        }
        
        // Temporary SP allocation (during level-up modal, before confirm)
        [System.NonSerialized]
        public int pendingSpentStrength;
        
        [System.NonSerialized]
        public int pendingSpentClubControl;
        
        [System.NonSerialized]
        public int pendingSpentRecovery;
        
        [System.NonSerialized]
        public int pendingSpentStamina;
        
        public PlayerCharacterData(string characterId)
        {
            this.characterId = characterId;
            this.currentLevel = 1;
            this.totalSPEarned = 0;
            this.spentStrength = 0;
            this.spentClubControl = 0;
            this.spentRecovery = 0;
            this.spentStamina = 0;
            this.currentStrength = 0;
            this.currentClubControl = 0;
            this.currentRecovery = 0;
            this.currentStamina = 0;
            this.isSelected = false;
            this.acquiredDate = DateTime.Now;
            ResetPendingSP();
        }
        
        /// <summary>
        /// Get available SP that hasn't been allocated
        /// </summary>
        public int GetAvailableSP()
        {
            int totalSpent = spentStrength + spentClubControl + spentRecovery + spentStamina;
            return totalSPEarned - totalSpent;
        }
        
        /// <summary>
        /// Get available SP from current level-up (pending allocation)
        /// </summary>
        public int GetAvailablePendingSP()
        {
            int totalPending = pendingSpentStrength + pendingSpentClubControl + pendingSpentRecovery + pendingSpentStamina;
            return totalSPEarned - (spentStrength + spentClubControl + spentRecovery + spentStamina) - totalPending;
        }
        
        /// <summary>
        /// Allocate SP to a specific stat (before confirmation)
        /// </summary>
        public bool AllocatePendingSP(string statName, int amount)
        {
            if (amount < 0)
                return false;
            
            int available = GetAvailablePendingSP();
            if (amount > available)
                return false;
            
            switch (statName)
            {
                case "Strength":
                    pendingSpentStrength += amount;
                    return true;
                case "ClubControl":
                    pendingSpentClubControl += amount;
                    return true;
                case "Recovery":
                    pendingSpentRecovery += amount;
                    return true;
                case "Stamina":
                    pendingSpentStamina += amount;
                    return true;
                default:
                    return false;
            }
        }
        
        /// <summary>
        /// Confirm pending SP allocations (move from pending to actual)
        /// </summary>
        public void ConfirmPendingSP()
        {
            spentStrength += pendingSpentStrength;
            spentClubControl += pendingSpentClubControl;
            spentRecovery += pendingSpentRecovery;
            spentStamina += pendingSpentStamina;
            
            ResetPendingSP();
        }
        
        /// <summary>
        /// Discard pending SP allocations
        /// </summary>
        public void ResetPendingSP()
        {
            pendingSpentStrength = 0;
            pendingSpentClubControl = 0;
            pendingSpentRecovery = 0;
            pendingSpentStamina = 0;
        }
        
        /// <summary>
        /// Get current stat value (base + SP allocation)
        /// </summary>
        public int GetCurrentStat(string statName)
        {
            return statName switch
            {
                "Strength" => currentStrength,
                "ClubControl" => currentClubControl,
                "Recovery" => currentRecovery,
                "Stamina" => currentStamina,
                _ => 0
            };
        }
        
        /// <summary>
        /// Set current stat value (called after SP allocation confirmed)
        /// </summary>
        public void SetCurrentStat(string statName, int value)
        {
            switch (statName)
            {
                case "Strength":
                    currentStrength = value;
                    break;
                case "ClubControl":
                    currentClubControl = value;
                    break;
                case "Recovery":
                    currentRecovery = value;
                    break;
                case "Stamina":
                    currentStamina = value;
                    break;
            }
        }
        
        public override string ToString()
        {
            return $"{characterId} Lv{currentLevel}: SP Earned={totalSPEarned}, Available={GetAvailableSP()}, STR={currentStrength} CTRL={currentClubControl} REC={currentRecovery} STAM={currentStamina}";
        }
    }
}
