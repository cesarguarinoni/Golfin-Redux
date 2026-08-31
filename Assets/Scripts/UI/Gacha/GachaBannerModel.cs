// Assets/Scripts/UI/Gacha/GachaBannerModel.cs
// gacha_screen Stage 2 — §3a Banner Catalog
// gacha_admin_catalogs §3 — header-INDEXED quote-aware parse (was positional Split(',')).
// gacha_client_real_pull §2/§3.1 — the admin overlay, the twenty-two columns, the WITHHOLD rule
//   and the 5b same-session re-apply.
//
// THE CATALOG IS NO LONGER BUNDLED-ONLY. LoadFromCsv now merges the published `gacha_banners`
// overlay on top of the shipped CSV in the ClubDatabaseCSV.LoadCSV shape (ClubDatabaseCSV.cs:92-97):
// bundled is the floor (I1), a published row patches FIELD-BY-FIELD, an unknown id is APPENDED, and
// `is_active=false` drops the row. RequireReady false — EditMode, a lab scene, a build with no
// ContentService — keeps the bundled table, silently and correctly.
//
// AND A BANNER IS NOW WITHHELD RATHER THAN SHOWN BROKEN (§3.1). `GetLiveBanners` no longer asks
// only "is it active and unexpired": it asks whether this build could actually complete the pull
// the card offers — window open on the device clock, a pool whose rates sum to 10 000 with a
// resolvable entry behind every rated tier, a published ticket type, and art. That is the CLIENT's
// copy of `golfin_gacha_pull()` step 8. Two locks, neither trusting the other: the client hides
// what it cannot render, the server refuses to pay what the client could not show.
//
// Internal testable seams:
//   ParseCsv(string csvText)                                — bundled-only parse
//   ParseCsv(string csvText, ContentCatalog? overlay)       — the merge
//   GetLiveBanners(IEnumerable<GachaBannerEntry>, DateTime) — window filter ONLY (unchanged shape)
//   IsRollable(GachaBannerEntry, IRefResolver, out reason)  — the §3.1 invariant, pure
// Exercised by GachaStage2Tests / GachaClientRealPullTests via reflection; InternalsVisibleTo
// exposes them for direct access if a test assembly is ever given a compile-time reference.

using System;
using System.Collections.Generic;
using Golfin.Content;
using Golfin.Roster;
using UnityEngine;

// Must appear after using directives (C# grammar: using-directives precede global-attributes).
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("GolfinRedux.Tests.EditMode")]

namespace GolfinRedux.UI.Gacha
{
    /// <summary>
    /// One row of gacha_banners.csv — all twenty-two columns.
    ///
    /// <para>
    /// The first nine (bannerId … active) have shipped since gacha_screen Stage 2. The other
    /// thirteen arrived with the admin catalog (gacha_admin_catalogs §4) and were parsed into
    /// nothing until this task; they land here now because the card renders them and the withhold
    /// rule reads them. <c>taglineEn</c>/<c>taglineJa</c> are the exception and are still parsed
    /// into nothing — the card is TITLE ONLY (Cesar, 2026-08-31).
    /// </para>
    /// </summary>
    [Serializable]
    public class GachaBannerEntry
    {
        public string   BannerId  { get; set; } = string.Empty;
        public string   NameKey   { get; set; } = string.Empty;
        /// <summary>Filename (no path, no extension). Load via Resources.Load&lt;Sprite&gt;("Art/Gacha/Banners/" + ArtSprite).</summary>
        public string   ArtSprite { get; set; } = string.Empty;
        public int      CostX1    { get; set; }
        public int      CostX10   { get; set; }
        /// <summary>ISO-8601 UTC string parsed to DateTime (Kind=Utc). MaxValue = unbounded.</summary>
        public DateTime EndUtc    { get; set; } = DateTime.MaxValue;
        public string   RulesUrl  { get; set; } = string.Empty;
        public int      SortOrder { get; set; }
        public bool     Active    { get; set; }

        // ── The admin-catalog columns (gacha_admin_catalogs §4) ───────────────

        /// <summary>ISO-8601 listing start, INCLUSIVE. MinValue = unbounded, the same
        /// absent-means-unbounded rule the shop and the server's bound parser both use.</summary>
        public DateTime StartUtc { get; set; } = DateTime.MinValue;

        /// <summary>The prize table this banner rolls against (<c>gacha_pools.poolId</c>).</summary>
        public string PoolId { get; set; } = string.Empty;

        /// <summary>The currency it charges in — a <c>ticket_types.id</c>, and the int
        /// <see cref="TicketType"/> and the server ledger both key on.</summary>
        public int TicketType { get; set; }

        /// <summary>Pulls before a minimum rarity is FORCED. 0 = this banner has no pity.</summary>
        public int PityThreshold { get; set; }

        /// <summary>The rarity pity forces. Only meaningful when <see cref="PityThreshold"/> &gt; 0.</summary>
        public CharacterRarity PityMinRarity { get; set; } = CharacterRarity.Common;

        /// <summary>True when the banner declares an x10 floor at all — a blank column is "no
        /// guarantee", which is NOT the same as a guarantee of Common.</summary>
        public bool HasGuaranteeX10 { get; set; }

        /// <summary>The rarity every x10 is guaranteed at least one of. Read only when
        /// <see cref="HasGuaranteeX10"/>.</summary>
        public CharacterRarity GuaranteeMinRarityX10 { get; set; } = CharacterRarity.Common;

        /// <summary>Per-player pull cap, or null when uncapped.</summary>
        public int? MaxPullsPerPlayer { get; set; }

        /// <summary>Remote art URL (admin upload). Resolved through the CatalogArtCache ladder.</summary>
        public string ArtUrl { get; set; } = string.Empty;

        /// <summary>UI-authored display title, English. Beats <see cref="NameKey"/>.</summary>
        public string NameEn { get; set; } = string.Empty;

        /// <summary>UI-authored display title, Japanese.</summary>
        public string NameJa { get; set; } = string.Empty;

        /// <summary>Featured prize refs, <c>;</c>-separated. Parsed but NOT rendered — the card's
        /// featured strip is spec D.</summary>
        public string[] FeaturedRefIds { get; set; } = Array.Empty<string>();

        /// <summary>I6 — false means the operator deactivated the published row. Such a row is
        /// dropped from the catalog outright: unlike a club, nobody OWNS a banner.</summary>
        public bool IsActive { get; set; } = true;

        /// <summary>True when Active and the banner has not yet expired relative to the device UTC
        /// clock. Kept for callers that only ask the old question; the full rule is
        /// <see cref="GachaBannerCatalog.IsRollable"/>.</summary>
        public bool IsLive => Active && EndUtc > DateTime.UtcNow;
    }

    /// <summary>
    /// What a banner's pool needs from this build's databases, behind an interface so the §3.1
    /// invariant can be driven by an EditMode fixture with no scene and no singletons.
    /// </summary>
    public interface IRefResolver
    {
        /// <summary>True when <paramref name="refId"/> of <paramref name="kind"/> resolves to an
        /// ACTIVE, RENDERABLE row in this build. Unknown kinds answer false.</summary>
        bool Resolves(string kind, string refId);

        /// <summary>True when the ticket type id is one this build has a published row for.</summary>
        bool TicketTypeExists(int ticketType);

        /// <summary>True when the banner's art resolves to something drawable — the same ladder the
        /// card uses, so "the card can draw it" and "the catalog admits it" cannot disagree.</summary>
        bool ArtResolves(GachaBannerEntry entry);
    }

    /// <summary>
    /// Static catalog: loads gacha_banners.csv from Resources/Data/ on first access and merges the
    /// published <c>gacha_banners</c> overlay on top. Mirrors GeneralShopCatalog / ClubDatabaseCSV.
    /// </summary>
    public static class GachaBannerCatalog
    {
        private static List<GachaBannerEntry> _entries;

        /// <summary>The build number the pool's per-entry <c>min_build</c> is compared against.
        /// Read once per load so a test can pin it without a ContentService.</summary>
        internal static int BuildForWithhold = -1;   // -1 → ask ContentBuildNumber

        public static IReadOnlyList<GachaBannerEntry> Entries
        {
            get { EnsureLoaded(); return _entries; }
        }

        /// <summary>
        /// The banners this build may show, sorted by SortOrder — every clause of SPEC §3.1
        /// applied. A banner that fails ANY of them is WITHHELD, counted, and named once per load.
        /// </summary>
        public static List<GachaBannerEntry> GetLiveBanners()
        {
            EnsureLoaded();

            var windowed = GetLiveBanners(_entries, DateTime.UtcNow);
            var live     = new List<GachaBannerEntry>(windowed.Count);
            var withheld = new List<string>();

            IRefResolver resolver = LiveResolver.Instance;
            foreach (var e in windowed)
            {
                if (IsRollable(e, resolver, out string reason)) live.Add(e);
                else withheld.Add($"{e.BannerId} — {reason}");
            }

            // ONE warning per load, in the club-loader shape: an operator who published a banner
            // this build cannot complete needs to see WHY, and one line per banner in a per-frame
            // rebuild would drown the console.
            if (withheld.Count > 0)
                Debug.LogWarning($"[GachaBannerCatalog] {withheld.Count} banner(s) withheld: " +
                                 string.Join("; ", withheld) + ". A withheld banner is one this " +
                                 "build could not complete a pull on — publish the missing row, " +
                                 "export the CSVs, or upload the art.");

            return live;
        }

        /// <summary>
        /// Testable seam: the WINDOW filter only — Active, published-active, and
        /// <c>StartUtc ≤ nowUtc &lt; EndUtc</c> — sorted by SortOrder ascending.
        ///
        /// <para>
        /// The start side is new (§3.1). It is what makes a SCHEDULED banner invisible rather than
        /// visible-with-a-countdown: the carousel's countdown label ticks <c>EndUtc</c>, so a
        /// not-yet-started banner rendered here would show the wrong clock entirely.
        /// </para>
        /// <para>
        /// Deliberately does NOT apply the pool / ticket / art clauses — those need this build's
        /// databases, and keeping them out is what lets the pre-existing GachaStage2Tests drive
        /// this overload with bare entries and no scene.
        /// </para>
        /// </summary>
        internal static List<GachaBannerEntry> GetLiveBanners(IEnumerable<GachaBannerEntry> entries, DateTime nowUtc)
        {
            var result = new List<GachaBannerEntry>();
            foreach (var e in entries)
                if (e.Active && e.IsActive && e.StartUtc <= nowUtc && e.EndUtc > nowUtc)
                    result.Add(e);
            result.Sort((a, b) => a.SortOrder.CompareTo(b.SortOrder));
            return result;
        }

        /// <summary>
        /// SPEC §3.1 — can this build complete a pull on <paramref name="entry"/>?
        ///
        /// <para>
        /// This is the client's copy of <c>golfin_gacha_pull()</c> steps 5 and 8. It asks four
        /// questions the window filter cannot: is there a POOL whose rate table sums to 10 000;
        /// does every rated tier have at least one entry this build can actually pay out (active,
        /// within this build's <c>min_build</c>, and resolvable in the local database); is the
        /// TICKET TYPE published; and does the ART resolve.
        /// </para>
        /// <para>
        /// The window is NOT re-checked here — <see cref="GetLiveBanners(IEnumerable{GachaBannerEntry}, DateTime)"/>
        /// owns that and runs first, so a fixture can test the two independently.
        /// </para>
        /// </summary>
        /// <param name="reason">Why it was withheld. Empty when it is rollable.</param>
        internal static bool IsRollable(GachaBannerEntry entry, IRefResolver resolver, out string reason)
        {
            reason = string.Empty;
            if (entry == null) { reason = "null entry"; return false; }

            // ── Ticket type ───────────────────────────────────────────────────
            if (resolver != null && !resolver.TicketTypeExists(entry.TicketType))
            {
                reason = $"ticket type {entry.TicketType} is not published";
                return false;
            }

            // ── Pool + rates ──────────────────────────────────────────────────
            if (string.IsNullOrEmpty(entry.PoolId)) { reason = "no poolId"; return false; }

            var rates = GachaRatesCatalog.ForPool(entry.PoolId);
            if (rates.Count == 0) { reason = $"pool '{entry.PoolId}' has no rate table"; return false; }

            int sum = 0;
            foreach (var r in rates) sum += r.RateBp;
            if (sum != 10000)
            {
                reason = $"pool '{entry.PoolId}' rates sum to {sum}, not 10000";
                return false;
            }

            var pool = GachaPoolCatalog.ForPool(entry.PoolId);
            int build = BuildForWithhold >= 0 ? BuildForWithhold : ContentBuildNumber.Current;

            foreach (var rate in rates)
            {
                if (rate.RateBp <= 0) continue;   // a tier at 0 is never rolled — it needs no entry

                bool payable = false;
                foreach (var p in pool)
                {
                    if (p.Rarity != rate.Rarity) continue;
                    if (!p.IsActive) continue;
                    if (p.MinBuild > build) continue;                 // the server's step-8 build lock
                    if (p.Weight <= 0) continue;                      // weight 0 is never drawn
                    if (resolver != null && !resolver.Resolves(p.Kind, p.RefId)) continue;
                    payable = true;
                    break;
                }

                if (!payable)
                {
                    reason = $"pool '{entry.PoolId}' has no payable {rate.Rarity} entry for build {build}, " +
                             $"but {rate.Rarity} is rated at {rate.RateBp}bp";
                    return false;
                }
            }

            // ── Art ───────────────────────────────────────────────────────────
            // LAST, deliberately: it is the only clause that touches Resources, so a banner refused
            // for a content reason never pays for a sprite load.
            if (resolver != null && !resolver.ArtResolves(entry))
            {
                reason = "no usable banner art (neither artUrl nor artSprite resolves)";
                return false;
            }

            return true;
        }

        private static void EnsureLoaded()
        {
            if (_entries != null && !s_refreshPending) return;
            if (s_refreshPending) ApplyPendingRefresh();
            if (_entries == null) LoadFromCsv();
        }

        private static void LoadFromCsv()
        {
            EnsureSubscribed();

            var asset = Resources.Load<TextAsset>("Data/gacha_banners");
            if (asset == null)
            {
                Debug.LogError("[GachaBannerCatalog] gacha_banners.csv not found in Resources/Data/.");
                _entries = new List<GachaBannerEntry>();
                return;
            }

            // RequireReady logs an ERROR when a ContentService exists but has not installed the
            // store yet, and stays QUIET when there is no ContentService at all — a lab or EditMode
            // scene running bundled, which is correct. Either way the parse below proceeds.
            ContentCatalog overlay = ContentCatalogStore.RequireReady(nameof(GachaBannerCatalog))
                ? ContentCatalogStore.Catalog(ContentCatalogs.GachaBanners)
                : null;

            _entries = ParseCsv(asset.text, overlay);

            int overlaid = 0;
            if (overlay != null)
                foreach (var e in _entries)
                    if (overlay.ById.ContainsKey(e.BannerId)) overlaid++;

            Debug.Log($"[GachaBannerCatalog] Loaded {_entries.Count} banner entries" +
                      (overlay != null
                          ? $" ({overlaid} overlaid from the published catalog v{overlay.Version})."
                          : " (bundled only)."));
        }

        /// <summary>Bundled-only parse. Kept as its own overload because GachaStage2Tests drives
        /// it, and because a caller with no overlay should not have to say so with a null.</summary>
        internal static List<GachaBannerEntry> ParseCsv(string csvText) => ParseCsv(csvText, null);

        /// <summary>
        /// Testable seam: parse a CSV string into entries, with an optional published overlay
        /// merged on top.
        ///
        /// <para>
        /// HEADER-INDEXED AND QUOTE-AWARE since `gacha_admin_catalogs` (§3). Fields are read BY
        /// COLUMN NAME off the header line, not by position, and unknown columns are ignored —
        /// which is what let the thirteen admin columns pass through this parser for a whole task
        /// before anything read them.
        /// </para>
        /// <para>
        /// THE OVERLAY IS A SPARSE PATCH, field-by-field through <see cref="ContentFields"/>: a
        /// published row that names only <c>costX1</c> must not blank <c>artSprite</c> by omission.
        /// An overlay id the bundled CSV has never carried is APPENDED. A row the operator
        /// deactivated (<c>is_active=false</c>) is DROPPED — nobody owns a banner, so unlike a club
        /// there is nothing for a deactivated row to keep rendering.
        /// </para>
        /// <para>
        /// A row is SKIPPED when its bannerId is blank, or when a BUNDLED line carries fewer fields
        /// than the header (a truncated row is malformed). A column the HEADER does not name
        /// defaults to empty (I4) rather than dropping the row: a narrower published header must
        /// not blank the catalog. If endUtc is unparseable the entry defaults to DateTime.MaxValue.
        /// </para>
        /// </summary>
        internal static List<GachaBannerEntry> ParseCsv(string csvText, ContentCatalog overlay)
        {
            var result = new List<GachaBannerEntry>();
            var lines  = (csvText ?? string.Empty).Split('\n');
            var seen   = new HashSet<string>(StringComparer.Ordinal);

            if (lines.Length >= 2)
            {
                var header = ParseCsvLine(lines[0].Trim());
                var index  = new Dictionary<string, int>(StringComparer.Ordinal);
                for (int c = 0; c < header.Count; c++) index[header[c].Trim()] = c;

                for (int i = 1; i < lines.Length; i++)
                {
                    var line = lines[i].Trim();
                    if (string.IsNullOrEmpty(line)) continue;

                    var cols = ParseCsvLine(line);
                    if (cols.Count < header.Count) continue; // truncated row — skip without throwing

                    // The id has to come out of the CSV before the overlay can be looked up, so the
                    // bundled row is read first and patched second — ClubCsvParser's ordering.
                    var bundledFields = ContentFields.Csv(cols, index);
                    string bannerId = bundledFields.Get("bannerId");
                    if (string.IsNullOrEmpty(bannerId)) continue; // a row with no id is not a banner

                    seen.Add(bannerId);

                    ContentRow patch = null;
                    overlay?.ById.TryGetValue(bannerId, out patch);

                    if (patch != null && !patch.IsActive) continue;   // I6 — deactivated, and unowned

                    var fields = patch == null ? bundledFields : ContentFields.Csv(cols, index, patch);
                    var entry  = ReadRow(fields, i);
                    if (entry != null) result.Add(entry);
                }
            }

            // Overlay rows the bundled CSV has never carried — a banner authored entirely in the
            // admin, which is the whole point of the catalog.
            if (overlay != null)
            {
                foreach (var row in overlay.Rows)
                {
                    if (seen.Contains(row.Id)) continue;
                    if (!row.IsActive) continue;

                    var entry = ReadRow(ContentFields.OverlayOnly(row), -1);
                    if (entry == null) continue;
                    if (string.IsNullOrEmpty(entry.BannerId)) entry.BannerId = row.Id;
                    result.Add(entry);
                }
            }

            return result;
        }

        /// <summary>One entry out of whatever <see cref="ContentFields"/> stands in front of it —
        /// a bundled row, a bundled row patched by an overlay, or an overlay row on its own. The
        /// column names and defaults are declared ONCE, here.</summary>
        private static GachaBannerEntry ReadRow(ContentFields f, int lineNumber)
        {
            string bannerId = f.Get("bannerId");

            var entry = new GachaBannerEntry
            {
                BannerId  = bannerId,
                NameKey   = f.Get("nameKey"),
                ArtSprite = f.Get("artSprite"),
                CostX1    = f.GetInt("costX1"),
                CostX10   = f.GetInt("costX10"),
                RulesUrl  = f.Get("rulesUrl"),
                SortOrder = f.GetInt("sortOrder"),
                Active    = string.Equals(f.Get("active"), "true", StringComparison.OrdinalIgnoreCase),

                PoolId        = f.Get("poolId"),
                TicketType    = f.GetInt("ticketType"),
                PityThreshold = f.GetInt("pityThreshold"),
                ArtUrl        = f.Get("artUrl"),
                NameEn        = f.Get("nameEn"),
                NameJa        = f.Get("nameJa"),
            };

            // pityMinRarity only means anything alongside a threshold; a blank one with a threshold
            // set is an authoring error the admin validator refuses, and Common here is the
            // harmless reading of it (a "guarantee" of the commonest tier forces nothing).
            entry.PityMinRarity = GachaCsvMerge.ParseRarity(f.Get("pityMinRarity"));

            // BLANK IS "NO GUARANTEE", NOT "GUARANTEE COMMON" — the distinction the card's second
            // line is bound to, and the reason this is a bool plus a rarity rather than a rarity
            // with a sentinel.
            string guarantee = f.Get("guaranteeMinRarityX10");
            entry.HasGuaranteeX10 = !string.IsNullOrWhiteSpace(guarantee);
            if (entry.HasGuaranteeX10)
                entry.GuaranteeMinRarityX10 = GachaCsvMerge.ParseRarity(guarantee);

            string cap = f.Get("maxPullsPerPlayer");
            entry.MaxPullsPerPlayer = int.TryParse(cap, out int capValue) && capValue > 0
                ? (int?)capValue
                : null;

            string featured = f.Get("featuredRefIds");
            entry.FeaturedRefIds = string.IsNullOrWhiteSpace(featured)
                ? Array.Empty<string>()
                : featured.Split(';');

            entry.StartUtc = ParseBound(f.Get("startUtc"), DateTime.MinValue, bannerId, "startUtc", lineNumber);
            entry.EndUtc   = ParseBound(f.Get("endUtc"),   DateTime.MaxValue, bannerId, "endUtc",   lineNumber);

            return entry;
        }

        /// <summary>Absent means UNBOUNDED (<paramref name="whenAbsent"/>); unparseable is loud and
        /// also unbounded, which is how this catalog has always read a bad endUtc.</summary>
        private static DateTime ParseBound(string raw, DateTime whenAbsent, string bannerId,
                                           string column, int lineNumber)
        {
            if (string.IsNullOrWhiteSpace(raw)) return whenAbsent;

            if (DateTime.TryParse(raw,
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.AssumeUniversal,
                    out var parsed))
                return parsed.ToUniversalTime();

            Debug.LogWarning($"[GachaBannerCatalog] {(lineNumber >= 0 ? $"Row {lineNumber}" : bannerId)}: " +
                             $"could not parse {column} '{raw}'; treating it as unbounded.");
            return whenAbsent;
        }

        /// <summary>
        /// Splits one CSV line on commas, honouring double-quoted fields so a field may itself
        /// contain commas. A literal quote inside a quoted field is <c>""</c>.
        ///
        /// Same logic as <c>ModesDatabaseCSV.ParseCsvLine</c> / <c>GeneralShopCatalog.ParseCsvLine</c>
        /// / <c>TournamentCsvLoader</c>. Copied rather than shared because there is still no public
        /// splitter to share: <c>Golfin.Content.ContentFields</c> reads an ALREADY-SPLIT field list
        /// and the other copies are private to their loaders.
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

        // ── 5b: a banner published mid-session ────────────────────────────────
        //
        // I5 says a fetched catalog takes effect at the NEXT launch, and for clubs and modes it
        // still does. The gacha is the carve-out (plan §9 decision 5b) because nothing holds a
        // banner across a frame — see ContentService.LiveSwappable. The wiring is deliberately
        // PASSIVE: the refresh only arms a flag, and the swap happens on the next Reload(), which
        // GachaCarouselController.OnEnable already calls. So a banner published while the player is
        // mid-pull cannot change the card under their thumb; it appears the next time they open the
        // Rewards Center.

        private static bool s_subscribed;
        private static bool s_refreshPending;

        /// <summary>
        /// Subscribe BEFORE ANY SCENE LOADS, not on first use.
        ///
        /// <para>
        /// ⚠️ This is the whole 5b path, and a lazy subscription silently disabled it. The boot
        /// refresh is started by <c>ContentService.Awake</c> (order -900) and raises
        /// <c>OnCacheRefreshed</c> when it lands — typically a second or two into the session, and
        /// always LONG before the player opens the Rewards Center. A catalog that only subscribed
        /// on its first read therefore attached AFTER the event it exists to hear, missed it, and
        /// went on serving the cache installed at boot. Measured on prod: the disk cache held
        /// <c>gacha_banners</c> v4 with <c>costX1: 60</c> while the store still held v3, and the
        /// card kept showing 50 — the exact failure the re-apply was written to prevent.
        /// </para>
        /// <para>
        /// <c>BeforeSceneLoad</c> runs ahead of every <c>Awake</c>, so the listener is in place
        /// before the fetch can possibly complete. It only ARMS a flag; the swap still happens on
        /// the next <see cref="Reload"/>, so nothing changes under a player mid-pull.
        /// </para>
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void SubscribeAtBoot() => EnsureSubscribed();

        private static void EnsureSubscribed()
        {
            if (s_subscribed) return;
            s_subscribed = true;
            ContentService.OnCacheRefreshed += () => s_refreshPending = true;
        }

        private static void ApplyPendingRefresh()
        {
            s_refreshPending = false;

            bool any = false;
            any |= ContentService.TryReinstallFromCache(ContentCatalogs.GachaBanners);
            any |= ContentService.TryReinstallFromCache(ContentCatalogs.GachaRates);
            any |= ContentService.TryReinstallFromCache(ContentCatalogs.GachaPools);
            any |= ContentService.TryReinstallFromCache(ContentCatalogs.TicketTypes);

            if (!any) return;

            // All four re-read together: a rate table that moved without its pool would withhold
            // banners for a reason that no longer exists.
            _entries = null;
            GachaRatesCatalog.Reload();
            GachaPoolCatalog.Reload();
            TicketTypeCatalog.Reload();

            Debug.Log("[GachaBannerCatalog] A content refresh landed this session — the four gacha " +
                      "catalogs were re-installed from cache and the banner list will be rebuilt.");
        }

        /// <summary>
        /// Test / hot-reload hook: forces re-read on next access, and applies a mid-session content
        /// refresh if one landed (5b). Called by <c>GachaCarouselController.OnEnable</c>.
        /// </summary>
        public static void Reload()
        {
            EnsureSubscribed();
            if (s_refreshPending) ApplyPendingRefresh();
            _entries = null;
        }

        // ── The shipping resolver ─────────────────────────────────────────────

        /// <summary>
        /// <see cref="IRefResolver"/> over this build's real databases.
        ///
        /// <para>
        /// A NULL DATABASE SINGLETON IS NOT A FAILURE, exactly as in
        /// <c>GeneralShopCatalog.UnrenderableReason</c>: in EditMode, or on a lazy first access
        /// before the scene singletons exist, there is nothing to resolve against and treating that
        /// as unresolvable would withhold every banner in the catalog. It answers TRUE and lets the
        /// server have the final word.
        /// </para>
        /// </summary>
        private sealed class LiveResolver : IRefResolver
        {
            internal static readonly LiveResolver Instance = new LiveResolver();

            public bool Resolves(string kind, string refId)
            {
                if (string.IsNullOrEmpty(refId)) return false;

                switch (kind)
                {
                    case "club":
                    {
                        var db = Golfin.Inventory.ClubDatabaseCSV.Instance;
                        if (db == null) return true;                 // no database this load — admit
                        var club = db.GetClub(refId);
                        return club != null && club.isActive;
                    }
                    case "ball":
                    {
                        var db = Golfin.Inventory.BallDatabaseCSV.Instance;
                        if (db == null) return true;
                        var ball = db.GetBall(refId);
                        return ball != null && ball.isActive && ball.renderable;
                    }
                    case "character":
                    {
                        var db = Golfin.Roster.CharacterDatabaseCSV.Instance;
                        if (db == null) return true;
                        var ch = db.GetCharacter(refId);
                        return ch != null && ch.isActive && ch.renderable;
                    }
                    case "item":
                    {
                        var db = Golfin.Inventory.ItemDatabaseCSV.Instance;
                        if (db == null) return true;
                        var item = db.GetItem(refId);
                        return item != null && item.isActive && item.renderable;
                    }
                    case "ticket":
                        return int.TryParse(refId, out int t) && TicketTypeCatalog.Get(t) != null;

                    default:
                        // A kind this build has never heard of. The server may well be able to pay
                        // it; this build cannot RENDER it, so it cannot be the thing that keeps a
                        // rated tier payable.
                        return false;
                }
            }

            public bool TicketTypeExists(int ticketType) => TicketTypeCatalog.Get(ticketType) != null;

            public bool ArtResolves(GachaBannerEntry entry)
                => GachaBannerArt.Resolve(entry) != null;
        }
    }
}
