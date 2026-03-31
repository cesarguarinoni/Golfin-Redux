#nullable enable
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Golfin.Inventory
{
    /// <summary>
    /// Manages Compare Mode for the Club Detail Panel.
    ///
    /// Attach to ClubDetailPanel (same GameObject as ClubDetailPanel component).
    ///
    /// State machine:
    ///   NORMAL → EnterCompareMode() → COMPARE (empty right)
    ///         → OnCarouselSelection() → COMPARE (filled right, with stat diffs)
    ///         → ExitCompareMode()    → NORMAL
    ///         → Swap/Equip           → NORMAL (new selection)
    ///
    /// Carousel interception: subscribes to ClubCarouselController.OnClubSelected.
    /// ClubDetailPanel checks IsCompareMode and skips UpdatePanel when true.
    /// </summary>
    public class ClubCompareController : MonoBehaviour
    {
        // ── Normal Mode ────────────────────────────────────────────────────────
        [Header("Normal Mode References")]
        [SerializeField] private GameObject      leftPanel   = null!;  // LeftPanel (image + info)
        [SerializeField] private RectTransform   rightPanel  = null!;  // RightPanel (stats)

        // ── Compare Mode Layout ────────────────────────────────────────────────
        [Header("Compare Mode References")]
        [SerializeField] private GameObject      compareRightPanel      = null!;
        [SerializeField] private GameObject      comparePlaceholder     = null!;
        [SerializeField] private TextMeshProUGUI comparePlaceholderText = null!;
        [SerializeField] private GameObject      compareInfoPanel       = null!;
        [SerializeField] private GameObject      verticalDivider        = null!;

        // ── Left Column Buttons ────────────────────────────────────────────────
        [Header("Left Column Buttons")]
        [SerializeField] private Button compareButton      = null!; // shown in normal mode
        [SerializeField] private Button closeCompareButton = null!; // shown in compare mode
        [SerializeField] private Button equipButton        = null!; // EQUIP/EQUIPPED (normal + compare if equipped)
        [SerializeField] private Button swapButton         = null!; // SWAP (compare mode, left not equipped)

        // ── Right Column — Club Info ────────────────────────────────────────────
        [Header("Right Column — Club Info")]
        [SerializeField] private TextMeshProUGUI compareNameText     = null!;
        [SerializeField] private TextMeshProUGUI compareRarityLabel  = null!;
        [SerializeField] private TextMeshProUGUI compareLevelText    = null!;
        [SerializeField] private TextMeshProUGUI compareMaxLevelText = null!;

        // ── Right Column — Stats ───────────────────────────────────────────────
        [Header("Right Column — Stat: Power")]
        [SerializeField] private TextMeshProUGUI comparePowerName   = null!;
        [SerializeField] private Image           comparePowerBar    = null!;
        [SerializeField] private TextMeshProUGUI comparePowerNumber = null!;
        [SerializeField] private TextMeshProUGUI comparePowerDiff   = null!;

        [Header("Right Column — Stat: Accuracy")]
        [SerializeField] private TextMeshProUGUI compareAccuracyName   = null!;
        [SerializeField] private Image           compareAccuracyBar    = null!;
        [SerializeField] private TextMeshProUGUI compareAccuracyNumber = null!;
        [SerializeField] private TextMeshProUGUI compareAccuracyDiff   = null!;

        [Header("Right Column — Stat: Lie Resistance")]
        [SerializeField] private TextMeshProUGUI compareLieResName   = null!;
        [SerializeField] private Image           compareLieResBar    = null!;
        [SerializeField] private TextMeshProUGUI compareLieResNumber = null!;
        [SerializeField] private TextMeshProUGUI compareLieResDiff   = null!;

        [Header("Right Column — Stat: Loft")]
        [SerializeField] private TextMeshProUGUI compareLoftName   = null!;
        [SerializeField] private Image           compareLoftBar    = null!;
        [SerializeField] private TextMeshProUGUI compareLoftNumber = null!;
        [SerializeField] private TextMeshProUGUI compareLoftDiff   = null!;

        [Header("Right Column — Stat: Durability")]
        [SerializeField] private TextMeshProUGUI compareDurabilityName   = null!;
        [SerializeField] private Image           compareDurabilityBar    = null!;
        [SerializeField] private TextMeshProUGUI compareDurabilityNumber = null!;
        [SerializeField] private TextMeshProUGUI compareDurabilityDiff   = null!;

        [Header("Right Column — Stat: Distance")]
        [SerializeField] private TextMeshProUGUI compareDistanceName  = null!;
        [SerializeField] private TextMeshProUGUI compareDistanceValue = null!;
        [SerializeField] private TextMeshProUGUI compareDistanceDiff  = null!;

        // ── Right Column — Buttons ─────────────────────────────────────────────
        [Header("Right Column — Buttons")]
        [SerializeField] private Button          compareLevelUpButton    = null!;
        [SerializeField] private Button          compareRepairButton     = null!;
        [SerializeField] private Button          compareRightCompareButton = null!;
        [SerializeField] private Button          compareRightEquipButton = null!;
        [SerializeField] private TextMeshProUGUI compareRightEquipText   = null!;
        [SerializeField] private TextMeshProUGUI compareBagLabel         = null!;

        // ── Status Icons ───────────────────────────────────────────────────────
        [Header("Status Icons")]
        [SerializeField] private GameObject? compareEquippedIcon;

        // ── Carousel ───────────────────────────────────────────────────────────
        [Header("Carousel")]
        [SerializeField] private ClubCarouselController? carousel;

        // ── Modals ─────────────────────────────────────────────────────────────
        [Header("Modals")]
        [SerializeField] private ClubLevelUpModalController?  levelUpModal;

        [Header("Bag Selection")]
        [SerializeField] private BagSelectionModalController? bagSelectionModal;

        // ── Animation ─────────────────────────────────────────────────────────
        [Header("Animation")]
        [SerializeField] private float slideDuration = 0.3f;
        [SerializeField] private float fadeDuration  = 0.2f;

        // ── Colors ─────────────────────────────────────────────────────────────
        private static readonly Color DiffPositiveColor  = new Color(0.2f, 0.8f, 0.3f, 1f);
        private static readonly Color DiffNegativeColor  = new Color(0.9f, 0.2f, 0.2f, 1f);
        private static readonly Color DurabilityLowColor = new Color(0.9f, 0.2f, 0.2f, 1f);
        private static readonly Color DurabilityOkColor  = new Color(0.2f, 0.5f, 0.9f, 1f);
        private static readonly Color EquippedGoldColor  = new Color(1f,   0.8f, 0.2f, 1f);

        private const int STAT_MAX = 100;

        // ── Runtime State ──────────────────────────────────────────────────────
        private bool    _isCompareMode  = false;
        private string  _leftClubId     = "";
        private string? _rightClubId    = null;
        private Vector2 _normalRightPos;
        private Vector2 _compareLeftPos;

        public bool IsCompareMode => _isCompareMode;

        // ── Lifecycle ──────────────────────────────────────────────────────────

        private void Awake()
        {
            SafeSetActive(compareRightPanel,              false);
            SafeSetActive(verticalDivider,                false);
            SafeSetActive(closeCompareButton?.gameObject, false);
            SafeSetActive(swapButton?.gameObject,         false);
        }

        private void Start()
        {
            closeCompareButton?.onClick.AddListener(ExitCompareMode);
            swapButton?.onClick.AddListener(OnSwapClicked);
            compareRightCompareButton?.onClick.AddListener(OnRightCompareClicked);
            compareRightEquipButton?.onClick.AddListener(OnRightEquipClicked);
            compareLevelUpButton?.onClick.AddListener(OnRightLevelUpClicked);
            compareRepairButton?.onClick.AddListener(OnRightRepairClicked);
        }

        private void OnEnable()
        {
            if (carousel != null)
                carousel.OnClubSelected += OnCarouselSelection;

            if (ClubManager.Instance != null)
            {
                ClubManager.Instance.OnClubLeveledUp += OnClubLeveledUp;
                ClubManager.Instance.OnClubRepaired  += OnClubRepairedHandler;
            }

            LocalizationManager.OnLanguageChanged += RefreshLocalizedText;
        }

        private void OnDisable()
        {
            if (carousel != null)
                carousel.OnClubSelected -= OnCarouselSelection;

            if (ClubManager.Instance != null)
            {
                ClubManager.Instance.OnClubLeveledUp -= OnClubLeveledUp;
                ClubManager.Instance.OnClubRepaired  -= OnClubRepairedHandler;
            }

            LocalizationManager.OnLanguageChanged -= RefreshLocalizedText;
        }

        private void OnClubLeveledUp(string leveledClubId)
        {
            // Refresh compare right column if the leveled club is currently displayed there
            if (_isCompareMode && leveledClubId == _rightClubId && !string.IsNullOrEmpty(_rightClubId))
                RefreshRightColumn(_rightClubId);
        }

        private void OnClubRepairedHandler(string repairedClubId)
        {
            if (_isCompareMode && repairedClubId == _rightClubId && !string.IsNullOrEmpty(_rightClubId))
                RefreshRightColumn(_rightClubId);
        }

        private void RefreshLocalizedText()
        {
            if (comparePlaceholderText != null)
                comparePlaceholderText.text = LocalizationManager.Get("CLUB_COMPARE_EMPTY_PROMPT");

            if (!string.IsNullOrEmpty(_rightClubId))
                RefreshRightColumn(_rightClubId);
        }

        // ── Enter / Exit ───────────────────────────────────────────────────────

        /// <summary>Called by ClubDetailPanel when COMPARE is tapped.</summary>
        public void EnterCompareMode(string clubId)
        {
            if (_isCompareMode) return;

            _isCompareMode = true;
            _leftClubId    = clubId;
            _rightClubId   = null;

            _normalRightPos = rightPanel.anchoredPosition;

            var detailRect = rightPanel.parent?.GetComponent<RectTransform>();
            float detailW  = detailRect != null ? detailRect.rect.width : 800f;
            _compareLeftPos = new Vector2(-detailW * 0.25f, _normalRightPos.y);

            StopAllCoroutines();

            // Hide image+info panel immediately
            SafeSetActive(leftPanel, false);

            // Slide stats panel to left half
            StartCoroutine(SlidePanel(rightPanel, _compareLeftPos, slideDuration));

            // Show placeholder, hide info; fade in the compare right panel
            if (comparePlaceholderText != null)
                comparePlaceholderText.text = LocalizationManager.Get("CLUB_COMPARE_EMPTY_PROMPT");

            SafeSetActive(comparePlaceholder, true);
            SafeSetActive(compareInfoPanel,   false);
            StartCoroutine(FadeIn(compareRightPanel, fadeDuration, slideDuration));

            SafeSetActive(verticalDivider, true);

            // Swap buttons
            SafeSetActive(compareButton?.gameObject,      false);
            SafeSetActive(closeCompareButton?.gameObject, true);
            UpdateLeftColumnButtons();

            Debug.Log($"[ClubCompareController] Entered compare mode for {clubId}");
        }

        /// <summary>Called by CLOSE COMPARE button or same-club tap in carousel.</summary>
        public void ExitCompareMode()
        {
            if (!_isCompareMode) return;
            Debug.Log("[ClubCompareController] Exited compare mode");
            CleanupAndExit();
        }

        // ── Carousel Interception ──────────────────────────────────────────────

        private void OnCarouselSelection(string clubId)
        {
            if (!_isCompareMode) return;

            // Same club → exit compare
            if (clubId == _leftClubId)
            {
                ExitCompareMode();
                return;
            }

            // Different club → populate right column
            _rightClubId = clubId;

            StopAllCoroutines();

            // Ensure compare panel fully visible
            SafeSetActive(compareRightPanel, true);
            SetAlpha(compareRightPanel, 1f);

            SafeSetActive(comparePlaceholder, false);
            SafeSetActive(compareInfoPanel,   true);

            RefreshRightColumn(clubId);
            UpdateLeftColumnButtons();

            Debug.Log($"[ClubCompareController] Comparing {_leftClubId} vs {clubId}");
        }

        // ── Data Binding ───────────────────────────────────────────────────────

        private void RefreshRightColumn(string clubId)
        {
            if (ClubManager.Instance == null || ClubDatabaseCSV.Instance == null) return;

            var rightPlayerClub = ClubManager.Instance.GetClubData(clubId);
            var rightTemplate   = ClubDatabaseCSV.Instance.GetClub(clubId);
            if (rightPlayerClub == null || rightTemplate == null) return;

            var leftPlayerClub = ClubManager.Instance.GetClubData(_leftClubId);
            var leftTemplate   = ClubDatabaseCSV.Instance.GetClub(_leftClubId);

            // ── Name ──────────────────────────────────────────────────────────
            if (compareNameText != null)
                compareNameText.text = rightTemplate.name.ToUpper();

            // ── Rarity + Level ────────────────────────────────────────────────
            var rarity = rightTemplate.rarity;
            if (compareRarityLabel != null)
            {
                compareRarityLabel.text  = LocalizationManager.Get($"RARITY_{rarity.ToString().ToUpper()}");
                compareRarityLabel.color = Golfin.Roster.RarityHelper.GetRarityColor(rarity);
            }
            if (compareLevelText    != null) compareLevelText.text    = $"Lv {rightPlayerClub.currentLevel}";

            int maxLevel = ClubManager.Instance.GetMaxLevel(clubId);
            if (compareMaxLevelText != null) compareMaxLevelText.text = $"/{maxLevel}";

            // ── Gather stat values ────────────────────────────────────────────
            int rightPower       = rightPlayerClub.GetPower(rightTemplate);
            int rightAccuracy    = rightPlayerClub.GetAccuracy(rightTemplate);
            int rightLieRes      = rightPlayerClub.GetLieResistance(rightTemplate);
            int rightLoft        = rightPlayerClub.GetLoft(rightTemplate);
            int rightDurability  = rightPlayerClub.currentDurability;
            int rightDurMax      = rightPlayerClub.maxDurability;
            int rightDistance    = rightPlayerClub.GetDistance(rightTemplate);

            int leftPower      = leftPlayerClub != null && leftTemplate != null ? leftPlayerClub.GetPower(leftTemplate)          : 0;
            int leftAccuracy   = leftPlayerClub != null && leftTemplate != null ? leftPlayerClub.GetAccuracy(leftTemplate)       : 0;
            int leftLieRes     = leftPlayerClub != null && leftTemplate != null ? leftPlayerClub.GetLieResistance(leftTemplate)  : 0;
            int leftLoft       = leftPlayerClub != null && leftTemplate != null ? leftPlayerClub.GetLoft(leftTemplate)           : 0;
            int leftDurability = leftPlayerClub != null                         ? leftPlayerClub.currentDurability               : 0;
            int leftDistance   = leftPlayerClub != null && leftTemplate != null ? leftPlayerClub.GetDistance(leftTemplate)       : 0;

            // ── Power ─────────────────────────────────────────────────────────
            UpdateCompareStatBar(comparePowerName, comparePowerBar, comparePowerNumber,
                LocalizationManager.Get("CLUB_POWER"), rightPower, STAT_MAX);
            ShowStatDifference(comparePowerDiff, leftPower, rightPower);

            // ── Accuracy ──────────────────────────────────────────────────────
            UpdateCompareStatBar(compareAccuracyName, compareAccuracyBar, compareAccuracyNumber,
                LocalizationManager.Get("CLUB_ACCURACY"), rightAccuracy, STAT_MAX);
            ShowStatDifference(compareAccuracyDiff, leftAccuracy, rightAccuracy);

            // ── Lie Resistance ────────────────────────────────────────────────
            UpdateCompareStatBar(compareLieResName, compareLieResBar, compareLieResNumber,
                LocalizationManager.Get("CLUB_LIE_RESISTANCE"), rightLieRes, STAT_MAX);
            ShowStatDifference(compareLieResDiff, leftLieRes, rightLieRes);

            // ── Loft ──────────────────────────────────────────────────────────
            UpdateCompareStatBar(compareLoftName, compareLoftBar, compareLoftNumber,
                LocalizationManager.Get("CLUB_LOFT"), rightLoft, STAT_MAX);
            ShowStatDifference(compareLoftDiff, leftLoft, rightLoft);

            // ── Durability (current/max with low-color) ───────────────────────
            if (compareDurabilityName   != null) compareDurabilityName.text   = LocalizationManager.Get("CLUB_DURABILITY");
            if (compareDurabilityNumber != null) compareDurabilityNumber.text = $"{rightDurability}/{rightDurMax}";
            if (compareDurabilityBar    != null)
            {
                if (compareDurabilityBar.type != Image.Type.Filled)
                {
                    compareDurabilityBar.type       = Image.Type.Filled;
                    compareDurabilityBar.fillMethod = Image.FillMethod.Horizontal;
                    compareDurabilityBar.fillOrigin = 0;
                }
                compareDurabilityBar.fillAmount = rightDurMax > 0 ? (float)rightDurability / rightDurMax : 0f;
                compareDurabilityBar.color      = rightPlayerClub.IsDurabilityLow ? DurabilityLowColor : DurabilityOkColor;
            }
            ShowStatDifference(compareDurabilityDiff, leftDurability, rightDurability);

            // ── Distance (value, no bar) ───────────────────────────────────────
            if (compareDistanceName  != null) compareDistanceName.text  = LocalizationManager.Get("CLUB_DISTANCE");
            if (compareDistanceValue != null) compareDistanceValue.text = $"{rightDistance} yd";
            ShowStatDifference(compareDistanceDiff, leftDistance, rightDistance);

            // ── Equip state ───────────────────────────────────────────────────
            bool isEquipped = rightPlayerClub.IsEquipped;
            if (compareRightEquipText != null)
                compareRightEquipText.text = isEquipped
                    ? LocalizationManager.Get("CLUB_EQUIPPED")
                    : LocalizationManager.Get("CLUB_EQUIP");

            var equipImg = compareRightEquipButton == null ? null : compareRightEquipButton.GetComponent<Image>();
            if (equipImg != null) equipImg.color = Color.white;

            if (compareBagLabel != null)
            {
                if (isEquipped)
                {
                    var bagData = BagDatabaseCSV.Instance?.GetBagBySlot(rightPlayerClub.equippedBagSlot);
                    string bagName = (bagData?.name ?? $"Bag {rightPlayerClub.equippedBagSlot}").ToUpper();
                    compareBagLabel.text = $"{LocalizationManager.Get("CLUB_IN_BAG")} {bagName}";
                }
                else
                {
                    compareBagLabel.text = " ";
                }
                compareBagLabel.color = new Color(compareBagLabel.color.r, compareBagLabel.color.g, compareBagLabel.color.b,
                    isEquipped ? 1f : 0f);
            }

            if (compareEquippedIcon != null) compareEquippedIcon.SetActive(isEquipped);

            // ── Button states ─────────────────────────────────────────────────
            bool atMax       = rightPlayerClub.currentLevel >= maxLevel;
            bool needsRepair = rightPlayerClub.currentDurability < rightPlayerClub.maxDurability;
            bool hasKits     = ItemManager.Instance != null && ItemManager.Instance.HasAnyRepairKit();
            if (compareLevelUpButton != null) compareLevelUpButton.interactable = !atMax;
            if (compareRepairButton  != null) compareRepairButton.interactable  = needsRepair && hasKits;

            // Already in compare mode — disable this button
            if (compareRightCompareButton != null) compareRightCompareButton.interactable = false;
        }

        private void UpdateCompareStatBar(TextMeshProUGUI? nameField, Image? bar,
            TextMeshProUGUI? numberField, string label, int value, int cap)
        {
            if (nameField   != null) nameField.text   = label;
            if (numberField != null) numberField.text = $"{value}/{cap}";
            if (bar         != null)
            {
                if (bar.type != Image.Type.Filled)
                {
                    bar.type       = Image.Type.Filled;
                    bar.fillMethod = Image.FillMethod.Horizontal;
                    bar.fillOrigin = 0;
                }
                bar.fillAmount = cap > 0 ? (float)value / cap : 0f;
            }
        }

        private void ShowStatDifference(TextMeshProUGUI? diffLabel, int leftValue, int rightValue)
        {
            if (diffLabel == null) return;

            int diff = rightValue - leftValue;
            if (diff > 0)
            {
                diffLabel.text  = $"+{diff}";
                diffLabel.color = DiffPositiveColor;
                diffLabel.gameObject.SetActive(true);
            }
            else if (diff < 0)
            {
                diffLabel.text  = $"{diff}";
                diffLabel.color = DiffNegativeColor;
                diffLabel.gameObject.SetActive(true);
            }
            else
            {
                diffLabel.gameObject.SetActive(false);
            }
        }

        // ── Button State Helpers ───────────────────────────────────────────────

        private void UpdateLeftColumnButtons()
        {
            var leftClub     = ClubManager.Instance == null ? null : ClubManager.Instance.GetClubData(_leftClubId);
            bool leftEquipped = leftClub?.IsEquipped ?? false;

            // Show SWAP only when a right club is selected AND that club is already in a bag
            bool rightEquipped = false;
            if (!string.IsNullOrEmpty(_rightClubId) && ClubManager.Instance != null)
            {
                var rightClub = ClubManager.Instance.GetClubData(_rightClubId);
                rightEquipped = rightClub?.IsEquipped ?? false;
            }

            bool showSwap = rightEquipped;
            SafeSetActive(swapButton?.gameObject,  showSwap);
            SafeSetActive(equipButton?.gameObject, !showSwap);

            // Keep equip button text correct when it is showing instead of SWAP
            if (!showSwap && equipButton != null)
            {
                var btnText = equipButton.GetComponentInChildren<TextMeshProUGUI>();
                if (btnText != null)
                    btnText.text = leftEquipped
                        ? LocalizationManager.Get("CLUB_EQUIPPED")
                        : LocalizationManager.Get("CLUB_EQUIP");
            }
        }

        // ── Button Handlers ────────────────────────────────────────────────────

        private void OnSwapClicked()
        {
            // Equip the left club and exit
            if (string.IsNullOrEmpty(_leftClubId) || ClubManager.Instance == null) return;
            ClubManager.Instance.EquipClub(_leftClubId, 1);
            Debug.Log($"[ClubCompareController] Equipped (SWAP) {_leftClubId}");

            CleanupAndExit();
            GetComponent<ClubDetailPanel>()?.ShowClub(_leftClubId);
        }

        private void OnRightEquipClicked()
        {
            if (string.IsNullOrEmpty(_rightClubId) || ClubManager.Instance == null) return;

            var club = ClubManager.Instance.GetClubData(_rightClubId);
            if (club == null) return;

            if (!club.IsEquipped)
            {
                // Open bag selection modal
                if (bagSelectionModal != null)
                    bagSelectionModal.Open(_rightClubId);
                else
                    Debug.Log($"[ClubCompareController] EQUIP clicked for '{_rightClubId}' — wire BagSelectionModal.");
            }
            else
            {
                // Already equipped → remove from bag, exit compare
                BagManager.Instance?.RemoveClubFromBag(_rightClubId);
                CleanupAndExit();
                GetComponent<ClubDetailPanel>()?.ShowClub(_rightClubId);
            }
        }

        private void OnRightCompareClicked()
        {
            // Make right club the new left, start fresh compare
            if (string.IsNullOrEmpty(_rightClubId)) return;
            string newLeft = _rightClubId;
            ForceExitImmediate();
            EnterCompareMode(newLeft);
        }

        private void OnRightLevelUpClicked()
        {
            if (string.IsNullOrEmpty(_rightClubId)) return;

            if (levelUpModal != null)
                levelUpModal.Open(_rightClubId, compareRightPanel.GetComponent<RectTransform>());
            else
                Debug.Log($"[ClubCompareController] Level Up clicked for '{_rightClubId}' — wire ClubLevelUpModal.");
        }

        private void OnRightRepairClicked()
        {
            if (string.IsNullOrEmpty(_rightClubId)) return;
            if (ClubManager.Instance == null || ItemManager.Instance == null) return;

            var playerClub = ClubManager.Instance.GetClubData(_rightClubId);
            if (playerClub == null) return;

            var (newDurability, itemUsed) = ItemManager.Instance.UseBestRepairKit(
                playerClub.currentDurability, playerClub.maxDurability);

            if (itemUsed == null)
            {
                Debug.Log("[ClubCompareController] No repair kits available."); // TODO: Toast
                return;
            }

            int oldDurability = playerClub.currentDurability;
            ClubManager.Instance.RepairClub(_rightClubId, newDurability);

            var template = ClubDatabaseCSV.Instance?.GetClub(_rightClubId);
            string clubName = template?.name ?? _rightClubId;
            Debug.Log($"[ClubCompareController] {clubName} repaired with {itemUsed}. " +
                      $"Durability {oldDurability} → {newDurability}."); // TODO: Toast
        }

        // ── Cleanup ────────────────────────────────────────────────────────────

        private void CleanupAndExit()
        {
            _isCompareMode = false;
            _rightClubId   = null;

            StopAllCoroutines();
            StartCoroutine(FadeOut(compareRightPanel, fadeDuration));
            SafeSetActive(verticalDivider, false);
            StartCoroutine(SlidePanel(rightPanel, _normalRightPos, slideDuration));
            SafeSetActive(leftPanel, true);

            SafeSetActive(compareButton?.gameObject,      true);
            SafeSetActive(closeCompareButton?.gameObject, false);
            SafeSetActive(swapButton?.gameObject,         false);
            SafeSetActive(equipButton?.gameObject,        true);
        }

        private void ForceExitImmediate()
        {
            StopAllCoroutines();
            _isCompareMode = false;
            _rightClubId   = null;

            SetAlpha(compareRightPanel, 0f);
            SafeSetActive(compareRightPanel, false);
            SafeSetActive(verticalDivider,   false);

            if (rightPanel != null) rightPanel.anchoredPosition = _normalRightPos;

            SafeSetActive(leftPanel, true);

            SafeSetActive(compareButton?.gameObject,      true);
            SafeSetActive(closeCompareButton?.gameObject, false);
            SafeSetActive(swapButton?.gameObject,         false);
            SafeSetActive(equipButton?.gameObject,        true);
        }

        // ── Animation Coroutines ───────────────────────────────────────────────

        private IEnumerator FadeOut(GameObject obj, float duration)
        {
            if (obj == null) yield break;
            var cg = GetOrAddCG(obj);
            cg.alpha = 1f;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                cg.alpha = 1f - t / duration;
                yield return null;
            }
            cg.alpha = 0f;
            obj.SetActive(false);
        }

        private IEnumerator FadeIn(GameObject obj, float duration, float delay = 0f)
        {
            if (obj == null) yield break;
            if (delay > 0f) yield return new WaitForSeconds(delay);
            obj.SetActive(true);
            var cg = GetOrAddCG(obj);
            cg.alpha = 0f;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                cg.alpha = t / duration;
                yield return null;
            }
            cg.alpha = 1f;
        }

        private IEnumerator SlidePanel(RectTransform panel, Vector2 target, float duration)
        {
            if (panel == null) yield break;
            Vector2 start = panel.anchoredPosition;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float s = Mathf.SmoothStep(0f, 1f, t / duration);
                panel.anchoredPosition = Vector2.Lerp(start, target, s);
                yield return null;
            }
            panel.anchoredPosition = target;
        }

        // ── Utilities ──────────────────────────────────────────────────────────

        private static CanvasGroup GetOrAddCG(GameObject obj)
        {
            // Do NOT use ?? here — Unity's == null is overloaded; ?? uses C# reference equality
            var cg = obj.GetComponent<CanvasGroup>();
            if (cg == null) cg = obj.AddComponent<CanvasGroup>();
            return cg;
        }

        private static void SetAlpha(GameObject obj, float alpha)
        {
            if (obj == null) return;
            var cg = obj.GetComponent<CanvasGroup>();
            if (cg != null) cg.alpha = alpha;
        }

        private static void SafeSetActive(GameObject? obj, bool active)
        {
            if (obj != null) obj.SetActive(active);
        }
    }
}
