#nullable enable
using UnityEngine;

namespace Golfin.Roster
{
    /// <summary>
    /// Represents a single row in the CharacterLevelUpCosts.csv
    /// Contains all economy data for a character at a specific level
    /// </summary>
    [System.Serializable]
    public class CharacterLevelUpData
    {
        public string characterId;          // e.g., "char_elizabeth"
        public int level;                   // The level being leveled TO (1-199)
        public int cost_r;                  // Reward Points cost to level up
        public int sp_reward;               // Stat Points earned from this level-up
        
        // Stat caps at this level (can increase as you level)
        public int str_cap;                 // Max Strength this character can reach
        public int ctrl_cap;                // Max Club Control this character can reach
        public int rec_cap;                 // Max Recovery this character can reach
        public int stam_cap;                // Max Stamina this character can reach
        
        public CharacterLevelUpData() { }
        
        public CharacterLevelUpData(
            string characterId, int level, int cost_r, int sp_reward,
            int str_cap, int ctrl_cap, int rec_cap, int stam_cap)
        {
            this.characterId = characterId;
            this.level = level;
            this.cost_r = cost_r;
            this.sp_reward = sp_reward;
            this.str_cap = str_cap;
            this.ctrl_cap = ctrl_cap;
            this.rec_cap = rec_cap;
            this.stam_cap = stam_cap;
        }
        
        /// <summary>
        /// Get stat cap by stat name
        /// </summary>
        public int GetStatCap(string statName)
        {
            return statName switch
            {
                "Strength" => str_cap,
                "ClubControl" => ctrl_cap,
                "Recovery" => rec_cap,
                "Stamina" => stam_cap,
                _ => 0
            };
        }
        
        public override string ToString()
        {
            return $"{characterId} Lv{level}: Cost={cost_r}R, SP Reward={sp_reward}, Caps: STR={str_cap} CTRL={ctrl_cap} REC={rec_cap} STAM={stam_cap}";
        }
    }
}
