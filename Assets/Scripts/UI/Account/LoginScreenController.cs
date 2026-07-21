// Order: login_signup_screens — Phase 1 (UI only, no backend)
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using GolfinRedux.UI;

namespace Golfin.UI.Account
{
    /// <summary>
    /// Login screen controller — Phase 1 (UI shell).
    /// All auth handlers are clearly-marked TODO(Phase 2) stubs.
    /// No UnityWebRequest, no HTTP, no Supabase.
    /// </summary>
    public class LoginScreenController : MonoBehaviour
    {
        [Header("ScreenManager")]
        [SerializeField] private ScreenManager _screenManager;

        [Header("Input Fields")]
        [SerializeField] private TMP_InputField _emailInput;
        [SerializeField] private TMP_InputField _passwordInput;

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

        // ── Navigation / stubs ───────────────────────────────────────────────
        private void OnLoginClicked()
        {
            // TODO(Phase 2 — GPS/Supabase): auth.signInWithPassword({email, password})
            // Placeholder: advance to CreateUsername to demo first-login flow
            Debug.Log("[LoginScreen] LOGIN tapped — Phase 2 stub (would authenticate)");
            if (_screenManager != null)
                _screenManager.ShowScreen(ScreenId.CreateUsername);
        }

        private void OnForgotPasswordClicked()
        {
            // TODO(Phase 2 — GPS/Supabase): auth.resetPasswordForEmail(email)
            Debug.Log("[LoginScreen] Forgot Password tapped — Phase 2 stub");
        }

        private void OnGoogleClicked()
        {
            // TODO(Phase 2 — GPS/Supabase): auth.signInWithOAuth({provider:'google'})
            Debug.Log("[LoginScreen] Google login tapped — Phase 2 stub");
        }

        private void OnAppleClicked()
        {
            // TODO(Phase 2 — GPS/Supabase): auth.signInWithOAuth({provider:'apple'})
            Debug.Log("[LoginScreen] Apple login tapped — Phase 2 stub");
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
