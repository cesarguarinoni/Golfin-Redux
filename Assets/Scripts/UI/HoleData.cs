using System;
using System.Collections.Generic;
using UnityEngine;
using Golfin.Course.Runtime;  // TeeSet, TeeData (SPEC §3, Phase 3)

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

    // ── Tee-schema types live in Golfin.Course.Runtime (TeeSet, TeeData) ────
    // See Assets/Scripts/Course/Runtime/TeeData.cs

    // ────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Data for a single hole (name localization key, rewards, tee positions).
    /// </summary>
    [Serializable]
    public class HoleData
    {
        public string courseNameKey;    // Localization key (e.g., "HOLE_LOMOND_5")
        public int holeNumber;
        public int par;
        public string descriptionKey;             // Localization key for strategy text (e.g. "HOLE_LOMOND_1_DESC")
        public string holeImageName;              // Path within Resources/HoleImages/ e.g. "lomond-country-club/Hole_01"
        public float windSpeedMph         = 0f;
        public float windDirectionDegrees = 0f;
        public List<HoleReward> rewards = new();
        public List<HoleReward> replayRewards = new();   // Rewards shown when REPLAY button is shown (hole already played)
        public List<TeeData>    tees = new();            // Tee positions (absent = course does not offer that tee)

        public HoleData(string courseNameKey, int holeNumber)
        {
            this.courseNameKey = courseNameKey;
            this.holeNumber = holeNumber;
        }

        public void AddReward(RewardType type, int amount)
        {
            rewards.Add(new HoleReward(type, amount));
        }

        public void AddReplayReward(RewardType type, int amount)
        {
            replayRewards.Add(new HoleReward(type, amount));
        }

        /// <summary>
        /// Tries to find the tee data for the given tee set.
        /// Returns false if the course does not offer that tee for this hole.
        /// </summary>
        public bool TryGetTee(TeeSet teeSet, out TeeData result)
        {
            foreach (var t in tees)
            {
                if (t.set == teeSet)
                {
                    result = t;
                    return true;
                }
            }
            result = null;
            return false;
        }
    }
}
