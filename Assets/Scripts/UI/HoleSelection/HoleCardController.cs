using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using GolfinRedux.UI;

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
                cardTapButton.onClick.AddListener(() => OnCardTapped?.Invoke(this));

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

            // Titles
            // IMPORTANT: do NOT overwrite the title at runtime. Cesar's polish pass set the
            // title text manually in the prefab to match the Figma reference (e.g. "NEXT")
            // — overwriting it with "PLAY HOLE" / "REPLAY HOLE" was clobbering his copy.
            // Only the subtitle gets a runtime write because it carries the dynamic
            // "Lomond Country Club  - Hole N - Par P" payload.
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
                    actionButtonLabel.text = "REPLAY";
                    actionButtonLabel.color = new Color32(0x1E, 0x29, 0x3B, 255); // #1E293B dark navy
                }
                else
                {
                    actionButtonLabel.text = "PLAY";
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

            // Chevron arrow — Cesar's polish pass deactivated this entirely (the old
            // implementation was a no-op anyway). We deliberately don't touch it here so
            // any stale GameObject reference stays in whatever active-state Cesar set in
            // the prefab. Fields kept SerializeField for backward compatibility with the
            // pre-polish auto-wire script.

            // Lock icon (in title row, only when state is Locked)
            if (lockIconCollapsed != null) lockIconCollapsed.SetActive(isLocked);
            if (lockIconExpanded  != null) lockIconExpanded.SetActive(false); // locked never expands

            if (rootRect != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(rootRect);
        }

        // ── Private helpers ───────────────────────────────────────────────────

        private void PopulateRewards(List<HoleReward> rewards,
                                     GameObject[] slots, Image[] icons, TextMeshProUGUI[] amounts)
        {
            if (slots == null) return;

            for (int i = 0; i < 3; i++)
            {
                bool hasReward = (rewards != null) && i < rewards.Count;

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
