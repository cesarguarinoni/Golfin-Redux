// Assets/Scripts/UI/Gacha/GachaContentCatalogs.cs
// gacha_client_real_pull §2 — the three read-mostly gacha catalogs the banner card and the
// withhold rule need: rates (poolId → rarity → bp), pools (poolId → entries) and ticket types.
//
// SAME SHAPE AS ClubDatabaseCSV.LoadCSV (ClubDatabaseCSV.cs:92-97): parse the BUNDLED CSV,
// then merge the admin overlay on top of it field-by-field through ContentFields, patch by id,
// append ids the bundled file has never carried, and let is_active=false drop the row. Bundled
// is the floor (I1) — RequireReady false (EditMode, a lab scene, a build with no ContentService)
// keeps the bundled table, silently and correctly.
//
// These are DATA ONLY. They resolve nothing, they load no sprite and they know nothing about a
// pull: the withhold rule (GachaBannerCatalog.IsRollable) and the card do the resolving, which is
// what keeps all three testable from EditMode with no scene.
//
// ⚠️ THE SERVER READS THE SAME PUBLISHED ROWS. `golfin_gacha_pull()` prices, rolls the tier and
// picks the entry from `content_rows`, so these tables are not a client-side preview of the odds
// — they are the client's copy of the same authority, and the withhold rule exists so the client
// never shows a banner the server would refuse to roll.
#nullable enable
using System;
using System.Collections.Generic;
using Golfin.Content;
using Golfin.Roster;
using UnityEngine;

namespace GolfinRedux.UI.Gacha
{
    // ─────────────────────────────────────────────────────────────────────────
    // Rates — one row per (pool, rarity), in basis points. Must sum to 10 000.
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>One row of gacha_rates.csv: how likely one rarity tier is, in a pool.</summary>
    public sealed class GachaRateEntry
    {
        public string          Id     = string.Empty;
        public string          PoolId = string.Empty;
        public CharacterRarity Rarity = CharacterRarity.Common;

        /// <summary>Basis points out of 10 000. A tier at 0 is never rolled.</summary>
        public int RateBp;
    }

    /// <summary>
    /// Static catalog over <c>Resources/Data/gacha_rates.csv</c> + the <c>gacha_rates</c> overlay.
    /// </summary>
    public static class GachaRatesCatalog
    {
        private static Dictionary<string, List<GachaRateEntry>>? _byPool;

        /// <summary>Every rate row of one pool, or an empty list when the pool is unknown.</summary>
        public static IReadOnlyList<GachaRateEntry> ForPool(string? poolId)
        {
            EnsureLoaded();
            if (string.IsNullOrEmpty(poolId)) return Array.Empty<GachaRateEntry>();
            return _byPool!.TryGetValue(poolId!, out var rows)
                ? rows
                : (IReadOnlyList<GachaRateEntry>)Array.Empty<GachaRateEntry>();
        }

        /// <summary>Test / hot-reload hook: forces re-read on next access.</summary>
        public static void Reload() => _byPool = null;

        private static void EnsureLoaded()
        {
            if (_byPool != null) return;

            var asset = Resources.Load<TextAsset>("Data/gacha_rates");
            if (asset == null)
            {
                Debug.LogError("[GachaRatesCatalog] gacha_rates.csv not found in Resources/Data/.");
                _byPool = new Dictionary<string, List<GachaRateEntry>>(StringComparer.Ordinal);
                return;
            }

            ContentCatalog? overlay = ContentCatalogStore.RequireReady(nameof(GachaRatesCatalog))
                ? ContentCatalogStore.Catalog(ContentCatalogs.GachaRates)
                : null;

            _byPool = Index(Parse(asset.text, overlay));
        }

        private static Dictionary<string, List<GachaRateEntry>> Index(List<GachaRateEntry> rows)
        {
            var map = new Dictionary<string, List<GachaRateEntry>>(StringComparer.Ordinal);
            foreach (var r in rows)
            {
                if (string.IsNullOrEmpty(r.PoolId)) continue;
                if (!map.TryGetValue(r.PoolId, out var list))
                    map[r.PoolId] = list = new List<GachaRateEntry>();
                list.Add(r);
            }
            return map;
        }

        /// <summary>Testable seam: bundled text + optional overlay → rows. Pure.</summary>
        internal static List<GachaRateEntry> Parse(string? csvText, ContentCatalog? overlay)
            => GachaCsvMerge.Merge(csvText, overlay, "id", f => new GachaRateEntry
            {
                Id     = f.Get("id"),
                PoolId = f.Get("poolId"),
                Rarity = GachaCsvMerge.ParseRarity(f.Get("rarity")),
                RateBp = f.GetInt("rateBp"),
            });
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Pools — the prize table a banner rolls against.
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>One row of gacha_pools.csv: one prize a pool can pay, and how heavily.</summary>
    public sealed class GachaPoolEntry
    {
        public string          Id     = string.Empty;
        public string          PoolId = string.Empty;

        /// <summary><c>club</c> | <c>ball</c> | <c>character</c> | <c>item</c> | <c>ticket</c>.</summary>
        public string          Kind   = string.Empty;
        public string          RefId  = string.Empty;
        public CharacterRarity Rarity = CharacterRarity.Common;

        public int  Weight   = 1;
        public int  Quantity = 1;
        public int  DupeRp;
        public bool Featured;

        /// <summary>
        /// The build floor of this ENTRY (not of the banner). Comes from the overlay row's
        /// <c>min_build</c> — the same field the server's step 8 reads — and is 0 for a bundled
        /// row, which by definition shipped with this build.
        /// </summary>
        public int MinBuild;

        /// <summary>I6 — false means the operator deactivated this entry; it is not rollable.</summary>
        public bool IsActive = true;
    }

    /// <summary>
    /// Static catalog over <c>Resources/Data/gacha_pools.csv</c> + the <c>gacha_pools</c> overlay.
    /// </summary>
    public static class GachaPoolCatalog
    {
        private static Dictionary<string, List<GachaPoolEntry>>? _byPool;

        /// <summary>Every entry of one pool, active or not, or an empty list when unknown.</summary>
        public static IReadOnlyList<GachaPoolEntry> ForPool(string? poolId)
        {
            EnsureLoaded();
            if (string.IsNullOrEmpty(poolId)) return Array.Empty<GachaPoolEntry>();
            return _byPool!.TryGetValue(poolId!, out var rows)
                ? rows
                : (IReadOnlyList<GachaPoolEntry>)Array.Empty<GachaPoolEntry>();
        }

        /// <summary>Test / hot-reload hook: forces re-read on next access.</summary>
        public static void Reload() => _byPool = null;

        private static void EnsureLoaded()
        {
            if (_byPool != null) return;

            var asset = Resources.Load<TextAsset>("Data/gacha_pools");
            if (asset == null)
            {
                Debug.LogError("[GachaPoolCatalog] gacha_pools.csv not found in Resources/Data/.");
                _byPool = new Dictionary<string, List<GachaPoolEntry>>(StringComparer.Ordinal);
                return;
            }

            ContentCatalog? overlay = ContentCatalogStore.RequireReady(nameof(GachaPoolCatalog))
                ? ContentCatalogStore.Catalog(ContentCatalogs.GachaPools)
                : null;

            var map = new Dictionary<string, List<GachaPoolEntry>>(StringComparer.Ordinal);
            foreach (var r in Parse(asset.text, overlay))
            {
                if (string.IsNullOrEmpty(r.PoolId)) continue;
                if (!map.TryGetValue(r.PoolId, out var list))
                    map[r.PoolId] = list = new List<GachaPoolEntry>();
                list.Add(r);
            }
            _byPool = map;
        }

        /// <summary>Testable seam: bundled text + optional overlay → rows. Pure.</summary>
        internal static List<GachaPoolEntry> Parse(string? csvText, ContentCatalog? overlay)
            => GachaCsvMerge.Merge(csvText, overlay, "id", f => new GachaPoolEntry
            {
                Id       = f.Get("id"),
                PoolId   = f.Get("poolId"),
                Kind     = f.Get("kind").Trim().ToLowerInvariant(),
                RefId    = f.Get("refId"),
                Rarity   = GachaCsvMerge.ParseRarity(f.Get("rarity")),
                Weight   = f.GetInt("weight", 1),
                Quantity = f.GetInt("quantity", 1),
                DupeRp   = f.GetInt("dupeRp"),
                Featured = f.GetBool("featured"),

                // The BUNDLED `is_active` cell (gacha_ops_polish). `export_content.py` appends an
                // `is_active` column to a CSV the first time that catalog carries a deactivated
                // row, and gacha_pools.csv now has one: `psc1_ball_golfin` was deactivated in the
                // admin. Reading it here is what makes a FRESH INSTALL — which has no overlay yet
                // — agree with the server about what the pool can pay. Without it the bundled
                // floor listed, and admitted as payable, a prize `golfin_gacha_pull()` refuses.
                IsActive = f.GetBool("is_active", true),
            },
            (row, overlayRow) =>
            {
                // An overlay row still WINS — it is the newer word, and `is_active` lives there as
                // a table COLUMN rather than a data field, so the factory above cannot see it.
                if (overlayRow != null) row.IsActive = overlayRow.IsActive;
                row.MinBuild = overlayRow?.MinBuild ?? 0;
            });
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Ticket types — the currency a banner charges in.
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>One row of ticket_types.csv. <c>Id</c> is the int the ledger and
    /// <see cref="TicketType"/> both key on — never a slug.</summary>
    public sealed class TicketTypeEntry
    {
        public int    Id;
        public string Key        = string.Empty;
        public string NameEn     = string.Empty;
        public string NameJa     = string.Empty;
        public string IconSprite = string.Empty;
        public string IconUrl    = string.Empty;

        /// <summary>The name in the language currently selected, falling back to the other.</summary>
        public string DisplayName => GachaCsvMerge.PickLocalised(NameEn, NameJa);
    }

    /// <summary>
    /// Static catalog over <c>Resources/Data/ticket_types.csv</c> + the <c>ticket_types</c> overlay.
    /// </summary>
    public static class TicketTypeCatalog
    {
        private static Dictionary<int, TicketTypeEntry>? _byId;

        /// <summary>The published row for an id, or null when this build has never heard of it.</summary>
        public static TicketTypeEntry? Get(int id)
        {
            EnsureLoaded();
            return _byId!.TryGetValue(id, out var row) ? row : null;
        }

        /// <summary>Every published ticket type, for diagnostics and the ticket counter.</summary>
        public static IReadOnlyCollection<TicketTypeEntry> All
        {
            get { EnsureLoaded(); return _byId!.Values; }
        }

        /// <summary>Test / hot-reload hook: forces re-read on next access.</summary>
        public static void Reload() => _byId = null;

        private static void EnsureLoaded()
        {
            if (_byId != null) return;

            var asset = Resources.Load<TextAsset>("Data/ticket_types");
            if (asset == null)
            {
                Debug.LogError("[TicketTypeCatalog] ticket_types.csv not found in Resources/Data/.");
                _byId = new Dictionary<int, TicketTypeEntry>();
                return;
            }

            ContentCatalog? overlay = ContentCatalogStore.RequireReady(nameof(TicketTypeCatalog))
                ? ContentCatalogStore.Catalog(ContentCatalogs.TicketTypes)
                : null;

            var map = new Dictionary<int, TicketTypeEntry>();
            foreach (var r in Parse(asset.text, overlay)) map[r.Id] = r;
            _byId = map;

            // gacha_ops_polish §4 — WARM THE CACHE, or the admin's iconUrl upload is inert.
            //
            // `CatalogArtCache.Cached` never starts a download; it reads memory and then the
            // on-disk cache, and something has to have put the bytes there. The four
            // inventory/roster catalogs each end their load with this exact call
            // (CharacterDatabaseCSV:220, ClubDatabaseCSV:199, ItemDatabaseCSV:164,
            // BallDatabaseCSV:161); the two gacha catalogs never did, which made every
            // `iconUrl`/`artUrl` an operator uploads a URL nothing on the device ever fetches.
            // Fire-and-forget, allowlisted inside Request, and it lands for the NEXT launch —
            // which is exactly what §4 promises.
            var iconUrls = new List<string?>(map.Count);
            foreach (var r in map.Values) if (!string.IsNullOrWhiteSpace(r.IconUrl)) iconUrls.Add(r.IconUrl);
            Golfin.Tournaments.TournamentArtService.CatalogArt.Prefetch(iconUrls);
        }

        /// <summary>Testable seam: bundled text + optional overlay → rows. Pure.</summary>
        internal static List<TicketTypeEntry> Parse(string? csvText, ContentCatalog? overlay)
            => GachaCsvMerge.Merge(csvText, overlay, "id", f => new TicketTypeEntry
            {
                Id         = f.GetInt("id", -1),
                Key        = f.Get("key"),
                NameEn     = f.Get("nameEn"),
                NameJa     = f.Get("nameJa"),
                IconSprite = f.Get("iconSprite"),
                IconUrl    = f.Get("iconUrl"),
            });
    }

    // ─────────────────────────────────────────────────────────────────────────
    // The merge itself — one copy for all four gacha catalogs.
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The bundled-CSV + overlay merge, shared by the gacha catalogs.
    ///
    /// <para>
    /// It is <see cref="Golfin.Inventory.ClubCsvParser.Parse"/>'s loop with the club-specific row
    /// type lifted out into a factory: bundled row parsed first (the id has to come out of the CSV
    /// before the overlay can be looked up), patched field-by-field through
    /// <see cref="ContentFields"/> so a published row that names only <c>costX1</c> cannot blank
    /// the sprite by omission, then overlay ids the bundled file has never carried are appended.
    /// </para>
    /// <para>
    /// Not shared WITH <c>ClubCsvParser</c> because that one carries the club sprite-veto
    /// provenance (<c>bundled</c>, <c>overlayAppended</c>) the gacha rows have no use for; lifting
    /// both into one generic helper is a refactor of a shipping loader, which this task is not.
    /// </para>
    /// </summary>
    internal static class GachaCsvMerge
    {
        /// <summary>Rows that carry <c>is_active=false</c> are dropped by the caller's predicate;
        /// this overload keeps every row and reports activation through <paramref name="after"/>.</summary>
        internal static List<T> Merge<T>(string? csvText, ContentCatalog? overlay, string idColumn,
                                         Func<ContentFields, T> build,
                                         Action<T, ContentRow?>? after = null)
            where T : class
        {
            var rows = new List<T>();
            if (string.IsNullOrWhiteSpace(csvText) && overlay == null) return rows;

            var seen = new HashSet<string>(StringComparer.Ordinal);

            var lines = (csvText ?? string.Empty).Split('\n');
            int headerLine = -1;
            for (int i = 0; i < lines.Length; i++)
                if (!IsSkippable(lines[i])) { headerLine = i; break; }

            if (headerLine >= 0)
            {
                var headers = ParseCsvLine(lines[headerLine].Trim());
                var idx = new Dictionary<string, int>(StringComparer.Ordinal);
                for (int c = 0; c < headers.Count; c++) idx[headers[c].Trim()] = c;

                for (int i = headerLine + 1; i < lines.Length; i++)
                {
                    if (IsSkippable(lines[i])) continue;

                    var fields = ParseCsvLine(lines[i].Trim());
                    var bundledFields = ContentFields.Csv(fields, idx);

                    string id = bundledFields.Get(idColumn);
                    if (string.IsNullOrEmpty(id)) continue;   // a row with no id is not a row
                    seen.Add(id);

                    ContentRow? patch = null;
                    overlay?.ById.TryGetValue(id, out patch);

                    T row = build(patch == null ? bundledFields : ContentFields.Csv(fields, idx, patch));
                    after?.Invoke(row, patch);
                    rows.Add(row);
                }
            }

            if (overlay != null)
            {
                foreach (var overlayRow in overlay.Rows)
                {
                    if (seen.Contains(overlayRow.Id)) continue;

                    T row = build(ContentFields.OverlayOnly(overlayRow));
                    after?.Invoke(row, overlayRow);
                    rows.Add(row);
                }
            }

            return rows;
        }

        /// <summary>Blank lines and <c>#</c> comment lines carry no data — same rule as Clubs.csv.</summary>
        internal static bool IsSkippable(string? line)
            => string.IsNullOrWhiteSpace(line) || line!.TrimStart().StartsWith("#", StringComparison.Ordinal);

        /// <summary>
        /// A rarity NAME as the pool and rate tables spell it ("Legendary"). Unknown or blank is
        /// Common — the same default <c>ClubCsvParser</c> uses, and the safe one: a tier nobody can
        /// parse must not silently become the rarest.
        /// </summary>
        internal static CharacterRarity ParseRarity(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return CharacterRarity.Common;
            return Enum.TryParse(raw!.Trim(), ignoreCase: true, out CharacterRarity r)
                ? r
                : CharacterRarity.Common;
        }

        /// <summary>
        /// The JA string when the game is in Japanese and it is non-blank, the EN one otherwise —
        /// and the OTHER one when the preferred side is blank, so a half-filled row still renders
        /// something rather than an empty label.
        /// </summary>
        internal static string PickLocalised(string? en, string? ja)
        {
            bool japanese = LocalizationManager.CurrentLanguage == Language.Japanese;
            string first  = japanese ? (ja ?? string.Empty) : (en ?? string.Empty);
            string second = japanese ? (en ?? string.Empty) : (ja ?? string.Empty);
            return !string.IsNullOrWhiteSpace(first) ? first.Trim() : (second ?? string.Empty).Trim();
        }

        /// <summary>
        /// Splits one CSV line on commas, honouring double-quoted fields. Same logic as
        /// <c>GachaBannerCatalog.ParseCsvLine</c> — see the note there on why the four copies in
        /// this project have not been lifted into one helper.
        /// </summary>
        internal static List<string> ParseCsvLine(string line)
        {
            var fields  = new List<string>();
            var current = new System.Text.StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"') { current.Append('"'); i++; }
                    else inQuotes = !inQuotes;
                }
                else if (c == ',' && !inQuotes) { fields.Add(current.ToString()); current.Clear(); }
                else current.Append(c);
            }

            fields.Add(current.ToString());
            return fields;
        }
    }
}
