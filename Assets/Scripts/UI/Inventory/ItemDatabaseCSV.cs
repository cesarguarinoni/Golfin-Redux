#nullable enable
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace Golfin.Inventory
{
    /// <summary>
    /// CSV-driven item database — mirrors BallDatabaseCSV pattern.
    /// Loads Items.csv from a TextAsset assigned in Inspector and resolves
    /// sprites from Resources/Items/Thumbnails/ and Resources/Items/Full/.
    ///
    /// Execution order: runs before ItemManager so data is ready for it.
    /// </summary>
    public class ItemDatabaseCSV : MonoBehaviour
    {
        public static ItemDatabaseCSV? Instance { get; private set; }

        [Header("CSV File")]
        [SerializeField] private TextAsset itemsCSV = null!;

        private const string ThumbnailPath = "Items/Thumbnails";
        private const string FullPath      = "Items/Full";

        private readonly Dictionary<string, ItemDataRuntime> itemMap  = new();
        private readonly List<ItemDataRuntime>                allItems = new();

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadCSV();
        }

        private void LoadCSV()
        {
            if (itemsCSV == null)
            {
                Debug.LogError("[ItemDatabaseCSV] itemsCSV not assigned — drag Items.csv into Inspector.");
                return;
            }

            itemMap.Clear();
            allItems.Clear();

            string[] lines = itemsCSV.text.Split('\n');
            if (lines.Length < 2) { Debug.LogError("[ItemDatabaseCSV] Items.csv is empty."); return; }

            var headerIndex = BuildHeaderIndex(ParseCSVLine(lines[0]));

            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (string.IsNullOrEmpty(line)) continue;

                var item = ParseRow(ParseCSVLine(line), headerIndex);
                if (item == null) continue;

                itemMap[item.itemId] = item;
                allItems.Add(item);
            }

            Debug.Log($"[ItemDatabaseCSV] Loaded {allItems.Count} items.");
        }

        private Dictionary<string, int> BuildHeaderIndex(List<string> headers)
        {
            var idx = new Dictionary<string, int>();
            for (int i = 0; i < headers.Count; i++)
                idx[headers[i].Trim()] = i;
            return idx;
        }

        private ItemDataRuntime? ParseRow(List<string> fields, Dictionary<string, int> idx)
        {
            try
            {
                string Get(string col, string def = "")
                    => idx.TryGetValue(col, out int i) && i < fields.Count ? fields[i].Trim() : def;
                int GetInt(string col, int def = 0)
                    => int.TryParse(Get(col), out int v) ? v : def;

                var item = new ItemDataRuntime
                {
                    itemId              = Get("id"),
                    name                = Get("name"),
                    category            = Get("category"),
                    rarity              = Get("rarity"),
                    restorePercent      = GetInt("restorePercent"),
                    thumbnailSpriteName = Get("thumbnailSprite"),
                    fullSpriteName      = Get("fullSprite"),
                    proTip              = Get("proTip"),
                    info                = Get("info"),
                };

                if (string.IsNullOrEmpty(item.itemId)) return null;

                item.thumbnailSprite = LoadSprite(ThumbnailPath, item.thumbnailSpriteName);
                item.fullSprite      = LoadSprite(FullPath,      item.fullSpriteName);

                return item;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[ItemDatabaseCSV] Row parse error: {e.Message}");
                return null;
            }
        }

        private static Sprite? LoadSprite(string folder, string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            var sprite = Resources.Load<Sprite>($"{folder}/{name}");
            if (sprite == null)
                Debug.LogWarning($"[ItemDatabaseCSV] Sprite not found: Resources/{folder}/{name}");
            return sprite;
        }

        private static List<string> ParseCSVLine(string line)
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

        // ── Public API ────────────────────────────────────────────────────────

        public ItemDataRuntime? GetItem(string itemId)
        {
            if (itemMap.TryGetValue(itemId, out var data)) return data;
            Debug.LogWarning($"[ItemDatabaseCSV] Item '{itemId}' not found.");
            return null;
        }

        public List<ItemDataRuntime> GetAllItems() => allItems.ToList();

        public List<ItemDataRuntime> GetItemsByCategory(string category)
            => allItems.Where(i => i.category == category).ToList();
    }
}
