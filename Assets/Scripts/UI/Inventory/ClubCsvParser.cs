// ─────────────────────────────────────────────────────────────────────────────
// Golfin.Inventory — ClubCsvParser
// The one Clubs.csv reader. Pure text→rows: no Unity objects, no Resources, no
// MonoBehaviour, so the 799-row roster can be asserted from an EditMode test.
// ClubDatabaseCSV is the thin runtime adapter that maps rows onto ClubDataRuntime
// and resolves sprites.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System.Collections.Generic;
using Golfin.Content;  // ContentCatalog / ContentFields / ContentRow
using Golfin.Roster;   // CharacterRarity

namespace Golfin.Inventory
{
    /// <summary>
    /// One parsed Clubs.csv row, before sprite resolution. Field-for-field with the CSV
    /// columns so a test can assert the shipped roster without booting Unity.
    /// </summary>
    public class ClubCsvRow
    {
        // ── Overlay provenance (content_overlay_catalogs) ─────────────────────
        // Carried on the row rather than returned alongside it, because the runtime adapter needs
        // to answer two questions per row — "did the overlay touch this?" and "what did the CSV say
        // before it did?" — and the second one is what SPEC §5's sprite veto falls back to.

        /// <summary>True when a published row patched this one, or supplied it outright.</summary>
        public bool overlayApplied = false;

        /// <summary>True when there was no bundled CSV row at all — an APPENDED overlay row.</summary>
        public bool overlayAppended = false;

        /// <summary>
        /// I6 — deactivated, never deleted. False means: gone from the shop and from any pool,
        /// still fully renderable in the bag of a player who owns one, still equipped if it was.
        /// It is NOT a signal to drop the row.
        /// </summary>
        public bool isActive = true;

        /// <summary>
        /// The pre-merge row, when an overlay patched this one. Null for a bundled-only row and for
        /// an appended one. This is what SPEC §5 reverts to when a published sprite does not resolve.
        /// </summary>
        public ClubCsvRow? bundled = null;

        public string id    = "";
        public string name  = "";
        public ClubType       type   = ClubType.Driver;
        public CharacterRarity rarity = CharacterRarity.Common;
        public string brand = "";

        public int basePower         = 0;
        public int baseAccuracy      = 0;
        public int baseLieResistance = 0;
        public int baseLoft          = 0;
        public int maxDurability     = 100;
        public int baseDistance      = 0;

        public float ballSpeedMps   = 75f;
        public float launchAngleDeg = 10.9f;
        public float spinRateRpm    = 2686f;

        public string portraitSprite = "";
        public string portraitFull   = "";
        public string controlSprite  = "";

        public int    startLevel = 0;      // 0 = column absent; the caller falls back to the rarity table
        public int    maxLevel = 119;
        public string info     = "";
        public string infoJa   = "";
    }

    /// <summary>
    /// Parses Clubs.csv text into <see cref="ClubCsvRow"/>s.
    ///
    /// <para>
    /// <b>Comment lines are why this exists.</b> Clubs.csv opens with <c>#</c>-prefixed
    /// provenance lines (which generator wrote the 792 generated rows). The previous reader
    /// took <c>lines[0]</c> as the header, so once those comments landed the header index was
    /// built from prose, every column lookup missed, every row parsed to an empty id, and the
    /// database silently loaded ZERO clubs. The header is the first line that is neither blank
    /// nor a comment — never simply the first line.
    /// </para>
    /// </summary>
    public static class ClubCsvParser
    {
        /// <summary>Lines starting with this (after trimming) are provenance comments, not data.</summary>
        public const char CommentPrefix = '#';

        /// <summary>The bundled roster, with no overlay. Unchanged from Phase 1.</summary>
        public static List<ClubCsvRow> Parse(string? csvText) => Parse(csvText, null);

        /// <summary>
        /// The bundled roster with an admin-published overlay merged on top (SPEC §1).
        ///
        /// <para>
        /// <b>Field-by-field, never row-for-row.</b> A published row is a sparse patch: it overrides
        /// the columns it names and leaves every other column at its bundled value (I4). That is why
        /// the merge runs through <see cref="ContentFields"/> rather than replacing the row — an
        /// operator editing only <c>basePower</c> must not blank the sprite names by omission.
        /// </para>
        /// <para>
        /// Overlay rows whose id is NEW are APPENDED, after the bundled ones, in payload order.
        /// </para>
        /// <para>
        /// Still pure — the overlay is a parameter, not a global — so the whole merge matrix is an
        /// EditMode test. Sprite resolution (and SPEC §5's veto) stays in the runtime adapter,
        /// because it is the only part that needs <c>Resources</c>.
        /// </para>
        /// </summary>
        public static List<ClubCsvRow> Parse(string? csvText, ContentCatalog? overlay)
        {
            var rows = new List<ClubCsvRow>();
            if (string.IsNullOrWhiteSpace(csvText)) return rows;

            var lines = csvText!.Split('\n');

            // Find the header: first line that is neither blank nor a comment.
            int headerLine = -1;
            for (int i = 0; i < lines.Length; i++)
            {
                if (!IsSkippable(lines[i])) { headerLine = i; break; }
            }
            if (headerLine < 0) return rows;

            var idx = BuildHeaderIndex(ParseLine(lines[headerLine]));
            var seen = new HashSet<string>(System.StringComparer.Ordinal);

            for (int i = headerLine + 1; i < lines.Length; i++)
            {
                if (IsSkippable(lines[i])) continue;

                var fields = ParseLine(lines[i].Trim());

                // The id has to come out of the CSV before the overlay can be looked up, so the
                // bundled row is parsed first and patched second. That also gives §5 the pre-merge
                // row it reverts to for free.
                var bundled = ParseRow(ContentFields.Csv(fields, idx));
                if (bundled == null) continue;

                seen.Add(bundled.id);

                ContentRow? patch = null;
                overlay?.ById.TryGetValue(bundled.id, out patch);
                if (patch == null) { rows.Add(bundled); continue; }

                var merged = ParseRow(ContentFields.Csv(fields, idx, patch));
                if (merged == null) { rows.Add(bundled); continue; }

                merged.overlayApplied = true;
                merged.isActive       = patch.IsActive;
                merged.bundled        = bundled;
                rows.Add(merged);
            }

            // Append overlay rows the bundled CSV has never carried.
            if (overlay != null)
            {
                foreach (var row in overlay.Rows)
                {
                    if (seen.Contains(row.Id)) continue;

                    var appended = ParseRow(ContentFields.OverlayOnly(row));
                    if (appended == null) continue;

                    appended.overlayApplied  = true;
                    appended.overlayAppended = true;
                    appended.isActive        = row.IsActive;
                    rows.Add(appended);
                }
            }

            return rows;
        }

        /// <summary>Blank lines and <c>#</c> comment lines carry no data.</summary>
        public static bool IsSkippable(string? line)
        {
            if (string.IsNullOrWhiteSpace(line)) return true;
            return line!.TrimStart().StartsWith(CommentPrefix.ToString());
        }

        private static Dictionary<string, int> BuildHeaderIndex(List<string> headers)
        {
            var idx = new Dictionary<string, int>();
            for (int i = 0; i < headers.Count; i++)
                idx[headers[i].Trim()] = i;
            return idx;
        }

        /// <summary>
        /// One row from whatever <see cref="ContentFields"/> is standing in front of it: a bundled
        /// CSV row, a bundled row patched by an overlay, or an overlay row on its own. The column
        /// names and defaults are declared ONCE, here, which is what keeps a published row and a
        /// bundled row from ever diverging on how a column is read.
        /// </summary>
        public static ClubCsvRow? ParseRow(ContentFields f)
        {
            string id = f.Get("id");
            if (string.IsNullOrEmpty(id)) return null;

            return new ClubCsvRow
            {
                id                = id,
                name              = f.Get("name"),
                type              = ParseType(f.Get("type")),
                rarity            = ParseRarity(f.Get("rarity", "Common")),
                brand             = f.Get("brand"),
                basePower         = f.GetInt("basePower"),
                baseAccuracy      = f.GetInt("baseAccuracy"),
                baseLieResistance = f.GetInt("baseLieResistance"),
                baseLoft          = f.GetInt("baseLoft"),
                maxDurability     = f.GetInt("maxDurability", 100),
                baseDistance      = f.GetInt("baseDistance"),
                ballSpeedMps      = f.GetFloat("ballSpeedMps",   75f),
                launchAngleDeg    = f.GetFloat("launchAngleDeg", 10.9f),
                spinRateRpm       = f.GetFloat("spinRateRpm",    2686f),
                portraitSprite    = f.Get("portraitSprite"),
                portraitFull      = f.Get("portraitFull"),
                controlSprite     = f.Get("controlSprite"),
                startLevel        = f.GetInt("startLevel", 0),
                maxLevel          = f.GetInt("maxLevel", 119),
                info              = f.Get("info"),
                infoJa            = f.Get("info_ja"),
                isActive          = f.IsActive,
            };
        }

        // ── Field parsers ─────────────────────────────────────────────────────

        /// <summary>
        /// CSV type token → <see cref="ClubType"/>. Every shipped token maps explicitly; an
        /// unknown token degrades to Driver rather than throwing, so a future roster column
        /// can never hard-fail the boot.
        /// </summary>
        public static ClubType ParseType(string? s) => (s ?? "").ToLower().Replace(" ", "") switch
        {
            "driver"  => ClubType.Driver,
            "wood"    => ClubType.Wood,
            "iron"    => ClubType.Iron,
            "a.wedge" => ClubType.A_Wedge,
            "p.wedge" => ClubType.P_Wedge,
            "s.wedge" => ClubType.S_Wedge,
            "putter"  => ClubType.Putter,
            _         => ClubType.Driver
        };

        public static CharacterRarity ParseRarity(string? s) => (s ?? "").ToLower() switch
        {
            "common"    => CharacterRarity.Common,
            "uncommon"  => CharacterRarity.Uncommon,
            "rare"      => CharacterRarity.Rare,
            "mythic"    => CharacterRarity.Mythic,
            "legendary" => CharacterRarity.Legendary,
            "supreme"   => CharacterRarity.Supreme,
            _           => CharacterRarity.Common
        };

        /// <summary>Splits one CSV line, honouring quoted fields that contain commas.</summary>
        public static List<string> ParseLine(string line)
        {
            var fields  = new List<string>();
            var current = new System.Text.StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    { current.Append('"'); i++; }
                    else
                    { inQuotes = !inQuotes; }
                }
                else if (c == ',' && !inQuotes)
                { fields.Add(current.ToString()); current.Clear(); }
                else
                { current.Append(c); }
            }

            fields.Add(current.ToString());
            return fields;
        }
    }
}
