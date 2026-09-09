// Order: reward_points_backend Slice 1 — one queued, idempotency-keyed earn.
using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Golfin.Economy
{
    /// <summary>Kinds of queued operation. v1 queues EARNS ONLY — spends require a live connection
    /// (SPEC decision of record #2), so there is deliberately no Spend member to accidentally enqueue.</summary>
    public enum PendingOpKind
    {
        Earn = 0
    }

    /// <summary>
    /// A single points mutation waiting to reach the server.
    ///
    /// IDEMPOTENCY. <see cref="IdempotencyKey"/> is minted exactly once, at enqueue, and is then
    /// immutable for the life of the op — through disk round-trips, app restarts and every replay
    /// attempt. That is the whole contract: the server's partial unique index on
    /// <c>points_transactions(user_id, idempotency_key)</c> turns "replay after an ambiguous timeout"
    /// into a no-op that returns the ORIGINAL award instead of double-crediting. Regenerating the key on
    /// retry would silently defeat that, so the tests assert stability explicitly.
    /// </summary>
    public sealed class PendingPointsOp
    {
        [JsonProperty("key")]       public string IdempotencyKey;
        [JsonProperty("kind")]      public PendingOpKind Kind = PendingOpKind.Earn;
        [JsonProperty("action")]    public string Action;

        /// <summary>Client-proposed amount. Ignored by the server for catalog-fixed actions; validated
        /// against <c>max_per_event</c> / <c>daily_cap</c> for variable ones (e.g. tournament_prize).</summary>
        [JsonProperty("amount")]    public int Amount;

        [JsonProperty("createdAt")] public long CreatedAtUnix;

        /// <summary>Replay attempts so far. Diagnostics only — it never influences the key.</summary>
        [JsonProperty("attempts")]  public int AttemptCount;

        /// <summary>
        /// The loans whose assets the round that produced this earn was played with
        /// (asset_loans §4.5). Null when none, which is every earn in the game today.
        ///
        /// <para>
        /// NO QUEUE MIGRATION, AND THAT IS WHY IT IS NULLABLE RATHER THAN AN EMPTY LIST. Ops queued
        /// by an older build deserialise with this null and serialise back out without a
        /// <c>loan_ids</c> field at all — <see cref="ToEarnGameJson"/> omits it when empty — so a
        /// player who updates mid-queue replays their pending earns unchanged. An empty list would
        /// travel as <c>"loan_ids": []</c>, which is a different request for no reason.
        /// </para>
        /// <para>
        /// The ids are a CLAIM, not an authority: the server re-reads every one and keeps only
        /// live loans where this player is the borrower.
        /// </para>
        /// </summary>
        [JsonProperty("loans")]     public List<string> LoanIds;

        /// <summary>Parameterless ctor for Newtonsoft.</summary>
        public PendingPointsOp() { }

        /// <summary>Mint a new earn op with a fresh key and the current UTC timestamp.</summary>
        public static PendingPointsOp NewEarn(string action, int amount, long? nowUnix = null,
                                              List<string> loanIds = null) => new PendingPointsOp
        {
            IdempotencyKey = Guid.NewGuid().ToString("D"),
            Kind = PendingOpKind.Earn,
            Action = action,
            Amount = amount,
            CreatedAtUnix = nowUnix ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            AttemptCount = 0,
            // Copied, not aliased: the caller's list is LoanService's live snapshot, and a queued
            // op that kept a reference to it would silently change when the next round starts.
            LoanIds = (loanIds != null && loanIds.Count > 0) ? new List<string>(loanIds) : null
        };

        /// <summary>
        /// Request body for <c>POST /api/v1/points/earn-game</c>. Field names match the deployed
        /// <c>EarnGameRequest</c> pydantic model: <c>{action, amount?, idempotency_key}</c>.
        /// <c>amount</c> is omitted when non-positive so catalog-fixed actions take the server's value.
        /// </summary>
        public string ToEarnGameJson()
        {
            var body = new EarnGameBody
            {
                action = Action,
                amount = Amount > 0 ? (int?)Amount : null,
                idempotency_key = IdempotencyKey,
                // Omitted entirely when there is nothing to split — see LoanIds.
                loan_ids = (LoanIds != null && LoanIds.Count > 0) ? LoanIds : null
            };
            return JsonConvert.SerializeObject(body, new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore
            });
        }

        public override string ToString()
            => $"{Kind}:{Action} x{Amount} key={IdempotencyKey} attempts={AttemptCount}";

        // Mirrors backend/routers/points.py::EarnGameRequest — snake_case on purpose.
        private sealed class EarnGameBody
        {
            public string action;
            public int? amount;
            public string idempotency_key;
            public List<string> loan_ids;
        }
    }
}
