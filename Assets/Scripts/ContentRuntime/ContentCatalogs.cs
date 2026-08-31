// ─────────────────────────────────────────────────────────────────────────────
// ContentRuntime — ContentCatalogs
//
// The catalog names, in one place. Phase 1 hard-coded "texts" in
// RemoteContentSource; Phase 2 read seven, progress_server_side (2026-08-28)
// added an eighth, game_modes_admin a ninth and gacha_client_real_pull
// (2026-08-31) four more, and a typo in any one of
// them is INVISIBLE — an unknown catalog name is ignored server-side (not a
// 400), so a misspelled "charcters" simply comes back absent and the game runs
// bundled forever with no error anywhere.
//
// Verified against the live endpoint 2026-08-26:
//   GET /api/v1/content?since=…&build=0&catalogs=nosuchcatalog
//     → 200 {"data":{…,"catalogs":{}}}          ← silent, not an error
//
// Hence: every name here is spelled once, and ContentCatalogNamesMatchExporter
// (Golfin.Content.Tests) pins them against Tools/content/catalogs.py, which is
// the seeder's and the exporter's own list.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;
using System.Collections.Generic;

namespace Golfin.Content
{
    /// <summary>
    /// Catalog names as the server spells them (<c>content_catalogs.name</c>).
    /// </summary>
    public static class ContentCatalogs
    {
        public const string Texts       = "texts";
        public const string Clubs       = "clubs";
        public const string Characters  = "characters";
        public const string Items       = "items";
        public const string Bags        = "bags";
        public const string Balls       = "balls";
        public const string ShopCatalog = "shop_catalog";

        /// <summary>
        /// The level-up cost table (progress_server_side §2). Unlike the other seven, this catalog is
        /// also read by the SERVER: <c>golfin_level_up()</c> sums <c>cost_r</c> over its published
        /// rows to price a level-up. The client overlay exists so the modal PREVIEWS the same number
        /// the server will charge — when it does not, the server answers <c>cost_changed</c> and the
        /// modal re-prices, which is a correct but avoidable extra round trip.
        /// </summary>
        public const string LevelUpCosts = "level_up_costs";

        /// <summary>
        /// The game-mode table (game_modes_admin §2). The SECOND catalog the server also reads:
        /// publishing it mirrors <c>entryFee</c>/<c>locked</c> into <c>golfin_mode_fees</c>, and
        /// <c>POST /points/spend</c> refuses a <c>mode_entry_fee:&lt;id&gt;</c> debit that does not
        /// match. So the overlay is not cosmetic — it is what keeps the fee on the card and the fee
        /// the player is charged the same number. When they differ (a publish landed mid-session)
        /// the server answers <c>fee_changed</c> and the card re-prices, which is correct but is one
        /// avoidable round trip.
        /// </summary>
        public const string Modes = "modes";

        // ── missions_v1 §A2 — the seven mission catalogs ──────────────────────
        //
        // `Missions` and `MissionTiers` are the two the SERVER also reads: publishing
        // either mirrors it into `golfin_mission_rewards` / `golfin_mission_tier_bonus`,
        // and `golfin_mission_claim()` pays from the mirror. The other five are
        // components a mission is composed from — client and generator data with no
        // server mirror, but not inert: the admin recomputes every mission's
        // difficultyScore from `MissionGoalWeights` on publish.
        public const string Missions            = "missions";
        public const string MissionStartAreas   = "mission_start_areas";
        public const string MissionWindPresets  = "mission_wind_presets";
        public const string MissionLoadouts     = "mission_loadouts";
        public const string MissionGoalWeights  = "mission_goal_weights";
        public const string MissionTiers        = "mission_tiers";
        public const string DailyMissionWeights = "daily_mission_weights";

        // ── gacha_client_real_pull §2 — the four gacha catalogs ───────────────
        //
        // `GachaBanners`, `GachaRates` and `GachaPools` are read by the SERVER as well
        // (`golfin_gacha_pull()` prices the banner, rolls the tier and picks the entry from the
        // PUBLISHED rows). The client overlay is therefore not cosmetic: it is what keeps the
        // number on the banner and the number the player is charged the same one, and what keeps
        // the client's withhold rule (SPEC §3.1) agreeing with the server's step 8. When they
        // differ the server answers `cost_changed` / `not_available` and the card re-renders —
        // correct, but one avoidable round trip.
        //
        // `TicketTypes` is client-and-admin data with no server mirror; the pull reads
        // `ticket_types` only to confirm the banner's type is published and active.
        //
        // ⚠️ These four ARE in `All` — unlike the seven `Mission*` names above, which are declared
        // but never requested (missions_v1 fetches nothing for them, so `MissionCatalog` reads an
        // empty store and runs bundled). That is pre-existing and is NOT changed here.
        public const string GachaBanners = "gacha_banners";
        public const string GachaRates   = "gacha_rates";
        public const string GachaPools   = "gacha_pools";
        public const string TicketTypes  = "ticket_types";

        /// <summary>
        /// The catalogs whose ROWS this build overlays onto a bundled CSV — everything except
        /// <see cref="Texts"/>, which merges into <c>LocalizationManager</c> instead and therefore
        /// has its own applier.
        /// </summary>
        public static readonly string[] Data =
        {
            Clubs, Characters, Items, Bags, Balls, ShopCatalog, LevelUpCosts, Modes,
            GachaBanners, GachaRates, GachaPools, TicketTypes,
        };

        /// <summary>Every catalog this build asks the server for, texts included.</summary>
        public static readonly string[] All =
        {
            Texts, Clubs, Characters, Items, Bags, Balls, ShopCatalog, LevelUpCosts, Modes,
            GachaBanners, GachaRates, GachaPools, TicketTypes,
        };

        /// <summary>
        /// The <c>catalogs=</c> query value:
        /// "texts,clubs,characters,items,bags,balls,shop_catalog,level_up_costs,modes,
        /// gacha_banners,gacha_rates,gacha_pools,ticket_types".
        /// Narrowing the request matters — an unnarrowed one returns every catalog the server holds,
        /// and this build can only apply the thirteen it knows.
        /// </summary>
        public static string RequestList => string.Join(",", All);

        /// <summary>True when <paramref name="name"/> is one of the thirteen. Case-insensitive.</summary>
        public static bool IsKnown(string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            foreach (string c in All)
                if (string.Equals(c, name!.Trim(), StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        /// <summary>A dictionary keyed the way catalog names compare — ordinal, case-insensitive.</summary>
        public static Dictionary<string, T> NewMap<T>() =>
            new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);
    }
}
