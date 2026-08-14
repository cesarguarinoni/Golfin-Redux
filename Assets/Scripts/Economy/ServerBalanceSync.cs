// Order: rp_balance_sync §3.2 — the inbound half of the Slice-2 cutover.
using System;
using UnityEngine;

namespace Golfin.Economy
{
    /// <summary>
    /// Whatever owns the RP number the player sees. Implemented in Assembly-CSharp by
    /// <c>Golfin.EconomyRuntime.ServerBalanceSyncBehaviour</c>, which forwards to
    /// <c>RewardPointsManager.ApplyServerBalance</c>.
    ///
    /// The seam exists because <c>Golfin.Economy</c> must not reference Assembly-CSharp — the same
    /// split as <c>IRewardPointsService</c> / <c>RewardPointsServiceAdapter</c> in the tournament
    /// stack. It also makes the rules below (pending-earn addition, the never-render-unknown gate)
    /// testable without a MonoBehaviour, a save file or a scene.
    /// </summary>
    public interface IServerBalanceSink
    {
        /// <summary>Show <paramref name="total"/> RP. Called on the main thread, only ever with a
        /// number derived from a real server answer.</summary>
        void ApplyServerBalance(int total);
    }

    /// <summary>
    /// Binds <see cref="PointsService.OnDisplayBalanceChanged"/> to an <see cref="IServerBalanceSink"/>.
    ///
    /// Slice 2 taught the game to WRITE to the ledger and never to read it back, so the nav bar showed
    /// a stale local number indefinitely (rp_balance_sync §1). This is the missing wire, and it is
    /// deliberately the ONLY one: every existing RP consumer already listens to
    /// <c>RewardPointsManager.OnPointsChanged</c>, so nothing downstream needs rewriting.
    ///
    /// Static, like <see cref="Golfin.EconomyRuntime"/>'s spend gate, because there is exactly one
    /// player, one ledger and one counter.
    /// </summary>
    public static class ServerBalanceSync
    {
        private static PointsService _service;
        private static IServerBalanceSink _sink;

        /// <summary>True while a sink is receiving server balances.</summary>
        public static bool IsBound => _sink != null;

        /// <summary>
        /// Route <paramref name="service"/>'s displayed balance into <paramref name="sink"/>, and push
        /// the current value immediately when one is already known — a sink that binds after the first
        /// refresh (late scene load, domain reload) must not have to wait for the next change.
        ///
        /// Rebinding replaces the previous binding rather than stacking a second subscription.
        /// </summary>
        public static void Bind(PointsService service, IServerBalanceSink sink)
        {
            if (service == null || sink == null)
            {
                Debug.LogError("[ServerBalanceSync] Bind called with a null service or sink — ignored.");
                return;
            }

            Unbind();

            _service = service;
            _sink = sink;
            _service.OnDisplayBalanceChanged += OnDisplayBalanceChanged;

            // §3.5: with no answer this session there is nothing authoritative to push, and the cached
            // local value must stand. HasBalance is what separates "0 RP" from "unknown".
            if (_service.HasBalance)
                Push(_service.DisplayBalance);
        }

        /// <summary>Stop routing. Safe to call when nothing is bound.</summary>
        public static void Unbind()
        {
            if (_service != null)
                _service.OnDisplayBalanceChanged -= OnDisplayBalanceChanged;

            _service = null;
            _sink = null;
        }

        private static void OnDisplayBalanceChanged(int total) => Push(total);

        private static void Push(int total)
        {
            IServerBalanceSink sink = _sink;
            if (sink == null) return;

            try
            {
                sink.ApplyServerBalance(total);
            }
            catch (Exception ex)
            {
                // A throwing sink must not poison the service's event list for everything else.
                Debug.LogError($"[ServerBalanceSync] Sink threw applying balance {total}: {ex}");
            }
        }
    }
}
