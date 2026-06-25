// ─────────────────────────────────────────────────────────────────────────────
// Golfin.Tournaments — PrizeBand / PrizeTable
// tournament_prizes.csv rows (GDD §10). Split-pool / tie resolution is T4.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System.Collections.Generic;

namespace Golfin.Tournaments
{
    /// <summary>
    /// One rank (or percentile) band in a prize table.
    /// <para>
    /// <b>D-Tie — indivisible-item rule (GDD §6.4, SPEC §7 decision locked):</b>
    /// when multiple tied players span a prize-band boundary, the RP for all
    /// spanned bands is pooled and split evenly (rounded up, player-favorable).
    /// For <em>indivisible items</em> (a club, a single gacha ticket), a copy is
    /// granted to <em>each</em> tied player — not awarded to one by tiebreak order.
    /// True ties this deep are rare; the duplicate-per-tied-player rule avoids a
    /// "lost the club by a timestamp" feel-bad. T4 implements; T1 records the rule here.
    /// </para>
    /// </summary>
    public sealed class PrizeBand
    {
        /// <summary>Inclusive start of the rank/percentile range (1-based).</summary>
        public int RankFrom { get; }

        /// <summary>Inclusive end of the rank/percentile range.</summary>
        public int RankTo { get; }

        /// <summary>RP awarded to each finisher in this band (before split-pool for ties).</summary>
        public long RpReward { get; }

        /// <summary>
        /// Optional item reward id (maps to inventory/gacha item).
        /// Null = no item reward for this band.
        /// See D-Tie indivisible-item rule above for tie handling.
        /// </summary>
        public string? ItemRewardId { get; }

        public PrizeBand(int rankFrom, int rankTo, long rpReward, string? itemRewardId = null)
        {
            RankFrom     = rankFrom;
            RankTo       = rankTo;
            RpReward     = rpReward;
            ItemRewardId = itemRewardId;
        }
    }

    /// <summary>
    /// Complete prize table for a tournament, keyed by <see cref="PrizeTableId"/>.
    /// Loaded from <c>tournament_prizes.csv</c> by T2.
    /// </summary>
    public sealed class PrizeTable
    {
        /// <summary>Stable id matching the <c>prizeTableId</c> column in tournaments.csv.</summary>
        public string PrizeTableId { get; }

        /// <summary>Ordered bands (lowest rank first).</summary>
        public IReadOnlyList<PrizeBand> Bands { get; }

        public PrizeTable(string prizeTableId, IReadOnlyList<PrizeBand> bands)
        {
            PrizeTableId = prizeTableId;
            Bands        = bands ?? new List<PrizeBand>();
        }
    }
}
