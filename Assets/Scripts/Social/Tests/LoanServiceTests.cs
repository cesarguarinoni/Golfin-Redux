// asset_loans — EditMode tests for the loan service and its wire shapes.
//
// WHAT THESE EXIST TO CATCH, and every one of them is invisible on screen until it is expensive:
//
//   * A MISSPELLED snake_case FIELD. The lend body mirrors `loans.py::LendRequest`; one wrong key
//     is a silent 422 that reads to the player as "lending is broken".
//   * THE LIVENESS PREDICATE DRIFTING FROM THE SERVER'S. `status = 'active' and now() < ends_at`
//     is written in three places (the migration, the router, `LoanDto.IsLive`); a client that
//     thinks an expired loan is live shows a RETURN button that answers `not_active`.
//   * THE ENDED-LOAN SIDE. An ended loan arrives in neither the live `out` nor the live `in` list,
//     so which side the player was on has to be REMEMBERED from the payload. Getting it backwards
//     means the lender's catch-up runs on the borrower's device.
//   * RE-TOASTING FOR FOURTEEN DAYS. The server reports ended loans for a fortnight so an offline
//     client can reconcile; without the `firstTime` flag every Roster entry re-applies and
//     re-toasts them.
//   * A REFUSAL READ AS A FAILURE. Every business outcome is HTTP 200 with a `status`; treating
//     one as a transport error retries a decision that will never change.
//
// Every test drives the REAL service through the REAL ApiClient over a scripted transport.
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Golfin.Net;
using Golfin.Net.Tests;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Golfin.Social.Tests
{
    public class LoanServiceTests
    {
        private FakeHttpTransport _transport;
        private ApiClient _client;
        private LoanService _service;

        private const string CharRef = "char_kai";
        private const string ClubRef = "club_driver_x";

        [SetUp]
        public void SetUp()
        {
            _transport = new FakeHttpTransport();
            _client = new ApiClient(_transport, new FakeAuthTokenProvider(), new ImmediateCoroutineRunner())
            {
                MaxTransientRetries = 0,
                RetryDelaySeconds = 0f,
                LogRequests = false,
            };
            _service = new LoanService(_client);
        }

        [TearDown]
        public void TearDown()
        {
            LoanService.ResetForTest();
            Endpoints.ResetToDefault();
        }

        private static void Pump(IEnumerator routine)
        {
            while (routine.MoveNext()) { }
        }

        // ════════════════════════════════════════════════════════════════════
        // The wire body
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public void LendJson_MirrorsTheRouterFieldNames()
        {
            JObject o = JObject.Parse(
                LoanService.BuildLendJson("character", CharRef, "borrower-1", 3, 42, "key-1"));

            Assert.AreEqual("character", (string)o["kind"]);
            Assert.AreEqual(CharRef, (string)o["ref_id"]);
            Assert.AreEqual("borrower-1", (string)o["borrower_id"]);
            Assert.AreEqual(3, (int)o["days"]);
            Assert.AreEqual(42, (int)o["level"]);
            Assert.AreEqual("key-1", (string)o["idempotency_key"]);
        }

        [Test]
        public void Lend_PostsToTheLoansEndpoint()
        {
            _transport.Enqueue(HttpResponse.Status(200, "{\"data\":{\"status\":\"ok\"}}"));
            Pump(_service.Lend("club", ClubRef, "b1", 7, 12, "k", _ => { }));

            Assert.AreEqual("POST", _transport.SentMethods[0]);
            StringAssert.EndsWith("/loans", _transport.SentUrls[0]);
        }

        [Test]
        public void Return_PostsToTheLoansReturnEndpoint()
        {
            _transport.Enqueue(HttpResponse.Status(200, "{\"data\":{\"status\":\"ok\"}}"));
            Pump(_service.Return("loan-99", _ => { }));

            Assert.AreEqual("POST", _transport.SentMethods[0]);
            StringAssert.EndsWith("/loans/loan-99/return", _transport.SentUrls[0]);
        }

        // ════════════════════════════════════════════════════════════════════
        // Refusals are 200 payloads, not failures
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public void ARefusalIsASuccessfulCallCarryingAStatus()
        {
            _transport.Enqueue(HttpResponse.Status(200, "{\"data\":{\"status\":\"not_following\"}}"));

            ApiResult<LoanMutationDto> result = null;
            Pump(_service.Lend("character", CharRef, "b1", 3, 1, "k", r => result = r));

            Assert.IsTrue(result.Success, "a refusal is a 200 — treating it as a failure retries forever");
            Assert.IsFalse(result.Data.IsOk);
            Assert.AreEqual("LOAN_ERR_NOT_FOLLOWING", result.Data.ErrorKey());
        }

        [Test]
        public void EveryKnownRefusalMapsToItsOwnKey()
        {
            var expected = new Dictionary<string, string>
            {
                { "not_following",   "LOAN_ERR_NOT_FOLLOWING" },
                { "already_on_loan", "LOAN_ERR_ALREADY_ON_LOAN" },
                { "borrower_has_it", "LOAN_ERR_BORROWER_HAS_IT" },
                { "limit_out",       "LOAN_ERR_LIMIT_OUT" },
                { "limit_in",        "LOAN_ERR_LIMIT_IN" },
            };

            foreach (var kv in expected)
                Assert.AreEqual(kv.Value, new LoanMutationDto { Status = kv.Key }.ErrorKey(), kv.Key);
        }

        [Test]
        public void AnUnknownRefusalStillSaysSomething()
        {
            // A status this build has never heard of must not produce a BLANK toast — that reads to
            // the player as "the tap did nothing".
            Assert.AreEqual("LOAN_ERR_GENERIC",
                            new LoanMutationDto { Status = "some_future_status" }.ErrorKey());
        }

        // ════════════════════════════════════════════════════════════════════
        // Parsing, and the liveness predicate
        // ════════════════════════════════════════════════════════════════════

        private static string Iso(double hoursFromNow)
            => System.DateTime.UtcNow.AddHours(hoursFromNow).ToString("o");

        private static string LoanJson(string id, string kind, string refId, double endsInHours,
                                       string status = "active", int level = 42, int? levelAtEnd = null)
            => "{" +
               $"\"id\":\"{id}\",\"kind\":\"{kind}\",\"ref_id\":\"{refId}\"," +
               "\"lender\":{\"id\":\"L\",\"display_name\":\"KENJI\",\"avatar_url\":null,\"avatar_level\":9}," +
               "\"borrower\":{\"id\":\"B\",\"display_name\":\"MARTA\",\"avatar_url\":null,\"avatar_level\":3}," +
               $"\"days\":3,\"starts_at\":\"{Iso(-24)}\",\"ends_at\":\"{Iso(endsInHours)}\"," +
               $"\"ended_at\":null,\"status\":\"{status}\",\"level\":{level},\"level_at_start\":40," +
               $"\"level_at_end\":{(levelAtEnd.HasValue ? levelAtEnd.Value.ToString() : "null")}," +
               "\"lender_share_bp\":2000,\"rp_to_lender\":140,\"rp_to_borrower\":560}";

        private void Refresh(string outJson, string inJson)
        {
            _transport.Enqueue(HttpResponse.Status(200,
                "{\"data\":{\"out\":[" + outJson + "],\"in\":[" + inJson + "]}}"));
            Pump(_service.Refresh());
        }

        [Test]
        public void ALiveOutLoanLandsInOutAndIsFoundByRef()
        {
            Refresh(LoanJson("l1", "character", CharRef, 52), "");

            Assert.AreEqual(1, _service.Out.Count);
            Assert.AreEqual(0, _service.In.Count);
            Assert.IsTrue(_service.IsLentOut("character", CharRef, out LoanDto loan));
            Assert.AreEqual("MARTA", loan.Borrower.Name);
            Assert.AreEqual(20, loan.SharePercent, "the terms line reads the share off the ROW, never a constant");
        }

        [Test]
        public void ALiveInLoanLandsInIn()
        {
            Refresh("", LoanJson("l2", "club", ClubRef, 5));

            Assert.IsTrue(_service.IsBorrowed("club", ClubRef, out LoanDto loan));
            Assert.AreEqual("KENJI", loan.Lender.Name);
            Assert.IsFalse(_service.IsLentOut("club", ClubRef));
        }

        [Test]
        public void APastEndsAtIsNotLiveEvenWhileStatusSaysActive()
        {
            // The server's expiry is LAZY, so a row can still say `active` after its clock ran out.
            // The client applies the same predicate the server does rather than trusting `status`.
            Refresh(LoanJson("l3", "character", CharRef, -1), "");

            Assert.AreEqual(0, _service.Out.Count, "an expired loan must not be live");
            Assert.AreEqual(1, _service.Ended.Count);
            Assert.IsFalse(_service.IsLentOut("character", CharRef));
        }

        [Test]
        public void AReturnedLoanIsEndedRegardlessOfItsClock()
        {
            Refresh(LoanJson("l4", "character", CharRef, 40, status: "returned"), "");

            Assert.AreEqual(0, _service.Out.Count);
            Assert.AreEqual(1, _service.Ended.Count);
        }

        [Test]
        public void AFailedRefreshKeepsTheLastList()
        {
            Refresh(LoanJson("l5", "character", CharRef, 30), "");
            Assert.AreEqual(1, _service.Out.Count);

            _transport.Enqueue(HttpResponse.ConnectionFailure());
            Pump(_service.Refresh());

            // "We could not reach the server" and "you have no loans" produce completely different
            // screens, and only one of them is true.
            Assert.AreEqual(1, _service.Out.Count, "an unreachable server must not clear the list");
        }

        [Test]
        public void AnUnparseableEndsAtIsTreatedAsLive()
        {
            // Ending a real loan over a parse hiccup is the more expensive mistake, and it is the
            // choice the server makes too.
            var dto = new LoanDto { Status = "active", EndsAt = "not-a-date" };
            Assert.IsTrue(dto.IsLive());
            Assert.IsNull(dto.EndsAtUtc);
        }

        // ════════════════════════════════════════════════════════════════════
        // Reconciliation
        // ════════════════════════════════════════════════════════════════════

        private sealed class RecordingReconciler : ILoanReconciler
        {
            public readonly List<string> Calls = new List<string>();
            public readonly HashSet<string> Seen = new HashSet<string>();

            public void EnsureBorrowed(LoanDto loan) => Calls.Add("ensure:" + loan.RefId);
            public void RemoveBorrowed(LoanDto loan, bool first) => Calls.Add($"remove:{loan.RefId}:{first}");
            public void MarkLentOut(LoanDto loan) => Calls.Add("lock:" + loan.RefId);
            public void ClearLentOut(LoanDto loan, bool first) => Calls.Add($"unlock:{loan.RefId}:{first}");
            public void ReconcileFinished(bool changed) => Calls.Add("finished:" + changed);
            public bool WasReconciled(string id) => Seen.Contains(id);
            public void MarkReconciled(string id) => Seen.Add(id);
        }

        [Test]
        public void LiveLoansBorrowOnTheBorrowerAndLockOnTheLender()
        {
            var rec = new RecordingReconciler();
            _service.Reconciler = rec;

            Refresh(LoanJson("l6", "character", CharRef, 20),
                    LoanJson("l7", "club", ClubRef, 20));

            CollectionAssert.Contains(rec.Calls, "lock:" + CharRef);
            CollectionAssert.Contains(rec.Calls, "ensure:" + ClubRef);
        }

        [Test]
        public void AnEndedLoanIsAppliedOnTheSideItArrivedOn()
        {
            // An ended loan is in NEITHER live list, so the side has to be remembered from the
            // payload. Getting it backwards runs the lender's level catch-up on the borrower.
            var rec = new RecordingReconciler();
            _service.Reconciler = rec;

            Refresh(LoanJson("out-1", "character", CharRef, -2),      // ours, expired
                    LoanJson("in-1", "club", ClubRef, -2));            // theirs, expired

            CollectionAssert.Contains(rec.Calls, "unlock:" + CharRef + ":True");
            CollectionAssert.Contains(rec.Calls, "remove:" + ClubRef + ":True");
        }

        [Test]
        public void AnEndedLoanIsAppliedOnceEvenThoughTheServerReportsItForFourteenDays()
        {
            var rec = new RecordingReconciler();
            _service.Reconciler = rec;

            Refresh(LoanJson("out-1", "character", CharRef, -2), "");
            Assert.AreEqual(1, rec.Calls.Count(c => c == "unlock:" + CharRef + ":True"));

            rec.Calls.Clear();
            Refresh(LoanJson("out-1", "character", CharRef, -2), "");

            Assert.AreEqual(0, rec.Calls.Count(c => c.EndsWith(":True")),
                            "the second sighting of the same ended loan must not re-apply or re-toast");
            CollectionAssert.Contains(rec.Calls, "unlock:" + CharRef + ":False");
        }

        [Test]
        public void ReconcileIsANoOpWithNoReconciler()
        {
            // An EditMode test — and the very first frames of boot — run with no reconciler. The
            // list must still parse and the call must not throw.
            Refresh(LoanJson("l8", "character", CharRef, 10), "");
            Assert.AreEqual(1, _service.Out.Count);
            Assert.DoesNotThrow(() => _service.Reconcile());
        }

        // ════════════════════════════════════════════════════════════════════
        // The round snapshot
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public void TheRoundSnapshotNamesOnlyBorrowedAssetsInPlay()
        {
            Refresh(LoanJson("out-1", "character", "char_mine", 20),   // ours: never splits
                    LoanJson("in-1", "character", CharRef, 20) + "," +
                    LoanJson("in-2", "club", ClubRef, 20));

            _service.SnapshotRoundLoans(CharRef, new[] { ClubRef, "club_owned_by_me" });
            List<string> ids = _service.UsedLoanIdsForRound();

            CollectionAssert.AreEquivalent(new[] { "in-1", "in-2" }, ids);
        }

        [Test]
        public void ARoundWithNothingBorrowedNamesNoLoansAtAll()
        {
            Refresh("", "");
            _service.SnapshotRoundLoans("char_mine", new[] { "club_mine" });

            // NULL, not an empty list: the field is then omitted from the request entirely and the
            // earn is byte-identical to what it was before loans existed.
            Assert.IsNull(_service.UsedLoanIdsForRound());
        }

        [Test]
        public void TheSnapshotSurvivesALoanEndingMidRound()
        {
            Refresh("", LoanJson("in-1", "character", CharRef, 20));
            _service.SnapshotRoundLoans(CharRef, null);

            // The loan ends while the round is still being played.
            Refresh("", LoanJson("in-1", "character", CharRef, -1));

            CollectionAssert.AreEquivalent(new[] { "in-1" }, _service.UsedLoanIdsForRound(),
                "the snapshot is frozen at round START — the server drops a loan that is no longer live");
        }

        [Test]
        public void ClearRoundLoansDropsTheSnapshot()
        {
            Refresh("", LoanJson("in-1", "character", CharRef, 20));
            _service.SnapshotRoundLoans(CharRef, null);
            Assert.IsNotNull(_service.UsedLoanIdsForRound());

            _service.ClearRoundLoans();
            Assert.IsNull(_service.UsedLoanIdsForRound());
        }

        // ════════════════════════════════════════════════════════════════════
        // Time formatting is on the DTO, so a panel cannot get it subtly different
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public void TimeLeftNeverGoesNegative()
        {
            var dto = new LoanDto { Status = "active", EndsAt = Iso(-5) };
            Assert.AreEqual(System.TimeSpan.Zero, dto.TimeLeft());
        }

        [Test]
        public void TheSharePercentComesOffTheRow()
        {
            Assert.AreEqual(20, new LoanDto { LenderShareBp = 2000 }.SharePercent);
            Assert.AreEqual(35, new LoanDto { LenderShareBp = 3500 }.SharePercent);
        }
    }
}
