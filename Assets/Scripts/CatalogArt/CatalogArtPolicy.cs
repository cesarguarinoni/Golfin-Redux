// ─────────────────────────────────────────────────────────────────────────────
// CatalogArt — CatalogArtPolicy
// The whole security surface of the catalog art URL feature (SPEC §4.1).
// Pure static functions, no Unity lifecycle, so both are directly unit-testable.
//
// Lives in Assembly-CSharp (no asmdef), NOT inside Assets/Scripts/ContentRuntime/
// (which is Golfin.Content) — that assembly cannot reference TournamentArtPolicy,
// which is also in Assembly-CSharp. This is the same arrangement as BannerPolicy:
// Assets/Scripts/BannersRuntime/ has no runtime asmdef, and the file is beside it
// rather than inside the Golfin.Content tree for exactly this reason. A second
// discoverer of this fact will find the same explanation here (and in BannerPolicy).
//
// ⚠️ These URL columns are free text on a row the client fetches UNATTENDED at boot.
//    The allowlist here is THE CONTROL, not a usability guard — the dashboard's
//    equivalent check is the usability guard. That is also why
//    TournamentArtPolicy.IsAllowedUnder is reused rather than copied: forking that
//    check would fork a security-critical decision and the two copies would drift.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;
using Golfin.Tournaments;

namespace Golfin.CatalogArt
{
    /// <summary>
    /// Decides whether a server-supplied catalog-art URL may be fetched onto a player's device.
    /// </summary>
    public static class CatalogArtPolicy
    {
        /// <summary>
        /// The ONLY prefix the client will download catalog art from — scheme, host AND path.
        ///
        /// ⚠️ <c>portraitUrl</c> / <c>fullUrl</c> / <c>thumbnailUrl</c> / <c>controlUrl</c> are
        /// free-text columns and the client fetches them UNATTENDED at boot. Without this prefix
        /// check they are an arbitrary content channel into every player's device and a way to
        /// harvest every player's IP address. The dashboard runs an equivalent check, but that one
        /// is a usability guard — this constant is the control, and it is the only one that still
        /// holds if a row is written by something other than the dashboard.
        /// </summary>
        public const string AllowedArtPrefix =
            "https://wmszyghwwkaptgqdunel.supabase.co/storage/v1/object/public/catalog-art/";

        /// <summary>
        /// Cache directory name under <c>Application.persistentDataPath</c>. A THIRD directory,
        /// separate from <c>tournament-art</c> and <c>game-banners</c>, so the three 50 MB LRU
        /// budgets cannot evict each other.
        /// </summary>
        public const string CacheDirName = "catalog-art";

        /// <summary>The allowlist parsed once, so the comparison below is Uri-vs-Uri.</summary>
        private static readonly Uri AllowedArtRoot = new Uri(AllowedArtPrefix);

        /// <summary>
        /// True when <paramref name="url"/> resolves — <b>after normalization</b> — strictly inside
        /// the allowlisted <c>catalog-art</c> bucket.
        /// <para>
        /// The check itself is <see cref="TournamentArtPolicy.IsAllowedUnder"/>, shared rather than
        /// copied: it already refuses a non-https scheme, a wrong host, userinfo, a non-default
        /// port, the bucket root itself, and any <c>..</c> / <c>%2e</c> that survived normalization.
        /// Read its doc comment for why a raw <c>StartsWith</c> is exploitable here.
        /// </para>
        /// </summary>
        public static bool IsArtAllowed(string? url) =>
            TournamentArtPolicy.IsAllowedUnder(url, AllowedArtRoot);
    }
}
