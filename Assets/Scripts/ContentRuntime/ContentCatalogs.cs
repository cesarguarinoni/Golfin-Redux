// ─────────────────────────────────────────────────────────────────────────────
// ContentRuntime — ContentCatalogs
//
// The catalog names, in one place. Phase 1 hard-coded "texts" in
// RemoteContentSource; Phase 2 reads seven catalogs, and a typo in any one of
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
        /// The catalogs whose ROWS this build overlays onto a bundled CSV — everything except
        /// <see cref="Texts"/>, which merges into <c>LocalizationManager</c> instead and therefore
        /// has its own applier.
        /// </summary>
        public static readonly string[] Data =
        {
            Clubs, Characters, Items, Bags, Balls, ShopCatalog,
        };

        /// <summary>Every catalog this build asks the server for, texts included.</summary>
        public static readonly string[] All =
        {
            Texts, Clubs, Characters, Items, Bags, Balls, ShopCatalog,
        };

        /// <summary>
        /// The <c>catalogs=</c> query value: "texts,clubs,characters,items,bags,balls,shop_catalog".
        /// Narrowing the request matters — an unnarrowed one returns every catalog the server holds,
        /// and this build can only apply the seven it knows.
        /// </summary>
        public static string RequestList => string.Join(",", All);

        /// <summary>True when <paramref name="name"/> is one of the seven. Case-insensitive.</summary>
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
