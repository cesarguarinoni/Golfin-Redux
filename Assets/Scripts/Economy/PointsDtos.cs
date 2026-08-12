// Order: reward_points_backend Slice 1 — payload shapes, transcribed from the LIVE API (not guessed).
using Newtonsoft.Json;

namespace Golfin.Economy
{
    /// <summary>
    /// <c>GET /api/v1/points/balance</c> → <c>{data:{activity_pts, gift_pts, total_points,
    /// avatar_level, avatar_xp}}</c>.
    ///
    /// Field set transcribed from the deployed <c>routers/points.py::get_balance</c> and confirmed live
    /// on 2026-08-12 (unauthenticated → 403, so the shape came from the deployed source, not a guess).
    /// </summary>
    public sealed class PointsBalance
    {
        [JsonProperty("activity_pts")] public int ActivityPts;
        [JsonProperty("gift_pts")]     public int GiftPts;
        [JsonProperty("total_points")] public int TotalPoints;
        [JsonProperty("avatar_level")] public int AvatarLevel;
        [JsonProperty("avatar_xp")]    public int AvatarXp;

        /// <summary>
        /// The game's Reward Points. SPEC decision of record #4: GOLFIN RP **is** the PLAYLIFE
        /// <c>total_points</c> (= activity_pts + gift_pts). There is no separate game currency.
        /// </summary>
        [JsonIgnore] public int RewardPoints => TotalPoints;

        public override string ToString()
            => $"RP={TotalPoints} (activity={ActivityPts}, gift={GiftPts}, avatar L{AvatarLevel}/{AvatarXp}xp)";
    }

    /// <summary>
    /// <c>POST /api/v1/points/earn-game</c> → <c>{data: &lt;earn_pts_v2 result&gt;}</c>, i.e.
    /// <c>{awarded, action, activity_pts, total_points, avatar_level, avatar_xp, leveled_up, replayed}</c>.
    ///
    /// The router also short-circuits with <c>{awarded:0, reason:"Unknown game action"}</c> or
    /// <c>{awarded:0, reason:"Daily cap reached", daily_cap:N}</c> — both are HTTP 200, so
    /// <see cref="Reason"/> being non-null is how a refusal is distinguished from a credit.
    ///
    /// Note there is no <c>gift_pts</c> on the earn payload (earns only ever touch activity_pts), which
    /// is why <see cref="PointsService"/> does not synthesise a full <see cref="PointsBalance"/> from it.
    /// </summary>
    public sealed class PointsEarnResult
    {
        [JsonProperty("awarded")]      public int Awarded;
        [JsonProperty("action")]       public string Action;
        [JsonProperty("activity_pts")] public int ActivityPts;
        [JsonProperty("total_points")] public int TotalPoints;
        [JsonProperty("avatar_level")] public int AvatarLevel;
        [JsonProperty("avatar_xp")]    public int AvatarXp;
        [JsonProperty("leveled_up")]   public bool LeveledUp;
        [JsonProperty("replayed")]     public bool Replayed;

        /// <summary>Set only on a router-side refusal (unknown action / daily cap). Null on a credit.</summary>
        [JsonProperty("reason")]       public string Reason;
        [JsonProperty("daily_cap")]    public int? DailyCap;

        /// <summary>A refusal is a definitive server answer, not a transport failure — the queued op is
        /// consumed rather than retried forever.</summary>
        [JsonIgnore] public bool WasRefused => !string.IsNullOrEmpty(Reason);
    }

    /// <summary>
    /// <c>POST /api/v1/points/spend</c> → <c>{data: &lt;spend_pts result&gt;}</c>.
    ///
    /// Field set transcribed from the applied migration
    /// (<c>2026_08_12_points_spend_idempotency.sql</c>, <c>public.spend_pts</c>), not guessed:
    ///   • debit  → <c>{status:"ok", spent, from_activity, from_gift, activity_pts, gift_pts,
    ///                 total_points, replayed:false}</c>
    ///   • replay → the same with <c>replayed:true</c> and the ORIGINAL split (nothing debited twice)
    ///   • short  → <c>{status:"insufficient", requested, shortfall, activity_pts, gift_pts,
    ///                 total_points, replayed:false}</c>
    ///
    /// Insufficient funds arrives as HTTP **200**, not an error status — the router says so explicitly
    /// ("Insufficient funds is NOT an HTTP error"). So <see cref="IsInsufficient"/>, never the status
    /// code, is what distinguishes "you cannot afford this" from "the server is unreachable". Nothing
    /// is written on an insufficient answer, so the same idempotency key can succeed later.
    /// </summary>
    public sealed class PointsSpendResult
    {
        [JsonProperty("status")]        public string Status;
        [JsonProperty("spent")]         public int Spent;
        [JsonProperty("from_activity")] public int FromActivity;
        [JsonProperty("from_gift")]     public int FromGift;
        [JsonProperty("requested")]     public int Requested;
        [JsonProperty("shortfall")]     public int Shortfall;
        [JsonProperty("activity_pts")]  public int ActivityPts;
        [JsonProperty("gift_pts")]      public int GiftPts;
        [JsonProperty("total_points")]  public int TotalPoints;
        [JsonProperty("replayed")]      public bool Replayed;

        [JsonIgnore] public bool IsOk => string.Equals(Status, "ok", System.StringComparison.Ordinal);

        [JsonIgnore] public bool IsInsufficient
            => string.Equals(Status, "insufficient", System.StringComparison.Ordinal);

        public override string ToString()
            => IsInsufficient
                ? $"insufficient (requested={Requested}, short {Shortfall}, have {TotalPoints})"
                : $"{Status} spent={Spent} (activity={FromActivity}, gift={FromGift}) " +
                  $"→ RP={TotalPoints}{(Replayed ? " (idempotent replay)" : "")}";
    }
}
