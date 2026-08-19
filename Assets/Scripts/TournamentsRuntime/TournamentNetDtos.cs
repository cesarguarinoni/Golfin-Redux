// ─────────────────────────────────────────────────────────────────────────────
// TournamentsRuntime — wire DTOs for the async-multiplayer tournament endpoints
// (tournament_async_board SPEC §1: enter / submit-hole / entry / leaderboard).
//
// Lives in Assembly-CSharp, NOT in Golfin.Tournaments: that assembly is
// deliberately dependency-light and must never learn that a network exists
// (the same rule that keeps Golfin.Net out of Golfin.Tournaments.asmdef).
// These DTOs are the only types that know the server's field names.
//
// EVERY TIMESTAMP IS A STRING, deliberately. Typing one as DateTime lets
// Newtonsoft apply its default DateTimeZoneHandling and hand back a LOCAL time,
// which would shift a schedule by the device's offset — a player in UTC+7 and one
// in UTC-5 would then disagree about when a tournament closes. The reader in
// TournamentNetJson pins DateParseHandling.None on BOTH the JsonTextReader and the
// serializer for the same reason; do not "simplify" either away.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Golfin.Tournaments
{
    // ═════════════════════════════════════════════════════════════════════════
    // POST /golfin/{slug}/enter
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Request body for <c>POST /golfin/{slug}/enter</c>.</summary>
    public sealed class TournamentEnterRequestDto
    {
        [JsonProperty("character_id")] public string? CharacterId;
    }

    /// <summary>
    /// The <c>data</c> object of an enter response.
    /// <para>
    /// Two shapes share one type because the server returns 200 for both: the happy path
    /// (<c>entered</c>/<c>already_entered</c> + <c>entry</c>) and the short-balance path
    /// (<c>entered:false</c>, <c>status:"insufficient"</c>, <c>requested</c>, <c>total_points</c>).
    /// An insufficient answer is NOT an HTTP error, so branching on the status code alone would
    /// read it as success and enter a player who was never charged.
    /// </para>
    /// </summary>
    public sealed class TournamentEnterResponseDto
    {
        [JsonProperty("entered")]        public bool Entered;
        [JsonProperty("already_entered")] public bool AlreadyEntered;

        /// <summary>Only present on the refusal path — <c>"insufficient"</c>.</summary>
        [JsonProperty("status")]         public string? Status;

        [JsonProperty("requested")]      public long Requested;
        [JsonProperty("total_points")]   public long TotalPoints;

        [JsonProperty("entry")]          public TournamentEntryDto? Entry;

        // ── Restriction denials (tournament_restrictions, server LIVE 2026-08-18) ──
        // Same 200-shaped family as "insufficient", and for the same reason: a refusal is an
        // ANSWER, not a transport failure, so the client toasts it instead of retrying it.

        /// <summary>Which band refused the entry — <c>"char_rarity"</c> or <c>"char_level"</c> —
        /// on the <c>ineligible</c> denial.</summary>
        [JsonProperty("reason")]         public string? Reason;

        /// <summary>The human cap, echoed back on the <c>full</c> denial.</summary>
        [JsonProperty("max_players")]    public int    MaxPlayers;

        /// <summary>True when the server refused for lack of points (SPEC §1).</summary>
        [JsonIgnore]
        public bool IsInsufficient
            => string.Equals(Status, "insufficient", StringComparison.OrdinalIgnoreCase);

        /// <summary>True when the human field is already at <c>max_players</c>.</summary>
        [JsonIgnore]
        public bool IsFull
            => string.Equals(Status, "full", StringComparison.OrdinalIgnoreCase);

        /// <summary>True when the character failed a rarity/level band.</summary>
        [JsonIgnore]
        public bool IsIneligible
            => string.Equals(Status, "ineligible", StringComparison.OrdinalIgnoreCase);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // POST /golfin/{slug}/submit-hole
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Request body for <c>POST /golfin/{slug}/submit-hole</c>.</summary>
    public sealed class TournamentSubmitHoleRequestDto
    {
        [JsonProperty("hole_number")]     public int    HoleNumber;
        [JsonProperty("strokes")]         public int    Strokes;
        [JsonProperty("idempotency_key")] public string? IdempotencyKey;
    }

    /// <summary>
    /// The <c>data</c> object of a submit-hole response.
    /// <c>replayed:true</c> is a SUCCESS, not a conflict — it is exactly what the offline queue sees
    /// when it re-sends an op the server already accepted, and the op is dropped on it.
    /// </summary>
    public sealed class TournamentSubmitHoleResponseDto
    {
        [JsonProperty("replayed")] public bool Replayed;
        [JsonProperty("hole")]     public TournamentHoleDto? Hole;
        [JsonProperty("entry")]    public TournamentEntryDto? Entry;
    }

    // ═════════════════════════════════════════════════════════════════════════
    // GET /golfin/{slug}/entry
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>One hole the server has on record for the caller's entry.</summary>
    public sealed class TournamentHoleDto
    {
        [JsonProperty("hole_number")]  public int     HoleNumber;
        [JsonProperty("strokes")]      public int     Strokes;
        [JsonProperty("submitted_at")] public string? SubmittedAt;
    }

    /// <summary>
    /// The caller's entry as the server holds it — the cross-device resume payload.
    /// <c>{data: null}</c> (i.e. a null DTO) means "not entered", which is a normal answer.
    /// </summary>
    public sealed class TournamentEntryDto
    {
        [JsonProperty("character_id")] public string? CharacterId;

        /// <summary><c>in_progress</c> | <c>finished</c> | <c>dnf</c>.</summary>
        [JsonProperty("status")]       public string? Status;

        [JsonProperty("best_score")]   public int?    BestScore;
        [JsonProperty("entered_at")]   public string? EnteredAt;
        [JsonProperty("submitted_at")] public string? SubmittedAt;

        [JsonProperty("holes")]        public List<TournamentHoleDto>? Holes;
    }

    // ═════════════════════════════════════════════════════════════════════════
    // GET /golfin/{slug}/leaderboard
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// One board row. Mapped VERBATIM to <see cref="TournamentLeaderboardEntry"/> — rank, tie flags
    /// and the organic bot reveal are the server's answer and are never recomputed here (SPEC §1:
    /// "Do NOT re-rank client-side").
    /// </summary>
    public sealed class TournamentBoardRowDto
    {
        /// <summary>Nullable: the caller's own row carries <c>rank: null</c> while unranked
        /// (entered, nothing submitted yet). Ranked rows in <c>entries</c> always have one.</summary>
        [JsonProperty("rank")]          public int?    Rank;

        [JsonProperty("is_tie")]        public bool    IsTie;
        [JsonProperty("display_name")]  public string? DisplayName;
        [JsonProperty("character_id")]  public string? CharacterId;
        [JsonProperty("level")]         public int     Level;
        [JsonProperty("strokes")]       public int     Strokes;
        [JsonProperty("thru")]          public int     Thru;
        [JsonProperty("score_to_par")]  public int     ScoreToPar;
        [JsonProperty("is_player")]     public bool    IsPlayer;
        [JsonProperty("is_bot")]        public bool    IsBot;
        [JsonProperty("is_dnf")]        public bool    IsDnf;

        /// <summary>
        /// Human-only rank — bots are never paid. Present on the <c>player</c> object; the sticky row
        /// shows it alongside the display rank while <c>bots_active</c> and the two differ.
        /// </summary>
        [JsonProperty("prize_rank")]    public int?    PrizeRank;
    }

    /// <summary>The <c>data</c> object of a leaderboard response.</summary>
    public sealed class TournamentBoardDto
    {
        [JsonProperty("fetched_at")]            public string? FetchedAt;
        [JsonProperty("provisional")]           public bool    Provisional;

        /// <summary>False once the bot field has retired (10 human entries). ONE-WAY: it never
        /// goes true again for that tournament.</summary>
        [JsonProperty("bots_active")]           public bool    BotsActive;

        [JsonProperty("end_at")]                public string? EndAt;
        [JsonProperty("resolve_delay_minutes")] public int     ResolveDelayMinutes;

        [JsonProperty("entries")]               public List<TournamentBoardRowDto>? Entries;

        /// <summary>The caller's own row — ALWAYS present when entered, even when excluded from
        /// <c>entries</c> (DNF, thru-0, or outside the returned slice). Null when not entered.</summary>
        [JsonProperty("player")]                public TournamentBoardRowDto? Player;
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Reader
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// The one JSON reader for every tournament network payload.
    ///
    /// <para>Tolerates BOTH shapes on purpose, exactly like <c>BannerService.Deserialize</c> and
    /// <c>BackendLeaderboardProvider.Deserialize</c>: the live path hands over a body
    /// <c>ApiEnvelope</c> has already unwrapped, while the disk cache holds the raw
    /// <c>{"data": …}</c>. One reader has to survive both or a cold open and a warm refresh
    /// disagree.</para>
    ///
    /// <para><b><c>DateParseHandling.None</c> on BOTH the reader and the serializer.</b> The reader
    /// setting stops <c>JToken.ReadFrom</c> materialising date-shaped strings as <c>DateTime</c>
    /// while building the tree; the serializer setting stops <c>ToObject</c> doing the same on the
    /// way into the DTO. Either one alone leaves a hole, and the symptom is a schedule that shifts
    /// by the device's UTC offset.</para>
    /// </summary>
    public static class TournamentNetJson
    {
        private const string Tag = "[TournamentNet]";

        /// <summary>Parse one raw (or already-unwrapped) body into <typeparamref name="T"/>,
        /// or null when it is absent, JSON <c>null</c>, or unusable.</summary>
        public static T? Read<T>(string? json, string source) where T : class
        {
            if (string.IsNullOrWhiteSpace(json)) return null;

            try
            {
                var settings   = new JsonSerializerSettings { DateParseHandling = DateParseHandling.None };
                var serializer = JsonSerializer.CreateDefault(settings);

                JToken root;
                using (var reader = new JsonTextReader(new StringReader(json!)) { DateParseHandling = DateParseHandling.None })
                    root = JToken.ReadFrom(reader);

                JToken payload = root;
                if (root.Type == JTokenType.Object)
                {
                    JToken? inner = ((JObject)root)["data"];
                    if (inner != null) payload = inner;
                }

                // {"data": null} is a legitimate answer on GET /entry — "not entered", not a failure.
                if (payload.Type == JTokenType.Null) return null;

                return payload.ToObject<T>(serializer);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"{Tag} Could not parse the {source} payload as {typeof(T).Name}: {ex.Message}");
                return null;
            }
        }

        /// <summary>Serialize a request body, omitting nulls (the server's pydantic models treat an
        /// absent optional and an explicit null differently on some fields).</summary>
        public static string Write(object body)
            => JsonConvert.SerializeObject(body, new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                DateParseHandling = DateParseHandling.None
            });

        /// <summary>
        /// Absolute UTC from an ISO-8601 string, or null. <c>AssumeUniversal</c> covers a server that
        /// drops the offset; <c>AdjustToUniversal</c> normalises <c>+00:00</c> / <c>Z</c> forms to the
        /// same instant. The same parse the banner, schedule and rankings payloads use.
        /// </summary>
        public static DateTime? ParseUtc(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            return DateTime.TryParse(value, CultureInfo.InvariantCulture,
                       DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out DateTime parsed)
                   ? parsed
                   : (DateTime?)null;
        }
    }
}
