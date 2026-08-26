// ─────────────────────────────────────────────────────────────────────────────
// ContentRuntime — ContentRow / ContentFields
//
// The two types every catalog database reads. Both are deliberately dumb:
// I4 (CONTENT_PIPELINE_PLAN §2) says the client parses by column NAME, ignores
// unknown columns and defaults missing ones, so a published row is a
// {column: value} bag and never a typed struct.
//
// ContentFields is the whole merge. A database's row parser used to close over
// (fields, headerIndex); it now closes over a ContentFields, which answers each
// column from the OVERLAY when the overlay names it and from the BUNDLED CSV
// otherwise. That is "merge field-by-field" (SPEC §1) implemented once instead
// of six times, and it makes the append case — an overlay row with no bundled
// counterpart — the same code path with an empty CSV side.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Golfin.Content
{
    /// <summary>
    /// One published row: its id, its I6 activation flag, and the column bag.
    /// </summary>
    public sealed class ContentRow
    {
        public ContentRow(string id, bool isActive, int minBuild, IReadOnlyDictionary<string, string?> data)
        {
            Id       = id;
            IsActive = isActive;
            MinBuild = minBuild;
            Data     = data;
        }

        public string Id { get; }

        /// <summary>
        /// I6 — <b>deactivated, never deleted</b>. False means: gone from the shop, gone from any
        /// pool, but still fully renderable in the bag or roster of a player who owns one, and still
        /// equipped if it was. It is NOT a signal to drop the row from the database.
        /// </summary>
        public bool IsActive { get; }

        /// <summary>Echoed for logging only — the server has already applied the filter (I4).</summary>
        public int MinBuild { get; }

        /// <summary>The CSV row as <c>{column: value}</c>. Unknown columns are ignored (I4).</summary>
        public IReadOnlyDictionary<string, string?> Data { get; }

        /// <summary>
        /// True when the row names <paramref name="column"/> with a usable value. A column present
        /// but blank counts as ABSENT: a published empty cell must not blank a bundled value it was
        /// never meant to touch — the overlay is a sparse patch, not a replacement row.
        /// </summary>
        public bool TryGet(string column, out string value)
        {
            value = string.Empty;
            if (Data == null || string.IsNullOrEmpty(column)) return false;
            if (!Data.TryGetValue(column, out string? raw)) return false;
            if (string.IsNullOrWhiteSpace(raw)) return false;
            value = raw!;
            return true;
        }
    }

    /// <summary>
    /// A column reader over a bundled CSV row, an overlay row, or both. The overlay wins per column;
    /// anything it does not name falls through to the bundled value, and anything neither names
    /// falls through to the caller's default (I4).
    /// </summary>
    public sealed class ContentFields
    {
        private readonly IReadOnlyList<string>? _fields;
        private readonly IReadOnlyDictionary<string, int>? _headerIndex;
        private readonly ContentRow? _overlay;

        private ContentFields(IReadOnlyList<string>? fields,
                              IReadOnlyDictionary<string, int>? headerIndex,
                              ContentRow? overlay)
        {
            _fields      = fields;
            _headerIndex = headerIndex;
            _overlay     = overlay;
        }

        /// <summary>A bundled CSV row, optionally patched by an overlay row.</summary>
        public static ContentFields Csv(IReadOnlyList<string> fields,
                                        IReadOnlyDictionary<string, int> headerIndex,
                                        ContentRow? overlay = null)
            => new ContentFields(fields, headerIndex, overlay);

        /// <summary>An overlay row with no bundled counterpart — the APPEND case (SPEC §1).</summary>
        public static ContentFields OverlayOnly(ContentRow overlay)
            => new ContentFields(null, null, overlay);

        /// <summary>True when an overlay row is patching (or supplying) this row.</summary>
        public bool IsOverlaid => _overlay != null;

        /// <summary>True when there is no bundled CSV row behind this one.</summary>
        public bool IsAppended => _fields == null;

        /// <summary>The overlay row, when there is one. Diagnostics and the sprite guard.</summary>
        public ContentRow? Overlay => _overlay;

        /// <summary>
        /// I6 activation. A row with no overlay is active — the bundled catalog is the floor (I1),
        /// and a catalog the server has never spoken about cannot have been deactivated.
        /// </summary>
        public bool IsActive => _overlay?.IsActive ?? true;

        // ── Column readers ────────────────────────────────────────────────────

        /// <summary>Overlay value, else bundled CSV value, else <paramref name="def"/>.</summary>
        public string Get(string column, string def = "")
        {
            if (_overlay != null && _overlay.TryGet(column, out string overlaid)) return overlaid;
            return Bundled(column, def);
        }

        /// <summary>The BUNDLED value only, ignoring any overlay. The sprite guard's fallback.</summary>
        public string Bundled(string column, string def = "")
        {
            if (_fields == null || _headerIndex == null) return def;
            if (!_headerIndex.TryGetValue(column, out int i)) return def;
            if (i < 0 || i >= _fields.Count) return def;
            return _fields[i].Trim();
        }

        public int GetInt(string column, int def = 0)
            => int.TryParse(Get(column), NumberStyles.Integer, CultureInfo.InvariantCulture, out int v)
                ? v : def;

        public float GetFloat(string column, float def = 0f)
            => float.TryParse(Get(column), NumberStyles.Float, CultureInfo.InvariantCulture, out float v)
                ? v : def;

        /// <summary>
        /// TRUE / true / True / 1 are true; everything else is <paramref name="def"/> when the
        /// column is absent and false when it is present-but-something-else. Bags.csv writes
        /// <c>TRUE</c>, shop_catalog.csv writes <c>true</c>, and the wire echoes whichever the CSV
        /// held — so both spellings have to work or a published bag silently locks itself.
        /// </summary>
        public bool GetBool(string column, bool def = false)
        {
            string raw = Get(column, string.Empty);
            if (string.IsNullOrWhiteSpace(raw)) return def;
            raw = raw.Trim();
            return raw.Equals("true", StringComparison.OrdinalIgnoreCase) || raw == "1";
        }
    }
}
