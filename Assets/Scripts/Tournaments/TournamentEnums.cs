// ─────────────────────────────────────────────────────────────────────────────
// Golfin.Tournaments — TournamentEnums
// All enums for the tournament system. Derivation logic lives in T4
// (LocalTournamentBackend); T1 only defines the shapes.
// ─────────────────────────────────────────────────────────────────────────────
namespace Golfin.Tournaments
{
    /// <summary>
    /// Lifecycle state of a tournament, derived from (startUtc, endUtc, now,
    /// hasEntry, entryStatus). Maps to Figma 13386:1758 badges.
    /// Derivation is T4 — T1 defines the enum only.
    /// </summary>
    public enum TournamentState
    {
        /// <summary>now &lt; startUtc — UPCOMING badge.</summary>
        Upcoming,

        /// <summary>startUtc &lt;= now &lt; endUtc, player has NOT entered — OPEN badge.</summary>
        Open,

        /// <summary>startUtc &lt;= now &lt; endUtc, player IS entered and still playing.</summary>
        Playing,

        /// <summary>Near endUtc, window still active — ENDING badge.</summary>
        Ending,

        /// <summary>Window over, player did NOT enter — CLOSED badge.</summary>
        Closed,

        /// <summary>Window over, player DID enter — ENDED badge.</summary>
        Ended,
    }

    /// <summary>
    /// Status of the player's entry for a specific tournament.
    /// </summary>
    public enum EntryStatus
    {
        /// <summary>Player has not registered for this tournament.</summary>
        NotEntered,

        /// <summary>Player registered and is actively playing (not all holes complete).</summary>
        InProgress,

        /// <summary>Player completed all holes before endUtc.</summary>
        Finished,

        /// <summary>Window closed before player completed all holes (auto-submitted).</summary>
        DNF,
    }
}
