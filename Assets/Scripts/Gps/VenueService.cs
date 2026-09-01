// Order: gps_trust_core §6 — venue lookups over the EXISTING ApiClient. No second HTTP path.
using System;
using System.Collections;
using System.Collections.Generic;
using Golfin.Net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Golfin.Gps
{
    /// <summary>
    /// The venue half of the GPS module (SPEC §6).
    ///
    /// Plain C# singleton in the shape of <c>Golfin.Economy.PointsService</c> — constructible in an
    /// EditMode test, no MonoBehaviour, no queue (none of these calls is worth replaying offline).
    /// Bearer auth, the <c>{data:…}</c> unwrap, transient retries and the single 401 replay all come
    /// from <see cref="ApiClient"/> and are deliberately not reimplemented here.
    /// </summary>
    public sealed class VenueService
    {
        private static VenueService _instance;

        public static VenueService Instance => _instance ?? (_instance = new VenueService(ApiClient.Instance));

        public static void ConfigureForTest(VenueService service) => _instance = service;

        public static void ResetForTest() => _instance = null;

        private readonly ApiClient _client;

        public VenueService(ApiClient client)
        {
            _client = client;
        }

        /// <summary>
        /// <c>POST /venue/auto-register {latitude, longitude}</c> — resolve (and, server-side, create
        /// if new) the nearest golf course.
        ///
        /// ONLY the two fields the Dart client sends go in the body. <c>radius_m</c>,
        /// <c>language_code</c> and <c>preferred_name</c> keep their server defaults
        /// (venue.py:116-121) — sending them would freeze a tuning knob into a shipped build.
        ///
        /// A SUCCESS with <c>Data == null</c> means "no course nearby" (200 <c>{"data": null,
        /// "message": …}</c>), not a failure. Branch on Data.
        /// </summary>
        public IEnumerator AutoRegister(double lat, double lon, Action<ApiResult<VenueAutoRegisterResult>> onResult)
        {
            var body = new JObject
            {
                ["latitude"] = lat,
                ["longitude"] = lon
            };
            return _client.Post(Endpoints.VenueAutoRegister, body.ToString(Formatting.None), onResult);
        }

        /// <summary>
        /// <c>GET /venue/nearby?prefixes=…&amp;language_code=…</c> → the venues in any of the given
        /// geohash cells. Build <paramref name="prefixes"/> with
        /// <see cref="Geohash.NearbyPrefixes"/> so the cells match the ones the server hashed into
        /// <c>venues.geohash</c>.
        /// </summary>
        public IEnumerator Nearby(string prefixes, string languageCode, Action<ApiResult<List<VenueDto>>> onResult)
            => _client.Get(Endpoints.VenueNearby(prefixes, languageCode), onResult);

        /// <summary><c>GET /venue/list?language_code=</c> → every venue. The manual-selection
        /// fallback for when GPS resolves nothing.</summary>
        public IEnumerator List(string languageCode, Action<ApiResult<List<VenueDto>>> onResult)
            => _client.Get(Endpoints.VenueList(languageCode), onResult);
    }
}
