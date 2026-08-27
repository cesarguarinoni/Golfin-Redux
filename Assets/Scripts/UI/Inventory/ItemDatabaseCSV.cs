#nullable enable
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Golfin.Content;

namespace Golfin.Inventory
{
    /// <summary>
    /// CSV-driven item database — mirrors BallDatabaseCSV pattern.
    /// Loads Items.csv from a TextAsset assigned in Inspector, merges the admin-published
    /// <c>items</c> overlay on top of it (content_overlay_catalogs §1), and resolves
    /// sprites from Resources/Items/Thumbnails/ and Resources/Items/Full/.
    ///
    /// Execution order -90 (from ItemDatabaseCSV.cs.meta, set by ItemManagerSetup), i.e. ahead of
    /// ItemManager's -80 and behind ContentService's -900.
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

            ContentCatalog? overlay = ContentCatalogStore.RequireReady(nameof(ItemDatabaseCSV))
                ? ContentCatalogStore.Catalog(ContentCatalogs.Items)
                : null;

            string[] lines = itemsCSV.text.Split('\n');
            if (lines.Length < 2) { Debug.LogError("[ItemDatabaseCSV] Items.csv is empty."); return; }

            var headerIndex = BuildHeaderIndex(ParseCSVLine(lines[0]));
            var seen = new HashSet<string>(System.StringComparer.Ordinal);
            int overlaid = 0, deactivated = 0;

            // content_two_way §4 — ids this build cannot draw, reported ONCE at the end in the
            // shape ClubDatabaseCSV already uses for its missing-art line.
            var withheld = new List<string>();

            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (string.IsNullOrEmpty(line)) continue;

                var fields = ParseCSVLine(line);

                // Bundled first: the id has to exist before the overlay can be looked up.
                var bundled = ParseRow(ContentFields.Csv(fields, headerIndex));
                if (bundled == null) continue;

                seen.Add(bundled.itemId);

                ContentRow? patch = null;
                overlay?.ById.TryGetValue(bundled.itemId, out patch);

                var item = bundled;
                if (patch != null)
                {
                    var merged = ParseRow(ContentFields.Csv(fields, headerIndex, patch));
                    if (merged != null)
                    {
                        // SPEC §5 — only names the overlay CHANGED are guarded; a bundled sprite
                        // that has never landed is the art pipeline's problem, not the overlay's.
                        string? unresolved = ContentSpriteGuard.FirstUnresolvedChange(new[]
                        {
                            new SpriteRef(ThumbnailPath, bundled.thumbnailSpriteName, merged.thumbnailSpriteName),
                            new SpriteRef(FullPath,      bundled.fullSpriteName,      merged.fullSpriteName),
                        });

                        if (unresolved != null)
                            ContentSpriteGuard.LogVeto(ContentCatalogs.Items, bundled.itemId, unresolved, false);
                        else { item = merged; overlaid++; }
                    }
                }

                if (!item.isActive) deactivated++;
                if (!item.renderable) withheld.Add(item.itemId);
                itemMap[item.itemId] = item;
                allItems.Add(item);
            }

            if (overlay != null)
            {
                foreach (var row in overlay.Rows)
                {
                    if (seen.Contains(row.Id)) continue;

                    var appended = ParseRow(ContentFields.OverlayOnly(row));
                    if (appended == null) continue;

                    string? unresolved = ContentSpriteGuard.FirstUnresolved(new[]
                    {
                        SpritePath(ThumbnailPath, appended.thumbnailSpriteName),
                        SpritePath(FullPath,      appended.fullSpriteName),
                    });

                    if (unresolved != null)
                    {
                        ContentSpriteGuard.LogVeto(ContentCatalogs.Items, appended.itemId, unresolved, true);
                        continue;
                    }

                    if (!appended.isActive) deactivated++;
                    if (!appended.renderable) withheld.Add(appended.itemId);
                    itemMap[appended.itemId] = appended;
                    allItems.Add(appended);
                    overlaid++;
                }
            }

            if (withheld.Count > 0)
            {
                // Warning, never an error: data published ahead of its art is a legitimate state
                // (content_two_way §5). GetAllItems still carries the row so an owner keeps it.
                Debug.LogWarning(
                    $"[ItemDatabaseCSV] {withheld.Count} item(s) withheld (unrenderable — sprite " +
                    "missing in this build; ships when the art does): " +
                    string.Join(", ", withheld.OrderBy(n => n).Take(12)) +
                    (withheld.Count > 12 ? $", +{withheld.Count - 12} more" : ""));
            }

            Debug.Log($"[ItemDatabaseCSV] Loaded {allItems.Count} items" +
                      (overlay == null
                          ? " — BUNDLED only, no items overlay this launch."
                          : $" — overlay v{overlay.Version}: {overlaid} row(s) patched/appended, " +
                            $"{deactivated} deactivated (still owned + renderable, I6)."));
        }

        private Dictionary<string, int> BuildHeaderIndex(List<string> headers)
        {
            var idx = new Dictionary<string, int>();
            for (int i = 0; i < headers.Count; i++)
                idx[headers[i].Trim()] = i;
            return idx;
        }

        /// <summary>
        /// One row from whatever <see cref="ContentFields"/> stands in front of it — a bundled CSV
        /// row, a bundled row patched by an overlay, or an overlay row alone. Column names and
        /// defaults are declared once, here (I4).
        /// </summary>
        private ItemDataRuntime? ParseRow(ContentFields f)
        {
            try
            {
                var item = new ItemDataRuntime
                {
                    itemId              = f.Get("id"),
                    name                = f.Get("name"),
                    category            = f.Get("category"),
                    rarity              = f.Get("rarity"),
                    restorePercent      = f.GetInt("restorePercent"),
                    thumbnailSpriteName = f.Get("thumbnailSprite"),
                    fullSpriteName      = f.Get("fullSprite"),
                    proTip              = f.Get("proTip"),
                    info                = f.Get("info"),
                    isActive            = f.IsActive,
                };

                if (string.IsNullOrEmpty(item.itemId)) return null;

                item.thumbnailSprite = LoadSprite(ThumbnailPath, item.thumbnailSpriteName);
                item.fullSprite      = LoadSprite(FullPath,      item.fullSpriteName);

                // content_two_way §4 — the PRIMARY sprite (the thumbnail the grid draws) is the
                // renderability test, read off the resolution just performed.
                item.renderable = item.thumbnailSprite != null;

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

        private static string SpritePath(string folder, string name)
            => string.IsNullOrEmpty(name) ? string.Empty : folder + "/" + name;

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

        /// <summary>EVERY item row, deactivated ones included — the inventory view (I6).</summary>
        public List<ItemDataRuntime> GetAllItems() => allItems.ToList();

        /// <summary>Only ACTIVE rows this build can DRAW — the shop / inventory-seed /
        /// "can be acquired" view (I6 + content_two_way §4).</summary>
        public List<ItemDataRuntime> GetAvailableItems()
            => allItems.Where(i => i.isActive && i.renderable).ToList();

        public List<ItemDataRuntime> GetItemsByCategory(string category)
            => allItems.Where(i => i.category == category).ToList();
    }
}
