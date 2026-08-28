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
        /// <summary>
        /// PREFIX ONLY. A mode entry sends <c>mode_entry_fee:&lt;mode id&gt;</c> — see
        /// <see cref="ModeEntryFeeFor"/>. The bare constant is kept because the SERVER still accepts
        /// it (every build installed before game_modes_admin sends it), and because closing that
        /// door is a separate one-line commit once the suffixed build is what testers run.
        /// </summary>
        public const string ModeEntryFee     = "mode_entry_fee";

        /// <summary>
        /// The reason for entering <paramref name="modeId"/>: <c>mode_entry_fee:practice</c>.
        ///
        /// THIS IS NOT COSMETIC. <c>POST /points/spend</c> parses the mode id out of it and refuses
        /// the debit unless the amount matches the PUBLISHED fee — so the suffix is what turns a
        /// client-asserted price into a server-validated one (game_modes_admin §4). It also makes
        /// the admin ledger per-mode legible, which is the free part.
        ///
        /// A blank id falls back to the bare reason rather than sending a dangling
        /// <c>mode_entry_fee:</c>: the server reads an empty suffix as <c>unknown_mode</c> and
        /// refuses, and a mode with no id is a client bug that should not also become a failed entry.
        /// </summary>
        public static string ModeEntryFeeFor(string modeId)
            => string.IsNullOrWhiteSpace(modeId) ? ModeEntryFee : ModeEntryFee + ":" + modeId.Trim();

        /// <summary>Stamina Boost Shop (points_cutover_followups item 2).</summary>
        public const string StaminaBoost     = "stamina_boost";

        /// <summary>General Shop catalog purchase — club or ball (points_cutover_followups item 2).</summary>
        public const string ShopPurchase     = "shop_purchase";
    }
}
