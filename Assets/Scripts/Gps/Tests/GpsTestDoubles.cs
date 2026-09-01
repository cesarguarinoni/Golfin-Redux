// Order: gps_trust_core §Tests — doubles for the location / clock / mock / platform seams.
// The HTTP + auth doubles are REUSED from Golfin.Net.Tests (FakeHttpTransport, FakeAuthTokenProvider,
// ImmediateCoroutineRunner, Pump) rather than copied — one transport double, one place to fix it.
using System;
using System.Collections;
using System.Collections.Generic;
using Golfin.Net;
using Golfin.Net.Tests;

namespace Golfin.Gps.Tests
{
    /// <summary>Scripted <see cref="ILocationProvider"/>. Records the timeout it was asked for, which
    /// is how §Tests #14 proves the attachment path uses 5 s and not the notifier's 10 s.</summary>
    public sealed class FakeLocationProvider : ILocationProvider
    {
        private readonly Queue<LocationResult> _results = new Queue<LocationResult>();

        /// <summary>Used once the queue runs dry.</summary>
        public LocationResult Fallback = LocationResult.Failure(LocationFailReason.Unknown);

        public readonly List<float> RequestedTimeouts = new List<float>();

        public int CallCount => RequestedTimeouts.Count;

        public float LastRequestedTimeout
            => RequestedTimeouts.Count > 0 ? RequestedTimeouts[RequestedTimeouts.Count - 1] : float.NaN;

        public FakeLocationProvider Enqueue(params LocationResult[] results)
        {
            foreach (var r in results) _results.Enqueue(r);
            return this;
        }

        public FakeLocationProvider EnqueueFix(double lat, double lon, float accuracyM = 8f, long timestampMs = 0)
            => Enqueue(LocationResult.Success(new LocationFix
            {
                Lat = lat, Lon = lon, AccuracyM = accuracyM, TimestampMs = timestampMs
            }));

        public FakeLocationProvider EnqueueFailure(LocationFailReason reason)
            => Enqueue(LocationResult.Failure(reason));

        public IEnumerator Fetch(float timeoutSeconds, Action<LocationResult> onResult)
        {
            RequestedTimeouts.Add(timeoutSeconds);
            onResult?.Invoke(_results.Count > 0 ? _results.Dequeue() : Fallback);
            yield break;
        }
    }

    /// <summary>Settable clock. <see cref="Now"/> is the <c>Func&lt;long&gt;</c> a tracker takes.</summary>
    public sealed class FakeClock
    {
        public long NowMs;

        public FakeClock(long nowMs = 0) { NowMs = nowMs; }

        public Func<long> Now => () => NowMs;

        public FakeClock AdvanceMinutes(double minutes)
        {
            NowMs += (long)(minutes * 60_000);
            return this;
        }

        public FakeClock AdvanceHours(double hours) => AdvanceMinutes(hours * 60);
    }

    public sealed class FakeMockDetector : IMockLocationDetector
    {
        public bool Value;
        public bool Throws;

        public FakeMockDetector(bool value = false) { Value = value; }

        public bool IsMock()
        {
            if (Throws) throw new InvalidOperationException("probe exploded");
            return Value;
        }
    }

    public sealed class FakePlatformProbe : IClientPlatformProbe
    {
        public string Value;
        public bool Throws;

        public FakePlatformProbe(string value = "editor") { Value = value; }

        public string Label()
        {
            if (Throws) throw new InvalidOperationException("probe exploded");
            return Value;
        }
    }

    /// <summary>Shared wiring for the HTTP-backed GPS tests.</summary>
    public static class GpsTestApi
    {
        /// <summary>An <see cref="ApiClient"/> over a scripted transport, with the retry pause set to
        /// zero so a manual <c>while (MoveNext())</c> pump does not spin on wall-clock.</summary>
        public static ApiClient Client(FakeHttpTransport transport, FakeAuthTokenProvider auth = null)
            => new ApiClient(transport, auth ?? new FakeAuthTokenProvider(), new ImmediateCoroutineRunner())
            {
                RetryDelaySeconds = 0f,
                LogRequests = false
            };

        /// <summary>A tracker on an empty in-memory log and a controllable clock.</summary>
        public static GpsSessionTracker Tracker(FakeClock clock, InMemoryGpsFixStore store = null)
            => new GpsSessionTracker(store ?? new InMemoryGpsFixStore(), clock.Now);

        public static GpsTrustSignals Signals(bool isMock = false, string platform = "editor")
            => new GpsTrustSignals { IsMock = isMock, ClientPlatform = platform };
    }
}
