using UnityEngine;
using UnityEngine.UI;
using Golfin.Roster;

namespace Golfin.UI
{
    /// <summary>
    /// Singleton manager for UI elements that persist across scenes (Top Bar, Bottom Nav)
    /// </summary>
    public class PersistentUIManager : MonoBehaviour
    {
        public static PersistentUIManager Instance { get; private set; }

        [Header("Top Bar References")]
        public GameObject topBarPanel;
        public Image rewardPointsIcon;
        public TMPro.TextMeshProUGUI rewardPointsText;
        public Button settingsButton;
        public TMPro.TextMeshProUGUI usernameText;

        // Cached real username so HighlightScreen can restore it on Home.
        private string _username = string.Empty;

        [Header("Bottom Navigation Bar References")]
        public GameObject bottomNavPanel;
        public Button homeButton;
        public Button gachaButton;
        public Button mainPlayButton;
        public Button inventoryButton;
        public Button charactersButton;

        [Header("Bottom Nav Icon Highlight")]
        [Tooltip("Image component on each nav button. Tinted activeColor when its screen is active, normalColor otherwise.")]
        public Image homeIcon;
        public Image gachaIcon;
        public Image mainPlayIcon;
        public Image inventoryIcon;
        public Image charactersIcon;

        public Color iconNormalColor = Color.white;
        public Color iconActiveColor = Color.cyan;

        public enum Screen
        {
            Home,
            Gacha,
            MainPlay,
            Inventory,
            Characters,
            Settings
        }

        private Screen currentScreen = Screen.Home;

        private void Awake()
        {
            // Singleton pattern
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Cache the designer-set username text (e.g. "CHOTO") at startup.
            // HighlightScreen will restore it when navigating to Home.
            if (usernameText != null)
                _username = usernameText.text;

            // Hide by default (show when HomeScreen loads)
            HideBars();

            InitializeButtons();
        }

        private void OnEnable()
        {
            // RewardPointsManager may not exist yet if we're very early in startup —
            // Start() below covers that case.
            if (RewardPointsManager.Instance != null)
            {
                RewardPointsManager.Instance.OnPointsChanged += SetRewardPoints;
                SetRewardPoints(RewardPointsManager.Instance.GetPoints());
            }
        }

        private void Start()
        {
            // By Start() all Awake() calls are done, so Instance is guaranteed available.
            // Subscribe only if OnEnable() missed it (Instance was null at that point).
            if (RewardPointsManager.Instance != null)
            {
                // Re-subscribing a delegate that's already subscribed would double-fire,
                // so remove first to ensure exactly one subscription.
                RewardPointsManager.Instance.OnPointsChanged -= SetRewardPoints;
                RewardPointsManager.Instance.OnPointsChanged += SetRewardPoints;
                SetRewardPoints(RewardPointsManager.Instance.GetPoints());
            }
            else
            {
                Debug.LogWarning("[PersistentUI] RewardPointsManager not found — RP display will not update.");
            }
        }

        private void OnDisable()
        {
            if (RewardPointsManager.Instance != null)
                RewardPointsManager.Instance.OnPointsChanged -= SetRewardPoints;
        }

        /// <summary>
        /// Show Top Bar and Bottom Nav (call from HomeScreen onwards)
        /// </summary>
        public void ShowBars()
        {
            ShowTopBar(true);
            ShowBottomNav(true);
        }

        /// <summary>
        /// Hide Top Bar and Bottom Nav (for Logo, Splash, Loading screens)
        /// </summary>
        public void HideBars()
        {
            ShowTopBar(false);
            ShowBottomNav(false);
        }

        private void InitializeButtons()
        {
            // Settings button
            if (settingsButton != null)
            {
                settingsButton.onClick.AddListener(OnSettingsButtonClick);
            }

            // Bottom nav buttons
            if (homeButton != null)
                homeButton.onClick.AddListener(() => NavigateTo(Screen.Home));
            
            if (gachaButton != null)
                gachaButton.onClick.AddListener(() => NavigateTo(Screen.Gacha));
            
            if (mainPlayButton != null)
                mainPlayButton.onClick.AddListener(() => NavigateTo(Screen.MainPlay));
            
            if (inventoryButton != null)
                inventoryButton.onClick.AddListener(() => NavigateTo(Screen.Inventory));
            
            if (charactersButton != null)
                charactersButton.onClick.AddListener(() => NavigateTo(Screen.Characters));
        }

        public void SetRewardPoints(int points)
        {
            if (rewardPointsText != null)
            {
                rewardPointsText.text = points.ToString("N0");
            }
        }

        /// <summary>
        /// Sets the visible center-text WITHOUT touching the cached _username.
        /// This is a transient display override (e.g. screen titles).
        /// To update the persisted username use UpdateUsername instead.
        /// </summary>
        public void SetUsername(string username)
        {
            // NOTE: deliberately does NOT write _username.
            // _username is authoritative and is only written by Awake() and UpdateUsername().
            // ModeSelectScreenController previously poked SetUsername("MODE SELECTION") as a
            // transient title; that was removed in iter-10 (Option A). SetUsername now only
            // updates the live text. If any future caller truly needs to change the real
            // username they must call UpdateUsername instead.
            if (usernameText != null)
            {
                usernameText.text = username;
            }
        }

        /// <summary>
        /// Update the username display (alias for SetUsername for Phase 2 compatibility)
        /// </summary>
        public void UpdateUsername(string newUsername)
        {
            _username = newUsername;
            if (usernameText != null)
            {
                usernameText.text = newUsername;
            }

            Debug.Log($"[PersistentUI] Username updated: {newUsername}");
        }

        private void OnSettingsButtonClick()
        {
            if (SettingsController.Instance != null)
            {
                SettingsController.Instance.OpenSettings();
            }
            else
            {
                Debug.LogWarning("[PersistentUI] No SettingsController instance found - cannot open settings");
            }
        }

        public void NavigateTo(Screen screen)
        {
            currentScreen = screen;
            UpdateScreenHighlight();

            var sm = GolfinRedux.UI.ScreenManager.Instance;
            if (sm == null)
            {
                Debug.LogWarning("[PersistentUI] ScreenManager.Instance is null — cannot navigate.");
                return;
            }

            switch (screen)
            {
                case Screen.Home:
                    sm.ShowScreen(GolfinRedux.UI.ScreenId.Home);
                    break;
                case Screen.Inventory:
                    sm.ShowScreen(GolfinRedux.UI.ScreenId.Inventory);
                    break;
                case Screen.Characters:
                    sm.ShowScreen(GolfinRedux.UI.ScreenId.Roster);
                    break;
                case Screen.MainPlay:
                    // Bottom-nav tee button → Mode Select screen (mode_select_system spec)
                    sm.ShowScreen(GolfinRedux.UI.ScreenId.ModeSelection);
                    break;
                default:
                    Debug.LogWarning($"[PersistentUI] Navigation to {screen} not yet implemented.");
                    break;
            }
        }

        /// <summary>
        /// Called by ScreenManager whenever the active shell screen changes,
        /// so the bottom-nav highlight tracks navigation that bypasses the nav buttons
        /// (e.g. initial load, programmatic transitions).
        /// Also drives the top-bar center text per screen:
        ///   Home        → real username (e.g. "CHOTO")
        ///   Leaderboard → "LEADERBOARD"
        ///   all others  → "" (blank center)
        /// </summary>
        public void HighlightScreen(GolfinRedux.UI.ScreenId screenId)
        {
            // ── Drive top-bar center text BEFORE the nav-highlight switch ────────
            // (The switch has a default:return for Leaderboard; text must be set first.)
            if (usernameText != null)
            {
                switch (screenId)
                {
                    case GolfinRedux.UI.ScreenId.Home:
                        usernameText.text = _username;
                        break;
                    case GolfinRedux.UI.ScreenId.Leaderboard:
                        usernameText.text = "LEADERBOARD";
                        break;
                    case GolfinRedux.UI.ScreenId.ModeSelection:
                        usernameText.text = "MODE SELECTION";
                        break;
                    default:
                        usernameText.text = string.Empty;
                        break;
                }
            }

            // ── Bottom-nav icon highlight ─────────────────────────────────────────
            switch (screenId)
            {
                case GolfinRedux.UI.ScreenId.Home:          currentScreen = Screen.Home; break;
                case GolfinRedux.UI.ScreenId.Roster:        currentScreen = Screen.Characters; break;
                case GolfinRedux.UI.ScreenId.Inventory:     currentScreen = Screen.Inventory; break;
                case GolfinRedux.UI.ScreenId.HoleSelection:  currentScreen = Screen.MainPlay; break;
                case GolfinRedux.UI.ScreenId.ModeSelection:  currentScreen = Screen.MainPlay; break;
                default:
                    return; // Logo/Splash/Loading/Leaderboard: bars hidden or no nav highlight.
            }
            UpdateScreenHighlight();
        }

        private void UpdateScreenHighlight()
        {
            if (homeIcon != null)       homeIcon.color       = (currentScreen == Screen.Home)       ? iconActiveColor : iconNormalColor;
            if (gachaIcon != null)      gachaIcon.color      = (currentScreen == Screen.Gacha)      ? iconActiveColor : iconNormalColor;
            if (mainPlayIcon != null)   mainPlayIcon.color   = (currentScreen == Screen.MainPlay)   ? iconActiveColor : iconNormalColor;
            if (inventoryIcon != null)  inventoryIcon.color  = (currentScreen == Screen.Inventory)  ? iconActiveColor : iconNormalColor;
            if (charactersIcon != null) charactersIcon.color = (currentScreen == Screen.Characters) ? iconActiveColor : iconNormalColor;
        }

        public void ShowTopBar(bool show)
        {
            if (topBarPanel != null)
                topBarPanel.SetActive(show);
        }

        public void ShowBottomNav(bool show)
        {
            if (bottomNavPanel != null)
                bottomNavPanel.SetActive(show);
        }

        /// <summary>
        /// Stage C0: explicit bottom-nav visibility toggle.
        /// Alias for ShowBottomNav(visible) — exposed so GameplaySceneLoader
        /// has a self-documenting call site for the gameplay-transition flow.
        /// </summary>
        public void SetBottomNavVisible(bool visible) => ShowBottomNav(visible);
    }
}
