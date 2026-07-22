// Order: login_signup_screens Phase 2 — auth transport models (mockable now, live/OAuth next phase)
namespace Golfin.Auth
{
    /// <summary>Categorised auth failure, so the UI can show the right message and the flow can branch.</summary>
    public enum AuthError
    {
        None = 0,
        InvalidEmail,
        InvalidCredentials,      // wrong email/password on login
        EmailNotConfirmed,       // login before the confirmation link was clicked
        EmailAlreadyRegistered,  // signup with an existing email
        WeakPassword,            // server rejected the password
        RateLimited,             // too many requests (Supabase 429)
        Network,                 // no connection / timeout
        Server,                  // 5xx / unexpected server response
        NotImplemented,          // OAuth in the mock / before Phase 2b
        Unknown
    }

    /// <summary>Third-party sign-in providers. Wired in Phase 2b (deep-link redirect flow).</summary>
    public enum OAuthProvider
    {
        Google,
        Apple
    }

    /// <summary>Minimal authenticated user identity returned by Supabase Auth.</summary>
    public sealed class AuthUser
    {
        public string Id;
        public string Email;
        public string DisplayName;   // Supabase user_metadata.display_name (set on Create Username)
        public bool   EmailConfirmed;
    }

    /// <summary>
    /// Uniform result for every auth call. On success carries the session tokens + user;
    /// on failure carries a categorised <see cref="AuthError"/> and a user-facing message.
    /// </summary>
    public sealed class AuthResult
    {
        public bool      Success;
        public AuthError Error;
        public string    Message;        // user-facing, already localised-friendly plain text

        // Session (present when the call establishes/refreshes a session; null on signup-needs-confirmation)
        public bool      HasSession;
        public string    AccessToken;
        public string    RefreshToken;
        public long      ExpiresAtUnix;  // absolute expiry (unix seconds); 0 when unknown

        public AuthUser  User;

        public static AuthResult Ok(AuthUser user, string accessToken = null, string refreshToken = null,
                                     long expiresAtUnix = 0, string message = null)
        {
            bool hasSession = !string.IsNullOrEmpty(accessToken);
            return new AuthResult
            {
                Success       = true,
                Error         = AuthError.None,
                Message       = message,
                HasSession    = hasSession,
                AccessToken   = accessToken,
                RefreshToken  = refreshToken,
                ExpiresAtUnix = expiresAtUnix,
                User          = user
            };
        }

        public static AuthResult Fail(AuthError error, string message)
        {
            return new AuthResult { Success = false, Error = error, Message = message };
        }
    }
}
