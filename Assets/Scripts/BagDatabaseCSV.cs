#nullable enable
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Golfin.Content;  // ContentCatalog / ContentFields / ContentRow
using Golfin.Roster;   // CharacterRarity

/// <summary>
/// Runtime data for a single bag entry loaded from Bags.csv.
/// </summary>
public class BagDataRuntime
{
    public string bagId          = "";
    public string name           = "";
    public CharacterRarity rarity = CharacterRarity.Common;
    public string thumbnailName   = "";        // filename in Resources/Bags/Thumbnail/
    public Sprite? thumbnailSprite = null;    // loaded from Resources
    public string fullImageName   = "";       // filename in Resources/Bags/Full/
    public Sprite? fullImageSprite = null;    // loaded from Resources
    public string description     = "";
    public bool startsUnlocked    = false;

    /// <summary>
    /// I6 — deactivated, never deleted. False means: gone from any "available" list, still fully
    /// renderable for a player who already owns one.
    /// </summary>
    public bool isActive          = true;
}

/// <summary>
/// CSV-driven bag database — mirrors ClubDatabaseCSV pattern.
/// Loads Bags.csv from a TextAsset assigned in Inspector and merges the admin-published
/// <c>bags</c> overlay on top of it (content_overlay_catalogs §1).
///
/// No namespace (matches ClubManager, BagManager pattern).
/// Execution order: -90 (before BagManager, behind ContentService's -900).
/// Attach to: Managers GameObject.
///
/// <para>
/// ⚠️ <see cref="GetBagBySlot"/> is INDEX-BASED (slot 1 = first CSV row), so overlay rows are only
/// ever APPENDED after the bundled ones and a row is never dropped — reordering or removing one
/// would silently repoint every player's saved bag slot at a different bag.
/// </para>
/// </summary>
public class BagDatabaseCSV : MonoBehaviour
{
    public static BagDatabaseCSV? Instance { get; private set; }

    [Header("CSV File")]
    [SerializeField] private TextAsset bagsCSV = null!;

    private const string ThumbnailPath = "Bags/Thumbnail";

    private readonly Dictionary<string, BagDataRuntime> bagMap  = new();
    private readonly List<BagDataRuntime>                allBags = new();

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        LoadCSV();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ── Loading ───────────────────────────────────────────────────────────────

    private void LoadCSV()
    {
        if (bagsCSV == null)
        {
            Debug.LogError("[BagDatabaseCSV] bagsCSV not assigned — drag Bags.csv into Inspector.");
            return;
        }

        bagMap.Clear();
        allBags.Clear();

        ContentCatalog? overlay = ContentCatalogStore.RequireReady(nameof(BagDatabaseCSV))
            ? ContentCatalogStore.Catalog(ContentCatalogs.Bags)
            : null;

        string[] lines = bagsCSV.text.Split('\n');
        if (lines.Length < 2) { Debug.LogError("[BagDatabaseCSV] Bags.csv is empty."); return; }

        var headerIndex = BuildHeaderIndex(ParseCSVLine(lines[0]));
        var seen = new HashSet<string>(System.StringComparer.Ordinal);
        int overlaid = 0, deactivated = 0;

        for (int i = 1; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;

            var fields = ParseCSVLine(line);

            var bundled = ParseRow(ContentFields.Csv(fields, headerIndex));
            if (bundled == null) continue;

            seen.Add(bundled.bagId);

            ContentRow? patch = null;
            overlay?.ById.TryGetValue(bundled.bagId, out patch);

            var bag = bundled;
            if (patch != null)
            {
                var merged = ParseRow(ContentFields.Csv(fields, headerIndex, patch));
                if (merged != null)
                {
                    // SPEC §5 — only names the overlay CHANGED are guarded.
                    string? unresolved = ContentSpriteGuard.FirstUnresolvedChange(new[]
                    {
                        new SpriteRef(ThumbnailPath, bundled.thumbnailName, merged.thumbnailName),
                        new SpriteRef("Bags/Full",   bundled.fullImageName, merged.fullImageName),
                    });

                    if (unresolved != null)
                        ContentSpriteGuard.LogVeto(ContentCatalogs.Bags, bundled.bagId, unresolved, false);
                    else { bag = merged; overlaid++; }
                }
            }

            if (!bag.isActive) deactivated++;
            bagMap[bag.bagId] = bag;
            allBags.Add(bag);
        }

        // APPEND ONLY, and always after every bundled row — GetBagBySlot is index-based.
        if (overlay != null)
        {
            foreach (var row in overlay.Rows)
            {
                if (seen.Contains(row.Id)) continue;

                var appended = ParseRow(ContentFields.OverlayOnly(row));
                if (appended == null) continue;

                string? unresolved = ContentSpriteGuard.FirstUnresolved(new[]
                {
                    SpritePath(ThumbnailPath, appended.thumbnailName),
                    SpritePath("Bags/Full",   appended.fullImageName),
                });

                if (unresolved != null)
                {
                    ContentSpriteGuard.LogVeto(ContentCatalogs.Bags, appended.bagId, unresolved, true);
                    continue;
                }

                if (!appended.isActive) deactivated++;
                bagMap[appended.bagId] = appended;
                allBags.Add(appended);
                overlaid++;
            }
        }

        Debug.Log($"[BagDatabaseCSV] Loaded {allBags.Count} bags" +
                  (overlay == null
                      ? " — BUNDLED only, no bags overlay this launch."
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
    /// One row from whatever <see cref="ContentFields"/> stands in front of it — bundled,
    /// bundled+overlay, or overlay alone. Column names declared once, here (I4).
    /// </summary>
    private BagDataRuntime? ParseRow(ContentFields f)
    {
        try
        {
            string id          = f.Get("id");
            string name        = f.Get("name");
            string rarityStr   = f.Get("rarity", "Common");
            string thumbnail   = f.Get("thumbnail");
            string fullImage   = f.Get("fullImage");
            string description = f.Get("description");
            string unlocked    = f.Get("unlocked", "false");

            if (string.IsNullOrEmpty(id)) return null;

            System.Enum.TryParse<CharacterRarity>(rarityStr, out var rarity);

            Sprite? sprite = null;
            if (!string.IsNullOrEmpty(thumbnail))
                sprite = Resources.Load<Sprite>($"{ThumbnailPath}/{thumbnail}");

            Sprite? fullSprite = null;
            if (!string.IsNullOrEmpty(fullImage))
                fullSprite = Resources.Load<Sprite>($"Bags/Full/{fullImage}");

            return new BagDataRuntime
            {
                bagId            = id,
                name             = name,
                rarity           = rarity,
                thumbnailName    = thumbnail,
                thumbnailSprite  = sprite,
                fullImageName    = fullImage,
                fullImageSprite  = fullSprite,
                description      = description,
                startsUnlocked   = unlocked.Equals("true", System.StringComparison.OrdinalIgnoreCase),
                isActive         = f.IsActive,
            };
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[BagDatabaseCSV] Row parse error: {e.Message}");
            return null;
        }
    }

    private static string SpritePath(string folder, string name)
        => string.IsNullOrEmpty(name) ? string.Empty : folder + "/" + name;

    private static List<string> ParseCSVLine(string line)
    {
        var result = new List<string>();
        bool inQuotes = false;
        var current = new System.Text.StringBuilder();
        foreach (char c in line)
        {
            if (c == '"') { inQuotes = !inQuotes; }
            else if (c == ',' && !inQuotes) { result.Add(current.ToString()); current.Clear(); }
            else { current.Append(c); }
        }
        result.Add(current.ToString());
        return result;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Returns bag data by ID, or null if not found.</summary>
    public BagDataRuntime? GetBag(string bagId)
        => bagMap.TryGetValue(bagId, out var bag) ? bag : null;

    /// <summary>Returns bag data by slot (1-based). Slot 1 = first CSV row.</summary>
    public BagDataRuntime? GetBagBySlot(int slot)
        => (slot >= 1 && slot <= allBags.Count) ? allBags[slot - 1] : null;

    /// <summary>Returns all bags in CSV order (overlay-appended rows last). Deactivated included (I6).</summary>
    public List<BagDataRuntime> GetAllBags() => allBags;

    /// <summary>Only ACTIVE rows — the "can be acquired" view (I6). Never use for slot lookup.</summary>
    public List<BagDataRuntime> GetAvailableBags() => allBags.Where(b => b.isActive).ToList();

    /// <summary>Returns total number of bags defined in CSV.</summary>
    public int GetBagCount() => allBags.Count;
}
