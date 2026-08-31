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

            // rp_balance_sync §3.4: the displayed number is server + pending, so a queue mutation
            // moves it just as much as a server answer does. Subscribed AFTER Load so the initial
            // read does not raise into a half-built service.
            Queue.OnChanged += OnQueueChanged;
            _lastDisplayBalance = DisplayBalance;
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

        /// <summary>Fires whenever the cached SERVER balance changes value. Consumers that render RP
        /// should prefer <see cref="OnDisplayBalanceChanged"/>, which also accounts for queued earns
        /// the server has not seen yet (rp_balance_sync §3.4).</summary>
        public event Action<int> OnBalanceChanged;

        /// <summary>The persistent pending-earn queue.</summary>
        public PendingOpsQueue Queue { get; }

        // ── displayed balance (rp_balance_sync §3.4) ──────────────────────────────

        /// <summary>RP that has been paid to the player locally but not yet accepted by the server.</summary>
        public int PendingEarnTotal => Queue != null ? Queue.PendingEarnTotal : 0;

        /// <summary>
        /// What the player should actually see: the server balance PLUS everything still queued.
        ///
        /// A queued earn has already been shown to the player and already added to the local save, but
        /// by definition is not in the server total yet. Rendering the raw server balance would make
        /// freshly earned points visibly disappear until the queue flushed — a worse bug than the stale
        /// counter this feature exists to fix.
        ///
        /// Meaningless while <see cref="HasBalance"/> is false (the server half is unknown, not zero),
        /// which is why <see cref="OnDisplayBalanceChanged"/> stays silent until the first answer.
        /// </summary>
        public int DisplayBalance => Balance + PendingEarnTotal;

        /// <summary>
        /// Fires when <see cref="DisplayBalance"/> changes — from a server answer OR a queue mutation.
        /// NEVER fires while <see cref="HasBalance"/> is false: with no server answer this session there
        /// is nothing authoritative to show, and the cached local value must stand (§3.5).
        /// </summary>
        public event Action<int> OnDisplayBalanceChanged;

        private int _lastDisplayBalance;

        private void OnQueueChanged() => RaiseDisplayBalanceIfChanged();

        /// <param name="force">Raise even when the number is unchanged. Used on the FIRST server answer
        /// of the session: "unknown" becoming "0" is a real transition the display must follow, and it
        /// is indistinguishable from no-change by value alone.</param>
        private void RaiseDisplayBalanceIfChanged(bool force = false)
        {
            int now = DisplayBalance;
            bool changed = now != _lastDisplayBalance;
            _lastDisplayBalance = now;

            if (!HasBalance) return;      // unknown ≠ 0 — see §3.5
            if (!changed && !force) return;

            OnDisplayBalanceChanged?.Invoke(now);
        }

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

                // Dequeue drops this op out of PendingEarnTotal and ApplyEarn puts the same points into
                // the server total a few lines below, with no yield in between — so the displayed
                // balance dips and recovers inside ONE frame and nothing is ever painted low. A refused
                // op deliberately keeps the dip: the server rejected that earn, so the optimistic local
                // credit for it SHOULD evaporate.
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

        // ── spends (online-required, never queued) ────────────────────────────────

        /// <summary>
        /// Fire-and-forget server debit (SPEC §4 Slice 2). Spends are ONLINE-ONLY by design
        /// (decision of record #2) — there is deliberately no queue fallback here, because a queued
        /// spend would let the player buy something the server later refuses.
        ///
        /// <paramref name="onDone"/> is invoked exactly once. Callers act on
        /// <see cref="SpendOutcome.Approved"/> and do nothing otherwise.
        /// </summary>
        public void SpendAsync(int amount, string reason, Action<SpendOutcome> onDone)
            => _client.Run(SpendRoutine(amount, reason, onDone));

        /// <summary>Coroutine form of <see cref="SpendAsync"/>. The flag gate lives HERE so neither
        /// entry point can reach the network with the flag off.</summary>
        public IEnumerator SpendRoutine(int amount, string reason, Action<SpendOutcome> onDone)
        {
            if (!PointsBackendFlag.Enabled)
            {
                // Flag OFF is not a refusal — it means "the server is not in this build's loop",
                // and the caller falls through to the unchanged local-only path.
                onDone?.Invoke(SpendOutcome.Disabled());
                yield break;
            }

            if (amount <= 0)
            {
                // A free action still has to run. Nothing to debit, nothing to ask.
                onDone?.Invoke(SpendOutcome.FreeOfCharge());
                yield break;
            }

            if (string.IsNullOrEmpty(reason))
            {
                Debug.LogError("[PointsService] SpendAsync called with no reason — refusing to debit.");
                onDone?.Invoke(SpendOutcome.Unavailable(null));
                yield break;
            }

            string body = BuildSpendJson(amount, reason, Guid.NewGuid().ToString("D"));

            ApiResult<PointsSpendResult> result = null;
            IEnumerator call = _client.Post<PointsSpendResult>(Endpoints.PointsSpend, body, r => result = r);
            while (call.MoveNext()) yield return call.Current;

            if (result == null || !result.Success || result.Data == null)
            {
                Debug.LogWarning($"[PointsService] Spend of {amount} ({reason}) failed: " +
                                 $"{(result != null ? result.ToString() : "no result")}");
                onDone?.Invoke(SpendOutcome.Unavailable(result));
                yield break;
            }

            PointsSpendResult data = result.Data;

            if (data.IsInsufficient)
            {
                // 200 OK with status:"insufficient" — a definitive answer, and nothing was written.
                Debug.Log($"[PointsService] Spend of {amount} ({reason}) refused: {data}");
                ApplySpend(data);
                onDone?.Invoke(SpendOutcome.Insufficient(data, result));
                yield break;
            }

            // ── Mode entry, server-validated (game_modes_admin §4) ───────────────────────────
            //
            // Three more definitive 200s, and NONE of them calls ApplySpend: nothing was debited,
            // so the cached balance is already right and folding a payload that carries no
            // post-debit total would zero it. (`insufficient` above DOES apply, because its payload
            // carries the real balance — that is how a client that thought it was rich finds out.)
            //
            // Falling through to the "unrecognised status" branch instead would report all three as
            // Unavailable, i.e. as a connection problem, and a `fee_changed` — which every open
            // session gets when a publish lands — would tell the player they are offline.
            if (data.IsFeeChanged)
            {
                Debug.Log($"[PointsService] Spend of {amount} ({reason}) re-priced by the server: {data}");
                onDone?.Invoke(SpendOutcome.FeeChanged(data, result));
                yield break;
            }

            if (data.IsUnknownMode)
            {
                Debug.LogWarning($"[PointsService] Spend of {amount} ({reason}) refused: {data} — " +
                                 "this client is holding a mode the published catalog does not carry.");
                onDone?.Invoke(SpendOutcome.UnknownMode(data, result));
                yield break;
            }

            if (data.IsModeLocked)
            {
                Debug.LogWarning($"[PointsService] Spend of {amount} ({reason}) refused: {data} — " +
                                 "the mode is published Coming Soon.");
                onDone?.Invoke(SpendOutcome.ModeLocked(data, result));
                yield break;
            }

            if (!data.IsOk)
            {
                Debug.LogWarning($"[PointsService] Spend of {amount} ({reason}) returned an " +
                                 $"unrecognised status '{data.Status}' — treating as unavailable.");
                onDone?.Invoke(SpendOutcome.Unavailable(result));
                yield break;
            }

            Debug.Log($"[PointsService] Spent {amount} ({reason}) → {data}");

            // ORDER MATTERS (rp_balance_sync). onDone is what runs the LOCAL debit
            // (RewardPointsManager.SpendPoints inside PointsSpendGate's onApproved). Folding the
            // post-debit server total into the cache BEFORE that would push the already-debited number
            // into the display, and the local debit would then subtract the same amount a second time —
            // the counter would sit one spend too low until the next refresh. Applying it afterwards
            // means the two agree and ApplyServerBalance no-ops. finally: a throwing call site must not
            // leave the cached balance stale.
            try
            {
                onDone?.Invoke(SpendOutcome.Ok(data, result));
            }
            finally
            {
                ApplySpend(data);
            }
        }

        /// <summary>Request body for <c>POST /api/v1/points/spend</c>. Field names match the deployed
        /// <c>SpendRequest</c> pydantic model: <c>{amount, reason, idempotency_key}</c>.
        /// Public so the tests can pin the wire shape without a live transport, mirroring
        /// <see cref="PendingPointsOp.ToEarnGameJson"/>.</summary>
        public static string BuildSpendJson(int amount, string reason, string idempotencyKey)
            => Newtonsoft.Json.JsonConvert.SerializeObject(new SpendBody
            {
                amount = amount,
                reason = reason,
                idempotency_key = idempotencyKey
            });

        // Mirrors backend/routers/points.py::SpendRequest — snake_case on purpose.
        private sealed class SpendBody
        {
            public int amount;
            public string reason;
            public string idempotency_key;
        }

        // ── internals ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Fold a spend payload that came from somewhere OTHER than <see cref="SpendRoutine"/> into the
        /// cached balance.
        ///
        /// <para>
        /// Added by shop_server_purchase §3.1 for <see cref="ShopPurchaseService"/>: a shop purchase no
        /// longer goes through <c>/points/spend</c>, but its <c>ok</c> payload is a SUPERSET of
        /// <see cref="PointsSpendResult"/> precisely so the balance can be folded with this code rather
        /// than a second copy of it.
        /// </para>
        /// <para>
        /// ⚠️ CALL IT AFTER the local debit, never before — the ordering rule is the one written out
        /// above <see cref="SpendRoutine"/>'s try/finally, and it applies identically here: fold the
        /// post-debit server total in first and the local debit subtracts the same amount again, so the
        /// counter sits one spend too low until the next refresh.
        /// </para>
        /// <para>
        /// Only pass a payload that actually CARRIES balances (an <c>ok</c> or an <c>insufficient</c>).
        /// A refusal that never reached <c>spend_pts</c> has zeros in those fields, and folding those in
        /// would wipe the displayed RP.
        /// </para>
        /// </summary>
        public void ApplySpendResult(PointsSpendResult spend) => ApplySpend(spend);

        /// <summary>
        /// The EARN-side twin of <see cref="ApplySpendResult"/> (gacha_client_real_pull §4.1).
        ///
        /// <para>
        /// A gacha pull spends TICKETS, so its payload is not a <see cref="PointsSpendResult"/>
        /// superset the way a shop purchase's is — but a duplicate prize pays RP, and that credit
        /// arrives as both buckets plus the total. This folds those three numbers through the SAME
        /// <see cref="ApplyBalance"/> every other path uses; it is an entry point, NOT a second
        /// balance path, and that distinction is the whole reason it exists rather than a
        /// gacha-shaped copy of ApplySpend.
        /// </para>
        /// <para>
        /// Avatar level/XP are carried forward: an RP credit from a duplicate never touches them.
        /// </para>
        /// <para>
        /// ⚠️ Only call it with numbers from a payload that CARRIES balances. A pull that granted no
        /// duplicate has no <c>rp</c> block at all, and folding its zeros in would wipe the
        /// displayed RP — the caller checks for the block, not for a non-zero total.
        /// </para>
        /// </summary>
        public void ApplyEarnedBalance(int activityPts, int giftPts, int totalPoints)
            => ApplyBalance(new PointsBalance
            {
                ActivityPts = activityPts,
                GiftPts     = giftPts,
                TotalPoints = totalPoints,
                AvatarLevel = LastBalance != null ? LastBalance.AvatarLevel : 0,
                AvatarXp    = LastBalance != null ? LastBalance.AvatarXp : 0
            });

        /// <summary>Fold a spend response into the cached balance. Unlike an earn, the spend payload
        /// carries BOTH buckets, so the full balance can be rebuilt. Avatar level/XP are deliberately
        /// carried forward — <c>spend_pts</c> never touches them.</summary>
        private void ApplySpend(PointsSpendResult spend)
        {
            if (spend == null) return;

            ApplyBalance(new PointsBalance
            {
                ActivityPts = spend.ActivityPts,
                GiftPts     = spend.GiftPts,
                TotalPoints = spend.TotalPoints,
                AvatarLevel = LastBalance != null ? LastBalance.AvatarLevel : 0,
                AvatarXp    = LastBalance != null ? LastBalance.AvatarXp : 0
            });
        }

        private void ApplyBalance(PointsBalance balance)
        {
            if (balance == null) return;

            int before = Balance;
            bool hadBalance = HasBalance;
            LastBalance = balance;

            if (!hadBalance || before != balance.TotalPoints)
                OnBalanceChanged?.Invoke(balance.TotalPoints);

            // Forced on the first answer of the session: see RaiseDisplayBalanceIfChanged.
            RaiseDisplayBalanceIfChanged(force: !hadBalance);
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
