// Assets/Scripts/UI/Gacha/GachaBannerArt.cs
// gacha_client_real_pull §3 — the ONE banner-art ladder.
//
// It exists so the withhold rule and the card cannot disagree. §3.1 says a banner whose art does
// not resolve is WITHHELD; §3 says the card draws the same art. If those were two ladders, a
// change to one would eventually admit a banner the other renders blank — which is the exact
// failure ("never the Placeholder sprite, never a blank card") the withhold rule was written for.
//
// The ladder is ClubDatabaseCSV.cs:235's, minus the club-only Placeholder step:
//   1. the admin URL, when it DIFFERS from the bundled one   → art re-uploaded since this build
//   2. the bundled Resources sprite by name                  → what this build shipped
//   3. the admin URL unconditionally                         → a row with no bundled counterpart
//   4. null                                                  → WITHHELD (§3.1)
//
// ⚠️ Step 1's bundled-URL comparison is load-bearing and is the content_art_bundling scar: a
// bundled banner carrying a URL must compare that URL against ITSELF, so step 1 returns null and
// the build's own sprite wins at step 2. A banner with no bundled row has no bundled URL, so the
// comparison is against empty, differs, and the download is served — which is correct there.
#nullable enable
using Golfin.CatalogArt;
using UnityEngine;

namespace GolfinRedux.UI.Gacha
{
    /// <summary>The banner art ladder, shared by the withhold rule and the card.</summary>
    public static class GachaBannerArt
    {
        /// <summary>Resources path prefix for bundled banner art.</summary>
        public const string BundledPath = "Art/Gacha/Banners/";

        /// <summary>
        /// The sprite to draw for <paramref name="entry"/>, or null when this build has none —
        /// which is what withholds the banner (§3.1). Never returns a placeholder.
        /// </summary>
        public static Sprite? Resolve(GachaBannerEntry? entry)
        {
            if (entry == null) return null;

            // The bundled row's URL, when the catalog still carries one for this id. An APPENDED
            // overlay row has no bundled counterpart, so the comparison falls through to the
            // entry's own URL and step 1 is skipped — see the header.
            string bundledUrl = BundledArtUrl(entry.BannerId, entry.ArtUrl);

            return CatalogArtCache.Cached(entry.ArtUrl, bundledUrl)          // 1 — re-uploaded
                ?? LoadBundled(entry.ArtSprite)                              // 2 — this build's own
                ?? CatalogArtCache.Cached(entry.ArtUrl);                     // 3 — URL, unchanged/new
        }

        private static Sprite? LoadBundled(string? spriteName)
            => string.IsNullOrWhiteSpace(spriteName)
                ? null
                : Resources.Load<Sprite>(BundledPath + spriteName!.Trim());

        // The bundled CSV is re-read here rather than threaded through GachaBannerEntry: the entry
        // is the MERGED row, so it no longer knows what the shipped file said. Reading the one
        // column back costs a Resources.Load of an already-loaded TextAsset (Unity caches it) and
        // keeps the entry a plain data class.
        private static string BundledArtUrl(string bannerId, string fallback)
        {
            var bundled = BundledUrls;
            return bundled != null && bundled.TryGetValue(bannerId, out string? url) ? (url ?? string.Empty)
                                                                                     : fallback;
        }

        private static System.Collections.Generic.Dictionary<string, string?>? _bundledUrls;

        private static System.Collections.Generic.Dictionary<string, string?>? BundledUrls
        {
            get
            {
                if (_bundledUrls != null) return _bundledUrls;

                var asset = Resources.Load<TextAsset>("Data/gacha_banners");
                if (asset == null) return null;

                _bundledUrls = new System.Collections.Generic.Dictionary<string, string?>(System.StringComparer.Ordinal);
                foreach (var e in GachaBannerCatalog.ParseCsv(asset.text))   // bundled-only overload
                    _bundledUrls[e.BannerId] = e.ArtUrl;
                return _bundledUrls;
            }
        }

        /// <summary>Test / hot-reload hook.</summary>
        public static void Reload() => _bundledUrls = null;
    }
}
