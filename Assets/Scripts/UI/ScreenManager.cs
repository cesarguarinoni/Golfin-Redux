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

        private ScreenId _currentScreen;

        private void Start()
        {
            // TODO: Wire up references to per-screen controllers and perform
            // an initial transition to _initialScreen.
            _currentScreen = _initialScreen;
        }

        public void ShowScreen(ScreenId screenId)
        {
            if (_currentScreen == screenId)
                return;

            // TODO: Implement transitions (fade/slide) between _currentScreen and screenId.
            _currentScreen = screenId;
        }
    }
}
