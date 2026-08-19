// ─────────────────────────────────────────────────────────────────────────────
// TournamentsRuntime — wire DTOs for GET /api/v1/tournaments/golfin
//
// Lives in Assembly-CSharp (no asmdef), NOT in Golfin.Tournaments: that assembly
// is deliberately dependency-light and must never learn that a network exists
// (SPEC §3 D2). These DTOs are the only types that know the server's field names.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Golfin.Tournaments
{
    /// <summary>
    /// The <c>data</c> object of the schedule response. <see cref="Golfin.Net.ApiEnvelope"/> has
    /// already unwrapped <c>{"data": …}</c> by the time this is deserialised.
    /// </summary>
    public sealed class RemoteScheduleDto
    {
        [JsonProperty("fetched_at")]  public string? FetchedAt;
        [JsonProperty("tournaments")] public List<RemoteTournamentDto>? Tournaments;
    }

    /// <summary>One <c>kind='golfin'</c> row plus its prize ladder.</summary>
    public sealed class RemoteTournamentDto
    {
        [JsonProperty("slug")]      public string? Slug;
        [JsonProperty("title")]     public string? Title;

        /// <summary>
        /// The dashboard's optional Japanese title. A plain string — it must NOT be interpreted by
        /// the reader (the file-level <c>DateParseHandling.None</c> already guarantees that; do not
        /// "simplify" it away). Shown only to JP players — see <c>TournamentDisplayName.Resolve</c>.
        /// </summary>
        [JsonProperty("title_ja")]  public string? TitleJa;

        [JsonProperty("name_key")]  public string? NameKey;
        [JsonProperty("course_id")] public string? CourseId;
        [JsonProperty("hole_set")]  public string? HoleSet;

        // Kept as STRINGS on purpose. Typing these as DateTime lets Newtonsoft apply its default
        // DateTimeZoneHandling and hand back a LOCAL time, which would give a player in UTC+7 a
        // different schedule from a player in UTC-5. They are parsed explicitly with
        // AdjustToUniversal | AssumeUniversal in TournamentScheduleMapper, matching the discipline
        // TournamentCsvLoader.ParseUtc already enforces for the shipped CSV.
        [JsonProperty("start_at")] public string? StartAt;
        [JsonProperty("end_at")]   public string? EndAt;

        [JsonProperty("resolve_delay_minutes")] public int    ResolveDelayMinutes;
        [JsonProperty("entry_fee_pts")]         public long   EntryFeePts;
        [JsonProperty("bot_field_id")]          public string? BotFieldId;
        [JsonProperty("sponsor_name")]          public string? SponsorName;
        [JsonProperty("league_key")]            public string? LeagueKey;
        [JsonProperty("banner_url")]            public string? BannerUrl;
        [JsonProperty("bot_seed")]              public long   BotSeed;

        // ── Category + entry restrictions (tournament_restrictions, server LIVE 2026-08-18) ──
        // ALL NULLABLE, and null means unrestricted. int? rather than int is load-bearing:
        // a plain int would deserialise an absent max_players as 0, which the definition would
        // then read as "a cap of zero players". The enum-ish fields stay STRINGS here — this
        // layer only knows the server's field names; TournamentDefinition is where they are
        // parsed, and where an unknown value degrades to unrestricted instead of throwing.
        [JsonProperty("category")]             public string? Category;
        [JsonProperty("max_players")]          public int?    MaxPlayers;
        [JsonProperty("players_per_division")] public int?    PlayersPerDivision;
        [JsonProperty("division_type")]        public string? DivisionType;
        [JsonProperty("char_rarity_min")]      public string? CharRarityMin;
        [JsonProperty("char_rarity_max")]      public string? CharRarityMax;
        [JsonProperty("char_level_min")]       public int?    CharLevelMin;
        [JsonProperty("char_level_max")]       public int?    CharLevelMax;
        [JsonProperty("gear_rule")]            public string? GearRule;
        [JsonProperty("club_rarity_max")]      public string? ClubRarityMax;

        // The sign-up modal's blurb (SPEC §3.1). Plain pass-through strings, exactly like
        // title_ja — the file-level DateParseHandling.None discipline applies here too, so a
        // description that happens to open with a date-shaped token is not silently reinterpreted.
        [JsonProperty("description_en")]  public string? DescriptionEn;
        [JsonProperty("description_ja")]  public string? DescriptionJa;
        [JsonProperty("description_key")] public string? DescriptionKey;

        [JsonProperty("prize_bands")] public List<RemotePrizeBandDto>? PrizeBands;

        /// <summary>
        /// The sign-up modal's cross-promotion strip, or null when this tournament has no banner
        /// assigned — or its banner row was deleted, or switched inactive. The server collapses all
        /// three into the same null, deliberately: they are the same thing to the client, and the
        /// no-banner modal is a complete design, not a degraded one.
        /// <para>
        /// Note there is no id here. <c>modal_banner_id</c> stays server-side.
        /// </para>
        /// </summary>
        [JsonProperty("modal_banner")] public RemoteModalBannerDto? ModalBanner;
    }

    /// <summary>
    /// The <c>modal_banner</c> object on a tournament row — the artwork for Figma
    /// <c>13892:3435</c>, resolved out of <c>game_banners</c> by the server.
    /// <para>
    /// Plain strings, no date handling: a <c>tournament_modal</c> banner has no schedule of its
    /// own. The tournament's own window governs when the strip is on screen, which is why
    /// <c>start_at</c>/<c>end_at</c>/<c>sort_order</c> are absent here even though the table has
    /// them for the other two placements.
    /// </para>
    /// </summary>
    public sealed class RemoteModalBannerDto
    {
        [JsonProperty("image_url_en")] public string? ImageUrlEn;
        [JsonProperty("image_url_ja")] public string? ImageUrlJa;
        [JsonProperty("link_url")]     public string? LinkUrl;
    }

    /// <summary>One rank band. Per-tournament, not a shared template (Phase-1 schema decision).</summary>
    public sealed class RemotePrizeBandDto
    {
        [JsonProperty("rank_from")]      public int    RankFrom;
        [JsonProperty("rank_to")]        public int    RankTo;
        [JsonProperty("rp_reward")]      public long   RpReward;
        [JsonProperty("item_reward_id")] public string? ItemRewardId;
    }
}
