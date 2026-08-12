// ─────────────────────────────────────────────────────────────────────────────
// Golfin.Tournaments — ITournamentSeams
// The three remaining injected seams for LocalTournamentBackend (T4).
// All are thin adapters over UnityEngine singletons; the interfaces keep
// LocalTournamentBackend headless (no UnityEngine dependency in the logic).
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System.Collections.Generic;

namespace Golfin.Tournaments
{
    // ─────────────────────────────────────────────────────────────────────────
    // IRewardPointsService — wraps RewardPointsManager (int API) behind long
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// RP debit/grant seam for <c>LocalTournamentBackend</c>.
    /// <para>
    /// Adapter wraps <c>RewardPointsManager.Instance</c> (int API) and bridges
    /// to the tournament DTOs which use <c>long</c> for EntryFeeRP / RpReward.
    /// The adapter guards against values that exceed <c>int.MaxValue</c>.
    /// </para>
    /// </summary>
    public interface IRewardPointsService
    {
        /// <summary>Current RP balance (long to match tournament DTO types).</summary>
        long Balance { get; }

        /// <summary>
        /// Attempt to debit <paramref name="rp"/> from the balance.
        /// Returns true and deducts; returns false if insufficient (no mutation on false).
        /// </summary>
        bool TrySpend(long rp);

        /// <summary>
        /// Server-authoritative variant of <see cref="TrySpend"/> (reward_points_backend Slice 2).
        ///
        /// Debits the server ledger FIRST and only then the local balance, reporting the combined
        /// outcome through <paramref name="onDone"/> — which is invoked exactly once, synchronously
        /// when no round-trip is needed (fee of 0, or <c>PointsBackendEnabled</c> OFF) and after the
        /// response otherwise. <c>false</c> means NOTHING was debited anywhere and the caller must
        /// not perform the action.
        ///
        /// Sign-up is a UI/modal flow, so awaiting a round-trip is acceptable here; this exists
        /// because <see cref="TrySpend"/>'s bool return has nowhere to put "the server never
        /// answered", and collapsing that into "insufficient" would tell a player with a bad
        /// connection that they are broke.
        /// </summary>
        /// <param name="reason">Ledger description (see <c>Golfin.Economy.SpendReasons</c>).</param>
        void TrySpendAsync(long rp, string reason, System.Action<bool> onDone);

        /// <summary>Add <paramref name="rp"/> to the balance.</summary>
        void Grant(long rp);
    }

    /// <summary>
    /// In-memory fake <see cref="IRewardPointsService"/> for tests.
    /// Starts with a configurable initial balance; never negative.
    /// </summary>
    public sealed class FakeRewardPointsService : IRewardPointsService
    {
        private long _balance;

        public FakeRewardPointsService(long initialBalance = 10000L)
        {
            _balance = initialBalance;
        }

        public long Balance => _balance;

        public bool TrySpend(long rp)
        {
            if (rp < 0) throw new System.ArgumentOutOfRangeException(nameof(rp), "Must be >= 0");
            if (_balance < rp) return false;
            _balance -= rp;
            return true;
        }

        /// <summary>No server in a fake — completes synchronously with the same answer as
        /// <see cref="TrySpend"/>, which is exactly the flag-OFF production behaviour.</summary>
        public void TrySpendAsync(long rp, string reason, System.Action<bool> onDone)
            => onDone?.Invoke(TrySpend(rp));

        public void Grant(long rp)
        {
            if (rp < 0) throw new System.ArgumentOutOfRangeException(nameof(rp), "Must be >= 0");
            _balance += rp;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // IItemRewardService — wraps SaveData.itemQuantities
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Item grant seam for <c>LocalTournamentBackend</c>.
    /// v1: in-memory fake (tests + T4 prod until T5 wires the real Save path).
    /// </summary>
    public interface IItemRewardService
    {
        /// <summary>Grant <paramref name="qty"/> of <paramref name="itemId"/> to the player.</summary>
        void Grant(string itemId, int qty);
    }

    /// <summary>In-memory fake <see cref="IItemRewardService"/> for tests and v1 production.</summary>
    public sealed class FakeItemRewardService : IItemRewardService
    {
        private readonly Dictionary<string, int> _granted
            = new Dictionary<string, int>(System.StringComparer.Ordinal);

        /// <summary>Returns a read-only snapshot of all items granted so far (for test assertions).</summary>
        public IReadOnlyDictionary<string, int> Granted => _granted;

        public void Grant(string itemId, int qty)
        {
            if (string.IsNullOrEmpty(itemId)) return;
            if (!_granted.TryGetValue(itemId, out int current))
                current = 0;
            _granted[itemId] = current + qty;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // IHoleParProvider — wraps HoleDatabaseLoader → HoleData.par
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Per-hole par provider seam for <c>LocalTournamentBackend</c>.
    /// Maps (clubId + holeSet) → ordered par values consumed by
    /// <c>BotFieldGenerator.RollField</c>.
    /// </summary>
    public interface IHoleParProvider
    {
        /// <summary>
        /// Return the par values for each hole in <paramref name="holeSet"/>,
        /// in the same order as the list.
        /// </summary>
        IReadOnlyList<int> ParsFor(string clubId, IReadOnlyList<string> holeSet);
    }

    /// <summary>
    /// Fixed-par fake <see cref="IHoleParProvider"/> for tests.
    /// Returns a configurable par (default 4) for every hole, regardless of id.
    /// </summary>
    public sealed class FakeHoleParProvider : IHoleParProvider
    {
        private readonly int _par;
        public FakeHoleParProvider(int par = 4) { _par = par; }

        public IReadOnlyList<int> ParsFor(string clubId, IReadOnlyList<string> holeSet)
        {
            var pars = new int[holeSet.Count];
            for (int i = 0; i < pars.Length; i++)
                pars[i] = _par;
            return pars;
        }
    }
}
