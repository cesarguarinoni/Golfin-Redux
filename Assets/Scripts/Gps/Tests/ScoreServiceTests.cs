// Order: score_upload_flow §1 Tests — the two-owner merge, the error mapping, the in-flight latch.
using System.Collections;
using System.Collections.Generic;
using Golfin.Net;
using Golfin.Net.Tests;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Golfin.Gps.Tests
{
    public class ScoreServiceTests
    {
        private const string OkBody =
            "{\"data\":{\"points_earned\":80,\"trust\":80,\"gps_verified\":true,\"gps_distance_m\":42.5," +
            "\"avatar_level\":3,\"leveled_up\":false,\"newly_earned_badges\":[]," +
            "\"activity\":{\"id\":991,\"venue_name\":\"Tokyo Golf Club\",\"score\":92,\"score_type\":\"18\"}}}";

        private static ScoreSubmitRequest Request() => new ScoreSubmitRequest
        {
            Score = 92,
            ScoreType = "18",
            CourseName = "Tokyo Golf Club",
            InputMethod = "screenshot"
        };

        /// <summary>An attachment with a fix, a venue and a 3-fix session — the shape that produces
        /// all eleven GPS keys.</summary>
        private static GpsScoreAttachment Attachment(bool isMock = false)
        {
            var clock = new FakeClock(1_700_000_000_000);
            GpsSessionTracker tracker = GpsTestApi.Tracker(clock);
            tracker.RecordFix(35.6, 139.7);
            clock.AdvanceMinutes(30);
            tracker.RecordFix(35.6001, 139.7001);
            clock.AdvanceMinutes(30);
            tracker.RecordFix(35.6002, 139.7002);

            return new GpsScoreAttachment
            {
                Position = new LocationFix { Lat = 35.6002, Lon = 139.7002, AccuracyM = 9f },
                VenueId = 77,
                VenueName = "Tokyo Golf Club",
                VenueDistanceM = 42.5,
                Signals = GpsTestApi.Signals(isMock, "ios"),
                Session = tracker.SessionNear(35.6002, 139.7002)
            };
        }

        [Test]
        public void Submit_MergesTheGpsKeysOverTheRequestBody()
        {
            var transport = new FakeHttpTransport().Enqueue(HttpResponse.Status(200, OkBody));

            ApiResult<ScoreSubmitResult> result = null;
            Pump.Drain(new ScoreService(GpsTestApi.Client(transport))
                       .Submit(Request(), Attachment(), r => result = r));

            Assert.AreEqual(Endpoints.BaseUrl + "/score/submit", transport.SentUrls[0]);
            Assert.AreEqual("POST", transport.SentMethods[0]);

            JObject body = JObject.Parse(transport.SentBodies[0]);

            // The request half.
            Assert.AreEqual(92, body["score"].Value<int>());
            Assert.AreEqual("18", body["score_type"].Value<string>());
            Assert.AreEqual("Tokyo Golf Club", body["course_name"].Value<string>());
            Assert.AreEqual("screenshot", body["input_method"].Value<string>());
            Assert.AreEqual("public", body["visibility"].Value<string>());

            // The GPS half — all eleven keys GpsScoreAttachment.ToJson() can produce.
            Assert.IsTrue(body["gps_verified"].Value<bool>());
            Assert.AreEqual(35.6002d, body["latitude"].Value<double>(), 1e-9);
            Assert.AreEqual(139.7002d, body["longitude"].Value<double>(), 1e-9);
            Assert.AreEqual(77, body["venue_id"].Value<int>());
            Assert.IsFalse(body["gps_is_mock"].Value<bool>());
            Assert.AreEqual("ios", body["client_platform"].Value<string>());
            Assert.AreEqual(3, body["gps_check_count"].Value<int>());
            Assert.IsNotNull(body["gps_start_lat"]);
            Assert.IsNotNull(body["gps_start_lon"]);
            Assert.IsNotNull(body["gps_end_lat"]);
            Assert.IsNotNull(body["gps_end_lon"]);

            Assert.IsTrue(result.Success, result.ToString());
            Assert.AreEqual(80, result.Data.PointsEarned);
            Assert.AreEqual(80, result.Data.Trust);
            Assert.IsTrue(result.Data.GpsVerified);
            Assert.AreEqual("Tokyo Golf Club", result.Data.Activity.VenueName);
        }

        [Test]
        public void Submit_AKeyPresentInBothTakesTheAttachmentsValue()
        {
            // ScoreSubmitRequest deliberately has NO venue_id field — the attachment owns it, and
            // this is the test that pins that ownership. The server verifies the fix AGAINST the
            // venue, so the pair that goes up has to be self-consistent; a second writer for that
            // key is how a client ends up claiming a course it was never near. Proven twice: on a
            // hand-built body where both sides DO carry the key, and end to end through Submit.
            var transport = new FakeHttpTransport().Enqueue(HttpResponse.Status(200, OkBody));

            ScoreSubmitRequest req = Request();
            req.ScreenshotData = new JObject { ["venue_id"] = 999 };  // decoy nested, must not leak
            JObject reqJson = req.ToJson();
            reqJson["venue_id"] = 4242;                                // the screen's claim

            // Prove the merge direction on the assembled body itself.
            JObject merged = (JObject)reqJson.DeepClone();
            merged.Merge(Attachment().ToJson(), new JsonMergeSettings
            {
                MergeArrayHandling = MergeArrayHandling.Replace
            });
            Assert.AreEqual(77, merged["venue_id"].Value<int>(),
                            "the attachment's venue_id must overwrite the request's");

            // …and end to end through the service.
            Pump.Drain(new ScoreService(GpsTestApi.Client(transport)).Submit(req, Attachment(), _ => { }));
            JObject sent = JObject.Parse(transport.SentBodies[0]);
            Assert.AreEqual(77, sent["venue_id"].Value<int>());
            Assert.AreEqual(999, sent["screenshot_data"]["venue_id"].Value<int>(),
                            "the merge is top-level only — the archived AI payload is not rewritten");
        }

        [Test]
        public void Submit_OmitsHolesWhenNoHoleWasEdited()
        {
            var transport = new FakeHttpTransport()
                .Enqueue(HttpResponse.Status(200, OkBody), HttpResponse.Status(200, OkBody));

            ScoreSubmitRequest bare = Request();
            Pump.Drain(new ScoreService(GpsTestApi.Client(transport)).Submit(bare, Attachment(), _ => { }));
            Assert.IsNull(JObject.Parse(transport.SentBodies[0])["holes"],
                          "an un-edited card posts the AI total only — never eighteen zeroes");

            ScoreSubmitRequest edited = Request();
            edited.Holes = new List<HoleScore> { new HoleScore(1, 5), new HoleScore(2, null), new HoleScore(3, 4) };
            Pump.Drain(new ScoreService(GpsTestApi.Client(transport)).Submit(edited, Attachment(), _ => { }));

            var holes = (JArray)JObject.Parse(transport.SentBodies[1])["holes"];
            Assert.AreEqual(2, holes.Count, "a hole the player never touched is omitted, not sent as 0");
            Assert.AreEqual(1, holes[0]["hole"].Value<int>());
            Assert.AreEqual(5, holes[0]["score"].Value<int>());
            Assert.AreEqual(3, holes[1]["hole"].Value<int>());
        }

        [Test]
        public void Submit_MapsA400ToTheScoreRangeKeyAndKeepsTheServersDetail()
        {
            var transport = new FakeHttpTransport().Enqueue(HttpResponse.Status(400,
                "{\"detail\":\"9ホールのスコアは25〜100の範囲で入力してください\"}"));

            ApiResult<ScoreSubmitResult> result = null;
            Pump.Drain(new ScoreService(GpsTestApi.Client(transport)).Submit(Request(), null, r => result = r));

            Assert.IsFalse(result.Success);
            Assert.AreEqual(400, result.StatusCode);
            Assert.AreEqual(ScoreService.ErrScoreRangeKey, ScoreService.ErrorKeyFor(result));

            string shown = ScoreService.ErrorMessageFor(result, k => "[" + k + "]");
            StringAssert.StartsWith("[SU_ERR_SCORE_RANGE]", shown);
            StringAssert.Contains("25", shown, "the server's own range is the only actionable half");
        }

        [Test]
        public void Submit_MapsA429ToTheRateLimitKeyAndEverythingElseToGeneric()
        {
            var transport = new FakeHttpTransport()
                .Enqueue(HttpResponse.Status(429, "{\"detail\":\"too many posts\"}"),
                         HttpResponse.Status(500, "{\"detail\":\"boom\"}"));

            ApiResult<ScoreSubmitResult> limited = null;
            Pump.Drain(new ScoreService(GpsTestApi.Client(transport)).Submit(Request(), null, r => limited = r));
            Assert.AreEqual(ScoreService.ErrRateLimitKey, ScoreService.ErrorKeyFor(limited));

            ApiResult<ScoreSubmitResult> broken = null;
            Pump.Drain(new ScoreService(GpsTestApi.Client(transport)).Submit(Request(), null, r => broken = r));
            Assert.AreEqual(ScoreService.ErrGenericKey, ScoreService.ErrorKeyFor(broken));
        }

        [Test]
        public void Submit_SecondCallWhileOneIsPendingSendsNoSecondRequest()
        {
            // A double post is two activities rows, two payouts, and one step closer to the
            // 10-per-24h hard limit. The button is latched too; this is the floor under it.
            var transport = new SlowFakeTransport(yieldsBeforeResponding: 3);
            transport.Enqueue(HttpResponse.Status(200, OkBody), HttpResponse.Status(200, OkBody));

            var service = new ScoreService(new ApiClient(transport, new FakeAuthTokenProvider(),
                                                        new ImmediateCoroutineRunner())
            {
                RetryDelaySeconds = 0f,
                LogRequests = false
            });

            ApiResult<ScoreSubmitResult> first = null;
            IEnumerator inFlight = service.Submit(Request(), Attachment(), r => first = r);

            // Start it, but do NOT drain it: the latch must hold while the first call is mid-flight.
            inFlight.MoveNext();
            Assert.IsTrue(service.IsSubmitting);

            ApiResult<ScoreSubmitResult> duplicate = null;
            Pump.Drain(service.Submit(Request(), Attachment(), r => duplicate = r));

            Assert.AreEqual(1, transport.CallCount, "the duplicate must never reach the transport");
            Assert.IsFalse(duplicate.Success);
            Assert.AreEqual(ApiErrorKind.Disabled, duplicate.ErrorKind,
                            "Disabled means 'never sent' — the Confirm step treats it as a no-op, not an error");

            Pump.Drain(inFlight);
            Assert.IsFalse(service.IsSubmitting, "the latch clears when the first call answers");
            Assert.IsTrue(first.Success);

            // …and the NEXT post is allowed again.
            Pump.Drain(service.Submit(Request(), Attachment(), _ => { }));
            Assert.AreEqual(2, transport.CallCount);
        }

        [Test]
        public void Submit_WithNoAttachmentStillPostsTheScoreHalf()
        {
            // "投稿自体は止めない" — a player with no fix at all still gets to post, just without
            // any GPS keys and therefore without gps_verified.
            var transport = new FakeHttpTransport().Enqueue(HttpResponse.Status(200, OkBody));

            Pump.Drain(new ScoreService(GpsTestApi.Client(transport)).Submit(Request(), null, _ => { }));

            JObject body = JObject.Parse(transport.SentBodies[0]);
            Assert.AreEqual(92, body["score"].Value<int>());
            Assert.IsNull(body["gps_verified"], "no attachment ⇒ the key is absent, never a false claim");
            Assert.IsNull(body["latitude"]);
        }
    }

    /// <summary>
    /// <see cref="FakeHttpTransport"/> answers synchronously, which makes it useless for the ONE
    /// property that only exists mid-flight: the in-flight latch. This one yields a few times before
    /// responding, so a test can hold a half-pumped request open and start a second one against it.
    /// </summary>
    internal sealed class SlowFakeTransport : IHttpTransport
    {
        private readonly Queue<HttpResponse> _responses = new Queue<HttpResponse>();
        private readonly int _yields;

        public readonly List<string> SentUrls = new List<string>();
        public readonly List<string> SentBodies = new List<string>();

        public int CallCount => SentUrls.Count;

        public SlowFakeTransport(int yieldsBeforeResponding)
        {
            _yields = yieldsBeforeResponding;
        }

        public void Enqueue(params HttpResponse[] responses)
        {
            foreach (HttpResponse r in responses) _responses.Enqueue(r);
        }

        public IEnumerator Send(HttpRequest request, System.Action<HttpResponse> onResponse)
        {
            SentUrls.Add(request.Url);
            SentBodies.Add(request.Body);

            for (int i = 0; i < _yields; i++) yield return null;

            onResponse?.Invoke(_responses.Count > 0
                ? _responses.Dequeue()
                : HttpResponse.Status(500, "{\"detail\":\"no scripted response\"}"));
        }
    }
}
