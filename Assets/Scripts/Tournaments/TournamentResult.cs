// ─────────────────────────────────────────────────────────────────────────────
// Golfin.Tournaments — TournamentResult
// Final result for a player in one tournament. Returned by
// ITournamentBackend.GetResults(). Only available when the tournament is in
// a Resolved/Ended state.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable

namespace Golfin.Tournaments
{
    /// <summary>
    /// Final rank, prize, and claim state for the player in a specific tournament.
    /// Returned by <see cref="ITournamentBackend.GetResults"/>.
    /// Only valid when the tournament state is past <c>endUtc + resolveDelay</c>.
    /// <para>
    /// <b>D-Tie — indivisible-item rule (GDD §6.4, SPEC §7 decision locked):</b>
    /// if <see cref="IsTie"/> is true and <see cref="ItemRewardId"/> is non-null,
    /// a copy of the item is granted to each tied player (not split).
    /// RP ties are split-pool, rounded up (player-favorable). T4 implements; T1 records.
    /// </para>
    /// </summary>
    public sealed class TournamentResult
    {
        /// <summary>The player's final rank (1-based). Tied entries share the same rank.</summary>
        public int FinalRank { get; }

        /// <summary>True when the player's rank is shared with at least one other entry.</summary>
        public bool IsTie { get; }

        /// <summary>RP prize granted to this player (after split-pool for ties, rounded up).</summary>
        public long PrizeRP { get; }

        /// <summary>
        /// Item reward id, if the player's prize band includes an item.
        /// Null when no item is awarded.
        /// See D-Tie indivisible-item rule above.
        /// </summary>
        public string? ItemRewardId { get; }

        /// <summary>
        /// True when the player has already claimed their prize.
        /// One-time-grant guard: prevents double-claim on reconnect or retry.
        /// GDD §17.6: in v1, prizes are auto-claimed and surfaced via a modal;
        /// this flag ensures they are only granted once.
        /// </summary>
        public bool Claimed { get; }

        public TournamentResult(
            int finalRank,
            bool isTie,
            long prizeRP,
            string? itemRewardId,
            bool claimed)
        {
            FinalRank    = finalRank;
            IsTie        = isTie;
            PrizeRP      = prizeRP;
            ItemRewardId = itemRewardId;
            Claimed      = claimed;
        }
    }
}
