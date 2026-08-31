// Order: gacha_client_real_pull §4.1 — the eight answers a server-side pull can come back with.
using Golfin.Net;

namespace Golfin.Economy
{
    /// <summary>
    /// What the server said about a gacha pull. Same shape and same reasoning as
    /// <see cref="ShopPurchaseVerdict"/>: each verdict needs a different piece of UI, and merging
    /// any two of them is how a card ends up telling the player they are offline because a banner
    /// ended.
    /// </summary>
    public enum GachaPullVerdict
    {
        /// <summary>Debited, rolled and queued (or replayed a pull that already happened). The
        /// prizes in <see cref="GachaPullOutcome.Prizes"/> are what to reveal — all of them, in
        /// that order, and nothing else.</summary>
        Ok = 0,

        /// <summary>HTTP 200, <c>status:"insufficient"</c>. Not enough tickets; nothing written, so
        /// the same key can succeed later.</summary>
        Insufficient,

        /// <summary>HTTP 200, <c>status:"cost_changed"</c>. The number on the card is not the
        /// published one. Nothing written — re-render at <see cref="GachaPullOutcome.Cost"/> and
        /// let the player decide again.</summary>
        CostChanged,

        /// <summary>HTTP 200, <c>status:"pull_cap"</c>. The banner's <c>maxPullsPerPlayer</c> would
        /// be crossed.</summary>
        PullCap,

        /// <summary>HTTP 200, <c>status:"not_available"</c> with an operator-facing reason, or
        /// <c>unknown_banner</c>. The banner this build is showing is not one the server will roll:
        /// reload the catalog and rebuild the carousel.</summary>
        NotAvailable,

        /// <summary>HTTP 200, <c>not_available</c> with reason <c>paused</c> or <c>disabled</c> —
        /// the gacha kill switch. Separated from <see cref="NotAvailable"/> because it is temporary
        /// and global: the banner is fine, the feature is off, and reloading the catalog would
        /// withhold nothing.</summary>
        Paused,

        /// <summary>No usable answer — offline, timeout, 5xx, expired session — or the
        /// <c>PointsBackendEnabled</c> flag is OFF. NOTHING may proceed: with the roll
        /// server-owned there is no local prize table left to fall back to.</summary>
        Unavailable,

        /// <summary>A status this build does not know, including <c>invalid_count</c> — which is a
        /// client bug, not a player-facing outcome. Nothing may proceed.</summary>
        Unknown
    }

    /// <summary>
    /// Result of one <see cref="GachaPullService.PullAsync"/> call.
    ///
    /// <para>
    /// There is no <c>MayProceed</c> here, for the same reason <see cref="ShopPurchaseOutcome"/>
    /// has none and more sharply: with the flag OFF there is no local roll to fall back to. The
    /// mock pool this task deleted WAS that fallback, and it is gone precisely so a call site
    /// cannot treat "the server did not answer" as "roll it yourself".
    /// </para>
    /// </summary>
    public sealed class GachaPullOutcome
    {
        public GachaPullVerdict Verdict { get; private set; }

        /// <summary>The server payload, when the server answered. Null on Unavailable.</summary>
        public GachaPullResult Server { get; private set; }

        /// <summary>Transport-level detail, for logging and for branching on <see cref="ApiErrorKind"/>.</summary>
        public ApiResult<GachaPullResult> Api { get; private set; }

        /// <summary>True when the failure is connectivity rather than a server refusal — drives the
        /// offline copy instead of a gacha-specific message.</summary>
        public bool IsOffline => Api != null &&
                                 (Api.ErrorKind == ApiErrorKind.Network ||
                                  Api.ErrorKind == ApiErrorKind.Timeout);

        /// <summary>The prizes to reveal, in reveal order. Empty unless <see cref="GachaPullVerdict.Ok"/>.</summary>
        public GachaPrizeDto[] Prizes
            => Server != null && Server.Prizes != null ? Server.Prizes : System.Array.Empty<GachaPrizeDto>();

        /// <summary>The published cost, on <see cref="GachaPullVerdict.CostChanged"/>.</summary>
        public int Cost => Server != null ? Server.Cost : 0;

        /// <summary>The ticket balance the player is short of, on <see cref="GachaPullVerdict.Insufficient"/>.</summary>
        public int Balance => Server != null ? Server.Balance : 0;

        /// <summary>The banner's cap and the player's usage, on <see cref="GachaPullVerdict.PullCap"/>.</summary>
        public int Limit => Server != null ? Server.Limit : 0;
        public int Used  => Server != null ? Server.Used  : 0;

        /// <summary>The <c>not_available</c> reason, for the log. Never shown to the player — the
        /// ten reasons are operator vocabulary.</summary>
        public string Reason => Server != null ? Server.Reason : null;

        public static GachaPullOutcome Ok(GachaPullResult server, ApiResult<GachaPullResult> api)
            => new GachaPullOutcome { Verdict = GachaPullVerdict.Ok, Server = server, Api = api };

        public static GachaPullOutcome Insufficient(GachaPullResult server, ApiResult<GachaPullResult> api)
            => new GachaPullOutcome { Verdict = GachaPullVerdict.Insufficient, Server = server, Api = api };

        public static GachaPullOutcome CostChanged(GachaPullResult server, ApiResult<GachaPullResult> api)
            => new GachaPullOutcome { Verdict = GachaPullVerdict.CostChanged, Server = server, Api = api };

        public static GachaPullOutcome PullCap(GachaPullResult server, ApiResult<GachaPullResult> api)
            => new GachaPullOutcome { Verdict = GachaPullVerdict.PullCap, Server = server, Api = api };

        public static GachaPullOutcome NotAvailable(GachaPullResult server, ApiResult<GachaPullResult> api)
            => new GachaPullOutcome { Verdict = GachaPullVerdict.NotAvailable, Server = server, Api = api };

        public static GachaPullOutcome Paused(GachaPullResult server, ApiResult<GachaPullResult> api)
            => new GachaPullOutcome { Verdict = GachaPullVerdict.Paused, Server = server, Api = api };

        public static GachaPullOutcome Unknown(GachaPullResult server, ApiResult<GachaPullResult> api)
            => new GachaPullOutcome { Verdict = GachaPullVerdict.Unknown, Server = server, Api = api };

        public static GachaPullOutcome Unavailable(ApiResult<GachaPullResult> api)
            => new GachaPullOutcome { Verdict = GachaPullVerdict.Unavailable, Api = api };

        public override string ToString()
            => Server != null ? $"{Verdict} ({Server})" : Verdict.ToString();
    }
}
