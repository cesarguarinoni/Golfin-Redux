#nullable enable
using System;
using System.Net.Http;
using System.Threading.Tasks;
using UnityEngine;

namespace Golfin.UI.Rankings
{
    /// <summary>
    /// Fetches authoritative UTC time from an HTTP Date header (HEAD request to a reliable host).
    /// Computes an offset vs device clock and serves UtcNow = deviceUtc + offset.
    ///
    /// Offline fallback: if the fetch fails, IsAuthoritative = false and UtcNow returns device DateTime.UtcNow.
    ///
    /// The fetch is async, non-blocking, and runs at most once per session (result is cached).
    /// Chosen host: https://www.google.com (stable, HTTPS, returns a Date header, widely accessible).
    /// </summary>
    public class NetworkTimeProvider : ITimeProvider
    {
        // ── Chosen host (flagged in IMPLEMENTER_REPORT) ───────────────────────
        // www.google.com was chosen because it:
        //  - is reachable globally from mobile devices
        //  - always returns an HTTP Date header
        //  - uses HTTPS (avoids MITM time manipulation)
        //  - responds to HEAD requests quickly (typically <300ms)
        private const string TimeHost = "https://www.google.com";
        private const int TimeoutSeconds = 5;

        // ── Singleton ─────────────────────────────────────────────────────────
        public static NetworkTimeProvider Instance { get; } = new NetworkTimeProvider();

        // ── State ─────────────────────────────────────────────────────────────
        private TimeSpan _deviceToNetworkOffset = TimeSpan.Zero;
        private bool _fetchCompleted;

        // ── ITimeProvider ─────────────────────────────────────────────────────
        public bool IsAuthoritative { get; private set; }

        public DateTime UtcNow => DateTime.UtcNow + _deviceToNetworkOffset;

        // ── API ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Kick off the async fetch. Safe to call multiple times (no-ops after first call).
        /// </summary>
        public void FetchAsync()
        {
            if (_fetchCompleted) return;
            _ = FetchNetworkTimeAsync();
        }

        // ── Internals ─────────────────────────────────────────────────────────

        private async Task FetchNetworkTimeAsync()
        {
            try
            {
                DateTime deviceUtcBefore = DateTime.UtcNow;

                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(TimeoutSeconds) };
                using var request = new HttpRequestMessage(HttpMethod.Head, TimeHost);
                using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead)
                                                 .ConfigureAwait(false);

                DateTime deviceUtcAfter = DateTime.UtcNow;
                DateTime deviceUtcMid = deviceUtcBefore + (deviceUtcAfter - deviceUtcBefore) / 2;

                // Parse the Date header
                if (response.Headers.Date.HasValue)
                {
                    DateTime networkUtc = response.Headers.Date.Value.UtcDateTime;
                    _deviceToNetworkOffset = networkUtc - deviceUtcMid;
                    IsAuthoritative = true;
                    Debug.Log($"[NetworkTimeProvider] Fetched network time from {TimeHost}. " +
                              $"Offset vs device: {_deviceToNetworkOffset.TotalSeconds:F1}s. " +
                              $"NetworkUtcNow: {UtcNow:u}");
                }
                else
                {
                    Debug.LogWarning($"[NetworkTimeProvider] {TimeHost} returned no Date header — falling back to device clock.");
                    IsAuthoritative = false;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[NetworkTimeProvider] Network time fetch failed ({ex.GetType().Name}: {ex.Message}). " +
                                 "Falling back to device UTC clock.");
                IsAuthoritative = false;
            }
            finally
            {
                _fetchCompleted = true;
            }
        }
    }
}
