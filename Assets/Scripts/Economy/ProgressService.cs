// Order: progress_server_side §4 — the server-priced level-up call.
using System;
using System.Collections;
using Golfin.Net;
using UnityEngine;

namespace Golfin.Economy
{
    /// <summary>
    /// <c>POST /api/v1/progress/level-up</c> — the one call that levels a character or a club.
    ///
    /// <para>
    /// THE CLIENT NO LONGER DECIDES A COST, OR A LEVEL. Before this, a level-up was
    /// <c>PointsSpendGate.Spend(totalRPCost, SpendReasons.CharacterLevelUp, …)</c> followed by the
    /// client writing the new level into its own save: the server saw "N RP left this balance" and
    /// never WHAT was levelled — while paying out real RP for tournament play against the stats those
    /// levels produce. Here the request carries WHICH ref and WHICH levels, and nothing else that
    /// matters: the server sums the cost from the published <c>level_up_costs</c> catalog, debits
    /// through <c>spend_pts</c> and RECORDS the new level, in one transaction.
    /// <paramref name="expectedCost"/> is a GUARD, not a price — if it disagrees with the published
    /// sum the call is refused with <see cref="ProgressLevelUpVerdict.CostChanged"/> and nothing is
    /// written.
    /// </para>
    /// <para>
    /// Structurally a mirror of <see cref="ShopPurchaseService"/>, deliberately down to the details:
    /// a plain C# singleton over <see cref="ApiClient"/> (so EditMode tests construct it directly), a
    /// coroutine with an <c>Async</c> wrapper, the <see cref="PointsBackendFlag"/> gate INSIDE the
    /// routine rather than in the wrapper (so neither entry point can reach the network with the flag
    /// off), its OWN in-flight latch, a fresh idempotency key per attempt, and the balance fold in a
    /// <c>finally</c> AFTER <c>onDone</c>.
    /// </para>
    /// </summary>
    public sealed class ProgressService
    {
        private static ProgressService _instance;

        /// <summary>Lazily built against the shipping <see cref="ApiClient"/>. Nothing constructs this
        /// while the flag is OFF, because nothing calls it.</summary>
        public static ProgressService Instance
            => _instance ?? (_instance = new ProgressService(ApiClient.Instance));

        private readonly ApiClient _client;

        public ProgressService(ApiClient client) => _client = client;

        public static void ConfigureForTest(ProgressService service) => _instance = service;

        public static void ResetForTest() => _instance = null;

        /// <summary>The two things that have levels. Passed straight through to the server, which
        /// checks it again — this is here so a typo at a call site is a compile-time constant rather
        /// than a round trip.</summary>
        public const string KindCharacter = "character";

        /// <inheritdoc cref="KindCharacter"/>
        public const string KindClub = "club";

        /// <summary>
        /// One level-up at a time, process-wide.
        ///
        /// <para>
        /// Same semantics and same reason as <see cref="ShopPurchaseService.InFlight"/>'s latch, and a
        /// SEPARATE one on purpose: a level-up no longer goes through
        /// <c>Golfin.EconomyRuntime.PointsSpendGate</c>, so sharing its latch would couple two flows
        /// that no longer call each other. A double-tapped CONFIRM would otherwise fire two requests
        /// with two different idempotency keys, and the server would honour both — the replay guard
        /// only collapses the SAME key.
        /// </para>
        /// </summary>
        private bool _inFlight;

        /// <summary>True while a level-up is awaiting the server. Exposed for the tests and for UI
        /// that wants to disable CONFIRM rather than rely on the silent no-op.</summary>
        public bool InFlight => _inFlight;

        /// <summary>
        /// Fire-and-forget level-up. <paramref name="onDone"/> is invoked exactly once.
        /// </summary>
        /// <param name="kind"><see cref="KindCharacter"/> or <see cref="KindClub"/>.</param>
        /// <param name="refId">The <c>characters</c> / <c>clubs</c> row id.</param>
        /// <param name="fromLevel">The level this client believes the ref is at. The server compares
        /// it against its own record — or, the FIRST time, seeds the record from it (the
        /// grandfathering decision of record).</param>
        /// <param name="toLevel">The previewed target level. Must be above
        /// <paramref name="fromLevel"/>.</param>
        /// <param name="expectedCost">The RP total the modal showed. Pass 0 or less to skip the guard
        /// — which means accepting whatever the server charges, and should be rare.</param>
        /// <param name="build">The running build number (<c>ContentBuildNumber.Current</c>); the
        /// server refuses a ref whose <c>min_build</c> exceeds it.</param>
        public void LevelUpAsync(string kind, string refId, int fromLevel, int toLevel,
                                 int expectedCost, int build,
                                 Action<ProgressLevelUpOutcome> onDone)
            => _client.Run(LevelUpRoutine(kind, refId, fromLevel, toLevel, expectedCost, build, onDone));

        /// <summary>Coroutine form of <see cref="LevelUpAsync"/>. The flag gate lives HERE so neither
        /// entry point can reach the network with the flag off.</summary>
        public IEnumerator LevelUpRoutine(string kind, string refId, int fromLevel, int toLevel,
                                          int expectedCost, int build,
                                          Action<ProgressLevelUpOutcome> onDone)
        {
            if (!PointsBackendFlag.Enabled)
            {
                // Flag OFF is not a refusal — it means "the server is not in this build's loop", and
                // the modals never call this at all on that branch. Answering Disabled rather than
                // silently succeeding is what keeps a mistaken call site from levelling for free.
                onDone?.Invoke(ProgressLevelUpOutcome.Disabled());
                yield break;
            }

            if (string.IsNullOrEmpty(refId))
            {
                Debug.LogError("[ProgressService] LevelUpAsync called with no refId — refusing.");
                onDone?.Invoke(ProgressLevelUpOutcome.Unavailable(null));
                yield break;
            }

            if (toLevel <= fromLevel || fromLevel < 0)
            {
                Debug.LogError($"[ProgressService] LevelUpAsync called with a bad range for '{refId}': " +
                               $"{fromLevel} → {toLevel}. Refusing rather than spending a round trip.");
                onDone?.Invoke(ProgressLevelUpOutcome.Unavailable(null));
                yield break;
            }

            if (_inFlight)
            {
                Debug.LogWarning($"[ProgressService] Level-up of '{refId}' ignored — another " +
                                 "level-up is still awaiting the server.");
                onDone?.Invoke(ProgressLevelUpOutcome.Unavailable(null));
                yield break;
            }

            _inFlight = true;
            try
            {
                // A FRESH key per attempt, exactly as SpendRoutine and PurchaseRoutine do. A retry
                // after Unavailable is a NEW attempt: the previous one may or may not have landed, and
                // the server's replay guard is what covers the case where it did — reusing the key
                // here would instead make a genuine second level-up impossible.
                string body = BuildLevelUpJson(kind, refId, fromLevel, toLevel, expectedCost, build,
                                               Guid.NewGuid().ToString("D"));

                ApiResult<ProgressLevelUpResult> result = null;
                IEnumerator call = _client.Post<ProgressLevelUpResult>(
                    Endpoints.ProgressLevelUp, body, r => result = r);
                while (call.MoveNext()) yield return call.Current;

                if (result == null || !result.Success || result.Data == null)
                {
                    Debug.LogWarning($"[ProgressService] Level-up of '{refId}' failed: " +
                                     $"{(result != null ? result.ToString() : "no result")}");
                    onDone?.Invoke(ProgressLevelUpOutcome.Unavailable(result));
                    yield break;
                }

                ProgressLevelUpResult data = result.Data;

                if (data.IsOk)
                {
                    Debug.Log($"[ProgressService] Levelled '{refId}' → {data}");

                    if (data.BlobLevel.HasValue)
                    {
                        // The server took the claim on trust and said so. Worth a line in the client
                        // log too: this is the one place a save/blob divergence is visible from the
                        // device, and it is a fact about THIS install.
                        Debug.LogWarning($"[ProgressService] Grandfathered seed for '{refId}' " +
                                         $"disagreed with the server's copy of the inventory blob " +
                                         $"(claimed Lv {fromLevel}, blob said Lv {data.BlobLevel.Value}). " +
                                         "The level-up went through; the server logged it too.");
                    }

                    // ORDER MATTERS, and it is the same ordering rule as SpendRoutine's and
                    // PurchaseRoutine's — read the comment block above SpendRoutine's try/finally.
                    // onDone is what runs the LOCAL commit, and the per-level LevelUp() calls inside
                    // it each debit local RP. Folding the post-debit server total into the cache
                    // BEFORE that would push the already-debited number into the display and the
                    // local debits would subtract it a second time, leaving the counter one whole
                    // level-up too low until the next refresh. finally: a throwing call site must not
                    // leave the cached balance stale.
                    try
                    {
                        onDone?.Invoke(ProgressLevelUpOutcome.Ok(data, result));
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
                    Debug.Log($"[ProgressService] Level-up of '{refId}' refused: {data}");
                    PointsService.Instance.ApplySpendResult(data.ToSpendResult());
                    onDone?.Invoke(ProgressLevelUpOutcome.Insufficient(data, result));
                    yield break;
                }

                // Everything below carries NO balances (the server never got as far as spend_pts), so
                // none of them may touch the cache — folding their zeros in would wipe the displayed RP.

                if (data.IsCostChanged)
                {
                    Debug.Log($"[ProgressService] Level-up of '{refId}' refused: {data}. " +
                              "Nothing was written; the modal must re-price and ask again.");
                    onDone?.Invoke(ProgressLevelUpOutcome.CostChanged(data, result));
                    yield break;
                }

                if (data.IsLevelConflict)
                {
                    Debug.LogWarning($"[ProgressService] Level-up of '{refId}' refused: {data}. " +
                                     "This client's level is not the server's; nothing was debited.");
                    onDone?.Invoke(ProgressLevelUpOutcome.LevelConflict(data, result));
                    yield break;
                }

                // not_available / costs_missing / invalid_range / anything a later server adds. All
                // of them are content or client bugs rather than player-facing outcomes — loud in the
                // log, one toast in the UI. costs_missing in particular means an operator published a
                // gap into level_up_costs, which the admin validator is supposed to have refused.
                Debug.LogWarning($"[ProgressService] Level-up of '{refId}' returned '{data.Status}' " +
                                 $"({data}) — nothing levelled. If this is costs_missing, a level has " +
                                 "no published cost row and no player can buy it.");
                onDone?.Invoke(ProgressLevelUpOutcome.NotAvailable(data, result));
            }
            finally
            {
                _inFlight = false;
            }
        }

        /// <summary>
        /// Request body for <c>POST /api/v1/progress/level-up</c>. Field names match the deployed
        /// <c>LevelUpRequest</c> pydantic model: <c>{kind, ref_id, from_level, to_level,
        /// idempotency_key, build, expected_cost}</c>.
        ///
        /// <para>
        /// A non-positive <paramref name="expectedCost"/> is sent as <c>null</c>, not as 0: null means
        /// "do not guard", 0 would mean "I expect this to be free" and would refuse every real
        /// level-up with <c>cost_changed</c>.
        /// </para>
        /// Public so the tests can pin the wire shape without a live transport, mirroring
        /// <see cref="ShopPurchaseService.BuildPurchaseJson"/>.
        /// </summary>
        public static string BuildLevelUpJson(string kind, string refId, int fromLevel, int toLevel,
                                              int expectedCost, int build, string idempotencyKey)
            => Newtonsoft.Json.JsonConvert.SerializeObject(new LevelUpBody
            {
                kind            = kind,
                ref_id          = refId,
                from_level      = Mathf.Max(0, fromLevel),
                to_level        = toLevel,
                idempotency_key = idempotencyKey,
                build           = Mathf.Max(0, build),
                expected_cost   = expectedCost > 0 ? (int?)expectedCost : null
            });

        // Mirrors backend/routers/progress.py::LevelUpRequest — snake_case on purpose.
        private sealed class LevelUpBody
        {
            public string kind;
            public string ref_id;
            public int from_level;
            public int to_level;
            public string idempotency_key;
            public int build;
            public int? expected_cost;
        }
    }
}
