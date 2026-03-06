#nullable enable
using UnityEngine;

namespace Golfin.Roster
{
    /// <summary>
    /// Rarity-based stat cap definitions
    /// Each rarity determines the maximum values a character can reach
    /// Stat caps are FIXED per rarity (not per-level, not per-character)
    /// </summary>
    public static class RarityStatCaps
    {
        /// <summary>
        /// Stat cap data for a rarity tier
        /// </summary>
        public class StatCapData
        {
            public int strengthCap;
            public int clubControlCap;
            public int recoveryCap;
            public int staminaCap;
            
            public StatCapData(int str, int ctrl, int rec, int stam)
            {
                strengthCap = str;
                clubControlCap = ctrl;
                recoveryCap = rec;
                staminaCap = stam;
            }
            
            public int GetCap(string statName)
            {
                return statName switch
                {
                    "Strength" => strengthCap,
                    "ClubControl" => clubControlCap,
                    "Recovery" => recoveryCap,
                    "Stamina" => staminaCap,
                    _ => 0
                };
            }
        }
        
        /// <summary>
        /// Get stat caps for a rarity
        /// </summary>
        public static StatCapData GetStatCaps(CharacterRarity rarity)
        {
            return rarity switch
            {
                CharacterRarity.Common => new StatCapData(
                    str: 25, ctrl: 25, rec: 18, stam: 22
                ),
                CharacterRarity.Uncommon => new StatCapData(
                    str: 28, ctrl: 28, rec: 19, stam: 25
                ),
                CharacterRarity.Rare => new StatCapData(
                    str: 30, ctrl: 30, rec: 20, stam: 27
                ),
                CharacterRarity.Mythic => new StatCapData(
                    str: 35, ctrl: 35, rec: 25, stam: 32
                ),
                CharacterRarity.Legendary => new StatCapData(
                    str: 40, ctrl: 40, rec: 40, stam: 40
                ),
                CharacterRarity.Supreme => new StatCapData(
                    str: 50, ctrl: 50, rec: 50, stam: 50
                ),
                _ => new StatCapData(0, 0, 0, 0)
            };
        }
        
        /// <summary>
        /// Get a specific stat cap for rarity + stat name
        /// </summary>
        public static int GetStatCap(CharacterRarity rarity, string statName)
        {
            return GetStatCaps(rarity).GetCap(statName);
        }
    }
}
