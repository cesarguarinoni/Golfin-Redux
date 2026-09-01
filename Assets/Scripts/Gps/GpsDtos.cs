// Order: gps_trust_core §9 — payload shapes transcribed from the deployed PLAYLIFE routers, not guessed.
using Newtonsoft.Json;

namespace Golfin.Gps
{
    /// <summary>
    /// <c>POST /api/v1/venue/auto-register</c> → <c>{data:{venue_id, name, latitude, longitude,
    /// distance_m, created}}</c> (venue.py:246-256 existing-match branch, :273-282 insert branch).
    ///
    /// "No golf course found nearby" is NOT an error: the router answers 200 with
    /// <c>{"data": null, "message": …}</c>, which <c>ApiEnvelope.TryUnwrap</c> turns into a
    /// SUCCESSFUL <c>ApiResult</c> whose <c>Data</c> is null. Callers must branch on Data, never on
    /// <c>Success</c>.
    ///
    /// <see cref="VenueId"/> is nullable on purpose. The Dart client guards with
    /// <c>if (d['venue_id'] != null)</c> before trusting the row; a non-nullable int would silently
    /// turn a missing id into venue 0 and send <c>gps_verified: true</c> for a venue that does not exist.
    /// </summary>
    public sealed class VenueAutoRegisterResult
    {
        [JsonProperty("venue_id")]   public int? VenueId;
        [JsonProperty("name")]       public string Name;
        [JsonProperty("latitude")]   public double? Latitude;
        [JsonProperty("longitude")]  public double? Longitude;
        [JsonProperty("distance_m")] public double? DistanceM;
        [JsonProperty("created")]    public bool Created;

        public override string ToString()
            => $"Venue #{VenueId} {Name} ({DistanceM}m, created={Created})";
    }

    /// <summary>
    /// A row of the <c>venues</c> table, as returned by <c>GET /venue/list</c>, <c>/venue/nearby</c>
    /// and <c>/venue/{id}</c> (venue.py:56-113). Columns per
    /// <c>Docs/GPS/GPS_INTEGRATION_REFERENCE.md</c> §5.
    ///
    /// Those routers all <c>select("*")</c>, so the server can grow a column without a client
    /// release. Newtonsoft ignores unknown members by default — do NOT switch this type to
    /// <c>MissingMemberHandling.Error</c>.
    ///
    /// Everything except <c>id</c> and <c>name</c> is nullable because rows written by the OSM
    /// fallback carry far less than rows written from Google Places.
    /// </summary>
    public sealed class VenueDto
    {
        [JsonProperty("id")]           public int Id;
        [JsonProperty("name")]         public string Name;
        [JsonProperty("sport_type")]   public string SportType;
        [JsonProperty("latitude")]     public double? Latitude;
        [JsonProperty("longitude")]    public double? Longitude;
        [JsonProperty("geohash")]      public string Geohash;
        [JsonProperty("address")]      public string Address;
        [JsonProperty("gps_radius_m")] public double? GpsRadiusM;
        [JsonProperty("rating")]       public double? Rating;
        [JsonProperty("phone")]        public string Phone;
        [JsonProperty("place_id")]     public string PlaceId;
        [JsonProperty("source")]       public string Source;

        public override string ToString() => $"VenueDto #{Id} {Name}";
    }

    /// <summary>
    /// An <c>activities</c> row, as returned by <c>POST /activity/checkin</c> (activity.py:44-58)
    /// and written by <c>POST /score/submit</c> (score.py:200-231).
    ///
    /// The GPS columns are nullable because a check-in writes none of them — only a score submit
    /// does. No service method consumes this yet; the type exists so <c>gps_checkin_screen</c> does
    /// not have to re-derive the shape.
    /// </summary>
    public sealed class ActivityDto
    {
        [JsonProperty("id")]              public long Id;
        [JsonProperty("user_id")]         public string UserId;
        [JsonProperty("venue_id")]        public int? VenueId;
        [JsonProperty("venue_name")]      public string VenueName;
        [JsonProperty("sport_type")]      public string SportType;
        [JsonProperty("check_in_at")]     public string CheckInAt;
        [JsonProperty("check_out_at")]    public string CheckOutAt;
        [JsonProperty("status")]          public string Status;

        // ── GPS Trust columns, written by score.py:200-231 only ──
        [JsonProperty("trust_level")]     public int? TrustLevel;
        [JsonProperty("gps_verified")]    public bool? GpsVerified;
        [JsonProperty("gps_check_count")] public int? GpsCheckCount;
        [JsonProperty("gps_start_lat")]   public double? GpsStartLat;
        [JsonProperty("gps_start_lon")]   public double? GpsStartLon;
        [JsonProperty("gps_end_lat")]     public double? GpsEndLat;
        [JsonProperty("gps_end_lon")]     public double? GpsEndLon;
        [JsonProperty("gps_is_mock")]     public bool? GpsIsMock;
        [JsonProperty("client_platform")] public string ClientPlatform;
        [JsonProperty("points")]          public int? Points;

        public override string ToString() => $"ActivityDto #{Id} {VenueName} ({Status})";
    }
}
