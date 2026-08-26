// ─────────────────────────────────────────────────────────────────────────────
// ContentRuntime — wire DTOs for GET /api/v1/content
//
// The only types that know the server's field names, exactly as
// RemoteNoticeDtos.cs is for /notices. Verified against the live response on
// 2026-08-26, not assumed:
//
//   {"data":{"fetched_at":"…","enabled":true,"latest_version":11,
//            "catalogs":{"texts":{"version":11,"full":false,
//                                 "changed":[{"id":"BTN_START","is_active":true,
//                                             "min_build":0,
//                                             "data":{"key":"BTN_START",
//                                                     "English":"PLAY",
//                                                     "Japanese":"プレイ"}}]}}}}
//
// I4 (CONTENT_PIPELINE_PLAN §2) is what shapes these: the client parses by
// column NAME, ignores unknown columns and defaults missing ones. `data` is
// therefore a loose string→string bag, never a typed row — a new column added
// in the admin must not need a client change to be ignored safely.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Golfin.Content
{
    /// <summary>
    /// The <c>data</c> object of the content response. <see cref="Golfin.Net.ApiEnvelope"/> has
    /// already unwrapped <c>{"data": …}</c> by the time this is deserialised from a live fetch;
    /// the disk cache holds the RAW body, so the reader tolerates both (see
    /// <c>ContentPayload.Parse</c>).
    /// </summary>
    public sealed class RemoteContentDto
    {
        [JsonProperty("fetched_at")] public string? FetchedAt;

        /// <summary>
        /// The GLOBAL kill switch (§7.4). <b>False means ignore this response ENTIRELY and drop
        /// EVERY cache</b> — never "the catalog is now empty", which would wipe every remote string
        /// as if an operator had deleted them. Absent is treated as true, so an older server that
        /// predates the flag is not read as disabled.
        /// <para>
        /// ⚠️ IT IS GLOBAL, AND IT ONLY BECAME GLOBAL ON 2026-08-26
        /// (content_kill_switch_and_order §1). Until then the server ANDed
        /// <c>content_catalogs.is_enabled</c> across the catalogs the client had REQUESTED, so
        /// disabling ONE catalog set this false and every client dropped ALL SEVEN caches. The
        /// per-catalog kill now arrives as <see cref="Disabled"/> instead, and this flag is false
        /// only for a genuine global kill (the <c>content_settings.content_enabled</c> row, or
        /// every catalog in the registry disabled at once).
        /// </para>
        /// </summary>
        [JsonProperty("enabled")] public bool? Enabled;

        /// <summary>
        /// The PER-CATALOG kill switch: every catalog the server's registry has disabled, named.
        /// <para>
        /// A disabled catalog is <b>absent</b> from <see cref="Catalogs"/> — that stays true and is
        /// load-bearing, since a catalog at cursor parity comes back present-and-empty, so absent
        /// already means "not served". What this list adds is WHICH absence it is: without it,
        /// <c>is_enabled=false</c>, an unknown catalog name and a server-side omission bug are
        /// indistinguishable on the wire, so the client could revert to bundled correctly but never
        /// say why. Absent (an older server) is an empty list, which reads as "nothing killed".
        /// </para>
        /// </summary>
        [JsonProperty("disabled")] public List<string>? Disabled;

        /// <summary>
        /// Newest publish this server holds. <b>INFORMATIONAL ONLY — never replay it as a cursor.</b>
        /// The cursor is per-catalog; see <see cref="Golfin.Net.Endpoints.Content"/>.
        /// </summary>
        [JsonProperty("latest_version")] public int? LatestVersion;

        /// <summary>Keyed by catalog name — this build only ever asks for, and only ever reads, <c>texts</c>.</summary>
        [JsonProperty("catalogs")] public Dictionary<string, RemoteCatalogDto>? Catalogs;
    }

    /// <summary>One catalog's slice of the response.</summary>
    public sealed class RemoteCatalogDto
    {
        /// <summary>That catalog's own published version. The ONLY value that is a valid cursor.</summary>
        [JsonProperty("version")] public int Version;

        /// <summary>
        /// This catalog's own kill switch. The server only ever serves <c>true</c> here — a
        /// disabled catalog is omitted from <c>catalogs</c> and named in the top-level
        /// <c>disabled</c> list instead, because an absent object cannot carry a field.
        /// <para>
        /// Read anyway, and honoured: a future server that chooses to serve a killed catalog
        /// present-and-flagged rather than absent must not have its kill silently ignored. Absent
        /// is true, so a server that predates the field is not read as disabled.
        /// </para>
        /// </summary>
        [JsonProperty("enabled")] public bool? Enabled;

        /// <summary>
        /// True when the server sent the whole (active) catalog rather than a delta.
        /// <para>
        /// Texts treats both identically and deliberately: I1 makes the bundled table the floor and
        /// the payload an overlay applied on top by id, so "everything" and "what changed" merge the
        /// same way. The flag is carried for logging, and because Phase 2/3 catalogs WILL need to
        /// distinguish them (a full response carries active rows only, so it cannot be used to
        /// un-apply a deactivation).
        /// </para>
        /// </summary>
        [JsonProperty("full")] public bool Full;

        [JsonProperty("changed")] public List<RemoteContentRowDto>? Changed;
    }

    /// <summary>
    /// One content row. <c>min_build</c> is echoed for logging only — the server has ALREADY
    /// applied the filter (I4 lives server-side by design, so it cannot be bypassed by an old or
    /// modified client).
    /// </summary>
    public sealed class RemoteContentRowDto
    {
        [JsonProperty("id")] public string? Id;

        /// <summary>
        /// I6: a deactivated row is an update, never a delete. For texts a false value means
        /// <b>ignore this row and keep the bundled string</b> — there is nothing to remove, because
        /// the bundled table is the floor.
        /// </summary>
        [JsonProperty("is_active")] public bool? IsActive;

        [JsonProperty("min_build")] public int? MinBuild;

        /// <summary>The CSV row as <c>{column: value}</c>. Unknown columns are ignored (I4).</summary>
        [JsonProperty("data")] public Dictionary<string, string?>? Data;
    }
}
