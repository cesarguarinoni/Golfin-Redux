using UnityEngine;
using UnityEngine.UI;

namespace Golfin.UI
{
    /// <summary>
    /// Controller for the Settings Screen (Phase 1: Static panel with all buttons)
    /// </summary>
    public class SettingsController : MonoBehaviour
    {
        public static SettingsController Instance { get; private set; }

        [Header("Settings Panel")]
        public GameObject settingsPanel;
        public Button closeButton;

        [Header("Settings Menu Items")]
        public Button userProfileButton;
        public Button soundSettingsButton;
        public Button languageButton;
        public Button termsOfUseButton;
        public Button privacyPolicyButton;
        public Button faqButton;
        public Button aboutButton;
        public Button contactButton;
        public Button logOutButton;

        [Header("Menu Item Icons")]
        public Image userProfileIcon;
        public Image soundSettingsIcon;
        public Image languageIcon;
        public Image termsOfUseIcon;
        public Image privacyPolicyIcon;
        public Image faqIcon;
        public Image aboutIcon;
        public Image contactIcon;
        public Image logOutIcon;

        [Header("Menu Item Labels")]
        public TMPro.TextMeshProUGUI userProfileLabel;
        public TMPro.TextMeshProUGUI soundSettingsLabel;
        public TMPro.TextMeshProUGUI languageLabel;
        public TMPro.TextMeshProUGUI termsOfUseLabel;
        public TMPro.TextMeshProUGUI privacyPolicyLabel;
        public TMPro.TextMeshProUGUI faqLabel;
        public TMPro.TextMeshProUGUI aboutLabel;
        public TMPro.TextMeshProUGUI contactLabel;
        public TMPro.TextMeshProUGUI logOutLabel;

        [Header("Menu Item Right Arrows")]
        public Image userProfileArrow;
        public Image soundSettingsArrow;
        public Image languageArrow;
        public Image termsOfUseArrow;
        public Image privacyPolicyArrow;
        public Image faqArrow;
        public Image aboutArrow;
        public Image contactArrow;

        private void Awake()
        {
            Debug.Log("[SettingsController] Awake() called");
            
            if (Instance != null && Instance != this)
            {
                Debug.Log("[SettingsController] Duplicate instance - destroying");
                Destroy(gameObject);
                return;
            }

            Instance = this;
            Debug.Log("[SettingsController] Singleton initialized");
            InitializeButtons();
        }

        private void Start()
        {
            Debug.Log("[SettingsController] Start() called - Instance still valid");
            
            // Start with settings panel closed
            if (settingsPanel != null)
            {
                settingsPanel.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            Debug.LogWarning("[SettingsController] OnDestroy() called - Instance being destroyed!");
        }

        private void InitializeButtons()
        {
            // Close button
            if (closeButton != null)
            {
                closeButton.onClick.AddListener(CloseSettings);
            }

            // Menu item buttons
            if (userProfileButton != null)
                userProfileButton.onClick.AddListener(OnUserProfileClick);

            if (soundSettingsButton != null)
                soundSettingsButton.onClick.AddListener(OnSoundSettingsClick);

            if (languageButton != null)
                languageButton.onClick.AddListener(OnLanguageClick);

            if (termsOfUseButton != null)
                termsOfUseButton.onClick.AddListener(OnTermsOfUseClick);

            if (privacyPolicyButton != null)
                privacyPolicyButton.onClick.AddListener(OnPrivacyPolicyClick);

            if (faqButton != null)
                faqButton.onClick.AddListener(OnFaqClick);

            if (aboutButton != null)
                aboutButton.onClick.AddListener(OnAboutClick);

            if (contactButton != null)
                contactButton.onClick.AddListener(OnContactClick);

            if (logOutButton != null)
                logOutButton.onClick.AddListener(OnLogOutClick);
        }

        public void OpenSettings()
        {
            Debug.Log("[SettingsController] OpenSettings() called");
            
            if (settingsPanel != null)
            {
                Debug.Log($"[SettingsController] Activating settings panel: {settingsPanel.name}");
                settingsPanel.SetActive(true);
            }
            else
            {
                Debug.LogError("[SettingsController] settingsPanel is NULL! Not assigned in Inspector?");
            }
        }

        public void CloseSettings()
        {
            if (settingsPanel != null)
            {
                settingsPanel.SetActive(false);
                Debug.Log("Settings panel closed");
            }
        }

        // Menu Item Click Handlers (Phase 1: Just logs, Phase 2 will add expand/collapse)
        private void OnUserProfileClick()
        {
            Debug.Log("User Profile clicked - TODO: Expand submenu");
            // Phase 2: Expand to show account linking options
        }

        private void OnSoundSettingsClick()
        {
            Debug.Log("Sound Settings clicked - TODO: Expand submenu");
            // Phase 2: Expand to show Music Volume + SFX Volume sliders
        }

        private void OnLanguageClick()
        {
            Debug.Log("Language clicked - TODO: Open language selection");
            // Phase 2: Open language selection screen (English/Japanese)
        }

        private void OnTermsOfUseClick()
        {
            Debug.Log("Terms of Use clicked - TODO: Open webview");
            // Phase 3: Open webview with Terms of Use document
            OpenWebView("https://golfin.io/terms-of-use");
        }

        private void OnPrivacyPolicyClick()
        {
            Debug.Log("Privacy Policy clicked - TODO: Open webview");
            // Phase 3: Open webview with Privacy Policy document
            OpenWebView("https://golfin.io/privacy-policy");
        }

        private void OnFaqClick()
        {
            Debug.Log("FAQ clicked - TODO: Open webview");
            // Phase 3: Open webview with FAQ screen
            OpenWebView("https://golfin.io/faq");
        }

        private void OnAboutClick()
        {
            Debug.Log("About clicked - TODO: Show version + licenses");
            // Phase 3: Show app version, licenses modal
        }

        private void OnContactClick()
        {
            Debug.Log("Contact clicked - TODO: Open contact form");
            // Phase 3: Open webview with contact form
            OpenWebView("https://golfin.io/contact");
        }

        private void OnLogOutClick()
        {
            Debug.Log("Log Out clicked - TODO: Show confirmation modal");
            // Phase 3: Show confirmation modal, clear session, return to login
        }

        private void OpenWebView(string url)
        {
            // Placeholder for webview opening
            Debug.Log($"Opening webview: {url}");
            // Use platform-specific webview plugin (e.g., UniWebView, Vuplex)
#if UNITY_ANDROID || UNITY_IOS
            Application.OpenURL(url);
#else
            Debug.LogWarning("Webview not supported on this platform. Opening in external browser.");
            Application.OpenURL(url);
#endif
        }
    }
}
