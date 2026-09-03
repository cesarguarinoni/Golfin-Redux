// ─────────────────────────────────────────────────────────────────────────────
// gps_checkin §C2 — the check-in/check-out payloads, transcribed from the two
// RPCs in 2026_09_03_venue_partners.sql, not guessed.
//
// The routers are thin wrappers over `golfin_activity_checkin` /
// `golfin_activity_checkout`, so the {data:…} envelope carries the FUNCTION's
// return object — `ok`, `replayed`, `activity`, `awarded`, and the balances the
// same call already moved. That is deliberate: the Rounds screen must not have
// to ask /points/balance to know what a check-in paid.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using Newtonsoft.Json;

namespace Golfin.Gps
{
    /// <summary>
    /// <c>POST /activity/checkin</c> → <c>{data:{ok, replayed, activity, awarded, gps_verified,
    /// distance_m, radius_m, activity_pts, total_points}}</c>.
    ///
    /// <para>A REFUSAL DOES NOT ARRIVE HERE. The router raises a 409 for
    /// <c>already_active</c> and a 404 for an unknown venue, so a refusal reaches the client as
    /// <c>ApiResult.Success == false</c> with the reason in the body — see
    /// <see cref="ActivityService.ReasonOf"/>, which is the ONE place that reads it.</para>
    /// </summary>
    public sealed class CheckInResult
    {
        [JsonProperty("ok")]           public bool Ok;

        /// <summary>True when the same idempotency key already opened this round. The client mints
        /// its key BEFORE the request and persists it, so a force-quit mid-check-in replays into
        /// this branch instead of opening a second round (§C3).</summary>
        [JsonProperty("replayed")]     public bool Replayed;

        [JsonProperty("activity")]     public ActivityDto? Activity;

        /// <summary>30 when the server put the player inside the venue radius, 0 otherwise.
        /// NEVER inferred client-side: the client's own distance check gates the BUTTON, this
        /// number is what was actually paid.</summary>
        [JsonProperty("awarded")]      public int Awarded;

        [JsonProperty("gps_verified")] public bool GpsVerified;

        /// <summary>Metres from the venue, as the SERVER measured it. Null when the request
        /// carried no fix.</summary>
        [JsonProperty("distance_m")]   public double? DistanceM;

        [JsonProperty("radius_m")]     public int? RadiusM;

        /// <summary>Post-call balances, so the Top UI can count up without a second request.</summary>
        [JsonProperty("activity_pts")] public int? ActivityPts;
        [JsonProperty("total_points")] public int? TotalPoints;

        public override string ToString()
            => $"CheckIn(ok={Ok}, replayed={Replayed}, awarded={Awarded}, verified={GpsVerified})";
    }

    /// <summary>
    /// <c>POST /activity/{id}/checkout</c> → the check-out half of the same shape.
    /// </summary>
    public sealed class CheckOutResult
    {
        [JsonProperty("ok")]               public bool Ok;
        [JsonProperty("replayed")]         public bool Replayed;
        [JsonProperty("activity")]         public ActivityDto? Activity;

        /// <summary>10 base + 5 when BOTH ends were inside the radius; 0 for an expired round.</summary>
        [JsonProperty("awarded")]          public int Awarded;

        /// <summary>The round was open longer than 8 h. Status is <c>expired</c>, nothing was paid,
        /// and <c>activities_count</c> did not move — the card says so rather than showing a
        /// reward that is not coming (§C4).</summary>
        [JsonProperty("expired")]          public bool Expired;

        [JsonProperty("gps_verified")]     public bool GpsVerified;

        /// <summary>The server's own "1h 24m", not a client re-derivation. The two would disagree
        /// by whatever the request took.</summary>
        [JsonProperty("duration")]         public string? Duration;

        [JsonProperty("elapsed_seconds")]  public double? ElapsedSeconds;
        [JsonProperty("activity_pts")]     public int? ActivityPts;
        [JsonProperty("total_points")]     public int? TotalPoints;
        [JsonProperty("activities_count")] public int? ActivitiesCount;

        public override string ToString()
            => $"CheckOut(ok={Ok}, awarded={Awarded}, expired={Expired}, duration={Duration})";
    }
}
