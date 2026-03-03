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
    /// Uses FadeController (if present) to fade to/from black when switching screens.
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
            // Show the initial screen immediately
            ApplyScreen(_initialScreen);

            // Then fade in from black if FadeController is present
            if (FadeController.Instance != null)
            {
                FadeController.Instance.FadeIn();
            }
        }

        /// <summary>
        /// Show the given screen. If instant=false and FadeController exists,
        /// performs fade-out -> swap -> fade-in.
        /// </summary>
        public void ShowScreen(ScreenId screenId, bool instant = false)
        {
            if (_currentScreen == screenId && !instant)
                return;

            // If no fade system, or caller requests instant, just swap
            if (instant || FadeController.Instance == null)
            {
                ApplyScreen(screenId);
            }
            else
            {
                // Fade to black, swap at midpoint, fade back in
                FadeController.Instance.FadeOutThenIn(() => ApplyScreen(screenId));
            }
        }

        /// <summary>
        /// Actually activates/deactivates screen GameObjects.
        /// </summary>
        private void ApplyScreen(ScreenId screenId)
        {
            _currentScreen = screenId;

            if (_logoScreen != null) _logoScreen.SetActive(screenId == ScreenId.Logo);
            if (_splashScreen != null) _splashScreen.SetActive(screenId == ScreenId.Splash);
            if (_loadingScreen != null) _loadingScreen.SetActive(screenId == ScreenId.Loading);
            if (_homeScreen != null) _homeScreen.SetActive(screenId == ScreenId.Home);
            if (_settingsScreen != null) _settingsScreen.SetActive(screenId == ScreenId.Settings);
        }
    }
}