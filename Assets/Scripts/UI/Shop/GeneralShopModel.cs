// Assets/Scripts/UI/Shop/GeneralShopModel.cs
// Order 610 — general_shop_ui (Phase B)
// Catalog data layer for the Rewards Center STORE tab. CSV-first, mirrors ShopCatalog (517).
//
// content_overlay_catalogs (Phase 2) added three things to this file:
//   §1  the shop_catalog overlay merges field-by-field over the bundled CSV;
//   §4  is_active=false drops the row from the STORE (I6 — it is still owned and renderable
//       everywhere else, this list is the "can be acquired" view);
//   §6  startAt / endAt / saleStartAt / saleEndAt are HONOURED. The columns have shipped in
//       shop_catalog.csv since content_panels_gaps and this loader has never read them, so every
//       window an operator has authored so far has been silently ignored.

#nullable enable
using System;
using System.Collections.Generic;
using Golfin.Content;
using Golfin.Inventory;
using Golfin.Roster;
using UnityEngine;

namespace GolfinRedux.UI.Shop
{
    /// <summary>
    /// Which inventory system an entry grants into.
    ///
    /// <para>
    /// Characters and Items joined Clubs and Balls in shop_server_purchase §3.3. The admin Shop panel
    /// could already publish both (<c>SHOP_CATEGORY_TO_CATALOG</c> has covered them since the panel
    /// shipped) — the client just parsed every non-<c>ball</c> category as a Club, so a published
    /// character row rendered as a broken club card. <c>bag</c> is deliberately still absent: the
    /// grants queue has no bag kind, so the server refuses to sell one and a card for it could only
    /// ever fail.
    /// </para>
    /// </summary>
    public enum ShopCategory
    {
        Club,
        Ball,
        Character,
        Item
    }

    /// <summary>
    /// One purchasable row of shop_catalog.csv. RefId points at a clubId (ClubDatabaseCSV) or
    /// ballId (BallDatabaseCSV); the card resolves name/stats/sprites from that DB at bind time.
    /// RP prices are authored here (D2 re-token of the Figma $ prices — design values, not $→RP).
    /// </summary>
    [Serializable]
    public class ShopCatalogEntry
    {
        public string       EntryId    { get; set; } = string.Empty;
        public ShopCategory Category   { get; set; } = ShopCategory.Club;
        public string       RefId      { get; set; } = string.Empty;
        public int          RpCost     { get; set; }
        public int          SaleRpCost { get; set; }   // 0 or ==RpCost → no sale strike
        public int          SortOrder  { get; set; }
        public bool         Popular    { get; set; }   // v1 unused (POPULAR curation grayed)
        public bool         Offer      { get; set; }   // v1 unused (OFFERS curation grayed)
        public string       Rarity     { get; set; } = string.Empty; // ball display-rarity (clubs use DB rarity)

        // ── Scheduling (content_panels_gaps columns; honoured since content_overlay_catalogs §6) ──

        /// <summary>ISO-8601 listing start, INCLUSIVE. Empty = unbounded.</summary>
        public string StartAt     { get; set; } = string.Empty;

        /// <summary>ISO-8601 listing end, <b>EXCLUSIVE</b>. Empty = unbounded.</summary>
        public string EndAt       { get; set; } = string.Empty;

        /// <summary>ISO-8601 sale start, INCLUSIVE. Empty = unbounded.</summary>
        public string SaleStartAt { get; set; } = string.Empty;

        /// <summary>ISO-8601 sale end, <b>EXCLUSIVE</b>. Empty = unbounded.</summary>
        public string SaleEndAt   { get; set; } = string.Empty;

        /// <summary>
        /// False when the operator deactivated the row (I6). It leaves the STORE and nothing else:
        /// a club already bought stays owned, renderable and equipped.
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Resolved once at load: whether the SALE WINDOW is open right now. False outside it, in
        /// which case <see cref="SaleRpCost"/> is ignored entirely and the row sells at list price
        /// (§6 rule 2 — a sale window discounts a listing, it never IS the listing).
        /// </summary>
        public bool SaleWindowOpen { get; set; } = true;

        /// <summary>True when a discounted sale price applies (strike-through affordance).</summary>
        public bool HasSale => SaleWindowOpen && SaleRpCost > 0 && SaleRpCost < RpCost;

        /// <summary>The price actually charged: sale price when on sale, else the list price.</summary>
        public int EffectiveRpCost => HasSale ? SaleRpCost : RpCost;
    }

    /// <summary>
    /// Catalog: loads shop_catalog.csv from Resources/Data/ on first access, merges the
    /// admin-published <c>shop_catalog</c> overlay on top, then filters by <c>is_active</c> and by
    /// the listing window.
    ///
    /// <para>
    /// ⚠️ <b>The window is evaluated ONCE, at load.</b> That matches I5 — content applies at launch,
    /// not mid-session — and it means a row does not vanish out from under a player who is mid-
    /// purchase. A window that opens or closes while the app is foregrounded takes effect on the
    /// next launch, exactly like every other content change.
    /// </para>
    /// </summary>
    public static class GeneralShopCatalog
    {
        private static List<ShopCatalogEntry>? _entries;

        /// <summary>Rows dropped this load because their category is not one this build can sell.
        /// Reset at the top of every load and folded into the summary line so an operator who
        /// published a <c>bag</c> (or a typo) can see it went nowhere, rather than wondering why the
        /// row never appears.</summary>
        private static int _droppedUnknownCategory;

        /// <summary>Rows withheld this load because the thing they SELL could not be resolved —
        /// no row in the client's database, a deactivated one, or no usable sprite
        /// (shop_stocking §6). Same treatment as the counters above: reset per load, reported in
        /// the summary line.</summary>
        private static int _unresolvable;

        /// <summary>Databases already reported absent this load, so the EditMode "no scene, no
        /// singletons" case logs ONCE per database instead of once per row.</summary>
        private static readonly HashSet<string> _dbAbsentLogged = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>
        /// TEST SEAM, set by reflection from the EditMode suite and by nothing else.
        ///
        /// <para>
        /// Returns the reason a row is unrenderable, or <c>null</c> when it is fine. The logic under
        /// test is the SHIPPING <see cref="Admit"/>; what this replaces is only the database lookup,
        /// which in an EditMode test has no scene to live in and would otherwise take the
        /// "no database — skip resolution" path on every row and prove nothing.
        /// </para>
        /// </summary>
        private static Func<ShopCatalogEntry, string?>? _resolverOverride;

        public static IReadOnlyList<ShopCatalogEntry> Entries
        {
            get { EnsureLoaded(); return _entries!; }
        }

        /// <summary>Entries for a category (null = ALL), sorted by SortOrder.</summary>
        public static List<ShopCatalogEntry> GetByCategory(ShopCategory? category)
        {
            EnsureLoaded();
            var result = new List<ShopCatalogEntry>();
            foreach (var e in _entries!)
                if (category == null || e.Category == category.Value)
                    result.Add(e);
            result.Sort((a, b) => a.SortOrder.CompareTo(b.SortOrder));
            return result;
        }

        private static void EnsureLoaded()
        {
            if (_entries != null) return;
            LoadFromCsv();
        }

        private static void LoadFromCsv()
        {
            _entries = new List<ShopCatalogEntry>();
            _droppedUnknownCategory = 0;
            _unresolvable = 0;
            _dbAbsentLogged.Clear();
            var asset = Resources.Load<TextAsset>("Data/shop_catalog");
            if (asset == null)
            {
                Debug.LogError("[GeneralShopCatalog] shop_catalog.csv not found in Resources/Data/.");
                return;
            }

            // This is a static class with no Awake, so it loads lazily on first access — well after
            // ContentService (-900). RequireReady is still asked, because a lazy first access from
            // an EditMode test has no ContentService at all and must run bundled without an error.
            ContentCatalog? overlay = ContentCatalogStore.RequireReady(nameof(GeneralShopCatalog))
                ? ContentCatalogStore.Catalog(ContentCatalogs.ShopCatalog)
                : null;

            DateTime nowUtc = DateTime.UtcNow;

            var lines = asset.text.Split('\n');
            if (lines.Length < 2) { Debug.LogError("[GeneralShopCatalog] shop_catalog.csv is empty."); return; }

            var headerIndex = BuildHeaderIndex(ParseCsvLine(lines[0]));
            var seen = new HashSet<string>(StringComparer.Ordinal);
            int overlaid = 0, deactivated = 0, outOfWindow = 0, failedClosed = 0;

            for (int i = 1; i < lines.Length; i++) // skip header
            {
                var line = lines[i].Trim();
                if (string.IsNullOrEmpty(line)) continue;

                var fields = ParseCsvLine(line);

                var bundled = ParseRow(ContentFields.Csv(fields, headerIndex));
                if (bundled == null) continue;

                seen.Add(bundled.EntryId);

                ContentRow? patch = null;
                overlay?.ById.TryGetValue(bundled.EntryId, out patch);

                var entry = bundled;
                if (patch != null)
                {
                    var merged = ParseRow(ContentFields.Csv(fields, headerIndex, patch));
                    if (merged != null) { entry = merged; overlaid++; }
                }

                Admit(entry, nowUtc, ref deactivated, ref outOfWindow, ref failedClosed);
            }

            // Append overlay rows shop_catalog.csv has never carried.
            if (overlay != null)
            {
                foreach (var row in overlay.Rows)
                {
                    if (seen.Contains(row.Id)) continue;

                    var appended = ParseRow(ContentFields.OverlayOnly(row));
                    if (appended == null) continue;

                    overlaid++;
                    Admit(appended, nowUtc, ref deactivated, ref outOfWindow, ref failedClosed);
                }
            }

            Debug.Log($"[GeneralShopCatalog] Loaded {_entries.Count} catalog entries" +
                      (overlay == null ? " — BUNDLED only, no shop_catalog overlay this launch."
                                       : $" — overlay v{overlay.Version}: {overlaid} row(s) patched/appended.") +
                      $" Withheld: {deactivated} deactivated (I6), {outOfWindow} outside their listing " +
                      $"window, {failedClosed} dropped for an unparseable bound (§6, fail closed), " +
                      $"{_droppedUnknownCategory} dropped for an unsellable category (§3.3), " +
                      $"{_unresolvable} withheld as unrenderable (shop_stocking §6).");
        }

        /// <summary>
        /// Decide whether one entry belongs in the STORE, and whether its sale price applies.
        /// Both filters live here so a bundled row and an appended overlay row can never be judged
        /// by different rules.
        /// </summary>
        private static void Admit(ShopCatalogEntry entry, DateTime nowUtc,
                                  ref int deactivated, ref int outOfWindow, ref int failedClosed)
        {
            // I6 — deactivated leaves the STORE and nothing else. A club already bought stays
            // owned, renderable and equipped; ClubDatabaseCSV.GetAllClubs() still carries its row.
            if (!entry.IsActive)
            {
                deactivated++;
                Debug.Log($"[GeneralShopCatalog] '{entry.EntryId}' is deactivated (is_active=false) — " +
                          $"withheld from the store. Anyone who already owns '{entry.RefId}' keeps it (I6).");
                return;
            }

            var verdict = ContentShopWindow.Evaluate(
                new ShopWindowSpec(entry.StartAt, entry.EndAt, entry.SaleStartAt, entry.SaleEndAt),
                nowUtc);

            if (!verdict.Listed)
            {
                // "unparseable" is called out separately from "not yet / no longer scheduled",
                // because one is an operator authoring an intent and the other is an operator
                // making a typo, and only the second one needs somebody to go and fix it.
                bool authoring = verdict.Reason.Contains("unparseable");
                if (authoring) failedClosed++; else outOfWindow++;

                if (authoring)
                    Debug.LogWarning($"[GeneralShopCatalog] '{entry.EntryId}' DROPPED — {verdict.Reason}. " +
                                     $"Fix the bound in the admin; the row cannot be sold until it parses.");
                else
                    Debug.Log($"[GeneralShopCatalog] '{entry.EntryId}' withheld — {verdict.Reason}.");
                return;
            }

            // ---- shop_stocking §6: never list what this build cannot render ----
            //
            // The row itself being published is not enough. Rendering a card needs the REFERENCED
            // row in the client's own database (bundled or overlaid) and a real sprite for it. A
            // row that is missing either used to be admitted, instantiated, and then early-returned
            // half-bound by `GeneralShopCard.Bind*` — a blank card with a live BUY button. With
            // server pricing that card cannot even succeed: `golfin_shop_purchase` refuses the same
            // row as `not_listed` / `ref_inactive`. So it is withheld here, loudly, and counted.
            string? unrenderable = UnrenderableReason(entry);
            if (unrenderable != null)
            {
                _unresolvable++;
                Debug.LogWarning($"[GeneralShopCatalog] '{entry.EntryId}' WITHHELD — {entry.Category} " +
                                 $"ref '{entry.RefId}' cannot be rendered by this build: {unrenderable}. " +
                                 "Either the row is not in this build's catalog (publish it and export " +
                                 "the CSVs), or its art has not shipped yet.");
                return;
            }

            entry.SaleWindowOpen = verdict.OnSale;
            if (!verdict.OnSale && entry.SaleRpCost > 0)
                Debug.Log($"[GeneralShopCatalog] '{entry.EntryId}' is listed but OUTSIDE its sale window — " +
                          $"saleRpCost {entry.SaleRpCost} ignored, selling at rpCost {entry.RpCost} (§6).");

            _entries!.Add(entry);
        }

        /// <summary>
        /// Why <paramref name="entry"/> cannot be rendered by THIS build, or <c>null</c> when it can.
        ///
        /// <para>
        /// Three ways to fail, and they are reported separately because they need different people:
        /// no row in the client's database is a CONTENT problem (publish + export), a deactivated
        /// row is an OPERATOR decision the shop row did not follow, and a missing sprite is an ART
        /// problem. A <c>Placeholder</c> sprite counts as missing: <c>ClubDatabaseCSV</c>
        /// substitutes the shared placeholder asset for a name the art batches have not produced,
        /// so "not null" is not the same question as "has art".
        /// </para>
        /// <para>
        /// A NULL DATABASE SINGLETON IS NOT A FAILURE. In an EditMode test — or any lazy first
        /// access before the scene's singletons exist — there is nothing to resolve against, and
        /// treating that as unrenderable would withhold every row in the catalog. It logs once per
        /// database per load and admits, exactly like <c>RequireReady</c> does for a missing
        /// <c>ContentService</c> further up.
        /// </para>
        /// </summary>
        private static string? UnrenderableReason(ShopCatalogEntry entry)
        {
            if (_resolverOverride != null) return _resolverOverride(entry);

            switch (entry.Category)
            {
                case ShopCategory.Ball:
                {
                    var db = BallDatabaseCSV.Instance;
                    if (db == null) return NoDatabase(nameof(BallDatabaseCSV));
                    var ball = db.GetBall(entry.RefId);
                    if (ball == null) return "no row in the balls catalog";
                    if (!ball.isActive) return "the balls row is deactivated";
                    return Usable(ball.thumbnailSprite != null ? ball.thumbnailSprite : ball.fullSprite)
                        ? null : "no usable ball sprite";
                }

                case ShopCategory.Character:
                {
                    var db = CharacterDatabaseCSV.Instance;
                    if (db == null) return NoDatabase(nameof(CharacterDatabaseCSV));
                    var ch = db.GetCharacter(entry.RefId);
                    if (ch == null) return "no row in the characters catalog";
                    if (!ch.isActive) return "the characters row is deactivated";
                    return Usable(ch.portraitSprite != null ? ch.portraitSprite : ch.portraitFullSprite)
                        ? null : "no usable character portrait";
                }

                case ShopCategory.Item:
                {
                    var db = ItemDatabaseCSV.Instance;
                    if (db == null) return NoDatabase(nameof(ItemDatabaseCSV));
                    var item = db.GetItem(entry.RefId);
                    if (item == null) return "no row in the items catalog";
                    if (!item.isActive) return "the items row is deactivated";
                    return Usable(item.thumbnailSprite != null ? item.thumbnailSprite : item.fullSprite)
                        ? null : "no usable item sprite";
                }

                default:
                {
                    var db = ClubDatabaseCSV.Instance;
                    if (db == null) return NoDatabase(nameof(ClubDatabaseCSV));
                    var club = db.GetClub(entry.RefId);
                    if (club == null) return "no row in the clubs catalog";
                    if (!club.isActive) return "the clubs row is deactivated";
                    return Usable(club.portraitSprite != null ? club.portraitSprite : club.portraitFull)
                        ? null : "no usable club sprite";
                }
            }
        }

        /// <summary>Name of the shared stand-in art every database falls back to. Compared BY NAME
        /// because that is all the loaded <see cref="Sprite"/> carries — the databases hand back the
        /// placeholder asset itself, not a marker.</summary>
        private const string PlaceholderSpriteName = "Placeholder";

        private static bool Usable(Sprite? sprite)
            => sprite != null &&
               !string.Equals(sprite.name, PlaceholderSpriteName, StringComparison.OrdinalIgnoreCase);

        /// <summary>Logs once per database per load and admits the row. See the note on
        /// <see cref="UnrenderableReason"/>.</summary>
        private static string? NoDatabase(string database)
        {
            if (_dbAbsentLogged.Add(database))
                Debug.Log($"[GeneralShopCatalog] no {database} this load — reference resolution not " +
                          "checked for its category (shop_stocking §6). Expected in EditMode and on a " +
                          "lazy first access before the scene singletons exist; NOT expected in the store.");
            return null;
        }

        /// <summary>
        /// One row from whatever <see cref="ContentFields"/> stands in front of it — bundled,
        /// bundled+overlay, or overlay alone. Column names declared once, here (I4).
        /// <para>
        /// Reads by NAME rather than by the old fixed column indices: the overlay is keyed by
        /// column name, and shop_catalog.csv has already grown four columns since this loader was
        /// written. An index-based reader silently mis-assigns every column after an insertion.
        /// </para>
        /// </summary>
        private static ShopCatalogEntry? ParseRow(ContentFields f)
        {
            string entryId = f.Get("entryId");
            if (string.IsNullOrEmpty(entryId)) return null;

            string rawCategory = f.Get("category");
            ShopCategory? category = ParseCategory(rawCategory);
            if (category == null)
            {
                // Counted and logged by the caller (Admit is never reached), so the operator can find
                // the row. `bag` lands here on purpose — see ParseCategory.
                _droppedUnknownCategory++;
                Debug.LogWarning($"[GeneralShopCatalog] '{entryId}' DROPPED — category " +
                                 $"'{rawCategory}' is not one this build can sell " +
                                 "(club | ball | character | item). Fix it in the admin, or ship a " +
                                 "build that knows the category.");
                return null;
            }

            return new ShopCatalogEntry
            {
                EntryId     = entryId,
                Category    = category.Value,
                RefId       = f.Get("refId"),
                RpCost      = f.GetInt("rpCost"),
                SaleRpCost  = f.GetInt("saleRpCost"),
                SortOrder   = f.GetInt("sortOrder"),
                Popular     = f.GetBool("popular"),
                Offer       = f.GetBool("offer"),
                Rarity      = f.Get("rarity"),
                StartAt     = f.Get("startAt"),
                EndAt       = f.Get("endAt"),
                SaleStartAt = f.Get("saleStartAt"),
                SaleEndAt   = f.Get("saleEndAt"),
                IsActive    = f.IsActive,
            };
        }

        private static Dictionary<string, int> BuildHeaderIndex(List<string> headers)
        {
            var idx = new Dictionary<string, int>();
            for (int i = 0; i < headers.Count; i++)
                idx[headers[i].Trim()] = i;
            return idx;
        }

        /// <summary>
        /// Quote-aware, unlike the <c>line.Split(',')</c> this replaced. shop_catalog.csv has no
        /// quoted commas today, but every other catalog reader in the project is quote-aware and a
        /// published <c>name</c> containing a comma would otherwise shift every later column.
        /// </summary>
        private static List<string> ParseCsvLine(string line)
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

        /// <summary>
        /// The four categories this client can render AND the server will sell. Anything else returns
        /// null and the row is DROPPED.
        ///
        /// <para>
        /// This used to be <c>== "ball" ? Ball : Club</c> — so a typo, a <c>bag</c>, or a category from
        /// a newer server all became a club card bound to a refId <c>ClubDatabaseCSV</c> has never
        /// heard of. Falling back to Club is exactly the wrong default: it is the category with the
        /// most machinery behind it (owned state, durability, level) and therefore the one that fails
        /// most confusingly. Dropping the row means the operator sees a warning naming the entryId and
        /// the category, and the player sees nothing — which is the correct outcome for a listing this
        /// build cannot honour.
        /// </para>
        /// </summary>
        private static ShopCategory? ParseCategory(string s)
        {
            switch ((s ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "club":      return ShopCategory.Club;
                case "ball":      return ShopCategory.Ball;
                case "character": return ShopCategory.Character;
                case "item":      return ShopCategory.Item;
                default:          return null;
            }
        }

        /// <summary>Test/hot-reload hook: forces a re-read on next access.</summary>
        public static void Reload() { _entries = null; _resolverOverride = null; }
    }
}
