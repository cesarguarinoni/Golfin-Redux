#nullable enable
using System.Collections.Generic;
using System.Text;

namespace GolfinRedux.UI.MissionSelection
{
    /// <summary>
    /// The mission CSVs, parsed. Spec: missions_v1 §A1.
    ///
    /// A small parser rather than a shared one because these files have two properties the
    /// project's other CSV readers do not both handle: `#` COMMENT LINES (every mission CSV
    /// carries a header block explaining what the columns mean and, for start areas, why some
    /// rows are blank) and QUOTED COMMA-BEARING FIELDS (`"ban:Driver,Wood"` is one cell, and a
    /// naive split turns one loadout into two).
    ///
    /// It matches `Tools/content/catalogs.py` — the same QUOTE_MINIMAL dialect the exporter
    /// writes — so a round-tripped file reads back identically here.
    /// </summary>
    public static class MissionCsv
    {
        /// <summary>Rows as column→value maps, in file order. Comment and blank lines dropped.</summary>
        public static List<Dictionary<string, string>> Parse(string text)
        {
            var rows = new List<Dictionary<string, string>>();
            if (string.IsNullOrEmpty(text)) return rows;

            string[]? header = null;
            foreach (string raw in text.Replace("\r\n", "\n").Split('\n'))
            {
                string line = raw;
                if (line.Length == 0) continue;
                if (line.TrimStart().StartsWith("#")) continue;   // the explanatory header block

                string[] cells = SplitLine(line);
                if (header == null) { header = cells; continue; }

                var row = new Dictionary<string, string>(header.Length);
                for (int i = 0; i < header.Length; i++)
                    row[header[i]] = i < cells.Length ? cells[i] : "";
                rows.Add(row);
            }
            return rows;
        }

        /// <summary>One physical line → fields. RFC4180-ish: `""` inside quotes is a literal quote.</summary>
        public static string[] SplitLine(string line)
        {
            var outCells = new List<string>();
            var sb = new StringBuilder();
            bool quoted = false;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (quoted)
                {
                    if (c == '"' && i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                    else if (c == '"') quoted = false;
                    else sb.Append(c);
                }
                else if (c == '"') quoted = true;
                else if (c == ',') { outCells.Add(sb.ToString()); sb.Length = 0; }
                else sb.Append(c);
            }
            outCells.Add(sb.ToString());
            return outCells.ToArray();
        }
    }
}
