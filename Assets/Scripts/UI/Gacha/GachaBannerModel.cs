// Assets/Scripts/UI/Gacha/GachaBannerModel.cs
// gacha_screen Stage 2 — §3a Banner Catalog
// Mirrors GeneralShopCatalog exactly: static, Resources.Load<TextAsset>, header-INDEXED quote-aware
// parse (gacha_admin_catalogs §3 — was positional Split(',') through gacha_screen Stage 2),
// Reload() hook, malformed rows skipped, GetLiveBanners() = Active && EndUtc > UtcNow by SortOrder.
//
// Internal testable seams (stage2 iter-3):
//   ParseCsv(string csvText)                                — extracted from LoadFromCsv()
//   GetLiveBanners(IEnumerable<GachaBannerEntry>, DateTime) — extracted from GetLiveBanners()
// Both seams are exercised by GachaStage2Tests via reflection; InternalsVisibleTo exposes them
// for direct (non-reflection) access if the test assembly is ever given a compile-time reference.

using System;
using System.Collections.Generic;
using UnityEngine;

// Must appear after using directives (C# grammar: using-directives precede global-attributes).
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("GolfinRedux.Tests.EditMode")]

namespace GolfinRedux.UI.Gacha
{
    /// <summary>
    /// One row of gacha_banners.csv. The nine columns this build reads (locked D4):
    ///   bannerId, nameKey, artSprite, costX1, costX10, endUtc, rulesUrl, sortOrder, active
    ///
    /// The CSV also carries thirteen admin-catalog columns (startUtc, poolId, ticketType,
    /// pityThreshold, pityMinRarity, guaranteeMinRarityX10, maxPullsPerPlayer, artUrl,
    /// nameEn, nameJa, taglineEn, taglineJa, featuredRefIds) which ParseCsv deliberately
    /// IGNORES — they land on this type in `gacha_client_real_pull` (plan §6), not here.
    /// </summary>
    [Serializable]
    public class GachaBannerEntry
    {
        public string   BannerId  { get; set; } = string.Empty;
        public string   NameKey   { get; set; } = string.Empty;
        /// <summary>Filename (no path, no extension). Load via Resources.Load&lt;Sprite&gt;("Art/Gacha/Banners/" + ArtSprite).</summary>
        public string   ArtSprite { get; set; } = string.Empty;
        public int      CostX1    { get; set; }
        public int      CostX10   { get; set; }
        /// <summary>ISO-8601 UTC string parsed to DateTime (Kind=Utc).</summary>
        public DateTime EndUtc    { get; set; } = DateTime.MaxValue;
        public string   RulesUrl  { get; set; } = string.Empty;
        public int      SortOrder { get; set; }
        public bool     Active    { get; set; }

        /// <summary>True when Active and the banner has not yet expired relative to device UTC clock.</summary>
        public bool IsLive => Active && EndUtc > DateTime.UtcNow;
    }

    /// <summary>
    /// Static catalog: loads gacha_banners.csv from Resources/Data/ on first access.
    /// Mirrors GeneralShopCatalog pattern (Order 610).
    /// </summary>
    public static class GachaBannerCatalog
    {
        private static List<GachaBannerEntry> _entries;

        public static IReadOnlyList<GachaBannerEntry> Entries
        {
            get { EnsureLoaded(); return _entries; }
        }

        /// <summary>
        /// Returns Active banners whose EndUtc is in the future (relative to DateTime.UtcNow),
        /// sorted ascending by SortOrder.
        /// </summary>
        public static List<GachaBannerEntry> GetLiveBanners()
        {
            EnsureLoaded();
            return GetLiveBanners(_entries, DateTime.UtcNow);
        }

        /// <summary>
        /// Testable seam: filter and sort an arbitrary entry collection against a supplied clock.
        /// Returns Active entries whose EndUtc &gt; nowUtc, sorted by SortOrder ascending.
        /// Called by the public no-arg overload and by GachaStage2Tests via reflection.
        /// </summary>
        internal static List<GachaBannerEntry> GetLiveBanners(IEnumerable<GachaBannerEntry> entries, DateTime nowUtc)
        {
            var result = new List<GachaBannerEntry>();
            foreach (var e in entries)
                if (e.Active && e.EndUtc > nowUtc)
                    result.Add(e);
            result.Sort((a, b) => a.SortOrder.CompareTo(b.SortOrder));
            return result;
        }

        private static void EnsureLoaded()
        {
            if (_entries != null) return;
            LoadFromCsv();
        }

        private static void LoadFromCsv()
        {
            var asset = Resources.Load<TextAsset>("Data/gacha_banners");
            if (asset == null)
            {
                Debug.LogError("[GachaBannerCatalog] gacha_banners.csv not found in Resources/Data/.");
                _entries = new List<GachaBannerEntry>();
                return;
            }
            _entries = ParseCsv(asset.text);
            Debug.Log($"[GachaBannerCatalog] Loaded {_entries.Count} banner entries.");
        }

        /// <summary>
        /// Testable seam: parse a CSV string into a list of GachaBannerEntry objects.
        ///
        /// HEADER-INDEXED AND QUOTE-AWARE since `gacha_admin_catalogs` (§3). The nine fields
        /// below are read BY COLUMN NAME off line 0, not by position, and unknown columns are
        /// ignored — so the thirteen columns the admin catalog added (startUtc, poolId,
        /// ticketType, pity*, art/name/tagline per locale, featuredRefIds) pass straight
        /// through this parser without it knowing they exist. Reading them is spec C's job.
        ///
        /// Why it had to change: `export_content.py` writes QUOTE_MINIMAL canonical form, and
        /// the bundled floor of the next build is whatever the exporter wrote. A `taglineEn`
        /// containing a comma is one quoted field to the exporter and two columns to
        /// `line.Split(',')` — every later column would shift by one and `active` would be
        /// read out of `featuredRefIds`. Same reasoning as GeneralShopCatalog.ParseCsvLine.
        ///
        /// A row is SKIPPED when its bannerId is blank, or when it carries fewer fields than
        /// the header (a truncated row is malformed — the behaviour the old `cols.Length &lt; 9`
        /// guard had, kept). A column the HEADER does not name defaults to empty (I4) rather
        /// than dropping the row: a narrower published header must not blank the catalog.
        /// If endUtc is unparseable, the entry defaults to DateTime.MaxValue.
        ///
        /// Called by LoadFromCsv() and by GachaStage2Tests via reflection.
        /// </summary>
        internal static List<GachaBannerEntry> ParseCsv(string csvText)
        {
            var result = new List<GachaBannerEntry>();
            var lines  = (csvText ?? string.Empty).Split('\n');
            if (lines.Length < 2) return result;

            var header = ParseCsvLine(lines[0].Trim());
            var index  = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int c = 0; c < header.Count; c++) index[header[c].Trim()] = c;

            for (int i = 1; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (string.IsNullOrEmpty(line)) continue;

                var cols = ParseCsvLine(line);
                if (cols.Count < header.Count) continue; // truncated row — skip without throwing

                string Field(string column)
                {
                    if (!index.TryGetValue(column, out int at)) return string.Empty; // absent column
                    return at < cols.Count ? cols[at].Trim() : string.Empty;
                }

                var bannerId = Field("bannerId");
                if (string.IsNullOrEmpty(bannerId)) continue; // a row with no id is not a banner

                var entry = new GachaBannerEntry
                {
                    BannerId  = bannerId,
                    NameKey   = Field("nameKey"),
                    ArtSprite = Field("artSprite"),
                    CostX1    = int.TryParse(Field("costX1"),    out var cx1) ? cx1 : 0,
                    CostX10   = int.TryParse(Field("costX10"),   out var cx10) ? cx10 : 0,
                    RulesUrl  = Field("rulesUrl"),
                    SortOrder = int.TryParse(Field("sortOrder"), out var so) ? so : 0,
                    Active    = string.Equals(Field("active"), "true", StringComparison.OrdinalIgnoreCase),
                };

                // Parse EndUtc — on malformed date, default to MaxValue (never expires) rather than throwing.
                var endRaw = Field("endUtc");
                if (DateTime.TryParse(endRaw,
                        System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.AssumeUniversal,
                        out var endUtc))
                {
                    entry.EndUtc = endUtc.ToUniversalTime();
                }
                else
                {
                    Debug.LogWarning($"[GachaBannerCatalog] Row {i}: could not parse endUtc '{endRaw}'; using DateTime.MaxValue.");
                    entry.EndUtc = DateTime.MaxValue;
                }

                result.Add(entry);
            }
            return result;
        }

        /// <summary>
        /// Splits one CSV line on commas, honouring double-quoted fields so a field may itself
        /// contain commas. A literal quote inside a quoted field is <c>""</c>.
        ///
        /// Same logic as <c>ModesDatabaseCSV.ParseCsvLine</c> / <c>GeneralShopCatalog.ParseCsvLine</c>
        /// / <c>TournamentCsvLoader</c>. Copied rather than shared because there is still no public
        /// splitter to share: <c>Golfin.Content.ContentFields</c> reads an ALREADY-SPLIT field list
        /// and the other two copies are private to their loaders. Lifting all four into one helper
        /// is a refactor of four files, which this task is not.
        /// </summary>
        private static List<string> ParseCsvLine(string line)
        {
            var fields  = new List<string>();
            var current = new System.Text.StringBuilder();
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

        /// <summary>Test / hot-reload hook: forces re-read on next access.</summary>
        public static void Reload() { _entries = null; }
    }
}
