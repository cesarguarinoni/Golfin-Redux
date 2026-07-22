// Order: login_signup_screens Phase 2 — auth facade (session owner + transport selector + coroutine runner)
using System;
using UnityEngine;

namespace Golfin.Auth
{
    /// <summary>
    /// Single entry point the account screens call. Self-bootstrapping (no scene wiring): the first
    /// access to <see cref="Instance"/> creates a DontDestroyOnLoad host, loads <see cref="SupabaseConfig"/>
    /// from Resources, and selects the transport (mock vs live). Owns the <see cref="AuthSession"/> and
    /// updates+persists it whenever a call establishes a session.
    ///
    /// Being a MonoBehaviour, it can run the live client's UnityWebRequest coroutines. The mock client
    /// ignores the runner (its callbacks are synchronous).
    /// </summary>
    public sealed class AuthService : MonoBehaviour
    {
        private static AuthService _instance;
        public static AuthService Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("[AuthService]");
                    _instance = go.AddComponent<AuthService>();
                    DontDestroyOnLoad(go);
                    _instance.Initialize();
                }
                return _instance;
            }
        }

        public SupabaseConfig Config { get; private set; }
        public AuthSession Session { get; private set; } = new AuthSession();

        private ISupabaseAuthClient _client;
        private bool _useMock;

        // Phase 2b — pending OAuth completion, resolved when the deep-link redirect arrives.
        private Action<AuthResult> _pendingOAuth;
        private bool _deepLinkHooked;

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            if (_instance == null) { _instance = this; DontDestroyOnLoad(gameObject); Initialize(); }
        }

        private void Initialize()
        {
            if (_client != null) return; // already initialised (lazy path)

            Config = Resources.Load<SupabaseConfig>("SupabaseConfig");
            if (Config == null)
            {
                Debug.LogWarning("[AuthService] No SupabaseConfig in Resources — defaulting to MOCK transport.");
                Config = ScriptableObject.CreateInstance<SupabaseConfig>(); // useMockTransport = true by default
            }

            _useMock = Config.useMockTransport || Config.LiveButUnconfigured;
            if (Config.LiveButUnconfigured)
                Debug.LogWarning("[AuthService] Live transport selected but anonKey is empty — falling back to MOCK. See PHASE2_SERVER_SETUP_FOR_KEN.md.");

            _client = _useMock
                ? (ISupabaseAuthClient)new MockSupabaseAuthClient()
                : new SupabaseAuthClient(Config, this);

            HookDeepLinks();
            Session.Load();
            Debug.Log($"[AuthService] Ready — transport={(_useMock ? "MOCK" : "LIVE")}, authenticated={Session.IsAuthenticated}.");
        }

        private void HookDeepLinks()
        {
            if (_deepLinkHooked) return;
            _deepLinkHooked = true;
            Application.deepLinkActivated += OnDeepLink;
            // Cold-start: the app may have been launched by the redirect itself.
            if (!string.IsNullOrEmpty(Application.absoluteURL))
                OnDeepLink(Application.absoluteURL);
        }

        private void OnDestroy()
        {
            if (_deepLinkHooked) Application.deepLinkActivated -= OnDeepLink;
        }

        /// <summary>Test hook: inject a client + fresh session directly (bypasses Resources/scene).</summary>
        public void ConfigureForTest(ISupabaseAuthClient client, AuthSession session = null)
        {
            _client = client;
            Session = session ?? new AuthSession();
        }

        // ── Public API (mirrors the transport, but owns session side-effects) ─────
        public void SignUp(string email, string password, Action<AuthResult> onResult)
            => _client.SignUp(email, password, Wrap(onResult));

        public void SignInWithPassword(string email, string password, Action<AuthResult> onResult)
            => _client.SignInWithPassword(email, password, Wrap(onResult));

        public void ResendConfirmation(string email, Action<AuthResult> onResult)
            => _client.ResendConfirmation(email, onResult);

        public void RequestPasswordReset(string email, Action<AuthResult> onResult)
            => _client.RequestPasswordReset(email, onResult);

        public void UpdateDisplayName(string displayName, Action<AuthResult> onResult)
            => _client.UpdateDisplayName(Session.AccessToken, displayName, Wrap(onResult));

        public void RefreshSession(Action<AuthResult> onResult)
        {
            if (string.IsNullOrEmpty(Session.RefreshToken))
            { onResult?.Invoke(AuthResult.Fail(AuthError.InvalidCredentials, "No session to refresh.")); return; }
            _client.RefreshSession(Session.RefreshToken, Wrap(onResult));
        }

        public void GetUser(Action<AuthResult> onResult)
            => _client.GetUser(Session.AccessToken, Wrap(onResult));

        /// <summary>
        /// Phase 2b OAuth. MOCK transport delegates to the mock client (returns "coming soon" unless
        /// SimulateOAuthSuccess). LIVE transport opens the provider's consent page in the system browser;
        /// completion arrives asynchronously via <see cref="OnDeepLink"/> when Supabase redirects back to
        /// <c>Config.oauthRedirect</c>. Only one OAuth attempt is tracked at a time.
        /// </summary>
        public void SignInWithOAuth(OAuthProvider provider, Action<AuthResult> onResult)
        {
            if (_useMock) { _client.SignInWithOAuth(provider, Wrap(onResult)); return; }

            _pendingOAuth = onResult;
            string url = OAuthUrlBuilder.Authorize(Config, provider);
            Debug.Log($"[AuthService] Opening OAuth ({provider}) in browser: {url}");
            Application.OpenURL(url);
            // Resolved later in OnDeepLink. NOTE(Phase 2b polish): if the user backs out of the browser
            // without completing, no deep-link fires and _pendingOAuth stays set until the next attempt —
            // a focus-regained timeout could surface a "cancelled" result; deferred.
        }

        private void OnDeepLink(string url)
        {
            if (!OAuthCallbackParser.IsCallback(url, Config)) return;

            AuthResult tokens = OAuthCallbackParser.Parse(url);
            var pending = _pendingOAuth;
            _pendingOAuth = null;

            if (!tokens.Success)
            {
                pending?.Invoke(tokens);
                return;
            }

            // Establish the session from the redirect tokens, then resolve the profile (email/display_name)
            // so the caller can route first-login → Create Username vs returning → Home.
            Session.ApplyFrom(tokens);
            Session.Save();
            _client.GetUser(tokens.AccessToken, userResult =>
            {
                if (userResult != null && userResult.Success)
                {
                    Session.ApplyFrom(userResult);
                    Session.Save();
                }
                // Return a combined success carrying the session + resolved user.
                var final = AuthResult.Ok(userResult != null ? userResult.User : null,
                    tokens.AccessToken, tokens.RefreshToken, tokens.ExpiresAtUnix, "Signed in.");
                pending?.Invoke(final);
            });
        }

        public void SignOut()
        {
            Session.Clear();
            AuthFlowState.Clear();
        }

        // ── session side-effects ──────────────────────────────────────────────────
        private Action<AuthResult> Wrap(Action<AuthResult> onResult) => result =>
        {
            if (result != null && result.Success)
            {
                Session.ApplyFrom(result);
                Session.Save();
            }
            onResult?.Invoke(result);
        };
    }
}
