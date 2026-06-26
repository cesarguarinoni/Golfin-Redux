// ─────────────────────────────────────────────────────────────────────────────
// Golfin.Tournaments — ITournamentEntryStore
// T4/T5 boundary seam. v1 ships an in-memory implementation here.
// T5 (tournament_save_entry) swaps in the Golfin.Save-backed implementation.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System.Collections.Generic;

namespace Golfin.Tournaments
{
    /// <summary>
    /// Persistence seam for the player's tournament entry state.
    /// <para>
    /// v1 (T4): <see cref="InMemoryEntryStore"/> — per-session, not persisted across restarts.
    /// v2 (T5): <c>SaveBackedEntryStore</c> — writes to <c>Golfin.Save.SaveData</c>.
    /// </para>
    /// <para>
    /// This is the T4/T5 boundary: T4 produces it, T5 replaces the impl without touching
    /// <c>LocalTournamentBackend</c>.
    /// </para>
    /// </summary>
    public interface ITournamentEntryStore
    {
        /// <summary>
        /// Load the player's entry for <paramref name="tournamentId"/>.
        /// Returns null if the player has not registered.
        /// </summary>
        EntryState? Load(string tournamentId);

        /// <summary>Persist (create or overwrite) the player's entry for the tournament.</summary>
        void Save(EntryState entry);

        /// <summary>
        /// Returns true if the prize for <paramref name="tournamentId"/> has already been claimed.
        /// Persisted so a simulated restart (new backend instance, same store) prevents re-claim.
        /// </summary>
        bool IsClaimed(string tournamentId);

        /// <summary>
        /// Mark the prize for <paramref name="tournamentId"/> as claimed.
        /// Idempotent — calling twice is safe.
        /// </summary>
        void MarkClaimed(string tournamentId);
    }

    /// <summary>
    /// In-memory implementation of <see cref="ITournamentEntryStore"/>.
    /// Data lives only for the current process/session; not persisted across restarts.
    /// Used in T4 v1 production and in all T4 unit tests.
    /// Replaced by the Golfin.Save-backed implementation in T5.
    /// </summary>
    public sealed class InMemoryEntryStore : ITournamentEntryStore
    {
        private readonly Dictionary<string, EntryState> _store
            = new Dictionary<string, EntryState>(System.StringComparer.Ordinal);

        // Claim-once set: tournaments whose prize has been claimed.
        // In T5, this is backed by Golfin.Save so it survives app restart.
        private readonly HashSet<string> _claimed
            = new HashSet<string>(System.StringComparer.Ordinal);

        /// <inheritdoc/>
        public EntryState? Load(string tournamentId)
        {
            _store.TryGetValue(tournamentId, out var entry);
            return entry;
        }

        /// <inheritdoc/>
        public void Save(EntryState entry)
        {
            _store[entry.TournamentId] = entry;
        }

        /// <inheritdoc/>
        public bool IsClaimed(string tournamentId)
            => _claimed.Contains(tournamentId);

        /// <inheritdoc/>
        public void MarkClaimed(string tournamentId)
            => _claimed.Add(tournamentId);
    }
}
