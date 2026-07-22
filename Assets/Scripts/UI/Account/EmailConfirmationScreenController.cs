// Order: login_signup_screens — Phase 1 (UI only, no backend)
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using GolfinRedux.UI;
using Golfin.Auth;

namespace Golfin.UI.Account
{
    /// <summary>
    /// Email Confirmation screen controller — Phase 2 (mockable auth).
    /// Shows the address carried from Sign Up (<see cref="AuthFlowState.PendingEmail"/>); Resend calls
    /// <see cref="AuthService"/>.ResendConfirmation; Open-Email launches the device mail app.
    /// </summary>
    public class EmailConfirmationScreenController : MonoBehaviour
    {
        [Header("ScreenManager")]
        [SerializeField] private ScreenManager _screenManager;

        [Header("UI Text")]
        [SerializeField] private TextMeshProUGUI _emailAddressLabel;  // shows the submitted email

        [Header("Buttons")]
        [SerializeField] private Button _openEmailButton;     // "Open Email App" CTA
        [SerializeField] private Button _resendEmailButton;   // "Resend Email" link
        [SerializeField] private Button _backToLoginButton;   // "Back to Login" link

        [Header("Feedback (optional)")]
        [Tooltip("Optional TMP label shown for resend confirmation / errors. Safe to leave unset.")]
        [SerializeField] private TextMeshProUGUI _errorLabel;

        // Email carried from Sign Up via AuthFlowState; falls back to a placeholder if entered directly.
        private string _pendingEmail = "your-email@example.com";
        private bool _busy;

        /// <summary>
        /// Called by the coordinator (Phase 2) to show the actual submitted email address.
        /// </summary>
        public void SetPendingEmail(string email)
        {
            _pendingEmail = email;
            RefreshEmailLabel();
        }

        private void OnEnable()
        {
            if (_errorLabel != null) _errorLabel.gameObject.SetActive(false);
            // Pick up the email carried from Sign Up (if any).
            if (!string.IsNullOrEmpty(AuthFlowState.PendingEmail))
                _pendingEmail = AuthFlowState.PendingEmail;
            RefreshEmailLabel();

            if (_openEmailButton   != null) _openEmailButton.onClick.AddListener(OnOpenEmailClicked);
            if (_resendEmailButton != null) _resendEmailButton.onClick.AddListener(OnResendEmailClicked);
            if (_backToLoginButton != null) _backToLoginButton.onClick.AddListener(OnBackToLoginClicked);
        }

        private void OnDisable()
        {
            if (_openEmailButton   != null) _openEmailButton.onClick.RemoveListener(OnOpenEmailClicked);
            if (_resendEmailButton != null) _resendEmailButton.onClick.RemoveListener(OnResendEmailClicked);
            if (_backToLoginButton != null) _backToLoginButton.onClick.RemoveListener(OnBackToLoginClicked);
        }

        private void RefreshEmailLabel()
        {
            if (_emailAddressLabel != null)
                _emailAddressLabel.text = _pendingEmail;
        }

        // ── Button handlers ──────────────────────────────────────────────────
        private void OnOpenEmailClicked()
        {
            // Launch the device's default mail app.
            Application.OpenURL("mailto:");
        }

        private void OnResendEmailClicked()
        {
            if (_busy) return;
            SetBusy(true);
            AuthService.Instance.ResendConfirmation(_pendingEmail, result =>
            {
                SetBusy(false);
                SetMessage(result.Success ? "Confirmation email re-sent." : result.Message, isError: !result.Success);
            });
        }

        private void SetBusy(bool busy)
        {
            _busy = busy;
            if (_resendEmailButton != null) _resendEmailButton.interactable = !busy;
        }

        private static readonly Color ErrColor = new Color(0.898f, 0.282f, 0.302f); // #E5484D
        private static readonly Color OkColor  = new Color(34f/255f, 184f/255f, 0f);  // #22B800

        private void SetMessage(string message, bool isError)
        {
            if (_errorLabel != null)
            {
                bool has = !string.IsNullOrEmpty(message);
                _errorLabel.gameObject.SetActive(has);
                _errorLabel.text  = message ?? "";
                _errorLabel.color = isError ? ErrColor : OkColor;
            }
            if (!string.IsNullOrEmpty(message)) Debug.Log($"[EmailConfirmation] {message}");
        }

        private void OnBackToLoginClicked()
        {
            if (_screenManager != null)
                _screenManager.ShowScreen(ScreenId.Login);
        }
    }
}
