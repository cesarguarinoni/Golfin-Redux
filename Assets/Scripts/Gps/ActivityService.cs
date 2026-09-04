// ─────────────────────────────────────────────────────────────────────────────
// gps_checkin §C2 — the check-in half of the GPS module, over the EXISTING
// ApiClient. Same shape as VenueService: a plain C# singleton, constructible in
// an EditMode test, no MonoBehaviour, no queue.
//
// EVERY WRITE CARRIES AN IDEMPOTENCY KEY, and the key is MINTED AND PERSISTED BY
// THE CALLER before the request leaves (RoundSession §C3), not by this class.
// That distinction is the whole point: a key generated here would be lost with
// the process, so a force-quit between "request sent" and "response received"
// would retry with a NEW key and open a second round — which the server would
// then refuse as `already_active`, leaving the player with a round they cannot
// see and cannot close. A persisted key replays into the same row.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using Golfin.Net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Golfin.Gps
{
    /// <summary>
    /// <c>/activity/*</c> — open a round, close a round, read the open one, page the history.
    /// </summary>
    public sealed class ActivityService
    {
        private static ActivityService? _instance;

        public static ActivityService Instance =>
            _instance ?? (_instance = new ActivityService(ApiClient.Instance));

        public static void ConfigureForTest(ActivityService service) => _instance = service;

        public static void ResetForTest() => _instance = null;

        private readonly ApiClient _client;

        public ActivityService(ApiClient client) { _client = client; }

        /// <summary>
        /// <c>POST /activity/checkin</c>.
        ///
        /// <para><paramref name="fix"/> may be null — a check-in with no fix is legal, opens the
        /// round, and awards NOTHING, because the server decides <c>gps_verified</c> from the
        /// venue's own coordinates and cannot verify a position it was not given. The client's
        /// own distance check gates the BUTTON (D1); this is what actually pays.</para>
        /// </summary>
        public IEnumerator CheckIn(int venueId, LocationFix? fix, string idempotencyKey,
                                   Action<ApiResult<CheckInResult>> onResult)
        {
            GpsTrustSignals signals = GpsTrustSignals.CaptureDefault();
            var body = new JObject
            {
                ["venue_id"] = venueId,
                ["idempotency_key"] = idempotencyKey,
                // The SAME two anti-cheat signals a score submit carries (gps_trust_core §3).
                // Captured per call rather than cached: a player who switches on a mock-location
                // app mid-session must not keep a "real device" verdict from launch.
                ["client_platform"] = signals.ClientPlatform,
                ["gps_is_mock"] = signals.IsMock,
            };
            if (fix != null)
            {
                body["latitude"] = fix.Lat;
                body["longitude"] = fix.Lon;
                body["accuracy_m"] = fix.AccuracyM;
            }
            return _client.Post(Endpoints.ActivityCheckin, body.ToString(Formatting.None), onResult);
        }

        /// <summary>
        /// <c>POST /activity/{id}/checkout</c>.
        ///
        /// <para><paramref name="checkCount"/> is the session tracker's count, and the server takes
        /// the MAX of it and what the row already holds — so a client that under-counts cannot
        /// erase evidence the round accumulated, and one that over-counts still has to have been
        /// inside the radius at both ends to be paid the +5.</para>
        /// </summary>
        public IEnumerator CheckOut(long activityId, LocationFix? fix, int checkCount,
                                    string idempotencyKey,
                                    Action<ApiResult<CheckOutResult>> onResult)
        {
            var body = new JObject
            {
                ["idempotency_key"] = idempotencyKey,
                ["gps_check_count"] = checkCount,
                ["gps_is_mock"] = GpsTrustSignals.CaptureDefault().IsMock,
            };
            if (fix != null)
            {
                body["latitude"] = fix.Lat;
                body["longitude"] = fix.Lon;
            }
            return _client.Post(Endpoints.ActivityCheckout(activityId.ToString()),
                                body.ToString(Formatting.None), onResult);
        }

        /// <summary>
        /// <c>GET /activity/active</c> → the caller's open round, or null.
        ///
        /// <para>A SUCCESS WITH <c>Data == null</c> MEANS "no round open" — it is a 200
        /// <c>{"data": null}</c>, not a failure. Branch on Data, never on Success (the same trap
        /// <see cref="VenueAutoRegisterResult"/> documents).</para>
        /// </summary>
        public IEnumerator Active(Action<ApiResult<ActivityDto>> onResult)
            => _client.Get(Endpoints.ActivityActive, onResult);

        /// <summary><c>GET /activity/history</c> — the caller's check-in ledger, newest first.
        /// Distinct from <c>/score/history</c>, which is the SCORE ledger.</summary>
        public IEnumerator History(int skip, int limit, Action<ApiResult<List<ActivityDto>>> onResult)
            => _client.Get(Endpoints.ActivityHistory(skip, limit), onResult);

        // ═════════════════════════════════════════════════════════════════════
        // Refusals
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>
        /// The <c>reason</c> out of a refused call's body, or null.
        ///
        /// <para>The routers raise the RPC's refusal object as the FastAPI <c>detail</c>, so a
        /// 409 body is <c>{"detail":{"ok":false,"reason":"already_active","activity_id":42}}</c>.
        /// This is the ONE place that shape is parsed: every caller asks for the reason by name
        /// rather than string-matching an error message, which would break the first time a
        /// message is reworded.</para>
        /// </summary>
        public static string? ReasonOf(string? body)
        {
            JObject? detail = RefusalObject(body);
            return detail?["reason"]?.ToString();
        }

        /// <summary>The <c>activity_id</c> an <c>already_active</c> refusal names, so the screen
        /// can show THAT round instead of asking the player to guess.</summary>
        public static long? ActiveIdOf(string? body)
        {
            JToken? id = RefusalObject(body)?["activity_id"];
            if (id == null || id.Type == JTokenType.Null) return null;
            // A server that answers `"activity_id": "42"` — or anything unparseable — must not
            // take the failure handler down with it.
            return long.TryParse(id.ToString(), out long v) ? v : (long?)null;
        }

        /// <summary>
        /// The refusal object inside an error body, or null when there isn't one.
        ///
        /// <para>⚠️ <c>detail</c> IS NOT ALWAYS AN OBJECT, and assuming it was is a real crash this
        /// project's own routers can cause: FastAPI writes <c>{"detail": "..."}</c> with a PLAIN
        /// STRING for every <c>HTTPException(400, detail="…")</c>, and <c>activity.py::_key</c>
        /// raises exactly that for a malformed idempotency key. Indexing a
        /// <see cref="JValue"/> throws <see cref="InvalidOperationException"/> — which is NOT a
        /// <see cref="JsonException"/>, so the old catch missed it, the exception escaped into the
        /// check-in coroutine, and the coroutine died with <c>PendingSpend</c> still holding the
        /// button. A stuck "…" on CHECK IN, from an error path.</para>
        ///
        /// <para>So the type is CHECKED rather than assumed, and the catch is broad: this runs only
        /// on a path that has already failed, where throwing again can only make things worse.</para>
        /// </summary>
        private static JObject? RefusalObject(string? body)
        {
            if (string.IsNullOrEmpty(body)) return null;
            try
            {
                if (!(JToken.Parse(body!) is JObject root)) return null;
                JToken? detail = root["detail"];
                return detail as JObject ?? (detail == null ? root : null);
            }
            catch (JsonException)
            {
                return null;
            }
        }
    }
}
