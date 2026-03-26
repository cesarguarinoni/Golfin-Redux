#nullable enable
using UnityEngine;
using System.Collections.Generic;
using Golfin.Roster;   // CharacterRarity

/// <summary>
/// Runtime data for a single bag entry loaded from Bags.csv.
/// </summary>
public class BagDataRuntime
{
    public string bagId          = "";
    public string name           = "";
    public CharacterRarity rarity = CharacterRarity.Common;
    public string thumbnailName  = "";        // filename in Resources/Bags/Thumbnail/
    public Sprite? thumbnailSprite = null;    // loaded from Resources
    public bool startsUnlocked   = false;
}

/// <summary>
/// CSV-driven bag database — mirrors ClubDatabaseCSV pattern.
/// Loads Bags.csv from a TextAsset assigned in Inspector.
///
/// No namespace (matches ClubManager, BagManager pattern).
/// Execution order: -90 (before BagManager).
/// Attach to: Managers GameObject.
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

        string[] lines = bagsCSV.text.Split('\n');
        if (lines.Length < 2) { Debug.LogError("[BagDatabaseCSV] Bags.csv is empty."); return; }

        var headerIndex = BuildHeaderIndex(ParseCSVLine(lines[0]));

        for (int i = 1; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;

            var bag = ParseRow(ParseCSVLine(line), headerIndex);
            if (bag == null) continue;

            bagMap[bag.bagId] = bag;
            allBags.Add(bag);
        }

        Debug.Log($"[BagDatabaseCSV] Loaded {allBags.Count} bags.");
    }

    private Dictionary<string, int> BuildHeaderIndex(List<string> headers)
    {
        var idx = new Dictionary<string, int>();
        for (int i = 0; i < headers.Count; i++)
            idx[headers[i].Trim()] = i;
        return idx;
    }

    private BagDataRuntime? ParseRow(List<string> fields, Dictionary<string, int> idx)
    {
        try
        {
            string Get(string col, string def = "")
                => idx.TryGetValue(col, out int i) && i < fields.Count ? fields[i].Trim() : def;

            string id        = Get("id");
            string name      = Get("name");
            string rarityStr = Get("rarity", "Common");
            string thumbnail = Get("thumbnail");
            string unlocked  = Get("unlocked", "false");

            if (string.IsNullOrEmpty(id)) return null;

            System.Enum.TryParse<CharacterRarity>(rarityStr, out var rarity);

            Sprite? sprite = null;
            if (!string.IsNullOrEmpty(thumbnail))
                sprite = Resources.Load<Sprite>($"{ThumbnailPath}/{thumbnail}");

            return new BagDataRuntime
            {
                bagId           = id,
                name            = name,
                rarity          = rarity,
                thumbnailName   = thumbnail,
                thumbnailSprite = sprite,
                startsUnlocked  = unlocked.Equals("true", System.StringComparison.OrdinalIgnoreCase),
            };
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[BagDatabaseCSV] Row parse error: {e.Message}");
            return null;
        }
    }

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

    /// <summary>Returns all bags in CSV order.</summary>
    public List<BagDataRuntime> GetAllBags() => allBags;

    /// <summary>Returns total number of bags defined in CSV.</summary>
    public int GetBagCount() => allBags.Count;
}
