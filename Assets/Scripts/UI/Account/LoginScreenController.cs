// Order: login_signup_screens — Phase 1 (UI only, no backend)
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using GolfinRedux.UI;
using Golfin.Auth;

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
        }

        private void OnEnable()
        {
            ClearError();
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
            if (_loginButton          != null) _loginButton.onClick.RemoveListener(OnLoginClicked);
            if (_forgotPasswordButton != null) _forgotPasswordButton.onClick.RemoveListener(OnForgotPasswordClicked);
            if (_googleButton         != null) _googleButton.onClick.RemoveListener(OnGoogleClicked);
            if (_appleButton          != null) _appleButton.onClick.RemoveListener(OnAppleClicked);
            if (_cancelButton         != null) _cancelButton.onClick.RemoveListener(OnCancelClicked);
            if (_createAccountButton  != null) _createAccountButton.onClick.RemoveListener(OnCreateAccountClicked);
            if (_eyeToggleButton      != null) _eyeToggleButton.onClick.RemoveListener(OnEyeToggle);
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
            { SetError("Enter your email and password."); return; }

            SetBusy(true);
            AuthService.Instance.SignInWithPassword(email, pw, result =>
            {
                SetBusy(false);
                if (result.Success)
                {
                    AccountUiBridge.SyncUsername();
                    // First login (no username yet) → Create Username; otherwise → Home.
                    var target = AuthService.Instance.Session.HasDisplayName ? ScreenId.Home : ScreenId.CreateUsername;
                    if (_screenManager != null) _screenManager.ShowScreen(target);
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
            if (string.IsNullOrEmpty(email)) { SetError("Enter your email first."); return; }
            SetBusy(true);
            AuthService.Instance.RequestPasswordReset(email, result =>
            {
                SetBusy(false);
                if (result.Success) SetError("Password reset email sent.", isError: false);
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
                    var target = AuthService.Instance.Session.HasDisplayName ? ScreenId.Home : ScreenId.CreateUsername;
                    if (_screenManager != null) _screenManager.ShowScreen(target);
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
