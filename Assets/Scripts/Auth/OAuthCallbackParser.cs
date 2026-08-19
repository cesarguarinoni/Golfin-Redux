// Order: login_signup_screens Phase 2b — OAuth redirect (deep-link) parser
using System;
using System.Collections.Generic;

namespace Golfin.Auth
{
    /// <summary>
    /// Parses the deep-link URL Supabase redirects to after Google/Apple sign-in, e.g.
    ///   golfin://auth-callback#access_token=eyJ...&amp;expires_in=3600&amp;refresh_token=xyz&amp;token_type=bearer
    /// or an error:
    ///   golfin://auth-callback#error=access_denied&amp;error_description=User+cancelled
    /// Tokens arrive in the URL fragment (implicit flow); errors may be in the fragment or the query.
    /// Pure/static so the parsing is unit-testable without a running player.
    /// </summary>
    public static class OAuthCallbackParser
    {
        /// <summary>
        /// auth_recovery_flow — non-token metadata riding the callback: the GoTrue <c>type</c>
        /// (<c>recovery</c>, <c>signup</c>, …) and the error triplet Supabase puts in the fragment
        /// when an email link is expired or already used (e.g. <c>error_code=otp_expired</c>).
        /// Pure data; <see cref="Parse"/> stays the session-token path — minimal diff, nothing
        /// about the existing parse behavior changes.
        /// </summary>
        public readonly struct CallbackInfo
        {
            public readonly string Type;
            public readonly string Error;
            public readonly string ErrorCode;
            public readonly string ErrorDescription;

            public CallbackInfo(string type, string error, string errorCode, string errorDescription)
            {
                Type             = type;
                Error            = error;
                ErrorCode        = errorCode;
                ErrorDescription = errorDescription;
            }

            /// <summary>True when the link is a password-recovery callback (<c>type=recovery</c>).</summary>
            public bool IsRecovery => string.Equals(Type, "recovery", StringComparison.OrdinalIgnoreCase);

            /// <summary>True when Supabase reported an error (expired / already-used link, denial…).</summary>
            public bool HasError => !string.IsNullOrEmpty(Error) || !string.IsNullOrEmpty(ErrorCode);
        }

        /// <summary>Extract <c>type</c> + error params from the callback URL (fragment and/or query).</summary>
        public static CallbackInfo GetCallbackInfo(string url)
        {
            var p = ParseParams(url);
            p.TryGetValue("type", out var type);
            p.TryGetValue("error", out var error);
            p.TryGetValue("error_code", out var errorCode);
            string desc = p.TryGetValue("error_description", out var d) ? d.Replace('+', ' ') : null;
            return new CallbackInfo(type, error, errorCode, desc);
        }

        /// <summary>True when the URL is our app's OAuth redirect (matches the configured scheme/host).</summary>
        public static bool IsCallback(string url, SupabaseConfig config)
        {
            if (string.IsNullOrEmpty(url) || config == null || string.IsNullOrEmpty(config.oauthRedirect)) return false;
            return url.StartsWith(config.oauthRedirect, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Parse using the current time for expiry.</summary>
        public static AuthResult Parse(string url)
            => Parse(url, DateTimeOffset.UtcNow.ToUnixTimeSeconds());

        /// <summary>Parse with an explicit 'now' (unix seconds) — used by tests for deterministic expiry.</summary>
        public static AuthResult Parse(string url, long nowUnix)
        {
            var p = ParseParams(url);

            if (p.TryGetValue("error", out var err) || p.TryGetValue("error_code", out err))
            {
                string desc = p.TryGetValue("error_description", out var d) ? d.Replace('+', ' ') : err;
                var kind = err != null && err.IndexOf("cancel", StringComparison.OrdinalIgnoreCase) >= 0
                    ? AuthError.Unknown : AuthError.Unknown;
                return AuthResult.Fail(kind, string.IsNullOrEmpty(desc) ? "Sign-in was cancelled or failed." : desc);
            }

            if (p.TryGetValue("access_token", out var access) && !string.IsNullOrEmpty(access))
            {
                p.TryGetValue("refresh_token", out var refresh);
                long expiresAt = 0;
                if (p.TryGetValue("expires_in", out var ei) && long.TryParse(ei, out var secs))
                    expiresAt = nowUnix + secs;
                // The redirect carries only tokens, not the user profile — AuthService follows up with
                // GetUser(accessToken) to populate email / display_name.
                return AuthResult.Ok(user: null, accessToken: access, refreshToken: refresh, expiresAtUnix: expiresAt);
            }

            return AuthResult.Fail(AuthError.Unknown, "Sign-in did not return a session. Please try again.");
        }

        /// <summary>Extract key/value pairs from the URL fragment (after '#') and/or query (after '?').</summary>
        private static Dictionary<string, string> ParseParams(string url)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(url)) return dict;

            void Absorb(char sep)
            {
                int i = url.IndexOf(sep);
                if (i < 0 || i == url.Length - 1) return;
                string seg = url.Substring(i + 1);
                foreach (var pair in seg.Split('&'))
                {
                    if (pair.Length == 0) continue;
                    int eq = pair.IndexOf('=');
                    string k = eq >= 0 ? pair.Substring(0, eq) : pair;
                    string v = eq >= 0 ? pair.Substring(eq + 1) : "";
                    try { v = Uri.UnescapeDataString(v); } catch { /* keep raw */ }
                    if (!string.IsNullOrEmpty(k)) dict[k] = v;
                }
            }

            Absorb('#'); // implicit-flow tokens live in the fragment
            Absorb('?'); // errors sometimes come as query params
            return dict;
        }
    }
}
