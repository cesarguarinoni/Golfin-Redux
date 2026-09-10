// asset_loans §2 — the loan wire shapes, transcribed from backend/routers/loans.py::_to_dto.
using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Golfin.Social
{
    /// <summary>
    /// One party to a loan — the embedded profile subset <c>loans.py::_party</c> writes.
    /// <c>display_name</c> is never null on the wire (the router substitutes "PLAYER"), but it is
    /// still read defensively: a nameless ribbon is a bug, a NullReferenceException in
    /// <c>UpdatePanel</c> is a black screen.
    /// </summary>
    public sealed class LoanPartyDto
    {
        [JsonProperty("id")]           public string Id;
        [JsonProperty("display_name")] public string DisplayName;
        [JsonProperty("avatar_url")]   public string AvatarUrl;
        [JsonProperty("avatar_level")] public int    AvatarLevel;

        /// <summary>What a nameless party renders as. Named rather than inlined because three
        /// call sites now need the same word and a second spelling of it would show up as one
        /// player being called PLAYER on the ribbon and something else in a toast.</summary>
        public const string Fallback = "PLAYER";

        public string Name => string.IsNullOrWhiteSpace(DisplayName) ? Fallback : DisplayName;
    }

    /// <summary>
    /// One row of <c>golfin_loans</c>, from either side.
    ///
    /// <para>
    /// TIMESTAMPS ARRIVE AS STRINGS AND STAY STRINGS. <c>ApiEnvelope</c> deserialises with
    /// <c>DateParseHandling.None</c>, so Newtonsoft never turns these into <see cref="DateTime"/>
    /// behind our back — the same posture as the GPS DTOs. <see cref="EndsAtUtc"/> parses on
    /// demand with <c>RoundtripKind</c>, and answers null rather than throwing, because a loan
    /// whose timestamp will not parse must still render (as "no time left shown") rather than take
    /// the panel down.
    /// </para>
    /// <para>
    /// <see cref="Level"/> IS THE OWNER'S CURRENT LEVEL, not the borrower's copy of it. That is
    /// the whole point of the feature: there is exactly one level for an asset and it belongs to
    /// the lender, so this is what a borrowed instance is created at and what the lender catches
    /// up to when the loan ends.
    /// </para>
    /// </summary>
    public sealed class LoanDto
    {
        [JsonProperty("id")]              public string Id;
        /// <summary><c>character</c> | <c>club</c>.</summary>
        [JsonProperty("kind")]            public string Kind;
        [JsonProperty("ref_id")]          public string RefId;
        [JsonProperty("lender")]          public LoanPartyDto Lender;
        [JsonProperty("borrower")]        public LoanPartyDto Borrower;
        [JsonProperty("days")]            public int    Days;
        [JsonProperty("starts_at")]       public string StartsAt;
        [JsonProperty("ends_at")]         public string EndsAt;
        [JsonProperty("ended_at")]        public string EndedAt;

        // ── asset_loans_offers §2 — the offer clock ───────────────────────────
        // Null on every v1 loan, which is exactly how an older row renders as the
        // plain ON LOAN state it always was.
        [JsonProperty("offered_at")]       public string OfferedAt;
        [JsonProperty("offer_expires_at")] public string OfferExpiresAt;
        /// <summary>When the offer was accepted / declined / rescinded / lapsed.</summary>
        [JsonProperty("answered_at")]      public string AnsweredAt;

        /// <summary><c>offered</c> | <c>active</c> | <c>returned</c> | <c>expired</c> |
        /// <c>declined</c> | <c>rescinded</c> | <c>offer_expired</c>.</summary>
        [JsonProperty("status")]          public string Status;
        [JsonProperty("level")]           public int    Level;
        [JsonProperty("level_at_start")]  public int    LevelAtStart;
        [JsonProperty("level_at_end")]    public int?   LevelAtEnd;
        [JsonProperty("lender_share_bp")] public int    LenderShareBp;
        [JsonProperty("rp_to_lender")]    public int    RpToLender;
        [JsonProperty("rp_to_borrower")]  public int    RpToBorrower;

        public const string KindCharacter = "character";
        public const string KindClub      = "club";

        public bool IsCharacter => string.Equals(Kind, KindCharacter, StringComparison.Ordinal);
        public bool IsClub      => string.Equals(Kind, KindClub,      StringComparison.Ordinal);

        /// <summary><c>ends_at</c> as UTC, or null when it is absent or unparseable.</summary>
        public DateTime? EndsAtUtc => ParseUtc(EndsAt);

        /// <summary><c>offer_expires_at</c> as UTC, or null on a v1 loan.</summary>
        public DateTime? OfferExpiresAtUtc => ParseUtc(OfferExpiresAt);

        public const string StatusOffered      = "offered";
        public const string StatusActive       = "active";
        public const string StatusDeclined     = "declined";
        public const string StatusRescinded    = "rescinded";
        public const string StatusOfferExpired = "offer_expired";

        /// <summary>
        /// The ONE liveness predicate, mirrored from the server (`status = 'active' and now() &lt;
        /// ends_at`). Written once here so no caller re-derives it and gets it subtly different —
        /// a client that thinks a loan is live when the server does not shows a RETURN button that
        /// answers <c>not_active</c>.
        ///
        /// <para>A row whose <c>ends_at</c> will not parse is treated as live, matching the
        /// server's own choice: ending a real loan over a parse hiccup is the worse mistake.</para>
        /// </summary>
        public bool IsLive(DateTime? nowUtc = null)
        {
            if (!string.Equals(Status, StatusActive, StringComparison.Ordinal)) return false;
            DateTime? ends = EndsAtUtc;
            if (!ends.HasValue) return true;
            return (nowUtc ?? DateTime.UtcNow) < ends.Value;
        }

        /// <summary>
        /// THE SECOND PREDICATE (asset_loans_offers §1.2): is this row an offer still waiting
        /// for an answer? <c>offered</c> and not past <c>offer_expires_at</c>.
        ///
        /// <para>
        /// DELIBERATELY NOT FOLDED INTO <see cref="IsLive"/>. The two answer different
        /// questions and the server keeps them apart for the same reason: LIVE means "the
        /// borrower is playing it" (splits RP, redirects level-ups, sits in their roster) and
        /// an offer does NONE of those. What an offer does is LOCK the asset on the lender's
        /// side — and that is <see cref="IsLocked"/>, which is the union of the two.
        /// </para>
        /// <para>
        /// An unparseable <c>offer_expires_at</c> is treated as pending, matching the server's
        /// own choice on <c>ends_at</c>: a parse hiccup silently cancelling a real offer is the
        /// more expensive mistake.
        /// </para>
        /// </summary>
        public bool IsPendingOffer(DateTime? nowUtc = null)
        {
            if (!string.Equals(Status, StatusOffered, StringComparison.Ordinal)) return false;
            DateTime? expires = OfferExpiresAtUtc;
            if (!expires.HasValue) return true;
            return (nowUtc ?? DateTime.UtcNow) < expires.Value;
        }

        /// <summary>
        /// Is the asset out of the LENDER's hands — offered OR running? Mirrors the server's
        /// <c>_is_locked</c> and the two widened partial unique indexes.
        ///
        /// <para>This is what <c>LoanService.Out</c> is filtered on, which is what makes
        /// <c>IsLentOut</c> answer true for an offer and every existing lock path — select,
        /// equip, level up, both detail panels — hold with no new code.</para>
        /// </summary>
        public bool IsLocked(DateTime? nowUtc = null) => IsLive(nowUtc) || IsPendingOffer(nowUtc);

        /// <summary>
        /// An offer that ENDED without becoming a loan. The three terminal states the lender's
        /// client turns into "they declined" / "you took it back" / "it expired", each with its
        /// own toast — one <c>cancelled</c> state would have to guess which sentence to show.
        /// </summary>
        public bool IsOfferEnded =>
            string.Equals(Status, StatusDeclined,     StringComparison.Ordinal)
         || string.Equals(Status, StatusRescinded,    StringComparison.Ordinal)
         || string.Equals(Status, StatusOfferExpired, StringComparison.Ordinal);

        /// <summary>Whole hours left, floored at zero. The ribbon formats from this.</summary>
        public TimeSpan TimeLeft(DateTime? nowUtc = null)
        {
            DateTime? ends = EndsAtUtc;
            if (!ends.HasValue) return TimeSpan.Zero;
            TimeSpan left = ends.Value - (nowUtc ?? DateTime.UtcNow);
            return left < TimeSpan.Zero ? TimeSpan.Zero : left;
        }

        /// <summary>
        /// Whole time left to ANSWER an offer, floored at zero. The OFFERED ribbon and the offer
        /// modal's fine print both format from this.
        ///
        /// <para>Separate from <see cref="TimeLeft"/> rather than a parameter on it, because the
        /// two read different columns and only one of them exists at a time: an offer has no
        /// <c>ends_at</c> and a running loan has no <c>offer_expires_at</c>. A single method
        /// picking a column off the status would put the predicate split back into one place.</para>
        /// </summary>
        public TimeSpan OfferTimeLeft(DateTime? nowUtc = null)
        {
            DateTime? expires = OfferExpiresAtUtc;
            if (!expires.HasValue) return TimeSpan.Zero;
            TimeSpan left = expires.Value - (nowUtc ?? DateTime.UtcNow);
            return left < TimeSpan.Zero ? TimeSpan.Zero : left;
        }

        /// <summary>Whole hours left to answer, rounded UP and floored at 1 while any time
        /// remains — the ribbon says "46h to answer", and "0h to answer" on an offer that is
        /// still answerable would be a lie the player would act on.</summary>
        public int OfferHoursLeft(DateTime? nowUtc = null)
        {
            TimeSpan left = OfferTimeLeft(nowUtc);
            if (left <= TimeSpan.Zero) return 0;
            return Math.Max(1, (int)Math.Ceiling(left.TotalHours));
        }

        /// <summary>The lender's cut as a whole percent, for <c>LOAN_TERMS_FMT</c>. Never
        /// hardcoded at a call site — the number is the server's and rides on the row.</summary>
        public int SharePercent => LenderShareBp / 100;

        /// <summary>
        /// Public and static so an EditMode test can pin the parse without a transport. Answers
        /// null on anything it cannot read; <c>RoundtripKind</c> keeps the offset the server sent
        /// rather than reinterpreting it in local time.
        /// </summary>
        public static DateTime? ParseUtc(string iso)
        {
            if (string.IsNullOrWhiteSpace(iso)) return null;
            if (!DateTime.TryParse(iso, System.Globalization.CultureInfo.InvariantCulture,
                                   System.Globalization.DateTimeStyles.RoundtripKind,
                                   out DateTime dt))
                return null;
            return dt.Kind == DateTimeKind.Utc ? dt : dt.ToUniversalTime();
        }
    }

    /// <summary>
    /// GET <c>/loans</c> — <c>{out: […], in: […], offers_in: […]}</c>.
    ///
    /// <para>
    /// THREE LISTS, AND THE THIRD IS NOT A LOAN LIST. <c>Out</c> drives the lender's ON LOAN /
    /// OFFERED state and the level+RP catch-up; <c>In</c> drives the borrower's runtime
    /// instances; <c>OffersIn</c> is what has been offered TO this player and not yet answered.
    /// </para>
    /// <para>
    /// The separation is the safety property. A row in <c>In</c> is reconciled into the roster
    /// by <c>EnsureBorrowed</c>; an unanswered offer must never take that path, and keeping it
    /// in a third list means no reconciler branch has to remember to check a status first.
    /// The server splits them, and <c>LoanService.Apply</c> keeps them split.
    /// </para>
    /// </summary>
    public sealed class LoanListDto
    {
        [JsonProperty("out")]       public List<LoanDto> Out;
        [JsonProperty("in")]        public List<LoanDto> In;
        [JsonProperty("offers_in")] public List<LoanDto> OffersIn;
    }

    /// <summary>
    /// POST <c>/loans</c> and POST <c>/loans/{id}/return</c> — <c>{status, loan?}</c>.
    ///
    /// <para>
    /// <see cref="Status"/> IS THE ANSWER, not the HTTP code. Every refusal arrives as a 200 with
    /// one of <see cref="StatusOk"/> … <see cref="StatusLimitIn"/>, and the caller turns it into a
    /// toast. Treating a refusal as a failure would retry a decision that will never change.
    /// </para>
    /// </summary>
    public sealed class LoanMutationDto
    {
        [JsonProperty("status")]   public string  Status;
        [JsonProperty("loan")]     public LoanDto Loan;
        [JsonProperty("replayed")] public bool    Replayed;

        /// <summary>ISO-8601. Set only on <see cref="StatusCooldown"/> — when this lender may
        /// offer this recipient again. Rendered into <c>LOAN_ERR_COOLDOWN_FMT</c>.</summary>
        [JsonProperty("retry_after")] public string RetryAfter;

        public const string StatusOk            = "ok";
        public const string StatusSelf          = "self";
        public const string StatusBadDays       = "bad_days";
        public const string StatusUnknownRef    = "unknown_ref";
        /// <summary>RETIRED by asset_loans_offers (the follow gate is gone). Kept as a constant
        /// so a server that has not been redeployed yet still maps to a real toast rather than
        /// falling through to the generic one.</summary>
        public const string StatusNotFollowing  = "not_following";
        public const string StatusAlreadyOnLoan = "already_on_loan";
        public const string StatusBorrowerHasIt = "borrower_has_it";
        public const string StatusLimitOut      = "limit_out";
        public const string StatusLimitIn       = "limit_in";
        public const string StatusNotBorrower   = "not_borrower";
        public const string StatusNotActive     = "not_active";

        // ── asset_loans_offers §1.3 ───────────────────────────────────────────
        public const string StatusNotAccepting  = "not_accepting";
        public const string StatusPendingLimit  = "pending_limit";
        public const string StatusPendingPair   = "pending_pair";
        public const string StatusCooldown      = "cooldown";
        public const string StatusNotOffered    = "not_offered";
        public const string StatusNotLender     = "not_lender";

        public bool IsOk => string.Equals(Status, StatusOk, StringComparison.Ordinal);

        /// <summary>
        /// The localization key for a refusal, or <c>LOAN_ERR_GENERIC</c> for anything this build
        /// does not recognise — including a status the server grows later. An unknown refusal must
        /// still say SOMETHING; a blank toast reads as "the tap did nothing".
        /// </summary>
        public string ErrorKey()
        {
            switch (Status)
            {
                case StatusNotFollowing:  return "LOAN_ERR_NOT_FOLLOWING";
                case StatusAlreadyOnLoan: return "LOAN_ERR_ALREADY_ON_LOAN";
                case StatusBorrowerHasIt: return "LOAN_ERR_BORROWER_HAS_IT";
                case StatusLimitOut:      return "LOAN_ERR_LIMIT_OUT";
                case StatusLimitIn:       return "LOAN_ERR_LIMIT_IN";
                case StatusNotAccepting:  return "LOAN_ERR_NOT_ACCEPTING";
                case StatusPendingLimit:  return "LOAN_ERR_PENDING_LIMIT";
                case StatusPendingPair:   return "LOAN_ERR_PENDING_PAIR";
                // StatusCooldown is deliberately ABSENT: its toast takes two
                // arguments (who, and how long) and lives in the lend modal,
                // which is the only caller that knows the recipient's name.
                // Falling through to the generic key here would be a blank
                // "{0}" on screen.
                default:                  return "LOAN_ERR_GENERIC";
            }
        }

        /// <summary>
        /// The key for a refusal the RECIPIENT can get while answering an offer. A separate
        /// table from <see cref="ErrorKey"/> because the same server status means different
        /// things to the two parties: <c>limit_in</c> to a lender is "they are full", to the
        /// recipient it is "YOU are full", and one shared mapping would tell one of them the
        /// other one's sentence.
        /// </summary>
        public string AnswerErrorKey()
        {
            switch (Status)
            {
                case StatusNotOffered:    return "LOAN_ERR_OFFER_GONE";
                case StatusNotBorrower:   return "LOAN_ERR_OFFER_GONE";
                case StatusLimitIn:       return "LOAN_ERR_LIMIT_IN_SELF";
                case StatusBorrowerHasIt: return "LOAN_ERR_HAVE_IT";
                default:                  return "LOAN_ERR_GENERIC";
            }
        }

        /// <summary><c>retry_after</c> as UTC, or null. Only meaningful on a cooldown.</summary>
        public System.DateTime? RetryAfterUtc => LoanDto.ParseUtc(RetryAfter);
    }

    /// <summary>
    /// One row of GET <c>/social/{id}/following</c>. The router selects
    /// <c>following_id, created_at, profiles!followers_following_id_fkey(...)</c>, so the profile
    /// arrives nested under <c>profiles</c> — the same embed shape <c>ReceivedGiftDto</c> handles.
    /// </summary>
    public sealed class FollowedUserDto
    {
        [JsonProperty("following_id")] public string FollowingId;
        [JsonProperty("created_at")]   public string CreatedAt;
        [JsonProperty("profiles")]     public LoanPartyDto Profile;

        // ── asset_loans_offers §2 — the SECOND wire shape ─────────────────────
        //
        // `GET /user/search` returns a bare `profiles` row: id / display_name /
        // avatar_url / avatar_level at the TOP LEVEL, with no `following_id` and
        // no embed. `GET /social/{id}/following` nests exactly the same fields
        // under `profiles`.
        //
        // ONE DTO WITH TWO MAPPINGS rather than two DTOs, because the lend modal
        // holds ONE selection across both sections and one list of rows: a second
        // type would mean `LoanRecipientRow.Bind` taking an interface, or an
        // adapter, to express "these are the same four fields spelled two ways".
        // Newtonsoft fills whichever set the payload actually carries and leaves
        // the other null; the accessors below prefer the embed and fall back.
        [JsonProperty("id")]           public string FlatId;
        [JsonProperty("display_name")] public string FlatDisplayName;
        [JsonProperty("avatar_url")]   public string FlatAvatarUrl;
        [JsonProperty("avatar_level")] public int?   FlatAvatarLevel;

        public string Id
        {
            get
            {
                if (Profile != null && !string.IsNullOrEmpty(Profile.Id)) return Profile.Id;
                if (!string.IsNullOrEmpty(FollowingId)) return FollowingId;
                return FlatId;
            }
        }

        public string DisplayName
        {
            get
            {
                if (Profile != null && !string.IsNullOrWhiteSpace(Profile.DisplayName))
                    return Profile.DisplayName;
                return string.IsNullOrWhiteSpace(FlatDisplayName) ? "PLAYER" : FlatDisplayName;
            }
        }

        public string AvatarUrl => Profile != null && Profile.AvatarUrl != null
                                        ? Profile.AvatarUrl : FlatAvatarUrl;

        public int AvatarLevel
        {
            get
            {
                if (Profile != null && Profile.AvatarLevel > 0) return Profile.AvatarLevel;
                return FlatAvatarLevel.HasValue && FlatAvatarLevel.Value > 0
                     ? FlatAvatarLevel.Value : 1;
            }
        }
    }
}
