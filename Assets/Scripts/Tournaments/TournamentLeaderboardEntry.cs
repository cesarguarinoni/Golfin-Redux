// ─────────────────────────────────────────────────────────────────────────────
// Golfin.Tournaments — TournamentLeaderboardEntry
// Mirrors Golfin.UI.Rankings.LeaderboardEntry but strokes-based (SPEC §2).
// The existing leaderboard widgets already bind to the Rankings entry; T4 will
// swap the fill source and adapt the binding layer (SPEC §4.1).
// ─────────────────────────────────────────────────────────────────────────────
namespace Golfin.Tournaments
{
    /// <summary>
    /// One row in the tournament leaderboard. Strokes-based mirror of
    /// <c>Golfin.UI.Rankings.LeaderboardEntry</c>.
    /// <para>
    /// Key differences from the Rankings entry:
    /// <list type="bullet">
    ///   <item><description><see cref="Strokes"/> replaces <c>Score</c> (RP).</description></item>
    ///   <item><description><see cref="Thru"/> drives the organic "thru 7" reveal (GDD §7).</description></item>
    ///   <item><description><see cref="TimeSeconds"/> is the human-visible tiebreak (GDD §17.3).</description></item>
    ///   <item><description><see cref="IsDNF"/> and <see cref="IsProvisional"/> have no Rankings equivalent.</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// Display note (GDD §17.2): the board shows raw strokes, not score-to-par.
    /// The <c>T</c> prefix on <see cref="Rank"/> shows only when <see cref="IsTie"/> is true (GDD §17.3).
    /// DNFs are hidden from ranked rows (GDD §17.4); the player's own DNF is visible
    /// via the sticky "you" row only.
    /// </para>
    /// </summary>
    public struct TournamentLeaderboardEntry
    {
        /// <summary>1-based rank. Tied entries share the same rank value.</summary>
        public int Rank;

        /// <summary>
        /// True when two or more entries share this rank value.
        /// The <c>T</c> prefix (e.g. "T2") is shown only when this is true (GDD §17.3).
        /// </summary>
        public bool IsTie;

        /// <summary>Display name (username or "YOU").</summary>
        public string DisplayName;

        /// <summary>Character id — drives portrait and rarity art resolution.</summary>
        public string CharacterId;

        /// <summary>Display level (1–240).</summary>
        public int Level;

        /// <summary>
        /// Total strokes across all completed holes. Lower = better.
        /// Replaces <c>Score</c> (RP) from <c>LeaderboardEntry</c>.
        /// </summary>
        public int Strokes;

        /// <summary>
        /// Number of holes completed ("thru X"). Drives the organic reveal (GDD §7).
        /// 0 = not yet started; equals holeSet.Count when finished.
        /// NOTE: GDD §17.2 removes the thru column from the visible board;
        /// the field is retained for the organic-reveal projection logic in T3/T4.
        /// </summary>
        public int Thru;

        /// <summary>
        /// Total time in seconds across all completed holes.
        /// Human-visible tiebreak (GDD §17.3); displayed as "Xm Ys" or similar.
        /// NOTE: GDD §17.2 removes the time column from the v1 board;
        /// the field is retained for tiebreak computation in T4.
        /// </summary>
        public float TimeSeconds;

        /// <summary>True for the real player's row.</summary>
        public bool IsPlayer;

        /// <summary>
        /// True if the entry is a DNF (player did not complete all holes before endUtc).
        /// DNF rows are hidden from the ranked board (GDD §17.4);
        /// the player's own DNF still appears in the sticky "you" row.
        /// </summary>
        public bool IsDNF;

        /// <summary>
        /// True while the tournament window is still open (leaderboard not yet final).
        /// The board shows a "PROVISIONAL / LIVE" banner when true (GDD §16.7 / §17).
        /// </summary>
        public bool IsProvisional;
    }
}
