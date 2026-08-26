// ─────────────────────────────────────────────────────────────────────────────
// ContentRuntime — ContentCatalogMapper
// Payload JSON → per-catalog row tables. The Phase-2 counterpart of
// ContentTextsMapper, and pure for the same reason: this is the one place that
// can turn a good payload into wrong CLUB STATS, so it must be the file an
// EditMode test can drive with hand-written JSON.
//
// ⚠️ THE ABSENT-CATALOG DISTINCTION IS THE POINT OF THIS FILE.
//
//   Measured against the live endpoint 2026-08-26:
//     since=clubs:1  (== the server's version)  → {"clubs":{"version":1,"full":false,"changed":[]}}
//     catalogs=nosuchcatalog                    → {"catalogs":{}}
//
//   So "nothing changed" is PRESENT-AND-EMPTY, and ABSENT means the server did
//   not serve that catalog at all. Those are different answers and this mapper
//   keeps them different: `Catalogs` holds what came back, and the caller
//   compares it against what it ASKED for. See ContentService.RefreshRoutine
//   and SPEC §7 for what the difference is worth.
//
//   ⚠️ AND SINCE 2026-08-26, ABSENT COMES WITH A REASON.
//     content_kill_switch_and_order added a top-level `disabled` list, so a
//     catalog the operator killed is absent AND NAMED. `Disabled` carries it,
//     and `IsDisabled` is what the caller asks instead of guessing between kill
//     / unknown-name / server bug. The same change made top-level `enabled` a
//     GENUINE global flag: it used to be an AND across the REQUESTED catalogs,
//     so one killed catalog dropped every cache on every client.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Golfin.Content
{
    /// <summary>One catalog's rows, in payload order and keyed by id.</summary>
    public sealed class ContentCatalog
    {
        public ContentCatalog(string name, int version, bool full, List<ContentRow> rows,
                              bool enabled = true)
        {
            Name    = name;
            Version = version;
            Full    = full;
            Enabled = enabled;
            Rows    = rows;

            ById = new Dictionary<string, ContentRow>(rows.Count, StringComparer.Ordinal);
            foreach (var row in rows) ById[row.Id] = row;
        }

        public string Name { get; }

        /// <summary>That catalog's own published version — the ONLY value that is a valid cursor.</summary>
        public int Version { get; }

        /// <summary>
        /// True when the server sent the whole ACTIVE catalog rather than a delta.
        /// <para>
        /// Unlike texts this is load-bearing: a full response carries active rows only, so it
        /// cannot express a deactivation by omission. The client therefore never infers
        /// <c>is_active=false</c> from a row's absence — I6 deactivation only ever arrives as an
        /// explicit row with <c>is_active:false</c>, which is exactly what the delta path sends.
        /// </para>
        /// </summary>
        public bool Full { get; }

        /// <summary>
        /// This catalog's own kill switch, as the server reported it.
        /// <para>
        /// Practically always true: the server omits a disabled catalog and names it in the
        /// top-level <c>disabled</c> list instead (see <see cref="ContentPayload.Disabled"/>), so a
        /// catalog that reached this type was served. It defaults to true for the same reason and
        /// is honoured anyway, so a server that starts sending a killed catalog present-and-flagged
        /// cannot have its kill silently dropped.
        /// </para>
        /// </summary>
        public bool Enabled { get; }

        public List<ContentRow> Rows { get; }
        public Dictionary<string, ContentRow> ById { get; }

        public int ActiveCount
        {
            get { int n = 0; foreach (var r in Rows) if (r.IsActive) n++; return n; }
        }
    }

    /// <summary>What one payload turned into, across every catalog it carried.</summary>
    public readonly struct ContentPayload
    {
        /// <summary>False when the payload was absent, malformed, or not an object.</summary>
        public readonly bool Parsed;

        /// <summary>
        /// The GLOBAL kill switch (§7). When false the caller must ignore the payload ENTIRELY and
        /// drop EVERY catalog's cache. Defaults to true so a server that predates the flag is not
        /// read as disabled.
        /// </summary>
        public readonly bool Enabled;

        /// <summary>Informational only — never replay as a cursor.</summary>
        public readonly int LatestVersion;

        /// <summary>Catalogs the server actually served. Never null.</summary>
        public readonly Dictionary<string, ContentCatalog> Catalogs;

        /// <summary>
        /// Catalogs the server's registry has KILLED (top-level <c>disabled</c>). Never null; empty
        /// on a server that predates the field, which reads as "nothing killed".
        /// <para>
        /// This is the half of the §7.4 story `Catalogs` cannot tell. A killed catalog is absent
        /// from `Catalogs`, and so is a catalog name the server has never heard of, and so is one
        /// the server omitted through a bug. All three end in the same place — that catalog reverts
        /// to bundled — but only the first is an operator's decision, and only this list can say so.
        /// </para>
        /// </summary>
        public readonly HashSet<string> Disabled;

        public ContentPayload(bool parsed, bool enabled, int latestVersion,
                              Dictionary<string, ContentCatalog> catalogs,
                              HashSet<string>? disabled = null)
        {
            Parsed        = parsed;
            Enabled       = enabled;
            LatestVersion = latestVersion;
            Catalogs      = catalogs;
            Disabled      = disabled ?? NewNameSet();
        }

        /// <summary>A set keyed the way catalog names compare — ordinal, case-insensitive.</summary>
        public static HashSet<string> NewNameSet() =>
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public static ContentPayload Unparsed() =>
            new ContentPayload(false, true, 0, ContentCatalogs.NewMap<ContentCatalog>());

        public ContentCatalog? Catalog(string name)
            => Catalogs.TryGetValue(name, out var c) ? c : null;

        /// <summary>
        /// True when the server said THIS catalog is killed — either by naming it in
        /// <see cref="Disabled"/>, or by serving it with <c>enabled:false</c>. Both are honoured so
        /// the client is correct under either wire shape.
        /// </summary>
        public bool IsDisabled(string name)
        {
            if (Disabled.Contains(name)) return true;
            return Catalogs.TryGetValue(name, out var c) && !c.Enabled;
        }

        /// <summary>Requested catalogs the response did NOT carry. See the file header.</summary>
        public List<string> AbsentFrom(IEnumerable<string> requested)
        {
            var missing = new List<string>();
            foreach (string name in requested)
                if (!Catalogs.ContainsKey(name)) missing.Add(name);
            return missing;
        }
    }

    public static class ContentCatalogMapper
    {
        private const string Tag = "[Content]";

        /// <summary>
        /// Parse a raw or unwrapped body. A cached RAW body still carries <c>{"data": …}</c>; a live
        /// fetch has already been unwrapped by <c>ApiEnvelope</c>. Both are accepted, exactly as
        /// <see cref="ContentTextsMapper.Map"/> does.
        /// <para>
        /// Returns <see cref="ContentPayload.Unparsed"/> on ANY failure, with one warning. A corrupt
        /// cache is a designed path, not a malfunction, so nothing here may throw.
        /// </para>
        /// </summary>
        public static ContentPayload Map(string? json)
        {
            RemoteContentDto? dto;
            try
            {
                if (string.IsNullOrWhiteSpace(json)) return ContentPayload.Unparsed();

                JToken root;
                using (var reader = new JsonTextReader(new StringReader(json!))
                                    { DateParseHandling = DateParseHandling.None })
                    root = JToken.ReadFrom(reader);

                JToken payload = root;
                if (root.Type == JTokenType.Object)
                {
                    JToken? inner = ((JObject)root)["data"];
                    if (inner != null) payload = inner;
                }

                if (payload.Type != JTokenType.Object) return ContentPayload.Unparsed();

                var settings = new JsonSerializerSettings { DateParseHandling = DateParseHandling.None };
                dto = payload.ToObject<RemoteContentDto>(JsonSerializer.CreateDefault(settings));
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"{Tag} Could not parse the content payload: {ex.Message}. " +
                                 $"Falling back to the bundled catalogs.");
                return ContentPayload.Unparsed();
            }

            if (dto == null) return ContentPayload.Unparsed();

            bool enabled = dto.Enabled ?? true;

            // GLOBAL KILL SWITCH. Short-circuit BEFORE reading any catalog: enabled:false must mean
            // "ignore this response entirely", never "every catalog came back empty".
            //
            // Since 2026-08-26 this really is global (content_kill_switch_and_order §1). It used to
            // be an AND over the REQUESTED catalogs, which made one catalog's kill land here and
            // wipe all seven caches — the per-catalog kill now arrives as `disabled` below.
            if (!enabled)
                return new ContentPayload(true, false, dto.LatestVersion ?? 0,
                                          ContentCatalogs.NewMap<ContentCatalog>());

            // PER-CATALOG KILL. Names only — the catalogs themselves are absent, by design.
            var disabled = ContentPayload.NewNameSet();
            if (dto.Disabled != null)
            {
                foreach (string? name in dto.Disabled)
                    if (!string.IsNullOrWhiteSpace(name)) disabled.Add(name!.Trim());
            }

            var catalogs = ContentCatalogs.NewMap<ContentCatalog>();
            if (dto.Catalogs != null)
            {
                foreach (var pair in dto.Catalogs)
                {
                    if (string.IsNullOrWhiteSpace(pair.Key) || pair.Value == null) continue;
                    catalogs[pair.Key.Trim()] = MapCatalog(pair.Key.Trim(), pair.Value);
                }
            }

            return new ContentPayload(true, true, dto.LatestVersion ?? 0, catalogs, disabled);
        }

        private static ContentCatalog MapCatalog(string name, RemoteCatalogDto dto)
        {
            var rows = new List<ContentRow>(dto.Changed?.Count ?? 0);
            int unusable = 0;

            if (dto.Changed != null)
            {
                foreach (var row in dto.Changed)
                {
                    if (row == null) { unusable++; continue; }

                    // The id can arrive as the row envelope's `id` or as the row's own id column;
                    // shop_catalog keys on `entryId`, so the envelope is the only reliable source
                    // and the column fallbacks exist for a hand-seeded row.
                    string? id = Trimmed(row.Id)
                                 ?? Column(row.Data, "id")
                                 ?? Column(row.Data, "entryId")
                                 ?? Column(row.Data, "key");

                    if (string.IsNullOrEmpty(id)) { unusable++; continue; }

                    rows.Add(new ContentRow(
                        id!,
                        row.IsActive ?? true,
                        row.MinBuild ?? 0,
                        row.Data ?? EmptyData));
                }
            }

            if (unusable > 0)
                Debug.LogWarning($"{Tag} Catalog '{name}': dropped {unusable} row(s) with no usable id.");

            return new ContentCatalog(name, dto.Version, dto.Full, rows, dto.Enabled ?? true);
        }

        /// <summary>
        /// The RAW JSON text of each requested catalog's slice, keyed by catalog name. Absent
        /// catalogs are simply not in the result — that is the caller's §7 signal.
        /// <para>
        /// Verbatim on purpose: <see cref="Map"/> throws away every column this build does not
        /// understand, so re-serialising a mapped view into the cache would quietly narrow the
        /// payload to what TODAY's build reads. Caching the untouched slice keeps a future build's
        /// columns alive across the upgrade, which is the same reason Phase 1 cached the raw body.
        /// </para>
        /// </summary>
        public static Dictionary<string, string> ExtractSlices(string? json, IEnumerable<string> catalogs)
        {
            var slices = ContentCatalogs.NewMap<string>();
            if (string.IsNullOrWhiteSpace(json)) return slices;

            try
            {
                JToken root;
                using (var reader = new JsonTextReader(new StringReader(json!))
                                    { DateParseHandling = DateParseHandling.None })
                    root = JToken.ReadFrom(reader);

                JToken payload = root;
                if (root.Type == JTokenType.Object)
                {
                    JToken? inner = ((JObject)root)["data"];
                    if (inner != null) payload = inner;
                }

                if (payload.Type != JTokenType.Object) return slices;

                if (((JObject)payload)["catalogs"] is not JObject bag) return slices;

                foreach (string name in catalogs)
                {
                    JToken? slice = bag[name];
                    if (slice != null && slice.Type == JTokenType.Object)
                        slices[name] = slice.ToString(Formatting.None);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"{Tag} Could not slice the content payload: {ex.Message}. " +
                                 $"No cache is written from it.");
            }

            return slices;
        }

        private static readonly Dictionary<string, string?> EmptyData = new Dictionary<string, string?>(0);

        private static string? Column(Dictionary<string, string?>? data, string name)
            => data != null && data.TryGetValue(name, out string? v) && !string.IsNullOrWhiteSpace(v)
                ? v!.Trim() : null;

        private static string? Trimmed(string? s)
            => string.IsNullOrWhiteSpace(s) ? null : s!.Trim();
    }
}
