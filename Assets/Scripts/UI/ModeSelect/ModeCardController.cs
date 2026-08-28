using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Golfin.UI.Toast;
using Golfin.Economy;
using Golfin.EconomyRuntime;
using Golfin.Roster;
using Golfin.UI.Polish;

namespace GolfinRedux.UI.ModeSelect
{
    /// <summary>
    /// Controls a single mode card's visual state (Collapsed / Expanded / Locked).
    /// Cloned verbatim from HoleCardController; adapted for ModeData.
    ///
    /// FIGMA METRICS §6.2 (binding source — SPEC.md iter-6):
    ///   Active/expanded border: 3px WHITE.
    ///   Collapsed/inactive border: 3px #3E7CA8 (blue).
    ///   Active title: #EEDC9A (gold).
    ///   Collapsed/inactive title: silver gradient #FFFFFF → #D1D5DB(40%) → #818EA1.
    ///   Title: EN/Subhead 45px Figma → 32.14 TMP (Rubik variable font, weight 600).
    ///   Tagline/description: EN/Footnote/Subhead per state, white, Rubik variable weight 600.
    ///   ENTRY FEE / REWARDS: EN/Footnote 39px → 27.86 TMP, white.
    ///   Fee/reward cluster: CENTERED horizontal group [LABEL gap32 coin42 gap6 value].
    ///   Rows stacked gap-24, centered in card.
    ///   Active card: 3 separators (under title, under description, above PLAY).
    ///   Collapsed card: 1 separator (under title only).
    ///   PLAY: 359×120, centered, wrapper 144h (24px bottom pad).
    ///   Description inset: ~80px each side.
    ///   Full-screen: no expand chevron on list cards.
    /// </summary>
    public class ModeCardController : MonoBehaviour
    {
        // ── Layout containers ─────────────────────────────────────────────────
        [Header("Layout")]
        [SerializeField] public RectTransform rootRect;
        [SerializeField] private GameObject collapsedContainer;
        [SerializeField] private GameObject expandedContainer;

        // ── Card border ───────────────────────────────────────────────────────
        // NOTE: iter-8 — replaced Outline (which doesn't respect corner radius) with a
        // full-bleed sliced Image (FillCenter=false) that draws only the border ring.
        // cardBorderOutline kept as optional fallback; borderImage is the primary path.
        [Header("Card Border")]
        [SerializeField] private Outline cardBorderOutline;   // legacy fallback (may be null)
        [SerializeField] private Image   borderImage;         // iter-8: sliced border ring
        // iter-11: §6.2 border via SPRITE SWAP on the card background. The panel sprite has the
        // border baked in (white = active, #3E7CA8 = collapsed/inactive). Swapped in SetState.
        [SerializeField] private Image   cardBackground;          // root panel Image (sliced)
        [SerializeField] private Sprite  panelActiveSprite;      // white-border panel (active/expanded)
        [SerializeField] private Sprite  panelCollapsedSprite;   // #3E7CA8-border panel (collapsed/locked)
        // ── Colours (§6.2 — editable per prefab in the Inspector) ─────────────
        // Were hardcoded constants; now serialized so border/title/fee colours can be
        // retuned in Unity without a code change. Defaults match the shipped values.
        [Header("Colours (§6.2)")]
        [Tooltip("Border tint on the active/selected card (white).")]
        [SerializeField] private Color borderActiveColor   = new Color(1f, 1f, 1f, 1f);
        [Tooltip("Border tint when inactive/collapsed (#3E7CA8 blue).")]
        [SerializeField] private Color borderInactiveColor = new Color(0.243f, 0.486f, 0.659f, 1f);
        [Tooltip("Title colour on the active/selected card (#EEDC9A gold).")]
        [SerializeField] private Color titleActiveColor    = new Color32(0xEE, 0xDC, 0x9A, 255);
        [Tooltip("Title colour when inactive/locked (#D1D5DB silver).")]
        [SerializeField] private Color titleCollapsedColor = new Color32(0xD1, 0xD5, 0xDB, 255);
        [Tooltip("Entry-fee text colour when the player can't afford the fee (#C04000).")]
        [SerializeField] private Color insufficientRpColor = new Color32(0xC0, 0x40, 0x00, 255);
        [Tooltip("Label→value gap on a REWARDS row that shows localized TEXT instead of a coin " +
                 "amount. The §6.2 authored gap (32) is sized for [LABEL gap32 coin42 gap6 value]; " +
                 "with no coin it strands 32px between two words and reads as a double space. " +
                 "Coin rows keep the authored gap.")]
        [SerializeField] private float textRewardsGap = 12f;

        // ── Title TMP elements ────────────────────────────────────────────────
        // NOTE: Figma 45px ÷ 1.4 = 32.14 TMP, Rubik variable font weight 600
        [Header("Text - Title")]
        [SerializeField] private TextMeshProUGUI titleText;           // collapsed title
        [SerializeField] private TextMeshProUGUI titleTextExpanded;   // expanded title (if separate)

        // Title colours are the serialized titleActiveColor / titleCollapsedColor fields above.

        // ── Tagline / Description — single auto-sizing TMP element ─────────────
        // NOTE: Collapsed shows tagline (EN/Footnote 39px → 27.86); Expanded swaps to description.
        // For home expanded: EN/Footnote 39px → 27.86. For full-screen expanded: EN/Subhead 45px → 32.14.
        [SerializeField] private TextMeshProUGUI explanationText;
        [SerializeField] private TextMeshProUGUI subtitleTextExpanded;
        [SerializeField] private TextMeshProUGUI descriptionTextExpanded;

        // ── Economy rows (collapsed container) ───────────────────────────────
        [Header("Economy - Collapsed")]
        [SerializeField] private GameObject rewardSlot1;            // ENTRY FEE row
        [SerializeField] private GameObject rewardSlot2;            // REWARDS row
        [SerializeField] private TextMeshProUGUI entryFeeLabel;     // "ENTRY FEE" label
        [SerializeField] private TextMeshProUGUI entryFeeAmount;    // "x100" / "NO ENTRY FEE"
        [SerializeField] private TextMeshProUGUI rewardsLabel;      // "REWARDS"
        [SerializeField] private TextMeshProUGUI rewardsAmount;     // "x200"
        [SerializeField] private Image coinIcon;                    // entry-fee coin (Reward1Icon)
        [SerializeField] private Image rewardsCoin;                 // rewards coin (Reward2Icon)

        // ── Economy rows (expanded container) ────────────────────────────────
        [Header("Economy - Expanded")]
        [SerializeField] private GameObject rewardSlot1Exp;
        [SerializeField] private GameObject rewardSlot2Exp;
        [SerializeField] private TextMeshProUGUI entryFeeLabelExp;
        [SerializeField] private TextMeshProUGUI entryFeeAmountExp;
        [SerializeField] private TextMeshProUGUI rewardsLabelExp;
        [SerializeField] private TextMeshProUGUI rewardsAmountExp;
        // Expanded-container coin icons — the counterparts of coinIcon / rewardsCoin above.
        // They were never referenced, so the expanded rows always drew a coin: a fee-free mode
        // read "(coin) NO ENTRY FEE", and a text-rewards mode would read "(coin) Varies by
        // tournament". Toggled by the same hasFee / hasRewards rules as the collapsed pair.
        [SerializeField] private Image coinIconExp;                 // Reward1IconExp
        [SerializeField] private Image rewardsCoinExp;              // Reward2IconExp

        // ── Separators ────────────────────────────────────────────────────────
        // Active/expanded card has THREE separators; collapsed has ONE (under title).
        [Header("Separators")]
        [SerializeField] private GameObject separator1UnderTitle;     // always shown
        [SerializeField] private GameObject separator2UnderDesc;      // expanded only
        [SerializeField] private GameObject separator3AbovePlay;      // expanded only

        // ── PLAY button wrapper ────────────────────────────────────────────────
        // Wrapper height 144 (button 120 + 24px bottom pad). Visible only on expanded non-locked.
        [Header("PLAY Button")]
        [SerializeField] private Button playButton;
        [SerializeField] private TextMeshProUGUI playButtonLabel;
        [SerializeField] private RectTransform playButtonWrapper;    // height 144 container

        // ── Tap + Locked overlay ──────────────────────────────────────────────
        [Header("Interaction")]
        [SerializeField] private Button cardTapButton;
        [SerializeField] private GameObject lockedOverlay;
        // Tapping the subtitle/tagline row toggles expand/collapse on the home centered card.
        [SerializeField] private Button taglineButton;

        // ── Chevron arrow (expand/collapse indicator — HOME only) ─────────────
        // Per §6.3 item 16: hidden on full-screen list cards. ModeCarouselController
        // sets _showChevron=true for home; ModeSelectScreenController leaves it false.
        [Header("Chevron Arrow")]
        [SerializeField] private GameObject chevronCollapsed;
        [SerializeField] private GameObject chevronExpanded;
        [SerializeField] private bool _showChevron = false;  // set by parent controller

        // ── Lock icon ─────────────────────────────────────────────────────────
        [Header("Lock Icon")]
        [SerializeField] private GameObject lockIconCollapsed;
        [SerializeField] private GameObject lockIconExpanded;

        // ── Expand/Collapse animation ─────────────────────────────────────────
        [Header("Animation")]
        [SerializeField] private float _expandDuration   = 0.18f;
        [SerializeField] private float _collapseDuration = 0.15f;
        // NOTE: These are overridden by parent carousel (ModeCarouselController sets them via
        // SetHeights() before calling SetState). For home carousel: collapsed=484, expanded=822.
        // For full-screen: collapsed=auto (content-hug), expanded=auto (content-hug).
        [SerializeField] private float _collapsedHeight  = 484f;
        [SerializeField] private float _expandedHeight   = 822f;

        // Insufficient-RP color — matches LevelUpModalController.spDepletedColor (#C04000)
        // Insufficient-RP colour is the serialized insufficientRpColor field above.
        private static readonly Color32 NormalWhite = new Color32(255, 255, 255, 255);

        // Authored §6.2 REWARDS-row gaps, captured on first bind so the coin variant can be
        // restored after a text-variant card has tightened them. -1 = not captured yet.
        private float _authoredRewardsGap    = -1f;
        private float _authoredRewardsGapExp = -1f;

        // ── Public state ──────────────────────────────────────────────────────
        public string ModeId { get; private set; }
        public ModeCardState State { get; private set; }
        private ModeData _data;

        // True when this card is the centered card of the home carousel. Drives PLAY + chevron
        // visibility (position-based failsafe). Set by ModeCarouselController via SetCenter().
        private bool _isCenter;
        // Locked is owned by the bound data, not the transient visual state.
        public bool IsLocked => _data != null && _data.locked;

        // Full-screen list expand/collapse height animation handle + a first-show guard so the
        // initial Bind sizes instantly (only genuine user expand/collapse animates).
        private Coroutine _heightAnim;
        private bool _stateInitialized;

        // When true the whole-card tap stays interactable even on a LOCKED card. The home carousel
        // sets this so tapping a locked side card still slides it into the centre — tap and swipe
        // reach the same place. The full-screen list leaves it false so locked rows stay inert.
        private bool _tapWhenLocked;

        // The card ROOT carries its own Button. It is the nearest IPointerClickHandler ancestor for
        // every child graphic that isn't itself a button (the locked overlay, the economy rows, the
        // background), so a tap landing there is swallowed unless we listen to it too. Hooking it
        // makes the ENTIRE card surface a tap target, which is what "tapping selects it" requires.
        private Button _rootTapButton;

        public event System.Action<ModeCardController> OnCardTapped;
        public event System.Action<ModeCardController> OnPlayClicked;
        public event System.Action<ModeCardController> OnTaglineTapped;

        /// <summary>
        /// Called by parent controller to enable or disable the expand chevron.
        /// Home carousel: true. Full-screen list: false (§6.3 item 16).
        /// </summary>
        public void SetShowChevron(bool show) { _showChevron = show; }

        /// <summary>
        /// Keep the whole-card tap live on locked cards (home carousel), so a tap can still centre
        /// them exactly as a swipe does. Leave false on the full-screen list.
        /// </summary>
        public void SetTapWhenLocked(bool enabled)
        {
            _tapWhenLocked = enabled;
            bool tappable = _tapWhenLocked || !IsLocked;
            if (cardTapButton  != null) cardTapButton.interactable  = tappable;
            if (_rootTapButton != null) _rootTapButton.interactable = tappable;
        }

        /// <summary>
        /// Set by the home carousel: true for the centered card, false for side/peek cards.
        /// Drives PLAY + chevron visibility (position-based failsafe, NOT state-based) so paging
        /// across locked cards can never leave the center without PLAY or a side card keeping one.
        /// </summary>
        public void SetCenter(bool isCenter)
        {
            _isCenter = isCenter;
            RefreshCenterVisuals();
        }

        /// <summary>
        /// PLAY + chevron visibility, derived purely from carousel POSITION (center vs side) and
        /// lock state — never from the collapsed/expanded state. Home carousel (_showChevron=true):
        /// PLAY + chevron on the centered card only. Full-screen list (_showChevron=false): no
        /// center concept, so PLAY follows the expanded card and there is no chevron.
        /// </summary>
        private void RefreshCenterVisuals()
        {
            bool isLocked   = State == ModeCardState.Locked;
            bool isExpanded = State == ModeCardState.Expanded;

            bool playVisible = !isLocked && (_showChevron ? _isCenter : isExpanded);
            if (playButton != null)
                playButton.gameObject.SetActive(playVisible);
            // playButtonWrapper may be wired to rootRect on ModeHomeCard — never deactivate the
            // whole card; only toggle a dedicated wrapper.
            if (playButtonWrapper != null && playButtonWrapper != rootRect)
                playButtonWrapper.gameObject.SetActive(playVisible);

            bool showChevrons = _showChevron && !isLocked && _isCenter;
            if (chevronCollapsed != null) chevronCollapsed.SetActive(showChevrons && !isExpanded);
            if (chevronExpanded  != null) chevronExpanded.SetActive(showChevrons && isExpanded);

            // ── Border: WHITE on the SELECTED card, blue (#3E7CA8) on inactive (§6.2) ──
            // Home carousel: the SELECTED (centered) card is white in EVERY state — collapsed+PLAY,
            // expanded, or locked — matching the gold title; side cards stay blue. (Cesar 2026-06-05:
            // the collapsed+PLAY centre was showing blue while its title was gold.)
            // Full-screen list: no center concept → active = the expanded, non-locked card.
            bool whiteBorder = _showChevron
                ? _isCenter
                : (isExpanded && !isLocked);
            Color borderColor = whiteBorder ? borderActiveColor : borderInactiveColor;
            if (borderImage != null)        borderImage.color        = borderColor;
            if (cardBorderOutline != null)  cardBorderOutline.effectColor = borderColor;
            if (cardBackground != null && panelActiveSprite != null && panelCollapsedSprite != null)
                cardBackground.sprite = whiteBorder ? panelActiveSprite : panelCollapsedSprite;

            // ── Title color: gold (#EEDC9A) on the SELECTED card, silver on inactive (§6.2) ──
            // Home: a centered non-locked card is gold in BOTH collapsed+PLAY and expanded states
            // (matching the selected look); side & locked cards stay silver. Full-screen: gold = the
            // expanded non-locked card. Locked cards never go gold.
            bool goldTitle = !isLocked && (_showChevron ? _isCenter : isExpanded);
            Color titleColor = goldTitle ? titleActiveColor : titleCollapsedColor;
            if (titleText != null)         titleText.color         = titleColor;
            if (titleTextExpanded != null) titleTextExpanded.color = titleColor;
        }

        /// <summary>
        /// Called by parent carousel to override the collapsed/expanded heights.
        /// ModeCarouselController sets (484, 822) for the center home card;
        /// full-screen cards use content-hug via ContentSizeFitter.
        /// </summary>
        public void SetHeights(float collapsed, float expanded)
        {
            _collapsedHeight = collapsed;
            _expandedHeight  = expanded;
        }

        private void Awake()
        {
            if (cardTapButton != null)
            {
                cardTapButton.transform.SetAsFirstSibling();
                cardTapButton.onClick.AddListener(() => OnCardTapped?.Invoke(this));
            }
            // Catch-all: clicks on any non-button child bubble to the root Button, not to
            // cardTapButton. UGUI dispatches pointerClick to ONE handler, so this never double-fires.
            _rootTapButton = GetComponent<Button>();
            if (_rootTapButton != null && _rootTapButton != cardTapButton)
                _rootTapButton.onClick.AddListener(() => OnCardTapped?.Invoke(this));
            if (playButton != null)
                playButton.onClick.AddListener(HandlePlayButtonClicked);
            if (taglineButton != null)
                taglineButton.onClick.AddListener(() => OnTaglineTapped?.Invoke(this));
        }

        private void OnEnable()
        {
            if (RewardPointsManager.Instance != null)
                RewardPointsManager.Instance.OnPointsChanged += OnPointsChanged;

            // Every string on this card is resolved imperatively at Bind() time (title, tagline,
            // description, NO ENTRY FEE, the tournaments rewards text), so unlike a LocalizedText
            // label it does NOT repaint on its own when the language changes. The language toggle
            // lives in the Settings OVERLAY, which leaves the screen underneath enabled — so the
            // card never re-enables and the old-language text survived until the screen was
            // re-entered. Re-apply the text in place instead.
            LocalizationManager.OnLanguageChanged += RefreshLocalizedText;
        }

        private void OnDisable()
        {
            if (RewardPointsManager.Instance != null)
                RewardPointsManager.Instance.OnPointsChanged -= OnPointsChanged;

            LocalizationManager.OnLanguageChanged -= RefreshLocalizedText;
        }

        private void OnPointsChanged(int _) => RefreshFeeColor();

        /// <summary>
        /// Re-resolve every localized string on the card against the current language, without
        /// touching layout state. Deliberately NOT SetState(State): that re-runs the expand /
        /// collapse height animation and would visibly jump the card on a language switch.
        /// </summary>
        private void RefreshLocalizedText()
        {
            if (_data == null) return;   // never bound yet — Bind() will localize on its own

            SetTitleText(_data.title);

            bool isExpanded = State == ModeCardState.Expanded;
            if (explanationText != null)         explanationText.text         = isExpanded ? LocDescription() : LocTagline();
            if (descriptionTextExpanded != null) descriptionTextExpanded.text = LocDescription();
            if (subtitleTextExpanded != null)    subtitleTextExpanded.text    = LocTagline();

            // Carries the localized "NO ENTRY FEE" and the tournaments rewards text.
            UpdateEconomyRows(_data);
        }

        /// <summary>Bind a mode's data and initial state.</summary>
        public void Bind(ModeData mode, ModeCardState state)
        {
            if (mode == null) return;
            _data = mode;
            ModeId = mode.id;

            // Set text content (localized by convention, CSV string as fallback)
            SetTitleText(mode.title);
            if (subtitleTextExpanded != null) subtitleTextExpanded.text = LocTagline();
            if (explanationText != null)      explanationText.text      = LocTagline();
            if (descriptionTextExpanded != null) descriptionTextExpanded.text = LocDescription();

            UpdateEconomyRows(mode);
            SetState(state);
        }

        private void SetTitleText(string title)
        {
            // Localize known mode titles (batch 3 — localize_hole_results).
            // Key convention: "MODE_" + title uppercased with spaces→underscores.
            // Get() returns the key itself when not found; fall back to raw title in that case.
            string key = "MODE_" + title.ToUpper().Replace(" ", "_");
            string display = LocalizationManager.Get(key);
            if (string.Equals(display, key, System.StringComparison.Ordinal)) display = title;

            if (titleText != null)         titleText.text         = display;
            if (titleTextExpanded != null) titleTextExpanded.text = display;
        }

        // Localize tagline/description by convention (id-based): MODE_<ID>_TAGLINE / MODE_<ID>_DESC.
        // Get() returns the key itself when not found; fall back to the raw CSV string in that
        // case, so existing modes without keys are pixel-identical to today.
        private static string Localize(string key, string fallback)
        {
            if (string.IsNullOrEmpty(key)) return fallback;
            string s = LocalizationManager.Get(key);
            return string.Equals(s, key, System.StringComparison.Ordinal) ? fallback : s;
        }

        private string LocTagline() => _data == null ? "" :
            Localize($"MODE_{_data.id.ToUpperInvariant()}_TAGLINE", _data.tagline);

        private string LocDescription() => _data == null ? "" :
            Localize($"MODE_{_data.id.ToUpperInvariant()}_DESC", _data.description);

        /// <summary>Switch visual state. Animates height. Updates all visuals.</summary>
        public void SetState(ModeCardState state)
        {
            State = state;

            bool isLocked   = state == ModeCardState.Locked;
            bool isExpanded = state == ModeCardState.Expanded;

            // ── Update explanation text (single auto-sizing element) ──────────
            if (explanationText != null && _data != null)
                explanationText.text = isExpanded ? LocDescription() : LocTagline();
            if (descriptionTextExpanded != null && _data != null)
                descriptionTextExpanded.text = LocDescription();
            if (subtitleTextExpanded != null && _data != null)
                subtitleTextExpanded.text = LocTagline();

            // Home card uses dedicated Subtitle (always visible) + Description (expanded only)
            // elements rather than the single toggling explanationText. The subtitle stays on;
            // the description shows only on the expanded card. (Full-screen card is unaffected:
            // its descriptionTextExpanded lives inside ExpandedContainer which is already gated.)
            if (descriptionTextExpanded != null)
                descriptionTextExpanded.gameObject.SetActive(isExpanded && !isLocked);

            // ── Container visibility ──────────────────────────────────────────
            if (collapsedContainer != null) collapsedContainer.SetActive(!isExpanded);
            if (expandedContainer  != null) expandedContainer.SetActive(isExpanded);

            // ── Separator visibility: 1 when collapsed, 3 when expanded ───────
            if (separator1UnderTitle != null) separator1UnderTitle.SetActive(true);  // always
            if (separator2UnderDesc  != null) separator2UnderDesc.SetActive(isExpanded && !isLocked);
            if (separator3AbovePlay  != null) separator3AbovePlay.SetActive(isExpanded && !isLocked);

            // ── PLAY button + chevron are POSITION-driven (RefreshCenterVisuals, end of method) ──
            // Failsafe: on the home carousel the centered card ALWAYS shows PLAY and side cards
            // NEVER do — independent of the collapsed/expanded state, which used to desync the
            // PLAY button when paging across locked cards (virtual-array instance swap).

            // ── Locked overlay ────────────────────────────────────────────────
            if (lockedOverlay != null) lockedOverlay.SetActive(isLocked);

            // ── Tap interactability ───────────────────────────────────────────
            // _tapWhenLocked keeps the tap alive on locked home-carousel cards so tap == swipe.
            bool tappable = _tapWhenLocked || !isLocked;
            if (cardTapButton  != null) cardTapButton.interactable  = tappable;
            if (_rootTapButton != null) _rootTapButton.interactable = tappable;

            // ── Border (white=active / #3E7CA8=inactive) is POSITION-aware → RefreshCenterVisuals.
            // A locked card that is the SELECTED (centered) home card must show the white border
            // like any other selected card, so the swap needs _isCenter (set after SetState).

            // ── Title color is POSITION-aware (gold on the SELECTED card) → RefreshCenterVisuals.
            // A centered non-locked card must show the gold title in BOTH collapsed+PLAY and
            // expanded states, so the swap needs _isCenter (set after SetState).

            // ── Chevron is position-driven (RefreshCenterVisuals, end of method) ──

            // ── Lock icon ─────────────────────────────────────────────────────
            if (lockIconCollapsed != null) lockIconCollapsed.SetActive(isLocked);
            if (lockIconExpanded  != null) lockIconExpanded.SetActive(false);

            // ── Refresh fee color ─────────────────────────────────────────────
            RefreshFeeColor();

            // ── PLAY + chevron from position (center vs side) — the failsafe ───
            RefreshCenterVisuals();

            // ── Height ────────────────────────────────────────────────────────
            // Home carousel cards (_showChevron) are sized + animated by ModeCarouselController's
            // LerpToTargetLayout — nothing to do here. Full-screen list cards animate their OWN
            // expand/collapse: a ContentSizeFitter + a parent VerticalLayoutGroup(childControlHeight)
            // own the height, so we smoothly drive LayoutElement.preferredHeight (priority 1 overrides
            // the content calc, and both the CSF and the parent VLG follow it). The card's RectMask2D
            // clips the content that hasn't unfolded yet. The first SetState (Bind) sizes instantly.
            if (rootRect != null && !_showChevron)
            {
                if (_heightAnim != null) { StopCoroutine(_heightAnim); _heightAnim = null; }
                var csf = rootRect.GetComponent<ContentSizeFitter>();
                bool csfOwnsHeight = csf != null && csf.enabled
                    && csf.verticalFit != ContentSizeFitter.FitMode.Unconstrained;
                if (!_stateInitialized)
                {
                    _stateInitialized = true;   // initial display: let the layout size it instantly
                }
                else if (csfOwnsHeight)
                {
                    _heightAnim = StartCoroutine(AnimateListHeight(isExpanded ? _expandDuration : _collapseDuration));
                }
                else
                {
                    float targetHeight = isExpanded ? _expandedHeight : _collapsedHeight;
                    _heightAnim = StartCoroutine(AnimateHeight(rootRect.sizeDelta.y, targetHeight,
                        isExpanded ? _expandDuration : _collapseDuration));
                }
            }
        }

        // ── Economy helpers ───────────────────────────────────────────────────

        private void UpdateEconomyRows(ModeData mode)
        {
            if (mode == null) return;

            // demo_build_slice §3.4: hide the RP economy (entry fee + rewards) on mode cards when
            // points are disabled in the demo. Play still works off the hidden RP balance. No-op in
            // the full game.
            if (GolfinRedux.Demo.DemoGate.IsDemo && !GolfinRedux.Demo.DemoConfig.Instance.PointsEnabled)
            {
                if (rewardSlot1 != null)    rewardSlot1.SetActive(false);
                if (rewardSlot2 != null)    rewardSlot2.SetActive(false);
                if (rewardSlot1Exp != null) rewardSlot1Exp.SetActive(false);
                if (rewardSlot2Exp != null) rewardSlot2Exp.SetActive(false);
                return;
            }

            // A mode may express its REWARDS as localized TEXT (rewardsTextKey) instead of a coin
            // amount — tournaments pays out per-tournament prizes, so it shows "Varies by
            // tournament" with no coin. Every other mode keeps the legacy "x{rewards}" path.
            bool hasFee     = mode.entryFee > 0;
            bool hasTextRwd = !string.IsNullOrEmpty(mode.rewardsTextKey);
            bool hasRewards = hasTextRwd || mode.rewards > 0;
            string feeText  = hasFee ? $"x{mode.entryFee}" : Localize("MODE_NO_ENTRY_FEE", "NO ENTRY FEE");
            string rwdText  = hasTextRwd ? LocalizationManager.Get(mode.rewardsTextKey) : $"x{mode.rewards}";

            // ── Collapsed container ───────────────────────────────────────────
            if (rewardSlot1 != null) rewardSlot1.SetActive(true);
            if (entryFeeLabel  != null) entryFeeLabel.gameObject.SetActive(hasFee);
            if (entryFeeAmount != null) { entryFeeAmount.text = feeText; entryFeeAmount.color = NormalWhite; }
            if (coinIcon != null) coinIcon.gameObject.SetActive(hasFee);

            if (rewardSlot2 != null) rewardSlot2.SetActive(hasRewards);
            if (rewardsLabel  != null) rewardsLabel.gameObject.SetActive(hasRewards);
            if (rewardsAmount != null) { rewardsAmount.text = rwdText; rewardsAmount.color = NormalWhite; }
            // The text variant carries no amount, so it shows no coin icon.
            if (rewardsCoin != null) rewardsCoin.gameObject.SetActive(hasRewards && !hasTextRwd);

            // ── Expanded container ────────────────────────────────────────────
            if (rewardSlot1Exp     != null) rewardSlot1Exp.SetActive(true);
            if (entryFeeLabelExp   != null) entryFeeLabelExp.gameObject.SetActive(hasFee);
            if (entryFeeAmountExp  != null) { entryFeeAmountExp.text = feeText; entryFeeAmountExp.color = NormalWhite; }
            if (coinIconExp != null) coinIconExp.gameObject.SetActive(hasFee);

            if (rewardSlot2Exp   != null) rewardSlot2Exp.SetActive(hasRewards);
            if (rewardsLabelExp  != null) rewardsLabelExp.gameObject.SetActive(hasRewards);
            if (rewardsAmountExp != null) { rewardsAmountExp.text = rwdText; rewardsAmountExp.color = NormalWhite; }
            if (rewardsCoinExp != null) rewardsCoinExp.gameObject.SetActive(hasRewards && !hasTextRwd);

            // Tighten the label→value gap when the value is a word rather than a coin amount.
            ApplyRewardsGap(rewardSlot2,    ref _authoredRewardsGap,    hasTextRwd);
            ApplyRewardsGap(rewardSlot2Exp, ref _authoredRewardsGapExp, hasTextRwd);

            RefreshFeeColor();
        }

        /// <summary>
        /// The REWARDS row is authored as [LABEL gap32 coin42 gap6 value]. When the value is
        /// localized TEXT the coin is hidden, leaving the authored 32px stranded between two
        /// words — it reads as a double space. Swap in the tighter textRewardsGap for that
        /// case only, caching the authored value so coin rows are untouched.
        /// </summary>
        private void ApplyRewardsGap(GameObject slot, ref float authored, bool textVariant)
        {
            if (slot == null) return;
            var row = slot.GetComponent<HorizontalLayoutGroup>();
            if (row == null) return;
            if (authored < 0f) authored = row.spacing;
            float target = textVariant ? textRewardsGap : authored;
            if (!Mathf.Approximately(row.spacing, target))
            {
                row.spacing = target;
                LayoutRebuilder.MarkLayoutForRebuild(slot.transform as RectTransform);
            }
        }

        private void RefreshFeeColor()
        {
            if (_data == null) return;

            bool insufficient = _data.entryFee > 0
                && RewardPointsManager.Instance != null
                && !RewardPointsManager.Instance.CanAfford(_data.entryFee);

            Color feeColor = insufficient ? insufficientRpColor : (Color)NormalWhite;

            if (entryFeeAmount    != null) entryFeeAmount.color    = feeColor;
            if (entryFeeAmountExp != null) entryFeeAmountExp.color = feeColor;

            if (playButton != null)
            {
                var cg = playButton.GetComponent<CanvasGroup>();
                if (cg == null) cg = playButton.gameObject.AddComponent<CanvasGroup>();
                cg.alpha = insufficient ? 0.4f : 1f;
            }
        }

        private void HandlePlayButtonClicked()
        {
            if (_data == null) return;

            bool unaffordable = _data.entryFee > 0
                && (RewardPointsManager.Instance == null
                    || !RewardPointsManager.Instance.CanAfford(_data.entryFee));

            if (unaffordable)
            {
                if (ToastController.Instance != null)
                    ToastController.Instance.Show("Not enough Reward Points");
                return;
            }

            // Slice 2: the entry fee is debited server-side BEFORE the mode is entered, so a refused
            // or unreachable debit cannot drop the player into a round they never paid for.
            // Flag OFF (or a free mode) → this runs inline and synchronously, exactly as before.
            //
            // The reason carries the MODE ID since game_modes_admin §4, and that is what lets the
            // server price the entry instead of taking the client's word for it. Everything below
            // the gate is unchanged; what is new is the onDenied arm.
            //
            // Show the round-trip on the card that was tapped (transaction_feedback §3.1). PLAY is
            // the one spend the player is most likely to read as "nothing happened" and tap again,
            // because the answer is a whole screen transition. Both tap surfaces go with it: the
            // card body and the tagline expand the card, and expanding a card whose entry is being
            // priced reads as if the tap did something else.
            //
            // NOT begun when the gate is already busy — its latch would drop this spend without
            // calling either callback, and nothing would ever restore the button.
            var pending = PointsSpendGate.IsSpendInFlight
                ? null
                : PendingSpend.Begin(playButton, playButtonLabel, cardTapButton, taglineButton);

            PointsSpendGate.Spend(_data.entryFee, SpendReasons.ModeEntryFeeFor(_data.id), () =>
            {
                // Restore before the mode is entered: OnPlayClicked navigates away, and the card is
                // reused when the player comes back.
                pending?.Dispose();

                if (_data.entryFee > 0 && RewardPointsManager.Instance != null)
                    RewardPointsManager.Instance.SpendPoints(_data.entryFee);

                OnPlayClicked?.Invoke(this);
            },
            outcome =>
            {
                // Restore before HandleSpendDenied re-renders the economy rows at the server's fee.
                pending?.Dispose();
                HandleSpendDenied(outcome);
            });
        }

        /// <summary>
        /// The server refused the entry debit. NOTHING was charged in any of these cases.
        ///
        /// The gate has already toasted the copy (kept there so every spend surface says the same
        /// thing); what is left is the CARD's half — making the next tap correct.
        ///
        /// <para>
        /// FeeChanged is the interesting one, and it is deliberately the same shape as the shop's
        /// <c>price_changed</c>: re-render at the server's number, do NOT auto-debit, and let the
        /// player's SECOND tap pay the fee they can now see. Auto-paying would charge a number they
        /// were never shown, which is the exact thing the whole validation exists to prevent — the
        /// refusal would have "protected" them by silently doing what it refused.
        /// </para>
        ///
        /// <para>
        /// unknown_mode / mode_locked mean the mode is gone or shut. There is no number to re-price
        /// to, so the card simply re-renders from whatever the database now holds; the mode is
        /// withdrawn on the next launch, when the overlay is applied (I5).
        /// </para>
        /// </summary>
        private void HandleSpendDenied(SpendOutcome outcome)
        {
            if (outcome == null || _data == null) return;

            switch (outcome.Verdict)
            {
                case SpendVerdict.FeeChanged:
                    // The published fee, straight onto the card. `_data` is the shared ModeData
                    // instance the database handed out, so the carousel card and the full-screen
                    // list card agree without either being told.
                    _data.entryFee = outcome.ServerFee;
                    UpdateEconomyRows(_data);
                    break;

                case SpendVerdict.UnknownMode:
                case SpendVerdict.ModeLocked:
                    UpdateEconomyRows(_data);
                    break;
            }
        }

        // Smoothly expand/collapse a full-screen LIST card. The card is sized by a parent
        // VerticalLayoutGroup(childControlHeight) reading its preferred height, so we animate
        // LayoutElement.preferredHeight (priority 1 → overrides the content's calc; the CSF and the
        // parent VLG both follow it, and the parent reflows the cards below). Content is already set
        // to the target state; the card's RectMask2D clips whatever hasn't unfolded yet. On settle we
        // hand the height back to the content (preferredHeight = -1) so resting cards stay content-hug.
        private IEnumerator AnimateListHeight(float duration)
        {
            var le = rootRect.GetComponent<UnityEngine.UI.LayoutElement>();
            if (le == null) le = rootRect.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();

            // Start = current laid-out height (content not yet rebuilt to the new state).
            float startH = rootRect.rect.height;

            // Measure the target height with content-driven sizing.
            le.preferredHeight = -1f;
            LayoutRebuilder.ForceRebuildLayoutImmediate(rootRect);
            float targetH = rootRect.rect.height;

            if (duration <= 0f || Mathf.Approximately(startH, targetH))
            {
                le.preferredHeight = -1f;
                LayoutRebuilder.ForceRebuildLayoutImmediate(rootRect);
                _heightAnim = null;
                yield break;
            }

            // Snap back to start and apply immediately (no flash to the target frame).
            le.preferredHeight = startH;
            LayoutRebuilder.ForceRebuildLayoutImmediate(rootRect);

            var parent = rootRect.parent as RectTransform;
            float el = 0f;
            while (el < duration)
            {
                el += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(el / duration);
                float e = 1f - (1f - t) * (1f - t) * (1f - t);   // cubic ease-out
                le.preferredHeight = Mathf.Lerp(startH, targetH, e);
                if (parent != null) LayoutRebuilder.MarkLayoutForRebuild(parent);
                yield return null;
            }

            // Settle: hand the height back to the ContentSizeFitter / content.
            le.preferredHeight = -1f;
            if (parent != null) LayoutRebuilder.MarkLayoutForRebuild(parent);
            LayoutRebuilder.ForceRebuildLayoutImmediate(rootRect);
            _heightAnim = null;
        }

        private IEnumerator AnimateHeight(float from, float to, float duration)
        {
            if (duration <= 0f)
            {
                if (rootRect != null)
                    rootRect.sizeDelta = new Vector2(rootRect.sizeDelta.x, to);
                LayoutRebuilder.ForceRebuildLayoutImmediate(rootRect);
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t     = Mathf.Clamp01(elapsed / duration);
                float eased = 1f - (1f - t) * (1f - t);
                if (rootRect != null)
                    rootRect.sizeDelta = new Vector2(rootRect.sizeDelta.x, Mathf.Lerp(from, to, eased));
                yield return null;
            }

            if (rootRect != null)
            {
                rootRect.sizeDelta = new Vector2(rootRect.sizeDelta.x, to);
                LayoutRebuilder.ForceRebuildLayoutImmediate(rootRect);
            }
        }
    }

    // Collapsed = collapsed WITH the PLAY button (home centered card collapsed).
    // CollapsedNoPlay = collapsed WITHOUT PLAY (home side/peek cards).
    public enum ModeCardState { Collapsed, Expanded, Locked, CollapsedNoPlay }
}
