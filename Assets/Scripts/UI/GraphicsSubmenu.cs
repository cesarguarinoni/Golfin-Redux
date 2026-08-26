using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Golfin.Gameplay.UI.Quality;

namespace Golfin.UI
{
    /// <summary>
    /// Graphics quality submenu — Auto / Low / Medium / High (quality_tiers §5).
    ///
    /// Deliberately a 1:1 copy of <see cref="LanguageSubmenu"/>'s shape (four rows instead of two):
    /// same selected/unselected row fill, same "selection is carried by the row colour, no tick",
    /// same re-sync on every accordion open. There is no Figma for this submenu, so matching the
    /// neighbouring one IS the spec.
    ///
    /// The tier itself lives in <see cref="QualityTierService"/> (PlayerPrefs, like volume and
    /// language) — this view only reads and writes it.
    /// </summary>
    public class GraphicsSubmenu : MonoBehaviour
    {
        [Header("Quality Buttons")]
        [SerializeField] private Button autoButton;
        [SerializeField] private Button lowButton;
        [SerializeField] private Button midButton;
        [SerializeField] private Button highButton;

        [Header("Auto Label")]
        [Tooltip("Optional. When set, the Auto row shows the tier the device resolved to, e.g. \"Auto (High)\".")]
        [SerializeField] private TextMeshProUGUI autoLabel;

        // The composed "Auto (High)" string, rebuilt only when the language or the resolved tier
        // actually moves. See LateUpdate for why it is re-asserted rather than written once.
        private string _autoText;
        private Language _autoTextLanguage;
        private QualityTier _autoTextTier;
        private bool _autoTextValid;

        [Header("Row Colors")]
        [SerializeField] private Color selectedColor   = new Color32(0x33, 0x99, 0xFF, 0xFF);
        [SerializeField] private Color unselectedColor = new Color32(0x26, 0x42, 0x5F, 0xFF);

        private void Awake()
        {
            if (autoButton != null) autoButton.onClick.AddListener(() => OnTierSelected(QualityTierService.AutoPref));
            if (lowButton  != null) lowButton .onClick.AddListener(() => OnTierSelected((int)QualityTier.Low));
            if (midButton  != null) midButton .onClick.AddListener(() => OnTierSelected((int)QualityTier.Mid));
            if (highButton != null) highButton.onClick.AddListener(() => OnTierSelected((int)QualityTier.High));
        }

        private void OnEnable()
        {
            QualityTierService.OnTierChanged += OnTierChangedExternally;

            // While the accordion is collapsed this object is inactive, so it misses any tier change
            // fired in the meantime. Re-sync on every open — same reason as LanguageSubmenu.
            UpdateUI();
        }

        private void OnDisable()
        {
            QualityTierService.OnTierChanged -= OnTierChangedExternally;
        }

        private void OnTierChangedExternally(QualityTier tier)
        {
            UpdateUI();
            Debug.Log($"[GraphicsSubmenu] Tier changed externally to: {tier}");
        }

        /// <summary>-1 = Auto, 0/1/2 = a pinned tier.</summary>
        private void OnTierSelected(int prefValue)
        {
            if (QualityTierService.GetOverridePref() == prefValue)
            {
                Debug.Log($"[GraphicsSubmenu] Quality already selected: pref={prefValue}");
                return;
            }

            // Persists AND applies. Applying is immediate and safe on Home and mid-hole — URP
            // re-reads the pipeline asset next frame (see QualityTierService.Apply).
            QualityTierService.SetOverride(prefValue);

            UpdateUI();   // Apply() only raises OnTierChanged when the tier actually MOVED; Auto->same-tier must still repaint.
            Debug.Log($"[GraphicsSubmenu] Quality set to pref={prefValue} (effective {QualityTierService.Current}).");
        }

        private void UpdateUI()
        {
            int pref = QualityTierService.GetOverridePref();

            UpdateButtonColor(autoButton, pref == QualityTierService.AutoPref);
            UpdateButtonColor(lowButton,  pref == (int)QualityTier.Low);
            UpdateButtonColor(midButton,  pref == (int)QualityTier.Mid);
            UpdateButtonColor(highButton, pref == (int)QualityTier.High);

            ApplyAutoLabel();
        }

        /// <summary>
        /// "Auto (High)" — the resolved tier is the one thing a player cannot otherwise see, and it
        /// is what makes a support conversation about "my phone runs it badly" possible.
        ///
        /// The Auto row's Label keeps its <c>LocalizedText</c> (so it is localised and font-scaled
        /// like its three siblings), which means LocalizedText also writes this TMP on every
        /// language change. Subscriber order between the two is not defined, so rather than racing
        /// it, the composed string is simply RE-ASSERTED while the submenu is open — the accordion
        /// keeps this object inactive the rest of the time, so LateUpdate does not run at all until
        /// the player opens Graphics, and the string itself is only rebuilt when the language or
        /// resolved tier changes.
        /// </summary>
        private void LateUpdate() => ApplyAutoLabel();

        private void ApplyAutoLabel()
        {
            if (autoLabel == null) return;

            var language = LocalizationManager.CurrentLanguage;
            var tier = QualityTierService.AutoTier;

            if (!_autoTextValid || _autoTextLanguage != language || _autoTextTier != tier)
            {
                _autoText = $"{LocalizationManager.Get("SETTINGS_QUALITY_AUTO")} ({LocalizationManager.Get(TierKey(tier))})";
                _autoTextLanguage = language;
                _autoTextTier = tier;
                _autoTextValid = true;
            }

            if (autoLabel.text != _autoText) autoLabel.text = _autoText;
        }

        private static string TierKey(QualityTier tier)
        {
            switch (tier)
            {
                case QualityTier.Low:  return "SETTINGS_QUALITY_LOW";
                case QualityTier.High: return "SETTINGS_QUALITY_HIGH";
                default:               return "SETTINGS_QUALITY_MID";
            }
        }

        private void UpdateButtonColor(Button button, bool isSelected)
        {
            if (button == null) return;

            Image buttonImage = button.GetComponent<Image>();
            if (buttonImage != null)
            {
                buttonImage.color = isSelected ? selectedColor : unselectedColor;
            }
        }
    }
}
