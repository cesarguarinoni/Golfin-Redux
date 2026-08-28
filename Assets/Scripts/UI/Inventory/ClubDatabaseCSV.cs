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
    /// CSV-driven club database — mirrors CharacterDatabaseCSV pattern.
    /// Loads Clubs.csv from a TextAsset assigned in Inspector, merges the admin-published
    /// <c>clubs</c> overlay on top of it, and resolves portrait sprites from
    /// Resources/Clubs/Portraits/ and Resources/Clubs/Full/.
    ///
    /// Row parsing lives in <see cref="ClubCsvParser"/> (pure, EditMode-testable); this class
    /// is the runtime adapter that maps rows onto <see cref="ClubDataRuntime"/>, resolves
    /// sprites, and applies SPEC §5's sprite veto — the one part of the merge that needs
    /// <c>Resources</c>.
    ///
    /// <para>
    /// <b>EXECUTION ORDER (content_overlay_catalogs).</b> The old comment here said "runs before
    /// ClubManager so data is ready for it" and nothing backed it: there is no
    /// <c>[DefaultExecutionOrder]</c> on either class and this project has no
    /// <c>ProjectSettings/MonoManager.asset</c>. The guarantee is the <c>executionOrder:</c> field
    /// committed into <c>ClubDatabaseCSV.cs.meta</c> (-90) and <c>ClubManager.cs.meta</c> (-80),
    /// written ONCE by the <c>GOLFIN ▸ Setup ▸ Club Managers</c> menu item and never re-asserted —
    /// so a regenerated or merge-mangled .meta silently drops both to 0, where the order is
    /// UNDEFINED. <see cref="IsLoaded"/> exists so ClubManager can check the invariant at runtime
    /// instead of trusting a comment.
    /// </para>
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

        /// <summary>
        /// True once <c>LoadCSV()</c> has produced at least one row. <b>The runtime half of the
        /// DB-before-Manager invariant</b> — ClubManager asserts this rather than trusting the
        /// script execution order, because a zero-row database and an unrun one look identical to
        /// every consumer and both produce a player with no clubs.
        /// </summary>
        public bool IsLoaded { get; private set; }

        /// <summary>How many rows the clubs overlay patched or appended this boot. Diagnostics only.</summary>
        public int OverlaidRowCount { get; private set; }

        // ── Lifecycle ─────────────────────────────────────────────────────────

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
            IsLoaded = false;
            OverlaidRowCount = 0;

            // RequireReady logs an ERROR when ContentService exists but has not installed the store
            // yet (i.e. this database's execution order is ahead of -900), and stays quiet when
            // there is no ContentService at all — a lab or EditMode scene running bundled, which is
            // correct. Either way the parse below proceeds on the bundled CSV.
            ContentCatalog? overlay = ContentCatalogStore.RequireReady(nameof(ClubDatabaseCSV))
                ? ContentCatalogStore.Catalog(ContentCatalogs.Clubs)
                : null;

            var rows = ClubCsvParser.Parse(clubsCSV.text, overlay);
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

            int vetoed = 0, dropped = 0, deactivated = 0;

            foreach (var raw in rows)
            {
                var row = raw;

                // ── SPEC §5: the sprite veto ─────────────────────────────────
                // Only names the OVERLAY CHANGED are guarded. The bundled roster already has
                // missing art on purpose while club_art_batches fills in brand × type combos
                // (that is the summary warning below), and guarding bundled names too would reject
                // every overlay row for a club whose art has not landed yet.
                if (row.overlayApplied)
                {
                    string? unresolved = row.overlayAppended
                        ? ContentSpriteGuard.FirstUnresolved(new[]
                          {
                              // Clubs fall back to Placeholder, so a missing PRIMARY is not dropped.
                              // However the sprite veto still applies when a name was given: if the
                              // name does not resolve and no URL is supplied, the guard fires.
                              new SpriteRef(PortraitPath, string.Empty, row.portraitSprite,
                                            CatalogArtPolicy.IsArtAllowed(row.portraitUrl)),
                              new SpriteRef(FullPath,     string.Empty, row.portraitFull,
                                            CatalogArtPolicy.IsArtAllowed(row.portraitFullUrl)),
                              new SpriteRef(ControlPath,  string.Empty, row.controlSprite,
                                            CatalogArtPolicy.IsArtAllowed(row.controlUrl)),
                          })
                        : ContentSpriteGuard.FirstUnresolvedChange(new[]
                          {
                              new SpriteRef(PortraitPath, row.bundled!.portraitSprite, row.portraitSprite,
                                            CatalogArtPolicy.IsArtAllowed(row.portraitUrl)),
                              new SpriteRef(FullPath,     row.bundled!.portraitFull,   row.portraitFull,
                                            CatalogArtPolicy.IsArtAllowed(row.portraitFullUrl)),
                              new SpriteRef(ControlPath,  row.bundled!.controlSprite,  row.controlSprite,
                                            CatalogArtPolicy.IsArtAllowed(row.controlUrl)),
                          });

                    if (unresolved != null)
                    {
                        ContentSpriteGuard.LogVeto(ContentCatalogs.Clubs, row.id, unresolved,
                                                   row.overlayAppended);

                        // An APPENDED row has no bundled counterpart to fall back to, so it is
                        // dropped outright: a Placeholder card in the grid is worse than no card.
                        if (row.overlayAppended) { dropped++; continue; }

                        row = row.bundled!;     // a silently-stale club beats an obviously broken one
                        vetoed++;
                    }
                }

                if (row.overlayApplied) OverlaidRowCount++;
                if (!row.isActive) deactivated++;

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

            IsLoaded = allClubs.Count > 0;

            Debug.Log($"[ClubDatabaseCSV] Loaded {allClubs.Count} clubs " +
                      $"({spriteCache.Count} distinct sprite lookups, {missingNames.Count} missing)" +
                      (overlay == null
                          ? " — BUNDLED only, no clubs overlay this launch."
                          : $" — overlay v{overlay.Version}: {OverlaidRowCount} row(s) patched/appended, " +
                            $"{deactivated} deactivated (still owned + renderable, I6), " +
                            $"{vetoed} reverted to bundled and {dropped} dropped by the sprite veto (§5).") +
                      ".");

            // SPEC §4 — boot prefetch (see CharacterDatabaseCSV for rationale).
            var artUrls = allClubs
                .SelectMany(c => new[] { c.portraitUrl, c.portraitFullUrl, c.controlUrl })
                .Where(u => !string.IsNullOrEmpty(u));
            TournamentArtService.CatalogArt.Prefetch(artUrls);
        }

        private static string Path(string folder, string name)
            => string.IsNullOrEmpty(name) ? string.Empty : folder + "/" + name;

        private static ClubDataRuntime ToRuntime(ClubCsvRow row,
                                                 Dictionary<string, Sprite?> cache,
                                                 HashSet<string> missing)
        {
            // Resolution ladder for clubs (SPEC §2, revised 2026-08-27 + Placeholder policy):
            //   1. URL the OVERLAY CHANGED (overlay URL ≠ bundled CSV URL), if cached
            //      → art re-uploaded since this build; same comparison ContentSpriteGuard
            //        performs on sprite NAMES. row.bundled holds the pre-merge row.
            //   2. REAL bundled sprite by name (NOT Placeholder fallback)
            //      → the build's own art wins; Placeholder must not shadow step 3.
            //   3. URL unchanged since the build, if cached
            //      → row is newer than any bundled art (new admin row, no bundled counterpart)
            //   4. clubs only: Placeholder (LoadSprite always returns something; decision of record)
            //
            // ⚠️ Step 2 uses LoadRealSprite (no Placeholder fallback) so that a club with no
            // real bundled art but a cached URL uses the URL at step 3, not the stand-in at step 4.
            // ⚠️ NO BUNDLED COUNTERPART ⇒ NOTHING CHANGED ⇒ STEP 1 MUST NOT FIRE.
            // (content_art_bundling, 2026-08-28.) These used to fall back to "", so a
            // BUNDLED club carrying a URL compared its own URL against "" — always
            // "different" — and step 1 served the cached download in front of the build's
            // own sprite. The bundled asset was then dead weight in every build that had
            // it, silently, which is precisely what content_art_bundling exists to
            // produce. When there is no overlay, `row` IS the bundled row, so each URL is
            // compared against ITSELF: step 1 returns null and step 2 wins, as SPEC §2.2
            // orders them. A genuine re-upload still differs and still takes step 1.
            string bundledPortraitUrl  = row.bundled?.portraitUrl     ?? row.portraitUrl;
            string bundledFullUrl      = row.bundled?.portraitFullUrl ?? row.portraitFullUrl;
            string bundledControlUrl   = row.bundled?.controlUrl      ?? row.controlUrl;

            Sprite? portraitSpriteResolved =
                CatalogArtCache.Cached(row.portraitUrl,  bundledPortraitUrl)  // step 1
                ?? LoadRealSprite(PortraitPath, row.portraitSprite)            // step 2 REAL
                ?? CatalogArtCache.Cached(row.portraitUrl)                    // step 3
                ?? LoadSprite(PortraitPath, row.portraitSprite, cache, missing); // step 4 Placeholder

            Sprite? portraitFullResolved =
                CatalogArtCache.Cached(row.portraitFullUrl, bundledFullUrl)   // step 1
                ?? LoadRealSprite(FullPath, row.portraitFull)                  // step 2 REAL
                ?? CatalogArtCache.Cached(row.portraitFullUrl)                // step 3
                ?? LoadSprite(FullPath, row.portraitFull, cache, missing);    // step 4 Placeholder

            Sprite? controlSpriteResolved =
                CatalogArtCache.Cached(row.controlUrl,   bundledControlUrl)   // step 1
                ?? LoadRealSprite(ControlPath, row.controlSprite)              // step 2 REAL
                ?? CatalogArtCache.Cached(row.controlUrl)                     // step 3
                ?? LoadSprite(ControlPath, row.controlSprite, cache, missing); // step 4 Placeholder

            return new ClubDataRuntime
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
                portraitUrl        = row.portraitUrl,
                portraitFullUrl    = row.portraitFullUrl,
                controlUrl         = row.controlUrl,
                startLevel         = row.startLevel,
                maxLevel           = row.maxLevel,
                isActive           = row.isActive,
                info               = row.info,
                infoJa             = row.infoJa,

                portraitSprite  = portraitSpriteResolved,
                portraitFull    = portraitFullResolved,
                controlSprite   = controlSpriteResolved,
            };
        }

        // ── Sprite loading ────────────────────────────────────────────────────

        /// <summary>
        /// Step 2 of the resolution ladder — REAL bundled art only.
        /// Returns the named sprite if found in Resources; returns null if
        /// <paramref name="name"/> is empty or the sprite is not bundled.
        ///
        /// <para>
        /// Does NOT fall back to <c>Placeholder</c> — that is step 4
        /// (<see cref="LoadSprite"/>). This distinction ensures that a club with no real
        /// bundled art (name missing or sprite file absent) does not have the stand-in
        /// shadow a live URL at step 3, which would be worse than shipping nothing
        /// (SPEC §2 ⚠️ note on step 4).
        /// </para>
        ///
        /// <para>
        /// Does NOT use the shared sprite cache to avoid reading a <c>Placeholder</c>
        /// that <see cref="LoadSprite"/> may have written for a different miss on the
        /// same key. Unity's internal resource system memoizes the load itself; this is
        /// not expensive.
        /// </para>
        /// </summary>
        private static Sprite? LoadRealSprite(string folder, string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            return Resources.Load<Sprite>($"{folder}/{name}");
        }

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

        /// <summary>
        /// EVERY club row, deactivated ones included. This is the bag / roster / detail-panel view:
        /// I6 says a deactivated club stays fully renderable for a player who owns one.
        /// </summary>
        public List<ClubDataRuntime> GetAllClubs() => allClubs.ToList();

        /// <summary>
        /// Only rows an operator has left ACTIVE. This is the "available" view — shop listings,
        /// gacha pools, anything that can hand a player a NEW club (I6).
        /// </summary>
        public List<ClubDataRuntime> GetAvailableClubs()
            => allClubs.Where(c => c.isActive).ToList();

        public List<ClubDataRuntime> GetClubsOfType(ClubType type)
            => allClubs.Where(c => c.type == type).ToList();
    }
}
