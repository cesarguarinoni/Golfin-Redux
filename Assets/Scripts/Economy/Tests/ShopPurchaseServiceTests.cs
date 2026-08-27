// Order: shop_server_purchase §3.7 — the server-priced purchase call.
using Golfin.Net;
using Golfin.Net.Tests;
using NUnit.Framework;

namespace Golfin.Economy.Tests
{
    /// <summary>
    /// The shop is the one flow where the CLIENT used to pick the number it was charged, so what is
    /// worth pinning here is that every server answer lands on a distinct verdict and that the two
    /// answers which carry no balances never touch the cached one.
    ///
    ///   • ok             → Ok            (apply the grant, debit the SERVER's `charged`)
    ///   • insufficient   → Insufficient  (nothing written; the balance IS carried and folded)
    ///   • price_changed  → PriceChanged  (nothing written; NO balances — must not fold)
    ///   • not_listed     → NotListed     (nothing written; NO balances — must not fold)
    ///   • already_owned  → AlreadyOwned
    ///   • unknown_entry / unsupported_category / anything new → Unknown
    ///   • offline / 5xx  → Unavailable
    ///   • flag OFF       → Disabled      (no request at all)
    ///
    /// The sharp ones are <c>price_changed</c> and <c>not_listed</c>: they arrive as HTTP **200** like
    /// every other refusal, and their payloads have zeros in <c>total_points</c> because the server
    /// never reached <c>spend_pts</c>. Folding those zeros into the cache would blank the player's RP
    /// on a refusal that charged them nothing — which is why two of the tests below assert the cached
    /// balance did NOT move rather than asserting only on the verdict.
    /// </summary>
    public class ShopPurchaseServiceTests
    {
        private FakeHttpTransport _transport;
        private ApiClient _client;
        private PointsService _points;
        private ShopPurchaseService _service;

        private const string Entry = "shop_club_iron9_klyro";

        private const string OkEnvelope =
            "{\"data\":{\"status\":\"ok\",\"entry_id\":\"" + Entry + "\",\"category\":\"club\"," +
            "\"ref_id\":\"club_iron9_klyro\",\"charged\":150,\"list_rp\":200,\"on_sale\":true," +
            "\"grant\":{\"id\":\"g-1\",\"kind\":\"club\",\"ref_id\":\"club_iron9_klyro\",\"amount\":1," +
            "\"note\":\"shop:" + Entry + "\"}," +
            "\"spent\":150,\"from_activity\":150,\"from_gift\":0," +
            "\"activity_pts\":95,\"gift_pts\":50,\"total_points\":145,\"replayed\":false}}";

        private const string ReplayedEnvelope =
            "{\"data\":{\"status\":\"ok\",\"entry_id\":\"" + Entry + "\",\"category\":\"club\"," +
            "\"ref_id\":\"club_iron9_klyro\",\"charged\":150,\"list_rp\":200,\"on_sale\":true," +
            "\"grant\":{\"id\":\"g-1\",\"kind\":\"club\",\"ref_id\":\"club_iron9_klyro\",\"amount\":1}," +
            "\"spent\":150,\"from_activity\":150,\"from_gift\":0," +
            "\"activity_pts\":95,\"gift_pts\":50,\"total_points\":145,\"replayed\":true}}";

        private const string InsufficientEnvelope =
            "{\"data\":{\"status\":\"insufficient\",\"requested\":150,\"shortfall\":55," +
            "\"activity_pts\":45,\"gift_pts\":50,\"total_points\":95,\"replayed\":false}}";

        private const string PriceChangedEnvelope =
            "{\"data\":{\"status\":\"price_changed\",\"price\":200,\"list_rp\":200,\"on_sale\":false}}";

        private const string NotListedEnvelope =
            "{\"data\":{\"status\":\"not_listed\",\"reason\":\"window\"}}";

        private const string AlreadyOwnedEnvelope =
            "{\"data\":{\"status\":\"already_owned\",\"ref_id\":\"club_iron9_klyro\"}}";

        private const string UnsupportedEnvelope =
            "{\"data\":{\"status\":\"unsupported_category\",\"category\":\"bag\"}}";

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

            _service = new ShopPurchaseService(_client);
            ShopPurchaseService.ConfigureForTest(_service);

            PointsBackendFlag.Enabled = true; // per-test default; the OFF case sets it back explicitly
        }

        [TearDown]
        public void TearDown()
        {
            PointsBackendFlag.ResetToDefault();
            ShopPurchaseService.ResetForTest();
            PointsService.ResetForTest();
            ApiClient.ResetForTest();
        }

        private ShopPurchaseOutcome Purchase(int expected = 150, int build = 2113)
        {
            ShopPurchaseOutcome outcome = null;
            Pump.Drain(_service.PurchaseRoutine(Entry, expected, build, o => outcome = o));
            return outcome;
        }

        // ── the wire shape ────────────────────────────────────────────────────────

        [Test]
        public void BuildPurchaseJson_MatchesTheDeployedRequestModel()
        {
            string json = ShopPurchaseService.BuildPurchaseJson(Entry, 150, 2113, "key-1");

            StringAssert.Contains("\"entry_id\":\"" + Entry + "\"", json);
            StringAssert.Contains("\"idempotency_key\":\"key-1\"", json);
            StringAssert.Contains("\"build\":2113", json);
            StringAssert.Contains("\"expected_rp_cost\":150", json);
            StringAssert.DoesNotContain("user_id", json,
                "The server stamps the caller from the token. A user id in the body would be the exact " +
                "hole this endpoint exists to close.");
            StringAssert.DoesNotContain("\"amount\"", json,
                "A purchase must never send a price. It sends WHICH LISTING; the server prices it.");
        }

        [Test]
        public void BuildPurchaseJson_SendsNullNotZeroWhenThereIsNoExpectedPrice()
        {
            // null means "do not guard". 0 would mean "I expect this to be free" and would refuse
            // every real listing with price_changed.
            string json = ShopPurchaseService.BuildPurchaseJson(Entry, 0, 2113, "key-1");
            StringAssert.Contains("\"expected_rp_cost\":null", json);
        }

        [Test]
        public void BuildPurchaseJson_ClampsANegativeBuildToZero()
        {
            // build=0 is the safe end: the server then serves only rows every build can render.
            string json = ShopPurchaseService.BuildPurchaseJson(Entry, 150, -5, "key-1");
            StringAssert.Contains("\"build\":0", json);
        }

        [Test]
        public void EveryAttemptSendsAFreshIdempotencyKey()
        {
            // 500 is NOT a transient (ApiClient retries 408 + connection failures only), so these are
            // two distinct calls rather than one call and its retry.
            _transport.Enqueue(HttpResponse.Status(500, "{}"));
            _transport.Enqueue(HttpResponse.Status(200, OkEnvelope));

            Purchase();
            Purchase();

            Assert.AreEqual(2, _transport.CallCount);
            string first = _transport.SentBodies[0];
            string second = _transport.SentBodies[1];

            Assert.AreNotEqual(first, second,
                "A retry after Unavailable is a NEW attempt — the server's replay guard covers the " +
                "case where the first one actually landed. Reusing the key would instead make a " +
                "genuine second purchase impossible.");
        }

        // ── the flag gate ─────────────────────────────────────────────────────────

        [Test]
        public void FlagOff_MakesNoRequestAndAnswersDisabled()
        {
            PointsBackendFlag.Enabled = false;
            _transport.Enqueue(HttpResponse.Status(200, OkEnvelope));

            ShopPurchaseOutcome outcome = Purchase();

            Assert.AreEqual(ShopPurchaseVerdict.Disabled, outcome.Verdict);
            Assert.AreEqual(0, _transport.CallCount, "Flag OFF must not reach the network.");
            Assert.IsNull(outcome.Grant, "Disabled grants nothing — ShopTransaction runs its local path.");
        }

        // ── ok ────────────────────────────────────────────────────────────────────

        [Test]
        public void Ok_CarriesTheServersChargedAmountAndTheGrant()
        {
            _transport.Enqueue(HttpResponse.Status(200, OkEnvelope));

            ShopPurchaseOutcome outcome = Purchase();

            Assert.AreEqual(ShopPurchaseVerdict.Ok, outcome.Verdict);
            Assert.AreEqual(150, outcome.Charged, "The SERVER's number is what the client debits.");
            Assert.AreEqual(200, outcome.Server.ListRp);
            Assert.IsTrue(outcome.Server.OnSale);
            Assert.IsNotNull(outcome.Grant);
            Assert.AreEqual("g-1", outcome.Grant.Id);
            Assert.AreEqual("club", outcome.Grant.Kind);
            Assert.AreEqual("club_iron9_klyro", outcome.Grant.RefId);
            Assert.AreEqual(1, outcome.Grant.Amount);
        }

        [Test]
        public void Ok_FoldsTheBalanceAFTERTheCallbackHasRun()
        {
            // The ordering rule from SpendRoutine, and the reason it exists: onDone is what runs the
            // LOCAL debit. Fold first and the local debit subtracts the same amount a second time.
            _transport.Enqueue(HttpResponse.Status(200, OkEnvelope));

            int balanceSeenInsideCallback = -1;
            Pump.Drain(_service.PurchaseRoutine(Entry, 150, 2113,
                _ => balanceSeenInsideCallback = _points.Balance));

            Assert.AreEqual(0, balanceSeenInsideCallback,
                "Inside onDone the cache must still be UNTOUCHED (0 = never answered this session).");
            Assert.AreEqual(145, _points.Balance, "…and folded immediately afterwards.");
            Assert.IsTrue(_points.HasBalance);
        }

        [Test]
        public void ReplayedPurchase_IsStillAnOk()
        {
            // An idempotent replay means the debit AND the grant already happened. Refusing here would
            // strand the player having paid for something the client then declined to hand over.
            _transport.Enqueue(HttpResponse.Status(200, ReplayedEnvelope));

            ShopPurchaseOutcome outcome = Purchase();

            Assert.AreEqual(ShopPurchaseVerdict.Ok, outcome.Verdict);
            Assert.IsTrue(outcome.Server.Replayed);
            Assert.AreEqual("g-1", outcome.Grant.Id, "The SAME grant, not a second one.");
        }

        // ── refusals that carry balances ──────────────────────────────────────────

        [Test]
        public void Insufficient_Arrives200ButMustNotProceed()
        {
            _transport.Enqueue(HttpResponse.Status(200, InsufficientEnvelope));

            ShopPurchaseOutcome outcome = Purchase();

            Assert.AreEqual(ShopPurchaseVerdict.Insufficient, outcome.Verdict);
            Assert.AreEqual(55, outcome.Server.Shortfall);
            Assert.IsNull(outcome.Grant, "Nothing was written, so nothing may be applied.");
            Assert.IsFalse(outcome.IsOffline, "Insufficient is definitive — not a connectivity fault.");
            Assert.AreEqual(95, _points.Balance,
                "An insufficient answer still carries the TRUE balance, so the cache learns from it.");
        }

        // ── refusals that carry NO balances (must not touch the cache) ────────────

        [Test]
        public void PriceChanged_ReportsThePublishedPriceAndLeavesTheCachedBalanceAlone()
        {
            _transport.Enqueue(HttpResponse.Status(200, PriceChangedEnvelope));

            ShopPurchaseOutcome outcome = Purchase();

            Assert.AreEqual(ShopPurchaseVerdict.PriceChanged, outcome.Verdict);
            Assert.AreEqual(200, outcome.Server.Price, "The price the card must re-render at.");
            Assert.IsNull(outcome.Grant);
            Assert.IsFalse(_points.HasBalance,
                "price_changed never reached spend_pts, so its balance fields are ZEROS. Folding them " +
                "in would blank the player's RP on a refusal that charged them nothing.");
        }

        [Test]
        public void NotListed_ReportsTheReasonAndLeavesTheCachedBalanceAlone()
        {
            _transport.Enqueue(HttpResponse.Status(200, NotListedEnvelope));

            ShopPurchaseOutcome outcome = Purchase();

            Assert.AreEqual(ShopPurchaseVerdict.NotListed, outcome.Verdict);
            Assert.AreEqual("window", outcome.Server.Reason);
            Assert.IsFalse(_points.HasBalance, "Same reasoning as price_changed — no balances to fold.");
        }

        [Test]
        public void AlreadyOwned_IsItsOwnVerdict()
        {
            _transport.Enqueue(HttpResponse.Status(200, AlreadyOwnedEnvelope));

            ShopPurchaseOutcome outcome = Purchase();

            Assert.AreEqual(ShopPurchaseVerdict.AlreadyOwned, outcome.Verdict);
            Assert.IsNull(outcome.Grant);
        }

        [Test]
        public void AnUnsellableCategoryIsUnknownNotOk()
        {
            // `bag` is publishable from the admin but not grantable. It must never look like a success.
            _transport.Enqueue(HttpResponse.Status(200, UnsupportedEnvelope));

            ShopPurchaseOutcome outcome = Purchase();

            Assert.AreEqual(ShopPurchaseVerdict.Unknown, outcome.Verdict);
            Assert.IsNull(outcome.Grant);
        }

        [Test]
        public void AStatusThisBuildDoesNotKnowIsUnknownNotOk()
        {
            _transport.Enqueue(HttpResponse.Status(200, "{\"data\":{\"status\":\"invented_later\"}}"));

            ShopPurchaseOutcome outcome = Purchase();

            Assert.AreEqual(ShopPurchaseVerdict.Unknown, outcome.Verdict);
        }

        // ── transport ─────────────────────────────────────────────────────────────

        [Test]
        public void ConnectionFailure_IsUnavailableNotInsufficient()
        {
            // Every retry attempt fails: the caller must be told "connection required", never "broke".
            _transport.Enqueue(HttpResponse.ConnectionFailure("dns failure"));
            _transport.Enqueue(HttpResponse.ConnectionFailure("dns failure"));
            _transport.Enqueue(HttpResponse.ConnectionFailure("dns failure"));

            ShopPurchaseOutcome outcome = Purchase();

            Assert.AreEqual(ShopPurchaseVerdict.Unavailable, outcome.Verdict);
            Assert.IsTrue(outcome.IsOffline,
                "Collapsing 'offline' into 'insufficient' is how a dropped connection tells a player " +
                "they are broke.");
            Assert.IsNull(outcome.Grant);
        }

        [Test]
        public void ServerError_IsUnavailable()
        {
            _transport.Enqueue(HttpResponse.Status(500, "{}"));

            ShopPurchaseOutcome outcome = Purchase();

            Assert.AreEqual(ShopPurchaseVerdict.Unavailable, outcome.Verdict);
        }

        // ── the latch ─────────────────────────────────────────────────────────────

        [Test]
        public void AnOverlappingPurchaseIsRefusedRatherThanSentTwice()
        {
            // A double-tapped BUY would otherwise fire two requests with two DIFFERENT idempotency
            // keys, and the server would honour both — the replay guard only collapses the same key.
            _transport.Enqueue(HttpResponse.Status(200, OkEnvelope));

            ShopPurchaseOutcome reentrant = null;
            Pump.Drain(_service.PurchaseRoutine(Entry, 150, 2113, _ =>
            {
                // Still inside the first call, so the latch is set.
                Pump.Drain(_service.PurchaseRoutine(Entry, 150, 2113, o2 => reentrant = o2));
            }));

            Assert.IsNotNull(reentrant);
            Assert.AreEqual(ShopPurchaseVerdict.Unavailable, reentrant.Verdict);
            Assert.AreEqual(1, _transport.CallCount, "Exactly ONE request may leave for a double tap.");
        }

        [Test]
        public void TheLatchIsReleasedAfterAFailureSoARetryCanRun()
        {
            _transport.Enqueue(HttpResponse.Status(500, "{}"));
            _transport.Enqueue(HttpResponse.Status(200, OkEnvelope));

            Assert.AreEqual(ShopPurchaseVerdict.Unavailable, Purchase().Verdict);
            Assert.IsFalse(_service.InFlight, "A failed purchase must not wedge the latch shut.");
            Assert.AreEqual(ShopPurchaseVerdict.Ok, Purchase().Verdict);
        }

        // ── the spend-result projection ───────────────────────────────────────────

        [Test]
        public void ToSpendResult_CarriesEveryFieldPointsServiceNeeds()
        {
            _transport.Enqueue(HttpResponse.Status(200, OkEnvelope));
            ShopPurchaseResult data = Purchase().Server;

            PointsSpendResult spend = data.ToSpendResult();

            Assert.IsTrue(spend.IsOk);
            Assert.AreEqual(150, spend.Spent);
            Assert.AreEqual(150, spend.FromActivity);
            Assert.AreEqual(0, spend.FromGift);
            Assert.AreEqual(95, spend.ActivityPts);
            Assert.AreEqual(50, spend.GiftPts);
            Assert.AreEqual(145, spend.TotalPoints);
            Assert.IsFalse(spend.Replayed);
        }

        [Test]
        public void ToSpendResult_KeepsAnInsufficientStatusRatherThanForcingItToOk()
        {
            _transport.Enqueue(HttpResponse.Status(200, InsufficientEnvelope));
            ShopPurchaseResult data = Purchase().Server;

            Assert.IsTrue(data.ToSpendResult().IsInsufficient);
        }
    }
}
