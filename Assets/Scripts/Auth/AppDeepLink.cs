// Order: oauth_callback_per_app — the deep-link scheme THIS binary owns
using System;

namespace Golfin.Auth
{
    /// <summary>
    /// The custom URL scheme this binary claims, and therefore the one every redirect it asks
    /// for must come back on: the OAuth callback, and the hop the confirm/recovery landing page
    /// (confirm.golfin.world) makes back into the app.
    ///
    /// <para>
    /// WHY IT IS PER-VARIANT. "punch it standalone" ships a SECOND app record
    /// (<c>com.nextinnovation.golfingps</c>) that installs BESIDE the game on one phone. iOS
    /// resolves a custom scheme to whichever installed app claims it, and when two apps claim
    /// the same one the choice is undefined — in practice the most recently installed wins.
    /// Both apps asked Supabase to redirect to <c>golfin://auth-callback</c>, and the shell's
    /// Info.plist claimed <c>golfin</c> on top of its own scheme, so a Google sign-in started in
    /// the GAME landed in the GPS app — which parsed the tokens and signed ITSELF in — while the
    /// game's 8 s watchdog reported "Sign-in didn't complete". Reported 2026-09-11: the game
    /// could not be logged into until the GPS app was deleted.
    /// </para>
    /// <para>
    /// ONE define, three consumers that must agree on the string:
    /// <list type="bullet">
    ///   <item><c>StandaloneBuildPreprocessor</c> (editor) stamps Info.plist
    ///     <c>CFBundleURLSchemes</c> = [<see cref="StandaloneScheme"/>] and NOTHING else. An
    ///     editor script cannot read a player define, so it keeps its own copy of the string;
    ///     <c>StandaloneUrlSchemeTests</c> pins the two together.</item>
    ///   <item><see cref="OAuthUrlBuilder"/> / <see cref="OAuthCallbackParser"/> — the
    ///     <c>redirect_to</c> Supabase is asked for and the callback the app accepts, both through
    ///     <see cref="SupabaseConfig.OAuthRedirectForThisApp"/>.</item>
    ///   <item>The landing page — <c>?app=&lt;scheme&gt;</c> tells it which app to hop into
    ///     (<see cref="TagLandingPage(string)"/>); no tag means the game.</item>
    /// </list>
    /// </para>
    /// <para>
    /// Mirrors <c>AppVariantInfo</c>'s <c>#if</c> ladder. A copy rather than a reference because
    /// <c>Golfin.Auth</c> is an asmdef that cannot see Assembly-CSharp; the define is what the
    /// two genuinely share. <c>BannerPolicy</c> aliases these constants for its in-app routes.
    /// </para>
    /// <para>
    /// Android: <c>Assets/Plugins/Android/AndroidManifest.xml</c> hard-codes
    /// <c>golfin</c> / <c>auth-callback</c>. There is no Android shell today; the day there is
    /// one, its manifest needs <see cref="StandaloneScheme"/> the same way.
    /// </para>
    /// </summary>
    public static class AppDeepLink
    {
        /// <summary>The game's scheme (<c>com.nextinnovation.golfingame</c>) — the one it has always had.</summary>
        public const string GameScheme = "golfin";

        /// <summary>The PLAYLIFE shell's scheme (<c>com.nextinnovation.golfingps</c>) — the ONLY one it claims.</summary>
        public const string StandaloneScheme = "golfingps";

#if GOLFIN_STANDALONE
        public const string Scheme = StandaloneScheme;
#else
        public const string Scheme = GameScheme;
#endif

        /// <summary>Query key the landing page reads to pick the app it hops back into.</summary>
        public const string AppQueryKey = "app";

        /// <summary><paramref name="deepLink"/> re-schemed for THIS app. See <see cref="WithScheme"/>.</summary>
        public static string ForThisApp(string deepLink) => WithScheme(deepLink, Scheme);

        /// <summary>
        /// <paramref name="deepLink"/> with everything before <c>://</c> replaced by
        /// <paramref name="scheme"/>. Null, empty, or a string with no <c>://</c> is returned
        /// untouched — a config field someone cleared must keep meaning "no redirect".
        /// </summary>
        public static string WithScheme(string deepLink, string scheme)
        {
            if (string.IsNullOrEmpty(deepLink)) return deepLink;
            int sep = deepLink.IndexOf("://", StringComparison.Ordinal);
            if (sep < 0) return deepLink;
            return scheme + deepLink.Substring(sep);
        }

        /// <summary>The hosted landing-page URL tagged for THIS app. See <see cref="TagLandingPage(string, string)"/>.</summary>
        public static string TagLandingPage(string url) => TagLandingPage(url, Scheme);

        /// <summary>
        /// <paramref name="url"/> with <c>app=&lt;scheme&gt;</c> appended so the landing page hops
        /// back into the app that sent the email. The GAME's URLs are deliberately left
        /// byte-identical (the page defaults to <see cref="GameScheme"/>): what the game sends
        /// today is what is allow-listed and proven, and this must not be able to regress it.
        /// </summary>
        public static string TagLandingPage(string url, string scheme)
        {
            if (string.IsNullOrWhiteSpace(url) || string.Equals(scheme, GameScheme, StringComparison.Ordinal)) return url;
            string sep = url.IndexOf('?') >= 0 ? "&" : "?";
            return url + sep + AppQueryKey + "=" + Uri.EscapeDataString(scheme);
        }
    }
}
