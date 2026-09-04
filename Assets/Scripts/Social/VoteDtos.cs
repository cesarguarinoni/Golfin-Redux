// gps_gifts_votes §Client data bindings — the vote wire shapes, from voting.py::_format_vote
// and verified against the live `votes` / `vote_options` rows (2026-09-02).
using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;

namespace Golfin.Social
{
    /// <summary>
    /// One vote, as <c>/vote/list</c>, <c>/vote/create</c> and <c>/vote/{id}/cast</c> all return
    /// it. The router pops the PostgREST embed <c>vote_options</c> and re-emits it as
    /// <c>options</c> (voting.py <c>_format_vote</c>), so the key here is <c>options</c>.
    /// </summary>
    public sealed class VoteDto
    {
        [JsonProperty("id")]           public string Id;
        [JsonProperty("creator_id")]   public string CreatorId;
        [JsonProperty("creator_name")] public string CreatorName;
        [JsonProperty("question")]     public string Question;
        /// <summary><c>yesNo</c> for the two-option form; anything else renders as the multi-pill
        /// card. Not an enum on purpose — the server accepts a free string.</summary>
        [JsonProperty("vote_type")]    public string VoteType;
        [JsonProperty("total_votes")]  public int TotalVotes;
        [JsonProperty("status")]       public string Status;
        /// <summary>ISO-8601, or null for a vote that never expires.</summary>
        [JsonProperty("expires_at")]   public string ExpiresAt;
        /// <summary>
        /// The sponsored prize pool. Present in the schema and ZERO on every live row — nothing
        /// writes it (verified over all five active votes, 2026-09-02). The Figma frame's
        /// "500 pts / 2,000 pts" pill is a mockup of this concept; v1 deliberately renders the
        /// real cast reward instead. See SPEC § Reference.
        /// </summary>
        [JsonProperty("sponsor_pool")] public int SponsorPool;
        [JsonProperty("created_at")]   public string CreatedAt;
        [JsonProperty("options")]      public List<VoteOptionDto> Options;

        /// <summary>True when the server calls this a Yes/No vote AND it really has two options —
        /// a <c>yesNo</c> row with three options would otherwise render two bars and silently drop
        /// the third.</summary>
        public bool IsYesNo =>
            Options != null && Options.Count == 2;

        /// <summary>
        /// The YES option, matched BY LABEL — never by index.
        ///
        /// <para>
        /// ⚠️ THE SERVER'S OPTION ORDER IS NOT STABLE. <c>voting.py::_format_vote</c> re-emits a
        /// PostgREST embed (<c>vote_options(*)</c>) with no <c>order</c> clause, so the array comes
        /// back in whatever order Postgres produced. Observed on ONE vote within minutes:
        /// <c>[Yes, No]</c> during a play-mode run and <c>[No, Yes]</c> from the next request. So
        /// binding <c>Options[0]</c> to the bar labelled YES puts the wrong count under the wrong
        /// label, and casting <c>Options[0]</c> casts the wrong way — both intermittently, which is
        /// the worst kind.
        /// </para>
        /// <para>
        /// Matching covers what every writer produces: the seeded rows say <c>Yes</c> / <c>No</c>,
        /// and this build's CREATE modal sends the LOCALIZED labels, so a vote created in Japanese
        /// says <c>はい</c> / <c>いいえ</c>. An unmatched vote falls back to index order, which is
        /// no worse than before and keeps a non-Yes/No poll rendering.
        /// </para>
        /// </summary>
        public VoteOptionDto YesOption => Match(true);

        /// <summary>The NO option, by the same rule as <see cref="YesOption"/>.</summary>
        public VoteOptionDto NoOption => Match(false);

        private VoteOptionDto Match(bool wantYes)
        {
            if (Options == null || Options.Count == 0) return null;
            foreach (VoteOptionDto o in Options)
                if (o != null && IsYesLabel(o.Label) == wantYes && IsKnownLabel(o.Label))
                    return o;
            // No recognisable pair — fall back to the array's own order.
            int i = wantYes ? 0 : 1;
            return i < Options.Count ? Options[i] : null;
        }

        /// <summary>Public so an EditMode test can pin the vocabulary without a vote.</summary>
        public static bool IsYesLabel(string label)
        {
            string l = (label ?? string.Empty).Trim();
            return l.Equals("yes", StringComparison.OrdinalIgnoreCase) || l == "はい";
        }

        public static bool IsNoLabel(string label)
        {
            string l = (label ?? string.Empty).Trim();
            return l.Equals("no", StringComparison.OrdinalIgnoreCase) || l == "いいえ";
        }

        private static bool IsKnownLabel(string label) => IsYesLabel(label) || IsNoLabel(label);

        /// <summary>
        /// Whole days from now until <see cref="ExpiresAt"/>, or null when there is no expiry or
        /// it does not parse. NEGATIVE is possible and is left as-is: an active row whose expiry
        /// has passed is a real state (nothing sweeps them), and the caller renders it as 0.
        /// </summary>
        public int? DaysLeft(DateTime utcNow)
        {
            if (string.IsNullOrWhiteSpace(ExpiresAt)) return null;
            if (!DateTime.TryParse(ExpiresAt, CultureInfo.InvariantCulture,
                                   DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                                   out DateTime when))
                return null;
            return (int)Math.Ceiling((when - utcNow).TotalDays);
        }
    }

    /// <summary>One option of a vote. <c>percentage</c> is SERVER-computed and rounded to one
    /// decimal (voting.py <c>_update_percentages</c>), so the client never divides.</summary>
    public sealed class VoteOptionDto
    {
        [JsonProperty("id")]         public string Id;
        [JsonProperty("label")]      public string Label;
        [JsonProperty("vote_count")] public int VoteCount;
        [JsonProperty("percentage")] public float Percentage;
    }
}
