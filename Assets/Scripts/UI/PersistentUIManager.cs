using UnityEngine;
using UnityEngine.UI;

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
        public void UpdateUsername(string username)
        {
            SetUsername(username);
            Debug.Log($"[PersistentUI] Username updated: {username}");
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

            // Handle screen navigation
            // TODO: Implement actual screen switching logic
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
