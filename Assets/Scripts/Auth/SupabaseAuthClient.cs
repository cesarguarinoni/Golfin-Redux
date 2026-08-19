// Order: login_signup_screens Phase 2 — LIVE Supabase Auth transport (UnityWebRequest)
// Dormant until SupabaseConfig.useMockTransport = false AND anonKey is set (see PHASE2_SERVER_SETUP_FOR_KEN.md).
using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Golfin.Auth
{
    /// <summary>
    /// Live <see cref="ISupabaseAuthClient"/> hitting the Supabase Auth (GoTrue) REST API directly, per
    /// GPS_INTEGRATION_REFERENCE.md §3 (client → supabase.co, anon key, JWT back). Coroutine-driven; the
    /// injected <paramref name="runner"/> (AuthService) starts the coroutines.
    ///
    /// OAuth (SignInWithOAuth) is intentionally NotImplemented here — it needs the deep-link redirect
    /// flow that is scoped for Phase 2b.
    /// </summary>
    public sealed class SupabaseAuthClient : ISupabaseAuthClient
    {
        private readonly SupabaseConfig _config;
        private readonly MonoBehaviour _runner;

        public SupabaseAuthClient(SupabaseConfig config, MonoBehaviour runner)
        {
            _config = config;
            _runner = runner;
        }

        public void SignUp(string email, string password, Action<AuthResult> onResult)
            => _runner.StartCoroutine(Post(WithEmailRedirect("/signup"), Json2("email", email, "password", password), null, onResult, expectSession: false));

        public void SignInWithPassword(string email, string password, Action<AuthResult> onResult)
            => _runner.StartCoroutine(Post("/token?grant_type=password", Json2("email", email, "password", password), null, onResult, expectSession: true));

        public void ResendConfirmation(string email, Action<AuthResult> onResult)
            => _runner.StartCoroutine(Post(WithEmailRedirect("/resend"), Json2("type", "signup", "email", email), null, onResult, expectSession: false));

        public void RequestPasswordReset(string email, Action<AuthResult> onResult)
            => _runner.StartCoroutine(Post(AuthRedirectUrl.Append("/recover", _config.passwordResetRedirect), Json1("email", email), null, onResult, expectSession: false));

        public void UpdateDisplayName(string accessToken, string displayName, Action<AuthResult> onResult)
            => _runner.StartCoroutine(Put("/user", "{\"data\":{\"display_name\":" + Quote(displayName) + "}}", accessToken, onResult));

        public void RefreshSession(string refreshToken, Action<AuthResult> onResult)
            => _runner.StartCoroutine(Post("/token?grant_type=refresh_token", Json1("refresh_token", refreshToken), null, onResult, expectSession: true));

        public void GetUser(string accessToken, Action<AuthResult> onResult)
            => _runner.StartCoroutine(Send(UnityWebRequest.kHttpVerbGET, "/user", null, accessToken, onResult, expectSession: false));

        // OAuth for the LIVE client is orchestrated by AuthService (system browser + deep-link redirect),
        // not this transport — the browser + custom-URL-scheme handling is app-level, not HTTP. See
        // AuthService.SignInWithOAuth / OnDeepLink + OAuthUrlBuilder + OAuthCallbackParser.
        public void SignInWithOAuth(OAuthProvider provider, Action<AuthResult> onResult)
            => onResult(AuthResult.Fail(AuthError.NotImplemented, "OAuth is handled by AuthService for live transport."));

        // ── redirect_to ────────────────────────────────────────
        // Without an explicit redirect_to, Supabase falls back to the project Site URL
        // (admin.golfin.world) and the emailed link dead-ends on a Cloudflare Access block page.
        // Spec: Docs/Specs/Active/auth_email_redirect/SPEC.md.
        private string WithEmailRedirect(string path) => AuthRedirectUrl.Append(path, _config.emailConfirmRedirect);

        // ── HTTP ───────────────────────────────────────────────────────────────
        private IEnumerator Post(string path, string body, string bearer, Action<AuthResult> onResult, bool expectSession)
            => Send(UnityWebRequest.kHttpVerbPOST, path, body, bearer, onResult, expectSession);

        private IEnumerator Put(string path, string body, string bearer, Action<AuthResult> onResult)
            => Send(UnityWebRequest.kHttpVerbPUT, path, body, bearer, onResult, expectSession: false);

        private IEnumerator Send(string verb, string path, string body, string bearer, Action<AuthResult> onResult, bool expectSession)
        {
            string url = _config.AuthBaseUrl + path;
            using (var req = new UnityWebRequest(url, verb))
            {
                if (!string.IsNullOrEmpty(body))
                    req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                req.downloadHandler = new DownloadHandlerBuffer();
                req.timeout         = Mathf.Max(5, _config.requestTimeoutSeconds);
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("apikey", _config.anonKey);
                req.SetRequestHeader("Authorization", "Bearer " + (string.IsNullOrEmpty(bearer) ? _config.anonKey : bearer));

                yield return req.SendWebRequest();

                if (req.result == UnityWebRequest.Result.ConnectionError || req.result == UnityWebRequest.Result.DataProcessingError)
                {
                    onResult(AuthResult.Fail(AuthError.Network, "No internet connection. Please try again."));
                    yield break;
                }

                long status = req.responseCode;
                string text = req.downloadHandler != null ? req.downloadHandler.text : null;

                if (status >= 200 && status < 300)
                {
                    onResult(ParseSuccess(text, expectSession));
                }
                else
                {
                    onResult(ParseError(status, text));
                }
            }
        }

        // ── parsing ──────────────────────────────────────────────────────────────
        private static AuthResult ParseSuccess(string json, bool expectSession)
        {
            AuthResponse r = SafeParse<AuthResponse>(json);
            AuthUser user = ExtractUser(r);
            if (!string.IsNullOrEmpty(r != null ? r.access_token : null))
            {
                long expires = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + (r.expires_in > 0 ? r.expires_in : 3600);
                return AuthResult.Ok(user, r.access_token, r.refresh_token, expires);
            }
            // No session (signup-needs-confirmation, resend, recover, update-user with no token echo).
            return AuthResult.Ok(user);
        }

        private static AuthResult ParseError(long status, string json)
        {
            ErrorDto e = SafeParse<ErrorDto>(json);
            string msg = FirstNonEmpty(e?.error_description, e?.msg, e?.message, e?.error);
            string lower = (msg ?? "").ToLowerInvariant();

            if (status == 429) return AuthResult.Fail(AuthError.RateLimited, "Too many attempts. Please wait a moment and try again.");
            if (status >= 500)  return AuthResult.Fail(AuthError.Server, "Server error. Please try again later.");

            if (lower.Contains("already registered") || lower.Contains("already been registered") || lower.Contains("user already"))
                return AuthResult.Fail(AuthError.EmailAlreadyRegistered, "That email is already registered. Try logging in.");
            if (lower.Contains("email not confirmed"))
                return AuthResult.Fail(AuthError.EmailNotConfirmed, "Please confirm your email first.");
            if (lower.Contains("invalid login") || lower.Contains("invalid_grant") || lower.Contains("invalid credentials"))
                return AuthResult.Fail(AuthError.InvalidCredentials, "Incorrect email or password.");
            if (lower.Contains("password"))
                return AuthResult.Fail(AuthError.WeakPassword, string.IsNullOrEmpty(msg) ? "Password does not meet the requirements." : msg);

            return AuthResult.Fail(AuthError.Unknown, string.IsNullOrEmpty(msg) ? "Something went wrong. Please try again." : msg);
        }

        private static AuthUser ExtractUser(AuthResponse r)
        {
            if (r == null) return null;
            UserDto u = r.user != null && !string.IsNullOrEmpty(r.user.id) ? r.user : r.AsTopLevelUser();
            if (u == null || string.IsNullOrEmpty(u.id)) return null;
            return new AuthUser
            {
                Id             = u.id,
                Email          = u.email,
                DisplayName    = u.user_metadata != null ? u.user_metadata.display_name : null,
                EmailConfirmed = !string.IsNullOrEmpty(u.email_confirmed_at) || !string.IsNullOrEmpty(u.confirmed_at)
            };
        }

        private static T SafeParse<T>(string json) where T : class
        {
            if (string.IsNullOrEmpty(json)) return null;
            try { return JsonUtility.FromJson<T>(json); }
            catch { return null; }
        }

        private static string FirstNonEmpty(params string[] xs)
        {
            foreach (var x in xs) if (!string.IsNullOrEmpty(x)) return x;
            return null;
        }

        // ── tiny JSON writers (avoid a dependency; values are escaped) ────────────
        private static string Json1(string k, string v) => "{" + Quote(k) + ":" + Quote(v) + "}";
        private static string Json2(string k1, string v1, string k2, string v2)
            => "{" + Quote(k1) + ":" + Quote(v1) + "," + Quote(k2) + ":" + Quote(v2) + "}";

        private static string Quote(string s)
        {
            if (s == null) return "\"\"";
            var sb = new StringBuilder(s.Length + 2);
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"':  sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n");  break;
                    case '\r': sb.Append("\\r");  break;
                    case '\t': sb.Append("\\t");  break;
                    default:   sb.Append(c);      break;
                }
            }
            sb.Append('"');
            return sb.ToString();
        }

        // ── DTOs (JsonUtility) ────────────────────────────────────────────────────
        [Serializable]
        private sealed class AuthResponse
        {
            public string access_token;
            public string token_type;
            public long   expires_in;
            public string refresh_token;
            public UserDto user;

            // top-level user fields (present on /signup and /user responses)
            public string id;
            public string email;
            public string email_confirmed_at;
            public string confirmed_at;
            public UserMeta user_metadata;

            public UserDto AsTopLevelUser()
            {
                if (string.IsNullOrEmpty(id)) return null;
                return new UserDto
                {
                    id = id, email = email, email_confirmed_at = email_confirmed_at,
                    confirmed_at = confirmed_at, user_metadata = user_metadata
                };
            }
        }

        [Serializable]
        private sealed class UserDto
        {
            public string id;
            public string email;
            public string email_confirmed_at;
            public string confirmed_at;
            public UserMeta user_metadata;
        }

        [Serializable]
        private sealed class UserMeta
        {
            public string display_name;
        }

        [Serializable]
        private sealed class ErrorDto
        {
            public string error;
            public string error_description;
            public string msg;
            public string message;
            public string error_code;
            public int    code;
        }
    }
}
