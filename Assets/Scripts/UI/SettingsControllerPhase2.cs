using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace Golfin.UI
{
    /// <summary>
    /// Controller for the Settings Screen - Phase 2 with Accordion Behavior
    /// Manages expand/collapse of menu items and ensures only one is open at a time.
    /// </summary>
    public class SettingsControllerPhase2 : MonoBehaviour
    {
        public static SettingsControllerPhase2 Instance { get; private set; }

        [Header("Settings Panel")]
        public GameObject background;
        public GameObject settingsPanel;
        public Button closeButton;

        [Header("Menu Items with Accordion")]
        public SettingsMenuItem userProfileItem;
        public SettingsMenuItem soundSettingsItem;
        public SettingsMenuItem languageItem;

        [Header("Simple Menu Buttons (No Accordion)")]
        public Button termsOfUseButton;
        public Button privacyPolicyButton;
        public Button faqButton;
        public Button aboutButton;
        public Button contactButton;
        public Button logOutButton;

        [Header("Submenus")]
        public UserProfileSubmenu userProfileSubmenu;
        public SoundSettingsSubmenu soundSettingsSubmenu;
        public LanguageSubmenu languageSubmenu;

        private List<SettingsMenuItem> _accordionItems = new List<SettingsMenuItem>();
        private SettingsMenuItem _currentlyExpandedItem;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            InitializeAccordionItems();
            InitializeButtons();
        }

        private void Start()
        {
            // Start with settings closed
            if (background != null) background.SetActive(false);
            if (settingsPanel != null) settingsPanel.SetActive(false);
        }

        /// <summary>
        /// Initialize accordion menu items and subscribe to events.
        /// </summary>
        private void InitializeAccordionItems()
        {
            // Collect all accordion items
            if (userProfileItem != null)
            {
                _accordionItems.Add(userProfileItem);
                userProfileItem.OnExpanded += OnMenuItemExpanded;
            }

            if (soundSettingsItem != null)
            {
                _accordionItems.Add(soundSettingsItem);
                soundSettingsItem.OnExpanded += OnMenuItemExpanded;
            }

            if (languageItem != null)
            {
                _accordionItems.Add(languageItem);
                languageItem.OnExpanded += OnMenuItemExpanded;
            }
        }

        /// <summary>
        /// Initialize button click handlers.
        /// </summary>
        private void InitializeButtons()
        {
            if (closeButton != null)
            {
                closeButton.onClick.AddListener(CloseSettings);
            }

            // Simple buttons (no accordion)
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

        /// <summary>
        /// Called when any menu item is expanded.
        /// Ensures only one item is expanded at a time.
        /// </summary>
        private void OnMenuItemExpanded(SettingsMenuItem expandedItem)
        {
            // Collapse all other items
            foreach (var item in _accordionItems)
            {
                if (item != expandedItem && item.IsExpanded)
                {
                    item.ForceCollapse();
                }
            }

            _currentlyExpandedItem = expandedItem;
            Debug.Log($"[SettingsController] Expanded: {expandedItem.gameObject.name}");
        }

        /// <summary>
        /// Open the settings panel.
        /// </summary>
        public void OpenSettings()
        {
            if (background != null) background.SetActive(true);
            if (settingsPanel != null) settingsPanel.SetActive(true);

            Debug.Log("[SettingsController] Settings opened");
        }

        /// <summary>
        /// Close the settings panel and collapse all items.
        /// </summary>
        public void CloseSettings()
        {
            // Collapse all accordion items
            foreach (var item in _accordionItems)
            {
                if (item.IsExpanded)
                {
                    item.ForceCollapse();
                }
            }

            _currentlyExpandedItem = null;

            if (settingsPanel != null) settingsPanel.SetActive(false);
            if (background != null) background.SetActive(false);

            Debug.Log("[SettingsController] Settings closed");
        }

        // Simple menu item handlers (Phase 3 features)

        private void OnTermsOfUseClick()
        {
            Debug.Log("[SettingsController] Terms of Use clicked");
            OpenWebView("https://golfin.io/terms-of-use");
        }

        private void OnPrivacyPolicyClick()
        {
            Debug.Log("[SettingsController] Privacy Policy clicked");
            OpenWebView("https://golfin.io/privacy-policy");
        }

        private void OnFaqClick()
        {
            Debug.Log("[SettingsController] FAQ clicked");
            OpenWebView("https://golfin.io/faq");
        }

        private void OnAboutClick()
        {
            Debug.Log("[SettingsController] About clicked - TODO: Show version modal");
            // Phase 3: Show app version + licenses
        }

        private void OnContactClick()
        {
            Debug.Log("[SettingsController] Contact clicked");
            OpenWebView("https://golfin.io/contact");
        }

        private void OnLogOutClick()
        {
            Debug.Log("[SettingsController] Log Out clicked - TODO: Show confirmation");
            // Phase 3: Confirmation modal → Clear session → Login screen
        }

        /// <summary>
        /// Open a URL in webview or external browser.
        /// </summary>
        private void OpenWebView(string url)
        {
            Debug.Log($"[SettingsController] Opening URL: {url}");
            
#if UNITY_ANDROID || UNITY_IOS
            Application.OpenURL(url);
#else
            Debug.LogWarning("Webview not supported on this platform. Opening in external browser.");
            Application.OpenURL(url);
#endif
        }

        /// <summary>
        /// Collapse all accordion items (useful for external control).
        /// </summary>
        public void CollapseAllItems()
        {
            foreach (var item in _accordionItems)
            {
                if (item.IsExpanded)
                {
                    item.Collapse();
                }
            }
            _currentlyExpandedItem = null;
        }

        /// <summary>
        /// Get the currently expanded menu item.
        /// </summary>
        public SettingsMenuItem GetCurrentlyExpandedItem()
        {
            return _currentlyExpandedItem;
        }
    }
}
