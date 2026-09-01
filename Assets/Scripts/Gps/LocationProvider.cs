// Order: gps_trust_core §4 — the ONE native seam. Port of current_location_notifier.dart's
// fetchWithStatus + LocationFailReason, on Unity's LocationService instead of Geolocator.
using System;
using System.Collections;
using UnityEngine;

namespace Golfin.Gps
{
    /// <summary>
    /// Why a location fetch produced nothing. Transcribed from
    /// <c>current_location_notifier.dart::LocationFailReason</c>, plus <see cref="None"/> for the
    /// success case (Dart used a nullable enum).
    /// </summary>
    public enum LocationFailReason
    {
        None = 0,
        ServiceDisabled,
        PermissionDenied,
        PermissionDeniedForever,
        Timeout,
        Unknown
    }

    /// <summary>One successful fix. <see cref="TimestampMs"/> is Unix epoch milliseconds, matching
    /// <see cref="GpsFix.T"/>.</summary>
    public sealed class LocationFix
    {
        public double Lat;
        public double Lon;
        public float AccuracyM;
        public long TimestampMs;

        public override string ToString() => $"LocationFix({Lat:F6}, {Lon:F6} ±{AccuracyM:F0}m)";
    }

    /// <summary>Success carries a <see cref="Fix"/>; failure carries a <see cref="Reason"/>. Never both.</summary>
    public sealed class LocationResult
    {
        public LocationFix Fix;
        public LocationFailReason Reason;

        public bool Ok => Fix != null;

        public static LocationResult Success(LocationFix fix)
            => new LocationResult { Fix = fix, Reason = LocationFailReason.None };

        public static LocationResult Failure(LocationFailReason reason)
            => new LocationResult { Fix = null, Reason = reason };
    }

    /// <summary>
    /// The location seam. Coroutine-shaped rather than Task-shaped, matching the rest of the
    /// PLAYLIFE client layer (<c>ApiClient</c>, <c>ISupabaseAuthClient</c>).
    /// </summary>
    public interface ILocationProvider
    {
        /// <summary>Invokes <paramref name="onResult"/> EXACTLY once. Never throws — every failure
        /// arrives as a <see cref="LocationFailReason"/>.</summary>
        IEnumerator Fetch(float timeoutSeconds, Action<LocationResult> onResult);
    }

    /// <summary>
    /// The real device implementation.
    ///
    /// KNOWN LIMITATION, deliberate (spec §4): Unity's <c>Input.location.isEnabledByUser</c> collapses
    /// "location services are off" and "this app was denied" into one false, so this class reports
    /// <see cref="LocationFailReason.ServiceDisabled"/> for both. Geolocator distinguished them with
    /// <c>checkPermission()</c>; recovering that needs a native permission probe, which the check-in
    /// screen spec will decide on. The two only differ in the advice shown to the player.
    /// </summary>
    public sealed class UnityLocationProvider : ILocationProvider
    {
        /// <summary>The notifier's fetch budget (<c>current_location_notifier.dart</c> default).
        /// NOTE the score-attachment path uses 5 s — see <see cref="GpsScoreAttachment.Capture"/>.</summary>
        public const float DefaultTimeoutSeconds = 10f;

        /// <summary>LocationAccuracy.high equivalent.</summary>
        public const float DesiredAccuracyM = 10f;

        public const float UpdateDistanceM = 5f;

        public IEnumerator Fetch(float timeoutSeconds, Action<LocationResult> onResult)
        {
            if (Application.isEditor)
            {
                // Input.location never runs in the Editor; tests use FakeLocationProvider.
                Debug.LogWarning("[UnityLocationProvider] Location services do not run in the Editor — reporting Unknown.");
                onResult?.Invoke(LocationResult.Failure(LocationFailReason.Unknown));
                yield break;
            }

            if (!Input.location.isEnabledByUser)
            {
                onResult?.Invoke(LocationResult.Failure(LocationFailReason.ServiceDisabled));
                yield break;
            }

            Input.location.Start(DesiredAccuracyM, UpdateDistanceM);

            float deadline = Time.realtimeSinceStartup + Mathf.Max(0f, timeoutSeconds);
            while (Input.location.status == LocationServiceStatus.Initializing &&
                   Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            LocationServiceStatus status = Input.location.status;

            if (status == LocationServiceStatus.Running)
            {
                LocationInfo d = Input.location.lastData;
                var fix = new LocationFix
                {
                    Lat = d.latitude,
                    Lon = d.longitude,
                    AccuracyM = d.horizontalAccuracy,
                    TimestampMs = (long)(d.timestamp * 1000.0)
                };
                Input.location.Stop();               // battery — every exit path stops the service
                onResult?.Invoke(LocationResult.Success(fix));
                yield break;
            }

            Input.location.Stop();
            onResult?.Invoke(LocationResult.Failure(
                status == LocationServiceStatus.Failed ? LocationFailReason.Unknown : LocationFailReason.Timeout));
        }
    }

    /// <summary>
    /// Localization keys for the failure copy.
    ///
    /// Defined HERE so the check-in screen binds to fixed names, but the CSV rows are deliberately
    /// NOT added by this task: an unused key fails the content exporter's orphan check for no
    /// benefit. The JA copy to carry over lives in <c>current_location_notifier.dart:150-158</c>.
    /// </summary>
    public static class LocationFailReasonKeys
    {
        public const string ServiceDisabled = "GPS_ERR_SERVICE_DISABLED";
        public const string PermissionDenied = "GPS_ERR_PERMISSION_DENIED";
        public const string PermissionDeniedForever = "GPS_ERR_PERMISSION_DENIED_FOREVER";
        public const string Timeout = "GPS_ERR_TIMEOUT";
        public const string Unknown = "GPS_ERR_UNKNOWN";

        public static string For(LocationFailReason r)
        {
            switch (r)
            {
                case LocationFailReason.ServiceDisabled:         return ServiceDisabled;
                case LocationFailReason.PermissionDenied:        return PermissionDenied;
                case LocationFailReason.PermissionDeniedForever: return PermissionDeniedForever;
                case LocationFailReason.Timeout:                 return Timeout;
                default:                                         return Unknown;
            }
        }
    }
}
