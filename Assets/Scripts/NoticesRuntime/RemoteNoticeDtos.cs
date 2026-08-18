// ─────────────────────────────────────────────────────────────────────────────
// NoticesRuntime — wire DTOs for GET /api/v1/notices
//
// These are the only types that know the server's field names. Same arrangement
// (and same reasoning) as RemoteBannerDtos.cs.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Golfin.Notices
{
    /// <summary>
    /// The <c>data</c> object of the notices response. <see cref="Golfin.Net.ApiEnvelope"/> has
    /// already unwrapped <c>{"data": …}</c> by the time this is deserialised from a live fetch;
    /// the disk cache holds the RAW body, so the reader tolerates both (see
    /// <c>NoticeService.Deserialize</c>).
    /// </summary>
    public sealed class RemoteNoticesDto
    {
        [JsonProperty("fetched_at")] public string? FetchedAt;

        /// <summary>
        /// In page order — index 0 is page 1. At most 5 (<c>MAX_NOTICES</c> in
        /// <c>backend/routers/notices.py</c>).
        /// <para>
        /// An EMPTY list is a normal, healthy response meaning "hide the panel", and is the one
        /// place notices differ in kind from banners: a banner slot always has bundled art behind
        /// it, an unwritten announcement has nothing behind it.
        /// </para>
        /// </summary>
        [JsonProperty("notices")] public List<RemoteNoticeDto>? Notices;
    }

    /// <summary>
    /// One live notice. The server has already applied <c>is_active</c>, the schedule window and
    /// the sort order, so this build never re-derives any of it.
    /// <para>
    /// <c>label</c> is deliberately absent: it is admin-only and never reaches a player.
    /// </para>
    /// <para>
    /// Both locales are always sent and the client picks — see <c>NoticeService.TryResolve</c>.
    /// A null <c>*_ja</c> means "fall back to English", not "hide".
    /// </para>
    /// </summary>
    public sealed class RemoteNoticeDto
    {
        [JsonProperty("title_en")] public string? TitleEn;
        [JsonProperty("title_ja")] public string? TitleJa;
        [JsonProperty("body_en")]  public string? BodyEn;
        [JsonProperty("body_ja")]  public string? BodyJa;

        /// <summary>
        /// The row's <c>end_at</c>, verbatim, or null.
        ///
        /// Kept as a STRING on purpose, for the same reason <c>RemoteBannerDto.ExpiresAt</c> is:
        /// typing it as <see cref="System.DateTime"/> lets Newtonsoft apply its default
        /// <c>DateTimeZoneHandling</c> and hand back a LOCAL time, which would give two players in
        /// different zones different behaviour. It is parsed explicitly with
        /// <c>AdjustToUniversal | AssumeUniversal</c> in <c>NoticeService</c>.
        ///
        /// It exists because the client mirrors this body to disk: a maintenance notice cached
        /// while the player was online can outlive its window while they are offline, and a
        /// maintenance notice that outlives the maintenance is worse than no notice at all.
        /// </summary>
        [JsonProperty("expires_at")] public string? ExpiresAt;
    }
}
