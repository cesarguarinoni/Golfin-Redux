#nullable enable
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Golfin.Roster; // RarityHelper, CharacterRarity

namespace Golfin.Inventory
{
    /// <summary>
    /// Club card inside the Item Use modal. Shows club portrait, name, rarity badge,
    /// level, 5 stat bars (Power/Accuracy/LieResistance/Loft/Durability), Distance,
    /// disabled Level Up + Repair buttons, and a "USE REPAIR KIT" action button.
    ///
    /// Level Up and Repair are always disabled in this modal — only USE REPAIR KIT is active.
    /// USE REPAIR KIT is disabled if the club is already at full durability.
    /// </summary>
    public class ItemUseClubCard : MonoBehaviour
    {
        [Header("Card Top")]
        [SerializeField] private Image           backgroundImage  = null!; // rarity bg sprite
        [SerializeField] private Image           portraitImage    = null!;
        [SerializeField] private TextMeshProUGUI nameText         = null!; // "DRIVER\nG&F"
        [SerializeField] private TextMeshProUGUI rarityBadgeText  = null!; // "R"
        [SerializeField] private TextMeshProUGUI levelText        = null!; // "Lv10"

        [Header("Stat Bars")]
        [SerializeField] private Image           statBarPower     = null!;
        [SerializeField] private TextMeshProUGUI statNumPower     = null!;

        [SerializeField] private Image           statBarAccuracy  = null!;
        [SerializeField] private TextMeshProUGUI statNumAccuracy  = null!;

        [SerializeField] private Image           statBarLieRes    = null!;
        [SerializeField] private TextMeshProUGUI statNumLieRes    = null!;

        [SerializeField] private Image           statBarLoft      = null!;
        [SerializeField] private TextMeshProUGUI statNumLoft      = null!;

        [SerializeField] private Image           statBarDurability = null!;
        [SerializeField] private TextMeshProUGUI statNumDurability = null!;

        [Header("Distance")]
        [SerializeField] private TextMeshProUGUI? distanceValue;

        [Header("Action Buttons")]
        [SerializeField] private Button          levelUpButton      = null!;
        [SerializeField] private Button          repairButton       = null!;
        [SerializeField] private Button          useRepairKitButton = null!;
        [SerializeField] private TextMeshProUGUI useRepairKitText   = null!;

        /// <summary>Fired when "USE REPAIR KIT" is tapped.</summary>
        public event System.Action? OnUseRepairKit;

        private const int STAT_MAX = 100;
        private static readonly Color StatBarColor       = new Color(0.2f, 0.5f, 0.9f, 1f);  // blue
        private static readonly Color DurabilityOkColor  = new Color(0.2f, 0.5f, 0.9f, 1f);  // blue
        private static readonly Color DurabilityLowColor = new Color(0.9f, 0.2f, 0.2f, 1f);  // red

        /// <summary>
        /// Bind all visuals from player + template data.
        /// </summary>
        /// <param name="playerClub">Player's club instance.</param>
        /// <param name="template">Club template from CSV.</param>
        /// <param name="restorePercent">Repair kit's restore % (unused visually, kept for future tooltip).</param>
        /// <param name="needsRepair">False = already at full durability → disable USE button.</param>
        public void Initialize(PlayerClubData playerClub, ClubDataRuntime template,
                               int restorePercent, bool needsRepair)
        {
            // ── Portrait ───────────────────────────────────────────────────────
            if (portraitImage != null && template.portraitSprite != null)
                portraitImage.sprite = template.portraitSprite;

            // ── Rarity background ──────────────────────────────────────────────
            if (backgroundImage != null)
            {
                var bgSprite = Resources.Load<Sprite>($"Rarities/{template.rarity}");
                if (bgSprite != null)
                {
                    backgroundImage.sprite = bgSprite;
                    backgroundImage.color  = Color.white;
                }
            }

            // ── Name (type on top line, brand below) ───────────────────────────
            if (nameText != null)
            {
                string typeLine  = template.GetTypeLabel();
                string brandLine = template.brand.ToUpper();
                nameText.text = string.IsNullOrEmpty(brandLine)
                    ? typeLine
                    : $"{typeLine}\n{brandLine}";
            }

            // ── Rarity badge ───────────────────────────────────────────────────
            if (rarityBadgeText != null)
            {
                rarityBadgeText.text  = RarityHelper.GetRarityLabel(template.rarity);
                rarityBadgeText.color = RarityHelper.GetRarityBadgeTextColor(template.rarity);
            }

            // ── Level ──────────────────────────────────────────────────────────
            if (levelText != null)
                levelText.text = $"Lv{playerClub.currentLevel}";

            // ── Stat bars ──────────────────────────────────────────────────────
            SetStatBar(statBarPower,    statNumPower,    playerClub.GetPower(template),         STAT_MAX, StatBarColor);
            SetStatBar(statBarAccuracy, statNumAccuracy, playerClub.GetAccuracy(template),      STAT_MAX, StatBarColor);
            SetStatBar(statBarLieRes,   statNumLieRes,   playerClub.GetLieResistance(template), STAT_MAX, StatBarColor);
            SetStatBar(statBarLoft,     statNumLoft,     playerClub.GetLoft(template),          STAT_MAX, StatBarColor);

            // Durability — color based on low/ok state, shows current value only
            int curDur = playerClub.currentDurability;
            int maxDur = playerClub.maxDurability;
            if (statBarDurability != null)
            {
                statBarDurability.fillAmount = maxDur > 0 ? (float)curDur / maxDur : 0f;
                statBarDurability.color = playerClub.IsDurabilityLow ? DurabilityLowColor : DurabilityOkColor;
            }
            if (statNumDurability != null)
                statNumDurability.text = $"{curDur}";

            // ── Distance ───────────────────────────────────────────────────────
            if (distanceValue != null)
                distanceValue.text = $"{playerClub.GetDistance(template)} yd";

            // ── Buttons ────────────────────────────────────────────────────────
            // Level Up and Repair always disabled in this modal
            if (levelUpButton != null) levelUpButton.interactable = false;
            if (repairButton  != null) repairButton.interactable  = false;

            // USE REPAIR KIT — only active if club needs repair
            if (useRepairKitButton != null)
            {
                useRepairKitButton.interactable = needsRepair;
                useRepairKitButton.onClick.AddListener(() => OnUseRepairKit?.Invoke());
            }

            if (useRepairKitText != null)
                useRepairKitText.text = LocalizationManager.Get("ITEM_USE_REPAIR_KIT");
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private static void SetStatBar(Image? bar, TextMeshProUGUI? num, int value, int cap, Color color)
        {
            if (bar != null)
            {
                bar.fillAmount = cap > 0 ? (float)value / cap : 0f;
                bar.color      = color;
            }
            if (num != null)
                num.text = $"{value}";
        }
    }
}
