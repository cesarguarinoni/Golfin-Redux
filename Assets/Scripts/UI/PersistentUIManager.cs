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
            Debug.Log("[PersistentUI] Awake() called");
            
            // Singleton pattern
            if (Instance != null && Instance != this)
            {
                Debug.Log("[PersistentUI] Duplicate instance found - destroying this one");
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            Debug.Log("[PersistentUI] Singleton initialized, moved to DontDestroyOnLoad");

            // Hide by default (show when HomeScreen loads)
            HideBars();

            InitializeButtons();
        }

        /// <summary>
        /// Show Top Bar and Bottom Nav (call from HomeScreen onwards)
        /// </summary>
        public void ShowBars()
        {
            Debug.Log("[PersistentUI] ShowBars() called");
            Debug.Log($"[PersistentUI] topBarPanel: {(topBarPanel != null ? topBarPanel.name : "NULL")}");
            Debug.Log($"[PersistentUI] bottomNavPanel: {(bottomNavPanel != null ? bottomNavPanel.name : "NULL")}");
            ShowTopBar(true);
            ShowBottomNav(true);
        }

        /// <summary>
        /// Hide Top Bar and Bottom Nav (for Logo, Splash, Loading screens)
        /// </summary>
        public void HideBars()
        {
            Debug.Log("[PersistentUI] HideBars() called");
            ShowTopBar(false);
            ShowBottomNav(false);
        }

        private void InitializeButtons()
        {
            Debug.Log("[PersistentUI] InitializeButtons() called");
            
            // Settings button
            if (settingsButton != null)
            {
                Debug.Log($"[PersistentUI] Settings button found: {settingsButton.name}, adding click listener");
                settingsButton.onClick.AddListener(OnSettingsButtonClick);
            }
            else
            {
                Debug.LogWarning("[PersistentUI] Settings button is NULL! Not assigned in Inspector?");
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

        private void OnSettingsButtonClick()
        {
            Debug.Log("[PersistentUI] Settings button clicked!");
            Debug.Log($"[PersistentUI] SettingsController type exists: {(typeof(SettingsController) != null)}");
            Debug.Log($"[PersistentUI] SettingsController.Instance value: {SettingsController.Instance}");
            
            // Open Settings Screen
            if (SettingsController.Instance == null)
            {
                Debug.LogError("[PersistentUI] SettingsController.Instance is NULL! Is SettingsCanvas in the scene?");
                
                // Try to find it manually
                var controller = GameObject.FindObjectOfType<SettingsController>();
                if (controller != null)
                {
                    Debug.LogWarning($"[PersistentUI] Found SettingsController manually on GameObject: {controller.gameObject.name}");
                    controller.OpenSettings();
                }
                else
                {
                    Debug.LogError("[PersistentUI] Could not find SettingsController anywhere in scene!");
                }
            }
            else
            {
                Debug.Log("[PersistentUI] Calling SettingsController.Instance.OpenSettings()");
                SettingsController.Instance.OpenSettings();
            }
        }

        public void NavigateTo(Screen screen)
        {
            currentScreen = screen;
            UpdateScreenHighlight();

            // Handle screen navigation
            switch (screen)
            {
                case Screen.Home:
                    // Load Home Scene or activate Home Panel
                    Debug.Log("Navigate to Home");
                    break;
                case Screen.Gacha:
                    Debug.Log("Navigate to Gacha");
                    break;
                case Screen.MainPlay:
                    Debug.Log("Navigate to Main Play");
                    break;
                case Screen.Inventory:
                    Debug.Log("Navigate to Inventory");
                    break;
                case Screen.Characters:
                    Debug.Log("Navigate to Characters");
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
            Debug.Log($"[PersistentUI] ShowTopBar({show}) - topBarPanel: {(topBarPanel != null ? topBarPanel.name : "NULL")}");
            if (topBarPanel != null)
                topBarPanel.SetActive(show);
        }

        public void ShowBottomNav(bool show)
        {
            Debug.Log($"[PersistentUI] ShowBottomNav({show}) - bottomNavPanel: {(bottomNavPanel != null ? bottomNavPanel.name : "NULL")}");
            if (bottomNavPanel != null)
                bottomNavPanel.SetActive(show);
        }
    }
}
