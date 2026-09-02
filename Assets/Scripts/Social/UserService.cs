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

        /// <summary>
        /// <c>PUT /user/update</c> → the caller's own <c>profiles</c> row, after the write.
        ///
        /// <para>
        /// AUTH REQUIRED, same as <see cref="Detail"/>. <paramref name="displayName"/> is the one
        /// REQUIRED field — the deployed <c>UpdateProfileRequest</c> declares it non-optional, so
        /// the Golf Profile screen always sends the nickname field's value even when the player
        /// did not change it. Every other argument is written only when it is non-null, which is
        /// how the server behaves too: an omitted field is left alone rather than blanked.
        /// </para>
        /// <para>
        /// STATUS CODES the caller must branch on:
        /// <c>409</c> — the display name is taken (<c>profiles_display_name_lower_key</c>);
        /// <c>422</c> — <paramref name="golfExperience"/> or <paramref name="avatarColor"/> was
        /// outside its enum, which is a client bug, not a player-facing condition.
        /// </para>
        /// <para>
        /// On success the returned row replaces <see cref="LastDetail"/> and raises
        /// <see cref="OnDetailChanged"/>, exactly as a fresh <see cref="Detail"/> would — so the
        /// GPS Profile hero panel repaints with the new nickname and avatar colour without a
        /// second round trip. A FAILED write leaves the cache untouched (same reason as
        /// <see cref="Detail"/>: a failed refresh must not blank a panel showing real values).
        /// </para>
        /// </summary>
        /// <param name="displayName">Required. The nickname to store.</param>
        /// <param name="handicap">Optional. <c>null</c> = leave whatever is there (the screen's
        /// blank handicap field), a value = write it.</param>
        /// <param name="golfExperience">Optional. <c>beginner</c> | <c>intermediate</c> |
        /// <c>advanced</c>.</param>
        /// <param name="avatarColor">Optional. <c>pink</c> | <c>green</c> | <c>blue</c> |
        /// <c>gold</c>.</param>
        public IEnumerator Update(string displayName,
                                  double? handicap,
                                  string golfExperience,
                                  string avatarColor,
                                  Action<ApiResult<UserDetailDto>> onResult)
        {
            string body = BuildUpdateJson(displayName, handicap, golfExperience, avatarColor);
            return _client.Put<UserDetailDto>(Endpoints.UserUpdate, body, result =>
            {
                if (result != null && result.Success && result.Data != null)
                {
                    LastDetail = result.Data;
                    OnDetailChanged?.Invoke(result.Data);
                }

                onResult?.Invoke(result);
            });
        }

        /// <summary>
        /// The wire body for <see cref="Update"/>. Public so an EditMode test can pin the shape
        /// without a live transport — the same seam <c>GachaPullService.BuildPullJson</c> uses.
        ///
        /// <c>NullValueHandling.Ignore</c> is what makes "omitted" different from "null": a field
        /// left out of the JSON is not written server-side, whereas an explicit <c>null</c> would
        /// have to be handled as a blank. Field names are snake_case on purpose — they mirror
        /// <c>backend/routers/user.py::UpdateProfileRequest</c>.
        /// </summary>
        public static string BuildUpdateJson(string displayName, double? handicap,
                                             string golfExperience, string avatarColor)
            => Newtonsoft.Json.JsonConvert.SerializeObject(
                new UpdateBody
                {
                    display_name    = displayName,
                    handicap        = handicap,
                    golf_experience = string.IsNullOrEmpty(golfExperience) ? null : golfExperience,
                    avatar_color    = string.IsNullOrEmpty(avatarColor)    ? null : avatarColor
                },
                new Newtonsoft.Json.JsonSerializerSettings
                {
                    NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore
                });

        // Mirrors backend/routers/user.py::UpdateProfileRequest — snake_case on purpose.
        // bio / avatar_url are deliberately absent: this screen never touches them, and sending
        // them as null would be indistinguishable from not sending them anyway.
        private sealed class UpdateBody
        {
            public string  display_name;
            public double? handicap;
            public string  golf_experience;
            public string  avatar_color;
        }
    }
}
