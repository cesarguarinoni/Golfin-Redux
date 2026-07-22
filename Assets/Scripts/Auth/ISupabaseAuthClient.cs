// Order: login_signup_screens Phase 2 — auth transport seam (mock now, live UnityWebRequest next phase)
using System;

namespace Golfin.Auth
{
    /// <summary>
    /// Transport seam for Supabase Auth. Two implementations:
    ///   • <see cref="MockSupabaseAuthClient"/> — offline, deterministic (used until Ken supplies the anon key).
    ///   • <see cref="SupabaseAuthClient"/>     — live UnityWebRequest against the Supabase Auth REST API.
    /// Callback-based (not async/Task) to stay Unity-idiomatic and coroutine-friendly. Every callback
    /// is invoked exactly once on the main thread.
    /// </summary>
    public interface ISupabaseAuthClient
    {
        /// <summary>POST /auth/v1/signup — creates the account; when email confirmation is on the result
        /// has no session (HasSession == false) and the user must click the emailed link first.</summary>
        void SignUp(string email, string password, Action<AuthResult> onResult);

        /// <summary>POST /auth/v1/token?grant_type=password — email/password login; returns a session.</summary>
        void SignInWithPassword(string email, string password, Action<AuthResult> onResult);

        /// <summary>POST /auth/v1/resend — re-sends the signup confirmation email.</summary>
        void ResendConfirmation(string email, Action<AuthResult> onResult);

        /// <summary>POST /auth/v1/recover — sends the password-reset email.</summary>
        void RequestPasswordReset(string email, Action<AuthResult> onResult);

        /// <summary>PUT /auth/v1/user — sets user_metadata.display_name (the Create Username step).
        /// Requires a valid access token.</summary>
        void UpdateDisplayName(string accessToken, string displayName, Action<AuthResult> onResult);

        /// <summary>POST /auth/v1/token?grant_type=refresh_token — exchanges a refresh token for a fresh session.</summary>
        void RefreshSession(string refreshToken, Action<AuthResult> onResult);

        /// <summary>Phase 2b — OAuth (Google/Apple) via the deep-link redirect flow. Present on the seam
        /// now so screens can call it; the mock and the current live client return NotImplemented until 2b.</summary>
        void SignInWithOAuth(OAuthProvider provider, Action<AuthResult> onResult);
    }
}
