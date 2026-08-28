// ─────────────────────────────────────────────────────────────────────────────
// game_modes_admin §4 — the three server-validated answers a MODE ENTRY debit
// can come back with, and the reason string that makes them possible at all.
//
// This is the client half of the same door test_mode_entry_fee.py guards on the
// server. The property both sides carry: fee_changed / unknown_mode /
// mode_locked arrive as HTTP **200**, nothing is debited, and each lands on its
// OWN verdict. Collapsing them into Unavailable would be the classic failure —
// a publish landing mid-session would tell every open client it is offline.
// ─────────────────────────────────────────────────────────────────────────────
using Golfin.Net;
using Golfin.Net.Tests;
using NUnit.Framework;

namespace Golfin.Economy.Tests
{
    public class ModeEntryFeeSpendTests
    {
        private FakeHttpTransport _transport;
        private ApiClient _client;
        private PointsService _service;

        private const string FeeChangedEnvelope =
            "{\"data\":{\"status\":\"fee_changed\",\"mode_id\":\"practice\",\"fee\":15}}";

        private const string UnknownModeEnvelope =
            "{\"data\":{\"status\":\"unknown_mode\",\"mode_id\":\"battle_royale\"}}";

        private const string ModeLockedEnvelope =
            "{\"data\":{\"status\":\"mode_locked\",\"mode_id\":\"missions\"}}";

        private const string OkEnvelope =
            "{\"data\":{\"status\":\"ok\",\"spent\":15,\"from_activity\":15,\"from_gift\":0," +
            "\"activity_pts\":85,\"gift_pts\":50,\"total_points\":135,\"replayed\":false}}";

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
            PointsBackendFlag.Enabled = true;
        }

        [TearDown]
        public void TearDown()
        {
            PointsBackendFlag.ResetToDefault();
            PointsService.ResetForTest();
            ApiClient.ResetForTest();
        }

        private SpendOutcome Spend(int amount, string reason)
        {
            SpendOutcome outcome = null;
            Pump.Drain(_service.SpendRoutine(amount, reason, o => outcome = o));
            return outcome;
        }

        // ── The reason string ─────────────────────────────────────────────────────

        [Test]
        public void TheReasonCarriesTheModeId()
        {
            // Without the suffix the server cannot know WHICH mode is being paid for, so it cannot
            // check the price — which is the entire mechanism. `mode_entry_fee` stays the prefix so
            // the ledger keeps grouping, and the id makes each row per-mode legible for free.
            Assert.AreEqual("mode_entry_fee:practice", SpendReasons.ModeEntryFeeFor("practice"));
            Assert.AreEqual("mode_entry_fee:versus_1v1", SpendReasons.ModeEntryFeeFor(" versus_1v1 "),
                "surrounding whitespace would become part of the mode id the server parses out");
        }

        [Test]
        public void ABlankModeIdFallsBackToTheBareReasonRatherThanADanglingColon()
        {
            // `mode_entry_fee:` reads as unknown_mode server-side, so sending it would turn a client
            // bug (a mode with no id) into a refused entry on top. The bare reason still debits.
            Assert.AreEqual(SpendReasons.ModeEntryFee, SpendReasons.ModeEntryFeeFor(""));
            Assert.AreEqual(SpendReasons.ModeEntryFee, SpendReasons.ModeEntryFeeFor(null));
            Assert.AreEqual(SpendReasons.ModeEntryFee, SpendReasons.ModeEntryFeeFor("   "));
        }

        // ── fee_changed ───────────────────────────────────────────────────────────

        [Test]
        public void FeeChanged_Arrives200_DoesNotProceed_AndCarriesThePublishedFee()
        {
            _transport.Enqueue(HttpResponse.Status(200, FeeChangedEnvelope));

            SpendOutcome outcome = Spend(10, SpendReasons.ModeEntryFeeFor("practice"));

            Assert.AreEqual(SpendVerdict.FeeChanged, outcome.Verdict);
            Assert.IsFalse(outcome.MayProceed, "nothing was debited, so the mode must NOT be entered");
            Assert.AreEqual(15, outcome.ServerFee,
                "without the real fee the card can only know it was wrong, not what to show — and " +
                "the player's second tap would be refused for the same reason, forever");
            Assert.AreEqual("practice", outcome.ModeId);
        }

        [Test]
        public void FeeChanged_DoesNotTouchTheCachedBalance()
        {
            // The payload carries no post-debit total because there was no debit. Folding it in
            // (the way `insufficient` legitimately is) would zero the player's displayed RP.
            _transport.Enqueue(HttpResponse.Status(200, FeeChangedEnvelope));

            Spend(10, SpendReasons.ModeEntryFeeFor("practice"));

            Assert.IsFalse(_service.HasBalance,
                "a refusal that debited nothing must not overwrite the balance cache");
        }

        [Test]
        public void TheSecondTapAtTheServerFeeIsApproved()
        {
            // The whole point of answering with the fee: re-price, tap again, pay.
            _transport.Enqueue(HttpResponse.Status(200, FeeChangedEnvelope));
            SpendOutcome first = Spend(10, SpendReasons.ModeEntryFeeFor("practice"));
            Assert.AreEqual(SpendVerdict.FeeChanged, first.Verdict);

            _transport.Enqueue(HttpResponse.Status(200, OkEnvelope));
            SpendOutcome second = Spend(first.ServerFee, SpendReasons.ModeEntryFeeFor("practice"));

            Assert.AreEqual(SpendVerdict.Approved, second.Verdict);
            Assert.IsTrue(second.MayProceed);
            Assert.AreEqual(15, second.Server.Spent);
        }

        // ── unknown_mode / mode_locked ────────────────────────────────────────────

        [Test]
        public void UnknownMode_IsItsOwnVerdictAndDoesNotProceed()
        {
            _transport.Enqueue(HttpResponse.Status(200, UnknownModeEnvelope));

            SpendOutcome outcome = Spend(10, SpendReasons.ModeEntryFeeFor("battle_royale"));

            Assert.AreEqual(SpendVerdict.UnknownMode, outcome.Verdict);
            Assert.IsFalse(outcome.MayProceed);
            Assert.AreEqual("battle_royale", outcome.ModeId);
        }

        [Test]
        public void ModeLocked_IsItsOwnVerdictAndDoesNotProceed()
        {
            _transport.Enqueue(HttpResponse.Status(200, ModeLockedEnvelope));

            SpendOutcome outcome = Spend(25, SpendReasons.ModeEntryFeeFor("missions"));

            Assert.AreEqual(SpendVerdict.ModeLocked, outcome.Verdict);
            Assert.IsFalse(outcome.MayProceed);
        }

        [Test]
        public void NoneOfTheThreeIsReportedAsUnavailable()
        {
            // The load-bearing property. Unavailable means "I could not reach the server", and the
            // UI says "Connection required". All three of these are the server ANSWERING.
            foreach (string envelope in new[] { FeeChangedEnvelope, UnknownModeEnvelope, ModeLockedEnvelope })
            {
                _transport.Enqueue(HttpResponse.Status(200, envelope));
                SpendOutcome outcome = Spend(10, SpendReasons.ModeEntryFeeFor("practice"));
                Assert.AreNotEqual(SpendVerdict.Unavailable, outcome.Verdict,
                    "a definitive 200 refusal must never look like a connection failure: " + envelope);
                Assert.IsFalse(outcome.IsOffline);
            }
        }

        [Test]
        public void AStatusThisBuildDoesNotKnowIsStillUnavailable()
        {
            // The other side of the same coin: an unrecognised status is NOT something to proceed
            // on. A future server outcome must fail closed.
            _transport.Enqueue(HttpResponse.Status(200, "{\"data\":{\"status\":\"mode_on_fire\"}}"));

            SpendOutcome outcome = Spend(10, SpendReasons.ModeEntryFeeFor("practice"));

            Assert.AreEqual(SpendVerdict.Unavailable, outcome.Verdict);
            Assert.IsFalse(outcome.MayProceed);
        }
    }
}
