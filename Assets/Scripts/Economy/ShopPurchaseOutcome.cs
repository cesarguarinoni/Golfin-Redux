// Order: shop_server_purchase §3.1 — the eight answers a server-priced purchase can come back with.
using Golfin.Net;

namespace Golfin.Economy
{
    /// <summary>
    /// What the server said about a shop purchase. The same shape and the same reasoning as
    /// <see cref="SpendVerdict"/>, with three verdicts that only exist once the SERVER owns the price.
    ///
    /// <para>
    /// Deliberately NOT a bool, and deliberately not collapsed into <see cref="SpendVerdict"/>:
    /// "the price moved", "that is no longer for sale" and "I could not reach the server" need three
    /// different pieces of UI, and merging any two of them is how a card ends up telling the player
    /// they are offline because a sale ended.
    /// </para>
    /// </summary>
    public enum ShopPurchaseVerdict
    {
        /// <summary>Debited and queued (or replayed a purchase that already happened). Apply the grant.</summary>
        Ok = 0,

        /// <summary>HTTP 200, <c>status:"insufficient"</c>. Nothing was written; the player is short.</summary>
        Insufficient,

        /// <summary>HTTP 200, <c>status:"price_changed"</c>. The number on the card is not the published
        /// one. Nothing was written — re-render at <see cref="ShopPurchaseResult.Price"/> and let the
        /// player decide again.</summary>
        PriceChanged,

        /// <summary>HTTP 200, <c>status:"not_listed"</c>. The window closed, the row was deactivated,
        /// content is killed, a bound is unparseable, the price is invalid, or the referenced club /
        /// character / item / ball is itself inactive. <see cref="ShopPurchaseResult.Reason"/> says which.</summary>
        NotListed,

        /// <summary>HTTP 200, <c>status:"already_owned"</c>. Clubs and characters are unique.</summary>
        AlreadyOwned,

        /// <summary>HTTP 200 with a status this build does not know — including <c>unknown_entry</c> and
        /// <c>unsupported_category</c>, which are catalog bugs rather than player-facing outcomes.
        /// Nothing may proceed.</summary>
        Unknown,

        /// <summary>No usable answer — offline, timeout, 5xx, expired session. NOTHING may proceed:
        /// with the price server-owned there is no local number left to fall back to.</summary>
        Unavailable,

        /// <summary><c>PointsBackendEnabled</c> is OFF. No request was made; the caller runs its
        /// unchanged local-only path.</summary>
        Disabled
    }

    /// <summary>
    /// Result of one <see cref="ShopPurchaseService.PurchaseAsync"/> call.
    ///
    /// <para>
    /// There is no <c>MayProceed</c> here, unlike <see cref="SpendOutcome"/>, and its absence is the
    /// point. A spend's caller asks one question ("may I run my body?") and <see cref="SpendVerdict.Disabled"/>
    /// answers yes. A purchase caller cannot: with the flag ON the item arrives as a server GRANT, so
    /// "proceed" means something completely different from the flag-OFF local grant — and a shared
    /// boolean would let a call site treat <see cref="ShopPurchaseVerdict.Disabled"/> as "grant it
    /// yourself at your own price", which is the exact hole this whole task closes. Callers branch on
    /// <see cref="Verdict"/> explicitly.
    /// </para>
    /// </summary>
    public sealed class ShopPurchaseOutcome
    {
        public ShopPurchaseVerdict Verdict { get; private set; }

        /// <summary>The server payload, when the server answered. Null on Unavailable/Disabled.</summary>
        public ShopPurchaseResult Server { get; private set; }

        /// <summary>Transport-level detail, for logging and for branching on <see cref="ApiErrorKind"/>.</summary>
        public ApiResult<ShopPurchaseResult> Api { get; private set; }

        /// <summary>True when the failure is connectivity rather than a server refusal — drives the
        /// "Connection required" copy instead of "Not enough Reward Points".</summary>
        public bool IsOffline => Api != null &&
                                 (Api.ErrorKind == ApiErrorKind.Network ||
                                  Api.ErrorKind == ApiErrorKind.Timeout);

        /// <summary>The grant to apply. Non-null only on <see cref="ShopPurchaseVerdict.Ok"/>.</summary>
        public ShopGrantDto Grant => Server != null ? Server.Grant : null;

        /// <summary>What the player was actually charged — the SERVER's number, never the client's.</summary>
        public int Charged => Server != null ? Server.Charged : 0;

        public static ShopPurchaseOutcome Ok(ShopPurchaseResult server, ApiResult<ShopPurchaseResult> api)
            => new ShopPurchaseOutcome { Verdict = ShopPurchaseVerdict.Ok, Server = server, Api = api };

        public static ShopPurchaseOutcome Insufficient(ShopPurchaseResult server, ApiResult<ShopPurchaseResult> api)
            => new ShopPurchaseOutcome { Verdict = ShopPurchaseVerdict.Insufficient, Server = server, Api = api };

        public static ShopPurchaseOutcome PriceChanged(ShopPurchaseResult server, ApiResult<ShopPurchaseResult> api)
            => new ShopPurchaseOutcome { Verdict = ShopPurchaseVerdict.PriceChanged, Server = server, Api = api };

        public static ShopPurchaseOutcome NotListed(ShopPurchaseResult server, ApiResult<ShopPurchaseResult> api)
            => new ShopPurchaseOutcome { Verdict = ShopPurchaseVerdict.NotListed, Server = server, Api = api };

        public static ShopPurchaseOutcome AlreadyOwned(ShopPurchaseResult server, ApiResult<ShopPurchaseResult> api)
            => new ShopPurchaseOutcome { Verdict = ShopPurchaseVerdict.AlreadyOwned, Server = server, Api = api };

        public static ShopPurchaseOutcome Unknown(ShopPurchaseResult server, ApiResult<ShopPurchaseResult> api)
            => new ShopPurchaseOutcome { Verdict = ShopPurchaseVerdict.Unknown, Server = server, Api = api };

        public static ShopPurchaseOutcome Unavailable(ApiResult<ShopPurchaseResult> api)
            => new ShopPurchaseOutcome { Verdict = ShopPurchaseVerdict.Unavailable, Api = api };

        public static ShopPurchaseOutcome Disabled()
            => new ShopPurchaseOutcome { Verdict = ShopPurchaseVerdict.Disabled };

        public override string ToString()
            => Server != null ? $"{Verdict} ({Server})" : Verdict.ToString();
    }
}
