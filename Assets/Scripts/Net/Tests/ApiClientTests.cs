// Order: reward_points_backend Slice 1 — ApiClient envelope / retry / 401-refresh coverage (SPEC §4).
using Newtonsoft.Json;
using NUnit.Framework;

namespace Golfin.Net.Tests
{
    /// <summary>
    /// Drives <see cref="ApiClient"/> against a scripted transport — no network, no play mode.
    ///
    /// Response bodies are the REAL shapes probed against the live deployment on 2026-08-12:
    /// enveloped <c>{data:…}</c> success, un-enveloped <c>/health</c>, and FastAPI's
    /// <c>{"detail":"…"}</c> for 401/403.
    /// </summary>
    public class ApiClientTests
    {
        private FakeHttpTransport _transport;
        private FakeAuthTokenProvider _auth;
        private ApiClient _client;

        private const string BalanceEnvelope =
            "{\"data\":{\"activity_pts\":425,\"gift_pts\":50,\"total_points\":475,\"avatar_level\":3,\"avatar_xp\":120}}";

        private sealed class Balance
        {
            [JsonProperty("activity_pts")] public int ActivityPts;
            [JsonProperty("gift_pts")]     public int GiftPts;
            [JsonProperty("total_points")] public int TotalPoints;
            [JsonProperty("avatar_level")] public int AvatarLevel;
            [JsonProperty("avatar_xp")]    public int AvatarXp;
        }

        private sealed class Health
        {
            [JsonProperty("status")]  public string Status;
            [JsonProperty("version")] public string Version;
        }

        [SetUp]
        public void SetUp()
        {
            _transport = new FakeHttpTransport();
            _auth = new FakeAuthTokenProvider();
            _client = new ApiClient(_transport, _auth, new ImmediateCoroutineRunner())
            {
                RetryDelaySeconds = 0f,   // keep the manual pump off the wall clock
                LogRequests = false
            };
        }

        // ── envelope ──────────────────────────────────────────────────────────────

        [Test]
        public void Get_UnwrapsDataEnvelope()
        {
            _transport.Enqueue(HttpResponse.Status(200, BalanceEnvelope));

            ApiResult<Balance> result = null;
            Pump.Drain(_client.Get<Balance>(Endpoints.PointsBalance, r => result = r));

            Assert.IsNotNull(result, "onResult must be invoked exactly once.");
            Assert.IsTrue(result.Success, result.ToString());
            Assert.AreEqual(475, result.Data.TotalPoints, "total_points is the game's RP.");
            Assert.AreEqual(425, result.Data.ActivityPts);
            Assert.AreEqual(50, result.Data.GiftPts);
            Assert.AreEqual(1, result.Attempts);
            Assert.IsFalse(result.DidRefreshToken);
        }

        [Test]
        public void Get_PassesThroughBodyWithNoEnvelope()
        {
            // /health is root-mounted and NOT enveloped — verified live 2026-08-12.
            _transport.Enqueue(HttpResponse.Status(200, "{\"status\":\"ok\",\"version\":\"0.1.0\"}"));

            ApiResult<Health> result = null;
            Pump.Drain(_client.Get<Health>(Endpoints.Health, r => result = r));

            Assert.IsTrue(result.Success, result.ToString());
            Assert.AreEqual("ok", result.Data.Status);
            Assert.AreEqual("0.1.0", result.Data.Version);
        }

        [Test]
        public void Get_EmptyBodyIsSuccessNotParseFailure()
        {
            _transport.Enqueue(HttpResponse.Status(204, ""));

            ApiResult<Balance> result = null;
            Pump.Drain(_client.Get<Balance>(Endpoints.PointsBalance, r => result = r));

            Assert.IsTrue(result.Success);
            Assert.IsNull(result.Data);
        }

        [Test]
        public void Get_MalformedJsonIsParseError()
        {
            _transport.Enqueue(HttpResponse.Status(200, "{not json at all"));

            ApiResult<Balance> result = null;
            Pump.Drain(_client.Get<Balance>(Endpoints.PointsBalance, r => result = r));

            Assert.IsFalse(result.Success);
            Assert.AreEqual(ApiErrorKind.Parse, result.ErrorKind);
        }

        // ── auth header ───────────────────────────────────────────────────────────

        [Test]
        public void Send_AttachesBearerToken()
        {
            _transport.Enqueue(HttpResponse.Status(200, BalanceEnvelope));

            Pump.Drain(_client.Get<Balance>(Endpoints.PointsBalance, _ => { }));

            Assert.AreEqual("Bearer tok-initial", _transport.SentAuthHeaders[0]);
        }

        [Test]
        public void Send_OmitsBearerWhenSignedOut()
        {
            _auth.Authenticated = false;
            _transport.Enqueue(HttpResponse.Status(403, "{\"detail\":\"Not authenticated\"}"));

            ApiResult<Balance> result = null;
            Pump.Drain(_client.Get<Balance>(Endpoints.PointsBalance, r => result = r));

            Assert.IsNull(_transport.SentAuthHeaders[0], "No token → no Authorization header.");
            // Live behaviour: a header-less call is 403, and no refresh can fix that.
            Assert.AreEqual(ApiErrorKind.Forbidden, result.ErrorKind);
            Assert.AreEqual(0, _auth.RefreshCallCount, "403 must NOT trigger a refresh.");
            Assert.AreEqual(1, result.Attempts);
        }

        // ── 401 → refresh → retry once ────────────────────────────────────────────

        [Test]
        public void Unauthorized_RefreshesThenRetriesOnceWithTheNewToken()
        {
            _transport.Enqueue(
                HttpResponse.Status(401, "{\"detail\":\"Authentication failed: invalid JWT\"}"),
                HttpResponse.Status(200, BalanceEnvelope));

            ApiResult<Balance> result = null;
            Pump.Drain(_client.Get<Balance>(Endpoints.PointsBalance, r => result = r));

            Assert.IsTrue(result.Success, result.ToString());
            Assert.AreEqual(1, _auth.RefreshCallCount);
            Assert.AreEqual(2, _transport.CallCount);
            Assert.AreEqual(2, result.Attempts);
            Assert.IsTrue(result.DidRefreshToken);
            Assert.AreEqual("Bearer tok-initial", _transport.SentAuthHeaders[0]);
            Assert.AreEqual("Bearer tok-refreshed", _transport.SentAuthHeaders[1],
                "The replay must carry the REFRESHED token, not the stale one.");
        }

        [Test]
        public void Unauthorized_RefreshFails_ReturnsUnauthorizedWithoutRetrying()
        {
            _auth.RefreshSucceeds = false;
            _transport.Enqueue(HttpResponse.Status(401, "{\"detail\":\"Authentication failed\"}"));

            ApiResult<Balance> result = null;
            Pump.Drain(_client.Get<Balance>(Endpoints.PointsBalance, r => result = r));

            Assert.IsFalse(result.Success);
            Assert.AreEqual(ApiErrorKind.Unauthorized, result.ErrorKind);
            Assert.AreEqual(1, _auth.RefreshCallCount);
            Assert.AreEqual(1, _transport.CallCount, "A failed refresh must not replay the request.");
        }

        [Test]
        public void Unauthorized_SecondConsecutive401DoesNotLoop()
        {
            // Refresh succeeds but the server still rejects — the guard must stop after ONE replay.
            _transport.Enqueue(
                HttpResponse.Status(401, "{\"detail\":\"Authentication failed\"}"),
                HttpResponse.Status(401, "{\"detail\":\"Authentication failed\"}"));

            ApiResult<Balance> result = null;
            Pump.Drain(_client.Get<Balance>(Endpoints.PointsBalance, r => result = r));

            Assert.IsFalse(result.Success);
            Assert.AreEqual(ApiErrorKind.Unauthorized, result.ErrorKind);
            Assert.AreEqual(1, _auth.RefreshCallCount, "Refresh is armed once per logical call.");
            Assert.AreEqual(2, _transport.CallCount);
        }

        // ── transient retry ───────────────────────────────────────────────────────

        [Test]
        public void ConnectionFailure_RetriesThenSucceeds()
        {
            _transport.Enqueue(
                HttpResponse.ConnectionFailure("Cannot resolve host"),
                HttpResponse.Status(200, BalanceEnvelope));

            ApiResult<Balance> result = null;
            Pump.Drain(_client.Get<Balance>(Endpoints.PointsBalance, r => result = r));

            Assert.IsTrue(result.Success, result.ToString());
            Assert.AreEqual(2, result.Attempts);
            Assert.AreEqual(0, _auth.RefreshCallCount);
        }

        [Test]
        public void ConnectionFailure_ExhaustsRetryBudgetThenFailsNetwork()
        {
            _transport.Fallback = HttpResponse.ConnectionFailure("Cannot resolve host");

            ApiResult<Balance> result = null;
            Pump.Drain(_client.Get<Balance>(Endpoints.PointsBalance, r => result = r));

            Assert.IsFalse(result.Success);
            Assert.AreEqual(ApiErrorKind.Network, result.ErrorKind);
            Assert.AreEqual(_client.MaxTransientRetries + 1, result.Attempts,
                "First attempt plus MaxTransientRetries — and no more.");
        }

        [Test]
        public void Timeout408_RetriesThenFailsAsTimeout()
        {
            _transport.Fallback = HttpResponse.Status(408, "");

            ApiResult<Balance> result = null;
            Pump.Drain(_client.Get<Balance>(Endpoints.PointsBalance, r => result = r));

            Assert.IsFalse(result.Success);
            Assert.AreEqual(ApiErrorKind.Timeout, result.ErrorKind);
            Assert.AreEqual(_client.MaxTransientRetries + 1, result.Attempts);
        }

        [Test]
        public void RefreshDoesNotConsumeTheTransientRetryBudget()
        {
            // 401 → refresh → replay, then two connection failures, then success.
            // A shared budget would have given up before the final 200.
            _transport.Enqueue(
                HttpResponse.Status(401, "{\"detail\":\"expired\"}"),
                HttpResponse.ConnectionFailure("flaky"),
                HttpResponse.ConnectionFailure("flaky"),
                HttpResponse.Status(200, BalanceEnvelope));

            ApiResult<Balance> result = null;
            Pump.Drain(_client.Get<Balance>(Endpoints.PointsBalance, r => result = r));

            Assert.IsTrue(result.Success, result.ToString());
            Assert.AreEqual(4, result.Attempts);
            Assert.AreEqual(1, _auth.RefreshCallCount);
        }

        // ── error mapping ─────────────────────────────────────────────────────────

        [Test]
        public void ServerError_IsNotRetried()
        {
            _transport.Enqueue(HttpResponse.Status(500, "{\"detail\":\"boom\"}"));

            ApiResult<Balance> result = null;
            Pump.Drain(_client.Get<Balance>(Endpoints.PointsBalance, r => result = r));

            Assert.IsFalse(result.Success);
            Assert.AreEqual(ApiErrorKind.Server, result.ErrorKind);
            Assert.AreEqual(1, result.Attempts, "5xx is not in the transient-retry set.");
            Assert.AreEqual("boom", result.ErrorMessage, "FastAPI's {detail} becomes the message.");
        }

        [Test]
        public void NotFound_MapsToNotFound()
        {
            _transport.Enqueue(HttpResponse.Status(404, "{\"detail\":\"Not Found\"}"));

            ApiResult<Balance> result = null;
            Pump.Drain(_client.Get<Balance>(Endpoints.PointsBalance, r => result = r));

            Assert.AreEqual(ApiErrorKind.NotFound, result.ErrorKind);
        }

        [Test]
        public void Post_SendsBodyAndMethod()
        {
            _transport.Enqueue(HttpResponse.Status(200, "{\"data\":{\"total_points\":10}}"));

            const string body = "{\"action\":\"hole_complete\",\"idempotency_key\":\"k\"}";
            Pump.Drain(_client.Post<Balance>(Endpoints.PointsEarnGame, body, _ => { }));

            Assert.AreEqual("POST", _transport.SentMethods[0]);
            Assert.AreEqual(body, _transport.SentBodies[0]);
            Assert.AreEqual(Endpoints.PointsEarnGame, _transport.SentUrls[0]);
        }

        [Test]
        public void NoTransport_FailsAsNotConfiguredInsteadOfThrowing()
        {
            var client = new ApiClient(null, _auth, new ImmediateCoroutineRunner());

            ApiResult<Balance> result = null;
            Pump.Drain(client.Get<Balance>(Endpoints.PointsBalance, r => result = r));

            Assert.IsFalse(result.Success);
            Assert.AreEqual(ApiErrorKind.NotConfigured, result.ErrorKind);
        }

        [Test]
        public void Endpoints_MatchTheDeployedRoutes()
        {
            // Verified live 2026-08-12: /health is root-mounted; /points/* sits under /api/v1.
            Assert.AreEqual("https://playlife-api.fly.dev/health", Endpoints.Health);
            Assert.AreEqual("https://playlife-api.fly.dev/api/v1", Endpoints.BaseUrl);
            Assert.AreEqual("https://playlife-api.fly.dev/api/v1/points/balance", Endpoints.PointsBalance);
            Assert.AreEqual("https://playlife-api.fly.dev/api/v1/points/earn-game", Endpoints.PointsEarnGame);
            Assert.AreEqual("https://playlife-api.fly.dev/api/v1/points/spend", Endpoints.PointsSpend);
            Assert.AreEqual("https://playlife-api.fly.dev/api/v1/points/history?skip=0&limit=20&currency=activity",
                Endpoints.PointsHistory(0, 20, "activity"));
        }
    }
}
