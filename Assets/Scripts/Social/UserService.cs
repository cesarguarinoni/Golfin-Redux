// Order: gps_hub_entry §3 — the player's own profile over the EXISTING ApiClient. No second HTTP path.
using System;
using System.Collections;
using Golfin.Net;

namespace Golfin.Social
{
    /// <summary>
    /// The first service of the Social module: the caller's own PLAYLIFE profile row.
    ///
    /// <para>
    /// Plain C# singleton in the shape of <c>Golfin.Economy.PointsService</c> and
    /// <c>Golfin.Gps.VenueService</c> — constructible in an EditMode test, no MonoBehaviour, and no
    /// offline queue (a profile READ is worthless replayed later). Bearer auth, the
    /// <c>{data:…}</c> unwrap, transient retries and the single 401 replay all come from
    /// <see cref="ApiClient"/> and are deliberately not reimplemented here.
    /// </para>
    /// <para>
    /// The cache exists so a second screen entry can paint the hero panel from
    /// <see cref="LastDetail"/> on the FIRST frame instead of showing <c>—</c> again while the
    /// round trip repeats. <see cref="OnDetailChanged"/> is what a view subscribes to; it fires
    /// only on a successful non-null answer, so a failed refresh never blanks a panel that was
    /// already showing real numbers.
    /// </para>
    /// </summary>
    public sealed class UserService
    {
        private static UserService _instance;

        public static UserService Instance => _instance ?? (_instance = new UserService(ApiClient.Instance));

        public static void ConfigureForTest(UserService service) => _instance = service;

        public static void ResetForTest() => _instance = null;

        private readonly ApiClient _client;

        public UserService(ApiClient client)
        {
            _client = client;
        }

        /// <summary>The last profile the server returned this session, or null if it never has.</summary>
        public UserDetailDto LastDetail { get; private set; }

        /// <summary>Raised after <see cref="LastDetail"/> is replaced with a real row.</summary>
        public event Action<UserDetailDto> OnDetailChanged;

        /// <summary>
        /// <c>GET /user/detail</c> → the caller's own <c>profiles</c> row.
        ///
        /// <para>
        /// AUTH REQUIRED — the row is chosen by the bearer token, so there is nothing to pass. A
        /// SUCCESS with <c>Data == null</c> is possible (the row has not been created yet) and is
        /// NOT a failure; the cache and the event are both left untouched in that case, exactly as
        /// they are on an error, because "no answer" and "the answer was nothing" should not blank
        /// a panel differently.
        /// </para>
        /// </summary>
        public IEnumerator Detail(Action<ApiResult<UserDetailDto>> onResult)
        {
            return _client.Get<UserDetailDto>(Endpoints.UserDetail, result =>
            {
                if (result != null && result.Success && result.Data != null)
                {
                    LastDetail = result.Data;
                    OnDetailChanged?.Invoke(result.Data);
                }

                onResult?.Invoke(result);
            });
        }
    }
}
