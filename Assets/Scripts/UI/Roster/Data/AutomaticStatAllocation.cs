#nullable enable
using UnityEngine;

namespace Golfin.Roster
{
    /// <summary>
    /// Automatic Stat Allocation Strategy (FUTURE)
    /// SP is automatically distributed based on character rarity or a predefined formula
    /// No player choice required - level up is instant
    /// 
    /// To use: Set CharacterManager.SetAllocationStrategy(new AutomaticStatAllocation(...))
    /// </summary>
    public class AutomaticStatAllocation : StatAllocationStrategy
    {
        private CharacterManager characterManager;
        private AllocationFormula formula;
        
        public enum AllocationFormula
        {
            EvenDistribution,       // Equal to all stats
            RarityBased,            // Based on character rarity weights
            HighestStatPriority,    // Boost the character's highest stat
            BalancedGrowth          // Try to keep all stats balanced
        }
        
        public AutomaticStatAllocation(CharacterManager charManager, AllocationFormula formula = AllocationFormula.EvenDistribution)
        {
            this.characterManager = charManager;
            this.formula = formula;
        }
        
        /// <summary>
        /// Automatically allocate SP based on the selected formula
        /// </summary>
        public override void AllocateSP(string characterId, int earnedSP)
        {
            var playerChar = characterManager.GetPlayerCharacter(characterId);
            if (playerChar == null)
            {
                Debug.LogError($"[AutomaticStatAllocation] Character {characterId} not found");
                return;
            }
            
            switch (formula)
            {
                case AllocationFormula.EvenDistribution:
                    AllocateEvenDistribution(playerChar, earnedSP);
                    break;
                case AllocationFormula.RarityBased:
                    AllocateRarityBased(playerChar, earnedSP);
                    break;
                case AllocationFormula.HighestStatPriority:
                    AllocateHighestStatPriority(playerChar, earnedSP);
                    break;
                case AllocationFormula.BalancedGrowth:
                    AllocateBalancedGrowth(playerChar, earnedSP);
                    break;
            }
            
            // Confirm the allocations
            playerChar.ConfirmPendingSP();
            characterManager.RefreshStatValues(characterId);
            
            Debug.Log($"[AutomaticStatAllocation] Auto-allocated {earnedSP} SP for {characterId} using {formula}");
        }
        
        private void AllocateEvenDistribution(PlayerCharacterData playerChar, int earnedSP)
        {
            int perStat = earnedSP / 4;
            int remainder = earnedSP % 4;
            
            playerChar.pendingSpentStrength = perStat + (remainder > 0 ? 1 : 0);
            playerChar.pendingSpentClubControl = perStat + (remainder > 1 ? 1 : 0);
            playerChar.pendingSpentRecovery = perStat + (remainder > 2 ? 1 : 0);
            playerChar.pendingSpentStamina = perStat;
        }
        
        private void AllocateRarityBased(PlayerCharacterData playerChar, int earnedSP)
        {
            // TODO: Implement rarity-based weights
            // E.g., Legendary characters get more to their specialty stat
            AllocateEvenDistribution(playerChar, earnedSP);
        }
        
        private void AllocateHighestStatPriority(PlayerCharacterData playerChar, int earnedSP)
        {
            // Find the lowest stat (needs boost most)
            int[] currentStats = {
                playerChar.currentStrength,
                playerChar.currentClubControl,
                playerChar.currentRecovery,
                playerChar.currentStamina
            };
            
            int lowestStatIndex = System.Array.IndexOf(currentStats, System.Array.FindAll(currentStats, x => true)[0]);
            for (int i = 1; i < currentStats.Length; i++)
            {
                if (currentStats[i] < currentStats[lowestStatIndex])
                    lowestStatIndex = i;
            }
            
            // Allocate more to the lowest stat
            int preferentialAmount = earnedSP / 2;
            int remaining = earnedSP - preferentialAmount;
            
            switch (lowestStatIndex)
            {
                case 0: // Strength
                    playerChar.pendingSpentStrength += preferentialAmount;
                    break;
                case 1: // Club Control
                    playerChar.pendingSpentClubControl += preferentialAmount;
                    break;
                case 2: // Recovery
                    playerChar.pendingSpentRecovery += preferentialAmount;
                    break;
                case 3: // Stamina
                    playerChar.pendingSpentStamina += preferentialAmount;
                    break;
            }
            
            // Distribute remaining evenly
            int perStat = remaining / 3;
            for (int i = 0; i < 4; i++)
            {
                if (i != lowestStatIndex)
                {
                    switch (i)
                    {
                        case 0: playerChar.pendingSpentStrength += perStat; break;
                        case 1: playerChar.pendingSpentClubControl += perStat; break;
                        case 2: playerChar.pendingSpentRecovery += perStat; break;
                        case 3: playerChar.pendingSpentStamina += perStat; break;
                    }
                }
            }
        }
        
        private void AllocateBalancedGrowth(PlayerCharacterData playerChar, int earnedSP)
        {
            // TODO: Try to keep all stats within 10% of each other
            AllocateEvenDistribution(playerChar, earnedSP);
        }
        
        public override string GetStrategyName() => $"Automatic ({formula})";
    }
}
