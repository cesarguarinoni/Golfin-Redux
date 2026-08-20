#nullable enable
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace Golfin.Inventory
{
    /// <summary>
    /// CSV-driven club database — mirrors CharacterDatabaseCSV pattern.
    /// Loads Clubs.csv from a TextAsset assigned in Inspector and resolves
    /// portrait sprites from Resources/Clubs/Portraits/ and Resources/Clubs/Full/.
    ///
    /// Row parsing lives in <see cref="ClubCsvParser"/> (pure, EditMode-testable); this class
    /// is the runtime adapter that maps rows onto <see cref="ClubDataRuntime"/> and resolves
    /// sprites.
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
        private const string ControlPath  = "Clubs/Controls";

        /// <summary>Fallback sprite name looked up inside each folder, then in <see cref="FullPath"/>.</summary>
        private const string PlaceholderName = "Placeholder";

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

            var rows = ClubCsvParser.Parse(clubsCSV.text);
            if (rows.Count == 0)
            {
                Debug.LogError("[ClubDatabaseCSV] Clubs.csv produced no rows — is the file empty or all comments?");
                return;
            }

            // Sprite resolution is memoized across the whole load. The roster shares art across
            // brand x type combos, so 799 rows reference only a few hundred distinct sprite names;
            // without the cache this is 3 x 799 Resources.Load calls (and, while the art batches
            // are still filling in, ~1800 duplicate "not found" warnings) on every boot.
            var spriteCache  = new Dictionary<string, Sprite?>();
            var missingNames = new HashSet<string>();

            foreach (var row in rows)
            {
                var club = ToRuntime(row, spriteCache, missingNames);
                clubMap[club.clubId] = club;
                allClubs.Add(club);
            }

            if (missingNames.Count > 0)
            {
                // One summary line, not one per row. Missing art is EXPECTED while the
                // club_art_batches specs fill in brand x type combos; every card falls back to the
                // Placeholder sprite, so this is a warning and never an error.
                Debug.LogWarning(
                    $"[ClubDatabaseCSV] {missingNames.Count} club sprite(s) not found — falling back to " +
                    $"'{PlaceholderName}'. Expected while art batches land. Missing: " +
                    string.Join(", ", missingNames.OrderBy(n => n).Take(12)) +
                    (missingNames.Count > 12 ? $", +{missingNames.Count - 12} more" : ""));
            }

            Debug.Log($"[ClubDatabaseCSV] Loaded {allClubs.Count} clubs " +
                      $"({spriteCache.Count} distinct sprite lookups, {missingNames.Count} missing).");
        }

        private static ClubDataRuntime ToRuntime(ClubCsvRow row,
                                                 Dictionary<string, Sprite?> cache,
                                                 HashSet<string> missing) => new ClubDataRuntime
        {
            clubId             = row.id,
            name               = row.name,
            type               = row.type,
            rarity             = row.rarity,
            brand              = row.brand,
            basePower          = row.basePower,
            baseAccuracy       = row.baseAccuracy,
            baseLieResistance  = row.baseLieResistance,
            baseLoft           = row.baseLoft,
            maxDurability      = row.maxDurability,
            baseDistance       = row.baseDistance,
            ballSpeedMps       = row.ballSpeedMps,
            launchAngleDeg     = row.launchAngleDeg,
            spinRateRpm        = row.spinRateRpm,
            portraitSpriteName = row.portraitSprite,
            portraitFullName   = row.portraitFull,
            controlSpriteName  = row.controlSprite,
            maxLevel           = row.maxLevel,
            info               = row.info,
            infoJa             = row.infoJa,

            portraitSprite     = LoadSprite(PortraitPath, row.portraitSprite, cache, missing),
            portraitFull       = LoadSprite(FullPath,     row.portraitFull,   cache, missing),
            controlSprite      = LoadSprite(ControlPath,  row.controlSprite,  cache, missing),
        };

        // ── Sprite loading ────────────────────────────────────────────────────

        /// <summary>
        /// Resolves one sprite by name, memoized per (folder, name). A name the art batches have
        /// not produced yet warns ONCE (collected into <paramref name="missing"/> and summarised by
        /// the caller) and falls back to the Placeholder sprite, so a card is never blank and the
        /// boot is never an error.
        /// </summary>
        private static Sprite? LoadSprite(string folder, string name,
                                          Dictionary<string, Sprite?> cache,
                                          HashSet<string> missing)
        {
            if (string.IsNullOrEmpty(name)) return Placeholder(folder, cache);

            string key = $"{folder}/{name}";
            if (cache.TryGetValue(key, out var cached)) return cached;

            var sprite = Resources.Load<Sprite>(key);
            if (sprite == null)
            {
                missing.Add(key);
                sprite = Placeholder(folder, cache);
            }

            cache[key] = sprite;
            return sprite;
        }

        /// <summary>Placeholder for a folder, falling back to the one shipped in Clubs/Full/.</summary>
        private static Sprite? Placeholder(string folder, Dictionary<string, Sprite?> cache)
        {
            string key = $"{folder}/{PlaceholderName}";
            if (cache.TryGetValue(key, out var cached)) return cached;

            var sprite = Resources.Load<Sprite>(key);
            if (sprite == null && folder != FullPath)
                sprite = Resources.Load<Sprite>($"{FullPath}/{PlaceholderName}");

            cache[key] = sprite;
            return sprite;
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
