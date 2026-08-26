using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Golfin.Net;
using NUnit.Framework;

namespace Golfin.Content.Tests
{
    /// <summary>
    /// The FETCH path, end to end, with a fake transport instead of a socket — so the three
    /// scenarios the acceptance list cares most about become deterministic assertions rather than
    /// a log read: <b>airplane mode</b>, <b>the kill switch</b>, and <b>a payload this build cannot
    /// map</b>. All three are designed paths, and all three must leave the player with a working
    /// game.
    ///
    /// <para>
    /// The coroutine is pumped by hand (<c>while (r.MoveNext())</c>), the same convention
    /// <c>ApiClient</c> and <c>RemoteNoticeSource</c> are written for — no play mode, no network.
    /// </para>
    /// </summary>
    public class ContentFetchPathTests
    {
        private string _path;
        private string _backup;
        private bool _hadPreExisting;

        /// <summary>Answers every request with one canned response. No sockets, no clock.</summary>
        private sealed class FakeTransport : IHttpTransport
        {
            private readonly HttpResponse _response;
            public string LastUrl { get; private set; }
            public int Calls { get; private set; }

            public FakeTransport(HttpResponse response) { _response = response; }

            public IEnumerator Send(HttpRequest request, Action<HttpResponse> onResponse)
            {
                Calls++;
                LastUrl = request.Url;
                onResponse?.Invoke(_response);
                yield break;
            }
        }

        private sealed class NoAuth : IAuthTokenProvider
        {
            public bool IsAuthenticated => false;
            public string AccessToken => null;
            public IEnumerator Refresh(Action<bool> onDone) { onDone?.Invoke(false); yield break; }
        }

        private sealed class NullRunner : ICoroutineRunner
        {
            public void Run(IEnumerator routine) { while (routine != null && routine.MoveNext()) { } }
        }

        private FakeTransport Install(HttpResponse response)
        {
            var transport = new FakeTransport(response);
            var client = new ApiClient(transport, new NoAuth(), new NullRunner())
            {
                // No wall-clock waiting: a connection failure otherwise sleeps 0.75s per retry.
                RetryDelaySeconds = 0f,
                LogRequests = false,
            };
            ApiClient.ConfigureForTest(client);
            return transport;
        }

        private static string Pump(int since, int build)
        {
            string body = null;
            IEnumerator r = RemoteContentSource.FetchRoutine(since, build, b => body = b);
            while (r.MoveNext()) { }
            return body;
        }

        [SetUp]
        public void SetUp()
        {
            _path   = RemoteContentSource.TextsCachePath;
            _backup = _path + ".testbackup";
            _hadPreExisting = File.Exists(_path);
            if (_hadPreExisting)
            {
                if (File.Exists(_backup)) File.Delete(_backup);
                File.Move(_path, _backup);
            }
        }

        [TearDown]
        public void TearDown()
        {
            ApiClient.ResetForTest();
            Endpoints.ResetToDefault();

            if (File.Exists(_path)) File.Delete(_path);
            if (File.Exists(_path + ".tmp")) File.Delete(_path + ".tmp");
            if (_hadPreExisting && File.Exists(_backup)) File.Move(_backup, _path);
            else if (File.Exists(_backup)) File.Delete(_backup);
        }

        // ── Airplane mode ─────────────────────────────────────────────────────

        [Test]
        public void AirplaneMode_ColdLaunch_YieldsNullBody_AndWritesNoCache()
        {
            Install(HttpResponse.ConnectionFailure("Cannot resolve destination host"));

            string body = Pump(11, 2297);

            Assert.IsNull(body,
                "A connection failure must hand back null, not an empty string — the caller branches " +
                "on 'nothing arrived' and must keep whatever it already has.");
            Assert.IsFalse(File.Exists(_path), "No cache may be created from a failed fetch.");
        }

        [Test]
        public void AirplaneMode_WarmCache_LeavesTheCacheIntact()
        {
            const string good = @"{""data"":{""enabled"":true,""catalogs"":{""texts"":{""version"":11,""changed"":[
                {""id"":""BTN_START"",""is_active"":true,""data"":{""English"":""TEE OFF""}}]}}}}";
            RemoteContentSource.WriteCache(good);

            Install(HttpResponse.ConnectionFailure());
            Pump(11, 2297);

            Assert.AreEqual(good, RemoteContentSource.ReadCache(),
                "Offline must never cost the player the overlay they already had — the cached " +
                "payload still applies at the next launch.");
        }

        [Test]
        public void ServerError_IsTreatedLikeOffline_TheCacheSurvives()
        {
            RemoteContentSource.WriteCache(@"{""v"":1}");

            Install(HttpResponse.Status(500, @"{""detail"":""boom""}"));
            Assert.IsNull(Pump(11, 2297));

            Assert.AreEqual(@"{""v"":1}", RemoteContentSource.ReadCache());
        }

        // ── The request it actually sends ─────────────────────────────────────

        [Test]
        public void FetchRoutine_SendsThePerCatalogCursor_AndNarrowsToTexts()
        {
            var transport = Install(HttpResponse.Status(200,
                @"{""data"":{""enabled"":true,""catalogs"":{""texts"":{""version"":11,""changed"":[]}}}}"));

            Pump(11, 2297);

            StringAssert.Contains("/content?", transport.LastUrl);
            StringAssert.Contains("since=texts%3a11", transport.LastUrl.ToLowerInvariant());
            StringAssert.Contains("build=2297", transport.LastUrl);
            StringAssert.Contains("catalogs=texts", transport.LastUrl,
                "Asking for every catalog would pull the 275 KB clubs payload onto a boot that " +
                "reads none of it.");
        }

        [Test]
        public void FetchRoutine_ClampsANegativeCursorToZero()
        {
            var transport = Install(HttpResponse.Status(200, @"{""data"":{""enabled"":true,""catalogs"":{}}}"));

            Pump(-3, 2297);

            StringAssert.Contains("since=texts%3a0", transport.LastUrl.ToLowerInvariant(),
                "A negative cursor would be clamped server-side anyway; sending 0 keeps the two ends " +
                "agreeing on what was asked for.");
        }

        // ── Success ───────────────────────────────────────────────────────────

        [Test]
        public void SuccessfulFetch_HandsBackTheRawEnvelopedBody_ForVerbatimMirroring()
        {
            const string raw = @"{""data"":{""enabled"":true,""catalogs"":{""texts"":{""version"":12,""full"":false,""changed"":[
                {""id"":""BTN_START"",""is_active"":true,""min_build"":0,""data"":{""English"":""TEE OFF"",""Japanese"":""ティーオフ""}}]}}}}";

            Install(HttpResponse.Status(200, raw));

            Assert.AreEqual(raw, Pump(11, 2297),
                "The RAW body is what gets mirrored — a mapped view would strip anything THIS build " +
                "cannot read, and a later build could never recover it.");

            // And it maps to the row an admin publish would have produced.
            var overlay = ContentTextsMapper.Map(Pump(11, 2297));
            Assert.AreEqual("TEE OFF", overlay.Rows["BTN_START"].english);
            Assert.AreEqual(12, overlay.Version);
        }

        [Test]
        public void EmptyDelta_TheSteadyState_ParsesAsAnEnabledZeroRowOverlay()
        {
            Install(HttpResponse.Status(200,
                @"{""data"":{""fetched_at"":""2026-08-26T00:00:00+00:00"",""enabled"":true,""latest_version"":11,
                   ""catalogs"":{""texts"":{""version"":11,""full"":false,""changed"":[]}}}}"));

            var overlay = ContentTextsMapper.Map(Pump(11, 2297));

            Assert.IsTrue(overlay.Parsed);
            Assert.IsTrue(overlay.Enabled);
            Assert.AreEqual(0, overlay.Rows.Count);
        }

        // ── Kill switch ───────────────────────────────────────────────────────

        [Test]
        public void KillSwitch_MapsToDisabled_SoTheCallerDropsTheCache()
        {
            RemoteContentSource.WriteCache(@"{""data"":{""enabled"":true,""catalogs"":{""texts"":{""version"":11,""changed"":[]}}}}");
            Assert.IsTrue(File.Exists(_path), "precondition: a warm cache exists");

            Install(HttpResponse.Status(200, @"{""data"":{""enabled"":false,""catalogs"":{}}}"));

            var overlay = ContentTextsMapper.Map(Pump(11, 2297));
            Assert.IsTrue(overlay.Parsed);
            Assert.IsFalse(overlay.Enabled);

            // This is what ContentService.RefreshRoutine does on that verdict.
            RemoteContentSource.ClearCache();
            Assert.IsFalse(File.Exists(_path),
                "One enabled:false must fully undo remote text: no cache means the next launch is " +
                "bundled-only, with or without a network.");
        }
    }
}
