// Order: gps_hub_entry §3 — the caller's own posted scores over the EXISTING ApiClient.
using System;
using System.Collections;
using System.Collections.Generic;
using Golfin.Net;

namespace Golfin.Gps
{
    /// <summary>
    /// The score-history half of the GPS module — the same shape as <see cref="VenueService"/>,
    /// deliberately, so there is one service pattern in this assembly rather than two.
    ///
    /// <para>
    /// Plain C# singleton: constructible in an EditMode test, no MonoBehaviour, no offline queue
    /// (a history READ is worthless replayed later). Bearer auth, the <c>{data:…}</c> unwrap,
    /// transient retries and the single 401 replay all come from <see cref="ApiClient"/>.
    /// </para>
    /// <para>
    /// NOT the same thing as <c>Endpoints.ActivityHistory</c>: that is the CHECK-IN ledger, this is
    /// the SCORE history (score.py:419-436), ordered by <c>check_in_at</c> descending.
    /// </para>
    /// </summary>
    public sealed class ScoreHistoryService
    {
        private static ScoreHistoryService _instance;

        public static ScoreHistoryService Instance =>
            _instance ?? (_instance = new ScoreHistoryService(ApiClient.Instance));

        public static void ConfigureForTest(ScoreHistoryService service) => _instance = service;

        public static void ResetForTest() => _instance = null;

        private readonly ApiClient _client;

        public ScoreHistoryService(ApiClient client)
        {
            _client = client;
        }

        /// <summary>
        /// <c>GET /score/history?skip=&amp;limit=</c> → the caller's posted scores, newest first.
        ///
        /// <para>
        /// AUTH REQUIRED; the rows are chosen by the bearer token, so there is no user id to pass.
        /// An EMPTY LIST IS A HEALTHY ANSWER (a player who has never posted a score through the
        /// PLAYLIFE app), not an error — callers must branch on <c>Count</c>, not on
        /// <c>Success</c>.
        /// </para>
        /// </summary>
        public IEnumerator History(int skip, int limit, Action<ApiResult<List<ActivityDto>>> onResult)
            => _client.Get(Endpoints.ScoreHistory(skip, limit), onResult);
    }
}
