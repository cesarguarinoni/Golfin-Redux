// Assets/Scripts/UI/Shop/GeneralShopModel.cs
// Order 610 — general_shop_ui (Phase B)
// Catalog data layer for the Rewards Center STORE tab. CSV-first, mirrors ShopCatalog (517).

using System;
using System.Collections.Generic;
using UnityEngine;

namespace GolfinRedux.UI.Shop
{
    /// <summary>Which inventory system an entry grants into (v1: clubs + balls; D1).</summary>
    public enum ShopCategory
    {
        Club,
        Ball
    }

    /// <summary>
    /// One purchasable row of shop_catalog.csv. RefId points at a clubId (ClubDatabaseCSV) or
    /// ballId (BallDatabaseCSV); the card resolves name/stats/sprites from that DB at bind time.
    /// RP prices are authored here (D2 re-token of the Figma $ prices — design values, not $→RP).
    /// </summary>
    [Serializable]
    public class ShopCatalogEntry
    {
        public string       EntryId    { get; set; } = string.Empty;
        public ShopCategory Category   { get; set; } = ShopCategory.Club;
        public string       RefId      { get; set; } = string.Empty;
        public int          RpCost     { get; set; }
        public int          SaleRpCost { get; set; }   // 0 or ==RpCost → no sale strike
        public int          SortOrder  { get; set; }
        public bool         Popular    { get; set; }   // v1 unused (POPULAR curation grayed)
        public bool         Offer      { get; set; }   // v1 unused (OFFERS curation grayed)

        /// <summary>True when a discounted sale price applies (strike-through affordance).</summary>
        public bool HasSale => SaleRpCost > 0 && SaleRpCost < RpCost;

        /// <summary>The price actually charged: sale price when on sale, else the list price.</summary>
        public int EffectiveRpCost => HasSale ? SaleRpCost : RpCost;
    }

    /// <summary>
    /// Catalog: loads shop_catalog.csv from Resources/Data/ on first access.
    /// CSV format mirrors LevelUpCosts.csv (header row, comma-separated).
    /// </summary>
    public static class GeneralShopCatalog
    {
        private static List<ShopCatalogEntry> _entries;

        public static IReadOnlyList<ShopCatalogEntry> Entries
        {
            get { EnsureLoaded(); return _entries; }
        }

        /// <summary>Entries for a category (null = ALL), sorted by SortOrder.</summary>
        public static List<ShopCatalogEntry> GetByCategory(ShopCategory? category)
        {
            EnsureLoaded();
            var result = new List<ShopCatalogEntry>();
            foreach (var e in _entries)
                if (category == null || e.Category == category.Value)
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
            _entries = new List<ShopCatalogEntry>();
            var asset = Resources.Load<TextAsset>("Data/shop_catalog");
            if (asset == null)
            {
                Debug.LogError("[GeneralShopCatalog] shop_catalog.csv not found in Resources/Data/.");
                return;
            }

            var lines = asset.text.Split('\n');
            for (int i = 1; i < lines.Length; i++) // skip header
            {
                var line = lines[i].Trim();
                if (string.IsNullOrEmpty(line)) continue;
                var cols = line.Split(',');
                if (cols.Length < 8) continue;
                _entries.Add(new ShopCatalogEntry
                {
                    EntryId    = cols[0].Trim(),
                    Category   = ParseCategory(cols[1]),
                    RefId      = cols[2].Trim(),
                    RpCost     = int.TryParse(cols[3], out var rp)  ? rp  : 0,
                    SaleRpCost = int.TryParse(cols[4], out var srp) ? srp : 0,
                    SortOrder  = int.TryParse(cols[5], out var so)  ? so  : 0,
                    Popular    = string.Equals(cols[6].Trim(), "true", StringComparison.OrdinalIgnoreCase),
                    Offer      = string.Equals(cols[7].Trim(), "true", StringComparison.OrdinalIgnoreCase),
                });
            }

            Debug.Log($"[GeneralShopCatalog] Loaded {_entries.Count} catalog entries.");
        }

        private static ShopCategory ParseCategory(string s) =>
            s.Trim().ToLowerInvariant() == "ball" ? ShopCategory.Ball : ShopCategory.Club;

        /// <summary>Test/hot-reload hook: forces a re-read on next access.</summary>
        public static void Reload() { _entries = null; }
    }
}
