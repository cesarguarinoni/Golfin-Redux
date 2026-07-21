// Order: login_signup_screens — Phase 1 (UI only, no backend)
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using GolfinRedux.UI;

namespace Golfin.UI.Account
{
    /// <summary>
    /// Create Username screen controller — Phase 1 (UI shell).
    /// No UnityWebRequest, no HTTP, no Supabase.
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

        private void OnEnable()
        {
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
            // TODO(Phase 2 — GPS/Supabase): set profile display_name = _usernameInput.text;
            // Placeholder: advance to Home to demo the first-login complete step
            Debug.Log($"[CreateUsernameScreen] CREATE tapped with username='{_usernameInput?.text}' — Phase 2 stub");
        }

        private void OnCancelClicked()
        {
            if (_screenManager != null)
                _screenManager.ShowScreen(ScreenId.Login);
        }
    }
}
