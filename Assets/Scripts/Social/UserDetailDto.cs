// Order: gps_hub_entry §3 — the profiles row shape, transcribed from the deployed router, not guessed.
using Newtonsoft.Json;

namespace Golfin.Social
{
    /// <summary>
    /// <c>GET /api/v1/user/detail</c> → <c>{data: &lt;profiles row&gt;}</c> (user.py:78-86).
    ///
    /// <para>
    /// The router does <c>select("*")</c>, so this type maps a SUBSET of the columns and relies on
    /// Newtonsoft's default <c>MissingMemberHandling.Ignore</c> — do NOT switch it to
    /// <c>Error</c>, or the server growing a column becomes a client crash. Same posture, and the
    /// same reasoning, as <c>Golfin.Gps.VenueDto</c>.
    /// </para>
    /// <para>
    /// EVERYTHING EXCEPT <see cref="Id"/> IS NULLABLE. A profile created by the game (rather than by
    /// the PLAYLIFE app) has never posted a score, so <c>handicap</c>, <c>best_score</c>,
    /// <c>avg_score</c> and <c>trust_level</c> are genuinely absent — not zero. Rendering a null as
    /// <c>0</c> would tell a new player their best score is 0, which reads as a hole-in-one round;
    /// the hub renders <c>—</c> instead. Branch on <c>HasValue</c>, never on <c>!= 0</c>.
    /// </para>
    /// <para>
    /// Column list per <c>Docs/GPS/GPS_INTEGRATION_REFERENCE.md</c> §5 (<c>profiles</c>).
    /// </para>
    /// </summary>
    public sealed class UserDetailDto
    {
        [JsonProperty("id")]               public string Id;
        [JsonProperty("display_name")]     public string DisplayName;
        [JsonProperty("avatar_url")]       public string AvatarUrl;
        [JsonProperty("bio")]              public string Bio;

        // ── Golf record. `handicap` is written by score submits AND by the Golf Profile
        //    screen (auth_golf_profile); the rest by score submits only. ──
        [JsonProperty("handicap")]         public double? Handicap;
        [JsonProperty("best_score")]       public int? BestScore;
        [JsonProperty("avg_score")]        public double? AvgScore;
        [JsonProperty("trust_level")]      public int? TrustLevel;

        // ── Points ledger mirror. total_points IS the game's Reward Points balance,
        //    the same column PointsBalance reads — but the hub renders RP from
        //    PointsService (which also folds the pending queue), never from here. ──
        [JsonProperty("total_points")]     public int? TotalPoints;
        [JsonProperty("activity_pts")]     public int? ActivityPts;
        [JsonProperty("gift_pts")]         public int? GiftPts;

        // ── Avatar progression. ──
        [JsonProperty("avatar_level")]     public int? AvatarLevel;
        [JsonProperty("avatar_xp")]        public int? AvatarXp;

        // ── auth_golf_profile — self-declared, captured once by the post-signup Golf Profile
        //    screen and written through PUT /user/update. Both are NULL for every account that
        //    predates that screen and for anyone who tapped "Skip for now", so they are
        //    genuinely absent, not empty — the same posture as the golf record above.
        //    GolfExperience ∈ {beginner, intermediate, advanced}; AvatarColor ∈ {pink, green,
        //    blue, gold} (CHECK-constrained server-side by
        //    backend/migrations/2026_09_02_golf_profile.sql).
        [JsonProperty("golf_experience")]  public string GolfExperience;
        [JsonProperty("avatar_color")]     public string AvatarColor;

        // ── Social counters. ──
        [JsonProperty("followers_count")]  public int? FollowersCount;
        [JsonProperty("following_count")]  public int? FollowingCount;
        [JsonProperty("badges_count")]     public int? BadgesCount;
        [JsonProperty("activities_count")] public int? ActivitiesCount;

        public override string ToString()
            => $"UserDetailDto {DisplayName} (hc={Handicap}, best={BestScore}, trust={TrustLevel})";
    }
}
