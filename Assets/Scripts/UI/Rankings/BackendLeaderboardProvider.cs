// ─────────────────────────────────────────────────────────────────────────────
// UI/Rankings — BackendLeaderboardProvider
//
// The Phase-2 ILeaderboardProvider: every player sees the SAME board, because the
// board comes from the server rather than from a client-side fake generator.
//
// SYNCHRONOUS READS OVER AN ASYNC SNAPSHOT. ILeaderboardProvider is synchronous by
// design (the UI calls it during OnEnable and on every countdown tick), so this type
// holds a per-period SNAPSHOT and the screen drives the refresh. A cold open reads
// the disk cache written by the last successful fetch, so the board is on screen
// before the request that will replace it has even been sent.
//
// NO ERROR UI. Every failure path — offline, 5xx, unparseable body, corrupt cache —
// keeps whatever snapshot is already held and reports false to the caller. A
// leaderboard that is a few minutes stale is a far better outcome than an error
// state on a screen the player opened to look at numbers.
//
// THE SERVER RANKS. entries arrive sorted with rank + is_tie already computed
// (standard competition ranking, 1,2,2,4). Re-ranking here would be a second source
// of truth that silently disagrees with the server the moment the fake pool or the
// tie rules change — so the mapping below is verbatim, field for field.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Golfin.Net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Golfin.UI.Rankings
{
    /// <summary>
    /// Raw-body disk mirror for the leaderboard, one file per period.
    ///
    /// Same discipline as <c>RemoteBannerSource</c>, and for the same reasons: cache the RAW body
    /// (not a mapped view, so a later build that understands more fields can still read it), write
    /// it atomically via <c>.tmp</c> + replace (so a kill mid-write leaves the previous good cache
    /// rather than a truncated file), and return null on ANY failure.
    /// </summary>
    public static class LeaderboardDiskCache
    {
        private const string Tag = "[Leaderboard]";

        /// <summary><c>leaderboard_daily.json</c> … <c>leaderboard_historic.json</c>.</summary>
        public static string CacheFileName(LeaderboardPeriod period)
            => "leaderboard_" + BackendLeaderboardProvider.WirePeriod(period) + ".json";

        /// <summary><c>&lt;persistentDataPath&gt;/leaderboard_{period}.json</c>.
        /// Touches <c>Application.persistentDataPath</c>, so main thread only.</summary>
        public static string CachePath(LeaderboardPeriod period)
            => Path.Combine(Application.persistentDataPath, CacheFileName(period));

        /// <summary>The cached raw body, or null when there is no cache / it is unreadable.</summary>
        public static string? ReadCache(LeaderboardPeriod period)
        {
            try
            {
                string path = CachePath(period);
                return File.Exists(path) ? File.ReadAllText(path) : null;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"{Tag} Could not read the {period} cache: {ex.Message}");
                return null;
            }
        }

        /// <summary>Mirror the raw body to disk via <c>.tmp</c> + replace.</summary>
        public static void WriteCache(LeaderboardPeriod period, string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return;

            string path = CachePath(period);
            try
            {
                string? dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

                string tmp = path + ".tmp";
                File.WriteAllText(tmp, json);

                if (File.Exists(path)) File.Replace(tmp, path, null);
                else File.Move(tmp, path);
            }
            catch (Exception ex)
            {
                // A cache we could not write is a slower next open, not a broken session.
                Debug.LogWarning($"{Tag} Could not write the {period} cache '{path}': {ex.Message}");
            }
        }

        /// <summary>Test/debug helper — drop one period's cache.</summary>
        public static void ClearCache(LeaderboardPeriod period)
        {
            try { if (File.Exists(CachePath(period))) File.Delete(CachePath(period)); }
            catch (Exception ex) { Debug.LogWarning($"{Tag} Could not delete the {period} cache: {ex.Message}"); }
        }
    }

    public sealed class BackendLeaderboardProvider : ILeaderboardProvider
    {
        private const string Tag = "[Leaderboard]";

        /// <summary>What the screen renders for one period between refreshes.</summary>
        private sealed class Snapshot
        {
            /// <summary>Mapped verbatim from <c>entries</c> — ranks and ties untouched.</summary>
            public IReadOnlyList<LeaderboardEntry> Entries = Array.Empty<LeaderboardEntry>();

            /// <summary>The <c>player</c> object. Always present on a well-formed payload.</summary>
            public LeaderboardEntry? Player;

            /// <summary>Already skew-corrected — see <see cref="AdjustedPeriodEnd"/>.
            /// <see cref="DateTime.MaxValue"/> for historic / a null <c>period_end_utc</c>.</summary>
            public DateTime PeriodEndUtc = DateTime.MaxValue;
        }

        private readonly Dictionary<LeaderboardPeriod, Snapshot> _snapshots =
            new Dictionary<LeaderboardPeriod, Snapshot>();

        /// <summary>One in-flight request per period. A second Refresh for the same period while one is
        /// running is dropped rather than queued — the screen calls this from OnEnable AND from the tab
        /// handler, and a player bouncing tabs must not turn into a request per tap.</summary>
        private readonly HashSet<LeaderboardPeriod> _inFlight = new HashSet<LeaderboardPeriod>();

        private readonly ITimeProvider _time;

        /// <summary>
        /// Loads whatever the last successful fetch left on disk, for all four periods, SYNCHRONOUSLY.
        /// Nothing here waits on a socket — a cold open in airplane mode shows the last board the
        /// player saw (SPEC §7 manual: "Airplane mode → Rankings opens with the last cached board").
        /// </summary>
        public BackendLeaderboardProvider(ITimeProvider? time = null)
        {
            _time = time ?? NetworkTimeProvider.Instance;

            foreach (LeaderboardPeriod period in (LeaderboardPeriod[])Enum.GetValues(typeof(LeaderboardPeriod)))
            {
                string? cached = LeaderboardDiskCache.ReadCache(period);
                if (string.IsNullOrWhiteSpace(cached)) continue;

                // A corrupt cache file deserialises to null → no snapshot → GetRanking returns empty
                // and the screen's refresh fills it in. It is never a hard failure.
                Snapshot? snap = BuildSnapshot(cached!, period, "disk cache");
                if (snap != null) _snapshots[period] = snap;
            }
        }

        // ── ILeaderboardProvider ──────────────────────────────────────────────

        /// <summary>
        /// The board, verbatim from the payload. Empty until the first successful fetch or cache load —
        /// <c>RankingsScreenController.RebuildList</c> leaves the previous rows up on an empty list.
        /// </summary>
        public IReadOnlyList<LeaderboardEntry> GetRanking(LeaderboardPeriod period)
        {
            if (!_snapshots.TryGetValue(period, out Snapshot snap)) return Array.Empty<LeaderboardEntry>();

            // The player's own name is overridden on READ rather than at map time: the disk cache is
            // loaded at boot, before sign-in has restored the display name, so baking it in would pin
            // "YOU" onto the player's row for the whole session.
            var result = new List<LeaderboardEntry>(snap.Entries.Count);
            foreach (LeaderboardEntry e in snap.Entries)
                result.Add(e.IsPlayer ? WithLocalName(e) : e);
            return result;
        }

        /// <summary>
        /// The caller's own row. The server sends it on every response — including at score 0 and at a
        /// rank far outside the top slice — so the pinned row never has to be synthesised.
        /// </summary>
        public LeaderboardEntry GetPlayerEntry(LeaderboardPeriod period)
        {
            if (_snapshots.TryGetValue(period, out Snapshot snap) && snap.Player.HasValue)
                return WithLocalName(snap.Player.Value);

            return new LeaderboardEntry
            {
                Rank        = 0,
                IsTie       = false,
                DisplayName = Golfin.Auth.PlayerIdentity.DisplayNameOr("YOU"),
                CharacterId = string.Empty,
                Level       = 1,
                Score       = 0,
                IsPlayer    = true
            };
        }

        /// <summary>
        /// The countdown target, already corrected for device clock skew (see
        /// <see cref="AdjustedPeriodEnd"/>). <see cref="DateTime.MaxValue"/> when the period never
        /// resets or nothing has been fetched yet — <c>UpdateCountdownLabel</c> blanks the label on that.
        /// </summary>
        public DateTime GetPeriodEndUtc(LeaderboardPeriod period)
            => _snapshots.TryGetValue(period, out Snapshot snap) ? snap.PeriodEndUtc : DateTime.MaxValue;

        // ── Refresh ───────────────────────────────────────────────────────────

        /// <summary>
        /// Fire-and-forget refresh of one period. <paramref name="onDone"/> gets true only when a new
        /// snapshot was actually stored; false covers offline, a bad body, AND a duplicate call while
        /// one is already in flight, all of which mean "nothing changed, do not rebuild".
        /// </summary>
        public void Refresh(LeaderboardPeriod period, Action<bool>? onDone = null)
            => ApiClient.Instance.Run(RefreshRoutine(period, onDone));

        /// <summary>
        /// The coroutine behind <see cref="Refresh"/>. Pumped explicitly (rather than
        /// <c>yield return get</c>) to match <c>ApiClient</c>'s own convention, so it also runs under a
        /// plain <c>while (MoveNext())</c> in an EditMode test.
        /// </summary>
        public IEnumerator RefreshRoutine(LeaderboardPeriod period, Action<bool>? onDone = null)
        {
            if (!_inFlight.Add(period))
            {
                onDone?.Invoke(false);
                yield break;
            }

            try
            {
                string url = Endpoints.Leaderboard(WirePeriod(period));
                string? body = null;

                // T = string asks ApiEnvelope for the unwrapped payload verbatim; RawBody still carries
                // the full enveloped body, which is what gets mirrored to disk.
                IEnumerator get = ApiClient.Instance.Get<string>(url, result =>
                {
                    if (result.Success)
                    {
                        body = result.RawBody;
                    }
                    else
                    {
                        // Expected offline. Warning, not error: keeping the cached board is the design.
                        Debug.LogWarning(
                            $"{Tag} {period} fetch failed ({result.ErrorKind}, HTTP {result.StatusCode}): " +
                            $"{result.ErrorMessage}. Keeping the cached board.");
                    }
                });

                while (get.MoveNext()) yield return get.Current;

                if (string.IsNullOrWhiteSpace(body))
                {
                    onDone?.Invoke(false);
                    yield break;
                }

                Snapshot? snap = BuildSnapshot(body!, period, "server");
                if (snap == null)
                {
                    onDone?.Invoke(false);
                    yield break;
                }

                _snapshots[period] = snap;

                // Mirrored AFTER a successful parse, so a body this build cannot read never replaces a
                // cache it can. (RemoteBannerSource caches before mapping because a banner it cannot map
                // is still useful to a later build; a leaderboard the UI cannot render is not.)
                LeaderboardDiskCache.WriteCache(period, body!);

                onDone?.Invoke(true);
            }
            finally
            {
                _inFlight.Remove(period);
            }
        }

        // ── Mapping ───────────────────────────────────────────────────────────

        /// <summary>Parse + map one raw body into a snapshot, or null when it is unusable.</summary>
        private Snapshot? BuildSnapshot(string json, LeaderboardPeriod period, string source)
        {
            LeaderboardResponseDto? dto = Deserialize(json, source);
            if (dto == null) return null;

            return new Snapshot
            {
                Entries      = MapEntries(dto),
                Player       = MapPlayer(dto),
                PeriodEndUtc = AdjustedPeriodEnd(dto.PeriodEndUtc, dto.FetchedAt, _time.UtcNow)
            };
        }

        /// <summary>
        /// Tolerates BOTH shapes on purpose: the live path hands over a body already unwrapped by
        /// <c>ApiEnvelope</c>, while the disk cache holds the raw <c>{"data": …}</c>. Identical to
        /// <c>BannerService.Deserialize</c>, including <c>DateParseHandling.None</c> — without it
        /// Newtonsoft would convert the timestamp strings to LOCAL DateTimes on the way in.
        /// </summary>
        private static LeaderboardResponseDto? Deserialize(string? json, string source)
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

                if (payload.Type == JTokenType.Null) return null;
                return payload.ToObject<LeaderboardResponseDto>(serializer);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"{Tag} Could not parse the {source} leaderboard payload: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Payload rows → <see cref="LeaderboardEntry"/>, field for field. Rank and IsTie are COPIED,
        /// never recomputed — the server owns the ranking (SPEC §1).
        /// </summary>
        private static IReadOnlyList<LeaderboardEntry> MapEntries(LeaderboardResponseDto dto)
        {
            if (dto.Entries == null || dto.Entries.Count == 0) return Array.Empty<LeaderboardEntry>();

            var list = new List<LeaderboardEntry>(dto.Entries.Count);
            foreach (LeaderboardEntryDto? row in dto.Entries)
            {
                if (row == null) continue;
                list.Add(MapEntry(row, row.IsPlayer));
            }
            return list;
        }

        /// <summary>The <c>player</c> object, or null when the payload omitted it.</summary>
        private static LeaderboardEntry? MapPlayer(LeaderboardResponseDto dto)
            => dto.Player == null ? (LeaderboardEntry?)null : MapEntry(dto.Player, true);

        private static LeaderboardEntry MapEntry(LeaderboardEntryDto row, bool isPlayer) => new LeaderboardEntry
        {
            Rank        = row.Rank,
            IsTie       = row.IsTie,
            DisplayName = row.DisplayName ?? string.Empty,
            // A null character_id is normal (PLAYLIFE-only users, never-synced players). Empty string
            // is what the widgets already treat as "use the default portrait".
            CharacterId = row.CharacterId ?? string.Empty,
            Level       = row.Level,
            Score       = row.Score,
            IsPlayer    = isPlayer
        };

        /// <summary>
        /// The player's own row shows the LOCAL display name, not the server's copy of it: a player who
        /// just set a username would otherwise see the stale one until the backend caught up.
        /// </summary>
        private static LeaderboardEntry WithLocalName(LeaderboardEntry e)
        {
            e.DisplayName = Golfin.Auth.PlayerIdentity.DisplayNameOr("YOU");
            return e;
        }

        // ── Countdown ─────────────────────────────────────────────────────────

        /// <summary>
        /// Turn the server's <c>period_end_utc</c> into an end time the countdown can subtract the
        /// DEVICE clock from and still get the server's remaining time.
        ///
        /// The label computes <c>end − NetworkTimeProvider.UtcNow</c>. The truth is
        /// <c>period_end_utc − fetched_at</c>, so the end time is shifted by the skew observed at fetch:
        /// <code>adjusted = period_end_utc + (localNowAtFetch − fetched_at)</code>
        /// A device running ten minutes fast then shows the same remaining time as one running ten
        /// minutes slow, because the shift cancels exactly the error the subtraction reintroduces.
        ///
        /// Historic (<c>period_end_utc: null</c>) → <see cref="DateTime.MaxValue"/>, which
        /// <c>UpdateCountdownLabel</c> renders as a blank label.
        /// </summary>
        internal static DateTime AdjustedPeriodEnd(string? periodEndUtc, string? fetchedAt, DateTime localNowAtFetch)
        {
            DateTime? end = ParseUtc(periodEndUtc);
            if (!end.HasValue) return DateTime.MaxValue;

            DateTime? serverNow = ParseUtc(fetchedAt);
            if (!serverNow.HasValue) return end.Value;   // no reference → trust the timestamp as-is

            TimeSpan skew = localNowAtFetch - serverNow.Value;

            // Guard the arithmetic: a nonsense timestamp must not throw on a cosmetic screen.
            try { return end.Value + skew; }
            catch (ArgumentOutOfRangeException) { return DateTime.MaxValue; }
        }

        /// <summary>
        /// Absolute UTC from an ISO-8601 string, or null. <c>AssumeUniversal</c> covers a server that
        /// drops the offset; <c>AdjustToUniversal</c> normalises <c>+00:00</c> / <c>Z</c> forms to the
        /// same instant. Same parse the banner and tournament schedules use.
        /// </summary>
        internal static DateTime? ParseUtc(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            return DateTime.TryParse(value, CultureInfo.InvariantCulture,
                       DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out DateTime parsed)
                   ? parsed
                   : (DateTime?)null;
        }

        // ── Period naming ─────────────────────────────────────────────────────

        /// <summary>The URL/cache-file spelling: <c>daily|weekly|monthly|historic</c>.</summary>
        public static string WirePeriod(LeaderboardPeriod period) => period switch
        {
            LeaderboardPeriod.Daily    => "daily",
            LeaderboardPeriod.Weekly   => "weekly",
            LeaderboardPeriod.Monthly  => "monthly",
            LeaderboardPeriod.Historic => "historic",
            _                          => "daily"
        };
    }
}
