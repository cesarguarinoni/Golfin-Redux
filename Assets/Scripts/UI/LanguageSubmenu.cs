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
        
        [Header("Row Colors")]
        [SerializeField] private Color selectedColor = new Color32(0x33, 0x99, 0xFF, 0xFF);
        [SerializeField] private Color unselectedColor = new Color32(0x26, 0x42, 0x5F, 0xFF);

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

            // While the accordion is collapsed this object is inactive, so it misses any
            // OnLanguageChanged fired in the meantime. Re-sync on every open.
            UpdateUI();
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
        /// Re-apply the player's saved language preference. With no saved choice the
        /// language resolved at boot (device language) is left untouched — never forced
        /// back to English, or a Japanese device would flip to English on first open.
        /// </summary>
        private void LoadLanguagePreference()
        {
            if (!LanguageSettings.TryGetSavedLanguage(out Language language))
            {
                Debug.Log($"[LanguageSubmenu] No saved language; keeping startup language: {LocalizationManager.CurrentLanguage}");
                return;
            }

            if (LocalizationManager.CurrentLanguage != language)
            {
                LocalizationManager.SetLanguage(language);
            }
            Debug.Log($"[LanguageSubmenu] Loaded language: {language}");
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
            LanguageSettings.SaveLanguage(language);
            
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

            // Selection is carried entirely by the row fill — no tick/ring indicator.
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
