// gps_gifts_votes §Client data bindings — the gift economy over the EXISTING ApiClient.
// Plain C# singleton in the shape of UserService / PointsService: constructible in an EditMode
// test, no MonoBehaviour, no offline queue.
using System;
using System.Collections;
using System.Collections.Generic;
using Golfin.Net;
using Newtonsoft.Json;

namespace Golfin.Social
{
    /// <summary>
    /// Reads the gift catalog and the caller's received gifts, and performs the two economy
    /// writes the Gift screen can make: an RP send and a self-purchase.
    ///
    /// <para>
    /// BOTH WRITES CARRY AN IDEMPOTENCY KEY, GENERATED HERE. Since
    /// <c>2026_09_02_gift_atomic.sql</c> the server keys the ledger on
    /// <c>(user_id, idempotency_key)</c>, so a request that times out and is retried with the SAME
    /// key moves points once. The key is minted by the CALLER (a modal's confirm button) and held
    /// across retries — minting one per attempt would defeat the whole mechanism, which is why
    /// <see cref="NewKey"/> is public and the send/purchase methods take a key rather than making
    /// one.
    /// </para>
    /// </summary>
    public sealed class GiftService
    {
        private static GiftService _instance;

        public static GiftService Instance =>
            _instance ?? (_instance = new GiftService(ApiClient.Instance));

        public static void ConfigureForTest(GiftService service) => _instance = service;
        public static void ResetForTest() => _instance = null;

        private readonly ApiClient _client;

        public GiftService(ApiClient client) { _client = client; }

        /// <summary>The last catalog the server returned this session, or null.</summary>
        public List<GiftItemDto> LastItems { get; private set; }

        /// <summary>The last supporter aggregation, newest computation wins.</summary>
        public List<SupporterTotal> LastSupporters { get; private set; }

        public event Action OnItemsChanged;
        public event Action OnSupportersChanged;

        /// <summary>A fresh idempotency key. One per logical user action, reused across retries.</summary>
        public static string NewKey() => Guid.NewGuid().ToString();

        // ═════════════════════════════════════════════════════════════════════
        // Catalog
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>GET <c>/gifts/items</c> — the whole active catalog.</summary>
        public IEnumerator Items(Action<ApiResult<List<GiftItemDto>>> onResult = null)
            => _client.Get<List<GiftItemDto>>(Endpoints.GiftsItems, r =>
            {
                if (r != null && r.Success && r.Data != null)
                {
                    LastItems = r.Data;
                    OnItemsChanged?.Invoke();
                }
                onResult?.Invoke(r);
            });

        /// <summary>
        /// The three cells of the BUY GIFT ITEMS strip: <c>basic</c>-tier rows that actually have
        /// an activity price, CHEAPEST FIRST.
        ///
        /// <para>
        /// The sort is client-side and deliberate. The router orders by <c>category</c>, which is
        /// alphabetical and puts a 40-pt wristband ahead of a 30-pt glove — an arbitrary-looking
        /// strip next to a design (14027:102193) whose three cells ascend 50 → 100 → 500. Price
        /// ascending is deterministic, reproduces the design's shape, and does not depend on the
        /// server's ordering staying what it is.
        /// </para>
        /// </summary>
        public static List<GiftItemDto> BuyStrip(List<GiftItemDto> items, int count = 3)
        {
            var picked = new List<GiftItemDto>();
            if (items == null) return picked;

            foreach (GiftItemDto it in items)
            {
                if (it == null || !it.IsActive) continue;
                if (!string.Equals(it.Tier, "basic", StringComparison.OrdinalIgnoreCase)) continue;
                if (!it.PriceActivityPts.HasValue || it.PriceActivityPts.Value <= 0) continue;
                picked.Add(it);
            }

            picked.Sort((a, b) =>
            {
                int c = a.PriceActivityPts.Value.CompareTo(b.PriceActivityPts.Value);
                // Ties broken by id so the strip is stable across fetches rather than
                // depending on whatever order the server happened to return.
                return c != 0 ? c : string.CompareOrdinal(a.Id ?? "", b.Id ?? "");
            });

            if (picked.Count > count) picked.RemoveRange(count, picked.Count - count);
            return picked;
        }

        // ═════════════════════════════════════════════════════════════════════
        // Received gifts + the supporter aggregation
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>GET <c>/gifts/received</c> — one page of ITEM gifts.</summary>
        public IEnumerator Received(int skip, int limit,
                                    Action<ApiResult<List<ReceivedGiftDto>>> onResult)
            => _client.Get(Endpoints.GiftsReceived(skip, limit), onResult);

        /// <summary>GET <c>/points/history?currency=gift</c> — one page of the gift-currency
        /// ledger, which is where RP gifts land.</summary>
        public IEnumerator GiftLedger(int skip, int limit,
                                      Action<ApiResult<List<PointsLedgerRowDto>>> onResult)
            => _client.Get(Endpoints.PointsHistory(skip, limit, "gift"), onResult);

        /// <summary>Page size, and the cap on how far back the supporters panel looks. 4 x 50 is
        /// two hundred gifts, which is far past anything a top-3 panel can be changed by.</summary>
        public const int PageSize = 50;
        public const int MaxPages = 4;

        /// <summary>
        /// Build TOP SUPPORTERS. Pages BOTH sources to <see cref="MaxPages"/>, groups by sender,
        /// sums, and sorts descending.
        ///
        /// <para>
        /// TWO SOURCES, BECAUSE ONE OF THEM IS ALWAYS EMPTY TODAY. <c>/gifts/received</c> reads
        /// the <c>gifts</c> table, and only the ITEM-gifting path (<c>/gifts/send</c>, out of
        /// scope for v1) ever inserts there — it held zero rows in production on 2026-09-02. The
        /// RP send this screen actually performs writes no <c>gifts</c> row at all; it is recorded
        /// only as a <c>gift_received</c> row in <c>points_transactions</c>. Reading just the
        /// endpoint the SPEC names would therefore have shipped a panel that is empty by
        /// construction, so the ledger is read alongside it and the two are merged by name.
        /// </para>
        /// </summary>
        public IEnumerator Supporters(Action<List<SupporterTotal>> onDone)
        {
            var byName = new Dictionary<string, SupporterTotal>(StringComparer.Ordinal);

            // ── item gifts ────────────────────────────────────────────────────
            for (int page = 0; page < MaxPages; page++)
            {
                List<ReceivedGiftDto> rows = null;
                bool failed = false;
                IEnumerator call = Received(page * PageSize, PageSize, r =>
                {
                    if (r != null && r.Success) rows = r.Data;
                    else failed = true;
                });
                while (call.MoveNext()) yield return call.Current;

                if (failed || rows == null || rows.Count == 0) break;

                foreach (ReceivedGiftDto g in rows)
                {
                    if (g == null) continue;
                    string name = g.Sender != null ? g.Sender.DisplayName : null;
                    if (string.IsNullOrWhiteSpace(name)) continue;
                    Add(byName, name, g.SenderId, g.GiftPtsAwarded ?? 0);
                }

                if (rows.Count < PageSize) break;
            }

            // ── RP gifts ──────────────────────────────────────────────────────
            for (int page = 0; page < MaxPages; page++)
            {
                List<PointsLedgerRowDto> rows = null;
                bool failed = false;
                IEnumerator call = GiftLedger(page * PageSize, PageSize, r =>
                {
                    if (r != null && r.Success) rows = r.Data;
                    else failed = true;
                });
                while (call.MoveNext()) yield return call.Current;

                if (failed || rows == null || rows.Count == 0) break;

                foreach (PointsLedgerRowDto row in rows)
                {
                    if (row == null) continue;
                    if (!string.Equals(row.Type, "gift_received", StringComparison.Ordinal)) continue;
                    string name = SupporterName(row.Description);
                    if (string.IsNullOrWhiteSpace(name)) continue;
                    Add(byName, name, null, row.Amount);
                }

                if (rows.Count < PageSize) break;
            }

            var list = new List<SupporterTotal>(byName.Values);
            list.Sort((a, b) =>
            {
                int c = b.Points.CompareTo(a.Points);
                return c != 0 ? c : string.CompareOrdinal(a.DisplayName ?? "", b.DisplayName ?? "");
            });

            LastSupporters = list;
            OnSupportersChanged?.Invoke();
            onDone?.Invoke(list);
        }

        private static void Add(Dictionary<string, SupporterTotal> map, string name,
                                string senderId, int points)
        {
            if (!map.TryGetValue(name, out SupporterTotal t))
            {
                t = new SupporterTotal { DisplayName = name };
                map[name] = t;
            }
            t.Points += points;
            t.GiftCount++;
            if (t.SenderId == null && !string.IsNullOrEmpty(senderId)) t.SenderId = senderId;
        }

        /// <summary>
        /// The sender's name out of a <c>gift_received</c> ledger description.
        ///
        /// <para>
        /// The ledger has no counterparty column, so the name is only ever in the description, and
        /// THREE writers have produced one: the pre-2026-09-02 router wrote
        /// <c>"Gift from {name}"</c>, the item-gift path writes <c>"ギフト受取: {item}"</c>, and
        /// <c>golfin_gift_pts</c> writes <c>"ギフト受取: {name}"</c>. Both prefixes are matched;
        /// anything else returns null and is skipped rather than rendered as a supporter called
        /// "ギフト受取: ポロシャツ（白）".
        /// </para>
        /// </summary>
        public static string SupporterName(string description)
        {
            if (string.IsNullOrWhiteSpace(description)) return null;

            const string en = "Gift from ";
            const string ja = "ギフト受取: ";

            if (description.StartsWith(en, StringComparison.Ordinal))
                return Clean(description.Substring(en.Length));
            if (description.StartsWith(ja, StringComparison.Ordinal))
                return Clean(description.Substring(ja.Length));
            return null;
        }

        private static string Clean(string s)
        {
            s = (s ?? string.Empty).Trim();
            return s.Length == 0 ? null : s;
        }

        // ═════════════════════════════════════════════════════════════════════
        // Writes
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>
        /// POST <c>/gifts/send-pts</c>. <paramref name="idempotencyKey"/> must be held across
        /// retries of the SAME logical send — see the class remarks.
        /// </summary>
        public IEnumerator SendPts(string receiverId, int amount, string idempotencyKey,
                                   Action<ApiResult<GiftSendResultDto>> onResult,
                                   string message = null)
            => _client.Post(Endpoints.GiftsSendPts,
                            BuildSendJson(receiverId, amount, idempotencyKey, message),
                            onResult);

        /// <summary>POST <c>/gifts/purchase</c>. Same key posture as <see cref="SendPts"/>.</summary>
        public IEnumerator Purchase(string itemId, string currency, string idempotencyKey,
                                    Action<ApiResult<GiftPurchaseResultDto>> onResult)
            => _client.Post(Endpoints.GiftsPurchase,
                            BuildPurchaseJson(itemId, currency, idempotencyKey),
                            onResult);

        /// <summary>Public so an EditMode test can pin the wire shape without a transport — the
        /// same seam <c>UserService.BuildUpdateJson</c> uses. Field names are snake_case because
        /// they mirror <c>gifts.py::SendPtsGiftRequest</c>.</summary>
        public static string BuildSendJson(string receiverId, int amount, string key, string message)
            => JsonConvert.SerializeObject(
                new SendBody
                {
                    receiver_id     = receiverId,
                    amount          = amount,
                    message         = string.IsNullOrEmpty(message) ? null : message,
                    idempotency_key = key,
                },
                new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });

        public static string BuildPurchaseJson(string itemId, string currency, string key)
            => JsonConvert.SerializeObject(
                new PurchaseBody
                {
                    item_id         = itemId,
                    currency        = string.IsNullOrEmpty(currency) ? "activity" : currency,
                    idempotency_key = key,
                },
                new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });

        private sealed class SendBody
        {
            public string receiver_id;
            public int    amount;
            public string message;
            public string idempotency_key;
        }

        private sealed class PurchaseBody
        {
            public string item_id;
            public string currency;
            public string idempotency_key;
        }
    }
}
