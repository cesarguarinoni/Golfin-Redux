// gps_profile_pack — DTOs for /score/stats and /badges/progress endpoints
// These are in Golfin.Gps so they stay game-free (no UnityEngine / Golfin.Roster deps).
using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Golfin.Gps
{
    // ── /score/stats ────────────────────────────────────────────────────────────

    /// <summary>Envelope from GET /score/stats → {data: &lt;ScoreStatsDto&gt;}.</summary>
    [Serializable]
    public sealed class ScoreStatsResponse
    {
        [JsonProperty("data")] public ScoreStatsDto Data;
    }

    /// <summary>Aggregate score statistics for the authenticated user.</summary>
    [Serializable]
    public sealed class ScoreStatsDto
    {
        [JsonProperty("rounds_played")]  public int    RoundsPlayed;
        [JsonProperty("total_strokes")]  public int    TotalStrokes;
        [JsonProperty("best_score")]     public int?   BestScore;      // net vs par, can be negative
        [JsonProperty("avg_score")]      public float? AvgScore;       // net vs par
        [JsonProperty("handicap")]       public float? Handicap;
        [JsonProperty("birdies")]        public int    Birdies;
        [JsonProperty("eagles")]         public int    Eagles;
        [JsonProperty("holes_in_one")]   public int    HolesInOne;
        [JsonProperty("pars")]           public int    Pars;
        [JsonProperty("bogeys")]         public int    Bogeys;
        [JsonProperty("double_bogeys")]  public int    DoubleBogeys;
    }

    // ── /badges/progress ────────────────────────────────────────────────────────

    /// <summary>Envelope from GET /badges/progress → {data: [&lt;BadgeProgressDto&gt;]}.</summary>
    [Serializable]
    public sealed class BadgesProgressResponse
    {
        [JsonProperty("data")] public List<BadgeProgressDto> Data;
    }

    /// <summary>One badge definition with the caller's earned state.</summary>
    [Serializable]
    public sealed class BadgeProgressDto
    {
        [JsonProperty("id")]          public string Id;
        [JsonProperty("name_key")]    public string NameKey;          // loc key, e.g. "BADGE_FIRST_WIN_NAME"
        [JsonProperty("section")]     public string Section;          // "GOLF" | "SOCIAL" | "TRUST" | "SPECIAL"
        [JsonProperty("rarity")]      public string Rarity;           // "COMMON"|"RARE"|"EPIC"|"LEGEND"
        [JsonProperty("icon_url")]    public string IconUrl;          // CDN URL for the badge icon sprite
        [JsonProperty("icon_local")]  public string IconLocal;        // Resources/ path fallback
        [JsonProperty("required")]    public int    Required;         // threshold to earn
        [JsonProperty("progress")]    public int    Progress;         // caller's current count
        [JsonProperty("earned")]      public bool   Earned;
        [JsonProperty("earn_date")]   public string EarnDate;         // ISO date or null
    }
}
