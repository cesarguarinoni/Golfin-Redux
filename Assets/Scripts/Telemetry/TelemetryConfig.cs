// Order: beta_telemetry — tuning constants + the editor send gate.
namespace Golfin.Telemetry
{
    /// <summary>
    /// Every knob the telemetry pipeline has. Constants rather than a ScriptableObject on
    /// purpose: there is no remote config and no kill switch in scope for the beta
    /// (SPEC §6), so nothing here is meant to change without a build.
    /// </summary>
    public static class TelemetryConfig
    {
        /// <summary>Master switch. Compiled in; flipping it to false makes every hook a no-op.</summary>
        public const bool Enabled = true;

        /// <summary>Flush as soon as this many events are pending.</summary>
        public const int FlushEventCount = 20;

        /// <summary>…or this long after the last flush, whichever comes first.</summary>
        public const float FlushIntervalSeconds = 30f;

        /// <summary>Hard queue bound. Past this the OLDEST event is dropped, so a long
        /// offline stretch costs the start of the session rather than unbounded memory.</summary>
        public const int QueueCap = 500;

        /// <summary>The server 413s above 100 per batch, so never build a bigger one.</summary>
        public const int MaxEventsPerBatch = 100;

        /// <summary>SPEC §1 #10 — a crash loop must not become the whole dataset.</summary>
        public const int MaxClientErrorsPerSession = 10;

        public const int MaxErrorMessageChars = 300;
        public const int MaxErrorStackChars = 2000;

        /// <summary>
        /// Whether a fresh <see cref="TelemetryService"/> sends by default.
        ///
        /// OFF in the Editor unless <c>GOLFIN_TELEMETRY_DEBUG</c> is defined, so a day of
        /// play-mode iteration does not land in the middle of the beta dataset. Device
        /// builds send whenever the session is authenticated. Tests set
        /// <see cref="TelemetryService.SendsEnabled"/> explicitly and do not depend on this.
        /// </summary>
#if UNITY_EDITOR && !GOLFIN_TELEMETRY_DEBUG
        public const bool DefaultSendsEnabled = false;
#else
        public const bool DefaultSendsEnabled = true;
#endif
    }

    /// <summary>Every event name the client emits. String constants so a typo is a compile
    /// error at the hook site rather than a silently-unqueryable row.</summary>
    public static class TelemetryEventNames
    {
        public const string SessionStart   = "session_start";
        public const string SessionEnd     = "session_end";
        public const string ScreenView     = "screen_view";
        public const string RoundStart     = "round_start";
        public const string ShotTaken      = "shot_taken";
        public const string FlickRejected  = "flick_rejected";
        public const string ShotCancelled  = "shot_cancelled";
        public const string HoleComplete   = "hole_complete";
        public const string RoundAbandoned = "round_abandoned";
        public const string ClientError    = "client_error";
        public const string PointsChanged  = "points_changed";
        public const string LevelUp        = "level_up";

        /// <summary>
        /// The Home "NEW DAILY MISSION!" pill was tapped (daily_mission_home_pill §2).
        ///
        /// It exists to answer whether the Home surface is what actually drives daily
        /// engagement, or whether players were already reaching Missions from the mode carousel
        /// anyway. Carries the streak and the UTC date, so a tap can be joined to whether it
        /// became a claim.
        /// </summary>
        public const string DailyPillTap   = "daily_pill_tap";

        /// <summary>
        /// The additive inventory merge put a quantity the player already held back UP — i.e. it may
        /// have refunded something they consumed (CONTENT_PIPELINE_PLAN §6.5 decision 1).
        ///
        /// <para>
        /// This exists to turn an unknown into a COUNT. The refund itself is an accepted trade for
        /// the beta; what is not acceptable is not knowing how often it fires, because beta
        /// consumption figures are what tune the economy. ~0 of these through the beta and
        /// server-authoritative spends (PLAN §6 step 4d) stay a launch-gate; anything else and they
        /// move up. The player is the row's `user_id`, stamped server-side from the token.
        /// </para>
        /// </summary>
        public const string InventoryMergeRaise = "inventory_merge_raise";

        // ── The score upload flow (score_upload_flow §2) ─────────────────────
        //
        // The three moments that answer "does the first real PLAYLIFE feature convert": how many
        // players open it, how many finish, and — from the abandon step number — WHERE the ones
        // who do not finish stop. `score_upload_abandon` carries the 1-5 step so the drop-off is a
        // funnel and not a single count.

        /// <summary>Score Upload was opened. Payload: source.</summary>
        public const string ScoreUploadOpen   = "score_upload_open";

        /// <summary>The flow was left before posting. Payload: step (1-5).</summary>
        public const string ScoreUploadAbandon = "score_upload_abandon";

        /// <summary>A score reached the server. Payload: input_method, gps_verified, trust,
        /// points_earned, holes — the SERVER's numbers, so the funnel and the ledger agree.</summary>
        public const string ScoreUploadPosted = "score_upload_posted";

        // ── The gacha funnel (gacha_ops_polish §3) ────────────────────────────
        //
        // Five events, ALL carrying `banner_id`, that answer one question the server-side pull log
        // structurally cannot: what happened to the players who did NOT pull. The log has a row per
        // pull, so it can say what was won and never why a banner was seen a thousand times and
        // tapped twice. These are the BEHAVIOUR view — views, taps, refusals, skips — and they stop
        // at the six-int rarity histogram: duplicating prize detail into telemetry would create a
        // second, weaker copy of a ledger that is already authoritative.

        /// <summary>A banner card became the centred one, once per banner per Rewards Center open.
        /// The denominator of every conversion rate below.</summary>
        public const string GachaBannerView = "gacha_banner_view";

        /// <summary>A PULL button was tapped — BEFORE the server is asked, so a tap that the
        /// server then refuses still appears here. Taps minus results is the abandonment.</summary>
        public const string GachaPullTap = "gacha_pull_tap";

        /// <summary>The server answered. <c>status</c> distinguishes a pull from each of the six
        /// refusals, which is what makes "insufficient tickets" measurable as a funnel step rather
        /// than as a silent drop.</summary>
        public const string GachaPullResult = "gacha_pull_result";

        /// <summary>SKIP on the reveal. A high skip rate is a statement about the animation, not
        /// about the pull.</summary>
        public const string GachaRevealSkip = "gacha_reveal_skip";

        /// <summary>The in-app RATES &amp; RULES modal was opened (§2).</summary>
        public const string GachaRulesOpen = "gacha_rules_open";
    }
}
