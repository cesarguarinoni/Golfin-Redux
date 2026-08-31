#nullable enable
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Golfin.Roster;

namespace Golfin.Inventory
{
    /// <summary>
    /// Club card used in the Bag detail panel grid AND the Swap/Equip modal.
    /// Same visual layout as ItemUseClubCard (portrait, stats, buttons)
    /// but action button label is configurable ("SWAP" or "EQUIP").
    /// Level Up and Repair buttons are always disabled.
    ///
    /// Inspector wiring note:
    ///   cardTopImage  → CardTop   (the rarity-coloured image, NOT the Background container)
    ///   portraitImage → CardTop/Portrait  (or wherever the club portrait lives)
    /// </summary>
    public class BagClubCard : MonoBehaviour
    {
        [Header("Card Top")]
        [SerializeField] private Image           cardTopImage     = null!;  // wire to CardTop, NOT Background
        [SerializeField] private Image           portraitImage    = null!;
        [SerializeField] private TextMeshProUGUI nameText         = null!;
        [SerializeField] private TextMeshProUGUI rarityBadgeText  = null!;
        [SerializeField] private TextMeshProUGUI levelText        = null!;

        [Header("Stat Bars")]
        [SerializeField] private Image           statBarPower      = null!;
        [SerializeField] private TextMeshProUGUI statNumPower      = null!;
        [SerializeField] private Image           statBarAccuracy   = null!;
        [SerializeField] private TextMeshProUGUI statNumAccuracy   = null!;
        [SerializeField] private Image           statBarLieRes     = null!;
        [SerializeField] private TextMeshProUGUI statNumLieRes     = null!;
        [SerializeField] private Image           statBarLoft       = null!;
        [SerializeField] private TextMeshProUGUI statNumLoft       = null!;
        [SerializeField] private Image           statBarDurability = null!;
        [SerializeField] private TextMeshProUGUI statNumDurability = null!;

        [Header("Distance")]
        [SerializeField] private TextMeshProUGUI? distanceValue;

        [Header("Action Buttons")]
        [SerializeField] private Button          levelUpButton    = null!;
        [SerializeField] private Button          repairButton     = null!;
        [SerializeField] private Button          actionButton     = null!;  // SWAP or EQUIP
        [SerializeField] private TextMeshProUGUI actionButtonText = null!;

        public event System.Action? OnActionClicked;

        private const int STAT_MAX = 100;
        private static readonly Color StatBarColor       = new Color(0.2f, 0.5f, 0.9f, 1f);
        private static readonly Color DurabilityOkColor  = new Color(0.2f, 0.5f, 0.9f, 1f);
        private static readonly Color DurabilityLowColor = new Color(0.9f, 0.2f, 0.2f, 1f);

        public void Initialize(PlayerClubData playerClub, ClubDataRuntime template, string actionLabel)
        {
            // Portrait
            if (portraitImage != null && template.portraitSprite != null)
                portraitImage.sprite = template.portraitSprite;

            // Rarity bg — goes on CardTop image, not the Background container
            if (cardTopImage != null)
            {
                var bgSprite = Resources.Load<Sprite>($"Rarities/{template.rarity}");
                if (bgSprite != null) { cardTopImage.sprite = bgSprite; cardTopImage.color = Color.white; }
            }

            // Name
            if (nameText != null)
            {
                string typeLine = template.GetTypeLabel();
                string brand = template.brand.ToUpper();
                nameText.text = string.IsNullOrEmpty(brand) ? typeLine : $"{typeLine}\n{brand}";
            }

            // Rarity badge
            if (rarityBadgeText != null)
            {
                rarityBadgeText.text = RarityHelper.GetRarityLabel(template.rarity);
                rarityBadgeText.color = RarityHelper.GetRarityBadgeTextColor(template.rarity);
            }

            // Level
            if (levelText != null)
                levelText.text = $"Lv{playerClub.currentLevel}";

            // Stats
            SetBar(statBarPower,    statNumPower,    playerClub.GetPower(template),          STAT_MAX, StatBarColor);
            SetBar(statBarAccuracy, statNumAccuracy, playerClub.GetAccuracy(template),        STAT_MAX, StatBarColor);
            SetBar(statBarLieRes,   statNumLieRes,   playerClub.GetLieResistance(template),   STAT_MAX, StatBarColor);
            SetBar(statBarLoft,     statNumLoft,     playerClub.GetLoft(template),            STAT_MAX, StatBarColor);

            int curDur = playerClub.currentDurability;
            int maxDur = playerClub.maxDurability;
            if (statBarDurability != null)
            {
                statBarDurability.fillAmount = maxDur > 0 ? (float)curDur / maxDur : 0f;
                statBarDurability.color = playerClub.IsDurabilityLow ? DurabilityLowColor : DurabilityOkColor;
            }
            if (statNumDurability != null) statNumDurability.text = $"{curDur}";

            // Distance
            if (distanceValue != null)
                distanceValue.text = $"{playerClub.GetDistance(template)} yd";

            // Buttons
            if (levelUpButton != null) levelUpButton.interactable = false;
            if (repairButton  != null) repairButton.interactable  = false;

            if (actionButton != null)
            {
                actionButton.interactable = true;
                actionButton.onClick.RemoveAllListeners();
                actionButton.onClick.AddListener(() => OnActionClicked?.Invoke());
            }
            if (actionButtonText != null)
                actionButtonText.text = actionLabel;
        }

        // ── Prize binding (gacha_client_real_pull, after Cesar's rejection 2026-08-31) ──
        //
        // A gacha pull can pay a ball, an item, a character or a ticket, and Cesar's call is that
        // EVERY prize card is the same size and shape as the club one. So they all use THIS card —
        // which is not a stretch of it: `GachaHistoryRowBall.prefab` has nested a BagClubCard and
        // bound ball data into it since gacha_history Stage 1. This method is that pattern given a
        // name, so the four kinds share one shell instead of four hand-bound copies of its paths.
        //
        // The first attempt reused the wide Rewards-Center shop card scaled to 0.19 to fit. It was
        // the SPEC's instruction and it was wrong on sight: a 978x274 row card in a 183x410 portrait
        // slot is legible as a shape and not as text. Rejected, and rightly.

        /// <summary>Everything this card needs to draw a prize of any kind.</summary>
        public readonly struct PrizeView
        {
            public readonly Sprite?         Portrait;
            /// <summary>Card name. A <c>\n</c> gives the two-line form the club binding uses.</summary>
            public readonly string          Name;
            public readonly CharacterRarity Rarity;
            /// <summary>The top-right chip — <c>"Lv1"</c> for a club, <c>"x3"</c> for a stack.
            /// Empty hides it.</summary>
            public readonly string          Badge;
            /// <summary>The line the club card uses for distance — a restore percentage, a ticket
            /// count. Empty hides the row.</summary>
            public readonly string          Detail;
            /// <summary>Up to five stat values, or null to hide every stat row. Shorter than five
            /// hides the surplus rows rather than drawing them at zero.</summary>
            public readonly int[]?          Stats;
            /// <summary>What a full bar means for <see cref="Stats"/> — 10 for a ball, 60 for a club.</summary>
            public readonly int             StatMax;

            public PrizeView(Sprite? portrait, string name, CharacterRarity rarity,
                             string badge = "", string detail = "", int[]? stats = null, int statMax = 60)
            {
                Portrait = portrait; Name = name ?? string.Empty; Rarity = rarity;
                Badge = badge ?? string.Empty; Detail = detail ?? string.Empty;
                Stats = stats; StatMax = statMax > 0 ? statMax : 60;
            }
        }

        /// <summary>
        /// Draw an arbitrary prize on this card: same rect, same rarity frame, same badge, same
        /// stat lanes as a club — only the CONTENT differs.
        ///
        /// <para>
        /// The stat rows and the distance row are resolved BY PATH rather than by new
        /// <c>[SerializeField]</c>s, because the card already ships wired for a club and adding
        /// five row references to every existing instance to hide five rows would be a prefab
        /// change for no gain. The paths are the same ones the prize binder already uses to hide
        /// the action row, verified against BagClubCard.prefab.
        /// </para>
        /// </summary>
        public void InitializePrize(in PrizeView view)
        {
            if (portraitImage != null)
            {
                portraitImage.sprite  = view.Portrait;
                // A kind with no art must not show the PREVIOUS prize's portrait — this card is
                // re-bound in place on every pull.
                portraitImage.enabled = view.Portrait != null;
            }

            if (cardTopImage != null)
            {
                var bgSprite = Resources.Load<Sprite>($"Rarities/{view.Rarity}");
                if (bgSprite != null) { cardTopImage.sprite = bgSprite; cardTopImage.color = Color.white; }
            }

            if (nameText != null) nameText.text = view.Name;

            if (rarityBadgeText != null)
            {
                rarityBadgeText.text  = RarityHelper.GetRarityLabel(view.Rarity);
                rarityBadgeText.color = RarityHelper.GetRarityBadgeTextColor(view.Rarity);
            }

            if (levelText != null)
            {
                levelText.text = view.Badge;
                levelText.gameObject.SetActive(!string.IsNullOrEmpty(view.Badge));
            }

            // Distance row — the card's one free-text line.
            //
            // It is sized for "250 yd". A prize can put a longer string in it ("RESTORES 75%"),
            // which WRAPPED to three lines and spilled down the card. Auto-size with wrapping off
            // shrinks it to one line instead, and leaves a club's own "250 yd" pixel-identical
            // because it already fits at the authored size — so no restore is needed when this
            // same card instance is later re-bound to a club.
            if (distanceValue != null)
            {
                distanceValue.text              = view.Detail;
                distanceValue.textWrappingMode  = TMPro.TextWrappingModes.NoWrap;
                distanceValue.enableAutoSizing  = true;
                distanceValue.fontSizeMax       = distanceValue.fontSize;
                distanceValue.fontSizeMin       = 8f;
            }
            SetActiveAt(DistanceRowPath, !string.IsNullOrEmpty(view.Detail));

            // Stat lanes.
            var bars = new[] { statBarPower, statBarAccuracy, statBarLieRes, statBarLoft, statBarDurability };
            var nums = new[] { statNumPower, statNumAccuracy, statNumLieRes, statNumLoft, statNumDurability };

            for (int i = 0; i < StatRowPaths.Length; i++)
            {
                bool has = view.Stats != null && i < view.Stats.Length;
                SetActiveAt(StatRowPaths[i], has);
                if (has) SetBar(bars[i], nums[i], view.Stats![i], view.StatMax, StatBarColor);
            }

            // Display only, always — a prize card is never actionable.
            if (levelUpButton != null) levelUpButton.interactable = false;
            if (repairButton  != null) repairButton.interactable  = false;
            if (actionButton  != null) { actionButton.onClick.RemoveAllListeners(); actionButton.interactable = false; }
        }

        /// <summary>Restore the stat lanes and the distance row a <see cref="InitializePrize"/> may
        /// have hidden, so re-binding this card to a CLUB shows a whole club card again.</summary>
        public void RestoreClubRows()
        {
            SetActiveAt(DistanceRowPath, true);
            foreach (var path in StatRowPaths) SetActiveAt(path, true);
            if (levelText != null) levelText.gameObject.SetActive(true);
            if (portraitImage != null) portraitImage.enabled = true;
        }

        private const string DistanceRowPath = "Mask/Background/StatsPanel/DistanceRow";

        private static readonly string[] StatRowPaths =
        {
            "Mask/Background/StatsPanel/StatRow_Power",
            "Mask/Background/StatsPanel/StatRow_Accuracy",
            "Mask/Background/StatsPanel/StatRow_LieRes",
            "Mask/Background/StatsPanel/StatRow_Loft",
            "Mask/Background/StatsPanel/StatRow_Durability",
        };

        private void SetActiveAt(string path, bool active)
        {
            var t = transform.Find(path);
            if (t != null) t.gameObject.SetActive(active);
        }

        private static void SetBar(Image? bar, TextMeshProUGUI? num, int value, int cap, Color color)
        {
            if (bar != null) { bar.fillAmount = cap > 0 ? (float)value / cap : 0f; bar.color = color; }
            if (num != null) num.text = $"{value}";
        }
    }
}
