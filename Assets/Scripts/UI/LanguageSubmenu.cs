using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Golfin.UI
{
    /// <summary>
    /// Language selection submenu with toggle buttons for each language.
    /// Integrates with LocalizationManager to update all UI text in real-time.
    /// </summary>
    public class LanguageSubmenu : MonoBehaviour
    {
        [Header("Language Buttons")]
        [SerializeField] private Button englishButton;
        [SerializeField] private Button japaneseButton;
        
        [Header("Selection Indicators")]
        [SerializeField] private GameObject englishCheckmark;
        [SerializeField] private GameObject japaneseCheckmark;
        
        [Header("Button Colors (Optional)")]
        [SerializeField] private Color selectedColor = new Color(0.2f, 0.6f, 1f, 1f);
        [SerializeField] private Color unselectedColor = new Color(0.5f, 0.5f, 0.5f, 1f);
        
        private const string LANGUAGE_KEY = "Settings_Language";

        private void Awake()
        {
            // Wire up button events
            if (englishButton != null)
            {
                englishButton.onClick.AddListener(() => OnLanguageSelected(Language.English));
            }
            
            if (japaneseButton != null)
            {
                japaneseButton.onClick.AddListener(() => OnLanguageSelected(Language.Japanese));
            }
        }

        private void OnEnable()
        {
            // Subscribe to language change events
            LocalizationManager.OnLanguageChanged += OnLanguageChangedExternally;
        }

        private void OnDisable()
        {
            // Unsubscribe from language change events
            LocalizationManager.OnLanguageChanged -= OnLanguageChangedExternally;
        }

        private void Start()
        {
            LoadLanguagePreference();
            UpdateUI();
        }

        /// <summary>
        /// Called when language changes externally (e.g., from another script or startup).
        /// </summary>
        private void OnLanguageChangedExternally()
        {
            UpdateUI();
            Debug.Log($"[LanguageSubmenu] Language changed externally to: {LocalizationManager.CurrentLanguage}");
        }

        /// <summary>
        /// Load the saved language preference and apply it.
        /// </summary>
        private void LoadLanguagePreference()
        {
            // Load from PlayerPrefs (default to current language or English)
            string savedLanguage = PlayerPrefs.GetString(LANGUAGE_KEY, Language.English.ToString());
            
            if (System.Enum.TryParse<Language>(savedLanguage, out Language language))
            {
                // Apply to LocalizationManager if different from current
                if (LocalizationManager.CurrentLanguage != language)
                {
                    LocalizationManager.SetLanguage(language);
                }
                Debug.Log($"[LanguageSubmenu] Loaded language: {language}");
            }
            else
            {
                Debug.LogWarning($"[LanguageSubmenu] Invalid saved language: {savedLanguage}, defaulting to English");
                LocalizationManager.SetLanguage(Language.English);
            }
        }

        /// <summary>
        /// Called when a language button is clicked.
        /// </summary>
        private void OnLanguageSelected(Language language)
        {
            if (LocalizationManager.CurrentLanguage == language)
            {
                Debug.Log($"[LanguageSubmenu] Language already selected: {language}");
                return;
            }
            
            // Save preference
            PlayerPrefs.SetString(LANGUAGE_KEY, language.ToString());
            PlayerPrefs.Save();
            
            // Apply language change to LocalizationManager
            // This will fire OnLanguageChanged event, which updates all LocalizedText components
            LocalizationManager.SetLanguage(language);
            
            // UI will update via OnLanguageChangedExternally callback
            
            Debug.Log($"[LanguageSubmenu] Language changed to: {language}");
        }

        /// <summary>
        /// Update the UI to reflect the current language selection.
        /// </summary>
        private void UpdateUI()
        {
            Language currentLanguage = LocalizationManager.CurrentLanguage;
            bool isEnglish = currentLanguage == Language.English;
            
            // Update checkmarks
            if (englishCheckmark != null)
            {
                englishCheckmark.SetActive(isEnglish);
            }
            
            if (japaneseCheckmark != null)
            {
                japaneseCheckmark.SetActive(!isEnglish);
            }
            
            // Update button colors (optional)
            UpdateButtonColor(englishButton, isEnglish);
            UpdateButtonColor(japaneseButton, !isEnglish);
        }

        /// <summary>
        /// Update button color based on selection state.
        /// </summary>
        private void UpdateButtonColor(Button button, bool isSelected)
        {
            if (button == null) return;
            
            Image buttonImage = button.GetComponent<Image>();
            if (buttonImage != null)
            {
                buttonImage.color = isSelected ? selectedColor : unselectedColor;
            }
        }

        /// <summary>
        /// Get the currently selected language.
        /// </summary>
        public Language GetCurrentLanguage()
        {
            return LocalizationManager.CurrentLanguage;
        }

        /// <summary>
        /// Check if English is currently selected.
        /// </summary>
        public bool IsEnglish()
        {
            return LocalizationManager.CurrentLanguage == Language.English;
        }

        /// <summary>
        /// Check if Japanese is currently selected.
        /// </summary>
        public bool IsJapanese()
        {
            return LocalizationManager.CurrentLanguage == Language.Japanese;
        }
    }
}
