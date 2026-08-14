// Order: rp_balance_sync §5.2 — the inbound wire: server balance → the number the player sees.
using System.Collections.Generic;
using Golfin.Net;
using Golfin.Net.Tests;
using NUnit.Framework;

namespace Golfin.Economy.Tests
{
    /// <summary>
    /// The bug this feature fixes was a MISSING SUBSCRIBER, so these tests assert the wire itself:
    /// that a server answer reaches the sink, that a queued earn is added to it rather than swallowed
    /// by it (§3.4), and that an unanswered session never pushes a fabricated 0 (§3.5).
    ///
    /// <see cref="ServerBalanceSync"/> is the headless half of the bridge on purpose — the production
    /// sink (<c>ServerBalanceSyncBehaviour</c> → <c>RewardPointsManager.ApplyServerBalance</c>) is a
    /// two-line forward, so testing the rules here covers the logic without a scene or a save file.
    /// </summary>
    public class ServerBalanceSyncTests
    {
        private FakeHttpTransport _transport;
        private ApiClient _client;
        private PointsService _service;
        private RecordingSink _sink;

        private const string BalanceEnvelope =
            "{\"data\":{\"activity_pts\":150,\"gift_pts\":23,\"total_points\":173,\"avatar_level\":3,\"avatar_xp\":120}}";

        private static string EarnEnvelope(int awarded, int activity, int total) =>
            "{\"data\":{\"awarded\":" + awarded + ",\"action\":\"hole_complete\",\"activity_pts\":" + activity +
            ",\"total_points\":" + total + ",\"avatar_level\":3,\"avatar_xp\":150,\"leveled_up\":false,\"replayed\":false}}";

        /// <summary>Stands in for the nav bar: records every number it was told to display.</summary>
        private sealed class RecordingSink : IServerBalanceSink
        {
            public readonly List<int> Applied = new List<int>();
            public int Last => Applied.Count > 0 ? Applied[Applied.Count - 1] : -1;
            public void ApplyServerBalance(int total) => Applied.Add(total);
        }

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
            _sink = new RecordingSink();

            PointsBackendFlag.Enabled = true;
        }

        [TearDown]
        public void TearDown()
        {
            ServerBalanceSync.Unbind();
            PointsBackendFlag.ResetToDefault();
            PointsService.ResetForTest();
            ApiClient.ResetForTest();
        }

        // ── §3.2 the wire exists ──────────────────────────────────────────────────

        [Test]
        public void ServerBalance_ReachesTheSink()
        {
            ServerBalanceSync.Bind(_service, _sink);
            _transport.Enqueue(HttpResponse.Status(200, BalanceEnvelope));

            Pump.Drain(_service.RefreshBalanceRoutine(null));

            Assert.AreEqual(1, _sink.Applied.Count, "A server balance must reach the sink exactly once.");
            Assert.AreEqual(173, _sink.Last, "The sink must be told the server's total, not the local number.");
        }

        [Test]
        public void Bind_PushesTheAlreadyKnownBalanceImmediately()
        {
            _transport.Enqueue(HttpResponse.Status(200, BalanceEnvelope));
            Pump.Drain(_service.RefreshBalanceRoutine(null));

            // Sink arrives late (scene load, domain reload) — it must not wait for the next change.
            ServerBalanceSync.Bind(_service, _sink);

            Assert.AreEqual(1, _sink.Applied.Count);
            Assert.AreEqual(173, _sink.Last);
        }

        [Test]
        public void Unbind_StopsUpdates()
        {
            ServerBalanceSync.Bind(_service, _sink);
            ServerBalanceSync.Unbind();

            _transport.Enqueue(HttpResponse.Status(200, BalanceEnvelope));
            Pump.Drain(_service.RefreshBalanceRoutine(null));

            Assert.AreEqual(0, _sink.Applied.Count, "An unbound sink must receive nothing.");
            Assert.IsFalse(ServerBalanceSync.IsBound);
        }

        [Test]
        public void Bind_Twice_DoesNotDoubleSubscribe()
        {
            ServerBalanceSync.Bind(_service, _sink);
            ServerBalanceSync.Bind(_service, _sink);

            _transport.Enqueue(HttpResponse.Status(200, BalanceEnvelope));
            Pump.Drain(_service.RefreshBalanceRoutine(null));

            Assert.AreEqual(1, _sink.Applied.Count, "Rebinding must replace, not stack.");
        }

        // ── §3.4 displayed = server + pending earns ───────────────────────────────

        [Test]
        public void PendingEarn_IsAddedToTheServerBalance()
        {
            _transport.Enqueue(HttpResponse.Status(200, BalanceEnvelope));
            Pump.Drain(_service.RefreshBalanceRoutine(null));
            ServerBalanceSync.Bind(_service, _sink);
            Assert.AreEqual(173, _sink.Last);

            // An earn the server has not accepted yet. The player has already been shown these points.
            _service.EnqueueEarn(PointsActions.HoleComplete, 25);

            Assert.AreEqual(25, _service.PendingEarnTotal);
            Assert.AreEqual(198, _service.DisplayBalance, "displayed = server + pending (§3.4).");
            Assert.AreEqual(198, _sink.Last, "A queued earn must not vanish from the display.");
        }

        [Test]
        public void QueueDrain_LandsOnTheServerTotal_WithoutDoubleCounting()
        {
            _transport.Enqueue(HttpResponse.Status(200, BalanceEnvelope));
            Pump.Drain(_service.RefreshBalanceRoutine(null));
            ServerBalanceSync.Bind(_service, _sink);

            _service.EnqueueEarn(PointsActions.HoleComplete, 25);
            Assert.AreEqual(198, _sink.Last);

            // The server accepts it: pending goes to 0 and the server total absorbs the same 25.
            _transport.Enqueue(HttpResponse.Status(200, EarnEnvelope(25, 175, 198)));
            Pump.Drain(_service.ReplayPendingRoutine(null));

            Assert.AreEqual(0, _service.PendingEarnTotal);
            Assert.AreEqual(198, _service.DisplayBalance, "The flush must be a no-op for the player, not a jump.");
            Assert.AreEqual(198, _sink.Last);
        }

        [Test]
        public void RefusedEarn_DropsThePendingCredit()
        {
            _transport.Enqueue(HttpResponse.Status(200, BalanceEnvelope));
            Pump.Drain(_service.RefreshBalanceRoutine(null));
            ServerBalanceSync.Bind(_service, _sink);

            _service.EnqueueEarn(PointsActions.HoleComplete, 25);
            Assert.AreEqual(198, _sink.Last);

            // {awarded:0, reason:…} — a definitive refusal (daily cap / unknown action).
            _transport.Enqueue(HttpResponse.Status(200,
                "{\"data\":{\"awarded\":0,\"action\":\"hole_complete\",\"reason\":\"daily_cap\"," +
                "\"activity_pts\":150,\"total_points\":173,\"avatar_level\":3,\"avatar_xp\":120,\"replayed\":false}}"));
            Pump.Drain(_service.ReplayPendingRoutine(null));

            Assert.AreEqual(0, _service.PendingEarnTotal);
            Assert.AreEqual(173, _sink.Last,
                "The server refused that earn, so the optimistic local credit for it must come back off.");
        }

        [Test]
        public void CatalogFixedEarn_ContributesNothingToThePendingTotal()
        {
            // amount <= 0 means "the server picks the value" — the client has no honest figure to add.
            _service.Queue.EnqueueEarn(PointsActions.HoleComplete, 0);

            Assert.AreEqual(0, _service.Queue.PendingEarnTotal);
        }

        // ── §3.5 unknown is not zero ──────────────────────────────────────────────

        [Test]
        public void NoServerAnswer_NeverPushesAnything()
        {
            ServerBalanceSync.Bind(_service, _sink);

            // A whole session's worth of local activity, with the server never answering.
            _service.EnqueueEarn(PointsActions.HoleComplete, 25);
            _service.EnqueueEarn(PointsActions.VersusWin, 20);

            Assert.IsFalse(_service.HasBalance);
            Assert.AreEqual(0, _sink.Applied.Count,
                "With no server answer this session the cached local value must stand — never a fabricated 0.");
        }

        [Test]
        public void FailedRefresh_LeavesTheCachedValueAlone()
        {
            _transport.Enqueue(HttpResponse.Status(200, BalanceEnvelope));
            Pump.Drain(_service.RefreshBalanceRoutine(null));
            ServerBalanceSync.Bind(_service, _sink);
            int pushes = _sink.Applied.Count;

            _transport.Enqueue(HttpResponse.Status(500, "{\"detail\":\"boom\"}"));
            Pump.Drain(_service.RefreshBalanceRoutine(null));

            Assert.AreEqual(pushes, _sink.Applied.Count, "A failed refresh must not move the display.");
            Assert.AreEqual(173, _sink.Last);
        }

        [Test]
        public void FirstAnswerOfZero_IsPushed()
        {
            ServerBalanceSync.Bind(_service, _sink);

            // A brand-new account genuinely holds 0. "Unknown → 0" is a real transition the display
            // must follow, even though the number did not change.
            _transport.Enqueue(HttpResponse.Status(200,
                "{\"data\":{\"activity_pts\":0,\"gift_pts\":0,\"total_points\":0,\"avatar_level\":1,\"avatar_xp\":0}}"));
            Pump.Drain(_service.RefreshBalanceRoutine(null));

            Assert.AreEqual(1, _sink.Applied.Count);
            Assert.AreEqual(0, _sink.Last);
        }

        // ── the flag ──────────────────────────────────────────────────────────────

        [Test]
        public void FlagOff_NothingReachesTheSink()
        {
            PointsBackendFlag.Enabled = false;
            ServerBalanceSync.Bind(_service, _sink);

            _transport.Enqueue(HttpResponse.Status(200, BalanceEnvelope));
            Pump.Drain(_service.RefreshBalanceRoutine(null));

            Assert.AreEqual(0, _sink.Applied.Count, "Flag OFF must be byte-identical to the local-only game.");
        }
    }
}
