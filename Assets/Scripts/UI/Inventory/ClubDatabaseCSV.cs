#nullable enable
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Golfin.Roster;   // CharacterRarity

namespace Golfin.Inventory
{
    /// <summary>
    /// CSV-driven club database — mirrors CharacterDatabaseCSV pattern.
    /// Loads Clubs.csv from a TextAsset assigned in Inspector and resolves
    /// portrait sprites from Resources/Clubs/Portraits/ and Resources/Clubs/Full/.
    ///
    /// Execution order: runs before ClubManager so data is ready for it.
    /// </summary>
    public class ClubDatabaseCSV : MonoBehaviour
    {
        public static ClubDatabaseCSV? Instance { get; private set; }

        [Header("CSV File")]
        [SerializeField] private TextAsset clubsCSV = null!;

        private const string PortraitPath = "Clubs/Portraits";
        private const string FullPath     = "Clubs/Full";

        private readonly Dictionary<string, ClubDataRuntime> clubMap  = new();
        private readonly List<ClubDataRuntime>                allClubs = new();

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadCSV();
        }

        // ── Loading ───────────────────────────────────────────────────────────

        private void LoadCSV()
        {
            if (clubsCSV == null)
            {
                Debug.LogError("[ClubDatabaseCSV] clubsCSV not assigned — drag Clubs.csv into Inspector.");
                return;
            }

            clubMap.Clear();
            allClubs.Clear();

            string[] lines = clubsCSV.text.Split('\n');
            if (lines.Length < 2) { Debug.LogError("[ClubDatabaseCSV] Clubs.csv is empty."); return; }

            var headerIndex = BuildHeaderIndex(ParseCSVLine(lines[0]));

            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (string.IsNullOrEmpty(line)) continue;

                var club = ParseRow(ParseCSVLine(line), headerIndex);
                if (club == null) continue;

                clubMap[club.clubId] = club;
                allClubs.Add(club);
            }

            Debug.Log($"[ClubDatabaseCSV] Loaded {allClubs.Count} clubs.");
        }

        private Dictionary<string, int> BuildHeaderIndex(List<string> headers)
        {
            var idx = new Dictionary<string, int>();
            for (int i = 0; i < headers.Count; i++)
                idx[headers[i].Trim()] = i;
            return idx;
        }

        private ClubDataRuntime? ParseRow(List<string> fields, Dictionary<string, int> idx)
        {
            try
            {
                string  Get(string col, string def = "")
                    => idx.TryGetValue(col, out int i) && i < fields.Count ? fields[i].Trim() : def;
                int     GetInt(string col, int def = 0)
                    => int.TryParse(Get(col), out int v) ? v : def;

                var club = new ClubDataRuntime
                {
                    clubId             = Get("id"),
                    name               = Get("name"),
                    type               = ParseType(Get("type")),
                    rarity             = ParseRarity(Get("rarity", "Common")),
                    brand              = Get("brand"),
                    basePower          = GetInt("basePower"),
                    baseAccuracy       = GetInt("baseAccuracy"),
                    baseLieResistance  = GetInt("baseLieResistance"),
                    baseLoft           = GetInt("baseLoft"),
                    maxDurability      = GetInt("maxDurability", 100),
                    baseDistance       = GetInt("baseDistance"),
                    portraitSpriteName = Get("portraitSprite"),
                    portraitFullName   = Get("portraitFull"),
                    maxLevel           = GetInt("maxLevel", 119),
                    info               = Get("info"),
                };

                if (string.IsNullOrEmpty(club.clubId)) return null;

                club.portraitSprite = LoadSprite(PortraitPath, club.portraitSpriteName);
                club.portraitFull   = LoadSprite(FullPath,     club.portraitFullName);

                return club;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[ClubDatabaseCSV] Row parse error: {e.Message}");
                return null;
            }
        }

        // ── Sprite loading ────────────────────────────────────────────────────

        private static Sprite? LoadSprite(string folder, string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            var sprite = Resources.Load<Sprite>($"{folder}/{name}");
            if (sprite == null)
                Debug.LogWarning($"[ClubDatabaseCSV] Sprite not found: Resources/{folder}/{name}");
            return sprite;
        }

        // ── Parsers ───────────────────────────────────────────────────────────

        private static ClubType ParseType(string s) => s.ToLower().Replace(" ", "") switch
        {
            "driver"  => ClubType.Driver,
            "wood"    => ClubType.Wood,
            "iron"    => ClubType.Iron,
            "a.wedge" => ClubType.A_Wedge,
            "p.wedge" => ClubType.P_Wedge,
            "s.wedge" => ClubType.S_Wedge,
            "putter"  => ClubType.Putter,
            _         => ClubType.Driver
        };

        private static CharacterRarity ParseRarity(string s) => s.ToLower() switch
        {
            "common"    => CharacterRarity.Common,
            "uncommon"  => CharacterRarity.Uncommon,
            "rare"      => CharacterRarity.Rare,
            "mythic"    => CharacterRarity.Mythic,
            "legendary" => CharacterRarity.Legendary,
            "supreme"   => CharacterRarity.Supreme,
            _           => CharacterRarity.Common
        };

        /// <summary>Handles quoted fields containing commas.</summary>
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

        public ClubDataRuntime? GetClub(string clubId)
        {
            if (clubMap.TryGetValue(clubId, out var data)) return data;
            Debug.LogWarning($"[ClubDatabaseCSV] Club '{clubId}' not found.");
            return null;
        }

        public List<ClubDataRuntime> GetAllClubs() => allClubs.ToList();

        public List<ClubDataRuntime> GetClubsOfType(ClubType type)
            => allClubs.Where(c => c.type == type).ToList();
    }
}
