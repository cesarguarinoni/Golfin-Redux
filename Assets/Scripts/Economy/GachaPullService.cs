// Order: gacha_client_real_pull §4.1 — the gacha's server-side pull call.
using System;
using System.Collections;
using Golfin.Net;
using UnityEngine;

namespace Golfin.Economy
{
    /// <summary>
    /// <c>POST /api/v1/gacha/pull</c> — the one call that rolls a banner.
    ///
    /// <para>
    /// THE CLIENT NO LONGER DECIDES THE PRIZE, AND NO LONGER DECIDES THE PRICE. Before this,
    /// <c>GachaPullFlow.BuildResult</c> handed back ten hard-coded club ids and nothing was
    /// debited: the server had never seen a pull happen. Here the request carries WHICH BANNER and
    /// HOW MANY and nothing else that matters — <c>golfin_gacha_pull()</c> reads the published
    /// banner, prices it on its own clock, debits the ticket ledger, rolls against the published
    /// rates × pool with pity and the x10 floor, and queues the grants in one transaction.
    /// <paramref name="expectedCost"/> is a GUARD, not a price.
    /// </para>
    /// <para>
    /// Structurally a mirror of <see cref="ShopPurchaseService"/>, deliberately line for line: a
    /// plain C# singleton over <see cref="ApiClient"/> (so EditMode tests construct it directly), a
    /// coroutine with an <c>Async</c> wrapper, an in-flight latch, and — critically — the
    /// <see cref="PointsBackendFlag"/> gate INSIDE the routine rather than in the wrapper, so
    /// neither entry point can reach the network with the flag off.
    /// </para>
    /// <para>
    /// ⚠️ IT DELIBERATELY APPLIES NOTHING. <see cref="ShopPurchaseService"/> folds the RP balance
    /// itself because the balance lives in this assembly; a pull's four consequences — the ticket
    /// counter, the RP a duplicate paid, the grants drain and the history row — reach across
    /// <c>Golfin.Economy</c>'s boundary into Assembly-CSharp, which this assembly must not
    /// reference (the same split as <c>IServerBalanceSink</c>). They are applied, in the SPEC's
    /// order, by <c>GachaPullFlow.ApplyOk</c>, which is the gacha's <c>ShopTransaction</c>.
    /// </para>
    /// </summary>
    public sealed class GachaPullService
    {
        private static GachaPullService _instance;

        /// <summary>Lazily built against the shipping <see cref="ApiClient"/>. Nothing constructs
        /// this while the flag is OFF, because nothing calls it.</summary>
        public static GachaPullService Instance
            => _instance ?? (_instance = new GachaPullService(ApiClient.Instance));

        private readonly ApiClient _client;

        public GachaPullService(ApiClient client) => _client = client;

        public static void ConfigureForTest(GachaPullService service) => _instance = service;

        public static void ResetForTest() => _instance = null;

        /// <summary>
        /// One pull at a time, process-wide.
        ///
        /// <para>
        /// Same reason as <see cref="ShopPurchaseService"/>'s latch, and a separate one for the
        /// same reason it is separate from the spend gate: a double-tapped PULL would otherwise
        /// fire two requests with two different idempotency keys, and the server would honour both
        /// — the replay guard only collapses the SAME key. The reveal modal covering the round trip
        /// (§4.2) makes a second tap unlikely, not impossible.
        /// </para>
        /// </summary>
        private bool _inFlight;

        /// <summary>True while a pull is awaiting the server.</summary>
        public bool InFlight => _inFlight;

        /// <summary>
        /// Fire-and-forget pull. <paramref name="onDone"/> is invoked exactly once.
        /// </summary>
        /// <param name="bannerId">The <c>gacha_banners</c> row id.</param>
        /// <param name="count">1 or 10 — the only two shapes the pull ledger accepts.</param>
        /// <param name="expectedCost">The price the card showed. Pass 0 or less to skip the guard,
        /// which means accepting whatever the server charges and should be rare.</param>
        /// <param name="build">The running build number (<c>ContentBuildNumber.Current</c>); the
        /// server withholds a banner, and a pool entry, whose <c>min_build</c> exceeds it.</param>
        public void PullAsync(string bannerId, int count, int expectedCost, int build,
                              Action<GachaPullOutcome> onDone)
            => _client.Run(PullRoutine(bannerId, count, expectedCost, build, onDone));

        /// <summary>Coroutine form of <see cref="PullAsync"/>. The flag gate lives HERE so neither
        /// entry point can reach the network with the flag off.</summary>
        public IEnumerator PullRoutine(string bannerId, int count, int expectedCost, int build,
                                       Action<GachaPullOutcome> onDone)
        {
            if (!PointsBackendFlag.Enabled)
            {
                // Unlike a spend, flag-OFF is NOT a "run your local path" answer: there is no local
                // path any more. Unavailable is the truthful verdict and the UI shows the offline
                // copy — a build with the flag off simply cannot pull.
                Debug.LogWarning("[GachaPullService] PointsBackendEnabled is OFF — a pull cannot be " +
                                 "rolled locally, so nothing was requested and nothing was granted.");
                onDone?.Invoke(GachaPullOutcome.Unavailable(null));
                yield break;
            }

            if (string.IsNullOrEmpty(bannerId))
            {
                Debug.LogError("[GachaPullService] PullAsync called with no bannerId — refusing.");
                onDone?.Invoke(GachaPullOutcome.Unavailable(null));
                yield break;
            }

            if (count != 1 && count != 10)
            {
                Debug.LogError($"[GachaPullService] PullAsync called with count={count}; only 1 and 10 " +
                               "are pullable. Refusing without a request.");
                onDone?.Invoke(GachaPullOutcome.Unknown(null, null));
                yield break;
            }

            if (_inFlight)
            {
                Debug.LogWarning($"[GachaPullService] Pull on '{bannerId}' ignored — another pull is " +
                                 "still awaiting the server.");
                onDone?.Invoke(GachaPullOutcome.Unavailable(null));
                yield break;
            }

            _inFlight = true;
            try
            {
                // A FRESH key per attempt, exactly as ShopPurchaseService does. A retry after
                // Unavailable is a NEW attempt: the previous one may or may not have landed, and
                // the server's replay guard is what covers the case where it did — reusing the key
                // here would instead make a genuine second pull impossible.
                string body = BuildPullJson(bannerId, count, expectedCost, build, Guid.NewGuid().ToString("D"));

                ApiResult<GachaPullResult> result = null;
                IEnumerator call = _client.Post<GachaPullResult>(Endpoints.GachaPull, body, r => result = r);
                while (call.MoveNext()) yield return call.Current;

                if (result == null || !result.Success || result.Data == null)
                {
                    Debug.LogWarning($"[GachaPullService] Pull on '{bannerId}' failed: " +
                                     $"{(result != null ? result.ToString() : "no result")}");
                    onDone?.Invoke(GachaPullOutcome.Unavailable(result));
                    yield break;
                }

                GachaPullResult data = result.Data;

                if (data.IsOk)
                {
                    Debug.Log($"[GachaPullService] Pulled '{bannerId}' → {data}");
                    onDone?.Invoke(GachaPullOutcome.Ok(data, result));
                    yield break;
                }

                // ⚠️ PAUSED IS TESTED BEFORE NotAvailable: it IS a not_available, with a reason that
                // means the whole feature is off rather than this banner being wrong. Reversing
                // these two would reload the catalog on every paused tap and withhold nothing.
                if (data.IsPaused)
                {
                    Debug.Log($"[GachaPullService] Pull on '{bannerId}' refused: {data}.");
                    onDone?.Invoke(GachaPullOutcome.Paused(data, result));
                    yield break;
                }

                if (data.IsInsufficient)
                {
                    Debug.Log($"[GachaPullService] Pull on '{bannerId}' refused: {data}.");
                    onDone?.Invoke(GachaPullOutcome.Insufficient(data, result));
                    yield break;
                }

                if (data.IsCostChanged)
                {
                    Debug.Log($"[GachaPullService] Pull on '{bannerId}' refused: {data}. Nothing was " +
                              "written; the card must re-render at the published cost.");
                    onDone?.Invoke(GachaPullOutcome.CostChanged(data, result));
                    yield break;
                }

                if (data.IsPullCap)
                {
                    Debug.Log($"[GachaPullService] Pull on '{bannerId}' refused: {data}.");
                    onDone?.Invoke(GachaPullOutcome.PullCap(data, result));
                    yield break;
                }

                if (data.IsNotAvailable || data.IsUnknownBanner)
                {
                    // The client is showing a banner the server will not roll. That is exactly what
                    // the §3.1 withhold rule exists to prevent, so it is a WARNING: either the
                    // client's copy of the catalog is stale (a refresh landed) or the two rules
                    // disagree, and both are worth seeing.
                    Debug.LogWarning($"[GachaPullService] Pull on '{bannerId}' refused: {data}. The " +
                                     "client is showing a banner the server will not roll — the " +
                                     "catalog is stale, or the withhold rule and step 8 disagree.");
                    onDone?.Invoke(GachaPullOutcome.NotAvailable(data, result));
                    yield break;
                }

                // invalid_count (a client bug the guard above should have caught) or a status a
                // later server adds. Loud in the log, and nothing proceeds.
                Debug.LogError($"[GachaPullService] Pull on '{bannerId}' returned '{data.Status}' — " +
                               "nothing was revealed. This build does not know that status.");
                onDone?.Invoke(GachaPullOutcome.Unknown(data, result));
            }
            finally
            {
                _inFlight = false;
            }
        }

        /// <summary>
        /// Request body for <c>POST /api/v1/gacha/pull</c>. Field names match the deployed
        /// <c>PullRequest</c> pydantic model: <c>{banner_id, count, idempotency_key, build,
        /// expected_cost}</c>.
        ///
        /// <para>
        /// A non-positive <paramref name="expectedCost"/> is sent as <c>null</c>, not as 0: null
        /// means "do not guard", 0 would mean "I expect this to be free" and would refuse every
        /// priced banner with <c>cost_changed</c>. Same rule, same reason, as
        /// <see cref="ShopPurchaseService.BuildPurchaseJson"/>.
        /// </para>
        /// Public so the tests can pin the wire shape without a live transport.
        /// </summary>
        public static string BuildPullJson(string bannerId, int count, int expectedCost, int build,
                                           string idempotencyKey)
            => Newtonsoft.Json.JsonConvert.SerializeObject(new PullBody
            {
                banner_id       = bannerId,
                count           = count,
                idempotency_key = idempotencyKey,
                build           = Mathf.Max(0, build),
                expected_cost   = expectedCost > 0 ? (int?)expectedCost : null
            });

        // Mirrors backend/routers/gacha.py::PullRequest — snake_case on purpose.
        private sealed class PullBody
        {
            public string banner_id;
            public int count;
            public string idempotency_key;
            public int build;
            public int? expected_cost;
        }

        // ── The two reads ─────────────────────────────────────────────────────

        /// <summary>
        /// <c>GET /api/v1/gacha/tickets</c> — the caller's ticket balances.
        ///
        /// <para>
        /// An ABSENT type is a real balance of ZERO, by the server's own decision to answer by
        /// omission. Callers must therefore treat "not in this list" as 0 rather than as unknown;
        /// the ledger genuinely starts empty for every player (plan §9).
        /// </para>
        /// </summary>
        public void FetchTicketsAsync(Action<GachaTicketBalances> onDone)
            => _client.Run(FetchTicketsRoutine(onDone));

        public IEnumerator FetchTicketsRoutine(Action<GachaTicketBalances> onDone)
        {
            if (!PointsBackendFlag.Enabled) { onDone?.Invoke(null); yield break; }

            ApiResult<GachaTicketBalances> result = null;
            IEnumerator call = _client.Get<GachaTicketBalances>(Endpoints.GachaTickets, r => result = r);
            while (call.MoveNext()) yield return call.Current;

            // Null on failure, never an empty page: an empty page means "you hold nothing", and
            // handing that to the counter on a timeout would zero a real balance on screen.
            onDone?.Invoke(result != null && result.Success ? result.Data : null);
        }

        /// <summary>
        /// <c>GET /api/v1/gacha/history</c> — the caller's own pulls, newest first.
        /// <paramref name="limit"/> is clamped server-side to 200.
        /// </summary>
        public void FetchHistoryAsync(int limit, Action<GachaHistoryPage> onDone)
            => _client.Run(FetchHistoryRoutine(limit, onDone));

        public IEnumerator FetchHistoryRoutine(int limit, Action<GachaHistoryPage> onDone)
        {
            if (!PointsBackendFlag.Enabled) { onDone?.Invoke(null); yield break; }

            string url = Endpoints.GachaHistory + "?limit=" + Mathf.Clamp(limit, 1, 200);

            ApiResult<GachaHistoryPage> result = null;
            IEnumerator call = _client.Get<GachaHistoryPage>(url, r => result = r);
            while (call.MoveNext()) yield return call.Current;

            // Same rule as the tickets read: null on failure so the caller keeps what it has.
            onDone?.Invoke(result != null && result.Success ? result.Data : null);
        }
    }
}
