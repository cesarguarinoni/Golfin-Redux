// Order: progress_server_side §4 — the seven answers a server-priced level-up can come back with.
using Golfin.Net;

namespace Golfin.Economy
{
    /// <summary>
    /// What the server said about a level-up. The same shape and the same reasoning as
    /// <see cref="ShopPurchaseVerdict"/>, with the two verdicts that only exist once the SERVER owns
    /// both the price AND the level.
    ///
    /// <para>
    /// Deliberately NOT a bool. "the cost moved", "your level is not what you think it is" and
    /// "I could not reach the server" need three different pieces of UI: the first is answerable
    /// (re-price, tap CONFIRM again), the second is not (close, resync, reopen), and the third is
    /// neither. Merging any two of them is how a modal ends up telling the player they are offline
    /// because an operator retuned the cost table.
    /// </para>
    /// </summary>
    public enum ProgressLevelUpVerdict
    {
        /// <summary>Debited and RECORDED (or replayed a level-up that already happened). Run the
        /// local commit.</summary>
        Ok = 0,

        /// <summary>HTTP 200, <c>status:"insufficient"</c>. Nothing was written; the player is
        /// short.</summary>
        Insufficient,

        /// <summary>HTTP 200, <c>status:"cost_changed"</c>. The sum the modal showed is not the
        /// published one. Nothing was written — reload the cost table, rebuild the preview at the same
        /// target level, and let the player decide again at
        /// <see cref="ProgressLevelUpResult.Cost"/>.</summary>
        CostChanged,

        /// <summary>HTTP 200, <c>status:"level_conflict"</c>. The server's recorded level is not the
        /// one this client claimed — levelled on another device, or a save that drifted. Nothing was
        /// written and nothing was debited. The modal cannot fix this by asking again: it closes and
        /// the next inventory sync reconciles.</summary>
        LevelConflict,

        /// <summary>HTTP 200, <c>status:"not_available"</c> · <c>costs_missing</c> ·
        /// <c>invalid_range</c>, or a status this build does not know. All of them mean the same
        /// thing to the player — the server will not sell this level right now — and all of them are
        /// CONTENT or CLIENT bugs rather than player-facing outcomes, so they are loud in the log and
        /// quiet in the UI. Nothing may proceed.</summary>
        NotAvailable,

        /// <summary>No usable answer — offline, timeout, 5xx, expired session. NOTHING may proceed:
        /// with the cost server-owned there is no local number left to fall back to.</summary>
        Unavailable,

        /// <summary><c>PointsBackendEnabled</c> is OFF. No request was made; the caller runs its
        /// unchanged local-only path.</summary>
        Disabled
    }

    /// <summary>
    /// Result of one <see cref="ProgressService.LevelUpAsync"/> call.
    ///
    /// <para>
    /// There is no <c>MayProceed</c> here, and its absence is the point — the same call
    /// <see cref="ShopPurchaseOutcome"/> makes. A spend's caller asks one question ("may I run my
    /// body?") and <see cref="SpendVerdict.Disabled"/> answers yes. A level-up caller cannot: with
    /// the flag ON the level is RECORDED server-side, so "proceed" means something different from the
    /// flag-OFF local commit, and a shared boolean would let a call site treat
    /// <see cref="ProgressLevelUpVerdict.Disabled"/> as "level them up at your own price" — the exact
    /// hole this task closes. Callers branch on <see cref="Verdict"/> explicitly.
    /// </para>
    /// </summary>
    public sealed class ProgressLevelUpOutcome
    {
        public ProgressLevelUpVerdict Verdict { get; private set; }

        /// <summary>The server payload, when the server answered. Null on Unavailable/Disabled.</summary>
        public ProgressLevelUpResult Server { get; private set; }

        /// <summary>Transport-level detail, for logging and for branching on <see cref="ApiErrorKind"/>.</summary>
        public ApiResult<ProgressLevelUpResult> Api { get; private set; }

        /// <summary>True when the failure is connectivity rather than a server refusal.</summary>
        public bool IsOffline => Api != null &&
                                 (Api.ErrorKind == ApiErrorKind.Network ||
                                  Api.ErrorKind == ApiErrorKind.Timeout);

        /// <summary>What the player was actually charged — the SERVER's number, never the client's.
        /// On <see cref="ProgressLevelUpVerdict.CostChanged"/> it is the published sum to re-price
        /// at, which is the same field for the same reason: it is always what the server says the
        /// run costs.</summary>
        public int Cost => Server != null ? Server.Cost : 0;

        /// <summary>The level the server now holds. Meaningful on
        /// <see cref="ProgressLevelUpVerdict.LevelConflict"/>, where it is what the client must
        /// reconcile to; 0 otherwise.</summary>
        public int ServerLevel => Server != null ? Server.ServerLevel : 0;

        public static ProgressLevelUpOutcome Ok(ProgressLevelUpResult server, ApiResult<ProgressLevelUpResult> api)
            => new ProgressLevelUpOutcome { Verdict = ProgressLevelUpVerdict.Ok, Server = server, Api = api };

        public static ProgressLevelUpOutcome Insufficient(ProgressLevelUpResult server, ApiResult<ProgressLevelUpResult> api)
            => new ProgressLevelUpOutcome { Verdict = ProgressLevelUpVerdict.Insufficient, Server = server, Api = api };

        public static ProgressLevelUpOutcome CostChanged(ProgressLevelUpResult server, ApiResult<ProgressLevelUpResult> api)
            => new ProgressLevelUpOutcome { Verdict = ProgressLevelUpVerdict.CostChanged, Server = server, Api = api };

        public static ProgressLevelUpOutcome LevelConflict(ProgressLevelUpResult server, ApiResult<ProgressLevelUpResult> api)
            => new ProgressLevelUpOutcome { Verdict = ProgressLevelUpVerdict.LevelConflict, Server = server, Api = api };

        public static ProgressLevelUpOutcome NotAvailable(ProgressLevelUpResult server, ApiResult<ProgressLevelUpResult> api)
            => new ProgressLevelUpOutcome { Verdict = ProgressLevelUpVerdict.NotAvailable, Server = server, Api = api };

        public static ProgressLevelUpOutcome Unavailable(ApiResult<ProgressLevelUpResult> api)
            => new ProgressLevelUpOutcome { Verdict = ProgressLevelUpVerdict.Unavailable, Api = api };

        public static ProgressLevelUpOutcome Disabled()
            => new ProgressLevelUpOutcome { Verdict = ProgressLevelUpVerdict.Disabled };

        public override string ToString()
            => Server != null ? $"{Verdict} ({Server})" : Verdict.ToString();
    }
}
