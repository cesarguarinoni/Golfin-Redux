// Assets/Scripts/UI/Gacha/GachaHistoryStore.cs
// gacha_client_real_pull §4.5 — the pull log, from the SERVER.
//
// It was twelve hard-coded records. It is now `GET /gacha/history`, mirrored to disk so the screen
// has something to draw on a cold open or an offline launch, and PREPENDED to after every pull so
// the log the player just made is there without a refetch.
//
// ONE RECORD PER PRIZE, not per pull. The screen is a list of things you won, and an x10 that paid
// ten clubs is ten rows — that is what the mock modelled and what the row prefabs are built for.
// The pull's own metadata (banner, ticket type, count, timestamp) is copied onto each of its rows.
//
// A FAILED READ KEEPS WHAT IT HAS. The service hands back null rather than an empty page on a
// timeout (see GachaPullService.FetchHistoryRoutine), so an offline open shows the disk mirror
// rather than an empty log that reads as "you have never pulled".
#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Golfin.Economy;
using UnityEngine;

namespace GolfinRedux.UI.Gacha
{
    public static class GachaHistoryStore
    {
        /// <summary>The raw body of the last good <c>/gacha/history</c> response, mirrored beside
        /// the content caches. Same shape and same atomic write as
        /// <c>RemoteContentSource.WriteCache</c>.</summary>
        private const string CacheFileName = "gacha_history.json";

        /// <summary>Deserialise WITHOUT Newtonsoft's date handling, so a `string` field holding an
        /// ISO timestamp round-trips verbatim instead of becoming local wall-clock text.
        /// See <c>Golfin.Net.ApiEnvelope.ParseRaw</c> for the full failure this prevents.</summary>
        private static readonly Newtonsoft.Json.JsonSerializerSettings RawDates =
            new Newtonsoft.Json.JsonSerializerSettings
            { DateParseHandling = Newtonsoft.Json.DateParseHandling.None };

        private static List<GachaHistoryRecord>? _records;

        /// <summary>
        /// The log, newest first. Reads the disk mirror on first access — never the network, so a
        /// screen binding in OnEnable draws immediately and <see cref="Refresh"/> updates it when
        /// the server answers.
        /// </summary>
        public static IReadOnlyList<GachaHistoryRecord> All
        {
            get
            {
                if (_records == null) _records = LoadFromDisk();
                return _records;
            }
        }

        /// <summary>Raised after <see cref="Refresh"/> or <see cref="Prepend"/> changed the log, so
        /// a screen already on display re-binds.</summary>
        public static event Action? OnChanged;

        /// <summary>Returns records matching the predicate, preserving newest-first order.</summary>
        public static IReadOnlyList<GachaHistoryRecord> Filter(Func<GachaHistoryRecord, bool> predicate)
            => All.Where(predicate).ToList();

        /// <summary>
        /// Internal seam: filter by reward type int value (0=Club,1=Ball,etc.) without
        /// needing to construct a typed delegate. Used by EditMode tests via reflection.
        /// </summary>
        internal static IReadOnlyList<GachaHistoryRecord> FilterByRewardTypeInt(int rewardTypeInt)
            => Filter(r => (int)r.RewardType == rewardTypeInt);

        /// <summary>Force a re-read from disk on next access (tests, hot reload).</summary>
        public static void Reload() => _records = null;

        // ── The server read ────────────────────────────────────────────────────

        /// <summary>
        /// Fetch the log from the server and mirror it. Called by
        /// <c>GachaHistoryScreenController.OnEnable</c>; fire-and-forget.
        /// </summary>
        public static void Refresh(Action? done = null)
        {
            GachaPullService.Instance.FetchHistoryAsync(100, page =>
            {
                if (page == null || page.Pulls == null)
                {
                    // Offline, timed out, or the flag is off. Keep the mirror.
                    done?.Invoke();
                    return;
                }

                _records = Map(page);
                WriteToDisk(page);
                RaiseChanged();
                done?.Invoke();
            });
        }

        /// <summary>
        /// Put the pull that just happened at the top of the log, so the screen is current without
        /// a second round trip.
        ///
        /// <para>
        /// It does NOT touch the disk mirror: the mirror is what the server said, and the next
        /// <see cref="Refresh"/> will carry this pull anyway. Writing a locally-assembled page into
        /// it would make a cold open show a log the server has never confirmed.
        /// </para>
        /// </summary>
        public static void Prepend(GachaPullResult result)
        {
            if (result == null || result.Prizes == null || result.Prizes.Length == 0) return;

            var head = new List<GachaHistoryRecord>(result.Prizes.Length);
            // The pull has no created_at of its own in the response, so the record is stamped with
            // the DEVICE clock. It is display-only and is replaced by the server's timestamp on the
            // next refresh — but it must be a real time, or the row sorts to the bottom of a list
            // that is ordered newest-first.
            string nowUtc = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");

            foreach (var prize in result.Prizes)
                head.Add(ToRecord(prize, result.BannerId, result.TicketType, result.Count, nowUtc));

            var all = new List<GachaHistoryRecord>(head);
            all.AddRange(All);
            _records = all;

            RaiseChanged();
        }

        // ── Mapping ────────────────────────────────────────────────────────────

        /// <summary>Testable seam: one server page → the flat, newest-first record list.</summary>
        internal static List<GachaHistoryRecord> Map(GachaHistoryPage page)
        {
            var records = new List<GachaHistoryRecord>();
            if (page.Pulls == null) return records;

            // The server already orders pulls newest-first and prizes by slot; flattening in that
            // order is what keeps the list newest-first without a second sort.
            foreach (var pull in page.Pulls)
            {
                if (pull?.Prizes == null) continue;
                foreach (var prize in pull.Prizes)
                    records.Add(ToRecord(prize, pull.BannerId, pull.TicketType, pull.PullCount, pull.CreatedAt));
            }
            return records;
        }

        private static GachaHistoryRecord ToRecord(GachaPrizeDto prize, string bannerId,
                                                   int ticketType, int pullCount, string pulledUtc)
            => new GachaHistoryRecord(
                ToRewardType(prize.Kind),
                prize.RefId ?? string.Empty,
                // A DUPLICATE REACHED THE INVENTORY WITH NOTHING, so its quantity is 0 and its RP
                // is what it actually paid. The row renders "+N RP" off DupeRp.
                prize.IsDupe ? 0 : Mathf.Max(1, prize.Quantity),
                bannerId ?? string.Empty,
                (TicketType)ticketType,
                pullCount,
                pulledUtc ?? string.Empty,
                prize.IsDupe ? prize.DupeRp : 0);

        private static GachaRewardType ToRewardType(string? kind) =>
            (kind ?? string.Empty).Trim().ToLowerInvariant() switch
            {
                PrizeRecord.KindBall      => GachaRewardType.Ball,
                PrizeRecord.KindCharacter => GachaRewardType.Character,
                PrizeRecord.KindItem      => GachaRewardType.Item,
                PrizeRecord.KindTicket    => GachaRewardType.Ticket,
                _                         => GachaRewardType.Club,
            };

        // ── The disk mirror ────────────────────────────────────────────────────

        private static string CachePath => Path.Combine(Application.persistentDataPath, CacheFileName);

        private static List<GachaHistoryRecord> LoadFromDisk()
        {
            try
            {
                string path = CachePath;
                if (!File.Exists(path)) return new List<GachaHistoryRecord>();

                // Newtonsoft's default DateParseHandling rewrites an ISO timestamp into a LOCAL DateTime
                // token, so a `string` field receives "09/03/2026 12:26:19" instead of the UTC
                // text that was written. Settings keep the round-trip verbatim. See ApiEnvelope.ParseRaw.
                var page = Newtonsoft.Json.JsonConvert.DeserializeObject<GachaHistoryPage>(
                    File.ReadAllText(path), RawDates);
                return page != null ? Map(page) : new List<GachaHistoryRecord>();
            }
            catch (Exception ex)
            {
                // A corrupt mirror is an empty log, never a crash — and it is LEFT on disk, the way
                // an unmappable content cache is, in case a later build can read it.
                Debug.LogWarning($"[GachaHistoryStore] Could not read the history mirror: {ex.Message}. " +
                                 "Showing an empty log until the next refresh.");
                return new List<GachaHistoryRecord>();
            }
        }

        /// <summary>Atomic <c>.tmp</c> + replace, so a kill mid-write leaves the previous mirror
        /// intact rather than a half-written file. Same idiom as RemoteContentSource.WriteCache.</summary>
        private static void WriteToDisk(GachaHistoryPage page)
        {
            try
            {
                string path = CachePath;
                string tmp  = path + ".tmp";
                File.WriteAllText(tmp, Newtonsoft.Json.JsonConvert.SerializeObject(page));

                if (File.Exists(path)) File.Replace(tmp, path, null);
                else File.Move(tmp, path);
            }
            catch (Exception ex)
            {
                // The log is already in memory and on screen; failing to mirror it costs the next
                // cold open, not this one.
                Debug.LogWarning($"[GachaHistoryStore] Could not mirror the history: {ex.Message}");
            }
        }

        private static void RaiseChanged()
        {
            try { OnChanged?.Invoke(); }
            catch (Exception ex) { Debug.LogError($"[GachaHistoryStore] OnChanged subscriber threw: {ex}"); }
        }
    }
}
