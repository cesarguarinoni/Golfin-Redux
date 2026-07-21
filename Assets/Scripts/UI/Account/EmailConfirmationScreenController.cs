// Order: login_signup_screens — Phase 1 (UI only, no backend)
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using GolfinRedux.UI;

namespace Golfin.UI.Account
{
    /// <summary>
    /// Email Confirmation screen controller — Phase 1 (UI shell).
    /// Shown after Sign Up to prompt the user to verify their email.
    /// No UnityWebRequest, no HTTP, no Supabase.
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

        // The email address carried forward from SignUp is set by LoginFlowCoordinator in Phase 2.
        // For Phase 1 we show a placeholder.
        private string _pendingEmail = "your-email@example.com";

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
            // TODO(Phase 2 — GPS): Application.OpenURL("mailto:") or native email-app intent
            Debug.Log("[EmailConfirmation] Open Email App tapped — Phase 2 stub");
        }

        private void OnResendEmailClicked()
        {
            // TODO(Phase 2 — GPS/Supabase): auth.resend({type:'signup', email})
            Debug.Log("[EmailConfirmation] Resend Email tapped — Phase 2 stub");
        }

        private void OnBackToLoginClicked()
        {
            if (_screenManager != null)
                _screenManager.ShowScreen(ScreenId.Login);
        }
    }
}
