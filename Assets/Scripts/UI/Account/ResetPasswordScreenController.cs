// Order: auth_recovery_flow — set-new-password screen (reached from a type=recovery deep link)
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using GolfinRedux.UI;
using Golfin.Auth;

namespace Golfin.UI.Account
{
    /// <summary>
    /// Set-new-password screen — the landing point of a password-reset deep link
    /// (<c>type=recovery</c>). ScreenManager-routed like <see cref="LoginScreenController"/> (same
    /// pattern on purpose; the account flow has no ModalController overlays).
    ///
    /// The recovery tokens live in <see cref="AuthService.PendingRecovery"/>, held in memory only:
    /// nothing is persisted and <c>SignedIn</c> is not raised until
    /// <see cref="AuthService.UpdatePasswordWithRecovery"/> succeeds. Client-side checks reuse
    /// <see cref="PasswordRequirements"/> — the same rules Sign Up enforces.
    /// </summary>
    public class ResetPasswordScreenController : MonoBehaviour
    {
        [Header("ScreenManager")]
        [SerializeField] private ScreenManager _screenManager;

        [Header("Input Fields")]
        [SerializeField] private TMP_InputField _newPasswordInput;
        [SerializeField] private TMP_InputField _confirmPasswordInput;

        [Header("Feedback (optional)")]
        [Tooltip("Optional TMP label shown for validation / auth errors. Safe to leave unset.")]
        [SerializeField] private TextMeshProUGUI _errorLabel;

        [Header("Eye Toggle")]
        [SerializeField] private Button _eyeToggleButton;
        [SerializeField] private Image  _eyeIcon;
        [SerializeField] private Sprite _eyeShowSprite;
        [SerializeField] private Sprite _eyeHideSprite;

        [Header("Buttons")]
        [SerializeField] private Button _submitButton;
        [SerializeField] private Button _backToLoginButton;

        [Header("Localized Labels (AUTH_RESET_* keys)")]
        [Tooltip("Static copy localized at OnEnable and on language change. Each ref is optional.")]
        [SerializeField] private TextMeshProUGUI _titleLabel;
        [SerializeField] private TextMeshProUGUI _newPasswordLabel;
        [SerializeField] private TextMeshProUGUI _newPasswordPlaceholder;
        [SerializeField] private TextMeshProUGUI _confirmPasswordLabel;
        [SerializeField] private TextMeshProUGUI _confirmPasswordPlaceholder;
        [SerializeField] private TextMeshProUGUI _submitLabel;
        [SerializeField] private TextMeshProUGUI _backLabel;

        private bool _busy;
        private bool _passwordVisible;

        private void Awake()
        {
            _passwordVisible = false;
            ApplyMasking();
            if (_newPasswordInput     != null) _newPasswordInput.shouldHideMobileInput     = true;
            if (_confirmPasswordInput != null) _confirmPasswordInput.shouldHideMobileInput = true;
        }

        private void OnEnable()
        {
            ApplyLocalization();
            LocalizationManager.OnLanguageChanged += ApplyLocalization;
            ClearError();
            if (_newPasswordInput     != null) _newPasswordInput.text     = "";
            if (_confirmPasswordInput != null) _confirmPasswordInput.text = "";

            // Opened without held recovery tokens (stale navigation, cancelled link): nothing can be
            // submitted — say so and leave Back as the way out.
            if (AuthService.Instance.PendingRecovery == null)
                SetError(LocalizationManager.Get("AUTH_RESET_LINK_EXPIRED"));

            if (_submitButton      != null) _submitButton.onClick.AddListener(OnSubmitClicked);
            if (_backToLoginButton != null) _backToLoginButton.onClick.AddListener(OnBackToLoginClicked);
            if (_eyeToggleButton   != null) _eyeToggleButton.onClick.AddListener(OnEyeToggle);
        }

        private void OnDisable()
        {
            LocalizationManager.OnLanguageChanged -= ApplyLocalization;
            if (_submitButton      != null) _submitButton.onClick.RemoveListener(OnSubmitClicked);
            if (_backToLoginButton != null) _backToLoginButton.onClick.RemoveListener(OnBackToLoginClicked);
            if (_eyeToggleButton   != null) _eyeToggleButton.onClick.RemoveListener(OnEyeToggle);
        }

        // ── Localization ──────────────────────────────────────────────────────
        // The account screens carry hardcoded copy in the scene; this screen binds its static
        // strings to the AUTH_RESET_* keys so EN/JA both render (SPEC §5).
        private void ApplyLocalization()
        {
            Set(_titleLabel,                 "AUTH_RESET_TITLE");
            Set(_newPasswordLabel,           "AUTH_RESET_NEW_PLACEHOLDER");
            Set(_newPasswordPlaceholder,     "AUTH_RESET_NEW_PLACEHOLDER");
            Set(_confirmPasswordLabel,       "AUTH_RESET_CONFIRM_PLACEHOLDER");
            Set(_confirmPasswordPlaceholder, "AUTH_RESET_CONFIRM_PLACEHOLDER");
            Set(_submitLabel,                "AUTH_RESET_BUTTON");
            Set(_backLabel,                  "AUTH_RESET_BACK");
        }

        private static void Set(TextMeshProUGUI label, string key)
        {
            if (label != null) label.text = LocalizationManager.Get(key);
        }

        // ── Eye toggle (one button unmasks BOTH fields — they hold the same secret) ──
        private void OnEyeToggle()
        {
            _passwordVisible = !_passwordVisible;
            ApplyMasking();
            if (_eyeIcon != null)
                _eyeIcon.sprite = _passwordVisible ? _eyeHideSprite : _eyeShowSprite;
        }

        private void ApplyMasking()
        {
            var type = _passwordVisible ? TMP_InputField.ContentType.Standard
                                        : TMP_InputField.ContentType.Password;
            if (_newPasswordInput != null)
            { _newPasswordInput.contentType = type; _newPasswordInput.ForceLabelUpdate(); }
            if (_confirmPasswordInput != null)
            { _confirmPasswordInput.contentType = type; _confirmPasswordInput.ForceLabelUpdate(); }
        }

        // ── Submit ────────────────────────────────────────────────────────────
        private void OnSubmitClicked()
        {
            if (_busy) return;
            string pw      = _newPasswordInput     != null ? _newPasswordInput.text     : "";
            string confirm = _confirmPasswordInput != null ? _confirmPasswordInput.text : "";

            if (!PasswordRequirements.Check(pw).AllMet)
            { SetError(LocalizationManager.Get("AUTH_RESET_TOO_SHORT")); return; }
            if (pw != confirm)
            { SetError(LocalizationManager.Get("AUTH_RESET_MISMATCH")); return; }

            SetBusy(true);
            AuthService.Instance.UpdatePasswordWithRecovery(pw, result =>
            {
                SetBusy(false);
                if (result != null && result.Success)
                {
                    // Only now is the session persisted + SignedIn raised (inside AuthService).
                    SetError(LocalizationManager.Get("AUTH_RESET_SUCCESS"), isError: false);
                    AccountUiBridge.SyncUsername();
                    var target = AuthService.Instance.Session.HasDisplayName ? ScreenId.Home : ScreenId.CreateUsername;
                    if (_screenManager != null) _screenManager.ShowScreen(target);
                }
                else if (result != null && result.Error == AuthError.WeakPassword)
                {
                    SetError(string.IsNullOrEmpty(result.Message)
                        ? LocalizationManager.Get("AUTH_RESET_TOO_SHORT") : result.Message);
                }
                else
                {
                    // Recovery token rejected (expired while the screen sat open) or transport error.
                    SetError(result != null && result.Error == AuthError.Network
                        ? result.Message : LocalizationManager.Get("AUTH_RESET_LINK_EXPIRED"));
                }
            });
        }

        private void OnBackToLoginClicked()
        {
            // Drop the held tokens FIRST — Login.OnEnable re-routes here whenever tokens are held.
            AuthService.Instance.CancelPasswordRecovery();
            if (_screenManager != null) _screenManager.ShowScreen(ScreenId.Login);
        }

        // ── Feedback helpers (same palette as LoginScreenController) ──────────
        private static readonly Color ErrColor = new Color(0.898f, 0.282f, 0.302f); // #E5484D
        private static readonly Color OkColor  = new Color(34f/255f, 184f/255f, 0f);  // #22B800

        private void SetBusy(bool busy)
        {
            _busy = busy;
            if (_submitButton != null) _submitButton.interactable = !busy;
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
            if (!string.IsNullOrEmpty(message)) Debug.Log($"[ResetPasswordScreen] {message}");
        }

        private void ClearError()
        {
            if (_errorLabel != null) _errorLabel.gameObject.SetActive(false);
        }
    }
}
