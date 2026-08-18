// Order: login_signup_screens — Phase 1 (UI only, no backend)
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using GolfinRedux.UI;
using Golfin.Auth;

namespace Golfin.UI.Account
{
    /// <summary>
    /// Create Username screen controller — Phase 2 (mockable auth).
    /// CREATE validates the username then calls <see cref="AuthService"/>.UpdateDisplayName
    /// (Supabase user_metadata.display_name); on success advances to Home.
    /// </summary>
    public class CreateUsernameScreenController : MonoBehaviour
    {
        [Header("ScreenManager")]
        [SerializeField] private ScreenManager _screenManager;

        [Header("Input")]
        [SerializeField] private TMP_InputField _usernameInput;

        [Header("Buttons")]
        [SerializeField] private Button _createButton;
        [SerializeField] private Button _cancelButton;

        [Header("Feedback (optional)")]
        [Tooltip("Optional TMP label shown for validation / auth messages. Safe to leave unset.")]
        [SerializeField] private TextMeshProUGUI _errorLabel;


        private bool _busy;

        private void Awake()
        {
            // Mobile: type directly into the field — hide the OS keyboard's own input bar,
            // which otherwise receives the text and only forwards it on commit.
            if (_usernameInput != null) _usernameInput.shouldHideMobileInput = true;
        }

        private void OnEnable()
        {
            ClearError();
            if (_createButton != null) _createButton.onClick.AddListener(OnCreateClicked);
            if (_cancelButton != null) _cancelButton.onClick.AddListener(OnCancelClicked);
        }

        private void OnDisable()
        {
            if (_createButton != null) _createButton.onClick.RemoveListener(OnCreateClicked);
            if (_cancelButton != null) _cancelButton.onClick.RemoveListener(OnCancelClicked);
        }

        private void OnCreateClicked()
        {
            if (_busy) return;
            string username = _usernameInput != null ? _usernameInput.text.Trim() : "";
            if (!UsernameRules.IsValid(username))
            { SetError(UsernameRules.Requirement); return; }

            SetBusy(true);
            AuthService.Instance.UpdateDisplayName(username, result =>
            {
                SetBusy(false);
                if (result.Success)
                {
                    AccountUiBridge.SyncUsername();
                    if (_screenManager != null) _screenManager.ShowScreen(ScreenId.Home);
                }
                else SetError(result.Message);
            });
        }

        private void OnCancelClicked()
        {
            if (_screenManager != null)
                _screenManager.ShowScreen(ScreenId.Login);
        }

        private void SetBusy(bool busy)
        {
            _busy = busy;
            if (_createButton != null) _createButton.interactable = !busy;
        }

        private static readonly Color ErrColor = new Color(0.898f, 0.282f, 0.302f); // #E5484D

        private void SetError(string message)
        {
            if (_errorLabel != null)
            {
                bool has = !string.IsNullOrEmpty(message);
                _errorLabel.gameObject.SetActive(has);
                _errorLabel.text  = message ?? "";
                _errorLabel.color = ErrColor;
            }
            if (!string.IsNullOrEmpty(message)) Debug.Log($"[CreateUsernameScreen] {message}");
        }

        private void ClearError()
        {
            if (_errorLabel != null) _errorLabel.gameObject.SetActive(false);
        }
    }
}
