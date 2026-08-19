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

        /// <summary>
        /// Boot-time bootstrap: deep links (OAuth redirects) can arrive at any moment after launch —
        /// including a cold start caused BY the deep link — so the deepLinkActivated subscription must
        /// exist from frame one. Lazy Instance creation left a window where the URL arrived with no
        /// listener and was silently lost (observed on iOS, 2026-08-11). This forces creation at startup;
        /// Initialize() then also sweeps Application.absoluteURL for a cold-start URL.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void BootstrapAtStartup()
        {
            var _ = Instance; // touch to create + hook deep links
            Debug.Log("[AuthService] Bootstrapped at app start (deep-link listener live).");
        }

        public SupabaseConfig Config { get; private set; }
        public AuthSession Session { get; private set; } = new AuthSession();

        /// <summary>
        /// Fires whenever a call ESTABLISHES an authenticated session — password sign-in, sign-up,
        /// OAuth deep-link completion, or a token refresh. Not fired for calls that merely read the
        /// profile, and not fired on sign-out.
        ///
        /// Added for rp_balance_sync §3.3: the first server balance of a session has to be fetched at
        /// the moment there is a token to fetch it with, and polling for one was explicitly ruled out.
        /// STATIC on purpose — subscribers (the balance sync bridge) come up on their own schedule and
        /// must not have to win a race against <see cref="Instance"/> being created.
        /// </summary>
        public static event Action<AuthSession> SignedIn;

        /// <summary>
        /// auth_recovery_flow — fired when a password-recovery deep link arrives. Success=true means
        /// recovery tokens are HELD in <see cref="PendingRecovery"/> (session NOT persisted, SignedIn
        /// NOT raised) and the UI should route to the set-new-password screen; Success=false means the
        /// link was expired/already used and the UI should surface a localized failure. STATIC like
        /// <see cref="SignedIn"/> and for the same reason: the UI layer subscribes on its own schedule.
        /// </summary>
        public static event Action<AuthResult> PasswordRecovery;

        /// <summary>
        /// Recovery tokens parsed from a <c>type=recovery</c> deep link, held IN MEMORY ONLY until
        /// <see cref="UpdatePasswordWithRecovery"/> succeeds (then applied + saved) or
        /// <see cref="CancelPasswordRecovery"/> clears them. Never written to <see cref="Session"/>
        /// or PlayerPrefs before the new password is set — that early persist was the silent-sign-in
        /// bug this task removes. (AuthSession's shape persists on ApplyFrom+Save; holding an
        /// un-persisted AuthResult sidesteps that without touching AuthSession.)
        /// </summary>
        public AuthResult PendingRecovery { get; private set; }

        /// <summary>
        /// Cold-start seam: a recovery-link FAILURE (expired/used) that fired before any UI was
        /// listening. The Login screen consumes it on enable to show the localized error.
        /// </summary>
        private AuthResult _recoveryFailure;

        /// <summary>Returns the un-surfaced recovery failure (if any) and clears it.</summary>
        public AuthResult ConsumeRecoveryFailure()
        {
            var f = _recoveryFailure;
            _recoveryFailure = null;
            return f;
        }

        /// <summary>Player backed out of the set-new-password screen — drop the held tokens so the
        /// Login screen stops re-routing to it.</summary>
        public void CancelPasswordRecovery() => PendingRecovery = null;

        private ISupabaseAuthClient _client;
        private bool _useMock;

        /// <summary>
        /// True when this session runs on the MOCK auth transport (editor dev, unconfigured
        /// builds). Server-side checks that need a REAL bearer token — the username uniqueness
        /// claim, for one — skip themselves on a mock session instead of failing on a token the
        /// server has never seen.
        /// </summary>
        public bool IsMockTransport => _useMock;

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

        /// <summary>Test hook: inject a client + fresh session directly (bypasses Resources/scene).
        /// Also guarantees a Config in EDIT MODE, where Awake/Initialize never ran (no ExecuteAlways):
        /// without one, IsCallback rejects every deep link and the recovery tests test nothing.</summary>
        public void ConfigureForTest(ISupabaseAuthClient client, AuthSession session = null)
        {
            _client = client;
            Session = session ?? new AuthSession();
            if (Config == null)
                Config = ScriptableObject.CreateInstance<SupabaseConfig>(); // defaults carry golfin://auth-callback
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

        /// <summary>
        /// auth_recovery_flow — set the new password using the HELD recovery tokens (never
        /// <see cref="Session"/>'s). Deliberately NOT routed through Wrap: the recovery session
        /// becomes the real, persisted session only AFTER the server accepts the new password.
        /// On success: apply tokens + user, save, raise <see cref="SignedIn"/>.
        /// </summary>
        public void UpdatePasswordWithRecovery(string newPassword, Action<AuthResult> onResult)
        {
            var recovery = PendingRecovery;
            if (recovery == null || !recovery.HasSession)
            { onResult?.Invoke(AuthResult.Fail(AuthError.InvalidCredentials, "No recovery session. Please open the reset link again.")); return; }

            _client.UpdatePassword(recovery.AccessToken, newPassword, result =>
            {
                if (result == null || !result.Success) { onResult?.Invoke(result); return; }

                PendingRecovery = null;
                Session.ApplyFrom(recovery);   // tokens
                Session.ApplyFrom(result);     // PUT /user echoes the user (email / display_name)
                Session.Save();
                RaiseSignedIn();
                onResult?.Invoke(AuthResult.Ok(result.User, recovery.AccessToken, recovery.RefreshToken,
                    recovery.ExpiresAtUnix, "Password updated."));
            });
        }

        /// <summary>Test seam for the deep-link entry point (auth_recovery_flow §6 —
        /// recovery-does-not-persist-before-update is asserted through here).</summary>
        public void HandleAuthCallback(string url) => OnDeepLink(url);

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
            // Resolved later in OnDeepLink, or failed by the focus-regained timeout below if the user
            // returns to the app without a deep link ever arriving (backed out / redirect lost).
        }

        /// <summary>
        /// Focus-regained watchdog: if we're back in the foreground with an OAuth attempt still pending
        /// and no deep link arrives within a grace period, fail the attempt so the UI unlocks. The grace
        /// period matters because on some platforms focus returns BEFORE deepLinkActivated fires.
        /// </summary>
        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus || _pendingOAuth == null) return;
            Debug.Log("[AuthService] Focus regained with OAuth pending — starting 8s deep-link watchdog.");
            StartCoroutine(OAuthWatchdog());
        }

        private System.Collections.IEnumerator OAuthWatchdog()
        {
            float deadline = Time.realtimeSinceStartup + 8f;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (_pendingOAuth == null) yield break; // deep link resolved it
                yield return null;
            }
            if (_pendingOAuth == null) yield break;
            Debug.LogWarning("[AuthService] OAuth watchdog fired — no deep link received after focus regained.");
            var pending = _pendingOAuth;
            _pendingOAuth = null;
            pending?.Invoke(AuthResult.Fail(AuthError.Unknown,
                "Sign-in didn't complete. Please try again."));
        }

        private void OnDeepLink(string url)
        {
            // Diagnostic: log arrival without leaking tokens (prefix only, fragment stripped).
            int cut = url != null ? url.IndexOfAny(new[] { '#', '?' }) : -1;
            Debug.Log($"[AuthService] Deep link received: {(cut >= 0 ? url.Substring(0, cut) : url)} " +
                      $"(len={url?.Length ?? 0}, hasFragment={url != null && url.Contains("#")}, pending={_pendingOAuth != null})");

            if (!OAuthCallbackParser.IsCallback(url, Config))
            {
                Debug.LogWarning("[AuthService] Deep link did NOT match the configured oauthRedirect — ignored.");
                return;
            }

            AuthResult tokens = OAuthCallbackParser.Parse(url);
            var info = OAuthCallbackParser.GetCallbackInfo(url);
            Debug.Log($"[AuthService] Callback parsed: success={tokens.Success}, hasSession={tokens.HasSession}, type={info.Type ?? "-"}" +
                      $"{(tokens.Success ? "" : $", error={tokens.Error}: {tokens.Message}")}");
            var pending = _pendingOAuth;
            _pendingOAuth = null;

            if (!tokens.Success)
            {
                // In-flight OAuth attempt — existing behavior, unchanged.
                if (pending != null) { pending.Invoke(tokens); return; }

                // auth_recovery_flow — an email link Supabase rejected (expired / already used,
                // e.g. error_code=otp_expired) arrives with NO pending OAuth. Surface it instead
                // of dropping it silently; never sign the player in.
                if (info.HasError)
                {
                    Debug.LogWarning($"[AuthService] Email-link callback rejected: {info.ErrorCode ?? info.Error} — {info.ErrorDescription}");
                    var failure = AuthResult.Fail(AuthError.Unknown, tokens.Message);
                    _recoveryFailure = failure;
                    RaisePasswordRecovery(failure);
                }
                return;
            }

            // auth_recovery_flow — type=recovery: HOLD the tokens and route to set-new-password.
            // No Session.ApplyFrom, no Save, no RaiseSignedIn until the new password is set —
            // the old single-branch fall-through here was the silent-sign-in bug.
            if (info.IsRecovery)
            {
                PendingRecovery = tokens;
                Debug.Log("[AuthService] Recovery deep link — tokens held, awaiting new password (no sign-in).");
                RaisePasswordRecovery(tokens);
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
                // This path never goes through Wrap (the tokens came from the redirect, not a call),
                // so it raises the event itself.
                RaiseSignedIn();
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
                if (result.HasSession) RaiseSignedIn();
            }
            onResult?.Invoke(result);
        };

        /// <summary>Announce a recovery-link outcome. A throwing subscriber must never break the
        /// deep-link handler (mirror of <see cref="RaiseSignedIn"/>).</summary>
        private static void RaisePasswordRecovery(AuthResult r)
        {
            try { PasswordRecovery?.Invoke(r); }
            catch (Exception ex) { Debug.LogError($"[AuthService] PasswordRecovery subscriber threw: {ex}"); }
        }

        /// <summary>Announce a freshly established session. A throwing subscriber must never fail the
        /// sign-in that produced it.</summary>
        private void RaiseSignedIn()
        {
            if (!Session.IsAuthenticated) return;

            try { SignedIn?.Invoke(Session); }
            catch (Exception ex) { Debug.LogError($"[AuthService] SignedIn subscriber threw: {ex}"); }
        }
    }
}
