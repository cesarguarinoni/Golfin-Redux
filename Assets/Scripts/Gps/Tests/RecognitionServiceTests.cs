// Order: score_upload_flow §1 Tests — /recognition/analyze body shape + the golf extraction view.
using Golfin.Net;
using Golfin.Net.Tests;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Golfin.Gps.Tests
{
    public class RecognitionServiceTests
    {
        /// <summary>A full golf answer, exactly the five keys RECOGNITION_SYSTEM_PROMPT "## golf"
        /// asks for (recognition.py:73-78), inside the {data:…} envelope.</summary>
        private const string FullGolfBody =
            "{\"data\":{\"id\":\"rec_123\",\"sport_type\":\"golf\",\"user_id\":\"u1\"," +
            "\"confidence\":0.91,\"recognized_at\":\"2026-09-01T04:00:00Z\"," +
            "\"extracted_data\":{\"score\":92,\"course\":\"Tokyo Golf Club\",\"holes\":18," +
            "\"date\":\"2026-04-09\",\"par\":72},\"raw_response\":\"{...}\"}}";

        private static readonly byte[] Jpeg = { 0xFF, 0xD8, 0xFF, 0xE0, 0x01, 0x02, 0x03 };

        [Test]
        public void Analyze_PostsADataUrlBodyToTheAnalyzeEndpoint()
        {
            var transport = new FakeHttpTransport().Enqueue(HttpResponse.Status(200, FullGolfBody));

            ApiResult<RecognitionResult> result = null;
            Pump.Drain(new RecognitionService(GpsTestApi.Client(transport)).Analyze(Jpeg, r => result = r));

            Assert.AreEqual(1, transport.CallCount);
            Assert.AreEqual("POST", transport.SentMethods[0]);
            Assert.AreEqual(Endpoints.BaseUrl + "/recognition/analyze", transport.SentUrls[0]);

            JObject body = JObject.Parse(transport.SentBodies[0]);
            string image = body["image_base64"].Value<string>();

            StringAssert.StartsWith(RecognitionService.JpegDataUrlPrefix, image);
            Assert.AreEqual(System.Convert.ToBase64String(Jpeg),
                            image.Substring(RecognitionService.JpegDataUrlPrefix.Length),
                            "the payload after the data-URL prefix must be the JPEG verbatim");

            Assert.AreEqual("golf", body["sport_type"].Value<string>(),
                            "sport_type is pinned so a range photo cannot come back classified as 'running'");
        }

        [Test]
        public void Analyze_RaisesTheTimeoutTo90sForThisRequestOnly()
        {
            // The reason this class exists rather than a one-line ApiClient.Post: Vision on a cold
            // Fly machine is a 20–40 s answer, and the shared 30 s client default would kill it.
            var transport = new FakeHttpTransport().Enqueue(HttpResponse.Status(200, FullGolfBody));
            ApiClient client = GpsTestApi.Client(transport);

            Pump.Drain(new RecognitionService(client).Analyze(Jpeg, _ => { }));

            Assert.AreEqual(90, transport.SentTimeouts[0]);
            Assert.AreEqual(30, client.TimeoutSeconds,
                            "the shared client must be untouched — a global 90 s would freeze every screen");
        }

        [Test]
        public void Analyze_UnwrapsAFullGolfResultIntoGolfExtraction()
        {
            var transport = new FakeHttpTransport().Enqueue(HttpResponse.Status(200, FullGolfBody));

            ApiResult<RecognitionResult> result = null;
            Pump.Drain(new RecognitionService(GpsTestApi.Client(transport)).Analyze(Jpeg, r => result = r));

            Assert.IsTrue(result.Success, result.ToString());
            Assert.AreEqual("rec_123", result.Data.Id);
            Assert.AreEqual("golf", result.Data.SportType);
            Assert.AreEqual(0.91d, result.Data.Confidence, 0.0001d);

            GolfExtraction g = result.Data.Golf();
            Assert.AreEqual(92, g.Score);
            Assert.AreEqual("Tokyo Golf Club", g.Course);
            Assert.AreEqual(18, g.Holes);
            Assert.AreEqual("2026-04-09", g.Date);
            Assert.AreEqual(72, g.Par);
        }

        [Test]
        public void Analyze_MissingFieldsStayNullRatherThanThrowing()
        {
            // The prompt tells the model to return null for anything it cannot read; a blurry card
            // producing only a total is the NORMAL degraded outcome and must still reach step 3.
            var transport = new FakeHttpTransport().Enqueue(HttpResponse.Status(200,
                "{\"data\":{\"id\":\"rec_9\",\"sport_type\":\"golf\",\"confidence\":0.42," +
                "\"extracted_data\":{\"score\":88,\"course\":null,\"date\":null}}}"));

            ApiResult<RecognitionResult> result = null;
            Pump.Drain(new RecognitionService(GpsTestApi.Client(transport)).Analyze(Jpeg, r => result = r));

            Assert.IsTrue(result.Success, result.ToString());

            GolfExtraction g = result.Data.Golf();
            Assert.AreEqual(88, g.Score);
            Assert.IsNull(g.Course);
            Assert.IsNull(g.Holes, "an absent key is null, not 0 — 0 holes would fail the bounds check silently");
            Assert.IsNull(g.Date);
            Assert.IsNull(g.Par, "no par ⇒ the Confirm step hides '(+N)' rather than inventing a par");
        }

        [Test]
        public void GolfExtraction_ReadsAQuotedIntegerAndSurvivesAnAbsentExtractedData()
        {
            // Vision occasionally quotes a number. A quoted 92 is still a 92.
            GolfExtraction quoted = GolfExtraction.From(JObject.Parse("{\"score\":\"92\",\"holes\":\"9\"}"));
            Assert.AreEqual(92, quoted.Score);
            Assert.AreEqual(9, quoted.Holes);

            GolfExtraction none = GolfExtraction.From(null);
            Assert.IsNull(none.Score);
            Assert.IsNull(none.Course);
        }

        [Test]
        public void Analyze_WithNoImageFailsWithoutTouchingTheNetwork()
        {
            var transport = new FakeHttpTransport();

            ApiResult<RecognitionResult> result = null;
            Pump.Drain(new RecognitionService(GpsTestApi.Client(transport)).Analyze(new byte[0], r => result = r));

            Assert.AreEqual(0, transport.CallCount);
            Assert.IsFalse(result.Success);
            Assert.AreEqual(ApiErrorKind.NotConfigured, result.ErrorKind);
        }
    }
}
