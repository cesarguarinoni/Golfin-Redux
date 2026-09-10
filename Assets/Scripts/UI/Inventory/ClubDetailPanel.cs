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
        // ── Panel Roots ────────────────────────────────────────────────────────

        [Header("Panel Roots")]
        [SerializeField] private RectTransform rightPanel = null!;

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

        // ── Compare Controller ─────────────────────────────────────────────────

        [Header("Compare")]
        [SerializeField] private ClubCompareController? compareController;

        // ── Modals ─────────────────────────────────────────────────────────────

        // ── asset_loans §4.1 ──────────────────────────────────────────────────

        [Header("Loans")]
        [SerializeField] private Button? lendButton;
        [SerializeField] private TextMeshProUGUI? lendButtonText;
        [SerializeField] private Golfin.UI.Loans.LoanRibbonView? loanRibbon;
        [SerializeField] private Golfin.UI.Loans.LoanModalController? loanModal;
        [SerializeField] private Golfin.UI.Loans.LoanReturnModalController? loanReturnModal;
        [SerializeField] private Golfin.UI.Loans.LoanRescindModalController? loanRescindModal;

        [Header("Modals")]
        [SerializeField] private ClubLevelUpModalController?  levelUpModal;

        [Header("Bag Selection")]
        [SerializeField] private BagSelectionModalController? bagSelectionModal;

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
            if (lendButton    != null) lendButton.onClick.AddListener(OnLendClicked);
        }

        private void OnEnable()
        {
            if (carousel != null)
                carousel.OnClubSelected += UpdatePanel;

            if (ClubManager.Instance != null)
            {
                ClubManager.Instance.OnClubEquipped  += OnClubEquippedChanged;
                ClubManager.Instance.OnClubRepaired  += OnClubRepairedHandler;
            }

            LocalizationManager.OnLanguageChanged += RefreshLocalizedText;

            // asset_loans §4.1 — a loan can start or end on another device.
            Golfin.Social.LoanService.Instance.OnLoansChanged += OnLoansChanged;
        }

        private void OnDisable()
        {
            if (carousel != null)
                carousel.OnClubSelected -= UpdatePanel;

            if (ClubManager.Instance != null)
            {
                ClubManager.Instance.OnClubEquipped  -= OnClubEquippedChanged;
                ClubManager.Instance.OnClubRepaired  -= OnClubRepairedHandler;
            }

            LocalizationManager.OnLanguageChanged -= RefreshLocalizedText;
            Golfin.Social.LoanService.Instance.OnLoansChanged -= OnLoansChanged;
        }

        private void RefreshLocalizedText()
        {
            if (!string.IsNullOrEmpty(currentClubId))
                UpdatePanel(currentClubId);
        }

        // ── Data Binding ───────────────────────────────────────────────────────

        /// <summary>
        /// Force-shows a specific club regardless of compare mode.
        /// Called by ClubCompareController after a swap/equip action.
        /// </summary>
        public void ShowClub(string clubId)
        {
            currentClubId = clubId;
            UpdatePanel(clubId);
        }

        private void UpdatePanel(string clubId)
        {
            if (compareController != null && compareController.IsCompareMode) return;

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
                infoText.text = ClubInfoText.Resolve(template);

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
                equipImg.color = Color.white;

            if (bagLabel != null)
            {
                if (isEquipped)
                {
                    var bagData = BagDatabaseCSV.Instance?.GetBagBySlot(playerClub.equippedBagSlot);
                    string bagName = (bagData?.name ?? $"Bag {playerClub.equippedBagSlot}").ToUpper();
                    bagLabel.text = $"{LocalizationManager.Get("CLUB_IN_BAG")} {bagName}";
                }
                else
                {
                    bagLabel.text = " ";
                }
                bagLabel.color = new Color(bagLabel.color.r, bagLabel.color.g, bagLabel.color.b,
                    isEquipped ? 1f : 0f);
            }

            // ── Status Icons ──────────────────────────────────────────────────
            if (equippedIcon != null) equippedIcon.SetActive(isEquipped);

            // ── Button States ─────────────────────────────────────────────────
            bool atMax     = playerClub.currentLevel >= maxLevel;
            bool needsRepair = playerClub.currentDurability < playerClub.maxDurability;
            bool hasKits     = ItemManager.Instance != null && ItemManager.Instance.HasAnyRepairKit();
            if (levelUpButton != null) levelUpButton.interactable = !atMax;
            if (repairButton  != null) repairButton.interactable  = needsRepair && hasKits;

            // asset_loans §4.1 — LAST, so the loan layer wins over the ordinary button rules
            // above: a lent club's LEVEL UP must be off even when it is below max, and a borrowed
            // club's REPAIR must be off even when it is damaged and the player has kits.
            ApplyLoanState(clubId, playerClub);
        }

        // ── asset_loans §4.1 — the loan layer ─────────────────────────────────

        private void OnLoansChanged()
        {
            if (!string.IsNullOrEmpty(currentClubId)) UpdatePanel(currentClubId);
        }

        /// <summary>
        /// Paint the LEND / RETURN button, the ribbon, and the buttons the loan state overrides.
        ///
        /// <para>
        /// AN EQUIPPED CLUB *CAN* BE LENT, unlike a selected character — the difference is that
        /// unequipping is a side effect the player can be told about up front (the modal's amber
        /// warning line), whereas "who you are playing as" has no equivalent no-op resolution.
        /// Confirming the lend pulls the club out of the bag during reconciliation.
        /// </para>
        /// </summary>
        private void ApplyLoanState(string clubId, PlayerClubData playerClub)
        {
            Golfin.Social.LoanService loans = Golfin.Social.LoanService.Instance;
            bool lentOut  = loans.IsLentOut(Golfin.Social.LoanDto.KindClub, clubId,
                                            out Golfin.Social.LoanDto? outLoan);
            bool borrowed = loans.IsBorrowed(Golfin.Social.LoanDto.KindClub, clubId,
                                             out Golfin.Social.LoanDto? inLoan);

            // asset_loans_offers §3.2 — the same OFFERED narrowing the Roster panel takes, and
            // deliberately the same shape rather than a shared helper: the two panels disable
            // DIFFERENT button sets (REPAIR/EQUIP here, BOOST/SELECT there) and the only thing
            // they truly share is this one predicate, which is one line.
            bool offered = lentOut && outLoan != null && outLoan.IsPendingOffer();

            if (loanRibbon != null)
            {
                if (lentOut)       loanRibbon.Show(outLoan, asLender: true);
                else if (borrowed) loanRibbon.Show(inLoan,  asLender: false);
                else               loanRibbon.Clear();
            }

            if (lendButton != null) lendButton.gameObject.SetActive(true);
            if (lendButtonText != null)
                lendButtonText.text = LocalizationManager.Get(
                    offered  ? "LOAN_BTN_RESCIND"
                  : borrowed ? "LOAN_BTN_RETURN"
                             : "LOAN_BTN_LEND");
            // OFFERED is the one locked state with a live button — taking the offer back is what
            // a locked-by-offer club affords, and the only way out short of waiting 48 hours.
            if (lendButton != null) lendButton.interactable = offered || !lentOut;

            if (lentOut)
            {
                if (levelUpButton != null) levelUpButton.interactable = false;
                if (repairButton  != null) repairButton.interactable  = false;
                if (compareButton != null) compareButton.interactable = false;
                if (equipButton   != null) equipButton.interactable   = false;
            }
            else if (borrowed)
            {
                // Durability is FROZEN on a borrowed club (§3) — nothing wears it down, so there is
                // nothing to repair and the button is disabled rather than hidden (Figma
                // 14183:108675 swaps it to Silver Enabled=No, it does not remove it).
                if (repairButton  != null) repairButton.interactable  = false;
                if (compareButton != null) compareButton.interactable = true;
                if (equipButton   != null) equipButton.interactable   = true;
            }
            else
            {
                if (compareButton != null) compareButton.interactable = true;
                if (equipButton   != null) equipButton.interactable   = true;
            }
        }

        private void OnLendClicked()
        {
            if (string.IsNullOrEmpty(currentClubId)) return;

            Golfin.Social.LoanService loans = Golfin.Social.LoanService.Instance;

            // RESCIND first — an offered club is also "lent out" to every other lookup, so asking
            // the lend question first would open the lend modal on a club that is already locked.
            if (loans.IsOffered(Golfin.Social.LoanDto.KindClub, currentClubId,
                                out Golfin.Social.LoanDto? offerLoan))
            {
                loanRescindModal?.Open(offerLoan!, ClubDisplayName(currentClubId));
                return;
            }

            if (loans.IsBorrowed(Golfin.Social.LoanDto.KindClub, currentClubId,
                                 out Golfin.Social.LoanDto? inLoan))
            {
                loanReturnModal?.Open(inLoan!, ClubDisplayName(currentClubId));
                return;
            }

            var playerClub = ClubManager.Instance?.GetClubData(currentClubId);
            if (playerClub == null) return;

            loanModal?.Open(Golfin.Social.LoanDto.KindClub, currentClubId,
                            ClubDisplayName(currentClubId), playerClub.currentLevel,
                            clubIsEquipped: playerClub.IsEquipped);
        }

        private static string ClubDisplayName(string clubId)
        {
            var template = ClubDatabaseCSV.Instance?.GetClub(clubId);
            return template != null && !string.IsNullOrEmpty(template.name) ? template.name : clubId;
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
            if (levelUpModal != null)
                levelUpModal.Open(currentClubId, rightPanel);
            else
                Debug.Log($"[ClubDetailPanel] LEVEL UP clicked for '{currentClubId}' — wire ClubLevelUpModal.");
        }

        private void OnRepairClicked()
        {
            if (string.IsNullOrEmpty(currentClubId)) return;
            if (ClubManager.Instance == null || ItemManager.Instance == null) return;

            var playerClub = ClubManager.Instance.GetClubData(currentClubId);
            if (playerClub == null) return;

            var (newDurability, itemUsed) = ItemManager.Instance.UseBestRepairKit(
                playerClub.currentDurability, playerClub.maxDurability);

            if (itemUsed == null)
            {
                Debug.Log("[ClubDetailPanel] No repair kits available."); // TODO: Toast
                return;
            }

            int oldDurability = playerClub.currentDurability;
            ClubManager.Instance.RepairClub(currentClubId, newDurability);

            var template = ClubDatabaseCSV.Instance?.GetClub(currentClubId);
            string clubName = template?.name ?? currentClubId;
            Debug.Log($"[ClubDetailPanel] {clubName} repaired with {itemUsed}. " +
                      $"Durability {oldDurability} → {newDurability}."); // TODO: Toast
        }

        private void OnClubRepairedHandler(string repairedClubId)
        {
            if (repairedClubId == currentClubId) UpdatePanel(currentClubId);
        }

        private void OnCompareClicked()
        {
            if (compareController != null && !string.IsNullOrEmpty(currentClubId))
                compareController.EnterCompareMode(currentClubId);
            else
                Debug.Log($"[ClubDetailPanel] COMPARE clicked for '{currentClubId}' — run Wire Club Compare Panel.");
        }

        private void OnEquipClicked()
        {
            if (string.IsNullOrEmpty(currentClubId) || ClubManager.Instance == null) return;

            var playerClub = ClubManager.Instance.GetClubData(currentClubId);
            if (playerClub == null) return;

            if (playerClub.IsEquipped)
            {
                // Already equipped → remove from bag
                BagManager.Instance?.RemoveClubFromBag(currentClubId);
            }
            else
            {
                // Not equipped → open bag selection modal
                if (bagSelectionModal != null)
                    bagSelectionModal.Open(currentClubId);
                else
                    Debug.Log("[ClubDetailPanel] EQUIP clicked — wire BagSelectionModal.");
            }
            // Panel refreshes via OnClubEquippedChanged event
        }
    }
}
