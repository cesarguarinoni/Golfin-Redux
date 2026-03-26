#nullable enable
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace Golfin.Inventory
{
    /// <summary>
    /// CSV-driven ball database — mirrors ClubDatabaseCSV pattern.
    /// Loads Balls.csv from a TextAsset assigned in Inspector and resolves
    /// sprites from Resources/Balls/Thumbnails/ and Resources/Balls/Full/.
    ///
    /// Execution order: runs before BallManager so data is ready for it.
    /// </summary>
    public class BallDatabaseCSV : MonoBehaviour
    {
        public static BallDatabaseCSV? Instance { get; private set; }

        [Header("CSV File")]
        [SerializeField] private TextAsset ballsCSV = null!;

        private const string ThumbnailPath = "Balls/Thumbnails";
        private const string FullPath      = "Balls/Full";

        private readonly Dictionary<string, BallDataRuntime> ballMap  = new();
        private readonly List<BallDataRuntime>                allBalls = new();

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadCSV();
        }

        private void LoadCSV()
        {
            if (ballsCSV == null)
            {
                Debug.LogError("[BallDatabaseCSV] ballsCSV not assigned — drag Balls.csv into Inspector.");
                return;
            }

            ballMap.Clear();
            allBalls.Clear();

            string[] lines = ballsCSV.text.Split('\n');
            if (lines.Length < 2) { Debug.LogError("[BallDatabaseCSV] Balls.csv is empty."); return; }

            var headerIndex = BuildHeaderIndex(ParseCSVLine(lines[0]));

            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (string.IsNullOrEmpty(line)) continue;

                var ball = ParseRow(ParseCSVLine(line), headerIndex);
                if (ball == null) continue;

                ballMap[ball.ballId] = ball;
                allBalls.Add(ball);
            }

            Debug.Log($"[BallDatabaseCSV] Loaded {allBalls.Count} balls.");
        }

        private Dictionary<string, int> BuildHeaderIndex(List<string> headers)
        {
            var idx = new Dictionary<string, int>();
            for (int i = 0; i < headers.Count; i++)
                idx[headers[i].Trim()] = i;
            return idx;
        }

        private BallDataRuntime? ParseRow(List<string> fields, Dictionary<string, int> idx)
        {
            try
            {
                string Get(string col, string def = "")
                    => idx.TryGetValue(col, out int i) && i < fields.Count ? fields[i].Trim() : def;
                int GetInt(string col, int def = 0)
                    => int.TryParse(Get(col), out int v) ? v : def;

                var ball = new BallDataRuntime
                {
                    ballId              = Get("id"),
                    name                = Get("name"),
                    brand               = Get("brand"),
                    power               = GetInt("power"),
                    rebound             = GetInt("rebound"),
                    windResistance      = GetInt("windResistance"),
                    roll                = GetInt("roll"),
                    spin                = GetInt("spin"),
                    thumbnailSpriteName = Get("thumbnailSprite"),
                    fullSpriteName      = Get("fullSprite"),
                    info                = Get("info"),
                };

                if (string.IsNullOrEmpty(ball.ballId)) return null;

                ball.thumbnailSprite = LoadSprite(ThumbnailPath, ball.thumbnailSpriteName);
                ball.fullSprite      = LoadSprite(FullPath,      ball.fullSpriteName);

                return ball;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[BallDatabaseCSV] Row parse error: {e.Message}");
                return null;
            }
        }

        private static Sprite? LoadSprite(string folder, string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            var sprite = Resources.Load<Sprite>($"{folder}/{name}");
            if (sprite == null)
                Debug.LogWarning($"[BallDatabaseCSV] Sprite not found: Resources/{folder}/{name}");
            return sprite;
        }

        // Reuse the same CSV parser as ClubDatabaseCSV
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

        public BallDataRuntime? GetBall(string ballId)
        {
            if (ballMap.TryGetValue(ballId, out var data)) return data;
            Debug.LogWarning($"[BallDatabaseCSV] Ball '{ballId}' not found.");
            return null;
        }

        public List<BallDataRuntime> GetAllBalls() => allBalls.ToList();
    }
}
