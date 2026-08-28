#nullable enable
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Golfin.Content;
using Golfin.Economy;
using Golfin.EconomyRuntime;
using Golfin.InventorySync;
using Golfin.UI.Modals;
using Golfin.UI.Polish;
using Golfin.UI.Toast;

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

        // ── Static Labels (localized) ────────────────────────────────────────
        [Header("Static Labels")]
        [SerializeField] private TextMeshProUGUI nextLevelLabel    = null!;
        [SerializeField] private TextMeshProUGUI costLabel         = null!;
        [SerializeField] private TextMeshProUGUI rewardLabel       = null!;
        [SerializeField] private TextMeshProUGUI levelUpButtonLabel = null!;
        [SerializeField] private TextMeshProUGUI availableSPLabel  = null!;
        [SerializeField] private TextMeshProUGUI resetButtonLabel  = null!;
        [SerializeField] private TextMeshProUGUI cancelButtonLabel = null!;
        [SerializeField] private TextMeshProUGUI confirmButtonLabel = null!;

        // ── Colors ──────────────────────────────────────────────────────────
        [Header("Colors")]
        [SerializeField] private Color pendingBarColor  = new Color(1f, 0.7f, 0.2f, 1f);         // orange — pending label text
        [SerializeField] private Color greenTextColor   = new Color(0.2f, 0.8f, 0.2f, 1f);        // green — reward value
        [SerializeField] private Color spAvailableColor = new Color(1f, 0.525f, 0.110f, 1f);      // #FF861C — SP available
        [SerializeField] private Color spDepletedColor  = new Color(0.753f, 0.251f, 0f, 1f);      // #C04000 — no SP remaining
        [SerializeField] private Color levelTextColor   = new Color(0.153f, 0.459f, 0.867f, 1f);  // #2775DD — level display

        // ── Anchor (optional — reposition modalPanel over a specific panel) ─
        // LevelUpModal itself is never reparented — the backdrop child must stay
        // under RosterScreen so it darkens the entire screen, not just one panel.
        private Vector3? _savedModalLocalPos;

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

        private void OnEnable()
        {
            LocalizationManager.OnLanguageChanged += RefreshLocalizedText;
        }

        protected override void OnDisable()
        {
            // Call base FIRST so the S2 OpenModalCount leak guard fires.
            base.OnDisable();
            LocalizationManager.OnLanguageChanged -= RefreshLocalizedText;
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
        /// Pass anchorPanel to center the modal inside a specific panel (e.g. RightPanel or CompareRightPanel).
        /// </summary>
        public void Open(string characterId, RectTransform? anchorPanel = null)
        {
            this.characterId = characterId;

            // Move modalPanel to centre over anchorPanel without reparenting.
            // Keeping LevelUpModal under RosterScreen means the backdrop darkens
            // the full screen, not just the anchor panel.
            if (anchorPanel != null && modalPanel != null)
            {
                // World-space centre of the anchor panel.
                Vector3 anchorWorldCentre = anchorPanel.TransformPoint(anchorPanel.rect.center);

                // Convert to LevelUpModal's local space (= modalPanel's parent space).
                // Because LevelUpModal is stretch-full its local origin is the pivot
                // (centre of RosterScreen), so InverseTransformPoint gives us the
                // exact localPosition we need.
                Vector3 targetLocal = transform.InverseTransformPoint(anchorWorldCentre);
                targetLocal.z = 0f;

                _savedModalLocalPos           = modalPanel.transform.localPosition;
                modalPanel.transform.localPosition = targetLocal;
            }

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

            RefreshLocalizedText();
            Show();
        }

        // ───────────────────────────────────────────────────────────────────
        // Display
        // ───────────────────────────────────────────────────────────────────

        /// <summary>Sets all static label text from the localization table. Called once on
        /// open and again whenever the player switches language.</summary>
        private void RefreshLocalizedText()
        {
            if (nextLevelLabel     != null) nextLevelLabel.text     = LocalizationManager.Get("MODAL_NEXT_LEVEL");
            if (costLabel          != null) costLabel.text          = LocalizationManager.Get("MODAL_COST");
            if (rewardLabel        != null) rewardLabel.text        = LocalizationManager.Get("MODAL_REWARD");
            if (levelUpButtonLabel != null) levelUpButtonLabel.text = LocalizationManager.Get("MODAL_LEVEL_UP");
            if (availableSPLabel   != null) availableSPLabel.text   = LocalizationManager.Get("MODAL_AVAILABLE_SP");
            if (resetButtonLabel   != null) resetButtonLabel.text   = LocalizationManager.Get("MODAL_RESET");
            if (cancelButtonLabel  != null) cancelButtonLabel.text  = LocalizationManager.Get("MODAL_CANCEL");
            if (confirmButtonLabel != null) confirmButtonLabel.text = LocalizationManager.Get("MODAL_CONFIRM");

            // Re-run display so dynamic values (SP suffix, rarity name) also update
            if (!string.IsNullOrEmpty(characterId))
                RefreshDisplay();
        }

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
                rarityLabel.text  = LocalizationManager.Get($"RARITY_{rarity.ToString().ToUpper()}");
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
                    rewardValue.text  = $"{spReward} {LocalizationManager.Get("MODAL_SP_SUFFIX")}";
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
                availableSPValue.text  = $"{availableSP} {LocalizationManager.Get("MODAL_SP_SUFFIX")}";
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
        protected override void OnHide()
        {
            if (!_savedModalLocalPos.HasValue) return;

            // Defer the restore until the FadeOut coroutine finishes and
            // ModalController.HideImmediate() deactivates modalPanel.
            // Restoring immediately would snap the dialog to its default
            // position while it is still visible and mid-fade.
            StartCoroutine(RestorePositionAfterFade());
        }

        private System.Collections.IEnumerator RestorePositionAfterFade()
        {
            // Wait until the fade is done and the panel has been hidden.
            while (modalPanel != null && modalPanel.activeSelf)
                yield return null;

            if (modalPanel != null && _savedModalLocalPos.HasValue)
                modalPanel.transform.localPosition = _savedModalLocalPos.Value;

            _savedModalLocalPos = null;
        }

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

            // FLAG OFF — today's path, unchanged and byte-identical. PointsSpendGate.Spend
            // short-circuits before PointsService is touched and runs the commit on this very stack
            // frame, so modal timing does not shift.
            if (!PointsBackendFlag.Enabled)
            {
                PointsSpendGate.Spend(totalRPCost, SpendReasons.CharacterLevelUp,
                    () => CommitLevelUps(playerData));
                return;
            }

            // FLAG ON — progress_server_side §4. ONE call that prices the whole previewed run from
            // the PUBLISHED cost table, debits through spend_pts and RECORDS the new level, in one
            // transaction. It replaces the plain debit rather than adding to it: the old call told
            // the server an amount the client had computed, which is precisely the hole this closes.
            //
            // Still ONE call for an N-level run, for the reason the Slice-2 comment gave: the modal
            // presents this as a single transaction, and N round-trips would be both slow and only
            // partially reversible if the connection dropped mid-run.
            //
            // Show the round-trip on CONFIRM, and lock CANCEL with it — the run is already
            // being priced and recorded server-side, so letting the player discard the preview
            // mid-flight would hide a level-up that is about to land (transaction_feedback §3.1).
            // LevelUpAsync answers exactly once on every branch (including its own in-flight
            // refusal), so this scope is always disposed.
            var pending = PendingSpend.Begin(confirmButton, confirmButtonLabel, cancelButton);

            ProgressService.Instance.LevelUpAsync(
                ProgressService.KindCharacter, characterId,
                playerData.currentLevel, previewLevel, totalRPCost, ContentBuildNumber.Current,
                outcome =>
                {
                    // Restore before the verdict is acted on: RepriceFromServer re-derives CONFIRM's
                    // own enabled state, and the conflict arm closes the modal.
                    pending.Dispose();
                    OnServerAnswered(outcome, playerData);
                });
        }

        /// <summary>
        /// The five things the server can say about a level-up (progress_server_side §4).
        ///
        /// <para>
        /// Only <c>Ok</c> reaches <see cref="CommitLevelUps"/> — nothing is written locally on any
        /// refusal, which is the whole ordering rule this task inherits from Slice 2.
        /// </para>
        /// </summary>
        private void OnServerAnswered(ProgressLevelUpOutcome outcome, PlayerCharacterData playerData)
        {
            if (outcome == null) { Toast(PointsSpendGate.OfflineMessage); return; }

            switch (outcome.Verdict)
            {
                case ProgressLevelUpVerdict.Ok:
                    CommitLevelUps(playerData);
                    return;

                case ProgressLevelUpVerdict.CostChanged:
                    RepriceFromServer(playerData, outcome.Cost);
                    return;

                case ProgressLevelUpVerdict.LevelConflict:
                    // Not answerable by trying again: this client's level is not the server's, so any
                    // preview built on it is wrong too. Close, mark the inventory dirty, and let the
                    // next sync's additive merge reconcile — the modal reopens on real state.
                    Debug.LogWarning($"[LevelUpModal] Server holds Lv {outcome.ServerLevel} for " +
                                     $"'{characterId}', this client claimed Lv {playerData.currentLevel}. " +
                                     "Closing and resyncing.");
                    Toast(PointsSpendGate.LevelConflictMessage);
                    InventorySyncService.Instance?.MarkDirty();
                    Hide();
                    return;

                case ProgressLevelUpVerdict.Insufficient:
                    Toast(PointsSpendGate.InsufficientMessage);
                    return;

                default:
                    // NotAvailable / Unavailable / Disabled. The player cannot act on the difference
                    // between them and the log already carries it.
                    Toast(PointsSpendGate.OfflineMessage);
                    return;
            }
        }

        /// <summary>
        /// Rebuild the preview after a <c>cost_changed</c>, so the second CONFIRM pays.
        ///
        /// <para>
        /// Two halves, and the second is the one that matters. The DB is reloaded so the per-level
        /// numbers the modal shows come from whatever overlay this launch has — but the overlay is a
        /// next-launch effect (I5), so a cost published seconds ago is NOT in it, and re-summing
        /// locally would produce the same total the server just rejected and loop forever. So the RUN
        /// TOTAL is taken from the server's answer, which is authoritative by construction: it is the
        /// sum <c>golfin_level_up</c> computed for exactly this <c>from → to</c> range and exactly
        /// what it will charge on the next attempt.
        /// </para>
        /// <para>
        /// SP is not re-priced from the server because the server does not price it: it records
        /// LEVELS, and <c>sp_reward</c> is a client-side derivation from the same table.
        /// </para>
        /// </summary>
        private void RepriceFromServer(PlayerCharacterData playerData, int serverCost)
        {
            int target = previewLevel;

            var db = CharacterLevelUpDatabase.Instance;
            if (db != null) db.Reload();

            // Re-sum from the current level so previewTotalSPEarned is coherent with the target,
            // then let the server's number stand as the price.
            previewLevel         = playerData.currentLevel;
            previewTotalSPEarned = playerData.totalSPEarned;
            totalRPCost          = 0;

            for (int level = playerData.currentLevel + 1; level <= target; level++)
            {
                previewLevel          = level;
                previewTotalSPEarned += db == null ? 0 : db.GetSPReward(level);
            }

            totalRPCost = serverCost;

            Debug.Log($"[LevelUpModal] Cost changed for '{characterId}' Lv {playerData.currentLevel} → " +
                      $"{previewLevel}: the published total is {serverCost} RP. Preview rebuilt; " +
                      "the next CONFIRM pays that.");

            RefreshDisplay();
            Toast(PointsSpendGate.CostUpdatedMessage);
        }

        private static void Toast(string message)
        {
            if (ToastController.Instance != null) ToastController.Instance.Show(message, 2f);
        }

        /// <summary>The previously-inline body of <see cref="OnConfirmClicked"/>, now gated on the
        /// server debit landing first. Never runs when the debit is refused or unreachable.</summary>
        private void CommitLevelUps(PlayerCharacterData playerData)
        {
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
