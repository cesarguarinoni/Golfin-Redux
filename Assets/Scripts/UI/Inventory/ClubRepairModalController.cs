#nullable enable
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Golfin.Roster;       // RarityHelper
using Golfin.UI.Modals;

namespace Golfin.Inventory
{
    /// <summary>
    /// Club Repair Modal — Phase E2.
    ///
    /// Simpler than ClubLevelUpModalController — no SP allocation, no level preview.
    /// Flow: pick kit type → see durability preview → confirm → kit consumed, club repaired.
    ///
    /// Repair uses Repair Kits (via RepairKitManager), NOT RP.
    /// </summary>
    public class ClubRepairModalController : ModalController
    {
        // ── Club Info ─────────────────────────────────────────────────────────
        [Header("Club Info")]
        [SerializeField] private TextMeshProUGUI clubNameText = null!;
        [SerializeField] private TextMeshProUGUI rarityLabel  = null!;
        [SerializeField] private TextMeshProUGUI levelText    = null!;

        // ── Durability Display ────────────────────────────────────────────────
        [Header("Durability Display")]
        [SerializeField] private TextMeshProUGUI durabilityLabel       = null!;
        [SerializeField] private Image           durabilityBar         = null!;
        [SerializeField] private Image           durabilityBarPreview  = null!;
        [SerializeField] private TextMeshProUGUI durabilityValueText   = null!;
        [SerializeField] private TextMeshProUGUI durabilityChangeText  = null!;

        // ── Kit Selection ─────────────────────────────────────────────────────
        [Header("Kit Selection")]
        [SerializeField] private Button          standardKitButton    = null!;
        [SerializeField] private TextMeshProUGUI standardKitLabel     = null!;
        [SerializeField] private TextMeshProUGUI standardKitCountText = null!;
        [SerializeField] private GameObject      standardKitSelected  = null!;
        [SerializeField] private Button          premiumKitButton     = null!;
        [SerializeField] private TextMeshProUGUI premiumKitLabel      = null!;
        [SerializeField] private TextMeshProUGUI premiumKitCountText  = null!;
        [SerializeField] private GameObject      premiumKitSelected   = null!;
        [SerializeField] private TextMeshProUGUI noKitsMessage        = null!;

        // ── Actions ───────────────────────────────────────────────────────────
        [Header("Actions")]
        [SerializeField] private Button cancelButton  = null!;
        [SerializeField] private Button confirmButton = null!;

        // ── Static Labels ─────────────────────────────────────────────────────
        [Header("Static Labels")]
        [SerializeField] private TextMeshProUGUI cancelButtonLabel  = null!;
        [SerializeField] private TextMeshProUGUI confirmButtonLabel = null!;

        // ── Colors ────────────────────────────────────────────────────────────
        private static readonly Color PreviewBarColor  = new Color(0.2f, 0.8f, 0.2f, 0.6f);  // translucent green
        private static readonly Color ChangeTextColor  = new Color(0.2f, 0.8f, 0.2f, 1f);    // solid green
        private static readonly Color DurabilityOkColor = new Color(0.2f, 0.5f, 0.9f, 1f);  // blue

        // ── State ─────────────────────────────────────────────────────────────
        private string clubId = "";

        private enum KitType { None, Standard, Premium }
        private KitType selectedKit = KitType.None;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void OnEnable()
        {
            LocalizationManager.OnLanguageChanged += RefreshLocalizedText;

            if (RepairKitManager.Instance != null)
                RepairKitManager.Instance.OnInventoryChanged += OnKitInventoryChanged;
        }

        private void OnDisable()
        {
            LocalizationManager.OnLanguageChanged -= RefreshLocalizedText;

            if (RepairKitManager.Instance != null)
                RepairKitManager.Instance.OnInventoryChanged -= OnKitInventoryChanged;
        }

        private void Start()
        {
            standardKitButton?.onClick.AddListener(OnStandardKitSelected);
            premiumKitButton?.onClick.AddListener(OnPremiumKitSelected);
            cancelButton?.onClick.AddListener(OnCancelClicked);
            confirmButton?.onClick.AddListener(OnConfirmClicked);
        }

        private void OnKitInventoryChanged()
        {
            if (!string.IsNullOrEmpty(clubId)) RefreshDisplay();
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Opens the modal for the given club.
        /// anchorPanel parameter kept for API compatibility — not used for positioning.
        /// </summary>
        public void Open(string clubId, RectTransform? anchorPanel = null)
        {
            this.clubId  = clubId;
            selectedKit  = KitType.None;

            // Auto-select a kit type if available
            var kitMgr = RepairKitManager.Instance;
            if (kitMgr != null)
            {
                if (kitMgr.GetStandardCount() > 0)
                    selectedKit = KitType.Standard;
                else if (kitMgr.GetPremiumCount() > 0)
                    selectedKit = KitType.Premium;
            }

            RefreshLocalizedText();
            Show();
        }

        // ── Display ───────────────────────────────────────────────────────────

        private void RefreshLocalizedText()
        {
            if (durabilityLabel    != null) durabilityLabel.text    = LocalizationManager.Get("CLUB_REPAIR_DURABILITY");
            if (standardKitLabel   != null) standardKitLabel.text   = LocalizationManager.Get("CLUB_REPAIR_STANDARD_KIT");
            if (premiumKitLabel    != null) premiumKitLabel.text    = LocalizationManager.Get("CLUB_REPAIR_PREMIUM_KIT");
            if (noKitsMessage      != null) noKitsMessage.text      = LocalizationManager.Get("CLUB_REPAIR_NO_KITS");
            if (cancelButtonLabel  != null) cancelButtonLabel.text  = LocalizationManager.Get("CLUB_REPAIR_CANCEL");
            if (confirmButtonLabel != null) confirmButtonLabel.text = LocalizationManager.Get("CLUB_REPAIR_CONFIRM");

            if (!string.IsNullOrEmpty(clubId)) RefreshDisplay();
        }

        private void RefreshDisplay()
        {
            if (ClubManager.Instance == null || ClubDatabaseCSV.Instance == null) return;

            var playerClub = ClubManager.Instance.GetClubData(clubId);
            var template   = ClubDatabaseCSV.Instance.GetClub(clubId);
            if (playerClub == null || template == null)
            {
                Debug.LogError($"[ClubRepairModal] RefreshDisplay: data null for '{clubId}'");
                return;
            }

            // ── Club Info ──────────────────────────────────────────────────────
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
                levelText.text = $"Lv {playerClub.currentLevel}/{maxLevel}";

            // ── Durability ─────────────────────────────────────────────────────
            int curDur = playerClub.currentDurability;
            int maxDur = playerClub.maxDurability;
            bool atFullDurability = curDur >= maxDur;

            if (durabilityValueText != null)
                durabilityValueText.text = $"{curDur}/{maxDur}";

            if (durabilityBar != null)
            {
                durabilityBar.fillAmount = maxDur > 0 ? (float)curDur / maxDur : 0f;
                durabilityBar.color      = DurabilityOkColor;
            }

            // ── Kit Inventory ──────────────────────────────────────────────────
            var kitMgr       = RepairKitManager.Instance;
            int standardCount = kitMgr != null ? kitMgr.GetStandardCount() : 0;
            int premiumCount  = kitMgr != null ? kitMgr.GetPremiumCount()  : 0;
            bool hasAnyKit    = kitMgr != null && kitMgr.HasAnyKit();

            if (standardKitCountText != null) standardKitCountText.text = $"×{standardCount}";
            if (premiumKitCountText  != null) premiumKitCountText.text  = $"×{premiumCount}";

            // Show "no kits" message when out of kits or at full durability
            bool showNoKitsMsg = !hasAnyKit || atFullDurability;
            if (noKitsMessage != null)
            {
                noKitsMessage.gameObject.SetActive(showNoKitsMsg);
                if (atFullDurability && hasAnyKit)
                    noKitsMessage.text = LocalizationManager.Get("CLUB_REPAIR_FULL_DURABILITY");
                else
                    noKitsMessage.text = LocalizationManager.Get("CLUB_REPAIR_NO_KITS");
            }

            // Disable kit buttons when not available or club is full
            if (standardKitButton != null)
                standardKitButton.interactable = standardCount > 0 && !atFullDurability;
            if (premiumKitButton != null)
                premiumKitButton.interactable = premiumCount > 0 && !atFullDurability;

            // ── Selection highlight ────────────────────────────────────────────
            if (standardKitSelected != null)
                standardKitSelected.SetActive(selectedKit == KitType.Standard);
            if (premiumKitSelected != null)
                premiumKitSelected.SetActive(selectedKit == KitType.Premium);

            // ── Durability Preview ─────────────────────────────────────────────
            int previewDur = ComputePreviewDurability(curDur, maxDur);
            int durChange  = previewDur - curDur;
            bool showPreview = selectedKit != KitType.None && durChange > 0;

            if (durabilityBarPreview != null)
            {
                durabilityBarPreview.gameObject.SetActive(showPreview);
                if (showPreview)
                {
                    durabilityBarPreview.fillAmount = maxDur > 0 ? (float)previewDur / maxDur : 0f;
                    durabilityBarPreview.color      = PreviewBarColor;
                }
            }

            if (durabilityChangeText != null)
            {
                durabilityChangeText.gameObject.SetActive(showPreview);
                if (showPreview)
                {
                    durabilityChangeText.text  = $"+{durChange}";
                    durabilityChangeText.color = ChangeTextColor;
                }
            }

            // ── Confirm button ─────────────────────────────────────────────────
            bool canConfirm = selectedKit != KitType.None && !atFullDurability;
            if (confirmButton != null) confirmButton.interactable = canConfirm;
        }

        private int ComputePreviewDurability(int currentDurability, int maxDurability)
        {
            switch (selectedKit)
            {
                case KitType.Standard:
                    int restored = Mathf.CeilToInt(maxDurability * RepairKitManager.STANDARD_RESTORE_PERCENT);
                    return Mathf.Min(currentDurability + restored, maxDurability);
                case KitType.Premium:
                    return maxDurability;
                default:
                    return currentDurability;
            }
        }

        // ── Button Handlers ───────────────────────────────────────────────────

        private void OnStandardKitSelected()
        {
            selectedKit = KitType.Standard;
            RefreshDisplay();
        }

        private void OnPremiumKitSelected()
        {
            selectedKit = KitType.Premium;
            RefreshDisplay();
        }

        private void OnCancelClicked()
        {
            selectedKit = KitType.None;
            Debug.Log("[ClubRepairModal] Cancelled.");
            Hide();
        }

        private void OnConfirmClicked()
        {
            if (ClubManager.Instance == null || RepairKitManager.Instance == null) return;

            var playerClub = ClubManager.Instance.GetClubData(clubId);
            if (playerClub == null) return;

            int curDur = playerClub.currentDurability;
            int maxDur = playerClub.maxDurability;
            int newDur;

            if (selectedKit == KitType.Standard)
                newDur = RepairKitManager.Instance.UseStandardKit(curDur, maxDur);
            else if (selectedKit == KitType.Premium)
                newDur = RepairKitManager.Instance.UsePremiumKit(maxDur);
            else
                return;

            ClubManager.Instance.RepairClub(clubId, newDur);

            Debug.Log($"[ClubRepairModal] Repaired '{clubId}': {curDur} → {newDur}/{maxDur} using {selectedKit}"); // TODO: Toast

            selectedKit = KitType.None;
            Hide();
        }
    }
}
