#nullable enable
using UnityEngine;

namespace Golfin.Roster
{
    /// <summary>
    /// Represents a single level in the universal level-up progression
    /// All characters share the same level-up costs and SP rewards
    /// Stat caps are determined by CHARACTER RARITY, not level
    /// </summary>
    [System.Serializable]
    public class CharacterLevelUpData
    {
        public int level;                   // The level (1-199)
        public int cost_r;                  // Reward Points cost to reach this level
        public int sp_reward;               // Stat Points earned (usually 1)
        
        public CharacterLevelUpData() { }
        
        public CharacterLevelUpData(int level, int cost_r, int sp_reward)
        {
            this.level = level;
            this.cost_r = cost_r;
            this.sp_reward = sp_reward;
        }
        
        public override string ToString()
        {
            return $"Lv{level}: Cost={cost_r}R, SP Reward={sp_reward}";
        }
    }
}
