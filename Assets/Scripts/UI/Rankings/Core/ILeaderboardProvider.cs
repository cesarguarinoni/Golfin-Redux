#nullable enable
using System;
using System.Collections.Generic;

namespace Golfin.UI.Rankings
{
    public enum LeaderboardPeriod
    {
        Daily,
        Weekly,
        Monthly,
        Historic
    }

    public struct LeaderboardEntry
    {
        /// <summary>1-based rank. On ties multiple entries share the same Rank value.</summary>
        public int Rank;
        /// <summary>True when two or more entries share this rank value.</summary>
        public bool IsTie;
        /// <summary>Display name (username or "YOU").</summary>
        public string DisplayName;
        /// <summary>Character ID — drives portrait + rarity art resolution.</summary>
        public string CharacterId;
        /// <summary>Display level (1–240).</summary>
        public int Level;
        /// <summary>RP earned in the active period.</summary>
        public long Score;
        /// <summary>True for the real player's entry.</summary>
        public bool IsPlayer;
    }

    /// <summary>
    /// Provider seam for the leaderboard data layer.
    /// Phase 1: LocalFakeLeaderboardProvider (deterministic fake scores + real player).
    /// Phase 2+: backend implementation — UI code is untouched.
    /// </summary>
    public interface ILeaderboardProvider
    {
        /// <summary>Returns the full ranked list for the period (fakes + player merged + ranked).</summary>
        IReadOnlyList<LeaderboardEntry> GetRanking(LeaderboardPeriod period);

        /// <summary>Returns just the player's entry for the period.</summary>
        LeaderboardEntry GetPlayerEntry(LeaderboardPeriod period);

        /// <summary>
        /// UTC end time of the current period — drives the reset countdown.
        /// For Historic (no reset), returns DateTime.MaxValue.
        /// </summary>
        DateTime GetPeriodEndUtc(LeaderboardPeriod period);
    }
}
