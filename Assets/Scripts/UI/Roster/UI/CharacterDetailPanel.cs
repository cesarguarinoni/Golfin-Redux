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

        [Header("Modals")]
        [SerializeField] private LevelUpModalController? levelUpModal;
        [SerializeField] private RectTransform? levelUpAnchorPanel; // wire to RightPanel

        [Header("Compare")]
        [SerializeField] private CompareController? compareController;

        [Header("Carousel Reference")]
        [SerializeField] private CarouselController? carousel;

        // Bar colours removed — Image colours are set on the sprites in the Editor

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
        /// Main data binding — populates all UI fields from character data.
        /// Skipped while CompareController is in compare mode (it handles
        /// carousel taps itself in that state).
        /// </summary>
        private void UpdatePanel(string characterId)
        {
            // In compare mode the CompareController intercepts carousel taps;
            // we must not overwrite the left column while it is managed by compare.
            if (compareController != null && compareController.IsCompareMode) return;

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

            // --- Status Icons ---
            if (selectedIcon != null)
                selectedIcon.SetActive(playerData.isSelected);
            if (lowStaminaIcon != null)
                lowStaminaIcon.SetActive(playerData.IsStaminaLow(LOW_STAMINA_THRESHOLD));

            // --- Button States ---
            if (levelUpButton != null)
            {
                bool atMax = playerData.currentLevel >= maxLevel;
                bool canAfford = RewardPointsManager.Instance != null
                    && RewardPointsManager.Instance.CanAfford(CharacterManager.Instance.GetLevelUpCost(characterId));
                levelUpButton.interactable = !atMax && canAfford;
            }
            if (boostButton != null)
                boostButton.interactable = false; // until boost system exists

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
                bar.fillAmount = capValue > 0 ? (float)currentValue / capValue : 0f;
            // Bar colour left as-is on the Image — set in Editor
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

        /// <summary>
        /// Called by CompareController after a swap so the panel switches
        /// to displaying the newly selected character.
        /// </summary>
        public void ShowCharacter(string characterId)
        {
            currentCharacterId = characterId;
            UpdatePanel(characterId);
        }

        private void UpdateSelectButton(bool isSelected)
        {
            if (selectButtonText != null)
                selectButtonText.text = isSelected ? "SELECTED" : "SELECT";

            if (selectButton != null)
                selectButton.interactable = !isSelected;
            // Button visual state handled by Color Tint transition — do NOT set Image.color here
        }

        // --- Button Click Handlers ---

        private void OnLevelUpClicked()
        {
            if (levelUpModal != null && !string.IsNullOrEmpty(currentCharacterId))
                levelUpModal.Open(currentCharacterId, levelUpAnchorPanel);
        }

        private void OnBoostClicked()
        {
            Debug.Log($"[CharacterDetailPanel] Boost clicked for {currentCharacterId}");
            // Future: Open Experience Boost modal
        }

        private void OnCompareClicked()
        {
            if (compareController != null && !string.IsNullOrEmpty(currentCharacterId))
                compareController.EnterCompareMode(currentCharacterId);
        }

        private void OnSelectClicked()
        {
            if (string.IsNullOrEmpty(currentCharacterId)) return;

            Debug.Log($"[CharacterDetailPanel] Select clicked for {currentCharacterId}");
            CharacterManager.Instance.SelectCharacter(currentCharacterId);
        }
    }
}
