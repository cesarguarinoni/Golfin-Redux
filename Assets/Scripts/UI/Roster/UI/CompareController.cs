#nullable enable
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Golfin.Roster
{
    /// <summary>
    /// Manages Compare Mode for the detail panel.
    ///
    /// Attach to DetailPanel (or any active child).
    ///
    /// State machine:
    ///   NORMAL → EnterCompareMode() → COMPARE (empty right)
    ///         → OnCarouselSelection() → COMPARE (filled right)
    ///         → ExitCompareMode()    → NORMAL
    ///         → Swap/Select          → NORMAL (new selection)
    ///
    /// Carousel interception: subscribes to CarouselController.OnCharacterSelected.
    /// CharacterDetailPanel checks IsCompareMode and skips UpdatePanel when true.
    /// </summary>
    public class CompareController : MonoBehaviour
    {
        // ── Normal Mode ────────────────────────────────────────────────────────
        [Header("Normal Mode References")]
        [SerializeField] private GameObject leftPanel        = null!;  // LeftPanel (portrait)
        [SerializeField] private RectTransform rightPanel    = null!;  // RightPanel (info)

        // ── Compare Mode Layout ────────────────────────────────────────────────
        [Header("Compare Mode References")]
        [SerializeField] private GameObject compareRightPanel  = null!; // new right column
        [SerializeField] private GameObject comparePlaceholder = null!; // "TAP…" overlay
        [SerializeField] private TextMeshProUGUI comparePlaceholderText = null!; // TMP inside overlay
        [SerializeField] private GameObject compareInfoPanel   = null!; // actual data
        [SerializeField] private GameObject verticalDivider    = null!; // 1px line

        // ── Left Column Button Swap ────────────────────────────────────────────
        [Header("Left Column Buttons")]
        [SerializeField] private Button compareButton      = null!; // shown in normal mode
        [SerializeField] private Button closeCompareButton = null!; // shown in compare mode
        [SerializeField] private Button selectButton       = null!; // SELECT / SELECTED
        [SerializeField] private Button swapButton         = null!; // SWAP (left col, compare)

        // ── Right Column Info ──────────────────────────────────────────────────
        [Header("Right Column — Character Info")]
        [SerializeField] private TextMeshProUGUI compareNameText               = null!;
        [SerializeField] private TextMeshProUGUI compareRarityLabel            = null!;
        [SerializeField] private TextMeshProUGUI compareLevelText              = null!;
        [SerializeField] private TextMeshProUGUI compareMaxLevelText           = null!;
        [SerializeField] private GameObject      compareStrengthRow            = null!;
        [SerializeField] private GameObject      compareClubControlRow         = null!;
        [SerializeField] private GameObject      compareRecoveryRow            = null!;
        [SerializeField] private GameObject      compareStaminaRow             = null!;
        [SerializeField] private Button          compareLevelUpButton          = null!;
        [SerializeField] private Button          compareBoostButton            = null!;
        [SerializeField] private TextMeshProUGUI compareBioText                = null!;
        [SerializeField] private Button          compareRightCompareButton     = null!;
        [SerializeField] private Button          compareRightSelectButton      = null!;
        [SerializeField] private TextMeshProUGUI compareRightSelectButtonText  = null!;

        [Header("Right Column — Stat Diffs")]
        [SerializeField] private TextMeshProUGUI? strengthDiffLabel;
        [SerializeField] private TextMeshProUGUI? clubControlDiffLabel;
        [SerializeField] private TextMeshProUGUI? recoveryDiffLabel;
        [SerializeField] private TextMeshProUGUI? staminaDiffLabel;

        [Header("Status Icons — Right Column")]
        [SerializeField] private GameObject? compareSelectedIcon;
        [SerializeField] private GameObject? compareLevelUpReadyIcon; // IconLevelUpBig — wire in Inspector
        [SerializeField] private GameObject? compareLowStaminaIcon;

        // Authored (EN) compare-bio font size + box bottom edge, captured before any
        // Japanese auto-size shrink. English is fixed-size + Overflow so it ignores the box.
        private float _compareBioBaseFontSize;
        private float _compareBioBaseBottomInset;
        private bool _compareBioBaseCaptured;
        private const float BioJapaneseBottomLift = 30f;

        // ── Carousel ───────────────────────────────────────────────────────────
        [Header("Carousel")]
        [SerializeField] private CarouselController? carousel;

        // ── Level Up Modal ─────────────────────────────────────────────────────
        [Header("Level Up Modal")]
        [SerializeField] private LevelUpModalController? levelUpModal;

        // ── Animation ─────────────────────────────────────────────────────────
        [Header("Animation")]
        [SerializeField] private float slideDuration = 0.3f;
        [SerializeField] private float fadeDuration  = 0.2f;

        // ── Runtime State ──────────────────────────────────────────────────────
        private bool    _isCompareMode     = false;
        private string  _leftCharacterId   = "";
        private string? _rightCharacterId  = null;

        private Vector2 _normalRightPos;   // cached on first EnterCompareMode
        private Vector2 _compareLeftPos;   // computed from layout at enter time

        private const float LOW_STAMINA_THRESHOLD = 0.25f;

        private static readonly Color DiffPositiveColor = new(0.2f, 0.8f, 0.18f, 1f); // green
        private static readonly Color DiffNegativeColor = new(0.9f, 0.2f, 0.2f,  1f); // red

        // ── Public ─────────────────────────────────────────────────────────────
        public bool IsCompareMode => _isCompareMode;

        // ── Lifecycle ──────────────────────────────────────────────────────────

        private void Awake()
        {
            // Start compare-only elements hidden
            SafeSetActive(compareRightPanel,  false);
            SafeSetActive(verticalDivider,    false);
            SafeSetActive(closeCompareButton?.gameObject, false);
            SafeSetActive(swapButton?.gameObject,         false);
        }

        private void Start()
        {
            closeCompareButton?.onClick.AddListener(ExitCompareMode);
            swapButton?.onClick.AddListener(OnSwapClicked);
            compareRightCompareButton?.onClick.AddListener(OnRightCompareClicked);
            compareRightSelectButton?.onClick.AddListener(OnRightSelectClicked);
            compareLevelUpButton?.onClick.AddListener(OnRightLevelUpClicked);
            // Order 517: Boost button now opens StaminaShopSelection
            compareBoostButton?.onClick.AddListener(() =>
            {
                Debug.Log("[CompareController] Boost clicked — opening StaminaShopSelection");
                GolfinRedux.UI.Shop.StaminaShopSession.SelectedCharacterId = _rightCharacterId ?? string.Empty;
                GolfinRedux.UI.ScreenManager.Instance?.ShowScreen(GolfinRedux.UI.ScreenId.StaminaShopSelection);
            });
        }

        private void OnEnable()
        {
            if (carousel != null)
                carousel.OnCharacterSelected += OnCarouselSelection;

            LocalizationManager.OnLanguageChanged += RefreshLocalizedText;
        }

        private void OnDisable()
        {
            if (carousel != null)
                carousel.OnCharacterSelected -= OnCarouselSelection;

            LocalizationManager.OnLanguageChanged -= RefreshLocalizedText;

            // nav_back_memory D3 / F6 — compare is a transient action, not a place, so leaving
            // the screen exits it. ForceExitImmediate StopAllCoroutines() first, which also kills
            // the enter/exit slide+fade that would otherwise be frozen mid-way by the SetActive.
            if (_isCompareMode) ForceExitImmediate();
        }

        private void RefreshLocalizedText()
        {
            if (comparePlaceholderText != null)
                comparePlaceholderText.text = LocalizationManager.Get("COMPARE_EMPTY_PROMPT");

            // Refresh right column if a character is loaded
            if (!string.IsNullOrEmpty(_rightCharacterId))
                RefreshRightColumn(_rightCharacterId);
        }

        // ── Enter / Exit ───────────────────────────────────────────────────────

        /// <summary>Called by CharacterDetailPanel when COMPARE is tapped.</summary>
        public void EnterCompareMode(string characterId)
        {
            if (_isCompareMode) return;

            _isCompareMode     = true;
            _leftCharacterId   = characterId;
            _rightCharacterId  = null;

            HideAllDiffs();

            // Cache normal position (layout is live by now)
            _normalRightPos = rightPanel.anchoredPosition;

            // Compute target: slide RightPanel to the left half of the detail panel
            var detailRect = rightPanel.parent?.GetComponent<RectTransform>();
            float detailW   = detailRect != null ? detailRect.rect.width : 800f;
            _compareLeftPos = new Vector2(-detailW * 0.25f, _normalRightPos.y);

            StopAllCoroutines();

            // Hide portrait immediately — SetActive is more reliable than CanvasGroup fade
            SafeSetActive(leftPanel, false);

            // Info panel slides to the left half
            StartCoroutine(SlidePanel(rightPanel, _compareLeftPos, slideDuration));

            // Right column fades in (delayed until after slide)
            SafeSetActive(comparePlaceholder, true);
            SafeSetActive(compareInfoPanel, false);
            StartCoroutine(FadeIn(compareRightPanel, fadeDuration, slideDuration));

            // Divider
            SafeSetActive(verticalDivider, true);

            // Swap buttons — CompareButton hides, CloseCompareButton shows
            if (compareButton == null)
                Debug.LogWarning("[CompareController] compareButton is null — run GOLFIN > Wire Compare Panel.");
            if (closeCompareButton == null)
                Debug.LogWarning("[CompareController] closeCompareButton is null — run GOLFIN > Wire Compare Panel.");

            SafeSetActive(compareButton?.gameObject,      false);
            SafeSetActive(closeCompareButton?.gameObject, true);
            UpdateLeftColumnButtons();

            Debug.Log($"[CompareController] Entered compare mode for {characterId}");
        }

        /// <summary>Called by CLOSE COMPARE button or same-character tap in carousel.</summary>
        public void ExitCompareMode()
        {
            if (!_isCompareMode) return;
            Debug.Log("[CompareController] Exited compare mode");
            CleanupAndExit();
        }

        // ── Carousel Interception ──────────────────────────────────────────────

        private void OnCarouselSelection(string characterId)
        {
            if (!_isCompareMode) return;

            // Same character → exit
            if (characterId == _leftCharacterId)
            {
                ExitCompareMode();
                return;
            }

            // Different character → populate right column
            _rightCharacterId = characterId;

            StopAllCoroutines();

            // Ensure compare panel is fully visible — StopAllCoroutines may have
            // interrupted the FadeIn(compareRightPanel) started in EnterCompareMode.
            SafeSetActive(compareRightPanel, true);
            SetAlpha(compareRightPanel, 1f);

            // Switch from placeholder to info immediately (reliable SetActive)
            SafeSetActive(comparePlaceholder, false);
            SafeSetActive(compareInfoPanel,   true);

            RefreshRightColumn(characterId);

            Debug.Log($"[CompareController] Comparing {_leftCharacterId} vs {characterId}");
        }

        // ── Data Binding ───────────────────────────────────────────────────────

        private void RefreshRightColumn(string characterId)
        {
            var playerData = CharacterManager.Instance.GetCharacterData(characterId);
            var csvChar    = CharacterDatabaseCSV.Instance?.GetCharacter(characterId);
            if (playerData == null) return;

            // Name
            if (compareNameText != null)
                compareNameText.text = csvChar != null
                    ? csvChar.GetDisplayName()
                    : characterId.ToUpper();

            // Rarity + Level
            var rarity = csvChar?.rarity ?? CharacterRarity.Common;
            if (compareRarityLabel != null)
            {
                compareRarityLabel.text  = LocalizationManager.Get($"RARITY_{rarity.ToString().ToUpper()}");
                compareRarityLabel.color = RarityHelper.GetRarityColor(rarity);
            }
            if (compareLevelText    != null) compareLevelText.text    = $"Lv {playerData.currentLevel}";
            if (compareMaxLevelText != null) compareMaxLevelText.text = $"/{CharacterManager.Instance.GetMaxLevel(characterId)}";

            // Stat bars — paths mirror the existing detail panel structure
            UpdateCompareStatRow(compareStrengthRow,
                playerData.currentStrength,
                RarityStatCaps.GetStatCap(rarity, "Strength"));

            UpdateCompareStatRow(compareClubControlRow,
                playerData.currentClubControl,
                RarityStatCaps.GetStatCap(rarity, "ClubControl"));

            UpdateCompareStatRow(compareRecoveryRow,
                playerData.currentRecovery,
                RarityStatCaps.GetStatCap(rarity, "Recovery"));

            UpdateCompareStatRow(compareStaminaRow,
                playerData.currentStamina,
                RarityStatCaps.GetStatCap(rarity, "Stamina"));

            // Stat differences (left vs right)
            var leftData = CharacterManager.Instance.GetCharacterData(_leftCharacterId);
            if (leftData != null)
            {
                ShowStatDifference(strengthDiffLabel,    leftData.currentStrength,    playerData.currentStrength);
                ShowStatDifference(clubControlDiffLabel, leftData.currentClubControl, playerData.currentClubControl);
                ShowStatDifference(recoveryDiffLabel,    leftData.currentRecovery,    playerData.currentRecovery);
                ShowStatDifference(staminaDiffLabel,     leftData.currentStamina,     playerData.currentStamina);
            }
            else
            {
                HideAllDiffs();
            }

            // Bio (localized: CHAR_BIO_<NAME>; falls back to CSV English)
            if (compareBioText != null)
            {
                string bioKey = csvChar?.BioLocalizationKey;
                string localizedBio = null;
                if (!string.IsNullOrEmpty(bioKey))
                {
                    string loc = LocalizationManager.Get(bioKey);
                    if (loc != bioKey) localizedBio = loc; // key present in the localization table
                }
                compareBioText.text = !string.IsNullOrEmpty(localizedBio) ? localizedBio : (csvChar?.bio ?? "");
                ApplyCompareBioLanguageSizing();
            }

            // Status icons
            if (compareSelectedIcon != null)
                compareSelectedIcon.SetActive(playerData.isSelected);

            if (compareLevelUpReadyIcon != null)
            {
                int  maxLevel  = CharacterManager.Instance.GetMaxLevel(characterId);
                int  cost      = CharacterManager.Instance.GetLevelUpCost(characterId);
                bool canLevel  = RewardPointsManager.Instance != null
                              && RewardPointsManager.Instance.CanAfford(cost)
                              && playerData.currentLevel < maxLevel;
                compareLevelUpReadyIcon.SetActive(canLevel);
            }

            if (compareLowStaminaIcon != null)
                compareLowStaminaIcon.SetActive(playerData.IsStaminaLow(LOW_STAMINA_THRESHOLD));

            // Level Up button state
            if (compareLevelUpButton != null)
            {
                int  maxLevel  = CharacterManager.Instance.GetMaxLevel(characterId);
                bool atMax     = playerData.currentLevel >= maxLevel;
                bool canAfford = RewardPointsManager.Instance != null
                              && RewardPointsManager.Instance.CanAfford(
                                     CharacterManager.Instance.GetLevelUpCost(characterId));
                compareLevelUpButton.interactable = !atMax && canAfford;
            }
            if (compareBoostButton != null)
                compareBoostButton.interactable = true; // Order 517: StaminaShopSelection live

            // Disable Compare button on right panel — already in compare mode
            if (compareRightCompareButton != null)
                compareRightCompareButton.interactable = false;

            UpdateRightColumnButtons(playerData.isSelected);
        }

        // Japanese bios are taller than English; the long ones overflow the fixed compare
        // BioText box. Shrink-to-fit ONLY in Japanese (max = authored EN size), leaving
        // English unchanged and short Japanese bios at full size.
        private void ApplyCompareBioLanguageSizing()
        {
            if (compareBioText == null) return;

            if (!_compareBioBaseCaptured && !compareBioText.enableAutoSizing)
            {
                _compareBioBaseFontSize = compareBioText.fontSize;
                _compareBioBaseBottomInset = compareBioText.rectTransform.offsetMin.y;
                _compareBioBaseCaptured = true;
            }
            if (!_compareBioBaseCaptured) return;

            var rt = compareBioText.rectTransform;
            var offMin = rt.offsetMin;
            if (LocalizationManager.CurrentLanguage == Language.Japanese)
            {
                rt.offsetMin = new Vector2(offMin.x, _compareBioBaseBottomInset + BioJapaneseBottomLift);
                compareBioText.enableAutoSizing = true;
                compareBioText.fontSizeMax = _compareBioBaseFontSize;
                compareBioText.fontSizeMin = _compareBioBaseFontSize * 0.72f;
            }
            else
            {
                rt.offsetMin = new Vector2(offMin.x, _compareBioBaseBottomInset);
                compareBioText.enableAutoSizing = false;
                compareBioText.fontSize = _compareBioBaseFontSize;
            }
        }

        /// <summary>
        /// Updates one stat row's bar fill and value text.
        /// Expects children: "Name+Bar/Bar" (Image) and "StatNumber" (TMP).
        /// These match the same paths used in CharacterDetailPanel.
        /// </summary>
        private void UpdateCompareStatRow(GameObject statRow, int current, int cap)
        {
            if (statRow == null) return;

            var bar    = statRow.transform.Find("Name+Bar/Bar")?.GetComponent<Image>();
            var numTMP = statRow.transform.Find("StatNumber")?.GetComponent<TextMeshProUGUI>();

            if (bar != null)
            {
                // Force Filled mode — required for fillAmount to have any visual effect.
                // The clone preserves the original setting but we enforce it here to be safe.
                if (bar.type != Image.Type.Filled)
                {
                    bar.type       = Image.Type.Filled;
                    bar.fillMethod = Image.FillMethod.Horizontal;
                    bar.fillOrigin = 0;
                }
                bar.fillAmount = cap > 0 ? (float)current / cap : 0f;
            }

            if (numTMP != null) numTMP.text = $"{current}/{cap}";
            // Colours are set on the Image sprites in the Editor — not overridden here
        }

        // ── Button State Helpers ───────────────────────────────────────────────

        private void UpdateLeftColumnButtons()
        {
            var playerData = CharacterManager.Instance.GetCharacterData(_leftCharacterId);
            bool isSelected = playerData?.isSelected ?? false;

            // If left character is already selected, hide SWAP; otherwise show it
            SafeSetActive(selectButton?.gameObject, isSelected);
            SafeSetActive(swapButton?.gameObject,   !isSelected);
        }

        private void UpdateRightColumnButtons(bool isSelected)
        {
            if (compareRightSelectButtonText != null)
                compareRightSelectButtonText.text = isSelected
                    ? LocalizationManager.Get("ROSTER_SELECTED")
                    : LocalizationManager.Get("ROSTER_SWAP");

            if (compareRightSelectButton != null)
                compareRightSelectButton.interactable = !isSelected;
        }

        // ── Button Handlers ────────────────────────────────────────────────────

        private void OnSwapClicked()
        {
            // Determine which character to select (the non-selected one)
            string? swapToId = null;
            var leftData  = CharacterManager.Instance.GetCharacterData(_leftCharacterId);
            var rightData = _rightCharacterId != null
                          ? CharacterManager.Instance.GetCharacterData(_rightCharacterId)
                          : null;

            if      (leftData  != null && !leftData.isSelected)  swapToId = _leftCharacterId;
            else if (rightData != null && !rightData.isSelected) swapToId = _rightCharacterId;

            if (swapToId == null) return;
            CommitSwapAndExit(swapToId);
        }

        private void OnRightSelectClicked()
        {
            // "SWAP" button on right column — select right character
            if (string.IsNullOrEmpty(_rightCharacterId)) return;
            CommitSwapAndExit(_rightCharacterId);
        }

        /// <summary>
        /// Selects <paramref name="characterId"/>, exits compare mode, then updates the
        /// detail panel to display the newly selected character (not the old one).
        /// </summary>
        private void CommitSwapAndExit(string characterId)
        {
            CharacterManager.Instance.SelectCharacter(characterId);
            Debug.Log($"[CompareController] Swapped selection to {characterId}");

            CleanupAndExit();

            // After CleanupAndExit _isCompareMode is false, so UpdatePanel runs normally.
            // Push the new character into the detail panel so it reflects the swap.
            GetComponent<CharacterDetailPanel>()?.ShowCharacter(characterId);
        }

        private void OnRightCompareClicked()
        {
            // "COMPARE" on right column → make right character the new left, fresh compare
            if (string.IsNullOrEmpty(_rightCharacterId)) return;
            string newLeft = _rightCharacterId;
            ForceExitImmediate();           // instant reset — no animation overlap
            EnterCompareMode(newLeft);
        }

        private void OnRightLevelUpClicked()
        {
            if (string.IsNullOrEmpty(_rightCharacterId) || levelUpModal == null) return;
            var anchor = compareRightPanel != null ? compareRightPanel.GetComponent<RectTransform>() : null;
            levelUpModal.Open(_rightCharacterId, anchor);
        }

        // ── Cleanup ────────────────────────────────────────────────────────────

        /// <summary>
        /// Animated exit used by ExitCompareMode, Swap, and right-select.
        /// </summary>
        private void CleanupAndExit()
        {
            _isCompareMode    = false;
            _rightCharacterId = null;

            StopAllCoroutines();
            StartCoroutine(FadeOut(compareRightPanel, fadeDuration));
            SafeSetActive(verticalDivider, false);
            StartCoroutine(SlidePanel(rightPanel, _normalRightPos, slideDuration));
            // Restore portrait immediately (matches instant hide on enter)
            SafeSetActive(leftPanel, true);

            SafeSetActive(compareButton?.gameObject,      true);
            SafeSetActive(closeCompareButton?.gameObject, false);
            SafeSetActive(swapButton?.gameObject,         false);
            SafeSetActive(selectButton?.gameObject,       true);
        }

        /// <summary>
        /// Instant reset with no coroutines — used before immediately re-entering
        /// compare mode (avoids coroutine overlap).
        /// </summary>
        private void ForceExitImmediate()
        {
            StopAllCoroutines();
            _isCompareMode    = false;
            _rightCharacterId = null;

            SetAlpha(compareRightPanel, 0f);
            SafeSetActive(compareRightPanel, false);
            SafeSetActive(verticalDivider,   false);

            if (rightPanel != null) rightPanel.anchoredPosition = _normalRightPos;

            SafeSetActive(leftPanel, true);

            SafeSetActive(compareButton?.gameObject,      true);
            SafeSetActive(closeCompareButton?.gameObject, false);
            SafeSetActive(swapButton?.gameObject,         false);
            SafeSetActive(selectButton?.gameObject,       true);
        }

        // ── Stat Diff Helpers ──────────────────────────────────────────────────

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

        private void HideAllDiffs()
        {
            SafeSetActive(strengthDiffLabel?.gameObject,    false);
            SafeSetActive(clubControlDiffLabel?.gameObject, false);
            SafeSetActive(recoveryDiffLabel?.gameObject,    false);
            SafeSetActive(staminaDiffLabel?.gameObject,     false);
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

        // ── Small Utilities ────────────────────────────────────────────────────

        private static CanvasGroup GetOrAddCG(GameObject obj)
        {
            // Do NOT use ?? here — Unity's == null is overloaded; ?? uses C# reference equality
            // and can return a stale/missing component reference, causing MissingComponentException.
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
