#nullable enable
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using Golfin.Roster;       // CharacterLevelUpDatabase, RarityHelper
using Golfin.UI.Modals;

namespace Golfin.Inventory
{
    /// <summary>
    /// Club Level Up Modal — Phase E1.
    ///
    /// Mirrors LevelUpModalController (character version) closely.
    /// Key differences vs character modal:
    ///   • 4 allocatable stats: Power, Accuracy, Lie Resistance, Durability
    ///   • Loft is fixed — display only, no [+] button, no pending bar
    ///   • Stat cap = baseStat + MAX_SP_PER_STAT (flat 20, not rarity-based)
    ///   • Durability SP increases maxDurability (+5 per SP point)
    ///   • Confirm uses ClubManager.SetLevel + RefreshStatValues (not LevelUp per-level)
    ///
    /// All state is LOCAL until CONFIRM. CANCEL discards everything.
    /// CONFIRM is only enabled when availableSP == 0 AND totalPending > 0.
    /// </summary>
    public class ClubLevelUpModalController : ModalController
    {
        // ── Club Info ────────────────────────────────────────────────────────
        [Header("Club Info")]
        [SerializeField] private TextMeshProUGUI clubNameText   = null!;
        [SerializeField] private TextMeshProUGUI rarityLabel    = null!;
        [SerializeField] private TextMeshProUGUI levelText      = null!;
        [SerializeField] private TextMeshProUGUI nextLevelValue = null!;
        [SerializeField] private TextMeshProUGUI costValue      = null!;
        [SerializeField] private TextMeshProUGUI rewardValue    = null!;

        // ── Level Up Button ──────────────────────────────────────────────────
        [Header("Level Up")]
        [SerializeField] private Button levelUpButton = null!;

        // ── Available SP ─────────────────────────────────────────────────────
        [Header("SP Allocation")]
        [SerializeField] private TextMeshProUGUI availableSPValue = null!;

        // ── Stat Rows (allocatable) ──────────────────────────────────────────
        [Header("Stat Row — Power")]
        [SerializeField] private Image           powerBar             = null!;
        [SerializeField] private Image           powerBarPending      = null!;
        [SerializeField] private TextMeshProUGUI powerValueCurrent    = null!;
        [SerializeField] private TextMeshProUGUI powerValueMax        = null!;
        [SerializeField] private TextMeshProUGUI powerPending         = null!;
        [SerializeField] private Button          powerPlusButton      = null!;

        [Header("Stat Row — Accuracy")]
        [SerializeField] private Image           accuracyBar             = null!;
        [SerializeField] private Image           accuracyBarPending      = null!;
        [SerializeField] private TextMeshProUGUI accuracyValueCurrent    = null!;
        [SerializeField] private TextMeshProUGUI accuracyValueMax        = null!;
        [SerializeField] private TextMeshProUGUI accuracyPending         = null!;
        [SerializeField] private Button          accuracyPlusButton      = null!;

        [Header("Stat Row — Lie Resistance")]
        [SerializeField] private Image           lieResBar             = null!;
        [SerializeField] private Image           lieResBarPending      = null!;
        [SerializeField] private TextMeshProUGUI lieResValueCurrent    = null!;
        [SerializeField] private TextMeshProUGUI lieResValueMax        = null!;
        [SerializeField] private TextMeshProUGUI lieResPending         = null!;
        [SerializeField] private Button          lieResPlusButton      = null!;

        // ── Stat Row — Loft (fixed, no + button) ────────────────────────────
        [Header("Stat Row — Loft (fixed)")]
        [SerializeField] private Image           loftBar          = null!;
        [SerializeField] private TextMeshProUGUI loftValueCurrent = null!;
        [SerializeField] private TextMeshProUGUI loftValueMax     = null!;

        [Header("Stat Row — Durability")]
        [SerializeField] private Image           durabilityBar             = null!;
        [SerializeField] private Image           durabilityBarPending      = null!;
        [SerializeField] private TextMeshProUGUI durabilityValueCurrent    = null!;
        [SerializeField] private TextMeshProUGUI durabilityValueMax        = null!;
        [SerializeField] private TextMeshProUGUI durabilityPending         = null!;
        [SerializeField] private Button          durabilityPlusButton      = null!;

        // ── Action Buttons ───────────────────────────────────────────────────
        [Header("Actions")]
        [SerializeField] private Button resetButton   = null!;
        [SerializeField] private Button cancelButton  = null!;
        [SerializeField] private Button confirmButton = null!;

        // ── Static Labels (localized) ────────────────────────────────────────
        [Header("Static Labels")]
        [SerializeField] private TextMeshProUGUI nextLevelLabel     = null!;
        [SerializeField] private TextMeshProUGUI costLabel          = null!;
        [SerializeField] private TextMeshProUGUI rewardLabel        = null!;
        [SerializeField] private TextMeshProUGUI levelUpButtonLabel = null!;
        [SerializeField] private TextMeshProUGUI availableSPLabel   = null!;
        [SerializeField] private TextMeshProUGUI resetButtonLabel   = null!;
        [SerializeField] private TextMeshProUGUI cancelButtonLabel  = null!;
        [SerializeField] private TextMeshProUGUI confirmButtonLabel = null!;

        // ── Colors ───────────────────────────────────────────────────────────
        [Header("Colors")]
        [SerializeField] private Color pendingBarColor  = new Color(1f, 0.7f, 0.2f, 1f);        // orange
        [SerializeField] private Color greenTextColor   = new Color(0.2f, 0.8f, 0.2f, 1f);       // green — reward
        [SerializeField] private Color spAvailableColor = new Color(1f, 0.525f, 0.110f, 1f);     // #FF861C
        [SerializeField] private Color spDepletedColor  = new Color(0.753f, 0.251f, 0f, 1f);     // #C04000
        [SerializeField] private Color levelTextColor   = new Color(0.153f, 0.459f, 0.867f, 1f); // #2775DD

        // ── Anchor reposition ────────────────────────────────────────────────
        private Vector3? _savedModalLocalPos;

        // ── Preview State ────────────────────────────────────────────────────
        private string clubId            = "";
        private int    previewLevel;
        private int    previewTotalSPEarned;
        private int    totalRPCost;

        // ── Pending SP ───────────────────────────────────────────────────────
        private int pendingPower;
        private int pendingAccuracy;
        private int pendingLieRes;
        private int pendingDurability;

        // ───────────────────────────────────────────────────────────────────
        // Lifecycle
        // ───────────────────────────────────────────────────────────────────

        protected override void Awake()
        {
            base.Awake();
        }

        private void OnEnable()
        {
            LocalizationManager.OnLanguageChanged += RefreshLocalizedText;
        }

        private void OnDisable()
        {
            LocalizationManager.OnLanguageChanged -= RefreshLocalizedText;
        }

        private void Start()
        {
            levelUpButton?.onClick.AddListener(OnLevelUpClicked);
            powerPlusButton?.onClick.AddListener(OnPowerPlus);
            accuracyPlusButton?.onClick.AddListener(OnAccuracyPlus);
            lieResPlusButton?.onClick.AddListener(OnLieResPlus);
            durabilityPlusButton?.onClick.AddListener(OnDurabilityPlus);
            resetButton?.onClick.AddListener(OnResetClicked);
            cancelButton?.onClick.AddListener(OnCancelClicked);
            confirmButton?.onClick.AddListener(OnConfirmClicked);
        }

        // ───────────────────────────────────────────────────────────────────
        // Public API
        // ───────────────────────────────────────────────────────────────────

        /// <summary>
        /// Opens the modal for the given club.
        /// Pass anchorPanel to centre the modal over a specific panel (e.g. RightPanel).
        /// </summary>
        public void Open(string clubId, RectTransform? anchorPanel = null)
        {
            this.clubId = clubId;

            // Reposition modalPanel over anchorPanel without reparenting
            if (anchorPanel != null && modalPanel != null)
            {
                Vector3 anchorWorldCentre = anchorPanel.TransformPoint(anchorPanel.rect.center);
                Vector3 targetLocal       = transform.InverseTransformPoint(anchorWorldCentre);
                targetLocal.z = 0f;

                _savedModalLocalPos                = modalPanel.transform.localPosition;
                modalPanel.transform.localPosition = targetLocal;
            }

            var playerClub = ClubManager.Instance == null ? null : ClubManager.Instance.GetClubData(clubId);
            if (playerClub == null) return;

            // Seed preview from current state
            previewLevel         = playerClub.currentLevel;
            previewTotalSPEarned = playerClub.totalSPEarned;
            totalRPCost          = 0;

            pendingPower      = 0;
            pendingAccuracy   = 0;
            pendingLieRes     = 0;
            pendingDurability = 0;

            if (levelText != null) levelText.color = Color.white;

            RefreshLocalizedText();
            Show();
        }

        // ───────────────────────────────────────────────────────────────────
        // Display
        // ───────────────────────────────────────────────────────────────────

        private void RefreshLocalizedText()
        {
            if (nextLevelLabel     != null) nextLevelLabel.text     = LocalizationManager.Get("CLUB_MODAL_NEXT_LEVEL");
            if (costLabel          != null) costLabel.text          = LocalizationManager.Get("CLUB_MODAL_COST");
            if (rewardLabel        != null) rewardLabel.text        = LocalizationManager.Get("CLUB_MODAL_REWARD");
            if (levelUpButtonLabel != null) levelUpButtonLabel.text = LocalizationManager.Get("CLUB_MODAL_LEVEL_UP");
            if (availableSPLabel   != null) availableSPLabel.text   = LocalizationManager.Get("CLUB_MODAL_AVAILABLE_SP");
            if (resetButtonLabel   != null) resetButtonLabel.text   = LocalizationManager.Get("CLUB_MODAL_RESET");
            if (cancelButtonLabel  != null) cancelButtonLabel.text  = LocalizationManager.Get("CLUB_MODAL_CANCEL");
            if (confirmButtonLabel != null) confirmButtonLabel.text = LocalizationManager.Get("CLUB_MODAL_CONFIRM");

            if (!string.IsNullOrEmpty(clubId))
                RefreshDisplay();
        }

        private void RefreshDisplay()
        {
            if (ClubManager.Instance == null || ClubDatabaseCSV.Instance == null) return;

            var playerClub = ClubManager.Instance.GetClubData(clubId);
            var template   = ClubDatabaseCSV.Instance.GetClub(clubId);
            if (playerClub == null || template == null)
            {
                Debug.LogError($"[ClubLevelUpModal] RefreshDisplay: data null for '{clubId}'");
                return;
            }

            if (CharacterLevelUpDatabase.Instance == null)
            {
                Debug.LogError("[ClubLevelUpModal] CharacterLevelUpDatabase not found — modal cannot display.");
                return;
            }

            // ── Header ──────────────────────────────────────────────────────
            if (clubNameText != null)
                clubNameText.text = template.name.ToUpper();

            var rarity = template.rarity;
            if (rarityLabel != null)
            {
                rarityLabel.text  = LocalizationManager.Get($"RARITY_{rarity.ToString().ToUpper()}");
                rarityLabel.color = RarityHelper.GetRarityColor(rarity);
            }

            int maxLevel = ClubManager.Instance.GetMaxLevel(clubId);
            if (levelText != null)
            {
                levelText.text = $"Lv {previewLevel}/{maxLevel}";
                if (previewLevel > playerClub.currentLevel)
                    levelText.color = levelTextColor;
            }

            // ── Next Level / Cost / Reward ───────────────────────────────────
            bool isMaxLevel = previewLevel >= maxLevel;
            int  nextLevel  = previewLevel + 1;

            if (isMaxLevel)
            {
                if (nextLevelValue != null) nextLevelValue.text = LocalizationManager.Get("CLUB_MODAL_MAX");
                if (costValue      != null) costValue.text      = "-";
                if (rewardValue    != null) rewardValue.text    = "-";
                if (levelUpButton  != null) levelUpButton.interactable = false;
            }
            else
            {
                int nextCost       = CharacterLevelUpDatabase.Instance.GetLevelUpCost(nextLevel);
                int spReward       = CharacterLevelUpDatabase.Instance.GetSPReward(nextLevel);
                int totalIfLevelUp = totalRPCost + nextCost;
                bool canAffordNext = RewardPointsManager.Instance != null &&
                                     RewardPointsManager.Instance.CanAfford(totalIfLevelUp);

                if (nextLevelValue != null)
                {
                    nextLevelValue.text  = $"Lv {nextLevel}";
                    nextLevelValue.color = levelTextColor;
                }
                if (costValue   != null) costValue.text = nextCost.ToString();
                if (rewardValue != null)
                {
                    rewardValue.text  = $"{spReward} {LocalizationManager.Get("CLUB_MODAL_SP_SUFFIX")}";
                    rewardValue.color = greenTextColor;
                }
                if (levelUpButton != null)
                    levelUpButton.interactable = canAffordNext;
            }

            // ── Available SP ────────────────────────────────────────────────
            int currentTotalSpent = playerClub.spentPower + playerClub.spentAccuracy
                                  + playerClub.spentLieResistance + playerClub.spentDurability;
            int totalPending = pendingPower + pendingAccuracy + pendingLieRes + pendingDurability;
            int availableSP  = previewTotalSPEarned - currentTotalSpent - totalPending;

            if (availableSPValue != null)
            {
                availableSPValue.text  = $"{availableSP} {LocalizationManager.Get("CLUB_MODAL_SP_SUFFIX")}";
                availableSPValue.color = availableSP > 0 ? spAvailableColor : spDepletedColor;
            }

            // ── Stat Rows ────────────────────────────────────────────────────
            int capPower    = template.basePower          + PlayerClubData.MAX_SP_PER_STAT;
            int capAccuracy = template.baseAccuracy       + PlayerClubData.MAX_SP_PER_STAT;
            int capLieRes   = template.baseLieResistance  + PlayerClubData.MAX_SP_PER_STAT;
            int capDurability = template.maxDurability    + PlayerClubData.MAX_SP_PER_STAT; // SP +5 each but display as SP points

            // Allocatable stats
            UpdateStatRow(powerBar,      powerBarPending,
                powerValueCurrent,      powerValueMax,      powerPending,      powerPlusButton,
                template.basePower      + playerClub.spentPower,         pendingPower,      capPower,    availableSP);

            UpdateStatRow(accuracyBar,   accuracyBarPending,
                accuracyValueCurrent,   accuracyValueMax,   accuracyPending,   accuracyPlusButton,
                template.baseAccuracy   + playerClub.spentAccuracy,      pendingAccuracy,   capAccuracy, availableSP);

            UpdateStatRow(lieResBar,     lieResBarPending,
                lieResValueCurrent,     lieResValueMax,     lieResPending,     lieResPlusButton,
                template.baseLieResistance + playerClub.spentLieResistance, pendingLieRes,  capLieRes,   availableSP);

            UpdateStatRow(durabilityBar, durabilityBarPending,
                durabilityValueCurrent, durabilityValueMax, durabilityPending, durabilityPlusButton,
                template.maxDurability  + playerClub.spentDurability,     pendingDurability, capDurability, availableSP);

            // Fixed stat — Loft (display only)
            if (loftBar != null)
                loftBar.fillAmount = STAT_MAX > 0 ? (float)template.baseLoft / STAT_MAX : 0f;
            if (loftValueCurrent != null) loftValueCurrent.text = $"{template.baseLoft}";
            if (loftValueMax     != null) loftValueMax.text     = $"/{STAT_MAX}";

            // ── Reset / Confirm states ───────────────────────────────────────
            bool hasPending     = totalPending > 0;
            bool allSPAllocated = availableSP == 0 && hasPending;

            if (resetButton   != null) resetButton.interactable   = hasPending;
            if (confirmButton != null) confirmButton.interactable  = allSPAllocated;
        }

        private const int STAT_MAX = 100; // bar fill reference for non-SP stats

        /// <summary>
        /// Updates a single allocatable stat row (blue bar + orange pending bar, value text, +N label, [+] button).
        /// </summary>
        private void UpdateStatRow(
            Image bar, Image barPending,
            TextMeshProUGUI valueTextCurrent, TextMeshProUGUI valueTextMax,
            TextMeshProUGUI pendingText,
            Button plusButton,
            int currentValue, int pendingAmount, int cap, int availableSP)
        {
            // Blue bar — confirmed value
            if (bar != null)
                bar.fillAmount = cap > 0 ? (float)currentValue / cap : 0f;

            // Orange bar — current + pending
            if (barPending != null)
            {
                barPending.fillAmount = cap > 0 ? (float)(currentValue + pendingAmount) / cap : 0f;
                barPending.gameObject.SetActive(pendingAmount > 0);
            }

            if (valueTextCurrent != null) valueTextCurrent.text = $"{currentValue + pendingAmount}";
            if (valueTextMax     != null) valueTextMax.text     = $"/{cap}";

            if (pendingText != null)
            {
                pendingText.gameObject.SetActive(pendingAmount > 0);
                if (pendingAmount > 0)
                {
                    pendingText.text  = $"+{pendingAmount}";
                    pendingText.color = pendingBarColor;
                }
            }

            if (plusButton != null)
                plusButton.interactable = availableSP > 0 && (currentValue + pendingAmount) < cap;
        }

        // ───────────────────────────────────────────────────────────────────
        // Button Handlers
        // ───────────────────────────────────────────────────────────────────

        private void OnLevelUpClicked()
        {
            if (ClubManager.Instance == null) return;

            int maxLevel  = ClubManager.Instance.GetMaxLevel(clubId);
            if (previewLevel >= maxLevel) return;

            int nextLevel = previewLevel + 1;
            int cost      = CharacterLevelUpDatabase.Instance.GetLevelUpCost(nextLevel);

            if (RewardPointsManager.Instance == null ||
                !RewardPointsManager.Instance.CanAfford(totalRPCost + cost)) return;

            previewLevel++;
            totalRPCost          += cost;
            previewTotalSPEarned += CharacterLevelUpDatabase.Instance.GetSPReward(nextLevel);

            Debug.Log($"[ClubLevelUpModal] Preview: Lv {previewLevel}, RP so far: {totalRPCost}, SP pool: {previewTotalSPEarned}");
            RefreshDisplay();
        }

        private void OnPowerPlus()      { pendingPower++;      RefreshDisplay(); }
        private void OnAccuracyPlus()   { pendingAccuracy++;   RefreshDisplay(); }
        private void OnLieResPlus()     { pendingLieRes++;     RefreshDisplay(); }
        private void OnDurabilityPlus() { pendingDurability++; RefreshDisplay(); }

        private void OnResetClicked()
        {
            pendingPower      = 0;
            pendingAccuracy   = 0;
            pendingLieRes     = 0;
            pendingDurability = 0;
            RefreshDisplay();
        }

        /// <summary>Starts position restore coroutine on hide.</summary>
        protected override void OnHide()
        {
            if (!_savedModalLocalPos.HasValue) return;
            StartCoroutine(RestorePositionAfterFade());
        }

        private IEnumerator RestorePositionAfterFade()
        {
            while (modalPanel != null && modalPanel.activeSelf)
                yield return null;

            if (modalPanel != null && _savedModalLocalPos.HasValue)
                modalPanel.transform.localPosition = _savedModalLocalPos.Value;

            _savedModalLocalPos = null;
        }

        private void OnCancelClicked()
        {
            pendingPower      = 0;
            pendingAccuracy   = 0;
            pendingLieRes     = 0;
            pendingDurability = 0;
            Debug.Log("[ClubLevelUpModal] Cancelled — all previewed changes discarded.");
            Hide();
        }

        /// <summary>
        /// Commits all previewed level-ups and SP allocation.
        /// Single RP transaction. Durability SP increases maxDurability (+5 per point).
        /// </summary>
        private void OnConfirmClicked()
        {
            if (ClubManager.Instance == null || RewardPointsManager.Instance == null) return;

            var playerClub = ClubManager.Instance.GetClubData(clubId);
            var template   = ClubDatabaseCSV.Instance == null ? null : ClubDatabaseCSV.Instance.GetClub(clubId);
            if (playerClub == null || template == null) return;

            // 1. Deduct RP (single transaction)
            RewardPointsManager.Instance.Spend(totalRPCost);

            // 2. Set new level
            ClubManager.Instance.SetLevel(clubId, previewLevel);

            // 3. Commit SP earned
            playerClub.totalSPEarned = previewTotalSPEarned;

            // 4. Commit pending SP allocation
            playerClub.spentPower         += pendingPower;
            playerClub.spentAccuracy      += pendingAccuracy;
            playerClub.spentLieResistance += pendingLieRes;
            playerClub.spentDurability    += pendingDurability;

            // 5. Update maxDurability: +5 per SP point spent on durability
            playerClub.maxDurability = template.maxDurability + (playerClub.spentDurability * 5);

            // 6. Fire event to refresh UI
            ClubManager.Instance.RefreshStatValues(clubId);

            Debug.Log($"[ClubLevelUpModal] Confirmed: Lv {previewLevel}, RP -{totalRPCost}, " +
                      $"SP: PWR+{pendingPower} ACC+{pendingAccuracy} LIE+{pendingLieRes} DUR+{pendingDurability}");

            pendingPower      = 0;
            pendingAccuracy   = 0;
            pendingLieRes     = 0;
            pendingDurability = 0;

            Hide();
        }
    }
}
