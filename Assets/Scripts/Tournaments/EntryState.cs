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
        /// Frozen character stats captured at sign-up via <see cref="ICharacterStatsProvider"/>.
        /// Immutable for the lifetime of this entry — levelling up or swapping the roster
        /// character after registration has no effect on the tournament character.
        /// <para>
        /// Added by the tournament_character_snapshot amendment.
        /// Equals <c>CharacterId</c> in <see cref="CharacterSnapshot.CharacterId"/>;
        /// both fields are kept for backwards-compatible serialization during T5 migration.
        /// </para>
        /// <para>Null only on entries created before this amendment (legacy data).</para>
        /// </summary>
        public CharacterSnapshot? Snapshot { get; }

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

        /// <summary>
        /// Primary constructor — includes a frozen <see cref="CharacterSnapshot"/>.
        /// Used by <c>LocalTournamentBackend.Register</c> after the snapshot amendment.
        /// </summary>
        public EntryState(
            string tournamentId,
            string characterId,
            CharacterSnapshot? snapshot,
            IReadOnlyList<HoleResult> perHole,
            DateTime startedUtc,
            DateTime? lastHoleUtc,
            EntryStatus status)
        {
            TournamentId = tournamentId;
            CharacterId  = characterId;
            Snapshot     = snapshot;
            PerHole      = perHole ?? new List<HoleResult>();
            StartedUtc   = startedUtc;
            LastHoleUtc  = lastHoleUtc;
            Status       = status;
        }

        /// <summary>
        /// Legacy constructor — no snapshot (pre-amendment entries and SubmitHoleResult
        /// which clones the entry while preserving the existing snapshot).
        /// </summary>
        public EntryState(
            string tournamentId,
            string characterId,
            IReadOnlyList<HoleResult> perHole,
            DateTime startedUtc,
            DateTime? lastHoleUtc,
            EntryStatus status)
            : this(tournamentId, characterId, snapshot: null, perHole, startedUtc, lastHoleUtc, status)
        {
        }
    }
}
