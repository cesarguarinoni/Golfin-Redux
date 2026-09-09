// Assets/Scripts/UI/Gacha/GachaTicketArt.cs
// polish_regressions_0909 R3 (audit) — the ONE ticket-icon ladder.
//
// WHY THIS FILE EXISTS. Three call sites resolved a ticket icon, byte for byte the same three
// lines each — GachaBannerCard.BindTicketIcon, GachaPrizeCardBinder (the ticket prize) and
// GeneralShopCard. All three carried the SAME two defects, which is what a copied ladder always
// does:
//
//   1. `CatalogArtCache.Cached(type.IconUrl, type.IconUrl)` — the second argument is the BUNDLED
//      url, and passing the row's own url as its own bundled url makes the comparison
//      `url == bundledUrl` true unconditionally. That step returns null on EVERY call, in every
//      build, forever: re-uploaded ticket art could never win over the bundled sprite. It was
//      dead code that looked like a ladder rung.
//   2. The bundled sprite was loaded by NAME with no check that the name is THIS row's own art,
//      which is the R3 shape — a placeholder that resolves silently outranks the real uploaded
//      art. See GachaBannerArt.Resolve for the full post-mortem; ticket_types is one exporter run
//      away from the same state gacha_banners was in.
//
// The banner ladder is the model and this is deliberately its twin: same rung order, same
// own-name gate, same demotion of a foreign sprite to LAST.
#nullable enable
using Golfin.CatalogArt;
using UnityEngine;

namespace GolfinRedux.UI.Gacha
{
    /// <summary>The ticket-icon art ladder, shared by every surface that draws a ticket.</summary>
    public static class GachaTicketArt
    {
        /// <summary>Resources path prefix for bundled ticket art.</summary>
        public const string BundledPath = "Art/Gacha/Tickets/";

        /// <summary>
        /// The bundled sprite name a ticket type's OWN icon is filed under —
        /// <c>Ticket_{Pascal(key)}</c>.
        ///
        /// <para>The KEY, not the id: <c>ticket_types.csv</c>'s id column is a bare enum ordinal
        /// ("0", "1") and would derive <c>Ticket_0</c>. Same rule ContentArtFetcher writes and
        /// ContentArtValidator checks — ASSET_NAMING_CONVENTION.md §5.</para>
        /// </summary>
        public static string ConventionName(string? key)
            => string.IsNullOrWhiteSpace(key) ? string.Empty : "Ticket_" + Pascal(key!);

        /// <summary>Whether <c>iconSprite</c> names the art THIS row would bundle.</summary>
        public static bool SpriteIsOwn(TicketTypeEntry? entry)
            => entry != null
               && !string.IsNullOrWhiteSpace(entry.IconSprite)
               && string.Equals(entry.IconSprite.Trim(), ConventionName(entry.Key),
                                System.StringComparison.Ordinal);

        /// <summary>
        /// The icon to draw for <paramref name="entry"/>, or null when this build has none — in
        /// which case the caller KEEPS ITS AUTHORED ICON rather than blanking the row (a missing
        /// Gold ticket icon is an art gap, not a reason to render a currency-less price).
        /// </summary>
        public static Sprite? Resolve(TicketTypeEntry? entry)
        {
            if (entry == null) return null;

            string bundledUrl = BundledIconUrl(entry.Id, entry.IconUrl);
            bool   ownSprite  = SpriteIsOwn(entry);

            Sprite? art = CatalogArtCache.Cached(entry.IconUrl, bundledUrl);   // 1 — re-uploaded
            if (art != null) return art;

            if (ownSprite)
            {
                art = LoadBundled(entry.IconSprite);                            // 2 — this row's own
                if (art != null) return art;
            }

            art = CatalogArtCache.Cached(entry.IconUrl);                        // 3 — url art
            if (art != null) return art;

            // 4 — a foreign/placeholder sprite, LAST and only as a stand-in.
            return ownSprite ? null : LoadBundled(entry.IconSprite);
        }

        private static Sprite? LoadBundled(string? spriteName)
            => string.IsNullOrWhiteSpace(spriteName)
                ? null
                : Resources.Load<Sprite>(BundledPath + spriteName!.Trim());

        // The BUNDLED csv's url for this id, read back the same way GachaBannerArt does and for
        // the same reason: the entry is the MERGED row, so it no longer knows what the shipped
        // file said, and step 1's whole question is "did the overlay change the url since this
        // build". Unity caches the TextAsset, so the re-read costs a dictionary build once.
        private static System.Collections.Generic.Dictionary<int, string>? _bundledUrls;

        private static string BundledIconUrl(int id, string fallback)
        {
            if (_bundledUrls == null)
            {
                var asset = Resources.Load<TextAsset>("Data/ticket_types");
                if (asset == null) return fallback;

                _bundledUrls = new System.Collections.Generic.Dictionary<int, string>();
                // The BUNDLED-ONLY parse (overlay: null) — the whole question is what the SHIPPED
                // csv said, so feeding the overlay in would compare the row against itself.
                foreach (var e in TicketTypeCatalog.Parse(asset.text, null))
                    _bundledUrls[e.Id] = e.IconUrl ?? string.Empty;
            }
            return _bundledUrls.TryGetValue(id, out string? url) ? url : fallback;
        }

        /// <summary>Test / hot-reload hook.</summary>
        public static void Reload() => _bundledUrls = null;

        /// <summary>`gold` → `Gold`. Non-alphanumerics are separators. Byte-identical to
        /// GachaBannerArt.Pascal and ContentArtFetcher.Pascal.</summary>
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
    }
}
