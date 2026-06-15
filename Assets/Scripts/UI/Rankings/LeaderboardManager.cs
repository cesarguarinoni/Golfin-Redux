#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace Golfin.UI.Rankings
{
    /// <summary>
    /// Singleton holder for the active ILeaderboardProvider.
    /// Caches the current ranking per period for the open session to avoid
    /// regenerating scores on every frame during countdown ticks.
    ///
    /// Cache is invalidated when the active tab changes or the screen is re-opened.
    /// </summary>
    public class LeaderboardManager : MonoBehaviour
    {
        public static LeaderboardManager? Instance { get; private set; }

        // ── Provider ──────────────────────────────────────────────────────────
        private ILeaderboardProvider _provider = new LocalFakeLeaderboardProvider();

        /// <summary>The active leaderboard provider. Swap for backend later.</summary>
        public ILeaderboardProvider Provider
        {
            get => _provider;
            set
            {
                _provider = value;
                _cache.Clear();
            }
        }

        // ── Session cache ─────────────────────────────────────────────────────
        private readonly Dictionary<LeaderboardPeriod, IReadOnlyList<LeaderboardEntry>> _cache
            = new Dictionary<LeaderboardPeriod, IReadOnlyList<LeaderboardEntry>>();

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Kick off the network-time fetch so the offset is ready before the leaderboard opens.
            NetworkTimeProvider.Instance.FetchAsync();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>Returns the full ranked list, using the session cache when available.</summary>
        public IReadOnlyList<LeaderboardEntry> GetRanking(LeaderboardPeriod period)
        {
            if (!_cache.TryGetValue(period, out var cached))
            {
                cached = _provider.GetRanking(period);
                _cache[period] = cached;
            }
            return cached;
        }

        /// <summary>Returns the player's entry for the period.</summary>
        public LeaderboardEntry GetPlayerEntry(LeaderboardPeriod period)
        {
            return _provider.GetPlayerEntry(period);
        }

        /// <summary>Invalidate the cache for a specific period (e.g. after RP earned).</summary>
        public void InvalidateCache(LeaderboardPeriod period)
        {
            _cache.Remove(period);
        }

        /// <summary>Invalidate all cached periods (e.g. on screen open).</summary>
        public void InvalidateAllCache()
        {
            _cache.Clear();
        }
    }
}
