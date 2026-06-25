// ─────────────────────────────────────────────────────────────────────────────
// Golfin.Tournaments — EntryState
// Resumable player entry for one tournament. Returned by Register/GetMyEntry/
// SubmitHoleResult on ITournamentBackend.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;
using System.Collections.Generic;

namespace Golfin.Tournaments
{
    /// <summary>
    /// The player's entry state for a specific tournament.
    /// Resumable — persisted in Golfin.Save (schema defined in T5).
    /// </summary>
    public sealed class EntryState
    {
        /// <summary>Id of the tournament this entry belongs to.</summary>
        public string TournamentId { get; }

        /// <summary>
        /// Character locked at sign-up (GDD Decision S1).
        /// Cannot be changed after registration; the registered character is used for
        /// all holes in this entry.
        /// </summary>
        public string CharacterId { get; }

        /// <summary>
        /// Per-hole results submitted so far (append-only; grows as the player
        /// completes holes). Empty until the first hole is submitted.
        /// </summary>
        public IReadOnlyList<HoleResult> PerHole { get; }

        /// <summary>UTC instant when the player registered for the tournament.</summary>
        public DateTime StartedUtc { get; }

        /// <summary>
        /// UTC instant of the most recently submitted hole result.
        /// Null when no holes have been completed yet.
        /// </summary>
        public DateTime? LastHoleUtc { get; }

        /// <summary>Current status of this entry.</summary>
        public EntryStatus Status { get; }

        public EntryState(
            string tournamentId,
            string characterId,
            IReadOnlyList<HoleResult> perHole,
            DateTime startedUtc,
            DateTime? lastHoleUtc,
            EntryStatus status)
        {
            TournamentId = tournamentId;
            CharacterId  = characterId;
            PerHole      = perHole ?? new List<HoleResult>();
            StartedUtc   = startedUtc;
            LastHoleUtc  = lastHoleUtc;
            Status       = status;
        }
    }
}
