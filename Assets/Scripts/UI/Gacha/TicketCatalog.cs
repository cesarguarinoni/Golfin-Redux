// Assets/Scripts/UI/Gacha/TicketCatalog.cs
// Mirrors GachaBannerModel.cs pattern: static catalog, EnsureLoaded, ParseCsv seam.
// DO NOT redeclare [assembly: InternalsVisibleTo] — already in GachaBannerModel.cs.
#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace GolfinRedux.UI.Gacha
{
    /// <summary>One row from tickets.csv.
    /// Public fields (not auto-properties) so EditMode tests can locate them via reflection
    /// (BindingFlags.Public | BindingFlags.Instance on GetField).
    /// </summary>
    public class TicketEntry
    {
        public TicketType TicketType;
        public string     NameKey    = "";
        public string     IconSprite = "";

        public TicketEntry(TicketType ticketType, string nameKey, string iconSprite)
        {
            TicketType  = ticketType;
            NameKey     = nameKey;
            IconSprite  = iconSprite;
        }
    }

    /// <summary>
    /// Static catalog loaded from Assets/Resources/Data/tickets.csv.
    /// CSV columns: ticketType (int), nameKey, iconSprite
    /// One row per TicketType; enum int values are frozen — never reorder.
    /// </summary>
    public static class TicketCatalog
    {
        private static List<TicketEntry>? _entries;

        public static IReadOnlyList<TicketEntry> Entries
        {
            get { EnsureLoaded(); return _entries!; }
        }

        public static TicketEntry? Get(TicketType type)
        {
            EnsureLoaded();
            foreach (var e in _entries!)
                if (e.TicketType == type) return e;
            return null;
        }

        private static void EnsureLoaded()
        {
            if (_entries != null) return;
            var asset = Resources.Load<TextAsset>("Data/tickets");
            if (asset == null)
            {
                Debug.LogError("[TicketCatalog] tickets.csv not found at Resources/Data/tickets");
                _entries = new List<TicketEntry>();
                return;
            }
            _entries = ParseCsv(asset.text);
        }

        /// <summary>Exposed for EditMode unit tests.</summary>
        internal static List<TicketEntry> ParseCsv(string csvText)
        {
            var result = new List<TicketEntry>();
            if (string.IsNullOrWhiteSpace(csvText)) return result;

            var lines = csvText.Split('\n');
            for (int i = 1; i < lines.Length; i++) // skip header
            {
                var line = lines[i].Trim();
                if (string.IsNullOrEmpty(line)) continue;
                var cols = line.Split(',');
                if (cols.Length < 3) continue;
                if (!int.TryParse(cols[0].Trim(), out int typeInt)) continue;
                result.Add(new TicketEntry(
                    (TicketType)typeInt,
                    cols[1].Trim(),
                    cols[2].Trim()
                ));
            }
            return result;
        }

        /// <summary>Force reload (for tests or dev hot-reload).</summary>
        public static void Reload() => _entries = null;
    }
}
