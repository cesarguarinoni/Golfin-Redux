// gps_profile_pack §5.3 — badge progress over ApiClient.
using System;
using System.Collections;
using System.Collections.Generic;
using Golfin.Net;

namespace Golfin.Gps
{
    /// <summary>
    /// Caching singleton for GET /badges/progress. Same shape as <see cref="ScoreStatsService"/>.
    /// </summary>
    public sealed class BadgeService
    {
        private static BadgeService _instance;

        public static BadgeService Instance =>
            _instance ?? (_instance = new BadgeService(ApiClient.Instance));

        public static void ConfigureForTest(BadgeService service) => _instance = service;
        public static void ResetForTest() => _instance = null;

        private readonly ApiClient _client;

        public List<BadgeProgressDto> LastBadges  { get; private set; }
        public bool                   HasData      { get; private set; }

        public event Action OnBadgesChanged;

        public BadgeService(ApiClient client)
        {
            _client = client;
        }

        /// <summary>
        /// GET /badges/progress — all badge definitions + caller's progress/earned state.
        /// AUTH REQUIRED. An empty list means no badges are configured server-side (not an error).
        /// Caches in <see cref="LastBadges"/> and fires <see cref="OnBadgesChanged"/>.
        /// </summary>
        public IEnumerator FetchBadges(Action<ApiResult<List<BadgeProgressDto>>> onResult = null)
            => _client.Get(Endpoints.BadgesProgress, (ApiResult<List<BadgeProgressDto>> r) =>
            {
                if (r.Success)
                {
                    LastBadges = r.Data;
                    HasData    = true;
                    OnBadgesChanged?.Invoke();
                }
                onResult?.Invoke(r);
            });
    }
}
