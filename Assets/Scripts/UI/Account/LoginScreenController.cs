// Order: login_signup_screens — Phase 1 (UI only, no backend)
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using GolfinRedux.UI;
using Golfin.Auth;
using Golfin.Roster;

namespace Golfin.UI.Account
{
    /// <summary>
    /// Login screen controller — Phase 2 (mockable auth).
    /// Handlers call <see cref="AuthService"/>; the transport is a mock until Ken supplies the anon key.
    /// OAuth (Google/Apple) is wired to the seam but returns "coming soon" until Phase 2b.
    /// </summary>
    public class LoginScreenController : MonoBehaviour
    {
        [Header("ScreenManager")]
        [SerializeField] private ScreenManager _screenManager;

        [Header("Input Fields")]
        [SerializeField] private TMP_InputField _emailInput;
        [SerializeField] private TMP_InputField _passwordInput;

        [Header("Feedback (optional)")]
        [Tooltip("Optional TMP label shown for auth errors / messages. Safe to leave unset.")]
        [SerializeField] private TextMeshProUGUI _errorLabel;

        private bool _busy;

        [Header("Eye Toggle")]
        [SerializeField] private Button _eyeToggleButton;
        [SerializeField] private Image  _eyeIcon;
        [SerializeField] private Sprite _eyeShowSprite;
        [SerializeField] private Sprite _eyeHideSprite;

        [Header("Buttons")]
        [SerializeField] private Button _loginButton;
        [SerializeField] private Button _forgotPasswordButton;
        [SerializeField] private Button _googleButton;
        [SerializeField] private Button _appleButton;
        [SerializeField] private Button _cancelButton;
        [SerializeField] private Button _createAccountButton;   // footer link

        private bool _passwordVisible;

        private void Awake()
        {
            _passwordVisible = false;
            if (_passwordInput != null)
                _passwordInput.contentType = TMP_InputField.ContentType.Password;

            // Mobile: type directly into the fields — hide the OS keyboard's own input bar.
            if (_emailInput    != null) _emailInput.shouldHideMobileInput    = true;
            if (_passwordInput != null) _passwordInput.shouldHideMobileInput = true;
        }

        private void OnEnable()
        {
            ClearError();

            // auth_recovery_flow — cold-start recovery: the deep link fired before any screen was
            // listening, so this door checks for held tokens (→ set-new-password) or a rejected
            // link (→ localized error) on the way in.
            if (AuthService.Instance.PendingRecovery != null)
            {
                if (_screenManager != null) _screenManager.ShowScreen(ScreenId.ResetPassword);
                return; // leaving immediately — skip listener wiring (OnDisable removals are no-ops)
            }
            var recoveryFailure = AuthService.Instance.ConsumeRecoveryFailure();
            if (recoveryFailure != null)
                SetError(LocalizationManager.Get("AUTH_RESET_LINK_EXPIRED"));

            AuthService.PasswordRecovery += OnPasswordRecoveryWhileOpen;
            if (_loginButton          != null) _loginButton.onClick.AddListener(OnLoginClicked);
            if (_forgotPasswordButton != null) _forgotPasswordButton.onClick.AddListener(OnForgotPasswordClicked);
            if (_googleButton         != null) _googleButton.onClick.AddListener(OnGoogleClicked);
            if (_appleButton          != null) _appleButton.onClick.AddListener(OnAppleClicked);
            if (_cancelButton         != null) _cancelButton.onClick.AddListener(OnCancelClicked);
            if (_createAccountButton  != null) _createAccountButton.onClick.AddListener(OnCreateAccountClicked);
            if (_eyeToggleButton      != null) _eyeToggleButton.onClick.AddListener(OnEyeToggle);
        }

        private void OnDisable()
        {
            AuthService.PasswordRecovery -= OnPasswordRecoveryWhileOpen;
            if (_loginButton          != null) _loginButton.onClick.RemoveListener(OnLoginClicked);
            if (_forgotPasswordButton != null) _forgotPasswordButton.onClick.RemoveListener(OnForgotPasswordClicked);
            if (_googleButton         != null) _googleButton.onClick.RemoveListener(OnGoogleClicked);
            if (_appleButton          != null) _appleButton.onClick.RemoveListener(OnAppleClicked);
            if (_cancelButton         != null) _cancelButton.onClick.RemoveListener(OnCancelClicked);
            if (_createAccountButton  != null) _createAccountButton.onClick.RemoveListener(OnCreateAccountClicked);
            if (_eyeToggleButton      != null) _eyeToggleButton.onClick.RemoveListener(OnEyeToggle);
        }

        // auth_recovery_flow — expired/used reset link tapped while this screen is already open:
        // ScreenManager's ShowScreen(Login) dedupes to a no-op in that case, so the error surfaces
        // here. Success routing stays ScreenManager's job.
        private void OnPasswordRecoveryWhileOpen(AuthResult r)
        {
            if (r == null || r.Success) return;
            AuthService.Instance.ConsumeRecoveryFailure(); // consumed — don't re-show on next enable
            SetError(LocalizationManager.Get("AUTH_RESET_LINK_EXPIRED"));
        }

        // ── Eye toggle ────────────────────────────────────────────────────────
        private void OnEyeToggle()
        {
            _passwordVisible = !_passwordVisible;
            if (_passwordInput != null)
            {
                _passwordInput.contentType = _passwordVisible
                    ? TMP_InputField.ContentType.Standard
                    : TMP_InputField.ContentType.Password;
                _passwordInput.ForceLabelUpdate();
            }
            if (_eyeIcon != null)
                _eyeIcon.sprite = _passwordVisible ? _eyeHideSprite : _eyeShowSprite;
        }

        // ── Auth handlers ─────────────────────────────────────────────────────
        private void OnLoginClicked()
        {
            if (_busy) return;
            string email = _emailInput != null ? _emailInput.text.Trim() : "";
            string pw    = _passwordInput != null ? _passwordInput.text : "";
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(pw))
            { SetError(LocalizationManager.Get("AUTH_LOGIN_ERR_MISSING_FIELDS")); return; }

            SetBusy(true);
            AuthService.Instance.SignInWithPassword(email, pw, result =>
            {
                SetBusy(false);
                if (result.Success)
                {
                    AccountUiBridge.SyncUsername();
                    // starting_character_selection: check NeedsStarter before landing on Home.
                    if (CharacterManager.Instance != null && CharacterManager.Instance.NeedsStarter)
                    {
                        if (_screenManager != null) _screenManager.ShowScreen(ScreenId.StartingCharacterSelection);
                    }
                    else
                    {
                        var target = AuthService.Instance.Session.HasDisplayName ? ScreenId.Home : ScreenId.CreateUsername;
                        if (_screenManager != null) _screenManager.ShowScreen(target);
                    }
                }
                else if (result.Error == AuthError.EmailNotConfirmed)
                {
                    AuthFlowState.PendingEmail = email;
                    SetError(result.Message);
                    if (_screenManager != null) _screenManager.ShowScreen(ScreenId.EmailConfirmation);
                }
                else SetError(result.Message);
            });
        }

        private void OnForgotPasswordClicked()
        {
            if (_busy) return;
            string email = _emailInput != null ? _emailInput.text.Trim() : "";
            if (string.IsNullOrEmpty(email)) { SetError(LocalizationManager.Get("AUTH_LOGIN_ERR_MISSING_EMAIL")); return; }
            SetBusy(true);
            AuthService.Instance.RequestPasswordReset(email, result =>
            {
                SetBusy(false);
                if (result.Success) SetError(LocalizationManager.Get("AUTH_LOGIN_RESET_SENT"), isError: false);
                else SetError(result.Message);
            });
        }

        private void OnGoogleClicked() => StartOAuth(OAuthProvider.Google);
        private void OnAppleClicked()  => StartOAuth(OAuthProvider.Apple);

        private void StartOAuth(OAuthProvider provider)
        {
            if (_busy) return;
            SetBusy(true);
            AuthService.Instance.SignInWithOAuth(provider, result =>
            {
                SetBusy(false);
                if (result.Success)
                {
                    AccountUiBridge.SyncUsername();
                    // starting_character_selection: check NeedsStarter before landing on Home.
                    if (CharacterManager.Instance != null && CharacterManager.Instance.NeedsStarter)
                    {
                        if (_screenManager != null) _screenManager.ShowScreen(ScreenId.StartingCharacterSelection);
                    }
                    else
                    {
                        var target = AuthService.Instance.Session.HasDisplayName ? ScreenId.Home : ScreenId.CreateUsername;
                        if (_screenManager != null) _screenManager.ShowScreen(target);
                    }
                }
                else SetError(result.Message); // "coming soon" until Phase 2b providers are enabled
            });
        }

        // ── Feedback helpers ──────────────────────────────────────────────────
        private static readonly Color ErrColor = new Color(0.898f, 0.282f, 0.302f); // #E5484D
        private static readonly Color OkColor  = new Color(34f/255f, 184f/255f, 0f);  // #22B800

        private void SetBusy(bool busy)
        {
            _busy = busy;
            if (_loginButton != null) _loginButton.interactable = !busy;
        }

        private void SetError(string message, bool isError = true)
        {
            if (_errorLabel != null)
            {
                bool has = !string.IsNullOrEmpty(message);
                _errorLabel.gameObject.SetActive(has);
                _errorLabel.text  = message ?? "";
                _errorLabel.color = isError ? ErrColor : OkColor;
            }
            if (!string.IsNullOrEmpty(message)) Debug.Log($"[LoginScreen] {message}");
        }

        private void ClearError()
        {
            if (_errorLabel != null) _errorLabel.gameObject.SetActive(false);
        }

        private void OnCancelClicked()
        {
            if (_screenManager != null)
                _screenManager.ShowScreen(ScreenId.Splash);
        }

        private void OnCreateAccountClicked()
        {
            if (_screenManager != null)
                _screenManager.ShowScreen(ScreenId.SignUp);
        }
    }
}
