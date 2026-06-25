// ─────────────────────────────────────────────────────────────────────────────
// Golfin.Tournaments — TournamentDefinition
// One row from tournaments.csv (GDD §9). Loaded by T2 (tournament_csv_loaders).
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;
using System.Collections.Generic;

namespace Golfin.Tournaments
{
    /// <summary>
    /// Immutable definition of one tournament — maps directly to a row in
    /// <c>tournaments.csv</c> (GDD §9). Identical for v1 local and future remote
    /// backends (GDD §8 forward-compat).
    /// </summary>
    public sealed class TournamentDefinition
    {
        /// <summary>Stable string id; also seeds the deterministic bot field.</summary>
        public string Id { get; }

        /// <summary>Localization key (JP/EN) for the tournament name.</summary>
        public string NameKey { get; }

        /// <summary>
        /// The club/course id for this tournament.
        /// Formerly <c>courseId</c> in the GDD §9 schema;
        /// renamed to <c>clubId</c> to align with the T1 DTO spec §2.
        /// </summary>
        public string ClubId { get; }

        /// <summary>
        /// Explicit ordered list of hole ids in this tournament.
        /// Using an explicit list (not a range) so any subset of a course's holes
        /// can be combined (e.g. Lomond holes 1-9, or a 6-hole custom set).
        /// GDD §9: holeSet column; SPEC §7 flag resolved: explicit hole-id list.
        /// </summary>
        public IReadOnlyList<string> HoleSet { get; }

        /// <summary>Tournament opens for entries/play at this UTC instant.</summary>
        public DateTime StartUtc { get; }

        /// <summary>
        /// Tournament window closes at this UTC instant.
        /// At endUtc, all in-progress entries are auto-submitted (DNF if incomplete).
        /// </summary>
        public DateTime EndUtc { get; }

        /// <summary>
        /// RP entry fee. 0 = free entry.
        /// Debited once via RewardPointsManager at Register() time (T4).
        /// </summary>
        public long EntryFeeRP { get; }

        /// <summary>
        /// Reference to the prize table in tournament_prizes.csv (GDD §10).
        /// Resolved by T4 at prize-grant time.
        /// </summary>
        public string PrizeTableId { get; }

        /// <summary>
        /// Reference to the bot field config in tournament_bot_fields.csv (GDD §7).
        /// Used by T3 (tournament_bot_field) to pre-roll the deterministic bot scores.
        /// </summary>
        public string BotFieldId { get; }

        /// <summary>Localization key for the presenting sponsor (single mark, GDD §16 U3).</summary>
        public string SponsorKey { get; }

        /// <summary>Localization/filter key for the league this tournament belongs to.</summary>
        public string LeagueKey { get; }

        public TournamentDefinition(
            string id,
            string nameKey,
            string clubId,
            IReadOnlyList<string> holeSet,
            DateTime startUtc,
            DateTime endUtc,
            long entryFeeRP,
            string prizeTableId,
            string botFieldId,
            string sponsorKey,
            string leagueKey)
        {
            Id          = id;
            NameKey     = nameKey;
            ClubId      = clubId;
            HoleSet     = holeSet;
            StartUtc    = startUtc;
            EndUtc      = endUtc;
            EntryFeeRP  = entryFeeRP;
            PrizeTableId = prizeTableId;
            BotFieldId  = botFieldId;
            SponsorKey  = sponsorKey;
            LeagueKey   = leagueKey;
        }
    }
}
