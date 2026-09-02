// gps_gifts_votes §Client data bindings — the gift wire shapes, transcribed from the deployed
// router and verified against a live response, not guessed.
using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Golfin.Social
{
    /// <summary>
    /// One row of <c>gift_items</c> (GET <c>/gifts/items</c>).
    ///
    /// <para>
    /// <c>price_activity_pts</c> and <c>price_gift_pts</c> are NULLABLE and mutually exclusive in
    /// practice: every <c>basic</c> row in the live catalog prices in activity pts and leaves the
    /// gift price null, every <c>premium</c> row does the reverse (verified 2026-09-02 over all 21
    /// rows). So a null is "not sold for this currency", never "free" — branch on
    /// <c>HasValue</c>, never on <c>!= 0</c>.
    /// </para>
    /// </summary>
    public sealed class GiftItemDto
    {
        [JsonProperty("id")]                 public string Id;
        [JsonProperty("name")]               public string Name;
        [JsonProperty("description")]        public string Description;
        /// <summary>hat | tops | bottoms | shoes | gloves | accessory | fullset — the strip picks
        /// its icon off this.</summary>
        [JsonProperty("category")]           public string Category;
        /// <summary>basic | premium.</summary>
        [JsonProperty("tier")]               public string Tier;
        [JsonProperty("price_activity_pts")] public int? PriceActivityPts;
        [JsonProperty("price_gift_pts")]     public int? PriceGiftPts;
        [JsonProperty("rarity")]             public string Rarity;
        [JsonProperty("is_active")]          public bool IsActive;
    }

    /// <summary>
    /// One row of <c>gifts</c> as GET <c>/gifts/received</c> returns it — the row plus two
    /// PostgREST embeds. The sender embed is aliased in the router's select as
    /// <c>profiles!gifts_sender_id_fkey(...)</c>, so it arrives under the key <c>profiles</c>.
    /// </summary>
    public sealed class ReceivedGiftDto
    {
        [JsonProperty("id")]               public string Id;
        [JsonProperty("sender_id")]        public string SenderId;
        [JsonProperty("receiver_id")]      public string ReceiverId;
        [JsonProperty("item_id")]          public string ItemId;
        [JsonProperty("message")]          public string Message;
        [JsonProperty("payment_type")]     public string PaymentType;
        [JsonProperty("gift_pts_awarded")] public int? GiftPtsAwarded;
        [JsonProperty("status")]           public string Status;
        [JsonProperty("created_at")]       public string CreatedAt;

        /// <summary>The SENDER's profile (the embed alias). May be null on a row whose sender was
        /// deleted.</summary>
        [JsonProperty("profiles")]         public GiftPartyDto Sender;

        [JsonProperty("gift_items")]       public GiftItemStubDto Item;
    }

    /// <summary>The two columns the received-gift embed carries about the sender.</summary>
    public sealed class GiftPartyDto
    {
        [JsonProperty("display_name")] public string DisplayName;
        [JsonProperty("avatar_url")]   public string AvatarUrl;
    }

    /// <summary>The gift_items embed on a received row — a strict subset of
    /// <see cref="GiftItemDto"/>.</summary>
    public sealed class GiftItemStubDto
    {
        [JsonProperty("name")]      public string Name;
        [JsonProperty("image_url")] public string ImageUrl;
        [JsonProperty("rarity")]    public string Rarity;
    }

    /// <summary>
    /// POST <c>/gifts/send-pts</c> → <c>{data: …}</c>.
    /// <see cref="Replayed"/> is TRUE when the server recognised the idempotency key and moved
    /// nothing — the balances are still correct, so the caller repaints exactly as it would on a
    /// first send and simply does not toast twice.
    /// </summary>
    public sealed class GiftSendResultDto
    {
        [JsonProperty("amount")]                 public int Amount;
        [JsonProperty("receiver")]               public string ReceiverName;
        [JsonProperty("remaining_activity_pts")] public int? RemainingActivityPts;
        [JsonProperty("total_points")]           public int? TotalPoints;
        [JsonProperty("replayed")]               public bool Replayed;
        [JsonProperty("idempotency_key")]        public string IdempotencyKey;
    }

    /// <summary>POST <c>/gifts/purchase</c> → <c>{data: …}</c>. Same replay posture as
    /// <see cref="GiftSendResultDto"/>.</summary>
    public sealed class GiftPurchaseResultDto
    {
        [JsonProperty("message")]         public string Message;
        [JsonProperty("item")]            public string ItemName;
        [JsonProperty("item_id")]         public string ItemId;
        [JsonProperty("price")]           public int Price;
        [JsonProperty("currency")]        public string Currency;
        [JsonProperty("activity_pts")]    public int? ActivityPts;
        [JsonProperty("gift_pts")]        public int? GiftPts;
        [JsonProperty("total_points")]    public int? TotalPoints;
        [JsonProperty("inventory_id")]    public string InventoryId;
        [JsonProperty("replayed")]        public bool Replayed;
        [JsonProperty("idempotency_key")] public string IdempotencyKey;
    }

    /// <summary>
    /// One row of GET <c>/user/discover</c> — the explicit column list in
    /// <c>user.py::discover_users</c>, so this is the WHOLE shape, not a subset.
    /// </summary>
    public sealed class DiscoverUserDto
    {
        [JsonProperty("id")]              public string Id;
        [JsonProperty("display_name")]    public string DisplayName;
        [JsonProperty("avatar_url")]      public string AvatarUrl;
        [JsonProperty("followers_count")] public int? FollowersCount;
        [JsonProperty("activities_count")]public int? ActivitiesCount;
        [JsonProperty("avatar_level")]    public int? AvatarLevel;
        [JsonProperty("best_score")]      public int? BestScore;
    }

    /// <summary>
    /// One row of <c>points_transactions</c> as GET <c>/points/history</c> returns it
    /// (points.py <c>get_points_history</c> does a <c>select("*")</c>). Only the columns the
    /// supporters aggregation reads are mapped; the rest ride Newtonsoft's default
    /// <c>MissingMemberHandling.Ignore</c>.
    /// </summary>
    public sealed class PointsLedgerRowDto
    {
        [JsonProperty("id")]          public string Id;
        /// <summary>gift_received | gift_sent | purchase | spend | screenshot | …</summary>
        [JsonProperty("type")]        public string Type;
        [JsonProperty("amount")]      public int Amount;
        /// <summary>activity | gift.</summary>
        [JsonProperty("currency")]    public string Currency;
        /// <summary>Human-readable, and the ONLY place the counterparty is recorded — see
        /// <c>GiftService.SupporterName</c>.</summary>
        [JsonProperty("description")] public string Description;
        [JsonProperty("created_at")]  public string CreatedAt;
    }

    /// <summary>
    /// One aggregated supporter, produced CLIENT-SIDE. There is no server endpoint for this.
    ///
    /// <para>
    /// ⚠️ <see cref="SenderId"/> IS OFTEN NULL, and that is not a bug. Two different sources feed
    /// this (see <c>GiftService.Supporters</c>): item gifts come from <c>/gifts/received</c> and
    /// carry a real <c>sender_id</c>; RP gifts — the only kind this build can send — are recorded
    /// ONLY as <c>points_transactions</c> rows, which have no counterparty column, so the sender
    /// is known by DISPLAY NAME alone. The panel renders rank / initial / name / points and needs
    /// no id, which is why grouping by name is sufficient here and would not be for anything that
    /// had to navigate to a profile.
    /// </para>
    /// </summary>
    public sealed class SupporterTotal
    {
        public string SenderId;
        public string DisplayName;
        /// <summary>Points received from that sender, summed across both sources.</summary>
        public int Points;
        /// <summary>How many gift rows that sender accounts for.</summary>
        public int GiftCount;
    }
}
