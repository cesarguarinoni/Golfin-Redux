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
// asset_loans_offers adds THE PREDICATE SPLIT, and it is the most expensive thing here to get
// wrong because both mistakes are silent:
//
//   * AN OFFER TREATED AS LIVE would put gear nobody accepted into the recipient's roster and
//     pay the lender a cut of rounds played with an asset that never changed hands.
//   * AN OFFER TREATED AS FREE would let a lender walk into a match with something the recipient
//     is one tap from taking, and let the same asset be offered to five people at once.
//
// `IsLive` answers the first, `IsLocked` the second, and `Out` is filtered on the SECOND — which
// is what makes every existing lock path (select, equip, level up, both panels) hold for an
// offered asset with no new code. The tests below pin each of those separately.
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
            UserService.ResetForTest();
            Endpoints.ResetToDefault();
        }

        /// <summary>
        /// Give <see cref="LoanService.Following"/> a user id WITHOUT touching
        /// <c>UserService.Instance</c>'s lazy constructor.
        ///
        /// <para>
        /// ⚠️ <c>UserService.Instance</c> builds <c>ApiClient.Instance</c>, whose constructor
        /// creates the <c>[Golfin.Net]</c> coroutine host and calls <c>DontDestroyOnLoad</c> —
        /// which THROWS outside play mode. `Following` reads `LastDetail.Id` to build its URL, so
        /// any test that drives it takes the suite down unless the singleton is pre-seeded.
        /// (Same trap, same fix, as <c>LoanSyncBehaviour.SnapshotRoundLoans</c>'s edit-mode guard.)
        /// </para>
        /// </summary>
        private void SeedMe(string id = "me-1")
        {
            var svc = new UserService(_client);
            svc.SetDetailForTest(new UserDetailDto { Id = id });
            UserService.ConfigureForTest(svc);
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

        /// <summary>
        /// A PENDING OFFER row: `offered`, NO starts_at, NO ends_at, and its own clock.
        ///
        /// <para>The null timestamps are load-bearing rather than lazy fixture-writing — a
        /// placeholder `ends_at` would make <c>IsLive</c> accidentally answer True on an offer,
        /// which is the exact bug the split predicates exist to prevent.</para>
        /// </summary>
        private static string OfferJson(string id, string kind, string refId,
                                        double expiresInHours = 46, string status = "offered",
                                        int level = 42)
            => "{" +
               $"\"id\":\"{id}\",\"kind\":\"{kind}\",\"ref_id\":\"{refId}\"," +
               "\"lender\":{\"id\":\"L\",\"display_name\":\"KENJI\",\"avatar_url\":null,\"avatar_level\":9}," +
               "\"borrower\":{\"id\":\"B\",\"display_name\":\"MARTA\",\"avatar_url\":null,\"avatar_level\":3}," +
               "\"days\":3,\"starts_at\":null,\"ends_at\":null,\"ended_at\":null," +
               $"\"offered_at\":\"{Iso(-2)}\",\"offer_expires_at\":\"{Iso(expiresInHours)}\"," +
               "\"answered_at\":null," +
               $"\"status\":\"{status}\",\"level\":{level},\"level_at_start\":40,\"level_at_end\":null," +
               "\"lender_share_bp\":2000,\"rp_to_lender\":0,\"rp_to_borrower\":0}";

        private void Refresh(string outJson, string inJson, string offersJson = "")
        {
            _transport.Enqueue(HttpResponse.Status(200,
                "{\"data\":{\"out\":[" + outJson + "],\"in\":[" + inJson + "]," +
                "\"offers_in\":[" + offersJson + "]}}"));
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
        // asset_loans_offers — the predicate split
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public void AnOfferIsNotLiveButItIsLocked()
        {
            // THE headline invariant. Both halves in one test on purpose: they are two readings
            // of the SAME row, and a change that made them agree would pass a test that only
            // asserted one of them.
            var dto = new LoanDto { Status = "offered", OfferExpiresAt = Iso(46) };

            Assert.IsFalse(dto.IsLive(), "an offered row must not be live — nobody accepted it");
            Assert.IsTrue(dto.IsPendingOffer());
            Assert.IsTrue(dto.IsLocked(), "an offered row IS out of the lender's hands");
        }

        [Test]
        public void AnExpiredOfferIsNeitherLiveNorLocked()
        {
            // Lazy expiry: the server has not swept it yet, and the predicate is the enforcement.
            var dto = new LoanDto { Status = "offered", OfferExpiresAt = Iso(-1) };

            Assert.IsFalse(dto.IsPendingOffer());
            Assert.IsFalse(dto.IsLocked(), "a lapsed offer must stop blocking the asset");
        }

        [Test]
        public void AnUnparseableOfferExpiryIsTreatedAsPending()
        {
            // The same choice IsLive makes about ends_at, and for the same reason: cancelling a
            // real offer over a parse hiccup is the more expensive mistake.
            Assert.IsTrue(new LoanDto { Status = "offered", OfferExpiresAt = "not-a-date" }
                              .IsPendingOffer());
        }

        [Test]
        public void AnOfferedAssetIsLentOutSoEveryExistingLockHolds()
        {
            // This is the whole reason `Out` is filtered on IsLocked rather than IsLive: every
            // lock in the game — SelectCharacter, EquipClub, the level-up gate, both detail
            // panels — asks IsLentOut, and none of them was changed by this task.
            Refresh(OfferJson("o1", "character", CharRef), "");

            Assert.AreEqual(1, _service.Out.Count);
            Assert.IsTrue(_service.IsLentOut("character", CharRef, out LoanDto loan));
            Assert.AreEqual("offered", loan.Status);
            Assert.IsTrue(_service.IsOffered("character", CharRef, out _),
                          "and the panels can still tell the two states apart");
        }

        [Test]
        public void ALentAssetIsNotReportedAsOffered()
        {
            // The narrowing has to be a narrowing. If IsOffered answered true for a running loan
            // the LEND slot would say RESCIND on an accepted loan — which is the recall this
            // feature deliberately does not have.
            Refresh(LoanJson("l1", "character", CharRef, 52), "");

            Assert.IsTrue(_service.IsLentOut("character", CharRef));
            Assert.IsFalse(_service.IsOffered("character", CharRef, out _));
        }

        [Test]
        public void OffersInIsSeparateFromInSoNothingEntersTheRoster()
        {
            var rec = new RecordingReconciler();
            _service.Reconciler = rec;

            Refresh("", "", OfferJson("o2", "club", ClubRef));

            Assert.AreEqual(1, _service.OffersIn.Count);
            Assert.AreEqual(0, _service.In.Count, "an unanswered offer is not a borrowed asset");
            Assert.IsFalse(_service.IsBorrowed("club", ClubRef));
            CollectionAssert.DoesNotContain(rec.Calls, "ensure:" + ClubRef,
                "EnsureBorrowed on an offer would put gear nobody accepted into the roster");
        }

        [Test]
        public void AnExpiredOfferNeverReachesOffersIn()
        {
            Refresh("", "", OfferJson("o3", "club", ClubRef, expiresInHours: -1));
            Assert.AreEqual(0, _service.OffersIn.Count);
        }

        [Test]
        public void OffersInDoesNotLeakIntoEnded()
        {
            // An offer the RECIPIENT never answered reaches the LENDER as `offer_expired` in
            // `out` — which is the side that owns the toast. Adding it to `_ended` here would
            // toast the recipient about an offer they were never told existed.
            Refresh("", "", OfferJson("o4", "club", ClubRef, expiresInHours: -1));
            Assert.AreEqual(0, _service.Ended.Count);
        }

        [Test]
        public void NewestOfferInPicksTheMostRecentlyOffered()
        {
            // Built explicitly rather than by surgery on OfferJson, so the ordering under test
            // is visibly the one the assertion names.
            string a = "{\"id\":\"older\",\"kind\":\"character\",\"ref_id\":\"" + CharRef + "\"," +
                       "\"lender\":{\"id\":\"L1\",\"display_name\":\"KENJI\",\"avatar_level\":9}," +
                       "\"borrower\":{\"id\":\"B\",\"display_name\":\"MARTA\",\"avatar_level\":3}," +
                       "\"days\":3,\"status\":\"offered\",\"offered_at\":\"" + Iso(-10) + "\"," +
                       "\"offer_expires_at\":\"" + Iso(38) + "\",\"level\":42,\"lender_share_bp\":2000}";
            string b = "{\"id\":\"newer\",\"kind\":\"club\",\"ref_id\":\"" + ClubRef + "\"," +
                       "\"lender\":{\"id\":\"L2\",\"display_name\":\"KENDRA\",\"avatar_level\":4}," +
                       "\"borrower\":{\"id\":\"B\",\"display_name\":\"MARTA\",\"avatar_level\":3}," +
                       "\"days\":1,\"status\":\"offered\",\"offered_at\":\"" + Iso(-1) + "\"," +
                       "\"offer_expires_at\":\"" + Iso(47) + "\",\"level\":8,\"lender_share_bp\":2000}";

            Refresh("", "", a + "," + b);

            Assert.AreEqual(2, _service.OffersIn.Count);
            Assert.AreEqual("newer", _service.NewestOfferIn().Id,
                            "the pill opens the newest offer, by the row's own offered_at");
        }

        [Test]
        public void TheThreeTerminalOfferStatesAreRecognisedAsEnded()
        {
            foreach (string status in new[] { "declined", "rescinded", "offer_expired" })
                Assert.IsTrue(new LoanDto { Status = status }.IsOfferEnded, status);

            foreach (string status in new[] { "active", "offered", "returned", "expired" })
                Assert.IsFalse(new LoanDto { Status = status }.IsOfferEnded, status);
        }

        [Test]
        public void ATerminalOfferUnlocksTheLenderExactlyOnce()
        {
            var rec = new RecordingReconciler();
            _service.Reconciler = rec;

            string declined = OfferJson("o5", "character", CharRef, status: "declined");
            Refresh(declined, "");
            Assert.AreEqual(1, rec.Calls.Count(c => c == "unlock:" + CharRef + ":True"));

            // The server reports it for fourteen days; the second pass must not re-toast.
            Refresh(declined, "");
            Assert.AreEqual(1, rec.Calls.Count(c => c == "unlock:" + CharRef + ":True"));
            Assert.AreEqual(0, _service.Out.Count, "and the asset is unlocked");
        }

        [Test]
        public void OfferHoursLeftRoundsUpAndNeverSaysZeroWhileTimeRemains()
        {
            // "0h to answer" on an offer that IS still answerable is a lie the lender would act
            // on — they would assume it had lapsed and re-offer, into a `pending_pair` refusal.
            Assert.AreEqual(46, new LoanDto { Status = "offered", OfferExpiresAt = Iso(45.2) }
                                    .OfferHoursLeft());
            Assert.AreEqual(1, new LoanDto { Status = "offered", OfferExpiresAt = Iso(0.01) }
                                   .OfferHoursLeft());
            Assert.AreEqual(0, new LoanDto { Status = "offered", OfferExpiresAt = Iso(-1) }
                                   .OfferHoursLeft());
        }

        // ── the three offer writes, and the search ───────────────────────────

        [Test]
        public void AcceptDeclineAndRescindPostToTheirOwnEndpoints()
        {
            foreach (var kv in new Dictionary<string, System.Func<System.Action<ApiResult<LoanMutationDto>>, IEnumerator>>
            {
                { "/loans/loan-7/accept",  cb => _service.Accept("loan-7", cb) },
                { "/loans/loan-7/decline", cb => _service.Decline("loan-7", cb) },
                { "/loans/loan-7/rescind", cb => _service.Rescind("loan-7", cb) },
            })
            {
                _transport.Enqueue(HttpResponse.Status(200, "{\"data\":{\"status\":\"ok\"}}"));
                Pump(kv.Value(_ => { }));
                StringAssert.EndsWith(kv.Key, _transport.SentUrls.Last());
                Assert.AreEqual("POST", _transport.SentMethods.Last());
            }
        }

        [Test]
        public void SearchAsksForLoanFilteredResults()
        {
            _transport.Enqueue(HttpResponse.Status(200, "{\"data\":[]}"));
            Pump(_service.SearchUsers("ken", _ => { }));

            string url = _transport.SentUrls.Last();
            StringAssert.Contains("/user/search", url);
            StringAssert.Contains("q=ken", url);
            StringAssert.Contains("for_loans=1", url,
                "without this a player who switched offers off still appears in the lend modal");
        }

        [Test]
        public void AnEmptySearchMakesNoRequestAtAll()
        {
            // The endpoint would happily answer "recently active players" for an empty q, and the
            // modal's contract is that clearing the field HIDES the results section.
            List<FollowedUserDto> got = null;
            Pump(_service.SearchUsers("   ", r => got = r.Data));

            Assert.AreEqual(0, _transport.SentUrls.Count);
            Assert.IsNotNull(got);
            Assert.AreEqual(0, got.Count);
        }

        [Test]
        public void TheSearchDtoReadsTheFLAT_UserSearchShape()
        {
            // ⚠️ TWO WIRE SHAPES, ONE DTO. /user/search returns a BARE profiles row; the
            // following list nests the same fields under `profiles`. A DTO that only knew the
            // nested one would render every search result as "PLAYER Lv 1" — which looks like
            // data, not like a bug.
            _transport.Enqueue(HttpResponse.Status(200,
                "{\"data\":[{\"id\":\"u9\",\"display_name\":\"KENJI\",\"avatar_url\":null," +
                "\"avatar_level\":12}]}"));

            List<FollowedUserDto> got = null;
            Pump(_service.SearchUsers("ken", r => got = r.Data));

            Assert.AreEqual(1, got.Count);
            Assert.AreEqual("u9", got[0].Id);
            Assert.AreEqual("KENJI", got[0].DisplayName);
            Assert.AreEqual(12, got[0].AvatarLevel);
        }

        [Test]
        public void TheFollowingDtoStillReadsTheNESTEDShape()
        {
            // The other half of the same guarantee — adding the flat mapping must not have broken
            // the shape that was already shipping.
            _transport.Enqueue(HttpResponse.Status(200,
                "{\"data\":[{\"following_id\":\"u3\",\"created_at\":\"2026-09-01\"," +
                "\"profiles\":{\"id\":\"u3\",\"display_name\":\"MARTA\",\"avatar_url\":null," +
                "\"avatar_level\":31}}]}"));

            SeedMe();

            List<FollowedUserDto> got = null;
            Pump(_service.Following(r => got = r.Data));

            Assert.AreEqual(1, got.Count);
            Assert.AreEqual("u3", got[0].Id);
            Assert.AreEqual("MARTA", got[0].DisplayName);
            Assert.AreEqual(31, got[0].AvatarLevel);

            // The OTHER half of §1.4: the followed list is filtered too, or the modal would list
            // somebody the search deliberately hides and the LEND would answer `not_accepting`.
            StringAssert.Contains("for_loans=1", _transport.SentUrls.Last());
        }

        [Test]
        public void EveryNewRefusalMapsToItsOwnKey()
        {
            var expected = new Dictionary<string, string>
            {
                { "not_accepting", "LOAN_ERR_NOT_ACCEPTING" },
                { "pending_limit", "LOAN_ERR_PENDING_LIMIT" },
                { "pending_pair",  "LOAN_ERR_PENDING_PAIR" },
            };
            foreach (var kv in expected)
                Assert.AreEqual(kv.Value, new LoanMutationDto { Status = kv.Key }.ErrorKey(), kv.Key);

            // COOLDOWN IS DELIBERATELY ABSENT from ErrorKey: its sentence needs the recipient's
            // NAME, which only the lend modal knows. Falling through to the generic key is the
            // correct answer here — the modal formats it itself.
            Assert.AreEqual("LOAN_ERR_GENERIC",
                            new LoanMutationDto { Status = "cooldown" }.ErrorKey());
        }

        [Test]
        public void TheRecipientsAnswerRefusalsHaveTheirOwnTable()
        {
            // `limit_in` to a LENDER is "they are full"; to the RECIPIENT answering an offer it is
            // "YOU are full". One shared mapping would tell one of them the other's sentence.
            Assert.AreEqual("LOAN_ERR_LIMIT_IN",
                            new LoanMutationDto { Status = "limit_in" }.ErrorKey());
            Assert.AreEqual("LOAN_ERR_LIMIT_IN_SELF",
                            new LoanMutationDto { Status = "limit_in" }.AnswerErrorKey());

            Assert.AreEqual("LOAN_ERR_OFFER_GONE",
                            new LoanMutationDto { Status = "not_offered" }.AnswerErrorKey());
            Assert.AreEqual("LOAN_ERR_HAVE_IT",
                            new LoanMutationDto { Status = "borrower_has_it" }.AnswerErrorKey());
        }

        [Test]
        public void TheCooldownRetryAfterParses()
        {
            var dto = new LoanMutationDto { Status = "cooldown", RetryAfter = Iso(20) };
            Assert.IsNotNull(dto.RetryAfterUtc);
            Assert.Greater(dto.RetryAfterUtc.Value, System.DateTime.UtcNow);
        }

        [Test]
        public void FormattingAnOfferThroughTheLOANClockWouldPrintZero()
        {
            // WHY LoanRibbonView.LabelFor HAS A BRANCH AT ALL, pinned where it can be pinned
            // (an asmdef cannot reference Assembly-CSharp, so the view itself is not reachable
            // from this assembly — the TRAP is, and it lives on the DTO).
            //
            // A pending offer has NO ends_at: the loan's days do not start until the recipient
            // accepts. So TimeLeft() — the loan clock — honestly answers zero, and a ribbon that
            // formatted an offer through it would tell the lender "0h 0m" about an offer with 46
            // hours left on it. OfferTimeLeft() is the clock that exists for this row.
            var offer = new LoanDto { Status = "offered", EndsAt = null, OfferExpiresAt = Iso(46) };

            Assert.AreEqual(System.TimeSpan.Zero, offer.TimeLeft(),
                            "the LOAN clock has not started — this is why the ribbon must branch");
            Assert.Greater(offer.OfferTimeLeft().TotalHours, 45.0);
            Assert.AreEqual(46, offer.OfferHoursLeft());
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
