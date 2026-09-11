// home_carousel_and_daily_timing — MissionsClient.LastDaily.
//
// The Mission Selection daily card paints from the last answer GET /missions/daily gave, so
// it arrives with the campaign list instead of 200 ms–2 s after it. This pins the two things
// that makes safe: a success is remembered, and a failure does not overwrite it.
using System.Collections;
using Golfin.Net;
using Golfin.Net.Tests;
using NUnit.Framework;

namespace Golfin.Economy.Tests
{
    [TestFixture]
    public class MissionsClientDailyTests
    {
        private FakeHttpTransport _transport;
        private MissionsClient _missions;

        private const string DailyEnvelope =
            "{\"data\":{\"date\":\"2026-09-11\",\"recipe_hash\":\"abc\",\"pinned\":false,\"claimed\":false," +
            "\"claimed_rp\":0,\"streak\":3,\"recipe\":{\"holeId\":6,\"par\":4,\"startAreaId\":\"tee\"," +
            "\"windPresetId\":\"calm\",\"loadoutId\":\"SUP_IRONS\",\"pinIndex\":0,\"staminaDrain\":1," +
            "\"goals\":[],\"modifier\":null,\"difficultyScore\":1}}}";

        [SetUp]
        public void SetUp()
        {
            _transport = new FakeHttpTransport();
            var client = new ApiClient(_transport, new FakeAuthTokenProvider(), new ImmediateCoroutineRunner())
            {
                RetryDelaySeconds = 0f,
                LogRequests = false
            };
            _missions = new MissionsClient(client);
        }

        [TearDown]
        public void TearDown() => ApiClient.ResetForTest();

        [Test]
        public void FetchDaily_RemembersASuccessfulAnswer()
        {
            Assert.IsNull(_missions.LastDaily, "nothing has answered yet");
            _transport.Enqueue(HttpResponse.Status(200, DailyEnvelope));

            ApiResult<DailyMissionResult> seen = null;
            Pump.Drain(_missions.FetchDailyRoutine(r => seen = r));

            Assert.IsTrue(seen.Success);
            Assert.AreSame(seen.Data, _missions.LastDaily, "the remembered answer IS the one the caller got");
            Assert.AreEqual("2026-09-11", _missions.LastDaily.Date);
            Assert.AreEqual(3, _missions.LastDaily.Streak);
            Assert.AreEqual(6, _missions.LastDaily.Recipe.HoleId);
        }

        [Test]
        public void FetchDaily_AFailureKeepsThePreviousAnswer()
        {
            _transport.Enqueue(HttpResponse.Status(200, DailyEnvelope));
            Pump.Drain(_missions.FetchDailyRoutine(_ => { }));
            var first = _missions.LastDaily;
            Assert.IsNotNull(first);

            // The fake's fallback is a 500 once the queue is dry; the client retries at zero delay
            // and gives up. Whatever it gives up WITH must not be a null answer.
            ApiResult<DailyMissionResult> seen = null;
            Pump.Drain(_missions.FetchDailyRoutine(r => seen = r));

            Assert.IsFalse(seen.Success);
            Assert.AreSame(first, _missions.LastDaily, "a failed fetch is not an answer");
        }

        [Test]
        public void ForgetDaily_ClearsIt()
        {
            _transport.Enqueue(HttpResponse.Status(200, DailyEnvelope));
            Pump.Drain(_missions.FetchDailyRoutine(_ => { }));
            _missions.ForgetDaily();
            Assert.IsNull(_missions.LastDaily);
        }
    }
}
