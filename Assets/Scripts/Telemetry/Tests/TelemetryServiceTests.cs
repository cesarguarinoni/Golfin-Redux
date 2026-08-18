// Order: beta_telemetry — SPEC §5 acceptance tests 1–7. No network, no play mode.
using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Golfin.Telemetry.Tests
{
    /// <summary>
    /// Drives <see cref="TelemetryService"/> against a scripted sender — the same shape as
    /// <c>ApiClientTests</c>'s fake transport, one layer up: the ApiClient path is already
    /// covered by its own suite, so what matters here is the QUEUE contract (batching,
    /// cap, single re-enqueue, auth gate, throw-safety, error cap).
    /// </summary>
    public class TelemetryServiceTests
    {
        /// <summary>Records every batch body it is handed and replies with a scripted
        /// success/failure per call.</summary>
        private sealed class FakeSender
        {
            public readonly List<string> Bodies = new List<string>();
            private readonly Queue<bool> _outcomes = new Queue<bool>();
            public bool DefaultOutcome = true;
            /// <summary>When true, the callback is NOT invoked — the flush stays in flight.</summary>
            public bool HoldCallback;

            public int CallCount => Bodies.Count;

            public FakeSender Enqueue(params bool[] outcomes)
            {
                foreach (bool o in outcomes) _outcomes.Enqueue(o);
                return this;
            }

            public void Send(string json, Action<bool> done)
            {
                Bodies.Add(json);
                if (HoldCallback) return;
                done?.Invoke(_outcomes.Count > 0 ? _outcomes.Dequeue() : DefaultOutcome);
            }

            public JArray EventsOf(int callIndex) => (JArray)Parse(Bodies[callIndex])["events"];
        }

        /// <summary>
        /// Parse with <c>DateParseHandling.None</c>.
        ///
        /// Newtonsoft's DEFAULT is to sniff any ISO-8601-looking string and turn it into a
        /// Date token, so a plain <c>JObject.Parse</c> would hand back
        /// <c>"08/18/2026 02:38:14"</c> for a <c>ts</c> that is <c>"2026-08-18T02:38:14.565Z"</c>
        /// on the wire — and the assertion would be testing Newtonsoft's round-trip, not the
        /// body the server actually receives.
        /// </summary>
        private static JObject Parse(string json)
        {
            using (var reader = new JsonTextReader(new System.IO.StringReader(json))
                   { DateParseHandling = DateParseHandling.None })
            {
                return JObject.Load(reader);
            }
        }

        private FakeSender _sender;
        private TelemetryService _svc;

        [SetUp]
        public void SetUp()
        {
            _sender = new FakeSender();
            _svc = new TelemetryService
            {
                SendsEnabled = true,               // the editor gate is off by default; tests opt in
                IsAuthenticated = () => true,      // never touch the AuthService MonoBehaviour
                BuildNumber = 2192,
            };
            _svc.Sender = _sender.Send;
            TelemetryService.ConfigureForTest(_svc);
        }

        [TearDown]
        public void TearDown() => TelemetryService.ResetForTest();

        private void RecordMany(int count, string name = "shot_taken")
        {
            for (int i = 0; i < count; i++)
                _svc.Record(name, new Dictionary<string, object> { ["i"] = i });
        }

        // ── §5.1 — 20 queued events trigger a flush ──────────────────────────────

        [Test]
        public void TwentyEvents_TriggerFlush_WithDistinctEventIds()
        {
            RecordMany(20);

            Assert.AreEqual(1, _sender.CallCount, "20 pending events must flush exactly once.");
            Assert.AreEqual(0, _svc.QueuedCount, "The queue must be drained by the flush.");

            var events = _sender.EventsOf(0);
            Assert.AreEqual(20, events.Count, "All 20 events must be in the batch.");

            var ids = new HashSet<string>();
            foreach (var e in events)
                Assert.IsTrue(ids.Add((string)e["event_id"]), "event_id must be unique per event.");

            var body = Parse(_sender.Bodies[0]);
            Assert.AreEqual(_svc.SessionId, (string)body["session_id"]);
            Assert.AreEqual(2192, (int)body["build_number"]);
        }

        [Test]
        public void NineteenEvents_DoNotFlush()
        {
            RecordMany(19);
            Assert.AreEqual(0, _sender.CallCount, "Below the threshold nothing may be sent.");
            Assert.AreEqual(19, _svc.QueuedCount);
        }

        // ── §5.2 — timer flush at 30s with fewer than 20 events ──────────────────

        [Test]
        public void TimerFlush_FiresAtInterval_WithPartialBatch()
        {
            RecordMany(3);
            Assert.AreEqual(0, _sender.CallCount);

            _svc.Tick(TelemetryConfig.FlushIntervalSeconds - 0.5f);
            Assert.AreEqual(0, _sender.CallCount, "Must not flush before the interval elapses.");

            _svc.Tick(1f);
            Assert.AreEqual(1, _sender.CallCount, "The interval must flush a partial batch.");
            Assert.AreEqual(3, _sender.EventsOf(0).Count);
        }

        [Test]
        public void TimerTick_WithEmptyQueue_SendsNothing()
        {
            _svc.Tick(TelemetryConfig.FlushIntervalSeconds * 2f);
            Assert.AreEqual(0, _sender.CallCount);
        }

        // ── §5.3 — queue cap drops the oldest ────────────────────────────────────

        [Test]
        public void QueueCap_DropsOldest_AndHoldsAtCap()
        {
            // Sends off so nothing drains and the queue can actually reach the cap.
            _svc.SendsEnabled = false;

            for (int i = 0; i < TelemetryConfig.QueueCap; i++)
                _svc.Record("shot_taken", new Dictionary<string, object> { ["i"] = i });
            Assert.AreEqual(TelemetryConfig.QueueCap, _svc.QueuedCount);

            _svc.Record("shot_taken", new Dictionary<string, object> { ["i"] = 9999 });
            Assert.AreEqual(TelemetryConfig.QueueCap, _svc.QueuedCount,
                "The 501st event must evict the oldest, not grow the queue.");

            // Prove it was the OLDEST that went: i=0 is gone, i=9999 is present.
            _svc.SendsEnabled = true;
            _svc.Flush();
            var events = _sender.EventsOf(0);
            Assert.AreEqual(TelemetryConfig.MaxEventsPerBatch, events.Count,
                "A flush must never build a batch bigger than the server's limit.");
            Assert.AreEqual(1, (int)events[0]["payload"]["i"], "i=0 must have been evicted.");
        }

        // ── §5.4 — failed flush re-enqueues ONCE, then drops ─────────────────────

        [Test]
        public void FailedFlush_ReEnqueuesOnce_ThenDropsOnSecondFailure()
        {
            _sender.Enqueue(false, false);

            RecordMany(20);
            Assert.AreEqual(1, _sender.CallCount);
            Assert.AreEqual(20, _svc.QueuedCount, "A failed batch must come back to the queue.");

            var firstIds = IdsOf(_sender.EventsOf(0));

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(
                "Dropped 20 event\\(s\\) after a second failed flush"));

            _svc.Flush();
            Assert.AreEqual(2, _sender.CallCount);
            var secondIds = IdsOf(_sender.EventsOf(1));
            CollectionAssert.AreEquivalent(firstIds, secondIds,
                "The retry must carry the SAME event_ids — that is what makes it idempotent server-side.");

            Assert.AreEqual(0, _svc.QueuedCount, "A second failure must drop the batch, not loop.");
        }

        [Test]
        public void SuccessfulFlush_DoesNotReEnqueue()
        {
            _sender.Enqueue(true);
            RecordMany(20);
            Assert.AreEqual(0, _svc.QueuedCount);
            _svc.Flush();
            Assert.AreEqual(1, _sender.CallCount, "Nothing left to send.");
        }

        private static List<string> IdsOf(JArray events)
        {
            var ids = new List<string>();
            foreach (var e in events) ids.Add((string)e["event_id"]);
            return ids;
        }

        // ── §5.5 — unauthenticated holds the queue; SignedIn drains it ───────────

        [Test]
        public void Unauthenticated_SendsNothing_ThenFlushesOnceAuthenticated()
        {
            bool authed = false;
            _svc.IsAuthenticated = () => authed;

            RecordMany(20);
            Assert.AreEqual(0, _sender.CallCount, "Nothing may be sent without a token.");
            Assert.AreEqual(20, _svc.QueuedCount, "Events must still accumulate while held.");

            // This is exactly what the AuthService.SignedIn hook does.
            authed = true;
            _svc.Flush();

            Assert.AreEqual(1, _sender.CallCount);
            Assert.AreEqual(20, _sender.EventsOf(0).Count, "The held events must survive the wait.");
        }

        [Test]
        public void AuthPredicateThatThrows_IsTreatedAsUnauthenticated_NotAsACrash()
        {
            _svc.IsAuthenticated = () => throw new InvalidOperationException("auth exploded");
            RecordMany(20);
            Assert.AreEqual(0, _sender.CallCount);
            Assert.AreEqual(20, _svc.QueuedCount);
        }

        // ── §5.6 — a throwing payload builder is swallowed ───────────────────────

        [Test]
        public void ThrowingPayloadBuilder_IsSwallowed_AndQueuesNothing()
        {
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(
                "Payload builder for 'shot_taken' threw and was swallowed"));

            Assert.DoesNotThrow(() => _svc.RecordSafe("shot_taken",
                () => throw new NullReferenceException("manager was null")));

            Assert.AreEqual(0, _svc.QueuedCount, "A failed builder must not queue a partial event.");
            Assert.AreEqual(0, _sender.CallCount);
        }

        [Test]
        public void ThrowingScreenProvider_DoesNotBreakErrorRecording()
        {
            _svc.CurrentScreenProvider = () => throw new InvalidOperationException("no screen manager");
            Assert.DoesNotThrow(() => _svc.RecordException("boom", "at Foo()"));
            Assert.AreEqual(1, _svc.QueuedCount);
        }

        // ── §5.7 — client_error cap ──────────────────────────────────────────────

        [Test]
        public void ClientError_CapsAtTenPerSession()
        {
            _svc.SendsEnabled = false; // keep everything in the queue so it can be counted

            for (int i = 0; i < TelemetryConfig.MaxClientErrorsPerSession; i++)
                _svc.RecordException($"exception #{i}", $"at Frame{i}()");

            Assert.AreEqual(TelemetryConfig.MaxClientErrorsPerSession, _svc.QueuedCount);

            _svc.RecordException("exception #11", "at Frame11()");
            Assert.AreEqual(TelemetryConfig.MaxClientErrorsPerSession, _svc.QueuedCount,
                "The 11th distinct exception in a session must not be enqueued.");
            Assert.AreEqual(TelemetryConfig.MaxClientErrorsPerSession, _svc.ErrorsThisSession);
        }

        [Test]
        public void ClientError_DedupesByMessageAndFirstStackLine()
        {
            _svc.SendsEnabled = false;

            _svc.RecordException("NRE in ShotController", "at ShotController.Fire()\nat Update()");
            _svc.RecordException("NRE in ShotController", "at ShotController.Fire()\nat SomethingElse()");

            Assert.AreEqual(1, _svc.QueuedCount,
                "Same message + same first stack line is one row, however many times it fires.");
        }

        [Test]
        public void ClientError_TruncatesMessageAndStack()
        {
            _svc.SendsEnabled = false;
            _svc.RecordException(new string('m', 5000), new string('s', 50000));
            _svc.SendsEnabled = true;
            _svc.Flush();

            var payload = _sender.EventsOf(0)[0]["payload"];
            Assert.AreEqual(TelemetryConfig.MaxErrorMessageChars, ((string)payload["message"]).Length);
            Assert.AreEqual(TelemetryConfig.MaxErrorStackChars, ((string)payload["stack"]).Length);
        }

        // ── batch shape ──────────────────────────────────────────────────────────

        [Test]
        public void BatchJson_CarriesTheFullEnvelope()
        {
            _svc.SessionId = "11111111-2222-3333-4444-555555555555";
            _svc.AppVersion = "1.5.7";
            _svc.Platform = "IPhonePlayer";
            _svc.DeviceModel = "iPhone14,2";
            _svc.Os = "iOS 18.5";

            _svc.Record("session_start", new Dictionary<string, object> { ["memory_mb"] = 6144 });
            _svc.Flush();

            var body = Parse(_sender.Bodies[0]);
            Assert.AreEqual("11111111-2222-3333-4444-555555555555", (string)body["session_id"]);
            Assert.AreEqual("1.5.7", (string)body["app_version"]);
            Assert.AreEqual("IPhonePlayer", (string)body["platform"]);
            Assert.AreEqual("iPhone14,2", (string)body["device_model"]);
            Assert.AreEqual("iOS 18.5", (string)body["os"]);

            var evt = ((JArray)body["events"])[0];
            Assert.AreEqual("session_start", (string)evt["name"]);
            Assert.AreEqual(6144, (int)evt["payload"]["memory_mb"]);
            Assert.IsTrue(((string)evt["ts"]).EndsWith("Z"), "ts must be UTC ISO-8601.");
        }

        [Test]
        public void InFlightFlush_DoesNotStartASecondOne()
        {
            _sender.HoldCallback = true;
            RecordMany(20);
            Assert.AreEqual(1, _sender.CallCount);

            RecordMany(20);
            Assert.AreEqual(1, _sender.CallCount,
                "A second flush must wait for the first to complete, not race it.");
            Assert.AreEqual(20, _svc.QueuedCount);
        }

        [Test]
        public void SendsDisabled_QueuesButNeverSends()
        {
            _svc.SendsEnabled = false;
            RecordMany(25);
            Assert.AreEqual(0, _sender.CallCount, "The editor gate must not reach the network.");
            Assert.AreEqual(25, _svc.QueuedCount, "…but events must still be queued.");
        }
    }
}
