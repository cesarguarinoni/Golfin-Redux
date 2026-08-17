// ─────────────────────────────────────────────────────────────────────────────
// TournamentsRuntime — TournamentScheduleMapper
// JSON (server or disk cache) → the plain DTOs Golfin.Tournaments already speaks.
// This is the ONLY place that knows both vocabularies. Headless and pure: no
// Unity lifecycle, no coroutines, no file IO — so it is directly unit-testable.
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
    /// <summary>A fully-mapped schedule: definitions plus the prize table each one points at.</summary>
    public sealed class TournamentSchedule
    {
        public IReadOnlyList<TournamentDefinition>     Definitions { get; }
        public IReadOnlyDictionary<string, PrizeTable> PrizeTables { get; }
        public string?                                 FetchedAt   { get; }

        public TournamentSchedule(
            IReadOnlyList<TournamentDefinition>     definitions,
            IReadOnlyDictionary<string, PrizeTable> prizeTables,
            string?                                 fetchedAt)
        {
            Definitions = definitions;
            PrizeTables = prizeTables;
            FetchedAt   = fetchedAt;
        }
    }

    public static class TournamentScheduleMapper
    {
        private const string Tag = "[TournamentSchedule]";

        /// <summary>
        /// Parse a raw response body (live or cached — both are the same bytes) and map it.
        /// Returns null when the body is unusable; the caller then keeps whatever source it already had.
        /// <paramref name="origin"/> only labels log lines ("server" / "cache").
        /// </summary>
        public static TournamentSchedule? TryMapJson(
            string?                                     json,
            IReadOnlyDictionary<string, BotFieldConfig> botFields,
            string                                      origin)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;

            RemoteScheduleDto? dto;
            try
            {
                dto = Deserialize(json!);
            }
            catch (Exception ex)
            {
                Debug.LogError($"{Tag} Could not parse the {origin} schedule JSON: {ex.Message}");
                return null;
            }

            return Map(dto, botFields, origin);
        }

        /// <summary>
        /// Unwrap <c>{"data": …}</c> and deserialise, with <b>date parsing switched OFF</b>.
        ///
        /// ⚠️ This is why <see cref="Golfin.Net.ApiEnvelope.TryUnwrap{T}"/> is deliberately NOT reused
        /// here, despite the payload sharing its envelope. Newtonsoft's default
        /// <c>DateParseHandling.DateTime</c> converts anything that LOOKS like a date at the READER
        /// level — before the value is ever assigned to a field. So a <c>string StartAt</c> field
        /// still received a round-tripped LOCAL-time string: <c>"2026-08-25T00:00:00+00:00"</c>
        /// arrived as <c>"08/25/2026 07:00:00"</c> on a UTC+7 machine. Every player would have got a
        /// schedule shifted by their own timezone, and two players in different zones would disagree
        /// about when the same tournament opens.
        ///
        /// <c>DateParseHandling.None</c> keeps the timestamps as the exact characters the server
        /// sent, so <see cref="TryParseUtc"/> is the only thing that ever interprets them.
        /// (Caught by <c>ScheduleMapperTests.ParsesTimestampsAsAbsoluteUtc_NeverLocal</c>.)
        /// </summary>
        private static RemoteScheduleDto? Deserialize(string json)
        {
            var settings = new JsonSerializerSettings
            {
                DateParseHandling = DateParseHandling.None,
            };
            var serializer = JsonSerializer.CreateDefault(settings);

            JToken root;
            using (var reader = new JsonTextReader(new StringReader(json)) { DateParseHandling = DateParseHandling.None })
                root = JToken.ReadFrom(reader);

            // A cached RAW body still carries {"data":…}; an already-unwrapped payload does not.
            JToken payload = root;
            if (root.Type == JTokenType.Object)
            {
                JToken? inner = ((JObject)root)["data"];
                if (inner != null) payload = inner;
            }

            if (payload.Type == JTokenType.Null) return null;
            return payload.ToObject<RemoteScheduleDto>(serializer);
        }

        /// <summary>
        /// Map a deserialised payload to definitions + prize tables.
        ///
        /// <b>Bad data fails loudly and locally (SPEC §3 D5).</b> A tournament with a dangling
        /// <c>bot_field_id</c>, an unparseable window or an empty prize ladder is DROPPED with an
        /// error naming it, and every other tournament still renders. Never a silent disappearance,
        /// never a half-defined tournament reaching the card.
        /// </summary>
        public static TournamentSchedule? Map(
            RemoteScheduleDto?                          dto,
            IReadOnlyDictionary<string, BotFieldConfig> botFields,
            string                                      origin)
        {
            if (dto?.Tournaments == null)
            {
                Debug.LogError($"{Tag} The {origin} payload has no 'tournaments' array.");
                return null;
            }

            var defs   = new List<TournamentDefinition>(dto.Tournaments.Count);
            var prizes = new Dictionary<string, PrizeTable>(dto.Tournaments.Count, StringComparer.Ordinal);
            int dropped = 0;

            foreach (var t in dto.Tournaments)
            {
                if (t == null) { dropped++; continue; }

                string slug = (t.Slug ?? string.Empty).Trim();
                if (slug.Length == 0)
                {
                    Debug.LogError($"{Tag} Dropping a {origin} tournament with an empty slug.");
                    dropped++;
                    continue;
                }

                // ── Prize ladder ─────────────────────────────────────────────
                // The server's bands are per-tournament, so each tournament gets a one-entry
                // PrizeTable keyed by its own slug. PrizeTableId is therefore SYNTHESIZED = slug;
                // the CSV's shared prizeTableId indirection has no server equivalent.
                var bands = MapBands(t.PrizeBands);
                if (bands.Count == 0)
                {
                    Debug.LogError(
                        $"{Tag} Dropping '{slug}' ({origin}): it has an empty prize ladder. " +
                        "A tournament with no prizes would render a 0 RP reward on the card.");
                    dropped++;
                    continue;
                }

                // ── Bot field must resolve against the still-bundled CSV ─────
                string botFieldId = (t.BotFieldId ?? string.Empty).Trim();
                if (!botFields.ContainsKey(botFieldId))
                {
                    Debug.LogError(
                        $"{Tag} Dropping '{slug}' ({origin}): botFieldId='{botFieldId}' is not in the " +
                        "shipped tournament_bot_fields.csv, so its field could not be rolled.");
                    dropped++;
                    continue;
                }

                // ── Window ───────────────────────────────────────────────────
                if (!TryParseUtc(t.StartAt, out DateTime startUtc) ||
                    !TryParseUtc(t.EndAt,   out DateTime endUtc))
                {
                    Debug.LogError(
                        $"{Tag} Dropping '{slug}' ({origin}): unparseable window " +
                        $"start_at='{t.StartAt}' end_at='{t.EndAt}'. State is derived from these, " +
                        "so a bad window would put the card in an arbitrary state.");
                    dropped++;
                    continue;
                }

                // ── Hole set ─────────────────────────────────────────────────
                // ExpandHoleSet returns empty for an empty/unparseable value, and a zero-hole
                // tournament would otherwise reach a card reading "· 0 Holes" and be enterable.
                var holeSet = TournamentCsvLoader.ExpandHoleSet(t.HoleSet ?? string.Empty);
                if (holeSet.Count == 0)
                {
                    Debug.LogError(
                        $"{Tag} Dropping '{slug}' ({origin}): hole_set='{t.HoleSet}' expands to zero holes, " +
                        "so there would be nothing to play.");
                    dropped++;
                    continue;
                }

                // ── Artwork: refuse anything off the allowlisted Storage prefix ──
                string? bannerUrl = null;
                if (!string.IsNullOrWhiteSpace(t.BannerUrl))
                {
                    if (TournamentArtPolicy.IsAllowed(t.BannerUrl))
                        bannerUrl = t.BannerUrl!.Trim();
                    else
                        Debug.LogWarning(
                            $"{Tag} REFUSED banner_url for '{slug}': '{Describe(t.BannerUrl!)}' is not under " +
                            $"'{TournamentArtPolicy.AllowedArtPrefix}'. Nothing was downloaded; " +
                            "the card falls back to bundled art.");
                }

                prizes[slug] = new PrizeTable(slug, bands);

                defs.Add(new TournamentDefinition(
                    id:                  slug,                                          // the slug stays the client's stable key, not the uuid
                    nameKey:             t.NameKey  ?? string.Empty,
                    clubId:              t.CourseId ?? string.Empty,
                    holeSet:             holeSet,                                   // via TournamentCsvLoader.ExpandHoleSet — reuse, do not reimplement
                    startUtc:            startUtc,
                    endUtc:              endUtc,
                    resolveDelayMinutes: t.ResolveDelayMinutes,
                    entryFeeRP:          t.EntryFeePts,
                    prizeTableId:        slug,
                    botFieldId:          botFieldId,
                    sponsorKey:          t.SponsorName ?? string.Empty,
                    leagueKey:           t.LeagueKey   ?? string.Empty,
                    title:               NullIfBlank(t.Title),
                    bannerUrl:           bannerUrl,
                    titleJa:             NullIfBlank(t.TitleJa)));   // JP-only rung of the name ladder
            }

            if (defs.Count == 0)
            {
                Debug.LogError(
                    $"{Tag} The {origin} schedule produced ZERO usable tournaments " +
                    $"({dropped} dropped). Keeping the previous source rather than showing an empty screen.");
                return null;
            }

            // Belt-and-braces: the drops above already guarantee this passes, so a failure here means
            // a mapping bug rather than bad data. Run it anyway — it is the same gate the CSV goes through.
            TournamentCsvLoader.CheckReferentialIntegrity(defs, prizes, botFields);

            if (dropped > 0)
                Debug.LogWarning($"{Tag} Mapped {defs.Count} tournament(s) from {origin}; {dropped} dropped (see errors above).");

            return new TournamentSchedule(defs, prizes, dto.FetchedAt);
        }

        // ── Mid-entry preservation (SPEC §4.2, D3 as amended 2026-08-14) ──────

        /// <summary>
        /// Merge <paramref name="incoming"/> over the schedule currently in play, carrying forward
        /// any tournament the player has already ENTERED — definition and prize ladder together.
        ///
        /// <para>
        /// <b>This is a UNION, not an intersection, and that distinction is the whole point.</b>
        /// Walking only <paramref name="incoming"/> can protect an entered tournament that the server
        /// still sends, but silently loses one the server has DROPPED (deleted in the dashboard, or
        /// <c>kind</c>-flipped). Losing it makes every subsequent <c>GetTournament(id)</c> throw
        /// <c>KeyNotFoundException</c> (<c>LocalTournamentBackend.cs:124</c>) — that is the signup
        /// modal, the result modal, the round handler, and <c>SubmitHoleResult</c>, i.e. a mid-round
        /// hole submission takes the app down. §4.2 says an entered tournament must not change under
        /// the player; vanishing is the maximal change.
        /// </para>
        /// <para>
        /// D3's "wholesale or not at all" governs the schedule as a whole; a mid-entry definition is
        /// the one deliberate exception and is logged every time it is exercised.
        /// </para>
        /// </summary>
        /// <param name="isEntered">
        /// Returns true when the player holds an entry for that tournament id. Injected rather than
        /// reaching for the backend, so this stays a pure function and is directly testable.
        /// </param>
        public static TournamentSchedule MergePreservingEntered(
            TournamentSchedule                      incoming,
            IReadOnlyList<TournamentDefinition>?    current,
            IReadOnlyDictionary<string, PrizeTable>? currentPrizes,
            Func<string, bool>                      isEntered)
        {
            if (incoming == null) throw new ArgumentNullException(nameof(incoming));
            if (current == null || current.Count == 0 || isEntered == null) return incoming;

            var currentById = new Dictionary<string, TournamentDefinition>(current.Count, StringComparer.Ordinal);
            foreach (var d in current) currentById[d.Id] = d;

            var defs   = new List<TournamentDefinition>(incoming.Definitions.Count);
            var prizes = new Dictionary<string, PrizeTable>(incoming.PrizeTables.Count, StringComparer.Ordinal);
            foreach (var kv in incoming.PrizeTables) prizes[kv.Key] = kv.Value;

            var seen = new HashSet<string>(StringComparer.Ordinal);

            // Pass 1 — the incoming schedule, with entered rows swapped back to the in-play version.
            foreach (var incomingDef in incoming.Definitions)
            {
                seen.Add(incomingDef.Id);

                if (isEntered(incomingDef.Id) && currentById.TryGetValue(incomingDef.Id, out var inPlay))
                {
                    Debug.Log(
                        $"{Tag} DEFERRED update for '{incomingDef.Id}': the player is already entered, " +
                        "so the definition they registered against is kept. " +
                        "The server version applies at the next launch.");

                    defs.Add(inPlay);
                    CarryPrizeTable(inPlay, currentPrizes, prizes);
                    continue;
                }

                defs.Add(incomingDef);
            }

            // Pass 2 — entered tournaments the server no longer sends at all. THE B1 CASE.
            foreach (var d in current)
            {
                if (seen.Contains(d.Id)) continue;
                if (!isEntered(d.Id)) continue;   // not entered → the server dropping it is just a schedule change

                Debug.LogWarning(
                    $"{Tag} '{d.Id}' is no longer in the server schedule but the player has an entry in it. " +
                    "Carrying the in-play definition forward so the signup/result/round paths keep resolving; " +
                    "it will disappear once the entry is gone.");

                defs.Add(d);
                CarryPrizeTable(d, currentPrizes, prizes);
            }

            return new TournamentSchedule(defs, prizes, incoming.FetchedAt);
        }

        /// <summary>
        /// The ladder must travel with the definition. Swapping in the server's prizes while keeping
        /// the old definition is exactly the mid-session mutation the carry-forward exists to prevent.
        /// </summary>
        private static void CarryPrizeTable(
            TournamentDefinition                     def,
            IReadOnlyDictionary<string, PrizeTable>? source,
            Dictionary<string, PrizeTable>           target)
        {
            if (source != null && source.TryGetValue(def.PrizeTableId, out var table))
                target[def.PrizeTableId] = table;
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static List<PrizeBand> MapBands(List<RemotePrizeBandDto>? raw)
        {
            var bands = new List<PrizeBand>(raw?.Count ?? 0);
            if (raw == null) return bands;

            foreach (var b in raw)
            {
                if (b == null) continue;
                if (b.RankFrom < 1 || b.RankTo < b.RankFrom) continue; // mirrors the DB check constraint
                bands.Add(new PrizeBand(b.RankFrom, b.RankTo, b.RpReward, NullIfBlank(b.ItemRewardId)));
            }

            bands.Sort((x, y) => x.RankFrom.CompareTo(y.RankFrom));
            return bands;
        }

        /// <summary>
        /// Absolute-UTC parse, matching <c>TournamentCsvLoader.ParseUtc</c>'s discipline exactly:
        /// an offset in the string is honoured and converted, a naked timestamp is ASSUMED UTC.
        /// Never local time — every player must see the same window.
        /// </summary>
        internal static bool TryParseUtc(string? raw, out DateTime utc)
        {
            utc = DateTime.MinValue;
            if (string.IsNullOrWhiteSpace(raw)) return false;

            return DateTime.TryParse(
                raw, CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                out utc);
        }

        private static string? NullIfBlank(string? s)
            => string.IsNullOrWhiteSpace(s) ? null : s!.Trim();

        /// <summary>Log the offending scheme+host, not the full URL (which may carry a long signed path).</summary>
        private static string Describe(string url)
        {
            try
            {
                var u = new Uri(url);
                return u.Scheme + "://" + u.Host;
            }
            catch
            {
                return url.Length > 60 ? url.Substring(0, 60) + "…" : url;
            }
        }
    }
}
