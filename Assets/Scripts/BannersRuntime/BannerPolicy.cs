// ─────────────────────────────────────────────────────────────────────────────
// BannersRuntime — BannerPolicy
// The whole security surface of the game_banners feature: which artwork URLs
// may be fetched onto a device, and which tap-through URLs may be opened.
// Pure static functions, no Unity lifecycle, so both are directly unit-testable.
//
// Lives in Assembly-CSharp (no asmdef), NOT in Golfin.Tournaments — that
// assembly is deliberately dependency-light and must never learn a network
// exists. Same arrangement as Assets/Scripts/TournamentsRuntime/.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;
using Golfin.Tournaments;
using GolfinRedux.UI;

namespace Golfin.Banners
{
    /// <summary>
    /// Decides whether a server-supplied banner URL may be fetched onto a player's device
    /// (<see cref="IsArtAllowed"/>) or opened in their browser (<see cref="IsLinkAllowed"/>).
    /// </summary>
    public static class BannerPolicy
    {
        /// <summary>
        /// The ONLY prefix the client will download banner art from — scheme, host AND path.
        ///
        /// ⚠️ <c>image_url_en</c> / <c>image_url_ja</c> are free-text columns and the client fetches
        /// them UNATTENDED at boot. Without this prefix check they are an arbitrary content channel
        /// into every player's device and a way to harvest every player's IP address. The dashboard
        /// runs an equivalent check, but that one is a usability guard — this constant is the
        /// control, and it is the only one that still holds if a row is written by something other
        /// than the dashboard.
        /// </summary>
        public const string AllowedArtPrefix =
            "https://wmszyghwwkaptgqdunel.supabase.co/storage/v1/object/public/game-banners/";

        /// <summary>
        /// Cache directory name under <c>Application.persistentDataPath</c>. Deliberately separate
        /// from <c>tournament-art</c> so the two LRU budgets cannot evict each other.
        /// </summary>
        public const string CacheDirName = "game-banners";

        /// <summary>The allowlist parsed once, so the comparison is Uri-vs-Uri.</summary>
        private static readonly Uri AllowedArtRoot = new Uri(AllowedArtPrefix);

        /// <summary>
        /// True when <paramref name="url"/> resolves — <b>after normalization</b> — strictly inside
        /// the allowlisted <c>game-banners</c> bucket.
        /// <para>
        /// The check itself is <see cref="TournamentArtPolicy.IsAllowedUnder"/>, shared rather than
        /// copied: it already refuses a non-https scheme, a wrong host, userinfo, a non-default
        /// port, the bucket root itself, and any <c>..</c> / <c>%2e</c> that survived normalization.
        /// Read its doc comment for why a raw <c>StartsWith</c> is exploitable here. Duplicating
        /// that reasoning would produce two copies that drift.
        /// </para>
        /// </summary>
        public static bool IsArtAllowed(string? url) =>
            TournamentArtPolicy.IsAllowedUnder(url, AllowedArtRoot);

        /// <summary>
        /// Hosts a banner may send a player to, matched EXACTLY.
        ///
        /// <para>
        /// No suffix matching and no wildcard, deliberately: a <c>*.golfin.io</c> rule is precisely
        /// what would let <c>evil-golfin.io</c> and <c>golfin.io.attacker.net</c> through, and
        /// <c>link_url</c> is a free-text column.
        /// </para>
        /// <para>
        /// <c>golfin.io</c> is the live player-facing domain — the four URLs
        /// <c>SettingsController</c> already opens (<c>/terms-of-use</c>, <c>/privacy-policy</c>,
        /// <c>/faq</c>, <c>/contact</c>) are on it. <c>golfin.world</c> is the domain the admin
        /// dashboard runs on.
        /// </para>
        /// <para>
        /// ⚠️ This list SHIPS IN THE BUILD. An admin cannot add a host from the dashboard, by
        /// design — so a campaign page on a marketing host, a Notion/Typeform page, or a partner
        /// domain needs a client release, not a dashboard change. The dashboard's
        /// <c>ALLOWED_LINK_HOSTS</c> is kept in step with this list; a URL the dashboard accepts
        /// but this refuses is a banner that looks fine to the operator and does nothing on device.
        /// </para>
        /// </summary>
        private static readonly string[] AllowedLinkHosts =
        {
            "golfin.io", "www.golfin.io",
            "golfin.world", "www.golfin.world",
        };

        /// <summary>
        /// The scheme that means "this link stays inside the app" (gps_hub_entry §1).
        ///
        /// <para>
        /// A banner link is free text written in the dashboard, so an in-app destination needs the
        /// same fail-closed posture as an external one: a route is EXPLICITLY enumerated in
        /// <see cref="TryGetInternalRoute"/> or it is refused, exactly as a host is.
        /// </para>
        /// </summary>
        public const string InternalScheme = "golfin";

        /// <summary>
        /// <c>golfin://gps</c> → <see cref="ScreenId.GpsHub"/>. The ONLY internal route today.
        ///
        /// <para>
        /// Add routes HERE and nowhere else. A second ad-hoc parse at a call site is how the two
        /// copies drift, and this one is a navigation grant: whatever it returns, a server-supplied
        /// string gets to open.
        /// </para>
        /// <para>
        /// <see cref="Uri"/> lower-cases scheme and host, so <c>GOLFIN://GPS</c> matches and the
        /// ordinal compares below are exact rather than lenient. A route with a path, query or
        /// userinfo is refused: nothing in the switch consumes one, so accepting it would silently
        /// widen what a link may say.
        /// </para>
        /// <para>
        /// AN OLDER BUILD REFUSES <c>golfin://gps</c> and simply leaves the strip non-interactable —
        /// which is why the dashboard row can be activated without a <c>min_build</c> gate.
        /// </para>
        /// </summary>
        public static bool TryGetInternalRoute(string? url, out ScreenId screen)
        {
            screen = default;

            if (string.IsNullOrWhiteSpace(url)) return false;
            if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) || uri == null) return false;
            if (!string.Equals(uri.Scheme, InternalScheme, StringComparison.Ordinal)) return false;
            if (!string.IsNullOrEmpty(uri.UserInfo)) return false;

            // `golfin://gps` parses with an EMPTY path in .NET, `golfin://gps/x` with "/x".
            if (!string.IsNullOrEmpty(uri.AbsolutePath) && uri.AbsolutePath != "/") return false;
            if (!string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment)) return false;

            switch (uri.Host)
            {
                case "gps":
                    screen = ScreenId.GpsHub;
                    return true;
            }

            return false;
        }

        /// <summary>
        /// True when the banner's link may be followed at all — either an in-app route
        /// (<see cref="TryGetInternalRoute"/>) or an allowlisted external URL
        /// (<see cref="IsExternalLinkAllowed"/>).
        /// <para>
        /// This is what gates the Button's <c>interactable</c>, so a route this refuses shows the
        /// artwork with nothing to tap rather than doing something unexpected.
        /// </para>
        /// </summary>
        public static bool IsLinkAllowed(string? url) =>
            TryGetInternalRoute(url, out _) || IsExternalLinkAllowed(url);

        /// <summary>
        /// True when <paramref name="url"/> is an absolute https URL on an allowlisted host, with
        /// no userinfo and the default port. Everything unrecognised fails closed.
        /// <para>
        /// Re-checked at the point of <c>Application.OpenURL</c>, not only when the button was made
        /// interactable: the two are separated by a refresh that can swap the banner underneath.
        /// </para>
        /// </summary>
        public static bool IsExternalLinkAllowed(string? url)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;
            if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) || uri == null) return false;

            // Uri lower-cases scheme and host, so an ordinal compare here is exact, not lenient.
            if (!string.Equals(uri.Scheme, "https", StringComparison.Ordinal)) return false;

            // `https://a@golfin.io` parses with Host=golfin.io, so this is the check that stops a
            // credential-stuffed URL rather than a redundant one.
            if (!string.IsNullOrEmpty(uri.UserInfo)) return false;
            if (!uri.IsDefaultPort) return false;

            foreach (string host in AllowedLinkHosts)
                if (string.Equals(uri.Host, host, StringComparison.Ordinal)) return true;

            return false;
        }
    }
}
