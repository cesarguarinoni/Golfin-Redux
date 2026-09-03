// Order: score_upload_flow §1 — payload shapes for /recognition/analyze and /score/submit,
// transcribed from the deployed PLAYLIFE routers (recognition.py, score.py), not guessed.
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Golfin.Gps
{
    /// <summary>
    /// <c>POST /api/v1/recognition/analyze</c> → <c>{data:{id, sport_type, extracted_data,
    /// confidence, recognized_at, user_id, image_url?, raw_response?}}</c> (recognition.py:30-38,
    /// :405-414).
    ///
    /// <para>
    /// <see cref="ExtractedData"/> stays a raw <see cref="JObject"/> on purpose: the router serves
    /// SIX sports off one endpoint and each writes a different shape (golf's five keys, bowling's
    /// frame array, running's pace string…). Typing it here would either need six classes or one
    /// class that is wrong for five of them. The golf view is <see cref="GolfExtraction"/>, built
    /// by <see cref="GolfExtraction.From"/> off this token.
    /// </para>
    /// </summary>
    public sealed class RecognitionResult
    {
        [JsonProperty("id")]             public string Id;
        [JsonProperty("sport_type")]     public string SportType;
        [JsonProperty("extracted_data")] public JObject ExtractedData;
        [JsonProperty("confidence")]     public double Confidence;
        [JsonProperty("recognized_at")]  public string RecognizedAt;
        [JsonProperty("user_id")]        public string UserId;
        [JsonProperty("image_url")]      public string ImageUrl;
        [JsonProperty("raw_response")]   public string RawResponse;

        /// <summary>The golf view of <see cref="ExtractedData"/>. Never null; every field of it is.</summary>
        public GolfExtraction Golf() => GolfExtraction.From(ExtractedData);

        public override string ToString()
            => $"RecognitionResult {Id} {SportType} conf={Confidence:F2}";
    }

    /// <summary>
    /// The five keys <c>RECOGNITION_SYSTEM_PROMPT</c> "## golf" asks the model for
    /// (recognition.py:73-78), each nullable because the model is instructed to return null for
    /// anything it cannot read off the card.
    ///
    /// <para>
    /// ⚠️ THERE ARE NO PER-HOLE SCORES AND NO PUTTS HERE, and that is the backend's shape, not an
    /// omission: the prompt asks for a TOTAL. Every "OUT / IN / PUTTS" cell the Figma frames show
    /// after an AI read renders an em dash for exactly this reason (SPEC § Figma Fidelity), and the
    /// player fills the holes in by hand on step 3 if they want a breakdown.
    /// </para>
    /// </summary>
    public sealed class GolfExtraction
    {
        public int? Score;
        public string Course;
        public int? Holes;
        public string Date;
        public int? Par;

        /// <summary>
        /// Per-hole par, when the model returns one. RECOGNITION_SYSTEM_PROMPT's "## golf" asks for
        /// five keys and this is not among them, so it is empty in v1 — but a scorecard photo
        /// physically contains the row, so a future prompt could return it, and the Edit step's
        /// score colouring is already wired to read it.
        /// </summary>
        public int?[] Pars;

        /// <summary>
        /// Reads whichever of the five keys are present. A key that is absent, JSON-null, or of the
        /// wrong type leaves its field null rather than throwing — a partial read is the NORMAL
        /// outcome for a blurry card and must still reach step 3.
        /// </summary>
        public static GolfExtraction From(JObject extracted)
        {
            var g = new GolfExtraction();
            if (extracted == null) return g;

            g.Score  = AsInt(extracted["score"]);
            g.Holes  = AsInt(extracted["holes"]);
            g.Par    = AsInt(extracted["par"]);
            g.Course = AsString(extracted["course"]);
            g.Date   = AsString(extracted["date"]);
            g.Pars   = AsIntArray(extracted["hole_pars"] ?? extracted["pars"]);
            return g;
        }

        /// <summary>A JSON array of pars, each element as tolerant as <see cref="AsInt"/>. Anything
        /// that is not an array reads as null rather than throwing — a partial or absent breakdown
        /// is the normal outcome and must still reach step 3.</summary>
        private static int?[] AsIntArray(JToken t)
        {
            if (!(t is JArray arr) || arr.Count == 0) return null;
            var outp = new int?[arr.Count];
            for (int i = 0; i < arr.Count; i++) outp[i] = AsInt(arr[i]);
            return outp;
        }

        /// <summary>Accepts a JSON number OR a numeric string — Vision occasionally quotes an
        /// integer, and a quoted 92 is still a 92.</summary>
        private static int? AsInt(JToken t)
        {
            if (t == null || t.Type == JTokenType.Null) return null;
            if (t.Type == JTokenType.Integer) return t.Value<int>();
            if (t.Type == JTokenType.Float) return (int)t.Value<double>();
            if (t.Type == JTokenType.String &&
                int.TryParse(t.Value<string>(), out int parsed)) return parsed;
            return null;
        }

        private static string AsString(JToken t)
        {
            if (t == null || t.Type == JTokenType.Null) return null;
            string s = t.Type == JTokenType.String ? t.Value<string>() : t.ToString();
            return string.IsNullOrWhiteSpace(s) ? null : s;
        }

        public override string ToString()
            => $"GolfExtraction(score={Score}, course={Course}, holes={Holes}, date={Date}, par={Par})";
    }

    /// <summary>One hole of the optional per-hole breakdown (<c>ScorePostRequest.holes</c>,
    /// score.py:130). <see cref="Score"/> is nullable so an un-edited hole can be OMITTED from the
    /// list rather than posted as a zero.</summary>
    public sealed class HoleScore
    {
        public int Hole;
        public int? Score;

        public HoleScore(int hole, int? score)
        {
            Hole = hole;
            Score = score;
        }
    }

    /// <summary>
    /// The NON-GPS half of <c>ScorePostRequest</c> (score.py:117-140). The GPS half is
    /// <see cref="GpsScoreAttachment.ToJson"/> and the two are merged by
    /// <see cref="ScoreService.Submit"/> — which is the whole reason this type does not carry
    /// <c>gps_verified</c>, <c>latitude</c>, <c>gps_check_count</c> or their siblings: two owners
    /// for one key is how a client ends up claiming a fix it does not have.
    ///
    /// <para>
    /// Fields the server defaults and this client never overrides are absent by design:
    /// <c>create_vote</c> (votes are v3), <c>vote_question</c>, <c>vote_pts</c>,
    /// <c>photo_url</c> (no upload bucket yet). Sending a default would freeze a server knob into
    /// a shipped build.
    /// </para>
    /// </summary>
    public sealed class ScoreSubmitRequest
    {
        /// <summary>Total strokes. Server-validated to 50–200 ("18") / 25–100 ("9").</summary>
        public int Score;

        /// <summary>"18" or "9" — free text on the server, but only those two are meaningful.</summary>
        public string ScoreType = "18";

        /// <summary>REQUIRED by the server (no pydantic default). Empty string when the player never
        /// named a course, which is legal and posts an unnamed round.</summary>
        public string CourseName = string.Empty;

        /// <summary>"screenshot" (photo/library → AI) or "manual" (typed). Drives the server's
        /// points and Trust base (50/30 vs 20/30).</summary>
        public string InputMethod = "manual";

        /// <summary>Null or empty ⇒ the key is omitted and the server keeps only the total.</summary>
        public List<HoleScore> Holes;

        /// <summary>The recognition's <c>extracted_data</c> plus <c>recognition_id</c>, kept so a
        /// disputed post can be re-read against what the AI actually said. Null on the manual path.</summary>
        public JObject ScreenshotData;

        public string Visibility = "public";

        /// <summary>
        /// gps_checkin §A5 — the live round this score belongs to.
        ///
        /// <para>When it is the caller's own <c>active</c> row the server UPDATES that row instead
        /// of inserting a second activity, so a round the player checked into and then posted a
        /// score for is ONE row in history, not two (D6). Null on every other path, including a
        /// score posted with no round open, and the server falls back to the historical insert.
        /// </para>
        /// </summary>
        public long? ActivityId;

        /// <summary>The non-GPS body. <see cref="ScoreService.Submit"/> merges the attachment over
        /// this, so any key present in both resolves to the ATTACHMENT's value.</summary>
        public JObject ToJson()
        {
            var o = new JObject
            {
                ["score"] = Score,
                ["score_type"] = string.IsNullOrEmpty(ScoreType) ? "18" : ScoreType,
                ["course_name"] = CourseName ?? string.Empty,
                ["input_method"] = string.IsNullOrEmpty(InputMethod) ? "manual" : InputMethod,
                ["visibility"] = string.IsNullOrEmpty(Visibility) ? "public" : Visibility
            };

            if (Holes != null && Holes.Count > 0)
            {
                var arr = new JArray();
                foreach (HoleScore h in Holes)
                {
                    if (h == null || !h.Score.HasValue) continue;
                    arr.Add(new JObject { ["hole"] = h.Hole, ["score"] = h.Score.Value });
                }
                if (arr.Count > 0) o["holes"] = arr;
            }

            if (ScreenshotData != null) o["screenshot_data"] = ScreenshotData;

            // Omitted rather than sent as null: the field is Optional[int] on the server and an
            // explicit null costs a key on every score post that is not closing a round, which is
            // most of them.
            if (ActivityId.HasValue) o["activity_id"] = ActivityId.Value;

            return o;
        }
    }

    /// <summary>
    /// <c>POST /api/v1/score/submit</c> → <c>{data:{activity, points_earned, trust, gps_verified,
    /// gps_distance_m, avatar_level, leveled_up, vote, newly_earned_badges, referral_reward,
    /// tournaments_affected}}</c> (score.py:337-356).
    ///
    /// <para>
    /// <see cref="Trust"/> and <see cref="PointsEarned"/> are the SERVER's numbers and the Posted
    /// step renders these, never the client-side estimate the Confirm step shows — the estimate
    /// cannot know about the rate-limit Trust penalty, the mock penalty, or a badge.
    /// </para>
    /// </summary>
    public sealed class ScoreSubmitResult
    {
        [JsonProperty("activity")]            public ActivityDto Activity;
        [JsonProperty("points_earned")]       public int PointsEarned;
        [JsonProperty("trust")]               public int Trust;
        [JsonProperty("gps_verified")]        public bool GpsVerified;
        [JsonProperty("gps_distance_m")]      public double? GpsDistanceM;
        [JsonProperty("avatar_level")]        public int? AvatarLevel;
        [JsonProperty("leveled_up")]          public bool LeveledUp;
        [JsonProperty("newly_earned_badges")] public JArray NewlyEarnedBadges;

        /// <summary>gps_checkin §A5 — the live round this post CLOSED, or null when it opened a
        /// standalone history row. The Rounds screen reads it to drop its live card without
        /// waiting for the next <c>/activity/active</c>.</summary>
        [JsonProperty("closed_activity_id")]  public long? ClosedActivityId;

        public override string ToString()
            => $"ScoreSubmitResult(+{PointsEarned} pts, trust={Trust}, gps={GpsVerified})";
    }
}
