// ─────────────────────────────────────────────────────────────────────────────
// Golfin.Tournaments — RewardPointsServiceAdapter
// Production adapter for IRewardPointsService.
// Resolves RewardPointsManager.Instance lazily per-call (never cached in a field)
// so it is resilient to init order and domain reload.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;
using Golfin.Economy;
using Golfin.EconomyRuntime;
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
        public void TrySpendAsync(long rp, string reason, Action<bool> onDone)
        {
            int amount = ToInt(rp);

            // Server first (no-op and synchronous when the flag is OFF or the fee is 0), local second.
            // The local debit lives INSIDE the approved callback so the two can never disagree.
            PointsSpendGate.Spend(
                amount,
                string.IsNullOrEmpty(reason) ? SpendReasons.TournamentEntry : reason,
                () => onDone?.Invoke(TrySpend(rp)),
                _ => onDone?.Invoke(false));
        }

        /// <inheritdoc/>
        /// <remarks>
        /// The only caller is <c>LocalTournamentBackend</c>'s prize payout, so the server action is
        /// fixed here rather than threaded through the headless seam — <c>tournament_prize</c> is a
        /// variable-amount catalog action (rank-band payouts differ), capped server-side at
        /// <c>max_per_event</c> 2000 per RP_REBALANCE §3.
        /// </remarks>
        public void Grant(long rp)
        {
            RewardPointsManager.Instance.EarnPoints(ToInt(rp), PointsActions.TournamentPrize);
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
