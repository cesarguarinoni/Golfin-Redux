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

        /// <summary>
        /// GET → <c>{data:{fetched_at, banners:[…]}}</c> — the admin-controlled banner image for
        /// each in-game slot, at most one per placement.
        ///
        /// NO AUTH, same posture and same reason as <see cref="TournamentsGolfin"/>: it warms at
        /// boot before any token work. Server side does the whole is_active + schedule-window
        /// selection, so the client only has to honour the returned <c>expires_at</c>.
        ///
        /// No trailing slash — the bare form is the 200, and <c>RemoteBannerSource</c> must not
        /// depend on redirect following.
        /// </summary>
        public static string Banners => BaseUrl + "/banners";

        /// <summary>GET → <c>{data:{fetched_at, notices:[…]}}</c> — the Home notice panel's copy.
        /// No auth, same posture as <see cref="Banners"/>. No trailing slash.</summary>
        public static string Notices => BaseUrl + "/notices";

        /// <summary>
        /// POST <c>{session_id, app_version, build_number, platform, device_model, os, events:[…]}</c>
        /// — the beta telemetry sink (beta_telemetry SPEC §2.1).
        ///
        /// AUTH REQUIRED, unlike <see cref="Banners"/> / <see cref="TournamentsGolfin"/>: the server
        /// stamps <c>user_id</c> from the bearer token and never trusts one in the body, so a tester
        /// can only ever write rows attributed to themselves. The token rides ApiClient automatically.
        /// </summary>
        public static string TelemetryEvents => BaseUrl + "/telemetry/events";

        /// <summary>
        /// PUT <c>{character_id, level}</c> — the leaderboard portrait sync (leaderboard_backend SPEC §1).
        ///
        /// AUTH REQUIRED, same posture as <see cref="TelemetryEvents"/>: the server stamps the row from
        /// the bearer token, so a client can only ever write its own character. 400 on an empty or
        /// oversized <c>character_id</c>; <c>level</c> is clamped server-side to 1–999.
        /// </summary>
        public static string UserGolfinCharacter => BaseUrl + "/user/golfin-character";

        /// <summary>
        /// GET → <c>{data:{fetched_at, period, period_end_utc, entries:[…], player:{…}}}</c> — the ranked
        /// board for one period plus the caller's own row (leaderboard_backend SPEC §1).
        ///
        /// AUTH REQUIRED, unlike <see cref="Banners"/>: the server identifies the caller from the token
        /// and ALWAYS returns their row, even at score 0 outside the top slice. 404 for an unknown period.
        /// <paramref name="period"/> is one of <c>daily|weekly|monthly|historic</c>.
        ///
        /// Ranks and <c>is_tie</c> are computed server-side with standard competition ranking (1,2,2,4);
        /// the client renders them verbatim and never re-ranks.
        /// </summary>
        public static string Leaderboard(string period) => BaseUrl + "/leaderboards/" + period;

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
