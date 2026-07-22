// Order: login_signup_screens Phase 2b — OAuth authorize-URL builder
namespace Golfin.Auth
{
    /// <summary>
    /// Builds the Supabase Auth (GoTrue) OAuth authorize URL. The app opens this in the system browser;
    /// after the user signs in with Google/Apple, Supabase redirects back to <c>config.oauthRedirect</c>
    /// with the session tokens in the URL fragment (parsed by <see cref="OAuthCallbackParser"/>).
    /// Pure/static so it is unit-testable without a running player.
    /// </summary>
    public static class OAuthUrlBuilder
    {
        public static string ProviderKey(OAuthProvider provider)
            => provider == OAuthProvider.Apple ? "apple" : "google";

        /// <summary>GET {auth}/authorize?provider=&lt;p&gt;&amp;redirect_to=&lt;deep-link&gt;</summary>
        public static string Authorize(SupabaseConfig config, OAuthProvider provider)
        {
            string redirect = System.Uri.EscapeDataString(config.oauthRedirect ?? "");
            return $"{config.AuthBaseUrl}/authorize?provider={ProviderKey(provider)}&redirect_to={redirect}";
        }
    }
}
