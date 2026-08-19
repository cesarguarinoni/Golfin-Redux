// Order: auth_email_redirect — redirect_to query builder for the email-bearing auth endpoints
using System;

namespace Golfin.Auth
{
    /// <summary>
    /// Appends GoTrue's <c>redirect_to</c> query parameter to an auth endpoint path.
    ///
    /// WIRE FORMAT: for the raw REST API (what <see cref="SupabaseAuthClient"/> speaks) the redirect is a
    /// QUERY parameter — <c>POST /auth/v1/signup?redirect_to=...</c> — not a body field. The
    /// <c>options.emailRedirectTo</c> shape belongs to supabase-js, which lowers it onto this same query
    /// param. Mirrors how <see cref="OAuthUrlBuilder"/> passes the deep link today.
    ///
    /// Applies to the three endpoints that send an email: /signup, /resend, /recover.
    /// Pure/static so it is unit-testable without a running player.
    /// </summary>
    public static class AuthRedirectUrl
    {
        /// <summary>
        /// <paramref name="path"/> with <c>redirect_to=&lt;escaped&gt;</c> appended, using '?' or '&amp;'
        /// depending on whether the path already carries a query. Returns the path untouched when no
        /// redirect is configured, so clearing the field in SupabaseConfig restores the old behaviour
        /// (Supabase falls back to its Site URL).
        /// </summary>
        public static string Append(string path, string redirectTo)
        {
            if (string.IsNullOrWhiteSpace(redirectTo)) return path;
            string sep = (path != null && path.IndexOf('?') >= 0) ? "&" : "?";
            return path + sep + "redirect_to=" + Uri.EscapeDataString(redirectTo);
        }
    }
}
