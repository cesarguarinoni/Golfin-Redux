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
        /// The bundled sprite name a row's OWN art is filed under —
        /// <c>GachaBanner_{Pascal(bannerId minus "banner_")}</c>.
        ///
        /// <para>
        /// THE ONE DEFINITION OF THAT CONVENTION. <c>ContentArtFetcher</c> WRITES it,
        /// <c>ContentArtValidator</c> CHECKS it, and <see cref="Resolve"/> below TRUSTS it — three
        /// tools that must agree on one string, so they call this rather than each spelling it.
        /// Documented in ASSET_NAMING_CONVENTION.md §5.
        /// </para>
        /// </summary>
        public static string ConventionName(string? bannerId)
        {
            if (string.IsNullOrWhiteSpace(bannerId)) return string.Empty;
            string bare = bannerId!.StartsWith("banner_", System.StringComparison.Ordinal)
                ? bannerId.Substring(7)
                : bannerId;
            return "GachaBanner_" + Pascal(bare);
        }

        /// <summary>
        /// Whether <c>artSprite</c> names the art THIS row would bundle, as opposed to some other
        /// row's — which in practice means the shared placeholder every un-bundled row points at.
        /// </summary>
        public static bool SpriteIsOwn(GachaBannerEntry? entry)
            => entry != null
               && !string.IsNullOrWhiteSpace(entry.ArtSprite)
               && string.Equals(entry.ArtSprite!.Trim(), ConventionName(entry.BannerId),
                                System.StringComparison.Ordinal);

        /// <summary>
        /// The sprite to draw for <paramref name="entry"/>, or null when this build has none —
        /// which is what withholds the banner (§3.1).
        ///
        /// <para>
        /// ⚠️ STEP 2 IS CONDITIONAL, and that condition is polish_regressions_0909 R3. The ladder
        /// used to assume "artSprite is this row's own bundled art". That assumption is violated
        /// by exactly the state an un-bundled catalog is always in: a row carrying a real
        /// <c>artUrl</c> whose sprite cell still names the SHARED PLACEHOLDER. Once the exporter
        /// bakes the published URL into the bundled CSV (which is its job — it happened at
        /// c5558a400 on 2026-09-02), <c>url == bundledUrl</c>, so step 1 returns null by design,
        /// step 2 loads the placeholder, it RESOLVES, and step 3 never runs. The uploaded art
        /// could not appear on any launch, ever — not "one launch late". Cesar: "it simply does
        /// not show".
        /// </para>
        /// <para>
        /// So the bundled sprite only wins at step 2 when it is the row's OWN name. A placeholder
        /// is demoted to step 4: it may still stand in while the URL art is downloading — which
        /// keeps the banner VISIBLE rather than withheld, and
        /// <c>GachaCarouselController.OnCatalogArtCached</c> re-binds the card the moment the
        /// bytes land — but it can never be the reason the real art is not drawn.
        /// </para>
        /// </summary>
        public static Sprite? Resolve(GachaBannerEntry? entry)
        {
            if (entry == null) return null;

            // The bundled row's URL, when the catalog still carries one for this id. An APPENDED
            // overlay row has no bundled counterpart, so the comparison falls through to the
            // entry's own URL and step 1 is skipped — see the header.
            string bundledUrl = BundledArtUrl(entry.BannerId, entry.ArtUrl);
            bool   ownSprite  = SpriteIsOwn(entry);

            Sprite? art = CatalogArtCache.Cached(entry.ArtUrl, bundledUrl);   // 1 — re-uploaded
            if (art != null) return Answered(entry, 1, "url re-uploaded since this build", art);

            if (ownSprite)
            {
                art = LoadBundled(entry.ArtSprite);                           // 2 — this build's own
                if (art != null) return Answered(entry, 2, "bundled, this row's own art", art);
            }

            art = CatalogArtCache.Cached(entry.ArtUrl);                       // 3 — URL, unchanged/new
            if (art != null) return Answered(entry, 3, "url art from the cache", art);

            if (!ownSprite)
            {
                // 4 — the placeholder, LAST. Reached only when the row's art has not been bundled
                // AND its url is not cached yet. Temporary by construction: the carousel re-binds
                // when the download lands.
                art = LoadBundled(entry.ArtSprite);
                if (art != null)
                    return Answered(entry, 4, "PLACEHOLDER — '" + entry.ArtSprite +
                                              "' is not this row's own art; run GOLFIN/Content/Fetch URL Art", art);
            }

            return Answered(entry, 0, "nothing resolved — the banner is WITHHELD (§3.1)", null);
        }

        /// <summary>
        /// Log which rung answered, ONCE per banner per <see cref="Reload"/>. Once, because Resolve
        /// runs from both the withhold check and every card bind — a line per call would be a line
        /// per carousel rebuild.
        /// </summary>
        private static Sprite? Answered(GachaBannerEntry entry, int step, string why, Sprite? art)
        {
            if (_logged.Add(entry.BannerId))
            {
                string msg = $"[GachaBannerArt] '{entry.BannerId}' art from step {step} — {why}. " +
                             $"(artSprite='{entry.ArtSprite}', own='{ConventionName(entry.BannerId)}', " +
                             $"hasUrl={!string.IsNullOrWhiteSpace(entry.ArtUrl)})";
                if (step == 4)      Debug.LogWarning(msg);
                else if (step == 0) Debug.LogWarning(msg);
                else                Debug.Log(msg);
            }
            return art;
        }

        private static readonly System.Collections.Generic.HashSet<string> _logged =
            new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal);

        /// <summary>`standard_club1` → `StandardClub1`. Non-alphanumerics are separators.</summary>
        private static string Pascal(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            var sb = new System.Text.StringBuilder(s.Length);
            bool boundary = true;
            foreach (char c in s)
            {
                if (!char.IsLetterOrDigit(c)) { boundary = true; continue; }
                sb.Append(boundary ? char.ToUpperInvariant(c) : char.ToLowerInvariant(c));
                boundary = false;
            }
            return sb.ToString();
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
        public static void Reload()
        {
            _bundledUrls = null;
            _logged.Clear();
        }
    }
}
