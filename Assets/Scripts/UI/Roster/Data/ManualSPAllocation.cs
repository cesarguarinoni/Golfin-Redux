#nullable enable
using UnityEngine;

namespace Golfin.Roster
{
    /// <summary>
    /// Manual SP Allocation Strategy
    /// Player must allocate ALL earned SP to stats via [+] buttons before confirming
    /// Current implementation for MVP
    /// </summary>
    public class ManualSPAllocation : StatAllocationStrategy
    {
        private CharacterManager characterManager;
        
        public ManualSPAllocation(CharacterManager charManager)
        {
            this.characterManager = charManager;
        }
        
        /// <summary>
        /// Manual allocation: Already done in LevelUpModal via AllocatePendingSP()
        /// This just confirms the pending allocations
        /// </summary>
        public override void AllocateSP(string characterId, int earnedSP)
        {
            var playerChar = characterManager.GetPlayerCharacter(characterId);
            if (playerChar == null)
            {
                Debug.LogError($"[ManualSPAllocation] Character {characterId} not found");
                return;
            }
            
            // Validate that all SP has been allocated
            int totalAllocated = playerChar.pendingSpentStrength + 
                                 playerChar.pendingSpentClubControl + 
                                 playerChar.pendingSpentRecovery + 
                                 playerChar.pendingSpentStamina;
            
            if (totalAllocated != earnedSP)
            {
                Debug.LogWarning($"[ManualSPAllocation] {characterId}: Allocated {totalAllocated} SP but earned {earnedSP}. This shouldn't happen!");
                return;
            }
            
            // Move pending allocations to actual
            playerChar.ConfirmPendingSP();
            
            // Update current stat values
            characterManager.RefreshStatValues(characterId);
            
            Debug.Log($"[ManualSPAllocation] Confirmed {earnedSP} SP allocation for {characterId}");
        }
        
        public override string GetStrategyName() => "Manual SP Allocation";
    }
}
