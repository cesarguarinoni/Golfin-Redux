// ─────────────────────────────────────────────────────────────────────────────
// Golfin.Inventory — ClubCsvParser
// The one Clubs.csv reader. Pure text→rows: no Unity objects, no Resources, no
// MonoBehaviour, so the 799-row roster can be asserted from an EditMode test.
// ClubDatabaseCSV is the thin runtime adapter that maps rows onto ClubDataRuntime
// and resolves sprites.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System.Collections.Generic;
using Golfin.Roster;   // CharacterRarity

namespace Golfin.Inventory
{
    /// <summary>
    /// One parsed Clubs.csv row, before sprite resolution. Field-for-field with the CSV
    /// columns so a test can assert the shipped roster without booting Unity.
    /// </summary>
    public class ClubCsvRow
    {
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

        public static List<ClubCsvRow> Parse(string? csvText)
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

            for (int i = headerLine + 1; i < lines.Length; i++)
            {
                if (IsSkippable(lines[i])) continue;
                var row = ParseRow(ParseLine(lines[i].Trim()), idx);
                if (row != null) rows.Add(row);
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

        private static ClubCsvRow? ParseRow(List<string> fields, Dictionary<string, int> idx)
        {
            string Get(string col, string def = "")
                => idx.TryGetValue(col, out int i) && i < fields.Count ? fields[i].Trim() : def;
            int GetInt(string col, int def = 0)
                => int.TryParse(Get(col), out int v) ? v : def;
            float GetFloat(string col, float def = 0f)
                => float.TryParse(Get(col), System.Globalization.NumberStyles.Float,
                   System.Globalization.CultureInfo.InvariantCulture, out float v) ? v : def;

            string id = Get("id");
            if (string.IsNullOrEmpty(id)) return null;

            return new ClubCsvRow
            {
                id                = id,
                name              = Get("name"),
                type              = ParseType(Get("type")),
                rarity            = ParseRarity(Get("rarity", "Common")),
                brand             = Get("brand"),
                basePower         = GetInt("basePower"),
                baseAccuracy      = GetInt("baseAccuracy"),
                baseLieResistance = GetInt("baseLieResistance"),
                baseLoft          = GetInt("baseLoft"),
                maxDurability     = GetInt("maxDurability", 100),
                baseDistance      = GetInt("baseDistance"),
                ballSpeedMps      = GetFloat("ballSpeedMps",   75f),
                launchAngleDeg    = GetFloat("launchAngleDeg", 10.9f),
                spinRateRpm       = GetFloat("spinRateRpm",    2686f),
                portraitSprite    = Get("portraitSprite"),
                portraitFull      = Get("portraitFull"),
                controlSprite     = Get("controlSprite"),
                maxLevel          = GetInt("maxLevel", 119),
                info              = Get("info"),
                infoJa            = Get("info_ja"),
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
