// asset_loans §4.5 — the `loan_ids` field on a queued earn.
//
// WHAT THIS EXISTS TO CATCH. The field is OPTIONAL and absent from every build older than this
// feature, so the two things that must both be true are in tension:
//
//   * a round played with borrowed gear must NAME its loans, or the owner is never paid; and
//   * a round played with nothing borrowed must send the request it sent BEFORE this feature —
//     byte-identical — or every earn in the game changes shape for no reason, and an op queued by
//     an older build cannot be replayed by a newer one.
//
// The queue is on disk across app restarts, so the round-trip through JSON is the real contract,
// not the in-memory field.
using System.Collections.Generic;
using Golfin.Economy;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Golfin.Economy.Tests
{
    public class PendingPointsOpLoanTests
    {
        [Test]
        public void AnOrdinaryEarnOmitsLoanIdsEntirely()
        {
            var op = PendingPointsOp.NewEarn("hole_complete", 120);
            JObject body = JObject.Parse(op.ToEarnGameJson());

            Assert.IsNull(body["loan_ids"],
                "an earn with nothing borrowed must send the request it sent before loans existed");
            Assert.AreEqual("hole_complete", (string)body["action"]);
            Assert.AreEqual(120, (int)body["amount"]);
        }

        [Test]
        public void AnEmptyListIsTreatedAsNoLoans()
        {
            // `[]` on the wire is a different request from an absent field for no benefit, and it
            // would make the "unchanged for an ordinary round" property depend on the caller
            // remembering to pass null rather than an empty list.
            var op = PendingPointsOp.NewEarn("hole_complete", 120, null, new List<string>());
            Assert.IsNull(JObject.Parse(op.ToEarnGameJson())["loan_ids"]);
        }

        [Test]
        public void ABorrowedRoundNamesItsLoans()
        {
            var op = PendingPointsOp.NewEarn("hole_complete", 120, null,
                                             new List<string> { "loan-a", "loan-b" });
            JObject body = JObject.Parse(op.ToEarnGameJson());

            CollectionAssert.AreEqual(new[] { "loan-a", "loan-b" },
                                      body["loan_ids"].ToObject<List<string>>());
        }

        [Test]
        public void TheListIsCopiedNotAliased()
        {
            // The caller's list is LoanService's live round snapshot. An op that kept a REFERENCE
            // to it would silently change when the next round started — and the op may sit in the
            // queue for days.
            var live = new List<string> { "loan-a" };
            var op = PendingPointsOp.NewEarn("hole_complete", 120, null, live);

            live.Clear();
            live.Add("loan-from-a-later-round");

            CollectionAssert.AreEqual(new[] { "loan-a" }, op.LoanIds);
        }

        [Test]
        public void AnOpQueuedByAnOlderBuildDeserialisesWithNoLoans()
        {
            // The exact JSON the pre-loans build wrote. It must load, replay, and send no loan_ids.
            const string legacy =
                "{\"key\":\"11111111-1111-1111-1111-111111111111\",\"kind\":0," +
                "\"action\":\"hole_complete\",\"amount\":90,\"createdAt\":1757000000,\"attempts\":2}";

            var op = JsonConvert.DeserializeObject<PendingPointsOp>(legacy);

            Assert.IsNull(op.LoanIds, "no queue migration: an older op simply has no loans");
            Assert.AreEqual(2, op.AttemptCount);
            Assert.IsNull(JObject.Parse(op.ToEarnGameJson())["loan_ids"]);
        }

        [Test]
        public void LoanIdsSurviveTheDiskRoundTrip()
        {
            var op = PendingPointsOp.NewEarn("hole_complete", 90, null,
                                             new List<string> { "loan-a", "loan-b" });
            string key = op.IdempotencyKey;

            var loaded = JsonConvert.DeserializeObject<PendingPointsOp>(JsonConvert.SerializeObject(op));

            Assert.AreEqual(key, loaded.IdempotencyKey, "the key is immutable for the life of an op");
            CollectionAssert.AreEqual(new[] { "loan-a", "loan-b" }, loaded.LoanIds);
        }

        [Test]
        public void TheQueueForwardsLoanIdsToTheOpItMints()
        {
            var queue = new PendingOpsQueue(new InMemoryOpsStore());
            PendingPointsOp op = queue.EnqueueEarn("hole_complete", 50,
                                                   new List<string> { "loan-x" });

            CollectionAssert.AreEqual(new[] { "loan-x" }, op.LoanIds);
            Assert.AreEqual(1, queue.Count);
        }

        /// <summary>An <see cref="IPendingOpsStore"/> that keeps the payload in a field, so the
        /// queue can be exercised with no disk.</summary>
        private sealed class InMemoryOpsStore : IPendingOpsStore
        {
            private string _json;
            public string Read() => _json;
            public void Write(string json) => _json = json;
            public void Delete() => _json = null;
        }
    }
}
