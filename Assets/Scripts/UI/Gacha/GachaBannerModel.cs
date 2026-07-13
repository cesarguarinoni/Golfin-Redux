// Assets/Scripts/UI/Gacha/GachaBannerModel.cs
// gacha_screen Stage 2 — §3a Banner Catalog
// Mirrors GeneralShopCatalog exactly: static, Resources.Load<TextAsset>, header-skip parse,
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
    /// One row of gacha_banners.csv. Columns (locked D4):
    ///   bannerId, nameKey, artSprite, costX1, costX10, endUtc, rulesUrl, sortOrder, active
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
        /// Header row (index 0) is skipped. Malformed rows (fewer than 9 columns) are skipped
        /// without throwing. If endUtc is unparseable, the entry defaults to DateTime.MaxValue.
        /// Called by LoadFromCsv() and by GachaStage2Tests via reflection.
        /// </summary>
        internal static List<GachaBannerEntry> ParseCsv(string csvText)
        {
            var result = new List<GachaBannerEntry>();
            var lines  = csvText.Split('\n');
            for (int i = 1; i < lines.Length; i++) // skip header row
            {
                var line = lines[i].Trim();
                if (string.IsNullOrEmpty(line)) continue;
                var cols = line.Split(',');
                if (cols.Length < 9) continue; // malformed row — skip without throwing

                var entry = new GachaBannerEntry
                {
                    BannerId  = cols[0].Trim(),
                    NameKey   = cols[1].Trim(),
                    ArtSprite = cols[2].Trim(),
                    CostX1    = int.TryParse(cols[3], out var cx1)  ? cx1  : 0,
                    CostX10   = int.TryParse(cols[4], out var cx10) ? cx10 : 0,
                    // cols[5] = endUtc ISO-8601 (parsed below)
                    RulesUrl  = cols[6].Trim(),
                    SortOrder = int.TryParse(cols[7], out var so)   ? so   : 0,
                    Active    = string.Equals(cols[8].Trim(), "true", StringComparison.OrdinalIgnoreCase),
                };

                // Parse EndUtc — on malformed date, default to MaxValue (never expires) rather than throwing.
                if (DateTime.TryParse(cols[5].Trim(),
                        System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.AssumeUniversal,
                        out var endUtc))
                {
                    entry.EndUtc = endUtc.ToUniversalTime();
                }
                else
                {
                    Debug.LogWarning($"[GachaBannerCatalog] Row {i}: could not parse endUtc '{cols[5]}'; using DateTime.MaxValue.");
                    entry.EndUtc = DateTime.MaxValue;
                }

                result.Add(entry);
            }
            return result;
        }

        /// <summary>Test / hot-reload hook: forces re-read on next access.</summary>
        public static void Reload() { _entries = null; }
    }
}
