using UnityEngine;
using UnityEngine.UI;

namespace GolfinRedux.UI
{
    public class SplashScreenController : MonoBehaviour
    {
        [SerializeField] private Button _startButton;
        [SerializeField] private Button _createAccountButton;
        [SerializeField] private Button _loginButton;

        private ScreenManager _screenManager;

        private void Awake()
        {
            _screenManager = FindObjectOfType<ScreenManager>();

            if (_createAccountButton != null)
                _createAccountButton.onClick.AddListener(OnCreateAccountClicked);

            if (_loginButton != null)
                _loginButton.onClick.AddListener(OnLoginClicked);
        }

        public void OnStartClicked()
        {
            Debug.Log("START clicked – attempting to show Loading");
            if (_screenManager != null)
                _screenManager.ShowScreen(ScreenId.Loading, instant: true);
            else
                Debug.LogError("ScreenManager not found");
        }

        private void OnCreateAccountClicked()
        {
            // TODO: go to Create Account screen when implemented
            Debug.Log("Create Account clicked (not implemented yet)");
        }

        private void OnLoginClicked()
        {
            // TODO: go to Login screen when implemented
            Debug.Log("Login clicked (not implemented yet)");
        }
    }
}
