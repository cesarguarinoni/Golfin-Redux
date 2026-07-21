// Order: login_signup_screens — Phase 1 (UI only, no backend)
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using GolfinRedux.UI;

namespace Golfin.UI.Account
{
    /// <summary>
    /// Sign Up screen controller — Phase 1 (UI shell).
    /// Implements client-side password checklist and show/hide toggle.
    /// No UnityWebRequest, no HTTP, no Supabase.
    /// </summary>
    public class SignUpScreenController : MonoBehaviour
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

        [Header("Password Rules")]
        [SerializeField] private Image _ruleLen8Icon;
        [SerializeField] private TextMeshProUGUI _ruleLen8Text;
        [SerializeField] private Image _ruleLowerIcon;
        [SerializeField] private TextMeshProUGUI _ruleLowerText;
        [SerializeField] private Image _ruleUpperIcon;
        [SerializeField] private TextMeshProUGUI _ruleUpperText;
        [SerializeField] private Image _ruleDigitIcon;
        [SerializeField] private TextMeshProUGUI _ruleDigitText;
        [SerializeField] private Image _ruleSpecialIcon;
        [SerializeField] private TextMeshProUGUI _ruleSpecialText;

        [Header("Rule Sprites")]
        [SerializeField] private Sprite _crossSprite;
        [SerializeField] private Sprite _tickSprite;

        [Header("Buttons")]
        [SerializeField] private Button _createButton;
        [SerializeField] private Button _googleButton;
        [SerializeField] private Button _appleButton;
        [SerializeField] private Button _cancelButton;
        [SerializeField] private Button _loginHereButton;   // footer link

        private static readonly Color GreenColor = new Color(34f/255f, 184f/255f, 0f, 1f); // #22B800
        private static readonly Color WhiteColor = Color.white;

        private bool _passwordVisible;

        private void Awake()
        {
            _passwordVisible = false;
            if (_passwordInput != null)
                _passwordInput.contentType = TMP_InputField.ContentType.Password;
        }

        private void OnEnable()
        {
            if (_passwordInput     != null) _passwordInput.onValueChanged.AddListener(OnPasswordChanged);
            if (_eyeToggleButton   != null) _eyeToggleButton.onClick.AddListener(OnEyeToggle);
            if (_createButton      != null) _createButton.onClick.AddListener(OnCreateClicked);
            if (_googleButton      != null) _googleButton.onClick.AddListener(OnGoogleClicked);
            if (_appleButton       != null) _appleButton.onClick.AddListener(OnAppleClicked);
            if (_cancelButton      != null) _cancelButton.onClick.AddListener(OnCancelClicked);
            if (_loginHereButton   != null) _loginHereButton.onClick.AddListener(OnLoginHereClicked);

            // Reset checklist to unmet state
            UpdateChecklist(PasswordRequirements.Check(""));
        }

        private void OnDisable()
        {
            if (_passwordInput     != null) _passwordInput.onValueChanged.RemoveListener(OnPasswordChanged);
            if (_eyeToggleButton   != null) _eyeToggleButton.onClick.RemoveListener(OnEyeToggle);
            if (_createButton      != null) _createButton.onClick.RemoveListener(OnCreateClicked);
            if (_googleButton      != null) _googleButton.onClick.RemoveListener(OnGoogleClicked);
            if (_appleButton       != null) _appleButton.onClick.RemoveListener(OnAppleClicked);
            if (_cancelButton      != null) _cancelButton.onClick.RemoveListener(OnCancelClicked);
            if (_loginHereButton   != null) _loginHereButton.onClick.RemoveListener(OnLoginHereClicked);
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

        // ── Password checklist ────────────────────────────────────────────────
        private void OnPasswordChanged(string value)
        {
            UpdateChecklist(PasswordRequirements.Check(value));
        }

        private void UpdateChecklist(PasswordRequirements.Result r)
        {
            ApplyRule(_ruleLen8Icon,    _ruleLen8Text,    r.Len8);
            ApplyRule(_ruleLowerIcon,   _ruleLowerText,   r.HasLower);
            ApplyRule(_ruleUpperIcon,   _ruleUpperText,   r.HasUpper);
            ApplyRule(_ruleDigitIcon,   _ruleDigitText,   r.HasDigit);
            ApplyRule(_ruleSpecialIcon, _ruleSpecialText, r.HasSpecial);
        }

        private void ApplyRule(Image icon, TextMeshProUGUI label, bool met)
        {
            if (icon != null)
            {
                icon.sprite = met ? _tickSprite : _crossSprite;
                icon.color  = met ? GreenColor  : WhiteColor;
            }
            if (label != null)
                label.color = met ? GreenColor : WhiteColor;
        }

        // ── Navigation / stubs ───────────────────────────────────────────────
        private void OnCreateClicked()
        {
            // TODO(Phase 2 — GPS/Supabase): auth.signUp({email, password})
            // Placeholder: advance to EmailConfirmation screen
            Debug.Log("[SignUpScreen] CREATE tapped — Phase 2 stub");
            if (_screenManager != null)
                _screenManager.ShowScreen(ScreenId.EmailConfirmation);
        }

        private void OnGoogleClicked()
        {
            // TODO(Phase 2 — GPS/Supabase): auth.signInWithOAuth({provider:'google'})
            Debug.Log("[SignUpScreen] Google signup tapped — Phase 2 stub");
        }

        private void OnAppleClicked()
        {
            // TODO(Phase 2 — GPS/Supabase): auth.signInWithOAuth({provider:'apple'})
            Debug.Log("[SignUpScreen] Apple signup tapped — Phase 2 stub");
        }

        private void OnCancelClicked()
        {
            if (_screenManager != null)
                _screenManager.ShowScreen(ScreenId.Login);
        }

        private void OnLoginHereClicked()
        {
            if (_screenManager != null)
                _screenManager.ShowScreen(ScreenId.Login);
        }
    }
}
