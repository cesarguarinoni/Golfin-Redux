// ─────────────────────────────────────────────────────────────────────────────
// UI/Rankings — wire DTOs for GET /api/v1/leaderboards/{period}
//
// These are the only types that know the server's field names. Same arrangement
// (and same reasoning) as RemoteBannerDtos.cs / RemoteTournamentDtos.cs.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Golfin.UI.Rankings
{
    /// <summary>
    /// The <c>data</c> object of the leaderboard response (leaderboard_backend SPEC §1).
    /// <see cref="Golfin.Net.ApiEnvelope"/> has already unwrapped <c>{"data": …}</c> by the time a live
    /// fetch is deserialised; the disk cache holds the RAW body, so the reader tolerates both — see
    /// <c>BackendLeaderboardProvider.Deserialize</c>.
    /// </summary>
    public sealed class LeaderboardResponseDto
    {
        /// <summary>Server "now" at the moment the board was computed. The countdown's skew reference.</summary>
        [JsonProperty("fetched_at")]     public string? FetchedAt;

        /// <summary><c>daily|weekly|monthly|historic</c>, echoed back. Diagnostics only.</summary>
        [JsonProperty("period")]         public string? Period;

        /// <summary>
        /// End of the current period, or NULL for <c>historic</c> (which never resets).
        ///
        /// Kept as a STRING for the same reason <c>RemoteBannerDto.ExpiresAt</c> is: typing it as
        /// <see cref="System.DateTime"/> lets Newtonsoft apply its default <c>DateTimeZoneHandling</c>
        /// and hand back a LOCAL time, which would give two players in different zones different
        /// countdowns. Parsed explicitly with <c>AdjustToUniversal | AssumeUniversal</c>.
        /// </summary>
        [JsonProperty("period_end_utc")] public string? PeriodEndUtc;

        /// <summary>Top ≤100, already sorted and ranked server-side.</summary>
        [JsonProperty("entries")]        public List<LeaderboardEntryDto>? Entries;

        /// <summary>The caller's own row. ALWAYS present, even at score 0 outside the top slice.</summary>
        [JsonProperty("player")]         public LeaderboardEntryDto? Player;
    }

    /// <summary>
    /// One row of the board. Ranks and <c>is_tie</c> arrive computed (standard competition ranking,
    /// 1,2,2,4) and are rendered verbatim — the client does NOT re-rank.
    /// </summary>
    public sealed class LeaderboardEntryDto
    {
        [JsonProperty("rank")]         public int Rank;

        /// <summary>True when two or more rows share this rank — drives the "T11" prefix.</summary>
        [JsonProperty("is_tie")]       public bool IsTie;

        [JsonProperty("display_name")] public string? DisplayName;

        /// <summary>
        /// CAN BE NULL — PLAYLIFE-only users and players who have never synced a character have no
        /// portrait. Null flows through as an empty string so the existing default-portrait path in
        /// <c>RankingsCardWidget</c> / <c>Top3CardWidget</c> takes over.
        /// </summary>
        [JsonProperty("character_id")] public string? CharacterId;

        [JsonProperty("level")]        public int Level;

        /// <summary>Game-action RP for the period. Server filters the ledger by the
        /// <c>game_point_actions</c> catalog, so admin grants and PLAYLIFE actions never appear.</summary>
        [JsonProperty("score")]        public long Score;

        /// <summary>Marks the caller's row inside <c>entries</c> when they are in the top slice.
        /// Absent on the <c>player</c> object itself, which is the caller by definition.</summary>
        [JsonProperty("is_player")]    public bool IsPlayer;
    }

    /// <summary>Request body of PUT <c>/user/golfin-character</c> (SPEC §1 / §5).</summary>
    public sealed class GolfinCharacterSyncDto
    {
        [JsonProperty("character_id")] public string? CharacterId;
        [JsonProperty("level")]        public int Level;
    }
}
