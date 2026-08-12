// Order: reward_points_backend Slice 1 — server balance + queued earns. INERT while the flag is OFF.
using System;
using System.Collections;
using Golfin.Net;
using UnityEngine;

namespace Golfin.Economy
{
    /// <summary>
    /// The game's view of the server-side points ledger (SPEC §4 Slice 1).
    ///
    /// SCOPE. This slice adds capability only. It does NOT touch <c>RewardPointsManager</c> or any of its
    /// call sites, and with <see cref="PointsBackendFlag"/> OFF every public method here short-circuits
    /// before any HTTP, any disk write and any GameObject creation. Re-pointing the game at this service
    /// is Slice 2.
    ///
    /// Plain C# singleton (not a MonoBehaviour) so EditMode tests can construct it directly; it borrows
    /// <see cref="ApiClient.Run"/> when it needs a coroutine host.
    /// </summary>
    public sealed class PointsService
    {
        private static PointsService _instance;

        /// <summary>Lazily built against the shipping <see cref="ApiClient"/> and the persistent queue
        /// file. Nothing constructs this while the flag is OFF, because nothing calls it.</summary>
        public static PointsService Instance => _instance ?? (_instance = new PointsService(
            ApiClient.Instance,
            new PendingOpsQueue(new FilePendingOpsStore(FilePendingOpsStore.DefaultPath))));

        private readonly ApiClient _client;

        public PointsService(ApiClient client, PendingOpsQueue queue)
        {
            _client = client;
            Queue = queue ?? new PendingOpsQueue(new InMemoryPendingOpsStore());
            Queue.Load();
        }

        public static void ConfigureForTest(PointsService service) => _instance = service;

        public static void ResetForTest() => _instance = null;

        // ── cached balance ────────────────────────────────────────────────────────

        /// <summary>Last balance the server reported, or null if it has never answered this session.</summary>
        public PointsBalance LastBalance { get; private set; }

        /// <summary>True once a server balance has been observed. Distinguishes "0 RP" from "unknown".</summary>
        public bool HasBalance => LastBalance != null;

        /// <summary>Cached Reward Points (= <c>total_points</c>). 0 when unknown — check <see cref="HasBalance"/>.</summary>
        public int Balance => LastBalance != null ? LastBalance.TotalPoints : 0;

        /// <summary>Fires whenever the cached balance changes value. No subscribers exist in Slice 1.</summary>
        public event Action<int> OnBalanceChanged;

        /// <summary>The persistent pending-earn queue.</summary>
        public PendingOpsQueue Queue { get; }

        // ── balance ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Fire-and-forget refresh of the cached balance from <c>GET /api/v1/points/balance</c>.
        ///
        /// Named per SPEC §4; it is callback-shaped rather than Task-shaped to match the auth epic's
        /// deliberately Unity-idiomatic, coroutine-friendly convention (see <c>ISupabaseAuthClient</c>).
        /// <paramref name="onResult"/> is invoked exactly once on the main thread.
        ///
        /// This is the manual acceptance path: flag ON + signed in → the test account's real server
        /// balance is logged.
        /// </summary>
        public void RefreshBalanceAsync(Action<ApiResult<PointsBalance>> onResult = null)
            => _client.Run(RefreshBalanceRoutine(onResult));

        /// <summary>Coroutine form of <see cref="RefreshBalanceAsync"/>. The flag gate lives HERE, not in
        /// the wrapper, so neither entry point can reach the network with the flag off.</summary>
        public IEnumerator RefreshBalanceRoutine(Action<ApiResult<PointsBalance>> onResult)
        {
            if (!PointsBackendFlag.Enabled)
            {
                onResult?.Invoke(Disabled<PointsBalance>());
                yield break;
            }

            ApiResult<PointsBalance> result = null;
            IEnumerator call = _client.Get<PointsBalance>(Endpoints.PointsBalance, r => result = r);
            while (call.MoveNext()) yield return call.Current;

            if (result != null && result.Success && result.Data != null)
            {
                ApplyBalance(result.Data);
                Debug.Log($"[PointsService] Server balance: {result.Data}");
            }
            else if (result != null)
            {
                Debug.LogWarning($"[PointsService] Balance refresh failed: {result}");
            }

            onResult?.Invoke(result);
        }

        // ── queued earns ──────────────────────────────────────────────────────────

        /// <summary>
        /// Record an earn for the server. Slice 1 only ENQUEUES — it does not send, and no game code
        /// calls it yet; Slice 2 wires it behind <c>EarnPoints</c> while the local balance, leaderboard
        /// accumulators and SFX stay exactly as they are today.
        ///
        /// Returns null when the flag is OFF (nothing is written to disk).
        /// </summary>
        public PendingPointsOp EnqueueEarn(string action, int amount)
        {
            if (!PointsBackendFlag.Enabled) return null;

            if (string.IsNullOrEmpty(action))
            {
                Debug.LogError("[PointsService] EnqueueEarn called with no action — ignored.");
                return null;
            }

            var op = Queue.EnqueueEarn(action, amount);
            Debug.Log($"[PointsService] Queued earn {op}. Pending={Queue.Count}.");
            return op;
        }

        /// <summary>Fire-and-forget replay of the pending queue. Slice 2 calls this on reconnect/login.</summary>
        public void ReplayPendingAsync(Action<int> onDone = null)
            => _client.Run(ReplayPendingRoutine(onDone));

        /// <summary>
        /// Drain the queue oldest-first, one op at a time.
        ///
        /// STOPS at the first transport-level failure and leaves that op at the head, so FIFO order is
        /// preserved across reconnects (see <see cref="PendingOpsQueue"/> for why order matters). A
        /// server REFUSAL (<c>{awarded:0, reason:…}</c> — unknown action, daily cap) is a definitive
        /// answer, not a transport failure, so that op is consumed rather than retried forever.
        ///
        /// <paramref name="onDone"/> receives the number of ops the server accepted.
        /// </summary>
        public IEnumerator ReplayPendingRoutine(Action<int> onDone)
        {
            int sent = 0;

            if (!PointsBackendFlag.Enabled)
            {
                onDone?.Invoke(0);
                yield break;
            }

            while (Queue.Count > 0)
            {
                PendingPointsOp op = Queue.Peek();
                if (op == null) break;

                ApiResult<PointsEarnResult> result = null;
                IEnumerator call = _client.Post<PointsEarnResult>(
                    Endpoints.PointsEarnGame, op.ToEarnGameJson(), r => result = r);
                while (call.MoveNext()) yield return call.Current;

                op.AttemptCount++;

                if (result == null || !result.Success)
                {
                    // Persist the bumped attempt count, then stop — the next op must NOT jump the queue.
                    Queue.Save();
                    Debug.LogWarning($"[PointsService] Replay halted at {op}: " +
                                     $"{(result != null ? result.ToString() : "no result")}. Pending={Queue.Count}.");
                    break;
                }

                Queue.Dequeue();
                sent++;

                PointsEarnResult data = result.Data;
                if (data == null)
                {
                    Debug.LogWarning($"[PointsService] Replayed {op} but the server sent no payload.");
                    continue;
                }

                if (data.WasRefused)
                {
                    Debug.LogWarning($"[PointsService] Server refused {op}: {data.Reason}. Op consumed.");
                    continue;
                }

                ApplyEarn(data);
                Debug.Log($"[PointsService] Replayed {op} → awarded {data.Awarded}, " +
                          $"total {data.TotalPoints}{(data.Replayed ? " (idempotent replay)" : "")}. " +
                          $"Pending={Queue.Count}.");
            }

            onDone?.Invoke(sent);
        }

        // ── internals ─────────────────────────────────────────────────────────────

        private void ApplyBalance(PointsBalance balance)
        {
            if (balance == null) return;

            int before = Balance;
            bool hadBalance = HasBalance;
            LastBalance = balance;

            if (!hadBalance || before != balance.TotalPoints)
                OnBalanceChanged?.Invoke(balance.TotalPoints);
        }

        /// <summary>
        /// Fold an earn response into the cached balance. The earn payload carries no <c>gift_pts</c>
        /// (earns only ever touch activity_pts), so the cached gift bucket is CARRIED FORWARD rather
        /// than zeroed — writing 0 there would under-report RP until the next full refresh.
        /// </summary>
        private void ApplyEarn(PointsEarnResult earn)
        {
            if (earn == null) return;

            ApplyBalance(new PointsBalance
            {
                ActivityPts = earn.ActivityPts,
                GiftPts     = LastBalance != null ? LastBalance.GiftPts : 0,
                TotalPoints = earn.TotalPoints,
                AvatarLevel = earn.AvatarLevel,
                AvatarXp    = earn.AvatarXp
            });
        }

        private static ApiResult<T> Disabled<T>() => ApiResult<T>.Fail(
            ApiErrorKind.Disabled,
            "PointsBackendEnabled is OFF — no request was made.",
            0, null, 0);
    }
}
