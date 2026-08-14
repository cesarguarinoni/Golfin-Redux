// Order: reward_points_backend Slice 1 — PLAYLIFE API URLs. Scope-limited to /points/* + /health.
namespace Golfin.Net
{
    /// <summary>
    /// Every PLAYLIFE API URL the game knows about. Deliberately tiny: SPEC §4 Slice 1 scopes this to
    /// the points ledger plus the liveness probe. Anything else gets added by the slice that needs it.
    ///
    /// PATH NOTE (verified against the live deployment 2026-08-12, NOT assumed):
    /// <c>/health</c> hangs off the ROOT host, not off the <c>/api/v1</c> prefix —
    /// <c>GET https://playlife-api.fly.dev/health</c> → 200 <c>{"status":"ok","version":"0.1.0"}</c>
    /// while <c>GET https://playlife-api.fly.dev/api/v1/health</c> → 404. Hence <see cref="RootUrl"/>
    /// alongside <see cref="BaseUrl"/>.
    ///
    /// Health is also the one endpoint whose body is NOT wrapped in the <c>{data:…}</c> envelope; the
    /// unwrapper handles that by passing a non-enveloped object through untouched.
    /// </summary>
    public static class Endpoints
    {
        public const string DefaultRootUrl = "https://playlife-api.fly.dev";
        public const string ApiPrefix = "/api/v1";

        /// <summary>Host root. Settable so tests / a staging build can retarget without a code change.</summary>
        public static string RootUrl { get; set; } = DefaultRootUrl;

        /// <summary>Versioned API base — <c>https://playlife-api.fly.dev/api/v1</c>.</summary>
        public static string BaseUrl => Trim(RootUrl) + ApiPrefix;

        /// <summary>Liveness probe (root-mounted, un-enveloped, no auth).</summary>
        public static string Health => Trim(RootUrl) + "/health";

        /// <summary>GET → <c>{data:{activity_pts, gift_pts, total_points, avatar_level, avatar_xp}}</c>.
        /// <c>total_points</c> IS the game's Reward Points balance (SPEC decision of record #4).</summary>
        public static string PointsBalance => BaseUrl + "/points/balance";

        /// <summary>POST <c>{action, amount?, idempotency_key}</c> — the game earn path (Slice 2 writes to it).</summary>
        public static string PointsEarnGame => BaseUrl + "/points/earn-game";

        /// <summary>POST <c>{amount, reason, idempotency_key}</c> — the game spend path (Slice 2; unused here).</summary>
        public static string PointsSpend => BaseUrl + "/points/spend";

        /// <summary>
        /// GET → <c>{data:{fetched_at, tournaments:[…]}}</c> — the GOLFIN tournament schedule with each
        /// tournament's prize bands joined into the same payload (one round trip).
        ///
        /// NO AUTH, deliberately: same posture as the GPS <c>/tournaments/active</c>, and the schedule
        /// should be able to warm at boot before any token work has happened.
        /// Server side returns <c>kind='golfin'</c> rows only, unfiltered by status — the client derives
        /// state from start/end, and an Ended tournament still has a LEADERBOARD card to render.
        /// </summary>
        public static string TournamentsGolfin => BaseUrl + "/tournaments/golfin";

        /// <summary>GET ledger page. <paramref name="currency"/> is "activity" / "gift" or null for both.</summary>
        public static string PointsHistory(int skip = 0, int limit = 20, string currency = null)
        {
            string url = BaseUrl + "/points/history?skip=" + skip + "&limit=" + limit;
            if (!string.IsNullOrEmpty(currency)) url += "&currency=" + currency;
            return url;
        }

        /// <summary>Restore the shipping host (used by tests that retarget <see cref="RootUrl"/>).</summary>
        public static void ResetToDefault() => RootUrl = DefaultRootUrl;

        private static string Trim(string s) => (s ?? "").TrimEnd('/');
    }
}
