// Order: gps_trust_core §Tests 1-9 — the constants and the throttle/prune/count rules, pinned.
using System.Collections.Generic;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Golfin.Gps.Tests
{
    /// <summary>
    /// These tests exist so a "tidy-up" of a constant is a red test rather than a silent anti-cheat
    /// regression. Every value asserted here is transcribed from
    /// <c>playlife/lib/common/presentation/controller/gps_session_tracker.dart</c>.
    /// </summary>
    public class GpsSessionTrackerTests
    {
        // Tokyo Station.
        private const double Lat = 35.681236;
        private const double Lon = 139.767125;

        /// <summary>Roughly <paramref name="metres"/> north of <see cref="Lat"/> (1° lat ≈ 111,195 m).</summary>
        private static double LatOffsetBy(double metres) => Lat + metres / 111195.0;

        [Test]
        public void Constants_MatchTheDartSource()
        {
            Assert.AreEqual(12L * 60 * 60 * 1000, GpsSessionTracker.RetentionMs, "_retention = Duration(hours: 12)");
            Assert.AreEqual(100, GpsSessionTracker.MaxFixes, "_maxFixes = 100");
            Assert.AreEqual(5L * 60 * 1000, GpsSessionTracker.RecordMinGapMs, "_recordMinGap = Duration(minutes: 5)");
            Assert.AreEqual(100.0, GpsSessionTracker.RecordMinMoveM, "_recordMinMoveM = 100.0");
            Assert.AreEqual(10L * 60 * 1000, GpsSessionTracker.CountMinGapMs, "_countMinGap = Duration(minutes: 10)");
            Assert.AreEqual(5000.0, GpsSessionTracker.SessionRadiusM, "_sessionRadiusM = 5000.0");
            Assert.AreEqual(8L * 60 * 60 * 1000, GpsSessionTracker.SessionWindowMs, "_sessionWindow = Duration(hours: 8)");
            Assert.AreEqual(6371000.0, GpsSessionTracker.EarthRadiusM, "haversine const r = 6371000.0");
        }

        [Test]
        public void PrefsKey_IsTheDartOne()
        {
            Assert.AreEqual("gps_session_fixes_v1", PlayerPrefsGpsFixStore.PrefsKey);
        }

        // ── 1. throttle is AND, not OR ─────────────────────────────────────────────

        [Test]
        public void RecordFix_DropsAFixThatIsBothTooSoonAndTooClose()
        {
            var store = new InMemoryGpsFixStore();
            var clock = new FakeClock(1_000_000);
            var tracker = GpsTestApi.Tracker(clock, store);

            tracker.RecordFix(Lat, Lon);
            clock.AdvanceMinutes(4);
            tracker.RecordFix(LatOffsetBy(50), Lon);   // 4 min AND 50 m → dropped

            Assert.AreEqual(1, store.Load().Count);
        }

        [Test]
        public void RecordFix_KeepsAFixThatMovedFarEnoughEvenWhenTooSoon()
        {
            var store = new InMemoryGpsFixStore();
            var clock = new FakeClock(1_000_000);
            var tracker = GpsTestApi.Tracker(clock, store);

            tracker.RecordFix(Lat, Lon);
            clock.AdvanceMinutes(4);
            tracker.RecordFix(LatOffsetBy(150), Lon);  // 4 min but 150 m → kept

            Assert.AreEqual(2, store.Load().Count);
        }

        [Test]
        public void RecordFix_KeepsAFixThatWaitedLongEnoughEvenWhenBarelyMoved()
        {
            var store = new InMemoryGpsFixStore();
            var clock = new FakeClock(1_000_000);
            var tracker = GpsTestApi.Tracker(clock, store);

            tracker.RecordFix(Lat, Lon);
            clock.AdvanceMinutes(6);
            tracker.RecordFix(LatOffsetBy(50), Lon);   // 6 min, 50 m → kept

            Assert.AreEqual(2, store.Load().Count);
        }

        // ── 2. prune by age ────────────────────────────────────────────────────────

        [Test]
        public void RecordFix_PrunesFixesOlderThanTwelveHoursButKeepsEleven()
        {
            long now = 100L * 60 * 60 * 1000;   // well clear of epoch so the -13 h fix is positive
            var store = new InMemoryGpsFixStore(JsonConvert.SerializeObject(new List<GpsFix>
            {
                new GpsFix(Lat, Lon, now - 13L * 60 * 60 * 1000),   // −13 h → dropped
                new GpsFix(Lat, Lon, now - 11L * 60 * 60 * 1000)    // −11 h → survives
            }));

            var tracker = GpsTestApi.Tracker(new FakeClock(now), store);
            tracker.RecordFix(LatOffsetBy(500), Lon);

            List<GpsFix> kept = store.Load();
            Assert.AreEqual(2, kept.Count, "the −13 h fix should have been pruned and the new one added");
            Assert.AreEqual(now - 11L * 60 * 60 * 1000, kept[0].T);
            Assert.AreEqual(now, kept[1].T);
        }

        // ── 3. prune by count ──────────────────────────────────────────────────────

        [Test]
        public void RecordFix_DropsTheOldestOnceOverOneHundred()
        {
            var store = new InMemoryGpsFixStore();
            var clock = new FakeClock(1_000_000_000);
            var tracker = GpsTestApi.Tracker(clock, store);

            long firstT = clock.NowMs;
            for (int i = 0; i < 101; i++)
            {
                tracker.RecordFix(Lat, Lon);
                clock.AdvanceMinutes(6);           // > 5 min so every fix is recorded
            }

            List<GpsFix> kept = store.Load();
            Assert.AreEqual(100, kept.Count, "MaxFixes = 100");
            Assert.AreNotEqual(firstT, kept[0].T, "the OLDEST fix is the one dropped");
            Assert.AreEqual(firstT + 6L * 60 * 1000, kept[0].T);
        }

        // ── 4-5. sessionNear filtering ─────────────────────────────────────────────

        [Test]
        public void SessionNear_WithNoFixes_IsCountOneAndNoCoordinates()
        {
            GpsSession s = GpsTestApi.Tracker(new FakeClock(1_000_000)).SessionNear(Lat, Lon);

            Assert.AreEqual(1, s.CheckCount);
            Assert.IsNull(s.StartLat);
            Assert.IsNull(s.StartLon);
            Assert.IsNull(s.EndLat);
            Assert.IsNull(s.EndLon);
        }

        [Test]
        public void SessionNear_ExcludesFixesOutsideTheRadiusOrTheWindow()
        {
            long now = 100L * 60 * 60 * 1000;
            var store = new InMemoryGpsFixStore(JsonConvert.SerializeObject(new List<GpsFix>
            {
                new GpsFix(LatOffsetBy(6000), Lon, now - 1L * 60 * 60 * 1000),   // 6 km away → out
                new GpsFix(Lat, Lon,             now - 9L * 60 * 60 * 1000),     // 9 h ago    → out
                new GpsFix(LatOffsetBy(4000), Lon, now - 7L * 60 * 60 * 1000)    // 4 km / 7 h → IN
            }));

            GpsSession s = GpsTestApi.Tracker(new FakeClock(now), store).SessionNear(Lat, Lon);

            Assert.AreEqual(1, s.CheckCount);
            Assert.AreEqual(LatOffsetBy(4000), s.StartLat.Value, 1e-9);
            Assert.AreEqual(LatOffsetBy(4000), s.EndLat.Value, 1e-9);
        }

        // ── 6. count walks the LAST COUNTED fix, not the previous one ──────────────

        [Test]
        public void SessionNear_CountsAgainstTheLastCountedFixNotThePreviousOne()
        {
            long t0 = 100L * 60 * 60 * 1000;
            var store = new InMemoryGpsFixStore(JsonConvert.SerializeObject(new List<GpsFix>
            {
                new GpsFix(Lat, Lon, t0),
                new GpsFix(LatOffsetBy(10), Lon, t0 + 6L * 60 * 1000),
                new GpsFix(LatOffsetBy(20), Lon, t0 + 12L * 60 * 1000),
                new GpsFix(LatOffsetBy(30), Lon, t0 + 30L * 60 * 1000)
            }));

            GpsSession s = GpsTestApi.Tracker(new FakeClock(t0 + 40L * 60 * 1000), store).SessionNear(Lat, Lon);

            // 0 counted; +6 is < 10 min after it → skipped; +12 IS ≥ 10 min after 0 → counted;
            // +30 is ≥ 10 min after +12 → counted. Three, not four.
            Assert.AreEqual(3, s.CheckCount);
            Assert.AreEqual(Lat, s.StartLat.Value, 1e-9, "start is the FIRST fix");
            Assert.AreEqual(LatOffsetBy(30), s.EndLat.Value, 1e-9, "end is the LAST fix");
        }

        // ── 7. wire schema ─────────────────────────────────────────────────────────

        [Test]
        public void Store_SerialisesTheDartWireSchemaExactly()
        {
            var store = new InMemoryGpsFixStore();
            var tracker = GpsTestApi.Tracker(new FakeClock(1756000000000), store);

            tracker.RecordFix(35.5, 139.5);

            Assert.AreEqual("[{\"lat\":35.5,\"lon\":139.5,\"t\":1756000000000}]", store.Raw);

            List<GpsFix> back = store.Load();
            Assert.AreEqual(1, back.Count);
            Assert.AreEqual(35.5, back[0].Lat, 1e-9);
            Assert.AreEqual(139.5, back[0].Lon, 1e-9);
            Assert.AreEqual(1756000000000L, back[0].T);
        }

        // ── 8. malformed store ─────────────────────────────────────────────────────

        [Test]
        public void Store_MalformedPayloadReadsEmptyAndRecordingStillWorks()
        {
            var store = new InMemoryGpsFixStore("not json");

            Assert.AreEqual(0, store.Load().Count, "a corrupt log must never block a submit");

            GpsTestApi.Tracker(new FakeClock(1_000_000), store).RecordFix(Lat, Lon);

            Assert.AreEqual(1, store.Load().Count);
        }

        // ── 9. haversine ───────────────────────────────────────────────────────────

        [Test]
        public void HaversineM_TokyoStationToShinjuku()
        {
            double d = GpsSessionTracker.HaversineM(35.681236, 139.767125, 35.690921, 139.700258);
            Assert.AreEqual(6134.0, d, 20.0, "measured " + d.ToString("F1") + " m");
        }

        [Test]
        public void HaversineM_IsZeroForTheSamePoint()
        {
            Assert.AreEqual(0.0, GpsSessionTracker.HaversineM(Lat, Lon, Lat, Lon), 1e-6);
        }
    }
}
