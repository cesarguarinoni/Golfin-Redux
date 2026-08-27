// Order: shop_server_purchase §3.1 — the shop's server-priced purchase call.
using System;
using System.Collections;
using Golfin.Net;
using UnityEngine;

namespace Golfin.Economy
{
    /// <summary>
    /// <c>POST /api/v1/shop/purchase</c> — the one call that buys a shop listing.
    ///
    /// <para>
    /// THE CLIENT NO LONGER DECIDES A PRICE. Before this, a purchase was
    /// <c>PointsSpendGate.Spend(entry.EffectiveRpCost, …)</c> followed by the client granting itself
    /// the item; the server saw only "N RP left this balance" and never WHAT was bought. Here the
    /// request carries WHICH LISTING was tapped and nothing else that matters: the server reads the
    /// published <c>shop_catalog</c> row, prices it off its OWN clock, debits and queues the grant in
    /// one transaction. <paramref name="expectedRpCost"/> is a GUARD, not a price — if it disagrees
    /// with the published one, the call is refused with <see cref="ShopPurchaseVerdict.PriceChanged"/>
    /// and nothing is written.
    /// </para>
    /// <para>
    /// Structurally a mirror of <see cref="PointsService"/>: a plain C# singleton over
    /// <see cref="ApiClient"/> (so EditMode tests construct it directly), a coroutine with an
    /// <c>Async</c> wrapper, and — critically — the <see cref="PointsBackendFlag"/> gate INSIDE the
    /// routine rather than in the wrapper, so neither entry point can reach the network with the flag
    /// off.
    /// </para>
    /// </summary>
    public sealed class ShopPurchaseService
    {
        private static ShopPurchaseService _instance;

        /// <summary>Lazily built against the shipping <see cref="ApiClient"/>. Nothing constructs this
        /// while the flag is OFF, because nothing calls it.</summary>
        public static ShopPurchaseService Instance
            => _instance ?? (_instance = new ShopPurchaseService(ApiClient.Instance));

        private readonly ApiClient _client;

        public ShopPurchaseService(ApiClient client) => _client = client;

        public static void ConfigureForTest(ShopPurchaseService service) => _instance = service;

        public static void ResetForTest() => _instance = null;

        /// <summary>
        /// One purchase at a time, process-wide.
        ///
        /// <para>
        /// Same semantics and same reason as <see cref="Golfin.EconomyRuntime.PointsSpendGate"/>'s
        /// latch, and a SEPARATE one on purpose: a purchase no longer goes through that gate, so
        /// sharing its latch would couple two flows that no longer call each other. A double-tapped
        /// BUY would otherwise fire two requests with two different idempotency keys, and the server
        /// would honour both — the replay guard only collapses the SAME key.
        /// </para>
        /// </summary>
        private bool _inFlight;

        /// <summary>True while a purchase is awaiting the server. Exposed for the tests and for UI
        /// that wants to disable BUY rather than rely on the silent no-op.</summary>
        public bool InFlight => _inFlight;

        /// <summary>
        /// Fire-and-forget purchase. <paramref name="onDone"/> is invoked exactly once.
        /// </summary>
        /// <param name="entryId">The <c>shop_catalog</c> row id (<c>ShopCatalogEntry.EntryId</c>).</param>
        /// <param name="expectedRpCost">The price the card showed. Pass 0 or less to skip the guard —
        /// which means accepting whatever the server charges, and should be rare.</param>
        /// <param name="build">The running build number (<c>ContentBuildNumber.Current</c>); the server
        /// withholds a listing whose <c>min_build</c> exceeds it.</param>
        public void PurchaseAsync(string entryId, int expectedRpCost, int build,
                                  Action<ShopPurchaseOutcome> onDone)
            => _client.Run(PurchaseRoutine(entryId, expectedRpCost, build, onDone));

        /// <summary>Coroutine form of <see cref="PurchaseAsync"/>. The flag gate lives HERE so neither
        /// entry point can reach the network with the flag off.</summary>
        public IEnumerator PurchaseRoutine(string entryId, int expectedRpCost, int build,
                                           Action<ShopPurchaseOutcome> onDone)
        {
            if (!PointsBackendFlag.Enabled)
            {
                // Flag OFF is not a refusal — it means "the server is not in this build's loop", and
                // ShopTransaction never calls this at all on that branch. Answering Disabled rather
                // than silently succeeding is what keeps a mistaken call site from granting for free.
                onDone?.Invoke(ShopPurchaseOutcome.Disabled());
                yield break;
            }

            if (string.IsNullOrEmpty(entryId))
            {
                Debug.LogError("[ShopPurchaseService] PurchaseAsync called with no entryId — refusing.");
                onDone?.Invoke(ShopPurchaseOutcome.Unavailable(null));
                yield break;
            }

            if (_inFlight)
            {
                Debug.LogWarning($"[ShopPurchaseService] Purchase of '{entryId}' ignored — another " +
                                 "purchase is still awaiting the server.");
                onDone?.Invoke(ShopPurchaseOutcome.Unavailable(null));
                yield break;
            }

            _inFlight = true;
            try
            {
                // A FRESH key per attempt, exactly as SpendRoutine does. A retry after Unavailable is a
                // NEW attempt: the previous one may or may not have landed, and the server's replay
                // guard is what covers the case where it did — reusing the key here would instead make
                // a genuine second purchase impossible.
                string body = BuildPurchaseJson(entryId, expectedRpCost, build, Guid.NewGuid().ToString("D"));

                ApiResult<ShopPurchaseResult> result = null;
                IEnumerator call = _client.Post<ShopPurchaseResult>(Endpoints.ShopPurchase, body, r => result = r);
                while (call.MoveNext()) yield return call.Current;

                if (result == null || !result.Success || result.Data == null)
                {
                    Debug.LogWarning($"[ShopPurchaseService] Purchase of '{entryId}' failed: " +
                                     $"{(result != null ? result.ToString() : "no result")}");
                    onDone?.Invoke(ShopPurchaseOutcome.Unavailable(result));
                    yield break;
                }

                ShopPurchaseResult data = result.Data;

                if (data.IsOk)
                {
                    Debug.Log($"[ShopPurchaseService] Purchased '{entryId}' → {data}");

                    // ORDER MATTERS, and it is the same ordering rule as SpendRoutine's — read the
                    // comment block above its try/finally. onDone is what runs the LOCAL debit
                    // (RewardPointsManager.SpendPoints, with the SERVER's charged amount). Folding the
                    // post-debit server total into the cache BEFORE that would push the already-debited
                    // number into the display and the local debit would subtract it a second time, so
                    // the counter would sit one purchase too low until the next refresh. finally: a
                    // throwing call site must not leave the cached balance stale.
                    try
                    {
                        onDone?.Invoke(ShopPurchaseOutcome.Ok(data, result));
                    }
                    finally
                    {
                        PointsService.Instance.ApplySpendResult(data.ToSpendResult());
                    }
                    yield break;
                }

                if (data.IsInsufficient)
                {
                    // 200 with status:"insufficient" — a definitive answer, and nothing was written.
                    // The refusal still carries the true balances, so the cache learns from it exactly
                    // as it does from a refused /points/spend.
                    Debug.Log($"[ShopPurchaseService] Purchase of '{entryId}' refused: {data}");
                    PointsService.Instance.ApplySpendResult(data.ToSpendResult());
                    onDone?.Invoke(ShopPurchaseOutcome.Insufficient(data, result));
                    yield break;
                }

                // Everything below carries NO balances (the server never got as far as spend_pts), so
                // none of them may touch the cache — folding their zeros in would wipe the displayed RP.

                if (data.IsPriceChanged)
                {
                    Debug.Log($"[ShopPurchaseService] Purchase of '{entryId}' refused: {data}. " +
                              "Nothing was written; the card must re-render at the published price.");
                    onDone?.Invoke(ShopPurchaseOutcome.PriceChanged(data, result));
                    yield break;
                }

                if (data.IsNotListed)
                {
                    Debug.Log($"[ShopPurchaseService] Purchase of '{entryId}' refused: {data}.");
                    onDone?.Invoke(ShopPurchaseOutcome.NotListed(data, result));
                    yield break;
                }

                if (data.IsAlreadyOwned)
                {
                    Debug.Log($"[ShopPurchaseService] Purchase of '{entryId}' refused: {data}.");
                    onDone?.Invoke(ShopPurchaseOutcome.AlreadyOwned(data, result));
                    yield break;
                }

                // unknown_entry / unsupported_category / anything a later server adds. These are
                // catalog bugs, not player-facing outcomes — loud in the log, silent-ish in the UI.
                Debug.LogWarning($"[ShopPurchaseService] Purchase of '{entryId}' returned " +
                                 $"'{data.Status}' — nothing granted. This is a catalog problem: a " +
                                 "listing the client can show but the server will not sell.");
                onDone?.Invoke(ShopPurchaseOutcome.Unknown(data, result));
            }
            finally
            {
                _inFlight = false;
            }
        }

        /// <summary>
        /// Request body for <c>POST /api/v1/shop/purchase</c>. Field names match the deployed
        /// <c>PurchaseRequest</c> pydantic model: <c>{entry_id, idempotency_key, build,
        /// expected_rp_cost}</c>.
        ///
        /// <para>
        /// A non-positive <paramref name="expectedRpCost"/> is sent as <c>null</c>, not as 0: null means
        /// "do not guard", 0 would mean "I expect this to be free" and would refuse every real listing
        /// with <c>price_changed</c>.
        /// </para>
        /// Public so the tests can pin the wire shape without a live transport, mirroring
        /// <see cref="PointsService.BuildSpendJson"/>.
        /// </summary>
        public static string BuildPurchaseJson(string entryId, int expectedRpCost, int build,
                                               string idempotencyKey)
            => Newtonsoft.Json.JsonConvert.SerializeObject(new PurchaseBody
            {
                entry_id         = entryId,
                idempotency_key  = idempotencyKey,
                build            = Mathf.Max(0, build),
                expected_rp_cost = expectedRpCost > 0 ? (int?)expectedRpCost : null
            });

        // Mirrors backend/routers/shop.py::PurchaseRequest — snake_case on purpose.
        private sealed class PurchaseBody
        {
            public string entry_id;
            public string idempotency_key;
            public int build;
            public int? expected_rp_cost;
        }
    }
}
