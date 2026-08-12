// Order: reward_points_backend Slice 2 — the spend path (SPEC §4: online-required spends).
using Golfin.Net;
using Golfin.Net.Tests;
using NUnit.Framework;

namespace Golfin.Economy.Tests
{
    /// <summary>
    /// Spends are the half of the ledger that CANNOT be queued, so the thing worth pinning down is
    /// that every possible answer lands on a distinct verdict:
    ///
    ///   • debited            → Approved   (the action proceeds)
    ///   • HTTP 200 insufficient → Insufficient (the action does NOT proceed; nothing was written)
    ///   • offline / 5xx / no session → Unavailable (the action does NOT proceed)
    ///   • flag OFF           → Disabled   (no request at all; the caller runs its local-only path)
    ///
    /// The insufficient case is the sharp one: the server answers **200**, not an error status, so a
    /// naive `result.Success` check would read a refusal as an approval and hand out a free level-up.
    /// </summary>
    public class PointsSpendTests
    {
        private FakeHttpTransport _transport;
        private ApiClient _client;
        private PointsService _service;

        private const string OkEnvelope =
            "{\"data\":{\"status\":\"ok\",\"spent\":30,\"from_activity\":30,\"from_gift\":0," +
            "\"activity_pts\":95,\"gift_pts\":50,\"total_points\":145,\"replayed\":false}}";

        private const string ReplayedEnvelope =
            "{\"data\":{\"status\":\"ok\",\"spent\":30,\"from_activity\":30,\"from_gift\":0," +
            "\"activity_pts\":95,\"gift_pts\":50,\"total_points\":145,\"replayed\":true}}";

        private const string InsufficientEnvelope =
            "{\"data\":{\"status\":\"insufficient\",\"requested\":500,\"shortfall\":355," +
            "\"activity_pts\":95,\"gift_pts\":50,\"total_points\":145,\"replayed\":false}}";

        [SetUp]
        public void SetUp()
        {
            _transport = new FakeHttpTransport();
            _client = new ApiClient(_transport, new FakeAuthTokenProvider(), new ImmediateCoroutineRunner())
            {
                RetryDelaySeconds = 0f,
                LogRequests = false
            };
            _service = new PointsService(_client, new PendingOpsQueue(new InMemoryPendingOpsStore()));

            PointsBackendFlag.Enabled = true; // per-test default; the OFF case sets it back explicitly
        }

        [TearDown]
        public void TearDown()
        {
            PointsBackendFlag.ResetToDefault();
            PointsService.ResetForTest();
            ApiClient.ResetForTest();
        }

        private SpendOutcome Spend(int amount, string reason = SpendReasons.CharacterLevelUp)
        {
            SpendOutcome outcome = null;
            Pump.Drain(_service.SpendRoutine(amount, reason, o => outcome = o));
            return outcome;
        }

        // ── the flag gate ─────────────────────────────────────────────────────────

        [Test]
        public void FlagOff_MakesNoRequestAndStillLetsTheCallerProceed()
        {
            PointsBackendFlag.Enabled = false;
            _transport.Enqueue(HttpResponse.Status(200, OkEnvelope));

            SpendOutcome outcome = Spend(30);

            Assert.AreEqual(SpendVerdict.Disabled, outcome.Verdict);
            Assert.AreEqual(0, _transport.CallCount, "Flag OFF must not reach the network.");
            Assert.IsTrue(outcome.MayProceed,
                "Flag OFF is 'the server is not in this build's loop', NOT a refusal — the local-only " +
                "path must still run, or the game would be unplayable offline-by-default.");
        }

        // ── approved ──────────────────────────────────────────────────────────────

        [Test]
        public void Ok_ApprovesAndCachesTheNewBalance()
        {
            _transport.Enqueue(HttpResponse.Status(200, OkEnvelope));

            SpendOutcome outcome = Spend(30);

            Assert.AreEqual(SpendVerdict.Approved, outcome.Verdict);
            Assert.IsTrue(outcome.MayProceed);
            Assert.AreEqual(30, outcome.Server.Spent);
            Assert.AreEqual(145, _service.Balance, "Spend responses carry both buckets, so the cache is exact.");
            Assert.IsTrue(_service.HasBalance);
        }

        [Test]
        public void ReplayedDebit_IsStillAnApproval()
        {
            // An idempotent replay means the debit ALREADY happened — refusing here would strand the
            // player having paid for something the client then declined to give them.
            _transport.Enqueue(HttpResponse.Status(200, ReplayedEnvelope));

            SpendOutcome outcome = Spend(30);

            Assert.AreEqual(SpendVerdict.Approved, outcome.Verdict);
            Assert.IsTrue(outcome.Server.Replayed);
        }

        [Test]
        public void ZeroAmount_ApprovesWithoutAskingTheServer()
        {
            SpendOutcome outcome = Spend(0, SpendReasons.ModeEntryFee);

            Assert.AreEqual(SpendVerdict.Approved, outcome.Verdict);
            Assert.AreEqual(0, _transport.CallCount, "A free mode must not cost a round-trip.");
        }

        // ── refused ───────────────────────────────────────────────────────────────

        [Test]
        public void Insufficient_Arrives200ButMustNotProceed()
        {
            _transport.Enqueue(HttpResponse.Status(200, InsufficientEnvelope));

            SpendOutcome outcome = Spend(500);

            Assert.AreEqual(SpendVerdict.Insufficient, outcome.Verdict);
            Assert.IsFalse(outcome.MayProceed, "200 + status:insufficient is a REFUSAL, not a success.");
            Assert.AreEqual(355, outcome.Server.Shortfall);
            Assert.IsFalse(outcome.IsOffline, "Insufficient is a definitive answer — not a connectivity fault.");
            Assert.AreEqual(145, _service.Balance, "The refusal still tells us the true balance.");
        }

        [Test]
        public void ConnectionFailure_IsUnavailableNotInsufficient()
        {
            // Every retry attempt fails: the caller must be told "connection required", never "broke".
            _transport.Enqueue(HttpResponse.ConnectionFailure("dns failure"));
            _transport.Enqueue(HttpResponse.ConnectionFailure("dns failure"));
            _transport.Enqueue(HttpResponse.ConnectionFailure("dns failure"));

            SpendOutcome outcome = Spend(30);

            Assert.AreEqual(SpendVerdict.Unavailable, outcome.Verdict);
            Assert.IsFalse(outcome.MayProceed);
            Assert.IsTrue(outcome.IsOffline);
        }

        [Test]
        public void ServerError_DoesNotProceed()
        {
            _transport.Enqueue(HttpResponse.Status(500, "{\"detail\":\"boom\"}"));

            SpendOutcome outcome = Spend(30);

            Assert.AreEqual(SpendVerdict.Unavailable, outcome.Verdict);
            Assert.IsFalse(outcome.MayProceed);
        }

        [Test]
        public void UnrecognisedStatus_FailsClosed()
        {
            // An unknown status string must never be read as approval.
            _transport.Enqueue(HttpResponse.Status(200, "{\"data\":{\"status\":\"wat\"}}"));

            SpendOutcome outcome = Spend(30);

            Assert.AreEqual(SpendVerdict.Unavailable, outcome.Verdict);
            Assert.IsFalse(outcome.MayProceed);
        }

        [Test]
        public void EmptyReason_IsRefusedWithoutARequest()
        {
            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Error,
                new System.Text.RegularExpressions.Regex("no reason"));

            SpendOutcome outcome = Spend(30, "");

            Assert.AreEqual(SpendVerdict.Unavailable, outcome.Verdict);
            Assert.AreEqual(0, _transport.CallCount);
        }

        // ── request shape ─────────────────────────────────────────────────────────

        [Test]
        public void RequestBody_MatchesTheDeployedSpendRequestModel()
        {
            string json = PointsService.BuildSpendJson(30, SpendReasons.ClubLevelUp,
                "11111111-2222-3333-4444-555555555555");

            // backend/routers/points.py::SpendRequest — snake_case, all three fields required.
            StringAssert.Contains("\"amount\":30", json);
            StringAssert.Contains("\"reason\":\"club_level_up\"", json);
            StringAssert.Contains("\"idempotency_key\":\"11111111-2222-3333-4444-555555555555\"", json);
        }

        [Test]
        public void EverySpend_GetsItsOwnIdempotencyKey()
        {
            // Unlike a queued earn (one key, replayed until delivered), each spend is a distinct
            // intent — sharing a key would make the second level-up a free no-op replay of the first.
            _transport.Enqueue(HttpResponse.Status(200, OkEnvelope));
            _transport.Enqueue(HttpResponse.Status(200, OkEnvelope));

            Spend(30);
            Spend(30);

            Assert.AreEqual(2, _transport.CallCount);
            Assert.AreNotEqual(_transport.SentBodies[0], _transport.SentBodies[1],
                "Two spends must not share an idempotency key.");
        }

        [Test]
        public void Spend_PostsToTheSpendEndpoint()
        {
            _transport.Enqueue(HttpResponse.Status(200, OkEnvelope));

            Spend(30);

            Assert.AreEqual("POST", _transport.SentMethods[0]);
            StringAssert.EndsWith("/points/spend", _transport.SentUrls[0]);
        }

        [Test]
        public void Spend_IsNeverQueued()
        {
            // Decision of record #2: earns queue, spends do not. A queued spend would let the player
            // buy something the server later refuses.
            _transport.Enqueue(HttpResponse.ConnectionFailure("offline"));
            _transport.Enqueue(HttpResponse.ConnectionFailure("offline"));
            _transport.Enqueue(HttpResponse.ConnectionFailure("offline"));

            Spend(30);

            Assert.AreEqual(0, _service.Queue.Count);
        }
    }
}
