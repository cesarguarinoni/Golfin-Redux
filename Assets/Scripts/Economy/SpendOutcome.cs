// Order: reward_points_backend Slice 2 — the four answers a spend can come back with.
using Golfin.Net;

namespace Golfin.Economy
{
    /// <summary>What the server said about a spend. Deliberately NOT a bool: "you cannot afford this"
    /// and "I could not reach the server" need different UI, and collapsing them is how a connection
    /// drop ends up telling the player they are broke.</summary>
    public enum SpendVerdict
    {
        /// <summary>Debited (or replayed a debit that already happened). Proceed with the action.</summary>
        Approved = 0,

        /// <summary>HTTP 200, <c>status:"insufficient"</c>. Nothing was written; the player is short.</summary>
        Insufficient,

        /// <summary>No usable answer — offline, timeout, 5xx, expired session. NOTHING may proceed.</summary>
        Unavailable,

        /// <summary><c>PointsBackendEnabled</c> is OFF. No request was made; the caller runs its
        /// unchanged local-only path.</summary>
        Disabled,

        // ── Mode entry, server-validated (game_modes_admin §4) ───────────────────────
        //
        // Three more DEFINITIVE answers, all HTTP 200 with nothing debited. They are separate
        // verdicts and not one "Refused" for the same reason Insufficient and Unavailable are
        // separate: the UI differs. FeeChanged is an INVITATION to tap again at the shown number;
        // the other two mean the mode is gone or shut and the card should stop offering it.

        /// <summary>HTTP 200, <c>status:"fee_changed"</c>. The card's fee is not the published one.
        /// Nothing was written — re-render at <see cref="SpendOutcome.ServerFee"/> and let the player
        /// decide again. A publish landing mid-session produces this for every client still open, so
        /// it is a NORMAL outcome and not an error.</summary>
        FeeChanged,

        /// <summary>HTTP 200, <c>status:"unknown_mode"</c>. The published catalog has no such mode —
        /// this client is stale. Nothing was written.</summary>
        UnknownMode,

        /// <summary>HTTP 200, <c>status:"mode_locked"</c>. The mode is published Coming Soon, so
        /// entry is refused server-side too, not only hidden client-side.</summary>
        ModeLocked
    }

    /// <summary>
    /// Result of one <see cref="PointsService.SpendAsync"/> call.
    ///
    /// <see cref="MayProceed"/> is the single question every call site asks. It is true for
    /// <see cref="SpendVerdict.Approved"/> and <see cref="SpendVerdict.Disabled"/> — the flag-off case
    /// is "the server is not in this build's loop", not "denied", and must behave exactly like HEAD.
    /// </summary>
    public sealed class SpendOutcome
    {
        public SpendVerdict Verdict { get; private set; }

        /// <summary>The server payload, when the server answered. Null on Unavailable/Disabled.</summary>
        public PointsSpendResult Server { get; private set; }

        /// <summary>Transport-level detail, for logging and for branching on <see cref="ApiErrorKind"/>.</summary>
        public ApiResult<PointsSpendResult> Api { get; private set; }

        public bool MayProceed => Verdict == SpendVerdict.Approved || Verdict == SpendVerdict.Disabled;

        /// <summary>
        /// The PUBLISHED entry fee on a <see cref="SpendVerdict.FeeChanged"/> outcome, else 0.
        ///
        /// This is what makes FeeChanged actionable rather than merely a refusal: the card re-renders
        /// at this number and the player's SECOND tap pays it. Without it the client would only know
        /// that its price was wrong, not what the right one is, and the only recovery would be a
        /// relaunch.
        /// </summary>
        public int ServerFee => Server != null ? Server.Fee : 0;

        /// <summary>The mode the server was talking about, on the three mode-entry verdicts.</summary>
        public string ModeId => Server != null ? Server.ModeId : null;

        /// <summary>True when the failure is a connectivity problem rather than a server refusal —
        /// drives the "Connection required" copy instead of "Not enough Reward Points".</summary>
        public bool IsOffline => Api != null &&
                                 (Api.ErrorKind == ApiErrorKind.Network ||
                                  Api.ErrorKind == ApiErrorKind.Timeout);

        public static SpendOutcome Ok(PointsSpendResult server, ApiResult<PointsSpendResult> api)
            => new SpendOutcome { Verdict = SpendVerdict.Approved, Server = server, Api = api };

        public static SpendOutcome Insufficient(PointsSpendResult server, ApiResult<PointsSpendResult> api)
            => new SpendOutcome { Verdict = SpendVerdict.Insufficient, Server = server, Api = api };

        public static SpendOutcome FeeChanged(PointsSpendResult server, ApiResult<PointsSpendResult> api)
            => new SpendOutcome { Verdict = SpendVerdict.FeeChanged, Server = server, Api = api };

        public static SpendOutcome UnknownMode(PointsSpendResult server, ApiResult<PointsSpendResult> api)
            => new SpendOutcome { Verdict = SpendVerdict.UnknownMode, Server = server, Api = api };

        public static SpendOutcome ModeLocked(PointsSpendResult server, ApiResult<PointsSpendResult> api)
            => new SpendOutcome { Verdict = SpendVerdict.ModeLocked, Server = server, Api = api };

        public static SpendOutcome Unavailable(ApiResult<PointsSpendResult> api)
            => new SpendOutcome { Verdict = SpendVerdict.Unavailable, Api = api };

        public static SpendOutcome Disabled()
            => new SpendOutcome { Verdict = SpendVerdict.Disabled };

        /// <summary>A zero/negative-cost action with the flag ON. Nothing to debit, so nothing to ask.</summary>
        public static SpendOutcome FreeOfCharge()
            => new SpendOutcome { Verdict = SpendVerdict.Approved };

        public override string ToString()
            => Server != null ? $"{Verdict} ({Server})" : Verdict.ToString();
    }
}
