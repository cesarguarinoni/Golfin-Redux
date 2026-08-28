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

    /// <summary>
    /// The grant a purchase queued, as it rides the <c>ok</c> payload of
    /// <c>POST /api/v1/shop/purchase</c>.
    ///
    /// <para>
    /// A DELIBERATE NEAR-DUPLICATE of <c>Golfin.InventorySync.InventoryGrant</c>, not an oversight:
    /// <c>Golfin.Economy.asmdef</c> references only <c>Golfin.Net</c>, and referencing
    /// <c>Golfin.InventorySync</c> from here would point the low-level economy assembly at the save
    /// layer to reuse four fields. The <c>JsonProperty</c> names are identical
    /// (<c>id, kind, ref_id, amount</c>) because it is the same server row — so if the wire shape
    /// ever changes, both break together and neither drifts silently.
    /// </para>
    /// </summary>
    public sealed class ShopGrantDto
    {
        [JsonProperty("id")]     public string Id = "";
        [JsonProperty("kind")]   public string Kind = "";
        [JsonProperty("ref_id")] public string RefId = "";
        [JsonProperty("amount")] public int    Amount = 1;
        [JsonProperty("note")]   public string Note;

        public override string ToString() => $"{Kind} '{RefId}' x{Amount} (grant {Id})";
    }

    /// <summary>
    /// <c>POST /api/v1/shop/purchase</c> → <c>{data: &lt;golfin_shop_purchase result&gt;}</c>.
    ///
    /// Field set transcribed from the migration
    /// (<c>2026_08_27_golfin_shop_purchase.sql</c>, <c>public.golfin_shop_purchase</c>) and the
    /// router's docstring, not guessed. Seven statuses, and EVERY ONE arrives as HTTP <b>200</b>:
    ///
    ///   • <c>ok</c>                   — debited + queued. Carries the whole
    ///                                   <see cref="PointsSpendResult"/> field set (so the balance
    ///                                   folds with the code that already exists) plus <see cref="Grant"/>.
    ///   • <c>insufficient</c>         — short. NOTHING was written; the same key can succeed later.
    ///   • <c>price_changed</c>        — the price the client showed is not the published one.
    ///                                   <see cref="Price"/> is the real one. Nothing written.
    ///   • <c>not_listed</c>           — with <see cref="Reason"/>: window | inactive | min_build |
    ///                                   disabled | unparseable_bound | invalid_price | ref_inactive.
    ///   • <c>already_owned</c>        — clubs and characters are unique.
    ///   • <c>unknown_entry</c>        — no such row in the published catalog.
    ///   • <c>unsupported_category</c> — publishable but not grantable (<c>bag</c>).
    ///
    /// So <see cref="Status"/>, never the status code, is what distinguishes a refusal from an
    /// unreachable server — the same rule <see cref="PointsSpendResult"/> already carries, for the
    /// same reason.
    /// </summary>
    public sealed class ShopPurchaseResult
    {
        [JsonProperty("status")]   public string Status;

        // ── the sale ──────────────────────────────────────────────────────────────
        [JsonProperty("entry_id")] public string EntryId;
        [JsonProperty("category")] public string Category;
        [JsonProperty("ref_id")]   public string RefId;

        /// <summary>What the player was ACTUALLY charged. The client debits this, never its own
        /// number — that is the whole point of the endpoint.</summary>
        [JsonProperty("charged")]  public int Charged;

        [JsonProperty("list_rp")]  public int ListRp;
        [JsonProperty("on_sale")]  public bool OnSale;

        /// <summary>Set on <c>price_changed</c> only: the published price the client should re-render
        /// the card at before asking again.</summary>
        [JsonProperty("price")]    public int Price;

        /// <summary>Set on <c>not_listed</c> only.</summary>
        [JsonProperty("reason")]   public string Reason;

        /// <summary>Set on <c>ok</c> only. Applied through the managers, never through
        /// <c>InventoryGrants.Apply</c> — see <c>ShopTransaction.ApplyPurchaseGrant</c>.</summary>
        [JsonProperty("grant")]    public ShopGrantDto Grant;

        // ── the spend, verbatim from spend_pts ────────────────────────────────────
        [JsonProperty("spent")]         public int Spent;
        [JsonProperty("from_activity")] public int FromActivity;
        [JsonProperty("from_gift")]     public int FromGift;
        [JsonProperty("requested")]     public int Requested;
        [JsonProperty("shortfall")]     public int Shortfall;
        [JsonProperty("activity_pts")]  public int ActivityPts;
        [JsonProperty("gift_pts")]      public int GiftPts;
        [JsonProperty("total_points")]  public int TotalPoints;
        [JsonProperty("replayed")]      public bool Replayed;

        [JsonIgnore] public bool IsOk => Is("ok");
        [JsonIgnore] public bool IsInsufficient => Is("insufficient");
        [JsonIgnore] public bool IsPriceChanged => Is("price_changed");
        [JsonIgnore] public bool IsNotListed => Is("not_listed");
        [JsonIgnore] public bool IsAlreadyOwned => Is("already_owned");
        [JsonIgnore] public bool IsUnknownEntry => Is("unknown_entry");
        [JsonIgnore] public bool IsUnsupportedCategory => Is("unsupported_category");

        private bool Is(string s) => string.Equals(Status, s, System.StringComparison.Ordinal);

        /// <summary>
        /// The spend half of this payload as a <see cref="PointsSpendResult"/>, so
        /// <c>PointsService.ApplySpendResult</c> can fold the balance with the code it already has.
        ///
        /// <para>
        /// <c>status</c> is carried across rather than forced to "ok": an <c>insufficient</c>
        /// purchase carries real balances too, and the cache should learn them exactly as it does
        /// from a refused <c>/points/spend</c>.
        /// </para>
        /// </summary>
        public PointsSpendResult ToSpendResult() => new PointsSpendResult
        {
            Status       = IsOk || IsInsufficient ? Status : "ok",
            Spent        = Spent,
            FromActivity = FromActivity,
            FromGift     = FromGift,
            Requested    = Requested,
            Shortfall    = Shortfall,
            ActivityPts  = ActivityPts,
            GiftPts      = GiftPts,
            TotalPoints  = TotalPoints,
            Replayed     = Replayed
        };

        public override string ToString()
        {
            if (IsOk)
                return $"ok charged={Charged} (list {ListRp}{(OnSale ? ", on sale" : "")}) " +
                       $"→ RP={TotalPoints}, grant {Grant}{(Replayed ? " (idempotent replay)" : "")}";
            if (IsInsufficient)
                return $"insufficient (requested={Requested}, short {Shortfall}, have {TotalPoints})";
            if (IsPriceChanged)
                return $"price_changed → {Price} (list {ListRp}{(OnSale ? ", on sale" : "")})";
            if (IsNotListed)
                return $"not_listed ({Reason})";
            return $"{Status}{(string.IsNullOrEmpty(RefId) ? "" : $" '{RefId}'")}";
        }
    }

    /// <summary>
    /// <c>POST /api/v1/progress/level-up</c> → <c>{data: &lt;golfin_level_up result&gt;}</c>.
    ///
    /// Field set transcribed from the migration
    /// (<c>2026_08_28_golfin_progress.sql</c>, <c>public.golfin_level_up</c>) and the router's
    /// docstring, not guessed. Seven statuses, and EVERY ONE arrives as HTTP <b>200</b>:
    ///
    ///   • <c>ok</c>             — debited and RECORDED. Carries the whole
    ///                             <see cref="PointsSpendResult"/> field set (so the balance folds
    ///                             with the code that already exists) plus the new
    ///                             <see cref="Level"/> and what it <see cref="Cost"/>.
    ///   • <c>insufficient</c>   — short. NOTHING was written; the same key can succeed later.
    ///   • <c>cost_changed</c>   — the sum the modal showed is not the published one.
    ///                             <see cref="Cost"/> is the real one. Nothing written, so the modal
    ///                             re-prices and the player answers again.
    ///   • <c>level_conflict</c> — the server's recorded level is not the claimed
    ///                             <see cref="FromLevel"/>. <see cref="ServerLevel"/> is the truth.
    ///   • <c>costs_missing</c>  — a level in the range has no published, active cost row;
    ///                             <see cref="Level"/> names the first one. A CONTENT bug.
    ///   • <c>invalid_range</c>  — <see cref="Reason"/>: order | step_cap | max_level.
    ///   • <c>not_available</c>  — <see cref="Reason"/>: disabled | ref | min_build |
    ///                             ref_max_level | invalid_cost.
    ///
    /// So <see cref="Status"/>, never the status code, is what distinguishes a refusal from an
    /// unreachable server — the same rule <see cref="ShopPurchaseResult"/> already carries, for the
    /// same reason.
    ///
    /// <para>
    /// <see cref="Grandfathered"/> is the visible half of the decision of record: the FIRST server
    /// level-up for a ref seeds the record at the level the client claimed, because no honest
    /// server-side history of it exists. <see cref="BlobLevel"/> rides along ONLY when the inventory
    /// blob disagreed with that claim — it is a diagnostic, never a refusal.
    /// </para>
    /// </summary>
    public sealed class ProgressLevelUpResult
    {
        [JsonProperty("status")]     public string Status;

        // ── the level-up ──────────────────────────────────────────────────────────
        [JsonProperty("kind")]       public string Kind;
        [JsonProperty("ref_id")]     public string RefId;

        /// <summary>The level the server now RECORDS for this ref. On <c>costs_missing</c> this is
        /// instead the first level with no published cost row — the two never co-occur.</summary>
        [JsonProperty("level")]      public int Level;

        [JsonProperty("from_level")] public int FromLevel;

        /// <summary>What the run ACTUALLY cost, summed from published content. On
        /// <c>cost_changed</c> this is the published sum the modal must re-price at.</summary>
        [JsonProperty("cost")]       public int Cost;

        /// <summary>True when this call SEEDED the record from the client's claim (the decision of
        /// record). False on every subsequent level-up of the same ref.</summary>
        [JsonProperty("grandfathered")] public bool Grandfathered;

        /// <summary>Present only when a grandfathered seed disagreed with the inventory blob. Logged,
        /// never acted on — a malformed or stale blob must not cost the player a level-up.</summary>
        [JsonProperty("blob_level")] public int? BlobLevel;

        /// <summary>Set on <c>level_conflict</c> only: the level the server actually holds.</summary>
        [JsonProperty("server_level")] public int ServerLevel;

        /// <summary>Set on <c>not_available</c> and <c>invalid_range</c> only.</summary>
        [JsonProperty("reason")]     public string Reason;

        // ── the spend, verbatim from spend_pts ────────────────────────────────────
        [JsonProperty("spent")]         public int Spent;
        [JsonProperty("from_activity")] public int FromActivity;
        [JsonProperty("from_gift")]     public int FromGift;
        [JsonProperty("requested")]     public int Requested;
        [JsonProperty("shortfall")]     public int Shortfall;
        [JsonProperty("activity_pts")]  public int ActivityPts;
        [JsonProperty("gift_pts")]      public int GiftPts;
        [JsonProperty("total_points")]  public int TotalPoints;
        [JsonProperty("replayed")]      public bool Replayed;

        [JsonIgnore] public bool IsOk => Is("ok");
        [JsonIgnore] public bool IsInsufficient => Is("insufficient");
        [JsonIgnore] public bool IsCostChanged => Is("cost_changed");
        [JsonIgnore] public bool IsLevelConflict => Is("level_conflict");
        [JsonIgnore] public bool IsCostsMissing => Is("costs_missing");
        [JsonIgnore] public bool IsInvalidRange => Is("invalid_range");
        [JsonIgnore] public bool IsNotAvailable => Is("not_available");

        /// <summary>True when the operator has pulled the cost table or all remote content — the one
        /// <c>not_available</c> reason that is a deliberate act rather than a data problem.</summary>
        [JsonIgnore] public bool IsDisabled
            => IsNotAvailable && string.Equals(Reason, "disabled", System.StringComparison.Ordinal);

        private bool Is(string s) => string.Equals(Status, s, System.StringComparison.Ordinal);

        /// <summary>
        /// The spend half of this payload as a <see cref="PointsSpendResult"/>, so
        /// <c>PointsService.ApplySpendResult</c> can fold the balance with the code it already has.
        /// Identical in shape and in reasoning to <see cref="ShopPurchaseResult.ToSpendResult"/>:
        /// <c>status</c> is carried across rather than forced to "ok", because an
        /// <c>insufficient</c> level-up carries real balances too.
        /// </summary>
        public PointsSpendResult ToSpendResult() => new PointsSpendResult
        {
            Status       = IsOk || IsInsufficient ? Status : "ok",
            Spent        = Spent,
            FromActivity = FromActivity,
            FromGift     = FromGift,
            Requested    = Requested,
            Shortfall    = Shortfall,
            ActivityPts  = ActivityPts,
            GiftPts      = GiftPts,
            TotalPoints  = TotalPoints,
            Replayed     = Replayed
        };

        public override string ToString()
        {
            if (IsOk)
                return $"ok {Kind} '{RefId}' → Lv {Level} for {Cost} RP" +
                       $"{(Grandfathered ? " (grandfathered seed)" : "")}" +
                       $"{(BlobLevel.HasValue ? $" [blob said Lv {BlobLevel.Value}]" : "")}" +
                       $" → RP={TotalPoints}{(Replayed ? " (idempotent replay)" : "")}";
            if (IsInsufficient)
                return $"insufficient (requested={Requested}, short {Shortfall}, have {TotalPoints})";
            if (IsCostChanged)
                return $"cost_changed → {Cost} RP";
            if (IsLevelConflict)
                return $"level_conflict (server is at Lv {ServerLevel}, client claimed Lv {FromLevel})";
            if (IsCostsMissing)
                return $"costs_missing (no published cost for Lv {Level})";
            return $"{Status}{(string.IsNullOrEmpty(Reason) ? "" : $" ({Reason})")}";
        }
    }
}
