// Order: gps_trust_core §7 — port of gps_score_attachment.dart (capture + toJson). The one orchestrator.
using System;
using System.Collections;
using Golfin.Net;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Golfin.Gps
{
    /// <summary>
    /// Everything a score submit says about WHERE it was made, assembled in one pass.
    ///
    /// EVERY STEP DEGRADES; NONE ABORTS. The Dart original's rule — 投稿自体は止めない, "never stop
    /// the post itself" — is the whole design: a player standing on the 18th green with one bar of
    /// signal must still be able to submit, just with less Trust. So a failed fetch leaves
    /// <see cref="Position"/> null and records why in <see cref="PositionFailReason"/>; a failed
    /// auto-register leaves <see cref="VenueId"/> null; and <see cref="ToJson"/> simply omits what it
    /// does not have.
    ///
    /// The backend re-derives <c>gps_verified</c> itself (<c>_verify_gps</c>) and zeroes it on mock,
    /// so nothing here is trusted — this is a REQUEST to verify, not a claim of verification.
    /// </summary>
    public sealed class GpsScoreAttachment
    {
        /// <summary>Null when the fetch failed — see <see cref="PositionFailReason"/>.</summary>
        public LocationFix Position;

        public LocationFailReason PositionFailReason = LocationFailReason.None;

        public int? VenueId;
        public string VenueName;
        public double? VenueDistanceM;

        /// <summary>Never null.</summary>
        public GpsTrustSignals Signals = new GpsTrustSignals();

        /// <summary>Null exactly when <see cref="Position"/> is null (no fix ⇒ no trace to attach).</summary>
        public GpsSession Session;

        /// <summary>The Dart attachment's <c>timeout</c> default for THIS path — 5 s, not the
        /// notifier's 10 s. A submit is a foreground action the player is waiting on.</summary>
        public const float DefaultTimeoutSeconds = 5f;

        /// <summary>
        /// fetch → signals → RecordFix + SessionNear → <c>/venue/auto-register</c> → attachment.
        /// Invokes <paramref name="onDone"/> exactly once, always with a non-null attachment.
        /// </summary>
        public static IEnumerator Capture(ILocationProvider location,
                                          GpsSessionTracker tracker,
                                          GpsTrustSignals signals,
                                          VenueService venues,
                                          Action<GpsScoreAttachment> onDone,
                                          float timeoutSeconds = DefaultTimeoutSeconds)
        {
            var attachment = new GpsScoreAttachment
            {
                Signals = signals ?? new GpsTrustSignals()
            };

            // 1. Position. A missing provider is a wiring bug, not a player condition — still degrades.
            if (location == null)
            {
                attachment.PositionFailReason = LocationFailReason.Unknown;
            }
            else
            {
                LocationResult located = null;
                IEnumerator fetch = location.Fetch(timeoutSeconds, r => located = r);
                while (fetch.MoveNext()) yield return fetch.Current;

                if (located != null && located.Ok) attachment.Position = located.Fix;
                else attachment.PositionFailReason = located != null ? located.Reason : LocationFailReason.Unknown;
            }

            // 2. Session trace (K4). Record THIS fix first, then read the trace back including it —
            //    the Dart order, and the reason a first-ever submit reports check_count 1 rather than 0.
            if (attachment.Position != null && tracker != null)
            {
                tracker.RecordFix(attachment.Position.Lat, attachment.Position.Lon);
                attachment.Session = tracker.SessionNear(attachment.Position.Lat, attachment.Position.Lon);
            }

            // 3. Venue. No coordinates ⇒ no call at all: the router would have nothing to search on.
            if (attachment.Position != null && venues != null)
            {
                ApiResult<VenueAutoRegisterResult> result = null;
                IEnumerator call = venues.AutoRegister(attachment.Position.Lat, attachment.Position.Lon,
                                                       r => result = r);
                while (call.MoveNext()) yield return call.Current;

                if (result != null && result.Success && result.Data != null && result.Data.VenueId.HasValue)
                {
                    attachment.VenueId = result.Data.VenueId;
                    attachment.VenueName = result.Data.Name;
                    attachment.VenueDistanceM = result.Data.DistanceM;
                }
                else if (result != null && !result.Success)
                {
                    Debug.LogWarning("[GpsScoreAttachment] venue auto-register failed: " + result.ErrorMessage);
                }
            }

            onDone?.Invoke(attachment);
        }

        /// <summary>Convenience over the shipping singletons and defaults.</summary>
        public static IEnumerator Capture(Action<GpsScoreAttachment> onDone)
            => Capture(new UnityLocationProvider(),
                       GpsSessionTracker.Instance,
                       GpsTrustSignals.CaptureDefault(),
                       VenueService.Instance,
                       onDone);

        /// <summary>
        /// The fields to MERGE into the <c>/score/submit</c> body. Key names are
        /// <c>ScorePostRequest</c>'s (score.py:117-140) and the key SET is the Dart
        /// <c>toJson()</c>'s, exactly.
        ///
        /// ABSENT, NEVER NULL-VALUED: the Dart map used <c>if (…)</c> guards, and FastAPI's
        /// <c>Optional[float] = None</c> defaults mean an omitted key and an explicit null land in
        /// the same place — but an omitted key also survives a future <c>None</c>-rejecting validator.
        /// </summary>
        public JObject ToJson()
        {
            var o = new JObject();

            o["gps_verified"] = Position != null && VenueId.HasValue;

            if (Position != null)
            {
                o["latitude"] = Position.Lat;
                o["longitude"] = Position.Lon;
            }

            if (VenueId.HasValue) o["venue_id"] = VenueId.Value;

            GpsTrustSignals s = Signals ?? new GpsTrustSignals();
            o["gps_is_mock"] = s.IsMock;
            o["client_platform"] = s.ClientPlatform;

            if (Session != null)
            {
                o["gps_check_count"] = Session.CheckCount;
                if (Session.StartLat.HasValue) o["gps_start_lat"] = Session.StartLat.Value;
                if (Session.StartLon.HasValue) o["gps_start_lon"] = Session.StartLon.Value;
                if (Session.EndLat.HasValue)   o["gps_end_lat"]   = Session.EndLat.Value;
                if (Session.EndLon.HasValue)   o["gps_end_lon"]   = Session.EndLon.Value;
            }

            return o;
        }

        public override string ToString() => ToJson().ToString(Newtonsoft.Json.Formatting.None);
    }
}
