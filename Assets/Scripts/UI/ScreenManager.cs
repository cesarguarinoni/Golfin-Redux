using UnityEngine;

namespace GolfinRedux.UI
{
    public enum ScreenId
    {
        Logo,
        Splash,
        Loading,
        Home,
        Settings
    }

    /// <summary>
    /// Central controller for shell UI screens (Logo, Splash, Loading, Home, Settings).
    /// Gameplay lives in a separate scene.
    /// </summary>
    public class ScreenManager : MonoBehaviour
    {
        [SerializeField] private ScreenId _initialScreen = ScreenId.Logo;

        [Header("Screen Containers")]
        [SerializeField] private GameObject _logoScreen;
        [SerializeField] private GameObject _splashScreen;
        [SerializeField] private GameObject _loadingScreen;
        [SerializeField] private GameObject _homeScreen;
        [SerializeField] private GameObject _settingsScreen;

        private ScreenId _currentScreen;

        private void Start()
        {
            // Simple initial show without animation; can be extended to use fades/slides.
            ShowScreen(_initialScreen, instant: true);
        }

        public void ShowScreen(ScreenId screenId, bool instant = false)
        {
            if (_currentScreen == screenId && !instant)
                return;

            _currentScreen = screenId;

            if (_logoScreen != null) _logoScreen.SetActive(screenId == ScreenId.Logo);
            if (_splashScreen != null) _splashScreen.SetActive(screenId == ScreenId.Splash);
            if (_loadingScreen != null) _loadingScreen.SetActive(screenId == ScreenId.Loading);
            if (_homeScreen != null) _homeScreen.SetActive(screenId == ScreenId.Home);
            if (_settingsScreen != null) _settingsScreen.SetActive(screenId == ScreenId.Settings);

            // TODO: Replace instant toggles with fade/slide transitions.
        }
    }
}
