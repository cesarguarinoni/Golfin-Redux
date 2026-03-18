#nullable enable
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Golfin.UI.Modals;

namespace Golfin.Roster
{
    /// <summary>
    /// Level-Up Modal — Phase 2c.
    ///
    /// All state is LOCAL until CONFIRM. Tapping LEVEL UP is a preview only —
    /// nothing is written to CharacterManager until the player confirms.
    /// CANCEL reverts everything. CONFIRM commits level-ups + SP allocation.
    ///
    /// CONFIRM is only enabled when available SP == 0 AND pending allocation > 0
    /// (i.e. all earned SP has been fully allocated).
    /// </summary>
    public class LevelUpModalController : ModalController
    {
        // ── Character Info ──────────────────────────────────────────────────
        [Header("Character Info")]
        [SerializeField] private TextMeshProUGUI characterNameText = null!;
        [SerializeField] private TextMeshProUGUI rarityLabel        = null!;
        [SerializeField] private TextMeshProUGUI levelText          = null!;
        [SerializeField] private TextMeshProUGUI nextLevelValue     = null!;
        [SerializeField] private TextMeshProUGUI costValue          = null!;
        [SerializeField] private TextMeshProUGUI rewardValue        = null!;

        // ── Level Up Button ─────────────────────────────────────────────────
        [Header("Level Up")]
        [SerializeField] private Button levelUpButton = null!;

        // ── Available SP ────────────────────────────────────────────────────
        [Header("SP Allocation")]
        [SerializeField] private TextMeshProUGUI availableSPValue = null!;

        // ── Stat Rows ───────────────────────────────────────────────────────
        // Each stat row has: blue bar (current), orange bar (current+pending),
        // value text, pending label (+N), and plus button.
        // In the Unity hierarchy, the orange bar should sit BEHIND the blue bar
        // so only the delta segment shows through.

        [Header("Stat Row — Strength")]
        [SerializeField] private Image             strengthBar             = null!;
        [SerializeField] private Image             strengthBarPending      = null!;
        [SerializeField] private TextMeshProUGUI   strengthValueCurrent    = null!;  // "10"
        [SerializeField] private TextMeshProUGUI   strengthValueMax        = null!;  // "/25"
        [SerializeField] private TextMeshProUGUI   strengthPending         = null!;  // "+N" label
        [SerializeField] private Button            strengthPlusButton      = null!;

        [Header("Stat Row — Club Control")]
        [SerializeField] private Image             clubControlBar             = null!;
        [SerializeField] private Image             clubControlBarPending      = null!;
        [SerializeField] private TextMeshProUGUI   clubControlValueCurrent    = null!;
        [SerializeField] private TextMeshProUGUI   clubControlValueMax        = null!;
        [SerializeField] private TextMeshProUGUI   clubControlPending         = null!;
        [SerializeField] private Button            clubControlPlusButton      = null!;

        [Header("Stat Row — Recovery")]
        [SerializeField] private Image             recoveryBar             = null!;
        [SerializeField] private Image             recoveryBarPending      = null!;
        [SerializeField] private TextMeshProUGUI   recoveryValueCurrent    = null!;
        [SerializeField] private TextMeshProUGUI   recoveryValueMax        = null!;
        [SerializeField] private TextMeshProUGUI   recoveryPending         = null!;
        [SerializeField] private Button            recoveryPlusButton      = null!;

        [Header("Stat Row — Stamina")]
        [SerializeField] private Image             staminaBar             = null!;
        [SerializeField] private Image             staminaBarPending      = null!;
        [SerializeField] private TextMeshProUGUI   staminaValueCurrent    = null!;
        [SerializeField] private TextMeshProUGUI   staminaValueMax        = null!;
        [SerializeField] private TextMeshProUGUI   staminaPending         = null!;
        [SerializeField] private Button            staminaPlusButton      = null!;

        // ── Action Buttons ──────────────────────────────────────────────────
        [Header("Actions")]
        [SerializeField] private Button            resetButton         = null!;
        [SerializeField] private Button            cancelButton        = null!;
        [SerializeField] private Button            confirmButton       = null!;

        // ── Colors ──────────────────────────────────────────────────────────
        [Header("Colors")]
        [SerializeField] private Color pendingBarColor  = new Color(1f, 0.7f, 0.2f, 1f);         // orange — pending label text
        [SerializeField] private Color greenTextColor   = new Color(0.2f, 0.8f, 0.2f, 1f);        // green — reward value
        [SerializeField] private Color spAvailableColor = new Color(1f, 0.525f, 0.110f, 1f);      // #FF861C — SP available
        [SerializeField] private Color spDepletedColor  = new Color(0.753f, 0.251f, 0f, 1f);      // #C04000 — no SP remaining
        [SerializeField] private Color levelTextColor   = new Color(0.153f, 0.459f, 0.867f, 1f);  // #2775DD — level display

        // ── Preview State (local, not committed until Confirm) ──────────────
        private string characterId = "";
        private int previewLevel;           // starts at current level
        private int previewTotalSPEarned;   // starts at playerData.totalSPEarned
        private int totalRPCost;            // accumulated RP cost across all previewed levels

        // ── Pending SP Allocation ───────────────────────────────────────────
        private int pendingStrength;
        private int pendingClubControl;
        private int pendingRecovery;
        private int pendingStamina;

        // ───────────────────────────────────────────────────────────────────
        // Lifecycle
        // ───────────────────────────────────────────────────────────────────

        protected override void Awake()
        {
            base.Awake(); // ModalController sets up CanvasGroup + starts hidden
        }

        private void Start()
        {
            levelUpButton?.onClick.AddListener(OnLevelUpClicked);
            strengthPlusButton?.onClick.AddListener(OnStrengthPlus);
            clubControlPlusButton?.onClick.AddListener(OnClubControlPlus);
            recoveryPlusButton?.onClick.AddListener(OnRecoveryPlus);
            staminaPlusButton?.onClick.AddListener(OnStaminaPlus);
            resetButton?.onClick.AddListener(OnResetClicked);
            cancelButton?.onClick.AddListener(OnCancelClicked);
            confirmButton?.onClick.AddListener(OnConfirmClicked);
        }

        // ───────────────────────────────────────────────────────────────────
        // Public API
        // ───────────────────────────────────────────────────────────────────

        /// <summary>
        /// Called by CharacterDetailPanel when the LEVEL UP button is tapped.
        /// Initialises preview state from current character data and shows the modal.
        /// </summary>
        public void Open(string characterId)
        {
            this.characterId = characterId;

            var playerData = CharacterManager.Instance.GetCharacterData(characterId);
            if (playerData == null) return;

            // Seed preview from real current state
            previewLevel          = playerData.currentLevel;
            previewTotalSPEarned  = playerData.totalSPEarned;
            totalRPCost           = 0;

            pendingStrength    = 0;
            pendingClubControl = 0;
            pendingRecovery    = 0;
            pendingStamina     = 0;

            // Reset levelText colour to white on every open — it only turns blue
            // inside RefreshDisplay() if a preview level-up has happened this session.
            if (levelText != null) levelText.color = Color.white;

            RefreshDisplay();
            Show();
        }

        // ───────────────────────────────────────────────────────────────────
        // Display
        // ───────────────────────────────────────────────────────────────────

        private void RefreshDisplay()
        {
            var playerData = CharacterManager.Instance.GetCharacterData(characterId);
            var csvChar    = CharacterDatabaseCSV.Instance?.GetCharacter(characterId);
            if (playerData == null)
            {
                Debug.LogError($"[LevelUpModal] RefreshDisplay: playerData null for '{characterId}'");
                return;
            }

            if (CharacterLevelUpDatabase.Instance == null)
            {
                Debug.LogError("[LevelUpModal] CharacterLevelUpDatabase not found — modal cannot open. Is it in the scene?");
                return;
            }

            // --- Header ---
            if (characterNameText != null)
                characterNameText.text = csvChar != null
                    ? csvChar.GetDisplayName()
                    : characterId.ToUpper();

            var rarity = csvChar?.rarity ?? CharacterRarity.Common;
            if (rarityLabel != null)
            {
                rarityLabel.text  = rarity.ToString().ToUpper();
                rarityLabel.color = RarityHelper.GetRarityColor(rarity);
            }

            int maxLevel = CharacterManager.Instance.GetMaxLevel(characterId);
            if (levelText != null)
            {
                levelText.text = $"Lv {previewLevel}/{maxLevel}";
                // Only tint blue once a preview level-up has happened; otherwise leave Editor colour
                if (previewLevel > playerData.currentLevel)
                    levelText.color = levelTextColor; // #2775DD
            }

            // --- Next Level / Cost / Reward ---
            bool isMaxLevel = previewLevel >= maxLevel;
            int  nextLevel  = previewLevel + 1;

            if (isMaxLevel)
            {
                if (nextLevelValue != null) nextLevelValue.text = "MAX";
                if (costValue      != null) costValue.text      = "-";
                if (rewardValue    != null) rewardValue.text    = "-";
                if (levelUpButton  != null) levelUpButton.interactable = false;
            }
            else
            {
                int nextCost      = CharacterLevelUpDatabase.Instance.GetLevelUpCost(nextLevel);
                int spReward      = CharacterLevelUpDatabase.Instance.GetSPReward(nextLevel);
                int totalIfLevelUp = totalRPCost + nextCost;
                bool canAffordNext = RewardPointsManager.Instance.CanAfford(totalIfLevelUp);

                if (nextLevelValue != null)
                {
                    nextLevelValue.text  = $"Lv {nextLevel}";
                    nextLevelValue.color = levelTextColor; // #2775DD
                }
                if (costValue   != null) costValue.text   = nextCost.ToString();
                if (rewardValue != null)
                {
                    rewardValue.text  = $"{spReward} SP";
                    rewardValue.color = greenTextColor;
                }
                if (levelUpButton != null)
                    levelUpButton.interactable = canAffordNext;
            }

            // --- Available SP ---
            int currentTotalSpent = playerData.spentStrength + playerData.spentClubControl
                                  + playerData.spentRecovery + playerData.spentStamina;
            int totalPending = pendingStrength + pendingClubControl + pendingRecovery + pendingStamina;
            int availableSP  = previewTotalSPEarned - currentTotalSpent - totalPending;

            if (availableSPValue != null)
            {
                availableSPValue.text  = $"{availableSP} SP";
                availableSPValue.color = availableSP > 0 ? spAvailableColor : spDepletedColor;
            }

            // --- Stat Rows ---
            var caps = RarityStatCaps.GetStatCaps(rarity);

            int baseStr  = csvChar?.baseStrength    ?? 0;
            int baseCc   = csvChar?.baseClubControl ?? 0;
            int baseRec  = csvChar?.baseRecovery    ?? 0;
            int baseStam = csvChar?.baseStamina     ?? 0;

            UpdateStatRow(strengthBar,    strengthBarPending,
                strengthValueCurrent,    strengthValueMax,    strengthPending,    strengthPlusButton,
                baseStr  + playerData.spentStrength,    pendingStrength,    caps.strengthCap,    availableSP);

            UpdateStatRow(clubControlBar, clubControlBarPending,
                clubControlValueCurrent, clubControlValueMax, clubControlPending, clubControlPlusButton,
                baseCc   + playerData.spentClubControl, pendingClubControl, caps.clubControlCap, availableSP);

            UpdateStatRow(recoveryBar,    recoveryBarPending,
                recoveryValueCurrent,    recoveryValueMax,    recoveryPending,    recoveryPlusButton,
                baseRec  + playerData.spentRecovery,    pendingRecovery,    caps.recoveryCap,    availableSP);

            UpdateStatRow(staminaBar,     staminaBarPending,
                staminaValueCurrent,     staminaValueMax,     staminaPending,     staminaPlusButton,
                baseStam + playerData.spentStamina,     pendingStamina,     caps.staminaCap,     availableSP);

            // --- Reset / Confirm button states ---
            bool hasPending      = totalPending > 0;
            bool allSPAllocated  = availableSP == 0 && hasPending;

            if (resetButton   != null) resetButton.interactable  = hasPending;
            if (confirmButton != null) confirmButton.interactable = allSPAllocated;
            // Button visual state is handled by the Button's Color Tint transition — do NOT set Image.color here.
        }

        /// <summary>
        /// Updates a single stat row's bars, value text, pending label, and [+] button.
        /// The orange bar should sit BEHIND the blue bar in the Unity hierarchy so only
        /// the delta segment (beyond the blue fill) is visible.
        /// </summary>
        private void UpdateStatRow(
            Image bar, Image barPending,
            TextMeshProUGUI valueTextCurrent, TextMeshProUGUI valueTextMax,
            TextMeshProUGUI pendingText,
            Button plusButton,
            int currentValue, int pendingAmount, int cap, int availableSP)
        {
            // Blue bar — confirmed stat value (colour left as-is on the Image)
            if (bar != null)
                bar.fillAmount = cap > 0 ? (float)currentValue / cap : 0f;

            // Orange bar — current + pending, colour already on the Image
            if (barPending != null)
            {
                barPending.fillAmount = cap > 0 ? (float)(currentValue + pendingAmount) / cap : 0f;
                barPending.gameObject.SetActive(pendingAmount > 0);
            }

            // Value split: "10" (current font) + "/25" (smaller font)
            if (valueTextCurrent != null)
                valueTextCurrent.text = $"{currentValue + pendingAmount}";
            if (valueTextMax != null)
                valueTextMax.text = $"/{cap}";

            // "+N" pending label
            if (pendingText != null)
            {
                pendingText.gameObject.SetActive(pendingAmount > 0);
                if (pendingAmount > 0)
                {
                    pendingText.text  = $"+{pendingAmount}";
                    pendingText.color = pendingBarColor;
                }
            }

            // [+] button: enabled if SP available AND stat not at cap
            if (plusButton != null)
                plusButton.interactable = availableSP > 0 && (currentValue + pendingAmount) < cap;
        }

        // ───────────────────────────────────────────────────────────────────
        // Button Handlers
        // ───────────────────────────────────────────────────────────────────

        /// <summary>
        /// PREVIEW ONLY — increments local preview state. Nothing written to CharacterManager.
        /// </summary>
        private void OnLevelUpClicked()
        {
            int maxLevel = CharacterManager.Instance.GetMaxLevel(characterId);
            if (previewLevel >= maxLevel) return;

            int nextLevel = previewLevel + 1;
            int cost      = CharacterLevelUpDatabase.Instance.GetLevelUpCost(nextLevel);

            if (!RewardPointsManager.Instance.CanAfford(totalRPCost + cost)) return;

            // Update preview — do NOT call CharacterManager.LevelUp()
            previewLevel++;
            totalRPCost         += cost;
            previewTotalSPEarned += CharacterLevelUpDatabase.Instance.GetSPReward(nextLevel);

            Debug.Log($"[LevelUpModal] Preview: Lv {previewLevel}, RP cost so far: {totalRPCost}, SP pool: {previewTotalSPEarned}");
            RefreshDisplay();
        }

        private void OnStrengthPlus()    { pendingStrength++;    RefreshDisplay(); }
        private void OnClubControlPlus() { pendingClubControl++; RefreshDisplay(); }
        private void OnRecoveryPlus()    { pendingRecovery++;    RefreshDisplay(); }
        private void OnStaminaPlus()     { pendingStamina++;     RefreshDisplay(); }

        private void OnResetClicked()
        {
            pendingStrength    = 0;
            pendingClubControl = 0;
            pendingRecovery    = 0;
            pendingStamina     = 0;
            RefreshDisplay();
        }

        /// <summary>
        /// Discards ALL previewed changes (level-ups + SP allocation).
        /// Safe to call because nothing was ever written to CharacterManager.
        /// </summary>
        private void OnCancelClicked()
        {
            pendingStrength    = 0;
            pendingClubControl = 0;
            pendingRecovery    = 0;
            pendingStamina     = 0;
            Debug.Log("[LevelUpModal] Cancelled - all previewed changes discarded");
            Hide();
        }

        /// <summary>
        /// Commits everything: calls LevelUp() once per previewed level,
        /// writes pending SP to PlayerCharacterData, refreshes stat values.
        /// </summary>
        private void OnConfirmClicked()
        {
            var playerData = CharacterManager.Instance.GetCharacterData(characterId);
            if (playerData == null) return;

            // Commit each previewed level-up (LevelUp deducts RP, increments level, adds SP)
            int levelsGained = previewLevel - playerData.currentLevel;
            for (int i = 0; i < levelsGained; i++)
            {
                CharacterManager.Instance.LevelUp(characterId);
            }

            // Commit pending SP allocation directly to PlayerCharacterData
            playerData.spentStrength    += pendingStrength;
            playerData.spentClubControl += pendingClubControl;
            playerData.spentRecovery    += pendingRecovery;
            playerData.spentStamina     += pendingStamina;

            // Recalculate derived current stat values
            CharacterManager.Instance.RefreshStatValues(characterId);

            Debug.Log($"[LevelUpModal] Confirmed: +{levelsGained} levels, " +
                      $"SP: STR+{pendingStrength} CC+{pendingClubControl} " +
                      $"REC+{pendingRecovery} STAM+{pendingStamina}");

            pendingStrength    = 0;
            pendingClubControl = 0;
            pendingRecovery    = 0;
            pendingStamina     = 0;

            Hide();
        }
    }
}
