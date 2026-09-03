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

        // ── gps_checkin §A1 — venues became the SPOTS table ──
        //
        // Additive columns, so an old client keeps working and a new one sees three categories.
        // `sport_type` above is UNCHANGED and still 'golf' on every row: it is the Flutter app's
        // axis, and `Category` is the one the Rounds tab's chips browse by.

        /// <summary>"golf" | "range" | "food". Defaults to golf on any row written before the
        /// migration, and on any row a future server adds a fourth value to — the Rounds screen
        /// only ever asks for one category at a time, so an unknown value simply never arrives.</summary>
        [JsonProperty("category")]      public string Category;

        /// <summary>Drives the gold PARTNER tag on the spot row.</summary>
        [JsonProperty("is_partner")]    public bool IsPartner;

        /// <summary>The grey line under the name ("Kawagoe, Saitama · East 18H · PAR 72").</summary>
        [JsonProperty("subtitle")]      public string Subtitle;

        /// <summary>"¥15,000〜" — free text, appended after the distance on the row's green line.</summary>
        [JsonProperty("price_label")]   public string PriceLabel;

        /// <summary>"24H" / "10%OFF" / "ナイター" — the small chip the admin panel calls Chip.</summary>
        [JsonProperty("chip_extra")]    public string ChipExtra;

        /// <summary>Display text only; nothing redeems it (SPEC § Out of scope).</summary>
        [JsonProperty("partner_offer")] public string PartnerOffer;

        /// <summary>The server already filters <c>/venue/nearby</c> on this; it is carried so the
        /// venue-detail path can tell a deactivated spot from a missing one.</summary>
        [JsonProperty("is_active")]     public bool? IsActive;

        /// <summary>
        /// Metres from the caller, computed SERVER-SIDE by <c>/venue/nearby</c> when the request
        /// carried lat/lon, and the key the server already sorted the page by.
        ///
        /// <para>THE CLIENT SORTS NOTHING (§A2). It used to be impossible for it to sort
        /// correctly: geohash-prefix order is not distance order, so the old "nearby" list was in
        /// insertion order pretending to be near. Null means the fetch had no fix — the no-GPS
        /// state, where CHECK IN is disabled and the row says why.</para>
        /// </summary>
        [JsonProperty("distance_m")]    public double? DistanceM;

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

        /// <summary>gps_checkin §A3 — the server's own "1h 24m", written by the check-out RPC and
        /// by a score post that closes a live round. Never re-derived on the client: the two
        /// would disagree by whatever the request took.</summary>
        [JsonProperty("duration")]        public string Duration;

        // ── Score columns, written by score.py:206-230 (gps_hub_entry §3) ──
        //
        // Only a SCORE SUBMIT writes these; a bare check-in leaves every one null, which is why
        // Score is nullable rather than 0. GET /score/history returns rows carrying them, and the
        // GPS hub's MY RECENT ROUNDS panel renders exactly these four.
        //
        // ⚠️ There is NO `par` on this row, so a "+N vs par" cannot be computed from it. The hub
        // shows the hole count instead — see gps_hub_entry's § Figma Fidelity, Friends' Rounds row.
        [JsonProperty("score")]           public int? Score;
        /// <summary>"18" / "9" — how many holes the score covers, as free text from the app.</summary>
        [JsonProperty("score_type")]      public string ScoreType;
        /// <summary>"manual" / "ocr" — how the score reached the server.</summary>
        [JsonProperty("input_method")]    public string InputMethod;
        /// <summary>The course as the player typed/OCR'd it; may differ from <see cref="VenueName"/>.</summary>
        [JsonProperty("course_name")]     public string CourseName;

        public override string ToString() => $"ActivityDto #{Id} {VenueName} ({Status})";
    }
}
