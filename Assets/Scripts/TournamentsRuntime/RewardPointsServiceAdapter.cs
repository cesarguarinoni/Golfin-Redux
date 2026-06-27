// ─────────────────────────────────────────────────────────────────────────────
// Golfin.Tournaments — RewardPointsServiceAdapter
// Production adapter for IRewardPointsService.
// Resolves RewardPointsManager.Instance lazily per-call (never cached in a field)
// so it is resilient to init order and domain reload.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using Golfin.Roster;
using UnityEngine;

namespace Golfin.Tournaments
{
    /// <summary>
    /// Production <see cref="IRewardPointsService"/> that delegates to
    /// <c>RewardPointsManager.Instance</c> (int API) and bridges to the
    /// tournament DTOs which use <c>long</c> for EntryFeeRP / RpReward.
    /// <para>
    /// Resolves <c>RewardPointsManager.Instance</c> lazily per-call — never cached.
    /// </para>
    /// </summary>
    public sealed class RewardPointsServiceAdapter : IRewardPointsService
    {
        /// <inheritdoc/>
        public long Balance => (long)RewardPointsManager.Instance.GetPoints();

        /// <inheritdoc/>
        public bool TrySpend(long rp)
        {
            return RewardPointsManager.Instance.SpendPoints(ToInt(rp));
        }

        /// <inheritdoc/>
        public void Grant(long rp)
        {
            RewardPointsManager.Instance.EarnPoints(ToInt(rp));
        }

        // ── int ↔ long bridging ───────────────────────────────────────────────

        /// <summary>
        /// Narrows a <c>long</c> to <c>int</c> with clamp-and-log guard.
        /// RP values in practice are small; overflow would indicate a data error.
        /// </summary>
        internal static int ToInt(long amt)
        {
            if (amt > int.MaxValue)
            {
                Debug.LogError(
                    $"[RewardPointsServiceAdapter] ToInt: value {amt} exceeds int.MaxValue — clamping to {int.MaxValue}. " +
                    "This indicates a bug or corrupt tournament data (RP fees/prizes should be small integers).");
                return int.MaxValue;
            }
            if (amt < 0)
            {
                Debug.LogError(
                    $"[RewardPointsServiceAdapter] ToInt: negative value {amt} — clamping to 0.");
                return 0;
            }
            return (int)amt;
        }
    }
}
