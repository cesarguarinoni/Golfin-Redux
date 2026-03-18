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
        [SerializeField] private GameObject comparePlaceholder = null!; // "TAP…" text
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

        [Header("Status Icons — Right Column")]
        [SerializeField] private GameObject? compareSelectedIcon;
        [SerializeField] private GameObject? compareLowStaminaIcon;

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
            compareBoostButton?.onClick.AddListener(() =>
                Debug.Log("[CompareController] Boost clicked (right column) — not yet implemented"));
        }

        private void OnEnable()
        {
            if (carousel != null)
                carousel.OnCharacterSelected += OnCarouselSelection;
        }

        private void OnDisable()
        {
            if (carousel != null)
                carousel.OnCharacterSelected -= OnCarouselSelection;
        }

        // ── Enter / Exit ───────────────────────────────────────────────────────

        /// <summary>Called by CharacterDetailPanel when COMPARE is tapped.</summary>
        public void EnterCompareMode(string characterId)
        {
            if (_isCompareMode) return;

            _isCompareMode     = true;
            _leftCharacterId   = characterId;
            _rightCharacterId  = null;

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

            // Swap buttons
            SafeSetActive(compareButton?.gameObject, false);
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

            // Placeholder fades out, info panel fades in
            if (comparePlaceholder.activeSelf)
                StartCoroutine(FadeOut(comparePlaceholder, fadeDuration));

            StartCoroutine(FadeIn(compareInfoPanel, fadeDuration, fadeDuration));

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
                compareRarityLabel.text  = rarity.ToString().ToUpper();
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

            // Bio
            if (compareBioText != null)
                compareBioText.text = csvChar?.bio ?? "";

            // Status icons
            if (compareSelectedIcon   != null) compareSelectedIcon.SetActive(playerData.isSelected);
            if (compareLowStaminaIcon != null) compareLowStaminaIcon.SetActive(playerData.IsStaminaLow(LOW_STAMINA_THRESHOLD));

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
                compareBoostButton.interactable = false; // not yet implemented

            UpdateRightColumnButtons(playerData.isSelected);
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

            if (bar    != null) bar.fillAmount = cap > 0 ? (float)current / cap : 0f;
            if (numTMP != null) numTMP.text    = $"{current}/{cap}";
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
                compareRightSelectButtonText.text = isSelected ? "SELECTED" : "SWAP";

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

            CharacterManager.Instance.SelectCharacter(swapToId);
            Debug.Log($"[CompareController] Swapped to {swapToId}");
            CleanupAndExit();
        }

        private void OnRightSelectClicked()
        {
            // "SWAP" button on right column — select right character
            if (string.IsNullOrEmpty(_rightCharacterId)) return;
            CharacterManager.Instance.SelectCharacter(_rightCharacterId);
            Debug.Log($"[CompareController] Right column swap to {_rightCharacterId}");
            CleanupAndExit();
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
            levelUpModal.Open(_rightCharacterId);
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
            => obj.GetComponent<CanvasGroup>() ?? obj.AddComponent<CanvasGroup>();

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
