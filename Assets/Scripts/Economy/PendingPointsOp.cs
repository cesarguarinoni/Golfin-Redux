// Order: reward_points_backend Slice 1 — one queued, idempotency-keyed earn.
using System;
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

        /// <summary>Parameterless ctor for Newtonsoft.</summary>
        public PendingPointsOp() { }

        /// <summary>Mint a new earn op with a fresh key and the current UTC timestamp.</summary>
        public static PendingPointsOp NewEarn(string action, int amount, long? nowUnix = null) => new PendingPointsOp
        {
            IdempotencyKey = Guid.NewGuid().ToString("D"),
            Kind = PendingOpKind.Earn,
            Action = action,
            Amount = amount,
            CreatedAtUnix = nowUnix ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            AttemptCount = 0
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
                idempotency_key = IdempotencyKey
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
        }
    }
}
