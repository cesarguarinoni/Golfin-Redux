#nullable enable
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Golfin.Roster;

namespace Golfin.Inventory
{
    /// <summary>
    /// Club card inside the Bag Swap/Equip modal.
    /// Same visual layout as ItemUseClubCard (portrait, stats, buttons)
    /// but action button label is configurable ("SWAP" or "EQUIP").
    /// Level Up and Repair buttons are always disabled.
    /// </summary>
    public class BagClubCard : MonoBehaviour
    {
        [Header("Card Top")]
        [SerializeField] private Image           backgroundImage  = null!;
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

            // Rarity bg
            if (backgroundImage != null)
            {
                var bgSprite = Resources.Load<Sprite>($"Rarities/{template.rarity}");
                if (bgSprite != null) { backgroundImage.sprite = bgSprite; backgroundImage.color = Color.white; }
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

        private static void SetBar(Image? bar, TextMeshProUGUI? num, int value, int cap, Color color)
        {
            if (bar != null) { bar.fillAmount = cap > 0 ? (float)value / cap : 0f; bar.color = color; }
            if (num != null) num.text = $"{value}";
        }
    }
}
