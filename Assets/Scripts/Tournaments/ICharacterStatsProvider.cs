// ─────────────────────────────────────────────────────────────────────────────
// Golfin.Tournaments — ICharacterStatsProvider + CharacterManagerStatsProvider
//                    + FakeStatsProvider
//
// Pure interface — no UnityEngine — so LocalTournamentBackend stays headless.
// Same pattern as IRewardPointsService wrapping RewardPointsManager.
//
// Production adapter (CharacterManagerStatsProvider) is the only Unity-touching
// part; tests inject FakeStatsProvider to drive the freeze-invariant test.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;
using System.Collections.Generic;

namespace Golfin.Tournaments
{
    // ─────────────────────────────────────────────────────────────────────────
    // Interface
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Seam for reading a character's effective stats as an immutable snapshot.
    /// <para>
    /// Implementations must throw <see cref="KeyNotFoundException"/> when the
    /// <paramref name="characterId"/> is unknown — registering with an unknown
    /// character is a programmer error.
    /// </para>
    /// </summary>
    public interface ICharacterStatsProvider
    {
        /// <summary>
        /// Return an immutable <see cref="CharacterSnapshot"/> for the given character.
        /// </summary>
        /// <exception cref="KeyNotFoundException">
        /// Thrown when <paramref name="characterId"/> is not found.
        /// </exception>
        CharacterSnapshot SnapshotFor(string characterId);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // FakeStatsProvider — mutable source, for test injection
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Test-only implementation of <see cref="ICharacterStatsProvider"/>.
    /// <para>
    /// Callers register a <see cref="CharacterSnapshot"/> per character id via
    /// <see cref="Register"/>. The source is mutable so a test can update the
    /// registered snapshot AFTER calling <c>Register</c> on the backend and then
    /// assert that the frozen entry is unchanged (the freeze-invariant test, SPEC §6.2).
    /// </para>
    /// </summary>
    public sealed class FakeStatsProvider : ICharacterStatsProvider
    {
        private readonly Dictionary<string, CharacterSnapshot> _snapshots
            = new Dictionary<string, CharacterSnapshot>(StringComparer.Ordinal);

        /// <summary>Register or replace the snapshot returned for <paramref name="characterId"/>.</summary>
        public void Register(string characterId, CharacterSnapshot snapshot)
        {
            if (string.IsNullOrEmpty(characterId))
                throw new ArgumentException("characterId must not be null or empty.", nameof(characterId));
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            _snapshots[characterId] = snapshot;
        }

        /// <inheritdoc/>
        public CharacterSnapshot SnapshotFor(string characterId)
        {
            if (_snapshots.TryGetValue(characterId, out var snap))
                return snap;
            throw new KeyNotFoundException(
                $"FakeStatsProvider: no snapshot registered for character id '{characterId}'.");
        }
    }
}
