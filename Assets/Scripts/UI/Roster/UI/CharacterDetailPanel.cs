#nullable enable
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Golfin.Roster
{
    /// <summary>
    /// Data-binding controller for the character detail panel.
    /// Populates existing UI hierarchy from CharacterManager data
    /// when a carousel card is tapped.
    ///
    /// Phase 2b: Pure data binding — does NOT modify hierarchy.
    /// </summary>
    public class CharacterDetailPanel : MonoBehaviour
    {
        [Header("Portrait")]
        [SerializeField] private Image characterImage;           // LeftPanel > Character

        [Header("Name")]
        [SerializeField] private TextMeshProUGUI characterNameText; // CharacterNamePanel > CharacterNameText

        [Header("Rarity & Level")]
        [SerializeField] private TextMeshProUGUI rarityLabel;     // RarityRow child 0
        [SerializeField] private TextMeshProUGUI currentLevelText; // RarityRow child 1
        [SerializeField] private TextMeshProUGUI maxLevelText;     // RarityRow child 2

        [Header("Stat Bars — Strength")]
        [SerializeField] private TextMeshProUGUI strengthName;     // CharacterStats1 > Name+Bar > StatsName
        [SerializeField] private Image strengthBar;                // CharacterStats1 > Name+Bar > Bar
        [SerializeField] private TextMeshProUGUI strengthNumber;   // CharacterStats1 > StatNumber

        [Header("Stat Bars — Club Control")]
        [SerializeField] private TextMeshProUGUI clubControlName;
        [SerializeField] private Image clubControlBar;
        [SerializeField] private TextMeshProUGUI clubControlNumber;

        [Header("Stat Bars — Recovery")]
        [SerializeField] private TextMeshProUGUI recoveryName;
        [SerializeField] private Image recoveryBar;
        [SerializeField] private TextMeshProUGUI recoveryNumber;

        [Header("Stat Bars — Stamina")]
        [SerializeField] private TextMeshProUGUI staminaName;
        [SerializeField] private Image staminaBar;
        [SerializeField] private TextMeshProUGUI staminaNumber;

        [Header("Buttons")]
        [SerializeField] private Button levelUpButton;
        [SerializeField] private Button boostButton;
        [SerializeField] private Button compareButton;
        [SerializeField] private Button selectButton;
        [SerializeField] private TextMeshProUGUI selectButtonText;  // SelectButton > Text (TMP)

        [Header("Bio")]
        [SerializeField] private TextMeshProUGUI bioText;           // BioPanel > BioText

        [Header("Status Icons (Optional — add when ready)")]
        [SerializeField] private GameObject? selectedIcon;            // Eye icon, null until added
        [SerializeField] private GameObject? lowStaminaIcon;          // Bolt icon, null until added

        [Header("Carousel Reference")]
        [SerializeField] private CarouselController? carousel;

        [Header("Colors")]
        [SerializeField] private Color normalBarColor = new Color(0.2f, 0.6f, 1f, 1f);    // Blue
        [SerializeField] private Color criticalBarColor = new Color(0.9f, 0.2f, 0.2f, 1f); // Red
        [SerializeField] private Color maxBarColor = new Color(0.2f, 1f, 0.4f, 1f);        // Green

        private string currentCharacterId = "";
        private const float LOW_STAMINA_THRESHOLD = 0.25f;

        private void Start()
        {
            if (levelUpButton != null) levelUpButton.onClick.AddListener(OnLevelUpClicked);
            if (boostButton != null) boostButton.onClick.AddListener(OnBoostClicked);
            if (compareButton != null) compareButton.onClick.AddListener(OnCompareClicked);
            if (selectButton != null) selectButton.onClick.AddListener(OnSelectClicked);
        }

        private void OnEnable()
        {
            if (carousel != null)
                carousel.OnCharacterSelected += UpdatePanel;

            if (CharacterManager.Instance != null)
            {
                CharacterManager.Instance.OnCharacterLeveledUp += OnLeveledUp;
                CharacterManager.Instance.OnCharacterSelected += OnSelectionChanged;
            }
        }

        private void OnDisable()
        {
            if (carousel != null)
                carousel.OnCharacterSelected -= UpdatePanel;

            if (CharacterManager.Instance != null)
            {
                CharacterManager.Instance.OnCharacterLeveledUp -= OnLeveledUp;
                CharacterManager.Instance.OnCharacterSelected -= OnSelectionChanged;
            }
        }

        /// <summary>
        /// Main data binding — populates all UI fields from character data
        /// </summary>
        private void UpdatePanel(string characterId)
        {
            currentCharacterId = characterId;

            var playerData = CharacterManager.Instance.GetCharacterData(characterId);
            if (playerData == null) return;

            // Get template data (try CSV first, then ScriptableObject)
            var csvData = CharacterDatabaseCSV.Instance?.GetCharacter(characterId);
            var soData = CharacterManager.Instance.GetCharacterTemplate(characterId);

            // --- Portrait (CSV full-body first, then SO full, then CSV thumbnail) ---
            if (characterImage != null)
            {
                if (csvData?.portraitFullSprite != null)
                    characterImage.sprite = csvData.portraitFullSprite;
                else if (soData != null && soData.portraitFull != null)
                    characterImage.sprite = soData.portraitFull;
                else if (csvData?.portraitSprite != null)
                    characterImage.sprite = csvData.portraitSprite;
            }

            // --- Name (single TMP, line break for first/last) ---
            if (characterNameText != null)
            {
                if (csvData != null)
                {
                    characterNameText.text = csvData.GetDisplayName();
                }
                else if (soData != null)
                {
                    characterNameText.text = string.IsNullOrEmpty(soData.characterLastName)
                        ? soData.characterName.ToUpper()
                        : $"{soData.characterName.ToUpper()}\n{soData.characterLastName.ToUpper()}";
                }
            }

            // --- Rarity ---
            CharacterRarity rarity = csvData?.rarity ?? soData?.rarity ?? CharacterRarity.Common;

            if (rarityLabel != null)
            {
                rarityLabel.text = rarity.ToString().ToUpper();
                rarityLabel.color = RarityHelper.GetRarityColor(rarity);
            }

            // --- Level ---
            if (currentLevelText != null)
                currentLevelText.text = $"Lv {playerData.currentLevel}";

            int maxLevel = CharacterManager.Instance.GetMaxLevel(characterId);
            if (maxLevelText != null)
                maxLevelText.text = $"/{maxLevel}";

            // --- Stats ---
            UpdateStatBar(strengthName, strengthBar, strengthNumber, "STRENGTH",
                playerData.currentStrength, RarityStatCaps.GetStatCap(rarity, "Strength"));

            UpdateStatBar(clubControlName, clubControlBar, clubControlNumber, "CLUB CONTROL",
                playerData.currentClubControl, RarityStatCaps.GetStatCap(rarity, "ClubControl"));

            UpdateStatBar(recoveryName, recoveryBar, recoveryNumber, "RECOVERY",
                playerData.currentRecovery, RarityStatCaps.GetStatCap(rarity, "Recovery"));

            UpdateStatBar(staminaName, staminaBar, staminaNumber, "STAMINA",
                playerData.currentStamina, RarityStatCaps.GetStatCap(rarity, "Stamina"));

            // Override stamina bar color if energy is low
            if (playerData.IsStaminaLow(LOW_STAMINA_THRESHOLD) && staminaBar != null)
            {
                staminaBar.color = criticalBarColor;
            }

            // --- Status Icons ---
            if (selectedIcon != null)
                selectedIcon.SetActive(playerData.isSelected);
            if (lowStaminaIcon != null)
                lowStaminaIcon.SetActive(playerData.IsStaminaLow(LOW_STAMINA_THRESHOLD));

            // --- Select Button ---
            UpdateSelectButton(playerData.isSelected);

            // --- Bio ---
            if (bioText != null)
            {
                if (csvData != null && !string.IsNullOrEmpty(csvData.bio))
                    bioText.text = csvData.bio;
                else if (soData != null && !string.IsNullOrEmpty(soData.bioFallbackText))
                    bioText.text = soData.bioFallbackText;
                else
                    bioText.text = "Bio coming soon.";
            }
        }

        private void UpdateStatBar(TextMeshProUGUI nameField, Image bar, TextMeshProUGUI numberField,
            string label, int currentValue, int capValue)
        {
            if (nameField != null)
                nameField.text = label;

            if (numberField != null)
                numberField.text = $"{currentValue}/{capValue}";

            if (bar != null)
            {
                float fillAmount = capValue > 0 ? (float)currentValue / capValue : 0f;
                bar.fillAmount = fillAmount;

                if (fillAmount >= 1f)
                    bar.color = maxBarColor;
                else
                    bar.color = normalBarColor;
            }
        }

        // --- Event Handlers ---

        private void OnLeveledUp(string characterId)
        {
            if (characterId == currentCharacterId)
                UpdatePanel(characterId);
        }

        private void OnSelectionChanged(string characterId)
        {
            // Refresh to update SELECT/SELECTED state
            if (!string.IsNullOrEmpty(currentCharacterId))
                UpdatePanel(currentCharacterId);
        }

        private void UpdateSelectButton(bool isSelected)
        {
            if (selectButtonText != null)
                selectButtonText.text = isSelected ? "SELECTED" : "SELECT";
        }

        // --- Button Click Handlers ---

        private void OnLevelUpClicked()
        {
            Debug.Log($"[CharacterDetailPanel] Level Up clicked for {currentCharacterId}");
            // Phase 2c: Open LevelUpModal
        }

        private void OnBoostClicked()
        {
            Debug.Log($"[CharacterDetailPanel] Boost clicked for {currentCharacterId}");
            // Future: Open Experience Boost modal
        }

        private void OnCompareClicked()
        {
            Debug.Log($"[CharacterDetailPanel] Compare clicked for {currentCharacterId}");
            // Phase 2d: Enter compare mode
        }

        private void OnSelectClicked()
        {
            if (string.IsNullOrEmpty(currentCharacterId)) return;

            Debug.Log($"[CharacterDetailPanel] Select clicked for {currentCharacterId}");
            CharacterManager.Instance.SelectCharacter(currentCharacterId);
        }
    }
}
