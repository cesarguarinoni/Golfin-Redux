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

        public string Name => string.IsNullOrWhiteSpace(DisplayName) ? "PLAYER" : DisplayName;
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
        /// <summary><c>active</c> | <c>returned</c> | <c>expired</c>.</summary>
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
            if (!string.Equals(Status, "active", StringComparison.Ordinal)) return false;
            DateTime? ends = EndsAtUtc;
            if (!ends.HasValue) return true;
            return (nowUtc ?? DateTime.UtcNow) < ends.Value;
        }

        /// <summary>Whole hours left, floored at zero. The ribbon formats from this.</summary>
        public TimeSpan TimeLeft(DateTime? nowUtc = null)
        {
            DateTime? ends = EndsAtUtc;
            if (!ends.HasValue) return TimeSpan.Zero;
            TimeSpan left = ends.Value - (nowUtc ?? DateTime.UtcNow);
            return left < TimeSpan.Zero ? TimeSpan.Zero : left;
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

    /// <summary>GET <c>/loans</c> — <c>{out: […], in: […]}</c>. Both sides in one payload because
    /// both clients need both: <c>Out</c> drives the lender's ON LOAN state and the level/RP catch
    /// up, <c>In</c> drives the borrower's runtime instances.</summary>
    public sealed class LoanListDto
    {
        [JsonProperty("out")] public List<LoanDto> Out;
        [JsonProperty("in")]  public List<LoanDto> In;
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

        public const string StatusOk            = "ok";
        public const string StatusSelf          = "self";
        public const string StatusBadDays       = "bad_days";
        public const string StatusUnknownRef    = "unknown_ref";
        public const string StatusNotFollowing  = "not_following";
        public const string StatusAlreadyOnLoan = "already_on_loan";
        public const string StatusBorrowerHasIt = "borrower_has_it";
        public const string StatusLimitOut      = "limit_out";
        public const string StatusLimitIn       = "limit_in";
        public const string StatusNotBorrower   = "not_borrower";
        public const string StatusNotActive     = "not_active";

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
                default:                  return "LOAN_ERR_GENERIC";
            }
        }
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

        public string Id          => Profile != null && !string.IsNullOrEmpty(Profile.Id)
                                        ? Profile.Id : FollowingId;
        public string DisplayName => Profile != null ? Profile.Name : "PLAYER";
        public string AvatarUrl   => Profile != null ? Profile.AvatarUrl : null;
        public int    AvatarLevel => Profile != null ? Profile.AvatarLevel : 1;
    }
}
