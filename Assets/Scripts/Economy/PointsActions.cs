// Order: reward_points_backend Slice 2 — the server-side vocabulary, in one place.
namespace Golfin.Economy
{
    /// <summary>
    /// Earn action ids. These are NOT free text: every value here must exist as a row in the server's
    /// <c>game_point_actions</c> catalog, or <c>POST /points/earn-game</c> answers
    /// <c>{awarded:0, reason:"Unknown game action"}</c> and the queued op is dropped.
    ///
    /// Mirrored by <c>backend/migrations/2026_08_12_game_point_actions_rebalance.sql</c>
    /// (RP_REBALANCE §3, approved 2026-08-12):
    ///   hole_complete     pts NULL · max_per_event 20   · daily_cap 400
    ///   hole_replay       pts NULL · max_per_event 5    · daily_cap 100
    ///   versus_win        pts 20   · max_per_event 20   · daily_cap 200
    ///   tournament_prize  pts NULL · max_per_event 2000 · no daily cap
    ///
    /// The amount the client sends is a PROPOSAL. For fixed-<c>pts</c> actions the server ignores it;
    /// for variable ones it is validated against <c>max_per_event</c>. The local balance is never
    /// waiting on that answer — the queue reconciles afterwards.
    /// </summary>
    public static class PointsActions
    {
        /// <summary>First clear of a hole (HoleDatabase.csv <c>reward*</c> Points row).</summary>
        public const string HoleComplete = "hole_complete";

        /// <summary>Replay of an already-cleared hole (HoleDatabase.csv <c>replayReward*</c> Points row).</summary>
        public const string HoleReplay = "hole_replay";

        /// <summary>1v1 versus win (modes.csv <c>versus_1v1</c> reward list).</summary>
        public const string VersusWin = "versus_win";

        /// <summary>Tournament rank-band payout (tournament_prizes.csv <c>rpReward</c>).</summary>
        public const string TournamentPrize = "tournament_prize";
    }

    /// <summary>
    /// Spend reasons. Unlike <see cref="PointsActions"/> these are NOT validated against a catalog —
    /// <c>POST /points/spend</c> takes any non-empty string and writes it to the ledger's
    /// <c>description</c> column (truncated server-side). They exist so the admin dashboard's ledger
    /// view reads as something other than a wall of anonymous debits, so keep them stable.
    /// </summary>
    public static class SpendReasons
    {
        public const string CharacterLevelUp = "character_level_up";
        public const string ClubLevelUp      = "club_level_up";
        public const string TournamentEntry  = "tournament_entry";
        public const string ModeEntryFee     = "mode_entry_fee";
    }
}
