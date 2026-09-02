// gps_profile_pack §5.1 — caller's aggregate score stats over ApiClient.
using System;
using System.Collections;
using Golfin.Net;

namespace Golfin.Gps
{
    /// <summary>
    /// Caching singleton for GET /score/stats. Same shape as <see cref="ScoreHistoryService"/>:
    /// plain C#, constructible in EditMode tests, no MonoBehaviour.
    /// </summary>
    public sealed class ScoreStatsService
    {
        private static ScoreStatsService _instance;

        public static ScoreStatsService Instance =>
            _instance ?? (_instance = new ScoreStatsService(ApiClient.Instance));

        public static void ConfigureForTest(ScoreStatsService service) => _instance = service;
        public static void ResetForTest() => _instance = null;

        private readonly ApiClient _client;

        public ScoreStatsDto LastStats    { get; private set; }
        public bool          HasData      { get; private set; }

        public event Action OnStatsChanged;

        public ScoreStatsService(ApiClient client)
        {
            _client = client;
        }

        /// <summary>
        /// GET /score/stats — caller's aggregate stats. AUTH REQUIRED.
        /// Caches the result in <see cref="LastStats"/> and fires <see cref="OnStatsChanged"/>.
        /// An empty result (no rounds played) is valid — <see cref="ScoreStatsDto.RoundsPlayed"/>
        /// will be 0 and callers should show "—" placeholders.
        /// </summary>
        public IEnumerator FetchStats(Action<ApiResult<ScoreStatsDto>> onResult = null)
            => _client.Get(Endpoints.ScoreStats, (ApiResult<ScoreStatsDto> r) =>
            {
                if (r.Success)
                {
                    LastStats = r.Data;
                    HasData   = true;
                    OnStatsChanged?.Invoke();
                }
                onResult?.Invoke(r);
            });
    }
}
