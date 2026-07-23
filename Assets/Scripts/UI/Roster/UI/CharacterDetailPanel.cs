#nullable enable
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Golfin.Core.Stamina;

namespace Golfin.Roster
{
    /// <summary>
    /// Data-binding controller for the character detail panel.
    /// Populates existing UI hierarchy from CharacterManager data
    /// when a carousel card is tapped.
    ///
    /// Phase 2b: Pure data binding — does NOT modify hierarchy.
    /// Phase 4 (stamina_roster_ux): Ghost overlay bars on Strength + ClubControl
    ///   show stat lost to low Condition. Stamina row becomes a Condition meter.
    /// Phase 5 (stamina_roster_live_meter): Live tick coroutine — read-only
    ///   projection of Condition recovery; smooth lerp of fills + numbers;
    ///   demo time-accelerator (editor-only menu).
    /// </summary>
    public class CharacterDetailPanel : MonoBehaviour
    {
        // ── Demo accelerator (static, code-only, defaults OFF) ────────────────
        // Toggled by GOLFIN > Stamina > Toggle Live-Meter Demo Accel (editor menu).
        // When ON: simElapsed advances faster than real-time so the meter is
        //          visibly climbing on screen. NEVER ships enabled.
        // When OFF: simElapsed = real elapsed since conditionUpdatedUtc only.
        public static bool DemoAccelerate = false;
        public static float DemoHoursPerRealSecond = 0.5f;   // 0.5 h/s ≈ ×1800

        /// <summary>
        /// Called by StaminaLiveMeterDemoMenu when toggling demo-accel OFF.
        /// Spec L4: "Toggling OFF resets demoExtra to zero."
        /// Resets the accumulated virtual-time field on the active panel instance.
        /// </summary>
        public static void ResetDemoAccel()
        {
            // Find the active panel instance and clear its accumulated virtual hours.
            // Using FindObjectOfType is acceptable here — this is editor-only,
            // invoked from a [MenuItem], never at runtime.
            var panel = UnityEngine.Object.FindObjectOfType<CharacterDetailPanel>();
            if (panel != null)
                panel._demoExtraHours = 0f;
        }

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
        [SerializeField] private Image strengthBar;                // CharacterStats1 > Name+Bar > BarContainer > Bar  (effective fill)
        [SerializeField] private Image strengthGhostBar;          // CharacterStats1 > Name+Bar > BarContainer > GhostBar (base fill, behind)
        [SerializeField] private TextMeshProUGUI strengthNumber;   // CharacterStats1 > StatNumber

        [Header("Stat Bars — Club Control")]
        [SerializeField] private TextMeshProUGUI clubControlName;
        [SerializeField] private Image clubControlBar;            // effective fill
        [SerializeField] private Image clubControlGhostBar;       // base fill, behind
        [SerializeField] private TextMeshProUGUI clubControlNumber;

        [Header("Stat Bars — Recovery")]
        [SerializeField] private TextMeshProUGUI recoveryName;
        [SerializeField] private Image recoveryBar;
        [SerializeField] private TextMeshProUGUI recoveryNumber;

        [Header("Stat Bars — Condition Meter (was Stamina)")]
        [SerializeField] private TextMeshProUGUI staminaName;
        [SerializeField] private Image staminaBar;               // Condition meter fill — uses LevelUpWhite for tinting
        [SerializeField] private TextMeshProUGUI staminaNumber;

        [Header("Condition Meter Colors")]
        [SerializeField] private Color meterColorHigh  = new Color(0.34f, 0.57f, 0.90f, 1f);  // #5792E6 (High / blue)
        [SerializeField] private Color meterColorMid   = new Color(0.90f, 0.72f, 0.28f, 1f);  // #E6B847 (Mid / amber)
        [SerializeField] private Color meterColorLow   = new Color(0.82f, 0.42f, 0.28f, 1f);  // #D16A47 (Low / red)

        [Header("Buttons")]
        [SerializeField] private Button levelUpButton;
        [SerializeField] private Button boostButton;
        [SerializeField] private Button compareButton;
        [SerializeField] private Button selectButton;
        [SerializeField] private TextMeshProUGUI selectButtonText;  // SelectButton > Text (TMP)

        [Header("Bio")]
        [SerializeField] private TextMeshProUGUI bioText;           // BioPanel > BioText

        [Header("Status Icons")]
        [SerializeField] private GameObject? selectedIcon;       // IconSelectedBig — wire in Inspector
        [SerializeField] private GameObject? levelUpReadyIcon;   // IconLevelUpBig  — wire in Inspector

        [Header("Modals")]
        [SerializeField] private LevelUpModalController? levelUpModal;
        [SerializeField] private RectTransform? levelUpAnchorPanel; // wire to RightPanel

        [Header("Compare")]
        [SerializeField] private CompareController? compareController;

        [Header("Carousel Reference")]
        [SerializeField] private CarouselController? carousel;

        // Bar colours removed — Image colours are set on the sprites in the Editor

        private string currentCharacterId = "";

        // ── Live-tick lerp state (Phase 5) ───────────────────────────────────
        // Displayed (lerped) values separate from target values so we can
        // animate smoothly toward target without snapping.
        private float _displayedPct = 1f;        // current lerped conditionPct shown
        private float _targetPct    = 1f;         // target from latest projection
        private float _demoExtraHours = 0f;       // accumulates demo-accel virtual time
        private Coroutine? _tickCoroutine = null;

        // Lerp speed: fills should reach target within ~0.3 s.
        // Using MoveTowards with rate 3.5/s gives ~0.3 s (1/3.5 ≈ 0.29 s).
        private const float LerpRate = 3.5f;
        // Tick interval: 20 Hz (~15–30 Hz range per spec)
        private static readonly WaitForSeconds TickInterval = new WaitForSeconds(0.05f);

        // The pure testable projection helper lives in StaminaModel.LiveDisplayEnergy
        // (Golfin.Core.Stamina assembly) so the EditMode test asmdef can reach it directly.
        // CharacterDetailPanel delegates to it in ComputeTargetPct below.

        // ─────────────────────────────────────────────────────────────────────

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

            if (RewardPointsManager.Instance != null)
                RewardPointsManager.Instance.OnPointsChanged += OnPointsChanged;

            LocalizationManager.OnLanguageChanged += RefreshLocalizedText;

            // Restart tick if a character is already selected (re-enable from background)
            if (!string.IsNullOrEmpty(currentCharacterId))
                RestartTick();

            // Re-localize on enable so a language change made while this panel was
            // inactive — or fired before this panel subscribed in OnEnable — is picked
            // up (closes the SELECT-button init race). Safe no-op when no char selected.
            RefreshLocalizedText();
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

            if (RewardPointsManager.Instance != null)
                RewardPointsManager.Instance.OnPointsChanged -= OnPointsChanged;

            LocalizationManager.OnLanguageChanged -= RefreshLocalizedText;

            // Stop live tick and reset demo accel residue
            StopTick();
            _demoExtraHours = 0f;
        }

        private void RefreshLocalizedText()
        {
            if (!string.IsNullOrEmpty(currentCharacterId))
                UpdatePanel(currentCharacterId);
        }

        /// <summary>
        /// Main data binding — populates all UI fields from character data.
        /// Skipped while CompareController is in compare mode (it handles
        /// carousel taps itself in that state).
        /// On each call the displayed fills SNAP to target (character switch snap, L5).
        /// The tick then lerps from the new snapped value on subsequent ticks.
        /// </summary>
        private void UpdatePanel(string characterId)
        {
            // In compare mode the CompareController intercepts carousel taps;
            // we must not overwrite the left column while it is managed by compare.
            if (compareController != null && compareController.IsCompareMode) return;

            bool charChanged = characterId != currentCharacterId;
            currentCharacterId = characterId;

            var playerData = CharacterManager.Instance.GetCharacterData(characterId);
            if (playerData == null) return;

            // Get template data — CSV first; only query ScriptableObject if CSV has nothing
            var csvData = CharacterDatabaseCSV.Instance?.GetCharacter(characterId);
            var soData = csvData == null ? CharacterManager.Instance.GetCharacterTemplate(characterId) : null;

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
                rarityLabel.text = LocalizationManager.Get($"RARITY_{rarity.ToString().ToUpper()}");
                rarityLabel.color = RarityHelper.GetRarityColor(rarity);
            }

            // --- Level ---
            if (currentLevelText != null)
                currentLevelText.text = $"Lv {playerData.currentLevel}";

            int maxLevel = CharacterManager.Instance.GetMaxLevel(characterId);
            if (maxLevelText != null)
                maxLevelText.text = $"/{maxLevel}";

            // --- Condition (for ghost bars + meter) ---
            // Compute the LIVE projected conditionPct using the read-only helper.
            // On character switch: reset demo-accel FIRST so the snap uses raw condition,
            // not accumulated virtual hours from the previous character's demo session.
            if (charChanged)
                _demoExtraHours = 0f;

            bool staminaConfigured = StaminaModel.IsConfigured;
            float conditionPct = ComputeTargetPct(playerData, staminaConfigured);

            // On character switch: SNAP displayed to target (L5 — no cross-character tween).
            // On same-character update (level-up, RP): keep displayed value,
            // let the tick lerp from current displayed toward new target.
            if (charChanged)
                _displayedPct = conditionPct;
            _targetPct = conditionPct;

            // --- Stats ---
            int strCap  = RarityStatCaps.GetStatCap(rarity, "Strength");
            int ccCap   = RarityStatCaps.GetStatCap(rarity, "ClubControl");
            int recCap  = RarityStatCaps.GetStatCap(rarity, "Recovery");
            int stamCap = RarityStatCaps.GetStatCap(rarity, "Stamina");

            // Apply displayed pct (snapped on char-switch, lerped on live tick)
            ApplyLiveStats(playerData, staminaConfigured, _displayedPct, strCap, ccCap, recCap, stamCap);

            // --- Status Icons ---
            if (selectedIcon != null)
                selectedIcon.SetActive(playerData.isSelected);

            if (levelUpReadyIcon != null)
            {
                int cost      = CharacterManager.Instance.GetLevelUpCost(characterId);
                bool canLevel = RewardPointsManager.Instance != null
                             && RewardPointsManager.Instance.CanAfford(cost)
                             && playerData.currentLevel < maxLevel;
                levelUpReadyIcon.SetActive(canLevel);
            }

            // --- Button States ---
            if (levelUpButton != null)
            {
                bool atMax = playerData.currentLevel >= maxLevel;
                bool canAfford = RewardPointsManager.Instance != null
                    && RewardPointsManager.Instance.CanAfford(CharacterManager.Instance.GetLevelUpCost(characterId));
                levelUpButton.interactable = !atMax && canAfford;
            }
            if (boostButton != null)
                boostButton.interactable = true; // Order 517: StaminaShopSelection live

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

            // Ensure tick is running (idempotent restart on same char)
            RestartTick();
        }

        // ── Live projection helpers ───────────────────────────────────────────

        /// <summary>
        /// Computes the read-only target conditionPct for the current tick frame.
        /// Reads currentStaminaEnergy + conditionUpdatedUtc — never writes them.
        /// Delegates projection math to StaminaModel.LiveDisplayEnergy (testable pure helper).
        /// </summary>
        private float ComputeTargetPct(PlayerCharacterData pcd, bool staminaConfigured)
        {
            if (!staminaConfigured) return 1f;

            bool hasTimestamp = pcd.conditionUpdatedUtc != default(DateTime);
            TimeSpan realElapsed = hasTimestamp
                ? DateTime.UtcNow - pcd.conditionUpdatedUtc
                : TimeSpan.Zero;

            // Add demo-accel virtual hours (accumulated in _demoExtraHours)
            TimeSpan simElapsed = realElapsed + TimeSpan.FromHours(_demoExtraHours);

            float projected = StaminaModel.LiveDisplayEnergy(
                pcd.currentStaminaEnergy,
                pcd.maxStaminaEnergy,
                pcd.currentRecovery,
                hasTimestamp,
                simElapsed);

            return StaminaModel.ConditionPct(projected, pcd.currentStamina);
        }

        /// <summary>
        /// Applies the displayed (possibly lerped) conditionPct to all UI elements:
        /// STR/CC effective fills + numbers + ghost fills + Condition meter fill/colour.
        /// This is the single apply primitive used by both UpdatePanel and the tick.
        /// Never touches persistent state.
        /// </summary>
        private void ApplyLiveStats(
            PlayerCharacterData pcd,
            bool staminaConfigured,
            float displayPct,
            int strCap, int ccCap, int recCap, int stamCap)
        {
            // Strength — ghost shows base, solid shows effective
            int strEffective = (staminaConfigured && StaminaModel.IsDegraded("Strength"))
                ? StaminaModel.EffectiveStat(pcd.currentStrength, displayPct)
                : pcd.currentStrength;
            UpdateGhostStatBar(strengthName, strengthBar, strengthGhostBar, strengthNumber,
                LocalizationManager.Get("ROSTER_STRENGTH"),
                pcd.currentStrength, strEffective, strCap);

            // Club Control — ghost shows base, solid shows effective
            int ccEffective = (staminaConfigured && StaminaModel.IsDegraded("ClubControl"))
                ? StaminaModel.EffectiveStat(pcd.currentClubControl, displayPct)
                : pcd.currentClubControl;
            UpdateGhostStatBar(clubControlName, clubControlBar, clubControlGhostBar, clubControlNumber,
                LocalizationManager.Get("ROSTER_CLUB_CONTROL"),
                pcd.currentClubControl, ccEffective, ccCap);

            // Recovery — no ghost (not degraded by Condition)
            UpdateStatBar(recoveryName, recoveryBar, recoveryNumber,
                LocalizationManager.Get("ROSTER_RECOVERY"),
                pcd.currentRecovery, recCap);

            // Stamina row → Condition meter
            // Fill = displayPct (live lerp); number stays base stamina stat "currentStamina/cap"
            UpdateConditionMeter(pcd.currentStamina, stamCap, displayPct);
        }

        // ── Live tick coroutine ───────────────────────────────────────────────

        private void RestartTick()
        {
            StopTick();
            if (!string.IsNullOrEmpty(currentCharacterId))
                _tickCoroutine = StartCoroutine(LiveMeterTick());
        }

        private void StopTick()
        {
            if (_tickCoroutine != null)
            {
                StopCoroutine(_tickCoroutine);
                _tickCoroutine = null;
            }
        }

        private IEnumerator LiveMeterTick()
        {
            while (true)
            {
                yield return TickInterval;

                // Guard: skip if in compare mode (same as UpdatePanel guard)
                if (compareController != null && compareController.IsCompareMode)
                    continue;

                if (string.IsNullOrEmpty(currentCharacterId))
                    continue;

                // Cache the pcd lookup once per tick — no per-tick allocation
                var pcd = CharacterManager.Instance?.GetCharacterData(currentCharacterId);
                if (pcd == null)
                    continue;

                // !IsConfigured → inert (L6)
                if (!StaminaModel.IsConfigured)
                    continue;

                // Accumulate demo-accel virtual time (only when ON)
                if (DemoAccelerate)
                    _demoExtraHours += Time.unscaledDeltaTime * DemoHoursPerRealSecond;

                // Recompute target from read-only projection (never writes pcd)
                _targetPct = ComputeTargetPct(pcd, true);

                // Lerp displayed pct toward target (L3)
                _displayedPct = Mathf.MoveTowards(_displayedPct, _targetPct, LerpRate * Time.unscaledDeltaTime);

                // Get stat caps (cached from rarity — low-cost lookup)
                var csvData = CharacterDatabaseCSV.Instance?.GetCharacter(currentCharacterId);
                CharacterRarity rarity = csvData?.rarity ?? CharacterRarity.Common;
                int strCap  = RarityStatCaps.GetStatCap(rarity, "Strength");
                int ccCap   = RarityStatCaps.GetStatCap(rarity, "ClubControl");
                int recCap  = RarityStatCaps.GetStatCap(rarity, "Recovery");
                int stamCap = RarityStatCaps.GetStatCap(rarity, "Stamina");

                // Apply the lerped displayed pct to all UI elements (fills + numbers + colour)
                ApplyLiveStats(pcd, true, _displayedPct, strCap, ccCap, recCap, stamCap);
            }
        }

        // ── Stat bar apply helpers ────────────────────────────────────────────

        /// <summary>
        /// Updates a stat row that participates in ghost-bar rendering.
        /// The ghost bar shows the BASE (full) stat; the solid bar shows the EFFECTIVE (degraded) stat.
        /// When not degraded (baseValue == effectiveValue) the ghost is hidden (fillAmount=0).
        /// </summary>
        private void UpdateGhostStatBar(
            TextMeshProUGUI nameField, Image effectiveBar, Image? ghostBar,
            TextMeshProUGUI numberField,
            string label, int baseValue, int effectiveValue, int capValue)
        {
            if (nameField != null)
                nameField.text = label;

            // Number shows effective value (D1=A: effective shown in number)
            if (numberField != null)
                numberField.text = $"{effectiveValue}/{capValue}";

            float baseFill      = capValue > 0 ? (float)baseValue / capValue : 0f;
            float effectiveFill = capValue > 0 ? (float)effectiveValue / capValue : 0f;

            bool isDegraded = effectiveValue < baseValue;

            // Effective bar (foreground solid fill)
            if (effectiveBar != null)
                effectiveBar.fillAmount = effectiveFill;

            // Ghost bar (background translucent fill showing base)
            if (ghostBar != null)
            {
                if (isDegraded)
                {
                    ghostBar.fillAmount = baseFill;
                    // Ensure ghost is visually behind effective bar (sibling index 0)
                    ghostBar.gameObject.SetActive(true);
                }
                else
                {
                    ghostBar.fillAmount = 0f;
                    ghostBar.gameObject.SetActive(false);
                }
            }
        }

        /// <summary>
        /// Standard stat bar update (no ghost) for Recovery.
        /// </summary>
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

        /// <summary>
        /// Updates the Condition meter (was Stamina row).
        /// Fill = conditionPct (0..1); number shows base stamina stat "stat/cap"; color by MeterState.
        /// </summary>
        private void UpdateConditionMeter(int staminaStatValue, int staminaStatCap, float conditionPct)
        {
            if (staminaName != null)
                staminaName.text = LocalizationManager.Get("ROSTER_STAMINA");

            // Number = stamina STAT value (not energy) — D1=A: effective value shown
            if (staminaNumber != null)
                staminaNumber.text = $"{staminaStatValue}/{staminaStatCap}";

            if (staminaBar != null)
            {
                staminaBar.fillAmount = conditionPct;
                staminaBar.color = ApplyMeterColor(conditionPct);
            }
        }

        /// <summary>
        /// Returns the meter color for the given conditionPct using MeterState thresholds.
        /// Falls back to meterColorHigh when StaminaModel is not yet configured.
        /// </summary>
        private Color ApplyMeterColor(float conditionPct)
        {
            if (!StaminaModel.IsConfigured)
                return meterColorHigh;

            return StaminaModel.MeterState(conditionPct) switch
            {
                MeterColorState.High => meterColorHigh,
                MeterColorState.Mid  => meterColorMid,
                MeterColorState.Low  => meterColorLow,
                _                    => meterColorHigh,
            };
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

        private void OnPointsChanged(int _)
        {
            // Re-evaluate level-up-ready icon whenever RP balance changes
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
                selectButtonText.text = isSelected
                    ? LocalizationManager.Get("ROSTER_SELECTED")
                    : LocalizationManager.Get("ROSTER_SELECT");

            if (selectButton != null)
                selectButton.interactable = !isSelected;

            // Compare is only meaningful for the active character.
            if (compareButton != null)
                compareButton.interactable = isSelected;

            // Button visual state handled by Color Tint transition — do NOT set Image.color here
        }

        // --- Button Click Handlers ---

        private void OnLevelUpClicked()
        {
            if (levelUpModal == null || string.IsNullOrEmpty(currentCharacterId)) return;

            // In compare mode the detail panel is sharing the screen, so anchor
            // the modal to RightPanel only. Outside compare, use the default
            // full-screen centre (no anchor) — exactly as before.
            var anchor = (compareController != null && compareController.IsCompareMode)
                ? levelUpAnchorPanel
                : null;
            levelUpModal.Open(currentCharacterId, anchor);
        }

        private void OnBoostClicked()
        {
            Debug.Log(string.Format("[CharacterDetailPanel] Boost clicked for {0} — opening StaminaShopSelection", currentCharacterId));
            // Order 517: set session character id, navigate to Shop Selection
            GolfinRedux.UI.Shop.StaminaShopSession.SelectedCharacterId = currentCharacterId;
            GolfinRedux.UI.ScreenManager.Instance?.ShowScreen(GolfinRedux.UI.ScreenId.StaminaShopSelection);
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
