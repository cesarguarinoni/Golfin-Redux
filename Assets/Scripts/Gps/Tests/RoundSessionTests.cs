// gps_checkin §C3 — the one live round: its mirror, its idempotency keys, and its clock.
//
// THE KEYS ARE THE POINT. Every other assertion here is about painting the card correctly; the
// three key tests are about a player force-quitting mid-check-in and NOT ending up with a round
// they can neither see nor close. That is the property the whole persisted-key design exists for
// and it is invisible in any screenshot.
using System;
using System.Collections;
using System.Collections.Generic;
using Golfin.Net;
using Golfin.Net.Tests;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Golfin.Gps.Tests
{
    public class RoundSessionTests
    {
        static readonly DateTimeOffset T0 =
            new DateTimeOffset(2026, 9, 3, 8, 12, 0, TimeSpan.Zero);

        static RoundSession Session(InMemoryKeyValueStore store, DateTimeOffset now)
            => new RoundSession(store, () => now);

        static ActivityDto Active(long id = 42, string checkInAt = "2026-09-03T08:12:00+00:00")
            => new ActivityDto
            {
                Id = id,
                VenueId = 1993,
                VenueName = "TEST Office (WeWork Harumi)",
                Status = "active",
                CheckInAt = checkInAt,
                GpsVerified = true,
                GpsCheckCount = 1,
            };

        // ── The mirror ────────────────────────────────────────────────────────

        [Test]
        public void SetActive_MirrorsTheRow_AndTheNextSessionPaintsItWithoutAFetch()
        {
            var store = new InMemoryKeyValueStore();
            Session(store, T0).SetActive(Active());

            // A NEW session over the same store is what a relaunch is.
            var relaunched = Session(store, T0);
            Assert.IsTrue(relaunched.HasActive);
            Assert.AreEqual(42, relaunched.Active.Id);
            Assert.AreEqual("TEST Office (WeWork Harumi)", relaunched.Active.VenueName);
        }

        [Test]
        public void AMirrorThatIsNotActive_IsDropped()
        {
            // Written by an older build, or closed on another device while this one slept. The
            // card must never paint a round that is over.
            var store = new InMemoryKeyValueStore();
            var row = Active();
            row.Status = "completed";
            store.SetString(RoundSession.PrefsRound, JsonConvert.SerializeObject(row));

            Assert.IsFalse(Session(store, T0).HasActive);
        }

        [Test]
        public void ACorruptMirror_DegradesToNoRound_RatherThanThrowing()
        {
            var store = new InMemoryKeyValueStore();
            store.SetString(RoundSession.PrefsRound, "{not json");
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            Assert.IsFalse(Session(store, T0).HasActive);
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = false;
        }

        [Test]
        public void SetActiveNull_ClearsTheMirror_AndRaisesTheEvent()
        {
            var store = new InMemoryKeyValueStore();
            var s = Session(store, T0);
            s.SetActive(Active());

            int raised = 0;
            ActivityDto seen = Active();
            s.OnActiveChanged += r => { raised++; seen = r; };

            s.SetActive(null);
            Assert.AreEqual(1, raised);
            Assert.IsNull(seen);
            Assert.IsFalse(s.HasActive);
            Assert.AreEqual(string.Empty, store.GetString(RoundSession.PrefsRound, string.Empty));
        }

        [Test]
        public void SettingTheSameRoundTwice_DoesNotRaiseTheEvent()
        {
            // /activity/active answers on every entry and every resume; re-raising on each would
            // re-run the screen's whole flip animation while the player is looking at it.
            var s = Session(new InMemoryKeyValueStore(), T0);
            s.SetActive(Active());
            int raised = 0;
            s.OnActiveChanged += _ => raised++;
            s.SetActive(Active());
            Assert.AreEqual(0, raised);
        }

        // ── Idempotency keys ──────────────────────────────────────────────────

        [Test]
        public void BeginCheckInKey_PersistsBeforeItReturns()
        {
            var store = new InMemoryKeyValueStore();
            string key = Session(store, T0).BeginCheckInKey();

            Assert.IsFalse(string.IsNullOrEmpty(key));
            Assert.AreEqual(key, store.GetString(RoundSession.PrefsCheckInKey, string.Empty),
                "the key must be on disk BEFORE the request leaves — that is the whole force-quit story");
        }

        [Test]
        public void AForceQuitMidCheckIn_ReplaysTheSameKey()
        {
            var store = new InMemoryKeyValueStore();
            string first = Session(store, T0).BeginCheckInKey();

            // The process dies here. A brand-new session over the same store is the relaunch.
            string second = Session(store, T0).BeginCheckInKey();

            Assert.AreEqual(first, second,
                "a NEW key would open a second round the player can neither see nor close");
        }

        [Test]
        public void ClearingTheKey_MintsAFreshOneNextTime()
        {
            var store = new InMemoryKeyValueStore();
            var s = Session(store, T0);
            string first = s.BeginCheckInKey();
            s.ClearCheckInKey();
            Assert.AreNotEqual(first, s.BeginCheckInKey());
        }

        [Test]
        public void CheckInAndCheckOutKeys_AreIndependent()
        {
            var s = Session(new InMemoryKeyValueStore(), T0);
            Assert.AreNotEqual(s.BeginCheckInKey(), s.BeginCheckOutKey());
        }

        // ── Elapsed and expiry ────────────────────────────────────────────────

        [Test]
        public void Elapsed_ComesFromTheServerTimestamp_NotAClientStopwatch()
        {
            // The app is killed and relaunched during a four-hour round; a stopwatch started at
            // check-in would restart at zero.
            var s = Session(new InMemoryKeyValueStore(), T0.AddHours(1).AddMinutes(24));
            s.SetActive(Active());
            Assert.AreEqual(84, (int)s.Elapsed.TotalMinutes);
            Assert.AreEqual("1:24", RoundSession.FormatElapsed(s.Elapsed));
        }

        [Test]
        public void ATimestampWithNoOffset_IsReadAsUtc()
        {
            // PostgREST can answer without one. Reading it as device-local time on a phone in JST
            // would make every round read as nine hours old — and therefore expired.
            var s = Session(new InMemoryKeyValueStore(), T0.AddMinutes(30));
            s.SetActive(Active(checkInAt: "2026-09-03T08:12:00"));
            Assert.AreEqual(30, (int)s.Elapsed.TotalMinutes);
        }

        [Test]
        public void ElapsedNeverGoesNegative_WhenTheDeviceClockIsBehindTheServer()
        {
            var s = Session(new InMemoryKeyValueStore(), T0.AddMinutes(-5));
            s.SetActive(Active());
            Assert.AreEqual(TimeSpan.Zero, s.Elapsed);
        }

        [Test]
        public void FormatElapsed_PadsTheMinutes_SoTheDigitsDoNotJitter()
        {
            Assert.AreEqual("0:05", RoundSession.FormatElapsed(TimeSpan.FromMinutes(5)));
            Assert.AreEqual("0:59", RoundSession.FormatElapsed(TimeSpan.FromMinutes(59)));
            Assert.AreEqual("1:00", RoundSession.FormatElapsed(TimeSpan.FromMinutes(60)));
            Assert.AreEqual("12:34", RoundSession.FormatElapsed(new TimeSpan(12, 34, 0)));
        }

        [Test]
        public void IsExpired_MatchesTheServersEightHourRule()
        {
            var store = new InMemoryKeyValueStore();

            var justInside = Session(store, T0.AddHours(7).AddMinutes(59));
            justInside.SetActive(Active());
            Assert.IsFalse(justInside.IsExpired);

            var justOutside = Session(store, T0.AddHours(8).AddMinutes(1));
            justOutside.SetActive(Active());
            Assert.IsTrue(justOutside.IsExpired);
        }

        [Test]
        public void NoRound_MeansNoElapsedAndNoExpiry()
        {
            var s = Session(new InMemoryKeyValueStore(), T0);
            Assert.AreEqual(TimeSpan.Zero, s.Elapsed);
            Assert.IsFalse(s.IsExpired);
            Assert.IsNull(s.CheckInAt);
        }

        // ── GPS quality and the trail ─────────────────────────────────────────

        [Test]
        public void Quality_ThresholdsMatchTheSpec()
        {
            var s = Session(new InMemoryKeyValueStore(), T0);
            Assert.AreEqual(GpsQuality.Low, s.Quality, "no fix at all is LOW, which is honest");

            GpsSessionTracker.ConfigureForTest(GpsTestApi.Tracker(new FakeClock(0)));
            try
            {
                s.RecordFix(new LocationFix { Lat = 35.654103, Lon = 139.779219, AccuracyM = 8f });
                Assert.AreEqual(GpsQuality.High, s.Quality);

                s.RecordFix(new LocationFix { Lat = 35.654103, Lon = 139.779219, AccuracyM = 30f });
                Assert.AreEqual(GpsQuality.Medium, s.Quality);

                s.RecordFix(new LocationFix { Lat = 35.654103, Lon = 139.779219, AccuracyM = 120f });
                Assert.AreEqual(GpsQuality.Low, s.Quality);
            }
            finally { GpsSessionTracker.ResetForTest(); }
        }

        [Test]
        public void RecordFix_FeedsTheTracker_AndTheThrottleStopsItInflatingTheCount()
        {
            // The anti-cheat property score.py pays Trust +20 for: repeatedly returning to the
            // screen must NOT run gps_check_count up.
            var clock = new FakeClock(0);
            GpsSessionTracker.ConfigureForTest(GpsTestApi.Tracker(clock));
            try
            {
                var s = Session(new InMemoryKeyValueStore(), T0);
                for (int i = 0; i < 20; i++)
                    s.RecordFix(new LocationFix { Lat = 35.654103, Lon = 139.779219, AccuracyM = 8f });

                Assert.AreEqual(1, s.FixCount, "20 fixes in the same second and place are ONE");

                // Two more, each a real hour apart, ARE worth counting.
                clock.AdvanceHours(1);
                s.RecordFix(new LocationFix { Lat = 35.654103, Lon = 139.779219, AccuracyM = 8f });
                clock.AdvanceHours(1);
                s.RecordFix(new LocationFix { Lat = 35.654103, Lon = 139.779219, AccuracyM = 8f });

                Assert.AreEqual(3, s.FixCount,
                    "K4's threshold is 3 — a foreground round with two resumes must reach it");
            }
            finally { GpsSessionTracker.ResetForTest(); }
        }

        [Test]
        public void RecordFix_IgnoresNull_SoAFailedFetchCannotBlankTheCard()
        {
            var s = Session(new InMemoryKeyValueStore(), T0);
            s.RecordFix(null);
            Assert.IsNull(s.LastFix);
            Assert.AreEqual(1, s.FixCount);
        }

        // ── Server sync ───────────────────────────────────────────────────────

        [Test]
        public void Refresh_AdoptsTheServersRow()
        {
            var transport = new FakeHttpTransport().Enqueue(HttpResponse.Status(200,
                "{\"data\":{\"id\":77,\"venue_id\":1993,\"venue_name\":\"TEST Office\"," +
                "\"status\":\"active\",\"check_in_at\":\"2026-09-03T08:12:00+00:00\"}}"));
            var service = new ActivityService(GpsTestApi.Client(transport));

            var s = Session(new InMemoryKeyValueStore(), T0);
            bool ok = false;
            Pump.Drain(s.Refresh(service, v => ok = v));

            Assert.IsTrue(ok);
            Assert.IsTrue(s.HasActive);
            Assert.AreEqual(77, s.Active.Id);
            Assert.AreEqual(Endpoints.ActivityActive, transport.SentUrls[0]);
        }

        [Test]
        public void Refresh_WithNoOpenRound_ClearsTheMirror()
        {
            // 200 {"data": null} is the legitimate "no round open" — a SUCCESS with null Data.
            var transport = new FakeHttpTransport().Enqueue(HttpResponse.Status(200, "{\"data\":null}"));
            var s = Session(new InMemoryKeyValueStore(), T0);
            s.SetActive(Active());

            Pump.Drain(s.Refresh(new ActivityService(GpsTestApi.Client(transport)), null));
            Assert.IsFalse(s.HasActive);
        }

        [Test]
        public void Refresh_ThatFails_KeepsTheMirroredRound()
        {
            // The round IS still open; the tunnel is just down. Clearing it here would take the
            // player's live card away for a network blip.
            var transport = new FakeHttpTransport().Enqueue(HttpResponse.Status(500, "{}"));
            var s = Session(new InMemoryKeyValueStore(), T0);
            s.SetActive(Active());

            bool ok = true;
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            Pump.Drain(s.Refresh(new ActivityService(GpsTestApi.Client(transport)), v => ok = v));
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = false;

            Assert.IsFalse(ok);
            Assert.IsTrue(s.HasActive, "a failed refresh must not close the player's round");
        }
    }
}
