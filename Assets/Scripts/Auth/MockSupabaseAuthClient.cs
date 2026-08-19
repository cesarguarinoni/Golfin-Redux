// Order: login_signup_screens Phase 2 — offline deterministic auth transport
using System;
using System.Collections.Generic;

namespace Golfin.Auth
{
    /// <summary>
    /// Offline, deterministic <see cref="ISupabaseAuthClient"/>. Used until Ken supplies the anon key
    /// (SupabaseConfig.useMockTransport = true). Keeps an in-memory account store so the full
    /// sign-up → confirm → login → set-username flow is walkable in the editor, and every branch
    /// (email-taken, wrong-password, not-confirmed, OAuth-not-implemented) is unit-testable.
    ///
    /// Callbacks fire synchronously — trivial to assert in EditMode tests. The live client is async.
    /// </summary>
    public sealed class MockSupabaseAuthClient : ISupabaseAuthClient
    {
        private sealed class Account
        {
            public string Id;
            public string Email;
            public string Password;
            public bool   Confirmed;
            public string DisplayName;
        }

        private readonly Dictionary<string, Account> _accounts =
            new Dictionary<string, Account>(StringComparer.OrdinalIgnoreCase);
        private int _idSeq = 1000;

        /// <summary>Manual-demo convenience: mark new signups confirmed immediately so a follow-up login
        /// succeeds without a real email. Set false in tests to exercise the EmailNotConfirmed branch.</summary>
        public bool AutoConfirmOnSignUp = true;

        /// <summary>Force every call to fail with <see cref="AuthError.Network"/> (tests).</summary>
        public bool SimulateNetwork = false;

        // ── Test seeding ─────────────────────────────────────────────────────────
        public void SeedAccount(string email, string password, bool confirmed, string displayName = null)
        {
            _accounts[email] = new Account
            {
                Id = "mock-" + (_idSeq++), Email = email, Password = password,
                Confirmed = confirmed, DisplayName = displayName
            };
        }

        // ── ISupabaseAuthClient ──────────────────────────────────────────────────
        public void SignUp(string email, string password, Action<AuthResult> onResult)
        {
            if (Net(onResult)) return;
            if (!LooksLikeEmail(email)) { onResult(AuthResult.Fail(AuthError.InvalidEmail, "Enter a valid email address.")); return; }
            if (_accounts.ContainsKey(email)) { onResult(AuthResult.Fail(AuthError.EmailAlreadyRegistered, "That email is already registered. Try logging in.")); return; }
            if (string.IsNullOrEmpty(password) || password.Length < 8) { onResult(AuthResult.Fail(AuthError.WeakPassword, "Password does not meet the requirements.")); return; }

            var acc = new Account { Id = "mock-" + (_idSeq++), Email = email, Password = password, Confirmed = AutoConfirmOnSignUp };
            _accounts[email] = acc;

            // Real Supabase returns NO session when email confirmation is on — mirror that.
            onResult(AuthResult.Ok(ToUser(acc), message: "Confirmation email sent."));
        }

        public void SignInWithPassword(string email, string password, Action<AuthResult> onResult)
        {
            if (Net(onResult)) return;
            if (!_accounts.TryGetValue(email, out var acc) || acc.Password != password)
            { onResult(AuthResult.Fail(AuthError.InvalidCredentials, "Incorrect email or password.")); return; }
            if (!acc.Confirmed)
            { onResult(AuthResult.Fail(AuthError.EmailNotConfirmed, "Please confirm your email first.")); return; }

            onResult(Session(acc, "Signed in."));
        }

        public void ResendConfirmation(string email, Action<AuthResult> onResult)
        {
            if (Net(onResult)) return;
            // Supabase returns success even for unknown emails (no account enumeration) — mirror that.
            onResult(AuthResult.Ok(null, message: "Confirmation email re-sent."));
        }

        public void RequestPasswordReset(string email, Action<AuthResult> onResult)
        {
            if (Net(onResult)) return;
            onResult(AuthResult.Ok(null, message: "Password reset email sent."));
        }

        public void UpdateDisplayName(string accessToken, string displayName, Action<AuthResult> onResult)
        {
            if (Net(onResult)) return;
            if (string.IsNullOrEmpty(accessToken)) { onResult(AuthResult.Fail(AuthError.InvalidCredentials, "Not signed in.")); return; }
            // Mock: apply to whichever account this fake token belongs to (token encodes the id).
            Account acc = FindByToken(accessToken);
            if (acc == null) { onResult(AuthResult.Fail(AuthError.InvalidCredentials, "Session expired.")); return; }
            acc.DisplayName = displayName;
            onResult(AuthResult.Ok(ToUser(acc), message: "Username saved."));
        }

        // auth_recovery_flow — mirrors the live PUT /user {"password":…}. Same 8-char floor as the
        // mock SignUp so both mock entry points agree. NOTE: real minimum lives in Supabase project
        // settings (spec §5 says verify before hardcoding client copy).
        public void UpdatePassword(string accessToken, string newPassword, Action<AuthResult> onResult)
        {
            if (Net(onResult)) return;
            Account acc = FindByToken(accessToken);
            if (acc == null) { onResult(AuthResult.Fail(AuthError.InvalidCredentials, "Session expired.")); return; }
            if (string.IsNullOrEmpty(newPassword) || newPassword.Length < 8)
            { onResult(AuthResult.Fail(AuthError.WeakPassword, "Password does not meet the requirements.")); return; }
            acc.Password = newPassword;
            onResult(AuthResult.Ok(ToUser(acc), message: "Password updated."));
        }

        public void RefreshSession(string refreshToken, Action<AuthResult> onResult)
        {
            if (Net(onResult)) return;
            Account acc = FindByToken(refreshToken);
            if (acc == null) { onResult(AuthResult.Fail(AuthError.InvalidCredentials, "Session expired. Please log in again.")); return; }
            onResult(Session(acc, "Session refreshed."));
        }

        /// <summary>When true, OAuth returns a synthetic signed-in session (for flow demos/tests) instead
        /// of "coming soon". Default false — keeps the pre-2b UX.</summary>
        public bool SimulateOAuthSuccess = false;

        public void GetUser(string accessToken, Action<AuthResult> onResult)
        {
            if (Net(onResult)) return;
            var acc = FindByToken(accessToken);
            if (acc == null) { onResult(AuthResult.Fail(AuthError.InvalidCredentials, "Session expired.")); return; }
            onResult(AuthResult.Ok(ToUser(acc)));
        }

        public void SignInWithOAuth(OAuthProvider provider, Action<AuthResult> onResult)
        {
            if (Net(onResult)) return;
            if (!SimulateOAuthSuccess)
            {
                // Phase 2b. The seam exists so screens can call it today; live flow is deep-link based.
                onResult(AuthResult.Fail(AuthError.NotImplemented, $"{provider} sign-in is coming soon."));
                return;
            }
            string email = OAuthUrlBuilder.ProviderKey(provider) + "-user@example.com";
            if (!_accounts.TryGetValue(email, out var acc))
            { acc = new Account { Id = "mock-" + (_idSeq++), Email = email, Confirmed = true }; _accounts[email] = acc; }
            onResult(Session(acc, $"Signed in with {provider}."));
        }

        // ── helpers ──────────────────────────────────────────────────────────────
        private bool Net(Action<AuthResult> onResult)
        {
            if (!SimulateNetwork) return false;
            onResult(AuthResult.Fail(AuthError.Network, "No internet connection. Please try again."));
            return true;
        }

        private AuthResult Session(Account acc, string message)
        {
            long expires = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 3600;
            return AuthResult.Ok(ToUser(acc),
                accessToken: "mock-access." + acc.Id,
                refreshToken: "mock-refresh." + acc.Id,
                expiresAtUnix: expires, message: message);
        }

        private static AuthUser ToUser(Account acc) => new AuthUser
        {
            Id = acc.Id, Email = acc.Email, DisplayName = acc.DisplayName, EmailConfirmed = acc.Confirmed
        };

        private Account FindByToken(string token)
        {
            if (string.IsNullOrEmpty(token)) return null;
            int dot = token.LastIndexOf('.');
            string id = dot >= 0 ? token.Substring(dot + 1) : token;
            foreach (var a in _accounts.Values) if (a.Id == id) return a;
            return null;
        }

        private static bool LooksLikeEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email)) return false;
            int at = email.IndexOf('@');
            return at > 0 && email.IndexOf('.', at) > at + 1 && !email.EndsWith(".");
        }
    }
}
