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

        [JsonProperty("prize_bands")] public List<RemotePrizeBandDto>? PrizeBands;
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
