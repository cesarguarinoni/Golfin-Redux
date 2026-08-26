// ─────────────────────────────────────────────────────────────────────────────
// ContentRuntime — ContentVersionFile
// Reads the bundled per-catalog cursor that Tools/content/export_content.py
// writes next to the CSVs it exports.
//
//   Assets/Resources/Data/content_version.txt
//     bags=1
//     balls=5
//     characters=5
//     clubs=1
//     items=1
//     shop_catalog=3
//     texts=11
//
// FORMAT MISMATCH, ON PURPOSE. The file writes `texts=11`; the endpoint wants
// `texts:11`. The exporter's format predates the endpoint and is shared with
// other tooling, so the conversion lives HERE rather than changing the file —
// see SPEC §3.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Golfin.Content
{
    /// <summary>
    /// The bundled content cursor, per catalog. <b>Every failure resolves to 0</b> — a missing
    /// file, an unreadable one, a garbage line, a negative number. 0 means "this client holds
    /// none of that catalog", so the server answers with the full payload, which is always
    /// recoverable. Throwing here would take the whole boot down over a text file.
    /// </summary>
    public static class ContentVersionFile
    {
        private const string Tag = "[Content]";

        /// <summary><c>Resources.Load</c> key — no folder prefix, no extension.</summary>
        public const string ResourcePath = "Data/content_version";

        private static Dictionary<string, int>? _cursors;

        /// <summary>
        /// The cursor for one catalog, or 0 when the file does not name it. Case-insensitive on the
        /// catalog name; the server's names are lower-case but a hand-edited file should not be able
        /// to silently cost a boot's worth of payload over capitalisation.
        /// </summary>
        public static int VersionFor(string catalog)
        {
            if (string.IsNullOrWhiteSpace(catalog)) return 0;
            var map = _cursors ??= Load();
            return map.TryGetValue(catalog.Trim(), out int v) ? v : 0;
        }

        /// <summary>Drop the memoised table so the next read re-parses. Tests and the editor only.</summary>
        public static void ResetForTest() => _cursors = null;

        /// <summary>Install a hand-built table as the parsed file (EditMode tests).</summary>
        public static void ConfigureForTest(Dictionary<string, int>? cursors) => _cursors = cursors;

        private static Dictionary<string, int> Load()
        {
            try
            {
                var asset = Resources.Load<TextAsset>(ResourcePath);
                if (asset == null || string.IsNullOrWhiteSpace(asset.text))
                {
                    // Not an error. A build made before the exporter ran has no file, and the
                    // correct answer is "ask for everything".
                    Debug.LogWarning(
                        $"{Tag} No bundled '{ResourcePath}'; every catalog cursor is 0 (full payload).");
                    return NewMap();
                }

                return Parse(asset.text);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"{Tag} Could not read '{ResourcePath}': {ex.Message}. Cursors are 0.");
                return NewMap();
            }
        }

        /// <summary>
        /// Pure parser — no Unity, no IO, so it is directly unit-testable.
        /// <list type="bullet">
        ///   <item>Blank lines and <c>#</c> comments are skipped.</item>
        ///   <item>A line with no <c>=</c>, an empty name, or an unparseable number is skipped with
        ///   a warning; the catalog then has no cursor, which is 0, which is a full payload.</item>
        ///   <item>A negative version clamps to 0, mirroring the server's own <c>parse_since</c>.</item>
        ///   <item>A duplicate name keeps the LAST occurrence, matching how the exporter rewrites
        ///   the file line by line.</item>
        /// </list>
        /// </summary>
        public static Dictionary<string, int> Parse(string? text)
        {
            var map = NewMap();
            if (string.IsNullOrWhiteSpace(text)) return map;

            foreach (string rawLine in text!.Split('\n'))
            {
                string line = rawLine.Trim().TrimEnd('\r');
                if (line.Length == 0 || line[0] == '#') continue;

                int eq = line.IndexOf('=');
                if (eq <= 0)
                {
                    Debug.LogWarning($"{Tag} Skipping unparseable content_version line '{line}'.");
                    continue;
                }

                string name = line.Substring(0, eq).Trim();
                string value = line.Substring(eq + 1).Trim();
                if (name.Length == 0)
                {
                    Debug.LogWarning($"{Tag} Skipping content_version line with no catalog name: '{line}'.");
                    continue;
                }

                if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int version))
                {
                    Debug.LogWarning(
                        $"{Tag} Skipping content_version line '{line}' — '{value}' is not an integer. " +
                        $"'{name}' will be requested in full.");
                    continue;
                }

                map[name] = Math.Max(0, version);
            }

            return map;
        }

        private static Dictionary<string, int> NewMap() =>
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    }
}
