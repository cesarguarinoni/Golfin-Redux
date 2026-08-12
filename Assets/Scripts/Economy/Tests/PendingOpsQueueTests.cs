// Order: reward_points_backend Slice 1 — queue round-trip + idempotency-key stability (SPEC §4).
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

namespace Golfin.Economy.Tests
{
    /// <summary>
    /// The queue is the only local record of an earn the player has already been shown, and its
    /// idempotency keys are what stop a replay from double-crediting. Both properties are asserted
    /// against a REAL file round-trip, not an in-memory string, because the failure mode being guarded
    /// against ("app killed, relaunched, replays") only exists across a disk boundary.
    /// </summary>
    public class PendingOpsQueueTests
    {
        private string _dir;
        private string _path;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "golfin_points_queue_tests", Path.GetRandomFileName());
            Directory.CreateDirectory(_dir);
            _path = Path.Combine(_dir, "points_pending_ops.json");
        }

        [TearDown]
        public void TearDown()
        {
            try { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); } catch { /* temp dir */ }
        }

        private PendingOpsQueue NewFileQueue()
        {
            var q = new PendingOpsQueue(new FilePendingOpsStore(_path));
            q.Load();
            return q;
        }

        // ── round-trip ────────────────────────────────────────────────────────────

        [Test]
        public void RoundTrip_SurvivesAFullReloadFromDisk()
        {
            var write = NewFileQueue();
            var a = write.EnqueueEarn("hole_complete", 10);
            var b = write.EnqueueEarn("versus_win", 30);
            var c = write.EnqueueEarn("tournament_prize", 250);

            Assert.IsTrue(File.Exists(_path), "Enqueue must persist immediately, not on a later flush.");

            // Simulate a relaunch: brand-new queue over the same file.
            var read = NewFileQueue();

            Assert.AreEqual(3, read.Count);
            Assert.AreEqual(new[] { a.IdempotencyKey, b.IdempotencyKey, c.IdempotencyKey },
                new[] { read.Items[0].IdempotencyKey, read.Items[1].IdempotencyKey, read.Items[2].IdempotencyKey });
            Assert.AreEqual(new[] { "hole_complete", "versus_win", "tournament_prize" },
                new[] { read.Items[0].Action, read.Items[1].Action, read.Items[2].Action });
            Assert.AreEqual(new[] { 10, 30, 250 },
                new[] { read.Items[0].Amount, read.Items[1].Amount, read.Items[2].Amount });
            Assert.AreEqual(PendingOpKind.Earn, read.Items[0].Kind);
            Assert.AreEqual(a.CreatedAtUnix, read.Items[0].CreatedAtUnix);
        }

        [Test]
        public void RoundTrip_EmptyQueueLoadsEmpty()
        {
            var write = NewFileQueue();
            write.EnqueueEarn("hole_complete", 10);
            write.Clear();

            Assert.AreEqual(0, NewFileQueue().Count);
        }

        [Test]
        public void Load_MissingFileStartsEmptyWithoutThrowing()
        {
            Assert.AreEqual(0, NewFileQueue().Count);
        }

        [Test]
        public void Load_CorruptFileStartsEmptyWithoutThrowing()
        {
            File.WriteAllText(_path, "{ this is not json");
            Assert.DoesNotThrow(() => NewFileQueue());
            Assert.AreEqual(0, NewFileQueue().Count);
        }

        [Test]
        public void Load_UnknownVersionIsDiscardedRatherThanMisread()
        {
            File.WriteAllText(_path, "{\"version\":99,\"ops\":[{\"key\":\"k\",\"action\":\"hole_complete\",\"amount\":10}]}");
            Assert.AreEqual(0, NewFileQueue().Count);
        }

        [Test]
        public void Load_DropsOpsWithNoIdempotencyKey()
        {
            // A key-less op cannot be replayed safely — dropping beats double-crediting.
            File.WriteAllText(_path,
                "{\"version\":1,\"ops\":[" +
                "{\"key\":\"\",\"action\":\"hole_complete\",\"amount\":10}," +
                "{\"key\":\"11111111-1111-1111-1111-111111111111\",\"action\":\"versus_win\",\"amount\":30}]}");

            var q = NewFileQueue();
            Assert.AreEqual(1, q.Count);
            Assert.AreEqual("versus_win", q.Items[0].Action);
        }

        // ── idempotency keys ──────────────────────────────────────────────────────

        [Test]
        public void IdempotencyKey_IsAUniqueGuidPerEnqueue()
        {
            var q = NewFileQueue();
            var keys = new HashSet<string>();

            for (int i = 0; i < 50; i++)
            {
                var op = q.EnqueueEarn("hole_complete", 10);
                Assert.IsTrue(System.Guid.TryParse(op.IdempotencyKey, out _),
                    "The server casts the key to uuid — it must parse as a GUID.");
                Assert.IsTrue(keys.Add(op.IdempotencyKey), "Keys must be unique per enqueue.");
            }
        }

        [Test]
        public void IdempotencyKey_IsStableAcrossSaveLoadCycles()
        {
            var q = NewFileQueue();
            string key = q.EnqueueEarn("hole_complete", 10).IdempotencyKey;

            for (int i = 0; i < 5; i++)
            {
                var reloaded = NewFileQueue();
                Assert.AreEqual(key, reloaded.Items[0].IdempotencyKey,
                    "Regenerating the key on reload would defeat the server's unique index and double-credit.");
                reloaded.Save();
            }
        }

        [Test]
        public void IdempotencyKey_IsUnchangedByAFailedAttempt()
        {
            var q = NewFileQueue();
            var op = q.EnqueueEarn("hole_complete", 10);
            string key = op.IdempotencyKey;

            op.AttemptCount++;
            q.Save();

            var reloaded = NewFileQueue();
            Assert.AreEqual(key, reloaded.Items[0].IdempotencyKey);
            Assert.AreEqual(1, reloaded.Items[0].AttemptCount, "Attempt count persists; the key does not move.");
        }

        // ── FIFO semantics ────────────────────────────────────────────────────────

        [Test]
        public void Fifo_PeekAndDequeueReturnTheOldestFirst()
        {
            var q = NewFileQueue();
            q.EnqueueEarn("first", 1);
            q.EnqueueEarn("second", 2);
            q.EnqueueEarn("third", 3);

            Assert.AreEqual("first", q.Peek().Action);
            Assert.AreEqual("first", q.Dequeue().Action);
            Assert.AreEqual("second", q.Peek().Action);
            Assert.AreEqual(2, q.Count);
            Assert.AreEqual("second", NewFileQueue().Items[0].Action, "Dequeue persists immediately.");
        }

        [Test]
        public void Remove_ByKeyTakesTheRightOpOut()
        {
            var q = NewFileQueue();
            q.EnqueueEarn("first", 1);
            var mid = q.EnqueueEarn("second", 2);
            q.EnqueueEarn("third", 3);

            Assert.IsTrue(q.Remove(mid.IdempotencyKey));
            Assert.IsFalse(q.Remove(mid.IdempotencyKey), "Removing twice is a no-op, not a throw.");
            Assert.AreEqual(new[] { "first", "third" }, new[] { q.Items[0].Action, q.Items[1].Action });
        }

        [Test]
        public void Enqueue_OverTheCapDropsTheOldest()
        {
            var q = new PendingOpsQueue(new InMemoryPendingOpsStore());
            for (int i = 0; i < PendingOpsQueue.MaxOps + 5; i++)
                q.EnqueueEarn("op" + i, 1);

            Assert.AreEqual(PendingOpsQueue.MaxOps, q.Count);
            Assert.AreEqual("op5", q.Items[0].Action, "The five oldest are the ones dropped.");
        }

        // ── request body ──────────────────────────────────────────────────────────

        [Test]
        public void ToEarnGameJson_MatchesTheDeployedEarnGameRequestModel()
        {
            var op = PendingPointsOp.NewEarn("tournament_prize", 250);
            string json = op.ToEarnGameJson();

            // backend EarnGameRequest: {action: str, amount: Optional[int], idempotency_key: str}
            StringAssert.Contains("\"action\":\"tournament_prize\"", json);
            StringAssert.Contains("\"amount\":250", json);
            StringAssert.Contains("\"idempotency_key\":\"" + op.IdempotencyKey + "\"", json);
        }

        [Test]
        public void ToEarnGameJson_OmitsAmountWhenTheServerOwnsIt()
        {
            // Catalog-fixed actions (hole_complete, versus_win) must not have a client amount imposed.
            string json = PendingPointsOp.NewEarn("hole_complete", 0).ToEarnGameJson();

            StringAssert.Contains("\"action\":\"hole_complete\"", json);
            StringAssert.DoesNotContain("\"amount\"", json);
        }
    }
}
