using UnityEngine;

namespace GolfinRedux.UI
{
    public enum ScreenId
    {
        Logo,
        Splash,
        Loading,
        Home,
        Roster
        // Settings removed - it's an overlay, not a screen
    }

    /// <summary>
    /// Central controller for shell UI screens (Logo, Splash, Loading, Home).
    /// Uses FadeController (if present) to fade to/from black when switching screens.
    /// Note: Settings is an overlay managed by SettingsController, not a screen.
    /// </summary>
    public class ScreenManager : MonoBehaviour
    {
        [SerializeField] private ScreenId _initialScreen = ScreenId.Logo;

        [Header("Screen Containers")]
        [SerializeField] private GameObject _logoScreen;
        [SerializeField] private GameObject _splashScreen;
        [SerializeField] private GameObject _loadingScreen;
        [SerializeField] private GameObject _homeScreen;
        [SerializeField] private GameObject _rosterScreen;
        // _settingsScreen removed - Settings is an overlay managed by SettingsController, not ScreenManager

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
            Debug.Log($"[ScreenManager] ShowScreen called: {screenId} (current: {_currentScreen}, instant: {instant})");
            
            if (_currentScreen == screenId && !instant)
            {
                Debug.Log($"[ScreenManager] Already on {screenId}, ignoring");
                return;
            }

            // If no fade system, or caller requests instant, just swap
            if (instant || FadeController.Instance == null)
            {
                Debug.Log($"[ScreenManager] Applying screen immediately: {screenId}");
                ApplyScreen(screenId);
            }
            else
            {
                Debug.Log($"[ScreenManager] Fading to {screenId}");
                // Fade to black, swap at midpoint, fade back in
                FadeController.Instance.FadeOutThenIn(() => ApplyScreen(screenId));
            }
        }

        /// <summary>
        /// Actually activates/deactivates screen GameObjects.
        /// </summary>
        private void ApplyScreen(ScreenId screenId)
        {
            Debug.Log($"[ScreenManager] ApplyScreen: {screenId}");
            _currentScreen = screenId;

            if (_logoScreen != null) _logoScreen.SetActive(screenId == ScreenId.Logo);
            if (_splashScreen != null) _splashScreen.SetActive(screenId == ScreenId.Splash);
            if (_loadingScreen != null) _loadingScreen.SetActive(screenId == ScreenId.Loading);
            if (_homeScreen != null) _homeScreen.SetActive(screenId == ScreenId.Home);
            
            if (_rosterScreen != null)
            {
                bool shouldBeActive = (screenId == ScreenId.Roster);
                Debug.Log($"[ScreenManager] RosterScreen: {(shouldBeActive ? "ACTIVATING" : "deactivating")}");
                _rosterScreen.SetActive(shouldBeActive);
            }
            else
            {
                Debug.LogWarning("[ScreenManager] _rosterScreen is NULL!");
            }
            // Settings is an overlay (SettingsController), not managed here
        }
    }
}