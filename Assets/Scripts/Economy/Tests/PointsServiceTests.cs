// Order: reward_points_backend Slice 1 — flag gate, balance cache, replay ordering (SPEC §4).
using Golfin.Net;
using Golfin.Net.Tests;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Golfin.Economy.Tests
{
    /// <summary>
    /// The acceptance bar for this slice is ZERO behaviour change, so the first thing asserted is that
    /// with <c>PointsBackendEnabled</c> OFF nothing leaves the device — no HTTP, no queue write.
    /// The rest covers the balance cache and FIFO replay against a scripted transport.
    /// </summary>
    public class PointsServiceTests
    {
        private FakeHttpTransport _transport;
        private ApiClient _client;
        private InMemoryPendingOpsStore _store;
        private PointsService _service;

        private const string BalanceEnvelope =
            "{\"data\":{\"activity_pts\":425,\"gift_pts\":50,\"total_points\":475,\"avatar_level\":3,\"avatar_xp\":120}}";

        private static string EarnEnvelope(int awarded, int activity, int total, bool replayed = false) =>
            "{\"data\":{\"awarded\":" + awarded + ",\"action\":\"hole_complete\",\"activity_pts\":" + activity +
            ",\"total_points\":" + total + ",\"avatar_level\":3,\"avatar_xp\":150,\"leveled_up\":false,\"replayed\":" +
            (replayed ? "true" : "false") + "}}";

        [SetUp]
        public void SetUp()
        {
            _transport = new FakeHttpTransport();
            _client = new ApiClient(_transport, new FakeAuthTokenProvider(), new ImmediateCoroutineRunner())
            {
                RetryDelaySeconds = 0f,
                LogRequests = false
            };
            _store = new InMemoryPendingOpsStore();
            _service = new PointsService(_client, new PendingOpsQueue(_store));

            PointsBackendFlag.Enabled = true; // per-test default; the OFF cases set it back explicitly
        }

        [TearDown]
        public void TearDown()
        {
            PointsBackendFlag.ResetToDefault();
            PointsService.ResetForTest();
            ApiClient.ResetForTest();
        }

        // ── the flag gate — zero behaviour change ─────────────────────────────────

        [Test]
        public void FlagOff_RefreshBalanceMakesNoRequest()
        {
            PointsBackendFlag.Enabled = false;
            _transport.Enqueue(HttpResponse.Status(200, BalanceEnvelope));

            ApiResult<PointsBalance> result = null;
            Pump.Drain(_service.RefreshBalanceRoutine(r => result = r));

            Assert.AreEqual(0, _transport.CallCount, "Flag OFF must not reach the network.");
            Assert.IsFalse(result.Success);
            Assert.AreEqual(ApiErrorKind.Disabled, result.ErrorKind);
            Assert.IsFalse(_service.HasBalance);
        }

        [Test]
        public void FlagOff_EnqueueEarnWritesNothing()
        {
            PointsBackendFlag.Enabled = false;

            var op = _service.EnqueueEarn("hole_complete", 10);

            Assert.IsNull(op);
            Assert.AreEqual(0, _service.Queue.Count);
            Assert.AreEqual(0, _store.WriteCount, "Flag OFF must not touch the pending-ops file.");
        }

        [Test]
        public void FlagOff_ReplayIsANoOp()
        {
            PointsBackendFlag.Enabled = true;
            _service.EnqueueEarn("hole_complete", 10);
            PointsBackendFlag.Enabled = false;

            int sent = -1;
            Pump.Drain(_service.ReplayPendingRoutine(n => sent = n));

            Assert.AreEqual(0, sent);
            Assert.AreEqual(0, _transport.CallCount);
            Assert.AreEqual(1, _service.Queue.Count, "The queued op stays queued.");
        }

        [Test]
        public void CompiledDefault_IsOn()
        {
            // Flipped at the Slice-2 cutover (2026-08-12), in the same commit as the RP rebalance.
            // Asserted rather than assumed: an accidental revert to OFF would silently stop the game
            // writing to the ledger while everything still LOOKED correct locally.
            Assert.IsTrue(PointsBackendFlag.DefaultEnabled);
            Assert.IsTrue(PointsBackendFlag.CompiledDefault);

            PointsBackendFlag.ResetToDefault();
            Assert.IsTrue(PointsBackendFlag.Enabled);
        }

        // ── balance ───────────────────────────────────────────────────────────────

        [Test]
        public void RefreshBalance_CachesTotalPointsAsRewardPoints()
        {
            _transport.Enqueue(HttpResponse.Status(200, BalanceEnvelope));

            int? notified = null;
            _service.OnBalanceChanged += v => notified = v;

            ApiResult<PointsBalance> result = null;
            Pump.Drain(_service.RefreshBalanceRoutine(r => result = r));

            Assert.IsTrue(result.Success, result.ToString());
            Assert.AreEqual(Endpoints.PointsBalance, _transport.SentUrls[0]);
            Assert.IsTrue(_service.HasBalance);
            Assert.AreEqual(475, _service.Balance);
            Assert.AreEqual(475, _service.LastBalance.RewardPoints, "RP == total_points (decision of record #4).");
            Assert.AreEqual(425, _service.LastBalance.ActivityPts);
            Assert.AreEqual(50, _service.LastBalance.GiftPts);
            Assert.AreEqual(3, _service.LastBalance.AvatarLevel);
            Assert.AreEqual(475, notified);
        }

        [Test]
        public void RefreshBalance_ZeroBalanceIsStillKnown()
        {
            // New accounts start at 0 RP (decision of record #6) — "0" must not read as "unknown".
            _transport.Enqueue(HttpResponse.Status(200,
                "{\"data\":{\"activity_pts\":0,\"gift_pts\":0,\"total_points\":0,\"avatar_level\":1,\"avatar_xp\":0}}"));

            int notifications = 0;
            _service.OnBalanceChanged += _ => notifications++;
            Pump.Drain(_service.RefreshBalanceRoutine(null));

            Assert.IsTrue(_service.HasBalance);
            Assert.AreEqual(0, _service.Balance);
            Assert.AreEqual(1, notifications, "First observation notifies even when the value is 0.");
        }

        [Test]
        public void RefreshBalance_FailureLeavesTheCacheUntouched()
        {
            _transport.Enqueue(HttpResponse.Status(200, BalanceEnvelope));
            Pump.Drain(_service.RefreshBalanceRoutine(null));

            _transport.Enqueue(HttpResponse.Status(500, "{\"detail\":\"boom\"}"));
            ApiResult<PointsBalance> second = null;
            Pump.Drain(_service.RefreshBalanceRoutine(r => second = r));

            Assert.IsFalse(second.Success);
            Assert.AreEqual(475, _service.Balance, "A failed refresh must not clobber the last known balance.");
        }

        // ── replay ordering ───────────────────────────────────────────────────────

        [Test]
        public void Replay_SendsOpsOldestFirstAndDrainsTheQueue()
        {
            var a = _service.EnqueueEarn("hole_complete", 10);
            var b = _service.EnqueueEarn("versus_win", 30);
            var c = _service.EnqueueEarn("tournament_prize", 250);

            _transport.Enqueue(
                HttpResponse.Status(200, EarnEnvelope(10, 10, 10)),
                HttpResponse.Status(200, EarnEnvelope(30, 40, 40)),
                HttpResponse.Status(200, EarnEnvelope(250, 290, 290)));

            int sent = 0;
            Pump.Drain(_service.ReplayPendingRoutine(n => sent = n));

            Assert.AreEqual(3, sent);
            Assert.AreEqual(0, _service.Queue.Count);
            Assert.AreEqual(3, _transport.CallCount);

            // FIFO: the keys must appear on the wire in enqueue order.
            StringAssert.Contains(a.IdempotencyKey, _transport.SentBodies[0]);
            StringAssert.Contains(b.IdempotencyKey, _transport.SentBodies[1]);
            StringAssert.Contains(c.IdempotencyKey, _transport.SentBodies[2]);
            Assert.AreEqual(Endpoints.PointsEarnGame, _transport.SentUrls[0]);
            Assert.AreEqual("POST", _transport.SentMethods[0]);

            Assert.AreEqual(290, _service.Balance, "The last earn's total_points becomes the cached RP.");
        }

        [Test]
        public void Replay_StopsAtTheFirstFailureAndKeepsOrder()
        {
            var a = _service.EnqueueEarn("hole_complete", 10);
            var b = _service.EnqueueEarn("versus_win", 30);
            var c = _service.EnqueueEarn("tournament_prize", 250);

            // #1 lands, #2 hits a hard 500 (not retried), #3 must NOT jump ahead of it.
            _transport.Enqueue(
                HttpResponse.Status(200, EarnEnvelope(10, 10, 10)),
                HttpResponse.Status(500, "{\"detail\":\"boom\"}"));

            int sent = -1;
            Pump.Drain(_service.ReplayPendingRoutine(n => sent = n));

            Assert.AreEqual(1, sent);
            Assert.AreEqual(2, _transport.CallCount, "Replay halts — it does not skip to the next op.");
            Assert.AreEqual(2, _service.Queue.Count);
            Assert.AreEqual(b.IdempotencyKey, _service.Queue.Items[0].IdempotencyKey, "#2 stays at the head.");
            Assert.AreEqual(c.IdempotencyKey, _service.Queue.Items[1].IdempotencyKey);
            Assert.AreEqual(1, _service.Queue.Items[0].AttemptCount, "The failed attempt is recorded…");
            Assert.AreEqual(0, _service.Queue.Items[1].AttemptCount, "…and #3 was never attempted.");
            Assert.IsFalse(_store.Json.Contains(a.IdempotencyKey), "The delivered op is gone from disk.");
        }

        [Test]
        public void Replay_ResumesInOrderOnTheNextConnection()
        {
            _service.EnqueueEarn("hole_complete", 10);
            var b = _service.EnqueueEarn("versus_win", 30);

            _transport.Enqueue(HttpResponse.ConnectionFailure("offline"));
            _client.MaxTransientRetries = 0;
            Pump.Drain(_service.ReplayPendingRoutine(null));

            Assert.AreEqual(2, _service.Queue.Count, "Nothing is lost while offline.");

            _transport.Enqueue(
                HttpResponse.Status(200, EarnEnvelope(10, 10, 10)),
                HttpResponse.Status(200, EarnEnvelope(30, 40, 40)));

            int sent = 0;
            Pump.Drain(_service.ReplayPendingRoutine(n => sent = n));

            Assert.AreEqual(2, sent);
            Assert.AreEqual(0, _service.Queue.Count);
            StringAssert.Contains(b.IdempotencyKey, _transport.SentBodies[_transport.CallCount - 1]);
        }

        [Test]
        public void Replay_IdempotentServerReplayIsAcceptedNotRetriedForever()
        {
            // An ambiguous timeout means the same key can be re-sent; the server answers replayed:true
            // with the ORIGINAL award. That is a success, so the op must leave the queue.
            _service.EnqueueEarn("hole_complete", 10);
            _transport.Enqueue(HttpResponse.Status(200, EarnEnvelope(10, 10, 10, replayed: true)));

            int sent = 0;
            Pump.Drain(_service.ReplayPendingRoutine(n => sent = n));

            Assert.AreEqual(1, sent);
            Assert.AreEqual(0, _service.Queue.Count);
            Assert.AreEqual(10, _service.Balance);
        }

        [Test]
        public void Replay_ServerRefusalConsumesTheOpInsteadOfLoopingForever()
        {
            // {awarded:0, reason:"Daily cap reached"} is HTTP 200 — a definitive answer, not a failure.
            _service.EnqueueEarn("hole_complete", 10);
            _transport.Enqueue(HttpResponse.Status(200,
                "{\"data\":{\"awarded\":0,\"reason\":\"Daily cap reached\",\"daily_cap\":500}}"));

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Server refused"));

            int sent = 0;
            Pump.Drain(_service.ReplayPendingRoutine(n => sent = n));

            Assert.AreEqual(1, sent);
            Assert.AreEqual(0, _service.Queue.Count, "A refused op must not be retried on every reconnect.");
            Assert.IsFalse(_service.HasBalance, "A refusal carries no balance, so nothing is cached.");
        }

        [Test]
        public void Replay_KeepsTheCachedGiftBucketWhenFoldingInAnEarn()
        {
            // The earn payload has no gift_pts; zeroing it would under-report RP until the next refresh.
            _transport.Enqueue(HttpResponse.Status(200, BalanceEnvelope));
            Pump.Drain(_service.RefreshBalanceRoutine(null));
            Assert.AreEqual(50, _service.LastBalance.GiftPts);

            _service.EnqueueEarn("hole_complete", 10);
            _transport.Enqueue(HttpResponse.Status(200, EarnEnvelope(10, 435, 485)));
            Pump.Drain(_service.ReplayPendingRoutine(null));

            Assert.AreEqual(50, _service.LastBalance.GiftPts, "Gift bucket carried forward.");
            Assert.AreEqual(435, _service.LastBalance.ActivityPts);
            Assert.AreEqual(485, _service.Balance);
        }

        [Test]
        public void EnqueueEarn_RejectsAnEmptyActionWithoutQueueingIt()
        {
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("EnqueueEarn called with no action"));

            Assert.IsNull(_service.EnqueueEarn("", 10));
            Assert.AreEqual(0, _service.Queue.Count);
        }
    }
}
