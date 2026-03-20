#nullable enable
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Golfin.Inventory
{
    /// <summary>
    /// Data-binding controller for the Club Detail Panel.
    /// Subscribes to ClubCarouselController.OnClubSelected and populates all
    /// UI fields from ClubManager / ClubDatabaseCSV data.
    ///
    /// Layout:
    ///   LeftPanel  — club image (top) + INFO text (bottom)
    ///   RightPanel — name, rarity/level, 5 stat bars, distance, buttons, equip
    /// </summary>
    public class ClubDetailPanel : MonoBehaviour
    {
        // ── Left Panel ─────────────────────────────────────────────────────────

        [Header("Left Panel")]
        [SerializeField] private Image clubImage = null!;
        [SerializeField] private TextMeshProUGUI infoHeader = null!;
        [SerializeField] private TextMeshProUGUI infoText   = null!;

        // ── Right Panel — Name & Level ─────────────────────────────────────────

        [Header("Right Panel — Name & Level")]
        [SerializeField] private TextMeshProUGUI clubNameText     = null!;
        [SerializeField] private TextMeshProUGUI rarityLabel      = null!;
        [SerializeField] private TextMeshProUGUI currentLevelText = null!;
        [SerializeField] private TextMeshProUGUI maxLevelText     = null!;

        // ── Stat Bars ──────────────────────────────────────────────────────────

        [Header("Stat — Power")]
        [SerializeField] private TextMeshProUGUI powerName   = null!;
        [SerializeField] private Image           powerBar    = null!;
        [SerializeField] private TextMeshProUGUI powerNumber = null!;

        [Header("Stat — Accuracy")]
        [SerializeField] private TextMeshProUGUI accuracyName   = null!;
        [SerializeField] private Image           accuracyBar    = null!;
        [SerializeField] private TextMeshProUGUI accuracyNumber = null!;

        [Header("Stat — Lie Resistance")]
        [SerializeField] private TextMeshProUGUI lieResistanceName   = null!;
        [SerializeField] private Image           lieResistanceBar    = null!;
        [SerializeField] private TextMeshProUGUI lieResistanceNumber = null!;

        [Header("Stat — Loft")]
        [SerializeField] private TextMeshProUGUI loftName   = null!;
        [SerializeField] private Image           loftBar    = null!;
        [SerializeField] private TextMeshProUGUI loftNumber = null!;

        [Header("Stat — Durability")]
        [SerializeField] private TextMeshProUGUI durabilityName   = null!;
        [SerializeField] private Image           durabilityBar    = null!;
        [SerializeField] private TextMeshProUGUI durabilityNumber = null!;

        [Header("Stat — Distance (no bar)")]
        [SerializeField] private TextMeshProUGUI distanceName  = null!;
        [SerializeField] private TextMeshProUGUI distanceValue = null!;

        // ── Buttons ────────────────────────────────────────────────────────────

        [Header("Buttons")]
        [SerializeField] private Button          levelUpButton  = null!;
        [SerializeField] private Button          repairButton   = null!;
        [SerializeField] private Button          compareButton  = null!;
        [SerializeField] private Button          equipButton    = null!;
        [SerializeField] private TextMeshProUGUI equipButtonText = null!;
        [SerializeField] private TextMeshProUGUI bagLabel       = null!;

        // ── Status Icons ───────────────────────────────────────────────────────

        [Header("Status Icons (wire in Inspector)")]
        [SerializeField] private GameObject? equippedIcon;

        // ── Carousel Reference ─────────────────────────────────────────────────

        [Header("Carousel")]
        [SerializeField] private ClubCarouselController? carousel;

        // ── State ──────────────────────────────────────────────────────────────

        private string currentClubId = "";

        private static readonly Color DurabilityLowColor = new Color(0.9f, 0.2f, 0.2f, 1f); // red
        private static readonly Color DurabilityOkColor  = new Color(0.2f, 0.5f, 0.9f, 1f); // blue
        private static readonly Color EquippedGoldColor  = new Color(1f,   0.8f, 0.2f, 1f);

        private const int STAT_MAX = 100; // all non-durability bars fill against 100

        // ── Lifecycle ──────────────────────────────────────────────────────────

        private void Start()
        {
            if (levelUpButton != null) levelUpButton.onClick.AddListener(OnLevelUpClicked);
            if (repairButton  != null) repairButton.onClick.AddListener(OnRepairClicked);
            if (compareButton != null) compareButton.onClick.AddListener(OnCompareClicked);
            if (equipButton   != null) equipButton.onClick.AddListener(OnEquipClicked);
        }

        private void OnEnable()
        {
            if (carousel != null)
                carousel.OnClubSelected += UpdatePanel;

            if (ClubManager.Instance != null)
                ClubManager.Instance.OnClubEquipped += OnClubEquippedChanged;

            LocalizationManager.OnLanguageChanged += RefreshLocalizedText;
        }

        private void OnDisable()
        {
            if (carousel != null)
                carousel.OnClubSelected -= UpdatePanel;

            if (ClubManager.Instance != null)
                ClubManager.Instance.OnClubEquipped -= OnClubEquippedChanged;

            LocalizationManager.OnLanguageChanged -= RefreshLocalizedText;
        }

        private void RefreshLocalizedText()
        {
            if (!string.IsNullOrEmpty(currentClubId))
                UpdatePanel(currentClubId);
        }

        // ── Data Binding ───────────────────────────────────────────────────────

        private void UpdatePanel(string clubId)
        {
            currentClubId = clubId;

            var playerClub = ClubManager.Instance == null
                ? null
                : ClubManager.Instance.GetClubData(clubId);
            if (playerClub == null) return;

            var template = ClubDatabaseCSV.Instance == null
                ? null
                : ClubDatabaseCSV.Instance.GetClub(clubId);
            if (template == null) return;

            // ── Club Image ────────────────────────────────────────────────────
            if (clubImage != null)
            {
                if (template.portraitFull != null)
                    clubImage.sprite = template.portraitFull;
                else if (template.portraitSprite != null)
                    clubImage.sprite = template.portraitSprite;
            }

            // ── Name ─────────────────────────────────────────────────────────
            if (clubNameText != null)
                clubNameText.text = template.name.ToUpper();

            // ── Rarity ───────────────────────────────────────────────────────
            var rarity = template.rarity;
            if (rarityLabel != null)
            {
                rarityLabel.text  = LocalizationManager.Get($"RARITY_{rarity.ToString().ToUpper()}");
                rarityLabel.color = Golfin.Roster.RarityHelper.GetRarityColor(rarity);
            }

            // ── Level ────────────────────────────────────────────────────────
            if (currentLevelText != null)
                currentLevelText.text = $"Lv {playerClub.currentLevel}";

            int maxLevel = ClubManager.Instance == null
                ? template.maxLevel
                : ClubManager.Instance.GetMaxLevel(clubId);
            if (maxLevelText != null)
                maxLevelText.text = $"/{maxLevel}";

            // ── INFO text ─────────────────────────────────────────────────────
            if (infoHeader != null)
                infoHeader.text = LocalizationManager.Get("CLUB_INFO");
            if (infoText != null)
                infoText.text = template.info;

            // ── Stat Bars ─────────────────────────────────────────────────────
            UpdateStatBar(powerName, powerBar, powerNumber,
                LocalizationManager.Get("CLUB_POWER"),
                playerClub.GetPower(template), STAT_MAX);

            UpdateStatBar(accuracyName, accuracyBar, accuracyNumber,
                LocalizationManager.Get("CLUB_ACCURACY"),
                playerClub.GetAccuracy(template), STAT_MAX);

            UpdateStatBar(lieResistanceName, lieResistanceBar, lieResistanceNumber,
                LocalizationManager.Get("CLUB_LIE_RESISTANCE"),
                playerClub.GetLieResistance(template), STAT_MAX);

            UpdateStatBar(loftName, loftBar, loftNumber,
                LocalizationManager.Get("CLUB_LOFT"),
                playerClub.GetLoft(template), STAT_MAX);

            // ── Durability (current/max, turns red when low) ──────────────────
            int curDur = playerClub.currentDurability;
            int maxDur = playerClub.maxDurability;

            if (durabilityName   != null) durabilityName.text   = LocalizationManager.Get("CLUB_DURABILITY");
            if (durabilityNumber != null) durabilityNumber.text = $"{curDur}/{maxDur}";
            if (durabilityBar    != null)
            {
                durabilityBar.fillAmount = maxDur > 0 ? (float)curDur / maxDur : 0f;
                durabilityBar.color = playerClub.IsDurabilityLow ? DurabilityLowColor : DurabilityOkColor;
            }

            // ── Distance (value only, no bar) ─────────────────────────────────
            if (distanceName  != null) distanceName.text  = LocalizationManager.Get("CLUB_DISTANCE");
            if (distanceValue != null) distanceValue.text = $"{playerClub.GetDistance(template)} yd";

            // ── Equip Button ──────────────────────────────────────────────────
            bool isEquipped = playerClub.IsEquipped;

            if (equipButtonText != null)
                equipButtonText.text = isEquipped
                    ? LocalizationManager.Get("CLUB_EQUIPPED")
                    : LocalizationManager.Get("CLUB_EQUIP");

            var equipImg = equipButton == null ? null : equipButton.GetComponent<Image>();
            if (equipImg != null)
                equipImg.color = isEquipped ? EquippedGoldColor : Color.white;

            if (bagLabel != null)
            {
                bagLabel.gameObject.SetActive(isEquipped);
                if (isEquipped)
                    bagLabel.text = $"{LocalizationManager.Get("CLUB_IN_BAG")} {playerClub.equippedBagSlot}";
            }

            // ── Status Icons ──────────────────────────────────────────────────
            if (equippedIcon != null) equippedIcon.SetActive(isEquipped);

            // ── Button States ─────────────────────────────────────────────────
            bool atMax = playerClub.currentLevel >= maxLevel;
            if (levelUpButton != null) levelUpButton.interactable = !atMax;
            if (repairButton  != null)
                repairButton.interactable = playerClub.currentDurability < playerClub.maxDurability;
        }

        private void UpdateStatBar(TextMeshProUGUI? nameField, Image? bar,
            TextMeshProUGUI? numberField, string label, int value, int cap)
        {
            if (nameField   != null) nameField.text   = label;
            if (numberField != null) numberField.text = $"{value}/{cap}";
            if (bar         != null) bar.fillAmount   = cap > 0 ? (float)value / cap : 0f;
        }

        // ── Event Handlers ─────────────────────────────────────────────────────

        private void OnClubEquippedChanged(string _)
        {
            if (!string.IsNullOrEmpty(currentClubId))
                UpdatePanel(currentClubId);
        }

        // ── Button Handlers ────────────────────────────────────────────────────

        private void OnLevelUpClicked()
        {
            Debug.Log($"[ClubDetailPanel] LEVEL UP clicked for '{currentClubId}' — modal in Phase D.");
            ClubManager.Instance?.LevelUp(currentClubId);
        }

        private void OnRepairClicked()
        {
            Debug.Log($"[ClubDetailPanel] REPAIR clicked for '{currentClubId}' — modal in Phase D.");
            ClubManager.Instance?.Repair(currentClubId);
        }

        private void OnCompareClicked()
        {
            Debug.Log($"[ClubDetailPanel] COMPARE clicked for '{currentClubId}' — Phase D.");
        }

        private void OnEquipClicked()
        {
            if (string.IsNullOrEmpty(currentClubId) || ClubManager.Instance == null) return;

            var playerClub = ClubManager.Instance.GetClubData(currentClubId);
            if (playerClub == null) return;

            // Toggle equip: if already equipped, unequip (bagSlot 0); else equip to Bag 1
            ClubManager.Instance.EquipClub(currentClubId, playerClub.IsEquipped ? 0 : 1);
            // Panel refreshes via OnClubEquippedChanged event
        }
    }
}
