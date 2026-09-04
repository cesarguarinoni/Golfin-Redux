// gps_gifts_votes §Client data bindings — /vote/* over the EXISTING ApiClient.
using System;
using System.Collections;
using System.Collections.Generic;
using Golfin.Net;
using Newtonsoft.Json;

namespace Golfin.Social
{
    /// <summary>
    /// The vote feed and the two writes the Vote screen makes: cast and create.
    ///
    /// <para>
    /// ALREADY-VOTED IS A STATE, NOT AN ERROR. <c>/vote/{id}/cast</c> answers <c>400 "Already
    /// voted"</c> when a <c>user_votes</c> row exists, and that is the ONLY way this build can
    /// learn what the player has already voted on — there is no "my votes" endpoint and
    /// <c>/vote/list</c> carries no per-caller flag. So the screen finds out lazily, and
    /// <see cref="VotedLocally"/> remembers the answer for the session so the card does not offer
    /// a button that is guaranteed to fail. Cross-session it re-learns, which costs one refused
    /// request per vote per session and is why that request is treated as information rather than
    /// as a failure.
    /// </para>
    /// </summary>
    public sealed class VoteService
    {
        private static VoteService _instance;

        public static VoteService Instance =>
            _instance ?? (_instance = new VoteService(ApiClient.Instance));

        public static void ConfigureForTest(VoteService service) => _instance = service;
        public static void ResetForTest() => _instance = null;

        private readonly ApiClient _client;
        private readonly HashSet<string> _voted = new HashSet<string>(StringComparer.Ordinal);

        public VoteService(ApiClient client) { _client = client; }

        /// <summary>The last list the server returned this session, or null.</summary>
        public List<VoteDto> LastVotes { get; private set; }

        public event Action OnVotesChanged;

        /// <summary>Vote ids this session has seen a successful — or a refused-as-duplicate —
        /// cast for.</summary>
        public bool VotedLocally(string voteId)
            => !string.IsNullOrEmpty(voteId) && _voted.Contains(voteId);

        /// <summary>Remember a cast. Public so a test (and the already-voted branch) can seed it.</summary>
        public void MarkVoted(string voteId)
        {
            if (!string.IsNullOrEmpty(voteId)) _voted.Add(voteId);
        }

        public void ClearVotedForTest() => _voted.Clear();

        // ═════════════════════════════════════════════════════════════════════
        // Reads
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>GET <c>/vote/list</c> — active votes, newest first.</summary>
        public IEnumerator List(int skip, int limit, Action<ApiResult<List<VoteDto>>> onResult = null)
            => _client.Get<List<VoteDto>>(Endpoints.VoteList(skip, limit), r =>
            {
                if (r != null && r.Success && r.Data != null)
                {
                    LastVotes = r.Data;
                    OnVotesChanged?.Invoke();
                }
                onResult?.Invoke(r);
            });

        // ═════════════════════════════════════════════════════════════════════
        // Writes
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>
        /// POST <c>/vote/{id}/cast</c>. The result carries the REPAINTED vote (server-recomputed
        /// counts and percentages), so the caller rebinds from the response rather than
        /// incrementing anything locally.
        ///
        /// <para>
        /// A <c>400</c> whose body says the caller already voted is turned into
        /// <see cref="MarkVoted"/> and handed to the callback as a FAILED result — the caller
        /// branches on <see cref="AlreadyVoted"/> to tell that apart from a real failure.
        /// </para>
        /// </summary>
        public IEnumerator Cast(string voteId, string optionId, Action<ApiResult<VoteDto>> onResult)
            => _client.Post<VoteDto>(Endpoints.VoteCast(voteId), BuildCastJson(optionId), r =>
            {
                if (r != null && (r.Success || AlreadyVoted(r))) MarkVoted(voteId);
                onResult?.Invoke(r);
            });

        /// <summary>
        /// Does this failure mean "you have already voted on this" rather than "the call broke"?
        ///
        /// <para>
        /// The router raises <c>HTTPException(400, "Already voted")</c>, so the signal is the
        /// status plus the detail string. Matching on the status ALONE would swallow every other
        /// 400 the endpoint can produce (a missing option_id is a 422, but a bad one currently
        /// surfaces as a 500 from the <c>.single()</c> lookup), and matching on the string alone
        /// would be fooled by a 500 whose body happened to contain it.
        /// </para>
        /// </summary>
        public static bool AlreadyVoted(ApiResult<VoteDto> result)
            => result != null
               && !result.Success
               && result.StatusCode == 400
               && (result.RawBody ?? "").IndexOf("Already voted", StringComparison.OrdinalIgnoreCase) >= 0;

        /// <summary>
        /// POST <c>/vote/create</c>. v1 always sends exactly two options, YES and NO, in that
        /// order — the CREATE modal offers no other shape (SPEC § Goal).
        /// </summary>
        public IEnumerator Create(string question, IList<string> options, string expiresAtIso,
                                  Action<ApiResult<VoteDto>> onResult)
            => _client.Post(Endpoints.VoteCreate,
                            BuildCreateJson(question, options, expiresAtIso), onResult);

        /// <summary>Public so an EditMode test can pin the wire shape. Mirrors
        /// <c>voting.py::CastVoteRequest</c>.</summary>
        public static string BuildCastJson(string optionId)
            => JsonConvert.SerializeObject(new CastBody { option_id = optionId });

        /// <summary>Mirrors <c>voting.py::CreateVoteRequest</c>. <c>expires_at</c> is omitted
        /// rather than sent as null when there is none, so the column keeps its own default.</summary>
        public static string BuildCreateJson(string question, IList<string> options, string expiresAtIso)
            => JsonConvert.SerializeObject(
                new CreateBody
                {
                    question   = question,
                    vote_type  = "yesNo",
                    options    = options != null ? new List<string>(options) : new List<string>(),
                    expires_at = string.IsNullOrEmpty(expiresAtIso) ? null : expiresAtIso,
                },
                new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });

        private sealed class CastBody { public string option_id; }

        private sealed class CreateBody
        {
            public string       question;
            public string       vote_type;
            public List<string> options;
            public string       expires_at;
        }
    }
}
