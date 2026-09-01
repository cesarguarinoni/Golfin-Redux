// Order: score_upload_flow §1 — POST /score/submit: the request half + the GPS half, merged once.
using System;
using System.Collections;
using Golfin.Net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Golfin.Gps
{
    /// <summary>
    /// The score-posting half of the GPS module (SPEC §1). Same plain-C#-singleton shape as
    /// <see cref="VenueService"/> / <see cref="ScoreHistoryService"/>.
    ///
    /// <para>
    /// ONE JOB THAT IS NOT A PASS-THROUGH: the body is assembled from TWO owners. The screen owns
    /// the score half (<see cref="ScoreSubmitRequest"/>) and <see cref="GpsScoreAttachment"/> owns
    /// the location half, and this class merges the second OVER the first. The direction matters:
    /// if the screen and the attachment ever disagree about <c>venue_id</c> — which they do the
    /// moment a player picks a course by hand — the value that goes up must be the one whose
    /// coordinates are also going up, or the server verifies a fix against a venue it was never
    /// near.
    /// </para>
    /// <para>
    /// THE IN-FLIGHT LATCH IS HERE, NOT ONLY ON THE BUTTON. A disabled button is a UI state and UI
    /// states get lost — a lost tap, a re-enable on an error path, a second controller. A double
    /// post is not cosmetic: it is two <c>activities</c> rows, two points payouts, and one step
    /// closer to the 10-per-24h hard limit. The button is latched too; this is the floor.
    /// </para>
    /// </summary>
    public sealed class ScoreService
    {
        private const string Tag = "[ScoreService]";

        private static ScoreService _instance;

        public static ScoreService Instance =>
            _instance ?? (_instance = new ScoreService(ApiClient.Instance));

        public static void ConfigureForTest(ScoreService service) => _instance = service;

        public static void ResetForTest() => _instance = null;

        // ── localization keys the caller renders for a failed post ────────────────

        /// <summary>400 — the server rejected the total against SCORE_BOUNDS_18/9. The server's own
        /// <c>detail</c> (Japanese, and specific) is appended to the localized prefix.</summary>
        public const string ErrScoreRangeKey = "SU_ERR_SCORE_RANGE";

        /// <summary>429 — 10 posts in 24 h (RATE_LIMIT_24H_HARD).</summary>
        public const string ErrRateLimitKey = "SU_ERR_RATE_LIMIT";

        public const string ErrGenericKey = "SU_ERR_GENERIC";

        private readonly ApiClient _client;

        public ScoreService(ApiClient client)
        {
            _client = client;
        }

        /// <summary>True between the first <see cref="Submit"/> and its callback. The Confirm step
        /// mirrors this onto the POST button.</summary>
        public bool IsSubmitting { get; private set; }

        /// <summary>The body of the last request actually sent, kept for the report/diagnostics.</summary>
        public string LastSentBody { get; private set; }

        /// <summary>
        /// <c>POST /score/submit</c> — the request merged with the GPS attachment.
        ///
        /// <para>
        /// A SECOND CALL WHILE ONE IS PENDING SENDS NOTHING. It reports
        /// <see cref="ApiErrorKind.Disabled"/> ("the call was never made"), which the Confirm step
        /// treats as a no-op: no error strip, no re-enable, because the first post is still on its
        /// way and its answer is the one that matters.
        /// </para>
        /// </summary>
        public IEnumerator Submit(ScoreSubmitRequest req,
                                  GpsScoreAttachment gps,
                                  Action<ApiResult<ScoreSubmitResult>> onResult)
        {
            if (req == null)
            {
                onResult?.Invoke(ApiResult<ScoreSubmitResult>.Fail(ApiErrorKind.NotConfigured,
                    "No score to submit.", 0, null, 0));
                yield break;
            }

            if (IsSubmitting)
            {
                Debug.LogWarning($"{Tag} a submit is already in flight — the duplicate was dropped.");
                onResult?.Invoke(ApiResult<ScoreSubmitResult>.Fail(ApiErrorKind.Disabled,
                    "A score post is already in flight.", 0, null, 0));
                yield break;
            }

            JObject body = req.ToJson();
            if (gps != null) body.Merge(gps.ToJson(), MergeOverwrite);

            LastSentBody = body.ToString(Formatting.None);
            IsSubmitting = true;

            ApiResult<ScoreSubmitResult> result = null;
            try
            {
                IEnumerator call = _client.Post<ScoreSubmitResult>(Endpoints.ScoreSubmit, LastSentBody,
                                                                   r => result = r);
                while (call.MoveNext()) yield return call.Current;
            }
            finally
            {
                // finally, not after the loop: a caller that stops pumping (screen closed mid-post)
                // must not leave the latch stuck on for the rest of the session.
                IsSubmitting = false;
            }

            onResult?.Invoke(result ?? ApiResult<ScoreSubmitResult>.Fail(ApiErrorKind.Network,
                "The score post produced no response.", 0, null, 1));
        }

        /// <summary>Attachment keys REPLACE request keys — see the class remarks.</summary>
        private static readonly JsonMergeSettings MergeOverwrite = new JsonMergeSettings
        {
            MergeArrayHandling = MergeArrayHandling.Replace,
            MergeNullValueHandling = MergeNullValueHandling.Merge
        };

        // ── error mapping ─────────────────────────────────────────────────────────

        /// <summary>
        /// The localization key for a failed post (SPEC §1). 400 is the score-bounds rejection and
        /// 429 the hard rate limit; everything else is generic, deliberately — a player cannot act
        /// on "502" and the log line already carries it.
        /// </summary>
        public static string ErrorKeyFor(ApiResult<ScoreSubmitResult> result)
        {
            if (result == null) return ErrGenericKey;
            if (result.StatusCode == 400) return ErrScoreRangeKey;
            if (result.StatusCode == 429) return ErrRateLimitKey;
            return ErrGenericKey;
        }

        /// <summary>
        /// The localized message plus, on a 400, the server's own <c>detail</c> — which names the
        /// legal range in Japanese and is the only part of the message that tells the player what to
        /// change.
        /// </summary>
        public static string ErrorMessageFor(ApiResult<ScoreSubmitResult> result, Func<string, string> localize)
        {
            string key = ErrorKeyFor(result);
            string text = localize != null ? localize(key) : key;

            if (result != null && result.StatusCode == 400 && !string.IsNullOrWhiteSpace(result.ErrorMessage))
                text = text + " " + result.ErrorMessage;

            return text;
        }
    }
}
