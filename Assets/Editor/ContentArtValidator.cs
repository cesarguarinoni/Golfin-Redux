// ─────────────────────────────────────────────────────────────────────────────
// ContentArtValidator — "what does this build withhold, and why?" (content_two_way §5)
//
// A REPORT, NEVER A GATE. A character whose data is published in the admin and
// whose art lands next week is a LEGITIMATE state — §4 is what makes it safe
// (the row is withheld from every visible list instead of drawing a null
// sprite). Failing the build for it would recreate the "validator gets switched
// off" problem, so this only ever writes a file and logs a warning.
//
// It answers the question §4's runtime warning cannot: the runtime line names
// what the LOADED catalogs withheld, one loader at a time, in a log nobody reads
// after the fact. This walks all four bundled CSVs at BUILD time and leaves the
// list in Docs/Reports/content_art_<build>.txt, so the archive carries a record
// of what it ships without.
//
// SAME RESOLUTION AS THE RUNTIME. Every column is resolved with the identical
// `Resources.Load<Sprite>(folder + "/" + name)` the loaders perform, from folder
// literals copied from those loaders (cited per catalog below). A validator that
// resolved differently from the game would be worse than no validator.
//
// CLUBS ARE INCLUDED BUT JUDGED DIFFERENTLY. Clubs keep the Placeholder policy
// by decision (content_two_way §4): a club with missing art still renders, using
// the shared Placeholder sprite. So a club miss is reported as "Placeholder",
// not "withheld" — the operator still wants the list, because the placeholder is
// a stand-in and not an outcome anybody signed off.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Golfin.EditorTools
{
    public static class ContentArtValidator
    {
        const string Tag = "[ContentArt]";
        const string ReportDir = "Docs/Reports";

        // ── The four bundled catalogs, and where their sprites live ─────────
        //
        // Folder literals are COPIES of the loader constants, cited so a rename
        // there is findable from here:
        //   Portraits/Thumbnails, Portraits/FullBody   CharacterDatabaseCSV.cs:36-37
        //   Items/Thumbnails,     Items/Full           ItemDatabaseCSV.cs:25-26
        //   Balls/Thumbnails,     Balls/Full           BallDatabaseCSV.cs:25-26
        //   Clubs/Portraits, Clubs/Full, Clubs/Controls  ClubDatabaseCSV.cs:39-41

        /// <summary>One sprite-bearing column of one catalog.</summary>
        sealed class Column
        {
            public readonly string Name;
            public readonly string Folder;

            /// <summary>True for the column that decides <c>renderable</c> (§4). A miss here
            /// withholds the row; a miss on a secondary column only degrades it.</summary>
            public readonly bool Primary;

            public Column(string name, string folder, bool primary = false)
            {
                Name = name;
                Folder = folder;
                Primary = primary;
            }
        }

        sealed class CatalogSpec
        {
            public readonly string Name;
            public readonly string CsvPath;
            public readonly string IdColumn;
            public readonly Column[] Columns;

            /// <summary>Clubs: a miss falls back to the shared Placeholder sprite and the row
            /// still renders (§4, decision of record). Everything else withholds the row.</summary>
            public readonly bool PlaceholderPolicy;

            public CatalogSpec(string name, string csvPath, string idColumn, bool placeholderPolicy,
                               params Column[] columns)
            {
                Name = name;
                CsvPath = csvPath;
                IdColumn = idColumn;
                PlaceholderPolicy = placeholderPolicy;
                Columns = columns;
            }
        }

        static readonly CatalogSpec[] Catalogs =
        {
            new CatalogSpec("characters", "Assets/Data/Characters.csv", "id", false,
                new Column("portraitSprite", "Portraits/Thumbnails", primary: true),
                new Column("portraitFull",   "Portraits/FullBody")),

            new CatalogSpec("items", "Assets/Data/Items.csv", "id", false,
                new Column("thumbnailSprite", "Items/Thumbnails", primary: true),
                new Column("fullSprite",      "Items/Full")),

            new CatalogSpec("balls", "Assets/Data/Balls.csv", "id", false,
                new Column("thumbnailSprite", "Balls/Thumbnails", primary: true),
                new Column("fullSprite",      "Balls/Full")),

            new CatalogSpec("clubs", "Assets/Resources/Data/Clubs.csv", "id", true,
                new Column("portraitSprite", "Clubs/Portraits", primary: true),
                new Column("portraitFull",   "Clubs/Full"),
                new Column("controlSprite",  "Clubs/Controls")),
        };

        // ── Findings ────────────────────────────────────────────────────────

        public sealed class Miss
        {
            public string Catalog = "";
            public string RowId = "";
            public string Column = "";
            public string SpriteName = "";
            public bool Primary;

            /// <summary>"withheld" | "degraded" | "Placeholder" — what the row DOES at runtime.</summary>
            public string Verdict = "";
        }

        public sealed class Report
        {
            public readonly List<Miss> misses = new List<Miss>();
            public readonly List<string> errors = new List<string>();
            public readonly Dictionary<string, int> rowCounts = new Dictionary<string, int>(StringComparer.Ordinal);

            /// <summary>Rows that will not render at all — the number that matters (§4).</summary>
            public int WithheldRowCount =>
                misses.Where(m => m.Verdict == "withheld").Select(m => m.Catalog + "/" + m.RowId).Distinct().Count();

            public int PlaceholderRowCount =>
                misses.Where(m => m.Verdict == "Placeholder").Select(m => m.Catalog + "/" + m.RowId).Distinct().Count();

            public string ToText(int build)
            {
                var sb = new StringBuilder();
                // Build number: yes — it is what dates the contents, and it costs one line of
                // diff per build. Wall-clock timestamp: NO. It would change on every
                // regeneration and make the file dirty on builds whose coverage is identical,
                // which is the churn the stable filename exists to remove. Git already knows
                // when the commit happened.
                sb.AppendLine($"content_art — build {build}   (GOLFIN/Content/Validate Catalog Art)");
                sb.AppendLine();
                sb.AppendLine("WARNING-ONLY REPORT. A row whose data is published and whose art ships in a later");
                sb.AppendLine("build is a legitimate state: content_two_way §4 withholds it from every visible list");
                sb.AppendLine("instead of drawing a blank. This file records what THIS build withholds.");
                sb.AppendLine();
                sb.AppendLine($"  {Summary()}");
                sb.AppendLine();

                foreach (var spec in Catalogs)
                {
                    var rows = misses.Where(m => m.Catalog == spec.Name).ToList();
                    int total = rowCounts.TryGetValue(spec.Name, out int n) ? n : 0;
                    sb.AppendLine($"── {spec.Name}  ({total} row(s), {rows.Select(r => r.RowId).Distinct().Count()} with missing art)");
                    if (rows.Count == 0)
                    {
                        sb.AppendLine("   every sprite column resolves.");
                        sb.AppendLine();
                        continue;
                    }

                    foreach (var m in rows.OrderBy(r => r.RowId, StringComparer.Ordinal)
                                          .ThenBy(r => r.Column, StringComparer.Ordinal))
                    {
                        string name = string.IsNullOrEmpty(m.SpriteName) ? "(empty)" : m.SpriteName;
                        sb.AppendLine($"   {m.RowId,-28} {m.Column,-16} {name,-32} → {m.Verdict}");
                    }
                    sb.AppendLine();
                }

                if (errors.Count > 0)
                {
                    sb.AppendLine("── could not be read");
                    foreach (var e in errors) sb.AppendLine($"   {e}");
                    sb.AppendLine();
                }

                sb.AppendLine("verdicts:");
                sb.AppendLine("  withheld    — primary sprite missing; the row is absent from every visible list (§4).");
                sb.AppendLine("  degraded    — a secondary sprite is missing; the row renders, one slot is empty.");
                sb.AppendLine("  Placeholder — clubs only; the row renders with the shared Placeholder sprite (§4 decision).");
                return sb.ToString();
            }

            public string Summary() =>
                $"{WithheldRowCount} row(s) withheld, {PlaceholderRowCount} club row(s) on Placeholder, " +
                $"{misses.Count} missing sprite reference(s) across {Catalogs.Length} catalogs" +
                (errors.Count > 0 ? $", {errors.Count} catalog(s) unreadable" : "");
        }

        // ── Validation ──────────────────────────────────────────────────────

        /// <summary>Walks all four bundled CSVs. Never throws: a catalog that cannot be read is
        /// recorded in <see cref="Report.errors"/>, because a report that dies halfway is a report
        /// that turns into a build failure the first time somebody moves a file.</summary>
        public static Report ValidateAll()
        {
            var report = new Report();
            string root = Directory.GetParent(Application.dataPath)!.FullName;

            foreach (var spec in Catalogs)
            {
                try
                {
                    ValidateCatalog(spec, root, report);
                }
                catch (Exception e)
                {
                    report.errors.Add($"{spec.Name} ({spec.CsvPath}): {e.GetType().Name}: {e.Message}");
                }
            }

            return report;
        }

        static void ValidateCatalog(CatalogSpec spec, string root, Report report)
        {
            string path = Path.Combine(root, spec.CsvPath);
            if (!File.Exists(path))
            {
                report.errors.Add($"{spec.Name}: {spec.CsvPath} not found");
                return;
            }

            var lines = File.ReadAllText(path).Replace("\r\n", "\n").Split('\n');
            List<string>? header = null;
            var index = new Dictionary<string, int>(StringComparer.Ordinal);
            int rows = 0;

            foreach (var raw in lines)
            {
                string line = raw.Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith("#", StringComparison.Ordinal)) continue;

                var fields = ParseCsvLine(line);
                if (header == null)
                {
                    header = fields;
                    for (int i = 0; i < header.Count; i++) index[header[i].Trim()] = i;
                    continue;
                }

                string rowId = Field(fields, index, spec.IdColumn);
                if (string.IsNullOrEmpty(rowId)) continue;
                rows++;

                foreach (var column in spec.Columns)
                {
                    if (!index.ContainsKey(column.Name)) continue;   // column absent from this CSV
                    string spriteName = Field(fields, index, column.Name);

                    // An EMPTY name resolves to null at runtime exactly like a wrong one, so it is
                    // the same finding — the loaders early-return null on IsNullOrEmpty.
                    if (!string.IsNullOrEmpty(spriteName) && Resolves(column.Folder, spriteName)) continue;

                    report.misses.Add(new Miss
                    {
                        Catalog = spec.Name,
                        RowId = rowId,
                        Column = column.Name,
                        SpriteName = spriteName,
                        Primary = column.Primary,
                        Verdict = spec.PlaceholderPolicy ? "Placeholder"
                                : column.Primary ? "withheld"
                                : "degraded",
                    });
                }
            }

            report.rowCounts[spec.Name] = rows;
        }

        // Memoised for the same reason ContentSpriteGuard memoises: 799 club rows share a few
        // hundred distinct sprite names, and the uncached form is thousands of Resources.Load calls.
        static readonly Dictionary<string, bool> Resolved = new Dictionary<string, bool>(StringComparer.Ordinal);

        static bool Resolves(string folder, string name)
        {
            string key = folder + "/" + name;
            if (Resolved.TryGetValue(key, out bool hit)) return hit;
            hit = Resources.Load<Sprite>(key) != null;
            Resolved[key] = hit;
            return hit;
        }

        static string Field(List<string> fields, Dictionary<string, int> index, string column)
            => index.TryGetValue(column, out int i) && i < fields.Count ? fields[i].Trim() : string.Empty;

        /// <summary>Quote-aware single-line CSV split — the same shape every loader uses.</summary>
        static List<string> ParseCsvLine(string line)
        {
            var fields = new List<string>();
            var current = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"') { current.Append('"'); i++; }
                    else inQuotes = !inQuotes;
                }
                else if (c == ',' && !inQuotes) { fields.Add(current.ToString()); current.Clear(); }
                else current.Append(c);
            }

            fields.Add(current.ToString());
            return fields;
        }

        // ── Output ──────────────────────────────────────────────────────────

        /// <summary>Writes <c>Docs/Reports/content_art_&lt;build&gt;.txt</c> and returns its
        /// repo-relative path, or null when the write failed (which is never a build failure).</summary>
        public static string? WriteReport(Report report, int build)
        {
            try
            {
                string root = Directory.GetParent(Application.dataPath)!.FullName;
                string dir = Path.Combine(root, ReportDir);
                Directory.CreateDirectory(dir);
                // ONE STABLE FILENAME, not content_art_<build>.txt (Cesar, 2026-08-27).
                // The build number belongs INSIDE the file, never in the name: a per-build name
                // makes every archive add a NEW ~700-line blob instead of modifying one, so git
                // stores near-identical copies and shows no diffs at all — the worst of both
                // ends. With a stable name an unchanged coverage picture produces NO diff, a
                // character that starts or stops being withheld produces a two-line one (which
                // is the signal §5 wanted), and "what did build N withhold" is still answerable,
                // better than before: `git show <sha>:Docs/Reports/content_art.txt`.
                string rel = $"{ReportDir}/content_art.txt";
                File.WriteAllText(Path.Combine(root, rel), report.ToText(build));
                return rel;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"{Tag} could not write the report: {e.GetType().Name}: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// The build number the report is filed under. `git rev-list --count HEAD` is the number
        /// the binary will carry (BuildStampGenerator derives it the same way); the bundled stamp
        /// is the fallback for a working copy without git, and 0 is the fallback for neither.
        /// </summary>
        public static int BuildNumber()
        {
            int git = GolfinRedux.BuildEditor.BuildStampGenerator.GitRevCount();
            if (git > 0) return git;

            var stamp = Resources.Load<TextAsset>(Golfin.Content.ContentBuildNumber.ResourcePath);
            int parsed = Golfin.Content.ContentBuildNumber.Parse(stamp != null ? stamp.text : null);
            return Mathf.Max(0, parsed);
        }

        /// <summary>Run it, write the report, log the summary. Returns the report path or null.
        /// <b>Never fails anything</b> — this is the entry point CIBuild calls.</summary>
        public static string? RunAndReport()
        {
            var report = ValidateAll();
            int build = BuildNumber();
            string? path = WriteReport(report, build);

            string where = path != null ? $" Full list: {path}" : "";
            if (report.misses.Count == 0 && report.errors.Count == 0)
                Debug.Log($"{Tag} every sprite column of every bundled catalog row resolves.{where}");
            else
                Debug.LogWarning($"{Tag} {report.Summary()}.{where}");

            return path;
        }

        [MenuItem("GOLFIN/Content/Validate Catalog Art")]
        public static void ValidateMenu()
        {
            // Console, never a dialog — Cesar's standing rule on editor popups.
            Resolved.Clear();
            RunAndReport();
        }
    }
}
