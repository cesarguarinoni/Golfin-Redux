// ─────────────────────────────────────────────────────────────────────────────
// Golfin.Tournaments — CharacterSnapshot
// Immutable freeze of a character's effective stats captured at tournament
// sign-up. Primitives only — no UnityEngine dependency — trivially serializable
// for the T5 save migration.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;

namespace Golfin.Tournaments
{
    /// <summary>
    /// Immutable snapshot of a character's effective stats at tournament registration.
    /// <para>
    /// Captured by <see cref="ICharacterStatsProvider.SnapshotFor"/> at the point
    /// <c>LocalTournamentBackend.Register</c> builds a new <see cref="EntryState"/>.
    /// Stored on <see cref="EntryState.Snapshot"/>; persisted by T5.
    /// </para>
    /// <para>
    /// The four <c>current*</c> stats from <c>PlayerCharacterData</c> are already the
    /// final capped effective values (base + spent SP, clamped by rarity cap), so
    /// raw SP allocation and rarity are not needed here.
    /// Stamina ENERGY (the depleting per-hole bar) is excluded — that is T6 runtime state.
    /// </para>
    /// </summary>
    public sealed class CharacterSnapshot
    {
        /// <summary>Character id matching the roster entry.</summary>
        public string CharacterId { get; }

        /// <summary>Current level at time of sign-up.</summary>
        public int Level { get; }

        /// <summary>Effective Strength stat (currentStrength, capped by rarity).</summary>
        public int Strength { get; }

        /// <summary>Effective Club Control stat (currentClubControl, capped by rarity).</summary>
        public int ClubControl { get; }

        /// <summary>Effective Recovery stat (currentRecovery, capped by rarity).</summary>
        public int Recovery { get; }

        /// <summary>
        /// Effective Stamina STAT (currentStamina, capped by rarity).
        /// This is the stat ceiling; stamina energy depletion across holes is T6's concern.
        /// </summary>
        public int Stamina { get; }

        public CharacterSnapshot(
            string characterId,
            int    level,
            int    strength,
            int    clubControl,
            int    recovery,
            int    stamina)
        {
            if (string.IsNullOrEmpty(characterId))
                throw new ArgumentException("characterId must not be null or empty.", nameof(characterId));

            CharacterId = characterId;
            Level       = level;
            Strength    = strength;
            ClubControl = clubControl;
            Recovery    = recovery;
            Stamina     = stamina;
        }

        /// <summary>
        /// Value equality — two snapshots with the same field values are considered equal.
        /// Useful for test assertions.
        /// </summary>
        public override bool Equals(object? obj)
        {
            if (obj is not CharacterSnapshot other) return false;
            return string.Equals(CharacterId, other.CharacterId, StringComparison.Ordinal)
                && Level       == other.Level
                && Strength    == other.Strength
                && ClubControl == other.ClubControl
                && Recovery    == other.Recovery
                && Stamina     == other.Stamina;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int h = StringComparer.Ordinal.GetHashCode(CharacterId);
                h = h * 31 + Level;
                h = h * 31 + Strength;
                h = h * 31 + ClubControl;
                h = h * 31 + Recovery;
                h = h * 31 + Stamina;
                return h;
            }
        }

        public override string ToString()
            => $"CharacterSnapshot({CharacterId} Lv{Level}: STR={Strength} CTRL={ClubControl} REC={Recovery} STAM={Stamina})";
    }
}
