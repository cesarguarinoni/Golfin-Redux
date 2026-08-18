#nullable enable
using System.Collections.Generic;
using Golfin.Auth;
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

        /// <summary>Which kind <see cref="_provider"/> currently is, so re-selection is a no-op when
        /// nothing changed (rebuilding the backend provider would drop its snapshots).</summary>
        private LeaderboardProviderKind _providerKind = LeaderboardProviderKind.LocalFake;

        /// <summary>The active leaderboard provider (<see cref="LeaderboardProviderPolicy"/> picks it).</summary>
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

            EnsureProviderForSession();

            // This GameObject lives in ShellScene, so Awake runs at BOOT — before the auth gate has
            // been passed on a first launch, when the only honest answer is "signed out". Without this
            // subscription a player who signs in during the session would be pinned to the local fakes
            // for the rest of it. A returning player is already authenticated at Awake and never fires
            // the event, which is why the Awake call above is not redundant.
            AuthService.SignedIn += OnSignedIn;
        }

        private void OnDestroy()
        {
            AuthService.SignedIn -= OnSignedIn;

            if (Instance == this)
                Instance = null;
        }

        private void OnSignedIn(AuthSession session) => EnsureProviderForSession();

        // ── Provider selection (SPEC §4) ──────────────────────────────────────

        /// <summary>
        /// Point <see cref="Provider"/> at whatever this session should read, and do nothing at all
        /// when that has not changed. Safe to call repeatedly — <c>RankingsScreenController.OnEnable</c>
        /// does, so a session restored without a <c>SignedIn</c> event still lands on the backend.
        /// </summary>
        public void EnsureProviderForSession()
        {
            bool botOverride = false;
#if UNITY_EDITOR || GOLFIN_BOT_HARNESS
            // Whole-file-guarded type: the caller must repeat the guard (see BotSessionOverride's header
            // and the iOS lesson about #if UNITY_EDITOR seams in runtime assemblies).
            botOverride = Golfin.Dev.BotSessionOverride.Active;
#endif
            // AuthService.Instance lazily creates a DontDestroyOnLoad singleton, which throws outside
            // play mode — and this MonoBehaviour is reachable from editor tooling.
            bool signedIn = Application.isPlaying
                            && AuthService.Instance != null
                            && AuthService.Instance.Session != null
                            && AuthService.Instance.Session.IsAuthenticated;

            LeaderboardProviderKind kind = LeaderboardProviderPolicy.Choose(botOverride, signedIn);
            if (kind == _providerKind && _provider != null) return;

            _providerKind = kind;
            Provider = kind == LeaderboardProviderKind.Backend
                ? (ILeaderboardProvider)new BackendLeaderboardProvider()
                : new LocalFakeLeaderboardProvider();

            Debug.Log($"[Leaderboard] Provider = {kind} (bot override: {botOverride}, signed in: {signedIn}).");
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
