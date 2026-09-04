// gps_checkin §C2 — the check-in/check-out wire format, against the shapes
// 2026_09_03_venue_partners.sql actually returns.
//
// EVERY FIXTURE BELOW IS THE RPC's OWN json_build_object, transcribed field for field. A DTO that
// parses a shape nobody sends is worse than no DTO: it turns a wrong payload into a silently
// zeroed one, and `awarded: 0` reads exactly like "the server paid nothing".
using System.Collections.Generic;
using Golfin.Net;
using Golfin.Net.Tests;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Golfin.Gps.Tests
{
    public class ActivityServiceJsonTests
    {
        const string CheckInOk =
            "{\"data\":{\"ok\":true,\"replayed\":false," +
            "\"activity\":{\"id\":42,\"user_id\":\"u-1\",\"venue_id\":1993," +
            "\"venue_name\":\"TEST Office (WeWork Harumi)\",\"sport_type\":\"golf\"," +
            "\"check_in_at\":\"2026-09-03T08:12:00+00:00\",\"status\":\"active\"," +
            "\"gps_verified\":true,\"gps_check_count\":1,\"trust_level\":30,\"points\":0}," +
            "\"awarded\":30,\"gps_verified\":true,\"distance_m\":41.2,\"radius_m\":500," +
            "\"activity_pts\":6930,\"total_points\":6980}}";

        const string CheckOutOk =
            "{\"data\":{\"ok\":true,\"replayed\":false," +
            "\"activity\":{\"id\":42,\"status\":\"completed\",\"duration\":\"1h 24m\"," +
            "\"gps_check_count\":7,\"points\":15}," +
            "\"awarded\":15,\"expired\":false,\"gps_verified\":true,\"duration\":\"1h 24m\"," +
            "\"elapsed_seconds\":5040.0,\"activity_pts\":6945,\"total_points\":6995," +
            "\"activities_count\":13}}";

        // ── CheckIn ───────────────────────────────────────────────────────────

        [Test]
        public void CheckIn_PostsToTheRightUrl_WithTheKeyAndTheFix()
        {
            var transport = new FakeHttpTransport().Enqueue(HttpResponse.Status(200, CheckInOk));
            var svc = new ActivityService(GpsTestApi.Client(transport));

            var fix = new LocationFix { Lat = 35.654103, Lon = 139.779219, AccuracyM = 8f };
            ApiResult<CheckInResult> result = null;
            Pump.Drain(svc.CheckIn(1993, fix, "11111111-2222-3333-4444-555555555555", r => result = r));

            Assert.AreEqual(Endpoints.ActivityCheckin, transport.SentUrls[0]);
            Assert.AreEqual("POST", transport.SentMethods[0]);

            JObject body = JObject.Parse(transport.SentBodies[0]);
            Assert.AreEqual(1993, (int)body["venue_id"]);
            Assert.AreEqual("11111111-2222-3333-4444-555555555555", (string)body["idempotency_key"]);
            Assert.AreEqual(35.654103, (double)body["latitude"], 1e-9);
            Assert.AreEqual(139.779219, (double)body["longitude"], 1e-9);
            Assert.AreEqual(8f, (float)body["accuracy_m"], 1e-4f);
            // The two anti-cheat signals a score submit carries, on this path too.
            Assert.IsNotNull(body["client_platform"]);
            Assert.IsNotNull(body["gps_is_mock"]);

            Assert.IsTrue(result.Success);
        }

        [Test]
        public void CheckIn_WithNoFix_OmitsTheCoordinates_RatherThanSendingZeros()
        {
            // (0, 0) is a real place in the Gulf of Guinea. Sending it would make the server
            // compute a distance to it and refuse the +30 for a reason nobody could debug.
            var transport = new FakeHttpTransport().Enqueue(HttpResponse.Status(200, CheckInOk));
            var svc = new ActivityService(GpsTestApi.Client(transport));

            Pump.Drain(svc.CheckIn(1993, null, "k", _ => { }));

            JObject body = JObject.Parse(transport.SentBodies[0]);
            Assert.IsNull(body["latitude"]);
            Assert.IsNull(body["longitude"]);
            Assert.IsNull(body["accuracy_m"]);
        }

        [Test]
        public void CheckIn_UnwrapsEveryFieldTheRpcReturns()
        {
            var transport = new FakeHttpTransport().Enqueue(HttpResponse.Status(200, CheckInOk));
            ApiResult<CheckInResult> result = null;
            Pump.Drain(new ActivityService(GpsTestApi.Client(transport))
                       .CheckIn(1993, null, "k", r => result = r));

            CheckInResult d = result.Data;
            Assert.IsTrue(d.Ok);
            Assert.IsFalse(d.Replayed);
            Assert.AreEqual(30, d.Awarded);
            Assert.IsTrue(d.GpsVerified);
            Assert.AreEqual(41.2, d.DistanceM.Value, 1e-6);
            Assert.AreEqual(500, d.RadiusM.Value);
            Assert.AreEqual(6930, d.ActivityPts.Value);
            Assert.AreEqual(6980, d.TotalPoints.Value);

            Assert.IsNotNull(d.Activity);
            Assert.AreEqual(42, d.Activity.Id);
            Assert.AreEqual("active", d.Activity.Status);
            Assert.AreEqual(1993, d.Activity.VenueId.Value);
            Assert.AreEqual(1, d.Activity.GpsCheckCount.Value);
        }

        [Test]
        public void CheckIn_Replay_ReportsReplayedAndAwardsNothing()
        {
            const string replay =
                "{\"data\":{\"ok\":true,\"replayed\":true," +
                "\"activity\":{\"id\":42,\"status\":\"active\"},\"awarded\":0," +
                "\"gps_verified\":true,\"activity_pts\":6930,\"total_points\":6980}}";
            var transport = new FakeHttpTransport().Enqueue(HttpResponse.Status(200, replay));
            ApiResult<CheckInResult> result = null;
            Pump.Drain(new ActivityService(GpsTestApi.Client(transport))
                       .CheckIn(1993, null, "k", r => result = r));

            Assert.IsTrue(result.Data.Replayed);
            Assert.AreEqual(0, result.Data.Awarded,
                "counting up by nothing would tell the player they earned twice");
        }

        // ── Refusals ──────────────────────────────────────────────────────────

        [Test]
        public void AlreadyActive_ArrivesAsAFailureWhoseReasonIsReadable()
        {
            // The router raises the RPC's refusal object as FastAPI's `detail`, so the client sees
            // a 409 with the reason in the BODY — never as a success.
            const string refusal =
                "{\"detail\":{\"ok\":false,\"reason\":\"already_active\",\"activity_id\":42}}";
            var transport = new FakeHttpTransport().Enqueue(HttpResponse.Status(409, refusal));
            ApiResult<CheckInResult> result = null;
            Pump.Drain(new ActivityService(GpsTestApi.Client(transport))
                       .CheckIn(1993, null, "k", r => result = r));

            Assert.IsFalse(result.Success);
            Assert.AreEqual(409, result.StatusCode);
            Assert.AreEqual("already_active", ActivityService.ReasonOf(result.RawBody));
            Assert.AreEqual(42L, ActivityService.ActiveIdOf(result.RawBody).Value);
        }

        [Test]
        public void ReasonOf_ReadsBothTheWrappedAndTheBareShape()
        {
            Assert.AreEqual("venue_not_found",
                ActivityService.ReasonOf("{\"detail\":{\"reason\":\"venue_not_found\"}}"));
            Assert.AreEqual("venue_not_found",
                ActivityService.ReasonOf("{\"reason\":\"venue_not_found\"}"));
        }

        [Test]
        public void ReasonOf_ReturnsNull_ForAnythingItCannotRead()
        {
            // Never throws: this runs on the failure path, where throwing would replace a toast
            // with a crash.
            Assert.IsNull(ActivityService.ReasonOf(null));
            Assert.IsNull(ActivityService.ReasonOf(""));
            Assert.IsNull(ActivityService.ReasonOf("<html>502 Bad Gateway</html>"));
            Assert.IsNull(ActivityService.ReasonOf("{\"detail\":\"plain string detail\"}"));
            Assert.IsNull(ActivityService.ActiveIdOf("{\"detail\":{\"reason\":\"x\"}}"));
        }

        // ── CheckOut ──────────────────────────────────────────────────────────

        [Test]
        public void CheckOut_PostsToTheActivitysOwnUrl()
        {
            var transport = new FakeHttpTransport().Enqueue(HttpResponse.Status(200, CheckOutOk));
            var fix = new LocationFix { Lat = 35.654103, Lon = 139.779219, AccuracyM = 8f };
            Pump.Drain(new ActivityService(GpsTestApi.Client(transport))
                       .CheckOut(42, fix, 7, "kk", _ => { }));

            Assert.AreEqual(Endpoints.ActivityCheckout("42"), transport.SentUrls[0]);
            JObject body = JObject.Parse(transport.SentBodies[0]);
            Assert.AreEqual("kk", (string)body["idempotency_key"]);
            Assert.AreEqual(7, (int)body["gps_check_count"]);
            Assert.AreEqual(35.654103, (double)body["latitude"], 1e-9);
        }

        [Test]
        public void CheckOut_UnwrapsTheServersElapsedAndAward()
        {
            var transport = new FakeHttpTransport().Enqueue(HttpResponse.Status(200, CheckOutOk));
            ApiResult<CheckOutResult> result = null;
            Pump.Drain(new ActivityService(GpsTestApi.Client(transport))
                       .CheckOut(42, null, 7, "kk", r => result = r));

            CheckOutResult d = result.Data;
            Assert.AreEqual(15, d.Awarded);
            Assert.IsFalse(d.Expired);
            Assert.IsTrue(d.GpsVerified);
            Assert.AreEqual("1h 24m", d.Duration);
            Assert.AreEqual(5040.0, d.ElapsedSeconds.Value, 1e-6);
            Assert.AreEqual(13, d.ActivitiesCount.Value);
            Assert.AreEqual("completed", d.Activity.Status);
            Assert.AreEqual(7, d.Activity.GpsCheckCount.Value);
        }

        [Test]
        public void CheckOut_Expired_PaysNothingAndSaysSo()
        {
            const string expired =
                "{\"data\":{\"ok\":true,\"replayed\":false," +
                "\"activity\":{\"id\":42,\"status\":\"expired\",\"points\":0}," +
                "\"awarded\":0,\"expired\":true,\"gps_verified\":false,\"duration\":\"9h 2m\"," +
                "\"elapsed_seconds\":32520.0}}";
            var transport = new FakeHttpTransport().Enqueue(HttpResponse.Status(200, expired));
            ApiResult<CheckOutResult> result = null;
            Pump.Drain(new ActivityService(GpsTestApi.Client(transport))
                       .CheckOut(42, null, 1, "kk", r => result = r));

            Assert.IsTrue(result.Data.Expired);
            Assert.AreEqual(0, result.Data.Awarded);
            Assert.AreEqual("expired", result.Data.Activity.Status);
        }

        // ── Active / history ──────────────────────────────────────────────────

        [Test]
        public void Active_WithNoOpenRound_IsASuccessWithNullData()
        {
            // THE TRAP THIS PINS: branch on Data, never on Success. A 200 {"data": null} is the
            // legitimate "no round open", and treating it as a failure would keep a stale card up.
            var transport = new FakeHttpTransport().Enqueue(HttpResponse.Status(200, "{\"data\":null}"));
            ApiResult<ActivityDto> result = null;
            Pump.Drain(new ActivityService(GpsTestApi.Client(transport)).Active(r => result = r));

            Assert.IsTrue(result.Success);
            Assert.IsNull(result.Data);
            Assert.AreEqual(Endpoints.ActivityActive, transport.SentUrls[0]);
        }

        [Test]
        public void History_UnwrapsTheArray_AndCarriesTheDuration()
        {
            var transport = new FakeHttpTransport().Enqueue(HttpResponse.Status(200,
                "{\"data\":[{\"id\":42,\"venue_name\":\"TEST Office\",\"status\":\"completed\"," +
                "\"duration\":\"1h 24m\",\"points\":15}," +
                "{\"id\":41,\"venue_name\":\"Kasumigaseki\",\"status\":\"completed\"}]}"));
            ApiResult<List<ActivityDto>> result = null;
            Pump.Drain(new ActivityService(GpsTestApi.Client(transport))
                       .History(0, 3, r => result = r));

            Assert.AreEqual(Endpoints.ActivityHistory(0, 3), transport.SentUrls[0]);
            Assert.AreEqual(2, result.Data.Count);
            Assert.AreEqual("1h 24m", result.Data[0].Duration);
            Assert.IsNull(result.Data[1].Duration, "a sparse row must parse, not throw");
        }
    }

    /// <summary>
    /// A timestamp must reach the DTO EXACTLY as the server wrote it.
    ///
    /// <para>Newtonsoft's default <c>DateParseHandling.DateTime</c> rewrites any ISO-8601-looking
    /// string into a DateTime token in the DEVICE'S LOCAL ZONE, and a <c>string</c> field then
    /// receives that token's <c>ToString()</c>. On a JST device the server's
    /// <c>2026-09-03T03:26:19+00:00</c> arrived as <c>09/03/2026 12:26:19</c> — local wall clock,
    /// US format, no offset — which <c>ParseTimestamp</c> then read as UTC and shifted a SECOND
    /// time. A round checked in at 12:26 JST rendered "Since 21:26", and Elapsed went negative.</para>
    ///
    /// <para>These assertions are timezone-independent on purpose: they pin the verbatim string and
    /// the absolute instant, so the test fails on a UTC build machine too.</para>
    /// </summary>
    public class ActivityTimestampFidelityTests
    {
        const string Iso = "2026-09-03T03:26:19.123456+00:00";

        [Test]
        public void CheckInAt_ReachesTheDto_Verbatim()
        {
            string body = "{\"data\":{\"id\":43,\"check_in_at\":\"" + Iso + "\",\"status\":\"active\"}}";

            Assert.IsTrue(ApiEnvelope.TryUnwrap(body, out ActivityDto dto, out string err), err);
            Assert.AreEqual(Iso, dto.CheckInAt,
                "the envelope rewrote the timestamp — DateParseHandling.None has been lost");
        }

        [Test]
        public void ElapsedIsMeasuredFromTheInstantTheServerMeant()
        {
            string body = "{\"data\":{\"id\":43,\"check_in_at\":\"" + Iso + "\",\"status\":\"active\"}}";
            ApiEnvelope.TryUnwrap(body, out ActivityDto dto, out _);

            // Ten minutes after the server's instant, through the PUBLIC surface the card reads.
            var now = new System.DateTimeOffset(2026, 9, 3, 3, 36, 19, System.TimeSpan.Zero);
            var session = new RoundSession(new InMemoryKeyValueStore(), () => now);
            session.SetActive(dto);

            Assert.AreEqual(10d, session.Elapsed.TotalMinutes, 0.01d,
                "elapsed drifted — the timestamp was shifted by the device's UTC offset");
        }
    }
}
