// Assets/Scripts/UI/Shop/StoreHistoryStore.cs
// store_history §3 — the purchase log, from the SERVER.
//
// `GET /shop/history`, mirrored to disk so the screen has something to draw on a cold open or an
// offline launch, and PREPENDED to after every purchase so the thing the player just bought is
// there without a refetch.
//
// A COPY OF GachaHistoryStore'S SHAPE, ON PURPOSE (SPEC §3: "Copy, don't generalise").
// GachaHistoryStore is untouched. A shared base would have to abstract over the one thing the two
// logs genuinely disagree about — the gacha page FLATTENS (one record per prize of a pull), this
// one does not (a purchase is already one row) — and the abstraction that hides that difference is
// bigger than the ~80 lines it would save.
//
// A FAILED READ KEEPS WHAT IT HAS. The service hands back null rather than an empty page on a
// timeout (see ShopPurchaseService.FetchHistoryRoutine), so an offline open shows the disk mirror
// rather than an empty log that reads as "you have never bought anything".
#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Golfin.Economy;
using UnityEngine;

namespace GolfinRedux.UI.Shop
{
    public static class StoreHistoryStore
    {
        /// <summary>The raw body of the last good <c>/shop/history</c> response, mirrored beside the
        /// content caches. Same shape and same atomic write as <c>GachaHistoryStore</c>'s.</summary>
        private const string CacheFileName = "store_history.json";

        /// <summary>Deserialise WITHOUT Newtonsoft's date handling, so a `string` field holding an
        /// ISO timestamp round-trips verbatim instead of becoming local wall-clock text.
        /// See <c>Golfin.Net.ApiEnvelope.ParseRaw</c> for the full failure this prevents.</summary>
        private static readonly Newtonsoft.Json.JsonSerializerSettings RawDates =
            new Newtonsoft.Json.JsonSerializerSettings
            { DateParseHandling = Newtonsoft.Json.DateParseHandling.None };

        private static List<StoreHistoryRecord>? _records;

        /// <summary>
        /// The log, newest first. Reads the disk mirror on first access — never the network, so a
        /// screen binding in OnEnable draws immediately and <see cref="Refresh"/> updates it when
        /// the server answers.
        /// </summary>
        public static IReadOnlyList<StoreHistoryRecord> All
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
        public static IReadOnlyList<StoreHistoryRecord> Filter(Func<StoreHistoryRecord, bool> predicate)
            => All.Where(predicate).ToList();

        /// <summary>Force a re-read from disk on next access (tests, hot reload).</summary>
        public static void Reload() => _records = null;

        // ── The server read ────────────────────────────────────────────────────

        /// <summary>
        /// Fetch the log from the server and mirror it. Called by
        /// <c>StoreHistoryScreenController.OnEnable</c>; fire-and-forget.
        /// </summary>
        public static void Refresh(Action? done = null)
        {
            ShopPurchaseService.Instance.FetchHistoryAsync(100, page =>
            {
                if (page == null || page.Purchases == null)
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
        /// Put the purchase that just happened at the top of the log, so the screen is current
        /// without a second round trip.
        ///
        /// <para>
        /// It does NOT touch the disk mirror: the mirror is what the server said, and the next
        /// <see cref="Refresh"/> will carry this purchase anyway. Writing a locally-assembled page
        /// into it would make a cold open show a log the server has never confirmed.
        /// </para>
        /// </summary>
        public static void Prepend(StoreHistoryRecord record)
        {
            if (record == null) return;

            var all = new List<StoreHistoryRecord>(All.Count + 1) { record };
            all.AddRange(All);
            _records = all;

            RaiseChanged();
        }

        // ── Mapping ────────────────────────────────────────────────────────────

        /// <summary>Testable seam: one server page → the newest-first record list.</summary>
        internal static List<StoreHistoryRecord> Map(ShopHistoryPage page)
        {
            var records = new List<StoreHistoryRecord>();
            if (page?.Purchases == null) return records;

            // The server already orders newest-first, so mapping in order is what keeps the list
            // newest-first without a second sort.
            foreach (var row in page.Purchases)
            {
                if (row == null) continue;

                // ONE parser, GeneralShopModel's. A category this build cannot name is SKIPPED with
                // a warning rather than defaulted: the default that suggests itself is Club, which
                // is the category with the most machinery behind it and therefore the one that
                // fails most confusingly (see GeneralShopCatalog.ParseCategory's own note).
                ShopCategory? category = GeneralShopCatalog.ParseCategory(row.Category);
                if (category == null)
                {
                    Debug.LogWarning($"[StoreHistoryStore] Purchase '{row.EntryId}' SKIPPED — " +
                                     $"category '{row.Category}' is not one this build can render. " +
                                     "The purchase is recorded server-side; ship a build that " +
                                     "knows the category to see it in the log.");
                    continue;
                }

                records.Add(new StoreHistoryRecord
                {
                    Category     = category.Value,
                    RefId        = row.RefId ?? string.Empty,
                    EntryId      = row.EntryId ?? string.Empty,
                    Amount       = Mathf.Max(1, row.Amount),
                    ChargedRp    = row.ChargedRp,
                    ListRp       = row.ListRp,
                    OnSale       = row.OnSale,
                    PurchasedUtc = row.CreatedAt ?? string.Empty,
                });
            }
            return records;
        }

        // ── The disk mirror ────────────────────────────────────────────────────

        private static string CachePath => Path.Combine(Application.persistentDataPath, CacheFileName);

        private static List<StoreHistoryRecord> LoadFromDisk()
        {
            try
            {
                string path = CachePath;
                if (!File.Exists(path)) return new List<StoreHistoryRecord>();

                // Newtonsoft's default DateParseHandling rewrites an ISO timestamp into a LOCAL
                // DateTime token, so a `string` field receives "09/03/2026 12:26:19" instead of the
                // UTC text that was written. Settings keep the round-trip verbatim.
                var page = Newtonsoft.Json.JsonConvert.DeserializeObject<ShopHistoryPage>(
                    File.ReadAllText(path), RawDates);
                return page != null ? Map(page) : new List<StoreHistoryRecord>();
            }
            catch (Exception ex)
            {
                // A corrupt mirror is an empty log, never a crash — and it is LEFT on disk, the way
                // an unmappable content cache is, in case a later build can read it.
                Debug.LogWarning($"[StoreHistoryStore] Could not read the history mirror: {ex.Message}. " +
                                 "Showing an empty log until the next refresh.");
                return new List<StoreHistoryRecord>();
            }
        }

        /// <summary>Atomic <c>.tmp</c> + replace, so a kill mid-write leaves the previous mirror
        /// intact rather than a half-written file. Same idiom as RemoteContentSource.WriteCache.</summary>
        private static void WriteToDisk(ShopHistoryPage page)
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
                Debug.LogWarning($"[StoreHistoryStore] Could not mirror the history: {ex.Message}");
            }
        }

        private static void RaiseChanged()
        {
            try { OnChanged?.Invoke(); }
            catch (Exception ex) { Debug.LogError($"[StoreHistoryStore] OnChanged subscriber threw: {ex}"); }
        }
    }
}
