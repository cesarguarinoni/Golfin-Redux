// Order: progress_server_side §4 — the server-priced level-up call.
using Golfin.Net;
using Golfin.Net.Tests;
using NUnit.Framework;

namespace Golfin.Economy.Tests
{
    /// <summary>
    /// A level-up is the flow where the client used to pick BOTH the number it was charged and the
    /// level it ended up at, so what is worth pinning here is that every server answer lands on a
    /// distinct verdict and that the answers carrying no balances never touch the cached one.
    ///
    ///   • ok             → Ok             (run the local commit; the SERVER's cost is what was paid)
    ///   • insufficient   → Insufficient   (nothing written; the balance IS carried and folded)
    ///   • cost_changed   → CostChanged    (nothing written; NO balances — must not fold)
    ///   • level_conflict → LevelConflict  (nothing written; NO balances — must not fold)
    ///   • costs_missing / invalid_range / not_available / anything new → NotAvailable
    ///   • offline / 5xx  → Unavailable
    ///   • flag OFF       → Disabled       (no request at all)
    ///
    /// The sharp ones are <c>cost_changed</c> and <c>level_conflict</c>: they arrive as HTTP **200**
    /// like every other refusal, and their payloads have zeros in <c>total_points</c> because the
    /// server never reached <c>spend_pts</c>. Folding those zeros into the cache would blank the
    /// player's RP on a refusal that charged them nothing — which is why two of the tests below assert
    /// the cached balance did NOT move rather than asserting only on the verdict.
    ///
    /// The one verdict with no counterpart in <see cref="ShopPurchaseServiceTests"/> is
    /// <c>level_conflict</c>, and it is the whole reason this endpoint records a level at all: it is
    /// the answer a client gets when its save says one thing and the ledger says another.
    /// </summary>
    public class ProgressServiceTests
    {
        private FakeHttpTransport _transport;
        private ApiClient _client;
        private PointsService _points;
        private ProgressService _service;

        private const string Ref = "char_kai";

        private const string OkEnvelope =
            "{\"data\":{\"status\":\"ok\",\"kind\":\"character\",\"ref_id\":\"" + Ref + "\"," +
            "\"level\":13,\"from_level\":10,\"cost\":36,\"grandfathered\":true," +
            "\"spent\":36,\"from_activity\":36,\"from_gift\":0," +
            "\"activity_pts\":109,\"gift_pts\":0,\"total_points\":109,\"replayed\":false}}";

        private const string OkBlobMismatchEnvelope =
            "{\"data\":{\"status\":\"ok\",\"kind\":\"character\",\"ref_id\":\"" + Ref + "\"," +
            "\"level\":13,\"from_level\":10,\"cost\":36,\"grandfathered\":true,\"blob_level\":8," +
            "\"spent\":36,\"from_activity\":36,\"from_gift\":0," +
            "\"activity_pts\":109,\"gift_pts\":0,\"total_points\":109,\"replayed\":false}}";

        private const string ReplayedEnvelope =
            "{\"data\":{\"status\":\"ok\",\"kind\":\"character\",\"ref_id\":\"" + Ref + "\"," +
            "\"level\":13,\"from_level\":10,\"cost\":36,\"grandfathered\":false," +
            "\"spent\":36,\"from_activity\":36,\"from_gift\":0," +
            "\"activity_pts\":109,\"gift_pts\":0,\"total_points\":109,\"replayed\":true}}";

        private const string InsufficientEnvelope =
            "{\"data\":{\"status\":\"insufficient\",\"requested\":36,\"shortfall\":11," +
            "\"activity_pts\":25,\"gift_pts\":0,\"total_points\":25,\"replayed\":false}}";

        private const string CostChangedEnvelope =
            "{\"data\":{\"status\":\"cost_changed\",\"cost\":48,\"expected\":36," +
            "\"from_level\":10,\"to_level\":13}}";

        private const string LevelConflictEnvelope =
            "{\"data\":{\"status\":\"level_conflict\",\"server_level\":12,\"claimed_from\":10," +
            "\"kind\":\"character\",\"ref_id\":\"" + Ref + "\"}}";

        private const string CostsMissingEnvelope =
            "{\"data\":{\"status\":\"costs_missing\",\"level\":12,\"from_level\":10,\"to_level\":13}}";

        private const string InvalidRangeEnvelope =
            "{\"data\":{\"status\":\"invalid_range\",\"reason\":\"max_level\",\"to_level\":40,\"max_level\":39}}";

        private const string NotAvailableEnvelope =
            "{\"data\":{\"status\":\"not_available\",\"reason\":\"disabled\"}}";

        [SetUp]
        public void SetUp()
        {
            _transport = new FakeHttpTransport();
            _client = new ApiClient(_transport, new FakeAuthTokenProvider(), new ImmediateCoroutineRunner())
            {
                RetryDelaySeconds = 0f,
                LogRequests = false
            };

            // The service folds balances through PointsService.Instance, so the singleton has to be a
            // test double rather than the file-backed shipping one.
            _points = new PointsService(_client, new PendingOpsQueue(new InMemoryPendingOpsStore()));
            PointsService.ConfigureForTest(_points);

            _service = new ProgressService(_client);
            ProgressService.ConfigureForTest(_service);

            PointsBackendFlag.Enabled = true; // per-test default; the OFF case sets it back explicitly
        }

        [TearDown]
        public void TearDown()
        {
            PointsBackendFlag.ResetToDefault();
            ProgressService.ResetForTest();
            PointsService.ResetForTest();
            ApiClient.ResetForTest();
        }

        private ProgressLevelUpOutcome LevelUp(int from = 10, int to = 13, int expected = 36,
                                               int build = 2113)
        {
            ProgressLevelUpOutcome outcome = null;
            Pump.Drain(_service.LevelUpRoutine(ProgressService.KindCharacter, Ref, from, to,
                                               expected, build, o => outcome = o));
            return outcome;
        }

        // ── the wire shape ────────────────────────────────────────────────────────

        [Test]
        public void BuildLevelUpJson_MatchesTheDeployedRequestModel()
        {
            string json = ProgressService.BuildLevelUpJson(
                ProgressService.KindCharacter, Ref, 10, 13, 36, 2113, "key-1");

            StringAssert.Contains("\"kind\":\"character\"", json);
            StringAssert.Contains("\"ref_id\":\"" + Ref + "\"", json);
            StringAssert.Contains("\"from_level\":10", json);
            StringAssert.Contains("\"to_level\":13", json);
            StringAssert.Contains("\"idempotency_key\":\"key-1\"", json);
            StringAssert.Contains("\"build\":2113", json);
            StringAssert.Contains("\"expected_cost\":36", json);
            StringAssert.DoesNotContain("user_id", json,
                "The server stamps the caller from the token. A user id in the body would be the exact " +
                "hole this endpoint exists to close.");
            StringAssert.DoesNotContain("\"amount\"", json,
                "A level-up must never send an amount. It sends WHICH REF and WHICH LEVELS; the " +
                "server prices it from published content.");
            StringAssert.DoesNotContain("\"reason\"", json,
                "The legacy path sent reason:\"character_level_up\" with a client-computed amount. " +
                "This request has no such field — the reason string is built server-side.");
        }

        [Test]
        public void BuildLevelUpJson_SendsNullNotZeroWhenThereIsNoExpectedCost()
        {
            // null means "do not guard". 0 would mean "I expect this to be free" and would refuse
            // every real level-up with cost_changed.
            string json = ProgressService.BuildLevelUpJson(
                ProgressService.KindCharacter, Ref, 10, 13, 0, 2113, "key-1");
            StringAssert.Contains("\"expected_cost\":null", json);
        }

        [Test]
        public void BuildLevelUpJson_ClampsANegativeBuildToZero()
        {
            // build=0 is the safe end: the server then admits only refs every build can render.
            string json = ProgressService.BuildLevelUpJson(
                ProgressService.KindCharacter, Ref, 10, 13, 36, -5, "key-1");
            StringAssert.Contains("\"build\":0", json);
        }

        [Test]
        public void EveryAttemptSendsAFreshIdempotencyKey()
        {
            // 500 is NOT a transient (ApiClient retries 408 + connection failures only), so these are
            // two distinct calls rather than one call and its retry.
            _transport.Enqueue(HttpResponse.Status(500, "{}"));
            _transport.Enqueue(HttpResponse.Status(200, OkEnvelope));

            LevelUp();
            LevelUp();

            Assert.AreEqual(2, _transport.CallCount);
            Assert.AreNotEqual(_transport.SentBodies[0], _transport.SentBodies[1],
                "A retry after Unavailable is a NEW attempt — the server's replay guard covers the " +
                "case where the first one actually landed. Reusing the key would instead make a " +
                "genuine second level-up impossible.");
        }

        // ── the flag gate ─────────────────────────────────────────────────────────

        [Test]
        public void FlagOff_MakesNoRequestAndAnswersDisabled()
        {
            PointsBackendFlag.Enabled = false;
            _transport.Enqueue(HttpResponse.Status(200, OkEnvelope));

            ProgressLevelUpOutcome outcome = LevelUp();

            Assert.AreEqual(ProgressLevelUpVerdict.Disabled, outcome.Verdict);
            Assert.AreEqual(0, _transport.CallCount, "Flag OFF must not reach the network.");
            Assert.IsNull(outcome.Server, "Disabled carries no server answer; the modal runs its local path.");
        }

        // ── the latch ─────────────────────────────────────────────────────────────

        [Test]
        public void ASecondLevelUpWhileOneIsInFlightIsRefusedRatherThanDoubleCharged()
        {
            // A double-tapped CONFIRM would otherwise fire two requests with two DIFFERENT idempotency
            // keys, and the server would honour both — the replay guard only collapses the same key.
            _transport.Enqueue(HttpResponse.Status(200, OkEnvelope));

            ProgressLevelUpOutcome reentrant = null;
            Pump.Drain(_service.LevelUpRoutine(
                ProgressService.KindCharacter, Ref, 10, 13, 36, 2113,
                _ => reentrant = LevelUp()));   // fired from INSIDE the callback, i.e. still in flight

            Assert.IsNotNull(reentrant);
            Assert.AreEqual(ProgressLevelUpVerdict.Unavailable, reentrant.Verdict);
            Assert.AreEqual(1, _transport.CallCount, "Exactly ONE request may leave the client.");
        }

        [Test]
        public void TheLatchIsReleasedAfterTheCallSoTheNextConfirmWorks()
        {
            _transport.Enqueue(HttpResponse.Status(200, OkEnvelope));
            LevelUp();
            Assert.IsFalse(_service.InFlight);
        }

        // ── ok ────────────────────────────────────────────────────────────────────

        [Test]
        public void Ok_CarriesTheServersCostAndTheRecordedLevel()
        {
            _transport.Enqueue(HttpResponse.Status(200, OkEnvelope));

            ProgressLevelUpOutcome outcome = LevelUp();

            Assert.AreEqual(ProgressLevelUpVerdict.Ok, outcome.Verdict);
            Assert.AreEqual(36, outcome.Cost, "The SERVER's number is what was paid.");
            Assert.AreEqual(13, outcome.Server.Level, "The level the server now RECORDS.");
            Assert.IsTrue(outcome.Server.Grandfathered,
                "The first level-up of a ref seeds the record from the client's claim — the decision " +
                "of record, and the client is told when it happened.");
            Assert.IsFalse(outcome.Server.BlobLevel.HasValue,
                "blob_level rides along ONLY when the blob disagreed.");
        }

        [Test]
        public void Ok_WithABlobMismatchStillSucceedsAndReportsTheBlobLevel()
        {
            // The cross-check LOGS and reports; it never blocks. A malformed or stale blob must not
            // cost the player a level-up they can afford.
            _transport.Enqueue(HttpResponse.Status(200, OkBlobMismatchEnvelope));

            ProgressLevelUpOutcome outcome = LevelUp();

            Assert.AreEqual(ProgressLevelUpVerdict.Ok, outcome.Verdict);
            Assert.AreEqual(8, outcome.Server.BlobLevel);
        }

        [Test]
        public void Ok_FoldsTheBalanceAFTERTheCallbackHasRun()
        {
            // The ordering rule from SpendRoutine, and the reason it exists: onDone is what runs the
            // LOCAL commit, whose per-level LevelUp() calls each debit local RP. Fold first and those
            // debits subtract the same amount a second time.
            _transport.Enqueue(HttpResponse.Status(200, OkEnvelope));

            int balanceSeenInsideCallback = -1;
            Pump.Drain(_service.LevelUpRoutine(ProgressService.KindCharacter, Ref, 10, 13, 36, 2113,
                _ => balanceSeenInsideCallback = _points.Balance));

            Assert.AreEqual(0, balanceSeenInsideCallback,
                "Inside onDone the cache must still be UNTOUCHED (0 = never answered this session).");
            Assert.AreEqual(109, _points.Balance, "…and folded immediately afterwards.");
            Assert.IsTrue(_points.HasBalance);
        }

        [Test]
        public void ReplayedLevelUp_IsStillAnOk()
        {
            // An idempotent replay means the debit AND the record already happened. Refusing here
            // would strand the player having paid for a level the client then declined to show.
            _transport.Enqueue(HttpResponse.Status(200, ReplayedEnvelope));

            ProgressLevelUpOutcome outcome = LevelUp();

            Assert.AreEqual(ProgressLevelUpVerdict.Ok, outcome.Verdict);
            Assert.IsTrue(outcome.Server.Replayed);
            Assert.AreEqual(13, outcome.Server.Level, "The SAME level, not a second step.");
        }

        // ── refusals that carry balances ──────────────────────────────────────────

        [Test]
        public void Insufficient_Arrives200ButMustNotProceed()
        {
            _transport.Enqueue(HttpResponse.Status(200, InsufficientEnvelope));

            ProgressLevelUpOutcome outcome = LevelUp();

            Assert.AreEqual(ProgressLevelUpVerdict.Insufficient, outcome.Verdict);
            Assert.AreEqual(11, outcome.Server.Shortfall);
            Assert.IsFalse(outcome.IsOffline, "Insufficient is definitive — not a connectivity fault.");
            Assert.AreEqual(25, _points.Balance,
                "An insufficient answer still carries the TRUE balance, so the cache learns from it.");
        }

        // ── refusals that carry NO balances (must not touch the cache) ────────────

        [Test]
        public void CostChanged_ReportsThePublishedTotalAndLeavesTheCachedBalanceAlone()
        {
            _transport.Enqueue(HttpResponse.Status(200, CostChangedEnvelope));

            ProgressLevelUpOutcome outcome = LevelUp();

            Assert.AreEqual(ProgressLevelUpVerdict.CostChanged, outcome.Verdict);
            Assert.AreEqual(48, outcome.Cost,
                "The published total for exactly this from→to range — what the modal re-prices at, " +
                "and what the next CONFIRM will pay.");
            Assert.IsFalse(_points.HasBalance,
                "cost_changed never reached spend_pts, so its balance fields are ZEROS. Folding them " +
                "in would blank the player's RP on a refusal that charged them nothing.");
        }

        [Test]
        public void LevelConflict_ReportsTheServersLevelAndLeavesTheCachedBalanceAlone()
        {
            _transport.Enqueue(HttpResponse.Status(200, LevelConflictEnvelope));

            ProgressLevelUpOutcome outcome = LevelUp();

            Assert.AreEqual(ProgressLevelUpVerdict.LevelConflict, outcome.Verdict);
            Assert.AreEqual(12, outcome.ServerLevel, "What the client must reconcile to.");
            Assert.IsFalse(_points.HasBalance, "Same reasoning as cost_changed — no balances to fold.");
        }

        // ── content and client bugs ───────────────────────────────────────────────

        [Test]
        public void CostsMissing_IsNotAvailableNotOk()
        {
            // A gap in level_up_costs. The admin validator refuses to publish one; this is the second
            // lock, and it must never look like a success.
            _transport.Enqueue(HttpResponse.Status(200, CostsMissingEnvelope));

            ProgressLevelUpOutcome outcome = LevelUp();

            Assert.AreEqual(ProgressLevelUpVerdict.NotAvailable, outcome.Verdict);
            Assert.AreEqual(12, outcome.Server.Level, "…and names the level with no published cost.");
        }

        [Test]
        public void InvalidRange_IsNotAvailableNotOk()
        {
            _transport.Enqueue(HttpResponse.Status(200, InvalidRangeEnvelope));
            Assert.AreEqual(ProgressLevelUpVerdict.NotAvailable, LevelUp().Verdict);
        }

        [Test]
        public void TheKillSwitchIsNotAvailable()
        {
            _transport.Enqueue(HttpResponse.Status(200, NotAvailableEnvelope));

            ProgressLevelUpOutcome outcome = LevelUp();

            Assert.AreEqual(ProgressLevelUpVerdict.NotAvailable, outcome.Verdict);
            Assert.IsTrue(outcome.Server.IsDisabled,
                "The operator pulled the cost table (or all content). Distinguishable in the log, " +
                "the same toast to the player.");
        }

        [Test]
        public void AStatusThisBuildDoesNotKnowIsNotAvailableNotOk()
        {
            _transport.Enqueue(HttpResponse.Status(200, "{\"data\":{\"status\":\"invented_later\"}}"));
            Assert.AreEqual(ProgressLevelUpVerdict.NotAvailable, LevelUp().Verdict);
        }

        // ── refused before the wire ───────────────────────────────────────────────

        [Test]
        public void ABackwardsRangeNeverReachesTheNetwork()
        {
            // The server refuses this too (invalid_range/order). Refusing here as well means an
            // obvious client bug costs a log line rather than a round trip.
            _transport.Enqueue(HttpResponse.Status(200, OkEnvelope));

            // The refusal is deliberately a LogError — a call site asking for 13 → 13 is a bug in
            // that call site, not a state the player can reach, and it should be loud.
            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Error,
                new System.Text.RegularExpressions.Regex("bad range"));

            ProgressLevelUpOutcome outcome = LevelUp(from: 13, to: 13);

            Assert.AreEqual(ProgressLevelUpVerdict.Unavailable, outcome.Verdict);
            Assert.AreEqual(0, _transport.CallCount);
        }

        // ── transport ─────────────────────────────────────────────────────────────

        [Test]
        public void ConnectionFailure_IsUnavailableNotInsufficient()
        {
            // Every retry attempt fails: the caller must be told "connection required", never "broke".
            _transport.Enqueue(HttpResponse.ConnectionFailure("dns failure"));
            _transport.Enqueue(HttpResponse.ConnectionFailure("dns failure"));
            _transport.Enqueue(HttpResponse.ConnectionFailure("dns failure"));

            ProgressLevelUpOutcome outcome = LevelUp();

            Assert.AreEqual(ProgressLevelUpVerdict.Unavailable, outcome.Verdict);
            Assert.IsTrue(outcome.IsOffline,
                "Collapsing 'offline' into 'insufficient' is how a dropped connection tells a player " +
                "they are broke.");
            Assert.IsNull(outcome.Server);
        }

        [Test]
        public void ServerError_IsUnavailable()
        {
            _transport.Enqueue(HttpResponse.Status(500, "{}"));
            Assert.AreEqual(ProgressLevelUpVerdict.Unavailable, LevelUp().Verdict);
        }

        [Test]
        public void TheRequestGoesToTheProgressEndpointNotToPointsSpend()
        {
            // The whole point of the task: a level-up is no longer a /points/spend with a
            // client-computed amount.
            _transport.Enqueue(HttpResponse.Status(200, OkEnvelope));

            LevelUp();

            StringAssert.EndsWith("/progress/level-up", _transport.SentUrls[0]);
        }
    }
}
