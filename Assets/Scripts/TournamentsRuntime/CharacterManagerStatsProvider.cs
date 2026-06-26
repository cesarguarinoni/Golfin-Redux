// ─────────────────────────────────────────────────────────────────────────────
// Golfin.Tournaments — CharacterManagerStatsProvider
// Production adapter for ICharacterStatsProvider.
// The ONLY UnityEngine-touching part of the snapshot seam.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System.Collections.Generic;
using Golfin.Roster;

namespace Golfin.Tournaments
{
    /// <summary>
    /// Production <see cref="ICharacterStatsProvider"/> that reads
    /// <c>CharacterManager.Instance.GetCharacterData(characterId)</c> →
    /// <c>PlayerCharacterData</c> and copies the five relevant fields into a
    /// <see cref="CharacterSnapshot"/>.
    /// <para>
    /// <b>Read path chosen:</b> direct <c>current*</c> read (no <c>RefreshStatValues</c>
    /// call first). The <c>current*</c> fields are maintained up-to-date on every SP
    /// confirm (see <c>PlayerCharacterData.RefreshStatValues</c> called from
    /// <c>CharacterManager.ConfirmSPAllocation</c>), so a direct read is correct and safe.
    /// Calling RefreshStatValues here would also require passing a CharacterData template
    /// (roster ScriptableObject), adding coupling; the direct read avoids it.
    /// </para>
    /// </summary>
    public sealed class CharacterManagerStatsProvider : ICharacterStatsProvider
    {
        /// <inheritdoc/>
        public CharacterSnapshot SnapshotFor(string characterId)
        {
            var mgr     = CharacterManager.Instance;
            var charData = mgr?.GetCharacterData(characterId);

            if (charData == null)
                throw new KeyNotFoundException(
                    $"CharacterManagerStatsProvider: character '{characterId}' not found in CharacterManager.");

            return new CharacterSnapshot(
                characterId: charData.characterId,
                level:       charData.currentLevel,
                strength:    charData.currentStrength,
                clubControl: charData.currentClubControl,
                recovery:    charData.currentRecovery,
                stamina:     charData.currentStamina);
        }
    }
}
