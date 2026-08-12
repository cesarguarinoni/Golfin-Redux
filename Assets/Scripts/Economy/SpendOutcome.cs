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
        Disabled
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

        /// <summary>True when the failure is a connectivity problem rather than a server refusal —
        /// drives the "Connection required" copy instead of "Not enough Reward Points".</summary>
        public bool IsOffline => Api != null &&
                                 (Api.ErrorKind == ApiErrorKind.Network ||
                                  Api.ErrorKind == ApiErrorKind.Timeout);

        public static SpendOutcome Ok(PointsSpendResult server, ApiResult<PointsSpendResult> api)
            => new SpendOutcome { Verdict = SpendVerdict.Approved, Server = server, Api = api };

        public static SpendOutcome Insufficient(PointsSpendResult server, ApiResult<PointsSpendResult> api)
            => new SpendOutcome { Verdict = SpendVerdict.Insufficient, Server = server, Api = api };

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
