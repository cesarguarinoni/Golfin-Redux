using System;
using System.Collections.Generic;
using UnityEngine;

namespace GolfinRedux.UI
{
    /// <summary>
    /// Represents reward types for completing a hole.
    /// </summary>
    public enum RewardType
    {
        Points,
        RepairKit,
        Ball
    }

    /// <summary>
    /// A single reward (type + amount).
    /// </summary>
    [Serializable]
    public class HoleReward
    {
        public RewardType type;
        public int amount;

        public HoleReward(RewardType type, int amount)
        {
            this.type = type;
            this.amount = amount;
        }
    }

    /// <summary>
    /// Data for a single hole (name localization key + rewards).
    /// Later you can expand this with difficulty, par, etc.
    /// </summary>
    [Serializable]
    public class HoleData
    {
        public string courseNameKey;    // Localization key (e.g., "HOLE_LOMOND_5")
        public int holeNumber;
        public List<HoleReward> rewards = new();

        public HoleData(string courseNameKey, int holeNumber)
        {
            this.courseNameKey = courseNameKey;
            this.holeNumber = holeNumber;
        }

        public void AddReward(RewardType type, int amount)
        {
            rewards.Add(new HoleReward(type, amount));
        }
    }
}
