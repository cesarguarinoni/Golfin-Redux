// Order: gps_trust_core §6 — venue lookups over the EXISTING ApiClient. No second HTTP path.
using System;
using System.Collections;
using System.Collections.Generic;
using Golfin.Net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

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

        /// <summary>
        /// gps_checkin §A2 — the Rounds tab's fetch: one CATEGORY, and the caller's position so
        /// the SERVER computes <c>distance_m</c> and returns the page already sorted by it.
        ///
        /// <para>THE CLIENT SORTS NOTHING. The row order that comes back IS nearest-first;
        /// re-sorting here would be a second opinion about a number the server owns, and the
        /// "DISTANCE ▾" toggle flips to NAME order rather than re-deriving distance.</para>
        ///
        /// <para><paramref name="lat"/>/<paramref name="lon"/> may be null — that is the no-GPS
        /// state, and the rows come back unsorted with a null <c>distance_m</c>, which the screen
        /// renders with CHECK IN disabled and a reason.</para>
        /// </summary>
        public IEnumerator Nearby(string prefixes, string category, double? lat, double? lon,
                                  string languageCode, Action<ApiResult<List<VenueDto>>> onResult)
            => _client.Get(Endpoints.VenueNearby(prefixes, category, lat, lon, languageCode), onResult);

        /// <summary><c>GET /venue/list?language_code=</c> → every venue. The manual-selection
        /// fallback for when GPS resolves nothing.</summary>
        public IEnumerator List(string languageCode, Action<ApiResult<List<VenueDto>>> onResult)
            => _client.Get(Endpoints.VenueList(languageCode), onResult);

        /// <summary><c>GET /venue/{id}</c> → one venue row, including the §A1 spot columns.</summary>
        public IEnumerator ById(int venueId, string languageCode, Action<ApiResult<VenueDto>> onResult)
            => _client.Get(Endpoints.VenueById(venueId, languageCode), onResult);

        // ═════════════════════════════════════════════════════════════════════
        // The map tile (gps_checkin §A4 / §C2)
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>
        /// <c>GET /venue/map</c> → the dark roadmap tile for the Rounds panel, as a
        /// <see cref="Texture2D"/>.
        ///
        /// <para>NOT THROUGH <see cref="ApiClient"/>, and the exception is deliberate. That class
        /// exists to unwrap a <c>{data:…}</c> JSON envelope into a typed payload; this response is
        /// a PNG. Rather than teach the shared client a binary mode used by exactly one call, this
        /// borrows the ONE thing it does need from it — the bearer token, off the same
        /// <see cref="IAuthTokenProvider"/> — and hands the rest to
        /// <see cref="UnityWebRequestTexture"/>, which decodes straight into GPU memory instead of
        /// through a base64 string.</para>
        ///
        /// <para>A failure invokes <paramref name="onTexture"/> with null. The screen's answer is
        /// the baked stylised placeholder from the frame with the attribution hidden (§C4) — never
        /// an empty panel, and never a retry loop.</para>
        /// </summary>
        public IEnumerator MapTile(double lat, double lon, int zoom, int w, int h,
                                   Action<Texture2D> onTexture)
        {
            string url = Endpoints.VenueMap(lat, lon, zoom, w, h);
            using (UnityWebRequest req = UnityWebRequestTexture.GetTexture(url))
            {
                req.timeout = _client.TimeoutSeconds;

                IAuthTokenProvider auth = _client.Auth;
                if (auth != null && auth.IsAuthenticated && !string.IsNullOrEmpty(auth.AccessToken))
                    req.SetRequestHeader("Authorization", "Bearer " + auth.AccessToken);

                yield return req.SendWebRequest();

                if (req.result != UnityWebRequest.Result.Success)
                {
                    // WARNING, not error: a missing tile degrades the panel to the placeholder,
                    // and the most likely cause is the one Cesar has to fix in Google Cloud
                    // ("Maps Static API" not enabled on the key), which the body names.
                    Debug.LogWarning($"[VenueService] /venue/map failed ({req.responseCode}) — " +
                                     $"falling back to the placeholder. {req.error}");
                    onTexture?.Invoke(null);
                    yield break;
                }

                Texture2D tex = DownloadHandlerTexture.GetContent(req);
                if (tex != null) tex.wrapMode = TextureWrapMode.Clamp;
                onTexture?.Invoke(tex);
            }
        }
    }
}
