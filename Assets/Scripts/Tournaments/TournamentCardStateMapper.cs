// ─────────────────────────────────────────────────────────────────────────────
// Golfin.Tournaments — TournamentCardStateMapper
// Pure static mapping: (TournamentState, EntryStatus, nowPastEnd) → CardState.
// Lives in the pure Golfin.Tournaments assembly so EditMode tests can exercise
// it without referencing Assembly-CSharp (where the UI MonoBehaviours live).
//
// TournamentSelectionScreenController delegates to this method directly.
// Removing this method or changing its name makes MapCardStateTests go RED.
// ─────────────────────────────────────────────────────────────────────────────
namespace Golfin.Tournaments
{
    /// <summary>
    /// Maps (TournamentState, EntryStatus, nowPastEnd) to one of the seven CardState
    /// values shown on the selection card. This is the SPEC §2 table implemented as
    /// a pure function — no Unity runtime, no clock, no I/O.
    /// </summary>
    public enum TournamentCardState
    {
        Open,
        Ending,
        EnteredActive,
        EnteredFinished,
        Upcoming,
        Ended,
    }

    public static class TournamentCardStateMapper
    {
        /// <summary>
        /// Pure mapping from backend state to UI CardState.
        /// All 7 SPEC rows are covered by <c>MapCardStateTests</c>.
        ///
        /// Row 1: Upcoming state                                   → Upcoming
        /// Row 2: entry InProgress + in window (!nowPastEnd)       → EnteredActive
        /// Row 3: entry Finished                                   → EnteredFinished
        /// Row 4: entry (any) + nowPastEnd                         → EnteredFinished
        /// Row 5: no entry + Ending                                → Ending
        /// Row 6: no entry + Open (or Playing)                     → Open
        /// Row 7: no entry + nowPastEnd (Closed/Ended)             → Ended
        /// </summary>
        public static TournamentCardState Map(
            TournamentState state,
            EntryStatus entryStatus,
            bool nowPastEnd)
        {
            bool hasEntry = entryStatus != EntryStatus.NotEntered;

            // Row 1
            if (state == TournamentState.Upcoming)
                return TournamentCardState.Upcoming;

            // Row 2
            if (hasEntry && entryStatus == EntryStatus.InProgress && !nowPastEnd)
                return TournamentCardState.EnteredActive;

            // Row 3
            if (hasEntry && entryStatus == EntryStatus.Finished)
                return TournamentCardState.EnteredFinished;

            // Row 4
            if (hasEntry && nowPastEnd)
                return TournamentCardState.EnteredFinished;

            // Row 5
            if (!hasEntry && state == TournamentState.Ending)
                return TournamentCardState.Ending;

            // Row 6
            if (!hasEntry && (state == TournamentState.Open || state == TournamentState.Playing))
                return TournamentCardState.Open;

            // Row 7: no entry + Closed/Ended, or DNF
            return TournamentCardState.Ended;
        }
    }
}
