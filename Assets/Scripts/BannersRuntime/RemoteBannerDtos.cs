// ─────────────────────────────────────────────────────────────────────────────
// BannersRuntime — wire DTOs for GET /api/v1/banners
//
// These are the only types that know the server's field names. Same arrangement
// (and same reasoning) as RemoteTournamentDtos.cs.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Golfin.Banners
{
    /// <summary>
    /// The <c>data</c> object of the banners response. <see cref="Golfin.Net.ApiEnvelope"/> has
    /// already unwrapped <c>{"data": …}</c> by the time this is deserialised from a live fetch;
    /// the disk cache holds the RAW body, so the reader tolerates both (see
    /// <c>BannerService.Deserialize</c>).
    /// </summary>
    public sealed class RemoteBannersDto
    {
        [JsonProperty("fetched_at")] public string? FetchedAt;
        [JsonProperty("banners")]    public List<RemoteBannerDto>? Banners;
    }

    /// <summary>
    /// One live banner. At most one per placement — the server has already applied
    /// <c>is_active</c>, the schedule window and the sort order.
    /// <para>
    /// <c>label</c> is deliberately absent: it is admin-only and never reaches a player.
    /// </para>
    /// </summary>
    public sealed class RemoteBannerDto
    {
        [JsonProperty("placement")]    public string? Placement;
        [JsonProperty("image_url_en")] public string? ImageUrlEn;
        [JsonProperty("image_url_ja")] public string? ImageUrlJa;
        [JsonProperty("link_url")]     public string? LinkUrl;

        /// <summary>
        /// The chosen row's <c>end_at</c>, verbatim, or null.
        ///
        /// Kept as a STRING on purpose, for the same reason <c>RemoteTournamentDto</c> keeps
        /// <c>start_at</c> / <c>end_at</c> as strings: typing it as <see cref="System.DateTime"/>
        /// lets Newtonsoft apply its default <c>DateTimeZoneHandling</c> and hand back a LOCAL
        /// time, which would give two players in different zones different behaviour. It is parsed
        /// explicitly with <c>AdjustToUniversal | AssumeUniversal</c> in <c>BannerService</c>.
        ///
        /// It exists because the client mirrors this body to disk: a cached banner whose window
        /// closed while the player was offline has to be dropped on-device, and without this the
        /// client would need the whole scheduling rule.
        /// </summary>
        [JsonProperty("expires_at")]   public string? ExpiresAt;
    }
}
