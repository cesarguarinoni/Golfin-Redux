// Order: gps_trust_core §2 — port of gps_session_tracker.dart. EVERY constant here is transcribed
// from that file and asserted by GpsSessionTrackerTests. A "tidy-up" of a number is a test failure,
// not a refactor: the backend awards Trust +20 off gps_check_count (score.py:178-180), so a loosened
// throttle here silently hands the anti-cheat away while every screen above it looks fine.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Golfin.Gps
{
    /// <summary>
    /// The evidence that the player was actually AT the course for a while, not that they teleported
    /// there to press submit.
    ///
    /// Unlike the Dart original this is an INSTANCE class, not a static one: the clock and the store
    /// are injected so an EditMode test can move time without sleeping and without touching
    /// PlayerPrefs. <see cref="Instance"/> is the shipping wiring (PlayerPrefs + wall clock).
    /// </summary>
    public sealed class GpsSessionTracker
    {
        // ── Constants, transcribed from gps_session_tracker.dart ───────────────────
        // dart: static const _retention = Duration(hours: 12);
        public const long RetentionMs = 12L * 60 * 60 * 1000;
        // dart: static const _maxFixes = 100;
        public const int MaxFixes = 100;
        // dart: static const _recordMinGap = Duration(minutes: 5);
        public const long RecordMinGapMs = 5L * 60 * 1000;
        // dart: static const _recordMinMoveM = 100.0;
        public const double RecordMinMoveM = 100.0;
        // dart: static const _countMinGap = Duration(minutes: 10);
        public const long CountMinGapMs = 10L * 60 * 1000;
        // dart: static const _sessionRadiusM = 5000.0;
        public const double SessionRadiusM = 5000.0;
        // dart: static const _sessionWindow = Duration(hours: 8);
        public const long SessionWindowMs = 8L * 60 * 60 * 1000;

        /// <summary>Earth radius used by the Dart haversine (<c>const r = 6371000.0</c>).</summary>
        public const double EarthRadiusM = 6371000.0;

        private static GpsSessionTracker _instance;

        /// <summary>Shipping wiring: PlayerPrefs-backed log on the real UTC clock.</summary>
        public static GpsSessionTracker Instance => _instance ?? (_instance = new GpsSessionTracker(
            new PlayerPrefsGpsFixStore(),
            () => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));

        public static void ConfigureForTest(GpsSessionTracker tracker) => _instance = tracker;

        public static void ResetForTest() => _instance = null;

        private readonly IGpsFixStore _store;
        private readonly Func<long> _nowMs;

        public GpsSessionTracker(IGpsFixStore store, Func<long> nowMs)
        {
            _store = store ?? new InMemoryGpsFixStore();
            _nowMs = nowMs ?? (() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        }

        /// <summary>
        /// Record one fix, if it is worth recording (Dart <c>recordFix</c>).
        ///
        /// THE THROTTLE IS AN <b>AND</b>, NOT AN OR: a fix is dropped only when it is BOTH sooner
        /// than <see cref="RecordMinGapMs"/> AND closer than <see cref="RecordMinMoveM"/>. That is
        /// what stops a player tapping submit repeatedly from inflating gps_check_count, while still
        /// keeping a fix from someone who genuinely walked 150 m in three minutes.
        /// </summary>
        public void RecordFix(double lat, double lon)
        {
            try
            {
                List<GpsFix> fixes = _store.Load() ?? new List<GpsFix>();
                long now = _nowMs();

                if (fixes.Count > 0)
                {
                    GpsFix last = fixes[fixes.Count - 1];
                    long gapMs = now - last.T;
                    double moved = HaversineM(last.Lat, last.Lon, lat, lon);
                    if (gapMs < RecordMinGapMs && moved < RecordMinMoveM) return;  // noise
                }

                fixes.Add(new GpsFix(lat, lon, now));
                Prune(fixes, now);
                _store.Save(fixes);
            }
            catch (Exception e)
            {
                // Same posture as the Dart original: a broken fix log must never block a submit.
                Debug.LogWarning("[GpsSessionTracker] RecordFix failed: " + e.Message);
            }
        }

        /// <summary>
        /// The session trace to attach to a submit made at (<paramref name="lat"/>,
        /// <paramref name="lon"/>) — Dart <c>sessionNear</c>.
        ///
        /// Fixes count when they are inside BOTH the 8 h window and the 5 km radius. The count then
        /// walks the sorted list comparing against the LAST COUNTED fix, not the previous one, so a
        /// burst of fixes 1 minute apart contributes 1, not N.
        ///
        /// An empty trace is <c>CheckCount = 1</c> with null coordinates — "this one submit", which
        /// is exactly what the backend's <c>gps_check_count</c> default is (score.py:133).
        /// </summary>
        public GpsSession SessionNear(double lat, double lon)
        {
            try
            {
                List<GpsFix> fixes = _store.Load() ?? new List<GpsFix>();
                long now = _nowMs();

                List<GpsFix> session = fixes
                    .Where(f => (now - f.T) <= SessionWindowMs &&
                                HaversineM(f.Lat, f.Lon, lat, lon) <= SessionRadiusM)
                    .OrderBy(f => f.T)
                    .ToList();

                if (session.Count == 0) return new GpsSession { CheckCount = 1 };

                int count = 1;
                long lastCounted = session[0].T;
                for (int i = 1; i < session.Count; i++)
                {
                    if (session[i].T - lastCounted >= CountMinGapMs)
                    {
                        count++;
                        lastCounted = session[i].T;
                    }
                }

                GpsFix first = session[0];
                GpsFix last = session[session.Count - 1];
                return new GpsSession
                {
                    CheckCount = count,
                    StartLat = first.Lat,
                    StartLon = first.Lon,
                    EndLat = last.Lat,
                    EndLon = last.Lon
                };
            }
            catch (Exception e)
            {
                Debug.LogWarning("[GpsSessionTracker] SessionNear failed: " + e.Message);
                return new GpsSession { CheckCount = 1 };
            }
        }

        /// <summary>Great-circle distance in metres. Same formula and same radius as the Dart
        /// <c>_haversineM</c> AND as the backend's own dedupe check (venue.py:231-236).</summary>
        public static double HaversineM(double lat1, double lon1, double lat2, double lon2)
        {
            const double toRad = Math.PI / 180.0;
            double p1 = lat1 * toRad;
            double p2 = lat2 * toRad;
            double dp = (lat2 - lat1) * toRad;
            double dl = (lon2 - lon1) * toRad;
            double a = Math.Sin(dp / 2) * Math.Sin(dp / 2) +
                       Math.Cos(p1) * Math.Cos(p2) * Math.Sin(dl / 2) * Math.Sin(dl / 2);
            return 2 * EarthRadiusM * Math.Asin(Math.Sqrt(a));
        }

        /// <summary>Dart <c>_prune</c>: drop anything older than the retention window, then drop the
        /// OLDEST until the log fits <see cref="MaxFixes"/>.</summary>
        private static void Prune(List<GpsFix> fixes, long nowMs)
        {
            fixes.RemoveAll(f => nowMs - f.T > RetentionMs);
            while (fixes.Count > MaxFixes) fixes.RemoveAt(0);
        }
    }

    /// <summary>
    /// The session trace attached to a score submit. Field names map 1:1 onto
    /// <c>ScorePostRequest.gps_check_count / gps_start_lat / … </c> (score.py:133-137).
    /// </summary>
    public sealed class GpsSession
    {
        public int CheckCount;
        public double? StartLat;
        public double? StartLon;
        public double? EndLat;
        public double? EndLon;

        public override string ToString()
            => $"GpsSession(count={CheckCount}, start={StartLat},{StartLon}, end={EndLat},{EndLon})";
    }
}
