// missions_v1 §B4 — the claim path. Payload shapes transcribed from the deployed
// backend/routers/missions.py, not guessed.
using System;
using System.Collections;
using Golfin.Net;
using Newtonsoft.Json;
using UnityEngine;

namespace Golfin.Economy
{
    /// <summary>
    /// <c>POST /api/v1/missions/claim</c> → <c>{data: &lt;golfin_mission_claim result&gt;}</c>.
    ///
    /// EVERY BUSINESS OUTCOME IS HTTP 200 and arrives as a <see cref="Status"/> the client
    /// branches on — the same contract as <c>/points/spend</c>'s "insufficient". A client that
    /// read a refusal as a transport failure would retry it forever; one that read a transport
    /// failure as a refusal would tell the player their clear did not count when it did.
    /// </summary>
    public sealed class MissionClaimResult
    {
        /// <summary>ok | attempt | inactive | unknown_mission | replayed.</summary>
        [JsonProperty("status")] public string Status;

        [JsonProperty("mission_id")]  public string MissionId;

        /// <summary>Total credited — the mission's RP plus any tier bonus.</summary>
        [JsonProperty("awarded")]     public int Awarded;
        [JsonProperty("mission_rp")]  public int MissionRp;
        [JsonProperty("tier_bonus")]  public int TierBonus;
        [JsonProperty("tier")]        public string Tier;

        /// <summary>True when this was the FIRST clear, so the card can say so.</summary>
        [JsonProperty("first_clear")] public bool FirstClear;

        [JsonProperty("clears")]       public int Clears;
        [JsonProperty("attempts")]     public int Attempts;
        [JsonProperty("best_strokes")] public int? BestStrokes;

        /// <summary>Set on `replayed` — the ORIGINAL result, returned verbatim.</summary>
        [JsonProperty("result")] public MissionClaimResult Result;

        [JsonIgnore] public bool Paid => Awarded > 0;

        /// <summary>
        /// The result that actually describes what happened. A replay wraps the original, so
        /// unwrapping here means no caller has to remember to.
        /// </summary>
        [JsonIgnore]
        public MissionClaimResult Effective
            => string.Equals(Status, "replayed", StringComparison.Ordinal) && Result != null ? Result : this;
    }

    /// <summary>
    /// <c>GET /api/v1/missions/daily</c> → today's UTC recipe.
    ///
    /// The recipe is COMPOSED SERVER-SIDE and frozen for the day: once a date's row exists the
    /// server never regenerates it, because a recipe that changed under a player mid-round
    /// would fail their claim's `recipe_hash` check and pay them nothing.
    /// </summary>
    public sealed class DailyMissionResult
    {
        [JsonProperty("date")]        public string Date;
        [JsonProperty("recipe")]      public DailyRecipe Recipe;
        [JsonProperty("recipe_hash")] public string RecipeHash;
        [JsonProperty("pinned")]      public bool Pinned;
        [JsonProperty("claimed")]     public bool Claimed;
        [JsonProperty("claimed_rp")]  public int ClaimedRp;
        [JsonProperty("streak")]      public int Streak;
    }

    /// <summary>One generated daily, as `services/daily_mission.py` composes it.</summary>
    public sealed class DailyRecipe
    {
        [JsonProperty("holeId")]          public int HoleId;
        [JsonProperty("par")]             public int Par;
        [JsonProperty("courseId")]        public string CourseId;
        [JsonProperty("startAreaId")]     public string StartAreaId;
        [JsonProperty("startKind")]       public string StartKind;
        [JsonProperty("windPresetId")]    public string WindPresetId;
        [JsonProperty("loadoutId")]       public string LoadoutId;
        [JsonProperty("goals")]           public DailyGoal[] Goals;
        [JsonProperty("modifier")]        public string Modifier;
        [JsonProperty("band")]            public string Band;
        [JsonProperty("difficultyScore")] public int DifficultyScore;
        [JsonProperty("pinIndex")]        public int PinIndex;
        [JsonProperty("staminaDrain")]    public float StaminaDrain;
    }

    public sealed class DailyGoal
    {
        [JsonProperty("type")]  public string Type;
        [JsonProperty("param")] public string Param;
    }

    /// <summary>
    /// The mission claim, and nothing else.
    ///
    /// ⚠️ IT TAKES PRIMITIVES, NOT A `MissionResult`, AND THAT IS ON PURPOSE. Accepting the
    /// gameplay type would make `Golfin.Economy` depend on `Golfin.Gameplay.Missions` for the
    /// sake of four fields, and the assembly graph is the thing keeping the Hole Complete modal
    /// able to reference Missions at all. The caller unpacks; this posts.
    ///
    /// ⚠️ NO OFFLINE QUEUE, DELIBERATELY, AND IT IS THE OPPOSITE CALL FROM `/earn-game`.
    /// `PointsService` queues earns because the amount is already known and replaying it later
    /// is exact. A mission claim's amount is decided BY THE SERVER at claim time, so a queued
    /// claim is not a deferred credit, it is a deferred DECISION — and one made against
    /// whatever `golfin_mission_rewards` says days later. The claim is online-only; the
    /// idempotency key makes retrying it safe, which is the property that actually matters.
    /// </summary>
    public sealed class MissionsClient
    {
        private readonly ApiClient _client;

        public MissionsClient(ApiClient client) => _client = client;

        private static MissionsClient _instance;
        public static MissionsClient Instance
            => _instance ??= new MissionsClient(ApiClient.Instance);

        public static void ConfigureForTest(MissionsClient c) => _instance = c;
        public static void ResetForTest() => _instance = null;

        /// <summary>
        /// Claim one campaign mission attempt.
        ///
        /// `goalsMet=false` is STILL SENT. The attempt is recorded server-side and pays
        /// nothing, which is what lets the Mission Selection screen show "tried and failed"
        /// rather than treating it as never opened — a real state, and usually the one a
        /// support question is about.
        /// </summary>
        public IEnumerator ClaimRoutine(string missionId, int strokes, bool goalsMet,
                                        string idempotencyKey,
                                        Action<ApiResult<MissionClaimResult>> onResult)
        {
            string body = BuildClaimJson(missionId, strokes, goalsMet, idempotencyKey);
            return _client.Post(Endpoints.MissionsClaim, body, onResult);
        }

        /// <summary>
        /// Today's daily. AUTH REQUIRED, so this only answers for a signed-in player; a
        /// failure leaves the card HIDDEN rather than showing an empty one, which is the
        /// standing invariant (never a dead card).
        /// </summary>
        public IEnumerator FetchDailyRoutine(Action<ApiResult<DailyMissionResult>> onResult)
            => _client.Get(Endpoints.MissionsDaily, onResult);

        /// <summary>Exposed so a test can assert the wire shape without a transport.</summary>
        public static string BuildClaimJson(string missionId, int strokes, bool goalsMet, string key)
            => JsonConvert.SerializeObject(new ClaimBody
            {
                mission_id = missionId,
                // The server refuses a cleared claim with no stroke count, and bounds it at 60.
                // Sending null for a failed attempt is correct: there may be no meaningful count.
                strokes = goalsMet ? (int?)Mathf.Clamp(strokes, 1, 60) : null,
                goals_met = goalsMet,
                idempotency_key = key,
            });

        [Serializable]
        private sealed class ClaimBody
        {
            public string mission_id;
            public int? strokes;
            public bool goals_met;
            public string idempotency_key;
        }
    }
}
