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

        [Header("Bottom Navigation Bar References")]
        public GameObject bottomNavPanel;
        public Button homeButton;
        public Button gachaButton;
        public Button mainPlayButton;
        public Button inventoryButton;
        public Button charactersButton;

        [Header("Current Screen Highlight")]
        public Image homeHighlight;
        public Image gachaHighlight;
        public Image mainPlayHighlight;
        public Image inventoryHighlight;
        public Image charactersHighlight;

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

        public void SetUsername(string username)
        {
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
            if (usernameText != null)
            {
                usernameText.text = newUsername;
            }

            Debug.Log($"[PersistentUI] Username updated: {newUsername}");
        }

        private void OnSettingsButtonClick()
        {
            // Open Settings Screen (support both Phase 1 and Phase 2 controllers)
            if (SettingsControllerPhase2.Instance != null)
            {
                SettingsControllerPhase2.Instance.OpenSettings();
            }
            else if (SettingsController.Instance != null)
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
                    sm.ShowScreen(GolfinRedux.UI.ScreenId.HoleSelection);
                    break;
                default:
                    Debug.LogWarning($"[PersistentUI] Navigation to {screen} not yet implemented.");
                    break;
            }
        }

        private void UpdateScreenHighlight()
        {
            // Disable all highlights
            if (homeHighlight != null) homeHighlight.enabled = false;
            if (gachaHighlight != null) gachaHighlight.enabled = false;
            if (mainPlayHighlight != null) mainPlayHighlight.enabled = false;
            if (inventoryHighlight != null) inventoryHighlight.enabled = false;
            if (charactersHighlight != null) charactersHighlight.enabled = false;

            // Enable current screen highlight
            switch (currentScreen)
            {
                case Screen.Home:
                    if (homeHighlight != null) homeHighlight.enabled = true;
                    break;
                case Screen.Gacha:
                    if (gachaHighlight != null) gachaHighlight.enabled = true;
                    break;
                case Screen.MainPlay:
                    if (mainPlayHighlight != null) mainPlayHighlight.enabled = true;
                    break;
                case Screen.Inventory:
                    if (inventoryHighlight != null) inventoryHighlight.enabled = true;
                    break;
                case Screen.Characters:
                    if (charactersHighlight != null) charactersHighlight.enabled = true;
                    break;
            }
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
    }
}
