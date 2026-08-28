#nullable enable
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Golfin.CatalogArt;
using Golfin.Tournaments;
using Golfin.Content;

namespace Golfin.Inventory
{
    /// <summary>
    /// CSV-driven ball database — mirrors ClubDatabaseCSV pattern.
    /// Loads Balls.csv from a TextAsset assigned in Inspector, merges the admin-published
    /// <c>balls</c> overlay on top of it (content_overlay_catalogs §1), and resolves
    /// sprites from Resources/Balls/Thumbnails/ and Resources/Balls/Full/.
    ///
    /// Execution order -70 (from BallDatabaseCSV.cs.meta), i.e. behind ContentService's -900.
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

            ContentCatalog? overlay = ContentCatalogStore.RequireReady(nameof(BallDatabaseCSV))
                ? ContentCatalogStore.Catalog(ContentCatalogs.Balls)
                : null;

            string[] lines = ballsCSV.text.Split('\n');
            if (lines.Length < 2) { Debug.LogError("[BallDatabaseCSV] Balls.csv is empty."); return; }

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

                var bundled = ParseRow(ContentFields.Csv(fields, headerIndex));
                if (bundled == null) continue;

                seen.Add(bundled.ballId);

                ContentRow? patch = null;
                overlay?.ById.TryGetValue(bundled.ballId, out patch);

                var ball = bundled;
                if (patch != null)
                {
                    var merged = ParseRow(ContentFields.Csv(fields, headerIndex, patch),
                        bundled.thumbnailUrl, bundled.fullUrl);
                    if (merged != null)
                    {
                        // SPEC §5 — only names the overlay CHANGED are guarded.
                        string? unresolved = ContentSpriteGuard.FirstUnresolvedChange(new[]
                        {
                            new SpriteRef(ThumbnailPath, bundled.thumbnailSpriteName, merged.thumbnailSpriteName,
                                          CatalogArtPolicy.IsArtAllowed(merged.thumbnailUrl)),
                            new SpriteRef(FullPath,      bundled.fullSpriteName,      merged.fullSpriteName,
                                          CatalogArtPolicy.IsArtAllowed(merged.fullUrl)),
                        });

                        if (unresolved != null)
                            ContentSpriteGuard.LogVeto(ContentCatalogs.Balls, bundled.ballId, unresolved, false);
                        else { ball = merged; overlaid++; }
                    }
                }

                if (!ball.isActive) deactivated++;
                if (!ball.renderable) withheld.Add(ball.ballId);
                ballMap[ball.ballId] = ball;
                allBalls.Add(ball);
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
                        new SpriteRef(ThumbnailPath, string.Empty, appended.thumbnailSpriteName,
                                      CatalogArtPolicy.IsArtAllowed(appended.thumbnailUrl)),
                        new SpriteRef(FullPath,      string.Empty, appended.fullSpriteName,
                                      CatalogArtPolicy.IsArtAllowed(appended.fullUrl)),
                    });

                    if (unresolved != null)
                    {
                        ContentSpriteGuard.LogVeto(ContentCatalogs.Balls, appended.ballId, unresolved, true);
                        continue;
                    }

                    if (!appended.isActive) deactivated++;
                    if (!appended.renderable) withheld.Add(appended.ballId);
                    ballMap[appended.ballId] = appended;
                    allBalls.Add(appended);
                    overlaid++;
                }
            }

            if (withheld.Count > 0)
            {
                // Warning, never an error: data published ahead of its art is a legitimate state
                // (content_two_way §5). GetAllBalls still carries the row so an owner keeps it.
                Debug.LogWarning(
                    $"[BallDatabaseCSV] {withheld.Count} ball(s) withheld (unrenderable — sprite " +
                    "missing in this build; ships when the art does): " +
                    string.Join(", ", withheld.OrderBy(n => n).Take(12)) +
                    (withheld.Count > 12 ? $", +{withheld.Count - 12} more" : ""));
            }

            Debug.Log($"[BallDatabaseCSV] Loaded {allBalls.Count} balls" +
                      (overlay == null
                          ? " — BUNDLED only, no balls overlay this launch."
                          : $" — overlay v{overlay.Version}: {overlaid} row(s) patched/appended, " +
                            $"{deactivated} deactivated (still owned + playable, I6)."));

            // SPEC §4 — boot prefetch (see CharacterDatabaseCSV for rationale).
            var artUrls = allBalls
                .SelectMany(b => new[] { b.thumbnailUrl, b.fullUrl })
                .Where(u => !string.IsNullOrEmpty(u));
            TournamentArtService.CatalogArt.Prefetch(artUrls);
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
        private BallDataRuntime? ParseRow(ContentFields f,
            string? bundledThumbnailUrl = null, string? bundledFullUrl = null)
        {
            try
            {
                string thumbnailUrl = f.Get("thumbnailUrl");
                string fullUrl      = f.Get("fullUrl");

                // ⚠️ NO BUNDLED COUNTERPART ⇒ NOTHING CHANGED ⇒ STEP 1 MUST NOT FIRE.
                // (content_art_bundling, 2026-08-28.) The overload defaults used to be "", so a
                // BUNDLED row carrying a URL compared its own URL against "" — always "different"
                // — and step 1 served the cached download in front of the build's own sprite.
                // The bundled asset was then dead weight in every build that had it, silently,
                // which is precisely what this task exists to produce. Comparing the URL against
                // ITSELF is the correct expression of "the overlay has not changed anything":
                // step 1 returns null and step 2 wins, exactly as SPEC §2.2 orders them.
                string bundledThumbnail = bundledThumbnailUrl ?? thumbnailUrl;
                string bundledFull      = bundledFullUrl      ?? fullUrl;

                var ball = new BallDataRuntime
                {
                    ballId              = f.Get("id"),
                    name                = f.Get("name"),
                    brand               = f.Get("brand"),
                    power               = f.GetInt("power"),
                    rebound             = f.GetInt("rebound"),
                    windResistance      = f.GetInt("windResistance"),
                    roll                = f.GetInt("roll"),
                    spin                = f.GetInt("spin"),
                    thumbnailSpriteName = f.Get("thumbnailSprite"),
                    fullSpriteName      = f.Get("fullSprite"),
                    thumbnailUrl        = thumbnailUrl,
                    fullUrl             = fullUrl,
                    info                = f.Get("info"),
                    isActive            = f.IsActive,
                };

                if (string.IsNullOrEmpty(ball.ballId)) return null;

                // Resolution ladder (SPEC §2, revised 2026-08-27):
                //   1. URL the OVERLAY CHANGED (overlay URL ≠ bundled CSV URL), if cached
                //   2. bundled sprite by name  →  the build's own art wins
                //   3. URL unchanged since build, if cached  →  row newer than any bundled art
                //   5. otherwise null ⇒ renderable=false ⇒ withheld
                ball.thumbnailSprite = CatalogArtCache.Cached(thumbnailUrl, bundledThumbnail)     // step 1
                                       ?? LoadSprite(ThumbnailPath, ball.thumbnailSpriteName)     // step 2
                                       ?? CatalogArtCache.Cached(thumbnailUrl);                   // step 3
                ball.fullSprite      = CatalogArtCache.Cached(fullUrl, bundledFull)               // step 1
                                       ?? LoadSprite(FullPath, ball.fullSpriteName)               // step 2
                                       ?? CatalogArtCache.Cached(fullUrl);                        // step 3

                // content_two_way §4 — the PRIMARY sprite (the thumbnail the bag draws) is the
                // renderability test, read off the resolution just performed.
                ball.renderable = ball.thumbnailSprite != null;

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

        private static string SpritePath(string folder, string name)
            => string.IsNullOrEmpty(name) ? string.Empty : folder + "/" + name;

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

        /// <summary>EVERY ball row, deactivated ones included — the bag view (I6).</summary>
        public List<BallDataRuntime> GetAllBalls() => allBalls.ToList();

        /// <summary>Only ACTIVE rows this build can DRAW — the shop / bag-seed /
        /// "can be acquired" view (I6 + content_two_way §4).</summary>
        public List<BallDataRuntime> GetAvailableBalls()
            => allBalls.Where(b => b.isActive && b.renderable).ToList();
    }
}
