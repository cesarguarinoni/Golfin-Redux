// Assets/Scripts/UI/Shop/ShopModel.cs
// Order 517 — stamina_boost_shop
// Data models for the Stamina Boost Shop pillar.
// CSV-first, mirrors the LevelUpCosts / Characters / Clubs convention.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace GolfinRedux.UI.Shop
{
    /// <summary>
    /// Tier of a stamina boost item (maps to HIGH / MEDIUM / LIGHT in CSV).
    /// </summary>
    public enum ShopItemTier
    {
        High,
        Medium,
        Light
    }

    /// <summary>
    /// Runtime model for a single shop (one row of stamina_shops.csv).
    /// </summary>
    [Serializable]
    public class ShopModel
    {
        public string Id            { get; set; } = string.Empty;
        public int    Order         { get; set; }
        public string Region        { get; set; } = string.Empty;
        public string Prefecture    { get; set; } = string.Empty;
        public string City          { get; set; } = string.Empty;
        public string Category      { get; set; } = string.Empty;
        public string Name          { get; set; } = string.Empty;
        public string Tagline       { get; set; } = string.Empty;
        public string HoursOpen     { get; set; } = string.Empty;
        public string HoursClose    { get; set; } = string.Empty;
        public string HoursNote     { get; set; } = string.Empty;
        public string Address       { get; set; } = string.Empty;
        public string MapUrl        { get; set; } = string.Empty;
        public int    WalkMinutes   { get; set; }
        public string DailyBonusStat { get; set; } = string.Empty;
        public int    DailyBonusPct { get; set; }
        public string SignatureName { get; set; } = string.Empty;
        public bool   Featured      { get; set; }
        public string StorefrontImg { get; set; } = string.Empty;
        public string HeroImg       { get; set; } = string.Empty;

        /// <summary>Hours display: "18:00 – 02:00  Open daily" (em dash + note).</summary>
        public string HoursDisplay =>
            string.IsNullOrEmpty(HoursNote)
                ? string.Format("{0} – {1}", HoursOpen, HoursClose)
                : string.Format("{0} – {1}  {2}", HoursOpen, HoursClose, HoursNote);

        /// <summary>Daily bonus chip text: "+15% RECOVERY".</summary>
        public string DailyBonusChipText =>
            string.Format("+{0}% {1}", DailyBonusPct, DailyBonusStat.ToUpperInvariant());
    }

    /// <summary>
    /// Runtime model for a single shop item (one row of stamina_shop_items.csv).
    /// Three items per shop (HIGH / MEDIUM / LIGHT).
    /// </summary>
    [Serializable]
    public class ShopItemModel
    {
        public string       ShopId    { get; set; } = string.Empty;
        public int          ItemOrder { get; set; }
        public ShopItemTier Tier      { get; set; }
        public string       Name      { get; set; } = string.Empty;
        public string       Desc      { get; set; } = string.Empty;
        public int          Stamina   { get; set; }
        public int          RpCost    { get; set; }
        public string       ItemImg   { get; set; } = string.Empty;

        /// <summary>Tier badge text: "HIGH BOOST" / "MEDIUM BOOST" / "LIGHT BOOST".</summary>
        public string TierBadgeText => Tier switch
        {
            ShopItemTier.High   => "HIGH BOOST",
            ShopItemTier.Medium => "MEDIUM BOOST",
            ShopItemTier.Light  => "LIGHT BOOST",
            _                   => string.Empty
        };

        /// <summary>STA display: "+60 STA".</summary>
        public string StaDisplay => string.Format("+{0} STA", Stamina);

        private static ShopItemTier ParseTier(string s) => s.ToUpperInvariant() switch
        {
            "HIGH"   => ShopItemTier.High,
            "MEDIUM" => ShopItemTier.Medium,
            "LIGHT"  => ShopItemTier.Light,
            _        => ShopItemTier.Light
        };

        public static ShopItemTier TierFromString(string s) => ParseTier(s);
    }

    /// <summary>
    /// Catalog: loads both CSVs from Resources/Data/ at first access.
    /// CSV format mirrors LevelUpCosts.csv (header row, comma-separated).
    /// </summary>
    public static class ShopCatalog
    {
        private static List<ShopModel>     _shops;
        private static List<ShopItemModel> _items;

        public static IReadOnlyList<ShopModel>     Shops  => _shops;
        public static IReadOnlyList<ShopItemModel> Items  => _items;

        static ShopCatalog()
        {
            LoadFromCsv();
        }

        private static void LoadFromCsv()
        {
            _shops  = new List<ShopModel>();
            _items  = new List<ShopItemModel>();

            // ── stamina_shops.csv ────────────────────────────────────────────────
            var shopsAsset = Resources.Load<TextAsset>("Data/stamina_shops");
            if (shopsAsset == null)
            {
                Debug.LogError("[ShopCatalog] stamina_shops.csv not found in Resources/Data/.");
            }
            else
            {
                var lines = shopsAsset.text.Split('\n');
                for (int i = 1; i < lines.Length; i++) // skip header
                {
                    var line = lines[i].Trim();
                    if (string.IsNullOrEmpty(line)) continue;
                    var cols = SplitCsvLine(line);
                    if (cols.Length < 20) continue;
                    _shops.Add(new ShopModel
                    {
                        Id             = cols[0],
                        Order          = int.TryParse(cols[1], out var ord) ? ord : 0,
                        Region         = cols[2],
                        Prefecture     = cols[3],
                        City           = cols[4],
                        Category       = cols[5],
                        Name           = cols[6],
                        Tagline        = cols[7],
                        HoursOpen      = cols[8],
                        HoursClose     = cols[9],
                        HoursNote      = cols[10],
                        Address        = cols[11],
                        MapUrl         = cols[12],
                        WalkMinutes    = int.TryParse(cols[13], out var wm) ? wm : 0,
                        DailyBonusStat = cols[14],
                        DailyBonusPct  = int.TryParse(cols[15], out var dp) ? dp : 0,
                        SignatureName  = cols[16],
                        Featured       = string.Equals(cols[17], "true", StringComparison.OrdinalIgnoreCase),
                        StorefrontImg  = cols[18],
                        HeroImg        = cols[19]
                    });
                }
            }

            // ── stamina_shop_items.csv ────────────────────────────────────────────
            var itemsAsset = Resources.Load<TextAsset>("Data/stamina_shop_items");
            if (itemsAsset == null)
            {
                Debug.LogError("[ShopCatalog] stamina_shop_items.csv not found in Resources/Data/.");
            }
            else
            {
                var lines = itemsAsset.text.Split('\n');
                for (int i = 1; i < lines.Length; i++) // skip header
                {
                    var line = lines[i].Trim();
                    if (string.IsNullOrEmpty(line)) continue;
                    var cols = SplitCsvLine(line);
                    if (cols.Length < 8) continue;
                    _items.Add(new ShopItemModel
                    {
                        ShopId    = cols[0],
                        ItemOrder = int.TryParse(cols[1], out var io) ? io : 0,
                        Tier      = ShopItemModel.TierFromString(cols[2]),
                        Name      = cols[3],
                        Desc      = cols[4],
                        Stamina   = int.TryParse(cols[5], out var sta) ? sta : 0,
                        RpCost    = int.TryParse(cols[6], out var rp) ? rp : 0,
                        ItemImg   = cols[7]
                    });
                }
            }

            Debug.Log(string.Format("[ShopCatalog] Loaded {0} shops, {1} items.", _shops.Count, _items.Count));
        }

        /// <summary>Returns all items for the given shop id, sorted by item_order.</summary>
        public static List<ShopItemModel> GetItemsForShop(string shopId)
        {
            var result = new List<ShopItemModel>();
            foreach (var item in Items)
            {
                if (string.Equals(item.ShopId, shopId, StringComparison.Ordinal))
                    result.Add(item);
            }
            result.Sort((a, b) => a.ItemOrder.CompareTo(b.ItemOrder));
            return result;
        }

        /// <summary>Minimal CSV splitter handling quoted fields (e.g. "gin, plum, smoked oak").</summary>
        private static string[] SplitCsvLine(string line)
        {
            var fields  = new List<string>();
            var current = new System.Text.StringBuilder();
            bool inQuotes = false;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '"')
                {
                    inQuotes = !inQuotes;
                }
                else if (c == ',' && !inQuotes)
                {
                    fields.Add(current.ToString().Trim());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }
            fields.Add(current.ToString().Trim());
            return fields.ToArray();
        }
    }
}
