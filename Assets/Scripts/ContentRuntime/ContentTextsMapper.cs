// ─────────────────────────────────────────────────────────────────────────────
// ContentRuntime — ContentTextsMapper
// Payload JSON → the overlay dictionary LocalizationManager.ApplyOverlay takes.
//
// Everything here is a PURE function of its inputs — no MonoBehaviour, no
// clock, no socket, no Resources — so the whole mapping path is exercised in
// EditMode with hand-written JSON. That is deliberate: this is the one place
// that can silently turn a good payload into wrong strings, so it must be the
// most testable file in the assembly.
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
    /// <summary>What one payload turned into. A struct so a failed parse needs no allocation.</summary>
    public readonly struct TextsOverlay
    {
        /// <summary>False when the payload was absent, malformed, or carried no <c>texts</c> catalog.</summary>
        public readonly bool Parsed;

        /// <summary>
        /// The kill switch (§7). When false the caller must ignore the payload ENTIRELY and drop
        /// the cache. Defaults to true so a payload that never mentions the flag is not read as
        /// disabled.
        /// </summary>
        public readonly bool Enabled;

        /// <summary>That catalog's own published version — the only valid cursor. 0 when unknown.</summary>
        public readonly int Version;

        /// <summary>True when the server sent the whole active catalog rather than a delta.</summary>
        public readonly bool Full;

        /// <summary>Rows to merge, keyed by localization key. Never null; empty is a normal state.</summary>
        public readonly Dictionary<string, LocalizedTextRow> Rows;

        /// <summary>Rows the server sent that this mapper deliberately dropped. Diagnostics only.</summary>
        public readonly int SkippedInactive;
        public readonly int SkippedUnusable;

        public TextsOverlay(bool parsed, bool enabled, int version, bool full,
                            Dictionary<string, LocalizedTextRow> rows,
                            int skippedInactive, int skippedUnusable)
        {
            Parsed          = parsed;
            Enabled         = enabled;
            Version         = version;
            Full            = full;
            Rows            = rows;
            SkippedInactive = skippedInactive;
            SkippedUnusable = skippedUnusable;
        }

        public static TextsOverlay Unparsed() =>
            new TextsOverlay(false, true, 0, false, new Dictionary<string, LocalizedTextRow>(), 0, 0);
    }

    public static class ContentTextsMapper
    {
        private const string Tag = "[Content]";

        // The admin exports the CSV header verbatim, so the wire columns are capitalised
        // ("English"/"Japanese"). The lower-case spellings are accepted too because I4 says parse
        // by NAME and default what is missing — and a hand-seeded row is exactly where a
        // capitalisation slip would otherwise cost a silent blank string.
        private static readonly string[] EnglishColumns  = { "English", "english" };
        private static readonly string[] JapaneseColumns = { "Japanese", "japanese" };
        private static readonly string[] KeyColumns      = { "key", "Key" };

        /// <summary>
        /// Parse a raw or unwrapped body into an overlay.
        /// <para>
        /// A cached RAW body still carries <c>{"data": …}</c>; a live fetch has already been
        /// unwrapped by <c>ApiEnvelope</c>. Both are accepted, exactly as
        /// <c>NoticeService.Deserialize</c> does.
        /// </para>
        /// <para>
        /// Returns <see cref="TextsOverlay.Unparsed"/> on ANY failure, with one warning. A corrupt
        /// cache is a designed path (SPEC acceptance list), not a malfunction, so nothing here may
        /// throw.
        /// </para>
        /// </summary>
        public static TextsOverlay Map(string? json)
        {
            RemoteContentDto? dto;
            try
            {
                if (string.IsNullOrWhiteSpace(json)) return TextsOverlay.Unparsed();

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

                if (payload.Type != JTokenType.Object) return TextsOverlay.Unparsed();

                var settings   = new JsonSerializerSettings { DateParseHandling = DateParseHandling.None };
                dto = payload.ToObject<RemoteContentDto>(JsonSerializer.CreateDefault(settings));
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"{Tag} Could not parse the texts payload: {ex.Message}. " +
                                 $"Falling back to the bundled strings.");
                return TextsOverlay.Unparsed();
            }

            if (dto == null) return TextsOverlay.Unparsed();

            bool enabled = dto.Enabled ?? true;

            // KILL SWITCH. Short-circuit BEFORE reading any catalog: enabled:false must mean
            // "ignore this response entirely", never "the catalog came back empty".
            if (!enabled)
                return new TextsOverlay(true, false, 0, false,
                                        new Dictionary<string, LocalizedTextRow>(), 0, 0);

            RemoteCatalogDto? catalog = null;
            dto.Catalogs?.TryGetValue(RemoteContentSource.TextsCatalog, out catalog);

            // A well-formed body that simply does not carry `texts` is NOT a parse failure — it is
            // what a server that has never heard of the catalog answers, and the right response is
            // bundled strings with no cache churn. Reported as unparsed so the caller does not
            // overwrite a good cache with it.
            if (catalog == null) return TextsOverlay.Unparsed();

            var rows = new Dictionary<string, LocalizedTextRow>(StringComparer.Ordinal);
            int skippedInactive = 0;
            int skippedUnusable = 0;

            if (catalog.Changed != null)
            {
                foreach (var row in catalog.Changed)
                {
                    if (row == null) { skippedUnusable++; continue; }

                    // I6 — a deactivated row is IGNORED, not deleted. The bundled string stays,
                    // because the bundled table is the floor (I1) and there is nothing to remove.
                    if (row.IsActive == false) { skippedInactive++; continue; }

                    string? key = Trimmed(row.Id) ?? Column(row.Data, KeyColumns);
                    if (string.IsNullOrEmpty(key)) { skippedUnusable++; continue; }

                    string? english  = Column(row.Data, EnglishColumns);
                    string? japanese = Column(row.Data, JapaneseColumns);

                    // A blank english is worse than the bundled string, and Get()'s JA→EN fallback
                    // depends on english being present. ApplyOverlay enforces this too — this is
                    // the cheap half, so a dropped row never reaches the merge or the log count.
                    if (string.IsNullOrEmpty(english)) { skippedUnusable++; continue; }

                    rows[key!] = new LocalizedTextRow
                    {
                        key      = key!,
                        english  = english!,
                        japanese = japanese ?? string.Empty,
                    };
                }
            }

            return new TextsOverlay(true, true, catalog.Version, catalog.Full,
                                    rows, skippedInactive, skippedUnusable);
        }

        /// <summary>
        /// First non-empty value among <paramref name="names"/>, or null. NOT trimmed of inner
        /// whitespace — a string's leading/trailing spaces can be intentional layout — but a value
        /// that is nothing BUT whitespace counts as absent.
        /// </summary>
        private static string? Column(Dictionary<string, string?>? data, string[] names)
        {
            if (data == null) return null;
            foreach (string name in names)
                if (data.TryGetValue(name, out string? value) && !string.IsNullOrWhiteSpace(value))
                    return value;
            return null;
        }

        private static string? Trimmed(string? s) =>
            string.IsNullOrWhiteSpace(s) ? null : s!.Trim();
    }
}
