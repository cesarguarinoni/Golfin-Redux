using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Golfin.UI
{
    /// <summary>
    /// Language selection submenu with toggle buttons for each language.
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
        
        private string _currentLanguage = "English";
        private const string LANGUAGE_KEY = "Settings_Language";

        private void Awake()
        {
            // Wire up button events
            if (englishButton != null)
            {
                englishButton.onClick.AddListener(() => OnLanguageSelected("English"));
            }
            
            if (japaneseButton != null)
            {
                japaneseButton.onClick.AddListener(() => OnLanguageSelected("Japanese"));
            }
        }

        private void Start()
        {
            LoadLanguagePreference();
            UpdateUI();
        }

        /// <summary>
        /// Load the saved language preference.
        /// </summary>
        private void LoadLanguagePreference()
        {
            _currentLanguage = PlayerPrefs.GetString(LANGUAGE_KEY, "English");
            Debug.Log($"[LanguageSubmenu] Loaded language: {_currentLanguage}");
        }

        /// <summary>
        /// Called when a language button is clicked.
        /// </summary>
        private void OnLanguageSelected(string language)
        {
            if (_currentLanguage == language)
            {
                Debug.Log($"[LanguageSubmenu] Language already selected: {language}");
                return;
            }
            
            _currentLanguage = language;
            
            // Save preference
            PlayerPrefs.SetString(LANGUAGE_KEY, language);
            PlayerPrefs.Save();
            
            // Update UI
            UpdateUI();
            
            // Apply language change to LocalizationManager
            ApplyLanguageChange();
            
            Debug.Log($"[LanguageSubmenu] Language changed to: {language}");
        }

        /// <summary>
        /// Update the UI to reflect the current language selection.
        /// </summary>
        private void UpdateUI()
        {
            bool isEnglish = _currentLanguage == "English";
            
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
        /// Apply the language change to the LocalizationManager.
        /// </summary>
        private void ApplyLanguageChange()
        {
            // TODO: Integrate with your LocalizationManager
            // Example:
            // if (LocalizationManager.Instance != null)
            // {
            //     LocalizationManager.Instance.SetLanguage(_currentLanguage);
            // }
            
            Debug.Log($"[LanguageSubmenu] TODO: Apply language change to LocalizationManager: {_currentLanguage}");
            
            // For Phase 2, just log. Phase 3 will integrate with actual localization system.
        }

        /// <summary>
        /// Get the currently selected language.
        /// </summary>
        public string GetCurrentLanguage()
        {
            return _currentLanguage;
        }

        /// <summary>
        /// Check if English is currently selected.
        /// </summary>
        public bool IsEnglish()
        {
            return _currentLanguage == "English";
        }

        /// <summary>
        /// Check if Japanese is currently selected.
        /// </summary>
        public bool IsJapanese()
        {
            return _currentLanguage == "Japanese";
        }
    }
}
