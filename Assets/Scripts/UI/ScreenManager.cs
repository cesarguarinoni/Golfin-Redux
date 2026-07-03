using UnityEngine;
using Golfin.Audio;

namespace GolfinRedux.UI
{
    public enum ScreenId
    {
        Logo,
        Splash,
        Loading,
        Home,
        Roster,
        Inventory,
        HoleSelection,
        ModeSelection,
        Leaderboard,
        // Tournament screens (Stage 1 scaffolds — separate full screens from the
        // non-tournament HoleSelection / Leaderboard above).
        TournamentHoleSelection,
        TournamentLeaderboard,
        // T7 — Tournament Selection browse screen (Figma 13386:1758, Stage 0–1)
        TournamentSelection,
        // Order 517 — Stamina Boost Shop (Figma 13156:1178 + 13330:1139)
        StaminaShopSelection,
        StaminaShopDetail
        // Settings removed - it's an overlay, not a screen
    }

    /// <summary>
    /// Central controller for shell UI screens (Logo, Splash, Loading, Home).
    /// Uses FadeController (if present) to fade to/from black when switching screens.
    /// Note: Settings is an overlay managed by SettingsController, not a screen.
    /// </summary>
    public class ScreenManager : MonoBehaviour
    {
        public static ScreenManager? Instance { get; private set; }

        [SerializeField] private ScreenId _initialScreen = ScreenId.Logo;

        [Header("Screen Containers")]
        [SerializeField] private GameObject _logoScreen;
        [SerializeField] private GameObject _splashScreen;
        [SerializeField] private GameObject _loadingScreen;
        [SerializeField] private GameObject _homeScreen;
        [SerializeField] private GameObject _rosterScreen;
        [SerializeField] private GameObject _inventoryScreen;
        [SerializeField] private GameObject _holeSelectionScreen;
        [SerializeField] private GameObject _modeSelectionScreen;
        [SerializeField] private GameObject _leaderboardScreen;
        [SerializeField] private GameObject _tournamentHoleSelectionScreen;
        [SerializeField] private GameObject _tournamentLeaderboardScreen;
        [SerializeField] private GameObject _tournamentSelectionScreen;
        // Order 517 — Stamina Boost Shop screens
        [SerializeField] private GameObject _staminaShopSelectionScreen;
        [SerializeField] private GameObject _staminaShopDetailScreen;
        // _settingsScreen removed - Settings is an overlay managed by SettingsController, not ScreenManager

        [Header("Audio (Order 350)")]
        [Tooltip("Main Theme music clip — assign Assets/Music/Main Theme.mp3 in the Inspector.")]
        [SerializeField] private AudioClip _mainThemeClip;

        private ScreenId _currentScreen;

        /// <summary>Returns the currently active ScreenId.</summary>
        public ScreenId CurrentScreen => _currentScreen;

        /// <summary>
        /// Fired at the end of every ApplyScreen call (after SetActive calls and _currentScreen update).
        /// Used by TournamentResultPresenter to know when an eligible screen is active.
        /// </summary>
        public static event System.Action<ScreenId>? ScreenChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            // FadeController's GameObject may be left inactive in the Editor.
            // Activate it now so fade transitions work for all screens including Inventory.
            if (FadeController.Instance == null)
            {
                var fc = FindObjectOfType<FadeController>(includeInactive: true);
                if (fc != null) fc.gameObject.SetActive(true);
            }
        }

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

            if (_inventoryScreen != null)
                _inventoryScreen.SetActive(screenId == ScreenId.Inventory);

            if (_holeSelectionScreen != null)
                _holeSelectionScreen.SetActive(screenId == ScreenId.HoleSelection);

            if (_modeSelectionScreen != null)
                _modeSelectionScreen.SetActive(screenId == ScreenId.ModeSelection);

            if (_leaderboardScreen != null)
                _leaderboardScreen.SetActive(screenId == ScreenId.Leaderboard);

            if (_tournamentHoleSelectionScreen != null)
                _tournamentHoleSelectionScreen.SetActive(screenId == ScreenId.TournamentHoleSelection);

            if (_tournamentLeaderboardScreen != null)
                _tournamentLeaderboardScreen.SetActive(screenId == ScreenId.TournamentLeaderboard);

            if (_tournamentSelectionScreen != null)
                _tournamentSelectionScreen.SetActive(screenId == ScreenId.TournamentSelection);

            // Order 517 — Stamina Boost Shop screens
            if (_staminaShopSelectionScreen != null)
                _staminaShopSelectionScreen.SetActive(screenId == ScreenId.StaminaShopSelection);
            if (_staminaShopDetailScreen != null)
                _staminaShopDetailScreen.SetActive(screenId == ScreenId.StaminaShopDetail);

            // Settings is an overlay (SettingsController), not managed here

            // Order 350: menu music — start on menu screens, stop during loading/logo/splash.
            // AudioManager is DDOL and lives in Assembly-CSharp, same as ScreenManager.
            bool isMenuScreen = screenId == ScreenId.Home
                             || screenId == ScreenId.Roster
                             || screenId == ScreenId.Inventory
                             || screenId == ScreenId.HoleSelection
                             || screenId == ScreenId.ModeSelection
                             || screenId == ScreenId.Leaderboard
                             || screenId == ScreenId.TournamentHoleSelection
                             || screenId == ScreenId.TournamentLeaderboard
                             || screenId == ScreenId.TournamentSelection
                             || screenId == ScreenId.StaminaShopSelection
                             || screenId == ScreenId.StaminaShopDetail;
            if (AudioManager.Instance != null)
            {
                if (isMenuScreen && _mainThemeClip != null && !AudioManager.Instance.IsMusicPlaying())
                    AudioManager.Instance.PlayMusic(_mainThemeClip, loop: true);
                else if (!isMenuScreen && AudioManager.Instance.IsMusicPlaying())
                    AudioManager.Instance.StopMusic();
            }

            // Show persistent bars on Home, Roster, Inventory, HoleSelection, ModeSelection, Leaderboard; hide on Logo/Splash/Loading
            bool showBars = screenId == ScreenId.Home
                         || screenId == ScreenId.Roster
                         || screenId == ScreenId.Inventory
                         || screenId == ScreenId.HoleSelection
                         || screenId == ScreenId.ModeSelection
                         || screenId == ScreenId.Leaderboard
                         || screenId == ScreenId.TournamentHoleSelection
                         || screenId == ScreenId.TournamentLeaderboard
                         || screenId == ScreenId.TournamentSelection
                         || screenId == ScreenId.StaminaShopSelection
                         || screenId == ScreenId.StaminaShopDetail;
            if (Golfin.UI.PersistentUIManager.Instance != null)
            {
                if (showBars)
                {
                    Golfin.UI.PersistentUIManager.Instance.ShowBars();
                    Golfin.UI.PersistentUIManager.Instance.HighlightScreen(screenId);
                }
                else
                {
                    Golfin.UI.PersistentUIManager.Instance.HideBars();
                }
            }

            // S1 — notify TournamentResultPresenter (and any other listeners) that the
            // active screen has changed. Fired AFTER all SetActive calls and _currentScreen update.
            ScreenChanged?.Invoke(screenId);
        }
    }
}