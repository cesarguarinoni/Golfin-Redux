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

            bool useMock = Config.useMockTransport || Config.LiveButUnconfigured;
            if (Config.LiveButUnconfigured)
                Debug.LogWarning("[AuthService] Live transport selected but anonKey is empty — falling back to MOCK. See PHASE2_SERVER_SETUP_FOR_KEN.md.");

            _client = useMock
                ? (ISupabaseAuthClient)new MockSupabaseAuthClient()
                : new SupabaseAuthClient(Config, this);

            Session.Load();
            Debug.Log($"[AuthService] Ready — transport={(useMock ? "MOCK" : "LIVE")}, authenticated={Session.IsAuthenticated}.");
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

        public void SignInWithOAuth(OAuthProvider provider, Action<AuthResult> onResult)
            => _client.SignInWithOAuth(provider, Wrap(onResult));

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
