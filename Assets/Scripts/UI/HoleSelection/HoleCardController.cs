using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using GolfinRedux.UI;
using Golfin.Utilities;

namespace GolfinRedux.UI.HoleSelection
{
    public enum HoleCardState { Collapsed, Expanded, Locked }
    public enum HoleCardMode  { Play, Replay }

    /// <summary>
    /// Controls a single hole card's visual state (Collapsed / Expanded / Locked)
    /// and data binding. Lives on the root of the HoleCard prefab.
    /// </summary>
    public class HoleCardController : MonoBehaviour
    {
        // ── Layout containers ─────────────────────────────────────────────────
        [Header("Layout")]
        [SerializeField] public RectTransform rootRect;
        [SerializeField] private GameObject collapsedContainer;
        [SerializeField] private GameObject expandedContainer;

        // ── Title + Subtitle ─────────────────────────────────────────────────
        // Title fields are kept here for backward compat with auto-wire but are no
        // longer written to at runtime. Cesar's prefab text wins.
        [Header("Text — Collapsed")]
        [SerializeField] private TextMeshProUGUI titleTextCollapsed;
        [SerializeField] private TextMeshProUGUI subtitleTextCollapsed;

        [Header("Text — Expanded")]
        [SerializeField] private TextMeshProUGUI titleTextExpanded;
        [SerializeField] private TextMeshProUGUI subtitleTextExpanded;

        // ── Hole image + description ──────────────────────────────────────────
        [Header("Hole Content")]
        [SerializeField] private Image holeImage;
        [SerializeField] private TextMeshProUGUI descriptionText;

        // ── Rewards (collapsed) ───────────────────────────────────────────────
        [Header("Rewards — Collapsed")]
        [SerializeField] private GameObject[] collapsedRewardSlots = new GameObject[3];
        [SerializeField] private Image[] collapsedRewardIcons = new Image[3];
        [SerializeField] private TextMeshProUGUI[] collapsedRewardAmounts = new TextMeshProUGUI[3];

        // ── Rewards (expanded) ────────────────────────────────────────────────
        [Header("Rewards — Expanded")]
        [SerializeField] private GameObject[] expandedRewardSlots = new GameObject[3];
        [SerializeField] private Image[] expandedRewardIcons = new Image[3];
        [SerializeField] private TextMeshProUGUI[] expandedRewardAmounts = new TextMeshProUGUI[3];

        // ── Reward icon sprites ───────────────────────────────────────────────
        [Header("Reward Icons")]
        [SerializeField] private Sprite pointsIcon;
        [SerializeField] private Sprite repairKitIcon;
        [SerializeField] private Sprite ballIcon;

        // ── Action button ─────────────────────────────────────────────────────
        [Header("Action Button")]
        [SerializeField] private Button actionButton;
        [SerializeField] private TextMeshProUGUI actionButtonLabel;
        [SerializeField] private Sprite playButtonSprite;    // Assets/Art/HoleSelectScreen/Button - Play.png
        [SerializeField] private Sprite replayButtonSprite;  // Assets/Art/HoleSelectScreen/Button - Replay.png

        // ── Tap + Locked overlay ──────────────────────────────────────────────
        [Header("Interaction")]
        [SerializeField] private Button cardTapButton;
        [SerializeField] private GameObject lockedOverlay;

        // ── Chevron arrow (Open/Close indicator) ──────────────────────────────
        [Header("Chevron Arrow")]
        [SerializeField] private GameObject chevronCollapsed; // ▼ shown when collapsed/locked
        [SerializeField] private GameObject chevronExpanded;  // ▲ shown when expanded

        // ── Lock icon (in title row, like the YAITA filter pill) ──────────────
        [Header("Lock Icon")]
        [SerializeField] private GameObject lockIconCollapsed; // shown in collapsed title when locked
        [SerializeField] private GameObject lockIconExpanded;  // never shown (locked never expands), but wired for completeness

        // ── Public state ──────────────────────────────────────────────────────
        public int HoleNumber { get; private set; }
        public HoleCardMode Mode { get; private set; }
        public HoleCardState State { get; private set; }

        /// <summary>
        /// Raised when this card is tapped. Parent controller decides whether to expand/collapse
        /// based on locked status (parent enforces single-expanded invariant).
        /// </summary>
        public event System.Action<HoleCardController> OnCardTapped;

        /// <summary>
        /// Raised when the user taps PLAY/REPLAY on the expanded card.
        /// Parent forwards to MatchmakingModalController.Open(holeIndex).
        /// </summary>
        public event System.Action<HoleCardController> OnActionButtonClicked;

        private void Awake()
        {
            if (cardTapButton != null)
            {
                // The prefab saves CardTapButton as the LAST child of HoleCard, which
                // means it renders ON TOP of every sibling — including ExpandedContainer
                // and the ActionButton inside it. Because CardTapButton's Image has
                // raycastTarget=true and fully overlaps the action button, every tap on
                // PLAY/REPLAY is intercepted as a card-tap (which then toggles the
                // expanded state and collapses the card). Force it to the bottom of
                // the sibling stack so children of ExpandedContainer (and LockedOverlay)
                // render — and raycast — above it. Pure code fix; no prefab edit needed.
                cardTapButton.transform.SetAsFirstSibling();
                cardTapButton.onClick.AddListener(() => OnCardTapped?.Invoke(this));
            }

            if (actionButton != null)
                actionButton.onClick.AddListener(() => OnActionButtonClicked?.Invoke(this));
        }

        /// <summary>
        /// Bind a hole's data and initial state. Called once by the parent after instantiation.
        /// </summary>
        public void Bind(HoleData hole, HoleCardMode mode, HoleCardState state)
        {
            if (hole == null) return;

            HoleNumber = hole.holeNumber;
            Mode = mode;

            // Determine reward list based on mode
            List<HoleReward> rewards = (mode == HoleCardMode.Replay) ? hole.replayRewards : hole.rewards;

            // Title text per (mode, state) — three valid states for an active card:
            //   Locked      → "LOCKED" + silver gradient (lock icon comes from SetState)
            //   Replay      → "REPLAY HOLE" + silver gradient
            //   Play (next) → "NEXT" — prefab's gold/yellow colour stays untouched
            // Both Title (collapsed) and TitleExp (expanded) get the same string.
            string titleStr;
            bool titleSilver;
            if (state == HoleCardState.Locked)        { titleStr = LocalizationManager.Get("UI_LOCKED");            titleSilver = true;  }
            else if (mode == HoleCardMode.Replay)     { titleStr = LocalizationManager.Get("RESULT_REPLAY_HOLE");   titleSilver = true;  }
            else                                       { titleStr = LocalizationManager.Get("RESULT_NEXT");         titleSilver = false; }

            ApplyTitle(titleTextCollapsed, titleStr, titleSilver);
            ApplyTitle(titleTextExpanded,  titleStr, titleSilver);

            // Subtitle is always the dynamic "Lomond Country Club  - Hole N - Par P"
            string subtitleStr = $"Lomond Country Club  - Hole {hole.holeNumber} - Par {hole.par}";
            if (subtitleTextCollapsed != null) subtitleTextCollapsed.text = subtitleStr;
            if (subtitleTextExpanded  != null) subtitleTextExpanded.text  = subtitleStr;

            // Hole image
            // Don't override preserveAspect — Cesar configured the prefab Image's
            // preserveAspect / Image Type / 9-slice settings during his polish pass.
            if (holeImage != null)
            {
                Sprite img = null;
                if (!string.IsNullOrEmpty(hole.holeImageName))
                    img = Resources.Load<Sprite>($"HoleImages/{hole.holeImageName}");

                if (img == null)
                    img = Resources.Load<Sprite>("HoleImages/Missing");

                holeImage.sprite = img;
            }

            // Description
            if (descriptionText != null)
            {
                string desc = string.IsNullOrEmpty(hole.descriptionKey)
                    ? ""
                    : LocalizationManager.Get(hole.descriptionKey);
                descriptionText.text = desc;
            }

            // Rewards
            PopulateRewards(rewards, collapsedRewardSlots, collapsedRewardIcons, collapsedRewardAmounts);
            PopulateRewards(rewards, expandedRewardSlots,  expandedRewardIcons,  expandedRewardAmounts);

            // Action button label + colour + sprite (Cesar correction 8)
            if (actionButtonLabel != null)
            {
                if (mode == HoleCardMode.Replay)
                {
                    actionButtonLabel.text = LocalizationManager.Get("RESULT_REPLAY");
                    actionButtonLabel.color = new Color32(0x1E, 0x29, 0x3B, 255); // #1E293B dark navy
                }
                else
                {
                    actionButtonLabel.text = LocalizationManager.Get("BTN_START");
                    actionButtonLabel.color = new Color32(0x32, 0x15, 0x06, 255); // #321506 dark brown
                }
            }

            // Action button background sprite (Cesar correction 8)
            if (actionButton != null && actionButton.image != null)
            {
                bool replay = (mode == HoleCardMode.Replay);
                Sprite btnSprite = replay ? replayButtonSprite : playButtonSprite;
                if (btnSprite != null)
                    actionButton.image.sprite = btnSprite;
            }

            SetState(state);
        }

        /// <summary>
        /// Switch state. Caller is responsible for the single-expanded invariant.
        /// </summary>
        public void SetState(HoleCardState state)
        {
            State = state;

            bool isLocked   = state == HoleCardState.Locked;
            bool isExpanded = state == HoleCardState.Expanded;

            // Container visibility — Locked always shows collapsed content
            if (collapsedContainer != null) collapsedContainer.SetActive(!isExpanded);
            if (expandedContainer  != null) expandedContainer.SetActive(isExpanded);

            // Locked overlay
            if (lockedOverlay != null) lockedOverlay.SetActive(isLocked);

            // Tap button interactability
            if (cardTapButton != null) cardTapButton.interactable = !isLocked;

            // Locked state: dim reward icons + amounts to alpha 0.4
            float alpha = isLocked ? 0.4f : 1f;
            ApplyRewardAlpha(collapsedRewardIcons, collapsedRewardAmounts, alpha);
            ApplyRewardAlpha(expandedRewardIcons,  expandedRewardAmounts,  alpha);

            // Chevron — locked cards can't expand, so the ">" icon is misleading.
            // Hide it on locked, leave it active otherwise (the prefab default).
            // chevronExpanded is left untouched (Cesar's polish has it deactivated).
            if (chevronCollapsed != null) chevronCollapsed.SetActive(!isLocked);

            // Lock icon (in title row, only when state is Locked)
            if (lockIconCollapsed != null) lockIconCollapsed.SetActive(isLocked);
            if (lockIconExpanded  != null) lockIconExpanded.SetActive(false); // locked never expands

            if (rootRect != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(rootRect);
        }

        // ── Private helpers ───────────────────────────────────────────────────

        /// <summary>
        /// Set a title TMP's text and apply the silver gradient when needed.
        /// When useSilver is true the base colour is forced to white so the silver
        /// vertex gradient (white -> #818EA1) renders cleanly — without this the
        /// prefab's NEXT-yellow base tint multiplies into the gradient and the
        /// LOCKED / REPLAY HOLE titles read as yellow-tinted instead of silver.
        /// When useSilver is false the base colour stays whatever the prefab has
        /// (Cesar's polish set the NEXT yellow there) and the gradient is off.
        /// </summary>
        private static void ApplyTitle(TextMeshProUGUI tmp, string text, bool useSilver)
        {
            if (tmp == null) return;
            tmp.text = text;
            if (useSilver)
            {
                tmp.color = Color.white; // clean base for the silver gradient
                TextGradients.ApplySilver(tmp);
            }
            else
            {
                tmp.enableVertexGradient = false;
            }
        }

        private void PopulateRewards(List<HoleReward> rewards,
                                     GameObject[] slots, Image[] icons, TextMeshProUGUI[] amounts)
        {
            if (slots == null) return;

            for (int i = 0; i < 3; i++)
            {
                bool hasReward = (rewards != null) && i < rewards.Count;

                // demo_build_slice §3.4: the demo disables the RP / repair-kit / ball economy,
                // so hide reward slots whose type is off in the demo. No-op in the full game.
                if (hasReward && GolfinRedux.Demo.DemoGate.IsDemo && !IsRewardTypeEnabledInDemo(rewards[i].type))
                    hasReward = false;

                if (slots.Length > i && slots[i] != null)
                    slots[i].SetActive(hasReward);

                if (!hasReward) continue;

                HoleReward reward = rewards[i];

                if (icons != null && icons.Length > i && icons[i] != null)
                {
                    icons[i].sprite = GetRewardIcon(reward.type);
                }

                if (amounts != null && amounts.Length > i && amounts[i] != null)
                {
                    amounts[i].text = $"x{reward.amount}";
                }
            }
        }

        private static bool IsRewardTypeEnabledInDemo(RewardType type)
        {
            switch (type)
            {
                case RewardType.Points:    return GolfinRedux.Demo.DemoConfig.Instance.PointsEnabled;
                case RewardType.RepairKit: return GolfinRedux.Demo.DemoConfig.Instance.RepairKitsEnabled;
                case RewardType.Ball:      return GolfinRedux.Demo.DemoConfig.Instance.BallsEnabled;
                default:                   return true;
            }
        }

        private Sprite GetRewardIcon(RewardType type)
        {
            switch (type)
            {
                case RewardType.Points:    return pointsIcon;
                case RewardType.RepairKit: return repairKitIcon;
                case RewardType.Ball:      return ballIcon;
                default:                   return null;
            }
        }

        private void ApplyRewardAlpha(Image[] icons, TextMeshProUGUI[] amounts, float alpha)
        {
            if (icons != null)
            {
                foreach (var img in icons)
                {
                    if (img == null) continue;
                    Color c = img.color;
                    c.a = alpha;
                    img.color = c;
                }
            }

            if (amounts != null)
            {
                foreach (var tmp in amounts)
                {
                    if (tmp == null) continue;
                    Color c = tmp.color;
                    c.a = alpha;
                    tmp.color = c;
                }
            }
        }
    }
}
