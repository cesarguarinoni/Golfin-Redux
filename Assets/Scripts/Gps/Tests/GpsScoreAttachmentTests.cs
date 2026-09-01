// Order: gps_trust_core §Tests 10-15 — the orchestrator degrades at every step and never aborts.
using System.Collections;
using System.Collections.Generic;
using Golfin.Net;
using Golfin.Net.Tests;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Golfin.Gps.Tests
{
    public class GpsScoreAttachmentTests
    {
        private const double Lat = 35.681236;
        private const double Lon = 139.767125;

        private const string VenueOk =
            "{\"data\":{\"venue_id\":42,\"name\":\"Tokyo GC\",\"distance_m\":12.5,\"created\":false}}";

        private const string NoCourseNearby =
            "{\"data\":null,\"message\":\"No golf course found nearby. Fall back to manual selection.\"}";

        private static GpsScoreAttachment Run(ILocationProvider location, GpsSessionTracker tracker,
                                              VenueService venues, float? timeout = null)
        {
            GpsScoreAttachment captured = null;
            IEnumerator routine = timeout.HasValue
                ? GpsScoreAttachment.Capture(location, tracker, GpsTestApi.Signals(), venues, a => captured = a, timeout.Value)
                : GpsScoreAttachment.Capture(location, tracker, GpsTestApi.Signals(), venues, a => captured = a);
            Pump.Drain(routine);
            Assert.IsNotNull(captured, "Capture must always invoke onDone exactly once");
            return captured;
        }

        // ── 10. happy path ─────────────────────────────────────────────────────────

        [Test]
        public void Capture_HappyPath_EmitsEveryFieldAndSendsOnlyTheTwoCoordinates()
        {
            var transport = new FakeHttpTransport().Enqueue(HttpResponse.Status(200, VenueOk));
            var location = new FakeLocationProvider().EnqueueFix(Lat, Lon);
            var tracker = GpsTestApi.Tracker(new FakeClock(1_000_000_000));

            GpsScoreAttachment a = Run(location, tracker, new VenueService(GpsTestApi.Client(transport)));
            JObject json = a.ToJson();

            Assert.AreEqual(42, a.VenueId);
            Assert.AreEqual("Tokyo GC", a.VenueName);
            Assert.AreEqual(12.5, a.VenueDistanceM.Value, 1e-9);

            Assert.AreEqual(11, json.Count, "key set: " + string.Join(",", KeysOf(json)));
            Assert.IsTrue(json["gps_verified"].Value<bool>());
            Assert.AreEqual(Lat, json["latitude"].Value<double>(), 1e-9);
            Assert.AreEqual(Lon, json["longitude"].Value<double>(), 1e-9);
            Assert.AreEqual(42, json["venue_id"].Value<int>());
            Assert.IsFalse(json["gps_is_mock"].Value<bool>());
            Assert.AreEqual("editor", json["client_platform"].Value<string>());
            Assert.AreEqual(1, json["gps_check_count"].Value<int>(), "a fresh store means this submit is fix #1");
            Assert.AreEqual(Lat, json["gps_start_lat"].Value<double>(), 1e-9);
            Assert.AreEqual(Lon, json["gps_start_lon"].Value<double>(), 1e-9);
            Assert.AreEqual(Lat, json["gps_end_lat"].Value<double>(), 1e-9);
            Assert.AreEqual(Lon, json["gps_end_lon"].Value<double>(), 1e-9);

            Assert.AreEqual(1, transport.CallCount);
            Assert.AreEqual("POST", transport.SentMethods[0]);
            Assert.AreEqual(Endpoints.VenueAutoRegister, transport.SentUrls[0]);

            var body = JObject.Parse(transport.SentBodies[0]);
            Assert.AreEqual(2, body.Count, "only latitude+longitude — radius_m/language_code keep server defaults; sent: " + transport.SentBodies[0]);
            Assert.AreEqual(Lat, body["latitude"].Value<double>(), 1e-9);
            Assert.AreEqual(Lon, body["longitude"].Value<double>(), 1e-9);
        }

        // ── 11. no course nearby is a 200, not an error ────────────────────────────

        [Test]
        public void Capture_NoCourseNearby_KeepsCoordinatesAndReportsUnverified()
        {
            var transport = new FakeHttpTransport().Enqueue(HttpResponse.Status(200, NoCourseNearby));
            var location = new FakeLocationProvider().EnqueueFix(Lat, Lon);

            GpsScoreAttachment a = Run(location, GpsTestApi.Tracker(new FakeClock(1_000_000_000)),
                                       new VenueService(GpsTestApi.Client(transport)));
            JObject json = a.ToJson();

            Assert.IsNull(a.VenueId);
            Assert.IsFalse(json["gps_verified"].Value<bool>());
            Assert.IsFalse(json.ContainsKey("venue_id"));
            Assert.AreEqual(Lat, json["latitude"].Value<double>(), 1e-9, "coordinates still go up");
            Assert.AreEqual(Lon, json["longitude"].Value<double>(), 1e-9);
            Assert.AreEqual(1, json["gps_check_count"].Value<int>());
        }

        // ── 12. no fix at all ──────────────────────────────────────────────────────

        [Test]
        public void Capture_LocationFailed_MakesNoHttpCallAndEmitsThreeKeys()
        {
            var transport = new FakeHttpTransport();
            var location = new FakeLocationProvider().EnqueueFailure(LocationFailReason.Timeout);

            GpsScoreAttachment a = Run(location, GpsTestApi.Tracker(new FakeClock(1_000_000_000)),
                                       new VenueService(GpsTestApi.Client(transport)));
            JObject json = a.ToJson();

            Assert.AreEqual(0, transport.CallCount, "nothing to search on, so nothing is asked");
            Assert.AreEqual(LocationFailReason.Timeout, a.PositionFailReason);
            Assert.IsNull(a.Session, "no fix ⇒ no session trace");
            Assert.AreEqual(3, json.Count, "key set: " + string.Join(",", KeysOf(json)));
            Assert.IsFalse(json["gps_verified"].Value<bool>());
            Assert.IsTrue(json.ContainsKey("gps_is_mock"));
            Assert.IsTrue(json.ContainsKey("client_platform"));
            Assert.AreEqual("GPS_ERR_TIMEOUT", LocationFailReasonKeys.For(a.PositionFailReason));
        }

        // ── 13. auto-register 500 ──────────────────────────────────────────────────

        [Test]
        public void Capture_AutoRegisterServerError_StillCarriesTheSessionTrace()
        {
            var transport = new FakeHttpTransport();
            transport.Fallback = HttpResponse.Status(500, "{\"detail\":\"boom\"}");
            var location = new FakeLocationProvider().EnqueueFix(Lat, Lon);

            GpsScoreAttachment a = Run(location, GpsTestApi.Tracker(new FakeClock(1_000_000_000)),
                                       new VenueService(GpsTestApi.Client(transport)));
            JObject json = a.ToJson();

            Assert.AreEqual(1, transport.CallCount, "500 is not transient — the retry budget is for 408/connection only");
            Assert.IsNull(a.VenueId);
            Assert.IsFalse(json["gps_verified"].Value<bool>());
            Assert.IsNotNull(a.Session);
            Assert.AreEqual(1, json["gps_check_count"].Value<int>());
            Assert.IsTrue(json.ContainsKey("gps_start_lat"));
        }

        // ── 14. the attachment path's own timeout ──────────────────────────────────

        [Test]
        public void Capture_AsksTheProviderForFiveSecondsNotTen()
        {
            var location = new FakeLocationProvider().EnqueueFailure(LocationFailReason.Timeout);

            Run(location, GpsTestApi.Tracker(new FakeClock(0)), new VenueService(GpsTestApi.Client(new FakeHttpTransport())));

            Assert.AreEqual(5f, location.LastRequestedTimeout, 1e-6,
                "the notifier's 10 s is a different path — " + UnityLocationProvider.DefaultTimeoutSeconds + " s");
            Assert.AreEqual(5f, GpsScoreAttachment.DefaultTimeoutSeconds);
        }

        // ── 15. a second capture in the same session counts ────────────────────────

        [Test]
        public void Capture_TwiceElevenMinutesApart_CountsTwo()
        {
            var transport = new FakeHttpTransport().Enqueue(
                HttpResponse.Status(200, VenueOk),
                HttpResponse.Status(200, VenueOk));
            var venues = new VenueService(GpsTestApi.Client(transport));
            var clock = new FakeClock(1_000_000_000);
            var tracker = GpsTestApi.Tracker(clock);

            Run(new FakeLocationProvider().EnqueueFix(Lat, Lon), tracker, venues);
            clock.AdvanceMinutes(11);
            GpsScoreAttachment second = Run(new FakeLocationProvider().EnqueueFix(Lat, Lon), tracker, venues);

            Assert.AreEqual(2, second.ToJson()["gps_check_count"].Value<int>());
        }

        [Test]
        public void Capture_WithNoSeamsAtAllStillProducesAnAttachment()
        {
            GpsScoreAttachment captured = null;
            Pump.Drain(GpsScoreAttachment.Capture(null, null, null, null, a => captured = a));

            Assert.IsNotNull(captured);
            Assert.AreEqual(3, captured.ToJson().Count);
            Assert.AreEqual(LocationFailReason.Unknown, captured.PositionFailReason);
        }

        private static List<string> KeysOf(JObject o)
        {
            var keys = new List<string>();
            foreach (var p in o.Properties()) keys.Add(p.Name);
            return keys;
        }
    }
}
