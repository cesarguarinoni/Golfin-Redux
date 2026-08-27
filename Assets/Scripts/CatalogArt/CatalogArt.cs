// ─────────────────────────────────────────────────────────────────────────────
// CatalogArt — CatalogArt
// Thin helper: one static method the four loaders call to probe the in-memory
// cache. Lives here (Assembly-CSharp) for the same reason CatalogArtPolicy does:
// TournamentArtService is in Assembly-CSharp and Golfin.Content cannot reference it.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using Golfin.Tournaments;
using UnityEngine;

namespace Golfin.CatalogArt
{
    /// <summary>
    /// Cache probe for catalog art URLs. The loaders call this during row parsing;
    /// it is deliberately read-only — no download is ever started here.
    ///
    /// Named <c>CatalogArtCache</c> rather than <c>CatalogArt</c> to avoid a C# name
    /// resolution ambiguity: a class with the same name as the last segment of its own
    /// namespace shadows itself when the namespace is imported with a <c>using</c> directive.
    /// </summary>
    public static class CatalogArtCache
    {
        /// <summary>
        /// Step 1 of the resolution ladder (SPEC §2, revised 2026-08-27):
        /// returns the cached sprite for <paramref name="url"/> ONLY when
        /// <paramref name="url"/> differs from <paramref name="bundledUrl"/>, signalling
        /// that the overlay URL has changed since this build was cut — i.e. art was
        /// re-uploaded after the build.
        ///
        /// <para>
        /// Modelled on <c>ContentSpriteGuard.SpriteRef.Changed</c> — the same
        /// "overlay named something other than what the bundled CSV named" comparison,
        /// applied to URLs instead of sprite names. All four loaders already hold both
        /// the bundled row and the overlaid one at the point of resolution (that is what
        /// feeds the sprite guard), so both URLs are in hand with no extra CSV read.
        /// </para>
        ///
        /// <para>
        /// Returns null when the URLs agree (bundled art wins at step 2) or when the
        /// URL is not in the in-memory cache. Empty/null <paramref name="url"/>
        /// returns null unconditionally.
        /// </para>
        /// </summary>
        public static Sprite? Cached(string? url, string? bundledUrl)
        {
            if (string.IsNullOrEmpty(url)) return null;
            // URLs agree → the overlay has NOT re-uploaded art since this build.
            // Let step 2 (bundled sprite by name) take priority.
            if (url == bundledUrl) return null;
            TournamentArtService.CatalogArt.TryGet(url, out Sprite? sprite);
            return sprite;
        }

        /// <summary>
        /// Step 3 of the resolution ladder (SPEC §2, revised 2026-08-27):
        /// returns the cached sprite for <paramref name="url"/> unconditionally
        /// (URL-unchanged-since-build path, or new row from admin with no bundled art).
        /// Never starts a download. Empty/null url returns null.
        /// </summary>
        public static Sprite? Cached(string? url)
        {
            if (string.IsNullOrEmpty(url)) return null;
            TournamentArtService.CatalogArt.TryGet(url, out Sprite? sprite);
            return sprite;
        }
    }
}
