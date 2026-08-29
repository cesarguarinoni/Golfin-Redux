// Order: reward_points_backend Slice 1 — PLAYLIFE API URLs. Scope-limited to /points/* + /health.
using UnityEngine.Networking;

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

        // ── Missions (missions_v1 §A4) ────────────────────────────────────────
        //
        // AUTH REQUIRED on all four, and the amount is NEVER in the request: the client says
        // which mission it cleared and in how many strokes, and the server reads what that is
        // worth out of `golfin_mission_rewards`. That is the whole reason these are their own
        // endpoints rather than another `/points/earn-game` action.

        /// <summary>GET → <c>{data:{missions:[…], cleared_by_tier, tier_sizes}}</c> — this
        /// player's campaign progress and which tiers are open to them.</summary>
        public static string MissionsCatalogState => BaseUrl + "/missions/catalog-state";

        /// <summary>POST <c>{mission_id, strokes, goals_met, idempotency_key}</c> → the server
        /// prices and pays the clear. Every business outcome is a 200 payload.</summary>
        public static string MissionsClaim => BaseUrl + "/missions/claim";

        /// <summary>GET → <c>{data:{date, recipe, recipe_hash, claimed, streak}}</c> — today's
        /// daily, generated on first read and then frozen for the day.</summary>
        public static string MissionsDaily => BaseUrl + "/missions/daily";

        /// <summary>POST <c>{date, recipe_hash, strokes, idempotency_key}</c>. The hash guard is
        /// what stops a client that cached yesterday's recipe being paid for it today.</summary>
        public static string MissionsDailyClaim => BaseUrl + "/missions/daily/claim";

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
        /// GET → <c>{data:{fetched_at, enabled, latest_version, catalogs:{…}}}</c> — the
        /// admin-managed content delta. No auth, same posture and same reason as
        /// <see cref="Banners"/>: it warms at boot before any token work. No trailing slash — the
        /// bare form is the 200, and the caller must not depend on redirect following.
        /// <paramref name="build"/> is the running build number, and the server withholds any row
        /// whose <c>min_build</c> exceeds it, so an old build is never sent content it cannot render.
        ///
        /// <paramref name="since"/> IS PER-CATALOG: <c>"clubs:1,texts:9,characters:5"</c>. Each
        /// catalog's cursor comes from its own line in
        /// <c>Assets/Resources/Data/content_version.txt</c> (which the exporter already writes one
        /// <c>&lt;catalog&gt;=&lt;version&gt;</c> line at a time), or from that catalog's own
        /// <c>version</c> in a previous response — whichever is higher. A catalog left out of the
        /// string has no cursor and comes back in full. A bare integer is still accepted and
        /// applies to every catalog, for runbook curls and staging builds.
        ///
        /// THERE IS DELIBERATELY NO SINGLE TOP-LEVEL VERSION TO STORE. Catalogs version
        /// independently, so no scalar can describe the client's state: replaying the max silently
        /// drops a catalog that published behind the newest one (and costs 610 KB a boot until it
        /// does), and replaying the min pins the cursor to the least-active catalog and replays
        /// every row that ever moved, forever. Measured against prod 2026-08-25 on the same live
        /// data: max-scalar 610,333 B · min-scalar 2,192 B and rising · per-catalog 454 B.
        /// See <c>content_cursor_per_catalog/SPEC.md</c>.
        ///
        /// <c>data.latest_version</c> survives as INFORMATIONAL ONLY — "which publish is prod on",
        /// for the dashboard and for logs. NEVER replay it as a cursor.
        ///
        /// <paramref name="catalogs"/> narrows the response to a comma-separated subset
        /// ("texts"). Null/empty asks for every catalog. An UNKNOWN NAME IS IGNORED server-side,
        /// not a 400, so a build asking for a catalog this server does not have yet degrades
        /// rather than fails. <c>Golfin.Content.ContentService</c> sends "texts" and only "texts":
        /// Phase 1 overlays texts alone, and asking for catalogs it will not read would cost a
        /// 275 KB clubs payload on the boot path for nothing.
        ///
        /// CALLED BY <c>Golfin.Content.RemoteContentSource.FetchRoutine</c> (content_overlay_texts,
        /// Phase 1) — the first client-side reader of the content pipeline.
        /// </summary>
        public static string Content(string since, int build, string catalogs = null)
        {
            string url = BaseUrl + "/content?since=" + UnityWebRequest.EscapeURL(since ?? "") +
                         "&build=" + build;
            if (!string.IsNullOrEmpty(catalogs))
                url += "&catalogs=" + UnityWebRequest.EscapeURL(catalogs);
            return url;
        }

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
        /// PUT <c>{username}</c> — claim (or change to) a game username, enforcing GLOBAL
        /// uniqueness on <c>profiles.display_name</c> (case-insensitive unique index).
        ///
        /// AUTH REQUIRED. The client calls this BEFORE writing Supabase Auth
        /// <c>user_metadata.display_name</c>: the profiles row is what every other player's
        /// board reads, so it is the row that has to be unique. A taken name answers
        /// 200 <c>{updated:false, status:"taken"}</c> — a rule, not an HTTP error — mirroring
        /// the tournament-enter "insufficient" pattern.
        /// </summary>
        public static string UserUsername => BaseUrl + "/user/username";

        // ── GOLFIN player inventory, Phase 4 (content_player_inventory SPEC §2, §4) ──
        //
        // ALL FOUR REQUIRE AUTH and the server stamps `user_id` from the bearer token, never from
        // the body — the same posture as <see cref="TelemetryEvents"/> and
        // <see cref="UserGolfinCharacter"/>. There is deliberately no user id in any of these
        // request bodies: one could not be trusted, so one is not accepted.
        //
        // ⚠️ THIS IS NOT `user_inventory`. That table exists already and is the PARTNER APP's GIFT
        // inventory (routers/gifts.py) — a different concern on a different row. The game's
        // inventory is `profiles.golfin_inventory`, next to golfin_character_id.
        //
        // AND IT IS SYNC AND BACKUP, NOT ANTI-CHEAT (SPEC §6). Everything the client PUTs here is
        // client-asserted; a modified client can still grant itself anything. Moving inventory
        // server-side does not change that, exactly as moving the shop listing server-side did not
        // make prices authoritative. Server-authoritative spends are a separate, later decision.

        /// <summary>
        /// GET → <c>{data:{inventory, rev, updated_at}}</c> · PUT <c>{inventory, rev}</c> →
        /// <c>{data:{stored, rev}}</c> — the whole player inventory as ONE JSONB blob.
        ///
        /// The blob is DELTAS FROM THE CATALOG DEFAULT (<c>Golfin.InventorySync.InventoryCodec</c>):
        /// a club sitting at its catalog default is a bare id string. That is the cost constraint,
        /// and it is also why a published rebalance reaches every untouched instance for free.
        ///
        /// <c>rev</c> is optimistic concurrency. A PUT carrying a stale rev is REFUSED, and the
        /// refusal is a <b>200</b> carrying <c>{stored:false, status:"stale", rev, inventory}</c> —
        /// a business outcome, like the "taken" username, not an HTTP error. The client merges the
        /// returned blob into its own ADDITIVELY (union ids, max levels/quantities, never subtract)
        /// and PUTs once more at the returned rev. The server never merges: the merge needs catalog
        /// defaults that live in the client's bundled CSVs.
        /// </summary>
        public static string UserGolfinInventory => BaseUrl + "/user/golfin-inventory";

        /// <summary>
        /// GET → <c>{data:{grants:[{id, kind, ref_id, amount, note, created_at}]}}</c> — every
        /// admin-issued grant this player has not acked yet.
        ///
        /// Drained at BOOT, never mid-session. Additive-only by schema (<c>amount &gt; 0</c>), so a
        /// grant can only ever give something; there is no way to express a subtraction.
        /// </summary>
        public static string UserGolfinGrants => BaseUrl + "/user/golfin-grants";

        /// <summary>
        /// POST <c>{grant_ids:[…]}</c> → <c>{data:{acked}}</c> — mark grants applied.
        ///
        /// The client applies FIRST and acks SECOND, so a lost ack leaves a grant applied but still
        /// pending. That is why the client ALSO records applied ids in its save
        /// (<c>SaveData.appliedGrantIds</c>): the ack is the server's idempotency lock, the id
        /// ledger is the client's, and the window between them needs both.
        /// </summary>
        public static string UserGolfinGrantsAck => BaseUrl + "/user/golfin-grants/ack";

        // ── Server-authoritative shop purchase (shop_server_purchase SPEC §2.2) ──
        //
        // AUTH REQUIRED and the server stamps `user_id` from the bearer token, never from the body —
        // the same posture as <see cref="UserGolfinInventory"/> and <see cref="TelemetryEvents"/>.
        //
        // AND UNLIKE THE INVENTORY ENDPOINTS, THIS ONE *IS* ANTI-CHEAT. The body carries WHICH
        // LISTING the player tapped, never a price: the server reads the PUBLISHED shop_catalog row,
        // prices it off its own clock (listing + sale windows), debits through spend_pts and queues
        // the item as a grant — all in ONE transaction, so the RP can never be gone while the grant
        // does not exist. `expected_rp_cost` is a GUARD, not a price: if it disagrees with the
        // published one the call is refused with `price_changed` and nothing is written.

        /// <summary>
        /// POST <c>{entry_id, idempotency_key, build, expected_rp_cost?}</c> →
        /// <c>{data:{status, …}}</c> — buy one published shop listing at the SERVER's price.
        ///
        /// EVERY BUSINESS OUTCOME IS HTTP <b>200</b>, exactly like <see cref="PointsSpend"/>'s
        /// "insufficient" and the inventory PUT's "stale": <c>ok</c> · <c>insufficient</c> ·
        /// <c>price_changed</c> · <c>not_listed</c> (with a <c>reason</c>) · <c>already_owned</c> ·
        /// <c>unknown_entry</c> · <c>unsupported_category</c>. Only auth, malformed input and genuine
        /// faults are HTTP errors — so the status code must NEVER be what distinguishes "you cannot
        /// afford this" from "the server is unreachable".
        ///
        /// The <c>ok</c> payload is a SUPERSET of <see cref="PointsSpendResult"/> so the balance can
        /// be folded with the code that already exists, plus a <c>grant</c> the client applies exactly
        /// the way it applies an admin grant from <see cref="UserGolfinGrants"/>.
        /// </summary>
        public static string ShopPurchase => BaseUrl + "/shop/purchase";

        /// <summary>
        /// POST → <c>{data:&lt;golfin_level_up result&gt;}</c> — level ONE character or club, at the
        /// SERVER's price (progress_server_side SPEC §3.3).
        ///
        /// Body <c>{kind, ref_id, from_level, to_level, idempotency_key, build, expected_cost}</c>.
        /// AUTH REQUIRED and the user id comes from the TOKEN, never the body. The COST is not in the
        /// request either: the server sums it from the published <c>level_up_costs</c> catalog, debits
        /// through <c>spend_pts</c> and records the new level in one transaction. <c>expected_cost</c>
        /// is a GUARD — if it disagrees, the call is refused with <c>cost_changed</c> and NOTHING is
        /// written.
        ///
        /// EVERY BUSINESS OUTCOME IS HTTP <b>200</b>, exactly like <see cref="ShopPurchase"/>:
        /// <c>ok</c> · <c>insufficient</c> · <c>cost_changed</c> · <c>level_conflict</c> ·
        /// <c>costs_missing</c> · <c>invalid_range</c> · <c>not_available</c> (with a <c>reason</c>).
        ///
        /// The <c>ok</c> payload is a SUPERSET of <see cref="PointsSpendResult"/>, so the balance folds
        /// through <c>PointsService.ApplySpendResult</c> with the code that already exists.
        /// </summary>
        public static string ProgressLevelUp => BaseUrl + "/progress/level-up";

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

        // ── GOLFIN tournaments, async multiplayer half (tournament_async_board SPEC §2) ──
        //
        // All four hang off /tournaments/golfin/{slug}/… and ALL REQUIRE AUTH, unlike the public
        // schedule at TournamentsGolfin above: the server identifies the entrant from the bearer
        // token and never trusts a user id in the body. {slug} is the game-facing id
        // ("kasumigaseki_open") — the same string as TournamentDefinition.Id — never a uuid.

        /// <summary>
        /// POST <c>{character_id}</c> — enter a tournament.
        ///
        /// The server debits <c>entry_fee_pts</c> itself, through <c>spend_pts</c> with a
        /// DETERMINISTIC key (uuid5 of user:slug), so a retry after an ambiguous timeout cannot
        /// double-charge. The client must therefore NOT run its own spend for the fee — see
        /// <c>RemoteTournamentBackend.RegisterAsync</c>.
        ///
        /// 200 <c>{entered, already_entered, entry}</c> on success; 200
        /// <c>{entered:false, status:"insufficient", requested, total_points}</c> when the balance is
        /// short; 400 outside <c>start_at &lt;= now &lt; end_at</c>.
        /// </summary>
        public static string TournamentEnter(string slug) => TournamentGolfin(slug) + "/enter";

        /// <summary>
        /// POST <c>{hole_number, strokes, idempotency_key}</c> — submit one completed hole.
        ///
        /// Idempotent per (entry, hole): a replay answers 200 <c>{replayed:true, …}</c>, which the
        /// offline queue treats as success and drops the op on. 400 is a REJECTION (hole not in the
        /// set, strokes outside 1–15, no entry, window past <c>end_at + resolve_delay_minutes</c>,
        /// implausible pace) — the queue drops those too rather than retrying forever.
        /// </summary>
        public static string TournamentSubmitHole(string slug) => TournamentGolfin(slug) + "/submit-hole";

        /// <summary>
        /// GET → the caller's entry plus <c>holes:[…]</c>, or <c>{data: null}</c> when not entered.
        /// This is what makes a half-played tournament resumable on a second device.
        /// </summary>
        public static string TournamentEntry(string slug) => TournamentGolfin(slug) + "/entry";

        /// <summary>
        /// GET → <c>{fetched_at, provisional, bots_active, end_at, resolve_delay_minutes, entries:[…],
        /// player:{…}}</c> — the board every entrant shares.
        ///
        /// Ranks, ties and the organic bot reveal are all computed server-side; the client renders
        /// them verbatim and never re-ranks (same posture as <see cref="Leaderboard"/>).
        /// </summary>
        public static string TournamentLeaderboard(string slug) => TournamentGolfin(slug) + "/leaderboard";

        private static string TournamentGolfin(string slug) => BaseUrl + "/tournaments/golfin/" + slug;

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
