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
    }
}
